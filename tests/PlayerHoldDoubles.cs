// F2e native doubles for Runtime/PlayerContact.cs: the game's player controller, the vehicle the player drives and
// Unity's log. Only these can be the player; every other body in the scene doubles is a non-player actor.
using System.Collections.Generic;

public class InstanceBehavior<T> where T : class
{
    public static T? Instance { get; set; }
}

public sealed class GameManager
{
    public PlayerControllerDouble? playerController;
    public VehicleController? selectedVehicle;
    public static bool Driving;
    public static bool IsDrivingVehicle() => Driving;
}

public sealed class PlayerControllerDouble { public UnityEngine.Transform transform = new(); }
public sealed class VehicleController { public UnityEngine.Transform transform = new(); }

namespace UnityEngine
{
    public static class Debug
    {
        private static readonly object Gate = new();
        private static readonly List<string> lines = new();
        public static void Log(object message) { lock (Gate) lines.Add(message?.ToString() ?? ""); }
        public static List<string> Lines() { lock (Gate) return new List<string>(lines); }
    }
}
