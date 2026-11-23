using Buildings;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private void GateCommand(string action, string detail, string value)
        {
            if (action != "status")
            { Debug.LogWarning("[Persistent City] Unknown command. Use: " + GateStatusReport.Usage); return; }
            if (!GateStatusReport.TryParse(detail, value, out var source, out bool list, out double x, out double z, out double radius, out string error))
            { Debug.LogWarning("[Persistent City] Unknown command (" + error + "). Use: " + GateStatusReport.Usage); return; }
            var current = routes;
            var access = current?.Access;
            if (failed || current == null || access == null)
            { Debug.LogWarning("[Persistent City] gate status unavailable: no active Persistent City routes or gate snapshot yet."); return; }
            var roads = current.Roads;
            string state = roads == null ? "none" : roads.Ready ? "ready" : "building";
            Debug.Log(GateStatusReport.Format(access.Readings(), Building, roads?.Nodes, state,
                index => roads?.RoadId(index) ?? -1, source, list, x, z, radius));
        }

        private static string Building(FenceDoor door)
        {
            if (door == null) return "gone";
            var building = door.GetComponentInParent<CityBuildingController>(true);
            return building == null ? "none" : building.name;
        }
    }
}
