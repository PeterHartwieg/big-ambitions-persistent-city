using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static string Hash(object value)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value,
            new JsonSerializerOptions { IncludeFields = true }))));
    }
    private sealed class Fixture
    {
        internal readonly Waypoint[] Native;
        internal readonly PriorityIntersection[] Junctions;
        internal readonly CurrentSceneData Scene;
        internal readonly MultimodalRoutes Planner;
        internal readonly RoadRoutes Roads;
        internal readonly CitySimulator Engine;
        internal readonly PublicRoadAccess Access;
        internal Fixture(int nodes = 97, bool junctions = true, int demand = 40, int gates = 0)
        {
            Guard.MainThread = Environment.CurrentManagedThreadId; Guard.FailAtRead = -1;
            Native = Enumerable.Range(0, nodes).Select(i => new Waypoint { listIndex = i, maxSpeed = 36,
                position = new Vector3(i * 10, 0, 0), neighbors = i + 1 < nodes ? new List<int> { i + 1 } : new List<int>() }).ToArray();
            Junctions = junctions ? Enumerable.Range(0, (nodes + 15) / 16).Select(group => new PriorityIntersection
            {
                name = "junction" + group,
                Groups = new List<IntersectionStopWaypointsIndex> { new IntersectionStopWaypointsIndex {
                    roadWaypoints = Enumerable.Range(group * 16, Math.Min(16, nodes - group * 16)).ToList() } }
            }).ToArray() : Array.Empty<PriorityIntersection>();
            if (junctions) for (int i = 0; i < nodes; i++) Native[i].Associate(Junctions[i / 16]);
            Scene = new CurrentSceneData { allWaypoints = Native, allPriorityIntersections = Junctions,
                allLightsIntersections = Array.Empty<TrafficLightsIntersection>(), roadIds = new int[nodes] };
            Object.Scene = Scene; Object.SceneObjects = Array.Empty<object>(); CityManager.Instance = new CityManager();
            Object.Doors = Enumerable.Range(0, gates).Select(i => Door(i * 10 + 5, 10)).ToArray();
            UnityEngine.AI.NavMesh.SampleTransform = null;
            var home = new Place { Id = "home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0, 0, 0) };
            Planner = new MultimodalRoutes(new[] { home }, 2, 1, null);
            for (int attempt = 0; ; attempt++)
            {
                Time.frameCount++;
                if (Planner.Prepare()) break;
                if (attempt > 10000) throw new Exception("Preflight failed");
                Thread.Sleep(1);
            }
            Roads = (RoadRoutes)typeof(MultimodalRoutes).GetField("roads", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Planner)!;
            Access = (PublicRoadAccess)typeof(MultimodalRoutes).GetField("access", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Planner)!;
            var state = Population.Create(new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 30,
                AppearanceSeed = 1, Persona = Persona.Commuter } }, new[] { home }, "closure-harness", "0.40.4", 7, 1440);
            Engine = new CitySimulator(state, Planner); Engine.RefreshTrafficClosures();
            var network = Roads.TrafficNetwork!;
            // Isolated kernel demand exercises actual refresh/scheduling. These fixture IDs are not a saveable city.
            for (int i = 0; i < demand; i++)
            {
                int id = i % network.Links.Count, next = (id + 1) % network.Links.Count;
                double free = network.Links[id].FreeFlowMinutes;
                Engine.Traffic!.Enqueue("fixture" + i, new List<TrafficRouteLeg> {
                    new TrafficRouteLeg { LinkId = id, StartNominalMinutes = 0, EndNominalMinutes = free },
                    new TrafficRouteLeg { LinkId = next, StartNominalMinutes = free,
                        EndNominalMinutes = free + network.Links[next].FreeFlowMinutes } });
            }
            Engine.Traffic!.AdvanceTo(1440, Math.Max(1, demand * 4)); // Admit only; no vehicle can complete at this clock.
        }
        internal string Fingerprint() => Hash(new { traffic = Engine.Traffic!.Capture(), current = Roads.IsCurrent,
            relevant = Engine.Traffic.RelevantLinkIds.ToArray() });
        internal void Refresh() => Engine.RefreshTrafficClosures();
    }
    private static FenceDoor Door(float x, float z) => new FenceDoor { acceptsVehicles = true, name = "fixture-gate",
        Colliders = new Collider[] { new BoxCollider { size = new Vector3(2, 4, 2),
            transform = new Transform { position = new Vector3(x, 0, z) } } } };
    private static void Main(string[] args)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var results = new List<object>();
        void Record(string name, Fixture f)
        {
            long start = Guard.Reads; f.Refresh();
            results.Add(new { name, fingerprint = f.Fingerprint(), nativeReads = Guard.Reads - start });
        }
        var basic = new Fixture(); Record("initial", basic); Record("repeat", basic);
        int closureFrame = Time.frameCount;
        basic.Native[4].temporaryDisabled = true; Record("temporary-close", basic);
        Check(basic.Engine.Traffic!.Capture().Links.Any(l => l.Closed), "closure missed");
        basic.Native[4].temporaryDisabled = false; Record("same-frame-reopen", basic);
        Check(basic.Engine.Traffic.Capture().Links.All(l => !l.Closed), "reopening missed");
        Check(Time.frameCount == closureFrame, "fixture advanced the native frame between close/reopen passes");
        // An ordinary boundary query after a completed batch must read live state too.
        basic.Native[0].temporaryDisabled = true;
        Check(basic.Roads.IsTrafficLinkClosed(0), "boundary reused old batch");
        basic.Native[0].temporaryDisabled = false;
        Check(!basic.Roads.IsTrafficLinkClosed(0), "boundary did not reopen");
        Record("after-boundary", basic);
        // Candidate wraparound path, absent in baseline; behavior must remain identical.
        if (typeof(RoadRoutes).GetField("closureCheckedAt", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(basic.Roads) is int[] stamps)
            Array.Fill(stamps, 1); // A generation from before wrap must not be treated as current.
        typeof(RoadRoutes).GetField("closureGeneration", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(basic.Roads, int.MaxValue);
        basic.Native[5].temporaryDisabled = true; Record("generation-wrap", basic);
        Check(basic.Engine.Traffic.Capture().Links.Any(l => l.Closed), "wrapped stamps reused an old open result");
        basic.Native[5].temporaryDisabled = false; Record("after-wrap", basic);

        Object.Doors = new[] { Door(5, 0) }; basic.Roads.SetAccess(new PublicRoadAccess());
        Record("new-gate-snapshot", basic);
        Check(basic.Engine.Traffic.Capture().Links.Any(l => l.Closed), "gate cache did not invalidate");
        basic.Roads.SetAccess(basic.Access); Record("gate-removed", basic);
        Check(basic.Engine.Traffic.Capture().Links.All(l => !l.Closed), "gate removal did not invalidate");

        var failing = new Fixture(); failing.Refresh();
        Guard.FailAtRead = Guard.Reads + 1; bool threw = false;
        try { failing.Refresh(); } catch (InvalidOperationException) { threw = true; }
        finally { Guard.FailAtRead = -1; }
        Check(threw, "native failure was not injected");
        failing.Native[2].temporaryDisabled = true; Record("after-read-exception", failing);
        Check(failing.Engine.Traffic!.Capture().Links.Any(l => l.Closed), "failed batch polluted retry");
        failing.Native[2].temporaryDisabled = false; Record("exception-reopened", failing);

        foreach (var mutation in new (string Name, Action<Fixture> Change)[] {
            ("agents", f => f.Native[4].allowedAgents!.Clear()),
            ("cars", f => f.Native[4].allowedCars!.Clear()),
            ("neighbours", f => f.Native[4].neighbors!.Clear()),
            ("position", f => f.Native[4].position.x += 1),
            ("flags", f => f.Native[4].giveWay = true),
            ("road-id", f => f.Scene.roadIds![4] = 9),
            ("association", f => f.Native[4].Associate(f.Junctions[1])),
            ("lanes", f => f.Native[4].otherLanes!.Add(3)),
            ("junction-group", f => f.Junctions[0].Groups[0].roadWaypoints.Add(96)),
            ("junction-name", f => f.Junctions[0].name += "changed"),
            ("junction-array", f => f.Scene.allPriorityIntersections = f.Junctions.ToArray()),
            ("waypoint-array", f => f.Scene.allWaypoints = f.Native.ToArray()) })
        {
            var f = new Fixture(); f.Refresh(); mutation.Change(f); Record(mutation.Name, f);
            Check(!f.Roads.IsCurrent, mutation.Name + " failed to invalidate snapshot");
            Check(f.Engine.Traffic!.Capture().Links.Any(l => l.Closed), mutation.Name + " failed to close");
        }
        // Empty closed links must remain in the refresh set until they reopen.
        var dormant = new Fixture(demand: 0); dormant.Engine.Traffic!.SetLinkClosed(3, true);
        Record("empty-closed-reopened", dormant);
        Check(!dormant.Engine.Traffic.RelevantLinkIds.Any(), "dormant reopened section remains relevant");
        Console.WriteLine(JsonSerializer.Serialize(new { checks, results }));
        if (args.Contains("--benchmark-fixtures") || args.Contains("--benchmark"))
        {
            foreach (string name in new[] { "junction-heavy", "long-sections", "gate-heavy", "sparse", "empty" })
            {
                var f = BenchmarkFixture(name); f.Refresh(); string expected = f.Fingerprint();
                if (args.Contains("--benchmark-fixtures"))
                { Console.WriteLine(JsonSerializer.Serialize(new { benchmark = name, fingerprint = expected })); continue; }
                const int iterations = 300;
                for (int i = 0; i < 10; i++) f.Refresh();
                var times = new List<double>(); var allocations = new List<long>(); long nativeReads = 0;
                for (int batch = 0; batch < 5; batch++)
                {
                    long startReads = Guard.Reads, startBytes = GC.GetAllocatedBytesForCurrentThread();
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < iterations; i++) f.Refresh();
                    double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    long bytes = GC.GetAllocatedBytesForCurrentThread() - startBytes, reads = Guard.Reads - startReads;
                    if (batch == 0) nativeReads = reads;
                    else Check(reads == nativeReads, "unstable native read counts");
                    allocations.Add(bytes);
                    Check(f.Fingerprint() == expected, "timed refresh changed the result"); times.Add(ms);
                }
                times.Sort(); allocations.Sort(); Console.WriteLine(JsonSerializer.Serialize(new { benchmark = name, fingerprint = expected,
                    iterations, medianMilliseconds = times[2], nativeReads, allocatedBytes = allocations[2], allocationSamples = allocations }));
            }
        }
    }
    private static Fixture BenchmarkFixture(string name) => name switch {
        "junction-heavy" => new Fixture(1025, true, 957),
        "long-sections" => new Fixture(1025, false, 128),
        "gate-heavy" => new Fixture(1025, false, 128, 512),
        "sparse" => new Fixture(1025, true, 1),
        "empty" => new Fixture(1025, true, 0),
        _ => throw new ArgumentException(name)
    };
}
