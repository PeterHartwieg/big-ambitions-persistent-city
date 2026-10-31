using System;

namespace PersistentCity.Core
{
    /// <summary>Pure lazy projection. Reads at most four entries; no catalogue, routes or mutation.</summary>
    public static class FoodMemoryPolicy
    {
        public static bool Expired(ShopMemory entry, int day, FoodMemorySettings settings)
            => day - entry.Day > settings.FoodMemoryForgetAfterDays;

        public static int EffectiveAffinity(ShopMemory entry, int day, ResidentTraits traits, FoodMemorySettings settings)
        {
            if (Expired(entry, day, settings)) return 0;
            double persistence = .5 + 1.5 * traits.Band(ResidentTrait.LoyaltyCuriosity) / 4.0;
            int decay = (int)(Math.Max(0, day - entry.Day) / (settings.FoodMemoryDecayDaysPerPoint * persistence));
            return entry.Affinity < 0 ? Math.Min(0, entry.Affinity + decay) : Math.Max(0, entry.Affinity - decay);
        }

        public static double Contribution(ConsumerMemory memory, int businessRef, int day, ResidentTraits traits, FoodMemorySettings settings)
        {
            if (settings.FoodMemoryWeight == 0) return 0;
            ShopMemory? remembered = null;
            for (int i = 0; i < memory.Entries.Count; i++)
                if (memory.Entries[i].BusinessRef == businessRef && !Expired(memory.Entries[i], day, settings))
                { remembered = memory.Entries[i]; break; }
            double loyalty = traits.Band(ResidentTrait.LoyaltyCuriosity) / 4.0;
            double multiplier;
            if (remembered == null)
                multiplier = 1 + (1 - loyalty) * (settings.FoodMemoryCuriousNoveltyMultiplier - 1);
            else
            {
                int affinity = EffectiveAffinity(remembered, day, traits, settings);
                // Both signs are monotone in affinity. No served-count bonus can mask disappointment.
                multiplier = affinity < 0
                    ? Math.Exp(-affinity / 8.0 * Math.Log(settings.FoodMemoryMinimumMultiplier))
                    : 1 + affinity / 8.0 * (.1 + .9 * loyalty) * (settings.FoodMemoryLoyalReturnMultiplier - 1);
            }
            multiplier = Math.Max(settings.FoodMemoryMinimumMultiplier, Math.Min(settings.FoodMemoryMaximumMultiplier, multiplier));
            return settings.FoodMemoryWeight * Math.Log(multiplier);
        }

        public static int Penalty(ShopExperience cause, ResidentTraits traits, FoodMemorySettings settings)
        {
            int configured = cause == ShopExperience.Stockout ? settings.FoodMemoryStockPenalty
                : cause == ShopExperience.Closed ? settings.FoodMemoryClosedPenalty : settings.FoodMemoryQueuePenalty;
            double sensitivity = cause == ShopExperience.Queue ? (4 - traits.Band(ResidentTrait.QueuePatience)) / 4.0
                : traits.Band(ResidentTrait.TimePressure) / 4.0;
            double forgiveness = 1 - .5 * traits.Band(ResidentTrait.LoyaltyCuriosity) / 4.0;
            // Zero configured magnitude still retains the minimum honest negative signal.
            return Math.Max(1, (int)Math.Ceiling(configured * (1 + settings.FoodMemoryBadVisitTraitWeight * sensitivity) * forgiveness));
        }

        /// <summary>Independent eviction ordering: expired, least effective affinity, oldest, ordinal generation ID.</summary>
        public static int CompareEviction(ShopMemory a, string aId, ShopMemory b, string bId, int day, ResidentTraits traits, FoodMemorySettings settings)
        {
            int order = Expired(b, day, settings).CompareTo(Expired(a, day, settings));
            if (order == 0) order = EffectiveAffinity(a, day, traits, settings).CompareTo(EffectiveAffinity(b, day, traits, settings));
            if (order == 0) order = a.Day.CompareTo(b.Day);
            return order == 0 ? StringComparer.Ordinal.Compare(aId, bId) : order;
        }
    }
}
