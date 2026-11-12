using System;
using System.Reflection;
using AI.Citizens;
using AI.Customers.CustomerEntries;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Entities;
using Buildings.Retail.Simulation;
using Helpers.BusinessSimulation;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class NativeFoodContract
    {
        internal sealed class Result
        {
            internal Result(string? refusal, MemberInfo?[] members) { Refusal = refusal; Members = members; }
            internal string? Refusal { get; }
            internal MemberInfo?[] Members { get; }
        }

        private static Result? authority, checkout;

        internal static NativeFoodTypes Types => new NativeFoodTypes
        {
            GameManager = typeof(GameManager), RetailBusinessSimulator = typeof(RetailBusinessSimulator), BusinessSimulator = typeof(BusinessSimulator),
            CustomerEntriesHelper = typeof(CustomerEntriesHelper), CustomerEntry = typeof(CustomerEntry), BuildingRegistration = typeof(BuildingRegistration),
            Address = typeof(Address), DayOfWeek = typeof(DayOfWeekOrdered), CitizenData = typeof(CitizenData), ItemInstance = typeof(ItemInstance),
            PointOfSale = typeof(PointOfSale),
        };

        internal static Result Authority => authority ??= Check(NativeFoodContractSpec.Authority(Types), "authority");
        internal static Result Checkout => checkout ??= Check(NativeFoodContractSpec.Checkout(Types), "checkout");

        internal static Result CheckoutForLoad(bool loading, out bool settled)
        {
            settled = !loading;
            if (loading) { HookConflicts.TakeLoadScan(); return Checkout; }
            var decided = Decide(Checkout, HookConflicts.ScanForLoad);
            return WithConflict(decided, RloOverrule.CheckoutRefusal(null, RloOverrules.ReadForLoad()));
        }

        internal static Result Decide(Result members, Func<HookConflictReport> scan)
        {
            string? conflict;
            try { conflict = scan().CheckoutRefusal; }
            catch (Exception error) { conflict = "hook conflict scan failed: " + error.Message; }
            return WithConflict(members, conflict);
        }

        internal static Result WithConflict(Result members, string? conflict)
            => members.Refusal != null || conflict == null ? members : new Result(conflict, members.Members);

        internal static string? AuthorityRefusal => authority?.Refusal;

        internal static void InstallChecked(Result contract, Action install, Action rollback)
        {
            if (contract.Refusal != null) throw new InvalidOperationException(NativeFoodContractSpec.AuthorityStartMessage(contract.Refusal));
            try { install(); }
            catch { rollback(); throw; }
        }

        internal static Result Verify(NativeMemberSpec[] specs) => new Result(NativeMemberContract.CheckAll(specs, out var members), members);

        internal static MethodInfo AuthorityTarget(Type type, string method)
        {
            var result = Authority;
            if (result.Refusal != null) throw new InvalidOperationException(NativeFoodContractSpec.AuthorityStartMessage(result.Refusal));
            for (int i = 0; i < NativeFoodContractSpec.AuthorityTargets; i++)
                if (result.Members[i] is MethodInfo target && target.Name == method && target.DeclaringType == type) return target;
            throw new InvalidOperationException("food.authority contract does not name " + type.Name + "." + method + ".");
        }

        internal static FieldInfo AuthorityField(string name)
        {
            var result = Authority;
            if (result.Refusal != null) throw new InvalidOperationException(NativeFoodContractSpec.AuthorityStartMessage(result.Refusal));
            for (int i = NativeFoodContractSpec.AuthorityTargets; i < result.Members.Length; i++)
                if (result.Members[i] is FieldInfo field && field.Name == name) return field;
            throw new InvalidOperationException("food.authority contract does not name BusinessSimulator." + name + ".");
        }

        internal static FoodCheckoutMode StartCheckout(Result checkout, out MethodInfo? process)
        {
            var mode = new FoodCheckoutMode(checkout.Refusal);
            process = mode.Serves ? checkout.Members[NativeFoodContractSpec.ProcessCustomer] as MethodInfo : null;
            return mode;
        }

        internal static bool InstallPricePrefix(FoodCheckoutMode mode, Action install)
        {
            if (!mode.InstallsPricePrefix) return false;
            install();
            return true;
        }

        internal static T? CheckoutMember<T>(int index) where T : MemberInfo => Checkout.Members[index] as T;

        private static Result Check(NativeMemberSpec[] specs, string arm)
        {
#if PC_FAULT_POINTS
            FaultPoints.Apply(specs, arm);
#endif
            string? refusal = NativeMemberContract.CheckAll(specs, out var members);
            return new Result(refusal, members);
        }
    }

#if PC_FAULT_POINTS
    internal static class FaultPoints
    {
        internal static string Selected => Environment.GetEnvironmentVariable("PC_FAULT_POINT") ?? "";

        internal static void Apply(NativeMemberSpec[] specs, string arm)
        {
            var parts = Selected.Split(':');
            if (parts.Length != 3 || parts[0] != arm) return;
            for (int i = 0; i < specs.Length; i++)
                if (specs[i].Name == parts[1])
                    specs[i] = parts[2] == "signature" ? specs[i].WithExtraParameter(typeof(int)) : specs[i].Renamed(specs[i].Name + "Fault");
        }

        internal static string? Research()
        {
            var parts = Selected.Split(new[] { ':' }, 2);
            return parts.Length == 2 && parts[0] == "research" ? parts[1] + " changed (PC_FAULT_POINTS)." : null;
        }
    }
#endif
}
