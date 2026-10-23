# Traffic-closure refresh comparison

This harness compiles the actual Core, MultimodalRoutes, RoadRoutes and traffic importer against synthetic Unity/native surfaces. It compares the full traffic-kernel snapshot, relevant section IDs and road-current status after each operation against a frozen previous source tree.

```sh
python3 tools/traffic-closure-regression/compare.py \
  --baseline build/traffic-closure-refresh-20261021/baseline \
  --output build/traffic-closure-refresh-20261021/recheck
```

The default run checks 24 state snapshots with 35 assertions per revision: repeated passes, temporary closure/reopening while the stubbed native frame counter stays unchanged, fresh single-section boundary checks, generation wraparound, replaced/removed gate snapshots, a native-read exception and retry, twelve structural changes, and an empty closed section reopening. Core tests separately check ordered batch/legacy equality, detached relevant IDs, and exception/nesting cleanup. Comparator failures use explicit exceptions and remain enforced under `python -O`.

Add `--benchmark` for five warmed synthetic scenarios. Before any timed loop, the runner compares the exact fixture fingerprints from both compiled source trees. Three alternating baseline/candidate process pairs each measure five batches of 300 complete `CitySimulator.RefreshTrafficClosures` calls. Every timed row must retain its preflight fingerprint and instrumented native-read count. Allocation samples are retained individually and reported by median: the runtime can add small incidental allocations to some batches. Tiered compilation is disabled. Do not run competing builds or benchmarks while measuring.

- Junction-heavy: 1,025 nodes, one synthetic junction per 16 nodes, 957 demands.
- Long sections: 1,025 nodes without junctions, 128 demands.
- Gate-heavy: the long-section fixture plus 512 nearby nonblocking gates.
- Sparse/empty: 1,025 junction nodes with one/no demand.

Fixtures use isolated kernel demand IDs to exercise the actual closure-refresh and scheduling paths. They are not saveable resident populations; no completed synthetic vehicle is advanced through a resident callback. Fingerprints include full kernel states, including schedules and vehicle ownership. The native instrument counts selected field getters and junction group reads, not all Unity calls. Allocations are .NET 10 thread allocations. Geometry, gates and scene objects are synthetic; these are neither native frame timings nor FPS measurements.

Timed passes hold closures unchanged and exercise steady-state refresh. Changed closures and snapshot invalidation have correctness coverage, not timing coverage. Gate replacement uses the direct `SetAccess` invalidation safeguard; production normally replaces the entire road adapter when gate geometry changes, as covered by the existing gate/handoff harness. The generation-wrap test detects a missing stamp clear when the generation resets; it does not establish every possible future wrap implementation. The recorded timing run preceded only the added frame-counter assertion and comparator hardening (34 assertions then); all production files, benchmark fixtures and timed loops are unchanged.
