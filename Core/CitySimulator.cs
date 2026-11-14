using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PersistentCity.Tests")]

namespace PersistentCity.Core
{
    public readonly struct AdvanceResult
    {
        public AdvanceResult(int events, bool caughtUp, int trafficEvents = 0, bool planningPending = false, int foodContinuationEvents = 0, int researchUnits = 0)
        { Events = events; CaughtUp = caughtUp; TrafficEvents = trafficEvents; PlanningPending = planningPending; FoodContinuationEvents = foodContinuationEvents; ResearchUnits = researchUnits; }
        public int Events { get; }
        public int TrafficEvents { get; }
        public int FoodContinuationEvents { get; }
        public int ResearchUnits { get; }
        public int CitizenEvents => Events - TrafficEvents - FoodContinuationEvents - ResearchUnits;
        public bool CaughtUp { get; }
        public bool PlanningPending { get; }
    }

    public sealed partial class CitySimulator
    {
        private sealed class Due
        {
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
        private CityView? view;
        public ICityView View => view ??= new CityView(State);
        private FoodCommerce? food;
        public long HistoricalClockMisses => food?.HistoricalClockMisses ?? 0;
        public long FoodAnchorReads => food?.AnchorReads ?? 0;
        internal int QueuedPersonEvents => due.Count;
        internal int QueuedMealEvents => food?.QueuedMeals ?? 0;
        internal int DuePersonEventsAtOrBefore(double minute)
        {
            int count = pending != null && pending.Item.Minute <= minute && !due.Contains(pending.Item) ? 1 : 0;
            foreach (var entry in due) { if (entry.Minute > minute) break; count++; }
            return count;
        }
        internal int DueMealEventsAtOrBefore(double minute) => food?.DueMealsAtOrBefore(minute) ?? 0;
        internal CitizenState? FindCitizen(string id) => citizenEvents.TryGetValue(id, out var entry) ? entry.Citizen : null;
        internal Place FindPlace(string id) => places[id];
        internal IReadOnlyDictionary<string, Place> PlaceIndex => places;
        private readonly ActiveJourneyIndex activeJourneys;
        private readonly WorkplaceOccupancy occupancy = new WorkplaceOccupancy();
        internal bool OccupancyMatchesFullCount() => occupancy.Matches(State);
        public ActiveJourneyIndex ActiveJourneys => activeJourneys;
        public bool ActiveJourneyIndexMatchesState() => activeJourneys.MatchesScan(State.Citizens, State.VisitorRoutines);
        internal Position? FindPosition(string id) => places.TryGetValue(id,out var place) ? place.Position : (Position?)null;
        public ResidentInsight? Inspect(string residentId, Func<string, string>? productName = null, Func<string, int, string>? formatAddress = null,
            Func<string, string?>? shopName = null, Func<FoodScope, bool>? currentOwnShop = null)
        {
            if (residentId == null) return null;
            var person = FindCitizen(residentId);
            if (person == null || person.IsVisitor) return null;
            ResidentMeal? meal = food != null ? food.FindMeal(residentId) : State.Food?.Residents.Find(m => m.ResidentId == residentId);
            return ResidentInsight.Capture(View, person, meal, id => places.TryGetValue(id, out var place) ? place : null, productName, formatAddress, food?.MemorySettings, food == null ? null : food.MemoryReference, shopName, currentOwnShop,
                ResidentInsight.MemoryFood(State));
        }
        public void AttachFoodMarket(IFoodMarket market, FoodChoiceSettings? choiceSettings = null, FoodMemorySettings? memorySettings = null, FoodAwarenessSettings? awarenessSettings = null, IFoodAwarenessProvider? memory = null, IFoodAwarenessProvider? campaign = null, RivalFoodSettings? rivalSettings = null)
        {
            if (food != null) throw new InvalidOperationException("Food market already attached.");
            food = new FoodCommerce(this, market, routes, choiceSettings, memorySettings, awarenessSettings, memory, campaign, rivalSettings);
        }

        private ResearchSurveyRunner? research;
        public ResearchSurveyRunner? Research => research;
        public ResearchSurveyRunner AttachResearch(ResearchLedger ledger, int consumersPerUnit = 256, Func<string, string?>? businessPlace = null)
        {
            if (research != null) throw new InvalidOperationException("Research already attached.");
            return research = new ResearchSurveyRunner(this, ledger, consumersPerUnit, businessPlace);
        }

        public void InvalidateFoodAwareness() => food?.InvalidateAwareness();
        public FoodAwarenessDump? InspectFoodAwareness(string consumerId, int? day=null) => food?.InspectAwareness(consumerId,day);

        private sealed class ActivityObservers
        {
            public Action<string, CitizenState, double>? Handlers;
            public Action<FoodDecisionTrace>? FoodHandlers;
            public Action<RoutineDiagnostic>? RoutineHandlers;
        }
        private ActivityObservers activityObservers = new ActivityObservers();
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
        private void MissRoutine(CitizenState person, RoutineSlot slot, double now, string reason, PlanMissCause cause = PlanMissCause.None)
        {
            DayPlanExecutor.Mark(person, slot.Activity, slot.EndsAt, PlanItemState.Missed, cause != PlanMissCause.None ? cause : DayPlanExecutor.CauseOf(reason));
            if (slot.Activity == Activity.Work)
            {
                var w = DailyRoutine.WorkOnDay(person, State.Seed, Math.Max(0, slot.EndsAt - 480), places);
                if (w.HasValue && person.Accrual.Day == (int)(w.Value.StartsAt / 1440)
                    && (person.Accrual.Shift == EmploymentWages.Closed || person.Accrual.Shift == EmploymentWages.ClosureDay))
                { ObserveRoutine(person, slot, now, "closed", "workplace-closed"); return; }
                if (food?.TracksEmployment(person) == true)
                { ObserveRoutine(person, slot, now, "missed", reason); return; }
            }
            person.MissedActivities++;
            ObserveRoutine(person, slot, now, "missed", reason);
        }
        internal bool ObservingFood => activityObservers.FoodHandlers != null;
        internal void ObserveFood(FoodDecisionTrace trace) => activityObservers.FoodHandlers?.Invoke(trace);

        public CitySimulator RebuildRoutes(IRoutePlanner replacementRoutes)
        {
            AbortPending();
            var replacement = new CitySimulator(State, replacementRoutes);
            replacement.CopyVendorPlaces(this);
            replacement.activityObservers = activityObservers;
            replacement.workplaceAvailability = workplaceAvailability;
            replacement.ordinaryOwnership = ordinaryOwnership; replacement.ordinaryPrices = ordinaryPrices;
            replacement.ordinaryLoadedHour = ordinaryLoadedHour; replacement.ordinaryObservedHour = ordinaryObservedHour;
            replacement.logicalWorkplaceClosures = logicalWorkplaceClosures;
            replacement.venueFeedback = venueFeedback; replacement.throughTraffic = throughTraffic?.Fresh();
            replacement.HouseholdOutingsEnabled = HouseholdOutingsEnabled;
            replacement.weatherSource=weatherSource;replacement.weatherSettings=weatherSettings;
            replacement.fillCatalogue = fillCatalogue; replacement.fillPlan = fillPlan; replacement.MeasurementPersonTarget = MeasurementPersonTarget;
            if (fillPlan != null) fillPlan.Factory = null;
            if (stopEnvironment != null) replacement.AttachOptionalStops(stopEnvironment, stopPercentage, stopTiming);
            if (food != null) replacement.AttachFoodMarket(food.Market, food.ChoiceSettings, food.MemorySettings, food.AwarenessSettings, food.MemoryProvider, food.CampaignProvider, food.RivalSettings);
            if (research != null) replacement.AttachResearch(research.Ledger, research.ConsumersPerUnit, research.BusinessPlace);
            replacement.purchases = purchases; replacement.PurchaseFault = PurchaseFault;
            return replacement;
        }

        public CitySimulator(CityState state, IRoutePlanner routes)
        {
            activeJourneys = new ActiveJourneyIndex(FindPosition);
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (state.Capabilities == null) throw new InvalidOperationException("Missing population capabilities.");
            state.Capabilities.RequireExecutable();
            if (state.Schema != CityState.CurrentSchema || state.MenuPolicy != FoodNameIdentity.DynamicWire)
                throw new InvalidOperationException("Unsupported menu product policy; preserve the payload.");
            this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
            foreach (var place in state.Places)
            {
                if (VendorNames.IsVendorPlace(place.Id)) throw new ArgumentException("A city place uses the vendor place prefix.");
                places.Add(place.Id, place);
                if (place.Kind == PlaceKind.Shop || place.Kind == PlaceKind.Leisure) optionalPlaces.Add(place);
            }
            RestoreVendorPlaces();
            for (int citizenIndex = 0; citizenIndex < state.Citizens.Count; citizenIndex++)
            {
                var person = state.Citizens[citizenIndex];
                if (!Position.Finite(person.NextEventMinute) || person.NextEventMinute < state.Minute)
                    throw new ArgumentException("A citizen has an overdue or nonfinite event.");
                var item = new Due(citizenIndex) { Citizen = person, Minute = person.NextEventMinute };
                if (!due.Add(item)) throw new ArgumentException("Duplicate citizen event.");
                citizenEvents.Add(person.Identity.Id, item);
            }
            if (state.VisitorRoutines.Count != 0 || state.Visitors.Count != 0)
            {
                VisitorSchedules.LinkAll(state);
                for (int visitorIndex = 0; visitorIndex < state.VisitorRoutines.Count; visitorIndex++)
                {
                    var routine = state.VisitorRoutines[visitorIndex];
                    if (!Position.Finite(routine.NextEventMinute) || routine.NextEventMinute < state.Minute)
                        throw new ArgumentException("A visitor has an overdue or nonfinite event.");
                    if (!places.ContainsKey(routine.HomeId)) throw new ArgumentException("A visitor's portal is not a saved place.");
                    var item = new Due(~visitorIndex) { Citizen = routine, Minute = routine.NextEventMinute };
                    if (!due.Add(item)) throw new ArgumentException("Duplicate citizen event.");
                    citizenEvents.Add(routine.Identity.Id, item);
                }
            }
            activeJourneys.Rebuild(state.Citizens, state.VisitorRoutines);
            occupancy.Rebuild(state);
            RestoreTraffic();
            EnsureTrafficNetwork();
        }

        internal void ScheduleVisitors()
        {
            if (pending != null && pending.Item.ListIndex < 0) throw new InvalidOperationException("A visitor event is in flight.");
            foreach (var visitor in State.VisitorRoutines)
                if (citizenEvents.TryGetValue(visitor.Identity.Id, out var old)) { due.Remove(old); citizenEvents.Remove(visitor.Identity.Id); }
            VisitorSchedules.LinkAll(State);
            for (int visitorIndex = 0; visitorIndex < State.VisitorRoutines.Count; visitorIndex++)
            {
                var routine = State.VisitorRoutines[visitorIndex];
                if (!places.ContainsKey(routine.HomeId))
                {
                    var portal = State.Places.Find(p => p.Id == routine.HomeId) ?? throw new InvalidOperationException("A visitor's portal is not a saved place.");
                    places.Add(portal.Id, portal);
                }
                var item = new Due(~visitorIndex) { Citizen = routine, Minute = routine.NextEventMinute };
                if (!due.Add(item)) throw new InvalidOperationException("Duplicate citizen event.");
                citizenEvents.Add(routine.Identity.Id, item);
                activeJourneys.File(routine);
            }
            occupancy.Rebuild(State);
        }

        public AdvanceResult AdvanceTo(double targetMinute, int eventBudget = 10000)
            => AdvanceCore(targetMinute, eventBudget, true);

        public AdvanceResult AdvanceFrameSlice(double targetMinute, int eventBudget = 1)
            => AdvanceCore(targetMinute, eventBudget, false);

        public bool IsCaughtUpTo(double targetMinute) => Position.Finite(targetMinute)
            && State.Minute >= targetMinute && NoEventsThrough(targetMinute);

        public bool IsAccountingEpochDrained(double nativeMinute)
        {
            double target = AccountingClock.PreviousEpochTarget(nativeMinute);
            if (Math.Floor(nativeMinute) == 0) return true;
            return State.Minute >= target && NoEventsThrough(target, inspectSavedFood: true);
        }
        public bool BeginAccountingObservations(double nativeMinute)
        {
            if (!AccountingClock.Compatible(State.Minute, nativeMinute) || !IsAccountingEpochDrained(nativeMinute)) return false;
            double epoch = Math.Floor(nativeMinute);
            if (traffic != null && traffic.Clock < epoch) traffic.BeginObservationEpoch(epoch);
            if (State.Minute < epoch)
            {
                if (!State.Capabilities.Active && Math.Floor(State.Minute / 1440) < Math.Floor(epoch / 1440))
                    State.Capabilities.Active = true;
                State.Minute = epoch; SynchronizeSnapshot();
            }
            ordinaryOwnership?.ObserveBoundary(epoch);
            if (ordinaryOwnership != null && (long)(epoch / 60) > ordinaryLoadedHour) ordinaryObservedHour = (long)(epoch / 60);
            if (OrdinaryExchangesActive) OrdinaryExchanges.Prune(State.Economy, (int)(epoch / 1440));
            return true;
        }
        private bool SavedFoodAfter(double target)
        {
            foreach (var meal in State.Food!.Residents)
            {
                if (meal.Budget != null)
                {
                    var person = FindCitizen(meal.HostId);
                    if (person != null && EmploymentWages.NextPosting(person, State.Seed, State.Minute, out _, places) <= target) return false;
                }
                if (meal.DueAt <= target || meal.Decision != null && meal.Decision.OriginAt <= target
                    || meal.Budget != null && meal.Budget.NextPostingMinute <= target) return false;
            }
            return true;
        }
        private bool NoEventsThrough(double targetMinute, bool inspectSavedFood = false)
            => (ordinaryOwnership?.Pending != true || inspectSavedFood)
            && (food?.HasPendingCampaignPublication != true || inspectSavedFood)
            && (research == null || inspectSavedFood || !research.HoldsThrough(targetMinute))
            && (pending == null || inspectSavedFood && pending.Item.Minute > targetMinute)
            && (food?.HasPendingDecision != true || inspectSavedFood && food!.NextMinute > targetMinute)
            && (due.Count == 0 || due.Min!.Minute > targetMinute)
            && (traffic == null || traffic.NextEventMinute > targetMinute)
            && (State.Food == null || (food != null ? food.NextMinute > targetMinute : inspectSavedFood && SavedFoodAfter(targetMinute)))
            && (State.Capabilities.Active || (Math.Floor(State.Minute / 1440) + 1) * 1440 > targetMinute)
            && ThroughPeek() > targetMinute
            && !FillHolds(targetMinute);

        private AdvanceResult AdvanceCore(double targetMinute, int eventBudget, bool publishSnapshot)
        {
            if (!Position.Finite(targetMinute) || targetMinute < State.Minute || targetMinute > 1e9)
                throw new ArgumentOutOfRangeException(nameof(targetMinute), "Simulation time only advances; load a saved snapshot to go back.");
            if (eventBudget < 1) throw new ArgumentOutOfRangeException(nameof(eventBudget));
            if (State.Food != null && food == null) throw new InvalidOperationException("Saved food economy requires its market before advancing.");
            EnsureTrafficNetwork();
            int count = 0, trafficCount = 0, foodContinuationCount = 0, researchCount = 0;
            if (pending != null)
            {
                if (!publishSnapshot) return new AdvanceResult(0, false, 0, planningPending: true);
                bool completed = false;
                while (pending != null) completed = ContinuePlanning(double.PositiveInfinity);
                if (completed) count++;
            }
            while (count < eventBudget)
            {
                if (ordinaryOwnership?.Pending == true)
                { ordinaryOwnership.Continue(); count++; continue; }
                if (food?.HasPendingCampaignPublication == true)
                { food.ContinueCampaignPublication(); count++; foodContinuationCount++; continue; }
                double citizenMinute = due.Count == 0 ? double.PositiveInfinity : due.Min!.Minute;
                double trafficMinute = traffic?.NextEventMinute ?? double.PositiveInfinity;
                double foodMinute = food?.NextMinute ?? double.PositiveInfinity;
                double activation = State.Capabilities.Active ? double.PositiveInfinity : (Math.Floor(State.Minute / 1440) + 1) * 1440;
                double next = Math.Min(activation, Math.Min(foodMinute, Math.Min(citizenMinute, trafficMinute)));
                double through = throughTraffic == null ? double.PositiveInfinity : ThroughMinute();
                double work = Math.Min(next, through);
                if (research != null && research.ReadyBefore(!FillActive ? work : FillUnitDue() ? State.Minute : Math.Min(work, NextDayBoundary), targetMinute))
                { research.Continue(); count++; researchCount++; continue; }
                if (FillUnitDue()) { RunFillUnit(); count++; continue; }
                if (FillActive && NextDayBoundary <= work && NextDayBoundary <= targetMinute)
                {
                    double boundary = NextDayBoundary;
                    if (traffic != null && traffic.Clock < boundary) traffic.BeginObservationEpoch(boundary);
                    State.Minute = boundary;
                    if (!State.Capabilities.Active) { State.Capabilities.Active = true; count++; }
                    continue;
                }
                if (through < next && through <= targetMinute && (!FillActive || through < NextDayBoundary))
                {
                    State.Minute = Math.Max(State.Minute, through);
                    traffic!.AdvanceTo(State.Minute, 0);
                    RunThroughTraffic(); count++; trafficCount++;
                    continue;
                }
                if (next > targetMinute) break;
                State.Minute = next;
                if (next == activation) { State.Capabilities.Active = true; count++; continue; }
                if(food?.NeedsAwarenessPreparation(next)==true)
                {
                    food.ContinueAwarenessPreparation();count++;foodContinuationCount++;continue;
                }
                bool postingFirst = food?.PostingDueAt(next) == true
                    && foodMinute <= citizenMinute && foodMinute <= trafficMinute;
                if (food?.HasPendingDecision == true || postingFirst || foodMinute < citizenMinute && foodMinute < trafficMinute)
                {
                    traffic?.AdvanceTo(next, 0);
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
                if (!RunEvent(item, inline: publishSnapshot))
                    return new AdvanceResult(count, false, trafficCount, planningPending: true, foodContinuationEvents: foodContinuationCount, researchUnits: researchCount);
                count++;
            }
            bool caughtUp = NoEventsThrough(targetMinute);
            if (caughtUp) { traffic?.AdvanceTo(targetMinute, 0); State.Minute = targetMinute; }
            if (publishSnapshot) SynchronizeSnapshot();
            return new AdvanceResult(count, caughtUp, trafficCount, foodContinuationEvents: foodContinuationCount, researchUnits: researchCount);
        }

        private sealed class PendingEvent
        {
            public PendingEvent(Due item, CitizenState working, IEnumerator<RouteRequest> steps)
            { Item = item; Working = working; Steps = steps; }
            public readonly Due Item;
            public readonly CitizenState Working;
            public readonly IEnumerator<RouteRequest> Steps;
            public RouteRequest? Request;
            public IRoutePlanningJob? Job;
            public bool InputLease;
        }
        private PendingEvent? pending;
        public bool PlanningPending => pending != null;
        public long PlanningRequests { get; private set; }
        private bool InProgress(Due item) => pending != null && ReferenceEquals(pending.Item, item);
        public long EventRestarts { get; private set; }
        public long InlineAfterRestarts => 0;
        // A changed version discards one attempt and requeues the original event. Every replay uses
        // the same finite frame admission as its first attempt; no number of restarts grants an
        // unlimited call. The held clock is intentional: later events can change route forecasts,
        // employment and meals. Reordering them would make a frame cadence change city outcomes.

        private bool RunEvent(Due item, bool inline)
        {
            var working = CloneForEvent(item.Citizen);
            var evt = new PendingEvent(item, working, StepEvent(working, item.Minute).GetEnumerator());
            try
            {
                if (routes is IRouteEventInputs inputs) { inputs.BeginRouteEvent(); evt.InputLease = true; }
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
            catch { ReleaseEventInputs(evt); evt.Steps.Dispose(); due.Add(item); throw; }
            CompleteEvent(evt);
            return true;
        }

        private Route? AnswerSynchronously(RouteRequest request)
            => request.Citizen != null && routes is ICitizenRoutePlanner citizenRoutes
                ? citizenRoutes.Plan(request.From, request.To, request.DepartureMinute, request.Citizen)
                : routes.Plan(request.From, request.To, request.DepartureMinute, request.Preferences);

        public bool ContinuePlanning(double budgetMilliseconds)
        {
            if (double.IsNaN(budgetMilliseconds) || budgetMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            if (food?.HasPendingCampaignPublication == true) { food.ContinueCampaignPublication(); return false; }
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
                ReleaseEventInputs(evt);
                evt.Steps.Dispose();
                if (!due.Add(evt.Item)) throw new InvalidOperationException("An interrupted event lost its queue slot.");
            }
        }

        private void CompleteEvent(PendingEvent evt)
        {
            ReleaseEventInputs(evt);
            var item = evt.Item; var working = evt.Working; var live = item.Citizen;
            evt.Steps.Dispose();
            if (!Position.Finite(working.NextEventMinute) || working.NextEventMinute <= State.Minute)
            {
                if (!due.Add(item)) throw new InvalidOperationException("An invalid event lost its queue slot.");
                throw new InvalidOperationException("Each citizen event must schedule a later event.");
            }
            var arrivalRoute = live.Phase == CitizenPhase.Travelling ? live.Journey : null;
            bool sameVisit = live.Phase == CitizenPhase.AtPlace && working.Phase == CitizenPhase.AtPlace
                && live.PlaceId == working.PlaceId && live.Activity == working.Activity
                && live.RecentVisits.Count > 0 && working.RecentVisits.Count > 0
                && live.RecentVisits[live.RecentVisits.Count - 1].AtMinute == working.RecentVisits[working.RecentVisits.Count - 1].AtMinute
                && live.RecentVisits[live.RecentVisits.Count - 1].PlaceId == working.RecentVisits[working.RecentVisits.Count - 1].PlaceId;
            if (sameVisit)
            {
                working.OptionalStopChoice = live.OptionalStopChoice;
                working.OptionalStop = live.OptionalStop?.Copy();
            }
            else if (live.OptionalStopChoice == true && live.OptionalStop == null) working.OptionalStopChoice = true;
            occupancy.Move(live.DayPlaceId, working.DayPlaceId);
            food?.AccrueWorkDeparture(live, working);
            var needVisit = NewNeedVisit(live, working);
            bool departed = purchases != null && working.Phase == CitizenPhase.Travelling && working.Journey != null && !ReferenceEquals(working.Journey, live.Journey);
            DeferMissedNeeds(live, working);
            RecordVisitFeedback(live, working);
            CommitOrdinaryArrival(live, working);
            CommitEvent(live, working);
            if (needVisit != null) CommitNeedPurchase(live, needVisit);
            if (departed) ImpulseOnDeparture(live);
            food?.EmploymentChanged(live);
            if (sameVisit && live.OptionalStop != null) CancelOptionalStop(live.Identity.Id);
            if (live.Visitor != null) VisitorSchedules.Sync(live.Visitor);
            item.Minute = ScheduledMinute(live);
            if (!due.Add(item)) throw new InvalidOperationException("A completed event was already queued.");
            food?.ObserveStop(live);
            food?.SyncPlanMeals(live);
            ScheduleOptionalStop(live, arrivalRoute);
            FileOptionalStop(live);
            activityObservers.Handlers?.Invoke(live.Identity.Id, live, State.Minute);
        }

        private void ReleaseEventInputs(PendingEvent evt)
        {
            if (!evt.InputLease) return;
            evt.InputLease = false;
            ((IRouteEventInputs)routes).EndRouteEvent();
        }

        internal static CitizenState CloneForEvent(CitizenState live) => new CitizenState
        {
            Identity = live.Identity, HomeId = live.HomeId, DayPlaceId = live.DayPlaceId, ShopId = live.ShopId, LeisureId = live.LeisureId,
            Preferences = new TravelPreferences { OwnsCar = live.Preferences.OwnsCar, AllowTransit = live.Preferences.AllowTransit,
                WalkingToleranceMetres = live.Preferences.WalkingToleranceMetres, FareWeightMinutes = live.Preferences.FareWeightMinutes },
            CommutePreparation = CopyPreparation(live.CommutePreparation),
            MealOuting = live.MealOuting == null ? null : CopyOuting(live.MealOuting),
            OptionalStop = live.OptionalStop?.Copy(),
            W = live.W,
            OptionalStopChoice = live.OptionalStopChoice,
            Car = live.Car == null ? null : CopyCar(live.Car),
            JoinedMinute = live.JoinedMinute, LastEmploymentCheckDay = live.LastEmploymentCheckDay, JobChanges = live.JobChanges,
            EmploymentStartDay = live.EmploymentStartDay, Accrual = live.Accrual, Wallet = live.Wallet, Plan = live.Plan?.Copy(), Needs = live.Needs?.Copy(),
            Phase = live.Phase, Activity = live.Activity, PlaceId = live.PlaceId, DestinationId = live.DestinationId,
            DestinationActivity = live.DestinationActivity, Journey = live.Journey, DepartureMinute = live.DepartureMinute,
            NextEventMinute = live.NextEventMinute, ActivityUntilMinute = live.ActivityUntilMinute,
            CompletedJourneys = live.CompletedJourneys, MissedActivities = live.MissedActivities,
            PlayerHoldMinutes = live.PlayerHoldMinutes,
            RecentVisits = new List<VisitRecord>(live.RecentVisits),
            Traits = live.Traits,
            RoutineKeys = live.RoutineKeys,
            Visitor = live.Visitor, VisitorCarried = live.VisitorCarried
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

        internal static void CommitEvent(CitizenState live, CitizenState working)
        {
            live.OptionalStop = working.OptionalStop;
            live.W = working.W;
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
            live.EmploymentStartDay = working.EmploymentStartDay; live.Accrual = working.Accrual;
            live.Wallet = working.Wallet;
            live.Plan = working.Plan;
            live.Needs = working.Needs;
            live.Phase = working.Phase; live.Activity = working.Activity; live.PlaceId = working.PlaceId; live.DestinationId = working.DestinationId;
            live.PlayerHoldMinutes = working.Phase == CitizenPhase.Travelling && ReferenceEquals(working.Journey, live.Journey) ? working.PlayerHoldMinutes : 0;
            live.DestinationActivity = working.DestinationActivity; live.Journey = working.Journey; live.DepartureMinute = working.DepartureMinute;
            live.NextEventMinute = working.NextEventMinute; live.ActivityUntilMinute = working.ActivityUntilMinute;
            live.CompletedJourneys = working.CompletedJourneys; live.MissedActivities = working.MissedActivities;
            live.RecentVisits.Clear(); live.RecentVisits.AddRange(working.RecentVisits);
            live.RoutineKeys = working.RoutineKeys;
            live.VisitorCarried = working.VisitorCarried;
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

        private IEnumerable<RouteRequest> StepEvent(CitizenState person, double now)
        {
            var planBefore = person.Plan;
            var needPlanning = NeedPlanningNow();
            DayPlanExecutor.Ensure(person, State.Seed, now, State.Places, places, State.Food != null, needPlanning, HouseholdLookup);
            if (needPlanning != null && !ReferenceEquals(planBefore, person.Plan)) WakeForNewPlan(person, now);
            DayPlanExecutor.Settle(person, now);
            person.OptionalStopChoice = null;
            person.OptionalStop = null;
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
            bool paidRefusedArrival = false;
            bool arrived = false;
            if ((person.Phase == CitizenPhase.WaitingForRoute || person.Phase == CitizenPhase.WaitingForOpening)
                && person.DestinationActivity != Activity.Home && now >= person.ActivityUntilMinute)
                MissRoutine(person, new RoutineSlot(person.DestinationId, person.DestinationActivity, person.ActivityUntilMinute),
                    now, person.Phase == CitizenPhase.WaitingForRoute ? "route-wait-expired" : "opening-wait-expired");
            if (person.Phase == CitizenPhase.Travelling)
            {
                CommitCarArrival(person);
                person.PlaceId = person.DestinationId;
                person.Activity = person.DestinationActivity;
                person.Phase = CitizenPhase.AtPlace;
                person.CompletedJourneys++;
                double admissionFrom = AdmissionFrom(person);
                bool closedWorkShift = person.DestinationActivity == Activity.Work && person.Accrual.Shift == EmploymentWages.Closed;
                bool admitted = person.DestinationActivity == Activity.Home
                    || (!closedWorkShift && now >= admissionFrom && now < person.ActivityUntilMinute && DailyRoutine.Open(places[person.PlaceId], now));
                bool affordable = !admitted || CanAdmitOrdinary(person, person.PlaceId, person.Activity, now);
                admitted &= affordable; paidRefusedArrival = !affordable;
                if (admitted)
                {
                    VisitorSchedules.RecordArrival(person, now, person.PlaceId, person.Activity);
                    DayPlanExecutor.Mark(person, person.Activity, person.ActivityUntilMinute, PlanItemState.Active, placeId: person.PlaceId);
                }
                else
                {
                    double? opening = affordable && !closedWorkShift && now < person.ActivityUntilMinute
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
            var candidates = ResidentLifecycle.BeginEmploymentCheck(State, person, now, places, occupancy);
            if (candidates != null)
                foreach (var candidate in candidates)
                {
                    var probe = WorkplaceProbe(person, candidate);
                    yield return probe;
                    if (!ReachableWorkplace(probe)) continue;
                    ResidentLifecycle.AssignWorkplace(person, candidate);
                    break;
                }
            DayPlanExecutor.Ensure(person, State.Seed, now, State.Places, places, State.Food != null, NeedPlanningNow(), HouseholdLookup);
            if (arrived && person.Activity == Activity.Home && now < person.ActivityUntilMinute)
            {
                person.NextEventMinute = HomeEvent(person, person.ActivityUntilMinute, now);
                yield break;
            }
            if (person.CommutePreparation.PlaceId != person.DayPlaceId)
                person.CommutePreparation = new CommutePreparation();
            bool attending = !paidRefusedArrival && person.Phase == CitizenPhase.AtPlace && person.Activity != Activity.Home
                && !(person.Activity == Activity.Work && person.Accrual.Shift == EmploymentWages.Closed)
                && now < person.ActivityUntilMinute;
            double? currentClose = attending ? DailyRoutine.NextClose(places[person.PlaceId], now) : null;
            if (attending && currentClose.HasValue)
            {
                person.NextEventMinute = Math.Min(person.ActivityUntilMinute, currentClose.Value);
                yield break;
            }
            bool committedWait = (person.Phase == CitizenPhase.WaitingForRoute || person.Phase == CitizenPhase.WaitingForOpening)
                && person.DestinationId != "" && now < person.ActivityUntilMinute
                && !(person.DestinationActivity == Activity.Work && person.Accrual.Shift == EmploymentWages.Closed);
            bool homeFallback = IsHomeRoutineFallback(person, now);
            var slot = committedWait
                ? new RoutineSlot(person.DestinationId, person.DestinationActivity, person.ActivityUntilMinute)
                : attending || homeFallback ? new RoutineSlot(person.HomeId, Activity.Home, person.ActivityUntilMinute)
                : DailyRoutine.At(person, State.Seed, now, State.Places, places.TryGetValue(person.HomeId, out var home) ? home : null, places);
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
            if (slot.Activity == Activity.Work)
            {
                var work = DailyRoutine.WorkOnDay(person, State.Seed, Math.Max(0, slot.EndsAt - 480), places);
                if (work.HasValue && now >= work.Value.StartsAt && person.Accrual.Day == (int)(work.Value.StartsAt / 1440)
                    && person.Accrual.Shift == EmploymentWages.Closed)
                {
                    DayPlanExecutor.Mark(person, slot.Activity, slot.EndsAt, PlanItemState.Missed, PlanMissCause.Closed);
                    ObserveRoutine(person, slot, now, "closed", "workplace-closed");
                    slot = new RoutineSlot(person.HomeId, Activity.Home, slot.EndsAt);
                    earlyCommute = false; commuteAt = double.PositiveInfinity;
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
            bool recordArrival = RecordsSamePlaceArrival(person, slot);
            if (recordArrival && !CanAdmitOrdinary(person, slot.PlaceId, slot.Activity, now))
            {
                MissRoutine(person, slot, now, "paid-visit-unaffordable");
                slot = new RoutineSlot(person.HomeId, Activity.Home, slot.EndsAt); destination = places[person.HomeId];
                recordArrival = false;
            }
            person.ActivityUntilMinute = slot.EndsAt;
            if (slot.PlaceId == person.PlaceId)
            {
                if (recordArrival)
                {
                    VisitorSchedules.RecordArrival(person, now, slot.PlaceId, slot.Activity);
                    DayPlanExecutor.Mark(person, slot.Activity, slot.EndsAt, PlanItemState.Active, placeId: slot.PlaceId);
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
            person.Journey = route.Commit();
            person.DepartureMinute = now;
            person.NextEventMinute = now + route.Minutes;
            DayPlanExecutor.Mark(person, slot.Activity, slot.EndsAt, PlanItemState.Travelling, placeId: slot.PlaceId);
        }

        private RouteRequest WorkplaceProbe(CitizenState person, Place destination)
        {
            var home = places[person.HomeId];
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
            var window = DailyRoutine.WorkOnDay(person, State.Seed, Math.Max(0, person.ActivityUntilMinute - 480), places);
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

        private CommuteDecision DecideCommute(CitizenState person, double now, bool allowPlanning)
        {
            if (!(routes is ICommuteTimingPlanner) || !ResidentLifecycle.IsSafelyHome(person)) return CommuteDecision.None;
            var current = DailyRoutine.WorkOnDay(person, State.Seed, now, places);
            if (current.HasValue && now >= current.Value.StartsAt && now < current.Value.EndsAt) return CommuteDecision.None;
            var planned = DailyRoutine.NextWork(person, State.Seed, now, places);
            if (!planned.HasValue || planned.Value.PlaceId == person.HomeId) return CommuteDecision.None;
            var window = planned.Value;
            if (person.ActivityUntilMinute == window.EndsAt) return CommuteDecision.None;
            var preparation = person.CommutePreparation;
            bool matches = preparation.PlaceId == window.PlaceId && preparation.WorkStartsAt == window.StartsAt
                && preparation.WorkEndsAt == window.EndsAt;
            if (matches)
            {
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
                person.CommutePreparation = new CommutePreparation
                {
                    PlaceId = window.PlaceId, WorkStartsAt = window.StartsAt, WorkEndsAt = window.EndsAt,
                    PreparedAt = now, TravelMinutes = previousMinutes
                };
                double leaveAt = Math.Max(now, window.StartsAt - previousMinutes);
                return new CommuteDecision(true, window, leaveAt,
                    now < leaveAt || !allowPlanning ? CommutePlanPoint.None : CommutePlanPoint.KnownDuration);
            }
            if (!allowPlanning) return new CommuteDecision(false, window, now, CommutePlanPoint.None);
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
            var original = DailyRoutine.At(person, State.Seed, Math.Max(0, person.ActivityUntilMinute - 0.001), places);
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

        private void WaitForOpening(CitizenState person, RoutineSlot slot, double opening)
        {
            person.Phase = CitizenPhase.WaitingForOpening;
            person.DestinationId = slot.PlaceId;
            person.DestinationActivity = slot.Activity;
            person.ActivityUntilMinute = slot.EndsAt;
            person.NextEventMinute = opening;
            if (person.PlaceId == slot.PlaceId && (slot.Activity == Activity.Shop || slot.Activity == Activity.Leisure))
                CaptureQueueWeather(person, State.Minute);
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
                    || Position.Distance(previous, leg.From) > 0.1 || (uint)leg.Mode > (uint)TravelMode.Car)
                    throw new InvalidOperationException("A route is nonfinite, discontinuous or has invalid time/mode.");
                previous = leg.To;
            }
            if (Position.Distance(previous, to) > 0.1 || !Position.Finite(route.Minutes)) throw new InvalidOperationException("A route does not end at its destination.");
        }
    }
}
