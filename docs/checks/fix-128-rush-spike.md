Status: implemented (offline); not verified by rig

# Fix #128 — evening-rush advancement spike

Refs #128. Base `f46b3dd`, writer 25, no schema change. Perf numbers: [perf file](../perf/fix-128-rush-spike.md). Rig evidence for the spike: [rig 6 Nov results](rig-2026-11-06-results.md#performance).

## Finding

The rig's 134.96 ms advance (day 3 ~19:19, 957 residents, 56 events in the frame) cannot come from the frame loop letting work run on. `AdvancePopulation` checks its 4 ms soft budget after every unit, so one unit, or time outside the units, took about 130 ms. The rig log line could not say which: it reports the maximum advance but not its slowest unit, the native reads inside it, or whether a garbage collection ran during it.

The offline replay (matched city; see the perf file) drives Core at the same scale and pace. No Core unit reaches 30 ms: the slowest in four runs is 4.38 ms. No frame reaches 30 ms, and p95 is 0.13 ms in the evening. The cause is therefore not Core managed work at this scale. What remains reachable from one unit:

1. native route-planning slices (already bounded by F3 admission, but one native query cannot be preempted);
2. optional-stop native reads in `OptionalStopEnvironment` (frontage rebind, park edges, audience sampling);
3. a Mono garbage collection that completes inside the call. The replay's one 60 ms frame, under parallel tests, sat outside every unit, which shows how a process-wide pause lands in this interval.

## Implemented

- **Replay.** `tests/RushSpikeReplay.cs` (fixture) and `tests/RushSpikeReplayTests.cs` (xUnit, non-parallel collection). The test runs day 3 18:30–19:30 after a two-day warm-up. It asserts the rush is present (>250 travelling, >50 cars), single unit <30 ms, advancement interval <30 ms (the failure message carries the GC count during the slowest frame) and p95 <5 ms. It also asserts that the sliced save hash equals an uninterrupted advance of the same city. `tools/rush-spike-replay` prints the evening and morning windows as JSON, and with `-p:SourceRoot=` it compiles the same fixture against another Core/Persistence tree.
- **Attribution (Runtime, diagnostics only).** The 60-second population summary now adds, after "maximum events in one frame": `Slowest advance: unit <ms> (<kind>), optional-stop reads <ms>, <n> garbage collections completed during it`. It also adds the observation's optional-stop read calls, total, maximum and park trigger scans. Unit kinds: `route-planning`, `citizen`, `citizen-route-begin`, `traffic`, `food-continuation`, `other`, `snapshot` (the closing `SynchronizeSnapshot`).
- **Park trigger scan (Runtime, perf).** `ParkEdge` used to run `FindObjectsOfType<LocationHappinessTrigger>(true)` on every uncached call. A failed lookup was never cached, so the scan repeated on each resting-stop attempt at that park, inside an advancement unit. It now reuses one trigger array until the loaded-scene set (handle plus `isLoaded`) changes. The per-call checks and their order are unchanged. Only scene-authored, non-clone triggers can match, so the result changes only with a scene change.

No Core or Persistence change. Saved state is byte-identical: the replay hashes equal the `f46b3dd` baseline in both windows.

## Decisions

- The task allows a behaviour change only where the replay shows a cause. The replay clears Core, so this slice changes no budget, cap or event order. It adds the attribution the rig needs, plus one cheap native-cost removal on a path the evidence points to (evening leisure arrivals).
- A `TryConversation` hoist (one spot plan per arrival instead of per partner) was prototyped and reverted. The replay never reaches that multiplicity: seam-call totals were equal before and after.
- `ParkEdge`'s failed lookups still repeat their per-collider NavMesh sampling. Caching that failure would change behaviour if the NavMesh changes, so it is left alone until rig attribution shows it matters.
- The replay test runs serially. Parallel tests share the process GC and CPU, and one parallel run showed a 60 ms interval that none of its units caused.

## Offline evidence

- `python tools/build.py --game-managed "<Steam>/Big Ambitions_Data/Managed"`: passed.
- `dotnet test tests/PersistentCity.Tests.csproj`: 3,180 passed, 0 failed (includes the new replay test).
- `dotnet run -c Release --project tools/rush-spike-replay`, before (`f46b3dd` Core) and after: numbers and hashes in the [perf file](../perf/fix-128-rush-spike.md).

This is offline evidence only. It does not show that the in-game spike is gone.

## In-game checks for the next batched rig

Every check runs in a fresh head FT city on the owned rig, never Peter's save. The rig player runs at High priority, autosave is off (original recorded, verified off before each window and after reload, restored at cleanup), and no save falls inside a window.

1. **Evening rush attribution and gate.** Setup: fresh 957-resident head city, run to day 3 at speed 1, clock factor 1/3. Steps: measure 17:30–20:30; parse every population summary line. Expected: every `maximum advance` is under 30 ms. For any observation at or above 30 ms, the same line names the slowest unit kind and its ms, the optional-stop read ms and the GC count. The checks file of the follow-up slice records that attribution. Advancement mean per frame stays at or below the 6 Nov 0.439 ms. — rig-automated (existing capability: `rig flow run` with Player.log observation-line parsing as in `build/rig-1106/analyze_log.py`, extended to the new fields).
2. **Morning and lunch windows.** Setup: same city. Steps: measure 07:00–09:30 and 11:30–14:30 on day 3. Expected: no `maximum advance` at or above 30 ms. Slowest-unit fields are present in every summary line. — rig-automated (existing capability: `rig flow run`, Player.log observation-line parsing).
3. **Park trigger scans and resting stops unchanged.** Setup: matched pre-fix (`f46b3dd`) and fixed builds on the same fresh city seed. Steps: run the evening window on both, then travel to the Hamptons and back on the fixed build. Expected: on the fixed build, `park trigger scans` per observation is 0 or 1 unless a scene loads or unloads in it, and rises after the Hamptons load and unload. Optional-stop read maximum is under 30 ms. Resting stops at parks still appear: count `resting on a bench` residents via the inspector or chance tally. Their count matches the pre-fix build within the same seed. — rig-automated (existing capability: `rig flow run`, Player.log parsing; pending capability: optional-stop/resident-phase readback for the resting-stop count).
4. **Combined mod-work union.** Setup: a head city with a player food business, so the union probe can arm. Steps: arm `tools/e5b-native-rig-proof` over the evening window. Expected: combined mod-work p95 under 5 ms, and every interval at or above 30 ms flagged with its component. — rig-automated (pending capability: player-business fixture provisioning for the probe; fresh-city rental through `shop_fixture` was refused on 6 Nov).

An in-game behaviour release still needs a `rig_test` session (Codex Sol, high) with before → red / after → green proof on its PR. This slice performs no such run and claims none.
