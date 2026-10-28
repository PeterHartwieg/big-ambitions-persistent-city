# Architecture review for performance, 27 October 2026

A read-only review of the current working tree (schema 13, 0.10.0 candidate) with one question in mind: can this architecture carry a highly realistic simulation at the frame cost the performance gate demands? Everything here comes from reading the source and the project's own recorded measurements. The offline suite was run once (1,218 passed). No game was launched and no rig was touched.

## Verdict

The core design is right for scale. Residents live in a discrete-event scheduler, cars in an event-driven FIFO traffic kernel, and the rendered view is bounded by count and radius. Nothing ticks per citizen per frame. Average throughput is not the problem: the project's own 60-second summaries show about 16 events per second at native pace in a quiet window and over 250 per second in a catch-up window, and the simulator handles both without visible cost.

What hurts is latency variance. One citizen event can run a complete multimodal route search on Unity's main thread, and nothing in the loop can interrupt it. The project's captures put the measured stalls exactly there: 52 ms and 67 ms single advancement calls on 24 October, a 32 ms maximum in the 27 October soak after the latest optimizations. The second problem is steady allocation and native-call churn across several per-frame paths. It never shows in the mod's own timers, because it surfaces as collector pauses and native time inside hooked game methods, which is consistent with the 65 ms frame the 24 October review could not assign to any scope.

Two structural changes matter more than any further micro-optimization:

1. Make route planning a resumable, budgeted operation that answers later, instead of a synchronous call inside the citizen event. The model already has the parking state for this (`CitizenPhase.WaitingForRoute`).
2. Stop using strings as identities in hot paths. Place IDs, vehicle IDs, per-leg service tags and string-keyed hashing in routine selection all allocate and rehash on every event.

Everything else ranks below those two.

## What runs per frame today

| Stage | Where | Bound | Measured (24 Oct K2 / 27 Oct soak) |
| --- | --- | --- | --- |
| Discovery slice | `RouteObjectDiscovery.Step` | 1 ms / 2,048 steps; `Prune` over every retained door and station each frame | p95 1.79 ms, max 10.9 ms |
| RLO check | `PopulationRuntime.Update` → `RloIdentityAdapter.Read` | every 30 s, unbounded | 30–262 ms in earlier logs |
| Road preparation and sweep | `MultimodalRoutes.Prepare` → `RoadRoutes.Prepare` / `Sweep` | 2 ms + 1 ms slices | p95 1.02 ms, max 2.9 ms |
| Closure refresh | `CitySimulator.RefreshTrafficClosures` | relevant links only | p95 0.08 ms, max 1.5 ms |
| Population advance | `AdvancePopulation` → `AdvanceFrameSlice` up to 128 times | 2 ms soft, 16 citizen events; one event is never preempted | p95 3.2 ms, max 52.5 ms |
| Snapshot sync | `SynchronizeSnapshot` in the advance `finally` | O(leases + traffic cars × queue length) | inside advance |
| Physical view | `PhysicalPopulation.Update`, vehicles, ambient | 48 walkers, 24 cars, 120 m; selection every 0.25 s | p95 1.35 ms, max 15.4 ms (28.8 ms in K1) |
| Harmony hooks | `TrafficManager.FixedUpdate`, `DrivingAI`, `Pedestrian`, `IntersectionManager` | none, and not timed | unmeasured |
| Save | `onSaveGame` → `Save` | O(city) on the main thread | unmeasured |

Combined mod-work p95 was 8.46 ms in the 24 October PC capture and 0.47 to 1.9 ms on 27 October against a 5 ms target. The remaining maxima are the stall problem, not the average.

## Findings, ranked

### 1. Route planning is synchronous inside the event loop

[`CitySimulator.Step`](../../Core/CitySimulator.cs:236) calls the planner directly ([CitySimulator.cs:366](../../Core/CitySimulator.cs:366), [:401](../../Core/CitySimulator.cs:401), [:484](../../Core/CitySimulator.cs:484)), as do [`ChooseOptionalDestination`](../../Core/OptionalDestinations.cs:55) with up to two plans per optional decision and the meal return in [`StepMealOuting`](../../Core/OptionalVisits.cs:72). One [`MultimodalRoutes.Plan`](../../Runtime/MultimodalRoutes.cs:236) call does, synchronously: one NavMesh walk, up to four subway connector walks, up to two road A* searches over the 18,791-node snapshot with live per-node validation, car access and egress walks, and curb searches ([`RoadRoutes.Near`](../../Runtime/RoadRoutes.cs:462): up to 12 `NavMesh.SamplePosition` calls and a `Physics.Linecast` per candidate). The 19 October baseline measured road A* at 6.4 ms per search on average (3.2 ms after the heap rewrite) with a 138 ms worst advancement call, and walking at 0.36 ms per calculation.

[`AdvancePopulation`](../../Runtime/PersistentCityMod.cs:351) checks its 2 ms budget only between events ([PersistentCityMod.cs:376](../../Runtime/PersistentCityMod.cs:376)). The code comment says so and the 24 October review reached the same conclusion from the data. The architecture therefore guarantees that any frame can last as long as the most expensive plan one citizen needs. At one-third clock the commute preparation and departure windows concentrate hundreds of plans into a few real minutes, and catch-up after a hidden skip stacks them further.

Fix direction: separate deciding from computing.

- A citizen event that needs a route enqueues a planning request keyed by citizen, simulation minute, origin, destination and preferences, and parks the citizen in `WaitingForRoute` with its original deadline. That state already exists and already persists. Requests are served in (minute, citizen id) order so answers depend on simulation time, not frame cadence, which keeps the continuity rule that a snapshot between simultaneous events is valid. Departure happens when the answer arrives, the same way a rejected route retries today.
- The planner gets its own per-frame budget and may stop between the stages of one request: walk, connectors, each A*. [`TransportRoadGraph.Search`](../../Core/TransportComposition.cs:221) can be made resumable with no change in meaning. The workspace already survives between calls; the conditions are that `RoadRoutes.IsCurrent` still holds on resume and that the final `NativePathValid` runs live, exactly as it does now. The per-search `Disabled` memo (`searchedAt` / `searchGeneration`) carries across the slices unchanged.
- `WalkingRoutes.Plan` stays atomic per query, since `NavMesh.CalculatePath` cannot be sliced, but becomes countable per frame. If this Unity version exposes `NavMeshQuery` from the experimental AI namespace, walking paths could move to a job later. I would not bet on that first.

Expected effect: the >50 ms advancement frames disappear by construction while average work stays the same. The price is one more persisted phase transition and tests for interrupted requests. A reload while a request is pending simply re-requests; `WaitingForRoute` already captures everything needed.

Once planning is a request and response boundary, no event in Core touches Unity, and the simulator loop could move to a worker thread with the main thread servicing native queries and the snapshot sync. That is the real headroom for more residents and more detail. I would not start there. The sliced planner takes most of the benefit at far less risk to the determinism and ownership rules.

### 2. Identity and hashing are string-based on every event

Per citizen event the routine layer rebuilds strings and hashes them:

- [`DailyRoutine.SelectRoutine`](../../Core/DailyRoutine.cs:65) hashes `Id + ":hours"`, then for up to ten days concatenates and hashes `Id + ":shop:" + day` and `Id + ":leisure:" + day`; `DailyRhythm.Lookup.OnDay` adds `Id + ":quiet-day:" + day` per day touched. One `Step` reaches `SelectRoutine` several times through `At`, `WorkOnDay`, `NextWork`, `IsHomeRoutineFallback`, `AdmissionFrom` and `HomeEvent` → `TryPrepareCommute`.
- [`DailyRoutine.At`](../../Core/DailyRoutine.cs:51) scans all 1,011 places by string comparison to find the home, then [`DestinationChoice.Choose`](../../Core/DestinationChoice.cs:39) concatenates `choiceKey + ":" + place.Id` and hashes it for every leisure place. That is on the order of a thousand string allocations per leisure routine evaluation.
- [`OpeningIntervals`](../../Core/DailyRoutine.cs:211) allocates and sorts a list for every `Open`, `NextClose` and `NextOpen` call, and one `Step` makes several of those per place.
- Every `places[person.PlaceId]` and `citizenEvents[id]` lookup hashes a string.

None of this is slow on its own. It is the dominant steady garbage source in Core, and in a 128-event catch-up frame it is real time. Unity's collector is not generational, so steady garbage turns into periodic whole-frame pauses that the mod's scoped timers never see.

Fix direction, all behind the existing save format:

- Precompute one `uint` seed per citizen from `Hash(Id, citySeed)` and derive every routine draw by integer mixing of (seed, day, tag). Keep the current outputs reproducible with a one-time mapping test, or bump the policy version deliberately. Cache the hour shift on `CitizenState` as a transient field.
- Give `Place` a transient integer index and an opening-interval cache keyed by day and an hours version. `NativeFoodMarket.RefreshRoutineHours` already knows when hours change.
- Pass the resolved home `Place` into `DailyRoutine.At` instead of the whole list.
- Resolve `HomeId`, `PlaceId` and `DestinationId` to `Place` references at load; keep the strings for serialization only.

### 3. Route legs carry native provenance as parsed strings

Every car leg's `ServiceId` is `native:road:v1:<64-hex topology>:<from>:<to>`, built per leg per plan ([RoadRoutes.cs:586](../../Runtime/RoadRoutes.cs:586)) and parsed with `Split(':')` and two `int.TryParse` calls on every mapping ([`TrafficNetwork.TryTag`](../../Core/TrafficNetwork.cs:506) and the two `TryTag` / `KnownPhysicalFragment` variants in `RoadRoutes`). [`TryMapCore`](../../Core/TrafficNetwork.cs:198) allocates a fresh list and splits every leg each time it runs, and it runs often: on every departure through `ScheduledMinute`, on every traffic boundary through `TryStartTraffic`, on every frame's observation of a leased car that has not yet entered traffic through [`NextTrafficBoundary`](../../Core/TrafficJourneys.cs:146), and over every travelling citizen whenever the network reference changes. A 300-leg car route is about 27 KB of near-identical strings and 300 splits per call.

Fix: keep the string on the wire, decode it once into transient integer fields on `RouteLeg` at plan time and at load, and cache the mapped traffic legs on the `Route` with a topology stamp. `TryMap*` becomes an array walk with no allocation.

### 4. Traffic kernel queries cost O(queue) per call and key everything by string

[`TrafficKernel.Snapshot`](../../Core/TrafficKernel.cs:583) walks the whole link queue, reconstructing every occupant's motion, to answer one vehicle's permission. `GetPermission` and `TryGetVehicle` call it; `JourneyProgress` uses them for every event, position sample and observation of a traffic citizen; and [`SynchronizeTrafficJourneys`](../../Core/TrafficJourneys.cs:208) calls it for every traffic citizen every frame from the advance `finally`. `Link.Queue` is a `LinkedList<string>`, so each hop hashes a vehicle ID, and the curb-exit path uses `Queue.Find(vehicleId)`. `links` is a `SortedDictionary<int, Link>` although IDs are dense from zero. `ForecastEntry` builds a whole projection kernel, with `Concat`, `OrderBy` and fresh lists, once per start section per A*.

At 24 physical cars and a few hundred background cars this is fine. At thousands of cars it is the first Core structure to stop scaling. Fix: an array of links indexed by ID, queue nodes that hold the `TrafficVehicleState` reference, a per-vehicle cached snapshot invalidated by `Reconsider`, and a reusable projection kernel for forecasts.

### 5. The save path is O(city) on the main thread, twice over

[`Save`](../../Runtime/PersistentCityMod.cs:536) runs on the game's save event and does, in order: a full RLO read of 400 citizens with an avatar computation each ([PersistentCityMod.cs:541](../../Runtime/PersistentCityMod.cs:541); 30–262 ms in earlier logs), `ObserveForSave` over bodies, [`TrafficKernel.Capture`](../../Core/TrafficKernel.cs:634) which copies every link with three `ToList()` calls and sorts all vehicles, then `SaveCodec.Write`, which validates the whole state and restores a second complete traffic kernel just to compare progress ([SaveCodec.cs:452](../../Persistence/SaveCodec.cs:452)), and finally serializes a multi-megabyte payload. The traffic snapshot alone writes two city-sized lists, definitions and link states with their incoming-link arrays, for thousands of sections whether or not a car is on them. Loading parses the string three times (schema, shape, deserialize). The 27 October campaign measured supported reload waits around 55 s, game load included.

Fix direction: keep the synchronous contract and cut what it does. Drop the RLO read on save, or compare a version stamp instead of 400 avatars. Make write-side validation a debug switch, or run it on a worker over the produced string afterwards. Capture traffic incrementally. Stop writing definitions that the topology ID and the policy constant already determine. For load, decode on a worker as initialization already does, in one streaming pass.

### 6. The physical view repeats native scans that are cheap to cache

- [`PhysicalVehicleBody.Valid`](../../Runtime/PhysicalVehicles.cs:73) → [`RandomAmbient`](../../Runtime/PhysicalVehicles.cs:168) does three `GetComponentInChildren<T>(true)` calls plus `VehicleScriptOwnership.NativeOnly` (a `GetComponentsInChildren<MonoBehaviour>(true)`) every time. `Valid` is evaluated in the `Position` getter, in `Owned()` which the `ReachedPrefix`, `RequestPrefix`, `RescuePrefix` and `DespawnerPrefix` hooks hit on native per-vehicle paths, in `DeferNativeForce` and `ApplyNativeRotation` per owned body per physics step, and twice per body in `EndNativeFixedStep`. For 24 cars that is thousands of hierarchy walks per second inside native `TrafficManager.FixedUpdate`, where no mod timer runs. A verdict stamped with the frame and physics step keeps the recheck-every-frame guarantee at a fraction of the cost.
- [`TaxiNeedsReplenishment`](../../Runtime/PhysicalVehicles.cs:135) runs `IsTaxi` over the whole vehicle pool on every native `AddVehicle` draw through [`SelectIdle`](../../Runtime/PhysicalTrafficHooks.cs:110). Taxi-ness is a prefab property; classify each pool slot once per prefab reference.
- The selection in [`PhysicalPopulation.Update`](../../Runtime/PhysicalPopulation.cs:151) is a six-stage LINQ pipeline over all citizens four times a second, with `PositionOf` (traffic lookup plus `Route.PositionAt`) per travelling citizen, two `bindings.Values.ToArray()` copies per frame and three `Count(...)` passes in `Diagnostics`. A reusable candidate list with a comparer and a cheap spatial prefilter (citizens bucketed by place cell, updated on event) removes both the allocation and the O(N) scan.
- [`PhysicalPedestrians.TryAcquire`](../../Runtime/PhysicalPedestrians.cs:101) serializes the appearance data to JSON twice as an equality check on top of the native mesh merge, and selection allows four acquisitions in one 0.25 s tick. One acquisition per frame and a field-wise comparison would flatten the appearance-build spikes the performance gate names.
- `AmbientPedestrians` refreshes RLO reservations, a reflection or CityApi read, in `Update` every frame ([AmbientPedestrians.cs:336](../../Runtime/AmbientPedestrians.cs:336)) and again inside `EndCyclePrefix` ([:146](../../Runtime/AmbientPedestrians.cs:146)) for each unheld ordinary pedestrian the native cycle touches. [`IsOffscreen`](../../Runtime/AmbientPedestrians.cs:312) runs `GetComponentsInChildren<Renderer>` per pedestrian in the resume and retire loops, and the held and departing bookkeeping calls `List.Contains` on the active pedestrian list. Frame-stamp the reservation read and ask the renderer question once per pedestrian per frame.
- [`RouteObjectDiscovery.Step`](../../Runtime/RouteObjectDiscovery.cs:86) prunes every retained door and station every frame, about seven native property reads per component. If the 60-second summary shows discovery p95 above a millisecond with zero sweeps, this is why; a one-second cadence or scene-event-driven pruning is enough.
- [`TrafficRoadNetwork.NodeCurrent`](../../Runtime/TrafficRoadNetwork.cs:159) reads `associatedIntersection` through `FieldInfo.GetValue` per node per closure refresh and per search node. `AccessTools.FieldRefAccess` gives the same read without reflection dispatch.

### 7. The food layer creates and destroys Unity objects per decision

[`FoodSupplyReader.ReadMenu`](../../Runtime/FoodSupplyReader.cs:72) creates a `FullServiceBusinessSimulator` ScriptableObject, runs native `SetUp`, and destroys it, once per candidate business per meal decision (up to eight) and again at checkout. With 1,914 consumers and three meals a day that is roughly 5,000 decisions per game day, each with several native setups. The 27 October change trimmed checkout from four setups to three; the decision path still pays per candidate. A supply cache keyed by registration and game minute, which the checkout reuse already implements in miniature, makes repeated reads within a minute free. [`DiscoverShops(64)`](../../Runtime/NativeFoodMarket.cs:119) also builds 64 escaped place-ID strings per frame to notice a newly rented shop; a one-second cadence or the existing `BuildingRegistration.Reset` hook is enough.

[`HouseholdFinance.PostDay`](../../Core/HouseholdFinance.cs:89) sets every budget's next posting to exactly midnight, so all 1,914 postings land in the same simulation minute and drain at the 128-event frame cap over about 15 frames, during which the city is not caught up. Posting them as one batch event, or spreading them through the first hour as the employment check already does, removes a predictable daily hitch.

### 8. The walking cache is dropped every 30 seconds

[`MultimodalRoutes.Refresh`](../../Runtime/MultimodalRoutes.cs:103) clears every cached NavMesh path on each periodic RLO check, whether or not a gate changed. The 20 October review listed this and the fix kept it by design. The result is a 30-second sawtooth of NavMesh recomputation. Cache keys are still six `double.ToString("R")` calls per lookup ([`PointKey`](../../Runtime/WalkingRoutes.cs:240)), also flagged then. Invalidate on a gate or NavMesh change signal and key by the two `Position` structs with a struct comparer.

### 9. The 30-second RLO read is an unbounded periodic stall

[`RloIdentityAdapter.Read`](../../Runtime/PersistentCityMod.cs:229) runs every 30 s, at initialization and on save, and reads 400 citizens through reflection with an avatar computation each. The 27 October binding reuse removed metadata construction, not the per-citizen work. Either spread the 400 reads across frames and compare at the end, or ask RLO's CityApi for a cheap change stamp and read in full only when it moves.

## What is already right

The next step should build on this rather than replace it. The event scheduler and traffic kernel never tick per citizen. The road A* uses generation-stamped scratch arrays and an indexed heap. The gate grid, closure batch memoization, sample scopes and per-search node memo remove most repeated native validation. Road snapshot capture, traffic network construction and city initialization are sliced or off-thread. The physical view is bounded by count and radius. Budgets are explicit, and the 60-second summary already carries the right counters.

## Recommended order

1. Resumable, budgeted planning with `WaitingForRoute` as the parking state (finding 1). This is the only change that removes the measured stall class. Verify with the existing frame CSV tooling: the advancement maximum should fall to the budget plus one NavMesh query.
2. Frame-stamped ownership and reservation checks in the physical layer and one acquisition per frame (finding 6). Small diffs, large reduction in untimed native work.
3. Transient integer identities and seeds in Core (findings 2 and 3), with a differential test that old and new routine outputs match for a fixed seed set, or an explicit policy-version bump.
4. Supply snapshot cache and batched budget postings in the food layer (finding 7).
5. Save path trimming (finding 5) and walking cache invalidation (finding 8).
6. Traffic kernel data layout (finding 4), when background car counts grow.

## Measurement gaps to close first

The 24 October review could not attribute the slow advancement calls because the frame CSV records scopes, not events. Before or alongside item 1, record per frame the slowest event (citizen id, `Step` branch, number of `Plan` calls, NavMesh and A* milliseconds) and a per-frame managed allocation delta (`GC.GetAllocatedBytesForCurrentThread` where the runtime supports it, else `Profiler.GetMonoUsedSizeLong`). Time the hook bodies in `TrafficManager.FixedUpdate` and `IntersectionManager.UpdateIntersections` separately; they are the largest untimed mod code paths today.

## Not verified

No game was launched. All timings quoted are the project's own captures and were not reproduced here. The per-call costs attributed to native component searches and reflection are order-of-magnitude estimates from Unity behaviour, not measurements on the rig.
