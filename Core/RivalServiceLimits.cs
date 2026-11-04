using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>One business type's estimated service limits before the capacity percent is applied.
    /// Mod-owned estimates only: never a claim about native rival stock, staff or finances.</summary>
    public sealed class RivalServiceProfile
    {
        public int Lanes { get; set; } = RivalServiceCalibration.DefaultLanes;
        public int MealPeriodUnits { get; set; } = RivalServiceCalibration.DefaultMealPeriodUnits;
    }

    /// <summary>F6b calibration (plan section 3.2, Peter's Q2 default). The reference is the verified native
    /// K1 player shop: two staffed counters serving 30 meals/hour (docs/checks/history-2026-10-development.md).
    /// Two lanes at the default five-minute estimate give 24/hour, -20% of that reference, inside the ±25% band.</summary>
    public static class RivalServiceCalibration
    {
        public const int ReferencePlayerMealsPerHour = 30;
        public const int CalibrationBandPercent = 25;
        // Q2 "how tight should rivals feel?": Moderate (about a comparable player shop) is the default.
        // Peter's alternative, Generous (about 2x), is RivalCapacityPercent = GenerousCapacityPercent.
        public const int ModerateCapacityPercent = 100;
        public const int GenerousCapacityPercent = 200;
        public const int DefaultLanes = 2;
        // Two rush hours at the reference staffed ceiling.
        public const int DefaultMealPeriodUnits = 2 * ReferencePlayerMealsPerHour;
        public const int MaximumLanes = 8;
        public const int MaximumMealPeriodUnits = 10000;
        public const int MaximumProfiles = 64;
        // Clock meal periods: breakfast [00:00,11:00), lunch [11:00,16:00), dinner [16:00,24:00).
        public const int LunchStartsMinute = 660;
        public const int DinnerStartsMinute = 960;
        public const string Label = "estimated (modeled service limits)";
        public const string SellOutLabel = "modeled sell-out (estimated)";

        /// <summary>Absolute meal-period number. Boundaries are whole minutes, so a fractional service
        /// time and its whole-minute outcome record always fall in the same period.</summary>
        public static long Period(double minute)
        {
            long day = (long)Math.Floor(minute / 1440);
            double time = minute - day * 1440.0;
            return day * 3 + (time < LunchStartsMinute ? 0 : time < DinnerStartsMinute ? 1 : 2);
        }
        public static int Scale(int value, int percent, int maximum)
            => Math.Max(1, Math.Min(maximum, (int)Math.Round(value * (long)percent / 100.0, MidpointRounding.AwayFromZero)));
        public static double CeilingMealsPerHour(int lanes, int serviceMinutes) => lanes * 60.0 / serviceMinutes;
    }

    public sealed partial class RivalFoodSettings
    {
        public int RivalCapacityPercent { get; set; } = RivalServiceCalibration.ModerateCapacityPercent;
        public int RivalServiceLanes { get; set; } = RivalServiceCalibration.DefaultLanes;
        public int RivalMealPeriodUnits { get; set; } = RivalServiceCalibration.DefaultMealPeriodUnits;
        /// <summary>Optional per native business type overrides, for example "ba:businesstype_coffeeshop".</summary>
        public Dictionary<string, RivalServiceProfile> RivalServiceProfiles { get; set; } = new Dictionary<string, RivalServiceProfile>(StringComparer.Ordinal);

        public int Lanes(string businessType) => RivalServiceCalibration.Scale(Profile(businessType)?.Lanes ?? RivalServiceLanes,
            RivalCapacityPercent, RivalServiceCalibration.MaximumLanes);
        public int MealPeriodUnits(string businessType) => RivalServiceCalibration.Scale(Profile(businessType)?.MealPeriodUnits ?? RivalMealPeriodUnits,
            RivalCapacityPercent, RivalServiceCalibration.MaximumMealPeriodUnits);
        private RivalServiceProfile? Profile(string businessType)
            => businessType != null && RivalServiceProfiles.TryGetValue(businessType, out var profile) ? profile : null;

        private void ClampServiceLimits(Func<int, int, int, int, string, int> integer, Action<string>? log)
        {
            RivalCapacityPercent = integer(RivalCapacityPercent, 25, 400, RivalServiceCalibration.ModerateCapacityPercent, nameof(RivalCapacityPercent));
            RivalServiceLanes = integer(RivalServiceLanes, 1, RivalServiceCalibration.MaximumLanes, RivalServiceCalibration.DefaultLanes, nameof(RivalServiceLanes));
            RivalMealPeriodUnits = integer(RivalMealPeriodUnits, 1, RivalServiceCalibration.MaximumMealPeriodUnits, RivalServiceCalibration.DefaultMealPeriodUnits, nameof(RivalMealPeriodUnits));
            var source = RivalServiceProfiles ?? new Dictionary<string, RivalServiceProfile>();
            var profiles = new Dictionary<string, RivalServiceProfile>(StringComparer.Ordinal);
            var keys = new List<string>(source.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key) || source[key] == null || profiles.Count == RivalServiceCalibration.MaximumProfiles)
                { log?.Invoke(nameof(RivalServiceProfiles) + " entry ignored: " + key); continue; }
                profiles.Add(key, new RivalServiceProfile {
                    Lanes = integer(source[key].Lanes, 1, RivalServiceCalibration.MaximumLanes, RivalServiceLanes, key + ".Lanes"),
                    MealPeriodUnits = integer(source[key].MealPeriodUnits, 1, RivalServiceCalibration.MaximumMealPeriodUnits, RivalMealPeriodUnits, key + ".MealPeriodUnits") });
            }
            RivalServiceProfiles = profiles;
        }
        internal string ServiceLimitsText() => "lanes=" + Lanes("").ToString(CultureInfo.InvariantCulture)
            + ", units/meal period=" + MealPeriodUnits("").ToString(CultureInfo.InvariantCulture)
            + ", capacity=" + RivalCapacityPercent.ToString(CultureInfo.InvariantCulture) + "%, type profiles=" + RivalServiceProfiles.Count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Transient F6b load of one rival place. Derived only from saved state: active Serving rival
    /// meals (the lane schedule) plus paid rival outcome records of the current meal period. Never saved.</summary>
    internal sealed class RivalPlaceLoad
    {
        internal readonly List<ResidentMeal> Serving = new List<ResidentMeal>(4);
        internal long PaidPeriod = long.MinValue;
        internal int PaidCount;
    }

    /// <summary>Estimated lanes and meal-period sell-out for every rival, keyed by place. Each lane serves one
    /// customer per estimated service time. A lane is busy while a Serving meal's ServiceAt is later than now,
    /// so the earliest free lane is the lanes-th latest Serving finish. Arrival and completion are O(serving at
    /// that rival, bounded by lanes x queue patience) with no allocation once the place entry exists.</summary>
    internal sealed class RivalServiceLoad
    {
        private readonly Dictionary<string, RivalPlaceLoad> places = new Dictionary<string, RivalPlaceLoad>(StringComparer.Ordinal);
        private readonly double[] latest = new double[RivalServiceCalibration.MaximumLanes];
        internal int Places => places.Count;

        /// <summary>Attach-time reconstruction. One pass over consumers and at most three outcome records each.</summary>
        internal static RivalServiceLoad Reconstruct(IEnumerable<ResidentMeal> meals, double now)
        {
            var load = new RivalServiceLoad();
            long period = RivalServiceCalibration.Period(now);
            foreach (var meal in meals) load.Observe(meal, period);
            return load;
        }
        /// <summary>One saved consumer's contribution, inside FoodCommerce's existing attach pass.</summary>
        internal void Observe(ResidentMeal meal, long period)
        {
            if (meal.Status == MealStatus.Serving && meal.TargetKind == FoodAlternativeKind.Rival) Place(meal.PlaceId).Serving.Add(meal);
            var today = meal.Today;
            if (today == null) return;
            for (int i = 0; i < today.Meals.Count; i++)
            {
                var record = today.Meals[i];
                if (!PaidRival(record) || RivalServiceCalibration.Period(record.CheckoutAt) != period) continue;
                var place = Place(record.PlaceId);
                place.PaidPeriod = period; place.PaidCount++;
            }
        }
        internal static bool PaidRival(MealOutcomeRecord record) => record.TargetKind == FoodAlternativeKind.Rival
            && record.Status == MealOutcomeStatus.Bought && record.PlaceId.Length > 0;
        private RivalPlaceLoad Place(string place)
        {
            if (!places.TryGetValue(place, out var load)) places.Add(place, load = new RivalPlaceLoad());
            return load;
        }

        /// <summary>Earliest minute a lane can start a new customer at this rival.</summary>
        internal double NextFree(string place, double now, int lanes)
        {
            if (!places.TryGetValue(place, out var load)) return now;
            int busy = 0;
            foreach (var meal in load.Serving)
            {
                double finish = meal.ServiceAt;
                if (finish <= now) continue;
                // Keep the lanes latest finishes in descending order; the last one frees first.
                int at = Math.Min(busy, lanes - 1);
                if (busy == lanes && finish <= latest[at]) continue;
                while (at > 0 && latest[at - 1] < finish) { latest[at] = latest[at - 1]; at--; }
                latest[at] = finish; if (busy < lanes) busy++;
            }
            return busy < lanes ? now : latest[lanes - 1];
        }

        /// <summary>Paid servings in the period plus committed Serving meals finishing in it.</summary>
        internal int Count(string place, long period)
        {
            if (!places.TryGetValue(place, out var load)) return 0;
            int count = load.PaidPeriod == period ? load.PaidCount : 0;
            foreach (var meal in load.Serving) if (RivalServiceCalibration.Period(meal.ServiceAt) == period) count++;
            return count;
        }

        internal void Admit(ResidentMeal meal) => Place(meal.PlaceId).Serving.Add(meal);
        internal void Leave(ResidentMeal meal)
        {
            if (places.TryGetValue(meal.PlaceId, out var load)) load.Serving.Remove(meal);
        }
        /// <summary>Settlement: ServiceAt is the checkout minute that the outcome record will carry.</summary>
        internal void Paid(ResidentMeal meal)
        {
            var load = Place(meal.PlaceId);
            load.Serving.Remove(meal);
            long period = RivalServiceCalibration.Period(Math.Floor(meal.ServiceAt));
            if (load.PaidPeriod != period) { load.PaidPeriod = period; load.PaidCount = 0; }
            load.PaidCount++;
        }
        /// <summary>A consumer's lazy day rollover drops its outcome records; reconstruction no longer sees them.</summary>
        internal void Forget(MealOutcomeRecord record)
        {
            if (!PaidRival(record) || !places.TryGetValue(record.PlaceId, out var load)) return;
            if (load.PaidPeriod == RivalServiceCalibration.Period(record.CheckoutAt) && load.PaidCount > 0) load.PaidCount--;
        }

        /// <summary>Order-independent canonical image for reload equivalence tests and rig diagnostics.</summary>
        internal string Image(double now)
        {
            long period = RivalServiceCalibration.Period(now);
            var keys = new List<string>(places.Keys); keys.Sort(StringComparer.Ordinal);
            var text = new System.Text.StringBuilder();
            foreach (var key in keys)
            {
                var load = places[key];
                int paid = load.PaidPeriod == period ? load.PaidCount : 0;
                if (paid == 0 && load.Serving.Count == 0) continue;
                var finishes = new List<double>(load.Serving.Count);
                foreach (var meal in load.Serving) finishes.Add(meal.ServiceAt);
                finishes.Sort();
                text.Append(key).Append('|').Append(paid.ToString(CultureInfo.InvariantCulture));
                foreach (var finish in finishes) text.Append('|').Append(finish.ToString("R", CultureInfo.InvariantCulture));
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
