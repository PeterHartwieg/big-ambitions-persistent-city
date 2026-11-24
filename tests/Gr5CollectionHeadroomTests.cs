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
        Assert.True(CollectionHeadroom.ClockAllowsDeferral(true, native));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(false, native));
        float requested = .05f;
        Assert.Equal(0f, AccountingClock.Limit(false, native, requested));
        bool ready = CollectionHeadroom.ClockReady(false, true, logical, native);
        Assert.True(ready);
        Assert.Equal(requested, AccountingClock.Limit(ready, native, requested));
        Assert.False(CollectionHeadroom.ClockReady(false, false, logical, native));
        Assert.False(CollectionHeadroom.ClockReady(false, true, 599.5, native));
        double fence = 600 + 1 - 1.0 / 262144;
        Assert.NotEqual(fence, AccountingClock.Target(fence));
        Assert.False(CollectionHeadroom.ClockAllowsDeferral(true, fence));
        Assert.False(CollectionHeadroom.ClockReady(false, true, 600.5, fence));
        float clipped = AccountingClock.Limit(CollectionHeadroom.ClockReady(false, true, 600.9, 600.99), 600.99, 1f);
        Assert.True(600.99 + clipped < 601, "a deferral ready tick stops at the fence: " + clipped);
    }

    private sealed class ModelResult
    {
        public int Collections, InMod;
        public long FenceRefusals, Unpredicted, Deferred, Capped, MaxDeferred, CompletedBytes, MaxBacklogBytes, Holds, MaxBacklogFrames;
        public double Withheld, NativeEnd;
    }

    private sealed class ModelInputs
    {
        public ModelInputs(int seed, int frames, double modShare, double bursty)
        {
            var random = new Random(seed);
            Game = new long[frames]; Mod = new long[frames];
            for (int f = 0; f < frames; f++)
            {
                Game[f] = (long)((60 + random.NextDouble() * 80) * 1024);
                Mod[f] = (long)(Game[f] * modShare * (random.NextDouble() < bursty ? 20 : 1));
            }
            Drift = new double[4096]; Jitter = new double[4096];
            for (int k = 0; k < Drift.Length; k++) { Drift[k] = random.NextDouble(); Jitter[k] = random.NextDouble(); }
        }
        public long[] Game, Mod;
        public double[] Drift, Jitter;
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
                    if (guard && !CollectionHeadroom.ClockAllowsDeferral(caughtUpAfter, native)) result.FenceRefusals++;
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
            bool skip = guard && CollectionHeadroom.ClockAllowsDeferral(caughtUpAfter, native) && headroom.Defer(result.Collections, since, result.Collections);
            if (skip)
            {
                deferredFlag = true; result.Deferred++; run++; result.MaxDeferred = Math.Max(result.MaxDeferred, run);
                backlog += inputs.Mod[frame]; backlogFrames++;
                result.MaxBacklogBytes = Math.Max(result.MaxBacklogBytes, backlog); result.MaxBacklogFrames = Math.Max(result.MaxBacklogFrames, backlogFrames);
            }
            else { run = 0; Advance(inputs.Mod[frame]); }
            Allocate(game - game / 2);
        }
        if (backlog > 0) Advance(0);
        result.Capped = headroom.CappedCycles; result.NativeEnd = native;
        return result;
    }

    [Fact]
    public void InABoehmStyleFrameModelTheGuardMovesCollectionsOutOfModWorkWithTheSameWorkAndNoClockHolds()
    {
        int baselineIn = 0, guardedIn = 0, collections = 0;
        long deferred = 0, capped = 0, maxRun = 0, maxBacklog = 0, maxBacklogFrames = 0, holds = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var inputs = new ModelInputs(seed, 17000 * 6, .06, .02);
            var baseline = Model(false, inputs);
            var guarded = Model(true, inputs);
            Assert.Equal(baseline.CompletedBytes, guarded.CompletedBytes);
            Assert.Equal(inputs.Mod.Sum(), guarded.CompletedBytes);
            Assert.Equal(baseline.NativeEnd, guarded.NativeEnd);
            Assert.Equal(baseline.Withheld, guarded.Withheld);
            Assert.Equal(0, baseline.Holds);
            output.WriteLine("seed " + seed + ": in mod " + baseline.InMod + " / " + guarded.InMod + ", at a minute fence " + guarded.FenceRefusals + ", without a prediction " + guarded.Unpredicted + ", capped " + guarded.Capped);
            baselineIn += baseline.InMod; guardedIn += guarded.InMod; collections += guarded.Collections;
            deferred += guarded.Deferred; capped += guarded.Capped; maxRun = Math.Max(maxRun, guarded.MaxDeferred);
            maxBacklog = Math.Max(maxBacklog, guarded.MaxBacklogBytes); maxBacklogFrames = Math.Max(maxBacklogFrames, guarded.MaxBacklogFrames); holds += guarded.Holds;
        }
        output.WriteLine("model: " + collections + " collections; inside mod work without / with the guard " + baselineIn + " / " + guardedIn
            + "; deferred updates " + deferred + " (" + (deferred / (double)collections).ToString("0.0") + " per collection), capped cycles " + capped + ", longest deferral " + maxRun
            + " updates; largest backlog " + maxBacklog + " B over " + maxBacklogFrames + " updates, drained; ordinary clock holds " + holds + "; equal completed work and native clock in both arms");
        Assert.Equal(0, holds);
        Assert.True(collections > 100, "collections " + collections);
        Assert.True(baselineIn >= collections / 20, "baseline collections in mod work " + baselineIn);
        Assert.True(guardedIn * 4 <= baselineIn, "guarded collections in mod work " + guardedIn + " vs " + baselineIn);
        Assert.True(maxRun <= CollectionHeadroom.MaximumDeferredUpdates);
        Assert.True(deferred <= (long)collections * CollectionHeadroom.MaximumDeferredUpdates);
    }
}
