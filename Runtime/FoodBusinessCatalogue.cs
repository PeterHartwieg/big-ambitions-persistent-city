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

    public static class FoodBusinessCatalogue
    {
        internal class CatalogueRefusalException : InvalidOperationException
        {
            internal CatalogueRefusalException(string message, Exception? cause = null) : base(message, cause) { }
        }

        internal sealed class UnsupportedPriceException : CatalogueRefusalException
        {
            internal UnsupportedPriceException(ArgumentOutOfRangeException cause)
                : base("Food price is outside the supported range or precision.", cause) { }
        }

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
            => ReadSelectedCore(shopPlaceIds, foodProductIds, controllers, identity, null);

        private static readonly string[] DemandEvidenceCache = BuildDemandEvidenceCache();
        private static string[] BuildDemandEvidenceCache()
        {
            var cache = new string[101];
            for (int i = 0; i < cache.Length; i++)
                cache[i] = "Native saved product/district demand " + i.ToString(CultureInfo.InvariantCulture)
                    + "; normalized to 0–100; includes provider saturation and market events, not a resident count";
            return cache;
        }
        private static string DemandEvidence(int demand) => (uint)demand <= 100u
            ? DemandEvidenceCache[demand]
            : "Native saved product/district demand " + demand.ToString(CultureInfo.InvariantCulture)
                + "; normalized to 0–100; includes provider saturation and market events, not a resident count";
        private static readonly Comparison<FoodOffer> CompareOfferProduct = (a, b) => StringComparer.Ordinal.Compare(a.ProductId, b.ProductId);
        private static readonly Comparison<OpeningWindow> CompareOpeningWindow = (a, b) =>
        {
            int day = a.Day.CompareTo(b.Day);
            if (day != 0) return day;
            int from = a.FromMinute.CompareTo(b.FromMinute);
            return from != 0 ? from : a.ToMinute.CompareTo(b.ToMinute);
        };

        internal static FoodBusiness? ReadSingleCompleteMenu(string placeId, CityBuildingController controller, BuildingRegistration reg,
            string[] products, string businessId, Action<BuildingRegistration, CatalogueRefusalException>? refused)
        {
            var save = SaveGameManager.Current;
            if (save == null) throw new InvalidOperationException("No live city catalogue is available.");
            try
            {
                if (products == null || products.Length < 1 || products.Length > FoodMenuPolicy.MaximumOffers
                    || reg.cachedAvailableProducts == null || reg.cachedAvailableProducts.Count != products.Length
                    || reg.retailPrices == null || reg.retailPrices.Count > 128)
                    throw new CatalogueRefusalException("Complete food menu changed or is outside its bounds.");
                for (int i = 0; i < products.Length; i++)
                {
                    string p = products[i];
                    if (!FoodNameIdentity.IsCanonical(p) || !reg.cachedAvailableProducts.Contains(p))
                        throw new CatalogueRefusalException("Complete food menu changed or is outside its bounds.");
                    for (int j = 0; j < i; j++)
                        if (string.Equals(products[j], p, StringComparison.Ordinal))
                            throw new CatalogueRefusalException("Complete food menu changed or is outside its bounds.");
                }
                var business = new FoodBusiness { Id = businessId, PlaceId = placeId, Name = reg.BusinessName, DistrictId = controller.building.Neighbourhood ?? "" };
                if (reg.satisfaction != null)
                {
                    int quality = reg.satisfaction.customerService;
                    if (quality < 0 || quality > 100) throw new CatalogueRefusalException("Customer-service score is outside 0–100.");
                    business.CustomerServicePercent = quality;
                    business.CustomerServiceEvidence = "Native saved customer-service score; may be the game's initial default, not verified current staffing";
                }
                if (!reg.temporarilyClosed && reg.scheduleDays != null)
                {
                    var days = reg.scheduleDays;
                    for (int d = 0; d < days.Count; d++)
                    {
                        var day = days[d];
                        if (day == null || !day.isOpen || day.openingHourSlots == null) continue;
                        int weekday = (int)day.day - 1;
                        if (weekday < 0 || weekday > 6) continue;
                        var slots = day.openingHourSlots;
                        for (int s = 0; s < slots.Count; s++)
                        {
                            var slot = slots[s];
                            if (slot != null && slot.startingHour >= 0 && slot.startingHour < slot.endingHour && slot.endingHour <= 24)
                                business.Hours.Add(new OpeningWindow { Day = weekday, FromMinute = slot.startingHour * 60, ToMinute = slot.endingHour * 60 });
                        }
                    }
                }
                var prices = reg.retailPrices;
                for (int i = 0; i < prices.Count; i++)
                {
                    var price = prices[i];
                    if (price == null || !ContainsProduct(products, price.itemName) || !reg.cachedAvailableProducts.Contains(price.itemName)) continue;
                    Money exact;
                    try { exact = Money.FromNative(price.price); }
                    catch (ArgumentOutOfRangeException ex) { throw new UnsupportedPriceException(ex); }
                    if (exact == Money.Zero || HasOffer(business.Offers, price.itemName))
                        throw new CatalogueRefusalException("Ambiguous food price; observation refused.");
                    var offer = new FoodOffer { ProductId = price.itemName, Price = exact, Units = null };
                    if (business.DistrictId.Length > 0 && save.productMarketEntries != null)
                    {
                        var entries = save.productMarketEntries;
                        for (int e = 0; e < entries.Count; e++)
                        {
                            var entry = entries[e];
                            if (entry == null || entry.itemName != price.itemName || entry.demandValues == null) continue;
                            var demands = entry.demandValues;
                            for (int dv = 0; dv < demands.Count; dv++)
                            {
                                var demand = demands[dv];
                                if (demand == null || demand.neighborhood != business.DistrictId) continue;
                                if (offer.DistrictDemandPercent.HasValue) throw new CatalogueRefusalException("Duplicate saved product/district demand; observation refused.");
                                offer.DistrictDemandPercent = Math.Max(0, Math.Min(100, demand.demand));
                                offer.DistrictDemandEvidence = DemandEvidence(demand.demand);
                            }
                        }
                    }
                    business.Offers.Add(offer);
                }
                if (business.Offers.Count != products.Length)
                    throw new CatalogueRefusalException("Complete food menu requires one explicit price for every advertised product.");
                business.Offers.Sort(CompareOfferProduct);
                business.Hours.Sort(CompareOpeningWindow);
                return business;
            }
            catch (CatalogueRefusalException error) when (refused != null)
            {
                refused(reg, error);
                return null;
            }
        }

        private static bool ContainsProduct(string[] products, string name)
        {
            for (int i = 0; i < products.Length; i++) if (products[i] == name) return true;
            return false;
        }
        private static bool HasOffer(List<FoodOffer> offers, string name)
        {
            for (int i = 0; i < offers.Count; i++) if (offers[i].ProductId == name) return true;
            return false;
        }

        internal static FoodCatalogueObservation ReadCompleteMenus(IReadOnlyCollection<string> shopPlaceIds,
            IEnumerable<CityBuildingController> controllers, Func<BuildingRegistration, IReadOnlyCollection<string>?> menu,
            Func<BuildingRegistration, string>? identity = null,
            Action<BuildingRegistration, CatalogueRefusalException>? refused = null)
            => ReadSelectedCore(shopPlaceIds, null, controllers, identity, menu, refused);

        private static FoodCatalogueObservation ReadSelectedCore(IReadOnlyCollection<string> shopPlaceIds,
            IReadOnlyCollection<string>? foodProductIds, IEnumerable<CityBuildingController> controllers,
            Func<BuildingRegistration, string>? identity, Func<BuildingRegistration, IReadOnlyCollection<string>?>? menu,
            Action<BuildingRegistration, CatalogueRefusalException>? refused = null)
        {
            if (shopPlaceIds == null || shopPlaceIds.Count < 1 || shopPlaceIds.Count > 20000
                || menu == null && (foodProductIds == null || foodProductIds.Count < 1 || foodProductIds.Count > 16
                    || foodProductIds.Any(string.IsNullOrWhiteSpace))
                || shopPlaceIds.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Select one to twenty thousand shop places and one to sixteen reviewed food product IDs.");
            var save = SaveGameManager.Current;
            if (save == null)
                throw new InvalidOperationException("No live city catalogue is available.");
            var selected = new HashSet<string>(shopPlaceIds, StringComparer.Ordinal);
            var selectedProducts = foodProductIds == null ? null : new HashSet<string>(foodProductIds, StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            var result = new FoodCatalogueObservation { AtMinute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute };
            foreach (var controller in controllers)
            {
                if (controller == null || controller.building == null || !GamePlaces.BuildingAccessible(controller)) continue;
                var reg = controller.buildingRegistration;
                if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName)) continue;
                string address = Uri.EscapeDataString(reg.StreetName) + ":" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
                string place = "address:" + address + ":shop";
                if (!selected.Contains(place) || !reg.HasEstablishedBusiness || reg.AvailableForRent
                    || BusinessTypes.Normalize(reg.businessTypeName).Length == 0) continue;
                var completeMenu = menu?.Invoke(reg);
                if (menu != null && completeMenu == null) continue;
                try
                {
                if (!found.Add(place)) throw new CatalogueRefusalException("Duplicate live shop address; observation refused.");
                IReadOnlyCollection<string> products = menu == null ? selectedProducts! : completeMenu!;
                if (menu != null && (products == null || products.Count < 1 || products.Count > FoodMenuPolicy.MaximumOffers
                    || reg.cachedAvailableProducts == null || reg.cachedAvailableProducts.Count != products.Count
                    || products.Any(p => !FoodNameIdentity.IsCanonical(p) || !reg.cachedAvailableProducts.Contains(p))
                    || products.Distinct(StringComparer.Ordinal).Count() != products.Count
                    || reg.retailPrices == null || reg.retailPrices.Count > 128))
                    throw new CatalogueRefusalException("Complete food menu changed or is outside its bounds.");
                var business = new FoodBusiness { Id = identity?.Invoke(reg) ?? BusinessIdentity(reg),
                    PlaceId = place, Name = reg.BusinessName, DistrictId = controller.building.Neighbourhood ?? "" };
                if (reg.satisfaction != null)
                {
                    int quality = reg.satisfaction.customerService;
                    if (quality < 0 || quality > 100) throw new CatalogueRefusalException("Customer-service score is outside 0–100.");
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
                        Money exact;
                        try { exact = Money.FromNative(price.price); }
                        catch (ArgumentOutOfRangeException ex) { throw new UnsupportedPriceException(ex); }
                        if (exact == Money.Zero || business.Offers.Any(o => o.ProductId == price.itemName))
                            throw new CatalogueRefusalException("Ambiguous food price; observation refused.");
                        var offer = new FoodOffer { ProductId = price.itemName, Price = exact, Units = null };
                        if (business.DistrictId.Length > 0 && save.productMarketEntries != null)
                            foreach (var entry in save.productMarketEntries)
                            {
                                if (entry == null || entry.itemName != price.itemName || entry.demandValues == null) continue;
                                foreach (var demand in entry.demandValues)
                                {
                                    if (demand == null || demand.neighborhood != business.DistrictId) continue;
                                    if (offer.DistrictDemandPercent.HasValue) throw new CatalogueRefusalException("Duplicate saved product/district demand; observation refused.");
                                    offer.DistrictDemandPercent = Math.Max(0, Math.Min(100, demand.demand));
                                    offer.DistrictDemandEvidence = DemandEvidence(demand.demand);
                                }
                            }
                        business.Offers.Add(offer);
                    }
                if (menu != null && business.Offers.Count != products.Count)
                    throw new CatalogueRefusalException("Complete food menu requires one explicit price for every advertised product.");
                business.Offers.Sort(CompareOfferProduct);
                business.Hours.Sort(CompareOpeningWindow);
                result.Businesses.Add(business);
                }
                catch (CatalogueRefusalException error) when (menu != null && refused != null)
                {
                    result.Businesses.RemoveAll(b => b.PlaceId == place);
                    refused(reg, error);
                }
            }
            result.Businesses.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            result.UnavailablePlaces = selected.Where(p => !found.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
            return result;
        }
    }
}
