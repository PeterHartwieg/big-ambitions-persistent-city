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

        private bool ChooseOptionalDestination(CitizenState person, RoutineSlot proposed, double now,
            out RoutineSlot selected, out Route? selectedRoute)
        {
            selected = proposed; selectedRoute = null;
            var origin = places[person.PlaceId];
            var kind = proposed.Activity == Activity.Shop ? PlaceKind.Shop : PlaceKind.Leisure;
            string key = person.Identity.Id + ":optional:" + proposed.Activity + ":" + proposed.EndsAt.ToString("R", CultureInfo.InvariantCulture);
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
                    route = routes is ICitizenRoutePlanner citizenRoutes ? citizenRoutes.Plan(origin, candidate, now, person)
                        : routes.Plan(origin, candidate, now, person.Preferences);
                    if (route == null)
                    { ObserveRoutine(person, slot, now, "route-rejected", "unreachable"); continue; }
                    ValidateRoute(route, origin.Position, candidate.Position); ValidateCarRoute(route, person);
                }
                double arrival = now + (route?.EstimatedMinutes ?? 0);
                if (!UsefulOpening(candidate, arrival, slot.EndsAt, stay).HasValue)
                { ObserveRoutine(person, slot, now, "route-rejected", "arrival-no-useful-opening"); continue; }
                selected = slot; selectedRoute = route;
                ObserveRoutine(person, slot, now, "selected", candidate.Id == proposed.PlaceId ? "preferred-feasible" : "alternative-feasible");
                return true;
            }
            MissRoutine(person, proposed, now, routeCalls == 2 ? "optional-search-exhausted" : "no-feasible-optional-venue");
            selected = new RoutineSlot(person.HomeId, Activity.Home, proposed.EndsAt);
            return false;
        }
    }
}
