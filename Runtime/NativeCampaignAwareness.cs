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
            campaignSource.PublicationFailed = StopCampaignObservation;
            return campaignSource;
        }
        private void StopCampaignObservation(Exception error)
        {
            campaignSource?.DiscardPublications();
            campaignObservationError=error.GetType().Name+": "+error.Message;
            UnityEngine.Debug.LogWarning("[Persistent City] Campaign observation stopped for this session: "+campaignObservationError);
        }
        internal void ObserveCampaignBoundary(long hour)
        {
            if (campaignSettings?.Enabled != true || campaignSource == null || campaignObservationError.Length!=0 || hour <= campaignHour) return;
            campaignHour = hour;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { SampleCampaignBoundary(hour); }
            catch(Exception error)
            {
                StopCampaignObservation(error);
            }
            finally { maximumCampaignBoundaryMilliseconds = Math.Max(maximumCampaignBoundaryMilliseconds,
                (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); }
        }
        private void SampleCampaignBoundary(long hour)
        {
            // Candidate membership and identity mapping are maintained by discovery/ownership.
            // Historical retained scopes do not consume native-read admission.
            int actualCandidates=0;
            foreach (string id in candidateBusinesses)
                if (scopesByBusiness.TryGetValue(id,out var known) && registrations.ContainsKey(known.PlaceId)) actualCandidates++;
            if (!NativeCampaignReader.BoundaryScopeAdmitted(actualCandidates))
            { campaignScopeRefusals++;lastRefusedCampaignScope=actualCandidates;throw new InvalidOperationException("Campaign read-candidate envelope exceeded."); }
            awarenessCampaignReader ??= new NativeCampaignReader();
            bool any = state.Campaigns.Count != 0 || campaignSource!.HasPendingPublication;
            if (!any) foreach (string id in candidateBusinesses)
                if (scopesByBusiness.TryGetValue(id,out var known) && registrations.TryGetValue(known.PlaceId,out var registration)
                    && awarenessCampaignReader.HasCampaignEntries(registration)) { any=true;break; }
            if (!any) return; // Keep the warmed empty-campaign path allocation-free.
            var sources = new List<CampaignBusinessSource>(actualCandidates);
            foreach (string id in candidateBusinesses)
                if (scopesByBusiness.TryGetValue(id,out var scope) && registrations.TryGetValue(scope.PlaceId,out var reg))
                    sources.Add(new CampaignBusinessSource(id,reg));
            var places = new Dictionary<string,string>(state.Shops.Count,StringComparer.Ordinal);
            foreach (var scope in state.Shops) places.Add(scope.BusinessId,scope.PlaceId);
            // Native references live only in this bounded atomic capture, never in a continuation.
            var snapshot = awarenessCampaignReader.ReadBoundary(sources,hour*60,hour);
            campaignReadBusinesses += sources.Count;
            campaignSource!.EnqueuePublication(snapshot,places);
        }
        internal void CompleteCampaignsForSave() => campaignSource?.CompletePublicationsForSave();
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
            pendingPublication = campaignSource?.HasPendingPublication ?? false, publicationUnits = campaignSource?.PublicationUnits ?? 0,
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
