using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AI.Customers.CustomerEntries;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

internal static partial class Program
{
    private static int checks;
    private static void Check(bool valid, string message) { if (!valid) throw new Exception("FAILED: " + message); checks++; }

    private static readonly Dictionary<string, Type> StubTypes = new(StringComparer.Ordinal)
    {
        ["GameManager"] = typeof(GameManager), ["RetailBusinessSimulator"] = typeof(Buildings.Retail.Simulation.RetailBusinessSimulator),
        ["CustomerEntriesHelper"] = typeof(CustomerEntriesHelper), ["BuildingRegistration"] = typeof(BuildingRegistration),
    };

    public static void Prefix() { }
    public static void Postfix() { }

    private static int Main()
    {
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", null);
        VerifiedContractPatchesExactTargets();
        RequiredRefusalPatchesNothingAndWritesNothing();
        FailedInstallRollsBack();
        CheckoutFaultLeavesAuthorityVerified();
        ResearchFaultReportsTheNamedMember();
        ChangedCheckoutMemberRedOnBaseGreenAfter();
        CheckoutStartup();
        ContractCheckTime();
        HookConflictArms();
        ConflictRefusalArms();
        Console.WriteLine(checks + " capability-contract checks passed (synthetic native surfaces and Harmony 2.4.2, not game execution).");
        return 0;
    }

    private static void VerifiedContractPatchesExactTargets()
    {
        var contract = NativeFoodContract.Authority;
        Check(contract.Refusal == null, "installed shape verifies: " + contract.Refusal);
        var harmony = new Harmony(NativeHookInventory.FoodAuthority);
        try
        {
            foreach (var target in NativeHookInventory.Targets.Where(t => t.Owner == NativeHookInventory.FoodAuthority))
            {
                var method = NativeFoodContract.AuthorityTarget(StubTypes[target.Type], target.Method);
                Check(method.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(target.Parameters!), target.Signature + " exact parameters");
                bool post = (target.Kinds & NativePatchKinds.Postfix) != 0;
                harmony.Patch(method, prefix: post ? null : new HarmonyMethod(typeof(Program), nameof(Prefix)), postfix: post ? new HarmonyMethod(typeof(Program), nameof(Postfix)) : null);
                Check(Harmony.GetPatchInfo(method)!.Owners.Contains(NativeHookInventory.FoodAuthority), target.Signature + " patched by food-authority");
            }
            Check(NativeFoodContract.AuthorityField("buildingRegistration").DeclaringType == typeof(Helpers.BusinessSimulation.BusinessSimulator), "inherited field resolves");
            Check(NativeFoodContract.AuthorityField("currentHour").FieldType == typeof(int), "hour field type");
        }
        finally { harmony.UnpatchAll(NativeHookInventory.FoodAuthority); }
        Check(!Harmony.HasAnyPatches(NativeHookInventory.FoodAuthority), "unpatched");
        Check(NativeFoodContract.Checkout.Refusal == null, "checkout verifies on the installed shape");
        Console.WriteLine("verified: 6 food-authority targets resolved with exact parameters and patched by the pinned Harmony; checkout verified");
    }

    private static void RequiredRefusalPatchesNothingAndWritesNothing()
    {
        var save = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SaveStore.StateKey] = "population-payload-bytes", [FoodAuthorityStore.Key] = "manifest-bytes", ["persistentcity.food-receipt.example"] = "receipt",
        };
        var before = save.ToArray();
        int arms = 0;
        foreach (var spec in NativeFoodContractSpec.Authority(NativeFoodContract.Types))
            foreach (var shape in new[] { "name", "signature" })
            {
                if (shape == "signature" && spec.Kind == NativeMemberKind.Field) continue;
                Environment.SetEnvironmentVariable("PC_FAULT_POINT", "authority:" + spec.Name + ":" + shape);
                var specs = NativeFoodContractSpec.Authority(NativeFoodContract.Types);
                FaultPoints.Apply(specs, "authority");
                string? refusal = NativeMemberContract.CheckAll(specs, out _);
                Check(refusal != null && refusal.Contains(spec.Name), spec.Label + " " + shape + " is named: " + refusal);
                Exception? thrown = null;
                try { InstallShape(specs, save); } catch (InvalidOperationException error) { thrown = error; }
                Check(thrown != null && thrown.Message.StartsWith("Persistent City did not start: food.authority is off: ", StringComparison.Ordinal), "named start refusal");
                Check(!Harmony.HasAnyPatches(NativeHookInventory.FoodAuthority), "no food-authority owner after refusal");
                Check(save.SequenceEqual(before), "every saved key byte-identical");
                arms++;
            }
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", null);
        Console.WriteLine("required refusal: " + arms + " fault arms (6 targets x name/signature, 2 fields x name) refused before any patch; saved keys unchanged");
    }

    private static void InstallShape(NativeMemberSpec[] specs, IDictionary<string, string> save)
    {
        var harmony = new Harmony(NativeHookInventory.FoodAuthority);
        NativeFoodContract.InstallChecked(NativeFoodContract.Verify(specs), () =>
        {
            save["install-body-ran"] = "yes";
            foreach (var spec in specs.Where(s => s.Kind == NativeMemberKind.Method))
                harmony.Patch(NativeMemberContract.Check(spec, out var m) == null ? (MethodInfo)m! : throw new InvalidOperationException(spec.Label), prefix: new HarmonyMethod(typeof(Program), nameof(Prefix)));
        }, () => harmony.UnpatchAll(NativeHookInventory.FoodAuthority));
        harmony.UnpatchAll(NativeHookInventory.FoodAuthority);
        save.Remove("install-body-ran");
    }

    private static void FailedInstallRollsBack()
    {
        var harmony = new Harmony(NativeHookInventory.FoodAuthority);
        bool threw = false;
        try
        {
            NativeFoodContract.InstallChecked(NativeFoodContract.Authority, () =>
            {
                harmony.Patch(NativeFoodContract.AuthorityTarget(typeof(GameManager), "RunMainGameTick"), postfix: new HarmonyMethod(typeof(Program), nameof(Postfix)));
                throw new InvalidOperationException("injected patch failure");
            }, () => harmony.UnpatchAll(NativeHookInventory.FoodAuthority));
        }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && !Harmony.HasAnyPatches(NativeHookInventory.FoodAuthority), "a failed install rolls back every patch");
        Console.WriteLine("install rollback: a failure after one patch leaves no food-authority owner");
    }

    private static void CheckoutFaultLeavesAuthorityVerified()
    {
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", "checkout:ProcessCustomer:signature");
        var checkout = NativeFoodContractSpec.Checkout(NativeFoodContract.Types); FaultPoints.Apply(checkout, "checkout");
        var authority = NativeFoodContractSpec.Authority(NativeFoodContract.Types); FaultPoints.Apply(authority, "authority");
        string? refusal = NativeMemberContract.CheckAll(checkout, out var members);
        Check(refusal != null && refusal.StartsWith("RetailBusinessSimulator.ProcessCustomer(CustomerEntry, Int32) changed", StringComparison.Ordinal), "checkout refusal: " + refusal);
        Check(members[NativeFoodContractSpec.ProcessCustomer] == null && members[NativeFoodContractSpec.Producers] != null && members[NativeFoodContractSpec.Capacity] != null, "supply members still resolve");
        Check(NativeMemberContract.CheckAll(authority, out _) == null, "authority unaffected by a checkout fault point");
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", null);
        Console.WriteLine("checkout fault: " + refusal + " -> " + NativeFoodContractSpec.CheckoutScopeReason(refusal!));
    }

    private static void ResearchFaultReportsTheNamedMember()
    {
        Check(FaultPoints.Research() == null, "no research fault by default");
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", "research:GameManager.ChangeMoneySafe");
        Check(FaultPoints.Research() == "GameManager.ChangeMoneySafe changed (PC_FAULT_POINTS).", "research fault text");
        Environment.SetEnvironmentVariable("PC_FAULT_POINT", null);
        Console.WriteLine("research fault: ResearchBillingContract.Check reports GameManager.ChangeMoneySafe changed (PC_FAULT_POINTS).");
    }

    private static void ChangedCheckoutMemberRedOnBaseGreenAfter()
    {
        var meal = new ResidentMeal { ResidentId = "r1", Day = 3, Kind = MealKind.Lunch, BusinessId = "business:x:generation:1",
            ProductId = FoodProducts.HotDog, Price = Money.FromCents(825) };
        var legacyData = new Dictionary<string, string>(StringComparer.Ordinal);
        var byName = AccessTools.Method(typeof(Changed.RetailBusinessSimulator), "ProcessCustomer");
        Check(byName != null, "base lookup by name finds the changed method");
        FoodSaleReceipts.Begin(legacyData, meal);
        bool threw = false;
        try { byName!.Invoke(new Changed.RetailBusinessSimulator(), new object[] { new CustomerEntry() }); }
        catch (TargetParameterCountException) { threw = true; }
        Check(threw && FoodSaleReceipts.ReadStatus(legacyData, meal) == FoodReceiptStatus.Pending, "base: uncertain receipt (red)");

        var types = NativeFoodContract.Types; types.RetailBusinessSimulator = typeof(Changed.RetailBusinessSimulator);
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        string? refusal = NativeMemberContract.CheckAll(NativeFoodContractSpec.Checkout(types), out _);
        if (refusal == null) FoodSaleReceipts.Begin(data, meal);
        Check(refusal != null && FoodSaleReceipts.ReadStatus(data, meal) == FoodReceiptStatus.Missing && Changed.RetailBusinessSimulator.Calls == 0,
            "after: refused at construction, no receipt, no native call (green)");
        Console.WriteLine("changed ProcessCustomer: base leaves receipt Pending (uncertain); F8c refuses before any receipt: " + refusal);
    }

    public static bool PricePrefix() => true;

    private static void CheckoutStartup()
    {
        var harmony = new Harmony(NativeHookInventory.FoodRoutines);
        var target = typeof(AI.Citizens.CitizenData).GetMethod("IsPriceAcceptable", new[] { typeof(string), typeof(float) })!;
        bool Install(FoodCheckoutMode mode) => NativeFoodContract.InstallPricePrefix(mode, () =>
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Program), nameof(PricePrefix)) { priority = Priority.First }));
        try
        {
            var verified = NativeFoodContract.StartCheckout(NativeFoodContract.Checkout, out var process);
            Check(verified.Serves && process != null && process.Name == "ProcessCustomer", "verified startup resolves ProcessCustomer");
            Check(Install(verified) && Harmony.HasAnyPatches(NativeHookInventory.FoodRoutines), "verified startup installs the price prefix");
            harmony.UnpatchAll(NativeHookInventory.FoodRoutines);

            Environment.SetEnvironmentVariable("PC_FAULT_POINT", "checkout:ProcessCustomer:signature");
            var specs = NativeFoodContractSpec.Checkout(NativeFoodContract.Types); FaultPoints.Apply(specs, "checkout");
            Environment.SetEnvironmentVariable("PC_FAULT_POINT", null);
            FoodCheckoutMode off = null!; MethodInfo? none = null; bool threw = false;
            try { off = NativeFoodContract.StartCheckout(NativeFoodContract.Verify(specs), out none); } catch { threw = true; }
            Check(!threw && off.Refusal != null, "checkout-off startup does not throw");
            Check(!Install(off) && !Harmony.HasAnyPatches(NativeHookInventory.FoodRoutines), "checkout-off installs no price prefix");
            Check(!off.Serves && none == null && !off.Observe(true) && off.ScopeReason!.StartsWith("Excluded: food checkout contract off: ", StringComparison.Ordinal),
                "checkout-off serves nothing, resolves no ProcessCustomer and observes every shop ineligible");
            var again = NativeFoodContract.StartCheckout(NativeFoodContract.Checkout, out _);
            Check(Install(again) && Harmony.HasAnyPatches(NativeHookInventory.FoodRoutines), "a later verified load installs the prefix again");
        }
        finally { harmony.UnpatchAll(NativeHookInventory.FoodRoutines); }
        Console.WriteLine("checkout startup: verified installs the price prefix; checkout-off starts without throwing, installs no prefix, serves nothing");
    }

    private static void ContractCheckTime()
    {
        var times = new double[2000];
        for (int i = -200; i < times.Length; i++)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            NativeMemberContract.CheckAll(NativeFoodContractSpec.Authority(NativeFoodContract.Types), out _);
            NativeMemberContract.CheckAll(NativeFoodContractSpec.Checkout(NativeFoodContract.Types), out _);
            if (i >= 0) times[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
        Array.Sort(times);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "contract check (13 members on stubs): p50={0:0.0000} ms p95={1:0.0000} ms max={2:0.0000} ms",
            times[times.Length / 2], times[times.Length * 95 / 100], times[^1]));
    }

    static partial void HookConflictArms();
    static partial void ConflictRefusalArms();
}
