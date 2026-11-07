using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class VehicleMarketTests
{
    internal const long Morning = 2 * 1440 + 8 * 60;
    public VehicleMarketTests() => StreetFoodHooks.Reset();

    internal sealed class Api : IVendorApi
    {
        private readonly IVendorApi inner = StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi));
        public string Fault = "";
        public int SellCalls, CopyCalls, RevisionReads;
        public int LastDay, LastMinute;
        public long LastCents;
        public string LastItem = "";
        public Action? OnSell;
        public long Revision { get { RevisionReads++; return inner.Revision; } }
        public VendorApiStatus BeginRead(out int count) => inner.BeginRead(out count);
        public void CopyVehicle(int i, VendorVehicle into) { CopyCalls++; inner.CopyVehicle(i, into); }
        public VendorApiStatus RequestOwner(string id, bool want, out string reason) => inner.RequestOwner(id, want, out reason);
        public VendorApiStatus Renew(int d, int m, out int ud, out int um) => inner.Renew(d, m, out ud, out um);
        public VendorApiStatus ReleaseAll() => inner.ReleaseAll();
        public VendorApiStatus Sell(string id, long opening, string item, long cents, int day, int minute,
            out VendorSaleResult? sale, out bool uncertain, out string reason)
        {
            SellCalls++; LastDay = day; LastMinute = minute; LastCents = cents; LastItem = item; OnSell?.Invoke();
            sale = null; uncertain = false; reason = "pre-effect refusal";
            if (Fault.StartsWith("refuse")) return (VendorApiStatus)int.Parse(Fault.Substring(6));
            if (Fault == "throw-before") throw new InvalidOperationException("uncertain exception before acknowledgement");
            var result = inner.Sell(id, opening, item, cents, day, minute, out sale, out uncertain, out reason);
            if (Fault == "throw-after") throw new InvalidOperationException("lost acknowledgement");
            if (Fault == "uncertain") uncertain = true;
            if (Fault == "null") sale = null;
            if (sale != null)
            {
                sale = new VendorSaleResult(sale.BookedDay + (Fault == "day" ? 1 : 0), sale.Cents + (Fault == "cents" ? 1 : 0),
                    sale.UnitsBefore + (Fault == "before" ? 1 : 0), sale.UnitsAfter + (Fault == "after" ? 1 : 0), Fault == "sequence" ? 0 : sale.Sequence);
                if (Fault == "other-product") StreetFoodHooks.Find(id).Units[1]--;
                if (Fault == "product") StreetFoodHooks.Find(id).Items[0] = FoodProducts.Croissant;
                if (Fault == "post-read") StreetFoodHooks.ThrowOnRead = true;
            }
            return result;
        }
    }
    internal sealed class Fixture
    {
        public CitySimulator City;
        public readonly Api Api = new Api();
        public StreetFoodVendorMarket Market;
        public Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
        public long Now = Morning;
        public bool SameSave = true;
        public readonly WeightedFoodChoiceTests.Planner Planner;
        public Fixture(int count = 1, int shops = 2, Action<StreetFoodHooks.Vehicle>? configure = null)
        {
            (City, _, Planner) = WeightedFoodChoiceTests.Ready(5, shops);
            for (int i = 0; i < count; i++) StreetFoodHooks.Add("v" + i.ToString("D2"), x: 100 + i * 10,
                items: new[] { FoodProducts.HotDog, FoodProducts.Burger });
            if (configure != null) foreach (var v in StreetFoodHooks.Vehicles) configure(v);
            Market = Create(true); Run(Morning);
            for (long m = Morning + 15; m <= Morning + 60; m += 15) Run(m);
            foreach (var v in StreetFoodHooks.Vehicles) StreetFoodHooks.Open(v.Id, 300);
            Run(Now + 1);
        }
        private StreetFoodVendorMarket Create(bool present) => new(City, present ? Api : null, "Street Food vendor API missing.",
            new VendorNavMeshSampler(), Receipts, () => Now, () => SameSave);
        public void Run(long minute)
        {
            Now = StreetFoodHooks.Now = minute; StreetFoodHooks.Expire();
            while (Market.Prepare(minute)) { }
        }
        public ResidentMeal Meal(string id = "v00", int resident = 0)
        {
            var row = City.State.Vendors.Find(id)!;
            return new ResidentMeal { ResidentId = "customer" + resident, AnchorId = "customer" + resident, Day = (int)(Now / 1440),
                Kind = MealKind.Lunch, PlaceId = row.PlaceId, BusinessId = row.BusinessId, ProductId = FoodProducts.HotDog,
                Price = Money.FromCents(400), Allowance = Money.FromCents(1000), ServiceMinutes = 2, Status = MealStatus.Serving };
        }
        public void Reload(bool present = true)
        {
            // A native save carries the receipt dictionary alongside the population bytes.
            string payload = SaveCodec.Write(City.State);
            var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveCodec.Write(read.State!));
            Receipts = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(Receipts))!;
            City = new CitySimulator(read.State!, Planner);
            Market = Create(present);
            Run(Now);
        }
    }

    [Fact]
    public void PositiveSaleUsesExactDayCentsOneUnitAndPaidReceiptReplaysAfterReload()
    {
        var f = new Fixture(); var meal = f.Meal();
        StreetFoodHooks.Find("v00").Prices[0] = 12345; meal.Price = Money.FromCents(12345); meal.Allowance = meal.Price;
        f.Run(f.Now); // checkout must read the live price even at the same revision
        int before = StreetFoodHooks.Find("v00").Units[0];
        Assert.True(f.Market.Buy(meal, f.Now).Paid);
        Assert.Equal(before - 1, StreetFoodHooks.Find("v00").Units[0]);
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(f.Receipts, meal));
        Assert.Equal(1, f.Api.SellCalls); Assert.Equal(1, StreetFoodHooks.Sequence);
        Assert.Equal((int)(f.Now / 1440), f.Api.LastDay); Assert.Equal((int)(f.Now % 1440), f.Api.LastMinute);
        Assert.Equal(12345, f.Api.LastCents); Assert.Equal(FoodProducts.HotDog, f.Api.LastItem);
        f.Reload();
        Assert.True(f.Market.IsConfirmedPaid(meal));
        Assert.True(f.Market.Buy(meal, f.Now).Paid); Assert.Equal(1, f.Api.SellCalls);
    }

    [Fact]
    public void RuntimeSamplerRefusesOpeningOffMeshWithReasonAndNoVehicleOffer()
    {
        var f = new Fixture();
        StreetFoodHooks.Close("v00"); f.Run(f.Now + 1);
        StreetFoodHooks.Open("v00", x: 9000); f.Run(f.Now + 1);
        var row = f.City.State.Vendors.Find("v00")!;
        Assert.Contains("no walkable point within 2 m", row.Reason);
        Assert.Empty(f.Market.Read(f.Now)); Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
    }

    [Fact]
    public void CancellationConflictKeepsEvidenceAndCannotRetry()
    {
        var f = new Fixture(); var m = f.Meal(); f.Api.Fault = "refuse3";
        f.Api.OnSell = () => f.Receipts[FoodSaleReceipts.Prefix + m.IntentId] = "pending|different";
        Assert.Contains("uncertain", Assert.Throws<InvalidOperationException>(() => f.Market.Buy(m, f.Now)).Message);
        Assert.Equal("pending|different", f.Receipts[FoodSaleReceipts.Prefix + m.IntentId]);
        Assert.Throws<InvalidOperationException>(() => f.Market.Buy(m, f.Now)); Assert.Equal(1, f.Api.SellCalls);
    }

    [Fact]
    public void CheckoutReadsOnlyItsSavedSlotAndVerifiesIdentityWhenSnapshotOrderChanges()
    {
        var f = new Fixture(64); var meal = f.Meal("v63"); int copies = f.Api.CopyCalls;
        Assert.True(f.Market.Buy(meal, f.Now).Paid); Assert.Equal(2, f.Api.CopyCalls - copies);
        f.Receipts.Clear();
        StreetFoodHooks.Vehicles.Reverse(); // revision has not been polled, so the old hint is deliberately wrong
        Assert.True(f.Market.Buy(meal, f.Now).Paid);
        Assert.Equal(8, StreetFoodHooks.Find("v63").Units[0]);
        Assert.Equal(10, StreetFoodHooks.Find("v00").Units[0]);
    }

    [Fact]
    public void RetimingRebindPreservesLeaseIdentityAndPublishesIntoTheReplacementSimulator()
    {
        var f = new Fixture(); var row = f.City.State.Vendors.Find("v00")!; long lease = f.Market.Coordinator.Lease.UntilMinute;
        f.City = f.City.RebuildRoutes(f.Planner); f.Market.Rebind(f.City);
        Assert.Equal(lease, f.Market.Coordinator.Lease.UntilMinute);
        Assert.True(f.Market.CanServe(row.BusinessId));
        Assert.True(f.Market.Buy(f.Meal(), f.Now).Paid);
        StreetFoodHooks.Close("v00"); f.Run(f.Now + 1); StreetFoodHooks.Open("v00", x: 250); f.Run(f.Now + 1);
        Assert.Equal(250, f.City.FindPlace(row.PlaceId).Position.X);
    }

    [Fact]
    public void SnapshotReadsAndCapturesDoNotReadApiAndCheckoutDoesNotRepollRevision()
    {
        var f = new Fixture(); var m = f.Meal(); int reads = StreetFoodHooks.ReadCalls, revisions = f.Api.RevisionReads;
        for (int i = 0; i < 100; i++)
        {
            Assert.Single(f.Market.Read(f.Now));
            using (f.Market.BeginCapture(f.Now)) Assert.NotNull(f.Market.CaptureBusiness(m.BusinessId, f.Now));
        }
        Assert.Equal(reads, StreetFoodHooks.ReadCalls); Assert.Equal(revisions, f.Api.RevisionReads);
        Assert.True(f.Market.Buy(m, f.Now).Paid); f.Run(f.Now);
        Assert.Equal(reads + 2, StreetFoodHooks.ReadCalls); Assert.Equal(revisions, f.Api.RevisionReads);
        Assert.Equal(9, f.Market.ReadBusiness(m.BusinessId, f.Now)!.Offers[0].Units);
    }

    [Fact]
    public void MenuChangeWithinAnOpeningPublishesTheSameBusinessWithCurrentOriginalProducts()
    {
        var f = new Fixture(); var row = f.City.State.Vendors.Find("v00")!; string business = row.BusinessId;
        var v = StreetFoodHooks.Find("v00");
        v.Items = new[] { FoodProducts.HotDog, FoodProducts.Burger, FoodProducts.Croissant };
        v.Prices = new long[] { 400, 401, 402 }; v.Units = new[] { 10, 10, 10 }; StreetFoodHooks.Revision++;
        f.Run(f.Now + 1);
        Assert.Equal(business, row.BusinessId);
        Assert.True(f.City.State.Food!.Shops.Single(s => s.PlaceId == row.PlaceId).ContainsProduct(FoodProducts.Croissant));
        Assert.Equal(3, f.Market.ReadBusiness(business, f.Now)!.Offers.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EntireVehicleStaysNativeWhenAnyPriceExceedsExactMoneyRange(bool changedWhileOwned)
    {
        var f = new Fixture(configure: changedWhileOwned ? null : v => v.Prices[1] = long.MaxValue);
        var v = StreetFoodHooks.Find("v00");
        if (changedWhileOwned) { v.Prices[1] = long.MaxValue; StreetFoodHooks.Revision++; f.Run(f.Now + 1); }
        Assert.Empty(f.Market.Read(f.Now)); Assert.Equal("", v.Owner);
        Assert.False(f.City.State.Vendors.Find("v00")!.Authority.Desired);
        Assert.Contains("price outside exact resident money range", f.City.State.Vendors.Find("v00")!.Reason);
        Assert.Equal(0, f.Api.SellCalls); Assert.Empty(f.Receipts);
        Assert.DoesNotContain("v00", StreetFoodHooks.Wishes.Keys);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void EveryConfirmedPreEffectApiRefusalCancelsReceipt(int status)
    {
        var f = new Fixture(); var m = f.Meal(); f.Api.Fault = "refuse" + status;
        Assert.False(f.Market.Buy(m, f.Now).Paid);
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(f.Receipts, m));
        Assert.Equal(10, StreetFoodHooks.Find("v00").Units[0]);
        f.Api.Fault = ""; Assert.True(f.Market.Buy(m, f.Now).Paid); Assert.Equal(2, f.Api.SellCalls);
    }

    [Theory]
    [InlineData("uncertain")] [InlineData("throw-before")] [InlineData("throw-after")]
    [InlineData("day")] [InlineData("cents")] [InlineData("before")] [InlineData("after")]
    [InlineData("sequence")] [InlineData("other-product")] [InlineData("product")]
    [InlineData("post-read")] [InlineData("null")]
    public void UncertainExceptionOrEvidenceMismatchKeepsPendingAndNeverRetriesEvenAfterReload(string fault)
    {
        var f = new Fixture(); var m = f.Meal(); f.Api.Fault = fault;
        Assert.Contains("outcome is uncertain; do not retry", Assert.Throws<InvalidOperationException>(() => f.Market.Buy(m, f.Now)).Message);
        Assert.Equal(FoodReceiptStatus.Pending, FoodSaleReceipts.ReadStatus(f.Receipts, m));
        Assert.Throws<InvalidOperationException>(() => f.Market.Buy(m, f.Now)); Assert.Equal(1, f.Api.SellCalls);
        StreetFoodHooks.ThrowOnRead = false;
        f.Reload();
        Assert.Throws<InvalidOperationException>(() => f.Market.Buy(m, f.Now)); Assert.Equal(1, f.Api.SellCalls);
    }

    [Theory]
    [InlineData("price")] [InlineData("stock")] [InlineData("closed")] [InlineData("opening")]
    [InlineData("owner")] [InlineData("menu")] [InlineData("stand")]
    [InlineData("rate")] [InlineData("duration")] [InlineData("allowance")]
    [InlineData("fractional")] [InlineData("past")] [InlineData("future")]
    [InlineData("save")] [InlineData("place")] [InlineData("product")] [InlineData("desired")]
    [InlineData("player-window")] [InlineData("event")] [InlineData("lease")]
    [InlineData("bad-price")] [InlineData("bad-units")] [InlineData("bad-name")] [InlineData("overflow")]
    [InlineData("api-off")] [InlineData("api-notyet")] [InlineData("api-refused")] [InlineData("api-failed")]
    public void PreReceiptGuardsNeverCallSellOrWriteReceipt(string change)
    {
        var f = new Fixture(); var m = f.Meal(); var v = StreetFoodHooks.Find("v00"); double at = f.Now;
        switch (change)
        {
            case "price": v.Prices[0]++; break;
            case "stock": v.Units[0] = 0; break;
            case "closed": v.Open = false; break;
            case "opening": v.OpeningId++; break;
            case "owner": v.Owner = ""; break;
            case "menu": v.Items[1] = "ba:itemname_kabob"; break;
            case "stand": v.X += 100; break;
            case "rate": v.MealsPerHour = 0; break;
            case "duration": m.ServiceMinutes = 0.1; break;
            case "allowance": m.Allowance = Money.Zero; break;
            case "fractional": m.Price = Money.FromUnits(m.Price.Units + 1); break;
            case "past": at--; break;
            case "future": at++; break;
            case "save": f.SameSave = false; break;
            case "place": m.PlaceId = "s0"; break;
            case "product": m.ProductId = FoodProducts.Croissant; break;
            case "desired": f.City.State.Vendors.Find("v00")!.Authority.Desired = false; break;
            case "player-window": v.Crew = false; break;
            case "event": v.Event = true; break;
            case "lease": f.Now += 61; break;
            case "bad-price": v.Prices[0] = 0; break;
            case "bad-units": v.Units[0] = -1; break;
            case "bad-name": v.Items[1] = "noncanonical"; break;
            case "overflow": v.Items = new string[9]; break;
            case "api-off": StreetFoodHooks.ReadStatus = 1; break;
            case "api-notyet": StreetFoodHooks.ReadStatus = 2; break;
            case "api-refused": StreetFoodHooks.ReadStatus = 3; break;
            case "api-failed": StreetFoodHooks.ReadStatus = 4; break;
        }
        var result = f.Market.Buy(m, at);
        Assert.False(result.Paid); Assert.Equal(0, f.Api.SellCalls); Assert.Empty(f.Receipts);
        if (change == "closed" || change == "opening" || change == "stand") Assert.Equal(ShopExperience.Closed, result.Failure);
        if (change == "stock") Assert.Equal(ShopExperience.Stockout, result.Failure);
    }

    [Fact]
    public void WholeVehicleRefusedForOneNonKnownItemAndSamplerRejectsOffMeshStand()
    {
        var f = new Fixture(); var v = StreetFoodHooks.Find("v00"); v.Items[1] = "ba:itemname_kabob"; StreetFoodHooks.Revision++;
        f.Run(f.Now + 1);
        Assert.Empty(f.Market.Read(f.Now));
        Assert.Contains("menu item not executable", f.City.State.Vendors.Find("v00")!.Reason);
        Assert.Equal("", v.Owner);
        var sampler = new VendorNavMeshSampler();
        Assert.True(sampler.TrySample(new Position(10, 0.5, 2), 2, out var on)); Assert.Equal(0, on.Y);
        Assert.False(sampler.TrySample(new Position(9000, 0, 2), 2, out _));
        Assert.False(sampler.TrySample(new Position(10, 3, 2), 2, out _));
        Assert.False(new VendorNavMeshSampler((_, _) => new Position(100, 0, 0)).TrySample(new Position(0, 0, 0), 2, out _));
    }

    [Fact]
    public void DesiredClearedStopsEveryCaptureAndWithdrawalHandsBackAfterDrain()
    {
        var f = new Fixture(); var m = f.Meal(); var row = f.City.State.Vendors.Find("v00")!;
        row.Authority.Desired = false;
        Assert.Null(f.Market.CaptureBusiness(row.BusinessId, f.Now)); Assert.False(f.Market.IsCandidate(f.City.State.Food!.Shops.Single(s => s.PlaceId == row.PlaceId)));
        row.Authority.Desired = true;
        f.City.State.Food.Residents.Add(m);
        Assert.True(f.Market.Withdraw(row.VehicleId));
        Assert.Equal(VendorNames.Owner, StreetFoodHooks.Find("v00").Owner);
        f.Run(f.Now + 1); Assert.False(row.Authority.Desired); Assert.Null(f.Market.CaptureBusiness(row.BusinessId, f.Now));
        m.Status = MealStatus.Missed;
        f.Run(f.Now + 1); Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        Assert.False(f.Market.Buy(m, f.Now).Paid);
        StreetFoodHooks.Close("v00");
        f.Run(f.Now + 1);
        // At the next opening the normal later-hour request rule applies again.
        StreetFoodHooks.Open("v00"); f.Run(f.Now + 1);
        f.Run(f.Now + 61); StreetFoodHooks.Close("v00"); StreetFoodHooks.Open("v00"); f.Run(f.Now + 1);
        Assert.True(row.Authority.Desired);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancelledIntentCanBuyAtAnotherShopIncludingAcrossSaveReload(bool reload)
    {
        var f = new Fixture(); var m = f.Meal(); f.Api.Fault = "refuse3";
        Assert.False(f.Market.Buy(m, f.Now).Paid);
        if (reload) f.Reload();
        m.BusinessId = "b0"; m.PlaceId = "s0"; m.ProductId = FoodProducts.Croissant; m.Price = Money.FromCents(100);
        var shop = new ShopMarket(f.City.State.Food!, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food!, shop, f.Market, id => f.City.FindPlace(id).Position);
        Assert.True(composite.Buy(m, f.Now).Paid);
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(f.Receipts, m));
        Assert.Equal(1, f.Api.SellCalls); Assert.Equal(1, shop.Sales);
    }

    [Fact]
    public void CancelRequiresExactCurrentPendingIdentityAndRejectsPaidLegacyConflictOrMissing()
    {
        var f = new Fixture(); var m = f.Meal();
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Cancel(f.Receipts, m));
        FoodSaleReceipts.Begin(f.Receipts, m);
        foreach (string field in new[] { "price", "business", "product" })
        {
            var other = f.Meal();
            if (field == "price") other.Price += Money.FromCents(1);
            if (field == "business") other.BusinessId = "another";
            if (field == "product") other.ProductId = FoodProducts.Burger;
            Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Cancel(f.Receipts, other));
        }
        f.Receipts[FoodSaleReceipts.LegacyPrefix + m.IntentId] = f.Receipts[FoodSaleReceipts.Prefix + m.IntentId];
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Cancel(f.Receipts, m));
        f.Receipts.Remove(FoodSaleReceipts.LegacyPrefix + m.IntentId);
        FoodSaleReceipts.Confirm(f.Receipts, m);
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Cancel(f.Receipts, m));
        Assert.True(FoodSaleReceipts.AlreadyPaid(f.Receipts, m));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MixedShopVehicleCityConstructsSavesReloadsAndValidatesBothChannels(bool present)
    {
        var f = new Fixture(); var scopes = f.City.State.Food!.Shops;
        Assert.Equal(2, scopes.Count(s => s.Channel == FoodScopeChannel.Shop));
        Assert.Single(scopes, s => s.Channel == FoodScopeChannel.Vehicle);
        f.Reload(present);
        Assert.Equal(3, f.City.State.Food!.Shops.Count);
        var shop = new ShopMarket(f.City.State.Food, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food, shop, f.Market, id => f.City.FindPlace(id).Position);
        Assert.Equal(present ? 3 : 2, composite.Read(f.Now).Count);
        string payload = SaveCodec.Write(f.City.State);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(payload).Status);
        f.Reload(present);
        Assert.Equal(present, f.Market.CanServe(f.Meal().BusinessId));
        Assert.Single(f.City.State.Vendors.Rows);
        var manifest = f.City.State.Food!.Shops.Where(s => s.Channel == FoodScopeChannel.Shop)
            .Select(s => new FoodAuthority { PlaceId = s.PlaceId, Signature = s.BusinessId, FromHour = 0 }).ToList();
        void Validate(IEnumerable<FoodAuthority> rows) => FoodMarkets.ValidateShops(f.City.State.Food!, rows,
            (r, s) => r.Signature == s.BusinessId, reason => throw new InvalidOperationException(reason));
        Validate(manifest);
        Assert.Throws<InvalidOperationException>(() => Validate(manifest.Skip(1)));
        manifest.Add(new FoodAuthority { PlaceId = "missing", Signature = "missing", FromHour = 0 });
        Assert.Throws<InvalidOperationException>(() => Validate(manifest));
    }

    [Theory]
    [InlineData("scope")] [InlineData("duplicate")] [InlineData("missing-scope")]
    [InlineData("meal")] [InlineData("decision")] [InlineData("receipt")]
    public void RuntimeConstructorRechecksVehiclePopulation(string fault)
    {
        var f = new Fixture(); var state = f.City.State.Food!; var scope = state.Shops.Single(s => s.Channel == FoodScopeChannel.Vehicle);
        switch (fault)
        {
            case "scope": scope.BusinessId = "wrong"; break;
            case "duplicate": state.Shops.Add(scope); break;
            case "missing-scope": state.Shops.Remove(scope); break;
            case "meal": var m = f.Meal(); m.PlaceId = "vendor:missing"; state.Residents.Add(m); break;
            case "decision": state.Residents[0].Decision = new FoodDecisionState { Shops = new() { new FoodDecisionShop { PlaceId = "vendor:missing", BusinessId = f.Meal().BusinessId } } }; break;
            case "receipt": var receiptMeal = f.Meal(); receiptMeal.BusinessId = VendorNames.BusinessId("missing", 1); FoodSaleReceipts.Begin(f.Receipts, receiptMeal); break;
        }
        Assert.Throws<InvalidOperationException>(() => new StreetFoodVendorMarket(f.City, f.Api, "", new VendorNavMeshSampler(), f.Receipts, () => f.Now, () => true));
    }

    internal sealed class ShopMarket : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodFailureMarket, IFoodSaleEvidence, IRivalFoodMarket, IFoodAwarenessRevision, IDisposable
    {
        private readonly FoodCommerceState state;
        private readonly IDictionary<string, string> receipts;
        public ShopMarket(FoodCommerceState state, IDictionary<string, string> receipts) { this.state = state; this.receipts = receipts; }
        public double Start;
        public double EarliestMinute => Start;
        public int Captures, Disposals, Sales, RivalCalls, MarketDisposals;
        public bool ThrowDispose;
        public long AwarenessRevision => 7;
        private IEnumerable<FoodScope> Shops => state.Shops.Where(s => s.Channel == FoodScopeChannel.Shop);
        public IReadOnlyList<FoodBusiness> Read(double minute) => Shops.Select(s => ReadBusiness(s.BusinessId, minute)!).ToArray();
        public FoodBusiness? ReadBusiness(string id, double minute) => Shops.Where(s => s.BusinessId == id).Select(s => new FoodBusiness {
            Id = s.BusinessId, PlaceId = s.PlaceId, MealsPerHour = 60, Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100 } } }).SingleOrDefault();
        public IReadOnlyList<FoodBusiness> ReadLocal(Place p, double at, double tolerance, int max) => Read(at).Take(max).ToArray();
        public bool CanServe(string id) => Shops.Any(s => s.BusinessId == id);
        public bool IsConfirmedPaid(ResidentMeal m) => FoodSaleReceipts.ReadStatus(receipts, m) == FoodReceiptStatus.Paid;
        public FoodSale Buy(ResidentMeal m, double at) { FoodSaleReceipts.Begin(receipts, m); Sales++; FoodSaleReceipts.Confirm(receipts, m); return new(true, "shop double confirmed"); }
        public bool IsCandidate(FoodScope s) => s.Channel == FoodScopeChannel.Shop;
        public FoodBusiness? CaptureBusiness(string id, double at) { Captures++; return ReadBusiness(id, at); }
        public FoodBusiness? ReadFailureBusiness(string id, string p, double at) => ReadBusiness(id, at);
        public IDisposable BeginCapture(double at) { return new Handle(this); }
        private sealed class Handle : IDisposable
        {
            private readonly ShopMarket owner;
            public Handle(ShopMarket owner) { this.owner = owner; }
            public void Dispose() { owner.Disposals++; if (owner.ThrowDispose) throw new InvalidOperationException("dispose"); }
        }
        public void Dispose() { MarketDisposals++; if (ThrowDispose) throw new InvalidOperationException("market dispose"); }
        public long RivalRevision => 33;
        public IEnumerable<FoodScope> RivalScopes => Shops;
        public void CloseRivals() { RivalCalls++; }
        public bool NeedsRivalRefresh(double at) { RivalCalls++; return at > 10; }
        public void ContinueRivalRefresh(double at) { RivalCalls++; }
        public bool IsRivalCandidate(FoodScope s) { RivalCalls++; return s.BusinessId == "b0"; }
        public bool RivalAtPlace(string p) { RivalCalls++; return p == "s0"; }
        public readonly FoodAlternativeObservation Rival = new("rival", "s0", "Test", 1, 1, "food", "owner", FoodAlternativeKind.Rival,
            true, true, false, 80, 1, Array.Empty<FoodAdvertisedOffer>(), Array.Empty<FoodAdvertisedHours>());
        public FoodAlternativeObservation? ReadRival(string id) { RivalCalls++; return id == "rival" ? Rival : null; }
    }

    [Theory]
    [InlineData(8, false)] [InlineData(7, true)]
    public void CompositeRanksOnceBeforeSupplyAndKeepsNearestEight(int shops, bool vehicleIncluded)
    {
        var f = new Fixture(shops: shops); var shop = new ShopMarket(f.City.State.Food!, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food!, shop, f.Market, id => f.City.FindPlace(id).Position);
        var local = composite.ReadLocal(f.City.FindPlace("home"), f.Now, 1000, 8);
        Assert.Equal(8, local.Count);
        Assert.Equal(vehicleIncluded, local.Any(b => VendorNames.IsVendorPlace(b.PlaceId)));
        Assert.Equal(local.OrderBy(b => Position.Distance(f.City.FindPlace("home").Position, f.City.FindPlace(b.PlaceId).Position)).Select(b => b.Id), local.Select(b => b.Id));
        Assert.Equal(8 - (vehicleIncluded ? 1 : 0), shop.Captures);
        Assert.Equal(shops + 1, composite.Read(f.Now).Count);
        Assert.Equal(f.Now, composite.EarliestMinute); shop.Start = f.Now + 8; Assert.Equal(f.Now + 8, composite.EarliestMinute);
        Assert.Equal(33, composite.RivalRevision); Assert.Same(shop.Rival, composite.ReadRival("rival"));
        Assert.True(composite.RivalAtPlace("s0")); Assert.True(composite.IsRivalCandidate(f.City.State.Food!.Shops[0]));
        Assert.Equal(shop.RivalScopes, composite.RivalScopes); Assert.True(composite.NeedsRivalRefresh(11));
        composite.ContinueRivalRefresh(11); composite.CloseRivals(); Assert.Equal(6, shop.RivalCalls);
    }

    [Fact]
    public void CompositeDisposesBothMarketsOnceEvenWhenShopDisposalThrows()
    {
        var f = new Fixture(); var shop = new ShopMarket(f.City.State.Food!, f.Receipts) { ThrowDispose = true };
        var composite = new FoodMarkets(f.City.State.Food!, shop, f.Market, id => f.City.FindPlace(id).Position);
        Assert.Throws<InvalidOperationException>(() => composite.Dispose());
        Assert.Equal(1, shop.MarketDisposals);
        Assert.False(f.Market.CanServe(f.Meal().BusinessId)); Assert.Equal("", StreetFoodHooks.Find("v00").Owner);
        composite.Dispose(); Assert.Equal(1, shop.MarketDisposals);
    }

    [Fact]
    public void CompositeTieBreakAndCaptureDisposalIncludingExceptionAreDeterministic()
    {
        var f = new Fixture(); var shop = new ShopMarket(f.City.State.Food!, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food!, shop, f.Market, _ => new Position(0, 0, 0));
        Assert.Equal(new[] { "b0", "b1", f.Meal().BusinessId }, composite.ReadLocal(new Place(), f.Now, 100, 8).Select(b => b.Id));
        int start = shop.Disposals;
        var capture = composite.BeginCapture(f.Now); capture.Dispose(); capture.Dispose(); Assert.Equal(start + 1, shop.Disposals);
        using (f.Market.BeginCapture(f.Now)) { } // both markets have been released
        shop.ThrowDispose = true;
        capture = composite.BeginCapture(f.Now); Assert.Throws<InvalidOperationException>(() => capture.Dispose());
        using (f.Market.BeginCapture(f.Now)) { }
        shop.ThrowDispose = false;
        using (f.Market.BeginCapture(f.Now)) Assert.Throws<InvalidOperationException>(() => composite.BeginCapture(f.Now));
        Assert.Equal(start + 3, shop.Disposals); // failed second BeginCapture released the first
    }
}
