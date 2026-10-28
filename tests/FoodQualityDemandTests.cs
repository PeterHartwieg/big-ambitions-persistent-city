using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodQualityDemandTests
{
    private static FoodShadowState Scenario(int qualityValue = 300, int districtValue = 200)
    {
        var person = new FoodConsumer { Id = "p", Name = "Person", OriginPlaceId = "home", DailyAllowanceCents = 1000,
            ServiceQualityValueCents = qualityValue, DistrictAppealValueCents = districtValue };
        var intent = new FoodIntent { Id = FoodDemand.IntentId("p", 1), ConsumerId = "p", Day = 1, DesiredAt = 2160,
            RequestedAt = 2160, Deadline = 2205, EatingMinutes = 15, MaximumQueueMinutes = 15,
            OriginPlaceId = "work", ContextEvidence = "Observed work; controlled break" };
        var shops = new[] { "a", "b" }.Select(id => new FoodBusiness { Id = id, Name = id, PlaceId = id, DistrictId = "district",
            CustomerServicePercent = 50, MealsPerHour = 60,
            Hours = Enumerable.Range(0, 7).Select(day => new OpeningWindow { Day = day, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = "meal", PriceCents = id == "a" ? 600 : 700, Units = 10, DistrictDemandPercent = 50 } } }).ToArray();
        return FoodDemand.CreateObserved("city", "Controlled quality/district comparison", 1, 1, new[] { person }, shops,
            shops.Select(b => new FoodTravelQuote { ConsumerId = "p", BusinessId = b.Id, IntentId = intent.Id, OriginPlaceId = "work",
                Minutes = 5, ReturnMinutes = 5, Evidence = "Controlled complete paths" }), new[] { intent });
    }

    private static FoodShadowState Finish(FoodShadowState state)
    { var engine = new FoodDemand(state); while (!engine.AdvanceTo(state.EndsAt, 1)) { } return state; }

    internal static void LegacyMoney(JObject token)
    {
        foreach (var p in token.Descendants().OfType<JProperty>().Where(p => p.Name == "PriceCents" || p.Name == "SpentCents" || p.Name == "DailyAllowanceCents").ToArray())
            p.Value = checked((int)(decimal)p.Value);
    }

    [Fact]
    public void BetterCustomerServiceCanJustifyAPremiumForResidentsWhoValueIt()
    {
        var state = Scenario(); state.Businesses[0].CustomerServicePercent = 20; state.Businesses[1].CustomerServicePercent = 90;
        var indifferent = FoodShadowCodec.Read(FoodShadowCodec.Write(state), "city"); indifferent.Consumers[0].ServiceQualityValueCents = 0;
        Assert.Equal("a", Finish(indifferent).Intents.Single().BusinessId);
        var intent = Finish(state).Intents.Single();
        Assert.Equal("b", intent.BusinessId); Assert.Equal(700, intent.SpentCents);
        Assert.Equal(9, state.Businesses[1].Offers[0].Units);
        Assert.True(intent.Choices.Single(c => c.BusinessId == "b").ServiceQualityAdjustmentCents < 0);
    }

    [Fact]
    public void DistrictProductAppealCanChangeChoiceWithoutCreatingMealsOrMoney()
    {
        var state = Scenario(); state.Businesses[0].Offers[0].DistrictDemandPercent = 10; state.Businesses[1].Offers[0].DistrictDemandPercent = 90;
        state.Businesses[1].DistrictId = "other-district";
        Assert.Equal("b", Finish(state).Intents.Single().BusinessId);
        Assert.Single(state.Intents); Assert.Equal(1000, state.Consumers[0].DailyAllowanceCents);
        Assert.Equal(19, state.Businesses.Sum(b => b.Offers[0].Units));
    }

    [Fact]
    public void SameDistrictProductSignalDoesNotPenalizeAnAdditionalBranch()
    {
        var high = Scenario(); var low = Scenario();
        foreach (var b in high.Businesses) b.Offers[0].DistrictDemandPercent = 90;
        foreach (var b in low.Businesses) b.Offers[0].DistrictDemandPercent = 20;
        Finish(high); Finish(low);
        Assert.Equal(high.Intents[0].BusinessId, low.Intents[0].BusinessId);
        Assert.Equal(high.Intents[0].SpentCents, low.Intents[0].SpentCents);
        Assert.Equal(high.Intents[0].Status, low.Intents[0].Status);
        Assert.Equal(low.Intents[0].Choices[0].DistrictAppealAdjustmentCents, low.Intents[0].Choices[1].DistrictAppealAdjustmentCents);
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("stock")]
    [InlineData("service")]
    [InlineData("closed")]
    [InlineData("route")]
    public void AttractiveSignalsNeverOverrideFeasibility(string unavailable)
    {
        var state = Scenario(); var b = state.Businesses[1]; b.CustomerServicePercent = 100; b.Offers[0].DistrictDemandPercent = 100;
        if (unavailable == "budget") b.Offers[0].PriceCents = 1001;
        if (unavailable == "stock") b.Offers[0].Units = 0;
        if (unavailable == "service") b.MealsPerHour = 0;
        if (unavailable == "closed") b.Hours.Clear();
        if (unavailable == "route") state.TravelQuotes[1].Minutes = null;
        Assert.Equal("a", Finish(state).Intents.Single().BusinessId);
    }

    [Fact]
    public void UnknownSignalsAddNoAdjustmentAndRemainDifferentFromMeasuredZero()
    {
        var unknown = Scenario(); var zero = Scenario();
        foreach (var b in unknown.Businesses) { b.CustomerServicePercent = null; b.Offers[0].DistrictDemandPercent = null; }
        foreach (var b in zero.Businesses) { b.CustomerServicePercent = 0; b.Offers[0].DistrictDemandPercent = 0; }
        Finish(unknown); Finish(zero);
        Assert.All(unknown.Intents[0].Choices, c => { Assert.Null(c.CustomerServicePercent); Assert.Null(c.DistrictDemandPercent);
            Assert.Equal(0, c.ServiceQualityAdjustmentCents); Assert.Equal(0, c.DistrictAppealAdjustmentCents); });
        Assert.True(zero.Intents[0].Choices[0].ScoreCents > unknown.Intents[0].Choices[0].ScoreCents);
    }

    [Fact]
    public void QueuedQualityChoiceKeepsItsExplanationAcrossSaveAndResume()
    {
        var state = Scenario(); state.Businesses[1].CustomerServicePercent = 100;
        var engine = new FoodDemand(state); engine.AdvanceTo(2165);
        Assert.Equal(FoodIntentStatus.WaitingForService, state.Intents[0].Status);
        var resumed = FoodShadowCodec.Read(FoodShadowCodec.Write(state), "city");
        Finish(resumed); Finish(state);
        Assert.Equal(FoodShadowCodec.Write(state), FoodShadowCodec.Write(resumed));
        Assert.Equal("b", state.Intents[0].BusinessId);
    }

    [Fact]
    public void StrictSchemaTwoMigrationKeepsOldChoicesAndAddsNoInventedSignals()
    {
        var state = Scenario(0, 0); new FoodDemand(state).AdvanceTo(2165);
        var old = JObject.Parse(FoodShadowCodec.Write(state)); LegacyMoney(old); RemoveV3Fields(old); old["Schema"] = 2;
        var migrated = FoodShadowCodec.Read(old.ToString(), "city");
        Assert.Equal(FoodShadowState.CurrentSchema, migrated.Schema); Assert.Null(migrated.Businesses[0].CustomerServicePercent);
        Assert.Equal(0, migrated.Consumers[0].ServiceQualityValueCents);
        Finish(migrated); Finish(state);
        Assert.Equal(state.Intents[0].BusinessId, migrated.Intents[0].BusinessId);
        Assert.Equal(state.Intents[0].SpentCents, migrated.Intents[0].SpentCents);
        old["Consumers"]![0]!["ServiceQualityValueCents"] = 100;
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(old.ToString(), "city"));
    }

    [Fact]
    public void InvalidSignalsAndMissingDistrictIdentityAreRefused()
    {
        var state = Scenario(); state.Businesses[0].CustomerServicePercent = 101;
        Assert.Throws<ArgumentException>(() => FoodShadowCodec.Write(state));
        state = Scenario(); state.Businesses[0].Offers[0].DistrictDemandPercent = -1;
        Assert.Throws<ArgumentException>(() => FoodShadowCodec.Write(state));
        state = Scenario(); state.Businesses[0].DistrictId = "";
        Assert.Throws<ArgumentException>(() => FoodShadowCodec.Write(state));
    }

    internal static void RemoveV3Fields(JObject document)
    {
        FoodHouseholdTests.RemoveV4Fields(document);
        var names = new[] { "DistrictId", "CustomerServicePercent", "CustomerServiceEvidence", "DistrictDemandPercent", "DistrictDemandEvidence",
            "ServiceQualityValueCents", "DistrictAppealValueCents", "ServiceQualityAdjustmentCents", "DistrictAppealAdjustmentCents" };
        foreach (var p in document.Descendants().OfType<JProperty>().Where(p => names.Contains(p.Name)).ToArray()) p.Remove();
    }
}
