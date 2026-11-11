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

/// <summary>F8h arm (D4 policy): NativeFoodContract.CheckoutForLoad on Runtime/HookConflicts.ScanForLoad with the pinned
/// Harmony 2.4.2 on the stand-in methods. A foreign may-skip, rewrites or swallows owner on an H1 customer target or on H2
/// refuses food.checkout.shop with the owner named; a foreign observer does not; the check-1 modpack owners on
/// GameManager.RunMainGameTick do not. Synthetic native surfaces, not game execution.</summary>
internal static partial class Program
{
    private const string Foreign = "f8h.foreign";

    // The foreign owner's own patch methods: Harmony's UnpatchAll(id) removes by patch method, so sharing Program.Prefix
    // with the PC owners would strip their patches too.
    public static void ForeignPrefix() { }
    public static void ForeignPostfix() { }
    public static IEnumerable<CodeInstruction> ForeignTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;
    public static Exception? ForeignFinalizer(Exception? __exception) => __exception;

    static partial void ConflictRefusalArms()
    {
        var tick = Stub(typeof(GameManager), "RunMainGameTick");
        var policyTargets = new (MethodInfo Method, string Signature)[]
        {
            (Stub(typeof(Buildings.Retail.Simulation.RetailBusinessSimulator), "ProcessAllCustomersFromThisHour"), "RetailBusinessSimulator.ProcessAllCustomersFromThisHour()"),
            (Stub(typeof(Buildings.Retail.Simulation.RetailBusinessSimulator), "ProcessCustomer"), "RetailBusinessSimulator.ProcessCustomer(CustomerEntry)"),
            (Stub(typeof(CustomerEntriesHelper), "GetEntriesByAddress"), "CustomerEntriesHelper.GetEntriesByAddress(Address)"),
            (Stub(typeof(CustomerEntriesHelper), "UpdateCustomerEntriesForPlayerBusiness"), "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)"),
            (Stub(typeof(BuildingRegistration), "Reset"), "BuildingRegistration.Reset(Boolean)"),
            (Stub(typeof(AI.Citizens.CitizenData), "IsPriceAcceptable"), "CitizenData.IsPriceAcceptable(String, Single)"),
        };
        Check(policyTargets.Select(t => t.Signature).SequenceEqual(HookConflicts.CheckoutPolicy), "the policy is exactly the five H1 customer targets and H2");
        Check(!HookConflicts.CheckoutPolicy.Contains("GameManager.RunMainGameTick(Single)"), "RunMainGameTick stays report-only");

        var owners = new[] { NativeHookInventory.FoodAuthority, NativeHookInventory.CityClock, NativeHookInventory.ClockObserver, NativeHookInventory.FoodRoutines,
            "cityperformance.mod", "drivenrides.mod", "foodtrucks.mod", Foreign };
        var harmonies = owners.ToDictionary(o => o, o => new Harmony(o));
        HarmonyMethod Pre() => new HarmonyMethod(typeof(Program), nameof(Prefix));
        HarmonyMethod Post() => new HarmonyMethod(typeof(Program), nameof(Postfix));
        HarmonyMethod Fin() => new HarmonyMethod(typeof(Program), nameof(Finalizer));
        HarmonyMethod FPre() => new HarmonyMethod(typeof(Program), nameof(ForeignPrefix));
        HarmonyMethod FPost() => new HarmonyMethod(typeof(Program), nameof(ForeignPostfix));
        HarmonyMethod FTrans() => new HarmonyMethod(typeof(Program), nameof(ForeignTranspiler));
        HarmonyMethod FFin() => new HarmonyMethod(typeof(Program), nameof(ForeignFinalizer));
        try
        {
            // Persistent City's own owners as a load installs them before the market: never foreign, never refusing.
            harmonies[NativeHookInventory.FoodAuthority].Patch(tick, postfix: Post());
            harmonies[NativeHookInventory.CityClock].Patch(tick, prefix: Pre());
            harmonies[NativeHookInventory.ClockObserver].Patch(tick, postfix: Post());
            foreach (var (method, _) in policyTargets.Take(5)) harmonies[NativeHookInventory.FoodAuthority].Patch(method, prefix: Pre());
            Check(Load(out var dbg) == null, "PC owners alone refuse nothing");

            // Rig check 1 shape (both rigs, b3e5a910): may-skip prefixes and swallowing finalizers on RunMainGameTick.
            harmonies["cityperformance.mod"].Patch(tick, prefix: Pre(), finalizer: Fin());
            harmonies["drivenrides.mod"].Patch(tick, prefix: Pre());
            harmonies["foodtrucks.mod"].Patch(tick, prefix: Pre(), postfix: Post(), finalizer: Fin());
            var modpack = Load(out var modpackScan);
            Check(modpack == null && modpackScan.Entries.Single(e => e.Signature == "GameManager.RunMainGameTick(Single)").Foreign.Count == 6,
                "check-1 owners on RunMainGameTick are reported and refuse nothing: " + modpack);
            Check(!modpackScan.Text().Contains("-> refuses", StringComparison.Ordinal), "no entry marked refusing under the check-1 modpack");

            // A foreign observer on every policy target does not refuse.
            foreach (var (method, _) in policyTargets) harmonies[Foreign].Patch(method, postfix: FPost());
            Check(Load(out _) == null, "foreign postfixes (observer) on all six policy targets refuse nothing");
            harmonies[Foreign].UnpatchAll(Foreign);

            // A foreign prefix (may-skip) on each policy target refuses with the owner and target named.
            foreach (var (method, signature) in policyTargets)
            {
                harmonies[Foreign].Patch(method, prefix: FPre());
                Check(Load(out _) == "foreign prefix by " + Foreign + " on " + signature, "foreign prefix on " + signature + " refuses");
                harmonies[Foreign].UnpatchAll(Foreign);
            }
            // A foreign transpiler (rewrites) and a foreign finalizer (swallows) on H1 customer targets each refuse.
            harmonies[Foreign].Patch(policyTargets[2].Method, transpiler: FTrans());
            Check(Load(out _) == "foreign transpiler by " + Foreign + " on CustomerEntriesHelper.GetEntriesByAddress(Address)", "foreign transpiler refuses");
            harmonies[Foreign].UnpatchAll(Foreign);
            harmonies[Foreign].Patch(policyTargets[0].Method, finalizer: FFin());
            Check(Load(out _) == "foreign finalizer by " + Foreign + " on RetailBusinessSimulator.ProcessAllCustomersFromThisHour()", "foreign finalizer refuses");
            harmonies[Foreign].UnpatchAll(Foreign);

            // Sol-r1-1 lifecycle: population preparation finishes and the market is built while the game is still loading
            // the city; another mod installs its ProcessCustomer prefix later in the same load. Red (round-1 code, decided
            // at construction): the scan before the patch keeps checkout on for the whole load. Green: nothing is decided
            // or sold while loading; the first call after the load scans, refuses with the owner named, sells nothing.
            {
                var lifecycleMeal = new ResidentMeal { ResidentId = "r-load", Day = 3, Kind = MealKind.Lunch, BusinessId = "business:x:generation:1",
                    ProductId = FoodProducts.HotDog, Price = Money.FromCents(825) };
                var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
                var latched = NativeFoodContract.Decide(NativeFoodContract.Checkout, HookConflicts.Scan);
                Check(latched.Refusal == null, "red: a decision taken at construction, before the late patch, leaves checkout on");
                var built = NativeFoodContract.StartCheckout(NativeFoodContract.CheckoutForLoad(true, out bool settled), out _);
                Check(!settled && built.Refusal == null && HookConflicts.TakeLoadScan() == null, "while loading: undecided, nothing scanned");
                harmonies[Foreign].Patch(policyTargets[1].Method, prefix: FPre()); // the late city-load patch
                // The market's gates while undecided (BuyLive and CanServe return before any receipt).
                if (settled && built.Serves) FoodSaleReceipts.Begin(receipts, lifecycleMeal);
                NativeFoodContract.CheckoutForLoad(true, out bool stillLoading);
                Check(!stillLoading, "a frame still inside the load stays undecided");
                var final = NativeFoodContract.StartCheckout(NativeFoodContract.CheckoutForLoad(false, out bool done), out var finalProcess);
                if (done && final.Serves) FoodSaleReceipts.Begin(receipts, lifecycleMeal);
                Check(done && final.Refusal == "foreign prefix by " + Foreign + " on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)" && finalProcess == null
                    && !final.Observe(true), "green: decided after the load, refused with the owner named, every shop observed ineligible (existing withdrawal)");
                Check(receipts.Count == 0, "no receipt before or after the late decision");
                // Sol-r2-2: the deferred activation lines run after this decision and take its scan (no second scan).
                var deciding = HookConflicts.TakeLoadScan();
                Check(deciding?.CheckoutRefusal == final.Refusal && deciding!.ActivationLine().Contains("food.checkout.shop off: " + final.Refusal + ".", StringComparison.Ordinal),
                    "the activation line gets the deciding scan and shows the final refusal");
                harmonies[Foreign].UnpatchAll(Foreign);
                Console.WriteLine("late city-load patch: undecided while loading, refused after the load with no receipt; a construction-time decision kept checkout on (red)");
            }

            // Opus-r1-1: a scan that throws refuses (fail closed) with its reason; a member refusal stays first.
            var failed = NativeFoodContract.Decide(NativeFoodContract.Checkout, () => throw new InvalidOperationException("injected scan failure"));
            Check(failed.Refusal == "hook conflict scan failed: injected scan failure" && !NativeFoodContract.StartCheckout(failed, out _).Serves, "a failed scan refuses checkout with its reason");
            var changed = NativeFoodContract.Verify(new[] { NativeMemberSpec.Method(typeof(GameManager), "Missing", typeof(void), false) });
            Check(NativeFoodContract.Decide(changed, () => throw new InvalidOperationException("x")).Refusal == changed.Refusal, "a member refusal comes before a scan refusal");

            // Plan check 10 shape: a foreign prefix on ProcessCustomer. Red on the base code (the checkout decision read the
            // member contract only, so the market served and a sale began a receipt); green: checkout-off, no receipt.
            harmonies[Foreign].Patch(policyTargets[1].Method, prefix: FPre());
            var meal = new ResidentMeal { ResidentId = "r1", Day = 3, Kind = MealKind.Lunch, BusinessId = "business:x:generation:1",
                ProductId = FoodProducts.HotDog, Price = Money.FromCents(825) };
            var baseMode = NativeFoodContract.StartCheckout(NativeFoodContract.Checkout, out var baseProcess);
            var baseData = new Dictionary<string, string>(StringComparer.Ordinal);
            if (baseMode.Serves && baseProcess != null) FoodSaleReceipts.Begin(baseData, meal);
            Check(baseMode.Serves && FoodSaleReceipts.ReadStatus(baseData, meal) == FoodReceiptStatus.Pending, "base: a foreign prefix on ProcessCustomer still serves and begins a receipt (red)");

            var mode = NativeFoodContract.StartCheckout(NativeFoodContract.CheckoutForLoad(false, out _), out var process);
            var scan = HookConflicts.TakeLoadScan()!;
            const string Expected = "foreign prefix by " + Foreign + " on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)";
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            if (mode.Serves && process != null) FoodSaleReceipts.Begin(data, meal);
            Check(mode.Refusal == Expected && !mode.Serves && process == null && !mode.InstallsPricePrefix && !mode.Observe(true),
                "after: checkout-off with the owner named, no ProcessCustomer, no price prefix, every shop observed ineligible (green)");
            Check(FoodSaleReceipts.ReadStatus(data, meal) == FoodReceiptStatus.Missing && !data.Keys.Any(FoodSaleReceipts.IsReceiptKey), "no receipt after the refusal");
            Check(mode.ScopeReason == NativeFoodContractSpec.CheckoutScopeReason(Expected), "shops show the refusal as their scope reason");
            // pc capability status, hooks and the activation line.
            var inputs = new CapabilityInputs { CityActive = true, FoodMarketAttached = true, FoodCheckoutRefusal = mode.Refusal };
            string status = new CapabilityReport().Status(CapabilitySnapshot.Build(inputs), "x", CapabilitySnapshot.Context(inputs));
            Check(status.Contains("food.checkout.shop optional off: " + Expected + " | scope: shop ownership", StringComparison.Ordinal), "status names the refusal: " + status);
            Check(scan.Text().Contains("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) [food.authority]: " + Foreign + " prefix (may-skip) -> refuses food.checkout.shop\n", StringComparison.Ordinal),
                "pc capability hooks marks the refusing entry");
            Check(scan.ActivationLine().Contains("food.checkout.shop off: " + Expected + ".", StringComparison.Ordinal), "activation line names the refusal");
            Check(!scan.Text().Contains("compatible", StringComparison.OrdinalIgnoreCase), "never says compatible");
            Check(scan.Text(true, mode.Refusal) == scan.Text() && scan.Text(false, null) == scan.Text(), "hooks adds no note when the load decision matches or none is made");
            Check(scan.Text(true, null).EndsWith("food.checkout.shop at this load: active; the marks above are the live scan and apply at the next load.\n", StringComparison.Ordinal),
                "hooks says when the live marks differ from this load's decision");
            Check(HookConflicts.TakeLoadScan() == null, "the load scan is taken once");
            Console.WriteLine("conflict refusal: " + Expected + " -> checkout-off, no receipt; base served and began a receipt (red)");

            // Refusal-check time beside the F8e scan, same patched state (the check-1 owners plus the refusing prefix).
            var scanTimes = new double[500];
            var checkTimes = new double[500];
            for (int i = -50; i < scanTimes.Length; i++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                _ = HookConflicts.Scan();
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                _ = NativeFoodContract.CheckoutForLoad(false, out _);
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (i >= 0)
                {
                    scanTimes[i] = (t1 - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    checkTimes[i] = (t2 - t1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                }
            }
            HookConflicts.TakeLoadScan();
            Array.Sort(scanTimes); Array.Sort(checkTimes);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "conflict refusal timing ({0} patched targets): F8e scan p50={1:0.0000} ms p95={2:0.0000} ms max={3:0.0000} ms; CheckoutForLoad (scan + policy) p50={4:0.0000} ms p95={5:0.0000} ms max={6:0.0000} ms",
                scan.Entries.Count(e => e.Patched), scanTimes[250], scanTimes[475], scanTimes[^1], checkTimes[250], checkTimes[475], checkTimes[^1]));
        }
        finally { foreach (var pair in harmonies) pair.Value.UnpatchAll(pair.Key); HookConflicts.TakeLoadScan(); }
        Check(Harmony.GetAllPatchedMethods().All(m => Harmony.GetPatchInfo(m)?.Owners.Count is null or 0), "every F8h stand-in patch removed");
    }

    /// <summary>One city load's checkout decision; returns its refusal (null when checkout is on).</summary>
    private static string? Load(out HookConflictReport scan)
    {
        string? refusal = NativeFoodContract.CheckoutForLoad(false, out _).Refusal;
        scan = HookConflicts.TakeLoadScan()!;
        Check(scan != null && scan.CheckoutRefusal == refusal, "the decision comes from the load scan");
        Check(scan!.Entries.Count(e => e.PcOwners.Contains(NativeHookInventory.FoodAuthority)) == 6, "the PC owner stays on the tick and the five H1 customer targets");
        return refusal;
    }
}
