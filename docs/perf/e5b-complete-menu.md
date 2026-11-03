# E5b complete-menu measurements

## 2 November 2026 — offline components only

Native dynamic integration is incomplete; see [the contract gap and six automated rig checks](../checks/e5b-complete-menu.md). These are foreground native doubles and pure policy measurements, not ordinary-play combined mod-work or whole-frame samples. The standing gate remains **combined mod-work p95 <5 ms**, with every spike **>=30 ms** flagged; whole-frame cost/stalls are separate. No game, rig profile, native rush/soak, autosave setting or lease was used.

## Cheap measured win: discovery bag policy and duplicates

The comparison in `tools/food-checkout-regression/E5bBagPolicyChecks.cs` reconstructs the incoming `Supported` expression against the same original-three native doubles. It includes LINQ duplicate/known-product checks, three type lookups and native `HasTag`. The candidate uses bounded duplicate loops, one type lookup and the prewarmed authored-tag cache. Both inputs agree on the original three and refuse duplicate menus. Separate controls prove that `Supported` never calls `HasTag` and that selected capture/checkout refuse native/authored disagreements, including a change during a native callback.

Final required-matrix repeat: each arm warms 1,000 calls, then times 10,000 individual calls using `Stopwatch`, with timer storage allocated before the allocation counter. No GC was forced. The warm cache holds one boolean per encountered business definition and one `FieldInfo` per managed type, cleared on disposal. Weak keys keep retired definitions/registrations collectible; cold binding is startup/ownership-refresh work, not discovery. The native double's `HasTag` has no real database/cache cost, so timings cannot predict a native speedup.

| Discovery | B/call | p50 ms | p95 ms | max ms | >=30 ms samples |
| --- | ---: | ---: | ---: | ---: | ---: |
| Incoming expression | 280.0128 | 0.0002 | 0.0003 | 0.0321 | 0 |
| Cached authored policy + bounded duplicates | 0 | 0.0001 | 0.0001 | 0.0004 | 0 |

The allocation reduction is the claimed low-risk win. No native combined gate claim is made. Existing product permission remains historical while the dynamic notification/holder/held-execution contract is incomplete; this optimization grants no new support.

## Detached dynamic menu/cache workload

The new regression links the **actual E5a Runtime reader** to its existing generated native doubles. It supplies eight loaded retail products, eight real same-ID cargo slots in conditional shelves, and one shared bag counter. It explicitly sends dirty notifications. The dumplings/chopped-cheese fixtures are hypothetical positive building-holder contracts, not Street Food's observed assets. An independent false native-contract input always refuses. Clean paths run 10,000 times after 100 warmups. `CompleteFoodMenu.Check` uses indexed bounded unions rather than allocating hash sets for eight offers, history capacity and 256 referenced names.

| Focused arm | B/call | p50 ms | p95 ms | max ms | >=30 ms samples |
| --- | ---: | ---: | ---: | ---: | ---: |
| Clean detached cache hit | 0 | 0 | 0.0001 | 0.0019 | 0 |
| Conditional eight-offer admission | 0 | 0.0008 | 0.0010 | 0.0086 | 0 |
| Repeated dirty mark | 0 | 0 | 0.0001 | 0.0091 | 0 |

The clean cache records **10,100 hits / zero misses**, including 100 warmups. The 10,100 warmed/measured marks coalesce to **one pending shop**, invalidate visible cache before any simulated capture/checkout, and cause no catalogue pass. A clean read/admission loop performs **zero additional definition or holder reads**. No production per-frame reader/coordinator is installed. This is evidence about the detached cache, not a claim that native notifications automatically reach it.

A **1,024-shop / nine-holder-per-shop** backlog uses **1,025 units**, one catalogue pass and 1,024 shop passes, and drains in **10.5135 ms total**. Unit p50/p95/max is **0.0096/0.0125/0.0194 ms**, with **zero >=30 ms units** and zero pending shops. This small-holder case is not the 2,000-holder ceiling. The aggregate is not a frame or single-unit measurement. E5a retains its own cold/2,000-holder/16,384-definition/backlog evidence and original flagged 32.5793 ms spike.

## Selected checkout setup reuse and limits

The existing actual checkout workload still performs **123 reads × eight shops**, three sales, **957 decision cache hits / 27 misses**, and **33 supply-reader setups versus 990 uncached**. Checkout retains a live pre-sale read, one native sale setup and live post-sale read; the pre-sale image is reused only within that same call. This is preserved earlier optimization, not another E5b win. Complete-price and current authored-bag checks add no simulator setup. Dirty price/unknown-supply refusals precede receipts and prevent a priced subset from retaining new service authority.

No native selected capture/checkout timings, retained-native-cache bytes, notification coverage, full 1,024 × 2,000-holder production coordinator backlog, combined rush/soak mod-work or whole-frame numbers are claimed. They require the missing Runtime integration and named telemetry in next-batch rig check 6. The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are untouched. Persistence and save-size/read/write behavior are unchanged, so no new serializer benchmark or schema is introduced.

The final command/log receipt and any additional regression spike flags are recorded in the owning checks file. Repeated full required matrices are validation samples, not matched native performance windows.

## Required-matrix spike flags

The new E5b detached cache/admission/dirty/backlog and authored-policy arms report **zero samples >=30 ms**. Inherited bulk/cold work remains flagged separately: awareness whole-index build has **2 / 1** such samples in D3a/D4, max **32.0423 / 37.5550 ms**; both E4 history serialization arms flag **21/21** samples in each matrix, largest **374.4829 ms**; metadata first-use config intervals are **59.7815 / 64.2844 ms**, one flag each. Each name-identity before/after write/read/activation arm flags **3/3** samples in both matrices, largest **792.8677 ms**. Cold name migrations take **1,318.9686 / 1,370.8763 ms**; those are separate startup intervals, not ordinary-play/frame spikes.

The [machine receipt](../checks/e5b-complete-menu-results.json) retains **117** duration/distribution/nonzero-spike fields with matrix log path, nested metric path and value, plus hashes of every raw command log. It preserves the other inherited C/D/E bulk observations too. Intervals and aggregates are explicitly labeled and cannot certify or fail an ordinary-play combined mod-work p95 by themselves. No whole-frame measurement was made. These flags do not authorize a rewrite of the parked hotspots.

## Review round 1: offline repeat

Review fixes isolate refusal to one business and permit fresh original-product revalidation only on advancing accounting hours after completed handoff. Recovery still requires the existing later drained boundary and no live native customers. There is no new per-frame catalogue/holder read. The two-shop/recovery harness passes 132/50 focused assertions; the final full adapter run passes 10,596, including explicit local batches. Recovery's native setup cost under many simultaneous repairs remains a named next-batch rig measurement, not a new performance-gate claim.

The serialized final adapter repeat preserves the discovery allocation reduction **280 -> 0 B/call**, p50/p95/max **0.0002/0.0003/0.0313 -> 0.0001/0.0001/0.0002 ms**, zero >=30 ms samples. Its selected workload still records **957 hits / 27 misses, 33 setups versus 990 uncached**.

For Opus-r1-4 the scan fragment compares the former predicate `Any` with the indexed loop over the same original-three menu: old/new **0.0128/0 B per call**, p50 **0.0001/0.0001 ms**, p95 **0.0001/0.0002 ms**, max **0.0176/0.0006 ms**, zero >=30 ms samples, with 1,000 warmups and 10,000 measured calls. The 128-byte total difference is a small runtime artifact; **no meaningful latency or allocation win is claimed on the .NET 10 test CLR**. This is a scan-fragment comparison, not actual native Mono `MenuStatus` or frame timing. The requested indexed implementation removes the predicate/iterator path; the native effect remains a rig measurement.

The repeated detached E5b harness retains **1,071 assertions**, **10,100 hits / zero misses**, **10,100 marks -> one pending shop**, and **0 B** on clean/admission/dirty paths. Clean/admission/dirty p95 is **0.0001/0.0009/0.0001 ms**. The nine-holder x 1,024-shop backlog drains **1,025 units in 11.2489 ms total**, unit p50/p95/max **0.0098/0.0140/0.0497 ms**, zero >=30 ms units. Clean controls read no additional definitions or holders. These are conditional reader/cache measurements, not native notification or recovery throughput.

All inherited offline flags remain explicit: awareness whole-index build has **2/3** samples >=30 ms in D3a/D4, max **34.1626/37.8771 ms**; each E4 serialization arm flags **21/21** per matrix, largest **254.8993 ms**; metadata first-use is **62.8223/61.2270 ms**, one flag each. Every name-save before/after write/read/activation arm flags **3/3** per matrix, largest **778.0114 ms**. Cold migration intervals **1,296.0918/1,235.1902 ms** are separate startup totals. [The round-1 receipt](../checks/e5b-review-r1-results.json) retains all **121** raw numeric duration/distribution/nonzero-spike fields with their paths and all raw-log hashes. None certifies ordinary-play mod-work p95; whole-frame data remains separate and unmeasured here. No parked hotspot was changed.

## Review round 2: closed-shop refusal guard

Recovery adds a temporary-closure field check to the existing established-business and other eligibility gates before attempting catalogue or supply setup for a refused business. It adds no polling, scan, allocation or clean-frame work. The four new adapter scenarios assert **zero recovery supply setups** while closed/unestablished across two advancing boundaries each; this is a safety correction, not a measured latency win. The prior native-supply shortcut also avoided setup, but incorrectly treated its zero result as successful validation. Genuine reopened repair still requires the existing on-demand setup and later drained boundary.

Native recovery-wave cost, actual tick/queue/customer ordering and ordinary-play combined mod-work remain **unverified** (Sol-r2-2). The rig must measure combined mod-work p95 against <5 ms and flag every >=30 ms spike; whole-frame cost remains separate. No new native performance result or parked-hotspot change is claimed. The round-2 command receipt retains repeated offline component measurements and inherited duration/spike flags separately from native proof.

The final repeated detached harness passes **1,071 assertions**, retains **10,100 cache hits / zero misses**, coalesces **10,100 dirty marks** and allocates **0 B/call** on clean/admission/storm paths. Their p95 is **0.0001/0.0010/0.0001 ms**, with zero >=30 ms samples. Its 1,024-shop backlog drains **1,025 units in 10.9499 ms**, unit p95/max **0.0132/0.0399 ms**, zero >=30 ms units. The actual checkout double still records **957 hits / 27 misses, 33 setups versus 990 uncached**. These repeat existing offline workloads; they neither measure native recovery waves nor satisfy the mod-work/frame gate. [Round-2 receipts and all flagged inherited durations/spikes](../checks/e5b-review-r2-results.json).

## Review round 3: combined-batch disposition

The 3 November owner decision defers native recovery-wave and ordinary-play combined mod-work measurements to the next combined locked-PC batch under merge rule v2; they do not gate this PR's merge. [Dated checklist, check 6](../checks/rig-2026-11-06-checklist.md). No native performance window completed in the earlier fixture attempts, so no native p95, maximum, spike count or whole-frame result is claimed. Combined mod-work must still be measured against p95 <5 ms with every >=30 ms spike flagged; whole-frame costs remain separate. The four parked hotspots are untouched. This documentation correction adds no production work or new measured win. Repeated offline command/log receipts are linked from the owning checks file and remain separate evidence.
