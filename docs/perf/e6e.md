Status: implemented (offline); not verified by rig

# E6e — body spawn and dwell at a vehicle

## Method

Foreground xUnit on the Mac, .NET SDK 10.0.400 / runtime 10.0.11, Debug build. `E6eBodyMeasure` compiles the complete production `PhysicalPopulation` and `DwellSpots` sources with native scene/movement/pool doubles. Each component has 50 warmups and 1,000 samples. Reported time is PC hook work including double bookkeeping; it is not native prefab instantiation or a Unity frame. Selection/release uses eight waiting residents at one eligible vehicle and the existing eight-body cap, with two four-attempt selections. Dwell samples deliberately include a selection cadence on every update.

Reproduce after checking `pgrep -f testhost` and waiting for any other test host to finish:

```sh
env PATH="/opt/homebrew/bin:$PATH" DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec" PC_E6E_MEASURE=/tmp/pc-e6e-measure.txt dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~E6eBodyMeasure
```

These are headless windows: no autosave exists and no save runs inside a measured action. Native timing must use the autosave guard in [batched check 3](../checks/e6e.md#in-game-checks-for-the-next-batched-rig).

## Cheap win: cached layout equality

The first pass used tuple equality on two `Position` values. Their inherited value equality boxed them during every cached lookup. Explicit coordinate comparisons preserve exact invalidation and remove those allocations. Both measurements ran the real `TryVendorQueue` cache path under the same harness.

| Cached lookup, n=1,000 | Before | After |
| --- | ---: | ---: |
| p50 (ms) | 0.000584 | 0.000167 |
| p95 (ms) | 0.000750 | 0.000208 |
| max (ms) | 9.720500 | 0.000209 |
| bytes/op | 544 | 0 |
| spikes >=30 ms | 0 | 0 |

In that matched pass, eight-body selection/release p95 changed from 0.068917 to 0.062667 ms (42,119.5 to 33,410.0 B/op); eight-body dwell p95 changed from 0.055125 to 0.051000 ms (31,346.8 to 26,992.0 B/op). Maxima include host scheduling/GC; they are not a native frame attribution. The four known hotspots remain parked.

## Final component measurements

The final pass includes queue-layout-change retirement. These samples ran in the nonparallel Street Food test collection during the full foreground unit run.

| Component, n=1,000 | p50 (ms) | p95 (ms) | max (ms) | bytes/op | spikes >=30 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Cold eight-spot sampling | 0.001542 | 0.001709 | 0.002250 | 1,080.0 | 0 |
| Cached queue lookup | 0.000167 | 0.000208 | 0.000334 | 0 | 0 |
| Eight-body selection/release, pool double | 0.049667 | 0.050458 | 0.808042 | 33,410.0 | 0 |
| Eight-body dwell, including selection cadence | 0.038500 | 0.038958 | 0.431416 | 26,992.0 | 0 |

Each E6e component is below the 5 ms p95 gate offline. This does not measure combined game mod-work.

Flag from the first full unit run: the existing `RushSpikeReplayTests.EveningRushHasNoAdvancementIntervalAtOrAbove30MsAndKeepsTheUninterruptedState` failed on a **57.176417 ms** advancement interval at minute **3993.630831643027**, with **one GC** during it. This is its unmodified Core rush fixture, separate from the E6e component windows and from a native whole frame. Core and the parked hotspots were not edited. Isolated and full-suite rerun results are recorded in [checks](../checks/e6e.md#offline-checks).

## Review fix Opus-r1-1

The completed-meal hold adds a transient captured departure deadline to each physical binding; it adds no saved state or native sampling. `E6eBodyMeasure` now also times eight completed-meal bodies waiting at the stand. Same foreground Debug harness, 50 warmups and 1,000 samples per component, native scene/pool doubles:

| Component | p50 (ms) | p95 (ms) | max (ms) | bytes/op | spikes >=30 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Cold eight-spot sampling | 0.001666 | 0.002583 | 9.752958 | 1,080.0 | 0 |
| Cached queue lookup | 0.000167 | 0.000208 | 0.000209 | 0 | 0 |
| Eight-body selection/release, pool double | 0.061625 | 0.062500 | 2.337375 | 33,474.0 | 0 |
| Eight-body dwell, including selection cadence | 0.050959 | 0.051834 | 0.446125 | 26,992.0 | 0 |
| Eight completed-meal bodies awaiting departure | 0.003875 | 0.004083 | 0.005917 | 1,392.0 | 0 |

All component p95 values remain below 5 ms. The eight-body selection allocation changes from 33,410 to 33,474 B/op with the additional deadline field; the completed-meal window asserts all eight bodies remain acquired. No autosave or save runs in these headless windows. No hotspot or optimization outside this finding changed. Whole-frame and native spawn costs remain unmeasured offline.

## Whole-frame and native spawn cost

Unmeasured offline. Native `Instantiate`, appearance assignment, NavMeshAgent work, Street Food tick, Unity rendering and whole-frame timing require the batched rig. No total-game mod-work or whole-frame pass is claimed from these component numbers. The batched gate remains PC mod-work p95 <5 ms with every >=30 ms spike listed by component and condition; whole-frame cost is reported separately.
