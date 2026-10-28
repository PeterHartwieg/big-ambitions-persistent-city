using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodRoutineTests
{
    private sealed class Walking : ICommuteTimingPlanner
    {
        public int Calls;
        public double Duration = 10;
        public bool RefuseReturn;
        public readonly List<(string From, string To)> Requests = new();
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            Calls++; Requests.Add((from.Id, to.Id));
            if (RefuseReturn && from.Kind == PlaceKind.Shop) return null;
            return new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = Duration, Mode = TravelMode.Walk } } };
        }
    }

    private static CityState City(int count = 12)
    {
        var city = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "person:" + i, Name = "Person " + i, Persona = i % 4 == 0 ? Persona.NightOwl : Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new(100, 0, 0) },
            new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(150, 0, 0) },
            new Place { Id = "park", Name = "Park", Kind = PlaceKind.Leisure, Position = new(200, 0, 0) }
        }, "routine-test", "0.40.4", 17, 1440);
        return city;
    }

    private static FoodRoutineCapture Capture(CityState city, int days = 7, bool fine = false, double duration = 10)
    {
        var simulation = new CitySimulator(city, new Walking { Duration = duration });
        using var recorder = new FoodRoutineRecorder(simulation);
        double end = city.Minute + days * 1440;
        if (fine) for (double at = city.Minute; at <= end; at += 5) while (!simulation.AdvanceTo(at, 3).CaughtUp) { }
        else while (!simulation.AdvanceTo(end, 17).CaughtUp) { }
        return recorder.Finish();
    }

    private static FoodShadowState Scenario(int people = 1, int? capacity = 15, double outward = 5, double back = 5, int window = 45, int stock = 100)
    {
        var consumers = Enumerable.Range(0, people).Select(i => new FoodConsumer
        { Id = "r" + i, Name = "Resident " + i, OriginPlaceId = "home", DailyAllowance = Money.FromCents(1000) }).ToArray();
        var intents = consumers.Select(p => new FoodIntent { Id = FoodDemand.IntentId(p.Id, 1), ConsumerId = p.Id, Day = 1,
            DesiredAt = 2160, RequestedAt = 2160, Deadline = 2160 + window, OriginPlaceId = "work", OriginActivity = Activity.Work,
            EatingMinutes = 15, MaximumQueueMinutes = 30, ContextEvidence = "Observed workplace; controlled lunch break" }).ToArray();
        var shops = new[] { new FoodBusiness { Id = "cafe", Name = "Cafe", PlaceId = "cafe-door", MealsPerHour = capacity,
            ServiceEvidence = "Controlled staffed throughput", Offers = new() { new FoodOffer { ProductId = "meal", Price = Money.FromCents(500), Units = stock } },
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList() } };
        return FoodDemand.CreateObserved("routine-test", "Controlled observation/route fixture", 1, 1, consumers, shops,
            intents.Select(i => new FoodTravelQuote { ConsumerId = i.ConsumerId, BusinessId = "cafe", IntentId = i.Id,
                OriginPlaceId = "work", Minutes = outward, ReturnMinutes = back, Evidence = "Separate controlled routes" }), intents);
    }

    private static FoodShadowState Finish(FoodShadowState state)
    { var engine = new FoodDemand(state); while (!engine.AdvanceTo(state.EndsAt, 5)) { } return state; }

    [Fact]
    public void ObservationDoesNotChangePopulationAndRecordsActualPlaces()
    {
        var city = City(); var plain = SaveCodec.Read(SaveCodec.Write(city)).State!;
        var capture = Capture(city);
        var simulator = new CitySimulator(plain, new Walking()); simulator.AdvanceTo(8 * 1440);
        Assert.Equal(SaveCodec.Write(plain), SaveCodec.Write(city));
        var plan = FoodRoutinePlanning.Build(capture, 1, 7);
        Assert.Contains(plan.Opportunities, i => i.OriginActivity == Activity.Work && i.OriginPlaceId == "work");
        Assert.Contains(plan.Opportunities, i => i.Day == 5 && i.OriginActivity == Activity.Home && i.OriginPlaceId == "home");
        Assert.All(plan.Opportunities.Where(i => i.OriginPlaceId.Length > 0), i => Assert.Contains(capture.Spans,
            s => s.ConsumerId == i.ConsumerId && s.PlaceId == i.OriginPlaceId && s.From <= i.RequestedAt && s.Until >= i.Deadline));
    }

    [Fact]
    public void CoarseAndFinePopulationUpdatesGiveIdenticalFoodOpportunities()
    {
        var coarse = Capture(City()); var fine = Capture(City(), fine: true);
        Assert.Equal(JsonConvert.SerializeObject(coarse), JsonConvert.SerializeObject(fine));
        Assert.Equal(JsonConvert.SerializeObject(FoodRoutinePlanning.Build(coarse, 1, 7)), JsonConvert.SerializeObject(FoodRoutinePlanning.Build(fine, 1, 7)));
        var p = FoodRoutinePlanning.Build(coarse, 1, 7).Opportunities.Where(i => i.ConsumerId == "person:1").ToArray();
        Assert.True(p.Select(i => i.DesiredAt % 1440).Distinct().Count() > 2);
    }

    [Fact]
    public void LongTravelDoesNotPretendTheResidentIsAlreadyAtWork()
    {
        var city = City(1); city.Citizens[0].Identity.Persona = Persona.Commuter;
        var capture = Capture(city, 1, duration: 600);
        var plan = FoodRoutinePlanning.Build(capture, 1, 1);
        foreach (var i in plan.Opportunities.Where(i => i.OriginPlaceId != ""))
            Assert.Contains(capture.Spans, s => s.PlaceId == i.OriginPlaceId && s.From <= i.RequestedAt && s.Until >= i.Deadline);
        Assert.DoesNotContain(capture.Spans, s => s.Activity == Activity.Work && s.From < 1440 + 600);
    }

    [Fact]
    public void NoObservedStopDoesNotFabricateHomeOrACompletedMeal()
    {
        var capture = Capture(City(1), 1); capture.Spans.Clear();
        var plan = FoodRoutinePlanning.Build(capture, 1, 1);
        Assert.Equal("", plan.Opportunities.Single().OriginPlaceId);
        var state = FoodDemand.CreateObserved(capture.CityKey, "No recorded free stop", 1, 1, plan.Consumers,
            Array.Empty<FoodBusiness>(), Array.Empty<FoodTravelQuote>(), plan.Opportunities);
        Assert.Equal(FoodIntentStatus.Unresolved, Finish(state).Intents.Single().Status);
    }

    [Fact]
    public void OvernightPresenceIsContinuousAndRecorderDisposalDoesNotStopSimulation()
    {
        var city = City(1); city.Citizens[0].DayPlaceId = city.Citizens[0].ShopId = city.Citizens[0].LeisureId = "";
        var engine = new CitySimulator(city, new Walking()); using var recorder = new FoodRoutineRecorder(engine);
        engine.AdvanceTo(3 * 1440);
        var capture = recorder.Finish(); Assert.Single(capture.Spans);
        Assert.Equal(1440, capture.Spans[0].From); Assert.Equal(3 * 1440, capture.Spans[0].Until);
        engine.AdvanceTo(4 * 1440); Assert.Equal(4 * 1440, city.Minute);
        Assert.Throws<InvalidOperationException>(() => recorder.Finish());
    }

    [Fact]
    public void IncompleteOrOverlappingObservationsAreRefused()
    {
        var capture = Capture(City(1), 1);
        Assert.Throws<ArgumentException>(() => FoodRoutinePlanning.Build(capture, 1, 2));
        capture.Spans.Add(capture.Spans[0]);
        Assert.Throws<ArgumentException>(() => FoodRoutinePlanning.Build(capture, 1, 1));
    }

    [Fact]
    public void ReturnTravelAndEatingMustFitTheLunchBreak()
    {
        Assert.Equal(FoodIntentStatus.Fulfilled, Finish(Scenario()).Intents.Single().Status);
        var tooFar = Finish(Scenario(back: 30));
        Assert.Equal(FoodIntentStatus.Unmet, tooFar.Intents.Single().Status);
        Assert.Contains("return", tooFar.Intents.Single().Choices.Single().Reason);
        var unknownReturn = Scenario(); unknownReturn.TravelQuotes[0].ReturnMinutes = null;
        Assert.Equal(FoodIntentStatus.Unmet, Finish(unknownReturn).Intents.Single().Status);
    }

    [Theory]
    [InlineData(null, FoodIntentStatus.Unresolved)]
    [InlineData(0, FoodIntentStatus.Unmet)]
    public void UnverifiedOrUnavailableServiceCannotFulfill(int? capacity, FoodIntentStatus status)
    {
        var state = Finish(Scenario(capacity: capacity));
        Assert.Equal(status, state.Intents.Single().Status); Assert.Equal(Money.FromCents(0), state.Intents.Single().Spent);
    }

    [Fact]
    public void ServiceQueueConstrainsThroughputAndPreservesReturnDeadline()
    {
        var slow = Finish(Scenario(30)); var fast = Finish(Scenario(30, capacity: 30));
        int served = slow.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled);
        Assert.Equal(5, served); Assert.Equal(10, fast.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled));
        Assert.All(slow.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled), i => Assert.True(i.ReturnBy <= i.Deadline));
        Assert.Equal(100 - served, slow.Businesses[0].Offers[0].Units);
        Assert.Equal(Money.FromCents(served * 500), Money.Sum(slow.Intents.Select(i => i.Spent)));
    }

    [Fact]
    public void QueuedCustomersReserveFiniteStockAndResumeWithoutDoubleSales()
    {
        var state = Scenario(10, stock: 2); var engine = new FoodDemand(state);
        while (!engine.AdvanceTo(2166, 1)) { }
        Assert.Equal(2, state.Intents.Count(i => i.Status == FoodIntentStatus.WaitingForService));
        var resumed = FoodShadowCodec.Read(FoodShadowCodec.Write(state), state.CityKey);
        Finish(resumed); Finish(state);
        Assert.Equal(FoodShadowCodec.Write(state), FoodShadowCodec.Write(resumed));
        Assert.Equal(2, state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled));
        Assert.Equal(0, state.Businesses[0].Offers[0].Units);
        string before = FoodShadowCodec.Write(state); new FoodDemand(state).AdvanceTo(state.EndsAt);
        Assert.Equal(before, FoodShadowCodec.Write(state));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("price")]
    [InlineData("service")]
    [InlineData("stock")]
    public void ChangedConditionsDuringServiceDoNotBookASale(string change)
    {
        var state = Scenario(); var engine = new FoodDemand(state); engine.AdvanceTo(2166);
        Assert.Equal(FoodIntentStatus.WaitingForService, state.Intents.Single().Status);
        if (change == "closed") engine.Close("cafe");
        if (change == "price") engine.SetPrice("cafe", "meal", Money.FromCents(1500));
        if (change == "service") state.Businesses[0].MealsPerHour = 0;
        if (change == "stock") engine.SetStock("cafe", "meal", 0);
        engine.AdvanceTo(state.EndsAt);
        Assert.Equal(FoodIntentStatus.Unmet, state.Intents.Single().Status);
        Assert.Equal(Money.FromCents(0), state.Intents.Single().Spent);
        FoodShadowCodec.Write(state);
    }

    [Fact]
    public void RouteQuotesAreBoundToTheObservedOriginAndCannotBeReusedElsewhere()
    {
        var state = Scenario(); state.TravelQuotes[0].OriginPlaceId = "home";
        Assert.Throws<ArgumentException>(() => new FoodDemand(state));
        state = Scenario(); state.TravelQuotes[0].IntentId = "another-day";
        Assert.Throws<ArgumentException>(() => new FoodDemand(state));
    }

    [Fact]
    public void NativeRouteWorkIsBoundedAndQuotesBothDirections()
    {
        var city = City(2); var capture = Capture(city, 1); var plan = FoodRoutinePlanning.Build(capture, 1, 1);
        var walking = new Walking { RefuseReturn = true };
        var batch = new FoodRouteBatch(capture, plan, new[] { new FoodBusiness { Id = "cafe", PlaceId = "shop", MealsPerHour = 15 } },
            city.Places, walking, "Fixture paths");
        while (!batch.Complete)
        {
            int before = walking.Calls; batch.Step(1); Assert.InRange(walking.Calls - before, 0, 1);
        }
        Assert.Equal(plan.Opportunities.Count * 2, walking.Calls);
        Assert.All(batch.Quotes, q => { Assert.NotNull(q.Minutes); Assert.Null(q.ReturnMinutes); });
        Assert.Contains(walking.Requests, r => r.From == "shop" && r.To == "work");
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Step(0));
    }

    [Fact]
    public void StrictLegacyMigrationKeepsFixedReferenceAndDoesNotInventService()
    {
        var state = FoodDemand.Create("legacy", "Legacy fixture", 1, 1, new[] { new FoodConsumer { Id = "p", Name = "P", OriginPlaceId = "home" } },
            new[] { new FoodBusiness { Id = "b", Name = "B", PlaceId = "shop" } }, Array.Empty<FoodTravelQuote>());
        var json = JObject.Parse(FoodShadowCodec.Write(state)); FoodQualityDemandTests.LegacyMoney(json); FoodQualityDemandTests.RemoveV3Fields(json); json["Schema"] = 1; json.Remove("Mode");
        foreach (JObject b in json["Businesses"]!) foreach (string k in new[] { "MealsPerHour", "ServiceEvidence", "NextServiceMinute" }) b.Remove(k);
        foreach (JObject i in json["Intents"]!) foreach (string k in new[] { "OriginPlaceId", "OriginActivity", "ContextEvidence", "DesiredAt", "EatingMinutes", "MaximumQueueMinutes", "TimeCostMultiplier", "ReturnTravelMinutes", "ServiceEndsAt", "ReturnBy" }) i.Remove(k);
        var migrated = FoodShadowCodec.Read(json.ToString(), "legacy");
        Assert.Equal(FoodShadowState.CurrentSchema, migrated.Schema); Assert.Equal(FoodExperimentMode.FixedDailyReference, migrated.Mode);
        Assert.Null(migrated.Businesses[0].MealsPerHour);
        json["Mode"] = 1; Assert.ThrowsAny<Exception>(() => FoodShadowCodec.Read(json.ToString(), "legacy"));
    }

    [Fact]
    public void AValidMultiDayWalkIsRefusedWithoutAbortingOtherFoodQuotes()
    {
        var city = City(2); var capture = Capture(city, 1); var plan = FoodRoutinePlanning.Build(capture, 1, 1);
        var walking = new Walking { Duration = 2000 };
        var batch = new FoodRouteBatch(capture, plan, new[] { new FoodBusiness { Id = "cafe", PlaceId = "shop", MealsPerHour = 15 } },
            city.Places, walking, "Complete native-scale route exceeding the food horizon");
        while (!batch.Step(1)) { }
        Assert.Equal(2, batch.Quotes.Count); Assert.All(batch.Quotes, q => Assert.Null(q.Minutes));
        Assert.Equal(2, walking.Calls);
    }

    [Fact]
    public void DestinationsBeyondWalkingToleranceNeedNoNativeRouteCalls()
    {
        var city = City(2); var capture = Capture(city, 1); var plan = FoodRoutinePlanning.Build(capture, 1, 1);
        city.Places.Single(p => p.Id == "shop").Position = new Position(10000, 0, 0);
        var walking = new Walking();
        var batch = new FoodRouteBatch(capture, plan, new[] { new FoodBusiness { Id = "cafe", PlaceId = "shop", MealsPerHour = 15 } },
            city.Places, walking, "Geometry only refuses impossible walks");
        while (!batch.Step(1)) { }
        Assert.Equal(0, walking.Calls); Assert.Equal(2, batch.Quotes.Count);
        Assert.All(batch.Quotes, q => { Assert.Null(q.Minutes); Assert.Null(q.ReturnMinutes); });
    }
}
