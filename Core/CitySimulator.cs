using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PersistentCity.Tests")]

namespace PersistentCity.Core
{
    public readonly struct AdvanceResult
    {
        public AdvanceResult(int events, bool caughtUp, int trafficEvents = 0, bool planningPending = false, int foodContinuationEvents = 0)
        { Events = events; CaughtUp = caughtUp; TrafficEvents = trafficEvents; PlanningPending = planningPending; FoodContinuationEvents = foodContinuationEvents; }
        /// <summary>Total bounded work units, including citizen, traffic and food continuation units.</summary>
        public int Events { get; }
        public int TrafficEvents { get; }
        /// <summary>Bounded units of an already-started food decision, including its commit.
        /// The begin unit counts once as a citizen event; continuations still consume the total unit/time budget.</summary>
        public int FoodContinuationEvents { get; }
        public int CitizenEvents => Events - TrafficEvents - FoodContinuationEvents;
        public bool CaughtUp { get; }
        /// <summary>A citizen event is waiting for a route answer. The clock stays at that event's minute
        /// until ContinuePlanning reports completion; nothing else advances meanwhile.</summary>
        public bool PlanningPending { get; }
    }

    public sealed partial class CitySimulator
    {
        private sealed class Due
        {
            // Never rebind a lease after external removal, replacement or reordering
            // of the public snapshot list. Residents retain their identities.
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
        /// <summary>Read-only inspector view of one resident from already stored facts, or null if the
        /// ID has left the population. Never changes the city, draws, plans routes or reads a market.</summary>
        public ResidentInsight? Inspect(string residentId, Func<string, string>? productName = null, Func<string, int, string>? formatAddress = null,
            Func<string, string?>? shopName = null, Func<FoodScope, bool>? currentOwnShop = null)
        {
            if (residentId == null) return null;
            var person = FindCitizen(residentId);
            if (person == null) return null;
            ResidentMeal? meal = food != null ? food.FindMeal(residentId) : State.Food?.Residents.Find(m => m.ResidentId == residentId);
            return ResidentInsight.Capture(State, person, meal, id => places.TryGetValue(id, out var place) ? place : null, productName, formatAddress, food?.MemorySettings, food == null ? null : food.MemoryReference, shopName, currentOwnShop);
        }
        public void AttachFoodMarket(IFoodMarket market, FoodChoiceSettings? choiceSettings = null, FoodMemorySettings? memorySettings = null)
        {
            if (food != null) throw new InvalidOperationException("Food market already attached.");
            food = new FoodCommerce(this, market, routes, choiceSettings, memorySettings);
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
        /// ownership. The caller replaces the old simulator and must not advance it again. An event
        /// waiting for a route is abandoned here; the state still holds it unstarted, so the replacement
        /// runs it again from the beginning.</summary>
        public CitySimulator RebuildRoutes(IRoutePlanner replacementRoutes)
        {
            AbortPending();
            var replacement = new CitySimulator(State, replacementRoutes);
            replacement.activityObservers = activityObservers;
            if (stopEnvironment != null) replacement.AttachOptionalStops(stopEnvironment, stopPercentage, stopTiming);
            if (food != null) replacement.AttachFoodMarket(food.Market, food.ChoiceSettings, food.MemorySettings);
            return replacement;
        }

        public CitySimulator(CityState state, IRoutePlanner routes)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (state.Capabilities == null) throw new InvalidOperationException("Missing population capabilities.");
            state.Capabilities.RequireExecutable();
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

        /// <summary>Native frames may stop between events for their time budget; publish once after the frame batch.
        /// With a resumable planner a citizen event that needs a route returns PlanningPending instead of
        /// finishing; drive it with ContinuePlanning until that reports completion.</summary>
        public AdvanceResult AdvanceFrameSlice(double targetMinute, int eventBudget = 1)
            => AdvanceCore(targetMinute, eventBudget, false);

        // Several events may share the published minute, especially daily budget postings.
        // Minute equality alone does not establish that the simulation has drained its work.
        public bool IsCaughtUpTo(double targetMinute) => Position.Finite(targetMinute)
            && State.Minute >= targetMinute && NoEventsThrough(targetMinute);

        private bool NoEventsThrough(double targetMinute) => pending == null && food?.HasPendingDecision != true
            && (due.Count == 0 || due.Min!.Minute > targetMinute)
            && (traffic == null || traffic.NextEventMinute > targetMinute)
            && (State.Food == null || (food != null && food.NextMinute > targetMinute))
            && (State.Capabilities.Active || (Math.Floor(State.Minute / 1440) + 1) * 1440 > targetMinute);

        private AdvanceResult AdvanceCore(double targetMinute, int eventBudget, bool publishSnapshot)
        {
            if (!Position.Finite(targetMinute) || targetMinute < State.Minute || targetMinute > 1e9)
                throw new ArgumentOutOfRangeException(nameof(targetMinute), "Simulation time only advances; load a saved snapshot to go back.");
            if (eventBudget < 1) throw new ArgumentOutOfRangeException(nameof(eventBudget));
            if (State.Food != null && food == null) throw new InvalidOperationException("Saved food economy requires its market before advancing.");
            EnsureTrafficNetwork();
            ValidatePhysicalClock(targetMinute);
            int count = 0, trafficCount = 0, foodContinuationCount = 0;
            if (pending != null)
            {
                // The non-frame path finishes an interrupted event inline; a frame slice leaves it to ContinuePlanning.
                if (!publishSnapshot) return new AdvanceResult(0, false, 0, planningPending: true);
                // A restart requeues the event unstarted; the loop below then runs it again inline.
                bool completed = false;
                while (pending != null) completed = ContinuePlanning(double.PositiveInfinity);
                if (completed) count++;
            }
            while (count < eventBudget)
            {
                double citizenMinute = due.Count == 0 ? double.PositiveInfinity : due.Min!.Minute;
                double trafficMinute = traffic?.NextEventMinute ?? double.PositiveInfinity;
                double foodMinute = food?.NextMinute ?? double.PositiveInfinity;
                double activation = State.Capabilities.Active ? double.PositiveInfinity : (Math.Floor(State.Minute / 1440) + 1) * 1440;
                double next = Math.Min(activation, Math.Min(foodMinute, Math.Min(citizenMinute, trafficMinute)));
                if (next > targetMinute) break;
                State.Minute = next;
                if (next == activation) { State.Capabilities.Active = true; count++; continue; }
                if (food?.HasPendingDecision == true || foodMinute < citizenMinute && foodMinute < trafficMinute)
                {
                    traffic?.AdvanceTo(next, 0);
                    // Charge a decision once at begin. A restored decision has already begun;
                    // its quote/selection/commit units remain bounded work, not new citizen events.
                    bool continuation = food!.HasPendingDecision;
                    food.Step(); count++;
                    if (continuation) foodContinuationCount++;
                    continue;
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
                if (TryStartTraffic(item.Citizen))
                {
                    count++;
                    food?.ObserveStop(item.Citizen);
                    activityObservers.Handlers?.Invoke(item.Citizen.Identity.Id, item.Citizen, State.Minute);
                    continue;
                }
                bool inline = publishSnapshot;
                if (!inline && RestartsExhausted(item)) { inline = true; InlineAfterRestarts++; }
                if (!RunEvent(item, inline))
                    return new AdvanceResult(count, false, trafficCount, planningPending: true, foodContinuationEvents: foodContinuationCount);
                count++;
            }
            bool caughtUp = NoEventsThrough(targetMinute);
            if (caughtUp) { traffic?.AdvanceTo(targetMinute, 0); State.Minute = targetMinute; }
            if (publishSnapshot) SynchronizeSnapshot();
            return new AdvanceResult(count, caughtUp, trafficCount, foodContinuationEvents: foodContinuationCount);
        }

        // One citizen event in flight. The event runs on a working copy of the resident, so the published
        // state keeps the unstarted event (NextEventMinute equal to the clock) until the copy is committed.
        // A save or a planner rebuild taken meanwhile therefore stores and replays the event from its start.
        private sealed class PendingEvent
        {
            public PendingEvent(Due item, CitizenState working, IEnumerator<RouteRequest> steps)
            { Item = item; Working = working; Steps = steps; }
            public readonly Due Item;
            public readonly CitizenState Working;
            public readonly IEnumerator<RouteRequest> Steps;
            public RouteRequest? Request;
            public IRoutePlanningJob? Job;
        }
        private PendingEvent? pending;
        /// <summary>True while a citizen event waits for its route answer.</summary>
        public bool PlanningPending => pending != null;
        /// <summary>Route requests handed to a resumable planner, inline or across frames.</summary>
        public long PlanningRequests { get; private set; }
        private bool InProgress(Due item) => pending != null && ReferenceEquals(pending.Item, item);
        /// <summary>Citizen events abandoned between slices because a job lost its captured native state and
        /// requeued to run again from their first request.</summary>
        public long EventRestarts { get; private set; }
        /// <summary>Events run inline in a frame slice because the same resident's event had already restarted
        /// MaximumEventRestarts times at the same held minute.</summary>
        public long InlineAfterRestarts { get; private set; }
        // A city whose snapshot keeps changing must still progress: after this many restarts of one event at one
        // held minute, the next attempt runs inline, where every job gets an unlimited budget in one call.
        private const int MaximumEventRestarts = 3;
        private Due? restartedItem;
        private double restartedMinute;
        private int restartedCount;
        private bool RestartsExhausted(Due item) => ReferenceEquals(restartedItem, item)
            && restartedMinute == State.Minute && restartedCount >= MaximumEventRestarts;

        // Returns false only when a resumable planner's job was left pending for the frame loop.
        private bool RunEvent(Due item, bool inline)
        {
            var working = CloneForEvent(item.Citizen);
            var evt = new PendingEvent(item, working, StepEvent(working, item.Minute).GetEnumerator());
            try
            {
                while (evt.Steps.MoveNext())
                {
                    var request = evt.Steps.Current;
                    if (routes is IResumableRoutePlanner resumable)
                    {
                        PlanningRequests++;
                        var job = resumable.Begin(request);
                        if (!inline) { evt.Request = request; evt.Job = job; pending = evt; return false; }
                        try { while (!job.Continue(double.PositiveInfinity)) { } request.Answer(job.Result); }
                        finally { job.Dispose(); }
                    }
                    else request.Answer(AnswerSynchronously(request));
                }
            }
            catch { evt.Steps.Dispose(); due.Add(item); throw; }
            CompleteEvent(evt);
            return true;
        }

        private Route? AnswerSynchronously(RouteRequest request)
            => request.Citizen != null && routes is ICitizenRoutePlanner citizenRoutes
                ? citizenRoutes.Plan(request.From, request.To, request.DepartureMinute, request.Citizen)
                : routes.Plan(request.From, request.To, request.DepartureMinute, request.Preferences);

        /// <summary>Give the pending route job up to the budget (it always performs at least one unit of
        /// work), resume the event when the answer arrives, and report true once no request is pending.
        /// A planner exception leaves the resident's published state untouched with the event still due;
        /// the next advance runs it again from the beginning. A job that throws RoutePlanningRestartRequired
        /// abandons the whole event the same way, without propagating: earlier answers of the event belong to
        /// the replaced snapshot, so the working copy is discarded, the clock stays held and this returns false.
        /// The next advance replays the event from its first request on the new snapshot, as a reload would.</summary>
        public bool ContinuePlanning(double budgetMilliseconds)
        {
            if (double.IsNaN(budgetMilliseconds) || budgetMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            var evt = pending;
            if (evt == null) return true;
            try
            {
                var job = evt.Job!;
                if (!job.Continue(budgetMilliseconds)) return false;
                evt.Request!.Answer(job.Result);
                job.Dispose(); evt.Job = null; evt.Request = null;
                if (evt.Steps.MoveNext())
                {
                    PlanningRequests++;
                    evt.Request = evt.Steps.Current;
                    evt.Job = ((IResumableRoutePlanner)routes).Begin(evt.Request);
                    return false;
                }
                pending = null;
                CompleteEvent(evt);
                return true;
            }
            catch (RoutePlanningRestartRequired) { RestartPending(evt); return false; }
            catch { AbortPending(); throw; }
        }

        private void RestartPending(PendingEvent evt)
        {
            EventRestarts++;
            if (ReferenceEquals(restartedItem, evt.Item) && restartedMinute == State.Minute) restartedCount++;
            else { restartedItem = evt.Item; restartedMinute = State.Minute; restartedCount = 1; }
            AbortPending();
        }

        private void AbortPending()
        {
            var evt = pending;
            if (evt == null) return;
            pending = null;
            try { evt.Job?.Dispose(); }
            finally
            {
                evt.Steps.Dispose();
                if (!due.Add(evt.Item)) throw new InvalidOperationException("An interrupted event lost its queue slot.");
            }
        }

        private void CompleteEvent(PendingEvent evt)
        {
            var item = evt.Item; var working = evt.Working; var live = item.Citizen;
            evt.Steps.Dispose();
            if (!Position.Finite(working.NextEventMinute) || working.NextEventMinute <= State.Minute)
            {
                if (!due.Add(item)) throw new InvalidOperationException("An invalid event lost its queue slot.");
                throw new InvalidOperationException("Each citizen event must schedule a later event.");
            }
            var arrivalRoute = live.Phase == CitizenPhase.Travelling ? live.Journey : null;
            // A routine/lifecycle event can retain the admitted visit without recording a new one.
            // Keep its saved acceptance or decline; never reconstruct it using a changed rate.
            bool sameVisit = live.Phase == CitizenPhase.AtPlace && working.Phase == CitizenPhase.AtPlace
                && live.PlaceId == working.PlaceId && live.Activity == working.Activity
                && live.RecentVisits.Count > 0 && working.RecentVisits.Count > 0
                && live.RecentVisits[live.RecentVisits.Count - 1].AtMinute == working.RecentVisits[working.RecentVisits.Count - 1].AtMinute
                && live.RecentVisits[live.RecentVisits.Count - 1].PlaceId == working.RecentVisits[working.RecentVisits.Count - 1].PlaceId;
            if (sameVisit)
            {
                working.OptionalStopChoice = live.OptionalStopChoice;
                // Retain consumed-opportunity evidence until this visit ends. The event still
                // ends the stop; an accepted visitor must not be recruited again after it clears.
                working.OptionalStop = live.OptionalStop?.Copy();
            }
            // A pending chance outlives its visit. Read it from the live citizen at commit, so a chance
            // handed on while this event was in flight is not restored.
            else if (live.OptionalStopChoice == true && live.OptionalStop == null) working.OptionalStopChoice = true;
            CommitEvent(live, working);
            if (sameVisit && live.OptionalStop != null) CancelOptionalStop(live.Identity.Id);
            if (ReferenceEquals(restartedItem, item)) restartedItem = null;
            item.Minute = ScheduledMinute(live);
            // The in-flight item is outside the queue; failing to add it means it was queued meanwhile and
            // its sort key just changed inside the set.
            if (!due.Add(item)) throw new InvalidOperationException("A completed event was already queued.");
            food?.ObserveStop(live);
            ScheduleOptionalStop(live, arrivalRoute);
            FileOptionalStop(live);
            activityObservers.Handlers?.Invoke(live.Identity.Id, live, State.Minute);
        }

        // The event works on a copy that shares the immutable identity and the route object (events replace
        // journeys, never edit their legs) and owns copies of everything an event edits in place.
        internal static CitizenState CloneForEvent(CitizenState live) => new CitizenState
        {
            Identity = live.Identity, HomeId = live.HomeId, DayPlaceId = live.DayPlaceId, ShopId = live.ShopId, LeisureId = live.LeisureId,
            Preferences = new TravelPreferences { OwnsCar = live.Preferences.OwnsCar, AllowTransit = live.Preferences.AllowTransit,
                WalkingToleranceMetres = live.Preferences.WalkingToleranceMetres, FareWeightMinutes = live.Preferences.FareWeightMinutes },
            CommutePreparation = CopyPreparation(live.CommutePreparation),
            MealOuting = live.MealOuting == null ? null : CopyOuting(live.MealOuting),
            OptionalStop = live.OptionalStop?.Copy(),
            OptionalStopChoice = live.OptionalStopChoice,
            Car = live.Car == null ? null : CopyCar(live.Car),
            JoinedMinute = live.JoinedMinute, LastEmploymentCheckDay = live.LastEmploymentCheckDay, JobChanges = live.JobChanges,
            Phase = live.Phase, Activity = live.Activity, PlaceId = live.PlaceId, DestinationId = live.DestinationId,
            DestinationActivity = live.DestinationActivity, Journey = live.Journey, DepartureMinute = live.DepartureMinute,
            NextEventMinute = live.NextEventMinute, ActivityUntilMinute = live.ActivityUntilMinute,
            CompletedJourneys = live.CompletedJourneys, MissedActivities = live.MissedActivities,
            RecentVisits = new List<VisitRecord>(live.RecentVisits),
            // Immutable once generated, so the copy shares it; events never change traits.
            Traits = live.Traits,
            // Derived from the shared identity and the seed, so the copy may share it.
            RoutineKeys = live.RoutineKeys
        };
        private static CommutePreparation CopyPreparation(CommutePreparation source) => new CommutePreparation
        {
            PlaceId = source.PlaceId, WorkStartsAt = source.WorkStartsAt, WorkEndsAt = source.WorkEndsAt,
            PreparedAt = source.PreparedAt, TravelMinutes = source.TravelMinutes
        };
        private static MealOuting CopyOuting(MealOuting source) => new MealOuting
        {
            OriginId = source.OriginId, OriginActivity = source.OriginActivity, ResumeUntil = source.ResumeUntil,
            ResumeEvent = source.ResumeEvent, Returning = source.Returning
        };
        private static CarState CopyCar(CarState source) => new CarState
        { Id = source.Id, ModelId = source.ModelId, PaintSeed = source.PaintSeed, ParkedPosition = source.ParkedPosition };

        // Objects other code may hold (the car, the preparation, an outing) keep their identity when they survive the event.
        internal static void CommitEvent(CitizenState live, CitizenState working)
        {
            live.OptionalStop = working.OptionalStop;
            live.OptionalStopChoice = working.OptionalStopChoice;
            live.Traits = working.Traits;
            live.HomeId = working.HomeId; live.DayPlaceId = working.DayPlaceId; live.ShopId = working.ShopId; live.LeisureId = working.LeisureId;
            live.Preferences.OwnsCar = working.Preferences.OwnsCar; live.Preferences.AllowTransit = working.Preferences.AllowTransit;
            live.Preferences.WalkingToleranceMetres = working.Preferences.WalkingToleranceMetres;
            live.Preferences.FareWeightMinutes = working.Preferences.FareWeightMinutes;
            var preparation = live.CommutePreparation; var prepared = working.CommutePreparation;
            preparation.PlaceId = prepared.PlaceId; preparation.WorkStartsAt = prepared.WorkStartsAt; preparation.WorkEndsAt = prepared.WorkEndsAt;
            preparation.PreparedAt = prepared.PreparedAt; preparation.TravelMinutes = prepared.TravelMinutes;
            if (working.MealOuting == null) live.MealOuting = null;
            else if (live.MealOuting == null) live.MealOuting = CopyOuting(working.MealOuting);
            else
            {
                var outing = live.MealOuting; var changed = working.MealOuting;
                outing.OriginId = changed.OriginId; outing.OriginActivity = changed.OriginActivity; outing.ResumeUntil = changed.ResumeUntil;
                outing.ResumeEvent = changed.ResumeEvent; outing.Returning = changed.Returning;
            }
            if (working.Car == null) live.Car = null;
            else if (live.Car == null) live.Car = CopyCar(working.Car);
            else
            {
                var car = live.Car; var changed = working.Car;
                car.Id = changed.Id; car.ModelId = changed.ModelId; car.PaintSeed = changed.PaintSeed; car.ParkedPosition = changed.ParkedPosition;
            }
            live.JoinedMinute = working.JoinedMinute; live.LastEmploymentCheckDay = working.LastEmploymentCheckDay; live.JobChanges = working.JobChanges;
            live.Phase = working.Phase; live.Activity = working.Activity; live.PlaceId = working.PlaceId; live.DestinationId = working.DestinationId;
            live.DestinationActivity = working.DestinationActivity; live.Journey = working.Journey; live.DepartureMinute = working.DepartureMinute;
            live.NextEventMinute = working.NextEventMinute; live.ActivityUntilMinute = working.ActivityUntilMinute;
            live.CompletedJourneys = working.CompletedJourneys; live.MissedActivities = working.MissedActivities;
            live.RecentVisits.Clear(); live.RecentVisits.AddRange(working.RecentVisits);
            live.RoutineKeys = working.RoutineKeys;
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
                    if (!person.Preferences.OwnsCar || person.Car == null || !OwnedCarId(person.Car.Id, person.Identity.Id)
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

        // Car.Id == Identity.Id + ":car" (a null ID concatenating as empty) without building the string for every car leg.
        private static bool OwnedCarId(string? car, string? resident)
        {
            resident ??= "";
            return car != null && car.Length == resident.Length + 4 && car.StartsWith(resident, StringComparison.Ordinal)
                && string.CompareOrdinal(car, resident.Length, ":car", 0, 4) == 0;
        }

        private static void CommitCarArrival(CitizenState person)
        {
            if (person.Journey == null) return;
            ValidateCarRoute(person.Journey, person);
            foreach (var leg in person.Journey.Legs)
                if (leg.Mode == TravelMode.Car) person.Car!.ParkedPosition = leg.To;
        }

        private RouteRequest CitizenRequest(Place from, Place to, double now, CitizenState person)
            => new RouteRequest(from, to, now, person, person.Preferences);

        // One citizen event. Every route question is yielded as a request and the event resumes with its
        // answer, so the side effects before and after each plan keep the order of the former synchronous Step.
        private IEnumerable<RouteRequest> StepEvent(CitizenState person, double now)
        {
            person.OptionalStopChoice = null;
            person.OptionalStop = null; // The original activity event owns the continuation.
            var outing = BeginMealOuting(person, now);
            if (outing == MealOutingStep.Handled) yield break;
            if (outing == MealOutingStep.NeedsReturnRoute)
            {
                var returnRequest = new RouteRequest(places[person.PlaceId], places[person.MealOuting!.OriginId], now, null,
                    food?.ReturnPreferences(person) ?? new TravelPreferences { OwnsCar = false, AllowTransit = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres });
                yield return returnRequest;
                FinishMealReturn(person, now, returnRequest.Result);
                yield break;
            }
            RoutineSlot? missedArrival = null;
            bool arrived = false;
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
                        yield break;
                    }
                    MissRoutine(person, new RoutineSlot(person.PlaceId, person.Activity, person.ActivityUntilMinute),
                        now, now >= person.ActivityUntilMinute ? "arrival-after-deadline" : "arrival-no-opening");
                    missedArrival = new RoutineSlot(person.PlaceId, person.Activity, person.ActivityUntilMinute);
                }
                person.Journey = null;
                person.DestinationId = "";
                person.DepartureMinute = 0;
                arrived = true;
            }
            // The daily employment check probes candidate workplaces with detached residents; each probe is one request.
            var candidates = ResidentLifecycle.BeginEmploymentCheck(State, person, now, places);
            if (candidates != null)
                foreach (var candidate in candidates)
                {
                    var probe = WorkplaceProbe(person, candidate);
                    yield return probe;
                    if (!ReachableWorkplace(probe)) continue;
                    ResidentLifecycle.AssignWorkplace(person, candidate);
                    break;
                }
            // Retain a home fallback until its original window ends. This also survives reload
            // and prevents counting the same closed activity again after the journey home.
            if (arrived && person.Activity == Activity.Home && now < person.ActivityUntilMinute)
            {
                person.NextEventMinute = HomeEvent(person, person.ActivityUntilMinute, now);
                yield break;
            }
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
                yield break;
            }
            // Waits own their original destination/window too. In particular, a
            // failed early commute must not become Home on its next pre-shift retry.
            bool committedWait = (person.Phase == CitizenPhase.WaitingForRoute || person.Phase == CitizenPhase.WaitingForOpening)
                && person.DestinationId != "" && now < person.ActivityUntilMinute;
            bool homeFallback = IsHomeRoutineFallback(person, now);
            var slot = committedWait
                ? new RoutineSlot(person.DestinationId, person.DestinationActivity, person.ActivityUntilMinute)
                : attending || homeFallback ? new RoutineSlot(person.HomeId, Activity.Home, person.ActivityUntilMinute)
                : DailyRoutine.At(person, State.Seed, now, State.Places, places.TryGetValue(person.HomeId, out var home) ? home : null);
            Route? commuteRoute = null;
            bool earlyCommute = false;
            double commuteAt = double.PositiveInfinity;
            if (!homeFallback)
            {
                var commute = DecideCommute(person, now, allowPlanning: true);
                if (commute.Plan != CommutePlanPoint.None)
                {
                    var request = CitizenRequest(places[person.HomeId], places[commute.Window.PlaceId], now, person);
                    yield return request;
                    commuteRoute = ValidatedCommute(request, person);
                    commute = FinishCommute(person, commute, commuteRoute, now);
                }
                commuteAt = commute.LeaveAt;
                if (commute.Prepared && now >= commuteAt)
                {
                    slot = new RoutineSlot(commute.Window.PlaceId, commute.Window.Activity, commute.Window.EndsAt);
                    earlyCommute = true;
                }
            }
            Route? optionalRoute = null;
            bool optionalSelected = false;
            if (!committedWait && !attending && !homeFallback && !earlyCommute
                && (slot.Activity == Activity.Shop || slot.Activity == Activity.Leisure))
            {
                var choice = new OptionalChoice(slot);
                foreach (var request in ChooseOptionalDestination(person, slot, now, choice)) yield return request;
                optionalSelected = choice.Selected; slot = choice.Slot; optionalRoute = choice.Route;
            }
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
                    yield break;
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
                yield break;
            }
            person.DestinationId = slot.PlaceId;
            person.DestinationActivity = slot.Activity;
            Route? route;
            if (optionalSelected) route = optionalRoute;
            else if (earlyCommute) route = commuteRoute;
            else
            {
                var request = CitizenRequest(places[person.PlaceId], destination, now, person);
                yield return request;
                route = request.Result;
            }
            if (route == null)
            {
                ObserveRoutine(person, slot, now, "route-rejected", "unreachable");
                person.Phase = CitizenPhase.WaitingForRoute;
                person.NextEventMinute = Math.Min(now + 30, slot.EndsAt);
                yield break;
            }
            ValidateRoute(route, places[person.PlaceId].Position, destination.Position);
            ValidateCarRoute(route, person);
            person.Phase = CitizenPhase.Travelling;
            person.Journey = route;
            person.DepartureMinute = now;
            person.NextEventMinute = now + route.Minutes;
        }

        private RouteRequest WorkplaceProbe(CitizenState person, Place destination)
        {
            var home = places[person.HomeId];
            // Native planners may allocate a home car. Feasibility checks use a detached
            // resident, preserving live car identity, parking and committed travel state.
            var probe = new CitizenState
            {
                Identity = Population.CloneIdentity(person.Identity), HomeId = person.HomeId,
                PlaceId = person.HomeId, DayPlaceId = destination.Id, Activity = Activity.Home,
                Phase = CitizenPhase.AtPlace,
                Preferences = new TravelPreferences { OwnsCar = person.Preferences.OwnsCar,
                    AllowTransit = person.Preferences.AllowTransit, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres,
                    FareWeightMinutes = person.Preferences.FareWeightMinutes },
                Car = person.Car == null ? null : new CarState { Id = person.Car.Id, ModelId = person.Car.ModelId,
                    PaintSeed = person.Car.PaintSeed, ParkedPosition = person.Car.ParkedPosition }
            };
            return new RouteRequest(home, destination, State.Minute, probe, probe.Preferences);
        }

        private static bool ReachableWorkplace(RouteRequest probe)
        {
            var route = probe.Result;
            if (route == null) return false;
            try { ValidateRoute(route, probe.From.Position, probe.To.Position); ValidateCarRoute(route, probe.Citizen!); }
            catch (InvalidOperationException) { return false; }
            return true;
        }

        private double AdmissionFrom(CitizenState person)
        {
            if (!(routes is ICommuteTimingPlanner) || (person.DestinationActivity != Activity.Work && person.DestinationActivity != Activity.Study)) return 0;
            var window = DailyRoutine.WorkOnDay(person, State.Seed, Math.Max(0, person.ActivityUntilMinute - 0.001));
            return window.HasValue && window.Value.EndsAt == person.ActivityUntilMinute ? window.Value.StartsAt : 0;
        }

        private enum CommutePlanPoint { None, MatchingPreparation, KnownDuration, FreshEstimate }

        private readonly struct CommuteDecision
        {
            public CommuteDecision(bool prepared, WorkWindow window, double leaveAt, CommutePlanPoint plan)
            { Prepared = prepared; Window = window; LeaveAt = leaveAt; Plan = plan; }
            public static readonly CommuteDecision None = new CommuteDecision(false, default, double.PositiveInfinity, CommutePlanPoint.None);
            public bool Prepared { get; }
            public WorkWindow Window { get; }
            public double LeaveAt { get; }
            public CommutePlanPoint Plan { get; }
        }

        // The former TryPrepareCommute up to its route search. A decision that names a plan point has done
        // every side effect that preceded the search; FinishCommute performs the ones that followed it.
        private CommuteDecision DecideCommute(CitizenState person, double now, bool allowPlanning)
        {
            if (!(routes is ICommuteTimingPlanner) || !ResidentLifecycle.IsSafelyHome(person)) return CommuteDecision.None;
            // A current shift is handled by the ordinary activity path, never by
            // planning tomorrow while today's worker still needs to leave home.
            var current = DailyRoutine.WorkOnDay(person, State.Seed, now);
            if (current.HasValue && now >= current.Value.StartsAt && now < current.Value.EndsAt) return CommuteDecision.None;
            var planned = DailyRoutine.NextWork(person, State.Seed, now);
            if (!planned.HasValue || planned.Value.PlaceId == person.HomeId) return CommuteDecision.None;
            var window = planned.Value;
            // A return-home fallback owns a missed activity's original deadline.
            // Replanning that same shift after an early closed-venue arrival would
            // send the resident out again and count the same miss twice.
            if (person.ActivityUntilMinute == window.EndsAt) return CommuteDecision.None;
            var preparation = person.CommutePreparation;
            bool matches = preparation.PlaceId == window.PlaceId && preparation.WorkStartsAt == window.StartsAt
                && preparation.WorkEndsAt == window.EndsAt;
            if (matches)
            {
                // A failed estimate is retried at shift start, not at every home
                // event overnight. A successful estimate schedules one departure.
                double leaveAt = preparation.TravelMinutes > 0
                    ? Math.Max(preparation.PreparedAt, window.StartsAt - preparation.TravelMinutes) : window.StartsAt;
                return new CommuteDecision(true, window, leaveAt,
                    now < leaveAt || !allowPlanning ? CommutePlanPoint.None : CommutePlanPoint.MatchingPreparation);
            }
            double previousMinutes = preparation.PlaceId == window.PlaceId ? preparation.TravelMinutes : 0;
            double prepareAt = DailyRoutine.CommutePreparationAt(person, State.Seed, window, previousMinutes);
            if (now < prepareAt) return new CommuteDecision(false, window, prepareAt, CommutePlanPoint.None);
            if (previousMinutes > 0)
            {
                // A known commute supplies tomorrow's departure estimate without
                // another route search. Changes are resolved honestly at departure.
                person.CommutePreparation = new CommutePreparation
                {
                    PlaceId = window.PlaceId, WorkStartsAt = window.StartsAt, WorkEndsAt = window.EndsAt,
                    PreparedAt = now, TravelMinutes = previousMinutes
                };
                double leaveAt = Math.Max(now, window.StartsAt - previousMinutes);
                return new CommuteDecision(true, window, leaveAt,
                    now < leaveAt || !allowPlanning ? CommutePlanPoint.None : CommutePlanPoint.KnownDuration);
            }
            // A home arrival only schedules work. Its next event will perform any
            // required search once, retaining the result when departure is due now.
            if (!allowPlanning) return new CommuteDecision(false, window, now, CommutePlanPoint.None);
            // Only a duration estimate is retained. Route geometry and winners are
            // freshly selected at departure so reopened/off-path roads can compete.
            return new CommuteDecision(true, window, double.PositiveInfinity, CommutePlanPoint.FreshEstimate);
        }

        private CommuteDecision FinishCommute(CitizenState person, CommuteDecision decision, Route? route, double now)
        {
            var window = decision.Window;
            switch (decision.Plan)
            {
                case CommutePlanPoint.MatchingPreparation:
                    if (route != null) { person.CommutePreparation.TravelMinutes = route.EstimatedMinutes; person.CommutePreparation.PreparedAt = now; }
                    return new CommuteDecision(true, window, decision.LeaveAt, CommutePlanPoint.None);
                case CommutePlanPoint.KnownDuration:
                    if (route != null) person.CommutePreparation.TravelMinutes = route.EstimatedMinutes;
                    return new CommuteDecision(true, window, decision.LeaveAt, CommutePlanPoint.None);
                case CommutePlanPoint.FreshEstimate:
                    double minutes = route?.EstimatedMinutes ?? 0;
                    person.CommutePreparation = new CommutePreparation
                    {
                        PlaceId = window.PlaceId, WorkStartsAt = window.StartsAt, WorkEndsAt = window.EndsAt,
                        PreparedAt = now, TravelMinutes = minutes
                    };
                    // A first unknown or changed route can already be too long. Leave now
                    // when necessary; never backdate a departure or invent punctuality.
                    return new CommuteDecision(true, window, minutes > 0 ? Math.Max(now, window.StartsAt - minutes) : window.StartsAt, CommutePlanPoint.None);
                default:
                    return decision;
            }
        }

        private static Route? ValidatedCommute(RouteRequest request, CitizenState person)
        {
            var route = request.Result;
            if (route != null) { ValidateRoute(route, request.From.Position, request.To.Position); ValidateCarRoute(route, person); }
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
            double leaveAt = DecideCommute(person, now, allowPlanning: false).LeaveAt;
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
