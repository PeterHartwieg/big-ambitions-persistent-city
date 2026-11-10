using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests
{
    /// <summary>F8e (docs/plans/f8-capability-boundaries.md D4): foreign owners on Persistent City targets, classed by patch
    /// kind, report only. The Core join from synthetic rows, then the pinned Harmony 2.4.2 on stand-in methods (loaded by
    /// reflection as ResearchChargeTests.ForeignPatchCheckCost does; tools/capability-contracts adds the transpiler arm and
    /// runs Runtime/HookConflicts.cs itself).</summary>
    [Collection(F8aSerial.Collection)]
    public sealed class HookConflictReportTests
    {
        private static HookPatchedMethod Method(string type, string method, string[] parameters, params (string Owner, HookPatchKind Kind)[] patches)
            => new HookPatchedMethod(type, method, parameters, patches.Select(p => new HookPatch(p.Owner, p.Kind)).ToArray());

        [Fact]
        public void PatchKindsAreClassedAsD4Says()
        {
            Assert.Equal(HookConflictClass.Observer, HookConflictReport.Classify(HookPatchKind.Postfix));
            Assert.Equal(HookConflictClass.MaySkip, HookConflictReport.Classify(HookPatchKind.Prefix));
            Assert.Equal(HookConflictClass.Rewrites, HookConflictReport.Classify(HookPatchKind.Transpiler));
            Assert.Equal(HookConflictClass.Swallows, HookConflictReport.Classify(HookPatchKind.Finalizer));
            // Harmony 2.4.2's inner patches change calls inside the body: rewrites.
            Assert.Equal(HookConflictClass.Rewrites, HookConflictReport.Classify(HookPatchKind.InnerPrefix));
            Assert.Equal(HookConflictClass.Rewrites, HookConflictReport.Classify(HookPatchKind.InnerPostfix));
        }

        [Fact]
        public void ForeignOwnersAreJoinedToTheInventoryAndOurOwnersAreNot()
        {
            var report = HookConflictReport.Build(new[]
            {
                Method("GameManager", "RunMainGameTick", new[] { "Single" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Postfix),
                    (NativeHookInventory.CityClock, HookPatchKind.Prefix), (NativeHookInventory.ClockObserver, HookPatchKind.Postfix), ("rig.limiter", HookPatchKind.Prefix)),
                Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix),
                    ("other.mod", HookPatchKind.Finalizer), ("other.mod", HookPatchKind.Transpiler), ("other.mod", HookPatchKind.Postfix), ("other.mod", HookPatchKind.Prefix)),
                Method("MarketingHelper", "RunDaily", Array.Empty<string>(), (NativeHookInventory.ResearchBilling, HookPatchKind.Postfix)),
                Method("BuildingRegistration", "Reset", new[] { "Boolean" }),        // listed by Harmony with no patch left
                Method("UnrelatedType", "Unrelated", Array.Empty<string>(), ("other.mod", HookPatchKind.Prefix)),
                Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry", "Int32" }, ("other.mod", HookPatchKind.Prefix)), // another overload
            });
            Assert.Equal(NativeHookInventory.Targets.Select(t => t.Type + "." + t.Method + "(" + string.Join(", ", t.Parameters!) + ")").Distinct().Count(), report.Entries.Count);
            var tick = report.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)");
            Assert.Equal(new[] { "food.authority", "clock.city", "clock.observe" }, tick.Capabilities);
            Assert.Equal(3, tick.PcOwners.Count);
            Assert.Equal(new[] { ("rig.limiter", HookPatchKind.Prefix, HookConflictClass.MaySkip) }, tick.Foreign);
            var checkout = report.Entries.Single(e => e.Signature == "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)");
            Assert.Equal(new[] { HookConflictClass.MaySkip, HookConflictClass.Observer, HookConflictClass.Rewrites, HookConflictClass.Swallows }, checkout.Foreign.Select(f => f.Class));
            Assert.Empty(report.Entries.Single(e => e.Signature == "MarketingHelper.RunDaily()").Foreign);
            Assert.False(report.Entries.Single(e => e.Signature == "BuildingRegistration.Reset(Boolean)").Patched);
            Assert.False(report.Entries.Single(e => e.Signature == "CitizenData.IsPriceAcceptable(String, Single)").Patched);
            Assert.Equal(2, report.ForeignOwners);

            string text = report.Text();
            Assert.StartsWith("Persistent City hook conflicts (report only): 3 of " + report.Entries.Count + " target methods patched, 2 with foreign owners, 2 foreign owner(s)\n", text);
            Assert.Contains("GameManager.RunMainGameTick(Single) [food.authority, clock.city, clock.observe]: rig.limiter prefix (may-skip)\n", text);
            Assert.Contains("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) [food.authority]: other.mod prefix (may-skip); other.mod postfix (observer); other.mod transpiler (rewrites); other.mod finalizer (swallows)\n", text);
            Assert.Contains("MarketingHelper.RunDaily() [research.billing]: no foreign owner\n", text);
            Assert.Contains("BuildingRegistration.Reset(Boolean) [food.authority, food.dynamic]: not patched\n", text);
            Assert.DoesNotContain("Unrelated", text);
            Assert.DoesNotContain("compatible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GameManager.RunMainGameTick(Single) rig.limiter may-skip; RetailBusinessSimulator.ProcessCustomer(CustomerEntry) other.mod may-skip, other.mod observer, other.mod rewrites, other.mod swallows. Details: pc capability hooks.", report.ActivationLine());
            Assert.EndsWith("no foreign owner on 0 patched targets.", HookConflictReport.Build(Array.Empty<HookPatchedMethod>()).ActivationLine());
        }

        [Fact]
        public void ReportOnlyChangesNoCapability()
        {
            // D4: until F8h, economy and presentation hooks only report. The report type has no path into the registry.
            Assert.DoesNotContain(typeof(HookConflictReport).GetMethods(), m => m.GetParameters().Any(p => p.ParameterType == typeof(CapabilityRegistry)));
            string runtime = CapabilityReportTests.StripComments(File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "HookConflicts.cs")));
            foreach (string member in CapabilityReportTests.Mutating) Assert.DoesNotContain(member, runtime);
            Assert.Contains("Harmony.GetAllPatchedMethods()", runtime);
            Assert.Contains("Harmony.GetPatchInfo(method)", runtime);
        }

        // ---- The pinned Harmony 2.4.2, loaded by reflection ------------------------------------------------------------

        private static Assembly? Harmony()
        {
            string package = Path.Combine(CapabilityReportTests.Root(), "build", "lib.harmony.2.4.2.nupkg");
            if (!File.Exists(package)) return null;
            string directory = Path.Combine(Path.GetTempPath(), "pc-c5d-harmony-2.4.2");
            Directory.CreateDirectory(directory);
            string dll = Path.Combine(directory, "0Harmony.dll");
            if (!File.Exists(dll))
                using (var zip = System.IO.Compression.ZipFile.OpenRead(package)) System.IO.Compression.ZipFileExtensions.ExtractToFile(zip.GetEntry("lib/net10.0/0Harmony.dll")!, dll, true);
            return Assembly.LoadFrom(dll);
        }

        /// <summary>The same reads Runtime/HookConflicts.Scan makes, through reflection.</summary>
        private static List<HookPatchedMethod> Scan(Type harmony)
        {
            var all = (System.Collections.IEnumerable)harmony.GetMethod("GetAllPatchedMethods")!.Invoke(null, null)!;
            var info = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) })!;
            var rows = new List<HookPatchedMethod>();
            foreach (MethodBase method in all)
            {
                var patches = info.Invoke(null, new object[] { method });
                if (patches == null) continue;
                var list = new List<HookPatch>();
                foreach (var (field, kind) in new[] { ("Prefixes", HookPatchKind.Prefix), ("Postfixes", HookPatchKind.Postfix), ("Transpilers", HookPatchKind.Transpiler),
                    ("Finalizers", HookPatchKind.Finalizer), ("InnerPrefixes", HookPatchKind.InnerPrefix), ("InnerPostfixes", HookPatchKind.InnerPostfix) })
                    foreach (var patch in (System.Collections.IEnumerable)patches.GetType().GetField(field)!.GetValue(patches)!)
                        list.Add(new HookPatch((string)patch.GetType().GetField("owner")!.GetValue(patch)!, kind));
                rows.Add(new HookPatchedMethod(method.DeclaringType!.Name, method.Name, method.GetParameters().Select(p => p.ParameterType.Name).ToArray(), list));
            }
            return rows;
        }

        [Fact]
        public void RealHarmonyOnStandInMethodsIsClassed()
        {
            var assembly = Harmony();
            if (assembly == null) return; // tools/build.py downloads the pinned package; ForeignPatchCheckCost has the same guard
            var harmony = assembly.GetType("HarmonyLib.Harmony", true)!;
            var methodType = assembly.GetType("HarmonyLib.HarmonyMethod", true)!;
            var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            object H(string owner) => Activator.CreateInstance(harmony, owner)!;
            object M(string name) => Activator.CreateInstance(methodType, typeof(F8eStandIn.Patches).GetMethod(name))!;
            var tick = typeof(F8eStandIn.GameManager).GetMethod(nameof(F8eStandIn.GameManager.RunMainGameTick))!;
            var run = typeof(F8eStandIn.MarketingHelper).GetMethod(nameof(F8eStandIn.MarketingHelper.RunDaily))!;
            var owners = new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, NativeHookInventory.ClockObserver, NativeHookInventory.ResearchBilling, "f8e.foreign" };
            var instances = owners.ToDictionary(o => o, H);
            try
            {
                patch.Invoke(instances[NativeHookInventory.FoodAuthority], new object?[] { tick, null, M("Postfix"), null, null });
                patch.Invoke(instances[NativeHookInventory.CityClock], new object?[] { tick, M("Prefix"), null, null, null });
                patch.Invoke(instances[NativeHookInventory.ClockObserver], new object?[] { tick, null, M("Postfix"), null, null });
                var ours = HookConflictReport.Build(Scan(harmony)).Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)");
                Assert.True(ours.Patched); Assert.Empty(ours.Foreign); Assert.Equal(3, ours.PcOwners.Count);

                patch.Invoke(instances[NativeHookInventory.ResearchBilling], new object?[] { run, null, M("Postfix"), null, null });
                patch.Invoke(instances["f8e.foreign"], new object?[] { run, M("Prefix"), M("Postfix"), null, M("Finalizer") });
                var report = HookConflictReport.Build(Scan(harmony));
                var bill = report.Entries.Single(e => e.Signature == "MarketingHelper.RunDaily()");
                Assert.Equal(new[] { ("f8e.foreign", HookPatchKind.Prefix, HookConflictClass.MaySkip), ("f8e.foreign", HookPatchKind.Postfix, HookConflictClass.Observer),
                    ("f8e.foreign", HookPatchKind.Finalizer, HookConflictClass.Swallows) }, bill.Foreign);
                Assert.Empty(report.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)").Foreign);
                Assert.False(report.Entries.Single(e => e.Signature == "MarketingAgencyDialog.CampaignSettings()").Patched);
            }
            finally
            {
                var unpatch = harmony.GetMethod("UnpatchAll", new[] { typeof(string) })!;
                foreach (var pair in instances) unpatch.Invoke(pair.Value, new object[] { pair.Key });
            }
            Assert.All(HookConflictReport.Build(Scan(harmony)).Entries, e => Assert.False(e.Patched));
        }
    }
}

namespace PersistentCity.Tests.F8eStandIn
{
    public sealed class GameManager { [MethodImpl(MethodImplOptions.NoInlining)] public void RunMainGameTick(float deltaTimeWithMultiplier) { } }
    public static class MarketingHelper { [MethodImpl(MethodImplOptions.NoInlining)] public static void RunDaily() { } }
    public static class Patches
    {
        public static void Prefix() { }
        public static void Postfix() { }
        public static Exception? Finalizer(Exception? __exception) => __exception;
    }
}
