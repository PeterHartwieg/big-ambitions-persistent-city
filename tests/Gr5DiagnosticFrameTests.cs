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

    private static readonly double[] RepeatCollectionFrames = { 51.114, 63.173, 60.21, 60.914, 61.869, 64.676, 60.668, 642.998 };
    private const double PlainFrame = 33.757;

    private static GcPauseSampler Replay(bool markLast, out double stamp)
    {
        var sampler = new GcPauseSampler(4096);
        sampler.Start(1_300_000_000);
        int gen0 = 100, marks = 7;
        stamp = 0;
        sampler.Observe(stamp, gen0, 0, 0, 1_300_000_000, 1, marks, "");
        long frame = 2;
        for (int c = 0; c < RepeatCollectionFrames.Length; c++)
        {
            for (int i = 0; i < 300; i++) { stamp += PlainFrame + (i % 3 == 0 ? 0.4 : 0); sampler.Observe(stamp, gen0, 0, 0, 1_300_000_000 + i, frame++, marks, "probe status"); }
            bool tooling = markLast && c == RepeatCollectionFrames.Length - 1;
            if (tooling) marks++;
            stamp += RepeatCollectionFrames[c]; gen0++;
            sampler.Observe(stamp, gen0, 0, 0, 1_200_000_000, frame++, marks, tooling ? "probe measure-stop" : "probe status");
        }
        stamp += PlainFrame;
        sampler.Observe(stamp, gen0, 0, 0, 1_200_000_100, frame, marks, "probe status");
        return sampler;
    }

    [Fact]
    public void TheProbeStopFrameOfRig16vAImmediateLeavesThePauseAndIsReportedOnItsOwn()
    {
        var before = Replay(markLast: false, out _);
        var after = Replay(markLast: true, out _);
        output.WriteLine("16v A immediate collection frames, pause p95 / max ms: unmarked " + before.PauseSpread().P95 + " / " + before.PauseSpread().Max
            + "; probe stop frame marked " + after.PauseSpread().P95 + " / " + after.PauseSpread().Max + " (with it " + after.PauseIncludingDiagnosticSpread().P95 + ")");
        Assert.True(before.PauseSpread().P95 > 600, "red: the 643 ms probe stop frame sets the window's pause p95");
        Assert.Equal(8, after.Collections(0));
        Assert.Equal(8, after.CollectionFrames);
        Assert.Equal(1, after.DiagnosticFrameCount);
        Assert.Equal(1, after.DiagnosticCollectionFrames);
        Assert.Equal(7, after.PauseSpread().Count);
        Assert.True(after.PauseSpread().P95 < 36, "pause p95 " + after.PauseSpread().P95);
        Assert.Equal(before.PauseSpread().P95, after.PauseIncludingDiagnosticSpread().P95);
        Assert.Equal(1, after.DiagnosticFrameSpread().Count);
        Assert.Equal(642.998, after.DiagnosticFrameSpread().Max, 3);
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
    public void ACollectionFrameNamesAnAsyncBodyGrowThatFinishedInsideIt()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        double at = 0;
        int gen0 = 0;
        void Step(bool integrating, bool collect) { sampler.NoteBodies(10, 4, integrating); at += collect ? 60 : 34; if (collect) gen0++; sampler.Observe(at, gen0, 0, 0, 1, (long)at); }
        Step(false, false);
        Step(true, false);
        Step(true, true);
        Step(false, true);
        Step(false, true);
        Assert.Equal(GcAsyncGrow.Pending, sampler.CollectionRow(0).AsyncGrow);
        Assert.Equal(GcAsyncGrow.Integrated, sampler.CollectionRow(1).AsyncGrow);
        Assert.Equal(GcAsyncGrow.None, sampler.CollectionRow(2).AsyncGrow);
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, true, 1, 1)));
        Assert.Equal(new[] { "pending", "integrated", "none" }, report.RootElement.GetProperty("collection_rows").EnumerateArray().Select(r => r.GetProperty("async_grow").GetString()).ToArray());
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++) Step(i % 3 == 0, i % 50 == 0);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
    [Fact]
    public void LateUpdateWorkJoinsTheFramesModWorkAndLeavesThePauseWithoutModWork()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.LateModWork(9);
        sampler.Observe(0, 0, 0, 0, 1, 1);
        sampler.ModWork(2); sampler.LateModWork(1.5); sampler.LateModWork(-4);
        sampler.Observe(34, 0, 0, 0, 1, 2);
        sampler.ModWork(1); sampler.LateModWork(6);
        sampler.Observe(94, 1, 0, 0, 1, 3);
        Assert.Equal(3.5, sampler.ModSpread(false).Max, 6);
        Assert.Equal(7, sampler.CollectionRow(0).ModMs, 6);
        Assert.Equal(60 - 34 - 7, sampler.PauseWithoutModSpread().Max, 3);
    }
}
