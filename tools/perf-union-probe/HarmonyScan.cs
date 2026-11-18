using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PerfUnionProbe
{
    public sealed class PatchStateWatch
    {
        private readonly Dictionary<MethodBase, byte[]> state;
        private readonly Dictionary<MethodBase, byte[]> seen = new Dictionary<MethodBase, byte[]>();
        private readonly Dictionary<MethodBase, byte[]> scanning = new Dictionary<MethodBase, byte[]>();

        public PatchStateWatch(Dictionary<MethodBase, byte[]> state) { this.state = state ?? throw new ArgumentNullException(nameof(state)); }

        public static PatchStateWatch Shared()
        {
            var type = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState", false);
            var field = type?.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null) is Dictionary<MethodBase, byte[]> state ? new PatchStateWatch(state) : null;
        }

        public long Checks { get; private set; }
        public long Changes { get; private set; }

        public void BeginScan()
        {
            lock (state)
            {
                scanning.Clear();
                foreach (var pair in state) scanning.Add(pair.Key, pair.Value);
            }
        }

        public void CommitScan()
        {
            seen.Clear();
            foreach (var pair in scanning) seen.Add(pair.Key, pair.Value);
            scanning.Clear();
        }

        public void Capture() { BeginScan(); CommitScan(); }

        public bool Changed()
        {
            Checks++;
            bool changed = false;
            lock (state)
            {
                if (state.Count != seen.Count) changed = true;
                else
                    foreach (var pair in state)
                        if (!seen.TryGetValue(pair.Key, out var known) || !ReferenceEquals(known, pair.Value)) { changed = true; break; }
            }
            if (changed) Changes++;
            return changed;
        }
    }

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
