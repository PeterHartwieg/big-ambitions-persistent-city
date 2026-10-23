using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GleyTrafficSystem;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Object=UnityEngine.Object;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object? a,Object? b) => (ReferenceEquals(a,null)||a.Destroyed) ? ReferenceEquals(b,null)||b.Destroyed : ReferenceEquals(a,b);
        public static bool operator !=(Object? a,Object? b) => !(a==b);
        public override bool Equals(object? obj)=>ReferenceEquals(this,obj);
        public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class GameObject:Object
    {
        public bool activeInHierarchy=true;
        public Transform transform;
        public List<MonoBehaviour?> Scripts=new List<MonoBehaviour?>();
        public List<GameObject> Children=new List<GameObject>();
        public GameObject(){transform=new Transform(this);}
    }
    public class Transform:Object
    {
        public GameObject gameObject;public Transform? parent;public Position position;
        public Transform(GameObject go){gameObject=go;}
        public bool IsChildOf(Transform t){for(Transform? p=this;p!=null;p=p.parent)if(ReferenceEquals(p,t))return true;return false;}
    }
    public class Rigidbody:Object { }
    public class Component:Object
    {
        public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;
        public static Action<List<MonoBehaviour>>? FillHook;
        public static readonly List<string> Queries=new List<string>();
        public T? GetComponentInChildren<T>(bool inactive) where T:MonoBehaviour
        { Queries.Add(typeof(T).Name+":"+inactive);return All(gameObject,inactive).OfType<T>().FirstOrDefault(x=>x!=null); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T:MonoBehaviour
        { return All(gameObject,inactive).Where(x=>x==null||x is T).Cast<T>().ToArray(); }
        public void GetComponentsInChildren(bool inactive,List<MonoBehaviour> values)
        { Queries.Add("Scripts:"+inactive);values.Clear();foreach(var x in All(gameObject,inactive))values.Add(x!);FillHook?.Invoke(values); }
        static IEnumerable<MonoBehaviour?> All(GameObject go,bool inactive)
        { if(!inactive&&!go.activeInHierarchy)yield break;foreach(var s in go.Scripts)yield return s;foreach(var c in go.Children)foreach(var s in All(c,inactive))yield return s; }
    }
    public class MonoBehaviour:Component { }
}
namespace UI { public class TaxiController:MonoBehaviour { } public class PrivateDriverVehicle:MonoBehaviour { } }
namespace Helpers { public class VehicleController:MonoBehaviour { } }
namespace GleyTrafficSystem
{
    public enum VehicleTypes { Car,Truck }
    public class VehicleComponent:MonoBehaviour
    {
        public GameObject prefab=new GameObject();public Rigidbody rb=new Rigidbody();public int Index;public VehicleTypes vehicleType;
        public List<int>? presetPath;public Transform Front;
        public VehicleComponent(){Front=new Transform(new GameObject()){parent=transform};gameObject.Scripts.Add(this);}
        public int GetIndex()=>Index;public Transform GetFrontAxle()=>Front;
    }
}
namespace PersistentCity.Runtime
{
    partial class PhysicalVehicles { internal static Position Point(Position p)=>p; }
    partial class PhysicalVehicleBody
    {
        public bool Released;public VehicleComponent Vehicle;public int NativeIndex;public GameObject NativePrefab;
        public Rigidbody NativeRigidbody;public Transform FrontMarker;public List<int> Path;public Position LastTargetOrigin=new Position(4,0,9);
        public PhysicalVehicleBody(VehicleComponent v){Vehicle=v;NativeIndex=v.Index;NativePrefab=v.prefab;NativeRigidbody=v.rb;FrontMarker=v.Front;Path=new List<int>{1};v.presetPath=Path;}
    }
}
public class NativeScript:MonoBehaviour { }
public class TaxiSubclass:UI.TaxiController { }
public class DriverSubclass:UI.PrivateDriverVehicle { }
public class ControllerSubclass:Helpers.VehicleController { }
static class Program
{
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    static List<MonoBehaviour>? Available=>(List<MonoBehaviour>?)typeof(VehicleScriptOwnership).GetField("available",Flags)!.GetValue(null);
    static int TypeCount=>((Dictionary<Type,bool>?)typeof(VehicleScriptOwnership).GetField("nativeTypes",Flags)!.GetValue(null))?.Count??0;
    static void ClearCheck()=>Check(Available==null||Available.Count==0,"no retained component references");
    static MonoBehaviour Script(string assembly)
    {
        var a=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assembly),AssemblyBuilderAccess.Run);
        var t=a.DefineDynamicModule("Fixture").DefineType("ForeignOrNative",TypeAttributes.Public,typeof(MonoBehaviour));
        return (MonoBehaviour)Activator.CreateInstance(t.CreateType()!)!;
    }
    static bool Old(VehicleComponent v)=>v!=null&&v.prefab!=null&&v.GetComponentInChildren<UI.TaxiController>(true)==null
        &&v.GetComponentInChildren<UI.PrivateDriverVehicle>(true)==null&&v.GetComponentInChildren<Helpers.VehicleController>(true)==null
        &&!v.GetComponentsInChildren<MonoBehaviour>(true).Any(s=>s!=null&&!s.GetType().Assembly.GetName().Name!.StartsWith("BigAmbitions",StringComparison.Ordinal)
            &&s.GetType().Assembly.GetName().Name!="ExternalPlugins"&&!s.GetType().Assembly.GetName().Name!.StartsWith("Unity",StringComparison.Ordinal));
    static void Same(VehicleComponent v,bool expected,string why){Check(Old(v)==expected,"oracle: "+why);Check(PhysicalVehicles.RandomAmbient(v)==expected,why);ClearCheck();}
    static void Main()
    {
        var v=new VehicleComponent();var body=new PhysicalVehicleBody(v);Same(v,true,"ordinary");
        v.vehicleType=VehicleTypes.Truck;Same(v,true,"ambient truck");Check(!PhysicalVehicles.Ordinary(v),"truck not citizen car");v.vehicleType=VehicleTypes.Car;
        Check(PhysicalVehicles.Ordinary(v),"ordinary car eligible");
        var child=new GameObject{activeInHierarchy=false};v.gameObject.Children.Add(child);child.transform.parent=v.transform;
        var foreign=Script("Foreign.Mod");child.Scripts.Add(foreign);Same(v,false,"inactive foreign child after successful warm check");Check(!body.Valid,"live lease revoked");
        foreign.Destroyed=true;Same(v,true,"destroyed foreign ignored");foreign.Destroyed=false;Same(v,false,"foreign immediately present again");
        child.Scripts.Remove(foreign);Same(v,true,"foreign removed same frame");child.Scripts.Add(null);Same(v,true,"missing script ignored");
        foreach(var s in new MonoBehaviour[]{new UI.TaxiController(),new UI.PrivateDriverVehicle(),new Helpers.VehicleController(),new TaxiSubclass(),new DriverSubclass(),new ControllerSubclass()})
        {
            child.Scripts.Add(s);Same(v,false,"inactive service or subclass");child.Scripts.Remove(s);Same(v,true,"service removed");
            v.gameObject.Scripts.Add(s);Same(v,false,"service on root");v.gameObject.Scripts.Remove(s);
        }
        foreach(var p in new[]{("BigAmbitions",true),("BigAmbitionsExtra",true),("bigambitions",false),("Unity",true),("UnityExtra",true),("unity",false),("ExternalPlugins",true),("ExternalPlugins.More",false),("externalplugins",false),("Other",false)})
        { var s=Script(p.Item1);child.Scripts.Add(s);Same(v,p.Item2,"exact existing assembly policy "+p.Item1);child.Scripts.Remove(s); }
        int count=TypeCount;for(int i=0;i<20;i++)Same(v,true,"reuse known metadata");Check(TypeCount==count,"cache does not grow with calls");
        var other=new VehicleComponent();other.gameObject.Children.Add(child);v.gameObject.Children.Remove(child);child.Scripts.Add(foreign);
        Same(v,true,"reparent removed foreign from old car");Same(other,false,"reparent detected at new car");
        Component.FillHook=list=>{Component.FillHook=null;Check(!PhysicalVehicles.RandomAmbient(other),"nested foreign");};
        Same(v,true,"outer ordinary survives nested rejected query");
        Component.FillHook=list=>{Component.FillHook=null;Check(PhysicalVehicles.RandomAmbient(v),"nested ordinary");};
        Same(other,false,"outer foreign survives nested accepted query");
        List<MonoBehaviour>? captured=null;Component.FillHook=list=>{captured=list;throw new InvalidOperationException("partial native fill");};
        bool threw=false;try{PhysicalVehicles.RandomAmbient(v);}catch(InvalidOperationException){threw=true;}finally{Component.FillHook=null;}
        Check(threw,"native exception propagated");Check(captured!=null&&captured.Count==0,"partial fill references cleared on throw");Same(v,true,"next query after failure");
        Component.Queries.Clear();PhysicalVehicles.RandomAmbient(v);
        Check(string.Join(",",Component.Queries)=="TaxiController:True,PrivateDriverVehicle:True,VehicleController:True,Scripts:True","service query order and includeInactive preserved");
        v.gameObject.Scripts.Add(new UI.TaxiController());Component.Queries.Clear();Check(!PhysicalVehicles.RandomAmbient(v),"taxi early rejection");Check(Component.Queries.Count==1,"taxi keeps first-query short circuit");v.gameObject.Scripts.RemoveAt(v.gameObject.Scripts.Count-1);
        var large=new VehicleComponent();for(int i=0;i<2100;i++)large.gameObject.Scripts.Add(new NativeScript());Same(large,true,"oversized live inventory allowed");Check(Available==null,"oversized buffer not retained");Same(v,true,"small buffer recovered after oversized query");
        void Loss(Action change,Action restore,string reason){change();Check(body.OwnershipLossReason==reason,reason);Check(!body.Valid,"invalid body "+reason);restore();Check(body.Valid,"restored "+reason);}
        Loss(()=>body.Released=true,()=>body.Released=false,"released");
        Loss(()=>v.gameObject.activeInHierarchy=false,()=>v.gameObject.activeInHierarchy=true,"native-body-inactive");
        Loss(()=>v.Index++,()=>v.Index--,"native-slot-or-prefab-changed");
        var prefab=v.prefab;Loss(()=>v.prefab=new GameObject(),()=>v.prefab=prefab,"native-slot-or-prefab-changed");
        var rb=v.rb;Loss(()=>v.rb=new Rigidbody(),()=>v.rb=rb,"native-rigidbody-changed");
        var front=v.Front;Loss(()=>v.Front=new Transform(new GameObject()),()=>v.Front=front,"native-front-marker-changed");
        Loss(()=>front.parent=null,()=>front.parent=v.transform,"native-front-marker-changed");
        Loss(()=>front.position=new Position(double.NaN,0,0),()=>front.position=default,"native-front-marker-invalid");
        Loss(()=>v.gameObject.Scripts.Add(foreign),()=>v.gameObject.Scripts.Remove(foreign),"native-service-or-foreign-component");
        Loss(()=>v.presetPath=new List<int>{1},()=>v.presetPath=body.Path,"native-path-replaced");
        body.Released=true;var replacement=new PhysicalVehicleBody(v);Check(replacement.Valid&&!body.Valid,"pool reuse never revives released lease");body.Released=false;
        v.Destroyed=true;Same(v,false,"destroyed vehicle");v.Destroyed=false;v.prefab.Destroyed=true;Same(v,false,"destroyed prefab");v.prefab.Destroyed=false;
        Console.WriteLine("PASS "+checks+" actual Runtime vehicle ownership assertions");
    }
}
