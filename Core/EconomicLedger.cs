using System.Collections.Generic;
using System.Numerics;

namespace PersistentCity.Core
{
    // F6d save shape, used by F6e without adding fields. Shift: 0 unset, 1 working, 2 closed,
    // 3 settled, 4 closure day, 5 working after midnight, 6 working after midnight with prior attendance. Day is the shift-start day.
    // UnpostedWage is the half-denominator-biased exact numerator remainder (denominator 1920).
    // Accrual belongs to the PersonId/current Employment carried by its CitizenState (including VisitorRoutine).
    public struct EmploymentAccrual
    {
        public int AttendedMinutes { get; set; }
        public Money UnpostedWage { get; set; }
        public int MissMask { get; set; }
        public int Day { get; set; }
        public int Shift { get; set; }
    }

    // F5 B4: one wallet per PersonId; IncomeReceived remains OtherIncome; employment credits use EmploymentIncome.
    public struct OwnerAccounts
    {
        public Money EmploymentIncome { get; set; }
        public Money Discretionary { get; set; }
        // Last admitted visit's logical minute. Zero means no visit; F6f must admit at most once per minute.
        public long VisitWatermark { get; set; }
    }

    public sealed class MealPeriodCauses
    {
        public const int Periods = 4, Causes = 4, MaximumCount = 46655;
        // Period order: legacy, breakfast, lunch, dinner. Cause order: staff, stock, service/queue, closed/late.
        public int[] Counts { get; set; } = new int[Periods * Causes];
    }

    // Fixed categories, city totals only (no business identity or market denominator), exact D3a-wide units/counts.
    public sealed class OutsideSpendingDay
    {
        public int Day { get; set; }
        public BigInteger RetailUnits { get; set; }
        public BigInteger RetailVisits { get; set; }
        public BigInteger LeisureUnits { get; set; }
        public BigInteger LeisureVisits { get; set; }
    }

    public enum ShopChangeKind { Price, Stock, StaffedCounters, Hours }
    public struct ShopChange
    {
        public int ObservedMinute { get; set; }
        public ShopChangeKind Kind { get; set; }
        // Price/Stock: ProductId resolves through the wire-local name table. Hours: weekday * 128 + window slot * 2 + opening/closing edge. Staff: zero.
        public int Subject { get; set; }
        public string? ProductId { get; set; }
        // Price: exact Money units; Stock/Staff: counts; Hours: minute of day (1441 = absent edge).
        public long Before { get; set; }
        public long After { get; set; }
    }

    public struct ShopObservedOffer
    {
        public string ProductId { get; set; }
        public long PriceUnits { get; set; }
        // -1 unknown, 0 unavailable, 1 available; exact unit depletion is not journal evidence.
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
        // Canonical weekday/slot edges; empty until the first hours refresh.
        public int[] Hours { get; set; } = System.Array.Empty<int>();
        // Empty until first observation; otherwise the twenty-character Z85 encoding of a 128-bit fingerprint.
        public string Fingerprint { get; set; } = "";
        public List<ShopChange> Changes { get; set; } = new List<ShopChange>();
    }

    public sealed class EconomicLedger
    {
        public const int Days = 14, MaximumShops = 1024, ChangesPerShop = 8;
        public List<OutsideSpendingDay> Outside { get; set; } = new List<OutsideSpendingDay>();
        public List<ShopObservation> Shops { get; set; } = new List<ShopObservation>();
    }
}
