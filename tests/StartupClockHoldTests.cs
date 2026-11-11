using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// 10 Nov (b) Mac rig, run mac-b-save-reload: S1 was saved at logical minute 1921.9687 (08:01, 0.03 game minutes
/// before 08:02). After the native load, native time ticked into 08:02 before TryStart installed the clock hold
/// (it waited for RLO Ready), and activation refused with "Accounting input epoch was lost". H1 (1983.011, 0.99
/// minutes of slack) reloaded. These tests drive the real save-epoch binding, save session, codec, simulator and
/// <see cref="StartupClockHold"/> through that sequence; the frame loop mirrors PopulationRuntime's startup order.
/// </summary>
public sealed partial class FoodCommerceTests
{
    private const double RigS1Minute = 1921.9687246084213; // The S1 success marker's minute.

    /// <summary>Native time is Day*1440 + Hour*60 + a float minute of the hour.</summary>
    private static double Native(int hourStart, float minuteOfHour) => hourStart + (double)minuteOfHour;

    private sealed class SaveObject { public readonly Dictionary<string, string> modData = new(StringComparer.Ordinal); }

    /// <summary>When PopulationRuntime installs a hold on native time after a load.</summary>
    private enum HoldFrom
    {
        /// <summary>Base code: TryStart's RLO-Ready path, just before initialization.</summary>
        RloReady,
        /// <summary>Fix rounds 1-2: the first PopulationRuntime.Update on the loaded save.</summary>
        FirstUpdate,
        /// <summary>This fix: the city-load callback (OnLoadAsync -> ArmForCityLoad), before the loaded save's ticks.</summary>
        CityLoadCallback,
    }

    /// <summary>One menu/load cycle as production runs it: the previous runtime's Shutdown removed its clock, and
    /// ModEntryOnCityLoad creates a new runtime (a new <see cref="StartupClockHold"/>) in OnLoadAsync. Then loading
    /// frames, frames waiting for RLO, RLO Ready and initialization. Each frame runs GameManager.RunMainGameTick before
    /// PopulationRuntime.Update (Unity orders neither); an unheld tick advances 1/30 game minute.</summary>
    private sealed class NativeLoad
    {
        public int Hour; public float Minute;
        public double Now => Hour + (double)Minute;
        public NativeLoad(double saved) { Hour = (int)(Math.Floor(saved / 60) * 60); Minute = (float)(saved - Hour); }

        /// <summary>Returns the native minute at activation. <paramref name="currentAtCallback"/> false models a city-load
        /// callback that runs before SaveGameManager.Current names the loaded save.</summary>
        public double Run(HoldFrom from, int loadingFrames, int waitingFrames, bool currentAtCallback = true)
        {
            var loaded = new object();
            var startup = new StartupClockHold(); // a new runtime per load; nothing carried from the unloaded one
            bool held = false;                    // Shutdown stopped the previous clock
            if (from == HoldFrom.CityLoadCallback)
            {
                // OnLoadAsync -> ArmForCityLoad -> ArmStartupClock(SaveGameManager.Current)
                startup.ForSave(currentAtCallback ? loaded : null);
                if (startup.Begin()) held = true; // NativeCityClock.Arm()
            }
            for (int frame = 0; frame < loadingFrames + waitingFrames; frame++)
            {
                bool loading = frame < loadingFrames;
                if (!held) Minute += 1f / 30; // the native tick first
                // PopulationRuntime.Update: ArmStartupClock, Wait, then TryStart (RLO NotReady returns early).
                if (from == HoldFrom.CityLoadCallback)
                {
                    startup.ForSave(loaded); // a guard bound to no save stays installed; only the bookkeeping starts over
                    if (startup.Begin()) held = true;
                }
                if (from == HoldFrom.FirstUpdate)
                {
                    startup.ForSave(loaded);
                    if (startup.Begin()) held = true;
                }
                if (startup.Wait(loading, 1.0 / 30)) held = false;
            }
            // RLO Ready: TryStart installs the bound clock and starts initialization; activation follows.
            startup.Initializing();
            ReleasedAtEnd = startup.Released;
            return Now;
        }
        public bool ReleasedAtEnd { get; private set; }
    }

    private static Dictionary<string, string> OrdinarySave(SaveEpochBinding binding, SaveObject save, SaveSession session, CitySimulator city, double native)
    {
        var result = SaveEpochCallback.Run(binding, save, save.modData, false, ticket =>
        {
            // CaptureSteps: observations first, then the payload write inside the ticket.
            Assert.True(city.BeginAccountingObservations(native));
            Assert.True(binding.CanPublish(ticket, save, save.modData));
            var written = session.WriteTo(save.modData, city.State);
            return written.Success ? SaveEpochCapture.Written(city.State.Minute, city.State.PopulationKey) : SaveEpochCapture.Refused(written.Error);
        }, reason => throw new Exception("stopped: " + reason), _ => { });
        Assert.Equal(SaveEpochResult.Published, result);
        return new Dictionary<string, string>(save.modData, StringComparer.Ordinal);
    }

    /// <summary>TryStart's worker on a loaded file: epoch marker, strict read, marker-describes-state.</summary>
    private static (CityState State, SaveEpochMarker Marker) LoadFile(Dictionary<string, string> file, string populationKey)
    {
        Assert.False(AccountingStopStore.IsStopped(file));
        Assert.Null(SaveEpochStore.CheckForStart(file, out var marker));
        var read = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, populationKey).InitialReadResult;
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Null(SaveEpochStore.DescribesState(marker, read.SourceSchema, read.State));
        return (read.State!, marker!);
    }

    /// <summary>CompleteInitialization's accounting checks, unchanged by this fix: either refuses with StopAccounting.</summary>
    private static CitySimulator? Activate(CityState state, double native, Walking routes, Market market)
    {
        if (!AccountingClock.Compatible(state.Minute, native)) return null;
        var city = new CitySimulator(state, routes);
        if (!city.IsAccountingEpochDrained(native)) return null;
        city.AttachFoodMarket(market);
        return city;
    }

    /// <summary>Native time held at each minute fence: drain the epoch, cross into the next, observe, up to <paramref name="to"/>.</summary>
    private static void RunNative(CitySimulator city, double from, double to)
    {
        double clock = from;
        while (true)
        {
            Assert.True(city.BeginAccountingObservations(clock));
            double next = Math.Min(to, Math.Floor(clock) + 1);
            if (next == to) { Advance(city, Math.Max(city.State.Minute, to), 1); return; }
            Advance(city, Math.Max(city.State.Minute, AccountingClock.PreviousEpochTarget(next)), 1);
            clock = next;
        }
    }

    private static (CitySimulator City, Dictionary<string, string> File, SaveEpochMarker Marker, CityState Loaded, Walking Routes) FreshCityFirstSave(double saveMinute)
    {
        var (city, _, routes) = Ready(60);
        var save = new SaveObject();
        // #202: the creation save during initialization is deferred and writes nothing.
        var binding = new SaveEpochBinding(save, save.modData, 0, initializing: true);
        Assert.Equal(SaveEpochResult.Deferred, SaveEpochCallback.Run(binding, save, save.modData, false,
            _ => throw new Exception("captured while initializing"), reason => throw new Exception(reason), _ => { }));
        Assert.Empty(save.modData);
        binding.CompleteInitialization();
        var session = SaveSession.Open(save.modData, city.State.PopulationKey);
        Assert.True(session.BindTo(save.modData).Success);
        RunNative(city, 1440, saveMinute);
        Assert.Equal(saveMinute, city.State.Minute);
        var file = OrdinarySave(binding, save, session, city, saveMinute);
        var (loaded, marker) = LoadFile(file, city.State.PopulationKey);
        Assert.Equal(1, marker.Sequence);
        Assert.Equal(saveMinute, loaded.Minute);
        return (city, file, marker, loaded, routes);
    }

    [Fact]
    public void FirstOrdinarySaveAfterADeferredCreationSaveReloadsWithNativeHeldFromTheFirstStartupAttempt()
    {
        // The rig's S1 minute of the hour (xx:01.97), at 11:01 so lunch is under way and work is due right at the save.
        double native = Native(2100, 1.9687246f);
        Assert.Equal(RigS1Minute - 1920, native - 2100);
        var (city, file, marker, loaded, routes) = FreshCityFirstSave(native);
        Assert.Contains(city.State.Food!.Residents, m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving);

        // Before this fix: the rig's reload (3 loading frames, then a second of RLO NotReady) crosses into 11:02.
        double before = new NativeLoad(loaded.Minute).Run(HoldFrom.RloReady, 3, 30);
        Assert.True(Math.Floor(before) > Math.Floor(native));
        Assert.Null(Activate(loaded, before, routes, new Market())); // "Accounting input epoch was lost"
        // A hold installed by the first Update is still late if the first native tick runs before it: 11:02.002.
        double firstUpdate = new NativeLoad(loaded.Minute).Run(HoldFrom.FirstUpdate, 3, 30);
        Assert.Equal(2102, Math.Floor(firstUpdate));
        Assert.Null(Activate(loaded, firstUpdate, routes, new Market()));

        // After: the city-load callback arms the guard; native stays at the saved minute even with the tick first,
        // whether or not SaveGameManager.Current already names the loaded save in the callback.
        Assert.Equal(native, new NativeLoad(loaded.Minute).Run(HoldFrom.CityLoadCallback, 3, 30, currentAtCallback: false));
        double after = new NativeLoad(loaded.Minute).Run(HoldFrom.CityLoadCallback, 3, 30);
        Assert.Equal(native, after);
        var restored = Activate(loaded, after, routes, new Market());
        Assert.NotNull(restored);
        Assert.True(restored!.BeginAccountingObservations(after));
        // The reloaded city continues exactly as the city that never saved.
        double later = Native(2100, 3.5f);
        RunNative(city, native, later); RunNative(restored, after, later);
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(restored.State));

        // One in-game hour later (R2 -> H1 style): save again from the reloaded session and reload.
        var reloadedSave = new SaveObject();
        foreach (var pair in file) reloadedSave.modData.Add(pair.Key, pair.Value);
        var next = new SaveEpochBinding(reloadedSave, reloadedSave.modData, marker.Sequence, initializing: true);
        next.CompleteInitialization();
        var nextSession = SaveSession.Open(new Dictionary<string, string> { [SaveStore.StateKey] = file[SaveStore.StateKey] }, loaded.PopulationKey);
        Assert.True(nextSession.BindTo(reloadedSave.modData).Success);
        double hourLater = Native(2160, 3.0113802f);
        RunNative(restored, later, hourLater);
        var h1 = OrdinarySave(next, reloadedSave, nextSession, restored, hourLater);
        var (h1State, h1Marker) = LoadFile(h1, loaded.PopulationKey);
        Assert.Equal(2, h1Marker.Sequence);
        double h1Native = new NativeLoad(h1State.Minute).Run(HoldFrom.CityLoadCallback, 3, 30);
        var h1City = Activate(h1State, h1Native, routes, new Market());
        Assert.NotNull(h1City);
        Assert.True(h1City!.BeginAccountingObservations(h1Native));
        Assert.Equal(restored.State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId),
            h1State.Citizens.Select(c => c.Identity.Id + "|" + c.Identity.Name + "|" + c.HomeId));
    }

    [Fact]
    public void ReleasedStartupHoldStillRefusesALoadThatCrossedAMinute()
    {
        // RLO never became ready within the wait: native time is released, crosses, and the save is still refused.
        var (_, _, _, loaded, routes) = FreshCityFirstSave(Native(2100, 1.9687246f));
        int waiting = (int)(StartupClockHold.MaximumWaitSeconds * 30) + 2;
        var load = new NativeLoad(loaded.Minute);
        double native = load.Run(HoldFrom.CityLoadCallback, 3, waiting);
        Assert.True(load.ReleasedAtEnd);
        Assert.True(Math.Floor(native) > Math.Floor(loaded.Minute));
        Assert.Null(Activate(loaded, native, routes, new Market()));
    }

    [Fact]
    public void SaveWithUnfinishedWorkFromAnEarlierEpochStillRefuses()
    {
        var (city, _, routes) = Ready();
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 6); Assert.Equal(MealStatus.Serving, meal.Status);
        // A genuinely lost input: the snapshot's clock is inside the native minute but a purchase from an earlier
        // epoch is still owed. Holding native time early does not forgive it.
        double saved = Math.Floor(city.State.Minute) + .5;
        meal.DueAt = meal.ServiceAt = Math.Floor(saved) - 1.5; meal.Deadline = saved + 30;
        city.State.Food.Shops.Single().NextServiceAt = meal.ServiceAt;
        city.State.Minute = saved;
        Assert.True(AccountingClock.Compatible(saved, saved));
        Assert.Null(Activate(city.State, saved, routes, new Market()));
    }
}

public sealed class StartupClockHoldTests
{
    [Fact]
    public void HoldBeginsOnceAndLoadingScreenTimeIsNotCounted()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin()); Assert.False(hold.Begin());
        for (int i = 0; i < 10000; i++) Assert.False(hold.Wait(loading: true, 1));
        Assert.True(hold.Holding);
    }

    [Fact]
    public void WaitReleasesExactlyOnceAndNotAgainForThisSave()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - .5));
        Assert.True(hold.Wait(false, 1));
        Assert.False(hold.Holding); Assert.True(hold.Released);
        Assert.False(hold.Wait(false, 100));
        Assert.False(hold.Begin()); // No second freeze for the same save.
        hold.Reset();
        Assert.True(hold.Begin()); // A different save starts a fresh hold.
    }

    [Fact]
    public void InitializationTakesOverAndNeverTimesOut()
    {
        var hold = new StartupClockHold();
        Assert.True(hold.Begin());
        hold.Initializing();
        Assert.False(hold.Wait(false, 1000));
        Assert.False(hold.Released);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var h = new StartupClockHold(); h.Begin(); h.Wait(false, double.NaN); });
    }

    [Fact]
    public void HoldBelongsToOneSaveAndASwitchStartsOver()
    {
        object s1 = new object(), s2 = new object();
        var hold = new StartupClockHold();
        Assert.True(hold.ForSave(s1)); Assert.True(hold.Begin());
        Assert.False(hold.ForSave(s1)); Assert.True(hold.Holding);
        // S1 waits past the limit and is released; loading S2 starts a fresh hold from its first attempt.
        Assert.True(hold.Wait(false, StartupClockHold.MaximumWaitSeconds));
        Assert.True(hold.Released);
        Assert.True(hold.ForSave(s2));
        Assert.False(hold.Released);
        Assert.True(hold.Begin());
        // S2 replaces S1 while S1 still holds: S2 holds again, with its own full wait.
        Assert.True(hold.ForSave(s1)); Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - 1));
        Assert.True(hold.ForSave(s2)); Assert.True(hold.Begin());
        Assert.False(hold.Wait(false, StartupClockHold.MaximumWaitSeconds - 1));
        Assert.True(hold.Holding);
    }

    /// <summary>Pins the Runtime wiring (Unity-bound, not compiled here) to the order the tests model.</summary>
    [Fact]
    public void RuntimeArmsTheGuardBeforeTheLoadAndTiesItToOneSave()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string path) => File.ReadAllText(Path.Combine(root!.FullName, path)).Replace("\r\n", "\n");
        string mod = Read("Runtime/PersistentCityMod.cs");
        // ModEntryOnCityLoad: each load creates a new runtime; its load callback arms the guard after Initialize.
        string onLoad = mod.Substring(mod.IndexOf("public Task OnLoadAsync(ModContext context)", StringComparison.Ordinal));
        int init = onLoad.IndexOf("runtime.Initialize(context);", StringComparison.Ordinal);
        int armed = onLoad.IndexOf("runtime.ArmForCityLoad();", StringComparison.Ordinal);
        Assert.True(init > 0 && init < armed && armed < onLoad.IndexOf("return Task.CompletedTask;", init, StringComparison.Ordinal));
        string armForLoad = mod.Substring(mod.IndexOf("public void ArmForCityLoad()", StringComparison.Ordinal));
        Assert.Contains("try { ArmStartupClock(SaveGameManager.Current); }", armForLoad.Substring(0, armForLoad.IndexOf("\n        }", StringComparison.Ordinal)));
        string start = mod.Substring(mod.IndexOf("private void TryStart()", StringComparison.Ordinal));
        int rlo = start.IndexOf("ReadRlo(\"startup\")", StringComparison.Ordinal);
        int late = start.IndexOf("if (!NativeCityClock.Active) StartCityClock(save);", StringComparison.Ordinal);
        int task = start.IndexOf("Task.Run", StringComparison.Ordinal);
        Assert.True(rlo > 0 && rlo < late && late < task);
        Assert.True(start.IndexOf("startupHold.Initializing();", StringComparison.Ordinal) < task);
        string update = mod.Substring(mod.IndexOf("private void UpdateCity()", StringComparison.Ordinal));
        // Armed before the save check (the menu) and again after a save change reset, before any TryStart.
        int arm = update.IndexOf("ArmStartupClock(save);", StringComparison.Ordinal);
        Assert.True(arm > 0 && arm < update.IndexOf("if (save == null) return;", StringComparison.Ordinal));
        int rearm = update.IndexOf("ArmStartupClock(save);", update.IndexOf("ResetPopulationForSaveChange();", StringComparison.Ordinal), StringComparison.Ordinal);
        int wait = update.IndexOf("if (startupHold.Wait(UI.Load.LoadScene.isLoading, Time.unscaledDeltaTime))", StringComparison.Ordinal);
        Assert.True(rearm > 0 && rearm < wait && wait < update.IndexOf("TryStart();", StringComparison.Ordinal));
        Assert.True(update.IndexOf("NativeCityClock.Stop();", wait, StringComparison.Ordinal) > wait);
        string armClock = mod.Substring(mod.IndexOf("private void ArmStartupClock(object? save)", StringComparison.Ordinal));
        Assert.Contains("if (simulation != null || initialization != null) return;", armClock);
        Assert.Contains("if (startupHold.ForSave(save) && NativeCityClock.Bound) NativeCityClock.Stop();", armClock);
        string complete = mod.Substring(mod.IndexOf("private void CompleteInitialization(Task<PreparedCity> pending)", StringComparison.Ordinal));
        // The lost-epoch refusals at activation are unchanged.
        Assert.Contains("if (!AccountingClock.Compatible(state.Minute, Now()))\n            { StopAccounting(); return; }", complete);
        Assert.Contains("if (!simulation.IsAccountingEpochDrained(Now()))\n            { StopAccounting(); return; }", complete);
        Assert.True(complete.IndexOf("NativeCityClock.StartupHold = false;", StringComparison.Ordinal)
            < complete.IndexOf("saveEpoch.CompleteInitialization();", StringComparison.Ordinal));
        string clock = Read("Runtime/NativeCityClock.cs");
        // The guard and a clock bound to a replaced save hold every tick; Start replaces the guard in one call.
        Assert.Contains("if (patches != null && !Active) { if (deltaTimeWithMultiplier > 0) deltaTimeWithMultiplier = 0; return; }", clock);
        Assert.Contains("if (LoadScene.isLoading && StartupHold && deltaTimeWithMultiplier > 0) { deltaTimeWithMultiplier = 0; return; }", clock);
        Assert.True(clock.IndexOf("if (Armed) Stop();", StringComparison.Ordinal) < clock.IndexOf("if (patches != null) throw new InvalidOperationException(\"City clock already active.\");", StringComparison.Ordinal));
        Assert.Contains("AccountingLimited = Refused = StartupHold = false;", clock);
        Assert.Contains("startupHold.Reset();", Read("Runtime/PopulationLifecycle.cs"));
    }
}
