using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>GR-3 (R3, R4): needs with catalogue categories and a neutral default, impulse while passing, and one purchase
/// contract for every family with food on the same checkout.</summary>
public sealed class NeedPurchaseTests
{
    private const string Resident = "resident-0001", Venue = "address:Main%20Street:12:shop", Gift = "ba:businesstype_giftshop", Gym = "ba:businesstype_gym";
    private static readonly PurchaseLine Fee = new PurchaseLine("ba:itemname_entrancefee", Money.FromCents(500), true);
    private static readonly PurchaseLine Shirt = new PurchaseLine("ba:itemname_shirt", Money.FromCents(300), false);
    private static readonly PurchaseLine Socks = new PurchaseLine("ba:itemname_socks", Money.FromCents(200), false);

    private static ResidentBudget Wallet(long cents) => new ResidentBudget { OpeningBalance = Money.FromCents(cents), Balance = Money.FromCents(cents) };
    private static PurchaseRequest Request(long ceilingCents, long minute = 600)
        => new PurchaseRequest(Resident, Venue, PurchaseIntention.Need(Gift), Money.FromCents(ceilingCents), minute);
    private static string WalletText(ResidentBudget w) => string.Join("|", w.Balance, w.PreparedFoodSpent, w.Ledger.Discretionary, w.HeldPurchase);

    /// <summary>Native double: stock and booked orders, changed only when a sale goes through.</summary>
    internal sealed class Native : IPurchaseNative
    {
        public PurchaseFamily Family { get; set; } = PurchaseFamily.Gym;
        public IReadOnlyList<PurchaseLine> Basket { get; set; } = new[] { Fee, Shirt, Socks };
        public int ServedThisHour { get; set; }
        public int CapacityThisHour { get; set; } = 10;
        public int? NativeHourlyCount { get; set; }
        public readonly List<string> Orders = new List<string>();
        public int Calls, Undos, Commits;
        public Func<PurchaseNativeOutcome?>? Before;
        public bool ThrowAfterBooking, ThrowUndo;
        public Func<IReadOnlyList<PurchaseLine>, IReadOnlyList<PurchaseLine>>? Paid;
        public PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines)
        {
            Calls++;
            var early = Before?.Invoke();
            if (early.HasValue) return early.Value;
            Orders.Add(string.Join("+", lines.Select(l => l.ItemId)));
            if (ThrowAfterBooking) throw new InvalidOperationException("processor threw after booking");
            return PurchaseNativeOutcome.Processed(Paid?.Invoke(lines) ?? lines.ToArray());
        }
        public void Undo()
        {
            Undos++;
            if (ThrowUndo) throw new InvalidOperationException("restore refused");
            if (Orders.Count > 0) Orders.RemoveAt(Orders.Count - 1);
        }
        public void Commit() => Commits++;
    }

    [Fact]
    public void ACeilingBelowTheMandatoryLinesIsRefusedBeforeAnyNativeCallOrDebit()
    {
        var wallet = Wallet(10000); var before = WalletText(wallet);
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        var native = new Native();
        var result = PurchaseContract.Run(Request(499), wallet, receipts, native);
        Assert.Equal(PurchaseRefusal.Ceiling, result.Refusal);
        Assert.Equal(0, native.Calls); Assert.Empty(receipts); Assert.Equal(before, WalletText(wallet));

        // The wallet's own limit lowers the requested ceiling the same way.
        var poor = Wallet(300);
        Assert.Equal(PurchaseRefusal.Ceiling, PurchaseContract.Run(Request(100000), poor, receipts, native).Refusal);
        Assert.Equal(0, native.Calls); Assert.Empty(receipts);

        // A full venue refuses before the basket is priced; the native count never refuses.
        var full = new Native { ServedThisHour = 10, NativeHourlyCount = 0 };
        Assert.Equal(PurchaseRefusal.VenueFull, PurchaseContract.Run(Request(100000), wallet, receipts, full).Refusal);
        Assert.Equal(0, full.Calls); Assert.Empty(receipts); Assert.Equal(before, WalletText(wallet));

        // An unsupported processor stays native whatever the ceiling.
        var unsupported = new Native { Family = PurchaseFamily.Unsupported };
        Assert.Equal(PurchaseRefusal.Unsupported, PurchaseContract.Run(Request(100000), wallet, receipts, unsupported).Refusal);
        Assert.Equal(0, unsupported.Calls);
    }

    [Fact]
    public void OptionalLinesAreTrimmedFromTheEndAndTheDebitIsWhatTheNativeSideBooked()
    {
        var wallet = Wallet(10000);
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        var native = new Native();
        var result = PurchaseContract.Run(Request(850), wallet, receipts, native);
        Assert.True(result.IsBought);
        Assert.Equal(new[] { Fee.ItemId, Shirt.ItemId }, result.Lines.Select(l => l.ItemId));
        Assert.Equal(1, result.Quote!.Dropped);
        Assert.Equal(Money.FromCents(800), result.Total);
        Assert.Equal(Money.FromCents(9200), wallet.Balance);
        Assert.Equal(Money.FromCents(800), wallet.Ledger.Discretionary);
        Assert.StartsWith("paid|" + Money.FromCents(800).ToCentsText() + "|", receipts[result.Key]);
        // The same resident, venue and minute never sells again.
        Assert.Equal(PurchaseRefusal.AlreadyRecorded, PurchaseContract.Run(Request(850), wallet, receipts, native).Refusal);
        Assert.Equal(1, native.Calls);

        // A booked order outside the quote is reversed: books restored, no debit, no receipt.
        var rogue = new Native();
        rogue.Before = () => { rogue.Orders.Add("x"); return PurchaseNativeOutcome.Processed(new[] { new PurchaseLine("ba:itemname_hat", Money.FromCents(100), false) }); };
        var reversed = PurchaseContract.Run(Request(850, 601), wallet, receipts, rogue);
        Assert.Equal(PurchaseRefusal.Reversed, reversed.Refusal);
        Assert.Empty(rogue.Orders); Assert.Equal(Money.FromCents(9200), wallet.Balance);
        Assert.False(receipts.ContainsKey(reversed.Key));
    }

    [Fact]
    public void FoodOnTheSameCheckoutRefusesATicketOverItsAllowanceWholeAsBefore()
    {
        var extras = new[] { new FoodTicketLine("ba:itemname_soda", Money.FromCents(150)), new FoodTicketLine("ba:itemname_fries", Money.FromCents(250)) };
        var basket = PurchaseContract.FoodBasket("ba:itemname_burger", Money.FromCents(900), extras);
        Assert.All(basket, l => Assert.True(l.Mandatory));
        // The pre-GR-3 rule: the ticket sells whole when its total fits the allowance, and is refused whole when it does not.
        foreach (long allowance in new long[] { 800, 900, 1050, 1299, 1300, 2000 })
        {
            var quote = PurchaseContract.Quote(basket, Money.FromCents(allowance));
            bool refusedBefore = Money.FromCents(1300) > Money.FromCents(allowance);
            Assert.Equal(refusedBefore, quote.Refusal != PurchaseRefusal.None);
            Assert.Equal(refusedBefore ? 0 : 2, PurchaseContract.KeptExtras(quote, extras).Count);
        }
        Assert.Empty(PurchaseContract.KeptExtras(PurchaseContract.Quote(PurchaseContract.FoodBasket("m", Money.FromCents(100), null), Money.FromCents(100)), null));
    }

    [Fact]
    public void ADeferredNeedComesDueTheNextDayUpToThreeTimesThenWaitsAFullInterval()
    {
        var profile = NeedModel.For(Gift)!;
        Assert.True(profile.IsNeutral);
        Assert.Equal((7, 3), (profile.IntervalDays, profile.SpreadDays));
        Assert.Null(NeedModel.For(""));
        Assert.Same(profile, NeedModel.For(Gift));
        // A modded type is a category too, with the same neutral default.
        Assert.True(NeedModel.For("mymod:businesstype_toys")!.IsNeutral);

        var need = new NeedState(Gift, 10, "", 0);
        for (int i = 1; i <= NeedModel.MaximumDeferrals; i++)
        {
            Assert.True(NeedModel.Due(need, 9 + i));
            NeedModel.Defer(need, profile, 9 + i);
            Assert.Equal((10 + i, i), (need.NextDueDay, need.Deferrals));
            Assert.Equal(i, NeedModel.Urgency(need, 10 + i));
        }
        NeedModel.Defer(need, profile, 13);
        Assert.Equal((13 + profile.IntervalDays, 0), (need.NextDueDay, need.Deferrals));

        var person = new CitizenState { Identity = new CitizenIdentity { Id = Resident } };
        NeedModel.Satisfy(person, need, profile, Venue, 20, 7);
        Assert.InRange(need.NextDueDay, 20 + 7 - 3, 20 + 7 + 3);
        Assert.Equal((Venue, 0), (need.LastVenue, need.Deferrals));
    }

    [Fact]
    public void ImpulseIsSeededAtMostOncePerDayAndRisesAsTheNeedComesDue()
    {
        var profile = NeedModel.For(Gift)!;
        Assert.Equal(NeedModel.ImpulseBaseBasisPoints, NeedModel.ImpulseRate(null, profile, 5));
        Assert.Equal(NeedModel.ImpulseBaseBasisPoints, NeedModel.ImpulseRate(new NeedState(Gift, 20, "", 0), profile, 5));
        Assert.Equal(NeedModel.ImpulseBaseBasisPoints + NeedModel.ImpulseNeedBasisPoints, NeedModel.ImpulseRate(new NeedState(Gift, 5, "", 0), profile, 5));
        int mid = NeedModel.ImpulseRate(new NeedState(Gift, 8, "", 0), profile, 5);
        Assert.InRange(mid, NeedModel.ImpulseBaseBasisPoints + 1, NeedModel.ImpulseBaseBasisPoints + NeedModel.ImpulseNeedBasisPoints - 1);

        // Over many people the draw follows the rate, is the same on a second run, and gives each person one impulse a day.
        int hits = 0;
        for (int n = 0; n < 4000; n++)
        {
            var a = new CitizenState { Identity = new CitizenIdentity { Id = "p" + n } };
            var b = new CitizenState { Identity = new CitizenIdentity { Id = "p" + n } };
            bool first = NeedModel.Impulse(a, profile, Venue, 3, 11);
            Assert.Equal(first, NeedModel.Impulse(b, profile, Venue, 3, 11));
            if (first) { hits++; Assert.False(NeedModel.Impulse(a, profile, "other", 3, 11)); Assert.Equal(Venue, a.Needs!.ImpulseVenue); }
        }
        Assert.InRange(hits, 40, 130); // 2 % of 4000 = 80
    }

    [Fact]
    public void AnImpulseItemFitsOnlyBeforeTheNextFixedItemAndNeverBesideAnotherNeed()
    {
        var work = new PlanItem(PlanItemKind.Work, Activity.Work, "work", 600, 600, 1080, PlanPriority.Fixed);
        var plan = new DayPlan(0, 1, ShiftType.Day, 0, "work", new[] { work }, null);
        Assert.NotNull(NeedModel.ImpulseItem(plan, Venue, 400, 30));
        Assert.Null(NeedModel.ImpulseItem(plan, Venue, 560, 30)); // ends inside the work travel allowance
        var shop = new PlanItem(PlanItemKind.Need, Activity.Shop, "shop", 1100, 1100, 1145, PlanPriority.Optional);
        var withNeed = new DayPlan(0, 1, ShiftType.Day, 0, "work", new[] { work, shop }, null);
        Assert.Null(NeedModel.ImpulseItem(withNeed, Venue, 400, 30));
        withNeed.Restore(1, PlanItemState.Done, PlanMissCause.None);
        var item = NeedModel.ImpulseItem(withNeed, Venue, 1150, 30)!;
        Assert.Equal((PlanItemKind.Need, PlanPriority.Optional, 1150.0, 1180.0), (item.Kind, item.Priority, item.EarliestStart, item.EndsAt));
    }

    /// <summary>The market double: serves every shop of the banded city as a gift shop, and logs each request.</summary>
    internal sealed class Market : IPurchaseMarket
    {
        public readonly Dictionary<string, string> Store = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> Venues = new List<string>();
        public readonly List<(string Resident, long Minute, PurchaseRefusal Refusal, string Venue)> Log = new List<(string, long, PurchaseRefusal, string)>();
        public Func<PurchaseRequest, PurchaseNativeOutcome?> Decide = _ => null;
        public int? Count;
        public bool BookOutsideQuoteAndFailUndo;
        public int Opens;
        public bool ServesNothing;
        public bool Serves(string venueId) => !ServesNothing && Venues.Contains(venueId);
        IReadOnlyList<string> IPurchaseMarket.Venues => Venues;
        public IDictionary<string, string> Receipts => Store;
        public void Guarded(Action unit) => unit();
        public IPurchaseNative? Open(PurchaseRequest request, out PurchaseRefusal refusal, out string reason)
        {
            refusal = PurchaseRefusal.None; reason = ""; Opens++;
            var native = new Native { Basket = new[] { new PurchaseLine("ba:itemname_card", Money.FromCents(400), false) }, NativeHourlyCount = Count };
            native.Before = () => { var o = Decide(request); Log.Add((request.ResidentId, request.Minute, o?.Refusal ?? PurchaseRefusal.None, request.VenueId)); return o; };
            if (BookOutsideQuoteAndFailUndo)
            {
                native.ThrowUndo = true;
                native.Paid = _ => new[] { new PurchaseLine("ba:itemname_hat", Money.FromCents(100), false) };
            }
            return native;
        }
    }

    internal static (CitySimulator City, Market Market) Banded(int seed = 5150)
    {
        var (state, food) = WorkplaceCapacityTests.Fresh(3, seed);
        var market = new Market();
        foreach (var shop in state.Places.Where(p => p.Kind == PlaceKind.Shop)) { shop.BusinessType = Gift; market.Venues.Add(shop.Id); }
        // A gym, cinema or nightclub is a leisure place; its scheduled visits buy through the same contract.
        foreach (var leisure in state.Places.Where(p => p.Kind == PlaceKind.Leisure)) { leisure.BusinessType = Gym; market.Venues.Add(leisure.Id); }
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(food); city.AttachPurchases(market);
        return (city, market);
    }

    private static void Run(CitySimulator city, double to) { while (!city.AdvanceTo(to, 128).CaughtUp) { } }

    [Fact]
    public void ARefusedPurchaseDefersTheNeedAndItIsBoughtWhenItComesDueAgain()
    {
        var (city, market) = Banded();
        // Every purchase on the first planned day is refused by the venue; later days sell.
        market.Decide = r => r.Minute / 1440 <= 1 ? PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueNotReady, "no staff") : (PurchaseNativeOutcome?)null;
        // Every resident starts with the need due, so the first shop visit buys or refuses.
        foreach (var person in city.State.Citizens.Where(c => c.Wallet != null))
        {
            person.Needs = new PersonNeeds();
            person.Needs.Add(new NeedState(Gift, 0, "", 0));
        }
        Run(city, 1440 * 6);
        var refused = market.Log.Where(l => l.Refusal == PurchaseRefusal.VenueNotReady).Select(l => l.Resident).Distinct().ToList();
        Assert.NotEmpty(refused);
        Assert.True(city.PurchasesRefused >= refused.Count);
        int boughtLater = 0;
        foreach (var id in refused)
        {
            var first = market.Log.First(l => l.Resident == id).Minute;
            var later = market.Log.Where(l => l.Resident == id && l.Minute > first && l.Refusal == PurchaseRefusal.None).ToList();
            var need = city.State.Citizens.Single(c => c.Identity.Id == id).Needs!.Find(Gift)!;
            if (later.Count == 0) { Assert.True(need.Deferrals > 0 || need.NextDueDay > first / 1440); continue; }
            boughtLater++;
            Assert.True(later[0].Minute / 1440 > first / 1440, "a deferred need waits for the next day");
        }
        Assert.True(boughtLater > 0, "some deferred need was bought once it came due again");
        Assert.True(city.PurchasesBought > 0);
        // Each confirmed purchase moved exactly its paid total out of a wallet, into discretionary spending.
        Assert.Equal(city.PurchasesBought, market.Store.Values.Count(v => v.StartsWith("paid|" + Money.FromCents(400).ToCentsText() + "|", StringComparison.Ordinal)));
    }

    [Fact]
    public void NativeCountsCalibrateOnlyAndNeverChangeAPurchase()
    {
        string Outcome(int? count)
        {
            var (city, market) = Banded();
            market.Count = count;
            Run(city, 1440 * 4);
            Assert.All(city.Calibration, row => Assert.Equal(count, row.NativeCount));
            return SaveCodec.Write(city.State) + "|" + city.PurchasesBought + "|" + string.Join(",", market.Store.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value));
        }
        string none = Outcome(null);
        Assert.Equal(none, Outcome(0));
        Assert.Equal(none, Outcome(5000));
    }

    [Fact]
    public void ReloadKeepsNeedsAndTheCityContinuesTheSame()
    {
        var (reference, market) = Banded();
        Run(reference, 1440 * 3 + 500);
        Assert.Contains(reference.State.Citizens, c => c.Needs != null && c.Needs.Needs.Count > 0);
        string payload = SaveCodec.Write(reference.State);
        Assert.Contains("\"Needs\":\"1|", payload);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var byId = read.State!.Citizens.ToDictionary(c => c.Identity.Id);
        foreach (var person in reference.State.Citizens)
            Assert.True(person.Needs == null ? byId[person.Identity.Id].Needs == null : person.Needs.SameAs(byId[person.Identity.Id].Needs));
        Assert.Equal(payload, SaveCodec.Write(read.State));

        var resumed = new CitySimulator(read.State, new FreshCityTraceTests.Walk());
        resumed.AttachFoodMarket(WorkplaceCapacityTests.Fresh(3, 5150).Market);
        var copy = new Market(); copy.Venues.AddRange(market.Venues); foreach (var p in market.Store) copy.Store[p.Key] = p.Value;
        resumed.AttachPurchases(copy);
        Run(reference, 1440 * 5); Run(resumed, 1440 * 5);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(resumed.State));
        Assert.Equal(market.Store.OrderBy(p => p.Key), copy.Store.OrderBy(p => p.Key));
    }

    [Fact]
    public void ScheduledLeisureVisitsBuyThroughTheContract()
    {
        var (city, market) = Banded();
        Run(city, 1440 * 4);
        Assert.Contains(market.Log, l => l.Venue.StartsWith("leisure:", StringComparison.Ordinal) && l.Refusal == PurchaseRefusal.None);
        Assert.Contains(city.State.Citizens, c => c.Needs?.Find(Gym)?.LastVenue.StartsWith("leisure:", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AFailedRestoreKeepsTheUncertainReceiptStopsPurchasesAndNeverRetries()
    {
        var (city, market) = Banded();
        market.BookOutsideQuoteAndFailUndo = true;
        foreach (var person in city.State.Citizens.Where(c => c.Wallet != null))
        { person.Needs = new PersonNeeds(); person.Needs.Add(new NeedState(Gift, 0, "", 0)); person.Needs.Add(new NeedState(Gym, 0, "", 0)); }
        var wallets = city.State.Citizens.Where(c => c.Wallet != null).ToDictionary(c => c.Identity.Id, c => c.Wallet!.Ledger.Discretionary);
        Run(city, 1440 * 4);
        Assert.Equal(1, market.Opens);
        Assert.NotNull(city.PurchaseFault);
        Assert.Null(city.Purchases);
        var receipt = Assert.Single(market.Store);
        Assert.StartsWith("uncertain|", receipt.Value);
        Assert.Contains(receipt.Key, city.PurchaseFault);
        // Nobody was debited; the fault carries into a replacement simulator, which stays detached.
        Assert.All(city.State.Citizens.Where(c => c.Wallet != null), c => Assert.Equal(wallets[c.Identity.Id], c.Wallet!.Ledger.Discretionary));
        var rebuilt = city.RebuildRoutes(new FreshCityTraceTests.Walk());
        Assert.Equal(city.PurchaseFault, rebuilt.PurchaseFault); Assert.Null(rebuilt.Purchases);
        // The uncertain receipt is never pruned.
        Assert.Equal(0, PurchaseContract.Prune(market.Store, 1000));
        Assert.Single(market.Store);
    }

    [Fact]
    public void TheMarketSurvivesARouteRebuildAndKeepsBuying()
    {
        var (city, market) = Banded();
        Run(city, 1440 * 2);
        int before = market.Log.Count;
        var rebuilt = city.RebuildRoutes(new FreshCityTraceTests.Walk());
        Assert.Same(market, rebuilt.Purchases);
        Run(rebuilt, 1440 * 4);
        Assert.True(market.Log.Count > before);
        Assert.True(rebuilt.PurchasesBought > 0);
    }

    [Fact]
    public void AnEssentialCategoryTheMarketSellsIsNotAlsoDeductedAtTheDailyPosting()
    {
        var (city, market) = Banded();
        Assert.Equal(0, city.PurchaseEssentials()!.Count); // gift shops and gyms sell no essential category
        foreach (var shop in city.State.Places.Where(p => p.Kind == PlaceKind.Shop)) shop.BusinessType = "ba:businesstype_clothingstore";
        Run(city, city.State.Minute + 60); // the policy is read once per game hour
        var needs = city.PurchaseEssentials()!;
        Assert.Equal(new[] { HouseholdEssentials.Clothing }, needs.Categories);
        Assert.Equal(needs.Categories, city.RebuildRoutes(new FreshCityTraceTests.Walk()).PurchaseEssentials()!.Categories);
        var plain = new CitySimulator(city.State, new FreshCityTraceTests.Walk());
        Assert.Null(plain.PurchaseEssentials());

        // The daily posting reads it: two cities that differ only in their venues' type, with no purchase made in either,
        // differ in the essentials posted by exactly the clothing share of each posting.
        (Money Paid, int Postings) Essentials(string type)
        {
            var (run, m) = Banded();
            m.ServesNothing = true;
            foreach (var shop in run.State.Places.Where(p => p.Kind == PlaceKind.Shop)) shop.BusinessType = type;
            Run(run, 1440 * 4);
            var wallets = run.State.Citizens.Where(c => c.Wallet != null).Select(c => c.Wallet!).ToList();
            return (Money.Sum(wallets.Select(w => w.EssentialsPaid)), wallets.Count);
        }
        var gift = Essentials(Gift); var clothing = Essentials("ba:businesstype_clothingstore");
        Assert.True(clothing.Paid < gift.Paid, "clothing is no longer deducted while its spender sells it");
        Assert.Equal(gift.Postings, clothing.Postings);
    }

    private const string Other = "ba:businesstype_bookstore";

    /// <summary>The banded city at day 1, its places, and the last shop retyped as the only served bookstore.</summary>
    private static (CitySimulator City, List<Place> Catalogue, Dictionary<string, Place> Index, Place Bookstore) PlanningCity()
    {
        var (city, _) = Banded();
        Run(city, 1440 + 600);
        var catalogue = city.State.Places.Where(p => p.Kind == PlaceKind.Shop || p.Kind == PlaceKind.Leisure).ToList();
        var index = city.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var bookstore = catalogue.Last(p => p.Kind == PlaceKind.Shop);
        bookstore.BusinessType = Other;
        return (city, catalogue, index, bookstore);
    }

    [Fact]
    public void ADueNeedSendsTheNextPlanToAVenueSellingItsCategory()
    {
        var (city, catalogue, index, bookstore) = PlanningCity();
        var planning = new NeedPlanning(new[] { bookstore });
        // A person whose drawn day has a shop window, tried over a few days.
        foreach (var person in city.State.Citizens.Where(c => !c.IsVisitor))
            for (int day = 2; day < 6; day++)
            {
                person.Needs = null;
                var drawn = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false);
                int at = drawn.Items.ToList().FindIndex(i => i.Kind == PlanItemKind.Need);
                if (at < 0) continue;
                // Not due: the drawn venue stays.
                person.Needs = new PersonNeeds(); person.Needs.Add(new NeedState(Other, day + 1, "", 0));
                Assert.Equal(drawn.Items[at].VenueId, DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false, planning).Items[at].VenueId);
                // Deferred and due again: the window goes to the venue that sells the category.
                NeedModel.Defer(person.Needs.Find(Other)!, NeedModel.For(Other)!, day - 1);
                Assert.Equal(day, person.Needs.Find(Other)!.NextDueDay);
                var retry = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false, planning);
                Assert.Equal(bookstore.Id, retry.Items[at].VenueId);
                Assert.Equal(PlanItemKind.Need, retry.Items[at].Kind);
                return;
            }
        Assert.Fail("no person with a shop window");
    }

    [Fact]
    public void ADeferredNeedGetsItsOwnVisitOnADayTheRoutineDrewNoShopWindow()
    {
        var (city, catalogue, index, bookstore) = PlanningCity();
        var planning = new NeedPlanning(new[] { bookstore });
        foreach (var person in city.State.Citizens.Where(c => !c.IsVisitor && c.Wallet != null))
            for (int day = 2; day < 9; day++)
            {
                person.Needs = null;
                var drawn = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false);
                if (drawn.Items.Any(i => i.Kind == PlanItemKind.Need)) continue;
                // Planning first gives the person a need for every served category.
                DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false, planning);
                Assert.NotNull(person.Needs!.Find(Other));
                // Deferred yesterday: due today, and today's plan gets a visit for it although the routine drew none.
                var need = person.Needs.Find(Other)!;
                need.NextDueDay = day - 1; NeedModel.Defer(need, NeedModel.For(Other)!, day - 1);
                var plan = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false, planning);
                var visit = plan.Items.Single(i => i.Kind == PlanItemKind.Need);
                Assert.Equal(bookstore.Id, visit.VenueId);
                Assert.InRange(visit.EarliestStart, day * 1440.0 + 600, day * 1440.0 + 1200);
                foreach (var other in plan.Items.Where(i => i != visit && i.Kind != PlanItemKind.Meal))
                    Assert.True(visit.EndsAt <= other.EarliestStart - 30 || visit.EarliestStart >= other.EndsAt + 30, "the visit keeps clear of " + other.Kind);
                // The rest of the day is the drawn plan.
                Assert.Equal(drawn.Items.Count + 1, plan.Items.Count);
                return;
            }
        Assert.Fail("no person with a day without a shop window");
    }

    /// <summary>The scheduled visit is executed, not only planned: a resident whose day has no routine shop window and
    /// whose plan is drawn during a breakfast outing still goes and buys (the outing's home continuation is cut back).</summary>
    [Fact]
    public void AScheduledNeedVisitIsExecutedOnADayWithoutARoutineShopWindow()
    {
        var (city, market) = Banded();
        var person = city.State.Citizens.Single(c => c.Identity.Id == "pc:11");
        Assert.NotNull(person.Wallet);
        person.Needs = new PersonNeeds(); person.Needs.Add(new NeedState(Gift, 2, "", 0));
        // The routine alone draws no shop window for this resident on day 2.
        var catalogue = city.State.Places.Where(p => p.Kind == PlaceKind.Shop || p.Kind == PlaceKind.Leisure).ToList();
        var index = city.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var copy = CitySimulator.CloneForEvent(person); copy.Plan = null;
        Assert.DoesNotContain(DayPlanner.Generate(copy, city.State.Seed, 2, catalogue, index, false).Items, i => i.Kind == PlanItemKind.Need);
        Run(city, 3 * 1440 + 240);
        var bought = market.Log.Where(l => l.Resident == "pc:11" && l.Minute / 1440 == 2).ToList();
        Assert.Contains(bought, l => l.Refusal == PurchaseRefusal.None);
        var need = person.Needs.Find(Gift)!;
        Assert.Equal(0, need.Deferrals);
        Assert.True(need.NextDueDay > 2);
    }

    [Fact]
    public void ADueNeedWhoseVenueIsClosedInTheDrawnWindowGetsAVisitWhenItIsOpen()
    {
        var (city, catalogue, index, bookstore) = PlanningCity();
        // Open 09:00 to 11:00 only: an afternoon routine window cannot serve the need, the late morning can.
        bookstore.WeeklyHours.Clear(); bookstore.OpenMinute = 540; bookstore.CloseMinute = 660;
        var planning = new NeedPlanning(new[] { bookstore });
        foreach (var person in city.State.Citizens.Where(c => !c.IsVisitor && c.Wallet != null))
            for (int day = 2; day < 9; day++)
            {
                person.Needs = null;
                var drawn = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false);
                var window = drawn.Items.FirstOrDefault(i => i.Kind == PlanItemKind.Need);
                if (window == null || window.EarliestStart < day * 1440.0 + 660) continue;
                person.Needs = new PersonNeeds(); person.Needs.Add(new NeedState(Other, day, "", 1));
                var plan = DayPlanner.Generate(person, city.State.Seed, day, catalogue, index, false, planning);
                var visit = plan.Items.SingleOrDefault(i => i.Kind == PlanItemKind.Need);
                if (visit == null || visit.VenueId != bookstore.Id) continue; // that morning is taken; try another person
                Assert.InRange(visit.EarliestStart, day * 1440.0 + 600, day * 1440.0 + 615);
                Assert.True(visit.EndsAt <= day * 1440.0 + 660);
                // The drawn afternoon window is replaced, not kept beside it (one shop visit a day).
                Assert.DoesNotContain(plan.Items, i => i.Kind == PlanItemKind.Need && i.EarliestStart == window.EarliestStart);
                return;
            }
        Assert.Fail("no person with a free morning and an afternoon shop window");
    }

    [Fact]
    public void PlanningSkipsUnservedAndClosedVenuesBeforeMemoryAndDistance()
    {
        var (city, _, index, _) = PlanningCity();
        var shops = city.State.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        Assert.True(shops.Count >= 3);
        foreach (var shop in shops) shop.BusinessType = Other;
        var person = city.State.Citizens.First(c => !c.IsVisitor);
        var home = index[person.HomeId];
        var byDistance = shops.OrderBy(p => Position.Distance(home.Position, p.Position)).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
        person.Needs = new PersonNeeds(); person.Needs.Add(new NeedState(Other, 2, "", 0));
        double from = 2 * 1440 + 700, until = from + 45;
        // The nearest bookstore is a rival's: planning goes to the nearest one the market serves.
        var served = new NeedPlanning(byDistance.Skip(1));
        Assert.Equal(byDistance[1].Id, NeedModel.PlanVenue(person, 2, served, home, PlaceKind.Shop, from, until));
        Assert.Equal(byDistance[0].Id, NeedModel.PlanVenue(person, 2, new NeedPlanning(shops), home, PlaceKind.Shop, from, until));
        // The remembered venue wins while it is open for the visit; closed, the next open one does.
        person.Needs.Find(Other)!.LastVenue = byDistance[2].Id;
        Assert.Equal(byDistance[2].Id, NeedModel.PlanVenue(person, 2, served, home, PlaceKind.Shop, from, until));
        byDistance[2].OpenMinute = 0; byDistance[2].CloseMinute = 0; byDistance[2].WeeklyHours.Clear();
        Assert.Equal(byDistance[1].Id, NeedModel.PlanVenue(person, 2, served, home, PlaceKind.Shop, from, until));
        // Nothing served and open: no venue, the drawn one stays.
        byDistance[1].OpenMinute = 0; byDistance[1].CloseMinute = 0; byDistance[1].WeeklyHours.Clear();
        Assert.Null(NeedModel.PlanVenue(person, 2, served, home, PlaceKind.Shop, from, until));
    }

    [Fact]
    public void TheHourlyServiceCountIsReadBackFromSavedReceiptsAfterAReload()
    {
        const string Shop = "address:Main%20Street:12:shop", Leisure = "address:Main%20Street:12:leisure";
        long hour = 30;
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        var wallet = Wallet(100000);
        // Two residents bought this hour, one through each alias of the same building, against a capacity of two.
        foreach (var (who, venue, minute) in new[] { ("a", Shop, hour * 60 + 5), ("b", Leisure, hour * 60 + 20) })
        {
            var request = new PurchaseRequest(who, venue, PurchaseIntention.Need(Gift), Money.FromCents(10000), minute);
            Assert.True(PurchaseContract.Run(request, wallet, receipts, new Native { CapacityThisHour = 2 }).IsBought);
        }
        receipts[PurchaseContract.Prefix + "c:" + Uri.EscapeDataString(Shop) + ":" + (hour * 60 + 30)] = "abandoned|x";
        receipts[PurchaseContract.Prefix + "d:" + Uri.EscapeDataString(Shop) + ":" + (hour * 60 - 1)] = "paid|1|x";
        receipts[PurchaseContract.Prefix + "e:" + Uri.EscapeDataString("other:shop") + ":" + (hour * 60 + 1)] = "uncertain|x";
        // After a reload the market starts from these saved receipts, not from zero.
        var read = new Dictionary<string, string>(receipts, StringComparer.Ordinal);
        var served = PurchaseContract.ServedInHour(read, hour);
        Assert.Equal(2, served[PurchaseContract.Building(Leisure)]);
        Assert.Equal(1, served["other:shop"]);
        Assert.Equal(2, served.Count);
        var third = new PurchaseRequest("f", Shop, PurchaseIntention.Need(Gift), Money.FromCents(10000), hour * 60 + 40);
        var full = new Native { ServedThisHour = served[PurchaseContract.Building(Shop)], CapacityThisHour = 2 };
        Assert.Equal(PurchaseRefusal.VenueFull, PurchaseContract.Run(third, wallet, read, full).Refusal);
        Assert.Equal(0, full.Calls);
    }

    [Fact]
    public void TheContractsFailureTransitionsLeaveWalletAndReceiptsConsistent()
    {
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        // Sell throws after booking, restore succeeds: Reversed, receipt gone, books restored, no debit.
        var wallet = Wallet(10000); string before = WalletText(wallet);
        var thrown = new Native { ThrowAfterBooking = true };
        var r = PurchaseContract.Run(Request(10000, 700), wallet, receipts, thrown);
        Assert.Equal((PurchaseRefusal.Reversed, 1, 0), (r.Refusal, thrown.Undos, thrown.Orders.Count));
        Assert.Empty(receipts); Assert.Equal(before, WalletText(wallet)); Assert.Equal(0, thrown.Commits);

        // Restore fails too: Uncertain, the receipt kept as uncertain, no debit, never pruned.
        var stuck = new Native { ThrowAfterBooking = true, ThrowUndo = true };
        r = PurchaseContract.Run(Request(10000, 701), wallet, receipts, stuck);
        Assert.Equal(PurchaseRefusal.Uncertain, r.Refusal);
        Assert.StartsWith("uncertain|", receipts[r.Key]); Assert.Equal(before, WalletText(wallet));
        Assert.Equal(0, PurchaseContract.Prune(receipts, 100));

        // Not sold after the pending receipt: the receipt is removed and the native refusal passes through.
        receipts.Clear();
        var declined = new Native { Before = () => PurchaseNativeOutcome.Refused(PurchaseRefusal.Dropped, "dj skill") };
        r = PurchaseContract.Run(Request(10000, 702), wallet, receipts, declined);
        Assert.Equal(PurchaseRefusal.Dropped, r.Refusal); Assert.Empty(receipts); Assert.Equal(0, declined.Undos);

        // The game's per-item check refused every line: an order with no paid entry, a declined receipt, no money.
        var refusedEvery = new Native { Paid = _ => Array.Empty<PurchaseLine>() };
        r = PurchaseContract.Run(Request(10000, 703), wallet, receipts, refusedEvery);
        Assert.Equal(PurchaseRefusal.NativeDeclined, r.Refusal);
        Assert.StartsWith("declined|", receipts[r.Key]); Assert.Equal(before, WalletText(wallet)); Assert.Equal(0, refusedEvery.Commits);

        // A wallet that cannot take the booked total is reversed before any debit.
        var held = Wallet(10000); held.HeldPurchase = Money.FromCents(1);
        Assert.Equal(PurchaseRefusal.Ceiling, PurchaseContract.Run(Request(10000, 704), held, receipts, new Native()).Refusal);

        // A bought sale commits its native follow-up once.
        var good = new Native();
        Assert.True(PurchaseContract.Run(Request(10000, 705), wallet, receipts, good).IsBought);
        Assert.Equal(1, good.Commits);

        // Recover: a pending receipt outside a running purchase becomes abandoned; Prune drops earlier days only.
        receipts.Clear();
        receipts[PurchaseContract.Prefix + "a:b:100"] = "pending|x";
        receipts[PurchaseContract.Prefix + "a:b:3000"] = "paid|1|x";
        receipts["other:key"] = "kept";
        Assert.Equal(1, PurchaseContract.Recover(receipts));
        Assert.Equal("abandoned|x", receipts[PurchaseContract.Prefix + "a:b:100"]);
        Assert.Equal(1, PurchaseContract.Prune(receipts, 2));
        Assert.Equal(new[] { "other:key", PurchaseContract.Prefix + "a:b:3000" }, receipts.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void TheNeedsWireRoundTripsAndRefusesMalformedCells()
    {
        var state = TestCity.Minimal(minute: 1440 * 3);
        var needs = new PersonNeeds(2, "home");
        needs.Add(new NeedState("mod:type with|odd,chars;%", 9, "", 3));
        needs.Add(new NeedState(Gift, 4, "home", 0));
        state.Citizens[0].Needs = needs;
        string payload = SaveCodec.Write(state);
        Assert.Contains("\"Needs\":\"1|2|", payload);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.True(needs.SameAs(read.State!.Citizens[0].Needs));
        Assert.Equal(new[] { Gift, "mod:type with|odd,chars;%" }, read.State.Citizens[0].Needs!.Needs.Select(n => n.Category));

        string cell = JObject.Parse(payload)["Citizens"]![0]!["Needs"]!.ToString();
        foreach (var bad in new[] { cell.Replace(",3", ",4"), cell.Replace("1|2|", "2|2|"), cell + ";" + cell.Split('|')[3].Split(';')[0],
            "1|-|000|", "1|2|000|x,01,---,0" })
            Assert.NotEqual(SaveReadStatus.Loaded, SaveCodec.Read(payload.Replace(cell, bad)).Status);
        // An impulse on a day that has not begun is refused.
        state.Citizens[0].Needs = new PersonNeeds(9);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }

    private static string WithoutNeeds(string payload) => System.Text.RegularExpressions.Regex.Replace(payload, "\"Needs\":(null|\"[^\"]*\"),", "");
    private static string Corpus(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ReaderCorpus", file));

    /// <summary>Without an attached purchase market nobody gains needs: with the "Needs" fields taken out, the corpus's
    /// full.json is the one written before GR-3 byte for byte.</summary>
    [Fact]
    public void WithoutTheNeedsFieldsTheCorpusIsTheBytesFromBeforeNeeds()
    {
        string full = SaveCodec.Write(ChangeJournalTests.Fixture(5150));
        Assert.Equal(Corpus(Path.Combine("schema-01", "full.json")), full);
        Assert.Contains("\"Needs\":null", full);
        Assert.Equal(Corpus("retired-pre-needs-full.json"), WithoutNeeds(full));
    }

    /// <summary>A city written before needs (schema 1, no "Needs" field) is an older development city: refused with
    /// "start a new city", never migrated, and every key kept.</summary>
    [Fact]
    public void ACityWrittenBeforeNeedsIsRefusedWithStartANewCityAndKept()
    {
        string old = Corpus("retired-pre-needs-full.json");
        Assert.StartsWith(SaveCodec.BodyPrefix, old);
        Assert.DoesNotContain("\"Needs\":", old);
        var read = SaveCodec.Read(old);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State);
        Assert.Contains("start a new city", read.Error);
        Assert.Contains("before needs", read.Error);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.DevelopmentSchemaOf(old));

        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = old, ["rlo:state"] = "owned by RLO" };
        var keys = new Dictionary<string, string>(data, StringComparer.Ordinal);
        var fresh = CurrentSchemaOnlyTests.City(); fresh.PopulationKey = JObject.Parse(old)["PopulationKey"]!.ToString();
        var session = SaveSession.Open(data, fresh.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, fresh).Success);
        Assert.Equal(keys.Count, data.Count);
        foreach (var (key, value) in keys) Assert.Same(value, data[key]);
    }
}
