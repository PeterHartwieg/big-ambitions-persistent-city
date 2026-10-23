# Frame cost review, 20 October 2026

A read-only look at where 0.6.2 spends main-thread time, after lag spikes in the series-2 agent playtest on the PC rig. The fix for item 1 is recorded at the end. Item 2 was subsequently implemented with [offline comparisons and synthetic measurements](2026-10-21-gate-access-performance.md); native frame-time gains remain unverified.

## What the logs show

The rig's `Player-agent.log` for attempt 13 (windowed, 957 residents) has these Persistent City summary lines:

- 3,025 frames, 959 events, 4,421 ms total, 67.7 ms maximum advance in one frame.
- 3,427 frames, 15,563 events, 1,864 ms total, 56.4 ms maximum, 128 events in one frame.
- 3,926 frames, 9,753 events, 4,493 ms total, 61.3 ms maximum, 128 events in one frame.

Those totals cover `AdvancePopulation` only. The physical population, `RefreshTrafficClosures`, `routes.Prepare()` and the 30-second dependency check are not timed, so the real cost is higher than the log says.

The 2 ms advance budget cannot stop a single event, so a 60 ms maximum means one route request took about 60 ms. The 128-event frames hit `MaxAdvanceEventsPerFrame`, which fits a backlog being worked off after the population was held back.

CityPerformance traces from the rig show bursts of 25–90 ms frames that sometimes repeat about 30 s apart. They only cover the last 100 s before each quit, so I can't confirm the 30-second cycle from them.

## Findings, largest first

### 1. The 30-second dependency check rebuilds all routing data

`PersistentCityMod.Update` calls `routes?.Clear()` after every RLO check, whether or not anything changed. On the next `Prepare()`:

- `PublicRoadAccess` runs `FindObjectsOfType<FenceDoor>(true)` and walks each door's colliders.
- `EnsureStations` runs `FindObjectsOfType<SubwayStation>(true)` with a `GetComponentsInParent` per station.
- `new RoadRoutes` runs `FindObjectOfType<CurrentSceneData>()`.
- All three land in the same frame. RLO times its own whole-scene searches on this PC at 45–55 ms each, so this frame may well be over 100 ms. Not measured for these three calls.
- The 18,791 waypoints are copied again, the topology SHA-256 is recomputed and the traffic network is rebuilt, in 2 ms slices over many frames.
- `ReadyForPopulation` is false until the rebuild finishes, so citizens wait and then catch up up to 128 events a frame.
- The walking cache (up to 1,024 NavMesh paths) is emptied.

`RoadRoutes` and `TrafficRoadNetwork` already find most native changes on their own: `IsCurrent`, `ReferencesCurrent`, and per-node checks during every search. The rebuild also caught two things they don't: a change in the public gates, because `PublicRoadAccess` reads door state once in its constructor, and changes to nodes no search touches, such as a road captured without car access that later allows cars. The review found the second one.

### 2. Gate checks scan every gate

`PublicRoadAccess.ClearSegment` tests a segment against every vehicle gate. It runs for every edge the road A* expands, every step of a found path, and every frame inside `RefreshTrafficClosures` for each road section with cars on it. The gates don't change between rebuilds, so a per-edge flag computed once, or the 50 m cell grid `RoadRoutes` already has, would make this a lookup. I expect this to be most of the 60 ms single events.

Status, 21 October: the current source uses a bounded 50 m gate index with the same final intersection test and conservative full-scan fallbacks. See the [implementation and evidence](2026-10-21-gate-access-performance.md).

### 3. Car searches have no cost ceiling

Each car trip runs up to two A* searches with a 30,000-expansion limit, which is more than the whole graph. A goal that can't be reached gets the full graph searched, gate checks included. `MultimodalRoutes.Plan` already knows the best walking or subway estimate (`bestScore`) before it searches roads. Passing it as a ceiling would end hopeless searches early.

Status, 21 October: the development source now passes a conservative ceiling through the full car planner into A*. See the [implementation and offline evidence](2026-10-21-road-search-ceiling.md); review and native validation are tracked separately.

### Smaller items

- `RefreshTrafficClosures` runs every frame, untimed. Each relevant link checks every native node's contracts and every segment against all gates.
- `PhysicalPopulation.Update` selects candidates every 0.25 s with LINQ over all 957 citizens, a journey position calculation each and an `OrderBy`. Cheap but allocates.
- Every road search allocates four dictionaries. Every walking-cache lookup builds its key from six `double.ToString("R")` calls.
- The 30-second RLO read uses reflection and calls RLO's avatar check for all 400 donors.

Status, 21 October: the development source now reuses closure-refresh storage and shares native validation within each synchronous pass. Successful gate checks are retained for the same immutable access snapshot, and closure-refresh timing is logged separately. See the [implementation and offline evidence](2026-10-21-traffic-closure-refresh.md).

## Fix for item 1

The 30-second dependency check keeps its rhythm, and doors and stations are still searched every 30 s, but the road snapshot is only thrown away when the public gates changed or the snapshot finds itself stale.

- **Gates.** `PublicRoadAccess.Refreshed` searches for doors (`FindObjectsByType`, unsorted, since the comparison ignores order) and compares the gate boxes as a multiset. A change installs the new access and drops the road snapshot, because the traffic network filters its sections by gates when it is built. The rebuild log line names the first differing door.
- **Leaves.** `FenceDoor.doors` are the leaves DOTween swings or slides, so a box read at a random moment differs from the last one. While a door's non-leaf colliders are unchanged, its box only grows to take in each new leaf pose. It always covers what a fresh read would give, and stops changing once both leaf extremes have been seen. A door replaced by an identical one keeps its record.
- **Untouched snapshot state.** A search only checks the nodes it touches, and a node captured without car access is never touched. Each refresh with unchanged gates starts a staged sweep (1 ms or 1,024 units a frame) over every waypoint and junction with the existing current-checks. A running sweep is never restarted. Any difference marks the snapshot stale and the next preparation rebuilds it, as a failed search would. The node check now also compares the `stop` flag and whether the neighbour list was null, two things the traffic capture depends on.
- **Missing road scene.** A snapshot taken without `CurrentSceneData` is dropped at every refresh, so the scene is looked for again on 0.6.2's cadence.
- **One whole-scene search per frame.** Door, station and road scene searches each need a frame of their own; `Prepare` returns false and the population waits a frame otherwise. The station refresh runs 15 s after each door search. The guard is shared by all route planners, so a clock-rate change cannot add a second search in the same frame.
- **Diagnostics.** The 60 s summary reports road snapshots started (and how many for gate changes), frames spent waiting for routes, and the slowest RLO check, door search, station search and preparation frame.

Behaviour that changes from 0.6.2: a gate box can stay larger than a fresh read for the rest of the session once a leaf has been seen open. That errs towards closing roads.

## Review

Four reviewers over four rounds: Astra, GPT 6.1 Sol, Opus 5.5 and Grok 4.7. All four approve round 4. The first version (a 300-second door rescan and cached stations) was rejected by all four; the design above comes from their findings. Briefs, diffs, responses and every review are kept in `build/frame-cost-review/`.

Offline evidence:

- Built as the frozen 0.6.2 tree with only `Runtime/RoadRoutes.cs`, `MultimodalRoutes.cs`, `PersistentCityMod.cs` and `TrafficRoadNetwork.cs` changed, against the PC rig's reference assemblies with warnings as errors. DLL SHA-256 `5d4c2bd2272428298dffd99904a717f07ff45fd73573b559c2278c25412fd301`. The same tree with the four 0.6.2 files reproduces the tested 0.6.2 DLL `d0d8fbde…` byte for byte. The full source is in `build/frame-cost-review/package-source/`. An earlier package (`d6bc67f6…`) was built on a pre-0.6.2 tree with older RLO adapters and must not be used.
- The road handoff harness has two new blocks that use the actual `RoadRoutes` and `PublicRoadAccess`. The sweep block covers an unchanged snapshot, an excluded node that later allows cars, an interrupted sweep, neighbour nullness and the stop flag. The gate block covers a leaf captured open then closed, closed then opened, two non-overlapping leaf ends, an identical replacement door and a moved frame. Each case fails when its fix is reverted. The road speed harness (602 assertions) passes.
- The shared Unity stubs gained the members these need.

## To check in game

- The door and station search times and the preparation frame maxima in the summary line, against today's traces.
- Gate changes should stop climbing after each used gate's first open and close. A door that keeps appearing in the log has no non-leaf colliders.
- Whether a grown box on an outward-swinging street gate closes a curb lane or sidewalk (Opus).
- With persistent cars in flight, buy or sell a gated property and watch for "Saved road section policy disagrees" from `HasCompatibleDefinitions`. A gate change keeps the topology ID while the sections may differ. This is possible in 0.6.2 too.
