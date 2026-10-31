using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket : IFoodAwarenessRevision
    {
        private int discoveryCursor;
        public long AwarenessRevision { get; private set; }
        private readonly Dictionary<string,FoodScope> scopesByBusiness = new Dictionary<string,FoodScope>(StringComparer.Ordinal);
        private void Candidate(string id,bool admit)
        {
            bool changed=admit ? candidateBusinesses.Add(id) : candidateBusinesses.Remove(id);
            if(changed) AwarenessRevision++;
        }
        private readonly CityBuildingController[] controllers;
        // Escaped place IDs are memoized until a controller registration/address changes.
        private readonly BuildingRegistration?[] discoveryRegistrations;
        private readonly string?[] discoveryStreets;
        private readonly int[] discoveryNumbers;
        private readonly string?[] discoveryPlaces;
        private readonly HashSet<string> candidateBusinesses = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,FoodScope> scopesByPlace = new Dictionary<string,FoodScope>(StringComparer.Ordinal);
        private List<FoodScope>? scopeList;
        private int indexedScopeCount=-1;
        private bool captureBatch;
        private double captureMinute;
        public long CaptureMetadataPasses { get; private set; }
        private sealed class CaptureBatch : IDisposable
        {
            private NativeFoodMarket? owner;
            internal CaptureBatch(NativeFoodMarket owner) {this.owner=owner;}
            public void Dispose() {if(owner!=null) {owner.captureBatch=false;owner=null;}}
        }
        public IDisposable BeginCapture(double minute)
        {
            if(captureBatch) throw new InvalidOperationException("Food capture batches cannot nest.");
            if(!ReferenceEquals(save,SaveGameManager.Current)) throw new InvalidOperationException("Food market save changed.");
            // Discovery/ownership publishes changes through the existing 64-controller ring.
            // Captures read only the bounded sampled identities; never scan controllers per meal.
            CaptureMetadataPasses++;
            captureMinute=minute;captureBatch=true;
            return new CaptureBatch(this);
        }
        public bool IsCandidate(FoodScope scope)
        {
            if(!candidateBusinesses.Contains(scope.BusinessId) || !shopControllers.TryGetValue(scope.PlaceId,out var controller)) return false;
            var reg=controller.buildingRegistration;
            return reg!=null && registrations.TryGetValue(scope.PlaceId,out var observed) && ReferenceEquals(reg,observed)
                && Supported(reg) && GamePlaces.BuildingAccessible(controller)
                && NativeFoodOwnership.Find(reg)?.BusinessId==scope.BusinessId && NativeFoodOwnership.Find(reg)?.CanServe==true;
        }
        private void RefreshReadMetadata(double minute)
        {
            if(!ReferenceEquals(save,SaveGameManager.Current)) throw new InvalidOperationException("Food market save changed.");
            if(captureBatch) return;
            long observationMinute=(long)(SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60.0+SaveGameManager.Current.Minute);
            if(lastOwnershipRefresh!=observationMinute) {lastOwnershipRefresh=observationMinute;RefreshKnownOwnership();}
            RefreshRoutineHours(minute,true);
        }
        public FoodBusiness? CaptureBusiness(string businessId,double minute)
        {
            if(!captureBatch)
            {
                using(BeginCapture(minute)) return CaptureBusiness(businessId,minute);
            }
            if(minute!=captureMinute) throw new InvalidOperationException("Food capture minute changed inside its batch.");
            scopesByBusiness.TryGetValue(businessId,out var scope);
            // A persisted capture must match a cold read of the saved native world. A per-minute
            // cache alone cannot detect a player's delivery/stock edit earlier in this minute.
            return scope==null ? null : ReadSelected(minute,new[] {scope.PlaceId},capture:true).SingleOrDefault(b=>b.Id==businessId);
        }
        // The controller ring visits every address, never just the first N eligible shops.
        // Native city buildings are static; business registration/menu/ownership are live.
        // Called every frame. The 64-slot slice keeps the first observation of a newly eligible business
        // in the frame it changes, which fixes FoodAuthority.RequestedHour and therefore the acquisition hour.
        // Place IDs are memoized per slot, so a slice over unchanged addresses builds no strings.
        public void RefreshDiscovery() => DiscoverShops(64);
        /// <summary>Escaped place IDs discovery has built (diagnostic); unchanged addresses reuse theirs.</summary>
        internal long DiscoveryPlaceIdBuilds { get; private set; }
        private void DiscoverShops(int budget)
        {
            for (int count = 0; count < budget && controllers.Length > 0; count++)
            {
                int slot = discoveryCursor++ % controllers.Length;
                var controller = controllers[slot];
                if (discoveryCursor == controllers.Length) discoveryCursor = 0;
                var reg = controller.buildingRegistration;
                string? previous=discoveryPlaces[slot];
                if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName))
                {if(previous!=null) WithdrawAddress(previous);continue;}
                string place = DiscoveryPlaceId(slot, reg);
                if(previous!=null && previous!=place) WithdrawAddress(previous);
                if (!routinePlaces.ContainsKey(place)) {WithdrawAddress(place);continue;}
                if (!reg.RentedByPlayer && !registrations.ContainsKey(place)) continue;
                registrations[place] = reg; shopControllers[place] = controller;
                var authority = NativeFoodOwnership.Observe(reg, Supported(reg) && GamePlaces.BuildingAccessible(controller));
                if (authority.CanServe) { ActivateScope(authority, reg); Candidate(authority.BusinessId,true); }
                else Candidate(authority.BusinessId,false);
            }
        }
        private void WithdrawAddress(string place)
        {
            if(registrations.TryGetValue(place,out var old))
            {
                var authority=NativeFoodOwnership.Find(old);
                if(authority!=null) Candidate(authority.BusinessId,false);
                NativeFoodOwnership.Retire(place);
            }
            registrations.Remove(place);shopControllers.Remove(place);decisionSupply.Remove(place);
        }
        private string DiscoveryPlaceId(int slot, BuildingRegistration reg)
        {
            string? place = discoveryPlaces[slot];
            if (place != null && ReferenceEquals(discoveryRegistrations[slot], reg)
                && ReferenceEquals(discoveryStreets[slot], reg.StreetName) && discoveryNumbers[slot] == reg.StreetNumber) return place;
            place = PlaceId(reg); DiscoveryPlaceIdBuilds++;
            discoveryRegistrations[slot] = reg; discoveryStreets[slot] = reg.StreetName;
            discoveryNumbers[slot] = reg.StreetNumber; discoveryPlaces[slot] = place;
            return place;
        }
        internal void RefreshKnownOwnership()
        {
            foreach (var pair in shopControllers)
            {
                decisionSupply.Remove(pair.Key);
                var reg = pair.Value.buildingRegistration;
                if (reg == null)
                {
                    if(scopesByPlace.TryGetValue(pair.Key,out var old)) Candidate(old.BusinessId,false);
                    NativeFoodOwnership.Retire(pair.Key);registrations.Remove(pair.Key);continue;
                }
                registrations[pair.Key] = reg;
                var authority = NativeFoodOwnership.Observe(reg, Supported(reg) && GamePlaces.BuildingAccessible(pair.Value));
                if (authority.CanServe) { ActivateScope(authority, reg); Candidate(authority.BusinessId,true); }
                else Candidate(authority.BusinessId,false);
            }
        }
        internal void ActivateScope(FoodAuthority authority, BuildingRegistration reg)
        {
            if (!authority.CanServe || !Supported(reg)) {Candidate(authority.BusinessId,false);return;}
            // This adapter owns the live list; scopes persist in place when a business/menu changes.
            if(!ReferenceEquals(scopeList,state.Shops) || indexedScopeCount!=state.Shops.Count)
            {
                scopesByPlace.Clear();scopesByBusiness.Clear();foreach(var entry in state.Shops) {scopesByPlace.Add(entry.PlaceId,entry);scopesByBusiness[entry.BusinessId]=entry;}
                scopeList=state.Shops;indexedScopeCount=state.Shops.Count;
            }
            if(!scopesByPlace.TryGetValue(authority.PlaceId,out var scope))
            {
                scope=new FoodScope {PlaceId=authority.PlaceId};state.Shops.Add(scope);
                scopesByPlace.Add(scope.PlaceId,scope);indexedScopeCount=state.Shops.Count;
            }
            var menu=reg.cachedAvailableProducts;
            bool same=scope.BusinessId==authority.BusinessId && menu.Count==1+scope.AdditionalProductIds.Count;
            if(same) foreach(var product in menu) if(!scope.ContainsProduct(product)) {same=false;break;}
            if(same)
            {scopesByBusiness[scope.BusinessId]=scope;Candidate(authority.BusinessId,true);return;}
            var products = menu.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (scope.BusinessId != authority.BusinessId)
            {
                Candidate(scope.BusinessId,false);scopesByBusiness.Remove(scope.BusinessId);
                scope.NextServiceAt = EarliestMinute;
            }
            AwarenessRevision++;
            scope.BusinessId = authority.BusinessId; scopesByBusiness[scope.BusinessId]=scope; scope.ProductId = products[0]; scope.AdditionalProductIds = products.Skip(1).ToList();
            // AfterNativeTick transfers authority after its last refresh; publish acquisition immediately.
            Candidate(authority.BusinessId,true);
        }
    }
}
