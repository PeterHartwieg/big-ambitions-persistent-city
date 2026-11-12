using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class NativeInteriorGuard
    {
        public static InteriorGuardVerdict Verdict { get; private set; } = new InteriorGuardVerdict(true, "not evaluated");
        public static void Start(PrototypeSettings settings)
        {
            Verdict = ThirdPartyInteriorGuard.Evaluate(settings.ThirdPartyInteriorGuard,
                settings.ThirdPartyInteriorAssemblyPatterns, settings.ThirdPartyInteriorHarmonyOwnerIds,
                AssemblyNames(), PatchOwners());
        }
        public static void Stop() => Verdict = new InteriorGuardVerdict(true, "not evaluated");

        private static IEnumerable<string> AssemblyNames()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                yield return assembly.GetName().Name ?? throw new InvalidOperationException("Unnamed assembly.");
        }
        private static IEnumerable<string> PatchOwners()
        {
            foreach (string name in new[] { "EnterBuilding", "ExitFromBuilding" })
            {
                var method = typeof(BuildingManager).GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (method == null) throw new MissingMethodException(name);
                var info = Harmony.GetPatchInfo(method);
                if (info != null)
                    foreach (string owner in info.Owners) yield return owner;
            }
        }
    }
}
