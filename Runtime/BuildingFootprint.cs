using Buildings;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class BuildingFootprint
    {
        public static Bounds? ReadBounds(CityBuildingController controller, bool usableOnly = false)
        {
            Bounds? combined = null;
            foreach (var collider in controller.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var bounds = collider.bounds;
                if (usableOnly && !Usable(bounds)) continue;
                if (combined is Bounds sum) { sum.Encapsulate(bounds); combined = sum; }
                else combined = bounds;
            }
            return combined;
        }

        public static bool Usable(Bounds world) =>
            new Position(world.min.x, world.min.y, world.min.z).IsFinite && new Position(world.max.x, world.max.y, world.max.z).IsFinite
            && world.size.sqrMagnitude >= 0.0001f;
    }
}
