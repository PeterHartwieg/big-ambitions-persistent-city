using System.Reflection;
using AI.Customers.CustomerEntries;
using Buildings.Retail.Simulation;
using Helpers.BusinessSimulation;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

int checks = 0;
await new FoodAuthorityBootstrap().OnLoadAsync(new BAModAPI.ModContext());
Check(HarmonyLib.Harmony.Patched.Count == 6, "Initialization installs passive hooks before any city market exists");
var reg = Fresh();
var market = new NativeFoodMarket(reg);
NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
BusinessSimulatorHelper.Queue(reg, 7);
GameManager.RunMainGameTick(60);
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 7, 8 }), "old native work drains before acquisition");
Check(NativeFoodOwnership.Find(reg)!.FromHour == 33, "authority begins exactly at the later native accounting hour");
BusinessSimulatorHelper.Work.ForceCompleteAllWork();
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 7, 8 }), "new hour native demand suppressed");
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "owned hour suppresses indoor draws");
Check(reg.CompletedOrders.Count == 2, "old completed native orders are retained at acquisition");
BusinessSimulatorHelper.Queue(reg, 9); BusinessSimulatorHelper.Work.ForceCompleteAllWork();
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 7, 8 }), "newly owned hour cannot execute a native sale");

// A saved guard restores synchronously, before any market or population preparation.
string saved = SaveGameManager.Current.modData[FoodAuthorityStore.Key];
NativeFoodOwnership.Detach(market);
reg = Fresh(9, saved);
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "first demand restores saved authority without attached market");
CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, 1);
Check(CustomerEntriesHelper.Generated.Count == 0, "early saved-hour generation blocked");
var old = new RetailBusinessSimulator(); old.SetUp(reg, 8);
Check((bool)Hooks.Call("HourlyCustomers", old)!, "actual old simulator accounting hour stays native even when global clock is owned");
var current = new RetailBusinessSimulator(); current.SetUp(reg, 9);
Check(!(bool)Hooks.Call("HourlyCustomers", current)!, "actual owned simulator hour suppressed");

market = new NativeFoodMarket(reg); NativeFoodOwnership.Attach(market);
NativeFoodOwnership.Observe(reg, false); market.Eligible = false;
Check(!NativeFoodOwnership.Find(reg)!.CanServe, "unsupported menu freezes mod immediately");
GameManager.RunMainGameTick(60);
Check(CustomerEntriesHelper.Generated.All(h => h >= 10) && CustomerEntriesHelper.Generated.Count == 14, "release regenerates only current/future native hours");
Check(!NativeFoodOwnership.Find(reg)!.OwnsHour(34), "released new hour is native");
Check(NativeFoodOwnership.Find(reg)!.OwnsHour(33), "released old accounting hour stays owned");

reg = Fresh(9, saved);
reg.Signature = "business:Test:7:1:changed-type:player";
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0,
    "changed saved native signature cannot replay protected address/hour during startup");
Check(NativeFoodOwnership.Find(reg) == null, "changed signature does not authorize mod checkout under the old business");
NativeFoodOwnership.Observe(reg, true);
Check(NativeFoodOwnership.Find(reg)!.Suspended && !NativeFoodOwnership.Find(reg)!.CanServe,
    "startup signature mismatch resolves to suspended replacement rather than immediate new service");

reg = Fresh(); market = new NativeFoodMarket(reg);
NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
BuildingManager.Instance.buildingRegistration = reg; IndoorCustomerSpawner.Customers.Add(new object());
GameManager.RunMainGameTick(60);
Check(!NativeFoodOwnership.Find(reg)!.Managed, "native basket body defers acquisition");
Check(BusinessSimulatorHelper.Work.Pending.Count == 1, "body-deferred acquisition leaves ending-hour native work distributed");
BusinessSimulatorHelper.Work.ForceCompleteAllWork();
Check(reg.CompletedOrders.Count == 1, "native hour runs while acquisition deferred");
IndoorCustomerSpawner.Customers.Clear(); GameManager.RunMainGameTick(0);
Check(!NativeFoodOwnership.Find(reg)!.Managed, "repeated same-hour callback cannot acquire an already native hour");
GameManager.RunMainGameTick(60);
Check(NativeFoodOwnership.Find(reg)!.Managed, "later drained boundary acquires without deleting customers/baskets");
Check(reg.CompletedOrders.Count >= 1, "handoff preserves native accounting queue");

string identity = NativeFoodOwnership.Find(reg)!.BusinessId;
int beforeResetOrders = reg.CompletedOrders.Count;
BusinessSimulatorHelper.Queue(reg, SaveGameManager.Current.Hour);
reg.Reset();
Check(BusinessSimulatorHelper.Work.Pending.Count == 0 && reg.CompletedOrders.Count == beforeResetOrders,
    "reset drains queued owned work under old authority without creating an old native order");
Check(NativeFoodOwnership.Find(reg)!.BusinessId != identity && !NativeFoodOwnership.Find(reg)!.CanServe, "same-day same-object reset changes generation and stops sales");
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "same-hour reset does not expose stale native entries for the replacement");
NativeFoodOwnership.Observe(reg, true);
string resetIdentity = NativeFoodOwnership.Find(reg)!.BusinessId;
var replacement = new BuildingRegistration();
BusinessSimulatorHelper.Queue(reg, SaveGameManager.Current.Hour);
NativeFoodOwnership.Observe(replacement, true);
Check(BusinessSimulatorHelper.Work.Pending.Count == 0, "replacement drains previous generation work before changing its identity");
Check(NativeFoodOwnership.Find(replacement)!.BusinessId != resetIdentity, "same-address new object replacement changes generation");
reg = Fresh(11, SaveGameManager.Current.modData[FoodAuthorityStore.Key]);
market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(0);
Check(!NativeFoodOwnership.Find(reg)!.CanServe, "reloaded pending takeover cannot replay saved current native hour");
GameManager.RunMainGameTick(60);
Check(NativeFoodOwnership.Find(reg)!.CanServe, "reloaded pending takeover completes at next boundary");

reg = Fresh(8, "{\"version\":99,\"shops\":[]}");
string unsupported = SaveGameManager.Current.modData[FoodAuthorityStore.Key];
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "unreadable authority suspends native sales rather than choosing another owner");
Check(NativeFoodOwnership.Error.Length > 0, "invalid authority produces visible error");
Check(SaveGameManager.Current.modData[FoodAuthorityStore.Key] == unsupported, "unsupported manifest never overwritten");

reg = Fresh(14, ownPayload: true);
Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "missing manifest blocks first demand before async population validation");
CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, 1);
Check(CustomerEntriesHelper.Generated.Count == 0, "missing-manifest provisional guard blocks native generation");
try
{
    NativeFoodOwnership.ValidatePopulation(new FoodCommerceState { Shops = new() { new() { PlaceId = NativeFoodMarket.PlaceId(reg) } } });
    throw new InvalidOperationException("Owned scopes without authority must refuse");
}
catch (InvalidOperationException e) when (e.Message.Contains("manifest is missing")) { checks++; }
Check(!SaveGameManager.Current.modData.ContainsKey(FoodAuthorityStore.Key)
    && CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "missing authority remains unmodified and suspended for owned scopes");
reg = Fresh(14, ownPayload: true);
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState());
Check(CustomerEntriesHelper.Generated.SequenceEqual(Enumerable.Range(14, 10)), "validated zero-scope city releases only present/future native hours");
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState());
Check(CustomerEntriesHelper.Generated.Count == 10, "provisional native generation release occurs once");
Check(FoodAuthorityStore.Read(SaveGameManager.Current.modData[FoodAuthorityStore.Key]).Count == 0,
    "supported empty-scope city receives an empty authority manifest before its next population save");
// Independent-review regressions: owner loss cannot re-run an already managed batch.
reg = Fresh(9, saved); market = new NativeFoodMarket(reg) { Eligible = false }; NativeFoodOwnership.Attach(market);
BusinessSimulatorHelper.Queue(reg, 9);
reg.RentedByPlayer = false; reg.Signature = "business:Test:7:1:bakery:rival";
NativeFoodOwnership.Observe(reg, false);
Check(BusinessSimulatorHelper.Processed.Count == 0 && reg.CompletedOrders.Count == 0,
    "sale drains previously managed hour without any native sale or rival finance write");
Check(!NativeFoodOwnership.Find(reg)!.CanServe, "sold shop cannot receive mod service");
GameManager.RunMainGameTick(60);
BusinessSimulatorHelper.Queue(reg, 10); BusinessSimulatorHelper.Work.ForceCompleteAllWork();
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 10 }), "future rival hour is native after the historical protected interval");

reg = Fresh(9, saved); _ = NativeFoodOwnership.Find(reg);
replacement = new BuildingRegistration(); current = new RetailBusinessSimulator(); current.SetUp(replacement, 9);
Check(!(bool)Hooks.Call("HourlyCustomers", current)!, "replacement object cannot run protected current hour before bounded observation");
Check(NativeFoodOwnership.Find(replacement) == null, "replacement remains ineligible for mod service until generation observation");

// Review round 1: every supported receipt version proves earlier accounting ownership,
// even when the authority manifest is missing or empty.
foreach (string prefix in new[] { FoodSaleReceipts.LegacyPrefix, FoodSaleReceipts.Prefix })
foreach (string state in new[] { "paid", "pending" })
foreach (bool emptyManifest in new[] { false, true })
{
    string receiptKey = prefix + "food:resident:lunch:1";
    string receiptValue = state + "|1025|business|product";
    string? manifest = emptyManifest ? FoodAuthorityStore.Write(Array.Empty<FoodAuthority>()) : null;
    reg = Fresh(8, manifest, ownPayload: false, receiptKey: receiptKey, receiptValue: receiptValue);
    Check(NativeFoodOwnership.Error.Contains("without the population"), "orphaned " + prefix + state + " suspends startup");
    var originalData = SaveGameManager.Current.modData.OrderBy(p => p.Key).ToArray();
    Check(originalData.Length == (emptyManifest ? 2 : 1) && SaveGameManager.Current.modData[receiptKey] == receiptValue,
        "orphaned receipt and manifest retained by initial guard without new state");
    try { NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); throw new Exception("Replacement wallets must refuse"); }
    catch (InvalidOperationException) { checks++; }
    Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "orphaned receipt blocks native demand while suspended");
    Check(originalData.SequenceEqual(SaveGameManager.Current.modData.OrderBy(p => p.Key)), "partial save bytes remain untouched after refusal");
}

reg = Fresh(9, saved, ownPayload: false);
Check(NativeFoodOwnership.Error.Contains("without the population"), "owned manifest without population is rejected before fresh city creation");
try { NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); throw new Exception("Replacement wallets must refuse"); }
catch (InvalidOperationException) { checks++; }
Check(SaveGameManager.Current.modData[FoodAuthorityStore.Key] == saved && !SaveGameManager.Current.modData.ContainsKey(SaveStore.StateKey),
    "partial owned save is preserved without replacement population");
var retired = FoodAuthorityStore.Read(saved)[0]; retired.Observe(retired.Signature, false, 34); retired.Boundary(35, true, false, true);
retired.Observe("business:Test:7:2:bakery:player", false, 36);
reg = Fresh(12, FoodAuthorityStore.Write(new[] { retired }), ownPayload: false);
Check(NativeFoodOwnership.Error.Contains("without the population"), "retired then replaced authority still proves earlier owned wallets existed");
reg = Fresh(8, FoodAuthorityStore.Write(Array.Empty<FoodAuthority>()), ownPayload: false);
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); Check(NativeFoodOwnership.Error.Length == 0, "never-owned empty manifest allows actual first population initialization");
var pending = new FoodAuthority { PlaceId = NativeFoodMarket.PlaceId(reg) }; pending.Observe(reg.Signature, true, 32);
reg = Fresh(8, FoodAuthorityStore.Write(new[] { pending }), ownPayload: false);
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); Check(NativeFoodOwnership.Error.Length == 0, "never-acquired candidate manifest allows first population initialization");

reg = Fresh(9, saved); var activeRow = FoodAuthorityStore.Read(saved)[0];
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState { Shops = new() { new() { PlaceId = activeRow.PlaceId, BusinessId = activeRow.BusinessId } } });
Check(NativeFoodOwnership.Error.Length == 0, "matching active scope and authority validate");
reg = Fresh(9, saved);
try { NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); throw new Exception("Missing active scope must refuse"); }
catch (InvalidOperationException) { checks++; }
reg = Fresh(12, FoodAuthorityStore.Write(new[] { retired }));
NativeFoodOwnership.ValidatePopulation(new FoodCommerceState { Shops = new() { new() { PlaceId = retired.PlaceId, BusinessId = activeRow.BusinessId } } });
Check(NativeFoodOwnership.Error.Length == 0, "earlier same-address scope remains valid after legitimate release and replacement");
reg = Fresh(12, FoodAuthorityStore.Write(new[] { retired }));
try
{
    NativeFoodOwnership.ValidatePopulation(new FoodCommerceState { Shops = new() { new() { PlaceId = retired.PlaceId, BusinessId = "business:Foreign:99:1:bakery:player:generation:1" } } });
    throw new Exception("Foreign historical scope must refuse");
}
catch (InvalidOperationException) { checks++; }
// Real native call-order cases: RunHourly sees the ending hour, not the next hour.
reg = Fresh(1); SaveGameManager.Current.Day = 2;
market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(61);
Check(SaveGameManager.Current.Hour == 2 && NativeFoodOwnership.Find(reg)!.FromHour == 50,
    "observed K1 hour49 request acquires hour50 after native ending-hour processing");
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 1 }), "hour1 was native before hour2 acquisition, not retroactively suppressed");

reg = Fresh(23); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
BusinessSimulatorHelper.Queue(reg, 22); GameManager.RunMainGameTick(60);
Check(SaveGameManager.Current.Day == 2 && SaveGameManager.Current.Hour == 0 && NativeFoodOwnership.Find(reg)!.FromHour == 48,
    "midnight acquisition uses advanced absolute day/hour");
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 22, 23 }) && GameManager.DailySettlementOrderCounts.SequenceEqual(new[] { 2 }),
    "native23 force-drain and daily settlement complete under old ownership before midnight acquisition");
Check(!NativeFoodOwnership.Find(reg)!.OwnsHour(47) && NativeFoodOwnership.Find(reg)!.OwnsHour(48), "midnight never claims the ending23 hour");

reg = Fresh(22); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(60); market.Eligible = false; NativeFoodOwnership.Observe(reg, false);
GameManager.RunMainGameTick(60);
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 22 }) && GameManager.DailySettlementOrderCounts.SequenceEqual(new[] { 1 }),
    "midnight release does not replay owned23 before daily accounting");
Check(CustomerEntriesHelper.Generated.SequenceEqual(Enumerable.Range(0, 24)), "midnight release rebuilds new-day0 onward, not prior-day23");
Check(NativeFoodOwnership.Find(reg)!.UntilHour == 48, "midnight release ends authority at the new day");

reg = Fresh(); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(185);
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 8, 9, 10 }) && NativeFoodOwnership.Find(reg)!.FromHour == 35,
    "multi-hour native tick completes all ending hours and acquires only its final new hour");
Check(!NativeFoodOwnership.Find(reg)!.OwnsHour(33) && !NativeFoodOwnership.Find(reg)!.OwnsHour(34), "multi-hour catch-up cannot retrospectively claim intermediate native hours");
reg = Fresh(); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
BusinessSimulatorHelper.TimeMachineRunning = true; GameManager.RunMainGameTick(185);
Check(BusinessSimulatorHelper.Processed.SequenceEqual(new[] { 8, 9, 10 }) && NativeFoodOwnership.Find(reg)!.FromHour == 35,
    "time-machine force-completed ending batches remain native before final-hour acquisition");

reg = Fresh(9, saved); market = new NativeFoodMarket(reg) { Eligible = false }; NativeFoodOwnership.Attach(market); NativeFoodOwnership.Observe(reg, false);
GameManager.RunMainGameTick(125);
Check(BusinessSimulatorHelper.Processed.Count == 0 && CustomerEntriesHelper.Generated.SequenceEqual(Enumerable.Range(11, 13)),
    "multi-hour release retains old ownership through ending batches and generates only final current/future hours");

reg = Fresh(); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.DuringNewHour = () => { BuildingManager.Instance.buildingRegistration = reg; IndoorCustomerSpawner.Customers.Add(new object()); };
GameManager.RunMainGameTick(60);
Check(!NativeFoodOwnership.Find(reg)!.Managed, "native hourly callback bodies created before postfix prevent acquisition");
GameManager.DuringNewHour = null; IndoorCustomerSpawner.Customers.Clear(); GameManager.RunMainGameTick(0);
Check(!NativeFoodOwnership.Find(reg)!.Managed, "post-callback body drain does not acquire an already native current hour");
GameManager.RunMainGameTick(60); Check(NativeFoodOwnership.Find(reg)!.FromHour == 34, "body-delayed shop acquires the following complete new hour");

reg = Fresh(9, saved); market = new NativeFoodMarket(reg); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(60);
Check(BusinessSimulatorHelper.Work.Pending.Count == 1 && NativeFoodOwnership.Find(reg)!.CanServe,
    "stable owned boundary preserves native distributed work without changing service authority");
BusinessSimulatorHelper.Work.ForceCompleteAllWork();
Check(reg.CompletedOrders.Count == 0, "normally distributed owned-hour work still suppresses native sales");
GameManager.RunMainGameTick(125);
Check(BusinessSimulatorHelper.Work.Pending.Count == 1 && reg.CompletedOrders.Count == 0,
    "stable multi-hour tick retains its final distributed batch and protects intermediate owned hours");
market.Eligible = false; NativeFoodOwnership.Observe(reg, false);
GameManager.RunMainGameTick(60);
Check(BusinessSimulatorHelper.Work.Pending.Count == 0 && !NativeFoodOwnership.Find(reg)!.Managed && reg.CompletedOrders.Count == 0,
    "release drains the last managed hour before returning future hours to native authority");

reg = Fresh(); market = new NativeFoodMarket(reg) { Eligible = false };
NativeFoodOwnership.Observe(reg, false); NativeFoodOwnership.Attach(market);
GameManager.RunMainGameTick(60);
Check(BusinessSimulatorHelper.Work.Pending.Count == 1 && !NativeFoodOwnership.Find(reg)!.Managed,
    "stable unsupported business retains normal distributed native processing");

reg = Fresh(); market = new NativeFoodMarket(reg); NativeFoodOwnership.Observe(reg, true); NativeFoodOwnership.Attach(market);
GameManager.DuringNewHour = () => BusinessSimulatorHelper.Work.Pending.Enqueue(() =>
{ BuildingManager.Instance.buildingRegistration = reg; IndoorCustomerSpawner.Customers.Add(new object()); });
GameManager.RunMainGameTick(60);
Check(!NativeFoodOwnership.Find(reg)!.Managed && BusinessSimulatorHelper.Work.Pending.Count == 0,
    "customers introduced during required drain are rechecked before acquisition");
GameManager.DuringNewHour = null; IndoorCustomerSpawner.Customers.Clear(); GameManager.RunMainGameTick(0);
Check(!NativeFoodOwnership.Find(reg)!.Managed, "drain-time body deferral still cannot acquire midway through the same hour");

// B0 executes the actual earliest native hook, before attaching any population or market.
foreach (string damage in new[] { "revision", "code", "future" })
{
    string supported = PopulationPayload(9);
    string unsupportedPopulation = damage == "revision" ? supported.Replace("\"Revision\":1", "\"Revision\":2")
        : damage == "code" ? supported.Replace("\"Code\":\"B\"", "\"Code\":\"C\"")
        : supported.Replace("\"Schema\":18", "\"Schema\":999");
    reg = Fresh(9, saved, populationPayload: unsupportedPopulation);
    Check(NativeFoodOwnership.Error.Length > 0, "unsupported " + damage + " capability stops earliest native authority bootstrap");
    Check(CustomerEntriesHelper.GetEntriesByAddress(reg.Address).Count == 0, "unsupported capability cannot release native demand");
    bool blocked = false; try { NativeFoodOwnership.ValidatePopulation(new FoodCommerceState()); } catch (InvalidOperationException) { blocked = true; }
    Check(blocked, "population startup cannot clear unsupported-capability refusal");
    Check(ReferenceEquals(unsupportedPopulation, SaveGameManager.Current.modData[SaveStore.StateKey]), "capability refusal preserves exact population payload reference");
    Check(ReferenceEquals(saved, SaveGameManager.Current.modData[FoodAuthorityStore.Key]), "capability refusal preserves authority manifest");
}

// B1 explicitly supports active B revision 1 at the same earliest bootstrap hook.
string activeB = PopulationPayload(9).Replace("\"Active\":false", "\"Active\":true");
reg = Fresh(9, saved, populationPayload: activeB);
Check(NativeFoodOwnership.Error.Length == 0, "active B revision 1 executes at earliest native bootstrap");
Check(ReferenceEquals(activeB, SaveGameManager.Current.modData[SaveStore.StateKey]), "bootstrap does not rewrite active population");

Console.WriteLine($"PASS {checks} actual ownership-hook checks against native field/callback doubles. No Unity/game execution.");

BuildingRegistration Fresh(int hour = 8, string? payload = null, bool? ownPayload = null, string? receiptKey = null, string? receiptValue = null, string? populationPayload = null)
{
    var shop = new BuildingRegistration();
    SaveGameManager.Current = new Save { Hour = hour, BuildingRegistrations = new() { shop } };
    if (payload != null) SaveGameManager.Current.modData[FoodAuthorityStore.Key] = payload;
    if (ownPayload ?? payload != null) SaveGameManager.Current.modData[SaveStore.StateKey] = populationPayload ?? PopulationPayload(hour);
    BusinessSimulatorHelper.Work.Pending.Clear(); BusinessSimulatorHelper.Processed.Clear();
    BusinessSimulatorHelper.TimeMachineRunning = false;
    CustomerEntriesHelper.Generated.Clear(); IndoorCustomerSpawner.Customers.Clear();
    BuildingManager.Instance.buildingRegistration = null; GameManager.DuringNewHour = null; GameManager.DailySettlementOrderCounts.Clear();
    if (receiptKey != null) SaveGameManager.Current.modData[receiptKey] = receiptValue!;
    NativeFoodOwnership.EnsureSave(); return shop;
}
string PopulationPayload(int hour) => SaveCodec.Write(Population.Create(
    new[] { new CitizenIdentity { Id = "rlo:ownership-fixture", Name = "Ownership fixture", Age = 30 } },
    new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 } },
    "ownership-fixture", "test", 11, 1440 + hour * 60));
void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }

internal static class Hooks
{
    internal static object? Call(string name, params object?[] args) => typeof(NativeFoodOwnership)
        .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
}
public sealed class Save
{
    public int Day = 1, Hour = 8; public float Minute;
    public Dictionary<string, string> modData = new();
    public List<BuildingRegistration> BuildingRegistrations = new();
}
public static class SaveGameManager { public static Save Current = new(); }
public sealed class BuildingRegistration
{
    public string StreetName = "Test", Signature = "business:Test:7:1:bakery:player";
    public int StreetNumber = 7; public bool RentedByPlayer = true;
    public Entities.Address Address => new(StreetName, StreetNumber);
    public readonly List<int> CompletedOrders = new();
    public void Reset() => Hooks.Call("ResetRegistration", this);
}
public sealed class BuildingManager { public static BuildingManager Instance = new(); public BuildingRegistration? buildingRegistration; }
public static class IndoorCustomerSpawner { public static readonly List<object> Customers = new(); }
public static class BusinessSimulatorHelper
{
    public static readonly WorkQueue Work = new();
    public static readonly List<int> Processed = new();
    public static bool TimeMachineRunning;
    public static void Queue(BuildingRegistration reg, int hour) => Work.Pending.Enqueue(() => {
        var simulator = new RetailBusinessSimulator(); simulator.SetUp(reg, hour);
        if ((bool)Hooks.Call("HourlyCustomers", simulator)!) { Processed.Add(hour); reg.CompletedOrders.Add(hour); }
    });
    public static void RunHourly()
    {
        Work.ForceCompleteAllWork();
        foreach (var reg in SaveGameManager.Current.BuildingRegistrations.Where(r => r.RentedByPlayer)) Queue(reg, SaveGameManager.Current.Hour);
        if (SaveGameManager.Current.Hour >= 23 || TimeMachineRunning) Work.ForceCompleteAllWork();
    }
}
// Native call order copied from the current GameManager method: ending-hour accounting,
// clock increment, midnight rollover/daily settlement, then the new postfix boundary.
public static class GameManager
{
    public static Action? DuringNewHour;
    public static readonly List<int> DailySettlementOrderCounts = new();
    public static void RunMainGameTick(float minutes)
    {
        SaveGameManager.Current.Minute += minutes;
        while (SaveGameManager.Current.Minute >= 60)
        {
            BusinessSimulatorHelper.RunHourly();
            SaveGameManager.Current.Hour++; SaveGameManager.Current.Minute -= 60;
            if (SaveGameManager.Current.Hour >= 24)
            {
                SaveGameManager.Current.Hour -= 24; SaveGameManager.Current.Day++;
                DuringNewHour?.Invoke();
                DailySettlementOrderCounts.Add(SaveGameManager.Current.BuildingRegistrations.Sum(r => r.CompletedOrders.Count));
            }
            else DuringNewHour?.Invoke();
        }
        Hooks.Call("AfterNativeTick");
    }
}
public sealed class WorkQueue
{
    public readonly Queue<Action> Pending = new();
    public void ForceCompleteAllWork() { while (Pending.Count > 0) Pending.Dequeue()(); }
}
namespace Entities { public readonly record struct Address(string Street, int Number); }
namespace Helpers { public static class TimeHelper { public static int GetDayOfWeek() => 1; } }
namespace Helpers.BusinessSimulation
{
    public class BusinessSimulator
    {
        protected BuildingRegistration buildingRegistration = null!;
        protected int currentHour;
        public void SetUp(BuildingRegistration reg, int hour) { buildingRegistration = reg; currentHour = hour; }
    }
}
namespace Buildings.Retail.Simulation
{
    public sealed class RetailBusinessSimulator : BusinessSimulator
    { public void ProcessAllCustomersFromThisHour() { } public void ProcessCustomer() { } }
}
namespace AI.Customers.CustomerEntries
{
    public sealed class CustomerEntry { }
    public static class CustomerEntriesHelper
    {
        public static readonly List<int> Generated = new();
        public static List<CustomerEntry> GetEntriesByAddress(Entities.Address address)
        {
            object?[] args = { address, null };
            return (bool)Hooks.Call("CustomerDraw", args)! ? new() { new() } : (List<CustomerEntry>)args[1]!;
        }
        public static void UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration registration, int day)
        {
            if ((bool)Hooks.Call("GenerateCustomers", registration)!)
                Generated.AddRange(Enumerable.Range(SaveGameManager.Current.Hour, 24 - SaveGameManager.Current.Hour));
        }
    }
}
namespace PersistentCity.Runtime
{
    public sealed class NativeFoodMarket
    {
        public BuildingRegistration Reg; public bool Eligible = true;
        public NativeFoodMarket(BuildingRegistration reg) { Reg = reg; }
        public static string PlaceId(BuildingRegistration reg) => "address:" + reg.StreetName + ":" + reg.StreetNumber + ":shop";
        public static bool IsBuying(BuildingRegistration reg) => false;
        public void RefreshKnownOwnership() => NativeFoodOwnership.Observe(Reg, Eligible);
        public void ActivateScope(FoodAuthority row, BuildingRegistration reg) { }
    }
    public static class FoodBusinessCatalogue { public static string BusinessIdentity(BuildingRegistration reg) => reg.Signature; }
}
namespace UnityEngine { public static class Debug { public static void LogError(string value) { } } }
namespace BAModAPI
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)] public sealed class RegisterModClassAttribute : Attribute { public RegisterModClassAttribute(Type type) { } }
    public sealed class ModEntryOnInitializationLoadAttribute : Attribute { }
    public sealed class ModContext { }
    public interface IModBigAmbitions { string[] RelativeAssetBundlePaths { get; } Task OnLoadAsync(ModContext context); Task OnUnloadAsync(); }
}
namespace HarmonyLib
{
    public static class Priority { public const int First = 100; }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string name) { } public int priority; }
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;
    }
    public sealed class Harmony
    {
        public static readonly List<MethodInfo> Patched = new();
        public Harmony(string id) { }
        public void Patch(MethodInfo method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null) => Patched.Add(method);
        public void UnpatchAll(string id) => Patched.Clear();
    }
}
