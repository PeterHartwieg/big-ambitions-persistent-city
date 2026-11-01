using System;
using BAModAPI;
using BigAmbitions.InteriorDesigner.InteriorElements;
using Entities;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentCity.Runtime
{
    // Read-only adapter: owns no native transform, renderer, volume, camera or culling state.
    internal static class NativeInteriorCityView
    {
        internal static readonly InteriorViewState State = new InteriorViewState();
        private static readonly Func<InteriorViewEligibility> ReadEligibility = Inspect;
        private static PrototypeSettings? settings;
        private static IModLogger? log;
        private static object? building;
        private static Transform? layout;
        private static Position? cachedAnchor;
        private static Transform? cachedFloor;
        private static Action? restoreBodies;
        private static bool fallbackReported, activeReported;
        private static int suspendedFrame = -1, refreshedFrame = -1;
        public static bool Configured { get; private set; }

        public static void Start(PrototypeSettings loaded, IModLogger logger, Action restore)
        {
            Stop();
            settings = loaded; log = logger; restoreBodies = restore;
            NativeInteriorGuard.Start(loaded);
            Configured = NativeInteriorGuard.Verdict.AllowsView(loaded.InteriorCityView == "on");
            if (Configured && !NativeGameSpeed.Prepare())
            {
                Configured = false;
                log.Warn("InteriorCityView hidden fallback: native speed accessor unavailable; feature disabled for this city.");
            }
            log.Info("ThirdPartyInteriorGuard: " + (NativeInteriorGuard.Verdict.Detected ? "detected" : "not detected")
                + "; " + NativeInteriorGuard.Verdict.Reason + "; effective view=" + (Configured ? "on" : "off") + ".");
            if (!Configured) return;
            GlobalEvents.onEnterBuilding += OnBuildingChanged;
            GlobalEvents.onExitBuilding += OnBuildingChanged;
            GlobalEvents.onTimeMachineStarted += OnTimeMachineStarted;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        private static void OnSceneUnloaded(Scene scene)
        {
            if (!State.Active && !BuildingManager.IsInsideBuilding) return;
            try { restoreBodies?.Invoke(); }
            finally { ResetEntry(); Suspend(); }
        }
        private static void OnTimeMachineStarted()
        {
            // Vanilla raises this synchronously in StartTimeMachine, before its first Update tick.
            try { restoreBodies?.Invoke(); }
            finally { Suspend(); }
        }
        private static void OnBuildingChanged(Address address)
        {
            try { if (State.Active) restoreBodies?.Invoke(); }
            finally { ResetEntry(); }
        }
        private static void ResetEntry()
        {
            State.Reset(); building = null; layout = null; cachedAnchor = null; cachedFloor = null;
            fallbackReported = activeReported = false;
            refreshedFrame = -1;
        }
        public static void Stop()
        {
            NativeInteriorGuard.Stop();
            GlobalEvents.onEnterBuilding -= OnBuildingChanged;
            GlobalEvents.onExitBuilding -= OnBuildingChanged;
            GlobalEvents.onTimeMachineStarted -= OnTimeMachineStarted;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            ResetEntry(); settings = null; log = null; restoreBodies = null; suspendedFrame = -1; Configured = false;
        }
        public static void Suspend() { State.Suspend(); suspendedFrame = Time.frameCount; refreshedFrame = -1; }
        public static void FailClosed() { State.FailClosed(); suspendedFrame = Time.frameCount; refreshedFrame = -1; }

        public static bool Active
        {
            get { Refresh(); return State.Active; }
        }
        private static void Refresh()
        {
            if (!Configured) return;
            try
            {
                var travel = NativeTravelPace.Visibility;
                // Only these non-reflective safety reads repeat within a frame: blockers may
                // change after an earlier body query, before Unity increments frameCount.
                if (travel.OtherHidden) { Suspend(); return; }
                var manager = InstanceBehavior<BuildingManager>.Instance;
                if (!travel.Inside || manager == null || manager.building == null)
                { if (building != null || State.Active) ResetEntry(); return; }
                if (suspendedFrame == Time.frameCount || manager.enteringBuilding || manager.exitingBuilding)
                { State.Suspend(); return; }
                if (refreshedFrame == Time.frameCount) return;
                if (!ReferenceEquals(building, manager.building) || layout != manager.currentBuildingVersion)
                {
                    ResetEntry(); building = manager.building; layout = manager.currentBuildingVersion;
                }
                refreshedFrame = Time.frameCount;
                State.Refresh(true, travel, building, Time.unscaledTime, ReadEligibility);
                if (State.Active && !activeReported)
                {
                    activeReported = true;
                    log?.Info("InteriorCityView active: " + manager.building.Address + ", entrance anchor=("
                        + State.Anchor.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                        + State.Anchor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                        + State.Anchor.Z.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
                        + ", radius=" + Radius(settings!.PhysicalRadiusMetres) + "m, caps="
                        + State.Pedestrians(settings.MaximumPedestrians, settings.InteriorMaximumPedestrians) + "/"
                        + State.Vehicles(settings.MaximumVehicles, settings.InteriorMaximumVehicles) + "; drawing=no-op.");
                }
                else if (State.Fallback != InteriorViewFallback.None && !fallbackReported)
                {
                    fallbackReported = true;
                    log?.Warn("InteriorCityView hidden fallback: " + State.Fallback + " at " + manager.building.Address + ".");
                }
            }
            catch
            {
                State.FailClosed();
                if (!fallbackReported) { fallbackReported = true; log?.Warn("InteriorCityView hidden fallback: exception; closed until exit."); }
            }
        }
        private static InteriorViewEligibility Inspect()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null) return new InteriorViewEligibility(default, InteriorViewFallback.Uncertain);
            var controller = manager.cityBuildingController;
            if (manager.building == null || manager.currentBuildingVersion == null || controller == null
                || controller.building != manager.building || controller.entranceDoors == null || controller.entranceDoors.Length == 0
                || manager.exitZones == null || manager.exitZones.Count == 0)
                return new InteriorViewEligibility(default, InteriorViewFallback.Uncertain);
            var door = controller.entranceDoors[0].doorTransform;
            if (door == null) return new InteriorViewEligibility(default, InteriorViewFallback.Uncertain);
            bool shop = manager.building.BuildingType == "ba:buildingtype_retail"
                && manager.buildingRegistration != null && manager.buildingRegistration.HasEstablishedBusiness
                && !string.IsNullOrEmpty(manager.buildingRegistration.businessTypeName)
                && manager.buildingRegistration.businessTypeName != "ba:businesstype_empty"
                && GamePlaces.IsRetailShopActivity(manager.buildingRegistration.businessTypeName);
            bool elevator = manager.multipleHeightsBuildingController != null;
            foreach (var exit in manager.exitZones)
            {
                if (exit == null) return new InteriorViewEligibility(default, InteriorViewFallback.Uncertain);
                elevator |= exit.isElevator;
            }
            if (!cachedAnchor.HasValue)
            {
                Vector3 p = door.position;
                cachedAnchor = new Position(p.x, p.y, p.z);
            }
            // The vanilla floor-element cache is populated by LoadBuilding. Never instantiate a
            // layout or infer ground floor from an address/name. Missing floor evidence closes.
            double floor = double.NaN;
            if (cachedFloor == null)
                foreach (var element in InteriorElementsHelper.InteriorElementsCache.Values)
                    if (element != null && element.IsFloor && element.transform.IsChildOf(manager.currentBuildingVersion))
                    { cachedFloor = element.transform; break; }
            if (cachedFloor != null && cachedFloor.IsChildOf(manager.currentBuildingVersion)) floor = cachedFloor.position.y;
            return InteriorViewEligibility.Check(shop, elevator, cachedAnchor.Value, floor);
        }
        public static double Radius(double street) => State.Radius(street, settings?.InteriorViewRadiusMetres ?? 60);
        public static double AdmissionRadius(double street) => State.AdmissionRadius(street, settings?.InteriorViewRadiusMetres ?? 60);
        public static double ReleaseRadius(double street) => State.ReleaseRadius(street, settings?.InteriorViewRadiusMetres ?? 60);
        public static int Pedestrians(int global) => State.Pedestrians(global, settings?.InteriorMaximumPedestrians ?? 24);
        public static int Vehicles(int global) => State.Vehicles(global, settings?.InteriorMaximumVehicles ?? 12);
    }
}
