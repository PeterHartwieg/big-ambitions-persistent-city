Status: implemented (offline); not verified by rig

# City audit F2h: focus-independent native inputs, 8 November 2026

Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Branch `codex/pc-f2h-focus-independent-inputs` from `origin/codex/persistent-city` at `ec186f9` (contains F2f `d8a963b`, C5f `6e428c9`, F6j `ec186f9`; writer **33**). Scope: the F2h row and Decision 7 of [the F2 plan](../plans/f2-movement.md#decision-7-focus-independent-native-inputs-f2h), with Peter's accepted section 9 defaults ([6 Nov decisions](../decisions/2026-11-06-design-defaults.md), Q4: people still stop at a street performer's spot when no performer is out). No saved state, no schema bump, no migration; no Persistence file is touched, so the pinned current-writer golden hash is unchanged.

Re-read at this base: `Runtime/OptionalStopEnvironment.cs` (`NativeDoorPresent`, `ParkEdge`, `ParkTriggers`, `Audience`, `ReadAvailable`), `Runtime/MultimodalRoutes.cs` (`EnsureStations`, `RefreshStations`, `ReuseRoadSceneForRetiming`), `Runtime/OutdoorPlaces.cs` (`Read`), `Runtime/RouteObjectDiscovery.cs` (`Included`), `Runtime/PersistentCityMod.cs` (station refresh every 30 s, `RefreshOptionalStops` every frame, route retiming), `Core/OptionalStopScheduling.cs` (`RefreshOptionalStops`, `TryStop`) and the optional-stop branch of `Runtime/PhysicalPopulation.cs` (`ObserveOptional`).

## Inventory: does the native object deactivate or spawn by distance to the player?

Read-only inspection of `BigAmbitions.dll` from the installed game (ILSpy decompile into a temp folder outside the repository; nothing decompiled is committed). Types read: `StationaryAiSpawner`, `AiSpawnerZone`, `StreetPerformer`, `BoundsPositionGiver`, `Culling.CullingManager`, `Culling.CullingGroupController`, `LocationHappinessTrigger`, `SubwayStation`, `ToggleObjectDLC`, `CityBuildingController`, `HamptonsHouse`, `PedestrianSpawner`, `LoadScene`/`SceneLoader`, `BuildingManager.ToggleLayout`. The whole assembly was searched for `ICullable`, `CullingGroup`, scene loads and `SetActive` near distance tests.

| Read | Native object | Depends on player distance? | Evidence |
| --- | --- | --- | --- |
| Shop frontage availability | `CityBuildingController.gameObject.activeInHierarchy` | No (by inspection) | Not an `ICullable`. Its only LOD switch (`ToggleLodMode`) toggles renderers for the city map, not the object. `HamptonsHouse` loads interior items by distance, not the controller. City scenes load all at once (`LoadScene.LoadScenes(GameScenesHelper.GetAllCityScenes())`). |
| Street-performer discovery | `StationaryAiSpawner._streetPerformer` | **Yes** | The spawner registers with `CullingManager.generalCullingGroupController` (bands 60 m / 200 m, distance reference `playerController.transform`). `OnLod0` (inside 60 m) runs `SpawnGroupOverFrames`, which spawns a performer from a pool with `streetPerformerSpawnChance` at a random point in `streetPerformerBoundsPositionGiver`; `OnLod1`/`OnLod2` call `ClearPedestrians`, which releases it. Presence, identity and position of the performer all follow the player. |
| Park target availability | `LocationHappinessTrigger` | No (by inspection) | Plain `MonoBehaviour` with player trigger callbacks; never culled or toggled by distance. |
| Performer target availability | live `StreetPerformer` transform and position | **Yes** | As above: the saved stop was cancelled when the pooled performer was released, re-spawned elsewhere or never spawned. |
| Subway station discovery | `SubwayStation.gameObject.activeInHierarchy` | No (by inspection) | Not an `ICullable`; no distance toggle found. Only DLC visibility (`ToggleObjectDLC.OnEnable` sets the object inactive when the DLC state hides it). |
| Outdoor scene triggers | `LocationHappinessTrigger` at catalogue build | No (by inspection) | Same type as parks. `OutdoorPlaces.Read` runs once per city load, so a later deactivation could never remove a place. |

Limit of the inspection: only `BigAmbitions.dll` was read. A component from another assembly or a scene-authored toggle could still deactivate one of these objects near the player. Rig check 11 collects the native proof.

## What is implemented

- **Performer sites from static scene data.** `OptionalStopEnvironment` no longer reads `StationaryAiSpawner.AllPedestrianSpawners` or the private `_streetPerformer`. `EnsurePerformers` reads every `StationaryAiSpawner` with `FindObjectsOfType(true)` (inactive included) when the loaded scene set changes, the same rescan rule parks already use. Each spawner gives one site keyed by its static scene key (`performer:<key>`, unchanged id scheme), centred on its performer area: `streetPerformerBoundsPositionGiver` position plus its rotated horizontal bounds centre, which is the expected point of the native uniform spawn. `PerformerSites` (new, Unity-free) keeps the catalogue sorted by id and caches each place's sites within 8 m.
- **Audience stops planned and kept without a live performer.** Spots ring the static centre and face it. `Available` keeps an audience stop while its site is catalogued and the stop's `Facing` is within 0.5 m of the site centre. No native performer is consulted.
- **Q4 in the body.** The visible audience member already stops at the spot and calls `body.Face(stop.Facing)` with no performer check (`ObserveOptional`, unchanged). `Facing` is now the static centre, so the body waits facing the spot when no performer is out.
- **Activity counts once.** `StaticSceneMemory` (new, Unity-free) admits an object once it has been seen active and not excluded by the game. After that, a deactivation no longer withdraws it. It is used for:
  - frontages (`NativeDoorPresent`), keyed by place id and forgotten when the frontage is rebound to a different controller;
  - park volumes (`ParkEdge` and `Available`), keyed by `scene-park:<static scene key>`;
  - performer sites (spawner `isActiveAndEnabled` at a rescan);
  - stations (`EnsureStations`), keyed by the station's static scene key. A station with no static key keeps the live rule. `ReuseRoadSceneForRetiming` hands the memory to retimed routes built from the same discovery owner.
- **Game reasons keep today's rule.** Building access (`GamePlaces.BuildingAccessible`), established business, `temporarilyClosed` and the nightclub rule are read on every frontage query, as before. Station DLC visibility is read on every preparation, as before. Performer sites are excluded when the spawner cannot spawn performers (`canSpawnStreetPerformers`, no area, chance 0, `disabled`) or sits under a DLC toggle that hides it (`OutdoorPlaces.DlcHidden`, now shared with `OutdoorPlaces.Read`). An object never seen active stays excluded, as today.
- **Logical cancellation only.** With deactivation out of the availability answers, `RefreshOptionalStops` cancels for rain, closing, phase or activity end, a meal outing, a lost partner, a destroyed park volume, a performer site gone from the scene or hidden by the game, or a spot whose validated geometry no longer matches.

### Technical choices

- Activity at first discovery still counts. Inspection found no distance deactivation for the objects read at first discovery (spawners, triggers, stations, building controllers), so an object never seen active is treated as disabled for a game reason. Including never-active objects would add stations or spots the game itself does not use. "Inactive objects are included" therefore means the searches include them (`FindObjectsOfType(true)`, `FindObjectsInactive.Include`, `RouteObjectDiscovery`), so a validated object that later goes inactive is still found and kept.
- The memory covers frontages, parks and stations too, although inspection found no distance dependency for them. The rig proof is pending and the inspection covered one assembly. The change is one admitted-key lookup per read.
- An inactive park volume has empty collider bounds. A validated volume that is inactive when its spots are first sampled yields no spots for now and is retried later. Spots already sampled stay cached, so saved park stops are unaffected.
- Performer exclusion from game settings and DLC is read at each scene-set rescan, not on every query. DLC ownership does not change during a session.
- Audience stops saved by an earlier build face the live performer's random point. Their spot positions differ from the new static ring, so `GeometryStatus` reports them unavailable once and they are cancelled at the first refresh after the update. They are 2–8 minute presentation stops in development cities; no migration (delivery rules).
- `PopulationCensusDiagnostics.DiscoveredStationIds` still applies the live activity filter. It is a census count, not a Core input, and lies outside this slice's files.
- `PersistentCityMod.cs` is unchanged; the existing 30 s `RefreshStations` call keeps working with the memory.

## Offline checks

Build: `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"` passed. `dotnet test tests/PersistentCity.Tests.csproj`: 3,982 passed, 0 failed. The 21 new tests pass; with `StaticSceneMemory.Admit` temporarily reverted to the live activity rule, 15 of them fail (red before, green after).

`tests/FocusIndependentInputsTests.cs` (scene double over the production `StaticSceneMemory` and `PerformerSites`; Core `CitySimulator` optional stops):

| Test | Asserts |
| --- | --- |
| `EnvironmentAnswersAreIdenticalBeforeAndAfterObjectsAreDeactivated` (5 kinds) | `Spots` and `Available` are identical before deactivation, after deactivation plus a scene rescan, and after reactivation |
| `DeactivationDoesNotCancelSavedStopsAndTheSaveMatchesAnUntouchedArm` (5 kinds) | an arm whose scene is deactivated while stops are planned and active saves the same bytes as an untouched arm, through the stop and past it; no stop cancelled |
| `RainStillCancelsAfterDeactivation` (5 kinds) | deactivation cancels nothing; rain then cancels every stop |
| `ClosingAndPartnerLossStillCancelAfterDeactivation` | closure cancels a break; a removed partner and a logically cancelled partner cancel the conversation |
| `DlcGatedInaccessibleClosedAndNeverActiveObjectsStayExcluded` | DLC-hidden performer area, inaccessible frontage and never-active spawner plan nothing; a validated frontage that closes, a performer area hidden after validation and a site gone from the scene cancel the saved stop |
| `StationSetIsUnchangedByDeactivationAndKeepsDlcAndNeverActiveExclusions` | EnsureStations' filter: set unchanged by deactivation and after retiming hand-over; DLC-hidden and never-active stations excluded; DLC hiding a validated station removes it |
| `AudienceStopsUseTheStaticPerformerAreaAndNeedNoLivePerformer` | sites within 8 m, cached per place; availability needs only the site and a facing within 0.5 m; inactive rescan keeps the site |
| `NativeReadsRouteActivityThroughStaticMemoryAndReadNoLivePerformer` | source: no `StreetPerformer`, `AllPedestrianSpawners` or `_streetPerformer` in the environment; every `activeInHierarchy` there feeds `Activity.Admit`; `EnsureStations` admits through `stationActivity`; the body faces `stop.Facing` and reads no performer |
| `OfflineCostOfCachedSceneAnswers` | the [perf file](../perf/f2h.md) numbers |

The scene double exercises the production memory and site classes, not the Unity adapter in `OptionalStopEnvironment`. The adapter's reads are covered by the source test and rig check 11.

## In-game checks for the next batched rig

Fixture: a disposable FT city on the owned rig with Persistent City and RLO, autosave off and verified before every window (also after reload). Offline results above are not native evidence. These checks are batched for the next rig run and do not gate merge.

1. **Native objects near the player do not change stops or routes (plan section 7, check 11).** Setup: checkpoint at 10:00 with a park, a street-performer spot and a subway station in the fixture district. Steps: arm *stationary* keeps the player 1 km away; arm *sweep/away-return* teleports the player to the performer spot, the park and the station for 15 game minutes each, then away; both run to 18:00. Read `pc population census` and the optional-stop counters at the end. Expected: hold stream empty; equal `journeys` and `meals_campaigns` projections, including saved optional stops and their cancellation flags; same planned and cancelled audience and park stop counts; same station set. Red before F2h is allowed and recorded with the reads that changed. **rig-automated (pending capability: RIG-ARMS-STEPS; existing: FT `teleport`/`console`, RIG-PCSAVE).**
