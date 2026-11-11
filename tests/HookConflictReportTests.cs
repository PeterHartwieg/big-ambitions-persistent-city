using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests
{
    /// <summary>F8e (docs/plans/f8-capability-boundaries.md D4): foreign owners on Persistent City targets, classed by patch
    /// kind; F8h adds the food.checkout.shop refusal policy on the H1 customer targets and H2. The Core join from synthetic rows, then the pinned Harmony 2.4.2 on stand-in methods (loaded by
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
            Assert.StartsWith("Persistent City hook conflicts: 3 of " + report.Entries.Count + " target methods patched, 2 with foreign owners, 2 foreign owner(s), 0 refusing food.checkout.shop\n", text);
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
            // D4: the report type has no path into the registry; F8h's refusal leaves it as a reason string the market applies.
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

        // ---- F8h: conflict refusals (D4 policy, after rig check 1) ----------------------------------------------------

        /// <summary>The five H1 customer targets and H2. Rig check 1 (docs/checks/f8h-check1-modpack-owners-pc.json and
        /// -mac.json, b3e5a910) found no foreign owner on any of them; RunMainGameTick carries the modpack's prefixes and
        /// finalizers and is shared, so it is not in the policy.</summary>
        internal static readonly string[] ExpectedPolicy =
        {
            "RetailBusinessSimulator.ProcessAllCustomersFromThisHour()", "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)",
            "CustomerEntriesHelper.GetEntriesByAddress(Address)", "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)",
            "BuildingRegistration.Reset(Boolean)", "CitizenData.IsPriceAcceptable(String, Single)",
        };

        /// <summary>The table Runtime/HookConflicts.CheckoutPolicy declares, read from the source.</summary>
        internal static string[] RuntimePolicy()
        {
            string source = CapabilityReportTests.StripComments(File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "HookConflicts.cs")));
            var block = Regex.Match(source, @"CheckoutPolicy\s*=\s*\{(?<body>[^}]*)\}");
            Assert.True(block.Success, "CheckoutPolicy table");
            return Regex.Matches(block.Groups["body"].Value, "\"(?<s>[^\"]*)\"").Select(m => m.Groups["s"].Value).ToArray();
        }

        [Fact]
        public void CheckoutPolicyIsExactlyTheH1CustomerTargetsAndH2()
        {
            var policy = RuntimePolicy();
            Assert.Equal(ExpectedPolicy, policy);
            var inventory = NativeHookInventory.Targets.Select(t => t.Type + "." + t.Method + "(" + string.Join(", ", t.Parameters!) + ")").ToHashSet(StringComparer.Ordinal);
            Assert.All(policy, p => Assert.Contains(p, inventory));
            Assert.DoesNotContain("GameManager.RunMainGameTick(Single)", policy);
            // Scan applies the table; nothing else does.
            string runtime = CapabilityReportTests.StripComments(File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "HookConflicts.cs")));
            Assert.Contains("HookConflictReport.Build(methods, CheckoutPolicy)", runtime);
        }

        private static HookConflictReport Refusing(params HookPatchedMethod[] methods) => HookConflictReport.Build(methods, RuntimePolicy());

        [Theory]
        [InlineData(HookPatchKind.Prefix, "prefix")]
        [InlineData(HookPatchKind.Transpiler, "transpiler")]
        [InlineData(HookPatchKind.Finalizer, "finalizer")]
        [InlineData(HookPatchKind.InnerPrefix, "inner prefix")]
        [InlineData(HookPatchKind.InnerPostfix, "inner postfix")]
        public void MaySkipRewritesAndSwallowsOnEveryPolicyTargetRefuseCheckout(HookPatchKind kind, string word)
        {
            foreach (string signature in ExpectedPolicy)
            {
                var target = NativeHookInventory.Targets.First(t => t.Type + "." + t.Method + "(" + string.Join(", ", t.Parameters!) + ")" == signature);
                var report = Refusing(Method(target.Type, target.Method, target.Parameters!.ToArray(), (target.Owner, HookPatchKind.Prefix), ("other.mod", kind)));
                Assert.Equal("foreign " + word + " by other.mod on " + signature, report.CheckoutRefusal);
                var entry = report.Entries.Single(e => e.Signature == signature);
                Assert.Equal(report.CheckoutRefusal, entry.CheckoutRefusal);
                Assert.Contains(signature + " [" + string.Join(", ", entry.Capabilities) + "]: other.mod " + word + " (", report.Text());
                Assert.Contains(") -> refuses food.checkout.shop\n", report.Text());
                Assert.Contains("food.checkout.shop off: foreign " + word + " by other.mod on " + signature + ". Details: pc capability hooks.", report.ActivationLine());
                // Without the policy (F8e) the same owner only reports.
                Assert.Null(HookConflictReport.Build(new[] { Method(target.Type, target.Method, target.Parameters!.ToArray(), ("other.mod", kind)) }).CheckoutRefusal);
            }
        }

        [Fact]
        public void ObserversAndOtherTargetsOnlyReport()
        {
            // A foreign postfix (observer) on every policy target does not refuse.
            var observers = Refusing(ExpectedPolicy.Select(signature =>
            {
                var t = NativeHookInventory.Targets.First(x => x.Type + "." + x.Method + "(" + string.Join(", ", x.Parameters!) + ")" == signature);
                return Method(t.Type, t.Method, t.Parameters!.ToArray(), ("other.mod", HookPatchKind.Postfix));
            }).ToArray());
            Assert.Null(observers.CheckoutRefusal);
            Assert.Equal(6, observers.Entries.Count(e => e.Foreign.Count > 0));
            Assert.DoesNotContain("-> refuses", observers.Text());
            Assert.DoesNotContain("food.checkout.shop off", observers.ActivationLine());

            // Rig check 1 shapes (both rigs, b3e5a910): the modpack's RunMainGameTick owners, hardmodepatch on
            // ItemsGetter.RegisterModItem, a rig-input transpiler on an inspector target, a vehicle owner. None refuses.
            var modpack = Refusing(
                Method("GameManager", "RunMainGameTick", new[] { "Single" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Postfix), (NativeHookInventory.CityClock, HookPatchKind.Prefix),
                    (NativeHookInventory.ClockObserver, HookPatchKind.Postfix), ("cityperformance.mod", HookPatchKind.Prefix), ("cityperformance.mod", HookPatchKind.Finalizer),
                    ("drivenrides.mod", HookPatchKind.Prefix), ("foodtrucks.mod", HookPatchKind.Prefix), ("foodtrucks.mod", HookPatchKind.Postfix), ("foodtrucks.mod", HookPatchKind.Finalizer)),
                Method("ItemsGetter", "RegisterModItem", new[] { "Item" }, (NativeHookInventory.FoodContract, HookPatchKind.Postfix), ("hardmodepatch", HookPatchKind.Prefix)),
                Method("MouseController", "Run", Array.Empty<string>(), (NativeHookInventory.InspectorInput, HookPatchKind.Prefix), ("rig.input", HookPatchKind.Transpiler)),
                Method("DensityManager", "AddVehicle", new[] { "Boolean", "Int32", "Int32", "Vector3", "Vector3" }, (NativeHookInventory.PhysicalVehicles, HookPatchKind.Transpiler), ("vehicle.mod", HookPatchKind.Prefix)),
                Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix)));
            Assert.Null(modpack.CheckoutRefusal);
            Assert.Equal(6, modpack.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)").Foreign.Count);
            Assert.All(modpack.Entries, e => Assert.Null(e.CheckoutRefusal));
            Assert.Contains(", 0 refusing food.checkout.shop\n", modpack.Text());
            // Persistent City's own owners on a policy target are never foreign.
            Assert.Empty(modpack.Entries.Single(e => e.Signature == "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)").Foreign);
        }

        /// <summary>The decision runs once per load at the market's construction, from the scan the activation line then
        /// reuses; a member refusal stays first; a conflict refusal enters the existing checkout-off mode.</summary>
        [Fact]
        public void TheRefusalIsDecidedOncePerLoadFromTheLoadScan()
        {
            var sources = CapabilityReportTests.RuntimeSources().ToDictionary(p => p.Key, p => CapabilityReportTests.StripComments(p.Value), StringComparer.Ordinal);
            var callers = sources.Where(p => p.Value.Contains("CheckoutForLoad()", StringComparison.Ordinal)).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "Runtime/NativeFoodContract.cs", "Runtime/NativeFoodMarket.cs" }, callers);
            Assert.Single(Regex.Matches(sources["Runtime/NativeFoodMarket.cs"], @"CheckoutForLoad\(\)"));
            Assert.Contains("checkout = NativeFoodContract.StartCheckout(NativeFoodContract.CheckoutForLoad(), out process);", sources["Runtime/NativeFoodMarket.cs"]);
            string contract = sources["Runtime/NativeFoodContract.cs"];
            Assert.Contains("conflict = HookConflicts.ScanForLoad().CheckoutRefusal;", contract);
            Assert.Contains("members.Refusal != null || conflict == null ? members : new Result(conflict, members.Members)", contract);
            Assert.Single(Regex.Matches(string.Concat(sources.Values), @"ScanForLoad\(\)\.")); // only CheckoutForLoad reads it
            Assert.Contains("(HookConflicts.TakeLoadScan() ?? HookConflicts.Scan()).ActivationLine()", sources["Runtime/CapabilityDiagnostics.cs"]);
            // The refusal goes through F8c's checkout-off mode: no price prefix, nothing served, every shop observed ineligible.
            var mode = new FoodCheckoutMode("foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)");
            Assert.False(mode.Serves); Assert.False(mode.InstallsPricePrefix); Assert.False(mode.Observe(true));
            Assert.Equal("Excluded: food checkout contract off: foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)", mode.ScopeReason);
            var inputs = CapabilityReportTests.Healthy(); inputs.FoodCheckoutRefusal = mode.Refusal;
            var registry = CapabilitySnapshot.Build(inputs);
            Assert.Equal(CapabilityState.Off, registry.State(CapabilityId.FoodCheckoutShop));
            Assert.Equal(CapabilityStartStatus.Ready, registry.Start().Status);
            Assert.Contains("food.checkout.shop optional off: foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry) | scope: shop ownership",
                new CapabilityReport().Status(registry, "x"));
        }

        /// <summary>F8h with the pinned Harmony 2.4.2 on stand-in methods: a foreign prefix on ProcessCustomer and a foreign
        /// finalizer on ProcessAllCustomersFromThisHour refuse with the owner named; a foreign postfix does not; the check-1
        /// modpack owners on RunMainGameTick do not. tools/capability-contracts adds the transpiler arm and runs Runtime.</summary>
        [Fact]
        public void RealHarmonyForeignOwnersRefuseCheckoutByPolicy()
        {
            var assembly = Harmony();
            if (assembly == null) return; // tools/build.py downloads the pinned package; ForeignPatchCheckCost has the same guard
            var harmony = assembly.GetType("HarmonyLib.Harmony", true)!;
            var methodType = assembly.GetType("HarmonyLib.HarmonyMethod", true)!;
            var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            object M(string name) => Activator.CreateInstance(methodType, typeof(F8hStandIn.Patches).GetMethod(name))!;
            var tick = typeof(F8hStandIn.GameManager).GetMethod(nameof(F8hStandIn.GameManager.RunMainGameTick))!;
            var customer = typeof(F8hStandIn.RetailBusinessSimulator).GetMethod(nameof(F8hStandIn.RetailBusinessSimulator.ProcessCustomer))!;
            var hour = typeof(F8hStandIn.RetailBusinessSimulator).GetMethod(nameof(F8hStandIn.RetailBusinessSimulator.ProcessAllCustomersFromThisHour))!;
            var owners = new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, "cityperformance.mod", "drivenrides.mod", "foodtrucks.mod", "f8h.foreign" };
            var instances = owners.ToDictionary(o => o, o => Activator.CreateInstance(harmony, o)!);
            var unpatch = harmony.GetMethod("UnpatchAll", new[] { typeof(string) })!;
            HookConflictReport Load() => HookConflictReport.Build(Scan(harmony), RuntimePolicy());
            try
            {
                patch.Invoke(instances[NativeHookInventory.FoodAuthority], new object?[] { tick, null, M("PcPostfix"), null, null });
                patch.Invoke(instances[NativeHookInventory.CityClock], new object?[] { tick, M("PcPrefix"), null, null, null });
                patch.Invoke(instances[NativeHookInventory.FoodAuthority], new object?[] { customer, M("PcPrefix"), null, null, null });
                patch.Invoke(instances[NativeHookInventory.FoodAuthority], new object?[] { hour, M("PcPrefix"), null, null, null });
                patch.Invoke(instances["cityperformance.mod"], new object?[] { tick, M("ModPrefix"), null, null, M("ModFinalizer") });
                patch.Invoke(instances["drivenrides.mod"], new object?[] { tick, M("ModPrefix"), null, null, null });
                patch.Invoke(instances["foodtrucks.mod"], new object?[] { tick, M("ModPrefix"), M("ModPostfix"), null, M("ModFinalizer") });
                var modpack = Load();
                Assert.Null(modpack.CheckoutRefusal);
                Assert.Equal(6, modpack.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)").Foreign.Count);

                patch.Invoke(instances["f8h.foreign"], new object?[] { customer, null, M("ForeignPostfix"), null, null });
                Assert.Null(Load().CheckoutRefusal);
                unpatch.Invoke(instances["f8h.foreign"], new object[] { "f8h.foreign" });

                patch.Invoke(instances["f8h.foreign"], new object?[] { customer, M("ForeignPrefix"), null, null, null });
                var refused = Load();
                Assert.Equal("foreign prefix by f8h.foreign on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)", refused.CheckoutRefusal);
                Assert.Equal(new[] { NativeHookInventory.FoodAuthority }, refused.Entries.Single(e => e.Signature == "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)").PcOwners);
                unpatch.Invoke(instances["f8h.foreign"], new object[] { "f8h.foreign" });

                patch.Invoke(instances["f8h.foreign"], new object?[] { hour, null, null, null, M("ForeignFinalizer") });
                Assert.Equal("foreign finalizer by f8h.foreign on RetailBusinessSimulator.ProcessAllCustomersFromThisHour()", Load().CheckoutRefusal);
            }
            finally { foreach (var pair in instances) unpatch.Invoke(pair.Value, new object[] { pair.Key }); }
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

namespace PersistentCity.Tests.F8hStandIn
{
    public sealed class CustomerEntry { }
    public sealed class GameManager { [MethodImpl(MethodImplOptions.NoInlining)] public void RunMainGameTick(float deltaTimeWithMultiplier) { } }
    public sealed class RetailBusinessSimulator
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public void ProcessAllCustomersFromThisHour() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void ProcessCustomer(CustomerEntry customerEntry) { }
    }
    /// <summary>One patch method per owner group: Harmony 2.4.2's UnpatchAll(id) removes by patch method.</summary>
    public static class Patches
    {
        public static void PcPrefix() { }
        public static void PcPostfix() { }
        public static void ModPrefix() { }
        public static void ModPostfix() { }
        public static Exception? ModFinalizer(Exception? __exception) => __exception;
        public static void ForeignPrefix() { }
        public static void ForeignPostfix() { }
        public static Exception? ForeignFinalizer(Exception? __exception) => __exception;
    }
}
