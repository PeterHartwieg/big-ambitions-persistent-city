using System;
using System.Linq;
using HarmonyLib;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>F8b `pc capability status | show &lt;id&gt;` (docs/plans/f8-capability-boundaries.md D7). On request only: it
    /// copies the existing status members into <see cref="CapabilityInputs"/>, builds a fresh registry and prints the
    /// Core report. It installs, attaches, persists and observes nothing, and no frame path calls it.</summary>
    public sealed partial class PopulationRuntime
    {
        private readonly CapabilityReport capabilityReport = new CapabilityReport();
        private RloReadResult? lastRloRead;
        /// <summary>F8d: the running city is inside a transient RLO window (set by the periodic check).</summary>
        private bool rloTransient;

        private void CapabilityCommand(string action, string detail)
        {
            if (action == "status" && detail.Length == 0)
            {
                var inputs = ReadCapabilityInputs();
                Debug.Log("[Persistent City] capability status (read-only)\n" + capabilityReport.Status(CapabilitySnapshot.Build(inputs), Application.version, CapabilitySnapshot.Context(inputs)));
                return;
            }
            // F8e: foreign Harmony owners on every Persistent City target, classed by patch kind (report only).
            if (action == "hooks" && detail.Length == 0)
            {
                try { Debug.Log("[Persistent City] capability hooks (read-only)\n" + HookConflicts.Scan().Text()); }
                catch (Exception error) { Debug.LogWarning("[Persistent City] hook conflict report unavailable: " + error.Message); }
                return;
            }
            // F8f: the transaction mechanism (or unsupported reason) of one scoped business, from the existing scope status.
            if (action == "business" && detail.Length > 0) { BusinessMechanismCommand(detail); return; }
            if (action == "show" && NativeCapabilities.TryParse(detail, out var id))
            { Debug.Log("[Persistent City] capability show (read-only)\n" + capabilityReport.Show(CapabilitySnapshot.Build(ReadCapabilityInputs()), id)); return; }
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc capability status | pc capability hooks | pc capability business <place> | pc capability show <id> ("
                + string.Join(", ", NativeCapabilities.All.Select(d => d.Name)) + ")");
        }

        /// <summary>F8f: maps a place (or business ID) to its scope and reads NativeFoodMarket.ScopeStatus or the vendor
        /// market's serving answer; Core picks the mechanism. Reads only.</summary>
        private void BusinessMechanismCommand(string place)
        {
            var food = simulation?.State.Food;
            if (failed || food == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
            { Debug.LogWarning("[Persistent City] capability business unavailable: no active Persistent City food scope in this save."); return; }
            var scope = food.Shops.FirstOrDefault(s => s.PlaceId == place) ?? food.Shops.FirstOrDefault(s => s.BusinessId == place);
            if (scope == null) { Debug.LogWarning("[Persistent City] No scoped food business at " + place + "."); return; }
            string status;
            if (scope.Channel == FoodScopeChannel.Vehicle)
            {
                var vendors = vendorMarket;
                var row = vendors?.Coordinator.Table.Rows.FirstOrDefault(r => r.BusinessId == scope.BusinessId);
                status = vendors == null ? "vendor market not attached" : vendors.CanServe(scope.BusinessId) ? ""
                    : row != null && row.Reason.Length > 0 ? row.Reason : vendors.Coordinator.Reason.Length > 0 ? vendors.Coordinator.Reason : "vehicle not serviceable now";
            }
            else status = foodMarket?.ScopeStatus(scope.PlaceId) ?? "food market not attached";
            var verdict = TransactionMechanisms.ForBusiness(scope.Channel, scope.Products().ToArray(), status);
            Debug.Log("[Persistent City] capability business (read-only)\n" + TransactionMechanisms.Describe(scope.PlaceId, scope.BusinessId, scope.Channel, verdict));
        }

        /// <summary>Plain copies of today's status members. Harmony owner queries read patch metadata only.</summary>
        private CapabilityInputs ReadCapabilityInputs()
        {
            var save = SaveGameManager.Current;
            bool city = !failed && simulation != null && save != null && ReferenceEquals(saveIdentity, save);
            var market = foodMarket;
            var vendors = vendorMarket;
            var rloRead = lastRloRead;
            var guard = NativeInteriorGuard.Verdict;
            return new CapabilityInputs
            {
                CityActive = city, CityFailed = failed, FailureStatus = failed ? status : "",
                FoodAuthorityInstalled = Harmony.HasAnyPatches(NativeHookInventory.FoodAuthority),
                FoodAuthorityRefusal = NativeFoodContract.AuthorityRefusal, FoodAuthorityError = NativeFoodOwnership.Error,
                FoodMarketAttached = market != null, FoodCheckoutRefusal = market?.CheckoutRefusal,
                FoodPricePrefixInstalled = Harmony.HasAnyPatches(NativeHookInventory.FoodRoutines),
                FoodDynamicActive = market?.DynamicCapabilityActive == true,
                FoodDynamicRefusal = market?.DynamicCapabilityRefusal ?? "",
                StreetFoodLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "FoodTrucks"),
                VendorMarketAttached = vendors != null, VendorApiBound = vendors?.Coordinator.Image != null,
                VendorReason = vendors?.Coordinator.Reason ?? "",
                ClockObserving = NativeTravelPace.ObservationActive, ClockObserveError = NativeTravelPace.LastError,
                ClockCityActive = NativeCityClock.Active, ClockFactor = NativeCityClock.Factor,
                DrivenRidesLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("DrivenRides.Ride", false) != null),
                DrivenRidesCoordinated = NativeCityClock.CoordinatesDrivenRides,
                RloReason = rloRead == null ? DonorReadReason.NotRead : DonorReason(rloRead), RloMessage = rloRead?.Message ?? "", RloTransient = rloTransient,
                ResearchDialogInstalled = ResearchAgencyDialog.Installed, ResearchDialogStatus = ResearchAgencyDialog.Status,
                ResearchBillingInstalled = ResearchBilling.Installed, ResearchBillingStatus = ResearchBilling.Status,
                ResearchMessagesPassed = ResearchMessages.ContractPassed, ResearchMessagesStatus = ResearchMessages.Status,
                ResearchMessagesChecked = ResearchMessages.ContractPassed || ResearchMessages.Status != "Research messages are not checked.",
                PhysicalEnabled = settings.PhysicalEnabled, PhysicalActive = physical != null, PhysicalFailed = physicalFailed,
                InspectorFailed = inspectorFailed,
                InspectorClickInstalled = PatchedBy(NativeHookInventory.InspectorInput, "MouseController", "Run"),
                InspectorScrollInstalled = PatchedBy(NativeHookInventory.InspectorInput, "PedestrianCam", "UpdateInput"),
                InspectorUiBlockInstalled = PatchedBy(NativeHookInventory.InspectorInput, "GraphicRaycaster", "Raycast"),
                InteriorConfigured = NativeInteriorCityView.Configured,
                InteriorGuardAllowed = !guard.Detected, InteriorGuardReason = guard.Reason,
            };
        }

        private static DonorReadReason DonorReason(RloReadResult read) => read.Reason;

        /// <summary>F8e: the once-per-load conflict line. A failed scan is reported and changes nothing.</summary>
        private static string HookConflictActivationLine()
        {
            try { return HookConflicts.Scan().ActivationLine(); }
            catch (Exception error) { return "Persistent City hook conflict scan unavailable (report only): " + error.Message; }
        }

        /// <summary>F8d (D7): the activation Player.log line, once per load. Builds the same on-request snapshot.</summary>
        private string CapabilityActivationLine()
        {
            var registry = CapabilitySnapshot.Build(ReadCapabilityInputs());
            var text = new System.Text.StringBuilder("Persistent City capabilities not active at activation (game ").Append(Application.version).Append("): ");
            int listed = 0;
            foreach (var definition in NativeCapabilities.All)
            {
                var state = registry.State(definition.Id);
                if (state == CapabilityState.Active) continue;
                if (listed++ > 0) text.Append("; ");
                text.Append(definition.Name).Append(' ').Append(CapabilityReport.StateWord(state));
                var reason = registry.Reason(definition.Id);
                if (reason != null) text.Append(": ").Append(reason.Replace('\n', ' '));
            }
            if (listed == 0) text.Append("none");
            return text.Append(". Details: pc capability status.").ToString();
        }

        private static bool PatchedBy(string owner, string type, string method)
        {
            foreach (var patched in Harmony.GetAllPatchedMethods())
            {
                if (patched.Name != method || patched.DeclaringType?.Name != type) continue;
                var info = Harmony.GetPatchInfo(patched);
                if (info != null && info.Owners.Contains(owner)) return true;
            }
            return false;
        }
    }

    public sealed partial class NativeFoodMarket
    {
        /// <summary>F8b: E5b dynamic acquisition is on (read-only copy for the capability report).</summary>
        internal bool DynamicCapabilityActive => DynamicAcquisition;
        /// <summary>F8b: why dynamic acquisition is off once the coordinator left startup; empty while warming.</summary>
        internal string DynamicCapabilityRefusal => DynamicAcquisition || StartupPhase ? "" : DynamicRefusalReason;
    }
}
