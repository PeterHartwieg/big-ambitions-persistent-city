# Track F: city view from interiors

F0 discovery/prototype, 31 October 2026. Peter approved this independent track on that date: F0 spike, F1 shop view and visible customer entry, F2 penthouse/upper-floor view, F3 polish. F0 branches directly from `codex/persistent-city`; it uses no A1/A2/A3 code or save contracts. A later **separate E2c** will disable our view if the third-party “True City Interiors” mod is detected. No detection, integration or third-party implementation research belongs to F0.

## Evidence boundary and decision

**GO for a guarded physical-population experiment; NO-GO for city drawing in F0.** `InteriorCityView` defaults to `"off"`. Opting in can retain our street residents around a verified shop entrance while indoors. **It does not make the street visible through the detached interior's windows.** The drawing prototype is an explicit no-op. There is no camera, renderer, lighting, volume, culling, facade, layout, navmesh or native economy write. No new Harmony patch is installed, and `NativeCityClock` is unchanged.

This is clean-room work from our repository and the vanilla game DLLs at the explicitly supplied Steam `Big Ambitions_Data/Managed` path. Only selected vanilla types were inspected with local ILSpy; outputs remain ignored under `build/f0/vanilla/`, never in Git. No web/Workshop search, other mod binary/source, game launch, raw save, profile, ModsLocal, bridge, lease or test-rig access was used. No third-party implementation was encountered. Existing compatibility code is unchanged.

Read-only inputs: `BigAmbitions.dll` SHA-256 `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`; `ExternalPlugins.dll` SHA-256 `7d43e9d42d2f464674cff045bcb048c48e0f87aec0a987baa34122ac6e3415d1`. `BigAmbitions.InteriorDesigner.dll` supplies the public floor-element cache; the build now references it in place and packages none of these assemblies. Findings below describe this local vanilla build, not a promised contract across game versions. Runtime reads fail closed when evidence is missing.

## Vanilla entry/exit and the meaning of inside

1. `CityBuildingController` selects a usable street entrance and requests `BuildingManager.EnterBuilding`, carrying its door id. The ordinary `EnterBuildingCoroutine` fades, exits a previous building/parking, sets `building` and `cityBuildingController`, and calls `CityManager.SetTrafficSpawnDistanceTarget` with **`entranceDoors[0].doorTransform`**, regardless of the selected entry door. That exact first transform is F0's view anchor; a different usable door is never substituted.
2. `BuildingManager.IsInsideBuilding` means an initialized manager with a non-null `building`. It does not mean the player is near the street door, that loading has finished, or that an interior is on the ground floor. It can become true before the layout is ready.
3. The coroutine loads the structure through `BuildingSizeResolver`, `BuildingSizeContainer` and `BuildingStructureEntry`, enables its serialized local transform, fills `currentBuildingVersion`, `exitZones`, `interiorBounds` and `InteriorElementsHelper.InteriorElementsCache`, and loads items/layout. It changes the player's agent type, warps to a saved position or an exit zone's spawn point, selects `indoorCamera`, and raises `onEnterBuilding`. Delayed entry actions/behavior warm-up finish before `enteringBuilding` clears. The vanilla saved-position branch's x > 750 test is further evidence of detached interior space; it is **not** F0's eligibility rule.
4. `ExitZoneDespawner` requests `ExitFromBuilding`. The exit coroutine handles selected vehicles/drive-in bays, restores the traffic target to the player, moves to the matching exterior door with a safe-exit test, resets indoor state, clears building references, raises `onExitBuilding`, and selects the street/vehicle camera. `ResetIndoors` disables the indoor volume/lights, serializes native interior design and disables layouts. F0 invokes none of these writers.

F0 waits for `enteringBuilding`/`exitingBuilding` to clear. During an incomplete load, uncertainty keeps today's hidden behavior; existing bodies may be released during that transition and reacquired after eligibility is proven. Once the view is active, nearby bodies survive ordinary updates within its strict radius and caps. This is a physical lease experiment, not F1's continuous customer door hand-off.

## Do native traffic and pedestrians continue inside?

| System | DLL evidence | What is established / still missing |
|---|---|---|
| Native ordinary pedestrians | `PedestrianSpawner.Init` subscribes to building events. Its entry handler releases active pedestrians; `SetActivePedestrians` refuses while inside. `PedestrianPlayerRangeHandler` uses the city traffic target. | **No ordinary native ambient pedestrian continuation** in the reviewed vanilla path. No headless live count/motion measurement was made here. Special/indoor actors have separate lifecycles. |
| Native traffic | `SetTrafficSpawnDistanceTarget` stores the entrance and calls Gley's `Manager.SetCamera`. `TrafficManager.FixedUpdate` continues initialized wheel/vehicle/job work without a building-inside test; its camera positions/density/grid stay entrance-relative. `TrafficComponent` has no interior event stop. | **Source supports continued traffic simulation**, subject to actual enabled/pause/game-speed state. Not proof of visible or moving cars in a running interior; final-head native yes/no evidence is required. |
| Our physical walkers | Exclusively instantiated `HumanDefinitionLow`/`ThirdPersonCharacter` bodies, explicitly rejecting a native `Streets.Pedestrians.Pedestrian` component. Our per-body stop predicate previously stopped inside. | Vanilla's ordinary pool release is not a release of our walkers. F0 changes only our stop predicate. Native animator/culling/NavMesh behavior still needs rig observation. |
| Our physical cars | Inactive verified ordinary native slots, existing ownership and physics hooks. Our `NativeReady` previously refused inside. | F0 permits these only with an active eligible view; no native traffic lifecycle is overridden. Slot availability and safe admissions remain existing constraints. |

Historical interior/headless population reports establish background simulation/save continuity, not native street-body motion or rendering inside. Offline doubles in the F0 regressions establish our branch decisions, not Unity execution. Record separate native traffic and native ordinary-pedestrian yes/no results on the rig; do not call our residents “native ambient pedestrians.”

## Ground-floor eligibility and settings

All keys are flat in `config/persistent-city.json`, packaged to `<ModRootPath>/persistent-city.json` by the existing build/loader. Edit while unloaded, then reload; no file watcher or rewrite.

| Key | Missing/default | Range / effective meaning |
|---|---|---|
| `InteriorCityView` | `"off"` | Exact strings `"off"` / `"on"`; any unusable value falls back off with one diagnostic. |
| `InteriorViewRadiusMetres` | 60 | Finite effective 30–120 m; effective physical radius = min(this, `PhysicalRadiusMetres`). Numeric strings accepted; infinities clamp; NaN/wrong types default. |
| `InteriorMaximumPedestrians` | 24 | Integer 0–48, further capped by configured `MaximumPedestrians` (default 48). Zero disables this interior body kind. |
| `InteriorMaximumVehicles` | 12 | Integer 0–24, further capped by configured `MaximumVehicles` (default 24). |

Numbers outside their range clamp and log once after load, including cross-field caps. Unusable integer values/in-range fractions take their default. Endpoint fractions follow the existing numeric converter convention (values beyond an endpoint clamp). Missing keys preserve defaults. Effective settings are logged once; each entry logs its address, numeric entrance anchor, radius and caps once, with at most one fallback line per entry. No strings are built on ordinary cached reads. The global developer-setting validation/ranges remain unchanged, especially with the flag off.

Eligibility requires an established, known retail shop under our existing shop-activity taxonomy, a matching host controller, its first door, a loaded layout, exit zones, and a finite floor element in vanilla's cache belonging to that layout. Any elevator exit or `MultipleHeightsBuildingController` rejects it. Absolute interior floor height minus exterior door height must be **≤3 m**; >3 m in either direction rejects it. Missing/ambiguous evidence stays hidden. The first floor transform and entrance position are cached per entry. Geometry rechecks occur on completed entry and no more than once every **2 seconds**. Review r1 caches the configured bool at startup and the full context refresh per frame; each query still checks cheap hidden/transition flags for immediate same-frame invalidation. Suspend, failure and lifecycle events invalidate the cache. Exceptions latch closed until exit/new entry.

`NativeGameSpeed` validates the existing native `currentGameSpeed` field and creates a typed read delegate once when enabled. The enabled view/hidden path uses that delegate, with no per-frame `FieldInfo.GetValue`, reflection binding, strings, hierarchy discovery or eligibility scan. Failure to bind disables the feature for the city with one fallback line. The flag-off path retains the original reflective accessor and decisions. The current regression links the real accessor and compares the pre-r1 adapter (including real reflection) against the fixed adapter; the original F0 benchmark's bool speed double omitted that cost and is superseded for predicate claims.

| Layout/address category | F0 classification | Evidence limit |
|---|---|---|
| Single-height established retail, non-elevator exit, floor within 3 m of first street door | Candidate ground-floor shop | Equal height is eligibility evidence, **not** proof that the detached floor aligns horizontally with the street. |
| Elevator-marked exits, multiple-height structures, upper floors/penthouses | Hidden fallback | `ExitZone.isElevator` and the multiple-height controller are concrete native signals. |
| Residential, office, warehouse, school/leisure-only, empty or unknown registration | Hidden fallback | Retail/establishment and existing shop taxonomy are checked, not names or ownership alone. |
| Individual addresses and serialized size/version/layout assets | **Unclassified offline** | DLLs define the loaders; scene/Addressables data supplies the actual transforms and address mapping. No asset inventory or live scene was read. A size name or street number cannot safely establish ground-floor status. |

The rig must record size/version/address, first-door position, floor height, exit elevator flags and resulting eligibility for three representative own shops plus an elevator example. There is no address allowlist or invented claim that a particular shop is ground-floor. Refusal of all tested shops because the native floor evidence is unavailable is a valid red result, not permission to guess.

## Physical behavior and restoration

The physical hidden predicate still releases everything for fast-forward/6×, `timeScale > 1`, loading, parking or the time machine. Only the inside-building term is waived while the setting is on and eligibility is active. Focus becomes the cached entrance and retention uses **min(street, interior radius)** (no street 1.4× hysteresis). Admission is pulled inward to **release radius /1.2**: default **50 m admission /60 m release**, so sampling noise near the outer edge does not immediately churn newly acquired bodies. Every retained binding, including an arrival handoff, shares the strict release radius and sub-caps. The flag-off arrival path remains unchanged. Lower caps never enlarge a pool or change identity, routes or ownership.

F0 always waits until the player is **outside** before first constructing/preflighting `PhysicalPopulation`, including when the setting is on. Reloading inside starts with background population only until the player exits; re-entering can then retain already-established physical bodies. This avoids treating the unverified native preflight/ownership path inside an interior as F0 support.

With off/default, hidden travel, pedestrian stopping, vehicle readiness and the physical construction gate preserve their previous decisions. BeforeAdvance/Update still observe/release every hidden body, street focus/radius/hysteresis and global limits remain the same, and saving only observes as before. The measured full-pool scan skip cannot change acquisitions/cooldowns: both pools have no free slot, and observations/queue movement and final cleanup still run.

All state is runtime-only. Save while active runs **ObserveForSave → clear view → release bodies**, using `finally` to release even if observation throws. With off, saving retains the previous observe-only path. Reacquisition cannot occur in the suspension frame. Exit/another building, scene unload while inside/view-active, save identity change/loading, mod unload/disable, fast-forward and the time machine clear the view and release owned bodies through the existing cleanup path. Auxiliary scene unloads during ordinary outdoor play leave street bodies alone. The native `onTimeMachineStarted` event occurs synchronously before the first time-machine Update/tick, so restoration does not depend on MonoBehaviour Update order or a new clock patch. Existing fast-forward hooks restore before native clears traffic. Cleanup failure stops saving; an eligibility/read exception fails closed. No native camera/volume state needs restoration because none was changed.

## Why drawing is a no-op, and what later slices need

`CullingGroupController` privately owns its `CullingGroup`, player distance reference, spheres and pending/distributed LOD dispatches. It binds the **main camera**, with vanilla bands 60/200 m, and exposes no scoped anchor/radius override or safe snapshot/restore API. A direct `OnLod0`/renderer-enable sweep would fight queued vanilla LOD updates, omit other city cullables, and fail the strict-radius/restore contract. Even bypassing culling does not put the street in the indoor camera's frustum: ordinary interiors use separately placed structure instances. A second camera would introduce projection/compositing, depth/lighting and restore work without establishing a safer contract. F0 therefore changes neither culling nor cameras, and preserves indoor lighting/volume exactly.

F1 must first prove an independently authored main-camera approach with strict distance culling and coherent projection/geometry. If a patch really is unavoidable, propose the smallest guarded target, document why, and log foreign Harmony owners once; F0 adds **none**. Rendering success must not be inferred from active body leases.

World-space state a later layout move would invalidate (F0 moves nothing):

| Owner | Native/our state to audit before F1 relocation |
|---|---|
| Customer spawning/pools | `BuildingManager.customerSpawner`, `CustomerSpawner`'s pool dictionary and pooled customer agents/behavior state. `IndoorCustomerSpawner` samples cached `interiorBounds`, exit zones and NavMesh; spawn warps use exit-zone world positions. Merely moving the parent does not reinitialize active agents/paths. |
| Exits/doors | Cached exit-zone list, player/vehicle spawn transforms, despawner exit ids and trigger occupancy. `ExitZone.Awake` caches world open/closed positions/rotations (including elevators/drive-in bays); tweens and cached door data would remain stale. |
| Vehicles | Exterior `driveInEntrances`, garage slots, blocking checks, collider bounds, inside/outside vehicle spawn calculations and selected vehicle rigidbody/rotation. Stored street data and safe exterior exit must remain native. |
| Navigation/items | Loaded/baked NavMesh data, agents and in-flight paths, item NavMesh targets, placement grid/ground bounds, interior bounds, floor-element cache and employee/queue targets. Building placement completion explicitly refreshes item nav targets; translation alone does not establish a complete rebuild. |
| Our presentation/routing | Cached place entrances, `DwellSpots` door/queue geometry, optional-stop geometry, route/native waypoint fingerprints, physical leases and held corners. Never move a committed city route or reuse stale world-space data to fake arrival. |

| Slice | Scope / acceptance boundary |
|---|---|
| **F0 (this branch)** | Default-off settings, read-only eligibility/anchor discovery, guarded physical retention, restoration and offline proof; drawing no-op explicitly accepted by the task's fallback. No schema change. Final-head rig proof still required before merge. |
| **F1** | Ground-floor shop view and continuous visible customer approach/entry. First resolve detached projection/culling and all world-space caches above. Prove one resident/customer owner and no second purchase/body; never equate an entry animation with a native sale. No relocation, facade hiding or door hand-off was implemented in F0. |
| **F2** | Upper floors/penthouse/elevator view, actual floor/height/projection contract and transitions. No F0 address/height guess becomes an upper-floor authorization; height LOD remains absent here. |
| **F3** | Lighting/window/depth/culling polish, bounded refresh/invalidation and full restoration/performance in varied scenes. |
| **E2c (separate)** | Future third-party detection guard only; no foreign implementation dependency/research. |

Go/no-go: effective radius **30–120 m**, default **60 m**; default caps **24/12** within global **48/24**; height difference **≤3 m**; geometry cadence **≥2 s**; all failed/uncertain reads close. Native combined **mod-work p95 <5 ms**, flag **every spike ≥30 ms**. Whole-frame p50/p95/max is separate. A future working shop view at 60–80 m has a planning estimate of **+2–4 ms whole-frame cost over vanilla interior**, not a measurement or F0 renderer result. Three-shop matched rig windows must establish the actual cost.

The [review r1 evidence](../checks/f0-interior-view.md#f0-review-r1-fixes---31-october-2026-offline) records the corrected accessor benchmark and the **OPEN final-head native gate**. The numbered **In-game checks for 6 Nov rig** remain in [CHECKS](../checks/f0-interior-view.md#track-f-f0-interior-view-spike---31-october-2026-offline-only); the [prepared PR description](../reviews/2026-10-31-f0-pr-description.md) repeats the same list for the harness. Nothing here authorizes a Workshop release, ordinary-save deployment or bypass of final-head `rig_test` proof.
