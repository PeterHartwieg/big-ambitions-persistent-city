using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PerfUnionProbe
{
    public static class HarmonyScan
    {
        public static readonly IReadOnlyList<FieldInfo> PatchLists = Lists();

        private static FieldInfo[] Lists()
        {
            var lists = new List<FieldInfo>();
            foreach (var field in typeof(Patches).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (typeof(IEnumerable<Patch>).IsAssignableFrom(field.FieldType)) lists.Add(field);
            return lists.ToArray();
        }

        public static string Kind(string field) => field == "Prefixes" ? "Prefix" : field == "Postfixes" ? "Postfix"
            : field == "Finalizers" ? "Finalizer" : field == "Transpilers" ? "Transpiler" : field;

        public static void Observe(MethodBase target, Patches info, Assembly pc, List<ObservedPatch> into)
        {
            if (info == null) return;
            string targetName = target.DeclaringType?.Name + "." + target.Name;
            foreach (var field in PatchLists)
            {
                if (!(field.GetValue(info) is IEnumerable<Patch> list)) continue;
                string kind = Kind(field.Name);
                foreach (var patch in list)
                {
                    var method = patch.PatchMethod;
                    into.Add(new ObservedPatch { Owner = patch.owner, Kind = kind, Type = method?.DeclaringType?.FullName, Method = method?.Name,
                        Target = targetName, PcAssembly = pc != null && method?.DeclaringType?.Assembly == pc });
                }
            }
        }
    }
}
