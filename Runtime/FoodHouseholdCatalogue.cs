using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Copies already-loaded district composition for the selected home districts.
    /// No native class assignment, income, registration creation or market-demand lookup.</summary>
    public static class FoodHouseholdCatalogue
    {
        public static List<FoodDistrictBudgetMix> Read(IReadOnlyCollection<string> homeDistrictIds)
        {
            if (homeDistrictIds == null || homeDistrictIds.Count > 100 || homeDistrictIds.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Select bounded known home districts.");
            var selected = new HashSet<string>(homeDistrictIds, StringComparer.Ordinal);
            var result = new List<FoodDistrictBudgetMix>();
            var districts = NeighborhoodHelper.NeighborhoodsData;
            if (districts == null) return result;
            foreach (var district in districts)
                if (district != null && selected.Contains(district.neighbourhood))
                    result.Add(new FoodDistrictBudgetMix { DistrictId = district.neighbourhood,
                        WorkingPercent = district.workingClassPercentage, MiddlePercent = district.middleClassPercentage,
                        UpperPercent = district.upperClassClassPercentage,
                        Evidence = "Loaded native home-district working/middle/upper composition; individual income not observed" });
            FoodHouseholdPolicy.ValidateMixes(result);
            return result.OrderBy(m => m.DistrictId, StringComparer.Ordinal).ToList();
        }
    }
}
