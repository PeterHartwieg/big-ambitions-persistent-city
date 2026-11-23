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

    private enum Winner { Walk, Subway, Car }

    private sealed class Job
    {
        private readonly bool after;
        private readonly RouteCandidates candidates = new RouteCandidates();
        private readonly Dictionary<(Position, Position), Route> walks = new Dictionary<(Position, Position), Route>();
        private readonly Dictionary<(Position, Position), Route> trains = new Dictionary<(Position, Position), Route>();
        public Job(bool after) { this.after = after; }

        internal static Position From(int origin) => P(origin * 1000, origin % 7);
        internal static Position To(int origin) => P(origin * 1000 + 300, origin % 7);
        internal static Winner Expected(int origin) => (Winner)(origin % 3);
        private static double WalkPace(int origin) => Expected(origin) == Winner.Walk ? .01 : .05;
        private static double TrainMinutes(int origin) => Expected(origin) == Winner.Subway ? .5 : 30;
        private static double RoadMinutes(int origin) => Expected(origin) == Winner.Car ? .2 : 30;

        private static Route Line(Position from, Position to, int legs, TravelMode mode, string service, double totalMinutes)
        {
            var route = new Route();
            var at = from;
            for (int i = 1; i <= legs; i++)
            {
                double f = (double)i / legs;
                var next = i == legs ? to : new Position(from.X + (to.X - from.X) * f, 0, from.Z + (to.Z - from.Z) * f + (i % 2 == 0 ? .5 : -.5));
                route.Legs.Add(new RouteLeg { From = at, To = next, Minutes = totalMinutes / legs, Mode = mode, ServiceId = service });
                at = next;
            }
            return route;
        }

        public Route CachedWalk(int origin, Position from, Position to, int legs)
        {
            if (!walks.TryGetValue((from, to), out var route))
                walks.Add((from, to), route = TransportComposition.Shared(Line(from, to, legs, TravelMode.Walk, "", Position.Distance(from, to) * WalkPace(origin))));
            return route;
        }

        private Route Walk(int origin, Position from, Position to, int legs)
        {
            var cached = CachedWalk(origin, from, to, legs);
            return after ? cached : new Route { Legs = new RouteLegs(cached.Legs) };
        }

        private Route Train(int origin, Position from, Position to)
        {
            if (!trains.TryGetValue((from, to), out var route))
                trains.Add((from, to), route = TransportComposition.Shared(Line(from, to, 4, TravelMode.Subway, "native:subway", TrainMinutes(origin))));
            return route;
        }

        private Route Pause(Position at)
        {
            var leg = new RouteLeg { From = at, To = at, Mode = TravelMode.Car, Minutes = 2 };
            if (after) return TransportComposition.Shared(TransportComposition.Single(leg));
            var route = new Route { Legs = new RouteLegs(1) };
            route.Legs.Add(leg);
            return TransportComposition.Shared(route);
        }

        internal static Position Parked(int origin) => P(origin * 1000, -50);
        private static Position RoadEnd(int origin) => P(origin * 1000 + 300, -50);
        private static Position Station(int origin, bool start, int index) => P(origin * 1000 + (start ? 40 : 260) + index * 5, 30 + index * 3);
        internal static TravelPreferences Preferences { get; } = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 4000 };

        public Route? Plan(int origin)
        {
            var from = From(origin); var to = To(origin); var parked = Parked(origin); var preferences = Preferences;
            Route? best = null; double bestScore = double.PositiveInfinity;
            if (after) candidates.Reset();
            void Consider(Route? route, bool shared = false)
            {
                if (after) { candidates.Consider(route, from, to, preferences, parked, shared); return; }
                if (route == null || !TransportComposition.Valid(route, from, to, preferences, parked)) return;
                if (!TransportComposition.IsFaster(route.EstimatedMinutes, bestScore)) return;
                best = route; bestScore = route.EstimatedMinutes;
            }
            Consider(Walk(origin, from, to, 60), shared: true);
            for (int a = 0; a < 2; a++)
            {
                var originWalk = Walk(origin, from, Station(origin, true, a), 12 + a);
                for (int b = 0; b < 2; b++)
                {
                    var stationEnd = Station(origin, false, b);
                    var train = Train(origin, Station(origin, true, a), stationEnd);
                    var back = Walk(origin, stationEnd, to, 10 + b);
                    if (after) candidates.ConsiderJoined(originWalk, train, back, from, to, preferences, parked);
                    else Consider(TransportComposition.Join(new[] { originWalk, train, back }));
                }
            }
            var access = Walk(origin, from, parked, 8);
            var road = Line(parked, RoadEnd(origin), 20, TravelMode.Car, "", RoadMinutes(origin));
            var egress = Walk(origin, RoadEnd(origin), to, 8);
            Consider(after
                ? TransportComposition.Join(access, Pause(parked), road, Pause(RoadEnd(origin)), egress)
                : TransportComposition.Join(new[] { access, Pause(parked), road, Pause(RoadEnd(origin)), egress }));
            return after ? candidates.Publish() : best;
        }
    }

    private static Winner Mode(Route route)
    {
        foreach (var leg in route.Legs) if (leg.Mode == TravelMode.Car) return Winner.Car;
        foreach (var leg in route.Legs) if (leg.Mode == TravelMode.Subway) return Winner.Subway;
        return Winner.Walk;
    }

    private static long BytesPerJob(bool after, out List<Route?> results)
    {
        var job = new Job(after);
        results = new List<Route?>();
        for (int i = 0; i < 48; i++) results.Add(job.Plan(i));
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int jobs = 480;
        for (int i = 0; i < jobs; i++) job.Plan(i % 48);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return bytes / jobs;
    }

    [Fact]
    public void APlanningJobBuildsOnlyTheCandidatesItKeepsAndChoosesTheSameRoute()
    {
        long before = BytesPerJob(after: false, out var expected), after = BytesPerJob(after: true, out var actual);
        Assert.Equal(expected.Count, actual.Count);
        var winners = new int[3];
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.NotNull(expected[i]); Assert.NotNull(actual[i]);
            Assert.Equal(Job.Expected(i), Mode(expected[i]!));
            Assert.Equal(Job.Expected(i), Mode(actual[i]!));
            winners[(int)Mode(actual[i]!)]++;
            Assert.Equal(expected[i]!.EstimatedMinutes, actual[i]!.EstimatedMinutes);
            Assert.Equal(expected[i]!.Legs.Count, actual[i]!.Legs.Count);
            for (int leg = 0; leg < expected[i]!.Legs.Count; leg++) Assert.True(RouteLegContent(expected[i]!.Legs[leg], actual[i]!.Legs[leg]));
        }
        Assert.All(winners, count => Assert.Equal(16, count));
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "warmed planning job, one destination (60-leg direct walk, 2x2 valid subway candidates, 1 valid car candidate with 2 pauses and a fresh 20-leg road, cached walks; 16 walk / 16 subway / 16 car winners): before {0:N0} B/job, after {1:N0} B/job (.NET)",
            before, after));
        Assert.True(after * 3 <= before * 2, before + " -> " + after);
    }

    [Fact]
    public void APublishedRouteNeverSharesTheCachedWalkingLegList()
    {
        var job = new Job(after: true);
        const int walkOrigin = 0, subwayOrigin = 1;
        Assert.Equal(Winner.Walk, Job.Expected(walkOrigin));
        Assert.Equal(Winner.Subway, Job.Expected(subwayOrigin));
        var cached = job.CachedWalk(walkOrigin, Job.From(walkOrigin), Job.To(walkOrigin), 60);
        var original = cached.Legs.ToArray();
        var first = job.Plan(walkOrigin)!;
        Assert.Equal(Winner.Walk, Mode(first));
        Assert.NotSame(cached, first);
        Assert.NotSame(cached.Legs, first.Legs);
        first.Legs.RemoveAt(first.Legs.Count - 1);
        first.Legs.Add(new RouteLeg { From = P(-1), To = P(-2), Minutes = 1, Mode = TravelMode.Walk });
        first.Legs.Insert(0, new RouteLeg { From = P(-3), To = P(-1), Minutes = 1, Mode = TravelMode.Walk });
        Assert.Same(cached, job.CachedWalk(walkOrigin, Job.From(walkOrigin), Job.To(walkOrigin), 60));
        var again = job.Plan(walkOrigin)!;
        Assert.Equal(original.Length, cached.Legs.Count);
        Assert.Equal(original.Length, again.Legs.Count);
        for (int i = 0; i < original.Length; i++) { Assert.Same(original[i], cached.Legs[i]); Assert.Same(original[i], again.Legs[i]); }

        var joinedCached = job.CachedWalk(subwayOrigin, Job.From(subwayOrigin), Job.To(subwayOrigin), 60);
        var joinedOriginal = joinedCached.Legs.ToArray();
        var joined = job.Plan(subwayOrigin)!;
        Assert.Equal(Winner.Subway, Mode(joined));
        int joinedCount = joined.Legs.Count;
        joined.Legs.Clear();
        var rejoined = job.Plan(subwayOrigin)!;
        Assert.Equal(joinedCount, rejoined.Legs.Count);
        Assert.Equal(joinedOriginal.Length, joinedCached.Legs.Count);
        var repeat = job.Plan(walkOrigin)!;
        Assert.Equal(original.Length, repeat.Legs.Count);
        Assert.NotSame(again.Legs, repeat.Legs);
    }

    [Fact]
    public void TheCandidateChoicePublishesACopyOnlyWhenTheSharedRouteWins()
    {
        var preferences = Job.Preferences;
        var from = P(0); var to = P(10);
        Route Straight(double minutes) => TransportComposition.Shared(new Route { Legs = new List<RouteLeg> { new RouteLeg { From = from, To = to, Minutes = minutes, Mode = TravelMode.Walk } } });
        var choice = new RouteCandidates();
        var slow = Straight(5); var fast = Straight(3);
        choice.Consider(slow, from, to, preferences, null, shared: true);
        choice.Consider(fast, from, to, preferences, null);
        Assert.Same(fast, choice.Publish());
        choice.Reset();
        Assert.False(choice.HasBest);
        Assert.Null(choice.Publish());
        choice.Consider(fast, from, to, preferences, null);
        choice.Consider(slow, from, to, preferences, null, shared: true);
        Assert.Same(fast, choice.Publish());
        choice.Reset();
        choice.Consider(fast, from, to, preferences, null, shared: true);
        choice.Consider(slow, from, to, preferences, null);
        var copy = choice.Publish()!;
        Assert.NotSame(fast, copy);
        Assert.NotSame(fast.Legs, copy.Legs);
        Assert.Same(fast.Legs[0], copy.Legs[0]);
        Assert.Same(copy, choice.Publish());
        choice.Reset();
        choice.Consider(Straight(1), from, P(11), preferences, null, shared: true);
        Assert.False(choice.HasBest);
        Assert.Equal(double.PositiveInfinity, choice.BestScore);
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
