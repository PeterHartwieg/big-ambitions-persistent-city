# Rig test plan review, 19 November 2026

An independent second opinion on the GR-5 rig checks and gates, asked for by Peter on 19 November 2026 (t187u). It covers which checks a stable Workshop build at the 1,000-resident cap needs, a smaller rig for ordinary fixes, a release rig, what can move offline, and where the speedup plan on the table should change.

This is analysis only. No rig, game, test or build ran. No gate is changed here; every proposed change to how a gate is measured is listed for Peter in [section 8](#8-decisions-for-peter).

Sources: [AGENTS.md](../AGENTS.md), the [rig checklist](../docs/rig-checklist.md) sections for rigs 13d and 16m to 16q, the [roadmap](../docs/roadmap.md), the [decision register](../docs/decisions/2026-11-10-city-direction.md), the [F4 plan](../docs/plans/f4-perf-gate.md) and its [frozen contract](../tools/perf-gate/contract-v1.json), PRs [#311](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/311) and [#312](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/312), the 19c PC rig prompt and the speedup write-up of 19 November (both outside git). Arithmetic that is mine and not a rig figure is marked "my arithmetic".

Check letters follow the 19c prompt: A day 15:30 (steady, and a second window at 2× after a retime), B night 01:00, C street, D mod-versus-no-mod heap, E RSS, F retained census around a retime, G hold replay across a retime, H busy shop (PC), N no-mod GC baseline (PC).

## Summary

1. **The length of one rig run is the smaller problem.** Three of the five gates are measured in a way that cannot tell a better build from a worse one on the shared Mac, so fix rounds are being started from noise. Rig 16m is the only recent run on a quiet Mac (load average under 3). It passed the pause gate on two of three windows and the street gate with the highest steady mod allocation of the series. Every later run had a load average of about 6 or more, failed pause on both day windows, and the steady pause p95 went from 31 ms to 1,717 ms while steady mod allocation fell from 120 MB to 61 MB ([section 1.1](#11-host-load-decides-the-pause-and-street-verdicts)).
2. **What the mod costs in GC is already small.** At 16q the mod adds about half a collection per ten minutes at 1× and about 1.4 at 2× (my arithmetic from #311's figures). The game alone collects 5 to 7 times in the same window. A count of "about 5" is at or below the game's own rate on the Mac. The 19c PC run, which targeted 60 fps on a host the write-up describes as contaminated, read 14 in both day windows.
3. **What a player would feel is somewhere else:** mod-work frames of 30 to 60 ms when pedestrian bodies spawn or a planning slice runs long, the save pause every five minutes with autosave on, and memory growth over a session of hours. The first is measured every run and should stay. The second was last measured on 17 November on the Mac only. The third is not measured by any current check.
4. **Per-fix rig:** two windows (street, and night followed by a census screen), no live no-mod arm. About 45 to 50 minutes on the Mac against 1 h 42 min today.
5. **Release rig:** all current candidate windows with the census in its official place, fresh no-mod samples that also confirm the cached baseline, the save check, the busy shop, and a soak that does not exist today. About 2 h 30 min plus the soak, on the PC with nothing else running.
6. **A window on a host with independently recorded contamination should end as "invalid", not "fail".** It is re-run and starts no fix round. A slow window on a clean host is still judged.

## 1. What the evidence says about the gates

### 1.1 Host load decides the pause and street verdicts

All figures are from the 16m to 16q checklist sections. Load is the 1-minute load average at the start of the A steady, A repeat and B night windows.

| Rig | Load at window starts | Steady mod MB | Pause p95 ms, steady / repeat / night | Street union p95 ms | Spikes ≥30 ms |
| --- | --- | --- | --- | --- | --- |
| 16m | 2.0 / 1.7 / 1.7 | 120.0 | 31.3 / 41.2 / 29.7 | 3.97 | 0 |
| 16n | 8.9 / 8.7 / 5.8 | 99.7 | 403.2 / 55.3 / 41.1 | 5.83 | 4 |
| 16o | 7.4 / 14.1 / 8.3 | 91.7 | 74.7 / 114.3 / 88.3 | 4.16 | 0 |
| 16p | 16.2 / 6.9 / 8.4 | 87.7 | 283.0 / 96.5 / 97.8 | 5.47 | 4 |
| 16q | 10.3 / 18.7 / 7.1 | 60.7 | 1,717.5 / 103.3 / 281.8 | 5.05 | 4 |

- The pause p95 is the maximum. Each window has 5 to 7 collections, so nearest-rank p95 picks the largest one (16q lists p95 and maximum as the same number in all three windows). One host stall on one collection frame sets the verdict.
- The mod is not what makes those frames long. Pause with and without UpdateCity differs by about 3 ms or less in every 16l to 16q window except 16q steady.
- The collector's own time is steadier. Unity `GC.Collect` p95 was 24 to 37 ms in most candidate windows and 22 to 29 ms in the no-mod arm (16j, 16l, 16m, 16o; 16q read 63 against 46 under heavy host load).
- 16q ran with DaVinci Resolve, a VM, ffmpeg and many node processes; the load average reached 60 during D. The game made 21 to 25 frames per second against its 30 Hz cap.
- On the PC, 19c read a pause p95 of 563 ms in A steady and 24.5 ms in A retime. These were two separate processes in one run that targeted 60 fps, with another game running on the host. The write-up gives no achieved frame rate for either window, so I treat both as provisional (speedup write-up, section 2).

### 1.2 The gen0 count follows frames rendered, not the mod

- #311 worked this out from 16p: the game alone gives 6.7, 6.3 and 5.0 collections in the steady, repeat and night windows; the mod adds 0.74, 1.42 and 0.28. Its words: "A target of about 5 sits at or below the game's own rate on this fixture."
- The no-mod arm of check D collected 7, 9, 9, 7 and 6 times in 16j, 16l, 16m, 16o and 16q. The candidate in the same check collected 7, 6, 7, 6 and 5.
- 16q A steady passed with 5 because the loaded host rendered fewer frames: 12,532 frames and 726 MB of frame allocation, against 858 MB and 7 collections in 16o.
- 19c on the PC, targeting 60 fps, read 14 in both A windows. That reading is provisional: the host was contaminated, the achieved frame rate is not given, and the city had 957 residents and 1,914 visitors. If game allocation is per frame, as the 16q frame figures suggest, a clean host at 60 fps would collect more often than the Mac at 30 Hz and the absolute count could not pass there. That is a hypothesis until a clean 60 fps run records the count with its frame rate.
- The baseline is from 13d: 5 collections at 3,222, 10,000 and 20,000 persons alike, in a night scene at 30 Hz. The count did not move with population even there.
- The mod can only change this count through what it allocates. Peter made mod allocation report-only on 18 November. A gate on the absolute count is that same quantity plus the game's allocation and the frame rate.

### 1.3 The night heap margin is smaller than one heap step

- Final reads: 16o 1,478,131,712 B (fail by 8.1 MB), 16p 1,467,858,944 B (pass by 2.1 MB), 16q 1,462,370,304 B (pass by 7.6 MB). The Mono heap grows in steps of 16,777,216 B.
- #311 attributes the step after the sampler to the rig's own modwork export. In 16q the heap rose 32 MB at that export.
- The heap size depends on the path taken to the window. In 16q the same build in the same 01:00 scene read 1,429,864,448 B in B and 1,344,520,192 B in D's candidate arm, 85 MB apart. Read (a) just before B's sampler was 1,505,607,680 B, above the limit, and is not a gated read.
- The 1.47 GB figure is 13d's heap at 3,222 residents. The fixture has 1,000.

### 1.4 Heap size cannot carry a 5% comparison; live memory can, and the gap is 11 to 13%

- The no-mod arm alone read 1,337,065,472 B in 16o and 1,287,340,032 B in 16q at the same point, 3.9% apart. It then shrank by 107 MB and 66 MB inside its window. Most of the +12.76% and +10.09% stop offsets is that shrink.
- Live memory after a forced collection is stable: no-mod 1.080 and 1.088 GB (16o), 1.062 and 1.072 GB (16q); candidate 1.220 and 1.222 GB (16o), 1.223 and 1.229 GB (16p), 1.186 and 1.190 GB (16q).
- The gap is 118 to 140 MB and did not grow inside the window: 140.1 to 134.7 MB in 16o, 123.8 to 118.4 MB in 16q (my arithmetic from the forced-GC rows).
- The census finds 67 MB of mod-owned data: 49.8 MB simulation, 12.1 MB the save string. The rest of the gap is game objects the mod causes (#311).
- 5% of the no-mod live set is about 54 MB (my arithmetic). Reaching it means removing more than half the footprint. That is a footprint target. The stability question is whether the gap grows, and it does not.

### 1.5 The RSS allowance is far below what the no-mod process does by itself

- No-mod RSS change 120 s after the retime: −26,224 KiB (16o), +306,976 KiB (16p), +106,272 KiB (16q). Immediately after the retime it was +900,912 and +967,024 KiB.
- The allowance is 16 to 19 MB. The reference moves by hundreds of MB between runs.
- The part that is the mod's own native memory, owned against live meshes, matched at +120 s in 16o, 16p and 16q.

### 1.6 The street gate measures the right thing on the wrong host

- Mod-work p95 and spikes are what the player feels. This gate should stay in every run.
- On the Mac, whole-frame p95 was 169 to 350 ms in the street window. Union p95 moved between 3.97 and 5.83 ms across builds; #311 states it changed no per-frame path, and the reading moved from 5.47 to 5.05 ms.
- #311 gives a useful sign of a host stall: the fixed-cost hooks (`AccountingPrefix`, `PopulationRuntime.Update`) run 10 to 60 times their usual cost in the same frame. It is a sign and not proof, because heavy mod work or a collection in the same frame can inflate a hook too.
- By that sign, several spikes look like real work: `physical_spawn_pedestrian` with `physical_spawn_grow` at 58.4, 57.4 and 39.2 ms, `advance_planning_slice` at 41.1 ms and `advance_events_citizen` at 38.7 ms (16q A and B rows). These are the open items a player would notice. The 1,102 ms `physical_spawn_grow` frame in 16q A steady is unresolved; its hook cost was about 8 times normal.
- The rule differs between documents. AGENTS.md and contract row M2 say a spike of 30 ms or more is flagged. #311 check 3 and the 19c prompt say it fails the gate.

## 2. Is each check needed

"Player notices" means a failure the player could see or feel at the 1,000 cap.

| Check | Player notices a failure? | Needed for a stable mod? | Where it should run |
| --- | --- | --- | --- |
| C street: mod-work p95 under 5 ms, spikes of 30 ms or more | Yes. Stutter and hitches on the street. | Yes | Every run |
| F census around a retime | Yes, over time. Speed changes are frequent; anything retained per retime adds up. | Yes. It found a real defect in 16p (a retired `CitySimulator` kept alive) and costs about 3 minutes. | Every run: the official form after D in the release rig, a screen after B per fix (decision 11) |
| B night: heap | Only if it keeps growing. A single reading 8 MB over or under 1.47 GB is invisible. | Yes as a growth check, not as an 8 MB margin | Every run |
| A steady: pause, gen0, allocation segments | Pause yes, when the collector itself is slow. The sampler number is mostly host stalls. | The allocation segments are useful for diagnosis. The pause and gen0 verdicts are not reliable ([1.1](#11-host-load-decides-the-pause-and-street-verdicts), [1.2](#12-the-gen0-count-follows-frames-rendered-not-the-mod)). | Release rig; per fix only for an allocation fix |
| A retime at 2×, fresh process | Barely. About 1.4 extra collections in ten minutes at 2×. | Low value as its own window. The retime path is covered by C (1→2→1 inside the window) and F. | Release rig |
| D mod-versus-no-mod heap, both arms | No. 120 MB in a process of 2 to 3 GB. | Not for stability. Useful as a footprint record. | Release rig; no-mod arm from the cached baseline |
| E RSS, candidate reads | Only as growth over hours | The owned-against-live mesh match is useful and free (it rides in C). | Every run, report-only |
| E RSS, no-mod arm | No | No. The comparison cannot resolve 16 MB ([1.5](#15-the-rss-allowance-is-far-below-what-the-no-mod-process-does-by-itself)). | Baseline session, and one fresh sample in the release rig |
| G hold replay across a retime | Not directly; it is a diagnostic command path | One-off proof of #311 item 3; passed in 16q. Needs a separate package and process. | Release rig, and when retime handoff or hold-replay code changes |
| H busy shop (PC) | Yes. The player stands at their shops more than on a street. | Yes, as a second scene for the mod-work gate | Release rig; per fix only for food or shop changes |
| N no-mod GC baseline (PC) | No | Once per machine, city and game version | Baseline session only |
| Save under 40 ms | Yes. Autosave is every five minutes by default (16q preference read). A hitch here is the most regular stutter in the game. | Yes. Last read 29.4 ms p95 on the Mac in 16h, "Mac-indicative" only; the PC verdict has never run. | Release rig; per fix when save, codec or state size changes |
| Soak over game days (contract row R1) | Yes. Memory growth, slowdown after hours. | Yes. Nothing today runs longer than 600 s. R1 alone reads managed memory only, so native readings have to be added ([3.2](#32-release-rig)). | Release rig |

The five gates, by the same test:

| Gate | Keep? | Comment |
| --- | --- | --- |
| Street mod-work p95 under 5 ms, spikes of 30 ms or more flagged | Keep, every run | The one gate that maps straight to what the player feels. |
| GC pause p95 about 36 ms | Keep the number; change the statistic (decision 3) | A p95 of 5 to 7 samples is a maximum. Judge Unity `GC.Collect` time per condition, never pooled across conditions, and keep the whole-frame view of the same frames. |
| gen0 about 5 per 600 s | Replace the absolute count (decision 2) | On the Mac it passes more easily on a slower host, and it probably cannot pass at 60 fps (a hypothesis, [1.2](#12-the-gen0-count-follows-frames-rendered-not-the-mod)). Gate the collections the mod adds. |
| Night heap 1.47 GB or less | Keep at the sampler read; stop gating the read after the rig's exports (decision 4) | Add live-after-forced-GC as the stable companion. |
| Mod within 5% of no-mod heap | Decide what it is for (decision 5) | As measured it fails on heap-size noise. On live memory it is a footprint budget the mod misses by a factor of two. The leak question it stands for passes. |

## 3. Proposed rigs

### 3.1 Per-fix rig

Run after an ordinary GR-5 or performance fix. Two candidate windows in fresh processes. C and B run exactly as in #311, so their numbers stay comparable with 16j to 16q. F moves to a different lead-in and is a screen, not the official check (see below).

| Step | Content | Gates read | Minutes |
| --- | --- | --- | --- |
| Setup | Lease, build, stage, start, load. Refuse to start if the sealed fixture is missing or free disk is under the rig's limit. | | 4 |
| Host receipt | Before and during each window: CPU used by processes other than the game, memory and named top processes, from the boundary and per-minute reads the rig already takes, plus the rig receipts. If it fails before a window, wait and retry. If it fails during a window, re-run that window once. After that report INVALID-HOST. | | 1 to 2 |
| Window 1, street | Check C exactly as in #311, with E's candidate RSS and owned-memory reads (they already ride in C). From the rig's perf rows: collection count and `GC.Collect` durations. | Street p95; the full spike list; GC rows for this condition | 4 prep + 10 |
| Window 2, night | Check B exactly as in #311. Then the F sequence in the same process once the sampler has stopped: `pc gc retained` before the 1→2→1 retime, right after both `Clock retime:` lines, and 30 s after the new planner's "Road snapshot started" line. | Night heap; night pause and gen0; census screen: retained growth against #311's allowance, type counts, the donor row, total mod-owned bytes | 3 load + 9 prep + 10 + 3 |
| Optional window, day steady | Check A steady, only when the fix targets allocation. | Allocation segments (report-only) | +13 |
| Wrap | Restore, report tables from the controller, PR. | | 5 |

Total about 45 to 50 minutes on the Mac, about 60 with the optional window. 16p took 1 h 42 min clean and 16q took 2 h 21 min with a retry (speedup write-up).

Not in the per-fix rig: A retime at 2×, D, the no-mod arms of D and E, G, H, N and the save check. No-mod context comes from the cached bands ([3.3](#33-no-mod-baseline-session)); no per-fix gate rests on them. The census total (67.0 MB in 16q, 67.2 MB in 16p) is the per-fix tripwire for footprint: it is deterministic to a fraction of a megabyte and needs no no-mod arm.

**F after B is a screen.** #311 check 6 and the 19c prompt put F right after D's candidate arm in the same process. B leaves a different process behind: the sampler and the rig's exports have run, the probe and its forced collections have not. Retained bytes read after B may therefore not line up with the 16p and 16q reads. The criteria stay as #311 wrote them: one `CitySimulator`, `MultimodalRoutes` and `WalkingRoutes` at both after-reads, no donor objects at the timed read, and retained growth within 1 MB plus the allowance per cached traffic network. 16q failed that allowance by 0.41 MB with one `CitySimulator`, so the count alone is not the check. A failed screen is an open finding; the official F in the release rig decides the gate. Whether the screen may replace the official lead-in is decision 11.

**When a window is invalid.** Only on evidence from outside the measurement: the host receipt fails, or a rig receipt fails (autosave, a save inside the window, cap health, probe coverage). This follows the invalidation list in F4 Decision 3. An invalid window is kept in the report, excluded from the verdict in both directions and re-run. It starts no fix round and passes no gate.

A low achieved frame rate and inflated fixed-cost hooks do not invalidate a window. A slower build lowers the frame rate by itself, and heavy mod work or a collection can inflate a hook. Both are recorded as investigation notes on the window and on each spike row. F4 Decision 3 says the same: a complete window that ran slower is judged like any other. Every spike stays in the list and in the count until something independent attributes it to the host.

By the host receipt, 16q's windows were contaminated: load average 7 to 60, with Resolve, a VM and ffmpeg each near or above one core in the boundary reads. 16m's were not: load 1.4 to 2.5 and no other session. The threshold belongs between those two levels, expressed as CPU used by processes other than the game.

### 3.2 Release rig

Run before GR-5 is called done and again on the final development head (roadmap order item 6). On the PC at 60 fps windowed with no other heavy process, because the Mac cannot give a non-provisional number.

| Part | Content | Minutes |
| --- | --- | --- |
| Candidate windows | A steady, A retime, C street with E reads, B night, D candidate arm followed by F in the same process as #311 defines it, H busy shop | 60 |
| G | Hold replay across a retime, own package and process | 5 |
| Save | The #293 twenty-save check at 1,000 residents, its own run as the roadmap requires | 10 to 15 (estimate) |
| No-mod samples | One fresh E street replay and one fresh D night reference, each in its own protocol ([3.3](#33-no-mod-baseline-session)). They serve this run's comparisons and are checked against the cached band. | 20 |
| Loads, clock preparation, staging, wrap | About 8 loads, two night preparations, three stagings | 45 to 50 |
| Soak | Contract row R1 (three game days at speed 3, forced-GC heap read once per game day, growth of 5% per day or less, mod-work p95 drift of 20% or less) plus the native readings below. Unattended, its own session. | 75 to 105 (estimate, not measured) |

About 2 h 30 min for the windows plus the soak, and up to about 3 h 30 if all four optional matched references below are added. GR-5 closes when two consecutive valid release runs pass, so that one lucky or unlucky window does not decide it.

The full F4 matrix (about 7 hours, F4 plan Decision 3) stays the release gate the roadmap already names. This rig is the GR-5 part of it, not a second scorecard.

**Which no-mod record can excuse a stall.** The stall rule in decision 3 lets a candidate stall be set aside when the game alone shows the same. That needs a record that matches the condition: same scene and camera script, same clock rate, the candidate's calendar pace (probe reference mode with the candidate's factor, as F4 Decision 3 requires), same frame cap and the same frame measurement.

| Release condition | Matching no-mod record | Exception available |
| --- | --- | --- |
| B night; D candidate arm | The D night reference: 01:00, static camera, 1×, probe reference mode at the candidate's factor, frame intervals from the same recorder | Yes |
| C street | None. The E street replay repeats the position, pans and rate commands but runs with the pace shim off, so its calendar workload differs. | No |
| A steady (day 15:30, 1×) | None. N day runs at native pace without the pace shim. | No |
| A retime (day, sustained 2×) | None | No |
| H busy shop | None | No |

Where the last column says no, a candidate stall stays in the verdict. An available record is never borrowed for another condition. A matched reference can be added for any of the four: the same script in probe reference mode at the candidate's factor, about 10 minutes plus 3 to 5 of turnaround in the no-mod process. All four add about an hour. Recommended default: add none up front. Add the one for a condition only when a candidate stall there is claimed to be the game's own, and count the stall against the candidate until that reference has run.

**Soak readings.** R1 as frozen reads managed memory and mod-work drift. It cannot see native growth, and an owned-against-live mesh match can hold while both numbers climb. The soak therefore needs readings R1 does not have, taken at matched checkpoints: the same clock time each game day, at speed 1, after a fixed settle and after a fixed block of retimes (for example ten 1→2→1 changes, each waited to completion), so that anything kept per retime is multiplied.

- Process memory: RSS with the vmmap region summary on the Mac, as E already takes; working set and private bytes on the PC.
- The mod's native counters from E: owned, reserved and live meshes, live vertices, bake scratch bytes.
- Unity object counts by type from the census (`Mesh`, `SkinnedMeshRenderer` and the other foreign types it lists).
- Mono heap and used, beside R1's forced-GC read.

How growth is judged: the counters are compared checkpoint to checkpoint. At the same time of day they should return to the same level. A counter that rises at every checkpoint fails the native-growth gate. Process memory is judged as a trend over all checkpoints against a no-mod soak of the same script, because single no-mod RSS reads move by hundreds of MB ([1.5](#15-the-rss-allowance-is-far-below-what-the-no-mod-process-does-by-itself)). The trend is above reference when process memory rises at every checkpoint and its total rise over the soak exceeds the no-mod soak's total rise by more than the no-mod soak's own largest swing between checkpoints. These are additions beside R1, not changes to its frozen values. Whether they gate is decision 10.

What an above-reference trend does to the verdict: it leaves the native-growth gate unresolved, even with flat counters, and a release run with that gate unresolved does not pass. The gate resolves in one of three ways.

- Confirmed. A second candidate soak shows the trend again. The gate fails, and stays failed until the growth is traced to its source and fixed, or Peter accepts it with a stated bound. The region summary on the Mac, private bytes per checkpoint on the PC, the counters and the census are the tracing inputs.
- Not reproduced. The second soak is not above reference. Both soaks go in the report and the gate passes on the second.
- Plateau. A soak extended by two game days shows the last three checkpoints level within the no-mod swing. Growth that stops is a warm-up cost and not a leak. It is reported with its size.

A second or extended soak costs another 75 to 105 minutes or more and runs only when the first soak calls for it.

What stays uncovered: a native leak that is in neither counter and small against RSS noise would not show in three game days. The per-run E reads and the release rig's fresh E reference stay in the report for that reason. Only a longer soak is a stronger check.

### 3.3 No-mod baseline session

Peter's point in t185u is right: the no-mod numbers change only when their inputs change. Three changes to the plan on the table:

**Keep the reference protocols apart.** The no-mod arms are not one measurement. Each gets its own record.

| Record | Protocol and source | What it holds | Used for |
| --- | --- | --- | --- |
| E street replay | Day 15:30, same position, the 1→2→1 commands and the nine pans replayed in candidate order and timing, pace shim off, no forced collection, passive Mono reads (16q E) | RSS before, after and at +120 s; Mono heap and used; whole-frame intervals; GC rows | Check E; no-mod context for C |
| D night reference | Night 01:00, probe in reference mode with the candidate's calendar factor, forced-collection `heap` reads before the arm and after the window, `mono_heap` start and stop (16q D, probe README) | Heap, live memory after a forced collection, collection count and `GC.Collect` durations at the candidate's pace | Check D; no-mod context for B at matched pace |
| N day and N night (PC) | Full 600 s GC windows at 15:30 and 01:00 with the mod not staged, same cap and city (19c prompt) | The full GC report at native pace | The PC's clean GC baseline |

These windows do not stand in for each other. E has a retime and pans and runs at native pace. D runs at the candidate's pace with forced collections around it. N has neither. Sharing a process is safe only for readings that do not depend on what ran before. Heap size and RSS do depend on it ([1.3](#13-the-night-heap-margin-is-smaller-than-one-heap-step)), so E and D each start a fresh process. An N window may follow in one of those processes for collection counts and durations only, and only after one N window has been measured both fresh and shared and the two agree. Until then N keeps its own processes, which the 19c prompt allows.

**Cache a band, not a point.** One no-mod sample is itself noisy: the reference heap differed by 3.9% between 16o and 16q, and its RSS change at +120 s by more than 300 MB between 16o and 16p. Run each record three times and store minimum, median and maximum for each reading. Today every run compares against a single sample. The band is fixed when it is built. Later samples are logged beside it and not merged in, so it cannot widen over time.

**Key the cache on everything in the reference process.** Game build, machine, fixture hash, RLO version and the rest of the mod set (the reference keeps RLO and Food Trucks), the rig Ops overlay hash, the probe hash and scope hash, frame cap and display mode, the calendar factor, and the protocol version of each record. Any change voids the records it touches.

**Using the band.** Per-fix runs read it as context only. No per-fix gate rests on it. The release rig takes one fresh E and one fresh D sample and uses those same-run samples for its comparisons, as today.

- A fresh reading inside the band confirms the band.
- A fresh reading outside the band makes that record stale. The release run's comparisons stand on the fresh sample and are marked as such. The keys are checked for a change that was missed. Per-fix context drawn from that record since the last confirmation is disregarded. The record is rebuilt before the next release run.
- A stale band neither passes nor fails a candidate.

**Cost.** One pass is E plus D on the Mac, about 30 minutes. On the PC the two N windows bring a pass to about 60 minutes. Three passes are about 1 h 30 on the Mac and about 3 h on the PC, once per key set, and need not be one session. The release rig's two fresh samples are about 20 minutes of windows plus turnaround. These are estimates from the 16p and 16q reference turnarounds.

## 4. Offline, or live game

**Already offline in the repo** (xUnit and tools; these stand in for parts of the rig today):

| Rig concern | Offline cover |
| --- | --- |
| Retired simulator kept after a retime (F, G) | `Gr5RetiredSimulatorTests`: retained and scanned bytes flat over 8 retimes, one `CitySimulator` in the census |
| Mod allocation per call (A) | `Gr5AdvanceAllocationTests`, `Gr5RetimeRepeatAllocationTests`, `OuterModAllocationTests`, `Gr5ReportHeapTests` byte ceilings |
| Advancement spikes of 30 ms or more | `RushSpikeReplayTests`, `Gr5RetimeCommitBudgetTests`, the post-retime food frame test, wall-clock limits through `PerfGate.Under` |
| Mod-work p95, p99 and mean at 30, 60 and 120 FPS | `tools/perf-gate-offline`, as expectations only; its README says it can never pass the native gate |
| Mod-owned footprint | `Gr5RetainedHeapModelTests` and the census model |
| Save callback and bytes | `Gr5SaveCallbackTests`, `SaveCostDiagnosticsTests`, the pinned save hashes |
| Sampler and spike recorder behave | `GcMeasurementTests`, `Gr5SpikeCapTests` |

**Reasonably addable offline:**

1. The rig controller's analysis and verdict code, tested against the raw exports of 16q as fixtures, the way `tools/perf-gate/analyze.py` is tested. The rig agent then writes no analysis code during a run.
2. A multi-day census test in Core: several game days with a retime every game hour, retained bytes flat at each midnight. This is the offline half of the soak and would catch Core-side accumulation before any rig time is spent.
3. A test that a rate change arriving during an unfinished retime always ends with a completion record. The F wait failed in 16q for lack of a second `Clock retime:` line within 150 s, and 16h saw the same in night preparation. That may be the rig's timing, or the mod may merge two retimes without logging the second. It is worth knowing which.
4. Predicted mod-added collections from the allocation measures already in the tests, as an early warning. It is not a verdict: #311 notes Mono allocates more per call than .NET.

**Needs the live game:** Mono's allocation and collector behaviour; body, mesh and bake memory and the spawn and grow spikes (Unity side); main-thread frame time including native work the mod triggers; the save callback inside the game's own save; the road donor handover (#311 calls it "Unity-side and untested offline"); the hold-replay handoff; anything with RLO in the loop; the soak's native side.

## 5. Recommendations, ranked

Time saved is per Mac run unless noted. Risk is the chance of missing a real regression.

| # | Recommendation | Time saved | Risk | Owner |
| --- | --- | --- | --- | --- |
| 1 | Host receipt per window; a window with independently recorded contamination is invalid, is re-run and starts no fix round | A whole fix-and-rig round each time it applies (a fix session plus 1 h 40 to 2 h 20 of rig). On 19 November the boundary reads show contamination in 16p and 16q, and the write-up reports another game running during 19c. | Low, provided a low frame rate or inflated hooks never invalidate a window by themselves ([3.1](#31-per-fix-rig)). Fewer valid slots on the shared Mac. | us; Peter for the rule |
| 2 | Two tiers: the per-fix rig of [3.1](#31-per-fix-rig) and the release rig of [3.2](#32-release-rig) | About 55 min per clean fix run, about 95 against a run like 16q | Low to medium. Deferred to the release rig: 2× GC window, live no-mod arms, G, H. Covered meanwhile by C, the F screen, the census total and the offline byte ceilings. | Peter |
| 3 | Cached no-mod bands, one per reference record, with the wider key list | 20 to 30 min per Mac run; 40 or more on the PC once N is included. One-off cost about 1 h 30 on the Mac and 3 h on the PC per key set. | Low. Gating comparisons use fresh samples in the release rig. A stale band is caught by the keys and by those samples. | us (Peter agreed in t185u) |
| 4 | Pre-built controller, with analysis tested offline, and the F wait fix | 5 to 10 min clean; 18 to 25 min when a retry would have happened | None | us |
| 5 | Restore the sealed PC fixture; refuse to grow a substitute city | 5 to 10 min on the PC, and 19c's 957 / 1,914 city is not comparable with 1,000 / 2,000 | None | Rig bot, us |
| 6 | Measure the sampler's cost once (paired street windows, sampler on and off). If it is negligible, run the sampler in the street window and drop A steady as a separate window everywhere. | 13 min per run that would include A steady; one-off cost about 25 min | Low | us |
| 7 | Seal a night checkpoint at 00:50 and use a fixed settle (both `Clock retime:` lines, then 60 s) | 5 to 7 min per night window | Low. Needs one side-by-side with the played settle. | us, Rig bot; Peter (decision 6) |
| 8 | Add the soak, with native checkpoint readings, to the release rig | Costs 75 to 105 min per release run, plus one no-mod soak per key set, plus a second soak when a process-memory trend appears | It lowers risk: it is the only check for growth over hours. A small native leak stays a residual risk ([3.2](#32-release-rig)). | Peter |
| 9 | Re-run the save check on the PC before GR-5 closes | Costs 10 to 15 min | It lowers risk: the 40 ms verdict has no PC reading. | us |

With rows 1 to 5 the per-fix Mac run is about 45 to 50 minutes without any of the candidate options that needed Peter's decision in the earlier plan (clock jump, 300 s windows).

## 6. What would weaken confidence, and what remains covered

| Thinning | Residual risk | How a player-visible failure is still caught |
| --- | --- | --- |
| No live no-mod arm per fix | The game, RLO or the rig changes and a band goes stale | No per-fix gate uses the band. Cache keys; fresh E and D samples in every release run; the out-of-band rule in [3.3](#33-no-mod-baseline-session) |
| No A retime window per fix | A fix raises allocation only under sustained 2× | `Gr5RetimeRepeatAllocationTests` ceilings offline; the 1→2→1 inside C; the release rig |
| No A steady per fix (unless an allocation fix) | Day allocation creeps up unnoticed | Offline byte ceilings; the census total; GC rows from the street window; the release rig |
| No D per fix | Footprint grows | Census total every run (67.0 MB at 16q); D in the release rig |
| No G per fix | A later change brings back a second simulator during a hold replay | The F screen reads type counts and retained growth every run; G in the release rig |
| F after B per fix | The screen reads differently from the official F after D | Same three reads and criteria; the official F after D in every release run decides the gate |
| Invalid windows leave the verdict | A real regression is discarded as host noise | Only an independent host or rig receipt invalidates; a slow window on a clean host is judged; an invalid window is re-run and passes nothing |
| RSS allowance report-only | Native memory grows. The mesh counters can match while both rise, and native buffers may be in neither counter. | Soak checkpoints after retime blocks with process memory, the native counters and Unity object counts, against a no-mod soak ([3.2](#32-release-rig)). A process-memory trend above that soak blocks the release until it is confirmed and fixed or ruled out, even with flat counters. A leak small against RSS noise remains a risk. |
| Pause judged on `GC.Collect` time per condition | A hitch on a collection frame but outside the collector. Native work the mod triggers outside its timed hooks would not be in the mod-work spike list. | The stall rule in decision 3 counts absolute frame intervals over every frame of the window, not only collection frames. The worst `GC.Collect` marker and its frame interval are reported for every condition. A no-mod record excuses a stall only where it matches the condition ([3.2](#32-release-rig)). |
| gen0 judged as mod-added collections | None that the absolute count covers. The absolute count stays in the report next to the no-mod band. | |
| Read after the rig's exports no longer gated | Memory that appears only while the rig exports | Players do not run rig exports |

Three things on the table would weaken confidence and I advise against them:

- **300 s windows for GC numbers.** A 600 s window already holds only 5 to 7 collections. Halving it leaves 3. If more samples are wanted, repeat the same condition and pool those samples; do not shorten the windows.
- **A clock jump from 00:50 to 01:00 on the candidate.** For the mod a jump is a skip, and the decision register says skips release bodies while the city continues in the background. The window would then start in a different state from a played approach. The jump is fine in the no-mod arm, where 16q already used it.
- **Night and street in one process.** It saves one load of about 2 to 3 minutes. The Mono heap depends on what ran before ([1.3](#13-the-night-heap-margin-is-smaller-than-one-heap-step)), so the heap and RSS reads of the second window would no longer compare with 16j to 16q.

One thing in the current practice already weakens confidence: fix rounds chosen from provisional failures. Fix 25 (#313) carries the same title as fix 23 (#309) and was started from 16q's numbers.

## 7. The plan on the table

| Item | Verdict | Note |
| --- | --- | --- |
| Pre-built controller | Accept | Add the host receipt, hook-cost notes on spike rows and offline tests of the verdict code. |
| Fix the census / F wait | Accept | Also find out why the second `Clock retime:` line was missing ([section 4](#4-offline-or-live-game), item 3). |
| Cached no-mod baseline | Accept, revised | A band per reference record from three passes, the wider key list, context only per fix ([3.3](#33-no-mod-baseline-session)). |
| Fold census, food and RSS reads into windows | Accept in part | E's candidate reads already ride in C. F stays after D in the release rig; after B it is a per-fix screen (decision 11). |
| Night and street in one session | Reject | Small saving, breaks comparability. |
| About four windows | Revise | Two per fix, a third for allocation fixes; six in the release rig. |
| Retime window stays a fresh process | Accept, release rig only | |
| Restore the sealed PC fixture | Accept | And refuse to run without it. |
| Clock jump 00:50 to 01:00 | Reject for the candidate | Use a 00:50 checkpoint and a fixed settle. |
| Fast rig per fix plus a periodic full rig | Accept | This is the main structural change. |
| 300 s windows | Reject | |
| H and N off the every-run PC set | Accept | H to the release rig, N to the baseline session. |

## 8. Decisions for Peter

None of these is applied. Each has a recommended default.

1. **Two tiers.** Default: yes. Per-fix rig as in [3.1](#31-per-fix-rig); release rig as in [3.2](#32-release-rig); GR-5 closes on two consecutive valid release runs.
2. **gen0.** Default: gate the collections the mod adds, at 1.5 or fewer per 600 s at 1× and 2 or fewer at 2×, computed as mod allocation divided by the mean bytes freed per collection in the same window (the #311 method). Report the absolute count beside the no-mod band. The absolute "about 5" stays as the Mac 30 Hz reference figure, not a pass line.
3. **Pause.** Default: keep about 36 ms, applied to Unity `GC.Collect` durations per condition (window type, clock rate and scene).
   - Pool raw samples only across repetitions of the same condition, as the contract's pooling rule does. Never pool day, night, street and 2× together: a busy condition would dilute a slow one, and with 40 samples nearest-rank p95 drops the two longest.
   - With fewer than 20 samples nearest-rank p95 is the maximum. The report states the count, and the judged value is the worst collection of that condition.
   - For every collection frame keep the whole-frame interval and its excess over the neighbouring frames, and report them beside the matching no-mod record (on the quiet 16m run the candidate's `GC.Collect` p95 read 27.2 ms against 21.8 ms).
   - For every condition, whatever its sample count, report the worst `GC.Collect` marker together with the interval of the frame it fell in.
   - Stall rule for the release verdict, with contract row P3's values. It covers every frame of the valid window, collection frame or not, so native work that stalls presentation outside a collection is seen. A stall is an absolute interval of 50 ms or more between presented frames. Until the rig has a presentation trace (RIG-PRESENT is pending in the F4 plan), the whole-frame interval from the rig's perf recorder is used and the report says so. Several slow frames in a row are each a stall.
   - The release fails on more than two stalls per five minutes in a condition, on the same condition stalling in both consecutive release runs, or on any stall of 250 ms or more. The frame list must be complete (`truncated=false`); otherwise the stall verdict for that window is not established.
   - Excess over the neighbouring frames stays on each stall row for attribution only. It never removes a stall.
   - A candidate stall is set aside only when a matching no-mod record shows the same kind of stall at the same rate. The matching records are listed per condition in [3.2](#32-release-rig). Where none exists, the stall stays in the verdict.
   - This rule can only be judged on the PC. On the Mac's 30 Hz headless rig the whole-frame p95 is already 169 to 350 ms in the street window.
   - The sampler's per-window pause p95 stays in the report beside these.
4. **Night heap.** Default: keep 1.47 GB at the sampler report. Make the read after the rig's exports report-only. Add live memory after a forced collection at the end of the window as a reported figure.
5. **The 5% line.** Default: treat it as two things. A leak rule that gates: the live gap to no-mod may not grow across a window or across the soak. A footprint figure that is reported: currently 118 to 140 MB; pick a budget only if a smaller footprint is wanted for its own sake.
6. **Night approach.** Default: sealed 00:50 checkpoint and a fixed settle after one side-by-side with the played approach. No clock jump on the candidate.
7. **Spikes.** Default: follow AGENTS.md and contract row M2. Every spike of 30 ms or more is listed with its hook-cost ratio as a note; the note removes no spike from the count. The street gate fails on p95, and on spikes only by a budget Peter sets; contract row P3 (at most two stalls of 50 ms per five minutes, none of 250 ms from mod work) is the existing candidate.
8. **Invalid windows.** Default: a window is invalid only when the host receipt or a rig receipt fails: F4 Decision 3's list, plus CPU used by other processes from the boundary and per-minute reads. An invalid window is kept in the report, re-run, passes nothing and starts no fix round. A low frame rate and inflated hook cost are investigation notes. A slow window on a clean host is judged, and its spikes stay counted.
9. **RSS.** Default: the 16 MB allowance against a single no-mod read becomes report-only. The E reads and a fresh E reference stay in the release rig. The owned-against-live mesh match stays a pass condition. Native growth is judged at the soak checkpoints (decision 10).
10. **Soak and save.** Default: add the R1 soak, with the native checkpoint readings of [3.2](#32-release-rig), and the PC save check to the release rig. R1's frozen values do not change. Recommended rule for the added readings: a native counter that rises at every checkpoint fails the release. A process-memory trend above the no-mod soak leaves the native-growth gate unresolved, and the release does not pass until a second soak confirms it (a fail, until the growth is traced and fixed or Peter accepts a stated bound) or rules it out as not reproduced or as a plateau ([3.2](#32-release-rig)).
11. **Census lead-in.** Default: the official F stays right after D's candidate arm in the same process, in the release rig. Per fix, F after B runs as a screen with the same three reads and the same criteria. Replacing the official lead-in with F after B would need one release run in which both are taken and agree.

## 9. Limits of this review

- The Mac figures are provisional in their own reports. I used them to judge the measurement, not the mod.
- 19c had not finished when its snapshot was taken. Its numbers here come from the speedup write-up, which reports another game on the host and gives no achieved frame rate for the A windows. What this review says about 60 fps is a hypothesis.
- The per-fix, release and baseline times are estimates built from the 16p and 16q timelines. The soak time is a guess from the 1× and 2× game-minute rates in 16q.
- The native soak readings, the stall rule and the rules for sharing a no-mod process are proposals. None has been tried on the rig.
- I did not verify how the mod treats a ten-minute rig clock jump; the advice against it rests on the decision register's description of skips.
- Whether the sampler and the perf-union probe can run in one window was not checked. Until it is, B and D's candidate arm stay separate windows in the release rig.
