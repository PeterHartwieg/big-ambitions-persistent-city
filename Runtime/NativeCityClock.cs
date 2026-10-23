using System;
using System.Linq;
using HarmonyLib;
using UI;
using UI.Load;

namespace PersistentCity.Runtime
{
    /// <summary>City-scoped calendar pace. Never writes Unity's movement/physics time scale.</summary>
    internal static class NativeCityClock
    {
        private const string Owner = "hart.persistentcity.cityclock.v1";
        private static Harmony? patches;
        private static object? save;
        public static double Factor { get; private set; } = 1;
        public static bool CoordinatesDrivenRides { get; private set; }
        public static bool Active => patches != null && save != null && ReferenceEquals(save, SaveGameManager.Current);
        private static bool OrdinaryTick => Active && !LoadScene.isLoading
            && InstanceBehavior<UIs>.Instance?.timeMachine != null && !InstanceBehavior<UIs>.Instance.timeMachine.isRunning;

        public static void Start(object currentSave, double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < .1 || factor > 1)
                throw new ArgumentOutOfRangeException(nameof(factor), "GameClockFactor must be between 0.1 and 1.");
            if (patches != null) throw new InvalidOperationException("City clock already active.");
            if (factor == 1) return; // Native pace also retains Driven Rides' independent ride setting.
            if (!ReferenceEquals(currentSave, SaveGameManager.Current)) throw new InvalidOperationException("City clock save changed.");
            if (Harmony.HasAnyPatches(Owner)) throw new InvalidOperationException("City clock patch ID already owned.");
            var installing = new Harmony(Owner);
            try
            {
                var tick = AccessTools.Method(typeof(GameManager), nameof(GameManager.RunMainGameTick), new[] { typeof(float) })
                    ?? throw new MissingMethodException("GameManager.RunMainGameTick(float)");
                // Calendar policy precedes boundary caps (including the rig's Priority.First
                // run-until limiter), so a last partial tick still reaches its requested time.
                installing.Patch(tick, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(TickPrefix)) { priority = Priority.First + 1 });
                // Optional adapter for the installed Driven Rides contract. Undo only its positive
                // ride multiplier, preserving an explicit zero/held clock and all other tick patches.
                var ride = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DrivenRides.Ride", false)).FirstOrDefault(t => t != null);
                if (ride != null)
                {
                    var cap = AccessTools.Method(ride, "CapTick", new[] { typeof(float).MakeByRefType() })
                        ?? throw new MissingMethodException("DrivenRides.Ride.CapTick(ref float)");
                    if (!cap.IsStatic || cap.ReturnType != typeof(void)) throw new InvalidOperationException("Unsupported Driven Rides clock contract.");
                    installing.Patch(cap, prefix: new HarmonyMethod(typeof(NativeCityClock), nameof(RidePrefix)),
                        postfix: new HarmonyMethod(typeof(NativeCityClock), nameof(RidePostfix)));
                    CoordinatesDrivenRides = true;
                }
                Factor = factor; save = currentSave; patches = installing;
            }
            catch { installing.UnpatchAll(Owner); CoordinatesDrivenRides = false; throw; }
        }

        private static void TickPrefix(ref float deltaTimeWithMultiplier)
        {
            if (OrdinaryTick && deltaTimeWithMultiplier > 0 && !float.IsInfinity(deltaTimeWithMultiplier))
                deltaTimeWithMultiplier *= (float)Factor;
        }
        private static void RidePrefix(float __0, out float __state) => __state = OrdinaryTick ? __0 : float.NaN;
        private static void RidePostfix(ref float __0, float __state)
        {
            if (!float.IsNaN(__state) && __0 > 0 && OrdinaryTick) __0 = __state;
        }
        public static void Stop()
        {
            save = null;
            try { patches?.UnpatchAll(Owner); }
            finally { patches = null; Factor = 1; CoordinatesDrivenRides = false; }
        }
    }
}
