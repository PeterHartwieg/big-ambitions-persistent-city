using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI.Customers.CustomerEntries;
using BAModAPI;
using Buildings.Retail.Simulation;
using Entities;
using HarmonyLib;
using Helpers;
using Helpers.BusinessSimulation;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;

[assembly: RegisterModClass(typeof(PersistentCity.Runtime.FoodAuthorityBootstrap))]

namespace PersistentCity.Runtime
{
    [ModEntryOnInitializationLoad]
    public sealed class FoodAuthorityBootstrap : IModBigAmbitions
    {
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            try { NativeFoodOwnership.Install(); }
            catch (InvalidOperationException error) { context.Logger.Warn(error.Message); }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync() { NativeFoodOwnership.Uninstall(); return Task.CompletedTask; }
    }

    internal static class NativeFoodOwnership
    {
        private const string PatchId = "persistentcity.food-authority";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static FieldInfo Registration = null!, ProcessingHour = null!;
        private static readonly Dictionary<string, FoodAuthority> records = new Dictionary<string, FoodAuthority>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BuildingRegistration> observed = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private static readonly Dictionary<Address, BuildingRegistration> addresses = new Dictionary<Address, BuildingRegistration>();
        private static object? save;
        private static string? payload;
        private static NativeFoodMarket? market;
        private static bool installed, boundary, awaitingPopulation, releaseWithoutMarket;
        private static long lastBoundaryHour = -1;
        internal static string Error { get; private set; } = "";
        internal static FoodAuthorityStandDown? StandDown { get; private set; }
        internal static long Hour => SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
        internal static long SuppressedBatches { get; private set; }
        internal static long SuppressedDraws { get; private set; }

        internal static void Install()
        {
            if (installed) return;
            NativeFoodContract.InstallChecked(NativeFoodContract.Authority, () =>
            {
                Registration = NativeFoodContract.AuthorityField("buildingRegistration");
                ProcessingHour = NativeFoodContract.AuthorityField("currentHour");
                Patch(typeof(GameManager), "RunMainGameTick", nameof(AfterNativeTick), postfix: true);
                Patch(typeof(RetailBusinessSimulator), "ProcessAllCustomersFromThisHour", nameof(HourlyCustomers));
                Patch(typeof(RetailBusinessSimulator), "ProcessCustomer", nameof(CustomerSale));
                Patch(typeof(CustomerEntriesHelper), "GetEntriesByAddress", nameof(CustomerDraw));
                Patch(typeof(CustomerEntriesHelper), "UpdateCustomerEntriesForPlayerBusiness", nameof(GenerateCustomers));
                Patch(typeof(BuildingRegistration), "Reset", nameof(ResetRegistration));
                installed = true;
            }, () => patches.UnpatchAll(PatchId));
        }
        private static void Patch(Type type, string method, string patch, bool postfix = false) => patches.Patch(NativeFoodContract.AuthorityTarget(type, method),
            prefix: postfix ? null : new HarmonyMethod(typeof(NativeFoodOwnership), patch) { priority = Priority.First },
            postfix: postfix ? new HarmonyMethod(typeof(NativeFoodOwnership), patch) : null);
        internal static void Uninstall()
        {
            patches.UnpatchAll(PatchId); installed = false; market = null; save = null; StandDown = null;
            records.Clear(); observed.Clear(); addresses.Clear(); payload = null; Error = ""; awaitingPopulation = false;
        }
        internal static void EnsureSave()
        {
            var current = SaveGameManager.Current;
            if (current == null || current.modData == null || ReferenceEquals(save, current)) return;
            save = current; market = null; records.Clear(); observed.Clear(); addresses.Clear(); Error = ""; StandDown = null;
            lastBoundaryHour = Hour; releaseWithoutMarket = false;
            if (current.BuildingRegistrations != null)
                foreach (var reg in current.BuildingRegistrations) addresses[reg.Address] = reg;
            current.modData.TryGetValue(FoodAuthorityStore.Key, out payload);
            awaitingPopulation = payload == null && current.modData.ContainsKey(SaveStore.StateKey);
            current.modData.TryGetValue(SaveStore.StateKey, out var populationPayload);
            if (SaveCodec.DevelopmentSchemaOf(populationPayload) != null)
            {
                string? manifest = payload;
                StandDown = FoodAuthorityStandDown.FromManifest(() => FoodAuthorityStore.Read(manifest), Hour, out var problem);
                awaitingPopulation = false; releaseWithoutMarket = false;
                if (problem != null) Debug.LogWarning("[Persistent City] " + problem + "; refused development city, every player business is native.");
                return;
            }
            try
            {
                if (populationPayload != null) SaveCodec.RequireExecutableCapabilities(populationPayload);
                foreach (var row in FoodAuthorityStore.Read(payload))
                {
                    if (row.RequestedHour > Hour || row.FromHour > Hour || row.UntilHour > Hour)
                        throw new InvalidOperationException("Food authority is ahead of the loaded native clock; original payload retained.");
                    records.Add(row.PlaceId, row);
                }
                releaseWithoutMarket = records.Values.Any(row => row.Managed && !row.Desired);
                if (releaseWithoutMarket) ObserveReleaseRegistrations();
                if (!current.modData.ContainsKey(SaveStore.StateKey) && (records.Values.Any(r => r.FromHour >= 0)
                    || current.modData.Keys.Any(FoodSaleReceipts.IsReceiptKey)))
                    throw new InvalidOperationException("Saved food authority or receipts exist without the population payload; original data retained, fresh identities and wallets refused.");
            }
            catch (Exception e)
            {
                Error = "Food ownership suspended: " + e.Message;
                Debug.LogError("[Persistent City] " + Error);
            }
        }
        internal static void Attach(NativeFoodMarket value)
        {
            Install(); EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (StandDown != null) throw new InvalidOperationException(SaveCodec.DevelopmentSchemaMessage);
            if (market != null && !ReferenceEquals(market, value)) throw new InvalidOperationException("Food authority already attached.");
            if (awaitingPopulation) throw new InvalidOperationException("Saved food authority is awaiting population validation.");
            if (payload == null) Persist();
            market = value;
            lastBoundaryHour = Hour;
        }
        internal static void ValidatePopulation(FoodCommerceState state)
        {
            EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (StandDown != null) throw new InvalidOperationException(SaveCodec.DevelopmentSchemaMessage);
            if (!awaitingPopulation)
            {
                FoodMarkets.ValidateShops(state, records.Values, MatchesScope, RefusePopulation);
                return;
            }
            if (state.Shops.Any(s => s.Channel == FoodScopeChannel.Shop))
            {
                Error = "Saved population owns food businesses but its authority manifest is missing; original payload retained and native demand suspended.";
                throw new InvalidOperationException(Error);
            }
            awaitingPopulation = false; Persist();
            foreach (var reg in SaveGameManager.Current.BuildingRegistrations)
                if (reg.RentedByPlayer) CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
        }
        private static bool MatchesScope(FoodAuthority row, FoodScope scope)
        {
            if (row.FromHour < 0) return false;
            if (!row.PlaceId.EndsWith(":shop", StringComparison.Ordinal) || row.PlaceId.Length <= 13
                || !scope.BusinessId.StartsWith("business:" + row.PlaceId.Substring(8, row.PlaceId.Length - 13) + ":", StringComparison.Ordinal)) return false;
            if (scope.BusinessId == row.BusinessId) return true;
            if (row.Managed && !row.Suspended) return false;
            int separator = scope.BusinessId.LastIndexOf(":generation:", StringComparison.Ordinal);
            return separator >= 0 && int.TryParse(scope.BusinessId.Substring(separator + 12),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int generation)
                && generation >= 1 && generation < row.Generation;
        }
        private static void RefusePopulation(string reason)
        {
            Error = reason + "; original payloads retained and native demand suspended.";
            throw new InvalidOperationException(Error);
        }
        internal static void Detach(NativeFoodMarket value) { if (ReferenceEquals(market, value)) market = null; }
        internal static void ReleaseAfterAccountingFailure()
        {
            EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (StandDown != null) return;
            bool changed = false;
            foreach (var row in records.Values)
                if (row.Managed || row.Desired) changed |= row.Observe(row.Signature, false, Hour);
            releaseWithoutMarket = records.Values.Any(row => row.Managed && !row.Desired);
            if (releaseWithoutMarket) ObserveReleaseRegistrations();
            if (changed) Persist();
        }
        private static void ObserveReleaseRegistrations()
        {
            foreach (var reg in SaveGameManager.Current.BuildingRegistrations)
            {
                string place = NativeFoodMarket.PlaceId(reg);
                if (records.TryGetValue(place, out var row) && row.Managed && !row.Desired) observed[place] = reg;
            }
        }
        internal static FoodAuthority Observe(BuildingRegistration reg, bool eligible)
        {
            EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (StandDown != null) throw new InvalidOperationException(SaveCodec.DevelopmentSchemaMessage);
            string place = NativeFoodMarket.PlaceId(reg);
            if (!records.TryGetValue(place, out var row)) records.Add(place, row = new FoodAuthority { PlaceId = place });
            bool replacement = observed.TryGetValue(place, out var previous) && !ReferenceEquals(previous, reg);
            if (replacement || row.Signature.Length > 0 && row.Signature != FoodBusinessCatalogue.BusinessIdentity(reg)) DrainOldWork();
            observed[place] = reg; addresses[reg.Address] = reg;
            if (row.Observe(FoodBusinessCatalogue.BusinessIdentity(reg), eligible, Hour, replacement)) Persist();
            return row;
        }
        internal static void Retire(string place)
        {
            if (StandDown != null) return;
            if (records.TryGetValue(place, out var row) && row.Observe(row.Signature, false, Hour)) Persist();
            if (observed.TryGetValue(place, out var reg)) addresses.Remove(reg.Address);
            observed.Remove(place);
        }
        internal static bool ObservedOwnGeneration(BuildingRegistration reg, string businessId)
            => ReferenceEquals(save, SaveGameManager.Current) && Error.Length == 0
                && records.TryGetValue(NativeFoodMarket.PlaceId(reg), out var row)
                && row.BusinessId == businessId && row.Signature == FoodBusinessCatalogue.BusinessIdentity(reg)
                && observed.TryGetValue(row.PlaceId, out var previous) && ReferenceEquals(previous, reg);

        internal static FoodAuthority? Find(BuildingRegistration reg)
        {
            EnsureSave();
            if (!records.TryGetValue(NativeFoodMarket.PlaceId(reg), out var row)
                || row.Signature != FoodBusinessCatalogue.BusinessIdentity(reg)) return null;
            if (observed.TryGetValue(row.PlaceId, out var previous) && !ReferenceEquals(previous, reg)) return null;
            observed[row.PlaceId] = reg; return row;
        }
        private static void Persist()
        {
            var data = SaveGameManager.Current.modData;
            data.TryGetValue(FoodAuthorityStore.Key, out var current);
            if (current != payload) throw new InvalidOperationException("Food authority payload changed outside its owner; original retained.");
            payload = FoodAuthorityStore.Write(records.Values.ToArray()); data[FoodAuthorityStore.Key] = payload;
        }
        private static bool Suppress(BuildingRegistration reg, long hour)
        {
            EnsureSave();
            if (StandDown != null) return StandDown.Suppresses(NativeFoodMarket.PlaceId(reg), hour);
            string place = NativeFoodMarket.PlaceId(reg);
            if (records.TryGetValue(place, out var prior))
            {
                if (!observed.ContainsKey(place)) observed[place] = reg;
                if (prior.OwnsHour(hour)) return true;
            }
            if (!reg.RentedByPlayer) return false;
            return Error.Length > 0 || awaitingPopulation || Find(reg)?.OwnsHour(hour) == true;
        }
        private static long BatchHour(BusinessSimulator simulator)
        {
            int hour = (int)ProcessingHour.GetValue(simulator)!;
            if (hour < 0 || hour > 23) throw new InvalidOperationException("Native accounting hour is invalid.");
            return (SaveGameManager.Current.Day - (hour > SaveGameManager.Current.Hour ? 1L : 0L)) * 24 + hour;
        }
        private static bool HourlyCustomers(RetailBusinessSimulator __instance)
        {
            if (!Suppress((BuildingRegistration)Registration.GetValue(__instance)!, BatchHour(__instance))) return true;
            SuppressedBatches++; return false;
        }
        private static bool CustomerSale(RetailBusinessSimulator __instance)
        {
            var reg = (BuildingRegistration)Registration.GetValue(__instance)!;
            return NativeFoodMarket.IsBuying(reg) || NativeSaleProbe.IsProbing(reg) || !Suppress(reg, BatchHour(__instance));
        }
        private static bool GenerateCustomers(BuildingRegistration registration) => !Suppress(registration, Hour);
        private static bool CustomerDraw(Address address, ref List<CustomerEntry> __result)
        {
            EnsureSave();
            if (!addresses.TryGetValue(address, out var reg) || !Suppress(reg, Hour)) return true;
            SuppressedDraws++; __result = new List<CustomerEntry>(); return false;
        }
        private static void AfterNativeTick()
        {
            EnsureSave();
            if (StandDown != null) { if (!boundary && StandDown.HandBack(Hour)) HandBackToNative(StandDown); return; }
            if (boundary || Error.Length > 0 || Hour <= lastBoundaryHour
                || market == null && !releaseWithoutMarket) return;
            market?.RefreshKnownOwnership();
            lastBoundaryHour = Hour;
            if (records.Values.Any(row => Hour > row.RequestedHour && (row.Desired != row.Managed || row.Suspended)
                && (!row.Desired || !HasNativeCustomers(row))))
            {
                boundary = true;
                try
                {
                    BusinessSimulatorHelper.Work.ForceCompleteAllWork();
                    market?.RefreshKnownOwnership();
                    bool changed = false;
                    var rows = records.Values.ToArray();
                    Array.Sort(rows, FoodContractBoundary.Order);
                    market?.BeginContractCommit();
                    foreach (var row in rows)
                    {
                        observed.TryGetValue(row.PlaceId, out var reg);
                        bool live = HasNativeCustomers(row);
                        var outcome = market != null ? market.CommitBoundary(row, Hour, live)
                            : row.Boundary(Hour, true, live, false) ? FoodContractBoundary.Outcome.HandedBack : FoodContractBoundary.Outcome.None;
                        if (outcome != FoodContractBoundary.Outcome.Acquired && outcome != FoodContractBoundary.Outcome.HandedBack) continue;
                        changed = true;
                        if (row.Managed) market!.ActivateScope(row, reg!);
                        else if (reg != null && reg.RentedByPlayer) CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
                    }
                    if (changed) Persist();
                    releaseWithoutMarket = records.Values.Any(row => row.Managed && !row.Desired);
                }
                finally { boundary = false; }
            }
            market?.ObserveCampaignBoundary(Hour);
        }
        private static void HandBackToNative(FoodAuthorityStandDown standDown)
        {
            boundary = true;
            try
            {
                BusinessSimulatorHelper.Work.ForceCompleteAllWork();
                foreach (var reg in SaveGameManager.Current.BuildingRegistrations)
                    if (reg.RentedByPlayer && standDown.IsProtected(NativeFoodMarket.PlaceId(reg)))
                        CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
            }
            finally { boundary = false; }
        }
        private static bool HasNativeCustomers(FoodAuthority row) => observed.TryGetValue(row.PlaceId, out var reg)
            && LiveNativeCustomers(reg);
        internal static bool LiveNativeCustomers(BuildingRegistration reg) => BuildingManager.Instance != null
            && ReferenceEquals(BuildingManager.Instance.buildingRegistration, reg) && IndoorCustomerSpawner.Customers.Count > 0;
        internal static FoodAuthority? Row(string place)
            => ReferenceEquals(save, SaveGameManager.Current) && records.TryGetValue(place, out var row) ? row : null;
        private static void ResetRegistration(BuildingRegistration __instance)
        {
            EnsureSave();
            if (string.IsNullOrWhiteSpace(__instance.StreetName) || Error.Length > 0 || StandDown != null || !records.TryGetValue(NativeFoodMarket.PlaceId(__instance), out var row)
                || !observed.TryGetValue(row.PlaceId, out var reg) || !ReferenceEquals(reg, __instance)) return;
            DrainOldWork();
            row.Observe(row.Signature, false, Hour, reset: true); Persist();
        }
        private static void DrainOldWork()
        {
            if (boundary) return;
            boundary = true;
            try { BusinessSimulatorHelper.Work.ForceCompleteAllWork(); }
            finally { boundary = false; }
        }
    }
}
