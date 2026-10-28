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
[assembly: RegisterModClass(typeof(FoodRoutineSalesProbe.Entry))]
namespace FoodRoutineSalesProbe
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
        private void OnDestroy() { if(ReferenceEquals(observing,this)) { new Harmony(HousekeepingPatch).UnpatchAll(HousekeepingPatch); observing=null; } }

        private string last = "", pendingId = "", pauseStatus = "";
        private float poll;
        private PopulationRuntime? cached;
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Allowed() => Environment.GetCommandLineArgs().Contains("-agent-tests")
            && Application.persistentDataPath.Replace('\\','/').EndsWith("/Hovgaard Games/Big Ambitions Agent Tests", StringComparison.Ordinal);
        private PopulationRuntime Runtime => cached != null ? cached : cached = Object.FindObjectOfType<PopulationRuntime>() ?? throw new InvalidOperationException("Population runtime absent.");
        private CitySimulator Simulation => (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(Runtime) ?? throw new InvalidOperationException("Population not ready.");
        private static string PlaceId(BuildingRegistration r) => NativeFoodMarket.PlaceId(r);
        private void Update()
        {
            if (!Allowed() || SaveGameManager.Current?.SaveGameName?.StartsWith("FT Food Routines", StringComparison.Ordinal) != true) return;
            try
            {
                if (pauseStatus.Length > 0 && Simulation.State.Food?.Residents.Any(m => m.Status.ToString() == pauseStatus) == true)
                {
                    Object.FindObjectOfType<GameSpeedController>().SetPlayerPause(true);
                    File.WriteAllText(Path.Combine(Root, "checkpoint-"+pauseStatus+".json"), JsonConvert.SerializeObject(Status(),Formatting.Indented));
                    pauseStatus = "";
                }
                if (Time.unscaledTime < poll) return; poll = Time.unscaledTime + .2f;
                string path = Path.Combine(Root,"request.json"); if (!File.Exists(path) || new FileInfo(path).Length > 2048) return;
                string text = File.ReadAllText(path); if (text == last) return; last = text;
                var request = JObject.Parse(text); pendingId = (string?)request["id"] ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(pendingId,"^[a-zA-Z0-9_-]{1,60}$")) return;
                string op = (string?)request["op"] ?? "";
                if (op == "status") Respond(Status());
                else if (op == "setup") Respond(SetupPilot());
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
                else if (op == "dump")
                { File.WriteAllText(Path.Combine(Root,"population.json"),SaveCodec.Write(Simulation.State)); Respond(Status()); }
                else throw new InvalidOperationException("Unknown diagnostic operation.");
            }
            catch (Exception e) { if (pendingId.Length>0) Respond(new { error=e.ToString() }); }
        }
        private object Status()
        {
            var sim=Simulation; var state=sim.State.Food;
            if(state!=null && pilotShop==null) pilotShop=SaveGameManager.Current.BuildingRegistrations.Single(r=>PlaceId(r)==state.Shops.Single().PlaceId);
            if(pilotShop!=null) ObserveHousekeeping();
            return new { minute=sim.State.Minute, nativeMinute=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute,
                diagnostics=Runtime.FoodDiagnostics, housekeeping=new { restockChecks,restocks,stockTasks },
                receipts=SaveGameManager.Current.modData.Where(p=>p.Key.StartsWith("persistentcity:food-sale-v1:",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value), count=state?.Residents.Count, scope=state?.Shops,
                totals=state?.Residents.GroupBy(m=>m.Status.ToString()).ToDictionary(g=>g.Key,g=>g.Count()),
                purchases=state?.Residents.Sum(m=>m.Purchases),spendingCents=state==null?null:Money.Sum(state.Residents.Select(m=>m.Spent)).ToCentsText(),
                active=state?.Residents.Where(m=>m.Status==MealStatus.Travelling||m.Status==MealStatus.Serving||m.Status==MealStatus.Paid||m.Status==MealStatus.Uncertain).ToArray(),
                outings=sim.State.Citizens.Where(c=>c.MealOuting!=null).Select(c=>new { c.Identity.Id,c.Phase,c.PlaceId,c.DestinationId,c.NextEventMinute,c.MealOuting }),
                supply=pilotShop==null?null:FoodSupplyReader.Read(pilotShop,Product), orders=pilotShop?.unprocessedCompletedOrders,
                nativeItems=pilotShop?.itemInstances.Values.Select(i=>new { i.id,i.itemName,i.parentId,i.stackedItems,i.cargoInstances,requirements=ItemHelper.GetMissingRequirements(i).Select(r=>r.GetType().FullName).ToArray() }).ToArray(),
                nativeShifts=pilotShop?.scheduleDays,
                history=pilotShop?.orderHistory.Select(h=>new { h.dayNumber,h.totalRevenue,h.totalCustomers }).ToArray() };
        }
        private void Respond(object data)
        { File.WriteAllText(Path.Combine(Root,"response-"+pendingId+".json"),JsonConvert.SerializeObject(data,Formatting.Indented));pendingId=""; }
    }
}
