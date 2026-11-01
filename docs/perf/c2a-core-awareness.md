# C2a performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| C2a r1 | Bound geometry publication with 64-item continuation; keep eligibility-only changes out of geometry revision | Synchronous 20k rebuild: 40 measured >=30 ms samples, max78.1916 ms; eligibility flips could rebuild identical geometry | 371,898 measured units incl. cold/JIT: p95 <=0.0123 ms, max11.8403 ms, zero >=30 ms; eligibility flips perform zero builds. 20k complete build uses 9,390–10,408 units with clock/event ordering held. [r1 evidence](../reviews/2026-10-31-c2a-r1-rebuild.json) |
| C2a | Reuse source draw prefixes, use bounded linear dedup and size the one source list | First dense prototype: 4,564 / 4,413 bytes/context (1,024 / 20k) | Final three-process median 987 / 885 bytes/context; keys preserved by independent original-key test. Offline only; [measurements](../reviews/2026-10-31-c2a-measurements.json) |
| C2a | Stream binary catalogue revision through 64 KiB buffer; one indexed ordinal array/ranges per spatial scale | 20k dense / sparse builds allocate 17,479,416 / 31,995,973 bytes | 4,254,290 / 13,475,542 bytes. Wall time variable; **40 measured rebuild spikes >=30 ms**, max78.1916 ms, retained in CHECKS; no native gate claim |
| C2a | Remove full metadata and routine-hours sweeps from each native decision capture; direct generation/scope lookup | B0b batch: all controllers + one full hours pass per context | Zero full capture sweeps; <=24 sampled eligibility predicates and <=8 detailed captures. Synthetic eight-unbatched-read control 0.1105 ms ->0.0094 ms batch (not a native B0b frame comparison) |

## Base sync validation — 1 November 2026

No new optimization or native timing claim. B1 memory projection/rank wiring stays
inside the four memory slots, 24 admitted source identities, eight shortlist slots
and sixteen route requests. The existing 64-item yielded builder is unchanged.

The foreground awareness regression's explicit **whole-index synchronous offline
benchmark** flags ten samples >=30 ms: six at 20k dense (maximum **43.1620 ms**)
and four at 20k sparse (maximum **39.0312 ms**). These are total `BuildAll` costs,
not ordinary simulation work units; production still yields between bounded
builder phases. Food choice, memory, regulars and held-tuning harnesses report no
>=30 ms measured work sample in this run. Whole-frame/native mod-work timing is
unmeasured; the combined rig gate and parked hotspots remain unchanged.

Full measurements and command hashes are in the
[sync manifest](../reviews/2026-11-01-c2a-sync-base-results.json).

## Sol-r1-1 validation — 1 November 2026

The admission predicate adds fixed scalar checks per memory slot. The insertion
guard shares the existing at-most-four-entry eviction work; it adds no collection,
scan of the city, route request or saved field. B1 scalar contribution/decay and
the 64-item index continuation are unchanged. No new performance win is claimed.

The review run's explicit synchronous offline `BuildAll` benchmark flags **nine
samples >=30 ms**: four at 20k dense, maximum **43.1327 ms**, and five at 20k sparse,
maximum **42.6555 ms**. These complete-build measurements are separate from
ordinary yielded work units and native mod-work/whole-frame timing. Full benchmark
data and final checks are in the
[review manifest](../reviews/2026-11-01-c2a-sync-base-r1-results.json).


## Second base sync validation ? 1 November 2026

This merge adds no performance change or new win. The unchanged synchronous
offline awareness `BuildAll` benchmark reports **two samples >=30 ms** at 20k
dense, maximum **33.1144 ms**; all other complete-build cases and all measured
admission samples are below 30 ms. These are separate from production's yielded
64-item units and from native mod-work/whole-frame timing. Every measured capacity
write/read and each successful schema-17 migration is also >=30 ms and explicitly
flagged as offline save/load/migration latency in [C2a checks](../checks/c2a-core-awareness.md).
Full benchmark data and exact unchanged capacity/record totals are in the
[sync manifest](../reviews/2026-11-01-c2a-sync2-base-results.json). Mod-work p95 <5 ms,
spike reporting, autosave discipline, separate whole-frame tracking and the four
parked hotspots remain unchanged. No native performance claim is made.
