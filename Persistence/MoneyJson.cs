using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
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

    internal sealed class TokenText
    {
        private readonly string source;
        private readonly List<int> lineStarts = new List<int> { 0 };
        public TokenText(string source)
        {
            this.source = source;
            for (int i = 0; i < source.Length; i++)
                if (source[i] == '\r') { if (i + 1 < source.Length && source[i + 1] == '\n') i++; lineStarts.Add(i + 1); }
                else if (source[i] == '\n') lineStarts.Add(i + 1);
        }
        public string Source => source;
        public int End(IJsonLineInfo token) => lineStarts[token.LineNumber - 1] + token.LinePosition;
        public string Number(IJsonLineInfo token)
        {
            int end = End(token), start = end;
            while (start > 0 && !Delimiter(source[start - 1])) start--;
            return source.Substring(start, end - start);
        }
        private static bool Delimiter(char c) => c == ':' || c == ',' || c == '[' || c == '{' || char.IsWhiteSpace(c);
        public bool CanonicalInteger(IJsonLineInfo token)
        {
            int end = End(token), start = end;
            while (start > 0 && !Delimiter(source[start - 1])) start--;
            bool negative = start < end && source[start] == '-';
            if (negative) start++;
            if (start == end || source[start] == '0' && (negative || end - start != 1)) return false;
            for (int i = start; i < end; i++) if (source[i] < '0' || source[i] > '9') return false;
            return true;
        }
        public bool LiteralAsciiString(IJsonLineInfo token, string value)
        {
            int end = End(token), start = end - value.Length - 2;
            return start >= 0 && source[start] == '"' && source[end - 1] == '"'
                && string.CompareOrdinal(source, start + 1, value, 0, value.Length) == 0;
        }
        public static bool CanonicalUnits(string text)
            => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long units)
                && units.ToString(CultureInfo.InvariantCulture) == text;
    }
}
