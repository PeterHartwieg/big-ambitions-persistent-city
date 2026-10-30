using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>Bounded observed offer decisions, not an exhaustive counterfactual demand count.</summary>
    public sealed class FoodBuyerDistrict
    {
        public string DistrictId { get; set; } = "";
        public long Purchases { get; set; }
    }
    public sealed class FoodProductSales
    {
        public string ProductId { get; set; } = "";
        public long Purchases { get; set; }
        public decimal RevenueCents { get; set; }
        public long[] PurchaseMeals { get; set; } = new long[4];
    }
    public sealed class FoodShopDay
    {
        public int Day { get; set; }
        public string PlaceId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        public long OffersConsidered { get; set; }
        public long ChosenOffers { get; set; }
        public long PriceRejections { get; set; }
        public long AccessRejections { get; set; }
        public long ClosedRejections { get; set; }
        public long StockRejections { get; set; }
        public long StaffRejections { get; set; }
        public long ServiceRejections { get; set; }
        public long UnknownEvidence { get; set; }
        public long PreferredAlternatives { get; set; }
        public long Purchases { get; set; }
        public long ExistingStopPurchases { get; set; }
        public decimal RevenueCents { get; set; }
        public long[] PurchaseHours { get; set; } = new long[24];
        public long[] PurchaseMeals { get; set; } = new long[4];
        public List<FoodProductSales> ProductSales { get; set; } = new List<FoodProductSales>();
        public List<FoodBuyerDistrict> BuyerDistricts { get; set; } = new List<FoodBuyerDistrict>();
    }
    public sealed partial class FoodCommerce
    {
        private FoodShopDay ShopDay(string place, string business, int day)
        {
            // Fourteen days of business-specific aggregates; no resident-history growth.
            state.History.RemoveAll(h => h.Day < day - 13);
            var history = state.History.FirstOrDefault(h => h.Day == day && h.PlaceId == place && h.BusinessId == business);
            if (history == null) { history = new FoodShopDay { Day = day, PlaceId = place, BusinessId = business }; state.History.Add(history); }
            return history;
        }
        private void RecordChoices(FoodIntent intent, IReadOnlyList<FoodBusiness> businesses)
        {
            foreach (var choice in intent.Choices)
            {
                var shop = businesses.First(b => b.Id == choice.BusinessId);
                var row = ShopDay(shop.PlaceId, shop.Id, intent.Day); row.OffersConsidered++;
                if (choice.Reason.StartsWith("Chosen:", StringComparison.Ordinal) && intent.Status == FoodIntentStatus.Travelling) row.ChosenOffers++;
                else if (choice.ScoreCents.HasValue) row.PreferredAlternatives++;
                else if (choice.Reason == "Over allowance") row.PriceRejections++;
                else if (choice.Reason == "No staffed counter") row.StaffRejections++;
                else if (choice.Reason.StartsWith("Out of stock", StringComparison.Ordinal)) row.StockRejections++;
                else if (choice.Reason == "Closed or too late") row.ClosedRejections++;
                else if (choice.Reason == "Service unavailable" || choice.Reason.Contains("queue") || choice.Reason.Contains("Service and return")) row.ServiceRejections++;
                else if (choice.Reason.Contains("unknown") || choice.Reason.Contains("unavailable")) row.UnknownEvidence++;
                else row.AccessRejections++;
            }
        }
        private void RecordPurchase(ResidentMeal meal)
        {
            // Receipt reconciliation can happen days later; retain the actual checkout interval.
            int purchaseDay = (int)(meal.ServiceAt / 1440);
            if (purchaseDay < (int)(city.State.Minute / 1440) - 13) return;
            var row = ShopDay(meal.PlaceId, meal.BusinessId, purchaseDay); row.Purchases++;
            row.RevenueCents = checked(row.RevenueCents + meal.PriceCents);
            if (meal.ExistingStop) row.ExistingStopPurchases++;
            row.PurchaseHours[(int)(meal.ServiceAt % 1440 / 60)]++;
            row.PurchaseMeals[(int)meal.Kind]++;
            var product = row.ProductSales.FirstOrDefault(p => p.ProductId == meal.ProductId);
            if (product == null) { product = new FoodProductSales { ProductId = meal.ProductId }; row.ProductSales.Add(product); }
            product.Purchases++; product.RevenueCents = checked(product.RevenueCents + meal.PriceCents);
            product.PurchaseMeals[(int)meal.Kind]++;
            string district = city.FindPlace(city.FindCitizen(meal.AnchorId)!.HomeId).District;
            var group = row.BuyerDistricts.FirstOrDefault(d => d.DistrictId == district);
            if (group == null) { group = new FoodBuyerDistrict { DistrictId = district }; row.BuyerDistricts.Add(group); }
            group.Purchases++;
        }
    }
    public static class FoodEconomyInsight
    {
        // Built only on demand by the management panel or explicit diagnostics.
        public static object Capture(CityState city)
        {
            var food = city.Food ?? throw new InvalidOperationException("No enrolled food economy.");
            var budgets = food.Residents.Where(m => m.Budget != null).Select(m => m.Budget!).ToArray();
            return new {
                minute = city.Minute, residents = food.Residents.Count, physicalResidents = city.Citizens.Count, representedConsumers = food.Residents.Count, representationFactor = food.RepresentationFactor,
                unresolvedConsumers = food.Residents.Count(m => m.Status == MealStatus.Uncertain || m.Excursion?.Unresolved == true), financePolicy = food.HouseholdFinanceVersion, mealPolicy = food.MealRhythmVersion,
                boundary = food.HouseholdFinanceVersion == 0 ? "Temporary allowance experiment" : HouseholdFinance.Evidence,
                balances = budgets.Sum(b => b.BalanceCents), openingSavings = budgets.Sum(b => b.OpeningBalanceCents),
                income = budgets.Sum(b => b.IncomeReceivedCents), essentials = budgets.Sum(b => b.EssentialsPaidCents),
                householdFood = budgets.Sum(b => b.HomeFoodPaidCents), preparedFood = budgets.Sum(b => b.PreparedFoodSpentCents),
                heldPurchases = budgets.Sum(b => b.HeldPurchaseCents), arrears = budgets.Sum(b => b.ArrearsCents),
                unfundedFoodDays = budgets.Sum(b => b.DaysWithoutHomeFood),
                incomeSources = budgets.GroupBy(b => b.IncomeSource.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                purchases = food.Residents.Sum(m => m.Purchases), existingStopPurchases = food.Residents.Sum(m => m.ExistingStopPurchases),
                currentDecisions = food.Residents.GroupBy(m => m.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                history = food.History.OrderBy(h => h.Day).ThenBy(h => h.PlaceId, StringComparer.Ordinal).ThenBy(h => h.BusinessId, StringComparer.Ordinal).ToArray(),
                historyMeaning = "Observed offers and confirmed purchases within enrolled shops; repeated decisions are not unique people or total district demand. Revenue is not profit." };
        }
    }
}
