// rig_test diagnostics only; native actions and passive runtime observations.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PersistentCity.Runtime;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;
using HarmonyLib;
namespace PerformanceReview {
public partial class Guard {
 const BindingFlags BF=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static object Read(object o,string n){if(o==null)return null;var t=o as Type??o.GetType();return t.GetField(n,BF)?.GetValue(o is Type?null:o)??t.GetProperty(n,BF)?.GetValue(o is Type?null:o);}
 static Type PC(string n)=>typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime."+n);
 static object Invoke(object o,string n,params object[] a){var t=o as Type??o.GetType();return t.GetMethods(BF).Single(m=>m.Name==n&&m.GetParameters().Length==a.Length).Invoke(o is Type?null:o,a);}
 static object Vec(Vector3 v)=>new{x=v.x,y=v.y,z=v.z};
 object Body(object b,string id){var person=Read(b,"Person");var car=Read(b,"Car");object position=Read(person??car,"Position");var component=Read(person??car,car==null?"Character":"Vehicle") as Component;return new{id,kind=car==null?"pedestrian":"car",position,body=Read(person??car,"Generation"),nativeId=component==null?0:component.GetInstanceID(),valid=Read(person??car,"Valid"),carId=Read(car,"Id"),nativeIndex=Read(car,"NativeIndex"),progress=Read(Read(b,"Lease"),"Progress")};}
 object[] Bodies(object p){var d=Read(p,"bindings") as IDictionary;if(d==null)return new object[0];return d.Keys.Cast<object>().Select(k=>Body(d[k],k.ToString())).ToArray();}
 object Snapshot(bool detail=true){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();var p=Read(r,"physical");var sim=Read(r,"simulation") as CitySimulator;var m=BuildingManager.Instance;var iv=PC("NativeInteriorCityView");var state=Read(iv,"State");
  var floor=Read(iv,"cachedFloor") as Transform;var payload=SaveGameManager.Current.modData.Where(k=>detail && k.Key.StartsWith("persistentcity")).ToDictionary(k=>k.Key,k=>new{length=k.Value.Length,schema=Schema(k.Value)});
  var bindings=Bodies(p);var people=UnityEngine.Object.FindObjectsOfType<Streets.Pedestrians.Pedestrian>().Where(x=>x.gameObject.activeInHierarchy).Select(x=>new{id=x.GetInstanceID(),position=Vec(x.transform.position)}).ToArray();
  var cars=UnityEngine.Object.FindObjectsOfType<GleyTrafficSystem.VehicleComponent>().Where(x=>x.gameObject.activeInHierarchy).Select(x=>new{id=x.GetInstanceID(),position=Vec(x.transform.position),name=x.name}).ToArray();
  return new{frame=Time.frameCount,seconds=Time.unscaledTime,day=SaveGameManager.Current.Day,hour=SaveGameManager.Current.Hour,minute=SaveGameManager.Current.Minute,timeScale=Time.timeScale,nativeSpeed=Read(Read(InstanceBehavior<UI.UIs>.Instance.gameSpeed,"currentGameSpeed"),"speed")?.ToString(),
   inside=BuildingManager.IsInsideBuilding,entering=m!=null&&m.enteringBuilding,exiting=m!=null&&m.exitingBuilding,address=m?.building?.Address.ToString(),size=m?.building?.BuildingSize,version=m?.building?.BuildingVersion,elevator=m?.multipleHeightsBuildingController!=null,floor=floor==null?(float?)null:floor.position.y,
   configured=Read(iv,"Configured"),viewActive=Read(iv,"Active"),fallback=Read(state,"Fallback")?.ToString(),anchor=Read(state,"Anchor"),revision=Read(state,"Revision"),hidden=Read(PC("NativeTravelPace"),"HiddenTravel"),
   physical=p!=null,diagnostics=Read(p,"Diagnostics"),bindings,acquired=Read(p,"acquired"),released=Read(p,"released"),attempted=(Read(p,"attempted") as IDictionary)?.Count,
   citizens=sim?.State.Citizens.Count,ids=detail?sim?.State.Citizens.Select(c=>c.Identity.Id).OrderBy(x=>x).ToArray():null,failed=Read(r,"failed"),status=Read(r,"status"),payload,nativePeople=people,nativeCars=cars,
   lighting=new{ambientIntensity=RenderSettings.ambientIntensity,ambientMode=RenderSettings.ambientMode.ToString(),fog=RenderSettings.fog,volumeObjects=(detail?UnityEngine.Object.FindObjectsOfType<Behaviour>():new Behaviour[0]).Where(b=>b.GetType().Name=="Volume").Select(b=>new{id=b.GetInstanceID(),enabled=b.enabled,active=b.gameObject.activeInHierarchy}).ToArray()}};
 }
 static object Schema(string text){try{var j=JObject.Parse(text);return j.Properties().Where(x=>x.Name.ToLowerInvariant().Contains("schema")||x.Name.ToLowerInvariant().Contains("version")).ToDictionary(x=>x.Name,x=>x.Value.ToString());}catch{return null;}}
 bool tracking;float sampleAt;string phase="";readonly List<object> track=new List<object>();
 void LateUpdate(){if(!tracking)return;try{var m=BuildingManager.Instance;var key=BuildingManager.IsInsideBuilding+":"+(m!=null&&m.enteringBuilding)+":"+(m!=null&&m.exitingBuilding)+":"+Read(PC("NativeTravelPace"),"HiddenTravel")+":"+Read(PC("NativeInteriorCityView"),"Active");if(Time.unscaledTime>=sampleAt||key!=phase){sampleAt=Time.unscaledTime+.25f;phase=key;if(track.Count<20000)track.Add(Snapshot(false));}}catch(Exception e){tracking=false;File.WriteAllText(Path.Combine(Root,"f0-track-error.txt"),e.ToString());}}
 object Methods(Type t)=>t.GetMethods(BF).Where(m=>m.DeclaringType==t).Select(m=>new{signature=m.ToString(),parameters=m.GetParameters().Select(p=>new{name=p.Name,type=p.ParameterType.ToString(),optional=p.IsOptional,defaultValue=p.IsOptional?p.DefaultValue:null}).ToArray()}).ToArray();
 static Guard budgetOwner; readonly List<object> budgets=new List<object>(); float lastSelection=-1;
 static void AfterSelection(object __instance){var g=budgetOwner;if(g==null)return;try{float next=(float)Read(__instance,"nextSelection");if(next==g.lastSelection)return;g.lastSelection=next;var attempted=Read(__instance,"attempted") as IDictionary;var current=attempted.Keys.Cast<object>().Where(k=>Math.Abs((float)attempted[k]-Time.unscaledTime)<.00001).Select(k=>k.ToString()).ToArray();g.budgets.Add(new{frame=Time.frameCount,seconds=Time.unscaledTime,attempts=current.Length,ids=current,acquired=Read(__instance,"acquired"),released=Read(__instance,"released"),unavailable=Read(__instance,"failedAcquisitions"),bodies=g.Bodies(__instance)});}catch(Exception e){g.budgets.Add(new{error=e.ToString()});}}
 object F0(string op,JObject q){
  if(op=="f0-state")return Snapshot();
  if(op=="f0-loaded"){var a=typeof(PopulationRuntime).Assembly;string hash=null;using(var h=System.Security.Cryptography.SHA256.Create())using(var f=File.OpenRead(a.Location))hash=BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();return new{assembly=a.GetName().Name,version=a.GetName().Version.ToString(),sha256=hash,frame=Time.frameCount};}
  if(op=="f0-callbacks"){
   var list=new List<object>();foreach(var pair in new[]{Tuple.Create(typeof(GlobalEvents),"onEnterBuilding"),Tuple.Create(typeof(GlobalEvents),"onExitBuilding"),Tuple.Create(typeof(GlobalEvents),"onTimeMachineStarted"),Tuple.Create(typeof(GlobalEvents),"onGameUnloaded"),Tuple.Create(typeof(GlobalEvents),"onSaveGame"),Tuple.Create(typeof(UnityEngine.SceneManagement.SceneManager),"sceneUnloaded")}){var field=pair.Item1.GetField(pair.Item2,BF);var delegates=(field?.GetValue(null) as Delegate)?.GetInvocationList()??new Delegate[0];list.Add(new{target=pair.Item1.FullName+"."+pair.Item2,available=field!=null,persistentCityCallbacks=delegates.Where(x=>x.Method.DeclaringType.FullName.StartsWith("PersistentCity.")).Select(x=>x.Method.DeclaringType.FullName+"."+x.Method.Name).ToArray()});}
   return new{events=list,ambientOwnerPresent=Read(PC("AmbientPedestrians"),"current")!=null,ambientOwnershipActive=Read(Read(PC("AmbientPedestrians"),"current"),"Active"),physicalTrafficOwnershipActive=Read(Read(PC("PhysicalTrafficHooks"),"current"),"Active"),viewConfigured=Read(PC("NativeInteriorCityView"),"Configured")};
  }
  if(op=="f0-track-start"){track.Clear();tracking=true;sampleAt=0;phase="";return new{started=true};}
  if(op=="f0-track-stop"){tracking=false;var path=Path.Combine(Root,"f0-track-"+(string)q["id"]+".json");File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(track));return new{samples=track.Count,file=Path.GetFileName(path)};}
  if(op=="f0-methods")return new{buildingManager=Methods(typeof(BuildingManager)),gameSpeed=Methods(InstanceBehavior<UI.UIs>.Instance.gameSpeed.GetType()),cityController=Methods(BuildingManager.Instance.cityBuildingController?.GetType()??typeof(BuildingManager)),runtime=Methods(typeof(PopulationRuntime)),timeMachine=Methods(InstanceBehavior<UI.UIs>.Instance.timeMachine.GetType()),entryTypes=typeof(PopulationRuntime).Assembly.GetTypes().Where(t=>t.Name.Contains("Entry")||t.Name.Contains("Mod")).Select(t=>new{type=t.FullName,methods=Methods(t)}).ToArray()};
  if(op=="f0-shops")return SaveGameManager.Current.BuildingRegistrations.Where(r=>r.HasEstablishedBusiness).Select(r=>new{address=r.BuildingCached.Address.ToString(),r.RentedByPlayer,r.businessTypeName,r.BusinessName,size=r.BuildingCached.BuildingSize,version=r.BuildingCached.BuildingVersion}).ToArray();
  if(op=="f0-contract"){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();var sim=Read(r,"simulation") as CitySimulator;var state=JObject.Parse(SaveGameManager.Current.modData[SaveCodec.StateKey]);return new{key=SaveCodec.StateKey,liveSchema=sim?.State.Schema,savedSchema=state["Schema"],topLevelFields=state.Properties().Select(p=>p.Name).OrderBy(x=>x).ToArray(),interiorPropertyNames=state.Descendants().OfType<JProperty>().Where(p=>p.Name.IndexOf("interior",StringComparison.OrdinalIgnoreCase)>=0).Select(p=>p.Name).Distinct().ToArray(),namespaceFamilies=SaveGameManager.Current.modData.Keys.Where(k=>k.StartsWith("persistentcity:")).Select(k=>k.StartsWith("persistentcity:food-sale-")?k.Split(':')[0]+":"+k.Split(':')[1]:k).Distinct().OrderBy(x=>x).ToArray()};}
  if(op=="f0-failclosed"){Invoke(PC("NativeInteriorCityView"),"FailClosed");return new{seam="existing FailClosed; no injected exception",state=Snapshot()};}
  if(op=="f0-aux"){var scene=UnityEngine.SceneManagement.SceneManager.CreateScene("rig_test_F0_auxiliary");var operation=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);operation.completed+=unused=>File.WriteAllText(Path.Combine(Root,"f0-aux-completed.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{frame=Time.frameCount,completed=true,sceneName=scene.name,isLoaded=scene.isLoaded,remainingScenes=UnityEngine.SceneManagement.SceneManager.sceneCount}));return new{createdAndUnloadRequested=true};}
  if(op=="f0-budget-start"){budgets.Clear();lastSelection=-1;budgetOwner=this;new Harmony("persistentcity.rig_test.f0selection").Patch(AccessTools.Method(PC("PhysicalPopulation"),"Update"),postfix:new HarmonyMethod(typeof(Guard),"AfterSelection"));return new{started=true,passive=true};}
  if(op=="f0-budget-stop"){budgetOwner=null;new Harmony("persistentcity.rig_test.f0selection").UnpatchAll("persistentcity.rig_test.f0selection");var file="f0-budget-"+(string)q["id"]+".json";File.WriteAllText(Path.Combine(Root,file),Newtonsoft.Json.JsonConvert.SerializeObject(budgets));return new{rows=budgets.Count,file};}
  if(op=="f0-invoke"){
   string target=(string)q["target"],method=(string)q["method"];object obj=target=="building"?(object)BuildingManager.Instance:target=="runtime"?UnityEngine.Object.FindObjectOfType<PopulationRuntime>():target=="timeMachine"?InstanceBehavior<UI.UIs>.Instance.timeMachine:target=="gameSpeed"?InstanceBehavior<UI.UIs>.Instance.gameSpeed:throw new Exception("Unknown native target");
   var types=obj.GetType().GetMethods(BF).Where(m=>m.Name==method).ToArray();var args=q["args"] as JArray??new JArray();var mi=types.Single(m=>m.GetParameters().Length==args.Count);var ps=mi.GetParameters();var converted=args.Select((a,i)=>a.Type==JTokenType.Null?null:a.ToObject(ps[i].ParameterType)).ToArray();var before=Snapshot(false);var result=mi.Invoke(obj,converted);return new{nativeMethod=mi.ToString(),result=result?.ToString(),before,after=Snapshot(false)};
  }
  throw new Exception("Unknown F0 diagnostic");
 }
}}
