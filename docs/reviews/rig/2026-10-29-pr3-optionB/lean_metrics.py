from pathlib import Path
p=Path(__file__).parent/'OptionalObserver.cs';s=p.read_text()
s=s.replace('static bool inside;', 'static bool metricsOnly; static bool inside;')
s=s.replace('public static object Metrics()=>', '''public static object MetricsStart(){Guard();metricsOnly=true;scheduleMs.Clear();spotCalls=spotCount=occupancyCalls=occupancyBlocked=facadeDeferrals=0;return new{metricsOnly};}
 public static object MetricsStop(){metricsOnly=false;return Metrics();}
 public static object Metrics()=>''')
s=s.replace('if(!recording)return;timer=System.Diagnostics', 'if(metricsOnly){timer=System.Diagnostics.Stopwatch.GetTimestamp();lastKey="";return;}if(!recording)return;timer=System.Diagnostics')
s=s.replace('static void Scheduled(CitySimulator __instance,CitizenState person){if(!recording)return;', 'static void Scheduled(CitySimulator __instance,CitizenState person){if(metricsOnly){if(lastKey.Length>0)scheduleMs.Add((System.Diagnostics.Stopwatch.GetTimestamp()-timer)*1000.0/System.Diagnostics.Stopwatch.Frequency);return;}if(!recording)return;')
s=s.replace('static void Choice(string key,int seed,int percentage,bool __result){if(!recording)return;', 'static void Choice(string key,int seed,int percentage,bool __result){if(metricsOnly){lastKey=key;return;}if(!recording)return;')
s=s.replace('static void Occupancy(){if(recording)occupancyCalls++;}', 'static void Occupancy(bool __result){if(recording||metricsOnly){occupancyCalls++;if(__result)occupancyBlocked++;}}')
s=s.replace('name=="Occupied"?nameof(Occupancy):nameof(Attaching)))', 'name=="Occupied"?nameof(Occupancy):nameof(Attaching)))')
# Occupied needs its result, so use a postfix for that patch.
s=s.replace('if(method!=null)h.Patch(method,prefix:new HarmonyMethod(typeof(OptionalObserver),name=="Drop"?nameof(Dropping):name=="PassOnChance"?nameof(Passing):name=="Occupied"?nameof(Occupancy):nameof(Attaching)));', 'if(method!=null){if(name=="Occupied")h.Patch(method,postfix:new HarmonyMethod(typeof(OptionalObserver),nameof(Occupancy)));else h.Patch(method,prefix:new HarmonyMethod(typeof(OptionalObserver),name=="Drop"?nameof(Dropping):name=="PassOnChance"?nameof(Passing):nameof(Attaching)));}')
s=s.replace('if(!recording)return;spotCalls++;int count=Convert.ToInt32(Get(__result,"Count")??0);', 'if(!recording&&!metricsOnly)return;spotCalls++;int count=__result is ICollection collection?collection.Count:Convert.ToInt32(Get(__result,"Count")??0);')
s=s.replace('if(recording&&__result.ToString()=="Deferred")', 'if((recording||metricsOnly)&&__result.ToString()=="Deferred")')
s=s.replace('source,handedOn=hand', 'source,percentage=Get(__instance,"stopPercentage"),handedOn=hand')
p.write_text(s)
p=p.parent/'RigGuard.cs';s=p.read_text().replace('else if(op=="optional-status")', 'else if(op=="optional-metrics-start")result=OptionalObserver.MetricsStart();\n else if(op=="optional-metrics-stop")result=OptionalObserver.MetricsStop();\n else if(op=="optional-status")');p.write_text(s)
