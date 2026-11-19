using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5DiagnosticsProjectionTests
{
    private static void Frame(ModFrameRecorder recorder, double milliseconds, ref long at)
    {
        long ticks = (long)Math.Round(milliseconds * 10);
        recorder.Begin(at, at, 0, 0);
        recorder.Charge(GcAllocationSegment.PhysicalSpawnPedestrian, at + ticks * 3 / 4, 0, 0);
        recorder.Charge(GcAllocationSegment.ModClosures, at + ticks, 0, 0);
        recorder.End(at + ticks, 0, 0, null);
        at += ticks + 1;
    }

    [Fact]
    public void UpdatesBetweenFifteenAndThirtyMillisecondsAreRetainedForUnionSpikeAttribution()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        foreach (double ms in new[] { 2, 29.3, 14.9, 31, 22.1, 15, 4 }) Frame(recorder, ms, ref at);
        Assert.Equal(1, recorder.Spikes);
        Assert.Equal(3, recorder.NearSpikes);
        Assert.Equal(1, recorder.SpikeCount);
        Assert.Equal(3, recorder.NearSpikeCount);
        Assert.Equal(31, recorder.Spike(0).ModMs, 6);
        Assert.Equal(new[] { 29.3, 22.1, 15 }, Enumerable.Range(0, 3).Select(i => Math.Round(recorder.NearSpike(i).ModMs, 3)).ToArray());
        using var report = JsonDocument.Parse(recorder.ReportJson());
        var root = report.RootElement;
        Assert.Equal(1, root.GetProperty("phase_spikes_30ms").GetInt64());
        Assert.Equal(1, root.GetProperty("phase_spikes_retained").GetInt64());
        Assert.Equal(1, root.GetProperty("spike_frames").GetArrayLength());
        Assert.Equal(3, root.GetProperty("phase_near_spikes_15ms").GetInt64());
        Assert.Equal(3, root.GetProperty("phase_near_spikes_retained").GetInt64());
        Assert.False(root.GetProperty("phase_near_spikes_truncated").GetBoolean());
        var near = root.GetProperty("near_spike_frames");
        Assert.Equal(3, near.GetArrayLength());
        Assert.Equal("physical_spawn_pedestrian", near[0].GetProperty("phase").GetString());
        Assert.True(near.EnumerateArray().All(r => r.GetProperty("mod_ms").GetDouble() < 30));
        Assert.Contains("near>=15ms=3 retained=3", recorder.ReportHuman());
    }

    [Fact]
    public void NearRowsNeverDisplaceThirtyMillisecondSpikesAndShareTheExistingCap()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        for (int i = 0; i < ModFrameRecorder.SpikeCapacity; i++) Frame(recorder, 20, ref at);
        Assert.Equal(ModFrameRecorder.SpikeCapacity, recorder.NearSpikeCount);
        for (int i = 0; i < 1100; i++) Frame(recorder, 40, ref at);
        Frame(recorder, 20, ref at);
        Assert.Equal(1100, recorder.Spikes);
        Assert.Equal(ModFrameRecorder.SpikeCapacity, recorder.SpikeCount);
        Assert.Equal(0, recorder.NearSpikeCount);
        Assert.True(recorder.SpikesTruncated);
        Assert.True(recorder.NearSpikesTruncated);
        for (int i = 1; i < recorder.SpikeCount; i++) Assert.True(recorder.Spike(i).Frame > recorder.Spike(i - 1).Frame);
        var mixed = new ModFrameRecorder();
        mixed.Arm(10000);
        at = 0;
        for (int i = 0; i < 1000; i++) Frame(mixed, 20, ref at);
        for (int i = 0; i < 100; i++) Frame(mixed, 40, ref at);
        Assert.Equal(100, mixed.SpikeCount);
        Assert.Equal(ModFrameRecorder.SpikeCapacity - 100, mixed.NearSpikeCount);
        Assert.Equal(1000, mixed.NearSpikes);
    }

    [Fact]
    public void RetainingNearRowsAllocatesNothingAfterArming()
    {
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        Frame(recorder, 20, ref at); Frame(recorder, 40, ref at);
        recorder.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1500; i++) Frame(recorder, i % 3 == 0 ? 40 : i % 3 == 1 ? 20 : 2, ref at);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(500, recorder.Spikes);
        Assert.Equal(500, recorder.NearSpikes);
    }

    [Fact]
    public void CollectionRowsCarryTheUnityFrameAndSplitTheirPauseIntoModAndOtherWork()
    {
        var sampler = new GcPauseSampler(256);
        sampler.Start(1000);
        double stamp = 0;
        int gen0 = 0;
        for (int frame = 100; frame < 160; frame++)
        {
            bool collect = frame % 20 == 5;
            if (collect) gen0++;
            sampler.Observe(stamp, gen0, 0, 0, 1000 + frame, frame);
            sampler.ModWork(frame % 20 == 4 ? 12 : 1);
            stamp += frame % 20 == 4 ? 216 : 16;
        }
        sampler.Observe(stamp, gen0, 0, 0, 2000, 160);
        Assert.Equal(3, sampler.CollectionFrames);
        Assert.Equal(3, sampler.CollectionRowCount);
        Assert.False(sampler.CollectionRowsTruncated);
        var row = sampler.CollectionRow(0);
        Assert.Equal(104, row.Frame);
        Assert.Equal(216, row.FrameMs, 6);
        Assert.Equal(12, row.ModMs, 6);
        Assert.Equal(1, row.Gen0);
        Assert.Equal(1104, row.HeapBefore);
        Assert.Equal(1105, row.HeapAfter);
        Assert.Equal(16, row.PreviousFrameMs, 6);
        Assert.Equal(16, row.NextFrameMs, 6);
        Assert.Equal(200, sampler.PauseSpread().P95, 6);
        Assert.Equal(188, sampler.PauseWithoutModSpread().P95, 6);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 2, false, 1_470_000_000, 1_350_000_000)));
        var root = report.RootElement;
        Assert.Equal(188, root.GetProperty("pause_ms_without_mod").GetProperty("p95").GetDouble(), 3);
        Assert.Equal(3, root.GetProperty("collection_rows_retained").GetInt64());
        Assert.False(root.GetProperty("collection_rows_truncated").GetBoolean());
        var first = root.GetProperty("collection_rows")[0];
        Assert.Equal(new[] { "frame", "frame_ms", "mod_ms", "gen0", "gen1", "gen2", "heap_before_bytes", "heap_after_bytes", "previous_frame_ms", "next_frame_ms" }, first.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(16, first.GetProperty("previous_frame_ms").GetDouble(), 3);
        Assert.Equal(16, first.GetProperty("next_frame_ms").GetDouble(), 3);
        Assert.Equal(200, root.GetProperty("pause_ms_over_neighbours").GetProperty("p95").GetDouble(), 3);
        Assert.Equal(3, root.GetProperty("pause_ms_over_neighbours").GetProperty("count").GetInt64());
        Assert.Equal(104, first.GetProperty("frame").GetInt64());
        Assert.Equal(1_470_000_000, root.GetProperty("mono_heap_bytes").GetInt64());
        Assert.Contains("outside UpdateCity p95/max=188.00/188.00 ms", sampler.ReportHuman(new GcReportContext(1000, 2000, null, 2, false, -1, -1)));
    }

    [Fact]
    public void CollectionRowsAreBoundedAndAllocateNothingPerFrame()
    {
        var sampler = new GcPauseSampler(4096);
        sampler.Start(1000);
        sampler.Observe(0, 0, 0, 0, 1000, 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 1; frame <= 3000; frame++) { sampler.ModWork(1); sampler.Observe(frame * 16.0, frame / 10, 0, 0, 1000, frame); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(GcPauseSampler.CollectionRowCapacity, sampler.CollectionRowCount);
        Assert.True(sampler.CollectionRowsTruncated);
        sampler.Start(1000);
        Assert.Equal(0, sampler.CollectionRowCount);
    }

    [Fact]
    public void InspectorTimingProjectionReportsUpdateAndBuildMaximaWithoutAllocatingPerCall()
    {
        var timing = new InspectorTiming(1000);
        timing.Update(0, 2); timing.Update(1, 5);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { timing.Update(i % 3, 3); timing.Draw(i % 3, 1); }
        timing.Update(2, 104); timing.Build(7); timing.Build(40);
        timing.Update(5, 1000); timing.Build(-1);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(timing.Projection(12, "c1", null)));
        var root = document.RootElement;
        Assert.Equal(new[] { "update", "draw", "updateMaxMilliseconds", "picks", "builds", "buildMilliseconds", "buildMaxMilliseconds", "hovered", "selected" },
            root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "closed", "hover", "open" }, root.GetProperty("update").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(104, root.GetProperty("update").GetProperty("open").GetProperty("maxMilliseconds").GetDouble(), 6);
        Assert.Equal(5, root.GetProperty("update").GetProperty("hover").GetProperty("maxMilliseconds").GetDouble(), 6);
        Assert.Equal(104, root.GetProperty("updateMaxMilliseconds").GetDouble(), 6);
        Assert.Equal(3335, root.GetProperty("update").GetProperty("closed").GetProperty("calls").GetInt64());
        Assert.Equal(2, root.GetProperty("builds").GetInt64());
        Assert.Equal(40, root.GetProperty("buildMaxMilliseconds").GetDouble(), 6);
        Assert.Equal(12, root.GetProperty("picks").GetInt64());
        timing.Reset();
        Assert.Equal(0, timing.BuildMaxMilliseconds);
        Assert.Equal(0, timing.UpdateMaxMilliseconds(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InspectorTiming(0));
    }

    [Fact]
    public void TheRigCanReadInspectorTimingAndMonoHeapThroughPublicDiagnosticsProjections()
    {
        string root = CapabilityReportTests.Root();
        string panel = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Runtime", "FoodEconomyPanel.cs"));
        Assert.Contains("public object InspectorDiagnostics => inspector.Timing;", panel);
        string movement = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Runtime", "MovementDiagnostics.cs"));
        Assert.Contains("public object GcDiagnostics", movement);
        Assert.Contains("monoHeapBytes = context.MonoHeapBytes, monoUsedBytes = context.MonoUsedBytes", movement);
        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "tools", "perf-union-probe", "Probe.cs"));
        Assert.Contains("[\"mono_heap\"] = new JObject { [\"start_bytes\"] = monoHeapStart", probe);
        Assert.Contains("ReadMonoHeap(out monoHeapStop, out monoUsedStop);", probe);
        int heap = probe.IndexOf("if (action == \"heap\") return Wrap(Status(), Heap());", StringComparison.Ordinal);
        int armedGate = probe.IndexOf("Require(armed, \"f4b_not_armed\");", StringComparison.Ordinal);
        Assert.True(heap > 0 && armedGate > heap, "the forced-collection heap read is available before arm");
        int scanBefore = probe.IndexOf("ReadMonoHeap(out startScanHeap, out startScanUsed);", StringComparison.Ordinal);
        int startScan = probe.IndexOf("Rescan(true); unexpectedAtWindowStart", StringComparison.Ordinal);
        int start = probe.IndexOf("ReadMonoHeap(out monoHeapStart, out monoUsedStart);", StringComparison.Ordinal);
        Assert.True(scanBefore > 0 && startScan > scanBefore && start > startScan, "the window baseline is read after the forced start scan, which is bracketed");
        int stop = probe.IndexOf("ReadMonoHeap(out monoHeapStop, out monoUsedStop);", StringComparison.Ordinal);
        int stopScan = probe.IndexOf("if (reason == \"explicit-stop\") Rescan(true);", StringComparison.Ordinal);
        Assert.True(stop > 0 && stopScan > stop, "the stop reading precedes the forced stop scan");
        Assert.Contains("if (full && mode == \"reference\") try { MatchedScan(); }", probe);
        Assert.Contains("coverage = null; matchedScans = 0;\n                    MatchedScan();", probe);
        string pedestrians = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Runtime", "PhysicalPedestrians.cs"));
        Assert.Contains("\" ms grow start max/\"", pedestrians);
        Assert.Contains("\" ms grow finish max/\" + growLog.Report();", pedestrians);
        Assert.Contains("growTiming = new GrowTiming { Frame = Time.frameCount, Kind = kind };", pedestrians);
        int asyncStart = pedestrians.IndexOf("BeginGrow(GrowKind.AsyncStart);", StringComparison.Ordinal);
        int finish = pedestrians.IndexOf("BeginGrow(GrowKind.AsyncFinish);", StringComparison.Ordinal);
        int sync = pedestrians.IndexOf("BeginGrow(GrowKind.SyncInstantiate);", StringComparison.Ordinal);
        Assert.True(asyncStart > 0 && finish > asyncStart && sync > 0, "async start, async finish and sync instantiate are recorded separately");
        int result = pedestrians.IndexOf("var made = growing!.Result;", StringComparison.Ordinal);
        Assert.True(result > finish && pedestrians.IndexOf("growTiming.Result = GrowStep();", result, StringComparison.Ordinal) > result, "the async result read is timed");
        int checks = pedestrians.IndexOf("growTiming.Checks = GrowStep();", StringComparison.Ordinal);
        int parented = pedestrians.IndexOf("growTiming.Parent = GrowStep();", StringComparison.Ordinal);
        int owned = pedestrians.IndexOf("growTiming.Own = GrowStep();", StringComparison.Ordinal);
        Assert.True(checks > 0 && parented > checks && owned > parented, "grow finish steps are timed in order");
        Assert.True(pedestrians.IndexOf("character.transform.SetParent(parent, false);", StringComparison.Ordinal) < parented, "the reparent is inside the parent step");
        Assert.DoesNotContain("static readonly double[] GrowSteps", pedestrians);
        Assert.Contains("private static readonly GrowTimingLog growLog", pedestrians);
        int recorded = pedestrians.IndexOf("growLog.Record(growTiming);", StringComparison.Ordinal);
        Assert.True(recorded > 0 && pedestrians.IndexOf("ModFrames.NoteGrow(growTiming);", recorded, StringComparison.Ordinal) > recorded, "every grow operation is noted on the measurement's frame row");
        string counter = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Runtime", "GcAllocationCounter.cs"));
        Assert.Contains("public static void NoteGrow(in GrowTiming timing) => recorder?.NoteGrow(timing);", counter);
        Assert.Contains("SizeHierarchy(character.transform);", pedestrians);
        Assert.Contains("BodyHierarchy.Capacity(parent.hierarchyCount, SubtreeCount(body), Math.Max(0, ceiling - all.Count - 1))", pedestrians);
        Assert.DoesNotContain("body.hierarchyCount", pedestrians);
    }

    [Fact]
    public void TheBodyRootIsSizedFromOneBodySubtreeNotFromTheSharedHierarchy()
    {
        const int body = 150, ceiling = 128;
        Assert.Equal(2 + body + (ceiling - 1) * body, BodyHierarchy.Capacity(2 + body, body, ceiling - 1));
        int adopted = 60, rootAfterReserve = 2 + (adopted + 1) * body;
        int wanted = BodyHierarchy.Capacity(rootAfterReserve, body, ceiling - adopted - 1);
        Assert.Equal(2 + ceiling * body, wanted);
        Assert.True(wanted < 2 * (2 + ceiling * body), "sizing after a reserve adoption stays at one body ceiling");
        Assert.Equal(rootAfterReserve, BodyHierarchy.Capacity(rootAfterReserve, body, 0));
        Assert.Equal(int.MaxValue, BodyHierarchy.Capacity(int.MaxValue - 1, body, ceiling));
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyHierarchy.Capacity(10, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyHierarchy.Capacity(10, 1, -1));
    }

    [Fact]
    public void ThePauseOverNeighboursUsesOnlyAdjacentPlainFrames()
    {
        var sampler = new GcPauseSampler(256);
        sampler.Start(1000);
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        sampler.Frame(116, 0, 1, 0, 0, 1000);
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        sampler.Frame(96, 0, 1, 0, 0, 1000);
        sampler.Frame(86, 0, 1, 0, 0, 1000);
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        sampler.Frame(76, 0, 1, 0, 0, 1000);
        sampler.Rebaseline();
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        sampler.Frame(56, 0, 1, 0, 0, 1000);
        sampler.Frame(16, 0, 0, 0, 0, 1000);
        Assert.Equal(5, sampler.CollectionRowCount);
        Assert.Equal((16.0, 16.0), (sampler.CollectionRow(0).PreviousFrameMs, sampler.CollectionRow(0).NextFrameMs));
        Assert.Equal((16.0, -1.0), (sampler.CollectionRow(1).PreviousFrameMs, sampler.CollectionRow(1).NextFrameMs));
        Assert.Equal((-1.0, 16.0), (sampler.CollectionRow(2).PreviousFrameMs, sampler.CollectionRow(2).NextFrameMs));
        Assert.Equal((16.0, -1.0), (sampler.CollectionRow(3).PreviousFrameMs, sampler.CollectionRow(3).NextFrameMs));
        Assert.Equal((16.0, 16.0), (sampler.CollectionRow(4).PreviousFrameMs, sampler.CollectionRow(4).NextFrameMs));
        var spread = sampler.PauseOverNeighboursSpread();
        Assert.Equal(2, spread.Count);
        Assert.Equal(40, spread.P50, 6);
        Assert.Equal(100, spread.Max, 6);
    }
}
