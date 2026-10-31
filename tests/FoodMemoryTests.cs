using System;
using System.Collections.Generic;
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
    private static ResidentTraits Traits(int loyalty = 2, int patience = 2, int pressure = 2)
        => new() { Bands = "2222" + patience + loyalty + "222" + pressure };
    private static ResidentMeal Meal(int context = 1, int day = 1, string business = "generation:1")
        => new() { ResidentId = "r", AnchorId = "r", Day = day, Kind = MealKind.Lunch,
            ContextAttempts = context, BusinessId = business, PlaceId = "s0" };
    private static void Visit(FoodMemoryStore store, ResidentMeal meal, ShopExperience cause, int day, FoodMemorySettings? settings = null, ResidentTraits? traits = null)
    {
        Assert.True(store.CommitStage(meal, ExperienceStage.Committed));
        Assert.True(store.Experience(meal, cause, day, traits ?? Traits(), settings ?? new()));
    }

    [Fact]
    public void TerminalWatermarkBlocksObservationFinishReceiptsAndOlderContexts()
    {
        var state = new FoodCommerceState(); var store = new FoodMemoryStore(state); var meal = Meal();
        Assert.False(store.Experience(meal, ShopExperience.Served, 1, Traits(), new())); // pre-B commitment
        Assert.True(store.CommitStage(meal, ExperienceStage.Committed));
        Assert.False(store.CommitStage(meal, ExperienceStage.Committed));
        Assert.True(store.CommitStage(meal, ExperienceStage.Reached));
        Assert.True(store.Experience(meal, ShopExperience.Served, 1, Traits(), new()));
        string wire = MemoryWire.Encode(meal.Memory);
        meal.Memory = MemoryWire.Decode(wire); store = new FoodMemoryStore(state);
        foreach (var cause in Enum.GetValues<ShopExperience>())
            Assert.False(store.Experience(meal, cause, 2, Traits(), new()));
        Assert.False(store.CommitStage(meal, ExperienceStage.Reached));
        Assert.Equal(wire, MemoryWire.Encode(meal.Memory)); Assert.Equal(1, meal.Memory.Entries.Single().Served);
        meal.ContextAttempts = 2; Visit(store, meal, ShopExperience.Stockout, 1);
        Assert.False(store.Experience(meal, ShopExperience.Served, 1, Traits(), new()));
        meal.ContextAttempts = 1; Assert.False(store.CommitStage(meal, ExperienceStage.Committed));
    }

    [Fact]
    public void SaturatesAndNewGenerationAtSameDoorGetsNoInheritedEvidence()
    {
        var food = new FoodCommerceState(); var store = new FoodMemoryStore(food); var meal = Meal();
        for (int day = 1; day <= 50; day++) { meal.Day = day; Visit(store, meal, ShopExperience.Served, day); }
        Assert.Equal(35, meal.Memory.Entries.Single().Served); Assert.Equal(8, meal.Memory.Entries.Single().Affinity);
        meal.Day++; meal.BusinessId = "generation:2"; Visit(store, meal, ShopExperience.Stockout, meal.Day, traits: Traits(4));
        Assert.Equal(2, food.BusinessReferences.Count); Assert.Equal(2, meal.Memory.Entries.Count);
        Assert.Equal(0, meal.Memory.Entries.Last().Served); Assert.True(meal.Memory.Entries.Last().Affinity < 0);
        Assert.Equal("generation:1", store.Identity(meal.Memory.Entries[0].BusinessRef));
    }

    [Fact]
    public void EvictionTieOrderingIsIndependentOfListAndSlotOrder()
    {
        var s = new FoodMemorySettings(); var traits = Traits();
        ShopMemory E(int affinity, int day) => new() { Affinity = affinity, Day = day };
        Assert.True(FoodMemoryPolicy.CompareEviction(E(8, 0), "z", E(-8, 30), "a", 30, traits, s) < 0); // expired first
        Assert.True(FoodMemoryPolicy.CompareEviction(E(-2, 30), "z", E(1, 30), "a", 30, traits, s) < 0);
        Assert.True(FoodMemoryPolicy.CompareEviction(E(0, 29), "z", E(0, 30), "a", 30, traits, s) < 0);
        Assert.True(FoodMemoryPolicy.CompareEviction(E(0, 30), "a", E(0, 30), "z", 30, traits, s) < 0);
        var food = new FoodCommerceState(); var store = new FoodMemoryStore(food); var meal = Meal();
        foreach (string id in new[] { "z", "a", "y", "x" }) { meal.BusinessId = id; Visit(store, meal, ShopExperience.Served, 1); meal.ContextAttempts++; }
        meal.ContextAttempts = 1; meal.Day = 2; meal.BusinessId = "new"; Visit(store, meal, ShopExperience.Served, 2);
        Assert.DoesNotContain(meal.Memory.Entries, e => store.Identity(e.BusinessRef) == "a");
        Assert.Equal(4, meal.Memory.Entries.Count);
    }

    [Fact]
    public void LazyDecayExpiryAndLowerCapacityNeverTrimOnLoadOrRead()
    {
        var s = new FoodMemorySettings(); var food = new FoodCommerceState(); var store = new FoodMemoryStore(food); var meal = Meal();
        for (int i = 0; i < 4; i++) { meal.BusinessId = "b" + i; meal.ContextAttempts = i + 1; Visit(store, meal, ShopExperience.Served, 1); }
        string wire = MemoryWire.Encode(meal.Memory); s.FoodMemoryCapacity = 1;
        meal.Memory = MemoryWire.Decode(wire); store = new FoodMemoryStore(food);
        var e = meal.Memory.Entries[0]; e.Affinity = 8;
        Assert.Equal(4, FoodMemoryPolicy.EffectiveAffinity(e, 5, Traits(0), s));
        Assert.Equal(7, FoodMemoryPolicy.EffectiveAffinity(e, 5, Traits(4), s));
        Assert.False(FoodMemoryPolicy.Expired(e, 29, s)); Assert.True(FoodMemoryPolicy.Expired(e, 30, s));
        Assert.Equal(4, meal.Memory.Entries.Count);
        meal.Day = 30; meal.ContextAttempts = 1; meal.BusinessId = "new"; Visit(store, meal, ShopExperience.Served, 30, s);
        Assert.Single(meal.Memory.Entries); Assert.Contains(meal.Memory.Entries, e => store.Identity(e.BusinessRef) == "new");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void EveryBadVisitIsMonotoneAcrossAffinityDaysAndConfiguredBounds(int loyalty)
    {
        var traits = Traits(loyalty); var settings = new FoodMemorySettings();
        foreach (var cause in new[] { ShopExperience.Stockout, ShopExperience.Closed, ShopExperience.Queue })
        for (int affinity = -8; affinity <= 8; affinity++)
        foreach (int elapsed in new[] { 0, 1, 3, 7, 28, 29 })
        {
            var food = new FoodCommerceState { BusinessReferences = { new() { BusinessId = "generation:1", PlaceId = "s0", Slot = 0 } } };
            var store = new FoodMemoryStore(food); var meal = Meal(day: elapsed + 1);
            meal.Memory.Entries.Add(new() { BusinessRef = 0, Day = 1, Affinity = affinity, Served = 3, Outcome = ShopExperience.Served });
            double before = FoodMemoryPolicy.Contribution(meal.Memory, 0, elapsed + 1, traits, settings);
            Visit(store, meal, cause, elapsed + 1, settings, traits);
            double after = FoodMemoryPolicy.Contribution(meal.Memory, 0, elapsed + 1, traits, settings);
            Assert.True(after <= before, $"{cause} {affinity} {elapsed} {before} -> {after}");
            if (affinity == 0) Assert.True(after < 0);
            Assert.InRange(Math.Exp(after), settings.FoodMemoryMinimumMultiplier, settings.FoodMemoryMaximumMultiplier);
        }
    }

    [Fact]
    public void TraitSensitivityAndForgivenessKeepStockoutNegative()
    {
        var s = new FoodMemorySettings();
        Assert.True(FoodMemoryPolicy.Penalty(ShopExperience.Queue, Traits(2, 0), s) > FoodMemoryPolicy.Penalty(ShopExperience.Queue, Traits(2, 4), s));
        Assert.True(FoodMemoryPolicy.Penalty(ShopExperience.Closed, Traits(2, 2, 4), s) > FoodMemoryPolicy.Penalty(ShopExperience.Closed, Traits(2, 2, 0), s));
        Assert.True(FoodMemoryPolicy.Penalty(ShopExperience.Stockout, Traits(4), s) < FoodMemoryPolicy.Penalty(ShopExperience.Stockout, Traits(0), s));
        s.FoodMemoryStockPenalty = 0; Assert.True(FoodMemoryPolicy.Penalty(ShopExperience.Stockout, Traits(4), s) > 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(.5)] [InlineData(1)] [InlineData(2)]
    public void FinalEffectiveMultiplierHonorsBoundsAfterStrengthAndKeepsProbabilityFloor(double strength)
    {
        var settings = new FoodMemorySettings { FoodMemoryWeight = strength, FoodMemoryMinimumMultiplier = .1, FoodMemoryMaximumMultiplier = 1.5 };
        foreach (int affinity in new[] { -8, -4, 0, 4, 8 })
        {
            var m = new ConsumerMemory { Entries = { new() { BusinessRef = 0, Day = 1, Affinity = affinity } } };
            double contribution = FoodMemoryPolicy.Contribution(m, 0, 1, Traits(4), settings);
            Assert.InRange(Math.Exp(contribution), .1 - 1e-15, 1.5);
            var offer = new EvaluatedFoodOffer("b", FoodProducts.Croissant, Money.FromCents(100), 999999, 999999, 999999, 1, null, null, MealKind.Lunch, contribution);
            Assert.True(FoodChoicePolicy.Weight(offer, Traits(4), Money.FromCents(100), new()) >= .02);
            if (strength == 0) Assert.Equal(0, contribution);
        }
    }

    [Fact]
    public void LoyalAndCuriousCurvesAcrossSeedsAndNeutralWeightRetainsMemory()
    {
        var s = new FoodMemorySettings(); var m = new ConsumerMemory { Entries = { new() { BusinessRef = 0, Day = 1, Served = 8, Affinity = 8 } } };
        var counts = new int[5];
        for (int band = 0; band <= 4; band++)
        {
            var t = Traits(band); double known = FoodMemoryPolicy.Contribution(m, 0, 1, t, s), novel = FoodMemoryPolicy.Contribution(m, -1, 1, t, s);
            for (int seed = 0; seed < 4096; seed++)
            {
                EvaluatedFoodOffer Offer(string id, double contribution) => new(id, FoodProducts.Croissant, Money.FromCents(100), 1, 0, 5, 100, null, null, MealKind.Lunch, contribution);
                var result = FoodChoicePolicy.Select(new[] { Offer("habit", known), Offer("novel", novel) }, t, Money.FromCents(1000), new(seed, "r", 1, MealKind.Lunch, "curve", 1), new(), false);
                if (result.Index == 0) counts[band]++;
            }
        }
        for (int i = 1; i < counts.Length; i++) Assert.True(counts[i] > counts[i - 1]);
        Assert.True(counts[0] < 2048); Assert.True(counts[4] > 2500);
        s.FoodMemoryWeight = 0; Assert.Equal(0, FoodMemoryPolicy.Contribution(m, 0, 1, Traits(4), s)); Assert.Single(m.Entries);
    }

    private sealed class Market : IFoodMarket, IFoodSaleEvidence, IFoodFailureMarket
    {
        public WeightedFoodChoiceTests.Market Source = new();
        public bool Unknown, Confirmed;
        public bool ThrowOnFailureEvidence;
        public ShopExperience? Refusal;
        public int Calls;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double at) => Source.Shops;
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            Calls++;
            if (Unknown) throw new InvalidOperationException("Unconfirmed");
            return Refusal.HasValue ? new(false, "Named actual failure", Refusal) : new(true, "Confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => Confirmed;
        public FoodBusiness? ReadFailureBusiness(string businessId, string placeId, double at)
            => ThrowOnFailureEvidence ? throw new InvalidOperationException("Failure proof unavailable")
                : Source.Shops.SingleOrDefault(b => b.Id == businessId && b.PlaceId == placeId);
    }
    private static (CitySimulator city, Market market, WeightedFoodChoiceTests.Planner paths, FoodMemorySettings settings) Ready(int factor = 1, int shops = 1)
    {
        var (old, source, paths) = WeightedFoodChoiceTests.Ready(79, shops, representation: factor);
        old.State.Capabilities.Active = true;
        var market = new Market { Source = source }; var settings = new FoodMemorySettings();
        var city = new CitySimulator(old.State, paths); city.AttachFoodMarket(market, memorySettings: settings);
        return (city, market, paths, settings);
    }
    private static void Advance(CitySimulator city, double at, int budget = 128)
    { while (!city.AdvanceFrameSlice(at, budget).CaughtUp) { } }
    private static CitySimulator Reload(CitySimulator city, Market market, WeightedFoodChoiceTests.Planner paths, FoodMemorySettings? settings = null)
    {
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var restored = new CitySimulator(read.State!, paths); restored.AttachFoodMarket(market, memorySettings: settings); return restored;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void SaveAtEveryCaptureQuoteSelectionCommitArrivalAndCheckoutBoundaryReplays(int boundary)
    {
        var (a, ma, pa, _) = Ready(); var (b, mb, pb, _) = Ready();
        double at = a.State.Food!.Residents[0].DesiredAt;
        for (int i = 0; i < boundary; i++) b.AdvanceFrameSlice(at + 10, 1);
        b = Reload(b, mb, pb);
        Advance(a, at + 30); Advance(b, at + 30, 1);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        var meal = b.State.Food!.Residents.Single();
        Assert.Equal(1, meal.Memory.Entries.Single().Served); Assert.Equal(ExperienceStage.Served, meal.Memory.Watermark!.Stage);
        Assert.Equal(1, mb.Calls); Assert.Equal(pa.Out, pb.Out); Assert.Equal(pa.Back, pb.Back);
        string before = SaveCodec.Write(b.State); b.DumpFoodMemory("r"); b.DumpFoodMemory("r"); Assert.Equal(before, SaveCodec.Write(b.State));
    }

    [Theory]
    [InlineData(ShopExperience.Stockout)] [InlineData(ShopExperience.Closed)] [InlineData(ShopExperience.Queue)]
    public void ActualCheckoutFailureCountsOnceAndCandidateRejectionDoesNot(ShopExperience cause)
    {
        var (city, market, paths, _) = Ready(); market.Refusal = cause; double at = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, at + 20, 1); var meal = city.State.Food.Residents[0];
        Assert.Equal(cause, meal.Memory.Entries.Single().Outcome); Assert.True(meal.Memory.Entries.Single().Affinity < 0);
        Assert.Equal(0, meal.Memory.Entries.Single().Served); city = Reload(city, market, paths); Advance(city, at + 30);
        Assert.Single(city.State.Food!.Residents[0].Memory.Entries); Assert.Equal(1, market.Calls);
        var (rejected, candidateMarket, candidatePaths, _) = Ready(); candidateMarket.Source.Shops[0].Offers[0].Units = 0;
        Advance(rejected, at + 20); Assert.Empty(rejected.State.Food!.Residents[0].Memory.Entries); Assert.Equal(0, candidateMarket.Calls); Assert.Equal(0, candidatePaths.Out);
    }

    [Theory]
    [InlineData("closed")] [InlineData("queue")] [InlineData("unknown")] [InlineData("interrupted")]
    public void ChangedEvidenceAfterCommitOnlyCapturesNamedActualServiceFailure(string change)
    {
        var (city, market, _, _) = Ready(); var meal = city.State.Food!.Residents[0]; double at = meal.DesiredAt;
        Advance(city, at); Assert.Equal(MealStatus.Travelling, meal.Status);
        if (change == "closed") market.Source.Shops[0].Hours.Clear();
        if (change == "queue") city.State.Food.Shops[0].NextServiceAt = at + 100;
        if (change == "unknown") market.Source.Shops[0].MealsPerHour = null;
        if (change == "interrupted") meal.Deadline = at; // consumer's expired break, not bad service
        Advance(city, at + 20, 1);
        if (change == "closed" || change == "queue")
        {
            Assert.Equal(change == "closed" ? ShopExperience.Closed : ShopExperience.Queue, meal.Memory.Entries.Single().Outcome);
            Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        }
        else Assert.Empty(meal.Memory.Entries);
        Assert.Equal(0, market.Calls);
    }

    [Fact]
    public void LostRoutesAndUnaffordableCandidatesNeverBecomeBadService()
    {
        foreach (bool unaffordable in new[] { false, true })
        {
            var (city, market, paths, _) = Ready(); var meal = city.State.Food!.Residents[0];
            if (unaffordable) market.Source.Shops[0].Offers[0].Price = meal.Allowance + Money.FromCents(1);
            else paths.Refuse = true;
            Advance(city, meal.DesiredAt + 20);
            Assert.Empty(meal.Memory.Entries); Assert.Null(meal.Memory.Watermark); Assert.Equal(0, market.Calls);
        }
    }

    [Fact]
    public void AlreadyReachedShopFailureRecordsOnceWithoutRoutesOrCheckout()
    {
        var (city, market, paths, _) = Ready();
        var person = city.State.Citizens[0]; person.PlaceId = "s0"; person.Activity = Activity.Shop;
        market.Source.Shops[0].Offers[0].Units = 0;
        city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market);
        var meal = city.State.Food!.Residents[0]; Advance(city, meal.DesiredAt + 10, 1);
        Assert.Equal(ShopExperience.Stockout, meal.Memory.Entries.Single().Outcome);
        Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        Assert.Equal(0, paths.Out); Assert.Equal(0, market.Calls);
        city = Reload(city, market, paths); Advance(city, meal.DesiredAt + 20);
        Assert.Equal(0, city.State.Food!.Residents[0].Memory.Entries.Single().Served);
    }

    [Fact]
    public void UnaffordableExistingStopDoesNotAttributeUnrelatedStockoutToService()
    {
        var (city, market, paths, _) = Ready();
        var person = city.State.Citizens[0]; person.PlaceId = "s0"; person.Activity = Activity.Shop;
        market.Source.Shops[0].Offers[0].Units = 0;
        market.Source.Shops[0].Offers[0].Price = city.State.Food!.Residents[0].Allowance + Money.FromCents(1);
        city = new CitySimulator(city.State, paths); city.AttachFoodMarket(market);
        Advance(city, city.State.Food!.Residents[0].DesiredAt + 10);
        Assert.Empty(city.State.Food.Residents[0].Memory.Entries); Assert.Null(city.State.Food.Residents[0].Memory.Watermark);
        Assert.Equal(0, paths.Out); Assert.Equal(0, market.Calls);
    }

    [Fact]
    public void TableSaturationCountsMissAndKeepsReceiptsAndAllExistingSlots()
    {
        var food = new FoodCommerceState();
        for (int i = 0; i < 20000; i++) food.BusinessReferences.Add(new() { Slot = i, BusinessId = "g" + i, PlaceId = "s0" });
        var store = new FoodMemoryStore(food); var meal = Meal();
        Visit(store, meal, ShopExperience.Served, 1);
        Assert.Equal(1, store.CapacityMisses); Assert.Empty(meal.Memory.Entries);
        Assert.Equal(ExperienceStage.Served, meal.Memory.Watermark!.Stage); Assert.Equal(20000, food.BusinessReferences.Count);
        Assert.False(store.Experience(meal, ShopExperience.Served, 1, Traits(), new())); Assert.Equal(1, store.CapacityMisses);
        meal.ContextAttempts++; meal.BusinessId = "g19999"; Visit(store, meal, ShopExperience.Served, 1);
        Assert.Equal(19999, meal.Memory.Entries.Single().BusinessRef); Assert.Equal(1, store.CapacityMisses);
        Assert.Equal("g0", food.BusinessReferences[0].BusinessId);
    }

    [Fact]
    public void UnknownThenLateReceiptAfterMidnightAppliesOriginalGenerationExactlyOnce()
    {
        var (city, market, paths, _) = Ready(); market.Unknown = true; double at = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, at + 20); var meal = city.State.Food.Residents[0];
        Assert.Equal(MealStatus.Uncertain, meal.Status); Assert.Empty(meal.Memory.Entries);
        string original = meal.BusinessId; int originalDay = (int)(meal.ServiceAt / 1440);
        market.Source.Shops[0].Id = "replacement-generation"; city.State.Food.Shops[0].BusinessId = "replacement-generation";
        city = Reload(city, market, paths); Advance(city, 2880); Assert.Empty(city.State.Food!.Residents[0].Memory.Entries);
        market.Confirmed = true; city = Reload(city, market, paths);
        while (city.State.Food!.Residents[0].Purchases == 0) city.AdvanceFrameSlice(4320, 1);
        meal = city.State.Food!.Residents[0]; Assert.Equal(1, market.Calls); Assert.Equal(1, meal.Purchases);
        Assert.Equal(originalDay, meal.Memory.Entries.Single().Day); Assert.Equal(1, meal.Memory.Entries.Single().Served);
        Assert.Equal(original, city.State.Food.BusinessReferences.Single().BusinessId);
        city = Reload(city, market, paths); Assert.Equal(1, city.State.Food!.Residents[0].Memory.Entries.Single().Served);
    }

    [Fact]
    public void RepresentedConsumersHaveSeparateMemoryAndMoneyAndRouteCountsMatchWeightZero()
    {
        var (a, ma, pa, _) = Ready(3); var (b, mb, pb, settings) = Ready(3); settings.FoodMemoryWeight = 0;
        double at = a.State.Food!.Residents.Max(m => m.DesiredAt) + 30;
        Advance(a, at); Advance(b, at, 1);
        Assert.Equal(3, ma.Calls); Assert.Equal(ma.Calls, mb.Calls); Assert.Equal(pa.Out, pb.Out); Assert.Equal(pa.Back, pb.Back);
        Assert.All(a.State.Food.Residents, m => Assert.Equal(1, m.Memory.Entries.Single().Served));
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
    }

    [Fact]
    public void HeldContributionSurvivesConfigChangesAndFitsB0bAllowance()
    {
        var (city, market, paths, settings) = Ready(1, 8); var meal = city.State.Food!.Residents[0];
        var store = new FoodMemoryStore(city.State.Food);
        // Populate using the actual commit API, then return to an uncaptured next intent.
        meal.Day = 0; meal.ContextAttempts = 1; meal.BusinessId = "b0"; meal.PlaceId = "s0"; Visit(store, meal, ShopExperience.Served, 0);
        meal.Memory.Entries[0].Affinity = 8; meal.Day = 1; meal.ContextAttempts = 0; meal.LastContextKey = ""; meal.BusinessId = meal.PlaceId = "";
        city = Reload(city, market, paths, settings);
        city.AdvanceFrameSlice(meal.DesiredAt, 1); var decision = city.State.Food!.Residents[0].Decision!;
        Assert.Contains(decision.Shops, s => s.MemoryContribution != 0);
        string checkpoint = SaveCodec.Write(city.State);
        var root = JObject.Parse(checkpoint); var d = root["Food"]!["Residents"]![0]![9]!;
        ((JObject)d).Remove("Tuning"); foreach (JObject shop in d["Shops"]!) shop.Remove("MemoryContribution");
        Assert.InRange(checkpoint.Length - root.ToString(Formatting.None).Length, 1, 589);
        var control = Reload(city, market, new(), new()); var edited = Reload(city, market, new(), new() { FoodMemoryWeight = 0, FoodMemoryCapacity = 1 });
        Advance(control, meal.DesiredAt, 1); Advance(edited, meal.DesiredAt, 128);
        Assert.Equal(SaveCodec.Write(control.State), SaveCodec.Write(edited.State));
    }

    [Fact]
    public void ActivationWaitsForNextBoundaryAcrossRepeatedReloadsAndNoFoodCity()
    {
        var (city, market, paths, _) = Ready(); city.State.Capabilities.Active = false;
        Advance(city, 2000); Assert.False(city.State.Capabilities.Active);
        city = Reload(city, market, paths); Advance(city, 2879); Assert.False(city.State.Capabilities.Active);
        city = Reload(city, market, paths); Advance(city, 2880, 1); Assert.True(city.State.Capabilities.Active);
        Assert.All(city.State.Food!.Residents, m => Assert.Empty(m.Memory.Entries));
        city = Reload(city, market, paths); Assert.True(city.State.Capabilities.Active);
    }

    [Theory]
    [InlineData("null", 1)] [InlineData("{}", 1)] [InlineData("true", 1)] [InlineData("\"bad\"", 1)]
    [InlineData("\"NaN\"", 1)] [InlineData("\"Infinity\"", 2)] [InlineData("-100", 0)] [InlineData("\"1.5\"", 1.5)]
    public void FlatSettingsRecoverIndividualValuesAndLogOnce(string value, double expected)
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodMemoryWeight\":" + value + ",\"Enabled\":false}")!;
        var logs = new List<string>(); settings.FoodMemory.Clamp(logs.Add);
        Assert.Equal(expected, settings.FoodMemoryWeight); Assert.False(settings.Enabled);
        int count = logs.Count; settings.FoodMemory.Clamp(logs.Add); Assert.Equal(count, logs.Count);
        Assert.Equal(value == "\"1.5\"" ? 0 : 1, count);
    }

    [Theory]
    [InlineData("null", 4)] [InlineData("1.5", 4)] [InlineData("\"2\"", 2)] [InlineData("99.5", 4)] [InlineData("-1", 1)]
    public void IntegerSettingsUseDefaultsForUnusableValuesAndClampEndpoints(string value, int expected)
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodMemoryCapacity\":" + value + "}")!;
        var logs = new List<string>(); settings.FoodMemory.Clamp(logs.Add); Assert.Equal(expected, settings.FoodMemoryCapacity);
        Assert.Equal(value == "\"2\"" ? 0 : 1, logs.Count);
    }
}
