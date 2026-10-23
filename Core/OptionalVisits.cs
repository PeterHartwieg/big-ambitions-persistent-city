using System;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        public bool TryBeginMealVisit(string citizenId, string shopId, Route outward,
            double returnMinutes, double stayMinutes, double latestReturnMinute)
        {
            if (!citizenEvents.TryGetValue(citizenId, out var item) || !places.TryGetValue(shopId, out var shop)
                || shop.Kind != PlaceKind.Shop || !CurrentCitizenSlot(item, item.Citizen)) return false;
            var person = item.Citizen; double now = State.Minute;
            if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                || physicalJourneys.ContainsKey(citizenId) || trafficCitizens.ContainsKey(citizenId)
                || latestReturnMinute > Math.Min(person.NextEventMinute, person.ActivityUntilMinute)) return false;
            if (!Position.Finite(returnMinutes) || returnMinutes <= 0 || !Position.Finite(stayMinutes) || stayMinutes <= 0
                || stayMinutes > 120 || !Position.Finite(latestReturnMinute)) throw new ArgumentException("Invalid meal outing.");
            ValidateRoute(outward, places[person.PlaceId].Position, shop.Position);
            double arrival = now + outward.Minutes, leave = arrival + stayMinutes;
            if (outward.Legs.Any(l => l.Mode != TravelMode.Walk) || leave + returnMinutes > latestReturnMinute
                || !DailyRoutine.Open(shop, arrival) || DailyRoutine.NextClose(shop, arrival) < leave) return false;
            var outing = new MealOuting { OriginId = person.PlaceId, OriginActivity = person.Activity,
                ResumeUntil = person.ActivityUntilMinute, ResumeEvent = person.NextEventMinute };
            if (!due.Remove(item)) throw new InvalidOperationException("Resident has no queued event.");
            person.MealOuting = outing; person.DestinationId = shopId; person.DestinationActivity = Activity.Shop;
            person.ActivityUntilMinute = leave; person.Phase = CitizenPhase.Travelling;
            person.Journey = new Route { Legs = outward.Legs.Select(l => new RouteLeg { From = l.From, To = l.To,
                Minutes = l.Minutes, Mode = l.Mode, ServiceId = l.ServiceId }).ToList() };
            person.DepartureMinute = now; person.NextEventMinute = arrival;
            item.Minute = ScheduledMinute(person); due.Add(item);
            activityObservers.Handlers?.Invoke(citizenId, person, now); SynchronizeSnapshot(); return true;
        }

        internal void ExtendMealStay(CitizenState person, double until)
        {
            var item = citizenEvents[person.Identity.Id];
            if (!due.Remove(item)) throw new InvalidOperationException("Meal stay has no event.");
            person.ActivityUntilMinute = until; person.NextEventMinute = until;
            item.Minute = ScheduledMinute(person); due.Add(item);
        }

        private bool StepMealOuting(CitizenState person, double now)
        {
            var outing = person.MealOuting;
            if (outing == null) return false;
            if (outing.Returning && person.Phase == CitizenPhase.Travelling)
            {
                person.PlaceId = outing.OriginId; person.Activity = outing.OriginActivity; person.Phase = CitizenPhase.AtPlace;
                person.Journey = null; person.DestinationId = ""; person.DepartureMinute = 0; person.CompletedJourneys++;
                person.ActivityUntilMinute = outing.ResumeUntil; person.MealOuting = null;
                // A late return lets the regular routine decide what to do next.
                if (now >= outing.ResumeEvent) return false;
                person.NextEventMinute = outing.ResumeEvent; return true;
            }
            if (!outing.Returning && person.Phase == CitizenPhase.Travelling)
            {
                person.PlaceId = person.DestinationId; person.Activity = Activity.Shop;
                person.Phase = CitizenPhase.AtPlace; person.Journey = null; person.DestinationId = "";
                person.DepartureMinute = 0; person.CompletedJourneys++;
                if (now < person.ActivityUntilMinute && DailyRoutine.Open(places[person.PlaceId], now))
                {
                    person.RecentVisits.Add(new VisitRecord { AtMinute = now, PlaceId = person.PlaceId, Activity = Activity.Shop });
                    if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
                    person.NextEventMinute = Math.Min(person.ActivityUntilMinute, DailyRoutine.NextClose(places[person.PlaceId], now)!.Value);
                    return true;
                }
            }
            // While admitted, the ordinary event owns the planned eating/stay interval.
            if (!outing.Returning && person.Phase == CitizenPhase.AtPlace && now < person.ActivityUntilMinute
                && DailyRoutine.Open(places[person.PlaceId], now)) return false;
            var route = routes.Plan(places[person.PlaceId], places[outing.OriginId], now,
                new TravelPreferences { OwnsCar = false, AllowTransit = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres });
            outing.Returning = true; person.DestinationId = outing.OriginId; person.DestinationActivity = outing.OriginActivity;
            person.ActivityUntilMinute = outing.ResumeUntil;
            if (route == null)
            { person.Phase = CitizenPhase.WaitingForRoute; person.NextEventMinute = now + 5; return true; }
            ValidateRoute(route, places[person.PlaceId].Position, places[outing.OriginId].Position);
            if (route.Legs.Any(l => l.Mode != TravelMode.Walk)) throw new InvalidOperationException("Meal return must be walking.");
            person.Phase = CitizenPhase.Travelling; person.Journey = route; person.DepartureMinute = now;
            person.NextEventMinute = now + route.Minutes; return true;
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
                || shop.Kind != PlaceKind.Shop || !CurrentCitizenSlot(item, item.Citizen)) return false;
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
