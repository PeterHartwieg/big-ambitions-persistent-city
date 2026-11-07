using System;
using System.Numerics;

namespace PersistentCity.Core
{
    public enum OrdinaryVisitClass { ActivityOnly, Retail, Leisure }

    /// <summary>Published hourly evidence only. Runtime owns native reads; absence authorizes no spending.</summary>
    public readonly struct OrdinaryVisitTerms
    {
        public OrdinaryVisitTerms(long hour, bool playerOwned, OrdinaryVisitClass kind)
        { Hour = hour; PlayerOwned = playerOwned; Kind = kind; }
        public long Hour { get; }
        public bool PlayerOwned { get; }
        public OrdinaryVisitClass Kind { get; }
    }
    public interface IOrdinaryVisitOwnership
    {
        void ObserveBoundary(double minute);
        bool Pending { get; }
        void Continue();
        bool TryGet(string placeId, out OrdinaryVisitTerms terms);
    }
    public sealed class OrdinaryVisitPrices
    {
        // Modeled outside prices, never advertised/native receipts. F6h calibrates these defaults.
        public OrdinaryVisitPrices(int retailCents = 500, int leisureCents = 1200)
        {
            if (retailCents < 1 || retailCents > 10000 || leisureCents < 1 || leisureCents > 10000)
                throw new ArgumentOutOfRangeException(nameof(retailCents));
            Retail = Money.FromCents(retailCents); Leisure = Money.FromCents(leisureCents);
        }
        public Money Retail { get; }
        public Money Leisure { get; }
        public Money Price(OrdinaryVisitClass kind) => kind == OrdinaryVisitClass.Retail ? Retail
            : kind == OrdinaryVisitClass.Leisure ? Leisure : Money.Zero;
    }
    public static class OrdinaryExchanges
    {
        public static bool CanAdmit(ResidentBudget budget, Money price, double minute)
            => Position.Finite(minute) && minute > 0 && minute <= 1e9 && price > Money.Zero && budget.Ledger.VisitWatermark < (long)Math.Floor(minute)
                && price <= HouseholdFinance.SpendingLimit(budget)
                && Money.TryAdd(budget.Ledger.Discretionary, price, out _);

        /// <summary>One exact ModeledOutside counterpart. Every refusal leaves wallet and totals untouched.</summary>
        public static bool TryPay(ResidentBudget budget, EconomicLedger ledger, OrdinaryVisitClass kind, Money price, double minute)
        {
            if ((kind != OrdinaryVisitClass.Retail && kind != OrdinaryVisitClass.Leisure) || !CanAdmit(budget, price, minute)) return false;
            HouseholdFinance.CheckCash(budget);
            int day = checked((int)(minute / 1440));
            var rows = ledger.Outside;
            if (rows.Count > 0 && rows[rows.Count - 1].Day > day) return false;
            OutsideSpendingDay? row = rows.Count > 0 && rows[rows.Count - 1].Day == day ? rows[rows.Count - 1] : null;
            // Prepare the row before publishing money. Record stages both exact counters before assignment.
            bool added = row == null;
            if (row == null) row = new OutsideSpendingDay { Day = day };
            if (!row.TryRecord(kind, price)) return false;
            if (added) rows.Add(row);
            var accounts = budget.Ledger; accounts.Discretionary += price; accounts.VisitWatermark = (long)Math.Floor(minute);
            budget.Balance -= price; budget.Ledger = accounts;
            Prune(ledger, day);
            HouseholdFinance.CheckCash(budget); return true;
        }
        public static void Prune(EconomicLedger ledger, int day)
        {
            var rows = ledger.Outside;
            while (rows.Count > 0 && rows[0].Day < day - EconomicLedger.Days + 1) rows.RemoveAt(0);
        }
    }
    // Free feedback has no identity, shares, category breakdown or resident traits.
    public readonly struct OutsideSpendingTotal
    {
        public OutsideSpendingTotal(int firstDay, int lastDay, BigInteger units, BigInteger visits)
        { FirstDay = firstDay; LastDay = lastDay; Units = units; Visits = visits; }
        public int FirstDay { get; }
        public int LastDay { get; }
        public BigInteger Units { get; }
        public BigInteger Visits { get; }
        public string Text() => "Spending outside your businesses (modeled non-food visits, days " + FirstDay + "–" + LastDay
            + "): " + FoodFeedback.Amount(Units) + " · " + Visits + " visits. Transit, car costs, taxes and interest are not modeled.";
    }
    public sealed partial class CitySimulator
    {
        private IOrdinaryVisitOwnership? ordinaryOwnership;
        private long ordinaryLoadedHour, ordinaryObservedHour = -1;
        internal bool VisitorEndowments => ordinaryOwnership != null;
        internal bool OrdinaryExchangesActive => ordinaryObservedHour > ordinaryLoadedHour;
        private OrdinaryVisitPrices ordinaryPrices = new OrdinaryVisitPrices();
        public void AttachOrdinaryExchanges(IOrdinaryVisitOwnership ownership, OrdinaryVisitPrices? prices = null)
        {
            ordinaryOwnership = ownership ?? throw new ArgumentNullException(nameof(ownership)); ordinaryPrices = prices ?? new OrdinaryVisitPrices();
            ordinaryLoadedHour = (long)(State.Minute / 60); ordinaryObservedHour = -1;
        }
        private Money OrdinaryPrice(string placeId, Activity activity, double minute, out OrdinaryVisitClass kind)
        {
            kind = OrdinaryVisitClass.ActivityOnly;
            if ((activity != Activity.Shop && activity != Activity.Leisure) || ordinaryOwnership == null
                || !ordinaryOwnership.TryGet(placeId, out var terms) || terms.PlayerOwned || terms.Hour != (long)(minute / 60)) return Money.Zero;
            kind = terms.Kind; return ordinaryPrices.Price(kind);
        }
        private bool CanAdmitOrdinary(CitizenState person, string placeId, Activity activity, double minute)
        {
            Money price = OrdinaryPrice(placeId, activity, minute, out _);
            if (price == Money.Zero) return true; // activity only on unknown/stale/player-owned evidence
            var budget = food?.FindMeal(person.Identity.Id)?.Budget;
            if (budget != null) budget.VisitorEndowment = VisitorEndowments && person.IsVisitor;
            return budget == null || OrdinaryExchanges.CanAdmit(budget, price, minute);
        }
        private void CommitOrdinaryArrival(CitizenState live, CitizenState working)
        {
            if (working.MealOuting != null || working.RecentVisits.Count == 0) return;
            var visit = working.RecentVisits[working.RecentVisits.Count - 1];
            if (visit.AtMinute != State.Minute || live.RecentVisits.Count > 0
                && ReferenceEquals(visit, live.RecentVisits[live.RecentVisits.Count - 1])) return;
            var budget = food?.FindMeal(live.Identity.Id)?.Budget;
            var price = OrdinaryPrice(visit.PlaceId, visit.Activity, State.Minute, out var kind);
            if (budget != null && price > Money.Zero)
                OrdinaryExchanges.TryPay(budget, State.Economy, kind, price, State.Minute);
        }
        public long OutsideSpendingRequests { get; private set; }
        public OutsideSpendingTotal FreeOutsideSpending()
        {
            OutsideSpendingRequests++;
            int last = (int)(State.Minute / 1440), first = Math.Max(0, last - EconomicLedger.Days + 1);
            BigInteger units = 0, visits = 0;
            foreach (var row in State.Economy.Outside)
                if (row.Day >= first && row.Day <= last)
                { units += row.RetailUnits + row.LeisureUnits; visits += row.RetailVisits + row.LeisureVisits; }
            return new OutsideSpendingTotal(first, last, units, visits);
        }
    }
}
