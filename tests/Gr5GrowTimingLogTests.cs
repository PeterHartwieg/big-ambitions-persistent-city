using System;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5GrowTimingLogTests
{
    private static GrowTiming Op(int frame, GrowKind kind, double total, double result = 0, double checks = 0, double parent = 0, double own = 0) =>
        new GrowTiming { Frame = frame, Kind = kind, Total = total, Result = result, Checks = checks, Parent = parent, Own = own };

    [Fact]
    public void EachSlowGrowKeepsItsFrameKindAndSteps()
    {
        var log = new GrowTimingLog();
        log.Record(Op(100, GrowKind.AsyncStart, 0.4, result: 0.4));
        log.Record(Op(140, GrowKind.AsyncFinish, 58.3, result: 0.2, checks: 0.1, parent: 55.0, own: 3.0));
        log.Record(Op(141, GrowKind.SyncInstantiate, 12.5, result: 11.0, checks: 0.5, parent: 0.5, own: 0.5));
        Assert.Equal(3, log.Recorded);
        Assert.Equal(2, log.SlowCount);
        Assert.Equal(140, log.Slow(0).Frame);
        Assert.Equal(GrowKind.AsyncFinish, log.Slow(0).Kind);
        Assert.Equal(55.0, log.Slow(0).Parent);
        Assert.Equal(141, log.Slow(1).Frame);
        Assert.Equal(GrowKind.SyncInstantiate, log.Slow(1).Kind);
        Assert.Equal(140, log.Slowest.Frame);
        string report = log.Report();
        Assert.Contains("frame=140 async_finish 58.300 ms (result 0.200 / checks 0.100 / parent 55.000 / own 3.000)", report);
        Assert.Contains("frame=141 sync 12.500 ms", report);
        Assert.DoesNotContain("frame=100", report.Substring(report.IndexOf("; frame=", StringComparison.Ordinal)));
        Assert.Contains("truncated=false", report);
    }

    [Fact]
    public void TheSlowRingKeepsTheNewestAndSaysWhenItTruncated()
    {
        var log = new GrowTimingLog();
        for (int i = 0; i < GrowTimingLog.Capacity + 5; i++) log.Record(Op(1000 + i, GrowKind.AsyncFinish, 20 + i));
        Assert.Equal(GrowTimingLog.Capacity, log.SlowCount);
        Assert.Equal(1005, log.Slow(0).Frame);
        Assert.Equal(1000 + GrowTimingLog.Capacity + 4, log.Slow(GrowTimingLog.Capacity - 1).Frame);
        Assert.Contains("truncated=true", log.Report());
    }

    [Fact]
    public void RecordingAllocatesNothingAfterWarmUp()
    {
        var log = new GrowTimingLog();
        for (int i = 0; i < 64; i++) log.Record(Op(i, GrowKind.AsyncFinish, i % 2 == 0 ? 30 : 1));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) log.Record(Op(i, (GrowKind)(i % 3), i % 7 == 0 ? 40 : 2));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
