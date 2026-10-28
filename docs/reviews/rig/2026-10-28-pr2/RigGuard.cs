using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using PersistentCity.Persistence;
using UnityEngine;
[assembly:RegisterModClass(typeof(PR2Rig.Entry))]
namespace PR2Rig {
[ModEntryOnCityLoad] public class Entry:IModBigAmbitions {
 GameObject host; public string[] RelativeAssetBundlePaths=>Array.Empty<string>();
 public Task OnLoadAsync(ModContext c){if(Environment.GetCommandLineArgs().Contains("-agent-tests") && Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests")){host=new GameObject("PR2 rig guard");host.AddComponent<Guard>().Root=c.ModRootPath;}return Task.CompletedTask;}
 public Task OnUnloadAsync(){if(host!=null)UnityEngine.Object.Destroy(host);return Task.CompletedTask;}
}
public class Guard:MonoBehaviour {
 public string Root=""; string last=""; bool original; bool captured; int saves; float next; string pauseStatus="";
 void OnEnable(){FinanceObserver.Attach(Root);original=GameManager.preventAutoSave;captured=true;Application.logMessageReceived+=Log;}
 void Log(string text,string stack,LogType type){if(text.IndexOf("saving",StringComparison.OrdinalIgnoreCase)>=0 || text.IndexOf("saved '",StringComparison.OrdinalIgnoreCase)>=0) saves++;}
 void OnDestroy(){FinanceObserver.Detach();Application.logMessageReceived-=Log;if(captured)GameManager.SetPreventAutoSave(original);}
 void Update(){
 if(pauseStatus.Length>0)try{
  var sim=FinanceObserver.Simulation();var meal=sim.State.Food.Residents.FirstOrDefault(m=>m.Status.ToString()==pauseStatus);
  if(meal!=null){UnityEngine.Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true,true);File.WriteAllText(Path.Combine(Root,"checkpoint-"+pauseStatus+".json"),JsonConvert.SerializeObject(new{meal,minute=sim.State.Minute}));pauseStatus="";}
 }catch(Exception e){Debug.LogWarning("PR2 checkpoint observer: "+e.Message);pauseStatus="";}
 if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;
 var path=Path.Combine(Root,"request.json");if(!File.Exists(path))return;var text=File.ReadAllText(path);if(text==last)return;last=text;var q=JObject.Parse(text);string id=(string)q["id"];try{
 if(!SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines"))throw new Exception("Named FT food fixture required");
 string op=(string)q["op"];object result;
 if(op=="autosave-off"){GameManager.SetPreventAutoSave(true);result=Autosave();}
 else if(op=="status")result=FinanceObserver.Status();
 else if(op=="arm-checkpoint"){pauseStatus=(string)q["status"];if(pauseStatus!="Serving" && pauseStatus!="Paid")throw new Exception("Serving or Paid checkpoint only");result=new{armed=pauseStatus};}
 else if(op=="replay-paid"){
  var sim=FinanceObserver.Simulation();var runtime=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
  var market=(NativeFoodMarket)typeof(PopulationRuntime).GetField("foodMarket",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
  var meal=sim.State.Food.Residents.First(m=>m.Status==MealStatus.Paid);var before=FinanceObserver.Snapshot();var sale=market.Buy(meal,sim.State.Minute);result=new{meal.IntentId,sale.Paid,sale.Reason,before,after=FinanceObserver.Snapshot()};
 }
 else if(op=="autosave-check")result=Autosave();
 else if(op=="finance-start")result=FinanceObserver.Start(Root,id);
 else if(op=="finance-end")result=FinanceObserver.Stop();
 else if(op=="finance-snapshot")result=FinanceObserver.Snapshot();
 else if(op=="capture"){
 var runtime=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
 var sim=(CitySimulator)typeof(PopulationRuntime).GetField("simulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
 if(sim==null)throw new Exception("Population not ready");
 var json=SaveCodec.Write(sim.State);File.WriteAllText(Path.Combine(Root,id+"-population.json"),json);
 var saved=SaveGameManager.Current.modData.ContainsKey(SaveCodec.StateKey)?SaveGameManager.Current.modData[SaveCodec.StateKey]:null;
 if(saved!=null)File.WriteAllText(Path.Combine(Root,id+"-saved-payload.json"),saved);
 result=new {schema=JObject.Parse(json)["Schema"], citizens=sim.State.Citizens.Count, minute=sim.State.Minute, nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute, economy=runtime.EconomyOverview,accounts=runtime.NativeEconomyAccounts,autosave=Autosave(),savedPayloadPresent=saved!=null};
 }
 else throw new Exception("Unknown operation");
 File.WriteAllText(Path.Combine(Root,"response-"+id+".json"),JsonConvert.SerializeObject(result,Formatting.Indented));
 }catch(Exception e){File.WriteAllText(Path.Combine(Root,"response-"+id+".json"),JsonConvert.SerializeObject(new{error=e.ToString()}));}}
 object Autosave()=>new{originalPreventAutoSave=original,preventAutoSave=GameManager.preventAutoSave, minutesBetweenAutoSaves=PlayerPrefSettings.MinutesBetweenAutoSaves,maxAutoSavesPerGame=PlayerPrefSettings.MaxAutoSavesPerGame,savingGameInProgress=SaveGameManager.SavingGameInProgress,saveLogMessages=saves,save=SaveGameManager.Current.SaveGameName,frame=Time.frameCount};
}}
