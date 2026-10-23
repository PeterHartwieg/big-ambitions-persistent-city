using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using StagingHarness;

internal static class Program
{
    private static int checks;
    private static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); checks++; }
    private static void Equal(double expected, double actual, string message) =>
        Check(Math.Abs(expected - actual) <= 1e-10 * Math.Max(1, Math.Abs(expected)), message + ": expected " + expected.ToString("R") + ", got " + actual.ToString("R"));
    private static void Refuses(Action action, string message)
    { bool refused = false; try { action(); } catch (InvalidOperationException) { refused = true; } Check(refused, message); }

    private sealed class Fixture
    {
        internal readonly Waypoint[] Native;
        internal readonly Dictionary<object, (int Original, int Effective)> Overrides = new Dictionary<object, (int, int)>(References.Instance);
        internal readonly NativeRoadSpeeds Profile;
        internal int Captures;
        internal Fixture(int count = 9, float spacing = 10)
        {
            Native = Enumerable.Range(0, count).Select(i => new Waypoint
            {
                listIndex = i, position = new UnityEngine.Vector3(i * spacing, 0, 0), maxSpeed = 30,
                neighbors = i + 1 < count ? new List<int> { i + 1 } : new List<int>()
            }).ToArray();
            Profile = NewProfile(); ActivateScene();
        }
        internal NativeRoadSpeeds NewProfile() => new NativeRoadSpeeds(() =>
        { Guard.Read(); Captures++; return Overrides; });
        internal void ActivateScene() => UnityEngine.Object.Scene = new CurrentSceneData
        {
            allWaypoints = Native, allPriorityIntersections = Array.Empty<PriorityIntersection>(),
            allLightsIntersections = Array.Empty<TrafficLightsIntersection>(), roadIds = new int[Native.Length]
        };
        internal void Slow(int index)
        {
            // Synthetic fields only: the Compatibility tests separately verify RLO's reviewed contract.
            Native[index].maxSpeed = 20; Overrides.Add(Native[index], (30, 20));
        }
        internal void Restore(int index) { Native[index].maxSpeed = 30; Overrides.Remove(Native[index]); }
        internal RoadRoutes Start(NativeRoadSpeeds? profile = null) => new RoadRoutes(new PublicRoadAccess(), speeds: profile ?? Profile);
        internal RoadRoutes Build(NativeRoadSpeeds? profile = null)
        { var roads = Start(profile); Finish(roads); return roads; }
        internal Route Plan(RoadRoutes roads, int from = 0, int to = 8)
        {
            var a = Native[from].position; var b = Native[to].position;
            return roads.Plan(new RoadRoutes.Curb { Node = from, Position = new Position(a.x, a.y, a.z) },
                new RoadRoutes.Curb { Node = to, Position = new Position(b.x, b.y, b.z) }, 100)
                ?? throw new InvalidOperationException("Actual Runtime route planning failed.");
        }
    }
    private static void Finish(RoadRoutes roads)
    {
        int attempts = 0;
        while (!roads.Prepare())
        {
            if (!roads.IsCurrent || ++attempts > 10000) throw new InvalidOperationException("Runtime staged preflight failed.");
            Thread.Sleep(1);
        }
        Check(roads.Ready && roads.IsCurrent, "preflight published a stale/incomplete network");
    }
    private static void SameTopology(RoadRoutes original, RoadRoutes current)
    {
        Check(original.TopologyId == current.TopologyId, "native canonical fingerprint changed");
        var a = original.TrafficNetwork!; var b = current.TrafficNetwork!;
        Check(a.TopologyId == b.TopologyId, "traffic canonical fingerprint changed");
        Check(JsonSerializer.Serialize(a.Links) == JsonSerializer.Serialize(b.Links), "link IDs/capacities/canonical durations changed");
        foreach (var link in a.Links)
            Check(a.NativeNodes(link.Id).SequenceEqual(b.NativeNodes(link.Id)), "canonical section boundaries changed");
    }
    private static List<TrafficRouteLeg> Map(RoadRoutes roads, Route route)
    {
        Check(roads.TrafficNetwork!.TryMap(route, out var mapped), "actual native route did not map");
        roads.TrafficNetwork.ValidateRemainingMapping(route, mapped, 0);
        checks++; return mapped;
    }

    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        internal RoadRoutes Roads;
        internal Planner(RoadRoutes roads) { Roads = roads; }
        public TrafficNetwork? TrafficNetwork => Roads.TrafficNetwork;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => Roads.IsTrafficLinkClosed(linkId);
    }
    private static CitySimulator Engine(Planner planner, Route route, out CitizenState citizen)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "speed-proof", Name = "Speed proof", Age = 30, Persona = Persona.Commuter } },
            new[] { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = route.Legs[0].From },
                new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = route.Legs[route.Legs.Count - 1].To } },
            "speed-proof", "0.40.4", 1, 100);
        citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a"; citizen.DayPlaceId = citizen.DestinationId = "b";
        citizen.Phase = CitizenPhase.Travelling; citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 1000;
        citizen.DepartureMinute = 100; citizen.Preferences.OwnsCar = true;
        citizen.Car = new CarState { Id = "speed-proof:car", ParkedPosition = route.Legs[0].From };
        citizen.Journey = route; citizen.NextEventMinute = 100 + route.Minutes;
        var sim = new CitySimulator(state, planner); sim.AdvanceTo(100);
        Check(sim.TryAcquirePhysicalJourney(citizen.Identity.Id, out _), "actual simulator lease was not admitted");
        return sim;
    }
    private static void Persistence(CitySimulator sim, RoadRoutes roads)
    {
        string wire = SaveCodec.Write(sim.State);
        var loaded = SaveCodec.Read(wire);
        Check(loaded.Status == SaveReadStatus.Loaded, "SaveCodec refused committed timing: " + loaded.Error);
        Check(SaveCodec.Write(loaded.State!) == wire, "SaveCodec changed the committed speed/queue snapshot");
        var restored = new CitySimulator(loaded.State!, new Planner(roads)); restored.RefreshTrafficClosures();
        Check(restored.Traffic!.HasCompatibleDefinitions(roads.TrafficNetwork!.Links), "restored queue no longer binds canonical network");
        Check(restored.State.Citizens[0].Journey!.Minutes == sim.State.Citizens[0].Journey!.Minutes, "restore repriced committed journey");
    }
    private static void ExistingQueueAndFutureRoutes()
    {
        var f = new Fixture(); var baseline = f.Build(); var oldRoute = f.Plan(baseline);
        Equal(9.6, oldRoute.Minutes, "baseline duration"); Check(baseline.TrafficNetwork!.Links.Count == 1, "expected one 80m section");
        Check(baseline.TrafficNetwork.Links[0].Capacity == 13, "expected finite 6m storage policy");
        var oldMapped = Map(baseline, oldRoute); string committedRoute = JsonSerializer.Serialize(oldRoute);
        var planner = new Planner(baseline); var sim = Engine(planner, oldRoute, out var citizen);
        string queue = JsonSerializer.Serialize(sim.Traffic!.Capture());
        f.Slow(1);
        Check(ReferenceEquals(baseline.ResolveWaypoint(baseline.TopologyId, 1), f.Native[1]), "verified slowdown invalidated a live native reference before Prepare");
        Finish(baseline); sim.RefreshTrafficClosures();
        Check(JsonSerializer.Serialize(sim.Traffic.Capture()) == queue, "verified slowdown mutated occupied queue");
        Check(JsonSerializer.Serialize(oldRoute) == committedRoute && ReferenceEquals(citizen.Journey, oldRoute), "slowdown repriced a committed route");
        var during = f.Build(); SameTopology(baseline, during);
        var freshProfile = f.Build(f.NewProfile()); SameTopology(baseline, freshProfile);
        var slowRoute = f.Plan(during); var slowMapped = Map(during, slowRoute);
        Equal(10.2, slowRoute.Minutes, "weighted effective section duration");
        Check(slowRoute.Minutes > oldRoute.Minutes && slowRoute.EstimatedMinutes >= slowRoute.Minutes, "new route did not reflect slower effective speed");
        foreach (var leg in slowRoute.Legs) Equal(10.2 / 80, leg.Minutes / Position.Distance(leg.From, leg.To), "section pace is not uniform");
        Equal(slowRoute.Minutes, slowMapped.Single().FreeFlowMinutes, "mapped full-section timing");
        var partial = f.Plan(during, 2, 6); var partialMap = Map(during, partial).Single();
        Equal(5.1, partial.Minutes, "partial section did not receive proportional full-section pace");
        Equal(.25, partialMap.StartLinkFraction, "partial section source fraction"); Equal(.75, partialMap.EndLinkFraction, "partial section destination fraction");
        Equal(partial.Minutes, partialMap.FreeFlowMinutes, "partial mapped duration");
        Check(JsonSerializer.Serialize(oldMapped) == JsonSerializer.Serialize(Map(during, oldRoute)), "old route mapping changed under new speed");
        planner.Roads = during; sim.RefreshTrafficClosures(); Check(JsonSerializer.Serialize(sim.Traffic.Capture()) == queue, "canonical rebuild changed old queue");
        Persistence(sim, during);
        var newSim = Engine(new Planner(during), slowRoute, out _); Persistence(newSim, during);
        f.Restore(1);
        Check(ReferenceEquals(during.ResolveWaypoint(during.TopologyId, 1), f.Native[1]), "restoration callback before Prepare was refused");
        var restored = f.Build(); SameTopology(baseline, restored); Equal(oldRoute.Minutes, f.Plan(restored).Minutes, "new route after restoration");
        planner.Roads = restored; sim.RefreshTrafficClosures();
        Check(JsonSerializer.Serialize(sim.Traffic.Capture()) == queue, "restoration altered old queue");
        Persistence(newSim, restored); // Saved slow timing remains committed after the fault is gone.
        Console.WriteLine("PASS canonical topology, initial-active overlay, old queue immutability, SaveCodec/rebind/restoration: native=" + baseline.TopologyId
            + ", traffic=" + baseline.TrafficNetwork.TopologyId + ", links=1, capacity=13, baseline=" + oldRoute.Minutes.ToString("R")
            + ", slowed=" + slowRoute.Minutes.ToString("R") + ", partial40m=" + partial.Minutes.ToString("R"));
    }
    private static void StagedPreflight()
    {
        var f = new Fixture(4097); var baseline = f.Build();
        var staged = f.Start(); Check(!staged.Prepare(), "fixture did not force more than one bounded node-copy slice");
        f.Slow(0); f.Slow(4095); // An already-copied source and a source not yet copied.
        Finish(staged); SameTopology(baseline, staged);
        Check(f.Captures > 2, "staged adapter did not capture main-thread overlays");
        Check(staged.ResolveWaypoint(staged.TopologyId, 0) != null && staged.ResolveWaypoint(staged.TopologyId, 4095) != null, "staged overlay invalidated copied/uncopied nodes");
        Console.WriteLine("PASS slowdown during staged preflight on both sides of the copied-node cursor");
    }
    private static void GeometricSpacing()
    {
        var f = new Fixture(3, 6); f.Slow(0); var roads = f.Build(); var route = f.Plan(roads, 0, 2);
        var mapped = Map(roads, route); Check(roads.TrafficNetwork!.Links.Single().Capacity == 2, "12m capacity is not two vehicles");
        Equal(.9, route.Legs[0].Minutes, "first 6m edge pace"); Equal(.9, route.Legs[1].Minutes, "second 6m edge pace");
        var kernel = new TrafficKernel(roads.TrafficNetwork.Links); kernel.Enqueue("head", mapped); kernel.Enqueue("follower", mapped);
        kernel.AdvanceTo(0, 100); kernel.SetPhysicalControl("head", true); kernel.SetPhysicalControl("follower", true); kernel.AdvanceTo(100, 100);
        double headProgress = route.Minutes * 8 / 12; kernel.ReportPhysicalProgress("head", headProgress);
        double followerProgress = kernel.GetPermission("follower").MaximumNominalMinutes;
        Equal(8, route.PositionAt(headProgress).X, "head physical position");
        Equal(2, route.PositionAt(followerProgress).X, "follower permission position");
        Equal(6, route.PositionAt(headProgress).X - route.PositionAt(followerProgress).X, "effective timing violated geometric 6m FIFO spacing");
        Console.WriteLine("PASS actual Runtime mapping preserves 6m FIFO storage spacing with heterogeneous native speed overlay");
    }
    private static void UnknownChangesFailClosed()
    {
        var f = new Fixture(); var roads = f.Build();
        f.Native[1].maxSpeed = 25;
        Refuses(() => f.Plan(roads), "unexplained raw native speed mutation was accepted");
        Refuses(() => roads.ResolveWaypoint(roads.TopologyId, 1), "unexplained raw speed still resolved");
        Refuses(() => f.Profile.Canonical(f.Native[1]), "unexplained raw speed became canonical");
        f.Native[1].maxSpeed = 20; f.Overrides.Add(f.Native[1], (35, 20));
        Refuses(() => roads.Prepare(), "changed original speed was accepted over captured canonical speed");
        f.Overrides[f.Native[1]] = (30, 20); f.Native[1].maxSpeed = 19;
        Refuses(() => roads.Prepare(), "verified tuple with unexpected native observed speed was accepted");
        var geometry = new Fixture(); var unchangedContract = geometry.Build();
        geometry.Native[1].position = new UnityEngine.Vector3(10, 0, 1);
        Check(unchangedContract.ResolveWaypoint(unchangedContract.TopologyId, 1) == null && !unchangedContract.IsCurrent,
            "speed compatibility weakened native geometry validation");
        Console.WriteLine("PASS unexplained raw/original/observed speed mutations fail closed");
    }
    private static void Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Guard.MainThread = Environment.CurrentManagedThreadId;
        StagedPreflight(); UnknownChangesFailClosed(); ExistingQueueAndFutureRoutes(); GeometricSpacing();
        Console.WriteLine("PASS " + checks + " offline assertions using actual Runtime/Core/Persistence and synthetic Unity/native surfaces; no rig or RLO mutation.");
    }
    private sealed class References : IEqualityComparer<object>
    {
        internal static readonly References Instance = new References();
        public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
