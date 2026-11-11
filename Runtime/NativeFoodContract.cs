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

        /// <summary>F8h (D4): the checkout result one city load uses: the member contract (<see cref="Checkout"/>) and then
        /// the conflict policy on this load's hook scan (HookConflicts.CheckoutPolicy). A foreign may-skip, rewrites or
        /// swallows owner on an H1 customer target or on H2 refuses food.checkout.shop with the owner named, through the
        /// same checkout-off mode as a changed member. While the game is still loading the city (<paramref name="loading"/>,
        /// review Sol-r1-1) nothing is scanned and <paramref name="settled"/> is false: another mod may still install its
        /// patches, so the market sells nothing and asks again after the load. Decided once, at the first call after it.</summary>
        internal static Result CheckoutForLoad(bool loading, out bool settled)
        {
            settled = !loading;
            if (loading) { HookConflicts.TakeLoadScan(); return Checkout; } // drop a scan from an earlier market
            return Decide(Checkout, HookConflicts.ScanForLoad);
        }

        /// <summary>The decision from a scan the caller supplies (the harness's throwing arm). A failed scan refuses
        /// (Opus-r1-1, fail closed): the owners could not be checked, so no shop is taken over on an unchecked hook.</summary>
        internal static Result Decide(Result members, Func<HookConflictReport> scan)
        {
            string? conflict;
            try { conflict = scan().CheckoutRefusal; }
            catch (Exception error) { conflict = "hook conflict scan failed: " + error.Message; }
            return WithConflict(members, conflict);
        }

        /// <summary>A member refusal comes first; a conflict refusal keeps the resolved members (StartCheckout drops
        /// ProcessCustomer while off, so nothing can invoke it).</summary>
        internal static Result WithConflict(Result members, string? conflict)
            => members.Refusal != null || conflict == null ? members : new Result(conflict, members.Members);

        /// <summary>The refusal of a contract already checked; null while unchecked or verified. Diagnostics only.</summary>
        internal static string? AuthorityRefusal => authority?.Refusal;

        /// <summary>The install order NativeFoodOwnership.Install uses: the contract is checked before <paramref name="install"/>
        /// runs; a refusal throws the named start refusal and runs nothing; a failure inside the body runs
        /// <paramref name="rollback"/> and rethrows.</summary>
        internal static void InstallChecked(Result contract, Action install, Action rollback)
        {
            if (contract.Refusal != null) throw new InvalidOperationException(NativeFoodContractSpec.AuthorityStartMessage(contract.Refusal));
            try { install(); }
            catch { rollback(); throw; }
        }

        /// <summary>Builds a result from specs the caller supplies (the harness's fault arms); production uses Authority.</summary>
        internal static Result Verify(NativeMemberSpec[] specs) => new Result(NativeMemberContract.CheckAll(specs, out var members), members);

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

        /// <summary>F8c checkout startup, shared by the NativeFoodMarket constructor and tools/capability-contracts: the mode a
        /// checkout result gives (a refusal never throws; it is checkout-off) and the ProcessCustomer method checkout may
        /// invoke (null while off).</summary>
        internal static FoodCheckoutMode StartCheckout(Result checkout, out MethodInfo? process)
        {
            var mode = new FoodCheckoutMode(checkout.Refusal);
            process = mode.Serves ? checkout.Members[NativeFoodContractSpec.ProcessCustomer] as MethodInfo : null;
            return mode;
        }

        /// <summary>Installs the resident price prefix only while checkout is on. Returns whether it ran.</summary>
        internal static bool InstallPricePrefix(FoodCheckoutMode mode, Action install)
        {
            if (!mode.InstallsPricePrefix) return false;
            install();
            return true;
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
