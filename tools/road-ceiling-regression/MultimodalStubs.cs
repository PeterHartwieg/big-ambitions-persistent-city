using System;
using PersistentCity.Core;
using UnityEngine;

namespace UnityEngine
{
    public static class Time { public static int frameCount; }
    public sealed class GameObject { public bool activeInHierarchy = true; }
}
public sealed class CityManager
{
    public static CityManager? Instance;
    public SubwaySystem subwaySystem = new SubwaySystem();
}
public sealed class SubwaySystem
{
    public Vector3[]? manhattanBridgeIcToLm;
    public Vector3[]? manhattanBridgeLmToIc;
}
public sealed class SubwayStation
{
    private static int nextId;
    private readonly int instanceId = ++nextId;
    public int GetInstanceID() => instanceId;
    public string stationName = "", neighbourhood = "";
    public Vector3 Entrance;
    public Transform transform = new Transform();
    public GameObject gameObject = new GameObject();
    public Vector3 GetNavMeshTargetPosition() => Entrance;
    public T[] GetComponentsInParent<T>(bool inactive) => Array.Empty<T>();
}
public sealed class ToggleObjectDLC
{
    public bool showWhenOwned;
    public Dlc dlc = new Dlc();
}
public sealed class Dlc { public bool DlcIsOwned() => true; }

namespace PersistentCity.Runtime
{
    // Deterministic complete access/egress routes stand in for NavMesh. The actual car planner,
    // native snapshot, traffic network and multimodal comparison are compiled without changes.
    internal sealed class WalkingRoutes
    {
        public NativeRouteMetrics Metrics { get; } = new NativeRouteMetrics();
        internal static Func<Place, Place, Route?> Routes = (_, _) => null;
        internal static UnityEngine.AI.NavMeshQueryFilter DryFilter => default;
        private readonly Func<Position, Position, bool>? clear;
        public WalkingRoutes(double speed, Func<Position, Position, bool>? clear) { this.clear = clear; }
        public void Clear() { }
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
            => throw new NotSupportedException("This road harness does not validate cached native walking quotes.");
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            var route = Routes(from, to);
            if (route == null) return null;
            double length = 0;
            foreach (var leg in route.Legs)
            {
                length += Position.Distance(leg.From, leg.To);
                if (clear != null && !clear(leg.From, leg.To)) return null;
            }
            return length <= preferences.WalkingToleranceMetres ? route : null;
        }
    }
}
