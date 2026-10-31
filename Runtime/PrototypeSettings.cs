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
        public FoodMemorySettings FoodMemory { get; } = new FoodMemorySettings();
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryWeight { get => FoodMemory.FoodMemoryWeight; set => FoodMemory.FoodMemoryWeight=value; }
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryLoyalReturnMultiplier { get => FoodMemory.FoodMemoryLoyalReturnMultiplier; set => FoodMemory.FoodMemoryLoyalReturnMultiplier=value; }
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryCuriousNoveltyMultiplier { get => FoodMemory.FoodMemoryCuriousNoveltyMultiplier; set => FoodMemory.FoodMemoryCuriousNoveltyMultiplier=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 0, 4)]
        public int FoodMemoryServedGain { get => FoodMemory.FoodMemoryServedGain; set => FoodMemory.FoodMemoryServedGain=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 0, 4)]
        public int FoodMemoryStockPenalty { get => FoodMemory.FoodMemoryStockPenalty; set => FoodMemory.FoodMemoryStockPenalty=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 0, 4)]
        public int FoodMemoryClosedPenalty { get => FoodMemory.FoodMemoryClosedPenalty; set => FoodMemory.FoodMemoryClosedPenalty=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 0, 4)]
        public int FoodMemoryQueuePenalty { get => FoodMemory.FoodMemoryQueuePenalty; set => FoodMemory.FoodMemoryQueuePenalty=value; }
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryBadVisitTraitWeight { get => FoodMemory.FoodMemoryBadVisitTraitWeight; set => FoodMemory.FoodMemoryBadVisitTraitWeight=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 1, 14)]
        public int FoodMemoryDecayDaysPerPoint { get => FoodMemory.FoodMemoryDecayDaysPerPoint; set => FoodMemory.FoodMemoryDecayDaysPerPoint=value; }
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryMinimumMultiplier { get => FoodMemory.FoodMemoryMinimumMultiplier; set => FoodMemory.FoodMemoryMinimumMultiplier=value; }
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double FoodMemoryMaximumMultiplier { get => FoodMemory.FoodMemoryMaximumMultiplier; set => FoodMemory.FoodMemoryMaximumMultiplier=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 1, 4)]
        public int FoodMemoryCapacity { get => FoodMemory.FoodMemoryCapacity; set => FoodMemory.FoodMemoryCapacity=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 7, 90)]
        public int FoodMemoryForgetAfterDays { get => FoodMemory.FoodMemoryForgetAfterDays; set => FoodMemory.FoodMemoryForgetAfterDays=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 2, 10)]
        public int FoodMemoryRegularServedVisits { get => FoodMemory.FoodMemoryRegularServedVisits; set => FoodMemory.FoodMemoryRegularServedVisits=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 1, 28)]
        public int FoodMemoryRegularRecentDays { get => FoodMemory.FoodMemoryRegularRecentDays; set => FoodMemory.FoodMemoryRegularRecentDays=value; }
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

    internal sealed class FoodMemoryNumberConverter : JsonConverter<double>
    {
        internal static double Number(JsonReader reader)
        {
            var token = JToken.Load(reader);
            return (token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.String)
                && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : double.NaN;
        }
        public override double ReadJson(JsonReader reader, Type type, double existing, bool hasExisting, JsonSerializer serializer) => Number(reader);
        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer) => writer.WriteValue(value);
    }
    internal sealed class FoodMemoryIntegerConverter : JsonConverter<int>
    {
        private readonly int min, max;
        public FoodMemoryIntegerConverter(int min, int max) { this.min=min; this.max=max; }
        public override int ReadJson(JsonReader reader, Type type, int existing, bool hasExisting, JsonSerializer serializer)
        {
            double value=FoodMemoryNumberConverter.Number(reader);
            if (double.IsNaN(value)) return int.MinValue;
            if (value < min) return min-1;
            if (value > max) return max+1;
            return value == Math.Truncate(value) ? (int)value : int.MinValue;
        }
        public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) => writer.WriteValue(value);
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
