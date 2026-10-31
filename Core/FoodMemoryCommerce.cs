using System;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>On-demand current evidence at a reached shop. Never books sales or routes;
    /// native implementations bypass any stale decision-supply cache.</summary>
    public interface IFoodFailureMarket
    {
        FoodBusiness? ReadFailureBusiness(string businessId, string placeId, double minute);
    }

    public sealed partial class FoodCommerce
    {
        internal FoodMemorySettings MemorySettings { get; }
        private readonly FoodMemoryStore memoryStore;

        private double MemoryContribution(ResidentMeal meal, CitizenState person, string business)
            => city.State.Capabilities.Active && MemorySettings.FoodMemoryWeight != 0 ? FoodMemoryPolicy.Contribution(meal.Memory, memoryStore.Find(business),
                (int)(city.State.Minute / 1440), Traits(meal, person), MemorySettings) : 0;

        private void Experience(ResidentMeal meal, ShopExperience cause, double at)
        {
            if (city.State.Capabilities.Active)
                memoryStore.Experience(meal, cause, (int)(at / 1440), Traits(meal, city.FindCitizen(meal.AnchorId)!), MemorySettings);
        }

        private FoodBusiness? FailureEvidence(string businessId, string placeId, double minute, FoodBusiness? observed = null)
        {
            try { return market is IFoodFailureMarket evidence ? evidence.ReadFailureBusiness(businessId, placeId, minute)
                : observed ?? ReadBusiness(businessId, minute); }
            catch (Exception) { return null; } // Unavailable extra proof stays neutral; keep the original refusal.
        }

        // Only exact, causal arrival evidence. Generic "unavailable", interrupted breaks and routes stay neutral.
        private void FailedVisit(ResidentMeal meal, string reason)
        {
            ShopExperience? cause = reason == "Shop closed before admission" || reason == "Service no longer fits shop opening hours"
                ? ShopExperience.Closed : reason == "Queue exceeds patience on arrival" ? ShopExperience.Queue : (ShopExperience?)null;
            if (cause.HasValue) Experience(meal, cause.Value, city.State.Minute);
        }

        private void FailedExistingStop(ResidentMeal meal, CitizenState person, FoodDecisionState decision)
        {
            if (!city.State.Capabilities.Active || decision.Shops.Count != 1 || person.PlaceId != decision.Shops[0].PlaceId) return;
            var shop = decision.Shops[0];
            if (!shop.Offers.Any(o => o.Price > Money.Zero && o.Price <= AvailableForMeal(meal))) return;
            var business = CapturedBusiness(shop);
            var hours = new Place { Kind = PlaceKind.Shop, WeeklyHours = business.Hours, OpenMinute = 0, CloseMinute = 0 };
            bool stockout = !shop.ServiceUnavailable && shop.MealsPerHour > 0 && shop.Offers.Count > 0 && shop.Offers.All(o => o.Units == 0);
            if (!stockout && shop.ServiceUnavailable && shop.MealsPerHour == 0 && shop.Offers.Count > 0 && shop.Offers.All(o => o.Units == 0))
            {
                // Held images intentionally retain only a generic service flag. Revalidate the
                // named bag-stockout evidence at this reached shop; no routes or checkout.
                var live = FailureEvidence(shop.BusinessId, shop.PlaceId, decision.OriginAt);
                stockout = live?.Id == shop.BusinessId && live.PlaceId == shop.PlaceId && live.ServiceUnavailableReason == "Out of stock"
                    && live.Offers.Any(o => o.Units == 0 && o.Price > Money.Zero && o.Price <= AvailableForMeal(meal));
            }
            ShopExperience? cause = !DailyRoutine.Open(hours, decision.OriginAt) ? ShopExperience.Closed
                : stockout ? ShopExperience.Stockout
                : decision.Reason == MealReasonCode.QueueTooLong && !shop.ServiceUnavailable && shop.MealsPerHour > 0
                    && decision.OriginAt + 60 / shop.MealsPerHour + MealRhythm.EatingMinutes(meal.Kind) <= decision.Deadline
                    && decision.OriginAt + 60 / shop.MealsPerHour < (DailyRoutine.NextClose(hours, decision.OriginAt) ?? double.NegativeInfinity)
                    ? ShopExperience.Queue : (ShopExperience?)null;
            if (!cause.HasValue) return;
            memoryStore.CommitStage(meal, ExperienceStage.Committed, shop.BusinessId);
            memoryStore.CommitStage(meal, ExperienceStage.Reached, shop.BusinessId);
            memoryStore.Experience(meal, cause.Value, (int)(decision.OriginAt / 1440), Traits(meal, person), MemorySettings, shop.BusinessId, shop.PlaceId);
        }

        internal object? DumpMemory(string consumerId)
        {
            if (!records.TryGetValue(consumerId, out var meal)) return null;
            int day = (int)(city.State.Minute / 1440);
            var traits = Traits(meal, city.FindCitizen(meal.AnchorId)!);
            return new { ConsumerId = consumerId, Day = day, Active = city.State.Capabilities.Active,
                PolicyRevision = 1, memoryStore.CapacityMisses, memoryStore.ProtectedInsertions,
                Watermark = meal.Memory.Watermark == null ? null : new ExperienceWatermark { Day = meal.Memory.Watermark.Day,
                    Meal = meal.Memory.Watermark.Meal, Context = meal.Memory.Watermark.Context, Stage = meal.Memory.Watermark.Stage },
                Entries = meal.Memory.Entries.Select(e => new { BusinessId = memoryStore.Identity(e.BusinessRef), e.BusinessRef,
                    e.Day, e.Served, e.Affinity, e.Outcome, EffectiveAffinity = FoodMemoryPolicy.EffectiveAffinity(e, day, traits, MemorySettings),
                    Expired = FoodMemoryPolicy.Expired(e, day, MemorySettings) }).ToArray() };
        }
    }
    public sealed partial class CitySimulator
    {
        /// <summary>Detached read-only diagnostic, allocating only when called; no market, route or UI work.</summary>
        public object? DumpFoodMemory(string consumerId) => food?.DumpMemory(consumerId);
    }
}
