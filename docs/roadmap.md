# Persistent City roadmap (Peter's answers, 2026-10-30 15:17–15:24 JST, guided Q&A with Spock)

Replaces the phase order in docs/reviews/2026-10-27-realism-gameplay-roadmap.md where they differ. Same principles still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals.

## Vision
"Both equally: a living city that also drives my business." It should feel like a deep optimization game, not formula numbers you work out in the first 5 hours.

## Product decisions
1. **Choice is probabilistic, not a fixed score.** Preferences raise the chance for an item. Distance raises the chance for nearby shops but never rules out distant ones. Keep it deterministic per save through seeded draws, so a reload replays the same day.
2. **Resident inspector first.** Click a resident to see where they live and work, what their preferences are, and what they ate or skipped and why.
3. **Individuals are described in words, not numbers** (e.g. "loves burgers", "price-sensitive", "hates waiting"). City-wide statistics come from **paid market research through marketing agencies**, which gives players a new thing to spend money on.
4. **Traits, core plus lifestyle:** food preferences, price sensitivity, patience (queues), loyalty vs curiosity, willingness to travel, health-conscious, brand or quality focus, time pressure (commuters).
5. **Memory is driven by the loyalty trait.** Loyal residents keep going back to the same shops, curious ones try new places. Bad visits (stockout, closed, queue) lower the chance of returning, weighted by traits. Memory stays bounded, about 4 shops per consumer. The slice re-measure checks the cost.
6. **Marketing means awareness plus targeting.** Campaign types reach different resident groups (commuters, health-conscious, ...). Awareness only makes a visit possible; traits still decide.
7. **Rivals are real alternatives.** Residents can choose them over Peter's shops. Rivals are read-only with an outflow line, and nothing native is written.
8. **Weather, street life and bigger menus come after the core loop works.**

## Order
**Peter 15:25 JST: "Release will only happen at the end."** There's no interim release. Steps A–E build on the development line (still merged through the normal gates), and the single Workshop release comes after the full roadmap. Performance stays a standing requirement that is re-checked as features land, and it must pass before that final release.

0. Performance: the 30 October comprehensive review is done and passes the mod-work gate below. Spock agreed: no optimization work now; re-measure as each slice lands. This isn't a release step.
A. Inspector, traits and probabilistic choice. Start now (Peter 15:24 JST), with the standing performance gate applied to each slice.
B. Memory, loyalty and regulars. **B0 is implemented offline:** schema 18, exact old-source migration and history-inclusive capacity; memory remains empty/inactive and the regulars helper is a pure own-shop projection. **B0b is inserted between B0 and B1:** persist the held A3 tuning image and neutral bounded per-candidate contribution; schema 18 is amended before player use. B1 is implemented offline on B0b: per-consumer generation memory, atomic experience watermarks and captured bounded loyalty/curiosity weights; 6 Nov native proof remains pending. B2 UI remains pending. [Contract and capacity evidence](population-schema18.md).
   B1 protected-insertion exception: if recording a new bad visit would evict a competing boost, return chance stays equal (never higher), with a terminal failed watermark and no negative per-shop entry. B2 must not assume every failure leaves a negative entry; `ProtectedInsertions` is session-local, not saved history. [Exact exception](population-schema18.md#b1-execution--31-october-2026).
C. Marketing awareness and targeting, plus the market research product.
D. Rivals as alternatives.
E. Weather, street life, bigger menus and more products.
Then: re-check the same performance gate for the final release, the hands-on session, and one release.
Later: residents as employees, households.

## Performance gate (Peter, 2026-10-30)

Applies to every step A–E slice re-measure and to the final release:

- **Mod-work p95 must stay under 5 ms**, leaving a buffer under the 10 ms frame budget for 100 fps.
- Single mod-work spikes **under 30 ms are acceptable**. Flag every mod-work spike **of 30 ms or more** in the re-measure report.

**Baseline: PASS.** The [30 October review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) on `codex/pc-perf-review-2026-10-30` records primary dependency-only mod-work p95 **1.259 ms** (highest pooled condition; worst individual window **2.420 ms**) and worst mod-work spike **24.0 ms**. Both pass. If mod p95 climbs, look first at hotspot #1, physical population (**p95 0.64 ms / max 21.7 ms**).

**Whole-frame watch item.** Unexplained whole-frame stalls are tracked separately from the mod-work gate, including the review's four **104–175 ms** evening stalls. Their cause remains unresolved.

**Standing practice.** Being under 5 ms is never a reason to skip obvious improvements. Every slice's implementation and review must look for cheap, low-risk performance wins (redundant per-frame work, caching, avoidable allocations, refresh rates that can safely drop) and take them in that slice when low-risk and measurable. The review's four hotspots—physical population, closure refresh, optional-stop refresh and save serialization—stay parked as high-risk unless a cheap, safe variant turns up, such as lowering closure or optional-stop refresh frequency where correctness allows.

### Perf wins log

Record every taken win with before/after numbers.

| Slice | Change | Before | After |
|---|---|---|---|
| B1 round 3 | Reuse `FoodBusiness.OpeningPlace` for arrival closure, represented admission and reached-stop hours checks | 3,216 allocated bytes per warmed batch across the three sites (three temporary places and interval builds) | 0 bytes per warmed batch; 10,000 batches /32,160,000 -> 0 bytes on offline .NET 10. Identical open/admission/close results and in-place/replacement hours mutation checks pass in `FoodMemoryOpeningCacheTests`; no native timing claim. |
| B1 | Compare watermark meal codes as integers, avoiding enum boxing | ~120 bytes/commit in boxed prototype | 72 bytes/commit; 48 bytes removed, 0 lookup allocation (40 measured rounds at 1,914/5,000 consumers; offline .NET 10). [Raw measurements](reviews/2026-10-31-b1-measurements.json) |
| B1 | Reuse the existing entry and watermark for ordinary repeat-visit commits | 72 allocated bytes/commit after boxing fix | 0 bytes per warmed repeat commit at both populations; first-use state can allocate. Same full suite and canonical replay pass; [final/intermediate measurements](reviews/2026-10-31-b1-measurements.json) |
| B1 | Reuse each selected shop's captured memory scalar from shortlisting through final choice | Second generation lookup + up to four entry checks for each of eight shortlisted shops | 8 repeated dictionary lookups / up to 32 repeated entry checks removed per full context; exact scalar replay, no wall-time gain claimed |
| B0b | Nonmaterializing streaming shape precheck for the new held tuning image, before full graph decode | Materializing prototype: 136 bytes/check, median 1,375.69 ns | 0 bytes/check, 187.79 ns (three fresh processes, 300k calls/run; offline .NET 10). Continuation allocation unchanged; [raw B0b measurements](reviews/2026-10-31-b0b-measurements.json) |
| A2 | Save validation builds error text only on failure (`Text`/`Bounded`/`Known`/duplicate/unknown-field checks); runs on every save and load | Validation alloc per save **8.39 MiB** (957 residents with journeys, offline .NET 10) | **1.31 MiB** |
| A2 | Read shape check: one reusable duplicate-field set per JSON depth, bit flags per position, integer range from the token value; positions streamed instead of one `JObject` each | Read alloc **83.3 MiB**, read median **119.7 ms**; write alloc **26.1 MiB** (same payload) | **38.8 MiB**, **89.2 ms**; **19.1 MiB** |
| A2 r2 | Schema-17 outcome records as one compact string each (no field names) | 4,038,976 chars at 957 residents end of day; 50,000 × factor 3 above the input cap | 3,470,518 chars (−14.1%); 50,000 × factor 3 at 89.7% of the cap |
| A2 r2 | Unboxed range checks for schema-17 enums in save validation | Validation 1.83 MiB alloc, 4.3 ms per save (957 residents, end of day) | 1.04 MiB, 2.7 ms |
| A2 r1 | Schema-17 outcomes: whole-minute integer times, shop/product kept only for purchases | Bought record 210 chars, skipped 196 (full-precision minutes, shop/product on skips) | 182 and 122 chars (−13% / −38% per record) |
| A2 | Schema-17 traits stored as one 0–4 digit per trait (`{"Bands":"…","Version":1}`) instead of ten named integer fields | 208 chars per trait record (+333,036 chars at 957 residents × 2 consumers) | 34 chars per record (174 fewer; 1.74 M chars saved at 5,000, 17.4 M at 50,000) |
| A3 | Reuse the existing exact-hours place cache per business observation across menu feasibility checks; mutable/replaced hours still invalidate | 8-shop decision: 254,728 bytes; p95 0.735 ms | 117,704 bytes; p95 0.300 ms (offline .NET 10; synthetic routes) |
| A3 | Compare pinned logical-stay fields across quote units instead of repeating SHA-256; reload checks the canonical key once | 117,704 bytes; 8-shop p95 0.300 ms | 104,656 bytes; p95 0.169 ms in that run; larger-market timings noisy (offline, same harness) |
| A3 | Avoid exponentiation by one for default weights/temperatures, preserving exact weights | 1,024-shop + 24-offer default decision: 1,072 `Math.Pow` calls | 0 calls; non-unit config still uses `Math.Pow` (call-count reduction; no noisy wall-time claim) |
| A3 | Project immutable travel bands/coefficients once before shortlist iteration | 1,024 projections per 1,024-shop decision | 1 projection (1,023 repeated projections removed); exact coefficient vector and seed winners remain pinned |
| A3 r1 | Refresh ownership/hours once per atomic eight-shop capture; cache owned scope lookup and unchanged menus during the fresh full metadata pass | 8 full hours passes; interleaved 131-controller capture median 0.1041 ms | 1 pass; 0.0754 ms (64 warmed samples, synthetic native surfaces, actual metadata/hours code). Stable scope lookup changes 131 list scans to 131 dictionary lookups; no repeated stable-menu arrays |
| A3 r1 | Coalesce no-work/refused-return/present-route slots and select with the last quote; retain selected-before-commit save boundary | Full / closed / refused-outward / existing-shop decisions: 19 / 19 / 19 / 5 events | 18 / 3 / 10 / 3 events. At 957 residents: event-cap frames 2,273 -> 2,154 full, 2,273 -> 359 closed, 2,273 -> 1,197 refused, 300 -> 180 existing; fastest synthetic full-route lunch still has backlog |
| A3 r1 | Reuse bounded materialized captured businesses and their exact-hours caches through evaluation/selection; fewer continuation units | Prior native-length-ID decision allocation at 8 / 64 / 1,024 scopes: 99,280 / 99,400 / 105,400 bytes | 83,920 / 84,040 / 80,568 bytes with observer enabled. Full-decision p95 0.263 / 0.207 / 0.331 ms; no wall-time gain claimed across noisy runs. [R1 measurements](reviews/2026-10-31-a3-r1-measurements.json) |
| A3 r2 | Count a food decision once as a citizen event; report bounded quote/selection/commit units separately under the unchanged 128-unit/2 ms caps | Matched 957-resident full-route lunch median 2,157 frames; fastest modeled clock pending [583, 583, 583] | 274 frames; pending [0, 0, 0]. All four scenarios drain under every modeled clock rate; exact saved-state hashes and <=1 food quote/unit preserved. [R2 measurements](reviews/2026-10-31-a3-r2-measurements.json); native timing still required |
| B0 | Nonmaterializing outcome and memory shape prechecks; validate bounded tuple arity before creating children | 552 bytes/outcome and 352 bytes/memory precheck in the preserved prototype | 0 bytes for both new shape prechecks (500k calls); outcome 126.16 -> 50.24 ns, memory 328.32 -> 280.61 ns in the 5k comparison; no native timing claim |
| B0 | Cached typed primitive access in the new budget/meal tuple converters; direct scalar writer/reader dispatch | Equal-wire 5k prototype: write/read alloc 88,360,664 / 199,517,384 bytes; medians 109.36 / 316.11 ms | 79,001,488 / 173,354,440 bytes; 90.34 / 274.09 ms, seven interleaved measured rounds; byte equality asserted; [raw measurements](reviews/2026-10-31-b0-measurements.json) |
| B0 | Count UTF-8 in bulk string/array writes with tested split-surrogate carry, instead of per-character loops | 256 Mi ASCII-character count: 249.43 ms | 5.42 ms in the 5k comparison; same 268,435,456 chars/bytes; save preflight only, not a serializer traversal rewrite |
| B0 r1 | One-pass early capability probe, bounded direct header validation, and Schema/Capabilities first in new root writes; full decode unchanged | Actual previous-order reference/ceiling probe median **31.8863 / 1,827.6675 ms**, allocation **27,121,800 / 801,032,696 bytes** | New header-first **0.0201 / 0.0255 ms**, **2,968 bytes** each; fresh-context first calls **6.5292 / 11.7447 ms**. Previous-order fallback **16.1228 / 848.7131 ms**, >=30 ms calls flagged as load latency. [R1 measurements](reviews/2026-10-31-b0-r1-measurements.json); no native timing claim |
