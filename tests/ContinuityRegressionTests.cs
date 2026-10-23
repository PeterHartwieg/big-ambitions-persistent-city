using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class ContinuityRegressionTests
{
    private const int Seed = 7;
    private const string CitizenId = "person";
    private static int Shift => (int)(StableRandom.Hash(CitizenId + ":hours", Seed) % 31) - 15;

    private sealed class Routes : IRoutePlanner
    {
        public bool Reachable = true;
        public bool HomeReachable = true;
        public double Minutes = 12;
        public double? HomeMinutes;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
            => !Reachable || (!HomeReachable && to.Kind == PlaceKind.Home) ? null : new Route
            {
                Legs = new List<RouteLeg> { new() { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = to.Kind == PlaceKind.Home ? HomeMinutes ?? Minutes : Minutes } }
            };
    }

    private static CityState City(Persona persona, double minute, bool work = false, bool shop = false, bool leisure = false)
    {
        var places = new List<Place>
        {
            new() { Id = "home", Name = "Home", District = "district", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0, 0, 0) }
        };
        if (work) places.Add(new Place { Id = "work", Name = "Work", District = "district", Kind = PlaceKind.Work, Capacity = 1, Position = new Position(100, 0, 0) });
        if (shop) places.Add(new Place { Id = "shop", Name = "Shop", District = "district", Kind = PlaceKind.Shop, Capacity = 1, Position = new Position(200, 0, 0) });
        if (leisure) places.Add(new Place { Id = "leisure", Name = "Leisure", District = "district", Kind = PlaceKind.Leisure, Capacity = 1, Position = new Position(300, 0, 0) });
        return Population.Create(new[] { new CitizenIdentity { Id = CitizenId, Name = "Person", District = "district", Age = 30, Persona = persona } }, places, "test-population", "0.40.4", Seed, minute);
    }

    private static CityState ClosingShop(double start)
    {
        var city = City(Persona.Foodie, start, shop: true);
        city.Places.Single(p => p.Id == "shop").CloseMinute = 1050 + Shift + 5;
        return city;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlyParentWaitsForOpeningAndWorksAllFourDaysAcrossReload(bool weekly)
    {
        Assert.True(465 + Shift < 480);
        var city = City(Persona.Parent, 1440, work: true);
        var work = city.Places.Single(p => p.Id == "work");
        work.OpenMinute = 480; work.CloseMinute = 960;
        if (weekly)
            for (int day = 0; day < 4; day++) work.WeeklyHours.Add(new OpeningWindow { Day = day, FromMinute = 480, ToMinute = 960 });
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(1440 + 479);
        Assert.Equal("home", city.Citizens[0].PlaceId);
        Assert.Equal(CitizenPhase.WaitingForOpening, city.Citizens[0].Phase);
        Assert.Equal("work", city.Citizens[0].DestinationId);
        Assert.Equal(1440 + 480, city.Citizens[0].NextEventMinute);
        Assert.Equal(0, city.Citizens[0].MissedActivities);
        var read = SaveCodec.Read(SaveCodec.Write(city));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new Routes());
        resumed.AdvanceTo(5 * 1440);
        engine.AdvanceTo(5 * 1440);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
        var person = resumed.State.Citizens[0];
        Assert.Equal(4, person.RecentVisits.Count(v => v.Activity == Activity.Work));
        Assert.Equal(8, person.CompletedJourneys);
        Assert.Equal(0, person.MissedActivities);
    }

    [Theory]
    [InlineData(true, 12)]
    [InlineData(false, 12)]
    [InlineData(true, 60)]
    public void ClosedArrivalWaitsAtVenueAndRecordsAdmissionDespiteImpossibleOrLongHomeRoute(bool homeReachable, double homeMinutes)
    {
        double start = 1440 + 1050 + Shift;
        var city = ClosingShop(start);
        var shop = city.Places.Single(p => p.Id == "shop");
        shop.WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1050 + Shift - 10, ToMinute = 1050 + Shift + 5 });
        shop.WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1050 + Shift + 30, ToMinute = 1050 + Shift + 45 });
        var routes = new Routes { HomeReachable = homeReachable, HomeMinutes = homeMinutes };
        var engine = new CitySimulator(city, routes);
        engine.AdvanceTo(start + 18);
        var person = city.Citizens[0];
        Assert.Equal("shop", person.PlaceId); Assert.Equal("shop", person.DestinationId);
        Assert.Equal(CitizenPhase.WaitingForOpening, person.Phase);
        Assert.Equal(start + 45, person.ActivityUntilMinute);
        Assert.Equal(start + 30, person.NextEventMinute);
        Assert.Equal(0, person.MissedActivities);
        Assert.DoesNotContain(person.RecentVisits, visit => visit.Activity == Activity.Shop);
        var read = SaveCodec.Read(SaveCodec.Write(city));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, routes);
        resumed.AdvanceTo(start + 35);
        Assert.Equal(CitizenPhase.AtPlace, resumed.State.Citizens[0].Phase);
        Assert.Equal(start + 30, Assert.Single(resumed.State.Citizens[0].RecentVisits).AtMinute);
        while (!resumed.AdvanceTo(start + 65, 1).CaughtUp)
            resumed = new CitySimulator(SaveCodec.Read(SaveCodec.Write(resumed.State)).State!, routes);
        engine.AdvanceTo(start + 65);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
        person = resumed.State.Citizens[0];
        Assert.Equal(0, person.MissedActivities);
        Assert.Equal(homeReachable && homeMinutes <= 20 ? 2 : 1, person.CompletedJourneys);
        Assert.Single(person.RecentVisits, visit => visit.Activity == Activity.Shop);
    }

    [Fact]
    public void OpeningAtTheActivityDeadlineDoesNotCreateAnAttainableVisit()
    {
        var city = City(Persona.Parent, 1440, work: true);
        var work = city.Places.Single(p => p.Id == "work");
        work.OpenMinute = 465 + Shift + 480; work.CloseMinute = 1440;
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(2 * 1440);
        Assert.Equal(1, city.Citizens[0].MissedActivities);
        Assert.Equal(0, city.Citizens[0].CompletedJourneys);
    }

    [Fact]
    public void FailedRouteAfterWaitingForOpeningCountsOneMissAtTheDeadline()
    {
        var city = City(Persona.Parent, 1440, work: true);
        var work = city.Places.Single(p => p.Id == "work"); work.OpenMinute = 480; work.CloseMinute = 960;
        var engine = new CitySimulator(city, new Routes { Reachable = false });
        engine.AdvanceTo(1440 + 600);
        Assert.Equal(0, city.Citizens[0].MissedActivities);
        var resumed = new CitySimulator(SaveCodec.Read(SaveCodec.Write(city)).State!, new Routes { Reachable = false });
        resumed.AdvanceTo(2 * 1440);
        Assert.Equal(1, resumed.State.Citizens[0].MissedActivities);
        Assert.Equal(0, resumed.State.Citizens[0].CompletedJourneys);
    }

    [Fact]
    public void NextOpeningRespectsWeekBoundaryOvernightAndExclusiveDeadline()
    {
        var place = new Place { WeeklyHours = new List<OpeningWindow>
        { new() { Day = 6, FromMinute = 1380, ToMinute = 120 }, new() { Day = 0, FromMinute = 600, ToMinute = 720 } } };
        Assert.Equal(8 * 1440 + 60, DailyRoutine.NextOpen(place, 8 * 1440 + 60, 8 * 1440 + 80));
        Assert.Equal(8 * 1440 + 600, DailyRoutine.NextOpen(place, 8 * 1440 + 120, 8 * 1440 + 601));
        Assert.Null(DailyRoutine.NextOpen(place, 8 * 1440 + 120, 8 * 1440 + 600));
        Assert.Null(DailyRoutine.NextOpen(new Place { OpenMinute = 0, CloseMinute = 0 }, 1440, 2880));
    }

    [Fact]
    public void ClosureDuringTravelRecordsPhysicalArrivalButNoShopVisitAndOneMiss()
    {
        double start = 1440 + 1050 + Shift;
        var city = City(Persona.Foodie, start, shop: true);
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(start); // Commit while the published schedule still permits the visit.
        city.Places.Single(p => p.Id == "shop").CloseMinute = 1050 + Shift + 5;
        engine.AdvanceTo(start + 12);
        var person = Assert.Single(engine.State.Citizens);
        Assert.Equal("shop", person.PlaceId);
        Assert.Equal(Activity.Shop, person.Activity);
        Assert.Equal("home", person.DestinationId);
        Assert.Equal(CitizenPhase.Travelling, person.Phase);
        Assert.Equal(1, person.CompletedJourneys);
        Assert.Equal(1, person.MissedActivities);
        Assert.DoesNotContain(person.RecentVisits, visit => visit.Activity == Activity.Shop);
        Assert.Equal(200, engine.PositionOf(person).X);

        var saved = SaveCodec.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, saved.Status);
        var resumed = new CitySimulator(saved.State!, new Routes());
        resumed.AdvanceTo(start + 30);
        person = Assert.Single(resumed.State.Citizens);
        Assert.Equal("home", person.PlaceId);
        Assert.Equal(CitizenPhase.AtPlace, person.Phase);
        Assert.Equal(2, person.CompletedJourneys);
        Assert.Equal(1, person.MissedActivities);
        Assert.DoesNotContain(person.RecentVisits, visit => visit.Activity == Activity.Shop);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(resumed.State)).Status);
    }

    [Fact]
    public void UnreachableHomeFallbackDoesNotRecountClosedActivityAcrossReload()
    {
        double start = 1440 + 1050 + Shift;
        var city = City(Persona.Foodie, start, shop: true);
        var engine = new CitySimulator(city, new Routes { HomeReachable = false });
        engine.AdvanceTo(start); // Commit while the published schedule still permits the visit.
        city.Places.Single(p => p.Id == "shop").CloseMinute = 1050 + Shift + 5;
        engine.AdvanceTo(start + 12);
        Assert.Equal(CitizenPhase.WaitingForRoute, engine.State.Citizens[0].Phase);
        var saved = SaveCodec.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, saved.Status);
        var resumed = new CitySimulator(saved.State!, new Routes { HomeReachable = false });
        resumed.AdvanceTo(start + 80);
        var person = resumed.State.Citizens[0];
        Assert.Equal("shop", person.PlaceId);
        Assert.Equal("home", person.DestinationId);
        Assert.Equal(CitizenPhase.WaitingForRoute, person.Phase);
        Assert.Equal(1, person.MissedActivities);
        Assert.Empty(person.RecentVisits);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(resumed.State)).Status);
    }

    [Fact]
    public void WorkReachedAfterItsWindowIsNotAnAdmittedWorkVisit()
    {
        double start = 1440 + 510 + Shift;
        var engine = new CitySimulator(City(Persona.Commuter, start, work: true), new Routes { Minutes = 600 });
        engine.AdvanceTo(start + 600);
        var person = engine.State.Citizens[0];
        Assert.Equal("work", person.PlaceId);
        Assert.Equal(Activity.Work, person.Activity);
        Assert.Equal(1, person.CompletedJourneys);
        Assert.Equal(1, person.MissedActivities);
        Assert.DoesNotContain(person.RecentVisits, visit => visit.Activity == Activity.Work);
        Assert.Equal("home", person.DestinationId);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(engine.State)).Status);
    }

    [Fact]
    public void UnreachableActivityCountsOncePerExpiredWindowEvenForSameNextDayDestination()
    {
        double start = 1440 + 510 + Shift;
        var engine = new CitySimulator(City(Persona.Commuter, start, work: true), new Routes { Reachable = false });
        engine.AdvanceTo(start + 479);
        Assert.Equal(0, engine.State.Citizens[0].MissedActivities);
        engine.AdvanceTo(start + 480);
        Assert.Equal(1, engine.State.Citizens[0].MissedActivities);
        var saved = SaveCodec.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, saved.Status);
        var resumed = new CitySimulator(saved.State!, new Routes { Reachable = false });
        resumed.AdvanceTo(start + 1440 + 479);
        Assert.Equal(1, resumed.State.Citizens[0].MissedActivities);
        resumed.AdvanceTo(start + 1440 + 480);
        var person = resumed.State.Citizens[0];
        Assert.Equal(2, person.MissedActivities);
        Assert.Equal(0, person.CompletedJourneys);
        Assert.Equal("home", person.PlaceId);
        Assert.Empty(person.RecentVisits);
    }

    [Fact]
    public void ClosureResultsMatchFineCoarseAndBudgetedReloadedAdvancement()
    {
        double start = 1440 + 1050 + Shift;
        double end = start + 80;
        var fine = new CitySimulator(ClosingShop(start), new Routes());
        for (double minute = start; minute <= end; minute++) fine.AdvanceTo(minute);
        var coarse = new CitySimulator(ClosingShop(start), new Routes());
        coarse.AdvanceTo(end);
        var budgeted = new CitySimulator(ClosingShop(start), new Routes());
        while (true)
        {
            bool caughtUp = budgeted.AdvanceTo(end, 1).CaughtUp;
            var read = SaveCodec.Read(SaveCodec.Write(budgeted.State));
            Assert.Equal(SaveReadStatus.Loaded, read.Status);
            budgeted = new CitySimulator(read.State!, new Routes());
            if (caughtUp) break;
        }
        Assert.Equal(SaveCodec.Write(fine.State), SaveCodec.Write(coarse.State));
        Assert.Equal(SaveCodec.Write(fine.State), SaveCodec.Write(budgeted.State));
        Assert.Equal(1, fine.State.Citizens[0].MissedActivities);
    }

    [Fact]
    public void NightOwlLeisureCanRetainItsOriginalWindowAcrossMidnight()
    {
        double midnight = 6 * 1440;
        var person = Enumerable.Range(0, 1000).Select(i => new CitizenState
        {
            Identity = new CitizenIdentity { Id = "owl" + i, Persona = Persona.NightOwl },
            HomeId = "home", LeisureId = "leisure"
        }).First(candidate => DailyRoutine.At(candidate, Seed, midnight - 1).Activity == Activity.Leisure
            && DailyRoutine.At(candidate, Seed, midnight - 1).EndsAt > midnight);
        var before = DailyRoutine.At(person, Seed, midnight - 1);
        var after = DailyRoutine.At(person, Seed, midnight);
        Assert.Equal(Activity.Leisure, after.Activity);
        Assert.Equal(before.EndsAt, after.EndsAt);
        Assert.True(after.EndsAt > midnight);
    }

    [Fact]
    public void WeeklyOpeningUsesFridayHoursRatherThanMondayOrLegacyHours()
    {
        var place = new Place
        {
            WeeklyHours = new List<OpeningWindow>
            {
                new() { Day = 0, FromMinute = 600, ToMinute = 720 },
                new() { Day = 4, FromMinute = 900, ToMinute = 1020 }
            }
        };
        Assert.True(DailyRoutine.Open(place, 1440 + 650));
        Assert.False(DailyRoutine.Open(place, 2 * 1440 + 650));
        Assert.False(DailyRoutine.Open(place, 5 * 1440 + 650));
        Assert.True(DailyRoutine.Open(place, 5 * 1440 + 950));
        Assert.Equal(5 * 1440 + 1020, DailyRoutine.NextClose(place, 5 * 1440 + 950));
    }

    [Fact]
    public void OvernightWeeklyOpeningIncludesPreviousDayAndStopsAtClosingBoundary()
    {
        var place = new Place { WeeklyHours = new List<OpeningWindow> { new() { Day = 4, FromMinute = 1380, ToMinute = 120 } } };
        Assert.False(DailyRoutine.Open(place, 5 * 1440 + 1379));
        Assert.True(DailyRoutine.Open(place, 5 * 1440 + 1380));
        Assert.True(DailyRoutine.Open(place, 6 * 1440 + 60));
        Assert.Equal(6 * 1440 + 120, DailyRoutine.NextClose(place, 6 * 1440 + 60));
        Assert.False(DailyRoutine.Open(place, 6 * 1440 + 120));
        Assert.Null(DailyRoutine.NextClose(place, 6 * 1440 + 120));
    }

    [Fact]
    public void NextCloseMergesAdjacentAndOverlappingWindowsIncludingWeekBoundary()
    {
        var place = new Place
        {
            WeeklyHours = new List<OpeningWindow>
            {
                new() { Day = 0, FromMinute = 650, ToMinute = 700 },
                new() { Day = 0, FromMinute = 700, ToMinute = 720 },
                new() { Day = 0, FromMinute = 600, ToMinute = 660 },
                new() { Day = 6, FromMinute = 1380, ToMinute = 120 },
                new() { Day = 0, FromMinute = 100, ToMinute = 180 }
            }
        };
        Assert.Equal(1440 + 720, DailyRoutine.NextClose(place, 1440 + 620));
        Assert.Equal(8 * 1440 + 180, DailyRoutine.NextClose(place, 8 * 1440 + 60));
        Assert.Equal(double.PositiveInfinity, DailyRoutine.NextClose(new Place(), 1440));
        var alwaysOpenWeekly = new Place
        {
            WeeklyHours = Enumerable.Range(0, 7).Select(day => new OpeningWindow { Day = day, FromMinute = 0, ToMinute = 1440 }).ToList()
        };
        Assert.Equal(double.PositiveInfinity, DailyRoutine.NextClose(alwaysOpenWeekly, 5 * 1440 + 950));
    }

    [Fact]
    public void AdmittedVisitorDepartsAtClosingAndHoldsHomeFallbackWithoutAnotherMiss()
    {
        double start = 1440 + 1050 + Shift;
        var city = City(Persona.Foodie, start, shop: true);
        city.Places.Single(p => p.Id == "shop").WeeklyHours.Add(new OpeningWindow
        { Day = 0, FromMinute = 1050 + Shift - 10, ToMinute = 1050 + Shift + 20 });
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(start + 19);
        var person = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.AtPlace, person.Phase);
        Assert.Equal("shop", person.PlaceId);
        Assert.Equal(start + 20, person.NextEventMinute);
        Assert.Equal(0, person.MissedActivities);
        Assert.Single(person.RecentVisits, visit => visit.Activity == Activity.Shop);
        var read = SaveCodec.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new Routes());
        resumed.AdvanceTo(start + 20);
        person = resumed.State.Citizens[0];
        Assert.Equal("shop", person.PlaceId);
        Assert.Equal("home", person.DestinationId);
        Assert.Equal(CitizenPhase.Travelling, person.Phase);
        Assert.Equal(0, person.MissedActivities);
        resumed.AdvanceTo(start + 40);
        Assert.Equal("home", person.PlaceId);
        Assert.Equal(CitizenPhase.AtPlace, person.Phase);
        Assert.Equal(start + 45, person.NextEventMinute);
        Assert.Equal(0, person.MissedActivities);
        Assert.Single(person.RecentVisits, visit => visit.Activity == Activity.Shop);
    }

    [Fact]
    public void ZeroCapacityOptionalVenuesAreNotAssigned()
    {
        var source = City(Persona.Foodie, 1440, shop: true, leisure: true);
        foreach (var place in source.Places.Where(p => p.Kind != PlaceKind.Home)) place.Capacity = 0;
        var city = Population.Create(source.Citizens.Select(c => c.Identity), source.Places, source.PopulationKey, source.RloVersion, Seed, 1440);
        var person = city.Citizens[0];
        Assert.Equal("", person.ShopId);
        Assert.Equal("", person.LeisureId);
        var engine = new CitySimulator(city, new Routes());
        engine.AdvanceTo(8 * 1440);
        Assert.Equal("home", person.PlaceId);
        Assert.Equal(0, person.CompletedJourneys);
        Assert.Equal(0, person.MissedActivities);
    }

    [Fact]
    public void PopulationRejectsUnsupportedTimeAndEnumValues()
    {
        var source = City(Persona.Commuter, 1440);
        Assert.Throws<ArgumentOutOfRangeException>(() => Population.Create(source.Citizens.Select(c => c.Identity), source.Places, source.PopulationKey, source.RloVersion, Seed, 1e9 + 1));
        source.Citizens[0].Identity.Persona = (Persona)999;
        Assert.Throws<ArgumentException>(() => Population.Create(source.Citizens.Select(c => c.Identity), source.Places, source.PopulationKey, source.RloVersion, Seed, 1440));
        source.Citizens[0].Identity.Persona = Persona.Commuter;
        source.Places[0].Kind = (PlaceKind)999;
        Assert.Throws<ArgumentException>(() => Population.Create(source.Citizens.Select(c => c.Identity), source.Places, source.PopulationKey, source.RloVersion, Seed, 1440));
    }
}
