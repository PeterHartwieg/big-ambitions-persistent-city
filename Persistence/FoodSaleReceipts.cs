using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public enum FoodReceiptStatus { Missing, Pending, Paid, Conflict }

    /// <summary>Native save-side idempotency markers. Kept with native orders/stock even if the
    /// population's save callback stops. Pending and unresolved evidence never expires merely
    /// because the clock advanced. No separate disk transaction or household ledger.</summary>
    public static class FoodSaleReceipts
    {
        public const string Prefix = "persistentcity:food-sale-v2:";
        public const string LegacyPrefix = "persistentcity:food-sale-v1:";
        public static bool IsReceiptKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal) || key.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        private static string Key(ResidentMeal m) => Prefix + m.IntentId;
        // Exact canonical cents: the same text the earlier decimal-cents identity wrote for the same amount.
        private static string Identity(ResidentMeal m) => m.Price.ToCentsText()
            + "|" + Uri.EscapeDataString(m.BusinessId) + "|" + Uri.EscapeDataString(m.ProductId);
        /// <summary>F6j: a ticket's extras follow the main identity as "|cents:product" fields, at most three. The
        /// leading price is then the ticket total. A one-product receipt is unchanged.</summary>
        private static string Lines(IReadOnlyList<FoodTicketLine>? extras)
        {
            if (extras == null || extras.Count == 0) return "";
            if (extras.Count > FoodMealExtras.MaximumExtras) throw new InvalidOperationException("Too many meal extras for one receipt.");
            var text = new System.Text.StringBuilder();
            foreach (var extra in extras)
            {
                if (extra.Price <= Money.Zero || extra.ProductId.Length == 0) throw new InvalidOperationException("Invalid meal extra.");
                text.Append('|').Append(extra.Price.ToCentsText()).Append(':').Append(Uri.EscapeDataString(extra.ProductId));
            }
            return text.ToString();
        }
        private static bool Matches(string value, string head)
            => value == head || value.StartsWith(head + "|", StringComparison.Ordinal) && ExtraFields(value.Substring(head.Length + 1).Split('|'), 0);
        private static bool ExtraFields(string[] fields, int from)
        {
            if (fields.Length - from > FoodMealExtras.MaximumExtras) return false;
            for (int i = from; i < fields.Length; i++)
            {
                int colon = fields[i].IndexOf(':');
                if (colon <= 0 || !PositiveCents(fields[i].Substring(0, colon), false) || !CanonicalText(fields[i].Substring(colon + 1))) return false;
            }
            return true;
        }
        /// <summary>Missing is absence of evidence, never proof that a native purchase failed.
        /// Only Paid confirms this exact intention, business, product and agreed price (the ticket total).</summary>
        public static FoodReceiptStatus ReadStatus(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) return FoodReceiptStatus.Missing;
            bool current = data.TryGetValue(Key(meal), out var value);
            bool legacy = data.TryGetValue(LegacyPrefix + meal.IntentId, out var oldValue);
            if (current && legacy) return FoodReceiptStatus.Conflict;
            if (!current && !legacy) return FoodReceiptStatus.Missing;
            if (legacy)
            {
                if (!meal.Price.IsWholeCents()) return FoodReceiptStatus.Conflict;
                value = oldValue;
            }
            string identity = Identity(meal);
            if (value == "paid|" + identity || !legacy && Matches(value, "paid|" + identity)) return FoodReceiptStatus.Paid;
            if (value == "pending|" + identity || !legacy && Matches(value, "pending|" + identity)) return FoodReceiptStatus.Pending;
            return FoodReceiptStatus.Conflict;
        }
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal)
        {
            var status = ReadStatus(data, meal);
            if (status == FoodReceiptStatus.Missing) return false;
            if (status == FoodReceiptStatus.Paid) return true;
            throw new InvalidOperationException("Native food receipt is uncertain or conflicts with this intention; no retry.");
        }
        /// <summary>F6j reconciliation without a checkout replay: a paid receipt for this intention, business and main
        /// whose sold total is at most the meal's held Price (its planned total). Returns the sold total and the sold
        /// extras, so the caller can return unsold funds and record every sold line. False for any other evidence.</summary>
        public static bool TryReadPaidTicket(IDictionary<string, string> data, ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            total = Money.Zero; extras = Array.Empty<FoodTicketLine>();
            if (meal.TargetKind != FoodAlternativeKind.Player || meal.Price <= Money.Zero) return false;
            if (data.ContainsKey(LegacyPrefix + meal.IntentId))
            {
                if (ReadStatus(data, meal) != FoodReceiptStatus.Paid) return false;
                total = meal.Price; return true; // legacy receipts are always one product
            }
            if (!data.TryGetValue(Key(meal), out var value) || value == null) return false;
            var parts = value.Split('|');
            if (parts.Length < 4 || parts[0] != "paid" || !PositiveCents(parts[1], false)
                || parts[2] != Uri.EscapeDataString(meal.BusinessId) || parts[3] != Uri.EscapeDataString(meal.ProductId)
                || !ExtraFields(parts, 4) || !Money.TryParseCents(parts[1], Money.CentBits, out var paid)) return false;
            var lines = new List<FoodTicketLine>();
            var main = paid;
            for (int i = 4; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(':');
                if (!Money.TryParseCents(parts[i].Substring(0, colon), Money.CentBits, out var price)) return false;
                lines.Add(new FoodTicketLine(Uri.UnescapeDataString(parts[i].Substring(colon + 1)), price));
                if (!Money.TryAdd(main, -price, out main)) return false;
            }
            if (paid > meal.Price || main <= Money.Zero) return false;
            total = paid; extras = lines; return true;
        }
        /// <summary>F6j: true only for a paid receipt of exactly this planned ticket; any other evidence throws.</summary>
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal, IReadOnlyList<FoodTicketLine> extras)
        {
            if (!AlreadyPaid(data, meal)) return false;
            if (data.TryGetValue(Key(meal), out var value) && value == "paid|" + Identity(meal) + Lines(extras)) return true;
            throw new InvalidOperationException("Native food receipt conflicts with this ticket; no retry.");
        }
        /// <summary><paramref name="extras"/> are the planned ticket extras; meal.Price is then the planned total.</summary>
        public static void Begin(IDictionary<string, string> data, ResidentMeal meal, IReadOnlyList<FoodTicketLine>? extras = null)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) throw new InvalidOperationException("Modeled food has no native receipt.");
            if (ReadStatus(data, meal) != FoodReceiptStatus.Missing) throw new InvalidOperationException("Native food intention already has a receipt.");
            data.Add(Key(meal), "pending|" + Identity(meal) + Lines(extras));
        }
        /// <summary>F6j: confirm the exact planned pending ticket as the lines that actually sold. The paid total is
        /// the main price plus the sold extras; the caller books that total.</summary>
        public static void Confirm(IDictionary<string, string> data, ResidentMeal meal, Money mainPrice,
            IReadOnlyList<FoodTicketLine> planned, IReadOnlyList<FoodTicketLine> sold)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) throw new InvalidOperationException("Modeled food has no native receipt.");
            if (!data.TryGetValue(Key(meal), out var value) || value != "pending|" + Identity(meal) + Lines(planned))
                throw new InvalidOperationException("Native food receipt changed during checkout.");
            var total = mainPrice;
            foreach (var line in sold) total += line.Price;
            data[Key(meal)] = "paid|" + total.ToCentsText() + "|" + Uri.EscapeDataString(meal.BusinessId)
                + "|" + Uri.EscapeDataString(meal.ProductId) + Lines(sold);
        }
        public static void Confirm(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) throw new InvalidOperationException("Modeled food has no native receipt.");
            if (!data.TryGetValue(Key(meal), out var value) || value != "pending|" + Identity(meal))
                throw new InvalidOperationException("Native food receipt changed during checkout.");
            data[Key(meal)] = "paid|" + Identity(meal);
        }
        /// <summary>Only a seller-confirmed refusal before effects can cancel this exact pending identity.</summary>
        public static void Cancel(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player || ReadStatus(data, meal) != FoodReceiptStatus.Pending
                || !data.TryGetValue(Key(meal), out var value) || value != "pending|" + Identity(meal))
                throw new InvalidOperationException("Native food receipt changed during cancellation; outcome is uncertain; do not retry.");
            data.Remove(Key(meal));
        }
        /// <summary>Expire only known confirmed receipts outside the native checkout horizon.
        /// The caller protects intention IDs whose population-side outcome is still unresolved,
        /// including a paid native receipt whose matching held funds have not yet reconciled.</summary>
        public static void Prune(IDictionary<string, string> data, int nativeDay,
            IEnumerable<string>? unresolvedIntentIds = null)
        {
            var protectedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (unresolvedIntentIds != null)
                foreach (var id in unresolvedIntentIds) { protectedKeys.Add(Prefix + id); protectedKeys.Add(LegacyPrefix + id); }
            var remove = new List<string>();
            foreach (var pair in data)
            {
                if (!IsReceiptKey(pair.Key) || protectedKeys.Contains(pair.Key)
                    || !ConfirmedValue(pair.Value, pair.Key.StartsWith(LegacyPrefix, StringComparison.Ordinal))) continue;
                string other = (pair.Key.StartsWith(Prefix, StringComparison.Ordinal) ? LegacyPrefix : Prefix)
                    + pair.Key.Substring(Prefix.Length);
                if (data.ContainsKey(other)) continue; // Two versions are conflicting evidence, never garbage.
                if (!KnownDay(pair.Key, out int day)) continue;
                if (day < nativeDay - 1) remove.Add(pair.Key);
            }
            foreach (var key in remove) data.Remove(key);
        }
        private static bool ConfirmedValue(string value, bool legacy)
        {
            // Unknown or damaged markers are evidence to investigate, not garbage to erase.
            if (value == null) return false;
            var parts = value.Split('|');
            if (parts.Length < 4 || legacy && parts.Length != 4 || parts[0] != "paid" || !PositiveCents(parts[1], legacy)) return false;
            return CanonicalText(parts[2]) && CanonicalText(parts[3]) && ExtraFields(parts, 4);
        }
        private static bool PositiveCents(string text, bool legacy)
        {
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal cents) || cents <= 0
                || cents.ToString("0.############################", CultureInfo.InvariantCulture) != text) return false;
            return !legacy || cents == decimal.Truncate(cents) && cents <= int.MaxValue;
        }
        private static bool CanonicalText(string value) => value.Length > 0 && value.Length <= 8192
            && Uri.EscapeDataString(Uri.UnescapeDataString(value)) == value;
        private static bool KnownDay(string key, out int day)
        {
            day = 0;
            var parts = key.Substring(Prefix.Length).Split(':');
            if ((parts.Length != 3 && parts.Length != 4) || parts[0] != "food" || !CanonicalText(parts[1])) return false;
            if (parts.Length == 4 && parts[2] != "breakfast" && parts[2] != "lunch" && parts[2] != "dinner") return false;
            string value = parts[parts.Length - 1];
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out day)
                && day.ToString(CultureInfo.InvariantCulture) == value;
        }
    }
}
