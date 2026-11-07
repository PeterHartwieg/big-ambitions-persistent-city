using System.Collections.Generic;
using System.Numerics;

namespace PersistentCity.Core
{
    // F6d is an inactive save contract. Only codecs, validation and zero/empty initialization use these values.
    // Accrual belongs to the PersonId/current Employment carried by its CitizenState (including VisitorRoutine).
    public struct EmploymentAccrual
    {
        public int AttendedMinutes { get; set; }
        public Money UnpostedWage { get; set; }
        public int MissMask { get; set; }
        public int Day { get; set; }
        public int Shift { get; set; }
    }

    // F5 B4: one wallet per PersonId; IncomeReceived remains OtherIncome while attendance wages are inactive.
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

    public sealed class ShopObservation
    {
        public int BusinessRef { get; set; }
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
