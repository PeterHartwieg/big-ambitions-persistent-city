using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using Helpers;
using Helpers.BusinessSimulation;
using Buildings.Retail.Simulation;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;
[assembly: RegisterModClass(typeof(FoodDemandAuditProbe.Entry))]
namespace FoodDemandAuditProbe
{
    [ModEntryOnCityLoad] public sealed class Entry : IModBigAmbitions
    {
        private GameObject? host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        { if (Probe.Allowed()) { host = new GameObject("Food routine sales diagnostic"); host.AddComponent<Probe>().Root = context.ModRootPath; } return Task.CompletedTask; }
        public Task OnUnloadAsync() { if (host != null) Object.Destroy(host); host = null; return Task.CompletedTask; }
    }
    public sealed partial class Probe : MonoBehaviour
    {
        public string Root = "";
        private static Probe? observing;
        private CitySimulator? auditSimulation;
        private readonly JArray decisions = new JArray();
        private const int DecisionLimit = 5000, RoutineDiagnosticLimit = 100000;
        private long droppedDecisions, droppedRoutineDiagnostics;
        private void Decision(FoodDecisionTrace trace)
        {
            if (decisions.Count >= DecisionLimit) { if (droppedDecisions < long.MaxValue) droppedDecisions++; return; }
            decisions.Add(JObject.FromObject(trace));
        }
        private void DetachAuditObservers()
        {
            if (auditSimulation == null) return;
            auditSimulation.FoodDecisionObserved -= Decision;
            auditSimulation.RoutineObserved -= RoutineDiagnosticObserved;
            auditSimulation = null;
        }
        private object AuditCaptureStatus() => new {
            recording = auditSimulation != null,
            decisionsRetained = decisions.Count, decisionLimit = DecisionLimit, droppedDecisions,
            routineDiagnosticsRetained = routineDiagnostics.Count, routineDiagnosticLimit = RoutineDiagnosticLimit, droppedRoutineDiagnostics,
            truncated = droppedDecisions != 0 || droppedRoutineDiagnostics != 0,
            note = "Bounded diagnostic traces only; dropped records do not stop simulation or alter authoritative saved ledgers." };
        private int restockChecks, restocks, stockTasks;
        private const string HousekeepingPatch = "persistentcity.food-routine-test.housekeeping";
        private void ObserveHousekeeping()
        {
            if (ReferenceEquals(observing,this)) return;
            observing=this;
            var patch=new Harmony(HousekeepingPatch);
            foreach(var method in new[] { "RestockShelvesIfItsTime", "RestockShelves", "UpdateStockTasksFromFurniture" })
                patch.Patch(AccessTools.Method(typeof(RetailBusinessSimulator),method), postfix:new HarmonyMethod(typeof(Probe),nameof(Housekeeping)));
        }
        private static void Housekeeping(RetailBusinessSimulator __instance, MethodBase __originalMethod)
        {
            if(observing==null || observing.pilotShop==null || !ReferenceEquals(observing.pilotShop,
                AccessTools.Field(typeof(BusinessSimulator),"buildingRegistration").GetValue(__instance))) return;
            if(__originalMethod.Name=="RestockShelvesIfItsTime") observing.restockChecks++;
            else if(__originalMethod.Name=="RestockShelves") observing.restocks++;
            else observing.stockTasks++;
        }
        private void OnDestroy() { StopFrames(); StopClockAudit(); DetachAuditObservers(); if(ReferenceEquals(observing,this)) { new Harmony(HousekeepingPatch).UnpatchAll(HousekeepingPatch); observing=null; } }

        private readonly System.Collections.Generic.List<RoutineDiagnostic> routineDiagnostics = new System.Collections.Generic.List<RoutineDiagnostic>();
        private void RoutineDiagnosticObserved(RoutineDiagnostic trace)
        {
            if (routineDiagnostics.Count >= RoutineDiagnosticLimit) { if (droppedRoutineDiagnostics < long.MaxValue) droppedRoutineDiagnostics++; return; }
            routineDiagnostics.Add(trace);
        }
        private string last = "", pendingId = "", pauseStatus = "";
        private float poll;
        private PopulationRuntime? cached;
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Lazy<bool> allowedProcess = new Lazy<bool>(() => Environment.GetCommandLineArgs().Contains("-agent-tests")
            && (Application.persistentDataPath.Replace('\\','/').EndsWith("/Hovgaard Games/Big Ambitions Agent Tests", StringComparison.Ordinal)
                || Application.persistentDataPath.EndsWith("/com.hovgaardgames.bigambitions.agenttests.mac", StringComparison.Ordinal)));
        public static bool Allowed() => allowedProcess.Value;
        private PopulationRuntime Runtime => cached != null ? cached : cached = Object.FindObjectOfType<PopulationRuntime>() ?? throw new InvalidOperationException("Population runtime absent.");
        private CitySimulator Simulation => (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(Runtime) ?? throw new InvalidOperationException("Population not ready.");
        private static string PlaceId(BuildingRegistration r) => NativeFoodMarket.PlaceId(r);
        private void Update()
        {
            bool framesAllowed = Allowed() && SaveGameManager.Current?.SaveGameName?.StartsWith("FT Food Routines", StringComparison.Ordinal) == true;
            SampleFrameSafely(framesAllowed);
            if (!framesAllowed) return;
            try
            {
                if (pauseStatus.Length > 0 && Simulation.State.Food?.Residents.Any(m => m.Status.ToString() == pauseStatus) == true)
                {
                    Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true);
                    File.WriteAllText(Path.Combine(Root, "checkpoint-"+pauseStatus+".json"), JsonConvert.SerializeObject(Status(),Formatting.Indented));
                    pauseStatus = "";
                }
                if (Time.unscaledTime < poll) return; poll = Time.unscaledTime + .2f;
                SampleClockAudit();
                string path = Path.Combine(Root,"request.json"); if (!File.Exists(path) || new FileInfo(path).Length > 2048) return;
                string text = File.ReadAllText(path); if (text == last) return; last = text;
                pendingId = "";
                var request = JObject.Parse(text); pendingId = (string?)request["id"] ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(pendingId,"\\A[a-zA-Z0-9_-]{1,60}\\z")) { pendingId = ""; return; }
                if (DiagnosticResponses.Exists(Root, pendingId)) { pendingId = ""; return; }
                string op = (string?)request["op"] ?? "";
                RecordDiagnosticDispatch(op);
                if (op.StartsWith("operations-", StringComparison.Ordinal)) Respond(OperationsOperation(op, request));
                else if (op == "return-quote-check") Respond(ReturnQuoteCheck(request));
                else if (op == "status") Respond(Status());
                else if (op == "runtime-state")
                {
                    var runtime=Runtime;
                    object? Read(string name)=>typeof(PopulationRuntime).GetField(name,Fields)!.GetValue(runtime);
                    var task=Read("initialization") as System.Threading.Tasks.Task;
                    var discovery=Read("routeObjects");
                    object? Discovery(string name)=>discovery?.GetType().GetProperty(name)?.GetValue(discovery);
                    Respond(new {runtime.enabled,failed=Read("failed"),status=Read("status"),taskStatus=task?.Status.ToString(),
                        taskCompleted=task?.IsCompleted,taskError=task?.Exception?.ToString(),saveMatches=ReferenceEquals(Read("initializingSave"),SaveGameManager.Current),
                        discoveryReady=Discovery("Ready"),discoverySweeps=Discovery("Sweeps"),lastDiscoverySeconds=Discovery("LastSweepSeconds"),
                        lastDiscoveryWorkMs=Discovery("LastSweepMilliseconds"),maximumDiscoverySliceMs=Discovery("MaximumSliceMilliseconds"),
                        waitingRoutingFrames=Read("waitingFrames"),maximumPreparationMs=Read("maxPrepareMilliseconds")});
                }
                else if (op == "skip-needs-start") Respond(SkipNeeds(true));
                else if (op == "skip-needs-end") Respond(SkipNeeds(false));
                else if (op == "fixture-staffing") Respond(FixtureStaffing());
                else if (op == "frames-start") Respond(StartFrames());
                else if (op == "frames-status") Respond(FrameReport());
                else if (op == "frames-stop") Respond(StopAndReportFrames());
                else if (op == "clock-only-baseline") Respond(ClockOnlyBaseline());
                else if (op == "panel-screenshot") Respond(PanelScreenshot());
                else if (op == "taxi-locations") Respond(new {
                    stands=Object.FindObjectsOfType<PermanentTaxiController>().Where(t=>t.gameObject.activeInHierarchy)
                        .Select(t=>new {x=t.transform.position.x,z=t.transform.position.z}).ToArray(),
                    taxis=Object.FindObjectsOfType<TaxiController>().Where(t=>t.gameObject.activeInHierarchy)
                        .Select(t=>new {x=t.transform.position.x,z=t.transform.position.z}).ToArray() });
                else if (op == "audit-start")
                {
                    DetachAuditObservers();
                    decisions.Clear(); routineDiagnostics.Clear(); droppedDecisions = droppedRoutineDiagnostics = 0;
                    auditSimulation=Simulation; auditSimulation.FoodDecisionObserved+=Decision; auditSimulation.RoutineObserved+=RoutineDiagnosticObserved;
                    StartClockAudit();
                    File.WriteAllText(Path.Combine(Root,"audit-start-population.json"),SaveCodec.Write(Simulation.State));
                    Respond(Status());
                }
                else if (op == "audit-end")
                {
                    DetachAuditObservers(); StopClockAudit();
                    File.WriteAllText(Path.Combine(Root,"food-audit.json"),JsonConvert.SerializeObject(new {decisions, routineDiagnostics, capture=AuditCaptureStatus(), meals=Simulation.State.Food!.Residents,status=Status()},Formatting.Indented));
                    File.WriteAllText(Path.Combine(Root,"audit-end-population.json"),SaveCodec.Write(Simulation.State));
                    Respond(new {decisions=decisions.Count,capture=AuditCaptureStatus(),status=Status()});
                }
                else if (op == "setup") Respond(SetupPilot());
                else if (op == "hours-refresh-check") Respond(CheckLiveHours());
                else if (op == "menu-edit-reload") Respond(EditMenuForReload());
                else if (op == "menu-contract-check") Respond(MenuContractCheck((string?)request["shop"]));
                else if (op == "menu-catalogue") Respond(MenuCatalogue());
                else if (op == "neighborhood-candidates") Respond(NeighborhoodCandidates());
                else if (op == "neighborhood-setup") Respond(SetupNeighborhood(request));
                else if (op == "finances-enable") { Runtime.EnableHouseholdFinances(); Respond(Status()); }
                else if (op == "economy-report") Respond(new { report = Runtime.EconomyOverview, accounts = Runtime.NativeEconomyAccounts,
                    managementText = typeof(PopulationRuntime).GetMethod("BuildEconomyText", Fields)!.Invoke(Runtime, null) });
                else if (op.StartsWith("pause-",StringComparison.Ordinal)) { pauseStatus = op.Substring(6); Respond(new { waitingFor = pauseStatus }); }
                else if (op == "normal" || op == "fast-forward")
                { var speed=Object.FindObjectOfType<GameSpeedController>(); speed.isFastForwarding=op=="fast-forward";speed.Reset();Respond(new {fastForward=speed.isFastForwarding}); }
                else if (op == "correct-fixture-shift")
                {
                    var before=Status();
                    if(pilotShop==null || pilotShop.BusinessName!="Persistent City food routines") throw new InvalidOperationException("Named disposable fixture required.");
                    foreach(var day in pilotShop.scheduleDays)
                        foreach(var shift in day.workShifts) shift.type=WorkShiftType.Default;
                    Respond(new { before,after=Status(),note="Correct fixture shift enum from implicit Cleaning(0) to Default(1); preserve stock, sales and resident decisions." });
                }
                else if (op == "replay-paid")
                {
                    var meal=Simulation.State.Food!.Residents.First(m=>m.Status==MealStatus.Paid);
                    var native=(NativeFoodMarket)typeof(PopulationRuntime).GetField("foodMarket",Fields)!.GetValue(Runtime)!;
                    var before=Status();
                    var result=native.Buy(meal,Simulation.State.Minute);
                    Respond(new { paid=result.Paid,reason=result.Reason,before,after=Status() });
                }
                else if (op == "replay-meal-receipts") Respond(ReplayMealReceipts());
                else if (op == "dump")
                { File.WriteAllText(Path.Combine(Root,"population.json"),SaveCodec.Write(Simulation.State)); Respond(Status()); }
                else throw new InvalidOperationException("Unknown diagnostic operation.");
            }
            catch (Exception e) { if (pendingId.Length>0) Respond(new { error=e.ToString() }); }
        }
        private object Status()
        {
            var sim=Simulation; var state=sim.State.Food;
            var market=(NativeFoodMarket?)typeof(PopulationRuntime).GetField("foodMarket",Fields)!.GetValue(Runtime);
            if(state!=null && state.Shops.Count>0 && pilotShop==null) pilotShop=SaveGameManager.Current.BuildingRegistrations.Single(r=>PlaceId(r)==state.Shops.First().PlaceId);
            if(pilotShop!=null) ObserveHousekeeping();
            return new { minute=sim.State.Minute, nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute,
                caughtUp=sim.IsCaughtUpTo(SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute),
                clock=ClockAuditStatus(), travelClockRate=sim.State.TravelClockRate,
                routines=new { citizens=sim.State.Citizens.Count, journeys=sim.State.Citizens.Sum(c=>c.CompletedJourneys), missed=sim.State.Citizens.Sum(c=>c.MissedActivities) },
                diagnostics=Runtime.FoodDiagnostics, auditDecisions=decisions.Count, auditCapture=AuditCaptureStatus(), housekeeping=new { restockChecks,restocks,stockTasks },
                receipts=SaveGameManager.Current.modData.Where(p=>FoodSaleReceipts.IsReceiptKey(p.Key)).ToDictionary(p=>p.Key,p=>p.Value), count=state?.Residents.Count, scope=state?.Shops,
                totals=state?.Residents.GroupBy(m=>m.Status.ToString()).ToDictionary(g=>g.Key,g=>g.Count()),
                purchases=state?.Residents.Sum(m=>m.Purchases),spendingCents=state==null?null:Money.TotalCentsText(state.Residents.Select(m=>m.Spent)),
                active=state?.Residents.Where(m=>m.Status==MealStatus.Travelling||m.Status==MealStatus.Serving||m.Status==MealStatus.Paid||m.Status==MealStatus.Uncertain).ToArray(),
                outings=sim.State.Citizens.Where(c=>c.MealOuting!=null).Select(c=>new { c.Identity.Id,c.Phase,c.PlaceId,c.DestinationId,c.NextEventMinute,c.MealOuting }),
                supply=pilotShop==null?null:FoodSupplyReader.Read(pilotShop,Product), orders=pilotShop?.unprocessedCompletedOrders,
                nativeItems=pilotShop?.itemInstances.Values.Select(i=>new { i.id,i.itemName,i.parentId,i.stackedItems,i.cargoInstances,requirements=ItemHelper.GetMissingRequirements(i).Select(r=>r.GetType().FullName).ToArray() }).ToArray(),
                nativeShifts=pilotShop?.scheduleDays,
                economy=state==null?null:Runtime.EconomyOverview, accounts=state==null?null:Runtime.NativeEconomyAccounts,
                shops=state?.Shops.Select(scope=> { var shop=SaveGameManager.Current.BuildingRegistrations.Single(r=>PlaceId(r)==scope.PlaceId); return new {scope.PlaceId,shop.BusinessName,shop.customerCapacity,shop.RentPerDay,advertisedProducts=shop.cachedAvailableProducts,exclusion=market?.ScopeStatus(scope.PlaceId),supply=FoodSupplyReader.Read(shop,Product),menuSupply=FoodSupplyReader.ReadMenu(shop,scope.Products()),nativeItems=shop.itemInstances.Values.Select(i=>new { i.id,i.itemName,i.cargoInstances }).ToArray(),history=shop.orderHistory.Select(h=>new {h.dayNumber,h.totalRevenue,h.totalCustomers}).ToArray()}; }).ToArray(),
                history=pilotShop?.orderHistory.Select(h=>new { h.dayNumber,h.totalRevenue,h.totalCustomers }).ToArray() };
        }
        private void Respond(object data)
        {
            DiagnosticResponses.Publish(Root, pendingId, JsonConvert.SerializeObject(data, Formatting.Indented));
            pendingId = "";
        }
    }
}
