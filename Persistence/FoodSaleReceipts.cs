using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>Native save-side idempotency markers. Kept with native orders/stock even if the
    /// population's save callback stops. Only current/previous-day markers are needed: the adapter
    /// refuses sales from earlier native hours. No separate disk transaction or household ledger.</summary>
    public static class FoodSaleReceipts
    {
        private const string Prefix = "persistentcity:food-sale-v1:";
        private static string Key(ResidentMeal m) => Prefix + m.IntentId;
        private static string Identity(ResidentMeal m) => m.PriceCents.ToString(CultureInfo.InvariantCulture)
            + "|" + Uri.EscapeDataString(m.BusinessId) + "|" + Uri.EscapeDataString(m.ProductId);
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (!data.TryGetValue(Key(meal), out var value)) return false;
            if (value == "paid|" + Identity(meal)) return true;
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
        public static void Prune(IDictionary<string, string> data, int nativeDay)
        {
            var remove = new List<string>();
            foreach (var pair in data)
            {
                if (!pair.Key.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                int split = pair.Key.LastIndexOf(':');
                if (split < Prefix.Length || !int.TryParse(pair.Key.Substring(split + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int day)) continue; // Preserve unknown records.
                if (day < nativeDay - 1) remove.Add(pair.Key);
            }
            foreach (var key in remove) data.Remove(key);
        }
    }
}
