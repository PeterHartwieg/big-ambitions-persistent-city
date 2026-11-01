using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>Separate experiment documents. Never reads/writes native saves or population modData.</summary>
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
            if (token["Schema"]?.Type != JTokenType.Integer || (int)token["Schema"]! < 1 || (int)token["Schema"]! > FoodShadowState.CurrentSchema)
                throw new InvalidDataException("Unsupported food shadow schema; retain the original document.");
            int schema = (int)token["Schema"]!;
            var text = new TokenText(json);
            bool migrated = MigrateLegacyMoney(token, text, schema);
            CheckShape(token, typeof(FoodShadowState), schema, text);
            if (schema < FoodShadowState.CurrentSchema)
            {
                Upgrade(token, typeof(FoodShadowState), schema);
                token["Schema"] = FoodShadowState.CurrentSchema;
            }
            var state = (schema == FoodShadowState.CurrentSchema && !migrated
                ? JsonConvert.DeserializeObject<FoodShadowState>(json, Settings())
                : token.ToObject<FoodShadowState>(JsonSerializer.Create(Settings())))
                ?? throw new InvalidDataException("Missing food shadow state.");
            if (state.CityKey != expectedCityKey) throw new InvalidDataException("Food shadow document belongs to another city.");
            FoodDemand.Validate(state);
            return state;
        }

        private static void CheckShape(JToken token, Type type, int schema, TokenText text)
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
            // Original money tokens must be canonical unit integers; migrated values carry no line information.
            if (type == typeof(Money)) { Require(token.Type == JTokenType.Integer && (!((IJsonLineInfo)token).HasLineInfo() || TokenText.CanonicalUnits(text.Number((IJsonLineInfo)token)))); return; }
            if (type == typeof(double)) { Require(token.Type == JTokenType.Integer || token.Type == JTokenType.Float); return; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                Require(token is JArray);
                foreach (var item in (JArray)token) CheckShape(item, type.GetGenericArguments()[0], schema, text);
                return;
            }
            Require(token is JObject);
            var obj = (JObject)token;
            var fields = type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute), true)
                && (schema >= 2 || !AddedInV2(type, p.Name)) && (schema >= 3 || !AddedInV3(type, p.Name))
                && (schema >= 4 || !AddedInV4(type, p.Name))).ToArray();
            Require(obj.Count == fields.Length);
            foreach (var field in fields)
            {
                Require(obj.TryGetValue(field.Name, StringComparison.Ordinal, out var value));
                CheckShape(value!, field.PropertyType, schema, text);
            }
        }

        /// <summary>Schemas 1-4 (whole cents) and earlier schema-5 documents (decimal cents) name money
        /// "...Cents". Rewrite those fields to whole units from their original token text; an amount
        /// that is not a whole number of units, or a document mixing field generations, is refused.</summary>
        private static bool MigrateLegacyMoney(JObject document, TokenText text, int schema)
        {
            var legacy = document.Descendants().OfType<JProperty>().Where(p => LegacyMoneyFields.Names.ContainsKey(p.Name)).ToArray();
            if (legacy.Length == 0) return false;
            var current = new System.Collections.Generic.HashSet<string>(LegacyMoneyFields.Names.Values, StringComparer.Ordinal);
            Require(!document.Descendants().OfType<JProperty>().Any(p => current.Contains(p.Name)));
            foreach (var property in legacy)
            {
                var value = property.Value;
                Require(value.Type == JTokenType.Integer || schema >= 5 && value.Type == JTokenType.Float);
                Require(Money.TryParseCents(text.Number((IJsonLineInfo)value), schema >= 5 ? LegacyMoneyFields.MaximumFractionDigits : 0, out var money));
                property.Replace(new JProperty(LegacyMoneyFields.Names[property.Name], money.Units));
            }
            return true;
        }

        private static bool AddedInV2(Type type, string name)
        {
            string names = type == typeof(FoodShadowState) ? "Mode"
                : type == typeof(FoodBusiness) ? "MealsPerHour ServiceEvidence NextServiceMinute"
                : type == typeof(FoodTravelQuote) ? "IntentId OriginPlaceId ReturnMinutes"
                : type == typeof(FoodChoice) ? "ReturnMinutes ServiceEndsAt"
                : type == typeof(FoodIntent) ? "OriginPlaceId OriginActivity ContextEvidence DesiredAt EatingMinutes MaximumQueueMinutes TimeCostMultiplier ReturnTravelMinutes ServiceEndsAt ReturnBy" : "";
            return names.Split(' ').Contains(name, StringComparer.Ordinal);
        }

        private static bool AddedInV3(Type type, string name)
        {
            string names = type == typeof(FoodBusiness) ? "DistrictId CustomerServicePercent CustomerServiceEvidence"
                : type == typeof(FoodOffer) ? "DistrictDemandPercent DistrictDemandEvidence"
                : type == typeof(FoodConsumer) ? "ServiceQualityValueCents DistrictAppealValueCents"
                : type == typeof(FoodChoice) ? "CustomerServicePercent DistrictDemandPercent ServiceQualityAdjustmentCents DistrictAppealAdjustmentCents" : "";
            return names.Split(' ').Contains(name, StringComparer.Ordinal);
        }

        private static bool AddedInV4(Type type, string name)
        {
            string names = type == typeof(FoodConsumer) ? "HomeDistrictId BudgetEvidence"
                : type == typeof(FoodIntent) ? "OutsideOption OutsideOptionScoreCents OutsideOptionEvidence" : "";
            return names.Split(' ').Contains(name, StringComparer.Ordinal);
        }

        // Strict earlier documents retain their numerical behavior. New weights default to zero;
        // migration never invents customer-service/district observations or rewrites past choices.
        private static void Upgrade(JToken token, Type type, int schema)
        {
            if (token is JArray array)
            {
                foreach (var item in array) Upgrade(item, type.GetGenericArguments()[0], schema);
                return;
            }
            if (!(token is JObject obj)) return;
            object defaults = Activator.CreateInstance(type)!;
            foreach (var field in type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute), true)))
                if (schema < 2 && AddedInV2(type, field.Name) || schema < 3 && AddedInV3(type, field.Name)
                    || schema < 4 && AddedInV4(type, field.Name))
                {
                    object? value = field.GetValue(defaults);
                    obj.Add(field.Name, value == null ? JValue.CreateNull() : JToken.FromObject(value));
                }
                else Upgrade(obj[field.Name]!, field.PropertyType, schema);
        }

        private static void Require(bool valid)
        { if (!valid) throw new InvalidDataException("Missing, unknown or wrongly typed food shadow field; retain the original document."); }
    }
}
