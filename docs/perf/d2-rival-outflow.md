# D2 performance measurements (offline only)

Measurement provenance: the original D2 timings/byte tables and round-one repair
results below were measured on the D2 line based on `51ce709`, before E1 schema
20. They remain historical evidence, not current merged-head measurements. The
base-sync results are recorded separately in D2 checks; no old number is relabelled.

1 November 2026; base `51ce709`. Foreground .NET 10 on the shared Windows host,
actual D1/D2 reader code against native field doubles, no game/Unity execution.
The benchmark is `tools/rival-outflow-regression`; raw JSON is in ignored
`build/d2/gates/rival-outflow.log`. Exact data and the command/source manifest
are retained with the [D2 evidence](../checks/d2-rival-outflow.md).

The first D2 arrival prototype read live rival details twice and rebuilt the
same hours/offer projection. The implementation validates one detached snapshot,
reuses its hours and indexes the known one-item detail result. The benchmark
compares one versus two identical actual detail reads, isolating duplicate-read
savings rather than claiming a whole native-frame comparison. Metadata scopes
are created once; unchanged refreshes preserve the index revision.

| Control / taken win | Before p50 / p95 ms | After p50 / p95 ms | Allocated bytes before -> after |
| --- | ---: | ---: | ---: |
| Two detail reads -> one, 1,024 places | 0.0073 / 0.0112 | 0.0022 / 0.0057 | 5,744 -> 2,872 |
| Two detail reads -> one, 20,000 places | 0.0095 / 0.0151 | 0.0042 / 0.0095 | 5,744 -> 2,872 |
| Eight unknown-stock reservation probes -> skip unused keys | 0.0002 / 0.0006 | 0.0001 / 0.0001 | 1,024 -> 0 |

1,000 measured operations/control after warmup. The reservation control uses
the same eight native-format place/product strings and old/new conditional
lookup expressions. Unknown stock cannot use a reservation quantity; player
observed-stock lookups are unchanged. The arrival control isolates identical
actual reader calls, excluding additional savings from reused hours projection
and direct one-item indexing; no unsupported end-to-end speedup is claimed.

Unchanged metadata refreshes preserve the index revision (asserted at both scales),
so they do not rebuild C2a's six-level index. Warm refresh allocates only the
32-byte enumerator once per whole refresh, with zero subsequent-unit allocation.
The before control of rebuilding every refresh is identified structurally; no
timing gain is claimed for that unmeasured control. Cached scopes/indexed identity
lookup add retention: measured reader heap delta is **720,464 / 14,092,896 bytes**
at 1,024 / 20,000 places, excluding native fixtures/unmanaged weak handles and
including small timing containers. The full state remains bounded by 20,000 places.

Warm source/rank p95 is **0.0189 / 0.0438 ms**, with **1,563 / 1,566 bytes/context**
at 1,024 / 20,000 alternatives. Native details/routes are excluded from that
micro-timer. Cold/warm 16-read and 64-item index-unit maxima are in the checks.
Settlement p95 is **0.0001 ms**, max **0.1223 ms**, **192 bytes/payment**.

**Flagged costs:** two 20,000-alternative offline 128-unit advancement batches
are >=30 ms, maximum **47.7935 ms**, with no finer component attribution. The
inherited D1 aggregate cold-cache benchmark mean is **47.2054 ms** at 20,000
places; that is many bounded units, not one frame. No individual measured D2
refresh/index/source/detail/settlement micro-sample reaches 30 ms. Neither the
offline batch's p95 nor its max is a native mod-work/whole-frame result.

Refresh and index builds use bounded continuations; aggregate cold work is not
drained in one frame. Source/rank timing excludes construction/routes; ledger
timing excludes fixture setup. Save measurements concern bytes/record accounting,
not the parked serializer rewrite or a native save-profile session. Samples
>=30 ms are flagged separately from ordinary mod-work.

The full save-capacity rerun records >=30 ms standalone operation latency:
write/read 119.9/343.3 ms (reference), 212.7/746.2 ms (5k),
2,540.5/9,708.8 ms (CF32), 3,285.0/9,022.9 ms (CF64).
These single shared-host samples are not mod-work/frame observations or an
isolated before/after comparison. Save serialization remains parked. Exact
schema-19 comparison, allocations and unchanged future envelopes are retained
in the checks/results manifest.

Combined native mod-work p95 <5 ms / spikes <30 ms and whole-frame observations
remain pending the normal combined 6 Nov rig session.

## Round-one repair rerun — 1 November 2026

Known-food eligibility is cached during the existing metadata refresh, including
in-place advertised-menu changes. It adds no consumer-time product scan. Detached
trip admission preserves the one live arrival read and does not allocate/copy a
second hours schedule or mutate saved places. Source failures warn once and stop
their own refresh/candidate work for the loaded city.

At 1,024/20,000 alternatives, warm source/rank p95 is **0.0276/0.0353 ms**,
warm 16-read refresh-unit p95 **0.0044/0.0050 ms**, with unchanged allocation
1,563/1,566 bytes/context and 32 bytes/whole warm refresh. These shared-host reruns
are not an isolated speedup control. Ledger p95/max is **0.0001/0.1785 ms**,
192 bytes/payment. One offline 128-unit batch is **>=30 ms**, max **31.7774 ms**;
no finer component attribution. D1's aggregate cold 20k build mean is **50.0471 ms**,
many bounded units. Both are flagged separately from the pending native gate;
individual D2 micro-samples remain below 30 ms. Full rerun evidence is linked from
the slice checks; initial measurements and limits above are preserved.

The final rerun (including null/blank label coverage) retains those allocations
and bounds: source/rank p95 **0.0332/0.0441 ms**, warm refresh-unit p95
**0.0059/0.0166 ms**, ledger p95/max **0.0004/0.2696 ms**. **Three** final
20k offline 128-unit batches are >=30 ms, max **90.6681 ms**, with no finer
component attribution; D1 aggregate cold 20k mean **53.2260 ms** is also flagged.
Individual D2 micro-samples remain below 30 ms. The preceding first-rerun values
and raw JSON are retained separately in the round-one manifest. These shared-host
measurements do not establish a speedup or the native mod-work/whole-frame gate.

## Base sync re-measure, 1 November 2026

No new optimization in `pc-d2-sync-base`. The unchanged D2 Release native-double
harness was rerun on the merged E1/F1 tree (parents `ef2d72e` / `5877453`). Warm
source/rank p95 was 0.0179 / 0.0212 ms at 1,024 / 20,000 alternatives;
settlement p95 0.0004 ms. The 128-event offline batch p95 was 2.1625 / 1.7941 ms,
with one >=30 ms sample (35.5058 ms, 20,000-alternative whole-day arm). That is
an offline batch spike; component attribution and native mod-work/whole-frame
proof remain unavailable. No matched speedup/regression is inferred from these
shared-host reruns versus the archived pre-E1 measurements above.

The genuine schema-20 -> 21 idle-fixture comparison measured 2,284,562 ->
2,294,151 bytes for 1,914 consumers (+9,589), and 5,973,936 -> 5,998,955 bytes
for 5,000 consumers (+25,019): exactly +5 bytes/consumer +19 header. Streaming
and semantic records are unchanged (3,829 / 3,829 and 10,001 / 10,001).
Population-only save-contract output is now 120,184 bytes; its weather-stripped
projection is 119,083 bytes. These are named synthetic fixtures, not raw game
save sizes. The original tables remain pre-E1 evidence, with their original
numbers intact. Current raw output is retained with the sync validation results.
The four parked hotspots remain parked; no native timing gate is claimed.

## Final A5-head re-measure

At acceptance source `29147ee` (A5 `0ffb819` included), warm source/rank p95 is
0.0191 / 0.0298 ms at 1,024 / 20,000 alternatives; settlement p95 0.0006 ms.
Offline 128-event batch p95 is 2.1011 / 1.7834 ms, with one >=30 ms batch sample
(**40.9275 ms**, 20,000 alternatives). The earlier 35.5058 ms sample remains
identified above as the E1/F1-pass result. No matched performance inference or
new optimization is claimed. Save-byte deltas remain +5/idle consumer +19 header
from schema 20 to 21, with no extra records. Current capacity/save timings and
all >=30 ms whole-save latencies are recorded in [D2 checks](../checks/d2-rival-outflow.md#final-combined-head-validation);
native mod-work and whole-frame proof remain separate and pending.

The separate full dense awareness catalogue-build benchmark flags one 33.2802 ms
sample (p95 33.2802 ms); it is a whole-build offline operation, not a measured
runtime continuation or native frame. See the selected-tool timing flags in the
[raw final results](../reviews/2026-11-01-d2-sync-base-results.json).

## E4-head re-measure

Source `df69679` (integration `2bd9006`), before the pizza landing: warm
source/rank p95 0.0183 / 0.0242 ms at 1,024 / 20,000 alternatives, settlement
p95 0.0011 ms. Offline 128-unit batch p95 is 2.1740 / 2.1400 ms; one 20k batch
is >=30 ms, max 36.0793 ms. No matched speedup/regression or new optimization
is inferred from these shared-host reruns. D2's intermediate 20 -> 21 idle
delta stays +5 bytes/consumer +19 header, zero records. E4's separate 73-byte
header consumes the existing reserve once; all caps/future envelopes remain.
Current bytes, whole-save/aggregate timing flags and limits are in
[D2 checks](../checks/d2-rival-outflow.md#e4-head-acceptance-before-the-pizza-landing)
and the [raw E4-head results](../reviews/2026-11-01-d2-sync-base-e4-results.json).
Native mod-work and whole-frame proof remain pending the combined head;
the four parked hotspots are unchanged.

## Sync2 fresh before/after comparison

No optimization in this integration. Same Windows host, .NET 10 Release D2
native-double harness; exported `03960b1` source is the freshly rerun before arm,
final source `246c24e` is the after arm. Runs are sequential, not randomized or
interleaved. Setup/JIT/sample protocol is the unchanged harness; this is neither
native mod-work nor whole-frame evidence. The old worktree was untouched.
[Raw outputs, commands, source/log hashes and limits](../reviews/2026-11-01-d2-sync2-base-results.json).

| Alternatives | Source/rank p95 ms before -> after | Bytes/context before -> after | 128-unit batch p95 ms before -> after |
| --- | ---: | ---: | ---: |
| 1,024 | 0.0185 -> 0.0279 | 1,587.072 -> 1,595.072 | 2.1086 -> 2.0173 |
| 20,000 | 0.0236 -> 0.0236 | 1,590.032 -> 1,598.032 | 1.4965 -> 1.7952 |

The +8 bytes/context reflects the added C2b held-image cache field; both approved
slices remain composed. Timing differences are shared-host samples, not a measured
optimization or causal regression claim. Settlement p95 is **0.0004 -> 0.0005 ms**,
max **0.1045 -> 0.0923 ms**, with **192 bytes/payment** in both arms. No individual
D2 source/read/index/settlement micro-sample is >=30 ms.

The paired 20k batch arm has **1 -> 2** samples >=30 ms, max **41.7655 -> 36.5147 ms**;
the 1,024 arm has zero in both. The full 73-gate acceptance's separate D2 run has
one 20k batch >=30 ms, max **32.9809 ms**. These are whole offline AdvanceTo batches,
without finer component attribution, not individual game frames.

Genuine schema-20 -> 21 idle bytes remain **2,284,562 -> 2,294,151** at 1,914
consumers and **5,973,936 -> 5,998,955** at 5,000: +5/consumer +19 header,
zero record delta. Schema-23 capacity fixtures add the measured **15-byte empty
Campaigns collection** versus the explicitly named historical E4/D2 table;
all record totals and future envelopes stay unchanged. Current raw history,
capacity and whole-save measurements plus all >=30 ms flags are in
[D2 checks](../checks/d2-rival-outflow.md#sync2-c2bfrenchfries-composition-and-final-offline-acceptance---1-november-2026).
Cold C1 p95 **6.0459 ms** remains flagged; whole catalogue setup, day/rush and
serialization costs are tracked separately. No native p95 <5 ms or whole-frame
gate is claimed; the four parked hotspots remain parked.
