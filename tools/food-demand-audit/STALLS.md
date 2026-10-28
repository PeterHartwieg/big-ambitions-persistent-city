# Diagnosing frame stalls

This is an opt-in helper for the isolated FT test rig. It changes diagnostics, not Persistent City's simulation or ordinary-save configuration. The current helper is compiled and tested locally; its new capture behavior has not yet been exercised in the native game.

## What the October 27 data supports

Population advancement dominates 203/261 candidate rush-A mod frames over 5 ms, 201/256 in rush B, and 184/244 in the soak. The largest rush-A mod frame is 45.66 ms, including 41.85 ms in `PhysicalPopulation.Update`; the largest rush-B frame is 29.43 ms, including 28.61 ms there. These inclusive scopes overlap, so they must not be summed.

The ten-minute soak's largest wall interval is 328.71 ms, while its largest measured mod frame is 32.20 ms. Those are separate maxima, not a matched frame. The old helper retained whole-frame/CPU/GPU distributions but no raw observations to pair with its mod frames. Existing CityPerformance traces are a separate source whose session, frame and sampling-phase correspondence must be checked before joining.

Read-only recovery of 68 retained CityPerformance files adds a save-path lead: selected soak EOF intervals of 308.38 / 271.91 ms include 31.32 / 29.48 ms of native serialization and 1.06 / 0.28 ms of same-frame mod work. Much remains unexplained. Other retained rush stalls show small mod work and no collection-count increase. CityPerformance retains only an 8,192-row ring and the 128 slowest hitches per session; these are censored observations, not a new benchmark distribution. EOF intervals differ from the helper’s Update intervals, and delayed profiler counters cannot fill that gap. Full retained analysis is private under `build/stall-diagnostics-20261027/retained-stall-analysis.md`.

Standard performance runs now exclude autosave by Peter's instruction. The next optimization investigations are individual advancement events (including route planning), physical admission/observation, and the remaining uninstrumented parts of the outer update. The save-path observations above remain historical evidence; save-specific profiling belongs in a separate explicitly requested run. The current evidence does not justify changing schedules, traffic policy, or optional-visit behavior to improve timings.

## Capture

**Autosave must be disabled before performance timing.** Record the isolated profile's original setting, turn autosave off, and verify it is still off before every measured window, including after reload. If the setting cannot be verified, do not start a clean performance capture. Finish any in-progress save before starting; keep manual checkpoints outside the measured window. Retain save-event evidence and invalidate a window containing save activity rather than silently removing those frames. Restore the original autosave preference during rig cleanup. This applies equally to candidate, comparison, soak and logger-overhead runs; it does not change the user's ordinary profile.

Use the existing owned-rig controller and a named disposable `FT Food Routines…` save. Keep food/routine audit observers off for a performance capture. Send a fresh request ID with `frames-start`, run the intended workload, then a different fresh ID with `frames-stop`. Do not reuse answered IDs: responses are immutable and the dispatcher skips answered requests, including after a normal helper restart. This is not crash-exactly-once execution if a mutation succeeds before its response is published.

`frames-stop` exports two CSVs under the helper directory:

- `frames-observations-<requestId>.csv`: monotonic interval endpoints, previous/current Unity frame IDs, GC collection-count deltas, allocation-recorder availability/value, latest observed CPU/GPU values and source timestamp, repeated-timestamp flag, native minute and time scale.
- `frames-mod-work-<requestId>.csv`: the existing mod/component work totals, plus observed opening/closing timestamps and the next observed frame ID. Nested component timings remain inclusive.

The raw observation buffer holds 65,536 value records and allocates once when recording starts. Aggregate frame and per-call buffers are also pre-sized and capped at 65,536 before the timestamp seed. Recording stops at the aggregate frame cap; any raw/per-call drops remain explicit. This covers about 12 minutes at 90 observations/second, but only 7.6 minutes at 144. Treat the intended window as complete only when `stopReason == "frames-stop"`; an automatic sample-cap stop is a shorter capture. The marshaled storage estimate is not an exact managed-heap measurement on Unity/Mono. Timing patches, request polling and reports still introduce overhead. Buffer allocation is paid before sampling begins; the existing aggregate lists no longer grow during capture. Seven fixed value slots replace the former two component arrays allocated per Unity frame; this does not make the entire profiler allocation-free.

No CSV export or per-frame text logging runs inside the sampler. Export happens after recording stops. Avoid `frames-status` during a clean timing window: it computes distributions and allocates. The final report records all active diagnostic command names, frames and timestamps in bounded storage, plus active-report counts and frames. The start response contaminates the partial first interval.

A save/guard change stops recording and excludes the uncertain lifecycle tail; sampler errors stop timing patches. The report exposes stop reason, errors, dropped rows, skipped/duplicate/reversed frame observations, partial boundaries and discarded lifecycle tail. Save/export before unloading the helper; an in-memory trace is not crash-persistent.

## Interpretation

Wall intervals span successive `Probe.Update` observations, not exact render/presentation boundaries. The first interval is partial, and the current incomplete mod frame is excluded. CPU/GPU values are latest asynchronous observations with **unknown ownership of a wall interval**. Repeated source timestamps are visible; no automatic mapping is invented. Returned zero readings are retained separately from missing readings; a returned zero can itself mean Unity had no measurement. The historical CPU/GPU summary still filters positive samples for compatibility.

A GC counter increment means a collection occurred between observations; it does not measure its pause or attribute allocations to this mod. Allocation bytes are also a latest whole-game recorder observation. `paused` in the raw CSV means `Time.timeScale == 0`, not an independent game pause-state query.

The offline analyzer selects slow wall intervals and nearby mod frames without subtracting nested timings or assigning GPU ownership:

```sh
python3 tools/food-demand-audit/analyze_stalls.py \
  --observations /path/to/frames-observations-REQUEST.csv \
  --mod-frames /path/to/frames-mod-work-REQUEST.csv \
  --report /path/to/frames-stop-REQUEST.json \
  --threshold 50 --context 2 --limit 30 \
  --output /path/to/stalls.json
```

The analyzer reads the frequency from the required stop report, checks both exported CSV names/request IDs and row counts, and marks partial-start and observer-contaminated intervals. It exposes truncation, lifecycle termination and report counts in its output. This binds file selection but does not cryptographically bind CSV contents to the report. If bounded intervention logs overflow, affected later intervals are marked `observerContaminationUnknown` rather than treated as clean. Neighboring frames are context, not exact causal attribution.

For the next native run, use the same helper in each arm, interleave candidate/comparison windows, retain CityPerformance traces and all Player logs, and first compare capture on/off overhead. If large stalls sit outside measured mod scopes, use a focused native/OS trace to distinguish main-thread work, GC, rendering/present waits and scheduling/I/O; do not infer those causes from wall-time residuals alone.

## Local validation

The 28 October helper adds per-frame event entry-branch/slice attribution, nested native-path/A-star timing, separate fixed-step/intersection mod-hook component columns, and a named managed-counter delta. See [measurement definitions and limits](../../docs/reviews/2026-10-28-event-attribution.md). `managedDeltaBytes` is signed live-heap change when `managedCounterKind` is `mono-used-heap-bytes`; it must not be reported as allocation. `eventSlicesDropped` or event cross-frame errors prevent complete attribution. Existing asynchronous allocation observations remain separate. The helper adds diagnostic allocation and must be compared with capture disabled before performance claims.

```sh
dotnet run --project tools/event-observation-regression -c Release
dotnet run --project tools/frame-observation-regression -c Release
dotnet run --project tools/diagnostic-response-regression -c Release
python3 -m unittest discover -s tools/food-demand-audit -p 'test_analyze_stalls.py'
```

Build against the retained private PC references without installing or launching:

```sh
PC_FOOD_CAMPAIGN=stall-diagnostics-20261027 \
PC_FOOD_SOURCE=../performance-pc-20261027/source \
python3 tools/food-demand-audit/build_probe.py \
  --game-managed build/performance-pc-20261027/refs
```

The old ignored `build/performance-pc-20261027/build_probe.py` campaign injector is incompatible with these new source seams and is intentionally not modified. It supplies additional commands the tracked helper does not have. Use a fresh campaign builder if those commands are needed; do not reuse the old scripts blindly. Stop/export before any load, and archive or remove pending requests together with their responses when swapping helpers.

Raw game references, diagnostic exports and build products stay ignored. No accounting rule, receipt, price, traffic rule or production runtime is changed by this diagnostic increment.
