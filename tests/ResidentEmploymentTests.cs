using System;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class ResidentEmploymentTests
{
    private sealed class Routes : ICitizenRoutePlanner
    {
        public int Calls;
        public bool Reject, Incomplete, MutateProbe;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
            => throw new InvalidOperationException("The resident-aware overload is required.");
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen)
        {
            Calls++;
            if (MutateProbe)
            {
                citizen.Identity.Name = "Probe only";
                citizen.Preferences.AllowTransit = false;
                citizen.Car ??= new CarState { Id = citizen.Identity.Id + ":car" };
                citizen.Car.ParkedPosition = new(999, 0, 0);
            }
            if (Reject) return null;
            return new Route { Legs = new() { new() { From = from.Position,
                To = Incomplete ? new(999, 0, 0) : to.Position, Minutes = 10, Mode = TravelMode.Walk } } };
        }
    }

    // Select reproducible seeds for a rare event; assertions below concern state,
    // interrupted travel and accounting rather than repeating the chance calculation.
    private static int SeedFor(bool loss)
        => Enumerable.Range(0, 100000).First(seed =>
            (StableRandom.Hash("worker:employment:1", seed) + 1.0) / (uint.MaxValue + 2.0)
                < (loss ? CityBalance.DailyJobLossChance : CityBalance.DailyJobFindingChance));

    private static CityState City(int seed, double now = 1440, Persona persona = Persona.Commuter)
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "worker", Name = "Stable Resident", Age = 30, Persona = persona } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
                new Place { Id = "work-a", Name = "Work A", Kind = PlaceKind.Work, Capacity = 1, Position = new(10,0,0) },
                new Place { Id = "work-b", Name = "Work B", Kind = PlaceKind.Work, Capacity = 1, Position = new(20,0,0) },
                new Place { Id = "school", Name = "School", Kind = PlaceKind.Study, Capacity = 1, Position = new(30,0,0) } },
            "employment-test", "test", seed, 0);
        city.Minute = now;
        var person = city.Citizens.Single();
        person.NextEventMinute = now; person.ActivityUntilMinute = now;
        return city;
    }

    [Fact]
    public void LossAtHomeKeepsResidentHomeCarSavingsBasisAndAssignedPlacesStable()
    {
        var city = City(SeedFor(true)); var person = city.Citizens.Single();
        var identity = person.Identity; string home = person.HomeId, shop = person.ShopId, leisure = person.LeisureId;
        person.Preferences.OwnsCar = true;
        person.Car = new CarState { Id = "worker:car", ModelId = "model", PaintSeed = 12, ParkedPosition = new(2,0,0) };
        var car = person.Car;
        var simulator = new CitySimulator(city, new Routes()); simulator.AdvanceTo(1440, 1);
        Assert.Same(person, city.Citizens.Single()); Assert.Same(identity, person.Identity); Assert.Same(car, person.Car);
        Assert.Equal(home, person.HomeId); Assert.Equal(shop, person.ShopId); Assert.Equal(leisure, person.LeisureId);
        Assert.Equal("", person.DayPlaceId); Assert.Equal(1, person.JobChanges); Assert.Equal(1, person.LastEmploymentCheckDay);
        Assert.Equal(2, car.ParkedPosition.X); Assert.Equal("model", car.ModelId); Assert.Equal(12, car.PaintSeed);
        Assert.Equal(1, city.NextResidentSequence); Assert.Single(city.Citizens);
        var saved = SaveCodec.Write(city); var reloaded = SaveCodec.Read(saved);
        Assert.Equal(SaveReadStatus.Loaded, reloaded.Status); Assert.Equal(saved, SaveCodec.Write(reloaded.State!));
        new CitySimulator(reloaded.State!, new Routes()).AdvanceTo(1440);
        Assert.Equal(1, reloaded.State!.Citizens.Single().JobChanges);
    }

    [Fact]
    public void OverdueLossWaitsUntilPhysicalHomeArrivalAndDoesNotRerollAfterReload()
    {
        var city = City(SeedFor(true), 1445); var person = city.Citizens.Single();
        string workplace = person.DayPlaceId;
        person.PlaceId = workplace; person.Activity = Activity.Work; person.DestinationId = "home";
        person.DestinationActivity = Activity.Home; person.Phase = CitizenPhase.Travelling;
        person.DepartureMinute = 1440; person.NextEventMinute = 1470; person.ActivityUntilMinute = 1600;
        person.Journey = new Route { Legs = new() { new() { From = city.Places.Single(p=>p.Id==workplace).Position,
            To = new(0,0,0), Minutes = 30, Mode = TravelMode.Walk } } };
        var route = person.Journey;
        var simulator = new CitySimulator(city, new Routes()); simulator.AdvanceTo(1460);
        Assert.Same(route, person.Journey); Assert.Equal(workplace, person.DayPlaceId); Assert.Equal(0, person.LastEmploymentCheckDay);
        var saved = SaveCodec.Write(city); var read = SaveCodec.Read(saved); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        simulator = new CitySimulator(read.State!, new Routes()); simulator.AdvanceTo(1470, 1);
        person = simulator.State.Citizens.Single();
        Assert.Equal("worker", person.Identity.Id); Assert.Null(person.Journey); Assert.Equal("home", person.PlaceId);
        Assert.Equal("", person.DayPlaceId); Assert.Equal(1, person.JobChanges); Assert.Equal(1, person.LastEmploymentCheckDay);
        simulator.AdvanceTo(1500); Assert.Equal(1, person.JobChanges);
    }

    [Fact]
    public void MonthsOfOverdueEmploymentUseOneDayPerBudgetedEvent()
    {
        var city = City(7, 300 * 1440); var person = city.Citizens.Single();
        var simulator = new CitySimulator(city, new Routes());
        Assert.Equal(1, simulator.AdvanceTo(city.Minute, 1).Events);
        Assert.Equal(1, person.LastEmploymentCheckDay);
        Assert.Equal(city.Minute + 1, person.NextEventMinute);
        simulator.AdvanceTo(person.NextEventMinute, 1);
        Assert.Equal(2, person.LastEmploymentCheckDay);
        var saved = SaveCodec.Read(SaveCodec.Write(city)); Assert.Equal(SaveReadStatus.Loaded, saved.Status);
        var resumed = new CitySimulator(saved.State!, new Routes()); resumed.AdvanceTo(person.NextEventMinute, 1);
        Assert.Equal(3, resumed.State.Citizens.Single().LastEmploymentCheckDay);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SeekingRequiresCompleteReachableRouteAndBoundsNativeProbes(bool incomplete)
    {
        var city = City(SeedFor(false)); var person = city.Citizens.Single(); person.DayPlaceId = "";
        var routes = new Routes { Reject = !incomplete, Incomplete = incomplete };
        new CitySimulator(city, routes).AdvanceTo(1440, 1);
        Assert.Equal("", person.DayPlaceId); Assert.Equal(0, person.JobChanges);
        Assert.InRange(routes.Calls, 1, CityBalance.WorkplaceRouteProbes); Assert.Equal(1, person.LastEmploymentCheckDay);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FindingUsesDetachedProbeAndPreservesLiveCarAndPreferences(bool ownsExistingCar)
    {
        var city = City(SeedFor(false)); var person = city.Citizens.Single(); person.DayPlaceId = "";
        person.Preferences.AllowTransit = true; person.Preferences.OwnsCar = true;
        if (ownsExistingCar) person.Car = new CarState { Id = "worker:car", ParkedPosition = new(2,0,0) };
        string before = JsonConvert.SerializeObject(new { person.Identity, person.Preferences, person.Car });
        var routes = new Routes { MutateProbe = true };
        new CitySimulator(city, routes).AdvanceTo(1440, 1);
        Assert.NotEqual("", person.DayPlaceId); Assert.Equal(1, person.JobChanges); Assert.Equal(1, routes.Calls);
        Assert.Equal(before, JsonConvert.SerializeObject(new { person.Identity, person.Preferences, person.Car }));
        Assert.Null(person.Journey); Assert.Equal("home", person.PlaceId);
    }

    [Fact]
    public void SeekingDoesNotTakeWorkplaceCapacityAlreadyAssignedToOtherResidents()
    {
        var city = City(SeedFor(false)); var person = city.Citizens.Single(); person.DayPlaceId = "";
        city.Places.Single(p => p.Id == "work-b").Capacity = 0;
        var occupied = JsonConvert.DeserializeObject<CitizenState>(JsonConvert.SerializeObject(person))!;
        occupied.Identity.Id = "other-worker"; occupied.DayPlaceId = "work-a";
        occupied.NextEventMinute = 1500;
        city.Citizens.Add(occupied);
        var routes = new Routes(); new CitySimulator(city, routes).AdvanceTo(1440, 1);
        Assert.Equal("", person.DayPlaceId); Assert.Equal(0, routes.Calls);
    }

    [Theory]
    [InlineData(Persona.Student)] [InlineData(Persona.Retiree)]
    public void StudentsAndRetireesNeverEnterEmploymentTransitions(Persona persona)
    {
        var city = City(SeedFor(true), persona: persona); var person = city.Citizens.Single(); string assignment = person.DayPlaceId;
        var routes = new Routes(); new CitySimulator(city, routes).AdvanceTo(1440, 1);
        Assert.Equal(assignment, person.DayPlaceId); Assert.Equal(0, person.JobChanges); Assert.Equal(0, person.LastEmploymentCheckDay);
        Assert.Equal(0, routes.Calls);
    }
}
