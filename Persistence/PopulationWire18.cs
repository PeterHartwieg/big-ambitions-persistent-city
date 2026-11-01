using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        public const int MaxBusinessReferenceWire = 2 * 1024 * 1024;

        private static void Charge(ref int records) => Require(++records <= MaxEmbeddedRecords, "Too many embedded records.");
        private static void ValidateCapabilities(PopulationCapabilities capabilities)
        {
            try { capabilities.RequireExecutable(); }
            catch (InvalidOperationException e) { throw new SaveValidationException(e.Message); }
        }

        /// <summary>Small early authority check; no population graph, identities or wallets are allocated.
        /// Full decode remains mandatory at population startup. Any failure keeps authority suspended.</summary>
        public static void RequireExecutableCapabilities(string source)
        {
            Require(source != null && source.Length <= MaxInputCharacters, "Invalid population payload.");
            using var reader = Reader(source);
            Require(reader.Read() && reader.TokenType == JsonToken.StartObject, "Expected population object.");
            int? schema = null;
            PopulationCapabilities? capabilities = null;
            bool menuPolicy = false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            // Resolve token offsets only as far as a header requires; TokenText(source) would scan
            // every character even when both headers precede the population graph.
            int tokenLine = 1, lineStart = 0;
            bool canonicalInteger = false;
            int HeaderInteger(bool requireCanonical = true)
            {
                Require(reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, int.MinValue, int.MaxValue), "Invalid capability/schema integer.");
                while (tokenLine < reader.LineNumber)
                {
                    int newline = source.IndexOfAny(ProbeLineBreaks, lineStart);
                    Require(newline >= 0, "Invalid header position.");
                    lineStart = newline + 1;
                    if (source[newline] == '\r' && lineStart < source.Length && source[lineStart] == '\n') lineStart++;
                    tokenLine++;
                }
                int end = lineStart + reader.LinePosition, start = end;
                while (start > 0 && source[start - 1] != ':' && source[start - 1] != ',' && source[start - 1] != '['
                    && source[start - 1] != '{' && !char.IsWhiteSpace(source[start - 1])) start--;
                canonicalInteger = TokenText.CanonicalUnits(source.Substring(start, end - start));
                if (requireCanonical) Require(canonicalInteger, "Noncanonical capability/schema integer.");
                return (int)(long)reader.Value!;
            }
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Require(reader.TokenType == JsonToken.PropertyName, "Expected population field.");
                string name = (string)reader.Value!;
                Require(names.Add(name), "Duplicate population field.");
                Require(reader.Read(), "Missing field value.");
                if (name == "Schema")
                {
                    schema = HeaderInteger(requireCanonical: false);
                    if (schema >= 18) Require(canonicalInteger, "Noncanonical population schema.");
                    Require(schema >= 13 && (schema <= 19 || schema == 23), "Unsupported population schema; preserve the payload.");
                }
                else if (name == "Capabilities")
                {
                    Require(reader.TokenType == JsonToken.StartObject, "Expected population capabilities.");
                    capabilities = new PopulationCapabilities();
                    int seen = 0;
                    while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                    {
                        Require(reader.TokenType == JsonToken.PropertyName, "Expected capability field.");
                        string field = (string)reader.Value!;
                        int bit = field == "Active" ? 1 : field == "Code" ? 2 : field == "Revision" ? 4 : 0;
                        Require(bit != 0 && (seen & bit) == 0, "Unknown or duplicate capability field.");
                        seen |= bit;
                        Require(reader.Read(), "Missing capability value.");
                        if (field == "Active") { Require(reader.TokenType == JsonToken.Boolean, "Invalid active capability flag."); capabilities.Active = (bool)reader.Value!; }
                        else if (field == "Code") { Require(reader.TokenType == JsonToken.String && ((string)reader.Value!).Length <= 512, "Invalid capability code."); capabilities.Code = (string)reader.Value!; }
                        else capabilities.Revision = HeaderInteger();
                    }
                    Require(reader.TokenType == JsonToken.EndObject && seen == 7, "Missing capability fields.");
                    ValidateCapabilities(capabilities);
                }
                else if (name == "MenuPolicy")
                {
                    Require(reader.TokenType == JsonToken.String && (string)reader.Value! == FoodMenuPolicy.Wire, "Unsupported menu product policy.");
                    menuPolicy = true;
                }
                else reader.Skip();
                if (schema.HasValue && capabilities != null && (schema != 23 || menuPolicy))
                {
                    Require(schema >= 18, "Unknown capability header in a legacy population schema.");
                    Require(schema == 23 || !menuPolicy, "Menu policy in an old schema.");
                    return; // Full population startup still validates all fields, duplicates and trailing content.
                }
            }
            Require(reader.TokenType == JsonToken.EndObject && !reader.Read(), "Incomplete or trailing population content.");
            Require(schema.HasValue, "Missing population schema.");
            Require(schema < 18, "Missing population capabilities.");
        }
        private static readonly char[] ProbeLineBreaks = { '\r', '\n' };

        private static int ValidateMemory(ConsumerMemory memory, int day, ISet<int> references)
        {
            try { MemoryWire.Check(memory, day, references); }
            catch (FormatException e) { throw new SaveValidationException(e.Message); }
            return 1;
        }
        private static int ValidateBusinessReferences(CityState state)
        {
            Require(state.Food != null && state.Food.Shops != null && state.Food.Residents != null && state.Food.History != null
                && state.Places != null && state.Places.All(p => p != null), "Missing food collections/places.");
            var refs = state.Food!.BusinessReferences;
            Require(refs != null && refs.Count <= MaxPlaces, "Oversized business reference table.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var places = new HashSet<string>(state.Places.Where(p => p.Kind == PlaceKind.Shop).Select(p => p.Id), StringComparer.Ordinal);
            int previous = -1;
            foreach (var entry in refs)
            {
                Require(entry != null && entry.Slot > previous && entry.Slot < MaxPlaces && ids.Add(entry.BusinessId), "Duplicate or unordered business reference.");
                previous = entry.Slot;
                Text(entry.BusinessId, "Business reference identity", 512);
                Text(entry.PlaceId, "Business reference place", 256);
                Require(places.Contains(entry.PlaceId), "Unknown business reference place.");
                // Existing references must never disagree with the same complete identity elsewhere.
            }
            var byId = refs.ToDictionary(r => r.BusinessId, r => r.PlaceId, StringComparer.Ordinal);
            void Match(string id, string place) { if (byId.TryGetValue(id, out var expected)) Require(place == expected, "Reused business identity at another place."); }
            foreach (var s in state.Food.Shops) if (s != null) Match(s.BusinessId, s.PlaceId);
            foreach (var m in state.Food.Residents) if (m != null)
            {
                Match(m.BusinessId, m.PlaceId);
                if (m.Decision != null) foreach (var s in m.Decision.Shops) if (s != null) Match(s.BusinessId, s.PlaceId);
            }
            foreach (var h in state.Food.History) if (h != null) Match(h.BusinessId, h.PlaceId);
            var json = JsonConvert.SerializeObject(refs, Formatting.None, Settings(WireContext.FromState(state)));
            Require(json.Length <= MaxBusinessReferenceWire && System.Text.Encoding.UTF8.GetByteCount(json) <= MaxBusinessReferenceWire, "Business reference table exceeds its character/UTF-8 budget.");
            return refs.Count;
        }

        /// <summary>Reports both published counting paths; callers must budget the larger.</summary>
        public static (int Streaming, int Semantic) CountRecords(CityState state, string source)
        {
            int semantic = Validate(state), streaming = 0;
            var context = WireContext.FromSource(source);
            using var reader = Reader(source);
            Require(reader.Read(), "Missing state.");
            CheckShape(reader, typeof(CityState), StateContracts, ref streaming, new TokenText(source), CityState.CurrentSchema, new FieldNameSets());
            Require(!reader.Read(), "Trailing content.");
            return (streaming, semantic);
        }

        private sealed class WireContext
        {
            public static readonly WireContext Empty = new WireContext(Array.Empty<string>());
            public readonly string[] Places;
            public readonly Dictionary<string, int> Index;
            private WireContext(IEnumerable<string> places)
            {
                Places = places.OrderBy(p => p, StringComparer.Ordinal).ToArray();
                Require(Places.Length <= MaxPlaces, "Oversized places.");
                Index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < Places.Length; i++) Require(Index.TryAdd(Places[i], i), "Duplicate place ID.");
            }
            public static WireContext FromState(CityState state) => new WireContext(state.Places.Select(p => p.Id));
            public static WireContext FromSource(string source)
            {
                // Root/property/Places order is irrelevant. Collect only bounded saved IDs; no native lookup.
                using var reader = Reader(source);
                Require(reader.Read() && reader.TokenType == JsonToken.StartObject, "Expected root.");
                var ids = new List<string>(); bool found = false;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    Require(reader.TokenType == JsonToken.PropertyName, "Expected root field.");
                    string name = (string)reader.Value!; Require(reader.Read(), "Missing root value.");
                    if (name != "Places") { reader.Skip(); continue; }
                    Require(!found && reader.TokenType == JsonToken.StartArray, "Invalid Places."); found = true;
                    int count = 0;
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        Require(++count <= MaxPlaces && reader.TokenType == JsonToken.StartObject, "Oversized/invalid Places.");
                        string? id = null;
                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            Require(reader.TokenType == JsonToken.PropertyName, "Expected place field.");
                            string field = (string)reader.Value!; Require(reader.Read(), "Missing place value.");
                            if (field == "Id") { Require(id == null && reader.TokenType == JsonToken.String && ((string)reader.Value!).Length <= 256, "Invalid place ID."); id = (string)reader.Value!; }
                            else reader.Skip();
                        }
                        Require(id != null, "Missing place ID."); ids.Add(id!);
                    }
                }
                Require(found, "Missing Places."); return new WireContext(ids);
            }
        }

        private sealed class TupleLayout
        {
            public readonly PropertyInfo[] Properties;
            public readonly Action<JsonWriter, object, JsonSerializer>[] Write;
            public readonly Action<object, JsonReader, JsonSerializer>[] Read;
            public readonly Func<object> Create;
            private TupleLayout(Type type, string fields)
            {
                var names = fields.Split(' ');
                var writable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetSetMethod() != null).ToArray();
                if (names.Length != writable.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length
                    || !new HashSet<string>(writable.Select(p => p.Name), StringComparer.Ordinal).SetEquals(names))
                    throw new InvalidOperationException(type.Name + " tuple fields must exactly match its public writable properties; change the tuple version and save contract deliberately.");
                Properties = names.Select(n => type.GetProperty(n) ?? throw new InvalidOperationException(n)).ToArray();
                Write = Properties.Select(p => {
                    var writer = Expression.Parameter(typeof(JsonWriter)); var target = Expression.Parameter(typeof(object)); var serializer = Expression.Parameter(typeof(JsonSerializer));
                    Expression value = Expression.Property(Expression.Convert(target, type), p);
                    Type scalar = p.PropertyType;
                    if (scalar == typeof(Money)) { value = Expression.Property(value, nameof(Money.Units)); scalar = typeof(long); }
                    else if (scalar.IsEnum) { value = Expression.Convert(value, typeof(int)); scalar = typeof(int); }
                    var primitive = scalar == typeof(string) || scalar == typeof(bool) || scalar == typeof(int) || scalar == typeof(long) || scalar == typeof(double)
                        ? typeof(JsonWriter).GetMethod("WriteValue", new[] { scalar }) : null;
                    Expression body = primitive != null ? Expression.Call(writer, primitive, value)
                        : Expression.Call(serializer, typeof(JsonSerializer).GetMethod("Serialize", new[] { typeof(JsonWriter), typeof(object), typeof(Type) })!, writer, Expression.Convert(value, typeof(object)), Expression.Constant(p.PropertyType));
                    return Expression.Lambda<Action<JsonWriter, object, JsonSerializer>>(body, writer, target, serializer).Compile();
                }).ToArray();
                Read = Properties.Select(p => {
                    var target = Expression.Parameter(typeof(object)); var reader = Expression.Parameter(typeof(JsonReader)); var serializer = Expression.Parameter(typeof(JsonSerializer));
                    Expression token = Expression.Property(reader, nameof(JsonReader.Value));
                    Type scalar = p.PropertyType; Expression value;
                    if (scalar == typeof(string) || scalar == typeof(bool)) value = Expression.Convert(token, scalar);
                    else if (scalar == typeof(int)) value = Expression.ConvertChecked(Expression.Convert(token, typeof(long)), typeof(int));
                    else if (scalar == typeof(long)) value = Expression.Convert(token, typeof(long));
                    else if (scalar == typeof(double)) value = Expression.Call(typeof(SaveCodec).GetMethod(nameof(TupleNumber), BindingFlags.NonPublic | BindingFlags.Static)!, reader);
                    else if (scalar == typeof(Money)) value = Expression.Call(typeof(Money).GetMethod(nameof(Money.FromUnits))!, Expression.Convert(token, typeof(long)));
                    else if (scalar.IsEnum) value = Expression.Convert(Expression.Convert(token, typeof(long)), scalar);
                    else value = Expression.Convert(Expression.Call(serializer, typeof(JsonSerializer).GetMethod("Deserialize", new[] { typeof(JsonReader), typeof(Type) })!, reader, Expression.Constant(scalar)), scalar);
                    var body = Expression.Assign(Expression.Property(Expression.Convert(target, type), p), value);
                    return Expression.Lambda<Action<object, JsonReader, JsonSerializer>>(body, target, reader, serializer).Compile();
                }).ToArray();
                Create = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(type), typeof(object))).Compile();
            }
            // Frozen positional contracts, independent of declaration/reflection order. Version 1 is slot zero.
            private static readonly TupleLayout Budget = new TupleLayout(typeof(ResidentBudget), "Arrears Balance BaseIncomeCents DailyEssentialsCents DailyHomeFoodCents DailyHousingCents DailyIncomeCents DaysWithoutHomeFood EssentialsPaid HeldPurchase HomeFoodFundedDay HomeFoodPaid IncomeReceived IncomeSource LastPostedDay NextPostingMinute OpeningBalance PreparedFoodSpent");
            private static readonly TupleLayout Meal = new TupleLayout(typeof(ResidentMeal), "Allowance AnchorId Budget BusinessId ContextAttempts Day DayAllowanceUsed Deadline Decision DesiredAt DueAt Excursion ExistingStop ExistingStopPurchases ExpiresAt Kind LastContextKey NeedsHomeResync PlaceId PreferenceBonusCents PreferredProductId Price ProductId Purchases Reason RepresentedTraits ResidentId ReturnMinutes ServiceAt ServiceMinutes Slot Spent Status Today Memory");
            public static TupleLayout? For(Type type) => type == typeof(ResidentBudget) ? Budget : type == typeof(ResidentMeal) ? Meal : null;
        }
        private static double TupleNumber(JsonReader reader) => reader.Value is double d ? d : reader.Value is long n ? n
            : reader.Value is System.Numerics.BigInteger b ? (double)b : throw new SaveValidationException("Invalid tuple number.");
        private sealed class TupleConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => TupleLayout.For(type) != null;
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                var tuple = TupleLayout.For(value.GetType())!;
                writer.WriteStartArray(); writer.WriteValue(1);
                for (int i = 0; i < tuple.Properties.Length; i++) tuple.Write[i](writer, value, serializer);
                writer.WriteEndArray();
            }
            public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                var tuple = TupleLayout.For(type)!;
                Require(reader.TokenType == JsonToken.StartArray && reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 1, 1), "Invalid tuple version.");
                var value = tuple.Create();
                for (int i = 0; i < tuple.Properties.Length; i++)
                {
                    Require(reader.Read() && reader.TokenType != JsonToken.EndArray, "Missing tuple slot.");
                    tuple.Read[i](value, reader, serializer);
                }
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus tuple slot."); return value;
            }
        }
        private sealed class TodayConverter : JsonConverter<ResidentFoodToday>
        {
            public override void WriteJson(JsonWriter writer, ResidentFoodToday? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                writer.WriteStartArray(); writer.WriteValue(1); writer.WriteValue(value.Day); writer.WriteValue(value.Unrecorded);
                foreach (var record in value.Meals) writer.WriteValue(record.ToWire());
                writer.WriteEndArray();
            }
            public override ResidentFoodToday? ReadJson(JsonReader reader, Type type, ResidentFoodToday? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                var today = new ResidentFoodToday();
                Require(reader.Read(), "Missing rollup version.");
                Require(reader.Read(), "Missing rollup day."); today.Day = checked((int)(long)reader.Value!);
                Require(reader.Read(), "Missing rollup count."); today.Unrecorded = checked((int)(long)reader.Value!);
                while (reader.Read() && reader.TokenType != JsonToken.EndArray) { Require(today.Meals.Count < 3, "Surplus rollup slot."); today.Meals.Add(serializer.Deserialize<MealOutcomeRecord>(reader)!); }
                return today;
            }
        }
        private sealed class MemoryConverter : JsonConverter<ConsumerMemory>
        {
            public override void WriteJson(JsonWriter writer, ConsumerMemory? value, JsonSerializer serializer) => writer.WriteValue(MemoryWire.Encode(value!));
            public override ConsumerMemory ReadJson(JsonReader reader, Type type, ConsumerMemory? existingValue, bool hasExistingValue, JsonSerializer serializer) => MemoryWire.Decode((string)reader.Value!);
        }
        private sealed class VisitConverter : JsonConverter<VisitRecord>
        {
            private readonly WireContext context;
            public VisitConverter(WireContext context) { this.context = context; }
            public static void Check(JsonReader reader)
            {
                Require(reader.TokenType == JsonToken.String, "Invalid visit token."); string text = (string)reader.Value!;
                Require(text.Length == 21 && text[0] == '1', "Invalid visit length/version.");
                for (int i = 1; i <= 16; i++) Require(text[i] >= '0' && text[i] <= '9' || text[i] >= 'A' && text[i] <= 'F', "Invalid visit binary64 alphabet.");
                Require(MemoryWire.Number(text, 17, 3) < MaxPlaces && text[20] >= '0' && text[20] <= '4', "Invalid visit place/activity code.");
                double minute = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(text.Substring(1, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                Minute(minute, "Visit minute");
            }
            public override void WriteJson(JsonWriter writer, VisitRecord? value, JsonSerializer serializer)
            {
                writer.WriteValue("1" + unchecked((ulong)BitConverter.DoubleToInt64Bits(value!.AtMinute)).ToString("X16", CultureInfo.InvariantCulture)
                    + MemoryWire.Number(context.Index[value.PlaceId], 3) + (char)('0' + (int)value.Activity));
            }
            public override VisitRecord ReadJson(JsonReader reader, Type type, VisitRecord? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Check(reader); string text = (string)reader.Value!; int at = MemoryWire.Number(text, 17, 3);
                Require(at < context.Places.Length, "Invalid visit place reference.");
                return new VisitRecord { AtMinute = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(text.Substring(1, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture))), PlaceId = context.Places[at], Activity = (Activity)(text[20] - '0') };
            }
        }
        private sealed class BusinessReferenceConverter : JsonConverter<BusinessReference>
        {
            private readonly WireContext context;
            public BusinessReferenceConverter(WireContext context) { this.context = context; }
            public static void Check(JsonReader reader, TokenText text)
            {
                Require(reader.TokenType == JsonToken.StartArray && reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 1, 1) && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid business reference version.");
                Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 0, MaxPlaces - 1) && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid business reference slot.");
                Require(reader.Read() && reader.TokenType == JsonToken.String && ((string)reader.Value!).Length <= 512, "Invalid business reference identity.");
                Require(reader.Read() && reader.TokenType == JsonToken.String && ((string)reader.Value!).Length == 3 && MemoryWire.Number((string)reader.Value!, 0, 3) < MaxPlaces && text.LiteralAsciiString((IJsonLineInfo)reader, (string)reader.Value!), "Invalid business place reference.");
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Surplus business reference slot.");
            }
            public override void WriteJson(JsonWriter writer, BusinessReference? value, JsonSerializer serializer)
            {
                writer.WriteStartArray(); writer.WriteValue(1); writer.WriteValue(value!.Slot); writer.WriteValue(value.BusinessId); writer.WriteValue(MemoryWire.Number(context.Index[value.PlaceId], 3)); writer.WriteEndArray();
            }
            public override BusinessReference ReadJson(JsonReader reader, Type type, BusinessReference? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                reader.Read(); reader.Read(); int slot = checked((int)(long)reader.Value!);
                reader.Read(); string id = (string)reader.Value!;
                reader.Read(); int at = MemoryWire.Number((string)reader.Value!, 0, 3); Require(at < context.Places.Length, "Unknown business place reference."); reader.Read();
                return new BusinessReference { Slot = slot, BusinessId = id, PlaceId = context.Places[at] };
            }
        }
    }
}
