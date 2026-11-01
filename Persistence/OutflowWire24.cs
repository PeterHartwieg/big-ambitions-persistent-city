using System;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        private static void InitializeOutflow24(CityState state, int schema)
        {
            if (schema != 24 || state.Food == null || state.Food.Outflow != null) return;
            Require(Position.Finite(state.Minute) && state.Minute >= 0 && state.Minute <= 1000000000
                && state.Food.Residents != null && state.Food.Residents.All(m => m != null), "Invalid outflow source roster/clock.");
            state.Food.Outflow = RivalOutflowHistory.Begin(state.Food, state.Minute);
        }
        private static void MigrateOutflow24(CityState state)
        {
            Require(state.Schema == 23, "Outflow migration requires exact schema 23.");
            if (state.Food != null) state.Food.Outflow = RivalOutflowHistory.Begin(state.Food, state.Minute);
            state.Schema = 24;
        }
        private sealed class OutflowConverter24 : JsonConverter<RivalOutflowHistory>
        {
            public override void WriteJson(JsonWriter writer, RivalOutflowHistory? value, JsonSerializer serializer)
            { if (value == null) writer.WriteNull(); else writer.WriteValue(value.ToWire()); }
            public override RivalOutflowHistory? ReadJson(JsonReader reader, Type type, RivalOutflowHistory? value, bool existing, JsonSerializer serializer)
                => reader.TokenType == JsonToken.Null ? null : reader.TokenType == JsonToken.String
                    ? RivalOutflowHistory.FromWire((string)reader.Value!) : throw new SaveValidationException("Invalid outflow shape.");
        }
    }
}
