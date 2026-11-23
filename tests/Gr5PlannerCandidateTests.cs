using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5PlannerCandidateTests
{
    private readonly ITestOutputHelper output;
    public Gr5PlannerCandidateTests(ITestOutputHelper output) { this.output = output; }

    private static Position P(double x, double z = 0) => new Position(x, 0, z);

    private static Route Path(Random random, Position from, int legs, TravelMode mode, string service, bool shared, double jitter = 0, bool estimate = true)
    {
        var route = new Route();
        var at = from;
        for (int i = 0; i < legs; i++)
        {
            var next = new Position(at.X + 1 + random.NextDouble() * 4, 0, at.Z + random.NextDouble() - .5);
            route.Legs.Add(new RouteLeg { From = i == 0 ? new Position(at.X + jitter, 0, at.Z) : at, To = next, Minutes = .05 + random.NextDouble(), Mode = mode, ServiceId = service });
            at = next;
        }
        if (estimate && random.Next(4) == 0 && route.Legs.Count > 0) route.WithEstimate(route.Minutes + random.NextDouble() * 3);
        return shared ? TransportComposition.Shared(route) : route;
    }

    private static Position End(Route route, Position fallback) => route.Legs.Count == 0 ? fallback : route.Legs[route.Legs.Count - 1].To;

    private static (Route? Walk, Route? Train, Route? Back, Position From, Position To, TravelPreferences Preferences, Position? Parked) Case(Random random)
    {
        var from = P(random.NextDouble() * 10, random.NextDouble() * 10);
        var walk = Path(random, from, random.Next(0, 40), TravelMode.Walk, "", random.Next(3) != 0);
        int trainLegs = random.Next(1, 7);
        var train = Path(random, End(walk, from), trainLegs, random.Next(12) == 0 ? TravelMode.Car : TravelMode.Subway,
            random.Next(10) == 0 ? " " : "native:subway", random.Next(2) == 0, random.Next(10) == 0 ? .01 : 0);
        var back = Path(random, End(train, End(walk, from)), random.Next(0, 40), random.Next(15) == 0 ? TravelMode.Bus : TravelMode.Walk, "", random.Next(3) != 0,
            random.Next(12) == 0 ? .002 : 0);
        var to = End(back, End(train, End(walk, from)));
        if (random.Next(10) == 0) to = new Position(to.X + .01, 0, to.Z);
        var preferences = new TravelPreferences
        {
            OwnsCar = random.Next(2) == 0, AllowTransit = random.Next(8) != 0,
            WalkingToleranceMetres = random.Next(6) == 0 ? 5 : random.Next(30) == 0 ? double.NaN : 400,
        };
        Position? parked = random.Next(2) == 0 ? (Position?)End(walk, from) : null;
        if (random.Next(20) == 0) from = new Position(double.NaN, 0, 0);
        return (random.Next(25) == 0 ? null : walk, random.Next(25) == 0 ? null : train, back, from, to, preferences, parked);
    }

    [Fact]
    public void AJoinedCandidateScoresExactlyAsTheMaterializedJoinAndValidate()
    {
        var random = new Random(3701);
        int valid = 0, invalid = 0;
        for (int i = 0; i < 6000; i++)
        {
            var c = Case(random);
            var joined = TransportComposition.Join(c.Walk!, c.Train!, c.Back!);
            bool expected = joined != null && TransportComposition.Valid(joined, c.From, c.To, c.Preferences, c.Parked);
            bool actual = TransportComposition.TryJoinedScore(c.Walk, c.Train, c.Back, c.From, c.To, c.Preferences, c.Parked, out double score);
            Assert.Equal(expected, actual);
            if (!expected) { invalid++; continue; }
            valid++;
            Assert.Equal(BitConverter.DoubleToInt64Bits(joined!.EstimatedMinutes), BitConverter.DoubleToInt64Bits(score));
        }
        output.WriteLine("joined candidates: " + valid + " valid, " + invalid + " refused, every verdict and score bit-identical to Join + Valid");
        Assert.True(valid > 500 && invalid > 500, valid + " / " + invalid);
    }

    [Fact]
    public void TheFixedArityJoinsMatchTheArrayJoinLegForLeg()
    {
        var random = new Random(3702);
        for (int i = 0; i < 2000; i++)
        {
            var c = Case(random);
            var tail = Path(random, End(c.Back!, c.To), random.Next(0, 3), TravelMode.Walk, "", true);
            var last = Path(random, End(tail, End(c.Back!, c.To)), random.Next(0, 3), TravelMode.Walk, "", false);
            Same(TransportComposition.Join(new[] { c.Walk!, c.Train!, c.Back! }), TransportComposition.Join(c.Walk, c.Train, c.Back));
            Same(TransportComposition.Join(new[] { c.Walk!, c.Train!, c.Back!, tail, last }), TransportComposition.Join(c.Walk, c.Train, c.Back, tail, last));
        }
        static void Same(Route? expected, Route? actual)
        {
            Assert.Equal(expected == null, actual == null);
            if (expected == null) return;
            Assert.Equal(expected.Legs.Count, actual!.Legs.Count);
            Assert.Equal(expected.EstimatedMinutes, actual.EstimatedMinutes);
            for (int i = 0; i < expected.Legs.Count; i++)
            {
                var a = expected.Legs[i]; var b = actual.Legs[i];
                Assert.True(RouteLegContent(a, b));
                Assert.Equal(a.Frozen, b.Frozen);
            }
        }
    }

    private static bool RouteLegContent(RouteLeg a, RouteLeg b) => a.From.Equals(b.From) && a.To.Equals(b.To) && a.Minutes == b.Minutes
        && a.Mode == b.Mode && string.Equals(a.ServiceId, b.ServiceId, StringComparison.Ordinal);

    [Fact]
    public void CopyAndSingleMatchTheInitializerFormsTheyReplace()
    {
        var random = new Random(3703);
        var source = Path(random, P(0), 30, TravelMode.Walk, "", true);
        var before = new Route { Legs = new RouteLegs(source.Legs) };
        var after = TransportComposition.Copy(source);
        Assert.Equal(before.Legs.Count, after.Legs.Count);
        for (int i = 0; i < before.Legs.Count; i++) Assert.Same(before.Legs[i], after.Legs[i]);
        Assert.NotSame(source.Legs, after.Legs);
        Assert.Equal(before.EstimatedMinutes, after.EstimatedMinutes);
        Assert.False(after.IsCommitted);
        var leg = new RouteLeg { From = P(1), To = P(1), Mode = TravelMode.Car, Minutes = 2 };
        var single = TransportComposition.Shared(TransportComposition.Single(leg));
        Assert.Same(leg, single.Legs[0]);
        Assert.Single(single.Legs);
        Assert.Equal(2, single.EstimatedMinutes);
        Assert.Throws<ArgumentNullException>(() => TransportComposition.Copy(null!));
        Assert.Throws<ArgumentNullException>(() => TransportComposition.Single(null!));
    }

    private sealed class Job
    {
        private readonly bool after;
        private readonly Dictionary<int, Route> walks = new Dictionary<int, Route>();
        private readonly Dictionary<int, Route> trains = new Dictionary<int, Route>();
        private readonly Random shape = new Random(3704);
        public Job(bool after) { this.after = after; }

        private Route Cached(Dictionary<int, Route> cache, int key, Position from, int legs, TravelMode mode, string service)
        {
            if (!cache.TryGetValue(key, out var route)) cache.Add(key, route = Path(shape, from, legs, mode, service, true, estimate: false));
            return route;
        }

        private Route Walk(int key, Position from, int legs)
        {
            var cached = Cached(walks, key, from, legs, TravelMode.Walk, "");
            return after ? cached : new Route { Legs = new RouteLegs(cached.Legs) };
        }

        private Route Pause(Position at)
        {
            var leg = new RouteLeg { From = at, To = at, Mode = TravelMode.Car, Minutes = 2 };
            if (after) return TransportComposition.Shared(TransportComposition.Single(leg));
            var route = new Route { Legs = new RouteLegs(1) };
            route.Legs.Add(leg);
            return TransportComposition.Shared(route);
        }

        public Route? Plan(int origin, TravelPreferences preferences)
        {
            var from = P(origin * 1000);
            Route? best = null; double bestScore = double.PositiveInfinity; bool bestShared = false;
            void Consider(Route? route, Position to, bool shared = false)
            {
                if (route == null || !TransportComposition.Valid(route, from, to, preferences, from)) return;
                if (!TransportComposition.IsFaster(route.EstimatedMinutes, bestScore)) return;
                best = route; bestScore = route.EstimatedMinutes; bestShared = shared;
            }
            var direct = Walk(origin * 10, from, 60);
            var destination = End(direct, from);
            Consider(direct, destination, after);
            for (int a = 0; a < 2; a++)
            {
                var originWalk = Walk(origin * 10 + 1 + a, from, 12 + a);
                for (int b = 0; b < 2; b++)
                {
                    var train = Cached(trains, origin * 10 + a * 2 + b, End(originWalk, from), 4 + b, TravelMode.Subway, "native:subway");
                    var stationEnd = End(train, from);
                    var back = Walk(origin * 10 + 5 + a * 2 + b, stationEnd, 10 + b);
                    var to = End(back, stationEnd);
                    if (after)
                    {
                        if (TransportComposition.TryJoinedScore(originWalk, train, back, from, to, preferences, from, out double score)
                            && TransportComposition.IsFaster(score, bestScore))
                            Consider(TransportComposition.Join(originWalk, train, back), to);
                    }
                    else Consider(TransportComposition.Join(new[] { originWalk, train, back }), to);
                }
            }
            var access = Walk(origin * 10 + 9, from, 8);
            var parkedAt = End(access, from);
            var road = Path(shape, parkedAt, 20, TravelMode.Car, "", false, estimate: false);
            var egress = Walk(origin * 10 + 9 + 1000, End(road, parkedAt), 8);
            var carParts = after
                ? TransportComposition.Join(access, Pause(parkedAt), road, Pause(End(road, parkedAt)), egress)
                : TransportComposition.Join(new[] { access, Pause(parkedAt), road, Pause(End(road, parkedAt)), egress });
            if (carParts != null && TransportComposition.IsFaster(carParts.EstimatedMinutes, bestScore)) { best = carParts; bestScore = carParts.EstimatedMinutes; bestShared = false; }
            return best != null && bestShared ? TransportComposition.Copy(best) : best;
        }
    }

    private static long BytesPerJob(bool after, out List<Route?> results)
    {
        var job = new Job(after);
        var preferences = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 4000 };
        results = new List<Route?>();
        for (int i = 0; i < 50; i++) results.Add(job.Plan(i, preferences));
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int jobs = 500;
        for (int i = 0; i < jobs; i++) job.Plan(i % 50, preferences);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return bytes / jobs;
    }

    [Fact]
    public void APlanningJobBuildsOnlyTheCandidatesItKeepsAndChoosesTheSameRoute()
    {
        long before = BytesPerJob(after: false, out var expected), after = BytesPerJob(after: true, out var actual);
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i] == null, actual[i] == null);
            if (expected[i] == null) continue;
            Assert.Equal(expected[i]!.EstimatedMinutes, actual[i]!.EstimatedMinutes);
            Assert.Equal(expected[i]!.Legs.Count, actual[i]!.Legs.Count);
            for (int leg = 0; leg < expected[i]!.Legs.Count; leg++) Assert.True(RouteLegContent(expected[i]!.Legs[leg], actual[i]!.Legs[leg]));
        }
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "planning job (1 direct walk of 60 legs, 2x2 subway candidates, 1 car candidate with 2 pauses, cached walks): before {0:N0} B/job, after {1:N0} B/job (.NET)",
            before, after));
        Assert.True(after * 3 <= before * 2, before + " -> " + after);
    }

    [Fact]
    public void ScoringACandidateAllocatesNothing()
    {
        var random = new Random(3705);
        var walk = Path(random, P(0), 30, TravelMode.Walk, "", true);
        var train = Path(random, End(walk, P(0)), 5, TravelMode.Subway, "native:subway", true);
        var back = Path(random, End(train, P(0)), 30, TravelMode.Walk, "", true);
        var preferences = new TravelPreferences { AllowTransit = true, WalkingToleranceMetres = 4000 };
        var to = End(back, P(0));
        Assert.True(TransportComposition.TryJoinedScore(walk, train, back, P(0), to, preferences, null, out _));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) TransportComposition.TryJoinedScore(walk, train, back, P(0), to, preferences, null, out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void UnitSeamsHoldTheirBudgetCapFromTheStartAndNeverGrow()
    {
        var seams = new OptionalStopUnitSeams();
        var field = typeof(OptionalStopUnitSeams).GetField("seams", BindingFlags.NonPublic | BindingFlags.Instance)!;
        int initial = ((double[])field.GetValue(seams)!).Length;
        Assert.Equal(PopulationFrameBudget.MaximumUnits + 1, OptionalStopUnitSeams.PresizedUnits);
        Assert.Equal(OptionalStopUnitSeams.PresizedUnits, initial);
        for (int warm = 0; warm < 2; warm++) { seams.BeginCall(); for (int i = 0; i < 4; i++) seams.EndUnit("warm", .1); seams.EndCall(true); seams.P95UnitSeamMilliseconds(); }
        var calls = new[] { 128, 461, 753, PopulationFrameBudget.MaximumUnits + 1 };
        var recorded = new int[calls.Length];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int call = 0; call < calls.Length; call++)
        {
            seams.BeginCall();
            for (int i = 0; i < calls[call]; i++) { seams.Seam(.001 * i); seams.EndUnit(i % 2 == 0 ? "route-planning" : "snapshot", .01); }
            seams.EndCall(true);
            seams.P95UnitSeamMilliseconds();
            recorded[call] = seams.Units;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(calls, recorded);
        Assert.Equal(0, allocated);
        Assert.Same(field.GetValue(seams), field.GetValue(seams));
        Assert.Equal(initial, ((double[])field.GetValue(seams)!).Length);
        Assert.Equal(PopulationFrameBudget.MaximumUnits, new PopulationFrameBudget(5, false).Units);
    }

    [Fact]
    public void MappingValidationKeepsOneOffsetsBufferForJourneysUpToTheLegCap()
    {
        const string topology = "offsets-native";
        static TrafficNetworkNode Node(int id, Position position, params int[] next) => new()
        { Id = id, Position = position, AllowsCar = true, KilometresPerHour = 36, Successors = next };
        var network = new TrafficNetwork(topology, new[] { Node(0, P(0), 1), Node(1, P(10), 2), Node(2, P(20)) }, 1);
        Route Journey(int walkLegs)
        {
            var legs = new List<RouteLeg>();
            for (int i = 0; i < walkLegs; i++) legs.Add(new RouteLeg { From = P(i - walkLegs), To = P(i + 1 - walkLegs), Minutes = .01, Mode = TravelMode.Walk });
            legs.Add(new RouteLeg { From = P(0), To = P(10), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + topology + ":0:1" });
            legs.Add(new RouteLeg { From = P(10), To = P(20), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + topology + ":1:2" });
            return new Route { Legs = legs };
        }
        var field = typeof(TrafficNetwork).GetField("validationOffsets", BindingFlags.NonPublic | BindingFlags.Static)!;
        var lengths = new List<int>();
        long allocated = -1;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (int walkLegs in new[] { 1, 60, 255, 300, TransportComposition.MaximumLegs - 2 })
                {
                    var journey = Journey(walkLegs);
                    Assert.True(network.TryMap(journey, out var mapped));
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    network.ValidateMapping(journey, mapped);
                    if (walkLegs > 1) allocated = Math.Max(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
                    lengths.Add(((double[])field.GetValue(null)!).Length);
                }
            }
            catch (Exception error) { failure = error; }
        });
        thread.Start(); thread.Join();
        Assert.Null(failure);
        output.WriteLine("validation offsets length per journey: " + string.Join(", ", lengths) + "; bytes per validation after the first: " + allocated);
        Assert.All(lengths, length => Assert.Equal(TransportComposition.MaximumLegs + 1, length));
        Assert.Equal(0, allocated);
    }
}
