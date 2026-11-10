using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AI.Customers.CustomerEntries;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Runtime;

/// <summary>F8e arm: Runtime/HookConflicts.Scan with the pinned Harmony 2.4.2 on stand-in methods that carry the inventory
/// signatures. Rig check 1 records the owners actually present under the full modpack.</summary>
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
            // H1/H3/H4 on one method are Persistent City's own and not foreign.
            harmonies[NativeHookInventory.FoodAuthority].Patch(tick, postfix: Post());
            harmonies[NativeHookInventory.CityClock].Patch(tick, prefix: Pre());
            harmonies[NativeHookInventory.ClockObserver].Patch(tick, postfix: Post());
            var clean = HookConflicts.Scan();
            var entry = clean.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)");
            Check(entry.Patched && entry.Foreign.Count == 0 && entry.PcOwners.Count == 3, "H1/H3/H4 together are not foreign");
            Check(clean.ForeignOwners == 0 && clean.ActivationLine().Contains("no foreign owner"), "clean activation line");

            // One PC owner plus a foreign owner of each kind on the checkout target; a foreign prefix on the tick too.
            harmonies[NativeHookInventory.FoodAuthority].Patch(customer, prefix: Pre());
            harmonies["other.mod"].Patch(customer, prefix: Pre(), postfix: Post(), transpiler: new HarmonyMethod(typeof(Program), nameof(Transpiler)),
                finalizer: new HarmonyMethod(typeof(Program), nameof(Finalizer)));
            harmonies["rig.limiter"].Patch(tick, prefix: Pre());
            // A foreign owner on a method Persistent City does not patch is outside the report.
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

            // Scan time at activation (plan estimate 27 x 0.011 ms = 0.3 ms): every inventory-shaped stub patched.
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
        }
        finally { foreach (var pair in harmonies) pair.Value.UnpatchAll(pair.Key); }
        Check(Harmony.GetAllPatchedMethods().All(m => Harmony.GetPatchInfo(m)?.Owners.Count is null or 0), "every stand-in patch removed");
    }
}
