using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

namespace PersistentCity.Tests.Acceptance;

/// <summary>F9c (plan f9-city-acceptance.md, Decision 3): one integrated city in two sizes. Both sizes are the F5h reference
/// composition (F5g housing, census catalogue, two portals, seed 2026) with the player food shops, one rival under the F6b
/// service limits, one crew-run Street Food vehicle through the contract stand-in and C5 research on the saved ledger.
/// <c>city-m</c> is F5h's declared load (957 residents, three shops); <c>city-s</c> is the same composition at about 128
/// residents and one shop. Offline only: every native side is a double that lives outside the saved city.</summary>
internal sealed class CitySize
{
    public const int Seed = 2026;
    private CitySize(string id, F5hIntegratedAcceptanceTests.Shape shape, int shops) { Id = id; Shape = shape; Shops = shops; }
    public string Id { get; }
    public F5hIntegratedAcceptanceTests.Shape Shape { get; }
    public int Shops { get; }
    public int Homes => Shape.Homes;
    public PopulationFillCatalogue Catalogue => Shape.Catalogue;

    /// <summary>F5h's base businesses on 20 homes of seven beds: 128 residents.</summary>
    public static CitySize Small() => new("city-s", new F5hIntegratedAcceptanceTests.Shape(20, F5gPopulationFillTests.Base), 1);
    public static CitySize Medium() => new("city-m", F5hIntegratedAcceptanceTests.ReferenceShape(), 3);
}

/// <summary>The fixture's saved checkpoint: a fresh city at 06:00 on day 1, written by the current writer.</summary>
internal sealed class CityCheckpoint
{
    public CityCheckpoint(CitySize size, string payload) { Size = size; Payload = payload; }
    public CitySize Size { get; }
    public string Payload { get; }
    public string Sha256 => EconomicLedgerTests.Sha(Payload);
}

internal static class CityFixture
{
    public const double StartMinute = 1440 + 6 * 60;
    public const string RivalBusiness = "b-rival", RivalPlace = "rival:0";
    public const string Vehicle = "cart-f9";
    public const string Agency = "address:Agency%20Street:7:agency";
    public const int ResearchRadius = 500;
    public const string ResearchBusiness = "shop:0";
    public const int OpenMinute = 6 * 60, CloseMinute = 22 * 60, MealsPerHour = 30;
    /// <summary>The F6a menu, sorted by product ID as the scopes list it.</summary>
    public static readonly (string Product, int Cents)[] Menu = EconomyFixture.Menu.OrderBy(m => m.Product, StringComparer.Ordinal).ToArray();
    public static string Shop(int i) => "b" + i.ToString(CultureInfo.InvariantCulture);
    public static string ShopPlace(int i) => "shop:" + i.ToString(CultureInfo.InvariantCulture);

    public static CityCheckpoint Create(CitySize size)
    {
        var places = F5gPopulationFillTests.Places(size.Homes, size.Shape.Businesses);
        places.Add(new Place { Id = RivalPlace, Name = "Rival", Kind = PlaceKind.Shop, Capacity = 16, Position = new Position(35, 0, 40) });
        var state = Population.CreateFromHousing(Schema28Tests.Donors, places, "f9c-city", "0.41.1", CitySize.Seed, StartMinute);
        PortalPlaces.Add(state, Schema27Tests.Stations);
        var products = Menu.Select(m => m.Product).ToList();
        state.Food = new FoodCommerceState
        {
            MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion, RepresentationFactor = 1,
            Shops = Enumerable.Range(0, size.Shops).Select(i => new FoodScope { PlaceId = ShopPlace(i), BusinessId = Shop(i),
                ProductId = products[0], AdditionalProductIds = products.Skip(1).ToList() }).ToList(),
        };
        // Weighted choice, awareness and rivals from the first day, as F6a's fresh fixture.
        state.Food.ChoiceStartsDay = state.Food.AwarenessStartsDay = state.Food.RivalStartsDay = 1;
        // Attaching the market creates the roster, as Runtime does.
        var native = new CityNative(size);
        native.SetClock(StartMinute);
        new CitySimulator(state, new SyncWalk()).AttachFoodMarket(native.Shops, rivalSettings: native.Shops.RivalSettings);
        return new CityCheckpoint(size, SaveCodec.Write(state));
    }

    /// <summary>The minute-stamped native changes of Decision 3, in F6a's input stream: on day 2 the first shop raises every
    /// price by half, a product sells out, the staff is cut, the hours shorten, the rival loses half its lanes and one
    /// workplace closes; every change reverts on day 3.</summary>
    public static InterventionInput[] Inputs(CityState state)
    {
        string shop = Shop(0);
        string workplace = state.Citizens.Where(c => c.DayPlaceId.Length > 0 && state.Places.Any(p => p.Id == c.DayPlaceId && p.Kind == PlaceKind.Work))
            .GroupBy(c => c.DayPlaceId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
        double day2 = 2 * 1440, day3 = 3 * 1440;
        var list = new List<InterventionInput> { new(day2 + 5 * 60, InputKind.WorkplaceClosed, workplace, "", 1) };
        list.Add(new(day2 + 8 * 60, InputKind.RivalLanes, RivalBusiness, "", 50));
        foreach (var (product, cents) in Menu)
            list.Add(new(day2 + 9 * 60, InputKind.ProductPrice, shop, product, (int)Math.Round(cents * 1.5, MidpointRounding.AwayFromZero)));
        list.Add(new(day2 + 12 * 60, InputKind.Stock, shop, FoodProducts.HotDog, 0));
        list.Add(new(day2 + 13 * 60, InputKind.MealsPerHour, shop, "", MealsPerHour / 2));
        list.Add(new(day2 + 15 * 60, InputKind.Hours, shop, "", OpenMinute, 18 * 60));
        list.Add(new(day3 + 60, InputKind.WorkplaceClosed, workplace, "", 0));
        list.Add(new(day3 + 5 * 60, InputKind.RivalLanes, RivalBusiness, "", 100));
        foreach (var (product, cents) in Menu) list.Add(new(day3 + 5 * 60, InputKind.ProductPrice, shop, product, cents));
        list.Add(new(day3 + 5 * 60, InputKind.Stock, shop, FoodProducts.HotDog, -1));
        list.Add(new(day3 + 5 * 60, InputKind.MealsPerHour, shop, "", MealsPerHour));
        list.Add(new(day3 + 5 * 60, InputKind.Hours, shop, "", OpenMinute, CloseMinute));
        return list.ToArray();
    }

    /// <summary>One research order of each product, one a day at 10:05 for the first shop (a business holds one open order).</summary>
    public static (double Minute, string Product)[] ResearchOrders() => new[]
    {
        (1 * 1440 + 10 * 60 + 5.0, ResearchProducts.BasicLocalCode),
        (2 * 1440 + 10 * 60 + 5.0, ResearchProducts.DetailedLocalCode),
        (3 * 1440 + 10 * 60 + 5.0, ResearchProducts.DetailedCityCode),
    };

    /// <summary>The vehicle opens at 11:05 for three hours each day (a crew opening in a later hour than PC's request).
    /// On day 3 PC withdraws it at 12:30, so the opening is handed back while Street Food keeps it open.</summary>
    public const double VehicleOpens = 11 * 60 + 5, VehicleCloses = 14 * 60 + 5, WithdrawMinute = 3 * 1440 + 12 * 60 + 30;

    /// <summary>The walking route of F5h's admitted planner, answered synchronously: the reference arm's planner.</summary>
    internal sealed class SyncWalk : ICommuteTimingPlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route { Legs = new() { new RouteLeg
            { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
    }
}

/// <summary>Everything native in one arm: the shop and rival double, the Street Food stand-in behind the real bridge and
/// vendor market, the native receipt dictionary and the agency bill. It lives across a city reload, as native state does.</summary>
internal sealed class CityNative
{
    public CityNative(CitySize size)
    {
        Shops = new CityShopMarket(size.Shops);
        StreetFoodHooks.Reset();
        var v = StreetFoodHooks.Add(CityFixture.Vehicle, "cart", 10, FoodProducts.Croissant, FoodProducts.HotDog);
        v.Prices = new long[] { 200, 350 }; v.Units = new[] { 100000, 100000 }; v.MealsPerHour = 30;
        Api = new CityVendorApi(this);
    }
    public double Clock { get; private set; }
    public readonly CityShopMarket Shops;
    public readonly CityVendorApi Api;
    public readonly StreetFoodVendorTests.Sampler Sampler = new();
    public Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
    public readonly CityResearchPort Port = new();
    public StreetFoodVendorMarket? Vendor;
    public FoodMarkets? Markets;
    /// <summary>Fault: the stand-in stops honouring the lease (it lapses without handing the vehicle back) while PC is told it was renewed.</summary>
    public bool LeaseFault;
    public void SetClock(double native) { Clock = native; Shops.Clock = native; }
    /// <summary>The stand-in's native clock and lease expiry at a whole native minute.</summary>
    public void EnterMinute(long minute)
    {
        StreetFoodHooks.Now = minute;
        if (!LeaseFault) StreetFoodHooks.Expire();
    }
}

/// <summary>The player shops and the rival as native reports them. Inputs are F6a's <see cref="InterventionInput"/> kinds and
/// change only this double. Checkout is the sale authority: one order per intent, refused on changed terms, outside the
/// current native hour it is a harness failure. Every 23rd intent by hash loses its acknowledgement after the sale, so the
/// receipt is uncertain (F5h's double).</summary>
internal sealed class CityShopMarket : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority, IFoodFailureMarket,
    IFoodSaleEvidence, IRivalFoodMarket, IFoodAwarenessRevision, IWorkplaceAvailability
{
    internal sealed class Shop
    {
        public string BusinessId = "", PlaceId = "";
        public readonly SortedDictionary<string, int> Prices = new(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> Stock = new(StringComparer.Ordinal);
        public int MealsPerHour = CityFixture.MealsPerHour, OpenMinute = CityFixture.OpenMinute, CloseMinute = CityFixture.CloseMinute;
    }
    public CityShopMarket(int shops)
    {
        for (int i = 0; i < shops; i++)
        {
            var shop = new Shop { BusinessId = CityFixture.Shop(i), PlaceId = CityFixture.ShopPlace(i) };
            foreach (var (product, cents) in CityFixture.Menu) shop.Prices[product] = cents;
            Shops.Add(shop);
        }
        foreach (var (product, cents) in CityFixture.Menu) RivalPrices[product] = cents;
    }
    public readonly List<Shop> Shops = new();
    public readonly SortedDictionary<string, int> RivalPrices = new(StringComparer.Ordinal);
    public readonly RivalFoodSettings RivalSettings = new();
    public readonly SortedSet<string> ClosedWorkplaces = new(StringComparer.Ordinal);
    public double Clock;
    public const int BaseStock = 100000;
    private readonly HashSet<string> paid = new(StringComparer.Ordinal);
    /// <summary>Native orders in booking order: intent, logical minute, business, product, cents.</summary>
    public readonly List<string> Orders = new();
    public long RevenueUnits, Duplicates, HistoricalCheckouts;
    /// <summary>Fault: the next sale is booked twice natively.</summary>
    public bool InjectDuplicate;
    public Action<string>? Booked;

    public static bool Lose(ResidentMeal meal) => StableRandom.Hash(meal.IntentId, 11) % 23 == 0;
    public double EarliestMinute => Math.Floor(Clock / 60) * 60;
    public long AwarenessRevision => 1;
    public long RivalRevision => 1;

    public void Apply(InterventionInput input)
    {
        Shop For() => Shops.Single(s => s.BusinessId == input.Target);
        switch (input.Kind)
        {
            case InputKind.ProductPrice: For().Prices[input.Product] = input.Value; break;
            case InputKind.Stock: if (input.Value < 0) For().Stock.Remove(input.Product); else For().Stock[input.Product] = input.Value; break;
            case InputKind.MealsPerHour: For().MealsPerHour = input.Value; break;
            case InputKind.Hours: { var s = For(); s.OpenMinute = input.Value; s.CloseMinute = input.Value2; break; }
            case InputKind.WorkplaceClosed: if (input.Value != 0) ClosedWorkplaces.Add(input.Target); else ClosedWorkplaces.Remove(input.Target); break;
            case InputKind.RivalLanes:
                RivalSettings.RivalServiceLanes = RivalServiceCalibration.Scale(RivalServiceCalibration.DefaultLanes, input.Value, RivalServiceCalibration.MaximumLanes);
                break;
            default: throw new InvalidOperationException("F9c city has no input " + input.Kind);
        }
    }
    public bool WorkplaceOpen(string placeId, double minute) => !ClosedWorkplaces.Contains(placeId);

    private Shop? Find(string id) { foreach (var s in Shops) if (s.BusinessId == id) return s; return null; }
    private static int Units(Shop shop, string product) => shop.Stock.TryGetValue(product, out var u) ? u : BaseStock;
    private static FoodBusiness Business(Shop s) => new()
    {
        Id = s.BusinessId, PlaceId = s.PlaceId, Name = s.PlaceId, MealsPerHour = s.MealsPerHour, CustomerServicePercent = 50,
        Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = s.OpenMinute, ToMinute = s.CloseMinute }).ToList(),
        Offers = s.Prices.Select(p => new FoodOffer { ProductId = p.Key, Price = Money.FromCents(p.Value), Units = Units(s, p.Key) }).ToList(),
    };
    public IReadOnlyList<FoodBusiness> Read(double minute) => Shops.Select(Business).ToArray();
    public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximumCandidates) => Read(minute).Take(maximumCandidates).ToArray();
    public FoodBusiness? ReadBusiness(string id, double minute) { var s = Find(id); return s == null ? null : Business(s); }
    public FoodBusiness? ReadFailureBusiness(string id, string place, double minute) => ReadBusiness(id, minute);
    public FoodBusiness? CaptureBusiness(string id, double minute) => ReadBusiness(id, minute);
    public IDisposable BeginCapture(double minute) => NoCapture.Instance;
    public bool IsCandidate(FoodScope scope) => Find(scope.BusinessId) != null;
    public bool CanServe(string id) => Find(id) != null;
    public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    private sealed class NoCapture : IDisposable { public static readonly NoCapture Instance = new(); public void Dispose() { } }

    public FoodSale Buy(ResidentMeal meal, double minute)
    {
        if (minute < EarliestMinute || minute >= EarliestMinute + 60) { HistoricalCheckouts++; return new FoodSale(false, "historical native checkout"); }
        if (meal.TargetKind != FoodAlternativeKind.Player) throw new CityViolation("native rival checkout attempted");
        var shop = Find(meal.BusinessId);
        int at = (int)(minute % 1440);
        string? refusal = shop == null ? "Business unavailable" : shop.MealsPerHour <= 0 ? "No staffed counter"
            : at < shop.OpenMinute || at >= shop.CloseMinute ? "Closed"
            : !shop.Prices.TryGetValue(meal.ProductId, out var cents) || Money.FromCents(cents) != meal.Price ? "Timestamped price changed"
            : Units(shop, meal.ProductId) <= 0 ? "Out of stock" : null;
        if (refusal != null) return new FoodSale(false, refusal);
        if (!paid.Add(meal.IntentId)) { Duplicates++; return new FoodSale(false, "Duplicate native order"); }
        Book(meal, minute);
        if (InjectDuplicate) { InjectDuplicate = false; Duplicates++; Book(meal, minute); }
        if (Lose(meal)) throw new InvalidOperationException("Lost native acknowledgement");
        return new FoodSale(true, "Confirmed");
    }
    private void Book(ResidentMeal meal, double minute)
    {
        RevenueUnits += meal.Price.Units;
        string row = "o|" + CityOracles.M(minute) + "|" + meal.IntentId + "|" + meal.BusinessId + "|" + meal.ProductId + "|" + meal.Price.Units.ToString(CultureInfo.InvariantCulture);
        Orders.Add(row);
        Booked?.Invoke(row);
    }

    public void CloseRivals() { }
    public bool NeedsRivalRefresh(double minute) => false;
    public void ContinueRivalRefresh(double minute) { }
    public IEnumerable<FoodScope> RivalScopes => new[] { new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = CityFixture.RivalBusiness,
        PlaceId = CityFixture.RivalPlace, ProductId = RivalPrices.Keys.First(), AdditionalProductIds = RivalPrices.Keys.Skip(1).ToList() } };
    public bool IsRivalCandidate(FoodScope scope) => scope.BusinessId == CityFixture.RivalBusiness;
    public bool RivalAtPlace(string place) => place == CityFixture.RivalPlace;
    public FoodAlternativeObservation? ReadRival(string id) => id != CityFixture.RivalBusiness ? null
        : new FoodAlternativeObservation(CityFixture.RivalBusiness, CityFixture.RivalPlace, "Rival Street", 2, 0, "fastfood", "rival-owner",
            FoodAlternativeKind.Rival, true, true, false, 50, 0, RivalPrices.Select(p => new FoodAdvertisedOffer(p.Key, Money.FromCents(p.Value))),
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, CityFixture.OpenMinute, CityFixture.CloseMinute)));
}

/// <summary>The real Street Food bridge over the contract stand-in, recording every confirmed sale with the stand-in's lease
/// and owner at that moment. With <see cref="CityNative.LeaseFault"/> a renewal is reported to PC but not made.</summary>
internal sealed class CityVendorApi : IVendorApi
{
    private readonly IVendorApi inner = StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi));
    private readonly CityNative native;
    public CityVendorApi(CityNative native) { this.native = native; }
    public sealed record Sale(long Minute, string Vehicle, long Opening, string Item, long Cents, long Sequence, long LeaseUntil, string Owner);
    public readonly List<Sale> Sales = new();
    public Action<string>? Booked;
    public long Revision => inner.Revision;
    public VendorApiStatus BeginRead(out int count) => inner.BeginRead(out count);
    public void CopyVehicle(int index, VendorVehicle into) => inner.CopyVehicle(index, into);
    public VendorApiStatus RequestOwner(string id, bool want, out string reason) => inner.RequestOwner(id, want, out reason);
    public VendorApiStatus Renew(int day, int minute, out int untilDay, out int untilMinute)
    {
        if (!native.LeaseFault) return inner.Renew(day, minute, out untilDay, out untilMinute);
        long until = day * 1440L + minute + 60;
        untilDay = (int)(until / 1440); untilMinute = (int)(until % 1440);
        return VendorApiStatus.Ready;
    }
    public VendorApiStatus ReleaseAll() => inner.ReleaseAll();
    public VendorApiStatus Sell(string id, long opening, string item, long cents, int day, int minute, out VendorSaleResult? sale, out bool uncertain, out string reason)
    {
        var owner = StreetFoodHooks.Vehicles.Find(v => v.Id == id)?.Owner ?? "";
        long lease = StreetFoodHooks.LeaseUntil;
        var status = inner.Sell(id, opening, item, cents, day, minute, out sale, out uncertain, out reason);
        if (sale != null)
        {
            var row = new Sale(day * 1440L + minute, id, opening, item, cents, sale.Sequence, lease, owner);
            Sales.Add(row);
            Booked?.Invoke("v|" + row.Minute.ToString(CultureInfo.InvariantCulture) + "|" + id + "|" + opening.ToString(CultureInfo.InvariantCulture)
                + "|" + item + "|" + cents.ToString(CultureInfo.InvariantCulture));
        }
        return status;
    }
}

/// <summary>The native agency bill (ChangeMoneySafe contract of the C5 doubles): money in dollars as a float, one tagged
/// marketing record per charge. With <see cref="ChargeWithoutReport"/> the next boundary also books a tagged charge that no
/// delivered report backs.</summary>
internal sealed class CityResearchPort : IResearchChargePort
{
    public const float OpeningMoney = 50000;
    public float Money = OpeningMoney;
    public int Day;
    public readonly List<ResearchChargeRecord> Queue = new();
    public bool ChargeWithoutReport;
    public bool TagStored => true;
    public float Balance => Money;
    public int BilledDay => Day;
    public bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target)
    { target = new ResearchChargeTarget("Shop " + order.BusinessAddress, order.BusinessAddress); return true; }
    public void CollectTagged(List<ResearchChargeRecord> into) => into.AddRange(Queue);
    public object? Tail() => Queue.Count;
    public void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into) { for (int i = (int)tail!; i < Queue.Count; i++) into.Add(Queue[i]); }
    public bool Charge(in ResearchChargeRequest request, Action enteringNative)
    {
        enteringNative();
        if (Money + request.Amount < 0) return false;
        Money += request.Amount;
        Queue.Add(new ResearchChargeRecord(request.OrderId, true, request.TaxDeductible, request.Amount, (float)Math.Floor(Money), request.Day, request.Target.Address));
        return true;
    }
    public void Boundary()
    {
        if (!ChargeWithoutReport) return;
        ChargeWithoutReport = false;
        Money -= 500;
        Queue.Add(new ResearchChargeRecord(ResearchLedgerOrderId(999), true, false, -500, (float)Math.Floor(Money), Day, CityFixture.ResearchBusiness));
    }
    public static string ResearchLedgerOrderId(long sequence) => "pcr-" + sequence.ToString(CultureInfo.InvariantCulture);
}

/// <summary>An F9c oracle failure. Thrown, never caught by the scenario, so a broken invariant fails the test loudly.</summary>
internal sealed class CityViolation : Exception { public CityViolation(string message) : base(message) { } }
