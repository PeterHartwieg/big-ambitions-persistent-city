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
    // Initialization is the ModAPI lifetime scope, loaded before MainMenu/City. This passive
    // guard exists before a saved city can generate demand, including async population startup.
    [ModEntryOnInitializationLoad]
    public sealed class FoodAuthorityBootstrap : IModBigAmbitions
    {
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context) { NativeFoodOwnership.Install(); return Task.CompletedTask; }
        public Task OnUnloadAsync() { NativeFoodOwnership.Uninstall(); return Task.CompletedTask; }
    }

    internal static class NativeFoodOwnership
    {
        private const string PatchId = "persistentcity.food-authority";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static readonly FieldInfo Registration = AccessTools.Field(typeof(BusinessSimulator), "buildingRegistration");
        private static readonly FieldInfo ProcessingHour = AccessTools.Field(typeof(BusinessSimulator), "currentHour");
        private static readonly Dictionary<string, FoodAuthority> records = new Dictionary<string, FoodAuthority>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BuildingRegistration> observed = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private static readonly Dictionary<Address, BuildingRegistration> addresses = new Dictionary<Address, BuildingRegistration>();
        private static object? save;
        private static string? payload;
        private static NativeFoodMarket? market;
        private static bool installed, boundary, awaitingPopulation;
        private static long lastBoundaryHour = -1;
        internal static string Error { get; private set; } = "";
        internal static long Hour => SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
        internal static long SuppressedBatches { get; private set; }
        internal static long SuppressedDraws { get; private set; }

        internal static void Install()
        {
            if (installed) return;
            try
            {
                patches.Patch(AccessTools.Method(typeof(GameManager), "RunMainGameTick"),
                    postfix: new HarmonyMethod(typeof(NativeFoodOwnership), nameof(AfterNativeTick)));
                Patch(typeof(RetailBusinessSimulator), "ProcessAllCustomersFromThisHour", nameof(HourlyCustomers));
                Patch(typeof(RetailBusinessSimulator), "ProcessCustomer", nameof(CustomerSale));
                Patch(typeof(CustomerEntriesHelper), "GetEntriesByAddress", nameof(CustomerDraw));
                Patch(typeof(CustomerEntriesHelper), "UpdateCustomerEntriesForPlayerBusiness", nameof(GenerateCustomers));
                Patch(typeof(BuildingRegistration), "Reset", nameof(ResetRegistration));
                installed = true;
            }
            catch { patches.UnpatchAll(PatchId); throw; }
        }
        private static void Patch(Type type, string method, string prefix) => patches.Patch(AccessTools.Method(type, method),
            prefix: new HarmonyMethod(typeof(NativeFoodOwnership), prefix) { priority = Priority.First });
        internal static void Uninstall()
        {
            patches.UnpatchAll(PatchId); installed = false; market = null; save = null;
            records.Clear(); observed.Clear(); addresses.Clear(); payload = null; Error = ""; awaitingPopulation = false;
        }
        internal static void EnsureSave()
        {
            var current = SaveGameManager.Current;
            if (current == null || current.modData == null || ReferenceEquals(save, current)) return;
            save = current; market = null; records.Clear(); observed.Clear(); addresses.Clear(); Error = "";
            if (current.BuildingRegistrations != null)
                foreach (var reg in current.BuildingRegistrations) addresses[reg.Address] = reg;
            current.modData.TryGetValue(FoodAuthorityStore.Key, out payload);
            awaitingPopulation = payload == null && current.modData.ContainsKey(SaveStore.StateKey);
            try
            {
                if (current.modData.TryGetValue(SaveStore.StateKey, out var populationPayload))
                    SaveCodec.RequireExecutableCapabilities(populationPayload);
                foreach (var row in FoodAuthorityStore.Read(payload))
                {
                    if (row.RequestedHour > Hour || row.FromHour > Hour || row.UntilHour > Hour)
                        throw new InvalidOperationException("Food authority is ahead of the loaded native clock; original payload retained.");
                    records.Add(row.PlaceId, row);
                }
                if (!current.modData.ContainsKey(SaveStore.StateKey) && (records.Values.Any(r => r.FromHour >= 0)
                    || current.modData.Keys.Any(FoodSaleReceipts.IsReceiptKey)))
                    throw new InvalidOperationException("Saved food authority or receipts exist without the population payload; original data retained, fresh identities and wallets refused.");
            }
            catch (Exception e)
            {
                // Never overwrite an unreadable manifest or quietly run a second sales engine.
                Error = "Food ownership suspended: " + e.Message;
                Debug.LogError("[Persistent City] " + Error);
            }
        }
        internal static void Attach(NativeFoodMarket value)
        {
            Install(); EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (market != null && !ReferenceEquals(market, value)) throw new InvalidOperationException("Food authority already attached.");
            if (awaitingPopulation) throw new InvalidOperationException("Saved food authority is awaiting population validation.");
            if (payload == null) Persist(); // Supported empty-scope cities also carry the early-load contract.
            market = value;
            // A loaded native hour may already contain native sales. Pending onboarding must
            // wait for a later hour even when its original observation predates the reload.
            lastBoundaryHour = Hour;
        }
        internal static void ValidatePopulation(FoodCommerceState state)
        {
            EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
            if (!awaitingPopulation)
            {
                foreach (var scope in state.Shops)
                {
                    if (!records.TryGetValue(scope.PlaceId, out var row) || !MatchesScope(row, scope))
                        RefusePopulation("Saved food scope differs from its authority manifest");
                }
                foreach (var row in records.Values.Where(r => r.FromHour >= 0))
                    if (!state.Shops.Any(s => s.PlaceId == row.PlaceId && MatchesScope(row, s)))
                        RefusePopulation("Saved food authority has no corresponding population scope");
                return;
            }
            if (state.Shops.Count != 0)
            {
                Error = "Saved population owns food businesses but its authority manifest is missing; original payload retained and native demand suspended.";
                throw new InvalidOperationException(Error);
            }
            // This method runs only after the complete population payload passed strict decode,
            // identity and clock checks. No owned scopes means no mod checkout could have run.
            // Release the provisional guard once; native helper generates only current/future hours.
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
        internal static FoodAuthority Observe(BuildingRegistration reg, bool eligible)
        {
            EnsureSave();
            if (Error.Length > 0) throw new InvalidOperationException(Error);
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
            if (records.TryGetValue(place, out var row) && row.Observe(row.Signature, false, Hour)) Persist();
            if (observed.TryGetValue(place, out var reg)) addresses.Remove(reg.Address);
            observed.Remove(place);
        }
        // Feedback must not call EnsureSave/Find: both maintain authority state.
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
            // A live menu/owner/type mutation can precede our next bounded observation. Old
            // queued work still belongs to its original authority until drained, even if the
            // registration fields have already changed. Sales separately require exact identity.
            string place = NativeFoodMarket.PlaceId(reg);
            if (records.TryGetValue(place, out var prior))
            {
                // A changed saved native signature is not permission to replay the protected
                // address/hour before the population can resolve its replacement generation.
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
            // RunHourly force-drains 23:00 before midnight. This also covers a delayed old-day
            // batch defensively; never substitute the global hour for simulator.currentHour.
            return (SaveGameManager.Current.Day - (hour > SaveGameManager.Current.Hour ? 1L : 0L)) * 24 + hour;
        }
        private static bool HourlyCustomers(RetailBusinessSimulator __instance)
        {
            if (!Suppress((BuildingRegistration)Registration.GetValue(__instance)!, BatchHour(__instance))) return true;
            SuppressedBatches++; return false; // Native restock/tasks/settlement still run.
        }
        private static bool CustomerSale(RetailBusinessSimulator __instance)
        {
            var reg = (BuildingRegistration)Registration.GetValue(__instance)!;
            return NativeFoodMarket.IsBuying(reg) || !Suppress(reg, BatchHour(__instance));
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
            if (boundary || market == null || Error.Length > 0 || Hour <= lastBoundaryHour) return;
            // Observation must remain outside the lock: a replacement drains work under its
            // previous identity before Observe changes the generation. Stable businesses leave
            // the native ending-hour batch available for its normal distributed processing.
            market.RefreshKnownOwnership();
            lastBoundaryHour = Hour;
            if (records.Values.Any(row => Hour > row.RequestedHour && (row.Desired != row.Managed || row.Suspended)
                && (!row.Desired || !HasNativeCustomers(row))))
            {
                boundary = true;
                try
                {
                    // Native RunMainGameTick queues the ENDING hour before it increments Hour/Day.
                    // Here the clock has advanced; drain those ending batches under their old owner,
                    // then transfer only the current/future hour. At midnight native RunHourly already
                    // force-drained 23:00 before daily settlement. Never delete orders or baskets.
                    BusinessSimulatorHelper.Work.ForceCompleteAllWork();
                    // Drained native callbacks may have changed eligibility or introduced customers.
                    market.RefreshKnownOwnership();
                    bool changed = false;
                    foreach (var row in records.Values.ToArray())
                    {
                        observed.TryGetValue(row.PlaceId, out var reg);
                        if (!row.Boundary(Hour, true, HasNativeCustomers(row), market != null)) continue;
                        changed = true;
                        if (row.Managed) market!.ActivateScope(row, reg!);
                        // Regenerate only the new native hour and future hours. Native helper keeps
                        // completed entries and does not recreate past hours or touch completed orders.
                        else if (reg != null && reg.RentedByPlayer) CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
                    }
                    if (changed) Persist();
                }
                finally { boundary = false; }
            }
            // New scopes must exist before the first eligible observation. The optional observer
            // isolates its own failures and still runs on boundaries with no authority transfers.
            market?.ObserveCampaignBoundary(Hour);
        }
        private static bool HasNativeCustomers(FoodAuthority row) => observed.TryGetValue(row.PlaceId, out var reg)
            && BuildingManager.Instance != null && ReferenceEquals(BuildingManager.Instance.buildingRegistration, reg)
            && IndoorCustomerSpawner.Customers.Count > 0;
        private static void ResetRegistration(BuildingRegistration __instance)
        {
            EnsureSave();
            // Native constructors call Reset(false) before an address is assigned.
            if (string.IsNullOrWhiteSpace(__instance.StreetName) || Error.Length > 0 || !records.TryGetValue(NativeFoodMarket.PlaceId(__instance), out var row)
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
