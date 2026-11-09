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
            // F2c: the walker's native destination. On the leg of the logical pose it is that pose (refreshed every
            // 0.5 m), so the agent stops there and cannot walk past it; on an earlier leg it is that leg's corner.
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
        private readonly Dictionary<string, CitizenState> vendorWaiters = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
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
            foreach (var citizen in simulation.State.Citizens) TrackVendorWaiter(citizen);
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
                foreach (var citizen in simulation.State.Citizens.Take(64))
                {
                    if (!pedestrians.TryAcquire(citizen.Identity, places[citizen.HomeId].Position, out var body)) continue;
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

        // Release before an unrendered time jump so catch-up can use the same committed geometry.
        public void BeforeAdvance()
        {
            vehicles.AssertHealthy();
            if (NativeTravelPace.HiddenTravel) SuspendBeforeNativeFastForward();
        }
        private void SuspendBeforeNativeFastForward()
        {
            if (!active || disposed) return;
            try { ObserveForSave(); }
            finally
            {
                if (NativeInteriorCityView.Configured) NativeInteriorCityView.Suspend();
                Suspend();
            }
        }
        public void BeforeSave()
        {
            // A blocker may already have cleared the view predicate before this callback,
            // while its bodies still await BeforeAdvance. Saving must release those too.
            if (NativeInteriorCityView.Configured && BuildingManager.IsInsideBuilding) SuspendBeforeNativeFastForward();
            else ObserveForSave();
        }
        public void RestoreInterior() { if (bindings.Count != 0) SuspendBeforeNativeFastForward(); }
        public bool HasBodies => bindings.Count != 0;
        /// <summary>Walkers following a committed journey now (pc movement status).</summary>
        public int Followers
        {
            get { int count = 0; foreach (var binding in bindings.Values) if (binding.Person != null && binding.Journey != null) count++; return count; }
        }
        /// <summary>F2d: cars following the traffic kernel now (pc movement status).</summary>
        public int CarFollowers
        {
            get { int count = 0; foreach (var binding in bindings.Values) if (binding.Car != null && binding.Journey != null) count++; return count; }
        }
        public void Suspend()
        {
            Exception? failure = null;
            foreach (var binding in bindings.Values.ToArray())
                try { Release(binding); } catch (Exception error) { failure = failure ?? error; }
            if (failure != null) throw new InvalidOperationException("One or more physical bodies could not be released.", failure);
        }
        public void Update()
        {
            if (!active || disposed) return;
            followDelta = simulation.State.Minute > followMinute ? Time.unscaledDeltaTime : 0;
            followClock += followDelta;
            followMinute = simulation.State.Minute;
            RefreshVendorMeals();
            ambient.Update();
            if (!ambient.Active) throw new InvalidOperationException("Pedestrian ownership guard failed: " + ambient.LastError);
            vehicles.Update();
            if (NativeTravelPace.HiddenTravel) { SuspendBeforeNativeFastForward(); return; }
            Position focus = NativeTravelPace.Focus;
            if (!focus.IsFinite) { Suspend(); return; }
            double radius = NativeInteriorCityView.AdmissionRadius(this.radius);
            double releaseRadius = NativeInteriorCityView.ReleaseRadius(this.radius);
            int pedestrianLimit = NativeInteriorCityView.Pedestrians(this.pedestrianLimit);
            int vehicleLimit = NativeInteriorCityView.Vehicles(this.vehicleLimit);
            bool interior = NativeInteriorCityView.State.Active;
            // F2e: the player's pose once per frame; inside a building view nobody is blocked by the player.
            var playerPose = default(PlayerPose);
            bool player = !interior && PlayerContact.TryReadPlayer(out playerPose);
            var snapshot = bindingSnapshot ?? new List<Binding>(bindings.Count);
            bindingSnapshot = null;
            try
            {
                snapshot.AddRange(bindings.Values);
                int keptPeople = 0, keptCars = 0;
                foreach (var binding in snapshot)
                {
                    if (ambient.IsReserved(binding.Citizen.Identity.Id)) { Release(binding); continue; }
                    if (!Observe(binding)) { Release(binding); continue; }
                    Position position = binding.Person?.Position ?? binding.Car!.Position;
                    if (Position.Distance(position, focus) > releaseRadius) { Release(binding); continue; }
                    if (interior && (binding.Person != null ? ++keptPeople > pedestrianLimit : ++keptCars > vehicleLimit)) { Release(binding); continue; }
                    if (player) ObservePlayerContact(binding, playerPose);
                }
            }
            finally { snapshot.Clear(); bindingSnapshot = snapshot; }
            if (Time.unscaledTime < nextSelection || !routes.ReadyForPopulation) return;
            nextSelection = Time.unscaledTime + .25f;
            // The bounded view is selected deterministically. No filler identities or unrelated trips are created.
            int people = personBindings, cars = bindings.Count - people;
            int attempts = 0;
            if (!journeyIndexFallback && Time.unscaledTime >= nextJourneyIndexCheck)
            {
                nextJourneyIndexCheck = Time.unscaledTime + JourneyIndexCheckSeconds;
                CheckJourneyIndex();
                PruneFadeRestrictions();
            }
            PlanQueues(focus, radius);
            PlanVendorQueues(focus, radius);
            // Candidate selection. tools/physical-selection-regression extracts the text from this line
            // to the road sample below and compares it with the frozen LINQ selection.
            candidates.Clear();
            // F2f: the active-journey index members filed in the grid cells within the radius (visitors withheld
            // until F5i), or every citizen after a shadow mismatch. Filters and order below are the same for both sources.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
            for (int index = 0; index < roster.Count; index++)
            {
                var c = roster[index];
                // Both pools are full: observations and queue movement above still ran; no
                // candidate can acquire a body until a later update frees a slot.
                if (people >= pedestrianLimit && cars >= vehicleLimit) break;
                if (journeyIndexFallback && !ActiveJourneyIndex.ReceivesBody(c)) continue;
                if (!(c.NextEventMinute > simulation.State.Minute) || bindings.ContainsKey(c.Identity.Id) || ambient.IsReserved(c.Identity.Id)) continue;
                bool tried = attempted.TryGetValue(c.Identity.Id, out float lastAttempt);
                if (tried && !(Time.unscaledTime - lastAttempt >= 2)) continue;
                // Stationary indoor residents cannot consume an attempt. Keep travelling
                // residents here: PositionOf and SampleJourney have distinct boundary rules.
                if (!((c.Phase == CitizenPhase.Travelling && c.Journey != null) || queuers.ContainsKey(c.Identity.Id) || Outdoor(c))) continue;
                Position sampled = simulation.PositionOf(c);
                double distance = Position.Distance(sampled, focus);
                if (!(distance <= radius)) continue;
                candidates.Add(new SelectionCandidate(c, sampled, tried ? lastAttempt : float.NegativeInfinity, distance, candidates.Count));
            }
            // Vendor stays are outdoor meal service, so they are not in the journey/opening index.
            // Maintain them from committed activity events rather than scanning the population per frame.
            foreach (var citizen in vendorWaiters.Values)
            {
                if (journeyIndexFallback || simulation.ActiveJourneys.Contains(citizen)
                    || people >= pedestrianLimit || !queuers.TryGetValue(citizen.Identity.Id, out var queue)
                    || bindings.ContainsKey(citizen.Identity.Id) || ambient.IsReserved(citizen.Identity.Id)
                    || !(citizen.NextEventMinute > simulation.State.Minute)) continue;
                bool tried = attempted.TryGetValue(citizen.Identity.Id, out float lastAttempt);
                if (tried && Time.unscaledTime - lastAttempt < 2) continue;
                double distance = Position.Distance(queue.Line.Entrance, focus);
                if (distance <= radius) candidates.Add(new SelectionCandidate(citizen, queue.Line.Entrance,
                    tried ? lastAttempt : float.NegativeInfinity, distance, candidates.Count));
            }
            candidates.Sort(SelectionCandidate.ByPriority);
            foreach (var candidate in candidates)
            {
                var citizen = candidate.Citizen;
                bool moving = citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null;
                PhysicalJourneySample sample = moving ? simulation.SampleJourney(citizen) : default;
                bool car = moving && sample.Mode == TravelMode.Car && !sample.Stationary;
                QueuePlanEntry? queue = null;
                bool queuer = !moving && queuers.TryGetValue(citizen.Identity.Id, out queue);
                bool person = moving ? sample.Mode == TravelMode.Walk : queuer || Outdoor(citizen);
                if ((!car && !person) || (car ? cars >= vehicleLimit : people >= pedestrianLimit)) continue;
                if (++attempts > 4) break; // Route validation and rejected admissions share the native-work budget.
                attempted[citizen.Identity.Id] = Time.unscaledTime;
                PhysicalRoadSample road = default;
                // Q3: a faded person reappears only where the player cannot see a body appear (F2d: a car as well). A
                // deferral does no native work, so it gives back the attempt and the retry time it was just charged.
                if (FadeRestricted(citizen) && pedestrians.Visible(candidate.Position))
                {
                    attempts--;
                    if (float.IsNegativeInfinity(candidate.LastAttempt)) attempted.Remove(citizen.Identity.Id);
                    else attempted[citizen.Identity.Id] = candidate.LastAttempt;
                    continue;
                }
                fadedUntilHidden.Remove(citizen.Identity.Id);
                // Waiting sources own no finite road storage. Refuse before borrowing a native body.
                if (car && (!simulation.TryGetTrafficPermission(citizen.Identity.Id, out var admission)
                    || !admission.LinkId.HasValue || admission.IsCompleted)) continue;
                if (car && !routes.TryPhysicalRoadSample(citizen, sample, out road))
                    continue;
                var binding = new Binding { Citizen = citizen };
                Position at = candidate.Position;
                // A waiter who was indoors comes out through the entrance and walks to their spot.
                if (queuer) { binding.Queue = queue!.Dwell; binding.VendorOpening = queue.VendorOpening; binding.Line = queue.Line; binding.Slot = queue.Slot; at = queue.Line.Entrance; }
                // Nothing is leased (F2c walkers, F2d cars): the body follows its journey's logical pose from where it is now.
                if (moving) { binding.Journey = citizen.Journey; binding.BodyMinutes = sample.ElapsedMinutes; }
                bool bound = false;
                try
                {
                    if (faultAcquirePercent > 0 && faultDice.Next(100) < faultAcquirePercent)
                    { failedAcquisitions++; Telemetry.FaultedAcquisitions++; continue; }
                    if (car)
                    {
                        if (!vehicles.TryAcquire(citizen, road, out var body)) { failedAcquisitions++; continue; }
                        binding.Car = body;
                    }
                    else
                    {
                        if (!pedestrians.TryAcquire(citizen.Identity, at, out var body)) { failedAcquisitions++; continue; }
                        binding.Person = body;
                    }
                    Bind(binding); bound = true; acquired++;
                }
                finally { if (!bound) ReleaseBodies(binding); }
                // No walk from the entrance to the spot: stay indoors rather than stand in the doorway.
                if (queuer && !binding.Person!.SetPathTarget(binding.Line!.Spots[binding.Slot], NativeTravelPace.WalkingMetresPerSecond))
                { GiveUp(binding); Release(binding); continue; }
                if (!Observe(binding)) { Release(binding); continue; }
                if (car) cars++; else people++;
                if (queuer) queued++;
            }
            candidates.Clear();
            if (attempted.Count > simulation.State.Citizens.Count + 128)
            {
                var ids = new HashSet<string>(simulation.State.Citizens.Select(c => c.Identity.Id), StringComparer.Ordinal);
                foreach (var id in attempted.Keys.Where(id => !ids.Contains(id)).ToArray()) attempted.Remove(id);
            }
        }

        // Whether a faded person's restriction still applies: the same journey is still under way, or, after an
        // arrival-tail fade, the person is still at the place it arrived at.
        private bool FadeRestricted(CitizenState citizen)
            => fadedUntilHidden.TryGetValue(citizen.Identity.Id, out var faded) && ReferenceEquals(faded.Citizen, citizen)
            && (faded.ArrivedAt != null
                ? (citizen.Phase == CitizenPhase.AtPlace || citizen.Phase == CitizenPhase.WaitingForOpening) && citizen.PlaceId == faded.ArrivedAt
                : citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, faded.Journey)
                    && simulation.ActiveJourneys.Contains(citizen));

        private readonly List<string> staleFades = new List<string>();
        private void PruneFadeRestrictions()
        {
            foreach (var pair in fadedUntilHidden) if (!FadeRestricted(pair.Value.Citizen) || pair.Value.Citizen.Identity.Id != pair.Key) staleFades.Add(pair.Key);
            foreach (var id in staleFades) fadedUntilHidden.Remove(id);
            staleFades.Clear();
        }

        private void CheckJourneyIndex()
        {
            journeyIndexChecks++;
            if (simulation.ActiveJourneyIndexMatchesState() && StatefulCandidatesIndexed()) return;
            journeyIndexMismatches++; journeyIndexFallback = true;
            log.Warn("Active-journey index differs from a full population scan; physical selection uses the full scan for this session.");
        }

        // The index must hold every citizen this selection's stateful filters (journey, queue, outdoors) could admit.
        private bool StatefulCandidatesIndexed()
        {
            var index = simulation.ActiveJourneys;
            foreach (var c in simulation.State.Citizens)
                if (!index.Contains(c) && (c.Phase == CitizenPhase.Travelling && c.Journey != null
                    || c.Phase == CitizenPhase.WaitingForOpening || Outdoor(c))) return false;
            return true;
        }

        /// <summary>One physical selection candidate, with its sort keys captured before any attempt is recorded.</summary>
        private readonly struct SelectionCandidate
        {
            public SelectionCandidate(CitizenState citizen, Position position, float lastAttempt, double distance, int rosterIndex)
            { Citizen = citizen; Position = position; LastAttempt = lastAttempt; Distance = distance; RosterIndex = rosterIndex; }
            public CitizenState Citizen { get; }
            public Position Position { get; }
            public float LastAttempt { get; }
            public double Distance { get; }
            public int RosterIndex { get; }
            // Least recently attempted first (never attempted first of all), then nearest, then ordinal id.
            // Roster order breaks any remaining tie, as the stable LINQ OrderBy/ThenBy chain did.
            public static readonly IComparer<SelectionCandidate> ByPriority = new Priority();
            private sealed class Priority : IComparer<SelectionCandidate>
            {
                public int Compare(SelectionCandidate a, SelectionCandidate b)
                {
                    int order = a.LastAttempt.CompareTo(b.LastAttempt);
                    if (order == 0) order = a.Distance.CompareTo(b.Distance);
                    if (order == 0) order = StringComparer.Ordinal.Compare(a.Citizen.Identity.Id, b.Citizen.Identity.Id);
                    return order != 0 ? order : a.RosterIndex.CompareTo(b.RosterIndex);
                }
            }
        }
        private readonly List<SelectionCandidate> candidates = new List<SelectionCandidate>();
        // F2f grid query result, reused by PlanQueues and then the selection in the same update.
        private readonly List<CitizenState> nearby = new List<CitizenState>();

        // Waiting for a route or for an opening is indoors. Only a queue-at-opening dwell
        // (PlanQueues) puts a waiting citizen in the street, at a spot beside the door.
        private bool Outdoor(CitizenState c)
            => OptionalVisible(c) && simulation.OptionalStopStatus(c.OptionalStop!) == OptionalStopAvailability.Ready || c.Phase == CitizenPhase.AtPlace && c.Activity == Activity.Leisure && places[c.PlaceId].Id.StartsWith("scene-", StringComparison.Ordinal);

        /// <summary>
        /// Which waiting citizens near the focus stand in a door queue now, and in which spot. One
        /// line per door: places sharing an entrance share it. A body already in a line keeps its
        /// spot until it leaves (DwellPlanner.AssignQueueSlots). A full line leaves the rest indoors.
        /// </summary>
        private void PlanQueues(Position focus, double radius)
        {
            queuers.Clear();
            double now = simulation.State.Minute;
            var planned = new Dictionary<string, (Place Place, List<Dwell> Dwells)>(StringComparer.Ordinal);
            // Waiting citizens are indexed (F5f) and filed at their place cell (F2f). Line and slot order are sorted
            // below, not taken from this order.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
            for (int index = 0; index < roster.Count; index++)
            {
                var citizen = roster[index];
                if (journeyIndexFallback && !ActiveJourneyIndex.ReceivesBody(citizen)) continue;
                if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)
                    || Position.Distance(place.Position, focus) > radius) continue;
                var dwell = QueueDwell(citizen, place, now);
                if (!dwell.HasValue || !dwell.Value.ActiveAt(now) || GaveUp(dwell.Value)) continue;
                string key = DwellSpots.KeyOf(place);
                if (!planned.TryGetValue(key, out var line)) planned.Add(key, line = (place, new List<Dwell>()));
                line.Dwells.Add(dwell.Value);
            }
            foreach (var pair in planned.OrderBy(p => Position.Distance(p.Value.Place.Position, focus)).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!TryLine(pair.Value.Place, out var found)) continue;
                // People in the line move up, front first, into free spots ahead of them. A move counts
                // only once the body accepts the walk; a refused move keeps its spot reserved.
                var inLine = bindings.Values.Where(b => b.Line != null && b.Line.Key == found.Key && b.Queue.HasValue
                    && b.Step != QueueStep.Entering && b.Person != null).OrderBy(b => b.Slot).ToList();
                var occupied = new HashSet<int>(Standing(found).Select(b => b.Value));
                foreach (var bound in inLine)
                {
                    int? ahead = DwellPlanner.MoveUp(occupied, bound.Slot);
                    if (!ahead.HasValue || !bound.Person!.SetPathTarget(found.Spots[ahead.Value], NativeTravelPace.WalkingMetresPerSecond)) continue;
                    occupied.Remove(bound.Slot); occupied.Add(ahead.Value);
                    bound.Slot = ahead.Value; bound.Step = QueueStep.Approaching; bound.StepWalked = 0;
                }
                // Newcomers are placed from the spots bodies actually hold now.
                var slots = DwellPlanner.AssignQueueSlots(Standing(found), pair.Value.Dwells, found.Spots.Count);
                foreach (var dwell in pair.Value.Dwells)
                    if (slots.TryGetValue(dwell.CitizenId, out int slot))
                        queuers[dwell.CitizenId] = new QueuePlanEntry { Dwell = dwell, Line = found, Slot = slot };
            }
        }

        private Dwell? QueueDwell(CitizenState citizen, Place place, double now)
        {
            Dwell? committed = bindings.TryGetValue(citizen.Identity.Id, out var bound)
                && bound.Person != null && bound.Person.Valid && bound.Line != null && bound.Step != QueueStep.Entering
                ? bound.Queue : null;
            return simulation.QueueAtOpening(citizen, place, now, committed);
        }

        private bool GaveUp(Dwell dwell)
        {
            if (!gaveUp.TryGetValue(dwell.CitizenId, out double until)) return false;
            if (until == dwell.EndMinute && simulation.State.Minute < until) return true;
            gaveUp.Remove(dwell.CitizenId);
            return false;
        }

        private void GiveUp(Binding binding)
        {
            if (binding.Queue.HasValue) gaveUp[binding.Citizen.Identity.Id] = binding.Queue.Value.EndMinute;
            if (binding.VendorOpening > 0) vendorGaveUp[binding.Citizen.Identity.Id] = binding.VendorOpening;
            if (gaveUp.Count > 256)
                foreach (var stale in gaveUp.Where(p => p.Value <= simulation.State.Minute).Select(p => p.Key).ToList()) gaveUp.Remove(stale);
        }

        private IEnumerable<KeyValuePair<string, int>> Standing(DoorQueue line)
        {
            foreach (var b in bindings.Values)
            {
                if ((b.Queue.HasValue || b.VendorOpening > 0) && b.Line != null && b.Line.Key == line.Key && b.Step != QueueStep.Entering)
                    yield return new KeyValuePair<string, int>(b.Citizen.Identity.Id, b.Slot);
                else if (b.Optional != null && !b.OptionalReturning && (!b.OptionalStandsShort || b.Person != null))
                {
                    // A body standing short holds the slot it actually stands on, not the stop's spot.
                    Position at = b.OptionalStandsShort ? b.Person!.Position : b.Optional.Position;
                    for (int slot = 0; slot < line.Spots.Count; slot++)
                        if (Position.Distance(line.Spots[slot], at) < 0.65)
                            yield return new KeyValuePair<string, int>(b.Citizen.Identity.Id, slot);
                }
            }
        }

        private bool TryLine(Place place, out DoorQueue line)
        {
            bool found = spots.TryQueue(place, out var queue) && queue != null;
            line = queue!;
            return found;
        }

        private bool ObserveArrival(Binding binding)
        {
            var person = binding.Person!;
            if (!person.Valid) return false;
            person.Stop(true);
            var citizen = binding.Citizen;
            // A new journey (no later opening, so the simulator sent them on) is not this arrival.
            if (citizen.Phase == CitizenPhase.Travelling)
                return ReferenceEquals(citizen.Journey, binding.AwaitJourney) && simulation.State.Minute <= binding.AwaitMinute + 0.5;
            // Another door used this frame's sampling budget: wait a frame rather than lose the body.
            if (citizen.Phase == CitizenPhase.WaitingForOpening && spots.SampledThisFrame
                && places.TryGetValue(citizen.PlaceId, out var place) && spots.Unsampled(place)
                && simulation.State.Minute <= binding.AwaitMinute + 0.5) return true;
            if (VendorWaiting(citizen, out var vendor, out _) && spots.SampledThisFrame
                && spots.Unsampled(simulation.FindPlace(vendor.PlaceId)) && simulation.State.Minute <= binding.AwaitMinute + 0.5) return true;
            binding.AwaitingArrival = false;
            return TryJoinVendorQueue(binding) || TryJoinOptional(binding) || TryJoinQueue(binding);
        }

        /// <summary>Join the door's queue with the body that walked here. Its spot respects the people already planned for that line.</summary>
        private bool TryJoinQueue(Binding binding)
        {
            var person = binding.Person!; var citizen = binding.Citizen;
            if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)) return false;
            var dwell = QueueDwell(citizen, place, simulation.State.Minute);
            if (!dwell.HasValue || !dwell.Value.ActiveAt(simulation.State.Minute) || GaveUp(dwell.Value) || !TryLine(place, out var line)) return false;
            var reserved = Standing(line).Concat(queuers.Where(q => q.Key != citizen.Identity.Id && q.Value.Line.Key == line.Key
                && !bindings.ContainsKey(q.Key)).Select(q => new KeyValuePair<string, int>(q.Key, q.Value.Slot)));
            var slots = DwellPlanner.AssignQueueSlots(reserved, new[] { dwell.Value }, line.Spots.Count);
            if (!slots.TryGetValue(citizen.Identity.Id, out int slot)) return false;
            if (!person.SetPathTarget(line.Spots[slot], NativeTravelPace.WalkingMetresPerSecond))
            { gaveUp[citizen.Identity.Id] = dwell.Value.EndMinute; return false; }
            bool counted = binding.Queue.HasValue || !Bound(binding);
            binding.Queue = dwell; binding.Line = line; binding.Slot = slot;
            if (!counted) queueBindings++;
            binding.Step = QueueStep.Approaching; binding.StepWalked = 0;
            queued++;
            return ObserveQueue(binding);
        }

        private bool ObserveQueue(Binding binding)
        {
            var person = binding.Person!; var dwell = binding.Queue!.Value; var line = binding.Line!;
            var citizen = binding.Citizen;
            if (!person.Valid) return false;
            if (!person.Agent.isStopped) binding.StepWalked += Time.unscaledDeltaTime;
            if (binding.Step == QueueStep.Entering)
            {
                // Walking in through the opened door; the body is returned at the door or after a bound on walking time.
                if (binding.StepWalked >= QueueStepSeconds || Position.Distance(person.Position, line.Entrance) < 0.5) return false;
                person.Tick(); return true;
            }
            // One minute of grace: a budgeted catch-up can process the opening event a frame late.
            if (citizen.Phase == CitizenPhase.WaitingForOpening && citizen.PlaceId == dwell.SpotId && simulation.State.Minute < dwell.EndMinute + 1)
            {
                if (binding.Step == QueueStep.Approaching)
                {
                    if (Position.Distance(person.Position, line.Spots[binding.Slot]) <= 0.3)
                    {
                        // Drop the finished path so agent steering can't turn them away from the door.
                        binding.Step = QueueStep.Standing;
                        if (person.Agent.hasPath) person.Agent.ResetPath();
                    }
                    else if (binding.StepWalked < QueueStepSeconds) { person.Tick(); return true; }
                    else
                    {
                        // Never reached the spot (blocked by the player or a crowd): go back inside
                        // to wait for the rest of this dwell rather than stand short of the line.
                        GiveUp(binding);
                        if (!person.SetPathTarget(line.Entrance, NativeTravelPace.WalkingMetresPerSecond)) return false;
                        binding.Step = QueueStep.Entering; binding.StepWalked = 0;
                        person.Tick(); return true;
                    }
                }
                person.Stop(true);
                person.Face(binding.Slot == 0 ? line.Door : line.Spots[binding.Slot - 1]);
                return true;
            }
            // The place opened and admitted them: walk in through the door instead of vanishing on the spot.
            if (citizen.Phase != CitizenPhase.AtPlace || citizen.PlaceId != dwell.SpotId) return false;
            if (person.SetPathTarget(line.Entrance, NativeTravelPace.WalkingMetresPerSecond))
            { binding.Step = QueueStep.Entering; binding.StepWalked = 0; return true; }
            // A path can fail for a frame while bodies shuffle; keep standing and try again briefly.
            person.Stop(true);
            return ++binding.EntryAttempts < EntryAttemptFrames;
        }

        // ActivityObserved runs at commitment. Departure tail (F2c): a body returning from an optional stop or a
        // vendor stand keeps walking back while the logical journey has already started, then follows it with
        // catch-up. Nothing is held: the journey runs on the logical clock from its commit.
        private void OnOptionalDeparture(string id, CitizenState citizen, double minute)
        {
            TrackVendorWaiter(citizen);
            if (!bindings.TryGetValue(id, out var binding) || (binding.Optional == null && binding.VendorOpening == 0) || binding.Person == null
                || !binding.Person.Valid || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null
                || ReferenceEquals(binding.Journey, citizen.Journey)) return;
            Follow(binding, citizen.Journey, 0);
        }

        // A journey that committed without a departure notice for this body (for example while it was planned)
        // is followed from its start, where the returning body stands.
        private static void TryFollowCommitted(Binding binding)
        {
            var citizen = binding.Citizen;
            if (citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null) Follow(binding, citizen.Journey, 0);
        }

        private static void Follow(Binding binding, Route journey, double bodyMinutes)
        {
            binding.Journey = journey; binding.BodyMinutes = bodyMinutes; binding.TargetLeg = -1;
            binding.HurrySince = float.NaN; binding.Fading = false; binding.Tail = false; binding.FadeOutSince = float.NaN;
        }

        private bool OptionalVisible(CitizenState citizen) => citizen.Phase == CitizenPhase.AtPlace
            && citizen.OptionalStop != null && citizen.OptionalStop.ActiveAt(simulation.State.Minute);

        private bool TryJoinOptional(Binding binding)
        {
            if (binding.Person == null || !OptionalVisible(binding.Citizen)
                || simulation.OptionalStopStatus(binding.Citizen.OptionalStop!) != OptionalStopAvailability.Ready) return false;
            var stop = binding.Citizen.OptionalStop!;
            // A queue keeps the facade spot visually, and an unreachable spot cannot be walked to. In both
            // cases the body stands where it is (near the entrance) and never cancels the saved stop (F2b).
            bool shortOfSpot = bindings.Values.Any(b => b.Queue.HasValue && b.Line != null && b.Slot >= 0
                && Position.Distance(b.Line.Spots[b.Slot], stop.Position) < 0.65)
                || !binding.Person.SetPathTarget(stop.Position, NativeTravelPace.WalkingMetresPerSecond);
            binding.AwaitingArrival = false; binding.AwaitJourney = null; binding.Journey = null; binding.Tail = false;
            binding.Optional = stop; binding.OptionalReturning = false; binding.OptionalStandsShort = shortOfSpot; binding.StepWalked = 0;
            return true;
        }
        private bool ObserveOptional(Binding binding)
        {
            var body = binding.Person!; var stop = binding.Optional!;
            if (!body.Valid) return false;
            if (!body.Agent.isStopped) binding.StepWalked += Time.unscaledDeltaTime;
            bool active = OptionalVisible(binding.Citizen) && ReferenceEquals(binding.Citizen.OptionalStop, stop);
            // Event copies preserve values but replace DTO instances; follow that same saved stop.
            if (!active && OptionalVisible(binding.Citizen) && binding.Citizen.OptionalStop!.SpotId == stop.SpotId
                && binding.Citizen.OptionalStop.StartMinute == stop.StartMinute)
            { binding.Optional = stop = binding.Citizen.OptionalStop; active = true; }
            // Only the logical stop ending (its time, or a Core cancellation such as rain) sends the body back.
            if (!binding.OptionalReturning && !active)
            {
                binding.OptionalReturning = true; binding.OptionalStandsShort = false; binding.StepWalked = 0;
                body.Stop(false);
                if (!body.SetPathTarget(stop.ReturnPosition, NativeTravelPace.WalkingMetresPerSecond))
                { body.Stop(true); return true; }
            }
            if (binding.OptionalReturning)
            {
                if (Position.Distance(body.Position, stop.ReturnPosition) < 0.4)
                {
                    binding.Optional = null;
                    // The departure tail ends here: follow the journey that committed meanwhile (ObserveWalker
                    // releases at a car or transit boundary). Without one the body goes in at the entrance.
                    if (binding.Journey == null) TryFollowCommitted(binding);
                    if (binding.Journey == null) return false;
                    body.Stop(false); binding.TargetLeg = -1; return true;
                }
                // Retry a temporarily blocked return; never retire a visible citizen at the spot.
                if (!body.Agent.hasPath)
                {
                    body.Stop(false);
                    if (!body.SetPathTarget(stop.ReturnPosition, NativeTravelPace.WalkingMetresPerSecond)) body.Stop(true);
                }
                body.Tick(); return true;
            }
            bool atSpot = !binding.OptionalStandsShort && Position.Distance(body.Position, stop.Position) <= 0.3;
            // Blocked walking stops short of the spot after the bound; the saved stop is unchanged.
            if (!atSpot && binding.StepWalked >= QueueStepSeconds) binding.OptionalStandsShort = true;
            if (atSpot || binding.OptionalStandsShort)
            {
                if (body.Agent.hasPath) body.Agent.ResetPath();
                body.Stop(true); body.Face(stop.Facing); return true;
            }
            body.Tick(); return true;
        }

        private bool TryBodyPlace(string id, out Place place)
        {
            if (!VendorNames.IsVendorPlace(id)) return places.TryGetValue(id, out place!);
            var row = simulation.State.Vendors.FindPlace(id);
            if (row?.Stand == null || row.State == VendorRowState.Tombstone) { place = null!; return false; }
            place = simulation.FindPlace(id); return true;
        }

        private void RefreshVendorMeals()
        {
            var food = simulation.State.Food;
            if (owner?.PhysicalVendorMarket == null || food == null
                || ReferenceEquals(food, vendorFood) && food.Residents.Count == vendorMealCount) return;
            vendorMeals.Clear();
            foreach (var meal in food.Residents) vendorMeals[meal.ResidentId] = meal;
            vendorFood = food; vendorMealCount = food.Residents.Count;
        }

        private void TrackVendorWaiter(CitizenState citizen)
        {
            if (citizen.Phase == CitizenPhase.AtPlace && citizen.Activity == Activity.Shop
                && VendorNames.IsVendorPlace(citizen.PlaceId) && ActiveJourneyIndex.ReceivesBody(citizen))
                vendorWaiters[citizen.Identity.Id] = citizen;
            else { vendorWaiters.Remove(citizen.Identity.Id); vendorGaveUp.Remove(citizen.Identity.Id); }
        }

        private bool VendorWaiting(CitizenState citizen, out VendorRow row, out VendorVehicle vehicle)
        {
            row = null!; vehicle = null!;
            var market = owner?.PhysicalVendorMarket;
            if (market == null || citizen.Phase != CitizenPhase.AtPlace || citizen.Activity != Activity.Shop
                || !vendorMeals.TryGetValue(citizen.Identity.Id, out var meal) || meal.PlaceId != citizen.PlaceId
                || (meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving)) return false;
            row = simulation.State.Vendors.FindPlace(citizen.PlaceId)!;
            if (row?.Stand == null || !row.Stand.Published || simulation.State.Minute >= row.Stand.ClosesAt
                || vendorGaveUp.TryGetValue(citizen.Identity.Id, out long failedOpening) && failedOpening == row.Stand.OpeningId
                || !market.CanServe(row.BusinessId) || meal.BusinessId != row.BusinessId) return false;
            vehicle = market.Coordinator.Image?.Find(row.VehicleId)!;
            return vehicle != null && vehicle.OpeningId == row.Stand.OpeningId
                && Position.Distance(vehicle.Window, row.Stand.Position) <= VendorRules.StandRadiusMetres;
        }

        private void PlanVendorQueues(Position focus, double radius)
        {
            foreach (var bound in bindings.Values.Where(b => b.VendorOpening > 0 && b.Step != QueueStep.Entering).OrderBy(b => b.Slot))
            {
                if (!VendorWaiting(bound.Citizen, out _, out var vehicle) || vehicle.OpeningId != bound.VendorOpening) continue;
                int? ahead = DwellPlanner.MoveUp(new HashSet<int>(Standing(bound.Line!).Select(p => p.Value)), bound.Slot);
                if (!ahead.HasValue || !bound.Person!.SetPathTarget(bound.Line!.Spots[ahead.Value], NativeTravelPace.WalkingMetresPerSecond)) continue;
                bound.Slot = ahead.Value; bound.Step = QueueStep.Approaching; bound.StepWalked = 0;
            }
            foreach (var citizen in vendorWaiters.Values.OrderBy(c => vendorMeals.TryGetValue(c.Identity.Id, out var m) ? m.ServiceAt : double.MaxValue)
                .ThenBy(c => c.Identity.Id, StringComparer.Ordinal))
            {
                if (ambient.IsReserved(citizen.Identity.Id) || !VendorWaiting(citizen, out var row, out var vehicle) || Position.Distance(row.Stand!.Position, focus) > radius
                    || !spots.TryVendorQueue(row, vehicle, out var line) || line == null) continue;
                var occupied = new HashSet<int>(Standing(line).Select(p => p.Value));
                foreach (var planned in queuers.Values) if (planned.Line.Key == line.Key) occupied.Add(planned.Slot);
                int slot = 0;
                if (bindings.TryGetValue(citizen.Identity.Id, out var binding) && binding.VendorOpening == vehicle.OpeningId
                    && binding.Line == line && binding.Step != QueueStep.Entering) slot = binding.Slot;
                else { while (occupied.Contains(slot) && slot < line.Spots.Count) slot++; }
                if (slot < line.Spots.Count) queuers[citizen.Identity.Id] = new QueuePlanEntry { Line = line, Slot = slot, VendorOpening = vehicle.OpeningId };
            }
        }

        private bool TryJoinVendorQueue(Binding binding)
        {
            if (!VendorWaiting(binding.Citizen, out var row, out var vehicle)
                || !spots.TryVendorQueue(row, vehicle, out var line) || line == null) return false;
            var occupied = new HashSet<int>(Standing(line).Select(p => p.Value));
            foreach (var pair in queuers) if (pair.Key != binding.Citizen.Identity.Id && pair.Value.Line.Key == line.Key) occupied.Add(pair.Value.Slot);
            int slot = 0; while (occupied.Contains(slot) && slot < line.Spots.Count) slot++;
            if (slot == line.Spots.Count) return false;
            if (!binding.Person!.SetPathTarget(line.Spots[slot], NativeTravelPace.WalkingMetresPerSecond))
            { vendorGaveUp[binding.Citizen.Identity.Id] = vehicle.OpeningId; return false; }
            binding.Line = line; binding.Slot = slot; binding.VendorOpening = vehicle.OpeningId;
            binding.Step = QueueStep.Approaching; binding.StepWalked = 0;
            if (Bound(binding)) queueBindings++;
            queued++; return ObserveVendorQueue(binding);
        }

        private bool AwaitVendorDeparture(Binding binding)
        {
            var citizen = binding.Citizen;
            if (binding.Journey != null || citizen.Phase != CitizenPhase.AtPlace || citizen.PlaceId != binding.Line!.Key
                || !Position.Finite(binding.VendorDepartureUntil) || simulation.State.Minute > binding.VendorDepartureUntil
                || !vendorMeals.TryGetValue(citizen.Identity.Id, out var meal) || meal.PlaceId != citizen.PlaceId) return false;
            return meal.Status == MealStatus.Paid || meal.Status == MealStatus.Missed
                || meal.Status == MealStatus.Declined || meal.Status == MealStatus.Uncertain;
        }

        private bool ObserveVendorQueue(Binding binding)
        {
            var body = binding.Person!; var line = binding.Line!;
            if (!body.Valid) return false;
            if (!body.Agent.isStopped) binding.StepWalked += Time.unscaledDeltaTime;
            bool waiting = VendorWaiting(binding.Citizen, out var row, out var vehicle) && vehicle.OpeningId == binding.VendorOpening
                && spots.TryVendorQueue(row, vehicle, out var current) && ReferenceEquals(line, current);
            if (binding.Step != QueueStep.Entering && (!waiting || binding.StepWalked >= QueueStepSeconds))
            {
                if (waiting && binding.StepWalked >= QueueStepSeconds) GiveUp(binding);
                binding.VendorDepartureUntil = Math.Min(binding.Citizen.NextEventMinute, binding.Citizen.ActivityUntilMinute) + 1;
                binding.Step = QueueStep.Entering; binding.StepWalked = 0;
                if (!body.SetPathTarget(line.Entrance, NativeTravelPace.WalkingMetresPerSecond)) return false;
            }
            if (binding.Step == QueueStep.Entering)
            {
                if (binding.StepWalked >= QueueStepSeconds) return false;
                if (Position.Distance(body.Position, line.Entrance) < 0.3)
                {
                    // Pavement has no doorway to hide a returned body. Keep a completed meal's
                    // body here until its scheduled departure commits, even while paused.
                    if (AwaitVendorDeparture(binding))
                    {
                        if (body.Agent.hasPath) body.Agent.ResetPath();
                        body.Stop(true); return true;
                    }
                    binding.VendorOpening = 0; binding.Line = null;
                    if (Bound(binding)) queueBindings--;
                    // The return starts at the stand: follow it from here with catch-up (departure tail).
                    if (binding.Journey == null) TryFollowCommitted(binding);
                    if (binding.Journey == null) return false;
                    binding.TargetLeg = -1;
                    body.Stop(false); return true;
                }
                body.Tick(); return true;
            }
            if (Position.Distance(body.Position, line.Spots[binding.Slot]) <= 0.3)
            {
                binding.Step = QueueStep.Standing;
                if (body.Agent.hasPath) body.Agent.ResetPath();
                body.Stop(true); body.Face(binding.Slot == 0 ? line.Door : line.Spots[binding.Slot - 1]);
                return true;
            }
            body.Tick(); return true;
        }

        /// <summary>F2d: the car's own leg and its cap, for the native command. The cap is the kernel's background
        /// coordinate (MaximumNominalMinutes while the vehicle holds a link, the logical pose after the kernel completed
        /// it), never beyond the car's own run of car legs. False when the car follows nothing (its follow ended).</summary>
        private bool CarCommand(PhysicalVehicleBody body, out Binding binding, out PhysicalJourneySample own, out double cap)
        {
            own = default; cap = 0;
            if (!bindings.TryGetValue(body.CitizenId, out binding!) || !ReferenceEquals(binding.Car, body) || binding.Journey == null) return false;
            own = WalkerFollow.Sample(binding.Journey, binding.BodyMinutes);
            if (own.Mode != TravelMode.Car || !own.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
            cap = CarFollow.Cap(binding.Journey, binding.BodyMinutes, LogicalCarMinutes(binding.Citizen, binding.Journey));
            return true;
        }
        // The kernel's coordinate while the vehicle holds a link, the logical pose after the kernel completed it, and the
        // route's end once the journey itself has ended (the car then drives on only to its parking point).
        private double LogicalCarMinutes(CitizenState citizen, Route route)
        {
            if (citizen.Phase != CitizenPhase.Travelling || !ReferenceEquals(citizen.Journey, route)) return route.Minutes;
            return simulation.TryGetTrafficPermission(citizen.Identity.Id, out var permission) && permission.LinkId.HasValue && !permission.IsCompleted
                ? permission.MaximumNominalMinutes : simulation.SampleJourney(citizen).ElapsedMinutes;
        }
        private static readonly Position HoldDirection = new Position(1, 0, 0);
        /// <summary>F2d native command: Gley drives at its own pace toward the kernel's coordinate on the car's own leg and
        /// is braked so it never passes it. A corner Gley has reported is driven through on the next leg's limit; the
        /// corner handoff holds the turn while the kernel is still short of it, and a fading car stands.</summary>
        private PhysicalTrafficCommandLimit CommandLimit(PhysicalVehicleBody body)
        {
            if (!CarCommand(body, out var binding, out var own, out double cap) || own.Stationary)
                return new PhysicalTrafficCommandLimit(own.Stationary ? HoldDirection : own.Direction, 0, true);
            if (!float.IsNaN(binding.FadeOutSince) || binding.CornerHandoff.MustHold(own, cap))
                return new PhysicalTrafficCommandLimit(own.Direction, 0, true);
            var leg = own;
            if (binding.ReachedCarLeg != own.LegIndex && body.TryPeekReached(own.To, out _))
            {
                var next = WalkerFollow.Sample(binding.Journey!, own.ElapsedMinutes + own.RemainingLegMinutes);
                if (next.Mode != TravelMode.Car || next.Stationary || next.LegIndex == own.LegIndex)
                    return new PhysicalTrafficCommandLimit(own.Direction, 0, true);
                leg = next;
            }
            return new PhysicalTrafficCommandLimit(leg.Direction, PhysicalTrafficMotion.MaximumAdvance(leg, body.Position, cap), false);
        }
        private bool NativeTargetPermission(PhysicalVehicleBody body, int nativeTarget)
        {
            if (!CarCommand(body, out _, out var own, out double cap)) return false;
            // A native loop must not pop a later corner before the kernel's coordinate reaches this one.
            return own.ServiceId.EndsWith(":" + nativeTarget.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && PhysicalTrafficCornerPermission.CanReachEndpoint(own, cap);
        }

        private bool Observe(Binding binding)
        {
            if (binding.Person != null && !binding.Person.Valid || binding.Car != null && !binding.Car.Valid) return false;
            if (binding.VendorOpening > 0) return ObserveVendorQueue(binding);
            if (binding.Optional != null) return ObserveOptional(binding);
            if (binding.Person != null && binding.Journey != null) return ObserveWalker(binding);
            if (binding.Car != null) return binding.Journey != null && ObserveCar(binding);
            if (TryJoinOptional(binding)) return ObserveOptional(binding);
            if (binding.AwaitingArrival) return ObserveArrival(binding);
            if (binding.Queue.HasValue) return ObserveQueue(binding);
            if (!Outdoor(binding.Citizen) || binding.Citizen.Phase == CitizenPhase.Travelling) return false;
            binding.Person!.Stop(true); return true;
        }

        /// <summary>
        /// F2c walker follow (plan Decision 2). Each frame: sample the logical pose, project the body onto its own leg,
        /// take the along-route lag, and walk at native pace up to 1.5 m behind, faster (up to 1.5x) beyond it, never past
        /// the logical pose. Beyond 8 m, or once the logical leg is a car, bus or subway, the body is corrected
        /// (<see cref="CorrectWalker"/>). A changed journey reference or phase ends the follow; the logical arrival of
        /// this journey starts the arrival tail instead. Reads only; nothing is written to the simulation.
        /// </summary>
        private bool ObserveWalker(Binding binding)
        {
            var body = binding.Person!; var citizen = binding.Citizen; var route = binding.Journey!;
            bool following = citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, route);
            if (!following && !binding.Tail)
            {
                if ((citizen.Phase != CitizenPhase.AtPlace && citizen.Phase != CitizenPhase.WaitingForOpening)
                    || !TryBodyPlace(citizen.PlaceId, out var place)
                    || Position.Distance(place.Position, route.Legs[route.Legs.Count - 1].To) > 3) return false;
                binding.Tail = true; binding.TailSince = followClock; binding.HurrySince = float.NaN; binding.Fading = false;
            }
            PhysicalJourneySample logical = following ? simulation.SampleJourney(citizen) : WalkerFollow.Sample(route, route.Minutes);
            if (!float.IsNaN(binding.FadeOutSince)) return FadeOut(binding, logical.Position);
            binding.BodyMinutes = WalkerFollow.Advance(route, binding.BodyMinutes, logical.ElapsedMinutes, body.Position);
            double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
            if (binding.LagFrame != Time.frameCount) { binding.LagFrame = Time.frameCount; Telemetry.RecordLag(lag); }
            var own = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (binding.Tail) return ObserveArrivalTail(binding, own, lag);
            // The body reached the car, bus or subway it follows: hand over to a body of the new kind (or none).
            if (own.Mode != TravelMode.Walk) { binding.Handoff = true; Telemetry.Handoffs++; return false; }
            if (!WalkerFollow.NeedsCorrection(lag, logical.Mode)) { binding.HurrySince = float.NaN; binding.Fading = false; }
            else if (!CorrectWalker(binding, logical, own, lag)) return false;
            else if (!float.IsNaN(binding.FadeOutSince)) return FadeOut(binding, logical.Position);
            else if (float.IsNaN(binding.HurrySince))
            {
                // Moved to the logical pose off screen: follow on from there.
                own = WalkerFollow.Sample(route, binding.BodyMinutes);
                lag = 0;
            }
            return WalkLeg(binding, own, lag, logical);
        }

        /// <summary>
        /// Off screen, the body is moved to the logical pose (or released if the person is no longer walking). On screen
        /// the Q3 default applies: hurry for up to 10 s, then stop at the next corner and fade out there; it reappears at
        /// the logical pose once that pose is out of view. Until it reaches a corner it keeps hurrying, and if its
        /// materials cannot fade it keeps following until it is off screen and is moved then. Nobody jumps on screen.
        /// Every correction is recorded with its on-screen flag. False releases the body. Bounds run on the follow
        /// clock, which stops while the city clock does.
        /// </summary>
        private bool CorrectWalker(Binding binding, PhysicalJourneySample logical, PhysicalJourneySample own, double lag)
        {
            var body = binding.Person!; string id = binding.Citizen.Identity.Id;
            Position before = body.Position;
            if (!body.OnScreen)
            {
                if (logical.Mode == TravelMode.Walk && body.Warp(logical.Position))
                {
                    Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.Move, before, body.Position, false);
                    binding.BodyMinutes = logical.ElapsedMinutes; binding.TargetLeg = -1;
                    binding.HurrySince = float.NaN; binding.Fading = false;
                    return true;
                }
                binding.Handoff = logical.Mode != TravelMode.Walk;
                // Close behind a person who has just boarded is an ordinary handoff, not a lag correction.
                if (binding.Handoff && lag <= WalkerFollow.CorrectionMetres) { Telemetry.Handoffs++; return false; }
                Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.LagRelease, before, logical.Position, false);
                return false;
            }
            if (float.IsNaN(binding.HurrySince)) binding.HurrySince = followClock;
            float hurried = followClock - binding.HurrySince;
            if (!binding.Fading && hurried >= WalkerFollow.HurrySeconds) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
            if (!binding.Fading) return true;
            if ((own.LegIndex != binding.FadeLeg || own.Stationary || Position.Distance(before, own.To) <= 0.3) && body.CanFade)
                BeginFade(binding, true);
            return true;
        }

        private void BeginFade(Binding binding, bool onScreen)
        {
            binding.FadeOutSince = followClock; binding.FadeOnScreen = onScreen; binding.FadeFrom = binding.Person!.Position;
            binding.Person.Pace(0);
        }

        /// <summary>The fade itself: the body stands and its renderers fade over <see cref="WalkerFollow.FadeOutSeconds"/>,
        /// then it is released. Its person reappears through selection only once the logical pose is out of view.</summary>
        private bool FadeOut(Binding binding, Position after)
        {
            var body = binding.Person!;
            body.Pace(0);
            float alpha = 1 - (followClock - binding.FadeOutSince) / (float)WalkerFollow.FadeOutSeconds;
            if (alpha > 0) { body.SetFade(alpha); return true; }
            body.SetFade(0);
            Telemetry.Record(Time.frameCount, binding.Citizen.Identity.Id, WalkerCorrectionKind.Fade, binding.FadeFrom, after, binding.FadeOnScreen);
            fadedUntilHidden[binding.Citizen.Identity.Id] = (binding.Citizen, binding.Journey, binding.Tail ? binding.Citizen.PlaceId : null);
            return false;
        }

        /// <summary>Walk at the pace the lag asks for. The native destination is bounded by the logical pose: on the
        /// logical pose's leg it is that pose (refreshed every <see cref="WalkerFollow.RetargetMetres"/>), on an earlier
        /// leg the leg's corner, so the agent stops before the pose whatever the frame durations, even while the logical
        /// clock stands still. The speed is also capped at the lag per frame.</summary>
        private bool WalkLeg(Binding binding, PhysicalJourneySample own, double lag, PhysicalJourneySample logical)
        {
            var body = binding.Person!;
            double pace = float.IsNaN(binding.HurrySince) ? WalkerFollow.PaceFactor(lag)
                : lag > WalkerFollow.StopMetres ? WalkerFollow.MaximumPaceFactor : 0;
            if (own.Stationary || own.AtEnd) { body.Pace(0); return true; }
            bool pose = logical.Mode == TravelMode.Walk && !logical.Stationary && own.LegIndex == logical.LegIndex;
            Position goal = pose ? logical.Position : own.To;
            // A pose at the leg's own corner (the door at the end of the route) is always re-aimed, so a destination
            // cached a few decimetres short can never strand the body before the door.
            bool corner = Position.Distance(goal, own.To) <= 1e-6;
            bool retarget = binding.TargetLeg != own.LegIndex || binding.TargetIsPose != pose
                || pose && (Position.Distance(goal, binding.TargetPoint) >= WalkerFollow.RetargetMetres
                    || corner && Position.Distance(goal, binding.TargetPoint) > 1e-6);
            if (retarget && !(pose && lag <= WalkerFollow.StopMetres))
            {
                // The previous destination on this route (an earlier pose or corner) is never past the pose: while a
                // re-aim fails, or waits out its one-second retry, the body keeps walking to it.
                bool previous = binding.TargetLeg >= 0 && binding.TargetLeg <= own.LegIndex;
                if (Time.unscaledTime < binding.NextTargetAttempt) { if (!previous) { body.Pace(0); return true; } }
                else
                {
                    bool aimed = false;
                    // A pose between corners is projected onto the walkable surface first (a straight leg can cross a
                    // kerb or a crowned road without a NavMesh corner); a projection that lands ahead of the pose along
                    // the leg is not used. Then the pose is tried as given. Each SetTarget is one path calculation.
                    if (pose && !corner && body.TryProjectPose(goal, out var projected)
                        && WalkerFollow.NotAheadOnLeg(logical.From, logical.To, goal, projected))
                    { Telemetry.PathRequests++; aimed = body.SetTarget(projected, NativeTravelPace.WalkingMetresPerSecond); }
                    if (!aimed) { Telemetry.PathRequests++; aimed = body.SetTarget(goal, NativeTravelPace.WalkingMetresPerSecond); }
                    if (aimed) { binding.TargetLeg = own.LegIndex; binding.TargetIsPose = pose; binding.TargetPoint = goal; binding.NextTargetAttempt = 0; }
                    else
                    {
                        binding.NextTargetAttempt = Time.unscaledTime + 1; blockedSamples++;
                        if (!previous) { body.Stop(true); return true; }
                    }
                }
            }
            else if (retarget) { body.Pace(0); return true; }
            body.Pace(WalkerFollow.CommandSpeed(NativeTravelPace.WalkingMetresPerSecond * pace, lag, Time.deltaTime));
            return true;
        }

        /// <summary>
        /// Arrival tail: the logical arrival is processed, and the visible walker keeps walking to the door. At the door it
        /// joins a vendor line, an optional stop or the door queue (ObserveArrival), or goes in (released). The tail is
        /// bounded at 10 s on the follow clock: off screen it is then released as a lag release; on screen a fade-capable
        /// body stops at its next intermediate corner and fades out there, and on the last leg it still walks to the door.
        /// Nobody is moved on screen. A journey committed meanwhile is followed from the door; one that starts elsewhere
        /// ends the tail at once off screen, or at the door or a corner fade on screen.
        /// </summary>
        private bool ObserveArrivalTail(Binding binding, PhysicalJourneySample own, double lag)
        {
            var body = binding.Person!; var citizen = binding.Citizen; var route = binding.Journey!;
            Position door = route.Legs[route.Legs.Count - 1].To;
            var next = citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null && !ReferenceEquals(citizen.Journey, route)
                ? citizen.Journey : null;
            bool fromDoor = next != null && next.Legs.Count > 0 && Position.Distance(next.Legs[0].From, door) <= 3;
            bool atDoor = own.AtEnd || own.Mode != TravelMode.Walk;
            if (atDoor)
            {
                if (fromDoor) { Follow(binding, next!, 0); return true; }
                if (next != null) return false;
                binding.Tail = false; binding.Journey = null;
                binding.AwaitingArrival = true; binding.AwaitMinute = simulation.State.Minute; binding.AwaitJourney = route;
                return ObserveArrival(binding);
            }
            bool expired = followClock - binding.TailSince >= WalkerFollow.ArrivalTailSeconds;
            if ((expired || next != null && !fromDoor) && !body.OnScreen)
            {
                Telemetry.Record(Time.frameCount, citizen.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, door, false);
                return false;
            }
            // On screen past the bound, the Q3 flow applies: a body whose materials can fade stops at the next
            // intermediate corner and fades out there. On the last leg it walks on to the door, so the door queue,
            // a vendor line or entry still take it. An opaque body keeps walking until it is out of view.
            if (expired && body.CanFade)
            {
                if (!binding.Fading) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
                bool corner = own.LegIndex != binding.FadeLeg
                    || own.LegIndex < route.Legs.Count - 1 && (own.Stationary || Position.Distance(body.Position, own.To) <= 0.3);
                if (corner) { BeginFade(binding, true); return FadeOut(binding, door); }
            }
            return WalkLeg(binding, own, Math.Max(lag, WalkerFollow.StopMetres * 2), WalkerFollow.Sample(route, route.Minutes));
        }

        /// <summary>
        /// F2d car follow (plan Decision 2). Each frame: sample the logical pose (the kernel's background coordinate),
        /// advance the car's own progress (projection on its own leg; a road corner only with Gley's reached evidence),
        /// take the along-route lag and count the phase state. Up to 5 m the car follows, beyond it catches up at Gley's
        /// pace, never past the kernel's coordinate (CommandLimit). Beyond 30 m, or once the kernel has completed the
        /// traffic leg while the car still drives, the car is corrected (<see cref="CorrectCar"/>). Reaching its parking
        /// point hands over to a walker at the logical pose. When the journey ends (arrival or replacement) while the car
        /// still drives, an on-screen car drives on to its parking point under the same correction rule and an
        /// off-screen one is released. Reads only; nothing is written to the simulation or the kernel.
        /// </summary>
        private bool ObserveCar(Binding binding)
        {
            var body = binding.Car!; var citizen = binding.Citizen; var route = binding.Journey!;
            bool following = citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, route);
            var logical = following ? simulation.SampleJourney(citizen) : WalkerFollow.Sample(route, route.Minutes);
            if (!float.IsNaN(binding.FadeOutSince)) return FadeOutCar(binding, logical.Position);
            if (!following && !body.OnScreen)
            {
                Telemetry.Record(Time.frameCount, citizen.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, logical.Position, false, car: true);
                return false;
            }
            var permission = default(TrafficVehicleSnapshot);
            bool admitted = following && simulation.TryGetTrafficPermission(citizen.Identity.Id, out permission)
                && permission.LinkId.HasValue && !permission.IsCompleted;
            double logicalMinutes = admitted ? permission.MaximumNominalMinutes : logical.ElapsedMinutes;
            var own = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (own.Mode == TravelMode.Car && !own.Stationary && binding.ReachedCarLeg != own.LegIndex
                && body.TryPeekReached(own.To, out var marker) && body.ConsumeReached(own.To))
            { binding.ReachedCarLeg = own.LegIndex; binding.ReachedCarMarker = marker; }
            binding.BodyMinutes = CarFollow.Advance(route, binding.BodyMinutes, CarFollow.Cap(route, binding.BodyMinutes, logicalMinutes),
                body.Position, binding.ReachedCarLeg);
            var after = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (after.LegIndex == own.LegIndex + 1 && binding.ReachedCarLeg == own.LegIndex)
                binding.CornerHandoff.Begin(own, after, binding.ReachedCarMarker);
            double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
            if (binding.LagFrame != Time.frameCount)
            {
                binding.LagFrame = Time.frameCount; Telemetry.RecordCarLag(lag);
                if (lag > CarFollow.CatchUpMetres) Telemetry.CarCatchUpFrames++;
            }
            // The car reached its parking point (the end of its native road legs; the kerb connector and the parking pause
            // after it are not driven by a native car): a walker takes over at the logical pose.
            if (after.AtEnd || !CarFollow.IsRoad(after))
            { binding.Handoff = true; Telemetry.Handoffs++; Telemetry.CarHandoffs++; return false; }
            ObservePhase(binding, following, admitted, permission, lag);
            // A logical pose past the kernel's sections but still in this car's run (the trailing connector or the parking
            // pause) is the car's own journey: the car follows on to its parking point without a correction.
            if (!CarFollow.NeedsCorrection(lag, following && CarFollow.SameCarRun(route, binding.BodyMinutes, logical.ElapsedMinutes)))
            { binding.HurrySince = float.NaN; binding.Fading = false; return true; }
            return CorrectCar(binding, logical, after, lag);
        }

        /// <summary>
        /// Off screen, a native car cannot be placed: it is released and a body of the right kind is acquired at the
        /// logical pose at the next selection, once that pose is out of view (close behind a person who has just parked
        /// it is an ordinary handoff). On screen the Q3 default applies: the car keeps catching up for up to 10 s, then
        /// stops at its next corner and fades out there; it reappears at the logical pose once that pose is out of view.
        /// A car whose materials cannot fade keeps following until it is off screen. Nothing jumps on screen. Every
        /// correction is recorded with its on-screen flag. False releases the body.
        /// </summary>
        private bool CorrectCar(Binding binding, PhysicalJourneySample logical, PhysicalJourneySample own, double lag)
        {
            var body = binding.Car!; var citizen = binding.Citizen; string id = citizen.Identity.Id;
            if (!body.OnScreen)
            {
                binding.Handoff = true;
                if (logical.Mode != TravelMode.Car && lag <= CarFollow.CorrectionMetres)
                {
                    Telemetry.Handoffs++; Telemetry.CarHandoffs++;
                    // A walker some way ahead of the hidden car appears only where the player cannot see it appear.
                    if (lag > CarFollow.CatchUpMetres) fadedUntilHidden[id] = (citizen, binding.Journey, null);
                    return false;
                }
                Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.LagRelease, body.Position, logical.Position, false, car: true);
                fadedUntilHidden[id] = (citizen, binding.Journey, null);
                return false;
            }
            if (float.IsNaN(binding.HurrySince)) binding.HurrySince = followClock;
            if (!binding.Fading && followClock - binding.HurrySince >= CarFollow.HurrySeconds) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
            if (binding.Fading && body.CanFade
                && (own.LegIndex != binding.FadeLeg || Position.Distance(body.Position, own.To) <= CarFollow.CornerMetres))
            { binding.FadeOutSince = followClock; binding.FadeOnScreen = true; binding.FadeFrom = body.Position; }
            return true;
        }

        /// <summary>The car fade: it stands (CommandLimit holds it) and its renderers fade over
        /// <see cref="CarFollow.FadeOutSeconds"/>, then it is released. Its person reappears at the logical pose through
        /// selection only once that pose is out of view.</summary>
        private bool FadeOutCar(Binding binding, Position after)
        {
            var body = binding.Car!;
            float alpha = 1 - (followClock - binding.FadeOutSince) / (float)CarFollow.FadeOutSeconds;
            if (alpha > 0) { body.SetFade(alpha); return true; }
            body.SetFade(0);
            Telemetry.Record(Time.frameCount, binding.Citizen.Identity.Id, WalkerCorrectionKind.Fade, binding.FadeFrom, after, binding.FadeOnScreen, car: true);
            // A person whose journey ended (arrived) while the car still drove keeps the restriction at the place it
            // arrived at, as an arrival-tail walker fade does, so a door queue or an optional stop there gets no body in view.
            var citizen = binding.Citizen;
            bool arrived = !(citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, binding.Journey))
                && (citizen.Phase == CitizenPhase.AtPlace || citizen.Phase == CitizenPhase.WaitingForOpening);
            fadedUntilHidden[citizen.Identity.Id] = (citizen, binding.Journey, arrived ? citizen.PlaceId : null);
            return false;
        }

        /// <summary>Decision 6: count a standing visible car held by the kernel at a native green, or stopped by the
        /// native light at a kernel green. Both compare the native and kernel phases now, at the same junction: the
        /// kernel vehicle's own red at its link end, or the kernel signal of the section the car itself is in. Measured
        /// from Gley's target waypoint and the kernel; it changes no signal phase and writes nothing logical.</summary>
        private void ObservePhase(Binding binding, bool following, bool admitted, TrafficVehicleSnapshot permission, double lag)
        {
            var body = binding.Car!;
            bool stopped = body.Speed < CarFollow.StoppedMetresPerSecond;
            bool? red = stopped ? vehicles.NativeRed(body) : null;
            bool held = admitted && !permission.SignalGreen && !permission.CanCrossBoundary
                && permission.NominalMinutes >= permission.LinkEndNominalMinutes - 1e-9;
            bool green = red == true && following
                && simulation.TryGetTrafficSignalGreen(binding.Citizen, binding.BodyMinutes, out bool atBody) && atBody;
            var kind = CarFollow.Classify(stopped, red, lag, held, green);
            Telemetry.RecordPhase(kind, binding.Phase, followDelta);
            binding.Phase = kind;
        }

        // Inspector picking: read-only, allocation-free projection of active own walkers. Cars,
        // ambient/RLO-reserved actors and released or reused bodies are never candidates.
        private const float PickHalfWidthPixels = 8f;
        public void PickWalker(Camera camera, float mouseX, float mouseY, ref ResidentPickAccumulator best)
        {
            if (!active || disposed || camera == null) return;
            foreach (var binding in bindings.Values)
            {
                var body = binding.Person;
                if (body == null || binding.Car != null || !body.Valid || body.IdentityId != binding.Citizen.Identity.Id
                    || ambient.IsReserved(body.IdentityId)
                    || !Project(camera, body, out float footX, out float footY, out float headX, out float headY, out float depth)) continue;
                if (ResidentPickAccumulator.Hit(mouseX, mouseY, footX, footY, headX, headY, PickHalfWidthPixels, out float score))
                    best.Consider(body.IdentityId, body.Generation, score, depth);
            }
        }
        /// <summary>Screen rectangle of the selected walker if that exact body (identity and generation) is still active.</summary>
        public bool TryWalkerRect(Camera camera, string identityId, long generation, out Rect rect)
        {
            rect = default;
            if (!active || disposed || camera == null || identityId == null || !bindings.TryGetValue(identityId, out var binding)) return false;
            var body = binding.Person;
            if (body == null || binding.Car != null || body.Generation != generation || !body.Valid || ambient.IsReserved(identityId)
                || !Project(camera, body, out float footX, out float footY, out float headX, out float headY, out _)) return false;
            float height = footY - headY, half = Math.Max(PickHalfWidthPixels, height * 0.3f);
            float centre = (footX + headX) * 0.5f;
            rect = new Rect(centre - half, headY - height * 0.1f, half * 2, height * 1.2f);
            return true;
        }
        private static bool Project(Camera camera, PhysicalPedestrianBody body, out float footX, out float footY, out float headX, out float headY, out float depth)
        {
            footX = footY = headX = headY = depth = 0;
            var transform = body.Character.transform;
            Vector3 feet = transform.position;
            var capsule = body.Capsule;
            float height = capsule != null ? Math.Max(1.2f, capsule.height * transform.lossyScale.y) : 1.8f;
            Vector3 foot = camera.WorldToScreenPoint(feet), head = camera.WorldToScreenPoint(feet + Vector3.up * height);
            if (foot.z <= 0 || head.z <= 0) return false;
            // IMGUI coordinates grow downwards.
            footX = foot.x; footY = Screen.height - foot.y; headX = head.x; headY = Screen.height - head.y; depth = foot.z;
            return true;
        }

        /// <summary>
        /// F2e (plan Decision 4): the visible world's only write into Core. A following walker or car that the player blocks
        /// is reported to the simulator as a bounded, retroactive player hold, which Core applies at once; every applied hold
        /// is one Player.log stream line. Detection reads only the player's own pose (PlayerContact).
        /// </summary>
        private void ObservePlayerContact(Binding binding, in PlayerPose player)
        {
            var citizen = binding.Citizen; var route = binding.Journey;
            if (route == null || binding.Tail || !float.IsNaN(binding.FadeOutSince) || citizen.Phase != CitizenPhase.Travelling
                || !ReferenceEquals(citizen.Journey, route)) return;
            bool car = binding.Car != null;
            Position at = binding.Person?.Position ?? binding.Car!.Position;
            // Out of reach and no open track: nothing to observe (the common case, one distance test per body).
            if (!PlayerContact.InReach(at, player, car) && !contact.Tracking(citizen.Identity.Id)) return;
            double minute = simulation.State.Minute;
            var own = WalkerFollow.Sample(route, Math.Min(route.Minutes, binding.BodyMinutes));
            double report = contact.Observe(citizen.Identity.Id, route, car, minute - citizen.DepartureMinute, binding.BodyMinutes,
                at, own.From, own.To, player, minute, Time.frameCount);
            if (report > 0)
                contact.Settle(citizen.Identity.Id, simulation.HoldForPlayer(citizen.Identity.Id, report, car ? PlayerHoldCause.PlayerVehicle : PlayerHoldCause.PlayerFoot));
        }

        public void ObserveForSave()
        {
            vehicles.AssertHealthy();
            simulation.RefreshOptionalStops();
            foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
        }
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
