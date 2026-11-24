using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodAwarenessStopBindingTests
{
    private sealed class Nested : IFoodAwarenessProvider
    {
        public CitySimulator? City;
        public FoodAwarenessDump? Inner;
        private bool inside;
        public string? Reference(string consumer, int day, int slot)
        {
            if (consumer == "r" && !inside && Inner == null)
            {
                inside = true;
                try { Inner = City!.InspectFoodAwareness("q"); }
                finally { inside = false; }
            }
            return null;
        }
    }

    [Fact]
    public void AProviderThatInspectsAnotherResidentAtAnotherStopKeepsTheOuterShortlistAtItsOwnStop()
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } };
        places.AddRange(Enumerable.Range(0, 2).Select(i => new Place { Id = "s" + i, Name = "Shop " + i, Kind = PlaceKind.Shop, Capacity = 4, Position = new(10 + i * 10, 0, 0) }));
        var state = Population.Create(new[] { new CitizenIdentity { Id = "r", Name = "Outer", Age = 30, Persona = Persona.Foodie },
            new CitizenIdentity { Id = "q", Name = "Inner", Age = 31, Persona = Persona.Foodie } }, places, "weighted", "0.40.4", 79, 1440);
        state.Capabilities.Active = true;
        state.Food = new FoodCommerceState { ChoiceStartsDay = 1, AwarenessStartsDay = 1, RepresentationFactor = 1,
            Shops = places.Skip(1).Select((p, i) => new FoodScope { PlaceId = p.Id, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var market = new WeightedFoodChoiceTests.Market { Shops = state.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, Name = s.PlaceId, MealsPerHour = 60,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100 } } }).ToArray() };
        foreach (var (id, place) in new[] { ("r", "s0"), ("q", "s1") })
        {
            var person = state.Citizens.Single(c => c.Identity.Id == id);
            person.Phase = CitizenPhase.AtPlace; person.Activity = Activity.Shop; person.PlaceId = place;
            person.NextEventMinute = person.ActivityUntilMinute = 2870;
        }
        var provider = new Nested();
        var city = new CitySimulator(state, new WeightedFoodChoiceTests.Planner());
        city.AttachFoodMarket(market, LegacyFoodChoice.Settings(), awarenessSettings: new FoodAwarenessSettings { RadiusMetres = 400, DrawsPerAnchor = 4, DiscoveryProbability = 1 }, memory: provider);
        provider.City = city;
        var outer = city.InspectFoodAwareness("r")!;
        Assert.NotNull(provider.Inner);
        Assert.Equal(new[] { "b1" }, provider.Inner!.Image.Union.Where(c => c.Eligible).Select(c => c.BusinessId));
        Assert.Contains(outer.Image.Union, c => c.BusinessId == "b1");
        Assert.Equal(new[] { "b0" }, outer.Image.Union.Where(c => c.Eligible).Select(c => c.BusinessId));
        Assert.Equal(new[] { "b0" }, outer.Shortlist);
    }
}
