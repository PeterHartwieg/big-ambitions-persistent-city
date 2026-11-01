using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private readonly List<Place> optionalPlaces = new List<Place>();
        // This is a useful-visit floor, not time added to the resident's original window.
        private static double MinimumOptionalStay(Activity activity) => activity == Activity.Shop ? 5 : 15;

        private static double? UsefulOpening(Place place, double arrival, double deadline, double stay)
        {
            if (arrival + stay > deadline) return null;
            var opening = DailyRoutine.NextOpen(place, arrival, deadline);
            if (!opening.HasValue) return null;
            double close = DailyRoutine.NextClose(place, opening.Value)!.Value;
            return opening.Value + stay <= Math.Min(deadline, close) ? opening : null;
        }

        // The outcome of one optional decision, filled in once its request sequence has run to the end.
        private sealed class OptionalChoice
        {
            public OptionalChoice(RoutineSlot proposed) { Slot = proposed; }
            public RoutineSlot Slot;
            public Route? Route;
            public bool Selected;
        }

        private IEnumerable<RouteRequest> ChooseOptionalDestination(CitizenState person, RoutineSlot proposed, double now, OptionalChoice choice)
        {
            choice.Slot = proposed; choice.Route = null; choice.Selected = false;
            var origin = places[person.PlaceId];
            var kind = proposed.Activity == Activity.Shop ? PlaceKind.Shop : PlaceKind.Leisure;
            string key = person.Identity.Id + ":optional:" + proposed.Activity + ":" + proposed.EndsAt.ToString("R", CultureInfo.InvariantCulture);
            var weather=CaptureOuting(person,now,key);
            if(weather.Value==0)
            {
                choice.Slot=new RoutineSlot(person.HomeId,Activity.Home,proposed.EndsAt);
                ObserveRoutine(person,proposed,now,"weather-declined","captured-rain");
                yield break;
            }
            var candidates = new List<Place>();
            // The familiar shop remains a first choice; leisure keeps its seeded daily variety.
            if (places.TryGetValue(proposed.PlaceId, out var preferred) && preferred.Kind == kind && preferred.Capacity > 0)
                candidates.Add(preferred);
            foreach (var candidate in DestinationChoice.Shortlist(optionalPlaces, origin, kind, key, State.Seed))
                if (candidate.Id != proposed.PlaceId && candidates.Count < 8) candidates.Add(candidate);
            int routeCalls = 0;
            double stay = MinimumOptionalStay(proposed.Activity);
            foreach (var candidate in candidates)
            {
                var slot = new RoutineSlot(candidate.Id, proposed.Activity, proposed.EndsAt);
                if (!UsefulOpening(candidate, now, slot.EndsAt, stay).HasValue)
                { ObserveRoutine(person, slot, now, "candidate-rejected", "no-useful-opening"); continue; }
                // Only a walking-only traveller can use a walking lower bound safely.
                if (!person.Preferences.OwnsCar && !person.Preferences.AllowTransit && candidate.Id != origin.Id)
                {
                    if (Position.Distance(origin.Position, candidate.Position) > person.Preferences.WalkingToleranceMetres
                        || (routes is IWalkingTimeBound bound && now + bound.MinimumWalkingMinutes(origin, candidate) + stay > slot.EndsAt))
                    { ObserveRoutine(person, slot, now, "candidate-rejected", "walking-bound"); continue; }
                }
                Route? route = null;
                if (candidate.Id != origin.Id)
                {
                    // At most two full multimodal searches per scheduled optional decision.
                    if (routeCalls == 2) break;
                    routeCalls++;
                    var request = CitizenRequest(origin, candidate, now, person);
                    yield return request;
                    route = request.Result;
                    if (route == null)
                    { ObserveRoutine(person, slot, now, "route-rejected", "unreachable"); continue; }
                    ValidateRoute(route, origin.Position, candidate.Position); ValidateCarRoute(route, person);
                }
                double arrival = now + (route?.EstimatedMinutes ?? 0);
                if (!UsefulOpening(candidate, arrival, slot.EndsAt, stay).HasValue)
                { ObserveRoutine(person, slot, now, "route-rejected", "arrival-no-useful-opening"); continue; }
                choice.Slot = slot; choice.Route = route; choice.Selected = true;
                ObserveRoutine(person, slot, now, "selected", candidate.Id == proposed.PlaceId ? "preferred-feasible" : "alternative-feasible");
                yield break;
            }
            MissRoutine(person, proposed, now, routeCalls == 2 ? "optional-search-exhausted" : "no-feasible-optional-venue");
            choice.Slot = new RoutineSlot(person.HomeId, Activity.Home, proposed.EndsAt);
            choice.Route = null; choice.Selected = false;
        }
    }
}
