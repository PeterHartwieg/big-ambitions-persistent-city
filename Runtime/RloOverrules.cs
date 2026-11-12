using System;
using System.Linq;
using BAModAPI;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class RloOverrules
    {
        private static RloLivingTracker? living;
        private static RloTrafficScenes? scenes;
        private static bool trafficHeld;
        private static float nextSceneHold;
        private static int scenesHeld;

        internal static bool TrafficHeld
        {
            get => trafficHeld;
            set
            {
                trafficHeld = value;
                if (value)
                {
                    scenesHeld = Scenes.Hold();
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

        internal static bool HoldsAcquisitions => RloOverrule.HoldsAcquisitions(Living.Current.State);

        internal static RloLivingReading ReadForLoad()
        {
            var tracker = Living;
            if (tracker.Refresh()) Debug.Log("[Persistent City] RLO " + tracker.Changes[tracker.Changes.Count - 1] + " at city load; Persistent City keeps the shops it owns.");
            return tracker.Current;
        }

        internal static void Poll(float realSeconds, IModLogger? log)
        {
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
