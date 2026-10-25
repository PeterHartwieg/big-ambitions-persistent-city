using System;
namespace Extensions { public static class Marker { } }
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
namespace UnityEngine.AI
{
    public struct NavMeshQueryFilter { public int areaMask; }
    public struct NavMeshHit { public UnityEngine.Vector3 position; }
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public sealed class NavMeshPath
    {
        public NavMeshPathStatus status;
        public UnityEngine.Vector3[] corners = Array.Empty<UnityEngine.Vector3>();
    }
    public static class NavMesh
    {
        public static UnityEngine.Vector3[] Corners = Array.Empty<UnityEngine.Vector3>();
        public static bool Calculated = true, Sampled = true;
        public static NavMeshPathStatus Status = NavMeshPathStatus.PathComplete;
        public static Func<UnityEngine.Vector3, UnityEngine.Vector3>? SampleTransform;
        public static int PathCalls, SampleCalls, LastMask, RaycastCalls;
        public static bool SamplePosition(UnityEngine.Vector3 p, out NavMeshHit hit, float distance, NavMeshQueryFilter filter)
        {
            SampleCalls++; LastMask = filter.areaMask;
            hit = new NavMeshHit { position = SampleTransform == null ? p : SampleTransform(p) };
            return Sampled;
        }
        public static bool CalculatePath(UnityEngine.Vector3 from, UnityEngine.Vector3 to, NavMeshQueryFilter filter, NavMeshPath path)
        {
            PathCalls++; LastMask = filter.areaMask;
            path.status = Status; path.corners = (UnityEngine.Vector3[])Corners.Clone(); return Calculated;
        }
        // Model a complete native corner path whose independently sampled segments
        // are refused by raycast at polygon boundaries, as suspected in the K2 capture.
        public static bool Raycast(UnityEngine.Vector3 from, UnityEngine.Vector3 to, out NavMeshHit hit, NavMeshQueryFilter filter)
        { RaycastCalls++; hit = new NavMeshHit { position = to }; return true; }
    }
}
public static class NavMeshHelper
{
    public static UnityEngine.AI.NavMeshQueryFilter NpcNavMeshFilter => new UnityEngine.AI.NavMeshQueryFilter { areaMask = 15 };
    public static int SwimmingAreaMask => 8;
}
