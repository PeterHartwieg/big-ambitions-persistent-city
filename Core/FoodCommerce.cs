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
        public int RouteCalls { get; set; }
        public double ElapsedMilliseconds { get; set; }
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
        public string ProductId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        public double NextServiceAt { get; set; }
    }
    public sealed class ResidentMeal
    {
        public string ResidentId { get; set; } = "";
        public int Day { get; set; }
        public double DesiredAt { get; set; }
        public double ExpiresAt { get; set; }
        public double DueAt { get; set; }
        public MealStatus Status { get; set; }
        public string PlaceId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string Reason { get; set; } = "";
        public int AllowanceCents { get; set; }
        public int PriceCents { get; set; }
        public double ServiceMinutes { get; set; }
        public double ServiceAt { get; set; }
        public double Deadline { get; set; }
        public double ReturnMinutes { get; set; }
        public long Purchases { get; set; }
        public long SpentCents { get; set; }
        public string IntentId => FoodDemand.IntentId(ResidentId, Day);
    }
    public sealed class FoodCommerceState
    {
        public List<FoodScope> Shops { get; set; } = new List<FoodScope>();
        public List<ResidentMeal> Residents { get; set; } = new List<ResidentMeal>();
        public List<FoodDistrictBudgetMix> Districts { get; set; } = new List<FoodDistrictBudgetMix>();
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

    /// <summary>One discretionary midday purchase opportunity per resident/day. No nutrition or
    /// household-income claim. Events share the population clock and its bounded frame budget.</summary>
    public sealed class FoodCommerce
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
        public double NextMinute => due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt;
        internal IFoodMarket Market => market;
        internal FoodCommerce(CitySimulator city, IFoodMarket market, IRoutePlanner routes)
        {
            this.city = city; this.market = market; this.routes = routes;
            state = city.State.Food ?? throw new InvalidOperationException("No food state.");
            records = state.Residents.ToDictionary(r => r.ResidentId, StringComparer.Ordinal);
            foreach (var person in city.State.Citizens) Enrol(person);
            foreach (var entry in state.Residents)
            {
                if (entry.DueAt < city.State.Minute) throw new InvalidOperationException("Overdue meal record.");
                due.Add(entry);
                if (Active(entry)) Reserve(entry.PlaceId, 1);
            }
        }
        private static bool Active(ResidentMeal m) => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving;
        private void Reserve(string shop, int delta)
        { reservations.TryGetValue(shop, out int count); reservations[shop] = count + delta; }
        internal void Replace(string oldId, CitizenState person)
        {
            if (oldId == person.Identity.Id) return;
            if (records.TryGetValue(oldId, out var old))
            { due.Remove(old); records.Remove(oldId); state.Residents.Remove(old); if (Active(old)) Reserve(old.PlaceId, -1); }
            var added = Enrol(person); due.Add(added);
        }
        private ResidentMeal Enrol(CitizenState person)
        {
            if (records.TryGetValue(person.Identity.Id, out var existing)) return existing;
            var resident = Resident(person);
            var consumer = Consumer(person, 0);
            FoodHouseholdPolicy.SetAllowance(consumer, resident, city.State.Seed,
                state.Districts.SingleOrDefault(d => d.DistrictId == resident.HomeDistrictId), null);
            var meal = new ResidentMeal { ResidentId = person.Identity.Id, AllowanceCents = consumer.DailyAllowanceCents };
            StartDay(meal, person, (int)(city.State.Minute / 1440));
            records.Add(meal.ResidentId, meal); state.Residents.Add(meal); return meal;
        }
        private void StartDay(ResidentMeal meal, CitizenState person, int day)
        {
            meal.Day = day; meal.DesiredAt = FoodRoutinePlanning.DesiredMealMinute(person, city.State.Seed, day);
            meal.ExpiresAt = Math.Min((day + 1) * 1440.0, meal.DesiredAt + 240);
            meal.DueAt = Math.Max(city.State.Minute, meal.DesiredAt);
            meal.Status = MealStatus.Pending; meal.Reason = ""; meal.PlaceId = meal.BusinessId = meal.ProductId = "";
            meal.PriceCents = 0; meal.ServiceAt = meal.ServiceMinutes = meal.ReturnMinutes = meal.Deadline = 0;
        }
        internal void Step()
        {
            var meal = due.Min!; due.Remove(meal);
            var person = city.FindCitizen(meal.ResidentId);
            if (person == null) throw new InvalidOperationException("Meal has no resident.");
            double now = city.State.Minute;
            if (!Active(meal) && meal.Status != MealStatus.Pending)
                StartDay(meal, person, Math.Max(meal.Day + 1, (int)(now / 1440)));
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
                else if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                    || Math.Min(person.NextEventMinute, person.ActivityUntilMinute) - now < 25)
                    meal.DueAt = Math.Min(meal.ExpiresAt, Math.Max(now + 1, person.NextEventMinute));
                else Decide(meal, person);
            }
            else if (meal.Status == MealStatus.Travelling)
            {
                bool admitted = person.MealOuting != null && !person.MealOuting.Returning
                    && person.Phase == CitizenPhase.AtPlace && person.PlaceId == meal.PlaceId
                    && person.RecentVisits.Any(v => v.PlaceId == meal.PlaceId && v.Activity == Activity.Shop && v.AtMinute >= meal.DesiredAt);
                if (admitted)
                {
                    var available = market.Read(now).SingleOrDefault(b => b.Id == meal.BusinessId && b.PlaceId == meal.PlaceId);
                    if (available?.MealsPerHour == null || available.MealsPerHour <= 0)
                    { Finish(meal, MealStatus.Missed, "Service unavailable on arrival"); due.Add(meal); return; }
                    meal.ServiceMinutes = 60.0 / available.MealsPerHour.Value;
                    var scope = state.Shops.Single(s => s.PlaceId == meal.PlaceId);
                    meal.ServiceAt = Math.Max(now, scope.NextServiceAt) + meal.ServiceMinutes;
                    if (meal.ServiceAt + 15 + meal.ReturnMinutes > meal.Deadline)
                        Finish(meal, MealStatus.Missed, "Arrival or queue no longer fits the break");
                    else
                    {
                        city.ExtendMealStay(person, meal.ServiceAt + 15);
                        scope.NextServiceAt = meal.ServiceAt;
                        meal.Status = MealStatus.Serving; meal.DueAt = meal.ServiceAt;
                    }
                }
                else if (person.MealOuting == null || person.MealOuting.Returning || now >= meal.Deadline)
                    Finish(meal, MealStatus.Missed, "Visit was interrupted before admission");
                else meal.DueAt = Math.Max(now + .001, person.NextEventMinute);
            }
            else if (meal.Status == MealStatus.Serving)
            {
                if (person.Phase != CitizenPhase.AtPlace || person.PlaceId != meal.PlaceId
                    || person.MealOuting == null || person.MealOuting.Returning)
                    Finish(meal, MealStatus.Missed, "Resident left before service");
                else
                {
                    // Persist uncertainty before entering native code. No exception can trigger a replay.
                    Finish(meal, MealStatus.Uncertain, "Native purchase did not confirm completion");
                    try
                    {
                        var result = market.Buy(meal, now);
                        meal.Status = result.Paid ? MealStatus.Paid : MealStatus.Missed; meal.Reason = result.Reason;
                        if (result.Paid) { meal.Purchases++; meal.SpentCents += meal.PriceCents; }
                    }
                    catch (Exception) { /* Retain uncertain ownership; never issue a second order. */ }
                }
            }
            due.Add(meal);
        }
        private void Finish(ResidentMeal meal, MealStatus status, string reason)
        {
            if (Active(meal)) Reserve(meal.PlaceId, -1);
            meal.Status = status; meal.Reason = reason;
            var person = city.FindCitizen(meal.ResidentId)!;
            meal.DueAt = Math.Max(city.State.Minute + .001, FoodRoutinePlanning.DesiredMealMinute(person, city.State.Seed, meal.Day + 1));
        }
        private FoodRoutineResident Resident(CitizenState p) => new FoodRoutineResident { Identity = p.Identity, HomeId = p.HomeId,
            HomeDistrictId = city.FindPlace(p.HomeId).District, WalkingToleranceMetres = p.Preferences.WalkingToleranceMetres };
        private FoodConsumer Consumer(CitizenState p, int allowance) => new FoodConsumer { Id = p.Identity.Id, Name = p.Identity.Name,
            OriginPlaceId = p.PlaceId, DailyAllowanceCents = allowance,
            ValueOfTimeCentsPerMinute = 4 + (int)(StableRandom.Hash(p.Identity.Id + ":food-time", city.State.Seed) % 9),
            ServiceQualityValueCents = 100 + (int)(StableRandom.Hash(p.Identity.Id + ":food-quality", city.State.Seed) % 301),
            DistrictAppealValueCents = 50 + (int)(StableRandom.Hash(p.Identity.Id + ":food-district", city.State.Seed) % 151) };
        private void Decide(ResidentMeal meal, CitizenState person)
        {
            long started = city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var trace = city.ObservingFood ? new FoodDecisionTrace() : null;
            double now = city.State.Minute;
            var consumer = Consumer(person, meal.AllowanceCents);
            bool work = person.Activity == Activity.Work || person.Activity == Activity.Study;
            var intent = new FoodIntent { Id = meal.IntentId, ConsumerId = meal.ResidentId, Day = meal.Day,
                DesiredAt = meal.DesiredAt, RequestedAt = now, Deadline = Math.Min(meal.ExpiresAt,
                    Math.Min(now + FoodRoutinePlanning.BreakMinutes(meal.ResidentId, city.State.Seed, person.Activity),
                        Math.Min(person.NextEventMinute, person.ActivityUntilMinute))),
                OriginPlaceId = person.PlaceId, OriginActivity = person.Activity, EatingMinutes = 15, MaximumQueueMinutes = work ? 10 : 15,
                TimeCostMultiplier = 1 + Math.Min(1, (now - meal.DesiredAt) / 120), ContextEvidence = "Live scheduled presence and departure deadline" };
            FoodHouseholdPolicy.SetOutsideOption(consumer, Resident(person), intent, city.State.Seed);
            var businesses = market.Read(now).Where(b => state.Shops.Any(s => s.PlaceId == b.PlaceId && s.BusinessId == b.Id)).ToList();
            var quotes = new List<FoodTravelQuote>();
            var refusals = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new Dictionary<string, Route>(StringComparer.Ordinal);
            var preferences = new TravelPreferences { AllowTransit = false, OwnsCar = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
            foreach (var b in businesses)
            {
                var scope = state.Shops.Single(s => s.PlaceId == b.PlaceId);
                b.NextServiceMinute = Math.Max(now, scope.NextServiceAt);
                reservations.TryGetValue(b.PlaceId, out int reserved);
                foreach (var offer in b.Offers) if (offer.Units.HasValue) offer.Units = Math.Max(0, offer.Units.Value - reserved);
                var origin = city.FindPlace(person.PlaceId); var shop = city.FindPlace(b.PlaceId);
                if (Position.Distance(origin.Position, shop.Position) > preferences.WalkingToleranceMetres)
                { refusals[b.Id] = "Beyond walking tolerance"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                // A safe geometric bound saves native path searches for distant shops. It is
                // not a route quote: feasible candidates still need complete outward/return paths.
                if (routes is IWalkingTimeBound bound && b.MealsPerHour > 0
                    && now + bound.MinimumWalkingMinutes(origin, shop) + bound.MinimumWalkingMinutes(shop, origin)
                        + 60.0 / b.MealsPerHour.Value + intent.EatingMinutes > intent.Deadline)
                { refusals[b.Id] = "Travel and service cannot fit break"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                if (trace != null) trace.RouteCalls++;
                var outward = routes.Plan(origin, shop, now, preferences);
                if (trace != null && outward != null) trace.RouteCalls++;
                var back = outward == null ? null : routes.Plan(shop, origin, now + outward.Minutes, preferences);
                if (outward == null || back == null || outward.Legs.Any(l => l.Mode != TravelMode.Walk) || back.Legs.Any(l => l.Mode != TravelMode.Walk))
                { if (trace != null) trace.RouteChecks[b.Id] = outward == null ? "No outward walking path" : back == null ? "No return walking path" : "Non-walking path"; continue; }
                if (trace != null) trace.RouteChecks[b.Id] = "Complete walking paths";
                paths.Add(b.Id, outward);
                quotes.Add(new FoodTravelQuote { ConsumerId = meal.ResidentId, BusinessId = b.Id, IntentId = meal.IntentId,
                    OriginPlaceId = person.PlaceId, Minutes = outward.Minutes, ReturnMinutes = back.Minutes, Evidence = "Current complete walking paths" });
            }
            FoodDemand.ChooseObserved(consumer, businesses, quotes, intent, now, refusals);
            var choice = intent;
            if (trace != null)
            {
                trace.Consumer = consumer; trace.Intent = choice; trace.Businesses = businesses; trace.Quotes = quotes;
                trace.ElapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                city.ObserveFood(trace);
            }
            if (choice.Status != FoodIntentStatus.Travelling)
            { Finish(meal, choice.Status == FoodIntentStatus.DeclinedPurchase ? MealStatus.Declined : MealStatus.Missed, choice.Outcome); return; }
            var selected = businesses.Single(b => b.Id == choice.BusinessId);
            var selectedChoice = choice.Choices.Single(c => c.BusinessId == choice.BusinessId && c.ProductId == choice.ProductId);
            double stay = selectedChoice.ServiceEndsAt!.Value - (now + paths[selected.Id].Minutes) + 15;
            if (!city.TryBeginMealVisit(person.Identity.Id, selected.PlaceId, paths[selected.Id], choice.ReturnTravelMinutes!.Value, stay, intent.Deadline))
            { Finish(meal, MealStatus.Missed, "Schedule or shop changed before departure"); return; }
            meal.PlaceId = selected.PlaceId; meal.BusinessId = selected.Id; meal.ProductId = choice.ProductId;
            meal.PriceCents = selectedChoice.PriceCents; meal.ServiceMinutes = 60.0 / selected.MealsPerHour!.Value;
            meal.ReturnMinutes = choice.ReturnTravelMinutes.Value; meal.Deadline = intent.Deadline;
            meal.Status = MealStatus.Travelling; meal.DueAt = person.NextEventMinute;
            Reserve(meal.PlaceId, 1);
        }
    }
}
