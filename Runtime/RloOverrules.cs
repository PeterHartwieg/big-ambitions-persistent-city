using System;
using System.Linq;
using BAModAPI;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>
    /// RLO 1.0 overrule (Peter, 12 Nov 2026): Persistent City owns the areas where it and RLO overlap, with RLO's Living
    /// population on or off. This holds the session's Living population tracker (Compatibility/RloLivingPopulation bound
    /// once, Core/RloLivingTracker polled about once a real second, never per frame) and the status line. The overrule
    /// itself is enforced where each area lives: the owned-shop gate (NativeFoodOwnership via RloOverrule.AdmitsNative),
    /// the F8h checkout decision (NativeFoodContract), stationary crowds and the own-car traffic hold. Main thread only.
    /// </summary>
    internal static class RloOverrules
    {
        private static RloLivingTracker? living;

        /// <summary>Set by PhysicalVehicles while it holds the ordinary native groups back for its own cars.</summary>
        internal static bool TrafficHeld { get; set; }

        internal static RloLivingTracker Living => living ??= new RloLivingTracker(RloLivingPopulation.Bind(
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == RloLivingPopulation.AssemblyName)).Read);

        /// <summary>The switch read now, for one city load's checkout decision.</summary>
        internal static RloLivingReading ReadForLoad()
        {
            var tracker = Living;
            if (tracker.Refresh()) Debug.Log("[Persistent City] RLO " + tracker.Changes[tracker.Changes.Count - 1] + " at city load; Persistent City keeps the shops it owns.");
            return tracker.Current;
        }

        /// <summary>The coarse mid-session check: one static field read per second at most. A change is logged; the owned
        /// shops, crowds and traffic need no action because the overrule does not depend on the switch. A change to
        /// Unknown is logged and refuses food.checkout.shop from the next city load.</summary>
        internal static void Poll(float realSeconds, IModLogger? log)
        {
            var tracker = Living;
            if (!tracker.Poll(realSeconds)) return;
            string line = "RLO " + tracker.Changes[tracker.Changes.Count - 1] + " during play; Persistent City keeps owning its shops, crowds and traffic"
                + (tracker.Current.State == RloLivingState.Unknown ? ", and takes no shop over from the next city load (unknown holds for this session)." : ".");
            if (log != null) log.Info(line); else Debug.Log("[Persistent City] " + line);
        }

        internal static string Status(int calculatorsReplaced, string? checkoutRefusal)
        {
            var tracker = Living;
            return RloOverrule.Describe(tracker.Current, tracker.Toggles, calculatorsReplaced, checkoutRefusal,
                StationaryCrowds.Overruling, StationaryCrowds.RloZones(), TrafficHeld);
        }
    }
}
