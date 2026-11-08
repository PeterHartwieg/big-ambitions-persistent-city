# F2f performance: spatial presentation index

Offline component timings from `python tools/physical-selection-regression/run.py` on Peter's PC (Windows, .NET 10, Release, tiered compilation off), 8 November 2026, at `9241012` on top of `1c29590`. These are not in-game mod-work. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from rig checks 1–2 in [the F2f checks](../checks/f2f.md). Other work was running on the machine, so isolated maxima of 3–9 ms appear in both arms; p50/p95 are the comparison.

Each row is 300 single calls after 100 warm-up calls. "F5f" is the merged F5f selection (index roster over every body candidate), compiled from the current production text with the F5f roster line restored (`BeforeF2f`). "Grid" is this build. Both arms select the same IDs in the same order on each fixture (checked before timing).

Fixture: open pools 48/24, radius 120 m, focus at the centre of a 3 km × 3 km city. Of the A active persons, two-thirds walk two legs of up to about 300 m each, and one-third wait at one of 60 scene places or stay there; everyone else is indoors and not indexed. With 1,914 persons, at most 1,914 are active. "From cells" is the size of the query result before the exact distance filter.

## Selection (ms per call; bytes per call)

| Persons / active | Indexed | From cells | F5f p50 / p95 / max | Grid p50 / p95 / max | Bytes F5f → grid |
| --- | ---: | ---: | --- | --- | --- |
| 1,914 / 100 | 100 | 1 | 0.0021 / 0.0022 / 0.0030 | 0.0002 / 0.0003 / 0.0007 | 32 → 32 |
| 1,914 / 1,000 | 1,000 | 26 | 0.0273 / 0.0319 / 0.0364 | 0.0016 / 0.0017 / 0.0033 | 152 → 152 |
| 1,914 / 5,000 | 1,914 | 45 | 0.0510 / 0.0909 / 0.2469 | 0.0034 / 0.0041 / 0.0119 | 152 → 152 |
| 10,000 / 100 | 100 | 2 | 0.0023 / 0.0023 / 0.0086 | 0.0003 / 0.0003 / 0.0011 | 32 → 32 |
| 10,000 / 1,000 | 1,000 | 23 | 0.0273 / 0.0635 / 0.5621 | 0.0016 / 0.0018 / 0.0025 | 152 → 152 |
| 10,000 / 5,000 | 5,000 | 136 | 0.1397 / 0.3328 / 1.9137 | 0.0071 / 0.0073 / 0.0117 | 152 → 152 |
| 50,000 / 100 | 100 | 4 | 0.0022 / 0.0022 / 0.0051 | 0.0002 / 0.0003 / 0.0024 | 32 → 32 |
| 50,000 / 1,000 | 1,000 | 21 | 0.0262 / 0.0575 / 0.2420 | 0.0020 / 0.0021 / 0.0490 | 152 → 152 |
| 50,000 / 5,000 | 5,000 | 123 | 0.1440 / 0.2388 / 3.5540 | 0.0103 / 0.0120 / 0.0223 | 152 → 152 |

Plan target (section 6): selection p95 ≤ 0.05 ms at 5,000 active journeys. Met: 0.0073 ms (10,000 persons) and 0.0120 ms (50,000 persons). Selection no longer depends on the number of active journeys city-wide, only on how many are near the focus. The grid query itself allocates nothing; the 32/152 bytes per call are the same in both arms and come from the fixture's candidate path.

**Dense worst case.** Every active person inside the radius (150 m city), 10,000 persons. The query returns the whole index, so the grid adds its query and deduplication on top of the same filter work:

| Active | F5f p50 / p95 | Grid p50 / p95 | Query alone p50 / p95 |
| ---: | --- | --- | --- |
| 100 | 0.0131 / 0.0133 | 0.0201 / 0.0220 | 0.0005 / 0.0006 |
| 1,000 | 0.2533 / 0.3186 | 0.2628 / 0.4499 | 0.0067 / 0.0075 |
| 5,000 | 1.3607 / 5.1929 | 1.7487 / 5.7339 | 0.0463 / 0.0757 |

The query costs 0.05–0.08 ms at 5,000 people in one place, against 1.4–5.7 ms for the unchanged filter work in both arms. A city with 5,000 people inside 120 m of the player is outside the default caps' practical range; it is listed as the bound.

## Grid filing per route commit

One travelling member's journey replaced by a fresh two-leg route of about 530 m (about 10 cells) and re-filed through `ActiveJourneyIndex.File`: p50 0.0006–0.0010 ms, p95 0.0006–0.0010 ms, 0 bytes at 1,914 / 10,000 / 50,000 persons. Filing happens only at event commits that change phase or journey, never per frame.

Index rebuild (construction, load, route rebuild), p50 / p95 ms. The baseline is the F5f index alone as recorded at its merge ([F5f perf](f5f.md#index-maintenance-and-shadow-comparison-ms-second-run), same machine, quieter run). "Short legs" is this build on the F5f fixture (single 5 m legs, one cell each); "spread" is this build on the F2f fixture above:

| Persons (1,000 active) | F5f at merge | This build, short legs | This build, spread |
| ---: | --- | --- | --- |
| 1,914 | 0.039 / 0.042 | 0.195 / 0.258 | 0.558 / 0.882 |
| 10,000 | 0.170 / 0.177 | 0.271 / 0.632 | 0.618 / 1.446 |
| 50,000 | 1.062 / 2.210 | 1.367 / 2.639 | 1.906 / 4.707 |

Rebuild runs once per construction, not per frame; 0 bytes after warm-up.

## Shadow check (runtime, every 30 s)

`ActiveJourneyIndex.MatchesScan` now includes the grid's anchor and slot check. p50 / p95 ms, with the same baseline and fixture labels as the rebuild table:

| Persons / active | F5f at merge (p95) | This build, short legs | This build, spread |
| --- | --- | --- | --- |
| 1,914 / 100 | 0.028 | — | 0.045 / 0.057 |
| 1,914 / 1,000 | 0.033 | 0.083 / 0.192 | 0.114 / 0.184 |
| 10,000 / 1,000 | 0.168 | 0.228 / 0.420 | 0.248 / 0.390 |
| 10,000 / 5,000 | — | — | 0.547 / 1.805 |
| 50,000 / 1,000 | 1.832 | 1.504 / 2.852 | 1.395 / 2.732 |
| 50,000 / 5,000 | — | — | 1.956 / 5.510 |

At 50,000 persons most of the cost is F5f's existing scan of every citizen, which this slice does not change. The check runs once per 30 s, allocates nothing, and had no sample ≥ 30 ms (largest 9.1 ms at 50,000 / 5,000 on this loaded machine).

**Cheap win taken.** The first version also recomputed every member's cells inside the runtime check, and looked each cell up by key. Measured before/after on the same fixture, p50 / p95 ms:

| Persons / active | Recompute + key lookups | Anchors + stored list references |
| --- | --- | --- |
| 10,000 / 5,000 | 2.64 / 3.73 | 0.55 / 1.80 |
| 50,000 / 5,000 | 4.14 / 5.95 | 1.96 / 5.51 |

The runtime check now compares each member's anchor (journey reference or place position, which is what a missed filing leaves behind) and walks the slot back-pointers through list references stored on the entry. The full recompute stays available as the internal `Matches(members, recompute: true)`, which the tests and the regression tool call.

## Gate

Mod-work p95 < 5 ms: the selection component is under 0.013 ms p95 at every spread size; the 30 s shadow check is one sample per 30 s, not per frame. No offline sample ≥ 30 ms. In-game mod-work and whole-frame timing: pending rig checks 1–2.
