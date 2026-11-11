# Rig results — 11 November 2026 d PC (shop_fixture selection + F8h first sale)

**Summary: 1 passed / 14 partial / 0 failed / 54 not run (69 scoped numbered rows).** Cross-listed rows count separately. PARTIAL identifies completed assertion subsets; the missing assertions remain explicitly unrun. No first-sale or native-service pass is inferred from zero activity. Scope exclusions below are outside this count; 11c's four reload/toggle PASS rows were not repeated, and the selection row is new.

Role `rig_test`, HomePCHartwieg, Windows background agent1. Candidate `86c2e92f33e176fd29cea0072c0f5341f299e97f`, fetched and verified before build; tip unchanged. Schema held at 1. Testing only: no production changes, offline dotnet suite, overlay update, release, Workshop, version/changelog, issue or merge.

[Combined checklist](rig-2026-11-06-checklist.md). [Previous PC run #212](rig-2026-11-11c-pc-results.md).

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11d-pc`. Raw JSON, logs, saves, binaries, captures and backups stay outside Git. Earlier stages/worktrees and the official rig source were left unchanged.

## Build and rig

| Item | Value |
|---|---|
| Candidate / build | `86c2e92f33e176fd29cea0072c0f5341f299e97f`; detached `rig-2026-11-11d-pc`. Requested build.py passed; dotnet PATH prefix and wait-other-dotnet applied. No offline suite. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`; retained under `<stage>/candidate`. |
| Rig checkout | `baebea60ac96b378ddedfd28aa25682ae3526c3d`; remote main matches. Read-only source; no overlay build/install/update. |
| Installed overlay | Ops ProductVersion `1.0.0+baebea60ac96b378ddedfd28aa25682ae3526c3d`; SHA-256 `8fbbc7294fd0741e64ff46b5046be9b5b0ba368bc6d6b2e1c51b623c812fc1a3`. |
| Rollback revision | `C:/BigAmbitions-AgentTests/revisions/20261111T102906939` untouched, as are `20261111T084212647` and `20261111T064506475`. No rollback used. |
| Backup | Current post-install 301 protected entries plus exact isolated HKCU key. `<stage>/backup-manifest.json`, SHA-256 `e3b469d69fa008fd5f7b30b34b17ed69997fc81f159e9d57aafda44df0e5de21`; `original-lifecycle/`. |
| Isolation | Stable owner `pc-rig-2026-11-11d`, port 18330, full 30-DLL modpack except the explicit RLO-absent control. No focus, foreground, real input, normal profile/ModsLocal/bridge access, Windows setting change outside the isolated rig key or other process control. |
| Installed ops | Live `ops` advertises shop_fixture, foreign_patch, rlo_option, shop_state/log/edit, catalog, pcstate, pcsave, ledger, rival_fingerprint, modwork and baseline ops. `<stage>/runs/shop/results/003-installed-ops.json`. |
| Autosave | Original native preference 5 minutes / 3 slots, prevent_autosave=false. Installed rig-test guard verified enabled=true / autosave_disabled=true before waits and after every load. Original preferences/key restored exactly; no measured window or automatic save was accepted as evidence. |

## Runs

| Flow | Outcome | Cleanup |
|---|---|---|
| `creation` | PASS; `<stage>/runs/creation` | All cleanup actions succeeded. |
| `f7cold` | COMPLETE; `<stage>/runs/f7cold` | All cleanup actions succeeded. |
| `firstsale` | COMPLETE; `<stage>/runs/firstsale` | All cleanup actions succeeded. |
| `firstsale2` | COMPLETE; `<stage>/runs/firstsale2` | All cleanup actions succeeded. |
| `foreignshop` | COMPLETE; `<stage>/runs/foreignshop` | All cleanup actions succeeded. |
| `mixed` | COMPLETE; `<stage>/runs/mixed` | All cleanup actions succeeded. |
| `remaining` | COMPLETE; `<stage>/runs/remaining` | All cleanup actions succeeded. |
| `rlo-absent` | COMPLETE; `<stage>/runs/rlo-absent` | All cleanup actions succeeded. |
| `shop` | COMPLETE; `<stage>/runs/shop` | All cleanup actions succeeded. |

## shop_fixture selection

**PASS, verified by rig.** Used the checked-in pc-food-shop spec with `address` deleted and `selection: {building_size: ba:buildingsize_c}` added. Money readback: **$10,000 → documented $100,000,000 → $99,952,000 after build**. Native rental completed; no additional funding, campaign purchase, property-buy workaround or rival-finance write.

Picked **24 Fourth Avenue**, builder business id `ba:street_fourthavenue#24`, neighborhood `ba:neighborhood_hellskitchen` (Hell’s Kitchen), size `ba:buildingsize_c`, version **2**, **225 m²**. Native readback: rented=true, property owned=false, rent/day $792, deposit $47,520. The player operates the business as tenant. One customer-service employee; seven 09:00–18:00 Default shifts; cash register, two bakery showcases, storage shelf, cleaning station; 100 croissants at $5 and 300 bags. Raw spec/complete build/status/shop_state: `<stage>/shop-spec.json`, `runs/shop/results/010-shop-selection-build.json`, `usable-owned-shop.json`.

The initial image was Refused / `MissingDefinition`; one cargo entry was empty. This correlation is not a root-cause diagnosis. The supported checked-in `shop-multi-offer.json` spec then reconciled the **same named rental**, using a new fixture id without additional funding or a new acquisition. Native offers became croissant/donut, stock 100 each plus 300 bags. Loaded catalogue readback identifies donut as base-game with a complete bakery-showcase holder link. PC passed the image and acquired it at a later boundary. No Street Food holder was placed. `<stage>/runs/mixed`.

## F8h first sale / check 1 shop assertions

**Check 2 PARTIAL: no first-sale proof.** Checkout active, 49 target methods inventoried (38 patched), zero refusing entries; captured normal-load log contains no `Food checkout waits for the city load to finish` refusal. No observed first sale occurred.

| Attempt | Actual setup / outcome |
|---|---|
| firstsale | Stage waiter accepted stale paused health after next-day target request; actual load near day-1 08:00, ending 12:00. Not the requested window. 33 visits, 41 not_purchased, 33 left_without_buying, reason `unavailable`, 0 purchases, dropped=0, hook_faults=0; stock unchanged. Retained, not used as the exact-window proof. |
| firstsale2 | Corrected exact day-2 10:00 paused target, manual checkpoint, normal reload and autosave suppression verified. 10:00–12:00: 0 log entries/purchases, dropped=0, hook_faults=0, $0 shop revenue, stock unchanged; no food-sale key. PC shop Published / passed / managed / Ready. |
| foreignshop normal extension | Fresh normal load and 12:00–13:00: 0 purchases and no food-sale key. No first-sale verdict inferred. |

**Check 1 PARTIAL: shop ownership transitions verified by rig.** A fresh prefix cycle was needed for the shop assertions left open on 11c. `agent-rig.foreign.pc1111d` produced the exact shop status:

```text
Excluded: food checkout contract off: foreign prefix by agent-rig.foreign.pc1111d on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)
```

| Observation | Managed | Desired | Boundary readback |
|---|---|---|---|
| Refused load, hour 61 | true | false | hand back |
| After boundaries, hour 63 | false | false | native |
| Removed owner + normal reload, hour 63 | false | true | Ready |
| Next boundary, hour 64 / 16:00 | true | true | owned; lastBoundary=64, lastReadiness=Ready |

The refused 13:00–15:00 window recorded **4 native visits, 8 not_purchased, 4 left_without_buying**, reason `unavailable`, **0 purchases**, dropped=0, hook_faults=0. No food-sale key before/after; stock and $0 revenue unchanged. Native dispatch and receipt suppression are observed; successful native service is unproved. Owner removal/reload restored checkout. Canonical PC place is `address:ba%3Astreet_fourthavenue:24:shop`. Raw boundary records 033/046/059/067, logs and requests: `<stage>/runs/foreignshop`.

### Native-shop halves: F8a-5 / F8bg-4

**PARTIAL.** Same fresh checkpoint without RLO: `start refused (rlo.donor)` / `RealLifeOverhaul is required and is not loaded.`, no start-a-new-city wording. All live PC key hashes identical through 16:00–17:00; no new receipt. Native log: 0 visits/purchases, dropped=0, hook_faults=0. No absent-state checkpoint saved. Successful native service remains unproved. Full modpack restored. `<stage>/runs/rlo-absent`, `rlo-absent-summary.json`.

## Remaining shop-rental results

### E5b complete-menu / E5b2–5

Mixed native setup and catalogue facts are recorded above. A paused native donut price edit **$4→$6→$4** was confirmed; PC stayed Published, desired=true, managed=true at hour 64. Stamps/hints/capabilities retained. No in-flight price refusal/requeue, full structural storm, native refill, removal/return, held-policy replay or eight/nine-offer visual assertion is inferred. Required original-three burger/hot-dog, 8/64-shop and in-place mutation cases remain NOT RUN in the per-check table. `<stage>/runs/remaining/results`, e5b-prefixed actions.

### F9

Local **unsealed** `f9-city-ready` saved at day 3 07:30. Manifest: **957 residents, 1,914 visitors**, commuter target/capacity 1,914; city running and shop PC-managed. No selected shop decision or purchase was established. Capability, AccountingDiagnostics, FoodDiagnostics and EconomyOverview were readable; Street Food vendor API was explicitly missing. Formal RIG-ACCEPT records, paired controls, bodies-off/config readback, vendor/dialog and reserved presentation rows remain NOT RUN. No official FT-F9/FT-F4 manifest sealing. `<stage>/runs/remaining/f9-day3-Player.log`, f9-prefixed results.

### F6b

Native staffed-shop snapshots at 10:00, 11:00, 12:00 and 13:00 retained unchanged stock and $0 revenue; bounded log had 0 purchases, dropped=0, hook_faults=0. No useful staffed-throughput/calibration band. Core reported **25 modeled rival choices and settlements**, with labels `estimated (modeled service limits)`, `modeled sell-out (estimated)`, `EstimatedFromPriceAndHours`, `Unsupported` native rival checkout. Captured food decisions do not expose the required `ServiceEvidence` field; that assertion remains unrun.

Read-only fingerprint pair for `ba:street_firstavenue#34` is identical across the intraday lunch observation: `ae72350d69b5934f28c0290db413b18b2ec5ac6f219efa7c6311eeaea83ddb04`. This is one observed rival; it does not substitute for checks 1/4's tight-profile/control experiments. Native ledger retained. No native rival field written. Installed reviewed config allowlist lacks `RivalServiceLanes` and `RivalMealPeriodUnits`; no config workaround. `<stage>/runs/remaining`, f6-prefixed results.

### F7 checks 1 and 3

**Check 1 PARTIAL, coherent same-minute/cold-load subset verified by rig.** Two paused saves at native day 3 13:00, money **$99,949,970**, logical minute **5100.0099983215332**, have identical schema-1 payloads: **7,137,842 UTF-16 characters**, SHA-256 `e4a734e85e15586d7c8808e123dc4e866dca7a6d3c47b1e4ef5a41f18d806bd3`. Success-epoch sequences **12 / 13**, exact length declarations match; no accounting-stop or food-sale key. Native-reader extracts retained outside Git.

Fresh-process reload of A reached city running; SaveEpochDiagnostics attached sequence=12, refused=0, no lastRefusal/stopPending. Native/logical minute match, lag=0 and historicalClockMisses=0. A new explicit post-reload checkpoint was saved. This verifies continuation, not a paid-sale/Pending/midnight/relocation or interrupted-control matrix. `<stage>/f7-comparison.json`, `runs/remaining/f7-*-extract.json`, `runs/f7cold`.

**Check 3 NOT RUN:** no actual receipt_pending activity or corresponding FT F7 food shop checkpoint. Checked-in templates label a reached state; they do not produce Pending receipts. No artificial Pending state, save kill or fault arm was created. No repeated F7-2/7/9, historical/stress/50k, F7e or save-profiling run.

## Findings

1. Funded nearest size-C selection now completes native rental; the old named-address vacancy obstacle is removed for this fixture.
2. Successful construction does not establish service. Zero PC/native purchases remain explicit. The final food panel projection reports 2,064 evaluated offers: 864 closed/late, 650 no staffed counter, 550 service/queue, 0 purchases. These are observations, not a root-cause attribution. `runs/f7cold/results/*cold-FoodFeedbackOverview.json`.
3. F8h hand-back, refused-window receipt suppression and next-boundary reacquisition are verified; first sale and successful native service remain partial.
4. Price-only paused observations and same-minute/cold-load save coherence supply bounded evidence. They do not complete the broader E5b/F6b/F7/F9 matrices.
5. No performance window measured. Emitted observations below are provisional (PC shared), separate from whole-frame timing; no p95/gate verdict.

### Observed component timings

Every captured emitted PC observation >=30 ms is inventoried below. Interval maxima are not unique-spike counts. Full logs: `<stage>/runs/<flow>/Player.log`; compact inventory `<stage>/component-observations.json`, SHA-256 `255efb29049fd54511d0dda09ffcc9fadafc9c3b2f3feb43968f410a47937f42`. Whole-frame timing was not measured.

- `creation` line 1611, preparation_ms: **99.433 ms** (frame 6167).
- `f7cold` line 1719, preparation_ms: **167.853 ms** (frame 1690).
- `firstsale` line 2807, preparation_ms: **118.342 ms** (frame 6661).
- `firstsale2` line 1746, preparation_ms: **97.190 ms** (frame 4306).
- `firstsale2` line 1771, closure_ms: **76.328 ms** (frame 4363).
- `firstsale2` line 1930, advancement_interval_max: **157.515 ms**.
- `firstsale2` line 3402, preparation_ms: **152.751 ms** (frame 13800).
- `foreignshop` line 1735, preparation_ms: **100.097 ms** (frame 4396).
- `foreignshop` line 1757, closure_ms: **36.214 ms** (frame 4451).
- `foreignshop` line 2945, preparation_ms: **132.429 ms** (frame 5608).
- `foreignshop` line 4296, preparation_ms: **130.516 ms** (frame 7286).
- `foreignshop` line 4325, closure_ms: **49.809 ms** (frame 7344).
- `mixed` line 1600, preparation_ms: **98.016 ms** (frame 5111).
- `mixed` line 1601, closure_ms: **70.065 ms** (frame 5165).
- `remaining` line 1670, preparation_ms: **176.310 ms** (frame 6680).
- `remaining` line 1847, advancement_interval_max: **33.825 ms**.
- `shop` line 1552, preparation_ms: **118.606 ms** (frame 4804).

## NOT RUN grouped by missing capability or fixture

| Prerequisite | Rows / disposition |
|---|---|
| Actual paid/Pending service at this rental | First-sale and successful native-service assertions; E5b native transaction/accounting; F7 paid/Pending/interruption arms. Stock/staff readback and zero activity do not satisfy them. |
| Named original-three / mixed multi-shop / eight-nine-offer fixtures; in-place mutation and independent traces | E5b2–5 structural, repair, held-policy, removal/return, visual/performance rows. Only one two-offer coffee shop established. |
| FT-F6b-RivalLimits rush/paired controls and reviewed config keys | F6b-1/4/6/7/8 and remaining calibration/no-write assertions. pcstate, ledger and rival_fingerprint are installed; RivalServiceLanes/RivalMealPeriodUnits variants are not allowlisted. |
| RIG-ACCEPT / same-head full artifact / paired F9 controls | Formal F9 provenance/status/summary and base comparisons. No offline full suite was run; local unsealed setup is not formal acceptance. |
| PhysicalEnabled/config/price-weight readback; vendor/dialog fixture | F9 bodies-off, loaded-weight price response and agency/crew-truck rows. PhysicalEnabled absent from config allowlist; Street Food vendor API missing. No campaign bought. |
| RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY | NOT RUN: all reserved-step flows, including F4 G1/G3/G4/G5/G6/G7/G8/G11/G13 and G2 presentation-trace half; known pre-lease validation barrier, no real-input fallback. |
| ft-f4-stress10k / pc-stress-10k@1 / city-50k@1 | NOT RUN: F4 G15, F7-6, 10k/50k half of F7-7; blocked fixtures. |
| Excluded RIG-FAULT / RIG-PCFIX / visitor-body / historical fixtures | NOT RUN: F8a-6..8, F8bg-5..7, F7-4/5, F5i visitor rows and old/future-schema cases. Only fresh schema-one FT cities. |
| Unsealed official manifests / unbuilt slices | No FT-F4-Declared/FT-F9 sealing, official F4f verdict or F7e implementation/check 8. |

F7-2 / F4 G12, F7-7/9 from #211 and reload-epoch 3–4 from 11c were not repeated. Foreign install/remove was repeated only because shop assertions required this fresh cycle.

## Tooling gaps

| Capability / stage action | Exact error or observed gap | Flow | Blocked assertions / disposition |
|---|---|---|---|
| shop_fixture recipe usability | Build/status complete; PC image `MissingDefinition` on initial one-offer recipe; native attempted orders `unavailable`. No native ops error code returned. | shop / firstsale / foreignshop | First sale, successful native service and meaningful throughput not established. Documented same-shop multi-offer reconciliation passed the image, but sales stayed zero. Cause unresolved; no further workaround. |
| Stage target waiter | Accepted stale `paused:true`, day 1 08:00 after requesting day 2 10:00; no native op error. | firstsale | Invalid exact-window setup retained. Corrected waiter required exact day/time/paused, then firstsale2 ran as specified; zero sales still unproved. |
| FT ui escape (optional stage step) | `Nothing is open to close (Esc would open the pause menu).` | firstsale | No check blocked. No-op refusal retained; redundant escape omitted from later stage waits. |
| Stage capability business argument | `No scoped food business at ba:street_fourthavenue#24.` | shop / firstsale / firstsale2 | Native id used for a PC place query; later canonical `address:ba%3Astreet_fourthavenue:24:shop` query succeeded. Ownership verdicts use actual boundary readbacks, not this warning. |
| Stage result filename | Colon in native business label used a Windows alternate stream; no native error. | shop / firstsale | Raw transport evidence intact; copied to safe filenames. `<stage>/stage-filename-correction.json`. No verdict lost. |
| Required config/readback gaps | No reviewed RivalServiceLanes/RivalMealPeriodUnits/PhysicalEnabled keys; no captured ServiceEvidence field; RIG-ACCEPT absent. | remaining | Corresponding variants, final label field and formal acceptance rows remain unrun. No unknown op or reflection capability invented. |

Both native build/reconcile calls completed. No `ops_fixture_unknown`, `ops_city_required` or other failed native op occurred in this run. Native service absence is not relabeled as an op success proving a sale.

## Fixtures

Only the fresh native character created at this candidate was loaded. All 15 disposable saves remain in place; all 15 recorded creation/manual-save SHA-256 values reverified after stop. Full inventory `<stage>/fixtures.json`, verification `<stage>/fixture-hash-verification.json`.

| Name | Absolute path | SHA-256 |
|---|---|---|
| `FT E5b PC 11 Nov d 86c2e92f Ready` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT E5b PC 11 Nov d 86c2e92f Ready.hsg` | `574df5f88bc641653b37b8488d040d65abc6fc602133576300c04b9d5c7b3fad` |
| `FT E5b PC 11 Nov d 86c2e92f` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT E5b PC 11 Nov d 86c2e92f.hsg` | `225de5c7d97cba4dc507493ea96a4678a0b7236a5e86ca16a565142e9420a1b1` |
| `FT PC 11 Nov d f7cold F7 Cold Reload Verified` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d f7cold F7 Cold Reload Verified.hsg` | `e9fd60692894bf070a2d5626066d108096cf0134f92decadd9ec02f9b037dc7e` |
| `FT PC 11 Nov d firstsale Before Normal Load` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d firstsale Before Normal Load.hsg` | `a03341b8a8afc79cedf1f9396010bf763c9c9e2e3cac6334dc6960637ccff8b7` |
| `FT PC 11 Nov d firstsale First Sale Window` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d firstsale First Sale Window.hsg` | `841c141c5572674a002a4069d616852c6f97e22fe15011216da9f9ee8a2828a0` |
| `FT PC 11 Nov d firstsale2 Before Normal Load` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d firstsale2 Before Normal Load.hsg` | `51666ab565d3cd333e3d8dc7ee2e3ab7fcc4060ac94aacc66529172f7ea1ef20` |
| `FT PC 11 Nov d firstsale2 First Sale Window` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d firstsale2 First Sale Window.hsg` | `a920f55ccd19ba8006156e3c9881d2c413f35309e1044cdcd8bcb64bdbfb5eda` |
| `FT PC 11 Nov d foreignshop Before Foreign Shop Cycle` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d foreignshop Before Foreign Shop Cycle.hsg` | `9d8a428cfafc3d036ca5bfff10ed546a21423ed8ac4cab1207f760aa058b6b3a` |
| `FT PC 11 Nov d foreignshop Foreign Shop Reacquired` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d foreignshop Foreign Shop Reacquired.hsg` | `0dadc3089198890f9edda5f79aaac24a75b1c401955c6f7418711ec09606c352` |
| `FT PC 11 Nov d foreignshop Foreign Shop Served` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d foreignshop Foreign Shop Served.hsg` | `0ef81806afa9ecf5024bfa4b22bce8e577fe6256430a7cb871dc7df10158ef5b` |
| `FT PC 11 Nov d mixed Documented Mixed Shop` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d mixed Documented Mixed Shop.hsg` | `1738521e647cfd419f379f0e154d92897656f7338c2f2f55aec24b280949dad5` |
| `FT PC 11 Nov d remaining F7 Same Minute A` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d remaining F7 Same Minute A.hsg` | `a2c358ce56c3b97353078c33ccaf9611356dcfe91cae493f5a3e0147ccf17f1a` |
| `FT PC 11 Nov d remaining F7 Same Minute B` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d remaining F7 Same Minute B.hsg` | `d51f6e3c5a685b56ebdaa3eae2b52cc6e4be0f09f7c84c86bb87dea7e77faf75` |
| `FT PC 11 Nov d remaining f9-city-ready` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d remaining f9-city-ready.hsg` | `5386b993ccff2aa7bc6210d51555593f14dcf089a126956b96b7310f20f99cb3` |
| `FT PC 11 Nov d shop Owned Staffed Food Shop` | `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/7GtI09kH1Uqnnwoy22MA+A==/FT PC 11 Nov d shop Owned Staffed Food Shop.hsg` | `c298933d9c9c5020600912480f504e37d17fa136408c1b80bd9a8d3bb2b39ae7` |

## Per-check results

| Check | Result | Evidence / remaining prerequisite |
|---|---|---|
| `shop-fixture-selection` | PASS | Funded native size-C selection completed: 24 Fourth Avenue, Hell?s Kitchen, C2, 225 m?, staffed and stocked. |
| `e5b-complete-menu-2` | PARTIAL | Native croissant/donut mixed setup, equipment/staff/stock/prices and loaded donut holder link verified. Product/UI visual clearance and larger menu cases unrun. |
| `e5b-complete-menu-3` | NOT RUN | Needs actual paid shop transactions plus independent PC receipt/authority and producer/resource/order delta evidence. Natural windows produced none. |
| `e5b-complete-menu-4` | NOT RUN | Needs notification/publication/requested-boundary trace, structural/supply repair and native refill matrix. Price-only supporting reads are reported separately. |
| `e5b-complete-menu-5` | NOT RUN | Needs Street Food absent control transport and held/name/receipt semantic comparator; FT bridge cannot be removed as that control transport. |
| `e5b-complete-menu-6` | NOT RUN | Needs matched idle/breakfast/lunch/evening/soak, dirty/recovery and 0/8+ shop fixtures and independent noninterference controls; no timing window measured. |
| `e5b2-3` | NOT RUN | Needs owned burger/hot-dog structural-edit fixture and add-holder/add-price/change-type operations. The two-offer coffee shop and price edit do not supply that matrix. |
| `e5b2-4` | NOT RUN | Needs matched 0/8-shop noninterference/performance windows and controls; not supplied by one coffee shop. |
| `e5b3-2` | NOT RUN | Needs the named burger/hot-dog native-method storm (zero stock, bag/holder edits and hours). Price-only two-offer supporting observation is outside that complete setup. |
| `e5b3-3` | NOT RUN | Needs RIG-SHOPEDIT in-place writes and four acquiring shops; no such mutation capability was invented. |
| `e5b3-4` | NOT RUN | Needs loaded EmployeeWorkstation fixture/capture or complete negative catalogue search for that original-three setup. Bakery-showcase metadata is not that search. |
| `e5b3-5` | NOT RUN | Needs 8 repaired, then 64 owned + 8 acquiring shops and in-place repair mode; no such fixture built. |
| `e5b3-6` | NOT RUN | Needs 0/8/64-shop storm/recovery and matched ordinary performance/noninterference controls; no window measured. |
| `e5b4-4` | NOT RUN | Needs burger/hot-dog original-three metadata override/control fixture. This coffee shop has croissant/donut; no substitute menu was assumed. |
| `e5b4-5` | NOT RUN | Needs observed held dynamic tuples and metadata override replay checkpoint; none established. |
| `e5b4-6` | NOT RUN | Needs matched 0/8 original-three shops and historical base performance controls; no such windows. |
| `e5b5-2` | PARTIAL | Same shop acquired with croissant/donut; catalogue and native offers verified. Zero purchases/receipts; positive checkout/revenue and Street Food building-placeability assertions unproved. |
| `e5b5-3` | PARTIAL | Paused donut price $4→$6→$4 confirmed; desired/managed stayed true. This two-offer subset proves no in-flight checkout, eight/nine-offer menu or visual clearance assertion. |
| `e5b5-4` | NOT RUN | Needs product-provider removal/return control and boundary trace; vanilla donut is not a removable mod provider, and Street Food transport remains blocked. |
| `e5b5-5` | NOT RUN | Needs observed held dynamic decision and changed metadata/reload control; none established. |
| `e5b5-6` | NOT RUN | Needs 0/8/64 eight-offer fixtures and recovery-wave performance controls; no such windows. |
| `f6b-1` | NOT RUN | Needs FT-F6b-RivalLimits rush fixture and reviewed RivalServiceLanes/RivalMealPeriodUnits config variants; keys absent from installed allowlist. |
| `f6b-2` | PARTIAL | Native staffed-shop snapshots at 10/11/12/13 and bounded log collected, but purchases/revenue remained zero. No representative throughput band or calibration verdict. |
| `f6b-3` | PARTIAL | Core ServiceLimits/SellOut/Availability/NativeRivalCheckout labels match. 25 modeled rival choices/settlements observed; captured ServiceEvidence decision field unavailable, so that assertion is unrun. |
| `f6b-4` | NOT RUN | Needs matched default/tight rival-full controls, actual owned-shop purchases and supported lane config arm; not established. |
| `f6b-5` | PARTIAL | One intraday rival fingerprint pair is identical across this lunch observation. Tight-profile/full-rival experiments 1/4 did not run; their no-write assertions remain unrun. |
| `f6b-6` | NOT RUN | Needs real mid-queue/mid-service ServiceAt checkpoint and unreloaded control. Load-place counts are not that named state/readback. |
| `f6b-7` | NOT RUN | Needs check-6 checkpoint plus supported RivalMealPeriodUnits reload variant; key absent from installed allowlist. |
| `f6b-8` | NOT RUN | Needs valid staffed rush, matched pre-F6b control and measured union/whole-frame window. No such performance window was measured. |
| `f9a-1` | NOT RUN | Needs RIG-ACCEPT and same-head full offline acceptance/provenance artifact. Build/DLL hashes alone do not complete this formal row. |
| `f9a-2` | PARTIAL | Local unsealed day-3 07:30 checkpoint: city running, 957 residents/1,914 visitors at targets, PC-managed shop. No selected shop decision/purchase; full ready-fixture verdict unproved. |
| `f9a-3` | NOT RUN | Needs RIG-ACCEPT native-stage record/status set. Capability observations retained; no formal acceptance pass synthesized. |
| `f9a-4` | NOT RUN | Needs fully ready shop checkpoint and paired seven-day daily-reload/uninterrupted native/PC controls; no meaningful positive shop baseline established. |
| `f9a-5` | NOT RUN | Needs paired timestamped edits across 30/60/120 presentation/cadence arms and skip controls; background-only run supplies none. |
| `f9a-6` | NOT RUN | Needs mid-leg physical fixture, native enter/leave/skip controls and supported PhysicalEnabled=false arm; flag absent from installed allowlist. |
| `f9a-7` | NOT RUN | Needs agency dialog/crew-truck fixture and working Street Food vendor API; live capability reports API missing. No campaign bought. |
| `f9a-8` | NOT RUN | Needs historical-schema FT loader/fixture; excluded. Only fresh schema-one cities loaded. |
| `f9a-9` | NOT RUN | Needs loaded FoodChoicePriceWeight readback and paired two-day raised/control prices on a serving shop; not established. |
| `f9a-10` | NOT RUN | Needs RIG-ACCEPT, same-head full acceptance artifact and required native/F4 records. No offline suite or summary run. |
| `f9bdf-1` | NOT RUN | Needs RIG-ACCEPT and matching same-head full acceptance/provenance artifact; installed DLL hash retained only. |
| `f9bdf-2` | NOT RUN | Needs mid-leg fixture and PhysicalEnabled=false control; flag absent from installed allowlist. |
| `f9bdf-3` | NOT RUN | Needs RIG-ACCEPT plus full/native/F4 records; no summary verdict synthesized. |
| `f9bdf-4` | NOT RUN | Needs RIG-ACCEPT native record; raw capability observations retained separately. |
| `f9c-1` | PARTIAL | Same local f9-city-ready subset as f9a-2; no official manifest sealed and no shop purchase/selected decision proved. |
| `f9c-2` | NOT RUN | Needs complete serving f9-city-ready and paired seven-day daily-reload/uninterrupted controls; unsealed local subset is insufficient. |
| `f9c-3` | NOT RUN | Needs paired frame-cap/cadence/skip controls with timestamped edits; no presentation/cadence arms in background mode. |
| `f9c-4` | NOT RUN | Needs mid-leg physical fixture and supported PhysicalEnabled=false arm; missing allowlist key. |
| `f9c-5` | NOT RUN | Needs agency dialog/crew-truck fixture and working Street Food vendor API; API missing, no campaign bought. |
| `f9c-6` | NOT RUN | Needs historical-schema FT fixture/loader; excluded. |
| `f9e-1` | NOT RUN | Needs RIG-ACCEPT; raw active/inactive capabilities and AccountingDiagnostics retained without formal verdict. |
| `f9e-2` | NOT RUN | Needs paired head/base diagnostic and kept research-order fixture plus RIG-ACCEPT; not established. |
| `f9e-3` | NOT RUN | Needs paired head/base panel-page observations and supported Ctrl+F8 input; no foreground/real-input fallback. |
| `f9e-4` | NOT RUN | Needs paired head/base resident/body inspector fixture and supported input; not established. |
| `f9e-5` | NOT RUN | Needs paired base census/save-cost comparison and RIG-ACCEPT; no separate save-profiling run performed. |
| `f9e-6` | NOT RUN | Needs paired observer/presentation controls and panel input/performance windows; reserved presentation steps excluded. |
| `f9g-1` | NOT RUN | Needs mid-leg/bodies-off controls; PhysicalEnabled flag absent from installed allowlist. |
| `f9g-2` | NOT RUN | Needs paired head/base deterministic body-selection fixture and RIG-ACCEPT; native handles/status alone are insufficient. |
| `f9g-3` | NOT RUN | Needs opening-queue plus published vendor-stand fixture, paired body counts and RIG-ACCEPT; vendor API missing. |
| `f9g-4` | NOT RUN | Needs paired head/base presentation performance windows; reserved steps excluded and no window measured. |
| `f9h-1` | NOT RUN | Needs RIG-ACCEPT and matching same-head full acceptance/provenance artifact. |
| `f9h-2` | NOT RUN | Needs loaded price-weight readback and paired serving-shop two-day price controls. |
| `f9h-3` | NOT RUN | Needs RIG-ACCEPT, matching full acceptance and all native/F4 records; no summary run. |
| `f7-1` | PARTIAL | Same-minute payloads identical, success epochs 12/13 with matching lengths; fresh-process load runs at saved minute with no stop key/refusal. Paid-sale/Pending/midnight/relocation/control arms unrun. |
| `f7-3` | NOT RUN | Needs actual receipt_pending / interrupted-state FT F7 food shop fixture. Template labels do not produce the state; no synthetic Pending, kill or fault arm. |
| `f8h-1` | PARTIAL | Shop hand-back and next-boundary reacquisition/Ready verified with no new refused-window receipt. Native window: 4 visits, 0 purchases, unavailable; successful native service unproved. |
| `f8h-2` | PARTIAL | Normal load: checkout active, 49 targets (38 patched), zero refusing, no load-wait checkout refusal. Exact day-2 10:00–12:00 and 12:00–13:00 extension produced no purchase/receipt; first sale unproved. |
| `f8a-11` | PARTIAL | Same shop-specific foreign-prefix evidence as F8h-1. Ownership transitions and no new PC receipt verified; successful native sale unproved. |
| `f8a-5 native-shop half` | PARTIAL | RLO-absent startup refuses with required rlo.donor wording; live PC key hashes unchanged. 16:00–17:00 native log has 0 visits/purchases; native service unproved. |
| `f8bg-4 native-shop half` | PARTIAL | Same RLO-absent shop observation as F8a-5: preserved PC keys/no new receipt, zero native activity; successful native service unproved. |

## Cleanup

Restored **this run's post-install backup**, not a rollback revision. All **301 protected entries** match SHA-256/absence, and the isolated HKCU key matches exactly. Install/overlay, ModsLocal, PlayerSettings, persistent-city.json, launcher/run state and original autosave state verified. All owned games closed normally; no kill was needed. Final agent1 stopped, lease free, pending=0. All nine recorded owned game PIDs are gone; no isolated player/helper remains. Every foreground command was awaited, no detached job launched. Fixtures and raw evidence remain. `<stage>/restore-verification.json`, `final-verification.json`, `owned-process-verification.json`.
