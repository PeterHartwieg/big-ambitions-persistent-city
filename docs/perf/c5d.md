# C5d: research billing cost (6 November 2026, offline)

Plan: [C5 section 4, row C5d](../plans/c5-agency-research.md#c5d--native-billing). Checks: [docs/checks/c5d.md](../checks/c5d.md). C5d is not installed or attached by any caller yet (C5e wires it), so it adds no mod-work in the game build today. These are the costs the `MarketingHelper.RunDaily` postfix will add once wired. It runs once per game day, inside native daily settlement.

## Method

Test `ResearchChargeTests.PostfixCostWithZeroOneAndEightOrdersOnAFullQueue`, run with `--filter ResearchChargeTests --logger "console;verbosity=detailed"`. Debug build, net10.0, Windows 11, one foreground process. The test class runs in its own non-parallel xUnit collection: inside the parallel full suite the same boundaries measured p95 9.9 ms and max 19.2 ms for 8 orders, which was CPU and GC contention from other test classes, not billing work. 300 timed boundaries per row after 20 warm-up boundaries; `Stopwatch` timing; allocation from `GC.GetAllocatedBytesForCurrentThread`.

The port double does the same work as `NativeResearchChargePort`:

- a 1,000-record queue (one in seven a marketing record, each with a transaction-data dictionary), walked whole by `CollectTagged`, by `Tail` (native `Queue<Transaction>` has no end accessor) and by `CollectTaggedAfter`. The post-charge walk has the production shape: it probes every record's transaction data for the tag, keeps tagged records with their positions, then keeps those after the remembered record;
- 2,000 registrations, 20 rented by the player. Each order bills one of the last rented businesses, so every `TryResolve` walks the whole list and builds the `NativeFoodMarket.PlaceId` string of all 20 player businesses;
- the native money change: binary32 subtraction, one appended record and its data dictionary.

The postfix's foreign-patch check (`ResearchBillingContract.ForeignPatch`, once per boundary with work) is measured separately by `ResearchChargeTests.ForeignPatchCheckCost`. It loads the Harmony 2.4.2 build that `tools/build.py` pins (`lib/net10.0` from the same package), patches a stand-in method with our postfix and a foreign postfix, as `RunDaily` will carry, and times `Harmony.GetPatchInfo` plus the owner walk over prefixes, transpilers and finalizers. The test project does not reference Harmony, so the calls go through reflection; the number is an upper bound.

Not in the offline numbers: native `ChangeMoney` side work (top-bar animation, tax tracking, sound, `ba:gameevent_moneychange` listeners). That is native cost, not mod-work. The batched rig measures the whole boundary (check 8 in the checks file, part of plan check 10).

## Results

| Surveyed orders | Queue | p50 | p95 | max | Allocation per boundary |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,000 | 0.0000 ms | 0.0002 ms | 0.0016 ms | 0 B |
| 1 | 1,000 | 0.059 ms | 0.069 ms | 1.610 ms | 5,032 B |
| 8 | 1,000 | 0.326 ms | 0.369 ms | 0.512 ms | 33,536 B |

| Foreign-patch check (1,000 calls) | p50 | p95 | max | Allocation per call |
| --- | ---: | ---: | ---: | ---: |
| `GetPatchInfo` + owner walk, 2 postfixes | 0.008 ms | 0.011 ms | 0.021 ms | 1,960 B |

Full postfix with work, worst case: 8-order boundary p95 0.369 ms + check p95 0.011 ms = **0.38 ms**, once a day. The earlier rows (before the post-charge walk matched production) were 0.108 ms and 0.381 ms p95 for 1 and 8 orders; the difference is within run-to-run noise.

Idle is zero, asserted in the same test: with no open order, and with a Surveyed order in a stopped runtime, 1,000 postfix calls allocate 0 bytes and make no port call. Each idle loop runs once untimed first, so tier-up JIT allocation is not counted. The foreign-patch check does not run when idle. The postfix itself reads one static field and returns when there is no session.

Spikes: none. The largest single boundary is 1.6 ms (one order, a single outlier), far below the 30 ms flag. p95 at the 8-order city limit is 0.38 ms with the check, under the 5 ms gate. The allocation is the per-order place-ID strings, the target, the native `TransactionInfo` data dictionary, the appended record and the check's patch-list copies; once a day.

## Perf decisions

- **One tag scan per boundary, not one per order.** `CollectTagged` reads every queued record once and keeps only the tagged ones; each order then matches in memory. Taken in the first version, so there is no before number; a per-order scan would walk the queue 8 more times at the city limit.
- **`Tail` walks the queue** (reviewer note, Opus r1). Measured inside the rows above: it is one of the three queue walks per order and allocates nothing (struct enumerator). At 0.37 ms p95 for 8 orders once a day it is not worth a cache that could go stale against native dequeues. Not changed.
- **No allocation on the idle path.** `HasWork` loops the open list by index instead of `foreach` over `IReadOnlyList`, which boxes the list enumerator. Changed before the first measurement, so there is no measured before number; the index loop allocates 0 B (asserted).
- The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are not touched.
