using System;
using System.Collections.Generic;
using System.Reflection;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalTrafficCornerPermissionTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Planner : IRoutePlanner
    { public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null; }

    private sealed class TrafficPlanner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = new TrafficNetwork(Native, new[]
        {
            new TrafficNetworkNode { Id = 0, Position = new Position(0,0,0), KilometresPerHour = 36, AllowsCar = true, Successors = new[] { 1 } },
            new TrafficNetworkNode { Id = 1, Position = new Position(10,0,0), KilometresPerHour = 36, AllowsCar = true, Successors = new[] { 2 } },
            new TrafficNetworkNode { Id = 2, Position = new Position(20,0,0), KilometresPerHour = 36, AllowsCar = true }
        }, 10);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private static (CitySimulator Simulator, CitizenState Citizen) Journey(double prefix = 0, double minutes = 10,
        Position? from = null, Position? to = null, TravelMode mode = TravelMode.Car)
    {
        Position a = from ?? new Position(0, 0, 0), b = to ?? new Position(10, 0, 0);
        var legs = new List<RouteLeg>();
        if (prefix > 0) legs.Add(new RouteLeg { From = a, To = a, Mode = TravelMode.Walk, Minutes = prefix });
        legs.Add(new RouteLeg { From = a, To = b, Mode = mode, Minutes = minutes });
        legs.Add(new RouteLeg { From = b, To = new Position(b.X, b.Y, b.Z + 10), Mode = TravelMode.Car, Minutes = 10 });
        var route = new Route { Legs = legs };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "corner-permission" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", NextEventMinute = route.Minutes };
        var state = new CityState { Minute = route.Minutes, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = a }, new Place { Id = "b", Position = legs[legs.Count - 1].To } } };
        return (new CitySimulator(state, new Planner()), citizen);
    }

    [Fact]
    public void CapturedNativeCornerArithmeticRetainsExactExistingPermission()
    {
        const double elapsed = 115.74062969885975, fraction = .63410704080244773;
        const double minutes = .37883332483901316, maximum = 115.87924214512763;
        var segment = new RouteLeg { From = new Position(221.41065979003906, 1.1920928955078125E-07, -101.51970672607422),
            To = new Position(221.40960693359375, 1.1920928955078125E-07, -105.72896575927734), Mode = TravelMode.Car, Minutes = minutes };
        // Preserve the exact native elapsed/fraction pair; rebuilding its entire preceding route
        // from one summed prefix would change that fraction by several ulps before this test runs.
        var point = Position.Lerp(segment.From, segment.To, fraction);
        var sample = (PhysicalJourneySample)typeof(PhysicalJourneySample).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
            .Invoke(new object?[] { elapsed, 200.0, 263, fraction, segment, point, (Position?)point, (TravelMode?)TravelMode.Car });
        var marker = new Position(221.40997314453125, .0099703343585133553, -104.24519348144531);
        Assert.InRange(Position.Distance(marker, sample.To), 1.48, 1.5);
        Assert.True(PhysicalProgress.TryProject(sample, marker, true, out double endpoint));
        Assert.Equal(115.87924214512775, endpoint);
        Assert.True(endpoint > maximum);
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, maximum));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, marker, endpoint, maximum, out _));
        Assert.True(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, endpoint, maximum, out double accepted));
        Assert.Equal(maximum, accepted);
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, false, endpoint, maximum, out _));
    }

    [Fact]
    public void SubUlpCornerPermissionSurvivesSamplingBoundaryNormalizationWithoutExtraCredit()
    {
        var (simulation, citizen) = Journey();
        var previous = simulation.SampleJourney(citizen, 9);
        var marker = new Position(9, 0, .5);
        Assert.True(PhysicalProgress.TryProject(previous, marker, true, out double endpoint));
        double maximum = Math.BitDecrement(endpoint);
        Assert.True(PhysicalTrafficCornerPermission.TryClampEndpointExcess(previous, true, endpoint, maximum, out double accepted));
        Assert.Equal(maximum, accepted);
        Assert.True(accepted < endpoint);
        var next = simulation.SampleJourney(citizen, accepted);
        Assert.Equal(previous.LegIndex + 1, next.LegIndex);
        Assert.InRange(next.ElapsedMinutes, endpoint, endpoint + 1e-12); // Geometry normalizes; accounting remains accepted above.
        Assert.True(PhysicalProgress.TryProject(next, marker, false, out double reused));
        Assert.True(reused > endpoint + .1);
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(next, false, reused, maximum, out _));
    }

    [Fact]
    public void OnlyTheExactTrustedEndpointCanUseTheExistingNativePredicate()
    {
        var (simulation, citizen) = Journey();
        var sample = simulation.SampleJourney(citizen, 9);
        double endpoint = sample.ElapsedMinutes + sample.RemainingLegMinutes;
        double maximum = endpoint - 5e-10;
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, maximum));
        Assert.True(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, endpoint, maximum, out double accepted));
        Assert.Equal(maximum, accepted);
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, false, endpoint, maximum, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, endpoint - 1e-10, maximum, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, endpoint + 1e-10, maximum, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, maximum, maximum, out _));
    }

    [Theory]
    [InlineData(1e-8)] [InlineData(.01)] [InlineData(1)]
    public void MaterialEndpointGapsRemainRefusedEvenWithNativeReceipt(double gap)
    {
        var (simulation, citizen) = Journey(); var sample = simulation.SampleJourney(citizen, 9);
        const double endpoint = 10;
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint - gap));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, endpoint, endpoint - gap, out _));
    }

    [Fact]
    public void NonfiniteWrongModeAndRegressedPermissionCannotBorrowCornerAllowance()
    {
        var (simulation, citizen) = Journey(); var sample = simulation.SampleJourney(citizen, 9);
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(default, 10));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, double.NaN));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, double.PositiveInfinity));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, double.NaN, 10, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, double.PositiveInfinity, 10, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampEndpointExcess(sample, true, 10, 8, out _));
        var (walkSimulation, walker) = Journey(mode: TravelMode.Walk);
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(walkSimulation.SampleJourney(walker, 9), 10));
    }

    [Fact]
    public void ActualTrafficLeaseNormalizationCannotGrantExtraProgressOrLeaveItsSection()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "native-corner", Name = "Corner", Age = 30, Persona = Persona.Commuter } }, new[]
        {
            new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0,0,0) },
            new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = new Position(20,0,0) }
        }, "corner-permission-test", "0.40.4", 1, 100);
        var citizen = state.Citizens[0];
        citizen.PlaceId = citizen.HomeId = "a"; citizen.DayPlaceId = citizen.DestinationId = "b";
        citizen.Phase = CitizenPhase.Travelling; citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.DepartureMinute = 100; citizen.Preferences.OwnsCar = true;
        citizen.Car = new CarState { Id = "native-corner:car", ParkedPosition = new Position(0,0,0) };
        citizen.Journey = new Route { Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(10,0,0), Mode = TravelMode.Car, Minutes = 10, ServiceId = "native:road:v1:" + Native + ":0:1" },
            new RouteLeg { From = new Position(10,0,0), To = new Position(20,0,0), Mode = TravelMode.Car, Minutes = 10, ServiceId = "native:road:v1:" + Native + ":1:2" }
        } };
        citizen.NextEventMinute = 120;
        var simulation = new CitySimulator(state, new TrafficPlanner());
        simulation.AdvanceTo(100);
        Assert.True(simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out var lease));
        simulation.AdvanceTo(Math.BitDecrement(110));
        var before = simulation.Traffic!.GetPermission(citizen.Identity.Id);
        Assert.True(before.MaximumNominalMinutes < 10);
        Assert.True(before.MaximumNominalMinutes + 1e-9 >= 10);
        Assert.True(simulation.TrySamplePhysicalJourney(lease!, out var previous));
        Assert.True(PhysicalTrafficCornerPermission.TryClampEndpointExcess(previous, true, 10, before.MaximumNominalMinutes, out double accepted));
        Assert.True(simulation.ObservePhysicalJourney(lease!, accepted));
        Assert.Equal(before.MaximumNominalMinutes, lease!.ObservedMinutes);
        Assert.True(simulation.TrySamplePhysicalJourney(lease, out var next));
        Assert.Equal(1, next.LegIndex); Assert.Equal(0, next.LegFraction);
        Assert.True(next.ElapsedMinutes > lease.ObservedMinutes);
        Assert.True(PhysicalTrafficCornerPermission.IsNormalizedBoundary(next, lease.ObservedMinutes));
        Assert.True(PhysicalProgress.TryProject(next, new Position(10,0,0), false, out double normalized));
        Assert.Equal(next.ElapsedMinutes, normalized);
        Assert.True(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(next, lease.ObservedMinutes, normalized, before.MaximumNominalMinutes, out double same));
        Assert.Equal(accepted, same);
        Assert.True(simulation.ObservePhysicalJourney(lease, same));
        var after = simulation.Traffic.GetPermission(citizen.Identity.Id);
        Assert.Equal(before.LinkId, after.LinkId); Assert.Equal(before.RouteIndex, after.RouteIndex);
        Assert.Equal(accepted, after.NominalMinutes); Assert.Equal(accepted, lease.ObservedMinutes);
        Assert.False(after.IsCompleted); Assert.Equal(1, simulation.Traffic.VehicleCount);
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(next, lease.ObservedMinutes, normalized + .001, before.MaximumNominalMinutes, out _));
    }

    [Fact]
    public void NormalizedBoundaryCannotExcuseMotionRegressedPermissionOrAnUnrelatedCoordinate()
    {
        var (simulation, citizen) = Journey();
        var sample = simulation.SampleJourney(citizen, Math.BitDecrement(10));
        double observed = Math.BitDecrement(10);
        Assert.True(PhysicalTrafficCornerPermission.IsNormalizedBoundary(sample, observed));
        Assert.False(PhysicalTrafficCornerPermission.IsNormalizedBoundary(sample, observed - 1e-6));
        Assert.False(PhysicalTrafficCornerPermission.IsNormalizedBoundary(sample, double.NaN));
        Assert.False(PhysicalTrafficCornerPermission.IsNormalizedBoundary(simulation.SampleJourney(citizen, 10.1), observed));
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, observed, sample.ElapsedMinutes, Math.BitDecrement(observed), out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, observed, double.PositiveInfinity, observed, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, observed, sample.ElapsedMinutes + .000001, observed, out _));
    }
    [Fact]
    public void ActualSamplerRoundedNormalizationRemainsAnUnmovedBoundaryOnly()
    {
        // SampleJourney's sampledElapsed + leg.Minutes - remaining rounds twice. This real
        // three-leg route lands just outside its unrounded relative normalization envelope.
        var (simulation, citizen) = Journey(prefix: .2581134832421572, minutes: 407.51952228060867);
        const double rawObserved = 407.7776357638468;
        var sample = simulation.SampleJourney(citizen, rawObserved);
        Assert.Equal(2, sample.LegIndex); Assert.Equal(0, sample.LegFraction);
        Assert.Equal(407.77763576385087, sample.ElapsedMinutes);
        Assert.True(sample.ElapsedMinutes - rawObserved > System.Math.Max(1e-12, rawObserved * 1e-14));
        Assert.True(PhysicalProgress.TryProject(sample, sample.From, false, out double projected));
        Assert.Equal(sample.ElapsedMinutes, projected);
        Assert.True(PhysicalTrafficCornerPermission.IsNormalizedBoundary(sample, rawObserved));
        Assert.True(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, rawObserved,
            projected, rawObserved, out double accepted));
        Assert.Equal(rawObserved, accepted); // No clock, section, queue or accounting credit is added.
        var moved = new Position(sample.From.X, sample.From.Y, sample.From.Z + .01);
        Assert.True(PhysicalProgress.TryProject(sample, moved, false, out double movedProjection));
        Assert.True(movedProjection > projected);
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, rawObserved,
            movedProjection, rawObserved, out _));
        Assert.False(PhysicalTrafficCornerPermission.TryClampNormalizedBoundary(sample, rawObserved,
            projected, System.Math.BitDecrement(rawObserved), out _));
    }

}
