using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static string Cell(long n, int width)
        {
            Require(n >= 0, "Negative economic scalar.");
            var chars = new char[width];
            for (int i = width - 1; i >= 0; i--) { chars[i] = Digits[(int)(n % 36)]; n /= 36; }
            Require(n == 0, "Economic scalar exceeds its width.");
            return new string(chars);
        }
        private static long Cell(string text, int at, int width)
        {
            Require(at + width <= text.Length, "Incomplete economic scalar.");
            long n = 0;
            for (int i = at; i < at + width; i++)
            {
                int digit = Digits.IndexOf(text[i]);
                Require(digit >= 0, "Noncanonical economic scalar.");
                n = checked(n * 36 + digit);
            }
            return n;
        }
        private static bool EconomicScalar(Type t) => t == typeof(OwnerAccounts) || t == typeof(EmploymentAccrual)
            || t == typeof(MealPeriodCauses) || t == typeof(OutsideSpendingDay) || t == typeof(ShopChange);

        private static string EconomicText(object value, WireContext? context = null)
        {
            if (value is OwnerAccounts a) return Cell(a.EmploymentIncome.Units, 13) + Cell(a.Discretionary.Units, 13) + Cell(a.VisitWatermark, 6);
            if (value is EmploymentAccrual e) return Cell(e.AttendedMinutes, 3) + Cell(e.UnpostedWage.Units, 13)
                + Cell(e.MissMask, 3) + Cell(e.Day, 4) + Cell(e.Shift, 3);
            if (value is MealPeriodCauses c)
            {
                Require(c.Counts != null && c.Counts.Length == 16, "Meal-period causes require sixteen counters.");
                return string.Concat(c.Counts.Select(n => Cell(n, 3)));
            }
            if (value is ShopChange j) return Cell(j.ObservedMinute, 6) + Cell((int)j.Kind, 1) + Cell(j.Kind == ShopChangeKind.Price || j.Kind == ShopChangeKind.Stock ? context!.ProductIndex[j.ProductId!] - 1 : j.Subject, 2) + Cell(j.Before, 13) + Cell(j.After, 13);
            var d = (OutsideSpendingDay)value;
            return d.Day.ToString(CultureInfo.InvariantCulture) + "," + Wide(d.RetailUnits) + "," + Wide(d.RetailVisits) + "," + Wide(d.LeisureUnits) + "," + Wide(d.LeisureVisits);
        }
        private static string Wide(BigInteger n)
        {
            string s = n.ToString(CultureInfo.InvariantCulture);
            Require(n >= 0 && s.Length <= 26, "Outside-spending wide integer exceeds its bound.");
            return s;
        }
        private static BigInteger Wide(string s)
        {
            Require(s.Length >= 1 && s.Length <= 26 && (s.Length == 1 || s[0] != '0') && s.All(c => c >= '0' && c <= '9'), "Noncanonical outside-spending integer.");
            return BigInteger.Parse(s, CultureInfo.InvariantCulture);
        }
        private static object ParseEconomic(Type type, string s, WireContext? context = null)
        {
            if (type == typeof(OwnerAccounts))
            {
                Require(s.Length == 32, "Invalid owner-account width.");
                return new OwnerAccounts { EmploymentIncome = Money.FromUnits(Cell(s, 0, 13)), Discretionary = Money.FromUnits(Cell(s, 13, 13)), VisitWatermark = Cell(s, 26, 6) };
            }
            if (type == typeof(EmploymentAccrual))
            {
                Require(s.Length == 26, "Invalid employment-accrual width.");
                return new EmploymentAccrual { AttendedMinutes = (int)Cell(s, 0, 3), UnpostedWage = Money.FromUnits(Cell(s, 3, 13)), MissMask = (int)Cell(s, 16, 3), Day = (int)Cell(s, 19, 4), Shift = (int)Cell(s, 23, 3) };
            }
            if (type == typeof(MealPeriodCauses))
            {
                Require(s.Length == 48, "Invalid meal-period cause width.");
                var result = new MealPeriodCauses();
                for (int i = 0; i < 16; i++) result.Counts[i] = (int)Cell(s, i * 3, 3);
                return result;
            }
            if (type == typeof(ShopChange))
            {
                Require(s.Length == 35, "Invalid shop-change width.");
                var result = new ShopChange { ObservedMinute = checked((int)Cell(s, 0, 6)), Kind = (ShopChangeKind)Cell(s, 6, 1), Subject = (int)Cell(s, 7, 2), Before = Cell(s, 9, 13), After = Cell(s, 22, 13) };
                if ((result.Kind == ShopChangeKind.Price || result.Kind == ShopChangeKind.Stock) && context != null)
                {
                    Require(result.Subject < context.ProductNames.Length, "Invalid shop-change product.");
                    result.ProductId = context.ProductNames[result.Subject]; result.Subject = 0;
                }
                return result;
            }
            Require(s.Length <= 115, "Outside-spending row exceeds its bound.");
            var parts = s.Split(','); Require(parts.Length == 5, "Invalid outside-spending row width.");
            return new OutsideSpendingDay { Day = checked((int)Wide(parts[0])), RetailUnits = Wide(parts[1]), RetailVisits = Wide(parts[2]), LeisureUnits = Wide(parts[3]), LeisureVisits = Wide(parts[4]) };
        }
        private static void CheckEconomicScalar(JsonReader reader, Type type, TokenText text)
        {
            Require(reader.TokenType == JsonToken.String && text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Noncanonical economic encoding.");
            ParseEconomic(type, (string)reader.Value!);
        }
        private sealed class EconomicConverter : JsonConverter
        {
            private readonly WireContext context;
            public EconomicConverter(WireContext context) { this.context = context; }
            public override bool CanConvert(Type t) => EconomicScalar(t);
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => writer.WriteValue(EconomicText(value!, context));
            public override object ReadJson(JsonReader reader, Type type, object? value, JsonSerializer serializer) => ParseEconomic(type, (string)reader.Value!, context);
        }

        private static ShopObservation ReadObservation(JsonReader reader, TokenText? text, ref int records)
        {
            Require(reader.TokenType == JsonToken.StartArray && reader.Read() && reader.TokenType == JsonToken.String, "Expected observed-shop header.");
            string header = (string)reader.Value!;
            Require((header.Length == 3 || header.Length == 23) && (text == null || text.LiteralAsciiString((IJsonLineInfo)reader, header)), "Invalid observed-shop header.");
            var shop = new ShopObservation { BusinessRef = (int)Cell(header, 0, 3), Fingerprint = header.Substring(3) };
            Require(ValidFingerprint(shop.Fingerprint), "Invalid shop fingerprint.");
            Require(reader.Read() && reader.TokenType == JsonToken.StartArray, "Expected shop journal.");
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(shop.Changes.Count < EconomicLedger.ChangesPerShop, "Shop journal exceeds its bound.");
                Require(reader.TokenType == JsonToken.String && (text == null || text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!)), "Noncanonical shop change.");
                shop.Changes.Add((ShopChange)ParseEconomic(typeof(ShopChange), (string)reader.Value!)); Charge(ref records);
            }
            Require(reader.TokenType == JsonToken.EndArray, "Invalid journal.");
            Require(reader.Read() && reader.TokenType == JsonToken.String, "Missing observation state.");
            string state = (string)reader.Value!;
            Require(state.Length == 20 && (text == null || text.LiteralAsciiString((IJsonLineInfo)reader, state)), "Invalid observation state.");
            shop.Evicted = Cell(state, 0, 13); shop.StaffedCounters = checked((int)(Cell(state, 13, 7) - 1));
            Require(shop.StaffedCounters >= -1, "Invalid staffed counters.");
            Require(reader.Read() && reader.TokenType == JsonToken.StartArray, "Missing supply image.");
            var supply = new List<ShopObservedOffer>();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(reader.TokenType == JsonToken.String && supply.Count < 8, "Invalid supply image.");
                string cell = (string)reader.Value!;
                Require(cell.Length == 22 && (text == null || text.LiteralAsciiString((IJsonLineInfo)reader, cell)), "Invalid supply cell.");
                supply.Add(new ShopObservedOffer { ProductId = Cell(cell, 0, 2).ToString(CultureInfo.InvariantCulture), PriceUnits = Cell(cell, 2, 13), Stock = checked((int)(Cell(cell, 15, 7) - 1)) });
            }
            shop.Supply = supply.ToArray();
            Require(reader.Read() && reader.TokenType == JsonToken.String, "Missing hours image.");
            string hours = (string)reader.Value!;
            Require((hours.Length == 0 || hours.Length >= 14 && hours.Length <= 14 + 64 * 6)
                && (text == null || text.LiteralAsciiString((IJsonLineInfo)reader, hours)), "Invalid hours image.");
            if (hours.Length != 0)
            {
                shop.Hours = new int[FoodChangeJournal.HoursEdges]; Array.Fill(shop.Hours, 1441);
                int at = 14, windows = 0;
                for (int day = 0; day < 7; day++)
                {
                    int count = (int)Cell(hours, day * 2, 2); windows += count;
                    Require(count <= 64 && windows <= 64, "Observed hours exceed their bound.");
                    for (int slot = 0; slot < count; slot++)
                    {
                        int edge = day * 128 + slot * 2;
                        shop.Hours[edge] = (int)Cell(hours, at, 3); shop.Hours[edge + 1] = (int)Cell(hours, at + 3, 3); at += 6;
                        Require(shop.Hours[edge] < shop.Hours[edge + 1] && shop.Hours[edge + 1] <= 1440, "Invalid present hours window.");
                    }
                }
                Require(at == hours.Length, "Noncanonical sparse hours width.");
            }
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid observed-shop width.");
            return shop;
        }
        private static string ObservedHoursText(int[] hours)
        {
            if (hours.Length == 0) return "";
            var header = new StringBuilder(14); var edges = new StringBuilder();
            for (int day = 0; day < 7; day++)
            {
                int count = 0;
                for (int slot = 0; slot < 64 && hours[day * 128 + slot * 2] != 1441; slot++)
                {
                    int at = day * 128 + slot * 2; count++;
                    edges.Append(Cell(hours[at], 3)).Append(Cell(hours[at + 1], 3));
                }
                header.Append(Cell(count, 2));
            }
            return header.Append(edges).ToString();
        }
        private sealed class ObservationConverter : JsonConverter<ShopObservation>
        {
            private readonly WireContext context;
            public ObservationConverter(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, ShopObservation? shop, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                var escaping = writer.StringEscapeHandling;
                try { writer.StringEscapeHandling = StringEscapeHandling.Default; writer.WriteValue(Cell(shop!.BusinessRef, 3) + shop.Fingerprint); }
                finally { writer.StringEscapeHandling = escaping; }
                writer.WriteStartArray(); foreach (var change in shop.Changes) writer.WriteValue(EconomicText(change, context));
                writer.WriteEndArray();
                writer.WriteValue(Cell(shop.Evicted, 13) + Cell((long)shop.StaffedCounters + 1, 7));
                writer.WriteStartArray();
                foreach (var o in shop.Supply) writer.WriteValue(Cell(context.ProductIndex[o.ProductId] - 1, 2) + Cell(o.PriceUnits, 13) + Cell((long)o.Stock + 1, 7));
                writer.WriteEndArray(); writer.WriteValue(ObservedHoursText(shop.Hours));
                writer.WriteEndArray();
            }
            public override ShopObservation ReadJson(JsonReader reader, Type type, ShopObservation? value, bool hasValue, JsonSerializer serializer)
            {
                int ignored = 0;
                var shop = ReadObservation(reader, null, ref ignored);
                for (int i = 0; i < shop.Changes.Count; i++)
                {
                    var change = shop.Changes[i];
                    if (change.Kind == ShopChangeKind.Price || change.Kind == ShopChangeKind.Stock)
                    {
                        Require(change.Subject < context.ProductNames.Length, "Invalid shop-change product.");
                        change.ProductId = context.ProductNames[change.Subject]; change.Subject = 0; shop.Changes[i] = change;
                    }
                }
                for (int i = 0; i < shop.Supply.Length; i++)
                {
                    var offer = shop.Supply[i]; int index = int.Parse(offer.ProductId, CultureInfo.InvariantCulture);
                    Require(index < context.ProductNames.Length, "Invalid observed product.");
                    offer.ProductId = context.ProductNames[index]; shop.Supply[i] = offer;
                }
                return shop;
            }
        }

        private static int ValidateEconomy(CityState state)
        {
            var economy = state.Economy;
            Require(economy != null && economy.Outside != null && economy.Outside.Count <= EconomicLedger.Days
                && economy.Shops != null && economy.Shops.Count <= EconomicLedger.MaximumShops, "Economic ledger collection exceeds its bound.");
            int day = (int)(state.Minute / 1440), previous = -1, records = 0;
            foreach (var d in economy.Outside)
            {
                Require(d != null && d.Day > previous && d.Day <= day && d.Day >= Math.Max(0, day - EconomicLedger.Days + 1), "Outside-spending days are duplicate, unordered or out of range.");
                ValidatePair(d.RetailUnits, d.RetailVisits); ValidatePair(d.LeisureUnits, d.LeisureVisits);
                previous = d.Day; records++;
            }
            previous = -1;
            double latestObservation = state.Minute, nextMinute = Math.Floor(state.Minute) + 1;
            if (nextMinute <= 1e9 && state.Minute == AccountingClock.PreviousEpochTarget(nextMinute)) latestObservation = nextMinute;
            var references = state.Food?.BusinessReferences.ToDictionary(r => r.Slot);
            foreach (var shop in economy.Shops)
            {
                Require(shop != null && shop.BusinessRef > previous && references != null && references.ContainsKey(shop.BusinessRef), "Invalid or unordered observed-shop reference.");
                var reference = references![shop.BusinessRef];
                Require(state.Food!.Shops.Any(s => s.BusinessId == reference.BusinessId && s.PlaceId == reference.PlaceId), "Observed shop is not managed.");
                previous = shop.BusinessRef;
                Require(ValidFingerprint(shop.Fingerprint), "Invalid shop fingerprint.");
                Require(shop.Evicted >= 0 && shop.Supply != null && shop.Supply.Length <= 8 && shop.StaffedCounters >= -1
                    && (shop.Supply.Length != 0 || shop.StaffedCounters == -1), "Invalid observed supply.");
                string? priorProduct = null;
                foreach (var offer in shop.Supply)
                {
                    Require(offer.ProductId != null && state.ProductNames.Contains(offer.ProductId)
                        && (priorProduct == null || StringComparer.Ordinal.Compare(priorProduct, offer.ProductId) < 0)
                        && offer.PriceUnits > 0 && offer.PriceUnits <= Money.FromCents(Money.MaximumNativePriceCents).Units && offer.Stock >= -1 && offer.Stock <= 1, "Invalid observed offer.");
                    priorProduct = offer.ProductId;
                }
                Require(shop.Hours != null && (shop.Hours.Length == 0 || shop.Hours.Length == FoodChangeJournal.HoursEdges)
                    && shop.Hours.All(n => n >= 0 && n <= 1441), "Invalid observed hours.");
                int windowCount = 0;
                for (int weekday = 0; shop.Hours.Length != 0 && weekday < 7; weekday++)
                {
                    bool absent = false; int priorFrom = -1, priorTo = -1;
                    for (int slot = 0; slot < MaxOpeningWindows; slot++)
                    {
                        int edge = weekday * MaxOpeningWindows * 2 + slot * 2, from = shop.Hours[edge], to = shop.Hours[edge + 1];
                        if (from == 1441) { Require(to == 1441, "Unpaired absent hours edge."); absent = true; continue; }
                        Require(!absent && from < to && to <= 1440 && (from > priorFrom || from == priorFrom && to >= priorTo), "Noncanonical observed hours window.");
                        priorFrom = from; priorTo = to; windowCount++;
                    }
                }
                Require(windowCount <= MaxOpeningWindows, "Observed hours exceed their bound.");
                Require(shop.Supply.Length == 0 && shop.Hours.Length == 0 || shop.Fingerprint.Length != 0, "Observed image has no fingerprint.");
                Require(shop.Changes != null && shop.Changes.Count <= EconomicLedger.ChangesPerShop && (shop.Changes.Count == 0 || shop.Fingerprint.Length != 0), "Shop journal exceeds its bound or has no fingerprint.");
                int last = -1;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var change in shop.Changes)
                {
                    Known(change.Kind, "Shop change kind");
                    Require(change.ObservedMinute >= last && change.ObservedMinute <= latestObservation && change.ObservedMinute >= 0, "Invalid first-observed minute.");
                    Require(change.Before >= 0 && change.After >= 0 && change.Before != change.After, "Invalid shop-change values.");
                    Require(change.Subject >= 0 && (change.Kind == ShopChangeKind.Price || change.Kind == ShopChangeKind.Stock
                        ? change.Subject == 0 && change.ProductId != null && state.ProductNames.Contains(change.ProductId) : change.Kind == ShopChangeKind.Hours ? change.Subject < 7 * MaxOpeningWindows * 2 && change.ProductId == null : change.Subject == 0 && change.ProductId == null), "Invalid shop-change subject.");
                    long bound = change.Kind == ShopChangeKind.Price ? Money.FromCents(Money.MaximumNativePriceCents).Units
                        : change.Kind == ShopChangeKind.Hours ? 1441 : int.MaxValue;
                    Require(change.Before <= bound && change.After <= bound && seen.Add(change.ObservedMinute.ToString(CultureInfo.InvariantCulture) + ":" + change.Kind + ":" + change.Subject + ":" + change.ProductId + ":" + change.Before + ":" + change.After), "Invalid or duplicate shop change.");
                    last = change.ObservedMinute; records++;
                }
                records++;
            }
            foreach (var person in state.Citizens.Concat<CitizenState>(state.VisitorRoutines))
            {
                var a = person.Accrual;
                Require(a.AttendedMinutes >= 0 && a.AttendedMinutes <= 1440 && a.UnpostedWage >= Money.Zero && a.MissMask >= 0 && a.MissMask < 1 << 14
                    && a.Day >= 0 && a.Day <= day && a.Shift >= 0 && a.Shift <= 1439, "Invalid employment accrual.");
                if (person.DayPlaceId.Length == 0) Require(EconomicText(a) == new string('0', 26), "Accrual has no employment.");
            }
            if (state.Food != null)
            {
                foreach (var m in state.Food.Residents)
                    if (m.Budget != null)
                    {
                        var a = m.Budget.Ledger;
                        Require(a.EmploymentIncome >= Money.Zero && a.Discretionary >= Money.Zero && a.VisitWatermark >= 0 && a.VisitWatermark <= state.Minute, "Invalid owner accounts.");
                    }
                foreach (var h in state.Food.History)
                    Require(h.PeriodCauses != null && h.PeriodCauses.Counts != null && h.PeriodCauses.Counts.Length == 16
                        && h.PeriodCauses.Counts.All(n => n >= 0 && n <= MealPeriodCauses.MaximumCount), "Invalid meal-period causes.");
            }
            return records;
        }
        private static void ValidatePair(BigInteger units, BigInteger count)
        {
            Wide(units); Wide(count);
            Require((units == 0) == (count == 0) && units >= count && units <= count * Money.FromCents(Money.MaximumNativePriceCents).Units, "Invalid outside-spending totals.");
        }
        private static bool ValidFingerprint(string s)
        {
            if (s == "") return true;
            if (s == null || s.Length != 20) return false;
            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";
            for (int block = 0; block < 20; block += 5)
            {
                ulong n = 0;
                for (int i = block; i < block + 5; i++)
                {
                    int digit = alphabet.IndexOf(s[i]); if (digit < 0) return false;
                    n = n * 85 + (uint)digit;
                }
                if (n > uint.MaxValue) return false;
            }
            return true;
        }

        internal static EconomicLedger? CopyEconomy(EconomicLedger? e) => e == null ? null : new EconomicLedger
        {
            Outside = e.Outside?.Select(d => d == null ? null! : new OutsideSpendingDay { Day = d.Day, RetailUnits = d.RetailUnits, RetailVisits = d.RetailVisits, LeisureUnits = d.LeisureUnits, LeisureVisits = d.LeisureVisits }).ToList()!,
            Shops = e.Shops?.Select(s => s == null ? null! : new ShopObservation { BusinessRef = s.BusinessRef, Fingerprint = s.Fingerprint, Evicted = s.Evicted, StaffedCounters = s.StaffedCounters, Supply = s.Supply == null ? null! : (ShopObservedOffer[])s.Supply.Clone(), Hours = s.Hours == null ? null! : (int[])s.Hours.Clone(), Changes = s.Changes == null ? null! : new List<ShopChange>(s.Changes) }).ToList()!,
        };

    }
}
