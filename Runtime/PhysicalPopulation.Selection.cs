// Per-frame update and body selection: save/suspend hooks, the observation loop, candidate priority, fade restrictions, the journey-index check and the door-queue plan.
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
    internal sealed partial class PhysicalPopulation
    {
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
            // F2f: the active-journey index members filed in the grid cells within the radius (F5i: residents and
            // visitors), or every resident after a shadow mismatch (visitors get no body in that fallback, by design: they
            // receive bodies through the index only). Filters and order below are the same for both sources.
            var roster = journeyIndexFallback ? simulation.State.Citizens : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
            for (int index = 0; index < roster.Count; index++)
            {
                var c = roster[index];
                // Both pools are full: observations and queue movement above still ran; no
                // candidate can acquire a body until a later update frees a slot.
                if (people >= pedestrianLimit && cars >= vehicleLimit) break;
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
            // F5i: visitors are attempted too, so their retry times are kept like residents'.
            if (attempted.Count > PersonTable.PersonCount(simulation.State) + 128)
            {
                var ids = new HashSet<string>(PersonTable.Schedules(simulation.State).Select(c => c.Identity.Id), StringComparer.Ordinal);
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
            foreach (var c in PersonTable.Schedules(simulation.State))
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
    }
}
