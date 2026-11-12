using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistentCity.Core
{
    public enum PurchaseRefusal
    {
        None, Empty, Ceiling, Wallet, Unsupported, Closed, PlayerInside, VenueFull, VenueNotReady, AlreadyRecorded,
        Dropped, NativeDeclined, Interrupted, Reversed, Uncertain
    }

    public enum PurchaseIntentionKind { Need, Food }

    public readonly struct PurchaseIntention
    {
        private PurchaseIntention(PurchaseIntentionKind kind, string key) { Kind = kind; Key = key; }
        public static PurchaseIntention Need(string category)
            => new PurchaseIntention(PurchaseIntentionKind.Need, string.IsNullOrEmpty(category) ? throw new ArgumentException("Empty need.") : category);
        public static PurchaseIntention Food(string product)
            => new PurchaseIntention(PurchaseIntentionKind.Food, string.IsNullOrEmpty(product) ? throw new ArgumentException("Empty food choice.") : product);
        public PurchaseIntentionKind Kind { get; }
        public string Key { get; }
    }

    public readonly struct PurchaseLine
    {
        public PurchaseLine(string itemId, Money price, bool mandatory)
        {
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("Empty item.", nameof(itemId));
            if (price <= Money.Zero) throw new ArgumentOutOfRangeException(nameof(price));
            ItemId = itemId; Price = price; Mandatory = mandatory;
        }
        public string ItemId { get; }
        public Money Price { get; }
        public bool Mandatory { get; }
    }

    public sealed class PurchaseRequest
    {
        public PurchaseRequest(string residentId, string venueId, PurchaseIntention intention, Money ceiling, long minute)
        {
            if (string.IsNullOrEmpty(residentId) || string.IsNullOrEmpty(venueId)) throw new ArgumentException("A purchase needs a resident and a venue.");
            if (ceiling < Money.Zero) throw new ArgumentOutOfRangeException(nameof(ceiling));
            ResidentId = residentId; VenueId = venueId; Intention = intention; Ceiling = ceiling; Minute = minute;
        }
        public string ResidentId { get; }
        public string VenueId { get; }
        public PurchaseIntention Intention { get; }
        public Money Ceiling { get; }
        public long Minute { get; }
        public string Key => PurchaseContract.Prefix + Uri.EscapeDataString(ResidentId) + ":" + Uri.EscapeDataString(VenueId) + ":"
            + Minute.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class PurchaseQuote
    {
        internal PurchaseQuote(IReadOnlyList<PurchaseLine> kept, int dropped, Money total, Money ceiling, PurchaseRefusal refusal)
        { Kept = kept; Dropped = dropped; Total = total; Ceiling = ceiling; Refusal = refusal; }
        public IReadOnlyList<PurchaseLine> Kept { get; }
        public int Dropped { get; }
        public Money Total { get; }
        public Money Ceiling { get; }
        public PurchaseRefusal Refusal { get; }
    }

    public sealed class PurchaseResult
    {
        private PurchaseResult(string key, PurchaseRefusal refusal, IReadOnlyList<PurchaseLine> lines, Money total, PurchaseQuote? quote, string reason)
        { Key = key; Refusal = refusal; Lines = lines; Total = total; Quote = quote; Reason = reason; }
        internal static PurchaseResult Bought(string key, IReadOnlyList<PurchaseLine> lines, Money total, PurchaseQuote quote, string note)
            => new PurchaseResult(key, PurchaseRefusal.None, lines, total, quote, note);
        internal static PurchaseResult Refused(string key, PurchaseRefusal cause, PurchaseQuote? quote, string reason)
            => new PurchaseResult(key, cause, Array.Empty<PurchaseLine>(), Money.Zero, quote, reason);
        public string Key { get; }
        public bool IsBought => Refusal == PurchaseRefusal.None;
        public PurchaseRefusal Refusal { get; }
        public IReadOnlyList<PurchaseLine> Lines { get; }
        public Money Total { get; }
        public PurchaseQuote? Quote { get; }
        public string Reason { get; }
    }

    public enum PurchaseFamily { Unsupported, FullService, SelfService, Hairdresser, Gym, CinemaTheater, Nightclub }

    public sealed class PurchaseAdapter
    {
        internal PurchaseAdapter(PurchaseFamily family, bool supported, bool appendsEntranceFee, bool ticketFirst, string hook, string notReady)
        { Family = family; Supported = supported; AppendsEntranceFee = appendsEntranceFee; TicketFirst = ticketFirst; Hook = hook; NotReady = notReady; }
        public PurchaseFamily Family { get; }
        public bool Supported { get; }
        public bool AppendsEntranceFee { get; }
        public bool TicketFirst { get; }
        public string Hook { get; }
        public string NotReady { get; }
    }

    public static class PurchaseAdapters
    {
        private static readonly PurchaseAdapter[] Rows =
        {
            new PurchaseAdapter(PurchaseFamily.Unsupported, false, false, false, "none", "unsupported processor; stays native-owned"),
            new PurchaseAdapter(PurchaseFamily.FullService, true, true, false, "RetailBusinessSimulator.ProcessCustomer", ""),
            new PurchaseAdapter(PurchaseFamily.SelfService, true, true, false, "SelfServiceBusinessSimulator.ProcessCustomer", "no shopping containers; missing scales"),
            new PurchaseAdapter(PurchaseFamily.Hairdresser, true, true, false, "HairdresserBusinessSimulator.ProcessCustomer", "no stocked hair-care shelf or staffed station"),
            new PurchaseAdapter(PurchaseFamily.Gym, true, true, false, "GymBusinessSimulator.ProcessCustomer", ""),
            new PurchaseAdapter(PurchaseFamily.CinemaTheater, true, false, true, "CinemaTheaterBusinessSimulator.ProcessCustomer", "theatre without actors"),
            new PurchaseAdapter(PurchaseFamily.Nightclub, true, true, false, "NightclubBusinessSimulator.ProcessCustomer", "no staffed DJ booth"),
        };
        public static PurchaseAdapter For(PurchaseFamily family) => Rows[(int)family];
        public static IReadOnlyList<PurchaseAdapter> All => Rows;
    }

    public readonly struct PurchaseNativeOutcome
    {
        private PurchaseNativeOutcome(bool sold, PurchaseRefusal refusal, IReadOnlyList<PurchaseLine> paid, int unpriced, string reason)
        { Sold = sold; Refusal = refusal; Paid = paid; Unpriced = unpriced; Reason = reason; }
        public static PurchaseNativeOutcome Interrupted(string reason) => new PurchaseNativeOutcome(false, PurchaseRefusal.Interrupted, Array.Empty<PurchaseLine>(), 0, reason);
        public static PurchaseNativeOutcome Refused(PurchaseRefusal refusal, string reason) => new PurchaseNativeOutcome(false, refusal, Array.Empty<PurchaseLine>(), 0, reason);
        public static PurchaseNativeOutcome Processed(IReadOnlyList<PurchaseLine> paid, int unpriced = 0) => new PurchaseNativeOutcome(true, PurchaseRefusal.None, paid, unpriced, "");
        public bool Sold { get; }
        public PurchaseRefusal Refusal { get; }
        public IReadOnlyList<PurchaseLine> Paid { get; }
        public int Unpriced { get; }
        public string Reason { get; }
    }

    public interface IPurchaseNative
    {
        PurchaseFamily Family { get; }
        IReadOnlyList<PurchaseLine> Basket { get; }
        int ServedThisHour { get; }
        int CapacityThisHour { get; }
        int? NativeHourlyCount { get; }
        PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines);
        void Undo();
        void Commit();
    }

    public interface IPurchaseMarket
    {
        /// <summary>A cheap read: whether the venue is a player business this market sells for now. False keeps it native.</summary>
        bool Serves(string venueId);
        /// <summary>The venues <see cref="Serves"/> is true for now, for the impulse check while passing. Kept small: the
        /// player's own supported businesses.</summary>
        IReadOnlyList<string> Venues { get; }
        IPurchaseNative? Open(PurchaseRequest request, out PurchaseRefusal refusal, out string reason);
        IDictionary<string, string> Receipts { get; }
        void Guarded(Action unit);
    }

    public readonly struct PurchaseCalibration
    {
        public PurchaseCalibration(string venueId, long hour, int? nativeCount, int residentOrders)
        { VenueId = venueId; Hour = hour; NativeCount = nativeCount; ResidentOrders = residentOrders; }
        public string VenueId { get; }
        public long Hour { get; }
        public int? NativeCount { get; }
        public int ResidentOrders { get; }
    }

    public static class PurchaseContract
    {
        public const string Prefix = "persistentcity:purchase-v1:";
        public static bool IsReceiptKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

        public static Money Ceiling(Money requested, ResidentBudget wallet) => Money.Min(requested, HouseholdFinance.SpendingLimit(wallet));

        public static PurchaseQuote Quote(IReadOnlyList<PurchaseLine> basket, Money ceiling)
        {
            if (basket == null || basket.Count == 0) return new PurchaseQuote(Array.Empty<PurchaseLine>(), 0, Money.Zero, ceiling, PurchaseRefusal.Empty);
            var kept = basket.ToList();
            Money total = Money.Sum(kept.Select(l => l.Price));
            int dropped = 0;
            for (int i = kept.Count - 1; i >= 0 && total > ceiling; i--)
            {
                if (kept[i].Mandatory) continue;
                total -= kept[i].Price; kept.RemoveAt(i); dropped++;
            }
            if (total > ceiling) return new PurchaseQuote(Array.Empty<PurchaseLine>(), 0, Money.Sum(basket.Select(l => l.Price)), ceiling, PurchaseRefusal.Ceiling);
            return kept.Count == 0 ? new PurchaseQuote(Array.Empty<PurchaseLine>(), 0, Money.Sum(basket.Select(l => l.Price)), ceiling, PurchaseRefusal.Ceiling)
                : new PurchaseQuote(kept, dropped, total, ceiling, PurchaseRefusal.None);
        }

        public static IReadOnlyList<PurchaseLine> FoodBasket(string product, Money main, IReadOnlyList<FoodTicketLine>? extras)
        {
            var lines = new List<PurchaseLine>(1 + (extras?.Count ?? 0)) { new PurchaseLine(product, main, true) };
            if (extras != null) foreach (var extra in extras) lines.Add(new PurchaseLine(extra.ProductId, extra.Price, true));
            return lines;
        }

        public static IReadOnlyList<FoodTicketLine> KeptExtras(PurchaseQuote quote, IReadOnlyList<FoodTicketLine>? extras)
        {
            if (extras == null || extras.Count == 0 || quote.Refusal != PurchaseRefusal.None) return Array.Empty<FoodTicketLine>();
            int kept = quote.Kept.Count - 1;
            if (kept == extras.Count) return extras;
            var result = new FoodTicketLine[Math.Max(0, kept)];
            for (int i = 0; i < result.Length; i++) result[i] = extras[i];
            return result;
        }

        public static PurchaseResult Run(PurchaseRequest request, ResidentBudget? wallet, IDictionary<string, string> receipts, IPurchaseNative native)
        {
            if (request == null || receipts == null || native == null) throw new ArgumentNullException(request == null ? nameof(request) : receipts == null ? nameof(receipts) : nameof(native));
            string key = request.Key;
            if (receipts.TryGetValue(key, out _))
                return PurchaseResult.Refused(key, PurchaseRefusal.AlreadyRecorded, null, "Receipt already saved for this resident, venue and minute");
            if (!PurchaseAdapters.For(native.Family).Supported)
                return PurchaseResult.Refused(key, PurchaseRefusal.Unsupported, null, "Unsupported processor; stays native-owned");
            if (wallet == null) return PurchaseResult.Refused(key, PurchaseRefusal.Wallet, null, "Resident has no wallet");
            if (native.ServedThisHour >= native.CapacityThisHour)
                return PurchaseResult.Refused(key, PurchaseRefusal.VenueFull, null, "Venue served its capacity this hour");
            var quote = Quote(native.Basket, Ceiling(request.Ceiling, wallet));
            if (quote.Refusal != PurchaseRefusal.None)
                return PurchaseResult.Refused(key, quote.Refusal, quote, quote.Refusal == PurchaseRefusal.Empty ? "Basket is empty"
                    : "Mandatory lines " + quote.Total.ToCentsText() + " are above the ceiling " + quote.Ceiling.ToCentsText());
            var kind = request.Intention.Kind;
            if (!CanDebit(wallet, kind, quote.Total))
                return PurchaseResult.Refused(key, PurchaseRefusal.Wallet, quote, "Wallet cannot take the debit");
            string identity = Identity(request, quote.Kept);
            receipts[key] = "pending|" + identity;
            PurchaseNativeOutcome outcome;
            try { outcome = native.Sell(quote.Kept); }
            catch (Exception error) { return Reverse(key, identity, quote, receipts, native, "Native sale failed: " + error.Message); }
            if (!outcome.Sold)
            {
                receipts.Remove(key);
                return PurchaseResult.Refused(key, outcome.Refusal == PurchaseRefusal.None ? PurchaseRefusal.Interrupted : outcome.Refusal, quote, outcome.Reason);
            }
            Money paid = Money.Sum(outcome.Paid.Select(l => l.Price));
            if (!WithinQuote(outcome.Paid, quote.Kept) || paid > quote.Total || paid > Money.Zero && !CanDebit(wallet, kind, paid))
                return Reverse(key, identity, quote, receipts, native, "Booked order " + paid.ToCentsText() + " is outside the quote or the wallet");
            if (paid == Money.Zero)
            {
                receipts[key] = "declined|" + identity;
                return PurchaseResult.Refused(key, PurchaseRefusal.NativeDeclined, quote, "Native price check refused every line");
            }
            try { Debit(wallet, kind, paid); }
            catch (Exception error) { return Reverse(key, identity, quote, receipts, native, "Wallet debit failed: " + error.Message); }
            receipts[key] = "paid|" + paid.ToCentsText() + "|" + identity;
            string note = outcome.Unpriced == 0 ? "" : "Native paid lines the mod cannot price were not charged";
            try { native.Commit(); }
            catch (Exception error) { note += (note.Length == 0 ? "" : "; ") + "Native follow-up failed: " + error.Message; }
            return PurchaseResult.Bought(key, outcome.Paid.ToArray(), paid, quote, note);
        }

        private static PurchaseResult Reverse(string key, string identity, PurchaseQuote quote, IDictionary<string, string> receipts, IPurchaseNative native, string reason)
        {
            try { native.Undo(); }
            catch (Exception error)
            {
                receipts[key] = "uncertain|" + identity;
                return PurchaseResult.Refused(key, PurchaseRefusal.Uncertain, quote, reason + "; native restore failed: " + error.Message);
            }
            receipts.Remove(key);
            return PurchaseResult.Refused(key, PurchaseRefusal.Reversed, quote, reason + "; native books restored");
        }

        public static int Recover(IDictionary<string, string> receipts)
        {
            var pending = receipts.Where(p => IsReceiptKey(p.Key) && p.Value.StartsWith("pending|", StringComparison.Ordinal)).Select(p => p.Key).ToArray();
            foreach (var key in pending) receipts[key] = "abandoned|" + receipts[key].Substring("pending|".Length);
            return pending.Length;
        }

        public static string Building(string venueId)
            => venueId.EndsWith(":leisure", StringComparison.Ordinal) ? venueId.Substring(0, venueId.Length - ":leisure".Length) + ":shop" : venueId;

        public static Dictionary<string, int> ServedInHour(IDictionary<string, string> receipts, long hour)
        {
            var served = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in receipts)
            {
                if (!IsReceiptKey(pair.Key)) continue;
                string status = pair.Value.Length > 0 && pair.Value.IndexOf('|') > 0 ? pair.Value.Substring(0, pair.Value.IndexOf('|')) : pair.Value;
                if (status != "paid" && status != "declined" && status != "uncertain") continue;
                var parts = pair.Key.Substring(Prefix.Length).Split(':');
                if (parts.Length != 3 || !long.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long minute)
                    || minute / 60 != hour) continue;
                string building = Building(Uri.UnescapeDataString(parts[1]));
                served.TryGetValue(building, out int count); served[building] = count + 1;
            }
            return served;
        }

        public static int Prune(IDictionary<string, string> receipts, int day)
        {
            var old = new List<string>();
            foreach (var pair in receipts)
            {
                if (!IsReceiptKey(pair.Key) || pair.Value.StartsWith("uncertain|", StringComparison.Ordinal)) continue;
                int colon = pair.Key.LastIndexOf(':');
                if (colon > 0 && long.TryParse(pair.Key.Substring(colon + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long minute)
                    && minute / 1440 < day) old.Add(pair.Key);
            }
            foreach (var key in old) receipts.Remove(key);
            return old.Count;
        }

        private static bool CanDebit(ResidentBudget wallet, PurchaseIntentionKind kind, Money amount)
            => amount > Money.Zero && amount <= HouseholdFinance.SpendingLimit(wallet)
                && Money.TryAdd(kind == PurchaseIntentionKind.Food ? wallet.PreparedFoodSpent : wallet.Ledger.Discretionary, amount, out _);

        private static void Debit(ResidentBudget wallet, PurchaseIntentionKind kind, Money amount)
        {
            if (kind == PurchaseIntentionKind.Food)
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

        private static bool WithinQuote(IReadOnlyList<PurchaseLine> paid, IReadOnlyList<PurchaseLine> quoted)
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

        private static string Identity(PurchaseRequest request, IReadOnlyList<PurchaseLine> lines)
        {
            var text = new StringBuilder();
            text.Append(request.Intention.Kind).Append('|').Append(Uri.EscapeDataString(request.Intention.Key)).Append('|')
                .Append(Uri.EscapeDataString(request.ResidentId)).Append('|').Append(Uri.EscapeDataString(request.VenueId));
            foreach (var line in lines) text.Append('|').Append(line.Price.ToCentsText()).Append(':').Append(Uri.EscapeDataString(line.ItemId));
            return text.ToString();
        }
    }
}
