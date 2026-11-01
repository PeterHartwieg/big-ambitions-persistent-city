using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed partial class PrototypeSettings
    {
        [JsonIgnore]
        public FoodChoiceSettings FoodChoice { get; } = new FoodChoiceSettings();
        [JsonIgnore] public RivalFoodSettings Rivals { get; } = new RivalFoodSettings();
        [JsonConverter(typeof(FoodMemoryNumberConverter))]
        public double RivalChoiceWeight { get => Rivals.RivalChoiceWeight; set => Rivals.RivalChoiceWeight=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 1, 30)]
        public int RivalAssumedServiceMinutes { get => Rivals.RivalAssumedServiceMinutes; set => Rivals.RivalAssumedServiceMinutes=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 15, 240)]
        public int RivalRefreshGameMinutes { get => Rivals.RivalRefreshGameMinutes; set => Rivals.RivalRefreshGameMinutes=value; }
        [JsonConverter(typeof(FoodMemoryIntegerConverter), 1, 64)]
        public int RivalReadsPerUnit { get => Rivals.RivalReadsPerUnit; set => Rivals.RivalReadsPerUnit=value; }

        [JsonConverter(typeof(StreetLifeMinutesConverter), 1, 8)]
        public int FoodMenuMaximumOffersPerShop { get => FoodChoice.FoodMenuMaximumOffersPerShop; set => FoodChoice.FoodMenuMaximumOffersPerShop=value; }
        [JsonConverter(typeof(FoodMenuNumberConverter))]
        public double FoodMenuNewTasteWeight { get => FoodChoice.FoodMenuNewTasteWeight; set => FoodChoice.FoodMenuNewTasteWeight=value; }
        [JsonConverter(typeof(FoodMenuNumberConverter))]
        public double FoodMenuHealthWeight { get => FoodChoice.FoodMenuHealthWeight; set => FoodChoice.FoodMenuHealthWeight=value; }
        [JsonIgnore]
        public MarketResearchSettings MarketResearch { get; } = new MarketResearchSettings();
        [JsonConverter(typeof(ResearchCatchmentSettingConverter))]
        public int MarketResearchCatchmentMetres { get => MarketResearch.MarketResearchCatchmentMetres; set => MarketResearch.MarketResearchCatchmentMetres=value; }
        [JsonConverter(typeof(ResearchUnitSettingConverter))]
        public int MarketResearchConsumersPerUnit { get => MarketResearch.MarketResearchConsumersPerUnit; set => MarketResearch.MarketResearchConsumersPerUnit=value; }
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
        [JsonIgnore]
        public WeatherSettings Weather { get; } = new WeatherSettings();
        [JsonConverter(typeof(WeatherBooleanConverter))]
        public bool? WeatherEnabled { get => Weather.Enabled; set => Weather.SetEnabled(value); }
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
        private readonly bool[] timingAdjusted = new bool[5];
        private bool timingReported;
        private int breakMaximum = 10;
        [JsonConverter(typeof(StreetLifeMinutesConverter), 5, 10)]
        public int StreetLifeBreakMaximumMinutes
        {
            get => breakMaximum;
            set { int effective = value == int.MinValue ? 10 : Math.Max(5, Math.Min(10, value)); timingAdjusted[0] |= effective != value; breakMaximum = effective; }
        }
        private int chatMaximum = 30;
        [JsonConverter(typeof(StreetLifeMinutesConverter), 10, 30)]
        public int StreetLifeChatMaximumMinutes
        {
            get => chatMaximum;
            set { int effective = value == int.MinValue ? 30 : Math.Max(10, Math.Min(30, value)); timingAdjusted[1] |= effective != value; chatMaximum = effective; }
        }
        private int windowShoppingMaximum = 3;
        [JsonConverter(typeof(StreetLifeMinutesConverter), 1, 3)]
        public int StreetLifeWindowShoppingMaximumMinutes
        {
            get => windowShoppingMaximum;
            set { int effective = value == int.MinValue ? 3 : Math.Max(1, Math.Min(3, value)); timingAdjusted[2] |= effective != value; windowShoppingMaximum = effective; }
        }
        private int audienceMaximum = 8;
        [JsonConverter(typeof(StreetLifeMinutesConverter), 2, 8)]
        public int StreetLifeAudienceMaximumMinutes
        {
            get => audienceMaximum;
            set { int effective = value == int.MinValue ? 8 : Math.Max(2, Math.Min(8, value)); timingAdjusted[3] |= effective != value; audienceMaximum = effective; }
        }
        private int restingMaximum = 40;
        [JsonConverter(typeof(StreetLifeMinutesConverter), 10, 40)]
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
        private readonly int minimum, maximum;
        public StreetLifeMinutesConverter(int minimum, int maximum)
        { this.minimum = minimum; this.maximum = maximum; }
        public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            double value;
            // Numeric tokens already hold a number. Formatting them as localized text before
            // invariant parsing changes endpoint clamps under comma-decimal cultures.
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) value = (double)token;
            else if (token.Type != JTokenType.String || !double.TryParse(token.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)) return int.MinValue;
            if (!double.IsNaN(value))
            {
                // Bounds belong to the property, independent of accepted JSON key spelling.
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
    internal sealed class FoodMenuNumberConverter : JsonConverter<double>
    {
        public override double ReadJson(JsonReader reader, Type type, double existing, bool hasExisting, JsonSerializer serializer)
        {
            var token=JToken.Load(reader);
            if(token.Type==JTokenType.Integer || token.Type==JTokenType.Float) return (double)token;
            return token.Type==JTokenType.String && double.TryParse((string)token!,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) ? value : double.NaN;
        }
        public override void WriteJson(JsonWriter writer,double value,JsonSerializer serializer) => writer.WriteValue(value);
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

    // Normalize the token before Int32 conversion, preserving unrelated settings even for
    // null, structured or malformed individual values. Diagnostics use effective values only.
    internal abstract class ResearchIntegerSettingConverter : JsonConverter<int>
    {
        private readonly int minimum, maximum, fallback;
        protected ResearchIntegerSettingConverter(int minimum, int maximum, int fallback)
        { this.minimum=minimum; this.maximum=maximum; this.fallback=fallback; }

        public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float && reader.TokenType != JsonToken.String)
            { reader.Skip(); return fallback; }
            if (double.TryParse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value) && !double.IsNaN(value))
            {
                if (value <= minimum) return minimum;
                if (value >= maximum) return maximum;
                if (value == Math.Truncate(value)) return (int)value;
            }
            return fallback;
        }
        public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) => writer.WriteValue(value);
    }

    internal sealed class ResearchCatchmentSettingConverter : ResearchIntegerSettingConverter
    {
        public ResearchCatchmentSettingConverter() : base(50, 2000, 500) { }
    }
    internal sealed class ResearchUnitSettingConverter : ResearchIntegerSettingConverter
    {
        public ResearchUnitSettingConverter() : base(16, 1024, 256) { }
    }

    internal sealed class WeatherBooleanConverter : JsonConverter<bool?>
    {
        public override bool? ReadJson(JsonReader reader,Type type,bool? old,bool exists,JsonSerializer serializer)
        {
            var token=JToken.Load(reader);
            if(token.Type==JTokenType.Boolean) return (bool)token;
            if(token.Type==JTokenType.String && bool.TryParse((string)token!,out bool value)) return value;
            return null;
        }
        public override void WriteJson(JsonWriter writer,bool? value,JsonSerializer serializer) => writer.WriteValue(value);
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
