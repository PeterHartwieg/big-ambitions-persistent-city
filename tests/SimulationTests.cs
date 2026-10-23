using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SimulationTests
{
    private sealed class WalkPlanner : IRoutePlanner
    {
        public bool Reachable = true;
        public double Minutes = 12;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => !Reachable ? null : new Route
        {
            Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = Minutes, Mode = TravelMode.Walk } }
        };
    }

    private static CityState City(int count = 1, double minute = 1440)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "rlo:flare:c" + i, Name = "Citizen " + i, Age = 30, District = "a", Persona = Persona.Commuter, AppearanceSeed = i + 1 });
        return Population.Create(identities, new[]
        {
            new Place { Id = "home", Name = "Home", District = "a", Kind = PlaceKind.Home, Capacity = count, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", District = "a", Kind = PlaceKind.Work, Capacity = count, Position = new Position(300, 0, 0) },
            new Place { Id = "shop", Name = "Shop", District = "a", Kind = PlaceKind.Shop, Position = new Position(200, 0, 80) },
            new Place { Id = "leisure", Name = "Leisure", District = "a", Kind = PlaceKind.Leisure, Position = new Position(10, 0, 90) }
        }, "rlo-test-city", "0.40.4", 7, minute);
    }

    [Fact]
    public void FineAndCoarseTimeProduceIdenticalLives()
    {
        var a = new CitySimulator(City(40), new WalkPlanner());
        var b = new CitySimulator(City(40), new WalkPlanner());
        double until = 1440 * 8;
        for (double t = 1440; t <= until; t += 1) a.AdvanceTo(t);
        while (!b.AdvanceTo(until, 73).CaughtUp) { }
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        Assert.All(a.State.Citizens, c => Assert.True(c.CompletedJourneys > 10));
        Assert.All(a.State.Citizens, c => Assert.True(c.RecentVisits.Count <= 32));
    }

    [Fact]
    public void EventBudgetBoundsWorkAndLeavesResumableSnapshot()
    {
        var engine = new CitySimulator(City(400), new WalkPlanner());
        var result = engine.AdvanceTo(1440 * 2, 3);
        Assert.False(result.CaughtUp);
        Assert.Equal(3, result.Events);
        Assert.True(engine.State.Minute < 2880);
        Assert.All(engine.State.Citizens, c => Assert.True(c.NextEventMinute >= engine.State.Minute));
        var loaded = SaveCodec.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
    }

    [Fact]
    public void UnreachableCitizensNeverTeleportAndResumeWhenRouteReturns()
    {
        var planner = new WalkPlanner { Reachable = false };
        var engine = new CitySimulator(City(), planner);
        var c = engine.State.Citizens[0];
        engine.AdvanceTo(1440 + 600);
        Assert.Equal(CitizenPhase.WaitingForRoute, c.Phase);
        Assert.Equal("home", c.PlaceId);
        Assert.Equal(0, c.CompletedJourneys);
        Assert.Equal(0, engine.PositionOf(c).X);
        planner.Reachable = true;
        engine.AdvanceTo(1440 + 660);
        Assert.Equal("work", c.PlaceId);
        Assert.Equal(Activity.Work, c.Activity);
        Assert.Equal(1, c.CompletedJourneys);
    }

    [Fact]
    public void LongJourneyCompletesPhysicallyBeforeNewActivity()
    {
        var engine = new CitySimulator(City(), new WalkPlanner { Minutes = 600 });
        engine.AdvanceTo(1440 + 1000);
        var c = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.Travelling, c.Phase);
        Assert.Equal("work", c.DestinationId);
        Assert.Equal(0, c.CompletedJourneys);
        Assert.InRange(engine.PositionOf(c).X, 0.01, 299.99);
        engine.AdvanceTo(c.NextEventMinute);
        Assert.Equal(1, c.CompletedJourneys);
        Assert.Equal("work", c.PlaceId);
        Assert.True(c.MissedActivities >= 1);
    }

    [Fact]
    public void ClockRewindIsRejectedWithoutChangingState()
    {
        var engine = new CitySimulator(City(), new WalkPlanner());
        engine.AdvanceTo(2000);
        var before = SaveCodec.Write(engine.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AdvanceTo(1999));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AdvanceTo(double.NaN));
        Assert.Equal(before, SaveCodec.Write(engine.State));
    }

    [Fact]
    public void PopulationAssignmentsIgnoreInputEnumerationOrder()
    {
        var a = City(30);
        var b = Population.Create(a.Citizens.Select(c => c.Identity).Reverse(), a.Places.AsEnumerable().Reverse(), a.PopulationKey, a.RloVersion, a.Seed, a.Minute);
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(b));
    }

    [Fact]
    public void PopulationRefusesTooFewHomesAndPreservesOptionalVenueAbsence()
    {
        var source = City(2);
        source.Places[0].Capacity = 0;
        Assert.Throws<InvalidOperationException>(() => Population.Create(source.Citizens.Select(c => c.Identity), source.Places, "city", "0.40.4", 1, 1440));
        var homes = new[] { new Place { Id = "h", Name = "Home", Kind = PlaceKind.Home, Capacity = 2, Position = new Position(0, 0, 0) } };
        var city = Population.Create(source.Citizens.Select(c => c.Identity), homes, "city", "0.40.4", 1, 1440);
        var engine = new CitySimulator(city, new WalkPlanner());
        engine.AdvanceTo(10080);
        Assert.All(city.Citizens, c => { Assert.Equal("h", c.PlaceId); Assert.Equal("", c.DayPlaceId); Assert.Equal(0, c.CompletedJourneys); });
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city)).Status);
    }

    [Fact]
    public void RetireesDoNotReceiveWorkAndStudentsRequireStudyVenue()
    {
        var source = City(2);
        source.Citizens[0].Identity.Persona = Persona.Retiree;
        source.Citizens[1].Identity.Persona = Persona.Student;
        var city = Population.Create(source.Citizens.Select(c => c.Identity), source.Places, "city", "0.40.4", 1, 1440);
        Assert.All(city.Citizens, c => Assert.Equal("", c.DayPlaceId));
    }

    [Fact]
    public void ClosedShopsDoNotBecomeVisits()
    {
        var city = City();
        city.Citizens[0].Identity.Persona = Persona.Foodie;
        city.Places.Single(p => p.Id == "shop").OpenMinute = 0;
        city.Places.Single(p => p.Id == "shop").CloseMinute = 1;
        var engine = new CitySimulator(city, new WalkPlanner());
        engine.AdvanceTo(1440 * 2);
        Assert.DoesNotContain(city.Citizens[0].RecentVisits, v => v.Activity == Activity.Shop);
        Assert.True(city.Citizens[0].MissedActivities > 0);
    }
}
