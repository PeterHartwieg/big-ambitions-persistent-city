using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

// F2b (docs/plans/f2-movement.md Decision 3): bodies never write logical state. F2c removed the walker leases and F2d
// the traffic leases, so no write remains. Runtime cases use the production PhysicalPopulation with native scene doubles.
[Collection(StreetFoodHooks.Collection)]
public sealed class PresentationBoundaryTests
{
    private sealed class Paths : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p)
            => new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = 1 } } };
    }
    private sealed class Source : IWeatherSource
    {
        public WeatherSample Value; public int Calls;
        public WeatherSample ReadWeather() { Calls++; return Value; }
    }

    private static readonly Position Shop = new(100, 0, 0), SpotA = new(104, 0, 0), SpotB = new(104, 0, 1);

    private sealed class Fixture : IDisposable
    {
        internal readonly CitySimulator City;
        internal readonly PhysicalPopulation Bodies;
        internal PhysicalPedestrians Pool => PhysicalPedestrians.Last!;
        internal CitizenState A => City.State.Citizens.Single(c => c.Identity.Id == "a");
        internal CitizenState B => City.State.Citizens.Single(c => c.Identity.Id == "b");
        internal Fixture(bool withQueuer = false, int solo = 0)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            NativeTravelPace.Focus = Shop; Time.Next(3);
            var ids = new List<CitizenIdentity> { new() { Id = "a", Name = "A", Age = 30 }, new() { Id = "b", Name = "B", Age = 31 } };
            if (withQueuer) ids.Add(new() { Id = "q", Name = "Q", Age = 32 });
            for (int i = 0; i < solo; i++) ids.Add(new() { Id = "s" + i.ToString("D2"), Name = "S" + i, Age = 20 + i % 40 });
            var state = Population.Create(ids, new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 4 + solo, Position = new(60, 0, 0) },
                new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Leisure, Position = Shop } }, "f2b", "0.40.4", 3, 1440 + 600);
            double now = state.Minute;
            foreach (var c in state.Citizens.Where(c => c.Identity.Id != "q"))
            {
                c.PlaceId = "shop"; c.Activity = Activity.Leisure; c.Phase = CitizenPhase.AtPlace;
                c.NextEventMinute = c.ActivityUntilMinute = now + 30; c.Journey = null; c.MealOuting = null;
            }
            // A saved two-person conversation: each stop names both members (OptionalStopScheduling.TryConversation).
            var members = new List<string> { "a", "b" };
            OptionalStopState Stop(Position at, Position facing) => new() { Kind = DwellKind.ChatOutsideVenue, PlaceId = "shop",
                SpotId = "spot:" + at.Z, TargetId = "chat:a", Position = at, Facing = facing, ReturnPosition = Shop,
                StartMinute = now - 1, EndMinute = now + 10, VisitMinute = now - 1, ResumeEventMinute = now + 30, Members = new List<string>(members) };
            state.Citizens.Single(c => c.Identity.Id == "a").OptionalStop = Stop(SpotA, SpotB);
            state.Citizens.Single(c => c.Identity.Id == "b").OptionalStop = Stop(SpotB, SpotA);
            // Solo window-shopping stops on their own spots (perf fixture only).
            foreach (var c in state.Citizens.Where(c => c.Identity.Id.StartsWith("s", StringComparison.Ordinal)))
            {
                int i = int.Parse(c.Identity.Id.Substring(1), CultureInfo.InvariantCulture);
                var at = new Position(106 + i % 8, 0, 2 + i / 8);
                c.OptionalStop = new() { Kind = DwellKind.WindowShopping, PlaceId = "shop", SpotId = "solo:" + i, TargetId = "front:" + i,
                    Position = at, Facing = new Position(at.X, 0, at.Z + 1), ReturnPosition = Shop, StartMinute = now - 1, EndMinute = now + 10,
                    VisitMinute = now - 1, ResumeEventMinute = now + 30 };
            }
            if (withQueuer)
            {
                var q = state.Citizens.Single(c => c.Identity.Id == "q");
                q.PlaceId = q.DestinationId = "shop"; q.DestinationActivity = Activity.Leisure; q.Activity = Activity.Leisure;
                q.Phase = CitizenPhase.WaitingForOpening; q.NextEventMinute = now + 5; q.ActivityUntilMinute = now + 60; q.Journey = null;
            }
            City = new CitySimulator(state, new Paths());
            var parent = new Transform { Component = new PopulationRuntime() };
            Bodies = new(parent, City, new MultimodalRoutes(), typeof(Fixture).Assembly, new VendorBodyTests.Log(), Math.Max(8, 2 + solo), 3, 100, new DwellSpots());
        }
        internal void Tick(float seconds = 0.3f) { Time.Next(seconds); Bodies.Update(); }
        internal PhysicalPedestrianBody Body(string id) => Pool.Active[id];
        // The saved stops, their partners and the spot reservations Core plans against.
        internal string Logical() => string.Join("|", City.State.Citizens.OrderBy(c => c.Identity.Id, StringComparer.Ordinal).Select(c =>
            c.Identity.Id + ":" + c.Phase + ":" + c.PlaceId + ":" + c.NextEventMinute.ToString("R") + ":" + (c.OptionalStop == null ? "-"
                : c.OptionalStop.SpotId + "," + c.OptionalStop.Position + "," + c.OptionalStop.StartMinute.ToString("R") + ","
                + c.OptionalStop.EndMinute.ToString("R") + "," + c.OptionalStop.Cancelled + "," + string.Join("+", c.OptionalStop.Members))));
        // A queue body standing on a's facade spot, bound as PlanQueues/TryJoinQueue would bind it.
        internal void StandQueuerOn(Position spot)
        {
            var q = City.State.Citizens.Single(c => c.Identity.Id == "q");
            Assert.True(Pool.TryAcquire(q.Identity, spot, out var body));
            var type = typeof(PhysicalPopulation).GetNestedType("Binding", BindingFlags.NonPublic)!;
            var binding = Activator.CreateInstance(type)!;
            void Set(string name, object? value) => type.GetField(name)!.SetValue(binding, value);
            Set("Citizen", q); Set("Person", body);
            Set("Queue", (Dwell?)new Dwell("q", DwellKind.QueueAtOpening, "shop", City.State.Minute, q.NextEventMinute, "shop"));
            Set("Line", new DoorQueue("door", new Position(100, 0, -1), Shop, new[] { spot }));
            Set("Slot", 0);
            Set("Step", Enum.Parse(typeof(PhysicalPopulation).GetNestedType("QueueStep", BindingFlags.NonPublic)!, "Standing"));
            typeof(PhysicalPopulation).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Bodies, new[] { binding });
        }
        public void Dispose() { PhysicalPedestrianBody.Unreachable = null; Bodies.Dispose(); }
    }

    [Fact]
    public void OptionalStopSurvivesAnUnreachableSpot()
    {
        using var f = new Fixture();
        string before = f.Logical();
        PhysicalPedestrianBody.Unreachable = p => Position.Distance(p, SpotA) < 0.1;
        f.Tick();
        var body = f.Body("a");
        Assert.True(body.Agent.isStopped); Assert.Equal(f.A.OptionalStop!.Facing, body.Facing);
        for (int i = 0; i < 10; i++) f.Tick();
        Assert.True(f.Pool.Active.ContainsKey("a"));
        Assert.False(f.A.OptionalStop!.Cancelled); Assert.False(f.B.OptionalStop!.Cancelled);
        Assert.Equal(before, f.Logical());
        // The partner walked to its own spot as usual.
        Assert.Equal(SpotB, f.Body("b").Target);
    }

    [Fact]
    public void OptionalStopSurvivesAQueueHoldingItsSpot()
    {
        using var f = new Fixture(withQueuer: true);
        f.StandQueuerOn(SpotA);
        string before = f.Logical();
        f.Tick();
        var body = f.Body("a");
        Assert.True(body.Agent.isStopped); Assert.NotEqual(SpotA, body.Target);
        for (int i = 0; i < 10; i++) f.Tick();
        Assert.True(f.Pool.Active.ContainsKey("a")); Assert.True(f.Pool.Active.ContainsKey("q"));
        Assert.False(f.A.OptionalStop!.Cancelled); Assert.False(f.B.OptionalStop!.Cancelled);
        Assert.Equal(before, f.Logical());
    }

    [Fact]
    public void OptionalStopSurvivesTwentySecondsOfBlockedWalking()
    {
        using var f = new Fixture();
        string before = f.Logical();
        f.Tick();
        var body = f.Body("a");
        Assert.Equal(SpotA, body.Target); Assert.False(body.Agent.isStopped);
        // The double never moves a body by itself: every second below is walking without progress.
        for (int i = 0; i < 25; i++) f.Tick(1f);
        Assert.True(body.Agent.isStopped); Assert.False(body.Agent.hasPath);
        Assert.NotEqual(SpotA, body.Position); Assert.Equal(f.A.OptionalStop!.Facing, body.Facing);
        Assert.True(f.Pool.Active.ContainsKey("a")); Assert.True(f.Pool.Active.ContainsKey("b"));
        Assert.False(f.A.OptionalStop!.Cancelled); Assert.False(f.B.OptionalStop!.Cancelled);
        Assert.Equal(new[] { "a", "b" }, f.A.OptionalStop.Members); Assert.Equal(new[] { "a", "b" }, f.B.OptionalStop.Members);
        Assert.Equal(before, f.Logical());
        // Saving observes bodies but still writes nothing for them.
        f.Bodies.ObserveForSave();
        Assert.Equal(before, f.Logical());
    }

    [Fact]
    public void BodyStandingShortReservesTheQueueSlotItStandsOn()
    {
        using var f = new Fixture();
        f.Tick();
        for (int i = 0; i < 25; i++) f.Tick(1f);
        Assert.Equal(Shop, f.Body("a").Position);
        var standing = typeof(PhysicalPopulation).GetMethod("Standing", BindingFlags.NonPublic | BindingFlags.Instance)!;
        List<KeyValuePair<string, int>> Reserved(params Position[] spots)
            => ((IEnumerable<KeyValuePair<string, int>>)standing.Invoke(f.Bodies, new object[] {
                new DoorQueue("door", new Position(100, 0, -1), Shop, spots) })!).Where(p => p.Key == "a").ToList();
        // The slot under the body is held; the stop's own spot, which it never reached, is not.
        Assert.Equal(new[] { new KeyValuePair<string, int>("a", 0) }, Reserved(new Position(100.3, 0, 0), new Position(110, 0, 0)));
        Assert.Empty(Reserved(SpotA));
    }

    [Fact]
    public void LogicalEndOfTheStopStillSendsTheBodyBack()
    {
        using var f = new Fixture();
        f.Tick();
        for (int i = 0; i < 25; i++) f.Tick(1f);
        var body = f.Body("a"); body.Character.transform.position = PhysicalPedestrianBody.Vector(new Position(102, 0, 0));
        // Core cancels (for example for rain in RefreshOptionalStops); the body follows and only walks back.
        f.A.OptionalStop!.Cancelled = true; string before = f.Logical();
        f.Tick();
        Assert.Equal(Shop, body.Target); Assert.False(body.Agent.isStopped);
        Assert.Equal(before, f.Logical());
    }

    // Opt-in offline component timing (docs/perf/f2b.md): set PC_F2B_MEASURE to an output file.
    [Fact]
    public void MeasureUpdateWithOptionalStopBodies()
    {
        string? path = System.Environment.GetEnvironmentVariable("PC_F2B_MEASURE");
        if (path == null) return;
        void Measure(string label, Fixture f, int count = 2000)
        {
            for (int i = 0; i < 100; i++) f.Tick(0.016f);
            var times = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                Time.Next(0.016f); long start = Stopwatch.GetTimestamp();
                f.Bodies.Update();
                times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            }
            times.Sort();
            File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture,
                "{0}: bodies={1}, n={2}, p50={3:F4} ms, p95={4:F4} ms, max={5:F4} ms, spikes>=30ms={6}{7}", label, f.Pool.Active.Count, count,
                times[count / 2], times[(int)Math.Ceiling(count * .95) - 1], times[^1], times.Count(t => t >= 30), System.Environment.NewLine));
        }
        foreach (int solo in new[] { 6, 46 })
        {
            using (var f = new Fixture(solo: solo))
            {
                for (int i = 0; i < 20; i++) f.Tick();
                foreach (var b in f.Pool.Active.Values) b.Character.transform.position = PhysicalPedestrianBody.Vector(b.Target);
                Measure("at spots, " + (solo + 2) + " stops", f);
            }
            using (var f = new Fixture(solo: solo))
            {
                for (int i = 0; i < 20; i++) f.Tick();
                Measure("walking without progress (first 20 s, then the bound), " + (solo + 2) + " stops", f, 1500);
            }
        }
    }

    private static CityState WaitState(out Place shop)
    {
        double start = 1440 + 1050 + (int)(StableRandom.Hash("person:hours", 7) % 31) - 15;
        var state = Population.Create(new[] { new CitizenIdentity { Id = "person", Name = "Person", Age = 30, Persona = Persona.Foodie } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
                new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(10, 0, 0) } }, "weather-test", "0.40.4", 7, start);
        state.Citizens[0].ShopId = "shop"; shop = state.Places[1]; shop.OpenMinute = (int)(start % 1440) + 8;
        return state;
    }

    [Fact]
    public void QueueWeatherIsCapturedAtTheLogicalWaitEvent()
    {
        var state = WaitState(out var shop); var source = new Source { Value = WeatherSample.Dry };
        var city = new CitySimulator(state, new Paths()); city.AttachWeather(source);
        Assert.Equal(0, source.Calls);
        city.AdvanceTo(state.Minute + 1);
        var person = state.Citizens[0];
        Assert.Equal(CitizenPhase.WaitingForOpening, person.Phase);
        // Captured by the event itself (sampled Dry, not the neutral Unknown), before any presentation query.
        Assert.True(source.Calls > 0); int reads = source.Calls;
        Assert.NotNull(person.W); Assert.True(person.W!.Queue);
        Assert.Equal(person.NextEventMinute, person.W.Boundary); Assert.Equal(WeatherSample.Dry, person.W.Weather);
        var capture = person.W; string bytes = DevelopmentChain.Write(state);
        source.Value = WeatherSample.Rain;
        Assert.NotNull(city.QueueAtOpening(person, shop, state.Minute));
        Assert.Same(capture, person.W); Assert.Equal(reads, source.Calls); Assert.Equal(bytes, DevelopmentChain.Write(state));
    }

    [Fact]
    public void QueueAtOpeningWritesNothingAndGivesNoDwellWithoutACapture()
    {
        var state = WaitState(out var shop); var p = state.Citizens[0]; double now = state.Minute;
        p.PlaceId = p.DestinationId = "shop"; p.DestinationActivity = Activity.Shop; p.Activity = Activity.Shop;
        p.Phase = CitizenPhase.WaitingForOpening; p.NextEventMinute = now + 8; p.ActivityUntilMinute = now + 45; p.W = null;
        var spy = new Source { Value = WeatherSample.Rain };
        var city = new CitySimulator(state, new Paths()); city.AttachWeather(spy);
        string bytes = DevelopmentChain.Write(state);
        for (int i = 0; i < 3; i++) Assert.Null(city.QueueAtOpening(p, shop, now + i));
        Assert.Null(p.W); Assert.Equal(0, spy.Calls); Assert.Equal(bytes, DevelopmentChain.Write(state));
        // A capture for another boundary (a stale wait) is not replaced either.
        var stale = new WeatherCommitment(now + 1, WeatherSample.Rain, 5); p.W = stale; bytes = DevelopmentChain.Write(state);
        Assert.Null(city.QueueAtOpening(p, shop, now));
        Assert.Same(stale, p.W); Assert.Equal(0, spy.Calls); Assert.Equal(bytes, DevelopmentChain.Write(state));
        // With the event's capture the query answers from it alone.
        var captured = city.CaptureQueueWeather(p, now); Assert.Equal(1, spy.Calls); bytes = DevelopmentChain.Write(state);
        Assert.Equal(DwellPlanner.QueueAtOpening(p, shop, state.Seed, now, true, null, captured.Value), city.QueueAtOpening(p, shop, now));
        Assert.Same(captured, p.W); Assert.Equal(1, spy.Calls); Assert.Equal(bytes, DevelopmentChain.Write(state));
    }

    // Every CitySimulator member that Runtime/Physical*.cs may reach through `simulation.`. Adding a member
    // needs a reviewed change here. F2c removed TryHoldPhysicalDeparture and every walker lease call; F2d removed the
    // traffic lease group, so every member is read-only. F2e adds HoldForPlayer.
    private static readonly string[] ReadOnlyMembers = {
        "State", "ActiveJourneys", "ActiveJourneyIndexMatchesState", "ActivityObserved", "FindPlace", "OptionalStopStatus",
        "PositionOf", "QueueAtOpening", "SampleJourney", "TryGetTrafficPermission", "TryGetTrafficSignalGreen",
    };
    // The removed lease surface (F2c, F2d). None of it may come back on CitySimulator or in Runtime.
    private static readonly string[] RemovedLeaseMembers = {
        "TryAcquirePhysicalJourney", "ObservePhysicalJourney", "ReleasePhysicalJourney", "IsPhysicalJourneyValid",
        "TrySamplePhysicalJourney", "TryHoldPhysicalDeparture",
    };
    // Core's own logical rules (rain, closing, phase, availability, partners), run before a save exactly as
    // PersistentCityMod runs them every frame. The answer does not depend on any body.
    private static readonly string[] LogicalRefreshMembers = { "RefreshOptionalStops" };
    // F2e (Decision 4): the visible world's only write into Core, the bounded player hold, from one call site.
    private static readonly string[] PlayerHoldMembers = { "HoldForPlayer" };

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Runtime", "PhysicalPopulation.cs"))) root = root.Parent;
        Assert.NotNull(root); return root!.FullName;
    }

    [Fact]
    public void PhysicalRuntimeCallsOnlyTheReviewedSimulationMembers()
    {
        var files = Directory.GetFiles(Path.Combine(Root(), "Runtime"), "Physical*.cs");
        Assert.Contains(files, f => Path.GetFileName(f) == "PhysicalPopulation.cs");
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\bsimulation\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)")) used.Add(m.Groups[1].Value);
            // Nothing is assigned through the simulator's state from presentation code.
            Assert.DoesNotMatch(new Regex(@"\bsimulation\s*\.\s*State\b[\w\.\[\]""]*\s*(=[^=>]|\+=|-=|\+\+|--)"), text);
        }
        var allowed = new SortedSet<string>(ReadOnlyMembers.Concat(LogicalRefreshMembers).Concat(PlayerHoldMembers), StringComparer.Ordinal);
        Assert.Empty(used.Except(allowed));
        // The list stays exact: a member no longer used must be removed from it in the same change.
        Assert.Empty(allowed.Except(used));
        Assert.DoesNotContain("CancelOptionalStop", used);
        // Every allowed member exists on CitySimulator.
        var members = typeof(CitySimulator).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(m => m.Name).ToHashSet();
        Assert.All(allowed, name => Assert.Contains(name, members));
        // F2e: exactly one call site, the player-contact report in PhysicalPopulation, and none anywhere else in Runtime.
        int holdCalls = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "Runtime"), "*.cs"))
            holdCalls += Regex.Matches(File.ReadAllText(file), @"\bsimulation\s*\.\s*HoldForPlayer\b").Count;
        Assert.Equal(1, holdCalls);
        // F2c: the walker follow, its corrections and both tails read only. F2d: so do the car command, the car follow,
        // its corrections, fade and phase count.
        string population = string.Concat(new[] { "", ".Following", ".Queues", ".Selection" }.Select(part => File.ReadAllText(Path.Combine(Root(), "Runtime", "PhysicalPopulation" + part + ".cs"))));
        SortedSet<string> Region(string start, string end)
        {
            int from = population.IndexOf(start, StringComparison.Ordinal), to = population.IndexOf(end, from + 1, StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, start);
            return new SortedSet<string>(Regex.Matches(population.Substring(from, to - from), @"\bsimulation\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)")
                .Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        }
        Assert.Empty(Region("private bool ObserveWalker(", "private bool ObserveCar(").Except(ReadOnlyMembers));
        var car = Region("private bool CarCommand(", "private bool Observe(Binding binding)");
        car.UnionWith(Region("private bool ObserveCar(", "// Inspector picking"));
        Assert.Equal(new[] { "SampleJourney", "TryGetTrafficPermission", "TryGetTrafficSignalGreen" }, car);
        // The lease type and its API are gone, and nothing in Runtime reaches the kernel's internal physical control.
        var simulatorMembers = typeof(CitySimulator).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name).ToHashSet();
        Assert.All(RemovedLeaseMembers, name => Assert.DoesNotContain(name, simulatorMembers));
        Assert.Null(typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.PhysicalJourneyLease"));
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "Runtime"), "*.cs"))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotMatch(new Regex(@"\b(SetPhysicalControl|ReportPhysicalProgress|PhysicalJourneyLease)\b"), text);
            // F2d: a car body's model and paint are derived (CarAppearance), never written into the saved car.
            Assert.DoesNotMatch(new Regex(@"\.\s*(ModelId|PaintSeed)\s*(=[^=>]|\?\?=)"), text);
        }
        // The Runtime rain check is gone from the body paths; logical rain stays in Core.
        Assert.DoesNotContain("RainHelper", string.Concat(new[] { "", ".Following", ".Queues", ".Selection" }.Select(part => File.ReadAllText(Path.Combine(Root(), "Runtime", "PhysicalPopulation" + part + ".cs")))));
    }
}
