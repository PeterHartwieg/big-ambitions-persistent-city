using System;
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

    private readonly struct ModelResult
    {
        public ModelResult(int collections, int inMod, long deferred, long capped, long maxDeferred) { Collections = collections; InMod = inMod; Deferred = deferred; Capped = capped; MaxDeferred = maxDeferred; }
        public int Collections { get; }
        public int InMod { get; }
        public long Deferred { get; }
        public long Capped { get; }
        public long MaxDeferred { get; }
    }

    private static ModelResult Model(bool guard, int seed, double modShare, double bursty)
    {
        var random = new Random(seed);
        var headroom = new CollectionHeadroom();
        long since = 0, live = 1100 * MB, deferred = 0, maxDeferred = 0, run = 0;
        int collections = 0, inMod = 0;
        const double divisor = 5.0;
        long trigger = (long)(live / divisor);
        bool Allocate(long bytes)
        {
            since += bytes;
            if (since < trigger) return false;
            collections++; since = 0;
            live += (long)((random.NextDouble() - .3) * 8 * MB);
            trigger = (long)(live / divisor * (1 + (random.NextDouble() - .5) * .002));
            return true;
        }
        for (int frame = 0; frame < 17000 * 6; frame++)
        {
            long game = (long)((60 + random.NextDouble() * 80) * 1024);
            Allocate(game / 2);
            if (guard) headroom.Observe(since, live, collections);
            bool skip = guard && headroom.Defer(since);
            if (skip) { deferred++; run++; maxDeferred = Math.Max(maxDeferred, run); }
            else
            {
                run = 0;
                long mod = (long)(game * modShare * (random.NextDouble() < bursty ? 20 : 1));
                bool collected = false;
                for (long left = mod; left > 0; left -= 4096)
                    if (Allocate(Math.Min(4096, left))) { inMod++; collected = true; }
                if (guard && !collected) headroom.NoteAdvance(mod);
            }
            Allocate(game - game / 2);
        }
        return new ModelResult(collections, inMod, deferred, headroom.CappedCycles, maxDeferred);
    }

    [Fact]
    public void InABoehmStyleFrameModelTheGuardMovesCollectionsOutOfModWorkWithinItsCap()
    {
        int baselineIn = 0, guardedIn = 0, collections = 0;
        long deferred = 0, capped = 0, maxRun = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var baseline = Model(false, seed, .06, .02);
            var guarded = Model(true, seed, .06, .02);
            baselineIn += baseline.InMod; guardedIn += guarded.InMod; collections += guarded.Collections;
            deferred += guarded.Deferred; capped += guarded.Capped; maxRun = Math.Max(maxRun, guarded.MaxDeferred);
        }
        output.WriteLine("model: " + collections + " collections; inside mod work without / with the guard " + baselineIn + " / " + guardedIn
            + "; deferred updates " + deferred + " (" + (deferred / (double)collections).ToString("0.0") + " per collection), capped cycles " + capped + ", longest deferral " + maxRun + " updates");
        Assert.True(collections > 100, "collections " + collections);
        Assert.True(baselineIn >= collections / 20, "baseline collections in mod work " + baselineIn);
        Assert.True(guardedIn * 5 <= baselineIn, "guarded collections in mod work " + guardedIn + " vs " + baselineIn);
        Assert.True(maxRun <= CollectionHeadroom.MaximumDeferredUpdates);
        Assert.True(deferred <= (long)collections * CollectionHeadroom.MaximumDeferredUpdates);
    }
}
