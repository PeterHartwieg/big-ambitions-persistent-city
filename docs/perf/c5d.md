# C5d: research billing cost (6 November 2026, offline)

Plan: [C5 section 4, row C5d](../plans/c5-agency-research.md#c5d--native-billing). Checks: [docs/checks/c5d.md](../checks/c5d.md). C5d is not installed or attached by any caller yet (C5e wires it), so it adds no mod-work in the game build today. These are the costs the `MarketingHelper.RunDaily` postfix will add once wired. It runs once per game day, inside native daily settlement.

## Method

Test `ResearchChargeTests.PostfixCostWithZeroOneAndEightOrdersOnAFullQueue`, run with `--filter ResearchChargeTests --logger "console;verbosity=detailed"`. Debug build, net10.0, Windows 11, one foreground process. The test class runs in its own non-parallel xUnit collection: inside the parallel full suite the same boundaries measured p95 9.9 ms and max 19.2 ms for 8 orders, which was CPU and GC contention from other test classes, not billing work. 300 timed boundaries per row after 20 warm-up boundaries; `Stopwatch` timing; allocation from `GC.GetAllocatedBytesForCurrentThread`.

The port double does the same work as `NativeResearchChargePort`:

- a 1,000-record queue (one in seven a marketing record, each with a transaction-data dictionary), walked whole by `CollectTagged`, by `Tail` (native `Queue<Transaction>` has no end accessor) and by `CollectTaggedAfter`;
- 2,000 registrations, 20 rented by the player. Each order bills one of the last rented businesses, so every `TryResolve` walks the whole list and builds the `NativeFoodMarket.PlaceId` string of all 20 player businesses;
- the native money change: binary32 subtraction, one appended record and its data dictionary.

Not in the offline number: the one `Harmony.GetPatchInfo(RunDaily)` foreign-patch check per boundary with work (the test project does not reference Harmony; it is a dictionary lookup plus a copy of the patch lists, once a day) and native `ChangeMoney` side work (top-bar animation, tax tracking, sound, `ba:gameevent_moneychange` listeners), which is native cost, not mod-work. The batched rig measures both (check 8 in the checks file, part of plan check 10).

## Results

| Surveyed orders | Queue | p50 | p95 | max | Allocation per boundary |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,000 | 0.0001 ms | 0.0007 ms | 0.011 ms | 0 B |
| 1 | 1,000 | 0.065 ms | 0.108 ms | 0.200 ms | 5,032 B |
| 8 | 1,000 | 0.227 ms | 0.381 ms | 1.288 ms | 33,200 B |

Idle is zero, asserted in the same test: with no open order, and with a Surveyed order in a stopped runtime, 1,000 postfix calls allocate 0 bytes and make no port call. The postfix itself reads one static field and returns when there is no session.

Spikes: none. The largest single boundary is 1.3 ms with 8 orders, far below the 30 ms flag. p95 at the 8-order city limit is 0.38 ms, under the 5 ms gate. The allocation is the per-order place-ID strings, the target, the native `TransactionInfo` data dictionary and the appended record; once a day.

## Perf decisions

- **One tag scan per boundary, not one per order.** `CollectTagged` reads every queued record once and keeps only the tagged ones; each order then matches in memory. Taken in the first version, so there is no before number; a per-order scan would walk the queue 8 more times at the city limit.
- **`Tail` walks the queue** (reviewer note, Opus r1). Measured inside the rows above: it is one of the three queue walks per order and allocates nothing (struct enumerator). At 0.38 ms p95 for 8 orders once a day it is not worth a cache that could go stale against native dequeues. Not changed.
- **No allocation on the idle path.** `HasWork` loops the open list by index instead of `foreach` over `IReadOnlyList`, which boxes the list enumerator. Changed before the first measurement, so there is no measured before number; the index loop allocates 0 B (asserted).
- The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are not touched.
