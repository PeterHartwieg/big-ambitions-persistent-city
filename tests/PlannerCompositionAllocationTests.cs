using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class PlannerCompositionAllocationTests
{
    private readonly ITestOutputHelper output;
    public PlannerCompositionAllocationTests(ITestOutputHelper output) { this.output = output; }

    private const int WalkLegs = 8, TrainLegs = 5;
    private static readonly string Topology = new string('a', 64);
    private static Position P(double x) => new Position(x, 0, 0);

    private sealed class Planner
    {
        private readonly bool shared;
        private readonly Dictionary<int, Route> walks = new Dictionary<int, Route>();
        private readonly Dictionary<long, string> tags = new Dictionary<long, string>();
        public Planner(bool shared) { this.shared = shared; }

        private static Route Fresh(double from, int legs, TravelMode mode, string service = "")
        {
            var route = new Route();
            for (int i = 0; i < legs; i++)
                route.Legs.Add(new RouteLeg { From = P(from + i), To = P(from + i + 1), Minutes = 0.1, Mode = mode, ServiceId = service });
            return route;
        }

        public Route Walk(double from)
        {
            int key = (int)from;
            if (!walks.TryGetValue(key, out var cached))
            {
                cached = Fresh(from, WalkLegs, TravelMode.Walk);
                if (shared) TransportComposition.Shared(cached);
                walks.Add(key, cached);
            }
            if (shared) return new Route { Legs = new RouteLegs(cached.Legs) };
            var copy = new Route();
            foreach (var leg in cached.Legs) copy.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = TravelMode.Walk });
            return copy;
        }

        private Route Pause(double at)
        {
            if (!shared) return new Route { Legs = new List<RouteLeg> { new RouteLeg { From = P(at), To = P(at), Mode = TravelMode.Car, Minutes = 2 } } };
            var route = new Route { Legs = new RouteLegs(1) };
            route.Legs.Add(new RouteLeg { From = P(at), To = P(at), Mode = TravelMode.Car, Minutes = 2 });
            return TransportComposition.Shared(route);
        }

        private string Tag(int from, int to)
        {
            if (!shared)
            {
                string prefix = "native:road:v1:" + Topology + ":";
                return string.Concat(prefix, from.ToString(CultureInfo.InvariantCulture), ":", to.ToString(CultureInfo.InvariantCulture));
            }
            long key = (long)from << 32 | (uint)to;
            if (tags.TryGetValue(key, out var tag)) return tag;
            tag = string.Concat("native:road:v1:", Topology, ":", from.ToString(CultureInfo.InvariantCulture) + ":" + to.ToString(CultureInfo.InvariantCulture));
            tags.Add(key, tag);
            return tag;
        }

        private Route Road(double from, int legs)
        {
            var road = shared ? new Route { Legs = new RouteLegs(legs) } : new Route();
            for (int i = 0; i < legs; i++)
                road.Legs.Add(new RouteLeg { From = P(from + i), To = P(from + i + 1), Minutes = 0.05, Mode = TravelMode.Car });
            for (int i = 0; i < legs; i++) road.Legs[i].ServiceId = Tag((int)from + i, (int)from + i + 1);
            var begin = Fresh(from, 0, TravelMode.Car);
            var end = Fresh(from + legs, 0, TravelMode.Car);
            return shared
                ? TransportComposition.Join(TransportComposition.Shared(begin), TransportComposition.Shared(road), TransportComposition.Shared(end))!
                : CopyJoin(begin, road, end);
        }

        private Route Train(double from)
        {
            var route = Fresh(from, TrainLegs, TravelMode.Subway, "native:subway");
            return shared ? TransportComposition.Shared(route) : route;
        }

        private Route Join(params Route[] parts) => shared ? TransportComposition.Join(parts)! : CopyJoin(parts);

        public int Plan(int origin, int roadLegs, TravelPreferences preferences)
        {
            double a = origin * 1000;
            int valid = 0;
            var direct = Walk(a);
            if (Valid(direct, preferences)) valid++;
            var originWalk = Walk(a + 100); var destinationWalk = Walk(a + 100 + WalkLegs + TrainLegs);
            for (int pair = 0; pair < 2; pair++)
                if (Valid(Join(originWalk, Train(a + 100 + WalkLegs), destinationWalk), preferences)) valid++;
            var access = Walk(a + 200);
            for (int search = 0; search < 2; search++)
            {
                double driveFrom = a + 200 + WalkLegs;
                var road = Road(driveFrom, roadLegs);
                var egress = Walk(driveFrom + roadLegs);
                if (Valid(Join(access, Pause(driveFrom), road, Pause(driveFrom + roadLegs), egress), preferences)) valid++;
            }
            return valid;
        }

        private bool Valid(Route route, TravelPreferences preferences)
        {
            if (!shared) foreach (var leg in route.Legs) if (!Enum.IsDefined(typeof(TravelMode), leg.Mode)) return false;
            return route.Legs.Count > 0 && preferences.WalkingToleranceMetres >= 0;
        }

        private static Route CopyJoin(params Route[] parts)
        {
            var result = new Route();
            foreach (var part in parts)
                foreach (var leg in part.Legs)
                {
                    if (!Enum.IsDefined(typeof(TravelMode), leg.Mode)) throw new InvalidOperationException();
                    result.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = leg.Mode, ServiceId = leg.ServiceId });
                }
            return result;
        }
    }

    private static long BytesPerPlan(bool shared, int roadLegs, int origins = 40, int plans = 400)
    {
        var planner = new Planner(shared);
        var preferences = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 2000 };
        for (int i = 0; i < origins; i++) planner.Plan(i, roadLegs, preferences);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int valid = 0;
        for (int i = 0; i < plans; i++) valid += planner.Plan(i % origins, roadLegs, preferences);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(plans * 5, valid);
        return bytes / plans;
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void ACarPlanStopsCopyingLegsAndBuildingTagsAfterWarmUp(int roadLegs)
    {
        long before = BytesPerPlan(shared: false, roadLegs), after = BytesPerPlan(shared: true, roadLegs);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0} road legs per search, 2 searches + 2 subway + 1 walk per plan: before {1:N0} B/plan, after {2:N0} B/plan ({3:0.0}x less)",
            roadLegs, before, after, (double)before / after));
        Assert.True(after * 3 <= before, before + " -> " + after);
    }

    [Fact]
    public void JoinSharesFrozenLegsAndCopiesCallerOwnedOnes()
    {
        var frozen = new Route();
        frozen.Legs.Add(new RouteLeg { From = P(0), To = P(1), Minutes = 1, Mode = TravelMode.Walk });
        TransportComposition.Shared(frozen);
        var owned = new Route();
        owned.Legs.Add(new RouteLeg { From = P(1), To = P(2), Minutes = 1, Mode = TravelMode.Car, ServiceId = "x" });
        var joined = TransportComposition.Join(frozen, owned)!;
        Assert.Same(frozen.Legs[0], joined.Legs[0]);
        Assert.NotSame(owned.Legs[0], joined.Legs[0 + 1]);
        Assert.Equal("x", joined.Legs[1].ServiceId);
        Assert.Throws<InvalidOperationException>(() => joined.Legs[0].To = P(5));
        joined.Legs[1].To = P(2);
        Assert.Equal(2, joined.Legs.Count);
        Assert.Throws<ArgumentNullException>(() => TransportComposition.Shared(null!));
        var invalid = new Route();
        invalid.Legs.Add(new RouteLeg { From = P(0), To = P(1), Minutes = 1, Mode = (TravelMode)9 });
        Assert.Null(TransportComposition.Join(invalid));
        Assert.False(TransportComposition.Valid(invalid, P(0), P(1), new TravelPreferences { WalkingToleranceMetres = 10 }));
    }

    private sealed class SearchModel
    {
        private const int Side = 12;
        private readonly TransportRoadGraph graph;
        private readonly Dictionary<int, double> captured = new Dictionary<int, double>();
        private readonly Dictionary<int, double> pace = new Dictionary<int, double>();
        private readonly Func<int, int, double, double> traversal;
        private readonly Func<int, int, double> extra;
        private readonly Action<int[]> found;
        private Func<int, bool>? disabled;
        private Func<Position, Position, bool>? clear;
        private int[]? path;
        private double entry;

        public SearchModel()
        {
            var nodes = new TransportRoadNode[Side * Side];
            for (int x = 0; x < Side; x++)
                for (int z = 0; z < Side; z++)
                {
                    var next = new List<int>();
                    if (x > 0) next.Add((x - 1) * Side + z); if (x + 1 < Side) next.Add((x + 1) * Side + z);
                    if (z > 0) next.Add(x * Side + z - 1); if (z + 1 < Side) next.Add(x * Side + z + 1);
                    nodes[x * Side + z] = new TransportRoadNode { Position = new Position(x * 30, 0, z * 30), Successors = next.ToArray(), KilometresPerHour = 30, AllowsCar = true };
                }
            graph = new TransportRoadGraph(nodes);
            traversal = Traversal; extra = Extra; found = p => path = p;
        }

        private bool Disabled(int node) => false;
        private bool Clear(Position a, Position b) => true;
        private double Traversal(int a, int b, double baseline)
        {
            if (!pace.TryGetValue(a, out double factor)) { factor = 1 + a % 3 * 0.1; pace.Add(a, factor); }
            return baseline * factor;
        }
        private double Extra(int a, int b)
        {
            if (!captured.TryGetValue(b, out double value)) { value = entry * 0 + b % 5 * 0.01; captured.Add(b, value); }
            return value;
        }

        public int Before(int start, int goal)
        {
            captured.Clear(); pace.Clear(); path = null;
            var captured2 = captured; var pace2 = pace; double entryMinute = 3;
            double LocalTraversal(int a, int b, double baseline)
            {
                if (!pace2.TryGetValue(a, out double factor)) { factor = 1 + a % 3 * 0.1; pace2.Add(a, factor); }
                return baseline * factor;
            }
            double LocalExtra(int a, int b)
            {
                if (!captured2.TryGetValue(b, out double value)) { value = entryMinute * 0 + b % 5 * 0.01; captured2.Add(b, value); }
                return value;
            }
            using var search = graph.BeginSearch(start, goal, Disabled, Clear, LocalTraversal, pathFound: p => path = p, extraMinutes: LocalExtra);
            search.Advance(int.MaxValue);
            return search.Result!.Legs.Count + path!.Length;
        }

        public int After(int start, int goal)
        {
            captured.Clear(); pace.Clear(); path = null; entry = 3;
            using var search = graph.BeginSearch(start, goal, disabled ??= Disabled, clear ??= Clear, traversal, pathFound: found, extraMinutes: extra);
            search.Advance(int.MaxValue);
            return search.Result!.Legs.Count + path!.Length;
        }
    }

    [Fact]
    public void ARoadPlanReusesItsSearchDelegatesAndClosure()
    {
        var model = new SearchModel();
        long Measure(Func<int, int, int> plan)
        {
            for (int i = 0; i < 20; i++) plan(i, 143 - i);
            int legs = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++) legs += plan(i % 20, 143 - i % 20);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(legs > 0);
            return bytes / 400;
        }
        long closures = Measure(model.Before), cached = Measure(model.After);
        Assert.Equal(model.Before(3, 140), model.After(3, 140));
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "road search with traversal and forecast callbacks: before {0:N0} B/search, after {1:N0} B/search ({2:N0} B of closure and delegates removed)",
            closures, cached, closures - cached));
        Assert.True(closures - cached >= 200, closures + " -> " + cached);
    }

    private static Route? Train(Position from, Position platform, Position toPlatform, Position to)
    {
        var points = new List<Position> { from, platform, toPlatform, to };
        var route = new Route();
        route.Legs.Add(new RouteLeg { From = points[0], To = points[0], Mode = TravelMode.Subway, Minutes = 3, ServiceId = "native:subway" });
        for (int i = 1; i < points.Count; i++)
            route.Legs.Add(new RouteLeg { From = points[i - 1], To = points[i], Mode = TravelMode.Subway, Minutes = Position.Distance(points[i - 1], points[i]) / 10, ServiceId = "native:subway" });
        return TransportComposition.Shared(route);
    }

    [Fact]
    public void ASubwayTrainIsBuiltOncePerStationPair()
    {
        var walk = TransportComposition.Shared(new Route { Legs = new RouteLegs(1) { new RouteLeg { From = P(0), To = P(1), Minutes = 1, Mode = TravelMode.Walk } } });
        var egress = TransportComposition.Shared(new Route { Legs = new RouteLegs(1) { new RouteLeg { From = P(40), To = P(41), Minutes = 1, Mode = TravelMode.Walk } } });
        var trains = new Dictionary<(int, int), Route?>();
        Route? Cached(int a, int b)
        {
            if (!trains.TryGetValue((a, b), out var route)) { route = Train(P(1), P(2), P(39), P(40)); trains.Add((a, b), route); }
            return route;
        }
        long Measure(Func<int, int, Route?> train)
        {
            for (int i = 0; i < 20; i++) train(i % 6, (i + 1) % 6);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++)
                for (int pair = 0; pair < 4; pair++)
                    Assert.NotNull(TransportComposition.Join(walk, train(i % 6, (i + pair + 1) % 6)!, egress));
            return (GC.GetAllocatedBytesForCurrentThread() - before) / 400;
        }
        long fresh = Measure((a, b) => Train(P(1), P(2), P(39), P(40))), reused = Measure(Cached);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "4 subway candidates per plan: before {0:N0} B/plan, after {1:N0} B/plan ({2:0.0}x less)", fresh, reused, (double)fresh / reused));
        Assert.True(reused * 2 <= fresh, fresh + " -> " + reused);
    }
}
