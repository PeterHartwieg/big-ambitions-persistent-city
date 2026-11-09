using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>E6f: vehicles admit every name the held product policy executes (E5b4 Decision 14), as a whole vehicle.
/// Uses writer 36 unchanged. The vehicle market reads the policy once per logical minute.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class VendorDynamicProductTests
{
    internal const string Dumplings = FoodMetadataApiTests.Dumplings, ChoppedCheese = FoodMetadataApiTests.ChoppedCheese;
    internal const string Kabob = "ba:itemname_kabob", Donut = "ba:itemname_donut", Bagel = "ba:itemname_bagel", Muffin = "ba:itemname_muffin";
    /// <summary>A full eight-item vehicle: two Street Food mod products, vanilla and Bakery names, and two originals.</summary>
    internal static readonly string[] Eight = { Dumplings, ChoppedCheese, Kabob, Donut, Bagel, Muffin, FoodProducts.HotDog, FoodProducts.Burger };

    public VendorDynamicProductTests() => StreetFoodHooks.Reset();

    internal static FoodProductMetadataResolver Config(params (string Name, FoodProductMetadataOverride Value)[] entries)
        => new(entries.Select(e => new KeyValuePair<string, FoodProductMetadataOverride>(e.Name, e.Value)));
    internal static FoodProductPolicy Policy(FoodProductMetadataResolver? config = null, params string[] loaded)
        => new(config ?? FoodProductMetadataResolver.Empty, FoodMetadataApiTests.Catalogue(loaded.Length == 0 ? Eight : loaded));

    /// <summary>VehicleMarketTests' fixture with a product policy the test swaps.</summary>
    private sealed class Fixture
    {
        public CitySimulator City;
        public readonly VehicleMarketTests.Api Api = new();
        public StreetFoodVendorMarket Market;
        public Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
        public IFoodProductPolicy? Policy;
        public int PolicyReads;
        public long Now = VehicleMarketTests.Morning;
        private readonly WeightedFoodChoiceTests.Planner planner;
        public Fixture(IFoodProductPolicy? policy, params string[] items)
        {
            Policy = policy;
            (City, _, planner) = WeightedFoodChoiceTests.Ready(5, 2);
            StreetFoodHooks.Add("v00", x: 100, items: items.Length == 0 ? Eight : items);
            Market = Create(true); Run(Now);
            for (long m = Now + 15; m <= VehicleMarketTests.Morning + 60; m += 15) Run(m);
            StreetFoodHooks.Open("v00", 300); Run(Now + 1);
        }
        private StreetFoodVendorMarket Create(bool present) => new(City, present ? Api : null, "Street Food vendor API missing.",
            new VendorNavMeshSampler(), Receipts, () => Now, () => true, Policy == null ? null : () => { PolicyReads++; return Policy; });
        public void Run(long minute)
        {
            Now = StreetFoodHooks.Now = minute; StreetFoodHooks.Expire();
            while (Market.Prepare(minute)) { }
        }
        public VendorRow Row => City.State.Vendors.Find("v00")!;
        public string Refusal => Market.Coordinator.Refusals.Single(r => r.Key == "v00").Value;
        public ResidentMeal Meal(string product, int resident = 0, string? business = null)
        {
            var v = StreetFoodHooks.Find("v00"); int i = Array.IndexOf(v.Items, product);
            var row = City.State.Vendors.Find("v00");
            return new ResidentMeal { ResidentId = "customer" + resident, AnchorId = "customer" + resident, Day = (int)(Now / 1440),
                Kind = MealKind.Lunch, PlaceId = row?.PlaceId ?? "vendor:none", BusinessId = business ?? row!.BusinessId, ProductId = product,
                Price = Money.FromCents(v.Prices[i]), Allowance = Money.FromCents(1000), ServiceMinutes = 2, Status = MealStatus.Serving };
        }
        public void Reload(bool present)
        {
            Market.CompleteForSave();
            string payload = SaveCodec.Write(City.State);
            var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveCodec.Write(read.State!));
            Receipts = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(Receipts))!;
            City = new CitySimulator(read.State!, planner);
            Market = Create(present);
            Run(Now);
        }
    }

    // ---- Admission ----

    [Fact]
    public void DynamicNamesOnATestDoubleVehicleAreAdmittedServedAndSold()
    {
        // Without a policy (and so before E6f) the same vehicle is refused whole and stays with Street Food.
        var before = new Fixture(null);
        Assert.Null(before.City.State.Vendors.Find("v00"));
        Assert.Equal("menu item not executable: " + Dumplings, before.Refusal);
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner); Assert.Empty(before.Market.Read(before.Now));

        StreetFoodHooks.Reset();
        var f = new Fixture(Policy());
        Assert.Equal("", f.Row.Reason); Assert.True(f.Row.Authority.Desired);
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        Assert.True(f.Market.CanServe(f.Row.BusinessId));
        var business = Assert.Single(f.Market.Read(f.Now));
        Assert.Equal(Eight, business.Offers.Select(o => o.ProductId));
        // The vehicle scope holds the dynamic names exactly as Street Food reports them (name identity, no remapping).
        var scope = f.City.State.Food!.Shops.Single(s => s.Channel == FoodScopeChannel.Vehicle);
        Assert.Equal(Eight, scope.Products());
        foreach (var product in new[] { Dumplings, ChoppedCheese, Kabob, Donut })
        {
            var meal = f.Meal(product, Array.IndexOf(Eight, product));
            int units = StreetFoodHooks.Find("v00").Units[Array.IndexOf(Eight, product)];
            Assert.True(f.Market.Buy(meal, f.Now).Paid);
            Assert.Equal(product, f.Api.LastItem); Assert.Equal(units - 1, StreetFoodHooks.Find("v00").Units[Array.IndexOf(Eight, product)]);
            Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(f.Receipts, meal));
        }
        // The policy is read once per logical minute, not per item or per call.
        int reads = f.PolicyReads;
        for (int i = 0; i < 100; i++) { _ = f.Market.CanServe(f.Row.BusinessId); _ = f.Market.Read(f.Now); }
        Assert.Equal(reads, f.PolicyReads);
        f.Run(f.Now + 1); Assert.Equal(reads + 1, f.PolicyReads);
    }

    [Fact]
    public void ANameMissingFromTheLoadedImageOrNonCanonicalRefusesTheWholeVehicle()
    {
        var f = new Fixture(Policy(null, Eight.Where(n => n != Bagel).ToArray()));
        Assert.Null(f.City.State.Vendors.Find("v00"));
        Assert.Equal("menu item not executable: " + Bagel, f.Refusal);
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner); Assert.Empty(f.Market.Read(f.Now));
        StreetFoodHooks.Reset();
        var bad = new Fixture(Policy(), Dumplings, "bad|name");
        Assert.Equal("menu item is not a canonical name: bad|name", bad.Refusal);
    }

    [Theory]
    [InlineData(MealClass.Drink)] [InlineData(MealClass.Bundle)] [InlineData(MealClass.NonFood)] [InlineData(MealClass.Unknown)]
    public void AnExcludedClassOverrideKeepsTheWholeVehicleWithStreetFood(MealClass excluded)
    {
        var drink = Config((ChoppedCheese, new FoodProductMetadataOverride(excluded, 2, 900, 100, 450)));
        var f = new Fixture(Policy(drink));
        // No row is admitted: the vehicle never gets a business, a request or a vendor place.
        Assert.Null(f.City.State.Vendors.Find("v00"));
        Assert.Equal("menu item not executable: " + ChoppedCheese, f.Refusal);
        Assert.Empty(StreetFoodHooks.Wishes); Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.Empty(f.Market.Read(f.Now));
        Assert.DoesNotContain(f.City.State.Food!.Shops, s => s.Channel == FoodScopeChannel.Vehicle);
        // Checkout of a meal naming the vehicle's generation-1 business is refused before any API sale or receipt.
        var meal = f.Meal(Dumplings, business: VendorNames.BusinessId("v00", 1));
        Assert.False(f.Market.Buy(meal, f.Now).Paid);
        Assert.Equal(0, f.Api.SellCalls); Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(f.Receipts, meal));
    }

    [Theory]
    [InlineData(MealClass.Drink)] [InlineData(MealClass.Unknown)]
    public void AnOverrideWhileOwnedStopsCapturesAtOnceAndHandsBackAfterDrainWithoutANewRevision(MealClass excluded)
    {
        var f = new Fixture(Policy());
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        var committed = f.Meal(Dumplings); committed.Status = MealStatus.Travelling;
        f.City.State.Food!.Residents.Add(committed);
        long revision = StreetFoodHooks.Revision;
        f.Policy = Policy(Config((ChoppedCheese, new FoodProductMetadataOverride(excluded, 0, 0, 0, 0))));
        f.Run(f.Now + 1);
        Assert.Equal(revision, StreetFoodHooks.Revision);
        Assert.False(f.Row.Authority.Desired); Assert.Empty(f.Market.Read(f.Now));
        Assert.Null(f.Market.CaptureBusiness(f.Row.BusinessId, f.Now));
        // PC work at the served opening still holds the vehicle until the committed resident resolves.
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        committed.Status = MealStatus.Missed;
        f.Run(f.Now + 1); f.Run(f.Now + VendorCoordinator.WaitingRecheckMinutes);
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.Equal("menu item not executable: " + ChoppedCheese, f.Row.Reason);
        // The class returns to Food: the vehicle is requested again and served from a later opening.
        f.Policy = Policy();
        f.Run(f.Now + 1);
        Assert.True(f.Row.Authority.Desired); Assert.True(f.Row.Authority.Requested);
        StreetFoodHooks.Close("v00");
        for (int i = 0; i < 5; i++) f.Run(f.Now + 15); // lease renewals keep the request alive
        StreetFoodHooks.Open("v00", 300); f.Run(f.Now + 1);
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        Assert.True(f.Market.CanServe(f.Row.BusinessId));
    }

    [Fact]
    public void APolicyFallbackToTheOriginalThreeHandsADynamicVehicleBack()
    {
        var f = new Fixture(Policy());
        f.Policy = FoodProductPolicy.OriginalThree; // E5b4's fail-closed publication fallback
        f.Run(f.Now + 1); f.Run(f.Now + 1);
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.Equal("menu item not executable: " + Dumplings, f.Row.Reason);
        // An original-three vehicle is unaffected by the same fallback.
        StreetFoodHooks.Reset();
        var originals = new Fixture(FoodProductPolicy.OriginalThree, FoodProducts.HotDog, FoodProducts.Burger);
        Assert.True(originals.Market.CanServe(originals.Row.BusinessId));
    }

    [Fact]
    public void SteadyAdmissionOfAnEightItemDynamicMenuAllocatesNothingBeyondTheOriginalThreePath()
    {
        // CanServe's existing scope lookup allocates; the policy and its eight per-item checks add 0 B to it.
        long Bytes(Fixture f)
        {
            string business = f.Row.BusinessId;
            for (int i = 0; i < 100; i++) Assert.True(f.Market.CanServe(business));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) _ = f.Market.CanServe(business);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long original = Bytes(new Fixture(null, FoodProducts.HotDog, FoodProducts.Burger));
        StreetFoodHooks.Reset();
        Assert.Equal(original, Bytes(new Fixture(Policy())));
    }

    /// <summary>docs/perf/e6f.md. Measurement only: runs when PC_E6F_MEASURE is set, e.g.
    /// <c>PC_E6F_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~VendorDynamicProductTests.ChoiceCost --logger "console;verbosity=detailed"</c>.</summary>
    [Fact]
    public void ChoiceCostWithEightItemVehicleMenusAndDynamicNames()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PC_E6F_MEASURE"))) return;
        var lines = new List<string>();
        static (double p50, double p95, double max) Stats(List<double> values)
        {
            var s = values.OrderBy(v => v).ToList();
            return (s[s.Count / 2], s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], s[^1]);
        }
        static double Ms(long start) => (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        void Report(string name, List<double> ms, long bytes, long calls)
        {
            var (p50, p95, max) = Stats(ms);
            lines.Add($"{name}: p50 {p50:0.00000} ms, p95 {p95:0.00000} ms, max {max:0.00000} ms, {(double)bytes / calls:0.0} B/call, {ms.Count} samples");
        }
        // 1. Whole-vehicle admission (CanServe -> MenuRefusal over every item): 2 originals without a policy vs 8 items with one.
        foreach (var (label, policy, items) in new (string, IFoodProductPolicy?, string[])[] {
                     ("admission, 2 original items, no policy (E6d baseline)", null, new[] { FoodProducts.HotDog, FoodProducts.Burger }),
                     ("admission, 8 items (6 dynamic), held policy", Policy(), Eight) })
        {
            StreetFoodHooks.Reset();
            var f = new Fixture(policy, items); string business = f.Row.BusinessId;
            Assert.True(f.Market.CanServe(business));
            for (int i = 0; i < 20000; i++) f.Market.CanServe(business);
            var ms = new List<double>(); long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { long t = System.Diagnostics.Stopwatch.GetTimestamp(); for (int j = 0; j < 100; j++) f.Market.CanServe(business); ms.Add(Ms(t) / 100); }
            Report(label, ms, GC.GetAllocatedBytesForCurrentThread() - bytes, 100000);
            // 2. Capture of one vehicle business (what a decision context reads per vehicle candidate).
            for (int i = 0; i < 5000; i++) f.Market.CaptureBusiness(business, f.Now);
            ms = new List<double>(); bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { long t = System.Diagnostics.Stopwatch.GetTimestamp(); for (int j = 0; j < 10; j++) f.Market.CaptureBusiness(business, f.Now); ms.Add(Ms(t) / 10); }
            Report(label.Replace("admission", "capture"), ms, GC.GetAllocatedBytesForCurrentThread() - bytes, 10000);
        }
        // 3. One resident minute step of a running city with its decision, checkout and policy read: every step timed,
        //    original two-item vehicle vs eight-item dynamic vehicle. The decision step is the choice cost.
        foreach (var (label, items) in new[] { ("city minute, 2 original items", new[] { FoodProducts.HotDog, FoodProducts.Burger }), ("city minute, 8 items (6 dynamic)", Eight) })
        {
            var steps = new List<double>(); var decisions = new List<double>();
            for (int seed = -2; seed < 24; seed++) // two warm-up cities (JIT) are not recorded
            {
                StreetFoodHooks.Reset();
                var run = new CityRun(seed, items);
                bool decided = false, decidedNow = false;
                run.City.FoodDecisionObserved += _ => decidedNow = true;
                for (int i = 0; i < 2 * 1440 && !Resolved(run); i++)
                {
                    decidedNow = false;
                    long t = System.Diagnostics.Stopwatch.GetTimestamp(); run.Step(); double ms = Ms(t);
                    if (seed >= 0) steps.Add(ms);
                    if (!decided && decidedNow) { decided = true; if (seed >= 0) decisions.Add(ms); }
                }
                Assert.True(decided);
                Assert.Equal(0, run.City.HistoricalClockMisses);
            }
            Report(label + ", every step", steps, 0, 1);
            Report(label + ", the decision step", decisions, 0, 1);
            lines.Add($"{label}: steps >= 30 ms: {steps.Count(s => s >= 30)}; >= 5 ms: {steps.Count(s => s >= 5)}");
        }
        foreach (var line in lines) Console.WriteLine("E6F " + line);
        System.IO.File.WriteAllLines(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "e6f-measure.txt"), lines);
    }

    // ---- Taste ----

    [Fact]
    public void SeededTasteIsStableAcrossReloadAndCatalogueOrder()
    {
        var traits = new ResidentTraits { Bands = "0123401234" };
        var forward = Policy(); var reversed = Policy(null, Eight.Reverse().ToArray());
        var config = Config((Kabob, new FoodProductMetadataOverride(MealClass.Food, 4, 10, 20, 30)));
        foreach (var name in Eight)
            for (int seed = 0; seed < 16; seed++)
            {
                string resident = "pc:" + seed;
                Assert.Equal(forward.TasteBand(name, traits, seed, resident), reversed.TasteBand(name, traits, seed, resident));
                Assert.Equal(JsonConvert.SerializeObject(forward.Capture(name, traits, seed, resident)),
                    JsonConvert.SerializeObject(reversed.Capture(name, traits, seed, resident)));
                // A config override replaces the configured name's taste only.
                if (name != Kabob) Assert.Equal(forward.TasteBand(name, traits, seed, resident), Policy(config).TasteBand(name, traits, seed, resident));
            }
        Assert.Equal(4, Policy(config).TasteBand(Kabob, traits, 1, "pc:1"));
        // A food-by-default name with no config or API entry carries the default fits.
        var defaults = forward.Capture(Dumplings, traits, 1, "pc:1");
        Assert.Equal(2, defaults.Revision); Assert.Equal(FoodMealClass.Food, defaults.MealClass);
    }

    // ---- A running city with the production composition: one policy source for the composite and the vehicle ----

    /// <summary>A shop market with no candidate, so the resident's only option is the vehicle.</summary>
    private sealed class ClosedShops : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodFailureMarket, IFoodSaleEvidence, IRivalFoodMarket
    {
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double minute) => new(false, "closed shop double");
        public FoodBusiness? ReadBusiness(string id, double minute) => null;
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximum) => Array.Empty<FoodBusiness>();
        public bool CanServe(string id) => false;
        public bool IsCandidate(FoodScope scope) => false;
        public FoodBusiness? CaptureBusiness(string id, double minute) => null;
        public IDisposable BeginCapture(double minute) => new Handle();
        private sealed class Handle : IDisposable { public void Dispose() { } }
        public FoodBusiness? ReadFailureBusiness(string id, string place, double minute) => null;
        public bool IsConfirmedPaid(ResidentMeal meal) => false;
        public long RivalRevision => 0;
        public void CloseRivals() { }
        public bool NeedsRivalRefresh(double minute) => false;
        public void ContinueRivalRefresh(double minute) { }
        public IEnumerable<FoodScope> RivalScopes => Array.Empty<FoodScope>();
        public bool IsRivalCandidate(FoodScope scope) => false;
        public bool RivalAtPlace(string place) => false;
        public FoodAlternativeObservation? ReadRival(string id) => null;
    }

    private sealed class CityRun
    {
        public CitySimulator City;
        public StreetFoodVendorMarket Vendor = null!;
        public readonly VehicleMarketTests.Api Api = new();
        public Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
        public LoadedNameImage? Image;
        public FoodProductMetadataResolver Config = FoodProductMetadataResolver.Empty;
        private FoodProductPolicySource source = null!;
        private readonly WeightedFoodChoiceTests.Planner planner;
        private long revision = 1;
        public long Clock;
        public CityRun(int seed, params string[] items)
        {
            var (ready, _, readyPlanner) = WeightedFoodChoiceTests.Ready(seed, 1);
            planner = readyPlanner; City = new CitySimulator(ready.State, planner);
            Image = new LoadedNameImage(revision, Eight, FoodEligibilityRefusal.None);
            Clock = (long)City.State.Minute;
            StreetFoodHooks.Now = Clock;
            StreetFoodHooks.Add("v00", x: 30, items: items);
            Attach(true);
        }
        public void Load(params string[] names) => Image = new LoadedNameImage(++revision, names, FoodEligibilityRefusal.None);
        private void Attach(bool present)
        {
            // PersistentCityMod.ComposeFoodMarkets: the composite and the vehicle market read the same policy source.
            source = new FoodProductPolicySource(() => Image, () => Config, _ => FoodProductMetadataSnapshot.Empty);
            Vendor = new StreetFoodVendorMarket(City, present ? Api : null, "Street Food vendor API missing.", new VendorNavMeshSampler(),
                Receipts, () => Clock, () => true, () => source.Current);
            City.AttachFoodMarket(new FoodMarkets(City.State.Food!, new ClosedShops(), Vendor, id => City.FindPlace(id).Position, source));
        }
        public VendorRow Row => City.State.Vendors.Find("v00")!;
        public ResidentMeal Meal => City.State.Food!.Residents.Single();
        /// <summary>One native minute: Street Food's crew opens the vehicle at 01:00 for 22 hours each day.</summary>
        /// <remarks>Advances one event at a time and stops inside the minute as soon as <paramref name="stop"/> holds (a held
        /// decision context lives only between two events). The next step finishes that same minute, so no event runs
        /// behind the native clock.</remarks>
        public void Step(Func<bool>? stop = null)
        {
            if (!partial)
            {
                Clock++;
                StreetFoodHooks.Now = Clock; StreetFoodHooks.Expire();
                var v = StreetFoodHooks.Find("v00");
                if (Clock % 1440 == 60 && !v.Open) StreetFoodHooks.Open("v00", 22 * 60);
                if (Clock % 1440 == 23 * 60 && v.Open) StreetFoodHooks.Close("v00");
            }
            partial = false;
            while (Vendor.Prepare(Clock)) { }
            while (!City.AdvanceTo(Clock + 1023.0 / 1024, 1).CaughtUp)
                if (stop?.Invoke() == true) { partial = true; return; }
        }
        private bool partial;
        public void RunUntil(Func<bool> done, int limit = 3 * 1440)
        {
            for (int i = 0; i < limit && !done(); i++) Step(done);
            Assert.True(done());
        }
        public void RunFor(int minutes) { for (int i = 0; i < minutes; i++) Step(); }
        public IEnumerable<FoodDecisionOffer> HeldOffers => Meal.Decision?.Shops.SelectMany(s => s.Offers) ?? Enumerable.Empty<FoodDecisionOffer>();
        public string Tuples => JsonConvert.SerializeObject(HeldOffers.Select(o => new { o.ProductId, o.Metadata }));
        public string Reload(bool present)
        {
            Vendor.CompleteForSave();
            string payload = SaveCodec.Write(City.State);
            var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveCodec.Write(read.State!));
            // A native save carries the receipt dictionary next to the population bytes.
            Receipts = new Dictionary<string, string>(Receipts, StringComparer.Ordinal);
            City = new CitySimulator(read.State!, planner);
            Attach(present);
            return payload;
        }
    }

    /// <summary>A captured, not yet finished decision context (E5b4's held tuples), before the resident departs.</summary>
    private static bool Held(CityRun run) => run.Meal.Decision?.Shops.Count > 0;
    private static bool Resolved(CityRun run) => run.Meal.Status == MealStatus.Paid || run.Meal.Status == MealStatus.Missed || run.Meal.Status == MealStatus.Declined;

    [Theory]
    [InlineData(3)] [InlineData(11)]
    public void AHeldVehicleDecisionWithDumplingsOrChoppedCheeseReplaysAcrossReload(int seed)
    {
        // Control: no reload.
        var control = new CityRun(seed, Dumplings, ChoppedCheese);
        control.RunUntil(() => Held(control));
        string held = control.Tuples;
        Assert.Equal(new[] { Dumplings, ChoppedCheese }, control.HeldOffers.Select(o => o.ProductId));
        Assert.All(control.HeldOffers, o => Assert.Equal(2, o.Metadata!.Revision));
        Assert.All(control.Meal.Decision!.Shops, shop => Assert.Equal(control.Row.BusinessId, shop.BusinessId));
        control.RunUntil(() => Resolved(control));
        Assert.Equal(MealStatus.Paid, control.Meal.Status);
        control.RunFor(60);
        string controlEnd = SaveCodec.Write(control.City.State);
        var controlReceipts = JsonConvert.SerializeObject(control.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(1, control.Api.SellCalls);

        // Same city, saved and reloaded while the decision is held; config then changes taste and fits (still Food).
        StreetFoodHooks.Reset();
        var changed = new CityRun(seed, Dumplings, ChoppedCheese);
        changed.RunUntil(() => Held(changed));
        Assert.Equal(held, changed.Tuples);
        changed.Config = Config((Dumplings, new FoodProductMetadataOverride(MealClass.Food, 0, 5000, 5000, 5000)),
            (ChoppedCheese, new FoodProductMetadataOverride(MealClass.Food, 0, 5000, 5000, 5000)));
        changed.Reload(true);
        Assert.Equal(held, changed.Tuples);
        changed.RunUntil(() => Resolved(changed));
        Assert.Equal(MealStatus.Paid, changed.Meal.Status);
        changed.RunFor(60);
        Assert.Equal(controlEnd, SaveCodec.Write(changed.City.State));
        Assert.Equal(controlReceipts, JsonConvert.SerializeObject(changed.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal)));
        Assert.Equal(1, changed.Api.SellCalls);
        Assert.Equal(0, changed.City.HistoricalClockMisses);
    }

    [Fact]
    public void AHeldVehicleDecisionKeepsItsTuplesWhenADrinkOverrideHandsTheVehicleBackAndNothingIsSold()
    {
        var run = new CityRun(3, Dumplings, ChoppedCheese);
        run.RunUntil(() => Held(run));
        string held = run.Tuples;
        run.Config = Config((ChoppedCheese, new FoodProductMetadataOverride(MealClass.Drink, 0, 0, 0, 0)));
        run.Reload(true);
        Assert.Equal(held, run.Tuples);
        run.RunUntil(() => Resolved(run));
        Assert.Equal(MealStatus.Missed, run.Meal.Status);
        Assert.Equal(0, run.Api.SellCalls); Assert.DoesNotContain(run.Receipts, p => FoodSaleReceipts.IsReceiptKey(p.Key));
        run.RunFor(2 * VendorCoordinator.WaitingRecheckMinutes);
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.Equal("menu item not executable: " + ChoppedCheese, run.Row.Reason);
    }

    [Fact]
    public void MissingStreetFoodKeepsHistoryAndReceiptsAndItsReturnRevalidates()
    {
        var run = new CityRun(3, Dumplings, ChoppedCheese);
        run.RunUntil(() => Resolved(run));
        Assert.Equal(MealStatus.Paid, run.Meal.Status);
        var paid = run.Meal; string business = run.Row.BusinessId;
        Assert.Contains(paid.ProductId, new[] { Dumplings, ChoppedCheese });
        Assert.True(paid.Decision == null || paid.Decision.Shops.All(s => s.BusinessId == business));
        string receipts = JsonConvert.SerializeObject(run.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Contains(run.City.State.Food!.History, h => h.BusinessId == business);
        string history = JsonConvert.SerializeObject(run.City.State.Food!.History);
        var scope = JsonConvert.SerializeObject(run.City.State.Food.Shops.Single(s => s.Channel == FoodScopeChannel.Vehicle));

        // Street Food absent: the city loads, keeps its vehicle facts and serves no vehicle.
        string saved = run.Reload(false);
        Assert.Equal(receipts, JsonConvert.SerializeObject(run.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal)));
        Assert.Equal(history, JsonConvert.SerializeObject(run.City.State.Food!.History));
        Assert.Equal(scope, JsonConvert.SerializeObject(run.City.State.Food.Shops.Single(s => s.Channel == FoodScopeChannel.Vehicle)));
        Assert.Equal(saved, SaveCodec.Write(run.City.State));
        run.RunFor(30);
        Assert.False(run.Vendor.CanServe(business)); Assert.Equal("Street Food vendor API missing.", run.Vendor.Coordinator.Reason);
        Assert.Contains(run.City.State.Food.History, h => h.BusinessId == business);
        Assert.Equal(receipts, JsonConvert.SerializeObject(run.Receipts.OrderBy(p => p.Key, StringComparer.Ordinal)));
        int sells = run.Api.SellCalls;

        // Street Food returns, but the dumpling mod did not: the whole vehicle is refused and stays with Street Food.
        run.Load(Eight.Where(n => n != Dumplings).ToArray());
        run.Reload(true);
        run.RunFor(2 * 1440);
        Assert.Equal("menu item not executable: " + Dumplings, run.Row.Reason);
        Assert.False(run.Vendor.CanServe(run.Row.BusinessId));
        Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.Equal(sells, run.Api.SellCalls);

        // With dumplings loaded again the vehicle re-qualifies at a later opening and sells again.
        run.Load(Eight);
        run.RunUntil(() => run.Vendor.CanServe(run.Row.BusinessId), 2 * 1440);
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        run.RunUntil(() => run.Api.SellCalls > sells, 3 * 1440);
        Assert.Equal(0, run.City.HistoricalClockMisses);
    }
}
