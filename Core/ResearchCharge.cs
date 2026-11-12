using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ResearchChargeResult { Deferred, Paid, PaidFromScan, Declined, BalancePrecision, Cancelled, PaymentUnknown }

    public readonly struct ResearchChargeRecord
    {
        public ResearchChargeRecord(string? tag, bool marketing, bool taxDeductible, float amount, float balance, int day, object? address)
        { Tag = tag; Marketing = marketing; TaxDeductible = taxDeductible; Amount = amount; Balance = balance; Day = day; Address = address; }
        public string? Tag { get; }
        public bool Marketing { get; }
        public bool TaxDeductible { get; }
        public float Amount { get; }
        public float Balance { get; }
        public int Day { get; }
        public object? Address { get; }
    }

    public sealed class ResearchChargeTarget
    {
        public ResearchChargeTarget(string businessName, object address)
        {
            BusinessName = businessName ?? throw new ArgumentNullException(nameof(businessName));
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }
        public string BusinessName { get; }
        public object Address { get; }
    }

    public readonly struct ResearchChargeRequest
    {
        internal ResearchChargeRequest(string orderId, ResearchChargeTarget target, float amount, int day, bool taxDeductible)
        { OrderId = orderId; Target = target; Amount = amount; Day = day; TaxDeductible = taxDeductible; }
        public string OrderId { get; }
        public ResearchChargeTarget Target { get; }
        public float Amount { get; }
        public int Day { get; }
        public bool TaxDeductible { get; }
    }

    public interface IResearchChargePort
    {
        bool TagStored { get; }
        float Balance { get; }
        int BilledDay { get; }
        bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target);
        void CollectTagged(List<ResearchChargeRecord> into);
        object? Tail();
        void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into);
        bool Charge(in ResearchChargeRequest request, Action enteringNative);
    }

    public struct ResearchChargeTally
    {
        public int Attempts, Paid, PaidFromScan, Declined, BalancePrecision, Cancelled, PaymentUnknown, Deferred;
        public bool ChargingOff;
        internal void Add(ResearchChargeResult result)
        {
            Attempts++;
            switch (result)
            {
                case ResearchChargeResult.Paid: Paid++; break;
                case ResearchChargeResult.PaidFromScan: PaidFromScan++; break;
                case ResearchChargeResult.Declined: Declined++; break;
                case ResearchChargeResult.BalancePrecision: BalancePrecision++; break;
                case ResearchChargeResult.Cancelled: Cancelled++; break;
                case ResearchChargeResult.PaymentUnknown: PaymentUnknown++; break;
                default: Deferred++; break;
            }
        }
    }

    public static class ResearchCharge
    {
        public const string TagKey = "persistentcity_research";
        public const string BusinessNameKey = "businessName";
        public const string MarketingType = "ba:transaction_marketing";
        public const string MarketingCategory = "ba:transactioncategory_marketing";
        public const string TaxDeductibleName = "tax_marketing";

        public static bool TryExactDebit(float balance, ResearchProduct product, out float after, out float flooredAfter)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            after = flooredAfter = 0;
            if (float.IsNaN(balance) || float.IsInfinity(balance)) return false;
            long price = product.PriceDollars;
            int bits = BitConverter.SingleToInt32Bits(balance);
            int biased = (bits >> 23) & 255;
            long significand = bits & 0x7fffff;
            int exponent = -149;
            if (biased != 0) { significand |= 0x800000; exponent = biased - 150; }
            if (bits < 0) significand = -significand;
            long numerator;
            int power = 0;
            if (significand == 0) numerator = -price;
            else
            {
                while ((significand & 1) == 0) { significand >>= 1; exponent++; }
                if (exponent > 38 || exponent < -39) return false;
                if (exponent >= 0) numerator = (significand << exponent) - price;
                else { numerator = significand - (price << -exponent); power = exponent; }
            }
            if (numerator != 0)
            {
                while ((numerator & 1) == 0) { numerator >>= 1; power++; }
                if (numerator >= 1L << 24 || numerator <= -(1L << 24) || power < -149) return false;
            }
            after = (float)((double)balance - price);
            flooredAfter = (float)Math.Floor(after);
            return true;
        }

        public static bool ProvesEarlierCharge(in ResearchChargeRecord record, ResearchOrder order, ResearchChargeTarget target)
            => record.Marketing && string.Equals(record.Tag, order.Id, StringComparison.Ordinal)
                && Equals(record.Address, target.Address) && record.Amount == -order.Product.NativePrice;

        public static bool ProvesCharge(in ResearchChargeRecord record, in ResearchChargeRequest request, float flooredAfter)
            => record.Marketing && string.Equals(record.Tag, request.OrderId, StringComparison.Ordinal)
                && Equals(record.Address, request.Target.Address) && record.Day == request.Day
                && record.Amount == request.Amount && record.TaxDeductible == request.TaxDeductible
                && record.Balance == flooredAfter;
    }

    public sealed class ResearchChargeSession
    {
        private readonly ResearchLedger ledger;
        private readonly IResearchChargePort port;
        private readonly Func<bool> active;
        private readonly Action<Action> guardedUnit;
        private readonly ResearchOrder?[] due = new ResearchOrder?[ResearchLedger.MaximumOpenOrders];
        private readonly List<ResearchChargeRecord> tagged = new List<ResearchChargeRecord>(ResearchLedger.MaximumKeptReports);
        private readonly List<ResearchChargeRecord> appended = new List<ResearchChargeRecord>(4);
        private ResearchOrder? current;
        private ResearchChargeResult currentResult;
        private bool entered;
        private readonly Action billCurrent, enteringNative;

        public ResearchChargeSession(ResearchLedger ledger, IResearchChargePort port, Func<bool> active, Action<Action> guardedUnit)
        {
            this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            this.port = port ?? throw new ArgumentNullException(nameof(port));
            this.active = active ?? throw new ArgumentNullException(nameof(active));
            this.guardedUnit = guardedUnit ?? throw new ArgumentNullException(nameof(guardedUnit));
            billCurrent = BillCurrent;
            enteringNative = EnteringNative;
        }

        public ResearchLedger Ledger => ledger;
        public ResearchChargeTally LastBoundary { get; private set; }

        public bool HasWork
        {
            get
            {
                if (!ledger.HasOpenOrders || !active()) return false;
                var open = ledger.Open;
                for (int i = 0; i < open.Count; i++) if (open[i].State == ResearchOrderState.Surveyed) return true;
                return false;
            }
        }

        public ResearchChargeTally Boundary()
        {
            var tally = new ResearchChargeTally();
            if (!HasWork) { LastBoundary = tally; return tally; }
            if (!port.TagStored) { tally.ChargingOff = true; LastBoundary = tally; return tally; }
            int count = 0;
            var open = ledger.Open;
            for (int i = 0; i < open.Count; i++) if (open[i].State == ResearchOrderState.Surveyed) due[count++] = open[i];
            try
            {
                tagged.Clear();
                port.CollectTagged(tagged);
                for (int i = 0; i < count; i++)
                {
                    if (!active()) break;
                    current = due[i];
                    currentResult = ResearchChargeResult.Deferred;
                    guardedUnit(billCurrent);
                    tally.Add(currentResult);
                }
            }
            finally
            {
                Array.Clear(due, 0, due.Length);
                current = null; tagged.Clear(); appended.Clear();
                LastBoundary = tally;
            }
            return tally;
        }

        private void BillCurrent()
        {
            var order = current!;
            if (order.State != ResearchOrderState.Surveyed) return;
            entered = false;
            try { currentResult = Bill(order); }
            catch (Exception) when (order.State == ResearchOrderState.Surveyed)
            {
                currentResult = entered ? Unknown(order) : ResearchChargeResult.Deferred;
            }
        }

        private void EnteringNative()
        {
            if (entered) throw new InvalidOperationException("Native entry may occur only once.");
            entered = true;
        }

        private ResearchChargeResult Bill(ResearchOrder order)
        {
            if (!port.TryResolve(order, out var target) || target == null)
                return Move(ledger.TryCancel(order.Id), ResearchChargeResult.Cancelled);
            int matches = 0;
            ResearchChargeRecord found = default;
            foreach (var record in tagged)
                if (string.Equals(record.Tag, order.Id, StringComparison.Ordinal)) { matches++; found = record; }
            if (matches == 1 && ResearchCharge.ProvesEarlierCharge(found, order, target))
                return Move(ledger.TryMarkPaid(order.Id), ResearchChargeResult.PaidFromScan);
            if (matches > 0) return Unknown(order);
            float balance = port.Balance;
            if (!ResearchCharge.TryExactDebit(balance, order.Product, out _, out float flooredAfter))
                return Move(ledger.TryDecline(order.Id, ResearchDeclineReason.BalancePrecision), ResearchChargeResult.BalancePrecision);
            var request = new ResearchChargeRequest(order.Id, target, -order.Product.NativePrice, port.BilledDay, order.TaxDeductible);
            object? tail = port.Tail();
            bool charged = port.Charge(request, enteringNative);
            if (!entered) return charged ? Unknown(order) : ResearchChargeResult.Deferred;
            appended.Clear();
            port.CollectTaggedAfter(tail, appended);
            matches = 0;
            foreach (var record in appended)
                if (string.Equals(record.Tag, order.Id, StringComparison.Ordinal)) { matches++; found = record; }
            if (!charged) return matches == 0
                ? Move(ledger.TryDecline(order.Id, ResearchDeclineReason.InsufficientFunds), ResearchChargeResult.Declined)
                : Unknown(order);
            return matches == 1 && ResearchCharge.ProvesCharge(found, request, flooredAfter)
                ? Move(ledger.TryMarkPaid(order.Id), ResearchChargeResult.Paid)
                : Unknown(order);
        }

        private ResearchChargeResult Unknown(ResearchOrder order)
            => Move(ledger.TryMarkPaymentUnknown(order.Id), ResearchChargeResult.PaymentUnknown);

        private static ResearchChargeResult Move(ResearchMoveRefusal refusal, ResearchChargeResult result)
        {
            if (refusal != ResearchMoveRefusal.None) throw new InvalidOperationException("Research ledger refused " + result + ": " + refusal);
            return result;
        }
    }
}
