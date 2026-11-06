Status: implemented (offline); not verified by rig

# C5a: research ledger, products and entitlement (6 November 2026)

Plan: [C5 section 4, row C5a](../plans/c5-agency-research.md#c5a--core-ledger-products-and-entitlement). The design is in sections 3.2 and 3.3, and the defaults are in [design defaults](../decisions/2026-11-06-design-defaults.md). Base `f46b3dd` (writer 25). Perf: [docs/perf/c5a.md](../perf/c5a.md).

## Scope

- **Implemented:** `Core/ResearchProducts.cs` (product table); `Core/ResearchLedger.cs` (`ResearchLedger`, `ResearchOrder`, order states, decline reasons, order and move refusals, bounds, `IMarketResearchEntitlement`, and an internal restore seam for C5c); `tests/ResearchLedgerTests.cs` (13 cases).
- **Unchanged:** no `CityState` property, no schema or codec change, no Runtime change, and no caller in the game build. `MarketResearchSettings.Available` stays false. The C3 placeholders stay until C5f.

## What the code does

- **Products.** Three rows, with Peter's accepted defaults as named constants: `basic-local` (Basic, catchment, $500), `detailed-local` (Detailed, catchment, $2,000) and `detailed-city` (Detailed, city, $6,000). There is no In-depth product. Each row's `Price` is `Money.FromCents(dollars × 100)` and its `NativePrice` is the binary32 whole dollar. The row constructor refuses a price that `Money.FromNative(NativePrice)` does not give back exactly, and any price above the native $10,000 limit.
- **Orders.** `TryOrder(productCode, business, agency, taxDeductible, radiusMetres, orderMinute)` stores the ID `pcr-<sequence>` (the sequence starts at 1 and is never reused), the product's fixed price, both addresses, the tax flag, the catchment radius (0 for city coverage), the order minute and `SurveyAtMinute = AccountingClock.Target(orderMinute)`. Refusals: `UnknownProduct`, `InvalidBusiness`, `InvalidAgency`, `InvalidRadius` (outside C4's 50–2,000 m), `InvalidMinute` (outside the `AccountingClock` domain), `BusinessHasOpenOrder`, `CityOrderLimit` and `SequenceExhausted`. A refused order uses no sequence. Business eligibility (rented, PC-owned food scope) is the caller's check (C5e).
- **Moves.** Only the plan's moves exist: `TrySeal` (Ordered → Surveyed); `TryMarkPaid`, `TryDecline(InsufficientFunds | BalancePrecision)`, `TryCancel` and `TryMarkPaymentUnknown` (each from Surveyed); and Paid → Replaced through retention. Every other move returns `WrongState` and changes nothing. `TrySeal` requires the report's tier and coverage to match the order (address and radius, or no address for city coverage), and a report ID that no other order in the ledger uses.
- **Entitlement.** `TryGet` returns a grant with `Paid = Delivered = true` only for a Paid order, the same with `Replaced = true` for a Replaced order, and nothing otherwise. The grant's report ID is the sealed report's query key, so `TryRead` checks the exact order–report pairing. Each call returns a new grant, so a caller that changes one cannot change the next.

## Technical decisions

1. **Three lists instead of one 16-slot history.** Section 5 says "at most 16 retained terminal or paid orders". Read literally, Declined orders would push delivered reports out, and a Replaced order could never be seen, because its slot goes to the new report. The ledger keeps `Open` (at most 8), `Kept` (Paid, at most 16, oldest delivery first; a 17th delivery turns the oldest into Replaced) and `Closed` (Declined, Cancelled, PaymentUnknown and Replaced, at most 16, oldest dropped first). That is at most 40 orders. Only Surveyed and Paid orders hold cells (at most 24 reports), so section 5's size estimate holds. Dropping a closed record is safe: sequences are never reused, and each charge is keyed by its own order ID.
2. **The order stores the catchment radius.** The plan's order field list has no radius, but the sealed report does. Storing it at order time keeps the survey after a reload on the same catchment, even if the setting changed. C5c serializes one more integer.
3. **The report is released** when an order leaves Surveyed for a non-Paid state, and on Replaced. `ReportId` is kept, so the Replaced grant and the uniqueness check still work.
4. **Restore seam.** C5c does not own `ResearchLedger.cs`, so this slice adds `internal ResearchLedger.TryRestore(nextSequence, open, kept, closed)` and `internal ResearchOrderImage`. They are internal to keep them off the public surface. The dotnet multi-project build enforces that Runtime cannot call them. `tools/build.py` compiles Core and Runtime into one `PersistentCity.dll`, so in the shipped DLL it is a convention, kept by calling `TryRestore` only from the codec; C4's internal report constructor rests on the same convention. C5c's planned `InternalsVisibleTo("PersistentCity.Persistence")` line gives the codec access. Restore is strict. It requires: next sequence at least 1; every sequence below it and unique; a known product at its exact price; valid addresses, radius and minute, with `SurveyAtMinute` equal to `Target(OrderMinute)`; a defined state in the right list; a decline reason exactly on Declined; a report exactly on Surveyed and Paid, matching the order and its `ReportId`; a unique `ReportId` exactly on every surveyed state; the list caps; and open orders in sequence order, one per business.
5. **No lock.** The dialog, the survey and the daily bill all run on the game thread, so C3's lock is not carried over.

## Offline checks run (6 November 2026)

Every command ran in the foreground after the shared other-dotnet wait. No background process was left running.

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "...\Big Ambitions_Data\Managed"` | passed |
| `dotnet test tests/PersistentCity.Tests.csproj` | **3,192 / 3,192 passed** (13 new C5a cases) |
| `dotnet test ... --filter ResearchLedgerTests` | 13 / 13 passed; perf lines in [docs/perf/c5a.md](../perf/c5a.md) |

New cases in `tests/ResearchLedgerTests.cs`:

- Product table: codes, tiers and coverage, and no In-depth. For every price, the `Money` units equal dollars × `UnitsPerDollar`, `Money.FromNative(NativePrice)` gives the same `Money`, and the binary32 bits are pinned (`0x43FA0000`, `0x44FA0000`, `0x45BB8000`). Unknown and case-variant codes are refused.
- Ordering: fields, sequence IDs and `SurveyAtMinute`, including an input at the minute fence. No refusal uses a sequence. City coverage ignores the radius.
- Caps: one open order per business for every product, and 8 per city. A Surveyed order still counts; a Paid order frees both.
- All 7 states × 6 moves: the 6 allowed pairs succeed with the right state, reason, list and report. The other 36 return `WrongState` and change nothing.
- Unknown order IDs, a `None` or undefined decline reason, and the report mismatches (null, empty ID, tier, address, radius, missing address, city report with an address, duplicate ID) are refused.
- Repeated boundaries: six Surveyed orders with the outcomes Paid, InsufficientFunds, BalancePrecision, Ineligible, throw after entry and evidence mismatch, plus one still Ordered, over four boundaries. Each charged order reaches native entry exactly once; the preflight and cancel paths never reach it. Both unknown orders stay PaymentUnknown. The same business orders again under a new ID, and that order is paid once.
- Declined (both reasons), Cancelled and PaymentUnknown release the report and grant nothing, and `TryRead` fails. The business can order again.
- `TryRead` succeeds only for Paid. It fails for Surveyed, Declined, Cancelled and PaymentUnknown, for another paid order's ID, an empty ID and `NoPaymentPath`, and after Replaced (the 17th delivery). Changing a returned grant does not change later reads.
- Closed records keep the newest 16; sequences keep increasing after drops.
- Restore: a ledger with every state round-trips field for field, with the same report objects and entitlements. 17 corruptions and two bad next sequences are refused.

## In-game checks for the next batched rig

C5a has no caller in the game build, so it has no in-game behavior of its own. These checks cover the ledger once C5e and C5f attach it. They are the C5a parts of plan section 7.

1. **Order fields and caps.** Setup: named disposable FT city, C5f build, one native marketing agency contact, two PC-owned food businesses A and B. Steps: order `basic-local` for A; try a second product for A; order for B; read `pcresearch orders`. Expected: A's order is `pcr-1` at $500 with its order minute and survey minute; the second order for A is refused as "business has an open order"; B's order is `pcr-2`. Rig-automated (pending capability: R1 `research` op, `order` action; existing capability: `call: console` for `pcresearch`).
2. **Exact price on the bill.** Setup: as check 1, after the survey. Steps: advance through midnight; read the marketing transactions. Expected: the research record's amount is exactly −500.0 (−2000.0 and −6000.0 for the other products when ordered). Rig-automated (existing capability: `call: time` advance and `ledger` step; pending capability: R2 transaction data in `ledger`).
3. **Paid report readable, declined not.** Steps: after check 2, run `pcresearch report pcr-1`. On a second fixture with money below the price, run the same for the declined order. Expected: numbers only for the Paid order; the declined order shows its state and `InsufficientFunds` and no numbers. Rig-automated (existing capability: `call: console`, and bridge money set as in the `campaign` zero-money step).
4. **Retention.** Setup: as check 1. Steps: complete 17 paid orders across days, then run `pcresearch orders` and `pcresearch report` for the oldest. Expected: 16 Paid orders; the oldest shows Replaced and gives no numbers. Rig-automated (pending capability: R1 `order` action in a loop).
