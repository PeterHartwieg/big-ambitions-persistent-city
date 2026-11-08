using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.EconomyInterventions;

/// <summary>Detached native double for the F6a harness. It reports player supply, a read-only rival
/// observation and walking routes; it is the only place an intervention input changes anything.
/// Player checkout is the sale authority (one order per intent, refused on changed terms). No rival
/// checkout exists. Every read is fenced to the current input epoch.</summary>
public sealed class EconomyMarket : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision, IRivalFoodMarket,
    IFoodBusinessAuthority, IFoodSaleEvidence
{
    public sealed class Shop
    {
        public string BusinessId = "", PlaceId = "", Name = "";
        public readonly SortedDictionary<string, int> Prices = new(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> StockOverride = new(StringComparer.Ordinal);
        public int MealsPerHour, OpenMinute, CloseMinute, RouteTimePercent = 100;
        // Base walking minutes each way; the second site is twice as far as the first.
        public double BaseRouteMinutes = 1;
        public bool DoorClosed, SiteOpen = true;
    }
    public sealed class Rival
    {
        public string BusinessId = "", PlaceId = "";
        public readonly SortedDictionary<string, int> Prices = new(StringComparer.Ordinal);
        public int OpenMinute, CloseMinute;
        // Percentage of the default F6b lanes; units remain fixed in the lane-only arm.
        public int EstimatedLanes;
        public bool Listed = true;
    }
    sealed class Batch : IDisposable { public void Dispose() { } }
    private static readonly Batch NoBatch = new();

    // F6h shares the live settings instance with Core. Timestamped lane inputs therefore
    // affect later evaluations/arrivals. The base's committed travellers still meet the
    // live arrival limit; no held lane/unit fields or old-term replay are invented here.
    public readonly RivalFoodSettings RivalSettings = new();
    public FoodChoiceSettings ChoiceSettings = new();
    public FoodChangeJournal? Journal;
    public readonly List<Shop> Shops = new();
    public readonly Rival RivalShop = new();
    public readonly SortedSet<string> ClosedWorkplaces = new(StringComparer.Ordinal);
    public int WorkerRoutesBlockedPercent;
    public const int BaseStockUnits = 100000;

    // Native-side evidence that persists across a city save/reload, as native game state would.
    private readonly HashSet<string> paid = new(StringComparer.Ordinal);
    public long NativeOrders, NativeRevenueUnits, DuplicateOrders, Refusals;
    public long Epoch { get; private set; } = -1;
    public long EpochReadViolations;
    public bool InjectDuplicateOrder;

    public double EarliestMinute => Math.Floor(Epoch / 60.0) * 60;
    public long AwarenessRevision => 1;
    public long RivalRevision => 1;

    public void Enter(long epoch)
    {
        if (epoch <= Epoch) throw new InputViolation("native clock did not advance to a new epoch");
        Epoch = epoch;
    }
    private void Fence(double minute)
    {
        // A native read belongs to the epoch that is currently published. Any read outside it
        // would mean an input could be observed mid-epoch or before its boundary.
        if (!(minute >= Epoch && minute < Epoch + 1)) { EpochReadViolations++; throw new InputViolation("native read outside its input epoch: " + minute + " in " + Epoch); }
    }

    public void Apply(InterventionInput input, long epoch)
    {
        if (input.Minute != epoch) throw new InputViolation("input applied at a different epoch");
        Shop ShopFor() => Shops.Single(s => s.BusinessId == input.Target);
        switch (input.Kind)
        {
            case InputKind.ProductPrice: ShopFor().Prices[input.Product] = input.Value; break;
            case InputKind.MealsPerHour: ShopFor().MealsPerHour = input.Value; break;
            case InputKind.Hours: { var s = ShopFor(); s.OpenMinute = input.Value; s.CloseMinute = input.Value2; break; }
            case InputKind.Stock:
                if (input.Value < 0) ShopFor().StockOverride.Remove(input.Product); else ShopFor().StockOverride[input.Product] = input.Value;
                break;
            case InputKind.Door: ShopFor().DoorClosed = input.Value != 0; break;
            case InputKind.SiteOpen: ShopFor().SiteOpen = input.Value != 0; break;
            case InputKind.RouteTimePercent: ShopFor().RouteTimePercent = input.Value; break;
            case InputKind.WorkplaceClosed: if (input.Value != 0) ClosedWorkplaces.Add(input.Target); else ClosedWorkplaces.Remove(input.Target); break;
            case InputKind.WorkerRoutesBlockedPercent: WorkerRoutesBlockedPercent = input.Value; break;
            case InputKind.RivalPrice: RivalShop.Prices[input.Product] = input.Value; break;
            case InputKind.RivalLanes:
                RivalShop.EstimatedLanes = input.Value;
                RivalSettings.RivalServiceLanes = RivalServiceCalibration.Scale(RivalServiceCalibration.DefaultLanes,
                    input.Value, RivalServiceCalibration.MaximumLanes);
                break;
            default: throw new InputViolation("unknown input kind");
        }
    }

    private Shop? Find(string businessId) { foreach (var s in Shops) if (s.BusinessId == businessId) return s; return null; }
    private static List<OpeningWindow> Daily(int from, int to)
        => Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = from, ToMinute = to }).ToList();
    private static int Units(Shop shop, string product) => shop.StockOverride.TryGetValue(product, out var u) ? u : BaseStockUnits;

    private FoodBusiness Business(Shop shop)
    {
        // Consume the exact image this detached native double already reads, as Runtime does.
        // No extra observation or speculative sweep is introduced by the journal.
        if (Journal != null)
        {
            var offers = shop.Prices.Select(p => new ShopObservedOffer { ProductId = p.Key,
                PriceUnits = Money.FromCents(p.Value).Units, Stock = Units(shop, p.Key) }).ToArray();
            Journal.ObserveSupply(shop.BusinessId, shop.PlaceId, (int)Epoch, offers, offers.Length,
                (shop.MealsPerHour + 29) / 30);
            Journal.ObserveHours(shop.BusinessId, shop.PlaceId, (int)Epoch, Daily(shop.OpenMinute, shop.CloseMinute));
        }
        return new FoodBusiness {
        Id = shop.BusinessId, PlaceId = shop.PlaceId, Name = shop.Name, MealsPerHour = shop.MealsPerHour,
        Hours = Daily(shop.OpenMinute, shop.CloseMinute), CustomerServicePercent = 50,
        ServiceUnavailableReason = shop.DoorClosed ? "Door temporarily closed" : "",
        Offers = shop.Prices.Select(p => new FoodOffer { ProductId = p.Key, Price = Money.FromCents(p.Value), Units = Units(shop, p.Key) }).ToList() };
    }

    public IReadOnlyList<FoodBusiness> Read(double minute) { Fence(minute); return Shops.Where(s => s.SiteOpen).Select(Business).ToArray(); }
    public IDisposable BeginCapture(double minute) { Fence(minute); return NoBatch; }
    public bool IsCandidate(FoodScope scope) => Find(scope.BusinessId)?.SiteOpen == true;
    public bool CanServe(string businessId) => Find(businessId)?.SiteOpen == true;
    public FoodBusiness? CaptureBusiness(string businessId, double minute)
    {
        Fence(minute);
        var shop = Find(businessId);
        return shop == null || !shop.SiteOpen ? null : Business(shop);
    }
    public FoodSale Buy(ResidentMeal meal, double minute)
    {
        Fence(minute);
        if (minute < EarliestMinute || minute >= EarliestMinute + 60) throw new InputViolation("historical native checkout");
        if (meal.TargetKind != FoodAlternativeKind.Player) throw new InputViolation("native rival checkout attempted");
        var shop = Find(meal.BusinessId);
        int at = (int)(minute % 1440);
        string? refusal = shop == null || !shop.SiteOpen ? "Business unavailable"
            : shop.DoorClosed ? "Door temporarily closed" : shop.MealsPerHour <= 0 ? "No staffed counter"
            : at < shop.OpenMinute || at >= shop.CloseMinute ? "Closed"
            : !shop.Prices.TryGetValue(meal.ProductId, out var cents) || Money.FromCents(cents) != meal.Price ? "Timestamped price changed"
            : Units(shop, meal.ProductId) <= 0 ? "Out of stock" : null;
        if (refusal != null) { Refusals++; return new FoodSale(false, refusal); }
        if (!paid.Add(meal.IntentId)) { DuplicateOrders++; return new FoodSale(false, "Duplicate native order"); }
        NativeOrders++; NativeRevenueUnits += meal.Price.Units;
        if (InjectDuplicateOrder) { InjectDuplicateOrder = false; DuplicateOrders++; NativeOrders++; NativeRevenueUnits += meal.Price.Units; }
        return new FoodSale(true, "Native double confirmed");
    }
    public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);

    public void CloseRivals() { }
    public bool NeedsRivalRefresh(double minute) => false;
    public void ContinueRivalRefresh(double minute) { }
    public IEnumerable<FoodScope> RivalScopes => RivalShop.Listed ? new[] { RivalScope() } : Array.Empty<FoodScope>();
    public FoodScope RivalScope() => new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = RivalShop.BusinessId,
        PlaceId = RivalShop.PlaceId, ProductId = RivalShop.Prices.Keys.First(), AdditionalProductIds = RivalShop.Prices.Keys.Skip(1).ToList() };
    public bool IsRivalCandidate(FoodScope scope) => RivalShop.Listed && scope.BusinessId == RivalShop.BusinessId;
    public bool RivalAtPlace(string place) => RivalShop.Listed && place == RivalShop.PlaceId;
    public FoodAlternativeObservation? ReadRival(string businessId)
    {
        if (!RivalShop.Listed || businessId != RivalShop.BusinessId) return null;
        return new FoodAlternativeObservation(RivalShop.BusinessId, RivalShop.PlaceId, "Harness Street", 2, 0, "fastfood", "rival-owner",
            FoodAlternativeKind.Rival, true, true, false, 50, 0,
            RivalShop.Prices.Select(p => new FoodAdvertisedOffer(p.Key, Money.FromCents(p.Value))),
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, RivalShop.OpenMinute, RivalShop.CloseMinute)));
    }
}

/// <summary>One-minute walking routes (the F1 supported fixture); two minutes each way to the second
/// site at twice the distance; both scaled by a shop's access input.
/// Work trips refuse while the workplace is closed or the worker's route is blocked.</summary>
public sealed class EconomyRoutes : ICitizenRoutePlanner, IFoodWalkingPace, IWorkplaceAvailability
{
    private readonly EconomyMarket market;
    public EconomyRoutes(EconomyMarket market) { this.market = market; }
    public double WalkingMetresPerMinute => 120;
    public bool WorkplaceOpen(string placeId, double minute) => !market.ClosedWorkplaces.Contains(placeId);
    public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
    {
        double minutes = 1;
        foreach (var shop in market.Shops)
            if (shop.PlaceId == to.Id || shop.PlaceId == from.Id) minutes = shop.BaseRouteMinutes * shop.RouteTimePercent / 100.0;
        return new Route { Legs = { new RouteLeg { From = from.Position, To = to.Position, Minutes = minutes, Mode = TravelMode.Walk } } };
    }
    public Route? Plan(Place from, Place to, double departureMinute, CitizenState citizen)
    {
        if (to.Kind == PlaceKind.Work && (market.ClosedWorkplaces.Contains(to.Id) || Blocked(citizen.Identity.Id, market.WorkerRoutesBlockedPercent)))
            return null;
        return Plan(from, to, departureMinute, citizen.Preferences);
    }
    public static bool Blocked(string id, int percent) => percent > 0 && StableBucket(id) < percent;
    public static int StableBucket(string id)
    {
        uint hash = 2166136261;
        foreach (char c in id) { hash ^= c; hash *= 16777619; }
        return (int)(hash % 100);
    }
}

/// <summary>The F1 supported fixture shape (957 residents / 1,914 consumers, one-minute walking routes,
/// minute-stamped inputs), extended with a three-product own shop, an equidistant rival with the same
/// menu, price and hours, a dormant second site at twice the distance and ten workplaces.</summary>
public static class EconomyFixture
{
    public const int Residents = 957;
    public const int RepresentationFactor = 2;
    public const int Workplaces = 10;
    public const double StartMinute = 1440;
    public const int OpenMinute = 7 * 60, CloseMinute = 22 * 60;
    // Two staffed counters at 30 meals per hour each.
    public const int MealsPerHour = 60;
    public static readonly (string Product, int Cents)[] Menu = {
        (FoodProducts.Burger, 800), (FoodProducts.Croissant, 300), (FoodProducts.HotDog, 500) };
    public const string Shop = "b-shop", FarShop = "b-far", RivalBusiness = "b-rival";
    // Twice the one-minute own-shop walk, each way, matching the doubled distance.
    public const double FarSiteRouteMinutes = 2;

    public static string WorkplaceId(int index) => "work" + index;
    public static string PlaceOf(string business) => business == Shop ? "shop" : business == FarShop ? "shop-far" : business == RivalBusiness ? "rival" : "";

    public static (CityState State, EconomyMarket Market) Build(int seed, int residents, bool rivalListed = true, bool ownListed = true)
    {
        var places = new List<Place> {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents },
            new Place { Id = "shop", Name = "Own shop", Kind = PlaceKind.Shop, Position = new Position(2, 0, 0) },
            new Place { Id = "shop-far", Name = "Own second site", Kind = PlaceKind.Shop, Position = new Position(4, 0, 0) },
            new Place { Id = "rival", Name = "Rival", Kind = PlaceKind.Shop, Position = new Position(-2, 0, 0) },
        };
        for (int i = 0; i < Workplaces; i++)
            places.Add(new Place { Id = WorkplaceId(i), Name = "Work " + i, Kind = PlaceKind.Work, Capacity = residents / Workplaces + 1, Position = new Position(0, 1, 0) });
        var state = Population.Create(Enumerable.Range(0, Math.Min(residents, 5000)).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), places,
            "economy-interventions", "0.40.4", seed, StartMinute);
        if (residents > 5000)
        {
            // The immutable donor catalogue is capped at 5,000. Stress cities grow through
            // the same F5 housing expansion as the E5d save fixtures, retaining stable pc: IDs.
            state.Places.Single(p => p.Id == "home").Capacity = (int)(((long)residents * 10 + 8) / 9);
            Population.ExpandToHousing(state);
            if (state.Citizens.Count != residents) throw new InvalidOperationException("stress housing target mismatch");
        }
        foreach (var person in state.Citizens)
        {
            person.Phase = CitizenPhase.AtPlace; person.PlaceId = "home"; person.Activity = Activity.Home;
            person.NextEventMinute = person.ActivityUntilMinute = 4320;
        }
        var products = Menu.Select(m => m.Product).OrderBy(p => p, StringComparer.Ordinal).ToList();
        FoodScope Scope(string place, string business) => new FoodScope { PlaceId = place, BusinessId = business,
            ProductId = products[0], AdditionalProductIds = products.Skip(1).ToList() };
        state.Food = new FoodCommerceState { RepresentationFactor = RepresentationFactor, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new List<FoodScope> { Scope("shop", Shop), Scope("shop-far", FarShop) } };
        // F5d: one subway portal, so the factor-2 roster creates one visitor per resident on its own routine.
        PortalPlaces.Add(state, new[] { new PortalStation("subway:economy", new Position(0, 0, 1)) });
        var market = new EconomyMarket();
        foreach (var (business, place, open, minutes) in new[] { (Shop, "shop", ownListed, 1.0), (FarShop, "shop-far", false, FarSiteRouteMinutes) })
        {
            var shop = new EconomyMarket.Shop { BusinessId = business, PlaceId = place, Name = place, MealsPerHour = MealsPerHour,
                OpenMinute = OpenMinute, CloseMinute = CloseMinute, SiteOpen = open, BaseRouteMinutes = minutes };
            foreach (var (product, cents) in Menu) shop.Prices[product] = cents;
            market.Shops.Add(shop);
        }
        market.RivalShop.BusinessId = RivalBusiness; market.RivalShop.PlaceId = "rival";
        market.RivalShop.OpenMinute = OpenMinute; market.RivalShop.CloseMinute = CloseMinute;
        market.RivalShop.EstimatedLanes = InterventionArms.RivalLanesDefaultPercent; market.RivalShop.Listed = rivalListed;
        foreach (var (product, cents) in Menu) market.RivalShop.Prices[product] = cents;
        return (state, market);
    }

    /// <summary>Attaches the current Core model unchanged: weighted choice, awareness, rivals and budgets.</summary>
    public static CitySimulator Attach(CityState state, EconomyMarket market, EconomyRoutes routes, bool fresh)
    {
        var city = new CitySimulator(state, routes);
        city.AttachFoodMarket(market, choiceSettings: market.ChoiceSettings, rivalSettings: market.RivalSettings);
        if (fresh)
        {
            city.EnableHouseholdFinances();
            state.Food!.ChoiceStartsDay = state.Food.AwarenessStartsDay = state.Food.RivalStartsDay = 1;
        }
        return city;
    }
}
