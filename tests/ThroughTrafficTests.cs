using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>GR-8 (G5): anonymous through-traffic cars edge to edge in the traffic kernel.</summary>
public sealed class ThroughTrafficTests
{
    private const string Native = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>Two one-way roads crossing at node 3: east along z = 100 (nodes 0..5) and north along x = 120 (nodes 10, 11,
    /// 3, 13, 14), plus an interior cul-de-sac from node 2 to node 20. Every node is a boundary, so every native edge is
    /// its own section.</summary>
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
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    private static int Link(TrafficNetwork network, int from, int to) { Assert.True(network.TryGetEdge(from, to, out var edge)); return edge.LinkId; }

    /// <summary>Residents driving the east road, reaching it a minute apart from minute 1440.</summary>
    private static CitySimulator City(int count = 40)
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
            // A walk to the car of 0.5 + i minutes staggers the drivers' road entries.
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = new Position(-10, 0, 100), To = new Position(0, 0, 100), Minutes = 0.5 + i, Mode = TravelMode.Walk });
            for (int n = 0; n < 5; n++)
                route.Legs.Add(new RouteLeg { From = new Position(40 * n, 0, 100), To = new Position(40 * (n + 1), 0, 100), Minutes = 4, Mode = TravelMode.Car,
                    ServiceId = "native:road:v1:" + Native + ":" + n + ":" + (n + 1) });
            citizen.Journey = route; citizen.NextEventMinute = citizen.DepartureMinute + route.Minutes;
        }
        return new CitySimulator(state, new Planner());
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
        // The cul-de-sac ends inside the city: not an exit.
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
                // Whole sections to the far end of the exit: never a curb stop, so never parked.
                Assert.Equal(1, vehicle.Route[vehicle.Route.Count - 1].EndLinkFraction);
                // No person: no resident has the ID, no inspection, no permission as a resident.
                Assert.Null(city.FindCitizen(vehicle.VehicleId));
                Assert.Null(city.Inspect(vehicle.VehicleId));
                Assert.Equal(ThroughTraffic.InspectorLine, CitySimulator.DescribeVehicle(vehicle.VehicleId));
            }
        }
        Assert.NotEmpty(seen);
        Assert.True(city.ThroughCarsCompleted > 0);
        Assert.Equal(city.ThroughCarsGenerated, city.ThroughCarsCompleted + city.ThroughCarCount);
        // Completed through cars leave the kernel at once; residents keep their own cars and parking.
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
                // Every queued car counts toward the section's storage and the route forecast, whoever it is.
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
        // Capturing for the save does not touch the live kernel, and saving again writes the same bytes.
        Assert.Equal(live.Vehicles.Count, city.Traffic.Capture().Vehicles.Count);
        Assert.True(city.ThroughCarCount > 0);
        Assert.Equal(payload, SaveCodec.Write(city.State));
        // The saved kernel holds exactly the live resident cars, at their live positions.
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
        // The capture restores and the resident still completes its route.
        var restored = TrafficKernel.Restore(without);
        string? done = null; restored.VehicleCompleted += (id, _) => done = id;
        restored.AdvanceTo(60, 10000);
        Assert.Equal("resident", done);
    }
}
