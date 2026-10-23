# Windows modpack smoke — 21 October 2026

> Historical result: the retiming failure below was subsequently [fixed and retested](2026-10-21-retiming-fix.md), with the reviewed fix [installed in both PC profiles](2026-10-21-retiming-pc-install.md). Other limitations remain open.

**Result: a reproducible traffic-retiming failure blocks recording clearance.** The reviewed `e0756b4a…` DLL starts, moves its physical population, handles native 6× withdrawal/1× reacquisition, saves and reloads successfully. The Food Trucks bridge's 60× clock acceleration makes Persistent City stop. A fresh-process, clock-only reproduction also fails, without a truck or driving command. No production code or installed mod configuration was changed during this test.

## Exact scope

- Candidate SHA256: `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`; RLO `0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`, DLL `2f360a36…`.
- Isolated Windows product `Big Ambitions Agent Tests`, Direct3D 11 background mode, 960×540, 30 FPS cap, four job workers. Normal game stayed closed. Disposable saves start `FT PCity Windows Modpack 20261021`.
- Persistent City enabled, physical population enabled, 48 pedestrians, 24 cars, 120 m. All 18 selected mod assemblies loaded, with AgentOverlay and a temporary diagnostic helper.
- The actual rig has a private Food Trucks 0.10.2 bridge (`e033780a…`, Core `ae08dd61…`), different from the normal game's installed package. Its `drive` command **simulates travel using accelerated time and parking**, as confirmed by this run's log. Its immediate `Complete: arrived` result is not a Driven Rides pass; the older retained `ride-results.jsonl` was not fresh evidence. The earlier suspicion of a stale result was superseded by this log evidence.
- City Performance is the previously reviewed private-game-compatible 0.4.2 variant (`8799c099…`, verified against private game `055700fd…`). Background autosave is disabled on the rig. Driven Rides uses clock percent 33 here, versus 25 in the normal profile. HMP's indebted-start scenario is off here. Those settings were preserved.
- The temporary helper reads state, bindings, hashes and patch ownership, and invokes the native speed controller for the 6→1 check. It installs no timing-profiler hooks. Its output/serialization adds overhead; these results are not a frame-rate benchmark. Initial helper refusal due to Windows path separator normalization was corrected before the successful run, without changing the candidate.

## What passed

The retained evaluator reports **84 passed checks and two failed checks**. These are smoke-test invariants, separate from the candidate's previously reported 886 offline tests and four-model review.

- Actual RLO contract reads returned Ready with 400 immutable donors. All 957 distinct resident identities and all donor identities stayed identical across initial, normal and speed checkpoints and fresh-process reload.
- Across 14 running samples, body IDs and bound citizen IDs were unique and within configured limits; bindings matched citizen names/appearance seeds and remained valid. Observed repeated samples showed displacement for 56 pedestrian identities and seven car identities, with stable pedestrian appearance fingerprints. No sampled traffic overshoot occurred.
- Native 6× speed withdrew all physical bindings. Returning to 1× reacquired pedestrians. This native time-scale transition is different from the bridge's 60× clock acceleration that failed below.
- Normal and speed checkpoints serialized exactly the live Persistent City state. A fresh process loaded the speed checkpoint's exact saved payload hash `d4560759709905ff83b25e56081fd7c3bc7ccf429e1325d72386016aaf960897`, validated it, retained every identity, and initialized healthy. The live simulation advanced about 0.33 game minutes before the reload pause, so its subsequent live-state hash appropriately differs from the stored payload.
- Rivals and Voogle Maps opened and closed through native UI controls. RLO's More apps menu exposed Voogle Maps; a first attempt using a stale button list was refused. A Windows console encoding issue during the map listing was corrected in the local test wrapper and the already-open map was inspected, without replaying its open action.
- Bakery appears in the native business catalog with 33 businesses. Windows Hyperreal shaders restored (SSR, SSGI, volumetrics and clouds); its map-open/map-close transitions logged successfully. An off-screen city render was captured and visually inspected. It establishes rendering, not recording-camera image quality or every mod asset's correctness.
- City Performance reported `verified=True`. HMP's Bakery pricing, Internet Cafe and RLO patches were active; ownership capture also includes Persistent City, Food Trucks and Driven Rides hooks.

## Reproducible failure

The first failure occurred when the rig's simulated truck trip invoked its 60× run-until clock. Native log:

```text
Persistent City stopped: System.ArgumentException:
Traffic readiness precedes its committed motion schedule.
TrafficKernel.Restore → TrafficKernel.RescaleTime
→ TravelTiming.Apply → PopulationRuntime.Update
```

After a fresh process successfully loaded `FT PCity Windows Modpack 20261021 Speed`, the second test used only bridge clock controls:

1. Resume at speed 1 with needs held.
2. Set bridge `time` to `{"speed":60,"keep_needs":true}`.
3. Allow three seconds, then pause and inspect state.

Persistent City again reported `failed=true`, `enabled=false`; its physical population had been disposed. The saved namespace retained the checkpoint hash above. The failed in-memory state was not written as a new checkpoint. The direct native 6× check had passed; this is evidence of a clock-rate retiming failure, not proof that every fast-forward mode fails or that the performance changes introduced it. A minimal Core regression and fix to the readiness/motion invariant are still required. No save-schema relaxation or unreviewed replacement was deployed.

## Remaining limits

- Initial normal-speed dense-crowd samples accumulated 40.74 game minutes of lag; the first paused sample reached 42.75 before recovering to zero. There is no controlled old/new performance comparison in this run.
- The rig's drive shortcut does not test the actual driver, contacts, red lights or the Mac watchdog finding. A genuine Driven Rides test remains pending.
- HMP's Bakery Help-discovery guard declined the private game binary; Bakery later stopped its bounded Help polling after 120 seconds. HMP's Hyperreal discovery/map-return patches also withdrew because of the game/HDRP guard and missing or changed hooks. Published Hyperreal itself loaded and rendered. These are partial coverage, not an all-features pass.
- Existing null-renderer diagnostics, missing Bakery localization and LOD warnings were observed. A short new-game smoke does not cover all mod business workflows, a full day, weather, remote recording cameras or an established production save.

## Evidence and cleanup

Ignored local evidence: `build/modpack-compatibility-20261021/windows-smoke/`, including `evaluation.json`, snapshots, `active-modpack.json`, native logs, the rendered image and command receipts. Raw profile backups remain only on the PC at `C:\Users\Peter\HART-Backups\persistent-city-windows-smoke-20261021T101119Z`.

The rig was stopped and the helper removed. All 3,213 pre-existing profile files, including 962 save files, were restored/verified against the pre-test backup; preferences were restored exactly and all 97 original manifest entries verified. New disposable FT fixtures remain available. Cleanup verification is recorded in `cleanup.json`; final ownership release is recorded in `lock-released.json`. The normal game stayed closed. The reviewed installation remains in both profiles. This test does not clear it for recording until the retiming failure is resolved and retested.
