using System;

namespace PersistentCity.Core
{
    /// <summary>D4 uses D2 observations and B1's existing tuple/watermark. No native sale evidence.</summary>
    public static class RivalFoodMemory
    {
        // Advertised windows are same-day [from,to), unlike the more general Place contract.
        // No temporary Place, opening-cache tree or second rival read for a completion check.
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

        // On-demand words only. D1's complete native rival signature has six escaped fields;
        // player experiences use FoodAuthority's additional :generation:N suffix. Never infer
        // a rival merely because a player scope retired, or consult live native state for words.
        public static bool ModeledGeneration(string businessId)
        {
            if (!businessId.StartsWith("business:", StringComparison.Ordinal)) return false;
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
                    Traits(meal, city.FindCitizen(meal.AnchorId)!), MemorySettings);
        }
    }
}
