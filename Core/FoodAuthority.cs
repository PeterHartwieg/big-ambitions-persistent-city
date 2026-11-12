using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public enum FoodBoundaryStep { None, Acquire, HandBack }

    public sealed class FoodAuthority
    {
        public string PlaceId { get; set; } = "";
        public string Signature { get; set; } = "";
        public int Generation { get; set; } = 1;
        public bool Desired { get; set; }
        public long RequestedHour { get; set; }
        public long FromHour { get; set; } = -1;
        public long UntilHour { get; set; } = -1;
        public bool Suspended { get; set; }
        public string BusinessId => Signature + ":generation:" + Generation.ToString(CultureInfo.InvariantCulture);
        public bool Managed => FromHour >= 0 && UntilHour < 0;
        public bool CanServe => Managed && Desired && !Suspended;
        public bool OwnsHour(long hour) => FromHour >= 0 && hour >= FromHour && (UntilHour < 0 || hour < UntilHour);

        public bool Observe(string signature, bool eligible, long hour, bool reset = false)
        {
            if (hour < 0 || string.IsNullOrWhiteSpace(signature)) throw new ArgumentException("Invalid business observation.");
            bool changed = false;
            if (Signature != signature || reset)
            {
                bool handoff = Managed || OwnsHour(hour);
                if (Signature.Length > 0) Generation = checked(Generation + 1);
                Signature = signature;
                if (handoff) { FromHour = hour; UntilHour = -1; }
                Suspended = handoff;
                Desired = eligible; RequestedHour = hour; changed = true;
            }
            if (Desired != eligible)
            { Desired = eligible; RequestedHour = hour; Deferred = false; changed = true; }
            return changed;
        }

        public bool Deferred { get; private set; }

        public FoodBoundaryStep Pending(long hour, bool oldWorkDrained, bool liveNativeCustomers, bool marketReady)
        {
            if (!oldWorkDrained || hour <= RequestedHour || Desired == Managed && !Suspended) return FoodBoundaryStep.None;
            if (!Desired) return FoodBoundaryStep.HandBack;
            return marketReady && !liveNativeCustomers ? FoodBoundaryStep.Acquire : FoodBoundaryStep.None;
        }

        public bool Boundary(long hour, bool oldWorkDrained, bool liveNativeCustomers, bool marketReady)
            => Boundary(hour, oldWorkDrained, liveNativeCustomers, marketReady, true);

        public bool Boundary(long hour, bool oldWorkDrained, bool liveNativeCustomers, bool marketReady, bool contractReady)
        {
            var step = Pending(hour, oldWorkDrained, liveNativeCustomers, marketReady);
            if (step == FoodBoundaryStep.None) return false;
            if (step == FoodBoundaryStep.Acquire)
            {
                if (!contractReady) { Deferred = true; return false; }
                FromHour = hour; UntilHour = -1;
            }
            else UntilHour = hour;
            Suspended = false; Deferred = false;
            return true;
        }
    }
}
