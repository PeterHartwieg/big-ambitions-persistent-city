# E5a detached eligibility performance

## Roadmap perf-log entry (moved 6 Nov 2026)

- E5a — [Reuse bounded holder capacity and ordinal-key identity proof](e5a-catalogue-eligibility.md): E5a eight-offer rebuild 300,280 -> 129,144 B; clean reads 0 B/scans; initial 32.5793 ms backlog spike retained in evidence.

## 2 November 2026 — offline native doubles only

Release .NET 10, foreground sequential workloads on the shared Windows machine, with the required other-dotnet guard. Fixture construction is excluded. Fresh apphosts provide one cold sample per condition (including JIT/static metadata); no cold percentile is inferred. Warm distributions use 20 warmups and 101 rebuild samples / 1,001 cache samples. The reader has no production caller; there are no added frame scans, reflection, polling or allocations. A clean explicit `TryGet` plus `Continue` control verifies zero allocations and unchanged native lookup/scan counters. All native setup and route counts are zero; whole-frame and combined native mod-work are unmeasured.

Warm full catalogue+shop rebuilds use 21 samples per matrix cell, separately from warm shop-only rebuilds. Dirty storms send 1,000 catalogue or shop marks before draining one coalesced rebuild. Cold allocated bytes include the harness's small continuation timing buffer; warm per-call bytes exclude the distribution sample buffer. The baseline reconstruction removes only the ordinal-key proof reuse, exact holder-list capacity and empty-array-wrapper sharing, using the same final-head cases and runtime code. Source transformations/hashes are retained in the machine receipt.

The 2-holder / 3-or-8-product cases deliberately **refuse**: one counter and one single-cargo showcase cannot establish three/eight complete offers. Positive small menus are measured separately at 4/9 total holders. Empty catalogue cases are missing-definition refusals. The 2,000-holder / 3-or-8-product cases have separate same-ID cargo slots and one shared bag counter. Catalogue sizes are exactly 0, 16 and 16,384; no real native catalogue-size claim is made.

## Cheap measured improvements

The first E5a implementation redundantly rebuilt a furniture-ID hash set even though the already checked default/ordinal native dictionary and key == instance ID prove uniqueness. Runtime now supplies that explicit proof to Core; pure callers without it retain duplicate detection. Allocate holder-result list capacity once from the already bounded native count, after menu validation, rather than repeatedly growing the list. Empty detached whitelist/output arrays also share one immutable wrapper per process. No E3 source/read branch/pin is changed, and no mutable native value is cached as a fallback.

Matched initial/optimized eight-product, 2,000-holder, 16-definition E5a rebuild: **300,280 -> 129,144 B**, warm p50/p95/max **0.4898/0.8887/13.4199 -> 0.2145/0.3853/1.2967 ms**. GC generations 0/1/2: **2/1/0 -> 1/0/0** over 101 samples. Timing is descriptive on a shared host; allocation reduction is the deterministic improvement. The 1,024-shop/16,384-definition backlog used **310,191,088 -> 134,159,312 allocated B**, **116,049,664 -> 116,044,384 retained B**, 16,449 units in **922.0204 -> 464.8757 ms** (1,110.6 -> 2,202.7 shops/s), p95 **0.1861 -> 0.1021 ms/unit**. Chunking changes completion latency and never hides backlog.

**Flagged initial E5a spike:** the first 1,024-shop backlog recorded **one 32.5793 ms continuation**, with GC/host scheduling not individually attributed. Optimized repetition recorded max **20.9713 ms**, zero >=30 ms units. This does not erase the initial spike or prove native clearance. Initial cold full completion totals also exceeded 30 ms at 2,000 holders / 16,384 definitions (**30.5919 / 35.9274 ms** for 3/8 products); these are sums across **81 continuations**, not single mod-work spikes. Initial warm one-product 2,000-holder total rebuild p95 **6.6411 ms** is similarly an aggregate offline cost, not a frame percentile.

Eight frozen E3 facades on the *same mixed* 2,000-holder menu allocate **50,936 B/aggregate** (not the plan's hypothetical eight-times-single-product 392,832 B). Each facade retains only its own product's equipment, whereas the new image retains all holder/resource identities and full-menu validation: optimized new rebuild still allocates **129,144 B**. Do not claim an allocation win over those smaller diagnostic outputs. Their same optimized-run p50/p95/max are **0.4028/0.5410/0.6582 ms**, versus E5a **0.2145/0.3853/1.2967 ms**, zero >=30 ms samples. E5a makes one furniture pass instead of eight, and subsequent clean reads allocate **0 B**. The baseline is an observed aggregate, not a sum of percentiles.

## Final-head matrix and limits

Final-head evidence is [the machine receipt](../checks/e5a-offline-results.json): 65/65 D3a commands, 59/59 D4 commands, five outer stages, 2,556 unit tests/discovered cases and 2,744 E5a assertions. The separate reconstructed baseline also passed 2,744 assertions. All 107 parsed offline flags retain log/field/condition context; they include aggregate/cold totals and legacy stress costs, not 107 native spikes. Retained memory is a `GC.GetTotalMemory(true)` live-heap delta, not an exact object graph or native/Unity heap measure; small negative rebuild deltas are collection noise. The 1,024-shop stress reuses one double registration to isolate cache/backlog costs; it is not a city-scale native fixture. Cached images retain approximately 116 MB at this deliberately saturated holder count; this is a bounded worst-case cache cost, not zero retained memory.

The normal combined rig must still meet **mod-work p95 <5 ms**, flag each >=30 ms spike and report whole-frame stalls separately. Autosave must be disabled/verified before every native measured window and restored afterward; saves stay outside timing windows. Physical population, closure refresh, optional-stop refresh and save serialization remain parked and unchanged. No special performance-only rig run is planned.


### Controlled final-head comparison

Reconstruction before / final after, one complete eight-product / 2,000-holder shop rebuild: p50 **0.4583 / 0.4046 ms**, p95 **0.4877 / 0.4434 ms**, max **1.5549 / 3.6855 ms**; **300,280 / 129,144 B**. Both have zero >=30 ms samples. The controlled backlog completed in **423.4626 / 507.1596 ms**, with **310,190,760 / 134,159,408 allocated B**, **116,050,240 / 116,043,752 retained B**. Before was faster in this backlog repetition: no dependable timing or throughput win is claimed. Allocation savings reproduce under the exact four textual edits representing the three changes; their before/after source hashes are in the receipt.

Final 1,024-shop backlog: **16,449 units**, p50/p95/max **0.0107/0.1777/20.8730 ms**, **2019.1 shops/s** CPU throughput, zero pending work at completion, zero >=30 ms units. These units are drained synchronously only by the offline benchmark; they do not establish frame scheduling or native completion latency. The initial 32.5793 ms unit remains flagged above.

Final eight frozen facades: p50/p95/max **0.7312/0.8220/0.9015 ms**, **50,936 B**. Final 1,000-mark shop/catalogue storms: p95 **0.4645/0.4390 ms**, **129,144/131,224 B** per coalesced complete rebuild. Clean cache controls across every matrix cell remain **0 B and zero added scans/lookups**. Native setup/route counts are zero.

### Cold matrix

One fresh process/sample per row; CPU total is across all units, not a frame or percentile. No final cold unit reached 30 ms.

| Products | Holders | Catalogue | Total ms | Largest unit ms | Allocated B | Units |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 2 | 0 | 4.1958 | 2.1741 | 4,312 | 2 |
| 3 | 2 | 16 | 6.1630 | 3.0209 | 6,472 | 2 |
| 3 | 2 | 16,384 | 13.5773 | 3.0073 | 1,866,200 | 66 |
| 8 | 2 | 0 | 4.2198 | 2.2356 | 4,504 | 2 |
| 8 | 2 | 16 | 5.7542 | 2.8607 | 6,704 | 2 |
| 8 | 2 | 16,384 | 13.7008 | 3.1365 | 1,866,432 | 66 |
| 3 | 2,000 | 0 | 6.1205 | 3.2375 | 4,312 | 2 |
| 3 | 2,000 | 16 | 14.3464 | 4.8442 | 289,184 | 17 |
| 3 | 2,000 | 16,384 | 17.1656 | 3.6198 | 2,148,392 | 81 |
| 8 | 2,000 | 0 | 4.9894 | 2.4940 | 4,504 | 2 |
| 8 | 2,000 | 16 | 7.4117 | 2.5161 | 289,656 | 17 |
| 8 | 2,000 | 16,384 | 15.0771 | 2.6380 | 2,148,864 | 81 |

### Warm matrix

Each timing cell is p50/p95/max ms. Shop-only uses the cached catalogue; catalogue+shop is a complete multi-unit rebuild. **16,384-definition aggregate p95 values exceed 5 ms (up to 15.0179 ms); they must not be treated as a single frame budget or scheduled as one synchronous native frame.** All final single-distribution maxima are below 30 ms; clean cache controls are 0 B throughout. Retained values are heap deltas with the qualifications above. GC counts and all one-product control rows are preserved in the receipt.

| Products | Holders | Catalogue | Shop-only ms; B | Catalogue+shop ms; B | Cache retained B |
| ---: | ---: | ---: | --- | --- | ---: |
| 1 | 2 | 0 | 0.0005/0.0010/0.0078; 368 | 0.0006/0.0017/0.0026; 488 | 2,824 |
| 1 | 2 | 16 | 0.0041/0.0078/0.0108; 664 | 0.0162/0.0221/0.0268; 2,688 | 4,744 |
| 1 | 2 | 16,384 | 0.0042/0.0082/0.0104; 664 | 7.1277/11.0757/11.2237; 1,860,412 | 1,411,408 |
| 1 | 2,000 | 0 | 0.0003/0.0007/0.0013; 368 | 0.0004/0.0012/0.0027; 488 | 2,848 |
| 1 | 2,000 | 16 | 1.0531/1.4008/1.4407; 128,536 | 0.6456/0.7108/0.7753; 130,560 | 197,072 |
| 1 | 2,000 | 16,384 | 0.3312/0.4607/0.7257; 128,536 | 2.7292/8.4648/14.4980; 1,988,185 | 1,604,216 |
| 3 | 2 | 0 | 0.0002/0.0003/0.0010; 448 | 0.0002/0.0003/0.0010; 568 | 2,848 |
| 3 | 2 | 16 | 0.0005/0.0012/0.0017; 584 | 0.0021/0.0028/0.0032; 2,624 | 4,432 |
| 3 | 2 | 16,384 | 0.0005/0.0014/0.0017; 584 | 2.6665/12.9949/13.8253; 1,860,250 | 1,411,168 |
| 3 | 2,000 | 0 | 0.0002/0.0004/0.0019; 448 | 0.0003/0.0003/0.0011; 568 | 2,848 |
| 3 | 2,000 | 16 | 0.4048/0.4463/1.3288; 128,712 | 0.3543/0.3585/0.3634; 130,752 | 197,184 |
| 3 | 2,000 | 16,384 | 0.4088/0.4713/1.0303; 128,712 | 1.5914/15.0179/17.9887; 1,988,378 | 1,604,328 |
| 8 | 2 | 0 | 0.0001/0.0005/0.0011; 640 | 0.0002/0.0005/0.0006; 760 | 2,848 |
| 8 | 2 | 16 | 0.0005/0.0007/0.0008; 776 | 0.0016/0.0021/0.0031; 2,856 | 4,472 |
| 8 | 2 | 16,384 | 0.0005/0.0010/0.0013; 776 | 2.3914/12.9565/16.4177; 1,860,486 | 1,411,208 |
| 8 | 2,000 | 0 | 0.0002/0.0008/0.0011; 640 | 0.0003/0.0008/0.0008; 760 | 2,848 |
| 8 | 2,000 | 16 | 0.4874/0.5201/0.7368; 129,144 | 0.4582/0.4626/0.4679; 131,224 | 197,416 |
| 8 | 2,000 | 16,384 | 0.2718/0.4286/0.6166; 129,144 | 2.4338/11.2324/12.9759; 1,988,851 | 1,604,608 |

Positive small complete menus: 3 products/4 holders p50/p95/max 0.0011/0.0021/0.0028 ms, 968 B; 8 products/9 holders p50/p95/max 0.0024/0.0033/0.0039 ms, 1,720 B.

### Separate inherited stress flags

The receipt keeps the unchanged legacy stress costs separate: D1 20,000-place cold aggregate **45.3129 / 49.1595 ms**; C1 1,024-business cold aggregate p95 **6.2197 ms** in the first gate. E4 14,336-row serialization at four/eight offers has first-gate p95 **189.9169 / 212.3162 ms**, max **215.2308 / 241.9956 ms**, **21/21 >=30 ms samples per width**; second-gate p95 **183.9718 / 213.9440 ms**, max **198.5572 / 227.0492 ms**, again **21/21 per width**. These are offline serialization/discovery stress distributions, not E5a continuation or native whole-frame evidence. All remaining parsed flags retain their metric path and log hash in the receipt; none authorizes a rewrite of the four parked hotspots.
