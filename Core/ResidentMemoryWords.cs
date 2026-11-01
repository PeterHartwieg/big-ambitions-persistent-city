using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Words from this consumer's own retained evidence. Called only for an open popup or
    /// explicit diagnostic. No reconstruction of missed failures or invented price/product causes.</summary>
    public static class ResidentMemoryWords
    {
        public static IReadOnlyList<string> Capture(FoodCommerceState food, ResidentMeal consumer,
            ResidentTraits? traits, int day, FoodMemorySettings settings,
            Func<int, BusinessReference?> reference, Func<string, string> label,
            Func<FoodScope, bool>? currentOwnShop = null)
        {
            if (traits == null || !traits.IsValid) return Array.Empty<string>();
            var words = new List<string>(5);
            if (traits.Band(ResidentTrait.LoyaltyCuriosity) <= 1) words.Add("curious, tries new places");
            foreach (var entry in consumer.Memory.Entries)
            {
                var shop = reference(entry.BusinessRef);
                if (shop == null || entry.Day > day || FoodMemoryPolicy.Expired(entry, day, settings)) continue;
                if (RivalFoodMemory.ModeledGeneration(shop.BusinessId))
                {
                    string rival = label(shop.PlaceId);
                    if (entry.Outcome == ShopExperience.Closed)
                        words.Add("found " + rival + " closed during a modeled rival visit; native service unobserved");
                    else if (entry.Outcome == ShopExperience.Served)
                        words.Add("remembers a modeled food purchase at " + rival + "; native service unobserved");
                    continue;
                }
                // Generation and place must still belong to an owned scope.
                var scope = food.Shops.Find(s => s.BusinessId == shop.BusinessId && s.PlaceId == shop.PlaceId);
                if (scope == null || currentOwnShop != null && !currentOwnShop(scope)) continue;
                string name = label(shop.PlaceId);
                int affinity = FoodMemoryPolicy.EffectiveAffinity(entry, day, traits, settings);
                if (RegularsProjection.IsRegular(entry, day, affinity, settings)) words.Add("a regular at " + name);
                else if (entry.Served >= 2 && affinity > 0 && day - entry.Day <= settings.FoodMemoryRegularRecentDays)
                    words.Add("usually returns to " + name);
                if (entry.Outcome == ShopExperience.Stockout) words.Add("was let down by a stockout at " + name);
                else if (entry.Outcome == ShopExperience.Closed) words.Add("found " + name + " closed");
                else if (entry.Outcome == ShopExperience.Queue) words.Add("was let down by the queue at " + name);
            }
            return words;
        }
    }
}
