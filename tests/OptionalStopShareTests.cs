using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;
// The setting is the share of eligible visits that visibly receive a scheduled stop, capped by feasibility.
public sealed class OptionalStopShareTests(ITestOutputHelper output)
{
    private const double Start = 1440, Window = 600;
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
    }
    // Only some venues have validated geometry, as on the rig where most routes lack a usable spot.
    private sealed class Geometry : IOptionalStopEnvironment
    {
        public HashSet<string> Feasible = new(StringComparer.Ordinal);
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind) => kind != DwellKind.WindowShopping || !Feasible.Contains(place.Id)
            ? Array.Empty<OptionalStopSpot>() : Enumerable.Range(0, 40).Select(i => new OptionalStopSpot
            { Id = place.Id + ":" + i, TargetId = place.Id, Position = new(place.Position.X + i, 0, 1), Facing = place.Position }).ToArray();
        public OptionalStopAvailability Available(OptionalStopState stop) => Feasible.Contains(stop.PlaceId)
            ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
    }
    private sealed class Tally
    {
        public readonly HashSet<string> Eligible = new(StringComparer.Ordinal), Realised = new(StringComparer.Ordinal), FeasibleVisits = new(StringComparer.Ordinal);
        public double Share => (double)Realised.Count / Eligible.Count;
        public double FeasibleShare => (double)FeasibleVisits.Count / Eligible.Count;
    }

    // count citizens, each arriving once inside the window at one of `venues` shops; `feasibleVenues`
    // have geometry and every 20th visit is too short for even the shortest stop.
    private static (CitySimulator Sim, Geometry Env, Tally Tally) City(int percentage, int count = 1500, int venues = 40, int feasibleVenues = 14)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "c" + i.ToString("D4"), Name = "Citizen " + i,
            Age = 30, Persona = Persona.Commuter }).ToArray();
        var places = new List<Place> { new Place { Id = "home", Name = "Home", Capacity = count, Kind = PlaceKind.Home } };
        for (int v = 0; v < venues; v++)
            places.Add(new Place { Id = "shop" + v.ToString("D2"), Name = "Shop " + v, Kind = PlaceKind.Shop, Capacity = count, Position = new(1000 * (v + 1), 0, 0) });
        var state = Population.Create(identities, places, "share-tests", "0.40.4", 17, Start);
        var env = new Geometry();
        for (int v = 0; v < feasibleVenues; v++) env.Feasible.Add("shop" + (v * venues / feasibleVenues).ToString("D2"));
        var tally = new Tally();
        for (int i = 0; i < state.Citizens.Count; i++)
        {
            var c = state.Citizens[i];
            var venue = places[1 + (int)(StableRandom.Hash(c.Identity.Id + ":venue", 3) % (uint)venues)];
            double arrival = Start + 1 + StableRandom.Hash(c.Identity.Id + ":arrival", 3) % 10000 / 10000.0 * (Window - 2);
            bool brief = i % 20 == 7;
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home;
            c.DestinationId = venue.Id; c.DestinationActivity = Activity.Shop;
            c.DepartureMinute = Start; c.NextEventMinute = arrival; c.ActivityUntilMinute = arrival + (brief ? 0.5 : 2 * Window);
            c.Journey = new Walking().Plan(places[0], venue, Start, c.Preferences); c.Journey.Legs[0].Minutes = arrival - Start;
            if (!brief && env.Feasible.Contains(venue.Id)) tally.FeasibleVisits.Add(c.Identity.Id);
        }
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(env, percentage);
        Observe(sim, tally);
        return (sim, env, tally);
    }
    private static void Observe(CitySimulator sim, Tally tally) => sim.ActivityObserved += (id, c, minute) =>
    {
        if (c.Phase != CitizenPhase.AtPlace || c.Activity != Activity.Shop || c.RecentVisits.Count == 0 || c.RecentVisits[^1].AtMinute != minute) return;
        Assert.NotNull(c.OptionalStopChoice); tally.Eligible.Add(id);
        if (c.OptionalStop != null) tally.Realised.Add(id);
    };
    private Tally Run(int percentage, int venues = 40, int feasibleVenues = 14)
    {
        var (sim, _, tally) = City(percentage, venues: venues, feasibleVenues: feasibleVenues);
        sim.AdvanceTo(Start + Window);
        Assert.Equal(1500, tally.Eligible.Count);
        // A stop is only ever published for a feasible visit.
        Assert.Subset(tally.FeasibleVisits, tally.Realised);
        output.WriteLine($"setting {percentage}%: feasible {tally.FeasibleVisits.Count}/1500 ({tally.FeasibleShare:P1}), realised {tally.Realised.Count}/1500 ({tally.Share:P1})");
        return tally;
    }

    [Theory]
    [InlineData(25, 14)] // ~33% feasible, like the rig
    [InlineData(10, 14)]
    [InlineData(50, 32)] // ~76% feasible
    public void RealisedShareTracksTheSettingWhenFeasibilityAllowsIt(int percentage, int feasibleVenues)
    {
        var tally = Run(percentage, feasibleVenues: feasibleVenues);
        Assert.InRange(tally.FeasibleShare, percentage / 100.0 + 0.05, 1);
        // Tolerance: +/- 3 percentage points over 1,500 deterministic visits.
        Assert.InRange(tally.Share, percentage / 100.0 - 0.03, percentage / 100.0 + 0.03);
    }

    [Fact]
    public void ZeroSchedulesNothingAndHundredSchedulesEveryFeasibleVisitOnly()
    {
        var none = Run(0); Assert.Empty(none.Realised);
        var all = Run(100); Assert.InRange(all.FeasibleShare, 0.28, 0.38);
        Assert.Equal(all.FeasibleVisits.OrderBy(id => id), all.Realised.OrderBy(id => id));
    }

    [Theory]
    [InlineData(50)] [InlineData(75)]
    public void TargetAboveFeasibleShareIsCappedAtFeasibility(int percentage)
    {
        var tally = Run(percentage);
        Assert.True(tally.FeasibleShare < percentage / 100.0);
        Assert.InRange(tally.Share, tally.FeasibleShare - 0.03, tally.FeasibleShare);
    }

    [Fact]
    public void SameSeedSlicedBudgetsAndReloadReproduceTheSameDecisionsWithoutRerolling()
    {
        var (whole, _, _) = City(25, 400); whole.AdvanceTo(Start + Window / 2);
        var (again, _, _) = City(25, 400); again.AdvanceTo(Start + Window / 2);
        Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(again.State));
        whole.AdvanceTo(Start + Window);

        var (sliced, env, _) = City(25, 400); while (!sliced.AdvanceTo(Start + Window / 2, 7).CaughtUp) { }
        string saved = SaveCodec.Write(sliced.State);
        var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking());
        // A different rate on reload neither re-rolls nor drops saved stops, choices or handed-on chances.
        loaded.AttachOptionalStops(env, 100); loaded = loaded.RebuildRoutes(new Walking()); loaded.RefreshOptionalStops();
        Assert.Equal(saved, SaveCodec.Write(loaded.State));
        Assert.Contains(loaded.State.Citizens, c => c.OptionalStop != null);
        Assert.Contains(loaded.State.Citizens, c => c.OptionalStopChoice == false);
        loaded.AttachOptionalStops(env, 25); loaded.AdvanceTo(Start + Window);
        Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(loaded.State));
    }

    [Fact]
    public void ChancesHeldByInfeasibleVisitsAreHandedOnOnlyOnce()
    {
        var (sim, _, tally) = City(25); sim.AdvanceTo(Start + Window);
        // Every realised stop is backed by exactly one accepted chance; a handed-on chance is cleared on its holder.
        var accepted = sim.State.Citizens.Count(c => c.OptionalStopChoice == true);
        Assert.Equal(tally.Realised.Count + sim.State.Citizens.Count(c => c.OptionalStopChoice == true && c.OptionalStop == null), accepted);
        Assert.All(sim.State.Citizens.Where(c => c.OptionalStop != null), c => Assert.True(c.OptionalStopChoice));
    }
}
