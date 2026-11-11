# Rig results — 11 November 2026 g PC (#72 serving shop readiness + first sale)

**Summary: 1 passed / 1 partial / 1 failed / 67 not run (70 scoped rows).** The 69 rows from 11d are retained, with a separate serving-readiness prerequisite row. Cross-listed rows count separately. Counts describe this run; earlier passes are not carried forward. The one FAIL is the fixture's serving readiness, not an observed failed checkout.

Role `rig_test`, HomePCHartwieg, Windows background agent1. Candidate `4438c81a180a01791f88410d7526514dd033d35f`, fetched and verified before build; requested tip unchanged. Schema held at 1. Testing only; no production changes, offline dotnet test suite, release, Workshop, version/changelog, issue, merge or Mac interaction.

[Combined checklist](rig-2026-11-06-checklist.md). [11c results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a76b197ddce32def329eb3e34b25c00a01ead318/docs/checks/rig-2026-11-11c-pc-results.md). [11d results](rig-2026-11-11d-pc-results.md).

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11g-pc`. Bulky raw evidence, logs, saves, binaries and backup bytes remain outside Git. Earlier stages were read-only references. Check definitions were retained in `<stage>/check-text` from immutable `86c2e92f`; historical writer numbers in those definitions are superseded by schema 1.

## Build and rig

| Item | Value |
|---|---|
| Candidate / build | `4438c81a180a01791f88410d7526514dd033d35f`; detached build worktree `rig-2026-11-11g-pc`. Requested build.py succeeded against the supplied Steam Managed directory; dotnet PATH prefix and wait-other-dotnet applied. |
| DLL SHA-256 | `de1f287842d0f28d890aaaebd1cead3877ac4e3e33bbfe00be4a74f292678d90`; retained in `<stage>/candidate/PersistentCity.dll`. |
| Official rig checkout | `10cf7d873a733ac564a84d95cc23b4213c101d7f`; remote main independently matched. Tracked source unchanged. No overlay build/install/update. |
| Installed overlay | Ops ProductVersion `1.0.0+10cf7d873a733ac564a84d95cc23b4213c101d7f`; Ops SHA-256 `242f549eee92840d52e37c88060eb888803fca4f89c04d17e4151d7f0dcf0357`; runtime SHA-256 `c8bf526277aa1ffdef72ae6b677c7b8f4dae319c41b75ad2da8e50feebb5dd61`. |
| Rollback revision | `C:/BigAmbitions-AgentTests/revisions/20261111T151740876` untouched; all earlier rollback revisions retained and unused. Restoration used this run's post-#72 backup. |
| Backup | 301 protected entries plus exact isolated HKCU key. `<stage>/backup-manifest.json`, SHA-256 `ddddeaf813f3dc5cf6a546f4048a07c06bf8b5b85da2477ba45573eb9d04de85`; `<stage>/original-lifecycle`. Captured before candidate installation. |
| Isolation | Owner `pc-rig-2026-11-11g`, port 18330, full 30-DLL modpack, background mode. No focus activation, foreground/real input, normal profile/ModsLocal/bridge, ordinary save or other process control. |
| Installed ops | Live `ops` advertises `shop_fixture`, `shop_state`, `shop_log`, `shop_edit`, `foreign_patch`, `rlo_option`, `catalog`, `pcstate`, `pcsave`, `save`, `ledger`, `rival_fingerprint`, `modwork` and baseline ops. `<stage>/installed-capabilities.json`. No separate shop_readiness op was used. |
| Autosave | Native starting preference: 5 minutes / 3 slots / prevent_autosave=false. Installed rig-test guard verified enabled=true / autosave_disabled=true at menu, after new_game and after the normal load. No timed gameplay window ran. Exact starting preferences, config and isolated key restored. |
| Metadata inspection | The standard producer/selfcheck invoked the existing metadata-reader build under the rig's ignored `runtime/Patcher` output. This did not rebuild/install the overlay. Later selfchecks used the existing reader directly via stage-only tooling. No offline suite ran. |

## Runs

| Flow | Outcome | Cleanup |
|---|---|---|
| `creation` | PASS: native new_game at candidate, city running, explicit Ready save | All cleanup actions succeeded; preexisting isolated saves verified. |
| `shop` | COMPLETE: native rental completed, readiness FAIL | No clock advance or sale wait. Normal quit, mod/lifecycle restore and lease release succeeded. |
| Final restore / audit | 301/301 protected file hashes and isolated HKCU key verified | Game closed, lease free, pending=0; owned PIDs 32248 and 27968 gone. |

## shop_fixture selection

**PASS, verified by rig for acquisition.** Checked-in #72 `flows/fixtures/pc-food-shop.json`: deleted `address`, added `selection: {building_size: ba:buildingsize_c}`, retained one showcase and one croissant offer. Paused day 1 08:00 throughout. Money readback: **$10,000 → documented $100,000,000 → $99,971,648 after build**. No further funding or rental workaround.

Picked **58 Fifth Avenue**, business id **`ba:street_fifthavenue#58`**, neighborhood **`ba:neighborhood_murrayhill` (Murray Hill)**, size **`ba:buildingsize_c`**, version **1**, **225 m²**. Native tenant operation: rented=true, property owned=false, rent/day $468, deposit $28,080. Build completed all 19 steps without error.

Raw evidence: `<stage>/shop-spec.json`, `runs/shop/results/010-shop-selection-build.json`, `011-shop-final-status.json`, `015-shop-native-state.json`, `usable-owned-shop.json`. Selection is not a serving-shop pass.

### #72 serving readiness

**FAIL, verified by rig.** Diagnostic ran after rent and before any sale wait. Build `after.shop.readiness`, status `after.shop.readiness` and `shop_state.readiness` agree:

```json
{"ready":false,"reasons":["no_staffed_counter"],"staff":1,"staffed_registers":[],"empty_cargo_holders":[]}
```

Every day has `open=true`, `staffed_counters=0`, reason `no_staffed_counter`. Offer `ba:itemname_croissant` is ready: 100 displayed units, reserve 0, price $5, no offer reasons. The single bakery showcase is available. There is **no `empty_cargo_entry`** reason in this PC recipe.

One available customer-service employee has seven Default 09:00–18:00 shifts at register `0QrYpyAc0eZ9s7qE5xXQ==`, customer_service=50. Register native readback: `point_of_sale=true`, **`available=false`**, one cargo row containing 300 paper bags. This explains which readiness condition did not count; the native reason the register is unavailable was not established. No rearrangement, alternative equipment, extra employee, clock advance or modified recipe was tried.

`<stage>/readiness.json`, SHA-256 `2735bb9abb7a832cb8394836b3a028e6fbbed5d1802407b6cafb267b4b5d8495`. The saved checkpoint's inherited label `Owned Staffed Food Shop` records the builder's staff assignment, not successful serving readiness.

## F8h first sale / check 1 shop assertions

**Check 2 PARTIAL.** A normal load of this run's fresh Ready city completed with full modpack and no test foreign prefix. Checkout active; 49 target methods inventoried, 38 patched, zero refusing checkout. Shared tick owners remain report-only. Activation scans: creation 25.774 ms, normal shop load 27.216 ms. Captured logs contain zero `Food checkout waits for the city load to finish` refusals.

**First sale NOT RUN (needs serving shop: `no_staffed_counter`).** No natural-purchase window ran. No zero-sale wait is substituted for first-sale evidence. The diagnostic failed before any sale assertion.

**Check 1 shop assertions NOT RUN.** Native service, ownership hand-back/reacquisition with service, and receipt comparisons need the serving prerequisite. No fresh foreign-prefix cycle was necessary or performed. 11c's verified install/refusal/remove/reload half is not repeated or recounted. Native-shop halves F8a-5/F8bg-4 and F8a-11 shop assertions are also NOT RUN.

## Remaining shop-rental results

Rental is resolved; **serving readiness remains the blocker**. As required after a not-ready result, dependent gameplay did not start.

| Scope | Result / remaining prerequisite |
|---|---|
| E5b-complete-menu, E5b2–5 shop rows | NOT RUN: serving original-three/mixed shop. One original offer's ready metadata does not prove mixed-menu service, dirty-boundary recovery, in-flight price refusal, held replay or eight/nine-offer clearance. Native equipment/staff/stock facts above remain supporting evidence only. |
| F9a/bdf/c/e/g/h | NOT RUN: valid f9-city-ready serving-shop checkpoint; also paired controls/RIG-ACCEPT/config readbacks/vendor API where named. No official manifest sealed. |
| F6b-1..8 | NOT RUN: serving staffed/rush FT-F6b-RivalLimits fixture and its control variants. No throughput/calibration window. Installed pcstate/ledger/rival_fingerprint are not missing; their existence supplies no experiment. Reviewed config allowlist lacks RivalServiceLanes and RivalMealPeriodUnits. |
| F7-1 / F7-3 | NOT RUN: natural shop purchases / actual receipt_pending state and FT F7 food-shop checkpoint. Explicit native fixture saves were made for provenance only; no interrupted-save or Pending claim. |

Paused contract readback is retained: capabilities passed, dynamicAcquisition=true, productPolicy=published, 23/23 hooks installed. Boundary hour 32: place `address:ba%3Astreet_fifthavenue:58:shop`, phase Refused, managed=false, desired=false, nextBoundary=native. This is not a later accounting-boundary or successful-service assertion. `runs/shop/results/020-*` through `022-*`.

## Findings

1. The #72 one-showcase PC recipe completed native rental and removed the empty-cargo condition observed with two showcases. A fresh PC serving-readiness check still fails `no_staffed_counter`.
2. The register is recognized as a point of sale but is unavailable in native serving facts. Employee availability, positive customer-service value and matching Default shifts do not suffice for this fixture.
3. First-sale, successful native-service and dependent shop rows remain unproved. No production fix or workaround was attempted.
4. No performance window was measured. Startup component observations below are **provisional (PC shared)**, not union mod-work p95 or whole-frame timing.

### Emitted startup observations at or above 30 ms

These overlap and do not represent a unique-spike count. Raw logs: `<stage>/runs/<flow>/Player.log`.

- creation line 1621, frame 5049: preparation **181.298 ms**. Line 1646: advancement maximum **183.304 ms**, slowest citizen unit **183.001 ms**, planner scene search maximum **171.588 ms**; discovery sweep **291.523 ms total work across frames**.
- shop line 1666, frame 4429: preparation **109.408 ms**. Line 1674: advancement maximum **121.869 ms**, slowest other unit **121.684 ms**, planner scene search maximum **100.654 ms**; discovery sweep **210.044 ms total work across frames**.

The stage's compact `component-observations.json` records preparation and advancement maxima; the additional subcomponents and sweep totals above come from the same log lines. No performance verdict or before/after comparison is claimed.

## NOT RUN grouped by missing capability or fixture

| Missing prerequisite / scope exclusion | Checks |
|---|---|
| Serving owned food shop (`no_staffed_counter`) | First-sale half F8h-2; shop halves F8h-1/F8a-11/F8a-5/F8bg-4; E5b shop rows; F6b throughput; F7-1/3; f9-city-ready-dependent rows. |
| Original-three structural/mixed/eight-nine-offer/8–64-shop fixtures and independent traces | E5b2/3 mutation storms, effective holder identity, publication/authority/order/resource delta comparisons, recovery/noninterference and held-policy arms. |
| FT-F6b-RivalLimits rush/queue/service and config variants | F6b-1/4/6/7; tight-profile controls unavailable. F6b-3/5 require their actual decision/fingerprint experiments; no generic read is a pass. F6b-8 requires paired pre-F6b window. |
| RIG-ACCEPT / paired controls / config readback / Street Food vendor API | Formal F9 records, bodies-off/base/day-reload comparisons, weight readback and vendor/dialog rows as named in their definitions. |
| RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY | Reserved step kinds fail validation before lease acquisition; F4 G1/G3/G4/G5/G6/G7/G8/G11/G13 and G2 presentation-trace half. No foreground fallback. |
| ft-f4-stress10k / pc-stress-10k@1 / city-50k@1 | F4 G15, F7-6 and 10k/50k half F7-7; fixtures BLOCKED. |
| RIG-FAULT | F8a-6..8, F8bg-5..7, F7-4. |
| RIG-PCFIX / visitor-body / historical-schema fixtures | F5i visitor fixtures and F7-5; only this run's fresh schema-one cities loaded. |
| Official sealed FT-F4-Declared / FT-F9; unbuilt F7e | No sealing, F4f verdict/implementation or F7e implementation/F7-8. |

F7-2/F4 G12, F7-7/9 from #211 and reload-epoch checks 3–4 from 11c were intentionally not repeated. Foreign-prefix install/remove was not repeated. These exclusions are outside the 70-row count.

## Tooling gaps

| Capability / operation | Exact result | Flow / blocked checks |
|---|---|---|
| shop_fixture build/status + shop_state | `ready=false`, `reasons=["no_staffed_counter"]`; no operation error | `shop`: serving-shop prerequisite failed; dependent rows above NOT RUN. Rental/build itself completed. |

No failed new rig op or `ops_fixture_unknown` occurred. Three supplemental stage console probes used unsupported command spellings; these did not affect the readiness result or create another blocked check. Exact outputs are retained below and in `<stage>/tooling-gaps.json`:

- `FT console pc capability manifest`: `Warning: [Persistent City] Unknown command. Use: pc capability status | pc capability hooks | pc capability business <place> | pc capability show <id> (food.authority, food.checkout.shop, food.dynamic, vendor.streetfood, clock.observe, clock.city, clock.drivenrides, rlo.donor, rlo.reservations, rlo.roadspeeds, research.dialog, research.billing, research.delivery, physical.vehicles, physical.pedestrians, inspector.click, inspector.scroll, inspector.uiblock, interior.view)`
- `FT console pc food diagnostics`: `Warning: [Persistent City] Unknown command. Use: pc population census [workplaces] | pc population person <id> | pc population manifest`
- `FT console pc food rivals`: `Warning: [Persistent City] Unknown command. Use: pc population census [workplaces] | pc population person <id> | pc population manifest`

The address-form `pc capability business ba:street_fifthavenue#58` also returned `Warning: [Persistent City] No scoped food business at ba:street_fifthavenue#58.` The canonical place and authoritative boundary readback are recorded above. No absence-of-business verdict is inferred from that stage query.

## Fixtures

Only the independent character directory created by this run was used. All saves remain in place; three recorded hashes verified after cleanup. Directory:

`C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/nl7uJ56WxU2FNuvoyKvJ4w==/`

| File / fixture name | SHA-256 |
|---|---|
| `FT E5b PC 11 Nov g Ready.hsg` | `04f395a30760498d36a8b8bbbd2eb7670022c1df3dc859f7036573ba757e5aac` |
| `FT E5b PC 11 Nov g.hsg` | `198a5a2a957964f4efd40df952067b65e8f54844b4b0187c21913638fbe6743a` |
| `FT PC 11 Nov g shop Owned Staffed Food Shop.hsg` | `cd9f62f9a6cf600eaec2f2f3da7254695a753c6bc203e203d88ad54a62b82a40` |

Initial fixture was native new_game, then an explicit Ready save after city running; the shop checkpoint was explicitly saved while paused outside any performance window. No historical payload manipulation, old-city load or sealed fixture claim. `<stage>/fixtures.json`, `<stage>/fixture-hash-verification.json`.

## Restore and delivery

Both game instances launched by this run quit normally; no kill. 301/301 protected entries, exact isolated HKCU key, ModsLocal, receipt, run.json, preferences and persistent-city.json matched the post-#72 starting backup. Original autosave preference retained; native suppression was process-local. Preexisting isolated saves verified unchanged by the producer. Disposable saves above remain.

Agent1 closed; lease free, pending=0; no live owned game/helper process. `<stage>/final-verification.json`, `<stage>/restore-verification.json`, `<stage>/owned-process-verification.json`. No rollback revision used. Final-verification SHA-256 `f734d5426d184a0472acec18fa335b97135f1b434905516cf1d88541cd83e29c`.

Selection/readiness segment committed separately before final reporting. Results PR is docs only, remains open and unmerged. Build worktree is removed after evidence commit; evidence worktree detached after PR creation. Report copy: `C:/Users/Peter/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-11g-pc.md`.

## Per-check results

The frozen check definitions remain in `<stage>/check-text`. A NOT RUN row states the unmet setup even when an earlier run supplied some supporting observations; none of those earlier observations is recounted as this run's evidence.

| Check | Result | Evidence / reason |
|---|---|---|
| shop-fixture-selection | **PASS** | Funded native rental selected 58 Fifth Avenue, Murray Hill, C1, 225 m²; no pinned address. Selection pass certifies acquisition only. |
| shop-serving-readiness | **FAIL** | Build, status and shop_state agree: ready=false; sole shop reason no_staffed_counter; all seven days staffed_counters=0; cash register serving.available=false. Croissant offer ready, empty_cargo_holders=[]. |
| e5b-complete-menu-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs a genuine mixed-menu fixture and product/holder/UI clearance evidence. This run has one croissant offer and failed serving readiness. |
| e5b-complete-menu-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs actual paid shop transactions plus independent PC receipt/authority and producer/resource/order delta evidence. No natural-purchase window was started. |
| e5b-complete-menu-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs notification/publication/requested-boundary trace, structural/supply repair and native refill matrix. No price edit or recovery experiment ran. |
| e5b-complete-menu-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs Street Food absent control transport and held/name/receipt semantic comparator; FT bridge cannot be removed as that control transport. |
| e5b-complete-menu-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs matched idle/breakfast/lunch/evening/soak, dirty/recovery and 0/8+ shop fixtures and independent noninterference controls; no timing window measured. |
| e5b2-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs owned burger/hot-dog structural-edit fixture and add-holder/add-price/change-type operations. The one-offer coffee shop does not supply that matrix. |
| e5b2-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs matched 0/8-shop noninterference/performance windows and controls; not supplied by one coffee shop. |
| e5b3-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs the named burger/hot-dog native-method storm (zero stock, bag/holder edits and hours). No edit matrix was run. |
| e5b3-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-SHOPEDIT in-place writes and four acquiring shops; no such mutation capability was invented. |
| e5b3-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs loaded EmployeeWorkstation fixture/capture or complete negative catalogue search for that original-three setup. Bakery-showcase metadata is not that search. |
| e5b3-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs 8 repaired, then 64 owned + 8 acquiring shops and in-place repair mode; no such fixture built. |
| e5b3-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs 0/8/64-shop storm/recovery and matched ordinary performance/noninterference controls; no window measured. |
| e5b4-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs burger/hot-dog original-three metadata override/control fixture. This coffee shop has one croissant offer; no substitute menu was assumed. |
| e5b4-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs observed held dynamic tuples and metadata override replay checkpoint; none established. |
| e5b4-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs matched 0/8 original-three shops and historical base performance controls; no such windows. |
| e5b5-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs native service and PC acquisition of a mixed original/dynamic menu, receipt/revenue comparison and Street Food building-holder evidence. |
| e5b5-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs genuine eight/nine-offer fixtures, a live price-change checkout and menu visual clearance. No price edit or checkout arm ran. |
| e5b5-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs product-provider removal/return control and boundary trace; the native offer is not a removable mod provider, and Street Food transport remains blocked. |
| e5b5-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs observed held dynamic decision and changed metadata/reload control; none established. |
| e5b5-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs 0/8/64 eight-offer fixtures and recovery-wave performance controls; no such windows. |
| f6b-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs FT-F6b-RivalLimits rush fixture and reviewed RivalServiceLanes/RivalMealPeriodUnits config variants; keys absent from installed allowlist. |
| f6b-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs a serving staffed shop and hourly lunch observations with actual purchases to establish a throughput/calibration band. |
| f6b-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RivalFoodDiagnostics and a captured rival decision with ServiceEvidence during the named rush experiment. No rush or label experiment ran. |
| f6b-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs matched default/tight rival-full controls, actual owned-shop purchases and supported lane config arm; not established. |
| f6b-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs before/after native rival fingerprints around the actual checks 1/4 experiments. No such experiment ran. |
| f6b-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs real mid-queue/mid-service ServiceAt checkpoint and unreloaded control. Load-place counts are not that named state/readback. |
| f6b-7 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs check-6 checkpoint plus supported RivalMealPeriodUnits reload variant; key absent from installed allowlist. |
| f6b-8 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs valid staffed rush, matched pre-F6b control and measured union/whole-frame window. No such performance window was measured. |
| f9a-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT and same-head full offline acceptance/provenance artifact. Build/DLL hashes alone do not complete this formal row. |
| f9a-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs day-three 07:30 f9-city-ready with a serving shop, population/visitor targets and selected decisions. No f9 checkpoint was created. |
| f9a-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT native-stage record/status set. Capability observations retained; no formal acceptance pass synthesized. |
| f9a-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs fully ready shop checkpoint and paired seven-day daily-reload/uninterrupted native/PC controls; no meaningful positive shop baseline established. |
| f9a-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired timestamped edits across 30/60/120 presentation/cadence arms and skip controls; background-only run supplies none. |
| f9a-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs mid-leg physical fixture, native enter/leave/skip controls and supported PhysicalEnabled=false arm; flag absent from installed allowlist. |
| f9a-7 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs agency dialog/crew-truck fixture and working Street Food vendor API; live capability reports API missing. No campaign bought. |
| f9a-8 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs historical-schema FT loader/fixture; excluded. Only fresh schema-one cities loaded. |
| f9a-9 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs loaded FoodChoicePriceWeight readback and paired two-day raised/control prices on a serving shop; not established. |
| f9a-10 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT, same-head full acceptance artifact and required native/F4 records. No offline suite or summary run. |
| f9bdf-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT and matching same-head full acceptance/provenance artifact; installed DLL hash retained only. |
| f9bdf-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs mid-leg fixture and PhysicalEnabled=false control; flag absent from installed allowlist. |
| f9bdf-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT plus full/native/F4 records; no summary verdict synthesized. |
| f9bdf-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT native record; raw capability observations retained separately. |
| f9c-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs the f9-city-ready serving-shop checkpoint and its population/decision assertions. No f9 checkpoint was created. |
| f9c-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs complete serving f9-city-ready and paired seven-day daily-reload/uninterrupted controls; no local f9 checkpoint was created. |
| f9c-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired frame-cap/cadence/skip controls with timestamped edits; no presentation/cadence arms in background mode. |
| f9c-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs mid-leg physical fixture and supported PhysicalEnabled=false arm; missing allowlist key. |
| f9c-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs agency dialog/crew-truck fixture and working Street Food vendor API; API missing, no campaign bought. |
| f9c-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs historical-schema FT fixture/loader; excluded. |
| f9e-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT; no formal verdict or AccountingDiagnostics experiment. |
| f9e-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired head/base diagnostic and kept research-order fixture plus RIG-ACCEPT; not established. |
| f9e-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired head/base panel-page observations and supported Ctrl+F8 input; no foreground/real-input fallback. |
| f9e-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired head/base resident/body inspector fixture and supported input; not established. |
| f9e-5 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired base census/save-cost comparison and RIG-ACCEPT; no separate save-profiling run performed. |
| f9e-6 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired observer/presentation controls and panel input/performance windows; reserved presentation steps excluded. |
| f9g-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs mid-leg/bodies-off controls; PhysicalEnabled flag absent from installed allowlist. |
| f9g-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired head/base deterministic body-selection fixture and RIG-ACCEPT; native handles/status alone are insufficient. |
| f9g-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs opening-queue plus published vendor-stand fixture, paired body counts and RIG-ACCEPT; vendor API missing. |
| f9g-4 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs paired head/base presentation performance windows; reserved steps excluded and no window measured. |
| f9h-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT and matching same-head full acceptance/provenance artifact. |
| f9h-2 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs loaded price-weight readback and paired serving-shop two-day price controls. |
| f9h-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs RIG-ACCEPT, matching full acceptance and all native/F4 records; no summary run. |
| f7-1 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs natural purchases and the same-minute/cold-load, paid-sale/Pending/midnight/relocation/control matrix. The fixture-provenance saves do not execute that matrix. |
| f7-3 | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs actual receipt_pending / interrupted-state FT F7 food shop fixture. Template labels do not produce the state; no synthetic Pending, kill or fault arm. |
| f8h-1 | **NOT RUN** | Shop assertions need a serving shop: readiness=false / no_staffed_counter. Foreign-prefix install/remove already verified on 11c and intentionally not repeated. |
| f8h-2 | **PARTIAL** | Normal fresh-city load: checkout active, 49 targets inventoried, 0 refusing, no load-wait refusal. First sale NOT RUN because readiness=false / no_staffed_counter; no clock advance or sale wait. |
| f8a-11 | **NOT RUN** | Shop assertions need a serving shop: readiness=false / no_staffed_counter. Foreign-prefix install/remove already verified on 11c and intentionally not repeated. |
| f8a-5 native-shop half | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs a serving shop and RLO-absent native-service/control observation. Full modpack retained throughout; no absent control ran. |
| f8bg-4 native-shop half | **NOT RUN** | Serving-shop prerequisite failed (no_staffed_counter). Needs a serving shop and RLO-absent native-service/control observation. Full modpack retained throughout; no absent control ran. |

Final run disposition: **BLOCKED** by the serving fixture prerequisite; evidence delivery and cleanup completed.
