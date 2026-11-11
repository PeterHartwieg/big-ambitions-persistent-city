using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class HeldFoodTuningTests
{
    private sealed class Paths : IRoutePlanner
    {
        public int Calls;
        public Route Plan(Place a, Place b, double at, TravelPreferences preferences)
        {
            Calls++;
            return new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = 1 } } };
        }
    }
    private sealed class Market : IFoodMarket
    {
        public double EarliestMinute => 0;
        public int Orders;
        public IReadOnlyList<FoodBusiness> Read(double at) => new[] { new FoodBusiness {
            Id = "generation:1", PlaceId = "shop", Name = "Shop", MealsPerHour = 60,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() {
                new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100 },
                new FoodOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(100), Units = 100 } } } };
        public FoodSale Buy(ResidentMeal meal, double at) { Orders++; throw new InvalidOperationException("Must stop before checkout"); }
    }
    private static (CitySimulator city, Paths paths, Market market, FoodChoiceSettings settings) Ready(int representation = 1)
    {
        var places = new List<Place> {
            new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
            new() { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(10, 0, 0) } };
        var state = Population.Create(new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 30, Persona = Persona.Foodie } },
            places, "b1-contract-probe", "0.40.4", 2, 1440);
        var resident = state.Citizens.Single(); resident.NextEventMinute = resident.ActivityUntilMinute = 2870;
        resident.Traits = new ResidentTraits { Bands = "4022222222" };
        state.Food = new FoodCommerceState { RepresentationFactor = representation, Shops = new() { new FoodScope { PlaceId = "shop", BusinessId = "generation:1",
            ProductId = FoodProducts.Croissant, AdditionalProductIds = new() { FoodProducts.Burger } } } };
        // Visitors (K-1 per resident) are persons with their own routines; they arrive through a portal.
        if (representation > 1) PortalPlaces.Add(state, new[] { new PortalStation("subway:test", new Position(3, 0, 1)) });
        var paths = new Paths(); var market = new Market(); var city = new CitySimulator(state, paths);
        var settings = new FoodChoiceSettings { FoodChoiceTasteWeight = 0 }; city.AttachFoodMarket(market, settings);
        state.Food.ChoiceStartsDay = 1;
        return (city, paths, market, settings);
    }
    private static (string product, int routes, int orders, string state) Resume(string wire, double taste, int budget, bool editAll = false)
    {
        var read = DevelopmentChain.Read(wire); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var paths = new Paths(); var market = new Market(); var city = new CitySimulator(read.State!, paths);
        var settings = new FoodChoiceSettings { FoodChoiceTasteWeight = taste };
        if (editAll) {
            foreach (var property in typeof(FoodChoiceSettings).GetProperties().Where(p=>!p.Name.StartsWith("FoodMenu",StringComparison.Ordinal))) property.SetValue(settings, 3.0);
            settings.Clamp();
        }
        city.AttachFoodMarket(market, settings);
        var meal = city.State.Food!.Residents.Single();
        while (!city.AdvanceFrameSlice(meal.DesiredAt, budget).CaughtUp) { }
        Assert.Equal(MealStatus.Travelling, meal.Status); Assert.Equal(1, meal.Day);
        return (meal.ProductId, paths.Calls, market.Orders, DevelopmentChain.Write(city.State));
    }
    private static string Checkpoint(int units)
    {
        var (city, paths, market, _) = Ready(); var meal = city.State.Food!.Residents.Single();
        for (int i = 0; i < units; i++) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.NotNull(meal.Decision); Assert.NotNull(meal.Decision!.Tuning);
        Assert.Equal(units == 3 ? FoodDecisionResult.Selected : FoodDecisionResult.Evaluating, meal.Decision.Result);
        Assert.Equal(Math.Min(units - 1, 2), paths.Calls); Assert.Equal(0, market.Orders);
        Assert.All(meal.Decision.Shops, s => Assert.Equal(0, s.MemoryContribution));
        return DevelopmentChain.Write(city.State);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(1, 128)]
    [InlineData(2, 1)] [InlineData(2, 128)] [InlineData(3, 1)] [InlineData(3, 128)]
    public void EvaluatingQuoteAndSelectedReloadKeepCapturedTuning(int units, int budget)
    {
        string wire = Checkpoint(units);
        var original = Resume(wire, 0, 1); var edited = Resume(wire, 3, budget);
        Assert.Equal(original, edited); Assert.Equal(FoodProducts.Burger, edited.product);
        Assert.Equal(2 - Math.Min(units - 1, 2), edited.routes); Assert.Equal(0, edited.orders);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EditingEveryA3CoefficientKeepsTheHeldResultAndExactState(int units)
        => Assert.Equal(Resume(Checkpoint(units), 0, 1), Resume(Checkpoint(units), 3, 128, editAll: true));

    [Fact]
    public void LiveEditDoesNotMutateImageAndNextUncapturedContextUsesEditTheSameDay()
    {
        var (city, _, _, settings) = Ready(); var meal = city.State.Food!.Residents.Single();
        string uncaptured = DevelopmentChain.Write(city.State);
        city.AdvanceFrameSlice(meal.DesiredAt, 1);
        string image = meal.Decision!.Tuning!.ToWire(), key = meal.Decision.ContextKey;
        settings.FoodChoiceTasteWeight = 3;
        while (!city.AdvanceFrameSlice(meal.DesiredAt, 1).CaughtUp) { }
        Assert.Equal(FoodProducts.Burger, meal.ProductId);
        Assert.Equal(FoodProducts.Burger, Resume(uncaptured, 0, 1).product);
        Assert.Equal(FoodProducts.Croissant, Resume(uncaptured, 3, 128).product);
        // Same logical stay/seed/attempt has the same key under either tuning image.
        var read = DevelopmentChain.Read(uncaptured); var fresh = new CitySimulator(read.State!, new Paths());
        fresh.AttachFoodMarket(new Market(), settings); fresh.AdvanceFrameSlice(meal.DesiredAt, 1);
        var next = fresh.State.Food!.Residents.Single().Decision!;
        Assert.Equal(key, next.ContextKey); Assert.NotEqual(image, next.Tuning!.ToWire());
        Assert.Equal(1, fresh.State.Food.Residents.Single().Day);
    }

    [Fact]
    public void SelectedOldContextsKeepTheirWinnerWithoutNewRoutes()
    {
        var (city, _, _, _) = Ready(); var meal = city.State.Food!.Residents.Single();
        for (int i = 0; i < 3; i++) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.Equal(FoodDecisionResult.Selected, meal.Decision!.Result);
        var root = SaveWire.Parse(DevelopmentChain.Write(city.State)); var d = root["Food"]!["Residents"]![0]![8]!;
        ((JObject)d).Remove("Tuning"); foreach (JObject shop in d["Shops"]!) shop.Remove("MemoryContribution");
        string wire = root.ToString(Formatting.None);
        var resumed = Resume(wire, 3, 128);
        Assert.Equal(FoodProducts.Burger, resumed.product); Assert.Equal(0, resumed.routes); Assert.Equal(0, resumed.orders);
    }

    [Fact]
    public void PreImage18CapturesLiveAtFirstContinuation()
    {
        var (city, _, _, _) = Ready(); var meal = city.State.Food!.Residents.Single(); city.AdvanceFrameSlice(meal.DesiredAt, 1);
        var root = SaveWire.Parse(DevelopmentChain.Write(city.State));
        var d = root["Food"]!["Residents"]![0]![8]!; ((JObject)d).Remove("Tuning");
        foreach (JObject shop in d["Shops"]!) shop.Remove("MemoryContribution");
        string wire = root.ToString(Formatting.None);
        var read = DevelopmentChain.Read(wire); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Null(read.State!.Food!.Residents.Single().Decision!.Tuning);
        Assert.Equal(FoodProducts.Croissant, Resume(wire, 3, 1).product);
        var paths = new Paths(); var restored = new CitySimulator(read.State, paths); var settings = new FoodChoiceSettings { FoodChoiceTasteWeight = 3 };
        restored.AttachFoodMarket(new Market(), settings); restored.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.NotNull(read.State.Food.Residents.Single().Decision!.Tuning);
        settings.FoodChoiceTasteWeight = 0;
        string captured = DevelopmentChain.Write(read.State);
        Assert.Equal(FoodProducts.Croissant, Resume(captured, 0, 128).product);
        Assert.Equal(1, paths.Calls);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(9)]
    public void MalformedImagesAndReservedMemoryRefuseWithoutReplacement(int corruption)
    {
        var root = SaveWire.Parse(Checkpoint(1)); var d = root["Food"]!["Residents"]![0]![8]!;
        string valid = (string)d["Tuning"]!;
        switch (corruption) {
            case 0: d["Tuning"] = valid + "0"; break;
            case 1: d["Tuning"] = "2" + valid.Substring(1); break;
            case 2: d["Tuning"] = valid.ToLowerInvariant(); break;
            case 3: d["Tuning"] = "1" + "7FF8000000000000" + valid.Substring(17); break;
            case 4: d["Tuning"] = "1" + "7FF0000000000000" + valid.Substring(17); break;
            case 5: d["Tuning"] = "1" + "0000000000000000" + valid.Substring(17); break;
            case 6: d["Tuning"] = new JObject(); break;
            case 7: d["Shops"]![0]!["MemoryContribution"] = 8.01; break;
            case 8: d["Shops"]![0]!["MemoryContribution"] = .1; break;
            case 9: d["Shops"]![0]!["MemoryContribution"] = double.NaN; break;
        }
        string source = root.ToString(Formatting.None); var data = new Dictionary<string, string> { [SaveStore.StateKey] = source };
        var session = SaveSession.Open(data, "b1-contract-probe"); Assert.Equal(SaveReadStatus.Invalid, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, Ready().city.State).Success); Assert.Same(source, data[SaveStore.StateKey]);
    }

    [Fact]
    public void NonNeutralOrNonFiniteReservedContributionRefusesWrite()
    {
        var read = DevelopmentChain.Read(Checkpoint(1)); var state = read.State!;
        var shop = state.Food!.Residents.Single().Decision!.Shops.Single();
        foreach (double value in new[] { -8.01, 8.01, double.NaN, double.PositiveInfinity }) {
            shop.MemoryContribution = value; Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(state));
        }
    }

    [Fact]
    public void EvenEmptyShortlistsCannotRetainACityWideTuningHistory()
    {
        var (city, _, _) = WeightedFoodChoiceTests.Ready(2, 1, representation: 2);
        var meals = city.State.Food!.Residents;
        foreach (var meal in meals) {
            meal.ContextAttempts = 1; meal.LastContextKey = "trip:held";
            meal.Decision = new FoodDecisionState { IntentId = meal.IntentId, ContextKey = meal.LastContextKey, Attempt = 1,
                OriginAt = city.State.Minute, Deadline = city.State.Minute + 30, Tuning = FoodDecisionTuning.Capture(new FoodChoiceSettings()) };
        }
        var error = Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city.State));
        Assert.Contains("More than one held", error.Message);
    }

    [Fact]
    public void EightCandidateImageHasFixed405ByteWireCostAndNoNewCollectionRecords()
    {
        var (city, _, _) = WeightedFoodChoiceTests.Ready(2, 8); var meal = city.State.Food!.Residents.Single();
        city.AdvanceFrameSlice(meal.DesiredAt, 1);
        string wire = DevelopmentChain.Write(city.State); var root = SaveWire.Parse(wire); var d = root["Food"]!["Residents"]![0]![8]!;
        ((JObject)d).Remove("Tuning"); foreach (JObject shop in d["Shops"]!) shop.Remove("MemoryContribution");
        string before = root.ToString(Formatting.None);
        Assert.Equal(405, wire.Length - before.Length);
        Assert.Equal(405, System.Text.Encoding.UTF8.GetByteCount(wire) - System.Text.Encoding.UTF8.GetByteCount(before));
        Assert.InRange(wire.Length - before.Length, 1, 65536); // aggregate policy/held-tuning envelope
        var old = DevelopmentChain.Read(before); Assert.Equal(SaveReadStatus.Loaded, old.Status);
        Assert.Equal(SaveCodec.CountRecords(city.State, wire), SaveCodec.CountRecords(old.State!, before));
    }

    [Fact]
    public void ImageStoresAllTwelveExactCoefficientsAndClampsOnlyCapture()
    {
        var settings = LegacyFoodChoice.Settings(); // revision 1: the four-offer menu fields (E5b5 made eight the default)
        var properties = typeof(FoodChoiceSettings).GetProperties().Where(p=>!p.Name.StartsWith("FoodMenu",StringComparison.Ordinal)).ToArray(); Assert.Equal(12, properties.Length);
        foreach (var property in properties) {
            var changed = LegacyFoodChoice.Settings(); property.SetValue(changed, .37); changed.Clamp();
            var capture = FoodDecisionTuning.Capture(changed); string wire = capture.ToWire();
            Assert.Equal(FoodDecisionTuning.WireLength, wire.Length);
            Assert.True(FoodDecisionTuning.TryParse(wire, out var restored)); Assert.Equal(wire, restored!.ToWire());
            Assert.NotEqual(FoodDecisionTuning.Capture(settings).ToWire(), wire);
            property.SetValue(changed, double.NaN); Assert.Equal(wire, capture.ToWire());
        }
    }
}
