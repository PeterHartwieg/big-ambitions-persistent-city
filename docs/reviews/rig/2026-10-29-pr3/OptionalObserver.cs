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
 public static object Arm(){Guard();armed=true;return new{armed};}
 public static void SetRoot(string r){root=r;fixtureRoot=r;}
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static object Get(object o,string n)=>o==null?null:(o.GetType().GetField(n,flags)?.GetValue(o)??o.GetType().GetProperty(n,flags)?.GetValue(o));
 static object Runtime()=>UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
 static void Guard(){if(!Environment.GetCommandLineArgs().Contains("-agent-tests")||!Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests")||!SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines PR3 ",StringComparison.Ordinal))throw new Exception("PR3 disposable agent1 fixture required");}
 public static void Attach(string r){root=r;h=new Harmony("persistentcity.pr3-optional-observer");if(fixtureHook==null){fixtureHook=new Harmony("persistentcity.pr3-fixture-input");fixtureHook.Patch(AccessTools.Method(typeof(PopulationRuntime),"TryStart"),prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(InjectFixture)));}var a=typeof(CitySimulator).Assembly;
  var policy=a.GetType("PersistentCity.Core.OptionalStopPolicy");if(policy==null)return;
  h.Patch(AccessTools.Method(policy,"Choose"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Choice)));
  h.Patch(AccessTools.Method(typeof(CitySimulator),"ScheduleOptionalStop"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Scheduled)));
  var p=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.PhysicalPopulation");
  h.Patch(AccessTools.Method(p,"ObserveOptional"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Physical)));
 }
 static void InjectFixture(){try{var path=Path.Combine(fixtureRoot??"","inject-fixture.json");if(!File.Exists(path)||ReferenceEquals(injectedSave,SaveGameManager.Current))return;Guard();var q=JObject.Parse(File.ReadAllText(path));if((string)q["save"]!=SaveGameManager.Current.SaveGameName)throw new Exception("Injection save name mismatch");string n=(string)q["file"];if(Path.GetFileName(n)!=n)throw new Exception("Injection filename only");var source=File.ReadAllText(Path.Combine(fixtureRoot,n));SaveGameManager.Current.modData[PersistentCity.Persistence.SaveCodec.StateKey]=source;injectedSave=SaveGameManager.Current;File.WriteAllText(Path.Combine(fixtureRoot,"injected-fixture.json"),JsonConvert.SerializeObject(new{save=SaveGameManager.Current.SaveGameName,file=n,bytes=System.Text.Encoding.UTF8.GetByteCount(source)}));}catch(Exception e){Debug.LogError("PR3 fixture injector: "+e);throw;}}
 public static void Detach(){recording=false;h?.UnpatchAll("persistentcity.pr3-optional-observer");}
 static void Write(string kind,object value){File.AppendAllText(output,JsonConvert.SerializeObject(new{kind,value})+Environment.NewLine);}
 static void Choice(string key,int seed,int percentage,bool __result){if(!recording)return;try{Guard();Write("choice",new{key,seed,percentage,accepted=__result,minute=FinanceObserver.Simulation().State.Minute});}catch(Exception e){faults.Add(e.ToString());}}
 static void Scheduled(CitizenState person){if(!recording)return;try{Guard();var stop=Get(person,"OptionalStop");if(stop!=null)Write("scheduled",new{id=person.Identity.Id,minute=FinanceObserver.Simulation().State.Minute,place=person.PlaceId,activity=person.Activity.ToString(),stop});}catch(Exception e){faults.Add(e.ToString());}}
 static void Physical(object binding,bool __result){if(!recording)return;try{
  var c=(CitizenState)Get(binding,"Citizen");var stop=Get(binding,"Optional");var body=Get(binding,"Person");
  string id=c.Identity.Id;var pos=Get(body,"Position");string phase;
  if(stop==null)phase=__result?"resumed-walk":"returned-entrance";
  else if((bool)Get(binding,"OptionalReturning"))phase="returning";
  else phase=Position.Distance((Position)pos,(Position)Get(stop,"Position"))<=.3?"dwell":"approaching";
  if(armed && phase=="dwell"){armed=false;UnityEngine.Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true,true);File.WriteAllText(Path.Combine(root,"optional-dwell-checkpoint.json"),JsonConvert.SerializeObject(new{id,minute=FinanceObserver.Simulation().State.Minute,position=pos,stop}));}
  if(phases.TryGetValue(id,out var old)&&old==phase)return;phases[id]=phase;
  Write("physical",new{id,phase,minute=FinanceObserver.Simulation().State.Minute,frame=Time.frameCount,bodyGeneration=Get(body,"Generation"),bodyInstance=(Get(body,"Character") as UnityEngine.Object)?.GetInstanceID(),position=pos,stop,activity=c.Activity.ToString(),citizenPhase=c.Phase.ToString(),c.PlaceId,c.CompletedJourneys});
 }catch(Exception e){faults.Add(e.ToString());}}
 public static object Start(string r,string id){root=r;Guard();phases.Clear();faults.Clear();output=Path.Combine(root,id+"-optional.jsonl");recording=true;Write("start",Status());return new{recording,output};}
 public static object Stop(){Guard();Write("end",Status());recording=false;return new{recording,output,faults=faults.ToArray()};}
 public static object Status(){Guard();var sim=FinanceObserver.Simulation();var r=Runtime();var settings=Get(r,"settings");
  var rows=sim.State.Citizens.Select(c=>new{id=c.Identity.Id,c.PlaceId,activity=c.Activity.ToString(),phase=c.Phase.ToString(),c.CompletedJourneys,choice=Get(c,"OptionalStopChoice"),stop=Get(c,"OptionalStop")}).Where(c=>c.stop!=null).ToArray();
  return new{featurePresent=typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.OptionalStopPolicy")!=null,configuredPercentage=Get(settings,"OptionalStopChoicePercentage"),attachedPercentage=Get(sim,"stopPercentage"),minute=sim.State.Minute,raining=RainHelper.isRaining,stops=rows,faults=faults.ToArray()};
 }
 public static object Codec(string r,string name){root=r;Guard();if(Path.GetFileName(name)!=name)throw new Exception("Filename only");string source=File.ReadAllText(Path.Combine(root,name));var dict=new Dictionary<string,string>{{PersistentCity.Persistence.SaveCodec.StateKey,source}};var live=FinanceObserver.Simulation().State;
  var session=PersistentCity.Persistence.SaveSession.Open(dict,(string)JObject.Parse(source)["PopulationKey"]);var read=session.InitialReadResult;var write=session.WriteTo(dict,read.State??live);string after=dict[PersistentCity.Persistence.SaveCodec.StateKey];
  File.WriteAllText(Path.Combine(root,name+".result"),after);return new{status=read.Status.ToString(),read.Error,read.SourceSchema,writeSuccess=write.Success,writeError=write.Error,originalPreserved=source==after,output=name+".result"};
 }
}}
