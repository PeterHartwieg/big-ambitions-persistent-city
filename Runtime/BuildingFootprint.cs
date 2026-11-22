using Buildings;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class BuildingFootprint
    {
        public static Bounds? ReadBounds(CityBuildingController controller)
        {
            Bounds? combined = null;
            foreach (var collider in controller.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var bounds = collider.bounds;
                if (combined is Bounds sum) { sum.Encapsulate(bounds); combined = sum; }
                else combined = bounds;
            }
            return combined;
        }
    }
}
