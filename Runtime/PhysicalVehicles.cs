using System;
using System.Collections.Generic;
using System.Linq;
using GleyTrafficSystem;
using UI;
using Helpers;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal readonly struct PhysicalRoadSample
    {
        public PhysicalRoadSample(Position position, Position directionTarget, int spawnNode, IReadOnlyList<int> remainingRoadNodes, string topologyId, IReadOnlyList<RoadBoundary> admissionBoundaries, double routeProgress)
        { AdmissionBoundaries = admissionBoundaries; RouteProgress = routeProgress; Position = position; DirectionTarget = directionTarget; SpawnNode = spawnNode; RemainingRoadNodes = remainingRoadNodes; TopologyId = topologyId; }
        public IReadOnlyList<RoadBoundary> AdmissionBoundaries { get; }
        public double RouteProgress { get; }
        public Position Position { get; }
        public Position DirectionTarget { get; }
        public int SpawnNode { get; }
        public IReadOnlyList<int> RemainingRoadNodes { get; }
        public string TopologyId { get; }
    }

    internal sealed class PhysicalVehicleBody
    {
        internal PhysicalVehicleBody(string id, string citizenId, long generation, VehicleComponent vehicle, List<int> path, Position origin, PhysicalIntersections.Pin signalPin, Transform frontMarker)
        { FrontMarker = frontMarker; Id = id; CitizenId = citizenId; Generation = generation; Vehicle = vehicle; Path = path; LastTargetOrigin = origin;
          NativeIndex = vehicle.GetIndex(); NativePrefab = vehicle.prefab; NativeRigidbody = vehicle.rb; SignalPin = signalPin; }
        internal readonly Transform FrontMarker;
        internal readonly VehicleComponent Vehicle;
        internal readonly PhysicalIntersections.Pin SignalPin;
        internal readonly int NativeIndex;
        internal readonly GameObject NativePrefab;
        internal readonly Rigidbody NativeRigidbody;
        internal readonly List<int> Path;
        internal Position LastTargetOrigin;
        internal bool Released;
        private readonly Queue<(Position Corner, Position Marker)> reachedCorners = new Queue<(Position, Position)>();
        private int lastReachedNode = -1;
        internal float LastCollisionFixedTime = float.NegativeInfinity;
        internal bool PendingNativeForce;
        internal Vector3 NativeForce;
        internal float NativeForceFixedTime;
        internal long NativeForceStep;
        internal bool NativeStepPaired;
        internal bool NativeStepPairValid;
        internal Quaternion NativeRotation;
        public string StopRecoveryDeferredReason { get; internal set; } = "";
        internal void RecordReached(int node, Position corner)
        {
            // Cache only the most recent measured target, not lifetime node membership: a
            // committed route may return to this node after reaching another node in between.
            if (node == lastReachedNode) return;
            if (reachedCorners.Count < 512) reachedCorners.Enqueue((corner, PhysicalVehicles.Point(FrontMarker.position)));
            lastReachedNode = node;
        }
        internal bool HasRecordedReached(int node) => node == lastReachedNode;
        public bool TryPeekReached(Position currentTo, out Position measuredMarker)
        {
            measuredMarker = default;
            if (!Valid || reachedCorners.Count == 0 || PersistentCity.Core.Position.Distance(reachedCorners.Peek().Corner, currentTo) > .01) return false;
            measuredMarker = reachedCorners.Peek().Marker; return measuredMarker.IsFinite;
        }
        public bool ConsumeReached(Position currentTo)
        {
            if (!TryPeekReached(currentTo, out _)) return false;
            reachedCorners.Dequeue(); return true;
        }
        public string Id { get; }
        public string CitizenId { get; }
        public long Generation { get; }
        public string OwnershipLossReason
        {
            get
            {
                if (Released) return "released";
                if (Vehicle == null || !Vehicle.gameObject.activeInHierarchy || Vehicle.rb == null) return "native-body-inactive";
                if (Vehicle.GetIndex() != NativeIndex || !ReferenceEquals(Vehicle.prefab, NativePrefab)) return "native-slot-or-prefab-changed";
                if (!ReferenceEquals(Vehicle.rb, NativeRigidbody)) return "native-rigidbody-changed";
                if (FrontMarker == null || !FrontMarker.IsChildOf(Vehicle.transform) || !ReferenceEquals(Vehicle.GetFrontAxle(), FrontMarker)) return "native-front-marker-changed";
                if (!PhysicalVehicles.Point(FrontMarker.position).IsFinite) return "native-front-marker-invalid";
                if (!PhysicalVehicles.RandomAmbient(Vehicle)) return "native-service-or-foreign-component";
                if (!ReferenceEquals(Vehicle.presetPath, Path)) return "native-path-replaced";
                return "";
            }
        }
        public bool Valid => OwnershipLossReason.Length == 0;
        public Position Position => Valid ? PhysicalVehicles.Point(FrontMarker.position) : LastTargetOrigin;
        public bool RouteFinished { get; internal set; }
    }

    /// <summary>Disposable native pool leases; no VehicleInstance or native save ownership is created.</summary>
    internal sealed class PhysicalVehicles : IDisposable
    {
        private readonly Func<string, int, Waypoint?> resolve;
        private readonly Dictionary<int, PhysicalVehicleBody> bodies = new Dictionary<int, PhysicalVehicleBody>();
        private readonly Dictionary<Rigidbody, PhysicalVehicleBody> forceBodies = new Dictionary<Rigidbody, PhysicalVehicleBody>();
        private readonly int maximum;
        private readonly PhysicalTrafficHooks hooks;
        private readonly PhysicalIntersections intersections;
        public string SignalDiagnostics => intersections.Diagnostics;
        private long generation;
        private long foreignRelinquishments, unexpectedPoolReturns, expiredStopRecoveries, deferredStopRecoveries;
        private string recoveryError = "";
        public string OwnershipDiagnostics => "foreignRelinquishments=" + foreignRelinquishments + ", unexpectedPoolReturns=" + unexpectedPoolReturns + ", expiredStopRecoveries=" + expiredStopRecoveries
            + ", deferredStopRecoveries=" + deferredStopRecoveries;
        private bool active, disposed;
        private int retireCursor;
        private int taxiTarget;
        private bool nativeStepActive;
        private long nativeStep;
        internal Action? SuspendForNativeFastForward { get; set; }
        internal Func<PhysicalVehicleBody, PhysicalTrafficCommandLimit>? CommandLimit { get; set; }
        internal Func<PhysicalVehicleBody, int, bool>? NativeTargetPermission { get; set; }
        public PhysicalVehicles(Func<string, int, Waypoint?> resolveNode, int max = 24)
        { resolve = resolveNode; maximum = Math.Max(1, Math.Min(64, max)); intersections = new PhysicalIntersections(); hooks = new PhysicalTrafficHooks(this); }
        public bool Ready => hooks.Ready && intersections.Ready;
        public bool Active => active && !disposed;
        public string LastAdmissionStage { get; private set; } = "";
        public string LastAdmissionResident { get; private set; } = "";
        public string LastAdmissionError { get; private set; } = "";
        public string LastError => recoveryError.Length != 0 ? recoveryError : hooks.LastError.Length != 0 ? hooks.LastError : intersections.LastError;
        public bool Activate()
        {
            if (!Ready || disposed || !TrafficManager.IsInitialized) return false;
            if (active) return true;
            var taxis = Manager.GetVehicleList().Where(IsTaxi).ToArray();
            int present = taxis.Count(v => v.gameObject.activeInHierarchy);
            taxiTarget = present > 0 ? present : Math.Min(2, taxis.Length);
            active = true; return true;
        }
        internal static bool IsTaxi(VehicleComponent vehicle) => vehicle != null
            && vehicle.GetComponentInChildren<TaxiController>(true) != null;
        internal bool TaxiNeedsReplenishment => Active && TrafficManager.IsInitialized
            && Manager.GetVehicleList().Count(v => IsTaxi(v) && v.gameObject.activeInHierarchy) < taxiTarget;
        public void Disable()
        {
            active = false;
            Exception? failure = null;
            foreach (var body in bodies.Values.ToArray())
                try { Release(body); } catch (Exception error) { failure = failure ?? error; }
            if (failure != null) throw new InvalidOperationException("One or more native vehicle slots could not be released.", failure);
        }
        internal void BeforeNativeFastForward()
        {
            if (!Active) return;
            try
            {
                if (SuspendForNativeFastForward == null) throw new InvalidOperationException("Physical population suspension bridge is absent.");
                SuspendForNativeFastForward();
            }
            catch (Exception error)
            {
                // Harmony callback cannot replace native speed handling. Retain failure for the
                // population's existing adapter failure/uncertain-cleanup path on its next update.
                if (recoveryError.Length == 0) recoveryError = "Native fast-forward suspension failed: " + error.Message;
                Debug.LogError("[Persistent City] " + recoveryError);
            }
        }
        internal static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
        internal static Vector3 Vector(Position p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static bool NativeReady => TrafficManager.IsInitialized && TrafficManager.HasInstance
            && TrafficManager.Instance.enabled && !BuildingManager.IsInsideBuilding && Time.timeScale > 0;
        internal PhysicalVehicleBody? Owned(int index) => bodies.TryGetValue(index, out var body) && body.Valid ? body : null;
        internal bool Owns(GameObject obj) => obj != null && obj.GetComponentInParent<VehicleComponent>() is VehicleComponent vehicle && Owned(vehicle.GetIndex()) != null;
        internal static bool Ordinary(VehicleComponent vehicle) => vehicle != null && vehicle.vehicleType == VehicleTypes.Car && RandomAmbient(vehicle);
        internal static bool RandomAmbient(VehicleComponent vehicle)
        {
            // Both native Car and Truck enum values can be ordinary ambient traffic. Model eligibility
            // for citizen cars remains narrower; a truck must not become a protected service by its enum.
            if (vehicle == null || vehicle.prefab == null) return false;
            // Component ownership, not model names: never borrow a service/taxi instance.
            return vehicle.GetComponentInChildren<TaxiController>(true) == null
                && vehicle.GetComponentInChildren<PrivateDriverVehicle>(true) == null
                && vehicle.GetComponentInChildren<VehicleController>(true) == null
                && VehicleScriptOwnership.NativeOnly(vehicle);
        }
        private static uint Hash(string id)
        { uint value = 2166136261; foreach (char c in id) value = (value ^ c) * 16777619; return value; }

        public bool TryAcquire(CitizenState citizen, PhysicalRoadSample sample, out PhysicalVehicleBody body)
        {
            body = null!;
            LastAdmissionResident = citizen.Identity.Id; LastAdmissionError = "";
            LastAdmissionStage = "native availability";
            if (!Active || !NativeReady || citizen.Car == null || bodies.Count >= maximum || !sample.Position.IsFinite
                || !sample.DirectionTarget.IsFinite || sample.RemainingRoadNodes == null || sample.RemainingRoadNodes.Count == 0 || sample.RemainingRoadNodes.Count > 512) return false;
            if (bodies.Values.Any(b => b.Id == citizen.Car.Id)) return false;
            LastAdmissionStage = "committed road validation";
            var path = new List<int>(sample.RemainingRoadNodes);
            Waypoint? first = null;
            for (int i = 0; i < path.Count; i++)
            {
                var node = resolve(sample.TopologyId, path[i]);
                if (node == null || node.temporaryDisabled) return false;
                if (i == 0) first = node;
                else { var previous = resolve(sample.TopologyId, path[i - 1]); if (previous?.neighbors == null || !previous.neighbors.Contains(path[i])) return false; }
            }
            var spawn = resolve(sample.TopologyId, sample.SpawnNode);
            if (spawn == null || spawn.neighbors == null || !spawn.neighbors.Contains(path[0])) return false;
            var segment = first!.position - spawn.position;
            var offset = Vector(sample.Position) - spawn.position;
            if (segment.sqrMagnitude < 0.001f) return false;
            float fraction = Vector3.Dot(offset, segment) / segment.sqrMagnitude;
            if (fraction < -0.001f || fraction > 1.001f || (offset - segment * fraction).sqrMagnitude > 0.25f) return false;
            LastAdmissionStage = "model catalogue";
            var all = Manager.GetVehicleList();
            var models = all.Where(Ordinary).Select(v => v.prefab.name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (models.Length == 0) return false;
            if (string.IsNullOrEmpty(citizen.Car.ModelId)) citizen.Car.ModelId = models[Hash(citizen.Car.Id) % (uint)models.Length];
            if (citizen.Car.PaintSeed == 0) citizen.Car.PaintSeed = 1 + (int)(Hash(citizen.Car.Id + ":paint") % 2147483646u);
            LastAdmissionStage = "inactive model slot";
            var slot = all.FirstOrDefault(v => Ordinary(v) && !v.gameObject.activeInHierarchy && v.prefab.name == citizen.Car.ModelId);
            if (slot == null || bodies.ContainsKey(slot.GetIndex())) return false; // Never invoke LoadVehicle's active-name fallback.
            // LoadVehicle chooses the first inactive exact-prefab slot, including protected slots.
            // Reject that ordering conflict rather than activate a service body and roll it back later.
            var nativeChoice = all.FirstOrDefault(v => v != null && ReferenceEquals(v.prefab, slot.prefab) && !v.gameObject.activeInHierarchy);
            if (!ReferenceEquals(nativeChoice, slot)) return false;
            LastAdmissionStage = "junction rear clearance";
            if (!PhysicalRoadAdmission.Allows(sample.AdmissionBoundaries, sample.RouteProgress, slot.length)) return false;
            LastAdmissionStage = "native body geometry";
            var direction = Vector(sample.DirectionTarget) - Vector(sample.Position); direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return false;
            var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var frontMarker = slot.GetFrontAxle();
            if (frontMarker == null || !frontMarker.IsChildOf(slot.transform) || slot.rb == null) return false;
            var markerOffset = Quaternion.Inverse(slot.transform.rotation) * (frontMarker.position - slot.transform.position);
            if (!Point(markerOffset).IsFinite || markerOffset.sqrMagnitude > 400f || !Point(slot.transform.position).IsFinite
                || float.IsNaN(slot.length) || float.IsInfinity(slot.length) || slot.length <= 0
                || float.IsNaN(slot.colliderWidth) || float.IsInfinity(slot.colliderWidth) || slot.colliderWidth <= 0
                || float.IsNaN(slot.coliderHeight) || float.IsInfinity(slot.coliderHeight) || slot.coliderHeight <= 0) return false;
            // Native positioning steers the front axle. Align only XZ; keep native road/root height.
            var rootPosition = Vector(PhysicalVehicleReference.Root(sample.Position, Point(markerOffset), Point(direction)));
            // Deliberately conservative root-centred proxy: a full native length in both
            // directions covers the rear even when the front marker lies beyond the pivot.
            var clearance = new Vector3(Math.Max(0.5f, slot.colliderWidth + Math.Abs(markerOffset.x)),
                Math.Max(0.3f, slot.coliderHeight / 2f - 0.15f), Math.Max(1f, slot.length + Math.Abs(markerOffset.z)));
            int mask = LayerMask.GetMask("Vehicles", "AiVehicles", "PlayerVehicles", "Obstacles", "Default");
            LastAdmissionStage = "spawn obstruction";
            if (mask == 0 || Physics.CheckBox(rootPosition + Vector3.up * (slot.coliderHeight / 2f + 0.2f),
                clearance, rotation, mask, QueryTriggerInteraction.Ignore)) return false;
            LastAdmissionStage = "intersection ownership";
            var pinnedRoute = new List<Waypoint> { spawn };
            foreach (int index in path)
            {
                var waypoint = resolve(sample.TopologyId, index);
                if (waypoint == null) return false;
                pinnedRoute.Add(waypoint);
            }
            if (!intersections.TryPin(pinnedRoute, out var pin) || pin == null) return false;
            VehicleComponent? loaded = null;
            bool transferredPin = false, rollbackRemovalFailed = false;
            try
            {
                LastAdmissionStage = "native activation";
                loaded = TrafficManager.Instance.LoadVehicle(slot.prefab, spawn, true);
                if (loaded == null) return false;
                pin.NativeIndex = loaded.GetIndex();
                if (!ReferenceEquals(slot, loaded)) { unexpectedPoolReturns++; throw new InvalidOperationException("Native traffic pool returned an unexpected slot."); }
                // Native activation emits synchronous callbacks. A service/mod can claim the body there.
                if (!RandomAmbient(loaded) || loaded.presetPath != null) { foreignRelinquishments++; return false; }
                var loadedMarker = loaded.GetFrontAxle();
                if (!ReferenceEquals(loadedMarker, frontMarker) || loadedMarker == null || !loadedMarker.IsChildOf(loaded.transform))
                    throw new InvalidOperationException("Native front marker changed during activation.");
                var loadedOffset = Quaternion.Inverse(loaded.transform.rotation) * (loadedMarker.position - loaded.transform.position);
                if (!Point(loadedOffset).IsFinite || (loadedOffset - markerOffset).sqrMagnitude > 0.000001f)
                    throw new InvalidOperationException("Native front marker offset changed during activation.");
                loaded.rb.position = rootPosition; loaded.rb.rotation = rotation;
                loaded.transform.SetPositionAndRotation(rootPosition, rotation);
                var placedMarker = Point(frontMarker.position);
                if (!placedMarker.IsFinite || Math.Abs(placedMarker.X - sample.Position.X) > .001 || Math.Abs(placedMarker.Z - sample.Position.Z) > .001)
                    throw new InvalidOperationException("Native front marker did not align with road progress.");
                loaded.rb.velocity = Vector3.zero; loaded.rb.angularVelocity = Vector3.zero;
                loaded.presetPath = path;
                body = new PhysicalVehicleBody(citizen.Car.Id, citizen.Identity.Id, ++generation, loaded, path, sample.Position, pin, frontMarker);
                bodies.Add(loaded.GetIndex(), body); transferredPin = true;
                forceBodies.Add(loaded.rb, body);
                var colors = InstanceBehavior<GlobalReferences>.Instance.vehicleColors;
                var palette = colors.Where(c => c != null).OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
                if (palette.Length > 0)
                    foreach (var features in loaded.GetComponentsInChildren<CarFeatures>(true)) features.SetColor(palette[(uint)citizen.Car.PaintSeed % (uint)palette.Length]);
                TrafficManager.Instance.SetVehicleTargetWaypoint(loaded, first!);
                // VehicleActivated already defaults to Forward; resetting it here clears native stop/give-way actions.
                if (!body.Valid) throw new InvalidOperationException("Native vehicle ownership changed during activation.");
                LastAdmissionStage = "admitted";
                return true;
            }
            catch (Exception error)
            {
                LastAdmissionError = error.GetType().Name + ": " + error.Message;
                // LoadVehicle can activate a slot and then throw from a synchronous native/mod callback.
                if (loaded == null && slot.gameObject.activeInHierarchy) { loaded = slot; pin.NativeIndex = slot.GetIndex(); }
                if (body != null) Release(body);
                else if (loaded != null && ReferenceEquals(slot, loaded) && loaded.gameObject.activeInHierarchy
                    && RandomAmbient(loaded) && loaded.presetPath == null)
                {
                    try { Manager.RemoveVehicle(loaded.GetIndex()); }
                    catch { rollbackRemovalFailed = true; intersections.ReportRemovalFailure(); throw; }
                }
                body = null!; return false;
            }
            finally
            {
                if (!transferredPin && !rollbackRemovalFailed) intersections.Release(pin, loaded != null && loaded.gameObject.activeInHierarchy
                    && (!ReferenceEquals(slot, loaded) || !RandomAmbient(loaded) || loaded.presetPath != null));
            }
        }
        public void Release(PhysicalVehicleBody body)
        {
            if (body == null || body.Released) return;
            string loss = body.OwnershipLossReason;
            if (loss == "native-service-or-foreign-component" || loss == "native-slot-or-prefab-changed" || loss == "native-path-replaced") foreignRelinquishments++;
            bool owned = body.Valid && bodies.TryGetValue(body.NativeIndex, out var found) && ReferenceEquals(body, found);
            body.Released = true;
            body.PendingNativeForce = false;
            if (forceBodies.TryGetValue(body.NativeRigidbody, out var forceBody)
                && ReferenceEquals(forceBody, body)) forceBodies.Remove(body.NativeRigidbody);
            if (bodies.TryGetValue(body.NativeIndex, out var registered) && ReferenceEquals(body, registered)) bodies.Remove(body.NativeIndex);
            bool removed = !owned || !TrafficManager.IsInitialized;
            try
            {
                if (owned && body.Vehicle != null && TrafficManager.IsInitialized)
                { body.Vehicle.presetPath = null; Manager.RemoveVehicle(body.Vehicle.GetIndex()); removed = true; }
            }
            finally
            {
                // Keep signals available through the native intersection removal. A foreign service
                // retains its pins until its native occupancy clears; never despawn it on our release.
                if (removed) intersections.Release(body.SignalPin, !owned);
                else intersections.ReportRemovalFailure();
            }
        }
        internal void AssertHealthy()
        {
            if (!intersections.Ready || recoveryError.Length != 0) throw new InvalidOperationException("Physical native adapter failed: " + LastError);
        }
        internal void BeginNativeFixedStep()
        {
            if (!Active) return;
            if (nativeStepActive && recoveryError.Length == 0) recoveryError = "Native traffic fixed steps overlapped.";
            nativeStepActive = true; nativeStep++;
            foreach (var body in bodies.Values)
            {
                if (body.PendingNativeForce && recoveryError.Length == 0) recoveryError = "Native traffic force was left unpaired.";
                body.PendingNativeForce = false; body.NativeStepPaired = false; body.NativeStepPairValid = false;
            }
        }
        internal void EndNativeFixedStep(bool failed = false)
        {
            if (!nativeStepActive) return;
            try
            {
                if (failed && recoveryError.Length == 0) recoveryError = "Native traffic fixed step failed.";
                if (recoveryError.Length == 0 && bodies.Values.Any(body => body.Valid && body.PendingNativeForce))
                    recoveryError = "Native traffic force was left unpaired.";
                foreach (var body in bodies.Values.ToArray())
                {
                    if (!body.Valid) continue;
                    try { ApplyNativeCommand(body); }
                    catch (Exception error)
                    {
                        // A Unity API exception may occur after it queues a command. Do not append
                        // an uncertain compensating force; close the adapter and guard other bodies.
                        if (recoveryError.Length == 0) recoveryError = "Physical traffic fixed-step application failed: " + error.Message;
                    }
                    finally { body.PendingNativeForce = false; }
                }
            }
            finally { nativeStepActive = false; }
        }
        internal bool DeferNativeForce(Rigidbody rigidbody, Vector3 force, ForceMode mode)
        {
            if (!Active || !forceBodies.TryGetValue(rigidbody, out var body) || !body.Valid) return false;
            if (!nativeStepActive || mode != ForceMode.VelocityChange || body.PendingNativeForce || body.NativeStepPaired)
            {
                if (recoveryError.Length == 0) recoveryError = "Native traffic force/rotation pairing changed.";
                force = Vector3.zero;
            }
            body.NativeForce = force; body.NativeForceFixedTime = Time.fixedTime; body.NativeForceStep = nativeStep; body.PendingNativeForce = true;
            return true;
        }
        internal bool ApplyNativeRotation(Rigidbody rigidbody, Quaternion proposed)
        {
            if (!Active || !forceBodies.TryGetValue(rigidbody, out var body) || !body.Valid) return false;
            bool paired = nativeStepActive && !body.NativeStepPaired && body.PendingNativeForce
                && body.NativeForceStep == nativeStep && body.NativeForceFixedTime == Time.fixedTime;
            if (!paired && recoveryError.Length == 0) recoveryError = "Native traffic force/rotation pairing changed.";
            body.PendingNativeForce = false;
            body.NativeStepPaired = true; body.NativeStepPairValid = paired;
            body.NativeRotation = proposed;
            // Both Unity commands remain deferred until native reached/waypoint checks finish.
            // Those checks can create a receipt that must freeze this very physics step.
            return true;
        }
        private void ApplyNativeCommand(PhysicalVehicleBody body)
        {
            var rigidbody = body.NativeRigidbody;
            var force = body.NativeForceStep == nativeStep && (body.NativeStepPaired || body.PendingNativeForce) ? body.NativeForce : Vector3.zero;
            var proposed = body.NativeRotation;
            Quaternion allowed = rigidbody.rotation;
            Vector3 constrained;
            try
            {
                if (!body.NativeStepPaired)
                {
                    var velocity = rigidbody.velocity;
                    constrained = new Vector3(Position.Finite(velocity.x) ? -velocity.x : 0,
                        Position.Finite(force.y) ? force.y : 0, Position.Finite(velocity.z) ? -velocity.z : 0);
                    rigidbody.angularVelocity = Vector3.zero;
                }
                else
                {
                    if (!body.NativeStepPairValid || CommandLimit == null || recoveryError.Length != 0)
                        throw new InvalidOperationException("Native force/rotation/traffic permission contract is absent.");
                    var original = allowed;
                    var delta = Quaternion.Inverse(original) * proposed;
                    if (!Finite(original) || !Finite(proposed) || !Finite(delta) || Math.Abs(delta.x) > .00001 || Math.Abs(delta.z) > .00001)
                        throw new InvalidOperationException("Native traffic rotation no longer follows the verified local-yaw command.");
                    double radians = 2 * Math.Atan2(delta.y, delta.w);
                    if (radians > Math.PI) radians -= 2 * Math.PI;
                    if (radians < -Math.PI) radians += 2 * Math.PI;
                    var markerOffset = body.FrontMarker.position - rigidbody.position;
                    if (!Point(markerOffset).IsFinite || markerOffset.sqrMagnitude > 400)
                        throw new InvalidOperationException("Native front-marker offset changed outside its admitted bounds.");
                    // The measured marker and current kernel permission are refreshed for EVERY paired physics
                    // command. Reserve the entire positive rotation arc before allowing any root translation.
                    var permission = CommandLimit(body);
                    var rotation = PhysicalTrafficMotion.LimitRotation(Point(markerOffset), Point(original * Vector3.up),
                        permission.Direction, radians, permission.MaximumAdvance);
                    allowed = permission.HoldPosition ? original : rotation.Fraction == 1 ? proposed : rotation.Fraction == 0 ? original
                        : original * Quaternion.AngleAxis((float)(radians * rotation.Fraction * 180 / Math.PI), Vector3.up);
                    double speed = permission.HoldPosition ? 0 : Math.Max(0, permission.MaximumAdvance - rotation.ReservedAdvance) / Time.fixedDeltaTime;
                    if (permission.HoldPosition) rigidbody.angularVelocity = Vector3.zero;
                    constrained = Vector(PhysicalTrafficMotion.LimitVelocityChange(Point(rigidbody.velocity), Point(force), speed));
                }
            }
            catch (Exception error)
            {
                if (recoveryError.Length == 0) recoveryError = "Physical traffic command constraint failed: " + error.Message;
                // Native may skip BOTH calls with no grounded wheels. It still needs horizontal
                // braking and no residual spin. A broken pair/permission also stops this owned lease.
                var velocity = rigidbody.velocity;
                constrained = new Vector3(Position.Finite(velocity.x) ? -velocity.x : 0,
                    Position.Finite(force.y) ? force.y : 0, Position.Finite(velocity.z) ? -velocity.z : 0);
                allowed = rigidbody.rotation;
                rigidbody.angularVelocity = Vector3.zero;
            }
            // Apply once, after all validation. A native API exception must not append a second
            // compensating force to a command that Unity may already have queued.
            if (!body.Valid) return; // A permission callback must not reclaim a foreign body.
            rigidbody.AddForce(constrained, ForceMode.VelocityChange);
            // MoveRotation was not called earlier: this cancels all yaw when a receipt arrived
            // after the pair, without trying to undo an already queued Unity command.
            rigidbody.MoveRotation(allowed);
        }
        private static bool Finite(Quaternion value) => Position.Finite(value.x) && Position.Finite(value.y)
            && Position.Finite(value.z) && Position.Finite(value.w);
        public void Update()
        {
            if (!Active) return;
            AssertHealthy();
            foreach (var body in bodies.Values.ToArray())
            {
                if (!body.Valid) { Release(body); continue; }
                if (!NativeReady) continue;
                if (body.Path.Count == 0) { body.RouteFinished = true; TrafficManager.Instance.SetVehicleAction(body.Vehicle, SpecialDriveActionTypes.StopNow, true); }
            }
            // One classification per frame, offscreen only. Foreign preset paths are never retired.
            if (!NativeReady) return;
            var pool = Manager.GetVehicleList(); if (pool.Count == 0) return;
            var vehicle = pool[retireCursor++ % pool.Count];
            if (vehicle != null && vehicle.gameObject.activeInHierarchy && vehicle.presetPath == null && RandomAmbient(vehicle)
                && Owned(vehicle.GetIndex()) == null && !vehicle.GetComponentsInChildren<Renderer>().Any(r => r.isVisible))
                Manager.RemoveVehicle(vehicle.GetIndex());
        }
        internal void ObserveCrashExpiry(DrivingAI ai, int index, int otherIndex, bool addAction)
        {
            // Native random cars remove only StopNow when the manager reports its timer expired.
            // Preset paths skip that native removal, so restore it only for this adapter's live lease.
            try
            {
                if (!TrafficManager.IsInitialized || !ReferenceEquals(PhysicalTrafficHooks.DrivingAiField!.GetValue(TrafficManager.Instance), ai)) return;
                var body = Owned(index); if (body == null) return;
                if (addAction) { body.LastCollisionFixedTime = Time.fixedTime; return; }
                if (otherIndex != index || body.RouteFinished || body.Path.Count == 0) return;
                var state = TrafficManager.Instance.GetCurrentDrivingState(index);
                if (state.Item2 != SpecialDriveActionTypes.StopNow || !(state.Item1 < 0)) return;
                string blocked = "";
                if (Time.fixedTime - body.LastCollisionFixedTime <= Time.fixedDeltaTime * 1.01f) blocked = "new collision in this physics step";
                if (blocked.Length != 0) { body.StopRecoveryDeferredReason = blocked; deferredStopRecoveries++; return; }
                // Re-read immediately before mutation; an event callback may have installed a fresh stop.
                state = TrafficManager.Instance.GetCurrentDrivingState(index);
                if (!body.Valid || body.RouteFinished || state.Item2 != SpecialDriveActionTypes.StopNow || !(state.Item1 < 0)) return;
                // Exactly the native ambient-car expiry operation: remove this one timed action.
                // Keep obstacle, red-light, yield and all other actions; their normal priority wins.
                PhysicalTrafficHooks.RemoveAction!.Invoke(ai, new object[] { index, SpecialDriveActionTypes.StopNow, false });
                body.StopRecoveryDeferredReason = ""; expiredStopRecoveries++;
            }
            catch (Exception error)
            {
                // This postfix is outside the population update guard; surface it there next frame.
                if (recoveryError.Length == 0)
                {
                    recoveryError = "Native temporary-stop recovery failed: " + error.Message;
                    Debug.LogError("[Persistent City] " + recoveryError);
                }
            }
        }
        internal bool Reached(int index, int waypointIndex, out bool result)
        {
            result = false; var body = Owned(index); if (body == null) return false;
            var current = TrafficManager.Instance.GetTargetWaypoint(index);
            // Native preset-path loops can pop several nearby nodes before SetNextWaypoint marks
            // only the OLD target Passed. Advance one real target at a time so enter/exit accounting
            // is never skipped. Requests from its front-axle job must not reassign an unchanged target.
            if (current == null || current.listIndex != waypointIndex) return true;
            if (body.HasRecordedReached(waypointIndex)) { result = true; return true; }
            var target = current;
            var position = body.FrontMarker.position;
            var start = Vector(body.LastTargetOrigin); var delta = target.position - start; delta.y = 0;
            var offset = position - start; offset.y = 0;
            // Along-route projection, never the car's changing nose direction; tight approach tolerance.
            result = (target.position - position).sqrMagnitude < 2.25f
                || (delta.sqrMagnitude > 0.01f && Vector3.Dot(offset, delta) >= delta.sqrMagnitude
                    && Vector3.Cross(offset, delta.normalized).sqrMagnitude < 4f);
            if (target.stop && (target.position - position).sqrMagnitude > 0.25f) result = false;
            if (result)
            {
                try { if (NativeTargetPermission == null || !NativeTargetPermission(body, waypointIndex)) result = false; }
                catch (Exception error)
                {
                    if (recoveryError.Length == 0) recoveryError = "Physical native target permission failed: " + error.Message;
                    result = false;
                }
            }
            if (result) { body.LastTargetOrigin = Point(target.position); body.RecordReached(waypointIndex, Point(target.position)); }
            return true;
        }
        internal bool MayRequest(int index)
        {
            var body = Owned(index); if (body == null) return true;
            if (body.Path.Count == 0) { body.RouteFinished = true; TrafficManager.Instance.SetVehicleAction(body.Vehicle, SpecialDriveActionTypes.StopNow, true); return false; }
            var target = TrafficManager.Instance.GetTargetWaypoint(index);
            if (target == null || target.stop || body.Path[0] != target.listIndex) return false;
            // Gley's front axle reaches its request threshold before our centre-of-mass proof.
            // Calling the original early would SetNextWaypoint to the SAME index, whose native
            // MarkWaypointAsPassed would falsely register another intersection entry every frame.
            if (!Reached(index, target.listIndex, out bool reached) || !reached) return false;
            if (body.Path.Count == 1)
            {
                body.RouteFinished = true;
                TrafficManager.Instance.SetVehicleAction(body.Vehicle, SpecialDriveActionTypes.StopNow, true);
                // Release uses native RemoveVehicle, which performs final intersection cleanup.
                return false;
            }
            return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Disable(); }
            finally
            {
                disposed = true;
                try { hooks.Dispose(); }
                finally { intersections.Dispose(); }
            }
        }
    }
}
