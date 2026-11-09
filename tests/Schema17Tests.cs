using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// Population schema 17 (step A, slice A2): lifestyle traits, current-day outcomes, the food choice policy
// header and the decision wire contract. The legacy food choice and the economy stay exactly as before.
public sealed partial class FoodCommerceTests
{
    // ---- Traits -------------------------------------------------------------------------------------

    [Fact]
    public void S17_TraitGeneratorIsPinnedDeterministicAndBounded()
    {
        // Golden vectors for generation version 1: a change here changes every new resident's personality.
        Assert.Equal("v1:" + ResidentTraits.Generate(42, "rlo:a").Bands, ResidentTraits.Generate(42, "rlo:a").ToString());
        Assert.Equal(S17_Golden(42, "rlo:a"), ResidentTraits.Generate(42, "rlo:a").Bands);
        Assert.Equal(S17_Golden(42, "pc:1"), ResidentTraits.Generate(42, "pc:1").Bands);
        Assert.Equal(S17_Golden(7, "consumer:pc%3A1:1"), ResidentTraits.Generate(7, "consumer:pc%3A1:1").Bands);
        Assert.Equal(S17_Golden(-3, "rlo:a"), ResidentTraits.Generate(-3, "rlo:a").Bands);
        var counts = new int[ResidentTraits.Count, 5];
        const int people = 20000;
        for (int i = 0; i < people; i++)
        {
            var traits = ResidentTraits.Generate(11, "rlo:person:" + i);
            Assert.Equal(1, traits.Version); Assert.True(traits.IsValid);
            for (int t = 0; t < ResidentTraits.Count; t++) counts[t, traits.Band((ResidentTrait)t)]++;
        }
        double[] expected = { .10, .20, .40, .20, .10 };
        for (int t = 0; t < ResidentTraits.Count; t++)
            for (int band = 0; band < 5; band++)
                Assert.InRange(counts[t, band] / (double)people, expected[band] - .02, expected[band] + .02);
        // Each trait has its own hash domain: no two traits are the same column.
        var columns = Enumerable.Range(0, ResidentTraits.Count).Select(t => string.Concat(Enumerable.Range(0, 200)
            .Select(i => ResidentTraits.Generate(11, "rlo:person:" + i).Bands[t]))).ToList();
        Assert.Equal(ResidentTraits.Count, columns.Distinct().Count());
        Assert.Throws<ArgumentException>(() => ResidentTraits.Generate(1, ""));
    }

    // Independent reimplementation of the documented key: "pc:traits:v1|<length>:<id>|<code>", FNV-1a then the
    // fixed finaliser, band thresholds 10/30/70/90 of 100.
    private static string S17_Golden(int seed, string id)
    {
        string[] codes = { "taste:croissant", "taste:burger", "taste:hotdog", "price", "queue", "loyalty", "travel", "health", "quality", "time" };
        return string.Concat(codes.Select(code =>
        {
            uint value = StableRandom.Hash("pc:traits:v1|" + id.Length + ":" + id + "|" + code, seed) % 100;
            return value < 10 ? '0' : value < 30 ? '1' : value < 70 ? '2' : value < 90 ? '3' : '4';
        }));
    }

    [Fact]
    public void S17_TraitsDependOnlyOnTheSavedSeedAndStableId()
    {
        CitizenIdentity Person(string id, string name, string district) => new CitizenIdentity { Id = id, Name = name, District = district, Age = 30, Persona = Persona.Commuter };
        Place[] places = { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 10 },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 10, Position = new(1, 0, 0) } };
        var a = Population.Create(new[] { Person("r1", "Ada", "x"), Person("r2", "Bo", "y") }, places, "k", "v", 9, 0);
        // Different order, names and districts; same IDs and seed.
        var b = Population.Create(new[] { Person("r2", "Zed", "q"), Person("r1", "Quinn", "z") }, places, "k", "v", 9, 0);
        var c = Population.Create(new[] { Person("r1", "Ada", "x"), Person("r2", "Bo", "y") }, places, "k", "v", 10, 0);
        foreach (var person in a.Citizens)
        {
            Assert.Equal(ResidentTraits.Generate(9, person.Identity.Id).Bands, person.Traits!.Bands);
            Assert.Equal(person.Traits.Bands, b.Citizens.Single(x => x.Identity.Id == person.Identity.Id).Traits!.Bands);
        }
        Assert.NotEqual(a.Citizens.Select(x => x.Traits!.Bands), c.Citizens.Select(x => x.Traits!.Bands));
        // No food scope: traits exist, nothing economic does, and the save keeps it that way.
        Assert.Null(a.Food);
        var read = DevelopmentChain.Read(DevelopmentChain.Write(a));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Null(read.State!.Food);
        Assert.Equal(a.Citizens.Select(x => x.Traits!.Bands), read.State.Citizens.Select(x => x.Traits!.Bands));
    }

    [Fact]
    public void S17_NewResidentsAndRepresentedSlotsGetStableTraitsAndEventsNeverChangeThem()
    {
        var (city, _, _) = Ready(count: 2, threeMeals: true, representation: 3);
        foreach (var meal in city.State.Food!.Residents)
        {
            if (meal.HostSlot == 0) Assert.Null(meal.RepresentedTraits);
            else Assert.Equal(ResidentTraits.Generate(city.State.Seed, meal.ResidentId).Bands, meal.RepresentedTraits!.Bands);
        }
        var before = city.State.Citizens.ToDictionary(c => c.Identity.Id, c => c.Traits);
        Advance(city, 3 * 1440);
        foreach (var person in city.State.Citizens)
        {
            Assert.Same(before[person.Identity.Id], person.Traits);
            Assert.Equal(ResidentTraits.Generate(city.State.Seed, person.Identity.Id).Bands, person.Traits!.Bands);
        }
        // Housing expansion appends pc: residents with traits from their own new stable IDs.
        city.State.Places.Single(p => p.Id == "home").Capacity = 10;
        Population.ExpandToHousing(city.State);
        var added = city.State.Citizens.Where(c => c.Identity.Id.StartsWith("pc:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(added);
        foreach (var person in added) Assert.Equal(ResidentTraits.Generate(city.State.Seed, person.Identity.Id).Bands, person.Traits!.Bands);
    }

    // ---- Outcome upgrade ----------------------------------------------------------------------------

    [Fact]
    public void S17_UpgradeRecordsOnlyTheCurrentMealAndMarksEarlierMealsNotRecorded()
    {
        int day = 3; double minute = day * 1440 + 800;
        ResidentMeal Meal(MealKind kind, MealStatus status, int mealDay = 3) => new ResidentMeal { ResidentId = "r", AnchorId = "r", Day = mealDay, Kind = kind,
            Status = status, PlaceId = "shop", ProductId = FoodProducts.Burger, DesiredAt = mealDay * 1440 + 720, ServiceAt = mealDay * 1440 + 740, Reason = "Native fake confirmed" };
        var paid = MealOutcomes.Upgrade(Meal(MealKind.Lunch, MealStatus.Paid), minute, legacyRhythm: false);
        Assert.Equal(day, paid.Day); Assert.Equal(1, paid.Unrecorded);
        var record = Assert.Single(paid.Meals);
        Assert.Equal((MealKind.Lunch, MealOutcomeStatus.Bought, MealReasonCode.Bought, "shop", FoodProducts.Burger, day * 1440 + 740),
            (record.Kind, record.Status, record.Reason, record.PlaceId, record.ProductId, record.CheckoutAt));
        var uncertain = MealOutcomes.Upgrade(Meal(MealKind.Dinner, MealStatus.Uncertain), minute, false);
        Assert.Equal(2, uncertain.Unrecorded); Assert.Equal(MealOutcomeStatus.Unconfirmed, uncertain.Meals.Single().Status);
        var pending = MealOutcomes.Upgrade(Meal(MealKind.Breakfast, MealStatus.Pending), minute, false);
        Assert.Equal(0, pending.Unrecorded); Assert.Empty(pending.Meals);
        // Tomorrow's breakfast already started: all of today's meals ended before the upgrade.
        var tomorrow = MealOutcomes.Upgrade(Meal(MealKind.Breakfast, MealStatus.Pending, day + 1), minute, false);
        Assert.Equal((day, 3), (tomorrow.Day, tomorrow.Unrecorded)); Assert.Empty(tomorrow.Meals);
        var legacy = MealOutcomes.Upgrade(Meal(MealKind.Legacy, MealStatus.Declined), minute, legacyRhythm: true);
        Assert.Equal(0, legacy.Unrecorded); Assert.Equal(MealOutcomeStatus.ChoseAlternative, legacy.Meals.Single().Status);
        Assert.Equal(FoodOutsideOption.None, legacy.Meals.Single().Outside);
        Assert.Equal(1, MealOutcomes.Upgrade(Meal(MealKind.Legacy, MealStatus.Pending, day + 1), minute, true).Unrecorded);
    }

    // ---- Round trip and refusals --------------------------------------------------------------------

    private static (CitySimulator city, string json) S17_Full()
    {
        var (city, _, _) = Ready(count: 2, threeMeals: true, representation: 2);
        var food = city.State.Food!;
        food.ChoiceStartsDay=0; // This fixture deliberately constructs an already active decision.
        var first = food.Residents.First(m => m.HostSlot == 0);
        first.Today = new ResidentFoodToday { Day = 0, Meals = {
            new MealOutcomeRecord { Kind = MealKind.Breakfast, Status = MealOutcomeStatus.ChoseAlternative, Reason = MealReasonCode.PrefersAlternative, Outside = FoodOutsideOption.FundedHouseholdFood, At = 500 },
            new MealOutcomeRecord { Kind = MealKind.Lunch, Status = MealOutcomeStatus.Skipped, Reason = MealReasonCode.QueueTooLong, At = 760, Factor1 = MealFactor.Queue },
            new MealOutcomeRecord { Kind = MealKind.Dinner, Status = MealOutcomeStatus.Bought, Reason = MealReasonCode.Bought, PlaceId = "address:Gone%20Street:1:shop", ProductId = "pastry", At = 1150, CheckoutAt = 1150 } } };
        var decided = food.Residents.Last(m => m.HostSlot == 1);
        decided.LastContextKey = "work@1440"; decided.ContextAttempts = 1;
        Route Walk(Position from, Position to) => new Route { Legs = { new RouteLeg { From = from, To = new((from.X + to.X) / 2, 0, 0), Minutes = 1, Mode = TravelMode.Walk },
            new RouteLeg { From = new((from.X + to.X) / 2, 0, 0), To = to, Minutes = 1, Mode = TravelMode.Walk } } };
        decided.Decision = new FoodDecisionState { IntentId = decided.IntentId, ContextKey = "work@1440", Attempt = 1, Result = FoodDecisionResult.Selected,
            BusinessId = "b", ProductId = FoodProducts.Burger, Reason = MealReasonCode.Other, Factor1 = MealFactor.Taste, Factor2 = MealFactor.Distance,
            OriginAt = 1440, Deadline = 1500, Shops = { new FoodDecisionShop { BusinessId = "b", PlaceId = "shop", MealsPerHour = 15, NextServiceAt = 1440,
                Hours = { new OpeningWindow { Day = 1, FromMinute = 420, ToMinute = 1320 }, new OpeningWindow { Day = 2, FromMinute = 1320, ToMinute = 120 } },
                CustomerServicePercent = 80, DistrictId = "a",
                Offers = { new FoodDecisionOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(725), Units = 3, DistrictDemandPercent = 60 },
                    new FoodDecisionOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(350) } },
                OutwardQuote = FoodQuoteState.Answered, ReturnQuote = FoodQuoteState.Answered,
                Outward = Walk(new(0, 0, 0), new(2, 0, 0)), Return = Walk(new(2, 0, 0), new(0, 0, 0)) } }, QuotesDone = 2 };
        return (city, DevelopmentChain.Write(city.State));
    }

    [Fact]
    public void S17_InterruptedDecisionKeepsEachQuoteSlotsProgressAcrossReload()
    {
        var (city, _) = S17_Full();
        var decision = city.State.Food!.Residents.Single(m => m.Decision != null).Decision!;
        var answered = decision.Shops[0];
        Route Copy(Route r) => new Route { Legs = r.Legs.Select(l => new RouteLeg { From = l.From, To = l.To, Minutes = l.Minutes, Mode = l.Mode }).ToList() };
        // Shop b1: outward refused, so its return was never asked. Shop b2: outward answered, return refused.
        // Shop b3: not yet quoted. The cursor sits on b3's outward slot.
        var refusedOutward = new FoodDecisionShop { BusinessId = "b1", PlaceId = "shop", OutwardQuote = FoodQuoteState.Refused, ReturnQuote = FoodQuoteState.NotNeeded };
        var refusedReturn = new FoodDecisionShop { BusinessId = "b2", PlaceId = "shop", OutwardQuote = FoodQuoteState.Answered, ReturnQuote = FoodQuoteState.Refused, Outward = Copy(answered.Outward!) };
        var pending = new FoodDecisionShop { BusinessId = "b3", PlaceId = "shop" };
        decision.Shops = new List<FoodDecisionShop> { refusedOutward, refusedReturn, pending };
        decision.Result = FoodDecisionResult.Evaluating; decision.BusinessId = decision.ProductId = ""; decision.QuotesDone = 4;
        string json = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(json, DevelopmentChain.Write(read.State!));
        var restored = read.State!.Food!.Residents.Single(m => m.Decision != null).Decision!;
        Assert.Equal(4, restored.QuotesDone);
        Assert.Equal(new[] { (FoodQuoteState.Refused, FoodQuoteState.NotNeeded), (FoodQuoteState.Answered, FoodQuoteState.Refused), (FoodQuoteState.Unrequested, FoodQuoteState.Unrequested) },
            restored.Shops.Select(x => (x.OutwardQuote, x.ReturnQuote)));
        Assert.Null(restored.Shops[0].Outward); Assert.NotNull(restored.Shops[1].Outward); Assert.Null(restored.Shops[1].Return);
        // Before b2's return was asked, the same image differs: unrequested is not refused.
        refusedReturn.ReturnQuote = FoodQuoteState.Unrequested; decision.QuotesDone = 3;
        Assert.NotEqual(json, DevelopmentChain.Write(city.State));
    }

    [Fact]
    public void S17_EveryOutcomeRecordWriteAcceptsReadsBackAndTheReaderCapIsBounded()
    {
        var (city, _) = S17_Full();
        var meal = city.State.Food!.Residents.First(m => m.Today != null);
        // Largest record validation accepts: two-digit enums, ten-digit minutes and two 256-character IDs.
        var dinner = meal.Today!.Meals[2];
        dinner.PlaceId = new string('p', 256); dinner.ProductId = new string('q', 256);
        dinner.Reason = MealReasonCode.ReceiptConfirmed;
        string wire = dinner.ToWire();
        Assert.True(wire.Length <= SaveCodec.MaxMealOutcomeWire, wire.Length.ToString());
        string json = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(json, DevelopmentChain.Write(read.State!));
        Assert.Equal(553, 6 * 2 + 2 * 10 + 9 + 256 + 256);
        // An ID one character longer is refused at write, so it never reaches the reader.
        dinner.PlaceId += "p";
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city.State));
        // A record string beyond the reader's cap is refused before deserialization and preserved.
        string oversized = json.Replace(wire, wire.Substring(0, wire.Length - 256) + new string('p', SaveCodec.MaxMealOutcomeWire - (wire.Length - 256) + 1));
        Assert.NotEqual(json, oversized);
        var refused = DevelopmentChain.Read(oversized);
        Assert.Equal(SaveReadStatus.Invalid, refused.Status); Assert.Contains("Invalid meal outcome record", refused.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = oversized };
        Assert.False(SaveStore.WriteTo(data, read.State!).Success); Assert.Same(oversized, data[SaveStore.StateKey]);
    }

    [Fact]
    public void S17_AnOutcomeProductThatCannotBeWrittenCompactlyIsRefusedAtWrite()
    {
        var (city, _) = S17_Full();
        var meal = city.State.Food!.Residents.First(m => m.Today != null);
        meal.Today!.Meals[2].ProductId = "pas|try";
        Assert.Contains("cannot contain", Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city.State)).Message);
    }

    [Fact]
    public void S17_OutcomeRecordsUseOneCompactStringAndRoundTripExactly()
    {
        var record = new MealOutcomeRecord { Kind = MealKind.Lunch, Status = MealOutcomeStatus.Bought, Reason = MealReasonCode.ReceiptConfirmed,
            Outside = FoodOutsideOption.None, Factor1 = MealFactor.Taste, Factor2 = MealFactor.Price, At = 2163, CheckoutAt = 2161,
            ProductId = FoodProducts.Burger, PlaceId = "address:ba%3Astreet_harborroad:1042:shop" };
        string wire = record.ToWire();
        Assert.Equal("2|0|2|0|1|2|2163|2161|ba:itemname_burger|address:ba%3Astreet_harborroad:1042:shop", wire);
        Assert.True(MealOutcomeRecord.TryParse(wire, out var parsed));
        Assert.Equal(JsonConvert.SerializeObject(record), JsonConvert.SerializeObject(parsed));
        // The place is last, so an opaque historical place ID may itself contain the separator.
        record.PlaceId = "legacy|odd";
        Assert.True(MealOutcomeRecord.TryParse(record.ToWire(), out parsed)); Assert.Equal("legacy|odd", parsed.PlaceId);
        foreach (string bad in new[] { "", "2|0|2|0|1|2|2163|2161|x", "02|0|2|0|1|2|2163|2161|x|y", "2|0|2|0|1|2|+2163|2161|x|y", "2|0|2|0|1|2|99999999999|2161|x|y", "2|0|2|0|1|2| 1|2161|x|y" })
            Assert.False(MealOutcomeRecord.TryParse(bad, out _), bad);
        // Every dense enum the validator range-checks ends at the maximum it uses.
        Assert.Equal(MealKind.Dinner, Enum.GetValues<MealKind>().Max());
        Assert.Equal(MealOutcomeStatus.Unconfirmed, Enum.GetValues<MealOutcomeStatus>().Max());
        Assert.Equal(MealReasonCode.FundsUnavailable, Enum.GetValues<MealReasonCode>().Max());
        Assert.Equal(FoodOutsideOption.FundedHouseholdFood, Enum.GetValues<FoodOutsideOption>().Max());
        Assert.Equal(MealFactor.Household, Enum.GetValues<MealFactor>().Max());
        Assert.Equal(FoodDecisionResult.NoFeasible, Enum.GetValues<FoodDecisionResult>().Max());
        Assert.Equal(FoodQuoteState.NotNeeded, Enum.GetValues<FoodQuoteState>().Max());
        foreach (var e in new[] { typeof(MealKind), typeof(MealOutcomeStatus), typeof(MealReasonCode), typeof(FoodOutsideOption), typeof(MealFactor), typeof(FoodDecisionResult), typeof(FoodQuoteState) })
            Assert.Equal(Enumerable.Range(0, Enum.GetValues(e).Length), Enum.GetValues(e).Cast<object>().Select(Convert.ToInt32));
    }

    [Fact]
    public void S17_FutureSchemasAreRefusedAsFutureAndPreserved()
    {
        var (city, json) = S17_Full();
        foreach (int schema in new[] { CityState.CurrentSchema+1, CityState.CurrentSchema+2, 1000 })
        {
            var wire = SaveWire.Parse(json); wire["Schema"] = schema; string payload = wire.ToString(Formatting.None);
            Assert.Equal(SaveReadStatus.Future, DevelopmentChain.Read(payload).Status);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
            Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Same(payload, data[SaveStore.StateKey]);
        }
    }

    // ---- Current-day outcomes -----------------------------------------------------------------------

    [Fact]
    public void S17_ThreeMealsAreRecordedOncePerMealWithoutClaimingConsumption()
    {
        var (city, market, routes) = Ready(atWork: true, threeMeals: true);
        var meal = city.State.Food!.Residents.Single();
        Assert.Null(meal.Today);
        Advance(city, 1440 + 1439);
        var today = meal.Today!;
        Assert.Equal(1, today.Day); Assert.Equal(0, today.Unrecorded);
        Assert.Equal(today.Meals.Select(m => m.Kind).Distinct().Count(), today.Meals.Count);
        Assert.Equal(today.Meals.OrderBy(m => m.Kind).Select(m => m.Kind), today.Meals.Select(m => m.Kind));
        int bought = today.Meals.Count(m => m.Status == MealOutcomeStatus.Bought);
        Assert.Equal(meal.Purchases, bought);
        foreach (var record in today.Meals.Where(m => m.Status == MealOutcomeStatus.Bought))
        {
            Assert.Equal("shop", record.PlaceId); Assert.Equal(FoodProducts.Croissant, record.ProductId);
            Assert.True(record.CheckoutAt >= 1440 && record.CheckoutAt <= record.At);
        }
        // Reload keeps the records exactly and does not add any.
        city = Reload(city, market, routes);
        Assert.Equal(JsonConvert.SerializeObject(today), JsonConvert.SerializeObject(city.State.Food!.Residents.Single().Today));
    }

    [Fact]
    public void S17_OutcomesAreIdempotentPerMealAndALaterPurchaseReplacesAnEarlierSkip()
    {
        var meal = new ResidentMeal { ResidentId = "r", AnchorId = "r", Day = 4, Kind = MealKind.Lunch, Status = MealStatus.Missed,
            Reason = "Existing stop: Queue exceeds patience on arrival", PlaceId = "shop", ProductId = FoodProducts.HotDog };
        MealOutcomes.Record(meal, 4 * 1440 + 700, checkout: false);
        MealOutcomes.Record(meal, 4 * 1440 + 700, checkout: false);
        var skipped = Assert.Single(meal.Today!.Meals);
        Assert.Equal((MealOutcomeStatus.Skipped, MealReasonCode.QueueTooLong), (skipped.Status, skipped.Reason));
        meal.Status = MealStatus.Pending; MealOutcomes.Record(meal, 4 * 1440 + 710, false); // in progress: never recorded
        Assert.Same(skipped, Assert.Single(meal.Today.Meals));
        meal.Status = MealStatus.Uncertain; meal.ServiceAt = 4 * 1440 + 720; meal.Reason = "Native purchase did not confirm completion";
        MealOutcomes.Record(meal, 4 * 1440 + 720, checkout: true);
        Assert.Equal(MealOutcomeStatus.Unconfirmed, Assert.Single(meal.Today.Meals).Status);
        meal.Status = MealStatus.Paid; meal.Reason = "Native fake confirmed";
        MealOutcomes.Record(meal, 4 * 1440 + 721, checkout: true);
        var bought = Assert.Single(meal.Today.Meals);
        Assert.Equal((MealOutcomeStatus.Bought, 4 * 1440 + 720), (bought.Status, bought.CheckoutAt));
        // The checkout result path records a native refusal as skipped with its checkout time.
        var refused = new ResidentMeal { ResidentId = "q", AnchorId = "q", Day = 4, Kind = MealKind.Dinner, Status = MealStatus.Missed,
            Reason = "Unavailable at checkout", PlaceId = "shop", ProductId = FoodProducts.HotDog, ServiceAt = 4 * 1440 + 1100 };
        MealOutcomes.Record(refused, 4 * 1440 + 1100, checkout: true);
        Assert.Equal((MealOutcomeStatus.Skipped, MealReasonCode.CheckoutRefused, 4 * 1440 + 1100, "", ""),
            (refused.Today!.Meals[0].Status, refused.Today.Meals[0].Reason, refused.Today.Meals[0].CheckoutAt, refused.Today.Meals[0].PlaceId, refused.Today.Meals[0].ProductId));
    }

    [Fact]
    public void S17_MidnightIsLazyAndALateOutcomeNeverOverwritesALaterDay()
    {
        var meal = new ResidentMeal { ResidentId = "r", AnchorId = "r", Day = 2, Kind = MealKind.Dinner, Status = MealStatus.Paid,
            PlaceId = "shop", ProductId = FoodProducts.Burger, Reason = "Native fake confirmed", ServiceAt = 2 * 1440 + 1100 };
        MealOutcomes.Record(meal, 2 * 1440 + 1100, checkout: true);
        var today = meal.Today!; var list = today.Meals;
        // Past midnight nothing changes until this consumer's own next outcome.
        Assert.Equal(2, today.Day);
        meal.Day = 3; meal.Kind = MealKind.Breakfast; meal.Status = MealStatus.Declined; meal.PlaceId = meal.ProductId = "";
        MealOutcomes.Record(meal, 3 * 1440 + 500, false, FoodOutsideOption.FundedHouseholdFood);
        Assert.Same(today, meal.Today); Assert.Same(list, today.Meals);
        Assert.Equal(3, today.Day);
        var record = Assert.Single(today.Meals);
        Assert.Equal((MealKind.Breakfast, MealOutcomeStatus.ChoseAlternative, FoodOutsideOption.FundedHouseholdFood), (record.Kind, record.Status, record.Outside));
        // A confirmation belonging to day 2 arrives late: day 3's outcomes are untouched.
        meal.Day = 2; meal.Kind = MealKind.Dinner; meal.Status = MealStatus.Paid;
        MealOutcomes.Record(meal, 3 * 1440 + 600, true);
        Assert.Equal(3, today.Day); Assert.Same(record, Assert.Single(today.Meals));
    }

    [Fact]
    public void S17_LateReceiptConfirmationReplacesTheUnconfirmedOutcomeExactlyOnce()
    {
        var (city, market, routes) = Ready(atWork: true);
        market.ThrowAfterSale = true;
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 40);
        Assert.Equal(MealStatus.Uncertain, meal.Status);
        var unconfirmed = Assert.Single(meal.Today!.Meals);
        Assert.Equal((MealKind.Legacy, MealOutcomeStatus.Unconfirmed, MealReasonCode.CheckoutUnconfirmed), (unconfirmed.Kind, unconfirmed.Status, unconfirmed.Reason));
        city = Reload(city, market, routes);
        market.ThrowAfterSale = false; market.ConfirmEvidence = true;
        // Uncertain ownership is rechecked on the consumer's later events, after the day has turned.
        meal = city.State.Food!.Residents.Single();
        for (double at = city.State.Minute + 1; meal.Purchases == 0 && at < 4 * 1440; at++) Advance(city, at);
        Assert.True(city.State.Minute >= 2 * 1440);
        Assert.Single(market.Orders); Assert.Equal(1, meal.Purchases);
        Assert.Equal(1, meal.Today!.Day);
        var confirmed = Assert.Single(meal.Today.Meals);
        Assert.Equal((MealKind.Legacy, MealOutcomeStatus.Bought, MealReasonCode.ReceiptConfirmed, unconfirmed.CheckoutAt),
            (confirmed.Kind, confirmed.Status, confirmed.Reason, confirmed.CheckoutAt));
    }

    // ---- The economy and choice are exactly as before schema 17 ---------------------------------------

    // Values produced by tools/save-contract-regression's economy scenario compiled against the pre-A2 source
    // (schema 16, commit a817bcd); the candidate must reproduce them: same orders, purchases, money and meals.
    [Fact]
    public void S17_EconomyScenarioMatchesThePreSchema17Baseline()
    {
        // Two consumers per resident is the represented-consumer city, which only the frozen writer-26 path keeps (F5d).
        var result = JObject.FromObject(EconomyScenario.Run(legacyChoice:true,
            frozen: (s => s.Schema = DevelopmentChain.Represented, DevelopmentChain.Write, DevelopmentChain.Read)));
        Assert.Equal(849, (int)result["orders"]!);
        Assert.Equal(797, (int)result["purchases"]!);
        Assert.Equal(107967424757760L, (long)result["spentUnits"]!);
        Assert.Equal(2, (int)result["uncertain"]!);
        Assert.Equal("1B94987B3158044594B9C4D6A5473BA22BE0D4F3488D769CBBEE23318BB33B91", (string)result["ordersDigest"]!);
    }
}
