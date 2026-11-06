# E5c product metadata defaults - 2 November 2026, offline only

## Roadmap perf-log entry (moved 6 Nov 2026)

- E5c - [Load-time duplicate-field mask and metadata measurements](e5c-neutral-metadata.md): 256-entry parse 289,488 -> 195,280 B; resolver and 4/8-offer hot choice work 0 B, no frame work.

Reproduce with `python tools/food-metadata-regression/prepare.py`, the prescribed other-dotnet guard, then Release `tools/food-metadata-regression/FoodMetadataRegression.csproj`. Canonical sample: the successful required-repeat `food-metadata` arm in `build/d4-validation/23.txt`, with the final large-integer budget fix. The earlier D3a sample is retained in the same raw JSON for comparison, including its flagged startup interval. [Raw measurements](../checks/e5c-neutral-metadata-measurements.json) and the [validation manifest](../checks/e5c-neutral-metadata-results.json) retain values/log hashes. No native evidence is claimed.

The choice comparator is frozen base `034bb76`. The config before arm is a mechanical, policy-matched reconstruction using the original per-entry duplicate-field HashSet instead of the current five-bit mask; both arms use the same current byte budget, Food default and optional seeded-taste metadata. This isolates the allocation change rather than comparing different policies. A fresh reader/table is created for every parse. 0/1/256 short-name entries and 256 maximum-length escaped names are measured; the last uses two fields per entry, the ordinary full table five.

Warm resolver/choice numbers are 1,001 samples of 100 calls, reported as **batch-average ms/call**, not individual-call percentiles. Parse numbers are 101 individual calls with warm CLR/JIT/Json.NET contracts. Warmup precedes counters. Every full batch is checked for >=30 ms; averaging cannot conceal such a measurement interval. GC counts and allocation exclude setup; retained bytes are approximate whole-heap full-GC deltas outside timed windows, excluding the retained input JSON. Tiered JIT/order/host noise affects timing, so no latency win is claimed from this single sequential experiment.

## Resolver and choice

| Overrides / path | p50/p95/max ms/call | B/call |
| --- | ---: | ---: |
| 0 / lookup (seeded miss with empty table) | 0.000438/0.000502/0.030091 | 0 |
| 0 / unconfigured seeded name | 0.000282/0.000317/0.000574 | 0 |
| 0 / 256-unit unconfigured seeded name | 0.002325/0.002882/0.011452 | 0 |
| 1 / lookup (seeded miss with empty table) | 0.000106/0.000110/0.000233 | 0 |
| 1 / unconfigured seeded name | 0.000051/0.000052/0.000091 | 0 |
| 1 / 256-unit unconfigured seeded name | 0.000411/0.000421/0.000952 | 0 |
| 256 / lookup (seeded miss with empty table) | 0.000015/0.000016/0.000124 | 0 |
| 256 / unconfigured seeded name | 0.000051/0.000052/0.000087 | 0 |
| 256 / 256-unit unconfigured seeded name | 0.000412/0.000415/0.000960 | 0 |

| Eight shops x width / operation | Before p50/p95/max ms | After p50/p95/max ms | B/call before -> after |
| --- | ---: | ---: | ---: |
| 4 / historical full selection | 0.001407/0.006381/0.009488 | 0.001613/0.004732/0.007871 | 0 -> 0 |
| 4 / metadata resolve + all 32 weights (no selection) | n/a | 0.002852/0.003344/0.009147 | n/a -> 0 |
| 8 / historical full selection | 0.002856/0.003865/0.005239 | 0.003152/0.004382/0.005214 | 0 -> 0 |
| 8 / metadata resolve + all 64 weights (no selection) | n/a | 0.004258/0.004805/0.007271 | n/a -> 0 |

Original historical weights are asserted exactly equal before timing. Original-three unit proofs also pin saved traits/draws. Historical full selections and new metadata resolve/weight loops have **0 B/call**, with no GC in their measured windows. The new path is pure and not called by native choice. Different work is explicitly labeled; a weight-only loop is not a full held decision or an ordinary-play mod-work frame.

## Config parse and allocation win

| Entries / names | Before table p50/p95/max ms | After table p50/p95/max ms | Table B/call before -> after | Full fresh config p50/p95/max ms; B/call |
| --- | ---: | ---: | ---: | ---: |
| 0 / short | 0.000500/0.003500/0.005600 | 0.000500/0.003300/0.006100 | 2800 -> 2800 | 0.003500/0.008800/0.082100; 3904 |
| 1 / short | 0.007500/0.008300/0.008400 | 0.007600/0.008200/0.009200 | 4232 -> 3864 | 0.009700/0.010200/0.011300; 4816 |
| 256 / short | 0.233600/0.441900/0.632400 | 0.255100/0.306800/0.356400 | 289488 -> 195280 | 0.191100/0.325600/0.628900; 196232 |
| 256 / maximum escaped | 0.754900/1.522100/1.668500 | 0.747100/1.544600/1.744700 | 314240 -> 269184 | 0.793300/1.595500/1.824000; 270136 |

The mask removes **368 B per full five-field entry**: 256-entry parsing **289,488 -> 195,280 B** (94,208 B saved); empty tables stay **2,800 B**. For the two-field maximum-name fixture it saves **45,056 B** (314,240 -> 269,184). This is a load-only allocation win; no hot cache, watcher, polling, native setup, route work or diagnostic strings are added. Repeated five-bit tests preserve exact ordinal duplicate/unknown-field refusal.

| Entries / names | Config escaped characters / UTF-8 bytes | Approx. retained table B | Measured full-config GC0/1/2 |
| --- | ---: | ---: | ---: |
| 0 / short | 27 / 27 | 112 | 0/0/0 |
| 1 / short | 150 / 150 | 360 | 0/0/0 |
| 256 / short | 32172 / 32172 | 37504 | 1/0/0 |
| 256 / maximum escaped | 399130 / 399130 | 153224 | 2/0/0 |

**Flagged startup interval:** the first process use of a 256-entry short-name config, including CLR/JIT/Json.NET contract initialization, took **59.0058 ms**, **757,168 B**: **one >=30 ms startup spike**. It is outside ordinary-play work. No warm resolver/choice batch or fresh parse sample reached 30 ms. The earlier D3a sample also flagged its first-use interval at **57.6619 ms / 759,376 B**: two startup intervals >=30 ms across the two required executions, one in each. Neither run had a warm batch/parse interval >=30 ms. Raw data retains both samples and both batch and averaged counters. These measurements do not certify the combined native mod-work gate.

Writer 24 and held bytes are unchanged. Persisted per-offer effective-input sizing/replay belongs to E5d and later E5b integration; none is measured here. There is no new native setup/route call or processing backlog in pure E5c. Ordinary native mod-work **p95 <5 ms**, every mod-work spike **>=30 ms** flagged, whole-frame cost/stalls separately tracked, and autosave-controlled matched windows remain checks for the next batched headless rig. Physical population, closure refresh, optional-stop refresh and save serialization remain parked.
