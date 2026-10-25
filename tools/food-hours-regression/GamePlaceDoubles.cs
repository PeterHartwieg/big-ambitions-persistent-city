using System.Collections.Generic;
using PersistentCity.Core;

public sealed class Building
{
    public string BuildingType = "ba:buildingtype_retail", StreetName = "Test", BuildingSize = "small", Neighbourhood = "district", requiredDLC = "";
    public int StreetNumber = 7, trafficIndex = 50, locatedInsidePrivateFenceIndex = -1;
}
public sealed class Entrance { public UnityEngine.Transform doorTransform = new(); }
public sealed class CityBuildingController
{
    public Building building = new(); public bool blockPedestrianSpawn;
    public Entities.BuildingRegistration buildingRegistration = new();
    public List<Entrance> entranceDoors = new() { new() };
}
public sealed class CityManager { public static CityManager Instance = new(); public List<CityBuildingController> cityBuildingControllers = new(); }
namespace Buildings
{
    public sealed class BuildingSizeData { public string buildingSize = "small"; public int squareMeters = 30; }
    public static class BuildingSizeHelper { public static BuildingSizeData[] GetAllBuildingSizes() => new[] { new BuildingSizeData() }; }
}
namespace Extensions
{
    public static class NativeExtensions
    {
        public static bool DlcIsOwned(this string dlc) => true;
        public static bool IsHamptonsAIVilla(this Building building) => false;
    }
}
namespace Helpers { public static class BuildingHelper { public static bool IsHamptonsBuildingOwnedByRival(Building building) => false; } }
namespace UnityEngine
{
    public readonly struct Vector3
    {
        public readonly float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public sealed class Transform { public Vector3 position = new(1, 0, 1), forward = new(0, 0, 1); }
    public static class Debug { public static void LogWarning(string message) { } }
}
namespace UnityEngine.AI
{
    public struct NavMeshHit { public UnityEngine.Vector3 position; }
    public static class NavMesh
    {
        public static bool SamplePosition(UnityEngine.Vector3 point, out NavMeshHit hit, float distance, int filter)
        { hit = new NavMeshHit { position = point }; return true; }
    }
}
namespace PersistentCity.Runtime
{
    internal static class WalkingRoutes { public static int DryFilter => 0; }
    internal static class OutdoorPlaces { public static List<Place> Read(List<Place> places) => new(); }
}
