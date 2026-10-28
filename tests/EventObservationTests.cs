using FoodDemandAuditProbe;
using Xunit;

namespace PersistentCity.Tests;

public sealed class EventObservationTests
{
    private static EventWork Slice(string id, long ticks, long sequence = 0) => new()
    { Citizen = id, Branch = "routine", Minute = 12, Ticks = ticks, Slices = 1, Plans = 1, Sequence = sequence };

    [Fact]
    public void ResumedEventWinsByFrameWorkWithoutSuspendedTime()
    {
        var log = new EventWorkAccumulator(4);
        log.Add(Slice("a", 4)); log.Add(Slice("b", 7)); log.Add(Slice("a", 5));
        Assert.Equal("a", log.Slowest.Citizen);
        Assert.Equal(9, log.Slowest.Ticks); Assert.Equal(2, log.Slowest.Slices); Assert.Equal(2, log.Slowest.Plans);
        var retained = log.Slowest;
        log.Clear(); log.Add(Slice("c", 2));
        Assert.Equal("a", retained.Citizen); Assert.Equal(9, retained.Ticks);
        Assert.Equal(2, log.Slowest.Ticks);
    }

    [Fact]
    public void DistinctFoodCallsAtSameMinuteAreNotOneEvent()
    {
        var log = new EventWorkAccumulator(3);
        log.Add(Slice("", 5, 1)); log.Add(Slice("", 6, 2));
        Assert.Equal(6, log.Slowest.Ticks); Assert.Equal(1, log.Slowest.Slices);
    }

    [Fact]
    public void CapacityDropsNewEventsButContinuesKnownEventsAndClearsFailures()
    {
        var log = new EventWorkAccumulator(1);
        log.Add(Slice("a", 5)); log.Add(Slice("b", 100));
        var failure = Slice("a", 2); failure.Failures = 1; failure.NavMeshTicks = 1;
        log.Add(failure);
        Assert.Equal(1, log.Dropped); Assert.Equal(7, log.Slowest.Ticks);
        Assert.Equal(1, log.Slowest.Failures); Assert.Equal(1, log.Slowest.NavMeshTicks);
        log.Clear(); Assert.Equal(0, log.Dropped); Assert.Equal(0, log.Slowest.Failures);
    }

    [Fact]
    public void HeapShrinkIsValidButAllocationResetIsNot()
    {
        Assert.True(ManagedDelta.Between(100, 80, false).Available);
        Assert.Equal(-20, ManagedDelta.Between(100, 80, false).Value);
        Assert.False(ManagedDelta.Between(100, 80, true).Available);
        Assert.False(ManagedDelta.Between(-1, 80, false).Available);
        Assert.True(ManagedDelta.Between(100, 100, true).Available);
    }
}
