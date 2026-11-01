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

        private string interiorCityView = "off";
        private double interiorRadius = 60;
        private int interiorPedestrians = 24, interiorVehicles = 12;
        private bool modeAdjusted, radiusAdjusted, pedestriansAdjusted, vehiclesAdjusted, interiorReported;
        [JsonConverter(typeof(InteriorModeConverter))]
        public string InteriorCityView
        {
            get => interiorCityView;
            set { modeAdjusted |= value != "off" && value != "on"; interiorCityView = value == "on" ? "on" : "off"; }
        }
        [JsonConverter(typeof(InteriorNumberConverter))]
        public double InteriorViewRadiusMetres
        {
            get => interiorRadius;
            set { double effective = double.IsNaN(value) ? 60 : Math.Max(30, Math.Min(120, value)); radiusAdjusted |= effective != value; interiorRadius = effective; }
        }
        [JsonConverter(typeof(InteriorNumberConverter))]
        public int InteriorMaximumPedestrians
        {
            get => Math.Min(MaximumPedestrians, interiorPedestrians);
            set { int effective = value == int.MinValue ? 24 : Math.Max(0, Math.Min(48, value)); pedestriansAdjusted |= effective != value; interiorPedestrians = effective; }
        }
        [JsonConverter(typeof(InteriorNumberConverter))]
        public int InteriorMaximumVehicles
        {
            get => Math.Min(MaximumVehicles, interiorVehicles);
            set { int effective = value == int.MinValue ? 12 : Math.Max(0, Math.Min(24, value)); vehiclesAdjusted |= effective != value; interiorVehicles = effective; }
        }
        // Called once after the unchanged flat loader has finished (including cross-field caps).
        public void ReportInteriorSettings(Action<string> report)
        {
            if (interiorReported) return;
            interiorReported = true;
            if (modeAdjusted) report("InteriorCityView invalid; using off.");
            if (radiusAdjusted) report("InteriorViewRadiusMetres adjusted to " + interiorRadius.ToString(CultureInfo.InvariantCulture) + " (default 60, range 30-120).");
            if (pedestriansAdjusted || interiorPedestrians > MaximumPedestrians) report("InteriorMaximumPedestrians adjusted to " + InteriorMaximumPedestrians + " (default 24, range 0-48, within global cap).");
            if (vehiclesAdjusted || interiorVehicles > MaximumVehicles) report("InteriorMaximumVehicles adjusted to " + InteriorMaximumVehicles + " (default 12, range 0-24, within global cap).");
            report("InteriorCityView=" + interiorCityView + ", radius=" + interiorRadius.ToString(CultureInfo.InvariantCulture)
                + "m, pedestrians=" + InteriorMaximumPedestrians + ", vehicles=" + InteriorMaximumVehicles + "; city drawing prototype=no-op (F0).");
        }
    }

    internal sealed class InteriorModeConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
        { var token = JToken.Load(reader); return token.Type == JTokenType.String ? token.ToString() : ""; }
        public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer) => writer.WriteValue(value);
    }

    internal sealed class InteriorNumberConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int) || objectType == typeof(double);
        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            double value = double.NaN;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.String)
                if (!double.TryParse(token.Type == JTokenType.String ? token.ToString() : token.ToString(Formatting.None),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out value)) value = double.NaN;
            if (objectType == typeof(double)) return value;
            // Preserve endpoint clamping even for infinity; unusable in-range fractions default.
            int maximum = reader.Path == nameof(PrototypeSettings.InteriorMaximumPedestrians) ? 48 : 24;
            if (double.IsNaN(value) || value > 0 && value < maximum && value != Math.Truncate(value)) return int.MinValue;
            if (value < 0) return -1;
            if (value > maximum) return int.MaxValue;
            return (int)value;
        }
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => writer.WriteValue(value);
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
