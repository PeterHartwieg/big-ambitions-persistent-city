using System;

namespace PersistentCity.Core
{

    public enum DonorReadReason : byte { NotRead, Ready, Missing, Off, NotYet, Refreshing, Shared, Failed, Incompatible, NotReady }

    public enum DonorContinuity : byte { Match, Different, Transient, Unsupported }

    public static class DonorReads
    {
        public static bool Transient(DonorReadReason reason)
            => reason == DonorReadReason.Off || reason == DonorReadReason.NotYet || reason == DonorReadReason.Refreshing;

        public static DonorContinuity Continuity(DonorReadReason reason)
            => reason == DonorReadReason.Ready ? throw new ArgumentException("A Ready read is classed by matching the population.", nameof(reason))
                : Transient(reason) ? DonorContinuity.Transient : DonorContinuity.Unsupported;

        public static bool Continues(DonorContinuity continuity) => continuity == DonorContinuity.Match || continuity == DonorContinuity.Transient;
    }

    public sealed class CapabilityInputs
    {
        public bool CityActive { get; set; }
        public bool CityFailed { get; set; }
        public string FailureStatus { get; set; } = "";

        public bool FoodAuthorityInstalled { get; set; }
        public string? FoodAuthorityRefusal { get; set; }
        public string FoodAuthorityError { get; set; } = "";
        public bool FoodMarketAttached { get; set; }
        public bool FoodPricePrefixInstalled { get; set; }
        public string? FoodCheckoutRefusal { get; set; }
        public bool FoodDynamicActive { get; set; }
        public string FoodDynamicRefusal { get; set; } = "";

        public bool StreetFoodLoaded { get; set; }
        public bool VendorMarketAttached { get; set; }
        public bool VendorApiBound { get; set; }
        public VendorApiStatus VendorSnapshot { get; set; } = VendorApiStatus.NotYet;
        public string VendorReason { get; set; } = "";

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
        public bool RloTransient { get; set; }
        public bool RloDiffers { get; set; }
        public bool RoadSpeedsFromRlo { get; set; }
        public string? RoadSpeedWaiting { get; set; }
        public bool RoadSpeedsVerified { get; set; }
        public string? RoadSpeedRefusal { get; set; }
        public bool ReservationsChecked { get; set; }
        public bool ReservationsReady { get; set; }
        public bool ReservationsComplete { get; set; }
        public string ReservationsError { get; set; } = "";

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

        public bool InteriorConfigured { get; set; }
        public bool InteriorRequested { get; set; }
        public bool InteriorGuardAllowed { get; set; } = true;
        public string InteriorGuardReason { get; set; } = "";
    }

    public static class CapabilitySnapshot
    {
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

            if (input.InspectorFailed || city && (input.InspectorClickInstalled || input.InspectorScrollInstalled || input.InspectorUiBlockInstalled))
            {
                Guard(r, input, CapabilityId.InspectorClick, input.InspectorClickInstalled, "click guard not installed; see Player.log");
                Guard(r, input, CapabilityId.InspectorScroll, input.InspectorScrollInstalled, "scroll guard not installed; see Player.log");
                Guard(r, input, CapabilityId.InspectorUiBlock, input.InspectorUiBlockInstalled, "UI block not installed; see Player.log");
            }

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

        private static void DetectRloReads(CapabilityRegistry r, CapabilityInputs input)
        {
            if (input.RloReason == DonorReadReason.Missing)
            {
                r.Detect(CapabilityId.RloReservations, CapabilityState.Absent, "RLO is not loaded");
                r.Detect(CapabilityId.RloRoadSpeeds, CapabilityState.Absent, "RLO is not loaded; road speeds are native");
                return;
            }
            if (input.RoadSpeedRefusal != null) Off(r, CapabilityId.RloRoadSpeeds, input.RoadSpeedRefusal + " (refused; the runtime stopped, payload kept)");
            else if (input.RoadSpeedWaiting != null) Off(r, CapabilityId.RloRoadSpeeds, input.RoadSpeedWaiting + " " + RoadSpeedWaitingEffect);
            else if (input.RoadSpeedsFromRlo && input.RoadSpeedsVerified) r.Detect(CapabilityId.RloRoadSpeeds, CapabilityState.Active);
            if (!input.ReservationsChecked) return;
            if (!input.ReservationsReady) Off(r, CapabilityId.RloReservations, Or(input.ReservationsError, "reservation guard stopped") + "; everyone is reserved, visible bodies off");
            else if (!input.ReservationsComplete) Off(r, CapabilityId.RloReservations, "a read answered NotYet or Refreshing; everyone is reserved, so visible bodies pause until RLO answers");
            else r.Detect(CapabilityId.RloReservations, CapabilityState.Active);
        }

        public const string RoadSpeedWaitingEffect = "Route preparation waits for a complete answer; the city keeps running and saving.";

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
