using System;

namespace PersistentCity.Core
{
    /// <summary>Clamped once at configuration load. Absent keys retain defaults; no tunable enters a draw key.</summary>
    public sealed class FoodChoiceSettings
    {
        public double FoodChoiceTemperature { get; set; }=1;
        public double FoodChoiceWeightFloor { get; set; }=.02;
        public double FoodChoiceTasteWeight { get; set; }=1;
        public double FoodChoicePriceWeight { get; set; }=1;
        public double FoodChoiceDistanceWeight { get; set; }=1;
        public double FoodChoiceDistanceScaleMinutes { get; set; }=10;
        public double FoodChoiceQueueWeight { get; set; }=1;
        public double FoodChoiceQualityWeight { get; set; }=.5;
        public double FoodChoiceTimePressureWeight { get; set; }=1;
        public double FoodChoiceHealthWeight { get; set; }
        public double FoodChoiceHouseholdWeight { get; set; }=1;
        public double FoodShortlistTemperature { get; set; }=1;

        internal FoodChoiceSettings Copy() => (FoodChoiceSettings)MemberwiseClone();

        public void Clamp(Action<string>? log=null)
        {
            double C(double v,double min,double max,double fallback,string key)
            {
                double c=double.IsNaN(v) ? fallback : Math.Max(min,Math.Min(max,v));
                if(c!=v) log?.Invoke(key+" clamped to "+c.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return c;
            }
            FoodChoiceTemperature=C(FoodChoiceTemperature,.25,4,1,nameof(FoodChoiceTemperature));
            FoodChoiceWeightFloor=C(FoodChoiceWeightFloor,.001,.25,.02,nameof(FoodChoiceWeightFloor));
            FoodChoiceTasteWeight=C(FoodChoiceTasteWeight,0,3,1,nameof(FoodChoiceTasteWeight));
            FoodChoicePriceWeight=C(FoodChoicePriceWeight,0,3,1,nameof(FoodChoicePriceWeight));
            FoodChoiceDistanceWeight=C(FoodChoiceDistanceWeight,0,3,1,nameof(FoodChoiceDistanceWeight));
            FoodChoiceDistanceScaleMinutes=C(FoodChoiceDistanceScaleMinutes,2,60,10,nameof(FoodChoiceDistanceScaleMinutes));
            FoodChoiceQueueWeight=C(FoodChoiceQueueWeight,0,3,1,nameof(FoodChoiceQueueWeight));
            FoodChoiceQualityWeight=C(FoodChoiceQualityWeight,0,3,.5,nameof(FoodChoiceQualityWeight));
            FoodChoiceTimePressureWeight=C(FoodChoiceTimePressureWeight,0,3,1,nameof(FoodChoiceTimePressureWeight));
            FoodChoiceHealthWeight=C(FoodChoiceHealthWeight,0,3,0,nameof(FoodChoiceHealthWeight));
            FoodChoiceHouseholdWeight=C(FoodChoiceHouseholdWeight,.1,5,1,nameof(FoodChoiceHouseholdWeight));
            FoodShortlistTemperature=C(FoodShortlistTemperature,.25,4,1,nameof(FoodShortlistTemperature));
        }
    }
}
