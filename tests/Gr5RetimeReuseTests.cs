using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5RetimeReuseTests
{
    private const string Native = "gr5-retime-native";

    internal static List<TrafficNetworkNode> Grid(int side, double clockRate, int sub = 4)
    {
        var nodes = new List<TrafficNetworkNode>();
        int Id(int x, int z, int k, int dir) => ((x * side + z) * 2 + dir) * sub + k;
        for (int x = 0; x < side; x++)
            for (int z = 0; z < side; z++)
                for (int dir = 0; dir < 2; dir++)
                    for (int k = 0; k < sub; k++)
                    {
                        var next = new List<int>();
                        if (k + 1 < sub) next.Add(Id(x, z, k + 1, dir));
                        else { if (x + 1 < side) next.Add(Id(x + 1, z, 0, 0)); if (z + 1 < side) next.Add(Id(x, z + 1, 0, 1)); }
                        bool light = k == sub - 1 && (x + z) % 3 == 0;
                        double green = 10 + (x % 3), yellow = 3, slot = green * clockRate + yellow * clockRate;
                        nodes.Add(new TrafficNetworkNode
                        {
                            Id = Id(x, z, k, dir),
                            Position = new Position(x * 40 + (dir == 0 ? k * 9.7 : 0), (x + z) % 2 * 0.1, z * 40 + (dir == 1 ? k * 9.7 : 0)),
                            KilometresPerHour = 20 + (x * 7 + z * 3 + k) % 4 * 10, AllowsCar = (x + z + k) % 11 != 0, Boundary = k == 0,
                            StructuralContract = "road=" + (x * side + z) + ";dir=" + dir + (k == 2 && x % 2 == 0 ? ";giveWay" : ""),
                            Successors = next.ToArray(),
                            Signal = light ? new TrafficSignalSchedule { CycleMinutes = 2 * slot, GreenStartMinutes = dir * slot, GreenDurationMinutes = green * clockRate } : null,
                        });
                    }
        return nodes;
    }

    private static void AssertSame(TrafficNetwork expected, TrafficNetwork actual, IReadOnlyList<TrafficNetworkNode> nodes)
    {
        Assert.Equal(expected.TopologyId, actual.TopologyId);
        Assert.Equal(expected.NativeTopologyId, actual.NativeTopologyId);
        Assert.Equal(expected.Links.Count, actual.Links.Count);
        for (int i = 0; i < expected.Links.Count; i++)
        {
            var a = expected.Links[i]; var b = actual.Links[i];
            Assert.Equal(a.Id, b.Id); Assert.Equal(a.Capacity, b.Capacity);
            Assert.Equal(a.FreeFlowMinutes, b.FreeFlowMinutes); Assert.Equal(a.HeadwayMinutes, b.HeadwayMinutes);
            Assert.Equal(a.Signal == null, b.Signal == null);
            if (a.Signal != null)
            {
                Assert.NotSame(a.Signal, b.Signal);
                Assert.Equal(a.Signal.CycleMinutes, b.Signal!.CycleMinutes); Assert.Equal(a.Signal.GreenStartMinutes, b.Signal.GreenStartMinutes);
                Assert.Equal(a.Signal.GreenDurationMinutes, b.Signal.GreenDurationMinutes); Assert.Equal(a.Signal.PhaseOffsetMinutes, b.Signal.PhaseOffsetMinutes);
            }
            Assert.Equal(expected.NativeNodes(i), actual.NativeNodes(i));
        }
        foreach (var node in nodes)
        {
            Assert.Equal(expected.NodePosition(node.Id), actual.NodePosition(node.Id));
            foreach (int next in node.Successors)
            {
                bool had = expected.TryGetEdge(node.Id, next, out var a);
                Assert.Equal(had, actual.TryGetEdge(node.Id, next, out var b));
                if (!had) continue;
                Assert.Equal(a.LinkId, b.LinkId); Assert.Equal(a.FreeFlowMinutes, b.FreeFlowMinutes);
                Assert.Equal(a.StartLinkFraction, b.StartLinkFraction); Assert.Equal(a.EndLinkFraction, b.EndLinkFraction);
            }
        }
    }

    private static Route Drive(TrafficNetwork network, int link, double clockRate)
    {
        var indices = network.NativeNodes(link);
        var route = new Route();
        for (int i = 1; i < indices.Count; i++)
        {
            network.TryGetEdge(indices[i - 1], indices[i], out var edge);
            route.Legs.Add(new RouteLeg { From = network.NodePosition(indices[i - 1]), To = network.NodePosition(indices[i]), Mode = TravelMode.Car,
                Minutes = edge.FreeFlowMinutes, ServiceId = "native:road:v1:" + Native + ":" + indices[i - 1] + ":" + indices[i] });
        }
        return route;
    }

    [Theory]
    [InlineData(1.0, 1.0 / 3)]
    [InlineData(1.0 / 3, 20.0)]
    [InlineData(20.0, 1.0)]
    [InlineData(0.5, 0.75)]
    public void ARetimedNetworkMatchesAFreshBuildAtTheNewRate(double before, double after)
    {
        var original = new TrafficNetwork(Native, Grid(9, before), before);
        var target = Grid(9, after);
        var fresh = new TrafficNetwork(Native, target, after);
        var signals = target.ToDictionary(n => n.Id, n => n.Signal);
        var rerated = original.Rerated(after, id => signals[id]);
        AssertSame(fresh, rerated, target);
        for (int link = 0; link < fresh.Links.Count; link += 7)
        {
            var route = Drive(fresh, link, after);
            Assert.Equal(fresh.TryMap(route, out var a), rerated.TryMap(route, out var b));
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].LinkId, b[i].LinkId); Assert.Equal(a[i].FreeFlowMinutes, b[i].FreeFlowMinutes);
                Assert.Equal(a[i].StartNominalMinutes, b[i].StartNominalMinutes); Assert.Equal(a[i].EndNominalMinutes, b[i].EndNominalMinutes);
                Assert.Equal(a[i].StartLinkFraction, b[i].StartLinkFraction); Assert.Equal(a[i].EndLinkFraction, b[i].EndLinkFraction);
            }
            fresh.ValidateRemainingMapping(route, a, 0);
            rerated.ValidateRemainingMapping(route, b, 0);
        }
    }

    [Fact]
    public void ARetimedNetworkLeavesTheOriginalUntouched()
    {
        var nodes = Grid(6, 1);
        var original = new TrafficNetwork(Native, nodes, 1);
        var copy = new TrafficNetwork(Native, Grid(6, 1), 1);
        var target = Grid(6, 0.25).ToDictionary(n => n.Id, n => n.Signal);
        _ = original.Rerated(0.25, id => target[id]);
        AssertSame(copy, original, nodes);
    }

    [Fact]
    public void ARetimeAllocatesAFractionOfAFreshBuild()
    {
        var nodes = Grid(40, 1);
        var original = new TrafficNetwork(Native, nodes, 1);
        var target = Grid(40, 1.0 / 3);
        var signals = target.ToDictionary(n => n.Id, n => n.Signal);
        _ = original.Rerated(1.0 / 3, id => signals[id]);
        _ = new TrafficNetwork(Native, target, 1.0 / 3);
        long started = GC.GetAllocatedBytesForCurrentThread();
        _ = new TrafficNetwork(Native, target, 1.0 / 3);
        long fresh = GC.GetAllocatedBytesForCurrentThread() - started;
        started = GC.GetAllocatedBytesForCurrentThread();
        _ = original.Rerated(1.0 / 3, id => signals[id]);
        long rerated = GC.GetAllocatedBytesForCurrentThread() - started;
        Assert.True(rerated * 5 < fresh, rerated + " B rerated vs " + fresh + " B fresh for " + nodes.Count + " nodes");
    }
}

public sealed class Gr5FrameBoundTests
{
    [Fact]
    public void PrewarmedThroughRoutesMatchRoutesComputedOnTheMainThread()
    {
        var network = new TrafficNetwork("gr5-through", Gr5RetimeReuseTests.Grid(18, 1), 1);
        var direct = new ThroughTrafficRoutes(network);
        var warmed = new ThroughTrafficRoutes(network);
        Assert.True(direct.Entries.Count > 4, direct.Entries.Count.ToString());
        warmed.Prewarm(direct.Entries.ToList());
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (warmed.Prewarmed < direct.Entries.Count && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(5);
        Assert.Equal(direct.Entries.Count, warmed.Prewarmed);
        long directBytes = 0, warmedBytes = 0;
        foreach (int entry in direct.Entries)
            for (uint draw = 0; draw < 5; draw++)
            {
                long started = GC.GetAllocatedBytesForCurrentThread();
                var a = direct.Route(entry, draw * 7919);
                directBytes += GC.GetAllocatedBytesForCurrentThread() - started;
                started = GC.GetAllocatedBytesForCurrentThread();
                var b = warmed.Route(entry, draw * 7919);
                warmedBytes += GC.GetAllocatedBytesForCurrentThread() - started;
                Assert.Equal(a == null, b == null);
                if (a == null) continue;
                Assert.Equal(a.Select(l => (l.LinkId, l.StartNominalMinutes, l.EndNominalMinutes, l.FreeFlowMinutes)),
                    b!.Select(l => (l.LinkId, l.StartNominalMinutes, l.EndNominalMinutes, l.FreeFlowMinutes)));
            }
        Assert.True(warmedBytes * 2 < directBytes, warmedBytes + " B prewarmed vs " + directBytes + " B on the main thread");
    }

    [Fact]
    public void AThroughRouteRequestedBeforeItsPrewarmFinishesIsStillComputed()
    {
        var network = new TrafficNetwork("gr5-through", Gr5RetimeReuseTests.Grid(8, 1), 1);
        var direct = new ThroughTrafficRoutes(network);
        var racing = new ThroughTrafficRoutes(network);
        racing.Prewarm(direct.Entries.ToList());
        foreach (int entry in direct.Entries)
            Assert.Equal(direct.Route(entry, 3)?.Select(l => l.LinkId), racing.Route(entry, 3)?.Select(l => l.LinkId));
    }
}

public sealed class Gr5NetworkSwapTests
{
    private const string Native = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static Position P(double x) => new(x, 0, 0);

    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private static TrafficNetwork Line() => new TrafficNetwork(Native, Enumerable.Range(0, 12).Select(i => new TrafficNetworkNode
    { Id = i, Position = P(i * 10), AllowsCar = true, KilometresPerHour = 36, Boundary = i % 4 == 0,
        Successors = i == 11 ? Array.Empty<int>() : new[] { i + 1 } }).ToArray(), 1);

    private static CityState City(TrafficNetwork network)
    {
        var route = new Route();
        for (int i = 0; i < 11; i++)
        {
            network.TryGetEdge(i, i + 1, out var edge);
            route.Legs.Add(new RouteLeg { From = P(i * 10), To = P((i + 1) * 10), Mode = TravelMode.Car, Minutes = edge.FreeFlowMinutes,
                ServiceId = $"native:road:v1:{Native}:{i}:{i + 1}" });
        }
        var state = Population.Create(new[] { new CitizenIdentity { Id = "driver", Name = "Driver", Age = 30, Persona = Persona.Commuter } }, new[]
        { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = route.Legs[0].From },
          new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = route.Legs[^1].To } }, "gr5-network-swap", "0.40.4", 7, 100);
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a";
        citizen.DestinationId = citizen.DayPlaceId = "b"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.DepartureMinute = 100; citizen.NextEventMinute = 100 + route.Minutes; citizen.Journey = route;
        citizen.Preferences.OwnsCar = true; citizen.Car = new CarState { Id = "driver:car", ParkedPosition = route.Legs[0].From };
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        return state;
    }

    private static string Run(bool swap)
    {
        var network = Line();
        var planner = new Planner { TrafficNetwork = network };
        var city = new CitySimulator(City(network), planner);
        var saves = new List<string>();
        for (double minute = 100; minute <= 112; minute += 0.05)
        {
            if (swap && Math.Abs(minute - 101) < 0.001)
            {
                Assert.True(city.Traffic!.VehicleCount > 0);
                planner.TrafficNetwork = network.Rerated(1, _ => null);
            }
            city.RefreshTrafficClosures();
            city.AdvanceTo(minute);
            city.SynchronizeSnapshot();
            saves.Add(PersistentCity.Persistence.SaveCodec.Write(city.State));
        }
        return string.Join("\n", saves);
    }

    [Fact]
    public void ASteadyClosureRefreshAllocatesNothingAfterWarmUp()
    {
        var network = Line();
        var city = new CitySimulator(City(network), new Planner { TrafficNetwork = network });
        city.RefreshTrafficClosures(); city.AdvanceTo(101); city.RefreshTrafficClosures();
        Assert.True(city.Traffic!.VehicleCount > 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 500; frame++) city.RefreshTrafficClosures();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SwappingInARetimedNetworkMidJourneyKeepsTheCityIdentical()
    {
        Assert.Equal(Run(false), Run(true));
    }
}
