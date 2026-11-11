# Rig results — 11 November 2026 PC (Windows-only rows, autosave-off re-runs, F8h first sale)

Role `rig_test`, HomePCHartwieg, Windows background agent1. Candidate `11217a8f68c8dc3cbf7b2dd30041a860d2540e6c` matched the requested minimum and fetched tip before build. Schema held at 1. Testing only; no release, issue change, merge, version or changelog change.

**Summary: 6 passed / 8 partial / 0 failed / 170 not run (184 numbered rows).** Counts include cross-listed slice rows, plus the historical checklist's standalone E5b/E5e/F3/F6b rows; aliases are counted separately as in the prior PC report. Definitions: PASS covers the row's expected assertions; PARTIAL identifies executed assertions with a missing step or readback; NOT RUN has no qualifying execution. Fixture/transport refusals below are setup findings, not inferred product failures.

[Combined checklist](rig-2026-11-06-checklist.md). Its current file is a history link; historical definitions were read from `docs-history-2026-11-10`, current F8bg/F8h/fix definitions from the candidate. Source catalogues and exact check results stay in the stage.

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11-pc`; `<profile>` = `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests`. Raw logs, requests/replies, fixture files, manifests and stage-only test orchestration are outside Git. The earlier PC and Mac stages/worktrees were read only.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `11217a8f68c8dc3cbf7b2dd30041a860d2540e6c`; `git fetch origin` and `git rev-parse origin/codex/persistent-city` before detached-worktree build; tip had not moved. |
| Build | `python tools/build.py --game-managed "C:/Program Files (x86)/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed"`; requested wait-other-dotnet script ran first (no-op); PATH prefixed with `%LOCALAPPDATA%/Microsoft/dotnet`. Build succeeded; no offline test suite. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`; installed hash matched. |
| Rig source / overlay | Read-only `cfcc5fde3f652356a5aa3585250f4a92604557fb`; runtime 7, Input `c9d93537eea54abf8c7fe513cdfe708ded299301a6f652eea646c9a4c445d6c0`, Ops `4b16c613eb2d8bcb90da0f5665ea5f2d6704d21177a01bda35195316c69d2f24`. No overlay build/update. |
| Installed capability truth | `ops` lists panel/rival/weather/catalog/campaign/shop_fixture/shop_state/shop_log/shop_edit/forced_event/camera/screen_pos/capture. `save`, `pcstate`, `modwork` return `ops_unknown_op`; no installed `pcsave`. Source documentation describes later capabilities which are not installed here. `positive/results/003-005,012-013.json`. |
| Mod set / config | 30 DLLs, prior full isolated modpack including RLO, FoodTrucks and Driven Rides; optional E5b diagnostic DLL excluded as in prior PC run. Candidate PC DLL staged through existing RIG-MODSET. Enabled/PhysicalEnabled true, caps 48/24, radius 120 m; no developer config override. `<stage>/head-mods.json`. |
| Autosave original / suppression | Registry and PlayerSettings have no explicit autosave entry; native isolated Options reads 5 minutes / 3 saves (`dr-absent/results/016.json`), read without changing sliders. Existing `LocalRig`/`rig flow run` launcher path sets `-rig-test`; health verifies enabled=true/autosave_disabled=true before creation/load and after reload. This native guard suppresses autosaves while retaining preferences. No measured gameplay windows or save profiling. |
| Backup | Before first mutation: RIG-MODSET DLL/receipt backup and exact run.json, PlayerSettings and isolated HKCU snapshot; preexisting agent1 saves copied/hashed by producer. After verified producer restoration, full 301-entry protected inventory backed up before remaining groups, including all ModsLocal files and PC config. `<stage>/backup-manifest.json`, each run's modset/lifecycle manifests. |
| Isolation / input | Fixed `C:/BigAmbitions-AgentTests`, port 18330; native background, muted. No focus activation, real mouse/keyboard, normal game/profile/ModsLocal access or OS setting change. Own game instances only. Bridge UI callbacks used for native menu/Options readbacks. |
| Cleanup | Four normal stops; no force stop. Per-run modset and lifecycle restore/hash verification, lease released/free/pending 0. Final protected inventory verified: `True`; see cleanup evidence below. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| creation | Native new_game with candidate enabled | PASS creation-save check; Ready retained, guard verified. Evidence committed first in `fd77457`. |
| positive / S1 | Ordinary save and native reload at 08:00 | City running; settled 49/49; no lost-epoch refusal. Separate main-menu confirmation refused by bridge guard; `load` from city used. |
| positive / setup attempts | H1 and Hour-labelled checkpoints | Saved at 08:00: one asynchronous advance was interrupted by reload and another refused while a dialog held the clock. Excluded from one-hour proof; retained as setup artifacts. Some diagnostic requests during loading refused; settled captures followed. |
| positive / verified hour | Explicitly observed 08:00 → 09:00, save/load Verified checkpoint | Reloaded 09:00, city running, guard verified, 49/49. Three sampled identity assignments unchanged; full state equality not claimed. |
| positive / shop_fixture | Normal coffee-shop rental/setup | Refused at acquisition: `ops_shop_native_rental_refused`, address `ba:street_fourthavenue#18`; no shop rented/owned, no equipment/stock/staff steps. No money injected, no rental retry. |
| dr-absent | Fresh candidate Ready checkpoint, Driven Rides DLLs omitted | City running, optional-presence clock absent, factor one third and uncoordinated. |
| rlo-absent | Same own fresh candidate checkpoint, RLO DLL omitted | Required-donor refusal recorded. Native shop/no-receipt assertions unavailable. |

## F8h

**Check 1 NOT RUN — needs RIG-FOREIGNPATCH.** No official rig Markdown documents installing/removing a named test prefix; installed ops has no such operation. No injector, owner or fault build was created.

**Check 2 PARTIAL.** Settled `food.checkout.shop optional active`; hook header `49 of 49 target methods patched`, `0 refusing food.checkout.shop`. `RunMainGameTick` lists cityperformance.mod prefix (may-skip)/finalizer (swallows), drivenrides.mod prefix (may-skip), foodtrucks.mod prefix (may-skip)/postfix (observer)/finalizer (swallows). None is marked refusing. Activation lines retain `scan <ms>` and no checkout-off reason. Early post-load status temporarily has 38/49 while physical setup finishes; settled captures are used.

The first sale could not run: the existing shop_fixture flow refused native rental of 18 Fourth Avenue, leaving it empty, unrented and unowned, with no stock/staff/equipment. `<stage>/runs/positive/results/063.json` retains the refusal and native readback. The reply does not give a more specific rental reason; do not infer the Mac's not-enough-money cause. No shop was established, and no PC receipt or first-sale pass is claimed. The pinned Street Food vendor API is absent, independently of shop checkout.

Activation scan observations (provisional, PC shared): creation line 1598: 15.692 ms; positive line 1538: 25.381 ms; positive line 2767: 6.452 ms; positive line 4055: 12.346 ms; positive line 5480: 25.819 ms; positive line 6976: 9.816 ms; dr-absent line 1513: 24.172 ms. No post-load `Food checkout waits for the city load to finish` refusal observed in positive Player.log.

## Performance observations

No clean mod-work union window was measured: installed `modwork` is unavailable. No p95 gate or whole-frame performance verdict. The following are logged component observations only, **provisional (PC shared)**. Every emitted Slow routing component sample ≥30 ms is listed; these are not union samples. Unlogged individual samples cannot be enumerated. No save profiling.

| Run / Player.log line | Frame | Component | ms |
|---|---:|---|---:|
| creation / 1619 | 6492 | preparation | 148.538 |
| positive / 1545 | 1377 | preparation | 103.030 |
| positive / 2781 | 4142 | preparation | 144.975 |
| positive / 4086 | 5466 | preparation | 112.587 |
| positive / 5486 | 7655 | preparation | 145.731 |
| positive / 6983 | 13556 | preparation | 151.831 |
| positive / 6984 | 13635 | closure refresh | 99.500 |
| dr-absent / 1524 | 7441 | preparation | 185.920 |

Aggregate diagnostic maxima ≥30 ms (not extra independent spikes): scene search 171.979 ms, preparation maximum 185.920 ms, closure maximum 99.500 ms. Full extraction: `<stage>/observed-timings.json`. Advancement totals and whole-frame/RLO times are not classified as PC spikes.

## Findings

1. Creation initialization completes and the guarded native reloads run at their saved minutes. Positive log has no lost-epoch, clock.city refusal, startup-wait warning or start-a-new-city message. Payload/epoch validation and a full identity digest remain unverified because installed extraction/readback ops are missing.
2. c1/c500/c957 IDs and home/work assignments match across the verified 09:00 reload. c500/c957 full console records match; c1 next_event changes from 1989.7508482813714 to 1981.6334733148396. This is retained as an unassessed dynamic-state difference, not hidden behind an identity-equality claim.
3. Full-modpack F8h diagnostics pass; first-sale proof remains missing after the native rental refusal. No campaign, rival-finance write or money injection.
4. Existing rig source is newer than installed Ops. Missing operations stayed missing; no rig upgrade or new native capability was implemented. Expected bridge refusals and fixture setup failures are preserved in raw evidence.
5. The standalone menu confirmation callback is protected by the bridge's save/load guard. `ui escape` also refuses opening a pause menu when nothing is open. Documented native load from city was used; the requested separate menu step remains qualified, with no foreground fallback.

## NOT RUN grouped by missing capability or fixture

These prerequisite groups overlap. The per-check table below gives exact counted statuses.

| Missing capability / fixture | Rows / remaining assertions |
|---|---|
| RIG-FOREIGNPATCH / exact RIG-HARMONYOWNERS | F8h-1; F8a-11 and F8a-1 respectively. Foreign-owner reporting alone does not prove every PC owner's multiplicity. |
| RIG-RLOTOGGLE | fix-reload-accounting-epoch-3/4; F8a-9 / F8bg-8. No Flare/Social Media toggle attempted through real input. |
| RIG-FAULT | F8a-6..8 / F8bg-5..7, with semantic save prerequisites. No variant built or installed. |
| RIG-PCSAVE / RIG-PCSTATE / RIG-LEDGER | F2/F5 equality, held/replay, receipt/payload/epoch/no-receipt and F6b state checks; remaining assertions in creation-save-init-2, reload-epoch-2, X3-1 and RLO-absent rows. Direct capability reads refused. |
| RIG-FOODSHOP / native rental / dynamic held fixture | E5b2..5 shop-dependent rows, historical E5b checks, F8a-10 / F8bg-9, F8h-2 first sale, F6b throughput. Rental refused; no owned food shop, mixed/third shop or 0/8-shop comparison fixture. |
| RIG-SFAPI / RIG-VLEDGER / absent-mod control transport | E6f-1..7, Street Food absent/return and mechanism arms, E5b4-5 dynamic held choice. Vendor API absent; removing FoodTrucks also removes FT control transport. |
| RIG-PCFIX / RIG-LIVE / body-ID / config / arms / drive / replay support | F5i-1..6, F2g-1..11 and related F2c/d/e rows. No qualifying owned-shop/visitor-body/mover fixture. Installed screen_pos has no PC body-ID route; wrong view arguments were refused, not treated as evidence. |
| FT-F4, RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY, RIG-F4-PRODUCER | F4a..e rows. Missing fixture and producers; F4f/F7e and combined native/perf batch explicitly excluded. |
| RIG-PERF-UNION gaps / PC components / stress and checkpoint profilers | F8a-12 / F8bg-10, F2g-10, F5i-5, E5 performance, F3 and F6b timing. `modwork` unavailable; no clean window claimed. |
| FT-F9 / f9-city-ready / RIG-ACCEPT | All F9 rows; native rental prevented the day-3 owned-shop fixture. Combined batch excluded. |
| RIG-API real-provider / independent catalogue trace | Historical E5e API and E5b native-truth/override assertions. No provider or custom trace installed. |
| Historical/future schema fixtures / foreground-real input | Historical 35/36/37 assertions are superseded by schema 1; only fresh candidate cities allowed. Input-only hover/picking/car control rows have no foreground fallback. |

## Fixtures

All files below belong to the single new native FT character at this candidate, in the isolated agent1 profile. Disposable saves stay in place. No older development or ordinary save was loaded. H1/Hour setup names with 08:00 content are explicitly excluded from hour proof. Hashes re-read after stopping.

| Name | Path | SHA-256 |
|---|---|---|
| `FT E5b PC 11 Nov 11217a8f Ready` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT E5b PC 11 Nov 11217a8f Ready.hsg` | `f1c5c41923e9057bb206fa917dd6b6081b37c82ade4f409af04b6108dbe683a3` |
| `FT E5b PC 11 Nov 11217a8f` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT E5b PC 11 Nov 11217a8f.hsg` | `e73caffd08acef116ee407355898aaa36852985043793eb9b5d60fd38abd07c8` |
| `FT PC 11 Nov 0900 Verified Reload` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov 0900 Verified Reload.hsg` | `32937bf59c19d232ed5f9d18f749d01fc077fdc835b98234b7a7ad730740cd6f` |
| `FT PC 11 Nov 0900 Verified` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov 0900 Verified.hsg` | `f6658607e0ab3c281246247c21eb358134aa3b599a9f06792e4e72883748e457` |
| `FT PC 11 Nov H1 Reload` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov H1 Reload.hsg` | `33d2c3a6cebdcd07df2c32f559107fc1bfe3839332472715c5174794f597593c` |
| `FT PC 11 Nov H1` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov H1.hsg` | `782b2396febaef4b7d17f26448d9004391d498c66d50fffa9950c2b63088bf79` |
| `FT PC 11 Nov Hour 0900` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov Hour 0900.hsg` | `cd788013294516588e40ab5223c1024bcd107ea1796fe9afbb47f2d6f9fbfdb3` |
| `FT PC 11 Nov Hour Reload` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov Hour Reload.hsg` | `1b7086f1540496c3ce06dca949653b59cc13bf1728d3aeec0225891f527a377e` |
| `FT PC 11 Nov S1` | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT PC 11 Nov S1.hsg` | `42828f3b27dd852e6157c1aacdf27384b9232d18a48a176107b0383eeb9dd5d8` |

## Per-check results grouped by slice

The historical checklist's standalone E5b rows overlap later slice rows; this table counts their named rows separately. Full exact status/reason list: `<stage>/check-results.json`. NOT RUN ranges do not include rows separately marked PASS/PARTIAL below.

| Slice | Pass | Partial | Fail | Not run | Per-check results |
|---|---:|---:|---:|---:|---|
| e5b-complete-menu | 0 | 0 | 0 | 6 | 1,2,3,4,5 NOT RUN: RIG-FOODSHOP mixed/serving fixture unavailable after rental refusal; independent holder/tag-cache, PC receipt/order/authority or notification traces missing as required.; 6 NOT RUN: RIG-FOODSHOP and complete RIG-PERF-UNION / PC component counters missing; rental refused. |
| e5b2 | 0 | 0 | 0 | 3 | 2,3,4 NOT RUN: RIG-FOODSHOP mixed/serving fixture unavailable after rental refusal; independent holder/tag-cache, PC receipt/order/authority or notification traces missing as required. |
| e5b3 | 0 | 1 | 0 | 5 | 1 PARTIAL: Cargo version-reader and self-test passed; settled Active, capabilitiesPassed true, 23/23 hooks and warming=0 across reloads. Immediate Warming state not captured. positive/results/010-011,028-029,031-032,118-119.json.; 2,3,4,5 NOT RUN: RIG-FOODSHOP mixed/serving fixture unavailable after rental refusal; independent holder/tag-cache, PC receipt/order/authority or notification traces missing as required.; 6 NOT RUN: RIG-FOODSHOP and complete RIG-PERF-UNION / PC component counters missing; rental refused. |
| e5b4 | 0 | 0 | 0 | 6 | 1,2,3 NOT RUN: Schema 1 replaces historical 35/36/37 expectations; older saves excluded; RIG-PCSAVE extraction/write unavailable.; 4 NOT RUN: RIG-FOODSHOP mixed/serving fixture unavailable after rental refusal; independent holder/tag-cache, PC receipt/order/authority or notification traces missing as required.; 5 NOT RUN: Held dynamic-sale fixture missing: no shop after native rental refusal and Street Food vendor API absent.; 6 NOT RUN: RIG-FOODSHOP and complete RIG-PERF-UNION / PC component counters missing; rental refused. |
| e5b5 | 1 | 0 | 0 | 5 | 1 PASS: After warm-up across reloads: dynamicAcquisition true, productPolicy published, capabilitiesPassed true, firstFailure null. positive/results/010-011,028-029,031-032,118-119.json.; 2,3,4,5 NOT RUN: RIG-FOODSHOP mixed/serving fixture unavailable after rental refusal; independent holder/tag-cache, PC receipt/order/authority or notification traces missing as required.; 6 NOT RUN: RIG-FOODSHOP and complete RIG-PERF-UNION / PC component counters missing; rental refused. |
| e5e-metadata-api | 0 | 0 | 0 | 1 | 1 NOT RUN: RIG-API real-provider lifecycle fixture unavailable in the installed mod set; no provider built or installed. |
| e6f | 0 | 0 | 0 | 7 | 1,2,3,4,5,6,7 NOT RUN: RIG-SFAPI / RIG-VLEDGER and crewed dynamic cart fixture: pinned Street Food vendor API missing. |
| f2c | 0 | 0 | 0 | 5 | 1,2,3,4,5 NOT RUN: RIG-PCSAVE semantic extraction/comparison unavailable; visitor/mover fixture, PhysicalEnabled/fault config, RIG-ARMS-STEPS / replay input / RIG-DRIVE prerequisites as applicable. |
| f2d | 0 | 0 | 0 | 7 | 1,2,3,4,5,6,7 NOT RUN: RIG-PCSAVE semantic extraction/comparison unavailable; visitor/mover fixture, PhysicalEnabled/fault config, RIG-ARMS-STEPS / replay input / RIG-DRIVE prerequisites as applicable. |
| f2e | 0 | 0 | 0 | 2 | 1,2 NOT RUN: RIG-PCSAVE semantic extraction/comparison unavailable; visitor/mover fixture, PhysicalEnabled/fault config, RIG-ARMS-STEPS / replay input / RIG-DRIVE prerequisites as applicable. |
| f2g | 0 | 0 | 0 | 11 | 1,2,3,5,6,7,8,11 NOT RUN: RIG-PCSAVE semantic extraction/comparison unavailable; visitor/mover fixture, PhysicalEnabled/fault config, RIG-ARMS-STEPS / replay input / RIG-DRIVE prerequisites as applicable.; 4 NOT RUN: Owned-shop / resident walk-to-car fixture unavailable after rental refusal; no qualifying camera-follow run.; 9 NOT RUN: RIG-DRIVE / real-input car-blocking pose and hold replay/config support missing.; 10 NOT RUN: RIG-PERF-UNION modwork op unavailable; owned-shop sweep fixture missing. |
| f4a | 0 | 0 | 0 | 8 | 1,2,3,4,5,6,7,8 NOT RUN: FT-F4 fixture and RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY / RIG-F4-PRODUCER / complete RIG-PERF-UNION missing. |
| f4b | 0 | 0 | 0 | 8 | 1,2,3,4,5,6,7,8 NOT RUN: FT-F4 fixture and RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY / RIG-F4-PRODUCER / complete RIG-PERF-UNION missing. |
| f4c | 0 | 0 | 0 | 5 | 1,2,3,4,5 NOT RUN: FT-F4 fixture and RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY / RIG-F4-PRODUCER / complete RIG-PERF-UNION missing. |
| f4d | 0 | 0 | 0 | 15 | 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15 NOT RUN: FT-F4 fixture and RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY / RIG-F4-PRODUCER / complete RIG-PERF-UNION missing. |
| f4e | 0 | 0 | 0 | 5 | 1,2,3,4,5 NOT RUN: FT-F4 fixture and RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY / RIG-F4-PRODUCER / complete RIG-PERF-UNION missing. |
| f5i | 0 | 0 | 0 | 6 | 1,2,3,4,5 NOT RUN: RIG-PCFIX visitor-body fixture, RIG-LIVE and body-ID readback missing; RIG-PCSAVE / PC component union timing unavailable.; 6 NOT RUN: RIG-PCFIX visitor-body fixture, RIG-LIVE and body-ID readback missing; foreground/real input required. |
| f6b | 0 | 0 | 0 | 8 | 1,2,3,4,5,6,7,8 NOT RUN: FT-F6b-RivalLimits staffed/rush fixture unavailable; installed RIG-PCSTATE / RIG-LEDGER / RIG-RIVALFP and config variants missing. |
| f8a | 2 | 1 | 0 | 9 | 2 PASS: City running, all required capabilities active; optional active/off with reasons; 49/49 settled hook targets, foreign owners and classes retained. positive/results/064-065,111-112.json.; 4 PASS: Without Driven Rides: city running, clock.drivenrides required-when-present absent; activation factor 0.33333333333333331 matches default and coordinated=False. dr-absent/results/001.json and Player.log. No player-shop sale assertion in this absence row.; 5 PARTIAL: Without RLO: refusal/status recorded in rlo-absent; source fixture hash unchanged. Native-shop service and live PC payload/no-receipt preservation unverified: rental refused and installed RIG-PCSAVE/PCSTATE absent.; 1 NOT RUN: RIG-HARMONYOWNERS exact PC-owner multiplicity readback missing; foreign-owner report alone is insufficient.; 3 NOT RUN: FoodTrucks-absent independent bridge transport, RIG-PCSAVE receipts/money and positive vendor fixture missing.; 6,7,8 NOT RUN: RIG-FAULT selection plus RIG-PCSAVE; no fault variant installed.; 9 NOT RUN: RIG-RLOTOGGLE plus RIG-PCSAVE identities.; 10 NOT RUN: RIG-FOODSHOP / dynamic third shop and RIG-SFAPI cart fixture missing; rental refused.; 11 NOT RUN: RIG-FOREIGNPATCH: no documented install/remove capability.; 12 NOT RUN: Installed RIG-PERF-UNION modwork op unavailable; no clean union window. |
| f8bg | 2 | 1 | 0 | 7 | 1 PASS: City running, all required capabilities active; optional active/off with reasons; 49/49 settled hook targets, foreign owners and classes retained. positive/results/064-065,111-112.json.; 3 PASS: Without Driven Rides: city running, clock.drivenrides required-when-present absent; activation factor 0.33333333333333331 matches default and coordinated=False. dr-absent/results/001.json and Player.log. No player-shop sale assertion in this absence row.; 4 PARTIAL: Without RLO: refusal/status recorded in rlo-absent; source fixture hash unchanged. Native-shop service and live PC payload/no-receipt preservation unverified: rental refused and installed RIG-PCSAVE/PCSTATE absent.; 2 NOT RUN: FoodTrucks-absent independent bridge transport, RIG-PCSAVE receipts/money and positive vendor fixture missing.; 5,6,7 NOT RUN: RIG-FAULT selection plus RIG-PCSAVE; no fault variant installed.; 8 NOT RUN: RIG-RLOTOGGLE plus RIG-PCSAVE identities.; 9 NOT RUN: RIG-FOODSHOP / dynamic third shop and RIG-SFAPI cart fixture missing; rental refused.; 10 NOT RUN: Installed RIG-PERF-UNION modwork op unavailable; no clean union window. |
| f8h | 0 | 1 | 0 | 1 | 2 PARTIAL: Checkout active, 49/49 settled targets, 0 refusing checkout; shared-tick owners/classes report-only; activation scan retained, no post-load checkout-wait refusal. First sale unavailable: shop_fixture native rental refused at ba:street_fourthavenue#18, no shop established.; 1 NOT RUN: RIG-FOREIGNPATCH: no documented install/remove capability. |
| f9a | 0 | 0 | 0 | 10 | 1,2,3,4,5,6,7,8,9,10 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| f9bdf | 0 | 0 | 0 | 4 | 1,2,3,4 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| f9c | 0 | 0 | 0 | 6 | 1,2,3,4,5,6 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| f9e | 0 | 0 | 0 | 6 | 1,2,3,4,5,6 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| f9g | 0 | 0 | 0 | 4 | 1,2,3,4 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| f9h | 0 | 0 | 0 | 3 | 1,2,3 NOT RUN: FT-F9 / f9-city-ready unavailable after rental refusal; RIG-ACCEPT and semantic/ledger readbacks absent. Combined native/perf batch excluded. |
| fix-creation-save-init | 1 | 1 | 0 | 0 | 1 PASS: Native creation save completed; expected skipped-initialization line; city running, 49/49 targets; no save-epoch or clock.city refusal. Guard enabled/off before creation and after load. creation/report.json and Player.log.; 2 PARTIAL: Ordinary S1 and 09:00 saves load and run, 49/49 settled targets. Payload and success-epoch marker not extracted: installed pcsave op absent. Separate main-menu confirmation refused; native load used. |
| fix-reload-accounting-epoch | 0 | 2 | 0 | 2 | 1 PARTIAL: S1 reload at saved 08:00, city running, settled 49/49; guard enabled/off, no lost epoch, clock.city refusal or startup-wait warning. Separate quit-to-menu confirmation refused by bridge; native load from city used.; 2 PARTIAL: Verified one-hour advance 08:00 to 09:00; save/reload at 09:00, city running and 49/49. Three sampled IDs/home/work assignments match; full resident identity digest unavailable without RIG-PCSAVE. Native load from city used.; 3,4 NOT RUN: RIG-RLOTOGGLE: no documented Flare/Social Media switch. |
| pc-f3-bounded-bulk-work | 0 | 0 | 0 | 5 | 1,2,3,4,5 NOT RUN: RIG-PERF-UNION posting/backlog profiler, interruption checkpoints, native topology mutation/readback and stress fixtures missing. |
| x3 | 0 | 1 | 0 | 0 | 1 PARTIAL: Fresh candidate city advanced one game hour, Verified checkpoint saved/reloaded at 09:00, guard enabled/off after reload, 957 residents/957 visitors. Sampled identities match; no full digest or literal Loaded/schema-1 population log. No start-a-new-city refusal. |

## Cleanup evidence

Each lifecycle snapshot/restoration records exact bytes/absence for run.json and PlayerSettings, and SHA-256 of an export restricted to `HKCU/Software/Hovgaard Games/Big Ambitions Agent Tests`. Each RIG-MODSET transaction restored its original DLL set and receipt; no overlay change. Producer verification confirms preexisting isolated save files unchanged. Native preferences including autosave restored, then hashes verified; fixture outputs stay.

Final full verification: {"verified": true, "protected_entries": 301, "mismatches": 0, "extra_mod_files": 0, "registry_verified": true, "registry_sha256": "03f3d95c25debe2647ea840aab60557b4599cbfcfed9691efd723f0cb59eacb0", "lease_free": true, "lease_pending": 0, "agent_running": false, "normal_running": false}. `<stage>/restore-verification.json`, each `runs/<run>/lifecycle/restore.json`, modset receipt and report.json retain details. Final probe confirms lease free/pending 0 and no agent1 player running. All four foreground stage drivers exited; no owned worker remains. Build worktree removed after evidence commit; evidence worktree removed after PR creation. No results PR merge authorized.
