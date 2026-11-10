using System;

namespace PersistentCity.Core
{
    // F8b (docs/plans/f8-capability-boundaries.md D7): the registry built on request from the status members that already
    // exist in Runtime. Runtime copies them into CapabilityInputs; nothing here reads native state, writes saved state or runs
    // per frame.

    /// <summary>The last RLO donor read, by reason class. NotReady is an unclassified not-ready answer; it stops as today.</summary>
    public enum DonorReadReason : byte { NotRead, Ready, Missing, Off, NotYet, Refreshing, Shared, Failed, Incompatible, NotReady }

    /// <summary>F8d (D6 option B): what a running city does with one periodic or save-time donor read.</summary>
    public enum DonorContinuity : byte { Match, Different, Transient, Unsupported }

    public static class DonorReads
    {
        /// <summary>Off, NotYet and Refreshing say nothing about the saved catalogue, so a running city continues on it.</summary>
        public static bool Transient(DonorReadReason reason)
            => reason == DonorReadReason.Off || reason == DonorReadReason.NotYet || reason == DonorReadReason.Refreshing;

        /// <summary>The class of a read that is not Ready. Shared, Failed, Incompatible, Missing and any unclassified
        /// not-ready answer stop, as before F8d. A Ready read is Match or Different by the identity comparison.</summary>
        public static DonorContinuity Continuity(DonorReadReason reason)
            => reason == DonorReadReason.Ready ? throw new ArgumentException("A Ready read is classed by matching the population.", nameof(reason))
                : Transient(reason) ? DonorContinuity.Transient : DonorContinuity.Unsupported;

        /// <summary>A running city may continue (and save) after this read.</summary>
        public static bool Continues(DonorContinuity continuity) => continuity == DonorContinuity.Match || continuity == DonorContinuity.Transient;
    }

    /// <summary>Plain copies of the existing Runtime status members (CapabilityDiagnostics fills them on request).</summary>
    public sealed class CapabilityInputs
    {
        /// <summary>A city is attached for the current save and the runtime has not stopped.</summary>
        public bool CityActive { get; set; }
        /// <summary>The runtime stopped this session; <see cref="FailureStatus"/> is its last status line.</summary>
        public bool CityFailed { get; set; }
        public string FailureStatus { get; set; } = "";

        /// <summary>The food-authority owner has its patches installed (Harmony owner query).</summary>
        public bool FoodAuthorityInstalled { get; set; }
        /// <summary>NativeFoodContract's required-contract refusal (F8c); null when verified or not checked.</summary>
        public string? FoodAuthorityRefusal { get; set; }
        /// <summary>NativeFoodOwnership.Error: a refused manifest or payload suspends food ownership and stops the city.</summary>
        public string FoodAuthorityError { get; set; } = "";
        public bool FoodMarketAttached { get; set; }
        /// <summary>The food-routines owner has its price prefix installed.</summary>
        public bool FoodPricePrefixInstalled { get; set; }
        /// <summary>The market's checkout-contract refusal (F8c checkout-off mode); null when verified.</summary>
        public string? FoodCheckoutRefusal { get; set; }
        /// <summary>E5b dynamic acquisition is on (coordinator active and every contract capability passed).</summary>
        public bool FoodDynamicActive { get; set; }
        /// <summary>The E5b refusal reason once the coordinator left warming without dynamic acquisition; empty otherwise.</summary>
        public string FoodDynamicRefusal { get; set; } = "";

        public bool StreetFoodLoaded { get; set; }
        public bool VendorMarketAttached { get; set; }
        /// <summary>Street Food's VendorApi bound (the coordinator has an image reader).</summary>
        public bool VendorApiBound { get; set; }
        /// <summary>The bound API's last snapshot answer; NotYet before the first read.</summary>
        public VendorApiStatus VendorSnapshot { get; set; } = VendorApiStatus.NotYet;
        /// <summary>The snapshot's reason when it is not Ready, else the coordinator's own reason.</summary>
        public string VendorReason { get; set; } = "";

        /// <summary>Copies the coordinator's binding and snapshot state (Runtime and tests use the same reader).</summary>
        public void ReadVendor(bool streetFoodLoaded, VendorCoordinator? coordinator)
        {
            StreetFoodLoaded = streetFoodLoaded; VendorMarketAttached = coordinator != null;
            var image = coordinator?.Image;
            VendorApiBound = image != null;
            VendorSnapshot = image?.Status ?? VendorApiStatus.NotYet;
            VendorReason = image != null && !image.Ready ? image.Reason : coordinator?.Reason ?? "";
        }

        public bool ClockObserving { get; set; }
        public string ClockObserveError { get; set; } = "";
        public bool ClockCityActive { get; set; }
        public double ClockFactor { get; set; } = 1;
        public bool DrivenRidesLoaded { get; set; }
        public bool DrivenRidesCoordinated { get; set; }

        public DonorReadReason RloReason { get; set; }
        public string RloMessage { get; set; } = "";
        /// <summary>F8d: the running city is inside a transient RLO window.</summary>
        public bool RloTransient { get; set; }
        /// <summary>The last periodic, save or activation verdict found a Ready catalogue that differs from the saved one.</summary>
        public bool RloDiffers { get; set; }
        /// <summary>Road speeds come from RLO (the planner has an RLO capture); false while no route planner exists.</summary>
        public bool RoadSpeedsFromRlo { get; set; }
        /// <summary>The road-speed capture's own last transient answer (NativeRoadSpeeds.Waiting); null when complete.</summary>
        public string? RoadSpeedWaiting { get; set; }

        public bool ResearchDialogInstalled { get; set; }
        public string ResearchDialogStatus { get; set; } = "";
        public bool ResearchBillingInstalled { get; set; }
        public string ResearchBillingStatus { get; set; } = "";
        public bool ResearchMessagesChecked { get; set; }
        public bool ResearchMessagesPassed { get; set; }
        public string ResearchMessagesStatus { get; set; } = "";

        public bool PhysicalEnabled { get; set; }
        public bool PhysicalActive { get; set; }
        public bool PhysicalFailed { get; set; }

        public bool InspectorFailed { get; set; }
        public bool InspectorClickInstalled { get; set; }
        public bool InspectorScrollInstalled { get; set; }
        public bool InspectorUiBlockInstalled { get; set; }

        /// <summary>The F0 interior view is configured on (NativeInteriorCityView.Configured: requested, the guard allowed it
        /// and the native speed accessor resolved).</summary>
        public bool InteriorConfigured { get; set; }
        /// <summary>The setting asks for the interior view (InteriorCityView "on").</summary>
        public bool InteriorRequested { get; set; }
        public bool InteriorGuardAllowed { get; set; } = true;
        public string InteriorGuardReason { get; set; } = "";
    }

    public static class CapabilitySnapshot
    {
        /// <summary>One D1 state per capability from the inputs. A capability whose detection point has not been reached
        /// stays not checked; the report never infers more than the inputs show.</summary>
        public static CapabilityRegistry Build(CapabilityInputs input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var r = new CapabilityRegistry();
            bool city = input.CityActive;
            string stopped = "not running: the runtime stopped" + (input.FailureStatus.Length == 0 ? "" : " (" + input.FailureStatus + ")");

            if (input.FoodAuthorityRefusal != null) Off(r, CapabilityId.FoodAuthority, input.FoodAuthorityRefusal);
            else if (input.FoodAuthorityError.Length > 0) Off(r, CapabilityId.FoodAuthority, input.FoodAuthorityError);
            else if (input.FoodAuthorityInstalled) r.Detect(CapabilityId.FoodAuthority, CapabilityState.Active);

            if (input.FoodCheckoutRefusal != null) Off(r, CapabilityId.FoodCheckoutShop, input.FoodCheckoutRefusal);
            else if (input.FoodMarketAttached && input.FoodPricePrefixInstalled) r.Detect(CapabilityId.FoodCheckoutShop, CapabilityState.Active);
            else if (input.FoodMarketAttached) Off(r, CapabilityId.FoodCheckoutShop, "food-routines price prefix is not installed");

            if (input.FoodMarketAttached)
            {
                if (input.FoodDynamicActive) r.Detect(CapabilityId.FoodDynamic, CapabilityState.Active);
                else if (input.FoodDynamicRefusal.Length > 0) Off(r, CapabilityId.FoodDynamic, input.FoodDynamicRefusal);
            }

            if (!input.StreetFoodLoaded) r.Detect(CapabilityId.VendorStreetFood, CapabilityState.Absent, "Street Food (FoodTrucks) is not loaded");
            else if (input.VendorMarketAttached)
            {
                // A bound API whose snapshot is Off, Refused or Failed suspends every vehicle scope: off with its reason.
                if (!input.VendorApiBound) Off(r, CapabilityId.VendorStreetFood, input.VendorReason.Length > 0 ? input.VendorReason : "VendorApi contract refused");
                else if (input.VendorSnapshot == VendorApiStatus.Ready) r.Detect(CapabilityId.VendorStreetFood, CapabilityState.Active);
                else if (input.VendorSnapshot != VendorApiStatus.NotYet || input.VendorReason != "Street Food has not been read yet.")
                    Off(r, CapabilityId.VendorStreetFood, "snapshot " + input.VendorSnapshot + ": " + Or(input.VendorReason, "not ready"));
            }

            if (input.ClockObserveError.Length > 0) Off(r, CapabilityId.ClockObserve, input.ClockObserveError);
            else if (input.ClockObserving) r.Detect(CapabilityId.ClockObserve, CapabilityState.Active);

            if (input.ClockCityActive) r.Detect(CapabilityId.ClockCity, CapabilityState.Active);
            else if (input.CityFailed) Off(r, CapabilityId.ClockCity, stopped);

            if (!input.DrivenRidesLoaded) r.Detect(CapabilityId.ClockDrivenRides, CapabilityState.Absent, "Driven Rides is not loaded");
            // At factor 1 the calendar runs at native pace, so there is no ride multiplier to undo.
            else if (input.ClockCityActive && (input.DrivenRidesCoordinated || input.ClockFactor == 1))
                r.Detect(CapabilityId.ClockDrivenRides, CapabilityState.Active);
            else if (input.CityFailed) Off(r, CapabilityId.ClockDrivenRides, stopped);

            DetectDonor(r, input);
            DetectRloReads(r, input);

            Research(r, CapabilityId.ResearchDialog, input.ResearchDialogInstalled, input.ResearchDialogStatus);
            Research(r, CapabilityId.ResearchBilling, input.ResearchBillingInstalled, input.ResearchBillingStatus);
            if (input.ResearchMessagesPassed) r.Detect(CapabilityId.ResearchDelivery, CapabilityState.Active);
            else if (input.ResearchMessagesChecked) Off(r, CapabilityId.ResearchDelivery, Or(input.ResearchMessagesStatus, "message contract refused"));

            foreach (var id in new[] { CapabilityId.PhysicalVehicles, CapabilityId.PhysicalPedestrians })
            {
                if (!input.PhysicalEnabled) Off(r, id, "physical population disabled in settings");
                else if (input.PhysicalFailed) Off(r, id, "physical population stopped this session; see Player.log; the background city runs");
                else if (input.PhysicalActive) r.Detect(id, CapabilityState.Active);
            }

            // The guards attach at the first inspector update, click guard first; nothing installed yet is not checked.
            if (input.InspectorFailed || city && (input.InspectorClickInstalled || input.InspectorScrollInstalled || input.InspectorUiBlockInstalled))
            {
                Guard(r, input, CapabilityId.InspectorClick, input.InspectorClickInstalled, "click guard not installed; see Player.log");
                Guard(r, input, CapabilityId.InspectorScroll, input.InspectorScrollInstalled, "scroll guard not installed; see Player.log");
                Guard(r, input, CapabilityId.InspectorUiBlock, input.InspectorUiBlockInstalled, "UI block not installed; see Player.log");
            }

            // NativeInteriorCityView.Start runs at activation and leaves Configured false for a disabled setting, a guard
            // refusal or a missing speed accessor alike; once a city runs, each is reported with its reason.
            if (input.InteriorConfigured) r.Detect(CapabilityId.InteriorView, CapabilityState.Active);
            else if (city)
            {
                if (!input.InteriorRequested) Off(r, CapabilityId.InteriorView, "InteriorCityView is off in the settings (default)");
                else if (!input.InteriorGuardAllowed) Off(r, CapabilityId.InteriorView, Or(input.InteriorGuardReason, "third-party interior guard refused"));
                else Off(r, CapabilityId.InteriorView, "native speed accessor unavailable; see Player.log");
            }
            return r;
        }

        private static void DetectDonor(CapabilityRegistry r, CapabilityInputs input)
        {
            string message = Or(input.RloMessage, input.RloReason.ToString());
            switch (input.RloReason)
            {
                case DonorReadReason.NotRead: return;
                case DonorReadReason.Ready:
                    if (input.RloDiffers) Off(r, CapabilityId.RloDonor, "Ready, but RLO's catalogue differs from the saved one; the city stopped and the payload is kept");
                    else r.Detect(CapabilityId.RloDonor, CapabilityState.Active);
                    return;
                case DonorReadReason.Missing: r.Detect(CapabilityId.RloDonor, CapabilityState.Absent, message); return;
                default:
                    if (!DonorReads.Transient(input.RloReason)) { Off(r, CapabilityId.RloDonor, message); return; }
                    Off(r, CapabilityId.RloDonor, "transient (" + input.RloReason + "): " + message + (input.CityActive
                        ? " The city continues and saves on its saved donor catalogue."
                        : " Persistent City waits for RLO before it starts."));
                    return;
            }
        }

        /// <summary>F8d: each RLO read reports its own last result (no extra RLO read). Road speeds: the planner's capture
        /// (waiting on a transient answer, or complete). Reservations: the guard's state is private to AmbientPedestrians,
        /// which F8 does not edit, so it is reported from the physical population that requires a ready guard.</summary>
        private static void DetectRloReads(CapabilityRegistry r, CapabilityInputs input)
        {
            if (input.RloReason == DonorReadReason.Missing)
            {
                r.Detect(CapabilityId.RloReservations, CapabilityState.Absent, "RLO is not loaded");
                r.Detect(CapabilityId.RloRoadSpeeds, CapabilityState.Absent, "RLO is not loaded; road speeds are native");
                return;
            }
            if (input.RoadSpeedWaiting != null) Off(r, CapabilityId.RloRoadSpeeds, input.RoadSpeedWaiting + " " + RoadSpeedWaitingEffect);
            else if (input.RoadSpeedsFromRlo) r.Detect(CapabilityId.RloRoadSpeeds, CapabilityState.Active);
            if (input.PhysicalActive) r.Detect(CapabilityId.RloReservations, CapabilityState.Active);
            else if (input.PhysicalFailed && input.PhysicalEnabled)
                Off(r, CapabilityId.RloReservations, "physical population stopped (an unready reservation guard is one cause); see Player.log");
        }

        /// <summary>F8d: a transient road-speed answer holds route preparation; it is neither read as "no slowdowns" nor a stop.</summary>
        public const string RoadSpeedWaitingEffect = "Route preparation waits for a complete answer; the city keeps running and saving.";

        /// <summary>The report header: "city running" while a city runs (the start status is then moot), "start waiting"
        /// while a transient RLO answer holds the start; otherwise null (the registry's own start status).</summary>
        public static string? Context(CapabilityInputs input)
            => input.CityActive ? "city running"
                : DonorReads.Transient(input.RloReason) && !input.CityFailed ? "start waiting (rlo.donor transient)" : null;

        private static void Research(CapabilityRegistry r, CapabilityId id, bool installed, string status)
        {
            if (installed && status.Length == 0) r.Detect(id, CapabilityState.Active);
            else if (installed || status.Length > 0 && !status.EndsWith(" is not installed.", StringComparison.Ordinal)) Off(r, id, Or(status, "contract refused"));
        }

        private static void Guard(CapabilityRegistry r, CapabilityInputs input, CapabilityId id, bool installed, string missing)
        {
            if (input.InspectorFailed) Off(r, id, "resident inspector stopped this session; see Player.log");
            else if (installed) r.Detect(id, CapabilityState.Active);
            else Off(r, id, missing);
        }

        private static void Off(CapabilityRegistry r, CapabilityId id, string reason) => r.Detect(id, CapabilityState.Off, Or(reason, "off"));
        private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
