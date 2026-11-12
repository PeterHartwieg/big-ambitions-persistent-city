using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class A5IntegratedAcceptanceTests
{
    private sealed class Weather : IWeatherSource
    {
        public WeatherSample Value;
        public WeatherSample ReadWeather() => Value;
    }

    [Fact]
    public void PopupFormatsEachPlaceOnceAndRefreshesItsAddressOnTheNextCapture()
    {
        var home = new Place { Id = "address:Maple%20Street:12:home", Name = "Home", Kind = PlaceKind.Home };
        var person = new CitizenState { Identity = new() { Id = "r", Name = "Ada" }, HomeId = home.Id, PlaceId = home.Id };
        var city = new CityState { Citizens = { person }, Places = { home } };
        int calls = 0; string shown = "12 Maple Street";
        ResidentInsight Capture() => ResidentInsight.Capture(city, person, null, _ => home,
            formatAddress: (_, _) => { calls++; return shown; });
        var first = Capture(); Assert.Equal(1, calls); Assert.Contains(shown, first.Text());
        shown = "Maple Street 12"; var next = Capture();
        Assert.Equal(2, calls); Assert.Equal(shown, next.Home!.AddressText);
        Assert.Equal("12 Maple Street", first.Home!.AddressText);
        Assert.Equal("Maple Street", next.Home.Street); Assert.Equal(12, next.Home.Number);
    }

    [Fact]
    public void UnresolvedWorkAssignmentStaysUnknownRatherThanClaimingUnemployment()
    {
        var (city, _, _) = WeightedFoodChoiceTests.Ready(79);
        var person = city.State.Citizens[0];
        person.DayPlaceId = "missing-work";
        Assert.Equal("Work or study location unknown", city.Inspect(person.Identity.Id)!.WorkRole);
        person.DayPlaceId = "";
        Assert.Equal("No regular workplace", city.Inspect(person.Identity.Id)!.WorkRole);
    }

    [Fact]
    public void MissingHistoricalShopDoesNotInventAClosure()
    {
        var (city, _, _) = WeightedFoodChoiceTests.Ready(79);
        var meal = city.State.Food!.Residents[0]; meal.Kind = MealKind.Lunch;
        meal.Today = new() { Day = 1, Meals = { new() { Kind = MealKind.Lunch,
            Status = MealOutcomeStatus.Bought, PlaceId = "missing-shop", ProductId = FoodProducts.Croissant } } };
        Assert.Equal("bought croissant at an unknown shop", city.Inspect("r")!.Meals[1].Text);
    }

    [Fact]
    public void InactiveMemoryDoesNotClaimCuriousBehaviourAndActiveMemoryStaysPersonal()
    {
        var (city, market, paths) = WeightedFoodChoiceTests.Ready(79, representation: 2);
        var person = city.State.Citizens[0]; person.Traits = new() { Bands = "2222202222" };
        var food = city.State.Food!;
        food.BusinessReferences.Add(new() { Slot = 0, BusinessId = "b0", PlaceId = "s0" });
        food.Residents[1].Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Served = 3, Affinity = 8, Outcome = ShopExperience.Served });
        city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market);
        city.State.Capabilities.Active = false;
        string before = DevelopmentChain.Write(city.State);
        Assert.Empty(city.Inspect(person.Identity.Id)!.MemoryWords);
        Assert.Equal(before, DevelopmentChain.Write(city.State));
        city.State.Capabilities.Active = true;
        var insight = city.Inspect(person.Identity.Id)!;
        Assert.Equal(new[] { "curious, tries new places" }, insight.MemoryWords);
        food.Residents[0].Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Served = 3, Affinity = 8, Outcome = ShopExperience.Served });
        insight = city.Inspect(person.Identity.Id, shopName: _ => "Native Bakery")!;
        Assert.Contains(insight.MemoryWords, w => w.StartsWith("a regular at ", StringComparison.Ordinal));
        Assert.DoesNotContain(string.Join(" ", insight.Personality.Concat(insight.MemoryWords)), char.IsDigit);
        Assert.All(new[] { "campaign", "research", "rival", "rain" }, word => Assert.DoesNotContain(word, insight.Text(), StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-1, WeatherSample.Rain)] [InlineData(0, WeatherSample.Rain)]
    [InlineData(1, WeatherSample.Rain)] [InlineData(2, WeatherSample.Rain)]
    [InlineData(6, WeatherSample.Rain)] [InlineData(10, WeatherSample.Rain)] [InlineData(17, WeatherSample.Rain)]
    [InlineData(-1, WeatherSample.Dry)] [InlineData(-1, WeatherSample.Unknown)]
    public void ActiveMemoryAwarenessAndInspectorPreserveHeldChoiceAndExactlyOnceContinuation(int reloadAt, WeatherSample weather)
    {
        (CitySimulator city, WeightedFoodChoiceTests.Market market, WeightedFoodChoiceTests.Planner paths) Ready()
        {
            var x = WeightedFoodChoiceTests.Ready(79, 24);
            x.city.State.Capabilities.Active = true;
            x.city.State.Food!.AwarenessStartsDay = 1;
            x.city.State.Food.BusinessReferences.Add(new() { Slot = 0, BusinessId = "b23", PlaceId = "s23" });
            x.city.State.Food.Residents[0].Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Served = 3, Affinity = 8, Outcome = ShopExperience.Served });
            var city = new CitySimulator(x.city.State, x.planner); city.AttachFoodMarket(x.market);
            city.AttachWeather(new Weather { Value = weather });
            return (city, x.market, x.planner);
        }
        var (control, cm, cp) = Ready(); var (candidate, market, paths) = Ready();
        string? expectedDecision = null, actualDecision = null;
        control.FoodDecisionObserved += t => expectedDecision = JsonConvert.SerializeObject(t.Decision);
        candidate.FoodDecisionObserved += t => actualDecision = JsonConvert.SerializeObject(t.Decision);
        double at = control.State.Food!.Residents[0].DesiredAt;
        int units = 0, citizenUnits = 0, continuations = 0, maxShops = 0;
        bool reloaded = false, savedSelection = false;
        while (!candidate.IsCaughtUpTo(at))
        {
            Assert.InRange(units, 0, 255);
            if (reloadAt < 0 ? units == 0 || candidate.State.Food!.Residents[0].Decision != null : units == reloadAt)
            {
                var data = new Dictionary<string, string>();
                Assert.True(SaveSession.Open(data, candidate.State.PopulationKey).WriteTo(data, candidate.State).Success);
                string saved = data[SaveStore.StateKey];
                var read = SaveSession.Open(data, candidate.State.PopulationKey).InitialReadResult;
                Assert.Equal(SaveReadStatus.Loaded, read.Status);
                candidate = new CitySimulator(read.State!, paths); candidate.AttachFoodMarket(market);
                bool captured = candidate.State.Food!.Residents[0].Decision != null;
                candidate.AttachWeather(new Weather { Value = captured ? WeatherSample.Unknown : weather },
                    new WeatherSettings { Enabled = !captured, RainTravelWeightMultiplier = captured ? 2 : 1.3 });
                candidate.FoodDecisionObserved += t => actualDecision = JsonConvert.SerializeObject(t.Decision);
                Assert.Equal(SaveWire.Body(saved), DevelopmentChain.Write(candidate.State)); reloaded = true;
            }
            string before = DevelopmentChain.Write(candidate.State); int routes = paths.Out + paths.Back, buys = market.Attempts;
            var insight = candidate.Inspect("r")!; _ = insight.Text();
            var awareness = candidate.InspectFoodAwareness("r")!;
            Assert.All(awareness.Samples.Where(s => s.Source == FoodAwarenessSource.Campaign), s => Assert.Equal("unknown", s.Result));
            Assert.Equal(before, DevelopmentChain.Write(candidate.State)); Assert.Equal(routes, paths.Out + paths.Back); Assert.Equal(buys, market.Attempts);
            var r = candidate.AdvanceFrameSlice(at, 1);
            units += r.Events; citizenUnits += r.CitizenEvents; continuations += r.FoodContinuationEvents;
            Assert.InRange(paths.Out + paths.Back - routes, 0, 1);
            var held = candidate.State.Food!.Residents[0].Decision;
            if (held != null)
            {
                Assert.Equal(weather, held.Weather);
                Assert.Equal(weather == WeatherSample.Rain ? 1.3 : 1, held.WeatherTravelMultiplier);
                Assert.Equal(weather, candidate.State.Food!.Residents[0].Memory.Weather);
                maxShops = Math.Max(maxShops, held.Shops.Count); Assert.InRange(held.QuotesDone, 0, 16);
                savedSelection |= held.Result == FoodDecisionResult.Selected;
            }
        }
        if (reloadAt <= 2) Assert.True(reloaded);
        Assert.True(savedSelection);
        Assert.InRange(maxShops, 1, 8); Assert.InRange(paths.Out, 1, 8); Assert.InRange(paths.Back, 1, 8);
        Assert.Equal(1, citizenUnits); Assert.Equal(units - 1, continuations);
        while (!control.AdvanceFrameSlice(at, 128).CaughtUp) { }
        Assert.NotNull(expectedDecision); Assert.Equal(expectedDecision, actualDecision);
        Assert.Equal(DevelopmentChain.Write(control.State), DevelopmentChain.Write(candidate.State));
        while (!control.AdvanceTo(at + 40, 128).CaughtUp) { }
        while (!candidate.AdvanceTo(at + 40, 1).CaughtUp) { }
        Assert.Equal(DevelopmentChain.Write(control.State), DevelopmentChain.Write(candidate.State));
        Assert.Equal(cm.Orders, market.Orders); Assert.Single(market.Orders); Assert.Equal(1, market.Attempts);
        Assert.Equal(cp.Out, paths.Out); Assert.Equal(cp.Back, paths.Back);
    }
}

public sealed partial class FoodCommerceTests
{
}
