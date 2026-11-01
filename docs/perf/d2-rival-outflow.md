# D2 performance measurements (offline only)

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
