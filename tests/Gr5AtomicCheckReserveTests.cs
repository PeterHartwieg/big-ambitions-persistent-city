using System;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5AtomicCheckReserveTests
{
    [Fact]
    public void ALongRecheckWaitsForASliceWithRoomInsteadOfRunningPastANearlySpentDeadline()
    {
        var reserve = new AtomicCheckReserve(10);
        int waits = 0; long waited = long.MinValue;
        Assert.False(reserve.Admits(now: 1000, deadline: 1000 + 5109, items: 511, frame: 1, ref waits, ref waited));
        Assert.Equal(1, waits);
        Assert.False(reserve.Admits(now: 2000, deadline: 2000, items: 511, frame: 2, ref waits, ref waited));
        Assert.True(reserve.Admits(now: 3000, deadline: 3000 + 5110, items: 511, frame: 3, ref waits, ref waited));
        Assert.Equal(0, waits);
        Assert.Equal(2, reserve.Deferred);
        Assert.Equal(0, reserve.Forced);
    }

    [Fact]
    public void AnUnlimitedCallAlwaysRunsTheRecheck()
    {
        var reserve = new AtomicCheckReserve(10);
        int waits = 0; long waited = long.MinValue;
        Assert.True(reserve.Admits(now: 5, deadline: long.MaxValue, items: 511, frame: 1, ref waits, ref waited));
        Assert.Equal(0, reserve.Deferred);
    }

    [Fact]
    public void ARecheckThatNeverFitsRunsAfterTheWaitLimitSoTheRouteCannotStall()
    {
        var reserve = new AtomicCheckReserve(10, maximumWaits: 3);
        int waits = 0, calls = 0; long waited = long.MinValue;
        while (!reserve.Admits(now: 0, deadline: 100, items: 511, frame: calls, ref waits, ref waited)) calls++;
        Assert.Equal(3, calls);
        Assert.Equal(1, reserve.Forced);
        Assert.Equal(0, waits);
    }

    [Fact]
    public void RetriesInOneUpdateCountAsOneWaitSoALongRecheckStaysDeferredInThatUpdate()
    {
        var reserve = new AtomicCheckReserve(10, maximumWaits: 8);
        int waits = 0; long waited = long.MinValue;
        for (int retry = 0; retry < 50; retry++)
            Assert.False(reserve.Admits(now: 0, deadline: 100, items: 511, frame: 7, ref waits, ref waited));
        Assert.Equal(1, waits);
        Assert.Equal(0, reserve.Forced);
        long frame = 8;
        while (!reserve.Admits(now: 0, deadline: 100, items: 511, frame: frame, ref waits, ref waited))
            Assert.False(reserve.Admits(now: 0, deadline: 100, items: 511, frame: frame++, ref waits, ref waited));
        Assert.Equal(15, frame);
        Assert.Equal(1, reserve.Forced);
    }

    [Fact]
    public void TheReserveGrowsToTheMeasuredCostAndDecaysTowardsTheFloor()
    {
        var reserve = new AtomicCheckReserve(10);
        reserve.Record(ticks: 511 * 40, items: 511);
        Assert.Equal(50, reserve.TicksPerItem, 6);
        int waits = 0; long waited = long.MinValue;
        Assert.False(reserve.Admits(now: 0, deadline: 511 * 40, items: 511, frame: 1, ref waits, ref waited));
        for (int i = 0; i < 200; i++) reserve.Record(ticks: 0, items: 511);
        Assert.Equal(10, reserve.TicksPerItem, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AtomicCheckReserve(0));
    }

    [Fact]
    public void AdmittingAndRecordingAllocateNothing()
    {
        var reserve = new AtomicCheckReserve(10);
        int waits = 0; long waited = long.MinValue;
        reserve.Admits(0, 1, 511, 0, ref waits, ref waited); reserve.Record(100, 511);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { reserve.Admits(i, i + 3000, 511, i, ref waits, ref waited); reserve.Record(i, 511); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
