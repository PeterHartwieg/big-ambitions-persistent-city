using System;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5SpikeCapTests
{
    private static void Frames(ModFrameRecorder recorder, int count, double milliseconds, ref long at)
    {
        long ticks = (long)Math.Round(milliseconds * 10);
        for (int i = 0; i < count; i++)
        {
            recorder.Begin(at, at, 0, 0);
            recorder.Charge(GcAllocationSegment.AdvanceEvents, at + ticks / 2, 0, 0);
            recorder.Charge(GcAllocationSegment.PhysicalIndex, at + ticks, 0, 0);
            recorder.End(at + ticks, 0, 0, null);
            at += ticks + 1;
        }
    }

    [Fact]
    public void TheRecorderRetainsAThousandAndTwentyFourSpikeRows()
    {
        Assert.Equal(1024, ModFrameRecorder.SpikeCapacity);
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        Frames(recorder, ModFrameRecorder.SpikeCapacity, 31, ref at);
        Assert.Equal(ModFrameRecorder.SpikeCapacity, recorder.Spikes);
        Assert.Equal(ModFrameRecorder.SpikeCapacity, recorder.SpikeCount);
        Assert.False(recorder.SpikesTruncated);
        using var report = JsonDocument.Parse(recorder.ReportJson());
        var root = report.RootElement;
        Assert.Equal(1024, root.GetProperty("phase_spikes_30ms").GetInt64());
        Assert.Equal(1024, root.GetProperty("phase_spikes_retained").GetInt64());
        Assert.False(root.GetProperty("phase_spikes_truncated").GetBoolean());
        Assert.Equal(1024, root.GetProperty("phase_spike_cap").GetInt64());
        Assert.Equal(1024, root.GetProperty("spike_frames").GetArrayLength());
    }

    [Fact]
    public void SpikesPastTheCapAreCountedAndTheReportSaysItTruncated()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        Frames(recorder, 100, 2, ref at);
        long first = at;
        Frames(recorder, 1500, 40, ref at);
        Assert.Equal(1600, recorder.Frames);
        Assert.Equal(1500, recorder.Spikes);
        Assert.Equal(ModFrameRecorder.SpikeCapacity, recorder.SpikeCount);
        Assert.True(recorder.SpikesTruncated);
        Assert.Equal(first, recorder.Spike(0).Frame);
        for (int i = 1; i < recorder.SpikeCount; i++) Assert.True(recorder.Spike(i).Frame > recorder.Spike(i - 1).Frame);
        using var report = JsonDocument.Parse(recorder.ReportJson());
        var root = report.RootElement;
        Assert.Equal(1500, root.GetProperty("phase_spikes_30ms").GetInt64());
        Assert.Equal(1024, root.GetProperty("phase_spikes_retained").GetInt64());
        Assert.True(root.GetProperty("phase_spikes_truncated").GetBoolean());
        Assert.Equal(1024, root.GetProperty("spike_frames").GetArrayLength());
        Assert.Equal("physical_index", root.GetProperty("spike_frames")[1023].GetProperty("phase").GetString());
        Assert.Contains("spikes>=30ms=1500 retained=1024 truncated", recorder.ReportHuman());
        recorder.Reset();
        Assert.False(recorder.SpikesTruncated);
        Assert.Equal(0, recorder.SpikeCount);
    }

    [Fact]
    public void RecordingSpikeFramesAllocatesNothingAfterArming()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        Frames(recorder, 8, 40, ref at);
        recorder.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frames(recorder, 1200, 40, ref at);
        Frames(recorder, 1200, 1, ref at);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1200, recorder.Spikes);
        Assert.True(recorder.SpikesTruncated);
    }

    [Fact]
    public void AnUnarmedRecorderIgnoresFramesWithoutAllocating()
    {
        var recorder = new ModFrameRecorder();
        long at = 0;
        Frames(recorder, 4, 40, ref at);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frames(recorder, 1000, 40, ref at);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, recorder.Spikes);
        Assert.Equal(0, recorder.Frames);
    }
}
