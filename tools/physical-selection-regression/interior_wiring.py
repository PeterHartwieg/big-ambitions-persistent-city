"""Compile the actual F0 gates/release/body-retention loop against small native doubles.
No game binary is loaded. Fail extraction if a production seam moves.
"""
from pathlib import Path


def generate(root: Path, between):
    travel = (root / 'Runtime/NativeTravelPace.cs').read_text()
    population = (root / 'Runtime/PhysicalPopulation.cs').read_text()
    pedestrians = (root / 'Runtime/PhysicalPedestrians.cs').read_text()
    vehicles = (root / 'Runtime/PhysicalVehicles.cs').read_text()
    mod = (root / 'Runtime/PersistentCityMod.cs').read_text()
    clock = (root / 'Runtime/NativeCityClock.cs').read_text()
    hooks = (root / 'Runtime/PhysicalTrafficHooks.cs').read_text()
    hidden = between(travel, '        public static bool HiddenTravel =>', '        public static Position Focus')
    ready = between(vehicles, '        private static bool NativeReady =>', '        internal PhysicalVehicleBody? Owned(').replace('private static', 'public static')
    pause = between(pedestrians, '            bool pause =', '            Agent.isStopped =')
    construct = between(mod, 'if (settings.PhysicalEnabled &&', '\n                {')
    release = between(population, '        public void BeforeAdvance()', '        public void Update()')
    bodies = between(population, '            if (NativeTravelPace.HiddenTravel) {', '            if (Time.unscaledTime < nextSelection')
    tick = between(clock, '        private static void TickPrefix(', '        private static void RidePrefix(').replace('private static', 'public static')
    speed = between(hooks, '        private static void SpeedChangingPrefix(', '        private static void CrashPostfix(').replace('private static', 'public static')
    # Wiring assertions supplement executable seam tests: the real entry points must still use them.
    assert 'RunPhysical(() => physical?.BeforeAdvance());' in mod
    assert 'RunPhysical(() => physical?.BeforeSave());' in mod
    assert 'NativeInteriorCityView.Configured ? RestoreInterior : (Action?)null' in mod
    assert 'finally { NativeInteriorCityView.Suspend(); }' in mod
    assert 'NativeInteriorCityView.Stop();' in between(mod, '        private void Detach()', '        public void Shutdown()')
    assert 'physical = new PhysicalPopulation' in mod
    assert 'vehicles.SuspendForNativeFastForward = SuspendBeforeNativeFastForward;' in population
    assert 'SuspendForNativeFastForward();' in between(vehicles, '        internal void BeforeNativeFastForward()', '        internal static Position Point(')
    return '''namespace F0Wiring {
using System;
using System.Linq;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Runtime;
static class Time { public static float timeScale=1; }
static class LoadScene { public static bool isLoading; }
static class BuildingManager { public static bool IsInsideBuilding; }
static class UndergroundParkingManager { public static bool IsInsideParking; }
static class PhysicalTrafficHooks { public static bool CurrentFastForward; }
class InstanceBehavior<T> where T:new() { public static T Instance = new(); }
class UIs { public Machine timeMachine = new(); public Speed gameSpeed=new(); }
class Machine { public bool isRunning; }
class Speed { public bool Paused; }
static class TrafficManager { public static bool IsInitialized=true, HasInstance=true; public static Traffic Instance=new(); }
class Traffic { public bool enabled=true; }
static class NativeInteriorCityView {
 public static bool Configured;
 public static InteriorViewState State = new();
 public static bool Active => Configured && State.Active;
 public static List<string> Events=new();
 public static void Suspend() { Events.Add("restore"); State.Suspend(); }
 public static double Radius(double r) => State.Radius(r,60);
 public static double ReleaseRadius(double r) => State.ReleaseRadius(r,60);
 public static int Pedestrians(int n) => State.Pedestrians(n,24);
 public static int Vehicles(int n) => State.Vehicles(n,12);
}
static class NativeTravelPace {
''' + hidden + ''' public static Position Focus => NativeInteriorCityView.Active ? NativeInteriorCityView.State.Anchor : default;
}
static class Gates {
''' + ready + ''' public static bool PedestrianPause() {
''' + pause + ''' return pause; }
 public static bool Construct(PrototypeSettings settings, bool physicalFailed, object? physical) {
 return ''' + construct[4:-1] + '''; }
}
class Body { public Position Position; }
class Identity { public string Id=""; }
class Citizen { public Identity Identity=new(); }
class Binding { public Citizen Citizen=new(); public Body? Person, Car; }
class Vehicles { public void AssertHealthy() {} }
class Ambient { public bool IsReserved(string id) => false; }
class Fixture {
 public bool active=true, disposed, ThrowObserve;
 public string ThrowRelease="";
 public Vehicles vehicles=new(); public Ambient ambient=new();
 public Dictionary<string,Binding> bindings=new();
 public List<Binding>? bindingSnapshot=new();
 public double radius=120; public int pedestrianLimit=48,vehicleLimit=24;
 public void ObserveForSave() { NativeInteriorCityView.Events.Add("observe"); if(ThrowObserve)throw new Exception("observe"); }
 public bool Observe(Binding b)=>true;
 public bool TryAwaitArrival(Binding b)=>false;
 public void Release(Binding b) { bindings.Remove(b.Citizen.Identity.Id); NativeInteriorCityView.Events.Add("release"); if(b.Citizen.Identity.Id==ThrowRelease)throw new Exception("release"); }
 public void Add(int people, int cars, double distance=10) {
 for(int i=0;i<people+cars;i++) { var b=new Binding(); b.Citizen.Identity.Id=i.ToString(); var body=new Body{Position=new(distance,0,0)}; if(i<people)b.Person=body;else b.Car=body;bindings.Add(b.Citizen.Identity.Id,b); }
 }
''' + release + ''' public void UpdateBodies() {
''' + bodies + ''' }
}
enum TimeSpeed { Normal, FastForward }
static class ClockFixture {
 public static Action? beforeHiddenTravel;
 public static bool Active=true, ProcessingEconomySkip=false, OrdinaryTick=true;
 public static Func<bool>? populationCaughtUp=()=>true;
 public static Func<double>? nativeClock=()=>0;
 public static long HeldEconomySkipTicks;
 public static double Factor=1;
''' + tick + '''}
static class SpeedFixture {
 public static Fixture? current;
 public static bool CurrentFastForward => PhysicalTrafficHooks.CurrentFastForward;
''' + speed.replace('UnityEngine.Time.timeScale','Time.timeScale').replace('BeforeNativeFastForward()', 'RestoreInterior()') + '''}
''' + (root / 'tools/physical-selection-regression/InteriorWiring.cs.txt').read_text() + '\n}\n'
