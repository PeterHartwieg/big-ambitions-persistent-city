using System;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalProgressTests
{
    private sealed class Planner : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }
    private static PhysicalJourneySample Sample(double elapsed = 0, TravelMode mode = TravelMode.Walk, bool wait = false)
    {
        var route = new Route { Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(wait ? 0 : 10,0,0), Minutes = 10, Mode = mode },
            new RouteLeg { From = new Position(wait ? 0 : 10,0,0), To = new Position(wait ? 0 : 10,0,10), Minutes = 10, Mode = mode }
        } };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "p" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = 20 };
        var state = new CityState { Minute = elapsed, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = route.Legs[0].From }, new Place { Id = "b", Position = route.Legs[1].To } } };
        return new CitySimulator(state, new Planner()).SampleJourney(citizen, elapsed);
    }

    private static PhysicalJourneySample Vertical(double elapsed = 0, TravelMode mode = TravelMode.Walk, double height = 4)
    {
        var route = new Route { Legs = new()
        { new RouteLeg { From = new Position(2,0,3), To = new Position(2,height,3), Minutes = 8, Mode = mode } } };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "vertical" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = 8 };
        var state = new CityState { Minute = elapsed, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = route.Legs[0].From }, new Place { Id = "b", Position = route.Legs[0].To } } };
        return new CitySimulator(state, new Planner()).SampleJourney(citizen, elapsed);
    }

    [Fact]
    public void VerticalWalkerAtSourceCanInitializeTargetWithoutInventingProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(), new Position(2,0,3), false, out double progress));
        Assert.Equal(0, progress);
    }

    [Fact]
    public void ShortVerticalLegAtSourceDoesNotReceiveEndpointToleranceCredit()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(height: .02), new Position(2,0,3), false, out double progress));
        Assert.Equal(0, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(height: .02), new Position(2,.01,3), false, out progress));
        Assert.Equal(4, progress);
    }

    [Fact]
    public void VerticalWalkerCreditsMeasuredHeightAndNeverRegresses()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(), new Position(2,1,3), false, out double progress));
        Assert.Equal(2, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(4), new Position(2,1,3), false, out progress));
        Assert.Equal(4, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(4), new Position(2,3,3), false, out progress));
        Assert.Equal(6, progress);
    }

    [Fact]
    public void VerticalWalkerEndpointCompletesOnlyMeasuredLeg()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(2), new Position(2,4,3), false, out double progress));
        Assert.Equal(8, progress);
    }

    [Theory]
    [InlineData(2.081, 2, 3)] [InlineData(2, 4.081, 3)] [InlineData(2, -0.081, 3)]
    public void VerticalWalkerRejectsOffSegmentBodiesEvenWithTrustedCorner(double x, double y, double z)
    {
        Assert.False(PhysicalProgress.TryProject(Vertical(2), new Position(x,y,z), true, out double progress));
        Assert.Equal(2, progress);
    }

    [Fact]
    public void VerticalCarsCannotUseWalkingProjectionFallback()
    {
        Assert.False(PhysicalProgress.TryProject(Vertical(2, TravelMode.Car), new Position(2,4,3), true, out double progress));
        Assert.Equal(2, progress);
    }

    [Fact]
    public void StationaryMovingActorNeverGetsElapsedBasedProgress()
    {
        var sample = Sample(4);
        for (int frame = 0; frame < 100; frame++)
        { Assert.True(PhysicalProgress.TryProject(sample, new Position(4,0,0), false, out double progress)); Assert.Equal(4, progress); }
    }

    [Fact]
    public void WalkingBackwardsCannotRegressCommittedProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(6), new Position(2,0,0), false, out double progress));
        Assert.Equal(6, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(6), new Position(-3,0,0), false, out progress)); Assert.Equal(6, progress);
    }

    [Theory]
    [InlineData(TravelMode.Walk, .36)] [InlineData(TravelMode.Car, 2.01)]
    public void AdjacentParallelRoadIsRejectedEvenWhenPastEndpoint(TravelMode mode, double offset)
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4, mode), new Position(12,0,offset), false, out double progress));
        Assert.Equal(4, progress);
    }

    [Fact]
    public void CurvedNextSegmentCannotShortcutTheCurrentCorner()
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(8,0,3), false, out double progress)); Assert.Equal(4, progress);
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(10,0,3), false, out progress)); Assert.Equal(4, progress);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VerticalSeparationRejectsProjectionAndTrustedCorner(bool trusted)
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(10,2.01,0), trusted, out double progress)); Assert.Equal(4, progress);
    }

    [Fact]
    public void ActualEndpointAndAlignedForwardOvershootCompleteOnlyCurrentLeg()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(10,0,0), false, out double progress)); Assert.Equal(10, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(14,0,.2), false, out progress)); Assert.Equal(10, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(4, TravelMode.Car), new Position(18,0,1.9), false, out progress)); Assert.Equal(10, progress);
    }

    [Theory]
    [InlineData(TravelMode.Walk, 9.93)] [InlineData(TravelMode.Car, 8.6)]
    public void NearCornerUsesNativeActorDistanceTolerance(TravelMode mode, double x)
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4, mode), new Position(x,0,0), false, out double progress)); Assert.Equal(10, progress);
    }

    [Fact]
    public void TrustedNativeCornerCanCompleteAfterVehicleTurnsAway()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4, TravelMode.Car), new Position(10,0,3), true, out double progress));
        Assert.Equal(10, progress);
    }

    [Fact]
    public void CrossTrackToleranceAllowsSmallOffsetsWithoutInflatingProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(7,0,.3), false, out double progress)); Assert.Equal(7, progress, 10);
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(7,2,0), false, out progress)); Assert.Equal(7, progress, 10);
    }

    [Fact]
    public void WaitConsumesOnlyNonnegativeSimulationElapsedAndStopsAtBoundary()
    {
        var sample = Sample(4, wait: true);
        Assert.False(PhysicalProgress.TryProject(sample, new Position(0,0,0), false, out double progress)); Assert.Equal(4, progress);
        Assert.Equal(4, PhysicalProgress.AdvanceWait(sample, 0)); Assert.Equal(6, PhysicalProgress.AdvanceWait(sample, 2));
        Assert.Equal(10, PhysicalProgress.AdvanceWait(sample, 100));
        Assert.Throws<ArgumentException>(() => PhysicalProgress.AdvanceWait(Sample(4), 1));
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidWaitElapsedCannotAdvance(double elapsed)
    { Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalProgress.AdvanceWait(Sample(4, wait: true), elapsed)); }

    [Fact]
    public void InvalidCoordinatesAndEmptySampleCannotBecomeProgress()
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(double.NaN,0,0), false, out double progress)); Assert.Equal(4, progress);
        Assert.False(PhysicalProgress.TryProject(default, new Position(0,0,0), true, out progress));
    }
}
