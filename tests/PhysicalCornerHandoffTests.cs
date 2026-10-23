using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalCornerHandoffTests
{
    private sealed class Planner : IRoutePlanner
    { public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null; }

    private static PhysicalJourneySample Sample(double elapsed, bool obtuse = false)
    {
        var route = new Route { Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(10,0,0), Mode = TravelMode.Car, Minutes = 10 },
            new RouteLeg { From = new Position(10,0,0), To = new Position(obtuse ? 0 : 10,0,10), Mode = TravelMode.Car, Minutes = obtuse ? 20 : 10 },
            new RouteLeg { From = new Position(obtuse ? 0 : 10,0,10), To = new Position(20,0,10), Mode = TravelMode.Car, Minutes = 10 }
        } };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "corner" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", NextEventMinute = route.Minutes };
        var state = new CityState { Minute = elapsed, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a" }, new Place { Id = "b", Position = new Position(20,0,10) } } };
        return new CitySimulator(state, new Planner()).SampleJourney(citizen, elapsed);
    }

    [Theory]
    [InlineData(false, 9, .5)]
    [InlineData(true, 9, 0)]
    public void UnmovedNativeCornerReceiptCannotBecomeAnUnpermittedSecondMovement(bool obtuse, double x, double z)
    {
        var marker = new Position(x,0,z);
        var previous = Sample(9, obtuse); var next = Sample(10, obtuse);
        Assert.True(PhysicalProgress.TryProject(previous, marker, true, out double corner));
        Assert.Equal(10, corner);
        Assert.True(PhysicalProgress.TryProject(next, marker, false, out double reprojected));
        Assert.True(reprojected > 10.1); // The old observer discarded the unmoved body here.
        var handoff = new PhysicalCornerHandoff(); handoff.Begin(previous, next, marker);
        Assert.True(handoff.MustHold(next, 10.1));
        Assert.Equal(0, PhysicalTrafficMotion.MaximumAdvance(next, marker, 10.1));
        for (int frame = 0; frame < 100; frame++)
        {
            Assert.True(handoff.TryHold(Sample(10.1, obtuse), marker, 10.1, out double accepted));
            Assert.Equal(10.1, accepted); // No elapsed frames, queue advance or extra capacity invented.
        }
    }

    [Fact]
    public void ReceiptDoesNotExcuseNewForwardMotionEvenWhenObservationWasDelayed()
    {
        var handoff = new PhysicalCornerHandoff();
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,.5));
        Assert.False(handoff.TryHold(Sample(10), new Position(9,0,.51), 10.1, out _));
        Assert.False(handoff.TryHold(Sample(10), new Position(9,0,.561434), 10.1, out _));
        Assert.True(handoff.TryHold(Sample(10), new Position(9,0,.5), 10.1, out double accepted));
        Assert.Equal(10.1, accepted);
    }

    [Fact]
    public void OpenedReceivingPermissionReleasesHoldAndCannotLeaveAnOvershootExemption()
    {
        var handoff = new PhysicalCornerHandoff(); var sample = Sample(10);
        handoff.Begin(Sample(9), sample, new Position(9,0,.5));
        Assert.True(handoff.MustHold(sample, 10.1));
        Assert.False(handoff.MustHold(sample, 10.5));
        Assert.False(handoff.TryHold(sample, new Position(9,0,.6), 10.1, out _));
    }

    [Fact]
    public void DistantReceiptAndUnrelatedLegNeverReceiveCornerExemption()
    {
        var handoff = new PhysicalCornerHandoff();
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,2));
        Assert.False(handoff.MustHold(Sample(10), 10));
        handoff.Begin(Sample(9), Sample(20), new Position(9,0,.5));
        Assert.False(handoff.MustHold(Sample(20), 20));
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,.5));
        Assert.False(handoff.TryHold(Sample(20), new Position(10,0,10), 20, out _));
    }

    [Fact]
    public void NonfiniteMeasurementOrRegressedPermissionCannotHold()
    {
        var handoff = new PhysicalCornerHandoff();
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,.5));
        Assert.False(handoff.TryHold(Sample(10), new Position(double.NaN,0,.5), 10, out _));
        Assert.False(handoff.TryHold(Sample(10.1), new Position(9,0,.5), 10, out _));
    }

    [Fact]
    public void NormalizedNextLegRetainsExactRawLeaseAccountingDuringCornerHold()
    {
        double credited = System.Math.BitDecrement(10);
        var next = Sample(10); var marker = new Position(9,0,.5);
        var handoff = new PhysicalCornerHandoff(); handoff.Begin(Sample(9), next, marker);
        Assert.True(handoff.TryHold(next, marker, credited, credited, out double held));
        Assert.Equal(credited, held);
        Assert.False(handoff.TryHold(next, new Position(9,0,.51), credited, credited, out _));
        Assert.False(handoff.TryHold(next, marker, credited, 10, out _));
    }
}
