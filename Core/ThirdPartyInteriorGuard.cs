using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Detached process-name evidence only. Never part of a saved city.</summary>
    public readonly struct InteriorGuardVerdict
    {
        public bool Detected { get; }
        public string Reason { get; }
        public InteriorGuardVerdict(bool detected, string reason) { Detected = detected; Reason = reason; }
        public bool AllowsView(bool requested) => requested && !Detected;
    }

    public static class ThirdPartyInteriorGuard
    {
        // Deliberately small rule language: exact names or a single trailing '*' prefix.
        // No regex, type loading, foreign API calls, or foreign implementation knowledge.
        public static InteriorGuardVerdict Evaluate(string mode, IReadOnlyList<string>? assemblyPatterns,
            IReadOnlyList<string>? ownerIds, IEnumerable<string> assemblyNames, IEnumerable<string> patchOwners)
        {
            if (mode == "off") return new InteriorGuardVerdict(false, "guard off");
            try
            {
                if (mode != "auto" || !ValidRules(assemblyPatterns, true) || !ValidRules(ownerIds, false))
                    return new InteriorGuardVerdict(true, "invalid configuration (fail-safe)");
                foreach (var name in assemblyNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException();
                    foreach (var pattern in assemblyPatterns!)
                    {
                        bool prefix = pattern[pattern.Length - 1] == '*';
                        bool match = prefix
                            ? name.Length >= pattern.Length - 1 && string.Compare(name, 0, pattern, 0,
                                pattern.Length - 1, StringComparison.OrdinalIgnoreCase) == 0
                            : string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
                        if (match) return new InteriorGuardVerdict(true, "assembly rule " + pattern + " matched " + name);
                    }
                }
                // With no configured owners, do not even enumerate metadata inputs.
                if (ownerIds!.Count > 0)
                    foreach (var owner in patchOwners)
                    {
                        if (string.IsNullOrWhiteSpace(owner)) throw new InvalidOperationException();
                        foreach (var rule in ownerIds)
                            if (string.Equals(owner, rule, StringComparison.Ordinal))
                                return new InteriorGuardVerdict(true, "Harmony owner rule " + rule);
                    }
                return new InteriorGuardVerdict(false, "not detected");
            }
            catch { return new InteriorGuardVerdict(true, "detection exception (fail-safe)"); }
        }

        private static bool ValidRules(IReadOnlyList<string>? rules, bool patterns)
        {
            if (rules == null || rules.Count > 16) return false;
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule) || rule.Length > 128 || rule != rule.Trim()) return false;
                int star = rule.IndexOf('*');
                if (star >= 0 && (!patterns || star == 0 || star != rule.Length - 1)) return false;
            }
            return true;
        }
    }
}
