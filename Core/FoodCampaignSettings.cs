using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed class FoodCampaignSettings
    {
        public bool Enabled { get; set; } = true;
        public double ReachProbability { get; set; } = .25;
        public int RetentionDays { get; set; } = 7;
        public double DailyReachDecay { get; set; } = .5;
        public void Clamp(Action<string>? log = null)
        {
            double Bound(double value, double min, double max, double fallback, string name)
            {
                double result = double.IsNaN(value) ? fallback : Math.Max(min, Math.Min(max, value));
                if (result != value) log?.Invoke(name + " clamped to " + result.ToString(CultureInfo.InvariantCulture));
                return result;
            }
            ReachProbability = Bound(ReachProbability, 0, 1, .25, "FoodMarketingCampaignReachProbability");
            RetentionDays = (int)Bound(RetentionDays == int.MinValue ? double.NaN : RetentionDays, 1, 7, 7, "FoodMarketingRetentionDays");
            DailyReachDecay = Bound(DailyReachDecay, 0, 1, .5, "FoodMarketingDailyReachDecay");
        }
    }

}
