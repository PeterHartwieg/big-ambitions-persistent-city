using System;
using System.Collections.Generic;
using System.Globalization;
using Buildings;
using Extensions;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    internal static class GamePlaces
    {
        private const int BusinessAllocation = 16;
        private static readonly HashSet<string> LeisureTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ba:businesstype_gym", "ba:businesstype_nightclub", "ba:businesstype_cinema", "ba:businesstype_theater"
        };

        private const string WorkplaceEvidence = "native-building-traffic;policy:destination-weight;" + WorkplaceCapacityPolicy.Evidence;

        public static List<Place> Read() => Read(null);

        public static UnrentableHomeTally LastUnrentableHomes { get; private set; } = new UnrentableHomeTally();

        public static List<Place> Read(List<CensusWorkplace>? census)
        {
            var places = new List<Place>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var manager = CityManager.Instance;
            if (manager == null || SaveGameManager.Current == null || manager.cityBuildingControllers == null) return places;
            var interiorAreas = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var size in BuildingSizeHelper.GetAllBuildingSizes())
                if (size != null && !string.IsNullOrWhiteSpace(size.buildingSize)) interiorAreas[size.buildingSize] = size.squareMeters;
            int undersizedHomes = 0;
            var unrentable = new UnrentableHomeTally();
            foreach (var controller in manager.cityBuildingControllers)
            {
                if (controller == null || controller.building == null || controller.blockPedestrianSpawn) continue;
                var building = controller.building;
                if (!BuildingAccessible(controller)) continue;
                var registration = controller.buildingRegistration;
                string street = registration != null ? registration.StreetName : building.StreetName;
                int number = registration != null ? registration.StreetNumber : building.StreetNumber;
                if (string.IsNullOrWhiteSpace(street) || !TryEntrance(controller, out Position entrance)) continue;
                string address = Uri.EscapeDataString(street) + ":" + number.ToString(CultureInfo.InvariantCulture);
                string district = building.Neighbourhood ?? "";
                string addressName = street + " " + number.ToString(CultureInfo.InvariantCulture);
                var weekly = new List<OpeningWindow>();
                if (building.BuildingType == "ba:buildingtype_residential")
                {
                    if (building.IsHamptonsAIVilla() || Helpers.BuildingHelper.IsHamptonsBuildingOwnedByRival(building)) continue;
                    interiorAreas.TryGetValue(building.BuildingSize ?? "", out int area);
                    if (area <= 0)
                    {
                        ReadFootprint(controller, out double footprint, out double floors);
                        int beds = unrentable.Add(footprint, floors);
                        if (beds > 0)
                            Add(PlaceKind.Home, addressName, beds, 0, 1440, footprint * Math.Floor(floors) * CityBalance.UnrentableHomeUsableShare,
                                "native:building-bounds;policy:" + HousingPolicy.UnrentablePolicyId);
                        continue;
                    }
                    int capacity = HousingPolicy.Capacity(area);
                    if (capacity <= 0) { undersizedHomes++; continue; }
                    string evidence = "native:BuildingSizeData.squareMeters;policy:" + HousingPolicy.PolicyId;
                    Add(PlaceKind.Home, addressName, capacity, 0, 1440, area, evidence);
                    continue;
                }
                if (registration == null || !registration.HasEstablishedBusiness || string.IsNullOrEmpty(registration.businessTypeName)
                    || registration.businessTypeName == "ba:businesstype_empty" || registration.AvailableForRent)
                {
                    if (building.BuildingType == "ba:buildingtype_retail") Add(PlaceKind.Shop, addressName, BusinessAllocation, 0, 0);
                    continue;
                }

                if (!registration.temporarilyClosed && registration.scheduleDays != null)
                {
                    var windows = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var day in registration.scheduleDays)
                    {
                        if (day == null || !day.isOpen || day.openingHourSlots == null) continue;
                        int weekday = (int)day.day - 1;
                        if (weekday < 0 || weekday > 6) continue;
                        foreach (var slot in day.openingHourSlots)
                        {
                            if (slot == null || slot.startingHour < 0 || slot.endingHour > 24 || slot.startingHour >= slot.endingHour) continue;
                            int start = slot.startingHour * 60, end = slot.endingHour * 60;
                            string window = weekday.ToString(CultureInfo.InvariantCulture) + ":" + start.ToString(CultureInfo.InvariantCulture) + ":" + end.ToString(CultureInfo.InvariantCulture);
                            if (windows.Add(window)) weekly.Add(new OpeningWindow { Day = weekday, FromMinute = start, ToMinute = end });
                        }
                    }
                }
                weekly.Sort((a, b) => a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.FromMinute != b.FromMinute ? a.FromMinute.CompareTo(b.FromMinute) : a.ToMinute.CompareTo(b.ToMinute));
                bool school = registration.businessTypeName == "ba:businesstype_school";
                interiorAreas.TryGetValue(building.BuildingSize ?? "", out int businessArea);
                businessArea = Math.Max(0, businessArea);
                if (census != null && !ids.Contains(PlaceId(PlaceKind.Work)))
                {
                    var staff = registration.aiEmployees;
                    census.Add(new CensusWorkplace
                    {
                        WorkPlaceId = PlaceId(PlaceKind.Work), StudyPlaceId = school ? PlaceId(PlaceKind.Study) : "",
                        AreaSquareMetres = businessArea, PlayerOwned = registration.RentedByPlayer,
                        NativeStaff = !registration.RentedByPlayer && !string.IsNullOrEmpty(registration.businessOwnerRivalId) && staff != null ? staff.Count : -1
                    });
                }
                Add(PlaceKind.Work, registration.BusinessName, WorkplaceCapacityPolicy.Work(businessArea, registration.RentedByPlayer), 0, 0,
                    evidence: WorkplaceEvidence);
                if (school) Add(PlaceKind.Study, registration.BusinessName, WorkplaceCapacityPolicy.Study(businessArea), 0, 0, evidence: WorkplaceEvidence);
                else if (LeisureTypes.Contains(registration.businessTypeName)) Add(PlaceKind.Leisure, registration.BusinessName, BusinessAllocation, 0, 0);
                if (building.BuildingType == "ba:buildingtype_retail")
                    Add(PlaceKind.Shop, registration.BusinessName, BusinessAllocation, 0, 0,
                        copyWeeklyHours: IsRetailShopActivity(registration.businessTypeName));

                string PlaceId(PlaceKind kind) => "address:" + address + ":" + kind.ToString().ToLowerInvariant();
                void Add(PlaceKind kind, string name, int capacity, int opening, int closing, double residentialArea = 0, string? evidence = null, bool copyWeeklyHours = true)
                {
                    string id = PlaceId(kind);
                    if (!ids.Add(id)) return;
                    var place = new Place { Id = id, Name = string.IsNullOrWhiteSpace(name) ? addressName : name, District = district, Kind = kind, Position = entrance,
                        Capacity = capacity, OpenMinute = opening, CloseMinute = closing,
                        ResidentialAreaSquareMetres = residentialArea,
                        NativeTrafficIndex = building.trafficIndex >= 0 && building.trafficIndex <= 100 ? building.trafficIndex : -1,
                        AttractionWeight = Attraction(kind, registration?.businessTypeName),
                        BusinessType = kind == PlaceKind.Home ? BusinessTypes.None : EstablishedType(registration),
                        EvidenceSource = evidence ?? "native-building-traffic;policy:destination-weight" };
                    if (copyWeeklyHours)
                        foreach (var window in weekly) place.WeeklyHours.Add(new OpeningWindow { Day = window.Day, FromMinute = window.FromMinute, ToMinute = window.ToMinute });
                    places.Add(place);
                }
            }
            LastUnrentableHomes = unrentable;
            Debug.Log("[Persistent City] " + unrentable.Summary());
            if (unrentable.Unreadable > 0 || undersizedHomes > 0)
                Debug.LogWarning("[Persistent City] Residential places omitted: " + unrentable.Unreadable + " without a finite footprint or floor count; "
                    + undersizedHomes + " below the household policy's minimum interior area.");
            foreach (var place in OutdoorPlaces.Read(places)) if (ids.Add(place.Id)) places.Add(place);
            places.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return places;
        }

        private static string EstablishedType(BuildingRegistration? registration)
        {
            if (registration == null || !registration.HasEstablishedBusiness || registration.AvailableForRent) return BusinessTypes.None;
            string type = BusinessTypes.Normalize(registration.businessTypeName);
            return type.Length <= BusinessTypes.MaximumNameLength ? type : BusinessTypes.None;
        }

        public static Dictionary<string, string> ReadBusinessTypes(Dictionary<string, string>? reuse = null)
        {
            var types = reuse ?? new Dictionary<string, string>(StringComparer.Ordinal);
            types.Clear();
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null) return types;
            foreach (var controller in manager.cityBuildingControllers)
            {
                if (controller == null || controller.building == null || controller.blockPedestrianSpawn || !BuildingAccessible(controller)) continue;
                var registration = controller.buildingRegistration;
                if (registration == null || controller.building.BuildingType == "ba:buildingtype_residential") continue;
                string type = EstablishedType(registration);
                if (type.Length == 0 || string.IsNullOrWhiteSpace(registration.StreetName)) continue;
                types["address:" + Uri.EscapeDataString(registration.StreetName) + ":" + registration.StreetNumber.ToString(CultureInfo.InvariantCulture)] = type;
            }
            return types;
        }

        internal static bool IsRetailShopActivity(string? businessType) => businessType != "ba:businesstype_school"
            && (businessType == null || !LeisureTypes.Contains(businessType));

        internal static bool BuildingAccessible(CityBuildingController controller)
        {
            var building = controller.building;
            return building != null && building.locatedInsidePrivateFenceIndex < 0 && building.requiredDLC.DlcIsOwned();
        }

        private static double Attraction(PlaceKind kind, string? businessType)
        {
            if (kind == PlaceKind.Home || kind == PlaceKind.Work || kind == PlaceKind.Study) return 1;
            double weight = 1;
            if (kind == PlaceKind.Leisure)
            {
                if (businessType == "ba:businesstype_cinema" || businessType == "ba:businesstype_theater") weight *= 2;
                else if (businessType == "ba:businesstype_nightclub") weight *= 1.75;
                else if (businessType == "ba:businesstype_gym") weight *= 1.25;
            }
            return weight;
        }

        private static void ReadFootprint(CityBuildingController controller, out double footprint, out double floors)
        {
            footprint = double.NaN; floors = double.NaN;
            Bounds? combined = null;
            foreach (var collider in controller.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var bounds = collider.bounds;
                if (combined is Bounds sum) { sum.Encapsulate(bounds); combined = sum; } else combined = bounds;
            }
            if (!(combined is Bounds total)) return;
            footprint = (double)total.size.x * total.size.z;
            floors = Math.Floor(total.size.y / CityBalance.StoreyHeightMetres);
        }

        internal static bool TryEntrance(CityBuildingController controller, out Position position)
        {
            position = default;
            if (controller.entranceDoors == null) return false;
            foreach (var entrance in controller.entranceDoors)
            {
                if (entrance == null || entrance.doorTransform == null) continue;
                Transform door = entrance.doorTransform;
                Vector3 candidate = door.position + door.forward * 0.5f;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, WalkingRoutes.DryFilter)) continue;
                var point = new Position(hit.position.x, hit.position.y, hit.position.z);
                if (!point.IsFinite) continue;
                position = point; return true;
            }
            return false;
        }
    }
}
