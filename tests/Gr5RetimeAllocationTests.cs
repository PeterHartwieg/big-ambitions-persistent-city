using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RetimeAllocationTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeAllocationTests(ITestOutputHelper output) { this.output = output; }

    private const string Retimed = "222E663F2FD8440FF74517903D8F0E151B9DEAB5270102C90E679DB4D0B0F03C";

    private static (string Hash, long[] Bytes) RetimeThroughRates(bool committed)
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        if (committed) foreach (var c in state.Citizens) c.Journey = c.Journey!.Commit();
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        var rates = new[] { 3.0, 20, 1, 20 };
        var bytes = new long[rates.Length];
        for (int r = 0; r < rates.Length; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (true)
            {
                var outcome = driver.Update(state, rates[r], false, 2, ClockRetimeStages.SliceMilliseconds);
                Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
                if (outcome != ClockRetimeOutcome.Committed) continue;
                simulator = simulator.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
                break;
            }
            bytes[r] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        simulator.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        state.PrepareForSave();
        return (RushSpikeReplay.Sha(SaveCodec.Write(state)), bytes);
    }

    [Fact]
    public void RetimedJourneysTrafficAndSaveMatchThePinnedPreChangeHash()
    {
        var (hash, bytes) = RetimeThroughRates(committed: false);
        output.WriteLine(hash + "; MB per retime: " + string.Join(", ", bytes.Select(b => (b / 1e6).ToString("0.0"))));
        Assert.Equal(Retimed, hash);
    }

    [Fact]
    public void ARetimeOfCommittedJourneysStaysUnderItsAllocationCeiling()
    {
        var (_, bytes) = RetimeThroughRates(committed: true);
        output.WriteLine("committed journeys, MB per retime: " + string.Join(", ", bytes.Select(b => (b / 1e6).ToString("0.0"))));
        PerfGate.Under(bytes.Skip(1).Max() / 1e6, 60, "MB allocated by one retime at 13,965 links (.NET)");
    }

    [Fact]
    public void TheRetimeTallySplitsEveryRetimeByteIntoItsStagesAndReportsTheHeapAroundIt()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        var tally = new RetimeAllocationTally();
        for (int i = 0; i < 16; i++) Assert.Equal(ClockRetimeOutcome.None, driver.Update(state, state.TravelClockRate, false, 2, 4));
        int busy = 0;
        long idle = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            long mark = GC.GetAllocatedBytesForCurrentThread();
            if (driver.Update(state, state.TravelClockRate, false, 2, 4) != ClockRetimeOutcome.None) busy++;
            else if (tally.Open) tally.Reset();
            if (mark < 0) busy++;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - idle);
        Assert.Equal(0, busy);
        long started = GC.GetAllocatedBytesForCurrentThread();
        while (true)
        {
            long mark = GC.GetAllocatedBytesForCurrentThread();
            var outcome = driver.Update(state, 3, false, 2, ClockRetimeStages.SliceMilliseconds);
            long updated = GC.GetAllocatedBytesForCurrentThread();
            Assert.NotEqual(ClockRetimeOutcome.None, outcome);
            if (!tally.Open) tally.Begin(1_400_000_000, 1_200_000_000);
            tally.Add(outcome == ClockRetimeOutcome.Committed ? RetimeAllocationStage.Commit : RetimeAllocationStage.Journeys, mark, updated);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            long swap = GC.GetAllocatedBytesForCurrentThread();
            var rebuild = simulator.BeginRebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            simulator = rebuild.TakeResult();
            tally.Add(RetimeAllocationStage.Rebuild, swap, GC.GetAllocatedBytesForCurrentThread());
            break;
        }
        long spent = GC.GetAllocatedBytesForCurrentThread() - started;
        tally.Finish(1_550_000_000, 1_100_000_000);
        string line = tally.Describe("thread");
        output.WriteLine(line + " (.NET)");
        Assert.False(tally.Open);
        Assert.True(tally[RetimeAllocationStage.Journeys] > 0 && tally[RetimeAllocationStage.Commit] > 0 && tally[RetimeAllocationStage.Rebuild] > 0);
        Assert.Equal(0, tally[RetimeAllocationStage.Release] + tally[RetimeAllocationStage.Routes]);
        Assert.InRange(spent - tally.Total, 0, 64 * 1024);
        foreach (string stage in new[] { "body release 0 B", "journey retime ", "routes 0 B", "commit ", "rebuild ", "at start 1400000000 / 1200000000 B", "at finish 1550000000 / 1100000000 B", "heap growth 150000000 B" })
            Assert.Contains(stage, line);
        tally.Begin(1, 1);
        tally.Begin(2, 2);
        Assert.Equal(1, tally.HeapBefore);
        Assert.Equal(0, tally.Total);
        tally.Reset();
        Assert.Throws<InvalidOperationException>(() => tally.Add(RetimeAllocationStage.Journeys, 0, 1));
    }

    [Fact]
    public void AnUnchangedClockRateAllocatesNothingInTheRetimeDriver()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity();
        var driver = new ClockRetimeDriver();
        for (int i = 0; i < 16; i++) Assert.Equal(ClockRetimeOutcome.None, driver.Update(state, state.TravelClockRate, true, 2, 4));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) driver.Update(state, state.TravelClockRate, i % 2 == 0, 2, 4);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void RetimeAllocationSplitsIntoSnapshotRetimeCommitAndRebuild()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        foreach (double rate in new[] { 3.0, 20, 1, 20 })
        {
            var bytes = new Dictionary<ClockRetimeOutcome, long>();
            long rebuild = 0;
            while (true)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                int snapshots = driver.SnapshotSlices;
                var outcome = driver.Update(state, rate, false, 2, ClockRetimeStages.SliceMilliseconds);
                if (outcome == ClockRetimeOutcome.Preparing && driver.SnapshotSlices > snapshots) outcome = ClockRetimeOutcome.None;
                bytes[outcome] = (bytes.TryGetValue(outcome, out long b) ? b : 0) + GC.GetAllocatedBytesForCurrentThread() - before;
                if (outcome != ClockRetimeOutcome.Committed) continue;
                before = GC.GetAllocatedBytesForCurrentThread();
                simulator = simulator.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
                rebuild = GC.GetAllocatedBytesForCurrentThread() - before;
                break;
            }
            output.WriteLine("rate " + rate + ": " + string.Join(", ", bytes.Select(p => p.Key + " " + (p.Value / 1e6).ToString("0.00") + " MB"))
                + ", rebuild " + (rebuild / 1e6).ToString("0.00") + " MB");
            long total = bytes.Values.Sum() + rebuild;
            Assert.True(bytes[ClockRetimeOutcome.Preparing] > 0.8 * total, "journey and traffic retime slices carry the retime allocation");
            Assert.True(bytes[ClockRetimeOutcome.Committed] < 1_000_000, "commit " + bytes[ClockRetimeOutcome.Committed]);
            Assert.True(rebuild < 2_000_000, "rebuild " + rebuild);
        }
    }
}
