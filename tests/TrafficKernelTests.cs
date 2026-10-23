using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficKernelTests
{
    private static TrafficLinkDefinition Link(int id, int capacity = 1, double free = 1, double headway = 0) =>
        new() { Id = id, Capacity = capacity, FreeFlowMinutes = free, HeadwayMinutes = headway };
    private static List<TrafficRouteLeg> Route(params int[] ids) => ids.Select((id, i) =>
        new TrafficRouteLeg { LinkId = id, StartNominalMinutes = i, EndNominalMinutes = i + 1 }).ToList();
    private static TrafficVehicleSnapshot Vehicle(TrafficKernel kernel, string id) => kernel.GetPermission(id);
    private static string State(TrafficKernel kernel) => JsonSerializer.Serialize(kernel.Capture());

    [Fact]
    public void EmptyClosedSectionRemainsDiscoverableUntilNativeReopeningAfterReload()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        kernel.Enqueue("car", Route(1)); kernel.AdvanceTo(0, 10);
        kernel.SetLinkClosed(1, true); kernel.AdvanceTo(10, 100); kernel.RemoveCompleted("car");
        Assert.Equal(0, kernel.VehicleCount);
        kernel = TrafficKernel.Restore(kernel.Capture());
        Assert.Contains(1, kernel.RelevantLinkIds);
        foreach (int id in kernel.RelevantLinkIds) kernel.SetLinkClosed(id, false);
        Assert.False(kernel.Forecast(1, 10).Closed);
        Assert.True(Position.Finite(kernel.Forecast(1, 10).DelayMinutes));
        Assert.DoesNotContain(1, kernel.RelevantLinkIds);
    }

    [Fact]
    public void CapacityFifoAndDischargeHeadwayApplyAcrossLargeTimeSkip()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2, headway: 2) });
        foreach (string id in new[] { "a", "b", "c" }) kernel.Enqueue(id, Route(1));
        kernel.AdvanceTo(0, 10);
        Assert.Equal(2, kernel.Forecast(1, 0).Occupancy);
        Assert.Equal(-1, Vehicle(kernel, "c").RouteIndex);
        kernel.AdvanceTo(100, 100);
        Assert.Equal(1, Vehicle(kernel, "a").CompletedAt);
        Assert.Equal(3, Vehicle(kernel, "b").CompletedAt);
        Assert.Equal(5, Vehicle(kernel, "c").CompletedAt);
        Assert.Equal(0, kernel.PendingEventCount);
        Assert.True(kernel.RemoveCompleted("a"));
        Assert.False(kernel.RemoveCompleted("a"));
    }

    [Fact]
    public void ClosedDownstreamSpillsBackWithoutPeriodicRetryEvents()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2), Link(2), Link(3) });
        kernel.SetLinkClosed(3, true);
        for (int i = 0; i < 5; i++) kernel.Enqueue("v" + i, Route(1, 2, 3));
        kernel.AdvanceTo(100, 100);
        Assert.Equal(2, kernel.Forecast(1, 100).Occupancy);
        Assert.Equal(1, kernel.Forecast(2, 100).Occupancy);
        Assert.Equal(0, kernel.PendingEventCount);
        Assert.False(Vehicle(kernel, "v0").IsCompleted);
        kernel.SetLinkClosed(3, false);
        kernel.AdvanceTo(200, 100);
        Assert.All(Enumerable.Range(0, 5), i => Assert.True(Vehicle(kernel, "v" + i).IsCompleted));
    }

    [Fact]
    public void MergeRotatesStableIncomingLinksRatherThanStarvingOneApproach()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 3), Link(2, 3), Link(3) });
        for (int i = 0; i < 3; i++) kernel.Enqueue("a" + i, Route(1, 3));
        for (int i = 0; i < 3; i++) kernel.Enqueue("b" + i, Route(2, 3));
        kernel.AdvanceTo(100, 100);
        string[] order = kernel.Capture().Vehicles.OrderBy(v => v.CompletedAt).Select(v => v.VehicleId).ToArray();
        Assert.Equal(new[] { "a0", "b0", "a1", "b1", "a2", "b2" }, order);
    }

    [Fact]
    public void SourceDemandParticipatesInFairMergeAndDoesNotConsumeFiniteStorage()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 3), Link(2) });
        kernel.SetLinkClosed(2, true);
        kernel.Enqueue("a", Route(1, 2));
        kernel.Enqueue("b", Route(1, 2));
        kernel.Enqueue("source0", Route(2));
        kernel.Enqueue("source1", Route(2));
        kernel.AdvanceTo(2, 20);
        Assert.Equal(0, kernel.Forecast(2, 2).Occupancy);
        kernel.SetLinkClosed(2, false);
        kernel.AdvanceTo(100, 100);
        string[] order = kernel.Capture().Vehicles.OrderBy(v => v.CompletedAt).Select(v => v.VehicleId).ToArray();
        Assert.Equal(new[] { "a", "source0", "b", "source1" }, order);
    }

    [Fact]
    public void SignalsScheduleTheNextGreenWithoutPolling()
    {
        TrafficLinkDefinition link = Link(1, 3, headway: 1);
        link.Signal = new TrafficSignalSchedule { CycleMinutes = 10, GreenStartMinutes = 5, GreenDurationMinutes = 2 };
        var kernel = new TrafficKernel(new[] { link });
        for (int i = 0; i < 3; i++) kernel.Enqueue("v" + i, Route(1));
        kernel.AdvanceTo(4, 100);
        Assert.Equal(5, kernel.NextEventMinute);
        kernel.AdvanceTo(100, 100);
        Assert.Equal(new double?[] { 5, 6, 15 }, kernel.Capture().Vehicles.Select(v => v.CompletedAt).ToArray());
    }

    [Fact]
    public void PhysicalHeadHoldsOccupancyAndCapsFollowingProgressUntilActualEndpointReport()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2), Link(2) });
        kernel.Enqueue("physical", Route(1, 2));
        kernel.Enqueue("following", Route(1, 2));
        kernel.AdvanceTo(0, 100);
        kernel.SetPhysicalControl("physical", true);
        kernel.AdvanceTo(10, 100);
        Assert.Equal(0, kernel.PendingEventCount);
        Assert.Equal(1, Vehicle(kernel, "physical").LinkId);
        Assert.Equal(0, Vehicle(kernel, "following").MaximumNominalMinutes);
        Assert.Equal(0.1, kernel.ReportPhysicalProgress("physical", 0.1), 8);
        Assert.Equal(0, Vehicle(kernel, "following").MaximumNominalMinutes);
        Assert.Equal(1, kernel.ReportPhysicalProgress("physical", 100));
        Assert.False(Vehicle(kernel, "physical").IsCompleted);
        kernel.AdvanceTo(10, 100);
        Assert.Equal(2, Vehicle(kernel, "physical").LinkId);
        Assert.Equal(1, Vehicle(kernel, "following").LinkId);
        Assert.False(Vehicle(kernel, "physical").IsCompleted);
    }

    [Fact]
    public void ReleasingPhysicalLeaseContinuesRemainingDistanceWithoutNewDemandOrEarlyArrival()
    {
        var kernel = new TrafficKernel(new[] { Link(1, free: 10) });
        kernel.Enqueue("car", new[] { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 4, EndNominalMinutes = 14 } });
        kernel.AdvanceTo(0, 10);
        kernel.SetPhysicalControl("car", true);
        kernel.AdvanceTo(5, 10);
        Assert.Equal(6, kernel.ReportPhysicalProgress("car", 6));
        long order = kernel.Capture().Vehicles[0].Order;
        kernel.SetPhysicalControl("car", false);
        Assert.Equal(6, Vehicle(kernel, "car").NominalMinutes);
        kernel.AdvanceTo(12, 20);
        Assert.False(Vehicle(kernel, "car").IsCompleted);
        kernel.AdvanceTo(13, 20);
        Assert.Equal(13, Vehicle(kernel, "car").CompletedAt);
        Assert.Equal(order, kernel.Capture().Vehicles[0].Order);
        Assert.Equal(1, kernel.VehicleCount);
    }

    [Fact]
    public void PartialLinkUsesPartialRouteDurationAndNominalOffsetsRemainGeometryCoordinates()
    {
        var kernel = new TrafficKernel(new[] { Link(1, free: 100) }, 50);
        kernel.Enqueue("partial", new[] { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 8, EndNominalMinutes = 10 } });
        kernel.AdvanceTo(51, 20);
        Assert.Equal(9, Vehicle(kernel, "partial").NominalMinutes);
        kernel.AdvanceTo(52, 20);
        Assert.Equal(52, Vehicle(kernel, "partial").CompletedAt);
    }

    [Fact]
    public void SaveReloadAndBudgetInterruptionsKeepExactEventOrderAndCompletionTimes()
    {
        var continuous = new TrafficKernel(new[] { Link(1, 4, headway: 0.3), Link(2, 2), Link(3, 2) });
        for (int i = 0; i < 30; i++) continuous.Enqueue("v" + i, i % 2 == 0 ? Route(1, 3) : Route(2, 3));
        var interrupted = TrafficKernel.Restore(continuous.Capture(), false);
        continuous.AdvanceTo(100, 10000);
        int turns = 0;
        while (!interrupted.AdvanceTo(100, 1).ReachedTarget)
        {
            interrupted = TrafficKernel.Restore(interrupted.Capture(), false);
            Assert.True(interrupted.PendingEventCount <= interrupted.LinkCount);
            Assert.True(++turns < 1000);
        }
        Assert.Equal(State(continuous), State(interrupted));
    }

    [Fact]
    public void ReloadReleasesPhysicalLeaseFromSavedActualProgressByExplicitDefaultPolicy()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        kernel.Enqueue("car", Route(1));
        kernel.AdvanceTo(0, 10);
        kernel.SetPhysicalControl("car", true);
        kernel.AdvanceTo(10, 10);
        kernel.ReportPhysicalProgress("car", 0.25);
        var restored = TrafficKernel.Restore(kernel.Capture());
        Assert.False(Vehicle(restored, "car").PhysicalControl);
        Assert.Equal(0.25, Vehicle(restored, "car").NominalMinutes);
        restored.AdvanceTo(10.74, 20);
        Assert.False(Vehicle(restored, "car").IsCompleted);
        restored.AdvanceTo(10.75, 20);
        Assert.Equal(10.75, Vehicle(restored, "car").CompletedAt);
    }

    [Fact]
    public void CompletionCallbackIsOnceAndSafeForImmediateAcknowledgement()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        int calls = 0;
        kernel.VehicleCompleted += (id, at) => { Assert.Equal(1, at); calls++; Assert.True(kernel.RemoveCompleted(id)); };
        kernel.Enqueue("car", Route(1));
        kernel.AdvanceTo(100, 20);
        kernel.AdvanceTo(200, 20);
        Assert.Equal(1, calls);
        Assert.Equal(0, kernel.VehicleCount);
    }

    [Fact]
    public void ForecastIsReadOnlyAndCancellationWakesBackpressureWithoutRecreatingDemand()
    {
        var kernel = new TrafficKernel(new[] { Link(1), Link(2) });
        kernel.SetLinkClosed(2, true);
        kernel.Enqueue("blocked", Route(1, 2));
        kernel.Enqueue("waiting", Route(1));
        kernel.AdvanceTo(10, 100);
        string before = State(kernel);
        Assert.True(double.IsPositiveInfinity(kernel.Forecast(2, 10).DelayMinutes));
        kernel.Forecast(1, 10);
        Assert.Equal(before, State(kernel));
        Assert.True(kernel.Cancel("blocked"));
        Assert.False(kernel.Cancel("blocked"));
        kernel.AdvanceTo(100, 100);
        Assert.True(Vehicle(kernel, "waiting").IsCompleted);
        Assert.Equal(1, kernel.VehicleCount);
    }

    [Fact]
    public void RetimingAnchorsAtCurrentClockAndPreservesGeometryProgress()
    {
        var kernel = new TrafficKernel(new[] { Link(1) }, 100);
        kernel.Enqueue("car", new[] { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 2, EndNominalMinutes = 12 } });
        kernel.AdvanceTo(104, 20);
        var retimed = kernel.RescaleTime(2);
        Assert.Equal(104, retimed.Clock);
        Assert.Equal(6, Vehicle(retimed, "car").NominalMinutes);
        retimed.AdvanceTo(110, 20);
        Assert.False(Vehicle(retimed, "car").IsCompleted);
        Assert.Equal(9, Vehicle(retimed, "car").NominalMinutes);
        retimed.AdvanceTo(116, 20);
        Assert.Equal(116, Vehicle(retimed, "car").CompletedAt);
    }

    [Fact]
    public void ValidationRejectsFutureSchemaDuplicateOccupancyAndBrokenEventCursors()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2) });
        kernel.Enqueue("car", Route(1));
        kernel.AdvanceTo(0, 10);
        TrafficKernelState state = kernel.Capture();
        state.Schema = 99;
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(state));
        state = kernel.Capture();
        state.Links[0].Vehicles.Add("car");
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(state));
        state = kernel.Capture();
        state.Events[0].At = -1;
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(state));
    }

    [Fact]
    public void LargeDormantQueueHasAtMostOneEventPerLinkAndZeroBudgetDoesNotSkipIt()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        for (int i = 0; i < 5000; i++) kernel.Enqueue("v" + i, Route(1));
        Assert.Equal(1, kernel.PendingEventCount);
        Assert.False(kernel.AdvanceTo(100, 0).ReachedTarget);
        Assert.Equal(0, kernel.Clock);
        kernel.SetLinkClosed(1, true);
        kernel.AdvanceTo(100, 1);
        Assert.Equal(0, kernel.PendingEventCount);
        Assert.Equal(5000, kernel.VehicleCount);
        Assert.Equal(0, kernel.Forecast(1, 100).Occupancy);
    }

    [Fact]
    public void PartialInteriorSourceWaitsOutsideSectionAndFollowingCapsUseWholeLinkSpace()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 3) });
        kernel.Enqueue("full", Route(1));
        kernel.Enqueue("interior", new[] { new TrafficRouteLeg
        { LinkId = 1, StartNominalMinutes = 4, EndNominalMinutes = 5, StartLinkFraction = 0.5, EndLinkFraction = 1 } });
        kernel.Enqueue("rear", Route(1));
        kernel.AdvanceTo(0, 100);
        Assert.Equal(-1, Vehicle(kernel, "interior").RouteIndex);
        Assert.Equal(-1, Vehicle(kernel, "rear").RouteIndex);
        kernel.AdvanceTo(1, 100);
        Assert.Equal(1, Vehicle(kernel, "interior").LinkId);
        Assert.Equal(1, Vehicle(kernel, "rear").LinkId);
        kernel.SetPhysicalControl("interior", true);
        kernel.AdvanceTo(2, 100);
        Assert.Equal(4, Vehicle(kernel, "interior").NominalMinutes);
        Assert.Equal(1.0 / 6, Vehicle(kernel, "rear").MaximumNominalMinutes, 8);
        kernel.ReportPhysicalProgress("interior", 4.5);
        Assert.Equal(5.0 / 12, Vehicle(kernel, "rear").MaximumNominalMinutes, 8);
    }

    [Fact]
    public void PartialDestinationVacatesAtItsOwnEndpointWithoutCreditForUntravelledRemainder()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2) });
        kernel.Enqueue("curb", new[] { new TrafficRouteLeg
        { LinkId = 1, StartNominalMinutes = 0, EndNominalMinutes = 1, EndLinkFraction = 0.5 } });
        kernel.Enqueue("through", new[] { new TrafficRouteLeg
        { LinkId = 1, StartNominalMinutes = 0, EndNominalMinutes = 2 } });
        kernel.AdvanceTo(0, 100);
        kernel.SetPhysicalControl("curb", true);
        kernel.AdvanceTo(1, 100);
        kernel.ReportPhysicalProgress("curb", 0.5);
        Assert.Equal(0, Vehicle(kernel, "through").MaximumNominalMinutes);
        kernel.ReportPhysicalProgress("curb", 1);
        kernel.AdvanceTo(1, 100);
        Assert.True(Vehicle(kernel, "curb").IsCompleted);
        Assert.Equal(1, Vehicle(kernel, "through").MaximumNominalMinutes);
        kernel.AdvanceTo(2, 100);
        Assert.Equal(2, Vehicle(kernel, "through").CompletedAt);
    }

    [Fact]
    public void RetimingWithFixedCompletedPrefixPreservesActualSpatialPointAndRemainingTravel()
    {
        var kernel = new TrafficKernel(new[] { Link(1, 2) });
        kernel.Enqueue("head", new[] { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 0, EndNominalMinutes = 10 } });
        kernel.Enqueue("rear", new[] { new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 0, EndNominalMinutes = 10 } });
        kernel.AdvanceTo(0, 100);
        kernel.SetPhysicalControl("head", true);
        kernel.AdvanceTo(5, 100);
        kernel.ReportPhysicalProgress("head", 2);
        var retimed = kernel.RescaleTime(2, (id, p) => id == "head" ? (p <= 2 ? p : 2 + (p - 2) * 2) : p * 2);
        Assert.Equal(2, Vehicle(retimed, "head").NominalMinutes);
        Assert.Equal(0, Vehicle(retimed, "rear").MaximumNominalMinutes);
        TrafficVehicleState saved = retimed.Capture().Vehicles.Single(v => v.VehicleId == "head");
        Assert.Equal(0.2, saved.Route[0].StartLinkFraction, 8);
        Assert.Equal(2, saved.Route[0].StartNominalMinutes);
        retimed.SetPhysicalControl("head", false);
        retimed.AdvanceTo(20.9, 100);
        Assert.False(Vehicle(retimed, "head").IsCompleted);
        retimed.AdvanceTo(21, 100);
        Assert.Equal(21, Vehicle(retimed, "head").CompletedAt);
    }

    [Fact]
    public void MissingWakeupIsRejectedAndSnapshotsAreDetached()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        kernel.Enqueue("car", Route(1));
        TrafficKernelState state = kernel.Capture();
        state.Events.Clear();
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(state));
        state.Definitions[0].Capacity = 500;
        state.Vehicles[0].Route[0].EndNominalMinutes = 500;
        Assert.Equal(1, kernel.Capture().Definitions[0].Capacity);
        Assert.Equal(1, kernel.Capture().Vehicles[0].Route[0].EndNominalMinutes);
    }

    [Fact]
    public void RelevantClosuresIncludeDormantBlockedReceiverAndAreStableDetachedSnapshots()
    {
        var kernel = new TrafficKernel(new[] { Link(9), Link(1), Link(4) });
        kernel.SetLinkClosed(9, true);
        kernel.Enqueue("car", Route(1, 9));
        kernel.AdvanceTo(10, 100);
        var relevant = kernel.RelevantLinkIds.ToArray();
        Assert.Equal(new[] { 1, 9 }, relevant);
        kernel.Cancel("car");
        Assert.Equal(new[] { 9 }, kernel.RelevantLinkIds);
        kernel.SetLinkClosed(9, false);
        Assert.Empty(kernel.RelevantLinkIds);
        Assert.Equal(new[] { 1, 9 }, relevant);
    }


    [Fact]
    public void FreelyMovingOrOverduePhysicalHeadHasFiniteReadonlyRoutingForecast()
    {
        var kernel = new TrafficKernel(new[] { Link(1) });
        kernel.Enqueue("car", Route(1));
        kernel.AdvanceTo(0, 10);
        kernel.SetPhysicalControl("car", true);
        kernel.AdvanceTo(0.5, 10);
        Assert.True(double.IsFinite(kernel.Forecast(1, 0.5).DelayMinutes));
        kernel.AdvanceTo(100, 10);
        string before = State(kernel);
        TrafficLinkForecast forecast = kernel.Forecast(1, 100);
        Assert.True(double.IsFinite(forecast.DelayMinutes));
        Assert.True(forecast.DelayMinutes >= 0);
        Assert.Equal(before, State(kernel));
        Assert.Equal(0, kernel.PendingEventCount);
    }

}
