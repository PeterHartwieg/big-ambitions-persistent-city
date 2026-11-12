using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class ThroughTrafficTests
{
    private const string Native = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static TrafficNetwork Network()
    {
        var nodes = new List<TrafficNetworkNode>();
        void Node(int id, double x, double z, params int[] next)
            => nodes.Add(new TrafficNetworkNode { Id = id, Position = new Position(x, 0, z), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = next });
        Node(0, 0, 100, 1); Node(1, 40, 100, 2); Node(2, 80, 100, 3, 20); Node(3, 120, 100, 4, 13); Node(4, 160, 100, 5); Node(5, 200, 100);
        Node(10, 120, 20, 11); Node(11, 120, 60, 3); Node(13, 120, 140, 14); Node(14, 120, 180);
        Node(20, 80, 130);
        return new TrafficNetwork(Native, nodes, 1);
    }

    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = Network();
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public HashSet<int> Closed { get; } = new HashSet<int>();
        public bool IsTrafficLinkClosed(int id) => Closed.Contains(id);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    private static int Link(TrafficNetwork network, int from, int to) { Assert.True(network.TryGetEdge(from, to, out var edge)); return edge.LinkId; }

    private static CitySimulator City(int count = 40, Planner? planner = null)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "driver" + i.ToString("D3"), Name = "Driver " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(-10, 0, 100) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(200, 0, 100) }
        }, "through-traffic-test", "0.40.4", 11, 1440);
        foreach (var citizen in state.Citizens)
        {
            int i = int.Parse(citizen.Identity.Id.Substring(6));
            citizen.PlaceId = citizen.HomeId = "home"; citizen.DayPlaceId = "work";
            citizen.Phase = CitizenPhase.Travelling; citizen.DestinationId = "work"; citizen.DestinationActivity = Activity.Work;
            citizen.ActivityUntilMinute = 2880; citizen.DepartureMinute = 1440;
            citizen.Preferences.OwnsCar = true;
            citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 100) };
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = new Position(-10, 0, 100), To = new Position(0, 0, 100), Minutes = 0.5 + i, Mode = TravelMode.Walk });
            for (int n = 0; n < 5; n++)
                route.Legs.Add(new RouteLeg { From = new Position(40 * n, 0, 100), To = new Position(40 * (n + 1), 0, 100), Minutes = 4, Mode = TravelMode.Car,
                    ServiceId = "native:road:v1:" + Native + ":" + n + ":" + (n + 1) });
            citizen.Journey = route; citizen.NextEventMinute = citizen.DepartureMinute + route.Minutes;
        }
        return new CitySimulator(state, planner ?? new Planner());
    }

    private static List<TrafficVehicleState> Through(CitySimulator city)
        => city.Traffic!.Capture().Vehicles.Where(v => ThroughTraffic.IsThroughVehicle(v.VehicleId)).ToList();

    [Fact]
    public void MapEdgesAreTheOpenEndsOnTheOuterBandAndRoutesUseWholeSections()
    {
        var network = Network();
        var routes = new ThroughTrafficRoutes(network);
        Assert.Equal(new[] { Link(network, 0, 1), Link(network, 10, 11) }.OrderBy(i => i), routes.Entries);
        Assert.Equal(new[] { Link(network, 4, 5), Link(network, 13, 14) }.OrderBy(i => i), routes.Exits);
        Assert.False(routes.IsExit(Link(network, 2, 20)));
        foreach (int entry in routes.Entries)
            for (uint draw = 0; draw < 4; draw++)
            {
                var route = routes.Route(entry, draw)!;
                Assert.Equal(entry, route[0].LinkId);
                Assert.True(routes.IsExit(route[route.Count - 1].LinkId));
                Assert.All(route, leg => { Assert.Equal(0, leg.StartLinkFraction); Assert.Equal(1, leg.EndLinkFraction); });
            }
    }

    [Fact]
    public void ThroughCarsEnterAndLeaveOnlyAtMapEdgesNeverParkCarryNobodyAndHaveNoPersonId()
    {
        var city = City();
        city.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        var routes = new ThroughTrafficRoutes(Network());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parked = city.State.Citizens.ToDictionary(c => c.Identity.Id, c => c.Car!.ParkedPosition.X);
        for (double minute = 1440; minute <= 1440 + 180; minute += 0.5)
        {
            city.AdvanceTo(minute);
            foreach (var vehicle in Through(city))
            {
                seen.Add(vehicle.VehicleId);
                Assert.True(routes.IsEntry(vehicle.Route[0].LinkId), vehicle.VehicleId + " entered inside the city");
                Assert.True(routes.IsExit(vehicle.Route[vehicle.Route.Count - 1].LinkId), vehicle.VehicleId + " leaves inside the city");
                Assert.Equal(1, vehicle.Route[vehicle.Route.Count - 1].EndLinkFraction);
                Assert.Null(city.FindCitizen(vehicle.VehicleId));
                Assert.Null(city.Inspect(vehicle.VehicleId));
                Assert.Equal(ThroughTraffic.InspectorLine, CitySimulator.DescribeVehicle(vehicle.VehicleId));
            }
        }
        Assert.NotEmpty(seen);
        Assert.True(city.ThroughCarsCompleted > 0);
        Assert.Equal(city.ThroughCarsGenerated, city.ThroughCarsCompleted + city.ThroughCarCount);
        Assert.Equal(city.ThroughCarCount, Through(city).Count);
        Assert.All(city.State.Citizens, c => Assert.Equal(c.Identity.Id + ":car", c.Car!.Id));
        Assert.All(city.State.Citizens, c => Assert.True(c.Car!.ParkedPosition.X == parked[c.Identity.Id] || c.Car.ParkedPosition.X == 200));
        Assert.Null(CitySimulator.DescribeVehicle("driver000"));
    }

    [Fact]
    public void ThroughCarsCountTowardRoadCapacityLikeResidentCars()
    {
        var city = City();
        city.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        int shared = 0;
        for (double minute = 1440; minute <= 1440 + 240; minute += 0.25)
        {
            city.AdvanceTo(minute);
            var captured = city.Traffic!.Capture();
            var capacity = captured.Definitions.ToDictionary(d => d.Id, d => d.Capacity);
            foreach (var link in captured.Links)
            {
                Assert.True(link.Vehicles.Count <= capacity[link.LinkId]);
                Assert.Equal(link.Vehicles.Count, city.Traffic.Forecast(link.LinkId, city.Traffic.Clock).Occupancy);
                if (link.Vehicles.Any(ThroughTraffic.IsThroughVehicle) && link.Vehicles.Any(id => !ThroughTraffic.IsThroughVehicle(id))) shared++;
            }
        }
        Assert.True(shared > 0, "through and resident cars queue on the same sections");
        Assert.All(city.State.Citizens, c => Assert.Equal("work", c.PlaceId));
    }

    [Fact]
    public void ShareZeroLeavesTheKernelIdenticalToToday()
    {
        var plain = City(); var zero = City();
        zero.AttachThroughTraffic(new ThroughTrafficSettings(0));
        for (double minute = 1440; minute <= 1440 + 200; minute += 0.75) { plain.AdvanceTo(minute); zero.AdvanceTo(minute); }
        Assert.Equal(0, zero.ThroughCarsGenerated);
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(zero.State));
        Assert.Equal(plain.Traffic!.ProcessedEvents, zero.Traffic!.ProcessedEvents);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThroughTrafficSettings(0.51));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThroughTrafficSettings(-0.01));
        Assert.Equal(0.10, new ThroughTrafficSettings().Share);
    }

    [Fact]
    public void TheSaveHoldsResidentCarsOnlyAndAReloadGeneratesThroughCarsAgain()
    {
        var city = City();
        city.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        double minute = 1440;
        while (city.ThroughCarCount == 0 || !city.Traffic!.Capture().Vehicles.Any(v => !ThroughTraffic.IsThroughVehicle(v.VehicleId)))
        { minute += 0.25; city.AdvanceTo(minute); Assert.True(minute < 1440 + 240); }
        var live = city.Traffic!.Capture();
        string payload = SaveCodec.Write(city.State);
        Assert.DoesNotContain(ThroughTraffic.Prefix, payload);
        Assert.Equal(live.Vehicles.Count, city.Traffic.Capture().Vehicles.Count);
        Assert.True(city.ThroughCarCount > 0);
        Assert.Equal(payload, SaveCodec.Write(city.State));
        var saved = city.State.Traffic!.Vehicles.ToDictionary(v => v.VehicleId);
        var residents = live.Vehicles.Where(v => !ThroughTraffic.IsThroughVehicle(v.VehicleId)).ToList();
        Assert.Equal(residents.Select(v => v.VehicleId).OrderBy(i => i), saved.Keys.OrderBy(i => i));
        foreach (var vehicle in residents) { Assert.Equal(vehicle.RouteIndex, saved[vehicle.VehicleId].RouteIndex); Assert.Equal(vehicle.NominalMinutes, saved[vehicle.VehicleId].NominalMinutes); }

        var read = SaveWire.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        var restored = new CitySimulator(read.State!, new Planner());
        restored.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        double end = minute;
        while (restored.ThroughCarCount == 0) { end += 0.25; restored.AdvanceTo(end); Assert.True(end < minute + 120, "no through car after reload"); }
        Assert.All(Through(restored), v => Assert.StartsWith(ThroughTraffic.Prefix, v.VehicleId));
        restored.AdvanceTo(minute + 240);
        Assert.All(restored.State.Citizens, c => Assert.Equal("work", c.PlaceId));
    }

    [Fact]
    public void ACaptureWithoutNamedVehiclesKeepsTheOthersAndTheirWakeups()
    {
        var network = Network();
        var kernel = new TrafficKernel(network.Links, 0);
        var routes = new ThroughTrafficRoutes(network);
        kernel.Enqueue(ThroughTraffic.Prefix + "0:0", routes.Route(routes.Entries[0], 0)!);
        kernel.Enqueue("resident", routes.Route(routes.Entries[0], 0)!);
        kernel.AdvanceTo(0.01, 100);
        var full = kernel.Capture();
        var none = kernel.Capture(_ => false);
        Assert.Equal(full.Vehicles.Select(v => v.VehicleId), none.Vehicles.Select(v => v.VehicleId));
        var without = kernel.Capture(ThroughTraffic.IsThroughVehicle);
        Assert.Equal(new[] { "resident" }, without.Vehicles.Select(v => v.VehicleId));
        Assert.Equal(2, kernel.VehicleCount);
        var restored = TrafficKernel.Restore(without);
        string? done = null; restored.VehicleCompleted += (id, _) => done = id;
        restored.AdvanceTo(60, 10000);
        Assert.Equal("resident", done);
    }

    internal static TrafficNetwork Grid(int side)
    {
        var nodes = new List<TrafficNetworkNode>();
        int Id(int x, int z) => z * side + x;
        int outside = side * side;
        for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                var next = new List<int>();
                if (x > 0) next.Add(Id(x - 1, z)); if (x + 1 < side) next.Add(Id(x + 1, z));
                if (z > 0) next.Add(Id(x, z - 1)); if (z + 1 < side) next.Add(Id(x, z + 1));
                bool rim = x == 0 || z == 0 || x == side - 1 || z == side - 1;
                if (rim)
                {
                    int sink = outside++, source = outside++;
                    double ox = x == 0 ? -50 : x == side - 1 ? 50 : 0, oz = ox != 0 ? 0 : z == 0 ? -50 : 50;
                    nodes.Add(new TrafficNetworkNode { Id = sink, Position = new Position(x * 50 + ox, 0, z * 50 + oz), KilometresPerHour = 36, AllowsCar = true, Boundary = true });
                    nodes.Add(new TrafficNetworkNode { Id = source, Position = new Position(x * 50 + ox + oz / 5, 0, z * 50 + oz + ox / 5), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { Id(x, z) } });
                    next.Add(sink);
                }
                nodes.Add(new TrafficNetworkNode { Id = Id(x, z), Position = new Position(x * 50, 0, z * 50), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = next.ToArray() });
            }
        return new TrafficNetwork(Native, nodes, 1);
    }

    [Fact]
    public void EdgeDetectionAndOneEntrysPathSearchStayBelowTheSpikeThresholdOnALargeGrid()
    {
        var network = Grid(40);
        Assert.True(network.Links.Count > 6000);
        new ThroughTrafficRoutes(network).Route(0, 0);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var routes = new ThroughTrafficRoutes(network);
        double build = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(4 * 39, routes.Entries.Count);
        Assert.Equal(4 * 39, routes.Exits.Count);
        double worst = 0;
        foreach (int entry in routes.Entries.Take(16))
        {
            watch.Restart();
            Assert.NotNull(routes.Route(entry, 7));
            worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds);
        }
        PerfGate.Under(build, 30, "GR-8 map edges, " + network.Links.Count + " sections");
        PerfGate.Under(worst, 30, "GR-8 one entry's paths, " + network.Links.Count + " sections");
        Console.WriteLine($"GR-8 perf: {network.Links.Count} sections, edges {build:0.00} ms, worst first path search {worst:0.00} ms");
    }

    [Fact]
    public void ThroughCarsAvoidClosedSectionsAndLeaveARoadThatClosesUnderThem()
    {
        var planner = new Planner();
        var network = planner.TrafficNetwork!;
        int north = Link(network, 13, 14);
        planner.Closed.Add(north);
        var city = City(planner: planner);
        city.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        for (double minute = 1440; minute <= 1440 + 90; minute += 0.25)
        {
            city.AdvanceTo(minute);
            Assert.All(Through(city), v => Assert.DoesNotContain(v.Route, leg => leg.LinkId == north));
        }
        Assert.True(city.ThroughCarsSkippedClosed > 0);
        planner.Closed.Clear();
        double at = 1440 + 90;
        while (!Through(city).Any(v => v.Route.Skip(Math.Max(0, v.RouteIndex) + 1).Any(leg => leg.LinkId == north)))
        { at += 0.25; city.AdvanceTo(at); Assert.True(at < 1440 + 400, "no through car heads north"); }
        planner.Closed.Add(north);
        for (double end = at + 120; at <= end; at += 0.25)
        {
            city.AdvanceTo(at);
            city.RefreshTrafficClosures();
        }
        Assert.True(city.ThroughCarsCancelledClosed > 0);
        Assert.DoesNotContain(Through(city), v => v.Route.Skip(Math.Max(0, v.RouteIndex) + 1).Any(leg => leg.LinkId == north));
        Assert.All(city.State.Citizens, c => Assert.Equal("work", c.PlaceId));
    }

    [Fact]
    public void OneEventSlicesEnterThroughCarsAtTheSameMinutesAsWholeAdvancesAcrossMidnight()
    {
        var sliced = City(); var whole = City();
        sliced.AttachThroughTraffic(new ThroughTrafficSettings(0.5)); whole.AttachThroughTraffic(new ThroughTrafficSettings(0.5));
        static string Key(TrafficVehicleState v) => v.VehicleId + "@" + v.RouteIndex + ":" + v.NominalMinutes.ToString("R") + ":" + v.MotionStartedAt.ToString("R")
            + ":" + v.MotionStartFraction.ToString("R") + ":" + v.ReadyAt.ToString("R");
        for (double minute = 1440 + 30; minute <= 2880 + 120; minute += 7.3)
        {
            while (!sliced.AdvanceTo(minute, 1).CaughtUp) { }
            Assert.True(whole.AdvanceTo(minute).CaughtUp);
            Assert.Equal(whole.Traffic!.Capture().Vehicles.Select(Key), sliced.Traffic!.Capture().Vehicles.Select(Key));
            Assert.Equal(whole.ThroughCarsGenerated, sliced.ThroughCarsGenerated);
        }
        Assert.True(whole.ThroughCarsGenerated > 0);
    }

    [Fact]
    public void ACaptureWithoutABlockedThroughCarKeepsTheResidentBehindItWhereItIs()
    {
        var network = Network();
        var kernel = new TrafficKernel(network.Links, 0);
        var routes = new ThroughTrafficRoutes(network);
        var route = routes.Route(routes.Entries[0], 0)!;
        kernel.SetLinkClosed(route[1].LinkId, true);
        kernel.Enqueue(ThroughTraffic.Prefix + "0:0", route);
        kernel.Enqueue("resident", routes.Route(routes.Entries[0], 0)!);
        kernel.AdvanceTo(20, 10000);
        Assert.True(kernel.TryGetVehicle("resident", out var live));
        var without = kernel.Capture(ThroughTraffic.IsThroughVehicle);
        TrafficKernel.ValidateState(without);
        var restored = TrafficKernel.Restore(without);
        Assert.True(restored.TryGetVehicle("resident", out var saved));
        Assert.Equal(live.NominalMinutes, saved.NominalMinutes);
        Assert.Equal(live.MaximumNominalMinutes, saved.MaximumNominalMinutes);
        restored.SetLinkClosed(route[1].LinkId, false);
        string? done = null; restored.VehicleCompleted += (id, _) => done = id;
        restored.AdvanceTo(120, 10000);
        Assert.Equal("resident", done);
    }
}
