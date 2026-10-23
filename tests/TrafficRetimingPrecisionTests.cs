using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficRetimingPrecisionTests
{
    // Synthetic geometry/times: rounded absolute readiness is 16 ulps earlier than
    // residual movement after a 60x retime. No game save or identity data is embedded.
    private const double Entered = 2204.6706147373316;
    private const double Now = 2205.4809627895665;
    private const double Duration = 3.0717779591160537;

    private static TrafficKernel Interrupted(double shift = 0)
    {
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition
        { Id = 0, Capacity = 2, FreeFlowMinutes = Duration } }, Entered + shift);
        kernel.Enqueue("car", new[] { new TrafficRouteLeg
        { LinkId = 0, StartNominalMinutes = 0, EndNominalMinutes = Duration, FreeFlowMinutes = Duration } });
        kernel.AdvanceTo(Now + shift, 100);
        return kernel;
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 100000000)] [InlineData(true, 100000000)]
    public void AcceleratedResidualTravelRestoresAndDischargesWithoutAnEarlyArrival(bool mapped, double shift)
    {
        var original = Interrupted(shift);
        string unchanged = JsonSerializer.Serialize(original.Capture());
        double progress = original.GetPermission("car").NominalMinutes;
        var retimed = original.RescaleTime(60, mapped ? (_, p) => p <= progress ? p : progress + (p - progress) * 60 : null);
        Assert.Equal(unchanged, JsonSerializer.Serialize(original.Capture()));
        Assert.Equal(progress, retimed.GetPermission("car").NominalMinutes, 10);
        var snapshot = retimed.Capture();
        TrafficKernel.ValidateState(snapshot);
        var reloaded = TrafficKernel.Restore(snapshot, false);
        double ready = snapshot.Vehicles.Single().ReadyAt;
        double expected = Now + shift + (Duration - ((Now + shift) - (Entered + shift))) * 60;
        Assert.InRange(Math.Abs(ready - expected), 0, shift == 0 ? 1e-9 : 1e-5);
        foreach (var kernel in new[] { retimed, reloaded })
        {
            kernel.AdvanceTo(Math.BitDecrement(ready), 100);
            Assert.False(kernel.GetPermission("car").IsCompleted);
            kernel.AdvanceTo(ready, 100);
            Assert.True(kernel.GetPermission("car").IsCompleted);
            Assert.Equal(ready, kernel.GetPermission("car").CompletedAt);
            Assert.Equal(0, kernel.PendingEventCount);
        }
    }

    [Fact]
    public void RetimingRetainsAnExistingLaterReadinessConstraint()
    {
        var snapshot = Interrupted().Capture();
        snapshot.Vehicles.Single().ReadyAt += 5;
        var kernel = TrafficKernel.Restore(snapshot, false);
        double expected = Now + (snapshot.Vehicles.Single().ReadyAt - Now) * 60;
        var retimed = kernel.RescaleTime(60);
        Assert.Equal(expected, retimed.Capture().Vehicles.Single().ReadyAt);
        retimed.AdvanceTo(Math.BitDecrement(expected), 100);
        Assert.False(retimed.GetPermission("car").IsCompleted);
        retimed.AdvanceTo(expected, 100);
        Assert.Equal(expected, retimed.GetPermission("car").CompletedAt);
    }

    [Fact]
    public void RetimingDoesNotWeakenRestoreReadinessValidation()
    {
        var snapshot = Interrupted().RescaleTime(60).Capture();
        snapshot.Vehicles.Single().ReadyAt -= 0.001;
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(snapshot));
    }

    [Fact]
    public void RepeatedClockChangesPreservePhysicalProgressAndRequireEndpointObservation()
    {
        var kernel = Interrupted();
        kernel.SetPhysicalControl("car", true);
        double progress = kernel.GetPermission("car").NominalMinutes;
        double spatial = progress / Duration;
        double rate = 1;
        foreach (double next in new[] { 60.0, 1.0, .25, 1.0, 1000.0, .01, 1.0, 60.0, 1.0 })
        {
            kernel = kernel.RescaleTime(next / rate);
            var snapshot = kernel.Capture();
            TrafficKernel.ValidateState(snapshot);
            var leg = snapshot.Vehicles.Single().Route.Single();
            Assert.Equal(spatial, leg.StartLinkFraction, 10);
            Assert.Equal(progress, kernel.GetPermission("car").NominalMinutes, 10);
            Assert.True(snapshot.Vehicles.Single().PhysicalControl);
            kernel = TrafficKernel.Restore(snapshot, false);
            rate = next;
        }
        kernel.AdvanceTo(Now + 1000, 100);
        Assert.False(kernel.GetPermission("car").IsCompleted);
        double end = kernel.Capture().Vehicles.Single().Route.Single().EndNominalMinutes;
        kernel.ReportPhysicalProgress("car", end);
        kernel.AdvanceTo(Now + 1000, 100);
        Assert.True(kernel.GetPermission("car").IsCompleted);
    }
    private static TrafficKernel WaitingAtSignal()
    {
        const double start = 2209.9961150297877, duration = 1.391151728043869;
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition
        {
            Id = 0, Capacity = 2, FreeFlowMinutes = duration,
            Signal = new TrafficSignalSchedule { CycleMinutes = 6.019881736715269,
                GreenStartMinutes = 1.9862652969515115, GreenDurationMinutes = 1.8059645210145807,
                PhaseOffsetMinutes = 2206.81645835184 }
        } }, start);
        kernel.Enqueue("car", new[] { new TrafficRouteLeg
        { LinkId = 0, StartNominalMinutes = 0, EndNominalMinutes = duration, FreeFlowMinutes = duration } });
        kernel.AdvanceTo(2210.438860509648, 100);
        return kernel;
    }

    [Fact]
    public void RetimeRebuildsRoundedSignalWakeupDeterministicallyWithoutBypassingRed()
    {
        var original = WaitingAtSignal();
        string before = JsonSerializer.Serialize(original.Capture());
        var first = original.RescaleTime(60);
        var second = original.RescaleTime(60);
        Assert.Equal(before, JsonSerializer.Serialize(original.Capture()));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        var state = first.Capture();
        TrafficKernel.ValidateState(state);
        double ready = state.Vehicles.Single().ReadyAt;
        double green = first.NextEventMinute;
        double roundedOldWakeup = original.Clock + (original.NextEventMinute - original.Clock) * 60;
        Assert.True(green < roundedOldWakeup);
        Assert.True(green > ready);
        first.AdvanceTo(ready, 100);
        Assert.False(first.GetPermission("car").IsCompleted);
        first.AdvanceTo(green, 100);
        Assert.Equal(green, first.GetPermission("car").CompletedAt);
        Assert.Equal(0, first.PendingEventCount);
    }

    [Fact]
    public void PublicRestoreStillRejectsMissingAndLateWakeups()
    {
        var missing = WaitingAtSignal().Capture();
        missing.Events.Clear();
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(missing, false));
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(missing));
        var late = WaitingAtSignal().Capture();
        late.Events.Single().At += 0.001;
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(late, false));
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(late));
    }

    [Fact]
    public void WakeupReconstructionOverflowRejectsAtomically()
    {
        var state = WaitingAtSignal().Capture();
        state.NextEventOrder = long.MaxValue;
        var kernel = TrafficKernel.Restore(state, false);
        string before = JsonSerializer.Serialize(kernel.Capture());
        Assert.Throws<InvalidOperationException>(() => kernel.RescaleTime(60));
        Assert.Equal(before, JsonSerializer.Serialize(kernel.Capture()));
    }

    [Fact]
    public void RepeatedRetimingKeepsCompetingQueuesAndMergeRotation()
    {
        var kernel = new TrafficKernel(new[]
        {
            new TrafficLinkDefinition { Id = 1, Capacity = 3, FreeFlowMinutes = Duration },
            new TrafficLinkDefinition { Id = 2, Capacity = 3, FreeFlowMinutes = Duration },
            new TrafficLinkDefinition { Id = 3, Capacity = 1, FreeFlowMinutes = Duration }
        }, Entered);
        kernel.SetLinkClosed(3, true);
        foreach (int approach in new[] { 1, 2 })
            for (int i = 0; i < 3; i++)
                kernel.Enqueue((approach == 1 ? "a" : "b") + i, new[]
                {
                    new TrafficRouteLeg { LinkId = approach, StartNominalMinutes = 0, EndNominalMinutes = Duration },
                    new TrafficRouteLeg { LinkId = 3, StartNominalMinutes = Duration, EndNominalMinutes = Duration * 2 }
                });
        kernel.AdvanceTo(Now, 100);
        double rate = 1;
        foreach (double next in new[] { 60.0, 1.0, .25, 60.0, 1.0 })
        {
            var before = kernel.Capture();
            kernel = kernel.RescaleTime(next / rate);
            var after = kernel.Capture();
            TrafficKernel.ValidateState(after);
            for (int i = 0; i < before.Links.Count; i++)
            {
                Assert.Equal(before.Links[i].Vehicles, after.Links[i].Vehicles);
                Assert.Equal(before.Links[i].MergeCursor, after.Links[i].MergeCursor);
            }
            rate = next;
        }
        kernel.SetLinkClosed(3, false);
        kernel.AdvanceTo(Now + 1000, 100);
        var vehicles = kernel.Capture().Vehicles;
        Assert.All(vehicles, v => Assert.True(v.Completed));
        Assert.Equal(new[] { "a0", "b0", "a1", "b1", "a2", "b2" },
            vehicles.OrderBy(v => v.CompletedAt).Select(v => v.VehicleId));
    }

}
