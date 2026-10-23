using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class OptionalDestinationTests
{
    private const int Seed = 7;
    private static readonly double Start = 1440 + 1050 + (int)(StableRandom.Hash("person:hours", Seed) % 31) - 15;
    private sealed class Routes : IRoutePlanner, IWalkingTimeBound
    {
        public readonly List<string> Calls = new();
        public readonly Dictionary<string, double?> Times = new();
        public double Bound;
        public double MinimumWalkingMinutes(Place from, Place to) => Bound;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            Calls.Add(to.Id);
            double? duration = Times.TryGetValue(to.Id, out var value) ? value : 10;
            return !duration.HasValue ? null : new Route { Legs = new() {
                new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = duration.Value } } };
        }
    }
    private static CityState City(int shops = 2)
    {
        var venues = new List<Place> { new() { Id = "home", Name = "Home", District = "district", Kind = PlaceKind.Home, Capacity = 1 } };
        for (int i = 0; i < shops; i++) venues.Add(new Place { Id = "s" + i, Name = "Shop " + i, District = "district", Kind = PlaceKind.Shop,
            Position = new Position(20 + i * 5, 0, 0) });
        var city = Population.Create(new[] { new CitizenIdentity { Id = "person", Name = "Person", District = "district", Persona = Persona.Foodie, Age = 30 } },
            venues, "optional-test", "0.40.4", Seed, Start);
        city.Citizens[0].ShopId = "s0";
        city.Citizens[0].Preferences.OwnsCar = false;
        city.Citizens[0].Preferences.AllowTransit = false;
        return city;
    }
    private static CitySimulator Reload(CitySimulator sim, Routes routes)
    {
        var loaded = SaveCodec.Read(SaveCodec.Write(sim.State));
        Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
        return new CitySimulator(loaded.State!, routes);
    }

    [Fact]
    public void ClosedFamiliarShopUsesAnOpenAlternativeWithoutWastingRouteQueries()
    {
        var city = City(); city.Places[1].CloseMinute = 0;
        var routes = new Routes(); var sim = new CitySimulator(city, routes);
        sim.AdvanceTo(Start + 11);
        Assert.Equal("s1", city.Citizens[0].PlaceId);
        Assert.Equal(new[] { "s1" }, routes.Calls);
        Assert.Single(city.Citizens[0].RecentVisits);
        Assert.Equal(0, city.Citizens[0].MissedActivities);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void UnreachableOrTooLatePreferredTripChoosesAFeasibleAlternative(bool unreachable)
    {
        var city = City(); var routes = new Routes(); routes.Times["s0"] = unreachable ? null : 60;
        var sim = new CitySimulator(city, routes); sim.AdvanceTo(Start);
        Assert.Equal("s1", city.Citizens[0].DestinationId);
        Assert.Equal("home", city.Citizens[0].PlaceId);
        Assert.Equal(new[] { "s0", "s1" }, routes.Calls);
        var resumed = Reload(sim, routes); resumed.AdvanceTo(Start + 11);
        Assert.Equal("s1", resumed.State.Citizens[0].PlaceId);
        Assert.Single(resumed.State.Citizens[0].RecentVisits);
        Assert.Equal(2, routes.Calls.Count); // Chosen route is committed, never searched twice.
    }

    [Fact]
    public void LeavesBeforeOpeningAndWaitsAtDestinationAcrossReload()
    {
        var city = City(1); city.Places[1].OpenMinute = (int)(Start % 1440) + 20;
        var routes = new Routes(); var sim = new CitySimulator(city, routes);
        sim.AdvanceTo(Start);
        Assert.Equal(CitizenPhase.Travelling, city.Citizens[0].Phase);
        sim.AdvanceTo(Start + 10);
        Assert.Equal("s0", city.Citizens[0].PlaceId);
        Assert.Equal(CitizenPhase.WaitingForOpening, city.Citizens[0].Phase);
        Assert.Empty(city.Citizens[0].RecentVisits);
        var resumed = Reload(sim, routes); resumed.AdvanceTo(Start + 21);
        Assert.Equal(Start + 20, Assert.Single(resumed.State.Citizens[0].RecentVisits).AtMinute);
        Assert.Single(routes.Calls);
    }

    [Fact]
    public void RejectsAnOpenShopWhoseClosingTimeLeavesNoUsefulVisit()
    {
        var city = City(1); city.Places[1].CloseMinute = (int)(Start % 1440) + 12;
        var routes = new Routes(); var sim = new CitySimulator(city, routes); sim.AdvanceTo(Start + 20);
        Assert.Equal("home", city.Citizens[0].PlaceId);
        Assert.Empty(city.Citizens[0].RecentVisits);
        Assert.Equal(1, city.Citizens[0].MissedActivities);
        Assert.Single(routes.Calls);
    }

    [Fact]
    public void BoundedSearchDoesNotTeleportOrRerollOnAnEarlyHomeEventOrReload()
    {
        var city = City(12); var routes = new Routes();
        foreach (var shop in city.Places.Skip(1)) routes.Times[shop.Id] = null;
        var sim = new CitySimulator(city, routes); var observations = new List<RoutineDiagnostic>();
        sim.RoutineObserved += observations.Add;
        sim.AdvanceTo(Start);
        Assert.Equal(2, routes.Calls.Count);
        Assert.Equal(1, city.Citizens[0].MissedActivities);
        Assert.Single(observations, x => x.Outcome == "missed");
        // A runtime rebuild or lifecycle wake during the same window cannot retry it.
        city.Citizens[0].NextEventMinute = Start + 5;
        var resumed = Reload(sim, routes); resumed.AdvanceTo(Start + 44);
        Assert.Equal("home", resumed.State.Citizens[0].PlaceId);
        Assert.Equal(1, resumed.State.Citizens[0].MissedActivities);
        Assert.Equal(2, routes.Calls.Count);
    }

    [Fact]
    public void WalkingLowerBoundDoesNotExcludeAnAllowedMultimodalTrip()
    {
        var city = City(1); city.Citizens[0].Preferences.AllowTransit = true;
        var routes = new Routes { Bound = 500 };
        new CitySimulator(city, routes).AdvanceTo(Start + 11);
        Assert.Equal("s0", city.Citizens[0].PlaceId);
        Assert.Single(routes.Calls);
    }

    [Fact]
    public void AlreadyCommittedDestinationSurvivesNewPolicyAndReload()
    {
        var city = City(); var person = city.Citizens[0];
        person.Phase = CitizenPhase.WaitingForOpening; person.DestinationId = "s0";
        person.DestinationActivity = Activity.Shop; person.ActivityUntilMinute = Start + 45;
        person.NextEventMinute = Start + 5; city.Places[1].OpenMinute = (int)(Start % 1440) + 5;
        var routes = new Routes(); routes.Times["s0"] = null;
        var sim = new CitySimulator(city, routes); sim = Reload(sim, routes); sim.AdvanceTo(Start + 10);
        Assert.Equal("s0", sim.State.Citizens[0].DestinationId);
        Assert.Equal(CitizenPhase.WaitingForRoute, sim.State.Citizens[0].Phase);
        Assert.Equal(new[] { "s0" }, routes.Calls);
    }

    [Fact]
    public void ShortlistRetainsVarietyAndDoesNotDependOnCatalogueOrder()
    {
        var city = City(20); var origin = city.Places[0];
        var selected = new HashSet<string>();
        for (int day = 0; day < 40; day++)
        {
            var a = DestinationChoice.Shortlist(city.Places, origin, PlaceKind.Shop, "day" + day, Seed);
            var b = DestinationChoice.Shortlist(city.Places.AsEnumerable().Reverse(), origin, PlaceKind.Shop, "day" + day, Seed);
            Assert.Equal(a.Select(p => p.Id), b.Select(p => p.Id));
            selected.Add(a[0].Id);
        }
        Assert.True(selected.Count > 8);
    }

    [Fact]
    public void MissDiagnosticsSurviveClockRebuildAndCanUnsubscribeThroughOldFacade()
    {
        var city = City(1); city.Places[1].CloseMinute = 0;
        var original = new CitySimulator(city, new Routes()); var rows = new List<RoutineDiagnostic>();
        original.RoutineObserved += rows.Add;
        var sim = original.RebuildRoutes(new Routes()); sim.AdvanceTo(Start);
        Assert.Single(rows, x => x.Outcome == "missed");
        int count = rows.Count; original.RoutineObserved -= rows.Add;
        sim.AdvanceTo(Start + 1440);
        Assert.Equal(count, rows.Count);
    }
}
