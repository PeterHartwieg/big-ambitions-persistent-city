using System;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Compatibility;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>
    /// Stops the game's stationary pedestrian zones from placing their anonymous standers: 2–4 bodies per zone,
    /// put down whenever the zone comes into view and taken away when it leaves, whatever the hour or weather
    /// [1.0: StationaryAiSpawner.SpawnGroupOverFrames, Spawn(Vector3)]. Street performers and the homeless from
    /// the same zones stay: the performer is a happiness source the game's help names. Only Spawn(Vector3), the
    /// ordinary stander, is skipped; the zones, their culling and their other spawns run as before.
    /// RLO 1.0 overrule: RLO's landmark, campus and Uptown crowds are copies of these same native zones
    /// (<see cref="RloLivingPopulation.CrowdZoneName"/>), so their standers are deliberately off too, including zones RLO
    /// makes later. RLO's own CityPeople scene pool is a separate system and is not touched.
    /// </summary>
    internal sealed class StationaryCrowds : IDisposable
    {
        private const string Owner = "hart.persistentcity.stationarycrowds.v1";
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly MethodInfo? spawn = AccessTools.Method(typeof(StationaryAiSpawner), "Spawn", new[] { typeof(Vector3) });
        private readonly FieldInfo? visible = AccessTools.Field(typeof(StationaryAiSpawner), "isVisible");
        private bool active;
        public string LastError { get; private set; } = "";
        /// <summary>The stander prefix is installed (for the RLO overrule status line).</summary>
        internal static bool Overruling { get; private set; }

        /// <summary>Native zones RLO made, counted on request (status), never per frame.</summary>
        internal static int RloZones()
        {
            int count = 0;
            foreach (var zone in StationaryAiSpawner.AllPedestrianSpawners)
                if (zone != null && zone.name == RloLivingPopulation.CrowdZoneName) count++;
            return count;
        }

        public void Activate()
        {
            if (active) return;
            try
            {
                if (spawn == null || spawn.ReturnType != typeof(void) || visible == null || visible.FieldType != typeof(bool))
                    throw new InvalidOperationException("Unexpected native stationary spawner contract.");
                if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("Another stationary crowd hook owner is already registered.");
                harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));
                active = Overruling = true;
                // Zones already in view keep their standers until they leave view; clear them now and let the
                // next time each zone comes into view place only its performer or homeless person.
                int cleared = 0;
                foreach (var zone in StationaryAiSpawner.AllPedestrianSpawners)
                {
                    if (zone == null || !(bool)visible.GetValue(zone)!) continue;
                    zone.ClearPedestrians();
                    visible.SetValue(zone, false);
                    cleared++;
                }
                UnityEngine.Debug.Log("[Persistent City] Stationary crowds: the game's anonymous standers are off ("
                    + StationaryAiSpawner.AllPedestrianSpawners.Count + " zones, " + RloZones() + " of them RLO's, " + cleared
                    + " in view cleared); street performers and the homeless kept; RLO's own scene people untouched.");
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                harmony.UnpatchAll(Owner);
                active = Overruling = false;
                UnityEngine.Debug.LogWarning("[Persistent City] Stationary crowds left as the game has them: " + LastError);
            }
        }

        private static bool SpawnPrefix() => false;

        public void Dispose()
        {
            if (!active) return;
            active = Overruling = false;
            harmony.UnpatchAll(Owner);
        }
    }
}
