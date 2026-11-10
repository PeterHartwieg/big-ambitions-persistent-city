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
        /// <summary>F8d: the last periodic, save or activation verdict was Ready but a different catalogue.</summary>
        private bool rloDiffers;

        private void CapabilityCommand(string action, string detail)
        {
            if (action == "status" && detail.Length == 0)
            {
                try
                {
                    var inputs = ReadCapabilityInputs();
                    Debug.Log("[Persistent City] capability status (read-only)\n" + capabilityReport.Status(CapabilitySnapshot.Build(inputs), Application.version, CapabilitySnapshot.Context(inputs)));
                }
                catch (Exception error) { Debug.LogWarning("[Persistent City] capability status unavailable: " + error.Message); }
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
            {
                try { Debug.Log("[Persistent City] capability show (read-only)\n" + capabilityReport.Show(CapabilitySnapshot.Build(ReadCapabilityInputs()), id)); }
                catch (Exception error) { Debug.LogWarning("[Persistent City] capability show unavailable: " + error.Message); }
                return;
            }
            Debug.LogWarning("[Persistent City] Unknown command. Use: pc capability status | pc capability hooks | pc capability business <place> | pc capability show <id> ("
                + string.Join(", ", NativeCapabilities.All.Select(d => d.Name)) + ")");
        }

        /// <summary>F8f: maps a place (or business ID) to its scope and reads the market's read-only scope verdict
        /// (ReadOnlyScopeStatus, not ScopeStatus, which can refuse contracts and update observations) or the vendor market's
        /// serving answer; Core picks the mechanism. Reads only.</summary>
        private void BusinessMechanismCommand(string place)
        {
            var food = simulation?.View.Food; // the read-only view (OwnershipBoundaryTests)
            if (failed || food == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
            { Debug.LogWarning("[Persistent City] capability business unavailable: no active Persistent City food scope in this save."); return; }
            var scope = food?.ScopeList.FirstOrDefault(s => s.PlaceId == place) ?? food?.ScopeList.FirstOrDefault(s => s.BusinessId == place);
            if (scope == null) { Debug.LogWarning("[Persistent City] No scoped food business at " + place + "."); return; }
            string status;
            if (scope.Channel == FoodScopeChannel.Vehicle)
            {
                var vendors = vendorMarket;
                var row = vendors?.Coordinator.Table.Rows.FirstOrDefault(r => r.BusinessId == scope.BusinessId);
                status = vendors == null ? "vendor market not attached" : vendors.CanServe(scope.BusinessId) ? ""
                    : row != null && row.Reason.Length > 0 ? row.Reason : vendors.Coordinator.Reason.Length > 0 ? vendors.Coordinator.Reason : "vehicle not serviceable now";
            }
            else status = foodMarket?.ReadOnlyScopeStatus(scope.PlaceId) ?? "food market not attached";
            var verdict = TransactionMechanisms.ForBusiness(scope.Channel, new[] { scope.ProductId }.Concat(scope.ExtraProductIds).ToArray(), status);
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
            var planner = routes;
            var inputs = new CapabilityInputs
            {
                CityActive = city, CityFailed = failed, FailureStatus = failed ? status : "",
                FoodAuthorityRefusal = NativeFoodContract.AuthorityRefusal, FoodAuthorityError = NativeFoodOwnership.Error,
                FoodMarketAttached = market != null, FoodCheckoutRefusal = market?.CheckoutRefusal,
                FoodDynamicActive = market?.DynamicCapabilityActive == true,
                FoodDynamicRefusal = market?.DynamicCapabilityRefusal ?? "",
                ClockObserving = NativeTravelPace.ObservationActive, ClockObserveError = NativeTravelPace.LastError,
                ClockCityActive = NativeCityClock.Active, ClockFactor = NativeCityClock.Factor,

                DrivenRidesCoordinated = NativeCityClock.CoordinatesDrivenRides,
                RloReason = rloRead == null ? DonorReadReason.NotRead : DonorReason(rloRead), RloMessage = rloRead?.Message ?? "", RloTransient = rloTransient,
                RloDiffers = rloDiffers, RoadSpeedsFromRlo = planner?.RoadSpeedsFromRlo == true, RoadSpeedWaiting = planner?.RoadSpeedWaiting,
                RoadSpeedsVerified = planner?.RoadSpeedsVerified == true, RoadSpeedRefusal = planner?.RoadSpeedRefusal,
                ResearchDialogInstalled = ResearchAgencyDialog.Installed, ResearchDialogStatus = ResearchAgencyDialog.Status,
                ResearchBillingInstalled = ResearchBilling.Installed, ResearchBillingStatus = ResearchBilling.Status,
                ResearchMessagesPassed = ResearchMessages.ContractPassed, ResearchMessagesStatus = ResearchMessages.Status,
                ResearchMessagesChecked = ResearchMessages.ContractPassed || ResearchMessages.Status != "Research messages are not checked.",
                PhysicalEnabled = settings.PhysicalEnabled, PhysicalActive = physical != null, PhysicalFailed = physicalFailed,
                InspectorFailed = inspectorFailed,
                InteriorConfigured = NativeInteriorCityView.Configured, InteriorRequested = settings.InteriorCityView == "on",
                InteriorGuardAllowed = !guard.Detected, InteriorGuardReason = guard.Reason,
            };
            // Assemblies, Harmony owners and both owner checks: the shared metadata read the harness times (Astra-r2-6).
            HookConflicts.ReadMetadata(inputs);
            inputs.ReadVendor(inputs.StreetFoodLoaded, vendors?.Coordinator);
            var reservations = RloPhysicalReservations.Last;
            inputs.ReservationsChecked = reservations != null; inputs.ReservationsReady = reservations?.Ready == true;
            inputs.ReservationsComplete = reservations?.Complete == true; inputs.ReservationsError = reservations?.Error ?? "";
            return inputs;
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
            try { return CapabilityActivationText(); }
            catch (Exception error) { return "Persistent City capability line unavailable (report only): " + error.Message; }
        }

        private string CapabilityActivationText()
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

    }

    public sealed partial class NativeFoodMarket
    {
        /// <summary>F8b: E5b dynamic acquisition is on (read-only copy for the capability report).</summary>
        internal bool DynamicCapabilityActive => DynamicAcquisition;
        /// <summary>F8b: why dynamic acquisition is off once the coordinator left startup; empty while warming.</summary>
        internal string DynamicCapabilityRefusal => DynamicAcquisition || StartupPhase ? "" : DynamicRefusalReason;

        /// <summary>F8f (Sol-r1-2): the scope verdict from state the market already holds. Unlike ScopeStatus it calls no
        /// SupportedMenu, ContractAdmits or NativeFoodOwnership.Find, so it refuses no contract, rebases no image, records
        /// no refusal hour and changes no observation. NativeFoodOwnership.Row has no side effect.</summary>
        internal string ReadOnlyScopeStatus(string placeId)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) return "Excluded: another save is loaded";
            if (CheckoutRefusal != null) return NativeFoodContractSpec.CheckoutScopeReason(CheckoutRefusal);
            if (supplyExclusions.TryGetValue(placeId, out var excluded)) return excluded;
            if (!registrations.TryGetValue(placeId, out var reg)) return "Excluded: unsupported menu or business type";
            if (refusedContracts.TryGetValue(reg, out _)) return "Excluded: food contract refused; waiting for a recovery boundary";
            var menu = reg.cachedAvailableProducts;
            if (menu == null || menu.Count == 0) return "Excluded: empty menu";
            bool historical = true;
            for (int i = 0; i < menu.Count; i++) historical &= FoodProducts.Known(menu[i]);
            if (!historical && !DynamicAcquisition)
                return contractMode == ContractMode.Warming ? "Waiting: food contract warming; non-original products wait for every shop's first image"
                    : DynamicIntegrationRefusal + ": " + DynamicRefusalReason;
            var row = NativeFoodOwnership.Row(placeId);
            return row == null || !row.CanServe ? "Waiting for a safe native accounting boundary" : "";
        }
    }
}
