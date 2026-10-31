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
public class Guard:MonoBehaviour {
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
 void OnEnable(){owner=this;original=GameManager.preventAutoSave;Application.logMessageReceived+=Log;
  try{var m=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);if(m!=null){var read=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),m);long a=read();var b=new byte[128];long c=read();GC.KeepAlive(b);if(c>a){allocated=read;counterKind="thread-allocated-bytes";}}}catch{}
 }
 void Log(string s,string st,LogType t){if(s.IndexOf("saving",StringComparison.OrdinalIgnoreCase)>=0 || s.IndexOf("saved '",StringComparison.OrdinalIgnoreCase)>=0)saveMessages++;}
 void OnDestroy(){Stop();Application.logMessageReceived-=Log;GameManager.SetPreventAutoSave(original);if(owner==this)owner=null;}
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
 void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;string p=Path.Combine(Root,"request.json");if(!File.Exists(p))return;string text=File.ReadAllText(p);if(last==text)return;last=text;var q=JObject.Parse(text);string id=(string)q["id"];try{if(!SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines"))throw new Exception("Disposable FT fixture required");string op=(string)q["op"];object result;
 if(op=="autosave-off"){GameManager.SetPreventAutoSave(true);result=Autosave();}
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
