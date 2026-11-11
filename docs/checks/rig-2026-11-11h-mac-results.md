# Rig results — 11 November 2026 (h), Mac (#74 staffed-register / cabinet fixture + first native sale checks)

Role `rig_test`, where `mac`, isolated `agentmac`. Built candidate **a65e5a46166bda6ea1458f84ede217472b75eb9c**, verified with `git fetch origin` and `git rev-parse origin/codex/persistent-city` before build; unchanged from the requested minimum. Schema held at 1. Testing only; no production changes, release, version/changelog, merge, issue creation or Windows rig contact.

**Summary: 4 passed / 8 partial / 1 failed / 64 not run. Result: BLOCKED (active-checkout first-sale assertion failed).** Counts cover the 77 per-check rows below; grouped exclusions are not added again. Prior-run passes are not recounted.

**#74 readiness is verified by rig.** Selection rented **25 Fourth Avenue, Garment District, C2, 225 m²**, business `ba:street_fourthavenue#25`. The register is on the Modern Cabinet with its customer-service shift pinned to it: `ready=true`, `reasons=[]`, register `serving.available=true`, one staffed counter on all seven days. **F8h first sale failed in the completed next-day noon window:** native revenue $0, completed orders 0, PC native purchases 0, despite active checkout and no load-wait refusal. This is not attributed to an attachment or load-contract defect by assumption.

`<stage>` is `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-11h-mac`. Large raw JSON, logs, extracted payloads, scripts and backups remain there. SHA-256 archive index: `evidence-index.json`. Prior 11f/11g reports, stage scripts and frozen check text were read only; previous worktrees/stages and both named live harness sessions were untouched.

## Build and rig

| Item | Value |
|---|---|
| Candidate/worktrees | Detached `rig-2026-11-11h-mac`; independent evidence branch `codex/mac-rig-2026-11-11h`, both from verified `a65e5a4`. Main working tree unchanged. |
| Official rig checkout | **059c50351c4a4e9915154330e707abb2b9dd5e85**, clean; read only. No rig reinstall/update/rollback. |
| Retained rollback, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261111T191821Z`. Older rollback directories untouched. |
| Player/profile | Private native Apple Silicon Mac app, isolated `com.hovgaardgames.bigambitions.agenttests.mac`, endpoint `127.0.0.1:28330`, runtime 7. No normal profile/Steam app access. |
| Build | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; succeeded. Same compile-reference path as Mac 11f. |
| Environment | All Python/.NET/build commands used `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No full or regression suite. |
| Candidate DLL SHA-256 | `11a0fb7b489fd8fcd3028c0a47a1348c2f44450a713075275e60fdeba266f45f`; installed hash matched. |
| Provenance | Mac reference set `unknown`, 27 differing assemblies. No same-head full offline acceptance artifact or Windows compatibility claim. |
| Full modpack | All 29 historical full-modpack DLL paths present. Only candidate PC and preinstalled FoodTrucks `Dependencies/BridgeControl.dll` differ from the old owner inventory. `modpack-live.json`, `modpack-summary.json`. |
| Lease/mode | Free/stopped initially; owner `pc-mac-rig-2026-11-11h`, purpose `Persistent City 11 Nov Mac rig h`. Headless `--rig-test`; same owner throughout. No token printed/staged/passed in argv. |
| Resources | Immediately before launch: 65.0 GiB free disk / 46% reported free memory. No process outside this run was stopped or modified. |
| Backup/config | 260 original files SHA-256 manifested and copied before changes: isolated ModsLocal, top-level preferences, isolated plist and receipt. Candidate DLL, English locale, enabled/physical config with 48 pedestrians, 24 vehicles and 120 m radius; remaining compiled defaults. |
| Autosave original/restoration | Original isolated plist has no autosave keys. Process-only #70 guard used; `mac-agent autosave` confirmed `suppressed=true` before windows and after every completed reload. No preference edit to suppress saves. Cleanup restored exact original hashes. |
| Cleanup | Normal stop, 260/260 original hashes match; autosave restored=True; lease held=False, pending=0. `owned-process-verification.json`. |

## Runs

| Segment | Fixture / steps | Result |
|---|---|---|
| Fresh creation | `FT-PC-Mac-20261111h-a65e5a4` | Completed at candidate with full modpack. |
| Fund/rent/readiness | Run-specific copy of official Mac recipe; size-C selection | Rental and readiness PASS, checkpoint saved. |
| Normal load / first sale | Supplied `reload.py` template, then hourly day-one snapshots and documented next-day noon window | Contract/load checks passed; positive first-sale assertion FAIL ($0 / 0 completed orders / 0 native purchases). |
| F9 day-three setup | Advance to day 3 07:30, save `FT Mac 11h f9-city-ready` | Targets and PC ownership observed; selected own-shop purchase not established, PARTIAL. |
| F8h refusal/control | Named no-op foreign prefix, normal load, 12:00–15:00; remove, reload saved native-owned control, 16:00/17:00 | Native revenue delta $35; saved PC receipt keys 0; reacquired managed=true, lastReadiness=Ready. |
| F7 sale/save subset | Native control, saved epoch extraction and cold reload | Coherence evidence retained; menu command unavailable; paid/Pending PC sale-state matrix remains partial. |
| Read-only/performance subset | Paused snapshots and one speed-1 open-shop sample | Raw results and limitations below. |

## Shop rental, attachment and serving readiness

Read the verified official checkout's README, `docs/MAC.md`, `docs/rig-fixtures.md` pc-food-shop/Mac paragraph and `docs/rig-ops.md` serving-readiness/attachment sections before operations. No PC recipe or address hunt was used.

The recipe is a stage copy of `flows/mac/f8h-food-shop.json`; `spec_from` remains the updated `flows/fixtures/mac-food-shop.json`. Only unique id/name/checkpoint and documented selection `{"building_size":"ba:buildingsize_c"}` differ. No address pin or equipment workaround. Recipe SHA-256 `80e3dd1f6f5e09d6ef5b9ac536d9c585118b2679fd69cfb9e8c75fb837a8839b`. The recipe set and read back $100,000,000 before rental; no other money mutation or campaign purchase.

Exact commands (from official rig checkout, with the same owner/environment):

```sh
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#25"}'
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-11h-f8h-food"}'
```

The independent `shop_state` diagnostic ran **before any sales clock advance**, exit 0. Short excerpt: `readiness.ready=true; reasons=[]; empty_cargo_holders=[]; staffed_registers=[VyKT3Du9l0OLHu9DWiAbMA==]; hours[1..7].staffed_counters=1`. Croissant: 100 display units at $5, ready, no offer reason. Register: `serving.available=true`, `point_of_sale=true`, paper bags=300. Its Default 09:00–18:00 shifts use that register id, employee available=true, customer_service=50.

Cabinet `qpSTG+qeYkaBoCmv4ffMgw==` sits at `(897,0.000000119,-3)`; register `VyKT3Du9l0OLHu9DWiAbMA==` sits at `(897,0.8800001,-3)`. The prescribed `equipment[].on` plus raised co-located position and the native requirement-aware `serving.available=true` confirm cabinet placement, rather than a floor register. `shop_state` does not expose raw parentId/stackedItems, so those links are not quoted as a separate readback. The independent diagnostic and post-load readback stay ready. `shop-readiness.json` SHA-256 `ffcb55c0c9fcc97cdb0d3dee93a9af6e6074be3ab036675a2369e218fa7c5262`; `readiness-after-reload.json` SHA-256 `ffcb55c0c9fcc97cdb0d3dee93a9af6e6074be3ab036675a2369e218fa7c5262`.

The supplemental fixture-slug status op failed with `ops_fixture_unknown` after the recipe saved under a new save name; see Tooling gaps. It did not invalidate the successful independent readiness diagnostic. No retry/build workaround was used.

## F8h first sale and check-1 shop assertions

Normal completed load: `food.checkout.shop optional active`; hook inventory 49 targets, 0 refusing; `RunMainGameTick` owners classified. No `Food checkout waits for the city load to finish` refusal after load completion. Activation scan times remain in the player logs. PC acquired the ready shop at hour 33 (day 1 09:00), `lastReadiness=Ready`, and remained managed through next-day noon.

At next-day noon, `shop_state.today_revenue=0`, independent ledger completed_orders=0/units=0/revenue=0, `FoodDiagnostics.nativePurchasesThisSession=0`, and no `persistentcity:food-sale-v1:*` key. Exact assertion: **`Expected today_revenue > 0 after the documented next-day noon window; actual 0.`** Raw evidence: `first-sale-assertion.json`, SHA-256 `8630c7567e880b8e2c05ea1071d6d7b0dcda8e2a680e3d8b6c22f9dfac9aa317`. Day-one choices include “Beyond walking tolerance” / “Travel and service cannot fit break”; day-two includes “Service and return do not fit available time”. Core rival purchases are not own-shop native sales.

The foreign-prefix install/remove transport half had passed on PC 11c. It was repeated here only because the outstanding shop assertions required actual native hand-back/service/zero-PC-receipt and reacquisition observations on this now-ready shop. No production Harmony change or RLO toggle. `foreign_patch read/install/remove/read`, normal loads and independent shop/ledger/state/receipt extraction supplied the control. The first post-load read briefly reported transient RLO donor wait; the settled read reports city running, RLO active and checkout explicitly off for the foreign prefix. At 13:00 the shop is desired=false / managed=false / nextBoundary=native. Native refused-window revenue delta **$35**, independently **7 completed orders, 7 croissants, 7 bags**; PC receipt keys **0**. After removal, reloading the saved native-owned control and crossing a later boundary reacquires the shop with `lastReadiness=Ready`. The earlier load of the pre-refusal PC-owned checkpoint is not counted as reacquisition proof. This control is separate from the failed active-checkout first-sale assertion.

After the initial clean full-modpack/first-sale window, `shop_log start` was used only as a supplemental diagnostic during the refused native control; it adds the rig's observer postfix owner. Its read raced the following reload and failed, so the native ledger/stock/revenue snapshots, not an unavailable shop log, support the transaction claims. Recording was stopped; the cold relaunch removes the process-local diagnostic hooks.

## E5b2–5 / complete-menu shop rows

The historical check text was read from `docs-history-2026-11-10`; deleted check files were not recreated. The prescribed shop has one non-original croissant offer. It cannot substitute for the specified burger/hot-dog original-three, mixed product, eight/nine offer, 8/64-shop or in-place mutation fixtures. `shop_edit` has no add-holder/add-price/change-business-type or in-place definition-write operation. Those rows are NOT RUN with their specific prerequisites in the table below. Native control/transaction and paused-read noninterference subsets are PARTIAL; no producer/resource/refill or whole-menu matrix pass is inferred.

## F9 / f9-city-ready

Saved fresh day-three 07:30 checkpoint `FT Mac 11h f9-city-ready`. Manifest: residents=957, visitors=1914, commuter_target=1914, visitor_capacity=1914, fill=attached. Schema 1, no stop marker, ready PC-owned shop, no own-shop purchase. The setup is PARTIAL because a positive own-shop transaction baseline remains unproved. No manifest sealing was attempted.

Capability/status reads retained active and inactive reasons. Street Food vendor bridge is Absent: **`Street Food vendor API missing.`** RIG-ACCEPT is absent from the advertised operations; formal native acceptance/provenance/summary rows remain NOT RUN. Reviewed config keys do not include PhysicalEnabled or price-weight readback. Paired seven-day, frame/cadence/body, research truck and price-control experiments were not replaced by an empty-transaction or unsupported-API pass.

## F6b and F7 sale parts

F6b: hourly readiness/revenue snapshots and rival Core estimate labels were read; one ready croissant shop supplies limited throughput evidence. Named `FT-F6b-RivalLimits` rush/config variants need reviewed RivalServiceLanes/RivalMealPeriodUnits keys absent from the rig allowlist. No rival native field was written. No tight-profile/full-rival, ServiceAt paired reload, matched pre-F6b performance or fingerprint experiment was claimed.

F7 check 1: native-control and normal save/cold-load evidence retained. Extracted schema, payload character length, minute and population match the epoch; stop marker absent. The menu sub-arm is NOT RUN because the bridge rejects `menu`; no replacement command was guessed. No paid PC receipt/Pending receipt appeared, so the requested PC sale-state epoch matrix remains PARTIAL. `FT Mac 11h Paid Food v1` was named before its zero-sale observation was analyzed; its name is not a sale assertion. F7 check 3 receipt_pending: NOT RUN, needs an observed named-state checkpoint; no fabricated Pending state. Checks 2/7/9 from #211 and checks 4/5/6/8 were not repeated.

## S2 five checks

All five **NOT RUN — needs S2 #226 merge / probe command at the built candidate**: food sale, goods sale, service sale, below-basket ceiling refusal, reload mid-probe. `Runtime/NativeSaleProbe.cs` / `Core/PurchaseProbe.cs` are absent at `a65e5a4`. No S2 worktree, implementation or live session was touched. An ordinary native food sale/control would not substitute for the diagnostic probe.

## Performance

The provisional raw union and whole-frame records are in `<stage>/perf-union-end.json` and `perf-whole-end.json`; the scalar summary and every returned ≥30 ms union spike are in `performance-summary.json`. Valid complete-coverage sample: **1190 frames / 45.662 s**, mod-work p95 **4.7354 ms** (<5 ms), maximum **32.3188 ms**, **1 spike ≥30 ms**. The one flagged spike is frame **3441**, **32.3188 ms**, dominant `PopulationRuntime.Update` (31.9412 ms self), `frame_update`, condition `speed=1|hour=17`; whole frame at that spike 62.7929 ms. Union-recorder whole-frame p95 **64.6724 ms**, max **168.341 ms**; the separate outer whole-frame window p95 **68.2314 ms**, max **400.2122 ms**. Native save attempts/allowed/refused=0/0/0, saving frames=0, clean=true; epoch sequence unchanged at 7. The concurrent Mac harness was active at launch. These are provisional measurements, not a paired F6/F9 performance verdict. Save work and whole-frame time are reported separately. No performance-only or save-profiling run, production change or optimization was made. Earlier startup/route spikes are retained in the player logs as advancement timings, not misreported as per-frame union.

## Findings

- #74 fixes the observed native serving-readiness prerequisite on Mac: attached available register and staffed counter, with a ready croissant offer after reload.
- Active-checkout first-sale assertion failed in the prescribed window: $0 and zero completed orders/purchases. Choices show travel/service-time refusals; this run assigns no unproved code root cause.
- Native refusal-control revenue delta $35; zero new PC receipt keys; removal/reacquisition observed as recorded above.
- The stage `reload.py` used the supplied template and retained `from pathlib import Path`; every stage script using Path passed the import check. No repeat of 11f's NameError or failed sed insertion.

## NOT RUN grouped

| Group | Reason |
|---|---|
| Mixed/original-three/eight-offer and scale/recovery shop matrices | The prescribed one-croissant #74 shop is not those fixtures; add-holder/add-price/change-type/in-place writes, independent resource/receipt and Street Food absent-control transport remain missing. |
| Formal F9 / positive-sale paired controls | RIG-ACCEPT and same-head full acceptance artifact absent; partial day-three baseline, no selected own-shop purchase; no official manifest sealing. |
| F6b rush/config and F7 Pending | Named rush and reviewed rival config variants not available; no actual receipt_pending state captured. |
| Reserved capabilities | RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY; no foreground substitute. |
| Explicit exclusions | stress10k/city-50k, RIG-FAULT, RIG-PCFIX, historical schemas, F4f/F7e implementation, pointer/hover/focus/click playtest; Windows/overlay-specific and already-covered PC rows. |
| S2 probe | Five checks need S2 #226 merge / probe command at the built head. |

## Tooling gaps

| Capability / operation | Exact command / exit / error | Flow / blocked checks |
|---|---|---|
| shop_fixture status | `python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-11h-f8h-food"}'`; exit **1**, stdout empty, stderr JSON `{"error":"ops_fixture_unknown","reply":{"ok":false,"error":"ops_fixture_unknown"}}` (request/session ids omitted). | Supplemental post-recipe status; independent shop_state succeeded, so no dependent sale check was skipped on this error. Full reply in `shop-status.err`, SHA-256 `4796363c072890782cf371933e09ed7dfc910f43ace7ee2a8d955bc14d6360d2`. |
| shop_log read | `python3 scripts/mac-agent op shop_log '{"action":"read","since":0,"businesses":["ba:street_fourthavenue#25"]}'`; exit **1**, stdout empty, stderr JSON `{"error":"ops_city_required","reply":{"ok":false,"error":"ops_city_required"}}` (ids omitted). | Supplemental read reached the bridge during the subsequent reload. Log capture unavailable; independent completed-order ledger and revenue/stock readbacks retained. `native-log-hour-read.err`. |
| FT menu | `python3 scripts/mac-agent call menu '{}'`; exit **1**, stdout empty. Exact stderr: **`Unknown command 'menu'. Commands: state, console, debug, time, money, teleport, walk, drive, vehicle, commissary, crew, route, price, marketing, use, taxi, phone, call, eat, floorplan, place, drop, pickup, handtruck, fixture, logistics, parks, day_summary, places, map, ui, shoot, save, load, new_game, quit, op.`** | F7 normal-menu sub-arm NOT RUN. Cold reload uses the documented normal stop/start path independently. `menu-coherence.err`. |
| FT console spelling | `python3 scripts/mac-agent call console '{"line":"pc food status"}'`; exit **0**, output **`Warning: [Persistent City] Unknown command. Use: pc population census [workplaces] \| pc population person <id> \| pc population manifest`**. | Supplemental stage read; valid pcstate/contract diagnostics used for results. `pc-food-status.json`. |
| Post-load transient diagnostic | `python3 scripts/mac-agent call console '{"line":"pc food contract boundary"}'` and the equivalent `status` read; exit **0**, output **`Warning: [Persistent City] food contract diagnostics unavailable: no active city.`** | Initial control-load read during documented RLO donor start wait. Settled capability and boundary reads succeeded before the qualifying native-control snapshots; no inactive read counted as a pass. `native-control-loaded-pc-food-contract-boundary.json`, `native-control-settled-capabilities.json`. |
| Stage reload.py Path import | Supplied stage template copied and adapted; `from pathlib import Path` present. Script completed the player-log copy tail, no NameError. | `reload.py`, `reload.log`, `stage-script-path-check.json`; this is stage tooling, not an agent-rig defect. |

Any further exact operation errors are retained in `failures-later.jsonl` and listed in the PR body; no failed operation was retried blindly. Missing capabilities above were identified from docs/schemas and were not called with invented commands.

## Fixtures

Only this run's new character directory was used: `<isolated-profile>/SaveGames/Big Ambitions/PYYywuHR6Eq5tHwHW5gVdw==/`. Explicit FT checkpoints remain there; no preexisting or normal save opened/copied/changed/deleted. Save hashes verified after stop.

| Fixture/checkpoint | SHA-256 |
|---|---|
| FT Mac 11h Cold Verified v1 | `b3a69109213c1f25af4b0e3f5d542f600dc20e6c2fc9606d1b59287fabe82651` |
| FT Mac 11h Day2 Noon v1 | `646da61fbae6d9f386d9d406f97cb1ac44b3e35ab57a0a45e24b2ca07d4e951c` |
| FT Mac 11h F8h Food v1 | `a35ee319dac4c42b18891d0b468af4c06f97609b9699827bb2131261923c53e2` |
| FT Mac 11h Native Control v1 | `8e598d35efd971c8194f2475eb73d0da8fbc2bca50307b89e6f9bbbef99030fc` |
| FT Mac 11h Paid Food v1 | `c0865e1f4c0f1177c6e7d1b28104d3ec5f28f7f7d77b11ff5c60fa129b1f6a7d` |
| FT Mac 11h Reacquired v1 | `b4338d5a5d3f3ddbeb0eb46dfa87d1034c6ae9b9df1420e3532932f10668f8ea` |
| FT Mac 11h Reacquired v2 | `5d4168dbcac2be482a42893b1d3e956389cda3ec952714dbd923347a02d88b82` |
| FT Mac 11h f9-city-ready | `12069f0c1f28e67f4a0c802c024df67cc53c95ba8e00d4056a6b531fb04f27b5` |
| FT-PC-Mac-20261111h-a65e5a4 | `65785af93c532153887a047b1dcedbf9d5752fc15b9132228c150e0233496c60` |

`fixtures.json`, `fixture-hash-verification.json`, `coherence-summary.json` contain local paths and extraction details outside Git. No sealed fixture claim.

## Restore and delivery

Normal stop of only this run's agentmac player. All 260 protected original hashes match, isolated ModsLocal/config/preferences/plist/receipt restored; original autosave behavior restored by stopping the process-only guard and restoring the plist. Disposable FT saves retained and hash-verified. Lease free, pending=0; owned game/helper processes gone. No system setting change or rollback revision used. Official rig checkout and main mod working tree clean. Evidence branch pushed with a normal push; docs-only PR opened without reviewers and left unmerged. Build worktree removed and evidence worktree detached after PR creation. Report copy: `.agent-logs/persistent-city-rig-2026-11-11h-mac.md`.

## Per-check results

Frozen source definitions are in `<stage>/check-text`; schema-1 owner instructions override historical writer-number language. PASS applies only to the named observation; PARTIAL retains its missing arms; NOT RUN is never a pass by assumption.

| Check | Result | Evidence / reason |
|---|---|---|
| mac-autosave | **PASS** | --rig-test suppression true at menu, creation, recipe, every completed reload and measured window. Original isolated preferences restored and hash-verified. |
| shop-fixture-selection | **PASS** | The documented $100M Mac recipe and unpinned size-C selection rented 25 Fourth Avenue, Garment District, C2, 225 m²; checkpoint saved. |
| shop-serving-readiness | **PASS** | ready=true, reasons=[] before sales and after normal load. Register available=true at cabinet height, 300 bags, Default customer-service shift pinned to that register; staffed_counters=1 on all seven days. |
| f8h-1 shop assertions | **PASS** | Foreign-prefix refusal, native hand-back and removal/reacquisition ran on this shop. Native refused-window revenue delta $35; saved PC receipt keys=0; after removal managed=true, lastReadiness=Ready. Native service and zero new PC receipts verified. |
| f8h-2 first sale | **FAIL** | Completed normal-load window through next-day noon: checkout active, 49 targets / 0 refusing, no load-wait refusal, but today_revenue=0, completed_orders=0, nativePurchasesThisSession=0. Expected today_revenue > 0; actual 0. Shop choices include “Service and return do not fit available time”. |
| f6b-3 | **PARTIAL** | Core labels match: ServiceLimits=estimated (modeled service limits), SellOut=modeled sell-out (estimated), Availability=EstimatedFromPriceAndHours, NativeRivalCheckout=Unsupported. Captured rival decision ServiceEvidence is not exposed by these projections; no complete decision-label pass. |
| e5b2-2 | **NOT RUN** | Needs present/absent/returned catalogue lifecycle and exact RIG-CAT comparison; not a rental/first-sale-only row. |
| e5b2-3 | **NOT RUN** | Needs burger/hot-dog original-three fixture and add-holder/add-price/change-type operations, absent from the supported shop_edit set. The prescribed fixture is one croissant offer. |
| e5b2-4 | **NOT RUN** | Needs matched 0/8 owned-shop ordinary windows and control fixture; one croissant shop supplies no such comparison. |
| e5b3-2 | **NOT RUN** | Needs the burger/hot-dog original-three mutation-storm fixture and complete edit matrix. The prescribed one-offer coffee shop is not that fixture. |
| e5b3-3 | **NOT RUN** | Needs original-three owned + four acquiring shops and reviewed in-place writes; not supplied by current shop/ops. |
| e5b3-4 | **NOT RUN** | Needs EmployeeWorkstation reconciliation on the original-three fixture, or its complete negative loaded catalogue/holder search. The one-offer showcase fixture does not establish it. |
| e5b3-5 | **NOT RUN** | Needs 8 repaired then 64 owned + 8 acquiring shops, in-place repair and boundary backlog fixture. |
| e5b3-6 | **NOT RUN** | Needs matched 0/8/64-shop ordinary, storm and recovery fixtures; no substitute performance pass. |
| e5b4-4 | **NOT RUN** | Needs the original-three burger/hot-dog metadata override/control fixture and held/new lunch decisions. |
| e5b4-5 | **NOT RUN** | Needs observed held dynamic decision plus metadata/reload replay fixture; none reached. |
| e5b4-6 | **NOT RUN** | Needs 0/8 original-three shop matched head/base performance fixtures. |
| e5b5-2 | **NOT RUN** | Needs a mixed original/dynamic menu, Street Food building-holder evidence and an independent native-order/PC-receipt comparison. One croissant is not a mixed menu. |
| e5b5-3 | **NOT RUN** | Needs genuine eight/nine-offer fixtures and an in-flight price change with automated menu clearance. The prescribed shop has one offer; pointer checks remain excluded. |
| e5b5-4 | **NOT RUN** | Needs acquired mixed mod-product shop with receipt plus product-provider removal/return and boundary trace; missing baseline/transport. |
| e5b5-5 | **NOT RUN** | Needs owned mixed shop with an observed held dynamic decision and metadata/reload control. |
| e5b5-6 | **NOT RUN** | Needs 0/8/64 eight-offer shops and recovery-wave ordinary windows. |
| e5b-complete-menu-2 | **NOT RUN** | Needs a catalogue-qualified mixed-menu fixture and automated product/holder/UI clearance. The prescribed #74 fixture supplies one croissant offer. |
| e5b-complete-menu-3 | **PARTIAL** | Ready native control observed through 15:00, revenue delta $35; active PC checkout window had no purchases. Full native-order/PC-receipt/resource/refill/settlement comparison remains unproved; no zero slot or absence was relabeled a stockout. |
| e5b-complete-menu-4 | **NOT RUN** | Needs the complete structural/supply edit, repair/recovery and native refill matrix plus publication/requested-boundary trace. The foreign-prefix shop boundary subset is recorded under F8h-1; it is not this matrix. |
| e5b-complete-menu-5 | **NOT RUN** | Needs held/name/receipt comparator and Street Food absent-control transport; FT bridge is not that independent transport. |
| e5b-complete-menu-6 | **PARTIAL** | Paused read-only subset ran: shop, native ledger, live PC keys and economy snapshots before/after contract diagnostic reads. shop equal=true, ledger equal=true, keys equal=true, economy equal=true. The matched 0/8+/64-shop idle/breakfast/lunch/evening/soak and recovery matrix is NOT RUN. |
| f9a-1 | **NOT RUN** | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| f9a-2 | **PARTIAL** | Created day-three 07:30 FT Mac 11h f9-city-ready: 957 residents / 1,914 visitors at targets, schema 1, shop ready and PC-owned. No selected own-shop purchase; shop choices are recorded rejections. Formal FT-F9 manifest remains unsealed. |
| f9a-3 | **NOT RUN** | Needs RIG-ACCEPT native stage records; raw day-three capability/manifest/vendor/research/accounting observations retained, inactive capabilities are not passes. |
| f9a-4 | **NOT RUN** | Needs a positive sale/selected-decision FT-F9 baseline and paired seven-day daily cold-reload/uninterrupted order and ledger comparison. The fresh day-three checkpoint is only partial. |
| f9a-5 | **NOT RUN** | Needs the fully asserted FT-F9 baseline and matched cadence/frame-cap/skip fixtures; reserved presentation steps excluded. |
| f9a-6 | **NOT RUN** | Needs the fully asserted FT-F9 baseline, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| f9a-7 | **NOT RUN** | Needs agency-dialog/crew-truck fixture and working Street Food vendor API; live API missing. |
| f9a-8 | **NOT RUN** | Needs older-schema fixture/loader; historical fixtures explicitly excluded; schema held at 1. |
| f9a-9 | **NOT RUN** | Needs loaded FoodChoicePriceWeight readback and paired two-day price/control fixture. The reviewed rig allowlist has no such readback/variant key. |
| f9a-10 | **NOT RUN** | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| f9bdf-1 | **NOT RUN** | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| f9bdf-2 | **NOT RUN** | Needs the fully asserted FT-F9 baseline, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| f9bdf-3 | **NOT RUN** | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| f9bdf-4 | **NOT RUN** | Needs the fully asserted FT-F9 baseline and RIG-ACCEPT; supporting raw capability/status reads retained. |
| f9c-1 | **PARTIAL** | Created day-three 07:30 FT Mac 11h f9-city-ready: 957 residents / 1,914 visitors at targets, schema 1, shop ready and PC-owned. No selected own-shop purchase; shop choices are recorded rejections. Formal FT-F9 manifest remains unsealed. |
| f9c-2 | **NOT RUN** | Same incomplete positive FT-F9 baseline and paired seven-day control as F9a-4; no empty-transaction equivalence pass. |
| f9c-3 | **NOT RUN** | Needs the fully asserted FT-F9 baseline and matched cadence/frame-cap/skip fixtures; reserved presentation steps excluded. |
| f9c-4 | **NOT RUN** | Needs the fully asserted FT-F9 baseline, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| f9c-5 | **NOT RUN** | Needs agency-dialog/crew-truck fixture and working Street Food vendor API; live API missing. |
| f9c-6 | **NOT RUN** | Needs older-schema fixture/loader; historical fixtures explicitly excluded; schema held at 1. |
| f9e-1 | **NOT RUN** | Needs RIG-ACCEPT; raw active/inactive capability and accounting reads retained only. |
| f9e-2 | **NOT RUN** | Needs paired head/base diagnostic/research-order fixture and RIG-ACCEPT. |
| f9e-3 | **NOT RUN** | Needs paired head/base panel observations and supported Ctrl+F8 input; foreground/input arms excluded. |
| f9e-4 | **NOT RUN** | Peter pointer/hover/focus/click playtest; excluded. |
| f9e-5 | **NOT RUN** | Needs paired base census/save-cost comparison and RIG-ACCEPT; no save profiling requested. |
| f9e-6 | **NOT RUN** | Needs paired observer/presentation controls and input/performance windows; reserved steps excluded. |
| f9g-1 | **NOT RUN** | Needs the fully asserted FT-F9 baseline, mid-leg fixture and reviewed PhysicalEnabled=false variant; key absent from allowlist. |
| f9g-2 | **NOT RUN** | Needs a fully asserted positive-sale FT-F9 baseline and paired head/base body-selection fixture plus RIG-ACCEPT. |
| f9g-3 | **NOT RUN** | Needs opening-queue and published vendor stand, head/base counts/screenshots plus RIG-ACCEPT; vendor API missing. |
| f9g-4 | **NOT RUN** | Needs matched head/base presentation windows; reserved steps excluded. |
| f9h-1 | **NOT RUN** | Needs RIG-ACCEPT and same-head full offline artifact; Mac reference fingerprint is unknown. Build hashes retained only. |
| f9h-2 | **NOT RUN** | Same loaded price-weight and paired two-day price/control prerequisites as F9a-9. |
| f9h-3 | **NOT RUN** | Needs RIG-ACCEPT, same-head full offline/native/F4 records; no formal summary synthesized. |
| f6b-1 | **NOT RUN** | Needs FT-F6b-RivalLimits staffed rush and reviewed RivalServiceLanes/RivalMealPeriodUnits variants; keys absent from allowlist. |
| f6b-2 | **PARTIAL** | Hourly ready/staffed shop snapshots and native control were read. Day-one 09:00–13:00 and day-two noon active-checkout revenue stayed $0; native refusal-control delta $35 over 12:00–15:00. One croissant shop is not a representative throughput cohort; no MealsPerHour projection or calibration pass inferred. |
| f6b-4 | **NOT RUN** | Needs ready owned shop and matched default/tight full-rival controls plus independent order/receipt comparator; reviewed config keys missing. |
| f6b-5 | **NOT RUN** | Needs checks 1/4 experiments and their before/after rival fingerprints; experiments did not run. |
| f6b-6 | **NOT RUN** | Needs real queued/serving rival ServiceAt checkpoint and unreloaded control; no named-state fixture reached. |
| f6b-7 | **NOT RUN** | Needs check-6 checkpoint and reviewed RivalMealPeriodUnits reload variant; key absent. |
| f6b-8 | **NOT RUN** | Needs FT-F6b-RivalLimits staffed rush and matched pre-F6b control. The standalone provisional open-shop union window is not that paired check. |
| f7-1 sale parts | **PARTIAL** | Native sales/control and cold-reload coherence observations ran; extracted schema/length/minute/population markers match and no accounting-stop key is present. No paid PC receipt or receipt Pending was observed, so paid/Pending, same-minute, midnight-posting and relocation sale arms remain unproved. Menu arm NOT RUN: Unknown command menu. “Paid Food” is a checkpoint label, not evidence of a paid sale. |
| f7-3 receipt_pending | **NOT RUN** | Needs an actual receipt_pending checkpoint / named-state capture. None was observed; Mac native save labels cannot create it. No fault/kill arm or other F7-3 state matrix was run. |
| f8a-5 native-shop half | **NOT RUN** | Needs RLO-absent native-service control. This run retained the full modpack; the foreign-prefix control is a separate F8h assertion. |
| f8bg-4 native-shop half | **NOT RUN** | Needs RLO-absent native-service control, as F8a-5. Full modpack retained; no RLO option or DLL removal arm. |
| S2 food sale | **NOT RUN** | Needs S2 #226 merge / pc probe command. Runtime/NativeSaleProbe.cs and Core/PurchaseProbe.cs are absent at the built candidate a65e5a4. No probe implementation or S2 worktree used. |
| S2 goods sale | **NOT RUN** | Needs S2 #226 merge / pc probe command. Runtime/NativeSaleProbe.cs and Core/PurchaseProbe.cs are absent at the built candidate a65e5a4. No probe implementation or S2 worktree used. |
| S2 service sale | **NOT RUN** | Needs S2 #226 merge / pc probe command. Runtime/NativeSaleProbe.cs and Core/PurchaseProbe.cs are absent at the built candidate a65e5a4. No probe implementation or S2 worktree used. |
| S2 below-basket ceiling refusal | **NOT RUN** | Needs S2 #226 merge / pc probe command. Runtime/NativeSaleProbe.cs and Core/PurchaseProbe.cs are absent at the built candidate a65e5a4. No probe implementation or S2 worktree used. |
| S2 reload mid-probe | **NOT RUN** | Needs S2 #226 merge / pc probe command. Runtime/NativeSaleProbe.cs and Core/PurchaseProbe.cs are absent at the built candidate a65e5a4. No probe implementation or S2 worktree used. |
| mac provisional mod-work window | **PARTIAL** | Provisional speed-1 post-load open-shop sample: mod-work p95=4.7354; complete coverage=True. Whole frame reported separately. Shared Mac harness active; no paired or formal performance verdict. |
