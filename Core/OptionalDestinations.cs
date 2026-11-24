using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private readonly List<Place> optionalPlaces = new List<Place>();
        private static double MinimumOptionalStay(Activity activity) => activity == Activity.Shop ? 5 : 15;

        private static double? UsefulOpening(Place place, double arrival, double deadline, double stay)
        {
            if (arrival + stay > deadline) return null;
            var opening = DailyRoutine.NextOpen(place, arrival, deadline);
            if (!opening.HasValue) return null;
            double close = DailyRoutine.NextClose(place, opening.Value)!.Value;
            return opening.Value + stay <= Math.Min(deadline, close) ? opening : null;
        }

        private sealed class OptionalChoice
        {
            public OptionalChoice(RoutineSlot proposed) { Slot = proposed; }
            public RoutineSlot Slot;
            public Route? Route;
            public bool Selected;
            public readonly List<Place> Candidates = new List<Place>(9);
            public readonly List<(Place Place, double Score)> Ranked = new List<(Place Place, double Score)>(9);
        }

        private static readonly string[] ActivityNames = Enum.GetNames(typeof(Activity));
        internal static string OptionalKey(string personId, Activity activity, double endsAt)
            => personId + ":optional:" + activity + ":" + endsAt.ToString("R", CultureInfo.InvariantCulture);
        internal static uint OptionalKeyState(string personId, Activity activity, double endsAt, int seed)
        {
            uint state = StableRandom.Append(StableRandom.Append(StableRandom.Begin(seed), personId), ":optional:");
            int index = (int)activity;
            state = StableRandom.Append(StableRandom.Append(state, index >= 0 && index < ActivityNames.Length ? ActivityNames[index] : activity.ToString()), ':');
            Span<char> text = stackalloc char[32];
            return endsAt.TryFormat(text, out int written, "R", CultureInfo.InvariantCulture)
                ? StableRandom.Append(state, text.Slice(0, written))
                : StableRandom.Append(state, endsAt.ToString("R", CultureInfo.InvariantCulture));
        }

        private IEnumerable<RouteRequest> ChooseOptionalDestination(CitizenState person, RoutineSlot proposed, double now, OptionalChoice choice)
        {
            choice.Slot = proposed; choice.Route = null; choice.Selected = false;
            var origin = places[person.PlaceId];
            var kind = proposed.Activity == Activity.Shop ? PlaceKind.Shop : PlaceKind.Leisure;
            uint key = OptionalKeyState(person.Identity.Id, proposed.Activity, proposed.EndsAt, State.Seed);
            var weather=CaptureOuting(person,now,key);
            if(weather.Value==0)
            {
                choice.Slot=new RoutineSlot(person.HomeId,Activity.Home,proposed.EndsAt);
                DayPlanExecutor.Mark(person,proposed.Activity,proposed.EndsAt,PlanItemState.Cancelled);
                ObserveRoutine(person,proposed,now,"weather-declined","captured-rain");
                yield break;
            }
            var candidates = choice.Candidates; candidates.Clear();
            if (places.TryGetValue(proposed.PlaceId, out var preferred) && preferred.Kind == kind && preferred.Capacity > 0)
                candidates.Add(preferred);
            DestinationChoice.Shortlist(optionalPlaces, origin, kind, key, choice.Ranked);
            foreach (var ranked in choice.Ranked)
                if (ranked.Place.Id != proposed.PlaceId && candidates.Count < 8) candidates.Add(ranked.Place);
            choice.Ranked.Clear();
            int routeCalls = 0;
            double stay = MinimumOptionalStay(proposed.Activity);
            var cause = PlanMissCause.None;
            foreach (var candidate in candidates)
            {
                bool first = ReferenceEquals(candidate, candidates[0]);
                var slot = new RoutineSlot(candidate.Id, proposed.Activity, proposed.EndsAt);
                if ((candidate.Id != person.PlaceId || RecordsSamePlaceArrival(person, slot))
                    && !CanAdmitOrdinary(person, candidate.Id, proposed.Activity, now))
                { if (first) cause = PlanMissCause.Refused; ObserveRoutine(person, slot, now, "candidate-rejected", "paid-visit-unaffordable"); continue; }
                if (!UsefulOpening(candidate, now, slot.EndsAt, stay).HasValue)
                { if (first) cause = PlanMissCause.Closed; ObserveRoutine(person, slot, now, "candidate-rejected", "no-useful-opening"); continue; }
                if (!person.Preferences.OwnsCar && !person.Preferences.AllowTransit && candidate.Id != origin.Id)
                {
                    if (Position.Distance(origin.Position, candidate.Position) > person.Preferences.WalkingToleranceMetres
                        || (routes is IWalkingTimeBound bound && now + bound.MinimumWalkingMinutes(origin, candidate) + stay > slot.EndsAt))
                    { if (first) cause = PlanMissCause.NoRoute; ObserveRoutine(person, slot, now, "candidate-rejected", "walking-bound"); continue; }
                }
                Route? route = null;
                if (candidate.Id != origin.Id)
                {
                    if (routeCalls == 2) break;
                    routeCalls++;
                    var request = CitizenRequest(origin, candidate, now, person);
                    yield return request;
                    route = request.Result;
                    if (route == null)
                    { if (first) cause = PlanMissCause.NoRoute; ObserveRoutine(person, slot, now, "route-rejected", "unreachable"); continue; }
                    ValidateRoute(route, origin.Position, candidate.Position); ValidateCarRoute(route, person);
                }
                double arrival = now + (route?.EstimatedMinutes ?? 0);
                if (!UsefulOpening(candidate, arrival, slot.EndsAt, stay).HasValue)
                { if (first) cause = PlanMissCause.Late; ObserveRoutine(person, slot, now, "route-rejected", "arrival-no-useful-opening"); continue; }
                choice.Slot = slot; choice.Route = route; choice.Selected = true;
                ObserveRoutine(person, slot, now, "selected", candidate.Id == proposed.PlaceId ? "preferred-feasible" : "alternative-feasible");
                yield break;
            }
            MissRoutine(person, proposed, now, routeCalls == 2 ? "optional-search-exhausted" : "no-feasible-optional-venue", cause);
            choice.Slot = new RoutineSlot(person.HomeId, Activity.Home, proposed.EndsAt);
            choice.Route = null; choice.Selected = false;
        }
    }
}
