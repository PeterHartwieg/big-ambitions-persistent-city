using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        internal static void RequireExecutablePolicy(CityState state)
            => Require(state.MenuPolicy == FoodNameIdentity.DynamicWire,
                "Unsupported held-product policy for this writer; preserve the payload.");
        // Complete coherent saves rebuild every wire-local reference together. The in-memory graph
        // contains names only; catalogue removal/order is deliberately not an input to this method.
        private static List<string> ReferencedNames25(CityState state)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            void Add(string? name, bool opaque = false)
            {
                if (opaque || name == "") return;
                Require(FoodNameIdentity.IsCanonical(name), "Invalid canonical product name.");
                names.Add(name!);
                Require(names.Count <= FoodNameIdentity.MaximumNames, "Product name table requires more than 256 entries; preserve the source.");
            }
            var food = state.Food;
            if (food != null)
            {
                if (food.Shops != null) foreach (var shop in food.Shops)
                {
                    if (shop == null) continue;
                    Add(shop.ProductId);
                    if (shop.AdditionalProductIds != null) foreach (var name in shop.AdditionalProductIds) Add(name);
                }
                if (food.Residents != null) foreach (var meal in food.Residents)
                {
                    if (meal == null) continue;
                    Add(meal.ProductId); Add(meal.PreferredProductId);
                    if (meal.Decision != null)
                    {
                        Add(meal.Decision.ProductId);
                        if (meal.Decision.Shops != null) foreach (var shop in meal.Decision.Shops)
                            if (shop?.Offers != null) foreach (var offer in shop.Offers)
                                if (offer != null) Add(offer.ProductId);
                    }
                    if (meal.Today?.Meals != null) foreach (var outcome in meal.Today.Meals)
                        if (outcome != null && FoodNameIdentity.IsCanonical(outcome.ProductId)) Add(outcome.ProductId);
                }
                if (food.History != null) foreach (var row in food.History)
                    if (row?.ProductSales != null) foreach (var cell in row.ProductSales)
                        if (cell != null) Add(cell.ProductId, Opaque25(cell));
            }
            if (state.Economy?.Shops != null)
                foreach (var shop in state.Economy.Shops)
                    if (shop?.Changes != null) foreach (var change in shop.Changes)
                        if (change.Kind == ShopChangeKind.Price || change.Kind == ShopChangeKind.Stock) Add(change.ProductId);
            if (state.Economy?.Shops != null)
                foreach (var shop in state.Economy.Shops) if (shop?.Supply != null)
                    foreach (var offer in shop.Supply) Add(offer.ProductId);
            return names.ToList();
        }
        private static bool Opaque25(FoodProductSales cell)
            => cell.HasOpaqueIdentity() || !FoodNameIdentity.IsCanonical(cell.ProductId);

        private static FoodOfferMetadata LegacyMetadata25(CityState state, ResidentMeal meal, string product)
        {
            Require(FoodProducts.Known(product), "Uncaptured dynamic offer metadata; E5c/E5b capture is required.");
            var traits = meal.RepresentedTraits ?? state.Citizens?.FirstOrDefault(c => c?.Identity?.Id == meal.HostId)?.Traits;
            Require(traits != null && traits.IsValid, "Missing held-offer traits.");
            return LegacyFoodOfferMetadata.Capture(traits!, state.Seed, meal.ResidentId, product);
        }
        private static void PrepareNames25(CityState state)
        {
            if (state.Food?.Residents != null) foreach (var meal in state.Food.Residents)
                if (meal?.Decision?.Shops != null) foreach (var shop in meal.Decision.Shops)
                    if (shop?.Offers != null) foreach (var offer in shop.Offers)
                        if (offer != null && offer.Metadata == null)
                            offer.Metadata = LegacyMetadata25(state, meal, offer.ProductId);
            state.ProductNames = ReferencedNames25(state);
        }

        private static int ValidateNames25(CityState state)
        {
            Require(state.NamePolicyStartsDay >= 0 && state.NamePolicyStartsDay <= (int)(state.Minute / 1440) + 1,
                "Invalid name-policy activation day.");
            var expected = ReferencedNames25(state);
            Require(state.ProductNames != null && state.ProductNames.Count <= FoodNameIdentity.MaximumNames
                && state.ProductNames.SequenceEqual(expected, StringComparer.Ordinal), "Product name table differs from referenced ordinal identities.");
            string table = JsonConvert.SerializeObject(state.ProductNames, Formatting.None);
            Require(table.Length + 16 <= FoodNameIdentity.MaximumTableWire
                && System.Text.Encoding.UTF8.GetByteCount(table) + 16 <= FoodNameIdentity.MaximumTableWire, "Product name table exceeds its character/UTF-8 budget.");
            int tuples = 0;
            if (state.Food != null) foreach (var meal in state.Food.Residents)
                if (meal.Decision != null) foreach (var shop in meal.Decision.Shops)
                    foreach (var offer in shop.Offers)
                    {
                        ValidateMetadata25(offer.Metadata);
                        Require(offer.Metadata!.Revision != 2 || state.MenuPolicy == FoodNameIdentity.DynamicWire,
                            "Dynamic held-product inputs require their capability header.");
                        Require(offer.Metadata.Revision != 2
                            || offer.Metadata.MealClass == FoodMealClass.Food && !FoodProducts.Known(offer.ProductId), "Held dynamic offer is not an admitted Food name.");
                        if (offer.Metadata!.Revision == 1 || FoodProducts.KnownLegacy(offer.ProductId))
                        {
                            var old = LegacyMetadata25(state, meal, offer.ProductId);
                            Require(SameMetadata25(old, offer.Metadata), "Held predecessor metadata changed its original policy.");
                        }
                        Require(++tuples <= 64, "Oversized held metadata image.");
                    }
            Require(2 + tuples * (24 + 12) <= FoodNameIdentity.MaximumHeldWire, "Oversized held metadata bytes.");
            return expected.Count + tuples;
        }
        private static bool SameMetadata25(FoodOfferMetadata a, FoodOfferMetadata b)
            => a.MealClass == b.MealClass && a.TasteBand == b.TasteBand
            && a.BreakfastFitCents == b.BreakfastFitCents && a.LunchFitCents == b.LunchFitCents && a.DinnerFitCents == b.DinnerFitCents;
        private static void ValidateMetadata25(FoodOfferMetadata? value)
        {
            Require(value != null && (value.Revision == 1 || value.Revision == 2)
                && (int)value.MealClass >= 0 && (int)value.MealClass <= 4 && value.TasteBand >= 0 && value.TasteBand <= 4
                && value.BreakfastFitCents >= 0 && value.BreakfastFitCents <= 5000
                && value.LunchFitCents >= 0 && value.LunchFitCents <= 5000
                && value.DinnerFitCents >= 0 && value.DinnerFitCents <= 5000, "Invalid held product metadata.");
        }
        private static List<string> CheckNameTable25(JsonReader reader, TokenText text, ref int records, int fieldStart = -1)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Invalid product name table.");
            int start = text.End((IJsonLineInfo)reader) - 1;
            var names = new List<string>(); string? previous = null;
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Require(names.Count < FoodNameIdentity.MaximumNames && reader.TokenType == JsonToken.String
                    && FoodNameIdentity.IsCanonical((string?)reader.Value)
                    && ExactString25(text, (IJsonLineInfo)reader, (string)reader.Value!), "Oversized/invalid product name table.");
                string name = (string)reader.Value!;
                Require(previous == null || StringComparer.Ordinal.Compare(previous, name) < 0, "Duplicate or unordered product names.");
                names.Add(name); previous = name; Charge(ref records);
            }
            Require(reader.TokenType == JsonToken.EndArray, "Incomplete product name table.");
            int end = text.End((IJsonLineInfo)reader);
            int length = end - (fieldStart >= 0 ? fieldStart : start);
            int overhead = fieldStart >= 0 ? 0 : 16;
            Require(length + overhead <= FoodNameIdentity.MaximumTableWire
                && System.Text.Encoding.UTF8.GetByteCount(text.Source.AsSpan(fieldStart >= 0 ? fieldStart : start, length)) + overhead <= FoodNameIdentity.MaximumTableWire,
                "Product name table exceeds its character/UTF-8 budget.");
            return names;
        }
        private static bool ExactString25(TokenText text, IJsonLineInfo token, string value)
        {
            string source = text.Source;
            int end = text.End(token), start = end - 2;
            if (end < 2 || source[end - 1] != '"') return false;
            while (start >= 0)
            {
                if (source[start] == '"')
                {
                    int slash = start - 1;
                    while (slash >= 0 && source[slash] == '\\') slash--;
                    if ((start - slash - 1) % 2 == 0) break;
                }
                start--;
            }
            if (start < 0) return false;
            int decoded = 0;
            for (int i = start + 1; i < end - 1; i++)
            {
                char c = source[i];
                if (c == '\\')
                {
                    if (++i >= end - 1) return false;
                    c = source[i];
                    if (c == 'u')
                    {
                        if (i + 4 >= end - 1 || !ushort.TryParse(source.AsSpan(i + 1, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out ushort code)) return false;
                        c = (char)code; i += 4;
                    }
                    else if (c == 'n') c = '\n'; else if (c == 'r') c = '\r'; else if (c == 't') c = '\t';
                    else if (c == 'b') c = '\b'; else if (c == 'f') c = '\f';
                    else if (c != '"' && c != '\\' && c != '/') return false;
                }
                if (decoded >= value.Length || value[decoded++] != c) return false;
            }
            return decoded == value.Length;
        }
        private static void CheckMetadata25(JsonReader reader, TokenText text, ref int records, FieldNameSets fields, int fieldStart = -1)
        {
            Require(reader.TokenType == JsonToken.StartArray, "Invalid held metadata tuple.");
            Require(++fields.Metadata25Count <= 64, "Oversized held metadata image.");
            int start = text.End((IJsonLineInfo)reader) - 1;
            for (int i = 0; i < 6; i++)
                Require(reader.Read() && reader.TokenType == JsonToken.Integer
                    && IntegerToken(reader.Value, i == 0 ? 1 : 0, i == 0 ? 2 : i <= 2 ? 4 : 5000)
                    && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid held metadata scalar.");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid held metadata arity.");
            int length = text.End((IJsonLineInfo)reader) - (fieldStart >= 0 ? fieldStart : start);
            int overhead = fieldStart >= 0 ? 0 : 12;
            fields.Metadata25Characters += length + overhead;
            fields.Metadata25Bytes += System.Text.Encoding.UTF8.GetByteCount(text.Source.AsSpan(fieldStart >= 0 ? fieldStart : start, length)) + overhead;
            Require(fields.Metadata25Characters <= FoodNameIdentity.MaximumHeldWire
                && fields.Metadata25Bytes <= FoodNameIdentity.MaximumHeldWire, "Oversized held metadata character/UTF-8 image.");
            Charge(ref records);
        }
        private sealed class Metadata25Converter : JsonConverter<FoodOfferMetadata>
        {
            public override void WriteJson(JsonWriter writer, FoodOfferMetadata? value, JsonSerializer serializer)
            {
                ValidateMetadata25(value);
                writer.WriteStartArray(); writer.WriteValue(value!.Revision); writer.WriteValue((int)value.MealClass);
                writer.WriteValue(value.TasteBand); writer.WriteValue(value.BreakfastFitCents);
                writer.WriteValue(value.LunchFitCents); writer.WriteValue(value.DinnerFitCents); writer.WriteEndArray();
            }
            public override FoodOfferMetadata ReadJson(JsonReader reader, Type type, FoodOfferMetadata? value, bool existing, JsonSerializer serializer)
            {
                var values = new int[6];
                Require(reader.TokenType == JsonToken.StartArray, "Invalid held metadata tuple.");
                for (int i = 0; i < values.Length; i++)
                { Require(reader.Read() && reader.TokenType == JsonToken.Integer, "Invalid held metadata scalar."); values[i] = checked((int)(long)reader.Value!); }
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid held metadata arity.");
                var result = new FoodOfferMetadata(values[0], (FoodMealClass)values[1], values[2], values[3], values[4], values[5]);
                ValidateMetadata25(result); return result;
            }
        }
        private static void CheckProductCell25(JsonReader reader, TokenText text, int references)
        {
            int start = text.End((IJsonLineInfo)reader) - 1;
            for (int field = 0; field < 7; field++)
                Require(reader.Read() && reader.TokenType == JsonToken.Integer
                    && IntegerToken(reader.Value, field == 0 ? 1 : 0, field == 0 ? Math.Min(FoodNameIdentity.MaximumNames, references) : long.MaxValue)
                    && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid product reference cell.");
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid product reference cell arity.");
            Require(text.End((IJsonLineInfo)reader) - start <= 128, "Product reference cell exceeds 128 characters.");
        }
        private sealed class ProductSales25Converter : JsonConverter<FoodProductSales>
        {
            private readonly WireContext context;
            public ProductSales25Converter(WireContext context) { this.context = context; }
            public override void WriteJson(JsonWriter writer, FoodProductSales? value, JsonSerializer serializer)
            {
                Require(value != null, "Null product history.");
                if (Opaque25(value!)) { WriteLegacyProduct(writer, value!, serializer); return; }
                Require(context.ProductIndex.TryGetValue(value!.ProductId, out int reference), "Missing product reference.");
                writer.WriteStartArray(); writer.WriteValue(reference); writer.WriteValue(value.Purchases); writer.WriteValue(value.Revenue.Units);
                foreach (long count in value.PurchaseMeals) writer.WriteValue(count);
                writer.WriteEndArray();
            }
            public override FoodProductSales ReadJson(JsonReader reader, Type type, FoodProductSales? existing, bool hasExisting, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    var opaque = new FoodProductSales(); serializer.Populate(reader, opaque); opaque.PreserveOpaqueIdentity(); return opaque;
                }
                long Next()
                { Require(reader.Read() && reader.TokenType == JsonToken.Integer, "Invalid product cell."); return Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); }
                int reference = checked((int)Next());
                Require(reference >= 1 && reference <= context.ProductNames.Length, "Dangling product name reference.");
                var result = new FoodProductSales { ProductId = context.ProductNames[reference - 1], Purchases = Next(), Revenue = Money.FromUnits(Next()) };
                for (int i = 0; i < 4; i++) result.PurchaseMeals[i] = Next();
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid product cell arity.");
                return result;
            }
        }
    }
}
