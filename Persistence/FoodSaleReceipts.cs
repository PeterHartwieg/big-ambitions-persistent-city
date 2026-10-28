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
        private const string Prefix = "persistentcity:food-sale-v1:";
        private static string Key(ResidentMeal m) => Prefix + m.IntentId;
        private static string Identity(ResidentMeal m) => m.PriceCents.ToString(CultureInfo.InvariantCulture)
            + "|" + Uri.EscapeDataString(m.BusinessId) + "|" + Uri.EscapeDataString(m.ProductId);
        /// <summary>Missing is absence of evidence, never proof that a native purchase failed.
        /// Only Paid confirms this exact intention, business, product and agreed price.</summary>
        public static FoodReceiptStatus ReadStatus(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (!data.TryGetValue(Key(meal), out var value)) return FoodReceiptStatus.Missing;
            string identity = Identity(meal);
            if (value == "paid|" + identity) return FoodReceiptStatus.Paid;
            if (value == "pending|" + identity) return FoodReceiptStatus.Pending;
            return FoodReceiptStatus.Conflict;
        }
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal)
        {
            var status = ReadStatus(data, meal);
            if (status == FoodReceiptStatus.Missing) return false;
            if (status == FoodReceiptStatus.Paid) return true;
            throw new InvalidOperationException("Native food receipt is uncertain or conflicts with this intention; no retry.");
        }
        public static void Begin(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (data.ContainsKey(Key(meal))) throw new InvalidOperationException("Native food intention already has a receipt.");
            data.Add(Key(meal), "pending|" + Identity(meal));
        }
        public static void Confirm(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (!data.TryGetValue(Key(meal), out var value) || value != "pending|" + Identity(meal))
                throw new InvalidOperationException("Native food receipt changed during checkout.");
            data[Key(meal)] = "paid|" + Identity(meal);
        }
        /// <summary>Expire only known confirmed receipts outside the native checkout horizon.
        /// The caller protects intention IDs whose population-side outcome is still unresolved,
        /// including a paid native receipt whose matching held funds have not yet reconciled.</summary>
        public static void Prune(IDictionary<string, string> data, int nativeDay,
            IEnumerable<string>? unresolvedIntentIds = null)
        {
            var protectedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (unresolvedIntentIds != null)
                foreach (var id in unresolvedIntentIds) protectedKeys.Add(Prefix + id);
            var remove = new List<string>();
            foreach (var pair in data)
            {
                if (!pair.Key.StartsWith(Prefix, StringComparison.Ordinal) || protectedKeys.Contains(pair.Key)
                    || !ConfirmedValue(pair.Value)) continue;
                if (!KnownDay(pair.Key, out int day)) continue;
                if (day < nativeDay - 1) remove.Add(pair.Key);
            }
            foreach (var key in remove) data.Remove(key);
        }
        private static bool ConfirmedValue(string value)
        {
            // Unknown or damaged markers are evidence to investigate, not garbage to erase.
            if (value == null) return false;
            var parts = value.Split('|');
            if (parts.Length != 4 || parts[0] != "paid"
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int cents) || cents <= 0
                || cents.ToString(CultureInfo.InvariantCulture) != parts[1]) return false;
            return CanonicalText(parts[2]) && CanonicalText(parts[3]);
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
