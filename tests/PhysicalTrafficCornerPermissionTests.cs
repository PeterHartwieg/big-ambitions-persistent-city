using System;
using System.Collections.Generic;
using System.Reflection;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

// F2d: the corner permission only shapes the visible turn. It lets Gley take a visible car past its corner once the
// kernel's coordinate (the cap) reaches it, and the follower passes a corner only with Gley's reached evidence.
// Nothing here credits logical progress.
public sealed class PhysicalTrafficCornerPermissionTests
{
    private sealed class Planner : IRoutePlanner
    { public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null; }

    private static (CitySimulator Simulator, CitizenState Citizen) Journey(double prefix = 0, double minutes = 10,
        Position? from = null, Position? to = null, TravelMode mode = TravelMode.Car)
    {
        Position a = from ?? new Position(0, 0, 0), b = to ?? new Position(10, 0, 0);
        var legs = new List<RouteLeg>();
        if (prefix > 0) legs.Add(new RouteLeg { From = a, To = a, Mode = TravelMode.Walk, Minutes = prefix });
        // Native road legs (F2d: a car body drives only those).
        legs.Add(new RouteLeg { From = a, To = b, Mode = mode, Minutes = minutes, ServiceId = mode == TravelMode.Car ? "native:road:v1:t:0:1" : "" });
        legs.Add(new RouteLeg { From = b, To = new Position(b.X, b.Y, b.Z + 10), Mode = TravelMode.Car, Minutes = 10, ServiceId = "native:road:v1:t:1:2" });
        var route = new Route { Legs = legs };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "corner-permission" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", NextEventMinute = route.Minutes };
        var state = new CityState { Minute = route.Minutes, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = a }, new Place { Id = "b", Position = legs[legs.Count - 1].To } } };
        return (new CitySimulator(state, new Planner()), citizen);
    }

    private static PhysicalJourneySample Captured(out Position marker, out double maximum)
    {
        const double elapsed = 115.74062969885975, fraction = .63410704080244773, minutes = .37883332483901316;
        maximum = 115.87924214512763;
        var segment = new RouteLeg { From = new Position(221.41065979003906, 1.1920928955078125E-07, -101.51970672607422),
            To = new Position(221.40960693359375, 1.1920928955078125E-07, -105.72896575927734), Mode = TravelMode.Car, Minutes = minutes };
        // Preserve the exact native elapsed/fraction pair; rebuilding its entire preceding route
        // from one summed prefix would change that fraction by several ulps before this test runs.
        var point = Position.Lerp(segment.From, segment.To, fraction);
        marker = new Position(221.40997314453125, .0099703343585133553, -104.24519348144531);
        return (PhysicalJourneySample)typeof(PhysicalJourneySample).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
            .Invoke(new object?[] { elapsed, 200.0, 263, fraction, segment, point, (Position?)point, (TravelMode?)TravelMode.Car });
    }

    [Fact]
    public void CapturedNativeCornerArithmeticLetsGleyTurnOnceTheKernelReachesTheCorner()
    {
        var sample = Captured(out var marker, out double maximum);
        Assert.InRange(Position.Distance(marker, sample.To), 1.48, 1.5);
        Assert.True(PhysicalProgress.TryProject(sample, marker, true, out double endpoint));
        Assert.Equal(115.87924214512775, endpoint);
        // The kernel's coordinate is a few ulps short of the committed corner; the native predicate's
        // one-nanominute allowance still lets Gley start the turn.
        Assert.True(endpoint > maximum);
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, maximum));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, maximum - 1e-6));
    }

    [Fact]
    public void OnlyTheExistingNativePredicateAllowanceReachesTheCorner()
    {
        var (simulation, citizen) = Journey();
        var sample = simulation.SampleJourney(citizen, 9);
        double endpoint = sample.ElapsedMinutes + sample.RemainingLegMinutes;
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint - 5e-10));
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint));
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint + 5));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint - 2e-9));
        // A cap behind the car's own progress never reaches anything.
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, sample.ElapsedMinutes - 1e-6));
    }

    [Theory]
    [InlineData(1e-8)] [InlineData(.01)] [InlineData(1)]
    public void MaterialEndpointGapsRemainRefusedEvenWithNativeReceipt(double gap)
    {
        var (simulation, citizen) = Journey(); var sample = simulation.SampleJourney(citizen, 9);
        const double endpoint = 10;
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, endpoint - gap));
    }

    [Fact]
    public void NonfiniteWrongModeAndRegressedPermissionCannotBorrowCornerAllowance()
    {
        var (simulation, citizen) = Journey(); var sample = simulation.SampleJourney(citizen, 9);
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(default, 10));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, double.NaN));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, double.PositiveInfinity));
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(sample, 8));
        var (walkSimulation, walker) = Journey(mode: TravelMode.Walk);
        Assert.False(PhysicalTrafficCornerPermission.CanReachEndpoint(walkSimulation.SampleJourney(walker, 9), 10));
    }

    /// <summary>The follower at the corner: without Gley's reached evidence it approaches, strictly before the corner;
    /// with it, it crosses only as far as the cap. A cap an ulp short of the corner keeps it there until the kernel
    /// moves on, and then it crosses without a new receipt.</summary>
    [Fact]
    public void TheFollowerPassesACornerOnlyWithReachedEvidenceAndNeverBeyondTheCap()
    {
        var (simulation, citizen) = Journey();
        var route = citizen.Journey!;
        var corner = route.Legs[0].To;
        // Standing on the corner without evidence: the approach is credited, the corner is not.
        double approach = CarFollow.Advance(route, 9, 15, corner, -1);
        Assert.True(approach < 10 && approach > 9.99);
        Assert.Equal(0, WalkerFollow.Sample(route, approach).LegIndex);
        // With the evidence it crosses and keeps projecting on the next leg, capped at the kernel's coordinate.
        var beyond = new Position(10, 0, 2);
        Assert.Equal(12, CarFollow.Advance(route, 9, 12, beyond, 0), 9);
        Assert.Equal(1, WalkerFollow.Sample(route, CarFollow.Advance(route, 9, 12, beyond, 0)).LegIndex);
        // A cap an ulp short of the corner holds the car there, whatever the evidence. The sampler normalizes that
        // point onto the next leg's start, where the command allows no advance.
        double held = CarFollow.Advance(route, 9, Math.BitDecrement(10.0), corner, 0);
        Assert.Equal(Math.BitDecrement(10.0), held);
        Assert.Equal(0, PhysicalTrafficMotion.MaximumAdvance(WalkerFollow.Sample(route, held), corner, held));
        // Once the kernel moves on, the consumed evidence still lets it cross.
        Assert.Equal(1, WalkerFollow.Sample(route, CarFollow.Advance(route, held, 10.5, corner, 0)).LegIndex);
        // Never beyond the cap, and never behind its own progress.
        Assert.Equal(9.5, CarFollow.Advance(route, 9, 9.5, corner, 0));
        Assert.Equal(9, CarFollow.Advance(route, 9, 15, new Position(0, 0, 0), -1));
    }
}
