# City audit F3 performance — 3 November 2026, offline

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F3 — [Reusable posting heap, pre-sized campaign capture and bounded work](pc-f3-bounded-bulk-work.md): 48 B/posting -> no recurring node allocation; matched complete-day work 3.2406/15.7399/87.1907 -> 0.9100/5.1148/22.4029 ms at 1,914/10,000/100,000 consumers; 64-scope capture 141,472 -> 138,392 B/boundary. Exact replay hashes match; 49 stress advancement intervals >=30 ms remain flagged. R1 restores 1,024-candidate campaign coverage with resumable publication and time-aware route admission (16-path safety ceiling); native-double 957-plan burst drains at 30/60 FPS, native capture p95 6.1153 ms remains above gate. Offline only; [status and five automated next-batch rig checks](../checks/pc-f3-bounded-bulk-work.md).

Refs #90; **Review-round-1 campaign/admission changes supersede the initial limits and corresponding native-double figures below; posting measurements remain unchanged.** [correctness, scope limits and numbered batched-rig checks](../checks/pc-f3-bounded-bulk-work.md). Baseline **bd99eb3**, writer **25**, compared with this slice's source. No native performance proof. The audit report/fixture were read from PR #97 without adding them to this branch.

## Method and retained evidence

Run `python tools/bounded-bulk-regression/measure.py`. It extracts only baseline Core/Persistence into ignored `build/f3-baseline`, never switches branches, and coordinates each foreground .NET run through the required other-dotnet wait. [Metadata and JSONL](assets/f3-bounded-bulk/) retain host/runtime, work, allocations, counts and semantic hashes. Windows 11, .NET SDK 10.0.402. Identical source fixture compiles against baseline and changed Core/Persistence; no Unity/game/market API is involved in posting-only work.

Posting experiment: two consumers/resident, minimal state, next-day exact-money postings, one discarded warm day and seven complete measured days. Direct delegates exclude reflection invocation/boxing (the original audit included it); construction, completeness probes and sorting are outside each timed call. Synthetic frames admit ≤16 posting units under a soft 2 ms budget; probes are excluded. Baseline one-call days and after multi-call days have different latency granularity by design, so total completed work and total day cost are reported too. Host/JIT/GC noise, sequential size order and unequal sample counts limit comparisons; these results do not predict Mono or native FPS.

| Consumers | Before median / p95 / max call ms | After median / p95 / max call ms | Calls before → after (7 days) | Work after / call | Median B before → after |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,914 | 2.5374 / 4.4274 / 4.4274 | 0.0134 / 0.0208 / 0.0845 | 7 → 420 | 26–32 | 91,872 → 0 |
| 10,000 | 14.9262 / 18.6442 / 18.6442 | 0.0151 / 0.0229 / 0.1289 | 7 → 2,191 | 16–32 | 480,000 → 0 |
| 100,000 | 82.0918 / 119.4194 / 119.4194 | 0.0065 / 0.0111 / 0.3304 | 7 → 21,875 | 32 | 4,800,000 → 0 |

Exactly **13,398 / 70,000 / 700,000** postings complete on each head. Before allocations total **643,104 / 3,360,000 / 33,600,000 B**; after **0 / 0 / 8,136 B**. The single 8,136-byte stress call is unattributed; median zero does not mean every call allocated zero. All seven baseline stress calls are **≥30 ms**, maximum **119.4194 ms**; no after posting call is ≥30 ms. Original audit macOS median/max numbers (2.261/4.117, 13.729/15.741, 54.212/65.826 ms) belong to that different host/fixture invocation and are not a matched Windows baseline.

| Consumers | Before mean complete-day work ms | After mean complete-day work ms | After synthetic frame median / p95 / max ms | Frames/day after | Peak remaining postings after a frame |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,914 | 3.2406 | 0.9100 | 0.2149 / 0.3831 / 0.5275 | 4 | 1,402 |
| 10,000 | 15.7399 | 5.1148 | 0.2443 / 0.3563 / 0.4434 | 20 | 9,488 |
| 100,000 | 87.1907 | 22.4029 | 0.1058 / 0.1760 / 0.5902 | 196 | 99,488 |

The frame-unit cap intentionally spreads same-minute work. These are measured posting work intervals; rendered-frame delay, real calendar pace and F1's admission/backpressure policy are separate. Backlog is explicitly reported, not hidden by a lower event count.

## Actual Core meal-day replay and backlog drain

`--integrated` runs the actual scheduler from 05:00 through all meal peaks and next midnight (target minute 2,910), with household finances, two consumers/resident, work/leisure routines, three enrolled scopes, a detached stock/checkout port and deterministic walking. It uses ≤16 units and a soft 2 ms budget per synthetic frame. Cold first-use work remains included. This is one fixed-backlog run/head/size, **not a steady-state calendar-speed test**. It excludes construction, per-frame snapshot publication, serialization, traffic-graph/native work and whole-frame time.

| Consumers | Before frame advancement median / p95 / max ms | After frame advancement median / p95 / max ms | ≥30 ms before → after | Frames before → after | Total work ms before → after |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,914 | 0.0500 / 0.1518 / 20.5190 | 0.0512 / 0.1604 / 20.8352 | 0 → 0 | 2,672 → 2,682 | 257.2334 → 265.0771 |
| 10,000 | 0.0631 / 0.2327 / 17.2580 | 0.0613 / 0.2252 / 12.1579 | 0 → 0 | 13,948 → 13,989 | 1,387.6217 → 1,360.9036 |
| 100,000 | 0.0482 / 0.1527 / **110.9849** | 0.0465 / 0.1563 / **54.2938** | **39 → 49** | 111,974 → 112,360 | 13,875.7183 → 14,088.9321 |

**Stress spikes remain unresolved**, with more after intervals ≥30 ms despite a smaller maximum. An earlier exploratory changed-head run also reported **1 spike / 34.1614 ms at 10,000** and **144 spikes / 179.7187 ms at 100,000** (1,914 maximum 27.3559 ms); it predates the retained matched run and demonstrates run-to-run variation. No integrated/native gate pass or frame-rate gain is inferred from posting savings. No rewrite of physical population, closure refresh, optional-stop refresh or save serialization was taken.

Backlog maximum **1,170 game minutes**, final **0** on both heads at every size. Purchases/sales **5,077 / 26,636 / 124,970** and route/market-read counts match exactly. Final save SHA256 values are equal for each pair, retained in JSONL; writing then reading/writing each final schema-25 state is byte-identical. After replay unit totals **42,745 / 223,549 / 1,795,101** include the extra bounded posting units (more units, unchanged decisions). After throughput **161,255 / 164,265 / 127,412 units/s** is detached Core work throughput, not a native population support claim. Allocation totals before → after: **90,747,568→90,557,632**, **472,942,456→473,328,824**, **4,149,305,192→4,139,730,952 B**; mixed replay allocation is not uniformly improved.

## Indivisible-unit inventory and native admission

| Operation / entry point | Unit / boundary and admission | Tail/allocation evidence or limit |
| --- | --- | --- |
| Same-minute finances (`FoodFinances.StepBudget`) | ≤32 exact postings; heap O(log consumers) repair; saved timestamps rederive queue; all postings before meals | Tables above at 1,914/10,000/100,000; no workload-sized drain |
| Campaign boundary (`NativeCampaignAwareness.SampleCampaignBoundary`) | ≤64 retained shop scopes, ≤8 native entries/business; one fields-only reader unit, no retained native references; oversized whole-hour refusal before scans | Boundary counts/timer exposed; reader/native-double tails in raw campaign JSONL; no claim that mutable native reads resume safely |
| Campaign publication (`FoodCampaignSource.Publish`) | At most 64 incoming businesses, up to the existing 1,024 saved images ×64 masks, fixed 64 buckets; stays atomic with boundary | Bounded native exception includes managed publication; retained-history fixture measures it; no per-consumer campaign sweep |
| Citizen route (`MultimodalRoutes.PlanningJob.Continue`) | ≤8 stages/finite call; ≤1 path/call and ≤2 finite job paths/planner/frame, across repeated calls (including throwing queries); admission exhausted → unchanged stage, retry next frame | Final native doubles: 750 calls, median/p95/max **0.0001/0.0159/0.3137 ms**, max 6 stages/1 path, median/max **0/39,760 B**, zero ≥30 ms; [final rows](assets/f3-bounded-bulk/routes-final.jsonl) |
| Road A* (`RoadPlan.Continue`) | One 64-expansion slice/finite call, ≤2 curb-pair searches/request; native validation/build of selected route remains indivisible | 300-node generous-deadline test proves yielding; route fingerprints and snapshot-restart tests remain unchanged; actual native validation tails pending |
| Walking path/quote | `NavMesh.CalculatePath` synchronous; endpoints sampled twice; returned route ≤512 legs; cached-quote geometry still validated live | Existing query count/total/max timers; admission limits multiplicity, cannot guarantee any single native call <2/5/30 ms; rig must collect p95/tails |
| Curb search / access | Up to 12 curb sample attempts/search, ≤2 results; access/physics validation per bounded route segment; road spatial-cell candidate scan/sort depends on loaded geometry density | Existing native geometry work, not a new consumer-sized loop; native tail attribution pending. The frame query admission above counts **job path calculations**, not all food/sample/physics queries |
| Subway/preflight | ≤2 nearby stations per endpoint, ≤4 candidates; capture/discovery and road import remain separate readiness work | Whole-scene/native station captures cannot be preempted; existing one-search/frame admission and timing retained. Nearby sorting/cell density and native station count are not certified by the population-only benchmark |
| Food quote/commit / native checkout | Existing one held decision, eight shortlisted shops, finite quote/selection/commit phases; native checkout is one non-preemptible transaction | Existing bounds unchanged; full replay includes managed quotes/commit but no native checkout. F1 owns frame advancement/epoch policy |
| Physical population, closure refresh, optional-stop refresh, save serialization | Four parked hotspots, untouched | Excluded from this component proof; complete native mod-work union and whole-frame timing remain separate next-batch obligations |

The on-demand `ReadCampaignSnapshot` and explicit offline `AdvanceTo`/`Plan` drain paths are not called by native frame advancement. R1 captures all supported actual candidates up to 1,024 coherently; it does not refuse supported observations. Only the coherent native capture remains non-preemptible; detached publication resumes across bounded calls, and saves complete it before state capture.

Initial pre-review hourly adapter (64 scopes, 301 successive warm boundaries, constructor excluded; superseded by R1): median/p95/max **0.3800/0.4314/1.8631 ms**, median/max **138,392/155,752 B**, zero ≥30 ms. Its actual production adapter test also verifies 1/64 complete observations, 65/1,024 whole-hour refusal before reader binding, duplicate-callback idempotence, unchanged retained history and unchanged native campaign/expense/customer fields. The maximum saved-history transition (1,024 full eight-day images → 64 admitted scopes, 31 samples after warmup) measures median/p95/max **0.4377/1.5668/7.8461 ms**, median/max **338,320 B**, zero ≥30 ms. These are native doubles; the 7.8461 ms tail exceeds the soft advancement budget and is explicitly retained. Native calls still need their own observed-tail admission proof in the batched rig.

## Advance planning and versioned route inputs

The existing planner already captures road/access/station identities and restarts the whole uncommitted event when those inputs are replaced. F3 keeps that deterministic barrier. Earlier event answers may allocate a car or probe employment; later citizen/traffic/meal events can alter assignments, closure/traffic forecasts, parked position, access, money and opportunity deadlines. Letting those later events run while a request is outstanding would change what it reads and commits according to frame cadence. Geometry identity alone is insufficient: walking/NavMesh and public-access validation are native live reads, and traffic forecasts are not an immutable versioned event image.

A safe future implementation would capture a bounded request image keyed by logical event sequence and geometry/access/station/forecast revisions, compute it before its deadline without publishing side effects, and consume it only at the original deterministic commit slot after validating all replay-critical inputs. A stale image must be discarded/recomputed in bounded slices, never converted into a missed activity merely because it took longer to compute. Saving must retain the unstarted authoritative event or its existing committed terms, not a new persisted speculative cursor. This investigation does **not** start F5/F6/F7 or change the save model.

F3 implements no speculative look-ahead: there is no verified immutable native route-input capture contract that meets those requirements here. Under 120 consecutive replacements attempts remain finite and the original save bytes remain identical. Once inputs stabilize, sliced execution completes identically to uninterrupted execution. Round 2 defers mod-owned refresh publication for one event so managed refresh churn cannot hold it indefinitely. Actual native invalidation remains fail closed; F1 owns reporting/backpressure/overload.

## Taken low-risk performance wins

Replace per-posting sorted-set node replacement with a reusable heap and constructor-cached ordinal ranks/due times: **48 B/posting → no recurring node allocation**, and the matched complete-day work numbers above improve **3.2406→0.9100 / 15.7399→5.1148 / 87.1907→22.4029 ms**. The first heap prototype compared identity strings on every repair and worsened aggregate posting cost; it was refined before final measurement. No claim of a uniform integrated speed or allocation improvement. The one unattributed 8,136-byte call is preserved. No settings, scan frequency, parked subsystem or native economic policy was changed for these savings.

Pre-size the campaign capture list and place map to the already admitted scope: **141,472→138,392 median B/boundary**, saving **3,080 B** at 64 scopes. Same 301-boundary fixture before/after; p95 **0.4956→0.4314 ms**, maximum **1.8171→1.8631 ms**, so no uniform tail-speed claim. [Before capture](assets/f3-bounded-bulk/before-capacity-campaign.jsonl) belongs to the already bounded `0f632b3` source; [after capture](assets/f3-bounded-bulk/campaign.jsonl) includes the final pre-sizing. This is historical pre-review evidence at the named source commits. The R1 fixture uses a new publication API and is not overlaid onto that older implementation as an equivalent workload. This removes buffer growth, without new per-frame work or a changed observation limit. Exhausted route-query admission also signals the frame loop to stop same-frame zero-work retries; native performance of that coordinator path remains a batched-rig measurement.

## Review round 1 ? preserved coverage and time-aware admission

The initial 64-retained-scope cutoff was a correctness regression. It is removed. A managed lifecycle candidate set plus maintained business/place and registration indexes determines actual read sources; 1,024 retained scopes with only one candidate cause one native read. The full 1,024-source supported envelope is coherently detached at the native boundary, then managed publication processes at most 16 business/image units per call. No mutable native references survive, and no supported hour is omitted. Core event/route commitment waits for publication; save completes pending work without a new schema or cursor.

[Retained round-1 capture/publication rows](assets/f3-bounded-bulk/r1-campaign.jsonl) and [route/peak rows](assets/f3-bounded-bulk/r1-route-admission.jsonl). D3a round-1 native-double observations (cold managed publication after capture; no native FPS claim):

| Actual sources | Atomic capture median / p95 / max ms | Capture median B | Managed publication calls / total units | Publication median / p95 / max ms | Publication median / max B |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 0.0033 / 0.0062 / 0.0155 | 1,840 | 1 / 5 | 0.0348 / 0.0348 / 0.0348 | 2,312 / 2,312 |
| 64 | 0.2173 / 0.3098 / 4.2343 | 100,808 | 17 / 257 | 0.0057 / 0.0372 / 0.0372 | 1,616 / 11,768 |
| 65 | 0.1872 / 0.3151 / 0.6221 | 102,328 | 17 / 261 | 0.0117 / 0.0376 / 0.0376 | 3,296 / 12,432 |
| 1,024 | 0.9236 / **6.1153** / **19.7485** | 1,629,712 | 257 / 4,097 | 0.0123 / 0.0250 / 0.1615 | 0 / 114,896 |

Zero ?30 ms samples in these capture/publication observations. **The 1,024-source non-preemptible capture p95 exceeds 5 ms**, so this does not certify the combined mod-work gate. Native tails remain a rig obligation; the supported envelope is preserved instead of silently dropping work to improve latency. Tests mutate native fields after capture and compare every final image to synchronous publication; queued hours, partial publication/save completion and reconstruction yield identical masks, reference identities and awareness queries. Five new unit cases cover these invariants. The initial Core replay's 49 ?30 ms stress intervals remain flagged, not erased by this narrower component experiment.

The fixed two-path/frame policy was also removed. A finite continuation may run up to eight stages. A potential next path stage checks remaining time against a recent upper-envelope reserve: start 0.05 ms, raise to 1.25? observed query duration, decay 5% per later query. A 16-query safety ceiling prevents multiplicity runaway. One query/frame is still allowed when an unavoidable tail exceeds the frame budget; throwing calls consume admission. No claim is made that this reserve bounds an individual native call.

The production multimodal/road job harness runs a burst of **957 complete plans / 1,914 consumers**, with eight measured path queries/plan, a controlled **0.1 ms/query native double**, and **2 ms frame budget**. Frame time is virtual at 30/60 FPS; this tests coordinator capacity, not actual game frames. Queue every plan at once (stricter than the declared 30-game-minute arrival window at one-third game minute/second, 90 real seconds):

| Virtual FPS | Frames to drain | Virtual seconds | Completed plans/s | Max queries/frame | Final backlog |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 30 | 481 | 16.0333 | 59.6881 | 16 | 0 |
| 60 | 479 | 7.9833 | 119.8747 | 16 | 0 |

Both drain before 90 seconds; every route fingerprint matches the same-input synchronous result. The old two-query cap's theoretical ceiling for eight-query plans was **7.5 / 15 plans/s** at 30/60 FPS (127.6 / 63.8 seconds for 957 plans), before non-query overhead. The new measured workload clears the specified 31.9 plans/game-minute peak; it does not predict native costs, actual rush distributions or higher calendar speeds. Rig check 1 explicitly asserts backlog recovery at both frame cadences and records actual query tails. The finite-call harness separately measured 90 calls, up to 8 stages / 5 queries per call, median/p95/max **0.0435/0.1816/0.1854 ms**, zero ?30 ms; median/max allocation **19,352/22,760 B**. These stages already allocate route/search data; no per-frame polling, reflection, scan or diagnostic-string allocation was added by the admission policy.

Round 2 supersedes the managed-input liveness gap: a transient event lease coalesces mod-owned refreshes until commit/restart/abort. Native sweeps and city/scene/staleness checks remain active. Sustained managed-refresh tests require finite work and completion without stopping refresh requests. No per-frame scan or allocation is added by the lease, and no new timing win is claimed. Actual native topology churn still fails closed rather than pinning unsafe geometry.
