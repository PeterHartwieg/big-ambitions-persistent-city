using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodMemoryTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void BadInsertionNeverWinsNewSeedsByEvictingAnotherShopsPositiveMemory(int capacity)
    {
        foreach (int loyalty in new[] { 4, 2, 0 })
        foreach (bool floorBound in new[] { false, true })
        foreach (bool loweredCapacity in new[] { false, true })
        {
            var food = new FoodCommerceState(); var meal = Meal(business: "failed");
            for (int i = 0; i < capacity; i++)
            {
                food.BusinessReferences.Add(new() { Slot = i, BusinessId = "habit" + i, PlaceId = "s0" });
                meal.Memory.Entries.Add(new() { BusinessRef = i, Day = 1, Served = 8, Affinity = 8, Outcome = ShopExperience.Served });
            }
            var store = new FoodMemoryStore(food); var traits = Traits(loyalty);
            var settings = new FoodMemorySettings { FoodMemoryCapacity = loweredCapacity ? 1 : capacity };
            var choice = new FoodChoiceSettings();
            EvaluatedFoodOffer[] Offers() => Enumerable.Range(0, capacity + 1).Select(i => new EvaluatedFoodOffer(
                i == capacity ? "failed" : "habit" + i, FoodProducts.Croissant, Money.FromCents(100),
                floorBound ? 1000000 : 1, 0, 5, 100, null, null, MealKind.Lunch,
                FoodMemoryPolicy.Contribution(meal.Memory, store.Find(i == capacity ? "failed" : "habit" + i), 1, traits, settings))).ToArray();
            var before = Offers(); Visit(store, meal, ShopExperience.Stockout, 1, settings, traits); var after = Offers();
            double Probability(EvaluatedFoodOffer[] offers) => FoodChoicePolicy.Weight(offers[^1], traits, Money.FromCents(1000), choice)
                / offers.Sum(o => FoodChoicePolicy.Weight(o, traits, Money.FromCents(1000), choice));
            Assert.True(Probability(after) <= Probability(before), $"capacity={capacity}, loyalty={loyalty}, floor={floorBound}, lowered={loweredCapacity}");
            int beforeCount = 0, afterCount = 0;
            for (int seed = 0; seed < 4096; seed++)
            {
                var draw = new FoodDrawContext(seed, "r", 1, MealKind.Lunch, "review-eviction", 1);
                bool oldPick = FoodChoicePolicy.Select(before, traits, Money.FromCents(1000), draw, choice, false).Index == capacity;
                bool newPick = FoodChoicePolicy.Select(after, traits, Money.FromCents(1000), draw, choice, false).Index == capacity;
                Assert.False(newPick && !oldPick, "A bad visit gained a returning customer for the same seed.");
                if (oldPick) beforeCount++; if (newPick) afterCount++;
            }
            Assert.True(afterCount <= beforeCount);
            if (loyalty == 4 && !floorBound && !loweredCapacity)
                Assert.Equal(Probability(before), Probability(after)); // Includes the reported capacity-1 1/3 counterexample.
            Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
            string wire = MemoryWire.Encode(meal.Memory); meal.Memory = MemoryWire.Decode(wire);
            Assert.False(store.Experience(meal, ShopExperience.Stockout, 1, traits, settings));
            Assert.Equal(wire, MemoryWire.Encode(meal.Memory));
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void KnownBagStockoutAtArrivalAndExistingStopCommitsOnceAcrossReload(bool existingStop, bool reloadBeforeFailure)
    {
        var (city, market, paths, _) = Ready();
        var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        if (existingStop)
        {
            var person = city.State.Citizens[0]; person.PlaceId = "s0"; person.Activity = Activity.Shop;
            market.Source.Shops[0].MealsPerHour = 0; market.Source.Shops[0].ServiceUnavailableReason = "Out of stock";
            market.Source.Shops[0].Offers[0].Units = 0;
            city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market);
            city.AdvanceFrameSlice(at, 1); // Capture the held image, which has only generic service-unavailable.
        }
        else
        {
            Advance(city, at); Assert.Equal(MealStatus.Travelling, meal.Status);
            market.Source.Shops[0].MealsPerHour = 0; market.Source.Shops[0].ServiceUnavailableReason = "Out of stock";
            market.Source.Shops[0].Offers[0].Units = 0;
        }
        if (reloadBeforeFailure) city = Reload(city, market, paths);
        Advance(city, at + 10, 1); meal = city.State.Food!.Residents[0];
        var entry = Assert.Single(meal.Memory.Entries);
        Assert.Equal(ShopExperience.Stockout, entry.Outcome); Assert.True(entry.Affinity < 0);
        Assert.Equal(0, entry.Served); Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        string memory = MemoryWire.Encode(meal.Memory);
        city = Reload(city, market, paths); Advance(city, at + 20);
        Assert.Equal(memory, MemoryWire.Encode(city.State.Food!.Residents[0].Memory)); Assert.Equal(0, market.Calls);
        if (existingStop) Assert.Equal(0, paths.Out);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnstaffedZeroUnitsStayNeutralAtArrivalAndExistingStop(bool existingStop)
    {
        var (city, market, paths, _) = Ready(); var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        if (existingStop)
        {
            var p = city.State.Citizens[0]; p.PlaceId = "s0"; p.Activity = Activity.Shop;
        }
        else Advance(city, at);
        market.Source.Shops[0].MealsPerHour = 0; market.Source.Shops[0].ServiceUnavailableReason = "No staffed counter";
        market.Source.Shops[0].Offers[0].Units = 0;
        if (existingStop) { city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market); }
        Advance(city, at + 10); Assert.Empty(city.State.Food!.Residents[0].Memory.Entries); Assert.Equal(0, market.Calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnavailableFailureProofKeepsTheOriginalRefusalAndNeutralMemory(bool existingStop)
    {
        var (city, market, paths, _) = Ready(); var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        if (existingStop)
        {
            var person = city.State.Citizens[0]; person.PlaceId = "s0"; person.Activity = Activity.Shop;
        }
        else Advance(city, at);
        market.Source.Shops[0].MealsPerHour = 0; market.Source.Shops[0].ServiceUnavailableReason = "Out of stock";
        market.Source.Shops[0].Offers[0].Units = 0; market.ThrowOnFailureEvidence = true;
        if (existingStop) { city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market); }
        Advance(city, at + 10);
        Assert.Equal(MealStatus.Missed, city.State.Food!.Residents[0].Status);
        Assert.Empty(city.State.Food.Residents[0].Memory.Entries); Assert.Equal(0, market.Calls);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(true, 0)] [InlineData(true, 1)]
    public void PhysicalQueueGrowthStillServesWhenItFitsThePreviouslyCommittedBreak(bool active, double weight)
    {
        var (city, market, paths, settings) = Ready(); city.State.Capabilities.Active = active; settings.FoodMemoryWeight = weight;
        var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        Advance(city, at); Assert.Equal(MealStatus.Travelling, meal.Status);
        city.State.Food.Shops[0].NextServiceAt = at + 20;
        Assert.True(at + 20 + meal.ServiceMinutes + MealRhythm.EatingMinutes(meal.Kind) + meal.ReturnMinutes < meal.Deadline);
        Advance(city, at + 35); Assert.Equal(1, market.Calls); Assert.Equal(1, meal.Purchases);
        Assert.Equal(1, paths.Out); Assert.DoesNotContain(meal.Memory.Entries, e => e.Outcome == ShopExperience.Queue);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnchosenExistingStopKeepsMealFieldsAndOnlyBlamesAQueueThatDecidesFailure(bool breakAlreadyTooShort)
    {
        var (city, market, paths, _) = Ready(); var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        var p = city.State.Citizens[0]; p.PlaceId = "s0"; p.Activity = Activity.Shop;
        if (breakAlreadyTooShort) p.NextEventMinute = p.ActivityUntilMinute = at + 2;
        else market.Source.Shops[0].Offers[0].Units = 0;
        city.State.Food.Shops[0].NextServiceAt = at + 20;
        city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market);
        Advance(city, at + .1); meal = city.State.Food!.Residents[0];
        Assert.Equal("", meal.BusinessId); Assert.Equal("", meal.PlaceId); Assert.Equal("", meal.ProductId);
        if (breakAlreadyTooShort) Assert.Empty(meal.Memory.Entries);
        else Assert.Equal(ShopExperience.Stockout, Assert.Single(meal.Memory.Entries).Outcome);
        Assert.Equal(0, market.Calls); Assert.Equal(0, paths.Out);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
    }
}
