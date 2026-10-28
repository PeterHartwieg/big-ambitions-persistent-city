using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>Money on the wire is a canonical JSON integer count of <see cref="Money"/> units.
    /// Shape checks validate the original token text before typed deserialization runs.</summary>
    internal sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override void WriteJson(JsonWriter writer, Money value, JsonSerializer serializer) => writer.WriteValue(value.Units);
        public override Money ReadJson(JsonReader reader, Type objectType, Money existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Integer || !(reader.Value is long units))
                throw new JsonSerializationException("Money must be a whole number of units.");
            return Money.FromUnits(units);
        }
    }

    /// <summary>Original text of number tokens. Json.NET reports a token's line and the position just
    /// after it; it parses fractional numbers through double or decimal, both of which can round.</summary>
    internal sealed class TokenText
    {
        private readonly string source;
        private readonly List<int> lineStarts = new List<int> { 0 };
        public TokenText(string source)
        {
            this.source = source;
            // The same line breaks Json.NET counts: "\r\n", "\r" and "\n".
            for (int i = 0; i < source.Length; i++)
                if (source[i] == '\r') { if (i + 1 < source.Length && source[i + 1] == '\n') i++; lineStarts.Add(i + 1); }
                else if (source[i] == '\n') lineStarts.Add(i + 1);
        }
        public string Source => source;
        public int End(IJsonLineInfo token) => lineStarts[token.LineNumber - 1] + token.LinePosition;
        /// <summary>The whole number token, back to the preceding JSON delimiter or whitespace, so
        /// a prefix such as Json.NET's hexadecimal "0x" stays in the text that is validated.</summary>
        public string Number(IJsonLineInfo token)
        {
            int end = End(token), start = end;
            while (start > 0 && !Delimiter(source[start - 1])) start--;
            return source.Substring(start, end - start);
        }
        private static bool Delimiter(char c) => c == ':' || c == ',' || c == '[' || c == '{' || char.IsWhiteSpace(c);
        public static bool CanonicalUnits(string text)
            => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long units)
                && units.ToString(CultureInfo.InvariantCulture) == text;
    }

    /// <summary>Fields that held decimal cents before money became whole units. Their names were
    /// retired with the unit change, so a payload's field names identify its money unit.</summary>
    internal static class LegacyMoneyFields
    {
        public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OpeningBalanceCents"] = "OpeningBalance", ["BalanceCents"] = "Balance", ["IncomeReceivedCents"] = "IncomeReceived",
            ["EssentialsPaidCents"] = "EssentialsPaid", ["HomeFoodPaidCents"] = "HomeFoodPaid", ["PreparedFoodSpentCents"] = "PreparedFoodSpent",
            ["HeldPurchaseCents"] = "HeldPurchase", ["ArrearsCents"] = "Arrears", ["DayAllowanceUsedCents"] = "DayAllowanceUsed",
            ["AllowanceCents"] = "Allowance", ["DailyAllowanceCents"] = "DailyAllowance", ["PriceCents"] = "Price",
            ["SpentCents"] = "Spent", ["RevenueCents"] = "Revenue",
        };
        /// <summary>Earlier decimal money had at most System.Decimal's 28 fractional digits.</summary>
        public const int MaximumFractionDigits = 28;
    }
}
