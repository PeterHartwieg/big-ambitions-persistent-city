using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F6j same-visit extras: price-relative recognition, cap, wallet on the running total, same-shop stock,
/// no name matching, no meal-reference bargain, a whole-ticket markup, and exactly-once checkout/receipts.</summary>
public sealed class F6jMealExtrasTests
{
    private static readonly Money Main = Money.FromCents(1250);
    private static FoodTicketLine MainLine(string product = "main", long cents = 1250) => new(product, Money.FromCents(cents));
    private static FoodExtraOffer Offer(string product, long cents, int taste = ResidentTraits.Neutral, int? units = 10)
        => new(product, Money.FromCents(cents), units, taste);
    private static FoodMealTicket Plan(string consumer, IEnumerable<FoodExtraOffer> menu, long allowanceCents = 1000000, FoodTicketLine? main = null, int day = 3)
        => FoodMealExtras.Plan(11, consumer, day, MealKind.Lunch, "shop", main ?? MainLine(), menu, Money.FromCents(allowanceCents));
    private static IEnumerable<string> Consumers(int count) => Enumerable.Range(0, count).Select(i => "c" + i);
    private static double Rate(int count, Func<string, bool> included) => Consumers(count).Count(included) / (double)count;

    [Fact]
    public void BandsArePriceRelativeToTheMain()
    {
        Assert.Equal(FoodExtraBand.Cheaper, FoodMealExtras.Band(Main, Money.FromCents(1)));
        Assert.Equal(FoodExtraBand.Cheaper, FoodMealExtras.Band(Main, Money.FromCents(937)));
        Assert.Equal(FoodExtraBand.Similar, FoodMealExtras.Band(Main, Money.FromCents(938)));
        Assert.Equal(FoodExtraBand.Similar, FoodMealExtras.Band(Main, Money.FromCents(1250)));
        Assert.Equal(FoodExtraBand.Similar, FoodMealExtras.Band(Main, Money.FromCents(1562)));
        Assert.Equal(FoodExtraBand.Excluded, FoodMealExtras.Band(Main, Money.FromCents(1563)));
        Assert.Equal(FoodExtraBand.Excluded, FoodMealExtras.Band(Main, Money.Zero));
        Assert.True(FoodMealExtras.Chance(FoodExtraBand.Similar, 2) > 0);
        Assert.Equal(0, FoodMealExtras.Chance(FoodExtraBand.Excluded, 4));
    }

    // (a) Both liked and affordable: the cheaper extra comes first, and wins when only one fits.
    [Fact]
    public void CheaperExtraIsPreferredOverSimilarlyPricedOne()
    {
        var menu = new[] { Offer("similar", 1200, taste: 4), Offer("cheap", 500, taste: 0) };
        int both = 0;
        foreach (var c in Consumers(4000))
        {
            var ample = Plan(c, menu);
            if (ample.Extras.Count < 2) continue;
            both++;
            Assert.Equal(new[] { "cheap", "similar" }, ample.Extras.Select(e => e.ProductId));
            var tight = Plan(c, menu, 1250 + 1200);
            Assert.Equal("cheap", Assert.Single(tight.Extras).ProductId);
        }
        Assert.True(both > 10, "fixture needs consumers who like both: " + both);
    }

    // (b) Measured over 20,000 neutral consumers, each offered exactly one candidate extra.
    [Fact]
    public void SimilarlyPricedSecondDishIsUncommon()
    {
        double cheaper = Rate(20000, c => Plan(c, new[] { Offer("x", 600) }).Extras.Count == 1);
        double similar = Rate(20000, c => Plan(c, new[] { Offer("x", 1200) }).Extras.Count == 1);
        double dearer = Rate(20000, c => Plan(c, new[] { Offer("x", 1600) }).Extras.Count == 1);
        Assert.InRange(cheaper, .33, .37);
        Assert.InRange(similar, .04, .06);
        Assert.True(similar > 0);
        Assert.Equal(0, dearer);
        Assert.InRange(cheaper / similar, 5.5, 9);
    }

    // (c)
    [Fact]
    public void NeverMoreThanThreeExtras()
    {
        var menu = Enumerable.Range(0, 7).Select(i => Offer("s" + i, 100 + i, taste: 4)).ToArray();
        var counts = Consumers(3000).Select(c => Plan(c, menu).Extras.Count).ToArray();
        Assert.Equal(FoodMealExtras.MaximumExtras, counts.Max());
        Assert.True(counts.Count(n => n == 3) > 1000);
        Assert.Throws<ArgumentException>(() => new FoodMealTicket("shop", MainLine(),
            Enumerable.Range(0, 4).Select(i => new FoodTicketLine("s" + i, Money.FromCents(1))).ToArray()));
    }

    // (d)
    [Fact]
    public void RunningTotalStaysWithinAllowanceAndUnaffordableExtrasAreSkipped()
    {
        var menu = new[] { Offer("big", 800, taste: 4), Offer("small", 200, taste: 4), Offer("mid", 450, taste: 4) };
        int skipped = 0;
        foreach (var c in Consumers(3000))
        {
            foreach (long allowance in new long[] { 1250, 1449, 1450, 1700, 1900, 2000, 2700 })
            {
                var ticket = Plan(c, menu, allowance);
                Assert.True(ticket.Total <= Money.FromCents(allowance));
                Assert.Equal(ticket.Main.Price + ticket.Extras.Aggregate(Money.Zero, (s, e) => s + e.Price), ticket.Total);
            }
            var ample = Plan(c, menu).Extras.Select(e => e.ProductId).ToArray();
            var tight = Plan(c, menu, 1250 + 300).Extras.Select(e => e.ProductId).ToArray();
            Assert.DoesNotContain("big", tight); Assert.DoesNotContain("mid", tight);
            if (ample.Contains("big") && ample.Contains("small")) { skipped++; Assert.Contains("small", tight); }
            Assert.Empty(Plan(c, menu, 1250).Extras);
        }
        Assert.True(skipped > 100);
    }

    // (e) Pure policy: only the given menu's stocked items other than the main.
    [Fact]
    public void ExtrasComeOnlyFromTheGivenStockedMenu()
    {
        var menu = new[] { Offer("main", 300, taste: 4), Offer("empty", 100, taste: 4, units: 0), Offer("ok", 100, taste: 4),
            Offer("ok", 101, taste: 4), Offer("rival-unknown", 120, taste: 4, units: null), Offer("free", 0, taste: 4) };
        foreach (var c in Consumers(2000))
        {
            var ticket = Plan(c, menu);
            Assert.All(ticket.Extras, e => Assert.Contains(e.ProductId, new[] { "ok", "rival-unknown" }));
            Assert.Equal(ticket.Extras.Count, ticket.Extras.Select(e => e.ProductId).Distinct().Count());
            Assert.All(ticket.Extras.Where(e => e.ProductId == "ok"), e => Assert.Equal(Money.FromCents(100), e.Price));
        }
    }

    // (f) Identical prices, different names: same band, same chance, same measured rate.
    [Fact]
    public void IdenticallyPricedItemsAreTreatedTheSameRegardlessOfName()
    {
        string[] names = { "ba:itemname_soda", "ba:itemname_salad", "modded:anything", "zz" };
        foreach (var name in names) Assert.Equal(FoodExtraBand.Cheaper, FoodMealExtras.Band(Main, Money.FromCents(400)));
        var rates = names.Select(n => Rate(20000, c => Plan(c, new[] { Offer(n, 400) }).Extras.Count == 1)).ToArray();
        Assert.True(rates.Max() - rates.Min() < .02, string.Join(",", rates));
        // Order between two liked same-priced items follows the draw, not the name.
        int firstA = 0, both = 0;
        foreach (var c in Consumers(6000))
        {
            var t = Plan(c, new[] { Offer("aaa", 400, 4), Offer("zzz", 400, 4) }, 1250 + 400);
            var ample = Plan(c, new[] { Offer("aaa", 400, 4), Offer("zzz", 400, 4) });
            if (ample.Extras.Count != 2) continue;
            both++; if (t.Extras.Single().ProductId == "aaa") firstA++;
        }
        Assert.InRange(firstA / (double)both, .4, .6);
    }

    // (g) A very cheap side gets the same chance as any cheaper extra: no $12.50 meal-reference bargain.
    [Fact]
    public void ExtrasReceiveNoMealReferenceBargain()
    {
        for (int taste = 0; taste <= 4; taste++)
            Assert.Equal(FoodMealExtras.Chance(FoodExtraBand.Cheaper, taste),
                FoodMealExtras.Chance(FoodMealExtras.Band(Main, Money.FromCents(50)), taste));
        double penny = Rate(20000, c => Plan(c, new[] { Offer("x", 50) }).Extras.Count == 1);
        double nine = Rate(20000, c => Plan(c, new[] { Offer("x", 900) }).Extras.Count == 1);
        Assert.True(Math.Abs(penny - nine) < .015, penny + " vs " + nine);
        Assert.True(FoodMealExtras.Chance(FoodExtraBand.Cheaper, 4) <= FoodMealExtras.MaximumChance);
        // The price anchor itself (F6i) is not consulted: a $0.50 side is chosen at the same rate under a $5 main.
        double underCheapMain = Rate(20000, c => Plan(c, new[] { Offer("x", 50) }, main: MainLine("main", 500)).Extras.Count == 1);
        Assert.True(Math.Abs(penny - underCheapMain) < .015);
    }

    // (i) A whole-menu +50% markup marks up the whole ticket: same extras, total exactly x1.5.
    [Fact]
    public void WholeMenuMarkupMarksUpTheWholeTicket()
    {
        var baseMenu = new[] { Offer("a", 400), Offer("b", 600), Offer("c", 1100), Offer("d", 200) };
        var marked = baseMenu.Select(o => new FoodExtraOffer(o.ProductId, Money.FromCents(o.Price.Units / Money.UnitsPerCent * 3 / 2), o.Units, o.TasteBand)).ToArray();
        Money before = Money.Zero, after = Money.Zero; int withExtras = 0;
        foreach (var c in Consumers(3000))
        {
            var t0 = Plan(c, baseMenu);
            var t1 = Plan(c, marked, main: MainLine("main", 1875));
            Assert.Equal(t0.Extras.Select(e => e.ProductId), t1.Extras.Select(e => e.ProductId));
            Assert.Equal(t0.Total * 3, t1.Total * 2);
            if (t0.Extras.Count > 0) withExtras++;
            before += t0.Total; after += t1.Total;
        }
        Assert.True(withExtras > 1000);
        Assert.True(before > Main * 3000, "extras add to the ticket");
        Assert.Equal(before * 3, after * 2);
    }

    [Fact]
    public void PlanIsDeterministicAndKeyedByMealNotContext()
    {
        var menu = new[] { Offer("a", 400), Offer("b", 600), Offer("c", 900) };
        foreach (var c in Consumers(500))
            Assert.Equal(Plan(c, menu).Extras.Select(e => e.ProductId), Plan(c, menu.Reverse()).Extras.Select(e => e.ProductId));
        int differ = Consumers(500).Count(c => !Plan(c, menu).Extras.Select(e => e.ProductId).SequenceEqual(Plan(c, menu, day: 4).Extras.Select(e => e.ProductId)));
        Assert.True(differ > 50);
    }

    // ---- Integrated checkout (allowance mode). ----

    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences preferences) => new()
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 5, Mode = TravelMode.Walk } } };
    }
    private sealed class TicketMarket : IFoodMarket, IFoodTicketMarket, IFoodSaleEvidence
    {
        public bool Extras = true;
        public int ThrowFromDay = int.MaxValue;
        public int SellAtMost = 3;
        public readonly Dictionary<string, Dictionary<string, (Money Price, int Units)>> Menus = new()
        {
            ["b"] = new() { [FoodProducts.Croissant] = (Money.FromCents(1250), 50), [FoodProducts.Burger] = (Money.FromCents(900), 50),
                [FoodProducts.HotDog] = (Money.FromCents(600), 50) },
            ["b2"] = new() { [FoodProducts.Burger] = (Money.FromCents(850), 50) },
        };
        public readonly List<(string Intent, string Business, string Product, Money Price)> Lines = new();
        public readonly HashSet<string> Intents = new();
        public double EarliestMinute => 0;
        public bool IsConfirmedPaid(ResidentMeal meal) => false;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Menus.Select(m => new FoodBusiness {
            Id = m.Key, PlaceId = m.Key == "b" ? "shop" : "shop2", Name = m.Key, MealsPerHour = 30,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = m.Value.Select(o => new FoodOffer { ProductId = o.Key, Price = o.Value.Price, Units = o.Value.Units }).ToList() }).ToArray();
        public bool SellsExtras(string businessId) => Extras;
        public FoodSale Buy(ResidentMeal meal, double minute) => Sell(meal, null);
        public FoodSale BuyTicket(ResidentMeal meal, FoodMealTicket ticket, double minute)
        {
            Assert.True(Extras); Assert.Equal(meal.Price, ticket.Total); Assert.Equal(meal.ProductId, ticket.Main.ProductId);
            return Sell(meal, ticket);
        }
        private FoodSale Sell(ResidentMeal meal, FoodMealTicket? ticket)
        {
            Assert.True(Intents.Add(meal.IntentId), "one checkout per intent");
            var menu = Menus[meal.BusinessId];
            var main = menu[meal.ProductId];
            if (main.Units <= 0 || main.Price != (ticket?.Main.Price ?? meal.Price)) return new(false, "changed");
            menu[meal.ProductId] = (main.Price, main.Units - 1); Lines.Add((meal.IntentId, meal.BusinessId, meal.ProductId, main.Price));
            var sold = new List<FoodTicketLine>();
            foreach (var extra in ticket?.Extras ?? Array.Empty<FoodTicketLine>())
            {
                if (sold.Count == SellAtMost || !menu.TryGetValue(extra.ProductId, out var item) || item.Price != extra.Price || item.Units <= 0) break;
                menu[extra.ProductId] = (item.Price, item.Units - 1); sold.Add(extra);
                Lines.Add((meal.IntentId, meal.BusinessId, extra.ProductId, extra.Price));
            }
            if (meal.Day >= ThrowFromDay) throw new Exception("Lost native acknowledgement");
            return new(true, "fake ticket", null, sold);
        }
    }
    private static (CitySimulator City, TicketMarket Market) Ready(int count = 60, TicketMarket? market = null)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
                new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new(2, 0, 0) },
                new Place { Id = "shop2", Name = "Stand", Kind = PlaceKind.Shop, Position = new(-2, 0, 0) } }, "food-extras", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        { c.Phase = CitizenPhase.AtPlace; c.PlaceId = "home"; c.Activity = Activity.Home; c.NextEventMinute = c.ActivityUntilMinute = 2870; }
        state.Food = new FoodCommerceState { Shops = new() {
            new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = FoodProducts.Croissant, AdditionalProductIds = new() { FoodProducts.Burger, FoodProducts.HotDog } },
            new FoodScope { PlaceId = "shop2", BusinessId = "b2", ProductId = FoodProducts.Burger } } };
        DevelopmentChain.RepresentedSchema(state, 1);
        market ??= new TicketMarket();
        var city = new CitySimulator(state, new Walking()); city.AttachFoodMarket(market);
        return (city, market);
    }
    private static void Advance(CitySimulator city, double at) { while (!city.AdvanceTo(at, 128).CaughtUp) { } }
    // Day 1 runs the legacy choice; day 2 is the first F6i policy day. Each run ends after day 2's meals.
    private static double Until(CitySimulator city) => 3 * 1440 - 1;

    // (e)+(d) integrated: extras come only from the chosen shop's priced stock and the whole ticket is booked once.
    [Fact]
    public void CheckoutSellsSameShopExtrasAndBooksTheWholeTicket()
    {
        var (city, market) = Ready();
        Advance(city, Until(city));
        var meals = city.State.Food!.Residents;
        Assert.NotEmpty(market.Lines);
        var byIntent = market.Lines.GroupBy(l => l.Intent).ToArray();
        Assert.Contains(byIntent, g => g.Count() > 1);
        foreach (var g in byIntent)
        {
            Assert.Single(g.Select(l => l.Business).Distinct());
            Assert.All(g, l => Assert.True(market.Menus[l.Business].ContainsKey(l.Product)));
            Assert.Equal(g.Count(), g.Select(l => l.Product).Distinct().Count());
            Assert.InRange(g.Count(), 1, 1 + FoodMealExtras.MaximumExtras);
            // A $9 burger main never takes the $12.50 croissant (above 125%) as an extra; b2 sells nothing else.
            if (g.First().Product == FoodProducts.Burger) Assert.DoesNotContain(g, l => l.Product == FoodProducts.Croissant);
            if (g.First().Business == "b2" || g.First().Product == FoodProducts.HotDog) Assert.Single(g);
        }
        var spent = Money.Sum(meals.Select(m => m.Spent));
        Assert.Equal(market.Lines.Aggregate(Money.Zero, (s, l) => s + l.Price), spent);
        Assert.All(meals, m => Assert.True(m.DayAllowanceUsed <= m.Allowance));
        var rows = city.State.Food.History.Where(h => h.BusinessId == "b").ToArray();
        // Each sold line is one item purchase in the shop-day row.
        Assert.Equal(market.Lines.Count(l => l.Business == "b"), rows.Sum(r => r.Purchases));
        foreach (var product in new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog })
            Assert.Equal(market.Lines.Count(l => l.Business == "b" && l.Product == product),
                rows.SelectMany(r => r.ProductSales).Where(p => p.ProductId == product).Sum(p => p.Purchases));
        Assert.Equal(rows.Aggregate(Money.Zero, (s, r) => s + r.Revenue),
            rows.SelectMany(r => r.ProductSales).Aggregate(Money.Zero, (s, p) => s + p.Revenue));
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
    }

    [Fact]
    public void SellerWithoutExtrasSellsTheMainOnly()
    {
        var (city, market) = Ready(market: new TicketMarket { Extras = false });
        Advance(city, Until(city));
        Assert.NotEmpty(market.Lines);
        Assert.All(market.Lines.GroupBy(l => l.Intent), g => Assert.Single(g));
        Assert.Equal(market.Lines.Aggregate(Money.Zero, (s, l) => s + l.Price), Money.Sum(city.State.Food!.Residents.Select(m => m.Spent)));
    }

    [Fact]
    public void UnsoldExtrasAreReturnedAndOnlySoldLinesAreBooked()
    {
        var (full, fullMarket) = Ready();
        var (partial, partialMarket) = Ready(market: new TicketMarket { SellAtMost = 0 });
        Advance(full, Until(full)); Advance(partial, Until(partial));
        Assert.True(fullMarket.Lines.Count > partialMarket.Lines.Count);
        Assert.All(partialMarket.Lines.GroupBy(l => l.Intent), g => Assert.Single(g));
        Assert.Equal(partialMarket.Lines.Aggregate(Money.Zero, (s, l) => s + l.Price), Money.Sum(partial.State.Food!.Residents.Select(m => m.Spent)));
    }

    [Fact]
    public void UncertainTicketHoldsItsPlannedTotalAndIsNeverReplayed()
    {
        var market = new TicketMarket { ThrowFromDay = 2 };
        var (city, _) = Ready(market: market);
        Advance(city, Until(city));
        var uncertain = city.State.Food!.Residents.Where(m => m.Status == MealStatus.Uncertain).ToArray();
        Assert.NotEmpty(uncertain);
        foreach (var m in uncertain)
        {
            var lines = market.Lines.Where(l => l.Intent == m.IntentId).ToArray();
            Assert.Equal(lines.Aggregate(Money.Zero, (s, l) => s + l.Price), m.Price);
            Assert.Equal(m.Price, m.DayAllowanceUsed);
            // Only the earlier, confirmed day's lines are spent; the uncertain ticket stays held.
            Assert.Equal(market.Lines.Where(l => l.Intent.StartsWith("food:" + m.ResidentId + ":") && l.Intent != m.IntentId)
                .Aggregate(Money.Zero, (s, l) => s + l.Price), m.Spent);
        }
        Assert.Contains(uncertain, m => market.Lines.Count(l => l.Intent == m.IntentId) > 1);
        int orders = market.Intents.Count;
        Advance(city, Until(city) + 120);
        Assert.Equal(orders, market.Intents.Count);
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
    }

    // (9) Nothing about extras is saved: a reload mid-journey plans the same ticket from the same live terms.
    [Fact]
    public void ReloadBeforeCheckoutPlansTheSameTicket()
    {
        var (straight, straightMarket) = Ready(24);
        var (reloaded, reloadMarket) = Ready(24);
        int active = 0;
        // Reload every 37 minutes of the first policy day, including held decisions and committed journeys.
        for (double t = 2 * 1440; t < Until(straight); t += 37)
        {
            Advance(straight, t); Advance(reloaded, t);
            active += reloaded.State.Food!.Residents.Count(m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving || m.Decision != null);
            var read = DevelopmentChain.Read(DevelopmentChain.Write(reloaded.State)); Assert.Equal("", read.Error);
            reloaded = new CitySimulator(read.State!, new Walking()); reloaded.AttachFoodMarket(reloadMarket);
        }
        Assert.True(active > 0);
        Advance(straight, Until(straight)); Advance(reloaded, Until(reloaded));
        Assert.Equal(straightMarket.Lines, reloadMarket.Lines);
        Assert.Equal(SaveCodec.Write(straight.State), SaveCodec.Write(reloaded.State));
    }

    [Fact]
    public void ObservedDecisionReportsCandidateTicketsWithoutChangingChoice()
    {
        var (observed, observedMarket) = Ready(24);
        var (plain, plainMarket) = Ready(24);
        var tickets = new List<FoodMealTicket>();
        observed.FoodDecisionObserved += t => tickets.AddRange(t.Tickets);
        Advance(observed, Until(observed)); Advance(plain, Until(plain));
        Assert.Equal(plainMarket.Lines, observedMarket.Lines);
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.All(t.Extras, e => Assert.True(observedMarket.Menus[t.BusinessId].ContainsKey(e.ProductId))));
    }

    // ---- Native receipt identity. ----

    private static ResidentMeal ReceiptMeal(long totalCents) => new() { ResidentId = "r1", Day = 4, Kind = MealKind.Lunch,
        BusinessId = "b|1", ProductId = FoodProducts.Croissant, Price = Money.FromCents(totalCents) };

    [Fact]
    public void ReceiptCarriesTheTicketAndReconcilesItsTotal()
    {
        var extras = new[] { new FoodTicketLine(FoodProducts.Burger, Money.FromCents(300)), new FoodTicketLine(FoodProducts.HotDog, Money.FromCents(250)) };
        var data = new Dictionary<string, string>();
        var meal = ReceiptMeal(700 + 300 + 250);
        FoodSaleReceipts.Begin(data, meal, extras);
        Assert.Equal(FoodReceiptStatus.Pending, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(data, meal, extras));
        FoodSaleReceipts.Confirm(data, meal, Money.FromCents(700), extras, extras);
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.True(FoodSaleReceipts.AlreadyPaid(data, meal, extras));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(data, meal, extras.Take(1).ToArray()));
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, ReceiptMeal(700)));
        // A partly sold ticket confirms the lines actually sold, with their total.
        var partial = new Dictionary<string, string>();
        FoodSaleReceipts.Begin(partial, meal, extras);
        FoodSaleReceipts.Confirm(partial, meal, Money.FromCents(700), extras, extras.Take(1).ToArray());
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(partial, ReceiptMeal(1000)));
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(partial, meal));
        // A one-product receipt keeps its exact previous text.
        var single = new Dictionary<string, string>(); var one = ReceiptMeal(700);
        FoodSaleReceipts.Begin(single, one); FoodSaleReceipts.Confirm(single, one);
        Assert.Equal("paid|700|b%7C1|ba%3Aitemname_croissant", single.Values.Single());
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(new Dictionary<string, string>(), meal,
            Enumerable.Range(0, 4).Select(i => new FoodTicketLine("x" + i, Money.FromCents(1))).ToArray()));
    }

    [Fact]
    public void PruneExpiresConfirmedTicketsAndKeepsMalformedOnes()
    {
        var extras = new[] { new FoodTicketLine(FoodProducts.Burger, Money.FromCents(300)) };
        var data = new Dictionary<string, string>();
        var meal = ReceiptMeal(1000);
        FoodSaleReceipts.Begin(data, meal, extras); FoodSaleReceipts.Confirm(data, meal, Money.FromCents(700), extras, extras);
        string key = data.Keys.Single();
        data[FoodSaleReceipts.Prefix + "food:r2:lunch:4"] = data[key] + "|0:x";
        data[FoodSaleReceipts.Prefix + "food:r3:lunch:4"] = data[key] + "|1:a|1:b|1:c";
        FoodSaleReceipts.Prune(data, 10);
        Assert.False(data.ContainsKey(key));
        Assert.Equal(2, data.Count);
    }
}
