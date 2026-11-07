using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Existing registration fields only. Controllers are indexed once; hourly units read at most 16 places.</summary>
    internal static class NativeOrdinaryOwnership
    {
        internal static OrdinaryOwnershipIndex Create(IReadOnlyList<Place> places, double minute)
        {
            var paid = new List<Place>();
            foreach (var place in places)
                if ((place.Kind == PlaceKind.Shop || place.Kind == PlaceKind.Leisure) && place.Id.StartsWith("address:", StringComparison.Ordinal)) paid.Add(place);
            if (paid.Count > OrdinaryOwnershipIndex.MaximumPlaces) throw new ArgumentOutOfRangeException(nameof(places));
            paid.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            var controllers = new Dictionary<string, CityBuildingController?>(paid.Count, StringComparer.Ordinal);
            foreach (var place in paid) controllers.Add(place.Id, null);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            var source = CityManager.Instance?.cityBuildingControllers;
            if (source != null) foreach (var controller in source)
            {
                if (controller == null || controller.building == null) continue;
                var building = controller.building;
                var reg = controller.buildingRegistration;
                string street = reg != null ? reg.StreetName : building.StreetName;
                int number = reg != null ? reg.StreetNumber : building.StreetNumber;
                if (string.IsNullOrWhiteSpace(street)) continue;
                string address = "address:" + Uri.EscapeDataString(street) + ":" + number.ToString(CultureInfo.InvariantCulture);
                Add(address + ":shop", controller); Add(address + ":leisure", controller);
            }
            return new OrdinaryOwnershipIndex(paid, minute, Read);

            void Add(string id, CityBuildingController controller)
            {
                if (!controllers.TryGetValue(id, out var previous)) return;
                if (previous != null) ambiguous.Add(id);
                else controllers[id] = controller;
            }
            OrdinaryVisitTerms Read(Place place)
            {
                // Missing, ambiguous or unsupported registrations are activity only; never infer an outside owner.
                if (ambiguous.Contains(place.Id) || !controllers.TryGetValue(place.Id, out var controller) || controller == null)
                    return default;
                var reg = controller.buildingRegistration;
                if (reg == null || reg.RentedByPlayer) return new OrdinaryVisitTerms(0, true, OrdinaryVisitClass.ActivityOnly);
                if (string.IsNullOrWhiteSpace(reg.businessOwnerRivalId) || !reg.HasEstablishedBusiness || reg.AvailableForRent) return default;
                string type = reg.businessTypeName;
                // Explicit non-food class table; unsupported/food businesses stay activity only.
                var kind = Classify(type, place.Kind);
                return new OrdinaryVisitTerms(0, false, kind);
            }
        }
        internal static OrdinaryVisitClass Classify(string type, PlaceKind place)
        {
            if (place == PlaceKind.Leisure)
                switch (type)
                {
                    case "ba:businesstype_gym": case "ba:businesstype_nightclub":
                    case "ba:businesstype_cinema": case "ba:businesstype_theater": return OrdinaryVisitClass.Leisure;
                }
            if (place == PlaceKind.Shop)
                switch (type)
                {
                    case "ba:businesstype_giftshop": case "ba:businesstype_clothingstore":
                    case "ba:businesstype_jewelrystore": case "ba:businesstype_florist":
                    case "ba:businesstype_electronicsstore": return OrdinaryVisitClass.Retail;
                }
            return OrdinaryVisitClass.ActivityOnly;
        }
    }
}
