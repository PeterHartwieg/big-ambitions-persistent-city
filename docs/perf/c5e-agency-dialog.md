# C5e: agency dialog cost (8 November 2026, offline)

Plan: [C5 section 4, row C5e](../plans/c5-agency-research.md#c5e--agency-dialog-and-ordering) and section 6. Checks: [docs/checks/c5e-agency-dialog.md](../checks/c5e-agency-dialog.md).

The dialog postfix runs once each time the agency's campaign screen is built, never per frame. In the C5e build `Available` is false, so the postfix stops at the third precondition: it reads the desk, two installed flags and `Available`, and then returns. The menu and the order exist only after the button is clicked, which cannot happen in this build.

Attaching the saved ledger through the C5b seam adds no per-frame work while no order is open. C5b measured `HoldsThrough` with no open order as one sequence comparison and one null check ([C5b perf](c5b.md)). Billing is now attached, but with no Surveyed order the `RunDaily` postfix reads one field and the open-order count, allocating 0 B ([C5d perf](c5d.md)).

## Method

The test is `ResearchMenuTests.PostfixGateAndMenuBuildCostPerCall`, run with `--filter ResearchMenuTests --logger "console;verbosity=detailed"`. Setup:

- Debug build, net10.0, Windows 11, one foreground dotnet process. The class runs in its own non-parallel xUnit collection.
- Each row has 2,000 timed calls after 200 untimed warm-up calls (JIT tier-up is not per-call work).
- Timing is `Stopwatch`; allocation is `GC.GetAllocatedBytesForCurrentThread`.

Rows:

- **Gate, hidden.** `ResearchDialogGate.TryExtend` over a stand-in entry with the C5e preconditions (`Available` taken from `MarketResearchSettings`). Each call also allocates the stand-in entry and its two handlers, as native `CampaignSettings()` does for the real entry, plus the extend closure. That makes the bytes an upper bound for the postfix.
- **Gate, visible.** The cost C5f adds when `Available` becomes true: every precondition passes, and the eligibility scan walks 2,000 registrations. 20 of them are rented by the player, and each player registration builds its `NativeFoodMarket.PlaceId` string and compares it with 8 PC food scopes. The matching business is last, so the scan walks the whole list.
- **Menu build.** `ResearchMenu.Build` for the shipped 3 products and for 6 products.
- **Entry arguments.** One menu entry's argument dictionary: product, description, business and price.

Not in the offline numbers:

- the reflection `Harmony.GetPatchInfo` foreign-patch check. It runs only on a screen that passed every cheaper check, so it never runs in the C5e build. C5d measured the same check at p95 0.011 ms and 1,960 B ([C5d perf](c5d.md));
- native `ObservedOwnShop`, a dictionary lookup per player business;
- native dialog drawing.

The batched rig measures the whole screen (check 4 in the checks file).

## Results

| Call | p50 | p95 | max | Allocation per call |
| --- | ---: | ---: | ---: | ---: |
| Postfix gate, hidden (`Available` false) | 0.0000 ms | 0.0001 ms | 0.0106 ms | 56 B |
| Postfix gate, visible, 2,000 registrations | 0.0056 ms | 0.0082 ms | 7.5451 ms | 2,872 B |
| Menu build, 3 products | 0.0002 ms | 0.0006 ms | 0.0018 ms | 528 B |
| Menu build, 6 products | 0.0003 ms | 0.0008 ms | 0.0017 ms | 1,008 B |
| Menu entry arguments | 0.0002 ms | 0.0013 ms | 0.0089 ms | 640 B |

Spikes: none at 30 ms or more. The visible gate's 7.5 ms maximum is one sample in 2,000. Its p95 is 0.008 ms, so the maximum is a scheduler or GC pause in the test process, not scan work. The batched rig run will show whether it repeats in game. The mod-work p95 gate (5 ms) is met by every row, and each runs once per agency screen or menu click, not per frame. Whole-frame timing is not measured offline.

The four parked hotspots stay parked; C5e does not touch them.

## Perf decisions

- **Cheapest first, lazy scan.** `Read` computes each precondition only when every earlier one passed. The registration scan and the reflection foreign-patch check therefore never run while `Available` is false, or when the city is not attached or the slot is in use. This was taken in the first version, so there is no before number. The visible row is the cost an eager scan would add to every screen today.
- **Menu built once at install.** The tree is built from the fixed product table in `Install`, not per click. A click builds only the entries it shows. This was also taken in the first version; the menu build row is the per-click cost it avoids.
