using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RivalMemoryTests
{
    const string Rival = "business:Test%20Street:1:1:food:rival-owner";
    static readonly ResidentTraits Traits = new() { Bands = "2222422222" };
    static void Advance(CitySimulator city, double at, int budget = 1)
    { while (!city.AdvanceTo(at, budget).CaughtUp) { } }

    static CitySimulator Reload(CitySimulator city, RivalOutflowTests.Market market, WeightedFoodChoiceTests.Planner paths)
    {
        string wire = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(wire); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var next = new CitySimulator(read.State!, paths); next.AttachFoodMarket(market);
        Assert.Equal(wire, SaveCodec.Write(next.State));
        if (next.State.Food!.Residents.All(m => m.Decision == null)) next.InspectFoodAwareness("r");
        return next;
    }

    [Theory]
    [InlineData(1, false)] [InlineData(7, false)] [InlineData(128, false)]
    [InlineData(1, true)] [InlineData(7, true)] [InlineData(128, true)]
    public void ModeledServiceReplaysAcrossEveryContinuationAndReturn(int budget, bool reload)
    {
        var (city, market, paths) = RivalOutflowTests.Ready(representation: 3, finances: true);
        city.State.Capabilities.Active = true;
        double at = city.State.Food!.Residents.Max(m => m.DesiredAt);
        city.InspectFoodAwareness("r");
        for (int i = 0; i < 1000 && city.State.Food!.Residents.Any(m => m.Purchases == 0); i++)
        {
            var result = city.AdvanceTo(Math.Max(at, city.State.Minute), budget);
            if (reload) city = Reload(city, market, paths);
            if (result.CaughtUp) at++;
        }
        foreach (var meal in city.State.Food!.Residents)
        {
            Assert.Equal(1, meal.Purchases);
            var entry = Assert.Single(meal.Memory.Entries);
            Assert.Equal(1, entry.Served); Assert.Equal(ShopExperience.Served, entry.Outcome);
            Assert.Equal(meal.Day, entry.Day); Assert.Equal(ExperienceStage.Served, meal.Memory.Watermark!.Stage);
            Assert.True(HouseholdFinance.CashConserved(meal.Budget!));
        }
        var before = city.State.Food.Residents.Select(m => MemoryWire.Encode(m.Memory)).ToArray();
        city = Reload(city, market, paths); Advance(city, city.State.Minute + 20, budget);
        Assert.Equal(before, city.State.Food!.Residents.Select(m => MemoryWire.Encode(m.Memory)));
        Assert.Equal(0, market.NativeBuys); Assert.InRange(paths.Out + paths.Back, 2, 16);
        Assert.Empty(city.State.Food.History); Assert.Single(city.State.Food.BusinessReferences);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void ReachedClosureAtArrivalOrCompletionIsNegativeOnceThenRecoveryServes(bool completion, bool hours)
    {
        var (city, market, paths) = RivalOutflowTests.Ready(); city.State.Capabilities.Active = true;
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + (completion ? 1 : 0));
        Assert.Equal(completion ? MealStatus.Serving : MealStatus.Travelling, meal.Status);
        market.Observations[meal.BusinessId] = RivalOutflowTests.Observation(meal.BusinessId, meal.PlaceId,
            closed: !hours, opens: hours ? 1439 : 0);
        city = Reload(city, market, paths); Advance(city, city.State.Minute + 7);
        meal = city.State.Food!.Residents.Single();
        var entry = Assert.Single(meal.Memory.Entries);
        Assert.Equal(ShopExperience.Closed, entry.Outcome); Assert.True(entry.Affinity < 0);
        Assert.Equal(0, entry.Served); Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        Assert.Equal(0, meal.Purchases); Assert.Equal(Money.Zero, meal.LegacyExternalFoodSpent);
        string failed = MemoryWire.Encode(meal.Memory);
        city = Reload(city, market, paths); Advance(city, city.State.Minute + 20);
        meal = city.State.Food!.Residents.Single(); Assert.Equal(failed, MemoryWire.Encode(meal.Memory));
        // A subsequent context uses the same generation, not a replacement or second diary.
        var store = new FoodMemoryStore(city.State.Food); meal.ContextAttempts++;
        Assert.True(store.CommitStage(meal, ExperienceStage.Committed));
        Assert.True(store.Experience(meal, ShopExperience.Served, meal.Day, Traits, new()));
        Assert.False(store.Experience(meal, ShopExperience.Served, meal.Day, Traits, new()));
        Assert.Equal(1, Assert.Single(meal.Memory.Entries).Served); Assert.Single(city.State.Food.BusinessReferences);
        Assert.Equal(0, market.NativeBuys);
    }

    [Theory]
    [InlineData("withdraw")] [InlineData("price")] [InlineData("owner")]
    [InlineData("open-hours-change")] [InlineData("funds")] [InlineData("interrupt")]
    public void UncertainOrNoncausalRefusalsRemainNeutral(string change)
    {
        var (city, market, paths) = RivalOutflowTests.Ready(); city.State.Capabilities.Active = true;
        var meal = city.State.Food!.Residents.Single(); Advance(city, meal.DesiredAt + 1);
        Assert.Equal(MealStatus.Serving, meal.Status);
        if (change == "withdraw") market.Observations.Clear();
        else if (change == "funds") meal.DayAllowanceUsed = meal.Allowance;
        else if (change == "interrupt") city.State.Citizens[0].MealOuting!.Returning = true;
        else market.Observations[meal.BusinessId] = RivalOutflowTests.Observation(meal.BusinessId, meal.PlaceId,
            cents: change == "price" ? 101 : 100,
            kind: change == "owner" ? FoodAlternativeKind.Player : FoodAlternativeKind.Rival,
            opens: change == "open-hours-change" ? 1 : 0);
        city = Reload(city, market, paths); Advance(city, city.State.Minute + 7);
        meal = city.State.Food!.Residents.Single(); Assert.Empty(meal.Memory.Entries);
        Assert.InRange(meal.Memory.Watermark!.Stage, ExperienceStage.Committed, ExperienceStage.Reached);
        Assert.Empty(city.State.Food.BusinessReferences); Assert.Equal(0, meal.Purchases); Assert.Equal(0, market.NativeBuys);
    }

    [Fact]
    public void EventBudgetsAndReloadProduceIdenticalFullCityNotJustCounts()
    {
        var (a, ma, pa) = RivalOutflowTests.Ready(representation: 3);
        var (b, mb, pb) = RivalOutflowTests.Ready(representation: 3);
        a.State.Capabilities.Active = b.State.Capabilities.Active = true;
        double at = a.State.Food!.Residents.Max(m => m.DesiredAt) + 30;
        Advance(a, at, 128);
        b.InspectFoodAwareness("r");
        while (!b.AdvanceTo(at, 1).CaughtUp) b = Reload(b, mb, pb);
        Assert.True(a.State.Food!.Outflow!.Reconciles(a.State.Food));
        Assert.Equal(3, a.FreeRivalOutflow()!.Count);
        Assert.All(a.State.Food.Residents, m => Assert.Equal(1, Assert.Single(m.Memory.Entries).Served));
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        Assert.Equal(pa.Out, pb.Out); Assert.Equal(pa.Back, pb.Back);
        Assert.Equal(ma.Reads, mb.Reads);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void InactiveOrPreIntegrationCommitmentsFinishWithEmptyMemory(bool activateAfterCommit)
    {
        var (city, market, paths) = RivalOutflowTests.Ready();
        var meal = city.State.Food!.Residents.Single(); Advance(city, meal.DesiredAt);
        Assert.Null(meal.Memory.Watermark);
        city.State.Capabilities.Active = activateAfterCommit;
        city = Reload(city, market, paths); Advance(city, city.State.Minute + 7);
        meal = city.State.Food!.Residents.Single(); Assert.Equal(1, meal.Purchases);
        Assert.Empty(meal.Memory.Entries); Assert.Null(meal.Memory.Watermark);
    }

    [Fact]
    public void DecliningRivalChoiceForFundedHouseholdNeverRecordsAnExperience()
    {
        int declines = 0;
        for (int seed = 0; seed < 32; seed++)
        {
            var (city, market, _) = RivalOutflowTests.Ready(finances: true);
            city.State.Seed = seed; city.State.Capabilities.Active = true;
            var meal = city.State.Food!.Residents.Single(); meal.Budget!.HomeFoodFundedDay = meal.Day;
            Advance(city, meal.DesiredAt);
            if (meal.Status != MealStatus.Declined) continue;
            declines++;
            Assert.Empty(meal.Memory.Entries); Assert.Null(meal.Memory.Watermark);
            Assert.Equal(0, meal.Purchases); Assert.Equal(0, market.NativeBuys);
            Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
        }
        Assert.True(declines > 0);
    }

    [Fact]
    public void CapturedRivalContributionUsesSamePolicyAndSurvivesChangedTuningReload()
    {
        var (city, market, paths) = RivalOutflowTests.Ready(); city.State.Capabilities.Active = true;
        var meal = city.State.Food!.Residents.Single();
        city.State.Food.BusinessReferences.Add(new() { Slot = 0, BusinessId = "rival0", PlaceId = "s0" });
        meal.Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Affinity = -8, Outcome = ShopExperience.Closed });
        city = Reload(city, market, paths); city.InspectFoodAwareness("r");
        meal = city.State.Food!.Residents.Single(); city.AdvanceTo(meal.DesiredAt, 1);
        var shop = Assert.Single(meal.Decision!.Shops);
        Assert.Equal(FoodMemoryPolicy.Contribution(meal.Memory, 0, 1, city.State.Citizens[0].Traits!, new()), shop.MemoryContribution);
        Assert.True(shop.MemoryContribution < 0);
        string held = SaveCodec.Write(city.State);
        city = Reload(city, market, paths);
        city = new CitySimulator(city.State, paths);
        city.AttachFoodMarket(market, memorySettings: new FoodMemorySettings { FoodMemoryWeight = 0 });
        Assert.Equal(held, SaveCodec.Write(city.State));
        Assert.Equal(shop.MemoryContribution, city.State.Food!.Residents.Single().Decision!.Shops.Single().MemoryContribution);
    }

    [Fact]
    public void Source24AcceptsBoundedModeledScalarButRejectsNonfiniteWithoutChangingShape()
    {
        var (city, _, _) = RivalOutflowTests.Ready(); city.InspectFoodAwareness("r");
        city.AdvanceTo(city.State.Food!.Residents[0].DesiredAt, 1);
        var shop = city.State.Food.Residents[0].Decision!.Shops.Single();
        foreach (double contribution in new[] { -8d, -1d, 0d, 1d, 8d })
        {
            shop.MemoryContribution = contribution;
            Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
        }
        foreach (double contribution in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, -9d, 9d })
        {
            shop.MemoryContribution = contribution;
            Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State));
        }
    }

    [Theory] [InlineData(21)] [InlineData(22)] [InlineData(23)]
    public void OlderSourceValidatorsKeepTheirNeutralOnlyRivalContract(int schema)
    {
        var (city, _, _) = RivalOutflowTests.Ready(); city.InspectFoodAwareness("r");
        city.AdvanceTo(city.State.Food!.Residents[0].DesiredAt, 1);
        city.State.Schema = schema;
        string neutral = SaveCodec.WriteForSchema(city.State, schema);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(neutral).Status);
        var root = JObject.Parse(neutral);
        root["Food"]!["Residents"]![0]![9]!["Shops"]![0]!["MemoryContribution"] = 1;
        string forged = root.ToString(Formatting.None);
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(forged).Status);
        var saved = new System.Collections.Generic.Dictionary<string, string> { [SaveCodec.StateKey] = forged };
        Assert.False(SaveStore.WriteTo(saved, city.State).Success);
        Assert.Equal(forged, saved[SaveCodec.StateKey]);
        city.State.Food.Residents[0].Decision!.Shops[0].MemoryContribution = 1;
        Assert.Throws<SaveValidationException>(() => SaveCodec.WriteForSchema(city.State, schema));
    }

    [Fact]
    public void MixedEvictionDecayLateReplayAndProtectedInsertionShareExistingTuple()
    {
        var food = new FoodCommerceState(); var store = new FoodMemoryStore(food);
        var meal = new ResidentMeal { Day = 1, Kind = MealKind.Lunch, ContextAttempts = 1, PlaceId = "s0" };
        var settings = new FoodMemorySettings();
        void Visit(string id, ShopExperience cause, int day)
        {
            meal.BusinessId = id; meal.Day = day; meal.ContextAttempts = meal.ContextAttempts % 4 + 1;
            Assert.True(store.CommitStage(meal, ExperienceStage.Committed));
            Assert.True(store.Experience(meal, cause, day, Traits, settings));
        }
        for (int i = 0; i < 4; i++) Visit(i % 2 == 0 ? "own" + i : Rival + i, ShopExperience.Served, i + 1);
        string before = JsonConvert.SerializeObject(meal.Memory.Entries); int references = food.BusinessReferences.Count;
        Visit("new-rival", ShopExperience.Closed, 5);
        Assert.Equal(1, store.ProtectedInsertions); Assert.Equal(before, JsonConvert.SerializeObject(meal.Memory.Entries));
        Assert.Equal(references, food.BusinessReferences.Count); Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        Assert.False(store.Experience(meal, ShopExperience.Closed, 5, Traits, settings));
        Visit("new-rival", ShopExperience.Served, 34); // expired entries are evicted first
        Assert.Equal(4, meal.Memory.Entries.Count); Assert.DoesNotContain(meal.Memory.Entries, e => store.Identity(e.BusinessRef) == "own0");
        Assert.Equal(0, FoodMemoryPolicy.EffectiveAffinity(meal.Memory.Entries[0], 34, Traits, settings));
        string wire = MemoryWire.Encode(meal.Memory); Assert.Equal(48, wire.Length);
        meal.Memory = MemoryWire.Decode(wire); store = new FoodMemoryStore(food);
        Assert.False(store.Experience(meal, ShopExperience.Served, 40, Traits, settings)); // late replay after decay
        meal.Day = 3; meal.ContextAttempts = 1; Assert.False(store.CommitStage(meal, ExperienceStage.Committed));
        Assert.Equal(wire, MemoryWire.Encode(meal.Memory));
    }

    [Fact]
    public void RivalWordsAreModeledNeverNativeRegularsAndDoNotMislabelRetiredOwnShop()
    {
        var food = new FoodCommerceState(); var meal = new ResidentMeal();
        food.BusinessReferences.Add(new() { Slot = 0, BusinessId = Rival, PlaceId = "s0" });
        meal.Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Served = 5, Affinity = 8, Outcome = ShopExperience.Served });
        string[] Words() => ResidentMemoryWords.Capture(food, meal, Traits, 1, new(),
            i => food.BusinessReferences.Single(r => r.Slot == i), _ => "Bakery").ToArray();
        Assert.Contains(Words(), w => w.Contains("modeled food purchase") && w.Contains("native service unobserved"));
        Assert.DoesNotContain(Words(), w => w.Contains("regular"));
        meal.Memory.Entries[0].Outcome = ShopExperience.Closed;
        Assert.Contains(Words(), w => w.Contains("closed during a modeled rival visit"));
        food.BusinessReferences[0].BusinessId = "business:Test:1:1:food:player:generation:1";
        Assert.Empty(Words());
    }

    [Fact]
    public void AllocationFreeAdvertisedOpeningMembershipMatchesGeneralPlaceUnion()
    {
        var observation = new FoodAlternativeObservation(Rival, "s0", "Test", 1, 1, "food", "owner", FoodAlternativeKind.Rival,
            true, true, false, null, 0, new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(100)) },
            new[] { new FoodAdvertisedHours(0, 0, 1440), new FoodAdvertisedHours(1, 60, 600),
                new FoodAdvertisedHours(1, 400, 900), new FoodAdvertisedHours(6, 0, 1440) });
        var place = new Place { WeeklyHours = observation.Hours.Select(w => new OpeningWindow { Day = w.Day, FromMinute = w.FromMinute, ToMinute = w.ToMinute }).ToList() };
        for (double at = 0; at < 1440 * 16; at += 17.5)
            Assert.Equal(!DailyRoutine.Open(place, at), RivalFoodMemory.Closed(observation, at));
        foreach (int day in new[] { 0, 1, 2, 7, 8 })
        foreach (int at in new[] { 0, 59, 60, 599, 600, 899, 900, 1439 })
            Assert.Equal(!DailyRoutine.Open(place, day * 1440 + at), RivalFoodMemory.Closed(observation, day * 1440 + at));
    }
}
