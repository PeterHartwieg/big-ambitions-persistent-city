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
    public void ThePredictorLearnsTheTriggerFromACompletedCycleCountsEveryUpdateInsideTheMarginAndFlagsCyclesPastTheCap()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(5 * MB, 900 * MB, 10);
        Assert.False(headroom.WantsDeferral(400 * MB));
        headroom.Observe(1 * MB, 900 * MB, 11);
        Assert.Equal(-1, headroom.Predicted);
        headroom.Observe(299 * MB, 1000 * MB, 11);
        Assert.False(headroom.WantsDeferral(299 * MB));
        headroom.Observe(1 * MB, 1000 * MB, 12);
        Assert.Equal(1, headroom.Cycles);
        Assert.Equal(299.0 / 900, headroom.Ratio, 12);
        long predicted = (long)(299.0 / 900 * 1000 * MB);
        Assert.Equal(predicted, headroom.Predicted);
        Assert.False(headroom.WantsDeferral(predicted - CollectionHeadroom.MarginBytes - 1));
        for (int i = 0; i < CollectionHeadroom.MaximumWantedUpdates; i++) Assert.True(headroom.WantsDeferral(predicted - CollectionHeadroom.MarginBytes + i));
        Assert.Equal(0, headroom.CappedCycles);
        Assert.True(headroom.WantsDeferral(predicted));
        Assert.True(headroom.WantsDeferral(predicted + MB));
        Assert.Equal((long)CollectionHeadroom.MaximumWantedUpdates + 2, headroom.WantedUpdates);
        Assert.Equal(1, headroom.CappedCycles);
        headroom.Observe(predicted + 2 * MB, 1000 * MB, 12);
        headroom.Observe(2 * MB, 1010 * MB, 13);
        Assert.Equal(2, headroom.Cycles);
        Assert.Equal(1, headroom.WantedCycles);
        Assert.True(headroom.WantsDeferral(headroom.Predicted));
        var cycle = headroom.Cycle(1);
        Assert.Equal((predicted, predicted + 2 * MB, CollectionHeadroom.MaximumWantedUpdates + 2), (cycle.Predicted, cycle.Observed, cycle.Wanted));
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
        Assert.False(headroom.WantsDeferral(predicted - CollectionHeadroom.MarginBytes - 2 * MB));
        headroom.NoteAdvance(2 * MB);
        Assert.Equal(2 * MB, headroom.Burst);
        Assert.True(headroom.WantsDeferral(predicted - CollectionHeadroom.MarginBytes - 2 * MB));
        Assert.False(headroom.WantsDeferral(predicted - CollectionHeadroom.MarginBytes - 2 * MB - 1));
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
        Assert.False(headroom.WantsDeferral(1000 * MB));
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
        Assert.Equal((0L, 0L, 0L, 0), (headroom.Cycles, headroom.WantedUpdates, headroom.CappedCycles, headroom.RecordedCycles));
    }

    [Fact]
    public void ANewWindowStartedInsideACappedCycleKeepsThePredictionAndCountsOnlyItsOwnUpdates()
    {
        var headroom = new CollectionHeadroom();
        headroom.Observe(1 * MB, 900 * MB, 1);
        headroom.Observe(1 * MB, 900 * MB, 2);
        headroom.Observe(300 * MB, 900 * MB, 2);
        headroom.Observe(1 * MB, 900 * MB, 3);
        long predicted = headroom.Predicted;
        Assert.True(predicted > 0);
        for (int i = 0; i < CollectionHeadroom.MaximumWantedUpdates + 3; i++) Assert.True(headroom.WantsDeferral(predicted));
        Assert.Equal(1, headroom.CappedCycles);
        headroom.ResetCounters();
        Assert.Equal(predicted, headroom.Predicted);
        Assert.Equal((0L, 0L, 0L, 0L), (headroom.WantedUpdates, headroom.WantedCycles, headroom.CappedCycles, headroom.Cycles));
        Assert.True(headroom.WantsDeferral(predicted));
        Assert.True(headroom.WantsDeferral(predicted));
        Assert.Equal(2, headroom.WantedUpdates);
        Assert.Equal(0, headroom.CappedCycles);
        headroom.Observe(predicted + MB, 900 * MB, 3);
        headroom.Observe(1 * MB, 900 * MB, 4);
        Assert.Equal((1L, 1L, 1), (headroom.Cycles, headroom.WantedCycles, headroom.RecordedCycles));
        Assert.Equal(2, headroom.Cycle(0).Wanted);
        using var report = JsonDocument.Parse(headroom.ReportJson(true));
        Assert.Equal(2, report.RootElement.GetProperty("wanted_updates").GetInt64());
        Assert.Equal(1, report.RootElement.GetProperty("wanted_cycles").GetInt64());
        Assert.Equal(0, report.RootElement.GetProperty("capped_cycles").GetInt64());
        Assert.Equal(2, report.RootElement.GetProperty("recent_cycles")[0].GetProperty("wanted_updates").GetInt64());
    }

    [Fact]
    public void ObservingAndCheckingAllocateNothing()
    {
        var headroom = new CollectionHeadroom();
        long bytes = 0; int collections = 0;
        for (int i = 0; i < 200; i++) { bytes += MB; if (bytes > 200 * MB) { bytes = 0; collections++; } headroom.Observe(bytes, 900 * MB, collections); headroom.WantsDeferral(bytes); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++)
        {
            bytes += 64 * 1024;
            if (bytes > 220 * MB) { bytes = 0; collections++; }
            headroom.Observe(bytes, 900 * MB + i, collections);
            headroom.WantsDeferral(bytes);
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
        Assert.False(headroom.WantsDeferral(12, headroom.Predicted, 13));
        Assert.False(headroom.WantsDeferral(13, headroom.Predicted, 13));
        Assert.Equal(2, headroom.StaleChecks);
        Assert.True(headroom.WantsDeferral(12, headroom.Predicted, 12));
        using var report = JsonDocument.Parse(headroom.ReportJson(true));
        Assert.Equal(1, report.RootElement.GetProperty("torn_samples").GetInt64());
        Assert.Equal(2, report.RootElement.GetProperty("stale_checks").GetInt64());
    }
}
