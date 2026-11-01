using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed class FoodCampaignSettings
    {
        public bool Enabled { get; set; } = true;
        public double ReachProbability { get; set; } = .25;
        public double TargetMatchMultiplier { get; set; } = 2;
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
            TargetMatchMultiplier = Bound(TargetMatchMultiplier, 1, 4, 2, "FoodMarketingTargetMatchMultiplier");
            RetentionDays = (int)Bound(RetentionDays == int.MinValue ? double.NaN : RetentionDays, 1, 7, 7, "FoodMarketingRetentionDays");
            DailyReachDecay = Bound(DailyReachDecay, 0, 1, .5, "FoodMarketingDailyReachDecay");
        }
    }

    [Flags]
    public enum FoodCampaignGroup { Unknown = 0, Commuters = 1, HealthOriented = 2, Everyone = 4 }

    /// <summary>Explicit PC policy, separate from C1's Unknown native demographic evidence.</summary>
    public sealed class FoodCampaignTargetPolicy
    {
        private readonly FoodCampaignGroup[] groups = new FoodCampaignGroup[8];
        public static FoodCampaignTargetPolicy Unknown { get; } = new FoodCampaignTargetPolicy();
        public FoodCampaignTargetPolicy(params FoodCampaignGroup[] reviewedGroups)
        {
            if (reviewedGroups.Length > 8) throw new ArgumentException("At most eight reviewed types.");
            for (int i = 0; i < reviewedGroups.Length; i++)
            {
                if ((reviewedGroups[i] & ~(FoodCampaignGroup.Commuters | FoodCampaignGroup.HealthOriented | FoodCampaignGroup.Everyone)) != 0
                    || CampaignTypeGroups.VerifiedName(i) == null && reviewedGroups[i] != 0) throw new ArgumentException("Unverified type/group.");
                groups[i] = reviewedGroups[i];
            }
        }
        public FoodCampaignGroup Group(int type) => type >= 0 && type < 8 ? groups[type] : 0;
        public static FoodCampaignGroup Membership(ResidentTraits traits, bool commuteRoutine)
            => FoodCampaignGroup.Everyone | (commuteRoutine ? FoodCampaignGroup.Commuters : 0)
                | (traits.Band(ResidentTrait.HealthOrientation) >= 3 ? FoodCampaignGroup.HealthOriented : 0);
    }
}
