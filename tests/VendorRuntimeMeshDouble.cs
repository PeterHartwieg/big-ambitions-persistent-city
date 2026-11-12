namespace UnityEngine
{
    public partial struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
namespace UnityEngine.AI
{
    public struct NavMeshHit { public UnityEngine.Vector3 position; }
    public static partial class NavMesh
    {
        public const int AllAreas = -1;
        public static bool SamplePosition(UnityEngine.Vector3 p, out NavMeshHit hit, float radius, int mask)
        {
            hit = new NavMeshHit { position = new UnityEngine.Vector3(p.x, 0, p.z) };
            return System.Math.Abs(p.x) < 1000 && System.Math.Abs(p.z) < 1000 && System.Math.Abs(p.y) <= radius && mask == AllAreas;
        }
    }
}
