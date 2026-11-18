using System;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5ReportHeapTests
{
    private readonly ITestOutputHelper output;
    public Gr5ReportHeapTests(ITestOutputHelper output) { this.output = output; }
    private const int NightFrames = 16322;
    private static readonly GcReportContext Context = new GcReportContext(1000, 2000, null, 0, true, 1_462_697_984, 1_343_971_328);

    private static GcPauseSampler NightSampler()
    {
        var sampler = new GcPauseSampler();
        sampler.Start(1_400_000_000, "boehm_total");
        sampler.FrameRecorder.Arm(10_000_000);
        var random = new Random(7);
        long at = 0;
        for (int i = 0; i < NightFrames; i++)
        {
            bool gc = i % 2750 == 2749;
            sampler.Frame(30 + random.NextDouble() * 15 + (gc ? 40 : 0), random.NextDouble() * 2, gc ? 1 : 0, 0, 0, 1_300_000_000 + (i % 2750) * 48_000L);
            long ticks = (long)(random.NextDouble() * 30_000);
            sampler.FrameRecorder.Begin(i, at, 0, 0);
            sampler.FrameRecorder.Charge(GcAllocationSegment.ModClosures, at + ticks, 100, 0);
            sampler.FrameRecorder.End(at + ticks, 200, 0, sampler);
            at += ticks + 1;
        }
        return sampler;
    }

    [Fact]
    public void ANightReportAllocatesOnlyItsTextAfterTheFirstReport()
    {
        var sampler = NightSampler();
        sampler.ReportJson(Context); sampler.ReportHuman(Context);
        Assert.Equal((long)NightFrames * sizeof(double), sampler.SpreadScratchBytes);
        long before = GC.GetAllocatedBytesForCurrentThread();
        string json = sampler.ReportJson(Context);
        long afterJson = GC.GetAllocatedBytesForCurrentThread();
        string human = sampler.ReportHuman(Context);
        long afterHuman = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(afterJson - before < 8 * json.Length + 32 * 1024, "json bytes " + (afterJson - before) + " for " + json.Length + " chars");
        Assert.True(afterHuman - afterJson < 4 * 1024, "human bytes " + (afterHuman - afterJson));
    }

    private static long Retained(GcPauseSampler sampler)
    {
        var heap = new RetainedHeap();
        heap.Add("sampler", sampler);
        return heap.Bytes;
    }

    [Fact]
    public void TheFirstNightReportAndStopStayInsideTheirAllocationAndRetentionBounds()
    {
        var sampler = NightSampler();
        long atSampler = Retained(sampler);
        long started = GC.GetAllocatedBytesForCurrentThread();
        string json = sampler.ReportJson(Context);
        long afterJson = GC.GetAllocatedBytesForCurrentThread();
        string human = sampler.ReportHuman(Context);
        long afterHuman = GC.GetAllocatedBytesForCurrentThread();
        string frames = sampler.FrameRecorder.ReportHuman();
        string framesJson = sampler.FrameRecorder.ReportJson();
        long afterExport = GC.GetAllocatedBytesForCurrentThread();
        long atReport = Retained(sampler);
        sampler.Stop(1_400_000_000);
        sampler.FrameRecorder.Disarm();
        sampler.ReportJson(Context); sampler.ReportHuman(Context); sampler.FrameRecorder.ReportHuman();
        sampler.Rebaseline();
        long afterStop = GC.GetAllocatedBytesForCurrentThread();
        long atPostReport = Retained(sampler);
        long text = 2L * (json.Length + human.Length + frames.Length + framesJson.Length);
        output.WriteLine("first report (.NET): json " + (afterJson - started) + " B for " + json.Length + " chars, human " + (afterHuman - afterJson)
            + " B, frame recorder export " + (afterExport - afterHuman) + " B, stop and second report " + (afterStop - afterExport) + " B");
        output.WriteLine("retained (.NET): at sampler " + atSampler + " B, after first report " + atReport + " B, after stop " + atPostReport + " B");
        Assert.True(afterJson - started < 8 * json.Length + NightFrames * sizeof(double) + 64 * 1024, "first json bytes " + (afterJson - started));
        Assert.True(afterHuman - afterJson < 8 * 1024, "first human bytes " + (afterHuman - afterJson));
        Assert.True(afterExport - started < text * 4 + NightFrames * sizeof(double) + 256 * 1024, "first report and export bytes " + (afterExport - started));
        Assert.True(atReport - atSampler <= NightFrames * sizeof(double) + 1024, "report retained " + (atReport - atSampler));
        Assert.True(atPostReport - atSampler <= NightFrames * sizeof(double) + 1024, "stop retained " + (atPostReport - atSampler));
    }

    [Fact]
    public void ReusedScratchKeepsTheReportedSpreads()
    {
        var sampler = NightSampler();
        var first = (sampler.FrameSpread(false), sampler.PauseSpread(), sampler.PauseWithoutModSpread(), sampler.ModSpread(true));
        Assert.Equal(first, (sampler.FrameSpread(false), sampler.PauseSpread(), sampler.PauseWithoutModSpread(), sampler.ModSpread(true)));
    }

    [Fact]
    public void StoppingRetainsNoReportBuffersBeyondTheSamplerArrays()
    {
        var sampler = NightSampler();
        sampler.Stop(1_400_000_000);
        sampler.ReportJson(Context); sampler.ReportHuman(Context); sampler.FrameRecorder.ReportHuman();
        var heap = new RetainedHeap();
        heap.Add("sampler", sampler);
        long frameArrays = (long)GcPauseSampler.DefaultCapacity * (sizeof(float) * 2 + sizeof(bool));
        Assert.True(heap.Bytes < frameArrays + NightFrames * sizeof(double) + 256 * 1024, "retained " + heap.Bytes);
    }

    [Fact]
    public void SamplerAndRecorderPlumbingAllocatesNothingPerFrameAfterWarmUp()
    {
        var sampler = new GcPauseSampler();
        sampler.Start(0, GcAllocationCounter.Probe());
        sampler.FrameRecorder.Arm(System.Diagnostics.Stopwatch.Frequency);
        void Frame(int frame)
        {
            sampler.CounterFrame(GcAllocationCounter.Read(), GcAllocationCounter.Collections);
            sampler.Observe(frame * 16.0, 0, 0, 0, 0, frame);
            ModFrames.Begin(sampler, frame);
            ModFrames.Charge(GcAllocationSegment.ModPrelude);
            AdvanceAllocation.Begin(sampler);
            AdvanceAllocation.Detail(GcAllocationSegment.AdvanceStops);
            AdvanceAllocation.Sub(GcAllocationSegment.AdvanceEventsOther);
            AdvanceAllocation.Detail(GcAllocationSegment.AdvanceEvents);
            AdvanceAllocation.End();
            PhysicalAllocation.Begin(sampler);
            PhysicalAllocation.Detail(GcAllocationSegment.PhysicalUnattributedEntry);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalUnattributed);
            PhysicalAllocation.Mark();
            PhysicalAllocation.Detail(GcAllocationSegment.PhysicalSpawnBake);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalAmbient);
            PhysicalAllocation.End();
            ModFrames.Charge(GcAllocationSegment.ModInspector);
            ModFrames.End();
            sampler.ModWork(0.5);
        }
        for (int i = 0; i < 64; i++) Frame(i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 64; i < 4096; i++) Frame(i);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributedEntry));
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.AdvanceStops));
    }
}
