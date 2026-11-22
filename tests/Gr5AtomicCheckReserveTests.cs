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
        int waits = 0;
        Assert.False(reserve.Admits(now: 1000, deadline: 1000 + 5109, items: 511, ref waits));
        Assert.Equal(1, waits);
        Assert.False(reserve.Admits(now: 2000, deadline: 2000, items: 511, ref waits));
        Assert.True(reserve.Admits(now: 3000, deadline: 3000 + 5110, items: 511, ref waits));
        Assert.Equal(0, waits);
        Assert.Equal(2, reserve.Deferred);
        Assert.Equal(0, reserve.Forced);
    }

    [Fact]
    public void AnUnlimitedCallAlwaysRunsTheRecheck()
    {
        var reserve = new AtomicCheckReserve(10);
        int waits = 0;
        Assert.True(reserve.Admits(now: 5, deadline: long.MaxValue, items: 511, ref waits));
        Assert.Equal(0, reserve.Deferred);
    }

    [Fact]
    public void ARecheckThatNeverFitsRunsAfterTheWaitLimitSoTheRouteCannotStall()
    {
        var reserve = new AtomicCheckReserve(10, maximumWaits: 3);
        int waits = 0, calls = 0;
        while (!reserve.Admits(now: 0, deadline: 100, items: 511, ref waits)) calls++;
        Assert.Equal(3, calls);
        Assert.Equal(1, reserve.Forced);
        Assert.Equal(0, waits);
    }

    [Fact]
    public void TheReserveGrowsToTheMeasuredCostAndDecaysTowardsTheFloor()
    {
        var reserve = new AtomicCheckReserve(10);
        reserve.Record(ticks: 511 * 40, items: 511);
        Assert.Equal(50, reserve.TicksPerItem, 6);
        int waits = 0;
        Assert.False(reserve.Admits(now: 0, deadline: 511 * 40, items: 511, ref waits));
        for (int i = 0; i < 200; i++) reserve.Record(ticks: 0, items: 511);
        Assert.Equal(10, reserve.TicksPerItem, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AtomicCheckReserve(0));
    }

    [Fact]
    public void AdmittingAndRecordingAllocateNothing()
    {
        var reserve = new AtomicCheckReserve(10);
        int waits = 0;
        reserve.Admits(0, 1, 511, ref waits); reserve.Record(100, 511);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { reserve.Admits(i, i + 3000, 511, ref waits); reserve.Record(i, 511); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
