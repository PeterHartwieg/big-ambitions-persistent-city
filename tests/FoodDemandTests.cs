using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodDemandTests
{
    private static FoodConsumer Person(string id = "resident:1") => new()
    { Id = id, Name = id, OriginPlaceId = "home:1", DailyAllowanceCents = 1000 };

    private static FoodBusiness Shop(string id, int price, int? stock = 100) => new()
    {
        Id = id, Name = id, PlaceId = id + ":door",
        Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 600, ToMinute = 1200 }).ToList(),
        Offers = new() { new FoodOffer { ProductId = "meal", PriceCents = price, Units = stock } }
    };

    private static FoodShadowState Scenario(int people = 1, int days = 1)
    {
        var consumers = Enumerable.Range(1, people).Select(i => Person("resident:" + i)).ToArray();
        var shops = new[] { Shop("near", 600), Shop("far", 500) };
        var routes = consumers.SelectMany(c => shops.Select(b => new FoodTravelQuote
        { ConsumerId = c.Id, BusinessId = b.Id, Minutes = b.Id == "near" ? 5 : 15, Evidence = "synthetic connected route" }));
        return FoodDemand.Create("test-city", "synthetic fixture", 1, days, consumers, shops, routes);
    }

    private static FoodShadowState Finish(FoodShadowState state)
    { var engine = new FoodDemand(state); while (!engine.AdvanceTo(state.EndsAt)) { } return state; }

    [Fact]
    public void HigherPriceRedirectsDemandToACompetingBusiness()
    {
        Assert.Equal("far", Finish(Scenario()).Intents.Single().BusinessId);
        var changed = new FoodDemand(Scenario());
        changed.SetPrice("far", "meal", 800);
        changed.AdvanceTo(changed.State.EndsAt);
        Assert.Equal("near", changed.State.Intents.Single().BusinessId);
    }

    [Fact]
    public void TravelAndFoodPreferencesCanOutweighPrice()
    {
        var travel = Scenario();
        travel.Consumers[0].ValueOfTimeCentsPerMinute = 20;
        Assert.Equal("near", Finish(travel).Intents.Single().BusinessId);
        var preference = Scenario();
        preference.Businesses.Single(b => b.Id == "near").Offers[0].ProductId = "favourite";
        preference.Consumers[0].PreferredProductId = "favourite";
        Assert.Equal("near", Finish(preference).Intents.Single().BusinessId);
    }

    [Fact]
    public void ClosuresRedirectAndStockoutsExplainUnmetNeeds()
    {
        var engine = new FoodDemand(Scenario());
        engine.Close("far");
        engine.AdvanceTo(engine.State.EndsAt);
        Assert.Equal("near", engine.State.Intents.Single().BusinessId);
        var empty = new FoodDemand(Scenario());
        empty.SetStock("near", "meal", 0); empty.SetStock("far", "meal", 0);
        empty.AdvanceTo(empty.State.EndsAt);
        Assert.Equal(FoodIntentStatus.Unmet, empty.State.Intents.Single().Status);
        Assert.All(empty.State.Intents.Single().Choices, c => Assert.Equal("Out of stock", c.Reason));
    }

    [Fact]
    public void UnknownStockAndMissingOrRefusedRoutesAreNotInvented()
    {
        var state = Scenario(2);
        state.Businesses.Single(b => b.Id == "near").Offers[0].Units = null;
        state.TravelQuotes.RemoveAll(q => q.BusinessId == "far" && q.ConsumerId == "resident:1");
        state.TravelQuotes.Single(q => q.BusinessId == "far").Minutes = null;
        Finish(state);
        Assert.All(state.Intents, i => Assert.Equal(FoodIntentStatus.Unresolved, i.Status));
        Assert.All(state.Intents, i => Assert.Equal(0, i.SpentCents));
        Assert.Equal(FoodShadowCodec.Write(state), FoodShadowCodec.Write(FoodShadowCodec.Read(FoodShadowCodec.Write(state), "test-city")));
        Assert.Contains(state.Intents[0].Choices, c => c.Reason == "Route unknown");
        Assert.Contains(state.Intents[1].Choices, c => c.Reason == "Unreachable");
        Assert.All(state.Intents, i => Assert.Contains(i.Choices, c => c.Reason == "Stock unknown"));
    }

    [Fact]
    public void JourneyBeyondMealWindowDoesNotCreateAnArrival()
    {
        var state = Scenario();
        state.TravelQuotes.ForEach(q => q.Minutes = 100);
        Finish(state);
        Assert.Equal(FoodIntentStatus.Unmet, state.Intents.Single().Status);
        Assert.Null(state.Intents.Single().ArrivalMinute);
    }

    [Fact]
    public void AnOpeningWithinTheWindowAddsWaitToTheChoice()
    {
        var state = Scenario();
        state.Businesses.Single(b => b.Id == "far").Hours.ForEach(h => h.FromMinute = 750);
        // 500 + 30*5 now costs more than 600 + 5*5.
        Assert.Equal("near", Finish(state).Intents.Single().BusinessId);
    }

    [Fact]
    public void ChangedConditionsOnArrivalDoNotBecomeSuccessfulPurchases()
    {
        foreach (int kind in new[] { 0, 1, 2 })
        {
            var engine = new FoodDemand(Scenario());
            engine.AdvanceTo(2160);
            Assert.Equal(FoodIntentStatus.Travelling, engine.State.Intents.Single().Status);
            if (kind == 0) engine.Close("far");
            if (kind == 1) engine.SetStock("far", "meal", 0);
            if (kind == 2) engine.SetPrice("far", "meal", 1001);
            engine.AdvanceTo(engine.State.EndsAt);
            Assert.Equal(FoodIntentStatus.Unmet, engine.State.Intents.Single().Status);
            Assert.Equal(0, engine.State.Intents.Single().SpentCents);
        }
    }

    [Fact]
    public void SimultaneousArrivalsCannotSpendTheSameUnitTwice()
    {
        var state = Scenario(2);
        state.Businesses.Single(b => b.Id == "far").Offers[0].Units = 1;
        Finish(state);
        Assert.Single(state.Intents, i => i.Status == FoodIntentStatus.Fulfilled);
        Assert.Single(state.Intents, i => i.Outcome == "Out of stock on arrival");
        Assert.Equal(500, state.Intents.Sum(i => i.SpentCents));
        Assert.Equal(0, state.Businesses.Single(b => b.Id == "far").Offers[0].Units);
    }

    [Fact]
    public void ReadInputsAreDetachedFromHypotheticalPricesAndInventory()
    {
        var people = new[] { Person() }; var shops = new[] { Shop("shop", 500, 2) };
        var routes = new[] { new FoodTravelQuote { ConsumerId = people[0].Id, BusinessId = "shop", Minutes = 5, Evidence = "fixture" } };
        var state = FoodDemand.Create("test-city", "fixture", 1, 1, people, shops, routes);
        var engine = new FoodDemand(state);
        engine.SetPrice("shop", "meal", 600); engine.AdvanceTo(state.EndsAt);
        Assert.Equal(500, shops[0].Offers[0].PriceCents);
        Assert.Equal(2, shops[0].Offers[0].Units);
        Assert.Equal(1, state.Businesses[0].Offers[0].Units);
    }

    [Fact]
    public void InterruptedTravelAndBoundedBatchesReplayExactly()
    {
        string expected = FoodShadowCodec.Write(Finish(Scenario(25, 3)));
        var engine = new FoodDemand(Scenario(25, 3));
        Assert.False(engine.AdvanceTo(2161, 7));
        var restored = FoodShadowCodec.Read(FoodShadowCodec.Write(engine.State), "test-city");
        engine = new FoodDemand(restored);
        while (!engine.AdvanceTo(restored.EndsAt, 3)) { }
        Assert.Equal(expected, FoodShadowCodec.Write(restored));
        engine.AdvanceTo(restored.EndsAt);
        Assert.Equal(expected, FoodShadowCodec.Write(restored));
        Assert.Equal(75, restored.Intents.Count);
    }

    [Fact]
    public void FineAndCoarseUpdatesMakeTheSameDecisions()
    {
        var state = Scenario(10, 3); var engine = new FoodDemand(state);
        for (double minute = state.Minute; minute <= state.EndsAt; minute++) engine.AdvanceTo(minute);
        Assert.Equal(FoodShadowCodec.Write(Finish(Scenario(10, 3))), FoodShadowCodec.Write(state));
    }

    [Fact]
    public void AllowancesBoundSpendAndAreNotInventedWages()
    {
        var state = Scenario(2, 2); state.Consumers[0].DailyAllowanceCents = 499;
        Finish(state);
        Assert.All(state.Intents.Where(i => i.ConsumerId == "resident:1"), i => Assert.Equal(FoodIntentStatus.Unmet, i.Status));
        Assert.All(state.Intents.Where(i => i.ConsumerId == "resident:2"), i => Assert.Contains("temporary allowance", i.Outcome));
    }

    [Fact]
    public void CodecRefusesFutureForeignUnknownAndDuplicateDocuments()
    {
        string source = FoodShadowCodec.Write(Scenario());
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source.Replace("\"Schema\": 4", "\"Schema\": 5"), "test-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source, "another-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source.Insert(1, "\"NewField\":1,"), "test-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source.Insert(1, "\"Schema\":1,"), "test-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source.Replace("\"SpentCents\": 0,", ""), "test-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source.Replace("\"SpentCents\": 0", "\"SpentCents\": \"0\""), "test-city"));
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(source + "{}", "test-city"));
        Assert.Equal(source, FoodShadowCodec.Write(FoodShadowCodec.Read(source, "test-city")));
    }

    [Fact]
    public void InvalidResumeAndClockRewindDoNotSilentlyResetTheExperiment()
    {
        var engine = new FoodDemand(Scenario()); engine.AdvanceTo(2161);
        string before = FoodShadowCodec.Write(engine.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AdvanceTo(2000));
        Assert.Equal(before, FoodShadowCodec.Write(engine.State));
        engine.State.Intents[0].ArrivalMinute = 2000;
        Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Write(engine.State));
    }

    [Fact]
    public void CatalogueEnumerationDoesNotChangeTiedChoicesOrArrivals()
    {
        var a = Scenario(8); var b = Scenario(8);
        foreach (var state in new[] { a, b })
        {
            state.Businesses.ForEach(s => s.Offers[0].PriceCents = 500);
            state.TravelQuotes.ForEach(r => r.Minutes = 10);
        }
        b.Businesses.Reverse(); b.Consumers.Reverse(); b.TravelQuotes.Reverse();
        Finish(a); Finish(b);
        Assert.Equal(a.Intents.Select(i => (i.Id, i.BusinessId, i.SpentCents)), b.Intents.Select(i => (i.Id, i.BusinessId, i.SpentCents)));
    }
}
