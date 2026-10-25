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
    /// <summary>Rendering leases follow the population; measured body movement owns visible journey progress.</summary>
    internal sealed class PhysicalPopulation : IDisposable
    {
        private sealed class Binding
        {
            public CitizenState Citizen = null!;
            public PhysicalJourneyLease? Lease;
            public PhysicalPedestrianBody? Person;
            public PhysicalVehicleBody? Car;
            public int TargetLeg = -1;
            public int ReachedCarLeg = -1;
            public Position ReachedCarMarker;
            public readonly PhysicalCornerHandoff CornerHandoff = new PhysicalCornerHandoff();
            public double ObservedMinute;
            public float NextTargetAttempt;
            // A queue-at-opening stand, its line and its place in it. Null for every other binding.
            public Dwell? Queue;
            public DoorQueue? Line;
            public int Slot = -1;
            public QueueStep Step;
            // Seconds this body has actually been walking in its current queue step. Pauses and
            // red lights stop the agent, so they never count toward the step's bound.
            public float StepWalked;
            public int EntryAttempts;
            // A walker whose journey ended at a closed door keeps its body until the simulator
            // processes the arrival, then joins the queue with it (TryAwaitArrival).
            public bool AwaitingArrival;
            public double AwaitMinute;
            public Route? AwaitJourney;
        }
        private enum QueueStep { Approaching, Standing, Entering }
        private sealed class QueuePlanEntry
        {
            public Dwell Dwell; public DoorQueue Line = null!; public int Slot;
        }
        private readonly CitySimulator simulation;
        private readonly MultimodalRoutes routes;
        private readonly PhysicalPedestrians pedestrians;
        private readonly PhysicalVehicles vehicles;
        private readonly AmbientPedestrians ambient;
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> attempted = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places;
        private readonly DwellSpots spots = new DwellSpots();
        private readonly Dictionary<string, QueuePlanEntry> queuers = new Dictionary<string, QueuePlanEntry>(StringComparer.Ordinal);
        // Queue dwells whose citizen could not reach a spot: they wait inside for the rest of that dwell.
        private readonly Dictionary<string, double> gaveUp = new Dictionary<string, double>(StringComparer.Ordinal);
        private const float QueueStepSeconds = 20f;
        private const int EntryAttemptFrames = 30;
        // One uncached door is sampled per frame, shared by the queue plan and arriving walkers.
        private int sampledFrame = -1;
        private readonly IModLogger log;
        private readonly int pedestrianLimit, vehicleLimit;
        private readonly double radius;
        private float nextSelection;
        private bool active, disposed;
        private long acquired, released, blockedSamples, failedAcquisitions, trafficOvershoots, queued;

        public PhysicalPopulation(Transform parent, CitySimulator simulation, MultimodalRoutes routes, Assembly dependency,
            IModLogger log, int pedestrianLimit, int vehicleLimit, double radius)
        {
            this.simulation = simulation; this.routes = routes; this.log = log;
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
                log.Info("Physical population active: ordinary pedestrian and vehicle spawns replaced; RLO scenes, customers, workers and service vehicles retained.");
            }
            catch { Dispose(); throw; }
        }

        public string Diagnostics => "physical=" + active + ", pedestrians=" + bindings.Values.Count(b => b.Person != null)
            + ", cars=" + bindings.Values.Count(b => b.Car != null) + ", acquired=" + acquired + ", released=" + released
            + ", blockedSamples=" + blockedSamples + ", unavailable=" + failedAcquisitions + ", trafficOvershoots=" + trafficOvershoots
            + ", queueing=" + bindings.Values.Count(b => b.Queue != null) + ", queued=" + queued + ", walkersResumed=" + ambient.Resumed
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
            finally { Suspend(); }
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
            ambient.Update();
            if (!ambient.Active) throw new InvalidOperationException("Pedestrian ownership guard failed: " + ambient.LastError);
            vehicles.Update();
            if (NativeTravelPace.HiddenTravel) { SuspendBeforeNativeFastForward(); return; }
            Position focus = NativeTravelPace.Focus;
            if (!focus.IsFinite) { Suspend(); return; }
            foreach (var binding in bindings.Values.ToArray())
            {
                if (ambient.IsReserved(binding.Citizen.Identity.Id)) { Release(binding); continue; }
                if (!Observe(binding)) { if (!TryAwaitArrival(binding)) Release(binding); continue; }
                Position position = binding.Person?.Position ?? binding.Car!.Position;
                if (Position.Distance(position, focus) > radius * 1.4) Release(binding);
            }
            if (Time.unscaledTime < nextSelection || !routes.ReadyForPopulation) return;
            nextSelection = Time.unscaledTime + .25f;
            // The bounded view is selected deterministically. No filler identities or unrelated trips are created.
            int people = bindings.Values.Count(b => b.Person != null), cars = bindings.Count - people;
            int attempts = 0;
            PlanQueues(focus);
            foreach (var candidate in simulation.State.Citizens.Where(c => c.NextEventMinute > simulation.State.Minute
                    && !bindings.ContainsKey(c.Identity.Id) && !ambient.IsReserved(c.Identity.Id)
                    && (!attempted.TryGetValue(c.Identity.Id, out float at) || Time.unscaledTime - at >= 2))
                .Select(c => (Citizen: c, Position: simulation.PositionOf(c)))
                .Where(c => Position.Distance(c.Position, focus) <= radius)
                .OrderBy(c => attempted.TryGetValue(c.Citizen.Identity.Id, out float at) ? at : float.NegativeInfinity)
                .ThenBy(c => Position.Distance(c.Position, focus)).ThenBy(c => c.Citizen.Identity.Id, StringComparer.Ordinal))
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
                // Waiting sources own no finite road storage. Refuse before borrowing a native body.
                if (car && (!simulation.TryGetTrafficPermission(citizen.Identity.Id, out var admission)
                    || !admission.LinkId.HasValue || admission.IsCompleted)) continue;
                if (car && !routes.TryPhysicalRoadSample(citizen, sample, out road))
                    continue;
                var binding = new Binding { Citizen = citizen, ObservedMinute = simulation.State.Minute };
                Position at = candidate.Position;
                // A waiter who was indoors comes out through the entrance and walks to their spot.
                if (queuer) { binding.Queue = queue!.Dwell; binding.Line = queue.Line; binding.Slot = queue.Slot; at = queue.Line.Entrance; }
                if (moving && !simulation.TryAcquirePhysicalJourney(citizen.Identity.Id, out binding.Lease)) continue;
                bool bound = false;
                try
                {
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
                    bindings.Add(citizen.Identity.Id, binding); bound = true; acquired++;
                }
                finally
                {
                    if (!bound)
                    {
                        try { if (binding.Lease != null) simulation.ReleasePhysicalJourney(binding.Lease); }
                        finally { ReleaseBodies(binding); }
                    }
                }
                // No walk from the entrance to the spot: stay indoors rather than stand in the doorway.
                if (queuer && !binding.Person!.SetPathTarget(binding.Line!.Spots[binding.Slot], NativeTravelPace.WalkingMetresPerSecond))
                { GiveUp(binding); Release(binding); continue; }
                if (!Observe(binding)) { Release(binding); continue; }
                if (car) cars++; else people++;
                if (queuer) queued++;
            }
            if (attempted.Count > simulation.State.Citizens.Count + 128)
            {
                var ids = new HashSet<string>(simulation.State.Citizens.Select(c => c.Identity.Id), StringComparer.Ordinal);
                foreach (var id in attempted.Keys.Where(id => !ids.Contains(id)).ToArray()) attempted.Remove(id);
            }
        }

        // Waiting for a route or for an opening is indoors. Only a queue-at-opening dwell
        // (PlanQueues) puts a waiting citizen in the street, at a spot beside the door.
        private bool Outdoor(CitizenState c)
            => c.Phase == CitizenPhase.AtPlace && c.Activity == Activity.Leisure && places[c.PlaceId].Id.StartsWith("scene-", StringComparison.Ordinal);

        /// <summary>
        /// Which waiting citizens near the focus stand in a door queue now, and in which spot. One
        /// line per door: places sharing an entrance share it. A body already in a line keeps its
        /// spot until it leaves (DwellPlanner.AssignQueueSlots). A full line leaves the rest indoors.
        /// </summary>
        private void PlanQueues(Position focus)
        {
            queuers.Clear();
            double now = simulation.State.Minute;
            bool raining = RainHelper.isRaining;
            var planned = new Dictionary<string, (Place Place, List<Dwell> Dwells)>(StringComparer.Ordinal);
            foreach (var citizen in simulation.State.Citizens)
            {
                if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)
                    || Position.Distance(place.Position, focus) > radius) continue;
                var dwell = DwellPlanner.QueueAtOpening(citizen, place, simulation.State.Seed, now, raining);
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
                var occupied = new HashSet<int>(inLine.Select(b => b.Slot));
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
            if (gaveUp.Count > 256)
                foreach (var stale in gaveUp.Where(p => p.Value <= simulation.State.Minute).Select(p => p.Key).ToList()) gaveUp.Remove(stale);
        }

        private IEnumerable<KeyValuePair<string, int>> Standing(DoorQueue line) => bindings.Values
            .Where(b => b.Queue.HasValue && b.Line != null && b.Line.Key == line.Key && b.Step != QueueStep.Entering)
            .Select(b => new KeyValuePair<string, int>(b.Citizen.Identity.Id, b.Slot)).ToList();

        private bool TryLine(Place place, out DoorQueue line)
        {
            bool sampled = sampledFrame == Time.frameCount;
            bool found = spots.TryQueue(place, ref sampled, out var queue) && queue != null;
            if (sampled) sampledFrame = Time.frameCount;
            line = queue!;
            return found;
        }

        /// <summary>
        /// A visible walker that has just reached a closed shop or venue door. The journey lease is
        /// already returned, but the simulator processes the arrival on its next event batch. Keep
        /// the body standing at the door until then, so it can join the queue instead of vanishing.
        /// </summary>
        private bool TryAwaitArrival(Binding binding)
        {
            var person = binding.Person; var citizen = binding.Citizen; var lease = binding.Lease;
            if (person == null || !person.Valid || lease == null || binding.Queue.HasValue || binding.AwaitingArrival
                || citizen.Phase != CitizenPhase.Travelling || simulation.IsPhysicalJourneyValid(lease)
                || (citizen.DestinationActivity != Activity.Shop && citizen.DestinationActivity != Activity.Leisure)
                || !places.TryGetValue(citizen.DestinationId, out var destination)
                || DailyRoutine.Open(destination, simulation.State.Minute)
                || Position.Distance(person.Position, destination.Position) > 3) return false;
            binding.Lease = null;
            simulation.ReleasePhysicalJourney(lease); // Already returned on completion; a no-op kept for symmetry with Release.
            binding.AwaitingArrival = true; binding.AwaitMinute = simulation.State.Minute; binding.AwaitJourney = citizen.Journey;
            person.Stop(true);
            return true;
        }

        private bool ObserveArrival(Binding binding)
        {
            var person = binding.Person!;
            if (!person.Valid) return false;
            person.Stop(true);
            var citizen = binding.Citizen;
            // Half a game minute covers a budgeted catch-up; a paused clock does not count. A new
            // journey (no later opening, so the simulator sent them on) is not this arrival.
            if (citizen.Phase == CitizenPhase.Travelling)
                return ReferenceEquals(citizen.Journey, binding.AwaitJourney) && simulation.State.Minute <= binding.AwaitMinute + 0.5;
            // Another door used this frame's sampling budget: wait a frame rather than lose the body.
            if (citizen.Phase == CitizenPhase.WaitingForOpening && sampledFrame == Time.frameCount
                && places.TryGetValue(citizen.PlaceId, out var place) && spots.Unsampled(place)
                && simulation.State.Minute <= binding.AwaitMinute + 0.5) return true;
            binding.AwaitingArrival = false;
            return TryJoinQueue(binding);
        }

        /// <summary>Join the door's queue with the body that walked here. Its spot respects the people already planned for that line.</summary>
        private bool TryJoinQueue(Binding binding)
        {
            var person = binding.Person!; var citizen = binding.Citizen;
            if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)) return false;
            var dwell = DwellPlanner.QueueAtOpening(citizen, place, simulation.State.Seed, simulation.State.Minute, RainHelper.isRaining);
            if (!dwell.HasValue || !dwell.Value.ActiveAt(simulation.State.Minute) || GaveUp(dwell.Value) || !TryLine(place, out var line)) return false;
            var reserved = Standing(line).Concat(queuers.Where(q => q.Key != citizen.Identity.Id && q.Value.Line.Key == line.Key
                && !bindings.ContainsKey(q.Key)).Select(q => new KeyValuePair<string, int>(q.Key, q.Value.Slot)));
            var slots = DwellPlanner.AssignQueueSlots(reserved, new[] { dwell.Value }, line.Spots.Count);
            if (!slots.TryGetValue(citizen.Identity.Id, out int slot)) return false;
            if (!person.SetPathTarget(line.Spots[slot], NativeTravelPace.WalkingMetresPerSecond))
            { gaveUp[citizen.Identity.Id] = dwell.Value.EndMinute; return false; }
            binding.Queue = dwell; binding.Line = line; binding.Slot = slot;
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

        private bool MotionPermission(PhysicalVehicleBody body, out PhysicalJourneySample sample, out double maximum)
        {
            sample = default; maximum = 0;
            if (!bindings.TryGetValue(body.CitizenId, out var binding) || !ReferenceEquals(binding.Car, body)
                || binding.Lease == null || !simulation.TrySamplePhysicalJourney(binding.Lease, out sample)
                || sample.Mode != TravelMode.Car || !sample.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)
                || !simulation.TryGetTrafficPermission(body.CitizenId, out var permission)
                || !permission.LinkId.HasValue || permission.IsCompleted) return false;
            maximum = permission.MaximumNominalMinutes;
            return true;
        }
        private PhysicalTrafficCommandLimit CommandLimit(PhysicalVehicleBody body)
        {
            if (!MotionPermission(body, out var sample, out double maximum))
                throw new InvalidOperationException("Owned physical car has no admitted traffic permission.");
            var binding = bindings[body.CitizenId];
            bool hold = binding.ReachedCarLeg == sample.LegIndex || body.TryPeekReached(sample.To, out _)
                || binding.CornerHandoff.MustHold(sample, maximum);
            return new PhysicalTrafficCommandLimit(sample.Direction,
                hold ? 0 : PhysicalTrafficMotion.MaximumAdvance(sample, body.Position, maximum), hold);
        }
        private bool NativeTargetPermission(PhysicalVehicleBody body, int nativeTarget)
        {
            if (!MotionPermission(body, out var sample, out double maximum)) return false;
            // A native loop must not pop a later corner while the renderer has yet to report this one.
            return sample.ServiceId.EndsWith(":" + nativeTarget.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && PhysicalTrafficCornerPermission.CanReachEndpoint(sample, maximum);
        }

        private bool Observe(Binding binding)
        {
            if (binding.Person != null && !binding.Person.Valid || binding.Car != null && !binding.Car.Valid) return false;
            if (binding.Lease == null)
            {
                if (binding.AwaitingArrival) return ObserveArrival(binding);
                if (binding.Queue.HasValue) return ObserveQueue(binding);
                if (!Outdoor(binding.Citizen) || binding.Citizen.Phase == CitizenPhase.Travelling) return false;
                binding.Person!.Stop(true); return true;
            }
            if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var sample)) return false;
            double elapsed = Math.Max(0, simulation.State.Minute - binding.ObservedMinute);
            binding.ObservedMinute = simulation.State.Minute;
            Position actual = binding.Person?.Position ?? binding.Car!.Position;
            for (int corner = 0; corner < 16; corner++)
            {
                if (binding.Person != null && sample.Mode != TravelMode.Walk || binding.Car != null && sample.Mode != TravelMode.Car) return false;
                if (binding.Car != null && !sample.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
                if (sample.Stationary)
                {
                    // Deliberate zero-distance waits use elapsed simulation time; moving legs never do.
                    binding.Person?.Stop(true);
                    double waitProgress = PhysicalProgress.AdvanceWait(sample, elapsed);
                    double wait = waitProgress - sample.ElapsedMinutes;
                    if (!simulation.ObservePhysicalJourney(binding.Lease, waitProgress)
                        || !simulation.TrySamplePhysicalJourney(binding.Lease, out var afterWait)) return false;
                    elapsed -= wait;
                    if (afterWait.LegIndex == sample.LegIndex) return true;
                    sample = afterWait;
                    continue;
                }
                if (binding.Car != null && binding.ReachedCarLeg != sample.LegIndex
                    && binding.Car.TryPeekReached(sample.To, out var reachedMarker) && binding.Car.ConsumeReached(sample.To))
                { binding.ReachedCarLeg = sample.LegIndex; binding.ReachedCarMarker = reachedMarker; }
                bool nativeCorner = binding.Car != null && binding.ReachedCarLeg == sample.LegIndex;
                if (!PhysicalProgress.TryProject(sample, actual, nativeCorner, out double progress))
                {
                    // Native avoidance may move laterally around another body. Keep following the
                    // already validated corner path, but credit no journey progress until it rejoins.
                    binding.Person?.Tick(); blockedSamples++; return true;
                }
                // Crossing a road-node boundary also requires Gley's measured waypoint evidence.
                // This keeps queued intermediate corners ordered and cannot bypass a red stop line.
                if (binding.Car != null && !nativeCorner && progress >= sample.ElapsedMinutes + sample.RemainingLegMinutes - 1e-9)
                    progress = sample.ElapsedMinutes;
                // A native frame can precede a budgeted simulation event; never claim future progress.
                progress = Math.Min(progress, simulation.State.Minute);
                if (binding.Car != null && simulation.TryGetTrafficPermission(binding.Citizen.Identity.Id, out var permission))
                {
                    // Endpoint arithmetic can land on either side of the admitted boundary.
                    // Reconcile before the excess-only guard so a car cannot remain an ulp
                    // short of its exit forever. Neither path credits future simulation time.
                    if (permission.MaximumNominalMinutes <= simulation.State.Minute
                        && (PhysicalTrafficCornerPermission.TryReconcileEndpoint(sample, nativeCorner, progress,
                            permission.MaximumNominalMinutes, out double reconciled)
                            || PhysicalTrafficCornerPermission.TryReconcileNormalizedBoundary(sample, binding.Lease.ObservedMinutes,
                                progress, permission.MaximumNominalMinutes, out reconciled)))
                        progress = reconciled;
                    if (progress > permission.MaximumNominalMinutes)
                    {
                        if (binding.CornerHandoff.TryHold(sample, actual, permission.MaximumNominalMinutes,
                            binding.Lease.ObservedMinutes, out double held))
                            progress = held;
                        else if (PhysicalTrafficPrecision.TryClampNumericalExcess(sample, actual, progress,
                            permission.MaximumNominalMinutes, out double rounded)) progress = rounded;
                        else
                        {
                            // Material movement beyond permission still invalidates the visual lease.
                            // Neither numerical placement nor a reused corner receipt grants extra storage.
                            trafficOvershoots++; return false;
                        }
                    }
                }
                if (progress == sample.ElapsedMinutes) blockedSamples++;
                if (!simulation.ObservePhysicalJourney(binding.Lease, progress)) return false;
                if (!simulation.TrySamplePhysicalJourney(binding.Lease, out var updated)) return false;
                if (updated.LegIndex == sample.LegIndex)
                {
                    if (binding.Person != null)
                    {
                        if (binding.TargetLeg != sample.LegIndex)
                        {
                            if (Time.unscaledTime < binding.NextTargetAttempt) return true;
                            if (!binding.Person.SetTarget(sample.To, NativeTravelPace.WalkingMetresPerSecond))
                            { binding.Person.Stop(true); binding.NextTargetAttempt = Time.unscaledTime + 1; blockedSamples++; return true; }
                            binding.TargetLeg = sample.LegIndex;
                        }
                        binding.Person.Tick();
                    }
                    return true;
                }
                if (binding.Car != null && nativeCorner)
                    binding.CornerHandoff.Begin(sample, updated, binding.ReachedCarMarker);
                sample = updated;
            }
            return true;
        }

        public void ObserveForSave()
        {
            vehicles.AssertHealthy();
            foreach (var binding in bindings.Values.ToArray()) if (!Observe(binding)) Release(binding);
        }
        private void ReleaseBodies(Binding binding)
        {
            try { if (binding.Person != null) pedestrians.Release(binding.Person); }
            finally { if (binding.Car != null) vehicles.Release(binding.Car); }
        }
        private void Release(Binding binding)
        {
            try { if (binding.Lease != null) simulation.ReleasePhysicalJourney(binding.Lease); }
            finally
            {
                try { ReleaseBodies(binding); }
                finally
                {
                    bindings.Remove(binding.Citizen.Identity.Id);
                    attempted[binding.Citizen.Identity.Id] = Time.unscaledTime;
                    released++;
                }
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; active = false;
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
