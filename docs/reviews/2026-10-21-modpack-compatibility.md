# Persistent City: modpack compatibility, 21 October 2026

> Installation follow-up: after this test report, Peter requested and received [installation of the exact reviewed build in both PC profiles](2026-10-21-modpack-pc-install.md). The original test-scope statements below describe the state before that installation.

## Recording decision

The reviewed performance build now accepts the exact released RLO 0.45.0 version pair. The frozen candidate passes 886 offline tests, released-binary contract comparisons, four-model source review, and a bounded native Mac full-modpack load/simulation/save/restart smoke. No blocking Persistent City hook-composition or identity-corruption defect was demonstrated. A subsequent Driven Rides food-truck journey ended through its deadline watchdog with one contact, so clean driving compatibility remains unresolved.

**This is a partial compatibility pass, not an all-mod Windows recording clearance.** All 18 selected mod assemblies loaded, but Bakery's business assets and Hyperreal's optional rendering bundle are Windows-only in the installed packages. City Performance and several Hard Mode Patch features deliberately withdraw on this private Mac game binary. Normal-speed simulation accumulated recoverable backlog (up to 58.39 game minutes after the joint ride), so sustained recording performance remains unverified. There was no native before/after timing comparison.

Peter directed testing to the local Mac rig. The normal PC and the occupied Windows test rig were not changed or restarted. Their earlier installation does not contain this combined candidate: the normal PC inventory still showed `00e7d761…` (gate-index build plus RLO 0.45 acceptance). The new combined build contains the reviewed gate index, bounded car search and closure refresh. It is installed only in the private Mac test profile and packaged separately for later installation.

## Exact candidate and review boundary

- Persistent City 0.6.2 development DLL: `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`.
- RLO 0.45.0 DLL: `2f360a365cf7915f012cd62f4fd06214ef5cdaa0498331e05a7c831a3c20ab32`; informational version `0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`.
- Final source: `build/modpack-compatibility-20261021/source-r2`; 161-file manifest SHA-256 `8e55abccbccaf7e7b9b7039d8835569258d7de73a9319ed2b7257d139f6dd45f`.
- Against the previously reviewed closure-refresh freeze, only production file `Compatibility/RloIdentityAdapter.cs` changes: one reviewed assembly/informational-version pair is added. `tests/RloCompatibilityTests.cs` adds three checks. All 49 production files were compared. Identity generation, population-key format, save schema, RLO read-only behavior and the reviewed performance paths stay the same.
- Builds against the private Mac and retained Windows game references are byte-identical. Windows-reference compilation is not Windows execution.

Concurrent CityApi work appeared in the shared checkout while an initial candidate was being prepared. Astra and Opus rejected its provenance and review scope. That candidate (`c97c15cb…`) and its initial native observations are **discarded evidence for release approval** and retained separately under `native-r1`. The final r2 freeze excludes that concurrent work without overwriting it. The final tests, contract probe and native captures all identify r2. Do not rebuild this reviewed package from the moving checkout and assume equivalence.

Astra, GPT-6.1 Sol, Claude Opus 5.5 and Grok 4.7 approve the final r2 source delta. The Grok backend reported `grok-4.7-build`. Astra separately reviewed native identity/reload invariants; Sol reviewed the actual captured Harmony overlaps and exact installed City Performance/Driven Rides binaries. Their approvals are scoped to those artifacts and do not establish every feature of the modpack.

## Contract and offline validation

`final-r2-tests.log` / `test-results/final-r2.trx`: **886 passed, 0 failed, 0 skipped** on frozen r2 source.

`contract-r2/Probe.csproj` references frozen r2 Compatibility, not the live checkout. Actual released RLO 0.45.0 and 0.41.1 DLL probes establish:

- All 66 required metadata-contract rows pass and match.
- Audited pure-generator and null-instance paths remain safe; their relevant IL hashes match.
- All 4,800 synthetic donor/avatar rows and 12 population keys match, as do city-seed precedence cases. Pure-output SHA-256 is `9b0002e796f2175fd3224e7878189a721fcab38b7acede610beee6a81a5aaf4a`.
- Identity binding is correctly NotReady with no live Flare director, while empty reservation and speed-override bindings work. Static directors remain null.
- The actual unreviewed RLO 0.40.6 DLL is still refused by all three adapters. Mixed version/commit pairs are rejected by the tests.

All 17 checks in `contract-r2-summary.json` pass, including manifest integrity and Mac/Windows build equality. Earlier performance regression harnesses remain applicable because their production paths are byte-for-byte unchanged from the reviewed closure source; this session does not claim to have rerun those larger harnesses.

## Mac configuration and native observations

The isolated ARM64 rig ran with Metal, muted audio, 30 FPS cap and two workers on port 28330. It used its own profile and newly created `FT PCity Modpack R2 20261021` saves. The existing profile and preferences were backed up first (2,279 files, including 867 pre-existing save files).

The rig was updated to released RLO 0.45.0 and the exact PC Hard Mode Patch/City Performance/FoodTrucks Core/data versions. **93 active PC mod-option values** were mapped to Mac local-mod IDs; stale options belonging to disabled PC local copies were excluded. Driven Rides used the active Workshop settings (clock 25%, startup speed index 2), not stale local settings (33%/0). RLO camera controls remained off; Social Media, the donor dependency, was explicitly on. Persistent City used Enabled/PhysicalEnabled true, 48 pedestrian slots, 24 car slots and a 120 m player radius.

FoodTrucks required the standard private-rig overlay to bind its bridge to 28330 and participate in ownership/runtime control. Its test DLL is `8c738a81542e52cb2047e714b28eab1dc2d5b9410e2fb4f5bd924b9e23c2df68`, derived from PC DLL `1cc19025…`. Mac BridgeControl was retained; all 1,508 relevant API references and eight bridge calls resolved in the independent compatibility check. This instrumentation is a test exception to exact PC byte parity. A temporary PersistentCityProbe collected state and patch evidence; the release package does not contain it.

Final r2 native results:

- Actual loaded DLL hashes match r2 and RLO 0.45. Both identity reads report Ready with 400 matching donor identities; reservation and speed adapters are Ready. No positive incident reservation or pothole was present, so those active interactions were not exercised.
- The population contains 957 unique residents and 1,009 places. Across 27 normal/fast/returned/save snapshots, all 400 donor identities remain unchanged; no duplicate citizen/body binding or invalid identity mapping was found. Repeated appearance fingerprints match across 75 observed physical identities.
- At normal speed the test reached 48 physical pedestrians and two cars. All 40 people shared between normal-run endpoint samples moved more than 0.1 m; all 12 shared people in the returned-speed samples moved. This is bounded movement evidence, not proof every route or animation succeeds.
- Native speed 6 withdraws all physical bindings, with acquired/released balanced at 68/68; native speed 1 reacquires bodies. No traffic overshoot, foreign ownership relinquishment or unexpected pool return was observed. Acquisition failures were nonzero and reached 100 in these inspected snapshots; zero-failure availability is not claimed.
- Normal-speed backlog reached 31.60 game minutes and returned-speed backlog 17.04 minutes; both drained to zero when paused. This is a material capacity limitation. Large diagnostic captures and helper instrumentation also affect timing. Neither a performance gain nor a regression from the optimizations is established by this run.
- At both checkpoints the serialized own-data hash equals the in-memory state hash. After a complete process restart, the stored payload hash, all resident/donor identities, population key, seed and sequencing fields match; all 14 shared appearances persist. A 0.333334-minute load tick advances the simulation, so full evolved-state equality is not claimed.
- FoodTrucks cart creation, equipment/stock loading, licence/permit, opening/closing, and Rivals phone opening/closing ran while Persistent City was active. The short cart window had zero sales and no queued customers; it does not validate customer or crew coexistence. The Driven Rides link and truck prefab check succeeded; its historical `last_ride` field was not treated as evidence of this run.
- An offscreen Metal city image was inspected: the city, road vehicles and pedestrians rendered. An earlier low cart camera was obstructed by a vehicle and is not a useful visual pass. No free-camera or city-map return sequence was exercised.

## Per-mod scope

The actual loaded hashes, options and 179 patched targets are in `native/active-modpack.json`. The following table describes evidence, not blanket feature approval.

| Selected mod | DLL hash prefix | What this session establishes / remaining limit |
| --- | --- | --- |
| Real Life Overhaul | `2f360a365cf7915f` | Released 0.45 contract, live donors/reservations/speed binding and restart verified. Positive incidents, potholes, buses/taxi interactions and the broader economy/life feature set remain untested. |
| Dynamic Rivals | `2ed7d5a5a37b8c5f` | Loaded; joint Rivals screen populated. No long rival season or economic balance test. |
| HART Rival Balance | `af8abbf08b4ea0a2` | Loaded with copied options; no new Persistent City patch overlap. Balance progression untested. |
| Alcware Seasons | `d56a88d2c5aff831` | Loaded with copied options. Seasonal/weather transitions not exercised. |
| Retail Capacity Upgrades | `29a06ba1b520204e` | Loaded; no shared Persistent City production target in capture. Shop upgrade operations untested. |
| Internet Cafe | `4fc4ea9bdc455d86` | Loaded; no shared Persistent City production target. Business operation untested. |
| Bakery | `6d68edab0f5600f5` | DLL loaded but Windows-only bundle fails on Mac; business type/icons unavailable. FoodTrucks reports Bakery absent. **Not functionally tested.** |
| PolyMods Hyperreal | `7d1bd589947558e2` | Base lighting operates; missing Windows-only bundle leaves SSR, SSGI, volumetrics and clouds disabled. HMP map/discovery fixes withdraw. **Partial Mac coverage.** |
| Camera Tools | `e8125b371512d802` | Loaded with copied options. Interactive camera controls untested. |
| HART Free Camera | `ec32dc23b66a4293` | Loaded. Persistent City follows the player within 120 m; distant camera flights lack population coverage. Clock-only pause and filming controls untested here. |
| Campaign Objectives | `ad5f10cce784e879` | Loaded; no shared Persistent City production target. Objective completion/panel journey untested. |
| Big Copilot Link | `4e06a0700fa4674d` | Loaded. External client workflow untested; PC inventory had the same DLL in local/active Workshop locations, while Mac loads one local copy. |
| Food Trucks | PC `1cc190256f07e8bd`; Mac overlay `8c738a81542e52cb` | Cart smoke, prefab/link check and shared tick composition inspected. No customer sales or hired-crew journey established. |
| Hard Mode Patch | `61db7655b48af07e` | RLO 0.45 pin accepted, factory diploma IDs 18/19/34 registered and owner-job hook applied. Bakery discovery and Hyperreal fixes retain originals on this Mac. |
| City Performance | `05ef9bd0f42512b1` | Diagnostics coexist; refill, midnight, autosave, trees and cinematic optimizations withdraw on unverified Mac binary. Exact 0.4.2 binary additionally disables cinematic traffic for foreign FixedUpdate patches, including PC/Driven Rides. Windows-active implementations untested. |
| Persistent City | `e0756b4a74764c4b` | Evidence above. Normal-speed backlog and restricted camera coverage remain. |
| Roadster | `df349aa92d1cb1eb` | Loaded; no shared Persistent City production target. Vehicle purchase/driving not exercised. |
| Driven Rides | `f1e4a17c768cd300` | Actual loaded handlers and link/prefab checked. Shared driving hooks are lease-scoped; this binary lacks newer checkout parking hooks. See joint-ride addendum for this run. |

Sol found five shared production targets: RunMainGameTick; DrivingAI.HasReachedOrPassedWaypoint; DrivingAI.WaypointRequested; TrafficManager.FixedUpdate; PedestrianSpawner.SetActivePedestrians. Persistent City is the only recorded physics transpiler. Other driving handlers are scoped to their own rides; diagnostic hooks do not introduce another population policy. No composition blocker was found. Same-priority list order was not assumed to be execution order.

RLO's native Pedestrian-based incidents/horn discovery do not automatically include Persistent City's replacement low-detail character bodies. This pre-existing coverage difference remains; read-only reservations do not make every RLO NPC feature apply to the new crowd. RLO's own transit diagnostics also reported route/clearance limitations; this session did not isolate or fix them.

## Evidence and handoff

Local evidence root: `build/modpack-compatibility-20261021/`. Raw saves, game DLLs, decompilations, downloaded mod packages and detailed private runtime captures stay in ignored build storage, outside Git and the distributable.

Key records: `source-r2-manifest.json`, `r2-production-scope.json`, `final-r2-tests.log`, `contract-r2-summary.json`, `astra-r2-review.md`, `sol-r2-review.md`, `round-2/{opus,grok}/review.md`, `astra-native-r2.md`, `sol-native-patches-r2.md`, `sol-cityperformance042-binary.md`, `mapped-options.json` and final installation/cleanup receipt. Distribution configuration remains disabled by default; preserve the existing user's enabled configuration on any later authorized installation.

## Joint ride addendum

A real FoodTrucks own-truck ride used the loaded Driven Rides **0.2.0-rc7** binary, speed 2 and its 25% ride-clock setting. The target was selected from the current map data, about 128 m from the starting lane. The driver moved the truck to 7.9 m from its selected target, but ended after 106.5 seconds with `Vanilla: watchdog: Ride deadline exceeded`, one contact (one caused), zero red-light runs and zero stalls. FoodTrucks then parked the truck legally at the kerb 8.9 m from the requested goal. Its wrapper reported done; **the underlying ride is not a clean pass**. The driver returned controls and left no ride active. This is the current run, not the older `last_ride` record returned by the initial link check.

Sol inspected the exact loaded driver's deadline code and the live evidence. Overall deadline expiry precedes no-progress handling, and waiting does not extend that deadline. The log records red-light waiting/yielding and an RLO pedestrian injury/ambulance before the timeout. During the sampled ride the game's time scale was 2, and Persistent City had zero physical bindings and no physical failure. Its clock scale changed to 0.25 and back to 1.0 with two timing recalculations. A maximum measured retiming call of about 1,630 ms could add wall-clock load, but the evidence does not establish that it caused the contact or timeout. There is no matched Persistent-City-off baseline, so neither a PC regression nor a pre-existing driver defect is asserted.

After the ride, Persistent City caught up from a 58.39-minute backlog to zero and reacquired 22 pedestrians. The separate Joint checkpoint was saved. These observations support recovery and control cleanup; they do not turn the driving outcome into approval for recording. Resolve the joint-driving uncertainty on the intended Windows setup before relying on automated rides in the session.

## Final rig state

The native game was stopped gracefully. The temporary probe was removed, all **867 pre-existing save files match their verified backup**, and the 263 managed installation-file hashes validate. The configured r2/RLO 0.45 pack remains in the private Mac profile with the original 48/24/120 settings; the normal Mac and both Windows profiles were not modified by this work. The FT-only test checkpoints and ignored evidence are retained.

Final preference verification caught macOS `defaults import` merging rather than deleting absent keys. The 93 explicitly copied active PC values had been applied, but extra old Mac keys remained: three Driven Rides defaults (autopark=2, lights=1, nolimits=0) and two disabled Meaningful Ambience options. These were removed along with the run-generated LastPlayedVersion key; the final exported domain now exactly equals the prepared original-non-mod plus mapped-PC profile. The native ride used an explicit `autopark=off` command. Do not describe the earlier native run as an exhaustive equality test of every default setting; the final preference cleanup was checked offline, not with another game run.

Receipt: `build/modpack-compatibility-20261021/mac-final-receipt.json`. The profile backup and original preferences remain under `mac-backup/` for rollback. No raw saves, credentials, game DLLs or third-party mod DLLs are included in the Persistent City package; it contains only Persistent City, its pinned Harmony dependency/license, disabled-default config and review/provenance documents.
