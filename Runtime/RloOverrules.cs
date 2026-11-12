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
        private static RloTrafficScenes? scenes;
        private static bool trafficHeld;
        private static float nextSceneHold;
        private static int scenesHeld;

        /// <summary>Set by PhysicalVehicles while it holds the ordinary native groups back for its own cars. Review
        /// Sol-r1-2: holding also stands down RLO's staged accidents and reckless drivers, which take over live native cars;
        /// releasing gives the player's values back.</summary>
        internal static bool TrafficHeld
        {
            get => trafficHeld;
            set
            {
                trafficHeld = value;
                if (value)
                {
                    scenesHeld = Scenes.Hold();
                    // Review Sol-r2-1: a staged accident already under way is ended once; its tick reads the switch only between scenes.
                    bool ended = scenesHeld > 0 && Scenes.EndActiveScene();
                    if (Scenes.Holds.Count > 0 || Scenes.Unbound.Count > 0)
                        Debug.Log("[Persistent City] RLO traffic scenes that take over native cars stood down while Persistent City cars hold traffic ("
                            + scenesHeld + " of " + RloTrafficScenes.Switches.Length + " switches" + (ended ? ", a scene under way ended" : "")
                            + (Scenes.Unbound.Count == 0 ? "" : "; not bound: " + string.Join("; ", Scenes.Unbound)) + ").");
                }
                else
                {
                    scenesHeld = 0;
                    if (Scenes.Holding) { Scenes.Release(); Debug.Log("[Persistent City] RLO traffic scene switches given back (the player's stored options)."); }
                }
            }
        }

        private static System.Reflection.Assembly? Rlo => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == RloLivingPopulation.AssemblyName);

        internal static RloLivingTracker Living => living ??= new RloLivingTracker(RloLivingPopulation.Bind(Rlo).Read);
        private static RloTrafficScenes Scenes => scenes ??= RloTrafficScenes.Bind(Rlo, key => UnityEngine.PlayerPrefs.HasKey(key) ? UnityEngine.PlayerPrefs.GetInt(key) : (int?)null);

        /// <summary>Review Sol-r1-1: the cached switch state holds new shop takeovers at boundaries during play.</summary>
        internal static bool HoldsAcquisitions => RloOverrule.HoldsAcquisitions(Living.Current.State);

        /// <summary>The switch read now, for one city load's checkout decision.</summary>
        internal static RloLivingReading ReadForLoad()
        {
            var tracker = Living;
            if (tracker.Refresh()) Debug.Log("[Persistent City] RLO " + tracker.Changes[tracker.Changes.Count - 1] + " at city load; Persistent City keeps the shops it owns.");
            return tracker.Current;
        }

        /// <summary>The coarse mid-session check: one static field read per second at most. A change is logged; the owned
        /// shops, crowds and traffic need no action because the overrule does not depend on the switch. A change to
        /// Unknown holds new takeovers at once (<see cref="HoldsAcquisitions"/>) and refuses food.checkout.shop from the
        /// next city load.</summary>
        internal static void Poll(float realSeconds, IModLogger? log)
        {
            // The scene switches are re-held on the same cadence: a player who switched one on during the hold keeps
            // that value for the release, and the scene stays down meanwhile.
            if (trafficHeld && realSeconds >= nextSceneHold) { nextSceneHold = realSeconds + (float)RloLivingTracker.PollSeconds; Scenes.Hold(); }
            var tracker = Living;
            if (!tracker.Poll(realSeconds)) return;
            string line = "RLO " + tracker.Changes[tracker.Changes.Count - 1] + " during play; Persistent City keeps owning its shops, crowds and traffic"
                + (tracker.Current.State == RloLivingState.Unknown ? ", and takes no further shop over this session (shops it owns keep selling)." : ".");
            if (log != null) log.Info(line); else Debug.Log("[Persistent City] " + line);
        }

        internal static string Status(int calculatorsReplaced, string? checkoutRefusal)
        {
            var tracker = Living;
            return RloOverrule.Describe(tracker.Current, tracker.Toggles, calculatorsReplaced, checkoutRefusal,
                StationaryCrowds.Overruling, StationaryCrowds.RloZones(), TrafficHeld, scenesHeld);
        }
    }
}
