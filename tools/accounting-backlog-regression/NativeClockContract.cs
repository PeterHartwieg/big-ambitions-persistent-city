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
        SaveGameManager.Current = new object();
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
        SaveGameManager.Current = new object();
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
        Console.WriteLine("PASS actual NativeCityClock source: ordinary/skip/fence/refusal/pacing/recovery/teardown (native/Harmony doubles)");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
public class InstanceBehavior<T> { public static T? Instance; }
public static class SaveGameManager { public static object? Current; }
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
