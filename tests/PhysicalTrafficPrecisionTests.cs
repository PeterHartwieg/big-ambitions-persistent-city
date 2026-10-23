using System;
using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalTrafficPrecisionTests
{
    private sealed class Planner : IRoutePlanner
    { public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null; }

    private static Position P(double x, double z = 20, double y = 0) => new Position(x, y, z);
    private static PhysicalJourneySample Sample(Position from, Position to, double fraction = .5, double minutes = 10, double prefix = 0)
    {
        var legs = new List<RouteLeg>();
        if (prefix > 0) legs.Add(new RouteLeg { From = from, To = from, Mode = TravelMode.Walk, Minutes = prefix });
        legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Car, Minutes = minutes });
        var route = new Route { Legs = legs };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "precision-car" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = route.Minutes };
        var state = new CityState { Minute = prefix + minutes * fraction, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = from }, new Place { Id = "b", Position = to } } };
        return new CitySimulator(state, new Planner()).SampleJourney(citizen, state.Minute);
    }

    private static double Project(PhysicalJourneySample sample, Position actual)
    {
        Assert.True(PhysicalProgress.TryProject(sample, actual, false, out double projected));
        return projected;
    }

    [Theory]
    [InlineData(221.35087585449219,89.96954345703125,221.34933471679688,85.6143569946289,221.35012817382813,87.86065673828125,.48422342838348736,.39196680615700624,27.593172434050597,0)]
    [InlineData(221.34068298339844,61.195777893066406,221.33920288085938,57.044242858886719,221.34063720703125,61.091793060302734,.025046581586625909,.3736381768220044,30.00237034570749,0)]
    [InlineData(221.33770751953125,52.764442443847656,221.33612060546875,48.317058563232422,221.3375244140625,52.27056884765625,.11104739711969404,.40026457473626736,7.8895643026841382,0)]
    [InlineData(134.06588745117188,89.980949401855469,134.06396484375,85.626792907714844,134.06509399414063,88.169029235839844,.41613357566248066,.39187412267497179,77.828225217848924,.36)]
    [InlineData(308.70150756835938,-48.526737213134766,308.7001953125,-55.731487274169922,308.70071411132813,-52.893112182617188,.60604006763096618,.64842751624866468,12.253068857973847,0)]
    public void CapturedFloatMarkerPlacementClampsToExistingPermission(double ax, double az, double bx, double bz,
        double x, double z, double fraction, double minutes, double elapsed, double height)
    {
        var sample = Sample(P(ax, az), P(bx, bz), fraction, minutes, elapsed - fraction * minutes);
        double maximum = sample.ElapsedMinutes;
        double projected = Project(sample, P(x, z, height));
        Assert.True(projected > maximum + 1e-7);
        Assert.True(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, P(x, z, height), projected, maximum, out double accepted));
        Assert.Equal(maximum, accepted);
        Assert.Equal(sample.ElapsedMinutes, accepted);
    }

    [Theory]
    [InlineData(.001)] [InlineData(10)] [InlineData(1000000)]
    public void FloatPlacementAllowanceIsSpatialAcrossLegDurations(double minutes)
    {
        var sample = Sample(P(100), P(110), .5, minutes);
        var actual = P((float)(105 + .000005));
        double projected = Project(sample, actual);
        Assert.True(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, projected, sample.ElapsedMinutes, out double accepted));
        Assert.Equal(sample.ElapsedMinutes, accepted);
        Assert.True(projected > accepted);
    }

    [Fact]
    public void LargeNominalClockDoesNotTurnAllowanceIntoQueueCredit()
    {
        var sample = Sample(P(100), P(110), .5, 1000000, 100000000);
        var actual = P((float)(105 + .000005));
        Assert.True(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, Project(sample, actual), sample.ElapsedMinutes, out double accepted));
        Assert.Equal(sample.ElapsedMinutes, accepted);
    }

    [Theory]
    [InlineData(100)] [InlineData(1000)] [InlineData(1000000)]
    public void MaterialOverrunNeverBecomesRoundingAtLargeCoordinates(double origin)
    {
        var sample = Sample(P(origin), P(origin + 10));
        var actual = P(origin + 5.01);
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, Project(sample, actual), sample.ElapsedMinutes, out _));
    }

    [Fact]
    public void CoarseFloatCoordinatesFailClosedEvenForSmallClaimedExcess()
    {
        Assert.False(PhysicalTrafficPrecision.TryGetHorizontalAllowance(P(1000000), P(1000000), out _));
        var sample = Sample(P(1000000), P(1000010));
        var actual = P(1000005.000005);
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, Project(sample, actual), sample.ElapsedMinutes, out _));
    }

    [Fact]
    public void PositionAllowanceCannotHideParallelRoadOrInventedCornerCredit()
    {
        var sample = Sample(P(100), P(110));
        var beside = P(105.000005, 20.1);
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, beside, Project(sample, beside), sample.ElapsedMinutes, out _));
        var near = P(105.000005);
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, near, sample.ElapsedMinutes + .01, sample.ElapsedMinutes, out _));
        var endpoint = Sample(P(100), P(110), .99);
        Assert.True(PhysicalProgress.TryProject(endpoint, P(109.9), true, out double corner));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(endpoint, P(109.9), corner, endpoint.ElapsedMinutes, out _));
    }

    [Fact]
    public void NonfiniteOrUnsubstantiatedGeometryCannotUseAllowance()
    {
        var sample = Sample(P(100), P(110));
        var actual = P(105.000005);
        double projected = Project(sample, actual);
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(default, actual, projected, sample.ElapsedMinutes, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, P(double.NaN), projected, sample.ElapsedMinutes, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, double.PositiveInfinity, sample.ElapsedMinutes, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, projected, double.NaN, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, P(105.000005, 20, 2.01), projected, sample.ElapsedMinutes, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, projected, sample.ElapsedMinutes - .001, out _));
        Assert.False(PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, sample.ElapsedMinutes, sample.ElapsedMinutes, out _));
    }

    [Fact]
    public void HoldPositionIsExplicitAndDefaultsOff()
    {
        Assert.False(new PhysicalTrafficCommandLimit(P(1), 0).HoldPosition);
        Assert.True(new PhysicalTrafficCommandLimit(P(1), 0, true).HoldPosition);
    }
}
