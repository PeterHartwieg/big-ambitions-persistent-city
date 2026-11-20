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

    private static RetimeAllocationMark Thread() => new RetimeAllocationMark(GC.GetAllocatedBytesForCurrentThread(), 0);

    [Fact]
    public void TheRetimeTallySplitsEveryRetimeByteIntoItsStagesAndReportsTheHeapAroundIt()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        var tally = new RetimeAllocationTally();
        for (int i = 0; i < 16; i++) driver.Update(state, state.TravelClockRate, false, 2, 4);
        var idleOutcomes = new ClockRetimeOutcome[1000];
        long idle = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < idleOutcomes.Length; i++)
        {
            var mark = Thread();
            idleOutcomes[i] = driver.Update(state, state.TravelClockRate, false, 2, 4);
            tally.Observe(idleOutcomes[i]);
            tally.Add(RetimeAllocationStage.Journeys, mark, Thread());
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - idle);
        Assert.All(idleOutcomes, o => Assert.Equal(ClockRetimeOutcome.None, o));
        Assert.Equal(0, tally.Spans);

        var outcomes = new ClockRetimeOutcome[100_000];
        int count = 0;
        RetimeAllocationMark started = default, finished = default;
        started = Thread();
        while (true)
        {
            var mark = Thread();
            var outcome = driver.Update(state, 3, false, 2, 0);
            var updated = Thread();
            outcomes[count++] = outcome;
            tally.Observe(outcome);
            tally.Begin(1_400_000_000, 1_200_000_000);
            tally.Add(outcome == ClockRetimeOutcome.Committed ? RetimeAllocationStage.Commit : RetimeAllocationStage.Journeys, mark, updated);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            var swap = Thread();
            var rebuild = simulator.BeginRebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            simulator = rebuild.TakeResult();
            tally.Add(RetimeAllocationStage.Rebuild, swap, Thread());
            break;
        }
        finished = Thread();
        tally.Finish(1_550_000_000, 1_100_000_000);
        string line = tally.Describe("thread");
        output.WriteLine(line + "; " + count + " driver updates (.NET)");
        Assert.All(outcomes.Take(count), o => Assert.NotEqual(ClockRetimeOutcome.Restarted, o));
        Assert.All(outcomes.Take(count), o => Assert.NotEqual(ClockRetimeOutcome.None, o));
        Assert.Equal(ClockRetimeOutcome.Committed, outcomes[count - 1]);
        Assert.False(tally.Open);
        Assert.True(tally[RetimeAllocationStage.Journeys] > 0 && tally[RetimeAllocationStage.Commit] > 0 && tally[RetimeAllocationStage.Rebuild] > 0);
        Assert.Equal(0, tally[RetimeAllocationStage.Release] + tally[RetimeAllocationStage.Routes]);
        Assert.Equal(0, tally.InvalidSpans);
        Assert.Equal(count + 1, tally.Spans);
        Assert.Equal(finished.Counter - started.Counter, tally.Total);
        foreach (string stage in new[] { "body release 0 B", "journey retime ", "routes 0 B", "commit ", "rebuild ", "0 spans excluded", "at start 1400000000 / 1200000000 B", "at finish 1550000000 / 1100000000 B", "heap growth 150000000 B" })
            Assert.Contains(stage, line);
        Assert.DoesNotContain("lower bound", line);
    }

    [Fact]
    public void ReturningToTheOldRateDuringBodyReleaseStartsTheNextRetimeWithAFreshTally()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity();
        double one = state.TravelClockRate, two = 2 * one;
        var driver = new ClockRetimeDriver();
        var tally = new RetimeAllocationTally();
        long heap = 1_000;
        RetimeAllocationMark At(long bytes) => new RetimeAllocationMark(bytes, 0);
        void Frame(double rate, bool bodiesLive, long released, bool releasing)
        {
            var outcome = driver.Update(state, rate, bodiesLive, 2, 4);
            tally.Observe(outcome);
            if (outcome != ClockRetimeOutcome.None) tally.Begin(heap, heap / 2);
            bool tracked = outcome == ClockRetimeOutcome.ReleaseBodies;
            if (tracked || (outcome == ClockRetimeOutcome.None && releasing)) tally.Add(RetimeAllocationStage.Release, At(0), At(released));
        }
        Frame(two, true, 700, true);
        Assert.True(tally.Open);
        Assert.Equal(700, tally[RetimeAllocationStage.Release]);
        heap = 2_000;
        Frame(one, true, 300, true);
        Assert.False(tally.Open);
        Assert.Equal(0, tally.Total);
        heap = 3_000;
        Frame(two, true, 500, true);
        Assert.True(tally.Open);
        Assert.Equal(500, tally[RetimeAllocationStage.Release]);
        Assert.Equal(3_000, tally.HeapBefore);
        Assert.Equal(1, tally.Spans);
        Frame(two, true, 200, true);
        Assert.Equal(700, tally[RetimeAllocationStage.Release]);
        Assert.Equal(3_000, tally.HeapBefore);
    }

    [Fact]
    public void AHeapUsedCounterIsReportedAsHeapUsedDeltasWithOrWithoutACollection()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var simulator = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        simulator.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        RetimeAllocationMark Used() => new RetimeAllocationMark(GC.GetTotalMemory(false), GC.CollectionCount(0));
        var driver = new ClockRetimeDriver();
        var tally = new RetimeAllocationTally();
        int slice = 0, collected = -1;
        while (true)
        {
            var mark = Used();
            var outcome = driver.Update(state, 2, false, 1.4, 0);
            if (slice++ == 1) { GC.Collect(); collected = slice; }
            var updated = Used();
            tally.Observe(outcome);
            tally.Begin(1, 1);
            tally.Add(outcome == ClockRetimeOutcome.Committed ? RetimeAllocationStage.Commit : RetimeAllocationStage.Journeys, mark, updated);
            if (outcome == ClockRetimeOutcome.Committed) break;
        }
        tally.Finish(1, 1);
        string line = tally.Describe("heap_used");
        output.WriteLine(line + "; collection forced in slice " + collected);
        Assert.True(slice >= 3);
        Assert.True(tally.InvalidSpans >= 1);
        Assert.Equal(slice, tally.Spans);
        Assert.StartsWith("heap-used deltas, not allocated bytes (heap_used) ", line);
        Assert.Contains(tally.InvalidSpans + " spans excluded because a collection ran inside them (heap used moves by whole blocks, so these values do not attribute allocation to a stage)", line);
        Assert.DoesNotContain("allocated (", line);
        Assert.DoesNotContain("lower bound", line);
        var quiet = new RetimeAllocationTally();
        quiet.Begin(1, 1);
        quiet.Add(RetimeAllocationStage.Journeys, new RetimeAllocationMark(1_000, 7), new RetimeAllocationMark(9_000, 7));
        quiet.Finish(1, 1);
        string calm = quiet.Describe(RetimeAllocationTally.HeapUsedCounter);
        Assert.StartsWith("heap-used deltas, not allocated bytes (heap_used) ", calm);
        Assert.Contains("0 spans excluded because a collection ran inside them (heap used moves by whole blocks", calm);
        foreach (string counter in new[] { "boehm_total", "thread" })
        {
            Assert.True(RetimeAllocationTally.CountsAllocation(counter));
            Assert.StartsWith("allocated (" + counter + ") ", quiet.Describe(counter));
            Assert.DoesNotContain("heap-used", quiet.Describe(counter));
        }
        Assert.False(RetimeAllocationTally.CountsAllocation("heap_used"));
        var direct = new RetimeAllocationTally();
        direct.Begin(0, 0);
        direct.Add(RetimeAllocationStage.Journeys, new RetimeAllocationMark(5_000, 3), new RetimeAllocationMark(1_000, 4));
        direct.Add(RetimeAllocationStage.Journeys, new RetimeAllocationMark(5_000, 4), new RetimeAllocationMark(4_000, 4));
        direct.Add(RetimeAllocationStage.Journeys, new RetimeAllocationMark(4_000, 4), new RetimeAllocationMark(4_600, 4));
        Assert.Equal(600, direct.Total);
        Assert.Equal(2, direct.InvalidSpans);
        direct.Finish(0, 0);
        Assert.Contains("2 spans excluded because a collection ran inside them (totals are a lower bound)", direct.Describe("boehm_total"));
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
