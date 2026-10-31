using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed class PrototypeSettings
    {
        // Optional developer overrides; normal installation needs no configuration.
        private int optionalStopChoicePercentage = OptionalStopPolicy.ApprovedChoicePercentage;
        [JsonConverter(typeof(OptionalStopPercentageConverter))]
        public int OptionalStopChoicePercentage
        {
            get => optionalStopChoicePercentage;
            set => optionalStopChoicePercentage = Math.Max(0, Math.Min(100, value));
        }
        private readonly bool[] timingAdjusted = new bool[5];
        private bool timingReported;
        private int breakMaximum = 10;
        [JsonConverter(typeof(StreetLifeMinutesConverter))]
        public int StreetLifeBreakMaximumMinutes
        {
            get => breakMaximum;
            set { int effective = value == int.MinValue ? 10 : Math.Max(5, Math.Min(10, value)); timingAdjusted[0] |= effective != value; breakMaximum = effective; }
        }
        private int chatMaximum = 30;
        [JsonConverter(typeof(StreetLifeMinutesConverter))]
        public int StreetLifeChatMaximumMinutes
        {
            get => chatMaximum;
            set { int effective = value == int.MinValue ? 30 : Math.Max(10, Math.Min(30, value)); timingAdjusted[1] |= effective != value; chatMaximum = effective; }
        }
        private int windowShoppingMaximum = 3;
        [JsonConverter(typeof(StreetLifeMinutesConverter))]
        public int StreetLifeWindowShoppingMaximumMinutes
        {
            get => windowShoppingMaximum;
            set { int effective = value == int.MinValue ? 3 : Math.Max(1, Math.Min(3, value)); timingAdjusted[2] |= effective != value; windowShoppingMaximum = effective; }
        }
        private int audienceMaximum = 8;
        [JsonConverter(typeof(StreetLifeMinutesConverter))]
        public int StreetLifeAudienceMaximumMinutes
        {
            get => audienceMaximum;
            set { int effective = value == int.MinValue ? 8 : Math.Max(2, Math.Min(8, value)); timingAdjusted[3] |= effective != value; audienceMaximum = effective; }
        }
        private int restingMaximum = 40;
        [JsonConverter(typeof(StreetLifeMinutesConverter))]
        public int StreetLifeRestingMaximumMinutes
        {
            get => restingMaximum;
            set { int effective = value == int.MinValue ? 40 : Math.Max(10, Math.Min(40, value)); timingAdjusted[4] |= effective != value; restingMaximum = effective; }
        }
        public OptionalStopTiming StopTiming() => new OptionalStopTiming(StreetLifeBreakMaximumMinutes,
            StreetLifeChatMaximumMinutes, StreetLifeWindowShoppingMaximumMinutes, StreetLifeAudienceMaximumMinutes, StreetLifeRestingMaximumMinutes);
        public void ReportStreetLifeSettings(Action<string> report)
        {
            if (timingReported) return;
            timingReported = true;
            if (timingAdjusted[0]) report("StreetLifeBreakMaximumMinutes adjusted to " + StreetLifeBreakMaximumMinutes + " (default 10, range 5-10).");
            if (timingAdjusted[1]) report("StreetLifeChatMaximumMinutes adjusted to " + StreetLifeChatMaximumMinutes + " (default 30, range 10-30).");
            if (timingAdjusted[2]) report("StreetLifeWindowShoppingMaximumMinutes adjusted to " + StreetLifeWindowShoppingMaximumMinutes + " (default 3, range 1-3).");
            if (timingAdjusted[3]) report("StreetLifeAudienceMaximumMinutes adjusted to " + StreetLifeAudienceMaximumMinutes + " (default 8, range 2-8).");
            if (timingAdjusted[4]) report("StreetLifeRestingMaximumMinutes adjusted to " + StreetLifeRestingMaximumMinutes + " (default 40, range 10-40).");
            report("Street-life maxima (game minutes): break=" + StreetLifeBreakMaximumMinutes + ", chat=" + StreetLifeChatMaximumMinutes
                + ", windowShopping=" + StreetLifeWindowShoppingMaximumMinutes + ", audience=" + StreetLifeAudienceMaximumMinutes
                + ", resting=" + StreetLifeRestingMaximumMinutes + "; new stops only.");
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

    internal sealed class StreetLifeMinutesConverter : JsonConverter<int>
    {
        public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if ((token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.String)
                && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value))
            {
                int minimum, maximum;
                switch (reader.Path)
                {
                    case nameof(PrototypeSettings.StreetLifeBreakMaximumMinutes): minimum = 5; maximum = 10; break;
                    case nameof(PrototypeSettings.StreetLifeChatMaximumMinutes): minimum = 10; maximum = 30; break;
                    case nameof(PrototypeSettings.StreetLifeWindowShoppingMaximumMinutes): minimum = 1; maximum = 3; break;
                    case nameof(PrototypeSettings.StreetLifeAudienceMaximumMinutes): minimum = 2; maximum = 8; break;
                    default: minimum = 10; maximum = 40; break;
                }
                // Keep out-of-range values distinguishable for the once-only adjustment report.
                if (value < minimum) return minimum - 1;
                if (value > maximum) return maximum + 1;
                if (value == Math.Truncate(value)) return (int)value;
            }
            return int.MinValue; // Invalid individual value: property selects its documented default.
        }
        public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) => writer.WriteValue(value);
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
