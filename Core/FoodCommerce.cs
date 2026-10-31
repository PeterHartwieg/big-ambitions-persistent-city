using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>Opt-in diagnostic of one decision. Not retained by the simulation or saved.
    /// Subscribers must copy anything they retain and must not change these decision inputs.</summary>
    public sealed class FoodDecisionTrace
    {
        public FoodConsumer Consumer { get; set; } = null!;
        public FoodIntent Intent { get; set; } = null!;
        public IReadOnlyList<FoodBusiness> Businesses { get; set; } = Array.Empty<FoodBusiness>();
        public IReadOnlyList<FoodTravelQuote> Quotes { get; set; } = Array.Empty<FoodTravelQuote>();
        public Dictionary<string, string> RouteChecks { get; set; } = new Dictionary<string, string>();
        public MealKind MealKind { get; set; }
        public IEnumerable<object> ChoiceCosts => Intent.Choices.Select(c => new {
            c.BusinessId, c.ProductId, c.Price, c.Reason, c.ScoreCents,
            FitCents = MealRhythm.ProductPenalty(MealKind, c.ProductId),
            c.ServiceQualityAdjustmentCents, c.DistrictAppealAdjustmentCents,
            PreferenceCents = c.ProductId == Consumer.PreferredProductId ? -Consumer.PreferenceBonusCents : 0,
            TimeCents = c.ScoreCents - c.Price.ApproximateCents() - c.ServiceQualityAdjustmentCents - c.DistrictAppealAdjustmentCents
                - MealRhythm.ProductPenalty(MealKind, c.ProductId)
                + (c.ProductId == Consumer.PreferredProductId ? Consumer.PreferenceBonusCents : 0),
            HomeFoodMarginCents = Intent.OutsideOptionScoreCents - c.ScoreCents
        });
        public int RouteCalls { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public FoodDecisionState? Decision { get; set; }
        /// <summary>Shortlist/hash/traits/sampling work only, excluding supply capture, feasibility and routes.
        /// Null if this diagnostic did not observe the initial capture (for example after reload).</summary>
        public double? ChoiceMilliseconds { get; set; }
    }
    public enum MealStatus { Pending, Travelling, Serving, Paid, Declined, Missed, Uncertain }
    public sealed class MealOuting
    {
        public string OriginId { get; set; } = "";
        public Activity OriginActivity { get; set; }
        public double ResumeUntil { get; set; }
        public double ResumeEvent { get; set; }
        public bool Returning { get; set; }
    }
    public sealed class FoodScope
    {
        public string PlaceId { get; set; } = "";
        // Keep the original product identity for existing scopes and pending receipts.
        public string ProductId { get; set; } = "";
        public List<string> AdditionalProductIds { get; set; } = new List<string>();
        public bool ContainsProduct(string id) => ProductId == id || AdditionalProductIds.Contains(id);
        public IEnumerable<string> Products() => new[] { ProductId }.Concat(AdditionalProductIds);
        public string BusinessId { get; set; } = "";
        public double NextServiceAt { get; set; }
    }
    public sealed class ResidentMeal
    {
        public string ResidentId { get; set; } = "";
        public string AnchorId { get; set; } = "";
        public int Slot { get; set; }
        public string PreferredProductId { get; set; } = "";
        public int PreferenceBonusCents { get; set; }
        public bool NeedsHomeResync { get; set; }
        public MealExcursion? Excursion { get; set; }
        public int Day { get; set; }
        public MealKind Kind { get; set; }
        public Money DayAllowanceUsed { get; set; }
        public double DesiredAt { get; set; }
        public double ExpiresAt { get; set; }
        public double DueAt { get; set; }
        public MealStatus Status { get; set; }
        public string PlaceId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string Reason { get; set; } = "";
        public Money Allowance { get; set; }
        public Money Price { get; set; }
        public double ServiceMinutes { get; set; }
        public double ServiceAt { get; set; }
        public double Deadline { get; set; }
        public double ReturnMinutes { get; set; }
        public ResidentBudget? Budget { get; set; }
        public bool ExistingStop { get; set; }
        public long ExistingStopPurchases { get; set; }
        public long Purchases { get; set; }
        public Money Spent { get; set; }
        /// <summary>Traits of a represented consumer (slot above zero), keyed by its stable consumer ID.
        /// Null for slot zero, whose traits are its anchor resident's.</summary>
        public ResidentTraits? RepresentedTraits { get; set; }
        /// <summary>Current-day outcomes; null until this consumer's first recorded outcome.</summary>
        public ResidentFoodToday? Today { get; set; }
        /// <summary>The version-1 policy's in-progress decision for this meal; null under the legacy choice.</summary>
        public FoodDecisionState? Decision { get; set; }
        /// <summary>Retry latch for the current meal: the last processed decision context and how many distinct
        /// contexts this meal has used (at most FoodChoicePolicy.MaximumContextsPerMeal). Same-context polling reuses it.</summary>
        public string LastContextKey { get; set; } = "";
        public int ContextAttempts { get; set; }
        public string IntentId => MealRhythm.IntentId(ResidentId, Day, Kind);
    }
    public sealed class FoodCommerceState
    {
        public int RepresentationFactor { get; set; } = 1;
        public List<FoodRoutineStop> RoutineStops { get; set; } = new List<FoodRoutineStop>();
        public List<FoodShopDay> History { get; set; } = new List<FoodShopDay>();
        public int HouseholdFinanceVersion { get; set; }
        public int MealRhythmVersion { get; set; }
        public List<FoodScope> Shops { get; set; } = new List<FoodScope>();
        public List<ResidentMeal> Residents { get; set; } = new List<ResidentMeal>();
        public List<FoodDistrictBudgetMix> Districts { get; set; } = new List<FoodDistrictBudgetMix>();
        /// <summary>Food choice policy header. New scopes and migrated cities activate on the next in-game day.
        /// No runtime setting changes either value; committed legacy meals retain their own terms.</summary>
        public int ChoicePolicyVersion { get; set; } = FoodChoicePolicy.Version;
        public int ChoiceStartsDay { get; set; }
    }
    public readonly struct FoodSale
    {
        public FoodSale(bool paid, string reason) { Paid = paid; Reason = reason; }
        public bool Paid { get; }
        public string Reason { get; }
    }
    /// <summary>One main-thread sale call must finish before saving the native world plus CityState.
    /// Exceptions leave a persisted Uncertain decision which is never retried automatically.</summary>
    public interface IFoodMarket
    {
        double EarliestMinute { get; }
        IReadOnlyList<FoodBusiness> Read(double minute);
        FoodSale Buy(ResidentMeal meal, double minute);
    }
    /// <summary>Bound expensive native supply reads before materializing offers. Discovery
    /// and business authority remain separate from this local decision shortlist.</summary>
    public interface ILocalFoodMarket
    {
        IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double walkingToleranceMetres, int maximumCandidates);
        FoodBusiness? ReadBusiness(string businessId, double minute);
    }
    /// <summary>Metadata only; the scope catalogue and cached entrances are ranked before any supply read.</summary>
    public interface IFoodCandidateMarket
    {
        bool IsCandidate(FoodScope scope);
        FoodBusiness? CaptureBusiness(string businessId, double minute);
    }
    public interface IFoodBusinessAuthority
    {
        bool CanServe(string businessId);
    }
    /// <summary>Read-only proof of the original checkout. Absence is never proof of refusal.</summary>
    public interface IFoodSaleEvidence
    {
        bool IsConfirmedPaid(ResidentMeal meal);
    }

    /// <summary>Sequential meal opportunities sharing one resident wallet. No nutrition or
    /// closed-city payroll claim. Events share the population clock and its bounded frame budget.</summary>
    public sealed partial class FoodCommerce
    {
        private sealed class Order : IComparer<ResidentMeal>
        {
            public int Compare(ResidentMeal? a, ResidentMeal? b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1; if (b == null) return 1;
                int time = a.DueAt.CompareTo(b.DueAt);
                return time != 0 ? time : StringComparer.Ordinal.Compare(a.ResidentId, b.ResidentId);
            }
        }
        private readonly CitySimulator city;
        private readonly IFoodMarket market;
        private readonly IRoutePlanner routes;
        private readonly FoodCommerceState state;
        private readonly Dictionary<string, ResidentMeal> records;
        private readonly SortedSet<ResidentMeal> due = new SortedSet<ResidentMeal>(new Order());
        private readonly Dictionary<string, int> reservations = new Dictionary<string, int>(StringComparer.Ordinal);
        public double NextMinute => pendingDecision?.Decision?.OriginAt
            ?? Math.Min(NextBudgetMinute, due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt);
        internal IFoodMarket Market => market;
        internal ResidentMeal? FindMeal(string consumerId) => records.TryGetValue(consumerId, out var meal) ? meal : null;
        internal FoodCommerce(CitySimulator city, IFoodMarket market, IRoutePlanner routes, FoodChoiceSettings? choiceSettings = null)
        {
            this.city = city; this.market = market; this.routes = routes;
            ChoiceSettings = choiceSettings ?? new FoodChoiceSettings();
            ChoiceSettings.Clamp();
            state = city.State.Food ?? throw new InvalidOperationException("No food state.");
            if(state.Residents.Count==0) state.ChoiceStartsDay=(int)Math.Floor(city.State.Minute/1440)+1;
            records = state.Residents.ToDictionary(r => r.ResidentId, StringComparer.Ordinal);
            InitializeRepresentation();
            foreach (var entry in state.Residents)
            {
                if (entry.DueAt < city.State.Minute) throw new InvalidOperationException("Overdue meal record.");
                due.Add(entry);
                if (entry.Budget != null) budgetDue.Add(entry);
                if (Active(entry)) Reserve(entry, 1);
                if (entry.Decision != null) pendingDecision = entry;
            }
        }
        private static bool Active(ResidentMeal m) => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving;
        private static string ReservationKey(string shop, string product) => shop + "\n" + product;
        private void Reserve(ResidentMeal meal, int delta)
        {
            string key = ReservationKey(meal.PlaceId, meal.ProductId);
            reservations.TryGetValue(key, out int count); reservations[key] = count + delta;
        }
        private void ApplyReservations(FoodBusiness business)
        {
            foreach (var offer in business.Offers)
            {
                reservations.TryGetValue(ReservationKey(business.PlaceId, offer.ProductId), out int count);
                if (offer.Units.HasValue) offer.Units = Math.Max(0, offer.Units.Value - count);
            }
        }
        private ResidentMeal Enrol(CitizenState person, int slot = 0)
        {
            string id = FoodRepresentation.ConsumerId(person.Identity.Id, slot);
            if (records.TryGetValue(id, out var existing)) return existing;
            var resident = Resident(person);
            var meal = new ResidentMeal { ResidentId = id, AnchorId = person.Identity.Id, Slot = slot };
            if (slot > 0)
            {
                meal.RepresentedTraits = ResidentTraits.Generate(city.State.Seed, id);
                var products = FoodProducts.Ids.OrderBy(p => p, StringComparer.Ordinal).ToArray();
                meal.PreferredProductId = products[StableRandom.Hash(id + ":taste", city.State.Seed) % (uint)products.Length];
                meal.PreferenceBonusCents = 50 + (int)(StableRandom.Hash(id + ":taste-strength", city.State.Seed) % 101);
            }
            var consumer = Consumer(person, Money.Zero, meal);
            FoodHouseholdPolicy.SetAllowance(consumer, resident, city.State.Seed,
                state.Districts.SingleOrDefault(d => d.DistrictId == resident.HomeDistrictId), null);
            meal.Allowance = consumer.DailyAllowance;
            if (state.HouseholdFinanceVersion != 0) CreateBudget(meal, person);
            StartMeal(meal, person, (int)(city.State.Minute / 1440), state.MealRhythmVersion == 0 ? MealKind.Legacy : MealKind.Breakfast);
            records.Add(meal.ResidentId, meal); state.Residents.Add(meal); return meal;
        }
        private void StartMeal(ResidentMeal meal, CitizenState person, int day, MealKind kind)
        {
            if (meal.Day != day) meal.DayAllowanceUsed = Money.Zero;
            meal.Day = day; meal.Kind = kind; meal.DesiredAt = MealRhythm.DesiredAt(person, city.State.Seed, day, kind);
            meal.ExpiresAt = MealRhythm.ExpiresAt(person, city.State.Seed, day, kind);
            meal.DueAt = Math.Max(city.State.Minute, meal.DesiredAt);
            meal.Status = MealStatus.Pending; meal.Reason = ""; meal.PlaceId = meal.BusinessId = meal.ProductId = "";
            meal.ExistingStop = false;
            meal.Decision = null; meal.LastContextKey = ""; meal.ContextAttempts = 0;
            meal.Price = Money.Zero; meal.ServiceAt = meal.ServiceMinutes = meal.ReturnMinutes = meal.Deadline = 0;
        }
        internal void Step()
        {
            if (pendingDecision != null)
            {
                var pendingMeal=pendingDecision;
                due.Remove(pendingMeal);
                try { ContinueFoodDecision(pendingMeal,city.FindCitizen(pendingMeal.AnchorId)!); }
                finally { due.Add(pendingMeal); }
                return;
            }
            if (StepBudget()) return;
            var meal = due.Min!; due.Remove(meal);
            try { StepMeal(meal); }
            finally { due.Add(meal); }
        }
        private void StepMeal(ResidentMeal meal)
        {
            var person = city.FindCitizen(meal.AnchorId);
            if (person == null) throw new InvalidOperationException("Meal has no resident.");
            double now = city.State.Minute;
            if (meal.Status == MealStatus.Uncertain && meal.Excursion != null)
            {
                // A blocked walking return does not prevent independently proving the
                // original transaction. Movement remains unresolved after proof settles cash.
                try
                {
                    if (market is IFoodSaleEvidence evidence && evidence.IsConfirmedPaid(meal))
                    {
                        if (meal.Budget != null) HouseholdFinance.ConfirmPurchase(meal.Budget);
                        ConfirmPurchase(meal, "Original native receipt confirmed; no checkout replay");
                    }
                }
                catch (Exception) { /* Missing proof preserves held funds and the original receipt. */ }
            }
            if (StepExcursion(meal, person)) { return; }
            if (Active(meal) && market is IFoodBusinessAuthority authority && !authority.CanServe(meal.BusinessId))
            { Finish(meal, MealStatus.Missed, "Business no longer accepts this commitment"); return; }
            if (!Active(meal) && meal.Status != MealStatus.Pending)
            {
                // Retain the exact unresolved receipt and any held money across later meals.
                if (meal.Status == MealStatus.Uncertain)
                {
                    bool confirmed = false;
                    try { confirmed = market is IFoodSaleEvidence evidence && evidence.IsConfirmedPaid(meal); }
                    catch (Exception) { /* Unavailable evidence cannot authorize spending or a refund. */ }
                    if (confirmed)
                    {
                        if (meal.Budget != null) HouseholdFinance.ConfirmPurchase(meal.Budget);
                        ConfirmPurchase(meal, "Original native receipt confirmed; no checkout replay");
                        meal.DueAt = now + .001;
                    }
                    else meal.DueAt = (Math.Floor(now / 1440) + 1) * 1440;
                    return;
                }
                if (meal.Slot == 0 && person.MealOuting != null)
                { meal.DueAt = Math.Max(now + 1, person.NextEventMinute); return; }
                NextMeal(meal, out int day, out MealKind kind);
                if (day < (int)(now / 1440)) { day = (int)(now / 1440); kind = state.MealRhythmVersion == 0 ? MealKind.Legacy : MealKind.Breakfast; }
                StartMeal(meal, person, day, kind);
            }
            if (now < meal.DesiredAt) meal.DueAt = meal.DesiredAt;
            else if (now < market.EarliestMinute)
            {
                // Native inventory is current, never replay sales into a historical day/hour.
                if (Active(meal)) Finish(meal, MealStatus.Missed, "Native clock passed the purchase window");
                meal.DueAt = Math.Max(meal.DueAt, market.EarliestMinute);
            }
            else if (meal.Status == MealStatus.Pending)
            {
                if (now >= meal.ExpiresAt) Finish(meal, MealStatus.Missed, "No suitable stop in the meal window");
                else if (meal.Slot > 0) DecideRepresented(meal, person);
                else if (AtFoodStop(person)) DecideAtStop(meal, person);
                else if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                    || Math.Min(person.NextEventMinute, person.ActivityUntilMinute) - now < 25)
                    meal.DueAt = Math.Min(meal.ExpiresAt, Math.Max(now + 1, person.NextEventMinute));
                else Decide(meal, person);
            }
            else if (meal.Status == MealStatus.Travelling)
            {
                bool admitted = meal.Slot > 0 ? meal.Excursion != null && now >= meal.Excursion.ArrivesAt && !meal.Excursion.Returning
                    && now < meal.Deadline && Position.Distance(city.FindPlace(meal.PlaceId).Position, meal.Excursion.DestinationPosition) <= .01
                    : person.MealOuting != null && !person.MealOuting.Returning
                    && person.Phase == CitizenPhase.AtPlace && person.PlaceId == meal.PlaceId
                    && person.RecentVisits.Any(v => v.PlaceId == meal.PlaceId && v.Activity == Activity.Shop && v.AtMinute >= meal.DesiredAt);
                if (admitted)
                {
                    var available = ReadBusiness(meal.BusinessId, now);
                    if (available?.PlaceId != meal.PlaceId) available = null;
                    if (available?.MealsPerHour == null || available.MealsPerHour <= 0)
                    { Finish(meal, MealStatus.Missed, "Service unavailable on arrival"); return; }
                    double? close = null;
                    if (meal.Slot > 0)
                    {
                        var hours = new Place { Id = available.PlaceId, Kind = PlaceKind.Shop, OpenMinute = 0, CloseMinute = 0, WeeklyHours = available.Hours };
                        var open = DailyRoutine.NextOpen(hours, now, meal.Deadline);
                        if (!open.HasValue)
                        { Finish(meal, MealStatus.Missed, "Shop closed before admission"); return; }
                        if (open.Value > now)
                        { meal.Excursion!.AdmissionAt = open.Value; meal.DueAt = open.Value; return; }
                        close = DailyRoutine.NextClose(hours, now);
                    }
                    meal.ServiceMinutes = 60.0 / available.MealsPerHour.Value;
                    var scope = state.Shops.Single(s => s.PlaceId == meal.PlaceId);
                    meal.ServiceAt = Math.Max(now, scope.NextServiceAt) + meal.ServiceMinutes;
                    if (meal.Slot > 0 && Math.Max(0, scope.NextServiceAt - now) > meal.Excursion!.MaximumQueueMinutes)
                        Finish(meal, MealStatus.Missed, "Queue exceeds patience on arrival");
                    else if (meal.Slot > 0 && (!close.HasValue || meal.ServiceAt >= close.Value))
                        Finish(meal, MealStatus.Missed, "Service no longer fits shop opening hours");
                    else if (meal.ServiceAt + MealRhythm.EatingMinutes(meal.Kind) + meal.ReturnMinutes > meal.Deadline)
                        Finish(meal, MealStatus.Missed, "Arrival or queue no longer fits the break");
                    else
                    {
                        if (meal.Slot == 0) city.ExtendMealStay(person, meal.ServiceAt + MealRhythm.EatingMinutes(meal.Kind));
                        scope.NextServiceAt = meal.ServiceAt;
                        meal.Status = MealStatus.Serving; meal.DueAt = meal.ServiceAt;
                    }
                }
                else if (meal.Slot > 0 || person.MealOuting == null || person.MealOuting.Returning || now >= meal.Deadline)
                    Finish(meal, MealStatus.Missed, "Visit was interrupted before admission");
                else meal.DueAt = Math.Max(now + .001, person.NextEventMinute);
            }
            else if (meal.Status == MealStatus.Serving)
            {
                if (meal.Slot > 0 ? meal.Excursion == null || meal.Excursion.Returning || now > meal.Deadline
                    : person.Phase != CitizenPhase.AtPlace || person.PlaceId != meal.PlaceId
                    || (meal.ExistingStop ? person.MealOuting != null || person.Activity != Activity.Shop
                        || now >= Math.Min(person.ActivityUntilMinute, person.NextEventMinute)
                        : person.MealOuting == null || person.MealOuting.Returning))
                    Finish(meal, MealStatus.Missed, "Resident left before service");
                else
                {
                    // Every later addition must fit before funds move or native checkout starts.
                    if (!CanSettle(meal))
                    { Finish(meal, MealStatus.Missed, "Purchase would exceed the exact money range"); return; }
                    if (meal.Budget != null && !HouseholdFinance.HoldPurchase(meal.Budget, meal.Price))
                    { Finish(meal, MealStatus.Missed, "Household funds no longer available at checkout"); return; }
                    if (meal.Budget == null) meal.DayAllowanceUsed += meal.Price;
                    // Persist uncertainty before entering native code. No exception can trigger a replay.
                    meal.ServiceAt = now;
                    Finish(meal, MealStatus.Uncertain, "Native purchase did not confirm completion");
                    try
                    {
                        var result = market.Buy(meal, now);
                        if (meal.Budget != null) {
                            if (result.Paid) HouseholdFinance.ConfirmPurchase(meal.Budget);
                            else HouseholdFinance.RefusePurchase(meal.Budget);
                        }
                        else if (!result.Paid) meal.DayAllowanceUsed -= meal.Price;
                        meal.Status = result.Paid ? MealStatus.Paid : MealStatus.Missed; meal.Reason = result.Reason;
                        if (result.Paid) ConfirmPurchase(meal, result.Reason);
                        else MealOutcomes.Record(meal, now, checkout: true);
                        if (meal.Slot > 0 && meal.Excursion != null) ScheduleReturn(meal);
                    }
                    catch (Exception) { /* Retain uncertain ownership; never issue a second order. */ }
                }
            }
        }
        private void ConfirmPurchase(ResidentMeal meal, string reason)
        {
            RecordPurchase(meal); meal.Purchases++; meal.Spent += meal.Price;
            if (meal.ExistingStop) meal.ExistingStopPurchases++;
            meal.Status = MealStatus.Paid; meal.Reason = reason;
            MealOutcomes.Record(meal, city.State.Minute, checkout: true);
        }
        private void Finish(ResidentMeal meal, MealStatus status, string reason, FoodOutsideOption outside = FoodOutsideOption.None)
        {
            string choiceReason=meal.Reason;
            if (Active(meal)) Reserve(meal, -1);
            meal.Status = status; meal.Reason = reason;
            MealOutcomes.Record(meal, city.State.Minute, checkout: status == MealStatus.Uncertain, outside);
            if(status==MealStatus.Uncertain && meal.Today?.Find(meal.Kind) is MealOutcomeRecord outcome)
            {
                if(choiceReason.Contains("likes it")) outcome.Factor1=MealFactor.Taste;
                if(choiceReason.Contains("values quality"))
                {if(outcome.Factor1==MealFactor.None) outcome.Factor1=MealFactor.Quality;else outcome.Factor2=MealFactor.Quality;}
            }
            var person = city.FindCitizen(meal.AnchorId)!;
            NextMeal(meal, out int day, out MealKind kind);
            meal.DueAt = Math.Max(city.State.Minute + .001, MealRhythm.DesiredAt(person, city.State.Seed, day, kind));
            if (meal.Slot > 0 && meal.Excursion != null) ScheduleReturn(meal);
        }
        private void NextMeal(ResidentMeal meal, out int day, out MealKind kind)
        {
            bool nextDay = meal.Kind == MealKind.Legacy || meal.Kind == MealKind.Dinner;
            day = meal.Day + (nextDay ? 1 : 0);
            kind = state.MealRhythmVersion == 0 ? MealKind.Legacy : nextDay ? MealKind.Breakfast : meal.Kind + 1;
        }
        private FoodRoutineResident Resident(CitizenState p) => new FoodRoutineResident { Identity = p.Identity, HomeId = p.HomeId,
            HomeDistrictId = city.FindPlace(p.HomeId).District, WalkingToleranceMetres = p.Preferences.WalkingToleranceMetres };
        private FoodConsumer Consumer(CitizenState p, Money allowance, ResidentMeal? meal = null) => new FoodConsumer { Id = meal?.ResidentId ?? p.Identity.Id, Name = p.Identity.Name,
            PreferredProductId = meal?.PreferredProductId ?? "", PreferenceBonusCents = meal?.PreferenceBonusCents ?? 0,
            OriginPlaceId = p.PlaceId, DailyAllowance = allowance,
            ValueOfTimeCentsPerMinute = 4 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-time", city.State.Seed) % 9),
            ServiceQualityValueCents = 100 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-quality", city.State.Seed) % 301),
            DistrictAppealValueCents = 50 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-district", city.State.Seed) % 151) };
        private void Decide(ResidentMeal meal, CitizenState person)
        {
            if (NewChoice(meal)) { BeginFoodDecision(meal,person,false); return; }
            long started = city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var trace = city.ObservingFood ? new FoodDecisionTrace() : null;
            double now = city.State.Minute;
            CaptureRoutineStop(person);
            var stop = meal.Slot == 0 ? new FoodRoutineStop { AnchorId = person.Identity.Id, PlaceId = person.PlaceId, Activity = person.Activity,
                DepartureAt = Math.Min(person.NextEventMinute, person.ActivityUntilMinute) } : routineStops[person.Identity.Id];
            var consumer = Consumer(person, AvailableForMeal(meal), meal);
            consumer.OriginPlaceId = stop.PlaceId;
            MealRhythm.Apply(consumer, meal.Kind);
            var intent = new FoodIntent { Id = meal.IntentId, ConsumerId = meal.ResidentId, Day = meal.Day,
                DesiredAt = meal.DesiredAt, RequestedAt = now, Deadline = Math.Min(meal.ExpiresAt,
                    Math.Min(now + (meal.Slot == 0 ? MealRhythm.BreakMinutes(person, city.State.Seed, meal.Kind) : RepresentedBreak(person, stop, meal.Kind)),
                        stop.DepartureAt)),
                OriginPlaceId = stop.PlaceId, OriginActivity = stop.Activity, EatingMinutes = MealRhythm.EatingMinutes(meal.Kind), MaximumQueueMinutes = MealRhythm.QueueMinutes(meal.Kind, stop.Activity),
                TimeCostMultiplier = 1 + Math.Min(1, (now - meal.DesiredAt) / 120), ContextEvidence = meal.Kind + ": live scheduled presence and departure deadline; meal-specific time, quality and food suitability" };
            SetFoodAlternative(meal, person, consumer, intent);
            var businesses = ReadLocalBusinesses(city.FindPlace(stop.PlaceId), now, person.Preferences.WalkingToleranceMetres);
            var quotes = new List<FoodTravelQuote>();
            var refusals = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new Dictionary<string, Route>(StringComparer.Ordinal);
            var returnGeometry = new Dictionary<string, string>(StringComparer.Ordinal);
            var preferences = new TravelPreferences { AllowTransit = false, OwnsCar = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
            foreach (var b in businesses)
            {
                var scope = state.Shops.Single(s => s.PlaceId == b.PlaceId);
                b.NextServiceMinute = Math.Max(now, scope.NextServiceAt);
                ApplyReservations(b);
                var rejection = FoodDemand.KnownUnavailable(b, consumer, now, intent.Deadline);
                if (rejection != null)
                { refusals[b.Id] = rejection; if (trace != null) trace.RouteChecks[b.Id] = rejection; continue; }
                var origin = city.FindPlace(stop.PlaceId); var shop = city.FindPlace(b.PlaceId);
                if (Position.Distance(origin.Position, shop.Position) > preferences.WalkingToleranceMetres)
                { refusals[b.Id] = "Beyond walking tolerance"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                // A safe geometric bound saves native path searches for distant shops. It is
                // not a route quote: feasible candidates still need complete outward/return paths.
                if (routes is IWalkingTimeBound bound && b.MealsPerHour > 0
                    && now + bound.MinimumWalkingMinutes(origin, shop) + bound.MinimumWalkingMinutes(shop, origin)
                        + 60.0 / b.MealsPerHour.Value + intent.EatingMinutes > intent.Deadline)
                { refusals[b.Id] = "Travel and service cannot fit break"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                var outward = WalkingQuote(origin, shop, now, preferences, trace);
                var back = outward == null ? null : WalkingQuote(shop, origin, now + outward.Minutes, preferences, trace);
                if (outward == null || back == null || outward.Legs.Any(l => l.Mode != TravelMode.Walk) || back.Legs.Any(l => l.Mode != TravelMode.Walk))
                { if (trace != null) trace.RouteChecks[b.Id] = outward == null ? "No outward walking path" : back == null ? "No return walking path" : "Non-walking path"; continue; }
                if (trace != null) trace.RouteChecks[b.Id] = "Complete walking paths";
                paths.Add(b.Id, outward);
                if (meal.Slot > 0) returnGeometry.Add(b.Id, GeometrySignature(back));
                quotes.Add(new FoodTravelQuote { ConsumerId = meal.ResidentId, BusinessId = b.Id, IntentId = meal.IntentId,
                    OriginPlaceId = stop.PlaceId, Minutes = outward.Minutes, ReturnMinutes = back.Minutes, Evidence = "Current complete walking paths" });
            }
            FoodDemand.ChooseObserved(consumer, businesses, quotes, intent, now, refusals, meal.Kind);
            RecordChoices(intent, businesses);
            var choice = intent;
            if (trace != null)
            {
                trace.MealKind = meal.Kind; trace.Consumer = consumer; trace.Intent = choice; trace.Businesses = businesses; trace.Quotes = quotes;
                trace.ElapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                city.ObserveFood(trace);
            }
            if (choice.Status != FoodIntentStatus.Travelling)
            { Finish(meal, choice.Status == FoodIntentStatus.DeclinedPurchase ? MealStatus.Declined : MealStatus.Missed, choice.Outcome, choice.OutsideOption); return; }
            var selected = businesses.Single(b => b.Id == choice.BusinessId);
            var selectedChoice = choice.Choices.Single(c => c.BusinessId == choice.BusinessId && c.ProductId == choice.ProductId);
            double stay = selectedChoice.ServiceEndsAt!.Value - (now + paths[selected.Id].Minutes) + intent.EatingMinutes;
            if (meal.Slot == 0 && !city.TryBeginMealVisit(person.Identity.Id, selected.PlaceId, paths[selected.Id], choice.ReturnTravelMinutes!.Value, stay, intent.Deadline))
            { Finish(meal, MealStatus.Missed, "Schedule or shop changed before departure"); return; }
            meal.PlaceId = selected.PlaceId; meal.BusinessId = selected.Id; meal.ProductId = choice.ProductId;
            meal.Price = selectedChoice.Price; meal.ServiceMinutes = 60.0 / selected.MealsPerHour!.Value;
            meal.ReturnMinutes = choice.ReturnTravelMinutes!.Value; meal.Deadline = intent.Deadline;
            meal.Status = MealStatus.Travelling;
            if (meal.Slot > 0)
            {
                meal.Excursion = new MealExcursion { OriginId = stop.PlaceId, DestinationId = selected.PlaceId, OriginActivity = stop.Activity,
                    OriginPosition = city.FindPlace(stop.PlaceId).Position, DestinationPosition = city.FindPlace(selected.PlaceId).Position,
                    DepartedAt = now, ArrivesAt = now + paths[selected.Id].Minutes, SharedDepartureAt = stop.DepartureAt,
                    AdmissionAt = selectedChoice.AdmissionMinute!.Value,
                    ReturnGeometry = returnGeometry[selected.Id], MaximumQueueMinutes = intent.MaximumQueueMinutes };
                meal.DueAt = meal.Excursion.AdmissionAt;
            }
            else meal.DueAt = person.NextEventMinute;
            Reserve(meal, 1);
        }
    }
}
