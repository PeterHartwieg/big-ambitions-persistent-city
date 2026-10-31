using System;
using System.Linq;

namespace PersistentCity.Core
{
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
            var business = CapturedBusiness(shop);
            var hours = new Place { Kind = PlaceKind.Shop, WeeklyHours = business.Hours, OpenMinute = 0, CloseMinute = 0 };
            ShopExperience? cause = !DailyRoutine.Open(hours, decision.OriginAt) ? ShopExperience.Closed
                : shop.Offers.Count > 0 && shop.Offers.All(o => o.Units == 0) ? ShopExperience.Stockout
                : shop.NextServiceAt - decision.OriginAt > MealRhythm.QueueMinutes(meal.Kind, Activity.Shop) ? ShopExperience.Queue : (ShopExperience?)null;
            if (!cause.HasValue) return;
            meal.BusinessId = shop.BusinessId; meal.PlaceId = shop.PlaceId;
            meal.ProductId = shop.Offers.Count > 0 ? shop.Offers[0].ProductId : state.Shops.First(s => s.BusinessId == shop.BusinessId).ProductId;
            memoryStore.CommitStage(meal, ExperienceStage.Committed);
            memoryStore.CommitStage(meal, ExperienceStage.Reached);
            Experience(meal, cause.Value, decision.OriginAt);
        }

        internal object? DumpMemory(string consumerId)
        {
            if (!records.TryGetValue(consumerId, out var meal)) return null;
            int day = (int)(city.State.Minute / 1440);
            var traits = Traits(meal, city.FindCitizen(meal.AnchorId)!);
            return new { ConsumerId = consumerId, Day = day, Active = city.State.Capabilities.Active,
                PolicyRevision = 1, memoryStore.CapacityMisses,
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
