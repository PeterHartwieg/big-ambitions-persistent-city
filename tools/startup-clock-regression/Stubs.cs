using System;
using System.Reflection;
namespace UnityEngine
{
    public static class Time { public static int frameCount; public static float unscaledTime; public static float deltaTime = 1, timeScale = 1; }
    public struct Vector3 { public float x, y, z; }
    public sealed class Transform
    {
        public Vector3 position;
        public Transform? parent;
        public bool IsChildOf(Transform ancestor) => ReferenceEquals(this, ancestor) || parent?.IsChildOf(ancestor) == true;
    }
}
public class InstanceBehavior<T> where T : class { public static T? Instance; }
public sealed class PlayerController { public UnityEngine.Transform transform = new UnityEngine.Transform(); }
public sealed class GameManager
{
    private static float MinutesMultiplier = 1;
    public static void SetMultiplier(float value) { MinutesMultiplier = value; }
    public PlayerController playerController = new PlayerController();
    public void RunMainGameTick(float value) { _ = MinutesMultiplier * value; }
}
public sealed class BuildingManager
{
    public static bool IsInsideBuilding;
    public bool enteringBuilding, exitingBuilding;
    public Building? building;
    public BuildingRegistration? buildingRegistration;
    public CityBuildingController? cityBuildingController;
    public UnityEngine.Transform? currentBuildingVersion;
    public object? multipleHeightsBuildingController;
    public System.Collections.Generic.List<ExitZone>? exitZones;
}
public sealed class Building { public string BuildingType = "ba:buildingtype_retail"; public Entities.Address Address; }
public sealed class BuildingRegistration { public bool HasEstablishedBusiness = true; public string businessTypeName = "food"; }
public sealed class CityBuildingController { public Building? building; public BuildingEntranceDoor[]? entranceDoors; }
public sealed class BuildingEntranceDoor { public UnityEngine.Transform? doorTransform; }
public sealed class ExitZone { public bool isElevator; }
public static class GlobalEvents
{
    public static event Action<Entities.Address>? onEnterBuilding, onExitBuilding;
    public static void Enter() => onEnterBuilding?.Invoke(default);
    public static void Exit() => onExitBuilding?.Invoke(default);
}
namespace Entities { public struct Address { } }
namespace BAModAPI { public interface IModLogger { void Info(string text); void Warn(string text); } }
namespace BigAmbitions.InteriorDesigner.InteriorElements
{
    public sealed class InteriorElement { public bool IsFloor = true; public UnityEngine.Transform transform = new UnityEngine.Transform(); }
    public static class InteriorElementsHelper
    {
        public static readonly System.Collections.Generic.Dictionary<string, InteriorElement> InteriorElementsCache = new System.Collections.Generic.Dictionary<string, InteriorElement>();
    }
}
namespace UI { public sealed class UIs { public TimeMachine timeMachine = new TimeMachine(); } public sealed class TimeMachine { public bool isRunning; } }
namespace UI.Load { public static class LoadScene { public static bool isLoading; } }
namespace Parking.UndergroundParking { public static class UndergroundParkingManager { public static bool IsInsideParking; } }
namespace PersistentCity.Runtime
{
    internal static class NativeCityClock { public static bool Active; public static double Factor = 1; }
    internal static class PhysicalTrafficHooks { public static bool CurrentFastForward = false; }
    internal static class GamePlaces { public static bool IsRetailShopActivity(string type) => type != "school"; }
}
namespace HarmonyLib
{
    public sealed class Harmony
    {
        public Harmony(string owner) { }
        public static bool HasAnyPatches(string owner) => false;
        public void Patch(MethodInfo method, HarmonyMethod? postfix = null) { }
        public void UnpatchAll(string owner) { }
    }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] parameters) => type.GetMethod(name, parameters);
    }
}
