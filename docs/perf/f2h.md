# City audit F2h: focus-independent native inputs: performance

8 November 2026, offline. Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Measurement named in the [F2 plan](../plans/f2-movement.md#6-performance): discovery and `RefreshOptionalStops` cost before/after, and cache size. Expected: neutral or lower.

## Scope and limits

- Harness: `FocusIndependentInputsTests.OfflineCostOfCachedSceneAnswers` (Debug test build, .NET 10, one run on a PC shared with other worktrees). It times the production Unity-free classes `PerformerSites` and `StaticSceneMemory`, and Core `RefreshOptionalStops` against the scene double.
- The Unity adapter (`FindObjectsOfType`, transform reads, `StaticSceneKey`) cannot run offline. Those costs are described below, not measured.
- "Before" for `RefreshOptionalStops` uses the same fixture (48 residents, 6 performer sites, 40 active audience stops), the same 2,000-call sampling and the same scene double for spots. Its `Available` is `LivePerformerModel`: per audience stop, the two dictionary lookups, a reflection read of a private `_streetPerformer` field, an activity flag, a reference compare and the facing distance, the CPU shape of the removed check. It excludes Unity's native property costs (`activeInHierarchy`, `transform`, `transform.position`), which the base paid on every call and this build no longer does. The after arm runs with the scene deactivated; both arms keep all 40 stops, so they do the same Core work.
- "Before" for audience discovery is a model of the base shape: every uncached audience `Spots` call scanned every registered spawner with a distance test (and a reflection read per spawner, not modelled). It is not a measurement of the base build.
- These are component timings, not in-game mod-work.

## Numbers

| Item | Before | After |
| --- | --- | --- |
| Audience discovery per place query, 200 spawners, 64 places | 21.1 µs (linear scan model) | 0.57 µs (cached per place) |
| Catalogue rebuild, 200 spawners (only on a scene-set change) | none (per query instead) | 0.087 ms |
| `Available` audience target check, 1,000 saved stops | live reflection + transform reads per stop (not measurable offline) | 0.22 ms total |
| `StaticSceneMemory.Admit`, 1,000 validated keys | `activeInHierarchy` per read | 0.043 ms total |
| `RefreshOptionalStops`, 40 active audience stops (2,000 calls) | p50 0.0113 / p95 0.0184 / max 0.2138 ms (live performer model) | p50 0.0104 / p95 0.0115 / max 0.0901 ms (scene deactivated) |

Numbers from the second run (after the review fixes); the first run gave the same order (discovery 21.4 → 0.50 µs, after-arm refresh p95 0.0115 ms). No sample ≥ 30 ms.

## Cache size

- `PerformerSites`: one entry per catalogued spawner (200 in the harness; filtered by game reasons only, no activity memory) plus one site list per place queried for audience spots (64). Rebuilt only when the loaded scene set changes.
- `StaticSceneMemory`: one string key per validated frontage, park volume and station. Bounded by the scene's object count; nothing is added per query. Exposed in game as `OptionalStopEnvironment.ValidatedSceneObjects`, `PerformerSiteCount`, `PerformerSitePlaces` and `MultimodalRoutes.ValidatedStations`.

## Unmeasured native costs

- Performer catalogue: one `FindObjectsOfType<StationaryAiSpawner>(true)` plus a static scene key per spawner, at environment first use and after each scene load or unload. It replaces a reflection read and a distance test per spawner on every uncached audience query.
- Stations: `EnsureStations` now builds a static scene key per station on each preparation (every 30 s and after gate changes). That is a hierarchy walk and string build for a few dozen stations, next to the existing `GetComponentsInParent` DLC check.
- **City-wide audience spot sampling.** Before F2h, audience spots were sampled only near the player while a performer was live. Now the first `Spots(Audience)` query for each place within 8 m of any performer-capable spawner samples up to 8 ring points per site (`NavMesh.SamplePosition`, `NavMesh.CalculatePath`, `NavMesh.FindClosestEdge` each), inside an advancement unit, wherever the player is. The result is cached per place (`AudienceGeometryCache`), so each place pays once per session, but several first-time arrivals in one frame early in a session could add up. Not measurable offline. The batched rig run (plan check 10) should watch mod-work samples of 30 ms or more in the first game hour after load and attribute any to the optional-stop seam (`SeamMilliseconds`, `MaximumSeamMilliseconds` in the 60 s summary).
- `RefreshOptionalStops` per audience stop: a scene-set comparison (a few `GetSceneAt` calls, no allocation) and a dictionary lookup, instead of the reflection `GetValue` and transform reads.

## Gate reading

Offline only. F2h adds no per-frame scan or allocation; audience reads get cheaper and other reads add one hash lookup. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from the batched rig run (plan check 10); whole-frame figures stay separate from mod-work.
