# Rig results: 12 November 2026 (b), Mac

Candidate `060a7947ef9680486f17f7cf535d64cd94ebe2da` (#233), official rig checkout `5dae2f6f` (#77), isolated `agentmac` profile, headless `--rig-test`, full modpack, 48 pedestrians / 24 vehicles / 120 m. Product DLL SHA-256 `f89c9e04…eacbd`. Autosave suppressed and checked after every load; all 260 original files and 12 fixture saves hash-matched after restore. No performance window was taken.

**Result: 15 passed, 0 partial, 0 failed.** These rows are verified by rig at `060a7947` only. They do not cover GR-3, GR-4, GR-6 to GR-9 or #239, which merged later.

This run followed [12a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-12a-mac-results.md), where the old assertion "a loaded mid-probe cut holds no half sale" failed. #233 replaced that contract with refuse-and-recover. 12a's failure stands. The natural first sale passed earlier in [11j](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-11j-mac-results.md) and was not repeated.

## Mid-probe cuts (#233)

Food fixture: `FT-PC-Mac-20261112b-060a794`, 25 Fourth Avenue, Garment District, C2 / 225 m², croissant at $5, independently ready before any advance. A coherent pre-probe save was taken at minute 1980.01. The probe `pc probe c0 <shop> 10000` was then cut by `probe_checkpoint` at the Sell prefix (enter) and the Sell finalizer (exit), each saved mid-call.

| Loaded cut | Native ledger | Receipt | PC startup |
|---|---|---|---|
| Exit | 1 order / $5, croissants 99 | Pending | refused |
| Enter | 0 orders / $0, croissants 100 | Pending | refused |

What the cuts mean:

- Either cut refuses Persistent City startup, with the cause "This save did not publish a coherent Persistent City snapshot … Persistent City never settles or replays such a sale; the earlier coherent save holds it undone."
- The exit cut keeps the native $5 half sale and the Pending receipt. Persistent City never settles or replays it. No reconciliation was attempted.
- In both cuts the saved population payload equals the pre-probe payload (SHA-256 `0f0fa836…c5d1`, 957 residents).
- Loading the earlier coherent pre-probe save gives an active city with the sale undone: no receipts, 0 orders, original wallet.
- A plain probe from there is Paid 500, one order, one 500-cent debit. A normal save taken outside the probe reloads active with Paid 500, `recovered=0`, one order and no second debit.

S2 check 5 passes under this refuse-and-recover contract, not under the old no-half-sale assertion.

## Sales and refusals

| Row | Fixture | Result |
|---|---|---|
| S2 food | pre-probe recovery above | FullService/Food Paid 500/500; one order, one debit |
| S2 goods | `FT-PC-Mac-20261112b-S2-goods`, 23 Fourth Avenue C1 | SelfService/Goods Paid 500/500; one order, one debit |
| S2 service | `FT-PC-Mac-20261112b-S2-service`, 25 Third Street A1 | Hairdresser/Service Paid 2000 (cutting fee $20); one order, one debit |
| S2 ceiling | completed food save, `pc probe c1 <shop> 0` | Refusal Ceiling, receipt Missing, "Basket 500 is above the ceiling 0"; no order or debit |
| F7-3 capture | capture-only enter cut, no save | Real Pending=1; probe then completes Paid and the city stays active |

#77 fixes are verified by rig: the goods fixture status read succeeds after the recipe's checkpoint save (it failed with `ops_fixture_unknown` in 11j), and the service recipe completes native finish with a head wash holder, two hairstylists and a $15 shampooing fee (it failed with `ops_shop_requirements_unmet` in 11j). The shampooing fee was not sold; only cutting was.

## Lessons for the next run

- Right after a load, PC reads can return "sale probe unavailable: no active city". Wait for `FoodDiagnostics active=true` before baseline reads. No code root cause is assigned.
- Stage scripts that use `Path` must import it explicitly (`from pathlib import Path`). 11f failed on this; 12b checked every stage script before starting.

## Not run

Outside the 15 rows and not promoted by this run:

- E5b2 to E5b5 and F9: the single-croissant fixture does not cover their menus or matrices.
- 11j partials: F6b-2, the larger F7-1 matrix, the X3-1 literal assertion, F2 and F8f matrices.
- Paid shampooing, the full service-fee matrix, other GR-1 lifecycle rows, and the natural next-day sale.
- stress10k, city-50k, RIG-FAULT, RIG-PCFIX, manifest sealing, F4f and F7e: capabilities or implementation absent.
- Present, foreground and priority flow steps; pointer and hover rows (Peter's playtest); Windows rows.

Raw evidence, commands, saves and hashes stay in the local stage `persistent-city-rig-2026-11-12b-mac` (`evidence-index.json`, `analysis-summary.json`, `food-verification.json`). Open checks are tracked in the [rig checklist](../rig-checklist.md).
