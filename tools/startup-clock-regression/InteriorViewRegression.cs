using System;
using System.Collections.Generic;
using System.Diagnostics;
using BigAmbitions.InteriorDesigner.InteriorElements;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;

internal static class InteriorViewRegression
{
    private sealed class Logger : BAModAPI.IModLogger
    {
        public readonly List<string> Lines = new List<string>();
        public void Info(string text) => Lines.Add(text);
        public void Warn(string text) => Lines.Add(text);
    }
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static BuildingManager Shop(float x = 10, float floor = 1)
    {
        var layout = new Transform();
        var manager = new BuildingManager { building = new Building(), buildingRegistration = new BuildingRegistration(),
            currentBuildingVersion = layout, exitZones = new List<ExitZone> { new ExitZone() } };
        manager.cityBuildingController = new CityBuildingController { building = manager.building,
            entranceDoors = new[] { new BuildingEntranceDoor { doorTransform = new Transform { position = new Vector3 { x = x, y = 1, z = 20 } } } } };
        InteriorElementsHelper.InteriorElementsCache.Clear();
        InteriorElementsHelper.InteriorElementsCache.Add("floor", new InteriorElement { transform = new Transform { parent = layout, position = new Vector3 { y = floor } } });
        InstanceBehavior<BuildingManager>.Instance = manager; BuildingManager.IsInsideBuilding = true;
        return manager;
    }
    public static void Run()
    {
        var logger = new Logger(); int restored = 0;
        var off = new PrototypeSettings();
        InstanceBehavior<GameManager>.Instance = new GameManager();
        InstanceBehavior<GameManager>.Instance.playerController.transform.position = new Vector3 { x = 900, y = 2, z = 900 };
        InstanceBehavior<UI.UIs>.Instance = new UI.UIs();
        var manager = Shop();
        NativeInteriorCityView.Start(off, logger, () => restored++);
        Check(NativeTravelPace.HiddenTravel && !NativeInteriorCityView.Active, "default must stay hidden inside");
        Check(NativeTravelPace.Focus.X == 900, "off focus changed");
        GlobalEvents.Enter(); GlobalEvents.Exit(); Check(restored == 0 && logger.Lines.Count == 0, "off subscribed or logged discovery");
        var on = new PrototypeSettings { InteriorCityView = "on" };
        NativeInteriorCityView.Start(on, logger, () => restored++);
        Time.frameCount++; Check(!NativeTravelPace.HiddenTravel, "ground floor shop stayed hidden");
        Check(NativeTravelPace.Focus.X == 10 && NativeTravelPace.Focus.Z == 20, "focus must be first entrance, not detached player");
        Check(NativeInteriorCityView.Radius(120) == 60 && NativeInteriorCityView.ReleaseRadius(120) == 60, "strict interior radius");
        Check(logger.Lines.Count == 1 && logger.Lines[0].Contains("(10,1,20)"), "anchor/cap log missing");
        for (int i = 0; i < 1000; i++) Check(NativeInteriorCityView.Active, "cached active changed");
        Check(logger.Lines.Count == 1, "active log repeated");
        manager.cityBuildingController!.entranceDoors![0].doorTransform!.position = new Vector3 { x = 123, y = 1 };
        Time.unscaledTime = 2; Check(NativeTravelPace.Focus.X == 10, "entry anchor was not cached");
        manager.exitZones![0].isElevator = true;
        Time.unscaledTime = 3; Check(NativeInteriorCityView.Active, "eligibility polled more often than two seconds");
        Time.unscaledTime = 4; Check(NativeTravelPace.HiddenTravel && !NativeInteriorCityView.Active, "elevator must close at refresh");
        int logCount = logger.Lines.Count; Time.unscaledTime = 10; _ = NativeTravelPace.HiddenTravel;
        Check(logger.Lines.Count == logCount, "fallback repeated");
        manager = Shop(); GlobalEvents.Enter(); Time.frameCount++;
        Check(NativeInteriorCityView.Active, "reentry failed");
        BuildingManager.IsInsideBuilding = false; GlobalEvents.Exit();
        Check(restored == 1 && !NativeInteriorCityView.Active, "exit did not restore");
        manager = Shop(50); GlobalEvents.Enter(); Time.frameCount++;
        Check(NativeInteriorCityView.Active && NativeTravelPace.Focus.X == 50, "second shop retained first anchor");
        NativeInteriorCityView.Suspend(); Check(!NativeInteriorCityView.Active, "save/skip suspension reactivated in same frame");
        Time.frameCount++; Check(NativeInteriorCityView.Active, "save resume failed");
        foreach (int cause in new[] { 0, 1, 2, 3, 4 })
        {
            PhysicalTrafficHooks.CurrentFastForward = cause == 0;
            Time.timeScale = cause == 1 ? 6 : 1;
            UI.Load.LoadScene.isLoading = cause == 2;
            Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking = cause == 3;
            InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = cause == 4;
            Check(NativeTravelPace.HiddenTravel && !NativeInteriorCityView.Active, "hidden cause failed closed");
            PhysicalTrafficHooks.CurrentFastForward = false; Time.timeScale = 1; UI.Load.LoadScene.isLoading = false;
            Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking = false;
            InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = false;
            Time.frameCount++; Check(NativeInteriorCityView.Active, "normal view failed to resume");
        }
        NativeInteriorCityView.FailClosed(); Time.frameCount++; Check(!NativeInteriorCityView.Active, "exception reenabled same entry");
        GlobalEvents.Exit(); Time.frameCount++; Check(NativeInteriorCityView.Active, "exception did not clear at exit");
        manager = Shop(70, 5); Time.frameCount++;
        Check(!NativeInteriorCityView.Active && NativeTravelPace.HiddenTravel, "upper floor did not fall back");
        manager = Shop(); manager.cityBuildingController!.entranceDoors = Array.Empty<BuildingEntranceDoor>(); Time.frameCount++;
        Check(!NativeInteriorCityView.Active, "missing first door guessed another anchor");
        manager = Shop(); InteriorElementsHelper.InteriorElementsCache.Clear(); Time.frameCount++;
        Check(!NativeInteriorCityView.Active, "missing floor guessed ground level");
        manager = Shop(); manager.enteringBuilding = true; Time.frameCount++;
        Check(!NativeInteriorCityView.Active, "incomplete load was eligible");
        manager.enteringBuilding = false; Check(NativeInteriorCityView.Active, "completed load did not inspect");
        Bench();
        NativeInteriorCityView.Stop(); Check(!NativeInteriorCityView.Active && NativeTravelPace.HiddenTravel, "disable/load cleanup failed");
        BuildingManager.IsInsideBuilding = false; InstanceBehavior<BuildingManager>.Instance = null;
        Console.WriteLine("PASS " + checks + " F0 actual adapter/anchor/lifecycle checks (Unity doubles).");
    }
    private static void Measure(string name, Action action, int batch = 1000)
    {
        for (int i = 0; i < 10000; i++) action();
        var times = new double[300]; long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < times.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int j = 0; j < batch; j++) action();
            times[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / batch;
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes; Array.Sort(times);
        Console.WriteLine($"F0 offline {name}: per-call ms p50={times[150]:F7} p95={times[285]:F7} max(batch mean)={times[299]:F7}; bytes/call={bytes / (300.0 * batch):F3}; batches=300 x {batch}.");
    }
    private static void Bench()
    {
        Measure("cached active predicate", () => { if (!NativeInteriorCityView.Active) throw new Exception(); });
        Measure("anchor/focus", () => { if (NativeTravelPace.Focus.X != 10) throw new Exception(); });
        Measure("eligibility refresh (cached floor/anchor)", () => { Time.unscaledTime += 2; if (!NativeInteriorCityView.Active) throw new Exception(); });
    }
}
