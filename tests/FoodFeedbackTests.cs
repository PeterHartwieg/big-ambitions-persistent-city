using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// City-audit F6c: read-only feedback projections and incremental panel counters.
public sealed partial class FoodCommerceTests
{
    static FoodShopDay F6c_Row(int day, long purchases, long cents, long chosen = 0, long price = 0, long access = 0, long closed = 0,
        long stock = 0, long staff = 0, long service = 0, long preferred = 0, long unknown = 0, string place = "s", string business = "b") => new()
    {
        Day = day, PlaceId = place, BusinessId = business, Purchases = purchases, Revenue = Money.FromCents(cents),
        ChosenOffers = chosen, PriceRejections = price, AccessRejections = access, ClosedRejections = closed, StockRejections = stock,
        StaffRejections = staff, ServiceRejections = service, PreferredAlternatives = preferred, UnknownEvidence = unknown,
        OffersConsidered = chosen + price + access + closed + stock + staff + service + preferred + unknown
    };
    static string[] F6c_Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    [Fact]
    public void F6c_GoldenWeekOverWeekTextEqualsRowSumsAndIgnoresOtherShopsStaleAndFutureRows()
    {
        var rows = new List<FoodShopDay> {
            F6c_Row(6, 100, 100000, chosen: 50), // older than fourteen days and not yet pruned
            F6c_Row(7, 2, 1000, chosen: 2, price: 5, staff: 3), F6c_Row(13, 1, 400, chosen: 1, stock: 3),
            F6c_Row(14, 3, 1500, chosen: 3, staff: 9), F6c_Row(20, 1, 500, chosen: 1, unknown: 1),
            F6c_Row(15, 9, 9000, staff: 40, business: "other"), F6c_Row(21, 7, 700, staff: 7) };
        var weeks = FoodFeedback.Weeks(rows, "s", "b", 20);
        Assert.Equal(new[] {
            "Week over week · this week = days 14–20 (day 20 so far) · last week = days 7–13",
            "Purchases: 4 this week · 3 last week · change +1. Sales: $20 this week · $14 last week · change +$6.",
            "Evaluated offers: 14 this week · 14 last week. " + FoodFeedback.OfferMeaning,
            "Chosen: 4 evaluated offers this week (last week 3)",
            "Unaffordable: 0 evaluated offers this week (last week 5)",
            "Stock unavailable: 0 evaluated offers this week (last week 3)",
            "No staffed counter: 9 evaluated offers this week (last week 3)",
            "Unknown evidence: 1 evaluated offer this week (last week 0)" }, F6c_Lines(weeks.Text()));
        // Every displayed number is the sum of the window's own rows.
        var mine = rows.Where(r => r.PlaceId == "s" && r.BusinessId == "b").ToList();
        foreach (var (week, from, to) in new[] { (weeks.ThisWeek, 14, 20), (weeks.LastWeek, 7, 13) })
        {
            var window = mine.Where(r => r.Day >= from && r.Day <= to).ToList();
            Assert.Equal(window.Sum(r => r.Purchases), week.Purchases);
            Assert.Equal(window.Aggregate(BigInteger.Zero, (t, r) => t + r.Revenue.Units), week.RevenueUnits);
            Assert.Equal(window.Sum(r => r.OffersConsidered), week.Offers);
            Assert.Equal(window.Sum(r => r.StaffRejections), week.Causes[(int)FoodOfferCause.NoStaffedCounter]);
            Assert.Equal(week.Offers, week.Causes.Sum());
            Assert.Equal(FoodWeekCoverage.Complete, week.Coverage);
        }
        string text = weeks.Text();
        Assert.All(F6c_Lines(text).Skip(3), line => Assert.Contains(" evaluated offer", line));
        Assert.All(new[] { "breakfast", "lunch", "dinner", ":00", "resident ", "residents " },
            word => Assert.DoesNotContain(word, text.Replace(FoodFeedback.OfferMeaning, ""), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void F6c_PartialFirstWeeksAreLabeledAndNeverShowADelta()
    {
        var rows = new List<FoodShopDay> { F6c_Row(10, 2, 300, chosen: 2, staff: 1), F6c_Row(18, 5, 800, chosen: 5, staff: 4) };
        Assert.Equal(new[] {
            "Week over week · this week = days 14–20 (day 20 so far) · last week = days 7–13",
            "Purchases: 5 this week · 2 last week (no record before day 10). Sales: $8 this week · $3 last week. No change shown: last week has days with no record (no evaluated offers, or before recording began).",
            "Evaluated offers: 9 this week · 3 last week (no record before day 10). " + FoodFeedback.OfferMeaning,
            "Chosen: 5 evaluated offers this week (last week 2, no record before day 10)",
            "No staffed counter: 4 evaluated offers this week (last week 1, no record before day 10)" },
            F6c_Lines(FoodFeedback.Weeks(rows, "s", "b", 20).Text()));
        var fresh = FoodFeedback.Weeks(new[] { F6c_Row(18, 5, 800, chosen: 5) }, "s", "b", 20);
        Assert.Equal(FoodWeekCoverage.Partial, fresh.ThisWeek.Coverage); Assert.Equal(18, fresh.ThisWeek.RecordedFrom);
        Assert.Equal(FoodWeekCoverage.NotRecorded, fresh.LastWeek.Coverage);
        Assert.Equal(new[] {
            "Week over week · this week = days 14–20 (day 20 so far) · last week = days 7–13",
            "Purchases: 5 this week (no record before day 18) · no record last week. Sales: $8 this week. No change shown: last week has days with no record (no evaluated offers, or before recording began).",
            "Evaluated offers: 5 this week (no record before day 18) · no record last week. " + FoodFeedback.OfferMeaning,
            "Chosen: 5 evaluated offers this week (no record last week)" }, F6c_Lines(fresh.Text()));
        Assert.Equal(new[] { "Week over week: nothing recorded in the last 14 days." },
            F6c_Lines(FoodFeedback.Weeks(new[] { F6c_Row(2, 1, 1) }, "s", "b", 20).Text()));
        // A negative change keeps an explicit sign under every runtime's currency pattern.
        var down = FoodFeedback.Weeks(new[] { F6c_Row(7, 5, 5000), F6c_Row(20, 1, 100) }, "s", "b", 20).Text();
        Assert.Contains("change -4. Sales: $1 this week · $50 last week · change -$49.", down);
    }

    [Fact]
    public void F6c_WeekBoundaryIsExactAcrossTheFourteenDayRolloverWithOrWithoutPruning()
    {
        for (int today = 0; today <= 40; today++)
            foreach (bool pruned in new[] { false, true })
            {
                var rows = Enumerable.Range(0, today + 1).Select(d => F6c_Row(d, d + 1, d, staff: 2 * d)).ToList();
                if (pruned) rows.RemoveAll(h => h.Day < today - 13); // FoodCommerce.ShopDay retention
                var weeks = FoodFeedback.Weeks(rows, "s", "b", today);
                long Sum(int from, int to, Func<int, long> value) => Enumerable.Range(Math.Max(0, from), Math.Max(0, to - Math.Max(0, from) + 1)).Sum(value);
                Assert.Equal(Sum(today - 6, today, d => d + 1), weeks.ThisWeek.Purchases);
                Assert.Equal(Sum(today - 13, today - 7, d => d + 1), weeks.LastWeek.Purchases);
                Assert.Equal(Sum(today - 13, today - 7, d => 2 * d), weeks.LastWeek.Causes[(int)FoodOfferCause.NoStaffedCounter]);
                Assert.Equal(today - 6, weeks.ThisWeek.FirstDay); Assert.Equal(today - 7, weeks.LastWeek.LastDay);
                Assert.Equal(today >= 13 ? FoodWeekCoverage.Complete : today >= 7 ? FoodWeekCoverage.Partial : FoodWeekCoverage.NotRecorded, weeks.LastWeek.Coverage);
                Assert.Equal(today >= 6 ? FoodWeekCoverage.Complete : FoodWeekCoverage.Partial, weeks.ThisWeek.Coverage);
                Assert.Equal(FoodFeedback.Weeks(rows.AsEnumerable().Reverse(), "s", "b", today).Text(), weeks.Text());
            }
        Assert.Equal(14, FoodFeedback.RetainedDays);
    }

    [Fact]
    public void F6c_QuietCoveredDaysCountAsZeroAndKeepTheComparison()
    {
        // Recording ran since before day 7 (another shop's row on day 5), but this shop was quiet on days 7 and 8.
        var rows = new List<FoodShopDay> { F6c_Row(5, 1, 100, chosen: 1, business: "other"),
            F6c_Row(9, 2, 400, chosen: 2, staff: 1), F6c_Row(18, 5, 800, chosen: 5, staff: 4) };
        var food = new FoodCommerceState { ChoiceStartsDay = 30 }; food.History.AddRange(rows);
        Assert.Equal(5, FoodFeedback.RecordingSince(food));
        var weeks = FoodFeedback.Weeks(rows, "s", "b", 20, FoodFeedback.RecordingSince(food));
        Assert.Equal(FoodWeekCoverage.Complete, weeks.LastWeek.Coverage); Assert.Equal(9, weeks.FirstRecordedDay);
        Assert.Equal(new[] {
            "Week over week · this week = days 14–20 (day 20 so far) · last week = days 7–13",
            "Purchases: 5 this week · 2 last week · change +3. Sales: $8 this week · $4 last week · change +$4.",
            "Evaluated offers: 9 this week · 3 last week. " + FoodFeedback.OfferMeaning,
            "Chosen: 5 evaluated offers this week (last week 2)",
            "No staffed counter: 4 evaluated offers this week (last week 1)" }, F6c_Lines(weeks.Text()));
        // The saved choice-policy start alone also proves coverage, with no row from any shop that early.
        var byStart = new FoodCommerceState { ChoiceStartsDay = 3 }; byStart.History.AddRange(rows.Skip(1));
        Assert.Equal(FoodWeekCoverage.Complete, FoodFeedback.Weeks(rows, "s", "b", 20, FoodFeedback.RecordingSince(byStart)).LastWeek.Coverage);
        // A genuinely unknown period stays unknown: city recording itself began on day 9.
        var late = new FoodCommerceState { ChoiceStartsDay = 10 }; late.History.AddRange(rows.Skip(1));
        var unknown = FoodFeedback.Weeks(rows, "s", "b", 20, FoodFeedback.RecordingSince(late));
        Assert.Equal(FoodWeekCoverage.Partial, unknown.LastWeek.Coverage); Assert.Equal(9, unknown.LastWeek.RecordedFrom);
        Assert.Contains("No change shown", unknown.Text());
    }

    [Fact]
    public void F6c_RecordedHistoryProjectsTheSameNumbersAsItsRowsAndAfterReload()
    {
        var (city, market, routes) = Ready(count: 2, threeMeals: true);
        market.Units = 1000; city.EnableHouseholdFinances();
        for (int day = 2; day <= 17; day++)
        {
            if (day == 9) market.Capacity = 0; // a staffing gap shows up as evaluated "no staffed counter" offers
            if (day == 12) market.Capacity = 15;
            Advance(city, day * 1440 + 1439);
        }
        var food = city.State.Food!; int today = (int)(city.State.Minute / 1440);
        var weeks = FoodFeedback.Weeks(food.History, "shop", "b", today);
        var rows = food.History.Where(h => h.PlaceId == "shop" && h.BusinessId == "b").ToList();
        Assert.Equal(rows.Where(r => r.Day > today - 7 && r.Day <= today).Sum(r => r.Purchases), weeks.ThisWeek.Purchases);
        Assert.Equal(rows.Where(r => r.Day > today - 14 && r.Day <= today - 7).Sum(r => r.OffersConsidered), weeks.LastWeek.Offers);
        Assert.Equal(rows.Where(r => r.Day > today - 14 && r.Day <= today - 7).Sum(r => r.StaffRejections), weeks.LastWeek.Causes[(int)FoodOfferCause.NoStaffedCounter]);
        Assert.True(weeks.ThisWeek.Purchases > 0);
        var restored = Reload(city, market, routes);
        Assert.Equal(weeks.Text(), FoodFeedback.Weeks(restored.State.Food!.History, "shop", "b", today).Text());
    }

    // Every setter that the counters observe, applied in random order to attached budgets, meals and excursions.
    [Fact]
    public void F6c_CountersEqualAFullRescanAfterEveryMutation()
    {
        var food = new FoodCommerceState();
        for (int i = 0; i < 12; i++)
            food.Residents.Add(new ResidentMeal { ResidentId = "c" + i, Budget = i % 4 == 3 ? null : new ResidentBudget {
                OpeningBalance = Money.FromCents(5000), Balance = Money.FromCents(5000), DailyHousingCents = 1000, DailyEssentialsCents = 500, DailyHomeFoodCents = 300 } });
        var counters = FoodMoneyCounters.Attach(food);
        Assert.Equal(FoodMoneyCounters.Rescan(food), counters.Snapshot());
        var random = new Random(61);
        Money M() => Money.FromUnits(random.Next(4) == 0 ? 0 : (long)(random.NextDouble() * 9e18) * (random.Next(5) == 0 ? -1 : 1) / (random.Next(3) == 0 ? 1 : 1000000000L));
        var detached = new List<ResidentBudget>(); var excursions = new List<MealExcursion>();
        for (int step = 0; step < 4000; step++)
        {
            var meal = food.Residents[random.Next(food.Residents.Count)]; var b = meal.Budget;
            switch (random.Next(20))
            {
                case 0: meal.Status = (MealStatus)random.Next(7); break;
                case 1: var old = meal.Budget; meal.Budget = random.Next(3) == 0 ? null : random.Next(2) == 0 && detached.Count > 0 ? Take(detached) : new ResidentBudget { Balance = M(), Arrears = M() };
                    if (old != null) detached.Add(old); break;
                case 2: meal.Excursion = random.Next(3) == 0 ? null : new MealExcursion { Unresolved = random.Next(2) == 0 }; if (meal.Excursion != null) excursions.Add(meal.Excursion); break;
                case 3: if (meal.Excursion != null) meal.Excursion.Unresolved = !meal.Excursion.Unresolved; break;
                case 4: if (excursions.Count > 0) excursions[random.Next(excursions.Count)].Unresolved ^= true; break; // detached or attached
                case 5: if (detached.Count > 0) detached[random.Next(detached.Count)].Balance = M(); break; // a detached budget no longer counts
                default:
                    if (b == null) break;
                    switch (random.Next(15))
                    {
                        case 0: b.OpeningBalance = M(); break; case 1: b.Balance = M(); break; case 2: b.IncomeReceived = M(); break;
                        case 3: b.EssentialsPaid = M(); break; case 4: b.HomeFoodPaid = M(); break; case 5: b.PreparedFoodSpent = M(); break;
                        case 6: b.ExternalFoodSpent = M(); break; case 7: b.HeldPurchase = M(); break; case 8: b.Arrears = M(); break;
                        case 9: b.DailyHousingCents = random.Next(-5, 5000); break; case 10: b.DailyEssentialsCents = random.Next(int.MinValue, int.MaxValue); break;
                        case 14: var ledger = b.Ledger; ledger.EmploymentIncome = M(); b.Ledger = ledger; break;
                        case 11: b.DailyHomeFoodCents = random.Next(0, 2000); break; case 12: b.LastPostedDay = random.Next(-1, 4); break;
                        default: b.HomeFoodFundedDay = random.Next(-1, 4); break;
                    }
                    break;
            }
            Assert.Equal(FoodMoneyCounters.Rescan(food), counters.Snapshot());
        }
        Assert.Equal(1, counters.Rebuilds);
        // Exact 128-bit carries at the extremes of the money unit, including removing long.MinValue.
        var edge = food.Residents.First(m => m.Budget != null);
        foreach (long units in new[] { long.MinValue, long.MaxValue, long.MinValue, -1L, long.MaxValue, 0L, long.MinValue })
        {
            edge.Budget!.Balance = Money.FromUnits(units); edge.Budget.Arrears = Money.FromUnits(units);
            Assert.Equal(FoodMoneyCounters.Rescan(food), counters.Snapshot());
        }
        foreach (var m in food.Residents) if (m.Budget != null) m.Budget.PreparedFoodSpent = Money.FromUnits(long.MaxValue);
        edge.Budget = new ResidentBudget { HeldPurchase = Money.FromUnits(long.MinValue) }; edge.Budget = null;
        Assert.Equal(FoodMoneyCounters.Rescan(food), counters.Snapshot());
        Assert.True(counters.Snapshot().PreparedFoodSpent > long.MaxValue);
        // A consumer appended without the commerce owner is caught by the count guard and rescanned.
        food.Residents.Add(new ResidentMeal { ResidentId = "late", Status = MealStatus.Uncertain, Budget = new ResidentBudget { Arrears = Money.FromCents(1) } });
        Assert.Equal(FoodMoneyCounters.Rescan(food), counters.Snapshot()); Assert.Equal(2, counters.Rebuilds);
        static ResidentBudget Take(List<ResidentBudget> list) { var b = list[^1]; list.RemoveAt(list.Count - 1); return b; }
    }

    [Fact]
    public void F6c_ShortFlagMatchesTheSpendingLimitAndWordsFollowTheSameStates()
    {
        var random = new Random(5);
        for (int i = 0; i < 5000; i++)
        {
            var b = new ResidentBudget { Balance = Money.FromCents(random.Next(0, 40000)), HeldPurchase = Money.FromCents(random.Next(3) == 0 ? random.Next(1, 900) : 0),
                Arrears = Money.FromCents(random.Next(3) == 0 ? random.Next(1, 900) : 0), DailyHousingCents = random.Next(0, 15000),
                DailyEssentialsCents = random.Next(0, 2500), DailyHomeFoodCents = random.Next(0, 1500) };
            int flags = FoodMoneyCounters.Flags(b);
            Assert.Equal(b.HeldPurchase == Money.Zero && HouseholdFinance.SpendingLimit(b) == Money.Zero, (flags & FoodMoneyCounters.ShortFlag) != 0);
            var words = ResidentBudgetWords.Capture(b);
            Assert.Equal(b.Arrears != Money.Zero, words.Contains(ResidentBudgetWords.BehindOnBills));
            Assert.Equal(b.Arrears == Money.Zero && (flags & FoodMoneyCounters.ShortFlag) != 0, words.Contains(ResidentBudgetWords.NoSpareMoney));
            Assert.DoesNotContain(string.Join(" ", words), char.IsDigit);
        }
        var unfed = new ResidentBudget { Balance = Money.FromCents(100000), LastPostedDay = 4, HomeFoodFundedDay = 3, IncomeSource = ResidentIncomeSource.ExternalSupport };
        Assert.Equal(new[] { ResidentBudgetWords.WithoutHomeBasket, ResidentBudgetWords.OnSupport }, ResidentBudgetWords.Capture(unfed));
        Assert.Empty(ResidentBudgetWords.Capture(null));
        Assert.Empty(ResidentBudgetWords.Capture(new ResidentBudget { Balance = Money.FromCents(100000), LastPostedDay = 4, HomeFoodFundedDay = 4 }));
    }

    [Fact]
    public void F6c_InspectorShowsBudgetWordsWithoutNumbersAndWithoutChangingTheSave()
    {
        var (city, _, _) = Ready(threeMeals: true); city.EnableHouseholdFinances();
        var meal = city.State.Food!.Residents.Single();
        meal.Budget!.Arrears = Money.FromCents(250);
        string before = DevelopmentChain.Write(city.State);
        var insight = city.Inspect(meal.HostId)!;
        Assert.Equal(ResidentBudgetWords.BehindOnBills, insight.BudgetWords[0]);
        Assert.DoesNotContain(ResidentBudgetWords.NoSpareMoney, insight.BudgetWords);
        Assert.DoesNotContain(string.Join(" ", insight.BudgetWords), char.IsDigit);
        Assert.Contains(ResidentBudgetWords.BehindOnBills + ".", insight.Text());
        Assert.Equal(before, DevelopmentChain.Write(city.State));
        meal.Budget.Arrears = Money.Zero; meal.Budget.Balance = Money.FromCents(1);
        var words = city.Inspect(meal.HostId)!.BudgetWords;
        Assert.Contains(ResidentBudgetWords.NoSpareMoney, words); Assert.DoesNotContain(ResidentBudgetWords.BehindOnBills, words);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void F6c_CountersEqualRescansThroughSimulationReloadAndMigration(bool uncertain)
    {
        var (city, market, routes) = Ready(count: 3, threeMeals: true, representation: 2);
        market.ThrowAfterSale = uncertain; city.EnableHouseholdFinances();
        void Check(CitySimulator c) => Assert.Equal(FoodMoneyCounters.Rescan(c.State.Food!), c.FoodMoney!.Snapshot());
        Check(city);
        for (double minute = city.State.Minute + 1; minute <= 4 * 1440; minute += 1)
        {
            Advance(city, minute); Check(city);
            if (minute % 360 == 0) { city = Reload(city, market, routes); Check(city); }
        }
        var snapshot = city.FoodMoney!.Snapshot();
        Assert.True(snapshot.Budgets == 6 && snapshot.IncomeReceived > 0 && snapshot.EssentialsPaid > 0);
        Assert.Equal(uncertain, snapshot.Uncertain > 0);
        // A reload through the current writer rebuilds the totals from the loaded records.
        string source = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var migrated = new CitySimulator(read.State!, routes); migrated.AttachFoodMarket(market);
        Check(migrated); Assert.Equal(snapshot, migrated.FoodMoney!.Snapshot());
        // A route rebuild attaches a fresh owner over the same records; the old owner stops counting.
        var old = migrated.FoodMoney!; var rebuilt = migrated.RebuildRoutes(routes);
        rebuilt.State.Food!.Residents[0].Budget!.Balance += Money.FromCents(7);
        Check(rebuilt); Assert.NotEqual(FoodMoneyCounters.Rescan(rebuilt.State.Food), old.Snapshot());
    }

    [Fact]
    public void F6c_NothingIsBuiltWhileHiddenAndMutationsAllocateNothing()
    {
        var (city, _, _) = Ready(count: 2, threeMeals: true); city.EnableHouseholdFinances();
        Advance(city, 3 * 1440);
        Assert.Equal(0, city.FoodMoney!.Requests); Assert.Equal(1, city.FoodMoney.Rebuilds);
        var budget = city.State.Food!.Residents[0].Budget!; var meal = city.State.Food.Residents[0];
        var one = Money.FromUnits(1); budget.Balance += one; meal.Status = MealStatus.Uncertain; meal.Status = MealStatus.Pending; // warm
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            budget.Balance += one; budget.Arrears = (i & 1) == 0 ? one : Money.Zero; budget.LastPostedDay = i & 3;
            meal.Status = (i & 1) == 0 ? MealStatus.Uncertain : MealStatus.Pending;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - start);
        Assert.Equal(0, city.FoodMoney.Requests);
        Assert.Equal(FoodMoneyCounters.Rescan(city.State.Food), city.FoodMoney.Snapshot());
        Assert.Equal(1, city.FoodMoney.Requests);
    }

    [Fact]
    public void F6c_FreeFeedbackHasNoPaidResearchFieldsNamesOrShares()
    {
        // D3a guard extended to the F6c projections: totals, days and labeled coverage only.
        Assert.Equal(new[] { "Arrears", "BehindOnBills", "BlockedReturns", "Budgets", "Consumers", "EssentialsPaid", "ExternalFoodSpent",
            "HeldPurchase", "HoldingPurchase", "HomeFoodPaid", "IncomeReceived", "NoSpareMoney", "OpeningBalance", "PreparedFoodSpent",
            "Uncertain", "WithoutHomeBasket", "Balance" }.OrderBy(x => x, StringComparer.Ordinal),
            typeof(FoodMoneySnapshot).GetProperties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "FirstRecordedDay", "LastWeek", "ThisWeek", "Today" }, typeof(FoodShopWeeks).GetProperties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "Causes", "Coverage", "FirstDay", "LastDay", "Offers", "Purchases", "RecordedFrom", "RevenueUnits", "Rows" },
            typeof(FoodWeek).GetProperties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "Rebuilds", "Requests" }, typeof(FoodMoneyCounters).GetProperties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        var (city, _, _) = RivalOutflowTests.Ready(finances: true);
        var food = city.State.Food!;
        foreach (var m in food.Residents) { m.BusinessId = "SECRET RIVAL"; m.PlaceId = "SECRET ADDRESS"; }
        food.History.Add(F6c_Row(0, 1, 100, chosen: 1, place: "SECRET ADDRESS", business: "SECRET RIVAL"));
        var money = city.FoodMoney!.Snapshot(); var weeks = FoodFeedback.Weeks(food.History, "SECRET ADDRESS", "SECRET RIVAL", 1);
        string shown = money.Text() + weeks.Text() + JsonConvert.SerializeObject(money) + JsonConvert.SerializeObject(weeks) + FoodFeedback.RivalEstimateLabel;
        Assert.DoesNotContain("SECRET", shown);
        foreach (string word in new[] { "share", "market", "research", "trait", "percent", "%" })
            Assert.DoesNotContain(word, shown, StringComparison.OrdinalIgnoreCase);
        // The Q3 default switch (false: no share line) is the only permitted mention of a share.
        foreach (string c5f in new[] { "if (GUILayout.Button(\"Research reports\", GUILayout.Width(140))) researchWindow.Toggle();",
            "if (researchWindow.Open) researchWindow.Draw(ResearchLedgerNow, researchNames ??= ResearchBusinessName);" })
        foreach (string paid in new[] { "MarketResearch", "Research", "ProjectMarket", "Share" })
        Assert.Equal("Rival availability: estimated", FoodFeedback.RivalEstimateLabel);
        bool shareLine = FoodFeedback.FreeMealShareLine, suggestions = FoodFeedback.SuggestionLines;
        Assert.False(shareLine); Assert.False(suggestions); // plan Q3/Q4 defaults
        foreach (string advice in new[] { "add ", "consider", "should", "try " })
            Assert.DoesNotContain(advice, weeks.Text() + money.Text(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void F6c_CountersAreNotSavedAndWriterStays25()
    {
        var (city, market, routes) = Ready(count: 2, threeMeals: true); city.EnableHouseholdFinances();
        Advance(city, 2 * 1440 + 700);
        string before = DevelopmentChain.Write(city.State);
        _ = city.FoodMoney!.Snapshot(); _ = city.FoodMoney.Snapshot().Text();
        Assert.Equal(before, DevelopmentChain.Write(city.State));
        Assert.Equal(CityState.CurrentSchema, DevelopmentChain.Read(before).State!.Schema);
        Assert.DoesNotContain("counters", before, StringComparison.OrdinalIgnoreCase);
        var restored = Reload(city, market, routes);
        Assert.Equal(before, DevelopmentChain.Write(restored.State));
    }

    static string F6c_Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Core", "FoodFeedback.cs"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    static string F6c_Source(string file) => File.ReadAllText(Path.Combine(F6c_Root(), file));
}
