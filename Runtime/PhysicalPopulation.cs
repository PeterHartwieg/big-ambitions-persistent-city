// Physical population state and lifecycle: the binding type, fields, construction, diagnostics, binding registry and dispose.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        // Keep the E6e hook with physical population; reuse E6d's validated, logical-minute image.
        internal StreetFoodVendorMarket? PhysicalVendorMarket => vendorMarket;
    }

    /// <summary>Bodies follow the population. A walker (F2c) follows the logical pose of its committed journey and a car
    /// (F2d) follows the traffic kernel's background coordinate; neither writes logical state, and the kernel never
    /// waits for a body.</summary>
    internal sealed partial class PhysicalPopulation : IDisposable
    {
        private sealed class Binding
        {
            // F9g named exception: a bound body follows the entity's committed journey and optional stop, which the
            // read-only person view does not carry, and passes it to the simulator's journey samplers.
            public CitizenState Citizen = null!;
            // F2c walker and F2d car follow: the committed journey this body follows, and the body's own progress along it
            // (route minutes, never ahead of the logical pose). Presentation only; never saved.
            public Route? Journey;
            public double BodyMinutes;
            // Q3 correction of an on-screen body: hurrying since, then fading at the next corner or doorway.
            public float HurrySince = float.NaN;
            public bool Fading;
            public int FadeLeg = -1;
            // The fade itself: started at this follow-clock time, from this pose, seen or not when it began.
            public float FadeOutSince = float.NaN;
            public bool FadeOnScreen;
            public Position FadeFrom;
            // Arrival tail: the logical arrival was processed while the body still walks to the door.
            public bool Tail;
            public float TailSince;
            // Released at a mode boundary: a body of the new kind is re-acquired without the retry cooldown.
            public bool Handoff;
            public int LagFrame = -1;
            public PhysicalPedestrianBody? Person;
            public PhysicalVehicleBody? Car;
            public int TargetLeg = -1;
            // P-C: the walker's native destination, one path per leg: the leg's corner (the capped speed stops the agent
            // at the logical pose), or the pose itself on a leg whose corner path was refused (TargetIsPose).
            public Position TargetPoint;
            public bool TargetIsPose;
            // F2d: Gley's reached evidence for the car's own leg, and the corner hold that shapes the visible turn.
            public int ReachedCarLeg = -1;
            public Position ReachedCarMarker;
            public readonly PhysicalCornerHandoff CornerHandoff = new PhysicalCornerHandoff();
            // F2d: the visible car's phase state in its previous frame (Decision 6 count).
            public CarPhaseMismatch Phase;
            public float NextTargetAttempt;
            // A queue-at-opening stand, its line and its place in it. Null for every other binding.
            public Dwell? Queue;
            public long VendorOpening; // transient; shares the same pool, slot and movement bounds as door queues
            public double VendorDepartureUntil; // captured logical departure plus catch-up grace; never saved
            public OptionalStopState? Optional;
            public bool OptionalReturning;
            // The body could not reach or use the saved stop's spot, so it stands where it is until the
            // stop ends. Presentation only: the saved stop, its partners and spot occupancy stay logical.
            public bool OptionalStandsShort;
            public DoorQueue? Line;
            public int Slot = -1;
            public QueueStep Step;
            // Seconds this body has actually been walking in its current queue step. Pauses and
            // red lights stop the agent, so they never count toward the step's bound.
            public float StepWalked;
            public int EntryAttempts;
            // A walker whose arrival tail ended at the door joins a stop or queue with its body (ObserveArrival).
            public bool AwaitingArrival;
            public double AwaitMinute;
            public Route? AwaitJourney;
        }
        private enum QueueStep { Approaching, Standing, Entering }
        private sealed class QueuePlanEntry
        {
            public Dwell? Dwell; public DoorQueue Line = null!; public int Slot; public long VendorOpening;
        }
        private readonly CitySimulator simulation;
        private readonly MultimodalRoutes routes;
        private readonly PhysicalPedestrians pedestrians;
        private readonly PhysicalVehicles vehicles;
        private readonly AmbientPedestrians ambient;
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> attempted = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places;
        private readonly DwellSpots spots;
        private readonly PopulationRuntime? owner;
        private readonly Dictionary<string, long> vendorGaveUp = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, IPersonView> vendorWaiters = new Dictionary<string, IPersonView>(StringComparer.Ordinal);
        private FoodCommerceState? vendorFood;
        private int vendorMealCount;
        private readonly Dictionary<string, ResidentMeal> vendorMeals = new Dictionary<string, ResidentMeal>(StringComparer.Ordinal);
        private readonly Dictionary<string, QueuePlanEntry> queuers = new Dictionary<string, QueuePlanEntry>(StringComparer.Ordinal);
        // Queue dwells whose citizen could not reach a spot: they wait inside for the rest of that dwell.
        private readonly Dictionary<string, double> gaveUp = new Dictionary<string, double>(StringComparer.Ordinal);
        private const float QueueStepSeconds = 20f;
        private const int EntryAttemptFrames = 30;
        // One uncached door is sampled per frame, shared by the queue plan and arriving walkers.
        private readonly IModLogger log;
        // F2e contact detection memory (presentation only, never saved).
        private readonly PlayerContact contact = new PlayerContact();
        private readonly int pedestrianLimit, vehicleLimit;
        private readonly double radius;
        private float nextSelection;
        private bool active, disposed;
        private long acquired, released, blockedSamples, failedAcquisitions, queued;
        /// <summary>F2c walker and F2d car corrections, lag and counters for pc movement status. Runtime memory only; it outlives
        /// one physical population so correction sequence numbers stay increasing within a session.</summary>
        internal static readonly WalkerMovementTelemetry Telemetry = new WalkerMovementTelemetry();
        // Test-profile key MovementFaultAcquirePercent: refuse this share of body acquisitions (rig check 5).
        private readonly int faultAcquirePercent;
        private readonly System.Random faultDice = new System.Random(0x2F2C);
        // F2c follow clock: real seconds that pass only while the city clock runs, so a paused game freezes the
        // hurry, fade and arrival-tail bounds as it freezes the bodies.
        private float followClock, followDelta;
        private double followMinute = double.NaN;
        // Q3: persons whose body faded reappear only once their logical pose is out of view. The restriction belongs
        // to the journey that was followed: it ends when the body reappears out of view, or when that journey ends,
        // is replaced or leaves the active-journey index. A body faded on its arrival tail has arrived: its restriction
        // belongs to the place it arrived at (ArrivedAt) and lasts while the person is there, so a door queue or an
        // optional stop does not bring the body back in view.
        private readonly Dictionary<string, (CitizenState Citizen, Route? Journey, string? ArrivedAt)> fadedUntilHidden
            = new Dictionary<string, (CitizenState Citizen, Route? Journey, string? ArrivedAt)>(StringComparer.Ordinal);
        // Running counts of registered bindings with a walker, a car and a queue stand. Only Bind,
        // Unbind and TryJoinQueue change registration or a registered binding's queue.
        private int personBindings, carBindings, queueBindings;
        // Reused per-frame snapshot of bindings; taken out while in use so a nested call cannot share it.
        private List<Binding>? bindingSnapshot = new List<Binding>();
        // F5f shadow comparison of the active-journey index with a full scan, at a low cadence and at the
        // first selection after construction (load, reload, route rebuild). A mismatch logs once and falls
        // back to the full scan for this session.
        private const float JourneyIndexCheckSeconds = 30f;
        private float nextJourneyIndexCheck;
        private bool journeyIndexFallback;
        private long journeyIndexChecks, journeyIndexMismatches;

        public PhysicalPopulation(Transform parent, CitySimulator simulation, MultimodalRoutes routes, Assembly dependency,
            IModLogger log, int pedestrianLimit, int vehicleLimit, double radius, DwellSpots spots, int faultAcquirePercent = 0)
        {
            this.spots = spots;
            this.faultAcquirePercent = Math.Max(0, Math.Min(100, faultAcquirePercent));
            owner = parent.GetComponent<PopulationRuntime>();
            // F9g: residents, then visitor routines (PersonTable.Schedules' order), read through the view.
            var view = simulation.View;
            for (int i = 0; i < view.ResidentList.Count; i++) TrackVendorWaiter(view.ResidentList[i]);
            for (int i = 0; i < view.VisitorRoutineList.Count; i++) TrackVendorWaiter(view.VisitorRoutineList[i]);
            this.simulation = simulation; this.routes = routes; this.log = log;
            RefreshVendorMeals();
            this.pedestrianLimit = pedestrianLimit; this.vehicleLimit = vehicleLimit; this.radius = radius;
            places = simulation.State.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
            pedestrians = new PhysicalPedestrians(parent, pedestrianLimit);
            vehicles = new PhysicalVehicles(routes.ResolveWaypoint, vehicleLimit);
            vehicles.SuspendForNativeFastForward = SuspendBeforeNativeFastForward;
            vehicles.CommandLimit = CommandLimit;
            vehicles.NativeTargetPermission = NativeTargetPermission;
            ambient = new AmbientPedestrians(dependency, simulation.State.SourceIdentities);
            try
            {
                if (!vehicles.Ready) throw new InvalidOperationException("Vehicle hooks: " + vehicles.LastError);
                if (!ambient.Ready) throw new InvalidOperationException("Pedestrian hooks: " + ambient.LastError);
                // Test real native prefab/appearance/NavMesh acquisition before suppressing ordinary spawning.
                bool tested = false;
                foreach (var resident in view.ResidentList.Take(64))
                {
                    if (!pedestrians.TryAcquire(Entity(resident).Identity, places[resident.HomeId].Position, out var body)) continue;
                    pedestrians.Release(body!); tested = true; break;
                }
                if (!tested) throw new InvalidOperationException("No native pedestrian body passed appearance/NavMesh preflight.");
                if (!vehicles.Activate()) throw new InvalidOperationException("Native traffic is not ready.");
                ambient.Activate();
                if (!ambient.Active) throw new InvalidOperationException("Pedestrian activation: " + ambient.LastError);
                active = true;
                simulation.ActivityObserved += OnOptionalDeparture;
                log.Info("Physical population active: ordinary pedestrian and vehicle spawns replaced; RLO scenes, customers, workers and service vehicles retained.");
            }
            catch { Dispose(); throw; }
        }

        public string Diagnostics => "physical=" + active + ", pedestrians=" + personBindings
            + ", cars=" + carBindings + ", acquired=" + acquired + ", released=" + released
            + ", blockedSamples=" + blockedSamples + ", unavailable=" + failedAcquisitions
            + ", queueing=" + queueBindings + ", queued=" + queued + ", walkersResumed=" + ambient.Resumed
            + ", journeyIndex=" + simulation.ActiveJourneys.Count + ", journeyIndexWithheldVisitors=" + simulation.ActiveJourneys.WithheldVisitors
            + ", journeyIndexChecks=" + journeyIndexChecks + ", journeyIndexMismatches=" + journeyIndexMismatches + ", journeyIndexFallback=" + journeyIndexFallback
            + ", carAdmission=" + vehicles.LastAdmissionResident + ":" + vehicles.LastAdmissionStage + ":" + vehicles.LastAdmissionError;
        public void ObserveForSave()
        {
            vehicles.AssertHealthy();
            simulation.RefreshOptionalStops();
            foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
        }
        /// <summary>F9g named exception, the one step from a read-only person view back to its entity: following needs the
        /// committed journey, the optional stop, the car and the appearance identity, which <see cref="IPersonView"/> does not
        /// carry (F9e's Core/CityView.cs), and the simulator's samplers take the entity. An identity cast: no lookup, no
        /// allocation. tests/OwnershipBoundaryTests.cs refuses any other entity cast in the presentation files.</summary>
        private static CitizenState Entity(IPersonView person) => (CitizenState)person;
        private bool Bound(Binding binding) => bindings.TryGetValue(binding.Citizen.Identity.Id, out var registered) && ReferenceEquals(registered, binding);
        private void Bind(Binding binding)
        {
            bindings.Add(binding.Citizen.Identity.Id, binding);
            CountBinding(binding, 1);
        }
        private void Unbind(string citizenId)
        {
            // Removes whatever binding holds this id, as before; the counts follow the removed binding.
            if (!bindings.TryGetValue(citizenId, out var registered)) return;
            bindings.Remove(citizenId);
            CountBinding(registered, -1);
        }
        private void CountBinding(Binding binding, int delta)
        {
            if (binding.Person != null) personBindings += delta;
            if (binding.Car != null) carBindings += delta;
            if (binding.Queue.HasValue || binding.VendorOpening > 0) queueBindings += delta;
        }
        private void ReleaseBodies(Binding binding)
        {
            try { if (binding.Person != null) pedestrians.Release(binding.Person); }
            finally { if (binding.Car != null) vehicles.Release(binding.Car); }
        }
        private void Release(Binding binding)
        {
            try { ReleaseBodies(binding); }
            finally
            {
                Unbind(binding.Citizen.Identity.Id);
                // A mode boundary (or an off-screen car correction) re-acquires a body at once; every other release waits out the retry.
                if (binding.Handoff) attempted.Remove(binding.Citizen.Identity.Id);
                else attempted[binding.Citizen.Identity.Id] = Time.unscaledTime;
                released++;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; active = false;
            simulation.ActivityObserved -= OnOptionalDeparture;
            vehicles.SuspendForNativeFastForward = null;
            vehicles.CommandLimit = null;
            vehicles.NativeTargetPermission = null;
            try { Suspend(); }
            finally
            {
                try { ambient.Dispose(); }
                finally { try { vehicles.Dispose(); } finally { pedestrians.Dispose(); } }
            }
        }
    }
}
