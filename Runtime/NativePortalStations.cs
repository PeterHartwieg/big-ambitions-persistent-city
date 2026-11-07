using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using PersistentCity.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
    /// <summary>F5d: reads the subway stations once, on the main thread, when a city's first roster is created, so its
    /// visitors get portal places (plan section 3 World capacity, portals). Read-only: no native object is kept or
    /// changed. The filter matches MultimodalRoutes' station preparation (active, DLC visibility, finite entrance);
    /// a station the planner later refuses (for example near a gate) leaves its visitors waiting, never relocated.</summary>
    internal static class NativePortalStations
    {
        public static List<PortalStation> Read()
        {
            var stations = new List<PortalStation>();
            foreach (var station in Object.FindObjectsByType<SubwayStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(station => station.GetInstanceID()))
            {
                if (station == null || !station.gameObject.activeInHierarchy
                    || station.GetComponentsInParent<ToggleObjectDLC>(true).Any(t => t.showWhenOwned != t.dlc.DlcIsOwned())) continue;
                Vector3 entrance;
                try { entrance = station.GetNavMeshTargetPosition(); }
                catch (Exception) { continue; }
                var position = new Position(entrance.x, entrance.y, entrance.z);
                // The first station of a duplicated name wins, as in route preparation; PortalPlaces skips repeats.
                if (position.IsFinite) stations.Add(new PortalStation("subway:" + station.stationName, position));
            }
            return stations;
        }
    }
}
