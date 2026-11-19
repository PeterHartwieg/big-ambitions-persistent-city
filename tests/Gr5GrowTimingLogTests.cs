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
        Assert.Contains("frame=140 async_finish 58.300 ms (staging 0.000 / result 0.200 / checks 0.100 / parent 55.000 / own 3.000)", report);
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

    private static ModFrameRecorder Armed()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(1000);
        return recorder;
    }

    [Fact]
    public void ASpikeRowCarriesItsFramesSlowestGrowEvenUnderTheTenMillisecondCutoff()
    {
        var recorder = Armed();
        recorder.Begin(7610, 0, 0, 0);
        recorder.Charge(GcAllocationSegment.PhysicalSpawnPedestrian, 10, 0, 0);
        recorder.NoteGrow(Op(7610, GrowKind.AsyncStart, 0.3, result: 0.3));
        recorder.Detail(GcAllocationSegment.PhysicalSpawnGrow, 17, 0, 0);
        recorder.NoteGrow(Op(7610, GrowKind.AsyncFinish, 7.467, result: 0.1, checks: 0.2, parent: 6.9, own: 0.267));
        recorder.Charge(GcAllocationSegment.PhysicalUnattributed, 36, 0, 0);
        recorder.End(36, 0, 0, null);
        recorder.Begin(7611, 40, 0, 0);
        recorder.End(41, 0, 0, null);
        Assert.Equal(1, recorder.SpikeCount);
        var row = recorder.Spike(0);
        Assert.Equal(7610, row.Frame);
        Assert.Equal(2, row.GrowCount);
        Assert.Equal(GrowKind.AsyncFinish, row.Grow.Kind);
        Assert.Equal(6.9, row.Grow.Parent);
        string json = recorder.ReportJson();
        Assert.Contains("\"frame\":7610,", json);
        Assert.Contains("\"grow_ops\":2,\"grow_kind\":\"async_finish\",\"grow_failed\":false,\"grow_ms\":7.467,\"grow_staging_ms\":0,\"grow_result_ms\":0.1,\"grow_checks_ms\":0.2,\"grow_parent_ms\":6.9,\"grow_own_ms\":0.267", json);
        Assert.Contains("\"grow_ops\":0,\"grow_kind\":\"none\"", json);
    }

    [Fact]
    public void GrowBreakdownsStayWithTheRecorderAcrossPoolReplacementAndOutsideFramesAreIgnored()
    {
        var recorder = Armed();
        var firstPool = new GrowTimingLog();
        var timing = Op(500, GrowKind.SyncInstantiate, 31.0, result: 30.0, own: 1.0);
        firstPool.Record(timing);
        recorder.Begin(500, 0, 0, 0);
        recorder.NoteGrow(timing);
        recorder.Charge(GcAllocationSegment.PhysicalSpawnPedestrian, 33, 0, 0);
        recorder.End(33, 0, 0, null);
        firstPool = null;
        var replacement = new GrowTimingLog();
        recorder.NoteGrow(Op(501, GrowKind.AsyncFinish, 99));
        recorder.Begin(502, 50, 0, 0);
        recorder.End(51, 0, 0, null);
        Assert.Equal(0, replacement.Recorded);
        Assert.Equal(1, recorder.SpikeCount);
        Assert.Equal(GrowKind.SyncInstantiate, recorder.Spike(0).Grow.Kind);
        Assert.Equal(30.0, recorder.Spike(0).Grow.Result);
        Assert.Equal(0, recorder.Top(1).GrowCount);
    }

    [Fact]
    public void AGrowFrameWhoseColdStagingFailsStillCarriesTheOperation()
    {
        var recorder = Armed();
        var log = new GrowTimingLog();
        var failedStaging = new GrowTiming { Frame = 900, Kind = GrowKind.SyncInstantiate, Staging = 31.5, Total = 31.6, Failed = true };
        log.Record(failedStaging);
        recorder.Begin(900, 0, 0, 0);
        recorder.Charge(GcAllocationSegment.PhysicalSpawnPedestrian, 1, 0, 0);
        recorder.NoteGrow(failedStaging);
        recorder.Detail(GcAllocationSegment.PhysicalSpawnGrow, 33, 0, 0);
        recorder.End(33, 0, 0, null);
        var row = recorder.Spike(0);
        Assert.Equal((int)GcAllocationSegment.PhysicalSpawnGrow, row.Detail);
        Assert.Equal(1, row.GrowCount);
        Assert.True(row.Grow.Failed);
        Assert.Equal(31.5, row.Grow.Staging);
        Assert.Contains("\"grow_ops\":1,\"grow_kind\":\"sync\",\"grow_failed\":true,\"grow_ms\":31.6,\"grow_staging_ms\":31.5,", recorder.ReportJson());
        Assert.Contains("frame=900 sync failed 31.600 ms (staging 31.500", log.Report());
        var asyncFailure = new GrowTiming { Frame = 901, Kind = GrowKind.AsyncStart, Staging = 12, Total = 12, Failed = true };
        recorder.Begin(901, 40, 0, 0);
        recorder.NoteGrow(asyncFailure);
        recorder.End(72, 0, 0, null);
        Assert.Contains("\"grow_kind\":\"async_start\",\"grow_failed\":true,\"grow_ms\":12,\"grow_staging_ms\":12,", recorder.ReportJson());
    }

    [Fact]
    public void NotingGrowsAllocatesNothingAfterArming()
    {
        var recorder = Armed();
        recorder.Begin(1, 0, 0, 0);
        recorder.NoteGrow(Op(1, GrowKind.AsyncFinish, 1));
        recorder.End(1, 0, 0, null);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i < 2_000; i++)
        {
            recorder.Begin(i, i * 100L, 0, 0);
            recorder.NoteGrow(Op(i, GrowKind.AsyncFinish, i % 50));
            recorder.End(i * 100L + i % 60, 0, 0, null);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
