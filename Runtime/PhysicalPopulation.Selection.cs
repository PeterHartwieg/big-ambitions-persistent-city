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
            if (NativeInteriorCityView.Configured && BuildingManager.IsInsideBuilding) SuspendBeforeNativeFastForward();
            else ObserveForSave();
        }
        public void RestoreInterior() { if (bindings.Count != 0) SuspendBeforeNativeFastForward(); }
        public bool HasBodies => bindings.Count != 0;
        public int Followers
        {
            get { int count = 0; foreach (var binding in bindings.Values) if (binding.Person != null && binding.Journey != null) count++; return count; }
        }
        public int CarFollowers
        {
            get { int count = 0; foreach (var binding in bindings.Values) if (binding.Car != null && binding.Journey != null) count++; return count; }
        }
        public void Suspend()
        {
            Exception? failure = null;
            foreach (var binding in bindings.Values.ToArray())
                try { Release(binding); } catch (Exception error) { failure = failure ?? error; }
            try { FadeLeaving(all: true); } catch (Exception error) { failure = failure ?? error; }
            if (failure != null) throw new InvalidOperationException("One or more physical bodies could not be released.", failure);
        }
        public void Update()
        {
            if (!active || disposed) return;
            followDelta = simulation.View.Minute > followMinute ? Time.unscaledDeltaTime : 0;
            followClock += followDelta;
            followMinute = simulation.View.Minute;
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalUnattributed);
            RefreshVendorMeals();
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalVendorMeals);
            ambient.Update();
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalAmbient);
            if (!ambient.Active) throw new InvalidOperationException("Pedestrian ownership guard failed: " + ambient.LastError);
            vehicles.Update();
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalVehicles);
            FadeLeaving(all: false);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalFade);
            if (NativeTravelPace.HiddenTravel) { SuspendBeforeNativeFastForward(); return; }
            Position focus = NativeTravelPace.Focus;
            if (!focus.IsFinite) { Suspend(); return; }
            double radius = NativeInteriorCityView.AdmissionRadius(this.radius);
            double releaseRadius = NativeInteriorCityView.ReleaseRadius(this.radius);
            int pedestrianLimit = NativeInteriorCityView.Pedestrians(this.pedestrianLimit);
            int vehicleLimit = NativeInteriorCityView.Vehicles(this.vehicleLimit);
            bool interior = NativeInteriorCityView.State.Active;
            var playerPose = default(PlayerPose);
            bool player = !interior && PlayerContact.TryReadPlayer(out playerPose);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalFocus);
            var snapshot = bindingSnapshot ?? new List<Binding>(bindings.Count);
            bindingSnapshot = null;
            try
            {
                foreach (var value in bindings.Values) snapshot.Add(value);
                int keptPeople = 0, keptCars = 0;
                foreach (var binding in snapshot)
                {
                    if (ambient.IsReserved(binding.Citizen.Identity.Id)) { ReleaseCharged(binding); continue; }
                    PhysicalAllocation.Charge(GcAllocationSegment.PhysicalBindings);
                    bool observed = Observe(binding);
                    PhysicalAllocation.Charge(GcAllocationSegment.PhysicalObserve);
                    if (!observed) { ReleaseCharged(binding); continue; }
                    Position position = binding.Person?.Position ?? binding.Car!.Position;
                    if (Position.Distance(position, focus) > releaseRadius) { ReleaseCharged(binding); continue; }
                    if (interior && (binding.Person != null ? ++keptPeople > pedestrianLimit : ++keptCars > vehicleLimit)) { ReleaseCharged(binding); continue; }
                    if (player) ObservePlayerContact(binding, playerPose);
                    FadeIn(binding);
                }
            }
            finally { snapshot.Clear(); bindingSnapshot = snapshot; }
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalBindings);
            if (!interior && Time.unscaledTime >= nextParked)
            {
                nextParked = Time.unscaledTime + ParkedRefreshSeconds; ShowParked(focus, radius, vehicleLimit);
                PhysicalAllocation.Charge(GcAllocationSegment.PhysicalParked);
            }
            if (Time.unscaledTime < nextSelection || !routes.ReadyForPopulation) return;
            nextSelection = Time.unscaledTime + .25f;
            int people = personBindings, cars = bindings.Count - people;
            int attempts = 0;
            if (!journeyIndexFallback && Time.unscaledTime >= nextJourneyIndexCheck)
            {
                nextJourneyIndexCheck = Time.unscaledTime + JourneyIndexCheckSeconds;
                CheckJourneyIndex();
                PruneFadeRestrictions();
            }
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalIndex);
            PlanQueues(focus, radius);
            PlanVendorQueues(focus, radius);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalQueues);
            candidates.Clear();
            var roster = journeyIndexFallback ? simulation.View.ResidentList : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
            for (int index = 0; index < roster.Count; index++)
            {
                var c = roster[index];
                if (people >= pedestrianLimit && cars >= vehicleLimit) break;
                if (!(c.NextEventMinute > simulation.View.Minute) || bindings.ContainsKey(c.Id) || ambient.IsReserved(c.Id)) continue;
                bool tried = attempted.TryGetValue(c.Id, out float lastAttempt);
                if (tried && !(Time.unscaledTime - lastAttempt >= 2)) continue;
                var entity = Entity(c);
                if (!((c.Phase == CitizenPhase.Travelling && entity.Journey != null) || queuers.ContainsKey(c.Id) || Outdoor(entity))) continue;
                Position sampled = simulation.PositionOf(entity);
                double distance = Position.Distance(sampled, focus);
                if (!(distance <= radius)) continue;
                candidates.Add(new SelectionCandidate(entity, sampled, tried ? lastAttempt : float.NegativeInfinity, distance, candidates.Count));
            }
            foreach (var waiter in vendorWaiters.Values)
            {
                if (journeyIndexFallback || simulation.ActiveJourneys.Contains(waiter)
                    || people >= pedestrianLimit || !queuers.TryGetValue(waiter.Id, out var queue)
                    || bindings.ContainsKey(waiter.Id) || ambient.IsReserved(waiter.Id)
                    || !(waiter.NextEventMinute > simulation.View.Minute)) continue;
                bool tried = attempted.TryGetValue(waiter.Id, out float lastAttempt);
                if (tried && Time.unscaledTime - lastAttempt < 2) continue;
                double distance = Position.Distance(queue.Line.Entrance, focus);
                if (distance <= radius) candidates.Add(new SelectionCandidate(Entity(waiter), queue.Line.Entrance,
                    tried ? lastAttempt : float.NegativeInfinity, distance, candidates.Count));
            }
            candidates.Sort(SelectionCandidate.ByPriority);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalRoster);
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
                if (++attempts > 4) break;
                attempted[citizen.Identity.Id] = Time.unscaledTime;
                PhysicalRoadSample road = default;
                if (FadeRestricted(citizen) && pedestrians.Visible(candidate.Position))
                {
                    attempts--;
                    if (float.IsNegativeInfinity(candidate.LastAttempt)) attempted.Remove(citizen.Identity.Id);
                    else attempted[citizen.Identity.Id] = candidate.LastAttempt;
                    continue;
                }
                fadedUntilHidden.Remove(citizen.Identity.Id);
                if (car && (!simulation.TryGetTrafficPermission(citizen.Identity.Id, out var admission)
                    || !admission.LinkId.HasValue || admission.IsCompleted)) continue;
                if (car && !routes.TryPhysicalRoadSample(citizen, sample, out road))
                    continue;
                var binding = new Binding { Citizen = citizen };
                Position at = candidate.Position;
                if (queuer) { binding.Queue = queue!.Dwell; binding.VendorOpening = queue.VendorOpening; binding.Line = queue.Line; binding.Slot = queue.Slot; at = queue.Line.Entrance; }
                if (moving) { binding.Journey = citizen.Journey; binding.BodyMinutes = sample.ElapsedMinutes; }
                bool bound = false;
                try
                {
                    if (faultAcquirePercent > 0 && faultDice.Next(100) < faultAcquirePercent)
                    { failedAcquisitions++; Telemetry.FaultedAcquisitions++; continue; }
                    PhysicalAllocation.Charge(GcAllocationSegment.PhysicalSelection);
                    if (car)
                    {
                        bool taken = vehicles.TryAcquire(citizen, road, out var body);
                        PhysicalAllocation.Charge(GcAllocationSegment.PhysicalSpawnVehicle);
                        if (!taken) { failedAcquisitions++; continue; }
                        binding.Car = body;
                    }
                    else
                    {
                        bool taken = pedestrians.TryAcquire(citizen.Identity, at, out var body);
                        PhysicalAllocation.Charge(GcAllocationSegment.PhysicalSpawnPedestrian);
                        if (!taken) { failedAcquisitions++; continue; }
                        binding.Person = body;
                    }
                    Bind(binding); bound = true; acquired++;
                }
                finally { if (!bound) ReleaseBodies(binding); }
                if (queuer && !binding.Person!.SetPathTarget(binding.Line!.Spots[binding.Slot], NativeTravelPace.WalkingMetresPerSecond))
                { GiveUp(binding); Release(binding); continue; }
                if (!Observe(binding)) { Release(binding); continue; }
                BeginFadeIn(binding);
                if (car) cars++; else people++;
                if (queuer) queued++;
            }
            candidates.Clear();
            var view = simulation.View;
            if (attempted.Count > view.ResidentList.Count + view.VisitorCount + 128)
            {
                var ids = new HashSet<string>(view.ResidentList.Concat(view.VisitorRoutineList).Select(c => c.Id), StringComparer.Ordinal);
                foreach (var id in attempted.Keys.Where(id => !ids.Contains(id)).ToArray()) attempted.Remove(id);
            }
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalSelection);
        }

        private void ReleaseCharged(Binding binding)
        {
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalBindings);
            Release(binding);
            PhysicalAllocation.Charge(GcAllocationSegment.PhysicalRelease);
        }

        private void BeginFadeIn(Binding binding)
        {
            bool fades = binding.Person != null ? binding.Person.OnScreen && binding.Person.CanFade
                : binding.Car != null && binding.Car.OnScreen && binding.Car.CanFade;
            if (!fades) return;
            binding.FadeInSince = followClock;
            if (binding.Person != null) binding.Person.SetFade(0); else binding.Car!.SetFade(0);
        }
        private void FadeIn(Binding binding)
        {
            if (float.IsNaN(binding.FadeInSince)) return;
            if (!float.IsNaN(binding.FadeOutSince)) { binding.FadeInSince = float.NaN; return; }
            float alpha = BodyFade.In(followClock - binding.FadeInSince);
            if (alpha >= 1) binding.FadeInSince = float.NaN;
            if (binding.Person != null) binding.Person.SetFade(alpha); else binding.Car?.SetFade(alpha);
        }

        private const float ParkedRefreshSeconds = 1f;
        private float nextParked;
        private readonly List<ParkedCar> parkedCandidates = new List<ParkedCar>();
        private void ShowParked(Position focus, double radius, int limit)
        {
            parkedCandidates.Clear();
            var residents = simulation.View.ResidentList;
            for (int i = 0; i < residents.Count; i++)
            {
                var person = Entity(residents[i]);
                var car = person.Car;
                if (car == null || !person.Preferences.OwnsCar || bindings.TryGetValue(person.Identity.Id, out var bound) && bound.Car != null) continue;
                double elapsed = 0;
                if (person.Phase == CitizenPhase.Travelling && person.Journey != null)
                {
                    bool near = Position.Distance(car.ParkedPosition, focus) <= radius;
                    var legs = person.Journey.Legs;
                    for (int leg = legs.Count - 1; leg >= 0 && !near; leg--)
                        if (legs[leg].Mode == TravelMode.Car) { near = Position.Distance(legs[leg].To, focus) <= radius; break; }
                    if (!near) continue;
                    elapsed = simulation.SampleJourney(person).ElapsedMinutes;
                }
                if (ParkedCars.TryParked(person, elapsed, out var at) && Position.Distance(at, focus) <= radius)
                {
                    parkedCandidates.Add(new ParkedCar(car.Id, person.Identity.Id, at, 0));
                    parkedStates[car.Id] = car;
                }
            }
            var shown = ParkedCars.Nearest(parkedCandidates, focus, radius, limit, parkedShown);
            vehicles.ShowParked(shown, parkedState ??= id => parkedStates.TryGetValue(id, out var state) ? state : null);
            parkedCandidates.Clear(); parkedStates.Clear(); parkedShown.Clear();
        }
        private readonly List<ParkedCar> parkedShown = new List<ParkedCar>();
        private readonly Dictionary<string, CarState> parkedStates = new Dictionary<string, CarState>(StringComparer.Ordinal);
        private Func<string, CarState?>? parkedState;

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

        private bool StatefulCandidatesIndexed()
            => StatefulCandidatesIndexed(simulation.View.ResidentList) && StatefulCandidatesIndexed(simulation.View.VisitorRoutineList);

        private bool StatefulCandidatesIndexed(IReadOnlyList<IPersonView> persons)
        {
            var index = simulation.ActiveJourneys;
            for (int i = 0; i < persons.Count; i++)
            {
                var c = persons[i];
                if (!index.Contains(c) && (c.Phase == CitizenPhase.Travelling && Entity(c).Journey != null
                    || c.Phase == CitizenPhase.WaitingForOpening || Outdoor(Entity(c)))) return false;
            }
            return true;
        }

        private readonly struct SelectionCandidate
        {
            public SelectionCandidate(CitizenState citizen, Position position, float lastAttempt, double distance, int rosterIndex)
            { Citizen = citizen; Position = position; LastAttempt = lastAttempt; Distance = distance; RosterIndex = rosterIndex; }
            public CitizenState Citizen { get; }
            public Position Position { get; }
            public float LastAttempt { get; }
            public double Distance { get; }
            public int RosterIndex { get; }
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
        private readonly List<IPersonView> nearby = new List<IPersonView>();

        private bool Outdoor(CitizenState c)
            => OptionalVisible(c) && simulation.OptionalStopStatus(c.OptionalStop!) == OptionalStopAvailability.Ready || c.Phase == CitizenPhase.AtPlace && c.Activity == Activity.Leisure && places[c.PlaceId].Id.StartsWith("scene-", StringComparison.Ordinal);

        private void PlanQueues(Position focus, double radius)
        {
            queuers.Clear();
            double now = simulation.View.Minute;
            var planned = plannedQueues;
            foreach (var line in planned.Values) { line.Dwells.Clear(); dwellLists.Push(line.Dwells); }
            planned.Clear();
            var roster = journeyIndexFallback ? simulation.View.ResidentList : simulation.ActiveJourneys.Grid.Query(focus, radius, nearby);
            for (int index = 0; index < roster.Count; index++)
            {
                var citizen = roster[index];
                if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)
                    || Position.Distance(place.Position, focus) > radius) continue;
                var dwell = QueueDwell(Entity(citizen), place, now);
                if (!dwell.HasValue || !dwell.Value.ActiveAt(now) || GaveUp(dwell.Value)) continue;
                string key = spots.Key(place);
                if (!planned.TryGetValue(key, out var line)) planned.Add(key, line = (place, dwellLists.Count > 0 ? dwellLists.Pop() : new List<Dwell>()));
                line.Dwells.Add(dwell.Value);
            }
            if (planned.Count == 0) return;
            plannedOrder.Clear();
            foreach (var pair in planned) plannedOrder.Add(new PlannedLine(pair.Key, Position.Distance(pair.Value.Place.Position, focus)));
            plannedOrder.Sort(PlannedLine.ByDistance);
            foreach (var entry in plannedOrder)
            {
                var value = planned[entry.Key];
                if (!TryLine(value.Place, out var found)) continue;
                var inLine = inLineScratch;
                inLine.Clear();
                foreach (var b in bindings.Values)
                    if (b.Line != null && b.Line.Key == found.Key && b.Queue.HasValue && b.Step != QueueStep.Entering && b.Person != null) inLine.Add(b);
                SortBySlot(inLine);
                var occupied = occupiedScratch;
                occupied.Clear();
                foreach (var standing in Standing(found)) occupied.Add(standing.Value);
                foreach (var bound in inLine)
                {
                    int? ahead = DwellPlanner.MoveUp(occupied, bound.Slot);
                    if (!ahead.HasValue || !bound.Person!.SetPathTarget(found.Spots[ahead.Value], NativeTravelPace.WalkingMetresPerSecond)) continue;
                    occupied.Remove(bound.Slot); occupied.Add(ahead.Value);
                    bound.Slot = ahead.Value; bound.Step = QueueStep.Approaching; bound.StepWalked = 0;
                }
                var slots = DwellPlanner.AssignQueueSlots(Standing(found), value.Dwells, found.Spots.Count);
                foreach (var dwell in value.Dwells)
                    if (slots.TryGetValue(dwell.CitizenId, out int slot))
                        queuers[dwell.CitizenId] = new QueuePlanEntry { Dwell = dwell, Line = found, Slot = slot };
            }
            inLineScratch.Clear(); plannedOrder.Clear();
        }

        private readonly Stack<List<Dwell>> dwellLists = new Stack<List<Dwell>>();
        private readonly List<PlannedLine> plannedOrder = new List<PlannedLine>();
        private readonly List<Binding> inLineScratch = new List<Binding>();
        private readonly HashSet<int> occupiedScratch = new HashSet<int>();
        private readonly struct PlannedLine
        {
            public PlannedLine(string key, double distance) { Key = key; Distance = distance; }
            public string Key { get; }
            public double Distance { get; }
            public static readonly IComparer<PlannedLine> ByDistance = new Order();
            private sealed class Order : IComparer<PlannedLine>
            {
                public int Compare(PlannedLine a, PlannedLine b)
                {
                    int order = a.Distance.CompareTo(b.Distance);
                    return order != 0 ? order : StringComparer.Ordinal.Compare(a.Key, b.Key);
                }
            }
        }
        private static void SortBySlot(List<Binding> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Slot > item.Slot) { list[j + 1] = list[j]; j--; }
                list[j + 1] = item;
            }
        }

        private readonly Dictionary<string, (Place Place, List<Dwell> Dwells)> plannedQueues = new Dictionary<string, (Place Place, List<Dwell> Dwells)>(StringComparer.Ordinal);

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
            if (until == dwell.EndMinute && simulation.View.Minute < until) return true;
            gaveUp.Remove(dwell.CitizenId);
            return false;
        }

        private void GiveUp(Binding binding)
        {
            if (binding.Queue.HasValue) gaveUp[binding.Citizen.Identity.Id] = binding.Queue.Value.EndMinute;
            if (binding.VendorOpening > 0) vendorGaveUp[binding.Citizen.Identity.Id] = binding.VendorOpening;
            if (gaveUp.Count > 256)
                foreach (var stale in gaveUp.Where(p => p.Value <= simulation.View.Minute).Select(p => p.Key).ToList()) gaveUp.Remove(stale);
        }

        private IEnumerable<KeyValuePair<string, int>> Standing(DoorQueue line)
        {
            foreach (var b in bindings.Values)
            {
                if ((b.Queue.HasValue || b.VendorOpening > 0) && b.Line != null && b.Line.Key == line.Key && b.Step != QueueStep.Entering)
                    yield return new KeyValuePair<string, int>(b.Citizen.Identity.Id, b.Slot);
                else if (b.Optional != null && !b.OptionalReturning && (!b.OptionalStandsShort || b.Person != null))
                {
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
