using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed class RivalServiceProfile
    {
        public int Lanes { get; set; } = RivalServiceCalibration.DefaultLanes;
        public int MealPeriodUnits { get; set; } = RivalServiceCalibration.DefaultMealPeriodUnits;
    }

    public static class RivalServiceCalibration
    {
        public const int ReferencePlayerMealsPerHour = 30;
        public const int CalibrationBandPercent = 25;
        public const int ModerateCapacityPercent = 100;
        public const int GenerousCapacityPercent = 200;
        public const int DefaultLanes = 2;
        public const int DefaultMealPeriodUnits = 2 * ReferencePlayerMealsPerHour;
        public const int MaximumLanes = 8;
        public const int MaximumMealPeriodUnits = 10000;
        public const int MaximumProfiles = 64;
        public const int LunchStartsMinute = 660;
        public const int DinnerStartsMinute = 960;
        public const string Label = "estimated (modeled service limits)";
        public const string SellOutLabel = "modeled sell-out (estimated)";

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

    internal sealed class RivalPlaceLoad
    {
        internal readonly List<ResidentMeal> Serving = new List<ResidentMeal>(4);
        internal long PaidPeriod = long.MinValue;
        internal int PaidCount;
    }

    internal sealed class RivalServiceLoad
    {
        private readonly Dictionary<string, RivalPlaceLoad> places = new Dictionary<string, RivalPlaceLoad>(StringComparer.Ordinal);
        private readonly double[] latest = new double[RivalServiceCalibration.MaximumLanes];
        internal int Places => places.Count;

        internal static RivalServiceLoad Reconstruct(IEnumerable<ResidentMeal> meals, double now)
        {
            var load = new RivalServiceLoad();
            long period = RivalServiceCalibration.Period(now);
            foreach (var meal in meals) load.Observe(meal, period);
            return load;
        }
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

        internal double NextFree(string place, double now, int lanes)
        {
            if (!places.TryGetValue(place, out var load)) return now;
            int busy = 0;
            foreach (var meal in load.Serving)
            {
                double finish = meal.ServiceAt;
                if (finish <= now) continue;
                int at = Math.Min(busy, lanes - 1);
                if (busy == lanes && finish <= latest[at]) continue;
                while (at > 0 && latest[at - 1] < finish) { latest[at] = latest[at - 1]; at--; }
                latest[at] = finish; if (busy < lanes) busy++;
            }
            return busy < lanes ? now : latest[lanes - 1];
        }

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
        internal void Paid(ResidentMeal meal)
        {
            var load = Place(meal.PlaceId);
            load.Serving.Remove(meal);
            long period = RivalServiceCalibration.Period(Math.Floor(meal.ServiceAt));
            if (load.PaidPeriod != period) { load.PaidPeriod = period; load.PaidCount = 0; }
            load.PaidCount++;
        }
        internal void Forget(MealOutcomeRecord record)
        {
            if (!PaidRival(record) || !places.TryGetValue(record.PlaceId, out var load)) return;
            if (load.PaidPeriod == RivalServiceCalibration.Period(record.CheckoutAt) && load.PaidCount > 0) load.PaidCount--;
        }

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
