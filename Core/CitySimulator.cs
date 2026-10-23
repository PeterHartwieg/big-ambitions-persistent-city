using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct AdvanceResult
    {
        public AdvanceResult(int events, bool caughtUp, int trafficEvents = 0) { Events = events; CaughtUp = caughtUp; TrafficEvents = trafficEvents; }
        public int Events { get; }
        public int TrafficEvents { get; }
        public bool CaughtUp { get; }
    }

    public sealed partial class CitySimulator
    {
        private sealed class Due
        {
            // ResidentLifecycle replaces residents in this same list slot; never rebind a lease
            // after external removal, replacement or reordering of the public snapshot list.
            public readonly int ListIndex;
            public Due(int listIndex) { ListIndex = listIndex; }
            public CitizenState Citizen = null!;
            public double Minute;
        }
        private sealed class DueOrder : IComparer<Due>
        {
            public int Compare(Due? a, Due? b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                int time = a.Minute.CompareTo(b.Minute);
                return time != 0 ? time : StringComparer.Ordinal.Compare(a.Citizen.Identity.Id, b.Citizen.Identity.Id);
            }
        }
        private readonly SortedSet<Due> due = new SortedSet<Due>(new DueOrder());
        private readonly Dictionary<string, Due> citizenEvents = new Dictionary<string, Due>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places = new Dictionary<string, Place>(StringComparer.Ordinal);
        private readonly IRoutePlanner routes;
        public CityState State { get; }
        private FoodCommerce? food;
        internal CitizenState? FindCitizen(string id) => citizenEvents.TryGetValue(id, out var entry) ? entry.Citizen : null;
        internal Place FindPlace(string id) => places[id];
        public void AttachFoodMarket(IFoodMarket market)
        {
            if (food != null) throw new InvalidOperationException("Food market already attached.");
            food = new FoodCommerce(this, market, routes);
        }

        private sealed class ActivityObservers
        {
            public Action<string, CitizenState, double>? Handlers;
            public Action<FoodDecisionTrace>? FoodHandlers;
            public Action<RoutineDiagnostic>? RoutineHandlers;
        }
        private ActivityObservers activityObservers = new ActivityObservers();
        // Optional observer. No extra per-event allocation or scheduling in normal play.
        // Shared across a route rebuild so subscribers can also unsubscribe through the old facade.
        public event Action<string, CitizenState, double>? ActivityObserved
        {
            add { activityObservers.Handlers += value; }
            remove { activityObservers.Handlers -= value; }
        }
        public event Action<FoodDecisionTrace>? FoodDecisionObserved
        {
            add { activityObservers.FoodHandlers += value; }
            remove { activityObservers.FoodHandlers -= value; }
        }
        public event Action<RoutineDiagnostic>? RoutineObserved
        {
            add { activityObservers.RoutineHandlers += value; }
            remove { activityObservers.RoutineHandlers -= value; }
        }
        private void ObserveRoutine(CitizenState person, RoutineSlot slot, double now, string outcome, string reason)
        {
            if (activityObservers.RoutineHandlers == null) return;
            activityObservers.RoutineHandlers(new RoutineDiagnostic(person.Identity.Id, person.PlaceId,
                slot.PlaceId, slot.Activity, now, slot.EndsAt, outcome, reason));
        }
        private void MissRoutine(CitizenState person, RoutineSlot slot, double now, string reason)
        {
            person.MissedActivities++;
            ObserveRoutine(person, slot, now, "missed", reason);
        }
        internal bool ObservingFood => activityObservers.FoodHandlers != null;
        internal void ObserveFood(FoodDecisionTrace trace) => activityObservers.FoodHandlers?.Invoke(trace);

        /// <summary>Rebuild scheduling indexes after retiming this same state, retaining observation
        /// ownership. The caller replaces the old simulator and must not advance it again.</summary>
        public CitySimulator RebuildRoutes(IRoutePlanner replacementRoutes)
        {
            var replacement = new CitySimulator(State, replacementRoutes);
            replacement.activityObservers = activityObservers;
            if (food != null) replacement.AttachFoodMarket(food.Market);
            return replacement;
        }

        public CitySimulator(CityState state, IRoutePlanner routes)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
            foreach (var place in state.Places)
            {
                places.Add(place.Id, place);
                if (place.Kind == PlaceKind.Shop || place.Kind == PlaceKind.Leisure) optionalPlaces.Add(place);
            }
            for (int citizenIndex = 0; citizenIndex < state.Citizens.Count; citizenIndex++)
            {
                var person = state.Citizens[citizenIndex];
                if (!Position.Finite(person.NextEventMinute) || person.NextEventMinute < state.Minute)
                    throw new ArgumentException("A citizen has an overdue or nonfinite event.");
                var item = new Due(citizenIndex) { Citizen = person, Minute = person.NextEventMinute };
                if (!due.Add(item)) throw new ArgumentException("Duplicate citizen event.");
                citizenEvents.Add(person.Identity.Id, item);
            }
            RestoreTraffic();
            EnsureTrafficNetwork();
        }

        public AdvanceResult AdvanceTo(double targetMinute, int eventBudget = 10000)
            => AdvanceCore(targetMinute, eventBudget, true);

        /// <summary>Native frames may stop between events for their time budget; publish once after the frame batch.</summary>
        public AdvanceResult AdvanceFrameSlice(double targetMinute, int eventBudget = 1)
            => AdvanceCore(targetMinute, eventBudget, false);

        private AdvanceResult AdvanceCore(double targetMinute, int eventBudget, bool publishSnapshot)
        {
            if (!Position.Finite(targetMinute) || targetMinute < State.Minute || targetMinute > 1e9)
                throw new ArgumentOutOfRangeException(nameof(targetMinute), "Simulation time only advances; load a saved snapshot to go back.");
            if (eventBudget < 1) throw new ArgumentOutOfRangeException(nameof(eventBudget));
            if (State.Food != null && food == null) throw new InvalidOperationException("Saved food economy requires its market before advancing.");
            EnsureTrafficNetwork();
            ValidatePhysicalClock(targetMinute);
            int count = 0, trafficCount = 0;
            while (count < eventBudget)
            {
                double citizenMinute = due.Count == 0 ? double.PositiveInfinity : due.Min!.Minute;
                double trafficMinute = traffic?.NextEventMinute ?? double.PositiveInfinity;
                double foodMinute = food?.NextMinute ?? double.PositiveInfinity;
                double next = Math.Min(foodMinute, Math.Min(citizenMinute, trafficMinute));
                if (next > targetMinute) break;
                State.Minute = next;
                if (foodMinute < citizenMinute && foodMinute < trafficMinute)
                {
                    traffic?.AdvanceTo(next, 0);
                    food!.Step(); count++; continue;
                }
                if (trafficMinute <= citizenMinute)
                {
                    traffic!.AdvanceTo(next, 1);
                    count++;
                    trafficCount++;
                    continue;
                }
                traffic?.AdvanceTo(next, 0);
                var item = due.Min!;
                due.Remove(item);
                string oldId = item.Citizen.Identity.Id;
                bool enteredTraffic = TryStartTraffic(item.Citizen);
                if (!enteredTraffic) item.Citizen = Step(item.Citizen, item.Minute);
                citizenEvents.Remove(oldId);
                citizenEvents[item.Citizen.Identity.Id] = item;
                if (!enteredTraffic && (!Position.Finite(item.Citizen.NextEventMinute) || item.Citizen.NextEventMinute <= State.Minute))
                    throw new InvalidOperationException("Each citizen event must schedule a later event.");
                if (!enteredTraffic)
                {
                    item.Minute = ScheduledMinute(item.Citizen);
                    due.Add(item);
                }
                count++;
                food?.Replace(oldId, item.Citizen);
                activityObservers.Handlers?.Invoke(oldId, item.Citizen, State.Minute);
            }
            bool caughtUp = (due.Count == 0 || due.Min!.Minute > targetMinute)
                && (traffic == null || traffic.NextEventMinute > targetMinute)
                && (food == null || food.NextMinute > targetMinute);
            if (caughtUp) { traffic?.AdvanceTo(targetMinute, 0); State.Minute = targetMinute; }
            if (publishSnapshot) SynchronizeSnapshot();
            return new AdvanceResult(count, caughtUp, trafficCount);
        }

        public Position PositionOf(CitizenState person) => person.Phase == CitizenPhase.Travelling && person.Journey != null
            ? person.Journey.PositionAt(JourneyProgress(person)) : places[person.PlaceId].Position;

        public Position? CarPositionOf(CitizenState person)
        {
            if (person.Car == null) return null;
            var position = person.Car.ParkedPosition;
            if (person.Phase != CitizenPhase.Travelling || person.Journey == null) return position;
            double elapsed = Math.Max(0, JourneyProgress(person));
            foreach (var leg in person.Journey.Legs)
            {
                if (elapsed <= leg.Minutes)
                    return leg.Mode == TravelMode.Car ? Position.Lerp(leg.From, leg.To, Math.Min(1, elapsed / leg.Minutes)) : position;
                if (leg.Mode == TravelMode.Car) position = leg.To;
                elapsed -= leg.Minutes;
            }
            return position;
        }

        public static void ValidateCarRoute(Route route, CitizenState person)
        {
            bool seenCar = false, leftCar = false;
            bool containsTransit = false;
            foreach (var leg in route.Legs)
            {
                if (leg.Mode == TravelMode.Bus || leg.Mode == TravelMode.Subway) containsTransit = true;
                if (leg.Mode == TravelMode.Car)
                {
                    if (!person.Preferences.OwnsCar || person.Car == null || person.Car.Id != person.Identity.Id + ":car"
                        || !person.Car.ParkedPosition.IsFinite || leftCar)
                        throw new InvalidOperationException("A car route requires its owned parked car and one contiguous car block.");
                    if (!seenCar && Position.Distance(leg.From, person.Car.ParkedPosition) > 0.01)
                        throw new InvalidOperationException("A car route must start driving at the owned car's parked position.");
                    seenCar = true;
                }
                else if (seenCar) leftCar = true;
            }
            if (seenCar && containsTransit) throw new InvalidOperationException("A car route cannot also use public transit.");
        }

        private static void CommitCarArrival(CitizenState person)
        {
            if (person.Journey == null) return;
            ValidateCarRoute(person.Journey, person);
            foreach (var leg in person.Journey.Legs)
                if (leg.Mode == TravelMode.Car) person.Car!.ParkedPosition = leg.To;
        }

        private CitizenState Step(CitizenState person, double now)
        {
            if (StepMealOuting(person, now)) return person;
            RoutineSlot? missedArrival = null;
            if ((person.Phase == CitizenPhase.WaitingForRoute || person.Phase == CitizenPhase.WaitingForOpening)
                && person.DestinationActivity != Activity.Home && now >= person.ActivityUntilMinute)
                MissRoutine(person, new RoutineSlot(person.DestinationId, person.DestinationActivity, person.ActivityUntilMinute),
                    now, person.Phase == CitizenPhase.WaitingForRoute ? "route-wait-expired" : "opening-wait-expired");
            if (person.Phase == CitizenPhase.Travelling)
            {
                CommitCarArrival(person);
                person.PlaceId = person.DestinationId;
                // Activity also describes the physical place's role for persistence. Admission is
                // recorded separately: reaching a closed venue or an expired window is not a visit.
                person.Activity = person.DestinationActivity;
                person.Phase = CitizenPhase.AtPlace;
                person.CompletedJourneys++;
                double admissionFrom = AdmissionFrom(person);
                bool admitted = person.DestinationActivity == Activity.Home
                    || (now >= admissionFrom && now < person.ActivityUntilMinute && DailyRoutine.Open(places[person.PlaceId], now));
                if (admitted)
                {
                    person.RecentVisits.Add(new VisitRecord { AtMinute = now, PlaceId = person.PlaceId, Activity = person.Activity });
                    if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
                }
                else
                {
                    // A closure during travel can still leave a later opening in this activity.
                    double? opening = now < person.ActivityUntilMinute
                        ? DailyRoutine.NextOpen(places[person.PlaceId], Math.Max(now, admissionFrom), person.ActivityUntilMinute) : null;
                    if (opening.HasValue)
                    {
                        WaitForOpening(person, new RoutineSlot(person.PlaceId, person.Activity, person.ActivityUntilMinute), opening.Value);
                        return person;
                    }
                    MissRoutine(person, new RoutineSlot(person.PlaceId, person.Activity, person.ActivityUntilMinute),
                        now, now >= person.ActivityUntilMinute ? "arrival-after-deadline" : "arrival-no-opening");
                    missedArrival = new RoutineSlot(person.PlaceId, person.Activity, person.ActivityUntilMinute);
                }
                person.Journey = null;
                person.DestinationId = "";
                person.DepartureMinute = 0;
                person = ResidentLifecycle.ApplyAtHome(State, person, now);
                // Retain a home fallback until its original window ends. This also survives reload
                // and prevents counting the same closed activity again after the journey home.
                if (person.Activity == Activity.Home && now < person.ActivityUntilMinute)
                {
                    person.NextEventMinute = HomeEvent(person, person.ActivityUntilMinute, now);
                    return person;
                }
            }
            person = ResidentLifecycle.ApplyAtHome(State, person, now);
            if (person.CommutePreparation.PlaceId != person.DayPlaceId)
                person.CommutePreparation = new CommutePreparation();
            // An admitted activity retains its saved deadline, including an old
            // routine loaded after a policy change. A closure still ends attendance.
            bool attending = person.Phase == CitizenPhase.AtPlace && person.Activity != Activity.Home
                && now < person.ActivityUntilMinute;
            double? currentClose = attending ? DailyRoutine.NextClose(places[person.PlaceId], now) : null;
            if (attending && currentClose.HasValue)
            {
                person.NextEventMinute = Math.Min(person.ActivityUntilMinute, currentClose.Value);
                return person;
            }
            // Waits own their original destination/window too. In particular, a
            // failed early commute must not become Home on its next pre-shift retry.
            bool committedWait = (person.Phase == CitizenPhase.WaitingForRoute || person.Phase == CitizenPhase.WaitingForOpening)
                && person.DestinationId != "" && now < person.ActivityUntilMinute;
            bool homeFallback = IsHomeRoutineFallback(person, now);
            var slot = committedWait
                ? new RoutineSlot(person.DestinationId, person.DestinationActivity, person.ActivityUntilMinute)
                : attending || homeFallback ? new RoutineSlot(person.HomeId, Activity.Home, person.ActivityUntilMinute)
                : DailyRoutine.At(person, State.Seed, now, State.Places);
            Route? commuteRoute = null;
            bool earlyCommute = false;
            double commuteAt = double.PositiveInfinity;
            if (!homeFallback && TryPrepareCommute(person, now, out var commute, out commuteRoute, out commuteAt) && now >= commuteAt)
            {
                slot = new RoutineSlot(commute.PlaceId, commute.Activity, commute.EndsAt);
                earlyCommute = true;
            }
            Route? optionalRoute = null;
            bool optionalSelected = false;
            if (!committedWait && !attending && !homeFallback && !earlyCommute
                && (slot.Activity == Activity.Shop || slot.Activity == Activity.Leisure))
                optionalSelected = ChooseOptionalDestination(person, slot, now, out slot, out optionalRoute);
            if (!places.TryGetValue(slot.PlaceId, out var destination)) throw new InvalidOperationException("Unknown destination " + slot.PlaceId);
            bool commutingToWork = routes is ICommuteTimingPlanner && person.PlaceId == person.HomeId && person.Journey == null
                && (slot.Activity == Activity.Work || slot.Activity == Activity.Study) && slot.PlaceId != person.PlaceId;
            if (!earlyCommute && !commutingToWork && (!optionalSelected || slot.PlaceId == person.PlaceId) && slot.Activity != Activity.Home && !DailyRoutine.Open(destination, now))
            {
                bool attendedUntilClosure = person.Phase == CitizenPhase.AtPlace
                    && person.PlaceId == slot.PlaceId && person.Activity == slot.Activity;
                bool alreadyMissedArrival = missedArrival.HasValue && missedArrival.Value.PlaceId == slot.PlaceId
                    && missedArrival.Value.Activity == slot.Activity && missedArrival.Value.EndsAt == slot.EndsAt;
                double? retryAt = !attendedUntilClosure && !alreadyMissedArrival
                    ? DailyRoutine.NextOpen(destination, now, slot.EndsAt) : null;
                if (retryAt.HasValue)
                {
                    WaitForOpening(person, slot, retryAt.Value);
                    return person;
                }
                if (!attendedUntilClosure && !alreadyMissedArrival)
                    MissRoutine(person, slot, now, "no-opening-in-window");
                slot = new RoutineSlot(person.HomeId, Activity.Home, slot.EndsAt);
                destination = places[person.HomeId];
            }
            person.ActivityUntilMinute = slot.EndsAt;
            if (slot.PlaceId == person.PlaceId)
            {
                if (person.Phase == CitizenPhase.WaitingForOpening && person.DestinationId == slot.PlaceId
                    && person.DestinationActivity == slot.Activity && slot.Activity != Activity.Home)
                {
                    person.RecentVisits.Add(new VisitRecord { AtMinute = now, PlaceId = slot.PlaceId, Activity = slot.Activity });
                    if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
                }
                person.Phase = CitizenPhase.AtPlace;
                person.Activity = slot.Activity;
                person.DestinationId = "";
                person.ActivityUntilMinute = slot.Activity == Activity.Home ? slot.EndsAt
                    : Math.Min(slot.EndsAt, DailyRoutine.NextClose(destination, now)!.Value);
                person.NextEventMinute = ResidentLifecycle.NextHomeEvent(person, person.ActivityUntilMinute, now);
                if (slot.Activity == Activity.Home && commuteAt > now && commuteAt < person.NextEventMinute) person.NextEventMinute = commuteAt;
                return person;
            }
            person.DestinationId = slot.PlaceId;
            person.DestinationActivity = slot.Activity;
            var route = optionalSelected ? optionalRoute : earlyCommute ? commuteRoute : routes is ICitizenRoutePlanner citizenRoutes
                ? citizenRoutes.Plan(places[person.PlaceId], destination, now, person)
                : routes.Plan(places[person.PlaceId], destination, now, person.Preferences);
            if (route == null)
            {
                ObserveRoutine(person, slot, now, "route-rejected", "unreachable");
                person.Phase = CitizenPhase.WaitingForRoute;
                person.NextEventMinute = Math.Min(now + 30, slot.EndsAt);
                return person;
            }
            ValidateRoute(route, places[person.PlaceId].Position, destination.Position);
            ValidateCarRoute(route, person);
            person.Phase = CitizenPhase.Travelling;
            person.Journey = route;
            person.DepartureMinute = now;
            person.NextEventMinute = now + route.Minutes;
            return person;
        }

        private double AdmissionFrom(CitizenState person)
        {
            if (!(routes is ICommuteTimingPlanner) || (person.DestinationActivity != Activity.Work && person.DestinationActivity != Activity.Study)) return 0;
            var window = DailyRoutine.WorkOnDay(person, State.Seed, Math.Max(0, person.ActivityUntilMinute - 0.001));
            return window.HasValue && window.Value.EndsAt == person.ActivityUntilMinute ? window.Value.StartsAt : 0;
        }

        private bool TryPrepareCommute(CitizenState person, double now, out WorkWindow window, out Route? route, out double leaveAt, bool allowPlanning = true)
        {
            window = default; route = null; leaveAt = double.PositiveInfinity;
            if (!(routes is ICommuteTimingPlanner) || !ResidentLifecycle.IsSafelyHome(person)) return false;
            // A current shift is handled by the ordinary activity path, never by
            // planning tomorrow while today's worker still needs to leave home.
            var current = DailyRoutine.WorkOnDay(person, State.Seed, now);
            if (current.HasValue && now >= current.Value.StartsAt && now < current.Value.EndsAt) return false;
            var planned = DailyRoutine.NextWork(person, State.Seed, now);
            if (!planned.HasValue || planned.Value.PlaceId == person.HomeId) return false;
            window = planned.Value;
            // A return-home fallback owns a missed activity's original deadline.
            // Replanning that same shift after an early closed-venue arrival would
            // send the resident out again and count the same miss twice.
            if (person.ActivityUntilMinute == window.EndsAt) return false;
            var preparation = person.CommutePreparation;
            bool matches = preparation.PlaceId == window.PlaceId && preparation.WorkStartsAt == window.StartsAt
                && preparation.WorkEndsAt == window.EndsAt;
            if (matches)
            {
                // A failed estimate is retried at shift start, not at every home
                // event overnight. A successful estimate schedules one departure.
                leaveAt = preparation.TravelMinutes > 0
                    ? Math.Max(preparation.PreparedAt, window.StartsAt - preparation.TravelMinutes) : window.StartsAt;
                if (now < leaveAt || !allowPlanning) return true;
                route = PlanCommute(person, window, now);
                if (route != null) { preparation.TravelMinutes = route.EstimatedMinutes; preparation.PreparedAt = now; }
                return true;
            }
            double previousMinutes = preparation.PlaceId == window.PlaceId ? preparation.TravelMinutes : 0;
            double prepareAt = DailyRoutine.CommutePreparationAt(person, State.Seed, window, previousMinutes);
            if (now < prepareAt) { leaveAt = prepareAt; return false; }
            if (previousMinutes > 0)
            {
                // A known commute supplies tomorrow's departure estimate without
                // another route search. Changes are resolved honestly at departure.
                person.CommutePreparation = new CommutePreparation
                {
                    PlaceId = window.PlaceId, WorkStartsAt = window.StartsAt, WorkEndsAt = window.EndsAt,
                    PreparedAt = now, TravelMinutes = previousMinutes
                };
                leaveAt = Math.Max(now, window.StartsAt - previousMinutes);
                if (now < leaveAt || !allowPlanning) return true;
                route = PlanCommute(person, window, now);
                if (route != null) person.CommutePreparation.TravelMinutes = route.EstimatedMinutes;
                return true;
            }
            // A home arrival only schedules work. Its next event will perform any
            // required search once, retaining the result when departure is due now.
            if (!allowPlanning) { leaveAt = now; return false; }
            // Only a duration estimate is retained. Route geometry and winners are
            // freshly selected at departure so reopened/off-path roads can compete.
            route = PlanCommute(person, window, now);
            double minutes = route?.EstimatedMinutes ?? 0;
            person.CommutePreparation = new CommutePreparation
            {
                PlaceId = window.PlaceId, WorkStartsAt = window.StartsAt, WorkEndsAt = window.EndsAt,
                PreparedAt = now, TravelMinutes = minutes
            };
            // A first unknown or changed route can already be too long. Leave now
            // when necessary; never backdate a departure or invent punctuality.
            leaveAt = minutes > 0 ? Math.Max(now, window.StartsAt - minutes) : window.StartsAt;
            return true;
        }

        private Route? PlanCommute(CitizenState person, WorkWindow window, double now)
        {
            var home = places[person.HomeId]; var destination = places[window.PlaceId];
            var route = routes is ICitizenRoutePlanner citizenRoutes ? citizenRoutes.Plan(home, destination, now, person)
                : routes.Plan(home, destination, now, person.Preferences);
            if (route != null) { ValidateRoute(route, home.Position, destination.Position); ValidateCarRoute(route, person); }
            return route;
        }

        private bool IsHomeRoutineFallback(CitizenState person, double now)
        {
            if (!ResidentLifecycle.IsSafelyHome(person) || now >= person.ActivityUntilMinute) return false;
            // Existing saves encode a failed activity window as Home until its
            // original deadline. Preserve that window even when timing conversion
            // or a lifecycle event wakes this resident before the deadline.
            var original = DailyRoutine.At(person, State.Seed, Math.Max(0, person.ActivityUntilMinute - 0.001));
            return original.Activity != Activity.Home && original.EndsAt == person.ActivityUntilMinute;
        }

        private double HomeEvent(CitizenState person, double deadline, double now)
        {
            double next = ResidentLifecycle.NextHomeEvent(person, deadline, now);
            if (IsHomeRoutineFallback(person, now)) return next;
            TryPrepareCommute(person, now, out _, out _, out double leaveAt, allowPlanning: false);
            if (Position.Finite(leaveAt)) next = Math.Min(next, Math.Max(now + 0.000001, leaveAt));
            return next;
        }

        private static void WaitForOpening(CitizenState person, RoutineSlot slot, double opening)
        {
            // Wait at the current entrance; retain the intended destination and full deadline.
            // In particular, do not invent a return-home trip that can consume the activity.
            person.Phase = CitizenPhase.WaitingForOpening;
            person.DestinationId = slot.PlaceId;
            person.DestinationActivity = slot.Activity;
            person.ActivityUntilMinute = slot.EndsAt;
            person.NextEventMinute = opening;
            person.Journey = null;
            person.DepartureMinute = 0;
        }

        public static void ValidateRoute(Route route, Position from, Position to)
        {
            if (route.Legs.Count == 0 || route.Legs.Count > 512) throw new InvalidOperationException("Invalid route size.");
            Position previous = from;
            foreach (var leg in route.Legs)
            {
                if (!leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) || leg.Minutes <= 0
                    || Position.Distance(previous, leg.From) > 0.1 || !Enum.IsDefined(typeof(TravelMode), leg.Mode))
                    throw new InvalidOperationException("A route is nonfinite, discontinuous or has invalid time/mode.");
                previous = leg.To;
            }
            if (Position.Distance(previous, to) > 0.1 || !Position.Finite(route.Minutes)) throw new InvalidOperationException("A route does not end at its destination.");
        }
    }
}
