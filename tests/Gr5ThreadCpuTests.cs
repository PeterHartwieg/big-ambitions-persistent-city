using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5ThreadCpuTests
{
    [Fact]
    public void TheMainThreadClockCountsSpinningAndNotSleeping()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) { Assert.False(ThreadCpuClock.Available); Assert.Equal(-1, ThreadCpuClock.Nanoseconds()); return; }
        Assert.True(ThreadCpuClock.Available);
        long sleepStart = ThreadCpuClock.Nanoseconds();
        var wall = Stopwatch.StartNew();
        Thread.Sleep(60);
        long slept = ThreadCpuClock.Nanoseconds() - sleepStart;
        Assert.True(wall.Elapsed.TotalMilliseconds >= 55);
        Assert.InRange(slept, 0, 10_000_000);
        long spinStart = ThreadCpuClock.Nanoseconds();
        var limit = Stopwatch.StartNew();
        long spun = 0;
        while (spun < 20_000_000 && limit.Elapsed.TotalSeconds < 10) spun = ThreadCpuClock.Nanoseconds() - spinStart;
        Assert.True(spun >= 20_000_000);
    }

    [Fact]
    public void ReadingTheClockAllocatesNothing()
    {
        ThreadCpuClock.Nanoseconds();
        long before = GC.GetAllocatedBytesForCurrentThread();
        long total = 0;
        for (int i = 0; i < 1000; i++) total += ThreadCpuClock.Nanoseconds();
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        GC.KeepAlive(total);
    }

    private static ModFrameRow Frame(ModFrameRecorder recorder, long at, double wallMs, long cpuStart, long cpuEnd)
    {
        long ticks = (long)Math.Round(wallMs * 10);
        recorder.Begin(at, at, 0, 0, 0, cpuStart);
        recorder.Charge(GcAllocationSegment.PhysicalIndex, at + ticks, 0, 0);
        recorder.End(at + ticks, 0, 0, 0, null, cpuEnd);
        return recorder.Top(0);
    }

    [Fact]
    public void AModSpikeWithLittleThreadCpuIsCountedAsDescheduled()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        Frame(recorder, 0, 40, 1_000_000, 3_000_000);
        Frame(recorder, 1000, 35, 5_000_000, 41_000_000);
        Frame(recorder, 2000, 45, -1, -1);
        Assert.Equal(3, recorder.Spikes);
        Assert.Equal(2, recorder.CpuFrames);
        Assert.Equal(1, recorder.CpuSpikes);
        Assert.Equal(1, recorder.DescheduledSpikes);
        using var report = JsonDocument.Parse(recorder.ReportJson());
        var root = report.RootElement;
        Assert.Equal(1, root.GetProperty("phase_cpu_spikes_30ms").GetInt64());
        Assert.Equal(1, root.GetProperty("phase_descheduled_spikes_30ms").GetInt64());
        var spikes = root.GetProperty("spike_frames");
        Assert.Equal(2.0, spikes[0].GetProperty("cpu_ms").GetDouble(), 3);
        Assert.Equal(38.0, spikes[0].GetProperty("descheduled_ms").GetDouble(), 3);
        Assert.Equal(36.0, spikes[1].GetProperty("cpu_ms").GetDouble(), 3);
        Assert.Equal(0.0, spikes[1].GetProperty("descheduled_ms").GetDouble(), 3);
        Assert.Equal(JsonValueKind.Null, spikes[2].GetProperty("cpu_ms").ValueKind);
        Assert.Contains("descheduled>=30ms=1", recorder.ReportHuman());
    }

    [Fact]
    public void RecordingThreadCpuAllocatesNothingAfterWarmUp()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        for (int i = 0; i < 4; i++) Frame(recorder, i * 1000, 2, i, i + 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 4; i < 2000; i++) Frame(recorder, i * 1000, 2, i * 10, i * 10 + 5);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void CollectionRowsCarryMainThreadCpuAndThePauseByCpuLeavesPreemptionOut()
    {
        var sampler = new GcPauseSampler();
        sampler.Start(100);
        double at = 0;
        sampler.Observe(at, 0, 0, 0, 100);
        for (int i = 0; i < 40; i++)
        {
            sampler.ModWork(1); sampler.ModWorkCpu(1);
            at += 16; sampler.FrameCpu(15);
            sampler.Observe(at, 0, 0, 0, 100 + i);
        }
        sampler.ModWork(1); sampler.ModWorkCpu(0.9);
        at += 320; sampler.FrameCpu(40);
        sampler.Observe(at, 1, 0, 0, 50);
        sampler.ModWork(1); sampler.ModWorkCpu(1);
        at += 60; sampler.FrameCpu(58);
        sampler.Observe(at, 2, 0, 0, 40);
        Assert.Equal(2, sampler.CollectionFrames);
        Assert.Equal(42, sampler.CpuFrames);
        Assert.Equal(1, sampler.DescheduledFrames);
        Assert.Equal(1, sampler.DescheduledCollectionFrames);
        var first = sampler.CollectionRow(0);
        Assert.Equal(40, first.FrameCpuMs, 3);
        Assert.Equal(0.9, first.ModCpuMs, 3);
        Assert.Equal(304, sampler.PauseSpread().Max, 3);
        Assert.Equal(43, sampler.PauseCpuSpread().Max, 3);
        Assert.Equal(25, sampler.PauseCpuSpread().P50, 3);
        Assert.Equal(280, sampler.DescheduledSpread(true).Max, 3);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 2, true, -1, -1)));
        var root = report.RootElement;
        Assert.Equal(1, root.GetProperty("descheduled_collection_frames_30ms").GetInt64());
        Assert.Equal(43, root.GetProperty("pause_cpu_ms").GetProperty("max").GetDouble(), 3);
        var rows = root.GetProperty("collection_rows");
        Assert.Equal(40, rows[0].GetProperty("frame_cpu_ms").GetDouble(), 3);
        Assert.Equal(280, rows[0].GetProperty("descheduled_ms").GetDouble(), 3);
        Assert.Equal(2, rows[1].GetProperty("descheduled_ms").GetDouble(), 3);
    }

    [Fact]
    public void FramesWithoutAThreadClockReportNullCpu()
    {
        var sampler = new GcPauseSampler();
        sampler.Start(100);
        sampler.Observe(0, 0, 0, 0, 100);
        sampler.ModWork(1);
        sampler.Observe(16, 0, 0, 0, 100);
        sampler.ModWork(1);
        sampler.Observe(60, 1, 0, 0, 60);
        Assert.Equal(0, sampler.CpuFrames);
        Assert.Equal(-1, sampler.CollectionRow(0).FrameCpuMs);
        Assert.Equal(0, sampler.PauseCpuSpread().Count);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 2, true, -1, -1)));
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("collection_rows")[0].GetProperty("frame_cpu_ms").ValueKind);
        Assert.Contains("main-thread cpu unavailable", sampler.ReportHuman(new GcReportContext(1000, 2000, null, 2, true, -1, -1)));
    }
}
