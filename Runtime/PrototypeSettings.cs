using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed class PrototypeSettings
    {
        [JsonIgnore]
        public FoodChoiceSettings FoodChoice { get; } = new FoodChoiceSettings();
        public double FoodChoiceTemperature { get => FoodChoice.FoodChoiceTemperature; set => FoodChoice.FoodChoiceTemperature=value; }
        public double FoodChoiceWeightFloor { get => FoodChoice.FoodChoiceWeightFloor; set => FoodChoice.FoodChoiceWeightFloor=value; }
        public double FoodChoiceTasteWeight { get => FoodChoice.FoodChoiceTasteWeight; set => FoodChoice.FoodChoiceTasteWeight=value; }
        public double FoodChoicePriceWeight { get => FoodChoice.FoodChoicePriceWeight; set => FoodChoice.FoodChoicePriceWeight=value; }
        public double FoodChoiceDistanceWeight { get => FoodChoice.FoodChoiceDistanceWeight; set => FoodChoice.FoodChoiceDistanceWeight=value; }
        public double FoodChoiceDistanceScaleMinutes { get => FoodChoice.FoodChoiceDistanceScaleMinutes; set => FoodChoice.FoodChoiceDistanceScaleMinutes=value; }
        public double FoodChoiceQueueWeight { get => FoodChoice.FoodChoiceQueueWeight; set => FoodChoice.FoodChoiceQueueWeight=value; }
        public double FoodChoiceQualityWeight { get => FoodChoice.FoodChoiceQualityWeight; set => FoodChoice.FoodChoiceQualityWeight=value; }
        public double FoodChoiceTimePressureWeight { get => FoodChoice.FoodChoiceTimePressureWeight; set => FoodChoice.FoodChoiceTimePressureWeight=value; }
        public double FoodChoiceHealthWeight { get => FoodChoice.FoodChoiceHealthWeight; set => FoodChoice.FoodChoiceHealthWeight=value; }
        public double FoodChoiceHouseholdWeight { get => FoodChoice.FoodChoiceHouseholdWeight; set => FoodChoice.FoodChoiceHouseholdWeight=value; }
        public double FoodShortlistTemperature { get => FoodChoice.FoodShortlistTemperature; set => FoodChoice.FoodShortlistTemperature=value; }
        [JsonIgnore]
        public FoodAwarenessSettings Awareness { get; } = new FoodAwarenessSettings();
        [JsonConverter(typeof(AwarenessNumberConverter))]
        public double FoodMarketingNeighbourhoodMetres { get => Awareness.RadiusMetres; set => Awareness.RadiusMetres=value; }
        [JsonConverter(typeof(AwarenessNumberConverter))]
        public double FoodMarketingDiscoveryDailyProbability { get => Awareness.DiscoveryProbability; set => Awareness.DiscoveryProbability=value; }
        [JsonConverter(typeof(AwarenessNumberConverter))]
        public int FoodMarketingNeighbourhoodDrawsPerAnchor { get => Awareness.DrawsPerAnchor; set => Awareness.DrawsPerAnchor=value; }
        [JsonIgnore]
        public WeatherSettings Weather { get; } = new WeatherSettings();
        public bool WeatherEnabled { get => Weather.Enabled; set => Weather.Enabled=value; }
        [JsonConverter(typeof(AwarenessNumberConverter))]
        public double WeatherRainOptionalOutingMultiplier { get => Weather.RainOptionalOutingMultiplier; set => Weather.RainOptionalOutingMultiplier=value; }
        [JsonConverter(typeof(AwarenessNumberConverter))]
        public double WeatherRainTravelWeightMultiplier { get => Weather.RainTravelWeightMultiplier; set => Weather.RainTravelWeightMultiplier=value; }
        [JsonConverter(typeof(WeatherLeadConverter))]
        public int WeatherQueueRainLeadMinutes { get => Weather.QueueRainLeadMinutes; set => Weather.QueueRainLeadMinutes=value; }
        // Optional developer overrides; normal installation needs no configuration.
        private int optionalStopChoicePercentage = OptionalStopPolicy.ApprovedChoicePercentage;
        [JsonConverter(typeof(OptionalStopPercentageConverter))]
        public int OptionalStopChoicePercentage
        {
            get => optionalStopChoicePercentage;
            set => optionalStopChoicePercentage = Math.Max(0, Math.Min(100, value));
        }
        public bool Enabled { get; set; } = true;
        public double GameClockFactor { get; set; } = CityBalance.GameClockFactor;
        public bool PhysicalEnabled { get; set; } = true;
        public int MaximumPedestrians { get; set; } = 48;
        public int MaximumVehicles { get; set; } = 24;
        public double PhysicalRadiusMetres { get; set; } = 120;
    }

    // Preserve A's numeric-string/endpoint conventions without letting an unusable awareness
    // key discard unrelated configuration. Sentinels reach Clamp exactly once for bounded logging.
    internal sealed class AwarenessNumberConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type==typeof(int) || type==typeof(double);
        public override object ReadJson(JsonReader reader,Type type,object? existing,JsonSerializer serializer)
        {
            var token=JToken.Load(reader);
            string raw=token.Type==JTokenType.String ? (string)token! : token.ToString(Formatting.None);
            bool valid=(token.Type==JTokenType.Integer || token.Type==JTokenType.Float || token.Type==JTokenType.String)
                && double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out _);
            // Json.NET quotes nonfinite floats when formatting JSON. Read a numeric float directly
            // so infinities clamp to endpoints and finite radius precision is retained.
            double value=token.Type==JTokenType.Float ? (double)token
                : valid ? double.Parse(raw,NumberStyles.Float,CultureInfo.InvariantCulture) : double.NaN;
            if(type==typeof(double)) return value;
            if(double.IsNaN(value)) return int.MinValue;
            if(value<=0) return value==0 ? 0 : int.MinValue+1;
            if(value>=4) return value==4 ? 4 : int.MaxValue;
            return value==Math.Truncate(value) ? (int)value : int.MinValue;
        }
        public override void WriteJson(JsonWriter writer,object? value,JsonSerializer serializer) => writer.WriteValue(value);
    }

    internal sealed class WeatherLeadConverter : JsonConverter<int>
    {
        public override int ReadJson(JsonReader reader, Type type, int old, bool exists, JsonSerializer serializer)
        {
            var token=JToken.Load(reader);
            if((token.Type==JTokenType.Integer || token.Type==JTokenType.Float || token.Type==JTokenType.String)
                && double.TryParse(token.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double value) && !double.IsNaN(value))
            {
                if(value<2) return 1;
                if(value>10) return 11;
                if(value==Math.Truncate(value)) return (int)value;
            }
            return int.MinValue;
        }
        public override void WriteJson(JsonWriter writer,int value,JsonSerializer serializer) => writer.WriteValue(value);
    }

    // Invalid individual values fall back without discarding unrelated settings.
    internal sealed class OptionalStopPercentageConverter : JsonConverter<int>
    {
        public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if ((token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.String)
                && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value))
            {
                if (value <= 0) return 0;
                if (value >= 100) return 100;
                if (value == Math.Truncate(value)) return (int)value;
            }
            return OptionalStopPolicy.ApprovedChoicePercentage;
        }
        public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) => writer.WriteValue(value);
    }
}
