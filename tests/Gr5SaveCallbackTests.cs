using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5SaveCallbackTests
{
    private static (CityState State, CitySimulator Simulator) City()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        simulator.AdvanceTo(state.Minute + 0.25, int.MaxValue);
        return (state, simulator);
    }

    private static string CopiedTrafficPayload(CityState state)
    {
        state.PrepareForSave();
        var synchronizer = state.SnapshotSynchronizer;
        state.SnapshotSynchronizer = null;
        try
        {
            var copied = SaveCodec.Capture(state);
            Assert.NotSame(state.Traffic, copied.Wire.Traffic);
            return SaveCodec.Encode(copied);
        }
        finally { state.SnapshotSynchronizer = synchronizer; }
    }

    [Fact]
    public void TheSnapshotTakesTheTrafficItsOwnPrepareCapturedAndWritesTheSameBytes()
    {
        var (shared, _) = City();
        var (copied, _) = City();
        var before = shared.Traffic;
        var cost = new SaveCostRecorder();
        cost.Begin();
        var snapshot = SaveCodec.Capture(shared, cost);
        cost.End(true, "");
        Assert.NotSame(before, shared.Traffic);
        Assert.Same(shared.Traffic, snapshot.Wire.Traffic);
        Assert.True(cost.SharedTraffic);
        Assert.Equal(TravelTimingMaximumRouteTests.FixtureLinks, snapshot.Wire.Traffic!.Links.Count);
        Assert.NotEmpty(snapshot.Wire.Traffic.Vehicles);
        Assert.Equal(RushSpikeReplay.Sha(CopiedTrafficPayload(copied)), RushSpikeReplay.Sha(SaveCodec.Encode(snapshot)));
    }

    [Fact]
    public void LaterSimulationSavesAndRetimesNeverReachASharedTrafficSnapshot()
    {
        string expected = CopiedTrafficPayload(City().State);
        var (state, simulator) = City();
        var snapshot = SaveCodec.Capture(state);
        var traffic = snapshot.Wire.Traffic!;
        Assert.Same(traffic, state.Traffic);
        simulator.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        Assert.NotEqual(expected, SaveCodec.Write(state));
        Assert.NotSame(traffic, state.Traffic);
        Assert.Equal(expected, SaveCodec.Encode(snapshot));
        var (retimed, _) = City();
        var retimedSnapshot = SaveCodec.Capture(retimed);
        Assert.Same(retimedSnapshot.Wire.Traffic, retimed.Traffic);
        Assert.True(TravelTiming.Apply(retimed, 2, retimed.TravelClockRate * 2, out _));
        Assert.NotSame(retimedSnapshot.Wire.Traffic, retimed.Traffic);
        Assert.Equal(expected, SaveCodec.Encode(retimedSnapshot));
    }

    [Fact]
    public void TrafficThatThePrepareStepDidNotCaptureIsCopied()
    {
        var (state, _) = City();
        state.PrepareForSave();
        state.SnapshotSynchronizer = null;
        var cost = new SaveCostRecorder();
        cost.Begin();
        var snapshot = SaveCodec.Capture(state, cost);
        cost.End(true, "");
        Assert.NotSame(state.Traffic, snapshot.Wire.Traffic);
        Assert.False(cost.SharedTraffic);
        Assert.True(cost.CaptureSectionTicks(SnapshotSection.Traffic) > 0);
    }

    [Fact]
    public void StepAndSectionTimersPartitionTheMainThreadPhases()
    {
        var city = F7cFixtures.Build("fresh-957x3").State;
        var cost = new SaveCostRecorder();
        var steps = new[] { SaveCostStep.Prepare, SaveCostStep.Outflow, SaveCostStep.Names, SaveCostStep.Wallets };
        for (int save = 0; save < 2; save++)
        {
            cost.Begin();
            var snapshot = SaveCodec.Capture(city, cost);
            cost.End(true, "");
            Assert.Equal(SaveCodec.Write(city), SaveCodec.Encode(snapshot));
            Assert.InRange(steps.Sum(step => cost.StepTicks(step)), 1, cost.PhaseTicks(SaveCostPhase.Synchronize));
            long sections = Enum.GetValues<SnapshotSection>().Sum(section => cost.CaptureSectionTicks(section));
            Assert.InRange(sections + cost.StepTicks(SaveCostStep.LinkWallets), 1, cost.PhaseTicks(SaveCostPhase.Capture));
            foreach (var section in new[] { SnapshotSection.Residents, SnapshotSection.Visitors, SnapshotSection.Food })
                Assert.True(cost.CaptureSectionTicks(section) > 0, section + " capture not timed");
        }
        cost.Begin();
        Assert.All(Enum.GetValues<SaveCostStep>(), step => Assert.Equal(0, cost.StepTicks(step)));
        Assert.All(Enum.GetValues<SnapshotSection>(), section => Assert.Equal(0, cost.CaptureSectionTicks(section)));
    }
}
