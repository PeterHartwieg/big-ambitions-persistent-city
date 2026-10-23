using System;
using System.Collections.Generic;
using PersistentCity.Core;
namespace Extensions { public static class Marker { } }
namespace UnityEngine
{
    public class Object
    {
        public static int SceneSearches, StationSearches, DoorSearches;
        public bool Destroyed;
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) ||
            (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static object? Scene;
        public static T? FindObjectOfType<T>() where T : class { StagingHarness.Guard.Read(); SceneSearches++; return Scene as T; }
        public static object[] SceneObjects = Array.Empty<object>();
        public static T[] FindObjectsOfType<T>(bool inactive)
        {
            StagingHarness.Guard.Read();
            var result = new List<T>(); foreach (var item in SceneObjects) if (item is T value) result.Add(value);
            return result.ToArray();
        }
        public static FenceDoor[] Doors = Array.Empty<FenceDoor>();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort)
        {
            StagingHarness.Guard.Read();
            if (typeof(T) == typeof(FenceDoor)) { DoorSearches++; return (T[])(object)Doors.Clone(); }
            StationSearches++;
            var result = new List<T>(); foreach (var item in SceneObjects) if (item is T value) result.Add(value);
            return result.ToArray();
        }
    }
    public enum FindObjectsInactive { Exclude, Include }
    public enum FindObjectsSortMode { None, InstanceID }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public float sqrMagnitude => x*x + y*y + z*z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x/b,a.y/b,a.z/b);
    }
    public struct Bounds
    {
        private Vector3 center;
        public Vector3 size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 min => center - size/2;
        public Vector3 max => center + size/2;
        public void Expand(Vector3 value) { size += value; }
        public void Encapsulate(Vector3 value) => Encapsulate(new Bounds(value, zero));
        public void Encapsulate(Bounds value)
        {
            var low = new Vector3(Math.Min(min.x, value.min.x), Math.Min(min.y, value.min.y), Math.Min(min.z, value.min.z));
            var high = new Vector3(Math.Max(max.x, value.max.x), Math.Max(max.y, value.max.y), Math.Max(max.z, value.max.z));
            center = (low + high) / 2; size = high - low;
        }
        private static Vector3 zero => Vector3.zero;
    }
    public class Transform
    {
        public Vector3 position; public Transform? parent;
        public Vector3 TransformPoint(Vector3 p) => position + p;
        public bool IsChildOf(Transform other) { for (var t = this; t != null; t = t.parent) if (t == other) return true; return false; }
    }
    public class Collider { public Bounds bounds; public Transform transform = new Transform(); public T[] GetComponentsInChildren<T>(bool inactive) => Array.Empty<T>(); }
    public sealed class BoxCollider : Collider { public Vector3 size, center; }
    public static class LayerMask { public static int GetMask(params string[] names) => 1; }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics { public static bool Linecast(Vector3 a, Vector3 b, int mask, QueryTriggerInteraction query) => false; }
}
namespace UnityEngine.AI
{
    public struct NavMeshQueryFilter { }
    public struct NavMeshHit { public UnityEngine.Vector3 position; }
    public static class NavMesh
    {
#if MULTIMODAL_REGRESSION
        public static Func<UnityEngine.Vector3, UnityEngine.Vector3>? SampleTransform;
#endif
        public static bool SamplePosition(UnityEngine.Vector3 p, out NavMeshHit hit, float max, NavMeshQueryFilter filter)
        {
#if MULTIMODAL_REGRESSION
            if (SampleTransform != null) p = SampleTransform(p);
#endif
            hit = new NavMeshHit { position = p }; return true;
        }
    }
}
public class FenceDoor
{
    public bool acceptsVehicles;
    public string name = "";
    public UnityEngine.Transform[]? doors;
    public bool IsForSale() => false;
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public UnityEngine.Collider[] Colliders = Array.Empty<UnityEngine.Collider>();
    public T[] GetComponentsInChildren<T>(bool inactive) => typeof(T) == typeof(UnityEngine.Collider) ? (T[])(object)Colliders.Clone() : Array.Empty<T>();
}
public sealed class HamptonsPrivateFenceDoor : FenceDoor { }
namespace PersistentCity.Runtime
{
#if !MULTIMODAL_REGRESSION
    internal static class WalkingRoutes { internal static UnityEngine.AI.NavMeshQueryFilter DryFilter => default; }
#endif
    internal readonly struct PhysicalRoadSample
    {
        public PhysicalRoadSample(Position position, Position directionTarget, int spawnNode, IReadOnlyList<int> remainingRoadNodes, string topologyId, IReadOnlyList<RoadBoundary> admissionBoundaries, double routeProgress)
        { Position=position; DirectionTarget=directionTarget; SpawnNode=spawnNode; RemainingRoadNodes=remainingRoadNodes; TopologyId=topologyId; AdmissionBoundaries=admissionBoundaries; RouteProgress=routeProgress; }
        public Position Position { get; }
        public Position DirectionTarget { get; }
        public int SpawnNode { get; }
        public IReadOnlyList<int> RemainingRoadNodes { get; }
        public string TopologyId { get; }
        public IReadOnlyList<RoadBoundary> AdmissionBoundaries { get; }
        public double RouteProgress { get; }
    }
}
