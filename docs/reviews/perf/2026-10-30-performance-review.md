# Windows performance review — 30 October 2026

Role: `rig_test`. **PASS for the measured normal and rush-hour mod-work gate.** The implementation-plan criterion is combined Update/food-authority union **p95 ≤5 ms**, without repeated mod-attributable >50 ms stalls. Normal, morning and evening each pass in all three primary windows; none has mod work >50 ms. **This is not a 5 ms maximum guarantee**: maxima exceed 5 ms. Whole-frame stalls, unavailable allocation counters and limited City Performance activation remain explicit below. This review gives no release or ordinary-save deployment clearance.

Tested SHA: `7a6153cfa9bb88e28dea905b285b4273c3e268c7`; production DLL SHA-256: `f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c`. Requested fetch advanced the branch by a documentation-only commit. Verified first-session windows at `21db30b753b8fdbe8e4a4a836f5f2623e0935377` are reused: production source and rebuilt production/helper DLL hashes are identical. Build passed; offline sanity **1,551 passed / 0 failed / 0 skipped**. No production code, tests or mod behavior was changed. [Provenance](2026-10-30/session-provenance.json).

**Primary dependency-only results.** Three warm ≥60-second windows per condition, Monday day 8; targets 07:00, 12:00 and 15:30 plus 15-second normal-speed warmup. Frames are whole/mod counts; quantiles are p50 / p95 / p99 / max in ms, pooled from individual frames.

| Condition | Seconds | Frames whole/mod | Whole-frame ms | Combined mod-work ms | Worst window mod p95 |
|---|---:|---:|---:|---:|---:|
| Morning commute | 180.63 | 16219/16216 | 11.121 / 14.005 / 16.566 / 25.766 | 0.671 / 1.259 / 3.353 / 11.528 | 1.424 |
| Normal noon | 180.84 | 16262/16259 | 11.118 / 13.185 / 15.480 / 36.636 | 0.254 / 0.411 / 0.820 / 6.809 | 0.461 |
| Evening commute | 180.74 | 16032/16029 | 11.151 / 13.807 / 17.476 / 174.875 | 0.329 / 0.815 / 2.946 / 23.980 | 2.420 |

Actual sampled in-game spans: morning 07:05–08:06, normal 11:57–12:58, evening 15:29–16:30. Gaps between windows contain control/export work.
Endpoint pedestrian/car samples: morning 23–39 / 0–3, normal 0–2 / 0–0, evening 5–40 / 0–2. These are sparse observations, not maxima.

| Condition | Whole >33 / >100 ms | Mod >5 / >33 / >100 ms | GC generation counts 0/1/2 | Alloc bytes/frame p50/p95; total |
|---|---:|---:|---:|---|
| Morning commute | 0 / 0 | 60 / 0 / 0 | 5/5/5 | Unavailable / unavailable; unavailable |
| Normal noon | 1 / 0 | 4 / 0 / 0 | 4/4/4 | Unavailable / unavailable; unavailable |
| Evening commute | 16 / 4 | 45 / 0 / 0 | 6/6/6 | Unavailable / unavailable; unavailable |

GC counts are summed raw in-window observations, excluding stop/export overhead; Mono generations can alias, so do not add columns. Per-window GC counts and signed live-heap distributions are retained in [windows.json](2026-10-30/windows.json). Neither the current-thread allocation counter nor Unity allocation recorder supplied valid bytes allocated. Heap deltas are **not allocation totals**, and subsystem allocation attribution is unavailable. GC coincidence does not identify a pause cause.

**Subsystems.** Inclusive p95 / max ms per frame; nested percentiles must not be added. Primary hooks cover the first five rows. Separate detail captures cover traffic-core, food and optional scheduling; these carry extra instrumentation and a different minute/body mix.

| Bucket | Morning | Normal noon | Evening |
|---|---:|---:|---:|
| PopulationRuntime.AdvancePopulation | 0.148 / 2.904 | 0.041 / 2.878 | 0.100 / 10.744 |
| PhysicalPopulation.Update | 0.642 / 11.012 | 0.127 / 6.589 | 0.302 / 21.652 |
| PhysicalVehicles.Update | 0.032 / 0.121 | 0.004 / 0.029 | 0.031 / 4.936 |
| CitySimulator.RefreshTrafficClosures | 0.176 / 0.309 | 0.030 / 0.086 | 0.115 / 1.921 |
| PhysicalTrafficHooks.FixedStepPostfix | 0.054 / 2.202 | 0.004 / 0.065 | 0.045 / 3.418 |
| traffic-core (detail) | 0.012 / 0.042 | 0.003 / 0.028 | 0.012 / 0.053 |
| food-commerce (detail) | 0.000 / 0.914 | 0.000 / 0.772 | 0.000 / 0.373 |
| food-authority (detail) | 0.002 / 0.404 | 0.002 / 0.395 | 0.002 / 0.018 |
| optional-refresh (detail) | 0.090 / 3.124 | 0.079 / 3.100 | 0.093 / 3.139 |

Two **separate** manual-save steps measured mod save/serialization **191.751/189.735 ms** and **335.505/332.913 ms** (one call each; no save-call p95 inference). Serialization is nested in save. These calls are not charged to gameplay windows or used as full native disk-save timings. Food-commerce per-frame p95 is zero because calls are sparse, not because food work is free. [Bucket evidence](2026-10-30/bucket-summary.json).

**City Performance load-order cases.** Version 0.5.1, retained repo-build source `46585ff`, Workshop item `3825351532`; archive and DLL hashes are in [lifecycle evidence](2026-10-30/lifecycle-evidence.json). Both A-before and Z-after directory placements completed three windows per condition.

| Directory placement / condition | Whole p95 / max ms | Mod p95 / max ms | Δ whole p95 / mod p95 vs alone, ms |
|---|---:|---:|---:|
| before / Morning commute | 14.302 / 50.350 | 1.346 / 20.442 | +0.297 / +0.087 |
| before / Normal noon | 13.696 / 33.995 | 0.563 / 18.474 | +0.511 / +0.152 |
| before / Evening commute | 14.361 / 64.336 | 0.680 / 25.077 | +0.553 / -0.135 |
| after / Morning commute | 14.181 / 34.849 | 1.266 / 10.443 | +0.176 / +0.007 |
| after / Normal noon | 13.547 / 39.034 | 0.545 / 9.236 | +0.361 / +0.134 |
| after / Evening commute | 14.226 / 49.031 | 0.913 / 37.630 | +0.419 / +0.097 |

**Effective reverse patch order was not feasible:** City Performance initialization-load hooks precede Persistent City city-load hooks in both placements. More importantly, City Performance marks this native assembly **unverified**: behavioral optimizations stay off; timing/trace hooks and the presentation-only button-caption hook run. Four named inactive capabilities are pedestrian refill, midnight cleanup deferral, tree detail and cinematic coverage; autosave integration is also inactive. Thus these are observations of the fail-closed package with timing overhead, not evidence for active optimizations or two genuinely reversed patch orders. Shared methods are `PedestrianSpawner.SetActivePedestrians`, `TrafficManager.FixedUpdate` and `GameManager.RunMainGameTick`; both owners remain attached, with no new Persistent City exception or explicit patch-conflict error observed. This does not prove semantic compatibility of disabled patches. [Patch inventory](2026-10-30/patch-inventory.json), [chronology](2026-10-30/load-order.json), [native QA](2026-10-30/native-qa.json).

**Historical comparison and PR effects.** The supplied “28 Oct whole-frame 31.63/517.95” reference is actually **combined mod p95/max from the 24 October Mac final candidate**, described in [bounded implementation](../2026-10-24-bounded-implementation.md). The tracked summary gives whole-frame p95 359.1553 ms; its whole-frame maximum was not independently verified in this relaunch. The 28 October rounds are source/offline evidence.

| Capture | Whole p95 / max ms | Mod p95 / max ms | Comparison limit |
|---|---:|---:|---|
| 24 Oct Mac quoted reference | 359.155 / unverified | 31.629 / 517.950 | Different machine, build, workload |
| 27 Oct PC candidate Rush A | 14.032 / 81.220 | 1.904 / 45.663 | Older build; City Performance present |
| Current PC morning | 14.005 / 25.766 | 1.259 / 11.528 | Unmatched historical comparison |
| Current PC evening | 13.807 / 174.875 | 0.815 / 23.980 | Unmatched historical comparison |

The current samples are below the historical mod-work values; this establishes no causal PR speedup. Today has no PR #1/#2/#3 counterfactual. Earlier [optional-stop fresh-process evidence](../rig/2026-10-29-pr3-optionB-r2/README.md) records +0.0704 ms p50 / +0.0722 ms p95 mod work at the default setting; its primary run instead showed +0.1484/+0.2722 ms. Those descriptive results and their noise limits remain distinct from today. The [27 October PC campaign](../2026-10-27-performance-pc.md) already met 5 ms p95 before these PRs.

**Hotspots and proposed fix order.** Gain estimates below are hypotheses, not measured changes. Halving an inclusive bucket gives the shown optimistic bucket reduction; actual whole-frame p95 improvement may be zero, and rows cannot be added. No fix was made.

| Priority / hotspot | Evidence, p95 / max ms | Estimated gain at p95 / max | Effort / risk |
|---|---:|---|---|
| 1. Physical population selection/body updates | 0.642 / 21.652 | 50% bucket scenario: ≤0.321 / ≤10.826 ms | M / High: body/identity continuity |
| 2. Closure refresh cadence | 0.176 / 1.921 | 50% bucket scenario: ≤0.088 / ≤0.960 ms | M / High: live road closure correctness |
| 3. Optional-stop refresh | 0.093 / 3.139 | 50% bucket scenario: ≤0.046 / ≤1.570 ms | M / High: stop validity and saved decisions |
| 4. Save serialization | One-call max 332.913 | Gameplay p95: 0; save max: ~83.228 ms if reduced 25% | L / High: immutable snapshot, schema and interrupted saves |

First narrow physical selection/body work because it dominates measured recurring cost. Then examine closure refresh while preserving live closure handling. Next profile optional refresh before optimizing its sparse ~3 ms tail. Treat save serialization separately: it has the largest observed mod call, but autosave-off gameplay gains nothing from a save-only change. Obtain a verified allocation-capable native profile before proposing GC-specific fixes.

**Method, noise and limits.** Same retained disposable FT morning-base fixture, production defaults (25% optional stops, 48 pedestrian/24 car caps, 120 m radius, one-third calendar pace), 957 residents, 1× in windows. RTX 4070 / i5-13600KF, Direct3D11, 1920×1080 Low, vSync off, target 90. Stationary camera; real body counts vary and caps are not saturated. The helper measures wall intervals and the historical Update/authority union; fixed-step/intersection/force hooks outside that union remain separate inclusive buckets. Results are not presented-frame FPS proof. Windows with extra hooks are excluded from primary pooling. Existing floor(n×q) quantiles and first partial whole-frame interval are retained for historical comparability; partial mod frames are excluded. Detailed event instrumentation has unquantified overhead; it was identical across placements. No capture-off overhead experiment was done.

All 40 retained windows have clean validity records; 27 are primary Monday windows, nine are supplemental Friday/weekend windows and four are detail captures. Friday belongs to the mod's three-day weekend and is not commute evidence. All three morning primary windows and earlier supplemental/detail/save captures were recovered from the read-only first stage; the unfinished first-session noon capture is excluded. Sequential runs, uncontrolled outside activity, changing unsaved geometry/body history and the retained versus fresh morning set limit causal comparisons. The whole-frame 10% matched-reference release gate is not established. [Method](2026-10-30/method.json), [all quantiles and environment](2026-10-30/windows.json), [raw-file hashes](2026-10-30/raw-provenance.json).

**Spikes and native QA.** Dependency-only evening contains four >100 ms wall intervals (104.010–174.875 ms), with no coincident GC increments and neighboring mod union <0.8 ms; cause remains unresolved. Other spikes and GC coincidences across all cases are retained with neighboring frame/event/component context; coincidence is not causal attribution. No measured mod >50 ms occurs in the primary campaign. No new Persistent City exception was found. Native building-hover null-reference errors and a diagnostic shutdown null-reference in `PlayerHelper.SaveCurrentPosition` are retained; the latter occurs when restoring autosave flushes a pending native midnight save, outside timing. [Spike evidence](2026-10-30/spikes-and-gc.json), [QA stacks](2026-10-30/native-qa.json).

**Autosave and restoration.** Original autosave was enabled (`preventAutoSave=false`, five-minute interval, three slots). It was disabled again after loading and verified off before/after every window; no save was in progress, save inventories were unchanged and no actual save-start/completion markers appeared. Manual saves were outside timing. [Per-window proofs](2026-10-30/autosave-validity.json).

Both sessions restored and hash-verified **4828 profile files and 107 installation references**, original ModsLocal/config, root files and exact preference export. Original autosave is restored by those profile/registry hashes; the restored game was not relaunched. Added A/Z City Performance placements and diagnostic helpers were removed; the rig's preexisting CityPerformance folder was preserved and restored. Only the verified agent1 process was stopped. Lease released, held=false, pending=0; no takeover. [Backup, restore and release receipts](2026-10-30/lifecycle-evidence.json), [original settings](2026-10-30/original-settings.json), [portable export checks](2026-10-30/delivery-checks.json). Full private backups, native logs, DLLs and raw saves remain outside Git. No normal profile, Steam, Workshop or RLO source was changed.
