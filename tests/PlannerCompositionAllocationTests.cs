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
}
