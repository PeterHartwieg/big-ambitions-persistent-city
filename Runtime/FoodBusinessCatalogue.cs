using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed class FoodCatalogueObservation
    {
        public double AtMinute { get; set; }
        public string Evidence { get; set; } = "Native saved products, prices, hours, customer-service score and district product demand. Stock, service capacity and routes unverified.";
        public List<FoodBusiness> Businesses { get; set; } = new List<FoodBusiness>();
        public List<string> UnavailablePlaces { get; set; } = new List<string>();
    }

    /// <summary>
    /// Explicit main-thread observation of selected shops. Not scheduled or
    /// activated by the mod. Never calls registration creation, customer generation or RLO.
    /// </summary>
    public static class FoodBusinessCatalogue
    {
        internal static string BusinessIdentity(BuildingRegistration reg)
        {
            string address = Uri.EscapeDataString(reg.StreetName) + ":" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
            string owner = reg.RentedByPlayer ? "player" : reg.businessOwnerRivalId ?? "unknown";
            return "business:" + address + ":" + reg.creationDay.ToString(CultureInfo.InvariantCulture)
                + ":" + Uri.EscapeDataString(reg.businessTypeName ?? "") + ":" + Uri.EscapeDataString(owner);
        }

        public static FoodCatalogueObservation Read(IReadOnlyCollection<string> shopPlaceIds, IReadOnlyCollection<string> foodProductIds)
        {
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null)
                throw new InvalidOperationException("No live city catalogue is available.");
            return ReadSelected(shopPlaceIds, foodProductIds, manager.cityBuildingControllers);
        }

        internal static FoodCatalogueObservation ReadSelected(IReadOnlyCollection<string> shopPlaceIds,
            IReadOnlyCollection<string> foodProductIds, IEnumerable<CityBuildingController> controllers, Func<BuildingRegistration, string>? identity = null)
        {
            if (shopPlaceIds == null || shopPlaceIds.Count < 1 || shopPlaceIds.Count > 20000
                || foodProductIds == null || foodProductIds.Count < 1 || foodProductIds.Count > 16
                || shopPlaceIds.Any(string.IsNullOrWhiteSpace) || foodProductIds.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Select one to twenty thousand shop places and one to sixteen reviewed food product IDs.");
            var save = SaveGameManager.Current;
            if (save == null)
                throw new InvalidOperationException("No live city catalogue is available.");
            var selected = new HashSet<string>(shopPlaceIds, StringComparer.Ordinal);
            var products = new HashSet<string>(foodProductIds, StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            var result = new FoodCatalogueObservation { AtMinute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute };
            foreach (var controller in controllers)
            {
                if (controller == null || controller.building == null || !GamePlaces.BuildingAccessible(controller)) continue;
                // This field is already assigned by the game. Do not call GetBuildingRegistration.
                var reg = controller.buildingRegistration;
                if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName)) continue;
                string address = Uri.EscapeDataString(reg.StreetName) + ":" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
                string place = "address:" + address + ":shop";
                if (!selected.Contains(place) || !reg.HasEstablishedBusiness || reg.AvailableForRent
                    || string.IsNullOrWhiteSpace(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty") continue;
                if (!found.Add(place)) throw new InvalidOperationException("Duplicate live shop address; observation refused.");
                var business = new FoodBusiness { Id = identity?.Invoke(reg) ?? BusinessIdentity(reg),
                    PlaceId = place, Name = reg.BusinessName, DistrictId = controller.building.Neighbourhood ?? "" };
                if (reg.satisfaction != null)
                {
                    int quality = reg.satisfaction.customerService;
                    if (quality < 0 || quality > 100) throw new InvalidOperationException("Customer-service score is outside 0–100.");
                    business.CustomerServicePercent = quality;
                    business.CustomerServiceEvidence = "Native saved customer-service score; may be the game's initial default, not verified current staffing";
                }
                if (!reg.temporarilyClosed && reg.scheduleDays != null)
                    foreach (var day in reg.scheduleDays)
                    {
                        if (day == null || !day.isOpen || day.openingHourSlots == null) continue;
                        int weekday = (int)day.day - 1;
                        if (weekday < 0 || weekday > 6) continue;
                        foreach (var slot in day.openingHourSlots)
                            if (slot != null && slot.startingHour >= 0 && slot.startingHour < slot.endingHour && slot.endingHour <= 24)
                                business.Hours.Add(new OpeningWindow { Day = weekday, FromMinute = slot.startingHour * 60, ToMinute = slot.endingHour * 60 });
                    }
                if (reg.retailPrices != null && reg.cachedAvailableProducts != null)
                    foreach (var price in reg.retailPrices)
                    {
                        if (price == null || !products.Contains(price.itemName) || !reg.cachedAvailableProducts.Contains(price.itemName)) continue;
                        if (!Position.Finite(price.price) || price.price <= 0 || price.price > 10000)
                            throw new InvalidOperationException("Food price is outside the experiment's range.");
                        int cents = checked((int)Math.Round((double)price.price * 100, MidpointRounding.AwayFromZero));
                        if (cents == 0 || business.Offers.Any(o => o.ProductId == price.itemName))
                            throw new InvalidOperationException("Ambiguous or sub-cent food price; observation refused.");
                        // An advertised product is not proof of stocked, staffed production.
                        var offer = new FoodOffer { ProductId = price.itemName, PriceCents = cents, Units = null };
                        // Read existing saved values only. Demand helpers can supply fabricated
                        // defaults or recalculate providers/events; neither belongs in this observer.
                        if (business.DistrictId.Length > 0 && save.productMarketEntries != null)
                            foreach (var entry in save.productMarketEntries)
                            {
                                if (entry == null || entry.itemName != price.itemName || entry.demandValues == null) continue;
                                foreach (var demand in entry.demandValues)
                                {
                                    if (demand == null || demand.neighborhood != business.DistrictId) continue;
                                    if (offer.DistrictDemandPercent.HasValue) throw new InvalidOperationException("Duplicate saved product/district demand; observation refused.");
                                    offer.DistrictDemandPercent = Math.Max(0, Math.Min(100, demand.demand));
                                    offer.DistrictDemandEvidence = "Native saved product/district demand " + demand.demand.ToString(CultureInfo.InvariantCulture)
                                        + "; normalized to 0–100; includes provider saturation and market events, not a resident count";
                                }
                            }
                        business.Offers.Add(offer);
                    }
                business.Offers.Sort((a, b) => StringComparer.Ordinal.Compare(a.ProductId, b.ProductId));
                business.Hours = business.Hours.OrderBy(h => h.Day).ThenBy(h => h.FromMinute).ThenBy(h => h.ToMinute).ToList();
                result.Businesses.Add(business);
            }
            result.Businesses.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            result.UnavailablePlaces = selected.Where(p => !found.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
            return result;
        }
    }
}
