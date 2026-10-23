# Rentable interior population policy (0.3.1)

The current private background prototype uses residential interiors the player can rent or buy. This replaces the superseded [whole-property experiment](2026-10-19-housing.md), whose 25,495 residents and severe accelerated-time backlog are historical evidence. Neither that experiment nor the earlier 400-resident runs validate the current fresh-city population.

## Build and review status

- Package version 0.3.1; schema 4; storage key remains `persistentcity:state_v1`.
- Final `PersistentCity.dll` SHA-256: `b1365130ae546ae9ef5cc29d527704bf6696422d56a00d4b744c9cc9c75db686`.
- 223 offline tests pass. The native package compiles with warnings treated as errors.
- Astra, GPT 6.1 Sol, Grok 4.7 (resolved `grok-4.7-build`) and Claude Opus 5.5 approve the current delta after fix rounds. Opus requested explicit documentation and a regression test for permanent preservation of oversized experimental populations; both were added and re-reviewed. Reviewers inspected source/tests, not the native playthrough.

## Provisional housing policy

`rentable-interior-occupant-bands-v1` reads positive native `BuildingSizeData.squareMeters` for each eligible residential interior. `Building.totalSqm` is whole-property area and is never a fallback. Missing or undersized interiors are omitted; invalid/nonfinite areas refuse the pure capacity calculation. The adapter does not create native registrations to obtain housing data.

| Rentable/buyable interior | Maximum occupants |
| --- | ---: |
| Below 20 m² | 0 |
| 20 to below 40 m² | 1 |
| 40 to below 75 m² | 2 |
| 75 to below 150 m² | 4 |
| 150 to below 300 m² | 5 |
| 300 m² or larger | 7 |

These limits are provisional allocations for residents, with room for a small family and live-in staff in larger homes. The simulation implements neither family relationships nor staff roles. It does not infer additional apartments from a property's size. Even an extremely large villa remains limited to seven occupants; population scales with eligible residential units captured at initialization or migration rather than unbounded area.

The reference inventory contains 335 known accessible interiors:

| Interior size | Units | Maximum occupants per unit | Total capacity |
| --- | ---: | ---: | ---: |
| 54 m² | 182 | 2 | 364 |
| 96 m² | 105 | 4 | 420 |
| 204 m² | 28 | 5 | 140 |
| 589 m² | 20 | 7 | 140 |
| Total | 335 | | 1,064 |

Fresh initialization uses `floor(1,064 × 0.9) = 957` residents. The 90% factor applies once to aggregate occupant capacity. It does not fill 90% of units, assign family groups or prescribe which homes remain vacant. The 50,000-resident startup refusal remains a resource bound, not a performance claim.

Existing access filters require an accessible entrance, the appropriate owned DLC and permission for pedestrian spawning. Gated private estates remain excluded regardless of player ownership. No Hamptons residences passed the current access/entrance filters in the reference catalogue. Fixed AI villas and special-rival homes also receive pure catalogue eligibility checks; these do not call native registration, sale, rent or economy mutation methods. The current native state confirms 335 homes and 37,400 m² of interior area.

## Identity, migration and persistence

RLO 0.40.4 remains required. Its 400 original profiles are an immutable donor catalogue, independent of the active roster. Fresh initialization at the reference target would retain those 400 original identities and add 557 persistent mod-owned identities. No RLO profile is edited or removed.

Strict schema 1/2/3 validation precedes migration to schema 4. Housing enrichment preserves existing identities, assignments, cars, routes and physical event times. A home retains enough allocation for its saved occupants even when the corrected interior policy would allow fewer. An undiscovered legacy home retains only its current occupants. Existing populations are never evicted to meet a new target: a save from the old 25,495-person experiment remains that large, and can exceed both nominal per-home limits and the 957-resident fresh-city target. Departures introduce immediate replacements, so preservation is permanent under the current policy: an overcapacity population does not naturally drain through turnover. This deliberately protects continuity; downsizing is unimplemented. Use a fresh disposable city to test the 957-resident default. This is continuity protection, not evidence that the old population fits the corrected housing model.

The genuine 400-resident checkpoint has 19 home allocations that must remain above the corrected bands. Preserving them gives a derived capacity of 1,089 and a migration target of 980, rather than the fresh-city 957. The current native migration confirms these counts and preserves all 400 original identities, their home/day/shop/leisure assignments and the complete source catalogue. The two population scenarios must not be conflated.

The slower job policy remains unchanged: established workers receive a one-day minimum plus an exponential mean wait of 900 days; successful changes and replacement arrivals receive a 90-day minimum plus that wait. Schema 2 future dates are stretched once; later schema migrations do not retune them again.

Offline coverage includes band boundaries, invalid areas, bounded villas, non-linear density, the reference 1,064/957 calculation, immutable donor identities, capacity-respecting allocation, retained legacy occupants, permanent overcapacity preservation through a forced departure/replacement, and strict schema migration/refusal. These checks establish policy and persistence invariants without claiming native usability or throughput.

## Native validation

The exact DLL above was installed into the isolated headless Mac rig: Big Ambitions build 3682, bridge runtime 6, RLO 0.40.4. Neither the normal Mac profile nor the Windows PC was used. This run tests the migrated **980-person** checkpoint, not a freshly initialized 957-person city.

Strict schema 2 migration preserved all 400 original identities, home/day/shop/leisure assignments and source profiles, then added 580 persistent generated residents. The native state contains 335 homes, 37,400 m² of interior area and 1,089 grandfathered capacity, matching the predicted 980 target. No Hamptons residence is in that eligible inventory.

A full day from day 1 08:03 to day 2 08:03 at bridge speed 6 completed in about 243 seconds of observation. The simulation had caught up at the first paused sample. Five-second samples recorded a peak backlog of 38.88 game minutes and a maximum advancement call of 41.44 ms. The soft per-frame budget does not preempt an individual native route event; these are advancement-call elapsed times, not total frame, CPU or rendering measurements. Unsampled peaks may be higher.

The day ended with 3,026 completed journeys, 276 allocated logical cars and admitted home/work/study/shop/leisure visits. Walking, subway and car legs occurred. There were 625 missed activities and 50 residents without a day-place assignment; all 50 are students beyond the current 16-place study allocation. No job changes or departures occurred during this short run. The absence of runtime failure does not establish convincing schedules, rendering performance or long-term stability.

The native checkpoint saved successfully, with its stored own payload exactly matching the captured canonical state: 1,999,272 JSON characters; the compressed game file is 990,403 bytes. Independent core replay of the native reload clock advance (0.333333 game minute) matched the entire canonical state exactly without requesting a new route. All 276 logical cars remained, twelve journeys continued and one arrived. This validates persisted-route continuity for this checkpoint, not arbitrary future native route determinism.

The rig was stopped and its original installation manifest and hashes restored; ownership was released. All preexisting named checkpoints were unchanged. Of 95 preexisting save-directory files, four changed: two automatic recovery saves (`Recover #0` and `Recover Midnight`) and their metadata for this same disposable test character. Those native recovery updates were recorded, not overwritten to manufacture a clean comparison. No normal-game or Windows files were touched.

Evidence is local and ignored under `build/interior-validation/`: migration-evidence.json, interior6x-timeline.json, native-day-result.json, replay.json, cleanup.json and the saved-state snapshots. Capacity evidence comes from `state.Places` and `summary.housing`. The diagnostic helper's nested `nativeInventory.housing` policy formulas still describe the historical whole-property experiment and are not used for current allocation evidence.

## Remaining limits

Work/study allocations remain provisional at sixteen per business role. Destination assignment does not prove reachability or opening-hour overlap. Frozen venue data, non-preemptible native route work and sharply clustered activity schedules remain performance and behavior concerns; the smaller population requires its own measurements.

This build creates no purchases, wages, hiring, inventory changes, rent or other economic transactions. Future housing/economy integration needs separate design and populated-save non-interference checks. Visible citizens, physical traffic, household relationships, staff roles, Windows performance and long native soaks remain unimplemented or unverified. The package remains disabled by default.
