using System;
using Buildings;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Opens the game's own map at an address, the same call as the native "show on map" button.
    /// Main thread only; runs on a click, never per frame.</summary>
    internal static class NativeMap
    {
        public static bool TryFocus(ResidentAddress address, out string problem)
        {
            problem = "";
            if (address == null || !address.CanNavigate) { problem = "This place has no street address on the map."; return false; }
            var manager = CityManager.Instance;
            if (manager == null || manager.cityMap == null || manager.cityBuildingControllers == null) { problem = "The map is not available."; return false; }
            if (!CityMap.CanOpenMap()) { problem = "The map can't open right now."; return false; }
            foreach (var controller in manager.cityBuildingControllers)
            {
                if (controller == null || controller.building == null) continue;
                var registration = controller.buildingRegistration;
                string street = registration != null ? registration.StreetName : controller.building.StreetName;
                int number = registration != null ? registration.StreetNumber : controller.building.StreetNumber;
                if (number != address.Number || !string.Equals(street, address.Street, StringComparison.Ordinal)) continue;
                // The native focus reads the first entrance door.
                if (controller.entranceDoors == null || controller.entranceDoors.Length == 0
                    || controller.entranceDoors[0] == null || controller.entranceDoors[0].doorTransform == null) continue;
                manager.cityMap.FocusOnBuilding(controller);
                return true;
            }
            problem = "That address was not found on the map.";
            return false;
        }
    }
}
