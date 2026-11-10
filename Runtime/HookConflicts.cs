using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>F8e (docs/plans/f8-capability-boundaries.md D4): reads Harmony 2.4.2's patch metadata for every patched
    /// method (Harmony.GetAllPatchedMethods, then GetPatchInfo) and hands it to the Core report. Report only: it patches,
    /// unpatches and refuses nothing. Runs once at activation and on `pc capability hooks`.</summary>
    internal static class HookConflicts
    {
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
            var report = HookConflictReport.Build(methods);
            report.ScanMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return report;
        }

        private static void Add(List<HookPatch> into, IEnumerable<Patch>? patches, HookPatchKind kind)
        {
            if (patches == null) return;
            foreach (var patch in patches) into.Add(new HookPatch(patch.owner, kind));
        }
    }
}
