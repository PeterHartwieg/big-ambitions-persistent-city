using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetimeFrameTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeFrameTests(ITestOutputHelper output) { this.output = output; }

    private static string Hash(CityState state) { state.PrepareForSave(); return RushSpikeReplay.Sha(SaveCodec.Write(state)); }

    private static string Source(string file)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Runtime", "PersistentCityMod.cs"))) root = Path.GetDirectoryName(root)!;
        return File.ReadAllText(Path.Combine(root, file)).Replace("\r\n", "\n");
    }

    [Fact]
    public void TheLedgerNamesTheSlowestUpdateOfEachStageAndResets()
    {
        var ledger = new RetimeFrameLedger();
        ledger.Observe(RetimeFrameStage.Routes, 0.8);
        ledger.Observe(RetimeFrameStage.Release, 2.5, "bindings");
        ledger.Observe(RetimeFrameStage.Release, 9.26, "pedestrian pool");
        ledger.Observe(RetimeFrameStage.Release, 1.0, "ambient");
        ledger.Observe(RetimeFrameStage.Journeys, 4.0);
        ledger.Observe(RetimeFrameStage.Commit, 1.5);
        ledger.Observe(RetimeFrameStage.Rebuild, 3.0);
        ledger.Observe(RetimeFrameStage.HandOver, 2.0, "own update");
        Assert.Equal(RetimeFrameStage.Release, ledger.SlowestStage);
        Assert.Equal(3, ledger.Updates(RetimeFrameStage.Release));
        Assert.Equal(9.26, ledger.SlowestMilliseconds(RetimeFrameStage.Release));
        Assert.Equal("pedestrian pool", ledger.SlowestDetail(RetimeFrameStage.Release));
        string line = ledger.Describe();
        output.WriteLine(line);
        Assert.Equal("Clock retime frames: slowest update 9.3 ms in body release; routes 1 update slowest 0.8 ms, body release 3 updates slowest 9.3 ms (pedestrian pool), "
            + "journey retime 1 update slowest 4.0 ms, commit 1 update slowest 1.5 ms, rebuild 1 update slowest 3.0 ms, hand-over 1 update slowest 2.0 ms (own update); no update over 11.3 ms.", line);
        ledger.Reset();
        Assert.False(ledger.Observed);
        Assert.Equal(0, ledger.Updates(RetimeFrameStage.Release));
        Assert.Equal("", ledger.SlowestDetail(RetimeFrameStage.Release));
        ledger.Observe(RetimeFrameStage.Commit, double.NaN);
        Assert.Equal(0, ledger.SlowestMilliseconds(RetimeFrameStage.Commit));
        Assert.Contains("routes 0 updates, body release 0 updates", ledger.Describe());
    }

    [Fact]
    public void TheLedgerKeepsTheFrameOfEveryUpdateOverTheBound()
    {
        var ledger = new RetimeFrameLedger();
        ledger.Observe(RetimeFrameStage.Commit, 15.839, "", 42_134);
        ledger.Observe(RetimeFrameStage.Commit, 12.0, "", 42_140);
        ledger.Observe(RetimeFrameStage.Release, 11.3, "bindings", 42_100);
        ledger.Observe(RetimeFrameStage.HandOver, 11.4, "with the last rebuild step", 42_200);
        Assert.Equal(3, ledger.OverBound);
        Assert.Equal(42_134, ledger.OverBoundFrame(0));
        Assert.Equal(42_140, ledger.OverBoundFrame(1));
        Assert.Equal(RetimeFrameStage.Commit, ledger.OverBoundStage(1));
        Assert.Equal(12.0, ledger.OverBoundMilliseconds(1));
        Assert.Equal(RetimeFrameStage.HandOver, ledger.OverBoundStage(2));
        string line = ledger.Describe();
        output.WriteLine(line);
        Assert.EndsWith("; updates over 11.3 ms: frame 42134 commit 15.8 ms, frame 42140 commit 12.0 ms, frame 42200 hand-over 11.4 ms (with the last rebuild step).", line);
        for (int i = 0; i < 10; i++) ledger.Observe(RetimeFrameStage.Rebuild, 20 + i, "", 50_000 + i);
        Assert.Equal(13, ledger.OverBound);
        Assert.Equal(50_004, ledger.OverBoundFrame(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.OverBoundFrame(8));
        Assert.EndsWith(", frame 50004 rebuild 24.0 ms and 5 more.", ledger.Describe());
        ledger.Reset();
        Assert.Equal(0, ledger.OverBound);
        Assert.EndsWith("; no update over 11.3 ms.", ledger.Describe());
    }

    [Fact]
    public void ObservingAndResettingTheLedgerAllocatesNothing()
    {
        var ledger = new RetimeFrameLedger();
        for (int i = 0; i < 64; i++) { ledger.Observe((RetimeFrameStage)(i % 6), i, "bindings", i); ledger.Reset(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            ledger.Observe((RetimeFrameStage)(i % 6), i * 0.01, "bindings", i);
            if (i % 50 == 49) ledger.Reset();
        }
        ledger.Reset(); ledger.Reset();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void TheFinishingStepIsMarkedAndALaterHandOverIsTimedAsItsOwnUpdate()
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.PeopleCity();
        var driver = new ClockRetimeDriver();
        while (driver.Update(state, 2, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
        var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
        int steps = 0;
        while (!rebuild.Step(0)) { Assert.False(rebuild.FinishedInLastStep); steps++; }
        Assert.True(rebuild.FinishedInLastStep);
        int updates = rebuild.Updates;
        double stepped = rebuild.Milliseconds, slowestStep = rebuild.SlowestUpdateMilliseconds;
        Assert.True(rebuild.Step(0));
        Assert.False(rebuild.FinishedInLastStep);
        Assert.Equal(updates, rebuild.Updates);
        Assert.Equal(stepped, rebuild.Milliseconds);
        var result = rebuild.TakeResult();
        Assert.True(rebuild.HandOverSeparate);
        Assert.True(rebuild.HandOverMilliseconds > 0);
        Assert.Equal(stepped + rebuild.HandOverMilliseconds, rebuild.Milliseconds, 9);
        Assert.Equal(Math.Max(slowestStep, rebuild.HandOverMilliseconds), rebuild.SlowestUpdateMilliseconds, 9);
        Assert.Equal(updates, rebuild.Updates);
        Assert.True(result.ActiveJourneyIndexMatchesState());
        output.WriteLine("steps " + steps + ", updates " + updates + ", slowest step " + slowestStep.ToString("0.000") + " ms, hand-over "
            + rebuild.HandOverMilliseconds.ToString("0.000") + " ms in its own update (.NET)");
    }

    [Fact]
    public void AHandOverInTheFinishingUpdateStillAddsToThatUpdate()
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.PeopleCity();
        var driver = new ClockRetimeDriver();
        while (driver.Update(state, 2, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
        var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
        Assert.True(rebuild.Step(long.MaxValue));
        Assert.True(rebuild.FinishedInLastStep);
        double stepped = rebuild.Milliseconds;
        rebuild.TakeResult();
        Assert.False(rebuild.HandOverSeparate);
        Assert.Equal(stepped + rebuild.HandOverMilliseconds, rebuild.SlowestUpdateMilliseconds, 9);
    }

    [Fact]
    public void TakingTheRebuildOneUpdateLaterKeepsTheCityAndSaveIdentical()
    {
        string Run(bool deferred)
        {
            var (state, city) = Gr5RetimeRepeatAllocationTests.PeopleCity();
            var driver = new ClockRetimeDriver();
            var hashes = new List<string>();
            foreach (double rate in new[] { 2.0, 1, 2, 1 })
            {
                while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
                var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
                while (!rebuild.Step(0)) { }
                if (deferred) { Assert.True(rebuild.Step(0)); Assert.False(rebuild.FinishedInLastStep); }
                city = rebuild.TakeResult();
                Assert.Equal(deferred, rebuild.HandOverSeparate);
                Assert.True(city.ActiveJourneyIndexMatchesState());
                hashes.Add(Hash(state));
                city.AdvanceTo(state.Minute + 3, int.MaxValue);
            }
            city.AdvanceTo(state.Minute + 30, int.MaxValue);
            hashes.Add(Hash(state));
            return string.Join("|", hashes);
        }
        Assert.Equal(Run(deferred: false), Run(deferred: true));
    }

    [Fact]
    public void AtTheFinishingFrameBoundaryOnlyTheReplacementSimulatorIsRetained()
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.PeopleCity();
        var steady = new RetainedCensus();
        steady.Add("runtime.simulation", city);
        var driver = new ClockRetimeDriver();
        while (driver.Update(state, 2, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
        var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
        while (!rebuild.Step(0)) { }
        var deferred = new RetainedCensus();
        deferred.Add("runtime.simulation", city);
        deferred.Add("runtime.routeRebuild", rebuild);
        var replacement = rebuild.TakeResult();
        city = null!;
        var published = new RetainedCensus();
        published.Add("runtime.simulation", replacement);
        published.Add("runtime.retimeReport", rebuild);
        Assert.Equal(1, steady.ObjectsOf(typeof(CitySimulator)));
        Assert.Equal(2, deferred.ObjectsOf(typeof(CitySimulator)));
        Assert.Equal(1, published.ObjectsOf(typeof(CitySimulator)));
        Assert.Equal(0, published.Failures);
        Assert.True(published.Bytes < deferred.Bytes, "published " + published.Bytes + ", deferred " + deferred.Bytes);
        Assert.True(replacement.ActiveJourneyIndexMatchesState());
        output.WriteLine("retained at the finishing frame boundary: steady " + steady.Bytes + " B, hand-over deferred to the next update " + deferred.Bytes
            + " B (" + (deferred.Bytes - steady.Bytes).ToString("+0;-0") + "), published in the finishing update " + published.Bytes + " B (" + (published.Bytes - steady.Bytes).ToString("+0;-0") + ") (.NET)");
    }

    [Fact]
    public void ExpensiveMaintenanceIsNotChargedToTheFirstUnit()
    {
        var seams = new OptionalStopUnitSeams();
        seams.BeginCall(1_000_000);
        seams.Maintained(25.0, 1_000_000 + 24_000_000);
        seams.EndUnitAt("citizen", 25.6, 1_000_000 + 24_500_000);
        seams.EndUnitAt("traffic", 26.0, 1_000_000 + 24_800_000);
        seams.EndCall();
        Assert.Equal(25.0, seams.MaintenanceMilliseconds);
        Assert.Equal(24.0, seams.MaintenanceCpuMilliseconds, 9);
        Assert.Equal("citizen", seams.SlowestUnitKind);
        Assert.Equal(0.6, seams.SlowestUnitMilliseconds, 9);
        Assert.Equal(0.5, seams.SlowestUnitCpuMilliseconds, 9);
        seams.BeginCall(-1);
        seams.Maintained(30.0, -1);
        seams.EndUnitAt("citizen", 31.0, -1);
        seams.EndCall();
        Assert.Equal(30.0, seams.MaintenanceMilliseconds);
        Assert.Equal(-1, seams.MaintenanceCpuMilliseconds);
        Assert.Equal(1.0, seams.SlowestUnitMilliseconds, 9);
        Assert.Equal(-1, seams.SlowestUnitCpuMilliseconds);
        seams.BeginCall();
        Assert.Equal(0, seams.MaintenanceMilliseconds);
        Assert.Equal(-1, seams.MaintenanceCpuMilliseconds);
    }

    [Fact]
    public void TheRuntimeStartsBothUnitClocksAtTheCallAndRecordsMaintenanceApart()
    {
        string mod = Source(Path.Combine("Runtime", "PersistentCityMod.cs"));
        int entry = mod.IndexOf("long callCpu = unitCpu ? ThreadCpuClock.Nanoseconds() : -1;", StringComparison.Ordinal);
        int maintain = mod.IndexOf("stopEnvironment?.Maintain();", entry, StringComparison.Ordinal);
        int recorded = mod.IndexOf("unitSeams?.Maintained(unitStarted, unitCpu ? ThreadCpuClock.Nanoseconds() : -1);", maintain, StringComparison.Ordinal);
        Assert.True(entry > 0 && maintain > entry && recorded > maintain);
        Assert.Contains("bool unitCpu = unitSeams?.RecordsCpu == true && ThreadCpuClock.Available;", mod);
        Assert.Contains("unitSeams?.EndUnitAt(kind, elapsed, unitCpu ? ThreadCpuClock.Nanoseconds() : -1);", mod);
    }

    [Fact]
    public void TheRuntimePublishesTheReplacementInTheFinishingUpdateAndReportsInTheNext()
    {
        string mod = Source(Path.Combine("Runtime", "PersistentCityMod.cs"));
        Assert.Contains("if (done) { FinishRouteRebuild(rebuild, mark, started, \"with the last rebuild step\", reportNow: false); return; }", mod);
        Assert.Contains("FinishRouteRebuild(rebuild, RetimeMark(), Stopwatch.GetTimestamp(), \"save\", reportNow: true);", mod);
        Assert.Contains("if (retimeReport != null) { ReportRetime(); WaitForRetime(); return; }", mod);
        int finish = mod.IndexOf("private void FinishRouteRebuild(RouteRebuild rebuild, RetimeAllocationMark mark", StringComparison.Ordinal);
        int report = mod.IndexOf("private void ReportRetime()", StringComparison.Ordinal);
        string finishing = mod.Substring(finish, report - finish);
        Assert.Contains("simulation = rebuild.TakeResult();", finishing);
        Assert.DoesNotContain("log?.Info", finishing);
        Assert.Contains("retimeReport = null;", Source(Path.Combine("Runtime", "PopulationLifecycle.cs")));
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Rebuild,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.HandOver,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Release, releaseElapsed, releaseStage, Time.frameCount);", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Commit,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Journeys,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Routes,", mod);
        Assert.Contains("log?.Info(retimeFrames.Describe());", mod);
        Assert.Contains("retimeAllocation.Reset(); retimeFrames.Reset();", mod);
        Assert.Contains("retimeAllocation.Reset(); retimeFrames.Reset();", Source(Path.Combine("Runtime", "PopulationLifecycle.cs")));
        Assert.Contains("if (routeRebuild != null) FinishRouteRebuild(routeRebuild);", mod);
    }

    [Fact]
    public void TheSlowestUnitKeepsItsOwnThreadCpuTime()
    {
        var seams = new OptionalStopUnitSeams();
        seams.BeginCall();
        seams.EndUnit("citizen", 2.0, 1.5);
        seams.EndUnit("citizen", 30.5, 5.25);
        seams.EndUnit("traffic", 0.5, 0.5);
        seams.EndCall();
        Assert.Equal(30.5, seams.SlowestUnitMilliseconds);
        Assert.Equal(5.25, seams.SlowestUnitCpuMilliseconds);
        seams.BeginCall();
        Assert.Equal(-1, seams.SlowestUnitCpuMilliseconds);
        seams.EndUnit("citizen", 3.0);
        seams.EndUnit("other", 1.0, 0.9);
        seams.EndCall();
        Assert.Equal(-1, seams.SlowestUnitCpuMilliseconds);
        seams.BeginCall();
        seams.EndUnit("citizen", 3.0, double.NaN);
        Assert.Equal(-1, seams.SlowestUnitCpuMilliseconds);
        seams.EndUnit("citizen", 4.0, 3.5);
        seams.EndCall();
        Assert.Equal(3.5, seams.SlowestUnitCpuMilliseconds);
    }

    [Fact]
    public void RecordingUnitThreadCpuAllocatesNothingAfterWarmUp()
    {
        var seams = new OptionalStopUnitSeams();
        for (int call = 0; call < 8; call++) { seams.BeginCall(); for (int i = 0; i < 128; i++) { seams.Seam(0.01); seams.EndUnit("citizen", i * 0.01, i * 0.005); } seams.EndCall(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int call = 0; call < 200; call++)
        {
            seams.BeginCall();
            for (int i = 0; i < 128; i++) { seams.Seam(0.01); seams.EndUnit(i % 2 == 0 ? "citizen" : "traffic", i * 0.01, i * 0.005); }
            seams.EndCall();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
