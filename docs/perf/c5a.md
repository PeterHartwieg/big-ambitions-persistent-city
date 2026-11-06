# C5a: research ledger costs (6 November 2026, offline)

Plan: [C5 section 4, row C5a](../plans/c5-agency-research.md#c5a--core-ledger-products-and-entitlement). Checks: [docs/checks/c5a.md](../checks/c5a.md). C5a is Core only and has no caller in the game build, so it adds no mod-work per frame. These are unit costs for C5d to C5f.

## Method

Test `ResearchLedgerTests.OrderAndGrantCostsAreSmallAndFindDoesNotAllocate`, run with `--filter ResearchLedgerTests --logger "console;verbosity=detailed"`. Debug build, net10.0, Windows 11, one foreground process. The ledger is at its worst-case size: 7 open orders, 16 kept reports and 16 closed records. The grant lookup targets the last kept order, so `Find` walks the whole open list and the whole kept list. 20,000 samples of each call, each timed with `Stopwatch`; allocation from `GC.GetAllocatedBytesForCurrentThread`. Each order sample is followed (untimed) by a seal and a cancel, so the next order finds a free slot.

## Results

| Call | p50 | p95 | max | Allocation |
| --- | ---: | ---: | ---: | ---: |
| `TryGet` (Paid order, worst-case position) | 0.30 µs | 0.30 µs | 31.8 µs | 40 B/call (the grant) |
| `TryOrder` (7 open, 16 kept, 16 closed) | 0.20 µs | 0.40 µs | 15.3 µs | 208 B/call (order object and ID string) |
| `Find` | — | — | — | 0 B over 1,000 calls (asserted) |

The max values are single timer or scheduler outliers, far below the 30 ms spike flag. Both p95 values are under 0.5 µs, about four orders of magnitude below the 5 ms mod-work gate. An order happens once per player confirmation and a grant once per report read; neither is per-frame work.

## Perf wins

None measured as a before/after pair. The report-ID uniqueness check walks the three lists directly instead of allocating an array of lists per seal; that was written before the first measurement, so there is no before number. The grant allocation stays: `MarketResearchGrant` has public setters, so a cached grant could be changed by a caller.
