using System.Collections.Generic;
using System.Numerics;

namespace PersistentCity.Core
{
    public struct EmploymentAccrual
    {
        public int AttendedMinutes { get; set; }
        public Money UnpostedWage { get; set; }
        public int MissMask { get; set; }
        public int Day { get; set; }
        public int Shift { get; set; }
    }

    public struct OwnerAccounts
    {
        public Money EmploymentIncome { get; set; }
        public Money Discretionary { get; set; }
        public long VisitWatermark { get; set; }
    }

    public sealed class MealPeriodCauses
    {
        public const int Periods = 4, Causes = 4, MaximumCount = 46655;
        public int[] Counts { get; set; } = new int[Periods * Causes];
    }

    public sealed class OutsideSpendingDay
    {
        public int Day { get; set; }
        private OutsideCounter retailUnits, retailVisits, leisureUnits, leisureVisits;
        public BigInteger RetailUnits { get => retailUnits.Value; set => retailUnits = new OutsideCounter(value); }
        public BigInteger RetailVisits { get => retailVisits.Value; set => retailVisits = new OutsideCounter(value); }
        public BigInteger LeisureUnits { get => leisureUnits.Value; set => leisureUnits = new OutsideCounter(value); }
        public BigInteger LeisureVisits { get => leisureVisits.Value; set => leisureVisits = new OutsideCounter(value); }
        internal bool TryRecord(OrdinaryVisitClass kind, Money price)
        {
            var units = kind == OrdinaryVisitClass.Retail ? retailUnits : leisureUnits;
            var visits = kind == OrdinaryVisitClass.Retail ? retailVisits : leisureVisits;
            if (!units.TryAdd(price.Units) || !visits.TryAdd(1)) return false;
            if (kind == OrdinaryVisitClass.Retail) { retailUnits = units; retailVisits = visits; }
            else { leisureUnits = units; leisureVisits = visits; }
            return true;
        }
        private struct OutsideCounter
        {
            private static readonly BigInteger Maximum = BigInteger.Pow(10, 26) - 1;
            private BigInteger basis;
            private long delta, allowance;
            internal OutsideCounter(BigInteger value) { basis = value; delta = 0; allowance = Remaining(value); }
            private static long Remaining(BigInteger value)
            {
                var remaining = Maximum - value;
                return remaining <= 0 ? 0 : remaining > long.MaxValue ? long.MaxValue : (long)remaining;
            }
            internal BigInteger Value
            {
                get
                {
                    if (delta != 0) { basis += delta; delta = 0; }
                    allowance = Remaining(basis); return basis;
                }
            }
            internal bool TryAdd(long amount)
            {
                if (allowance < amount)
                {
                    var current = basis + delta;
                    allowance = Remaining(current);
                    if (allowance < amount) return false;
                }
                if (delta > long.MaxValue - amount) { basis += delta; delta = 0; }
                delta += amount; allowance -= amount; return true;
            }
        }
    }

    public enum ShopChangeKind { Price, Stock, StaffedCounters, Hours }
    public struct ShopChange
    {
        public int ObservedMinute { get; set; }
        public ShopChangeKind Kind { get; set; }
        public int Subject { get; set; }
        public string? ProductId { get; set; }
        public long Before { get; set; }
        public long After { get; set; }
    }

    public struct ShopObservedOffer
    {
        public string ProductId { get; set; }
        public long PriceUnits { get; set; }
        public int Stock { get; set; }
    }

    public sealed class ShopObservation
    {
        internal int[] HourCounts = new int[7];
        internal string HoursHash = "";
        public int BusinessRef { get; set; }
        public long Evicted { get; set; }
        public int StaffedCounters { get; set; } = -1;
        public ShopObservedOffer[] Supply { get; set; } = System.Array.Empty<ShopObservedOffer>();
        public int[] Hours { get; set; } = System.Array.Empty<int>();
        public string Fingerprint { get; set; } = "";
        public List<ShopChange> Changes { get; set; } = new List<ShopChange>();
    }

    public sealed class EconomicLedger : IEconomicLedgerView
    {
        public const int Days = 14, MaximumShops = 1024, ChangesPerShop = 8;
        public List<OutsideSpendingDay> Outside { get; set; } = new List<OutsideSpendingDay>();
        public List<ShopObservation> Shops { get; set; } = new List<ShopObservation>();
        int IEconomicLedgerView.OutsideDays => Outside.Count;
        int IEconomicLedgerView.ObservedShops => Shops.Count;
    }
}
