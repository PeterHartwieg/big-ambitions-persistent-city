using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public enum FoodReceiptStatus { Missing, Pending, Paid, Conflict }

    public static class FoodSaleReceipts
    {
        public const string Prefix = "persistentcity:food-sale-v1:";
        public static bool IsReceiptKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);
        private static string Key(ResidentMeal m) => Prefix + m.IntentId;
        private static string Identity(ResidentMeal m) => m.Price.ToCentsText()
            + "|" + Uri.EscapeDataString(m.BusinessId) + "|" + Uri.EscapeDataString(m.ProductId);
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
                if (colon <= 0 || !PositiveCents(fields[i].Substring(0, colon)) || !CanonicalText(fields[i].Substring(colon + 1))) return false;
            }
            return true;
        }
        public static FoodReceiptStatus ReadStatus(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) return FoodReceiptStatus.Missing;
            if (!data.TryGetValue(Key(meal), out var value)) return FoodReceiptStatus.Missing;
            string identity = Identity(meal);
            if (Matches(value, "paid|" + identity)) return FoodReceiptStatus.Paid;
            if (Matches(value, "pending|" + identity)) return FoodReceiptStatus.Pending;
            return FoodReceiptStatus.Conflict;
        }
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal)
        {
            var status = ReadStatus(data, meal);
            if (status == FoodReceiptStatus.Missing) return false;
            if (status == FoodReceiptStatus.Paid) return true;
            throw new InvalidOperationException("Native food receipt is uncertain or conflicts with this intention; no retry.");
        }
        public static bool TryReadPaidTicket(IDictionary<string, string> data, ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            total = Money.Zero; extras = Array.Empty<FoodTicketLine>();
            if (meal.TargetKind != FoodAlternativeKind.Player || meal.Price <= Money.Zero) return false;
            if (!data.TryGetValue(Key(meal), out var value) || value == null) return false;
            var parts = value.Split('|');
            if (parts.Length < 4 || parts[0] != "paid" || !PositiveCents(parts[1])
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
        public static bool AlreadyPaid(IDictionary<string, string> data, ResidentMeal meal, IReadOnlyList<FoodTicketLine> extras)
        {
            if (!AlreadyPaid(data, meal)) return false;
            if (data.TryGetValue(Key(meal), out var value) && value == "paid|" + Identity(meal) + Lines(extras)) return true;
            throw new InvalidOperationException("Native food receipt conflicts with this ticket; no retry.");
        }
        public static void Begin(IDictionary<string, string> data, ResidentMeal meal, IReadOnlyList<FoodTicketLine>? extras = null)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) throw new InvalidOperationException("Modeled food has no native receipt.");
            if (ReadStatus(data, meal) != FoodReceiptStatus.Missing) throw new InvalidOperationException("Native food intention already has a receipt.");
            data.Add(Key(meal), "pending|" + Identity(meal) + Lines(extras));
        }
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
        public static void Cancel(IDictionary<string, string> data, ResidentMeal meal)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player || ReadStatus(data, meal) != FoodReceiptStatus.Pending
                || !data.TryGetValue(Key(meal), out var value) || value != "pending|" + Identity(meal))
                throw new InvalidOperationException("Native food receipt changed during cancellation; outcome is uncertain; do not retry.");
            data.Remove(Key(meal));
        }
        public static void Prune(IDictionary<string, string> data, int nativeDay,
            IEnumerable<string>? unresolvedIntentIds = null)
        {
            var protectedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (unresolvedIntentIds != null)
                foreach (var id in unresolvedIntentIds) protectedKeys.Add(Prefix + id);
            var remove = new List<string>();
            foreach (var pair in data)
            {
                if (!IsReceiptKey(pair.Key) || protectedKeys.Contains(pair.Key) || !ConfirmedValue(pair.Value)) continue;
                if (!KnownDay(pair.Key, out int day)) continue;
                if (day < nativeDay - 1) remove.Add(pair.Key);
            }
            foreach (var key in remove) data.Remove(key);
        }
        private static bool ConfirmedValue(string value)
        {
            if (value == null) return false;
            var parts = value.Split('|');
            if (parts.Length < 4 || parts[0] != "paid" || !PositiveCents(parts[1])) return false;
            return CanonicalText(parts[2]) && CanonicalText(parts[3]) && ExtraFields(parts, 4);
        }
        private static bool PositiveCents(string text)
            => decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal cents) && cents > 0
                && cents.ToString("0.############################", CultureInfo.InvariantCulture) == text;
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
