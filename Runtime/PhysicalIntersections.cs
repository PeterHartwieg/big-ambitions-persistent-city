using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using GleyTrafficSystem;
using GleyUrbanAssets;
using HarmonyLib;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>Additive signal coverage only. Native clocks and intersection state machines remain authoritative.</summary>
    internal sealed class PhysicalIntersections : IDisposable
    {
        private const string Owner = "hart.persistentcity.physicalintersections.v1";
        // Bound ALL pinned references, including baseline junctions: a camera move cannot turn more
        // than 128 formerly-baseline pins into additional junctions after acquisition was admitted.
        private const int MaximumPinnedJunctions = 128;
        private static PhysicalIntersections? current;
        private static readonly FieldInfo? ManagerField = AccessTools.Field(typeof(TrafficManager), "intersectionManager");
        private static readonly FieldInfo? AllField = AccessTools.Field(typeof(IntersectionManager), "allIntersections");
        private static readonly FieldInfo? ActiveField = AccessTools.Field(typeof(IntersectionManager), "activeIntersections");
        private static readonly FieldInfo? AssociatedField = AccessTools.Field(typeof(Waypoint), "associatedIntersection");
        private static readonly FieldInfo? OccupancyField = AccessTools.Field(typeof(GenericIntersection), "carsInIntersection");
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly Dictionary<GenericIntersection, int> counts = new Dictionary<GenericIntersection, int>(References.Instance);
        private readonly List<Pin> orphans = new List<Pin>();
        private readonly HashSet<GenericIntersection> known = new HashSet<GenericIntersection>(References.Instance);
        private List<GenericIntersection> baseline = new List<GenericIntersection>();
        private TrafficManager? traffic;
        private IntersectionManager? manager;
        private bool ownsRegistration, applying, disposing, disposed;
        public string LastError { get; private set; } = "";
        public bool Ready => !disposing && !disposed && LastError.Length == 0 && SameManager;
        public int BaselineCount => baseline.Count;
        public int PinnedJunctionCount => counts.Count;
        public int AdditionalCount => counts.Keys.Count(j => !baseline.Contains(j));
        public int EffectiveCount => baseline.Concat(counts.Keys).Distinct(References.Instance).Count();
        public int PinReferences => counts.Values.Sum();
        public int OrphanLeases => orphans.Count;
        public bool PendingForeignCleanup => disposing && !disposed;
        public string Diagnostics => "baseline=" + BaselineCount + ", effective=" + EffectiveCount + ", extra=" + AdditionalCount
            + ", pinned=" + PinnedJunctionCount + ", pinReferences=" + PinReferences + ", foreignLeases=" + OrphanLeases
            + ", pendingCleanup=" + PendingForeignCleanup + ", error=" + LastError;
        private bool SameManager => traffic != null && manager != null && TrafficManager.HasInstance
            && ReferenceEquals(TrafficManager.Instance, traffic) && ReferenceEquals(ManagerField?.GetValue(traffic), manager);

        internal sealed class Pin
        {
            internal readonly GenericIntersection[] Junctions;
            internal int NativeIndex = -1;
            internal bool Released;
            internal Pin(GenericIntersection[] junctions) { Junctions = junctions; }
        }
        public PhysicalIntersections()
        {
            // A destroyed Unity manager cannot drive any old car; remove its exact stale overlay before reloading.
            if (current != null && current.manager == null) current.Teardown(false);
            if (current != null || Harmony.HasAnyPatches(Owner))
            { LastError = "Another signal coverage owner is present; full game reload/restart may be required."; return; }
            try
            {
                Shape(ManagerField, typeof(IntersectionManager)); Shape(AllField, typeof(GenericIntersection[]));
                Shape(ActiveField, typeof(List<GenericIntersection>)); Shape(AssociatedField, typeof(IIntersection));
                Shape(OccupancyField, typeof(List<int>));
                if (!TrafficManager.IsInitialized) throw new InvalidOperationException("Native traffic is not initialized.");
                traffic = TrafficManager.Instance; manager = ManagerField!.GetValue(traffic) as IntersectionManager;
                if (manager == null) throw new InvalidOperationException("Native intersection manager is absent.");
                var all = AllField!.GetValue(manager) as GenericIntersection[];
                var active = ActiveField!.GetValue(manager) as List<GenericIntersection>;
                if (all == null || active == null || all.Any(j => j == null)) throw new InvalidOperationException("Native intersection lists are invalid.");
                foreach (var junction in all)
                {
                    if (!known.Add(junction) || !(OccupancyField!.GetValue(junction) is List<int>))
                        throw new InvalidOperationException("Native intersection references/occupancy are invalid.");
                }
                if (active.Any(j => j == null || !known.Contains(j)) || active.Distinct(References.Instance).Count() != active.Count)
                    throw new InvalidOperationException("Native active intersection baseline is invalid.");
                baseline = new List<GenericIntersection>(active);
                var setter = AccessTools.Method(typeof(IntersectionManager), nameof(IntersectionManager.SetActiveIntersection), new[] { typeof(List<GenericIntersection>) });
                var update = AccessTools.Method(typeof(IntersectionManager), nameof(IntersectionManager.UpdateIntersections), Type.EmptyTypes);
                if (setter == null || update == null) throw new MissingMethodException("Native intersection coverage/update contract.");
                current = this; ownsRegistration = true;
                harmony.Patch(setter, prefix: new HarmonyMethod(typeof(PhysicalIntersections), nameof(SetActivePrefix)));
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(PhysicalIntersections), nameof(UpdatePostfix)));
                Debug.Log("[Persistent City] Signal coverage hooks: Active (128 pinned junction maximum).");
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                if (ownsRegistration) { harmony.UnpatchAll(Owner); current = null; ownsRegistration = false; }
                Debug.Log("[Persistent City] Signal coverage hooks: Not active: " + LastError);
            }
        }
        private static void Shape(FieldInfo? field, Type type)
        { if (field == null || field.FieldType != type) throw new MissingFieldException("Native signal field contract: " + (field?.Name ?? type.Name)); }

        public bool TryPin(IEnumerable<Waypoint> route, out Pin? pin)
        {
            pin = null; if (!Ready) return false;
            var needed = new HashSet<GenericIntersection>(References.Instance);
            foreach (var waypoint in route)
            {
                if (waypoint == null) return false;
                var associated = AssociatedField!.GetValue(waypoint);
                if (associated == null)
                {
                    if (waypoint.enter || waypoint.exit || waypoint.stop) return false;
                    continue;
                }
                if (!(associated is GenericIntersection junction) || !known.Contains(junction)) return false;
                needed.Add(junction);
            }
            if (counts.Keys.Concat(needed).Distinct(References.Instance).Count() > MaximumPinnedJunctions) return false;
            var lease = new Pin(needed.ToArray());
            foreach (var junction in lease.Junctions) counts[junction] = counts.TryGetValue(junction, out int n) ? n + 1 : 1;
            try { Apply(); pin = lease; return true; }
            catch (Exception error)
            {
                Decrement(lease); LastError = "Signal pin application failed: " + error.Message;
                try { Apply(); } catch (Exception rollback) { LastError += "; restoration failed: " + rollback.Message + "; full game reload/restart required."; }
                Debug.LogError("[Persistent City] " + LastError); return false;
            }
        }
        public void Release(Pin? pin, bool foreign)
        {
            if (pin == null || pin.Released) return;
            if (foreign && Occupied(pin))
            {
                // A native service now owns this pool slot. Never remove it or stop its signals.
                if (!orphans.Contains(pin)) orphans.Add(pin);
                return;
            }
            Decrement(pin); if (SameManager) Apply();
        }
        private bool Occupied(Pin pin)
        {
            if (pin.NativeIndex < 0) return false;
            foreach (var junction in pin.Junctions)
            {
                if (!(OccupancyField!.GetValue(junction) is List<int> cars))
                    throw new InvalidOperationException("Native signal occupancy list became unavailable.");
                if (cars.Contains(pin.NativeIndex)) return true;
            }
            return false;
        }
        public void ReportRemovalFailure()
        {
            LastError = "Native vehicle removal failed; its signal pins remain held. Full game reload/restart required.";
            Debug.LogError("[Persistent City] " + LastError);
        }
        private void Decrement(Pin pin)
        {
            if (pin.Released) return; pin.Released = true;
            foreach (var junction in pin.Junctions)
                if (counts.TryGetValue(junction, out int n)) { if (n <= 1) counts.Remove(junction); else counts[junction] = n - 1; }
            orphans.Remove(pin);
        }
        private void Apply()
        {
            if (!SameManager) return;
            var effective = baseline.Concat(counts.Keys).Distinct(References.Instance).ToList();
            applying = true;
            try { manager!.SetActiveIntersection(effective); }
            finally { applying = false; }
        }
        private static void SetActivePrefix(IntersectionManager __instance, ref List<GenericIntersection> activeIntersections)
        {
            var owner = current;
            if (owner == null || owner.disposed || owner.applying || !ReferenceEquals(__instance, owner.manager)) return;
            // Never mutate the grid manager's own baseline list or retain a caller-owned collection.
            owner.baseline = new List<GenericIntersection>(activeIntersections);
            activeIntersections = owner.baseline.Concat(owner.counts.Keys).Distinct(References.Instance).ToList();
        }
        private static void UpdatePostfix(IntersectionManager __instance)
        {
            var owner = current;
            if (owner == null || owner.disposed || !ReferenceEquals(__instance, owner.manager)) return;
            try
            {
                bool changed = false;
                foreach (var orphan in owner.orphans.ToArray())
                    if (!owner.Occupied(orphan)) { owner.Decrement(orphan); changed = true; }
                if (owner.disposing && owner.counts.Count == 0) owner.Teardown(true);
                else if (changed) owner.Apply();
            }
            catch (Exception error)
            {
                // This callback runs in native traffic, outside the population's recovery boundary.
                // Record the failure; the next owned Update fails through that guarded boundary.
                if (owner.LastError.Length == 0)
                {
                    owner.LastError = "Native signal cleanup failed: " + error.Message + "; full game reload/restart required.";
                    Debug.LogError("[Persistent City] " + owner.LastError);
                }
            }
        }
        public void Dispose()
        {
            if (disposed || disposing) return;
            disposing = true;
            if (!SameManager) { Teardown(false); return; }
            if (counts.Count == 0) { Teardown(true); return; }
            LastError = "Signal coverage retained for foreign/native occupants or unreleased leases; full game reload/restart required if cleanup does not complete.";
            Debug.LogError("[Persistent City] " + LastError);
            throw new InvalidOperationException(LastError);
        }
        private void Teardown(bool restore)
        {
            if (disposed) return;
            // All vehicle removals happen before dropping their coverage. Do not restore over an occupied orphan.
            if (restore && SameManager)
            {
                applying = true;
                try { manager!.SetActiveIntersection(new List<GenericIntersection>(baseline)); }
                finally { applying = false; }
            }
            if (ownsRegistration) { harmony.UnpatchAll(Owner); ownsRegistration = false; if (ReferenceEquals(current, this)) current = null; }
            counts.Clear(); orphans.Clear(); disposed = true;
        }
        private sealed class References : IEqualityComparer<GenericIntersection>
        {
            internal static readonly References Instance = new References();
            public bool Equals(GenericIntersection? a, GenericIntersection? b) => ReferenceEquals(a, b);
            public int GetHashCode(GenericIntersection value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
