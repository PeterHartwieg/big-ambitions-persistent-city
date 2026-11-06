using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>What one billing attempt did to its order. Deferred leaves the order Surveyed for the next bill:
    /// nothing reached native money. Every other result is a ledger move.</summary>
    public enum ResearchChargeResult { Deferred, Paid, PaidFromScan, Declined, BalancePrecision, Cancelled, PaymentUnknown }

    /// <summary>One native transaction record that carries the research tag, as the billing sees it.
    /// Address is the native address object; equality is the native value equality.</summary>
    public readonly struct ResearchChargeRecord
    {
        public ResearchChargeRecord(string? tag, bool marketing, bool taxDeductible, float amount, float balance, int day, object? address)
        { Tag = tag; Marketing = marketing; TaxDeductible = taxDeductible; Amount = amount; Balance = balance; Day = day; Address = address; }
        /// <summary>The value of <see cref="ResearchCharge.TagKey"/> in the record's transaction data.</summary>
        public string? Tag { get; }
        /// <summary>Native marketing type and marketing category.</summary>
        public bool Marketing { get; }
        public bool TaxDeductible { get; }
        public float Amount { get; }
        /// <summary>Native balance after the change, floored by native code.</summary>
        public float Balance { get; }
        public int Day { get; }
        public object? Address { get; }
    }

    /// <summary>The business an order bills, resolved by the port at billing time.</summary>
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

    /// <summary>The one native ChangeMoneySafe call of an order: native marketing type and category,
    /// data {businessName, persistentcity_research: OrderId}, tax_marketing when TaxDeductible, force false.</summary>
    public readonly struct ResearchChargeRequest
    {
        internal ResearchChargeRequest(string orderId, ResearchChargeTarget target, float amount, int day, bool taxDeductible)
        { OrderId = orderId; Target = target; Amount = amount; Day = day; TaxDeductible = taxDeductible; }
        public string OrderId { get; }
        public ResearchChargeTarget Target { get; }
        /// <summary>The negated product price, exact in binary32.</summary>
        public float Amount { get; }
        /// <summary>The billed day: native Day - 1, as the native marketing bill uses.</summary>
        public int Day { get; }
        public bool TaxDeductible { get; }
    }

    /// <summary>Native money and transactions for research billing. Runtime implements it over GameManager and the
    /// native transaction queue; tests use doubles. No member changes native campaigns.</summary>
    public interface IResearchChargePort
    {
        /// <summary>The native contract check found that the tag key is stored in transaction data. False keeps charging off.</summary>
        bool TagStored { get; }
        /// <summary>Native binary32 balance in dollars.</summary>
        float Balance { get; }
        int BilledDay { get; }
        /// <summary>False when the business is no longer an eligible research target.</summary>
        bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target);
        /// <summary>Appends every queued record whose data holds the tag key, oldest first. Read only.</summary>
        void CollectTagged(List<ResearchChargeRecord> into);
        /// <summary>The newest queued record, by reference; null for an empty queue.</summary>
        object? Tail();
        /// <summary>Appends the tagged records queued after <paramref name="tail"/>. The queue may have dropped records,
        /// including the tail, from its front; then every queued record counts as appended.</summary>
        void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into);
        /// <summary>Calls enteringNative immediately before the native call and returns its result.</summary>
        bool Charge(in ResearchChargeRequest request, Action enteringNative);
    }

    /// <summary>Counts for one daily boundary. Diagnostics only.</summary>
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

    /// <summary>Plan C5 section 3.5: the exact-debit preflight and the evidence rules. Pure.</summary>
    public static class ResearchCharge
    {
        public const string TagKey = "persistentcity_research";
        public const string BusinessNameKey = "businessName";
        public const string MarketingType = "ba:transaction_marketing";
        public const string MarketingCategory = "ba:transactioncategory_marketing";
        public const string TaxDeductibleName = "tax_marketing";

        /// <summary>True when native binary32 subtraction removes exactly the product price from the balance.
        /// The test is exact on the rational values: B - price must be a binary32 value. Native ChangeMoney adds
        /// with correctly rounded binary32 arithmetic, so the debit is then exact (also under wider intermediate
        /// precision), and its clamp is inactive. after is that value; flooredAfter is the native record balance.
        /// Nonfinite balances are refused.</summary>
        public static bool TryExactDebit(float balance, ResearchProduct product, out float after, out float flooredAfter)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            after = flooredAfter = 0;
            if (float.IsNaN(balance) || float.IsInfinity(balance)) return false;
            long price = product.PriceDollars; // 1 .. 10,000 < 2^14
            int bits = BitConverter.SingleToInt32Bits(balance);
            int biased = (bits >> 23) & 255;
            long significand = bits & 0x7fffff;
            int exponent = -149;
            if (biased != 0) { significand |= 0x800000; exponent = biased - 150; }
            if (bits < 0) significand = -significand;
            // B - price = numerator * 2^power, exactly.
            long numerator;
            int power = 0;
            if (significand == 0) numerator = -price;
            else
            {
                while ((significand & 1) == 0) { significand >>= 1; exponent++; }
                // |B| >= 2^39: the lowest bit of B - price is below 2^14 and its highest at least 2^38, so it spans
                // more than 24 bits. Below 2^-39 the odd numerator exceeds 2^24. Both are refused without overflow.
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

        /// <summary>Step 1: an existing tagged record proves this order was charged. Not the billed day: it may be from
        /// an earlier boundary. Type, address and amount must match exactly.</summary>
        public static bool ProvesEarlierCharge(in ResearchChargeRecord record, ResearchOrder order, ResearchChargeTarget target)
            => record.Marketing && string.Equals(record.Tag, order.Id, StringComparison.Ordinal)
                && Equals(record.Address, target.Address) && record.Amount == -order.Product.NativePrice;

        /// <summary>Step 5: the one appended tagged record must be this charge: marketing, this address and billed day,
        /// amount exactly the negated price, the order's tax flag and native balance floor(B - price).</summary>
        public static bool ProvesCharge(in ResearchChargeRecord record, in ResearchChargeRequest request, float flooredAfter)
            => record.Marketing && string.Equals(record.Tag, request.OrderId, StringComparison.Ordinal)
                && Equals(record.Address, request.Target.Address) && record.Day == request.Day
                && record.Amount == request.Amount && record.TaxDeductible == request.TaxDeductible
                && record.Balance == flooredAfter;
    }

    /// <summary>The research bill of one attached city at the native daily marketing boundary.
    /// Each order is billed in its own save-epoch guarded unit (F7b rule 5), so a save started by a native
    /// money-change listener refuses and stops instead of saving the debit without the ledger move.
    /// The tag scan before the units is read only and raises no native event. Single-threaded.</summary>
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

        /// <param name="active">False once Persistent City is stopped or detached. Checked before every order.</param>
        /// <param name="guardedUnit">Runs its argument as one F7b guarded unit.</param>
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

        /// <summary>True when an attached, active city has a Surveyed order. No allocation.</summary>
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

        /// <summary>One native daily marketing boundary. Bills every order that is Surveyed now, oldest first.
        /// An exception from the tag scan propagates before any charge; the caller contains it. A port exception
        /// inside an order is contained there.</summary>
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
                // Before entry nothing reached native money: the order waits for the next bill. After entry it is
                // blocked. Either way the boundary continues with the next order.
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
            // Step 1: an earlier debit of this order, for example from a boundary whose ledger move was lost.
            int matches = 0;
            ResearchChargeRecord found = default;
            foreach (var record in tagged)
                if (string.Equals(record.Tag, order.Id, StringComparison.Ordinal)) { matches++; found = record; }
            if (matches == 1 && ResearchCharge.ProvesEarlierCharge(found, order, target))
                return Move(ledger.TryMarkPaid(order.Id), ResearchChargeResult.PaidFromScan);
            if (matches > 0) return Unknown(order);
            // Step 2: exact-debit preflight, before any native call.
            float balance = port.Balance;
            if (!ResearchCharge.TryExactDebit(balance, order.Product, out _, out float flooredAfter))
                return Move(ledger.TryDecline(order.Id, ResearchDeclineReason.BalancePrecision), ResearchChargeResult.BalancePrecision);
            var request = new ResearchChargeRequest(order.Id, target, -order.Product.NativePrice, port.BilledDay, order.TaxDeductible);
            // Step 3: remember the queue end, then mark native entry immediately before the call.
            object? tail = port.Tail();
            // Step 6: an exception from here on is contained by BillCurrent: PaymentUnknown after entry.
            bool charged = port.Charge(request, enteringNative);
            if (!entered) return charged ? Unknown(order) : ResearchChargeResult.Deferred;
            // Step 5: only records appended after the remembered end; unrelated records are ignored.
            appended.Clear();
            port.CollectTaggedAfter(tail, appended);
            matches = 0;
            foreach (var record in appended)
                if (string.Equals(record.Tag, order.Id, StringComparison.Ordinal)) { matches++; found = record; }
            // Step 4: false is no money change by native contract, provided no record of this order appeared.
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
            // The order was Surveyed on entry and only this unit moves it.
            if (refusal != ResearchMoveRefusal.None) throw new InvalidOperationException("Research ledger refused " + result + ": " + refusal);
            return result;
        }
    }
}
