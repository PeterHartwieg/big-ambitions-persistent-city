using System;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 zero => default;
        public static Vector3 operator /(Vector3 value, float divisor) => value * (1f / divisor);
    }

    public struct Bounds
    {
        private Vector3 center;
        public Vector3 size { get; private set; }
        public Vector3 min => center - size / 2;
        public Vector3 max => center + size / 2;

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }

        public void Expand(Vector3 amount) => size += amount;

        public void Encapsulate(Bounds other)
        {
            Encapsulate(other.min);
            Encapsulate(other.max);
        }

        public void Encapsulate(Vector3 point)
        {
            var low = new Vector3(MathF.Min(min.x, point.x), MathF.Min(min.y, point.y), MathF.Min(min.z, point.z));
            var high = new Vector3(MathF.Max(max.x, point.x), MathF.Max(max.y, point.y), MathF.Max(max.z, point.z));
            center = (low + high) / 2;
            size = high - low;
        }
    }

    public sealed partial class Transform
    {
        public Transform? parent;
        public bool IsChildOf(Transform ancestor) => ReferenceEquals(this, ancestor) || parent?.IsChildOf(ancestor) == true;
        public Vector3 TransformPoint(Vector3 local) => position + local;
    }

    public class Collider
    {
        public readonly Transform transform = new();
        public Bounds bounds;
        public bool enabled = true, isTrigger;
    }

    public sealed class BoxCollider : Collider
    {
        public Vector3 center, size;
    }

    public sealed class Renderer
    {
        public readonly Transform transform = new();
        public Bounds bounds;
    }

    public enum FindObjectsInactive { Include }
    public enum FindObjectsSortMode { None }
    public static class Object
    {
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode order) => Array.Empty<T>();
    }
}

namespace Buildings
{
    public sealed partial class CityBuildingController
    {
        public Collider[] Colliders = Array.Empty<Collider>();
        public T[] GetComponentsInChildren<T>() => Colliders.OfType<T>().ToArray();
    }
}

public class FenceDoor
{
    public string name = "";
    public bool acceptsVehicles, ForSale;
    public readonly Transform transform = new();
    public Transform[] doors = Array.Empty<Transform>();
    public Collider[] Colliders = Array.Empty<Collider>();
    public Renderer[] Renderers = Array.Empty<Renderer>();
    public int ParentLookups;

    public bool IsForSale() => ForSale;

    public T[] GetComponentsInChildren<T>(bool includeInactive) =>
        typeof(T) == typeof(Collider) ? Colliders.OfType<T>().ToArray() : Renderers.OfType<T>().ToArray();

    public T? GetComponentInParent<T>(bool includeInactive) where T : class
    {
        ParentLookups++;
        for (Transform? part = transform; part != null; part = part.parent)
            if (part.GetComponent<T>() is T component) return component;
        return null;
    }
}

public sealed class HamptonsPrivateFenceDoor : FenceDoor { }
