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
        private bool ObserveArrival(Binding binding)
        {
            var person = binding.Person!;
            if (!person.Valid) return false;
            person.Stop(true);
            var citizen = binding.Citizen;
            if (citizen.Phase == CitizenPhase.Travelling)
                return ReferenceEquals(citizen.Journey, binding.AwaitJourney) && simulation.View.Minute <= binding.AwaitMinute + 0.5;
            if (citizen.Phase == CitizenPhase.WaitingForOpening && spots.SampledThisFrame
                && places.TryGetValue(citizen.PlaceId, out var place) && spots.Unsampled(place)
                && simulation.View.Minute <= binding.AwaitMinute + 0.5) return true;
            if (VendorWaiting(citizen, out var vendor, out _) && spots.SampledThisFrame
                && spots.Unsampled(simulation.FindPlace(vendor.PlaceId)) && simulation.View.Minute <= binding.AwaitMinute + 0.5) return true;
            binding.AwaitingArrival = false;
            return TryJoinVendorQueue(binding) || TryJoinOptional(binding) || TryJoinQueue(binding);
        }

        private bool TryJoinQueue(Binding binding)
        {
            var person = binding.Person!; var citizen = binding.Citizen;
            if (citizen.Phase != CitizenPhase.WaitingForOpening || !places.TryGetValue(citizen.PlaceId, out var place)) return false;
            var dwell = QueueDwell(citizen, place, simulation.View.Minute);
            if (!dwell.HasValue || !dwell.Value.ActiveAt(simulation.View.Minute) || GaveUp(dwell.Value) || !TryLine(place, out var line)) return false;
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
            IPersonView citizen = binding.Citizen;
            if (!person.Valid) return false;
            if (!person.Agent.isStopped) binding.StepWalked += Time.unscaledDeltaTime;
            if (binding.Step == QueueStep.Entering)
            {
                if (binding.StepWalked >= QueueStepSeconds || Position.Distance(person.Position, line.Entrance) < 0.5) return false;
                person.Tick(); return true;
            }
            if (citizen.Phase == CitizenPhase.WaitingForOpening && citizen.PlaceId == dwell.SpotId && simulation.View.Minute < dwell.EndMinute + 1)
            {
                if (binding.Step == QueueStep.Approaching)
                {
                    if (Position.Distance(person.Position, line.Spots[binding.Slot]) <= 0.3)
                    {
                        binding.Step = QueueStep.Standing;
                        if (person.Agent.hasPath) person.Agent.ResetPath();
                    }
                    else if (binding.StepWalked < QueueStepSeconds) { person.Tick(); return true; }
                    else
                    {
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
            if (citizen.Phase != CitizenPhase.AtPlace || citizen.PlaceId != dwell.SpotId) return false;
            if (person.SetPathTarget(line.Entrance, NativeTravelPace.WalkingMetresPerSecond))
            { binding.Step = QueueStep.Entering; binding.StepWalked = 0; return true; }
            person.Stop(true);
            return ++binding.EntryAttempts < EntryAttemptFrames;
        }

        private void OnOptionalDeparture(string id, CitizenState citizen, double minute)
        {
            TrackVendorWaiter(citizen);
            if (!bindings.TryGetValue(id, out var binding) || (binding.Optional == null && binding.VendorOpening == 0) || binding.Person == null
                || !binding.Person.Valid || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null
                || ReferenceEquals(binding.Journey, citizen.Journey)) return;
            Follow(binding, citizen.Journey, 0);
        }

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
            && citizen.OptionalStop != null && citizen.OptionalStop.ActiveAt(simulation.View.Minute);

        private bool TryJoinOptional(Binding binding)
        {
            if (binding.Person == null || !OptionalVisible(binding.Citizen)
                || simulation.OptionalStopStatus(binding.Citizen.OptionalStop!) != OptionalStopAvailability.Ready) return false;
            var stop = binding.Citizen.OptionalStop!;
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
            if (!active && OptionalVisible(binding.Citizen) && binding.Citizen.OptionalStop!.SpotId == stop.SpotId
                && binding.Citizen.OptionalStop.StartMinute == stop.StartMinute)
            { binding.Optional = stop = binding.Citizen.OptionalStop; active = true; }
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
                    if (binding.Journey == null) TryFollowCommitted(binding);
                    if (binding.Journey == null) return false;
                    body.Stop(false); binding.TargetLeg = -1; return true;
                }
                if (!body.Agent.hasPath)
                {
                    body.Stop(false);
                    if (!body.SetPathTarget(stop.ReturnPosition, NativeTravelPace.WalkingMetresPerSecond)) body.Stop(true);
                }
                body.Tick(); return true;
            }
            bool atSpot = !binding.OptionalStandsShort && Position.Distance(body.Position, stop.Position) <= 0.3;
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

        private void TrackVendorWaiter(IPersonView citizen)
        {
            if (citizen.Phase == CitizenPhase.AtPlace && citizen.Activity == Activity.Shop
                && VendorNames.IsVendorPlace(citizen.PlaceId))
                vendorWaiters[citizen.Id] = citizen;
            else { vendorWaiters.Remove(citizen.Id); vendorGaveUp.Remove(citizen.Id); }
        }

        private bool VendorWaiting(IPersonView citizen, out VendorRow row, out VendorVehicle vehicle)
        {
            row = null!; vehicle = null!;
            var market = owner?.PhysicalVendorMarket;
            if (market == null || citizen.Phase != CitizenPhase.AtPlace || citizen.Activity != Activity.Shop
                || !vendorMeals.TryGetValue(citizen.Id, out var meal) || meal.PlaceId != citizen.PlaceId
                || (meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving)) return false;
            row = simulation.State.Vendors.FindPlace(citizen.PlaceId)!;
            if (row?.Stand == null || !row.Stand.Published || simulation.View.Minute >= row.Stand.ClosesAt
                || vendorGaveUp.TryGetValue(citizen.Id, out long failedOpening) && failedOpening == row.Stand.OpeningId
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
            foreach (var citizen in vendorWaiters.Values.OrderBy(c => vendorMeals.TryGetValue(c.Id, out var m) ? m.ServiceAt : double.MaxValue)
                .ThenBy(c => c.Id, StringComparer.Ordinal))
            {
                if (ambient.IsReserved(citizen.Id) || !VendorWaiting(citizen, out var row, out var vehicle) || Position.Distance(row.Stand!.Position, focus) > radius
                    || !spots.TryVendorQueue(row, vehicle, out var line) || line == null) continue;
                var occupied = new HashSet<int>(Standing(line).Select(p => p.Value));
                foreach (var planned in queuers.Values) if (planned.Line.Key == line.Key) occupied.Add(planned.Slot);
                int slot = 0;
                if (bindings.TryGetValue(citizen.Id, out var binding) && binding.VendorOpening == vehicle.OpeningId
                    && binding.Line == line && binding.Step != QueueStep.Entering) slot = binding.Slot;
                else { while (occupied.Contains(slot) && slot < line.Spots.Count) slot++; }
                if (slot < line.Spots.Count) queuers[citizen.Id] = new QueuePlanEntry { Line = line, Slot = slot, VendorOpening = vehicle.OpeningId };
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
            IPersonView citizen = binding.Citizen;
            if (binding.Journey != null || citizen.Phase != CitizenPhase.AtPlace || citizen.PlaceId != binding.Line!.Key
                || !Position.Finite(binding.VendorDepartureUntil) || simulation.View.Minute > binding.VendorDepartureUntil
                || !vendorMeals.TryGetValue(citizen.Id, out var meal) || meal.PlaceId != citizen.PlaceId) return false;
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
                    if (AwaitVendorDeparture(binding))
                    {
                        if (body.Agent.hasPath) body.Agent.ResetPath();
                        body.Stop(true); return true;
                    }
                    binding.VendorOpening = 0; binding.Line = null;
                    if (Bound(binding)) queueBindings--;
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
    }
}
