using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodMemoryTests
{
    private static RegularsProjection Projection(FoodCommerceState food, int day = 20, FoodMemorySettings? settings = null)
    {
        settings ??= new();
        return new(food, day, (m, e, d) => FoodMemoryPolicy.EffectiveAffinity(e, d, m.RepresentedTraits ?? Traits(4), settings), settings);
    }

    private static FoodCommerceState RegularFixture(int served = 3, int affinity = 8, int experienceDay = 20)
        => new() { Shops = { new() { BusinessId = "own", PlaceId = "door" } },
            BusinessReferences = { new() { Slot = 0, BusinessId = "own", PlaceId = "door" } },
            Residents = { new() { ResidentId = "physical", Memory = new() { Entries = {
                new() { BusinessRef = 0, Served = served, Affinity = affinity, Day = experienceDay, Outcome = ShopExperience.Served } } } } } };

    [Theory]
    [InlineData(2, 8, 20, 0)] [InlineData(3, 8, 20, 1)] [InlineData(4, 8, 20, 1)]
    [InlineData(3, 8, 6, 1)] [InlineData(3, 8, 5, 0)] [InlineData(3, 8, 21, 0)]
    [InlineData(3, 0, 20, 0)] [InlineData(3, -1, 20, 0)]
    [InlineData(3, 1, 16, 0)] [InlineData(3, 2, 16, 1)]
    public void RegularsUseRetainedServedRecencyAndLazyAffinity(int served, int affinity, int day, int expected)
    {
        var food = RegularFixture(served, affinity, day);
        string before = MemoryWire.Encode(food.Residents[0].Memory);
        Assert.Equal(expected, Projection(food).CountOwnShop("own"));
        Assert.Equal(before, MemoryWire.Encode(food.Residents[0].Memory));
    }

    [Fact]
    public void RegularsCountStablePhysicalAndRepresentedConsumersOnceAndFilterGenerations()
    {
        var food = RegularFixture();
        var represented = new ResidentMeal { ResidentId = FoodRepresentation.ConsumerId("physical", 1), AnchorId = "physical",
            Slot = 1, RepresentedTraits = Traits(4), Memory = MemoryWire.Decode(MemoryWire.Encode(food.Residents[0].Memory)) };
        food.Residents.Add(represented);
        food.Residents.Add(represented); // duplicate observation cannot invent another person
        food.Residents[0].Purchases = 1000; food.Residents[0].ContextAttempts = 4;
        Assert.Equal(2, Projection(food).CountOwnShop("own"));
        food.Shops[0].BusinessId = "new-generation";
        Assert.Equal(0, Projection(food).CountOwnShop("new-generation"));
        Assert.Null(Projection(food).OwnShopText("own"));
        Assert.Null(Projection(food).OwnShopText("rival"));
    }

    [Fact]
    public void RegularsUseEachConsumersTraitsForDecayAndNoLongerCountEvictedMemory()
    {
        var food = RegularFixture(3, 1, 18);
        food.Residents[0].RepresentedTraits = Traits(0);
        Assert.Equal(0, Projection(food).CountOwnShop("own"));
        food.Residents[0].RepresentedTraits = Traits(4);
        Assert.Equal(1, Projection(food).CountOwnShop("own"));
        var meal = food.Residents[0]; var store = new FoodMemoryStore(food);
        meal.BusinessId = "different"; meal.PlaceId = "other-door"; meal.Day = 20; meal.Kind = MealKind.Lunch; meal.ContextAttempts = 1;
        Visit(store, meal, ShopExperience.Served, 20, new() { FoodMemoryCapacity = 1 }, Traits(4));
        Assert.Equal("different", store.Identity(meal.Memory.Entries.Single().BusinessRef));
        Assert.Equal(0, Projection(food).CountOwnShop("own"));
    }

    [Fact]
    public void RegularsAreLazyAndAllShopLookupsShareOneConsumerPass()
    {
        var food = RegularFixture(); int reads = 0;
        food.Shops.Add(new() { BusinessId = "second", PlaceId = "other" });
        var projection = new RegularsProjection(food, 20, (m, e, d) => { reads++; return e.Affinity; }, new());
        Assert.Equal(0, reads);
        Assert.Null(projection.OwnShopText("rival")); Assert.Equal(0, reads);
        Assert.Equal("Regulars: 1", projection.OwnShopText("own"));
        for (int i = 0; i < 100; i++) { Assert.Equal(1, projection.CountOwnShop("own")); Assert.Equal(0, projection.CountOwnShop("second")); }
        Assert.Equal(1, reads);
        Assert.Null(new RegularsProjection(food, 20, (m, e, d) => throw new Exception("Not owned"), new(), _ => false).OwnShopText("own"));
    }

    [Theory]
    [InlineData("-5", 2, 1)] [InlineData("100", 10, 28)] [InlineData("null", 3, 14)]
    [InlineData("1.5", 3, 14)] [InlineData("\"bad\"", 3, 14)] [InlineData("\"4\"", 4, 4)]
    public void RegularSettingsAreFlatClampedAndLogOnce(string value, int visits, int days)
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodMemoryRegularServedVisits\":" + value
            + ",\"FoodMemoryRegularRecentDays\":" + value + "}")!;
        var logs = new List<string>(); settings.FoodMemory.Clamp(logs.Add);
        Assert.Equal(visits, settings.FoodMemoryRegularServedVisits); Assert.Equal(days, settings.FoodMemoryRegularRecentDays);
        Assert.Equal(value == "\"4\"" ? 0 : 2, logs.Count);
        settings.FoodMemory.Clamp(logs.Add); Assert.Equal(value == "\"4\"" ? 0 : 2, logs.Count);
    }

    [Fact]
    public void ProjectionSettingsChangeOnlyProjectionAndNeverChoicesRoutesOrSavedBytes()
    {
        var (a, ma, pa, sa) = Ready(3, 8); var (b, mb, pb, sb) = Ready(3, 8);
        sa.FoodMemoryRegularServedVisits = 2; sa.FoodMemoryRegularRecentDays = 28;
        sb.FoodMemoryRegularServedVisits = 10; sb.FoodMemoryRegularRecentDays = 1;
        foreach (var city in new[] { a, b })
        {
            var meal = city.State.Food!.Residents[0];
            var store = new FoodMemoryStore(city.State.Food);
            meal.Day = 0; meal.ContextAttempts = 1; meal.BusinessId = "b0"; meal.PlaceId = "s0";
            Visit(store, meal, ShopExperience.Served, 0); meal.Memory.Entries[0].Served = 4; meal.Memory.Entries[0].Affinity = 8;
            meal.Day = 1; meal.ContextAttempts = 0; meal.BusinessId = meal.PlaceId = "";
        }
        a = Reload(a, ma, pa, sa); b = Reload(b, mb, pb, sb);
        string checkpoint = SaveCodec.Write(a.State);
        Assert.Equal(checkpoint, SaveCodec.Write(b.State));
        Assert.Equal(1, a.ProjectRegulars()!.CountOwnShop("b0"));
        Assert.Equal(0, b.ProjectRegulars()!.CountOwnShop("b0"));
        a.Inspect(a.State.Citizens[0].Identity.Id); a.DumpFoodMemory(a.State.Food!.Residents[0].ResidentId);
        Assert.Equal(checkpoint, SaveCodec.Write(a.State)); // exact payload, zero growth
        double at = a.State.Food!.Residents.Max(m => m.DesiredAt) + 20;
        Advance(a, at, 1); Advance(b, at, 128);
        Assert.Equal(pa.Out, pb.Out); Assert.Equal(pa.Back, pb.Back); Assert.Equal(ma.Calls, mb.Calls);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State)); // weights/draws included
        var restored = Reload(a, ma, new(), sa);
        Assert.Equal(a.ProjectRegulars()!.CountOwnShop("b0"), restored.ProjectRegulars()!.CountOwnShop("b0"));
        var replay = new CitySimulator(SaveCodec.Read(checkpoint).State!, new WeightedFoodChoiceTests.Planner());
        replay.AttachFoodMarket(new Market { Source = mb.Source }, memorySettings: sa); Advance(replay, at);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(replay.State));
    }

    [Fact]
    public void PopupUsesOwnConsumerMemoryWordsNativeLabelsAndNoMemoryNumbers()
    {
        var food = RegularFixture(); var meal = food.Residents[0];
        var person = new CitizenState { Identity = new() { Id = meal.ResidentId, Name = "Ada" }, Traits = Traits(0) };
        var city = new CityState { Food = food, Minute = 20 * 1440 };
        var place = new Place { Id = "door", Name = "Bakery", Kind = PlaceKind.Shop };
        ResidentInsight Capture(ResidentMeal m) => ResidentInsight.Capture(city, person, m, _ => place,
            shopName: _ => "Native Bakery");
        var insight = Capture(meal);
        Assert.Contains("a regular at Native Bakery", insight.MemoryWords);
        Assert.Contains("curious, tries new places", insight.MemoryWords);
        Assert.DoesNotContain(string.Join(" ", insight.MemoryWords), char.IsDigit);
        Assert.DoesNotContain("%", string.Join(" ", insight.MemoryWords));
        Assert.Contains("a regular at Native Bakery.", insight.Text());
        meal.Memory.Entries[0].Outcome = ShopExperience.Stockout;
        Assert.Contains("was let down by a stockout at Native Bakery", Capture(meal).MemoryWords);
        meal.Slot = 1; meal.ResidentId = FoodRepresentation.ConsumerId(person.Identity.Id, 1);
        Assert.Empty(Capture(meal).MemoryWords); // never attribute an abstract consumer to the visible person
        meal.Slot = 0; meal.ResidentId = person.Identity.Id; food.Shops[0].BusinessId = "replaced";
        Assert.DoesNotContain(Capture(meal).MemoryWords, w => w.Contains("Bakery"));
    }

    [Fact]
    public void DiagnosticReturnsOwnCountsAndConsumerRegularFlagWithoutMarketOrRouteCalls()
    {
        var (city, market, paths, _) = Ready(2);
        var meal = city.State.Food!.Residents[1]; var store = new FoodMemoryStore(city.State.Food);
        meal.Day = 0; meal.ContextAttempts = 1; meal.BusinessId = "b0"; meal.PlaceId = "s0";
        Visit(store, meal, ShopExperience.Served, 0); meal.Memory.Entries[0].Served = 3; meal.Memory.Entries[0].Affinity = 8;
        meal.Day = 1; meal.ContextAttempts = 0; meal.BusinessId = meal.PlaceId = "";
        // Reattach to rebuild B1's existing generation index after the fixture's direct setup.
        city = Reload(city, market, paths);
        string before = SaveCodec.Write(city.State);
        var dump = JObject.FromObject(city.DumpFoodMemory(meal.ResidentId)!);
        Assert.True((bool)dump["Represented"]!); Assert.True((bool)dump["Entries"]![0]!["Regular"]!);
        Assert.Equal(1, (int)dump["OwnShopRegulars"]![0]!["Regulars"]!);
        var refused = JObject.FromObject(city.DumpFoodMemory(meal.ResidentId, _ => false)!);
        Assert.Empty((JArray)refused["Entries"]!); Assert.Empty((JArray)refused["OwnShopRegulars"]!);
        Assert.Equal(before, SaveCodec.Write(city.State)); Assert.Equal(0, market.Calls); Assert.Equal(0, paths.Out + paths.Back);
    }

    [Fact]
    public void ServedVisitCountsOnceAndRealStockoutCanRemoveRegularStatus()
    {
        var food = RegularFixture(2, 1); var meal = food.Residents[0];
        meal.Day = 20; meal.Kind = MealKind.Lunch; meal.ContextAttempts = 1;
        meal.BusinessId = "own"; meal.PlaceId = "door";
        var store = new FoodMemoryStore(food);
        Visit(store, meal, ShopExperience.Served, 20);
        Assert.Equal(1, Projection(food).CountOwnShop("own"));
        for (int i = 0; i < 10; i++) Assert.False(store.Experience(meal, ShopExperience.Served, 20, Traits(), new()));
        Assert.Equal(3, meal.Memory.Entries.Single().Served);
        meal.ContextAttempts++;
        Visit(store, meal, ShopExperience.Stockout, 20, traits: Traits(0));
        Assert.True(meal.Memory.Entries.Single().Affinity < 0);
        Assert.Equal(0, Projection(food).CountOwnShop("own"));
    }

    [Fact]
    public void RuntimeBuildersRemainBehindOpenUiAndDiagnosticGates()
    {
        // Call-site contracts complement pure behavior tests; Unity cannot run in this headless suite.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Runtime", "FoodEconomyPanel.cs"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string file) => File.ReadAllText(Path.Combine(root!.FullName, file));
        string panel = Read("Runtime/FoodEconomyPanel.cs"), popup = Read("Runtime/ResidentInspector.cs");
        Assert.Contains("if (showEconomy) DrawEconomy();", panel);
        Assert.Contains("Time.unscaledTime >= nextEconomyText", panel);
        Assert.Contains("nextEconomyText = Time.unscaledTime + 3", panel);
        Assert.Contains("if (!ObservedOwnShop(scope)) continue;", panel);
        Assert.DoesNotContain("Customer home districts:", panel);
        Assert.Contains("if (visible && selectedId != null && Time.unscaledTime >= nextRefresh) Refresh(simulation);", popup);
        Assert.DoesNotContain("ResidentMemoryWords", Read("Runtime/PersistentCityMod.cs"));
        Assert.DoesNotContain("RegularsProjection", Read("Core/FoodMemoryStore.cs")); // zero commit maintenance
        Assert.DoesNotContain("ProjectRegulars", Read("Runtime/PersistentCityMod.cs")); // no Update/midnight scan
    }
}
