using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Runtime
{
    public sealed class FoodEnrollmentShop
    {
        public string PlaceId { get; set; } = "";
        public string Name { get; set; } = "";
    }
    public sealed class NativeFoodAccount
    {
        public int Day { get; set; }
        public string PlaceId { get; set; } = "";
        public string Name { get; set; } = "";
        public float Revenue { get; set; }
        public float Resources { get; set; }
        public float Wages { get; set; }
        public float Rent { get; set; }
        public float OtherOperatingCosts { get; set; }
        public float OperatingProfit { get; set; }
    }
    public sealed partial class NativeFoodMarket
    {
        private static string ShopLabel(BuildingRegistration shop) => shop.BusinessName + " · "
            + Streets.AddressHelper.ToFormattedString(shop.Address);
        public string? NameOf(string placeId) => registrations.TryGetValue(placeId, out var shop) ? ShopLabel(shop) : null;
        public static IReadOnlyList<FoodEnrollmentShop> EligibleShops()
        {
            if (CityManager.Instance == null || SaveGameManager.Current == null) return new FoodEnrollmentShop[0];
            return CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null
                && GamePlaces.BuildingAccessible(c) && Supported(c.buildingRegistration))
                .Select(c => new FoodEnrollmentShop { PlaceId = PlaceId(c.buildingRegistration), Name = ShopLabel(c.buildingRegistration) })
                .OrderBy(s => s.Name, System.StringComparer.Ordinal).ThenBy(s => s.PlaceId, System.StringComparer.Ordinal).ToArray();
        }
        // Existing native statements are read, never recalculated or posted a second time.
        public IReadOnlyList<string> UnmanagedShopStatuses() => registrations.Values.Where(r => r.RentedByPlayer)
            .Select(r => new { Name = ShopLabel(r), Status = MenuStatus(PlaceId(r)) })
            .Where(r => r.Status.Length > 0).OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => r.Name + ": " + r.Status).ToArray();

        public IReadOnlyList<NativeFoodAccount> ReadAccounts()
        {
            var result = new List<NativeFoodAccount>();
            if (!ReferenceEquals(save, SaveGameManager.Current)) return result;
            foreach (var summary in SaveGameManager.Current.financialSummaries.Where(s => s.dayNumber >= SaveGameManager.Current.Day - 14))
            foreach (var statement in summary.businessIncomeStatements)
            foreach (var reg in registrations.Values)
                if (statement.Address == reg.Address)
                    result.Add(new NativeFoodAccount { Day = summary.dayNumber, PlaceId = PlaceId(reg), Name = reg.BusinessName,
                        Revenue = statement.TotalSales, Resources = statement.TotalResources, Wages = statement.SalaryExpenses,
                        Rent = statement.RentExpenses, OtherOperatingCosts = statement.MarketingExpenses + statement.LicensingFees + statement.Theft,
                        OperatingProfit = statement.TotalProfit });
            return result;
        }
    }
}
