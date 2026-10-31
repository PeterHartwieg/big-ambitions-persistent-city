using System;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Core;
using UnityEngine;
using UI;
using UI.Load;
using Parking.UndergroundParking;

namespace PersistentCity.Runtime
{
    internal static class NativeTravelPace
    {
        private static readonly FieldInfo? Multiplier = typeof(GameManager).GetField("MinutesMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
        private const string ObserverOwner = "hart.persistentcity.clockobserver.v1";
        private static Harmony? observer;
        private static ClockRateTracker tracker = new ClockRateTracker();
        private static string observationError = "";
        public static bool ClockHeld => tracker.Held;
        public static string LastError => observationError.Length != 0 ? observationError : tracker.Error;
        public static bool ObservationActive => observer != null;
        public static void ResetReading() { tracker = new ClockRateTracker(); observationError = ""; }

        /// <summary>Observes the final tick argument after other mods' prefixes; never changes it.</summary>
        public static bool StartObservation()
        {
            if (observer != null) return true;
            ResetReading();
            Harmony? installing = null;
            try
            {
                if (Harmony.HasAnyPatches(ObserverOwner)) throw new InvalidOperationException("Another native clock observer already owns this patch ID.");
                var method = AccessTools.Method(typeof(GameManager), nameof(GameManager.RunMainGameTick), new[] { typeof(float) });
                if (method == null) throw new MissingMethodException("GameManager.RunMainGameTick(float)");
                installing = new Harmony(ObserverOwner);
                installing.Patch(method, postfix: new HarmonyMethod(typeof(NativeTravelPace), nameof(ObserveTickPostfix)));
                observer = installing;
                return true;
            }
            catch (Exception error)
            {
                installing?.UnpatchAll(ObserverOwner);
                observationError = "Native clock observation unavailable: " + error.Message;
                return false;
            }
        }

        public static void StopObservation()
        {
            observer?.UnpatchAll(ObserverOwner); observer = null;
            ResetReading();
        }

        private static void ObserveTickPostfix(float deltaTimeWithMultiplier)
        {
            try { ObserveTick(deltaTimeWithMultiplier); }
            catch (Exception error) { observationError = "Native clock observation failed: " + error.Message; }
        }

        private static void ObserveTick(float deltaTimeWithMultiplier)
        {
            if (LoadScene.isLoading || InstanceBehavior<UIs>.Instance?.timeMachine == null
                || InstanceBehavior<UIs>.Instance.timeMachine.isRunning || Time.deltaTime <= 0) return;
            tracker.Observe(Time.frameCount, deltaTimeWithMultiplier, Time.deltaTime);
        }

        // PedestrianPool's normal, non-animation-override walking speed in the reviewed game build.
        public const double WalkingMetresPerSecond = 2;
        internal static double InitialClockRate(double configuredFactor)
        {
            if (!Position.Finite(configuredFactor) || configuredFactor < .1 || configuredFactor > 1)
                throw new ArgumentOutOfRangeException(nameof(configuredFactor));
            return NativeClockRate(configuredFactor);
        }
        // Called on detached startup state, before this city's clock policy is active. A loaded
        // supported city must not be retimed against the temporary unmodified native clock.
        internal static void PrepareInitialTiming(CityState state, bool newPopulation, double initialRate)
        {
            if (newPopulation) TravelTiming.Apply(state, WalkingMetresPerSecond, initialRate);
            else
            {
                // Runtime saves always include household balances, even with no managed shops.
                // Food-less pure population fixtures remain valid in the diagnostic codec.
                if (state.Food == null)
                    throw new InvalidOperationException("Missing saved household economy; original payload retained.");
                if (state.TravelTimingVersion != 1)
                    throw new InvalidOperationException("Unsupported saved travel timing; original payload retained.");
            }
        }
        private static double NativeClockRate(double factor)
        {
            if (Multiplier == null || Multiplier.FieldType != typeof(float)) throw new MissingFieldException("GameManager.MinutesMultiplier");
            double rate = (float)Multiplier.GetValue(null)! * factor;
            if (!Position.Finite(rate) || rate < .01 || rate > 1000) throw new InvalidOperationException("Unsupported native clock rate.");
            return rate;
        }
        public static double ClockRate
        {
            get
            {
                if (LastError.Length != 0) throw new InvalidOperationException(LastError);
                if (tracker.Rate.HasValue) return tracker.Rate.Value;
                return NativeClockRate(NativeCityClock.Active ? NativeCityClock.Factor : 1);
            }
        }
        public static TravelVisibility Visibility => new TravelVisibility(PhysicalTrafficHooks.CurrentFastForward, Time.timeScale > 1,
            LoadScene.isLoading, BuildingManager.IsInsideBuilding, UndergroundParkingManager.IsInsideParking,
            InstanceBehavior<UIs>.Instance != null && InstanceBehavior<UIs>.Instance.timeMachine.isRunning);
        public static bool HiddenTravel => PhysicalTrafficHooks.CurrentFastForward || Time.timeScale > 1 || LoadScene.isLoading
            || (BuildingManager.IsInsideBuilding && !NativeInteriorCityView.Active) || UndergroundParkingManager.IsInsideParking
            || (InstanceBehavior<UIs>.Instance != null && InstanceBehavior<UIs>.Instance.timeMachine.isRunning);
        public static Position Focus
        {
            get
            {
                if (NativeInteriorCityView.Active) return NativeInteriorCityView.State.Anchor;
                var player = InstanceBehavior<GameManager>.Instance?.playerController;
                if (player == null) return new Position(double.NaN, double.NaN, double.NaN);
                Vector3 p = player.transform.position;
                return new Position(p.x, p.y, p.z);
            }
        }
    }
}
