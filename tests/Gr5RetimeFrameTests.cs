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
            + "journey retime 1 update slowest 4.0 ms, commit 1 update slowest 1.5 ms, rebuild 1 update slowest 3.0 ms, hand-over 1 update slowest 2.0 ms (own update).", line);
        ledger.Reset();
        Assert.False(ledger.Observed);
        Assert.Equal(0, ledger.Updates(RetimeFrameStage.Release));
        Assert.Equal("", ledger.SlowestDetail(RetimeFrameStage.Release));
        ledger.Observe(RetimeFrameStage.Commit, double.NaN);
        Assert.Equal(0, ledger.SlowestMilliseconds(RetimeFrameStage.Commit));
        Assert.Contains("routes 0 updates, body release 0 updates", ledger.Describe());
    }

    [Fact]
    public void ObservingAndResettingTheLedgerAllocatesNothing()
    {
        var ledger = new RetimeFrameLedger();
        for (int i = 0; i < 64; i++) { ledger.Observe((RetimeFrameStage)(i % 6), i, "bindings"); ledger.Reset(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            ledger.Observe((RetimeFrameStage)(i % 6), i * 0.001, "bindings");
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
    public void TheRuntimeHandsTheRebuildOverInTheUpdateAfterItsLastStepAndLogsTheRetimeFrames()
    {
        string mod = Source(Path.Combine("Runtime", "PersistentCityMod.cs"));
        Assert.Contains("if (done && !rebuild.FinishedInLastStep) { FinishRouteRebuild(rebuild, mark, started); return; }", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Rebuild,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.HandOver,", mod);
        Assert.Contains("retimeFrames.Observe(RetimeFrameStage.Release, releaseElapsed, releaseStage);", mod);
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
