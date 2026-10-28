using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PopulationExpansionTests
{
    private sealed class Walking : IRoutePlanner
    {
        public double Minutes = 12;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => new()
        { Legs = new() { new() { From = from.Position, To = to.Position, Minutes = Minutes, Mode = TravelMode.Walk } } };
    }

    private static CityState City(int count = 4)
    {
        var city = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "c" + i, Name = "Given" + i + " Family" + i, Age = 25 + i,
            Female = i % 2 == 0, AppearanceSeed = 100 + i, Persona = (Persona)(i % 8), District = "district"
        }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, District = "district", Capacity = count },
            new Place { Id = "work-a", Name = "Work A", Kind = PlaceKind.Work, Capacity = count, Position = new(300, 0, 0) },
            new Place { Id = "work-b", Name = "Work B", Kind = PlaceKind.Work, Capacity = count, Position = new(500, 0, 0) },
            new Place { Id = "study", Name = "Study", Kind = PlaceKind.Study, Capacity = count, Position = new(600, 0, 0) },
            new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = count, Position = new(100, 0, 0) },
            new Place { Id = "leisure-a", Name = "Leisure A", Kind = PlaceKind.Leisure, Capacity = count, Position = new(150, 0, 0) },
            new Place { Id = "leisure-b", Name = "Leisure B", Kind = PlaceKind.Leisure, Capacity = count, Position = new(250, 0, 0) }
        }, "test-source", "0.40.4", 7, 1440);
        return city;
    }

    private static CityState Copy(CityState city) => JsonConvert.DeserializeObject<CityState>(JsonConvert.SerializeObject(city))!;

    [Fact]
    public void SourceAndActiveIdentitiesAreDetachedAndRemixKeepsAppearanceDonorCoherent()
    {
        var city = City(16);
        var before = JsonConvert.SerializeObject(city.SourceIdentities);
        Assert.All(city.Citizens, c => Assert.NotSame(c.Identity, city.SourceIdentities.Single(s => s.Id == c.Identity.Id)));
        var generated = Enumerable.Range(1, 40).Select(i => ResidentLifecycle.RemixIdentity(city, i)).ToArray();
        foreach (var identity in generated)
        {
            var donor = city.SourceIdentities.Single(s => s.AppearanceSeed == identity.AppearanceSeed);
            Assert.Equal(donor.Age, identity.Age); Assert.Equal(donor.Female, identity.Female);
            Assert.StartsWith(donor.Name.Split(' ')[0] + " ", identity.Name);
            Assert.Contains(city.SourceIdentities, s => identity.Name.EndsWith(" " + s.Name.Split(' ')[1], StringComparison.Ordinal));
            Assert.Contains(city.SourceIdentities, s => s.Persona == identity.Persona);
        }
        Assert.Equal(40, generated.Select(g => g.Id).Distinct().Count());
        Assert.Contains(generated, g => city.SourceIdentities.Single(s => s.AppearanceSeed == g.AppearanceSeed).Persona != g.Persona);
        Assert.Equal(before, JsonConvert.SerializeObject(city.SourceIdentities));
    }

    [Fact]
    public void ThousandRemixedResidentsRespectReviewedRloAgeBandsWithoutLosingCompatibleVariety()
    {
        var city = City(40);
        foreach (var source in city.SourceIdentities)
        {
            int index = int.Parse(source.Id.Substring(1));
            source.Age = source.Persona switch
            {
                Persona.Student => 18 + index % 8,
                Persona.Parent => 27 + index % 24,
                Persona.Retiree => 65 + index % 18,
                _ => 20 + index % 45
            };
            city.Citizens.Single(c => c.Identity.Id == source.Id).Identity.Age = source.Age;
        }
        var residents = Enumerable.Range(1, 1000).Select(i => ResidentLifecycle.RemixIdentity(city, i)).ToArray();
        foreach (var person in residents)
        {
            var donor = city.SourceIdentities.Single(s => s.AppearanceSeed == person.AppearanceSeed);
            Assert.Equal(donor.Age, person.Age); Assert.Equal(donor.Female, person.Female);
            switch (person.Persona)
            {
                case Persona.Student: Assert.InRange(person.Age, 18, 25); break;
                case Persona.Parent: Assert.InRange(person.Age, 27, 50); break;
                case Persona.Retiree: Assert.InRange(person.Age, 65, 82); break;
                default: Assert.InRange(person.Age, 20, 64); break;
            }
        }
        Assert.Contains(residents, r => city.SourceIdentities.Single(s => s.AppearanceSeed == r.AppearanceSeed).Persona != r.Persona);
        Assert.Equal(8, residents.Select(r => r.Persona).Distinct().Count());
    }

    [Fact]
    public void FullCandidateChoiceAllowsFarVenuesAndRespondsToAttractionAndDistance()
    {
        var home = new Place { Id = "home", Kind = PlaceKind.Home };
        var venues = Enumerable.Range(0, 8).Select(i => new Place
        { Id = "v" + i, Kind = PlaceKind.Leisure, Position = new(i * 250, 0, 0) }).ToArray();
        var selections = Enumerable.Range(0, 2000).Select(i => DestinationChoice.Choose(venues, home, PlaceKind.Leisure, "choice" + i, 7)).ToArray();
        Assert.Equal(8, selections.Distinct().Count());
        Assert.True(selections.Count(s => s == "v0") > selections.Count(s => s == "v7"));
        venues[7].AttractionWeight = 20; venues[7].NativeTrafficIndex = 100;
        int attractive = Enumerable.Range(0, 2000).Count(i => DestinationChoice.Choose(venues, home, PlaceKind.Leisure, "choice" + i, 7) == "v7");
        Assert.True(attractive > selections.Count(s => s == "v7") * 3);
        Assert.Equal(DestinationChoice.Choose(venues, home, PlaceKind.Leisure, "order", 7),
            DestinationChoice.Choose(venues.Reverse(), home, PlaceKind.Leisure, "order", 7));
    }

    [Fact]
    public void LeisureVariesAcrossDaysAndCommittedWaitingChoiceSurvivesReload()
    {
        var city = City(1); var person = city.Citizens[0];
        var destinations = Enumerable.Range(1, 50).Select(day => DailyRoutine.At(person, city.Seed, day * 1440 + 1200, city.Places))
            .Where(s => s.Activity == Activity.Leisure).Select(s => s.PlaceId).Distinct().ToArray();
        Assert.Equal(2, destinations.Length);
        var slot = Enumerable.Range(1, 7).Select(day => DailyRoutine.At(person, city.Seed, day * 1440 + 1200, city.Places))
            .First(s => s.Activity == Activity.Leisure);
        double now = slot.EndsAt - 10;
        person.Phase = CitizenPhase.WaitingForOpening; person.DestinationId = slot.PlaceId == "leisure-a" ? "leisure-b" : "leisure-a";
        person.DestinationActivity = Activity.Leisure; person.ActivityUntilMinute = slot.EndsAt;
        var resumed = Copy(city).Citizens[0];
        Assert.Equal(person.DestinationId, DailyRoutine.At(resumed, city.Seed, now, city.Places).PlaceId);
    }

    [Fact]
    public void ClosingCommittedLeisureVenueReturnsHomeInsteadOfStartingSecondLeisureTrip()
    {
        var city = City(1); var person = city.Citizens[0];
        var slot = DailyRoutine.At(person, city.Seed, 5 * 1440 + 900, city.Places);
        Assert.Equal(Activity.Leisure, slot.Activity);
        // A migrated/previously committed venue need not equal today's new draw.
        var current = city.Places.Single(p => p.Id == (slot.PlaceId == "leisure-a" ? "leisure-b" : "leisure-a"));
        double closure = slot.EndsAt - 20;
        current.CloseMinute = (int)(closure % 1440);
        city.Minute = closure - 10;
        person.PlaceId = current.Id; person.Activity = Activity.Leisure; person.Phase = CitizenPhase.AtPlace;
        person.ActivityUntilMinute = closure; person.NextEventMinute = closure;
        person.RecentVisits.Add(new VisitRecord { AtMinute = city.Minute, PlaceId = current.Id, Activity = Activity.Leisure });
        var read = SaveCodec.Read(SaveCodec.Write(city));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var simulation = new CitySimulator(read.State!, new Walking());
        simulation.AdvanceTo(closure);
        person = simulation.State.Citizens[0];
        Assert.Equal("home", person.DestinationId);
        Assert.Equal(CitizenPhase.Travelling, person.Phase);
        simulation.AdvanceTo(slot.EndsAt);
        Assert.Equal("home", person.PlaceId);
        Assert.Single(person.RecentVisits, v => v.Activity == Activity.Leisure);
        Assert.DoesNotContain(person.RecentVisits, v => v.PlaceId == slot.PlaceId);
    }

    [Fact]
    public void PreviousDayLeisureArrivalDoesNotCommitTodaysDifferentWindow()
    {
        var city = City(1); var person = city.Citizens[0];
        var today = DailyRoutine.At(person, city.Seed, 5 * 1440 + 900, city.Places);
        var old = Enumerable.Range(1, 4).Select(day => DailyRoutine.At(person, city.Seed, day * 1440 + 1200, city.Places))
            .First(s => s.Activity == Activity.Leisure);
        Assert.Equal(Activity.Leisure, today.Activity);
        person.PlaceId = today.PlaceId == "leisure-a" ? "leisure-b" : "leisure-a";
        person.Activity = Activity.Leisure; person.Phase = CitizenPhase.AtPlace;
        person.ActivityUntilMinute = old.EndsAt;
        Assert.Equal(today.PlaceId, DailyRoutine.At(person, city.Seed, 5 * 1440 + 900, city.Places).PlaceId);
    }

    [Fact]
    public void DueLifecycleAndDailyChoicesIgnoreTickSizeBudgetAndReloadBoundary()
    {
        var initial = City(12);
        var fine = new CitySimulator(Copy(initial), new Walking());
        var coarse = new CitySimulator(Copy(initial), new Walking());
        double end = initial.Minute + 20 * 1440;
        for (double minute = initial.Minute; minute <= end; minute += 5) fine.AdvanceTo(minute);
        int slices = 0;
        while (!coarse.AdvanceTo(end, 17).CaughtUp)
        {
            if (++slices % 11 == 0)
            {
                var read = SaveCodec.Read(SaveCodec.Write(coarse.State));
                Assert.Equal(SaveReadStatus.Loaded, read.Status);
                coarse = new CitySimulator(read.State!, new Walking());
            }
        }
        Assert.Equal(initial.Citizens.Select(c => c.Identity.Id), coarse.State.Citizens.Select(c => c.Identity.Id));
        Assert.All(coarse.State.Citizens.Where(ResidentLifecycle.IsWorker), c => Assert.True(c.LastEmploymentCheckDay >= 20));
        Assert.Equal(SaveCodec.Write(fine.State), SaveCodec.Write(coarse.State));
    }

    private sealed class Driving : ICitizenRoutePlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => throw new InvalidOperationException("Citizen overload required.");
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen)
        {
            citizen.Car ??= new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = new(10, 0, 0) };
            return new Route { Legs = new()
            {
                new() { From = from.Position, To = citizen.Car.ParkedPosition, Minutes = 10, Mode = TravelMode.Walk },
                new() { From = citizen.Car.ParkedPosition, To = new(290, 0, 0), Minutes = 20, Mode = TravelMode.Car },
                new() { From = new(290, 0, 0), To = to.Position, Minutes = 10, Mode = TravelMode.Walk }
            } };
        }
    }

    [Fact]
    public void CarPositionFollowsDrivingLegsAndCommitsBeforeFinalWalkingArrival()
    {
        var city = City(1); var person = city.Citizens[0]; person.Preferences.OwnsCar = true; person.DayPlaceId = "work-a";
        double start = 1440 + 510 + (int)(StableRandom.Hash(person.Identity.Id + ":hours", city.Seed) % 31) - 15;
        var simulation = new CitySimulator(city, new Driving());
        simulation.AdvanceTo(start + 5);
        Assert.Equal(10, simulation.CarPositionOf(person)!.Value.X);
        simulation.AdvanceTo(start + 20);
        Assert.Equal(150, simulation.CarPositionOf(person)!.Value.X);
        var resumed = new CitySimulator(Copy(city), new Driving());
        Assert.Equal(150, resumed.CarPositionOf(resumed.State.Citizens[0])!.Value.X);
        simulation.AdvanceTo(start + 35);
        Assert.Equal(290, simulation.CarPositionOf(person)!.Value.X);
        Assert.Equal(10, person.Car!.ParkedPosition.X);
        simulation.AdvanceTo(start + 40);
        Assert.Equal(290, person.Car.ParkedPosition.X); Assert.Null(person.Journey);
    }

    [Fact]
    public void CarCannotBeBorrowedTeleportedOrUsedInDisconnectedBlocks()
    {
        var person = City(1).Citizens[0]; person.Preferences.OwnsCar = true;
        person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = new(10, 0, 0) };
        var route = new Route { Legs = new() { new() { From = new(50, 0, 0), To = new(100, 0, 0), Minutes = 10, Mode = TravelMode.Car } } };
        Assert.Throws<InvalidOperationException>(() => CitySimulator.ValidateCarRoute(route, person));
        route.Legs[0].From = person.Car.ParkedPosition;
        person.Car.Id = "somebody-else:car";
        Assert.Throws<InvalidOperationException>(() => CitySimulator.ValidateCarRoute(route, person));
        person.Car.Id = person.Identity.Id + ":car";
        route.Legs.Add(new() { From = new(100, 0, 0), To = new(110, 0, 0), Minutes = 1, Mode = TravelMode.Walk });
        route.Legs.Add(new() { From = new(110, 0, 0), To = new(120, 0, 0), Minutes = 1, Mode = TravelMode.Car });
        Assert.Throws<InvalidOperationException>(() => CitySimulator.ValidateCarRoute(route, person));
    }
}
