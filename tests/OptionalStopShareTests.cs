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
    // Leisure venues listed in Chat offer only two conversation spots, like a nightclub facade without a park.
    private sealed class Geometry : IOptionalStopEnvironment
    {
        public HashSet<string> Feasible = new(StringComparer.Ordinal), Chat = new(StringComparer.Ordinal);
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
            => kind == DwellKind.WindowShopping && Feasible.Contains(place.Id) ? Row(place, 40)
            : kind == DwellKind.ChatOutsideVenue && Chat.Contains(place.Id) ? Row(place, 2) : Array.Empty<OptionalStopSpot>();
        private static OptionalStopSpot[] Row(Place place, int count) => Enumerable.Range(0, count).Select(i => new OptionalStopSpot
            { Id = place.Id + ":" + i, TargetId = place.Id, Position = new(place.Position.X + i, 0, 1), Facing = place.Position }).ToArray();
        public OptionalStopAvailability Available(OptionalStopState stop) => Feasible.Contains(stop.PlaceId) || Chat.Contains(stop.PlaceId)
            ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
    }
    private sealed class Tally
    {
        public readonly HashSet<string> Eligible = new(StringComparer.Ordinal), Realised = new(StringComparer.Ordinal), FeasibleVisits = new(StringComparer.Ordinal);
        public int Drawn;
        public double Share => (double)Realised.Count / Eligible.Count;
        public double FeasibleShare => (double)FeasibleVisits.Count / Eligible.Count;
    }
    // Admission timing of the shop visits.
    public enum Timing { Uniform, SeparatedWaves }

    // count citizens, each arriving once at one of `venues` shops inside `width` minutes and staying `stay`
    // minutes; `feasibleVenues` have geometry and every 20th visit is too short for even the shortest stop.
    // SeparatedWaves admits every infeasible visit in the first 100 minutes and every feasible one 400 minutes later.
    private static (CitySimulator Sim, Geometry Env, Tally Tally) City(int percentage, int count = 1500, int venues = 40, int feasibleVenues = 14,
        double stay = 2 * Window, double width = Window, Timing timing = Timing.Uniform)
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
            double arrival = Start + 1 + StableRandom.Hash(c.Identity.Id + ":arrival", 3) % 10000 / 10000.0 * (width - 2);
            if (timing == Timing.SeparatedWaves) arrival = Start + 1 + (env.Feasible.Contains(venue.Id) ? 400 : 0) + (arrival - Start - 1) / width * 100;
            bool brief = i % 20 == 7;
            // No routine sends anyone out again: each citizen makes exactly this one visit.
            c.ShopId = ""; c.LeisureId = "";
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home;
            c.DestinationId = venue.Id; c.DestinationActivity = Activity.Shop;
            c.DepartureMinute = Start; c.NextEventMinute = arrival; c.ActivityUntilMinute = arrival + (brief ? 0.5 : stay);
            c.Journey = new Walking().Plan(places[0], venue, Start, c.Preferences); c.Journey.Legs[0].Minutes = arrival - Start;
            if (!brief && env.Feasible.Contains(venue.Id)) tally.FeasibleVisits.Add(c.Identity.Id);
            if (OptionalStopPolicy.Choose(OptionalStopPolicy.VisitKey(new CitizenState { Identity = c.Identity, PlaceId = venue.Id }, arrival), state.Seed, percentage)) tally.Drawn++;
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
    private static int Pending(CitySimulator sim) => sim.State.Citizens.Count(c => c.OptionalStopChoice == true && c.OptionalStop == null);
    private Tally Run(int percentage, int venues = 40, int feasibleVenues = 14, double stay = 2 * Window, double width = Window, Timing timing = Timing.Uniform)
    {
        var (sim, _, tally) = City(percentage, venues: venues, feasibleVenues: feasibleVenues, stay: stay, width: width, timing: timing);
        while (!sim.AdvanceTo(Start + width + stay).CaughtUp) { }
        Assert.Equal(1500, tally.Eligible.Count);
        // A stop is only ever published for a feasible visit.
        Assert.Subset(tally.FeasibleVisits, tally.Realised);
        // One visit per citizen: every draw is realised once or still pending, and below 100 none is dropped.
        // At 100 nothing can be handed on, so an unused chance ends with its visit.
        var chances = sim.OptionalStopChances;
        Assert.Equal(tally.Drawn, tally.Realised.Count + Pending(sim) + chances.Dropped);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1500, Drawn = tally.Drawn, Realised = tally.Realised.Count,
            HandedOn = chances.HandedOn, Dropped = percentage == 100 ? chances.Dropped : 0 }, chances);
        Assert.True(sim.OptionalStopIndexesMatchState());
        output.WriteLine($"setting {percentage}% stay {stay} width {width} {timing}: feasible {tally.FeasibleVisits.Count}/1500 ({tally.FeasibleShare:P1}), "
            + $"drawn {tally.Drawn}, realised {tally.Realised.Count}/1500 ({tally.Share:P1}), handed on {sim.OptionalStopChances.HandedOn}, pending {Pending(sim)}");
        return tally;
    }
    // Tolerance: +/- 3 percentage points of min(setting, feasible share) over 1,500 deterministic visits.
    private static void AssertTracks(Tally tally, int percentage)
    {
        double target = Math.Min(percentage / 100.0, tally.FeasibleShare);
        Assert.InRange(tally.Share, target - 0.03, Math.Min(target + 0.03, tally.FeasibleShare));
    }

    // ob4: the rig's 42,100 default draws came to 24.56% for its one fixed seed. Over visit keys of the
    // rig's shape, the draw share across seeds averages the setting, with binomial seed-to-seed spread;
    // a low share for one seed is sampling, not a hashing or rounding bias.
    [Theory]
    [InlineData(10)] [InlineData(25)] [InlineData(50)]
    public void DrawShareAcrossSeedsIsUnbiasedForRigShapedVisitKeys(int percentage)
    {
        string[] streets = { "thirdstreet", "fourthstreet", "tenthstreet", "twentyfourthstreet" };
        var keys = Enumerable.Range(0, 5000).Select(i => (i % 3 == 0 ? "pc:" + i % 600 : "c" + i % 400)
            + ":address:ba%3Astreet_" + streets[i % 4] + ":" + i % 50 + ":shop:"
            + (9290 + i * 0.3711 + i % 7 * 0.0131).ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var shares = Enumerable.Range(0, 200).Select(seed => keys.Count(k => OptionalStopPolicy.Choose(k, seed * 7919 + 13, percentage))
            / (double)keys.Length).ToArray();
        double p = percentage / 100.0, mean = shares.Average(), binomial = Math.Sqrt(p * (1 - p) / keys.Length);
        double spread = Math.Sqrt(shares.Sum(s => (s - mean) * (s - mean)) / (shares.Length - 1));
        output.WriteLine($"{percentage}%: mean {mean:P3}, seed spread {spread:P3}, binomial {binomial:P3}");
        Assert.InRange(mean, p - 4 * binomial / Math.Sqrt(shares.Length), p + 4 * binomial / Math.Sqrt(shares.Length));
        Assert.InRange(spread, 0.75 * binomial, 1.25 * binomial);
    }

    [Theory]
    [InlineData(25, 14)] // ~33% feasible, like the rig
    [InlineData(10, 14)]
    [InlineData(50, 32)] // ~76% feasible
    public void RealisedShareTracksTheSettingWhenFeasibilityAllowsIt(int percentage, int feasibleVenues)
    {
        var tally = Run(percentage, feasibleVenues: feasibleVenues);
        Assert.InRange(tally.FeasibleShare, percentage / 100.0 + 0.05, 1);
        AssertTracks(tally, percentage);
    }

    // Pending chances survive the end of their visit, so short stays, sparse admissions and infeasible
    // visits that all end before the feasible ones arrive still reach the target (ob1 reproductions).
    [Theory]
    [InlineData(25, 45, 600, Timing.SeparatedWaves)]   // fe72966: 8.3%
    [InlineData(50, 45, 600, Timing.SeparatedWaves)]   // fe72966: 17.2% against a 32.7% cap
    [InlineData(25, 45, 6000, Timing.Uniform)]         // sparse; fe72966: 20.6%
    [InlineData(25, 45, 15000, Timing.Uniform)]        // sparser; fe72966: 18.4%
    [InlineData(25, 3, 6000, Timing.Uniform)]          // short stays
    [InlineData(10, 3, 15000, Timing.SeparatedWaves)]  // short stays, separated
    public void RealisedShareSurvivesFiniteStaysSparseAndSeparatedAdmissions(int percentage, double stay, double width, Timing timing)
        => AssertTracks(Run(percentage, stay: stay, width: width, timing: timing), percentage);

    [Theory]
    [InlineData(Timing.Uniform)] [InlineData(Timing.SeparatedWaves)]
    public void ZeroSchedulesNothingAndHundredSchedulesEveryFeasibleVisitOnly(Timing timing)
    {
        var none = Run(0, stay: 45, timing: timing); Assert.Empty(none.Realised);
        var all = Run(100, stay: 45, timing: timing); Assert.InRange(all.FeasibleShare, 0.28, 0.38);
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
    public void EveryDrawIsRealisedOnceOrStillPendingAndEachHandOnMovesExactlyOneChance()
    {
        // Draws are counted independently of the scheduler, from each visit's key.
        var (sim, _, tally) = City(25, timing: Timing.SeparatedWaves, stay: 45);
        var before = sim.State.Citizens.ToDictionary(c => c.Identity.Id, c => (c.OptionalStopChoice, Stop: c.OptionalStop != null));
        int handOns = 0;
        while (sim.State.Citizens.Any(c => c.NextEventMinute <= Start + 700))
        {
            sim.AdvanceTo(sim.State.Citizens.Min(c => c.NextEventMinute), 1);
            var now = sim.State.Citizens.ToDictionary(c => c.Identity.Id, c => (c.OptionalStopChoice, Stop: c.OptionalStop != null));
            var donors = now.Keys.Where(id => before[id].OptionalStopChoice == true && !before[id].Stop && now[id].OptionalStopChoice == false).ToArray();
            var newStops = now.Keys.Where(id => !before[id].Stop && now[id].Stop).ToArray();
            // A donor is never also a recipient, a recipient never had a pending chance, and one event moves at most one chance.
            Assert.InRange(donors.Length, 0, 1);
            if (donors.Length == 1)
            {
                handOns++;
                var taker = Assert.Single(newStops);
                Assert.NotEqual(donors[0], taker);
                Assert.NotEqual(true, before[taker].OptionalStopChoice);
                Assert.True(now[taker].OptionalStopChoice);
                Assert.False(now[donors[0]].Stop);
            }
            before = now;
        }
        Assert.Equal(1500, tally.Eligible.Count);
        Assert.True(handOns > 200, "expected the infeasible wave's chances to be handed on: " + handOns);
        Assert.Equal(handOns, sim.OptionalStopChances.HandedOn);
        Assert.Equal(tally.Drawn, tally.Realised.Count + Pending(sim));
        Assert.Equal(tally.Drawn, sim.OptionalStopChances.Drawn);
        Assert.Equal(0, sim.OptionalStopChances.Dropped);
        Assert.All(sim.State.Citizens.Where(c => c.OptionalStop != null), c => Assert.True(c.OptionalStopChoice));
    }

    [Fact]
    public void FirstHandOnSurvivesSaveAndReloadOnEitherSideAtAnyRate()
    {
        var (sim, env, _) = City(25, 400, timing: Timing.SeparatedWaves, stay: 45);
        for (int step = 0; step < 5000; step++)
        {
            string before = SaveCodec.Write(sim.State);
            var old = sim.State.Citizens.ToDictionary(c => c.Identity.Id, c => c.OptionalStopChoice);
            double next = sim.State.Citizens.Min(c => c.NextEventMinute);
            sim.AdvanceTo(next, 1);
            if (!sim.State.Citizens.Any(c => old[c.Identity.Id] == true && c.OptionalStopChoice == false)) continue;
            // Saved just before: the reload makes the same hand-on.
            var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 25);
            loaded.AdvanceTo(next, 1);
            string after = SaveCodec.Write(sim.State);
            Assert.Equal(after, SaveCodec.Write(loaded.State));
            // Saved just after: reload at another rate changes nothing until resumed at 25.
            read = SaveCodec.Read(after); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 100); loaded = loaded.RebuildRoutes(new Walking());
            Assert.Equal(after, SaveCodec.Write(loaded.State));
            loaded.AttachOptionalStops(env, 25);
            sim.AdvanceTo(Start + Window); loaded.AdvanceTo(Start + Window);
            Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
            Assert.True(loaded.OptionalStopIndexesMatchState());
            return;
        }
        Assert.Fail("No hand-on occurred");
    }

    [Fact]
    public void ReloadAtZeroNeverSpendsChancesSavedPendingAtAHigherRate()
    {
        var (sim, env, _) = City(25, timing: Timing.SeparatedWaves, stay: 45);
        sim.AdvanceTo(Start + 300); // the infeasible wave has left; its chances are pending at home
        var pending = sim.State.Citizens.Where(c => c.OptionalStopChoice == true && c.OptionalStop == null).Select(c => c.Identity.Id).ToArray();
        Assert.True(pending.Length > 200, "pending " + pending.Length);
        Assert.All(pending, id => Assert.Equal(Activity.Home, sim.State.Citizens.First(c => c.Identity.Id == id).Activity));
        string saved = SaveCodec.Write(sim.State);
        CitySimulator Load(int rate)
        {
            var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, rate);
            loaded.AdvanceTo(Start + Window + 45); return loaded;
        }
        var zero = Load(0);
        Assert.True(zero.OptionalStopChances.Eligible > 0);
        Assert.Equal(new OptionalStopChanceTally { Eligible = zero.OptionalStopChances.Eligible }, zero.OptionalStopChances);
        Assert.Equal(pending, zero.State.Citizens.Where(c => c.OptionalStopChoice == true && c.OptionalStop == null).Select(c => c.Identity.Id));
        // Control: the same save resumed at 25 does spend them.
        var resumed = Load(25).OptionalStopChances;
        Assert.True(resumed.HandedOn >= pending.Length - 5 && resumed.Realised > pending.Length, $"handed on {resumed.HandedOn}, realised {resumed.Realised}");
    }

    // Conversation-only feasibility: 750 leisure venues with two chat spots and no resting or audience spot;
    // first members arrive at 1450, second members at 1460, and both may stay until 1580. With `spread`,
    // venue v's pair arrives (v % 10) * 100 minutes later.
    private static (CitySimulator Sim, Geometry Env, int Drawn) Pairs(int percentage, int venues = 750, bool spread = false, string prefix = "c")
    {
        var people = Enumerable.Range(0, 2 * venues).Select(i => new CitizenIdentity { Id = prefix + i.ToString("D4"), Name = "Citizen " + i, Age = 30, Persona = Persona.Commuter }).ToArray();
        var places = new List<Place> { new Place { Id = "home", Name = "Home", Capacity = 2 * venues, Kind = PlaceKind.Home } };
        for (int i = 0; i < venues; i++) places.Add(new Place { Id = "venue" + i.ToString("D3"), Name = "Venue " + i, Kind = PlaceKind.Leisure, Capacity = 2, Position = new(1000 * (i + 1), 0, 0) });
        var state = Population.Create(people, places, "share-chat", "0.40.4", 17, Start);
        var env = new Geometry(); int drawn = 0;
        foreach (var place in places.Skip(1)) env.Chat.Add(place.Id);
        for (int i = 0; i < 2 * venues; i++)
        {
            var c = state.Citizens[i]; var place = places[1 + i / 2]; double offset = spread ? i / 2 % 10 * 100 : 0;
            double arrival = (i % 2 == 0 ? 1450 : 1460) + offset; c.ShopId = ""; c.LeisureId = "";
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home; c.DestinationId = place.Id; c.DestinationActivity = Activity.Leisure;
            c.DepartureMinute = Start; c.NextEventMinute = arrival; c.ActivityUntilMinute = 1580 + offset;
            c.Journey = new Walking().Plan(places[0], place, Start, c.Preferences); c.Journey.Legs[0].Minutes = arrival - Start;
            if (OptionalStopPolicy.Choose(OptionalStopPolicy.VisitKey(new CitizenState { Identity = c.Identity, PlaceId = place.Id }, arrival), 17, percentage)) drawn++;
        }
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(env, percentage);
        return (sim, env, drawn);
    }
    private static void AssertConversationsAreWellFormed(CitySimulator sim)
    {
        var byId = sim.State.Citizens.ToDictionary(c => c.Identity.Id);
        foreach (var c in sim.State.Citizens.Where(c => c.OptionalStop != null))
        {
            var stop = c.OptionalStop!; Assert.Equal(DwellKind.ChatOutsideVenue, stop.Kind);
            Assert.Equal(2, stop.Members.Count); Assert.Contains(c.Identity.Id, stop.Members);
            var other = byId[stop.Members.Single(id => id != c.Identity.Id)].OptionalStop!;
            Assert.Equal(stop.Members, other.Members); Assert.Equal(stop.TargetId, other.TargetId);
            Assert.Equal(stop.StartMinute, other.StartMinute); Assert.Equal(stop.EndMinute, other.EndMinute);
            Assert.True(Position.Distance(stop.Position, other.Position) >= 0.65);
            Assert.True(stop.EndMinute <= c.ActivityUntilMinute);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(25)] [InlineData(50)] [InlineData(100)]
    public void ConversationOnlyFeasibleVisitsTrackTheSetting(int percentage) // fe72966: 6.0% at 25
    {
        var (sim, _, drawn) = Pairs(percentage);
        sim.AdvanceTo(1460);
        int stops = sim.State.Citizens.Count(c => c.OptionalStop != null);
        output.WriteLine($"setting {percentage}%: drawn {drawn}, stops {stops}/1500 ({stops / 1500.0:P1}), pending {Pending(sim)}, handed on {sim.OptionalStopChances.HandedOn}");
        // Every stop is one chance: the visit's own or a handed-on one, plus the waiting partner's own.
        Assert.Equal(drawn, stops + Pending(sim));
        AssertConversationsAreWellFormed(sim);
        Assert.True(sim.OptionalStopIndexesMatchState());
        Assert.InRange(stops / 1500.0, percentage / 100.0 - 0.03, percentage / 100.0 + 0.03);
    }

    [Fact]
    public void ConversationDecisionsSurviveSaveAndReloadBetweenTheWaves()
    {
        var (whole, env, _) = Pairs(25); whole.AdvanceTo(1460);
        var (sim, _, _) = Pairs(25); sim.AdvanceTo(1455);
        Assert.True(Pending(sim) > 100);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 25);
        loaded.AdvanceTo(1460);
        string after = SaveCodec.Write(loaded.State);
        Assert.Equal(SaveCodec.Write(whole.State), after);
        read = SaveCodec.Read(after); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 25); loaded.RefreshOptionalStops();
        Assert.Equal(after, SaveCodec.Write(loaded.State));
        AssertConversationsAreWellFormed(loaded);
    }

    // Shop visits with window-shopping geometry at 14 of 40 venues, together with leisure pairs that can only
    // converse. The feasible share is what 100 realises.
    [Theory]
    [InlineData(10)] [InlineData(25)]
    public void MixedIndividualAndConversationVisitsTrackTheSetting(int percentage)
    {
        (int Eligible, int Stops) Mixed(int rate)
        {
            var (shops, env, tally) = City(rate, 1000, stay: 45, width: 1200);
            var (pairs, _, _) = Pairs(rate, 250, spread: true, prefix: "p");
            // One city: the leisure pairs join the shop citizens, sharing the pending chances.
            var state = shops.State;
            state.Places.AddRange(pairs.State.Places.Where(p => p.Kind == PlaceKind.Leisure));
            state.Citizens.AddRange(pairs.State.Citizens);
            foreach (var place in state.Places.Where(p => p.Kind == PlaceKind.Leisure)) env.Chat.Add(place.Id);
            var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(env, rate);
            var eligible = new HashSet<string>(StringComparer.Ordinal); var stops = new HashSet<string>(StringComparer.Ordinal); int chats = 0;
            var byId = state.Citizens.ToDictionary(c => c.Identity.Id);
            sim.ActivityObserved += (id, c, minute) =>
            {
                if (c.Phase != CitizenPhase.AtPlace || c.RecentVisits.Count == 0 || c.RecentVisits[^1].AtMinute != minute || c.OptionalStopChoice == null) return;
                eligible.Add(id);
                if (c.OptionalStop == null) return;
                foreach (var member in c.OptionalStop.Members.DefaultIfEmpty(id)) if (stops.Add(member) && c.OptionalStop.Kind == DwellKind.ChatOutsideVenue) chats++;
            };
            while (!sim.AdvanceTo(Start + 1200 + 45).CaughtUp) { }
            Assert.True(sim.OptionalStopIndexesMatchState());
            output.WriteLine($"setting {rate}%: eligible {eligible.Count}, stops {stops.Count} ({(double)stops.Count / eligible.Count:P1}), "
                + $"in conversations {chats}, handed on {sim.OptionalStopChances.HandedOn}, pending {Pending(sim)}");
            Assert.True(rate == 0 || chats > 0);
            return (eligible.Count, stops.Count);
        }
        var all = Mixed(100); var share = Mixed(percentage);
        Assert.Equal(1500, all.Eligible); Assert.Equal(1500, share.Eligible);
        double target = Math.Min(percentage / 100.0, (double)all.Stops / all.Eligible);
        Assert.InRange((double)share.Stops / share.Eligible, target - 0.03, target + 0.03);
    }
}
