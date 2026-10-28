using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public enum FoodIntentStatus { Planned, Travelling, Fulfilled, Unmet, Unresolved, WaitingForService, DeclinedPurchase }
    public enum FoodExperimentMode { FixedDailyReference, ObservedRoutines }
    public enum FoodOutsideOption { None, HomePreparation, PackedMeal, FundedHouseholdFood }

    /// <summary>A detached input to a counterfactual experiment, never native inventory.</summary>
    public sealed class FoodBusiness
    {
        public string Id { get; set; } = "";
        public string PlaceId { get; set; } = "";
        public string Name { get; set; } = "";
        public List<OpeningWindow> Hours { get; set; } = new List<OpeningWindow>();
        public List<FoodOffer> Offers { get; set; } = new List<FoodOffer>();
        // Effective sellable/staffed throughput, not floor area or guaranteed demand.
        // null is unknown; zero is unavailable. Used only in ObservedRoutines mode.
        public int? MealsPerHour { get; set; }
        public string ServiceEvidence { get; set; } = "Service availability and throughput unknown";
        public string ServiceUnavailableReason { get; set; } = "";
        public double NextServiceMinute { get; set; }
        public string DistrictId { get; set; } = "";
        public int? CustomerServicePercent { get; set; }
        public string CustomerServiceEvidence { get; set; } = "Customer service unknown; no quality adjustment";
    }

    public sealed class FoodOffer
    {
        public string ProductId { get; set; } = "";
        public Money Price { get; set; }
        // null means unknown, not unlimited. The experiment consumes its own copy only.
        public int? Units { get; set; }
        public int? DistrictDemandPercent { get; set; }
        public string DistrictDemandEvidence { get; set; } = "District product demand unknown; no appeal adjustment";
    }

    public sealed class FoodConsumer
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string OriginPlaceId { get; set; } = "";
        public int MealMinuteOfDay { get; set; } = 720;
        public int MealWindowMinutes { get; set; } = 90;
        public Money DailyAllowance { get; set; } = Money.FromCents(1200);
        public int ValueOfTimeCentsPerMinute { get; set; } = 5;
        public string PreferredProductId { get; set; } = "";
        public int PreferenceBonusCents { get; set; } = 100;
        // Full 0-to-100-point difference in perceived value. Zero preserves older experiments.
        public int ServiceQualityValueCents { get; set; }
        public int DistrictAppealValueCents { get; set; }
        public string HomeDistrictId { get; set; } = "";
        public string BudgetEvidence { get; set; } = "Supplied temporary allowance; no household income observation";
    }

    public sealed class FoodTravelQuote
    {
        public string ConsumerId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        // null is a refused route. No quote is unknown; neither becomes a straight-line journey.
        public double? Minutes { get; set; }
        public string Evidence { get; set; } = "";
        // Routine quotes belong to one opportunity at one observed origin.
        public string IntentId { get; set; } = "";
        public string OriginPlaceId { get; set; } = "";
        public double? ReturnMinutes { get; set; }
    }

    public sealed class FoodChoice
    {
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public Money Price { get; set; }
        public double? TravelMinutes { get; set; }
        public double? AdmissionMinute { get; set; }
        public double? ScoreCents { get; set; }
        public string Reason { get; set; } = "";
        public double? ReturnMinutes { get; set; }
        public double? ServiceEndsAt { get; set; }
        public int? CustomerServicePercent { get; set; }
        public int? DistrictDemandPercent { get; set; }
        public double ServiceQualityAdjustmentCents { get; set; }
        public double DistrictAppealAdjustmentCents { get; set; }
    }

    public sealed class FoodIntent
    {
        public string Id { get; set; } = "";
        public string ConsumerId { get; set; } = "";
        public int Day { get; set; }
        public double RequestedAt { get; set; }
        public double Deadline { get; set; }
        public FoodIntentStatus Status { get; set; }
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public double? ArrivalMinute { get; set; }
        public Money Spent { get; set; }
        public string Outcome { get; set; } = "";
        public List<FoodChoice> Choices { get; set; } = new List<FoodChoice>();
        public string OriginPlaceId { get; set; } = "";
        public Activity OriginActivity { get; set; }
        public string ContextEvidence { get; set; } = "Fixed daily reference; no activity observation";
        public double DesiredAt { get; set; }
        public int EatingMinutes { get; set; }
        public int MaximumQueueMinutes { get; set; }
        public double TimeCostMultiplier { get; set; } = 1;
        public double? ReturnTravelMinutes { get; set; }
        public double? ServiceEndsAt { get; set; }
        public double? ReturnBy { get; set; }
        public FoodOutsideOption OutsideOption { get; set; }
        public int? OutsideOptionScoreCents { get; set; }
        public string OutsideOptionEvidence { get; set; } = "No outside option modeled";
    }

    public sealed class FoodShadowState
    {
        public const int CurrentSchema = 5;
        public int Schema { get; set; } = CurrentSchema;
        public string Kind { get; set; } = "persistentcity:food-shadow";
        public string CityKey { get; set; } = "";
        public string Evidence { get; set; } = "";
        public string Scope { get; set; } = "Residents only; commuters, visitors, outside trade and wages are not modeled.";
        public double StartedAt { get; set; }
        public double Minute { get; set; }
        public double EndsAt { get; set; }
        public List<FoodConsumer> Consumers { get; set; } = new List<FoodConsumer>();
        public List<FoodBusiness> Businesses { get; set; } = new List<FoodBusiness>();
        public List<FoodTravelQuote> TravelQuotes { get; set; } = new List<FoodTravelQuote>();
        public List<FoodIntent> Intents { get; set; } = new List<FoodIntent>();
        public FoodExperimentMode Mode { get; set; }
    }

    /// <summary>
    /// A bounded, detached counterfactual: one meal per resident/day, temporary allowances,
    /// explicit travel quotes and finite stock. It has no access to CityState or native writers.
    /// </summary>
    public sealed class FoodDemand
    {
        public FoodShadowState State { get; }
        private readonly Dictionary<string, FoodConsumer> consumers;
        private readonly Dictionary<string, FoodBusiness> businesses;
        private readonly Dictionary<string, FoodTravelQuote> quotes;
        private readonly SortedSet<FoodIntent> pending;
        private readonly Dictionary<string, int> reservedStock = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool Routine => State.Mode == FoodExperimentMode.ObservedRoutines;

        public FoodDemand(FoodShadowState state)
        {
            Validate(state);
            State = state;
            consumers = state.Consumers.ToDictionary(c => c.Id, StringComparer.Ordinal);
            businesses = state.Businesses.ToDictionary(b => b.Id, StringComparer.Ordinal);
            quotes = state.TravelQuotes.ToDictionary(q => Key(Routine ? q.IntentId : q.ConsumerId, q.BusinessId), StringComparer.Ordinal);
            foreach (var intent in state.Intents.Where(i => i.Status == FoodIntentStatus.WaitingForService)) Reserve(intent, 1);
            pending = new SortedSet<FoodIntent>(state.Intents.Where(i => !Terminal(i)), Comparer<FoodIntent>.Create((a, b) =>
            { int time = Due(a).CompareTo(Due(b)); return time != 0 ? time : StringComparer.Ordinal.Compare(a.Id, b.Id); }));
        }

        public static FoodShadowState Create(string cityKey, string evidence, int firstDay, int days,
            IEnumerable<FoodConsumer> people, IEnumerable<FoodBusiness> shops, IEnumerable<FoodTravelQuote> routes)
        {
            if (firstDay < 0 || firstDay > 100000 || days < 1 || days > 7)
                throw new ArgumentOutOfRangeException(nameof(days), "A shadow experiment covers one to seven days.");
            var state = new FoodShadowState
            {
                CityKey = cityKey, Evidence = evidence, StartedAt = firstDay * 1440.0,
                Minute = firstDay * 1440.0, EndsAt = (firstDay + days) * 1440.0,
                Consumers = people.Select(Clone).OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
                Businesses = shops.Select(Clone).OrderBy(b => b.Id, StringComparer.Ordinal).ToList(),
                TravelQuotes = routes.Select(q => new FoodTravelQuote { ConsumerId = q.ConsumerId, BusinessId = q.BusinessId,
                    Minutes = q.Minutes, Evidence = q.Evidence, IntentId = q.IntentId, OriginPlaceId = q.OriginPlaceId,
                    ReturnMinutes = q.ReturnMinutes }).OrderBy(q => q.ConsumerId, StringComparer.Ordinal)
                    .ThenBy(q => q.BusinessId, StringComparer.Ordinal).ToList()
            };
            foreach (var person in state.Consumers)
            for (int day = firstDay; day < firstDay + days; day++)
            {
                double at = day * 1440.0 + person.MealMinuteOfDay;
                state.Intents.Add(new FoodIntent { Id = IntentId(person.Id, day), ConsumerId = person.Id, Day = day,
                    RequestedAt = at, Deadline = Math.Min((day + 1) * 1440.0, at + person.MealWindowMinutes) });
            }
            state.Intents = state.Intents.OrderBy(i => i.RequestedAt).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
            Validate(state);
            return state;
        }

        public bool AdvanceTo(double minute, int eventBudget = 128)
        {
            if (!Position.Finite(minute) || minute < State.Minute || minute > State.EndsAt || eventBudget < 1)
                throw new ArgumentOutOfRangeException(nameof(minute));
            int events = 0;
            // The finite experiment has at most seven intentions per resident. Keep ordering
            // explicit so arrival/departure ties and interrupted batches replay identically.
            while (pending.Count > 0)
            {
                var next = pending.Min!;
                double at = Due(next);
                if (at > minute) break;
                if (events++ == eventBudget) return false;
                pending.Remove(next);
                State.Minute = at;
                if (next.Status == FoodIntentStatus.Planned) Plan(next);
                else if (next.Status == FoodIntentStatus.WaitingForService) CompleteService(next);
                else Arrive(next);
                if (!Terminal(next)) pending.Add(next);
            }
            State.Minute = minute;
            return true;
        }

        public static FoodShadowState CreateObserved(string cityKey, string evidence, int firstDay, int days,
            IEnumerable<FoodConsumer> people, IEnumerable<FoodBusiness> shops, IEnumerable<FoodTravelQuote> routes,
            IEnumerable<FoodIntent> opportunities)
        {
            var state = Create(cityKey, evidence, firstDay, days, people, shops, Array.Empty<FoodTravelQuote>());
            state.Mode = FoodExperimentMode.ObservedRoutines;
            state.Scope = "One optional purchased-meal opportunity per resident/day, from recorded activities. Provisional allowances/prepared-food preferences; household ingredients, finances and nutrition unverified. Declining purchase is not a fulfilled meal. Hypothetical trips do not alter recorded routines.";
            state.Intents = opportunities.Select(i => new FoodIntent
            {
                Id = i.Id, ConsumerId = i.ConsumerId, Day = i.Day, RequestedAt = i.RequestedAt, Deadline = i.Deadline,
                OriginPlaceId = i.OriginPlaceId, OriginActivity = i.OriginActivity, ContextEvidence = i.ContextEvidence,
                DesiredAt = i.DesiredAt, EatingMinutes = i.EatingMinutes, MaximumQueueMinutes = i.MaximumQueueMinutes,
                TimeCostMultiplier = i.TimeCostMultiplier, OutsideOption = i.OutsideOption,
                OutsideOptionScoreCents = i.OutsideOptionScoreCents, OutsideOptionEvidence = i.OutsideOptionEvidence
            }).OrderBy(i => i.RequestedAt).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
            state.TravelQuotes = routes.Select(q => new FoodTravelQuote { ConsumerId = q.ConsumerId, BusinessId = q.BusinessId,
                Minutes = q.Minutes, ReturnMinutes = q.ReturnMinutes, IntentId = q.IntentId, OriginPlaceId = q.OriginPlaceId,
                Evidence = q.Evidence }).OrderBy(q => q.IntentId, StringComparer.Ordinal).ThenBy(q => q.BusinessId, StringComparer.Ordinal).ToList();
            Validate(state);
            return state;
        }

        /// <summary>Explicit experiment intervention; never writes back into an observed business.</summary>
        public void SetPrice(string businessId, string productId, Money price)
        {
            if (price <= Money.Zero || price > Money.FromCents(1000000)) throw new ArgumentOutOfRangeException(nameof(price));
            Offer(businessId, productId).Price = price;
        }

        public void SetStock(string businessId, string productId, int? units)
        {
            if (units < 0 || units > 1000000) throw new ArgumentOutOfRangeException(nameof(units));
            Offer(businessId, productId).Units = units;
        }

        public void Close(string businessId) => businesses[businessId].Hours.Clear();

        private FoodOffer Offer(string businessId, string productId)
            => businesses[businessId].Offers.Single(o => o.ProductId == productId);

        private void Plan(FoodIntent intent)
        {
            var person = consumers[intent.ConsumerId];
            Choose(intent, person, State.Businesses, business => quotes.TryGetValue(
                Key(Routine ? intent.Id : person.Id, business.Id), out var quote) ? quote : null, State.Minute, Routine);
        }

        /// <summary>One decision using already validated live inputs. Mutates only the supplied
        /// fresh intent; no experiment document, cloning, scheduling or stock settlement.
        /// Persisted/untrusted experiment inputs still use the validating constructor.</summary>
        public static void ChooseObserved(FoodConsumer person, IReadOnlyList<FoodBusiness> shops,
            IReadOnlyList<FoodTravelQuote> routes, FoodIntent intent, double minute,
            IReadOnlyDictionary<string, string>? refusals = null, MealKind mealKind = MealKind.Legacy)
        {
            if (intent.Status != FoodIntentStatus.Planned || intent.Choices.Count != 0 || shops.Count > 8)
                throw new ArgumentException("A fresh intention and bounded live market are required.");
            Choose(intent, person, shops, business => routes.FirstOrDefault(q => q.BusinessId == business.Id
                && q.IntentId == intent.Id && q.ConsumerId == person.Id), minute, true, refusals, mealKind);
        }

        // Only reject facts that already prevent choice under this current-supply model.
        // Unknown supply stays unknown; no route or future staffing is invented.
        public static string? KnownUnavailable(FoodBusiness business, FoodConsumer consumer, double now, double deadline)
        {
            if (!DailyRoutine.NextOpen(Place(business), now, deadline).HasValue) return "Closed or too late";
            if (business.MealsPerHour == 0) return business.ServiceUnavailableReason.Length == 0
                ? "Service unavailable" : business.ServiceUnavailableReason;
            if (business.Offers.Count > 0 && business.Offers.All(o => o.Units == 0)) return "Out of stock";
            if (business.Offers.Count > 0 && business.Offers.All(o => o.Price > consumer.DailyAllowance)) return "Over allowance";
            return null;
        }

        private static void Choose(FoodIntent intent, FoodConsumer person, IReadOnlyList<FoodBusiness> shops,
            Func<FoodBusiness, FoodTravelQuote?> routeFor, double minute, bool routine,
            IReadOnlyDictionary<string, string>? refusals = null, MealKind mealKind = MealKind.Legacy)
        {
            if (routine && intent.OriginPlaceId.Length == 0)
            { Miss(intent, intent.ContextEvidence, true); return; }
            foreach (var business in shops.OrderBy(b => b.Id, StringComparer.Ordinal))
            foreach (var offer in business.Offers.OrderBy(o => o.ProductId, StringComparer.Ordinal))
            {
                var choice = new FoodChoice { BusinessId = business.Id, ProductId = offer.ProductId, Price = offer.Price,
                    CustomerServicePercent = business.CustomerServicePercent, DistrictDemandPercent = offer.DistrictDemandPercent,
                    ServiceQualityAdjustmentCents = business.CustomerServicePercent.HasValue
                        ? (50 - business.CustomerServicePercent.Value) / 100.0 * person.ServiceQualityValueCents : 0,
                    DistrictAppealAdjustmentCents = offer.DistrictDemandPercent.HasValue
                        ? (50 - offer.DistrictDemandPercent.Value) / 100.0 * person.DistrictAppealValueCents : 0 };
                var route = routeFor(business);
                var knownUnavailable = routine ? KnownUnavailable(business, person, minute, intent.Deadline) : null;
                if (knownUnavailable != null) choice.Reason = knownUnavailable;
                else if (refusals != null && refusals.TryGetValue(business.Id, out var refusal)) choice.Reason = refusal;
                else if (route == null) choice.Reason = "Route unknown";
                else if (!route.Minutes.HasValue) choice.Reason = "Unreachable";
                else if (routine && !route.ReturnMinutes.HasValue) choice.Reason = "Return route unavailable";
                else
                {
                    choice.TravelMinutes = route.Minutes.Value;
                    double arrival = minute + route.Minutes.Value;
                    double? admission = arrival < intent.Deadline ? DailyRoutine.NextOpen(Place(business), arrival, intent.Deadline) : null;
                    choice.AdmissionMinute = admission;
                    choice.ReturnMinutes = route.ReturnMinutes;
                    if (!offer.Units.HasValue) choice.Reason = "Stock unknown";
                    else if (offer.Units == 0) choice.Reason = "Out of stock";
                    else if (offer.Price > person.DailyAllowance) choice.Reason = "Over allowance";
                    else if (!admission.HasValue) choice.Reason = "Closed or too late";
                    else if (routine && !business.MealsPerHour.HasValue) choice.Reason = "Service unknown";
                    else if (routine && business.MealsPerHour == 0) choice.Reason = "Service unavailable";
                    else
                    {
                        double finish = routine ? Math.Max(admission.Value, business.NextServiceMinute) + 60.0 / business.MealsPerHour!.Value : admission.Value;
                        choice.ServiceEndsAt = routine ? finish : (double?)null;
                        double time = finish - minute + (routine ? route.ReturnMinutes!.Value + intent.EatingMinutes : 0);
                        double? close = DailyRoutine.NextClose(Place(business), admission.Value);
                        if (routine && Math.Max(0, business.NextServiceMinute - admission.Value) > intent.MaximumQueueMinutes)
                        { choice.Reason = "Expected queue exceeds patience"; intent.Choices.Add(choice); continue; }
                        if (routine && (minute + time > intent.Deadline || !close.HasValue || finish >= close.Value))
                        { choice.Reason = "Service and return do not fit available time"; intent.Choices.Add(choice); continue; }
                        choice.ScoreCents = offer.Price.ApproximateCents() + time * person.ValueOfTimeCentsPerMinute * intent.TimeCostMultiplier
                            - (offer.ProductId == person.PreferredProductId ? person.PreferenceBonusCents : 0)
                            + choice.ServiceQualityAdjustmentCents + choice.DistrictAppealAdjustmentCents
                            + MealRhythm.ProductPenalty(mealKind, offer.ProductId);
                        choice.Reason = "Eligible";
                    }
                }
                intent.Choices.Add(choice);
            }
            var selected = intent.Choices.Where(c => c.ScoreCents.HasValue).OrderBy(c => c.ScoreCents)
                .ThenBy(c => c.BusinessId, StringComparer.Ordinal).ThenBy(c => c.ProductId, StringComparer.Ordinal).FirstOrDefault();
            if (selected == null)
            {
                bool unknown = intent.Choices.Any(c => c.Reason == "Stock unknown" || c.Reason == "Route unknown" || c.Reason == "Service unknown");
                if (!unknown && intent.OutsideOptionScoreCents.HasValue) { Decline(intent); return; }
                Miss(intent, unknown ? "Insufficient stock, service or route evidence" : "No feasible purchased meal", unknown);
                return;
            }
            if (intent.OutsideOptionScoreCents.HasValue && selected.ScoreCents >= intent.OutsideOptionScoreCents.Value)
            { Decline(intent); return; }
            selected.Reason = routine ? "Chosen: price + round-trip/service time and urgency + quality/district adjustments - food preference"
                : "Chosen: price + travel/wait cost + quality/district adjustments - food preference";
            intent.BusinessId = selected.BusinessId; intent.ProductId = selected.ProductId;
            intent.ArrivalMinute = selected.AdmissionMinute; intent.Status = FoodIntentStatus.Travelling;
            intent.ReturnTravelMinutes = selected.ReturnMinutes;
        }

        private void Arrive(FoodIntent intent)
        {
            var business = businesses[intent.BusinessId];
            var offer = Offer(intent.BusinessId, intent.ProductId);
            if (State.Minute >= intent.Deadline || !DailyRoutine.Open(Place(business), State.Minute))
            { Miss(intent, "Closed on arrival"); return; }
            if (!offer.Units.HasValue) { Miss(intent, "Stock unknown on arrival", true); return; }
            if (offer.Units.Value == 0) { Miss(intent, "Out of stock on arrival"); return; }
            if (offer.Price > consumers[intent.ConsumerId].DailyAllowance)
            { Miss(intent, "Over allowance on arrival"); return; }
            if (Routine)
            {
                if (!business.MealsPerHour.HasValue) { Miss(intent, "Service unknown on arrival", true); return; }
                if (business.MealsPerHour == 0) { Miss(intent, "Service unavailable on arrival"); return; }
                if (offer.Units <= Reserved(intent)) { Miss(intent, "Remaining stock already reserved for customers in service"); return; }
                if (Math.Max(0, business.NextServiceMinute - State.Minute) > intent.MaximumQueueMinutes)
                { Miss(intent, "Queue exceeds patience on arrival"); return; }
                double finish = Math.Max(State.Minute, business.NextServiceMinute) + 60.0 / business.MealsPerHour.Value;
                double back = finish + intent.EatingMinutes + intent.ReturnTravelMinutes!.Value;
                var close = DailyRoutine.NextClose(Place(business), State.Minute);
                if (back > intent.Deadline || !close.HasValue || finish >= close.Value)
                { Miss(intent, "Queue leaves insufficient time to eat and return"); return; }
                business.NextServiceMinute = finish;
                intent.ServiceEndsAt = finish; intent.ReturnBy = back;
                intent.Status = FoodIntentStatus.WaitingForService;
                Reserve(intent, 1);
                return;
            }
            Fulfill(intent, offer);
        }

        private void CompleteService(FoodIntent intent)
        {
            Reserve(intent, -1);
            var business = businesses[intent.BusinessId];
            var offer = Offer(intent.BusinessId, intent.ProductId);
            if (!business.MealsPerHour.HasValue || !offer.Units.HasValue)
            { Miss(intent, "Service or stock unknown at completion", true); return; }
            if (business.MealsPerHour == 0 || !DailyRoutine.Open(Place(business), State.Minute))
            { Miss(intent, "Service closed before completion"); return; }
            if (offer.Units == 0) { Miss(intent, "Out of stock at completion"); return; }
            if (offer.Price > consumers[intent.ConsumerId].DailyAllowance)
            { Miss(intent, "Over allowance at completion"); return; }
            Fulfill(intent, offer);
        }

        private void Fulfill(FoodIntent intent, FoodOffer offer)
        {
            offer.Units--;
            intent.Spent = offer.Price;
            intent.Status = FoodIntentStatus.Fulfilled;
            intent.Outcome = Routine ? "Hypothetical served meal; return fits recorded activity; temporary allowance; no native sale"
                : "Hypothetical meal; temporary allowance; no native sale";
        }

        private int Reserved(FoodIntent intent) => reservedStock.TryGetValue(Key(intent.BusinessId, intent.ProductId), out int count) ? count : 0;
        private void Reserve(FoodIntent intent, int delta) => reservedStock[Key(intent.BusinessId, intent.ProductId)] = Reserved(intent) + delta;

        private static void Miss(FoodIntent intent, string why, bool unresolved = false)
        { intent.Status = unresolved ? FoodIntentStatus.Unresolved : FoodIntentStatus.Unmet; intent.Outcome = why; }

        private static void Decline(FoodIntent intent)
        {
            intent.Status = FoodIntentStatus.DeclinedPurchase;
            intent.Outcome = "Prefers " + (intent.OutsideOption == FoodOutsideOption.HomePreparation ? "home preparation"
                : intent.OutsideOption == FoodOutsideOption.FundedHouseholdFood ? "the paid household food basket" : "a planned packed meal")
                + " to the observed purchase options; no shop spend or stock consumed; "
                + (intent.OutsideOption == FoodOutsideOption.FundedHouseholdFood ? "household food basket is funded" : "household food/ingredients/cost not verified");
        }

        private static Place Place(FoodBusiness business) => new Place
        { Id = business.PlaceId, Kind = PlaceKind.Shop, OpenMinute = 0, CloseMinute = 0, WeeklyHours = business.Hours };
        private static bool Terminal(FoodIntent intent) => intent.Status == FoodIntentStatus.Fulfilled
            || intent.Status == FoodIntentStatus.Unmet || intent.Status == FoodIntentStatus.Unresolved || intent.Status == FoodIntentStatus.DeclinedPurchase;
        private static double Due(FoodIntent intent) => intent.Status == FoodIntentStatus.Planned ? intent.RequestedAt
            : intent.Status == FoodIntentStatus.WaitingForService ? intent.ServiceEndsAt!.Value : intent.ArrivalMinute!.Value;
        private static string Key(string a, string b) => a.Length.ToString(CultureInfo.InvariantCulture) + ":" + a + b;
        public static string IntentId(string id, int day) => "food:" + Uri.EscapeDataString(id) + ":" + day.ToString(CultureInfo.InvariantCulture);

        private static FoodConsumer Clone(FoodConsumer c) => new FoodConsumer { Id = c.Id, Name = c.Name,
            OriginPlaceId = c.OriginPlaceId, MealMinuteOfDay = c.MealMinuteOfDay, MealWindowMinutes = c.MealWindowMinutes,
            DailyAllowance = c.DailyAllowance, ValueOfTimeCentsPerMinute = c.ValueOfTimeCentsPerMinute,
            PreferredProductId = c.PreferredProductId, PreferenceBonusCents = c.PreferenceBonusCents,
            ServiceQualityValueCents = c.ServiceQualityValueCents, DistrictAppealValueCents = c.DistrictAppealValueCents,
            HomeDistrictId = c.HomeDistrictId, BudgetEvidence = c.BudgetEvidence };
        private static FoodBusiness Clone(FoodBusiness b) => new FoodBusiness { Id = b.Id, PlaceId = b.PlaceId, Name = b.Name,
            MealsPerHour = b.MealsPerHour, ServiceEvidence = b.ServiceEvidence, NextServiceMinute = b.NextServiceMinute,
            DistrictId = b.DistrictId, CustomerServicePercent = b.CustomerServicePercent, CustomerServiceEvidence = b.CustomerServiceEvidence,
            Hours = b.Hours.Select(h => new OpeningWindow { Day = h.Day, FromMinute = h.FromMinute, ToMinute = h.ToMinute }).ToList(),
            Offers = b.Offers.Select(o => new FoodOffer { ProductId = o.ProductId, Price = o.Price, Units = o.Units,
                DistrictDemandPercent = o.DistrictDemandPercent, DistrictDemandEvidence = o.DistrictDemandEvidence }).ToList() };

        public static void Validate(FoodShadowState state)
        {
            void Require(bool ok, string message) { if (!ok) throw new ArgumentException("Food shadow: " + message); }
            bool Text(string? value) => value != null && !string.IsNullOrWhiteSpace(value) && value.Length <= 1024;
            bool Time(double value) => Position.Finite(value) && value >= 0 && value <= 145000000;
            bool Percent(int? value) => !value.HasValue || value >= 0 && value <= 100;
            Require(state != null, "missing state.");
            Require(state!.Schema == FoodShadowState.CurrentSchema && state.Kind == "persistentcity:food-shadow", "unsupported schema or kind.");
            Require(Enum.IsDefined(typeof(FoodExperimentMode), state.Mode), "unknown experiment mode.");
            bool routine = state.Mode == FoodExperimentMode.ObservedRoutines;
            Require(Text(state.CityKey) && Text(state.Evidence) && Text(state.Scope), "missing evidence/city/scope.");
            Require(Time(state.StartedAt) && Time(state.Minute) && Time(state.EndsAt) && state.StartedAt % 1440 == 0
                && state.EndsAt % 1440 == 0 && state.Minute >= state.StartedAt && state.Minute <= state.EndsAt
                && state.EndsAt > state.StartedAt && state.EndsAt - state.StartedAt <= 7 * 1440, "invalid experiment clock.");
            Require(state.Consumers != null && state.Consumers.Count > 0 && state.Consumers.Count <= 5000
                && state.Consumers.All(c => c != null && Text(c.Id) && Text(c.Name) && Text(c.OriginPlaceId)
                    && c.MealMinuteOfDay >= 0 && c.MealMinuteOfDay < 1440 && c.MealWindowMinutes > 0 && c.MealWindowMinutes <= 240
                    && c.DailyAllowance >= Money.Zero && c.DailyAllowance <= Money.FromCents(1000000)
                    && c.HomeDistrictId != null && c.HomeDistrictId.Length <= 1024 && Text(c.BudgetEvidence)
                    && c.ValueOfTimeCentsPerMinute >= 0 && c.ValueOfTimeCentsPerMinute <= 10000
                    && c.ServiceQualityValueCents >= 0 && c.ServiceQualityValueCents <= 1000000
                    && c.DistrictAppealValueCents >= 0 && c.DistrictAppealValueCents <= 1000000
                    && c.PreferredProductId != null && c.PreferredProductId.Length <= 1024
                    && c.PreferenceBonusCents >= 0 && c.PreferenceBonusCents <= 1000000), "invalid consumers.");
            Require(state.Consumers!.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == state.Consumers!.Count, "duplicate consumer.");
            Require(state.Businesses != null && state.Businesses.Count <= 100
                && state.Businesses.All(b => b != null && Text(b.Id) && Text(b.PlaceId) && Text(b.Name)
                    && Text(b.ServiceEvidence) && Time(b.NextServiceMinute)
                    && b.DistrictId != null && b.DistrictId.Length <= 1024 && Percent(b.CustomerServicePercent) && Text(b.CustomerServiceEvidence)
                    && (!b.MealsPerHour.HasValue || b.MealsPerHour >= 0 && b.MealsPerHour <= 10000)
                    && b.Hours != null && b.Hours.Count <= 64 && b.Hours.All(h => h != null && h.Day >= 0 && h.Day <= 6
                        && h.FromMinute >= 0 && h.FromMinute < 1440 && h.ToMinute >= 0 && h.ToMinute <= 1440 && h.FromMinute != h.ToMinute)
                    && b.Offers != null && b.Offers.Count <= 32 && b.Offers.All(o => o != null && Text(o.ProductId)
                        && o.Price > Money.Zero && o.Price <= Money.FromCents(1000000) && (!o.Units.HasValue || o.Units >= 0 && o.Units <= 1000000)
                        && Percent(o.DistrictDemandPercent) && Text(o.DistrictDemandEvidence)
                        && (!o.DistrictDemandPercent.HasValue || Text(b.DistrictId)))
                    && b.Offers.Select(o => o.ProductId).Distinct(StringComparer.Ordinal).Count() == b.Offers.Count), "invalid businesses.");
            Require(state.Businesses!.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() == state.Businesses!.Count, "duplicate business.");
            var people = state.Consumers!.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var shops = state.Businesses!.ToDictionary(b => b.Id, StringComparer.Ordinal);
            Require(state.TravelQuotes != null && state.TravelQuotes.Count <= 500000
                && state.TravelQuotes.All(q => q != null && people.ContainsKey(q.ConsumerId) && shops.ContainsKey(q.BusinessId)
                    && Text(q.Evidence) && q.IntentId != null && q.OriginPlaceId != null
                    && (!q.Minutes.HasValue || Position.Finite(q.Minutes.Value) && q.Minutes.Value >= 0 && q.Minutes.Value <= 1440)
                    && (!q.ReturnMinutes.HasValue || Position.Finite(q.ReturnMinutes.Value) && q.ReturnMinutes.Value >= 0 && q.ReturnMinutes.Value <= 1440)
                    && (routine ? Text(q.IntentId) && Text(q.OriginPlaceId) : q.IntentId == "" && q.OriginPlaceId == "" && !q.ReturnMinutes.HasValue)), "invalid routes.");
            Require(state.TravelQuotes!.Select(q => Key(routine ? q.IntentId : q.ConsumerId, q.BusinessId)).Distinct(StringComparer.Ordinal).Count() == state.TravelQuotes!.Count, "duplicate route quote.");
            Require(state.Intents != null && state.Intents.Count == people.Count * (int)((state.EndsAt - state.StartedAt) / 1440), "missing intentions.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var intent in state.Intents!)
            {
                Require(intent != null && people.ContainsKey(intent.ConsumerId), "unknown intent owner.");
                var person = people[intent!.ConsumerId];
                Require(intent.Day >= state.StartedAt / 1440 && intent.Day < state.EndsAt / 1440
                    && intent.Id == IntentId(intent.ConsumerId, intent.Day) && seen.Add(intent.Id)
                    && (routine ? Time(intent.DesiredAt) && intent.DesiredAt >= intent.Day * 1440.0
                        && intent.DesiredAt <= intent.RequestedAt && intent.RequestedAt < (intent.Day + 1) * 1440.0
                        && intent.Deadline > intent.RequestedAt && intent.Deadline <= Math.Min(state.EndsAt, intent.DesiredAt + 240)
                        && intent.EatingMinutes >= 5 && intent.EatingMinutes <= 60
                        && intent.MaximumQueueMinutes >= 0 && intent.MaximumQueueMinutes <= 60
                        : intent.RequestedAt == intent.Day * 1440.0 + person.MealMinuteOfDay
                        && intent.Deadline == Math.Min((intent.Day + 1) * 1440.0, intent.RequestedAt + person.MealWindowMinutes))
                    && Time(intent.RequestedAt) && Time(intent.Deadline) && intent.OriginPlaceId != null && intent.OriginPlaceId.Length <= 1024
                    && Text(intent.ContextEvidence) && Enum.IsDefined(typeof(Activity), intent.OriginActivity)
                    && Position.Finite(intent.TimeCostMultiplier) && intent.TimeCostMultiplier >= 1 && intent.TimeCostMultiplier <= 3
                    && Enum.IsDefined(typeof(FoodIntentStatus), intent.Status), "invalid intention identity/window.");
                Require(Enum.IsDefined(typeof(FoodOutsideOption), intent.OutsideOption) && Text(intent.OutsideOptionEvidence)
                    && (intent.OutsideOption == FoodOutsideOption.None ? !intent.OutsideOptionScoreCents.HasValue
                        : routine && intent.OriginPlaceId!.Length > 0 && intent.OutsideOptionScoreCents >= 0 && intent.OutsideOptionScoreCents <= 1000000
                            && (intent.OutsideOption == FoodOutsideOption.HomePreparation
                                ? intent.OriginActivity == Activity.Home && intent.OriginPlaceId == person.OriginPlaceId
                                : intent.OutsideOption == FoodOutsideOption.FundedHouseholdFood
                                    || intent.OriginActivity == Activity.Work || intent.OriginActivity == Activity.Study)), "invalid outside option.");
                Require(intent.Status != FoodIntentStatus.DeclinedPurchase || intent.OutsideOption != FoodOutsideOption.None
                    && intent.BusinessId == "" && intent.ProductId == "" && !intent.ArrivalMinute.HasValue
                    && !intent.ServiceEndsAt.HasValue && !intent.ReturnBy.HasValue, "declined purchase committed a trip or service.");
                if (!routine) Require(intent.Status != FoodIntentStatus.WaitingForService && intent.DesiredAt == 0 && intent.EatingMinutes == 0 && intent.MaximumQueueMinutes == 0
                    && intent.TimeCostMultiplier == 1 && intent.OriginPlaceId == "" && !intent.ReturnTravelMinutes.HasValue
                    && !intent.ServiceEndsAt.HasValue && !intent.ReturnBy.HasValue, "routine fields in fixed reference.");
                Require(intent.Spent >= Money.Zero && intent.Spent <= person.DailyAllowance
                    && (intent.Status == FoodIntentStatus.Fulfilled ? intent.Spent > Money.Zero : intent.Spent == Money.Zero), "invalid hypothetical spend.");
                Require(intent.Choices != null && intent.Choices.Count <= 3200 && intent.Choices.All(c => c != null
                    && shops.ContainsKey(c.BusinessId) && shops[c.BusinessId].Offers.Any(o => o.ProductId == c.ProductId)
                    && c.Price > Money.Zero && c.Price <= Money.FromCents(1000000) && Text(c.Reason)
                    && Percent(c.CustomerServicePercent) && Percent(c.DistrictDemandPercent)
                    && Position.Finite(c.ServiceQualityAdjustmentCents) && Math.Abs(c.ServiceQualityAdjustmentCents) <= person.ServiceQualityValueCents / 2.0
                    && Position.Finite(c.DistrictAppealAdjustmentCents) && Math.Abs(c.DistrictAppealAdjustmentCents) <= person.DistrictAppealValueCents / 2.0
                    && (c.CustomerServicePercent.HasValue || c.ServiceQualityAdjustmentCents == 0)
                    && (c.DistrictDemandPercent.HasValue || c.DistrictAppealAdjustmentCents == 0)
                    && (!c.TravelMinutes.HasValue || Time(c.TravelMinutes.Value))
                    && (!c.AdmissionMinute.HasValue || Time(c.AdmissionMinute.Value))
                    && (!c.ReturnMinutes.HasValue || Time(c.ReturnMinutes.Value) && c.ReturnMinutes <= 1440)
                    && (!c.ServiceEndsAt.HasValue || Time(c.ServiceEndsAt.Value))
                    && (!c.ScoreCents.HasValue || Position.Finite(c.ScoreCents.Value))), "invalid choice explanation.");
                if (intent.Status == FoodIntentStatus.Planned)
                    Require(intent.RequestedAt >= state.Minute && intent.BusinessId == "" && intent.ProductId == ""
                        && !intent.ArrivalMinute.HasValue && intent.Choices!.Count == 0 && intent.Outcome == "", "invalid planned intention.");
                else
                {
                    Require(intent.RequestedAt <= state.Minute, "intention began in the future.");
                    Require(Terminal(intent) ? Text(intent.Outcome) : intent.Outcome == "", "invalid outcome.");
                    if (intent.ArrivalMinute.HasValue)
                        Require(Time(intent.ArrivalMinute.Value) && intent.ArrivalMinute.Value >= intent.RequestedAt
                            && intent.ArrivalMinute.Value < intent.Deadline && shops.ContainsKey(intent.BusinessId)
                            && shops[intent.BusinessId].Offers.Any(o => o.ProductId == intent.ProductId)
                            && (intent.Status == FoodIntentStatus.Travelling ? intent.ArrivalMinute >= state.Minute : intent.ArrivalMinute <= state.Minute), "invalid committed arrival.");
                    else Require((intent.Status == FoodIntentStatus.Unmet || intent.Status == FoodIntentStatus.Unresolved || intent.Status == FoodIntentStatus.DeclinedPurchase)
                        && intent.BusinessId == "" && intent.ProductId == "", "missing committed arrival.");
                }
                if (routine)
                {
                    Require(intent.ArrivalMinute.HasValue == intent.ReturnTravelMinutes.HasValue
                        && (!intent.ReturnTravelMinutes.HasValue || Time(intent.ReturnTravelMinutes.Value) && intent.ReturnTravelMinutes <= 1440), "invalid return route.");
                    Require(intent.ServiceEndsAt.HasValue == intent.ReturnBy.HasValue, "incomplete service reservation.");
                    if (intent.ServiceEndsAt.HasValue)
                        Require(intent.ArrivalMinute.HasValue && Time(intent.ServiceEndsAt.Value) && intent.ServiceEndsAt > intent.ArrivalMinute
                            && intent.ReturnBy == intent.ServiceEndsAt + intent.EatingMinutes + intent.ReturnTravelMinutes
                            && intent.ReturnBy <= intent.Deadline && shops[intent.BusinessId].NextServiceMinute >= intent.ServiceEndsAt
                            && (intent.Status == FoodIntentStatus.WaitingForService ? intent.ServiceEndsAt >= state.Minute
                                : Terminal(intent) && intent.ServiceEndsAt <= state.Minute), "invalid service completion/return.");
                    else Require(intent.Status != FoodIntentStatus.WaitingForService && intent.Status != FoodIntentStatus.Fulfilled, "missing service completion.");
                }
            }
            if (routine)
            {
                var intentions = state.Intents!.ToDictionary(i => i.Id, StringComparer.Ordinal);
                Require(state.TravelQuotes!.All(q => intentions.TryGetValue(q.IntentId, out var i)
                    && q.ConsumerId == i.ConsumerId && q.OriginPlaceId == i.OriginPlaceId), "route quote belongs to a different opportunity/origin.");
            }
        }
    }
}
