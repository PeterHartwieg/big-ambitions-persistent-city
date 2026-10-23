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
    /// <summary>Read-only main-thread snapshot of known buildings and sampled pedestrian entrances.</summary>
    internal static class GamePlaces
    {
        // Business allocations are simulation policy, not native staffing or customer capacity.
        private const int BusinessAllocation = 16;
        private static readonly HashSet<string> LeisureTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ba:businesstype_gym", "ba:businesstype_nightclub", "ba:businesstype_cinema", "ba:businesstype_theater"
        };

        public static List<Place> Read()
        {
            var places = new List<Place>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var manager = CityManager.Instance;
            if (manager == null || SaveGameManager.Current == null || manager.cityBuildingControllers == null) return places;
            // Read the already-loaded catalog once. Do not load/instantiate interiors or use
            // registration helpers: native GetBuildingRegistration can create save records.
            var interiorAreas = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var size in BuildingSizeHelper.GetAllBuildingSizes())
                if (size != null && !string.IsNullOrWhiteSpace(size.buildingSize)) interiorAreas[size.buildingSize] = size.squareMeters;
            int unknownHomeAreas = 0, undersizedHomes = 0;
            foreach (var controller in manager.cityBuildingControllers)
            {
                if (controller == null || controller.building == null || controller.blockPedestrianSpawn) continue;
                var building = controller.building;
                // Player ownership does not make a gated private estate public to simulated citizens.
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
                    // Native rental/sale setup excludes fixed AI villas and special-rival
                    // Hamptons homes. Both checks read catalog data without registrations.
                    if (building.IsHamptonsAIVilla() || Helpers.BuildingHelper.IsHamptonsBuildingOwnedByRival(building)) continue;
                    // One game-exposed residential interior represents one household.
                    // Whole-property totalSqm cannot establish additional apartments.
                    interiorAreas.TryGetValue(building.BuildingSize ?? "", out int area);
                    if (area <= 0) { unknownHomeAreas++; continue; }
                    int capacity = HousingPolicy.Capacity(area);
                    if (capacity <= 0) { undersizedHomes++; continue; }
                    string evidence = "native:BuildingSizeData.squareMeters;policy:" + HousingPolicy.PolicyId;
                    Add(PlaceKind.Home, addressName, capacity, 0, 1440, area, evidence);
                    continue;
                }
                if (registration == null || !registration.HasEstablishedBusiness || string.IsNullOrEmpty(registration.businessTypeName)
                    || registration.businessTypeName == "ba:businesstype_empty" || registration.AvailableForRent) continue;

                if (!registration.temporarilyClosed && registration.scheduleDays != null)
                {
                    var windows = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var day in registration.scheduleDays)
                    {
                        if (day == null || !day.isOpen || day.openingHourSlots == null) continue;
                        // Verified game enum Monday=1..Sunday=7; Core Monday=0..Sunday=6.
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
                // Unknown/closed schedules remain closed (0,0); no invented business opening hours.
                weekly.Sort((a, b) => a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.FromMinute != b.FromMinute ? a.FromMinute.CompareTo(b.FromMinute) : a.ToMinute.CompareTo(b.ToMinute));
                Add(PlaceKind.Work, registration.BusinessName, BusinessAllocation, 0, 0);
                if (registration.businessTypeName == "ba:businesstype_school") Add(PlaceKind.Study, registration.BusinessName, BusinessAllocation, 0, 0);
                else if (LeisureTypes.Contains(registration.businessTypeName)) Add(PlaceKind.Leisure, registration.BusinessName, BusinessAllocation, 0, 0);
                else if (building.BuildingType == "ba:buildingtype_retail") Add(PlaceKind.Shop, registration.BusinessName, BusinessAllocation, 0, 0);

                void Add(PlaceKind kind, string name, int capacity, int opening, int closing, double residentialArea = 0, string? evidence = null)
                {
                    string id = "address:" + address + ":" + kind.ToString().ToLowerInvariant();
                    if (!ids.Add(id)) return;
                    var place = new Place { Id = id, Name = string.IsNullOrWhiteSpace(name) ? addressName : name, District = district, Kind = kind, Position = entrance,
                        Capacity = capacity, OpenMinute = opening, CloseMinute = closing,
                        ResidentialAreaSquareMetres = residentialArea,
                        NativeTrafficIndex = building.trafficIndex >= 0 && building.trafficIndex <= 100 ? building.trafficIndex : -1,
                        AttractionWeight = Attraction(kind, registration?.businessTypeName),
                        EvidenceSource = evidence ?? "native-building-traffic;policy:destination-weight" };
                    foreach (var window in weekly) place.WeeklyHours.Add(new OpeningWindow { Day = window.Day, FromMinute = window.FromMinute, ToMinute = window.ToMinute });
                    places.Add(place);
                }
            }
            if (unknownHomeAreas > 0 || undersizedHomes > 0)
                Debug.LogWarning("[Persistent City] Residential places omitted: " + unknownHomeAreas + " without positive native rentable interior area; "
                    + undersizedHomes + " below the household policy's minimum interior area.");
            foreach (var place in OutdoorPlaces.Read(places)) if (ids.Add(place.Id)) places.Add(place);
            places.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return places;
        }

        internal static bool BuildingAccessible(CityBuildingController controller)
        {
            var building = controller.building;
            return building != null && building.locatedInsidePrivateFenceIndex < 0 && building.requiredDLC.DlcIsOwned();
        }

        private static double Attraction(PlaceKind kind, string? businessType)
        {
            if (kind == PlaceKind.Home || kind == PlaceKind.Work || kind == PlaceKind.Study) return 1;
            // Category appeal is policy. DestinationChoice applies the native traffic
            // index once, separately; this tier must not count that same index twice.
            double weight = 1;
            if (kind == PlaceKind.Leisure)
            {
                if (businessType == "ba:businesstype_cinema" || businessType == "ba:businesstype_theater") weight *= 2;
                else if (businessType == "ba:businesstype_nightclub") weight *= 1.75;
                else if (businessType == "ba:businesstype_gym") weight *= 1.25;
            }
            return weight;
        }

        private static bool TryEntrance(CityBuildingController controller, out Position position)
        {
            position = default;
            if (controller.entranceDoors == null) return false;
            // Choose the first usable serialized door deterministically; never select a random entrance.
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
