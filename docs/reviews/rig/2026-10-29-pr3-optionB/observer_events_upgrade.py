from pathlib import Path
p=Path(__file__).parent/'OptionalObserver.cs';s=p.read_text();(p.parent/'OptionalObserver-v2.cs').write_text(s)
s=s.replace('static bool metricsOnly;', 'static string currentArrival="",armEvent="",armedKind=""; static bool metricsOnly;')
s=s.replace('bool deferred=count==0&&doors!=null', 'bool deferred=count==0&&kind!=DwellKind.Audience&&kind!=DwellKind.Resting&&doors!=null')
s=s.replace('public static object Arm(){Guard();armed=true;return new{armed};}', '''public static object Arm(string kind=""){Guard();armedKind=kind;armed=true;return new{armed,kind};}
 public static object ArmEvent(string eventKind){Guard();if(!new[]{"hand-on","pass-on","conversation"}.Contains(eventKind))throw new Exception("Unknown observation event");armEvent=eventKind;return new{armEvent};}''')
s=s.replace('observedSim=__instance;if(metricsOnly)', 'observedSim=__instance;currentArrival=person.Identity.Id;if(metricsOnly)')
s=s.replace('var env=typeof(PopulationRuntime)', '''h.Patch(AccessTools.Method(typeof(CitySimulator),"FileOptionalStop"),prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(Filing)));
  var env=typeof(PopulationRuntime)''')
s=s.replace(' static void Dropping(', ''' static void Filing(CitySimulator __instance,CitizenState person){if(!recording||!inside||Get(person,"OptionalStop")!=null||Get(person,"OptionalStopChoice") is bool b&&b)return;try{
  var bank=Get(__instance,"bankedChances");var waiting=Get(__instance,"waitingChances");if(bank==null||waiting==null)return;
  if((bool)AccessTools.Method(bank.GetType(),"Contains").Invoke(bank,new object[]{person})||(bool)AccessTools.Method(waiting.GetType(),"Contains").Invoke(waiting,new object[]{person}))Write("pending-cleared",new{id=person.Identity.Id,arriving=currentArrival,key=lastKey,minute=__instance.State.Minute});
 }catch(Exception e){faults.Add(e.ToString());}}
 static void Dropping(''')
s=s.replace('recipient=first?.Identity.Id,available=first!=null', 'donor=currentArrival,recipient=first?.Identity.Id,available=first!=null')
s=s.replace(' }catch(Exception e){faults.Add(e.ToString());inside=false;}}', '''
  bool paired=stop!=null&&((IEnumerable<string>)Get(stop,"Members")).Count()==2;
  if(armEvent=="hand-on"&&hand>0||armEvent=="pass-on"&&pass>0||armEvent=="conversation"&&paired){
   string observed=armEvent;armEvent="";UnityEngine.Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true,true);
   File.WriteAllText(Path.Combine(root,"optional-event-checkpoint.json"),JsonConvert.SerializeObject(new{observed,id=person.Identity.Id,key=lastKey,minute=__instance.State.Minute,hand,pass,stop,pending=IndexedPending(__instance,person),tally}));
  }
 }catch(Exception e){faults.Add(e.ToString());inside=false;}}''')
s=s.replace('if(armed && phase=="dwell")', 'if(armed && phase=="dwell"&&(armedKind.Length==0||Get(stop,"Kind").ToString()==armedKind))')
p.write_text(s)
p=p.parent/'RigGuard.cs';s=p.read_text().replace('else if(op=="arm-optional-dwell")', 'else if(op=="arm-optional-event")result=OptionalObserver.ArmEvent((string)q["event"]);\n else if(op=="arm-optional-dwell-kind")result=OptionalObserver.Arm((string)q["kind"]);\n else if(op=="arm-optional-dwell")');(p.parent/'RigGuard-v2.cs').write_text(p.read_text());p.write_text(s)
