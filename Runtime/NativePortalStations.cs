using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using PersistentCity.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
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
                if (position.IsFinite) stations.Add(new PortalStation("subway:" + station.stationName, position));
            }
            return stations;
        }
    }
}
