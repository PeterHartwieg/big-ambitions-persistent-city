using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
    private static bool Same(Position a, Position b) => a.Equals(b);
    private static Route Walk(Position a, Position b, double minutes) => new Route
    { Legs = new List<RouteLeg> { new RouteLeg { From = a, To = b, Mode = TravelMode.Walk, Minutes = minutes } } };
    private static string Fingerprint(Route? route)
    {
        if (route == null) return "null";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(route, new JsonSerializerOptions { IncludeFields = true }))));
    }
    private sealed class Fixture
    {
        internal readonly MultimodalRoutes Planner;
        internal readonly Place From, To;
        internal readonly CitizenState Person;
        internal readonly Waypoint[] Native;
        internal readonly Position Parked, End;
        internal double? DirectMinutes;
        internal double AccessMinutes, EgressMinutes;
        internal double Delay;
        internal bool Closed;
        internal bool NoEgress;
        internal bool HaveSubway;
        internal double? SecondEgressMinutes;
        // A second home curb the walking stub can reach, for a home car allocated on a replacement snapshot.
        internal Position? SecondHomeCurb;
        internal int ForecastCalls;
        internal Fixture(int count, bool reachable, double rate = 1, double curbOffset = 0, float platformHeight = 0, float endOffset = 0)
        {
            StagingHarness.Guard.MainThread = Environment.CurrentManagedThreadId;
            AccessMinutes = EgressMinutes = .5 * rate;
            Native = Enumerable.Range(0, count).Select(i => new Waypoint { listIndex = i,
                position = new Vector3(i * 10, 0, 0), maxSpeed = 36,
                neighbors = i + 1 < count ? new List<int> { i + 1 } : new List<int>() }).ToArray();
            if (!reachable) { Native[count - 2].neighbors!.Clear(); Native[count - 1].position = new Vector3(0, 0, 30); }
            Object.Scene = new CurrentSceneData { allWaypoints = Native, allPriorityIntersections = Array.Empty<PriorityIntersection>(),
                allLightsIntersections = Array.Empty<TrafficLightsIntersection>(), roadIds = new int[count] };
            Object.Doors = Array.Empty<FenceDoor>();
            var goal = Native[count - 1].position;
            UnityEngine.AI.NavMesh.SampleTransform = endOffset == 0 ? null
                : p => p.x == goal.x && p.z == goal.z ? new Vector3(p.x, p.y, p.z + endOffset) : p;
            Parked = new Position(0, 0, curbOffset); End = new Position(goal.x, goal.y, goal.z + endOffset);
            From = new Place { Id = "home", Position = new Position(0, 0, curbOffset - 1) };
            To = new Place { Id = "work", Position = new Position(End.X, 0, End.Z + 1) };
            Person = new CitizenState { Identity = new CitizenIdentity { Id = "fixture" }, HomeId = "home", PlaceId = "home",
                Phase = CitizenPhase.AtPlace, Preferences = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 100000 },
                Car = new CarState { Id = "fixture:car", ParkedPosition = Parked } };
            CityManager.Instance = new CityManager();
            Object.SceneObjects = new object[] {
                new SubwayStation { stationName = "a", Entrance = new Vector3((float)From.Position.X, 0, (float)From.Position.Z),
                    transform = new Transform { position = new Vector3((float)From.Position.X, platformHeight, (float)From.Position.Z) } },
                new SubwayStation { stationName = "b", Entrance = new Vector3((float)To.Position.X, 0, (float)To.Position.Z),
                    transform = new Transform { position = new Vector3((float)To.Position.X, platformHeight, (float)To.Position.Z) } } };
            WalkingRoutes.Routes = (a, b) =>
            {
                if (a.Id == "home" && b.Id == "work") return DirectMinutes.HasValue ? Walk(a.Position, b.Position, DirectMinutes.Value) : null;
                if (b.Id.StartsWith("subway:") || a.Id.StartsWith("subway:"))
                    return HaveSubway && Same(a.Position, b.Position) ? Walk(a.Position, b.Position, .01) : null;
                if (a.Id == "home" && Same(b.Position, Parked)) return Walk(a.Position, b.Position, AccessMinutes);
                if (a.Id == "home" && SecondHomeCurb.HasValue && Same(b.Position, SecondHomeCurb.Value)) return Walk(a.Position, b.Position, AccessMinutes);
                if (b.Id == "work" && Same(a.Position, End) && !NoEgress) return Walk(a.Position, b.Position, EgressMinutes);
                if (b.Id == "work" && SecondEgressMinutes.HasValue && Same(a.Position, Point(Native[count - 2].position)))
                    return Walk(a.Position, b.Position, SecondEgressMinutes.Value);
                return null;
            };
            Planner = new MultimodalRoutes(new[] { From, To }, 2 / rate, rate, null);
            for (int tries = 0; ; tries++)
            {
                Time.frameCount++;
                if (Planner.Prepare()) break;
                if (tries > 10000) throw new Exception("Preflight did not finish");
                Thread.Sleep(1);
            }
            Planner.SetTrafficForecast((id, _) => { ForecastCalls++; return new TrafficLinkForecast(id, 0, 0, Delay, Closed); });
        }
        internal Route? Plan() => Planner.Plan(From, To, 100, Person);
#if SLICED_PLANNER
        // The same request through the resumable job, one unit of work per call (budget 0), as a frame loop would drive it.
        internal (Route? Route, int Slices) PlanSliced()
        {
            using var job = Planner.Begin(new RouteRequest(From, To, 100, Person, Person.Preferences));
            int slices = 1;
            while (!job.Continue(0)) slices++;
            return (job.Result, slices);
        }
        // The traffic network is constructed on a pool task; retry at the fixture's pace until preparation reports ready.
        internal void Preflight()
        {
            for (int tries = 0; ; tries++)
            {
                Time.frameCount++;
                if (Planner.Prepare()) break;
                if (tries > 10000) throw new Exception("Preflight did not finish");
                Thread.Sleep(1);
            }
        }
#endif
    }

    private static void Main(string[] args)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var results = new List<object>();
#if SLICED_PLANNER
        var sliced = new List<object>();
        int slicedChecks = 0;
        void SlicedCheck(bool value, string message) { slicedChecks++; if (!value) throw new Exception(message); }
#endif
        void Record(string name, Fixture f, TravelMode? mode)
        {
            long reads = StagingHarness.Guard.Reads;
            var route = f.Plan(); reads = StagingHarness.Guard.Reads - reads;
            if (mode.HasValue) Check(route != null && route.Legs.Any(l => l.Mode == mode.Value), name + " wrong mode");
            else Check(route == null, name + " expected refusal");
            if (route != null) Check(TransportComposition.Valid(route, f.From.Position, f.To.Position, f.Person.Preferences, f.Parked), name + " invalid result");
            results.Add(new { name, fingerprint = Fingerprint(route), minutes = route?.Minutes, estimate = route?.EstimatedMinutes, nativeReads = reads });
#if SLICED_PLANNER
            // Every case again through the sliced job: the complete route fingerprint must not change with slicing.
            long slicedReads = StagingHarness.Guard.Reads;
            var (slicedRoute, slices) = f.PlanSliced(); slicedReads = StagingHarness.Guard.Reads - slicedReads;
            SlicedCheck(Fingerprint(slicedRoute) == Fingerprint(route), name + " sliced route differs from the one-shot route");
            SlicedCheck(slices >= 3, name + " ran in too few slices: " + slices);
            sliced.Add(new { name, fingerprint = Fingerprint(slicedRoute), slices, nativeReads = slicedReads });
#endif
        }
        foreach (double rate in new[] { .01, 1, 2, 1000 })
        {
            var f = new Fixture(31, true, rate, 3) { DirectMinutes = 2000 * rate, AccessMinutes = 2 * rate, EgressMinutes = 3 * rate };
            Record("car-wins-" + rate, f, TravelMode.Car);
            f.Delay = 10000 * rate; Record("congestion-walk-" + rate, f, TravelMode.Walk);
            f.HaveSubway = true; Record("congestion-subway-" + rate, f, TravelMode.Subway);
            f.Delay = 0; f.HaveSubway = false; Record("drained-car-" + rate, f, TravelMode.Car);
            f.DirectMinutes = null; f.Delay = 10000 * rate; Record("no-alternative-" + rate, f, TravelMode.Car);
            f.Delay = 0; f.Closed = true; Record("closed-" + rate, f, null);
            f.Closed = false; f.NoEgress = true; Record("no-egress-" + rate, f, null);
        }
        foreach (bool connectors in new[] { false, true })
        {
            var f = new Fixture(3, true, curbOffset: connectors ? 6 : 0, endOffset: connectors ? 3 : 0) { Delay = 4 };
            var car = f.Plan()!;
            foreach (var pair in new[] { ("tie", car.EstimatedMinutes), ("ulp-car-win", Math.BitIncrement(car.EstimatedMinutes)),
                ("ulp-walk-win", Math.BitDecrement(car.EstimatedMinutes)) })
            {
                f.DirectMinutes = pair.Item2;
                Record((connectors ? "connectors-" : "") + pair.Item1, f,
                    pair.Item1 == "ulp-car-win" ? TravelMode.Car : TravelMode.Walk);
            }
        }
        {
            var f = new Fixture(10, true) { DirectMinutes = 100, EgressMinutes = 20, SecondEgressMinutes = 6 };
            Record("second-curb-car-wins", f, TravelMode.Car);
            Check(f.Plan()!.Legs.Where(l => l.ServiceId.StartsWith("native:road:")).Last().To.X == 80,
                "second curb did not replace the first car candidate");
        }
        {
            // The subway beats walking but is slower than the geometric car lower bound.
            // A car search must actually start and use the newer subway ETA as its ceiling.
            var f = new Fixture(31, true, platformHeight: 50) { DirectMinutes = 2000, HaveSubway = true, Delay = 10000 };
            Record("subway-ceiling-during-car-search", f, TravelMode.Subway);
            Check(f.ForecastCalls > 0, "subway fixture skipped car A* at the pre-existing geometric bound");
        }
        var stranded = new Fixture(1200, false) { DirectMinutes = 30 };
        Record("unreachable-walk", stranded, TravelMode.Walk);
        stranded.DirectMinutes = null; Record("unreachable-no-alternative", stranded, null);
#if SLICED_PLANNER
        {
            // A road search suspended mid-A*: a one-shot plan meanwhile reuses the per-node memo generation, so the
            // suspended search starts over and still answers the one-shot route.
            var f = new Fixture(300, true) { DirectMinutes = 2000, AccessMinutes = 2, EgressMinutes = 3 };
            string expected = Fingerprint(f.Plan());
            using var job = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, f.Person, f.Person.Preferences));
            int units = 0;
            while (f.Planner.LastJobUnit != "car-search") { SlicedCheck(!job.Continue(0), "job finished before its road search started"); units++; }
            SlicedCheck(!job.Continue(0) && f.Planner.LastJobUnit == "car-search", "a 300-node road search finished in one 64-expansion slice");
            SlicedCheck(Fingerprint(f.Plan()) == expected, "one-shot plan during a suspended search changed");
            SlicedCheck(f.Planner.RoadSearchRestarts == 0, "road search restarted before resuming");
            while (!job.Continue(0)) units++;
            SlicedCheck(f.Planner.RoadSearchRestarts == 1, "suspended road search did not start over after another search");
            SlicedCheck(Fingerprint(job.Result) == expected, "restarted road search answered a different route");
            sliced.Add(new { name = "search-generation-restart", fingerprint = Fingerprint(job.Result), slices = units, nativeReads = 0L });
        }
        {
            // The road snapshot is replaced between slices (a native node changed; the sweep marks the snapshot
            // stale; preparation captures a new one). The job refuses to mix two snapshots: Continue throws so the
            // simulator can replay the whole event, and a fresh job on the new snapshot answers the one-shot route.
            var f = new Fixture(31, true) { DirectMinutes = 2000, AccessMinutes = 2, EgressMinutes = 3 };
            string expected = Fingerprint(f.Plan());
            int snapshots = f.Planner.RoadSnapshots;
            var job = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, f.Person, f.Person.Preferences));
            for (int i = 0; i < 3; i++) SlicedCheck(!job.Continue(0), "job finished before the snapshot change");
            var contract = f.Native[10].allowedAgents;
            f.Native[10].allowedAgents = null;
            SlicedCheck(f.Planner.Refresh() == "", "gates changed unexpectedly");
            Time.frameCount++;
            SlicedCheck(!f.Planner.Prepare(), "the sweep did not notice the changed node");
            f.Native[10].allowedAgents = contract;
            f.Preflight();
            SlicedCheck(f.Planner.RoadSnapshots == snapshots + 1 && f.Planner.ReadyForPopulation, "no replacement snapshot");
            bool refused = false;
            try { job.Continue(0); }
            catch (RoutePlanningRestartRequired) { refused = true; }
            job.Dispose();
            SlicedCheck(refused, "a job resumed on a replaced snapshot did not ask for an event restart");
            SlicedCheck(job.Result == null, "a refused job kept a result");
            var (fresh, units) = f.PlanSliced();
            SlicedCheck(Fingerprint(fresh) == expected, "a fresh job on the replacement snapshot answered a different route");
            SlicedCheck(Fingerprint(f.Plan()) == expected, "one-shot plan after the replacement differs");
            sliced.Add(new { name = "snapshot-replacement-restart", fingerprint = Fingerprint(fresh), slices = units, nativeReads = 0L });
        }
        {
            // The snapshot is replaced after the job was begun but before its first slice: the frame that answered
            // the event's previous request also began this one, then preparation installed a new snapshot. The
            // first Continue must refuse, since the earlier answer belongs to the old snapshot.
            var f = new Fixture(31, true) { DirectMinutes = 2000, AccessMinutes = 2, EgressMinutes = 3 };
            string expected = Fingerprint(f.Plan());
            int snapshots = f.Planner.RoadSnapshots;
            var job = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, f.Person, f.Person.Preferences));
            var contract = f.Native[10].allowedAgents;
            f.Native[10].allowedAgents = null;
            SlicedCheck(f.Planner.Refresh() == "", "gates changed unexpectedly");
            Time.frameCount++;
            SlicedCheck(!f.Planner.Prepare(), "the sweep did not notice the changed node");
            f.Native[10].allowedAgents = contract;
            f.Preflight();
            SlicedCheck(f.Planner.RoadSnapshots == snapshots + 1 && f.Planner.ReadyForPopulation, "no replacement snapshot");
            bool refused = false;
            try { job.Continue(0); }
            catch (RoutePlanningRestartRequired) { refused = true; }
            job.Dispose();
            SlicedCheck(refused, "a job begun before a snapshot replacement did not refuse its first slice");
            SlicedCheck(job.Result == null, "a refused job kept a result");
            var (fresh, units) = f.PlanSliced();
            SlicedCheck(Fingerprint(fresh) == expected, "a fresh job on the replacement snapshot answered a different route");
            sliced.Add(new { name = "snapshot-replacement-before-first-slice", fingerprint = Fingerprint(fresh), slices = units, nativeReads = 0L });
        }
        {
            // A resident without a car whose job allocated one at a home curb of the old snapshot. The replacement
            // snapshot no longer lets cars use that curb's node, so a retried plan that kept the car would drive
            // from a curb the new snapshot does not offer. The caller discards the working copy with the job; a
            // fresh job on a fresh copy must allocate against the new snapshot exactly as a one-shot plan does.
            var oldCurb = new Position(0, 0, 0); var newCurb = new Position(10, 0, 0);
            var f = new Fixture(31, true) { DirectMinutes = 2000, AccessMinutes = 2, EgressMinutes = 3, SecondHomeCurb = newCurb };
            CitizenState Resident() => new CitizenState { Identity = f.Person.Identity, HomeId = "home", PlaceId = "home",
                Phase = CitizenPhase.AtPlace, Preferences = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 100000 } };
            var before = Resident();
            var oldRoute = f.Planner.Plan(f.From, f.To, 100, before);
            SlicedCheck(before.Car != null && Same(before.Car.ParkedPosition, oldCurb), "the old snapshot did not allocate the nearest home curb");
            SlicedCheck(oldRoute != null && oldRoute.Legs.Any(l => l.Mode == TravelMode.Car), "the old snapshot did not drive");
            int snapshots = f.Planner.RoadSnapshots;
            var working = Resident();
            var job = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, working, working.Preferences));
            int guard = 0;
            while (working.Car == null) { SlicedCheck(!job.Continue(0) && ++guard < 100, "job finished before allocating a home car"); }
            SlicedCheck(f.Planner.LastJobUnit == "home-curb-walk" && Same(working.Car.ParkedPosition, oldCurb), "the suspended job did not allocate at the old curb");
            f.Native[0].allowedAgents = new List<int>();
            SlicedCheck(f.Planner.Refresh() == "", "gates changed unexpectedly");
            Time.frameCount++;
            SlicedCheck(!f.Planner.Prepare(), "the sweep did not notice the changed curb node");
            f.Preflight();
            SlicedCheck(f.Planner.RoadSnapshots == snapshots + 1 && f.Planner.ReadyForPopulation, "no replacement snapshot");
            bool refused = false;
            try { job.Continue(0); }
            catch (RoutePlanningRestartRequired) { refused = true; }
            job.Dispose();
            SlicedCheck(refused, "a job holding a car from the old snapshot did not ask for an event restart");
            // The caller drops the working copy (and its car). One-shot and fresh sliced plans on fresh copies.
            var expectedResident = Resident();
            var expectedRoute = f.Planner.Plan(f.From, f.To, 100, expectedResident);
            SlicedCheck(expectedResident.Car != null && Same(expectedResident.Car.ParkedPosition, newCurb), "the new snapshot did not allocate the remaining curb");
            SlicedCheck(expectedRoute != null && expectedRoute.Legs.Any(l => l.Mode == TravelMode.Car), "the new snapshot did not drive");
            SlicedCheck(Fingerprint(expectedRoute) != Fingerprint(oldRoute), "the replacement snapshot did not change the route");
            var retried = Resident();
            using var fresh = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, retried, retried.Preferences));
            int units = 1;
            while (!fresh.Continue(0)) units++;
            SlicedCheck(Fingerprint(fresh.Result) == Fingerprint(expectedRoute), "the replayed request answered a different route than the one-shot plan");
            SlicedCheck(retried.Car != null && Same(retried.Car.ParkedPosition, expectedResident.Car!.ParkedPosition),
                "the replayed request parked the car somewhere else than the one-shot plan");
            sliced.Add(new { name = "home-car-snapshot-restart", fingerprint = Fingerprint(fresh.Result), slices = units, nativeReads = 0L });
        }
        {
            // A job abandoned mid-search releases the shared scratch storage; the next plans are unaffected.
            var f = new Fixture(300, true) { DirectMinutes = 2000, AccessMinutes = 2, EgressMinutes = 3 };
            string expected = Fingerprint(f.Plan());
            var job = f.Planner.Begin(new RouteRequest(f.From, f.To, 100, f.Person, f.Person.Preferences));
            while (f.Planner.LastJobUnit != "car-search") SlicedCheck(!job.Continue(0), "job finished early");
            job.Dispose();
            SlicedCheck(job.Result == null, "abandoned job kept a result");
            SlicedCheck(Fingerprint(f.Plan()) == expected && Fingerprint(f.PlanSliced().Route) == expected, "plans after an abandoned job differ");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { checks, results, slicedChecks, sliced }));
#else
        Console.WriteLine(JsonSerializer.Serialize(new { checks, results }));
#endif
        if (args.Contains("--benchmark") || args.Contains("--benchmark-fixtures"))
        {
            foreach (var name in new[] { "unreachable-walk", "congested-walk", "winning-car" })
            {
                var f = new Fixture(name == "unreachable-walk" ? 1200 : 100, name != "unreachable-walk")
                { DirectMinutes = name == "unreachable-walk" ? 30 : 500, Delay = name == "congested-walk" ? 10000 : 0 };
                string expected = Fingerprint(f.Plan());
                if (!args.Contains("--benchmark"))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { benchmark = name, fingerprint = expected }));
                    continue;
                }
                for (int i = 0; i < 10; i++) Check(Fingerprint(f.Plan()) == expected, "warmup result changed");
                const int iterations = 200;
                var samples = new List<double>(); long reads = 0, allocated = 0;
                for (int pair = 0; pair < 5; pair++)
                {
                    long initialReads = StagingHarness.Guard.Reads, initialBytes = GC.GetAllocatedBytesForCurrentThread();
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < iterations; i++) f.Plan();
                    watch.Stop();
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                    reads = StagingHarness.Guard.Reads - initialReads;
                    allocated = GC.GetAllocatedBytesForCurrentThread() - initialBytes;
                    Check(Fingerprint(f.Plan()) == expected, "timed result changed");
                }
                samples.Sort();
                Console.WriteLine(JsonSerializer.Serialize(new { benchmark = name, fingerprint = expected, iterations,
                    medianMilliseconds = samples[2], nativeReads = reads, allocatedBytes = allocated }));
            }
        }
    }
}
