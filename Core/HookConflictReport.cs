using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistentCity.Core
{
    // F8e (docs/plans/f8-capability-boundaries.md D4): foreign Harmony owners on Persistent City's targets, classed by patch
    // kind. F8h: a policy (Runtime/HookConflicts.CheckoutPolicy) names the targets on which a foreign may-skip, rewrites or
    // swallows owner refuses food.checkout.shop; the refusal is a reason string, and nothing here unpatches or touches a registry.

    /// <summary>Harmony patch kinds as Harmony 2.4.2 reports them (Patches.Prefixes ... InnerPostfixes).</summary>
    public enum HookPatchKind : byte { Prefix, Postfix, Transpiler, Finalizer, InnerPrefix, InnerPostfix }

    /// <summary>D4: observer (postfix), may-skip (prefix that can return false), rewrites (transpiler, or an inner patch that
    /// changes calls inside the body), swallows (finalizer).</summary>
    public enum HookConflictClass : byte { Observer, MaySkip, Rewrites, Swallows }

    public readonly struct HookPatch
    {
        public HookPatch(string owner, HookPatchKind kind) { Owner = owner ?? ""; Kind = kind; }
        public string Owner { get; }
        public HookPatchKind Kind { get; }
    }

    /// <summary>One patched method as Harmony lists it: declaring type short name, method name, CLR short parameter names.</summary>
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
        /// <summary>Persistent City owners present on the method now (H1/H3/H4 on RunMainGameTick are not foreign).</summary>
        public IReadOnlyList<string> PcOwners { get; }
        /// <summary>Harmony lists the method as patched.</summary>
        public bool Patched { get; }
        public IReadOnlyList<(string Owner, HookPatchKind Kind, HookConflictClass Class)> Foreign { get; }
        /// <summary>F8h: "foreign &lt;kind&gt; by &lt;owner&gt; on &lt;signature&gt;" for the first foreign may-skip, rewrites or
        /// swallows owner when the policy covers this target; null otherwise (observers never refuse).</summary>
        public string? CheckoutRefusal { get; }
    }

    public sealed class HookConflictReport
    {
        private HookConflictReport(IReadOnlyList<HookConflictEntry> entries) { Entries = entries; }
        /// <summary>One entry per distinct inventory target method, in inventory order.</summary>
        public IReadOnlyList<HookConflictEntry> Entries { get; }
        public int ForeignOwners => Entries.SelectMany(e => e.Foreign.Select(f => f.Owner)).Distinct(StringComparer.Ordinal).Count();
        /// <summary>F8h: the food.checkout.shop refusal of the first refusing entry in inventory order, or null.</summary>
        public string? CheckoutRefusal => Entries.Select(e => e.CheckoutRefusal).FirstOrDefault(r => r != null);
        /// <summary>The Runtime scan's wall time, Harmony reads and this join together.</summary>
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

        /// <summary>Joins Harmony's patched methods to the F8a inventory by signature. Owners in the inventory are Persistent
        /// City's, never foreign; every other owner on an inventory target is foreign and classed by its patch kind. F8h: on
        /// a target named in <paramref name="checkoutPolicy"/> (signatures), a foreign owner that is not an observer refuses
        /// food.checkout.shop; without a policy nothing refuses.</summary>
        public static HookConflictReport Build(IEnumerable<HookPatchedMethod> patched, IEnumerable<string>? checkoutPolicy = null)
        {
            var policy = new HashSet<string>(checkoutPolicy ?? Array.Empty<string>(), StringComparer.Ordinal);
            var ours = new HashSet<string>(NativeHookInventory.Owners.Select(o => o.Id), StringComparer.Ordinal);
            var bySignature = new Dictionary<string, HookPatchedMethod>(StringComparer.Ordinal);
            foreach (var method in patched ?? throw new ArgumentNullException(nameof(patched)))
                bySignature[method.Signature] = method; // Harmony lists each method once
            var entries = new List<HookConflictEntry>();
            foreach (var group in NativeHookInventory.Targets.GroupBy(t => Key(t)))
            {
                var capabilities = group.Select(t => NativeCapabilities.Get(t.Capability).Name).Distinct(StringComparer.Ordinal).ToArray();
                // Harmony keeps a method listed after its last patch is removed; an empty patch list is not patched.
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

        /// <summary><c>pc capability hooks</c>: one line per inventory target method. Opus-r1-2: the marks come from this
        /// live scan; when the attached market's once-per-load decision (<paramref name="settled"/>,
        /// <paramref name="loadRefusal"/>) differs, a closing line says the marks apply at the next load.</summary>
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

        /// <summary>The once-per-load Player.log line: only targets that carry a foreign owner.</summary>
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
