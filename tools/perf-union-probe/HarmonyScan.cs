using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace PerfUnionProbe
{
    public sealed class PatchStateWatch
    {
        private readonly Dictionary<MethodBase, byte[]> state;
        private readonly Dictionary<MethodBase, byte[]> seen = new Dictionary<MethodBase, byte[]>();
        private readonly Dictionary<MethodBase, byte[]> scanning = new Dictionary<MethodBase, byte[]>();

        public PatchStateWatch(Dictionary<MethodBase, byte[]> state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            HoldsState = Holds;
        }

        public readonly Func<MethodBase, byte[], bool> HoldsState;

        public bool Holds(MethodBase method, byte[] bytes)
        {
            lock (state) return state.TryGetValue(method, out var live) && ReferenceEquals(live, bytes);
        }

        public static PatchStateWatch Shared()
        {
            var type = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState", false);
            var field = type?.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null) is Dictionary<MethodBase, byte[]> state ? new PatchStateWatch(state) : null;
        }

        private bool pending;

        public static bool Run(PatchStateWatch watch, bool full, Action scan)
        {
            if (watch != null && !full && !watch.pending && !watch.Changed()) return false;
            watch?.BeginScan();
            scan();
            watch?.CommitScan();
            return true;
        }

        public bool Pending => pending;
        public Dictionary<MethodBase, byte[]> Scanning => pending ? scanning : null;
        public long Checks { get; private set; }
        public long Changes { get; private set; }

        public void BeginScan()
        {
            pending = true;
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
            pending = false;
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

    public sealed class PatchScanCache
    {
        private sealed class Entry
        {
            public byte[] State;
            public ObservedPatch[] Observed;
            public long Pass;
        }

        private readonly Dictionary<MethodBase, Entry> entries = new Dictionary<MethodBase, Entry>();
        private readonly List<MethodBase> gone = new List<MethodBase>();
        private readonly List<ObservedPatch> read = new List<ObservedPatch>();
        private byte[][] needles = Array.Empty<byte[]>();
        private string needleKey;
        private long pass;

        public int Entries => entries.Count;
        public int Needles => needles.Length;
        public long Scans { get; private set; }
        public long Deserialized { get; private set; }
        public long Skipped { get; private set; }
        public long Reused { get; private set; }
        public int LastTargets { get; private set; }
        public int LastDeserialized { get; private set; }
        public int LastSkipped { get; private set; }
        public int LastReused { get; private set; }
        public int LastDrifted { get; private set; }
        public long Drifted { get; private set; }
        public long Rescans { get; private set; }

        public void Configure(IEnumerable<string> mentions)
        {
            var list = new List<string>();
            foreach (var mention in mentions ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(mention) && !list.Contains(mention)) list.Add(mention);
            list.Sort(StringComparer.Ordinal);
            string key = string.Join("\n", list);
            if (key == needleKey) return;
            needleKey = key;
            needles = new byte[list.Count][];
            for (int i = 0; i < list.Count; i++) needles[i] = Encoding.UTF8.GetBytes(list[i]);
            entries.Clear();
        }

        public bool Mentions(byte[] bytes)
        {
            if (bytes == null || needles.Length == 0) return true;
            for (int n = 0; n < needles.Length; n++)
                if (Contains(bytes, needles[n])) return true;
            return false;
        }

        public static bool Contains(byte[] haystack, byte[] needle)
        {
            int last = haystack.Length - needle.Length;
            byte first = needle[0];
            for (int i = 0; i <= last; i++)
            {
                if (haystack[i] != first) continue;
                int j = 1;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        public void Scan(Dictionary<MethodBase, byte[]> state, Action<MethodBase, List<ObservedPatch>> observe, List<ObservedPatch> into)
            => Scan(state, observe, null, into);

        public int ScanSettled(PatchStateWatch watch, Action<MethodBase, List<ObservedPatch>> observe, List<ObservedPatch> into, int attempts)
        {
            if (watch == null) throw new ArgumentNullException(nameof(watch));
            int drifted = 0;
            for (int attempt = 1; ; attempt++)
            {
                var snapshot = watch.Scanning;
                if (snapshot == null) throw new InvalidOperationException("the patch watch has no scan in progress");
                into.Clear();
                Scan(snapshot, observe, watch.HoldsState, into);
                if (LastDrifted == 0) return drifted;
                drifted += LastDrifted;
                if (attempt >= attempts) throw new InvalidOperationException("Harmony patch state changed during every scan attempt");
                Rescans++;
                watch.BeginScan();
            }
        }

        public void Scan(Dictionary<MethodBase, byte[]> state, Action<MethodBase, List<ObservedPatch>> observe, Func<MethodBase, byte[], bool> holds, List<ObservedPatch> into)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            pass++; Scans++;
            int targets = 0, deserialized = 0, skipped = 0, reused = 0, drifted = 0;
            foreach (var pair in state)
            {
                var method = pair.Key;
                if (method == null) continue;
                targets++;
                if (entries.TryGetValue(method, out var entry) && ReferenceEquals(entry.State, pair.Value) && pair.Value != null)
                {
                    entry.Pass = pass; reused++;
                    Append(entry.Observed, into);
                    continue;
                }
                if (entry == null) { entry = new Entry(); entries.Add(method, entry); }
                entry.Pass = pass; entry.State = null; entry.Observed = Array.Empty<ObservedPatch>();
                if (!Mentions(pair.Value)) { entry.State = pair.Value; skipped++; continue; }
                read.Clear();
                observe(method, read);
                deserialized++;
                if (holds != null && !holds(method, pair.Value))
                {
                    drifted++;
                    for (int i = 0; i < read.Count; i++) into.Add(read[i]);
                    continue;
                }
                if (read.Count > 0) entry.Observed = read.ToArray();
                entry.State = pair.Value;
                Append(entry.Observed, into);
            }
            if (entries.Count > targets)
            {
                gone.Clear();
                foreach (var pair in entries) if (pair.Value.Pass != pass) gone.Add(pair.Key);
                for (int i = 0; i < gone.Count; i++) entries.Remove(gone[i]);
                gone.Clear();
            }
            LastTargets = targets; LastDeserialized = deserialized; LastSkipped = skipped; LastReused = reused; LastDrifted = drifted;
            Deserialized += deserialized; Skipped += skipped; Reused += reused; Drifted += drifted;
        }

        private static void Append(ObservedPatch[] observed, List<ObservedPatch> into)
        {
            for (int i = 0; i < observed.Length; i++) into.Add(observed[i]);
        }
    }
}
