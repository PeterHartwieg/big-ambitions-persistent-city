using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AI.Customers.CustomerEntries;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Runtime;

internal static partial class Program
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions;
    public static Exception? Finalizer(Exception? __exception) => __exception;

    private static MethodInfo Stub(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;

    static partial void HookConflictArms()
    {
        var tick = Stub(typeof(GameManager), "RunMainGameTick");
        var customer = Stub(typeof(Buildings.Retail.Simulation.RetailBusinessSimulator), "ProcessCustomer");
        var reset = Stub(typeof(BuildingRegistration), "Reset");
        var owners = new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, NativeHookInventory.ClockObserver, "rig.limiter", "other.mod", "PC_TEST_FOREIGN" };
        var harmonies = owners.ToDictionary(o => o, o => new Harmony(o));
        HarmonyMethod Pre() => new HarmonyMethod(typeof(Program), nameof(Prefix));
        HarmonyMethod Post() => new HarmonyMethod(typeof(Program), nameof(Postfix));
        try
        {
            harmonies[NativeHookInventory.FoodAuthority].Patch(tick, postfix: Post());
            harmonies[NativeHookInventory.CityClock].Patch(tick, prefix: Pre());
            harmonies[NativeHookInventory.ClockObserver].Patch(tick, postfix: Post());
            var clean = HookConflicts.Scan();
            var entry = clean.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)");
            Check(entry.Patched && entry.Foreign.Count == 0 && entry.PcOwners.Count == 3, "H1/H3/H4 together are not foreign");
            Check(clean.ForeignOwners == 0 && clean.ActivationLine().Contains("no foreign owner"), "clean activation line");

            harmonies[NativeHookInventory.FoodAuthority].Patch(customer, prefix: Pre());
            harmonies["other.mod"].Patch(customer, prefix: Pre(), postfix: Post(), transpiler: new HarmonyMethod(typeof(Program), nameof(Transpiler)),
                finalizer: new HarmonyMethod(typeof(Program), nameof(Finalizer)));
            harmonies["rig.limiter"].Patch(tick, prefix: Pre());
            harmonies["PC_TEST_FOREIGN"].Patch(Stub(typeof(Buildings.Retail.Simulation.RetailBusinessSimulator), "GetMaxCustomerCapacityThisHour"), postfix: Post());

            var report = HookConflicts.Scan();
            var checkout = report.Entries.Single(e => e.Signature == "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)");
            Check(checkout.Foreign.Select(f => f.Owner + ":" + f.Kind + ":" + HookConflictReport.ClassWord(f.Class)).SequenceEqual(new[]
                { "other.mod:Prefix:may-skip", "other.mod:Postfix:observer", "other.mod:Transpiler:rewrites", "other.mod:Finalizer:swallows" }),
                "every patch kind classed: " + string.Join(",", checkout.Foreign));
            Check(checkout.PcOwners.SequenceEqual(new[] { NativeHookInventory.FoodAuthority }), "PC owner listed, not foreign");
            var withRig = report.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)");
            Check(withRig.Foreign.Count == 1 && withRig.Foreign[0].Owner == "rig.limiter" && withRig.Foreign[0].Class == HookConflictClass.MaySkip, "rig limiter is a may-skip prefix");
            Check(report.Entries.Single(e => e.Signature == "CustomerEntriesHelper.GetEntriesByAddress(Address)").Patched == false, "no patch info reports none");
            Check(report.Entries.Single(e => e.Signature == "BuildingRegistration.Reset(Boolean)").Patched == false, "unpatched target reports not patched");
            Check(report.ForeignOwners == 2 && !report.Text().Contains("PC_TEST_FOREIGN"), "only PC targets are reported");
            Check(!report.Text().Contains("compatible", StringComparison.OrdinalIgnoreCase), "never says compatible");

            harmonies[NativeHookInventory.FoodAuthority].Patch(reset, prefix: Pre());
            var times = new double[500];
            for (int i = -50; i < times.Length; i++)
            {
                var scanned = HookConflicts.Scan();
                if (i >= 0) times[i] = scanned.ScanMilliseconds;
            }
            Array.Sort(times);
            int patched = report.Entries.Count(e => e.Patched) + 1;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "hook conflicts: {0} patched stand-in targets (+1 foreign-only): scan p50={1:0.0000} ms p95={2:0.0000} ms max={3:0.0000} ms",
                patched, times[times.Length / 2], times[times.Length * 95 / 100], times[^1]));
            Console.WriteLine("hook conflicts: " + report.ActivationLine());

            var extra = typeof(ManyTargets).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.Name.StartsWith("M", StringComparison.Ordinal)).ToArray();
            for (int i = 0; i < extra.Length; i++) harmonies[i % 2 == 0 ? "other.mod" : "rig.limiter"].Patch(extra[i], postfix: Post());
            int patchedMethods = Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m)?.Owners.Count > 0);
            var path = new double[500];
            long pathBytes = 0;
            var reportText = new CapabilityReport();
            for (int i = -50; i < path.Length; i++)
            {
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var inputs = new CapabilityInputs { CityActive = true };
                HookConflicts.ReadMetadata(inputs);
                _ = reportText.Status(CapabilitySnapshot.Build(inputs), "x", CapabilitySnapshot.Context(inputs));
                if (i >= 0) { path[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; pathBytes += GC.GetAllocatedBytesForCurrentThread() - a0; }
            }
            Array.Sort(path);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "pc capability status (shared metadata read + snapshot + report): {0} patched methods, {1} assemblies: p50={2:0.0000} ms p95={3:0.0000} ms max={4:0.0000} ms, {5} B per command",
                patchedMethods, AppDomain.CurrentDomain.GetAssemblies().Length, path[path.Length / 2], path[path.Length * 95 / 100], path[^1], pathBytes / path.Length));
        }
        finally { foreach (var pair in harmonies) pair.Value.UnpatchAll(pair.Key); }
        Check(Harmony.GetAllPatchedMethods().All(m => Harmony.GetPatchInfo(m)?.Owners.Count is null or 0), "every stand-in patch removed");
    }
}

public static class ManyTargets
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M00() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M01() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M02() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M03() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M04() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M05() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M06() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M07() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M08() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M09() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M10() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M11() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M12() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M13() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M14() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M15() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M16() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M17() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M18() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M19() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M20() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M21() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M22() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M23() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M24() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M25() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M26() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M27() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M28() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M29() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M30() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M31() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M32() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M33() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M34() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M35() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M36() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M37() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M38() { }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static void M39() { }
}
