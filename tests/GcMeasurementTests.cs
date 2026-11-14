using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class GcMeasurementTests
{
    private static GcReportContext Context(int? target = null) => new GcReportContext(1200, 1800, target, 0, false, 64L << 20, 48L << 20);

    [Fact]
    public void PauseStatisticsSeparateCollectionFramesFromPlainFrames()
    {
        var sampler = new GcPauseSampler(1000);
        sampler.Frame(99, 1, 1, 0, 0, 1);
        Assert.Equal(0, sampler.Frames);
        sampler.Start(100);
        for (int i = 0; i < 100; i++) sampler.Frame(16 + (i % 2), 1, 0, 0, 0, 100 + i);
        double[] gcFrames = { 20, 26, 46, 66, 116 };
        foreach (double ms in gcFrames) sampler.Frame(ms, 3, 1, 0, 0, 150);
        sampler.Frame(18, 2, 0, 0, 2, 140);
        sampler.Stop(130);

        Assert.Equal(106, sampler.Frames); Assert.Equal(6, sampler.CollectionFrames);
        Assert.Equal(5, sampler.Collections(0)); Assert.Equal(0, sampler.Collections(1)); Assert.Equal(2, sampler.Collections(2));
        var plain = sampler.FrameSpread(false);
        Assert.Equal(100, plain.Count); Assert.Equal(16, plain.P50); Assert.Equal(17, plain.P95); Assert.Equal(17, plain.Max);
        var pause = sampler.PauseSpread();
        Assert.Equal(6, pause.Count); Assert.Equal(10, pause.P50); Assert.Equal(100, pause.P95); Assert.Equal(100, pause.Max);
        Assert.Equal(3, sampler.CollectionSpikes()); Assert.Equal(3, sampler.FrameSpikes());
        Assert.Equal(3, sampler.ModSpread(true).P95); Assert.Equal(1, sampler.ModSpread(false).Max);
        Assert.Equal(100, sampler.HeapStart); Assert.Equal(130, sampler.HeapEnd); Assert.Equal(199, sampler.HeapPeak);
        Assert.Equal((199L + 150 * 5) / 6, sampler.HeapBeforeCollection); Assert.Equal((150L * 5 + 140) / 6, sampler.HeapAfterCollection);
        sampler.Frame(500, 500, 1, 1, 1, 1);
        Assert.Equal(106, sampler.Frames);
    }

    [Fact]
    public void FullBuffersCountDroppedFramesAndRestartClears()
    {
        var sampler = new GcPauseSampler(4);
        sampler.Start(10);
        for (int i = 0; i < 10; i++) sampler.Frame(i == 9 ? 80 : 16, 0, i == 9 ? 1 : 0, 0, 0, 10);
        Assert.Equal(10, sampler.Frames); Assert.Equal(6, sampler.Dropped);
        Assert.Equal(1, sampler.Collections(0)); Assert.Equal(80, sampler.MaxFrameMs);
        Assert.Equal(0, sampler.FrameSpread(true).Count);
        Assert.Equal(1, sampler.CollectionSpikes()); Assert.Equal(1, sampler.FrameSpikes());
        sampler.Start(20);
        Assert.Equal(0, sampler.Frames); Assert.Equal(0, sampler.Collections(0)); Assert.Equal(0, sampler.FrameSpread(false).Count);
        Assert.Equal(0, sampler.CollectionSpikes()); Assert.Equal(0, sampler.FrameSpikes());
        Assert.Equal(-1, sampler.HeapBeforeCollection);
    }

    [Fact]
    public void ReportingWorkAndItsCollectionsStayOutOfTheWindow()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(100);
        sampler.Observe(1000, 5, 0, 0, 100);
        sampler.ModWork(2);
        sampler.Observe(1016, 5, 0, 0, 101);
        sampler.ModWork(3);
        sampler.Observe(1032, 6, 0, 0, 90);
        Assert.Equal(2, sampler.Frames); Assert.Equal(1, sampler.Collections(0));
        Assert.Equal(2, sampler.ModSpread(false).Max); Assert.Equal(3, sampler.ModSpread(true).Max);
        sampler.ReportJson(Context());
        sampler.Rebaseline();
        sampler.ModWork(40);
        sampler.Observe(1600, 9, 1, 0, 300);
        sampler.ModWork(1);
        sampler.Observe(1616, 9, 1, 0, 301);
        Assert.Equal(3, sampler.Frames); Assert.Equal(1, sampler.Collections(0)); Assert.Equal(0, sampler.Collections(1));
        Assert.Equal(16, sampler.MaxFrameMs); Assert.Equal(3, sampler.MaxModMs);
        Assert.Equal(0, sampler.FrameSpikes()); Assert.Equal(0, sampler.CollectionSpikes());
        Assert.Equal(2, sampler.FrameSpread(false).Count); Assert.Equal(1, sampler.ModSpread(false).P50);
        sampler.Rebaseline();
        sampler.Observe(2000, 9, 1, 0, 1000);
        sampler.Observe(2016, 10, 1, 0, 200);
        Assert.Equal(4, sampler.Frames); Assert.Equal(2, sampler.Collections(0));
        Assert.Equal((101L + 1000) / 2, sampler.HeapBeforeCollection); Assert.Equal((90L + 200) / 2, sampler.HeapAfterCollection);
    }

    [Fact]
    public void MeasurementTargetSurvivesARouteRebuildBeforeItsBoundary()
    {
        var (state, market) = F5gPopulationFillTests.City(12, 1);
        var fill = F5gPopulationFillTests.Catalogue(12, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(3));
        var city = F5gPopulationFillTests.Start(state, market, fill);
        int day = (int)(state.Minute / 1440) + 1;
        int target = state.Citizens.Count * 3 + 1500;
        city.SetMeasurementPersonTarget(target);
        F5gPopulationFillTests.Advance(city, day * 1440 - 30);
        var rebuilt = city.RebuildRoutes(new FreshCityTraceTests.Walk());
        Assert.Equal(target, rebuilt.MeasurementPersonTarget);
        F5gPopulationFillTests.Advance(rebuilt, day * 1440 + 1);
        Assert.Equal(target, PersonTable.PersonCount(state));
        Assert.Null(F5gPopulationFillTests.Reload(rebuilt, market, fill).MeasurementPersonTarget);
    }

    [Fact]
    public void ReportLineParsesWithStableKeys()
    {
        var sampler = new GcPauseSampler(64);
        string empty = sampler.ReportJson(Context());
        sampler.Start(1000);
        for (int i = 0; i < 20; i++) sampler.Frame(16.25, 0.5, i % 7 == 0 ? 1 : 0, 0, 0, 1000 + i);
        string full = sampler.ReportJson(Context(3000));
        string[] keys = { "policy", "mode", "running", "residents", "visitors", "persons", "person_target", "max_generation", "incremental",
            "frames", "frames_stored", "frames_dropped", "collection_frames", "gen0", "gen1", "gen2",
            "heap_start_bytes", "heap_end_bytes", "heap_peak_bytes", "heap_before_gc_bytes", "heap_after_gc_bytes", "mono_heap_bytes", "mono_used_bytes",
            "frame_ms_gc", "frame_ms_plain", "pause_ms", "mod_ms_gc", "mod_ms_plain", "gc_spikes_30ms", "frame_spikes_30ms", "max_frame_ms", "max_mod_ms", "alloc_frames", "frame_alloc_bytes",
            "mod_alloc_bytes", "mod_alloc_samples", "mod_alloc_skipped", "advance_alloc_bytes", "advance_alloc_samples", "advance_alloc_skipped",
            "physical_alloc_bytes", "physical_alloc_samples", "physical_alloc_skipped" };
        foreach (string json in new[] { empty, full })
        {
            Assert.DoesNotContain('\n', json);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(keys, document.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
            foreach (string spread in new[] { "frame_ms_gc", "frame_ms_plain", "pause_ms", "mod_ms_gc", "mod_ms_plain" })
                Assert.Equal(new[] { "count", "p50", "p95", "max" }, document.RootElement.GetProperty(spread).EnumerateObject().Select(p => p.Name).ToArray());
        }
        using var report = JsonDocument.Parse(full);
        var root = report.RootElement;
        Assert.Equal(GcPauseSampler.PolicyId, root.GetProperty("policy").GetString());
        Assert.Equal("measurement", root.GetProperty("mode").GetString());
        Assert.Equal(3000, root.GetProperty("persons").GetInt64()); Assert.Equal(3000, root.GetProperty("person_target").GetInt32());
        Assert.Equal(3, root.GetProperty("gen0").GetInt64()); Assert.Equal(20, root.GetProperty("frames").GetInt64());
        Assert.Equal(16.25, root.GetProperty("frame_ms_plain").GetProperty("p50").GetDouble());
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(empty).RootElement.GetProperty("person_target").ValueKind);
        Assert.Contains("diagnostic only", sampler.ReportHuman(Context()));
    }

    [Fact]
    public void AllocationSplitCountsPlainFramesAndSkipsCollections()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Allocated(GcAllocationSegment.Mod, 0, 500, false);
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.Mod));
        sampler.Start(1000);
        sampler.Observe(0, 0, 0, 0, 1000);
        sampler.Observe(16, 0, 0, 0, 1400);
        sampler.Observe(32, 1, 0, 0, 200);
        sampler.Observe(48, 1, 0, 0, 500);
        Assert.Equal(2, sampler.AllocationFrames); Assert.Equal(700, sampler.FrameAllocatedBytes);
        sampler.Allocated(GcAllocationSegment.Mod, 1000, 1300, false);
        sampler.Allocated(GcAllocationSegment.Physical, 1000, 1100, false);
        sampler.Allocated(GcAllocationSegment.Physical, 1000, 2000, true);
        sampler.Allocated(GcAllocationSegment.Advance, 1000, 900, false);
        Assert.Equal(300, sampler.SegmentBytes(GcAllocationSegment.Mod)); Assert.Equal(1, sampler.SegmentSamples(GcAllocationSegment.Mod));
        Assert.Equal(100, sampler.SegmentBytes(GcAllocationSegment.Physical)); Assert.Equal(1, sampler.SegmentSkipped(GcAllocationSegment.Physical));
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.Advance)); Assert.Equal(1, sampler.SegmentSkipped(GcAllocationSegment.Advance));
        using var report = JsonDocument.Parse(sampler.ReportJson(Context()));
        Assert.Equal(700, report.RootElement.GetProperty("frame_alloc_bytes").GetInt64());
        Assert.Equal(300, report.RootElement.GetProperty("mod_alloc_bytes").GetInt64());
        sampler.Start(1);
        Assert.Equal(0, sampler.FrameAllocatedBytes); Assert.Equal(0, sampler.SegmentSamples(GcAllocationSegment.Mod));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; i++) sampler.Allocated((GcAllocationSegment)(i % GcPauseSampler.Segments), i, i + 8, (i & 3) == 0);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void FrameSamplingAllocatesNothing()
    {
        var sampler = new GcPauseSampler(1024);
        sampler.Start(1);
        sampler.Frame(16, 1, 0, 0, 0, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; i++) sampler.Frame(16, 1, i & 1, 0, 0, i);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        var on = new GcPauseSampler();
        on.Start(1);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < 100_000; i++) { on.Frame(16, 1, 0, 0, 0, i); GC.CollectionCount(0); GC.GetTotalMemory(false); }
        double perFrame = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / 100_000;
        Console.WriteLine("gc sampler on: " + perFrame.ToString("0.000000") + " ms/frame");
        PerfGate.Under(perFrame, 0.005, "gc sampler per frame");
    }

    [Fact]
    public void MeasurementTargetLeavesSaveBytesUnchangedAndIsGoneAfterReload()
    {
        var (state, market) = F5gPopulationFillTests.City(12, 1);
        var fill = F5gPopulationFillTests.Catalogue(12, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(3));
        var city = F5gPopulationFillTests.Start(state, market, fill);
        string before = SaveCodec.Write(city.State);
        city.SetMeasurementPersonTarget(500);
        Assert.Equal(500, city.MeasurementPersonTarget);
        Assert.Equal(before, SaveCodec.Write(city.State));
        Assert.Throws<ArgumentOutOfRangeException>(() => city.SetMeasurementPersonTarget(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => city.SetMeasurementPersonTarget(PersonTable.MaximumPersons + 1));
        var reloaded = F5gPopulationFillTests.Reload(city, market, fill);
        Assert.Null(reloaded.MeasurementPersonTarget);
        Assert.Equal(before, SaveCodec.Write(reloaded.State));
    }

    [Fact]
    public void MeasurementTargetGrowsPastTheCensusAndReportsTheReachedCount()
    {
        var fill = F5gPopulationFillTests.Catalogue(12, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(3));
        var (normalState, normalMarket) = F5gPopulationFillTests.City(12, 1);
        var normal = F5gPopulationFillTests.Start(normalState, normalMarket, fill);
        var (state, market) = F5gPopulationFillTests.City(12, 1);
        var city = F5gPopulationFillTests.Start(state, market, fill);
        int day = (int)(state.Minute / 1440) + 1;
        long residents = state.Citizens.Count;
        int target = (int)(residents * 3 + 1500);
        city.SetMeasurementPersonTarget(target);
        F5gPopulationFillTests.Advance(normal, day * 1440 + 1);
        F5gPopulationFillTests.Advance(city, day * 1440 + 1);
        long natural = PersonTable.PersonCount(normalState);
        Assert.True(natural < target, "the census alone reaches the target");
        Assert.Equal(target, PersonTable.PersonCount(state));
        Assert.True(state.Visitors.Count > PopulationFillPolicy.MaximumPersonsPerBoundary);
        Assert.Equal(target - residents, city.PopulationFillTargets!.Value.VisitorCapacity);
        F5gPopulationFillTests.Invariants(state, "measured");
        F5gPopulationFillTests.Conserved(state, "measured");

        var reloaded = F5gPopulationFillTests.Reload(city, market, fill);
        F5gPopulationFillTests.Advance(reloaded, (day + 1) * 1440 + 1);
        Assert.Equal(target, PersonTable.PersonCount(reloaded.State));

        var (lonely, lonelyMarket) = F5gPopulationFillTests.City(12, 1, portals: false);
        var stationless = F5gPopulationFillTests.Start(lonely, lonelyMarket, fill);
        stationless.SetMeasurementPersonTarget(target);
        F5gPopulationFillTests.Advance(stationless, day * 1440 + 1);
        Assert.Equal(residents, PersonTable.PersonCount(lonely));
    }
}
