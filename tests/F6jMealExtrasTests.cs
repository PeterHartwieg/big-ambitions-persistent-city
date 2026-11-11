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
    private sealed class TicketMarket : IFoodMarket, IFoodTicketMarket, IFoodSaleEvidence, IFoodTicketEvidence
    {
        public bool Extras = true;
        public HashSet<string>? ExtrasAt;
        // Native outcome lost before the receipt is confirmed (stays pending), or after it is confirmed.
        public int ThrowFromDay = int.MaxValue, LoseAcknowledgementDay = -1;
        public int SellAtMost = 3, MealsPerHour = 30;
        public readonly Dictionary<string, Dictionary<string, (Money Price, int Units)>> Menus = new()
        {
            ["b"] = new() { [FoodProducts.Croissant] = (Money.FromCents(1250), 50), [FoodProducts.Burger] = (Money.FromCents(900), 50),
                [FoodProducts.HotDog] = (Money.FromCents(600), 50) },
            ["b2"] = new() { [FoodProducts.Burger] = (Money.FromCents(850), 50) },
        };
        public readonly List<(string Intent, string Business, string Product, Money Price)> Lines = new();
        public readonly HashSet<string> Intents = new();
        // The real native receipt format, kept like native save data across a population reload.
        public readonly Dictionary<string, string> Receipts = new();
        public double EarliestMinute => 0;
        public bool IsConfirmedPaid(ResidentMeal meal) => FoodSaleReceipts.ReadStatus(Receipts, meal) == FoodReceiptStatus.Paid;
        public bool TryConfirmedTicket(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
            => FoodSaleReceipts.TryReadPaidTicket(Receipts, meal, out total, out extras);
        public IReadOnlyList<FoodBusiness> Read(double minute) => Menus.Select(m => new FoodBusiness {
            Id = m.Key, PlaceId = m.Key == "b" ? "shop" : "shop2", Name = m.Key, MealsPerHour = MealsPerHour,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = m.Value.Select(o => new FoodOffer { ProductId = o.Key, Price = o.Value.Price, Units = o.Value.Units }).ToList() }).ToArray();
        public bool SellsExtras(string businessId) => Extras && (ExtrasAt == null || ExtrasAt.Contains(businessId));
        public FoodSale Buy(ResidentMeal meal, double minute) => Sell(meal, null);
        public FoodSale BuyTicket(ResidentMeal meal, FoodMealTicket ticket, double minute)
        {
            Assert.True(SellsExtras(meal.BusinessId)); Assert.Equal(meal.Price, ticket.Total); Assert.Equal(meal.ProductId, ticket.Main.ProductId);
            return Sell(meal, ticket);
        }
        private FoodSale Sell(ResidentMeal meal, FoodMealTicket? ticket)
        {
            Assert.True(Intents.Add(meal.IntentId), "one checkout per intent");
            var menu = Menus[meal.BusinessId];
            var main = menu[meal.ProductId];
            if (main.Units <= 0 || main.Price != (ticket?.Main.Price ?? meal.Price)) return new(false, "changed");
            FoodSaleReceipts.Begin(Receipts, meal, ticket?.Extras);
            menu[meal.ProductId] = (main.Price, main.Units - 1); Lines.Add((meal.IntentId, meal.BusinessId, meal.ProductId, main.Price));
            var sold = new List<FoodTicketLine>();
            foreach (var extra in ticket?.Extras ?? Array.Empty<FoodTicketLine>())
            {
                if (sold.Count == SellAtMost || !menu.TryGetValue(extra.ProductId, out var item) || item.Price != extra.Price || item.Units <= 0) break;
                menu[extra.ProductId] = (item.Price, item.Units - 1); sold.Add(extra);
                Lines.Add((meal.IntentId, meal.BusinessId, extra.ProductId, extra.Price));
            }
            if (meal.Day >= ThrowFromDay) throw new Exception("Lost native acknowledgement");
            if (ticket == null) FoodSaleReceipts.Confirm(Receipts, meal);
            else FoodSaleReceipts.Confirm(Receipts, meal, ticket.Main.Price, ticket.Extras, sold);
            if (meal.Day == LoseAcknowledgementDay) throw new Exception("Receipt confirmed; acknowledgement lost");
            return new(true, "fake ticket", null, sold);
        }
    }
    private static (CitySimulator City, TicketMarket Market) Ready(int count = 60, TicketMarket? market = null, bool finances = false, long? allowanceCents = null)
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
        market ??= new TicketMarket();
        var city = new CitySimulator(state, new Walking()); city.AttachFoodMarket(market);
        if (allowanceCents.HasValue) foreach (var meal in city.State.Food!.Residents) meal.Allowance = Money.FromCents(allowanceCents.Value);
        if (finances)
        {
            city.EnableHouseholdFinances();
            foreach (var meal in city.State.Food!.Residents) meal.Budget!.HomeFoodFundedDay = -1;
        }
        return (city, market);
    }
    private static CitySimulator Reload(CitySimulator city, TicketMarket market)
    {
        var read = DevelopmentChain.Read(DevelopmentChain.Write(city.State)); Assert.Equal("", read.Error);
        var next = new CitySimulator(read.State!, new Walking()); next.AttachFoodMarket(market); return next;
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

    // Sol-r1-1: the shop expense compared in the choice is the whole ticket. The F6i main factor is unchanged.
    [Fact]
    public void TicketExpenseLowersAShopWeightButNotTheMainAnchor()
    {
        var traits = ResidentTraits.Generate(3, "neutral-traits");
        var settings = new FoodChoiceSettings(); settings.Clamp();
        EvaluatedFoodOffer O(long main, long? ticket) => new("b", "p", Money.FromCents(main), 5, 0, 20, 60, null, null, MealKind.Lunch,
            ticket: ticket.HasValue ? Money.FromCents(ticket.Value) : null);
        double alone = FoodChoicePolicy.Weight(O(1250, null), traits, Money.FromCents(100000), settings);
        Assert.Equal(alone, FoodChoicePolicy.Weight(O(1250, 1250), traits, Money.FromCents(100000), settings));
        double withSide = FoodChoicePolicy.Weight(O(1250, 1850), traits, Money.FromCents(100000), settings);
        Assert.True(withSide < alone);
        int band = traits.Band(ResidentTrait.PriceSensitivity);
        double expected = Math.Pow(1250.0 / 1850, 2 * settings.FoodChoicePriceWeight * (.5 + .25 * band));
        Assert.Equal(expected, withSide / alone, 9);
        // A whole-menu +50% keeps the ticket/main ratio: only the F6i main factor moves.
        double marked = FoodChoicePolicy.Weight(O(1875, 2775), traits, Money.FromCents(100000), settings);
        double markedAlone = FoodChoicePolicy.Weight(O(1875, null), traits, Money.FromCents(100000), settings);
        Assert.Equal(expected, marked / markedAlone, 9);
        // Price weight 0 disables the soft ticket signal as it does the main's.
        var off = new FoodChoiceSettings { FoodChoicePriceWeight = 0 }; off.Clamp();
        Assert.Equal(FoodChoicePolicy.Weight(O(1250, null), traits, Money.FromCents(100000), off),
            FoodChoicePolicy.Weight(O(1250, 1850), traits, Money.FromCents(100000), off));
    }

    // Sol-r1-1 integrated: two equal shops with the same $12.50 croissant and $6 hot dog; only one sells extras,
    // so only there does the croissant's planned ticket include a side. Fewer residents choose that croissant.
    [Fact]
    public void CandidateTicketsEnterTheShopChoice()
    {
        TicketMarket Market(bool extrasAtB) { var m = new TicketMarket { ExtrasAt = new() { extrasAtB ? "b" : "none" } };
            m.Menus["b"] = new() { [FoodProducts.Croissant] = (Money.FromCents(1250), 500), [FoodProducts.HotDog] = (Money.FromCents(600), 500) };
            m.Menus["b2"] = new() { [FoodProducts.Croissant] = (Money.FromCents(1250), 500), [FoodProducts.HotDog] = (Money.FromCents(600), 500) }; return m; }
        (CitySimulator, TicketMarket) Build(bool extrasAtB)
        {
            var (city, market) = Ready(400, Market(extrasAtB));
            city.State.Food!.Shops[0].AdditionalProductIds = new() { FoodProducts.HotDog };
            city.State.Food.Shops[1].ProductId = FoodProducts.Croissant; city.State.Food.Shops[1].AdditionalProductIds = new() { FoodProducts.HotDog };
            return (city, market);
        }
        var (withExtras, a) = Build(true); var (without, b) = Build(false);
        var observed = new List<FoodMealTicket>();
        withExtras.FoodDecisionObserved += t => observed.AddRange(t.Tickets);
        Advance(withExtras, Until(withExtras)); Advance(without, Until(without));
        int Mains(TicketMarket m, string business) => m.Lines.Where(l => l.Intent.EndsWith(":2")).GroupBy(l => l.Intent)
            .Count(g => g.First().Business == business && g.First().Product == FoodProducts.Croissant);
        Assert.Contains(observed, t => t.BusinessId == "b" && t.Main.ProductId == FoodProducts.Croissant && t.Extras.Count > 0);
        Assert.DoesNotContain(observed, t => t.BusinessId == "b2");
        Assert.True(Mains(a, "b") < Mains(b, "b"), Mains(a, "b") + " vs " + Mains(b, "b"));
        Assert.True(Mains(a, "b2") >= Mains(b, "b2"));
    }

    private static void LowerLimitBelow(ResidentMeal meal, Money price)
    {
        var budget = meal.Budget!;
        var target = HouseholdFinance.SpendingReserve(budget) + price - Money.FromCents(1);
        var delta = budget.Balance - target;
        budget.Balance -= delta; budget.OpeningBalance -= delta;
        Assert.True(HouseholdFinance.CashConserved(budget));
        Assert.True(HouseholdFinance.SpendingLimit(budget) < price);
    }

    // Sol-r1-2: a checkout refused because the wallet limit fell keeps a savable commitment.
    [Fact]
    public void RefusedCheckoutAfterTheLimitFallsStillSaves()
    {
        var (city, market) = Ready(40, finances: true);
        ResidentMeal? target = null;
        for (double t = 2 * 1440; t < Until(city) && target == null; t += 1)
        {
            Advance(city, t);
            target = city.State.Food!.Residents.FirstOrDefault(m => m.Status == MealStatus.Serving || m.Status == MealStatus.Travelling);
        }
        Assert.NotNull(target);
        LowerLimitBelow(target!, target!.Price);
        Advance(city, Until(city));
        Assert.Equal(MealStatus.Missed, target.Status);
        Assert.DoesNotContain(market.Intents, i => i == target.IntentId);
        Assert.True(target.Price <= target.Allowance);
        Reload(city, market);
    }

    [Fact]
    public void RefusedRivalSettlementAfterTheLimitFallsStillSaves()
    {
        var (city, market, paths) = RivalOutflowTests.Ready(finances: true);
        market.Rivals["rival0"].AdditionalProductIds = new() { FoodProducts.HotDog };
        market.Observations["rival0"] = new FoodAlternativeObservation("rival0", "s0", "street", 1, 1, "food", "rival-owner",
            FoodAlternativeKind.Rival, true, true, false, null, 0,
            new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(100)), new FoodAdvertisedOffer(FoodProducts.HotDog, Money.FromCents(40)) },
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, 0, 1440)));
        city.InspectFoodAwareness("r");
        var meal = city.State.Food!.Residents.Single();
        for (int i = 0; i < 4000 && meal.Status != MealStatus.Serving; i++) city.AdvanceTo(city.State.Minute + 1, 1);
        Assert.Equal(MealStatus.Serving, meal.Status);
        LowerLimitBelow(meal, meal.Price);
        for (int i = 0; i < 4000 && meal.Status == MealStatus.Serving; i++) city.AdvanceTo(city.State.Minute + 1, 1);
        Assert.Equal(MealStatus.Missed, meal.Status);
        Assert.True(meal.Price <= meal.Allowance);
        var read = DevelopmentChain.Read(DevelopmentChain.Write(city.State)); Assert.Equal("", read.Error);
    }

    // Sol-r2-1: the saved Allowance was the limit at commitment (here exactly the main). The wallet grows before
    // checkout, so a larger ticket is held; the saved terms must still cover it. Paid and uncertain checkouts.
    private static void RaiseLimitAfterCommitment(ResidentMeal meal)
    {
        var budget = meal.Budget!;
        var delta = Money.FromCents(10000);
        budget.Balance += delta; budget.OpeningBalance += delta;
        meal.Allowance = meal.Price;
        Assert.True(HouseholdFinance.CashConserved(budget));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargerTicketAfterTheLimitRisesStillSaves(bool uncertain)
    {
        var (city, market) = Ready(60, new TicketMarket { ThrowFromDay = uncertain ? 2 : int.MaxValue }, finances: true);
        var raised = new Dictionary<string, Money>();
        for (double t = 2 * 1440; t < Until(city); t += 1)
        {
            Advance(city, t);
            foreach (var m in city.State.Food!.Residents)
                if ((m.Status == MealStatus.Serving || m.Status == MealStatus.Travelling) && !raised.ContainsKey(m.ResidentId))
                { RaiseLimitAfterCommitment(m); raised[m.ResidentId] = m.Price; }
        }
        Advance(city, Until(city));
        var grown = city.State.Food!.Residents.Where(m => raised.TryGetValue(m.ResidentId, out var main) && m.Price > main
            && m.Status == (uncertain ? MealStatus.Uncertain : MealStatus.Paid)).ToArray();
        Assert.NotEmpty(grown);
        Assert.All(city.State.Food.Residents, m => Assert.True(m.Price <= m.Allowance));
        Reload(city, market);
    }

    [Fact]
    public void LargerRivalTicketAfterTheLimitRisesStillSaves()
    {
        // One resident, one draw per meal: follow several days. Every settlement must save, and some must be a
        // ticket larger than its committed main.
        var (city, market, _) = RivalOutflowTests.Ready(finances: true);
        market.Rivals["rival0"].AdditionalProductIds = new() { FoodProducts.HotDog, FoodProducts.Burger };
        market.Observations["rival0"] = new FoodAlternativeObservation("rival0", "s0", "street", 1, 1, "food", "rival-owner",
            FoodAlternativeKind.Rival, true, true, false, null, 0,
            new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(100)), new FoodAdvertisedOffer(FoodProducts.HotDog, Money.FromCents(70)),
                new FoodAdvertisedOffer(FoodProducts.Burger, Money.FromCents(60)) },
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, 0, 1440)));
        city.InspectFoodAwareness("r");
        var meal = city.State.Food!.Residents.Single();
        int grown = 0, settled = 0;
        for (int i = 0; i < 20 * 1440 && settled < 12; i++)
        {
            city.AdvanceTo(city.State.Minute + 1, 1);
            if (meal.Status != MealStatus.Serving) continue;
            var main = meal.Price;
            RaiseLimitAfterCommitment(meal);
            for (int j = 0; j < 4000 && meal.Status == MealStatus.Serving; j++) city.AdvanceTo(city.State.Minute + 1, 1);
            Assert.Equal(MealStatus.Paid, meal.Status);
            settled++; if (meal.Price > main) grown++;
            Assert.True(meal.Price <= meal.Allowance);
            var read = DevelopmentChain.Read(DevelopmentChain.Write(city.State)); Assert.Equal("", read.Error);
        }
        Assert.True(settled > 3, "settled " + settled);
        Assert.True(grown > 0, "some rival ticket carries an extra");
    }

    // Sol-r1-3: the receipt is confirmed but the acknowledgement is lost. After a reload the meal settles from the
    // receipt's sold lines: unsold extras are returned and each sold line is recorded once, with no second order.
    [Theory]
    [InlineData(3, false)] [InlineData(0, false)] [InlineData(3, true)] [InlineData(0, true)]
    public void ConfirmedReceiptSettlesTheSoldTicketAfterReload(int sellAtMost, bool finances)
    {
        var market = new TicketMarket { LoseAcknowledgementDay = 2, SellAtMost = sellAtMost };
        var (city, _) = Ready(120, market, finances, finances ? null : 5000);
        Advance(city, Until(city));
        var uncertainMeals = city.State.Food!.Residents.Where(m => m.Status == MealStatus.Uncertain).ToArray();
        var uncertain = uncertainMeals.Select(m => m.ResidentId).ToHashSet();
        // Some held tickets planned extras: fully sold (3) or only the main sold, extras unsold (0).
        Assert.Contains(uncertainMeals, m => m.Price > market.Lines.First(l => l.Intent == m.IntentId).Price);
        city = Reload(city, market);
        // TicketMarket asserts one checkout per intention, so a replayed order would fail here.
        Advance(city, Until(city) + 1440);
        var day2 = market.Lines.Where(l => l.Intent.EndsWith(":2")).GroupBy(l => l.Intent).ToDictionary(g => g.Key, g => g.ToArray());
        Assert.Contains(day2.Values, g => sellAtMost == 0 ? g.Length == 1 : g.Length > 1);
        if (sellAtMost == 0) Assert.All(day2.Values, g => Assert.Single(g));
        foreach (var m in city.State.Food!.Residents.Where(m => uncertain.Contains(m.ResidentId)))
        {
            Assert.NotEqual(MealStatus.Uncertain, m.Status);
            Assert.Equal(market.Lines.Where(l => l.Intent.StartsWith("food:" + m.ResidentId + ":")).Aggregate(Money.Zero, (s, l) => s + l.Price), m.Spent);
            if (m.Budget != null) { Assert.Equal(Money.Zero, m.Budget.HeldPurchase); Assert.True(HouseholdFinance.CashConserved(m.Budget)); }
        }
        var rows = city.State.Food.History.Where(h => h.BusinessId == "b").ToArray();
        Assert.Equal(market.Lines.Count(l => l.Business == "b"), rows.Sum(r => r.Purchases));
        foreach (var product in new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog })
            Assert.Equal(market.Lines.Where(l => l.Business == "b" && l.Product == product).Aggregate(Money.Zero, (s, l) => s + l.Price),
                rows.SelectMany(r => r.ProductSales).Where(p => p.ProductId == product).Aggregate(Money.Zero, (s, p) => s + p.Revenue));
        Reload(city, market);
    }

    [Fact]
    public void PaidTicketEvidenceIsReadOnlyForItsOwnIntentionAndHeldTotal()
    {
        var extras = new[] { new FoodTicketLine(FoodProducts.Burger, Money.FromCents(300)), new FoodTicketLine(FoodProducts.HotDog, Money.FromCents(250)) };
        var data = new Dictionary<string, string>(); var meal = ReceiptMeal(1250);
        Assert.False(FoodSaleReceipts.TryReadPaidTicket(data, meal, out _, out _));
        FoodSaleReceipts.Begin(data, meal, extras);
        Assert.False(FoodSaleReceipts.TryReadPaidTicket(data, meal, out _, out _));
        FoodSaleReceipts.Confirm(data, meal, Money.FromCents(700), extras, extras.Take(1).ToArray());
        Assert.True(FoodSaleReceipts.TryReadPaidTicket(data, meal, out var total, out var sold));
        Assert.Equal(Money.FromCents(1000), total);
        Assert.Equal(FoodProducts.Burger, Assert.Single(sold).ProductId);
        Assert.False(FoodSaleReceipts.TryReadPaidTicket(data, ReceiptMeal(999), out _, out _)); // more than held
        var other = ReceiptMeal(1250); other.ProductId = FoodProducts.HotDog;
        Assert.False(FoodSaleReceipts.TryReadPaidTicket(data, other, out _, out _));
        var single = new Dictionary<string, string>(); var one = ReceiptMeal(700);
        FoodSaleReceipts.Begin(single, one); FoodSaleReceipts.Confirm(single, one);
        Assert.True(FoodSaleReceipts.TryReadPaidTicket(single, one, out total, out sold));
        Assert.Equal(Money.FromCents(700), total); Assert.Empty(sold);
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
