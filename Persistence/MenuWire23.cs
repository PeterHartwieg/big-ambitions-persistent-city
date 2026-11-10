using System;
using System.Globalization;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        private static void CheckMenuIds23(JsonReader reader,ref int records, TokenText? text = null)
        {
            Require(reader.TokenType==JsonToken.StartArray,"Invalid additional menu IDs.");
            int count=0;
            while(reader.Read() && reader.TokenType!=JsonToken.EndArray)
            {
                Require(++count<FoodMenuPolicy.MaximumOffers && reader.TokenType==JsonToken.String
                    && !string.IsNullOrWhiteSpace((string)reader.Value!) && ((string)reader.Value!).Length<=256,"Oversized/invalid additional menu IDs.");
                if (text != null) Require(ExactString25(text, (IJsonLineInfo)reader, (string)reader.Value!), "Invalid menu name encoding.");
                Charge(ref records);
            }
            Require(reader.TokenType==JsonToken.EndArray,"Incomplete additional menu IDs.");
        }

        private static void WriteLegacyProduct(JsonWriter writer, FoodProductSales value, JsonSerializer serializer)
        {
            writer.WriteStartObject(); writer.WritePropertyName("ProductId"); writer.WriteValue(value.ProductId);
            writer.WritePropertyName("PurchaseMeals"); serializer.Serialize(writer, value.PurchaseMeals);
            writer.WritePropertyName("Purchases"); writer.WriteValue(value.Purchases);
            writer.WritePropertyName("Revenue"); writer.WriteValue(value.Revenue.Units); writer.WriteEndObject();
        }
    }
}
