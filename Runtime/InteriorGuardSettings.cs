using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PersistentCity.Runtime
{
    internal sealed partial class PrototypeSettings
    {
        [JsonConverter(typeof(InteriorModeConverter))]
        public string ThirdPartyInteriorGuard { get; set; } = "auto";
        [JsonConverter(typeof(InteriorGuardRulesConverter))]
        public string[]? ThirdPartyInteriorAssemblyPatterns { get; set; } = new[] { "TrueCityInteriors", "TrueCity*" };
        [JsonConverter(typeof(InteriorGuardRulesConverter))]
        public string[]? ThirdPartyInteriorHarmonyOwnerIds { get; set; } = Array.Empty<string>();
    }

    // Retain invalid input as a refusal rather than silently turning detection off.
    internal sealed class InteriorGuardRulesConverter : JsonConverter<string[]>
    {
        public override string[]? ReadJson(JsonReader reader, Type objectType, string[]? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if (!(token is JArray array) || array.Count > 16) return null;
            var rules = new string[array.Count];
            for (int i = 0; i < rules.Length; i++)
            {
                if (array[i].Type != JTokenType.String) return null;
                rules[i] = array[i].ToString();
            }
            return rules;
        }
        public override void WriteJson(JsonWriter writer, string[]? value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }
            writer.WriteStartArray();
            foreach (string rule in value) writer.WriteValue(rule);
            writer.WriteEndArray();
        }
    }
}
