using System;
using Buildings;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal static class BuildingFootprint
    {
        public static Bounds? ReadBounds(CityBuildingController controller, bool usableOnly = false)
            => ReadBounds(controller, usableOnly, null, out _);

        public static Bounds? ReadBounds(CityBuildingController controller, bool usableOnly, Func<Transform, bool>? moving, out Bounds? stationary)
        {
            Bounds? combined = null;
            stationary = null;
            foreach (var collider in controller.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var bounds = collider.bounds;
                if (usableOnly && !Usable(bounds)) continue;
                combined = Include(combined, bounds);
                if (moving == null || !moving(collider.transform)) stationary = Include(stationary, bounds);
            }
            return combined;
        }

        private static Bounds Include(Bounds? combined, Bounds bounds)
        {
            if (!(combined is Bounds sum)) return bounds;
            sum.Encapsulate(bounds);
            return sum;
        }

        public static bool Usable(Bounds world) =>
            new Position(world.min.x, world.min.y, world.min.z).IsFinite && new Position(world.max.x, world.max.y, world.max.z).IsFinite
            && world.size.sqrMagnitude >= 0.0001f;
    }
}
