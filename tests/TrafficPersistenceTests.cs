using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficPersistenceTests
{
    [Fact]
    public void InterruptedFiniteQueueRoundTripPreservesOwnershipEventsAndNominalGeometry()
    {
        var state = QueuedCity();
        string wire = SaveCodec.Write(state);
        var read = SaveCodec.Read(wire);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.False(read.Migrated);
        Assert.Equal(CityState.CurrentSchema, read.SourceSchema);
        Assert.Equal(wire, SaveCodec.Write(read.State!));
        var restored = TrafficKernel.Restore(read.State!.Traffic!, false);
        Assert.Equal(2, restored.VehicleCount);
        Assert.Equal(7, restored.GetPermission("c0").NominalMinutes);
        Assert.Equal(4, restored.GetPermission("c1").NominalMinutes);
        Assert.Equal(new[] { "c0" }, read.State.Traffic!.Links[0].Vehicles);
        Assert.Equal(new[] { "c1" }, read.State.Traffic.Links[0].WaitingSources);
        Assert.DoesNotContain("SnapshotSynchronizer", wire);
    }

    [Fact]
    public void LegacyTrafficMigrationIsExplicitAndOnlyTheFirstReadReportsIt()
    {
        var state = QueuedCity();
        var kernel = TrafficKernel.Restore(state.Traffic!, false);
        kernel.SetPhysicalControl("c0", true);
        state.Traffic = kernel.Capture();
        var wire = JObject.Parse(SaveCodec.Write(state)); wire["Traffic"]!["Schema"] = 1;
        string source = wire.ToString();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = source, ["rlo:state"] = "unchanged" };
        var read = SaveStore.ReadFrom(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.True(read.Migrated);
        Assert.Equal(2, read.State!.Traffic!.Schema);
        Assert.True(read.State.Traffic.Vehicles[0].PhysicalControl);
        Assert.Equal(source, data[SaveStore.StateKey]); Assert.Equal("unchanged", data["rlo:state"]);
        string migrated = SaveCodec.Write(read.State);
        var again = SaveCodec.Read(migrated);
        Assert.Equal(SaveReadStatus.Loaded, again.Status); Assert.False(again.Migrated);
        Assert.Equal(migrated, SaveCodec.Write(again.State!));
        wire["Traffic"]!["Vehicles"]![0]!["PhysicalControl"] = false;
        wire["Traffic"]!["Events"] = new JArray();
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Fact]
    public void SaveSynchronizerRunsBeforeClockAndProgressValidation()
    {
        var state = QueuedCity();
        state.Traffic!.Clock = 1;
        bool called = false;
        typeof(CityState).GetField("SnapshotSynchronizer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(state, (Action)(() => { called = true; state.Traffic.Clock = state.Minute; }));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        Assert.True(called);
    }

    [Fact]
    public void SchemaSixMigratesWithoutInventingTrafficAndKeepsExistingJourney()
    {
        var state = QueuedCity(); state.Traffic = null; state.TrafficTopologyId = "";
        var wire = JObject.Parse(SaveCodec.Write(state)); wire["Schema"] = 6;
        wire.Remove("Traffic"); wire.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(wire);
        string source = wire.ToString();
        var read = SaveCodec.Read(source);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.True(read.Migrated);
        Assert.Equal(6, read.SourceSchema); Assert.Equal(CityState.CurrentSchema, read.State!.Schema);
        Assert.Null(read.State.Traffic); Assert.Equal("", read.State.TrafficTopologyId);
        Assert.Equal(state.Citizens[0].NextEventMinute, read.State.Citizens[0].NextEventMinute);
        Assert.Equal(state.Citizens[0].Journey!.PositionAt(7).X, read.State.Citizens[0].Journey!.PositionAt(7).X);
        wire["Traffic"] = null;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData("clock")]
    [InlineData("identity")]
    [InlineData("duplicate-occupant")]
    [InlineData("missing-event")]
    [InlineData("wrong-progress")]
    [InlineData("walk-span")]
    [InlineData("past-end")]
    [InlineData("missing-field")]
    [InlineData("unknown-field")]
    [InlineData("order-as-string")]
    [InlineData("future-kernel")]
    [InlineData("topology")]
    public void InvalidTrafficIsRejectedWithoutOverwritingOriginalPayload(string defect)
    {
        var state = QueuedCity(); var wire = JObject.Parse(SaveCodec.Write(state));
        var traffic = (JObject)wire["Traffic"]!;
        if (defect == "clock") traffic["Clock"] = state.Minute + 1;
        if (defect == "identity") traffic["Vehicles"]![0]!["VehicleId"] = "unowned";
        if (defect == "duplicate-occupant") ((JArray)traffic["Links"]![0]!["WaitingSources"]!).Add("c0");
        if (defect == "missing-event") traffic["Events"] = new JArray();
        if (defect == "wrong-progress") wire["Citizens"]![0]!["DepartureMinute"] = 1441;
        if (defect == "wrong-progress") wire["Citizens"]![0]!["NextEventMinute"] = 1459;
        if (defect == "walk-span") traffic["Vehicles"]![0]!["Route"]![0]!["StartNominalMinutes"] = 0;
        if (defect == "past-end") traffic["Vehicles"]![0]!["Route"]![0]!["EndNominalMinutes"] = 19;
        if (defect == "missing-field") ((JObject)traffic["Vehicles"]![0]!).Remove("MotionStartFraction");
        if (defect == "unknown-field") traffic["Economy"] = new JObject();
        if (defect == "order-as-string") traffic["NextEventOrder"] = "1";
        if (defect == "future-kernel") traffic["Schema"] = 3;
        if (defect == "topology") wire["TrafficTopologyId"] = "unknown";
        string source = wire.ToString();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = source, ["rlo:state"] = "unchanged" };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, state.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.Equal(source, data[SaveStore.StateKey]); Assert.Equal("unchanged", data["rlo:state"]);
    }

    [Fact]
    public void ClockChangeRetimesDetachedQueueAndGeometryTogetherWithoutChangingPresentPose()
    {
        var state = QueuedCity();
        var oldTraffic = state.Traffic!; var oldRoute = state.Citizens[0].Journey!;
        double oldX = oldRoute.PositionAt(7).X;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.NotSame(oldTraffic, state.Traffic);
        Assert.NotSame(oldRoute, state.Citizens[0].Journey);
        Assert.Equal(oldX, state.Citizens[0].Journey!.PositionAt(7).X);
        Assert.Equal(2, state.Citizens[0].Journey!.Legs[1].Minutes); // Preparation remains in game minutes.
        Assert.Equal(new[] { "c0" }, state.Traffic!.Links[0].Vehicles);
        Assert.Equal(new[] { "c1" }, state.Traffic.Links[0].WaitingSources);
        Assert.Equal(20, state.Traffic.Definitions[0].FreeFlowMinutes);
        Assert.Equal(4, state.Traffic.Definitions[0].HeadwayMinutes);
        Assert.Equal(state.Minute, state.Traffic.Clock);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        Assert.Equal(10, oldTraffic.Definitions[0].FreeFlowMinutes); // Detached source snapshot was untouched.
        Assert.Equal(18, oldRoute.Minutes);
    }

    [Fact]
    public void InvalidTrafficRetimeLeavesEveryCitizenAndClockUntouched()
    {
        var state = QueuedCity();
        var originalTraffic = state.Traffic; var originalRoutes = state.Citizens.Select(c => c.Journey).ToArray();
        var originalEvents = state.Citizens.Select(c => c.NextEventMinute).ToArray();
        state.Traffic!.Vehicles[0].VehicleId = "unowned";
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Same(originalTraffic, state.Traffic);
        Assert.Equal(1, state.TravelClockRate);
        Assert.Equal(originalEvents, state.Citizens.Select(c => c.NextEventMinute));
        for (int i = 0; i < originalRoutes.Length; i++) Assert.Same(originalRoutes[i], state.Citizens[i].Journey);
    }

    [Fact]
    public void SuccessfulRetimeDetachesOldSnapshotOwnerButInvalidOrNoOpConversionRetainsIt()
    {
        var field = typeof(CityState).GetField("SnapshotSynchronizer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = QueuedCity(); bool synchronized = false;
        Action owner = () => synchronized = true;
        field.SetValue(state, owner);
        Assert.False(TravelTiming.Apply(state, 2, 1));
        Assert.Same(owner, field.GetValue(state));
        Assert.Throws<ArgumentOutOfRangeException>(() => TravelTiming.Apply(state, 2, double.NaN));
        Assert.Same(owner, field.GetValue(state)); Assert.False(synchronized);
        state.PrepareForSave(); Assert.True(synchronized); synchronized = false;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Null(field.GetValue(state));
        SaveCodec.Write(state); Assert.False(synchronized);
    }

    [Fact]
    public void PhysicalSaveAndClockChangePreserveActualQueuePositionBeforeSafeBackgroundRelease()
    {
        var state = QueuedCity();
        var kernel = TrafficKernel.Restore(state.Traffic!, false);
        kernel.SetPhysicalControl("c0", true);
        state.Traffic = kernel.Capture();
        var read = SaveCodec.Read(SaveCodec.Write(state));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.True(read.State!.Traffic!.Vehicles[0].PhysicalControl);
        var released = TrafficKernel.Restore(read.State.Traffic);
        Assert.False(released.GetPermission("c0").PhysicalControl);
        Assert.Equal(7, released.GetPermission("c0").NominalMinutes);
        Assert.True(TravelTiming.Apply(state, 2, 0.5));
        Assert.Equal(40, state.Citizens[0].Journey!.PositionAt(7).X);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        var retimed = TrafficKernel.Restore(state.Traffic!);
        Assert.False(retimed.GetPermission("c0").PhysicalControl);
        Assert.Equal(7, retimed.GetPermission("c0").NominalMinutes);
        Assert.Equal(new[] { "c0" }, state.Traffic!.Links[0].Vehicles);
    }

    [Fact]
    public void OccupiedMaximumSizePartialRouteCannotBeHalfRetimed()
    {
        var state = QueuedCity(); var citizen = state.Citizens[0];
        var legs = new List<RouteLeg> { Leg(0, 10, 2, TravelMode.Walk), Leg(10, 10, 2, TravelMode.Car) };
        for (int i = 0; i < 508; i++)
            legs.Add(Leg(10 + 100.0 * i / 508, 10 + 100.0 * (i + 1) / 508, 10.0 / 508, TravelMode.Car));
        legs.Add(Leg(110, 110, 2, TravelMode.Car)); legs.Add(Leg(110, 120, 2, TravelMode.Walk));
        citizen.Journey!.Legs = legs; citizen.NextEventMinute = citizen.DepartureMinute + citizen.Journey.Minutes;
        var route = citizen.Journey; var traffic = state.Traffic;
        var exception = Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Contains("maximum-size", exception.Message);
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic);
        Assert.Equal(1, state.TravelClockRate);
    }

    private static CityState QueuedCity()
    {
        var state = Population.Create(Enumerable.Range(0, 2).Select(i => new CitizenIdentity
        { Id = "c" + i, Name = "Queue Citizen " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 2, Position = new Position(120, 0, 0) }
        }, "traffic-persistence", "0.40.4", 7, 1440);
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition { Id = 0, Capacity = 1, FreeFlowMinutes = 10, HeadwayMinutes = 2 } }, 1444);
        foreach (var citizen in state.Citizens)
        {
            citizen.PlaceId = "home"; citizen.DayPlaceId = "work"; citizen.Activity = Activity.Home;
            citizen.DestinationId = "work"; citizen.DestinationActivity = Activity.Work; citizen.Phase = CitizenPhase.Travelling;
            citizen.ActivityUntilMinute = 2400; citizen.Preferences.OwnsCar = true;
            citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = new Position(10, 0, 0) };
            citizen.Journey = new Route { Legs = new List<RouteLeg> {
                Leg(0, 10, 2, TravelMode.Walk), Leg(10, 10, 2, TravelMode.Car), Leg(10, 110, 10, TravelMode.Car),
                Leg(110, 110, 2, TravelMode.Car), Leg(110, 120, 2, TravelMode.Walk) } };
            kernel.Enqueue(citizen.Identity.Id, new[] { new TrafficRouteLeg { LinkId = 0, StartNominalMinutes = 4, EndNominalMinutes = 14, FreeFlowMinutes = 10 } });
        }
        kernel.AdvanceTo(1447, 100);
        state.Minute = kernel.Clock;
        foreach (var citizen in state.Citizens)
        {
            citizen.DepartureMinute = state.Minute - kernel.GetPermission(citizen.Identity.Id).NominalMinutes;
            citizen.NextEventMinute = citizen.DepartureMinute + citizen.Journey!.Minutes;
        }
        state.Traffic = kernel.Capture(); state.TrafficTopologyId = new string('a', 64);
        return state;
    }

    private static RouteLeg Leg(double from, double to, double minutes, TravelMode mode) => new()
    { From = new Position(from, 0, 0), To = new Position(to, 0, 0), Minutes = minutes, Mode = mode };
}
