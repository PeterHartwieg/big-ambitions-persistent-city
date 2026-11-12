using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class HookConflicts
    {
        internal static readonly string[] CheckoutPolicy =
        {
            "RetailBusinessSimulator.ProcessAllCustomersFromThisHour()",
            "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)",
            "CustomerEntriesHelper.GetEntriesByAddress(Address)",
            "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)",
            "BuildingRegistration.Reset(Boolean)",
            "CitizenData.IsPriceAcceptable(String, Single)",
        };

        private static HookConflictReport? loadScan;

        internal static HookConflictReport ScanForLoad()
        {
            loadScan = null;
            return loadScan = Scan();
        }

        internal static HookConflictReport? TakeLoadScan()
        {
            var scan = loadScan;
            loadScan = null;
            return scan;
        }

        internal static HookConflictReport Scan()
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            var methods = new List<HookPatchedMethod>();
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                var patches = new List<HookPatch>();
                Add(patches, info.Prefixes, HookPatchKind.Prefix);
                Add(patches, info.Postfixes, HookPatchKind.Postfix);
                Add(patches, info.Transpilers, HookPatchKind.Transpiler);
                Add(patches, info.Finalizers, HookPatchKind.Finalizer);
                Add(patches, info.InnerPrefixes, HookPatchKind.InnerPrefix);
                Add(patches, info.InnerPostfixes, HookPatchKind.InnerPostfix);
                methods.Add(new HookPatchedMethod(method.DeclaringType?.Name ?? "", method.Name,
                    method.GetParameters().Select(p => p.ParameterType.Name).ToArray(), patches));
            }
            var report = HookConflictReport.Build(methods, CheckoutPolicy);
            report.ScanMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return report;
        }

        internal static void ReadMetadata(CapabilityInputs into)
        {
            bool streetFood = false, drivenRides = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                streetFood |= assembly.FullName?.StartsWith("FoodTrucks,", StringComparison.Ordinal) == true;
                drivenRides = drivenRides || assembly.GetType("DrivenRides.Ride", false) != null;
            }
            var inspector = OwnedTargets(NativeHookInventory.InspectorInput);
            into.StreetFoodLoaded = streetFood; into.DrivenRidesLoaded = drivenRides;
            into.InspectorClickInstalled = inspector.Contains("MouseController.Run");
            into.InspectorScrollInstalled = inspector.Contains("PedestrianCam.UpdateInput");
            into.InspectorUiBlockInstalled = inspector.Contains("GraphicRaycaster.Raycast");
            into.FoodAuthorityInstalled = Harmony.HasAnyPatches(NativeHookInventory.FoodAuthority);
            into.FoodPricePrefixInstalled = Harmony.HasAnyPatches(NativeHookInventory.FoodRoutines);
        }

        internal static HashSet<string> OwnedTargets(string owner)
        {
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info != null && info.Owners.Contains(owner)) targets.Add((method.DeclaringType?.Name ?? "") + "." + method.Name);
            }
            return targets;
        }

        private static void Add(List<HookPatch> into, IEnumerable<Patch>? patches, HookPatchKind kind)
        {
            if (patches == null) return;
            foreach (var patch in patches) into.Add(new HookPatch(patch.owner, kind));
        }
    }
}
