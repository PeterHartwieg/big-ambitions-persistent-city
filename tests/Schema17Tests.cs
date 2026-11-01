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
        var read = SaveCodec.Read(LegacyWire.Write(a));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Null(read.State!.Food);
        Assert.Equal(a.Citizens.Select(x => x.Traits!.Bands), read.State.Citizens.Select(x => x.Traits!.Bands));
    }

    [Fact]
    public void S17_NewResidentsAndRepresentedSlotsGetStableTraitsAndEventsNeverChangeThem()
    {
        var (city, _, _) = Ready(count: 2, threeMeals: true, representation: 3);
        foreach (var meal in city.State.Food!.Residents)
        {
            if (meal.Slot == 0) Assert.Null(meal.RepresentedTraits);
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

    // ---- Migration 13/14/15/16 -> 17 ----------------------------------------------------------------

    private static string S17_Old(string current, int schema)
    {
        var token = JObject.Parse(current);
        if (schema <= 14) LegacyMoneyWire.ToLegacy(token, wholeCents: schema == 13);
        return LegacyMoneyWire.AsSchema(token, schema).ToString(Formatting.None);
    }

    [Theory]
    [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)]
    public void S17_EarlierSchemasMigrateExactlyAndKeepMealsInProgressOnLegacyTerms(int schema)
    {
        var (city, market, routes) = Ready(count: 2, atWork: true, threeMeals: true, representation: 2);
        city.EnableHouseholdFinances();
        // Stop while a meal is under way: its committed shop, product, price and return must survive.
        double until = city.State.Food!.Residents.Min(m => m.DesiredAt);
        while (!city.State.Food.Residents.Any(m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving)) Advance(city, until += 1);
        string current = LegacyWire.Write(city.State);
        string old = S17_Old(current, schema);
        Assert.Contains("\"Schema\":" + schema + ",", old);
        foreach (string field in EconomyScenario.Schema17Fields) Assert.DoesNotContain("\"" + field + "\":", old);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = old };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(schema, read.SourceSchema);
        Assert.Same(old, data[SaveStore.StateKey]);
        var migrated = read.State!;
        // Everything the old payload held is preserved byte for byte: IDs, assignments, sequence, money units,
        // budgets, shop authority, receipts, active meals, returns and optional-stop choices.
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(current), LegacyMoneyWire.WithoutUpgradeFacts(LegacyWire.Write(migrated)));
        int today = (int)(migrated.Minute / 1440);
        Assert.Equal(FoodChoicePolicy.Version, migrated.Food!.ChoicePolicyVersion);
        Assert.Equal(today + 1, migrated.Food.ChoiceStartsDay);
        Assert.Equal(today + 1, city.State.Food.ChoiceStartsDay);
        foreach (var person in migrated.Citizens) Assert.Equal(ResidentTraits.Generate(migrated.Seed, person.Identity.Id).Bands, person.Traits!.Bands);
        foreach (var meal in migrated.Food.Residents)
        {
            Assert.Null(meal.Decision); Assert.Equal("", meal.LastContextKey); Assert.Equal(0, meal.ContextAttempts);
            Assert.Equal(meal.Slot == 0 ? null : ResidentTraits.Generate(migrated.Seed, meal.ResidentId).Bands, meal.RepresentedTraits?.Bands);
            var outcomes = meal.Today!;
            Assert.Equal(meal.Day, outcomes.Day);
            Assert.Equal(MealOutcomes.Index(meal.Kind), outcomes.Unrecorded);
            var status = MealOutcomes.StatusFor(meal.Status);
            Assert.Equal(status.HasValue ? 1 : 0, outcomes.Meals.Count);
        }
        // The upgraded payload is now writable through the normal save path, and reads back as 17.
        Assert.True(SaveStore.WriteTo(data, migrated).Success);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.Read(data[SaveStore.StateKey]).SourceSchema);
        // Continue both: identical orders and economy, the meal in progress finishing on its legacy terms.
        var twin = new Market { Units = market.Units }; twin.Orders.AddRange(market.Orders); twin.Times.AddRange(market.Times);
        var resumed = new CitySimulator(migrated, routes); resumed.AttachFoodMarket(twin);
        Advance(city, until + 2 * 1440); Advance(resumed, until + 2 * 1440);
        Assert.Equal(market.Orders, twin.Orders); Assert.Equal(market.Times, twin.Times);
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(LegacyWire.Write(city.State)), LegacyMoneyWire.WithoutUpgradeFacts(LegacyWire.Write(resumed.State)));
        Assert.Equal(today + 1, resumed.State.Food!.ChoiceStartsDay);
    }

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
        var first = food.Residents.First(m => m.Slot == 0);
        first.Today = new ResidentFoodToday { Day = 0, Meals = {
            new MealOutcomeRecord { Kind = MealKind.Breakfast, Status = MealOutcomeStatus.ChoseAlternative, Reason = MealReasonCode.PrefersAlternative, Outside = FoodOutsideOption.FundedHouseholdFood, At = 500 },
            new MealOutcomeRecord { Kind = MealKind.Lunch, Status = MealOutcomeStatus.Skipped, Reason = MealReasonCode.QueueTooLong, At = 760, Factor1 = MealFactor.Queue },
            new MealOutcomeRecord { Kind = MealKind.Dinner, Status = MealOutcomeStatus.Bought, Reason = MealReasonCode.Bought, PlaceId = "address:Gone%20Street:1:shop", ProductId = "pastry", At = 1150, CheckoutAt = 1150 } } };
        var decided = food.Residents.Last(m => m.Slot == 1);
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
        return (city, LegacyWire.Write(city.State));
    }

    [Fact]
    public void S17_FullContractRoundTripsByteIdentically()
    {
        var (city, json) = S17_Full();
        Assert.Contains("\"Schema\":17,", json);
        Assert.Contains("\"ChoicePolicyVersion\":1,\"ChoiceStartsDay\":0,", json);
        Assert.Contains("\"Traits\":{\"Bands\":\"" + city.State.Citizens[0].Traits!.Bands + "\",\"Version\":1}", json);
        var read = SaveCodec.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(17, read.SourceSchema);
        Assert.Equal(json, LegacyWire.Write(read.State!));
        var decision = read.State!.Food!.Residents.Single(m => m.Decision != null).Decision!;
        Assert.True(decision.RouteBearing); Assert.Equal(2, decision.Shops[0].Offers.Count); Assert.Equal(-1, decision.Shops[0].Offers[1].Units);
        var data = new Dictionary<string, string>();
        var session = SaveSession.Open(data, city.State.PopulationKey);
        Assert.True(session.WriteTo(data, read.State).Success); Assert.Equal(SaveCodec.Write(read.State), data[SaveStore.StateKey]);
    }

    public static IEnumerable<object[]> S17_Damage() => new (string name, string error)[]
    {
        ("traits-missing", "Missing object fields"), ("traits-null", "Missing resident traits"), ("traits-version", "Unknown resident trait version"),
        ("traits-band", "Invalid resident trait bands"), ("traits-short", "Invalid resident trait bands"), ("traits-letter", "Invalid resident trait bands"),
        ("traits-unknown-field", "Unknown field: Extra"), ("slot0-represented", "Slot zero duplicates"), ("slot1-unrepresented", "Missing represented consumer traits"),
        ("represented-band", "Invalid represented consumer trait bands"),
        ("policy-version", "Unknown food choice policy"), ("activation-negative", "Invalid food choice activation day"),
        ("activation-future", "Invalid food choice activation day"), ("policy-missing", "Missing object fields"),
        ("today-missing", "Missing object fields"), ("today-future", "Invalid current-day meal outcomes"),
        ("today-duplicate", "Duplicate or unordered meal outcomes"), ("today-oversized", "Oversized collection"),
        ("today-unrecorded-overlap", "Outcome recorded for a meal marked unrecorded"), ("today-unrecorded-range", "Invalid unrecorded meal count"),
        ("today-bought-without-shop", "Purchase outcome lost its shop or product"), ("today-fractional-minute", "Invalid meal outcome record"),
        ("today-after-clock", "Invalid meal outcome time"), ("today-receipt-mismatch", "Meal outcome differs from its receipt state"),
        ("today-legacy-kind", "Meal outcome kind differs from rhythm policy"), ("today-unknown-status", "Unknown Meal outcome status"),
        ("today-alternative-on-skip", "Alternative recorded for a non-alternative outcome"), ("today-unconfirmed-reason", "Impossible unconfirmed outcome reason"),
        ("today-bought-reason", "Impossible purchase outcome reason"), ("today-checkout-after", "Invalid meal checkout time"),
        ("today-factors", "Invalid meal outcome factors"), ("today-record-not-string", "Invalid meal outcome record"),
        ("today-record-short", "Invalid meal outcome record"), ("today-record-leading-zero", "Invalid meal outcome record"),
        ("today-record-signed", "Invalid meal outcome record"), ("today-record-overflow", "Invalid meal outcome record"),
        ("decision-quote-cursor", "Decision quote progress differs from its cursor"), ("decision-quote-route", "Decision route differs from its quote state"),
        ("decision-quote-return-unasked", "Return quote without its outward quote"), ("decision-quote-state", "Unknown Decision quote state"),
        ("decision-chose-early", "Decision chose before its quotes were settled"), ("decision-selected-refused", "Decision selects an offer outside its shortlist"),
        ("decision-quote-count", "Invalid decision quote progress"), ("today-skip-names-shop", "Non-purchase outcome names a shop or product"),
        ("decision-compact-unknown-product", "Unknown live product code"), ("decision-quality", "Invalid decision quality evidence"),
        ("decision-hours", "Weekly opening window has no duration"), ("decision-hours-oversized", "Oversized collection"),
        ("decision-district", "Invalid decision district evidence"), ("decision-district-demand-range", "Invalid decision district evidence"),
        ("decision-intent", "Decision belongs to another policy or meal"), ("decision-policy", "Decision belongs to another policy or meal"),
        ("decision-latch", "Decision differs from its retry latch"), ("latch-five", "Invalid decision retry latch"),
        ("latch-key-only", "Invalid decision retry latch"), ("decision-nine-shops", "Oversized collection"),
        ("decision-five-offers", "Oversized collection"), ("decision-unknown-product", "Unknown live product code"),
        ("decision-two-route-bearing", "More than one route-bearing food decision"), ("decision-endpoint", "Inconsistent decision route endpoints"),
        ("decision-not-walking", "Decision route is not a walking path"), ("decision-outside-shortlist", "Decision selects an offer outside its shortlist"),
        ("decision-duplicate-shop", "Duplicate decision shop"), ("decision-service-nan", "Invalid decision service evidence"),
        ("decision-not-pending", "Decision outside a pending version-1 meal"), ("decision-before-activation", "Decision outside a pending version-1 meal"),
        ("decision-return-only", "Decision route differs from its quote state"), ("decision-unknown-shop", "Unknown decision shop"),
        ("meal-unknown-field", "Unknown field: Foo"), ("schema16-with-17-fields", "Unknown field"),
        ("integer-overflow", "Integer is out of range"), ("integer-huge", "Integer is out of range"),
        ("position-duplicate", "Unknown or duplicate position field"), ("position-huge", "Invalid position X"),
    }.Select(c => new object[] { c.name, c.error });

    private static readonly string[] S17_RecordFields = { "Kind", "Status", "Reason", "Outside", "Factor1", "Factor2", "At", "CheckoutAt", "ProductId", "PlaceId" };
    private static string S17_Get(JToken record, string field) => ((string)record!).Split('|', 10)[Array.IndexOf(S17_RecordFields, field)];
    private static void S17_Set(JArray records, int index, string field, object value)
    {
        var parts = ((string)records[index]!).Split('|', 10);
        parts[Array.IndexOf(S17_RecordFields, field)] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
        records[index] = string.Join("|", parts);
    }

    [Theory]
    [MemberData(nameof(S17_Damage))]
    public void S17_DamagedOrForeignSchema17IsRefusedAndPreserved(string damage, string error)
    {
        var (city, json) = S17_Full();
        var wire = JObject.Parse(json);
        var residents = (JArray)wire["Food"]!["Residents"]!;
        var slot0 = (JObject)residents.First(m => (int)m["Slot"]! == 0); var slot1 = (JObject)residents.First(m => (int)m["Slot"]! == 1);
        var decided = (JObject)residents.First(m => m["Decision"]!.Type != JTokenType.Null);
        var decision = (JObject)decided["Decision"]!; var shop = (JObject)decision["Shops"]![0]!;
        var today = (JObject)slot0["Today"]!; var meals = (JArray)today["Meals"]!;
        var traits = (JObject)wire["Citizens"]![0]!["Traits"]!;
        string? text = null;
        switch (damage)
        {
            case "traits-missing": ((JObject)wire["Citizens"]![0]!).Remove("Traits"); break;
            case "traits-null": wire["Citizens"]![0]!["Traits"] = null; break;
            case "traits-version": traits["Version"] = 2; break;
            case "traits-band": traits["Bands"] = "2222252222"; break;
            case "traits-short": traits["Bands"] = "222"; break;
            case "traits-letter": traits["Bands"] = "22222a2222"; break;
            case "traits-unknown-field": traits["Extra"] = 1; break;
            case "slot0-represented": slot0["RepresentedTraits"] = traits.DeepClone(); break;
            case "slot1-unrepresented": slot1["RepresentedTraits"] = null; break;
            case "represented-band": slot1["RepresentedTraits"]!["Bands"] = "9222222222"; break;
            case "policy-version": wire["Food"]!["ChoicePolicyVersion"] = 2; break;
            case "activation-negative": wire["Food"]!["ChoiceStartsDay"] = -1; break;
            case "activation-future": wire["Food"]!["ChoiceStartsDay"] = 3; break;
            case "policy-missing": ((JObject)wire["Food"]!).Remove("ChoiceStartsDay"); break;
            case "today-missing": slot0.Remove("Today"); break;
            case "today-future": today["Day"] = 2; break;
            case "today-duplicate": S17_Set(meals, 1, "Kind", (int)MealKind.Breakfast); break;
            case "today-oversized": meals.Add(meals[2]!.DeepClone()); break;
            case "today-unrecorded-overlap": today["Unrecorded"] = 1; break;
            case "today-unrecorded-range": meals.RemoveAll(); today["Unrecorded"] = 4; break;
            case "today-bought-without-shop": S17_Set(meals, 2, "PlaceId", ""); break;
            case "today-fractional-minute": S17_Set(meals, 1, "At", "760.5"); break;
            case "today-record-not-string": meals[1] = new JObject { ["Kind"] = 2 }; break;
            case "today-record-short": meals[1] = "2|2|11|0|0|0|760|0|"; break;
            case "today-record-leading-zero": S17_Set(meals, 1, "At", "0760"); break;
            case "today-record-signed": S17_Set(meals, 1, "CheckoutAt", "-0"); break;
            case "today-record-overflow": S17_Set(meals, 1, "At", "2147483648"); break;
            case "today-after-clock": S17_Set(meals, 2, "At", 1440 + 1); break;
            case "today-receipt-mismatch":
                // The pending day-1 breakfast cannot already have been bought.
                today["Day"] = 1; S17_Set(meals, 2, "Kind", (int)MealKind.Breakfast); S17_Set(meals, 2, "At", 1440); S17_Set(meals, 2, "CheckoutAt", 1440);
                meals.Insert(0, meals[2]!.DeepClone()); while (meals.Count > 1) meals.RemoveAt(1); break;
            case "today-legacy-kind": S17_Set(meals, 0, "Kind", (int)MealKind.Legacy); break;
            case "today-unknown-status": S17_Set(meals, 0, "Status", 9); break;
            case "today-alternative-on-skip": S17_Set(meals, 1, "Outside", (int)FoodOutsideOption.PackedMeal); break;
            case "today-unconfirmed-reason": S17_Set(meals, 2, "Status", (int)MealOutcomeStatus.Unconfirmed); break;
            case "today-bought-reason": S17_Set(meals, 1, "Reason", (int)MealReasonCode.Bought); break;
            case "today-checkout-after": S17_Set(meals, 2, "CheckoutAt", 1200); break;
            case "today-factors": S17_Set(meals, 1, "Factor2", (int)MealFactor.Queue); break;
            case "today-skip-names-shop": S17_Set(meals, 1, "PlaceId", "shop"); break;
            case "decision-quote-cursor": decision["QuotesDone"] = 1; decision["Result"] = (int)FoodDecisionResult.Evaluating; decision["BusinessId"] = ""; decision["ProductId"] = ""; break;
            case "decision-quote-route": shop["ReturnQuote"] = (int)FoodQuoteState.Refused; break;
            case "decision-quote-return-unasked": shop["OutwardQuote"] = (int)FoodQuoteState.Refused; shop["Outward"] = null; shop["Return"] = null;
                shop["ReturnQuote"] = (int)FoodQuoteState.Refused; decision["Result"] = (int)FoodDecisionResult.NoFeasible; decision["BusinessId"] = ""; decision["ProductId"] = ""; break;
            case "decision-quote-state": shop["ReturnQuote"] = 7; break;
            case "decision-chose-early": decision["QuotesDone"] = 1; shop["ReturnQuote"] = (int)FoodQuoteState.Unrequested; shop["Return"] = null; break;
            case "decision-selected-refused": shop["ReturnQuote"] = (int)FoodQuoteState.Refused; shop["Return"] = null; break;
            case "decision-quote-count": decision["QuotesDone"] = 3; break;
            case "decision-compact-unknown-product": decision["Shops"] = new JArray(); decision["ProductId"] = "unsupported-live-product"; break;
            case "decision-quality": shop["CustomerServicePercent"] = 101; break;
            case "decision-hours": shop["Hours"]![0]!["ToMinute"] = 420; break;
            case "decision-hours-oversized": for (int i = 0; i < 63; i++) ((JArray)shop["Hours"]!).Add(shop["Hours"]![0]!.DeepClone()); break;
            case "decision-district": shop["DistrictId"] = ""; break;
            case "decision-district-demand-range": shop["Offers"]![0]!["DistrictDemandPercent"] = 101; break;
            case "decision-intent": decision["IntentId"] = "other"; break;
            case "decision-policy": decision["PolicyVersion"] = 2; break;
            case "decision-latch": decided["ContextAttempts"] = 2; break;
            case "latch-five": slot0["LastContextKey"] = "x"; slot0["ContextAttempts"] = 5; break;
            case "latch-key-only": slot0["LastContextKey"] = "x"; break;
            case "decision-nine-shops":
                for (int i = 0; i < 8; i++) { var copy = shop.DeepClone(); copy["BusinessId"] = "b" + i; ((JArray)decision["Shops"]!).Add(copy); }
                break;
            case "decision-five-offers":
                for (int i = 0; i < 3; i++) ((JArray)shop["Offers"]!).Add(shop["Offers"]![0]!.DeepClone());
                break;
            case "decision-unknown-product": shop["Offers"]![1]!["ProductId"] = "pastry"; break;
            case "decision-two-route-bearing":
            {
                var other = (JObject)residents.First(m => m != decided && (int)m["Slot"]! == 1);
                var copy = (JObject)decision.DeepClone();
                var otherMeal = city.State.Food!.Residents.Single(m => m.ResidentId == (string)other["ResidentId"]!);
                copy["IntentId"] = otherMeal.IntentId;
                other["Decision"] = copy; other["LastContextKey"] = "work@1440"; other["ContextAttempts"] = 1;
                break;
            }
            case "decision-endpoint": shop["Outward"]!["Legs"]![1]!["To"]!["X"] = 5.0; break;
            case "decision-not-walking": shop["Return"]!["Legs"]![0]!["Mode"] = (int)TravelMode.Car; break;
            case "decision-outside-shortlist": decision["ProductId"] = FoodProducts.HotDog; break;
            case "decision-duplicate-shop": ((JArray)decision["Shops"]!).Add(shop.DeepClone()); decision["QuotesDone"] = 4; break;
            case "decision-service-nan": text = json.Replace("\"MealsPerHour\":15.0", "\"MealsPerHour\":NaN"); break;
            case "decision-not-pending": decided["Status"] = (int)MealStatus.Declined; break;
            case "decision-before-activation": wire["Food"]!["ChoiceStartsDay"] = 2; break;
            case "decision-return-only": shop["Outward"] = null; break;
            case "decision-unknown-shop": shop["PlaceId"] = "home"; break;
            case "meal-unknown-field": slot0["Foo"] = 1; break;
            case "schema16-with-17-fields": wire["Schema"] = 16; break;
            case "integer-overflow": wire["Citizens"]![0]!["Identity"]!["Age"] = 2147483648L; break;
            case "integer-huge": text = json.Replace("\"Age\":30,", "\"Age\":999999999999999999999999,"); break;
            case "position-duplicate": text = json.Replace("\"Position\":{\"X\":0.0,\"Y\":0.0,\"Z\":0.0}", "\"Position\":{\"X\":0.0,\"X\":0.0,\"Z\":0.0}"); break;
            case "position-huge": text = json.Replace("\"Position\":{\"X\":0.0,\"Y\":0.0,\"Z\":0.0}", "\"Position\":{\"X\":100000000000000000000000,\"Y\":0.0,\"Z\":0.0}"); break;
            default: throw new ArgumentException(damage);
        }
        string payload = text ?? wire.ToString(Formatting.None);
        Assert.NotEqual(json, payload);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "owned by RLO" };
        Assert.False(SaveStore.WriteTo(data, city.State).Success);
        Assert.False(SaveSession.Open(data, city.State.PopulationKey).WriteTo(data, city.State).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("owned by RLO", data["rlo:state"]);
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
        string json = LegacyWire.Write(city.State);
        var read = SaveCodec.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(json, LegacyWire.Write(read.State!));
        var restored = read.State!.Food!.Residents.Single(m => m.Decision != null).Decision!;
        Assert.Equal(4, restored.QuotesDone);
        Assert.Equal(new[] { (FoodQuoteState.Refused, FoodQuoteState.NotNeeded), (FoodQuoteState.Answered, FoodQuoteState.Refused), (FoodQuoteState.Unrequested, FoodQuoteState.Unrequested) },
            restored.Shops.Select(x => (x.OutwardQuote, x.ReturnQuote)));
        Assert.Null(restored.Shops[0].Outward); Assert.NotNull(restored.Shops[1].Outward); Assert.Null(restored.Shops[1].Return);
        // Before b2's return was asked, the same image differs: unrequested is not refused.
        refusedReturn.ReturnQuote = FoodQuoteState.Unrequested; decision.QuotesDone = 3;
        Assert.NotEqual(json, LegacyWire.Write(city.State));
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
        string json = LegacyWire.Write(city.State);
        var read = SaveCodec.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(json, LegacyWire.Write(read.State!));
        Assert.Equal(553, 6 * 2 + 2 * 10 + 9 + 256 + 256);
        // An ID one character longer is refused at write, so it never reaches the reader.
        dinner.PlaceId += "p";
        Assert.Throws<SaveValidationException>(() => LegacyWire.Write(city.State));
        // A record string beyond the reader's cap is refused before deserialization and preserved.
        string oversized = json.Replace(wire, wire.Substring(0, wire.Length - 256) + new string('p', SaveCodec.MaxMealOutcomeWire - (wire.Length - 256) + 1));
        Assert.NotEqual(json, oversized);
        var refused = SaveCodec.Read(oversized);
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
        Assert.Contains("cannot contain", Assert.Throws<SaveValidationException>(() => LegacyWire.Write(city.State)).Message);
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

    private sealed class S17_NoMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double minute) => throw new InvalidOperationException("No checkout here.");
    }

    // The supported roster limit (residents x factor, up to 150,000) is unchanged by schema 17: a city above
    // 130,000 consumers loads from schema 16, upgrades, and saves and reloads at its largest end-of-day state.
    [Fact]
    public void S17_LargeRepresentedCitiesAboveOneHundredThirtyThousandConsumersMigrateAndSave()
    {
        var state = EconomyScenario.Create(43400, 5);
        state.Food = EconomyScenario.Food(); state.Food.RepresentationFactor = 3;
        var city = new CitySimulator(state, new Walking()); city.AttachFoodMarket(new S17_NoMarket()); city.EnableHouseholdFinances();
        Assert.Equal(130200, state.Food.Residents.Count);
        // Schema-16 image: the same city as an earlier writer produced it.
        string fresh = LegacyWire.Write(state);
        string old = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(fresh,
                "\"ChoicePolicyVersion\":1,\"ChoiceStartsDay\":[0-9]+,", ""),
                ",\"(Traits|RepresentedTraits)\":(null|\\{\"Bands\":\"[0-4]{10}\",\"Version\":1\\})|,\"Today\":null|,\"Decision\":null|,\"LastContextKey\":\"\"|,\"ContextAttempts\":0", ""),
                "\"Schema\":17,", "\"Schema\":16,");
        Assert.DoesNotContain("\"Traits\":", old);
        var migrated = SaveCodec.Read(old);
        Assert.True(migrated.Status == SaveReadStatus.Loaded, migrated.Error); Assert.Equal(16, migrated.SourceSchema);
        Assert.Equal(130200, migrated.State!.Food!.Residents.Count);
        migrated = null!; old = null!; fresh = null!;
        // End of day: three purchases per consumer at native-format shop addresses.
        int day = (int)(state.Minute / 1440) - 1;
        var today = new ResidentFoodToday { Day = day };
        int kind = 1;
        foreach (var (minute, street, product) in new[] { (487, "ba:street_thirdstreet", FoodProducts.Croissant), (763, "ba:street_harborroad", FoodProducts.Burger),
            (1151, "ba:street_martinlutherkingjrblvd", FoodProducts.HotDog) })
            today.Meals.Add(new MealOutcomeRecord { Kind = (MealKind)kind++, Status = MealOutcomeStatus.Bought, Reason = MealReasonCode.Bought,
                At = day * 1440 + minute, CheckoutAt = day * 1440 + minute, ProductId = product, PlaceId = "address:" + Uri.EscapeDataString(street) + ":1042:shop" });
        foreach (var meal in state.Food.Residents) meal.Today = today;
        string json = LegacyWire.Write(state);
        Assert.True(json.Length <= SaveCodec.MaxInputCharacters);
        var read = SaveCodec.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(130200, read.State!.Food!.Residents.Count(m => m.Today!.Meals.Count == 3));
    }

    [Fact]
    public void S17_FutureSchemasAreRefusedAsFutureAndPreserved()
    {
        var (city, json) = S17_Full();
        foreach (int schema in new[] { 20, 21, 1000 })
        {
            var wire = JObject.Parse(json); wire["Schema"] = schema; string payload = wire.ToString(Formatting.None);
            Assert.Equal(SaveReadStatus.Future, SaveCodec.Read(payload).Status);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
            Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Same(payload, data[SaveStore.StateKey]);
        }
        // An earlier schema cannot carry schema-17 fields, and a schema-17 payload cannot omit them.
        var stripped = LegacyMoneyWire.AsSchema(JObject.Parse(json), 16); stripped["Schema"] = 17;
        Assert.Contains("Missing object fields", SaveCodec.Read(stripped.ToString(Formatting.None)).Error);
    }

    [Theory]
    [InlineData("other-shop")] [InlineData("other-product")] [InlineData("erased-checkout")] [InlineData("moved-checkout")]
    public void S17_CurrentPurchaseOutcomeMustMatchItsRetainedMealAndIsRefusedAndPreserved(string damage)
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var (city, _, _) = Ready(atWork: true, threeMeals: true);
            var meal = city.State.Food!.Residents.Single();
            double until = meal.DesiredAt;
            while (meal.Status != MealStatus.Paid) Advance(city, until += 1);
            string current = LegacyWire.Write(city.State);
            // The same paid meal, recorded live and synthesized by a schema-16 upgrade.
            string json = upgraded ? LegacyWire.Write(SaveCodec.Read(LegacyMoneyWire.AsSchema(JObject.Parse(current), 16).ToString(Formatting.None)).State!) : current;
            var record = meal.Today!.Find(meal.Kind)!;
            Assert.Equal((MealOutcomeStatus.Bought, meal.PlaceId, meal.ProductId, (int)Math.Floor(meal.ServiceAt)),
                (record.Status, record.PlaceId, record.ProductId, record.CheckoutAt));
            var wire = JObject.Parse(json);
            var outcomes = (JArray)wire["Food"]!["Residents"]![0]!["Today"]!["Meals"]!;
            int index = outcomes.ToList().FindIndex(m => S17_Get(m, "Kind") == ((int)meal.Kind).ToString());
            switch (damage)
            {
                case "other-shop": S17_Set(outcomes, index, "PlaceId", "work"); break;
                case "other-product": S17_Set(outcomes, index, "ProductId", FoodProducts.HotDog); break;
                case "erased-checkout": S17_Set(outcomes, index, "CheckoutAt", 0); break;
                case "moved-checkout": S17_Set(outcomes, index, "CheckoutAt", int.Parse(S17_Get(outcomes[index], "CheckoutAt")) - 1); break;
            }
            string payload = wire.ToString(Formatting.None);
            var read = SaveCodec.Read(payload);
            Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Contains("Purchase outcome differs from its retained meal", read.Error);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
            Assert.False(SaveStore.WriteTo(data, city.State).Success);
            Assert.False(SaveSession.Open(data, city.State.PopulationKey).WriteTo(data, city.State).Success);
            Assert.Same(payload, data[SaveStore.StateKey]);
            // Damaging the live state the same way is refused at write as well.
            var bad = SaveCodec.Read(json).State!;
            var live = bad.Food!.Residents.Single().Today!.Find(meal.Kind)!;
            if (damage == "other-shop") live.PlaceId = "work"; else if (damage == "other-product") live.ProductId = FoodProducts.HotDog;
            else if (damage == "erased-checkout") live.CheckoutAt = 0; else live.CheckoutAt--;
            Assert.Throws<SaveValidationException>(() => LegacyWire.Write(bad));
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
        var result = JObject.FromObject(EconomyScenario.Run(legacyChoice:true));
        Assert.Equal(849, (int)result["orders"]!);
        Assert.Equal(797, (int)result["purchases"]!);
        Assert.Equal(107967424757760L, (long)result["spentUnits"]!);
        Assert.Equal(2, (int)result["uncertain"]!);
        Assert.Equal("1B94987B3158044594B9C4D6A5473BA22BE0D4F3488D769CBBEE23318BB33B91", (string)result["ordersDigest"]!);
        Assert.Equal("74D0FE8081EA795D4D56A11B4B955DC6E777E3822C702D3C09BC9477E6679AF9", (string)result["economyDigest"]!);
        Assert.Equal("59B6F14D44CA0F996C5A828E2E538B52C5426347FCFA40767527B72D77F1D4FA", (string)result["reloadDigest"]!);
    }
}
