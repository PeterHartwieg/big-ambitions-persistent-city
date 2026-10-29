from pathlib import Path
p=Path(__file__).parent/'OptionalObserver.cs'
s=p.read_text()
s=s.replace('static void Scheduled(CitizenState person){', 'static void OldScheduled(CitizenState person){')
s=s.replace('postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Scheduled)))','prefix:new HarmonyMethod(typeof(OptionalObserver),nameof(BeforeSchedule)),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Scheduled)))')
s=s.replace('var p=typeof(PopulationRuntime)', '''foreach(var name in new[]{"Drop","PassOnChance","Occupied","AttachOptionalStops"}){var method=AccessTools.Method(typeof(CitySimulator),name);if(method!=null)h.Patch(method,prefix:new HarmonyMethod(typeof(OptionalObserver),name=="Drop"?nameof(Dropping):name=="PassOnChance"?nameof(Passing):name=="Occupied"?nameof(Occupancy):nameof(Attaching)));}
  var env=typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.OptionalStopEnvironment");
  if(env!=null){h.Patch(AccessTools.Method(env,"Spots"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(SpotsRead)));h.Patch(AccessTools.Method(env,"Available"),postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Availability)));}
  var p=typeof(PopulationRuntime)''')
s=s.replace('public static object Status(){Guard();', 'public static object Status(){Guard();')
s=s.replace('return new{featurePresent=', 'return new{audit=Audit(),metrics=Metrics(),featurePresent=')
s=s.replace('phases.Clear();faults.Clear();output=', 'phases.Clear();faults.Clear();seen.Clear();admissions.Clear();spotCalls=occupancyCalls=occupancyBlocked=facadeDeferrals=spotCount=0;scheduleMs.Clear();output=')
insert='''
 static readonly HashSet<string> seen=new HashSet<string>();
 static readonly Dictionary<string,string> admissions=new Dictionary<string,string>();
 static readonly List<double> scheduleMs=new List<double>();
 static long spotCalls,occupancyCalls,occupancyBlocked,facadeDeferrals,spotCount;
 static bool inside;static bool lastDraw;static string lastKey="";static string source="";static long beforeDrops,beforePass,beforeHand;static int beforePending;static long timer;
 static long CountField(object o,string n)=>Convert.ToInt64(Get(o,n)??0L);
 static int Pending(CitySimulator sim)=>sim.State.Citizens.Count(c=>Get(c,"OptionalStopChoice") is bool b&&b&&Get(c,"OptionalStop")==null);
 public static object Audit(){var sim=FinanceObserver.Simulation();return new{pending=Pending(sim),tally=Get(sim,"OptionalStopChances"),minute=sim.State.Minute};}
 public static object Metrics()=>new{scheduleCalls=scheduleMs.Count,scheduleMilliseconds=scheduleMs.ToArray(),spotCalls,spotCount,occupancyCalls,occupancyBlocked,facadeDeferrals};
 static void BeforeSchedule(CitySimulator __instance,CitizenState person){if(!recording)return;timer=System.Diagnostics.Stopwatch.GetTimestamp();inside=true;lastKey="";lastDraw=false;source=Get(person,"OptionalStopChoice") is bool b&&b?"carried":"direct";var t=Get(__instance,"OptionalStopChances");beforeDrops=CountField(t,"Dropped");beforePass=CountField(t,"PassedOn");beforeHand=CountField(t,"HandedOn");beforePending=Pending(__instance);}
 static void Scheduled(CitySimulator __instance,CitizenState person){if(!recording)return;try{
  double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-timer)*1000.0/System.Diagnostics.Stopwatch.Frequency;inside=false;
  if(lastKey.Length==0)return;scheduleMs.Add(ms);var tally=Get(__instance,"OptionalStopChances");long hand=CountField(tally,"HandedOn")-beforeHand;long pass=CountField(tally,"PassedOn")-beforePass;long drops=CountField(tally,"Dropped")-beforeDrops;
  var stop=Get(person,"OptionalStop");var recipients=new List<object>();
  if(stop!=null){var ids=new List<string>{person.Identity.Id};var members=Get(stop,"Members") as IEnumerable<string>;if(members!=null)ids.AddRange(members);
   foreach(string id in ids.Distinct()){var c=__instance.State.Citizens.First(x=>x.Identity.Id==id);var st=Get(c,"OptionalStop");if(st==null)continue;string key=id+":"+Get(st,"PlaceId")+":"+Convert.ToDouble(Get(st,"VisitMinute")).ToString("R",System.Globalization.CultureInfo.InvariantCulture);if(!seen.Add(key))continue;
    var row=new{id,key,minute=__instance.State.Minute,place=c.PlaceId,activity=c.Activity.ToString(),source=id!=person.Identity.Id?"conversation-partner":hand>0?"handed-on":source,stop=st};recipients.Add(row);Write("scheduled",row);
   }
  }
  Write("admission",new{id=person.Identity.Id,key=lastKey,drawn=lastDraw,minute=__instance.State.Minute,place=person.PlaceId,source,handedOn=hand,passedOn=pass,dropped=drops,pendingBefore=beforePending,pendingAfter=Pending(__instance),recipients=recipients.Count,milliseconds=ms,tally});
 }catch(Exception e){faults.Add(e.ToString());inside=false;}}
 static void Dropping(CitizenState holder){if(!recording)return;try{Write("drop",new{id=holder.Identity.Id,reason=CountField(Get(FinanceObserver.Simulation(),"stopPercentage"),"unused")==-1?"unknown":Convert.ToInt32(Get(FinanceObserver.Simulation(),"stopPercentage"))==100?"100-policy-trim":"trim",minute=FinanceObserver.Simulation().State.Minute});}catch(Exception e){faults.Add(e.Message);}}
 static void Passing(CitySimulator __instance){if(!recording)return;try{var free=Get(__instance,"freeOfChances");var first=Get(free,"Min") as CitizenState;Write("pass-attempt",new{recipient=first?.Identity.Id,available=first!=null,minute=__instance.State.Minute});}catch(Exception e){faults.Add(e.Message);}}
 static void Occupancy(){if(recording)occupancyCalls++;}
 static void Attaching(){if(recording)Write("attach-reset",Audit());}
 static void SpotsRead(object __instance,Place place,DwellKind kind,object __result){if(!recording)return;spotCalls++;int count=Convert.ToInt32(Get(__result,"Count")??0);spotCount+=count;var doors=Get(__instance,"doors");bool deferred=count==0&&doors!=null&&(bool)(AccessTools.Method(doors.GetType(),"Unsampled").Invoke(doors,new object[]{place}))&&(bool)Get(doors,"SampledThisFrame");if(deferred)facadeDeferrals++;if(inside)Write("spots",new{place=place.Id,kind=kind.ToString(),count,deferred,minute=FinanceObserver.Simulation().State.Minute});}
 static void Availability(object __result){if(recording&&__result.ToString()=="Deferred")facadeDeferrals++;}
'''
s=s.replace(' static void InjectFixture()',insert+'\n static void InjectFixture()')
s=s.replace('Guard();Write("choice",', 'Guard();lastKey=key;lastDraw=__result;Write("choice",')
s=s.replace('reason=CountField(Get(FinanceObserver.Simulation(),"stopPercentage"),"unused")==-1?"unknown":Convert.ToInt32', 'reason=Convert.ToInt32')
p.write_text(s)
