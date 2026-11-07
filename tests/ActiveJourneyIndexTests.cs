using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// F5f (city audit F5 #92): the active-journey candidate index equals a full scan after every event, frame slice,
// skip, reload, route rebuild, interrupted plan and physical lease acquire/release; visitors are indexed but withheld.
public sealed class ActiveJourneyIndexTests
{
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
            Minutes = Math.Max(3, Position.Distance(from.Position, to.Position) / 80) } } };
    }
    // Answers after `steps` continuations, so a frame slice leaves the event in flight.
    private sealed class Resumable(int steps) : IResumableRoutePlanner
    {
        public Route? Plan(Place a, Place b, double at, TravelPreferences p) => new Walking().Plan(a, b, at, p);
        public IRoutePlanningJob Begin(RouteRequest request) => new Job(request, steps);
        private sealed class Job(RouteRequest request, int left) : IRoutePlanningJob
        {
            public Route? Result { get; private set; }
            public bool Continue(double budget)
            {
                if (--left > 0) return false;
                Result = new Walking().Plan(request.From, request.To, request.DepartureMinute, request.Preferences); return true;
            }
            public void Dispose() { }
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

    // The specification, written independently of Core: travelling, waiting at a door, holding an optional stop
    // or at an outdoor scene.
    private static bool Expected(CitizenState c) => c.Phase == CitizenPhase.Travelling || c.Phase == CitizenPhase.WaitingForOpening
        || c.Phase == CitizenPhase.AtPlace && (c.OptionalStop != null || c.Activity == Activity.Leisure && c.PlaceId.StartsWith("scene-", StringComparison.Ordinal));
    private static bool IsVisitor(string id) => id.StartsWith("pv:", StringComparison.Ordinal) || id.StartsWith("consumer:", StringComparison.Ordinal);

    private static string[] Members(CitySimulator sim) => sim.State.Citizens.Where(sim.ActiveJourneys.Contains)
        .Select(c => c.Identity.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    private static void AssertIndexed(CitySimulator sim)
    {
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        var index = sim.ActiveJourneys;
        var expected = sim.State.Citizens.Where(Expected).ToList();
        Assert.Equal(expected.Count, index.Count);
        Assert.All(expected, c => Assert.True(index.Contains(c)));
        var bodies = index.BodyCandidates.Select(c => c.Identity.Id).OrderBy(id => id, StringComparer.Ordinal);
        Assert.Equal(expected.Where(c => !IsVisitor(c.Identity.Id)).Select(c => c.Identity.Id).OrderBy(id => id, StringComparer.Ordinal), bodies);
        Assert.Equal(expected.Count(c => IsVisitor(c.Identity.Id)), index.WithheldVisitors);
        Assert.Equal(index.BodyCandidates.Count, index.BodyCandidates.Distinct().Count());
    }

    private static CitySimulator Mixed(IRoutePlanner planner, int count = 400, Func<int, string>? id = null)
    {
        var personas = (Persona[])Enum.GetValues(typeof(Persona));
        var ids = Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = id?.Invoke(i) ?? "r" + i.ToString("D4"), Name = "R" + i,
            Age = 20 + i % 50, Persona = personas[i % personas.Length] }).ToArray();
        var places = new List<Place>();
        for (int i = 0; i < 20; i++) places.Add(new Place { Id = "home" + i.ToString("D2"), Name = "H", Kind = PlaceKind.Home, Capacity = 40, Position = new(i * 50, 0, 0) });
        for (int i = 0; i < 10; i++) places.Add(new Place { Id = "work" + i.ToString("D2"), Name = "W", Kind = PlaceKind.Work, Capacity = 80, Position = new(i * 70, 0, 400), OpenMinute = 360, CloseMinute = 1320 });
        for (int i = 0; i < 2; i++) places.Add(new Place { Id = "school" + i, Name = "S", Kind = PlaceKind.Study, Capacity = 200, Position = new(i * 70, 0, 600) });
        for (int i = 0; i < 8; i++) places.Add(new Place { Id = "shop" + i.ToString("D2"), Name = "Sh", Kind = PlaceKind.Shop, Capacity = 80, Position = new(i * 60, 0, 200), OpenMinute = 480, CloseMinute = 1260 });
        for (int i = 0; i < 4; i++) places.Add(new Place { Id = "fun" + i.ToString("D2"), Name = "L", Kind = PlaceKind.Leisure, Capacity = 80, Position = new(i * 90, 0, -200), OpenMinute = 600, CloseMinute = 1439 });
        for (int i = 0; i < 4; i++) places.Add(new Place { Id = "scene-park:p" + i, Name = "Park", Kind = PlaceKind.Leisure, Capacity = 80, Position = new(i * 90, 0, -400) });
        var state = Population.Create(ids, places, "f5f-index", "0.40.4", 29, 1440 * 3);
        var sim = new CitySimulator(state, planner); sim.AttachOptionalStops(new Geometry(), 100);
        return sim;
    }

    private static CitySimulator Reload(CitySimulator sim, IRoutePlanner planner)
    {
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, planner); loaded.AttachOptionalStops(new Geometry(), 100);
        return loaded;
    }

    [Fact]
    public void IndexEqualsFullScanAfterEveryEventSliceSkipAndReload()
    {
        var sim = Mixed(new Walking());
        AssertIndexed(sim);
        int observed = 0, travelling = 0, stops = 0, scenes = 0;
        sim.ActivityObserved += (_, _, _) =>
        {
            observed++;
            Assert.True(sim.ActiveJourneyIndexMatchesState());
            travelling = Math.Max(travelling, sim.State.Citizens.Count(c => c.Phase == CitizenPhase.Travelling && sim.ActiveJourneys.Contains(c)));
            stops = Math.Max(stops, sim.State.Citizens.Count(c => c.Phase == CitizenPhase.AtPlace && c.OptionalStop != null && sim.ActiveJourneys.Contains(c)));
            scenes = Math.Max(scenes, sim.State.Citizens.Count(c => c.PlaceId.StartsWith("scene-", StringComparison.Ordinal) && c.Phase == CitizenPhase.AtPlace && sim.ActiveJourneys.Contains(c)));
        };
        // One ordinary day in single-event frame slices.
        double target = sim.State.Minute + 1440;
        while (!sim.IsCaughtUpTo(target)) { sim.AdvanceFrameSlice(target); AssertIndexed(sim); }
        Assert.True(observed > 1000, "observed " + observed);
        Assert.True(travelling > 0 && stops > 0 && scenes > 0, $"coverage travelling {travelling}, stops {stops}, scenes {scenes}");
        // A hidden skip of three days in event-budgeted calls.
        double skip = sim.State.Minute + 3 * 1440 + 437;
        while (!sim.AdvanceTo(skip).CaughtUp) { }
        AssertIndexed(sim);
        // Reload: the rebuilt index has the same members, and both continue identically.
        var members = Members(sim);
        var loaded = Reload(sim, new Walking());
        AssertIndexed(loaded);
        Assert.Equal(members, Members(loaded));
        foreach (double step in new[] { 13.5, 240, 600 })
        {
            sim.AdvanceTo(sim.State.Minute + step); loaded.AdvanceTo(loaded.State.Minute + step);
            AssertIndexed(sim); AssertIndexed(loaded);
            Assert.Equal(Members(sim), Members(loaded));
        }
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
    }

    [Fact]
    public void InterruptedPlanAndRouteRebuildKeepTheIndexExact()
    {
        var sim = Mixed(new Resumable(3), 120);
        double target = sim.State.Minute + 1440;
        int pending = 0;
        for (int frame = 0; frame < 200000 && !sim.IsCaughtUpTo(target); frame++)
        {
            var result = sim.AdvanceFrameSlice(target);
            if (result.PlanningPending)
            {
                pending++;
                // The event in flight has not touched the live resident yet.
                AssertIndexed(sim);
                if (pending % 7 == 0)
                {
                    // A rebuild abandons the event; the replacement replays it from its start.
                    sim = sim.RebuildRoutes(new Resumable(3));
                    AssertIndexed(sim);
                    continue;
                }
                while (!sim.ContinuePlanning(1)) AssertIndexed(sim);
            }
            AssertIndexed(sim);
        }
        Assert.True(sim.IsCaughtUpTo(target));
        Assert.True(pending > 100, "pending " + pending);
    }

    private static CitySimulator Travellers(params string[] ids)
    {
        var state = Population.Create(ids.Select((id, i) => new CitizenIdentity { Id = id, Name = "P" + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }),
            new[]
            {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = ids.Length, Position = new Position(0, 0, 0) },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = ids.Length, Position = new Position(120, 0, 0) }
            }, "f5f-travellers", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + 12; c.ActivityUntilMinute = 2880;
            c.Journey = new Route { Legs = { new RouteLeg { From = state.Places[0].Position, To = state.Places[1].Position, Minutes = 12, Mode = TravelMode.Walk } } };
        }
        return new CitySimulator(state, new Walking());
    }

    [Fact]
    public void PhysicalLeaseAcquireObserveAndReleaseKeepTheIndex()
    {
        var sim = Travellers("c0", "c1", "c2");
        AssertIndexed(sim); Assert.Equal(3, sim.ActiveJourneys.Count);
        var c = sim.State.Citizens[0];
        Assert.True(sim.TryAcquirePhysicalJourney(c.Identity.Id, out var lease));
        AssertIndexed(sim); Assert.True(sim.ActiveJourneys.Contains(c));
        // The leased journey is frozen while the others arrive at work and leave the index.
        sim.AdvanceTo(1460); AssertIndexed(sim);
        Assert.Equal(new[] { "c0" }, Members(sim));
        Assert.True(sim.ObservePhysicalJourney(lease!, 6)); AssertIndexed(sim);
        // Save and reload while leased: the lease is not saved; the journey continues from the measured point.
        var loaded = Reload(sim, new Walking()); AssertIndexed(loaded); Assert.Equal(Members(sim), Members(loaded));
        Assert.True(sim.ReleasePhysicalJourney(lease!)); AssertIndexed(sim); Assert.True(sim.ActiveJourneys.Contains(c));
        sim.AdvanceTo(1480); AssertIndexed(sim);
        Assert.Equal(CitizenPhase.AtPlace, c.Phase); Assert.Equal(0, sim.ActiveJourneys.Count);
        loaded.AdvanceTo(1480); AssertIndexed(loaded);
    }

    private static CitySimulator AtHome()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "person", Name = "Resident", Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
                new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(10, 0, 0) } }, "f5f-visit", "0.40.4", 17, 1440);
        var person = state.Citizens.Single();
        person.Phase = CitizenPhase.AtPlace; person.PlaceId = person.HomeId; person.Activity = Activity.Home;
        person.ActivityUntilMinute = person.NextEventMinute = 1800;
        return new CitySimulator(state, new Walking());
    }
    private static Route Out(CitySimulator s) => new Walking().Plan(s.State.Places.Single(p => p.Id == "home"),
        s.State.Places.Single(p => p.Id == "shop"), s.State.Minute, new TravelPreferences());

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VisitStartsAndReturnsFileMembership(bool meal)
    {
        var sim = AtHome(); var c = sim.State.Citizens.Single();
        AssertIndexed(sim); Assert.False(sim.ActiveJourneys.Contains(c));
        Assert.True(meal ? sim.TryBeginMealVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1500)
            : sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1500));
        AssertIndexed(sim); Assert.True(sim.ActiveJourneys.Contains(c));
        sim.AdvanceTo(1450); AssertIndexed(sim); Assert.Equal("shop", c.PlaceId); Assert.False(sim.ActiveJourneys.Contains(c));
        sim.AdvanceTo(1459.5); AssertIndexed(sim); Assert.Equal(CitizenPhase.Travelling, c.Phase); Assert.True(sim.ActiveJourneys.Contains(c));
        sim.AdvanceTo(1475); AssertIndexed(sim); Assert.Equal("home", c.PlaceId); Assert.False(sim.ActiveJourneys.Contains(c));
    }

    [Fact]
    public void VisitorsAreIndexedButNeverBodyCandidates()
    {
        Assert.False(ActiveJourneyIndex.VisitorsReceiveBodies);
        var sim = Travellers("c0", "consumer:c0:1", "pv:1", "pv:2", "r5");
        AssertIndexed(sim);
        Assert.Equal(5, sim.ActiveJourneys.Count); Assert.Equal(3, sim.ActiveJourneys.WithheldVisitors);
        Assert.Equal(new[] { "c0", "r5" }, sim.ActiveJourneys.BodyCandidates.Select(c => c.Identity.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(sim.State.Citizens.Where(c => IsVisitor(c.Identity.Id)), c =>
        { Assert.Equal(PersonKind.Visitor, ActiveJourneyIndex.KindOf(c)); Assert.False(ActiveJourneyIndex.ReceivesBody(c)); Assert.True(sim.ActiveJourneys.Contains(c)); });
        var loaded = Reload(sim, new Walking()); AssertIndexed(loaded); Assert.Equal(3, loaded.ActiveJourneys.WithheldVisitors);
        sim.AdvanceTo(1460); AssertIndexed(sim); Assert.Equal(0, sim.ActiveJourneys.Count); Assert.Equal(0, sim.ActiveJourneys.WithheldVisitors);
        // A whole mixed day with visitor-format IDs keeps visitors out of body candidates after every event.
        var mixed = Mixed(new Walking(), 120, i => i % 3 == 0 ? PersonIds.Visitor(i + 1) : "r" + i.ToString("D4"));
        int withheld = 0;
        mixed.ActivityObserved += (_, _, _) =>
        {
            Assert.DoesNotContain(mixed.ActiveJourneys.BodyCandidates, c => IsVisitor(c.Identity.Id));
            withheld = Math.Max(withheld, mixed.ActiveJourneys.WithheldVisitors);
        };
        mixed.AdvanceTo(mixed.State.Minute + 1440); AssertIndexed(mixed);
        Assert.True(withheld > 0);
    }

    // The index is a superset of every stateful candidate filter of Runtime physical selection
    // (moving journeys, planned door queues, Outdoor) for every phase/activity/stop/place combination.
    [Fact]
    public void MembershipCoversEveryStatefulSelectionCandidate()
    {
        int cases = 0;
        foreach (CitizenPhase phase in Enum.GetValues(typeof(CitizenPhase)))
        foreach (Activity activity in Enum.GetValues(typeof(Activity)))
        foreach (bool journey in new[] { false, true })
        foreach (bool stop in new[] { false, true })
        foreach (string place in new[] { "shop", "scene-park:a", "scene-beach:b", "scene-party-pier:c", "park" })
        {
            var c = new CitizenState { Identity = new CitizenIdentity { Id = "c1" }, Phase = phase, Activity = activity, PlaceId = place,
                Journey = journey ? new Route() : null, OptionalStop = stop ? new OptionalStopState { StartMinute = 0, EndMinute = 100 } : null };
            bool moving = c.Phase == CitizenPhase.Travelling && c.Journey != null;
            bool queue = c.Phase == CitizenPhase.WaitingForOpening;
            bool outdoor = c.Phase == CitizenPhase.AtPlace && c.OptionalStop != null && c.OptionalStop.ActiveAt(50)
                || c.Phase == CitizenPhase.AtPlace && c.Activity == Activity.Leisure && c.PlaceId.StartsWith("scene-", StringComparison.Ordinal);
            if (moving || queue || outdoor) Assert.True(ActiveJourneyIndex.Member(c), $"{phase} {activity} {journey} {stop} {place}");
            Assert.Equal(Expected(c), ActiveJourneyIndex.Member(c));
            cases++;
        }
        Assert.True(cases > 200);
        var waiting = AtHome(); var w = waiting.State.Citizens.Single();
        Assert.False(waiting.ActiveJourneys.Contains(w));
    }

    // Derived only: reading the index never changes saved state, identity or money, and the writer schema is pinned.
    [Fact]
    public void IndexHasNoIdentityOrEconomyEffect()
    {
        Assert.Equal(SaveCodec.VendorSchema, CityState.CurrentSchema);
        var plain = Mixed(new Walking(), 160); var probed = Mixed(new Walking(), 160);
        var identities = probed.State.Citizens.Select(c => c.Identity).ToArray();
        probed.ActivityObserved += (_, _, _) => { _ = probed.ActiveJourneyIndexMatchesState(); _ = probed.ActiveJourneys.BodyCandidates.Count; };
        foreach (double step in new[] { 90, 600, 1440 })
        {
            plain.AdvanceTo(plain.State.Minute + step); probed.AdvanceTo(probed.State.Minute + step);
            string text = SaveCodec.Write(probed.State);
            Assert.Equal(SaveCodec.Write(plain.State), text);
            Assert.True(probed.ActiveJourneyIndexMatchesState());
            Assert.Equal(text, SaveCodec.Write(probed.State));
        }
        Assert.Equal(identities, probed.State.Citizens.Select(c => c.Identity));
        // Only read members are public; filing and rebuilding stay inside Core.
        var mutators = typeof(ActiveJourneyIndex).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "Contains", "MatchesScan" }, mutators);
    }

    [Fact]
    public void MatchesScanDetectsDrift()
    {
        var sim = Travellers("c0", "c1");
        var c = sim.State.Citizens[1];
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        // A change made behind the simulator's back is a mismatch, which Runtime answers with a full scan.
        c.Phase = CitizenPhase.AtPlace; c.Journey = null; c.Activity = Activity.Work;
        Assert.False(sim.ActiveJourneyIndexMatchesState());
        sim.ActiveJourneys.File(c);
        Assert.True(sim.ActiveJourneyIndexMatchesState());
        var fresh = new ActiveJourneyIndex();
        Assert.False(fresh.MatchesScan(sim.State.Citizens));
        fresh.Rebuild(sim.State.Citizens); Assert.True(fresh.MatchesScan(sim.State.Citizens));
        Assert.False(fresh.MatchesScan(sim.State.Citizens.Take(0).ToList()));
    }
}
