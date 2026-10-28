using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodHouseholdTests
{
    private static FoodRoutineCapture Capture(int count = 200, Persona persona = Persona.Commuter)
    {
        var c = new FoodRoutineCapture { CityKey = "households", Seed = 23, StartedAt = 1440, EndsAt = 4 * 1440 };
        for (int i = 0; i < count; i++)
        {
            string id = "resident:" + i;
            c.Residents.Add(new FoodRoutineResident { Identity = new CitizenIdentity { Id = id, Name = id, Persona = persona,
                District = "identity-origin-not-home" }, HomeId = "home", HomeDistrictId = "district" });
            c.Spans.Add(new FoodActivitySpan { ConsumerId = id, PlaceId = "home", Activity = Activity.Home, From = 1440, Until = 4 * 1440 });
        }
        return c;
    }
    private static FoodDistrictBudgetMix Mix(int working, int middle, int upper) => new()
    { DistrictId = "district", WorkingPercent = working, MiddlePercent = middle, UpperPercent = upper, Evidence = "Controlled district composition" };
    private static FoodShadowState Scenario(FoodRoutineCapture? c = null, int price = 1500)
    {
        c ??= Capture(40);
        var plan = FoodRoutinePlanning.Build(c, 1, 3);
        var shop = new FoodBusiness { Id = "shop", Name = "Shop", PlaceId = "shop", MealsPerHour = 60,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = "food", Price = Money.FromCents(price), Units = 10000 } } };
        return FoodDemand.CreateObserved(c.CityKey, "Controlled budget/outside-option comparison", 1, 3, plan.Consumers, new[] { shop },
            plan.Opportunities.Select(i => new FoodTravelQuote { ConsumerId = i.ConsumerId, BusinessId = shop.Id,
                IntentId = i.Id, OriginPlaceId = i.OriginPlaceId, Minutes = 2, ReturnMinutes = 2, Evidence = "Controlled complete route" }), plan.Opportunities);
    }
    private static void Finish(FoodShadowState s, int budget = 128)
    { var e = new FoodDemand(s); while (!e.AdvanceTo(s.EndsAt, budget)) { } }

    [Fact]
    public void HomeDistrictChangesDistributionButRetainsVarietyAndOverlap()
    {
        var lower = Capture(1000); lower.HomeDistrictMixes.Add(Mix(80, 15, 5));
        var higher = Capture(1000); higher.HomeDistrictMixes.Add(Mix(15, 45, 40));
        var a = FoodRoutinePlanning.Build(lower, 1, 3).Consumers; var b = FoodRoutinePlanning.Build(higher, 1, 3).Consumers;
        Assert.True(b.Average(p => p.DailyAllowance.Units) > a.Average(p => p.DailyAllowance.Units));
        Assert.True(a.Select(p => p.DailyAllowance).Distinct().Count() > 500);
        Assert.True(a.Max(p => p.DailyAllowance) > b.Min(p => p.DailyAllowance));
        Assert.All(a.Concat(b), p => Assert.Equal("district", p.HomeDistrictId));
    }

    [Fact]
    public void IdentityOrderNameAndShoppingDestinationDoNotRerollBuyingPower()
    {
        var capture = Capture(); var first = FoodRoutinePlanning.Build(capture, 1, 3);
        capture.Residents.Reverse(); capture.Spans.Reverse();
        foreach (var r in capture.Residents) { r.Identity.Name = "Changed name"; r.Identity.Female = true; r.Identity.Age = 81; r.Identity.District = "changed-origin"; }
        foreach (var s in capture.Spans) { s.PlaceId = "different-work-district"; s.Activity = Activity.Work; }
        var second = FoodRoutinePlanning.Build(capture, 1, 3);
        Assert.Equal(first.Consumers.Select(p => p.DailyAllowance), second.Consumers.Select(p => p.DailyAllowance));
        Assert.Equal(first.Consumers.Select(p => p.HomeDistrictId), second.Consumers.Select(p => p.HomeDistrictId));
    }

    [Fact]
    public void PersonalityChangesOutsidePreferenceWithoutInventingHigherIncome()
    {
        var foodie = FoodRoutinePlanning.Build(Capture(1, Persona.Foodie), 1, 3);
        var student = FoodRoutinePlanning.Build(Capture(1, Persona.Student), 1, 3);
        Assert.Equal(foodie.Consumers[0].DailyAllowance, student.Consumers[0].DailyAllowance);
        Assert.True(foodie.Opportunities[0].OutsideOptionScoreCents > student.Opportunities[0].OutsideOptionScoreCents);
        Assert.Equal(JsonConvert.SerializeObject(foodie), JsonConvert.SerializeObject(FoodRoutinePlanning.Build(Capture(1, Persona.Foodie), 1, 3)));
    }

    [Fact]
    public void RecorderUsesActualHomeDistrictAndDetachesTheDistrictSnapshot()
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "p", Name = "Person", District = "origin", Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, District = "district" } }, "city", "0.40.4", 9, 1440);
        var mix = Mix(70, 22, 8); string before = SaveCodec.Write(city);
        using var recorder = new FoodRoutineRecorder(new CitySimulator(city, new NoRoutes()), new[] { mix });
        mix.UpperPercent = 99;
        var captured = recorder.Finish();
        Assert.Equal("district", captured.Residents.Single().HomeDistrictId); Assert.Equal(8, captured.HomeDistrictMixes.Single().UpperPercent);
        Assert.Equal(before, SaveCodec.Write(city));
    }
    private sealed class NoRoutes : IRoutePlanner
    { public Route? Plan(Place from, Place to, double at, TravelPreferences p) => null; }

    [Fact]
    public void ExplicitControlPreservesCommonAllowanceAndCanDisableOutsideChoices()
    {
        var p = FoodRoutinePlanning.Build(Capture(), 1, 3, 1800, allowOutsideOptions: false);
        Assert.All(p.Consumers, c => Assert.Equal(Money.FromCents(1800), c.DailyAllowance));
        Assert.All(p.Opportunities, i => { Assert.Equal(FoodOutsideOption.None, i.OutsideOption); Assert.Null(i.OutsideOptionScoreCents); });
    }

    [Fact]
    public void HomeAlternativeRequiresBeingAtHomeAndPackedPlansVaryByDay()
    {
        var c = Capture(200); foreach (var s in c.Spans) { s.Activity = Activity.Work; s.PlaceId = "work"; }
        var p = FoodRoutinePlanning.Build(c, 1, 3);
        Assert.DoesNotContain(p.Opportunities, i => i.OutsideOption == FoodOutsideOption.HomePreparation);
        Assert.Contains(p.Opportunities, i => i.OutsideOption == FoodOutsideOption.PackedMeal);
        Assert.Contains(p.Opportunities, i => i.OutsideOption == FoodOutsideOption.None);
        Assert.Contains(p.Opportunities.GroupBy(i => i.ConsumerId), g => g.Select(i => i.OutsideOption).Distinct().Count() > 1);
        c.Spans.Clear(); p = FoodRoutinePlanning.Build(c, 1, 3);
        Assert.All(p.Opportunities, i => Assert.Equal(FoodOutsideOption.None, i.OutsideOption));
    }

    [Fact]
    public void DecliningAnAffordablePurchaseDoesNotConsumeStockMoneyOrFulfillFood()
    {
        var s = Scenario(Capture(1), 500);
        foreach (var i in s.Intents) i.OutsideOptionScoreCents = 1;
        Finish(s); Assert.All(s.Intents, i => { Assert.Equal(FoodIntentStatus.DeclinedPurchase, i.Status);
            Assert.Equal(Money.FromCents(0), i.Spent); Assert.Equal("", i.BusinessId); Assert.Null(i.ArrivalMinute); Assert.Null(i.ReturnBy); });
        Assert.Equal(10000, s.Businesses[0].Offers[0].Units);
        Assert.Equal(FoodShadowCodec.Write(s), FoodShadowCodec.Write(FoodShadowCodec.Read(FoodShadowCodec.Write(s), s.CityKey)));
    }

    [Fact]
    public void UnknownStockRemainsUnresolvedRatherThanClaimingAHomeMeal()
    {
        var s = Scenario(Capture(1)); s.Businesses[0].Offers[0].Units = null;
        Finish(s); Assert.All(s.Intents, i => Assert.Equal(FoodIntentStatus.Unresolved, i.Status));
    }

    [Fact]
    public void QualityCanJustifyBuyingInsteadOfPreparingButCannotExceedBuyingPower()
    {
        var ordinary = Scenario(Capture(1), 1000);
        ordinary.Consumers[0].ValueOfTimeCentsPerMinute = 0; ordinary.Consumers[0].ServiceQualityValueCents = 400;
        ordinary.Consumers[0].DailyAllowance = Money.FromCents(2000);
        foreach (var i in ordinary.Intents) i.OutsideOptionScoreCents = 950;
        ordinary.Businesses[0].CustomerServicePercent = 50;
        var better = FoodShadowCodec.Read(FoodShadowCodec.Write(ordinary), ordinary.CityKey);
        better.Businesses[0].CustomerServicePercent = 100;
        var unaffordable = FoodShadowCodec.Read(FoodShadowCodec.Write(better), better.CityKey);
        unaffordable.Businesses[0].Offers[0].Price = Money.FromCents(2001);
        Finish(ordinary); Finish(better); Finish(unaffordable);
        Assert.All(ordinary.Intents, i => Assert.Equal(FoodIntentStatus.DeclinedPurchase, i.Status));
        Assert.All(better.Intents, i => Assert.Equal(FoodIntentStatus.Fulfilled, i.Status));
        Assert.DoesNotContain(unaffordable.Intents, i => i.Status == FoodIntentStatus.Fulfilled);
    }

    [Fact]
    public void CommittedPurchaseKeepsBudgetAndOutsideExplanationAcrossSave()
    {
        var state = Scenario(Capture(1), 500);
        foreach (var i in state.Intents) i.OutsideOptionScoreCents = 100000;
        var engine = new FoodDemand(state); engine.AdvanceTo(state.Intents[0].RequestedAt + 2);
        Assert.Equal(FoodIntentStatus.WaitingForService, state.Intents[0].Status);
        var copy = FoodShadowCodec.Read(FoodShadowCodec.Write(state), state.CityKey);
        Assert.Equal(state.Consumers[0].BudgetEvidence, copy.Consumers[0].BudgetEvidence);
        Assert.Equal(state.Intents[0].OutsideOptionScoreCents, copy.Intents[0].OutsideOptionScoreCents);
        Finish(state, 1); Finish(copy, 128); Assert.Equal(FoodShadowCodec.Write(state), FoodShadowCodec.Write(copy));
    }

    [Fact]
    public void PriceResponseHasIndividualThresholdsAndNoMinimumShopAllocation()
    {
        var cheap = Scenario(price: 500); var dear = Scenario(price: 2500); var impossible = Scenario(price: 100000);
        Finish(cheap); Finish(dear); Finish(impossible);
        Assert.True(cheap.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled) > dear.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled));
        Assert.DoesNotContain(impossible.Intents, i => i.Status == FoodIntentStatus.Fulfilled);
        Assert.All(impossible.Businesses, b => Assert.Equal(10000, b.Offers[0].Units));
        Assert.Contains(cheap.Intents, i => i.Status == FoodIntentStatus.Fulfilled);
        Assert.Contains(dear.Intents, i => i.Status == FoodIntentStatus.DeclinedPurchase);
    }

    [Fact]
    public void InterruptedMixedPurchasesAndOutsideDecisionsAreCadenceIndependent()
    {
        var a = Scenario(); var b = FoodShadowCodec.Read(FoodShadowCodec.Write(a), a.CityKey);
        var e = new FoodDemand(a); e.AdvanceTo(a.StartedAt + 750, 7);
        a = FoodShadowCodec.Read(FoodShadowCodec.Write(a), a.CityKey); Finish(a, 1); Finish(b, 128);
        Assert.Equal(FoodShadowCodec.Write(a), FoodShadowCodec.Write(b));
        Assert.Equal(10000 - a.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled), a.Businesses[0].Offers[0].Units);
    }

    [Fact]
    public void StrictV3MigrationKeepsOldBuyingPowerAndDoesNotAddOutsideOptions()
    {
        var s = Scenario(Capture(1), 500); foreach (var i in s.Intents) { i.OutsideOption = FoodOutsideOption.None; i.OutsideOptionScoreCents = null; }
        var old = JObject.Parse(FoodShadowCodec.Write(s)); FoodQualityDemandTests.LegacyMoney(old); RemoveV4Fields(old); old["Schema"] = 3;
        var migrated = FoodShadowCodec.Read(old.ToString(), s.CityKey);
        Assert.Equal(s.Consumers[0].DailyAllowance, migrated.Consumers[0].DailyAllowance);
        Assert.All(migrated.Intents, i => Assert.Equal(FoodOutsideOption.None, i.OutsideOption)); Finish(migrated); Finish(s);
        Assert.Equal(s.Intents.Select(i => (i.Status, i.Spent, i.BusinessId)), migrated.Intents.Select(i => (i.Status, i.Spent, i.BusinessId)));
        old["Intents"]![0]!["OutsideOption"] = 1;
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(old.ToString(), s.CityKey));
    }

    [Fact]
    public void InvalidDistrictMixAndTeleportingHomeAlternativeAreRefused()
    {
        var c = Capture(); c.HomeDistrictMixes.Add(Mix(70, 22, 9)); Assert.Throws<ArgumentException>(() => FoodRoutinePlanning.Build(c, 1, 3));
        var s = Scenario(Capture(1)); s.Intents[0].OriginActivity = Activity.Work;
        Assert.Throws<ArgumentException>(() => FoodShadowCodec.Write(s));
    }

    internal static void RemoveV4Fields(JObject document)
    {
        var names = new[] { "HomeDistrictId", "BudgetEvidence", "OutsideOption", "OutsideOptionScoreCents", "OutsideOptionEvidence" };
        foreach (var p in document.Descendants().OfType<JProperty>().Where(p => names.Contains(p.Name)).ToArray()) p.Remove();
    }
}
