using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 zero => default;
        public static Vector3 operator /(Vector3 a, float b) => new(a.x / b, a.y / b, a.z / b);
        public static Vector3 Min(Vector3 a, Vector3 b) => new(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y), MathF.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y), MathF.Max(a.z, b.z));
    }

    public struct Bounds
    {
        private Vector3 extents;
        public Vector3 center { get; set; }
        public Vector3 size
        {
            get => extents * 2f;
            set => extents = value * 0.5f;
        }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            extents = size * 0.5f;
        }

        public void Expand(Vector3 amount) => extents += amount * 0.5f;

        public void Encapsulate(Vector3 point)
        {
            Vector3 lo = Vector3.Min(min, point);
            Vector3 hi = Vector3.Max(max, point);
            extents = (hi - lo) * 0.5f;
            center = lo + extents;
        }

        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.min);
            Encapsulate(bounds.max);
        }
    }

    public enum FindObjectsInactive { Exclude, Include }
    public enum FindObjectsSortMode { None, InstanceID }

    public class Object
    {
        public string name = "";
        public static Func<Type, Object[]>? FindByType;
        public static T[] FindObjectsByType<T>(FindObjectsInactive includeInactive, FindObjectsSortMode sortMode) where T : Object
            => FindByType == null ? Array.Empty<T>() : FindByType(typeof(T)).OfType<T>().ToArray();
    }

    public sealed partial class Transform
    {
        private Transform? parentTransform;
        private List<Transform>? children;
        private List<Component>? components;
        public bool ActiveSelf = true;
        public Transform? parent
        {
            get => parentTransform;
            set => SetParent(value);
        }
        public bool ActiveInHierarchy
        {
            get
            {
                for (var node = this; node != null; node = node.parentTransform)
                    if (!node.ActiveSelf) return false;
                return true;
            }
        }

        public void SetParent(Transform? next)
        {
            if (ReferenceEquals(parentTransform, next)) return;
            parentTransform?.children?.Remove(this);
            parentTransform = next;
            if (next != null)
            {
                next.children ??= new List<Transform>();
                next.children.Add(this);
            }
        }

        public void Attach(Component component)
        {
            components ??= new List<Component>();
            if (!components.Contains(component)) components.Add(component);
        }

        public bool IsChildOf(Transform candidate)
        {
            for (var node = this; node != null; node = node.parentTransform)
                if (ReferenceEquals(node, candidate)) return true;
            return false;
        }

        public Vector3 TransformPoint(Vector3 local)
            => new(position.x + local.x * lossyScale.x, position.y + local.y * lossyScale.y, position.z + local.z * lossyScale.z);

        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var found = new List<T>();
            Collect(this, includeInactive, found);
            return found.ToArray();
        }

        private static void Collect<T>(Transform node, bool includeInactive, List<T> found) where T : class
        {
            if (!includeInactive && !node.ActiveInHierarchy) return;
            if (node.components != null)
                foreach (var component in node.components)
                    if (component is T match) found.Add(match);
            if (node.children != null)
                foreach (var child in node.children)
                    Collect(child, includeInactive, found);
        }

        public T? GetComponentInParent<T>(bool includeInactive) where T : class
        {
            for (var node = this; node != null; node = node.parentTransform)
            {
                if (!includeInactive && !node.ActiveInHierarchy) continue;
                if (node.components != null)
                    foreach (var component in node.components)
                        if (component is T match) return match;
            }
            return null;
        }
    }

    public class Component : Object
    {
        private Transform? attachedTransform;
        public Transform transform
        {
            get
            {
                if (attachedTransform == null) { attachedTransform = new Transform(); attachedTransform.Attach(this); }
                return attachedTransform;
            }
            set
            {
                attachedTransform = value;
                value.Attach(this);
            }
        }

        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
            => transform.GetComponentsInChildren<T>(includeInactive);

        public T? GetComponentInParent<T>(bool includeInactive = false) where T : class
            => transform.GetComponentInParent<T>(includeInactive);
    }

    public class Collider : Component
    {
        public bool enabled = true;
        public bool isTrigger;
        public Bounds bounds;
    }

    public sealed class BoxCollider : Collider
    {
        public Vector3 center;
        public Vector3 size;
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public Bounds bounds;
    }
}

public class FenceDoor : UnityEngine.Component
{
    public UnityEngine.Transform[]? doors;
    public bool acceptsVehicles = true;
    public bool ForSale;
    public bool IsForSale() => ForSale;
}

public sealed class HamptonsPrivateFenceDoor : FenceDoor
{
    public int privateFenceIndex;
}
