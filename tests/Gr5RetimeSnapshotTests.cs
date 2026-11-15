using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RetimeSnapshotTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeSnapshotTests(ITestOutputHelper output) { this.output = output; }

    private static (CityState State, CitySimulator Simulator) City()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        simulator.AdvanceTo(state.Minute + 0.25, int.MaxValue);
        return (state, simulator);
    }

    private static double Milliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

    [Fact]
    public void ASlicedSaveSnapshotWritesTheSameSaveAsTheWholeOne()
    {
        var (whole, _) = City();
        var (sliced, _) = City();
        Assert.NotNull(whole.Traffic);
        whole.PrepareForSave();
        var steps = sliced.PrepareForSaveInSteps(16);
        int count = 0;
        while (true) { Assert.True(steps.MoveNext()); count++; if (steps.Current) break; }
        Assert.True(count > 100, count.ToString());
        Assert.NotSame(whole.Traffic, sliced.Traffic);
        whole.SnapshotSynchronizer = null;
        sliced.SnapshotSynchronizer = null;
        var capturedTraffic = sliced.Traffic;
        Assert.Equal(RushSpikeReplay.Sha(SaveCodec.Write(whole)), RushSpikeReplay.Sha(SaveCodec.Write(sliced)));
        Assert.Same(capturedTraffic, sliced.Traffic);
        sliced.Traffic!.Links[sliced.Traffic.Links.Count / 2].NextDischargeAt += 0.5;
        Assert.NotEqual(RushSpikeReplay.Sha(SaveCodec.Write(whole)), RushSpikeReplay.Sha(SaveCodec.Write(sliced)));
    }

    [Fact]
    public void AnAccountingBoundaryDuringTheSnapshotRestartsItAtTheNewClock()
    {
        var (state, simulator) = City();
        var driver = new ClockRetimeDriver();
        double rate = state.TravelClockRate * 2;
        Assert.Equal(ClockRetimeOutcome.Preparing, driver.Update(state, rate, false, 2, 0));
        Assert.Null(driver.Pending);
        Assert.True(driver.Holding);
        simulator.AdvanceTo(state.Minute + 0.1, int.MaxValue);
        ClockRetimeOutcome outcome;
        do outcome = driver.Update(state, rate, false, 2, 0);
        while (outcome == ClockRetimeOutcome.Preparing && driver.Pending == null);
        Assert.Equal(ClockRetimeOutcome.Restarted, outcome);
        Assert.Equal(1, driver.Restarts);
        Assert.Equal(state.Minute, state.Traffic!.Clock);
    }

    [Fact]
    public void AnUnfinishedSnapshotLeavesTheSavedTrafficUntouched()
    {
        var (state, _) = City();
        state.PrepareForSave();
        var saved = state.Traffic;
        var driver = new ClockRetimeDriver();
        Assert.Equal(ClockRetimeOutcome.Preparing, driver.Update(state, state.TravelClockRate * 2, false, 2, 0));
        Assert.Same(saved, state.Traffic);
        Assert.Equal(ClockRetimeOutcome.None, driver.Update(state, state.TravelClockRate, false, 2, 0));
        Assert.Same(saved, state.Traffic);
        Assert.False(driver.Holding);
    }

    private static (List<double> Updates, int Slices) SnapshotUpdates(CityState state, double budget)
    {
        var driver = new ClockRetimeDriver();
        var updates = new List<double>();
        ClockRetimeOutcome outcome;
        do
        {
            long started = Stopwatch.GetTimestamp();
            outcome = driver.Update(state, state.TravelClockRate * 2, false, 2, budget);
            updates.Add(Milliseconds(started));
        }
        while (outcome == ClockRetimeOutcome.Preparing && driver.Pending == null);
        Assert.Equal(ClockRetimeOutcome.Began, outcome);
        int slices = driver.SnapshotSlices;
        driver.Cancel();
        return (updates, slices);
    }

    [Fact]
    public void TheRetimeSnapshotRunsInSlicesUnderTheFrameBudget()
    {
        var (state, _) = City();
        long started = Stopwatch.GetTimestamp();
        state.PrepareForSave();
        double whole = Milliseconds(started);
        var (stepped, slices) = SnapshotUpdates(state, 0);
        Assert.True(slices >= TravelTimingMaximumRouteTests.FixtureLinks / ClockRetimeDriver.SnapshotLinksPerStep, slices.ToString());
        var (updates, _) = SnapshotUpdates(state, ClockRetimeStages.SliceMilliseconds);
        output.WriteLine($"unsliced PrepareForSave {whole:0.0} ms; zero budget {stepped.Count} updates, slowest {stepped.Max():0.00} ms; "
            + $"4 ms budget {updates.Count} updates, slowest {updates.Max():0.0} ms, began update {updates[^1]:0.0} ms (.NET)");
        PerfGate.Under(updates.Max(), 30, "slowest retime snapshot update at 13,965 links");
        PerfGate.Under(stepped.Max(), 30, "slowest single snapshot step at 13,965 links");
    }

    private static IEnumerator<bool> Forgetting(IEnumerator<bool> steps, CityState state)
    {
        while (steps.MoveNext())
        {
            if (steps.Current) { state.TrafficSource = null; state.TrafficSourceState = null; }
            yield return steps.Current;
        }
    }

    private static (string Hash, long Bytes) RetimeTwice(bool live)
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        foreach (var rate in new[] { 3.0, 1 })
        {
            var driver = new ClockRetimeDriver();
            if (!live)
            {
                var original = state.SnapshotSteps!;
                state.SnapshotSteps = links => Forgetting(original(links), state);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (true)
            {
                var outcome = driver.Update(state, rate, false, 2, ClockRetimeStages.SliceMilliseconds);
                if (outcome != ClockRetimeOutcome.Committed) continue;
                simulator = simulator.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
                break;
            }
            if (rate != 1) continue;
            before = GC.GetAllocatedBytesForCurrentThread() - before;
            simulator.AdvanceTo(state.Minute + 0.5, int.MaxValue);
            state.PrepareForSave();
            return (RushSpikeReplay.Sha(SaveCodec.Write(state)), before);
        }
        throw new InvalidOperationException();
    }

    [Fact]
    public void RetimingFromTheLiveKernelMatchesRetimingFromARestoredCopy()
    {
        var (liveHash, liveBytes) = RetimeTwice(live: true);
        var (restoredHash, restoredBytes) = RetimeTwice(live: false);
        output.WriteLine($"retime 3 -> 1 at 13,965 links: restored copy {restoredBytes / 1e6:0.0} MB, live kernel {liveBytes / 1e6:0.0} MB (.NET)");
        Assert.Equal(restoredHash, liveHash);
        Assert.True(liveBytes + 4_000_000 < restoredBytes, liveBytes + " vs " + restoredBytes);
    }
}
