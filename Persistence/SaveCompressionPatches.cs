using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public sealed class SaveCompressionPatch
    {
        public SaveCompressionPatch(string owner, HookPatchKind kind, string declaringType, string method, string returnType, params string[] parameters)
        {
            Owner = owner ?? ""; Kind = kind; DeclaringType = declaringType ?? ""; Method = method ?? ""; ReturnType = returnType ?? "";
            Parameters = parameters ?? Array.Empty<string>();
        }

        public string Owner { get; }
        public HookPatchKind Kind { get; }
        public string DeclaringType { get; }
        public string Method { get; }
        public string ReturnType { get; }
        public IReadOnlyList<string> Parameters { get; }

        public string Describe() => Owner + " " + Kind.ToString().ToLowerInvariant() + " " + DeclaringType + "." + Method
            + "(" + string.Join(", ", Parameters) + ")";

        public bool SameShape(SaveCompressionPatch other)
        {
            if (other == null || !string.Equals(Owner, other.Owner, StringComparison.Ordinal) || Kind != other.Kind
                || !string.Equals(DeclaringType, other.DeclaringType, StringComparison.Ordinal)
                || !string.Equals(Method, other.Method, StringComparison.Ordinal)
                || !string.Equals(ReturnType, other.ReturnType, StringComparison.Ordinal)
                || Parameters.Count != other.Parameters.Count) return false;
            for (int i = 0; i < Parameters.Count; i++)
                if (!string.Equals(Parameters[i], other.Parameters[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }

    public sealed class SaveCompressionPatchCheck
    {
        internal SaveCompressionPatchCheck(string? conflict, IReadOnlyList<string> accepted) { Conflict = conflict; Accepted = accepted; }
        public string? Conflict { get; }
        public IReadOnlyList<string> Accepted { get; }
    }

    public static class SaveCompressionPatches
    {
        public const string HandOffOwner = "persistentcity.save-handoff";
        public const string ConflictPrefix = "Another mod patches the native save compression: ";

        public static readonly SaveCompressionPatch HandOffPrefix = new SaveCompressionPatch(HandOffOwner, HookPatchKind.Prefix,
            "PersistentCity.Runtime.NativeSaveHandOff", "CompressPrefix", "System.Boolean",
            "System.String path", "System.String outputPath", "System.Boolean& __result");

        public static readonly SaveCompressionPatch StreetFoodBridgeObserver = new SaveCompressionPatch("foodtrucks.mod", HookPatchKind.Finalizer,
            "FoodTrucks.TestBridgeCommands", "CompressFinalizer", "System.Exception",
            "System.String path", "System.String outputPath", "System.Boolean __result", "System.Exception __exception");

        private static readonly SaveCompressionPatch[] Compatible = { StreetFoodBridgeObserver };

        public static SaveCompressionPatchCheck Check(IEnumerable<SaveCompressionPatch> patches, IEnumerable<string> owners)
        {
            if (patches == null) throw new ArgumentNullException(nameof(patches));
            var accepted = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int own = 0;
            foreach (var patch in patches)
            {
                seen.Add(patch.Owner);
                if (string.Equals(patch.Owner, HandOffOwner, StringComparison.Ordinal))
                {
                    if (!patch.SameShape(HandOffPrefix))
                        return new SaveCompressionPatchCheck("The save hand-off owner has an unexpected patch on the native save compression: " + patch.Describe() + ".", accepted);
                    own++;
                    continue;
                }
                if (!Known(patch)) return new SaveCompressionPatchCheck(ConflictPrefix + patch.Owner + " (" + patch.Describe() + ").", accepted);
                accepted.Add(patch.Describe());
            }
            if (owners != null)
                foreach (string owner in owners)
                    if (!seen.Contains(owner ?? ""))
                        return new SaveCompressionPatchCheck(ConflictPrefix + owner + " (a patch kind Persistent City cannot inspect).", accepted);
            if (own == 0) return new SaveCompressionPatchCheck("Save hand-off patch is missing on CompressBinaryData.", accepted);
            if (own > 1) return new SaveCompressionPatchCheck("The save hand-off prefix is installed more than once on CompressBinaryData.", accepted);
            return new SaveCompressionPatchCheck(null, accepted);
        }

        private static bool Known(SaveCompressionPatch patch)
        {
            foreach (var compatible in Compatible) if (patch.SameShape(compatible)) return true;
            return false;
        }
    }
}
