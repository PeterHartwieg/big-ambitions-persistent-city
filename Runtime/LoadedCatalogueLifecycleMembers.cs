using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using HarmonyLib;
using Helpers;

namespace PersistentCity.Runtime
{
    /// <summary>Binds the Decision 1 hook inventory (<see cref="FoodContractHookTable"/>) by exact declaring type,
    /// name, static-ness and parameter types, and installs its postfixes. The postfixes are hints for the E5a
    /// reader, never a completeness proof: docs/plans/e5b-remaining.md section 2.2 lists the writers no hook sees.</summary>
    public static class LoadedCatalogueLifecycleMembers
    {
        private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["ItemsGetter"] = typeof(ItemsGetter), ["BusinessHelper"] = typeof(BusinessHelper),
            ["BuildingRegistration"] = typeof(BuildingRegistration), ["ItemHelper"] = typeof(ItemHelper),
            ["ItemInstance"] = typeof(ItemInstance), ["CargoInstance"] = typeof(CargoInstance),
            ["ReStockingHelper"] = typeof(ReStockingHelper),
        };
        private static readonly Dictionary<string, Type> Parameters = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["IList`1"] = typeof(IList<Item>), ["Item"] = typeof(Item), ["String"] = typeof(string),
            ["BuildingRegistration"] = typeof(BuildingRegistration), ["ItemInstance"] = typeof(ItemInstance),
            ["CargoInstance"] = typeof(CargoInstance), ["Boolean"] = typeof(bool), ["Int32"] = typeof(int),
        };

        public static MethodInfo? Bind(FoodContractHook hook, out string failure)
        {
            if (!Types.TryGetValue(hook.Type, out var type)) { failure = "Unknown declaring type " + hook.Type; return null; }
            var arguments = new Type[hook.Parameters.Count];
            for (int i = 0; i < arguments.Length; i++)
            {
                if (!Parameters.TryGetValue(hook.Parameters[i], out var argument)) { failure = "Unknown parameter type " + hook.Parameters[i]; return null; }
                arguments[i] = argument;
            }
            var method = type.GetMethod(hook.Method, All, null, arguments, null);
            if (method == null || method.DeclaringType != type || method.IsStatic != hook.IsStatic || method.IsGenericMethodDefinition)
            { failure = "Unverified native lifecycle member: " + hook.Signature; return null; }
            var actual = method.GetParameters();
            if (actual.Length != arguments.Length) { failure = "Signature changed: " + hook.Signature; return null; }
            for (int i = 0; i < actual.Length; i++)
                if (actual[i].ParameterType != arguments[i]) { failure = "Signature changed: " + hook.Signature; return null; }
            failure = ""; return method;
        }

        /// <summary>E5a's 19 members, in table order. Throws when any is not bound exactly.</summary>
        public static IReadOnlyList<MethodInfo> Discover()
        {
            var found = new List<MethodInfo>();
            for (int i = 0; i < FoodContractHookTable.E5aMembers; i++)
                found.Add(Bind(FoodContractHookTable.All[i], out var failure) ?? throw new NotSupportedException(failure));
            return found.AsReadOnly();
        }

        internal sealed class HarmonyPatcher : IFoodContractPatcher
        {
            internal const string PatchId = "persistentcity.food-contract";
            private readonly Harmony harmony = new Harmony(PatchId);
            public string? Patch(FoodContractHook hook)
            {
                var method = Bind(hook, out var failure);
                if (method == null) return failure;
                var postfix = typeof(Postfixes).GetMethod("Hook" + hook.Id, BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null) return "Postfix missing for " + hook.Signature;
                harmony.Patch(method, postfix: new HarmonyMethod(postfix) { priority = Priority.Last });
                return null;
            }
            public void UnpatchAll() => harmony.UnpatchAll(PatchId);
        }

        // One body per table id. Each reads only the owner's reference or address; none retains it.
        private static class Postfixes
        {
            private static void Hook0() => FoodContractHintRouter.Catalogue(0);
            private static void Hook1() => FoodContractHintRouter.Catalogue(1);
            private static void Hook2() => FoodContractHintRouter.Catalogue(2);
            private static void Hook3() => FoodContractHintRouter.Catalogue(3);
            private static void Hook4(BuildingRegistration __0) => FoodContractHintRouter.Registration(4, __0);
            private static void Hook5(BuildingRegistration __instance) => FoodContractHintRouter.Registration(5, __instance);
            private static void Hook6(BuildingRegistration __instance) => FoodContractHintRouter.Registration(6, __instance);
            private static void Hook7(BuildingRegistration __instance) => FoodContractHintRouter.Registration(7, __instance);
            private static void Hook8(BuildingRegistration __instance) => FoodContractHintRouter.Registration(8, __instance);
            private static void Hook9() => FoodContractHintRouter.Quantity(9);
            private static void Hook10() => FoodContractHintRouter.Quantity(10);
            private static void Hook11(ItemInstance __instance) => FoodContractHintRouter.Holder(11, __instance.streetName, __instance.streetNumber);
            private static void Hook12(ItemInstance __instance) => FoodContractHintRouter.Holder(12, __instance.streetName, __instance.streetNumber);
            private static void Hook13() => FoodContractHintRouter.Quantity(13);
            private static void Hook14() => FoodContractHintRouter.Quantity(14);
            private static void Hook15() => FoodContractHintRouter.Quantity(15);
            private static void Hook16() => FoodContractHintRouter.Quantity(16);
            private static void Hook17() => FoodContractHintRouter.Quantity(17);
            private static void Hook18() => FoodContractHintRouter.Quantity(18);
            private static void Hook19(ItemInstance __0) => FoodContractHintRouter.Holder(19, __0?.streetName, __0?.streetNumber ?? 0);
            private static void Hook20(BuildingRegistration __instance) => FoodContractHintRouter.Registration(20, __instance);
            private static void Hook21(BuildingRegistration __instance) => FoodContractHintRouter.Registration(21, __instance);
            private static void Hook22() => FoodContractHintRouter.Unattributed(22);
        }
    }
}
