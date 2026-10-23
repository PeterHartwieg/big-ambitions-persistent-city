using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class CommuteTimingTests
{
    private sealed class NativePacePlanner : ICommuteTimingPlanner
    {
        public double Minutes = 120;
        public int Calls;
        public bool Reachable = true;
        public readonly List<(string From, string To, double At)> Queries = new();
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            Calls++;
            Queries.Add((from.Id, to.Id, departureMinute));
            if (!Reachable) return null;
            return new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = Minutes } } };
        }
    }

    private static CityState City(Persona persona = Persona.Commuter, double now = 1440)
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "commuter", Name = "Commuter", Age = 30, Persona = persona, AppearanceSeed = 1 } }, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0,0,0) },
            new Place { Id = "work", Name = "Work", Kind = persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work, Capacity = 1, Position = new Position(1000,0,0) }
        }, "commute-test", "0.40.4", 7, now);
        city.Citizens[0].DayPlaceId = "work";
        return city;
    }

    private static void Prepare(CitySimulator engine)
    {
        engine.AdvanceTo(engine.State.Minute);
        if (engine.State.Citizens[0].CommutePreparation.PlaceId == "")
            engine.AdvanceTo(engine.State.Citizens[0].NextEventMinute);
        Assert.NotEqual("", engine.State.Citizens[0].CommutePreparation.PlaceId);
    }

    [Theory]
    [InlineData(Persona.Commuter)] [InlineData(Persona.Student)] [InlineData(Persona.Parent)] [InlineData(Persona.NightOwl)]
    public void LeavesBeforeShiftUsingActualRouteDuration(Persona persona)
    {
        var state = City(persona); var c = state.Citizens[0]; var planner = new NativePacePlanner();
        var window = DailyRoutine.WorkOnDay(c, state.Seed, state.Minute)!.Value;
        var engine = new CitySimulator(state, planner); Prepare(engine);
        Assert.Equal(window.StartsAt - 120, c.NextEventMinute); Assert.Equal(1, planner.Calls);
        engine.AdvanceTo(engine.State.Minute + 1); Assert.Equal(1, planner.Calls);
        engine.AdvanceTo(c.NextEventMinute);
        Assert.Equal(CitizenPhase.Travelling, c.Phase); Assert.Equal(window.StartsAt - 120, c.DepartureMinute);
        Assert.Equal(window.StartsAt, c.NextEventMinute); Assert.Equal(window.EndsAt, c.ActivityUntilMinute);
        Assert.Empty(c.RecentVisits); Assert.Equal(2, planner.Calls);
        engine.AdvanceTo(window.StartsAt);
        Assert.Single(c.RecentVisits); Assert.Equal(window.StartsAt, c.RecentVisits[0].AtMinute);
    }

    [Fact]
    public void ClosedWorkplaceReceivesCommuteBeforeOpeningAndWaitsAtItsEntrance()
    {
        var state = City(); var c = state.Citizens[0]; var shift = DailyRoutine.WorkOnDay(c, state.Seed, state.Minute)!.Value;
        state.Places[1].OpenMinute = (int)(shift.StartsAt - 1440 + 30); state.Places[1].CloseMinute = 1300;
        var engine = new CitySimulator(state, new NativePacePlanner()); engine.AdvanceTo(shift.StartsAt);
        Assert.Equal("work", c.PlaceId); Assert.Equal(CitizenPhase.WaitingForOpening, c.Phase);
        Assert.Equal(shift.StartsAt + 30, c.NextEventMinute); Assert.Empty(c.RecentVisits);
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new NativePacePlanner()); resumed.AdvanceTo(shift.StartsAt + 30);
        Assert.Single(resumed.State.Citizens[0].RecentVisits);
    }

    [Fact]
    public void EarlyPhysicalArrivalWaitsForShiftEvenWhenVenueIsAlreadyOpen()
    {
        var state = City(); var c = state.Citizens[0]; var shift = DailyRoutine.WorkOnDay(c, state.Seed, state.Minute)!.Value;
        var engine = new CitySimulator(state, new NativePacePlanner()); engine.AdvanceTo(shift.StartsAt - 120);
        Assert.True(engine.TryAcquirePhysicalJourney(c.Identity.Id, out var lease));
        engine.AdvanceTo(state.Minute + 1); Assert.True(engine.ObservePhysicalJourney(lease!, 120)); engine.AdvanceTo(state.Minute);
        Assert.Equal("work", c.PlaceId); Assert.Equal(CitizenPhase.WaitingForOpening, c.Phase);
        Assert.Equal(shift.StartsAt, c.NextEventMinute); Assert.Empty(c.RecentVisits);
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new NativePacePlanner()); resumed.AdvanceTo(shift.StartsAt);
        Assert.Single(resumed.State.Citizens[0].RecentVisits);
        Assert.Equal(shift.StartsAt, resumed.State.Citizens[0].RecentVisits[0].AtMinute);
    }

    [Fact]
    public void ReloadAtScheduledDepartureUsesSavedDurationAndReplansFreshRoute()
    {
        var state = City(); var engine = new CitySimulator(state, new NativePacePlanner()); Prepare(engine);
        double early = state.Citizens[0].NextEventMinute;
        // A bounded snapshot may have the next citizen event due exactly at its current clock.
        state.Minute = early;
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new NativePacePlanner());
        engine.AdvanceTo(early); resumed.AdvanceTo(early);
        Assert.Equal(CitizenPhase.Travelling, resumed.State.Citizens[0].Phase);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
        engine.AdvanceTo(2500); resumed.AdvanceTo(2500);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
    }

    [Fact]
    public void CommittedTripDoesNotReplanAfterPlannerDurationChanges()
    {
        var state = City(); var planner = new NativePacePlanner(); var engine = new CitySimulator(state, planner);
        Prepare(engine); engine.AdvanceTo(state.Citizens[0].NextEventMinute);
        var c = state.Citizens[0]; var route = c.Journey; double arrival = c.NextEventMinute; int calls = planner.Calls;
        planner.Minutes = 500; engine.AdvanceTo(arrival - 1);
        Assert.Same(route, c.Journey); Assert.Equal(arrival, c.NextEventMinute); Assert.Equal(calls, planner.Calls);
    }

    [Fact]
    public void JoiningAfterIdealDepartureLeavesNowWithoutBackdating()
    {
        var initial = City(); var shift = DailyRoutine.WorkOnDay(initial.Citizens[0], initial.Seed, initial.Minute)!.Value;
        var state = City(now: shift.StartsAt - 30); var engine = new CitySimulator(state, new NativePacePlanner());
        engine.AdvanceTo(state.Minute); var c = state.Citizens[0];
        Assert.Equal(CitizenPhase.Travelling, c.Phase); Assert.Equal(shift.StartsAt - 30, c.DepartureMinute);
        Assert.Equal(shift.StartsAt + 90, c.NextEventMinute);
    }

    [Fact]
    public void HomeLifecycleEventStillRunsBeforeEarlyDeparture()
    {
        var state = City(); var c = state.Citizens[0];
        var planner = new NativePacePlanner();
        var engine = new CitySimulator(state, planner); Prepare(engine);
        double departure = c.NextEventMinute;
        // Rebuild from a saved resting event to insert a lifecycle event after preparation.
        c.NextJobChangeMinute = engine.State.Minute + 1;
        c.NextEventMinute = c.NextJobChangeMinute;
        engine = new CitySimulator(state, planner);
        engine.AdvanceTo(c.NextJobChangeMinute);
        Assert.True(c.NextJobChangeMinute > state.Minute); Assert.Equal(CitizenPhase.AtPlace, c.Phase);
        Assert.Equal(departure, c.NextEventMinute);
        Assert.Equal(1, planner.Calls);
    }

    [Theory]
    [InlineData(30)] [InlineData(300)]
    public void DepartureSelectsFreshShorterOrLongerRouteWithoutBackdating(double newMinutes)
    {
        var state = City(); var c = state.Citizens[0]; var planner = new NativePacePlanner();
        var engine = new CitySimulator(state, planner); Prepare(engine);
        double departure = c.NextEventMinute;
        planner.Minutes = newMinutes; engine.AdvanceTo(departure);
        Assert.Equal(2, planner.Calls); Assert.Equal(departure, c.DepartureMinute);
        Assert.Equal(departure + newMinutes, c.NextEventMinute);
        Assert.Equal(newMinutes, c.Journey!.Minutes);
        Assert.Equal(CitizenPhase.Travelling, c.Phase);
        if (newMinutes < 120)
        {
            engine.AdvanceTo(c.NextEventMinute);
            Assert.Equal(CitizenPhase.WaitingForOpening, c.Phase);
            Assert.Equal(c.CommutePreparation.WorkStartsAt, c.NextEventMinute);
            Assert.Empty(c.RecentVisits);
        }
    }

    [Fact]
    public void FailedFreshDepartureRetriesOriginalWorkWindowBeforeShiftAndAcrossReload()
    {
        var state = City(); var c = state.Citizens[0]; var planner = new NativePacePlanner();
        var engine = new CitySimulator(state, planner); Prepare(engine);
        double departure = c.NextEventMinute, deadline = c.CommutePreparation.WorkEndsAt;
        planner.Reachable = false; engine.AdvanceTo(departure);
        Assert.Equal(2, planner.Calls); Assert.Equal(CitizenPhase.WaitingForRoute, c.Phase);
        Assert.Equal("work", c.DestinationId); Assert.Equal(deadline, c.ActivityUntilMinute);
        Assert.Equal(departure + 30, c.NextEventMinute); Assert.Equal(0, c.DepartureMinute);
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        planner.Reachable = true; planner.Minutes = 160;
        var resumed = new CitySimulator(read.State!, planner); resumed.AdvanceTo(departure + 30);
        c = resumed.State.Citizens[0];
        Assert.Equal(3, planner.Calls); Assert.Equal(CitizenPhase.Travelling, c.Phase);
        Assert.Equal("work", c.DestinationId); Assert.Equal(deadline, c.ActivityUntilMinute);
        Assert.Equal(departure + 30, c.DepartureMinute); Assert.Equal(departure + 190, c.NextEventMinute);
        Assert.Equal(0, c.MissedActivities);
    }

    [Fact]
    public void FailedPreparationIsNotRepeatedByHomeLifecycleEvents()
    {
        var state = City(); var c = state.Citizens[0]; var planner = new NativePacePlanner { Reachable = false };
        var engine = new CitySimulator(state, planner); Prepare(engine);
        Assert.Equal(0, c.CommutePreparation.TravelMinutes); Assert.Equal(1, planner.Calls);
        double shift = c.CommutePreparation.WorkStartsAt;
        c.NextJobChangeMinute = engine.State.Minute + 1; c.NextEventMinute = c.NextJobChangeMinute;
        engine = new CitySimulator(state, planner); engine.AdvanceTo(c.NextEventMinute);
        Assert.Equal(1, planner.Calls); Assert.Equal(shift, c.NextEventMinute);
        engine.AdvanceTo(shift);
        Assert.Equal(2, planner.Calls); Assert.Equal(CitizenPhase.WaitingForRoute, c.Phase);
    }

    [Fact]
    public void KnownLongCommuteDepartsOnPreviousEvening()
    {
        var state = City(now: 2 * 1440 + 1000); var c = state.Citizens[0];
        var prior = DailyRoutine.WorkOnDay(c, state.Seed, 2 * 1440)!.Value;
        var next = DailyRoutine.NextWork(c, state.Seed, state.Minute)!.Value;
        c.CommutePreparation = new CommutePreparation
        {
            PlaceId = "work", WorkStartsAt = prior.StartsAt, WorkEndsAt = prior.EndsAt,
            PreparedAt = prior.StartsAt - 900, TravelMinutes = 900
        };
        var planner = new NativePacePlanner { Minutes = 900 }; var engine = new CitySimulator(state, planner);
        Prepare(engine); double departure = c.NextEventMinute;
        Assert.Equal(next.StartsAt - 900, departure); Assert.True(departure < 3 * 1440);
        engine.AdvanceTo(departure);
        Assert.Equal(departure, c.DepartureMinute); Assert.Equal(next.StartsAt, c.NextEventMinute);
        Assert.Equal(1, planner.Calls);
        engine.AdvanceTo(next.StartsAt);
        Assert.Contains(c.RecentVisits, visit => visit.Activity == Activity.Work && visit.AtMinute == next.StartsAt);
    }
    [Fact]
    public void ShorterFreshCommuteToEntirelyClosedShiftIsMissedOnlyOnce()
    {
        var state = City(); var c = state.Citizens[0]; var planner = new NativePacePlanner { Minutes = 60 };
        state.Places[1].OpenMinute = 0; state.Places[1].CloseMinute = 0;
        var engine = new CitySimulator(state, planner); Prepare(engine);
        double departure = c.NextEventMinute, shiftEnd = c.CommutePreparation.WorkEndsAt;
        planner.Minutes = 15;
        engine.AdvanceTo(departure + 30);
        Assert.Equal("home", c.PlaceId); Assert.Equal(CitizenPhase.AtPlace, c.Phase);
        Assert.Equal(1, c.MissedActivities); Assert.Equal(2, c.CompletedJourneys);
        Assert.Equal(shiftEnd, c.ActivityUntilMinute);
        Assert.Equal(3, planner.Calls); // Estimate, fresh outbound route, return home.
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        engine = new CitySimulator(read.State!, planner); engine.AdvanceTo(shiftEnd - 1);
        c = engine.State.Citizens[0];
        Assert.Equal("home", c.PlaceId); Assert.Equal(CitizenPhase.AtPlace, c.Phase);
        Assert.Equal(1, c.MissedActivities); Assert.Equal(2, c.CompletedJourneys);
        Assert.Equal(3, planner.Calls);
    }

    [Fact]
    public void FirstUnknownOverlongCommuteLeavesAtPreparationWithoutBackdating()
    {
        var state = City(now: 900); var c = state.Citizens[0];
        var window = DailyRoutine.NextWork(c, state.Seed, state.Minute)!.Value;
        var planner = new NativePacePlanner { Minutes = 900 }; var engine = new CitySimulator(state, planner);
        double preparation = DailyRoutine.CommutePreparationAt(c, state.Seed, window, 0);
        engine.AdvanceTo(preparation);
        Assert.Equal(CitizenPhase.Travelling, c.Phase);
        Assert.Equal(preparation, c.DepartureMinute); Assert.Equal(preparation + 900, c.NextEventMinute);
        Assert.True(c.NextEventMinute > window.StartsAt); Assert.Equal(1, planner.Calls);
    }

    [Fact]
    public void RepeatedShiftsReuseMeasuredDurationAndQueryOnlyAtActualDeparture()
    {
        var state = City(); var person = state.Citizens[0];
        person.NextJobChangeMinute = person.NextDepartureMinute = ResidentLifecycle.Never;
        var planner = new NativePacePlanner(); var engine = new CitySimulator(state, planner);
        var shifts = new[] { 1, 2, 3, 4, 8 }.Select(day => DailyRoutine.WorkOnDay(person, state.Seed, day * 1440)!.Value).ToArray();
        foreach (var shift in shifts)
        {
            engine.AdvanceTo(shift.StartsAt - 121);
            int callsBeforeDeparture = planner.Queries.Count(query => query.From == "home");
            engine.AdvanceTo(shift.StartsAt - 120);
            Assert.Equal(callsBeforeDeparture + 1, planner.Queries.Count(query => query.From == "home"));
            Assert.Equal(shift.StartsAt - 120, person.DepartureMinute);
            engine.AdvanceTo(shift.EndsAt + 120);
        }
        var outbound = planner.Queries.Where(query => query.From == "home").ToArray();
        Assert.Equal(6, outbound.Length); // One first estimate plus five fresh departures.
        Assert.Equal(shifts.Select(shift => shift.StartsAt - 120), outbound.Skip(1).Select(query => query.At));
        Assert.Equal(5, planner.Queries.Count(query => query.To == "home"));
        Assert.Equal(10, person.CompletedJourneys); Assert.Equal(0, person.MissedActivities);
    }

    [Fact]
    public void KnownDurationSurvivesTemporaryNightFailureAndKeepsEarlyDepartureRetryAcrossReload()
    {
        var state = City(now: 2 * 1440); var person = state.Citizens[0];
        var previous = DailyRoutine.WorkOnDay(person, state.Seed, 1440)!.Value;
        var shift = DailyRoutine.WorkOnDay(person, state.Seed, state.Minute)!.Value;
        person.CommutePreparation = new CommutePreparation
        { PlaceId = "work", WorkStartsAt = previous.StartsAt, WorkEndsAt = previous.EndsAt, PreparedAt = previous.StartsAt - 120, TravelMinutes = 120 };
        var planner = new NativePacePlanner { Reachable = false }; var engine = new CitySimulator(state, planner);
        engine.AdvanceTo(shift.StartsAt - 121);
        Assert.Equal(0, planner.Calls); Assert.Equal(120, person.CommutePreparation.TravelMinutes);
        Assert.Equal(shift.StartsAt - 120, person.NextEventMinute);
        engine.AdvanceTo(person.NextEventMinute);
        Assert.Equal(1, planner.Calls); Assert.Equal(CitizenPhase.WaitingForRoute, person.Phase);
        Assert.Equal(shift.StartsAt - 90, person.NextEventMinute); Assert.Equal(shift.EndsAt, person.ActivityUntilMinute);
        Assert.Equal(120, person.CommutePreparation.TravelMinutes);
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        planner.Reachable = true; engine = new CitySimulator(read.State!, planner); engine.AdvanceTo(shift.StartsAt - 90);
        person = engine.State.Citizens[0];
        Assert.Equal(2, planner.Calls); Assert.Equal(CitizenPhase.Travelling, person.Phase);
        Assert.Equal(shift.StartsAt - 90, person.DepartureMinute); Assert.Equal(shift.StartsAt + 30, person.NextEventMinute);
        Assert.Equal(0, person.MissedActivities);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ChangedWorkplaceOrPreviousFailedEstimateStillMeasuresAtPreparation(bool changedPlace)
    {
        var state = City(now: 2 * 1440); var person = state.Citizens[0];
        var previous = DailyRoutine.WorkOnDay(person, state.Seed, 1440)!.Value;
        var shift = DailyRoutine.WorkOnDay(person, state.Seed, state.Minute)!.Value;
        state.Places.Add(new Place { Id = "old-work", Name = "Old work", Kind = PlaceKind.Work });
        person.CommutePreparation = new CommutePreparation
        { PlaceId = changedPlace ? "old-work" : "work", WorkStartsAt = previous.StartsAt, WorkEndsAt = previous.EndsAt,
            PreparedAt = previous.StartsAt - 120, TravelMinutes = changedPlace ? 120 : 0 };
        var planner = new NativePacePlanner(); var engine = new CitySimulator(state, planner);
        engine.AdvanceTo(Math.Max(state.Minute, DailyRoutine.CommutePreparationAt(person, state.Seed, shift, 0)));
        Assert.Equal(1, planner.Calls); Assert.Equal("work", person.CommutePreparation.PlaceId);
        Assert.Equal(120, person.CommutePreparation.TravelMinutes); Assert.Equal(shift.StartsAt - 120, person.NextEventMinute);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HomeArrivalPastDepartureSchedulesNextStepWithoutDiscardingAnyPlannedRoute(bool knownDuration)
    {
        var state = City(now: 2 * 1440); var person = state.Citizens[0];
        var shift = DailyRoutine.WorkOnDay(person, state.Seed, state.Minute)!.Value;
        var previous = DailyRoutine.WorkOnDay(person, state.Seed, 1440)!.Value;
        double arrival = shift.StartsAt - 30; state.Minute = arrival;
        person.PlaceId = "work"; person.Activity = Activity.Work; person.DestinationId = "home";
        person.DestinationActivity = Activity.Home; person.Phase = CitizenPhase.Travelling;
        person.DepartureMinute = arrival - 10; person.NextEventMinute = arrival; person.ActivityUntilMinute = shift.StartsAt;
        person.Journey = new Route { Legs = new() { new RouteLeg
        { From = state.Places[1].Position, To = state.Places[0].Position, Minutes = 10, Mode = TravelMode.Walk } } };
        if (knownDuration) person.CommutePreparation = new CommutePreparation
        { PlaceId = "work", WorkStartsAt = previous.StartsAt, WorkEndsAt = previous.EndsAt, PreparedAt = previous.StartsAt - 120, TravelMinutes = 120 };
        var planner = new NativePacePlanner(); var engine = new CitySimulator(state, planner);
        Assert.Equal(1, engine.AdvanceTo(arrival, 1).Events);
        Assert.Equal(0, planner.Calls); Assert.Equal("home", person.PlaceId); Assert.Equal(CitizenPhase.AtPlace, person.Phase);
        Assert.InRange(person.NextEventMinute, arrival + 0.0000001, arrival + 0.00001);
        double departure = person.NextEventMinute; engine.AdvanceTo(departure, 1);
        Assert.Equal(1, planner.Calls); Assert.Equal(CitizenPhase.Travelling, person.Phase);
        Assert.Equal(departure, person.DepartureMinute); Assert.Equal(departure + 120, person.NextEventMinute);
        Assert.Equal(1, person.CompletedJourneys);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SchemaFiveHomeFallbackRetainsMissedShiftDeadlineEvenWhenTimingChangeWakesResident(bool timingChange)
    {
        var state = City(); var person = state.Citizens[0];
        var shift = DailyRoutine.NextWork(person, state.Seed, state.Minute)!.Value;
        person.MissedActivities = 1; person.ActivityUntilMinute = person.NextEventMinute = shift.EndsAt;
        var wire = JObject.Parse(SaveCodec.Write(state)); wire["Schema"] = 5;
        wire.Remove("Traffic"); wire.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(wire);
        ((JObject)wire["Citizens"]![0]!).Remove("CommutePreparation");
        var read = SaveCodec.Read(wire.ToString()); Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.True(read.Migrated);
        state = read.State!; person = state.Citizens[0];
        Assert.Equal(shift.EndsAt, person.ActivityUntilMinute); Assert.Equal(shift.EndsAt, person.NextEventMinute);
        if (timingChange) Assert.True(TravelTiming.Apply(state, 2, 2));
        var planner = new NativePacePlanner(); var engine = new CitySimulator(state, planner);
        engine.AdvanceTo(shift.EndsAt - 1);
        Assert.Equal(0, planner.Calls); Assert.Equal(shift.EndsAt, person.ActivityUntilMinute);
        Assert.Equal(shift.EndsAt, person.NextEventMinute); Assert.Equal(1, person.MissedActivities);
        Assert.Equal("home", person.PlaceId); Assert.Equal(CitizenPhase.AtPlace, person.Phase);
    }

}
