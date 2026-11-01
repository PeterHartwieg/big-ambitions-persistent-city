using System;
using System.Globalization;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        private const int MenuPredecessorSchema = 22;
        private static void MigrateMenu23(CityState state)
        {
            Require(state.Schema == MenuPredecessorSchema, "Menu migration requires exact schema 22.");
            // The source has already passed its original shape, bounds, money and identity checks.
            state.MenuPolicy = FoodMenuPolicy.Wire;
            state.Schema = 23;
        }
        private static int MenuLimit(int schema) => schema == 23 ? FoodMenuPolicy.MaximumOffers : 4;
        private static bool KnownMenuProduct(int schema,string product) => schema==23 ? FoodProducts.Known(product) : FoodProducts.KnownLegacy(product);
        private static void CheckMenuIds23(JsonReader reader,ref int records)
        {
            Require(reader.TokenType==JsonToken.StartArray,"Invalid additional menu IDs.");
            int count=0;
            while(reader.Read() && reader.TokenType!=JsonToken.EndArray)
            {
                Require(++count<FoodMenuPolicy.MaximumOffers && reader.TokenType==JsonToken.String
                    && !string.IsNullOrWhiteSpace((string)reader.Value!) && ((string)reader.Value!).Length<=256,"Oversized/invalid additional menu IDs.");
                Charge(ref records);
            }
            Require(reader.TokenType==JsonToken.EndArray,"Incomplete additional menu IDs.");
        }

        // Compact reviewed product cells; unknown historical IDs retain their exact old object shape.
        // One cell remains one charged record; its four fixed meal counters are not separate records.
        private sealed class ProductSales23Converter : JsonConverter<FoodProductSales>
        {
            public override void WriteJson(JsonWriter writer, FoodProductSales? value, JsonSerializer serializer)
            {
                if (value == null) throw new SaveValidationException("Null product history.");
                int code = FoodMenuPolicy.Code(value.ProductId);
                if (code == 0) { WriteLegacyProduct(writer, value, serializer); return; }
                writer.WriteStartArray(); writer.WriteValue(code); writer.WriteValue(value.Purchases); writer.WriteValue(value.Revenue.Units);
                foreach (long count in value.PurchaseMeals) writer.WriteValue(count);
                writer.WriteEndArray();
            }
            public override FoodProductSales ReadJson(JsonReader reader, Type type, FoodProductSales? existing, bool hasExisting, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    var value = new FoodProductSales(); serializer.Populate(reader, value);
                    Require(FoodMenuPolicy.Code(value.ProductId)==0,"Reviewed product history requires a compact cell.");
                    return value;
                }
                long ReadNumber()
                {
                    Require(reader.Read() && reader.TokenType==JsonToken.Integer,"Invalid product cell.");
                    return Convert.ToInt64(reader.Value,CultureInfo.InvariantCulture);
                }
                int code=checked((int)ReadNumber());long purchases=ReadNumber(),revenue=ReadNumber();
                var result=new FoodProductSales { ProductId=FoodMenuPolicy.Product(code)!,Purchases=purchases,Revenue=Money.FromUnits(revenue) };
                for(int meal=0;meal<4;meal++) result.PurchaseMeals[meal]=ReadNumber();
                Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid product cell arity.");
                return result;
            }
        }
        private static void WriteLegacyProduct(JsonWriter writer, FoodProductSales value, JsonSerializer serializer)
        {
            writer.WriteStartObject(); writer.WritePropertyName("ProductId"); writer.WriteValue(value.ProductId);
            writer.WritePropertyName("PurchaseMeals"); serializer.Serialize(writer, value.PurchaseMeals);
            writer.WritePropertyName("Purchases"); writer.WriteValue(value.Purchases);
            writer.WritePropertyName("Revenue"); writer.WriteValue(value.Revenue.Units); writer.WriteEndObject();
        }
        private static void CheckProductCell23(JsonReader reader, TokenText text)
        {
            int start = text.End((IJsonLineInfo)reader) - 1;
            for (int field = 0; field < 7; field++)
            {
                Require(reader.Read() && reader.TokenType == JsonToken.Integer && IntegerToken(reader.Value, 0, long.MaxValue)
                    && text.CanonicalInteger((IJsonLineInfo)reader), "Invalid product cell number.");
                if (field == 0) Require((long)reader.Value! <= FoodMenuPolicy.MaximumDefinitions && FoodMenuPolicy.Product((int)(long)reader.Value!) != null, "Unknown product cell code.");
            }
            Require(reader.Read() && reader.TokenType == JsonToken.EndArray, "Invalid product cell arity.");
            Require(text.End((IJsonLineInfo)reader) - start <= 128, "Product cell exceeds 128 characters.");
        }
    }
}
