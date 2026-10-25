using System.Collections;
using System.Reflection;
using FoodDemandAuditProbe;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Refused(Action call, string message) { try { call(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(message); }
var preflight = JObject.Parse("{op:'pickup-preflight',owners:['c0']}");
var commit = JObject.Parse("{op:'pickup-commit',query:0}");
var f = Fresh();
string original = SaveCodec.Write(f.Simulation.State);
var initialResponse = JObject.FromObject(f.Call(preflight));
Check(initialResponse.Property("error") == null, "Successful response must not contain protocol-reserved error key");
Check(f.Planner.Calls == 0, "Preflight start executes no route work");
f.Tick(); Check(f.Planner.Calls == 1, "One query per frame");
Check(SaveCodec.Write(f.Simulation.State) == original, "Preflight preserves complete live state");
var statusResponse = JObject.FromObject(f.Call(JObject.Parse("{op:'pickup-status'}")));
Check(statusResponse.Property("error") == null, "Successful status must not collide with MacRig error presence check");
Check((string?)statusResponse["status"] == "ready", "Native route fixture is committable");
var car = f.Simulation.State.Citizens[0].Car; string carJson = JsonConvert.SerializeObject(car);
var receipt = JObject.FromObject(f.Call(commit));
Check((bool)receipt["carUnchanged"]! && ReferenceEquals(car, f.Simulation.State.Citizens[0].Car), "Commit retains same car object and fields");
Check((double)receipt["scheduledAt"]! == 1445, "Due event schedules car admission after access walk, not full arrival: " + receipt["scheduledAt"]);
Check(File.ReadAllText(Path.Combine(f.Root, "pickup-test-original-state.json")) == original, "Full original state retained before commit");
f.Simulation.AdvanceTo(1444); Check(f.Simulation.Traffic!.VehicleCount == 0, "No traffic before walking pickup");
f.Simulation.AdvanceTo(1445); Check(f.Simulation.Traffic!.VehicleCount == 1, "Normal kernel admits existing car at pickup");
Check(JsonConvert.SerializeObject(car) == carJson, "Parking is unchanged during pickup");
f.Simulation.AdvanceTo(1468); Check(f.Simulation.State.Citizens[0].CompletedJourneys >= 1, "Existing scheduler completes selected journey");
Check(car!.ParkedPosition.Equals(new Position(190, 0, 0)), "Arrival updates parking through real core completion");
Refused(() => f.Call(commit), "Second diagnostic commit must refuse");

f = Fresh(); SaveGameManager.Current.SaveGameName = "FT Food Routines Ordinary"; f.Call(preflight); f.Tick();
string before = SaveCodec.Write(f.Simulation.State);
Refused(() => f.Call(commit), "Commit outside named Pickup branch must refuse");
Check(SaveCodec.Write(f.Simulation.State) == before, "Wrong branch leaves state unchanged");
f = Fresh(); f.Call(preflight); f.Tick(); f.Planner.Changed = true;
var changedQuote = JObject.FromObject(f.Call(commit));
Check(changedQuote.Property("error") == null && (bool)changedQuote["routeQuoteChanged"]!, "Fresh valid native quote may change without bypassing route validity");
Check((double)changedQuote["scheduledAt"]! == 1446 && (string?)changedQuote["preflightFingerprint"] != (string?)changedQuote["freshFingerprint"], "Fresh validated route is committed and both fingerprints recorded");
f = Fresh(); f.Call(preflight); f.Tick(); f.Planner.Invalid = true;
var refusedQuote = JObject.FromObject(f.Call(commit));
Check(refusedQuote.Property("error") != null && refusedQuote["freshQuote"] != null, "Invalid fresh route refuses with diagnostic fresh geometry");
Check(f.Simulation.State.Citizens[0].Phase == CitizenPhase.AtPlace, "Invalidated quote does not start journey");
f = Fresh(); f.Simulation.State.Citizens[0].Car!.ParkedPosition = new Position(11, 0, 0);
Refused(() => f.Call(preflight), "Parking differing from captured subway evidence must refuse");
f = Fresh(); var trafficOwners = (IDictionary)typeof(CitySimulator).GetField("trafficCitizens", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Simulation)!;
trafficOwners.Add("c0", f.Simulation.State.Citizens[0]);
Refused(() => f.Call(preflight), "Existing journey ownership must refuse");
f = Fresh(); SaveGameManager.Current.BuildingRegistrations.Add(new BuildingRegistration { Place = "shop", RentedByPlayer = true });
var empty = JObject.FromObject(f.Call(preflight)); Check((int)empty["totalQueries"]! == 0, "Player-owned shop excluded before query");
f = Fresh(); UnityEngine.Time.timeScale = 1;
Refused(() => f.Call(preflight), "Running game preflight must refuse");
UnityEngine.Time.timeScale = 0;
Console.WriteLine($"PASS {checks} pickup helper checks against actual Core scheduler/kernel, with native route/clock doubles. No Unity/game execution.");

Probe Fresh()
{
    var state = Population.Create(new[] { new CitizenIdentity { Id = "c0", Name = "Test Resident", Age = 30, Persona = Persona.Commuter, AppearanceSeed = 1 } },
        new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
            new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = 5, Position = new Position(200, 0, 0), OpenMinute = 0, CloseMinute = 1440 } }, "fixture", "test", 7, 1440);
    var person = state.Citizens[0]; person.Preferences.OwnsCar = true; person.Preferences.AllowTransit = true;
    person.NextEventMinute = person.ActivityUntilMinute = 1800;
    person.Car = new CarState { Id = "c0:car", ModelId = "fixture-model", PaintSeed = 17, ParkedPosition = new Position(10, 0, 0) };
    var planner = new Planner(); var sim = new CitySimulator(state, planner); sim.AdvanceTo(1440);
    string path = Path.Combine(Path.GetTempPath(), "persistent-city-pickup-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
    File.WriteAllText(Path.Combine(path, "pickup-subway-evidence.json"), new JObject { ["subwayFirstSamples"] = new JObject {
        ["c0"] = new JObject { ["file"] = "synthetic-subway-before.json", ["car"] = JToken.FromObject(person.Car) } } }.ToString());
    SaveGameManager.Current = new Save { Day = 1, Hour = 0, Minute = 0, SaveGameName = "FT Food Routines Continuity Pickup Synthetic" };
    return new Probe(sim, planner, path);
}

public sealed class Planner : ICitizenRoutePlanner, ITrafficRoutePlanner
{
    public const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public bool ReadyForPopulation => true;
    public bool Changed, Invalid; public int Calls;
    public TrafficNetwork TrafficNetwork { get; } = new TrafficNetwork(Native, new[] {
        new TrafficNetworkNode { Id = 0, Position = new Position(10,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
        new TrafficNetworkNode { Id = 1, Position = new Position(70,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 2 } },
        new TrafficNetworkNode { Id = 2, Position = new Position(130,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 3 } },
        new TrafficNetworkNode { Id = 3, Position = new Position(190,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true } }, 1);
    public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
    public bool IsTrafficLinkClosed(int id) => false;
    public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    public Route? Plan(Place from, Place to, double minute, CitizenState citizen)
    {
        Calls++; if (from.Id != "home" || to.Id != "shop") return null;
        citizen.Identity.Name = "Detached query mutation must not affect live identity";
        return new Route { Legs = new() {
            new() { From=from.Position,To=new Position(10,0,0),Mode=TravelMode.Walk,Minutes=Changed?6:5 },
            new() { From=new Position(Invalid?11:10,0,0),To=new Position(70,0,0),Mode=TravelMode.Car,Minutes=6,ServiceId="native:road:v1:"+Native+":0:1" },
            new() { From=new Position(70,0,0),To=new Position(130,0,0),Mode=TravelMode.Car,Minutes=6,ServiceId="native:road:v1:"+Native+":1:2" },
            new() { From=new Position(130,0,0),To=new Position(190,0,0),Mode=TravelMode.Car,Minutes=6,ServiceId="native:road:v1:"+Native+":2:3" },
            new() { From=new Position(190,0,0),To=to.Position,Mode=TravelMode.Walk,Minutes=5 } } };
    }
}
public sealed class Save { public string SaveGameName = ""; public int Day, Hour; public double Minute; public List<BuildingRegistration> BuildingRegistrations = new(); }
public static class SaveGameManager { public static Save Current = new(); }
public sealed class BuildingRegistration { public bool RentedByPlayer; public string Place = ""; }
namespace UnityEngine { public static class Time { public static float timeScale; } }
namespace PersistentCity.Runtime { public static class NativeFoodMarket { public static string PlaceId(BuildingRegistration r) => r.Place; } }
namespace FoodDemandAuditProbe
{
    public sealed class RuntimeDouble { public object routes; public RuntimeDouble(object planner) { routes = planner; } }
    public sealed partial class Probe
    {
        private static readonly BindingFlags ContinuityFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public CitySimulator Simulation; public Planner Planner; public string Root;
        public RuntimeDouble Runtime; private string pendingId = "test";
        public Probe(CitySimulator simulation, Planner planner, string root) { Simulation = simulation; Planner = planner; Runtime = new RuntimeDouble(planner); Root = root; }
        private static bool Allowed() => true;
        private static object? ObserveField(object value, string name) => value.GetType().GetField(name, ContinuityFields)!.GetValue(value);
        private static object? ObserveProperty(object value, string name) => value.GetType().GetProperty(name, ContinuityFields)!.GetValue(value);
        private static JToken Value(object? value) => value == null ? JValue.CreateNull() : JToken.FromObject(value);
        public object Call(JObject request) => PickupCommand(request);
        public void Tick() => TickPickup();
    }
}
