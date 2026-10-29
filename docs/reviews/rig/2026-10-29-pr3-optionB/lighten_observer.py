from pathlib import Path
p=Path(__file__).parent/'OptionalObserver.cs';s=p.read_text();(p.parent/'OptionalObserver-v1.cs').write_text(s)
s=s.replace('static bool metricsOnly;', 'static CitySimulator observedSim; static bool metricsOnly;')
s=s.replace('static void BeforeSchedule(CitySimulator __instance,CitizenState person){', 'static void BeforeSchedule(CitySimulator __instance,CitizenState person){observedSim=__instance;')
s=s.replace('beforePending=Pending(__instance);', 'beforePending=IndexedPending(__instance,person);')
s=s.replace('pendingAfter=Pending(__instance)', 'pendingAfter=IndexedPending(__instance,person)')
s=s.replace(' static long CountField(', ''' static int IndexedPending(CitySimulator sim,CitizenState person){
  var bank=Get(sim,"bankedChances");var waiting=Get(sim,"waitingChances");if(bank==null||waiting==null)return Pending(sim);
  int count=Convert.ToInt32(Get(bank,"Count"))+Convert.ToInt32(Get(waiting,"Count"));
  bool filed=(bool)AccessTools.Method(bank.GetType(),"Contains").Invoke(bank,new object[]{person})||(bool)AccessTools.Method(waiting.GetType(),"Contains").Invoke(waiting,new object[]{person});
  bool live=Get(person,"OptionalStopChoice") is bool b&&b&&Get(person,"OptionalStop")==null;
  return count+(live?1:0)-(filed?1:0);
 }
 static long CountField(''')
# All event-path lookups use the simulator passed to the current scheduler. Status/codec and physical pause retain their separate lookups.
s=s.replace('percentage,accepted=__result,minute=FinanceObserver.Simulation().State.Minute', 'percentage,accepted=__result,minute=observedSim.State.Minute')
s=s.replace('Convert.ToInt32(Get(FinanceObserver.Simulation(),"stopPercentage"))', 'Convert.ToInt32(Get(observedSim,"stopPercentage"))')
s=s.replace('minute=FinanceObserver.Simulation().State.Minute});}catch(Exception e){faults.Add(e.Message);}}', 'minute=observedSim.State.Minute});}catch(Exception e){faults.Add(e.Message);}}')
s=s.replace('minute=FinanceObserver.Simulation().State.Minute});}\n static void Availability', 'minute=observedSim.State.Minute});}\n static void Availability')
p.write_text(s)
