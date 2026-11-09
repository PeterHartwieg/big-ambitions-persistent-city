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
        string before = DevelopmentChain.Write(city.State);
        foreach (var meal in city.State.Food!.Residents)
        {
            var dump = city.InspectFoodAwareness(meal.ResidentId)!;
            Assert.Equal(new[] { "b23", "", "b21", "" }, dump.Samples.Where(s => s.Source == FoodAwarenessSource.Memory).Select(s => s.BusinessId));
            Assert.All(dump.Image.Union.Where(s => (s.Sources & FoodAwarenessSource.Memory) != 0), s => Assert.True(s.Eligible));
            Assert.Equal(8, dump.Samples.Count(s => s.Source == FoodAwarenessSource.Campaign && s.Result == "unknown"));
        }
        Assert.Equal(before, DevelopmentChain.Write(city.State)); Assert.Equal(0, paths.Out + paths.Back);
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
}
