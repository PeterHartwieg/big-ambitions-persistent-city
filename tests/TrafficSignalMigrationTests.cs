using System;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficSignalMigrationTests
{
    // Generated and validated by the old schema-1 implementation. Synthetic only.
    private static TrafficKernelState Legacy(bool handoff = false)
    {
        const double clock = 2186.691011389108;
        var state = new TrafficKernelState { Schema = 1, Clock = clock, RequestedTime = clock,
            NextEventOrder = 2, NextVehicleOrder = 1 };
        state.Definitions.Add(new TrafficLinkDefinition { Id = 0, Capacity = 1, FreeFlowMinutes = 1,
            Signal = new TrafficSignalSchedule { CycleMinutes = 88.22907300336708,
                GreenStartMinutes = 17.645814600673415, GreenDurationMinutes = 26.468721901010124,
                PhaseOffsetMinutes = -84852.4917777872 } });
        var link = new TrafficLinkState { LinkId = 0 }; link.Vehicles.Add("car"); state.Links.Add(link);
        var car = new TrafficVehicleState { VehicleId = "car", Order = 0, RouteIndex = 0,
            EnqueuedAt = clock, EnteredAt = clock, MotionStartedAt = clock, ReadyAt = clock + 1 };
        car.Route.Add(new TrafficRouteLeg { LinkId = 0, EndNominalMinutes = 1, FreeFlowMinutes = 1 });
        state.Vehicles.Add(car);
        state.Events.Add(new TrafficEventState { LinkId = handoff ? 1 : 0, At = 2247.2490911367804, Order = 1 });
        if (handoff)
        {
            state.Definitions.Add(new TrafficLinkDefinition { Id = 1, Capacity = 1, FreeFlowMinutes = 1 });
            var destination = new TrafficLinkState { LinkId = 1 }; destination.IncomingLinks.Add(0); state.Links.Add(destination);
            car.Route.Add(new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 1, EndNominalMinutes = 2, FreeFlowMinutes = 1 });
        }
        return state;
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OldTerminalAndHandoffSchedulesAreRefusedWithoutChangingOwnedState(bool handoff)
    {
        var old = Legacy(handoff); string original = JsonSerializer.Serialize(old);
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(old, false));
        Assert.Throws<ArgumentException>(() => TrafficKernel.ValidateState(old));
        Assert.Equal(original, JsonSerializer.Serialize(old));
    }

    [Theory] [InlineData("missing")] [InlineData("late")] [InlineData("readiness")] [InlineData("future")]
    public void MigrationNeverRepairsMalformedLegacyOrFutureState(string defect)
    {
        var state = Legacy();
        if (defect == "missing") state.Events.Clear();
        if (defect == "late") state.Events[0].At += .001;
        if (defect == "readiness") state.Vehicles[0].ReadyAt -= .001;
        if (defect == "future") state.Schema = 3;
        string original = JsonSerializer.Serialize(state);
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(state, false));
        Assert.Equal(original, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void CurrentSchemaDoesNotAcceptAnOldLateEventAndOldOverflowStateIsUntouched()
    {
        var state = Legacy(); state.Schema = 2;
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(state, false));
        state.Schema = 1; state.NextEventOrder = long.MaxValue;
        string original = JsonSerializer.Serialize(state);
        Assert.Throws<ArgumentException>(() => TrafficKernel.Restore(state, false));
        Assert.Equal(original, JsonSerializer.Serialize(state));
    }
}
