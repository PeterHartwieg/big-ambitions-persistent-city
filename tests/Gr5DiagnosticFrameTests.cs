using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5DiagnosticFrameTests
{
    private readonly ITestOutputHelper output;
    public Gr5DiagnosticFrameTests(ITestOutputHelper output) { this.output = output; }

    private static readonly double[] CollectionFrames = { 48.0, 55.5, 52.25, 57.0, 50.5, 54.0, 53.75, 420.0 };
    private const double PlainFrame = 33.5;

    private static GcPauseSampler Replay(bool markLast, out double stamp)
    {
        var sampler = new GcPauseSampler(4096);
        sampler.Start(1_300_000_000);
        int gen0 = 100, marks = 7;
        stamp = 0;
        sampler.Observe(stamp, gen0, 0, 0, 1_300_000_000, 1, marks, "");
        long frame = 2;
        for (int c = 0; c < CollectionFrames.Length; c++)
        {
            for (int i = 0; i < 300; i++) { stamp += PlainFrame + (i % 3 == 0 ? 0.4 : 0); sampler.Observe(stamp, gen0, 0, 0, 1_300_000_000 + i, frame++, marks, "probe status"); }
            bool tooling = markLast && c == CollectionFrames.Length - 1;
            if (tooling) marks++;
            stamp += CollectionFrames[c]; gen0++;
            sampler.Observe(stamp, gen0, 0, 0, 1_200_000_000, frame++, new DiagnosticMark(marks, tooling ? "probe measure-stop" : "probe status"));
        }
        stamp += PlainFrame;
        sampler.Observe(stamp, gen0, 0, 0, 1_200_000_100, frame, marks, "probe status");
        return sampler;
    }

    [Fact]
    public void AMarkedCollectionFrameLeavesThePauseAndIsReportedOnItsOwn()
    {
        var before = Replay(markLast: false, out _);
        var after = Replay(markLast: true, out _);
        output.WriteLine("synthetic collection frames, pause p95 / max ms: unmarked " + before.PauseSpread().P95 + " / " + before.PauseSpread().Max
            + "; last frame marked " + after.PauseSpread().P95 + " / " + after.PauseSpread().Max + " (with it " + after.PauseIncludingDiagnosticSpread().P95 + ")");
        Assert.True(before.PauseSpread().P95 > 380, "red: an unmarked 420 ms collection frame sets the window's pause p95");
        Assert.Equal(8, after.Collections(0));
        Assert.Equal(8, after.CollectionFrames);
        Assert.Equal(1, after.DiagnosticFrameCount);
        Assert.Equal(1, after.DiagnosticCollectionFrames);
        Assert.Equal(7, after.PauseSpread().Count);
        Assert.Equal(57.0 - after.FrameSpread(false).P50, after.PauseSpread().P95, 3);
        Assert.Equal(before.PauseSpread().P95, after.PauseIncludingDiagnosticSpread().P95);
        Assert.Equal(1, after.DiagnosticFrameSpread().Count);
        Assert.Equal(420.0, after.DiagnosticFrameSpread().Max, 3);
        Assert.Equal("probe measure-stop", after.CollectionRow(7).Diagnostic);
        Assert.Equal("", after.CollectionRow(0).Diagnostic);
        Assert.Equal(before.FrameSpread(false).P50, after.FrameSpread(false).P50);
        Assert.Equal(7, after.PauseOverNeighboursSpread().Count);
    }

    [Fact]
    public void TheReportKeepsEveryCollectionAndNamesTheDiagnosticOne()
    {
        var sampler = Replay(markLast: true, out _);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 0, true, 1_400_000_000, 1_300_000_000)));
        var root = report.RootElement;
        Assert.Equal(8, root.GetProperty("gen0").GetInt32());
        Assert.Equal(1, root.GetProperty("diagnostic_frames").GetInt32());
        Assert.Equal(1, root.GetProperty("diagnostic_collection_frames").GetInt32());
        Assert.Equal(7, root.GetProperty("pause_ms").GetProperty("count").GetInt32());
        Assert.Equal(8, root.GetProperty("pause_ms_including_diagnostic").GetProperty("count").GetInt32());
        var rows = root.GetProperty("collection_rows").EnumerateArray().ToArray();
        Assert.Equal(8, rows.Length);
        Assert.Equal("probe measure-stop", rows[7].GetProperty("diagnostic").GetString());
        Assert.All(rows.Take(7), r => Assert.Equal("", r.GetProperty("diagnostic").GetString()));
        Assert.Contains("diagnostic frames 1 (1 with a collection", sampler.ReportHuman(new GcReportContext(1000, 2000, null, 0, true, 1, 1)));
    }

    [Fact]
    public void AMarkBeforeStartOrBeforeARebaselineIsNotChargedToTheNextFrame()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.Observe(0, 1, 0, 0, 1, 1, 5, "pc gc start");
        sampler.Observe(34, 2, 0, 0, 1, 2, 5, "pc gc start");
        Assert.Equal(0, sampler.DiagnosticFrameCount);
        sampler.Observe(68, 2, 0, 0, 1, 3, 6, "pc gc report");
        Assert.Equal(1, sampler.DiagnosticFrameCount);
        sampler.Rebaseline();
        sampler.Observe(500, 3, 0, 0, 1, 4, 7, "pc gc report");
        sampler.Observe(534, 4, 0, 0, 1, 5, 7, "pc gc report");
        Assert.Equal(1, sampler.DiagnosticFrameCount);
        Assert.Equal(2, sampler.CollectionFrames);
        Assert.Equal(0, sampler.DiagnosticCollectionFrames);
        sampler.Observe(568, 4, 0, 0, 1, 6);
        sampler.Observe(602, 5, 0, 0, 1, 7);
        Assert.Equal(1, sampler.DiagnosticFrameCount);
    }

    [Fact]
    public void ObservingFramesWithMarksAllocatesNothing()
    {
        var sampler = new GcPauseSampler(8192);
        sampler.Start(1);
        sampler.Observe(0, 0, 0, 0, 1, 0, 0, "probe status");
        sampler.Observe(16, 0, 0, 0, 1, 1, 1, "probe status");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i < 8000; i++) sampler.Observe(i * 16.0, i / 50, 0, 0, i, i, i / 7, "probe status");
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(sampler.DiagnosticFrameCount > 1000);
        Assert.True(sampler.DiagnosticCollectionFrames > 0);
    }

    [Fact]
    public void MarksFromAnyThreadAreCountedAndKeepTheirSource()
    {
        int start = DiagnosticFrames.Marks;
        System.Threading.Tasks.Parallel.For(0, 64, _ => DiagnosticFrames.Mark("probe page"));
        Assert.True(DiagnosticFrames.Marks - start >= 64);
        Assert.False(string.IsNullOrEmpty(DiagnosticFrames.LastSource));
    }

    [Fact]
    public void AReadPairsEachMarkSequenceWithTheSourceThatPublishedIt()
    {
        const int writers = 4, perWriter = 20000;
        var sources = new string[writers * perWriter];
        for (int i = 0; i < sources.Length; i++) sources[i] = "pc writer " + i;
        var parallelSources = new System.Collections.Generic.HashSet<string>(sources, StringComparer.Ordinal);
        var bySequence = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
        int mismatches = 0, reads = 0, parallelReads = 0, awaited = -1;
        using var done = new System.Threading.ManualResetEventSlim();
        using var reading = new System.Threading.ManualResetEventSlim();
        using var observed = new System.Threading.ManualResetEventSlim();
        using var parallelVerified = new System.Threading.ManualResetEventSlim();
        var reader = new System.Threading.Thread(() =>
        {
            var seen = new System.Collections.Generic.List<DiagnosticMark>(1 << 16);
            DiagnosticMark pending = default;
            bool hasPending = false;
            reading.Set();
            while (!done.IsSet)
            {
                var mark = DiagnosticFrames.Current;
                if (hasPending && bySequence.TryGetValue(pending.Sequence, out var confirmed))
                {
                    hasPending = false;
                    if (!ReferenceEquals(confirmed, pending.Source)) mismatches++;
                    parallelVerified.Set();
                }
                if (!mark.Source.StartsWith("pc writer ", StringComparison.Ordinal)) continue;
                if ((seen.Count == 0 || seen[seen.Count - 1].Sequence != mark.Sequence) && seen.Count < seen.Capacity) seen.Add(mark);
                if (mark.Sequence == System.Threading.Volatile.Read(ref awaited)) observed.Set();
                if (!parallelVerified.IsSet && parallelSources.Contains(mark.Source)) { pending = mark; hasPending = true; }
            }
            foreach (var mark in seen)
            {
                if (mark.Source.Length == 0 || !mark.Source.StartsWith("pc writer ", StringComparison.Ordinal)) continue;
                if (!bySequence.TryGetValue(mark.Sequence, out var expected)) continue;
                reads++;
                if (parallelSources.Contains(mark.Source)) parallelReads++;
                if (!ReferenceEquals(expected, mark.Source)) mismatches++;
            }
        });
        reader.Start();
        Assert.True(reading.Wait(TimeSpan.FromSeconds(30)), "the reader started");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        for (int attempt = 0; !observed.IsSet && DateTime.UtcNow < deadline; attempt++)
        {
            string source = "pc writer handshake " + attempt;
            DiagnosticFrames.Mark(source);
            var published = DiagnosticFrames.Current;
            if (!ReferenceEquals(published.Source, source)) continue;
            bySequence[published.Sequence] = source;
            System.Threading.Volatile.Write(ref awaited, published.Sequence);
            observed.Wait(TimeSpan.FromMilliseconds(50));
        }
        if (!observed.IsSet) { done.Set(); reader.Join(); }
        Assert.True(observed.IsSet, "the reader recorded a published writer sequence before the writers ran");
        var hooks = System.Threading.Tasks.Parallel.For(0, writers, w =>
        {
            for (int i = 0; i < perWriter || (!parallelVerified.IsSet && DateTime.UtcNow < deadline); i++)
            {
                string source = sources[w * perWriter + i % perWriter];
                DiagnosticFrames.Mark(source);
                var after = DiagnosticFrames.Current;
                if (ReferenceEquals(after.Source, source)) bySequence[after.Sequence] = source;
            }
        });
        done.Set(); reader.Join();
        var final = DiagnosticFrames.Current;
        Assert.True(hooks.IsCompleted);
        Assert.Equal(final.Sequence, DiagnosticFrames.Marks);
        Assert.True(parallelVerified.IsSet, "the reader verified a mark published by a parallel writer while the writers ran");
        Assert.True(reads > 0, "the reader saw a writer's mark");
        output.WriteLine(reads + " concurrent reads paired with their publishing source, " + parallelReads + " from the parallel writers");
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void ReadingTheMarkSnapshotAllocatesNothing()
    {
        DiagnosticFrames.Mark("pc gc report");
        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int i = 0; i < 10000; i++) { var mark = DiagnosticFrames.Current; total += mark.Sequence + mark.Source.Length; }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.NotEqual(0, total);
    }
    [Fact]
    public void ACollectionFrameNamesTheAsyncBodyGrowOutcomeOverTheWholeInterval()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        double at = 0;
        int gen0 = 0;
        long started = 0, cancelled = 0;
        void Step(bool pending, bool collect) { sampler.NoteBodies(10, 4, new GcGrowSample(pending, started, cancelled)); at += collect ? 60 : 34; if (collect) gen0++; sampler.Observe(at, gen0, 0, 0, 1, (long)at); }
        Step(false, false);
        started++; Step(true, false);
        Step(true, true);
        Step(false, true);
        started++; Step(false, true);
        started++; Step(true, true);
        Step(false, true);
        Step(false, true);
        started++; Step(true, false);
        cancelled++; Step(false, true);
        started++; cancelled++; Step(false, true);
        started++; Step(true, false);
        cancelled++; started++; Step(false, true);
        started++; Step(true, false);
        cancelled++; started++; Step(true, true);
        var expected = new[]
        {
            GcAsyncGrow.Pending, GcAsyncGrow.Integrated, GcAsyncGrow.Integrated, GcAsyncGrow.Pending, GcAsyncGrow.Integrated, GcAsyncGrow.None,
            GcAsyncGrow.Cancelled, GcAsyncGrow.Cancelled, GcAsyncGrow.Unknown, GcAsyncGrow.Cancelled,
        };
        Assert.Equal(expected, Enumerable.Range(0, sampler.CollectionRowCount).Select(i => sampler.CollectionRow(i).AsyncGrow).ToArray());
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, true, 1, 1)));
        Assert.Equal(new[] { "pending", "integrated", "integrated", "pending", "integrated", "none", "cancelled", "cancelled", "unknown", "cancelled" },
            report.RootElement.GetProperty("collection_rows").EnumerateArray().Select(r => r.GetProperty("async_grow").GetString()).ToArray());
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++) { if (i % 3 == 0) started++; Step(i % 3 == 0, i % 50 == 0); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void ACollectionBeforeGcFrameBeginCarriesTheCumulativeGen0CountForTheJoin()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.Observe(0, 5, 0, 0, 1, 100);
        sampler.Observe(60, 6, 0, 0, 1, 101);
        sampler.Observe(94, 6, 0, 0, 1, 102);
        sampler.Observe(150, 8, 0, 0, 1, 103);
        var early = sampler.CollectionRow(0);
        Assert.Equal(100, early.Frame);
        Assert.Equal(6, early.Gen0Through);
        Assert.Equal(1, early.Gen0);
        Assert.Equal(8, sampler.CollectionRow(1).Gen0Through);
        Assert.Equal(2, sampler.CollectionRow(1).Gen0);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, true, 1, 1)));
        var rows = report.RootElement.GetProperty("collection_rows");
        Assert.Equal(new long[] { 6, 8 }, rows.EnumerateArray().Select(r => r.GetProperty("gen0_through").GetInt64()).ToArray());
        Assert.Equal(new long[] { 100, 102 }, rows.EnumerateArray().Select(r => r.GetProperty("frame").GetInt64()).ToArray());
        var direct = new GcPauseSampler(8);
        direct.Start(1);
        direct.Frame(60, 0, 1, 0, 0, 1);
        Assert.Equal(-1, direct.CollectionRow(0).Gen0Through);
        var split = new GcPauseSampler(64);
        split.Start(1);
        split.Observe(0, 5, 0, 0, 1, 600);
        split.Observe(33, 6, 0, 0, 1, 601);
        split.Observe(66, 7, 0, 0, 1, 602);
        Assert.Equal(2, split.CollectionRowCount);
        Assert.Equal((600L, 1, 6L), (split.CollectionRow(0).Frame, split.CollectionRow(0).Gen0, split.CollectionRow(0).Gen0Through));
        Assert.Equal((601L, 1, 7L), (split.CollectionRow(1).Frame, split.CollectionRow(1).Gen0, split.CollectionRow(1).Gen0Through));
    }

    [Fact]
    public void AGrowCancelledWhileItsPoolIsReleasedStillReadsCancelledAfterThePoolIsGone()
    {
        var ledger = new GcGrowLedger();
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        double at = 0;
        int gen0 = 0;
        void Step(GcGrowSample grow, bool collect) { sampler.NoteBodies(10, 4, grow); at += collect ? 60 : 34; if (collect) gen0++; sampler.Observe(at, gen0, 0, 0, 1, (long)at); }
        ledger.Start();
        Step(ledger.Sample(true), false);
        Step(ledger.Sample(true), false);
        ledger.CancelPending();
        Step(ledger.Sample(false), true);
        Step(ledger.Sample(false), false);
        ledger.Start(); ledger.CancelPending();
        Step(ledger.Sample(false), true);
        ledger.Start();
        Step(ledger.Sample(true), false);
        Step(ledger.Sample(true), true);
        Assert.Equal(new[] { GcAsyncGrow.Cancelled, GcAsyncGrow.Cancelled, GcAsyncGrow.Pending },
            Enumerable.Range(0, sampler.CollectionRowCount).Select(i => sampler.CollectionRow(i).AsyncGrow).ToArray());
        Assert.Equal(GcAsyncGrow.Unknown, GcGrowSample.Outcome(new GcGrowSample(true, 3, 0), default));
    }

    [Fact]
    public void ACollectionRowWithoutAGrowSampleSaysUnknown()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.NoteBodies(1, 1);
        sampler.Observe(0, 0, 0, 0, 1, 1);
        sampler.Observe(60, 1, 0, 0, 1, 2);
        Assert.Equal(GcAsyncGrow.Unknown, sampler.CollectionRow(0).AsyncGrow);
        Assert.Equal(GcAsyncGrow.Unknown, GcGrowSample.Outcome(new GcGrowSample(false, 5, 0), new GcGrowSample(false, 4, 0)));
    }

    [Fact]
    public void ACollectionRowCarriesTheTwoSamplerBoundaryStampsThatBoundItsInterval()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.Observe(1000.25, 0, 0, 0, 1, 40);
        sampler.Observe(1016.5, 0, 0, 0, 1, 41);
        sampler.Observe(1071.125, 1, 0, 0, 1, 42);
        var row = sampler.CollectionRow(0);
        Assert.Equal(41, row.Frame);
        Assert.Equal(1016.5, row.BeginStampMs, 6);
        Assert.Equal(1071.125, row.EndStampMs, 6);
        Assert.Equal(row.EndStampMs - row.BeginStampMs, row.FrameMs, 6);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, true, 1, 1)));
        var json = report.RootElement.GetProperty("collection_rows")[0];
        Assert.Equal(1016.5, json.GetProperty("begin_stamp_ms").GetDouble(), 3);
        Assert.Equal(1071.125, json.GetProperty("end_stamp_ms").GetDouble(), 3);
    }

    [Fact]
    public void LateUpdateIsKeptApartFromUpdateCityAndBothPausesAreReported()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.LateModWork(9);
        sampler.Observe(0, 0, 0, 0, 1, 1);
        sampler.ModWork(2); sampler.LateModWork(1.5); sampler.LateModWork(-4);
        sampler.Observe(34, 0, 0, 0, 1, 2);
        sampler.ModWork(1); sampler.LateModWork(6);
        sampler.Observe(94, 1, 0, 0, 1, 3);
        Assert.Equal(2, sampler.ModSpread(false).Max, 6);
        Assert.Equal(1.5, sampler.LateSpread(false).Max, 6);
        Assert.Equal(3.5, sampler.ModWorkSpread(false).Max, 6);
        Assert.Equal(1, sampler.CollectionRow(0).ModMs, 6);
        Assert.Equal(6, sampler.CollectionRow(0).LateMs, 6);
        Assert.Equal(60 - 34 - 1, sampler.PauseWithoutModSpread().Max, 3);
        Assert.Equal(60 - 34 - 7, sampler.PauseWithoutModWorkSpread().Max, 3);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, true, 1, 1)));
        var root = report.RootElement;
        Assert.Equal(25, root.GetProperty("pause_ms_without_mod").GetProperty("max").GetDouble(), 3);
        Assert.Equal(19, root.GetProperty("pause_ms_without_mod_work").GetProperty("max").GetDouble(), 3);
        Assert.Equal(6, root.GetProperty("collection_rows")[0].GetProperty("late_ms").GetDouble(), 3);
        Assert.Equal(1, root.GetProperty("collection_rows")[0].GetProperty("mod_ms").GetDouble(), 3);
        Assert.Equal(6, root.GetProperty("max_late_ms").GetDouble(), 3);
        Assert.Contains("outside UpdateCity p95/max=25.00/25.00 ms, outside UpdateCity and LateUpdate p95/max=19.00/19.00 ms", sampler.ReportHuman(new GcReportContext(1, 1, null, 0, true, 1, 1)));
    }
}
