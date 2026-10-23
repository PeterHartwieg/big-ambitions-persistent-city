using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RhythmContinuityTests
{
    private sealed class Routes : ICommuteTimingPlanner
    {
        public bool Reachable = true;
        public double Minutes = 120;
        public readonly List<(string From, double At)> Queries = new();
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            Queries.Add((from.Id, departureMinute));
            return !Reachable ? null : new Route { Legs = new()
                { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = Minutes } } };
        }
    }

    private static CityState Commuters() => Population.Create(Enumerable.Range(0, 12).Select(i => new CitizenIdentity
    {
        Id = "worker" + i, Name = "Worker " + i, Age = 35, Persona = i % 3 == 0 ? Persona.NightOwl : Persona.Commuter
    }), new[]
    {
        new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 12, Position = new Position(0, 0, 0) },
        new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 12, Position = new Position(100, 0, 0) }
    }, "rhythm-continuity", "0.40.4", 7, 1440);

    [Fact]
    public void PreparationAndTripsMatchFineCoarseAndBudgetedReloadedAdvancement()
    {
        const double end = 9 * 1440;
        var fine = new CitySimulator(Commuters(), new Routes());
        var coarse = new CitySimulator(Commuters(), new Routes());
        var reloaded = new CitySimulator(Commuters(), new Routes());
        for (double minute = 1440; minute <= end; minute += 5) fine.AdvanceTo(minute);
        coarse.AdvanceTo(end);
        while (true)
        {
            bool caughtUp = reloaded.AdvanceTo(end, 1).CaughtUp;
            var read = SaveCodec.Read(SaveCodec.Write(reloaded.State));
            Assert.Equal(SaveReadStatus.Loaded, read.Status);
            reloaded = new CitySimulator(read.State!, new Routes());
            if (caughtUp) break;
        }
        Assert.Equal(SaveCodec.Write(fine.State), SaveCodec.Write(coarse.State));
        Assert.Equal(SaveCodec.Write(fine.State), SaveCodec.Write(reloaded.State));
        Assert.All(fine.State.Citizens, citizen => Assert.True(citizen.CompletedJourneys > 5));
    }

    [Fact]
    public void MultiDayCommuterAggregateHasBoundedEventsOneFreshQueryPerSteadyShiftAndNoMidnightPulse()
    {
        var city = Commuters();
        foreach (var person in city.Citizens) person.NextJobChangeMinute = person.NextDepartureMinute = ResidentLifecycle.Never;
        var routes = new Routes(); var engine = new CitySimulator(city, routes);
        var eventTimes = new List<double>(); const double end = 9 * 1440;
        while (true)
        {
            double next = city.Citizens.Min(person => person.NextEventMinute);
            if (next > end) break;
            eventTimes.Add(next);
            Assert.Equal(1, engine.AdvanceTo(next, 1).Events);
            Assert.True(eventTimes.Count <= 336, "A twelve-resident, five-shift run must not create polling storms.");
        }
        engine.AdvanceTo(end);
        Assert.Equal(72, routes.Queries.Count(query => query.From == "home")); // Twelve estimates plus sixty departures.
        Assert.Equal(60, routes.Queries.Count(query => query.From == "work"));
        Assert.Equal(120, city.Citizens.Sum(person => person.CompletedJourneys));
        Assert.Equal(0, city.Citizens.Sum(person => person.MissedActivities));
        Assert.Equal(60, city.Citizens.Sum(person => person.RecentVisits.Count(visit => visit.Activity == Activity.Work)));
        Assert.DoesNotContain(eventTimes, minute => minute >= 2 * 1440 && minute % 1440 == 0);
        Assert.DoesNotContain(routes.Queries, query => query.At >= 2 * 1440 && query.At % 1440 == 0);
        var departureMinutes = routes.Queries.Where(query => query.From == "home" && query.At >= 2 * 1440)
            .Select(query => query.At % 1440).Distinct().ToArray();
        Assert.True(departureMinutes.Length >= 6, "Personal shifts should distribute departures across several minutes.");
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city)).Status);
    }

    private const double Midnight = 6 * 1440;
    private static CityState OldLeisure()
    {
        var city = Population.Create(new[] { new CitizenIdentity
        { Id = "owl-old-policy", Name = "Older Policy", Age = 35, Persona = Persona.NightOwl } }, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0, 0, 0) },
            new Place { Id = "leisure", Name = "Leisure", Kind = PlaceKind.Leisure, Capacity = 1, Position = new Position(10, 0, 0) }
        }, "old-leisure", "0.40.4", 7, Midnight - 10);
        var person = city.Citizens[0];
        person.DestinationId = "leisure"; person.DestinationActivity = Activity.Leisure;
        // Intentionally differs from the new routine's endpoint, as a policy migration can.
        person.ActivityUntilMinute = Midnight + 30;
        Assert.NotEqual(person.ActivityUntilMinute, DailyRoutine.At(person, city.Seed, Midnight).EndsAt);
        return city;
    }

    private static CitySimulator Reload(CityState city, Routes routes)
    {
        var read = SaveCodec.Read(SaveCodec.Write(city));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        return new CitySimulator(read.State!, routes);
    }

    [Fact]
    public void OldCommittedJourneyKeepsLeisureDeadlineAfterMidnightArrivalAndReload()
    {
        var city = OldLeisure(); var person = city.Citizens[0];
        person.Phase = CitizenPhase.Travelling; person.DepartureMinute = city.Minute - 5;
        person.Journey = new Route { Legs = new List<RouteLeg> { new()
        { From = city.Places.First(p => p.Id == "home").Position, To = city.Places.First(p => p.Id == "leisure").Position,
            Mode = TravelMode.Walk, Minutes = 20 } } };
        person.NextEventMinute = person.DepartureMinute + 20;
        var engine = Reload(city, new Routes { Minutes = 5 });
        engine.AdvanceTo(Midnight + 5); person = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.AtPlace, person.Phase); Assert.Equal("leisure", person.PlaceId);
        Assert.Equal(Midnight + 30, person.ActivityUntilMinute); Assert.Equal(Midnight + 30, person.NextEventMinute);
        Assert.Single(person.RecentVisits);
        engine = Reload(engine.State, new Routes { Minutes = 5 }); engine.AdvanceTo(Midnight + 29);
        Assert.Equal("leisure", engine.State.Citizens[0].PlaceId);
        Assert.Equal(CitizenPhase.AtPlace, engine.State.Citizens[0].Phase);
    }

    [Fact]
    public void OldCommittedRouteWaitKeepsDestinationAndDeadlineThroughMidnightRetry()
    {
        var city = OldLeisure(); var person = city.Citizens[0];
        person.Phase = CitizenPhase.WaitingForRoute; person.NextEventMinute = city.Minute + 1;
        var routes = new Routes { Reachable = false, Minutes = 5 }; var engine = Reload(city, routes);
        engine.AdvanceTo(Midnight + 1); person = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.WaitingForRoute, person.Phase); Assert.Equal("leisure", person.DestinationId);
        Assert.Equal(Midnight + 30, person.ActivityUntilMinute); Assert.Equal(0, person.MissedActivities);
        routes.Reachable = true; engine = Reload(engine.State, routes);
        double retry = engine.State.Citizens[0].NextEventMinute;
        engine.AdvanceTo(retry + 5); person = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.AtPlace, person.Phase); Assert.Equal("leisure", person.PlaceId);
        Assert.Equal(Midnight + 30, person.NextEventMinute); Assert.Single(person.RecentVisits);
    }

    [Fact]
    public void OldCommittedOpeningWaitAdmitsUntilSavedDeadline()
    {
        var city = OldLeisure(); var person = city.Citizens[0];
        city.Places.First(p => p.Id == "leisure").OpenMinute = 10;
        person.Phase = CitizenPhase.WaitingForOpening; person.PlaceId = "leisure"; person.Activity = Activity.Leisure;
        person.NextEventMinute = Midnight + 10;
        var engine = Reload(city, new Routes { Minutes = 5 }); engine.AdvanceTo(Midnight + 10);
        person = engine.State.Citizens[0];
        Assert.Equal(CitizenPhase.AtPlace, person.Phase); Assert.Equal("leisure", person.PlaceId);
        Assert.Equal(Midnight + 30, person.NextEventMinute); Assert.Single(person.RecentVisits);
        Assert.Equal(Midnight + 10, person.RecentVisits[0].AtMinute);
    }
}
