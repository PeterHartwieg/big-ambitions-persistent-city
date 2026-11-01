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
        Check(NativeGameSpeed.Prepare(), "validated native speed reader could not bind");
        foreach (var speed in new[] { UI.TimeSpeed.Normal, UI.TimeSpeed.Pause, UI.TimeSpeed.FastForward })
        {
            InstanceBehavior<UI.UIs>.Instance.gameSpeed.SetForTest(speed);
            Check(NativeGameSpeed.CurrentFastForward == (speed == UI.TimeSpeed.FastForward), "typed accessor did not read real private field");
            Check(NativeGameSpeed.CurrentFastForward == NativeGameSpeed.LegacyFastForward, "typed/legacy native speed results differ");
        }
        InstanceBehavior<UI.UIs>.Instance.gameSpeed.SetForTest(UI.TimeSpeed.Normal);
        var manager = Shop();
        NativeInteriorCityView.Start(off, logger, () => restored++);
        Check(NativeTravelPace.HiddenTravel && !NativeInteriorCityView.Active, "default must stay hidden inside");
        Check(NativeTravelPace.Focus.X == 900, "off focus changed");
        GlobalEvents.Enter(); GlobalEvents.Exit(); GlobalEvents.TimeMachineStarted(); UnityEngine.SceneManagement.SceneManager.Unload();
        Check(restored == 0 && logger.Lines.Count == 1 && logger.Lines[0].Contains("not detected; not detected; effective view=off"),
            "off must log only the guard verdict and subscribe to no view events");
        logger.Lines.Clear();
        var on = new PrototypeSettings { InteriorCityView = "on",
            ThirdPartyInteriorHarmonyOwnerIds = new[] { "persistentcity.fixture.unmatched" } };
        NativeInteriorCityView.Start(on, logger, () => restored++);
        Check(HarmonyLib.Harmony.MetadataReads == 2, "one metadata read per vanilla target at load");
        on.InteriorCityView = "off";
        Check(NativeInteriorCityView.Configured, "configured bool must be captured at startup");
        on.InteriorCityView = "on";
        Time.UnscaledTimeReads = 0;
        Time.frameCount++; Check(!NativeTravelPace.HiddenTravel, "ground floor shop stayed hidden");
        Check(NativeTravelPace.Focus.X == 10 && NativeTravelPace.Focus.Z == 20, "focus must be first entrance, not detached player");
        Check(NativeInteriorCityView.Radius(120) == 60 && NativeInteriorCityView.ReleaseRadius(120) == 60, "strict interior radius");
        Check(logger.Lines.Count == 2 && logger.Lines[1].Contains("(10,1,20)"), "anchor/cap log missing");
        for (int i = 0; i < 1000; i++) Check(NativeInteriorCityView.Active, "cached active changed");
        Check(Time.UnscaledTimeReads == 1, "full context refresh repeated within a frame");
        Check(logger.Lines.Count == 2, "active log repeated");
        manager.cityBuildingController!.entranceDoors![0].doorTransform!.position = new Vector3 { x = 123, y = 1 };
        Time.frameCount++; Time.unscaledTime = 2; Check(NativeTravelPace.Focus.X == 10, "entry anchor was not cached");
        manager.exitZones![0].isElevator = true;
        Time.frameCount++; Time.unscaledTime = 3; Check(NativeInteriorCityView.Active, "eligibility polled more often than two seconds");
        Time.frameCount++; Time.unscaledTime = 4; Check(NativeTravelPace.HiddenTravel && !NativeInteriorCityView.Active, "elevator must close at refresh");
        int logCount = logger.Lines.Count; Time.frameCount++; Time.unscaledTime = 10; _ = NativeTravelPace.HiddenTravel;
        Check(logger.Lines.Count == logCount, "fallback repeated");
        manager = Shop(); GlobalEvents.Enter(); Time.frameCount++;
        Check(NativeInteriorCityView.Active, "reentry failed");
        BuildingManager.IsInsideBuilding = false; GlobalEvents.Exit();
        Check(restored == 1 && !NativeInteriorCityView.Active, "exit did not restore");
        int outdoorRestores = restored;
        UnityEngine.SceneManagement.SceneManager.Unload();
        Check(restored == outdoorRestores, "auxiliary outdoor scene unload released street bodies");
        manager = Shop(50); GlobalEvents.Enter(); Time.frameCount++;
        Check(NativeInteriorCityView.Active && NativeTravelPace.Focus.X == 50, "second shop retained first anchor");
        NativeInteriorCityView.Suspend(); Check(!NativeInteriorCityView.Active, "save/skip suspension reactivated in same frame");
        Time.frameCount++; Check(NativeInteriorCityView.Active, "save resume failed");
        int restoredBeforeMachine = restored;
        InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = true;
        GlobalEvents.TimeMachineStarted();
        Check(restored == restoredBeforeMachine + 1 && !NativeInteriorCityView.State.Active, "time machine start must restore synchronously before first tick");
        InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = false;
        Time.frameCount++; Check(NativeInteriorCityView.Active, "time machine resume failed");
        foreach (int cause in new[] { 0, 1, 2, 3, 4, 5 })
        {
            // A successful query already populated this exact frame's cache.
            Check(NativeInteriorCityView.Active, "same-frame blocker fixture not active");
            PhysicalTrafficHooks.CurrentFastForward = cause == 0 || cause == 5;
            Time.timeScale = cause == 1 ? 6 : cause == 0 ? 0 : 1;
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
        int restoredBeforeScene = restored;
        UnityEngine.SceneManagement.SceneManager.Unload();
        Check(restored == restoredBeforeScene + 1 && !NativeInteriorCityView.Active, "scene unload did not restore/suspend");
        Time.frameCount++; Check(NativeInteriorCityView.Active, "new scene did not recheck eligibility");
        Bench();
        Check(HarmonyLib.Harmony.MetadataReads == 2, "metadata must not repeat on frames, queries or building/scene events");
        GuardRegression(logger, ref restored);
        NativeInteriorCityView.Stop(); Check(!NativeInteriorCityView.Active && NativeTravelPace.HiddenTravel, "disable/load cleanup failed");
        BuildingManager.IsInsideBuilding = false; InstanceBehavior<BuildingManager>.Instance = null;
        Console.WriteLine("PASS " + checks + " F0 actual adapter/anchor/lifecycle checks (Unity doubles).");
    }
    private static void GuardRegression(Logger logger, ref int restored)
    {
        Shop();
        int logCount = logger.Lines.Count;
        var blocked = new PrototypeSettings { InteriorCityView = "on",
            ThirdPartyInteriorAssemblyPatterns = new[] { typeof(InteriorViewRegression).Assembly.GetName().Name! } };
        NativeInteriorCityView.Start(blocked, logger, () => { });
        Check(NativeInteriorGuard.Verdict.Detected && !NativeInteriorCityView.Configured, "matching our own fixture assembly must force F0 off");
        var bridge = new PopulationRuntime();
        for (int i = 0; i < 1000; i++)
        {
            Time.frameCount++;
            Check(!NativeInteriorCityView.Active && NativeTravelPace.HiddenTravel, "guarded F0 became active");
            Check(bridge.InteriorGuardDiagnostics.Contains("effective view=off; active=False"), "guard dump must show cached off state");
        }
        GlobalEvents.Enter(); GlobalEvents.Exit();
        Check(logger.Lines.Count == logCount + 1, "guard should log only once per city load, never per entry/frame/dump");
        blocked.ThirdPartyInteriorGuard = "off";
        NativeInteriorCityView.Start(blocked, logger, () => { });
        Time.frameCount++;
        Check(NativeInteriorCityView.Configured && NativeInteriorCityView.Active, "off must bypass only detection and retain normal F0 eligibility");
        Check(HarmonyLib.Harmony.MetadataReads == 2, "assembly match/off must not read patch metadata");
        // A new city start with the same unmatched owner configuration evaluates once again.
        NativeInteriorCityView.Start(new PrototypeSettings { InteriorCityView = "on",
            ThirdPartyInteriorHarmonyOwnerIds = new[] { "persistentcity.fixture.unmatched" } }, logger, () => { });
        Check(HarmonyLib.Harmony.MetadataReads == 4, "new load must reevaluate both vanilla targets once");
    }
    private static void Measure(string name, Action action, int batch = 1000)
    {
        for (int i = 0; i < Math.Min(10000, batch * 10); i++) action();
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
        Measure("eligibility refresh (cached floor/anchor)", () => { Time.frameCount++; Time.unscaledTime += 2; if (!NativeInteriorCityView.Active) throw new Exception(); });
        Measure("actual validated native speed accessor", () => { if (NativeGameSpeed.CurrentFastForward) throw new Exception(); });
        BeforeInteriorCityView.Start(new PrototypeSettings { InteriorCityView = "on" }, new Logger(), () => { });
        try
        {
            Measure("pre-r1 Active including real FieldInfo.GetValue", () => { if (!BeforeInteriorCityView.Active) throw new Exception(); });
            Measure("r1 Active including validated field delegate + frame cache", () => { if (!NativeInteriorCityView.Active) throw new Exception(); });
            Measure("pre-r1 36 body queries/frame", () => { Time.frameCount++; for (int i=0;i<36;i++) if (!BeforeInteriorCityView.Active) throw new Exception(); }, 100);
            Measure("r1 36 body queries/frame", () => { Time.frameCount++; for (int i=0;i<36;i++) if (!NativeInteriorCityView.Active) throw new Exception(); }, 100);
        }
        finally { BeforeInteriorCityView.Stop(); }
        var floor = InteriorElementsHelper.InteriorElementsCache["floor"];
        InteriorElementsHelper.InteriorElementsCache.Clear();
        for (int i = 0; i < 511; i++) InteriorElementsHelper.InteriorElementsCache.Add(i.ToString(), new InteriorElement { IsFloor = false });
        InteriorElementsHelper.InteriorElementsCache.Add("floor", floor);
        // Reflection is confined to harness setup, never the production/frame loop.
        var reset = (Action)Delegate.CreateDelegate(typeof(Action), typeof(NativeInteriorCityView).GetMethod("ResetEntry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!);
        Measure("entry eligibility 512 cached elements + lifecycle log", () => { reset(); if (!NativeInteriorCityView.Active) throw new Exception(); }, 1);
    }
}
