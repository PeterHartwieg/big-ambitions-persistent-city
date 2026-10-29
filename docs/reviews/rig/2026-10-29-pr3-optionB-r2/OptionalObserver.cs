using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using BigAmbitions.DayNightCycle;
namespace PR3Rig {
// Read-only observer; patches never alter arguments, results, prices or simulation state.
public static class OptionalObserver {
 static Harmony h; static Harmony fixtureHook; static string fixtureRoot; static string root; static bool recording; static string output;
 static readonly Dictionary<string,string> phases=new Dictionary<string,string>();
 static readonly List<string> faults=new List<string>();
 static object injectedSave; static bool armed;
 public static object Arm(string kind=""){Guard();armedKind=kind;armed=true;return new{armed,kind};}
 public static object ArmEvent(string eventKind){Guard();if(!new[]{"hand-on","pass-on","conversation"}.Contains(eventKind))throw new Exception("Unknown observation event");armEvent=eventKind;return new{armEvent};}
 public static void SetRoot(string r){root=r;fixtureRoot=r;}
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static object Get(object o,string n)=>o==null?null:(o.GetType().GetField(n,flags)?.GetValue(o)??o.GetType().GetProperty(n,flags)?.GetValue(o));
 static object Runtime()=>UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
 static void Guard(){if(!Environment.GetCommandLineArgs().Contains("-agent-tests")||!Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests")||!SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines PR3 ",StringComparison.Ordinal))throw new Exception("PR3 disposable agent1 fixture required");}
 public static void Attach(string r){root=r;h=new Harmony("persistentcity.pr3-optional-observer");if(fixtureHook==null){fixtureHook=new Harmony("persistentcity.pr3-fixture-input");fixtureHook.Patch(AccessTools.Method(typeof(PopulationRuntime),"TryStart"),prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(InjectFixture)));}var a=typeof(CitySimulator).Assembly;
  var policy=a.GetType("PersistentCity.Core.OptionalStopPolicy");if(policy==null)return;
  h.Patch(AccessTools.Method(policy,"Choose"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Choice)));
  h.Patch(AccessTools.Method(typeof(CitySimulator),"ScheduleOptionalStop"),prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(BeforeSchedule)),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Scheduled)));
  foreach(var name in new[]{"Drop","PassOnChance","Occupied","AttachOptionalStops"}){var method=AccessTools.Method(typeof(CitySimulator),name);if(method!=null){if(name=="Occupied")h.Patch(method,postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Occupancy)));else h.Patch(method,prefix:new HarmonyMethod(typeof(OptionalObserver),name=="Drop"?nameof(Dropping):name=="PassOnChance"?nameof(Passing):nameof(Attaching)));}}
  h.Patch(AccessTools.Method(typeof(CitySimulator),"FileOptionalStop"),prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(Filing)));
  var env=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.OptionalStopEnvironment");
  if(env!=null){h.Patch(AccessTools.Method(env,"Spots"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(SpotsRead)));h.Patch(AccessTools.Method(env,"Available"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Availability)));}
  var p=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.PhysicalPopulation");
  h.Patch(AccessTools.Method(p,"ObserveOptional"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Physical)));
 }

 static readonly HashSet<string> seen=new HashSet<string>();
 static readonly Dictionary<string,string> admissions=new Dictionary<string,string>();
 static readonly List<double> scheduleMs=new List<double>();
 static long spotCalls,occupancyCalls,occupancyBlocked,facadeDeferrals,spotCount;
 static CitySimulator observedSim; static string currentArrival="",armEvent="",armedKind=""; static bool metricsOnly; static bool inside;static bool lastDraw;static string lastKey="";static string source="";static long beforeDrops,beforePass,beforeHand;static int beforePending;static long timer;
 static int IndexedPending(CitySimulator sim,CitizenState person){
  var bank=Get(sim,"bankedChances");var waiting=Get(sim,"waitingChances");if(bank==null||waiting==null)return Pending(sim);
  int count=Convert.ToInt32(Get(bank,"Count"))+Convert.ToInt32(Get(waiting,"Count"));
  bool filed=(bool)AccessTools.Method(bank.GetType(),"Contains").Invoke(bank,new object[]{person})||(bool)AccessTools.Method(waiting.GetType(),"Contains").Invoke(waiting,new object[]{person});
  bool live=Get(person,"OptionalStopChoice") is bool b&&b&&Get(person,"OptionalStop")==null;
  return count+(live?1:0)-(filed?1:0);
 }
 static long CountField(object o,string n)=>Convert.ToInt64(Get(o,n)??0L);
 static int Pending(CitySimulator sim)=>sim.State.Citizens.Count(c=>Get(c,"OptionalStopChoice") is bool b&&b&&Get(c,"OptionalStop")==null);
 public static object Audit(){var sim=FinanceObserver.Simulation();return new{pending=Pending(sim),tally=Get(sim,"OptionalStopChances"),minute=sim.State.Minute};}
 public static object MetricsStart(){Guard();metricsOnly=true;scheduleMs.Clear();spotCalls=spotCount=occupancyCalls=occupancyBlocked=facadeDeferrals=0;return new{metricsOnly};}
 public static object MetricsStop(){metricsOnly=false;return Metrics();}
 public static object Metrics()=>new{scheduleCalls=scheduleMs.Count,scheduleMilliseconds=scheduleMs.ToArray(),spotCalls,spotCount,occupancyCalls,occupancyBlocked,facadeDeferrals};
 static void BeforeSchedule(CitySimulator __instance,CitizenState person){observedSim=__instance;currentArrival=person.Identity.Id;if(metricsOnly){timer=System.Diagnostics.Stopwatch.GetTimestamp();lastKey="";return;}if(!recording)return;timer=System.Diagnostics.Stopwatch.GetTimestamp();inside=true;lastKey="";lastDraw=false;source=Get(person,"OptionalStopChoice") is bool b&&b?"carried":"direct";var t=Get(__instance,"OptionalStopChances");beforeDrops=CountField(t,"Dropped");beforePass=CountField(t,"PassedOn");beforeHand=CountField(t,"HandedOn");beforePending=IndexedPending(__instance,person);}
 static void Scheduled(CitySimulator __instance,CitizenState person){if(metricsOnly){if(lastKey.Length>0)scheduleMs.Add((System.Diagnostics.Stopwatch.GetTimestamp()-timer)*1000.0/System.Diagnostics.Stopwatch.Frequency);return;}if(!recording)return;try{
  double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-timer)*1000.0/System.Diagnostics.Stopwatch.Frequency;inside=false;
  if(lastKey.Length==0)return;scheduleMs.Add(ms);var tally=Get(__instance,"OptionalStopChances");long hand=CountField(tally,"HandedOn")-beforeHand;long pass=CountField(tally,"PassedOn")-beforePass;long drops=CountField(tally,"Dropped")-beforeDrops;
  var stop=Get(person,"OptionalStop");var recipients=new List<object>();
  if(stop!=null){var ids=new List<string>{person.Identity.Id};var members=Get(stop,"Members") as IEnumerable<string>;if(members!=null)ids.AddRange(members);
   foreach(string id in ids.Distinct()){var c=__instance.State.Citizens.First(x=>x.Identity.Id==id);var st=Get(c,"OptionalStop");if(st==null)continue;string key=id+":"+Get(st,"PlaceId")+":"+Convert.ToDouble(Get(st,"VisitMinute")).ToString("R",System.Globalization.CultureInfo.InvariantCulture);if(!seen.Add(key))continue;
    var row=new{id,key,minute=__instance.State.Minute,place=c.PlaceId,activity=c.Activity.ToString(),source=id!=person.Identity.Id?"conversation-partner":hand>0?"handed-on":source,stop=st};recipients.Add(row);Write("scheduled",row);
   }
  }
  Write("admission",new{id=person.Identity.Id,key=lastKey,drawn=lastDraw,minute=__instance.State.Minute,place=person.PlaceId,source,percentage=Get(__instance,"stopPercentage"),handedOn=hand,passedOn=pass,dropped=drops,pendingBefore=beforePending,pendingAfter=IndexedPending(__instance,person),recipients=recipients.Count,milliseconds=ms,tally});

  bool paired=stop!=null&&((IEnumerable<string>)Get(stop,"Members")).Count()==2;
  if(armEvent=="hand-on"&&hand>0||armEvent=="pass-on"&&pass>0||armEvent=="conversation"&&paired){
   string observed=armEvent;armEvent="";UnityEngine.Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true,true);
   File.WriteAllText(Path.Combine(root,"optional-event-checkpoint.json"),JsonConvert.SerializeObject(new{observed,id=person.Identity.Id,key=lastKey,minute=__instance.State.Minute,hand,pass,stop,pending=IndexedPending(__instance,person),tally}));
  }
 }catch(Exception e){faults.Add(e.ToString());inside=false;}}
 static void Filing(CitySimulator __instance,CitizenState person){if(!recording||!inside||Get(person,"OptionalStop")!=null||Get(person,"OptionalStopChoice") is bool b&&b)return;try{
  var bank=Get(__instance,"bankedChances");var waiting=Get(__instance,"waitingChances");if(bank==null||waiting==null)return;
  if((bool)AccessTools.Method(bank.GetType(),"Contains").Invoke(bank,new object[]{person})||(bool)AccessTools.Method(waiting.GetType(),"Contains").Invoke(waiting,new object[]{person}))Write("pending-cleared",new{id=person.Identity.Id,arriving=currentArrival,key=lastKey,minute=__instance.State.Minute});
 }catch(Exception e){faults.Add(e.ToString());}}
 static void Dropping(CitizenState holder){if(!recording)return;try{Write("drop",new{id=holder.Identity.Id,reason=Convert.ToInt32(Get(observedSim,"stopPercentage"))==100?"100-policy-trim":"trim",minute=observedSim.State.Minute});}catch(Exception e){faults.Add(e.Message);}}
 static void Passing(CitySimulator __instance){if(!recording)return;try{var free=Get(__instance,"freeOfChances");var first=Get(free,"Min") as CitizenState;Write("pass-attempt",new{donor=currentArrival,recipient=first?.Identity.Id,available=first!=null,minute=__instance.State.Minute});}catch(Exception e){faults.Add(e.Message);}}
 static void Occupancy(bool __result){if(recording||metricsOnly){occupancyCalls++;if(__result)occupancyBlocked++;}}
 static void Attaching(){if(recording)Write("attach-reset",Audit());}
 static void SpotsRead(object __instance,Place place,DwellKind kind,object __result){if(!recording&&!metricsOnly)return;spotCalls++;int count=__result is ICollection collection?collection.Count:Convert.ToInt32(Get(__result,"Count")??0);spotCount+=count;var doors=Get(__instance,"doors");bool deferred=count==0&&kind!=DwellKind.Audience&&kind!=DwellKind.Resting&&doors!=null&&(bool)(AccessTools.Method(doors.GetType(),"Unsampled").Invoke(doors,new object[]{place}))&&(bool)Get(doors,"SampledThisFrame");if(deferred)facadeDeferrals++;if(inside)Write("spots",new{place=place.Id,kind=kind.ToString(),count,deferred,minute=observedSim.State.Minute});}
 static void Availability(object __result){if((recording||metricsOnly)&&__result.ToString()=="Deferred")facadeDeferrals++;}

 static void InjectFixture(){try{var path=Path.Combine(fixtureRoot??"","inject-fixture.json");if(!File.Exists(path)||ReferenceEquals(injectedSave,SaveGameManager.Current))return;Guard();var q=JObject.Parse(File.ReadAllText(path));if((string)q["save"]!=SaveGameManager.Current.SaveGameName)throw new Exception("Injection save name mismatch");string n=(string)q["file"];if(Path.GetFileName(n)!=n)throw new Exception("Injection filename only");var source=File.ReadAllText(Path.Combine(fixtureRoot,n));SaveGameManager.Current.modData[PersistentCity.Persistence.SaveCodec.StateKey]=source;injectedSave=SaveGameManager.Current;File.WriteAllText(Path.Combine(fixtureRoot,"injected-fixture.json"),JsonConvert.SerializeObject(new{save=SaveGameManager.Current.SaveGameName,file=n,bytes=System.Text.Encoding.UTF8.GetByteCount(source)}));}catch(Exception e){Debug.LogError("PR3 fixture injector: "+e);throw;}}
 public static void Detach(){recording=false;h?.UnpatchAll("persistentcity.pr3-optional-observer");}
 static void Write(string kind,object value){File.AppendAllText(output,JsonConvert.SerializeObject(new{kind,value})+Environment.NewLine);}
 static void Choice(string key,int seed,int percentage,bool __result){if(metricsOnly){lastKey=key;return;}if(!recording)return;try{Guard();lastKey=key;lastDraw=__result;Write("choice",new{key,seed,percentage,accepted=__result,minute=observedSim.State.Minute});}catch(Exception e){faults.Add(e.ToString());}}
 static void OldScheduled(CitizenState person){if(!recording)return;try{Guard();var stop=Get(person,"OptionalStop");if(stop!=null)Write("scheduled",new{id=person.Identity.Id,minute=FinanceObserver.Simulation().State.Minute,place=person.PlaceId,activity=person.Activity.ToString(),stop});}catch(Exception e){faults.Add(e.ToString());}}
 static void Physical(object binding,bool __result){if(!recording)return;try{
  var c=(CitizenState)Get(binding,"Citizen");var stop=Get(binding,"Optional");var body=Get(binding,"Person");
  string id=c.Identity.Id;var pos=Get(body,"Position");string phase;
  if(stop==null)phase=__result?"resumed-walk":"returned-entrance";
  else if((bool)Get(binding,"OptionalReturning"))phase="returning";
  else phase=Position.Distance((Position)pos,(Position)Get(stop,"Position"))<=.3?"dwell":"approaching";
  if(armed && phase=="dwell"&&(armedKind.Length==0||Get(stop,"Kind").ToString()==armedKind)){armed=false;UnityEngine.Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true,true);File.WriteAllText(Path.Combine(root,"optional-dwell-checkpoint.json"),JsonConvert.SerializeObject(new{id,minute=FinanceObserver.Simulation().State.Minute,position=pos,stop}));}
  if(phases.TryGetValue(id,out var old)&&old==phase)return;phases[id]=phase;
  Write("physical",new{id,phase,minute=FinanceObserver.Simulation().State.Minute,frame=Time.frameCount,bodyGeneration=Get(body,"Generation"),bodyInstance=(Get(body,"Character") as UnityEngine.Object)?.GetInstanceID(),position=pos,stop,activity=c.Activity.ToString(),citizenPhase=c.Phase.ToString(),c.PlaceId,c.CompletedJourneys});
 }catch(Exception e){faults.Add(e.ToString());}}
 public static object Start(string r,string id){root=r;Guard();phases.Clear();faults.Clear();seen.Clear();admissions.Clear();spotCalls=occupancyCalls=occupancyBlocked=facadeDeferrals=spotCount=0;scheduleMs.Clear();output=Path.Combine(root,id+"-optional.jsonl");recording=true;Write("start",Status());return new{recording,output};}
 public static object Stop(){Guard();Write("end",Status());recording=false;return new{recording,output,faults=faults.ToArray()};}
 public static object Status(){Guard();var sim=FinanceObserver.Simulation();var r=Runtime();var settings=Get(r,"settings");
  var rows=sim.State.Citizens.Select(c=>new{id=c.Identity.Id,c.PlaceId,activity=c.Activity.ToString(),phase=c.Phase.ToString(),c.CompletedJourneys,choice=Get(c,"OptionalStopChoice"),stop=Get(c,"OptionalStop")}).Where(c=>c.stop!=null).ToArray();
  return new{audit=Audit(),metrics=Metrics(),featurePresent=typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.OptionalStopPolicy")!=null,configuredPercentage=Get(settings,"OptionalStopChoicePercentage"),attachedPercentage=Get(sim,"stopPercentage"),minute=sim.State.Minute,raining=RainHelper.isRaining,stops=rows,faults=faults.ToArray()};
 }
 public static object Codec(string r,string name){root=r;Guard();if(Path.GetFileName(name)!=name)throw new Exception("Filename only");string source=File.ReadAllText(Path.Combine(root,name));var dict=new Dictionary<string,string>{{PersistentCity.Persistence.SaveCodec.StateKey,source}};var live=FinanceObserver.Simulation().State;
  var session=PersistentCity.Persistence.SaveSession.Open(dict,(string)JObject.Parse(source)["PopulationKey"]);var read=session.InitialReadResult;var write=session.WriteTo(dict,read.State??live);string after=dict[PersistentCity.Persistence.SaveCodec.StateKey];
  File.WriteAllText(Path.Combine(root,name+".result"),after);return new{status=read.Status.ToString(),read.Error,read.SourceSchema,writeSuccess=write.Success,writeError=write.Error,originalPreserved=source==after,output=name+".result"};
 }
}}
