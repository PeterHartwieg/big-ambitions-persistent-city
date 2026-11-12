using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistentCity.Core
{
    public enum ProbeFamily { Unsupported, FullService, SelfService, Hairdresser, Gym, CinemaTheater, Nightclub }
    public enum ProbeKind { Food, Goods, Service }
    public enum ProbeRefusal { None, Empty, Ceiling, Wallet, AlreadyRecorded, Interrupted, Reversed, NativeDeclined, Uncertain }
    public enum ProbeReceiptStatus { Missing, Pending, Paid, Declined, Abandoned, Uncertain, Conflict }

    public readonly struct ProbeLine
    {
        public ProbeLine(string itemId, Money price, bool mandatory)
        {
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("Empty item.", nameof(itemId));
            if (price <= Money.Zero) throw new ArgumentOutOfRangeException(nameof(price));
            ItemId = itemId; Price = price; Mandatory = mandatory;
        }
        public string ItemId { get; }
        public Money Price { get; }
        public bool Mandatory { get; }
    }

    public sealed class ProbeQuote
    {
        internal ProbeQuote(IReadOnlyList<ProbeLine> kept, Money total, Money ceiling, ProbeRefusal refusal)
        { Kept = kept; Total = total; Ceiling = ceiling; Refusal = refusal; }
        public IReadOnlyList<ProbeLine> Kept { get; }
        public Money Total { get; }
        public Money Ceiling { get; }
        public ProbeRefusal Refusal { get; }
    }

    public sealed class PurchaseProbeRequest
    {
        public PurchaseProbeRequest(string residentId, string placeId, ProbeKind kind, long minute, Money ceiling, IReadOnlyList<ProbeLine> basket)
        {
            if (string.IsNullOrEmpty(residentId) || string.IsNullOrEmpty(placeId)) throw new ArgumentException("Probe needs a resident and a place.");
            ResidentId = residentId; PlaceId = placeId; Kind = kind; Minute = minute; Ceiling = ceiling; Basket = basket;
        }
        public string ResidentId { get; }
        public string PlaceId { get; }
        public ProbeKind Kind { get; }
        public long Minute { get; }
        public Money Ceiling { get; }
        public IReadOnlyList<ProbeLine> Basket { get; }
        public string Key => PurchaseProbe.Prefix + Uri.EscapeDataString(ResidentId) + ":" + Uri.EscapeDataString(PlaceId) + ":" + Minute.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public readonly struct ProbeNativeOutcome
    {
        private ProbeNativeOutcome(bool sold, IReadOnlyList<ProbeLine> paid, int unpriced, string reason) { Sold = sold; Paid = paid; Unpriced = unpriced; Reason = reason; }
        public static ProbeNativeOutcome Interrupted(string reason) => new ProbeNativeOutcome(false, Array.Empty<ProbeLine>(), 0, reason);
        public static ProbeNativeOutcome Processed(IReadOnlyList<ProbeLine> paid, int unpriced = 0) => new ProbeNativeOutcome(true, paid, unpriced, "");
        public bool Sold { get; }
        public IReadOnlyList<ProbeLine> Paid { get; }
        public int Unpriced { get; }
        public string Reason { get; }
    }

    public interface IProbeNativeSale
    {
        ProbeNativeOutcome Sell(IReadOnlyList<ProbeLine> lines);
        void Undo();
    }

    public sealed class PurchaseProbeResult
    {
        internal PurchaseProbeResult(string key, ProbeRefusal refusal, ProbeReceiptStatus receipt, ProbeQuote? quote, Money paid, string reason)
        { Key = key; Refusal = refusal; Receipt = receipt; Quote = quote; Paid = paid; Reason = reason; }
        public string Key { get; }
        public ProbeRefusal Refusal { get; }
        public ProbeReceiptStatus Receipt { get; }
        public ProbeQuote? Quote { get; }
        public Money Paid { get; }
        public string Reason { get; }
        public bool Bought => Refusal == ProbeRefusal.None && Paid > Money.Zero;
    }

    public static class PurchaseProbe
    {
        public const string Prefix = "persistentcity:sale-probe-v1:";
        public static bool IsReceiptKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

        public static Money Ceiling(Money requested, ResidentBudget wallet) => Money.Min(requested, HouseholdFinance.SpendingLimit(wallet));

        public static ProbeQuote Quote(IReadOnlyList<ProbeLine> basket, Money ceiling)
        {
            Money total = Money.Sum(basket.Select(l => l.Price));
            var refusal = basket.Count == 0 ? ProbeRefusal.Empty : total > ceiling ? ProbeRefusal.Ceiling : ProbeRefusal.None;
            return new ProbeQuote(refusal == ProbeRefusal.None ? basket.ToArray() : Array.Empty<ProbeLine>(), total, ceiling, refusal);
        }

        public static PurchaseProbeResult Run(PurchaseProbeRequest request, ResidentBudget? wallet, IDictionary<string, string> receipts, IProbeNativeSale native)
        {
            string key = request.Key;
            if (receipts.TryGetValue(key, out var saved))
                return new PurchaseProbeResult(key, ProbeRefusal.AlreadyRecorded, Status(saved), null, Money.Zero, "Receipt already saved for this resident, place and minute");
            if (wallet == null)
                return new PurchaseProbeResult(key, ProbeRefusal.Wallet, ProbeReceiptStatus.Missing, null, Money.Zero, "Resident has no wallet");
            var quote = Quote(request.Basket, Ceiling(request.Ceiling, wallet));
            if (quote.Refusal != ProbeRefusal.None)
                return new PurchaseProbeResult(key, quote.Refusal, ProbeReceiptStatus.Missing, quote, Money.Zero,
                    quote.Refusal == ProbeRefusal.Empty ? "Basket is empty" : "Basket " + quote.Total.ToCentsText() + " is above the ceiling " + quote.Ceiling.ToCentsText());
            if (!CanDebit(wallet, request.Kind, quote.Total))
                return new PurchaseProbeResult(key, ProbeRefusal.Wallet, ProbeReceiptStatus.Missing, quote, Money.Zero, "Wallet cannot take the debit");
            string identity = Identity(request, quote.Kept);
            receipts[key] = "pending|" + identity;
            ProbeNativeOutcome outcome;
            try { outcome = native.Sell(quote.Kept); }
            catch (Exception error) { return Reverse(key, identity, quote, receipts, native, "Native sale failed: " + error.Message); }
            if (!outcome.Sold)
            {
                receipts.Remove(key);
                return new PurchaseProbeResult(key, ProbeRefusal.Interrupted, ProbeReceiptStatus.Missing, quote, Money.Zero, outcome.Reason);
            }
            Money paid = Money.Sum(outcome.Paid.Select(l => l.Price));
            string note = WithinQuote(outcome.Paid, quote.Kept) && outcome.Unpriced == 0 ? "" : "Native paid lines differ from the quote";
            if (paid > quote.Total || paid > Money.Zero && !CanDebit(wallet, request.Kind, paid))
                return Reverse(key, identity, quote, receipts, native, "Booked order " + paid.ToCentsText() + " exceeds the quote or the wallet");
            if (paid == Money.Zero)
            {
                receipts[key] = "declined|" + identity;
                return new PurchaseProbeResult(key, ProbeRefusal.NativeDeclined, ProbeReceiptStatus.Declined, quote, Money.Zero, "Native price check refused every line");
            }
            try { Debit(wallet, request.Kind, paid); }
            catch (Exception error) { return Reverse(key, identity, quote, receipts, native, "Wallet debit failed: " + error.Message); }
            receipts[key] = "paid|" + paid.ToCentsText() + "|" + identity;
            return new PurchaseProbeResult(key, ProbeRefusal.None, ProbeReceiptStatus.Paid, quote, paid, note);
        }

        private static PurchaseProbeResult Reverse(string key, string identity, ProbeQuote quote, IDictionary<string, string> receipts, IProbeNativeSale native, string reason)
        {
            try { native.Undo(); }
            catch (Exception error)
            {
                receipts[key] = "uncertain|" + identity;
                return new PurchaseProbeResult(key, ProbeRefusal.Uncertain, ProbeReceiptStatus.Uncertain, quote, Money.Zero, reason + "; native restore failed: " + error.Message);
            }
            receipts.Remove(key);
            return new PurchaseProbeResult(key, ProbeRefusal.Reversed, ProbeReceiptStatus.Missing, quote, Money.Zero, reason + "; native books restored");
        }

        public static int Recover(IDictionary<string, string> receipts)
        {
            var pending = receipts.Where(p => IsReceiptKey(p.Key) && p.Value.StartsWith("pending|", StringComparison.Ordinal)).Select(p => p.Key).ToArray();
            foreach (var key in pending) receipts[key] = "abandoned|" + receipts[key].Substring("pending|".Length);
            return pending.Length;
        }

        public static ProbeReceiptStatus Status(string? value)
        {
            if (value == null) return ProbeReceiptStatus.Missing;
            int bar = value.IndexOf('|');
            switch (bar < 0 ? value : value.Substring(0, bar))
            {
                case "pending": return ProbeReceiptStatus.Pending;
                case "paid": return ProbeReceiptStatus.Paid;
                case "declined": return ProbeReceiptStatus.Declined;
                case "abandoned": return ProbeReceiptStatus.Abandoned;
                case "uncertain": return ProbeReceiptStatus.Uncertain;
                default: return ProbeReceiptStatus.Conflict;
            }
        }

        private static bool CanDebit(ResidentBudget wallet, ProbeKind kind, Money amount)
            => amount > Money.Zero && amount <= HouseholdFinance.SpendingLimit(wallet)
                && Money.TryAdd(kind == ProbeKind.Food ? wallet.PreparedFoodSpent : wallet.Ledger.Discretionary, amount, out _);

        private static void Debit(ResidentBudget wallet, ProbeKind kind, Money amount)
        {
            if (kind == ProbeKind.Food)
            {
                if (!HouseholdFinance.HoldPurchase(wallet, amount)) throw new InvalidOperationException("Food hold refused after a checked quote.");
                HouseholdFinance.ConfirmPurchase(wallet);
                return;
            }
            HouseholdFinance.CheckCash(wallet);
            var accounts = wallet.Ledger; accounts.Discretionary += amount;
            wallet.Balance -= amount; wallet.Ledger = accounts;
            HouseholdFinance.CheckCash(wallet);
        }

        private static bool WithinQuote(IReadOnlyList<ProbeLine> paid, IReadOnlyList<ProbeLine> quoted)
        {
            var open = quoted.ToList();
            foreach (var line in paid)
            {
                int at = open.FindIndex(q => q.ItemId == line.ItemId && q.Price == line.Price);
                if (at < 0) return false;
                open.RemoveAt(at);
            }
            return true;
        }

        private static string Identity(PurchaseProbeRequest request, IReadOnlyList<ProbeLine> lines)
        {
            var text = new StringBuilder();
            text.Append(request.Kind).Append('|').Append(Uri.EscapeDataString(request.ResidentId)).Append('|').Append(Uri.EscapeDataString(request.PlaceId));
            foreach (var line in lines) text.Append('|').Append(line.Price.ToCentsText()).Append(':').Append(Uri.EscapeDataString(line.ItemId));
            return text.ToString();
        }
    }
}
