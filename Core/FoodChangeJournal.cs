using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public sealed class FoodChangeJournal
    {
        public const int HoursEdges = 7 * 64 * 2;
        private readonly EconomicLedger ledger;
        private readonly FoodMemoryStore references;
        private readonly Dictionary<string, ShopObservation> shops = new Dictionary<string, ShopObservation>(StringComparer.Ordinal);
        public FoodChangeJournal(FoodCommerceState food, EconomicLedger ledger)
        {
            this.ledger = ledger; references = new FoodMemoryStore(food);
            foreach (var row in ledger.Shops)
            {
                shops.Add(references.Identity(row.BusinessRef), row);
                Array.Clear(row.HourCounts, 0, row.HourCounts.Length);
                for (int i = 0; i < row.Hours.Length; i += 2) if (row.Hours[i] != 1441) row.HourCounts[i / 128]++;
                row.HoursHash = HashHours(row.Hours);
            }
        }
        public ShopObservation? Find(string business) => shops.TryGetValue(business, out var row) ? row : null;
        public void Clear(string business)
        {
            if (!shops.TryGetValue(business, out var row)) return;
            shops.Remove(business); ledger.Shops.Remove(row);
        }
        private ShopObservation? Get(string business, string place)
        {
            if (shops.TryGetValue(business, out var row)) return row;
            if (ledger.Shops.Count >= EconomicLedger.MaximumShops) return null;
            int slot = references.RegisterCampaign(business, place); if (slot < 0) return null;
            row = new ShopObservation { BusinessRef = slot }; shops.Add(business, row);
            int at = 0;
            while (at < ledger.Shops.Count && ledger.Shops[at].BusinessRef < slot) at++;
            ledger.Shops.Insert(at, row);
            return row;
        }
        private static void Add(ShopObservation row, int minute, ShopChangeKind kind, int subject, string? product, long before, long after)
        {
            if (before == after) return;
            for (int i = row.Changes.Count - 1; i >= 0 && row.Changes[i].ObservedMinute == minute; i--)
            {
                var seen = row.Changes[i];
                if (seen.Kind == kind && seen.Subject == subject && seen.ProductId == product && seen.Before == before && seen.After == after) return;
            }
            if (row.Changes.Count == EconomicLedger.ChangesPerShop) { row.Changes.RemoveAt(0); row.Evicted = checked(row.Evicted + 1); }
            row.Changes.Add(new ShopChange { ObservedMinute = minute, Kind = kind, Subject = subject, ProductId = product, Before = before, After = after });
        }
        public void ObserveSupply(string business, string place, int minute, ShopObservedOffer[] offers, int count, int staffedCounters,
            bool hasSupplyImage = true)
        {
            if (count < 1 || count > 8 || count > offers.Length || staffedCounters < 0) throw new ArgumentException("Invalid observation.");
            var row = Get(business, place); if (row == null) return;
            bool changed = row.Supply.Length != count;
            if (hasSupplyImage && row.StaffedCounters != staffedCounters)
            {
                if (row.StaffedCounters >= 0) Add(row, minute, ShopChangeKind.StaffedCounters, 0, null, row.StaffedCounters, staffedCounters);
                row.StaffedCounters = staffedCounters; changed = true;
            }
            ShopObservedOffer[]? next = null;
            int old = 0;
            for (int i = 0; i < count; i++)
            {
                var after = offers[i];
                if (i > 0 && StringComparer.Ordinal.Compare(offers[i - 1].ProductId, after.ProductId) >= 0) throw new ArgumentException("Unordered observed menu.");
                while (old < row.Supply.Length && StringComparer.Ordinal.Compare(row.Supply[old].ProductId, after.ProductId) < 0) old++;
                bool known = old < row.Supply.Length && row.Supply[old].ProductId == after.ProductId;
                var before = known ? row.Supply[old] : default;
                after.Stock = hasSupplyImage ? (after.Stock > 0 ? 1 : 0) : known ? before.Stock : -1;
                if (known)
                {
                    Add(row, minute, ShopChangeKind.Price, 0, after.ProductId, before.PriceUnits, after.PriceUnits);
                    if (before.Stock >= 0 && after.Stock >= 0) Add(row, minute, ShopChangeKind.Stock, 0, after.ProductId, before.Stock, after.Stock);
                }
                if (!known || old != i || before.PriceUnits != after.PriceUnits || before.Stock != after.Stock) changed = true;
                if (changed && next == null)
                {
                    next = new ShopObservedOffer[count];
                    if (i > 0) Array.Copy(row.Supply, next, i);
                }
                if (next != null) next[i] = after;
            }
            if (!changed) return;
            row.Supply = next!; Fingerprint(row);
        }
        public void ObserveHours(string business, string place, int minute, IReadOnlyList<OpeningWindow> hours)
        {
            if (hours.Count > 64) throw new ArgumentException("Observed hours exceed their bound.");
            var row = Get(business, place); if (row == null) return;
            Span<int> edges = stackalloc int[HoursEdges];
            Span<int> slots = stackalloc int[7]; slots.Clear();
            for (int h = 0; h < hours.Count; h++)
            {
                var w = hours[h];
                if (w.Day < 0 || w.Day > 6 || slots[w.Day] >= 64) throw new ArgumentException("Invalid observed hours.");
                int first = w.Day * 128, slot = slots[w.Day]++;
                while (slot > 0 && (edges[first + (slot - 1) * 2] > w.FromMinute
                    || edges[first + (slot - 1) * 2] == w.FromMinute && edges[first + (slot - 1) * 2 + 1] > w.ToMinute))
                {
                    edges[first + slot * 2] = edges[first + (slot - 1) * 2];
                    edges[first + slot * 2 + 1] = edges[first + (slot - 1) * 2 + 1]; slot--;
                }
                int at = first + slot * 2; edges[at] = w.FromMinute; edges[at + 1] = w.ToMinute;
            }
            bool same = row.Hours.Length == HoursEdges;
            for (int day = 0; day < 7; day++)
            {
                if (row.HourCounts[day] != slots[day]) same = false;
                if (row.Hours.Length != 0) for (int i = 0; i < slots[day] * 2; i++)
                    if (row.Hours[day * 128 + i] != edges[day * 128 + i]) same = false;
            }
            if (same) return;
            var next = new int[HoursEdges]; Array.Fill(next, 1441);
            for (int day = 0; day < 7; day++)
            {
                for (int i = 0; i < slots[day] * 2; i++) next[day * 128 + i] = edges[day * 128 + i];
                if (row.Hours.Length != 0) for (int i = 0; i < Math.Max(row.HourCounts[day], slots[day]) * 2; i++)
                {
                    int at = day * 128 + i; Add(row, minute, ShopChangeKind.Hours, at, null, row.Hours[at], next[at]);
                }
                row.HourCounts[day] = slots[day];
            }
            row.Hours = next; row.HoursHash = HashHours(next); Fingerprint(row);
        }
        private static void Fingerprint(ShopObservation row)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var text = new StringBuilder().Append(row.StaffedCounters.ToString(culture)).Append(';');
            foreach (var o in row.Supply) text.Append(o.ProductId).Append(':').Append(o.PriceUnits.ToString(culture)).Append(':').Append(o.Stock.ToString(culture)).Append(';');
            text.Append(row.HoursHash); row.Fingerprint = Hash(text.ToString());
        }
        private static string HashHours(int[] hours)
        {
            if (hours.Length == 0) return "";
            var image = new byte[hours.Length * 2];
            for (int i = 0; i < hours.Length; i++) { image[i * 2] = (byte)hours[i]; image[i * 2 + 1] = (byte)(hours[i] >> 8); }
            return Hash(image);
        }
        private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
        private static string Hash(byte[] value)
        {
            using var hash = SHA256.Create(); var bytes = hash.ComputeHash(value);
            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";
            var result = new char[20];
            for (int block = 0; block < 4; block++)
            {
                int at = block * 4;
                uint n = (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];
                for (int i = block * 5 + 4; i >= block * 5; i--) { result[i] = alphabet[(int)(n % 85)]; n /= 85; }
            }
            return new string(result);
        }
    }
}
