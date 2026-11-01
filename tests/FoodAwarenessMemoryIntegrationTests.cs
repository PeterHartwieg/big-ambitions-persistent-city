using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodAwarenessMemoryIntegrationTests
{
    private static (CitySimulator city, WeightedFoodChoiceTests.Market market, WeightedFoodChoiceTests.Planner paths) Ready()
    {
        var (original, market, paths) = WeightedFoodChoiceTests.Ready(79, 24, representation: 2);
        var state = original.State;
        state.Capabilities.Active = true;
        state.Food!.AwarenessStartsDay = 1;
        for (int i = 0; i < 4; i++)
            state.Food.BusinessReferences.Add(new BusinessReference { Slot = i, BusinessId = "b" + (23 - i), PlaceId = "s" + (23 - i) });
        foreach (var meal in state.Food.Residents)
            for (int i = 0; i < 4; i++)
                meal.Memory.Entries.Add(new ShopMemory { BusinessRef = i, Day = 1, Served = 3, Affinity = i % 2 == 0 ? 8 : -8,
                    Outcome = i % 2 == 0 ? ShopExperience.Served : ShopExperience.Stockout });
        var city = new CitySimulator(state, paths);
        city.AttachFoodMarket(market, awarenessSettings: new FoodAwarenessSettings { DrawsPerAnchor = 0, DiscoveryProbability = .001 });
        return (city, market, paths);
    }

    [Fact]
    public void SavedB1GenerationsFeedFourSlotsForEachConsumerWithoutWritingOrRevivingExpiredOrReplacedMemory()
    {
        var (city, _, paths) = Ready();
        string before = SaveCodec.Write(city.State);
        foreach (var meal in city.State.Food!.Residents)
        {
            var dump = city.InspectFoodAwareness(meal.ResidentId)!;
            Assert.Equal(new[] { "b23", "", "b21", "" }, dump.Samples.Where(s => s.Source == FoodAwarenessSource.Memory).Select(s => s.BusinessId));
            Assert.All(dump.Image.Union.Where(s => (s.Sources & FoodAwarenessSource.Memory) != 0), s => Assert.True(s.Eligible));
            Assert.Equal(8, dump.Samples.Count(s => s.Source == FoodAwarenessSource.Campaign && s.Result == "unknown"));
        }
        Assert.Equal(before, SaveCodec.Write(city.State)); Assert.Equal(0, paths.Out + paths.Back);
        city.State.Capabilities.Active = false;
        Assert.All(city.InspectFoodAwareness("r")!.Samples.Where(s => s.Source == FoodAwarenessSource.Memory), s => Assert.Empty(s.BusinessId));
        city.State.Capabilities.Active = true;
        Assert.All(city.InspectFoodAwareness("r", 92)!.Samples.Where(s => s.Source == FoodAwarenessSource.Memory), s => Assert.Empty(s.BusinessId));
        city.State.Food.Shops[23].BusinessId = "replacement";
        city.InvalidateFoodAwareness();
        Assert.DoesNotContain(city.InspectFoodAwareness("r")!.Image.Union, s => s.BusinessId == "replacement" && (s.Sources & FoodAwarenessSource.Memory) != 0);
        city.State.Food.Residents[1].Memory.Entries.Clear();
        Assert.All(city.InspectFoodAwareness(city.State.Food.Residents[1].ResidentId)!.Samples.Where(s => s.Source == FoodAwarenessSource.Memory), s => Assert.Empty(s.BusinessId));
        Assert.Contains(city.InspectFoodAwareness("r")!.Image.Union, s => s.BusinessId == "b21" && (s.Sources & FoodAwarenessSource.Memory) != 0);
    }

    [Theory]
    [InlineData(18)] [InlineData(19)] [InlineData(20)] [InlineData(22)] [InlineData(23)]
    public void PopulatedMemoryAndHeldContributionsMigrateWithoutRecapture(int sourceSchema)
    {
        var (control, mc, pc) = Ready();
        var (changed, mm, pm) = Ready();
        if (sourceSchema >= 20)
        {
            control.AttachWeather(new FrameWeatherSource(() => 1, () => true));
            changed.AttachWeather(new FrameWeatherSource(() => 1, () => true));
        }
        double at = changed.State.Food!.Residents.Min(m => m.DesiredAt);
        control.InspectFoodAwareness("r");
        control.AdvanceTo(at, 1);
        changed.InspectFoodAwareness("r");
        changed.AdvanceTo(at, 1);
        var held = Assert.Single(changed.State.Food.Residents, m => m.Decision != null).Decision!;
        Assert.NotNull(held.Awareness); Assert.NotEmpty(held.Shops);
        Assert.Equal(sourceSchema >= 20 ? WeatherSample.Rain : WeatherSample.Unknown, held.Weather);
        Assert.Equal(sourceSchema >= 20 ? 1.3 : 1, held.WeatherTravelMultiplier);
        Assert.All(held.Shops, s => Assert.NotEqual(0, s.MemoryContribution));
        string memory = JsonConvert.SerializeObject(changed.State.Food.Residents.Select(m => m.Memory));
        var root = JObject.Parse(SaveCodec.Write(changed.State));
        if(sourceSchema<22) ((JObject)root["Food"]!).Remove("Campaigns");
        if(sourceSchema<23) {root["Schema"]=sourceSchema;root.Remove("MenuPolicy");}
        if (sourceSchema < 20) root = JObject.Parse(WeatherTests.As19(root.ToString(Formatting.None)));
        bool legacy18 = sourceSchema == 18;
        if (legacy18)
        {
            root["Schema"] = 18;
            ((JObject)root["Food"]!).Remove("Campaigns");
            ((JObject)root["Food"]!).Remove("AwarenessStartsDay");
            foreach (var row in root["Food"]!["Residents"]!)
                if (row[9] is JObject d) d.Remove("Awareness");
        }
        string source = root.ToString(Formatting.None);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(sourceSchema, read.SourceSchema); Assert.Equal(CityState.CurrentSchema, read.State!.Schema);
        Assert.True(read.State.Capabilities.Active);
        Assert.Equal(sourceSchema < 20 ? 2 : 0, read.State.WeatherStartsDay);
        Assert.Equal(memory, JsonConvert.SerializeObject(read.State.Food!.Residents.Select(m => m.Memory)));
        Assert.Equal(4, read.State.Food.BusinessReferences.Count);
        var migrated = Assert.Single(read.State.Food.Residents, m => m.Decision != null).Decision!;
        if (legacy18) held.Awareness = null;
        Assert.Equal(JsonConvert.SerializeObject(held), JsonConvert.SerializeObject(migrated));
        Assert.Equal(legacy18 ? 2 : 1, read.State.Food.AwarenessStartsDay);
        string upgraded = SaveCodec.Write(read.State);
        Assert.Equal(upgraded, SaveCodec.Write(SaveCodec.Read(upgraded).State!));
        changed = new CitySimulator(read.State, pm);
        changed.AttachWeather(new FrameWeatherSource(() => 2, () => false), new WeatherSettings { Enabled = false });
        changed.AttachFoodMarket(mm, memorySettings: new FoodMemorySettings { FoodMemoryWeight = 0 },
            awarenessSettings: new FoodAwarenessSettings { DrawsPerAnchor = 0 });
        Assert.Equal(upgraded, SaveCodec.Write(changed.State));
        if (legacy18) control.State.Food!.AwarenessStartsDay = 2;
        // Compare the held consumer only: later fresh contexts legitimately use the edited settings.
        var heldMeal = changed.State.Food!.Residents.Single(m => m.Decision != null);
        while (heldMeal.Decision != null) changed.AdvanceTo(at, 1);
        while (!control.AdvanceTo(at, 128).CaughtUp) { }
        var expected = control.State.Food!.Residents.Single(m => m.ResidentId == heldMeal.ResidentId);
        Assert.Equal(expected.BusinessId, heldMeal.BusinessId); Assert.Equal(expected.Price, heldMeal.Price);
        Assert.Equal(expected.Reason, heldMeal.Reason);
        Assert.InRange(pm.Out + pm.Back, 0, 16);
    }
}
