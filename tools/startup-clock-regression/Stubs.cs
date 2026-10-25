using System;
using System.Reflection;
namespace UnityEngine
{
    public static class Time { public static int frameCount; public static float deltaTime = 1, timeScale = 1; }
    public struct Vector3 { public float x, y, z; }
    public sealed class Transform { public Vector3 position; }
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
public static class BuildingManager { public static bool IsInsideBuilding; }
namespace UI { public sealed class UIs { public TimeMachine timeMachine = new TimeMachine(); } public sealed class TimeMachine { public bool isRunning; } }
namespace UI.Load { public static class LoadScene { public static bool isLoading; } }
namespace Parking.UndergroundParking { public static class UndergroundParkingManager { public static bool IsInsideParking; } }
namespace PersistentCity.Runtime
{
    internal static class NativeCityClock { public static bool Active; public static double Factor = 1; }
    internal static class PhysicalTrafficHooks { public static bool CurrentFastForward = false; }
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
