using System;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    internal static partial class PopulationSchema20
    {
        private sealed class TuningConverter : JsonConverter<FoodDecisionTuning>
        {
            public override FoodDecisionTuning? ReadJson(JsonReader reader, Type type, FoodDecisionTuning? existing, bool hasExisting, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                Require(reader.TokenType == JsonToken.String, "Invalid held tuning image.");
                Require(FoodDecisionTuning.TryParse((string)reader.Value!, out var image), "Invalid held tuning image.");
                return image;
            }
            public override void WriteJson(JsonWriter writer, FoodDecisionTuning? value, JsonSerializer serializer)
            { if (value == null) writer.WriteNull(); else writer.WriteValue(value.ToWire()); }
        }
    }
}
