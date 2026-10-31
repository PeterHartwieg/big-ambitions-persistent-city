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
