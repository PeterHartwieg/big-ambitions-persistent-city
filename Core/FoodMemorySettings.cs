using System;

namespace PersistentCity.Core
{
    public sealed class FoodMemorySettings
    {
        public double FoodMemoryWeight { get; set; } = 1;
        public double FoodMemoryLoyalReturnMultiplier { get; set; } = 2;
        public double FoodMemoryCuriousNoveltyMultiplier { get; set; } = 1.3;
        public int FoodMemoryServedGain { get; set; } = 1;
        public int FoodMemoryStockPenalty { get; set; } = 3;
        public int FoodMemoryClosedPenalty { get; set; } = 3;
        public int FoodMemoryQueuePenalty { get; set; } = 2;
        public double FoodMemoryBadVisitTraitWeight { get; set; } = 1;
        public int FoodMemoryDecayDaysPerPoint { get; set; } = 2;
        public double FoodMemoryMinimumMultiplier { get; set; } = .25;
        public double FoodMemoryMaximumMultiplier { get; set; } = 2.5;
        public int FoodMemoryCapacity { get; set; } = 4;
        public int FoodMemoryForgetAfterDays { get; set; } = 28;

        public void Clamp(Action<string>? log = null)
        {
            double C(double value, double min, double max, double fallback, string key)
            {
                double result = double.IsNaN(value) ? fallback : Math.Max(min, Math.Min(max, value));
                if (result != value) log?.Invoke(key + " clamped to " + result.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return result;
            }
            FoodMemoryWeight = C(FoodMemoryWeight, 0, 2, 1, nameof(FoodMemoryWeight));
            FoodMemoryLoyalReturnMultiplier = C(FoodMemoryLoyalReturnMultiplier, 1, 3, 2, nameof(FoodMemoryLoyalReturnMultiplier));
            FoodMemoryCuriousNoveltyMultiplier = C(FoodMemoryCuriousNoveltyMultiplier, 1, 3, 1.3, nameof(FoodMemoryCuriousNoveltyMultiplier));
            FoodMemoryServedGain = (int)C(FoodMemoryServedGain == int.MinValue ? double.NaN : FoodMemoryServedGain, 0, 4, 1, nameof(FoodMemoryServedGain));
            FoodMemoryStockPenalty = (int)C(FoodMemoryStockPenalty == int.MinValue ? double.NaN : FoodMemoryStockPenalty, 0, 4, 3, nameof(FoodMemoryStockPenalty));
            FoodMemoryClosedPenalty = (int)C(FoodMemoryClosedPenalty == int.MinValue ? double.NaN : FoodMemoryClosedPenalty, 0, 4, 3, nameof(FoodMemoryClosedPenalty));
            FoodMemoryQueuePenalty = (int)C(FoodMemoryQueuePenalty == int.MinValue ? double.NaN : FoodMemoryQueuePenalty, 0, 4, 2, nameof(FoodMemoryQueuePenalty));
            FoodMemoryBadVisitTraitWeight = C(FoodMemoryBadVisitTraitWeight, 0, 2, 1, nameof(FoodMemoryBadVisitTraitWeight));
            FoodMemoryDecayDaysPerPoint = (int)C(FoodMemoryDecayDaysPerPoint == int.MinValue ? double.NaN : FoodMemoryDecayDaysPerPoint, 1, 14, 2, nameof(FoodMemoryDecayDaysPerPoint));
            FoodMemoryMinimumMultiplier = C(FoodMemoryMinimumMultiplier, .1, 1, .25, nameof(FoodMemoryMinimumMultiplier));
            FoodMemoryMaximumMultiplier = C(FoodMemoryMaximumMultiplier, 1, 3, 2.5, nameof(FoodMemoryMaximumMultiplier));
            FoodMemoryCapacity = (int)C(FoodMemoryCapacity == int.MinValue ? double.NaN : FoodMemoryCapacity, 1, 4, 4, nameof(FoodMemoryCapacity));
            FoodMemoryForgetAfterDays = (int)C(FoodMemoryForgetAfterDays == int.MinValue ? double.NaN : FoodMemoryForgetAfterDays, 7, 90, 28, nameof(FoodMemoryForgetAfterDays));
        }
    }
}
