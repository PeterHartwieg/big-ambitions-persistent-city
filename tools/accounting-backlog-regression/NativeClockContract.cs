using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Native/Harmony API doubles. Invoke the actual registered runtime prefixes in priority
// order. Their installation on the shipped game remains a pending rig assertion.
public static class NativeClockContract
{
    public delegate void Prefix(ref float delta);
    public static void Run()
    {
        SaveGameManager.Current = new NativeSave();
        InstanceBehavior<UI.UIs>.Instance = new UI.UIs();
        double native = 59, logical = 59; bool ready = false;
        NativeCityClock.Start(SaveGameManager.Current, 1.0 / 3, () => ready, () => true, () => native, () => logical);
        Require(HarmonyLib.Harmony.Prefixes.Count == 2, "calendar/accounting hook registration");
        float Tick(float requested)
        {
            foreach (var hook in HarmonyLib.Harmony.Prefixes.OrderByDescending(p => p.Priority)) hook.Call(ref requested);
            return requested;
        }
        Require(Tick(3) == 0 && NativeCityClock.HeldOrdinaryTicks == 1, "ordinary backlog not held");
        Require(NativeCityClock.Factor == 1.0 / 3 && NativeCityClock.AccountingLimited, "configured pace changed");
        typeof(NativeCityClock).GetField("waitingSince", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null,
            System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency);
        Require(NativeCityClock.PacingAffected, "sustained hold invisible to player status");
        ready = true; float step = Tick(3); Require(step > 0 && step < 1 && NativeCityClock.BoundaryClips == 1, "float fence missing");
        Require(NativeCityClock.MaximumRecoverySeconds >= 1, "recovery duration missing");
        native = 59 + step; logical = AccountingClock.Target(native);
        step = Tick(3); native = (float)((float)native + step);
        Require(native >= 60 && native < 61, "ending hour did not cross into the next epoch fence");
        ready = false; InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = true;
        Require(Tick(120) == 0 && NativeCityClock.HeldEconomySkipTicks == 1, "skip backlog not held");
        Require(NativeCityClock.WithheldMinutes > 0 && NativeCityClock.MaximumLagMinutes <= 1, "lag/overload counters missing");
        native = 62; Require(Tick(120) == 120 && NativeCityClock.Refused && NativeCityClock.PacingAffected, "refusal must release native time immediately");
        Require(Tick(120) == 120, "latched refusal must not freeze subsequent native ticks");
        NativeCityClock.Stop();
        Require(HarmonyLib.Harmony.Prefixes.Count == 0 && !NativeCityClock.Active && !NativeCityClock.Refused, "clock teardown leaked state");
        native = logical = 62; ready = true; InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = false;
        NativeCityClock.Start(SaveGameManager.Current, 1, () => ready, () => true, () => native, () => logical);
        Require(Tick(.5f) == .5f && Tick(0) == 0 && NativeCityClock.HeldOrdinaryTicks == 0, "native factor/earlier zero cap changed");
        Require(!NativeCityClock.CheckInputEpoch(64, logical) && NativeCityClock.Refused,
            "advance/save entry did not catch a writer bypassing ticks");
        SaveGameManager.Current = new NativeSave();
        Require(Tick(.5f) == .5f && !NativeCityClock.Active, "clock leaked into another save");
        NativeCityClock.Stop();
        native = 64; logical = AccountingClock.PreviousEpochTarget(native); ready = false;
        bool drained = false;
        NativeCityClock.Start(SaveGameManager.Current!, 1, () => ready, () => true, () => native, () => logical, _ => drained);
        Require(Tick(.5f) == .5f && NativeCityClock.Refused, "unfinished predecessor epoch must refuse PC and release native time");
        NativeCityClock.Stop(); drained = true;
        NativeCityClock.Start(SaveGameManager.Current!, 1, () => ready, () => true, () => native, () => logical, _ => drained);
        Require(Tick(.5f) == 0 && !NativeCityClock.Refused, "drained predecessor/new-boundary work was refused");
        NativeCityClock.Stop();
        Require(PopulationRuntime.PreparingAccountingText.Contains("Calendar held"), "startup notice must explain the clock hold");
        // Empty ordinary epochs still clip at fences. They must not masquerade as workload
        // holds, including over many hours and under native fast-forward requests.
        InstanceBehavior<UI.UIs>.Instance.timeMachine.isRunning = false;
        foreach (var arm in new[] { (Factor: 1.0 / 3, Rate: 1f), (Factor: 1.0, Rate: 1f), (Factor: 1.0, Rate: 2f) })
        {
            native = logical = 0; ready = true;
            NativeCityClock.Start(SaveGameManager.Current, arm.Factor, () => ready, () => true, () => native, () => logical);
            while (native < 180)
            {
                float allowed = Tick(arm.Rate / 60);
                Require(allowed > 0, "empty epoch did not progress");
                native = Math.Floor(native / 60) * 60 + (float)((float)(native % 60) + allowed);
                logical = AccountingClock.Target(native);
                Require(!NativeCityClock.PacingAffected, "zero-work minute fences raised the workload notice");
            }
            Require(NativeCityClock.BoundaryClips >= 180 && NativeCityClock.WithheldMinutes > 0,
                "quiet fences lost pacing diagnostics");
            Require(NativeCityClock.HeldOrdinaryTicks == 0, "empty ordinary epochs counted as workload holds");
            ready = false;
            Require(Tick(1f / 60) == 0 && NativeCityClock.PacingAffected, "actual workload hold lost its prominent notice");
            NativeCityClock.Stop();
        }
        Console.WriteLine("PASS quiet ordinary fences: 180 minutes each at 1/3, 1 and 2 game minutes/second; workload notice false until an actual hold");
        RecoveryLifecycleContract();
        Console.WriteLine("PASS actual NativeCityClock source: ordinary/skip/fence/refusal/pacing/recovery/teardown (native/Harmony doubles)");
    }
    private static void RecoveryLifecycleContract()
    {
        SaveGameManager.Current = new NativeSave();
        SaveGameManager.Current.modData[PersistentCity.Persistence.SaveStore.StateKey] = "original population";
        var runtime = new PopulationRuntime();
        var stopped = SaveGameManager.Current;
        runtime.SeedOldSaveForTest();
        NativeCityClock.CheckInputEpoch(62, 59);
        Require(runtime.StatusForTest().Contains(PopulationRuntime.AccountingRecoveryText), "transient refusal omitted save-change guidance");
        int withdrawals = PersistentCity.Runtime.NativeFoodOwnership.WithdrawalRequests;
        string message = runtime.StopForTest(); // Actual failure, transition and reset methods; native cleanup surfaces are doubles.
        Require(PersistentCity.Runtime.NativeFoodOwnership.WithdrawalRequests == withdrawals + 1, "accounting stop omitted native withdrawal");
        Require(message.StartsWith(PopulationRuntime.AccountingRecoveryText), "save-change guidance must precede failure details");
        Require(runtime.FailedForTest && !runtime.PrepareForTest(), "same stopped save resumed without a load");
        Require(PersistentCity.Persistence.AccountingStopStore.IsStopped(SaveGameManager.Current.modData), "failure intent lost");
        Require(SaveGameManager.Current.modData[PersistentCity.Persistence.SaveStore.StateKey] == "original population", "stop rewrote population");
        SaveGameManager.Current = null!;
        Require(!runtime.PrepareForTest(), "unload gap cleared the failure");
        var checkpoint = new NativeSave();
        checkpoint.modData[PersistentCity.Persistence.SaveStore.StateKey] = "unmarked synchronized checkpoint";
        SaveGameManager.Current = checkpoint; UI.Load.LoadScene.isLoading = true;
        Require(!runtime.PrepareForTest(), "loading scene resumed old population");
        UI.Load.LoadScene.isLoading = false;
        Require(runtime.PrepareForTest() && !runtime.FailedForTest && runtime.OldSaveClearedForTest,
            "unmarked synchronized checkpoint did not clear accounting failure and old state");
        Require(runtime.CanStartForTest(), "unmarked save could not enter normal initialization/attachment");
        Require(!checkpoint.modData.ContainsKey(PersistentCity.Persistence.AccountingStopStore.Key), "old stop marker leaked into new city");
        Require(checkpoint.modData[PersistentCity.Persistence.SaveStore.StateKey] == "unmarked synchronized checkpoint", "load reset rewrote new population");
        Require(PersistentCity.Persistence.AccountingStopStore.IsStopped(stopped.modData), "leaving stopped save erased its refusal");

        // A newly loaded object for the stopped file must be refused before initialization,
        // including when the first stop happened before saveIdentity was ever assigned.
        var early = new PopulationRuntime(); early.SeedOldSaveForTest(beforeAttachment: true); early.StopForTest();
        var markedReload = new NativeSave();
        foreach (var pair in stopped.modData) markedReload.modData.Add(pair.Key, pair.Value);
        SaveGameManager.Current = markedReload;
        Require(!early.PrepareForTest() && early.FailedForTest && early.OldSaveClearedForTest,
            "marked reload escaped refusal or retained pre-attachment state");
        Require(!early.CanStartForTest(), "marked reload entered initialization");
        Require(markedReload.modData[PersistentCity.Persistence.SaveStore.StateKey] == "original population", "marked reload overwrote archive");
        var anotherCity = new NativeSave();
        anotherCity.modData[PersistentCity.Persistence.SaveStore.StateKey] = "another PC city";
        SaveGameManager.Current = anotherCity;
        Require(early.PrepareForTest() && !early.FailedForTest && early.CanStartForTest(), "accounting marker in one city stranded another city");

        var unrelated = new PopulationRuntime(); unrelated.FailForTest(); SaveGameManager.Current = new NativeSave();
        Require(!unrelated.PrepareForTest() && unrelated.FailedForTest, "non-accounting failure was cleared by save change");
        Console.WriteLine("PASS actual accounting lifecycle: unmarked checkpoint resumes startup, marked reload refuses, old state discarded, generic failures retained (native cleanup doubles; actual Fail)");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
public class InstanceBehavior<T> { public static T? Instance; }
public sealed class NativeSave { public readonly Dictionary<string, string> modData = new(); }
public static class SaveGameManager { public static NativeSave Current = null!; }
namespace PersistentCity.Runtime
{
    internal static class NativeFoodOwnership
    {
        internal static int WithdrawalRequests;
        internal static void ReleaseAfterAccountingFailure() => WithdrawalRequests++;
        // F5c-lite's development-city refusal hooks: no refused city exists in this harness.
        internal static void EnsureSave() { }
        internal static PersistentCity.Core.FoodAuthorityStandDown? StandDown => null;
    }
}
public sealed class GameManager { public void RunMainGameTick(float deltaTimeWithMultiplier) { } }
namespace UI
{
    public sealed class UIs { public TimeMachine timeMachine = new TimeMachine(); }
    public sealed class TimeMachine { public bool isRunning; }
}
namespace UI.Load { public static class LoadScene { public static bool isLoading = false; } }
namespace HarmonyLib
{
    public static class Priority { public const int First = 800, Last = 0; }
    public sealed class HarmonyMethod
    {
        public int priority;
        public MethodInfo Method;
        public HarmonyMethod(Type type, string method) { Method = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!; }
    }
    public sealed class Harmony
    {
        public static readonly List<(int Priority, NativeClockContract.Prefix Call)> Prefixes = new();
        public Harmony(string owner) { }
        public static bool HasAnyPatches(string owner) => Prefixes.Count > 0;
        public void Patch(MethodInfo method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
        { if (prefix != null) Prefixes.Add((prefix.priority, prefix.Method.CreateDelegate<NativeClockContract.Prefix>())); }
        public void UnpatchAll(string owner) => Prefixes.Clear();
    }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] arguments) => type.GetMethod(name, arguments);
    }
}
