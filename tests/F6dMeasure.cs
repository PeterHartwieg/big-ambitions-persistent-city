using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Activity = PersistentCity.Core.Activity;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// Explicitly requested offline save measurement; no game/rig or regression matrix. Set PC_F6D_MEASURE to JSONL path.
[Collection(StreetFoodHooks.Collection)]
public sealed class F6dMeasure
{
    private readonly ITestOutputHelper output;
    public F6dMeasure(ITestOutputHelper output) { this.output = output; }
    private static readonly string[] Products = { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog,
        "ba:itemname_donut", "ba:itemname_cupcake", "ba:itemname_salad", "foodtrucks:itemname_dumplings", "foodtrucks:itemname_choppedcheese" };
    internal static CityState Build(int residents, int factor, int visits, bool cf)
    {
        // E5d's seed/city/32-or-64 visits, rebuilt as fresh F5 independent persons, never migrated.
        var state = EconomyScenario.Create(residents, 11);
        state.Minute += 12 * 1440;
        foreach (var p in state.Citizens) { p.NextEventMinute += 12 * 1440; p.ActivityUntilMinute += 12 * 1440; }
        state.Food = EconomyScenario.Food(); state.Food.RepresentationFactor = factor;
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        var city = new CitySimulator(state, new RepresentativeFixture.Routes()); city.AttachFoodMarket(new RepresentativeFixture.Market()); city.EnableHouseholdFinances();
        state.PrepareForSave();
        foreach (var p in state.Citizens)
        {
            p.RecentVisits.Clear();
            for (int j = 0; j < visits; j++) p.RecentVisits.Add(new VisitRecord { AtMinute = j * 17.125 + .123456789,
                PlaceId = j % 2 == 0 ? p.HomeId : "shop-1", Activity = j % 2 == 0 ? Activity.Home : Activity.Shop });
        }
        var places = state.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        for (int i = places.Count; i < 1024; i++)
        { var p = new Place { Id = "name25:shop:" + i, Name = "Historical shop", Kind = PlaceKind.Shop }; state.Places.Add(p); places.Add(p); }
        var food = state.Food;
        food.Shops.Clear();
        for (int i = 0; i < (cf ? 896 : 32); i++) food.Shops.Add(new FoodScope { PlaceId = places[i].Id, BusinessId = "name25:scope:" + i,
            ProductId = Products[0], AdditionalProductIds = Products.Skip(1).ToList() });
        if (cf) VendorSaveTests.Fill(city, 64, 64, 1024, references: true);
        var memory = new FoodMemoryStore(food);
        foreach (var scope in food.Shops) Assert.True(memory.RegisterCampaign(scope.BusinessId, scope.PlaceId) >= 0);
        while (food.BusinessReferences.Count < 20000)
        {
            int slot = food.BusinessReferences.Count;
            food.BusinessReferences.Add(new BusinessReference { Slot = slot, BusinessId = "business:fixture:" + slot.ToString("D5") + ":created:17:food:player:generation:1", PlaceId = "shop-1" });
        }
        // Consume the full B table's 2Mi wire allowance, including the vendor references.
        int tableSize = 1 + food.BusinessReferences.Sum(r => 1 + ("[1," + r.Slot + ",\"" + r.BusinessId + "\",\"000\"]").Length);
        int padding = 2 * 1024 * 1024 - tableSize;
        var generic = food.BusinessReferences.Where(r => r.BusinessId.StartsWith("business:fixture:", StringComparison.Ordinal)).ToArray();
        for (int i = 0; i < generic.Length; i++) { int extra = padding / (generic.Length - i); padding -= extra; generic[i].BusinessId += new string('X', extra); }
        for (int i = 0; i < food.Residents.Count; i++)
        {
            var meal = food.Residents[i];
            meal.LastContextKey = "trip:" + new string('x', 44); meal.ContextAttempts = 4;
            meal.Memory = new ConsumerMemory { Watermark = new ExperienceWatermark { Day = 0, Meal = MealKind.Dinner, Context = 4, Stage = ExperienceStage.Served },
                Entries = Enumerable.Range((i % 5000) * 4, 4).Select(slot => new ShopMemory { BusinessRef = slot, Day = 0, Served = 35, Affinity = 8, Outcome = ShopExperience.Served }).ToList() };
        }
        foreach (var p in state.Citizens) p.W = new WeatherCommitment(state.Minute, WeatherSample.Rain, true);
        // A fully quoted 8-shop x 8-offer held image, with exact detached dynamic-product metadata.
        var first = food.Residents[0];
        food.ChoiceStartsDay = first.Day; first.Status = MealStatus.Pending;
        state.MenuPolicy = FoodNameIdentity.DynamicWire;
        first.Decision = new FoodDecisionState { IntentId = first.IntentId, ContextKey = first.LastContextKey, Attempt = 4,
            OriginAt = state.Minute, Deadline = state.Minute + 90, QuotesDone = 16, Tuning = FoodDecisionTuning.Capture(new FoodChoiceSettings { FoodMenuMaximumOffersPerShop = 8 }) };
        for (int i = 0; i < 8; i++) first.Decision.Shops.Add(new FoodDecisionShop { BusinessId = "size-probe:" + i, PlaceId = "shop-1", MealsPerHour = 60,
            NextServiceAt = state.Minute, Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = Products.Select(p => new FoodDecisionOffer { ProductId = p, Price = Money.FromCents(100), Units = 100,
                Metadata = FoodProducts.Known(p) ? null : new FoodOfferMetadata(2, FoodMealClass.Food, 2, 100, 100, 100) }).ToList(),
            OutwardQuote = FoodQuoteState.Answered, ReturnQuote = FoodQuoteState.Answered,
            Outward = new Route { Legs = { new RouteLeg { From = state.Places.First(p => p.Id == state.Citizens[0].PlaceId).Position, To = places[0].Position, Mode = TravelMode.Walk, Minutes = 1 } } },
            Return = new Route { Legs = { new RouteLeg { From = places[0].Position, To = state.Places.First(p => p.Id == state.Citizens[0].PlaceId).Position, Mode = TravelMode.Walk, Minutes = 1 } } } });
        for (int day = 0; day < 14; day++) for (int i = 0; i < 1024; i++)
        {
            var row = new FoodShopDay { Day = day, PlaceId = places[i].Id, BusinessId = "name25:history:" + i,
                Purchases = 8, Revenue = Money.FromCents(8), PurchaseMeals = new long[] { 8, 0, 0, 0 }, BuyerDistricts = { new FoodBuyerDistrict { DistrictId = "a", Purchases = 8 } } };
            row.PurchaseHours[0] = 8;
            foreach (string p in Products) row.ProductSales.Add(new FoodProductSales { ProductId = p, Purchases = 1, Revenue = Money.FromCents(1), PurchaseMeals = new long[] { 1, 0, 0, 0 } });
            food.History.Add(row);
        }
        if (cf)
        {
            // Actual 14,000 places, with the full E5d 12Mi existing-state allowance carried separately below.
            for (int i = state.Places.Count; i < 14000; i++) state.Places.Add(new Place { Id = "address:cf:" + i + ":shop", Name = "Historical shop " + i, District = "a", Kind = PlaceKind.Shop, Position = new Position(i, 0, 0) });
            // Keep the C5c maximum research section inside the same composition (fixture has no native billing).
            state.Research = ResearchSaveTests.AllStates();
        }
        return state;
    }
    internal static void FillLedger(CityState state)
    {
        EconomicLedgerTests.Populate(state);
        state.Economy.Outside.Clear();
        BigInteger wide = BigInteger.Parse(new string('9', 26));
        for (int day = 0; day < 14; day++) state.Economy.Outside.Add(new OutsideSpendingDay { Day = day, RetailUnits = wide, RetailVisits = wide, LeisureUnits = wide, LeisureVisits = wide });
        state.Economy.Shops.Clear();
        var store = new FoodMemoryStore(state.Food!);
        foreach (var scope in state.Food!.Shops)
        {
            var shop = new ShopObservation { BusinessRef = store.Find(scope.BusinessId), Fingerprint = EconomicLedgerTests.Fingerprint };
            for (int i = 0; i < EconomicLedger.ChangesPerShop; i++) shop.Changes.Add(new ShopChange { ObservedMinute = (int)state.Minute,
                Kind = ShopChangeKind.Price, ProductId = Products[i], Before = Money.FromCents(Money.MaximumNativePriceCents).Units - 1, After = Money.FromCents(Money.MaximumNativePriceCents).Units });
            state.Economy.Shops.Add(shop);
        }
        state.Economy.Shops.Sort((a, b) => a.BusinessRef.CompareTo(b.BusinessRef));
        foreach (var row in state.Food.History) Array.Fill(row.PeriodCauses.Counts, MealPeriodCauses.MaximumCount);
    }
    private static object Stats(List<double> samples)
    {
        samples.Sort(); return new { count = samples.Count, p50 = samples[samples.Count / 2], p95 = samples[(int)Math.Ceiling(samples.Count * .95) - 1], max = samples[^1], spikes30 = samples.Count(v => v >= 30) };
    }
    [Fact]
    public void MeasureAndCheckComposedSaveBudget()
    {
        string? path = Environment.GetEnvironmentVariable("PC_F6D_MEASURE"); if (path == null) return;
        foreach (string name in (Environment.GetEnvironmentVariable("PC_F6D_FIXTURES") ?? "957x2,957x3,5000x2,5000x3,50000x2,50000x3,CF32,CF64").Split(','))
        {
            bool cf = name.StartsWith("CF", StringComparison.Ordinal);
            int residents = cf ? 50000 : int.Parse(name.Split('x')[0]), factor = cf ? 3 : int.Parse(name.Split('x')[1]);
            var state = Build(residents, factor, name == "CF64" ? 64 : 32, cf);
            string inactive = SaveCodec.Write(state);
            var inactiveRecords = SaveCodec.CountRecords(state, inactive);
            var previousRecords = inactiveRecords;
            FillLedger(state);
            string after = SaveCodec.Write(state);
            var records = SaveCodec.CountRecords(state, after);
            int expectedRecords = state.Economy.Shops.Sum(s => 1 + s.Changes.Count) + 14;
            Assert.Equal(expectedRecords, records.Streaming - previousRecords.Streaming);
            Assert.Equal(expectedRecords, records.Semantic - previousRecords.Semantic);
            var write = new List<double>(); var read = new List<double>();
            int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F6D_SAMPLES"), out int n) ? n : 3;
            for (int i = 0; i < samples; i++)
            {
                long t = Stopwatch.GetTimestamp(); string again = SaveCodec.Write(state); write.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds); Assert.Equal(after, again);
                t = Stopwatch.GetTimestamp(); var result = SaveWire.Read(after); read.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                Assert.True(result.Status == SaveReadStatus.Loaded, result.Error);
                Assert.Equal(after, SaveCodec.Write(result.State!));
            }
            // Conservative: retain every byte/record of E5d's outstanding B-E reserve AND its full 12Mi existing
            // growth allowance, even though this image already contains the places, scopes and full histories.
            const int reserveChars = 19386774 + 540672 + 12582912, reserveRecords = 12710 + 320;
            long composed = after.Length + reserveChars + (long)state.Food!.Residents.Count * 13;
            int composedRecords = Math.Max(records.Streaming, records.Semantic) + reserveRecords;
            if (cf)
            {
                Assert.True(composed <= SaveCodec.MaxInputCharacters, $"{name}: {composed} exceeds hard cap");
                Assert.True(composedRecords <= SaveCodec.MaxEmbeddedRecords);
                if (name == "CF32") Assert.True(composed <= Math.Floor(SaveCodec.MaxInputCharacters * .85), $"CF32: {composed} exceeds 85%");
            }
            string row = JsonConvert.SerializeObject(new { name, schema = CityState.CurrentSchema, residents, persons = PersonTable.PersonCount(state), places = state.Places.Count,
                inactive = new { characters = inactive.Length, bytes = Encoding.UTF8.GetByteCount(inactive), inactiveRecords.Streaming, inactiveRecords.Semantic },
                after = new { characters = after.Length, bytes = Encoding.UTF8.GetByteCount(after), records.Streaming, records.Semantic },
                addedCharacters = after.Length - inactive.Length, addedRecords = expectedRecords, composed, composedRecords, reserveChars, reserveRecords,
                memoryHeadroom = (long)state.Food.Residents.Count * 13, writeMs = Stats(write), readMs = Stats(read), sha256 = EconomicLedgerTests.Sha(after) });
            File.AppendAllText(path, row + Environment.NewLine); output.WriteLine(row);
            // Release the two large payload strings and graph before the next fixture.
            GC.Collect();
        }
    }
    [Fact]
    public void FourteenDaysAndEightJournalEntriesAtTheirBounds()
    {
        var state = Build(8, 2, 32, false); FillLedger(state);
        EconomicLedgerTests.AssertRoundTrip(state);
        state.Economy.Shops[0].Changes.Add(state.Economy.Shops[0].Changes[0]);
        EconomicLedgerTests.WriteRefused(state);
    }
}
