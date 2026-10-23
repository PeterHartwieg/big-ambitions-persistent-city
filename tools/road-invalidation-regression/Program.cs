using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;
using PersistentCity.Runtime;
using StagingHarness;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private sealed class Fixture
    {
        public readonly Waypoint[] Native;
        public readonly TrafficLightsIntersection Light;
        public readonly CurrentSceneData Scene;
        public readonly MultimodalRoutes Planner;
        public RoadRoutes Roads => (RoadRoutes)typeof(MultimodalRoutes).GetField("roads", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Planner)!;
        public Fixture(int count = 17, bool prepare = true, IRouteObjectSource? objects = null)
        {
            Object.SceneObjects = Array.Empty<object>(); Object.Doors = Array.Empty<FenceDoor>();
            CityManager.Instance = new CityManager();
            Native = Enumerable.Range(0, count).Select(i => new Waypoint { listIndex = i, maxSpeed = 36,
                position = new Vector3(i * 10, 0, 0), neighbors = i + 1 < count ? new List<int> { i + 1 } : new List<int>() }).ToArray();
            Light = new TrafficLightsIntersection { name = "traffic-light", exitWaypoints = new List<int> { 7, 15 },
                Groups = new List<IntersectionStopWaypointsIndex> { new IntersectionStopWaypointsIndex { roadWaypoints = new List<int> { 3 } },
                    new IntersectionStopWaypointsIndex { roadWaypoints = new List<int> { 11 } } } };
            foreach (int i in new[] { 3, 7, 11, 15 }) Native[i].Associate(Light);
            Native[3].enter = Native[11].enter = true; Native[7].exit = Native[15].exit = true;
            Scene = new CurrentSceneData { allWaypoints = Native, allLightsIntersections = new[] { Light },
                allPriorityIntersections = Array.Empty<PriorityIntersection>(), roadIds = new int[count] };
            Object.Scene = Scene;
            Planner = new MultimodalRoutes(Array.Empty<Place>(), 2, 1, null, objects);
            if (prepare) Ready();
        }
        public void Ready(Action? betweenSlices = null)
        {
            for (int i = 0; i < 10000; i++)
            {
                betweenSlices?.Invoke(); Time.frameCount++;
                if (Planner.Prepare()) return;
                Thread.Sleep(1);
            }
            throw new Exception("preflight did not finish");
        }
        public void Sweep()
        {
            Roads.BeginSweep();
            for (int i = 0; i < 20 && Roads.IsCurrent; i++) Roads.Sweep(1000, 2048);
        }
        public bool AllOpen()
        {
            Planner.BeginTrafficClosureBatch();
            try { return Enumerable.Range(0, Planner.TrafficNetwork!.Links.Count).All(id => !Planner.IsTrafficLinkClosed(id)); }
            finally { Planner.EndTrafficClosureBatch(); }
        }
    }
    private sealed class Objects : IRouteObjectSource
    {
        public bool Ready { get; set; }
        public int Requests;
        public void RequestSweep(){Ready=false;Requests++;}
        public int Revision { get; set; }
        public FenceDoor[] Doors { get; set; } = Array.Empty<FenceDoor>();
        public SubwayStation[] Stations { get; set; } = Array.Empty<SubwayStation>();
    }
    private static string Reason(RoadRoutes roads) => (string?)typeof(RoadRoutes).GetProperty("InvalidationReason")?.GetValue(roads) ?? "unavailable-on-baseline";
    private static void Main(string[] args)
    {
        Guard.MainThread = Environment.CurrentManagedThreadId;
        var staged = new Fixture(4097, false); bool red = false;
        staged.Ready(() => { red = !red; staged.Native[3].stop = red; staged.Native[11].stop = !red; });
        Check(staged.Planner.RoadSnapshots == 1, "light changes during staged import rebuilt the road snapshot");
        var f = new Fixture(); var network = f.Planner.TrafficNetwork; string topology = f.Roads.TopologyId;
        int searches = Object.SceneSearches;
        var original = f.Roads.Plan(new RoadRoutes.Curb { Node = 0, Position = new Position(0, 0, 0) },
            new RoadRoutes.Curb { Node = 16, Position = new Position(160, 0, 0) });
        Check(original != null, "route fixture missing");
        for (int cycle = 0; cycle < 40; cycle++)
        {
            f.Native[3].stop = cycle % 2 == 0; f.Native[11].stop = !f.Native[3].stop;
            f.Sweep(); Check(f.Roads.IsCurrent, "ordinary signal change invalidated topology");
            Check(f.AllOpen(), "red signal was incorrectly treated as a road closure");
            f.Ready();
            var route = f.Roads.Plan(new RoadRoutes.Curb { Node = 0, Position = new Position(0, 0, 0) },
                new RoadRoutes.Curb { Node = 16, Position = new Position(160, 0, 0) });
            Check(route != null && route.Minutes == original!.Minutes && route.Legs.Select(l => l.ServiceId).SequenceEqual(original.Legs.Select(l => l.ServiceId)), "signal toggle changed route geometry/timing/tags");
            Check(ReferenceEquals(network, f.Planner.TrafficNetwork) && topology == f.Roads.TopologyId && f.Planner.RoadSnapshots == 1, "signal toggle replaced network");
        }
        Check(Object.SceneSearches == searches, "steady signal transitions searched the scene");
        f.Native[4].temporaryDisabled = true; Check(!f.AllOpen(), "temporary closure missed");
        f.Native[4].temporaryDisabled = false; Check(f.AllOpen() && f.Roads.IsCurrent, "same-frame closure reopening missed");
        foreach (var mutation in new (string Reason, Action<Fixture> Change)[] {
            ("waypoint-flags", x => x.Native[4].enter = true), ("waypoint-flags", x => x.Native[4].exit = true),
            ("waypoint-flags", x => x.Native[4].giveWay = true), ("position", x => x.Native[4].position.x += 1),
            ("agent-restrictions", x => x.Native[4].allowedAgents!.Clear()),
            ("car-restrictions", x => x.Native[4].allowedCars!.Clear()), ("neighbours", x => x.Native[4].neighbors!.Clear()),
            ("neighbours", x => x.Native[16].neighbors = null), ("waypoint-index", x => x.Native[4].listIndex++),
            ("lane-links", x => x.Native[4].otherLanes!.Add(5)), ("road-id", x => x.Scene.roadIds![4]++),
            ("junction-association", x => x.Native[3].Associate(null)), ("junction-exits", x => x.Light.exitWaypoints!.Add(16)),
            ("junction-groups", x => x.Light.Groups[0].roadWaypoints.Add(4)), ("signal-timing", x => x.Light.greenLightTime++),
            ("signal-timing", x => x.Light.yellowLightTime++), ("junction-name", x => x.Light.name += "new"),
            ("signal-junction-array", x => x.Scene.allLightsIntersections = new[] { x.Light }),
            ("priority-junction-array", x => x.Scene.allPriorityIntersections = new PriorityIntersection[0]),
            ("waypoint-array", x => x.Scene.allWaypoints = x.Native.ToArray()),
            ("waypoint-identity", x => x.Native[4] = new Waypoint()), ("unassociated-stop", x => x.Native[4].stop = true) })
        {
            var x = new Fixture(); mutation.Change(x); x.Sweep();
            Check(!x.Roads.IsCurrent, mutation.Reason + " not invalidated");
            Check(Reason(x.Roads) == mutation.Reason, mutation.Reason + " misdiagnosed as " + Reason(x.Roads));
            Check(!x.AllOpen(), "stale topology did not fail closed");
        }
        var speed = new Fixture(); speed.Native[4].maxSpeed++; bool speedRejected = false;
        try { speed.Sweep(); } catch (InvalidOperationException) { speedRejected = true; }
        Check(speedRejected && Reason(speed.Roads) == "unverified-speed", "unverified speed mutation did not fail closed");
        // Reject an unassociated stop introduced AFTER that node was captured, before publication.
        var mid = new Fixture(prepare: false);
        var dto = mid.Native.Select(w => new TransportRoadNode { Position = new Position(w.position.x, 0, 0),
            KilometresPerHour = 36, AllowsCar = true, Successors = w.neighbors!.ToArray() }).ToArray();
        var importer = new TrafficRoadNetwork("fixture", dto, mid.Native, mid.Scene, new PublicRoadAccess(), 1, _ => true);
        var cursor = typeof(TrafficRoadNetwork).GetField("nodeCursor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        while ((int)cursor.GetValue(importer)! <= 4) importer.Prepare(1000, 1);
        mid.Native[4].stop = true;
        for (int n = 0; n < 10000 && !importer.Stale; n++) { importer.Prepare(1000, 1); Thread.Sleep(1); }
        Check(importer.Stale && importer.Network == null, "late unassociated stop was published");
        bool captureRejected = false;
        try { var bad = new TrafficRoadNetwork("fixture", dto, mid.Native, mid.Scene, new PublicRoadAccess(), 1, _ => true);
            for (int n = 0; n < 30; n++) bad.Prepare(1000, 1); }
        catch (InvalidOperationException) { captureRejected = true; }
        Check(captureRejected, "initial unassociated stop was accepted");
        // Prove actual shape edits rebuild without a global search, and diagnostics survive replacement.
        var rebuild = new Fixture(); searches = Object.SceneSearches;
        for (int i = 0; i < 20; i++) { rebuild.Native[4].position.z += 1; rebuild.Sweep(); rebuild.Ready(); }
        Check(rebuild.Planner.RoadSnapshots == 21, "structural changes did not rebuild");
        Check(Object.SceneSearches == searches, "structural rebuild unnecessarily searched all scene objects");
        Check(rebuild.Planner.LastRoadRebuildReason == "position" && rebuild.Planner.LastRoadInvalidationIndex == 4, "lost invalidation reason at rebuild");
        // Missing initial scene retries on refresh; a new city and a destroyed scene must rediscover.
        var missing = new Fixture(prepare: false); Object.Scene = null; missing.Ready();
        Object.Scene = missing.Scene; missing.Planner.Refresh(); missing.Ready(); Check(missing.Roads.Available, "late road scene not found");
        searches = Object.SceneSearches; missing.Scene.Destroyed = true;
        Object.Scene = new CurrentSceneData { allWaypoints = missing.Native }; missing.Ready();
        Check(Object.SceneSearches == searches + 1 && missing.Roads.Available, "destroyed scene not rediscovered");
        searches = Object.SceneSearches; CityManager.Instance = new CityManager(); missing.Ready();
        Check(Object.SceneSearches == searches + 1, "new city reused the previous scene cache");
        // The runtime discovery source must replace global gate/station lookups, while
        // preserving initial preflight, additions/removals, station eligibility and geometry changes.
        var objects = new Objects(); var cached = new Fixture(prepare: false, objects: objects);
        int doorSearches = Object.DoorSearches, stationSearches = Object.StationSearches;
        Time.frameCount++;
        Check(!cached.Planner.Prepare() && !cached.Planner.ReadyForPopulation && cached.Planner.RoadSnapshots == 0,
            "partial inventory published population modes");
        objects.Ready = true; cached.Ready();
        var gate = new FenceDoor { acceptsVehicles = true, name = "added gate", transform = new Transform { position = new Vector3(80, 0, 0) } };
        objects.Doors = new[] { gate }; objects.Revision++;
        Check(!cached.Planner.ReadyForPopulation, "new inventory did not fence old readiness");
        cached.Ready();
        Check(cached.Planner.RoadSnapshots == 2 && cached.Planner.LastRoadRebuildReason == "gate-change", "new cached gate did not rebuild");
        gate.transform.position.z += 10; cached.Planner.Refresh(); cached.Ready();
        Check(cached.Planner.RoadSnapshots == 3, "live gate geometry not refreshed from retained object");
        objects.Doors = Array.Empty<FenceDoor>(); objects.Revision++; cached.Ready();
        Check(cached.Planner.RoadSnapshots == 4, "removed cached gate did not rebuild");
        var addedStation = new SubwayStation { stationName = "Added", Entrance = new Vector3(200,0,0) };
        objects.Stations = new[] { addedStation }; objects.Revision++; cached.Ready();
        var stationField = typeof(MultimodalRoutes).GetField("stations", BindingFlags.NonPublic | BindingFlags.Instance)!;
        int StationCount() => ((System.Collections.IList)stationField.GetValue(cached.Planner)!).Count;
        Check(StationCount() == 1 && cached.Planner.RoadSnapshots == 4, "new station missing or rebuilt unchanged roads");
        addedStation.gameObject.activeInHierarchy = false; cached.Planner.RefreshStations(); cached.Ready();
        Check(StationCount() == 0, "disabled cached station still eligible");
        addedStation.gameObject.activeInHierarchy = true; cached.Planner.RefreshStations(); cached.Ready();
        Check(StationCount() == 1, "reactivated cached station not eligible");
        for (int i = 0; i < 10; i++) { cached.Planner.Refresh(); cached.Planner.RefreshStations(); cached.Ready(); }
        Check(Object.DoorSearches == doorSearches && Object.StationSearches == stationSearches,
            "cached preparation/refresh fell back to whole-scene discovery");
        Check(cached.Planner.RoadSnapshots == 4, "unchanged source caused recurring road rebuilds");
        CityManager.Instance = new CityManager(); Time.frameCount++;
        Check(!cached.Planner.Prepare() && !objects.Ready && objects.Requests == 1 && !cached.Planner.ReadyForPopulation,
            "city change published ready old inventory");
        objects.Ready = true; objects.Revision++; cached.Ready();
        cached.Scene.Destroyed = true; Object.Scene = new CurrentSceneData { allWaypoints = cached.Native }; Time.frameCount++;
        Check(!cached.Planner.Prepare() && !objects.Ready && objects.Requests == 2,
            "lost road scene reused ready old inventory");
        objects.Ready = true; objects.Revision++; cached.Ready();
        Console.WriteLine("PASS " + checks + " road invalidation checks; 40 signal cycles preserve network; 20 genuine rebuilds perform zero additional global road-scene searches.");
    }
}
