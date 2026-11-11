# Rig results — 11 November 2026 (f), Mac (#72 serving-shop smoke + first native sale checks)

Role `rig_test`, where `mac`, isolated instance `agentmac`. Candidate **858b7a54c7b1df49da8befaf996d7a48d11b9506**, verified with `git fetch origin` and `git rev-parse origin/codex/persistent-city` before build; tip did not move from the requested candidate. Schema held at 1. Testing only; no production change, release, version/changelog, merge or issue creation.

**Summary: 2 passed / 3 partial / 1 failed / 66 not run. Result: BLOCKED.** Counts cover the 72 per-check rows below, including two setup passes and the failed serving-readiness prerequisite. Grouped exclusions outside this targeted inventory are not added again. Existing passes from other runs are retained.

**The Mac recipe rents, but #72 does not establish a serving Mac shop.** Selection picked **7 Fourth Street, Midtown, C1, 225 sqm**, business `ba:street_fourthstreet#7`. Readiness is false with exact reasons **`empty_cargo_entry`, `no_staffed_counter`**, unchanged after normal load. PC refuses its food image with **`MissingDefinition`**. No sales window was started after that diagnostic; first native sale remains unproved.

`<stage>` is `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-11f-mac`. Raw logs, JSON, save extraction, tooling and backups remain there, outside Git. Prior procedure read from [Mac e at its published commit](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a76b197ddce32def329eb3e34b25c00a01ead318/docs/checks/rig-2026-11-11e-mac-results.md), Mac b's remote branch and [PC d's open evidence branch](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-rig-2026-11-11d/docs/checks/rig-2026-11-11d-pc-results.md). None of their worktrees/stages was changed. Older E5/F6/F7/F9 check text was read from `docs-history-2026-11-10`; F8h/F9bdf/g/h from the preserved Mac-e branch. Deleted check docs were not recreated.

## Build and rig

| Item | Value |
|---|---|
| Candidate / worktrees | Detached `rig-2026-11-11f-mac`; separate `codex/mac-rig-2026-11-11f` evidence worktree, both from verified `858b7a5`. Main worktree unchanged. |
| Host / player | Native Apple Silicon, private `Big Ambitions Agent Tests Mac.app`, isolated `com.hovgaardgames.bigambitions.agenttests.mac`; loopback 28330, no tunnel; runtime 7. |
| Official rig checkout | **10cf7d873a733ac564a84d95cc23b4213c101d7f**, clean before/after; read only. No install/update/rollback script used. |
| Rollback retained, unused | `$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261111T152017Z`; older rollback also untouched. |
| Build | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; succeeded. Same compile-reference location as Mac e. |
| Environment | Every Python/.NET/build command used `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. |
| DLL SHA-256 | `de1f287842d0f28d890aaaebd1cead3877ac4e3e33bbfe00be4a74f292678d90`; installed hash matched. |
| Provenance | Mac reference fingerprint `unknown`, 27 differing assemblies. No same-head full offline acceptance artifact; no Windows compatibility claim. Build only, no full suite/regression suite or production test change. |
| Modpack | 29 DLL paths match Mac full-modpack owner record. Only candidate PC DLL and preinstalled FoodTrucks `Dependencies/BridgeControl.dll` differ from that historical record. `modpack-summary.json` and `modpack-live.json`. No shop-log/foreign patch added. |
| Rig ops addon | `ready`, agentmac; SHA-256 `0d5dd61fbbfad472858ae1182767b216071b86b22fe778f8caa0ac18da138175`. |
| Lease | Free/stopped initially; owner `pc-mac-rig-2026-11-11f`, purpose `Persistent City 11 Nov Mac rig f`, same owner throughout. No token printed/staged/passed in arguments. |
| Mode / resources | One headless launch, `--rig-test`; immediately before start: 79.6 GiB free disk / 70% reported free memory. |
| Backup / installation | 260 original files copied and SHA-256 manifested before installing candidate DLL, English locale and PC config: isolated ModsLocal, profile top-level preferences, native preference plist, install receipt. Only isolated mod/config/receipt files changed. |
| PC config | Enabled/PhysicalEnabled true, 48 pedestrians, 24 vehicles, radius 120 m; remaining compiled defaults. No rival config/finance mutation. |
| Original autosave | Original isolated plist has no autosave keys. Preferences not edited to suppress saves; process-only `--rig-test` guard used. Suppression true before creation/recipe and after completed reload and at finish. |
| Cleanup | Normal stop; 260/260 hashes match, original autosave plist restored; lease free, pending 0; owned PID 69482 and matching caffeinate gone. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| Fresh creation | `FT-PC-Mac-20261111f-858b7a5`, full modpack at candidate | Creation done; pause, autosave readback, capability reads. |
| Funded Mac rental | Selection-based Mac recipe, checkpoint `FT Mac 11f F8h Food v1` | Rental PASS; readiness FAIL. |
| Required independent diagnostic | `shop_state` before any clock advance | `ready=false`, `empty_cargo_entry`, `no_staffed_counter`; no sale wait. |
| Normal native menu/load | Load only this run's new shop checkpoint | Suppression true; same readiness failure; settled city running, checkout active, 49/49 and 0 refusing. |
| Later independent reads | Capabilities/status, PC boundary, RivalFoodDiagnostics, native pcsave extraction | Supporting observations only; dependent rows remain NOT RUN. |
| Cleanup | Stop → restore/hash → release → final status | Complete; no force stop or rollback. |

## Shop rental and selection

Commands ran from the official rig checkout through a stage-only foreground runner. Exact argv, exits and UTC times are in `<stage>/commands.jsonl`:

```sh
export BA_LOCK_OWNER="pc-mac-rig-2026-11-11f"
python3 scripts/mac-agent status
python3 scripts/mac-agent resources
python3 scripts/mac-agent lock acquire --purpose "Persistent City 11 Nov Mac rig f"
python3 scripts/mac-agent resources
python3 scripts/mac-agent start --mode headless --rig-test
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call new_game '{"name":"FT-PC-Mac-20261111f-858b7a5"}'
# Wait for this named creation: scene city, load.status done.
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call time '{"pause":true}'
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
```

`recipe.json` copies official `flows/mac/f8h-food-shop.json`, with only run-unique id/name/checkpoint and the documented `"selection":{"building_size":"ba:buildingsize_c"}` override. `spec_from` remains `flows/fixtures/shop-medium.json`; no pinned address, equipment/staff/stock workaround or money beyond the documented $100M step. Recipe SHA-256 `093b918ad4f42cdc0e86bb54f0077c05f248c2c4b3bbfa31aee449257bc2df5a`.

Recipe exit 0; `money_set=100000000` and its internal set/readback gate precede rental. Native saved money is $99,939,250. Actual choice: **7 Fourth Street**, `ba:street_fourthstreet#7`, `ba:neighborhood_midtown`, `ba:buildingsize_c`, building version **1**, **225 sqm**. Coffee shop `FT Mac 11f F8h Food`: `rented=true`, `owned=false` (native lease, not real-estate purchase), rent $1,008/day, deposit $60,480. PC management is a separate authority fact and is false.

Equipment: cash register, two bakery showcases, storage and cleaning station. One available customer-service employee has seven 09:00–18:00 `Default` shifts, $25/hour, customer-service readback 50. Croissant stock 100 at $5; register bags 300. Initial and post-load revenue $0 at day 1 08:00. Rental completion is not proof of serving readiness.

## Required readiness diagnostic

**FAIL — not ready**, before any sales wait and again after normal load. Exact command, both times exit **0**, stderr empty:

```sh
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthstreet#7"}'
```

Short excerpt from `shop-readiness.json` (same SHA-256 after reload):

```json
{"ready":false,"reasons":["empty_cargo_entry","no_staffed_counter"],"staff":1,"staffed_registers":[],"empty_cargo_holders":["LP8DekYvUEavmJ81UTw7bQ=="]}
```

The second bakery showcase has cargo `item:null, units:0`. The croissant offer itself reports ready, 100 units, $5, no offer reasons. The register is a point of sale with 300 bags but native `serving.available=false`; the employee reports `available=true` and seven Default shifts, yet all seven readiness hours have zero staffed counters. The diagnostic does not identify why the native register is unavailable; no cause beyond those facts is assumed.

The relevant #72 change is in `flows/fixtures/pc-food-shop.json`: its one-offer shop has one showcase. The official **Mac** recipe still inherits two showcases from `shop-medium.json`. That unchanged Mac input explains the observed empty cargo row. No removal/refill/rebuild workaround was invented. Readiness correctly exposes the blocker; the readiness op itself did not fail.

PC's `pc food contract boundary` before and after load reports `phase=Refused`, `refusal=MissingDefinition`, `desired=false`, `managed=false`, `nextBoundary=native`, at hour 32. These are PC food-image observations, separate from rig readiness reasons.

## F8h first sale and check-1 shop assertions

**F8h-2 PARTIAL.** Normal native `load {"name":"FT Mac 11f F8h Food v1"}` completed. After named-load completion, autosave suppression was verified. The first capability snapshot was `start ready`; a later settled read reports **city running**, **`food.checkout.shop optional active`**, and **49/49 targets, 0 refusing food.checkout.shop**. Shared tick owners remain report-only. Activation scans are 14.390 ms at creation and 7.725 ms after reload.

There are zero `Food checkout waits for the city load to finish` strings in the captured player log. **This does not prove a successful first checkout:** `nativePurchasesThisSession=0`, revenue $0, no PC receipt key in the saved extraction. The requested first-sale window is NOT RUN because readiness is false; no clock advance or shop-log patch was used.

**F8h-1 shop assertions PARTIAL.** Current-run readbacks establish the shop is unready, image-refused and not PC-managed. Successful native service, receipts during a foreign-refused window and next-boundary reacquisition are NOT RUN. The foreign-prefix install/remove proof from PC 11c/11d was not repeated; this run does not claim that cycle occurred on Mac. F8a-5/F8bg-4 native-shop halves likewise remain NOT RUN: no ready shop, no duplicate RLO toggle.

## E5b2–5 and complete-menu shop rows

**All listed shop rows NOT RUN**, with row-specific prerequisites below. The prescribed recipe did not supply a ready PC-owned shop. No original-three burger/hot-dog storm, mixed eight/nine-offer shop, in-place write, catalogue lifecycle, mod removal/return or 0/8/64-shop performance fixture was substituted.

Independent supporting reads succeeded: food-contract capabilities passed, 23/23 dynamic hooks, active coordinator, published product policy, catalogue revision 1/count 800/refusal None; shop phase still Refused/MissingDefinition. These observations do not pass the required sale/mutation matrices. No missing SFAPI/catalog capability was invented.

## F9, F6b and F7 sale parts

**F9 NOT RUN.** No `f9-city-ready` checkpoint was produced after readiness failed. The live ops list has no RIG-ACCEPT, the Mac assembly fingerprint is unknown and no same-head full acceptance artifact exists. No official manifest sealing attempted. Raw capability/status/accounting reads are supporting evidence only: RLO donor/clock/research/physical active, Street Food vendor API missing, interior view off by default. Day-1 population is 957 residents/957 visitors; no day-3 target verdict or selected shop decision is claimed. Bodies-off/config, paired head/base, cadence, research/truck and price-control rows retain their separate prerequisites.

**F6b-3 PARTIAL.** `mac-agent op pcstate '{"action":"read","property":"RivalFoodDiagnostics"}'` succeeds. Core labels match exactly: `ServiceLimits="estimated (modeled service limits)"`, `SellOut="modeled sell-out (estimated)"`, `Availability="EstimatedFromPriceAndHours"`, `NativeRivalCheckout="Unsupported"`. No captured rival `ServiceEvidence` assertion was reached. No FT-F6b-RivalLimits rush/calibration/control fixture was established; no purchases or MealsPerHour band inferred. Reviewed rig config allowlist omits `RivalServiceLanes` and `RivalMealPeriodUnits`; those variants and dependent no-write experiments are NOT RUN. No rival write occurred.

**F7-1 and F7-3 sale parts NOT RUN.** No natural paid sale or real Pending receipt, so no `FT F7 food shop` receipt_pending arm exists. A checkpoint label alone does not create that state. Native `pcsave take` successfully extracts the new shop checkpoint: schema 1, success epoch v1/sequence 1/minute 1920, payload length 1,157,311 characters, authority desired=false, no receipt key and no accounting-stop key. This is supporting extraction/reload evidence, not paid-sale epoch coherence or interrupted-state proof. Checks 2/7/9 were not repeated; 4/5/6/8 excluded as instructed.

**S2 omitted / NOT RUN:** no existing rig brief; it is the planned R0 code slice in `docs/plans/next-tracks.md`. No NativeSaleProbe implemented or improvised.

## Performance

**Provisional:** concurrent other-project harness activity was not excluded. Both named live harness sessions were left untouched. No measured gameplay/mod-work union window, comparison or save profiling was run; the p95 <5 ms gate is not assessed. Whole-frame timings are separate and not used as PC mod-work.

All emitted PC slow-routing samples at least 30 ms in `player-final.log`:

| Line / frame | Component | ms |
|---|---|---:|
| 1483 / 2932 | preparation | 121.926 |
| 2309 / 4837 | preparation | 120.684 |
| 2316 / 4890 | preparation | 32.913 |

Aggregate maxima at least 30 ms, not additional enumerated events: creation advance 128.145 ms/unit 127.971 ms, scene search 115.623 ms; reload advance/unit 129.699 ms, scene search 118.154 ms. Discovery-sweep work totals 168.847/167.726 ms span frames and are not frame spikes. Dynamic-contract unit p95 3.8693 ms across 18 units is not combined mod-work p95. Startup/load spikes are retained in `observed-spikes.json`; no before/after optimization claim.

## Findings

1. The funded size-C rental works on Mac at this candidate; native shop serving remains blocked.
2. #72 readiness is verified by rig as a diagnostic. The Mac inherited fixture still has the empty second showcase; the native register also reports unavailable. A ready result was not obtained.
3. Full-modpack checkout stays active after normal load, but no first sale or shop-dependent acceptance proof exists in this run.
4. Current PC correctly reports `MissingDefinition` and does not claim management of this malformed shop image. No service, receipt or economy pass is inferred.
5. All isolated files/settings were restored and the lease released before preparing the PR. Windows, normal Mac game/profile and unrelated sessions were not used.

## NOT RUN grouped

- **Readiness / first sale:** F8h first-sale and shop-service arms, F8a-5/F8bg-4 native-shop halves, E5b checkout/recovery rows, F9 ready-shop baseline, F6b throughput/control rows, F7 paid/Pending arms. Exact shared blocker: `empty_cargo_entry`, `no_staffed_counter`; PC image `MissingDefinition`.
- **Named shop/metadata fixtures:** original-three structural storms, mixed eight/nine-offer menus, in-place writes, 8/64-shop recovery and paired performance controls; a one-offer recipe supplies none of these.
- **Pending transport / configuration:** Street Food absent control and missing vendor API; reviewed RivalServiceLanes/RivalMealPeriodUnits/PhysicalEnabled/FoodChoicePriceWeight variants/readbacks; actual captured ServiceEvidence and receipt_pending state. Existing ops were not extended.
- **Formal acceptance / comparisons:** RIG-ACCEPT absent; same-head full offline artifact, matched head/base and seven-day/cadence controls not available; official manifests unsealed and left alone.
- **Explicit exclusions:** every reserved `present`, `foreground`, `priority` flow (RIG-PRESENT/FOREGROUND/PRIORITY); stress10k/city-50k, RIG-FAULT, RIG-PCFIX, historical-schema fixtures, official sealing, F4f/F7e implementation; pointer/hover/focus/click for Peter; Windows-only client/overlay assertions.
- **Previously covered / no duplicate:** F8h foreign-patch install/remove, F7 checks 2/7/9 and non-sale arms outside this brief. S2 has no current rig brief and is a code slice, not a runnable check.

## Tooling gaps

No native launcher/rig op returned a nonzero exit or JSON error. The prerequisite failure and two stage/console errors are retained exactly rather than treated as sales proof:

| Capability / exact command | Exit / exact error or diagnostic | Flow / blocked checks |
|---|---|---|
| Mac recipe: `python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60` | Exit 0, stderr empty; recipe completes but readiness reasons `empty_cargo_entry`, `no_staffed_counter`. Official Mac source inherits two showcases for one product. | Rental / all positive serving-shop and first-sale arms. No repair attempted. |
| Required readiness: `python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthstreet#7"}'` | Exit 0, stderr empty; `ready:false` with the same two exact reasons before/after reload. | First-sale prerequisite failure, not an op transport error. |
| PC image: `python3 scripts/mac-agent call console '{"line":"pc food contract boundary"}'` | Exit 0; `phase:Refused`, `refusal:MissingDefinition`, `desired:false`, `managed:false`. | PC-owned shop assertions. |
| Stage's obsolete diagnostic: `python3 scripts/mac-agent call console '{"line":"pc food status"}'` | Exit 0, stderr empty; output `Warning: [Persistent City] Unknown command. Use: pc population census [workplaces] \| pc population person <id> \| pc population manifest` | Inspection only; no check blocked. Supported food-contract and pcstate reads were retained. |
| Stage runner: `python3 reload.py` (from stage) | Exit 1 after all native reload/read commands completed: `NameError: name 'Path' is not defined`, line 18, log-copy call. Preceding stage sed import insertion also refused `command i expects \ followed by text`. | Evidence-copy tail only; no native mutation replayed. `finish_reads.py` collected settled reads and copied the player log; both raw runner logs retained. |
| Formal / config capability inventory | No `accept`/RIG-ACCEPT op in `op ops`; reviewed `scripts/rig_metadata.py:PC_CONFIG_KEYS` omits RivalServiceLanes/RivalMealPeriodUnits/PhysicalEnabled/FoodChoicePriceWeight. No unsupported op dispatched. | Formal F9 and config variants NOT RUN. |

Exact argv/exits for all bridge/rig calls are in `commands.jsonl`; raw responses stay in stage. Loading was waited by matching the requested save name, city scene and completed load; PC activation was then separately waited to reach city running. Initial `start ready` was not treated as the final capability verdict.

## Fixtures

One fresh native FT character created at candidate head, only in the isolated profile. Only its own new checkpoint was loaded. Both saves remain. Creation save has no PC payload; shop checkpoint has schema 1. No preexisting/real save loaded, copied, modified or deleted; no campaign or rival-finance write.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT-PC-Mac-20261111f-858b7a5` | `b239b3f3b1503987d42678986d974d98bcbd0f76cfd07f79fddae6c62a7e1163` | 505293 |
| `FT Mac 11f F8h Food v1` | `72655c3d8d3a4782b24218098ea35662d3a34727b8c74c395f0740e194721dac` | 863559 |

Shop business id **`ba:street_fourthstreet#7`**; PC place `address:ba%3Astreet_fourthstreet:7:shop`. Full absolute fixture paths and post-stop hashes are in `<stage>/fixtures.json`. No f9-city-ready, FT-F6b-RivalLimits or FT F7 Pending fixture was created.

## Evidence archive

| Stage file | SHA-256 |
|---|---|
| `shop-readiness.json` / identical `readiness-after-reload.json` | `cdb611d8f088404993e63ac330351dbb70a5cd89debf279c757ca52b4d76032e` |
| `player-final.log` | `ed358070c2331c335d678834bcd82d62996df393e68454a52c79c7049c219064` |
| `commands.jsonl` | `61cd5ddb244c7d5eb0b9baa10f2a44b60469adab451e16ed3146d2cf217a023a` |
| `native-pcsave-extraction.json` | `b1ee5d144a6e7ed7a93a2adebe9c8600a9c6ed019459af49a40dc211260ab02b` |

Also retained: `build.log`, `build-provenance.json`, `recipe.json`, source recipe snapshots, `check-results.json`, `save-summary.json`, hook/capability/diagnostic replies, backups/manifest, `restore.json`, `status-final.json`, `owned-process-verification.json`. Raw JSON, binaries and saves are not committed. A report copy is at `$HOME/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-11f-mac.md`.

## Per-check results

| Check | Result | Evidence / remaining prerequisite |
|---|---|---|
| `mac-autosave` | PASS | Native --rig-test suppression true at menu, creation, rental, completed reload and final readback; original preferences restored. |
| `shop-fixture-selection` | PASS | Documented $100M Mac recipe and size-C selection rented 7 Fourth Street, Midtown, C1, 225 sqm; staff, stock and checkpoint read back. |
| `shop-serving-readiness` | FAIL | ready=false: empty_cargo_entry; no_staffed_counter. Second showcase has item=null; native register serving.available=false. |
| `f8h-1 shop assertions` | PARTIAL | Shop inspected before/after normal reload: native-owned / PC managed=false, MissingDefinition. Native service, hand-back/reacquisition and refused-window receipt assertions NOT RUN; needs ready shop and the prior foreign-patch cycle, which was not repeated. |
| `f8h-2 first sale` | PARTIAL | After normal reload: city running, checkout active, 49/49, 0 refusing; no load-wait refusal. First-sale assertion NOT RUN: shop readiness false. |
| `f6b-3` | PARTIAL | All four Core estimate labels match. Captured rival decision / ServiceEvidence arm NOT RUN; no serving-rush activity or decision fixture. |
| `e5b2-2` | NOT RUN | Needs present/absent/returned catalogue lifecycle and exact RIG-CAT comparison; not a rental/first-sale-only row. |
| `e5b2-3` | NOT RUN | Needs owned burger/hot-dog structural-edit matrix (including add-holder/add-price/change-type); unready one-offer coffee shop does not supply it. |
| `e5b2-4` | NOT RUN | Needs matched 0/8 owned-shop ordinary windows and controls; readiness failed. |
| `e5b3-2` | NOT RUN | Needs PC-owned burger/hot-dog mutation storm and checkout/boundary observations; readiness failed, PC image MissingDefinition. |
| `e5b3-3` | NOT RUN | Needs original-three owned + four acquiring shops and reviewed in-place writes; not supplied by current shop/ops. |
| `e5b3-4` | NOT RUN | Needs original-three owned-shop EmployeeWorkstation reconciliation or complete negative catalogue search; current shop is unready. |
| `e5b3-5` | NOT RUN | Needs 8 repaired then 64 owned + 8 acquiring shops, in-place repair and boundary backlog fixture. |
| `e5b3-6` | NOT RUN | Needs 0/8/64 matched ordinary/storm/recovery windows; no ready baseline. |
| `e5b4-4` | NOT RUN | Needs owned burger/hot-dog metadata override/control fixture and lunch decisions; unready coffee shop is insufficient. |
| `e5b4-5` | NOT RUN | Needs observed held dynamic decision plus metadata/reload replay fixture; none reached. |
| `e5b4-6` | NOT RUN | Needs 0/8 original-three shop matched head/base performance fixtures. |
| `e5b5-2` | NOT RUN | Needs acquired mixed original/dynamic product shop with real sales and independent receipt/order comparator; readiness failed. |
| `e5b5-3` | NOT RUN | Needs genuine eight- and nine-offer shops, in-flight price change and UI clearance; one unready offer is insufficient. |
| `e5b5-4` | NOT RUN | Needs acquired mixed mod-product shop with receipt plus product-provider removal/return and boundary trace; missing baseline/transport. |
| `e5b5-5` | NOT RUN | Needs owned mixed shop with an observed held dynamic decision and metadata/reload control. |
| `e5b5-6` | NOT RUN | Needs 0/8/64 eight-offer shops and recovery-wave ordinary windows. |
| `e5b-complete-menu-2` | NOT RUN | Needs valid mixed shop and automated visual/UI clearance; prescribed Mac recipe produced unready one-offer shop. |
| `e5b-complete-menu-3` | NOT RUN | Needs natural native purchases and independent receipt/order/resource/accounting deltas; readiness false. |
| `e5b-complete-menu-4` | NOT RUN | Needs owned serving shop, dirty/repair/recovery boundary trace and native refill matrix; readiness false. |
| `e5b-complete-menu-5` | NOT RUN | Needs held/name/receipt comparator and Street Food absent-control transport; FT bridge is not that independent transport. |
| `e5b-complete-menu-6` | NOT RUN | Needs matched idle/breakfast/lunch/evening/soak and dirty/recovery 0/8+ shop controls; readiness false. |
| `f9a-1` | NOT RUN | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| `f9a-2` | NOT RUN | Needs ready PC-owned shop, day-3 targets and selected shop decisions; readiness false / MissingDefinition. No f9-city-ready produced. |
| `f9a-3` | NOT RUN | Needs f9-city-ready and RIG-ACCEPT; supporting raw capability/status reads retained. |
| `f9a-4` | NOT RUN | Needs serving f9-city-ready and paired seven-day daily-reload/uninterrupted ledger/native controls. |
| `f9a-5` | NOT RUN | Needs f9-city-ready and matched cadence/frame-cap/skip fixtures; reserved presentation steps excluded. |
| `f9a-6` | NOT RUN | Needs f9-city-ready, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| `f9a-7` | NOT RUN | Needs agency-dialog/crew-truck fixture and working Street Food vendor API; live API missing. |
| `f9a-8` | NOT RUN | Needs older-schema fixture/loader; historical fixtures explicitly excluded; schema held at 1. |
| `f9a-9` | NOT RUN | Needs serving price/control copies and loaded FoodChoicePriceWeight readback; no ready baseline or reviewed config key. |
| `f9a-10` | NOT RUN | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| `f9bdf-1` | NOT RUN | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| `f9bdf-2` | NOT RUN | Needs f9-city-ready, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| `f9bdf-3` | NOT RUN | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| `f9bdf-4` | NOT RUN | Needs f9-city-ready and RIG-ACCEPT; supporting raw capability/status reads retained. |
| `f9c-1` | NOT RUN | Needs ready PC-owned shop, day-3 targets and selected shop decisions; readiness false / MissingDefinition. No f9-city-ready produced. |
| `f9c-2` | NOT RUN | Needs serving f9-city-ready and paired seven-day daily-reload/uninterrupted ledger/native controls. |
| `f9c-3` | NOT RUN | Needs f9-city-ready and matched cadence/frame-cap/skip fixtures; reserved presentation steps excluded. |
| `f9c-4` | NOT RUN | Needs f9-city-ready, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| `f9c-5` | NOT RUN | Needs agency-dialog/crew-truck fixture and working Street Food vendor API; live API missing. |
| `f9c-6` | NOT RUN | Needs older-schema fixture/loader; historical fixtures explicitly excluded; schema held at 1. |
| `f9e-1` | NOT RUN | Needs RIG-ACCEPT; raw active/inactive capability and accounting reads retained only. |
| `f9e-2` | NOT RUN | Needs paired head/base diagnostic/research-order fixture and RIG-ACCEPT. |
| `f9e-3` | NOT RUN | Needs paired head/base panel observations and supported Ctrl+F8 input; foreground/input arms excluded. |
| `f9e-4` | NOT RUN | Peter pointer/hover/focus/click playtest; excluded. |
| `f9e-5` | NOT RUN | Needs paired base census/save-cost comparison and RIG-ACCEPT; no save profiling requested. |
| `f9e-6` | NOT RUN | Needs paired observer/presentation controls and input/performance windows; reserved steps excluded. |
| `f9g-1` | NOT RUN | Needs f9-city-ready, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| `f9g-2` | NOT RUN | Needs serving f9-city-ready and paired head/base body-selection fixture plus RIG-ACCEPT. |
| `f9g-3` | NOT RUN | Needs opening-queue and published vendor stand, head/base counts/screenshots plus RIG-ACCEPT; vendor API missing. |
| `f9g-4` | NOT RUN | Needs matched head/base presentation windows; reserved steps excluded. |
| `f9h-1` | NOT RUN | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| `f9h-2` | NOT RUN | Needs serving price/control copies and loaded FoodChoicePriceWeight readback; no ready baseline or reviewed config key. |
| `f9h-3` | NOT RUN | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| `f6b-1` | NOT RUN | Needs FT-F6b-RivalLimits staffed rush and reviewed RivalServiceLanes/RivalMealPeriodUnits variants; keys absent from allowlist. |
| `f6b-2` | NOT RUN | Needs representative ready staffed shops and natural lunch purchases / MealsPerHour; readiness false. No calibration inferred. |
| `f6b-4` | NOT RUN | Needs ready owned shop and matched default/tight full-rival controls plus independent order/receipt comparator; reviewed config keys missing. |
| `f6b-5` | NOT RUN | Needs checks 1/4 experiments and their before/after rival fingerprints; experiments did not run. |
| `f6b-6` | NOT RUN | Needs real queued/serving rival ServiceAt checkpoint and unreloaded control; no named-state fixture reached. |
| `f6b-7` | NOT RUN | Needs check-6 checkpoint and reviewed RivalMealPeriodUnits reload variant; key absent. |
| `f6b-8` | NOT RUN | Needs valid staffed rush, matched pre-F6b control and clean mod-work/whole-frame window; no ready baseline. |
| `f7-1 sale parts` | NOT RUN | Needs natural paid sale / Pending activity in an owned serving shop; readiness false. No same-minute/midnight/non-sale arms repeated. |
| `f7-3 receipt_pending` | NOT RUN | Needs actual receipt_pending FT F7 food shop checkpoint; label alone cannot create it, and readiness false prevents the sale arm. No kill/fault arm run. |
| `f8a-5 native-shop half` | NOT RUN | Needs native successful service with RLO absent; shop unready. Prior RLO-toggle proof not repeated. |
| `f8bg-4 native-shop half` | NOT RUN | Same native-service blocker as F8a-5; no RLO toggle repeated. |
| `S2 R0 sale proof` | NOT RUN | Omitted: no rig brief. Planned code slice (NativeSaleProbe etc.), not a current rig row; no implementation/probe invented. |

## Cleanup

`python3 scripts/mac-agent stop` exited 0 normally, then the original isolated ModsLocal/preferences/plist/config/receipt were restored. **260/260 protected files SHA-256 match**, including autosave preferences. `lock release` exited 0; final status stopped, lease free, pending 0. Owned game PID 69482 and its matching caffeinate are absent. No rollback, force stop, system setting change, unrelated process action or Windows contact. Every foreground command completed before delivery. Fixtures remain in the isolated profile.
