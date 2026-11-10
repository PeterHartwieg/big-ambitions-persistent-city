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
    /// <summary>F8c (docs/plans/f8-capability-boundaries.md D3): the food-economy native contract, verified once before
    /// the food-authority hooks are patched (food.authority, required) and before the market requests ownership
    /// (food.checkout.shop, optional). Reflection metadata only; nothing is patched, invoked or written here.</summary>
    internal static class NativeFoodContract
    {
        internal sealed class Result
        {
            internal Result(string? refusal, MemberInfo?[] members) { Refusal = refusal; Members = members; }
            /// <summary>Null when every member has its exact shape.</summary>
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

        /// <summary>food.authority: the six hook targets and the two BusinessSimulator fields. Checked once per process.</summary>
        internal static Result Authority => authority ??= Check(NativeFoodContractSpec.Authority(Types), "authority");
        /// <summary>food.checkout.shop: IsPriceAcceptable, ProcessCustomer and the three supply members. Checked once per process.</summary>
        internal static Result Checkout => checkout ??= Check(NativeFoodContractSpec.Checkout(Types), "checkout");

        /// <summary>The refusal of a contract already checked; null while unchecked or verified. Diagnostics only.</summary>
        internal static string? AuthorityRefusal => authority?.Refusal;

        /// <summary>The verified hook target; throws for a member the contract does not name.</summary>
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

        /// <summary>A checkout member that passed its own check, or null (each supply member stands alone, so a changed
        /// ProcessCustomer does not hide which supply fields still resolve).</summary>
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
    /// <summary>Test build only (tools/build.py --fault-points): PC_FAULT_POINT=&lt;arm&gt;:&lt;member&gt;:&lt;name|signature&gt;
    /// makes one named member expected under another name or with another parameter list (arms authority and checkout),
    /// and PC_FAULT_POINT=research:&lt;text&gt; makes ResearchBillingContract.Check report a member as changed.</summary>
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
