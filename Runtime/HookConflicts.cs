using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>F8e (docs/plans/f8-capability-boundaries.md D4): reads Harmony 2.4.2's patch metadata for every patched
    /// method (Harmony.GetAllPatchedMethods, then GetPatchInfo) and hands it to the Core report. It patches and unpatches
    /// nothing. F8h: the report names the food.checkout.shop refusal of <see cref="CheckoutPolicy"/>; NativeFoodContract
    /// applies it once per city load (the market's construction) and the activation line reuses that scan.</summary>
    internal static class HookConflicts
    {
        /// <summary>F8h policy table (D4, rig check 1 records docs/checks/f8h-check1-modpack-owners-pc.json and -mac.json):
        /// a foreign may-skip, rewrites or swallows owner on one of these refuses food.checkout.shop; a foreign observer does
        /// not. The five H1 customer targets and H2. GameManager.RunMainGameTick is shared with other mods (the check-1
        /// modpack carries foreign prefixes and finalizers there) and, like every other target, stays report-only.</summary>
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

        /// <summary>F8h: the scan the checkout decision of this city load uses, kept for the activation line.</summary>
        internal static HookConflictReport ScanForLoad()
        {
            loadScan = null; // a failed scan leaves no older one for the activation line
            return loadScan = Scan();
        }

        /// <summary>The load scan once, for the activation line (null when no market scanned since the last take).</summary>
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

        /// <summary>The metadata part of the capability report's inputs, shared by Runtime and the harness that times it:
        /// one pass over the loaded assemblies (Street Food, Driven Rides), one over Harmony's patched methods (inspector
        /// guards) and the two owner checks (food authority, food-routines price prefix). Reads metadata only.</summary>
        internal static void ReadMetadata(CapabilityInputs into)
        {
            bool streetFood = false, drivenRides = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                streetFood |= assembly.FullName?.StartsWith("FoodTrucks,", StringComparison.Ordinal) == true; // no AssemblyName per assembly
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

        /// <summary>"Type.Method" of every method <paramref name="owner"/> has a patch on: one pass over Harmony's
        /// patched methods (the capability report reads the inspector guards from it). Metadata only.</summary>
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
