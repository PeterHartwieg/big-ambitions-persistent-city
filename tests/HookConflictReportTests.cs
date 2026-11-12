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
                Method("BuildingRegistration", "Reset", new[] { "Boolean" }),
                Method("UnrelatedType", "Unrelated", Array.Empty<string>(), ("other.mod", HookPatchKind.Prefix)),
                Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry", "Int32" }, ("other.mod", HookPatchKind.Prefix)),
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
            Assert.DoesNotContain(typeof(HookConflictReport).GetMethods(), m => m.GetParameters().Any(p => p.ParameterType == typeof(CapabilityRegistry)));
        }


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
            if (assembly == null) return;
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


        internal static readonly string[] ExpectedPolicy =
        {
            "RetailBusinessSimulator.ProcessAllCustomersFromThisHour()", "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)",
            "CustomerEntriesHelper.GetEntriesByAddress(Address)", "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)",
            "BuildingRegistration.Reset(Boolean)", "CitizenData.IsPriceAcceptable(String, Single)",
        };


        [Fact]
        public void CheckoutPolicyIsExactlyTheH1CustomerTargetsAndH2()
        {
            var policy = ExpectedPolicy;
            var inventory = NativeHookInventory.Targets.Select(t => t.Type + "." + t.Method + "(" + string.Join(", ", t.Parameters!) + ")").ToHashSet(StringComparer.Ordinal);
            Assert.All(policy, p => Assert.Contains(p, inventory));
            Assert.DoesNotContain("GameManager.RunMainGameTick(Single)", policy);
        }

        private static HookConflictReport Refusing(params HookPatchedMethod[] methods) => HookConflictReport.Build(methods, ExpectedPolicy);

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
                Assert.Null(HookConflictReport.Build(new[] { Method(target.Type, target.Method, target.Parameters!.ToArray(), ("other.mod", kind)) }).CheckoutRefusal);
            }
        }

        [Fact]
        public void ObserversAndOtherTargetsOnlyReport()
        {
            var observers = Refusing(ExpectedPolicy.Select(signature =>
            {
                var t = NativeHookInventory.Targets.First(x => x.Type + "." + x.Method + "(" + string.Join(", ", x.Parameters!) + ")" == signature);
                return Method(t.Type, t.Method, t.Parameters!.ToArray(), ("other.mod", HookPatchKind.Postfix));
            }).ToArray());
            Assert.Null(observers.CheckoutRefusal);
            Assert.Equal(6, observers.Entries.Count(e => e.Foreign.Count > 0));
            Assert.DoesNotContain("-> refuses", observers.Text());
            Assert.DoesNotContain("food.checkout.shop off", observers.ActivationLine());

            var modpack = Refusing(
                Method("GameManager", "RunMainGameTick", new[] { "Single" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Postfix), (NativeHookInventory.CityClock, HookPatchKind.Prefix),
                    (NativeHookInventory.ClockObserver, HookPatchKind.Postfix), ("cityperformance.mod", HookPatchKind.Prefix), ("cityperformance.mod", HookPatchKind.Finalizer),
                    ("drivenrides.mod", HookPatchKind.Prefix), ("foodtrucks.mod", HookPatchKind.Prefix), ("foodtrucks.mod", HookPatchKind.Postfix), ("foodtrucks.mod", HookPatchKind.Finalizer)),
                Method("ItemsGetter", "RegisterModItem", new[] { "Item" }, (NativeHookInventory.FoodContract, HookPatchKind.Postfix), ("hardmodepatch", HookPatchKind.Prefix)),
                Method("MouseController", "Run", Array.Empty<string>(), (NativeHookInventory.InspectorInput, HookPatchKind.Prefix), ("rig.input", HookPatchKind.Transpiler)),
                Method("PedestrianSpawner", "SetActivePedestrians", new[] { "Int32" }, (NativeHookInventory.AmbientPedestrians, HookPatchKind.Prefix), ("vehicle.mod", HookPatchKind.Prefix)),
                Method("RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry" }, (NativeHookInventory.FoodAuthority, HookPatchKind.Prefix)));
            Assert.Null(modpack.CheckoutRefusal);
            Assert.Equal(6, modpack.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)").Foreign.Count);
            Assert.All(modpack.Entries, e => Assert.Null(e.CheckoutRefusal));
            Assert.Contains(", 0 refusing food.checkout.shop\n", modpack.Text());
            Assert.Empty(modpack.Entries.Single(e => e.Signature == "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)").Foreign);
        }

        [Fact]
        public void TheRefusalIsDecidedOncePerLoadFromTheLoadScan()
        {
            var mode = new FoodCheckoutMode("foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)");
            Assert.False(mode.Serves); Assert.False(mode.InstallsPricePrefix); Assert.False(mode.Observe(true));
            var inputs = CapabilityReportTests.Healthy(); inputs.FoodCheckoutRefusal = mode.Refusal;
            var registry = CapabilitySnapshot.Build(inputs);
            Assert.Equal(CapabilityState.Off, registry.State(CapabilityId.FoodCheckoutShop));
            Assert.Equal(CapabilityStartStatus.Ready, registry.Start().Status);
            Assert.Contains("food.checkout.shop optional off: foreign prefix by other.mod on RetailBusinessSimulator.ProcessCustomer(CustomerEntry) | scope: shop ownership",
                new CapabilityReport().Status(registry, "x"));
        }

        [Fact]
        public void InventoryCountsAndCapabilityMapping()
        {
            Assert.Equal(10, NativeHookInventory.Owners.Count);
            Assert.Equal(42, NativeHookInventory.Targets.Count);
            Assert.Equal(NativeHookInventory.Owners.Count, NativeHookInventory.Owners.Select(o => o.Id).Distinct().Count());
            Assert.All(NativeHookInventory.Targets, t => Assert.NotNull(NativeHookInventory.Owner(t.Owner)));
            Assert.All(NativeHookInventory.Owners, o => Assert.Contains(NativeHookInventory.Targets, t => t.Owner == o.Id));
            Assert.Equal(NativeHookInventory.Targets.Count, NativeHookInventory.Targets
                .Select(t => t.Owner + " " + t.Signature).Distinct().Count());
            Assert.All(NativeHookInventory.Targets, t => Assert.NotEqual(NativePatchKinds.None, t.Kinds));

            string Map(string owner) => string.Join(",", NativeHookInventory.Targets.Where(t => t.Owner == owner)
                .Select(t => NativeCapabilities.Get(t.Capability).Name).Distinct());
            Assert.Equal("food.authority", Map(NativeHookInventory.FoodAuthority));
            Assert.Equal("food.checkout.shop", Map(NativeHookInventory.FoodRoutines));
            Assert.Equal("food.dynamic", Map(NativeHookInventory.FoodContract));
            Assert.Equal("clock.city,clock.drivenrides", Map(NativeHookInventory.CityClock));
            Assert.Equal("clock.observe", Map(NativeHookInventory.ClockObserver));
            Assert.Equal("research.billing", Map(NativeHookInventory.ResearchBilling));
            Assert.Equal("research.dialog", Map(NativeHookInventory.ResearchDialog));
            Assert.DoesNotContain(NativeHookInventory.Targets, t => t.Capability == CapabilityId.PhysicalVehicles);
            Assert.Equal("physical.pedestrians", Map(NativeHookInventory.AmbientPedestrians));
            Assert.Equal("physical.pedestrians", Map(NativeHookInventory.StationaryCrowds));
            Assert.Equal("inspector.click,inspector.scroll,inspector.uiblock", Map(NativeHookInventory.InspectorInput));

            Assert.Equal(new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, NativeHookInventory.ClockObserver },
                NativeHookInventory.Targets.Where(t => t.Type == "GameManager" && t.Method == "RunMainGameTick").Select(t => t.Owner));
        }

        [Fact]
        public void RealHarmonyForeignOwnersRefuseCheckoutByPolicy()
        {
            var assembly = Harmony();
            if (assembly == null) return;
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
            HookConflictReport Load() => HookConflictReport.Build(Scan(harmony), ExpectedPolicy);
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
