using System;
using System.Globalization;

namespace PersistentCity.Core
{
    // Legacy retains the identity and timing of an already saved single-meal intention.
    public enum MealKind { Legacy, Breakfast, Lunch, Dinner }

    /// <summary>Provisional meal preferences, not nutritional requirements or guaranteed sales.
    /// Stable resident habits plus small daily variation; no extra resident objects or frame polling.</summary>
    public static class MealRhythm
    {
        public const int PolicyVersion = 1;
        private static int Draw(CitizenState p, int seed, string key, int radius)
            => (int)(StableRandom.Hash(p.Identity.Id + ":meal:" + key, seed) % (uint)(radius * 2 + 1)) - radius;
        public static string IntentId(string id, int day, MealKind kind) => kind == MealKind.Legacy
            ? FoodDemand.IntentId(id, day)
            : "food:" + Uri.EscapeDataString(id) + ":" + kind.ToString().ToLowerInvariant() + ":" + day.ToString(CultureInfo.InvariantCulture);
        public static double DesiredAt(CitizenState person, int seed, int day, MealKind kind)
        {
            if (kind == MealKind.Legacy) return FoodRoutinePlanning.DesiredMealMinute(person, seed, day);
            double wake = DailyRhythm.OnDay(person, seed, day - 1).EndsAt;
            int offset = kind == MealKind.Breakfast ? 30 : kind == MealKind.Lunch ? 300 : 690;
            int radius = kind == MealKind.Breakfast ? 15 : kind == MealKind.Lunch ? 30 : 45;
            double desired = wake + offset + Draw(person, seed, kind + ":habit", radius)
                + Draw(person, seed, kind + ":day:" + day.ToString(CultureInfo.InvariantCulture), kind == MealKind.Breakfast ? 10 : 15);
            return Math.Min(desired, Math.Min((day + 1) * 1440.0, DailyRhythm.OnDay(person, seed, day).StartsAt) - 90);
        }
        public static double ExpiresAt(CitizenState person, int seed, int day, MealKind kind)
        {
            double desired = DesiredAt(person, seed, day, kind);
            int window = kind == MealKind.Legacy ? 240 : kind == MealKind.Breakfast ? 90 : kind == MealKind.Lunch ? 120 : 150;
            double end = Math.Min((day + 1) * 1440.0, desired + window);
            return kind == MealKind.Legacy ? end : Math.Min(end, DailyRhythm.OnDay(person, seed, day).StartsAt);
        }
        public static int EatingMinutes(MealKind kind) => kind == MealKind.Legacy ? 15
            : kind == MealKind.Breakfast ? 10 : kind == MealKind.Lunch ? 20 : 30;
        public static int BreakMinutes(CitizenState person, int seed, MealKind kind)
        {
            int available = FoodRoutinePlanning.BreakMinutes(person.Identity.Id, seed, person.Activity);
            if (kind == MealKind.Legacy || kind == MealKind.Lunch) return available;
            if (kind == MealKind.Breakfast) return Math.Min(45, available);
            return person.Activity == Activity.Work || person.Activity == Activity.Study ? available : 90;
        }
        public static int QueueMinutes(MealKind kind, Activity activity) => kind == MealKind.Breakfast ? 5
            : activity == Activity.Work || activity == Activity.Study ? 10 : kind == MealKind.Dinner ? 20 : 15;
        public static void Apply(FoodConsumer consumer, MealKind kind)
        {
            if (kind == MealKind.Legacy) return;
            consumer.ValueOfTimeCentsPerMinute = (int)Math.Round(consumer.ValueOfTimeCentsPerMinute
                * (kind == MealKind.Breakfast ? 1.3 : kind == MealKind.Dinner ? .8 : 1));
            consumer.ServiceQualityValueCents = (int)Math.Round(consumer.ServiceQualityValueCents
                * (kind == MealKind.Breakfast ? .6 : kind == MealKind.Dinner ? 1.6 : 1));
        }
        public static double ConvenienceWeight(CitizenState person, MealKind kind) => kind == MealKind.Breakfast ? .55
            : kind == MealKind.Dinner ? (person.Identity.Persona == Persona.Parent ? 1 : 1.25) : 1;
        public static int ProductPenalty(MealKind kind, string product) => FoodProducts.Penalty(kind, product);
    }
}
