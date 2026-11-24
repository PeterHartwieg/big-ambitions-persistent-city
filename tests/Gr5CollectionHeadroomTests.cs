using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5CollectionHeadroomTests
{
    private readonly ITestOutputHelper output;
    public Gr5CollectionHeadroomTests(ITestOutputHelper output) { this.output = output; }

    private const long MB = 1024 * 1024;

    [Fact]
    public void TheGuardLearnsTheTriggerFromACompletedCycleAndDefersOnlyInsideTheMarginUpToTheCap()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(5 * MB, 900 * MB, 10);
        Assert.False(headroom.Defer(400 * MB));
        headroom.Observe(1 * MB, 900 * MB, 11);
        Assert.Equal(-1, headroom.Predicted);
        headroom.Observe(299 * MB, 1000 * MB, 11);
        Assert.False(headroom.Defer(299 * MB));
        headroom.Observe(1 * MB, 1000 * MB, 12);
        Assert.Equal(1, headroom.Cycles);
        Assert.Equal(299.0 / 900, headroom.Ratio, 12);
        long predicted = (long)(299.0 / 900 * 1000 * MB);
        Assert.Equal(predicted, headroom.Predicted);
        Assert.False(headroom.Defer(predicted - CollectionHeadroom.MarginBytes - 1));
        for (int i = 0; i < CollectionHeadroom.MaximumDeferredUpdates; i++) Assert.True(headroom.Defer(predicted - CollectionHeadroom.MarginBytes + i));
        Assert.False(headroom.Defer(predicted));
        Assert.False(headroom.Defer(predicted + MB));
        Assert.Equal((long)CollectionHeadroom.MaximumDeferredUpdates, headroom.DeferredUpdates);
        Assert.Equal(1, headroom.CappedCycles);
        headroom.Observe(predicted + 2 * MB, 1000 * MB, 12);
        headroom.Observe(2 * MB, 1010 * MB, 13);
        Assert.Equal(2, headroom.Cycles);
        Assert.Equal(1, headroom.DeferredCycles);
        Assert.True(headroom.Defer(headroom.Predicted));
        var cycle = headroom.Cycle(1);
        Assert.Equal((predicted, predicted + 2 * MB, CollectionHeadroom.MaximumDeferredUpdates + 1), (cycle.Predicted, cycle.Observed, cycle.Deferred));
        using var report = JsonDocument.Parse(headroom.ReportJson(true));
        Assert.True(report.RootElement.GetProperty("available").GetBoolean());
        Assert.Equal(2 * MB, report.RootElement.GetProperty("recent_cycles")[1].GetProperty("error_bytes").GetInt64());
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("recent_cycles")[0].GetProperty("error_bytes").ValueKind);
    }

    [Fact]
    public void TheRecentAdvancementBurstWidensTheDeferralDistanceAndDecays()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(1 * MB, 900 * MB, 1);
        headroom.Observe(300 * MB, 900 * MB, 1);
        headroom.Observe(1 * MB, 900 * MB, 2);
        headroom.Observe(300 * MB, 900 * MB, 2);
        headroom.Observe(1 * MB, 900 * MB, 3);
        long predicted = headroom.Predicted;
        Assert.Equal(300 * MB, predicted);
        Assert.False(headroom.Defer(predicted - CollectionHeadroom.MarginBytes - 2 * MB));
        headroom.NoteAdvance(2 * MB);
        Assert.Equal(2 * MB, headroom.Burst);
        Assert.True(headroom.Defer(predicted - CollectionHeadroom.MarginBytes - 2 * MB));
        Assert.False(headroom.Defer(predicted - CollectionHeadroom.MarginBytes - 2 * MB - 1));
        headroom.NoteAdvance(0);
        Assert.Equal((long)(2 * MB * CollectionHeadroom.BurstDecay), headroom.Burst);
        headroom.NoteAdvance(100 * MB);
        Assert.Equal(CollectionHeadroom.MaximumBurstBytes, headroom.Burst);
        headroom.NoteAdvance(-5);
        Assert.Equal(CollectionHeadroom.MaximumBurstBytes, headroom.Burst);
        for (int i = 0; i < 3000; i++) headroom.NoteAdvance(0);
        Assert.Equal(0, headroom.Burst);
    }

    [Fact]
    public void UnreadableCountersTwoCollectionsAtOnceAndFallingCountersStopPredictions()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(1 * MB, 900 * MB, 1);
        headroom.Observe(300 * MB, 900 * MB, 1);
        headroom.Observe(1 * MB, 900 * MB, 2);
        headroom.Observe(300 * MB, 900 * MB, 2);
        headroom.Observe(1 * MB, 900 * MB, 3);
        Assert.True(headroom.Predicted > 0);
        headroom.Observe(1 * MB, 900 * MB, 5);
        Assert.Equal(-1, headroom.Predicted);
        Assert.False(headroom.Defer(1000 * MB));
        headroom.Observe(250 * MB, 900 * MB, 5);
        headroom.Observe(1 * MB, 900 * MB, 6);
        Assert.True(headroom.Predicted > 0);
        headroom.Observe(200 * MB, 900 * MB, 6);
        headroom.Observe(100 * MB, 900 * MB, 6);
        Assert.Equal(-1, headroom.Predicted);
        Assert.Equal(1, headroom.Invalidated);
        headroom.Observe(-1, 900 * MB, 6);
        Assert.Equal(-1, headroom.Predicted);
        headroom.Observe(2 * MB, 900 * MB, 7);
        Assert.Equal(-1, headroom.Predicted);
        headroom.Observe(8 * MB, 900 * MB, 7);
        headroom.Observe(1 * MB, 900 * MB, 8);
        Assert.Equal(1, headroom.Cycles);
        Assert.Equal(300 * MB, headroom.Predicted);
        headroom.ResetCounters();
        Assert.Equal((0L, 0L, 0L, 0), (headroom.Cycles, headroom.DeferredUpdates, headroom.CappedCycles, headroom.RecordedCycles));
    }

    [Fact]
    public void ObservingAndDeferringAllocateNothing()
    {
        var headroom = new CollectionHeadroom();
        long bytes = 0; int collections = 0;
        for (int i = 0; i < 200; i++) { bytes += MB; if (bytes > 200 * MB) { bytes = 0; collections++; } headroom.Observe(bytes, 900 * MB, collections); headroom.Defer(bytes); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++)
        {
            bytes += 64 * 1024;
            if (bytes > 220 * MB) { bytes = 0; collections++; }
            headroom.Observe(bytes, 900 * MB + i, collections);
            headroom.Defer(bytes);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ACollectionBetweenTheByteAndCountReadsIsDiscardedAndKeepsThePrediction()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(1 * MB, 900 * MB, 9);
        headroom.Observe(1 * MB, 900 * MB, 10);
        headroom.Observe(299 * MB, 900 * MB, 10);
        headroom.Observe(1 * MB, 900 * MB, 11);
        long predicted = headroom.Predicted;
        Assert.True(predicted > 0);
        headroom.Observe(11, 299 * MB, 900 * MB, 12);
        Assert.Equal(1, headroom.TornSamples);
        Assert.Equal(predicted, headroom.Predicted);
        headroom.Observe(12, 1 * MB, 900 * MB, 12);
        headroom.Observe(12, 2 * MB, 900 * MB, 12);
        Assert.Equal(0, headroom.Invalidated);
        Assert.True(headroom.Predicted > 0);
        Assert.False(headroom.Defer(12, headroom.Predicted, 13));
        Assert.False(headroom.Defer(13, headroom.Predicted, 13));
        Assert.Equal(2, headroom.StaleDeferrals);
        Assert.True(headroom.Defer(12, headroom.Predicted, 12));
        using var report = JsonDocument.Parse(headroom.ReportJson(true));
        Assert.Equal(1, report.RootElement.GetProperty("torn_samples").GetInt64());
        Assert.Equal(2, report.RootElement.GetProperty("stale_deferrals").GetInt64());
    }

    [Fact]
    public void AGuardDeferralKeepsTheNativeClockMovingInsideTheMinuteAndNeverCrossesItsFence()
    {
        double native = 600.25, logical = 600.25;
        Assert.True(CollectionHeadroom.ClockAllowsDeferral(true, native, .05f));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(false, native, .05f));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(true, 600.95, .05f));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(true, native, float.NaN));
        float requested = .05f;
        Assert.Equal(0f, AccountingClock.Limit(false, native, requested));
        bool ready = CollectionHeadroom.ClockReady(false, true, logical, native);
        Assert.True(ready);
        Assert.Equal(requested, AccountingClock.Limit(ready, native, requested));
        Assert.False(CollectionHeadroom.ClockReady(false, false, logical, native));
        Assert.False(CollectionHeadroom.ClockReady(false, true, 599.5, native));
        double fence = 600 + 1 - 1.0 / 262144;
        Assert.NotEqual(fence, AccountingClock.Target(fence));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(true, fence, 0f));
        Assert.False(CollectionHeadroom.ClockReady(false, true, 600.5, fence));
        float clipped = AccountingClock.Limit(CollectionHeadroom.ClockReady(false, true, 600.9, 600.99), 600.99, 1f);
        Assert.True(600.99 + clipped < 601, "a deferral ready tick stops at the fence: " + clipped);
    }

    private sealed class ModelResult
    {
        public int Collections, InMod;
        public long FenceRefusals, Unpredicted, Deferred, Capped, MaxDeferred, CompletedBytes, MaxBacklogBytes, Holds, MaxBacklogFrames, DueRefusals, CapacityRefusals;
        public double Withheld, NativeEnd;
    }

    private sealed class ModelInputs
    {
        public ModelInputs(int seed, int frames, double modShare, double bursty, double dueShare = 1, double dueLoad = .3, double idleShare = .2)
        {
            var random = new Random(seed);
            var work = new Random(seed + 7919);
            Game = new long[frames]; Mod = new long[frames]; Due = new bool[frames]; Load = new double[frames];
            for (int f = 0; f < frames; f++)
            {
                Game[f] = (long)((60 + random.NextDouble() * 80) * 1024);
                Mod[f] = (long)(Game[f] * modShare * (random.NextDouble() < bursty ? 20 : 1));
                Due[f] = work.NextDouble() < dueShare;
                Load[f] = Due[f] ? .05 + work.NextDouble() * (dueLoad - .05) : .02;
                if (!Due[f]) Mod[f] = (long)(Mod[f] * idleShare);
            }
            Drift = new double[4096]; Jitter = new double[4096];
            for (int k = 0; k < Drift.Length; k++) { Drift[k] = random.NextDouble(); Jitter[k] = random.NextDouble(); }
        }
        public long[] Game, Mod;
        public bool[] Due;
        public double[] Drift, Jitter, Load;
    }

    private const double MinutesPerFrame = .035;

    private static ModelResult Model(bool guard, ModelInputs inputs)
    {
        var headroom = new CollectionHeadroom();
        var result = new ModelResult();
        long since = 0, live = 1100 * MB, run = 0, backlog = 0, backlogFrames = 0;
        const double divisor = 5.0;
        long trigger = (long)(live / divisor);
        double native = 600.0, city = 600.0;
        bool deferredFlag = false, caughtUpAfter = true;
        bool Allocate(long bytes)
        {
            since += bytes;
            if (since < trigger) return false;
            int k = result.Collections++ % inputs.Drift.Length; since = 0;
            live += (long)((inputs.Drift[k] - .3) * 8 * MB);
            trigger = (long)(live / divisor * (1 + (inputs.Jitter[k] - .5) * .002));
            return true;
        }
        void Advance(long mod)
        {
            long total = backlog + mod;
            bool collected = false;
            for (long left = total; left > 0; left -= 4096)
                if (Allocate(Math.Min(4096, left)))
                {
                    result.InMod++; collected = true;
                    if (guard && !CollectionHeadroom.ClockAllowsDeferral(caughtUpAfter, native, (float)MinutesPerFrame)) result.FenceRefusals++;
                    if (guard && headroom.Predicted < 0) result.Unpredicted++;
                }
            if (guard && !collected) headroom.NoteAdvance(total);
            result.CompletedBytes += total; backlog = 0; backlogFrames = 0;
            city = AccountingClock.Target(native); caughtUpAfter = true;
        }
        for (int frame = 0; frame < inputs.Game.Length; frame++)
        {
            bool caughtUp = city >= AccountingClock.Target(native);
            bool ready = CollectionHeadroom.ClockReady(caughtUp, deferredFlag, city, native);
            float requested = (float)MinutesPerFrame, allowed = AccountingClock.Limit(ready, native, requested);
            if (!ready) result.Holds++;
            result.Withheld += requested - allowed; native += allowed;
            deferredFlag = false;
            long game = inputs.Game[frame];
            Allocate(game / 2);
            if (guard) headroom.Observe(result.Collections, since, live, result.Collections);
            bool skip = false;
            if (guard && CollectionHeadroom.ClockAllowsDeferral(caughtUpAfter, native, (float)MinutesPerFrame))
            {
                if (!headroom.CapacityAllowsDeferral) result.CapacityRefusals++;
                else if (inputs.Due[frame]) result.DueRefusals++;
                else skip = headroom.Defer(result.Collections, since, result.Collections);
            }
            if (skip)
            {
                headroom.Carry(new PopulationFrameBudget(1.0 / 30, false));
                deferredFlag = true; result.Deferred++; run++; result.MaxDeferred = Math.Max(result.MaxDeferred, run);
                backlog += inputs.Mod[frame]; backlogFrames++;
                result.MaxBacklogBytes = Math.Max(result.MaxBacklogBytes, backlog); result.MaxBacklogFrames = Math.Max(result.MaxBacklogFrames, backlogFrames);
            }
            else { run = 0; Advance(inputs.Mod[frame]); headroom.Advanced(true, inputs.Load[frame]); }
            Allocate(game - game / 2);
        }
        if (backlog > 0) Advance(0);
        result.Capped = headroom.CappedCycles; result.NativeEnd = native;
        return result;
    }

    private sealed class FrameScenario
    {
        public int BaselineIn, GuardedIn, Collections;
        public long Deferred, Capped, MaxRun, MaxBacklog, MaxBacklogFrames, Holds, DueRefusals, CapacityRefusals;
    }

    private FrameScenario FrameModelScenario(string name, double dueShare, double dueLoad)
    {
        var total = new FrameScenario();
        for (int seed = 1; seed <= 6; seed++)
        {
            var inputs = new ModelInputs(seed, 17000 * 6, .06, .02, dueShare, dueLoad);
            var baseline = Model(false, inputs);
            var guarded = Model(true, inputs);
            Assert.Equal(baseline.CompletedBytes, guarded.CompletedBytes);
            Assert.Equal(inputs.Mod.Sum(), guarded.CompletedBytes);
            Assert.Equal(baseline.NativeEnd, guarded.NativeEnd);
            Assert.Equal(baseline.Withheld, guarded.Withheld);
            Assert.Equal(0, baseline.Holds);
            total.BaselineIn += baseline.InMod; total.GuardedIn += guarded.InMod; total.Collections += guarded.Collections;
            total.Deferred += guarded.Deferred; total.Capped += guarded.Capped; total.MaxRun = Math.Max(total.MaxRun, guarded.MaxDeferred);
            total.MaxBacklog = Math.Max(total.MaxBacklog, guarded.MaxBacklogBytes); total.MaxBacklogFrames = Math.Max(total.MaxBacklogFrames, guarded.MaxBacklogFrames);
            total.Holds += guarded.Holds; total.DueRefusals += guarded.DueRefusals; total.CapacityRefusals += guarded.CapacityRefusals;
        }
        output.WriteLine(name + " (due work in " + dueShare.ToString("0%") + " of updates, load up to " + dueLoad.ToString("0.00") + "): " + total.Collections
            + " collections; inside mod work without / with the guard " + total.BaselineIn + " / " + total.GuardedIn + "; deferred updates " + total.Deferred
            + ", refused for due work " + total.DueRefusals + ", for capacity " + total.CapacityRefusals + ", capped cycles " + total.Capped + ", longest deferral " + total.MaxRun
            + " updates; largest backlog " + total.MaxBacklog + " B over " + total.MaxBacklogFrames + " updates; ordinary clock holds " + total.Holds);
        Assert.Equal(0, total.Holds);
        Assert.True(total.Collections > 100, name + " collections " + total.Collections);
        Assert.True(total.MaxRun <= CollectionHeadroom.MaximumDeferredUpdates);
        Assert.True(total.Deferred <= (long)total.Collections * CollectionHeadroom.MaximumDeferredUpdates);
        return total;
    }

    [Fact]
    public void InABoehmStyleFrameModelTheGuardDefersOnlyUpdatesWithNothingDueAndKeepsTheWorkAndClock()
    {
        var saturated = FrameModelScenario("due work every update", 1, .3);
        Assert.Equal(0, saturated.Deferred);
        Assert.Equal(saturated.BaselineIn, saturated.GuardedIn);
        foreach (var scenario in new[] { FrameModelScenario("half idle, heavy", .5, .7), FrameModelScenario("half idle, light", .5, .3), FrameModelScenario("mostly idle, light", .2, .3) })
        {
            Assert.True(scenario.Deferred > 0);
            Assert.True(scenario.GuardedIn <= scenario.BaselineIn + 1, scenario.GuardedIn + " vs " + scenario.BaselineIn);
        }
    }

    private sealed class CatchUpResult
    {
        public long Holds, Deferred, BehindAfterAdvance, Processed;
        public double Withheld, NativeEnd;
    }

    private enum CatchUpPolicy { Naive, Carry, Capacity, NothingDue }

    private static CatchUpResult CatchUpModel(double eventsPerTick, double eventMilliseconds, int admissionsPerFrame, int period, int phase, int run, bool deferrals, CatchUpPolicy policy, double[]? schedule = null)
    {
        const float tick = 2f / 30;
        const double seconds = 1.0 / 30, timeBudget = UpdateCityBudget.AdvanceFloorMilliseconds;
        var headroom = new CollectionHeadroom();
        var result = new CatchUpResult();
        double spacing = tick / eventsPerTick, native = 600, city = 600;
        long next = 0;
        bool deferredFlag = false, caughtUpAfter = true, carry = policy != CatchUpPolicy.Naive;
        double Due(long index) => schedule == null ? 600 + (index + 1) * spacing : index < schedule.Length ? schedule[index] : double.PositiveInfinity;
        for (int frame = 0; frame < 3000; frame++)
        {
            bool caughtUp = city >= AccountingClock.Target(native);
            bool ready = CollectionHeadroom.ClockReady(caughtUp, deferredFlag || carry && headroom.CatchingUp, city, native);
            float allowed = AccountingClock.Limit(ready, native, tick);
            if (!ready) result.Holds++;
            result.Withheld += tick - allowed; native += allowed;
            deferredFlag = false;
            var budget = new PopulationFrameBudget(seconds, false);
            bool wanted = deferrals && (frame + phase) % period < run;
            if (wanted && CollectionHeadroom.ClockAllowsDeferral(caughtUpAfter, native, carry ? tick : 0f)
                && (policy < CatchUpPolicy.Capacity || headroom.CapacityAllowsDeferral)
                && (policy != CatchUpPolicy.NothingDue || Due(next) > AccountingClock.Target(native)))
            {
                deferredFlag = true; result.Deferred++;
                if (carry) headroom.Carry(budget);
                continue;
            }
            var (unitLimit, citizenLimit) = carry ? headroom.Limits(budget) : (budget.Units, budget.CitizenEvents);
            double target = AccountingClock.Target(native), elapsed = 0;
            int done = 0;
            bool limited = false;
            while (done < unitLimit && done < citizenLimit && Due(next) <= target)
            {
                if (done >= admissionsPerFrame) { limited = true; break; }
                next++; done++; elapsed += eventMilliseconds;
                if (elapsed >= timeBudget) break;
            }
            result.Processed += done;
            caughtUpAfter = Due(next) > target;
            city = caughtUpAfter ? target : Due(next - 1);
            if (!caughtUpAfter) result.BehindAfterAdvance++;
            headroom.Advanced(caughtUpAfter, CollectionHeadroom.Load(elapsed, timeBudget, done, budget.Units, done, budget.CitizenEvents, done, admissionsPerFrame, limited));
        }
        result.NativeEnd = native;
        return result;
    }

    [Fact]
    public void ADeferralIsRefusedWithoutSpareCapacityAndTheCarryIsReleased()
    {
        var headroom = new CollectionHeadroom();
        Assert.False(headroom.CapacityAllowsDeferral);
        headroom.Advanced(true, .2);
        Assert.False(headroom.CapacityAllowsDeferral);
        for (int i = 0; i < 20; i++) headroom.Advanced(true, .2);
        Assert.Equal(.2, headroom.PeakLoad);
        Assert.True(headroom.CapacityAllowsDeferral);
        var budget = new PopulationFrameBudget(1.0 / 30, false);
        for (int i = 0; i < 3; i++) { headroom.Carry(budget); Assert.True(headroom.CapacityAllowsDeferral); }
        headroom.Carry(budget);
        Assert.False(headroom.CapacityAllowsDeferral);
        Assert.True(headroom.CatchingUp);
        headroom.Release();
        Assert.False(headroom.CatchingUp);
        Assert.Equal((0, 0, 0), (headroom.CarriedUnits, headroom.CarriedCitizenEvents, headroom.CarriedUpdates));
        Assert.Equal(1, headroom.PeakLoad);
        for (int i = 0; i < 20; i++) headroom.Advanced(true, .2);
        headroom.Advanced(true, .55);
        Assert.False(headroom.CapacityAllowsDeferral);
        headroom.Advanced(true, double.NaN);
        Assert.Equal(1, headroom.PeakLoad);
        Assert.Equal(1, CollectionHeadroom.Load(.1, 2, 1, 256, 1, 32, 0, 16, true));
        Assert.Equal(1, CollectionHeadroom.Load(2, 2, 8, 256, 8, 32, 0, 16, false));
        Assert.Equal(.5, CollectionHeadroom.Load(.1, 2, 16, 256, 16, 32, 0, 16, false));
        Assert.Equal(.75, CollectionHeadroom.Load(.1, 2, 4, 256, 4, 32, 12, 16, false));
    }

    [Fact]
    public void DeferredAdvancementUnderTheLiveTimeAndAdmissionLimitsHoldsTheClockNoMoreThanWithoutDeferrals()
    {
        var naive = CatchUpModel(20, .01, 1024, 15, 0, 1, true, CatchUpPolicy.Naive);
        var naiveBaseline = CatchUpModel(20, .01, 1024, 15, 0, 1, false, CatchUpPolicy.Naive);
        output.WriteLine("without lookahead or carried budget, 20 events per tick at 2x and 30 fps: " + naive.Holds + " ordinary holds, "
            + naive.Withheld.ToString("0.000000") + " minutes withheld vs " + naiveBaseline.Holds + " / " + naiveBaseline.Withheld.ToString("0.000000") + " without deferrals");
        Assert.Equal(0, naiveBaseline.Holds);
        Assert.True(naive.Holds > 0, "the old policy holds the clock after a deferral next to the fence");
        var saturatedBaseline = CatchUpModel(8, .25, 1024, 15, 0, 1, false, CatchUpPolicy.Capacity);
        var carryOnly = CatchUpModel(8, .25, 1024, 15, 0, 1, true, CatchUpPolicy.Carry);
        var saturated = CatchUpModel(8, .25, 1024, 15, 0, 1, true, CatchUpPolicy.Capacity);
        output.WriteLine("eight units per tick at 0.25 ms against a 2 ms budget: carried limits alone " + carryOnly.Holds + " holds, "
            + carryOnly.Withheld.ToString("0.000000") + " minutes withheld; with the capacity check " + saturated.Holds + " / " + saturated.Withheld.ToString("0.000000")
            + " (" + saturated.Deferred + " deferred); baseline " + saturatedBaseline.Holds + " / " + saturatedBaseline.Withheld.ToString("0.000000"));
        Assert.Equal(0, saturatedBaseline.Holds);
        Assert.True(carryOnly.Holds > 0, "carried event limits do not lift the wall-time limit");
        Assert.Equal((saturatedBaseline.Holds, saturatedBaseline.Withheld, saturatedBaseline.NativeEnd), (saturated.Holds, saturated.Withheld, saturated.NativeEnd));
        long cases = 0, deferred = 0, behind = 0, lightDeferred = 0;
        foreach (double rate in new[] { .25, .5, 4.0, 8, 12, 20, 26, 31 })
            foreach (double cost in new[] { .01, .05, .1, .25 })
                foreach (int admissions in new[] { 8, 16, 1024 })
                    for (int run = 1; run <= CollectionHeadroom.MaximumDeferredUpdates; run++)
                        for (int phase = 0; phase < 15; phase += 4)
                        {
                            var baseline = CatchUpModel(rate, cost, admissions, 15, phase, run, false, CatchUpPolicy.NothingDue);
                            if (baseline.Holds > 0) continue;
                            var guarded = CatchUpModel(rate, cost, admissions, 15, phase, run, true, CatchUpPolicy.NothingDue);
                            string name = "rate " + rate + " cost " + cost + " admissions " + admissions + " run " + run + " phase " + phase;
                            Assert.True((baseline.Holds, baseline.Withheld, baseline.NativeEnd) == (guarded.Holds, guarded.Withheld, guarded.NativeEnd),
                                name + ": holds " + guarded.Holds + " withheld " + guarded.Withheld + " vs " + baseline.Holds + " / " + baseline.Withheld);
                            Assert.True(baseline.Processed - guarded.Processed <= (long)Math.Ceiling(rate) * (run + 1), name);
                            if (rate < 1) lightDeferred += guarded.Deferred;
                            cases++; deferred += guarded.Deferred; behind += guarded.BehindAfterAdvance;
                        }
        output.WriteLine(cases + " cases without baseline holds, " + deferred + " deferred updates (" + lightDeferred + " in the light-load cases); every case keeps the baseline's holds, withheld minutes and native clock; updates still catching up after advancing " + behind);
        Assert.True(cases > 300, "cases " + cases);
        Assert.True(lightDeferred > 0);
    }

    private static double[] QuietThenBusy(int minutes, bool idleFirstBusyUpdate)
    {
        const double tick = 2.0 / 30;
        var due = new System.Collections.Generic.List<double>();
        for (int minute = 0; minute < minutes; minute++)
            for (int k = 1; k <= 15; k++)
            {
                double start = 600 + minute + (k - 1) * tick;
                int units = k <= 11 ? 1 : k == 12 && idleFirstBusyUpdate ? 0 : 8;
                for (int j = 0; j < units; j++) due.Add(start + (j + .5) / units * tick);
            }
        return due.ToArray();
    }

    [Fact]
    public void ABusyStretchThatStartsOnTheDeferredUpdateDoesNotHoldTheClock()
    {
        var schedule = QuietThenBusy(220, false);
        var baseline = CatchUpModel(0, .25, 1024, 15, 4, 1, false, CatchUpPolicy.NothingDue, schedule);
        var capacity = CatchUpModel(0, .25, 1024, 15, 4, 1, true, CatchUpPolicy.Capacity, schedule);
        var shipped = CatchUpModel(0, .25, 1024, 15, 4, 1, true, CatchUpPolicy.NothingDue, schedule);
        output.WriteLine("11 updates with one due unit, then four with eight, 0.25 ms per unit, 2 ms budget, 2x at 30 fps, deferral wanted on the first busy update: baseline "
            + baseline.Holds + " holds / " + baseline.Withheld.ToString("0.000000") + " minutes withheld; capacity check alone " + capacity.Holds + " / " + capacity.Withheld.ToString("0.000000")
            + " (" + capacity.Deferred + " deferred); with the nothing-due check " + shipped.Holds + " / " + shipped.Withheld.ToString("0.000000") + " (" + shipped.Deferred + " deferred)");
        Assert.Equal(0, baseline.Holds);
        Assert.True(capacity.Deferred > 0 && capacity.Holds > 0 && capacity.Withheld > baseline.Withheld, "the capacity check alone defers into the busy stretch");
        Assert.Equal((baseline.Holds, baseline.Withheld, baseline.NativeEnd, baseline.Processed), (shipped.Holds, shipped.Withheld, shipped.NativeEnd, shipped.Processed));
        Assert.Equal(0, shipped.Deferred);
        var gap = QuietThenBusy(220, true);
        var gapBaseline = CatchUpModel(0, .25, 1024, 15, 4, 1, false, CatchUpPolicy.NothingDue, gap);
        var gapGuarded = CatchUpModel(0, .25, 1024, 15, 4, 1, true, CatchUpPolicy.NothingDue, gap);
        output.WriteLine("with nothing due on the wanted update: " + gapGuarded.Deferred + " deferred, " + gapGuarded.Holds + " holds / " + gapGuarded.Withheld.ToString("0.000000")
            + " vs baseline " + gapBaseline.Holds + " / " + gapBaseline.Withheld.ToString("0.000000"));
        Assert.True(gapGuarded.Deferred > 0);
        Assert.Equal((gapBaseline.Holds, gapBaseline.Withheld, gapBaseline.NativeEnd, gapBaseline.Processed), (gapGuarded.Holds, gapGuarded.Withheld, gapGuarded.NativeEnd, gapGuarded.Processed));
    }

    [Fact]
    public void EverySimulationReplacementReleasesTheCarriedCatchUpAndTheClockForgetsItsLastTick()
    {
        string root = CapabilityReportTests.Root();
        int sites = 0;
        foreach (var file in new[] { "PersistentCityMod.cs", "PopulationLifecycle.cs" })
        {
            var lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(root, "Runtime", file));
            for (int i = 0; i < lines.Length; i++)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"^\s*simulation = ")) continue;
                sites++;
                Assert.True(lines.Skip(i).Take(3).Any(line => line.Contains("ForgetCollectionCatchUp();")), file + ":" + (i + 1) + " " + lines[i].Trim());
            }
        }
        Assert.Equal(4, sites);
        string clock = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Runtime", "NativeCityClock.cs"));
        int reset = clock.IndexOf("private static void ResetDiagnostics()", StringComparison.Ordinal);
        Assert.True(reset > 0 && clock.IndexOf("LastRequestedMinutes = float.NaN;", reset, StringComparison.Ordinal) > reset);
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(true, 600.25, float.NaN));
    }
}
