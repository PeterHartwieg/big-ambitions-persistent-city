using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public readonly struct MealOutcomeCount
    {
        public MealOutcomeCount(string outcome, int count) { Outcome = outcome; Count = count; }
        public string Outcome { get; }
        public int Count { get; }
    }

    public static class FoodClockReach
    {
        public const int LunchBreakMinutes = 60;

        public const double ServiceMinutes = 1;

        public static double LunchReachMetres(double walkingMetresPerGameMinute, double serviceMinutes = ServiceMinutes)
            => Math.Max(0, LunchBreakMinutes - MealRhythm.EatingMinutes(MealKind.Lunch) - Math.Max(0, serviceMinutes)) / 2.0 * walkingMetresPerGameMinute;

        public static string RetimeLine(double clockRate, double walkingMetresPerGameMinute)
            => "Food reach at clock rate " + clockRate.ToString("0.000", CultureInfo.InvariantCulture)
                + " game minutes per second: walking covers " + walkingMetresPerGameMinute.ToString("0.0", CultureInfo.InvariantCulture)
                + " m per game minute, so a " + LunchBreakMinutes.ToString(CultureInfo.InvariantCulture)
                + "-minute lunch break with " + MealRhythm.EatingMinutes(MealKind.Lunch).ToString(CultureInfo.InvariantCulture)
                + " minutes eating and " + ServiceMinutes.ToString("0", CultureInfo.InvariantCulture) + " minute service reaches food shops within "
                + Math.Round(LunchReachMetres(walkingMetresPerGameMinute)).ToString("0", CultureInfo.InvariantCulture) + " m one way.";

        public static MealOutcomeCount[] Outcomes(FoodCommerceState? food, int top)
        {
            if (food == null || top <= 0) return Array.Empty<MealOutcomeCount>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var meal in food.Residents)
            {
                if (meal.Status == MealStatus.Pending) continue;
                string key = meal.Status + ": " + meal.Reason;
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            return counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(top)
                .Select(p => new MealOutcomeCount(p.Key, p.Value)).ToArray();
        }
    }
}
