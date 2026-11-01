// Offline behavioral doubles, independently authored. No native code/assets.
using PersistentCity.Core;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        private static int sequence;
        private readonly int id = ++sequence;
        public string name = "fixture";
        public int GetInstanceID() => id;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public void GetComponents<T>(List<T> output) where T : Component { output.Clear(); foreach (var c in gameObject.Components) { if (c is T t) output.Add(t); else if (c == null && typeof(T) == typeof(Component)) output.Add(null!); } }
        public T? GetComponent<T>() where T : Component => gameObject.Components.OfType<T>().FirstOrDefault();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class GameObject : Object
    {
        public readonly List<Component> Components = new();
        public Transform transform;
        public bool activeInHierarchy = true, isStatic = true;
        public SceneManagement.Scene scene = new();
        public GameObject() { transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T Add<T>() where T : Component, new() { var c = new T { gameObject = this }; Components.Add(c); return c; }
        public T? GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault();
    }
    public class Transform : Component
    {
        public Transform parent = null!;
        private int sibling;
        public List<Transform> Children = new();
        public int childCount => Children.Count;
        public Vector3 position;
        public Matrix4x4 localToWorldMatrix = Matrix4x4.Identity;
        public Transform GetChild(int index) => Children[index];
        public int GetSiblingIndex() => sibling;
        public bool IsChildOf(Transform other) { for (Transform? t = this; t != null; t = t.parent) if (t == other) return true; return false; }
        public void Attach(Transform child) { child.parent = this; child.sibling = Children.Count; Children.Add(child); }
    }
    public readonly struct Vector3
    {
        public readonly float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public float magnitude => MathF.Sqrt(x*x+y*y+z*z);
        public static Vector3 right => new(1,0,0);
        public static Vector3 up => new(0,1,0);
        public static Vector3 forward => new(0,0,1);
    }
    public struct Matrix4x4
    {
        public Vector3 X,Y,Z;
        public static Matrix4x4 Identity => new() { X=Vector3.right,Y=Vector3.up,Z=Vector3.forward };
        public float this[int i] => i==0?X.x:i==1?X.y:i==2?X.z:i==4?Y.x:i==5?Y.y:i==6?Y.z:i==8?Z.x:i==9?Z.y:i==10?Z.z:i==15?1:0;
        public Vector3 MultiplyVector(Vector3 p) => new(X.x*p.x+Y.x*p.y+Z.x*p.z,X.y*p.x+Y.y*p.y+Z.y*p.z,X.z*p.x+Y.z*p.y+Z.z*p.z);
    }
    public struct Bounds { public Vector3 min,max; }
    public class Renderer : Behaviour
    {
        public Bounds bounds;
        public string shadowCastingMode = "On";
        public bool PropertyBlock;
        public bool HasPropertyBlock() => PropertyBlock;
        public readonly List<Material> Shared = new();
        public void GetSharedMaterials(List<Material> result) { result.Clear(); result.AddRange(Shared); }
    }
    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { public Mesh? sharedMesh; }
    public class Mesh : Object { public int subMeshCount = 1; }
    public class MeshFilter : Component { public Mesh? sharedMesh; }
    public class Material : Object { public Shader? shader = new(); }
    public class Shader : Object { }
    public struct LOD { public Renderer[] renderers; }
    public class LODGroup : Behaviour { public LOD[] Levels=Array.Empty<LOD>(); public LOD[] GetLODs() => Levels; }
    public class Rigidbody : Component { public Vector3 velocity; }
    public class Animator : Behaviour { }
    public enum LightType { Directional, Point }
    public class Light : Behaviour { public float range=10,intensity=1; public int cullingMask=-1; public string shadows="Soft"; public LightType type; }
    public class Camera : Behaviour { public static Camera? main; public Matrix4x4 worldToCameraMatrix=Matrix4x4.Identity,projectionMatrix=Matrix4x4.Identity; }
    public static class RenderSettings { public static string ambientMode="Skybox"; public static float ambientIntensity=1; public static bool fog=false; }
    public static class Time { public static int frameCount=17; }
}
namespace UnityEngine.AI { public class NavMeshAgent : Behaviour { public Vector3 velocity; } }
namespace UnityEngine.Rendering
{
    public class Volume : Behaviour
    {
        public float weight=1,blendDistance=0,priority=0;
        public bool isGlobal { get; set; } = true;
        public object profile => throw new Exception("profile instantiation forbidden");
    }
}
namespace UnityEngine.SceneManagement
{
    public class Scene
    {
        public string name="fixture"; public bool isLoaded=true; public List<GameObject> Roots=new();
        public int rootCount => Roots.Count;
        public void GetRootGameObjects(List<GameObject> output) { output.Clear(); output.AddRange(Roots); }
    }
    public static class SceneManager
    {
        public static List<Scene> Scenes = new(); public static int sceneCount => Scenes.Count;
        public static Scene GetSceneAt(int i) => Scenes[i];
    }
}
public static class InstanceBehavior<T> where T : new()
{
    public static T Value = new(); public static int Reads;
    public static T Instance { get { Reads++; return Value; } }
}
public class Building { public string Address="30 3rd Street",BuildingSize="small",BuildingType="ba:buildingtype_retail"; public int BuildingVersion=1; }
public class Registration { public bool HasEstablishedBusiness=true; public string businessTypeName="coffee"; }
public class BuildingEntranceDoor { public Transform doorTransform=null!; public int doorId; }
public class EntityController : Behaviour { public Renderer[] BaseRenderers=Array.Empty<Renderer>(); public Renderer[] Renderers => BaseRenderers; }
public class ViewBlockingEntity : EntityController { public List<Renderer> renderersToFade=new(),renderersToHide=new(); }
public class CityBuildingController : ViewBlockingEntity { public MeshRenderer? lodVersion; public Building building=new(); public BuildingEntranceDoor[] entranceDoors=Array.Empty<BuildingEntranceDoor>(); }
public class ExitZone : Behaviour { public bool isElevator,isDriveInBay; public ExitZoneDespawner despawner=null!; }
public class ExitZoneDespawner : Behaviour { public int exitToZoneId; public bool isCasinoExit,isParkingExit,hasSpecialCondition; }
public class BuildingManager
{
    public static bool IsInsideBuilding=true;
    public bool enteringBuilding=false,exitingBuilding=false;
    public Building building=new(); public Registration buildingRegistration=new();
    public Transform currentBuildingVersion=null!;
    public CityBuildingController cityBuildingController=null!;
    public object? multipleHeightsBuildingController=null;
    public List<ExitZone> exitZones=new();
}
public static class SaveGameManager { public static object? Current=new(); }
public class ThirdPersonCharacter : Behaviour { public UnityEngine.AI.NavMeshAgent navmeshAgent=null!; }
namespace GleyTrafficSystem { public class VehicleComponent : Behaviour { public Rigidbody rb=null!; } }
namespace BigAmbitions.InteriorDesigner.InteriorElements
{
    public class Element : Component { public bool IsFloor=true; }
    public static class InteriorElementsHelper { public static Dictionary<int,Element> InteriorElementsCache=new(); }
}
namespace PersistentCity.Runtime
{
    internal static class NativeInteriorCityView { public static bool Configured=false; }
    internal static class NativeInteriorGuard { public static InteriorGuardVerdict Verdict=new(false,"fixture"); }
    internal static class GamePlaces { public static bool IsRetailShopActivity(string? type) => type == "coffee"; }
    public sealed partial class PopulationRuntime
    {
        private bool failed=false;
        private object? simulation=new();
        private object? saveIdentity=SaveGameManager.Current;
        private PhysicalPopulation? physical;
        internal void SetBodies(PhysicalPopulation bodies) => physical=bodies;
        internal void Stale() => saveIdentity=new();
    }
    internal sealed partial class PhysicalPopulation
    {
        private sealed class Person { public ThirdPersonCharacter Character=null!; }
        private sealed class Car { public GleyTrafficSystem.VehicleComponent Vehicle=null!; }
        private sealed class Binding { public CitizenState Citizen=new(); public Person? Person; public Car? Car; }
        private readonly Dictionary<string,Binding> bindings=new();
        internal void Add(string id, ThirdPersonCharacter? p=null,GleyTrafficSystem.VehicleComponent? c=null)
        { bindings.Add(id,new Binding { Citizen=new CitizenState { Identity=new CitizenIdentity { Id=id } }, Person=p==null?null:new Person { Character=p },Car=c==null?null:new Car { Vehicle=c } }); }
    }
}
