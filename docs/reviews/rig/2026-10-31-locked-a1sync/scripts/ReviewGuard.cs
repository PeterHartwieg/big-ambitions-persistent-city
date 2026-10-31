// rig_test only. Observers never change simulation decisions or production binaries.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using PersistentCity.Persistence;
using UnityEngine;
[assembly:RegisterModClass(typeof(PerformanceReview.Entry))]
[assembly:RegisterModClass(typeof(PerformanceReview.BridgeEntry))]
namespace PerformanceReview {
[ModEntryOnInitializationLoad] public class BridgeEntry:IModBigAmbitions {
 Assembly bridge; public string[] RelativeAssetBundlePaths=>Array.Empty<string>();
 public Task OnLoadAsync(ModContext c){
  if(!Environment.GetCommandLineArgs().Contains("-agent-tests") || !Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests"))return Task.CompletedTask;
  var path=File.ReadAllText(Path.Combine(c.ModRootPath,"bridge-link.private"));
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name+".dll";var found=Directory.GetFiles(Path.GetDirectoryName(path),name,SearchOption.AllDirectories).FirstOrDefault();return found==null?null:Assembly.LoadFrom(found);};
  bridge=Assembly.LoadFrom(path);
  var log=bridge.GetType("FoodTrucks.Log");log.GetMethod("Init",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{c.Logger});
  var patches=bridge.GetType("FoodTrucks.Patches");patches.GetField("harmony",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new Harmony("persistentcity.rig_test.bridge"));
  bridge.GetType("FoodTrucks.TestBridge").GetMethod("Start",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
  c.Logger.Info("rig_test bridge-only host: Food Trucks gameplay entry points were not invoked.");return Task.CompletedTask;
 }
 public Task OnUnloadAsync(){bridge?.GetType("FoodTrucks.TestBridge").GetMethod("Stop",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);return Task.CompletedTask;}
}
[ModEntryOnCityLoad] public class Entry:IModBigAmbitions {
 GameObject host; public string[] RelativeAssetBundlePaths=>Array.Empty<string>();
 public Task OnLoadAsync(ModContext c){if(Environment.GetCommandLineArgs().Contains("-agent-tests") && Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests")){host=new GameObject("rig_test review observer");host.AddComponent<Guard>().Root=c.ModRootPath;}return Task.CompletedTask;}
 public Task OnUnloadAsync(){if(host!=null)UnityEngine.Object.Destroy(host);return Task.CompletedTask;}
}
public partial class Guard:MonoBehaviour {
 public string Root=""; static Guard owner; const string PatchId="persistentcity.rig_test.review";
 bool original,recording; string last=""; int saveMessages; float next;
 Func<long> allocated; string counterKind="unavailable";
 readonly List<Row> rows=new List<Row>(65536); readonly Dictionary<MethodBase,string> methods=new Dictionary<MethodBase,string>();
 readonly Dictionary<string,int> depths=new Dictionary<string,int>();
 readonly Dictionary<string,Value> values=new Dictionary<string,Value>();
 int frame=-1,errors; long first,lastBytes;
 struct Value {public long Ticks,Bytes,Calls;}
 struct Row {public int Frame;public string Bucket;public long Ticks,Bytes,Calls;}
 public struct Scope {public Guard Owner;public string Bucket;public long Start,Bytes;public int Frame;public bool Outer;}
 System.Collections.IEnumerator Start(){while(UI.Load.LoadScene.isLoading)yield return null;var asm=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="FoodTrucks");var type=asm?.GetType("FoodTrucks.TestBridgeCommands");var status=type?.GetProperty("LoadStatus",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);if((string)status?.GetValue(null)=="pending"){type.GetMethod("EndLoad",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"done",null});UnityEngine.Debug.Log("rig_test: native city load verified; bridge-only readiness acknowledged without Food Trucks attachment.");}}
 void OnEnable(){owner=this;InstallFormatObserver();original=GameManager.preventAutoSave;Application.logMessageReceived+=Log;
  try{var m=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);if(m!=null){var read=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),m);long a=read();var b=new byte[128];long c=read();GC.KeepAlive(b);if(c>a){allocated=read;counterKind="thread-allocated-bytes";}}}catch{}
 }
 void Log(string s,string st,LogType t){if(s.IndexOf("saving",StringComparison.OrdinalIgnoreCase)>=0 || s.IndexOf("saved '",StringComparison.OrdinalIgnoreCase)>=0)saveMessages++;}
 void OnDestroy(){new Harmony("persistentcity.rig_test.address-observer").UnpatchAll("persistentcity.rig_test.address-observer");new Harmony("persistentcity.rig_test.market").UnpatchAll("persistentcity.rig_test.market");new Harmony("persistentcity.rig_test.units").UnpatchAll("persistentcity.rig_test.units");Stop();Application.logMessageReceived-=Log;GameManager.SetPreventAutoSave(original);if(owner==this)owner=null;}
 long Bytes(){try{return allocated==null?-1:allocated();}catch{return -1;}}
 void Flush(int current){if(frame==current)return;if(frame>=0){foreach(var pair in values){var v=pair.Value;rows.Add(new Row{Frame=frame,Bucket=pair.Key,Ticks=v.Ticks,Bytes=v.Bytes,Calls=v.Calls});}long b=Bytes();rows.Add(new Row{Frame=frame,Bucket="all-main-thread",Bytes=lastBytes<0||b<0?-1:b-lastBytes});lastBytes=b;}values.Clear();frame=current;}
 void Patch(Harmony h,Type t,string name,string bucket){var m=AccessTools.Method(t,name);if(m==null)throw new Exception("Missing observer seam "+t+"."+name);methods[m]=bucket;h.Patch(m,prefix:new HarmonyMethod(typeof(Timing),nameof(Timing.Before)),finalizer:new HarmonyMethod(typeof(Timing),nameof(Timing.After)));}
 void BeginRecording(){Stop();rows.Clear();methods.Clear();depths.Clear();values.Clear();errors=0;frame=-1;lastBytes=Bytes();first=Stopwatch.GetTimestamp();recording=true;var h=new Harmony(PatchId);
  Patch(h,typeof(PopulationRuntime),"Update","population-update");
  var asm=typeof(PopulationRuntime).Assembly;
  Patch(h,asm.GetType("PersistentCity.Runtime.NativeFoodOwnership"),"AfterNativeTick","food-authority");
  Patch(h,typeof(PopulationRuntime),"AdvancePopulation","advancement");
  Patch(h,asm.GetType("PersistentCity.Runtime.PhysicalPopulation"),"Update","physical-population");
  Patch(h,typeof(TrafficKernel),"AdvanceTo","traffic-core");
  Patch(h,typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.FoodCommerce"),"Step","food-commerce");
  Patch(h,typeof(CitySimulator),"RefreshOptionalStops","optional-refresh");
  Patch(h,typeof(PopulationRuntime),"Save","save");
  Patch(h,typeof(SaveCodec),"Write","serialize");
 }
 object Stop(){if(recording)Flush(Time.frameCount);recording=false;new Harmony(PatchId).UnpatchAll(PatchId);return new{rows=rows.Count,errors,counterKind,seconds=(Stopwatch.GetTimestamp()-first)/(double)Stopwatch.Frequency};}
 static class Timing {
  public static void Before(MethodBase __originalMethod,out Scope __state){__state=default;var o=owner;if(o==null||!o.recording)return;try{o.Flush(Time.frameCount);string b=o.methods[__originalMethod];o.depths.TryGetValue(b,out int depth);o.depths[b]=depth+1;__state=new Scope{Owner=o,Bucket=b,Start=Stopwatch.GetTimestamp(),Bytes=o.Bytes(),Frame=Time.frameCount,Outer=depth==0};}catch{o.errors++;}}
  public static Exception After(Exception __exception,Scope __state){var o=__state.Owner;if(o==null)return __exception;try{o.depths[__state.Bucket]--;if(__state.Outer){if(__state.Frame!=Time.frameCount){o.errors++;return __exception;}o.values.TryGetValue(__state.Bucket,out Value v);v.Ticks+=Stopwatch.GetTimestamp()-__state.Start;long b=o.Bytes();v.Bytes+=b<0||__state.Bytes<0?0:b-__state.Bytes;v.Calls++;o.values[__state.Bucket]=v;}}catch{o.errors++;}return __exception;}
 }
 object Autosave()=>new{originalPreventAutoSave=original,preventAutoSave=GameManager.preventAutoSave,minutesBetweenAutoSaves=PlayerPrefSettings.MinutesBetweenAutoSaves,maxAutoSavesPerGame=PlayerPrefSettings.MaxAutoSavesPerGame,savingGameInProgress=SaveGameManager.SavingGameInProgress,saveLogMessages=saveMessages,save=SaveGameManager.Current.SaveGameName,frame=Time.frameCount};
 int pointerMoves,pointerDowns,pointerUps,pointerWheels;
 void OnGUI(){var e=Event.current;if(e==null)return;if(e.type==EventType.MouseMove)pointerMoves++;if(e.type==EventType.MouseDown)pointerDowns++;if(e.type==EventType.MouseUp)pointerUps++;if(e.type==EventType.ScrollWheel)pointerWheels++;}
 object EnvironmentState()=>new{frame=Time.frameCount,focused=Application.isFocused,runInBackground=Application.runInBackground,targetFrameRate=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,width=Screen.width,height=Screen.height,mouse=Input.mousePosition.ToString(),left=Input.GetMouseButton(0),wheel=Input.mouseScrollDelta.ToString(),pointerMoves,pointerDowns,pointerUps,pointerWheels,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),assemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).ToArray()};
 object State(){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();var f=BindingFlags.Instance|BindingFlags.NonPublic;var sim=(CitySimulator)typeof(PopulationRuntime).GetField("simulation",f).GetValue(r);return new{minute=sim?.State.Minute,nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute,citizens=sim?.State.Citizens.Count,status=typeof(PopulationRuntime).GetField("status",f).GetValue(r),failed=typeof(PopulationRuntime).GetField("failed",f).GetValue(r),physical=typeof(PopulationRuntime).GetField("physical",f).GetValue(r)?.ToString(),autosave=Autosave()};}
 object InspectorSnapshot(){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();var f=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;var sf=typeof(PopulationRuntime).GetField("simulation",f);var sim=(CitySimulator)sf.GetValue(r);var ins=typeof(PopulationRuntime).GetField("inspector",f)?.GetValue(r);var fields=new Dictionary<string,object>();if(ins!=null)foreach(var n in new[]{"hoveredId","hoveredRect","selectedId","body","personality","header","homeLabel","workLabel","visible","popup","guardFailed","wheelFailed","uiBlockInstalled","uiBlockFailed"})fields[n]=ins.GetType().GetField(n,f)?.GetValue(ins)?.ToString();var cam=UnityEngine.Object.FindObjectOfType<CameraControllers.PedestrianCam>();var payload=SaveCodec.Write(sim.State);string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-","").ToLowerInvariant();return new{available=ins!=null,frame=Time.frameCount,minute=sim.State.Minute,stateSha256=hash,savedPayloadLength=SaveGameManager.Current.modData[SaveCodec.StateKey].Length,fields,cameraDistance=cam==null?0:cam.distance,blockCameraZoom=CameraControllers.PedestrianCam.blockCameraZoom,leftHeld=typeof(MouseController).GetField("LeftButtonHeld",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)?.GetValue(null),rightHeld=typeof(MouseController).GetField("RightButtonHeld",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)?.GetValue(null),mouse=Input.mousePosition.ToString()};}
 object Patches(){return Harmony.GetAllPatchedMethods().Where(m=>Harmony.GetPatchInfo(m).Owners.Any(n=>n.IndexOf("persistent",StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("performance",StringComparison.OrdinalIgnoreCase)>=0)).Select(m=>new{method=m.DeclaringType.FullName+"."+m.Name,owners=Harmony.GetPatchInfo(m).Owners.ToArray(),prefix=Harmony.GetPatchInfo(m).Prefixes.Select(p=>new{p.owner,p.priority,method=p.PatchMethod.DeclaringType.FullName+"."+p.PatchMethod.Name}).ToArray(),postfix=Harmony.GetPatchInfo(m).Postfixes.Select(p=>p.owner).ToArray(),finalizers=Harmony.GetPatchInfo(m).Finalizers.Select(p=>p.owner).ToArray()}).ToArray();}


 object Counters(){var r=Runtime();var sim=Sim();var routes=typeof(PopulationRuntime).GetField("routes",BF).GetValue(r);var metrics=routes?.GetType().GetProperty("Metrics",BF)?.GetValue(routes);var scalar=typeof(PopulationRuntime).GetFields(BF).Where(f=>f.FieldType.IsPrimitive && (f.Name.Contains("advance")||f.Name.Contains("planning"))).ToDictionary(f=>f.Name,f=>f.GetValue(r));var physical=typeof(PopulationRuntime).GetField("physical",BF).GetValue(r);var dict=physical?.GetType().GetField("bindings",BF)?.GetValue(physical) as System.Collections.IDictionary;var bodies=new List<object>();if(dict!=null)foreach(System.Collections.DictionaryEntry item in dict){var b=item.Value;var person=b.GetType().GetField("Person",BF).GetValue(b);var citizen=(CitizenState)b.GetType().GetField("Citizen",BF).GetValue(b);bodies.Add(new{id=(string)item.Key,identity=citizen.Identity,home=citizen.HomeId,person=person==null?null:new{identity=person.GetType().GetProperty("IdentityId").GetValue(person),generation=person.GetType().GetProperty("Generation").GetValue(person),character=((UnityEngine.Object)person.GetType().GetProperty("Character").GetValue(person)).GetInstanceID()}});}var food=sim.State.Food;return new{frame=Time.frameCount,minute=sim.State.Minute,nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute,sim.PlanningPending,runtime=scalar,routes=metrics==null?null:JObject.FromObject(metrics),marketCalls=new Dictionary<string,long>(observedMarketCalls),marketObserverMethods,physical=physical?.GetType().GetProperty("Diagnostics",BF)?.GetValue(physical),bodies,pendingDecisions=food?.Residents.Count(x=>x.GetType().GetProperty("Decision")?.GetValue(x)!=null),diagnostics=r.FoodDiagnostics};}


 object Link(JObject q){var r=Runtime();var ins=typeof(PopulationRuntime).GetField("inspector",BF).GetValue(r);var insight=ins.GetType().GetField("insight",BF).GetValue(ins);if(insight==null){var sim=Sim();string id=sim.State.Citizens.First(c=>c.DayPlaceId.Length>0).Identity.Id;ins.GetType().GetMethod("Open",BF).Invoke(ins,new object[]{id,new Vector2(700,400)});ins.GetType().GetMethod("Refresh",BF).Invoke(ins,new object[]{sim});insight=ins.GetType().GetField("insight",BF).GetValue(ins);}var address=insight.GetType().GetProperty((string)q["which"]??"Home").GetValue(insight);var method=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeMap").GetMethod("TryFocus",BindingFlags.Static|BindingFlags.Public);object[] args={address,null};bool ok=(bool)method.Invoke(null,args);var a=address as ResidentAddress;var native=a==null?null:SaveGameManager.Current.BuildingRegistrations.FirstOrDefault(reg=>reg.StreetName==a.Street && reg.StreetNumber==a.Number);return new{seamNotGesture=true,success=ok,problem=args[1],mapOpen=CityMap.IsOpen,address,nativeFormattedAddress=native==null?null:Streets.AddressHelper.ToFormattedString(native.Address)};}
 object MoveView(JObject q){if(Time.timeScale!=0)throw new Exception("Pause disposable fixture before moving view");var player=InstanceBehavior<GameManager>.Instance.playerController;Vector3 before=player.transform.position;var target=new Vector3((float)q["x"],before.y,(float)q["z"]);player.transform.position=target;return new{seamNotGesture=true,before=before.ToString(),after=target.ToString()};}


 long observedUnits,observedCitizenUnits,observedTrafficUnits,observedFoodContinuationUnits;
 static void UnitsAfter(AdvanceResult __result){var o=owner;if(o==null)return;object boxed=__result;var type=boxed.GetType();long all=Convert.ToInt64(type.GetProperty("Events").GetValue(boxed));long traffic=Convert.ToInt64(type.GetProperty("TrafficEvents").GetValue(boxed));long continuation=Convert.ToInt64(type.GetProperty("FoodContinuationEvents")?.GetValue(boxed)??0);o.observedUnits+=all;o.observedCitizenUnits+=all-traffic-continuation;o.observedTrafficUnits+=traffic;o.observedFoodContinuationUnits+=continuation;}
 object Units()=>new{observedUnits,observedCitizenUnits,observedTrafficUnits,observedFoodContinuationUnits};


 readonly Dictionary<string,long> observedMarketCalls=new Dictionary<string,long>();int marketObserverMethods;
 static void MarketObserved(MethodBase __originalMethod){var o=owner;if(o==null)return;o.observedMarketCalls.TryGetValue(__originalMethod.Name,out long value);o.observedMarketCalls[__originalMethod.Name]=value+1;}
 void ObserveMarket(){var type=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeFoodMarket");var h=new Harmony("persistentcity.rig_test.market");foreach(var name in new[]{"Read","ReadLocal","ReadBusiness","BeginCapture","CaptureBusiness","Buy"}){var m=AccessTools.Method(type,name);if(m==null)continue;h.Patch(m,prefix:new HarmonyMethod(typeof(Guard),nameof(MarketObserved)));marketObserverMethods++;}}


 int refreshDepth,formatCalls,formatOutsideRefresh,formatDrawCalls,drawDepth,refreshCalls;
 long formatTicks,formatBytes;
 void InstallFormatObserver(){var h=new Harmony("persistentcity.rig_test.address-observer");var ins=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.ResidentInspector");if(ins==null)return;h.Patch(AccessTools.Method(ins,"Refresh"),prefix:new HarmonyMethod(typeof(Guard),nameof(RefreshBefore)),finalizer:new HarmonyMethod(typeof(Guard),nameof(RefreshAfter)));h.Patch(AccessTools.Method(ins,"Draw"),prefix:new HarmonyMethod(typeof(Guard),nameof(DrawBefore)),finalizer:new HarmonyMethod(typeof(Guard),nameof(DrawAfter)));h.Patch(AccessTools.Method(typeof(Streets.AddressHelper),"ToFormattedString",new[]{typeof(Address)}),prefix:new HarmonyMethod(typeof(Guard),nameof(FormatBefore)),finalizer:new HarmonyMethod(typeof(Guard),nameof(FormatAfter)));}
 static void RefreshBefore(){if(owner!=null){owner.refreshDepth++;owner.refreshCalls++;}}
 static Exception RefreshAfter(Exception __exception){if(owner!=null)owner.refreshDepth--;return __exception;}
 static void DrawBefore(){if(owner!=null)owner.drawDepth++;}
 static Exception DrawAfter(Exception __exception){if(owner!=null)owner.drawDepth--;return __exception;}
 public struct FormatScope{public bool Count;public long Start,Bytes;}
 static void FormatBefore(out FormatScope __state){__state=default;var o=owner;if(o==null)return;/* Count only formatter calls originating in inspector Refresh/Draw. Independently queried native calls are excluded. */if(o.refreshDepth>0||o.drawDepth>0){__state=new FormatScope{Count=true,Start=Stopwatch.GetTimestamp(),Bytes=o.Bytes()};o.formatCalls++;if(o.refreshDepth==0)o.formatOutsideRefresh++;if(o.drawDepth>0)o.formatDrawCalls++;}}
 static Exception FormatAfter(Exception __exception,FormatScope __state){var o=owner;if(o!=null&&__state.Count){o.formatTicks+=Stopwatch.GetTimestamp()-__state.Start;long b=o.Bytes();if(b>=0&&__state.Bytes>=0)o.formatBytes+=b-__state.Bytes;}return __exception;}
 object FormatCounts()=>new{frame=Time.frameCount,refreshCalls,formatCalls,formatOutsideRefresh,formatDrawCalls,formatMilliseconds=formatTicks*1000.0/Stopwatch.Frequency,formatBytes,counterKind};
 object Loaded(){var a=typeof(PopulationRuntime).Assembly;return new{location=a.Location,dllSha256=FileDigest(a.Location),assembly=a.FullName,frame=Time.frameCount};}
 string FileDigest(string p){using(var s=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 object AddressBatch(JObject q){if(Time.timeScale!=0)throw new Exception("Pause before batch evidence");var sim=Sim();var ins=typeof(PopulationRuntime).GetField("inspector",BF).GetValue(Runtime());var ids=(JArray)q["residents"];if(ids==null)throw new Exception("Explicit same residents required");string before=SaveCodec.Write(sim.State);var countersBefore=Counters();var entries=new List<object>();var find=typeof(CitySimulator).GetMethod("FindPlace",BF);
 foreach(string id in ids){var c=sim.State.Citizens.FirstOrDefault(x=>x.Identity.Id==id);if(c==null)throw new Exception("Resident absent");ins.GetType().GetMethod("Open",BF).Invoke(ins,new object[]{id,new Vector2(700,400)});ins.GetType().GetMethod("Refresh",BF).Invoke(ins,new object[]{sim});var insight=ins.GetType().GetField("insight",BF).GetValue(ins);var places=new List<object>();var meal=sim.State.Food?.Residents.FirstOrDefault(m=>m.AnchorId==id&&m.Slot==0);var placeIds=new[]{c.HomeId,c.DayPlaceId,c.DestinationId,c.PlaceId,meal?.PlaceId}.Where(x=>!string.IsNullOrEmpty(x)).Distinct();foreach(var pid in placeIds){var place=(Place)find.Invoke(sim,new object[]{pid});string street;int number;if(ResidentAddress.TryParse(pid,out street,out number)){var native=SaveGameManager.Current.BuildingRegistrations.FirstOrDefault(b=>b.StreetName==street&&b.StreetNumber==number);string formatted=native==null?Streets.AddressHelper.ToFormattedString(new Address(street,number)):Streets.AddressHelper.ToFormattedString(native.Address);places.Add(new{PlaceId=pid,place.Name,Street=street,Number=number,nativeFormatted=formatted,nativeRegistrationFound=native!=null});}}
 var fields=new Dictionary<string,string>();foreach(var n in new[]{"header","personality","homeLabel","workLabel","body"})fields[n]=(string)ins.GetType().GetField(n,BF).GetValue(ins);entries.Add(new{id,identity=c.Identity,homeId=c.HomeId,dayPlaceId=c.DayPlaceId,destinationId=c.DestinationId,currentPlaceId=c.PlaceId,mealPlaceId=meal?.PlaceId,insight,fields,places});}
 string after=SaveCodec.Write(sim.State);return new{seamNotGesture=true,minute=sim.State.Minute,nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute,payloadHashBefore=Digest(before),payloadHashAfter=Digest(after),payloadIdentical=before==after,countersBefore,countersAfter=Counters(),entries};}

 const BindingFlags BF=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 PopulationRuntime Runtime()=>UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
 CitySimulator Sim()=>(CitySimulator)typeof(PopulationRuntime).GetField("simulation",BF).GetValue(Runtime());
 string Digest(string value){using(var s=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(s.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
 object Export(string id){var timer=Stopwatch.StartNew();var payload=SaveCodec.Write(Sim().State);timer.Stop();File.WriteAllText(Path.Combine(Root,"payload-"+id+".json"),payload);string saved=null;SaveGameManager.Current.modData.TryGetValue(SaveCodec.StateKey,out saved);if(saved!=null)File.WriteAllText(Path.Combine(Root,"saved-"+id+".json"),saved);return new{frame=Time.frameCount,minute=Sim().State.Minute,writeMs=timer.Elapsed.TotalMilliseconds,characters=payload.Length,sha256=Digest(payload),savedSha256=saved==null?null:Digest(saved),diagnostics=Runtime().FoodDiagnostics};}
 object Inspect(JObject q){var r=Runtime();var sim=Sim();var ins=typeof(PopulationRuntime).GetField("inspector",BF)?.GetValue(r);if(ins==null)return new{available=false};var id=(string)q["resident"]??sim.State.Citizens.First().Identity.Id;var countersBefore=Counters();var before=SaveCodec.Write(sim.State);string action=(string)q["action"]??"open";
  if(action=="close")ins.GetType().GetMethod("Reset",BF).Invoke(ins,null);
  else {ins.GetType().GetMethod("Open",BF).Invoke(ins,new object[]{id,new Vector2(700,400)});ins.GetType().GetMethod("Refresh",BF).Invoke(ins,new object[]{sim});}
  var after=SaveCodec.Write(sim.State);var snapshot=InspectorSnapshot();return new{available=true,seamNotGesture=true,id,beforeHash=Digest(before),afterHash=Digest(after),identical=before==after,countersBefore,countersAfter=Counters(),snapshot};}
 object CodecCopies(string id){var state=Sim().State;var payload=SaveCodec.Write(state);var parsed=JObject.Parse(payload);var results=new List<object>();
  foreach(var kind in new[]{"corrupt","future"}){string source;if(kind=="future"){var v=(JObject)parsed.DeepClone();v["Schema"]=99;source=v.ToString(Formatting.None);}else source=payload.Substring(0,payload.Length-11);
   string path=Path.Combine(Root,"refusal-"+kind+"-"+id+".json");File.WriteAllText(path,source);var dict=new Dictionary<string,string>{{SaveCodec.StateKey,File.ReadAllText(path)},{"rig_test:sentinel","unchanged"}};var before=Digest(File.ReadAllText(path));var watch=Stopwatch.StartNew();var read=SaveStore.ReadFrom(dict,state.PopulationKey);watch.Stop();var write=SaveStore.WriteTo(dict,state);if(dict[SaveCodec.StateKey]!=source)File.WriteAllText(path,dict[SaveCodec.StateKey]);results.Add(new{kind,status=read.Status.ToString(),read.SourceSchema,read.Error,readMs=watch.Elapsed.TotalMilliseconds,writeAccepted=write.Success,fileHashBefore=before,fileHashAfter=Digest(File.ReadAllText(path)),dictionaryUnchanged=dict[SaveCodec.StateKey]==source,sentinelUnchanged=dict["rig_test:sentinel"]=="unchanged"});}
  return results;}
 object ReadSaved(string id){var source=SaveGameManager.Current.modData[SaveCodec.StateKey];var timer=Stopwatch.StartNew();var read=SaveCodec.Read(source);timer.Stop();string upgraded=read.State==null?null:SaveCodec.Write(read.State);if(upgraded!=null)File.WriteAllText(Path.Combine(Root,"upgraded-"+id+".json"),upgraded);return new{status=read.Status.ToString(),read.SourceSchema,read.Error,readMs=timer.Elapsed.TotalMilliseconds,sourceCharacters=source.Length,upgradedCharacters=upgraded?.Length,sourceHash=Digest(source),upgradedHash=upgraded==null?null:Digest(upgraded)};}

 void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;string p=Path.Combine(Root,"request.json");if(!File.Exists(p))return;string text=File.ReadAllText(p);if(last==text)return;last=text;var q=JObject.Parse(text);string id=(string)q["id"];try{if(!SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines"))throw new Exception("Disposable FT fixture required");string op=(string)q["op"];object result;
 if(op.StartsWith("f0-"))result=F0(op,q);
 else if(op=="address-batch")result=AddressBatch(q);
 else if(op=="loaded")result=Loaded();
 else if(op=="format-counts")result=FormatCounts();
 else if(op=="format-reset"){formatCalls=formatOutsideRefresh=formatDrawCalls=refreshCalls=0;formatTicks=formatBytes=0;result=FormatCounts();}
 else if(op=="units")result=Units();
 else if(op=="units-reset"){observedUnits=observedCitizenUnits=observedTrafficUnits=observedFoodContinuationUnits=0;result=Units();}
 else if(op=="map-link")result=Link(q);
 else if(op=="move-view")result=MoveView(q);
 else if(op=="counters")result=Counters();
 else if(op=="export")result=Export(id);
 else if(op=="inspect-seam")result=Inspect(q);
 else if(op=="codec-copies")result=CodecCopies(id);
 else if(op=="read-saved")result=ReadSaved(id);
 else if(op=="autosave-off"){GameManager.SetPreventAutoSave(true);result=Autosave();}
 else if(op=="autosave-check")result=Autosave();
 else if(op=="state")result=State();
 else if(op=="environment")result=EnvironmentState();
 else if(op=="capture"){string path=Path.Combine(Root,"screen-capture.png");ScreenCapture.CaptureScreenshot(path);result=new{requested=true,file="screen-capture.png",frame=Time.frameCount};}
 else if(op=="inspector-state")result=InspectorSnapshot();
 else if(op=="inspector"){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();var prop=typeof(PopulationRuntime).GetProperty("InspectorTiming");result=prop==null?new{available=false}:(object)prop.GetValue(r);}
 else if(op=="inspector-reset"){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();typeof(PopulationRuntime).GetMethod("ResetInspectorTiming")?.Invoke(r,null);result=new{reset=true};}
 else if(op=="patches")result=Patches();
 else if(op=="extra-start"){BeginRecording();result=new{started=true,counterKind};}
 else if(op=="extra-stop"){result=Stop();using(var w=new StreamWriter(Path.Combine(Root,"buckets-"+id+".csv"))){w.WriteLine("unityFrame,bucket,milliseconds,bytes,calls");foreach(var r in rows)w.WriteLine(r.Frame+","+r.Bucket+","+(r.Ticks*1000.0/Stopwatch.Frequency).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+r.Bytes+","+r.Calls);}}
 else throw new Exception("Unknown operation");
 File.WriteAllText(Path.Combine(Root,"response-"+id+".json"),JsonConvert.SerializeObject(result,Formatting.Indented));
 }catch(Exception e){File.WriteAllText(Path.Combine(Root,"response-"+id+".json"),JsonConvert.SerializeObject(new{error=e.ToString()}));}}
}}
