using System;

namespace PersistentCity.Core
{
    // F8b (docs/plans/f8-capability-boundaries.md D7): the registry built on request from the status members that already
    // exist in Runtime. Runtime copies them into CapabilityInputs; nothing here reads native state, writes saved state or runs
    // per frame.

    /// <summary>The last RLO donor read, by reason class. NotReady is an unclassified not-ready answer; it stops as today.</summary>
    public enum DonorReadReason : byte { NotRead, Ready, Missing, Off, NotYet, Refreshing, Shared, Failed, Incompatible, NotReady }

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
        public string VendorReason { get; set; } = "";

        public bool ClockObserving { get; set; }
        public string ClockObserveError { get; set; } = "";
        public bool ClockCityActive { get; set; }
        public double ClockFactor { get; set; } = 1;
        public bool DrivenRidesLoaded { get; set; }
        public bool DrivenRidesCoordinated { get; set; }

        public DonorReadReason RloReason { get; set; }
        public string RloMessage { get; set; } = "";

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

        /// <summary>The F0 interior view is configured on (NativeInteriorCityView.Configured).</summary>
        public bool InteriorConfigured { get; set; }
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
                if (input.VendorApiBound) r.Detect(CapabilityId.VendorStreetFood, CapabilityState.Active);
                else Off(r, CapabilityId.VendorStreetFood, input.VendorReason.Length > 0 ? input.VendorReason : "VendorApi contract refused");
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

            if (input.InteriorConfigured)
            {
                if (input.InteriorGuardAllowed) r.Detect(CapabilityId.InteriorView, CapabilityState.Active);
                else Off(r, CapabilityId.InteriorView, Or(input.InteriorGuardReason, "third-party interior guard refused"));
            }
            return r;
        }

        private static void DetectDonor(CapabilityRegistry r, CapabilityInputs input)
        {
            string message = Or(input.RloMessage, input.RloReason.ToString());
            switch (input.RloReason)
            {
                case DonorReadReason.NotRead: return;
                case DonorReadReason.Ready: r.Detect(CapabilityId.RloDonor, CapabilityState.Active); return;
                case DonorReadReason.Missing: r.Detect(CapabilityId.RloDonor, CapabilityState.Absent, message); return;
                default: Off(r, CapabilityId.RloDonor, message); return;
            }
        }

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
