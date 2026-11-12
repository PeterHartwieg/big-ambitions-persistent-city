using Newtonsoft.Json;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private OrdinaryOwnershipIndex? ordinaryOwnershipIndex;
        public object? OrdinaryExchangesDump()
        {
            if (failed || simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)) return null;
            var totals = simulation.FreeOutsideSpending(); var index = ordinaryOwnershipIndex;
            return new { totals.FirstDay, totals.LastDay, units = totals.Units.ToString(), visits = totals.Visits.ToString(),
                places = index?.Places ?? 0, hour = index?.PublishedHour ?? -1, pending = index?.Pending ?? false,
                observationReads = index?.NativeReads ?? 0, completedUnits = index?.CompletedUnits ?? 0,
                lastUnitPlaces = index?.LastUnitPlaces ?? 0, lastUnitMilliseconds = index?.LastUnitMilliseconds ?? 0,
                maximumUnitMilliseconds = index?.MaximumUnitMilliseconds ?? 0 };
        }
        private void OrdinaryExchangeStatus() => Debug.Log("[Persistent City] ordinary exchanges " + JsonConvert.SerializeObject(OrdinaryExchangesDump()));
    }
}
