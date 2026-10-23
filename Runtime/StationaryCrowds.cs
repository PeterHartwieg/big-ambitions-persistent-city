using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>
    /// Stops the game's stationary pedestrian zones from placing their anonymous standers: 2–4 bodies per zone,
    /// put down whenever the zone comes into view and taken away when it leaves, whatever the hour or weather
    /// [1.0: StationaryAiSpawner.SpawnGroupOverFrames, Spawn(Vector3)]. Street performers and the homeless from
    /// the same zones stay: the performer is a happiness source the game's help names. Only Spawn(Vector3), the
    /// ordinary stander, is skipped; the zones, their culling and their other spawns run as before.
    /// </summary>
    internal sealed class StationaryCrowds : IDisposable
    {
        private const string Owner = "hart.persistentcity.stationarycrowds.v1";
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly MethodInfo? spawn = AccessTools.Method(typeof(StationaryAiSpawner), "Spawn", new[] { typeof(Vector3) });
        private readonly FieldInfo? visible = AccessTools.Field(typeof(StationaryAiSpawner), "isVisible");
        private bool active;
        public string LastError { get; private set; } = "";

        public void Activate()
        {
            if (active) return;
            try
            {
                if (spawn == null || spawn.ReturnType != typeof(void) || visible == null || visible.FieldType != typeof(bool))
                    throw new InvalidOperationException("Unexpected native stationary spawner contract.");
                if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("Another stationary crowd hook owner is already registered.");
                harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));
                active = true;
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
                    + StationaryAiSpawner.AllPedestrianSpawners.Count + " zones, " + cleared + " in view cleared); street performers and the homeless kept.");
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                harmony.UnpatchAll(Owner);
                active = false;
                UnityEngine.Debug.LogWarning("[Persistent City] Stationary crowds left as the game has them: " + LastError);
            }
        }

        private static bool SpawnPrefix() => false;

        public void Dispose()
        {
            if (!active) return;
            active = false;
            harmony.UnpatchAll(Owner);
        }
    }
}
