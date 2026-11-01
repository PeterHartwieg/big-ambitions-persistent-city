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
            var hours = business.OpeningPlace;
            var allowance = AvailableForMeal(meal);
            bool affordableStockout = AffordableStockout(business, allowance);
            double finish = Math.Max(decision.OriginAt, shop.NextServiceAt) + (shop.MealsPerHour > 0 ? 60 / shop.MealsPerHour : 0);
            bool fitsVisit = Math.Max(0, shop.NextServiceAt - decision.OriginAt) <= MealRhythm.QueueMinutes(meal.Kind, Activity.Shop)
                && finish + MealRhythm.EatingMinutes(meal.Kind) <= decision.Deadline
                && finish < (DailyRoutine.NextClose(hours, decision.OriginAt) ?? double.NegativeInfinity);
            bool stockout = !shop.ServiceUnavailable && shop.MealsPerHour > 0 && affordableStockout && fitsVisit;
            if (!stockout && shop.ServiceUnavailable && shop.MealsPerHour == 0 && affordableStockout && fitsVisit
                && finish + MealRhythm.EatingMinutes(meal.Kind) < decision.Deadline)
            {
                // Held images intentionally retain only a generic service flag. Revalidate the
                // named bag-stockout evidence only if the necessary time/queue bounds fit.
                // Zero throughput hides service duration, so eating alone must leave spare time.
                var live = FailureEvidence(shop.BusinessId, shop.PlaceId, decision.OriginAt);
                stockout = live?.Id == shop.BusinessId && live.PlaceId == shop.PlaceId && live.ServiceUnavailableReason == "Out of stock"
                    && AffordableStockout(live, allowance);
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

        private static bool AffordableStockout(FoodBusiness business, Money allowance)
        {
            bool affordable = false;
            foreach (var offer in business.Offers)
                if (offer.Price > Money.Zero && offer.Price <= allowance)
                {
                    affordable = true;
                    if (offer.Units != 0) return false; // Unknown stock is not proof of exhaustion.
                }
            return affordable;
        }

        internal BusinessReference? MemoryReference(int slot) => memoryStore.Reference(slot);
        internal RegularsProjection Regulars(Func<FoodScope, bool>? currentOwnShop = null)
            => new RegularsProjection(state, (int)(city.State.Minute / 1440), (m, e, day) =>
            {
                var traits = m.RepresentedTraits ?? city.FindCitizen(m.AnchorId)?.Traits;
                return traits == null ? 0 : FoodMemoryPolicy.EffectiveAffinity(e, day, traits, MemorySettings);
            }, MemorySettings, currentOwnShop);

        internal object[] DumpRegulars(Func<FoodScope, bool>? currentOwnShop = null)
        {
            var projection = Regulars(currentOwnShop);
            return state.Shops.Where(s => currentOwnShop == null || currentOwnShop(s))
                .Select(s => (object)new { s.BusinessId, s.PlaceId, Regulars = projection.CountOwnShop(s.BusinessId) }).ToArray();
        }

        internal object? DumpMemory(string consumerId, Func<FoodScope, bool>? currentOwnShop = null)
        {
            if (!records.TryGetValue(consumerId, out var meal)) return null;
            int day = (int)(city.State.Minute / 1440);
            var traits = Traits(meal, city.FindCitizen(meal.AnchorId)!);
            var projection = Regulars(currentOwnShop);
            var own = state.Shops.Where(s => currentOwnShop == null || currentOwnShop(s)).ToDictionary(s => s.BusinessId, StringComparer.Ordinal);
            var entries = meal.Memory.Entries.Select(e =>
            {
                var reference = memoryStore.Reference(e.BusinessRef)!;
                bool ownCurrent = own.TryGetValue(reference.BusinessId, out var scope) && scope.PlaceId == reference.PlaceId;
                int effective = FoodMemoryPolicy.EffectiveAffinity(e, day, traits, MemorySettings);
                return new { BusinessId = reference.BusinessId, e.BusinessRef, e.Day, e.Served, e.Affinity, e.Outcome,
                    EffectiveAffinity = effective, OwnCurrent = ownCurrent,
                    Regular = ownCurrent && RegularsProjection.IsRegular(e, day, effective, MemorySettings),
                    Expired = FoodMemoryPolicy.Expired(e, day, MemorySettings) };
            }).ToArray();
            return new { ConsumerId = consumerId, Day = day, Active = city.State.Capabilities.Active,
                Represented = meal.Slot > 0,
                StoryKind = meal.Slot > 0 ? "Represented consumer; no physical body" : "Resident's own consumer",
                Words = ResidentMemoryWords.Capture(state, meal, traits, day, MemorySettings, memoryStore.Reference,
                    id => ResidentAddress.From(city.FindPlace(id)).Label, currentOwnShop),
                OwnShopRegulars = own.Values.Select(s => new { s.BusinessId, s.PlaceId, Regulars = projection.CountOwnShop(s.BusinessId) }).ToArray(),
                PolicyRevision = 1, memoryStore.CapacityMisses, memoryStore.ProtectedInsertions,
                Watermark = meal.Memory.Watermark == null ? null : new ExperienceWatermark { Day = meal.Memory.Watermark.Day,
                    Meal = meal.Memory.Watermark.Meal, Context = meal.Memory.Watermark.Context, Stage = meal.Memory.Watermark.Stage },
                Regular = entries.Any(e => e.Regular), Entries = entries };
        }
    }
    public sealed partial class CitySimulator
    {
        /// <summary>Detached read-only diagnostic, allocating only when called; no market, route or UI work.</summary>
        public object? DumpFoodMemory(string consumerId, Func<FoodScope, bool>? currentOwnShop = null) => food?.DumpMemory(consumerId, currentOwnShop);
        public object[] DumpFoodRegulars(Func<FoodScope, bool>? currentOwnShop = null)
            => food?.DumpRegulars(currentOwnShop) ?? Array.Empty<object>();
        public RegularsProjection? ProjectRegulars(Func<FoodScope, bool>? currentOwnShop = null) => food?.Regulars(currentOwnShop);
    }
}
