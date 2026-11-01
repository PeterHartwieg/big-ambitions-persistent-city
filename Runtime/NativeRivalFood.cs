using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket : IRivalFoodMarket
    {
        private RivalFoodCatalogue? rivalCatalogue;
        private long unavailableRivalRevision;
        internal Action<string>? RivalWarning { private get; set; }
        public string RivalUnavailable { get; private set; } = "";
        public long RivalRevision => RivalUnavailable.Length > 0 ? unavailableRivalRevision : rivalCatalogue?.MetadataRevision ?? 0;
        public void CloseRivals() { rivalCatalogue?.Dispose(); rivalCatalogue=null; }
        private double nextRivalRefresh;
        public long RivalRefreshUnits { get; private set; }
        public long RivalDetailReads { get; private set; }
        public bool NeedsRivalRefresh(double minute) => RivalUnavailable.Length == 0
            && (rivalCatalogue == null || rivalCatalogue.RefreshPending || minute >= nextRivalRefresh);
        public void ContinueRivalRefresh(double minute)
        {
            if (RivalUnavailable.Length > 0) return;
            try
            {
                if (rivalCatalogue == null) rivalCatalogue = new RivalFoodCatalogue();
                if (!rivalCatalogue.RefreshPending) rivalCatalogue.BeginRefresh(controllers);
                RivalRefreshUnits++;
                if (rivalCatalogue.ContinueRefresh(rivalSettings.RivalReadsPerUnit))
                    nextRivalRefresh = minute + rivalSettings.RivalRefreshGameMinutes;
            }
            catch (Exception error)
            {
                // Retire only the optional rival source, invalidating any published index.
                // Held targets then fail their existing live check without debit/substitution.
                unavailableRivalRevision = (rivalCatalogue?.MetadataRevision ?? 0) + 1;
                RivalUnavailable = error.GetType().Name + ": " + error.Message;
                try { rivalCatalogue?.Dispose(); } catch (Exception) { }
                rivalCatalogue = null;
                try { RivalWarning?.Invoke("Rival food source unavailable for this city; player commerce and population continue: " + RivalUnavailable); }
                catch (Exception) { } // A diagnostic sink cannot revoke player/population ownership.
            }
        }
        public IEnumerable<FoodScope> RivalScopes => rivalCatalogue?.MetadataScopes ?? Array.Empty<FoodScope>();
        public bool IsRivalCandidate(FoodScope scope) => rivalCatalogue?.Candidate(scope.BusinessId, scope.PlaceId) == true;
        public bool RivalAtPlace(string place) => rivalCatalogue?.RivalAtPlace(place) == true;
        public FoodAlternativeObservation? ReadRival(string businessId)
        {
            if (rivalCatalogue == null || !rivalCatalogue.TryRivalPlace(businessId, out var place)) return null;
            RivalDetailReads++;
            var selected = rivalCatalogue.ReadSelected(new[] { place }).Businesses;
            if(selected.Count != 1) return null;
            var observation = selected[0];
            return observation.Id == businessId && observation.Kind == FoodAlternativeKind.Rival ? observation : null;
        }
    }
}
