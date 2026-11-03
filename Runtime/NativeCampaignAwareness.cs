using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        private NativeCampaignReader? awarenessCampaignReader;
        private FoodCampaignSource? campaignSource;
        private FoodCampaignSettings? campaignSettings;
        private long campaignHour;
        private string campaignObservationError = "";
        private long campaignScopeRefusals;
        private int lastRefusedCampaignScope;
        private long campaignReadBusinesses;
        private double maximumCampaignBoundaryMilliseconds;
        internal FoodCampaignSource AttachCampaignAwareness(int seed, FoodCampaignSettings settings)
        {
            campaignSettings = settings;
            foreach(var image in state.Campaigns)
                if(image.ThroughHour>NativeFoodOwnership.Hour) throw new InvalidOperationException("Campaign evidence is ahead of the native saved clock; preserve the source.");
            campaignHour = NativeFoodOwnership.Hour; // never reconstruct the loaded/current native hour
            campaignSource = new FoodCampaignSource(state, seed, settings);
            return campaignSource;
        }
        internal void ObserveCampaignBoundary(long hour)
        {
            if (campaignSettings?.Enabled != true || campaignSource == null || campaignObservationError.Length!=0 || hour <= campaignHour) return;
            campaignHour = hour;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { SampleCampaignBoundary(hour); }
            catch(Exception error)
            {
                campaignObservationError=error.GetType().Name+": "+error.Message;
                UnityEngine.Debug.LogWarning("[Persistent City] Campaign observation stopped for this session: "+campaignObservationError);
            }
            finally { maximumCampaignBoundaryMilliseconds = Math.Max(maximumCampaignBoundaryMilliseconds,
                (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); }
        }
        private void SampleCampaignBoundary(long hour)
        {
            // This is called ONLY by the existing accounting-hour boundary hook. With no campaigns,
            // count probes allocate nothing and there is no C1 snapshot/publication/index/decision work.
            // Refuse the complete boundary before scanning/reading any registrations. Splitting live
            // reads over frames would mix UI/native writes and invent evidence for an earlier hour.
            // Retain old history, add no bits, and expose the refusal; never silently sample a prefix.
            if (!NativeCampaignReader.BoundaryScopeAdmitted(state.Shops.Count))
            { campaignScopeRefusals++; lastRefusedCampaignScope = state.Shops.Count; return; }
            awarenessCampaignReader ??= new NativeCampaignReader();
            bool any = state.Campaigns.Count != 0;
            if (!any) foreach (var scope in state.Shops)
                if (candidateBusinesses.Contains(scope.BusinessId) && registrations.TryGetValue(scope.PlaceId,out var reg)
                    && awarenessCampaignReader.HasCampaignEntries(reg)) { any = true; break; }
            if (!any) return;
            var sources = new List<CampaignBusinessSource>();
            var places = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var scope in state.Shops)
            {
                places.Add(scope.BusinessId, scope.PlaceId); // retained identity, independent of today's candidacy
                if (!candidateBusinesses.Contains(scope.BusinessId) || !registrations.TryGetValue(scope.PlaceId, out var reg)) continue;
                if (sources.Count == 1024) throw new InvalidOperationException("Oversized campaign observation scope.");
                sources.Add(new CampaignBusinessSource(scope.BusinessId, reg));
            }
            if (sources.Count == 0)
            {
                campaignSource!.Publish(new CampaignSnapshot(hour * 60, hour, Array.Empty<BusinessCampaignSnapshot>()), places);
                return;
            }
            // Exactly one admitted unit, never a drain loop or an asynchronous native reference.
            var snapshot = awarenessCampaignReader.ReadBoundary(sources, hour * 60, hour);
            campaignReadBusinesses += sources.Count;
            campaignSource!.Publish(snapshot, places);
        }
        internal object CampaignAwarenessDump()
        {
            var references=state.BusinessReferences.ToDictionary(r=>r.Slot,r=>r.BusinessId);
            return new {
            images = state.Campaigns.Select(image => new { image.BusinessRef, businessId=references[image.BusinessRef], image.ThroughHour, wire = image.Encode() }).ToArray(),
            verifiedTypes = CampaignTypeGroups.Reviewed.Select(type => new { type.Code, type.Name,
                audience = "All residents", nativeTarget = "Not used" }).ToArray(),
            indexedEntries = campaignSource?.IndexedEntries ?? 0, publications = campaignSource?.Publications ?? 0,
            exposuresAdmitted = campaignSource?.Admitted ?? 0, exposuresRefused = campaignSource?.Refused ?? 0,
            observationError = campaignObservationError,
            atomicBusinessLimit = NativeCampaignReader.MaximumBoundaryBusinesses,
            scopeRefusals = campaignScopeRefusals, lastRefusedScope = lastRefusedCampaignScope,
            businessesRead = campaignReadBusinesses, maximumBoundaryMilliseconds = maximumCampaignBoundaryMilliseconds,
            lastObservedHour = campaignHour, enabled = campaignSettings?.Enabled == true,
            evidence = "Observed enabled types only; Active verified campaigns soften distance preference for every resident; no native writes."
            };
        }
    }
    public sealed partial class PopulationRuntime
    {
        public object? CampaignAwareness() => foodMarket?.CampaignAwarenessDump();
    }
}
