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
    public static class Time { public static int frameCount=1; }
}
namespace UI { public class TaxiController:MonoBehaviour { } public class PrivateDriverVehicle:MonoBehaviour { } }
namespace Helpers { public class VehicleController:MonoBehaviour { } }
namespace GleyTrafficSystem
{
    public enum VehicleTypes { Car,Truck }
    public enum SpecialDriveActionTypes { StopNow }
    public class Waypoint { }
    // Native mutation doubles. Callback models a synchronous native/mod callback inside the call.
    public class TrafficManager
    {
        public static readonly TrafficManager Instance=new TrafficManager();
        public static bool IsInitialized=true;
        public static Action? Callback;public static VehicleComponent? Loaded;public static readonly List<string> Calls=new List<string>();
        public VehicleComponent? LoadVehicle(GameObject prefab,Waypoint spawn,bool clear){Calls.Add("Load");Callback?.Invoke();return Loaded;}
        public void SetVehicleTargetWaypoint(VehicleComponent vehicle,Waypoint target){Calls.Add("Target");Callback?.Invoke();}
        public void SetVehicleAction(VehicleComponent vehicle,SpecialDriveActionTypes action,bool add){Calls.Add("Stop");Callback?.Invoke();}
    }
    public static class Manager
    {
        public static List<VehicleComponent> Pool=new List<VehicleComponent>();
        public static List<VehicleComponent> GetVehicleList()=>Pool;
        public static void RemoveVehicle(int index){TrafficManager.Calls.Add("Remove");TrafficManager.Callback?.Invoke();}
    }
    public class DrivingAI { public void NewDriveActionArrived(int index,SpecialDriveActionTypes action,bool add){TrafficManager.Calls.Add("Expire");TrafficManager.Callback?.Invoke();} }
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
    partial class PhysicalVehicles { internal static Position Point(Position p)=>p; public bool Active=true; internal int taxiTarget; }
    static class PhysicalTrafficHooks { internal static readonly MethodInfo? RemoveAction=typeof(DrivingAI).GetMethod(nameof(DrivingAI.NewDriveActionArrived)); }
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
    // The oracle's own searches are dropped from the query log, which counts production searches only.
    static bool Old(VehicleComponent v){int logged=Component.Queries.Count;try{return OldPredicate(v);}finally{Component.Queries.RemoveRange(logged,Component.Queries.Count-logged);}}
    static bool OldPredicate(VehicleComponent v)=>v!=null&&v.prefab!=null&&v.GetComponentInChildren<UI.TaxiController>(true)==null
        &&v.GetComponentInChildren<UI.PrivateDriverVehicle>(true)==null&&v.GetComponentInChildren<Helpers.VehicleController>(true)==null
        &&!v.GetComponentsInChildren<MonoBehaviour>(true).Any(s=>s!=null&&!s.GetType().Assembly.GetName().Name!.StartsWith("BigAmbitions",StringComparison.Ordinal)
            &&s.GetType().Assembly.GetName().Name!="ExternalPlugins"&&!s.GetType().Assembly.GetName().Name!.StartsWith("Unity",StringComparison.Ordinal));
    static void Same(VehicleComponent v,bool expected,string why){Check(Old(v)==expected,"oracle: "+why);Check(PhysicalVehicles.RandomAmbient(v)==expected,why);ClearCheck();}
    // The unchanged legacy body predicate, evaluated live with the legacy component oracle.
    static string OldLoss(PhysicalVehicleBody b)
    {
        var v=b.Vehicle;
        if(b.Released)return "released";
        if(v==null||!v.gameObject.activeInHierarchy||v.rb==null)return "native-body-inactive";
        if(v.GetIndex()!=b.NativeIndex||!ReferenceEquals(v.prefab,b.NativePrefab))return "native-slot-or-prefab-changed";
        if(!ReferenceEquals(v.rb,b.NativeRigidbody))return "native-rigidbody-changed";
        if(b.FrontMarker==null||!b.FrontMarker.IsChildOf(v.transform)||!ReferenceEquals(v.GetFrontAxle(),b.FrontMarker))return "native-front-marker-changed";
        if(!b.FrontMarker.position.IsFinite)return "native-front-marker-invalid";
        if(!Old(v))return "native-service-or-foreign-component";
        if(!ReferenceEquals(v.presetPath,b.Path))return "native-path-replaced";
        return "";
    }
    static void Next()=>PhysicalVehicleBody.AdvanceOwnershipEpoch();
    static int ComponentQueries=>Component.Queries.Count;
    static void Mutate(string name,params object?[] arguments)
    {
        var helper=typeof(PhysicalVehicles).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)??throw new Exception("missing wrapper "+name);
        helper.Invoke(null,arguments);
    }
    static void Main()
    {
        var v=new VehicleComponent();var body=new PhysicalVehicleBody(v);Same(v,true,"ordinary");
        v.vehicleType=VehicleTypes.Truck;Same(v,true,"ambient truck");Check(!PhysicalVehicles.Ordinary(v),"truck not citizen car");v.vehicleType=VehicleTypes.Car;
        Check(PhysicalVehicles.Ordinary(v),"ordinary car eligible");
        var child=new GameObject{activeInHierarchy=false};v.gameObject.Children.Add(child);child.transform.parent=v.transform;
        Check(body.Valid,"warm lease verdict for this epoch");
        var foreign=Script("Foreign.Mod");child.Scripts.Add(foreign);Same(v,false,"inactive foreign child after successful warm check");
        Check(body.Valid&&OldLoss(body)=="native-service-or-foreign-component","bounded relaxation: verdict reused until the epoch ends");
        Next();Check(!body.Valid&&body.OwnershipLossReason==OldLoss(body),"live lease revoked at the next epoch");
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
        Next();Check(body.Valid&&OldLoss(body)=="","ordinary lease before reference/state checks");
        // Reference, state and geometry losses are live inside one epoch and need no component search.
        void Loss(Action change,Action restore,string reason)
        {
            int queries=ComponentQueries;change();Check(body.OwnershipLossReason==reason&&OldLoss(body)==reason,reason);Check(!body.Valid,"invalid body "+reason);
            restore();Check(body.Valid&&OldLoss(body)=="","restored "+reason);Check(ComponentQueries==queries,"live check without component search "+reason);
        }
        Loss(()=>body.Released=true,()=>body.Released=false,"released");
        Loss(()=>v.gameObject.activeInHierarchy=false,()=>v.gameObject.activeInHierarchy=true,"native-body-inactive");
        Loss(()=>v.Index++,()=>v.Index--,"native-slot-or-prefab-changed");
        var prefab=v.prefab;Loss(()=>v.prefab=new GameObject(),()=>v.prefab=prefab,"native-slot-or-prefab-changed");
        var rb=v.rb;Loss(()=>v.rb=new Rigidbody(),()=>v.rb=rb,"native-rigidbody-changed");
        var front=v.Front;Loss(()=>v.Front=new Transform(new GameObject()),()=>v.Front=front,"native-front-marker-changed");
        Loss(()=>front.parent=null,()=>front.parent=v.transform,"native-front-marker-changed");
        Loss(()=>front.position=new Position(double.NaN,0,0),()=>front.position=default,"native-front-marker-invalid");
        v.gameObject.Scripts.Add(foreign);Check(body.Valid,"foreign component on root unseen within the epoch");
        Next();Check(body.OwnershipLossReason=="native-service-or-foreign-component"&&OldLoss(body)==body.OwnershipLossReason,"foreign component on root seen at the next epoch");
        v.gameObject.Scripts.Remove(foreign);Check(!body.Valid,"a lost verdict also holds until the epoch ends");Next();Check(body.Valid&&OldLoss(body)=="","removal seen at the next epoch");
        Loss(()=>v.presetPath=new List<int>{1},()=>v.presetPath=body.Path,"native-path-replaced");
        body.Released=true;var replacement=new PhysicalVehicleBody(v);Check(replacement.Valid&&!body.Valid,"pool reuse never revives released lease");body.Released=false;
        v.Destroyed=true;Same(v,false,"destroyed vehicle");v.Destroyed=false;v.prefab.Destroyed=true;Same(v,false,"destroyed prefab");v.prefab.Destroyed=false;
        // Verdict reuse within one epoch: one component search, then none until a boundary.
        v.presetPath=body.Path; // The pool-reuse fixture above handed the slot's path to its replacement.
        Next();Component.Queries.Clear();Check(body.Valid,"fresh verdict");Check(ComponentQueries==4,"one component search per epoch");
        for(int i=0;i<50;i++){Check(body.Valid&&body.OwnershipLossReason==""&&body.Position.Equals(body.FrontMarker.position),"memo hit "+i);}
        Check(ComponentQueries==4,"verdict reused by Valid, OwnershipLossReason and Position within the epoch");
        // Boundaries: an Update, a fixed step or a wrapper all advance the epoch; a new frame also expires the memo.
        Next();Check(body.Valid&&ComponentQueries==8,"epoch advance forces a fresh search");
        Time.frameCount++;Check(body.Valid&&ComponentQueries==12,"a new frame forces a fresh search without an epoch advance");
        Check(body.Valid&&ComponentQueries==12,"reused again within the new frame");
        var otherBody=new PhysicalVehicleBody(other);Check(!otherBody.Valid&&ComponentQueries>12,"memo is per body");
        int beforeOther=ComponentQueries;Check(body.Valid&&ComponentQueries==beforeOther,"another body's search leaves this memo intact");
        // A search that itself advances the epoch stamps the older epoch, so the next call searches again.
        Next();Component.FillHook=_=>{Component.FillHook=null;Next();};int stale=ComponentQueries;Check(body.Valid,"verdict during nested advance");
        Check(body.Valid&&ComponentQueries==stale+8,"stale stamp after a nested advance");
        // A failed search stores nothing.
        Next();Component.FillHook=_=>{Component.FillHook=null;throw new InvalidOperationException("native query failed");};
        threw=false;try{_=body.Valid;}catch(InvalidOperationException){threw=true;}Check(threw,"search failure propagates");
        int failed=ComponentQueries;Check(body.Valid&&ComponentQueries==failed+4,"no verdict stored from a failed search");
        // Each native mutation wrapper advances the epoch, so a claim made inside the native call is seen at once.
        var waypoint=new Waypoint();var ai=new DrivingAI();
        foreach(var (name,arguments,call) in new (string,object?[],string)[]{("LoadNative",new object?[]{v.prefab,waypoint},"Load"),("TargetNative",new object?[]{v,waypoint},"Target"),
            ("StopNative",new object?[]{v},"Stop"),("RemoveNative",new object?[]{v.Index},"Remove"),("ExpireNativeStop",new object?[]{ai,v.Index},"Expire")})
        {
            Next();Check(body.Valid,"owned before "+name);
            long epoch=PhysicalVehicleBody.OwnershipEpoch;TrafficManager.Calls.Clear();
            TrafficManager.Callback=()=>v.gameObject.Scripts.Add(foreign);Mutate(name,arguments);TrafficManager.Callback=null;
            Check(TrafficManager.Calls.SequenceEqual(new[]{call}),"wrapper calls native "+call+" once");
            Check(PhysicalVehicleBody.OwnershipEpoch==epoch+1,name+" advances the epoch");
            Check(body.OwnershipLossReason=="native-service-or-foreign-component"&&OldLoss(body)==body.OwnershipLossReason,"claim inside "+name+" seen immediately after it");
            v.gameObject.Scripts.Remove(foreign);
            Next();Check(body.Valid,"owned again after "+name);epoch=PhysicalVehicleBody.OwnershipEpoch;
            TrafficManager.Callback=()=>{v.gameObject.Scripts.Add(foreign);throw new InvalidOperationException("native callback failed");};
            threw=false;try{Mutate(name,arguments);}catch(TargetInvocationException error) when (error.InnerException is InvalidOperationException || error.InnerException?.InnerException is InvalidOperationException){threw=true;}
            TrafficManager.Callback=null;Check(threw,name+" propagates a native failure");
            Check(PhysicalVehicleBody.OwnershipEpoch==epoch+1&&!body.Valid,name+" advances the epoch when native throws");
            v.gameObject.Scripts.Remove(foreign);Next();Check(body.Valid,"restored after failing "+name);
        }
        Check(Available==null||Available.Count==0,"no retained component references after epoch checks");
        // Taxi classification and the active-taxi count are memoized per frame for native AddVehicle draws.
        static bool OldTaxi(VehicleComponent? x)=>x!=null&&x.GetComponentInChildren<UI.TaxiController>(true)!=null;
        bool OldNeed(PhysicalVehicles pv){int logged=Component.Queries.Count;try{return pv.Active&&TrafficManager.IsInitialized&&Manager.Pool.Count(x=>OldTaxi(x)&&x!.gameObject.activeInHierarchy)<pv.taxiTarget;}finally{Component.Queries.RemoveRange(logged,Component.Queries.Count-logged);}}
        VehicleComponent TaxiCar(bool live){var x=new VehicleComponent();x.gameObject.Scripts.Add(new UI.TaxiController());x.gameObject.activeInHierarchy=live;return x;}
        var liveTaxi=TaxiCar(true);var idleTaxi=TaxiCar(false);var plain=new VehicleComponent();var subclassTaxi=TaxiCar(false);subclassTaxi.gameObject.Scripts[1]=new TaxiSubclass();
        Manager.Pool=new List<VehicleComponent>{liveTaxi,idleTaxi,plain,null!,subclassTaxi};
        var adapter=new PhysicalVehicles{taxiTarget=2};Time.frameCount+=10;Component.Queries.Clear();
        Check(adapter.TaxiNeedsReplenishment&&OldNeed(adapter),"one active taxi below a target of two");
        int classified=ComponentQueries;Check(classified==4,"one taxi query per non-null pool vehicle");
        Check(adapter.TaxiNeedsReplenishment&&ComponentQueries==classified,"active-taxi count reused within the frame");
        Check(adapter.IsTaxiThisFrame(idleTaxi)&&adapter.IsTaxiThisFrame(subclassTaxi)&&!adapter.IsTaxiThisFrame(plain)&&!adapter.IsTaxiThisFrame(null!)&&ComponentQueries==classified,"classification reused within the frame");
        idleTaxi.gameObject.activeInHierarchy=true;
        Check(adapter.TaxiNeedsReplenishment&&!OldNeed(adapter),"bounded relaxation: a taxi spawned this frame is counted from the next frame");
        Time.frameCount++;Check(!adapter.TaxiNeedsReplenishment&&!OldNeed(adapter)&&ComponentQueries==classified+4,"next frame reclassifies and recounts");
        liveTaxi.gameObject.Scripts.RemoveAt(1);Check(adapter.IsTaxiThisFrame(liveTaxi),"classification fixed for the rest of the frame");
        Time.frameCount++;Check(!adapter.IsTaxiThisFrame(liveTaxi)&&adapter.TaxiNeedsReplenishment==OldNeed(adapter)&&adapter.TaxiNeedsReplenishment,"next frame sees the removed taxi controller");
        adapter.Active=false;Check(!adapter.TaxiNeedsReplenishment,"inactive adapter never asks for a taxi");adapter.Active=true;
        TrafficManager.IsInitialized=false;Check(!adapter.TaxiNeedsReplenishment,"uninitialized traffic never asks for a taxi");TrafficManager.IsInitialized=true;
        Console.WriteLine("PASS "+checks+" actual Runtime vehicle ownership assertions");
    }
}
