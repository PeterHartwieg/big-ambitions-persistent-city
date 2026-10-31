using System;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class FoodCommerce
    {
        private bool AtFoodStop(CitizenState person) => person.Phase == CitizenPhase.AtPlace
            && person.Activity == Activity.Shop && person.MealOuting == null
            && city.State.Minute < Math.Min(person.NextEventMinute, person.ActivityUntilMinute)
            && DailyRoutine.Open(city.FindPlace(person.PlaceId), city.State.Minute)
            && state.Shops.Any(s => s.PlaceId == person.PlaceId);

        // An observed activity transition wakes only this resident. No population scan or
        // per-frame market query. A declined or failed earlier offer does not consume the
        // prepared-meal intention; a later, better-context offer may still satisfy it.
        internal void ObserveStop(CitizenState person)
        {
            CaptureRoutineStop(person);
            if (city.State.Capabilities.Active && records.TryGetValue(person.Identity.Id, out var arriving) && arriving.Status == MealStatus.Travelling
                && person.PlaceId == arriving.PlaceId && person.MealOuting?.Returning == true)
            {
                // The native place-hours admission may have refused before a RecentVisit was added.
                // Reaching the committed door plus named closed hours is evidence; a lost route is not.
                var business = ReadBusiness(arriving.BusinessId, city.State.Minute);
                if (business != null && business.PlaceId == arriving.PlaceId && business.Id == arriving.BusinessId
                    && !DailyRoutine.Open(business.OpeningPlace, city.State.Minute))
                {
                    due.Remove(arriving);
                    try { Finish(arriving, MealStatus.Missed, "Shop closed before admission"); }
                    finally { due.Add(arriving); }
                }
            }
            // A brief home stay can fall entirely between meal windows. Rejoin idle
            // represented consumers when that stay is observed, not at their next meal.
            // An unfinished excursion or uncertain checkout keeps its own ownership.
            if (state.RepresentationFactor > 1 && ResidentLifecycle.IsSafelyHome(person))
                for (int slot = 1; slot < state.RepresentationFactor; slot++)
                    if (records.TryGetValue(FoodRepresentation.ConsumerId(person.Identity.Id, slot), out var represented)
                        && represented.Excursion == null) represented.NeedsHomeResync = false;
            if (!records.TryGetValue(person.Identity.Id, out var meal) || !AtFoodStop(person)
                || (meal.Status != MealStatus.Pending && meal.Status != MealStatus.Missed && meal.Status != MealStatus.Declined)
                || city.State.Minute >= meal.ExpiresAt) return;
            if(NewChoice(meal) && meal.LastContextKey==Context(ChoiceStop(meal,person),true)) return;
            double at = Math.Max(city.State.Minute, meal.DesiredAt);
            if (at >= Math.Min(person.NextEventMinute, person.ActivityUntilMinute)) return;
            due.Remove(meal);
            meal.Status = MealStatus.Pending;
            meal.DueAt = at;
            due.Add(meal);
        }

        private void DecideAtStop(ResidentMeal meal, CitizenState person)
        {
            if (NewChoice(meal)) { BeginFoodDecision(meal,person,true); return; }
            city.CancelOptionalStop(person.Identity.Id);
            double now = city.State.Minute;
            var consumer = Consumer(person, AvailableForMeal(meal), meal);
            MealRhythm.Apply(consumer, meal.Kind);
            var intent = new FoodIntent { Id = meal.IntentId, ConsumerId = meal.ResidentId, Day = meal.Day,
                DesiredAt = meal.DesiredAt, RequestedAt = now, OriginPlaceId = person.PlaceId, OriginActivity = Activity.Shop,
                Deadline = Math.Min(meal.ExpiresAt, Math.Min(person.NextEventMinute, person.ActivityUntilMinute)),
                EatingMinutes = MealRhythm.EatingMinutes(meal.Kind), MaximumQueueMinutes = MealRhythm.QueueMinutes(meal.Kind, Activity.Shop),
                TimeCostMultiplier = 1 + Math.Min(1, (now - meal.DesiredAt) / 120),
                ContextEvidence = meal.Kind + ": already admitted at an enrolled shop; service and eating fit the existing visit, with no extra journey" };
            SetFoodAlternative(meal, person, consumer, intent);
            var scope = state.Shops.Single(s => s.PlaceId == person.PlaceId);
            var observed = ReadBusiness(scope.BusinessId, now);
            var businesses = new System.Collections.Generic.List<FoodBusiness>();
            if (observed != null && observed.PlaceId == person.PlaceId) businesses.Add(observed);
            foreach (var business in businesses)
            {
                business.NextServiceMinute = Math.Max(now, state.Shops.Single(s => s.PlaceId == business.PlaceId).NextServiceAt);
                ApplyReservations(business);
            }
            var quotes = businesses.Select(b => new FoodTravelQuote { ConsumerId = meal.ResidentId,
                BusinessId = b.Id, IntentId = meal.IntentId, OriginPlaceId = person.PlaceId,
                Minutes = 0, ReturnMinutes = 0, Evidence = "Resident is already present at this shop" }).ToArray();
            FoodDemand.ChooseObserved(consumer, businesses, quotes, intent, now, mealKind: meal.Kind);
            RecordChoices(intent, businesses);
            if (city.ObservingFood) city.ObserveFood(new FoodDecisionTrace {
                MealKind = meal.Kind, Consumer = consumer, Intent = intent, Businesses = businesses, Quotes = quotes });
            if (intent.Status != FoodIntentStatus.Travelling)
            { Finish(meal, intent.Status == FoodIntentStatus.DeclinedPurchase ? MealStatus.Declined : MealStatus.Missed, "Existing stop: " + intent.Outcome, intent.OutsideOption); return; }
            var selected = businesses.Single(b => b.Id == intent.BusinessId);
            var choice = intent.Choices.Single(c => c.BusinessId == intent.BusinessId && c.ProductId == intent.ProductId);
            meal.ExistingStop = true;
            meal.PlaceId = selected.PlaceId; meal.BusinessId = selected.Id; meal.ProductId = intent.ProductId;
            meal.Price = choice.Price; meal.ServiceMinutes = 60.0 / selected.MealsPerHour!.Value;
            meal.ReturnMinutes = 0; meal.Deadline = intent.Deadline;
            meal.ServiceAt = choice.ServiceEndsAt!.Value;
            state.Shops.Single(s => s.PlaceId == selected.PlaceId).NextServiceAt = meal.ServiceAt;
            meal.Status = MealStatus.Serving; meal.DueAt = meal.ServiceAt;
            Reserve(meal, 1);
        }
    }
}
