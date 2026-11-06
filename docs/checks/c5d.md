Status: implemented (offline); not verified by rig

# C5d: native billing for paid research (6 November 2026)

Plan: [C5 section 4, row C5d](../plans/c5-agency-research.md#c5d--native-billing); design in sections 3.5 and 3.6; defaults in [design defaults](../decisions/2026-11-06-design-defaults.md). Base `cfb568a` (C5a, writer 25). Perf: [docs/perf/c5d.md](../perf/c5d.md).

## Scope

- **Implemented:** `Core/ResearchCharge.cs` (pure `IResearchChargePort`, `ResearchCharge.TryExactDebit` preflight, the evidence rules and `ResearchChargeSession`, the per-boundary billing loop); `Runtime/ResearchBilling.cs` (the `MarketingHelper.RunDaily` postfix, the native contract check for the billing members and `NativeResearchChargePort` over `GameManager.ChangeMoneySafe` and the native transaction queue); one helper `PopulationRuntime.RunSaveEpochUnit` in `Runtime/SaveEpochRuntime.cs`; `tests/ResearchChargeTests.cs` (34 cases).
- **Unchanged:** no caller installs or attaches research billing (C5e wires `ResearchBilling.Install` and `Attach`). `MarketResearchSettings.Available` stays false. No schema, no dialog, no delivery message, no `CitySimulator` change. Native campaigns are never read or written.

## What the code does

Per Surveyed order, oldest first, at the end of native `MarketingHelper.RunDaily` (before native `CreateFinancialSummary(Day - 1)`):

1. Cancelled if the business no longer resolves to a player-rented, eligible registration.
2. One read-only scan of the native queue collects every record carrying `persistentcity_research`. Exactly one record of this order with marketing type, this address and the negated price → Paid without a charge. Any other match count or shape → PaymentUnknown.
3. Exact-debit preflight: `B - price` must be a binary32 value, decided exactly on the decoded IEEE fields. Otherwise Declined (`BalancePrecision`) with no native call.
4. The queue end is remembered by reference, native entry is marked, then one `ChangeMoneySafe(-price, TransactionInfo("ba:transaction_marketing", "ba:transactioncategory_marketing", {businessName, persistentcity_research: <order ID>}), Day - 1, address, force: false)`, with `tax_marketing` when the saved flag is set.
5. `false` and no record of this order appended → Declined (`InsufficientFunds`). `true` → exactly one appended record of this order with marketing type and category, address, billed day, amount, tax flag and native balance `floor(B - price)` → Paid. Otherwise PaymentUnknown. Unrelated appended records are ignored; a front-dropped queue (including the remembered record) counts every queued record as appended.
6. An exception before entry leaves the order Surveyed; after entry it is PaymentUnknown. Nothing is retried or refunded. Exceptions are contained, so native `RunDaily` continues.

Each order runs in its own F7b guarded unit (`RunSaveEpochUnit`, the same `saveEpochUnits` counter as `Update`). A save started inside the charge (for example by a `ba:gameevent_moneychange` listener) writes both failure markers, publishes no payload and stops Persistent City after the unit; the session rechecks `active` before each order, so later orders are not charged in a city that can no longer be saved.

## Technical decisions

1. **Business key.** The ledger's business address is the Persistent City food place ID (`NativeFoodMarket.PlaceId`); the port resolves it to the registration and uses native `Address` equality in the evidence. C5e places orders with the same key.
2. **One guarded unit per order** instead of one for the whole boundary, so a refused save stops billing before the next order. The tag scan runs once before the units; it is read only and raises no native event.
3. **Exception before entry defers** (order stays Surveyed for the next bill): nothing reached native money, and the ledger has no state for it.
4. **Contract check** (3.6): reflection on `RunDaily()`, `ChangeMoneySafe(float, TransactionInfo, int?, Address, bool, bool)`, the `TransactionInfo` constructor and `SetTaxDeductibleName`, every `Transaction`, `Timestamp`, `GameInstance` and `BuildingRegistration` field used, a serialized `transactionData` field, and a probe that the native `Transaction` constructor keeps the tag. Foreign Harmony prefixes, transpilers or finalizers on `RunDaily` refuse; the foreign-patch check repeats at each boundary with work. Any mismatch leaves billing off with a status line and Persistent City running. Survival of the tag through save and reload is rig check 3 below.
5. **Port contract violations** (true without entry, entry twice, true without a record) are PaymentUnknown.

## Offline checks run (6 November 2026)

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "...\Big Ambitions_Data\Managed"` | passed |
| `dotnet test tests/PersistentCity.Tests.csproj` | **3,226 / 3,226 passed** (34 new C5d cases) |
| `dotnet test ... --filter ResearchChargeTests` | 34 / 34 passed; perf lines in [docs/perf/c5d.md](../perf/c5d.md) |

Cases in `tests/ResearchChargeTests.cs`: success; insufficient funds; false with a tagged record; exception before and after entry; port contract violations; appended-record mismatch in amount, address, day, balance, ID, type, category and tax flag; zero and two matching records; unrelated listener records still Paid; queue front dropped (1, 4 and 5 records, the last dropping the remembered record); existing record found by the scan, and with another amount or twice; repeated boundary; stopped runtime does nothing, also when stopped after the first order; ineligible business cancelled; campaigns never touched (also pinned in source); tax flag carried; unsupported tag keeps charging off; only Surveyed orders billed, each in its own guarded unit. Preflight: $0, $16,777,216 and $100,000,000 with $500 (the last BalancePrecision with no native call), $512 and $504 at $100,000,000 exact, fractional and negative balances, nonfinite and extreme balances refused; a sweep of every product price (plus $1, $504, $512, $10,000) against 7 mantissas × both signs at every binary32 exponent, 24,990 balances, checked against an independent BigInteger oracle and native binary32 subtraction. Save guard: a save inside the charge is Reentrant, writes the failure markers and the accounting stop, publishes no payload and leaves the second order uncharged; a save after the boundary publishes. Wiring: the helper's order in `SaveEpochRuntime.cs`, the session construction, contract check, postfix, `force: false` call with entry marked just before it, and no other Runtime file referencing `ResearchBilling`. Perf: postfix cost at 0, 1 and 8 orders on a 1,000-record queue with idle zero, and the foreign-patch check against the pinned Harmony build.

## In-game checks for the next batched rig

Run on a named disposable FT city at the development head with C5e wired (research billing installed and attached), a native marketing agency contact and one PC-owned food business, with an order placed and Surveyed (plan section 7, checks 1-3, are prerequisites from C5b/C5e).

1. **Contract check passes.** Setup: research build, fixture loaded. Steps: read the research billing status line through `call: console`. Expected: status empty (contract passed, postfix installed); no "Research billing off" warning in the game log. Rig-automated: existing capability `call: console` and log capture; pending capability R1 `research` op `list` for the status field.
2. **Charge at midnight (plan check 4).** Steps: with one Surveyed Basic order, advance through the next midnight; read money, the native marketing transactions with their data, that day's `MarketingExpenses` for the business and the order state. Expected: money down exactly $500 beyond native bills; one extra marketing record for the business on day `Day - 1` with amount -500, `businessName` and `persistentcity_research = pcr-<n>` in its data; `MarketingExpenses` includes it; order Paid; the business's campaigns unchanged. Rig-automated: existing capability `call: time` advance and `ledger` step; pending capability R2 transaction data in `ledger`.
3. **Save and reload after Paid (Paid half of plan check 7).** Steps: after check 2, trigger a save, reload the FT city, advance through the next midnight. Expected: the tagged record and its data survive the reload; no second research record and no second debit; order still Paid. Rig-automated: existing capability `save` trigger, `call: load`, RIG-PCSAVE extraction and compare, `ledger`; pending capability R2 transaction data in `ledger`.
4. **Insufficient funds (plan check 6).** Setup: after the survey, set the disposable money below the price. Steps: advance through midnight. Expected: order Declined (`InsufficientFunds`); no research record; money changes only by native bills; campaigns of that business not disabled by research (only by the native bill if that also fails). Rig-automated: existing capability bridge money set as in the `campaign` zero-money step, `call: time`, `ledger`.
5. **Balance precision.** Setup: set the disposable money to $100,000,000 after the survey. Steps: advance through midnight. Expected: order Declined (`BalancePrecision`); no research record; no research money change. Rig-automated: existing capability bridge money set, `call: time`, `ledger`.
6. **Native display of the record (plan check 9).** Steps: after check 2, open EconoView and BizMan for the business; capture. Expected: the research charge displays as a normal marketing expense; no error or missing-key text in the game log. Rig-automated: existing capability `panel` op and `capture` op.
7. **Stopped runtime does nothing.** Setup: Surveyed order, then stop Persistent City through an existing accounting stop path. Steps: advance through midnight. Expected: no research record, no research money change, order still Surveyed. Rig-automated: existing capability `call: console`, `call: time`, `ledger`.
8. **Billing perf (part of plan check 10).** Setup: autosave recorded, off and checked before the window. Steps: matched midnight windows with 0, 1 and 8 Surveyed orders. Expected: mod-work p95 under 5 ms; every spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; idle research work zero; autosave restored at cleanup. Rig-automated: existing capability RIG-PERF-UNION clean windows; pending capability R1 `order` action to create the orders.
