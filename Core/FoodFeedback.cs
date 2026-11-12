using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PersistentCity.Core
{
    public static class FoodFeedback
    {
        public const string RivalEstimateLabel = "Rival availability: estimated";
        public const bool FreeMealShareLine = false;
        public const bool SuggestionLines = false;
        public const int WeekDays = 7;
        public const int RetainedDays = 2 * WeekDays;
        public const string OfferMeaning = "Evaluated offers are daily totals summed per week. A resident who decides again counts again, so these are offers, not people, and these daily totals are not located by hour or meal. Meal-period causes appear separately.";
        private static readonly CultureInfo Dollars = CultureInfo.GetCultureInfo("en-US");
        private static readonly string[] CauseLabels = { "Chosen", "Unaffordable", "Travel/time", "Closed or too late",
            "Stock unavailable", "No staffed counter", "Service/queue", "Preferred another option", "Unknown evidence" };

        public static string CauseLabel(FoodOfferCause cause) => CauseLabels[(int)cause];

        public static Dictionary<(string, string), List<FoodShopDay>> IndexHistory(IReadOnlyList<FoodShopDay> history)
        {
            var index = new Dictionary<(string, string), List<FoodShopDay>>();
            for (int i = 0; i < history.Count; i++)
            {
                var row = history[i]; var key = Key(row.PlaceId, row.BusinessId);
                if (!index.TryGetValue(key, out var rows)) index.Add(key, rows = new List<FoodShopDay>(RetainedDays));
                rows.Add(row);
            }
            return index;
        }
        public static (string, string) Key(string placeId, string businessId) => (placeId, businessId);

        public static int RecordingSince(FoodCommerceState food)
        {
            int since = food.ChoiceStartsDay;
            foreach (var row in food.History) if (row.Day < since) since = row.Day;
            return since;
        }

        public static FoodShopWeeks Weeks(IEnumerable<FoodShopDay> rows, string placeId, string businessId, int today, int? recordingSince = null)
        {
            var current = new FoodWeek(today - WeekDays + 1, today);
            var previous = new FoodWeek(today - RetainedDays + 1, today - WeekDays);
            int? first = null;
            foreach (var row in rows)
            {
                if (row.PlaceId != placeId || row.BusinessId != businessId || row.Day > today || row.Day < previous.FirstDay) continue;
                (row.Day >= current.FirstDay ? current : previous).Add(row);
                if (first == null || row.Day < first) first = row.Day;
            }
            int? coverage = recordingSince.HasValue && (first == null || recordingSince < first) ? recordingSince : first;
            current.Close(coverage); previous.Close(coverage);
            return new FoodShopWeeks(today, first, current, previous);
        }

        public static string Amount(BigInteger units) => Amount((decimal)units);
        private static string Amount(decimal units) => (units / Money.UnitsPerDollar).ToString("C0", Dollars);
        private static string SignedAmount(BigInteger units) => (units < 0 ? "-" : "+") + Amount(BigInteger.Abs(units));
        private static string Signed(long value) => (value < 0 ? "" : "+") + value.ToString(CultureInfo.InvariantCulture);
        private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static void AppendWeeks(StringBuilder text, FoodShopWeeks weeks)
        {
            var now = weeks.ThisWeek; var last = weeks.LastWeek;
            if (weeks.FirstRecordedDay == null)
            {
                text.AppendLine("Week over week: nothing recorded in the last " + N(RetainedDays) + " days.");
                return;
            }
            text.Append("Week over week · this week = days ").Append(Days(now)).Append(" (day ").Append(N(weeks.Today))
                .Append(" so far) · last week = days ").Append(Days(last)).AppendLine();
            text.Append("Purchases: ").Append(N(now.Purchases)).Append(" this week").Append(Coverage(now)).Append(" · ");
            if (last.Coverage == FoodWeekCoverage.NotRecorded) text.Append("no record last week");
            else text.Append(N(last.Purchases)).Append(" last week").Append(Coverage(last));
            if (last.Coverage == FoodWeekCoverage.Complete)
                text.Append(" · change ").Append(Signed(now.Purchases - last.Purchases)).Append(". Sales: ").Append(Amount(now.RevenueUnits))
                    .Append(" this week · ").Append(Amount(last.RevenueUnits)).Append(" last week · change ")
                    .Append(SignedAmount(now.RevenueUnits - last.RevenueUnits)).AppendLine(".");
            else
                text.Append(". Sales: ").Append(Amount(now.RevenueUnits)).Append(" this week")
                    .Append(last.Coverage == FoodWeekCoverage.NotRecorded ? "" : " · " + Amount(last.RevenueUnits) + " last week")
                    .AppendLine(". No change shown: last week has days with no record (no evaluated offers, or before recording began).");
            text.Append("Evaluated offers: ").Append(N(now.Offers)).Append(" this week").Append(Coverage(now)).Append(" · ")
                .Append(last.Coverage == FoodWeekCoverage.NotRecorded ? "no record last week" : N(last.Offers) + " last week" + Coverage(last))
                .Append(". ").AppendLine(OfferMeaning);
            for (int i = 0; i < CauseLabels.Length; i++)
            {
                long a = now.Causes[i], b = last.Causes[i];
                if (a == 0 && b == 0) continue;
                text.Append(CauseLabels[i]).Append(": ").Append(N(a)).Append(a == 1 ? " evaluated offer" : " evaluated offers").Append(" this week (");
                if (last.Coverage == FoodWeekCoverage.NotRecorded) text.Append("no record last week");
                else text.Append("last week ").Append(N(b)).Append(last.Coverage == FoodWeekCoverage.Partial ? ", no record before day " + N(last.RecordedFrom!.Value) : "");
                text.AppendLine(")");
            }
        }
        private static string Days(FoodWeek week) => N(week.FirstDay) + "–" + N(week.LastDay);
        private static string Coverage(FoodWeek week) => week.Coverage == FoodWeekCoverage.Partial
            ? " (no record before day " + N(week.RecordedFrom!.Value) + ")" : "";

        internal static string MoneyText(FoodMoneySnapshot money)
        {
            var text = new StringBuilder(512);
            text.AppendLine("CURRENT RESIDENT ACCOUNTS · lifetime totals");
            text.Append("Cash ").Append(Amount(money.Balance)).Append(" · received income/support ").AppendLine(Amount(money.IncomeReceived));
            text.Append("Housing/essentials paid ").Append(Amount(money.EssentialsPaid)).Append(" · home-food baskets ")
                .Append(Amount(money.HomeFoodPaid)).Append(" · prepared food ").AppendLine(Amount(money.PreparedFoodSpent));
            text.Append("Unpaid bills ").Append(Amount(money.Arrears)).Append(" · purchase funds awaiting confirmation ").AppendLine(Amount(money.HeldPurchase));
            text.Append("Budgets now: ").Append(N(money.BehindOnBills)).Append(" of ").Append(N(money.Budgets))
                .Append(" consumer budgets behind on bills · ").Append(N(money.NoSpareMoney))
                .Append(" with no money to spare for prepared food · ").Append(N(money.WithoutHomeBasket))
                .AppendLine(" went without the home-food basket at their last daily posting. Each represented consumer has its own budget; these are not households.");
            return text.ToString();
        }
    }

    public enum FoodOfferCause { Chosen, Unaffordable, TravelTime, ClosedOrLate, StockUnavailable, NoStaffedCounter, ServiceQueue, PreferredAnother, Unknown }
    public enum FoodWeekCoverage { NotRecorded, Partial, Complete }

    public sealed class FoodWeek
    {
        private readonly long[] causes = new long[9];
        internal FoodWeek(int firstDay, int lastDay) { FirstDay = firstDay; LastDay = lastDay; }
        public int FirstDay { get; }
        public int LastDay { get; }
        public FoodWeekCoverage Coverage { get; private set; }
        public int? RecordedFrom { get; private set; }
        public int Rows { get; private set; }
        public long Purchases { get; private set; }
        public BigInteger RevenueUnits { get; private set; }
        public long Offers { get; private set; }
        public IReadOnlyList<long> Causes => causes;
        internal void Add(FoodShopDay row)
        {
            Rows++; Purchases += row.Purchases; RevenueUnits += row.Revenue.Units; Offers += row.OffersConsidered;
            causes[(int)FoodOfferCause.Chosen] += row.ChosenOffers; causes[(int)FoodOfferCause.Unaffordable] += row.PriceRejections;
            causes[(int)FoodOfferCause.TravelTime] += row.AccessRejections; causes[(int)FoodOfferCause.ClosedOrLate] += row.ClosedRejections;
            causes[(int)FoodOfferCause.StockUnavailable] += row.StockRejections; causes[(int)FoodOfferCause.NoStaffedCounter] += row.StaffRejections;
            causes[(int)FoodOfferCause.ServiceQueue] += row.ServiceRejections; causes[(int)FoodOfferCause.PreferredAnother] += row.PreferredAlternatives;
            causes[(int)FoodOfferCause.Unknown] += row.UnknownEvidence;
        }
        internal void Close(int? firstRecorded)
        {
            if (firstRecorded == null || firstRecorded > LastDay) Coverage = FoodWeekCoverage.NotRecorded;
            else if (firstRecorded > FirstDay) { Coverage = FoodWeekCoverage.Partial; RecordedFrom = firstRecorded; }
            else Coverage = FoodWeekCoverage.Complete;
        }
    }

    public sealed class FoodShopWeeks
    {
        internal FoodShopWeeks(int today, int? first, FoodWeek thisWeek, FoodWeek lastWeek)
        { Today = today; FirstRecordedDay = first; ThisWeek = thisWeek; LastWeek = lastWeek; }
        public int Today { get; }
        public int? FirstRecordedDay { get; }
        public FoodWeek ThisWeek { get; }
        public FoodWeek LastWeek { get; }
        public string Text() { var text = new StringBuilder(768); AppendTo(text); return text.ToString(); }
        public void AppendTo(StringBuilder text) => FoodFeedback.AppendWeeks(text, this);
    }

    internal enum FoodMoneyField { OpeningBalance, Balance, IncomeReceived, EssentialsPaid, HomeFoodPaid, PreparedFoodSpent, ExternalFoodSpent, HeldPurchase, Arrears }

    public sealed class FoodMoneySnapshot : IEquatable<FoodMoneySnapshot>
    {
        internal FoodMoneySnapshot(BigInteger[] totals, int consumers, int budgets, int uncertain, int blocked, int[] flags)
        {
            OpeningBalance = totals[0]; Balance = totals[1]; IncomeReceived = totals[2];
            EssentialsPaid = totals[3]; HomeFoodPaid = totals[4]; PreparedFoodSpent = totals[5];
            ExternalFoodSpent = totals[6]; HeldPurchase = totals[7]; Arrears = totals[8];
            Consumers = consumers; Budgets = budgets; Uncertain = uncertain; BlockedReturns = blocked;
            BehindOnBills = flags[0]; HoldingPurchase = flags[1]; NoSpareMoney = flags[2]; WithoutHomeBasket = flags[3];
        }
        public int Consumers { get; }
        public int Budgets { get; }
        public int Uncertain { get; }
        public int BlockedReturns { get; }
        public int BehindOnBills { get; }
        public int HoldingPurchase { get; }
        public int NoSpareMoney { get; }
        public int WithoutHomeBasket { get; }
        public BigInteger OpeningBalance { get; }
        public BigInteger Balance { get; }
        public BigInteger IncomeReceived { get; }
        public BigInteger EssentialsPaid { get; }
        public BigInteger HomeFoodPaid { get; }
        public BigInteger PreparedFoodSpent { get; }
        public BigInteger ExternalFoodSpent { get; }
        public BigInteger HeldPurchase { get; }
        public BigInteger Arrears { get; }
        public string Text() => FoodFeedback.MoneyText(this);
        public bool Equals(FoodMoneySnapshot? other) => other != null && Consumers == other.Consumers && Budgets == other.Budgets
            && Uncertain == other.Uncertain && BlockedReturns == other.BlockedReturns && BehindOnBills == other.BehindOnBills
            && HoldingPurchase == other.HoldingPurchase && NoSpareMoney == other.NoSpareMoney && WithoutHomeBasket == other.WithoutHomeBasket
            && OpeningBalance == other.OpeningBalance && Balance == other.Balance && IncomeReceived == other.IncomeReceived
            && EssentialsPaid == other.EssentialsPaid && HomeFoodPaid == other.HomeFoodPaid && PreparedFoodSpent == other.PreparedFoodSpent
            && ExternalFoodSpent == other.ExternalFoodSpent && HeldPurchase == other.HeldPurchase && Arrears == other.Arrears;
        public override bool Equals(object? obj) => Equals(obj as FoodMoneySnapshot);
        public override int GetHashCode() => HashCode.Combine(Consumers, Budgets, Uncertain, BlockedReturns, Balance, Arrears);
        public override string ToString() => string.Join(",", new object[] { Consumers, Budgets, Uncertain, BlockedReturns, BehindOnBills,
            HoldingPurchase, NoSpareMoney, WithoutHomeBasket, OpeningBalance, Balance, IncomeReceived, EssentialsPaid, HomeFoodPaid,
            PreparedFoodSpent, ExternalFoodSpent, HeldPurchase, Arrears });
    }

    public sealed class FoodMoneyCounters
    {
        internal const int BehindFlag = 1, HoldFlag = 2, ShortFlag = 4, BasketFlag = 8;
        private readonly FoodCommerceState state;
        private readonly long[] high = new long[9];
        private readonly ulong[] low = new ulong[9];
        private readonly int[] flagCounts = new int[4];
        private int meals, budgets, uncertain, blocked;
        private FoodMoneyCounters(FoodCommerceState state) { this.state = state; }
        public long Requests { get; private set; }
        public long Rebuilds { get; private set; }

        public static FoodMoneyCounters Attach(FoodCommerceState state)
        {
            var counters = new FoodMoneyCounters(state ?? throw new ArgumentNullException(nameof(state)));
            counters.Rebuild(); return counters;
        }
        private void Rebuild()
        {
            Array.Clear(high, 0, high.Length); Array.Clear(low, 0, low.Length); Array.Clear(flagCounts, 0, flagCounts.Length);
            meals = budgets = uncertain = blocked = 0; Rebuilds++;
            foreach (var meal in state.Residents) Add(meal);
        }
        public FoodMoneySnapshot Snapshot()
        {
            Requests++;
            if (meals != state.Residents.Count) Rebuild();
            var totals = new BigInteger[9];
            for (int i = 0; i < totals.Length; i++) totals[i] = ((BigInteger)high[i] << 64) + low[i];
            return new FoodMoneySnapshot(totals, meals, budgets, uncertain, blocked, flagCounts);
        }
        public static FoodMoneySnapshot Rescan(FoodCommerceState state)
        {
            var sums = new decimal[9]; var flags = new int[4]; int budgets = 0, uncertain = 0, blocked = 0;
            foreach (var meal in state.Residents)
            {
                if (meal.Status == MealStatus.Uncertain) uncertain++;
                if (meal.Excursion?.Unresolved == true) blocked++;
                var b = meal.Budget; if (b == null) continue;
                budgets++;
                sums[0] += b.OpeningBalance.Units; sums[1] += b.Balance.Units; sums[2] += b.IncomeReceived.Units; sums[2] += b.Ledger.EmploymentIncome.Units;
                sums[3] += b.EssentialsPaid.Units; sums[4] += b.HomeFoodPaid.Units; sums[5] += b.PreparedFoodSpent.Units;
                sums[6] += b.ExternalFoodSpent.Units; sums[7] += b.HeldPurchase.Units; sums[8] += b.Arrears.Units;
                Count(flags, Flags(b), 1);
            }
            var totals = new BigInteger[9];
            for (int i = 0; i < totals.Length; i++) totals[i] = new BigInteger(sums[i]);
            return new FoodMoneySnapshot(totals, state.Residents.Count, budgets, uncertain, blocked, flags);
        }

        internal static int Flags(ResidentBudget b)
        {
            int flags = 0;
            if (b.Arrears.Units != 0) flags |= BehindFlag;
            if (b.HeldPurchase.Units != 0) flags |= HoldFlag;
            else if (b.Arrears.Units != 0 || b.Balance <= HouseholdFinance.SpendingReserve(b))
                flags |= ShortFlag;
            if (!b.VisitorEndowment && b.LastPostedDay >= 0 && b.HomeFoodFundedDay != b.LastPostedDay) flags |= BasketFlag;
            return flags;
        }
        private static void Count(int[] counts, int flags, int sign)
        {
            if ((flags & BehindFlag) != 0) counts[0] += sign;
            if ((flags & HoldFlag) != 0) counts[1] += sign;
            if ((flags & ShortFlag) != 0) counts[2] += sign;
            if ((flags & BasketFlag) != 0) counts[3] += sign;
        }

        internal void Add(ResidentMeal meal)
        {
            meal.counters = this; meals++;
            if (meal.Status == MealStatus.Uncertain) uncertain++;
            Join(meal.Excursion); Join(meal.Budget);
        }
        internal void EmploymentMoved(Money before, Money after)
        { Add(2, after.Units); Subtract(2, before.Units); }
        internal void Moved(FoodMoneyField field, Money old, Money value, int before, ResidentBudget budget)
        {
            int i = (int)field; Add(i, value.Units); Subtract(i, old.Units);
            Reflag(before, budget);
        }
        private void Add(int field, long units)
        {
            ulong sum = unchecked(low[field] + (ulong)units);
            high[field] += (units < 0 ? -1 : 0) + (sum < low[field] ? 1 : 0);
            low[field] = sum;
        }
        private void Subtract(int field, long units)
        {
            if (units == long.MinValue) { Add(field, long.MaxValue); Add(field, 1); }
            else Add(field, -units);
        }
        internal void Reflag(int before, ResidentBudget budget)
        {
            int after = Flags(budget);
            if (after != before) { Count(flagCounts, before, -1); Count(flagCounts, after, 1); }
        }
        private void Signed(int field, Money amount, int sign)
        { if (sign > 0) Add(field, amount.Units); else Subtract(field, amount.Units); }
        internal void Restatus(MealStatus old, MealStatus value)
        {
            if (old == MealStatus.Uncertain) uncertain--;
            if (value == MealStatus.Uncertain) uncertain++;
        }
        internal void Unresolve(bool old, bool value) { if (old != value) blocked += value ? 1 : -1; }
        internal void Replaced(MealExcursion? old, MealExcursion? value) { Leave(old); Join(value); }
        internal void Replaced(ResidentBudget? old, ResidentBudget? value) { Leave(old); Join(value); }
        private void Join(MealExcursion? excursion)
        {
            if (excursion == null) return;
            excursion.counters = this; if (excursion.Unresolved) blocked++;
        }
        private void Leave(MealExcursion? excursion)
        {
            if (excursion == null || excursion.counters != this) return;
            if (excursion.Unresolved) blocked--;
            excursion.counters = null;
        }
        private void Join(ResidentBudget? b)
        {
            if (b == null) return;
            b.counters = this; budgets++; Apply(b, 1);
        }
        private void Leave(ResidentBudget? b)
        {
            if (b == null || b.counters != this) return;
            budgets--; Apply(b, -1); b.counters = null;
        }
        private void Apply(ResidentBudget b, int sign)
        {
            Signed(0, b.OpeningBalance, sign); Signed(1, b.Balance, sign); Signed(2, b.IncomeReceived, sign); Signed(2, b.Ledger.EmploymentIncome, sign);
            Signed(3, b.EssentialsPaid, sign); Signed(4, b.HomeFoodPaid, sign); Signed(5, b.PreparedFoodSpent, sign);
            Signed(6, b.ExternalFoodSpent, sign); Signed(7, b.HeldPurchase, sign); Signed(8, b.Arrears, sign);
            Count(flagCounts, Flags(b), sign);
        }
    }

    public static class ResidentBudgetWords
    {
        public const string BehindOnBills = "behind on bills";
        public const string NoSpareMoney = "no money to spare for eating out right now";
        public const string WithoutHomeBasket = "could not afford groceries at home on the last bill day";
        public const string OnSupport = "living on support income";

        public static IReadOnlyList<string> Capture(ResidentBudget? budget)
        {
            if (budget == null) return Array.Empty<string>();
            int flags = FoodMoneyCounters.Flags(budget);
            var words = new List<string>(3);
            if ((flags & FoodMoneyCounters.BehindFlag) != 0) words.Add(BehindOnBills);
            else if ((flags & FoodMoneyCounters.ShortFlag) != 0) words.Add(NoSpareMoney);
            if ((flags & FoodMoneyCounters.BasketFlag) != 0) words.Add(WithoutHomeBasket);
            if (budget.IncomeSource == ResidentIncomeSource.ExternalSupport) words.Add(OnSupport);
            return words;
        }
    }

    public sealed partial class FoodCommerce
    {
        internal FoodMoneyCounters? Feedback { get; private set; }
    }
    public sealed partial class CitySimulator
    {
        public FoodMoneyCounters? FoodMoney => food?.Feedback;
    }
}
