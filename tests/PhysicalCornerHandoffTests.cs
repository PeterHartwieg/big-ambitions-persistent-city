using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalCornerHandoffTests
{

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
        return new CitySimulator(state, new NoRoutes()).SampleJourney(citizen, elapsed);
    }

    [Theory]
    [InlineData(false, 9, .5)]
    [InlineData(true, 9, 0)]
    public void UnmovedNativeCornerReceiptHoldsTheTurnUntilTheKernelReachesIt(bool obtuse, double x, double z)
    {
        var marker = new Position(x,0,z);
        var previous = Sample(9, obtuse); var next = Sample(10, obtuse);
        Assert.True(PhysicalProgress.TryProject(previous, marker, true, out double corner));
        Assert.Equal(10, corner);
        Assert.True(PhysicalProgress.TryProject(next, marker, false, out double reprojected));
        Assert.True(reprojected > 10.1);
        var handoff = new PhysicalCornerHandoff(); handoff.Begin(previous, next, marker);
        for (int frame = 0; frame < 100; frame++)
        {
            Assert.True(handoff.MustHold(next, 10.1));
            Assert.Equal(0, PhysicalTrafficMotion.MaximumAdvance(next, marker, 10.1));
        }
        Assert.False(handoff.MustHold(next, reprojected + 1e-6));
        Assert.False(handoff.MustHold(next, 10.1));
    }

    [Fact]
    public void OpenedReceivingPermissionReleasesHoldForGood()
    {
        var handoff = new PhysicalCornerHandoff(); var sample = Sample(10);
        handoff.Begin(Sample(9), sample, new Position(9,0,.5));
        Assert.True(handoff.MustHold(sample, 10.1));
        Assert.False(handoff.MustHold(sample, 10.5));
        Assert.False(handoff.MustHold(sample, 10.1));
    }

    [Fact]
    public void DistantReceiptAndUnrelatedLegNeverHold()
    {
        var handoff = new PhysicalCornerHandoff();
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,2));
        Assert.False(handoff.MustHold(Sample(10), 10));
        handoff.Begin(Sample(9), Sample(20), new Position(9,0,.5));
        Assert.False(handoff.MustHold(Sample(20), 20));
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,.5));
        Assert.False(handoff.MustHold(Sample(20), 20));
    }

    [Fact]
    public void NonfiniteMarkerOrPermissionCannotHold()
    {
        var handoff = new PhysicalCornerHandoff();
        handoff.Begin(Sample(9), Sample(10), new Position(double.NaN,0,.5));
        Assert.False(handoff.MustHold(Sample(10), 10));
        handoff.Begin(Sample(9), Sample(10), new Position(9,0,.5));
        Assert.False(handoff.MustHold(Sample(10), double.NaN));
    }
}
