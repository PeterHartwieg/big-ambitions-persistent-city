using System;

namespace PersistentCity.Core
{
    public static class RivalFoodMemory
    {
        public static bool Closed(FoodAlternativeObservation observation, double minute)
        {
            if (!observation.Accessible || !observation.HasReadableDoor || observation.Hours.Count == 0
                || observation.Hours.Count > 64) return false;
            if (observation.TemporarilyClosed) return true;
            int day = (int)(minute / 1440);
            int weekday = ((day - 1) % 7 + 7) % 7;
            double time = minute - day * 1440.0;
            for (int i = 0; i < observation.Hours.Count; i++)
            {
                var window = observation.Hours[i];
                if (window.Day == weekday && time >= window.FromMinute && time < window.ToMinute) return false;
            }
            return true;
        }

        public static bool ModeledGeneration(string businessId)
        {
            if (!businessId.StartsWith("business:", StringComparison.Ordinal)) return false;
            int separators = 0;
            foreach (char c in businessId) if (c == ':' && ++separators > 5) return false;
            if (separators != 5) return false;
            var fields = businessId.Split(':');
            return fields.Length == 6 && fields[1].Length > 0 && fields[4].Length > 0
                && fields[5].Length > 0 && fields[5] != "player" && fields[5] != "unknown"
                && int.TryParse(fields[2], out _) && int.TryParse(fields[3], out int day) && day >= 0;
        }
    }

    public sealed partial class FoodCommerce
    {
        private void RivalExperience(ResidentMeal meal, ShopExperience? cause, double at)
        {
            if (cause.HasValue && meal.TargetKind == FoodAlternativeKind.Rival && city.State.Capabilities.Active)
                memoryStore.Experience(meal, cause.Value, (int)(at / 1440),
                    Traits(meal, Owner(meal)!), MemorySettings);
        }
    }
}
