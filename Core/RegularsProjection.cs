using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>On-demand snapshot of retained evidence, never saved or maintained by the simulator.
    /// One consumer pass for every owned shop together, then constant-time queries. Discard at the
    /// next open-panel refresh; no midnight work and no work at memory commit.</summary>
    public sealed class RegularsProjection
    {
        public const int RegularMinServedVisits = 3;
        public const int RegularMaxDaysSinceVisit = 14;
        private readonly FoodCommerceState food;
        private readonly int day, visits, recentDays;
        private readonly Func<ResidentMeal, ShopMemory, int, int> effectiveAffinity;
        private readonly Dictionary<int, string> ownReferences = new Dictionary<int, string>();
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool built;

        // B0 compatibility seam for a caller-supplied effective-affinity policy.
        public RegularsProjection(FoodCommerceState food, int day, Func<ShopMemory, int, int> effectiveAffinity)
            : this(food, day, (consumer, entry, at) => effectiveAffinity(entry, at), new FoodMemorySettings()) { }

        public RegularsProjection(FoodCommerceState food, int day,
            Func<ResidentMeal, ShopMemory, int, int> effectiveAffinity, FoodMemorySettings settings,
            Func<FoodScope, bool>? currentOwnShop = null)
        {
            this.food = food; this.day = day; this.effectiveAffinity = effectiveAffinity;
            visits = settings.FoodMemoryRegularServedVisits; recentDays = settings.FoodMemoryRegularRecentDays;
            var owned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var scope in food.Shops)
                if (currentOwnShop == null || currentOwnShop(scope))
                { owned[scope.BusinessId] = scope.PlaceId; counts[scope.BusinessId] = 0; }
            foreach (var reference in food.BusinessReferences)
                if (owned.TryGetValue(reference.BusinessId, out var place) && place == reference.PlaceId)
                    ownReferences.Add(reference.Slot, reference.BusinessId);
        }

        public static bool IsRegular(ShopMemory entry, int day, int effectiveAffinity, FoodMemorySettings settings)
            => entry.Served >= settings.FoodMemoryRegularServedVisits && entry.Day <= day
                && day - entry.Day <= settings.FoodMemoryRegularRecentDays && effectiveAffinity > 0;

        public int CountOwnShop(string businessId)
        {
            if (!counts.TryGetValue(businessId, out int cached)) return 0;
            if (built) return cached;
            if (!built)
            {
                var seen = new HashSet<string>(food.Residents.Count, StringComparer.Ordinal);
                foreach (var consumer in food.Residents)
                {
                    if (!seen.Add(consumer.ResidentId)) continue;
                    foreach (var entry in consumer.Memory.Entries)
                        if (entry.Served >= visits && entry.Day <= day && day - entry.Day <= recentDays
                            && ownReferences.TryGetValue(entry.BusinessRef, out var id)
                            && effectiveAffinity(consumer, entry, day) > 0) counts[id]++;
                }
                built = true;
            }
            return counts[businessId];
        }

        /// <summary>Null for any shop outside the owned snapshot. No free rival feedback.</summary>
        public string? OwnShopText(string businessId) => counts.ContainsKey(businessId)
            ? "Regulars: " + CountOwnShop(businessId).ToString(CultureInfo.InvariantCulture) : null;
    }
}
