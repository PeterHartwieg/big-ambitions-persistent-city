using System;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>One business lifetime at an address. Native stock and staffing are not eligibility.</summary>
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
                // Keep a closed earlier interval after replacement: it is also durable proof
                // that this saved city has owned commerce and cannot recreate missing wallets.
                if (handoff) { FromHour = hour; UntilHour = -1; }
                Suspended = handoff;
                Desired = eligible; RequestedHour = hour; changed = true;
            }
            if (Desired != eligible)
            { Desired = eligible; RequestedHour = hour; changed = true; }
            return changed;
        }

        // Called only after the old distributed native batch has completed. Waiting at least
        // one observed hour prevents replaying the partly native hour in which we discovered a shop.
        public bool Boundary(long hour, bool oldWorkDrained, bool liveNativeCustomers, bool marketReady)
        {
            if (!oldWorkDrained || hour <= RequestedHour || Desired == Managed && !Suspended) return false;
            if (Desired)
            {
                if (!marketReady || liveNativeCustomers) return false;
                FromHour = hour; UntilHour = -1;
            }
            else UntilHour = hour;
            Suspended = false;
            return true;
        }
    }
}
