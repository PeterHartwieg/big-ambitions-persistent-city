using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static class FoodShadowCodec
    {
        private const int MaxCharacters = 32 * 1024 * 1024;
        private static JsonSerializerSettings Settings() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error,
            MaxDepth = 24, FloatParseHandling = FloatParseHandling.Double,
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            Converters = { new MoneyJsonConverter() }
        };

        public static string Write(FoodShadowState state)
        {
            FoodDemand.Validate(state);
            string json = JsonConvert.SerializeObject(state, Formatting.Indented, Settings());
            if (json.Length > MaxCharacters) throw new InvalidDataException("Food shadow document is too large.");
            return json;
        }

        public static FoodShadowState Read(string json, string expectedCityKey)
        {
            if (json == null || json.Length > MaxCharacters) throw new InvalidDataException("Missing or oversized food shadow document.");
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 24, DateParseHandling = DateParseHandling.None };
            var token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing food shadow document content.");
            if (token["Schema"]?.Type != JTokenType.Integer || (int)token["Schema"]! != FoodShadowState.CurrentSchema)
                throw new InvalidDataException("Unsupported food shadow schema; retain the original document.");
            CheckShape(token, typeof(FoodShadowState), new TokenText(json));
            var state = JsonConvert.DeserializeObject<FoodShadowState>(json, Settings())
                ?? throw new InvalidDataException("Missing food shadow state.");
            if (state.CityKey != expectedCityKey) throw new InvalidDataException("Food shadow document belongs to another city.");
            FoodDemand.Validate(state);
            return state;
        }

        private static void CheckShape(JToken token, Type type, TokenText text)
        {
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (token.Type == JTokenType.Null)
            {
                if (nullable != null) return;
                throw new InvalidDataException("Unexpected null in food shadow document.");
            }
            type = nullable ?? type;
            if (type == typeof(string)) { Require(token.Type == JTokenType.String); return; }
            if (type == typeof(int) || type.IsEnum) { Require(token.Type == JTokenType.Integer); return; }
            if (type == typeof(Money)) { Require(token.Type == JTokenType.Integer && TokenText.CanonicalUnits(text.Number((IJsonLineInfo)token))); return; }
            if (type == typeof(double)) { Require(token.Type == JTokenType.Integer || token.Type == JTokenType.Float); return; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                Require(token is JArray);
                foreach (var item in (JArray)token) CheckShape(item, type.GetGenericArguments()[0], text);
                return;
            }
            Require(token is JObject);
            var obj = (JObject)token;
            var fields = type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute), true)).ToArray();
            Require(obj.Count == fields.Length);
            foreach (var field in fields)
            {
                Require(obj.TryGetValue(field.Name, StringComparison.Ordinal, out var value));
                CheckShape(value!, field.PropertyType, text);
            }
        }

        private static void Require(bool valid)
        { if (!valid) throw new InvalidDataException("Missing, unknown or wrongly typed food shadow field; retain the original document."); }
    }
}
