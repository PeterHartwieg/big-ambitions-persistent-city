Status: implemented (offline); not verified by rig

# F8h checks: conflict refusals

Refs [#95](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/95). Row F8h of the [F8 plan](../plans/f8-capability-boundaries.md), decision D4. No schema or save-shape change; no edit under `Persistence/`. Perf: [docs/perf/f8h.md](../perf/f8h.md).

## Condition: rig check 1

Rig check 1 ran on both rigs at `b3e5a910` with the full modpack: [PC record](f8h-check1-modpack-owners-pc.json), [Mac record](f8h-check1-modpack-owners-mac.json), notes in [f8bg.md](f8bg.md). Neither record shows a foreign owner on the five H1 customer targets (`RetailBusinessSimulator.ProcessAllCustomersFromThisHour`, `RetailBusinessSimulator.ProcessCustomer`, `CustomerEntriesHelper.GetEntriesByAddress`, `CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`, `BuildingRegistration.Reset`) or on H2 (`CitizenData.IsPriceAcceptable`). `GameManager.RunMainGameTick` carries may-skip prefixes and swallowing finalizers from cityperformance.mod, drivenrides.mod and foodtrucks.mod, plus a foodtrucks postfix. The plan treats that method as shared, so it is not in the policy. The other owners in the records (hardmodepatch on `ItemsGetter.RegisterModItem`, rig-input transpilers on inspector targets, vehicle and pedestrian owners) stay report-only as F8e left them.

## What changed

- `Runtime/HookConflicts.cs`: the policy table `CheckoutPolicy` (the five H1 customer targets and H2). `Scan` builds the report with it. `ScanForLoad` keeps the load's scan; `TakeLoadScan` hands it to the activation line once.
- `Core/HookConflictReport.cs`: on a policy target, the first foreign owner that is not an observer gives `foreign <kind> by <owner> on <target>`. `pc capability hooks` marks that entry `-> refuses food.checkout.shop`; the activation line names the refusal.
- `Runtime/NativeFoodContract.cs`: `CheckoutForLoad` combines the member contract (first) with the conflict refusal. A failed scan refuses too.
- `Runtime/NativeFoodMarket.cs`, one line: the constructor calls `CheckoutForLoad()` instead of `Checkout`. A refusal therefore uses F8c's checkout-off mode unchanged: no price prefix, no shop served, `ProcessCustomer` not resolved, every shop observed ineligible, owned shops handed back at the next drained boundary.
- `Runtime/CapabilityDiagnostics.cs`, one line: the activation line reuses the load scan.

## Offline evidence

- `tools/capability-contracts`, arm `ConflictRefusalArms` (pinned Harmony 2.4.2 on stubs, Runtime code): a foreign prefix on each of the six policy targets refuses with owner and target named; a foreign transpiler on `GetEntriesByAddress` and a foreign finalizer on `ProcessAllCustomersFromThisHour` refuse; foreign postfixes on all six do not; the check-1 owners on `RunMainGameTick` do not. Red on the base decision: with a foreign prefix on `ProcessCustomer`, `StartCheckout(Checkout)` serves and a sale begins a receipt. Green: checkout-off, no receipt key. 144 checks pass.
- `tests/HookConflictReportTests.cs`: the Runtime table is exactly the six signatures and excludes `RunMainGameTick`; may-skip, rewrites (transpiler, inner patches) and swallows refuse on every policy target, observers and non-policy targets do not; the decision is made once per load from the load scan; real Harmony 2.4.2 on stand-ins for prefix, postfix, finalizer and the check-1 tick shape.
- `tests/NativeFoodContractTests.cs`: the owned-shop withdrawal test now also runs with a conflict refusal. The shop keeps its loaded hour, begins no receipt, and is handed back at the next drained boundary through the existing path. Paid and uncertain receipts are unchanged.
- Golden hash unchanged: `CapabilityReportTests.CurrentWriterGoldenHashIsUnchanged` (schema constant).

## In-game checks

Fixture: a disposable FT city on the owned rig with Persistent City at head, RLO and Street Food, one owned food shop. Autosave off in the isolated profile, checked before each window (also after reload) and restored at cleanup. Every check is rig-automated; none is deferred to Peter or a by-hand check.

1. **Foreign prefix on checkout** (plan check 10; locked-PC). Setup: a rig-owned test Harmony owner adds a prefix on `RetailBusinessSimulator.ProcessCustomer` in the disposable profile. Steps: load; run `pc capability status` and `pc capability hooks`; run to the shop's lunch hour; remove the test owner; reload; run past the next accounting boundary. Expected red (base build): `food.checkout.shop optional active` and PC receipts written. Green: `food.checkout.shop optional off: foreign prefix by <owner> on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)`; the hooks entry ends `-> refuses food.checkout.shop`; the activation line names the refusal; the shop shows `Excluded: food checkout contract off: foreign prefix by ...`, native customers serve it after the next drained boundary and zero new PC receipts are written. After removing the owner and reloading, `food.checkout.shop optional active` and ownership returns at the next accounting boundary. **Pending:** RIG-FOREIGNPATCH (install and remove a named test Harmony patch in the disposable profile); **existing:** FT `console`/`load`/`time`.
2. **Full modpack keeps checkout on** (re-run of plan check 1; locked-PC). Setup: full modpack DLL set. Steps: load; run `pc capability status` and `pc capability hooks`; keep the activation lines. Expected: `food.checkout.shop optional active`; the hook report header ends `0 refusing food.checkout.shop`; the `RunMainGameTick` owners are listed with their classes and no entry is marked refusing; the activation line has no `food.checkout.shop off` and records `scan <ms>` for the perf file. **Existing:** RIG-MODSET full set, FT `console`/`load`.
