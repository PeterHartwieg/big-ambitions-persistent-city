using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PresentationGridTests
{
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p)
        {
            var a = from.Position; var b = to.Position;
            var corner = new Position(b.X, 0, a.Z); var near = new Position(b.X - 7.5, 0, b.Z - 3.25);
            return new Route { Legs =
            {
                new RouteLeg { From = a, To = corner, Mode = TravelMode.Walk, Minutes = Math.Max(1, Math.Abs(b.X - a.X) / 80) },
                new RouteLeg { From = corner, To = near, Mode = TravelMode.Walk, Minutes = Math.Max(1, Math.Abs(b.Z - a.Z) / 80) },
                new RouteLeg { From = near, To = b, Mode = TravelMode.Walk, Minutes = 1 }
            } };
        }
    }
    private sealed class Geometry : IOptionalStopEnvironment
    {
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
            => Enumerable.Range(0, kind == DwellKind.Audience || kind == DwellKind.Resting ? 0 : 4).Select(i => new OptionalStopSpot
            { Id = place.Id + ":" + kind + ":" + i, TargetId = place.Id, Position = new(place.Position.X + i, 0, place.Position.Z + 2 + (int)kind * 3),
                Facing = place.Position }).ToArray();
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }

    private static CitySimulator Spread(int count = 300, string seed = "f2f-grid")
    {
        var personas = (Persona[])Enum.GetValues(typeof(Persona));
        var ids = Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "r" + i.ToString("D4"), Name = "R" + i,
            Age = 20 + i % 50, Persona = personas[i % personas.Length] }).ToArray();
        var places = new List<Place>();
        for (int i = 0; i < 20; i++) places.Add(new Place { Id = "home" + i.ToString("D2"), Name = "H", Kind = PlaceKind.Home, Capacity = 40, Position = new(i * 71.3 - 600, 0, (i % 4) * 128 - 500) });
        for (int i = 0; i < 10; i++) places.Add(new Place { Id = "work" + i.ToString("D2"), Name = "W", Kind = PlaceKind.Work, Capacity = 80, Position = new(i * 128 - 640, 0, 512), OpenMinute = 360, CloseMinute = 1320 });
        for (int i = 0; i < 2; i++) places.Add(new Place { Id = "school" + i, Name = "S", Kind = PlaceKind.Study, Capacity = 200, Position = new(i * 300 + 0.004, 0, 640) });
        for (int i = 0; i < 8; i++) places.Add(new Place { Id = "shop" + i.ToString("D2"), Name = "Sh", Kind = PlaceKind.Shop, Capacity = 80, Position = new(i * 97 - 400, 0, 200 - i * 33), OpenMinute = 480, CloseMinute = 1260 });
        for (int i = 0; i < 4; i++) places.Add(new Place { Id = "fun" + i.ToString("D2"), Name = "L", Kind = PlaceKind.Leisure, Capacity = 80, Position = new(i * 190 - 300, 0, -200), OpenMinute = 600, CloseMinute = 1439 });
        for (int i = 0; i < 4; i++) places.Add(new Place { Id = "scene-park:p" + i, Name = "Park", Kind = PlaceKind.Leisure, Capacity = 80, Position = new(i * 256 - 400, 0, -700) });
        var state = Population.Create(ids, places, seed, "0.40.4", 29, 1440 * 3);
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(new Geometry(), 100);
        return sim;
    }

    private static CitySimulator Reload(CitySimulator sim)
    {
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(new Geometry(), 100);
        return loaded;
    }

    private static readonly double[] Radii = { 0, 60, 120, 450 };

    private static string[] Within(CitySimulator sim, IEnumerable<CitizenState> roster, Position focus, double radius)
        => roster.Where(c => !(c.Phase == CitizenPhase.Travelling && c.Journey == null))
            .Select(c => (c, d: Position.Distance(sim.PositionOf(c), focus))).Where(x => x.d <= radius)
            .OrderBy(x => x.d).ThenBy(x => x.c.Identity.Id, StringComparer.Ordinal).Select(x => x.c.Identity.Id).ToArray();

    private static string[] Waiting(CitySimulator sim, IEnumerable<CitizenState> roster, Position focus, double radius)
        => roster.Where(c => c.Phase == CitizenPhase.WaitingForOpening && Position.Distance(sim.State.Places.Single(p => p.Id == c.PlaceId).Position, focus) <= radius)
            .Select(c => c.Identity.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    private static int pruned;

    private static void AssertGrid(CitySimulator sim, Random rng, int foci)
    {
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        var index = sim.ActiveJourneys; var grid = index.Grid;
        Assert.True(grid.Matches(index.BodyCandidates, true));
        Assert.Equal(index.BodyCandidates.Count, grid.Count);
        Assert.All(index.BodyCandidates, c => Assert.True(grid.Contains(c)));
        var output = new List<IPersonView>();
        var points = new List<Position>();
        for (int i = 0; i < foci; i++) points.Add(new Position(rng.NextDouble() * 1600 - 800, rng.NextDouble() * 3, rng.NextDouble() * 1600 - 800));
        points.Add(new Position(64 * rng.Next(-10, 10), 0, Math.BitDecrement(64.0 * rng.Next(-10, 10))));
        if (index.BodyCandidates.Count != 0) points.Add(sim.PositionOf(index.BodyCandidates[rng.Next(index.BodyCandidates.Count)]));
        foreach (var focus in points)
        foreach (double radius in Radii)
        {
            var near = grid.Query(focus, radius, output).Cast<CitizenState>().ToList();
            Assert.Equal(near.Count, near.Distinct().Count());
            Assert.All(near, c => Assert.True(index.Contains(c) && index.BodyCandidates.Contains(c)));
            if (near.Count < index.BodyCandidates.Count) pruned++;
            Assert.Equal(Within(sim, index.BodyCandidates, focus, radius), Within(sim, near, focus, radius));
            Assert.Equal(Waiting(sim, index.BodyCandidates, focus, radius), Waiting(sim, near, focus, radius));
        }
    }

    [Fact]
    public void GridEqualsFullScanAfterEveryEventSliceSkipReloadAndRebuild()
    {
        var rng = new Random(2026_11_08);
        var sim = Spread();
        AssertGrid(sim, rng, 8);
        int observed = 0, maxCandidates = 0, maxCells = 0;
        var live = sim;
        sim.ActivityObserved += (_, _, _) =>
        {
            observed++;
            if (observed % 3 == 0) AssertGrid(live, rng, 1); else Assert.True(live.ActiveJourneyIndexMatchesState());
            maxCandidates = Math.Max(maxCandidates, live.ActiveJourneys.BodyCandidates.Count);
            maxCells = Math.Max(maxCells, live.ActiveJourneys.Grid.Registrations);
        };
        double target = sim.State.Minute + 1440;
        int slices = 0;
        while (!sim.IsCaughtUpTo(target)) { sim.AdvanceFrameSlice(target); if (++slices % 25 == 0) AssertGrid(sim, rng, 2); }
        Assert.True(observed > 800, "observed " + observed);
        Assert.True(maxCandidates > 20 && maxCells > maxCandidates, $"coverage candidates {maxCandidates}, registrations {maxCells}");
        Assert.True(pruned > 0);
        double skip = sim.State.Minute + 2 * 1440 + 437;
        while (!sim.AdvanceTo(skip).CaughtUp) { }
        AssertGrid(sim, rng, 8);
        var loaded = Reload(sim);
        AssertGrid(loaded, rng, 8);
        var rebuilt = live = sim.RebuildRoutes(new Walking());
        AssertGrid(rebuilt, rng, 8);
        foreach (double step in new[] { 13.5, 240, 600 })
        {
            rebuilt.AdvanceTo(rebuilt.State.Minute + step); loaded.AdvanceTo(loaded.State.Minute + step);
            AssertGrid(rebuilt, rng, 4); AssertGrid(loaded, rng, 4);
        }
        Assert.Equal(SaveCodec.Write(rebuilt.State), SaveCodec.Write(loaded.State));
    }

    private static CitySimulator Travellers(params string[] ids)
    {
        var state = Population.Create(ids.Select((id, i) => new CitizenIdentity { Id = id, Name = "P" + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }),
            new[]
            {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = ids.Length, Position = new Position(0, 0, 0) },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = ids.Length, Position = new Position(1000, 0, 300) }
            }, "f2f-travellers", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + 12; c.ActivityUntilMinute = 2880;
            c.Journey = new Route { Legs = { new RouteLeg { From = state.Places[0].Position, To = state.Places[1].Position, Minutes = 12, Mode = TravelMode.Walk } } };
        }
        return new CitySimulator(state, new Walking());
    }

    private static bool Found(CitySimulator sim, CitizenState c, Position focus, double radius)
        => sim.ActiveJourneys.Grid.Query(focus, radius, new List<IPersonView>()).Contains(c);

    [Fact]
    public void FilingHappensOnlyOnPhaseOrJourneyChange()
    {
        var sim = Travellers("c0", "c1", "c2");
        var grid = sim.ActiveJourneys.Grid;
        long filed = grid.Filings;
        Assert.Equal(3, filed);
        for (int i = 1; i <= 40; i++)
        {
            sim.AdvanceFrameSlice(1440 + i * 0.25);
            _ = grid.Query(sim.PositionOf(sim.State.Citizens[0]), 120, new List<IPersonView>());
            Assert.True(sim.ActiveJourneyIndexMatchesState());
        }
        Assert.Equal(filed, grid.Filings);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        sim.AdvanceTo(1500);
        Assert.Equal(0, grid.Count); Assert.Equal(0, grid.Registrations); Assert.Equal(filed, grid.Filings);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
    }

    [Fact]
    public void ReplacedJourneyIsRefiledAndDriftFallsBack()
    {
        var sim = Travellers("c0", "c1");
        var c = sim.State.Citizens[1];
        var far = new Position(-2000, 0, -2000);
        Assert.True(Found(sim, c, new Position(500, 0, 150), 1));
        Assert.False(Found(sim, c, far, 0));
        long filed = sim.ActiveJourneys.Grid.Filings;
        c.Journey = new Route { Legs = { new RouteLeg { From = far, To = new Position(-1800, 0, -2100), Minutes = 4, Mode = TravelMode.Walk } } }.Commit();
        Assert.False(sim.ActiveJourneyIndexMatchesState());
        sim.ActiveJourneys.File(c);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        Assert.Equal(filed + 1, sim.ActiveJourneys.Grid.Filings);
        Assert.True(Found(sim, c, far, 0));
        Assert.False(Found(sim, c, new Position(500, 0, 150), 0));
        sim.ActiveJourneys.File(c); Assert.Equal(filed + 1, sim.ActiveJourneys.Grid.Filings);
        c.Phase = CitizenPhase.WaitingForOpening; c.Journey = null; c.PlaceId = "work";
        sim.ActiveJourneys.File(c);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        Assert.True(Found(sim, c, new Position(1000, 0, 300), 0));
        sim.State.Places.Single(p => p.Id == "work").Position = new Position(1100, 0, 300);
        Assert.False(sim.ActiveJourneyIndexMatchesState());
        sim.ActiveJourneys.File(c);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        Assert.True(Found(sim, c, new Position(1100, 0, 300), 0));
    }

    [Fact]
    public void EveryPointOnACommittedLegIsInAQueriedCell()
    {
        var rng = new Random(64);
        var index = new ActiveJourneyIndex();
        var people = new List<CitizenState>();
        for (int i = 0; i < 400; i++)
        {
            var route = new Route();
            var at = i % 4 == 0 ? new Position(64 * rng.Next(-20, 20), 0, 64 * rng.Next(-20, 20)) : new Position(rng.NextDouble() * 2000 - 1000, 0, rng.NextDouble() * 2000 - 1000);
            for (int l = 0; l < 1 + i % 5; l++)
            {
                var to = ((i + l) % 6) switch
                {
                    0 => new Position(at.X, 0, at.Z + rng.Next(-5, 5) * 64),
                    1 => new Position(at.X + 1e-10, 0, at.Z + rng.NextDouble() * 900 - 450),
                    2 => at,
                    _ => new Position(at.X + rng.NextDouble() * 600 - 300, rng.NextDouble(), at.Z + rng.NextDouble() * 600 - 300)
                };
                route.Legs.Add(new RouteLeg { From = at, To = to, Minutes = 1 + l, Mode = TravelMode.Walk }); at = to;
            }
            var c = new CitizenState { Identity = new CitizenIdentity { Id = "c" + i }, Phase = CitizenPhase.Travelling, Journey = route.Commit() };
            people.Add(c);
        }
        index.Rebuild(people);
        Assert.True(index.MatchesScan(people)); Assert.True(index.Grid.Matches(index.BodyCandidates, true));
        var output = new List<IPersonView>();
        foreach (var c in people)
            for (int k = 0; k < 20; k++)
            {
                double elapsed = k == 0 ? 0 : k == 1 ? c.Journey!.Minutes : rng.NextDouble() * c.Journey!.Minutes;
                var point = c.Journey!.PositionAt(elapsed);
                Assert.Contains(c, index.Grid.Query(point, 0, output));
            }
        var all = index.Grid.Query(new Position(0, 0, 0), 5000, output);
        Assert.Equal(people.Count, all.Count); Assert.Equal(people.Count, all.Distinct().Count());
        Assert.True(index.Grid.Registrations > people.Count * 3);
        Assert.Empty(index.Grid.Query(new Position(0, 0, 0), -1, output));
        Assert.Empty(index.Grid.Query(new Position(0, 0, 0), double.NaN, output));
        Assert.Equal(people.Count, index.Grid.Query(new Position(0, 0, 0), double.PositiveInfinity, output).Count);
    }

    [Fact]
    public void UnresolvablePositionsAreReturnedByEveryQuery()
    {
        var waiting = new CitizenState { Identity = new CitizenIdentity { Id = "w" }, Phase = CitizenPhase.WaitingForOpening, PlaceId = "shop" };
        var lost = new CitizenState { Identity = new CitizenIdentity { Id = "t" }, Phase = CitizenPhase.Travelling };
        var huge = new CitizenState { Identity = new CitizenIdentity { Id = "h" }, Phase = CitizenPhase.Travelling, Journey = new Route { Legs =
            { new RouteLeg { From = new Position(-1e6, 0, 0), To = new Position(1e6, 0, 0), Minutes = 1, Mode = TravelMode.Walk } } }.Commit() };
        var visitor = new CitizenState { Identity = new CitizenIdentity { Id = "pv:1" }, Phase = CitizenPhase.WaitingForOpening, PlaceId = "shop" };
        var people = new List<CitizenState> { waiting, lost, huge, visitor };
        var index = new ActiveJourneyIndex();
        index.Rebuild(people);
        Assert.True(index.MatchesScan(people));
        Assert.Equal(4, index.Grid.Unplaced);
        var near = index.Grid.Query(new Position(5000, 0, 5000), 1, new List<IPersonView>());
        Assert.Equal(new[] { "h", "pv:1", "t", "w" }, near.Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal));
        var placed = new ActiveJourneyIndex(id => id == "shop" ? new Position(10, 0, 10) : (Position?)null);
        placed.Rebuild(people);
        Assert.True(placed.MatchesScan(people));
        Assert.Equal(2, placed.Grid.Unplaced);
        Assert.DoesNotContain(waiting, placed.Grid.Query(new Position(5000, 0, 5000), 1, new List<IPersonView>()));
        Assert.Contains(waiting, placed.Grid.Query(new Position(10, 0, 10), 0, new List<IPersonView>()));
        Assert.Contains(visitor, placed.Grid.Query(new Position(10, 0, 10), 0, new List<IPersonView>()));
    }

    [Fact]
    public void GridHasNoIdentityOrEconomyEffect()
    {
        var plain = Spread(160); var probed = Spread(160);
        var identities = probed.State.Citizens.Select(c => c.Identity).ToArray();
        var output = new List<IPersonView>();
        int queries = 0;
        probed.ActivityObserved += (_, c, _) =>
        {
            _ = probed.ActiveJourneys.Grid.Query(probed.PositionOf(c), 120, output); queries++;
            _ = probed.ActiveJourneys.Grid.Query(new Position(0, 0, 0), double.PositiveInfinity, output);
        };
        foreach (double step in new[] { 90, 600, 1440 })
        {
            plain.AdvanceTo(plain.State.Minute + step); probed.AdvanceTo(probed.State.Minute + step);
            Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(probed.State));
        }
        Assert.True(queries > 100);
        Assert.Equal(identities, probed.State.Citizens.Select(c => c.Identity));
        var methods = typeof(PresentationGrid).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "Contains", "Matches", "Query" }, methods);
        Assert.True(ActiveJourneyIndex.VisitorsReceiveBodies);
    }

    private static string Source(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

}
