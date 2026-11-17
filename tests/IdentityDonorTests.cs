using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class IdentityDonorTests
{
    private const double Now = 1440 + 600;
    private static readonly Position Home = new(40, 0, 0), Work = new(100, 0, 0), FarHome = new(5040, 0, 0), FarWork = new(5100, 0, 0), Nowhere = new(20000, 0, 0);

    private static CitizenIdentity[] Donors(int count) => Enumerable.Range(0, count).Select(i => new CitizenIdentity
    {
        Id = "source:" + i.ToString("D5"), Name = "Given" + i + " Family" + i, District = "district" + i % 3,
        Age = 18 + i * 7 % 64, Female = i % 2 == 0, AppearanceSeed = 100 + i,
        Persona = i % 4 == 0 ? Persona.Student : i % 4 == 1 ? Persona.Parent : i % 4 == 2 ? Persona.Retiree : Persona.Commuter
    }).ToArray();

    private static CityState HousedCity()
    {
        var places = new List<Place>
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 400, Position = Home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 100000, Position = Work },
        };
        for (int i = 0; i < 3; i++)
            places.Add(new Place { Id = "shop:" + i, Name = "Shop " + i, Kind = PlaceKind.Shop, Capacity = 16, Position = new(60 + i, 0, 0), BusinessType = "ba:businesstype_coffeeshop" });
        var city = Population.CreateFromHousing(Donors(40), places, "donors", "0.40.4", 11, Now);
        PortalPlaces.Add(city, VisitorRoutineTests.Stations);
        city.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            RepresentationFactor = 2,
            Shops = Enumerable.Range(0, 3).Select(i => new FoodScope { PlaceId = "shop:" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var market = new VisitorRoutineTests.Market
        {
            Shops = city.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, Name = s.PlaceId, MealsPerHour = 30,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(250), Units = 1000 } } }).ToArray()
        };
        new CitySimulator(city, new FreshCityTraceTests.Walk()).AttachFoodMarket(market);
        return city;
    }

    private static string Family(string name) { name = name.Trim(); int space = name.LastIndexOf(' '); return space >= 0 ? name.Substring(space + 1) : name; }

    [Fact]
    public void EveryPersonsDonorReproducesTheIdentityTheyWereGiven()
    {
        var city = HousedCity();
        var sources = city.SourceIdentities.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var donors = IdentityDonors.For(city);
        var kinds = new Dictionary<DonorKind, int>();
        foreach (var person in city.Citizens.Concat<CitizenState>(city.VisitorRoutines))
        {
            var donor = donors.Of(person);
            kinds[donor.Kind] = kinds.TryGetValue(donor.Kind, out int n) ? n + 1 : 1;
            Assert.Equal(IdentityPolicy.CurrentVersion, donor.PolicyVersion);
            var look = sources[donor.AppearanceId];
            Assert.Equal(look.AppearanceSeed, person.Identity.AppearanceSeed);
            Assert.Equal(look.Age, person.Identity.Age);
            Assert.Equal(look.Female, person.Identity.Female);
            Assert.Equal(sources[donor.PersonaId].Persona, person.Identity.Persona);
            Assert.EndsWith(" " + Family(sources[donor.SurnameId].Name), " " + person.Identity.Name);
            if (donor.Kind == DonorKind.Imported)
            {
                Assert.Equal(person.Identity.Id, donor.AppearanceId);
                Assert.Equal(person.Identity.Id, donor.SurnameId);
                Assert.Equal(person.Identity.Id, donor.PersonaId);
            }
            if (donor.Kind == DonorKind.Remixed) Assert.Equal(look.District, person.Identity.District);
        }
        Assert.False(kinds.ContainsKey(DonorKind.None));
        Assert.True(kinds[DonorKind.Imported] > 0 && kinds[DonorKind.Remixed] > 0 && kinds[DonorKind.Visitor] == city.VisitorRoutines.Count && city.VisitorRoutines.Count > 0, string.Join(",", kinds));
        for (int sequence = 1; sequence <= 2000; sequence++)
        {
            var remixed = ResidentLifecycle.RemixIdentity(city, sequence);
            var donor = donors.Of(new CitizenState { Identity = remixed });
            Assert.Equal(DonorKind.Remixed, donor.Kind);
            Assert.Equal(sources[donor.AppearanceId].AppearanceSeed, remixed.AppearanceSeed);
            Assert.Equal(sources[donor.AppearanceId].Age, remixed.Age);
            Assert.Equal(sources[donor.PersonaId].Persona, remixed.Persona);
            Assert.Equal(Family(sources[donor.SurnameId].Name), Family(remixed.Name));
        }
    }

    [Fact]
    public void DonorsAreTheSameAfterASaveRoundTripAndAreNotSaved()
    {
        var city = HousedCity();
        var donors = IdentityDonors.For(city);
        string written = SaveCodec.Write(city);
        var read = SaveCodec.Read(written);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var restored = read.State!;
        var again = IdentityDonors.For(restored);
        var before = city.Citizens.Concat<CitizenState>(city.VisitorRoutines).ToDictionary(p => p.Identity.Id, donors.Of, StringComparer.Ordinal);
        var after = restored.Citizens.Concat<CitizenState>(restored.VisitorRoutines).ToDictionary(p => p.Identity.Id, again.Of, StringComparer.Ordinal);
        Assert.Equal(before.Count, after.Count);
        foreach (var (id, donor) in before) Assert.Equal(donor, after[id]);
        Assert.Equal(written, SaveCodec.Write(restored));
        Assert.DoesNotContain("DonorId", written, StringComparison.Ordinal);
        Assert.Equal(1, CityState.CurrentSchema);
    }

    [Fact]
    public void ACityWithoutSourcesGivesNoDonor()
    {
        var city = HousedCity();
        city.SourceIdentities = new List<CitizenIdentity>();
        var donor = IdentityDonors.For(city).Of(city.Citizens[0]);
        Assert.Equal(DonorKind.None, donor.Kind);
        Assert.Equal("", donor.AppearanceId);
        Assert.Equal("", DonorKinds.Name(donor.Kind));
    }

    [Fact]
    public void ResolvingADonorDoesNotAllocate()
    {
        var city = HousedCity();
        var donors = IdentityDonors.For(city);
        var people = city.Citizens.Concat<CitizenState>(city.VisitorRoutines).ToArray();
        foreach (var person in people) donors.Of(person);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int seen = 0;
        for (int round = 0; round < 10; round++) foreach (var person in people) seen += donors.Of(person).AppearanceId.Length;
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(seen > 0);
    }

    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => PhysicalWalkerFollowTests.Legs(a.Position, b.Position);
    }

    private static CityState WalkingCity()
    {
        var ids = new List<CitizenIdentity>
        {
            new() { Id = "a", Name = "Ann A", Age = 30, AppearanceSeed = 3 },
            new() { Id = "b", Name = "Ben B", Age = 40, AppearanceSeed = 5 },
        };
        var state = Population.Create(ids, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2, Position = Home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 2, Position = Work },
            new Place { Id = "farhome", Name = "Far home", Kind = PlaceKind.Home, Capacity = 2, Position = FarHome },
            new Place { Id = "farwork", Name = "Far work", Kind = PlaceKind.Work, Capacity = 2, Position = FarWork } }, "donorwalk", "0.40.4", 3, Now);
        foreach (var c in state.Citizens)
        {
            bool far = c.Identity.Id == "b";
            c.HomeId = far ? "farhome" : "home"; c.DayPlaceId = far ? "farwork" : "work"; c.MealOuting = null;
            c.PlaceId = c.HomeId; c.Phase = CitizenPhase.Travelling; c.DestinationId = c.DayPlaceId; c.DestinationActivity = Activity.Work;
            c.Journey = PhysicalWalkerFollowTests.Legs(far ? FarHome : Home, far ? FarWork : Work);
            c.DepartureMinute = Now; c.NextEventMinute = Now + c.Journey.Minutes; c.ActivityUntilMinute = Now + 300;
        }
        return state;
    }

    private sealed class Walkers : IDisposable
    {
        internal readonly CitySimulator City;
        internal readonly PhysicalPopulation Bodies;
        internal PhysicalPedestrians Pool => PhysicalPedestrians.Last!;
        internal Walkers(CityState state, int limit)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            PhysicalPedestrians.VisiblePoint = null;
            NativeTravelPace.Focus = Work; Time.Next(3);
            City = new CitySimulator(state, new Walk());
            Bodies = new(new Transform { Component = new PopulationRuntime() }, City, new MultimodalRoutes(), typeof(Walkers).Assembly,
                new VendorBodyTests.Log(), limit, 3, 100, new DwellSpots());
        }
        internal void Settle(Position focus)
        {
            NativeTravelPace.Focus = focus;
            for (int i = 0; i < 4; i++) { Time.Next(2.1f); Bodies.Update(); }
        }
        internal Actor Body(string id) => Pool.Active[id].Character;
        public void Dispose() => Bodies.Dispose();
    }

    private static (string Donor, string Surname, string Persona, string Kind, int Version) Read(Actor actor)
        => (actor.DonorId, actor.SurnameDonorId, actor.PersonaDonorId, actor.DonorKind, actor.IdentityPolicyVersion);

    [Fact]
    public void ABodyShowsTheSameDonorAcrossReleaseReacquireAndReload()
    {
        var state = WalkingCity();
        var expected = IdentityDonors.For(state).Of(state.Citizens.Single(c => c.Identity.Id == "a"));
        Assert.Equal(DonorKind.Imported, expected.Kind);
        (string, string, string, string, int) shown;
        using (var w = new Walkers(state, 4))
        {
            w.Settle(Work);
            var actor = w.Body("a");
            shown = Read(actor);
            Assert.Equal(("a", expected.AppearanceId, expected.SurnameId, expected.PersonaId, "imported", IdentityPolicy.CurrentVersion), (actor.ResidentId, shown.Item1, shown.Item2, shown.Item3, shown.Item4, shown.Item5));
            w.Settle(Nowhere);
            Assert.Empty(w.Pool.Active);
            Assert.Equal("", actor.DonorId); Assert.Equal("", actor.ResidentId); Assert.Equal(0, actor.IdentityPolicyVersion);
            w.Settle(Work);
            Assert.Equal(shown, Read(w.Body("a")));
        }
        var read = SaveCodec.Read(SaveCodec.Write(state));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        using (var reloaded = new Walkers(read.State!, 4))
        {
            reloaded.Settle(Work);
            Assert.Equal(shown, Read(reloaded.Body("a")));
        }
    }

    [Fact]
    public void APooledBodyNeverKeepsThePreviousResidentsDonor()
    {
        var state = WalkingCity();
        var donors = IdentityDonors.For(state);
        var b = donors.Of(state.Citizens.Single(c => c.Identity.Id == "b"));
        using var w = new Walkers(state, 1);
        w.Settle(Work);
        var actor = w.Body("a");
        Assert.Equal("a", actor.DonorId);
        w.Settle(FarWork);
        Assert.False(w.Pool.Active.ContainsKey("a"));
        Assert.Same(actor, w.Body("b"));
        Assert.Equal("b", actor.ResidentId);
        Assert.Equal(b.AppearanceId, actor.DonorId);
        Assert.Equal(b.SurnameId, actor.SurnameDonorId);
        Assert.Equal(b.PersonaId, actor.PersonaDonorId);
    }
}
