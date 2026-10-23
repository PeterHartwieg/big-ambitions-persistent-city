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
    }

    private static void Main(string[] args)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var results = new List<object>();
        void Record(string name, Fixture f, TravelMode? mode)
        {
            long reads = StagingHarness.Guard.Reads;
            var route = f.Plan(); reads = StagingHarness.Guard.Reads - reads;
            if (mode.HasValue) Check(route != null && route.Legs.Any(l => l.Mode == mode.Value), name + " wrong mode");
            else Check(route == null, name + " expected refusal");
            if (route != null) Check(TransportComposition.Valid(route, f.From.Position, f.To.Position, f.Person.Preferences, f.Parked), name + " invalid result");
            results.Add(new { name, fingerprint = Fingerprint(route), minutes = route?.Minutes, estimate = route?.EstimatedMinutes, nativeReads = reads });
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
        Console.WriteLine(JsonSerializer.Serialize(new { checks, results }));
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
