using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>Strict current-development codec. Unsupported and failed reads never replace a city.</summary>
    internal static partial class PopulationSchema20
    {
        public const string StateKey = "persistentcity:state_v1";
        // Storage and nested-record limits are separate from the identity ceiling;
        // not every individual maximum can be combined into one valid payload.
        public const int MaxInputCharacters = 256 * 1024 * 1024;
        public const int MaxCitizens = 50000;
        public const int MaxEmbeddedRecords = 4000000;
        public const int MaxPlaces = 20000;
        public const int MaxRouteLegs = 512;
        public const int MaxRecentVisits = 64;
        public const int MaxOpeningWindows = 64;
        public const int MaxTrafficLinks = 250000;
        public const int MaxTrafficLinkCapacity = 1024;
        // Schema 17 current-day outcomes per consumer; decision bounds come from FoodChoicePolicy.
        public const int MaxMealOutcomes = 3;
        private const int Schema18MaximumOffersPerShop = 4;
        // A compact outcome record that validation accepts is at most 553 characters: six enum fields of at most two
        // digits, two whole minutes of at most ten, nine separators and two 256-character IDs. The reader's cap
        // must not be tighter, or a save Write accepted could not be read back.
        public const int MaxMealOutcomeWire = 600;
        private const double MaxMinute = 1000000000;
        private const double MaxCoordinate = 10000000;

        public static string Write(CityState state)
        {
            if (state == null) throw new SaveValidationException("State is null.");
            state.PrepareForSave();
            Validate(state);
            var json = JsonConvert.SerializeObject(state, Formatting.None, Settings(WireContext.FromState(state)));
            Require(json.Length <= MaxInputCharacters, "Serialized state exceeds the input size limit.");
            Require(System.Text.Encoding.UTF8.GetByteCount(json) <= MaxInputCharacters, "Serialized state exceeds the UTF-8 size limit.");
            return json;
        }

        /// <summary>Run the save contract without retaining the serialized activation payload.</summary>
        public static void ValidateForActivation(CityState state)
        {
            if (state == null) throw new SaveValidationException("State is null.");
            state.PrepareForSave();
            Validate(state);
            using var count = new CharacterCountingWriter();
            var serializer = JsonSerializer.CreateDefault(Settings(WireContext.FromState(state)));
            // Match JsonConvert.SerializeObject(state, Formatting.None, Settings()), including
            // application-wide defaults that the explicit save settings do not override.
            serializer.Formatting = Formatting.None;
            using (var writer = new JsonTextWriter(count)) serializer.Serialize(writer, state);
            Require(count.Count <= MaxInputCharacters, "Serialized state exceeds the input size limit.");
            Require(count.Bytes <= MaxInputCharacters, "Serialized state exceeds the UTF-8 size limit.");
        }

        // Count UTF-16 characters, just like string.Length, including JSON escapes.
        // Finish serialization before checking the bound to preserve serialization failures.
        private sealed class CharacterCountingWriter : TextWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;
            public override IFormatProvider FormatProvider => CultureInfo.InvariantCulture;
            public long Count { get; private set; }
            public long Bytes { get; private set; }
            private bool high;
            public override void Write(char value)
            {
                Count++;
                if (high && char.IsLowSurrogate(value)) { Bytes++; high = false; return; }
                high = char.IsHighSurrogate(value);
                Bytes += value <= 0x7f ? 1 : value <= 0x7ff ? 2 : 3;
            }
            public override void Write(string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                Count += value!.Length;
                Bytes += System.Text.Encoding.UTF8.GetByteCount(value);
                if (high && char.IsLowSurrogate(value[0])) Bytes -= 2;
                high = char.IsHighSurrogate(value[value.Length - 1]);
            }
            public override void Write(char[] buffer, int index, int count)
            {
                if (count == 0) return;
                Count += count; Bytes += System.Text.Encoding.UTF8.GetByteCount(buffer, index, count);
                if (high && char.IsLowSurrogate(buffer[index])) Bytes -= 2;
                high = char.IsHighSurrogate(buffer[index + count - 1]);
            }
        }

        public static SaveReadResult Read(string? source)
        {
            if (source == null) return new SaveReadResult(SaveReadStatus.Missing, null, "");
            if (source.Length > MaxInputCharacters) return Invalid("State exceeds the input size limit.");
            try
            {
                int schema = ReadSchema(source);
                if (schema > 20) return new SaveReadResult(SaveReadStatus.Future, null, "State uses a newer schema; preserve the original payload.", schema);
                if (schema <= 17)
                {
                    // Original readers, allow-lists, limits, money conversion and semantic/count rules.
                    var old = LegacyPopulationCodec.Read(source);
                    if (old.Status != SaveReadStatus.Loaded) return old;
                    var upgraded = old.State!;
                    upgraded.Schema = 20;
                    upgraded.WeatherStartsDay=(int)(upgraded.Minute/1440)+1;
                    if(upgraded.Food!=null) upgraded.Food.AwarenessStartsDay=(int)(upgraded.Minute/1440)+1;
                    // Constructors initialized empty memory/references and inactive B; infer nothing.
                    ValidateForActivation(upgraded);
                    return new SaveReadResult(SaveReadStatus.Loaded, upgraded, "", schema);
                }
                Require(schema == 18 || schema == 19 || schema == 20, "Unsupported experimental state schema; preserve the original payload.");
                Require(System.Text.Encoding.UTF8.GetByteCount(source) <= MaxInputCharacters, "State exceeds the UTF-8 size limit.");
                var context = WireContext.FromSource(source);
                var serializer = JsonSerializer.Create(SettingsForSchema(context, schema));
                using (var shape = Reader(source))
                {
                    Require(shape.Read(), "Missing state.");
                    int records = 0;
                    CheckShape(shape, typeof(CityState), serializer.ContractResolver, ref records, new TokenText(source), schema, new FieldNameSets());
                    Require(!shape.Read(), "State contains trailing JSON content.");
                }
                using var reader = Reader(source);
                var state = serializer.Deserialize<CityState>(reader);
                Require(state != null, "State is null.");
                if(schema==18)
                {
                    state!.Schema=20;
                    if(state.Food!=null) state.Food.AwarenessStartsDay=(int)(state.Minute/1440)+1;
                }
                if(schema<20)
                {
                    state!.Schema=20;state.WeatherStartsDay=(int)(state.Minute/1440)+1;
                    ValidateForActivation(state); // new required weather bytes must fit before publishing an upgrade
                }
                else Validate(state!);
                return new SaveReadResult(SaveReadStatus.Loaded, state, "", schema);
            }
            catch (Exception exception) when (exception is JsonException || exception is SaveValidationException || exception is OverflowException || exception is FormatException || exception is ArgumentException)
            { return Invalid("Invalid state: " + exception.Message); }
        }

        private static SaveReadResult Invalid(string error) => new SaveReadResult(SaveReadStatus.Invalid, null, error);

        // Json.NET otherwise coerces strings to numbers and rounds floating-point integers.
        // A supported payload has one exact wire shape; corruption must not silently change meaning.
        private static JsonTextReader Reader(string source) => new JsonTextReader(new StringReader(source))
        { MaxDepth = 48, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double };

        private static int ReadSchema(string source)
        {
            using var reader = Reader(source);
            Require(reader.Read() && reader.TokenType == JsonToken.StartObject, "State must be a JSON object.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            int? schema = null;
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Require(reader.TokenType == JsonToken.PropertyName, "Expected a field.");
                string name = (string)reader.Value!;
                if (!names.Add(name)) Fail("Duplicate field: " + name);
                Require(reader.Read(), "Missing field value.");
                if (name == "Schema")
                {
                    Require(reader.TokenType == JsonToken.Integer, "Invalid state schema.");
                    Require(int.TryParse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value), "Invalid state schema.");
                    schema = value;
                }
                reader.Skip();
            }
            Require(reader.TokenType == JsonToken.EndObject && !reader.Read(), "Incomplete or trailing JSON content.");
            Require(schema.HasValue, "Missing state schema.");
            return schema!.Value;
        }

        // Validate directly from tokens before deserialization: no full payload DOM, no coercion,
        // and bounded collection allocation even for hostile arrays of otherwise valid records.
        // One reusable duplicate-field set per JSON depth for a single read: a set is only in use while its
        // object is open, so nested objects never share one. Not shared between reads or threads.
        private sealed class FieldNameSets
        {
            private readonly HashSet<string>?[] sets = new HashSet<string>?[64];
            public HashSet<string> At(int depth)
            {
                var set = sets[depth] ??= new HashSet<string>(StringComparer.Ordinal);
                set.Clear();
                return set;
            }
        }

        // Json.NET reads an integer token as a long, or a BigInteger beyond the long range: the same
        // acceptance as parsing the token's invariant text, without formatting it first.
        private static bool IntegerToken(object? value, long min, long max) => value is long number && number >= min && number <= max;

        private static void CheckShape(JsonReader reader, Type type, IContractResolver resolver, ref int records, TokenText text, int schema, FieldNameSets fieldNames)
        {
            var token = reader.TokenType;
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (token == JsonToken.Null)
            {
                Require(nullable != null || NullableRecord(type), "Unexpected null value.");
                if (type == typeof(ResidentFoodToday)) Charge(ref records); // one rollup slot per consumer, including null
                return;
            }
            if (nullable != null) type = nullable;
            if (type == typeof(FoodDecisionTuning))
            {
                Require(token == JsonToken.String && FoodDecisionTuning.IsCanonicalWire((string)reader.Value!), "Invalid held tuning image.");
                return;
            }
            if (type == typeof(string)) { Require(token == JsonToken.String, "Expected a string."); Require(((string)reader.Value!).Length <= 512, "Oversized string."); return; }
            if (type == typeof(bool)) { Require(token == JsonToken.Boolean, "Expected a boolean."); return; }
            if (type == typeof(int) || type.IsEnum) { Require(token == JsonToken.Integer, "Expected an integer."); Require(IntegerToken(reader.Value, int.MinValue, int.MaxValue) && text.CanonicalInteger((IJsonLineInfo)reader), "Integer is out of range or noncanonical."); return; }
            if (type == typeof(long)) { Require(token == JsonToken.Integer, "Expected an integer."); Require(IntegerToken(reader.Value, long.MinValue, long.MaxValue) && text.CanonicalInteger((IJsonLineInfo)reader), "Integer is out of range or noncanonical."); return; }
            if (type == typeof(Money))
            {
                // Validate the original token: Json.NET reads "010" as octal 8 and rounds fractions.
                Require(token == JsonToken.Integer && TokenText.CanonicalUnits(text.Number((IJsonLineInfo)reader)), "Money must be a canonical whole number of units.");
                return;
            }
            if (type == typeof(double)) { Require(token == JsonToken.Float || token == JsonToken.Integer && text.CanonicalInteger((IJsonLineInfo)reader), "Expected a canonical number."); return; }
            if (type == typeof(MealOutcomeRecord))
            {
                Require(token == JsonToken.String && ((string)reader.Value!).Length <= MaxMealOutcomeWire
                    && MealOutcomeRecord.IsCanonicalWire((string)reader.Value!), "Invalid meal outcome record.");
                return;
            }
            if (TupleLayout.For(type) is TupleLayout tuple)
            {
                Require(token == JsonToken.StartArray && reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 1, 1) && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid tuple version.");
                foreach (var slot in tuple.Properties)
                {
                    Require(reader.Read() && reader.TokenType != JsonToken.EndArray, "Missing tuple slot.");
                    CheckShape(reader, slot.PropertyType, resolver, ref records, text, schema, fieldNames);
                }
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus tuple slot.");
                return;
            }
            if (type == typeof(ResidentFoodToday))
            {
                Require(token == JsonToken.StartArray && reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 1, 1) && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid rollup version.");
                for (int i = 0; i < 2; i++) { Require(reader.Read(), "Missing rollup header."); CheckShape(reader, typeof(int), resolver, ref records, text, schema, fieldNames); }
                int count = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    Require(++count <= 3, "Surplus rollup slot.");
                    CheckShape(reader, typeof(MealOutcomeRecord), resolver, ref records, text, schema, fieldNames);
                }
                Require(reader.TokenType == JsonToken.EndArray, "Incomplete rollup.");
                Charge(ref records); // one bounded rollup, never its scalar slots
                return;
            }
            if (type == typeof(WeatherCommitment))
            {
                if(reader.TokenType==JsonToken.Null) return;
                Require(reader.TokenType==JsonToken.String && text.LiteralAsciiString((IJsonLineInfo)reader,(string)reader.Value!),"Invalid weather encoding.");
                WeatherCommitment.Parse((string)reader.Value!);Charge(ref records);return;
            }
            if (type == typeof(ConsumerMemory))
            {
                Require(token == JsonToken.String && ((string)reader.Value!).Length <= (schema>=20 ? 50 : 48) && text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Invalid memory length/encoding.");
                if(schema>=20) CheckMemory20((string)reader.Value!);else MemoryWire.CheckWire((string)reader.Value!);
                Charge(ref records); // four fixed memories + watermark are ONE extension
                return;
            }
            if (type == typeof(VisitRecord)) { VisitConverter.Check(reader); Require(text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Noncanonical visit encoding."); return; }
            if (type == typeof(BusinessReference)) { BusinessReferenceConverter.Check(reader, text); return; }
            if (type == typeof(PopulationCapabilities)) Charge(ref records);
            if (type == typeof(Position))
            {
                Require(token == JsonToken.StartObject, "Expected a position object.");
                int components = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    Require(reader.TokenType == JsonToken.PropertyName, "Expected position field.");
                    string name = (string)reader.Value!;
                    int bit = name == "X" ? 1 : name == "Y" ? 2 : name == "Z" ? 4 : 0;
                    Require(bit != 0 && (components & bit) == 0, "Unknown or duplicate position field.");
                    components |= bit;
                    Require(reader.Read() && (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer && text.CanonicalInteger((IJsonLineInfo)reader)), "Invalid position number.");
                }
                Require(reader.TokenType == JsonToken.EndObject && components == 7, "Missing position field.");
                return;
            }
            var contract = resolver.ResolveContract(type);
            if (contract is JsonArrayContract array)
            {
                Require(token == JsonToken.StartArray, "Expected an array.");
                var item = array.CollectionItemType!;
                int limit = item == typeof(FoodAwarenessCandidate) ? 24 : item == typeof(BusinessReference) ? MaxPlaces : item == typeof(MealOutcomeRecord) ? MaxMealOutcomes : item == typeof(FoodDecisionShop) ? FoodChoicePolicy.MaximumShortlist
                    : item == typeof(FoodDecisionOffer) ? Schema18MaximumOffersPerShop
                    : item == typeof(FoodProductSales) ? 4 : item == typeof(FoodBuyerDistrict) ? 100 : item == typeof(long) ? 24 : item == typeof(FoodShopDay) ? 14 * MaxPlaces : item == typeof(ResidentMeal) ? MaxCitizens * 3 : item == typeof(FoodScope) ? 1024
                    : item == typeof(FoodRoutineStop) ? MaxCitizens
                    : item == typeof(FoodDistrictBudgetMix) ? 100 : item == typeof(CitizenState) ? MaxCitizens
                    : item == typeof(Place) ? MaxPlaces : item == typeof(CitizenIdentity) ? 5000
                    : item == typeof(RouteLeg) ? MaxRouteLegs : item == typeof(VisitRecord) ? MaxRecentVisits
                    : item == typeof(TrafficRouteLeg) ? MaxRouteLegs : item == typeof(TrafficVehicleState) ? MaxCitizens
                    : item == typeof(TrafficLinkDefinition) || item == typeof(TrafficLinkState) || item == typeof(TrafficEventState) || item == typeof(int) ? MaxTrafficLinks
                    : item == typeof(string) ? MaxCitizens
                    : item == typeof(OpeningWindow) ? MaxOpeningWindows : MaxEmbeddedRecords;
                int tableStart = item == typeof(BusinessReference) ? text.End((IJsonLineInfo)reader) - 1 : 0;
                int count = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    Require(++count <= limit, "Oversized collection.");
                    if (item == typeof(FoodAwarenessCandidate) || item == typeof(BusinessReference) || item == typeof(RouteLeg) || item == typeof(VisitRecord) || item == typeof(OpeningWindow)
                        || item == typeof(TrafficRouteLeg) || item == typeof(TrafficVehicleState) || item == typeof(TrafficLinkDefinition)
                        || item == typeof(TrafficLinkState) || item == typeof(TrafficEventState) || item == typeof(int) || item == typeof(string)
                        || item == typeof(MealOutcomeRecord) || item == typeof(FoodDecisionShop) || item == typeof(FoodDecisionOffer))
                        Require(++records <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                    CheckShape(reader, item, resolver, ref records, text, schema, fieldNames);
                }
                Require(reader.TokenType == JsonToken.EndArray, "Incomplete array.");
                if (item == typeof(BusinessReference))
                {
                    int end = text.End((IJsonLineInfo)reader);
                    Require(end - tableStart <= MaxBusinessReferenceWire && System.Text.Encoding.UTF8.GetByteCount(text.Source.AsSpan(tableStart, end - tableStart)) <= MaxBusinessReferenceWire, "Business reference table exceeds its character/UTF-8 budget.");
                }
                return;
            }
            Require(token == JsonToken.StartObject && contract is JsonObjectContract, "Expected an object.");
            var properties = ((JsonObjectContract)contract).Properties;
            var names = fieldNames.At(reader.Depth);
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Require(reader.TokenType == JsonToken.PropertyName, "Expected a field.");
                string name = (string)reader.Value!;
                if (!names.Add(name)) Fail("Duplicate field: " + name);
                var property = properties.GetProperty(name, StringComparison.Ordinal);
                if (property == null) Fail("Unknown field: " + name);
                Require(reader.Read(), "Missing field value.");
                CheckShape(reader, property.PropertyType!, resolver, ref records, text, schema, fieldNames);
            }
            Require(reader.TokenType == JsonToken.EndObject && names.Count == properties.Count - (type == typeof(FoodDecisionState) && !names.Contains("Tuning") ? 1 : 0)
                - (type == typeof(FoodDecisionShop) && !names.Contains("MemoryContribution") ? 1 : 0), "Missing object fields.");
        }

        private static bool NullableRecord(Type type) => type == typeof(OptionalStopState) || type == typeof(Route) || type == typeof(CarState)
            || type == typeof(TrafficKernelState) || type == typeof(TrafficSignalSchedule)
            || type == typeof(FoodCommerceState) || type == typeof(MealOuting) || type == typeof(MealExcursion) || type == typeof(ResidentBudget)
            || type == typeof(WeatherCommitment) || type == typeof(FoodAwarenessImage) || type == typeof(FoodDecisionTuning) || type == typeof(ResidentTraits) || type == typeof(ResidentFoodToday) || type == typeof(FoodDecisionState);
        // Contracts depend only on the sealed save DTO types, never a city or serializer setting.
        // DefaultContractResolver owns a thread-safe metadata cache; settings/serializer instances
        // remain per operation, including application-wide JsonConvert defaults.
        private static readonly IContractResolver StateContracts = new StateContractResolver();
        private static readonly IContractResolver Schema19Contracts = new StateContractResolver(19);
        private static readonly IContractResolver Schema18Contracts = new StateContractResolver(18);
        private static JsonSerializerSettings Settings(WireContext context) => SettingsForSchema(context,20);
        private static JsonSerializerSettings SettingsForSchema(WireContext context, int schema)
        {
            var settings = new JsonSerializerSettings {
                ContractResolver = schema==18 ? Schema18Contracts : schema==19 ? Schema19Contracts : StateContracts, Culture = CultureInfo.InvariantCulture,
                MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None,
                MaxDepth = 48, FloatParseHandling = FloatParseHandling.Double
            };
            settings.Converters.Add(new WeatherConverter()); settings.Converters.Add(new PositionConverter()); settings.Converters.Add(new MoneyJsonConverter());
            settings.Converters.Add(new TuningConverter()); settings.Converters.Add(new MealOutcomeConverter()); settings.Converters.Add(new TupleConverter());
            settings.Converters.Add(new TodayConverter()); settings.Converters.Add(new MemoryConverter(schema));
            settings.Converters.Add(new VisitConverter(context)); settings.Converters.Add(new BusinessReferenceConverter(context));
            return settings;
        }

        private static void ValidateOptionalStop(CitizenState citizen, CityState state, IReadOnlyDictionary<string, Place> places,
            IReadOnlyDictionary<string, CitizenState> citizens)
        {
            var stop = citizen.OptionalStop;
            if (stop == null) return;
            Known(stop.Kind, "Stop kind");
            Require(DwellRules.For(stop.Kind).Optional, "Nonoptional stop kind.");
            Text(stop.PlaceId, "Stop place", 256); Text(stop.SpotId, "Stop spot", 512); Text(stop.TargetId, "Stop target", 512);
            Point(stop.Position); Point(stop.Facing); Point(stop.ReturnPosition);
            Minute(stop.StartMinute, "Stop start"); Minute(stop.EndMinute, "Stop end");
            Minute(stop.VisitMinute, "Stop visit"); Minute(stop.ResumeEventMinute, "Stop continuation");
            Require(stop.VisitMinute <= stop.StartMinute && stop.VisitMinute <= state.Minute
                && stop.EndMinute > stop.StartMinute && stop.EndMinute <= stop.ResumeEventMinute
                && stop.EndMinute - stop.StartMinute >= OptionalStopPolicy.Minimum(stop.Kind) - 1e-9
                && stop.EndMinute - stop.StartMinute <= DwellRules.For(stop.Kind).MaximumMinutes + 1e-9, "Invalid stop duration/continuation.");
            places.TryGetValue(stop.PlaceId, out var stopPlace);
            Require(stopPlace != null && Position.Distance(stop.ReturnPosition, stopPlace.Position) < 0.001, "Missing/changed stop return place.");
            Require(Position.Distance(stop.Position, stop.ReturnPosition) <= 100, "Stop is not local to its existing visit.");
            Require(stop.Kind == DwellKind.Break ? stopPlace.Kind == PlaceKind.Work
                : stop.Kind == DwellKind.WindowShopping ? stopPlace.Kind == PlaceKind.Shop
                : stop.Kind == DwellKind.Audience ? stopPlace.Kind == PlaceKind.Shop || stopPlace.Kind == PlaceKind.Leisure
                : stopPlace.Kind == PlaceKind.Leisure, "Stop is ineligible for its place.");
            Require(stop.Members != null && stop.Members.Count <= 4 && stop.Members.Distinct(StringComparer.Ordinal).Count() == stop.Members.Count,
                "Invalid stop members.");
            if (stop.Kind == DwellKind.ChatOutsideVenue)
            {
                Require(stop.Members.Count >= 2 && stop.Members.Contains(citizen.Identity.Id), "Conversation requires persistent participants.");
                foreach (string member in stop.Members)
                {
                    Text(member, "Stop member", 256);
                    if (stop.Cancelled || state.Minute >= stop.EndMinute) continue;
                    citizens.TryGetValue(member, out var partnerCitizen);
                    var partner = partnerCitizen?.OptionalStop;
                    Require(partner != null && !partner.Cancelled && partner.Kind == stop.Kind && partner.PlaceId == stop.PlaceId
                        && partner.TargetId == stop.TargetId && partner.StartMinute == stop.StartMinute && partner.EndMinute == stop.EndMinute
                        && partner.Members != null && partner.Members.SequenceEqual(stop.Members), "Inconsistent conversation membership.");
                    if (member != citizen.Identity.Id) Require(partner.SpotId != stop.SpotId
                        && Position.Distance(partner.Position, stop.Position) >= 0.65, "Conversation members share a spot.");
                }
            }
            else Require(stop.Members.Count == 0, "Individual stop has group members.");
            if (!stop.Cancelled && state.Minute < stop.EndMinute)
                Require(citizen.Phase == CitizenPhase.AtPlace && citizen.PlaceId == stop.PlaceId && citizen.MealOuting == null
                    && citizen.NextEventMinute == stop.ResumeEventMinute && stop.EndMinute <= citizen.ActivityUntilMinute,
                    "Active stop lost its original activity continuation.");
        }

        private static readonly Money MaximumAllowance = Money.FromCents(1000000);

        private static int Validate(CityState state)
        {
            Require(state != null, "State is null.");
            Require(state.Schema == 20, "Unsupported state schema.");
            Require(state.WeatherStartsDay>=0 && state.WeatherStartsDay<=(int)(state.Minute/1440)+1,"Invalid weather activation day.");
            Require(state.Capabilities != null, "Missing population capabilities.");
            ValidateCapabilities(state.Capabilities);
            int embeddedRecords = 1 + ValidateFood(state);
            Require(state.TravelTimingVersion == 0 || state.TravelTimingVersion == 1, "Invalid travel timing version.");
            Bounded(state.TravelClockRate, 0.01, 1000, "Travel clock rate");
            Text(state.PopulationKey, "Population key", 256);
            Text(state.RloVersion, "RLO version", 128);
            Minute(state.Minute, "City minute");
            Require(state.Places != null && state.Places.Count > 0 && state.Places.Count <= MaxPlaces, "Invalid place count.");
            Require(state.Citizens != null && state.Citizens.Count > 0 && state.Citizens.Count <= MaxCitizens, "Invalid citizen count.");
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            foreach (var place in state.Places!)
            {
                Require(place != null, "Null place.");
                Text(place.Id, "Place ID", 256); Text(place.Name, "Place name", 512); Text(place.District, "Place district", 256, true);
                if (places.ContainsKey(place.Id)) Fail("Duplicate place ID: " + place.Id);
                places.Add(place.Id, place); Known(place.Kind, "Place kind"); Point(place.Position);
                Bounded(place.AttractionWeight, 0.01, 100, "Attraction weight");
                Require(place.NativeTrafficIndex >= -1 && place.NativeTrafficIndex <= 1000000, "Invalid native traffic index.");
                Text(place.EvidenceSource, "Place evidence source", 256);
                Bounded(place.ResidentialAreaSquareMetres, 0, 1000000000, "Residential area");
                Require(place.Capacity >= 0 && place.Capacity <= 1000000, "Invalid place capacity.");
                Require(place.OpenMinute >= 0 && place.OpenMinute < 1440 && place.CloseMinute >= 0 && place.CloseMinute <= 1440, "Invalid opening hours.");
                Require(place.WeeklyHours != null && place.WeeklyHours.Count <= MaxOpeningWindows, "Invalid weekly opening window count.");
                embeddedRecords += place.WeeklyHours.Count;
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                foreach (var window in place.WeeklyHours) OpeningWindowRules(window);
            }
            ValidateExpansion(state);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var citizens = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
            foreach (var citizen in state.Citizens!)
            {
                Require(citizen != null && citizen.Identity != null, "Missing citizen identity.");
                Text(citizen.Identity.Id, "Citizen ID", 256);
                if (citizens.ContainsKey(citizen.Identity.Id)) Fail("Duplicate citizen ID: " + citizen.Identity.Id);
                citizens.Add(citizen.Identity.Id, citizen);
            }
            var assigned = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var citizen in state.Citizens!)
            {
                Require(citizen != null && citizen.Identity != null && citizen.Preferences != null, "Missing citizen identity or preferences.");
                var identity = citizen.Identity;
                Text(identity.Id, "Citizen ID", 256); Text(identity.Name, "Citizen name", 512); Text(identity.District, "Citizen district", 256, true);
                if (!ids.Add(identity.Id)) Fail("Duplicate citizen ID: " + identity.Id);
                Require(identity.Age >= 0 && identity.Age <= 130, "Invalid citizen age."); Known(identity.Persona, "Persona");
                if(citizen.W!=null)
                {
                    Minute(citizen.W.Boundary,"Weather boundary");Dense((int)citizen.W.Weather,2,"resident weather");
                    Require(citizen.W.Queue ? citizen.W.Value>=2 && citizen.W.Value<=10 : citizen.W.Value==0 || citizen.W.Value==1,"Invalid weather commitment value.");
                    Require(citizen.W.Queue || citizen.W.Weather==WeatherSample.Rain || citizen.W.Value==1,"Nonneutral dry/unknown outing capture.");
                    Require(citizen.W.Queue ? citizen.W.Boundary<=citizen.ActivityUntilMinute : citizen.W.Boundary<=state.Minute,"Invalid resident weather boundary.");
                    Charge(ref embeddedRecords);
                }
                Traits(citizen.Traits, "resident");
                Reference(places, citizen.HomeId, PlaceKind.Home);
                // DailyRoutine derives the activity from persona. A mismatched assignment would
                // load successfully, then create a destination/activity mismatch on its next trip.
                if (identity.Persona == Persona.Retiree)
                    Require(citizen.DayPlaceId == "", "A retiree cannot have a day-place assignment.");
                else
                    OptionalReference(places, citizen.DayPlaceId, identity.Persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work);
                ValidateCommutePreparation(citizen, places, state.Minute);
                CountAssignment(citizen.HomeId);
                if (citizen.DayPlaceId != "") CountAssignment(citizen.DayPlaceId);
                OptionalReference(places, citizen.ShopId, PlaceKind.Shop);
                OptionalReference(places, citizen.LeisureId, PlaceKind.Leisure);
                ValidateOptionalStop(citizen, state, places, citizens);
                Known(citizen.Phase, "Citizen phase"); Known(citizen.Activity, "Activity"); Known(citizen.DestinationActivity, "Destination activity");
                var current = Reference(places, citizen.PlaceId);
                Require((int)current.Kind == (int)citizen.Activity, "Current activity does not match current place.");
                Bounded(citizen.Preferences.WalkingToleranceMetres, 0, MaxCoordinate, "Walking tolerance");
                Bounded(citizen.Preferences.FareWeightMinutes, 0, 1000000, "Fare weight");
                Minute(citizen.DepartureMinute, "Departure minute"); Minute(citizen.NextEventMinute, "Next event minute"); Minute(citizen.ActivityUntilMinute, "Activity end minute");
                Require(citizen.NextEventMinute >= state.Minute, "Next event precedes the city clock.");
                Require(citizen.CompletedJourneys >= 0 && citizen.MissedActivities >= 0, "Negative citizen counters.");
                if (citizen.Phase == CitizenPhase.AtPlace)
                {
                    Require(citizen.DestinationId == "" && citizen.Journey == null && citizen.DepartureMinute == 0, "At-place citizen has an active destination or journey.");
                }
                else
                {
                    var destination = Reference(places, citizen.DestinationId);
                    Require((int)destination.Kind == (int)citizen.DestinationActivity, "Destination activity does not match destination place.");
                    if (citizen.Phase == CitizenPhase.WaitingForRoute || citizen.Phase == CitizenPhase.WaitingForOpening)
                        Require(citizen.Journey == null && citizen.DepartureMinute == 0, "Waiting citizen has an active journey.");
                    else
                    {
                        var route = citizen.Journey;
                        Require(route != null && route.Legs != null && route.Legs.Count > 0 && route.Legs.Count <= MaxRouteLegs, "Missing or oversized route.");
                        embeddedRecords += route!.Legs.Count;
                        Position previous = current.Position;
                        foreach (var leg in route.Legs)
                        {
                            Require(leg != null, "Null route leg."); Point(leg.From); Point(leg.To); Known(leg.Mode, "Travel mode");
                            Bounded(leg.Minutes, double.Epsilon, 1000000, "Leg duration"); Text(leg.ServiceId, "Service ID", 256, true);
                            Require(Same(previous, leg.From), "Route legs are disconnected or start at the wrong place."); previous = leg.To;
                        }
                        Require(Same(previous, destination.Position), "Route ends at the wrong destination.");
                        Require(citizen.DepartureMinute <= state.Minute, "Journey departure is in the future.");
                        double arrival = citizen.DepartureMinute + route.Minutes;
                        // A completed physical lease rebases departure = clock - duration.
                        // Adding duration back can differ from clock by a few ulps. Normalize
                        // only this computed value when the explicit event is exactly queued
                        // at the current clock; persisted dates themselves remain strict.
                        double clockRoundoff = 4 * 2.2204460492503131e-16
                            * Math.Max(1, Math.Max(Math.Abs(state.Minute), Math.Abs(route.Minutes)));
                        if (citizen.NextEventMinute == state.Minute && Math.Abs(arrival - state.Minute) <= clockRoundoff)
                            arrival = state.Minute;
                        Require(arrival <= MaxMinute && Near(arrival, citizen.NextEventMinute), "Journey arrival does not match the next event.");
                        Require(arrival >= state.Minute, "Journey ended before the city clock.");
                        Point(route.PositionAt(state.Minute - citizen.DepartureMinute));
                    }
                }
                if (citizen.Phase == CitizenPhase.WaitingForOpening)
                {
                    Require(citizen.DestinationActivity != Activity.Home && citizen.NextEventMinute < citizen.ActivityUntilMinute,
                        "Opening wait must target an activity before its deadline.");
                    Require(DailyRoutine.Open(places[citizen.DestinationId], citizen.NextEventMinute),
                        "Opening wait does not end at an open venue.");
                }
                Require(citizen.RecentVisits != null && citizen.RecentVisits.Count <= MaxRecentVisits, "Invalid recent visit count.");
                embeddedRecords += citizen.RecentVisits!.Count;
                double lastVisit = 0;
                foreach (var visit in citizen.RecentVisits)
                {
                    Require(visit != null, "Null visit."); Minute(visit.AtMinute, "Visit minute"); Known(visit.Activity, "Visit activity");
                    var visited = Reference(places, visit.PlaceId);
                    Require((int)visited.Kind == (int)visit.Activity, "Visit activity does not match place.");
                    Require(visit.AtMinute >= lastVisit && visit.AtMinute <= state.Minute, "Visits are unordered or in the future."); lastVisit = visit.AtMinute;
                }
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
            }
            int totalRecords = ValidateTraffic(state, embeddedRecords);
            // The frozen semantic counter did not charge scope menu strings or optional-stop members;
            // CheckShape did. Keep both reported counts, but never let a write spend that difference.
            int streamingOnly = state.Citizens.Sum(c => c.OptionalStop?.Members.Count ?? 0)
                + (state.Food?.Shops.Sum(s => s.AdditionalProductIds.Count) ?? 0);
            Require(totalRecords + streamingOnly <= MaxEmbeddedRecords, "Too many embedded records in the streaming count.");
            return totalRecords;
            void CountAssignment(string id)
            {
                int count = assigned.TryGetValue(id, out int previous) ? previous + 1 : 1;
                if (count > places[id].Capacity) Fail("Place assignment exceeds capacity: " + id);
                assigned[id] = count;
            }
        }

        private static int ValidateTraffic(CityState state, int embeddedRecords)
        {
            Text(state.TrafficTopologyId, "Traffic topology ID", 64, true);
            if (state.Traffic == null)
            {
                Require(state.TrafficTopologyId == "", "Traffic topology has no kernel snapshot.");
                return embeddedRecords;
            }
            Require(state.TravelTimingVersion == 1, "Traffic requires native travel timing.");
            Require(state.TrafficTopologyId.Length == 64 && state.TrafficTopologyId.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'),
                "Invalid traffic topology fingerprint.");
            var traffic = state.Traffic;
            Require(traffic.Clock == state.Minute, "Traffic and city clocks disagree.");
            Minute(traffic.Clock, "Traffic clock"); Minute(traffic.RequestedTime, "Requested traffic clock");
            Require(traffic.Schema == 2 && traffic.RequestedTime >= traffic.Clock, "Invalid traffic schema or target.");
            Require(traffic.Definitions != null && traffic.Links != null && traffic.Vehicles != null && traffic.Events != null,
                "Missing traffic collections.");
            Count(traffic.Definitions.Count, MaxTrafficLinks); Count(traffic.Links.Count, MaxTrafficLinks);
            Count(traffic.Vehicles.Count, MaxCitizens); Count(traffic.Events.Count, MaxTrafficLinks);
            foreach (var definition in traffic.Definitions)
            {
                Require(definition != null, "Null traffic definition.");
                Require(definition.Capacity >= 1 && definition.Capacity <= MaxTrafficLinkCapacity, "Invalid traffic capacity.");
                Bounded(definition.FreeFlowMinutes, double.Epsilon, MaxMinute, "Traffic free-flow duration");
                Bounded(definition.HeadwayMinutes, 0, MaxMinute, "Traffic headway");
                if (definition.Signal != null)
                {
                    var signal = definition.Signal;
                    Bounded(signal.CycleMinutes, double.Epsilon, MaxMinute, "Traffic signal cycle");
                    Bounded(signal.GreenStartMinutes, 0, signal.CycleMinutes, "Traffic green start");
                    Bounded(signal.GreenDurationMinutes, 0, signal.CycleMinutes, "Traffic green duration");
                    Bounded(signal.PhaseOffsetMinutes, -MaxMinute, MaxMinute, "Traffic signal phase");
                }
            }
            foreach (var link in traffic.Links)
            {
                Require(link != null && link.Vehicles != null && link.WaitingSources != null && link.IncomingLinks != null,
                    "Missing traffic link collections.");
                Count(link.Vehicles.Count, MaxCitizens); Count(link.WaitingSources.Count, MaxCitizens); Count(link.IncomingLinks.Count, MaxTrafficLinks);
                Minute(link.NextDischargeAt, "Traffic next discharge");
                foreach (string id in link.Vehicles.Concat(link.WaitingSources)) Text(id, "Traffic vehicle reference", 256);
            }
            var citizens = state.Citizens.ToDictionary(c => c.Identity.Id, StringComparer.Ordinal);
            foreach (var vehicle in traffic.Vehicles)
            {
                Require(vehicle != null && vehicle.Route != null, "Missing traffic vehicle route.");
                Text(vehicle.VehicleId, "Traffic vehicle ID", 256); Count(vehicle.Route.Count, MaxRouteLegs);
                Minute(vehicle.EnqueuedAt, "Traffic enqueue time"); Minute(vehicle.EnteredAt, "Traffic entry time");
                Minute(vehicle.ReadyAt, "Traffic readiness"); Minute(vehicle.MotionStartedAt, "Traffic motion start");
                Minute(vehicle.NominalMinutes, "Traffic nominal progress");
                if (vehicle.CompletedAt.HasValue) Minute(vehicle.CompletedAt.Value, "Traffic completion time");
                Require(citizens.TryGetValue(vehicle.VehicleId, out var citizen) && citizen.Phase == CitizenPhase.Travelling
                    && citizen.Journey != null && citizen.Preferences.OwnsCar && citizen.Car != null
                    && citizen.Car.Id == citizen.Identity.Id + ":car", "Traffic vehicle lacks its travelling car owner.");
                int geometryIndex = 0; double geometryStart = 0, previousEnd = 0;
                foreach (var leg in vehicle.Route)
                {
                    Require(leg != null, "Null traffic route segment.");
                    Minute(leg.StartNominalMinutes, "Traffic geometry start"); Minute(leg.EndNominalMinutes, "Traffic geometry end");
                    Bounded(leg.FreeFlowMinutes, double.Epsilon, MaxMinute, "Traffic route free-flow duration");
                    Bounded(leg.StartLinkFraction, 0, 1, "Traffic section entry fraction");
                    Bounded(leg.EndLinkFraction, 0, 1, "Traffic section exit fraction");
                    Require(leg.StartLinkFraction < leg.EndLinkFraction, "Empty traffic spatial section.");
                    Require(leg.StartNominalMinutes >= previousEnd && leg.EndNominalMinutes > leg.StartNominalMinutes
                        && leg.EndNominalMinutes <= citizen.Journey.Minutes,
                        "Traffic geometry is outside its committed journey.");
                    previousEnd = leg.EndNominalMinutes;
                    while (geometryIndex < citizen.Journey.Legs.Count
                        && geometryStart + citizen.Journey.Legs[geometryIndex].Minutes <= leg.StartNominalMinutes)
                    {
                        geometryStart += citizen.Journey.Legs[geometryIndex].Minutes;
                        geometryIndex++;
                    }
                    int scanIndex = geometryIndex; double scanStart = geometryStart;
                    while (scanIndex < citizen.Journey.Legs.Count && scanStart < leg.EndNominalMinutes)
                    {
                        var geometry = citizen.Journey.Legs[scanIndex];
                        double end = scanStart + geometry.Minutes;
                        if (Math.Min(end, leg.EndNominalMinutes) > Math.Max(scanStart, leg.StartNominalMinutes))
                            Require(IsMovingTrafficGeometry(geometry),
                                "Traffic geometry includes a walk or stationary wait.");
                        if (end > leg.EndNominalMinutes) break;
                        scanStart = end; scanIndex++;
                    }
                    geometryIndex = scanIndex; geometryStart = scanStart;
                }
            }
            foreach (var item in traffic.Events) { Require(item != null, "Null traffic event."); Minute(item.At, "Traffic event time"); }
            TrafficKernel restored;
            try { restored = TrafficKernel.Restore(traffic, false); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { throw new SaveValidationException("Invalid traffic state: " + exception.Message); }
            foreach (var vehicle in traffic.Vehicles)
            {
                var citizen = citizens[vehicle.VehicleId];
                double expected = restored.GetPermission(vehicle.VehicleId).NominalMinutes;
                double actual = state.Minute - citizen.DepartureMinute;
                Require(Near(expected, actual), "Traffic progress and committed journey disagree.");
            }

            return embeddedRecords;
            void Count(int count, int limit)
            {
                Require(count <= limit, "Oversized traffic collection.");
                embeddedRecords += count;
                Require(embeddedRecords <= MaxEmbeddedRecords, "Too many embedded traffic records.");
            }
        }

        private static bool IsMovingTrafficGeometry(RouteLeg geometry)
        {
            double metres = Position.Distance(geometry.From, geometry.To);
            if (geometry.Mode != TravelMode.Car || !Position.Finite(metres) || metres <= 0) return false;
            if (metres > 0.000001) return true;
            // Clock retiming can retain a positive sub-micrometre native fragment. The codec
            // checks its tag shape; binding still proves its directed edge and geometry.
            string[] parts = geometry.ServiceId.Split(':');
            return parts.Length == 6 && parts[0] == "native" && parts[1] == "road" && parts[2] == "fragment1"
                && parts[3].Length == 64 && parts[3].All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
                && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out int from) && from >= 0
                && int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out int to) && to >= 0 && from != to;
        }

        private static void ValidateCommutePreparation(CitizenState citizen, Dictionary<string, Place> places, double minute)
        {
            var preparation = citizen.CommutePreparation;
            Require(preparation != null, "Missing commute preparation.");
            Text(preparation.PlaceId, "Commute preparation place", 256, true);
            Minute(preparation.WorkStartsAt, "Prepared work start");
            Minute(preparation.WorkEndsAt, "Prepared work end");
            Minute(preparation.PreparedAt, "Commute preparation minute");
            Bounded(preparation.TravelMinutes, 0, MaxRouteLegs * 1000000.0, "Prepared travel duration");
            if (preparation.PlaceId == "")
            {
                Require(preparation.WorkStartsAt == 0 && preparation.WorkEndsAt == 0
                    && preparation.PreparedAt == 0 && preparation.TravelMinutes == 0, "Empty commute preparation has measured values.");
                return;
            }
            Require(citizen.Identity.Persona != Persona.Retiree, "A retiree cannot prepare a work commute.");
            // A safe job change may leave the prior attempt until the next routine evaluation.
            Reference(places, preparation.PlaceId, citizen.Identity.Persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work);
            Require(preparation.WorkEndsAt > preparation.WorkStartsAt, "Prepared work window has no duration.");
            Require(preparation.PreparedAt <= minute && preparation.PreparedAt <= preparation.WorkStartsAt,
                "Commute preparation occurs in the future or after work starts.");
        }

        private static void ValidateExpansion(CityState state)
        {
            Require(state.SourceIdentities != null && state.SourceIdentities.Count > 0 && state.SourceIdentities.Count <= 5000,
                "Invalid immutable identity source catalogue size.");
            Require(state.NextResidentSequence >= 1 && state.NextResidentSequence < int.MaxValue,
                "Invalid resident sequence.");
            Require(state.PopulationTarget == state.Citizens.Count, "Active population differs from its housing allocation.");
            Require(state.InitialImportedResidents >= 0 && state.InitialImportedResidents <= state.SourceIdentities!.Count
                && state.InitialImportedResidents <= state.PopulationTarget, "Invalid imported resident count.");
            Require((long)state.Citizens.Count == (long)state.InitialImportedResidents + state.NextResidentSequence - 1,
                "Resident creation and active counts disagree.");
            var source = new Dictionary<string, CitizenIdentity>(StringComparer.Ordinal);
            foreach (var identity in state.SourceIdentities!)
            {
                ValidateIdentity(identity);
                Require(!identity.Id.StartsWith("pc:", StringComparison.Ordinal) && !source.ContainsKey(identity.Id), "Invalid or duplicate source ID.");
                source.Add(identity.Id, identity);
            }
            var importedIds = new HashSet<string>(source.Keys.OrderBy(id => id, StringComparer.Ordinal).Take(state.InitialImportedResidents), StringComparer.Ordinal);
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var citizen in state.Citizens)
            {
                Require(citizen != null, "Null citizen.");
                Provenance(citizen.Identity);
                Require(active.Add(citizen.Identity.Id), "Duplicate active ID.");
                Minute(citizen.JoinedMinute, "Joined minute");
                Require(citizen.JoinedMinute <= state.Minute, "Resident joins in the future.");
                Require(citizen.LastEmploymentCheckDay >= (int)(citizen.JoinedMinute / 1440)
                    && citizen.LastEmploymentCheckDay <= (int)(state.Minute / 1440), "Invalid daily employment checkpoint.");
                Require(citizen.JobChanges >= 0, "Invalid job-change count.");
                if (citizen.Car != null)
                {
                    Require(citizen.Preferences != null && citizen.Preferences.OwnsCar, "Unowned persistent car.");
                    Require(citizen.Car.Id == citizen.Identity.Id + ":car", "Car identity does not belong to resident.");
                    Point(citizen.Car.ParkedPosition);
                    Text(citizen.Car.ModelId, "Car model ID", 128, true);
                }
                if (citizen.Journey?.Legs != null)
                {
                    bool began = false, ended = false;
                    foreach (var leg in citizen.Journey.Legs)
                    {
                        Require(leg != null, "Null route leg.");
                        if (leg.Mode == TravelMode.Car)
                        {
                            Require(citizen.Car != null && !ended, "Car journey has no car or multiple driving blocks.");
                            if (!began) Require(Position.Distance(citizen.Car!.ParkedPosition, leg.From) <= 0.01, "Car journey starts away from its parked car.");
                            began = true;
                        }
                        else if (began) ended = true;
                    }
                    if (began) Require(citizen.Journey.Legs.All(l => l.Mode == TravelMode.Car || l.Mode == TravelMode.Walk),
                        "A car journey can only contain driving and walking access.");
                }
            }
            void Provenance(CitizenIdentity identity)
            {
                ValidateIdentity(identity);
                if (source.TryGetValue(identity.Id, out var original))
                    Require(importedIds.Contains(identity.Id) && Population.EqualIdentity(identity, original), "Imported identity is unallocated or differs from immutable source.");
                else
                {
                    Require(identity.Id.StartsWith("pc:", StringComparison.Ordinal), "Unknown source identity.");
                    string suffix = identity.Id.Substring(3);
                    Require(int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                        && sequence >= 1 && sequence < state.NextResidentSequence
                        && suffix == sequence.ToString(CultureInfo.InvariantCulture), "Generated resident ID is invalid or not yet allocated.");
                }
            }
        }

        private sealed class FoodPlaceLookup
        {
            private readonly List<Place> source;
            private readonly HashSet<(string, int)>? keys;
            public FoodPlaceLookup(List<Place> source)
            {
                this.source = source;
                if (source.Count > MaxPlaces || source.Any(p => p == null)) return;
                keys = new HashSet<(string, int)>();
                foreach (var place in source) keys.Add((place.Id, (int)place.Kind));
            }
            public bool Contains(string id, int kind) => keys != null
                ? keys.Contains((id, kind))
                : source.Any(p => p.Id == id && (int)p.Kind == kind);
            // Used only for the at most one route-bearing decision.
            public Place? Find(string id) => source.FirstOrDefault(p => p != null && p.Id == id);
        }

        /// <summary>Returns the number of nested food records counted toward MaxEmbeddedRecords.</summary>
        private static int ValidateFood(CityState state)
        {
            Require(state != null && state.Citizens != null && state.Places != null, "Missing food population.");
            var food = state.Food;
            if (food == null) { Require(state.Citizens.All(c => c != null && c.MealOuting == null), "Meal outing without economy."); return 0; }
            int records = ValidateBusinessReferences(state), routeBearing = 0;
            int currentDay = (int)Math.Floor(state.Minute / 1440);
            Require(food.ChoicePolicyVersion == FoodChoicePolicy.Version, "Unknown food choice policy.");
            Require(food.ChoiceStartsDay >= 0 && food.ChoiceStartsDay <= currentDay + 1, "Invalid food choice activation day.");
            Require(food.AwarenessStartsDay>=0 && food.AwarenessStartsDay<=currentDay+1,"Invalid awareness activation day.");
            Require(food.Shops != null && food.Shops.Count <= 1024, "Invalid food scope.");
            Require(food.RepresentationFactor >= 1 && food.RepresentationFactor <= 3, "Invalid consumer representation factor.");
            Require(food.Residents != null && food.Residents.Count == state.Citizens.Count * food.RepresentationFactor, "Meal roster differs from represented population.");
            Require(food.Districts != null, "Missing food budget districts.");
            FoodHouseholdPolicy.ValidateMixes(food.Districts);
            Require(food.HouseholdFinanceVersion == 0 || food.HouseholdFinanceVersion == HouseholdFinance.PolicyVersion, "Unknown household finance policy.");
            Require(food.MealRhythmVersion == 0 || food.MealRhythmVersion == MealRhythm.PolicyVersion, "Unknown meal rhythm policy.");
            // Duplicate IDs/kinds must not fail earlier than the existing catalogue validation.
            // A malformed null entry retains Any's original short-circuit/exception behavior.
            var foodPlaces = new FoodPlaceLookup(state.Places);
            var shops = new HashSet<string>(StringComparer.Ordinal);
            var businessIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shop in food.Shops)
            {
                Require(shop != null && shops.Add(shop.PlaceId) && businessIds.Add(shop.BusinessId), "Duplicate food scope.");
                Text(shop.PlaceId, "Food place", 256); Text(shop.ProductId, "Food product", 256); Text(shop.BusinessId, "Food business", 512);
                Require(shop.AdditionalProductIds != null && shop.AdditionalProductIds.Count <= 3
                    && shop.Products().Distinct(StringComparer.Ordinal).Count() == shop.AdditionalProductIds.Count + 1, "Invalid food menu.");
                foreach (var product in shop.AdditionalProductIds) Text(product, "Food menu product", 256);
                Require(foodPlaces.Contains(shop.PlaceId, (int)PlaceKind.Shop), "Unknown food shop.");
                Minute(shop.NextServiceAt, "Food service clock");
            }
            Require(food.History != null && food.History.Count <= 14 * MaxPlaces, "Oversized market history.");
            var historyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in food.History)
            {
                Require(row != null && row.Day >= 0 && row.Day <= (int)(state.Minute / 1440) && foodPlaces.Contains(row.PlaceId, (int)PlaceKind.Shop)
                    && historyKeys.Add(row.Day + ":" + row.PlaceId + "\n" + row.BusinessId), "Invalid market history ownership.");
                Text(row.BusinessId, "Historical business", 512);
                Require(row.OffersConsidered >= 0 && row.ChosenOffers >= 0 && row.PriceRejections >= 0
                    && row.AccessRejections >= 0 && row.ClosedRejections >= 0 && row.StockRejections >= 0 && row.StaffRejections >= 0
                    && row.ServiceRejections >= 0 && row.UnknownEvidence >= 0 && row.PreferredAlternatives >= 0
                    && row.Purchases >= 0 && row.ExistingStopPurchases >= 0 && row.ExistingStopPurchases <= row.Purchases
                    && row.Revenue >= Money.Zero, "Invalid market history counters.");
                Require(row.OffersConsidered == checked(row.ChosenOffers + row.PriceRejections + row.AccessRejections
                    + row.ClosedRejections + row.StockRejections + row.StaffRejections + row.ServiceRejections + row.UnknownEvidence + row.PreferredAlternatives),
                    "Market offer outcomes do not reconcile.");
                Require(row.PurchaseHours != null && row.PurchaseHours.Length == 24 && row.PurchaseHours.All(n => n >= 0)
                    && row.PurchaseHours.Sum() == row.Purchases, "Hourly purchases do not reconcile.");
                Require(row.PurchaseMeals != null && row.PurchaseMeals.Length == 4 && row.PurchaseMeals.All(n => n >= 0)
                    && row.PurchaseMeals.Sum() == row.Purchases, "Meal purchases do not reconcile.");
                Require(row.ProductSales != null && row.ProductSales.Count <= 4 && row.ProductSales.All(p => p != null)
                    && row.ProductSales.Select(p => p.ProductId).Distinct(StringComparer.Ordinal).Count() == row.ProductSales.Count,
                    "Invalid product sales history.");
                foreach (var product in row.ProductSales)
                    Require(product != null && !string.IsNullOrWhiteSpace(product.ProductId)
                        && product.Purchases > 0 && product.Revenue >= Money.Zero && product.PurchaseMeals != null
                        && product.PurchaseMeals.Length == 4 && product.PurchaseMeals.All(n => n >= 0)
                        && product.PurchaseMeals.Sum() == product.Purchases, "Invalid product purchase counters.");
                Require(row.ProductSales.Sum(p => p.Purchases) == row.Purchases
                    && Money.Sum(row.ProductSales.Select(p => p.Revenue)) == row.Revenue
                    && Enumerable.Range(0, 4).All(k => row.ProductSales.Sum(p => p.PurchaseMeals[k]) == row.PurchaseMeals[k]),
                    "Product sales do not reconcile.");
                Require(row.BuyerDistricts != null && row.BuyerDistricts.Count <= 100
                    && row.BuyerDistricts.All(d => d != null && d.Purchases > 0)
                    && row.BuyerDistricts.Select(d => d.DistrictId).Distinct(StringComparer.Ordinal).Count() == row.BuyerDistricts.Count
                    && row.BuyerDistricts.Sum(d => d.Purchases) == row.Purchases, "Customer districts do not reconcile.");
                foreach (var district in row.BuyerDistricts) Text(district.DistrictId, "Buyer district", 256, emptyAllowed: true);
            }
            var citizens = state.Citizens.ToDictionary(c => c.Identity.Id, StringComparer.Ordinal);
            Require(food.RoutineStops != null && food.RoutineStops.Count <= citizens.Count, "Invalid shared routine stops.");
            var stopOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stop in food.RoutineStops)
            {
                Require(stop != null && citizens.ContainsKey(stop.AnchorId) && stopOwners.Add(stop.AnchorId), "Invalid routine stop owner.");
                Known(stop.Activity, "Shared routine activity");
                Require(stop.Activity != Activity.Shop && foodPlaces.Contains(stop.PlaceId, (int)stop.Activity), "Invalid shared routine place.");
                Minute(stop.CapturedAt, "Routine capture"); Minute(stop.DepartureAt, "Routine departure");
                Require(stop.CapturedAt <= state.Minute && stop.DepartureAt > stop.CapturedAt, "Invalid routine opportunity.");
            }
            var memoryReferences = new HashSet<int>(food.BusinessReferences.Select(r => r.Slot));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var meal in food.Residents)
            {
                Require(meal != null && seen.Add(meal.ResidentId) && citizens.ContainsKey(meal.AnchorId)
                    && meal.Slot >= 0 && meal.Slot < food.RepresentationFactor
                    && meal.ResidentId == FoodRepresentation.ConsumerId(meal.AnchorId, meal.Slot), "Unknown or duplicate consumer/anchor slot.");
                Require(meal.PreferenceBonusCents >= 0 && meal.PreferenceBonusCents <= 150
                    && (meal.PreferredProductId.Length == 0 ? meal.PreferenceBonusCents == 0 : FoodProducts.Known(meal.PreferredProductId)), "Invalid consumer taste.");
                Require(meal.Slot > 0 || meal.Excursion == null && !meal.NeedsHomeResync, "Physical anchor cannot own an abstract excursion.");
                Require(meal.Slot == 0 || !meal.ExistingStop, "Represented consumer cannot claim anchor's existing-stop purchase.");
                if (meal.Slot == 0) Require(meal.RepresentedTraits == null, "Slot zero duplicates its anchor's traits.");
                else Traits(meal.RepresentedTraits, "represented consumer");
                Known(meal.Status, "Meal status");
                Known(meal.Kind, "Meal kind");
                Require(food.MealRhythmVersion != 0 || meal.Kind == MealKind.Legacy, "Meal kind differs from rhythm policy.");
                Require(meal.DayAllowanceUsed >= Money.Zero && meal.DayAllowanceUsed <= MaximumAllowance, "Invalid daily food allowance use.");
                Require((meal.Budget != null) == (food.HouseholdFinanceVersion != 0), "Household budget ownership differs from policy.");
                if (meal.Budget != null) ValidateBudget(meal.Budget, state.Minute);
                Require(meal.Day >= 0 && meal.Day <= 100000 && meal.Allowance >= Money.Zero && meal.Allowance <= MaximumAllowance
                    && meal.Price >= Money.Zero && meal.Price <= meal.Allowance && meal.Purchases >= 0 && meal.Spent >= Money.Zero,
                    "Invalid meal allowance/accounting.");
                Minute(meal.DesiredAt, "Meal appetite time"); Minute(meal.ExpiresAt, "Meal expiry"); Minute(meal.DueAt, "Meal event");
                Minute(meal.ServiceAt, "Meal service time"); Minute(meal.Deadline, "Meal return deadline");
                Bounded(meal.ServiceMinutes, 0, 120, "Meal service duration"); Bounded(meal.ReturnMinutes, 0, 1440, "Meal return duration");
                Require(meal.DesiredAt >= meal.Day * 1440.0 && meal.DesiredAt < (meal.Day + 1) * 1440.0
                    && meal.ExpiresAt > meal.DesiredAt && meal.ExpiresAt <= (meal.Day + 1) * 1440.0
                    && meal.ExpiresAt <= meal.DesiredAt + 240 && meal.DueAt >= state.Minute, "Invalid meal schedule.");
                Text(meal.PlaceId, "Meal shop", 256, true); Text(meal.BusinessId, "Meal business", 512, true);
                Text(meal.ProductId, "Meal product", 256, true); Text(meal.Reason, "Meal reason", 512, true);
                if (meal.PlaceId.Length > 0)
                    Require(foodPlaces.Contains(meal.PlaceId, (int)PlaceKind.Shop) && meal.BusinessId.Length > 0 && meal.ProductId.Length > 0, "Meal lost its historical business/place identity.");
                if (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving)
                    Require(meal.PlaceId.Length > 0 && meal.Price > Money.Zero && meal.ServiceMinutes > 0
                        && meal.Deadline <= meal.ExpiresAt, "Active meal lost its outing.");
                Require(meal.ExistingStopPurchases >= 0 && meal.ExistingStopPurchases <= meal.Purchases, "Invalid existing-stop purchase count.");
                Require(!meal.ExistingStop || meal.PlaceId.Length > 0 && meal.ReturnMinutes == 0
                    && meal.Status != MealStatus.Travelling, "Existing-stop purchase cannot own a second trip.");
                ValidateExcursion(meal, state, foodPlaces);
                records += ValidateToday(meal, food, currentDay, state.Minute);
                records += ValidateMemory(meal.Memory, currentDay, memoryReferences);
                records += ValidateDecision(meal, food, foodPlaces, ref routeBearing);
                Require(records <= MaxEmbeddedRecords, "Too many embedded food records.");
                var outing = meal.Slot == 0 ? citizens[meal.AnchorId].MealOuting : null;
                Require(!meal.ExistingStop || outing == null, "Existing-stop purchase also owns an outing.");
                if (outing != null)
                {
                    Text(outing.OriginId, "Meal origin", 256); Known(outing.OriginActivity, "Meal origin activity");
                    Require(foodPlaces.Contains(outing.OriginId, (int)outing.OriginActivity), "Invalid meal origin.");
                    Minute(outing.ResumeUntil, "Meal resume end"); Minute(outing.ResumeEvent, "Meal resume event");
                    Require(outing.ResumeEvent <= outing.ResumeUntil && meal.PlaceId.Length > 0, "Invalid meal return ownership.");
                }
            }
            return records;
        }

        private static void Traits(ResidentTraits? traits, string owner)
        {
            if (traits == null) Fail("Missing " + owner + " traits.");
            if (traits!.Version != ResidentTraits.CurrentVersion) Fail("Unknown " + owner + " trait version.");
            if (!traits.IsValid) Fail("Invalid " + owner + " trait bands.");
        }

        private static void OpeningWindowRules(OpeningWindow window)
        {
            Require(window != null, "Null weekly opening window.");
            Require(window!.Day >= 0 && window.Day <= 6, "Unknown weekly opening day.");
            Require(window.FromMinute >= 0 && window.FromMinute <= 1440 && window.ToMinute >= 0 && window.ToMinute <= 1440,
                "Invalid weekly opening minute.");
            int duration = window.ToMinute > window.FromMinute ? window.ToMinute - window.FromMinute : 1440 - window.FromMinute + window.ToMinute;
            Require(window.FromMinute != window.ToMinute && duration > 0, "Weekly opening window has no duration.");
        }

        private static int ValidateToday(ResidentMeal meal, FoodCommerceState food, int currentDay, double minute)
        {
            var today = meal.Today;
            if (today == null) return 1; // one rollup slot per consumer; null retains the original absence
            bool legacy = food.MealRhythmVersion == 0;
            Require(today.Day >= 0 && today.Day <= currentDay && today.Day <= meal.Day, "Invalid current-day meal outcomes.");
            Require(today.Unrecorded >= 0 && today.Unrecorded <= (legacy ? 1 : 3), "Invalid unrecorded meal count.");
            Require(today.Meals != null && today.Meals.Count <= (legacy ? 1 : MaxMealOutcomes), "Oversized current-day meal outcomes.");
            var previous = (MealKind)(-1);
            foreach (var record in today.Meals!)
            {
                Require(record != null, "Null meal outcome.");
                Dense((int)record!.Kind, (int)MealKind.Dinner, "Meal outcome kind"); Dense((int)record.Status, (int)MealOutcomeStatus.Unconfirmed, "Meal outcome status");
                Dense((int)record.Reason, (int)MealReasonCode.FundsUnavailable, "Meal outcome reason");
                Dense((int)record.Outside, (int)FoodOutsideOption.FundedHouseholdFood, "Meal outcome alternative");
                Dense((int)record.Factor1, (int)MealFactor.Household, "Meal outcome factor"); Dense((int)record.Factor2, (int)MealFactor.Household, "Meal outcome factor");
                Require(record.ProductId.IndexOf('|') < 0, "Meal outcome product cannot contain '|'.");
                Require(legacy ? record.Kind == MealKind.Legacy : record.Kind != MealKind.Legacy, "Meal outcome kind differs from rhythm policy.");
                Require(record.Kind > previous, "Duplicate or unordered meal outcomes.");
                previous = record.Kind;
                Require(MealOutcomes.Index(record.Kind) >= today.Unrecorded, "Outcome recorded for a meal marked unrecorded.");
                Require(record.Factor2 == MealFactor.None || record.Factor1 != MealFactor.None && record.Factor1 != record.Factor2, "Invalid meal outcome factors.");
                Text(record.PlaceId, "Meal outcome shop", 256, true); Text(record.ProductId, "Meal outcome product", 256, true);
                double dayStart = today.Day * 1440.0;
                Require(record.At >= dayStart && record.At <= minute, "Invalid meal outcome time.");
                Require(record.CheckoutAt == 0 || record.CheckoutAt >= dayStart && record.CheckoutAt <= record.At, "Invalid meal checkout time.");
                Require(record.Outside == FoodOutsideOption.None || record.Status == MealOutcomeStatus.ChoseAlternative, "Alternative recorded for a non-alternative outcome.");
                bool purchase = record.Status == MealOutcomeStatus.Bought || record.Status == MealOutcomeStatus.Unconfirmed;
                Require(!purchase || record.PlaceId.Length > 0 && record.ProductId.Length > 0, "Purchase outcome lost its shop or product.");
                Require(purchase || record.PlaceId.Length == 0 && record.ProductId.Length == 0, "Non-purchase outcome names a shop or product.");
                Require(record.Status != MealOutcomeStatus.Unconfirmed || record.Reason == MealReasonCode.CheckoutUnconfirmed, "Impossible unconfirmed outcome reason.");
                Require(record.Status == MealOutcomeStatus.Bought == (record.Reason == MealReasonCode.Bought || record.Reason == MealReasonCode.ReceiptConfirmed),
                    "Impossible purchase outcome reason.");
                // The outcome of the meal still held by this record must agree with its live status.
                if (today.Day == meal.Day && record.Kind == meal.Kind)
                {
                    var live = MealOutcomes.StatusFor(meal.Status);
                    Require(live.HasValue ? live.Value == record.Status
                        : record.Status == MealOutcomeStatus.Skipped || record.Status == MealOutcomeStatus.ChoseAlternative,
                        "Meal outcome differs from its receipt state.");
                    // A current purchase must name the retained meal's own shop, product and checkout minute; the
                    // inspector shows this record, so it cannot contradict the authoritative meal. Once the meal
                    // advances, the record stays an opaque historical reference.
                    if (purchase)
                        Require(record.PlaceId == meal.PlaceId && record.ProductId == meal.ProductId
                            && record.CheckoutAt == MealOutcomes.CheckoutMinute(meal, minute), "Purchase outcome differs from its retained meal.");
                }
            }
            return 1; // schema 18 fixed current-day tuple (including an empty rollup)
        }

        private static int ValidateDecision(ResidentMeal meal, FoodCommerceState food, FoodPlaceLookup foodPlaces, ref int routeBearing)
        {
            Text(meal.LastContextKey, "Decision context", 256, true);
            Require(meal.ContextAttempts >= 0 && meal.ContextAttempts <= FoodChoicePolicy.MaximumContextsPerMeal
                && (meal.ContextAttempts == 0) == (meal.LastContextKey.Length == 0), "Invalid decision retry latch.");
            var decision = meal.Decision;
            if (decision == null) return 0;
            Require(meal.Status == MealStatus.Pending && meal.Day >= food.ChoiceStartsDay, "Decision outside a pending version-1 meal.");
            Require(decision.PolicyVersion == food.ChoicePolicyVersion && decision.IntentId == meal.IntentId, "Decision belongs to another policy or meal.");
            Require(decision.ContextKey.Length > 0 && decision.ContextKey == meal.LastContextKey && decision.Attempt == meal.ContextAttempts,
                "Decision differs from its retry latch.");
            Dense((int)decision.Result, (int)FoodDecisionResult.NoFeasible, "Decision result");
            Dense((int)decision.Outside, (int)FoodOutsideOption.FundedHouseholdFood, "Decision alternative");
            Dense((int)decision.Reason, (int)MealReasonCode.FundsUnavailable, "Decision reason");
            Dense((int)decision.Factor1, (int)MealFactor.Household, "Decision factor"); Dense((int)decision.Factor2, (int)MealFactor.Household, "Decision factor");
            Require(decision.Factor2 == MealFactor.None || decision.Factor1 != MealFactor.None && decision.Factor1 != decision.Factor2, "Invalid decision factors.");
            Minute(decision.OriginAt, "Decision origin"); Minute(decision.Deadline, "Decision deadline");
            Require(decision.Deadline >= decision.OriginAt, "Decision deadline precedes its origin.");
            Text(decision.BusinessId, "Decision business", 512, true); Text(decision.ProductId, "Decision product", 256, true);
            Require((decision.Result == FoodDecisionResult.Selected) == (decision.BusinessId.Length > 0 && decision.ProductId.Length > 0)
                && (decision.Result == FoodDecisionResult.Selected || decision.BusinessId.Length == 0 && decision.ProductId.Length == 0),
                "Decision selection differs from its result.");
            Require((decision.Result == FoodDecisionResult.Outside) == (decision.Outside != FoodOutsideOption.None), "Decision alternative differs from its result.");
            // Also for a compact decision without its shortlist image.
            Require(decision.ProductId.Length == 0 || FoodProducts.Known(decision.ProductId), "Unknown live product code.");
            Require(decision.Shops != null && decision.Shops.Count <= FoodChoicePolicy.MaximumShortlist, "Oversized decision shortlist.");
            Require(decision.QuotesDone >= 0 && decision.QuotesDone <= 2 * decision.Shops!.Count, "Invalid decision quote progress.");
            // Choosing follows every feasibility answer; an undecided image may stop at any slot.
            Require(decision.Result == FoodDecisionResult.Evaluating || decision.QuotesDone == 2 * decision.Shops.Count,
                "Decision chose before its quotes were settled.");
            // The simulator holds one decision, including an empty-shortlist decision. Bound the tuning
            // header as well as routes; otherwise empty images could spend a city-wide tuning history.
            Dense((int)decision.Weather,2,"decision weather");
            Bounded(decision.WeatherTravelMultiplier,1,2,"weather travel multiplier");
            Require(decision.Weather==WeatherSample.Rain || decision.WeatherTravelMultiplier==1,"Nonneutral dry/unknown weather.");
            Require(++routeBearing <= FoodChoicePolicy.MaximumRouteBearingDecisions, "More than one held food decision.");
            if (decision.Shops.Count == 0) return ValidateAwareness(decision,meal,food);
            int records = ValidateAwareness(decision,meal,food);
            var businesses = new HashSet<string>(StringComparer.Ordinal);
            bool selected = decision.Result != FoodDecisionResult.Selected;
            int slot = 0;
            foreach (var shop in decision.Shops)
            {
                Require(shop != null && businesses.Add(shop.BusinessId), "Duplicate decision shop.");
                Require(Position.Finite(shop!.MemoryContribution) && shop.MemoryContribution >= -8 && shop.MemoryContribution <= 8,
                    "Invalid held memory contribution.");
                // B1 executes the finite B0b scalar; older B0b binaries deliberately refuse it.
                Text(shop!.BusinessId, "Decision shop business", 512);
                Require(foodPlaces.Contains(shop.PlaceId, (int)PlaceKind.Shop), "Unknown decision shop.");
                Require(shop.MealsPerHour == -1 || Position.Finite(shop.MealsPerHour) && shop.MealsPerHour >= 0 && shop.MealsPerHour <= 100000,
                    "Invalid decision service evidence.");
                Minute(shop.NextServiceAt, "Decision service clock");
                Require(shop.CustomerServicePercent >= -1 && shop.CustomerServicePercent <= 100, "Invalid decision quality evidence.");
                Text(shop.DistrictId, "Decision shop district", 256, true);
                Require(shop.Hours != null && shop.Hours.Count <= MaxOpeningWindows, "Invalid decision opening evidence.");
                foreach (var window in shop.Hours!) OpeningWindowRules(window);
                records += shop.Hours.Count;
                Require(shop.Offers != null && shop.Offers.Count <= Schema18MaximumOffersPerShop, "Oversized decision menu.");
                var products = new HashSet<string>(StringComparer.Ordinal);
                foreach (var offer in shop.Offers!)
                {
                    Require(offer != null && products.Add(offer.ProductId), "Duplicate decision offer.");
                    Require(FoodProducts.Known(offer!.ProductId), "Unknown live product code.");
                    Require(offer.Price >= Money.Zero && offer.Price <= MaximumAllowance && offer.Units >= -1, "Invalid decision offer evidence.");
                    Require(offer.DistrictDemandPercent >= -1 && offer.DistrictDemandPercent <= 100
                        && (offer.DistrictDemandPercent == -1 || shop.DistrictId.Length > 0), "Invalid decision district evidence.");
                    if (shop.BusinessId == decision.BusinessId && offer.ProductId == decision.ProductId)
                        selected = shop.OutwardQuote == FoodQuoteState.Answered && shop.ReturnQuote == FoodQuoteState.Answered;
                }
                records += 1 + shop.Offers.Count;
                Dense((int)shop.OutwardQuote, (int)FoodQuoteState.NotNeeded, "Decision quote state");
                Dense((int)shop.ReturnQuote, (int)FoodQuoteState.NotNeeded, "Decision quote state");
                // Slots before the cursor are settled, slots from it on are not.
                Require((shop.OutwardQuote != FoodQuoteState.Unrequested) == (slot++ < decision.QuotesDone)
                    && (shop.ReturnQuote != FoodQuoteState.Unrequested) == (slot++ < decision.QuotesDone), "Decision quote progress differs from its cursor.");
                Require((shop.Outward != null) == (shop.OutwardQuote == FoodQuoteState.Answered)
                    && (shop.Return != null) == (shop.ReturnQuote == FoodQuoteState.Answered), "Decision route differs from its quote state.");
                // A return is asked only after an answered outward quote; otherwise its slot is settled unasked.
                Require(shop.OutwardQuote == FoodQuoteState.Answered
                    ? shop.ReturnQuote != FoodQuoteState.NotNeeded
                    : shop.ReturnQuote == FoodQuoteState.Unrequested || shop.ReturnQuote == FoodQuoteState.NotNeeded, "Return quote without its outward quote.");
                if (shop.Outward != null) records += DecisionRoute(shop.Outward, foodPlaces, shop.PlaceId, outward: true);
                if (shop.Return != null) records += DecisionRoute(shop.Return, foodPlaces, shop.PlaceId, outward: false);
            }
            Require(selected, "Decision selects an offer outside its shortlist.");
            return records;
        }

        // A captured walking answer: connected, finite and bounded. The shop end must be the shop's own position;
        // the origin end is checked against the live stop when the decision resumes.
        private static int DecisionRoute(Route route, FoodPlaceLookup foodPlaces, string shopId, bool outward)
        {
            Require(route.Legs != null && route.Legs.Count > 0 && route.Legs.Count <= MaxRouteLegs, "Missing or oversized decision route.");
            Position? previous = null;
            foreach (var leg in route.Legs!)
            {
                Require(leg != null && leg.Mode == TravelMode.Walk, "Decision route is not a walking path.");
                Point(leg!.From); Point(leg.To); Bounded(leg.Minutes, 0, 1440, "Decision leg duration");
                Text(leg.ServiceId, "Decision leg service", 256, true);
                Require(previous == null || Same(previous.Value, leg.From), "Inconsistent decision route endpoints.");
                previous = leg.To;
            }
            var shop = foodPlaces.Find(shopId);
            Require(shop != null && Same(outward ? route.Legs[route.Legs.Count - 1].To : route.Legs[0].From, shop!.Position), "Inconsistent decision route endpoints.");
            return route.Legs.Count;
        }

        private static void ValidateExcursion(ResidentMeal meal, CityState state, FoodPlaceLookup foodPlaces)
        {
            var excursion = meal.Excursion;
            if (meal.Slot > 0 && (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving))
                Require(excursion != null, "Active represented meal lost its excursion.");
            if (excursion == null) return;
            Require(meal.Slot > 0 && !meal.NeedsHomeResync && meal.Status != MealStatus.Pending
                && excursion.DestinationId == meal.PlaceId, "Invalid represented excursion owner.");
            Known(excursion.OriginActivity, "Excursion origin activity");
            Require(excursion.ReturnGeometry.Length == 44, "Missing committed return geometry signature.");
            Text(excursion.UnresolvedReason, "Excursion unresolved reason", 256, true);
            Require(excursion.Unresolved == (excursion.UnresolvedReason.Length != 0), "Excursion unresolved evidence differs from state.");
            Require(excursion.MaximumQueueMinutes >= 0 && excursion.MaximumQueueMinutes <= 60, "Invalid represented queue limit.");
            Require(foodPlaces.Contains(excursion.OriginId, (int)excursion.OriginActivity)
                && foodPlaces.Contains(excursion.DestinationId, (int)PlaceKind.Shop), "Invalid excursion endpoints.");
            Require(excursion.OriginPosition.IsFinite && excursion.DestinationPosition.IsFinite, "Invalid excursion endpoint geometry.");
            Minute(excursion.DepartedAt, "Excursion departure"); Minute(excursion.ArrivesAt, "Excursion arrival");
            Minute(excursion.AdmissionAt, "Excursion admission");
            Minute(excursion.SharedDepartureAt, "Excursion shared departure"); Minute(excursion.ReturnStartsAt, "Excursion return start");
            Minute(excursion.ReturnsAt, "Excursion return completion");
            Require(excursion.DepartedAt <= state.Minute && excursion.ArrivesAt > excursion.DepartedAt
                && excursion.ArrivesAt <= meal.Deadline && meal.Deadline <= excursion.SharedDepartureAt,
                "Excursion does not fit its committed opportunity.");
            Require(excursion.AdmissionAt >= excursion.ArrivesAt && excursion.AdmissionAt < meal.Deadline, "Invalid represented admission time.");
            Require(!excursion.Returning || excursion.ReturnsAt > excursion.ReturnStartsAt
                && excursion.ReturnsAt <= excursion.SharedDepartureAt && excursion.ReturnsAt <= meal.Deadline,
                "Invalid represented return interval.");
            Require(!excursion.Unresolved || meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving,
                "Unresolved excursion cannot remain serviceable.");
            Require(!excursion.Returning || meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving,
                "Returning excursion cannot purchase.");
        }

        private static void ValidateBudget(ResidentBudget b, double minute)
        {
            Known(b.IncomeSource, "Resident income source");
            Require(b.BaseIncomeCents > 0 && b.BaseIncomeCents <= 1000000
                && b.DailyIncomeCents >= 0 && b.DailyIncomeCents <= 1000000
                && b.DailyHousingCents >= 0 && b.DailyHousingCents <= 1000000
                && b.DailyEssentialsCents >= 0 && b.DailyEssentialsCents <= 1000000
                && b.DailyHomeFoodCents > 0 && b.DailyHomeFoodCents <= 1000000, "Invalid household policy amounts.");
            Require(b.OpeningBalance >= Money.Zero && b.Balance >= Money.Zero && b.IncomeReceived >= Money.Zero
                && b.EssentialsPaid >= Money.Zero && b.HomeFoodPaid >= Money.Zero && b.PreparedFoodSpent >= Money.Zero
                && b.HeldPurchase >= Money.Zero && b.HeldPurchase <= MaximumAllowance && b.Arrears >= Money.Zero,
                "Negative household accounting.");
            // Checked unit sums: an amount beyond the unit's range is refused (OverflowException), never rounded.
            Require(HouseholdFinance.CashConserved(b), "Household cash is not conserved.");
            Minute(b.NextPostingMinute, "Next household posting");
            Require(b.LastPostedDay >= -1 && b.LastPostedDay <= (int)(minute / 1440)
                && b.HomeFoodFundedDay >= -1 && b.HomeFoodFundedDay <= b.LastPostedDay
                && b.NextPostingMinute >= minute && b.DaysWithoutHomeFood >= 0
                && (b.LastPostedDay < 0 || b.NextPostingMinute == (b.LastPostedDay + 1) * 1440.0), "Invalid household posting ownership.");
        }

        private static void ValidateIdentity(CitizenIdentity identity)
        {
            Require(identity != null, "Null identity.");
            Text(identity.Id, "Citizen ID", 256); Text(identity.Name, "Citizen name", 512); Text(identity.District, "Citizen district", 256, true);
            Require(identity.Age >= 0 && identity.Age <= 130, "Invalid citizen age."); Known(identity.Persona, "Persona");
        }

        private static Place Reference(Dictionary<string, Place> places, string id, params PlaceKind[] kinds)
        {
            Text(id, "Place reference", 256);
            if (!places.TryGetValue(id, out var place)) Fail("Unknown place reference: " + id);
            if (kinds.Length != 0 && Array.IndexOf(kinds, place!.Kind) < 0) Fail("Place reference has the wrong role: " + id);
            return place!;
        }
        private static void OptionalReference(Dictionary<string, Place> places, string id, params PlaceKind[] kinds)
        { Require(id != null, "Null place reference."); if (id != "") Reference(places, id, kinds); }
        // Messages are built only on failure: these checks run for every field of every save.
        private static void Known<T>(T value, string label) where T : struct { if (!Enum.IsDefined(typeof(T), value)) Fail("Unknown " + label + "."); }
        // Schema-17 enums are dense from zero: an unboxed range check with Known's message, for records that
        // occur per consumer on every save and load. FoodToday's enum tests pin each maximum to its last member.
        private static void Dense(int value, int maximum, string label) { if ((uint)value > (uint)maximum) Fail("Unknown " + label + "."); }
        private static void Text(string value, string label, int max, bool emptyAllowed = false)
        { if (!(value != null && value.Length <= max && (emptyAllowed || !string.IsNullOrWhiteSpace(value)))) Fail("Invalid " + label + "."); }
        private static void Minute(double value, string label) => Bounded(value, 0, MaxMinute, label);
        private static void Bounded(double value, double min, double max, string label) { if (!(Position.Finite(value) && value >= min && value <= max)) Fail("Invalid " + label + "."); }
        private static void Point(Position point) { Bounded(point.X, -MaxCoordinate, MaxCoordinate, "position X"); Bounded(point.Y, -MaxCoordinate, MaxCoordinate, "position Y"); Bounded(point.Z, -MaxCoordinate, MaxCoordinate, "position Z"); }
        private static bool Same(Position a, Position b) => Position.Distance(a, b) <= 0.1;
        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-7;
        private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new SaveValidationException(message); }
        [DoesNotReturn] private static void Fail(string message) => throw new SaveValidationException(message);

        private sealed class StateContractResolver : DefaultContractResolver
        {
            private readonly int schema;
            public StateContractResolver(int schema = 20) { this.schema = schema; }
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).Where(property => property.Writable && !RivalWire21.AddedProperty(type,property.PropertyName!) && !(schema<20 && WeatherProperty(type,property.PropertyName)) && !(schema==18 &&
                    (type==typeof(FoodDecisionState) && property.PropertyName=="Awareness"
                     || type==typeof(FoodCommerceState) && property.PropertyName=="AwarenessStartsDay")))
                    // These two root headers let the native authority bootstrap stop before the graph.
                    // JSON object order is not a schema change; all old property orders still read.
                    .OrderBy(property => type == typeof(CityState) ? property.PropertyName == "Schema" ? 0 : property.PropertyName == "Capabilities" ? 1 : 2 : 2)
                    .ThenBy(property => property.PropertyName, StringComparer.Ordinal).Select(property =>
                {
                    if (type == typeof(FoodDecisionState) && property.PropertyName == "Tuning"
                        || type == typeof(FoodDecisionShop) && property.PropertyName == "MemoryContribution")
                    { property.Required = Required.Default; return property; } // pre-image 18
                    property.Required = NullableRecord(property.PropertyType!) || Nullable.GetUnderlyingType(property.PropertyType!) != null ? Required.AllowNull : Required.Always;
                    return property;
                }).ToList();
            }
        }

        // One compact string per outcome record; see MealOutcomeRecord.ToWire.
        private sealed class MealOutcomeConverter : JsonConverter<MealOutcomeRecord>
        {
            public override void WriteJson(JsonWriter writer, MealOutcomeRecord? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                writer.WriteValue(value.ToWire());
            }
            public override MealOutcomeRecord ReadJson(JsonReader reader, Type objectType, MealOutcomeRecord? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                MealOutcomeRecord? record = null;
                if (reader.TokenType != JsonToken.String || !MealOutcomeRecord.TryParse((string)reader.Value!, out record)) Fail("Invalid meal outcome record.");
                return record!;
            }
        }

        private sealed class PositionConverter : JsonConverter<Position>
        {
            public override void WriteJson(JsonWriter writer, Position value, JsonSerializer serializer)
            {
                writer.WriteStartObject(); writer.WritePropertyName("X"); writer.WriteValue(value.X); writer.WritePropertyName("Y"); writer.WriteValue(value.Y); writer.WritePropertyName("Z"); writer.WriteValue(value.Z); writer.WriteEndObject();
            }
            // Streams the three components; no JObject per position (two per route leg). The shape check has
            // already required exactly X, Y and Z numbers, and this repeats those rules on its own.
            public override Position ReadJson(JsonReader reader, Type objectType, Position existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Require(reader.TokenType == JsonToken.StartObject, "Position requires exactly X, Y and Z.");
                double x = 0, y = 0, z = 0; int seen = 0;
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string name = (string)reader.Value!;
                    int bit = name == "X" ? 1 : name == "Y" ? 2 : name == "Z" ? 4 : 0;
                    Require(bit != 0 && (seen & bit) == 0, "Position requires exactly X, Y and Z.");
                    seen |= bit;
                    if (!reader.Read() || (reader.TokenType != JsonToken.Float && reader.TokenType != JsonToken.Integer)) Fail("Missing or invalid position " + name + ".");
                    double value = reader.Value is double d ? d : reader.Value is long l ? l
                        : reader.Value is System.Numerics.BigInteger big ? (double)big : throw new SaveValidationException("Missing or invalid position " + name + ".");
                    if (bit == 1) x = value; else if (bit == 2) y = value; else z = value;
                }
                Require(reader.TokenType == JsonToken.EndObject && seen == 7, "Position requires exactly X, Y and Z.");
                var point = new Position(x, y, z); Point(point); return point;
            }
        }
    }
}
