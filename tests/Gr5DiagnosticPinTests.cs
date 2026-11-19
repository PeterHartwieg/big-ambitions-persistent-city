using System;
using System.Linq;
using System.Runtime.CompilerServices;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5DiagnosticPinTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ActiveTraceAndSnapshot(FoodDecisionRecorder recorder, ResearchSnapshotCache snapshots)
    {
        var (_, city, _) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        recorder.Start(city);
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 12 * 60; minute += 1.0 / 30) city.AdvanceTo(minute, 64);
        Assert.True(recorder.Recording);
        Assert.NotEmpty(recorder.Lines);
        Assert.True(city.ObservingFood);
        Assert.NotNull(snapshots.Get(city, 1));
        Assert.Same(city, snapshots.City);
        return new WeakReference(city);
    }

    [Fact]
    public void ACityTeardownReleasesTheTracedCityAndTheResearchSnapshotWithoutAReplacement()
    {
        var recorder = new FoodDecisionRecorder(trace => trace.Consumer.Id, _ => { });
        var snapshots = new ResearchSnapshotCache();
        var city = ActiveTraceAndSnapshot(recorder, snapshots);
        recorder.Detach(); snapshots.Forget();
        Assert.False(recorder.Recording);
        Assert.Empty(recorder.Lines);
        Assert.Null(recorder.Observed);
        Assert.Null(snapshots.City);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(city.IsAlive);
        GC.KeepAlive(recorder); GC.KeepAlive(snapshots);
    }

    [Fact]
    public void StoppingKeepsTheRingAndARebuildMovesTheRecorderToTheReplacement()
    {
        var (_, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        var recorder = new FoodDecisionRecorder(trace => trace.Consumer.Id, _ => { });
        recorder.Start(city);
        var rebuild = city.BeginRebuildRoutes(planner, null);
        while (!rebuild.Step(long.MaxValue)) { }
        var replacement = rebuild.TakeResult();
        recorder.Rebind(replacement);
        Assert.Same(replacement, recorder.Observed);
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 12 * 60; minute += 1.0 / 30) replacement.AdvanceTo(minute, 64);
        int recorded = recorder.Lines.Count();
        Assert.True(recorded > 0);
        recorder.Stop();
        Assert.False(replacement.ObservingFood);
        Assert.Equal(recorded, recorder.Lines.Count());
    }
}
