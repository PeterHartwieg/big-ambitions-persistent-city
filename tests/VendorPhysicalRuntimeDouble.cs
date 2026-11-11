// Native scene doubles only. E6e tests compile the complete production DwellSpots and PhysicalPopulation.
// Movement advances only when a test explicitly moves an actor; path commands alone never prove arrival.
using System;
using System.Collections.Generic;
using System.Reflection;
using PersistentCity.Core;
using UnityEngine;

namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 up => new(0, 1, 0);
        public float sqrMagnitude => x*x + y*y + z*z;
        public void Normalize() { float n = MathF.Sqrt(sqrMagnitude); if (n > 0) { x /= n; y /= n; z /= n; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x, a.y-b.y, a.z-b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b, a.y*b, a.z*b);
        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt((a-b).sqrMagnitude);
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 _) => new(a.x, 0, a.z);
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
    }
    public struct Vector2
    {
        private readonly float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => MathF.Sqrt(x*x+y*y);
    }
    public static class Mathf { public static float Abs(float value) => MathF.Abs(value); }
    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime, unscaledDeltaTime = 0.1f, deltaTime = 0.1f;
        public static void Next(float seconds = 0.3f) { frameCount++; unscaledTime += seconds; unscaledDeltaTime = deltaTime = seconds; }
    }
    public sealed class Transform
    {
        public Vector3 position, forward = new(0, 0, 1), lossyScale = new(1, 1, 1);
        public object? Component;
        public T? GetComponent<T>() where T : class => Component as T;
    }
    public sealed class CapsuleCollider { public float height = 1.8f; }
    public sealed class Camera { public Vector3 WorldToScreenPoint(Vector3 p) => p; }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public static class Screen { public static int height => 1000; }
}
namespace UnityEngine.AI
{
    public struct NavMeshQueryFilter { }
    public enum NavMeshPathStatus { PathComplete, PathInvalid }
    public sealed class NavMeshPath { public NavMeshPathStatus status = NavMeshPathStatus.PathComplete; }
    public sealed class NavMeshAgent
    {
        public bool isStopped = true, hasPath, isPathStale;
        public void ResetPath() { hasPath = false; }
    }
    public static partial class NavMesh
    {
        public static Func<Vector3, Vector3?>? Sample;
        public static Func<Vector3, Vector3, bool>? Blocked;
        public static int Samples;
        public static bool SamplePosition(Vector3 p, out NavMeshHit hit, float radius, NavMeshQueryFilter _)
        {
            Samples++;
            var point = Sample == null ? (Vector3?)p : Sample(p);
            hit = new NavMeshHit { position = point ?? default };
            return point.HasValue;
        }
        public static bool CalculatePath(Vector3 a, Vector3 b, NavMeshQueryFilter _, NavMeshPath path)
        { path.status = NavMeshPathStatus.PathComplete; return true; }
        public static bool Raycast(Vector3 a, Vector3 b, out NavMeshHit hit, NavMeshQueryFilter _)
        { hit = default; return Blocked?.Invoke(a, b) == true; }
        public static void Reset() { Sample = null; Blocked = null; Samples = 0; }
    }
}
namespace Buildings
{
    public sealed class CityManager
    {
        public static CityManager? Instance => null;
        public readonly List<Controller> cityBuildingControllers = new();
    }
    public sealed class Controller { public readonly List<Door> entranceDoors = new(); }
    public sealed class Door { public readonly Transform doorTransform = new(); }
}
namespace BAModAPI
{
    public interface IModLogger { void Info(string value); void Warn(string value); }
}
namespace BigAmbitions.DayNightCycle
{
    public static class RainHelper { public static bool IsRaining => false; }
}
namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        internal StreetFoodVendorMarket? vendorMarket;
    }
    internal static class WalkingRoutes
    {
        internal static UnityEngine.AI.NavMeshQueryFilter DryFilter => default;
    }
    internal static class BuildingManager { internal static bool IsInsideBuilding => false; }
    internal static class RainHelper { internal static bool isRaining => false; }
    internal static class NativeTravelPace
    {
        internal static bool HiddenTravel => false;
        internal static Position Focus = new(100, 0, 0);
        internal static double WalkingMetresPerSecond => 1.4;
    }
    internal static class NativeInteriorCityView
    {
        internal static bool Configured => false;
        internal static InteriorViewState State => new();
        internal static double AdmissionRadius(double r) => r;
        internal static double ReleaseRadius(double r) => r * 1.2;
        internal static int Pedestrians(int n) => n;
        internal static int Vehicles(int n) => n;
        internal static void Suspend() { }
    }
    internal sealed class AmbientPedestrians : IDisposable
    {
        internal static readonly HashSet<string> Reserved = new();
        internal AmbientPedestrians(Assembly _, object __) { }
        internal bool Active { get; private set; }
        internal bool Ready => true;
        internal string LastError => "";
        internal long Resumed => 0;
        internal bool IsReserved(string id) => Reserved.Contains(id);
        internal void Activate() { Active = true; }
        internal void Update() { }
        public void Dispose() { Active = false; }
    }
    internal sealed class MultimodalRoutes
    {
        // F2d: car tests opt in to a road sample at the journey pose; every other test keeps cars without bodies.
        internal static bool CarSamples;
        internal bool ReadyForPopulation => true;
        internal object ResolveWaypoint(int _) => new();
        internal bool TryPhysicalRoadSample(CitizenState c, PhysicalJourneySample s, out PhysicalRoadSample sample)
        { sample = new PhysicalRoadSample { Position = s.Position }; return CarSamples; }
    }
    internal struct PhysicalRoadSample { internal Position Position; }
    // Native car pool double (F2d). A body moves only when a test drives it: Drive applies the production CommandLimit
    // the way the native fixed step does, at a native speed the test chooses, and raises Gley's reached marker at a
    // corner the test names.
    internal sealed class PhysicalVehicles : IDisposable
    {
        internal static PhysicalVehicles? Last;
        private readonly int maximum;
        internal readonly Dictionary<string, PhysicalVehicleBody> Active = new();
        internal int Acquisitions;
        internal PhysicalVehicles(Func<int, object> _, int maximum) { this.maximum = maximum; Last = this; }
        internal Action? SuspendForNativeFastForward;
        internal Func<PhysicalVehicleBody, PhysicalTrafficCommandLimit>? CommandLimit;
        internal Func<PhysicalVehicleBody, int, bool>? NativeTargetPermission;
        internal bool Ready => true;
        internal string LastError => "";
        internal string LastAdmissionResident => "";
        internal string LastAdmissionStage => "";
        internal string LastAdmissionError => "";
        internal bool Activate() => true;
        internal void AssertHealthy() { }
        internal void Update() { }
        internal bool TryAcquire(CitizenState c, PhysicalRoadSample s, out PhysicalVehicleBody? body)
        {
            body = null;
            if (Active.ContainsKey(c.Identity.Id) || Active.Count >= maximum) return false;
            body = new PhysicalVehicleBody(c.Identity.Id, s.Position); Active.Add(c.Identity.Id, body); Acquisitions++;
            return true;
        }
        internal void Release(PhysicalVehicleBody body)
        {
            if (!Active.TryGetValue(body.CitizenId, out var current) || !ReferenceEquals(current, body)) return;
            if (body.Valid) body.SetFade(1);
            Active.Remove(body.CitizenId); body.Valid = false;
        }
        /// <summary>One native fixed step for every car: move along the commanded direction by at most the command's
        /// advance (nothing while held), at <paramref name="speed"/> metres per second; a car standing on a corner
        /// <paramref name="corner"/> returns raises Gley's reached marker for it.</summary>
        internal static float LastStepSeconds = 1 / 60f;
        internal bool? NativeRed(PhysicalVehicleBody body) => body.NativeRed;
        internal void Drive(float seconds, double speed, Func<PhysicalVehicleBody, Position?>? corner = null)
        {
            LastStepSeconds = seconds;
            foreach (var body in new List<PhysicalVehicleBody>(Active.Values))
            {
                if (!body.Valid || CommandLimit == null) continue;
                var limit = CommandLimit(body);
                double step = limit.HoldPosition ? 0 : Math.Min(limit.MaximumAdvance, speed * body.Pace * seconds);
                var d = limit.Direction;
                body.Moved = step;
                body.Position = new Position(body.Position.X + d.X * step, body.Position.Y + d.Y * step, body.Position.Z + d.Z * step);
                var at = corner?.Invoke(body);
                if (at.HasValue && Position.Distance(body.Position, at.Value) <= 1e-6) body.Reach(at.Value);
            }
        }
        public void Dispose() { foreach (var body in Active.Values) body.Valid = false; Active.Clear(); }
    }
    internal sealed class PhysicalVehicleBody
    {
        internal PhysicalVehicleBody(string citizenId, Position at) { CitizenId = citizenId; Position = at; }
        internal Position Position;
        internal string CitizenId { get; }
        internal bool Valid = true;
        // Native speed multiple a test can lower (a slow car), and the advance of the last Drive step.
        internal double Pace = 1, Moved;
        private readonly Queue<Position> reached = new();
        internal void Reach(Position corner) { if (reached.Count == 0 || Position.Distance(reached.Peek(), corner) > .01) reached.Enqueue(corner); }
        internal bool TryPeekReached(Position p, out Position marker)
        {
            marker = default;
            if (!Valid || reached.Count == 0 || Position.Distance(reached.Peek(), p) > .01) return false;
            marker = reached.Peek(); return true;
        }
        internal bool ConsumeReached(Position p) { if (!TryPeekReached(p, out _)) return false; reached.Dequeue(); return true; }
        // F2d follower hooks: native speed (from the last Drive step), the test-controlled on-screen flag,
        // whether the car's materials can fade, and the fade itself.
        internal double Speed => Moved / Math.Max(1e-6, PhysicalVehicles.LastStepSeconds);
        internal bool OnScreen;
        internal bool CanFade;
        internal float Alpha = 1;
        internal readonly List<float> Fades = new();
        internal void SetFade(float alpha) { Alpha = alpha; Fades.Add(alpha); }
        // Gley's target waypoint light for this car: true red, false green, null unknown.
        internal bool? NativeRed;
    }
    internal sealed class Actor
    {
        internal readonly Transform transform = new();
    }
    internal sealed class PhysicalPedestrianBody
    {
        internal PhysicalPedestrianBody(Actor actor, string id, long generation)
        { Character = actor; IdentityId = id; Generation = generation; }
        internal readonly Actor Character;
        internal readonly UnityEngine.AI.NavMeshAgent Agent = new();
        internal CapsuleCollider? Capsule => null;
        internal readonly string IdentityId;
        internal readonly long Generation;
        internal bool Valid = true;
        internal Position Position => Point(Character.transform.position);
        internal Position Target, Facing;
        // F2b: a test can make a target unreachable (no complete NavMesh path).
        internal static Func<Position, bool>? Unreachable;
        internal bool SetPathTarget(Position p, double _)
        { if (Unreachable?.Invoke(p) == true) return false; Target = p; Agent.hasPath = true; Agent.isPathStale = false; Agent.isStopped = false; return true; }
        internal bool SetTarget(Position p, double speed) { SetTargets++; return SetPathTarget(p, speed); }
        // F2c: a pose is projected onto the walkable surface through the NavMesh.Sample hook; SetTarget counts its calls.
        internal int SetTargets;
        internal bool TryProjectPose(Position p, out Position projected)
        {
            projected = p;
            if (!UnityEngine.AI.NavMesh.SamplePosition(Vector(p), out var hit, 0.5f, default(UnityEngine.AI.NavMeshQueryFilter))) return false;
            projected = Point(hit.position); return true;
        }
        internal void Stop(bool value) { Agent.isStopped = value; }
        // P-C: the agent still holds the path SetTarget gave it (a warp drops it; a test may drop it too).
        internal bool HasPath => Valid && Agent.hasPath && !Agent.isPathStale;
        // F2c follower hooks: the commanded pace, an off-screen move, and the test-controlled on-screen flag.
        internal double Speed;
        internal int Warps;
        internal bool OnScreen;
        internal void Pace(double v) { Speed = v > 0 ? v : 0; Agent.isStopped = !(v > 0); }
        internal float Alpha = 1;
        // F2c: whether this body's materials can show a fade; tests decide (the production check is WalkerFollow.MaterialCanFade).
        internal bool CanFade = true;
        internal readonly List<float> Fades = new();
        internal void SetFade(float alpha) { Alpha = alpha; Fades.Add(alpha); }
        internal bool Warp(Position p) { Character.transform.position = Vector(p); Agent.hasPath = false; Agent.isStopped = true; Warps++; return true; }
        internal void Face(Position p) { Facing = p; }
        internal void Tick() { }
        internal static Vector3 Vector(Position p) => new((float)p.X, (float)p.Y, (float)p.Z);
        internal static Position Point(Vector3 p) => new(p.x, p.y, p.z);
    }
    // Pool-contract double: a recycled actor gets a new immutable identity lease. Selection/dwell code is production.
    internal sealed class PhysicalPedestrians : IDisposable
    {
        internal static PhysicalPedestrians? Last;
        private readonly int maximum;
        private readonly Stack<Actor> idle = new();
        internal readonly Dictionary<string, PhysicalPedestrianBody> Active = new();
        internal readonly List<(CitizenIdentity Identity, Actor Actor, long Generation)> Acquisitions = new();
        private int actors;
        private long generation;
        internal PhysicalPedestrians(Transform _, int maximum) { this.maximum = maximum; Last = this; }
        // F2c: whether a point is in the camera's view; tests decide. Unset means out of view.
        internal static Func<Position, bool>? VisiblePoint;
        // P-C: the production pool calls this right before each native navigation update; tests call it before moving bodies.
        internal Action? BeforeNavigation { get; set; }
        internal bool Visible(Position at) => VisiblePoint?.Invoke(at) == true;
        internal bool TryAcquire(CitizenIdentity identity, Position position, out PhysicalPedestrianBody? body)
        {
            body = null;
            if (Active.ContainsKey(identity.Id) || Active.Count >= maximum) return false;
            Actor actor;
            if (idle.Count > 0) actor = idle.Pop();
            else { if (actors >= maximum) return false; actors++; actor = new(); }
            actor.transform.position = PhysicalPedestrianBody.Vector(position);
            body = new(actor, identity.Id, ++generation); Active.Add(identity.Id, body);
            Acquisitions.Add((identity, actor, generation)); return true;
        }
        internal void Release(PhysicalPedestrianBody body)
        {
            if (!Active.TryGetValue(body.IdentityId, out var current) || !ReferenceEquals(current, body)) return;
            Active.Remove(body.IdentityId); body.Valid = false; idle.Push(body.Character);
        }
        public void Dispose() { foreach (var body in Active.Values) body.Valid = false; Active.Clear(); }
    }
    internal sealed partial class PhysicalPopulation { }
    internal struct ResidentPickAccumulator
    {
        internal static bool Hit(float x, float y, float fx, float fy, float hx, float hy, float width, out float score) { score = 0; return false; }
        internal void Consider(string id, long generation, float score, float depth) { }
    }
}
