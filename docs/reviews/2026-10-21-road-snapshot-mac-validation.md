# Road snapshot fix: native Mac validation, 21 October 2026

**The signal-driven invalidation fix is validated on the local Mac rig.** The exact reviewed candidate kept one road snapshot through 4,452 native light-change callbacks over three minutes, with zero rebuilds, zero route-preparation waiting frames and zero final population lag. Both baseline runs reproduced the churn. This validates the targeted mechanism and improves the observed long-frame tail; it does not eliminate all hitches or establish Windows recording performance.

## Builds and controlled conditions

Peter explicitly authorized the local Mac rig for validation after requesting an uninstalled candidate. Only the isolated `agentmac` profile was temporarily changed. The rig was initially stopped and unreserved; ownership was acquired for this test and released after cleanup. No Windows connection, installation or data transfer occurred, and neither normal game profile was touched.

- Baseline: reviewed installed-PC source build `6e0e5b58776a5a4430b4b585e03f7233d1dc5dedfd94200cb7f603a44ea55de1`.
- Candidate: reviewed road-invalidation DLL `9c15a6f1f492bd62143b95279fbc581fdd06332ef5534e2da496ae24e34ab897`. No production code was changed during validation.
- Native Apple Silicon Big Ambitions build 3682, isolated runtime 6, Metal background mode, 960×540, low graphics, 30 FPS cap, two job workers, reduced process priority and muted audio. This is a rendered test player, not an interactive recording benchmark.
- Same configured Mac modpack, including RLO 0.45.0; all mod DLL hashes except Persistent City were identical. Saved mod-option values matched the original profile and each other. Persistent City was enabled with physical population, 48 pedestrian slots, 24 car slots and a 120 m radius.
- Same frozen disposable `FT Road Snapshot Comparison 20261021 Checkpoint`, 957 residents, 1,011 places, and a 9,093-section road graph. Player remained outdoors at the same position. Native normal speed (`Time.timeScale=1`) was used with needs held and autosave interval set to 3,600 seconds for the test. The frozen checkpoint hash remained unchanged.
- Run order: baseline A, candidate B, baseline C. Every run loaded the same checkpoint and had a 45-second running warm-up before a 180-second capture. B and C each started in a fresh process and had the same explicit 45-second paused settling period after initial road/physical readiness. A had a longer paused period while the harness was corrected, so **C versus B is the primary matched comparison**, with A corroborating the reproduced failure.

The bridge briefly returned an old completed-load state before starting its deferred reload. The harness was corrected to require the requested save name, completed city load and a minimum delay. A request made during that transition timed out; no timed capture used it. Readiness requires road/physical initialization and less than one game minute of lag; the observed paused fractional-clock offset was 0.333 minutes. The scripts retain these attempts rather than presenting them as successful tests.

## Results

| Measure | Baseline A | Candidate B | Repeat baseline C |
| --- | ---: | ---: | ---: |
| Capture duration (seconds) | 180.02 | 180.01 | 180.12 |
| Native traffic-light callbacks | 6124 | 4452 | 7028 |
| Road snapshots started | 33 | 0 | 45 |
| Frames waiting for routes | 2735 | 0 | 3426 |
| Preparation time, total (ms) | 20765.5 | 1676.2 | 22954.2 |
| Preparation maximum (ms) | 770.4 | 529.9 | 770.6 |
| Closure refresh calls | 192 | 3756 | 23 |
| Closure refresh maximum (ms) | 266.99 | 8.23 | 198.90 |
| Frame interval, mean (ms) | 61.50 | 47.92 | 52.22 |
| Frame interval, median (ms) | 37.20 | 36.45 | 35.58 |
| Frame interval, p95 (ms) | 156.25 | 97.60 | 124.32 |
| Frame interval, p99 (ms) | 576.26 | 227.79 | 413.74 |
| Frame interval, maximum (ms) | 1547.81 | 673.59 | 1107.59 |
| Frames over 50 ms | 894 | 898 | 759 |
| Frames over 100 ms | 266 | 182 | 215 |
| Frames over 150 ms | 157 | 107 | 141 |
| Final population lag (game minutes) | 118.02 | 0.00 | 181.65 |
| Population / physical failure flags | false / false | false / false | false / false |

Against repeat baseline C, preparation consumed **92.7% less total wall time**, and maximum closure refresh fell from **198.90 ms to 8.23 ms**, while closure refresh could execute on every measured frame instead of only 23 of 3,449 frames. The candidate's scene-search counter remained at one throughout; its only logged rebuild reason was `initial`, before measurement. Genuine topology invalidation remains covered by the existing offline mutation regressions; this native comparison did not deliberately mutate topology or gates.

Mean frame interval improved by **8.2%**, p99 by **44.9%**, and frames over 100 ms fell from 215 to 182. This is a short controlled observation, not a statistically established general FPS claim. Not every measure improved: the candidate had 898 frames over 50 ms versus 759 in C, and its median interval was 36.45 ms versus 35.58 ms. The candidate advanced the population continuously while the baseline was usually stalled, so this is not equal completed simulation work per frame. Actual game-clock progress and signal-callback counts also differ; exact endpoints and all per-frame records are retained. All three runs caught up to zero lag after pausing and reported no population/physical failure.

## Remaining hitches and platform limits

The candidate still had six preparation frames above 16 ms, corresponding to periodic station discovery, with a maximum of **529.85 ms**. The matching built-in observation logged station search 529.79 ms and door search 503.40 ms. Its worst whole-frame interval was 673.59 ms. Individual population advancement also exceeded 100 ms. These costs remain separate work; this result is not an all-spikes fix or recording clearance.

The existing Mac package has known platform limits: Bakery's Windows-only business asset bundle and Hyperreal's optional Windows rendering bundle are unavailable, and several City Performance/Hard Mode Patch optimizations decline the private Mac binary. These conditions were shared by both variants. Native Windows behavior, genuine driving/physical red-light correctness, and exhaustive modpack compatibility are not established by this timing test. See the [earlier modpack scope](2026-10-21-modpack-compatibility.md).

## Instrumentation and evidence

A new temporary, profile-guarded `RoadSpikeProbe` DLL was identical in every run. It performs no game-state mutations. Lightweight Harmony timers record `PopulationRuntime.Update`, `MultimodalRoutes.Prepare` and `CitySimulator.RefreshTrafficClosures`; another prefix counts native `WaypointManager.TrafficLightChanged` callbacks. Frame intervals are measured in `LateUpdate` with a monotonic clock. Samples stay in memory during capture and are written after it stops. Timings are inclusive and nested, so they must not be summed. They include instrumentation overhead. Frame intervals include the rig's cap, rendering, all mods and OS scheduling. No full-scene inspection or full-state serialization occurs per measured frame.

The ignored evidence directory `build/road-invalidation-20261021/mac-validation/` contains frozen checkpoint receipt, original profile backup, installation/option receipts, helper source and build manifest, start/end snapshots, frame records, three full game logs, per-minute distributions in `comparison.json`, controller logs, and cleanup receipts. The source and candidate ZIPs from the reviewed build are unchanged. The prior four-model reviews certify the implementation, not these later measurements.

## Cleanup

The rig was stopped gracefully, the diagnostic helper was moved into local evidence, the original Persistent City DLL and all pre-existing profile files were restored and hash-verified, and the original preference domain was restored exactly. The original Mac rig DLL is `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`. **2373 pre-existing files, including 906 save-related files, match their backup.** The installation manifest verifies, the rig is stopped and its reservation is released. The new disposable FT checkpoint and ignored evidence are retained. The candidate is not left installed on the rig and has not been installed on the PC.

[Implementation, offline regressions and independent reviews](2026-10-21-road-snapshot-invalidation.md).
