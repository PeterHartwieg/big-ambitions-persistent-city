using System;
using System.Collections.Generic;
using System.Linq;
using AI.Customers.CustomerEntries;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

// Actual NativeFoodMarket/NativeFoodHours/FoodSupplyReader source against synthetic native surfaces.
// The stand-in simulator mirrors the native SetUp (stocked shelves, staffed registers) and ProcessCustomer
// (one product, one paper bag, one completed order). It is not game execution.
static partial class Program
{
    static int checks;
    static void Check(bool valid, string message) { if (!valid) throw new Exception("FAILED: " + message); checks++; }
    const string Croissant = FoodProducts.Croissant, Burger = FoodProducts.Burger;
    static readonly double Minute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0;

    static ItemInstance Shelf(string id, string product, int units) => new() { id = id,
        ItemCached = new Item { type = ItemType.ShowcaseShelf, itemsThatCanShowcase = new[] { product } },
        cargoInstances = new() { new() { itemName = product, amount = units } } };

    static BuildingRegistration Registration(int street, int croissants, int burgers, int bags)
    {
        var reg = new BuildingRegistration { StreetName = "Food Street", StreetNumber = street };
        reg.cachedAvailableProducts.AddRange(new[] { Croissant, Burger });
        reg.retailPrices.Add(new RetailPrice { itemName = Croissant, price = 3f });
        reg.retailPrices.Add(new RetailPrice { itemName = Burger, price = 6f });
        reg.Shelves.Add(Shelf("shelf-croissant", Croissant, croissants));
        reg.Shelves.Add(Shelf("shelf-burger", Burger, burgers));
        reg.Registers.Add(new PointOfSale { instance = Shelf("register", "bag", bags) });
        for (int day = 1; day <= 7; day++)
            reg.scheduleDays.Add(new ScheduleDay { day = day, isOpen = true, openingHourSlots = { new OpeningHourSlot { startingHour = 0, endingHour = 24 } } });
        return reg;
    }

    sealed class Fixture : IDisposable
    {
        public readonly CityBuildingController Controller;
        public readonly FoodCommerceState State = new();
        public readonly NativeFoodMarket Market;
        public readonly string PlaceId;
        public BuildingRegistration Reg => Controller.buildingRegistration!;
        public Fixture(int croissants = 5, int burgers = 7, int bags = 3)
        {
            Hooks.Reset(); SaveGameManager.Current.modData.Clear(); SaveGameManager.Current.Minute = 0;
            Controller = new CityBuildingController { buildingRegistration = Registration(1, croissants, burgers, bags) };
            CityManager.Instance.cityBuildingControllers = new[] { Controller };
            PlaceId = NativeFoodMarket.PlaceId(Reg);
            Market = new NativeFoodMarket(State, new[] { new Place { Id = PlaceId, Kind = PlaceKind.Shop } });
        }
        public string BusinessId => State.Shops.Single().BusinessId;
        public ResidentMeal Meal(string resident, string product = Croissant, decimal cents = 300) => new() {
            ResidentId = resident, Day = SaveGameManager.Current.Day, Kind = MealKind.Lunch, PlaceId = PlaceId, BusinessId = BusinessId,
            ProductId = product, PriceCents = cents, AllowanceCents = 1200, ServiceMinutes = 10, Status = MealStatus.Serving };
        public int Stock(string product) => Reg.Shelves.Single(s => s.cargoInstances![0].itemName == product).cargoInstances![0].amount;
        public int Bags => Reg.Registers.Sum(r => r.instance.cargoInstances![0].amount);
        public FoodReceiptStatus Receipt(ResidentMeal meal) => FoodSaleReceipts.ReadStatus(SaveGameManager.Current.modData, meal);
        public void Dispose() => Market.Dispose();
    }

    static void FractionalCheckoutMatchesNativeAndRejectsSameCentPriceChanges()
    {
        using var f = new Fixture();
        var price = f.Reg.retailPrices.Single(p => p.itemName == Croissant);
        price.price = 10.2528f;
        decimal cents = FoodMoney.FromNative(price.price);
        var meal = f.Meal("fractional", cents: cents);
        var budget = new ResidentBudget { BalanceCents = 100000, OpeningBalanceCents = 100000 };
        Check(HouseholdFinance.HoldPurchase(budget, cents), "hold exact fractional price");
        Check(f.Market.Buy(meal, Minute).Paid, "native adapter accepts fractional approved price");
        HouseholdFinance.ConfirmPurchase(budget);
        Check(budget.PreparedFoodSpentCents == cents && budget.BalanceCents == 100000 - cents, "wallet matches native quote");
        Check(f.Receipt(meal) == FoodReceiptStatus.Paid, "fractional paid receipt");
        int units = f.Stock(Croissant);
        Check(f.Market.Buy(meal, Minute).Paid && f.Stock(Croissant) == units, "fractional replay is idempotent");
        var next = f.Meal("price-changed", cents: cents);
        price.price = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(price.price) + 1);
        Check(!f.Market.Buy(next, Minute).Paid, "one native ULP price change refuses stale approval");
        Check(f.Stock(Croissant) == units && f.Receipt(next) == FoodReceiptStatus.Missing, "price refusal consumes no stock or receipt");
        price.price = 10.2528f;
        Hooks.OpenCheck = () => price.price = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(10.2528f) + 1);
        Check(!f.Market.Buy(next, Minute).Paid, "price mutation after live quote refuses before native sale");
        Check(f.Stock(Croissant) == units && f.Receipt(next) == FoodReceiptStatus.Missing, "setup-price refusal leaves stock and receipts unchanged");
    }

    static void InvalidPreSalePricesRefuseWithoutUncertainFunds()
    {
        foreach (bool beforeCatalogue in new[] { true, false })
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 0f, -1f, 10001f, float.Epsilon, 1e-20f })
        {
            using var f = new Fixture();
            var meal = f.Meal("invalid-price");
            var budget = new ResidentBudget { BalanceCents = 100000, OpeningBalanceCents = 100000 };
            Check(HouseholdFinance.HoldPurchase(budget, meal.PriceCents), "checkout owns held funds before price mutation");
            int units = f.Stock(Croissant), bags = f.Bags, orders = f.Reg.unprocessedCompletedOrders.Count;
            // Establish a valid decision quote, then mutate before the first live checkout
            // read or after that read during setup. Both are known pre-sale refusals.
            Check(f.Market.Read(Minute).Single().Offers.Any(o => o.ProductId == Croissant), "valid decision quote exists");
            if (beforeCatalogue) f.Reg.retailPrices.Single(p => p.itemName == Croissant).price = invalid;
            else Hooks.OpenCheck = () => f.Reg.retailPrices.Single(p => p.itemName == Croissant).price = invalid;
            var result = f.Market.Buy(meal, Minute);
            Check(!result.Paid && result.Reason.Contains("supported range"), "unsupported live price returns definite refusal, not an exception");
            Check(f.Receipt(meal) == FoodReceiptStatus.Missing && f.Market.NativePurchases == 0,
                "invalid pre-sale price creates no receipt or purchase");
            Check(f.Stock(Croissant) == units && f.Bags == bags && f.Reg.unprocessedCompletedOrders.Count == orders,
                "invalid pre-sale price leaves inventory and orders unchanged");
            // FoodCommerce refunds definite refusals; only thrown/unknown outcomes retain holds.
            HouseholdFinance.RefusePurchase(budget);
            Check(budget.BalanceCents == budget.OpeningBalanceCents && budget.HeldPurchaseCents == 0
                && budget.PreparedFoodSpentCents == 0, "definite refusal permits complete refund without stranded funds");
        }
    }

    static void CatalogueRefusalsRefundBeforeSale()
    {
        foreach (string fault in new[] { "price", "quality", "demand" })
        {
            using var f = new Fixture();
            var meal = f.Meal("invalid-catalogue");
            var budget = new ResidentBudget { BalanceCents = 100000, OpeningBalanceCents = 100000 };
            Check(HouseholdFinance.HoldPurchase(budget, meal.PriceCents), "hold before catalogue refusal");
            if (fault == "price") f.Reg.retailPrices.Add(new RetailPrice { itemName = Croissant, price = 3f });
            if (fault == "quality") f.Reg.satisfaction = new Satisfaction { customerService = 101 };
            if (fault == "demand")
            {
                f.Controller.building.Neighbourhood = "district";
                SaveGameManager.Current.productMarketEntries = new() { new() { itemName = Croissant,
                    demandValues = new() { new() { neighborhood = "district" }, new() { neighborhood = "district" } } } };
            }
            int units = f.Stock(Croissant), bags = f.Bags, orders = f.Reg.unprocessedCompletedOrders.Count;
            var result = f.Market.Buy(meal, Minute);
            string expected = fault == "price" ? "Ambiguous food price" : fault == "quality" ? "Customer-service score" : "Duplicate saved product/district demand";
            Check(!result.Paid && result.Reason.StartsWith("Catalogue refused: " + expected, StringComparison.Ordinal),
                "known catalogue refusal is definite and specific: " + fault);
            Check(f.Receipt(meal) == FoodReceiptStatus.Missing && f.Market.NativePurchases == 0,
                "catalogue refusal precedes receipt and sale");
            Check(f.Stock(Croissant) == units && f.Bags == bags && f.Reg.unprocessedCompletedOrders.Count == orders,
                "catalogue refusal leaves inventory and orders intact");
            HouseholdFinance.RefusePurchase(budget);
            Check(budget.BalanceCents == budget.OpeningBalanceCents && budget.HeldPurchaseCents == 0,
                "catalogue refusal permits full refund");
            SaveGameManager.Current.productMarketEntries = null;
        }
    }

    static void Main()
    {
        InvalidPreSalePricesRefuseWithoutUncertainFunds();
        CatalogueRefusalsRefundBeforeSale();
        FractionalCheckoutMatchesNativeAndRejectsSameCentPriceChanges();
        SuccessfulCheckoutUsesOnePreSaleRead();
        SameFrameSalesAndChangesAreSeenByTheNextCheckout();
        ReplacedRegistrationIsReadAfresh();
        UncertainNativeOutcomesStillStopTheCheckout();
        ChangesBetweenTheReusedImageAndTheSaleFailClosed();
        AnotherMinuteReadsAfresh();
        RefusalsAndRepeatsMakeNoSale();
        DecisionReadsInOneMinuteShareOneImage();
        DecisionImageNeverReachesCheckout();
        DecisionImageInvalidation();
        DecisionWorkload();
        DiscoveryWalksTheWholeRingWithoutRebuildingPlaceIds();
        Check(UnityEngine.Object.Destroyed == UnityEngine.ScriptableObject.Created, "every temporary simulator destroyed");
        Console.WriteLine(checks + " actual NativeFoodMarket checkout checks passed (synthetic native surfaces, not game execution).");
    }

    static void SuccessfulCheckoutUsesOnePreSaleRead()
    {
        using var f = new Fixture();
        var meal = f.Meal("r1");
        int orders = f.Reg.unprocessedCompletedOrders.Count;
        long native = RetailBusinessSimulator.Setups, reader = FoodSupplyReader.Setups;
        var sale = f.Market.Buy(meal, Minute);
        Check(sale.Paid && sale.Reason.Contains("confirmed"), "supported checkout is paid: " + sale.Reason);
        Check(f.Stock(Croissant) == 4 && f.Bags == 2 && f.Stock(Burger) == 7, "one croissant and one bag consumed, the other product untouched");
        Check(f.Reg.unprocessedCompletedOrders.Count == orders + 1, "one completed native order");
        Check(f.Receipt(meal) == FoodReceiptStatus.Paid && f.Market.IsConfirmedPaid(meal), "saved receipt confirms the intention");
        Check(f.Market.NativePurchases == 1 && f.Market.CheckoutSupplyReuses == 1, "the pre-sale image was the selected read");
        // Selected read, sale and after image: one native setup fewer than separate selected and before reads.
        Check(RetailBusinessSimulator.Setups - native == 3, "three native setups per successful checkout, was four");
        Check(FoodSupplyReader.Setups - reader == 2, "two supply-reader setups per successful checkout");
        Check(Hooks.PriceChecks == 1 && Hooks.LastPriceAccepted, "native price check ran through the approved-price prefix");
        Check(FoodSupplyReader.SetupMilliseconds >= 0, "setup timer is readable");
    }

    static void SameFrameSalesAndChangesAreSeenByTheNextCheckout()
    {
        using var f = new Fixture(croissants: 1);
        Check(f.Market.Buy(f.Meal("r1"), Minute).Paid, "first checkout takes the last croissant");
        long native = RetailBusinessSimulator.Setups;
        var second = f.Market.Buy(f.Meal("r2"), Minute);
        Check(!second.Paid && second.Reason == "Stock or staffing changed", "same-minute second checkout sees the sale: " + second.Reason);
        Check(f.Receipt(f.Meal("r2")) == FoodReceiptStatus.Missing && f.Stock(Croissant) == 0 && f.Bags == 2, "refusal makes no receipt or sale");
        Check(RetailBusinessSimulator.Setups - native == 1, "a refused checkout makes only its selected read");
        Check(f.Market.Buy(f.Meal("r3", Burger, 600), Minute).Paid, "another menu product is still sold");

        // Native changes between checkouts in the same frame are read live.
        f.Reg.Registers.Clear();
        Check(f.Market.Buy(f.Meal("r4", Burger, 600), Minute).Reason == "Stock or staffing changed", "unstaffed register refuses");
        f.Reg.Registers.Add(new PointOfSale { instance = Shelf("register", "bag", 0) });
        Check(f.Market.Buy(f.Meal("r5", Burger, 600), Minute).Reason == "Stock or staffing changed", "empty bag pool refuses");
        f.Reg.Registers[0].instance.cargoInstances![0].amount = 2;
        f.Reg.Shelves[0].cargoInstances![0].amount = 3;
        Check(f.Market.Buy(f.Meal("r6"), Minute).Paid && f.Stock(Croissant) == 2 && f.Bags == 1, "restocked shelf and bags sell again");
        f.Reg.Open = false;
        Check(f.Market.Buy(f.Meal("r7"), Minute).Reason == "Shop closed before service", "closed shop refuses before any receipt");
        Check(f.Receipt(f.Meal("r7")) == FoodReceiptStatus.Missing, "closed refusal leaves no receipt");
        f.Reg.Open = true;
        f.Reg.retailPrices[0].price = 3.5f;
        Check(f.Market.Buy(f.Meal("r8"), Minute).Reason == "Approved price changed", "changed price refuses");
        f.Reg.retailPrices[0].price = 3f;
        f.Reg.Shelves[0].cargoInstances!.Add(new Cargo { itemName = Croissant, amount = 1 });
        var unverified = f.Market.Buy(f.Meal("r9"), Minute);
        Check(!unverified.Paid && unverified.Reason.Contains("unverified"), "malformed shelf excludes the menu: " + unverified.Reason);
        f.Reg.Shelves[0].cargoInstances!.RemoveAt(1);
        Check(f.Market.Buy(f.Meal("r10"), Minute).Paid, "repaired shelf sells again");
    }

    static void ReplacedRegistrationIsReadAfresh()
    {
        using var f = new Fixture();
        var old = f.Reg;
        Check(f.Market.Buy(f.Meal("r1"), Minute).Paid, "original registration sells");
        f.Controller.buildingRegistration = Registration(1, 9, 9, 9);
        SaveGameManager.Current.Minute = 1; // ownership is refreshed once per observed minute
        var sale = f.Market.Buy(f.Meal("r2"), Minute);
        Check(sale.Paid && f.Stock(Croissant) == 8 && f.Bags == 8, "the replacement registration's own stock is sold: " + sale.Reason);
        Check(old.Shelves[0].cargoInstances![0].amount == 4 && old.Registers[0].instance.cargoInstances![0].amount == 2, "the replaced registration is untouched");
    }

    static void UncertainNativeOutcomesStillStopTheCheckout()
    {
        foreach (var fault in new[] { "product", "bag", "other", "order", "unpaid" })
        {
            using var f = new Fixture();
            var meal = f.Meal("r1");
            Hooks.Fault = fault;
            bool threw = false;
            try { f.Market.Buy(meal, Minute); }
            catch (InvalidOperationException e) { threw = e.Message.Contains("uncertain"); }
            Check(threw, fault + " fault is an uncertain native outcome");
            Check(f.Receipt(meal) == FoodReceiptStatus.Pending && f.Market.NativePurchases == 0, fault + " fault leaves the pending receipt and no confirmed purchase");
            Hooks.Fault = "";
            bool refused = false;
            try { f.Market.Buy(meal, Minute); } catch (InvalidOperationException e) { refused = e.Message.Contains("no retry"); }
            Check(refused, fault + " fault is never retried");
        }
    }

    // Native IsBusinessOpen, ownership lookup and SetUp read only. If something did change product or bag stock
    // between the reused pre-sale image and the sale, the after-image check refuses to confirm.
    static void ChangesBetweenTheReusedImageAndTheSaleFailClosed()
    {
        foreach (var change in new Action<Fixture>[] {
            f => f.Reg.Shelves[0].cargoInstances![0].amount--,
            f => f.Reg.Shelves[0].cargoInstances![0].amount++,
            f => f.Reg.Registers[0].instance.cargoInstances![0].amount++,
            f => f.Reg.Shelves[1].cargoInstances![0].amount-- })
        {
            using var f = new Fixture();
            var meal = f.Meal("r1");
            Hooks.OpenCheck = () => { Hooks.OpenCheck = null; change(f); };
            bool threw = false;
            try { f.Market.Buy(meal, Minute); }
            catch (InvalidOperationException e) { threw = e.Message.Contains("uncertain"); }
            Check(threw && f.Receipt(meal) == FoodReceiptStatus.Pending && f.Market.NativePurchases == 0,
                "a stock change after the selected read is never confirmed as this sale");
        }
    }

    static void AnotherMinuteReadsAfresh()
    {
        using var f = new Fixture();
        Hooks.OpenCheck = () => { Hooks.OpenCheck = null; SaveGameManager.Current.Minute += .5f; };
        long native = RetailBusinessSimulator.Setups;
        var sale = f.Market.Buy(f.Meal("r1"), Minute);
        Check(sale.Paid && f.Market.CheckoutSupplyReuses == 0, "an image from another game minute is not reused: " + sale.Reason);
        Check(RetailBusinessSimulator.Setups - native == 4, "the fallback reads the pre-sale image afresh");
    }

    static void RefusalsAndRepeatsMakeNoSale()
    {
        using var f = new Fixture();
        var meal = f.Meal("r1");
        Check(f.Market.Buy(meal, Minute).Paid, "first sale");
        long native = RetailBusinessSimulator.Setups;
        var repeat = f.Market.Buy(meal, Minute);
        Check(repeat.Paid && repeat.Reason.Contains("already confirms") && RetailBusinessSimulator.Setups == native, "a paid intention is not sold or read again");
        Check(!f.Market.Buy(f.Meal("r2"), Minute + 60).Paid, "a service time outside the native hour refuses");
        Check(f.Market.Buy(f.Meal("r3", Croissant, 250), Minute).Reason == "Approved price changed", "a different agreed price refuses");
        var slow = f.Meal("r4"); slow.ServiceMinutes = 1;
        Check(f.Market.Buy(slow, Minute).Reason == "Stock or staffing changed", "service faster than native capacity refuses");
        Check(f.Stock(Croissant) == 4 && f.Bags == 2 && f.Market.NativePurchases == 1, "refusals consumed nothing");
        var offers = f.Market.Read(Minute).Single().Offers;
        Check(offers.Single(o => o.ProductId == Croissant).Units == 2 && offers.Single(o => o.ProductId == Burger).Units == 2, "decision reads still share the bag pool");
    }
}

static partial class Program
{
    static double Units(NativeFoodMarket market, string product = Croissant)
        => market.Read(Minute).Single().Offers.Single(o => o.ProductId == product).Units!.Value;

    static void DecisionReadsInOneMinuteShareOneImage()
    {
        using var f = new Fixture();
        long reader = FoodSupplyReader.Setups;
        var first = f.Market.Read(Minute).Single();
        var second = f.Market.Read(Minute).Single();
        var business = f.Market.ReadBusiness(f.BusinessId, Minute)!;
        var local = f.Market.ReadLocal(new Place { Id = "origin", Kind = PlaceKind.Home }, Minute, 100, 8).Single();
        Check(FoodSupplyReader.Setups - reader == 1, "four decision reads in one game minute make one native setup");
        Check(f.Market.DecisionSupplyMisses == 1 && f.Market.DecisionSupplyHits == 3, "one miss then three hits");
        foreach (var b in new[] { second, business, local })
            Check(b.MealsPerHour == first.MealsPerHour && b.ServiceEvidence == first.ServiceEvidence
                && b.Offers.Select(o => o.ProductId + "=" + o.Units).SequenceEqual(first.Offers.Select(o => o.ProductId + "=" + o.Units)),
                "a cached decision read reports the same supply as the live one");
        SaveGameManager.Current.Minute = .75f;
        f.Market.Read(Minute);
        Check(f.Market.DecisionSupplyHits == 4 && FoodSupplyReader.Setups - reader == 1, "a later moment in the same game minute still hits");
        SaveGameManager.Current.Minute = 1;
        f.Market.Read(Minute);
        Check(f.Market.DecisionSupplyMisses == 2 && FoodSupplyReader.Setups - reader == 2, "the next game minute reads afresh");
    }

    // The decision image may be up to one game minute old. Checkout never uses it: Buy reads live,
    // so a purchase cannot proceed on stock the decision image still shows.
    static void DecisionImageNeverReachesCheckout()
    {
        using var f = new Fixture(croissants: 1);
        Check(Units(f.Market) == 1, "decision read sees the last croissant");
        f.Reg.Shelves[0].cargoInstances![0].amount = 0; // a native change the mod did not make
        Check(Units(f.Market) == 1 && f.Market.DecisionSupplyHits == 1, "within the minute the decision image is still the cached one");
        long native = RetailBusinessSimulator.Setups, reader = FoodSupplyReader.Setups;
        var refused = f.Market.Buy(f.Meal("r1"), Minute);
        Check(!refused.Paid && refused.Reason == "Stock or staffing changed", "checkout reads live and refuses the stale stock: " + refused.Reason);
        Check(f.Receipt(f.Meal("r1")) == FoodReceiptStatus.Missing && f.Market.NativePurchases == 0 && f.Bags == 3, "no receipt, sale or bag");
        Check(FoodSupplyReader.Setups - reader == 1 && RetailBusinessSimulator.Setups - native == 1, "the refused checkout made its own live read");
        Check(Units(f.Market) == 0 && f.Market.DecisionSupplyMisses == 2, "a checkout attempt drops the cached image");

        // A successful checkout keeps its live before/sale/after reads and drops the image again.
        f.Reg.Shelves[0].cargoInstances![0].amount = 3;
        Check(f.Market.Buy(f.Meal("r0"), Minute).Paid, "restocked shelf sells although no decision read saw the restock");
        Check(Units(f.Market) == 2, "decision read after a sale shows the sale");
        long hits = f.Market.DecisionSupplyHits, reuses = f.Market.CheckoutSupplyReuses;
        native = RetailBusinessSimulator.Setups; reader = FoodSupplyReader.Setups;
        var sale = f.Market.Buy(f.Meal("r2"), Minute);
        Check(sale.Paid && f.Stock(Croissant) == 1, "checkout after a cached decision read is paid from live stock: " + sale.Reason);
        Check(RetailBusinessSimulator.Setups - native == 3 && FoodSupplyReader.Setups - reader == 2, "checkout setups unchanged by the decision cache");
        Check(f.Market.DecisionSupplyHits == hits && f.Market.CheckoutSupplyReuses == reuses + 1, "checkout reused only its own read");
        Check(Units(f.Market) == 1, "decision read after the second sale shows it");
    }

    static void DecisionImageInvalidation()
    {
        using var f = new Fixture();
        f.Market.Read(Minute);
        long misses = f.Market.DecisionSupplyMisses;
        void Expect(bool miss, string message)
        {
            f.Market.Read(Minute);
            Check(f.Market.DecisionSupplyMisses == misses + (miss ? 1 : 0), message);
            misses = f.Market.DecisionSupplyMisses;
        }
        Expect(false, "unchanged shop hits");
        f.Reg.temporarilyClosed = true;
        Expect(true, "temporary closure is read afresh");
        Check(Units(f.Market) == 0, "closed image reports no units");
        f.Reg.temporarilyClosed = false;
        Expect(true, "reopening is read afresh");
        f.Reg.businessTypeName = "full-renamed";
        Expect(true, "a changed business type is read afresh");
        f.Reg.businessTypeName = "full";
        Expect(true, "the restored business type is read afresh");
        SaveGameManager.Current.modData[FoodAuthorityStore.Key] = new string('a', 3);
        Expect(true, "a food authority payload write is read afresh");
        Expect(false, "the same payload hits");
        SaveGameManager.Current.modData[FoodAuthorityStore.Key] = new string('a', 3);
        Expect(true, "a rewritten payload with equal text is still read afresh");
        f.Market.RefreshKnownOwnership();
        Expect(true, "an ownership refresh drops the place's image");
        f.Controller.buildingRegistration = Registration(1, 9, 9, 9);
        f.Market.RefreshKnownOwnership();
        Check(Units(f.Market) == 9 && f.Market.DecisionSupplyMisses == misses + 1, "a replaced registration is read afresh from its own stock");
    }

    // Eight shops, three game minutes, forty decision reads per minute and one checkout per minute.
    static void DecisionWorkload()
    {
        Hooks.Reset(); SaveGameManager.Current.modData.Clear(); SaveGameManager.Current.Minute = 0;
        var controllers = Enumerable.Range(1, 8).Select(n => new CityBuildingController { buildingRegistration = Registration(n, 50, 50, 50) }).ToArray();
        CityManager.Instance.cityBuildingControllers = controllers;
        var state = new FoodCommerceState();
        using var market = new NativeFoodMarket(state, controllers.Select(c => new Place { Id = NativeFoodMarket.PlaceId(c.buildingRegistration!), Kind = PlaceKind.Shop }).ToArray());
        long reader = FoodSupplyReader.Setups;
        int reads = 0, buys = 0;
        for (int minute = 0; minute < 3; minute++)
        {
            SaveGameManager.Current.Minute = minute;
            for (int decision = 0; decision < 40; decision++) { Check(market.Read(Minute).Count == 8, "all eight shops offered"); reads++; }
            var scope = state.Shops[minute];
            var meal = new ResidentMeal { ResidentId = "w" + minute, Day = SaveGameManager.Current.Day, Kind = MealKind.Lunch, PlaceId = scope.PlaceId,
                BusinessId = scope.BusinessId, ProductId = Croissant, PriceCents = 300, AllowanceCents = 1200, ServiceMinutes = 10, Status = MealStatus.Serving };
            Check(market.Buy(meal, Minute).Paid, "workload checkout is paid"); buys++;
            Check(market.Read(Minute).Single(b => b.PlaceId == scope.PlaceId).Offers.Single(o => o.ProductId == Croissant).Units == 49,
                "the bought shop's next decision read shows the sale"); reads++;
        }
        long setups = FoodSupplyReader.Setups - reader;
        Check(market.DecisionSupplyMisses == 8 * 3 + 3, "eight misses per minute plus one per checkout");
        Check(market.DecisionSupplyHits == reads * 8 - market.DecisionSupplyMisses, "every other shop read hit");
        Check(setups == market.DecisionSupplyMisses + 2 * buys, "reader setups are the misses plus two live checkout reads");
        Console.WriteLine("Decision workload: " + reads + " reads over 8 shops, " + buys + " checkouts; decision cache "
            + market.DecisionSupplyHits + " hits / " + market.DecisionSupplyMisses + " misses; supply-reader setups "
            + setups + " (uncached: " + (reads * 8 + 2 * buys) + ")");
    }

    // Discovery still walks 64 slots on every call. Memoized place IDs mean unchanged addresses build no
    // strings, and the ring still reaches every slot: a shop in the last of 150 slots is found within three calls.
    static void DiscoveryWalksTheWholeRingWithoutRebuildingPlaceIds()
    {
        Hooks.Reset(); SaveGameManager.Current.modData.Clear(); SaveGameManager.Current.Minute = 0;
        var controllers = Enumerable.Range(1, 150).Select(n => new CityBuildingController { buildingRegistration = Registration(n, 5, 5, 5) }).ToArray();
        foreach (var c in controllers.Skip(1)) c.buildingRegistration!.RentedByPlayer = false;
        CityManager.Instance.cityBuildingControllers = controllers;
        var state = new FoodCommerceState();
        string last = NativeFoodMarket.PlaceId(controllers[149].buildingRegistration!);
        string spare = NativeFoodMarket.PlaceId(Registration(151, 0, 0, 0));
        using var market = new NativeFoodMarket(state, new[] { controllers[0], controllers[149] }
            .Select(c => new Place { Id = NativeFoodMarket.PlaceId(c.buildingRegistration!), Kind = PlaceKind.Shop })
            .Append(new Place { Id = spare, Kind = PlaceKind.Shop }).ToArray());
        Check(state.Shops.Count == 1 && market.DiscoveryPlaceIdBuilds == 150, "the opening pass builds each slot's place ID once");
        for (int call = 0; call < 30; call++) market.RefreshDiscovery();
        Check(market.DiscoveryPlaceIdBuilds == 150, "thirty slices over unchanged addresses build no place IDs");
        controllers[149].buildingRegistration!.RentedByPlayer = true;
        int calls = 0;
        while (!state.Shops.Any(s => s.PlaceId == last) && calls < 10) { market.RefreshDiscovery(); calls++; }
        Check(state.Shops.Count == 2 && calls >= 1 && calls <= 3,
            "a newly rented shop in the last slot is found within ceil(150 / 64) = 3 slices: " + calls);
        Check(market.DiscoveryPlaceIdBuilds == 150, "renting in place keeps the memoized place ID");
        // The memoized place ID follows a slot's new registration and address.
        controllers[149].buildingRegistration = Registration(151, 5, 5, 5);
        for (int call = 0; call < 3; call++) market.RefreshDiscovery();
        Check(market.DiscoveryPlaceIdBuilds == 151, "a replaced registration builds its slot's place ID once");
        Check(state.Shops.Any(s => s.PlaceId == spare) && market.ScopeStatus(spare) == "" && market.Read(Minute).Any(b => b.PlaceId == spare),
            "the new registration is known under its own place ID");
        Check(market.Read(Minute).Count(b => b.PlaceId == spare) == 1, "aliased controller is observed exactly once");
        var conflicting = new CityBuildingController { buildingRegistration = Registration(151, 5, 5, 5) };
        bool refused = false;
        try { FoodBusinessCatalogue.ReadSelected(new[] { spare }, new[] { Croissant }, new[] { controllers[149], conflicting }); }
        catch (FoodBusinessCatalogue.CatalogueRefusalException ex) { refused = ex.Message.Contains("Duplicate live shop address"); }
        Check(refused, "distinct controllers at a conflicting address remain refused");
    }
}

static class Hooks
{
    public static string Fault = "";
    public static Action? OpenCheck;
    public static int PriceChecks;
    public static bool LastPriceAccepted;
    public static System.Reflection.MethodInfo? PricePrefix;
    public static void Reset() { Fault = ""; OpenCheck = null; PriceChecks = 0; LastPriceAccepted = false; }
}

public sealed class Save { public List<ProductMarketEntry>? productMarketEntries; public int Day = 3, Hour = 12; public float Minute; public Dictionary<string, string> modData = new(); }
public static class SaveGameManager { public static Save Current = new(); }
public sealed class RetailPrice { public string itemName = ""; public float price; }
public sealed class OpeningHourSlot { public int startingHour, endingHour; }
public sealed class ScheduleDay { public int day; public bool isOpen; public List<OpeningHourSlot> openingHourSlots = new(); }
public sealed class BuildingRegistration
{
    public int creationDay; public string businessOwnerRivalId = ""; public Satisfaction? satisfaction;
    public string StreetName = ""; public int StreetNumber;
    public bool RentedByPlayer = true, AvailableForRent, temporarilyClosed, Open = true;
    public string BusinessName = "Bakery", businessTypeName = "full";
    public bool HasEstablishedBusiness => !string.IsNullOrEmpty(BusinessName);
    public List<string> cachedAvailableProducts = new();
    public List<RetailPrice> retailPrices = new();
    public List<Order> unprocessedCompletedOrders = new();
    public List<ScheduleDay> scheduleDays = new();
    public Dictionary<string, ItemInstance> itemInstances = new();
    public List<ItemInstance> Shelves = new();
    public List<PointOfSale> Registers = new();
}
public sealed class Building { public string Neighbourhood = ""; }
public sealed class Satisfaction { public int customerService = 75; }
public sealed class NeighborhoodDemand { public string neighborhood = ""; public int demand; }
public sealed class ProductMarketEntry { public string itemName = ""; public List<NeighborhoodDemand> demandValues = new(); }
public sealed class CityBuildingController { public Building building = new(); public BuildingRegistration? buildingRegistration; }
public sealed class CityManager { public static CityManager Instance = new(); public CityBuildingController[] cityBuildingControllers = Array.Empty<CityBuildingController>(); }

namespace PersistentCity.Runtime
{
    public static class GamePlaces
    {
        public static bool BuildingAccessible(CityBuildingController controller) => true;
        public static bool IsRetailShopActivity(string type) => true;
    }
    // Ownership has its own regression; this one grants whole-business authority to the current registration.
    internal static class NativeFoodOwnership
    {
        // This harness keeps ownership stable while mutating catalogue/cache inputs.
        private static string TestIdentity(BuildingRegistration reg) => "business:" + Uri.EscapeDataString(reg.StreetName) + ":" + reg.StreetNumber + ":player";
        private static readonly Dictionary<string, FoodAuthority> Rows = new(StringComparer.Ordinal);
        internal static long SuppressedBatches => 0;
        internal static long SuppressedDraws => 0;
        internal static void ValidatePopulation(FoodCommerceState state) => Rows.Clear();
        internal static void Attach(NativeFoodMarket market) { }
        internal static void Detach(NativeFoodMarket market) { }
        internal static void Retire(string place) => Rows.Remove(place);
        internal static FoodAuthority Observe(BuildingRegistration reg, bool eligible)
        {
            string place = NativeFoodMarket.PlaceId(reg);
            if (!Rows.TryGetValue(place, out var row)) Rows[place] = row = new FoodAuthority { PlaceId = place, FromHour = 0 };
            row.Signature = TestIdentity(reg); row.Desired = eligible;
            return row;
        }
        internal static FoodAuthority? Find(BuildingRegistration reg)
            => Rows.TryGetValue(NativeFoodMarket.PlaceId(reg), out var row) && row.Signature == TestIdentity(reg) ? row : null;
    }
}
namespace Entities { public sealed class Unused { } }
namespace Helpers.BusinessSimulation { public sealed class Unused { } }
namespace AI.Citizens
{
    public sealed class CitizenData
    {
        // Stands in for Harmony: the market's recorded prefix decides, as the patched native method would.
        public bool IsPriceAcceptable(string itemName, float price)
        {
            Hooks.PriceChecks++;
            var args = new object[] { itemName, price, false };
            bool runOriginal = Hooks.PricePrefix == null || (bool)Hooks.PricePrefix.Invoke(null, args)!;
            return Hooks.LastPriceAccepted = runOriginal || (bool)args[2];
        }
    }
}
namespace AI.Customers.CustomerEntries
{
    public sealed class OrderEntry { public string itemName = ""; public bool paid; public float price; }
    public sealed class Order { public List<OrderEntry> entries = new(); public bool completed; }
    public sealed class CustomerEntry { public object? spawnTime; public Order order = new(); }
}
namespace HarmonyLib
{
    public static class Priority { public const int First = 800; }
    public sealed class HarmonyMethod
    {
        public readonly System.Reflection.MethodInfo Method; public int priority;
        public HarmonyMethod(Type type, string name) => Method = AccessTools.Method(type, name);
    }
    public static class AccessTools
    {
        // Harmony is not nullable-annotated; neither is this stand-in.
        public static System.Reflection.MethodInfo Method(Type type, string name, Type[]? parameters = null)
            => type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)!;
    }
    public sealed class Harmony
    {
        public Harmony(string id) { }
        public void Patch(System.Reflection.MethodBase original, HarmonyMethod? prefix = null) => Hooks.PricePrefix = prefix?.Method;
        public void UnpatchAll(string id) => Hooks.PricePrefix = null;
    }
}
namespace UnityEngine
{
    public class Object { public static int Destroyed; public static void Destroy(object value) => Destroyed++; }
    public class ScriptableObject : Object { public static int Created; public static T CreateInstance<T>() where T : new() { Created++; return new T(); } }
}
namespace BigAmbitions.Tags { public static class TagRef { public enum Businesstag { customersneedpaperbags } public enum Itemtag { isbag } } }
namespace BigAmbitions.Items
{
    public enum ItemType { ShowcaseShelf = 1 }
    public sealed class Cargo { public string itemName = ""; public int amount; }
    public sealed class ItemInstance { public string id = "id"; public Item ItemCached = new(); public List<Cargo>? cargoInstances = new(); }
    public sealed class Settings { public bool ResourcesRequired = true; }
    public sealed class Item { public ItemType type; public string[] itemsThatCanShowcase = Array.Empty<string>(); public Settings producerSettings = new(); public bool Bag; public bool HasTag(BigAmbitions.Tags.TagRef.Itemtag tag) => Bag; }
    public static class ItemsGetter { public static Item GetByName(string name) => new() { Bag = name == "bag" }; }
}
namespace Helpers
{
    public sealed class BusinessType { public object simulator = new FullServiceBusinessSimulator(); public bool hasEntranceFee; public bool HasTag(BigAmbitions.Tags.TagRef.Businesstag tag) => true; }
    public static class BusinessTypeHelper { public static BusinessType GetData(string name) => new(); }
    public static class BusinessHelper
    {
        public static bool IsBusinessOpen(BuildingRegistration reg) { Hooks.OpenCheck?.Invoke(); return reg.Open && !reg.temporarilyClosed; }
    }
    public static class TimeHelper { public static object Now() => SaveGameManager.Current.Day; }
}
namespace Buildings.Retail.Simulation
{
    public sealed class PointOfSale { public ItemInstance instance = new(); }
    public class RetailBusinessSimulator : UnityEngine.ScriptableObject
    {
        private Dictionary<string, List<ItemInstance>> availableProducers = new();
        private List<PointOfSale> pointOfSales = new();
        private BuildingRegistration? registration;
        public static long Setups;

        // Native SetUp: shelves stocked with a product become its producers; staffed registers become points of sale.
        public void SetUp(BuildingRegistration reg, int hour)
        {
            Setups++; registration = reg;
            availableProducers = new();
            foreach (var shelf in reg.Shelves)
            {
                var stock = shelf.cargoInstances![0];
                if (stock.amount <= 0) continue;
                if (!availableProducers.TryGetValue(stock.itemName, out var list)) availableProducers[stock.itemName] = list = new();
                list.Add(shelf);
            }
            pointOfSales = reg.Registers.ToList();
        }
        private int GetMaxCustomerCapacityThisHour() => 15;

        // Native ProcessCustomer for one product: price check, stock, a paper bag and a completed order.
        protected virtual void ProcessCustomer(CustomerEntry customer)
        {
            var reg = registration!;
            var register = pointOfSales.FirstOrDefault(p => p.instance.cargoInstances![0].amount > 0);
            var entry = customer.order.entries[0];
            if (register != null && availableProducers.TryGetValue(entry.itemName, out var producers) && producers.Count > 0)
            {
                entry.price = reg.retailPrices.Single(p => p.itemName == entry.itemName).price;
                entry.paid = Hooks.Fault != "unpaid" && new AI.Citizens.CitizenData().IsPriceAcceptable(entry.itemName, entry.price);
                if (entry.paid)
                {
                    if (Hooks.Fault != "product") producers[0].cargoInstances![0].amount--;
                    if (Hooks.Fault == "other") reg.Shelves.Single(s => s.cargoInstances![0].itemName != entry.itemName).cargoInstances![0].amount--;
                    register.instance.cargoInstances![0].amount -= Hooks.Fault == "bag" ? 2 : 1;
                    customer.order.entries.Add(new OrderEntry { itemName = "bag", paid = true, price = .1f });
                }
            }
            customer.order.completed = true;
            if (Hooks.Fault != "order") reg.unprocessedCompletedOrders.Add(customer.order);
        }
    }
    public sealed class FullServiceBusinessSimulator : RetailBusinessSimulator { }
}
