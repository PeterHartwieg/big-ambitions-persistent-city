using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistentCity.Core
{

    public enum HookPatchKind : byte { Prefix, Postfix, Transpiler, Finalizer, InnerPrefix, InnerPostfix }

    public enum HookConflictClass : byte { Observer, MaySkip, Rewrites, Swallows }

    public readonly struct HookPatch
    {
        public HookPatch(string owner, HookPatchKind kind) { Owner = owner ?? ""; Kind = kind; }
        public string Owner { get; }
        public HookPatchKind Kind { get; }
    }

    public sealed class HookPatchedMethod
    {
        public HookPatchedMethod(string type, string method, IReadOnlyList<string> parameters, IReadOnlyList<HookPatch> patches)
        { Type = type ?? ""; Method = method ?? ""; Parameters = parameters ?? Array.Empty<string>(); Patches = patches ?? Array.Empty<HookPatch>(); }
        public string Type { get; }
        public string Method { get; }
        public IReadOnlyList<string> Parameters { get; }
        public IReadOnlyList<HookPatch> Patches { get; }
        public string Signature => Type + "." + Method + "(" + string.Join(", ", Parameters) + ")";
    }

    public sealed class HookConflictEntry
    {
        internal HookConflictEntry(string signature, IReadOnlyList<string> capabilities, IReadOnlyList<string> pcOwners, bool patched,
            IReadOnlyList<(string Owner, HookPatchKind Kind, HookConflictClass Class)> foreign, string? checkoutRefusal)
        { Signature = signature; Capabilities = capabilities; PcOwners = pcOwners; Patched = patched; Foreign = foreign; CheckoutRefusal = checkoutRefusal; }
        public string Signature { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public IReadOnlyList<string> PcOwners { get; }
        public bool Patched { get; }
        public IReadOnlyList<(string Owner, HookPatchKind Kind, HookConflictClass Class)> Foreign { get; }
        public string? CheckoutRefusal { get; }
    }

    public sealed class HookConflictReport
    {
        private HookConflictReport(IReadOnlyList<HookConflictEntry> entries) { Entries = entries; }
        public IReadOnlyList<HookConflictEntry> Entries { get; }
        public int ForeignOwners => Entries.SelectMany(e => e.Foreign.Select(f => f.Owner)).Distinct(StringComparer.Ordinal).Count();
        public string? CheckoutRefusal => Entries.Select(e => e.CheckoutRefusal).FirstOrDefault(r => r != null);
        public double ScanMilliseconds { get; set; }

        public static HookConflictClass Classify(HookPatchKind kind) => kind switch
        {
            HookPatchKind.Postfix => HookConflictClass.Observer,
            HookPatchKind.Prefix => HookConflictClass.MaySkip,
            HookPatchKind.Finalizer => HookConflictClass.Swallows,
            _ => HookConflictClass.Rewrites,
        };

        public static string ClassWord(HookConflictClass value) => value switch
        {
            HookConflictClass.Observer => "observer", HookConflictClass.MaySkip => "may-skip", HookConflictClass.Rewrites => "rewrites", _ => "swallows",
        };

        private static string KindWord(HookPatchKind kind) => kind switch
        {
            HookPatchKind.Prefix => "prefix", HookPatchKind.Postfix => "postfix", HookPatchKind.Transpiler => "transpiler",
            HookPatchKind.Finalizer => "finalizer", HookPatchKind.InnerPrefix => "inner prefix", _ => "inner postfix",
        };

        public static HookConflictReport Build(IEnumerable<HookPatchedMethod> patched, IEnumerable<string>? checkoutPolicy = null)
        {
            var policy = new HashSet<string>(checkoutPolicy ?? Array.Empty<string>(), StringComparer.Ordinal);
            var ours = new HashSet<string>(NativeHookInventory.Owners.Select(o => o.Id), StringComparer.Ordinal);
            var bySignature = new Dictionary<string, HookPatchedMethod>(StringComparer.Ordinal);
            foreach (var method in patched ?? throw new ArgumentNullException(nameof(patched)))
                bySignature[method.Signature] = method;
            var entries = new List<HookConflictEntry>();
            foreach (var group in NativeHookInventory.Targets.GroupBy(t => Key(t)))
            {
                var capabilities = group.Select(t => NativeCapabilities.Get(t.Capability).Name).Distinct(StringComparer.Ordinal).ToArray();
                if (!bySignature.TryGetValue(group.Key, out var method) || method.Patches.Count == 0)
                {
                    entries.Add(new HookConflictEntry(group.Key, capabilities, Array.Empty<string>(), false, Array.Empty<(string, HookPatchKind, HookConflictClass)>(), null));
                    continue;
                }
                var pc = method.Patches.Where(p => ours.Contains(p.Owner)).Select(p => p.Owner).Distinct(StringComparer.Ordinal).ToArray();
                var foreign = method.Patches.Where(p => !ours.Contains(p.Owner))
                    .Select(p => (p.Owner, p.Kind, Classify(p.Kind))).Distinct()
                    .OrderBy(f => f.Owner, StringComparer.Ordinal).ThenBy(f => f.Kind).ToArray();
                string? refusal = null;
                if (policy.Contains(group.Key))
                    foreach (var f in foreign)
                        if (f.Item3 != HookConflictClass.Observer) { refusal = "foreign " + KindWord(f.Kind) + " by " + f.Owner + " on " + group.Key; break; }
                entries.Add(new HookConflictEntry(group.Key, capabilities, pc, true, foreign, refusal));
            }
            return new HookConflictReport(entries);
        }

        private static string Key(NativeHookTarget target)
            => target.Type + "." + target.Method + "(" + string.Join(", ", target.Parameters ?? Array.Empty<string>()) + ")";

        public string Text(bool settled, string? loadRefusal)
        {
            string text = Text();
            if (!settled || loadRefusal == CheckoutRefusal) return text;
            return text + "food.checkout.shop at this load: " + (loadRefusal == null ? "active" : "off: " + loadRefusal)
                + "; the marks above are the live scan and apply at the next load.\n";
        }

        public string Text()
        {
            var text = new StringBuilder("Persistent City hook conflicts: ")
                .Append(Entries.Count(e => e.Patched)).Append(" of ").Append(Entries.Count).Append(" target methods patched, ")
                .Append(Entries.Count(e => e.Foreign.Count > 0)).Append(" with foreign owners, ").Append(ForeignOwners).Append(" foreign owner(s), ")
                .Append(Entries.Count(e => e.CheckoutRefusal != null)).Append(" refusing food.checkout.shop\n");
            foreach (var entry in Entries)
            {
                text.Append(entry.Signature).Append(" [").Append(string.Join(", ", entry.Capabilities)).Append("]: ");
                if (!entry.Patched) text.Append("not patched");
                else if (entry.Foreign.Count == 0) text.Append("no foreign owner");
                else text.Append(string.Join("; ", entry.Foreign.Select(f => f.Owner + " " + KindWord(f.Kind) + " (" + ClassWord(f.Class) + ")")));
                if (entry.CheckoutRefusal != null) text.Append(" -> refuses food.checkout.shop");
                text.Append('\n');
            }
            return text.ToString();
        }

        public string ActivationLine()
        {
            var conflicted = Entries.Where(e => e.Foreign.Count > 0).ToArray();
            var text = new StringBuilder("Persistent City hook conflicts at activation (scan ")
                .Append(ScanMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(" ms): ");
            if (conflicted.Length == 0) return text.Append("no foreign owner on ").Append(Entries.Count(e => e.Patched)).Append(" patched targets.").ToString();
            text.Append(string.Join("; ", conflicted.Select(e => e.Signature + " " + string.Join(", ",
                e.Foreign.Select(f => f.Owner + " " + ClassWord(f.Class)))))).Append('.');
            if (CheckoutRefusal is string refusal) text.Append(" food.checkout.shop off: ").Append(refusal).Append('.');
            return text.Append(" Details: pc capability hooks.").ToString();
        }
    }
}
