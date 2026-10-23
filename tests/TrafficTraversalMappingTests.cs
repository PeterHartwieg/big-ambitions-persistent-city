using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficTraversalMappingTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static Position P(double x) => new(x, 0, 0);
    private static TrafficNetworkNode[] Nodes() => Enumerable.Range(0, 4).Select(i => new TrafficNetworkNode
    { Id = i, Position = P(i * 10), AllowsCar = true, KilometresPerHour = 36,
        Successors = i == 3 ? Array.Empty<int>() : new[] { i + 1 } }).ToArray();
    private static string Tag(int from, int to, bool fragment = false) =>
        $"native:road:{(fragment ? "fragment1" : "v1")}:{Native}:{from}:{to}";
    private static Route Uniform(double factor = 1.5) => new()
    { Legs = Enumerable.Range(0, 3).Select(i => new RouteLeg
        { From = P(i * 10), To = P((i + 1) * 10), Mode = TravelMode.Car, Minutes = factor, ServiceId = Tag(i, i + 1) }).ToList() };
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public Planner(double rate = 1) { TrafficNetwork = new TrafficNetwork(Native, Nodes(), rate); }
        public TrafficNetwork TrafficNetwork { get; }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }
    private static (CityState State, CitizenState Citizen) State(Route route)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "slow", Name = "Slow", Age = 30, Persona = Persona.Commuter } }, new[]
        { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = route.Legs[0].From },
          new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = route.Legs[^1].To } }, "uniform-slow-test", "0.40.4", 7, 100);
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a";
        citizen.DestinationId = citizen.DayPlaceId = "b"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.DepartureMinute = 100; citizen.NextEventMinute = 100 + route.Minutes; citizen.Journey = route;
        citizen.Preferences.OwnsCar = true; citizen.Car = new CarState { Id = "slow:car", ParkedPosition = route.Legs[0].From };
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        return (state, citizen);
    }

    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(1.75)]
    public void UniformCommittedSlowdownBindsAndReloadsWithoutChangingCanonicalNetwork(double factor)
    {
        var planner = new Planner(); var route = Uniform(factor);
        Assert.True(planner.TrafficNetwork.TryMap(route, out var mapping));
        planner.TrafficNetwork.ValidateRemainingMapping(route, mapping, 0);
        Assert.Equal(3 * factor, Assert.Single(mapping).FreeFlowMinutes);
        Assert.Equal(3, planner.TrafficNetwork.Links.Single().FreeFlowMinutes);
        var (state, citizen) = State(route); var simulation = new CitySimulator(state, planner); simulation.AdvanceTo(100);
        Assert.True(simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out var lease));
        simulation.AdvanceTo(102); Assert.True(simulation.ObservePhysicalJourney(lease!, factor));
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var restored = new CitySimulator(decoded.State!, new Planner());
        Assert.True(restored.TryAcquirePhysicalJourney(citizen.Identity.Id, out var reacquired));
        Assert.Equal(factor, reacquired!.ObservedMinutes);
        Assert.Equal(3 * factor, restored.State.Citizens[0].Journey!.Minutes);
        Assert.Equal(1, restored.Traffic!.VehicleCount);
    }

    [Fact]
    public void UniformSlowdownSurvivesActualClockRetimingCropAndASecondReload()
    {
        var (state, citizen) = State(Uniform()); var simulation = new CitySimulator(state, new Planner()); simulation.AdvanceTo(100);
        Assert.True(simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out var lease));
        simulation.AdvanceTo(102); Assert.True(simulation.ObservePhysicalJourney(lease!, 1.5));
        state = SaveCodec.Read(SaveCodec.Write(state)).State!;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        var newNetwork = new Planner(2).TrafficNetwork;
        var saved = Assert.Single(state.Traffic!.Vehicles);
        Assert.Equal(1.5, saved.Route[0].StartNominalMinutes);
        Assert.Equal(6, saved.Route[0].FreeFlowMinutes, 12);
        newNetwork.ValidateRemainingMapping(state.Citizens[0].Journey!, saved.Route, saved.NominalMinutes);
        state = SaveCodec.Read(SaveCodec.Write(state)).State!;
        var restored = new CitySimulator(state, new Planner(2));
        Assert.Equal(1, restored.Traffic!.VehicleCount);
        Assert.Equal(7.5, restored.State.Citizens[0].Journey!.Minutes);
    }

    [Theory]
    [InlineData(.9)] [InlineData(1.7501)]
    public void FutureCanonicalSpeedupAndUnreviewedSlowdownAreRefused(double factor)
    {
        var network = new Planner().TrafficNetwork; var route = Uniform(factor);
        Assert.True(network.TryMap(route, out var mapping)); // Mapper remains geometry-only.
        network.ValidateMapping(route, mapping);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapping, 0));
    }

    [Fact]
    public void IndividuallyBoundedButNonuniformEdgeTimesCannotBorrowAValidSectionAverage()
    {
        var network = new Planner().TrafficNetwork; var route = Uniform();
        route.Legs[0].Minutes = 1; route.Legs[1].Minutes = 1.75; route.Legs[2].Minutes = 1.75;
        Assert.True(network.TryMap(route, out var mapping)); network.ValidateMapping(route, mapping);
        Assert.Equal(4.5, Assert.Single(mapping).FreeFlowMinutes); // Same allowed mean as uniform 1.5.
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapping, 0));
    }

    [Fact]
    public void SavedKernelDurationCannotInventASecondAllowedFactor()
    {
        var network = new Planner().TrafficNetwork; var route = Uniform();
        Assert.True(network.TryMap(route, out var mapping)); mapping[0].FreeFlowMinutes = 4;
        network.ValidateMapping(route, mapping); // Same endpoints and a separately plausible slowdown.
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapping, 0));
    }

    [Fact]
    public void PartialCurbFragmentsAndProgressCropRetainTheirCommittedSpatialFactor()
    {
        var network = new Planner().TrafficNetwork;
        var route = new Route { Legs = new()
        { new RouteLeg { From = P(3), To = P(10), Minutes = 1.05, Mode = TravelMode.Car, ServiceId = Tag(0, 1, true) },
          new RouteLeg { From = P(10), To = P(20), Minutes = 1.5, Mode = TravelMode.Car, ServiceId = Tag(1, 2) },
          new RouteLeg { From = P(20), To = P(27), Minutes = 1.05, Mode = TravelMode.Car, ServiceId = Tag(2, 3, true) } } };
        Assert.True(network.TryMap(route, out var full)); network.ValidateRemainingMapping(route, full, 0);
        Assert.True(network.TryMapRemaining(route, 1.8, out var current));
        network.ValidateRemainingMapping(route, current, 1.8);
        Assert.Equal(1.8, Assert.Single(current).FreeFlowMinutes, 10);
        Assert.Equal(.5, current[0].StartLinkFraction, 10); Assert.Equal(.9, current[0].EndLinkFraction, 10);
        current[0].FreeFlowMinutes = 1.5;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, current, 1.8));
    }

    [Fact]
    public void CompletedHistoricalPaceDoesNotChangeFutureValidation()
    {
        var nodes = Nodes(); nodes[1].Boundary = true;
        var network = new TrafficNetwork(Native, nodes, 1); var route = Uniform(); route.Legs[0].Minutes = .5;
        Assert.True(network.TryMap(route, out var mapping)); Assert.Equal(2, mapping.Count);
        network.ValidateRemainingMapping(route, mapping, .5);
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, mapping, 0));
    }
    [Fact]
    public void NewlyPlannedTraversalDurationsMatchCanonicalMappingWithoutChangingItsDefinitions()
    {
        var nodes = Nodes(); var graph = new TransportRoadGraph(nodes.Select(node => new TransportRoadNode
        { Position = node.Position, AllowsCar = node.AllowsCar, KilometresPerHour = node.KilometresPerHour,
            Successors = node.Successors }).ToArray());
        int[] path = Array.Empty<int>();
        var route = graph.PlanWithTraversal(0, 3, _ => false, (_, _) => true,
            (_, _, baseline) => baseline * 1.5, pathFound: found => path = found)!;
        for (int i = 0; i < route.Legs.Count; i++) route.Legs[i].ServiceId = Tag(path[i], path[i + 1]);
        var network = new TrafficNetwork(Native, nodes, 1);
        Assert.True(network.TryMap(route, out var mapped)); network.ValidateRemainingMapping(route, mapped, 0);
        Assert.Equal(4.5, route.Minutes); Assert.Equal(3, network.Links.Single().FreeFlowMinutes);
    }

    [Fact]
    public void SlowedTinyFragmentsAndLargeNominalOffsetRetainOnlyArithmeticAllowances()
    {
        var network = new Planner().TrafficNetwork;
        var tiny = new Route { Legs = new() { new RouteLeg { From = P(10 - 1e-7), To = P(10), Minutes = 1.5e-8,
            Mode = TravelMode.Car, ServiceId = Tag(0, 1, true) } } };
        Assert.True(network.TryMap(tiny, out var fragment)); network.ValidateRemainingMapping(tiny, fragment, 0);
        fragment[0].FreeFlowMinutes = 1e-6;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(tiny, fragment, 0));
        var route = Uniform(); route.Legs.Insert(0, new RouteLeg
        { From = P(0), To = P(0), Mode = TravelMode.Walk, Minutes = 1000000 });
        Assert.True(network.TryMap(route, out var shifted));
        shifted[0].StartNominalMinutes = Math.BitIncrement(shifted[0].StartNominalMinutes);
        shifted[0].FreeFlowMinutes = Math.BitIncrement(shifted[0].FreeFlowMinutes);
        network.ValidateRemainingMapping(route, shifted, 1000000);
        shifted[0].FreeFlowMinutes += .001;
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(route, shifted, 1000000));
    }

}
