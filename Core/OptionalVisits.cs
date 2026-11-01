using System;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        public bool TryBeginMealVisit(string citizenId, string shopId, Route outward,
            double returnMinutes, double stayMinutes, double latestReturnMinute, bool deferObservation = false)
            => TryBeginMealVisit(citizenId, shopId, outward, returnMinutes, stayMinutes, latestReturnMinute, deferObservation, null);

        internal bool TryBeginMealVisit(string citizenId, string shopId, Route outward,
            double returnMinutes, double stayMinutes, double latestReturnMinute, bool deferObservation, Place? capturedHours)
        {
            if (!citizenEvents.TryGetValue(citizenId, out var item) || !places.TryGetValue(shopId, out var shop)
                || shop.Kind != PlaceKind.Shop || !CurrentCitizenSlot(item, item.Citizen) || InProgress(item)) return false;
            var person = item.Citizen; double now = State.Minute;
            if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                || physicalJourneys.ContainsKey(citizenId) || trafficCitizens.ContainsKey(citizenId)
                || latestReturnMinute > Math.Min(person.NextEventMinute, person.ActivityUntilMinute)) return false;
            if (!Position.Finite(returnMinutes) || returnMinutes <= 0 || !Position.Finite(stayMinutes) || stayMinutes <= 0
                || stayMinutes > 120 || !Position.Finite(latestReturnMinute)) throw new ArgumentException("Invalid meal outing.");
            ValidateRoute(outward, places[person.PlaceId].Position, shop.Position);
            var hours = capturedHours ?? shop;
            double arrival = now + outward.Minutes, leave = arrival + stayMinutes;
            if (outward.Legs.Any(l => l.Mode != TravelMode.Walk) || leave + returnMinutes > latestReturnMinute
                || !DailyRoutine.Open(hours, arrival) || DailyRoutine.NextClose(hours, arrival) < leave) return false;
            var outing = new MealOuting { OriginId = person.PlaceId, OriginActivity = person.Activity,
                ResumeUntil = person.ActivityUntilMinute, ResumeEvent = person.NextEventMinute };
            if (!due.Remove(item)) throw new InvalidOperationException("Resident has no queued event.");
            CancelOptionalStop(citizenId);
            person.MealOuting = outing; person.DestinationId = shopId; person.DestinationActivity = Activity.Shop;
            person.ActivityUntilMinute = leave; person.Phase = CitizenPhase.Travelling;
            person.Journey = new Route { Legs = outward.Legs.Select(l => new RouteLeg { From = l.From, To = l.To,
                Minutes = l.Minutes, Mode = l.Mode, ServiceId = l.ServiceId }).ToList() };
            person.DepartureMinute = now; person.NextEventMinute = arrival;
            item.Minute = ScheduledMinute(person); due.Add(item); FileOptionalStop(person);
            if(!deferObservation) ObserveFoodDeparture(person);
            SynchronizeSnapshot(); return true;
        }
        internal void ObserveFoodDeparture(CitizenState person) => activityObservers.Handlers?.Invoke(person.Identity.Id,person,State.Minute);

        internal void ExtendMealStay(CitizenState person, double until)
        {
            var item = citizenEvents[person.Identity.Id];
            if (InProgress(item) || !due.Remove(item)) throw new InvalidOperationException("Meal stay has no event.");
            person.ActivityUntilMinute = until; person.NextEventMinute = until;
            item.Minute = ScheduledMinute(person); due.Add(item);
        }

        private enum MealOutingStep { Ordinary, Handled, NeedsReturnRoute }

        // The former StepMealOuting up to its return-route search. Ordinary hands the event to the regular
        // routine, Handled ends it, and NeedsReturnRoute asks for the walk back before FinishMealReturn.
        private MealOutingStep BeginMealOuting(CitizenState person, double now)
        {
            var outing = person.MealOuting;
            if (outing == null) return MealOutingStep.Ordinary;
            if (outing.Returning && person.Phase == CitizenPhase.Travelling)
            {
                person.PlaceId = outing.OriginId; person.Activity = outing.OriginActivity; person.Phase = CitizenPhase.AtPlace;
                person.Journey = null; person.DestinationId = ""; person.DepartureMinute = 0; person.CompletedJourneys++;
                person.ActivityUntilMinute = outing.ResumeUntil; person.MealOuting = null;
                // A late return lets the regular routine decide what to do next.
                if (now >= outing.ResumeEvent) return MealOutingStep.Ordinary;
                person.NextEventMinute = outing.ResumeEvent; return MealOutingStep.Handled;
            }
            if (!outing.Returning && person.Phase == CitizenPhase.Travelling)
            {
                person.PlaceId = person.DestinationId; person.Activity = Activity.Shop;
                person.Phase = CitizenPhase.AtPlace; person.Journey = null; person.DestinationId = "";
                person.DepartureMinute = 0; person.CompletedJourneys++;
                if (now < person.ActivityUntilMinute && food?.OwnsRivalMealVisit(person, person.PlaceId) == true)
                {
                    // Arrival is at the door; FoodCommerce revalidates the detached terms
                    // before recording admission or serving. No shared place-hours write.
                    person.NextEventMinute = person.ActivityUntilMinute;
                    return MealOutingStep.Handled;
                }
                if (now < person.ActivityUntilMinute && DailyRoutine.Open(places[person.PlaceId], now))
                {
                    person.RecentVisits.Add(new VisitRecord { AtMinute = now, PlaceId = person.PlaceId, Activity = Activity.Shop });
                    if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
                    person.NextEventMinute = Math.Min(person.ActivityUntilMinute, DailyRoutine.NextClose(places[person.PlaceId], now)!.Value);
                    return MealOutingStep.Handled;
                }
            }
            if (!outing.Returning && person.Phase == CitizenPhase.AtPlace && now < person.ActivityUntilMinute
                && food?.OwnsRivalMealVisit(person, person.PlaceId) == true)
            { person.NextEventMinute = person.ActivityUntilMinute; return MealOutingStep.Handled; }
            // While admitted, the ordinary event owns the planned eating/stay interval.
            if (!outing.Returning && person.Phase == CitizenPhase.AtPlace && now < person.ActivityUntilMinute
                && DailyRoutine.Open(places[person.PlaceId], now)) return MealOutingStep.Ordinary;
            return MealOutingStep.NeedsReturnRoute;
        }

        private void FinishMealReturn(CitizenState person, double now, Route? route)
        {
            var outing = person.MealOuting!;
            outing.Returning = true; person.DestinationId = outing.OriginId; person.DestinationActivity = outing.OriginActivity;
            person.ActivityUntilMinute = outing.ResumeUntil;
            if (route == null)
            { person.Phase = CitizenPhase.WaitingForRoute; person.NextEventMinute = now + 5; return; }
            ValidateRoute(route, places[person.PlaceId].Position, places[outing.OriginId].Position);
            if (route.Legs.Any(l => l.Mode != TravelMode.Walk)) throw new InvalidOperationException("Meal return must be walking.");
            person.Phase = CitizenPhase.Travelling; person.Journey = route; person.DepartureMinute = now;
            person.NextEventMinute = now + route.Minutes;
        }

        /// <summary>Explicit, bounded home-origin walking visit. The caller owns the decision,
        /// not a second journey. Existing committed activity and lifecycle events take priority.
        /// No scheduler calls this automatically; the native food pilot is its first caller.</summary>
        public bool TryBeginOptionalShopVisit(string citizenId, string shopId, Route outward,
            double returnMinutes, double stayMinutes, double latestReturnMinute)
        {
            if (outward == null || !Position.Finite(returnMinutes) || returnMinutes <= 0
                || !Position.Finite(stayMinutes) || stayMinutes <= 0 || stayMinutes > 120
                || !Position.Finite(latestReturnMinute)) throw new ArgumentException("Invalid optional outing.");
            if (!citizenEvents.TryGetValue(citizenId, out var item) || !places.TryGetValue(shopId, out var shop)
                || shop.Kind != PlaceKind.Shop || !CurrentCitizenSlot(item, item.Citizen) || InProgress(item)) return false;
            var person = item.Citizen;
            double now = State.Minute;
            if (person.Phase != CitizenPhase.AtPlace || person.Activity != Activity.Home || person.PlaceId != person.HomeId
                || physicalJourneys.ContainsKey(citizenId) || trafficCitizens.ContainsKey(citizenId)
                || latestReturnMinute > Math.Min(person.NextEventMinute, person.ActivityUntilMinute)) return false;
            ValidateRoute(outward, places[person.HomeId].Position, shop.Position);
            if (outward.Legs.Any(l => l.Mode != TravelMode.Walk)) return false;
            double arrival = now + outward.Minutes, leave = arrival + stayMinutes;
            if (leave + returnMinutes > latestReturnMinute || !DailyRoutine.Open(shop, arrival)
                || DailyRoutine.NextClose(shop, arrival) < leave) return false;
            var committed = new Route { Legs = outward.Legs.Select(l => new RouteLeg { From = l.From, To = l.To,
                Minutes = l.Minutes, Mode = l.Mode, ServiceId = l.ServiceId }).ToList() };
            if (!due.Remove(item)) throw new InvalidOperationException("Resident has no queued activity.");
            person.DestinationId = shopId;
            person.DestinationActivity = Activity.Shop;
            person.ActivityUntilMinute = leave;
            person.Phase = CitizenPhase.Travelling;
            person.Journey = committed;
            person.DepartureMinute = now;
            person.NextEventMinute = arrival;
            item.Minute = ScheduledMinute(person);
            due.Add(item);
            activityObservers.Handlers?.Invoke(citizenId, person, now);
            SynchronizeSnapshot();
            return true;
        }
    }
}
