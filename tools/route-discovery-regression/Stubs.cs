using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    [Flags] public enum HideFlags { None=0, HideInHierarchy=1, DontSaveInEditor=4, DontSaveInBuild=16, DontUnloadUnusedAsset=32, DontSave=52, HideAndDontSave=61 }
    public class Object
    {
        static int next;
        public int Id=++next;
        public bool Destroyed;
        public HideFlags hideFlags;
        public int GetInstanceID()=>Id;
        public static bool operator ==(Object? a,Object? b)=>ReferenceEquals(a,b)||((ReferenceEquals(a,null)||a.Destroyed)&&(ReferenceEquals(b,null)||b.Destroyed));
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? other)=>ReferenceEquals(this,other);
        public override int GetHashCode()=>Id;
        public static void Destroy(Object value)
        {
            value.Destroyed=true;if(value is GameObject go)
            {
                if(go.transform.Parent!=null)go.transform.Parent.Children.Remove(go.transform);
                var stack=new Stack<GameObject>();stack.Push(go);
                while(stack.Count>0){var current=stack.Pop();current.Destroyed=true;current.transform.Destroyed=true;foreach(var c in current.Components)c.Destroyed=true;current.scene.Data?.Roots.Remove(current);foreach(var child in current.transform.Children)stack.Push(child.gameObject);}
            }
        }
        public static void DontDestroyOnLoad(GameObject go)=>go.Move(SceneManagement.SceneManager.Persistent);
    }
    public class Component:Object { public GameObject gameObject=null!; public Transform transform=>gameObject.transform; }
    public class MonoBehaviour:Component {}
    public class Transform:Component
    {
        public static readonly HashSet<Transform> DirectQueries=new HashSet<Transform>();
        public static readonly HashSet<Transform> SlowDirect=new HashSet<Transform>();
        public static readonly HashSet<Transform> Slow=new HashSet<Transform>();
        public static readonly HashSet<Transform> RecursiveQueries=new HashSet<Transform>();
        public readonly List<Transform> Children=new List<Transform>();
        public Transform? Parent;
        public int childCount=>Children.Count;
        public Transform GetChild(int i)=>Children[i];
        public void GetComponents<T>(List<T> values) where T:Component { GameObject.ComponentReads++;DirectQueries.Add(this);if(SlowDirect.Contains(this))System.Threading.Thread.Sleep(2); values.Clear();foreach(var c in gameObject.Components) if(c is T t&&t!=null)values.Add(t); }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> values) where T:Component
        {
            GameObject.ComponentReads++;RecursiveQueries.Add(this);if(Slow.Contains(this))System.Threading.Thread.Sleep(3);values.Clear();var stack=new Stack<Transform>();stack.Push(this);
            while(stack.Count>0){var node=stack.Pop();if(node==null)continue;foreach(var c in node.gameObject.Components)if(c is T t&&t!=null)values.Add(t);foreach(var child in node.Children)stack.Push(child);}
        }
        public void SetParent(Transform? parent)
        {
            if(Parent!=null)Parent.Children.Remove(this);else gameObject.scene.Data?.Roots.Remove(gameObject);
            Parent=parent;
            if(parent==null)gameObject.scene.Data?.Roots.Add(gameObject);else {parent.Children.Add(this);gameObject.MoveHierarchy(parent.gameObject.scene);}
        }
    }
    public class GameObject:Object
    {
        public static int ComponentReads;
        public readonly List<Component> Components=new List<Component>();
        public readonly Transform transform;
        public SceneManagement.Scene scene;
        public bool activeSelf=true;
        public GameObject(string name="") {transform=new Transform{gameObject=this};Move(SceneManagement.SceneManager.Current);}
        public T Add<T>() where T:Component,new() {var c=new T{gameObject=this};Components.Add(c);return c;}
        public void Move(SceneManagement.Scene target) {scene.Data?.Roots.Remove(this);MoveHierarchy(target);target.Data?.Roots.Add(this);}
        public void MoveHierarchy(SceneManagement.Scene target) {scene=target;foreach(var child in transform.Children)child.gameObject.MoveHierarchy(target);}
    }
    public static class Time {public static float unscaledTime;}
}
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public sealed class SceneData {public bool Loaded=true; public readonly List<UnityEngine.GameObject> Roots=new List<UnityEngine.GameObject>();}
    public struct Scene
    {
        public SceneData? Data;
        public bool IsValid()=>Data!=null;
        public bool isLoaded=>Data?.Loaded??false;
        public int rootCount=>Data?.Roots.Count??0;
        public void GetRootGameObjects(List<UnityEngine.GameObject> roots) { SceneManager.RootReadCount++; roots.Clear(); if(Data!=null)roots.AddRange(Data.Roots); SceneManager.RootsRead?.Invoke(this); }
    }
    public static class SceneManager
    {
        public static Action<Scene>? RootsRead;
        public static int RootReadCount;
        public static event Action<Scene,LoadSceneMode>? sceneLoaded;
        public static event Action<Scene>? sceneUnloaded;
        public static void Unload(Scene scene){scene.Data!.Loaded=false;sceneUnloaded?.Invoke(scene);}
        public static void Reload(Scene scene){scene.Data!.Loaded=true;sceneLoaded?.Invoke(scene,LoadSceneMode.Additive);}
        public static readonly List<Scene> Scenes=new List<Scene>();
        public static Scene Persistent=new Scene{Data=new SceneData()};
        public static Scene Current=>Scenes[0];
        public static int sceneCount=>Scenes.Count;
        public static Scene GetSceneAt(int i)=>Scenes[i];
        public static Scene New() {var s=new Scene{Data=new SceneData()};Scenes.Add(s);sceneLoaded?.Invoke(s,LoadSceneMode.Additive);return s;}
        public static void Reset(){RootsRead=null;Scenes.Clear();New();Persistent=new Scene{Data=new SceneData()};UnityEngine.Time.unscaledTime=0;}
    }
}
public class FenceDoor:UnityEngine.MonoBehaviour {}
public class HamptonsPrivateFenceDoor:FenceDoor {}
public class SubwayStation:UnityEngine.MonoBehaviour {}
namespace PersistentCity.Runtime
{
    internal interface IRouteObjectSource {bool Ready{get;} int Revision{get;} FenceDoor[] Doors{get;} SubwayStation[] Stations{get;} void RequestSweep(); }
}
