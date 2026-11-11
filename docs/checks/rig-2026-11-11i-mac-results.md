# Rig results — 11 November 2026 (i), Mac (#74 staffed-register / cabinet fixture + first native sale checks)

Role `rig_test`, where `mac`, isolated `agentmac`. Initial candidate **956aac1ea26c4e0508d5e78ce9a1b0848e44c4b9** was fetched and verified before build. A later fetch found **d265d1fbb516989382550dd51b791ce71841c56d** (#228, two documentation files only). The detached build worktree was advanced and rebuilt at that tip: the DLL hash is identical. All fixtures were freshly created in this run with those exact installed bytes. Schema held at 1. No production changes, release, version/changelog, merge, issue creation or Windows contact.

**Summary: 7 passed / 2 partial / 2 failed / 9 not run. Result: BLOCKED.** Counts cover the 20 targeted rows below; historical matrix groups are counted once per named group. The normal-load natural first-sale assertion failed; S2 Food sale and ceiling refusal passed. The provisional mod-work sample also exceeded the gate. In-game proof gates no merge.

The #74 recipe rented **24 Fourth Avenue, Hell's Kitchen, C2, 225 m²**, business `ba:street_fourthavenue#24`. Independent serving diagnostic: **ready=true, reasons=[]**, available register on the Modern Cabinet, one staffed counter on every day. The S2 **Food** probe completed a native croissant sale with a 500-cent wallet debit and one added order. The below-basket ceiling refused without changing the wallet or order count. A completed probe receipt survived reload; interruption inside the probe was not reached.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-11i-mac`. Raw logs, JSON, extracted payloads, scripts and backups remain there; `evidence-index.json` records SHA-256. Prior stages and worktrees were read only. Neither named protected harness session, branch or worktree was changed or debugged.

## Build and rig

| Item | Value |
|---|---|
| Candidate / worktrees | Detached `rig-2026-11-11i-mac`, rebuilt at `d265d1fb`; independent evidence branch `codex/mac-rig-2026-11-11i` from that tip. Main working tree unchanged. |
| Official rig checkout | **059c50351c4a4e9915154330e707abb2b9dd5e85**, clean before operations; read only. No reinstall, update or rollback. |
| Rollback retained, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261111T191821Z`; older revisions untouched. |
| Host / profile | Private native Apple Silicon app, isolated `com.hovgaardgames.bigambitions.agenttests.mac`, loopback port 28330, runtime 7. No normal/Steam install, profile or save access. |
| Build command / reference | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance-tip.json`; same compile-reference location as Mac 11h/11f. |
| Environment | Every Python/.NET/build command used `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No full or regression suite. |
| DLL SHA-256 | **2ffe1f51bb80f035021cca4a5f47a948997ffb09e68dc73e9303ed8f43fd1e35**; initial build, new-tip build and installed DLL match. |
| Probe availability | Built DLL contains `NativeSaleProbe`, `SaleProbeCommand` and UTF-16 `pc probe status`; live command returned probe status. `probe-dll-check.json`. |
| Provenance / modpack | Mac reference set `unknown`, 27 differing assemblies; no Windows compatibility claim. All 29 full-modpack DLL paths retained; `modpack-live.json`. No diagnostic mod DLL installed. |
| Lease | Initially free/stopped; owner `pc-mac-rig-2026-11-11i`, exact requested purpose `Persistent City 11 Nov Mac rig h`. Same owner throughout. Token never printed or passed in argv. |
| Mode / resources | Headless `--rig-test`; immediately before launch 61.3 GiB free disk / 79% reported free memory. Only this run's game is controlled. |
| Backup / configuration | 260 files copied and SHA-256 manifested before changes: isolated ModsLocal, profile top-level files, isolated plist and receipt. Candidate PC DLL, English locale and enabled/physical configuration (48 pedestrians, 24 vehicles, radius 120 m); remaining compiled defaults. |
| Original autosave | Isolated plist has no autosave keys. No preference edit to suppress saves; #70 process guard confirmed `suppressed=true` at creation, recipe, reloads and windows. |
| Cleanup | Normal stops; 260/260 original file hashes match, original autosave plist restored; all 7 fixture hashes match. Lease free/pending 0; all owned game/helper PIDs gone. No force stop or rollback. |

## Runs

| Segment | Fixture / steps | Result |
|---|---|---|
| Fresh creation | `FT-PC-Mac-20261111i-956aac1` | Full modpack; candidate DLL with S2 present; native creation completed. |
| Fund / rent / readiness | Run-specific official Mac recipe, size-C selection; `FT Mac 11i F8h Food v1` | PASS; ready diagnostic before advancing the clock. |
| S2 initial entry / refusal | Day 1 09:00, c0, open shop before PC authority transfer | Native sale paid; labelled Goods at a coffee shop, so not counted as food or a goods-shop check. Ceiling refusal passed. |
| S2 reload / managed Food | Reload completed receipt, advance to 10:00, c0 Food probe; c1 ceiling refusal | Managed Food sale and ceiling refusal PASS; receipt reload continuity observed. Mid-probe interruption not reached. |
| F8h natural first sale | Supplied reload template loads pre-probe checkpoint; hourly snapshots through next-day noon | FAIL: next-day noon revenue $0 / 0 completed orders / 0 native purchases. |
| Goods / service prerequisites | Documented generic goods rental; service fixture review | Documented generic gift rental selected `ba:street_fifthavenue#22`; `ready=false`, reasons `no_staffed_counter`. Original source fixture has a floor register and no `station_item`; no cabinet/station workaround was applied. Service sale NOT RUN: needs a serving service fixture. |
| Sale/save and hourly subset | F7-1 / F6b-2 only | Hourly natural snapshots, native-control transactions, paid-probe save/epoch and cold continuity observed; full calibration/Pending matrix remains PARTIAL. |

## Shop rental, cabinet attachment and readiness diagnostic

Read the official checkout README, Mac procedures, pc-food-shop/Mac F8h fixture paragraphs and serving-readiness/`equipment[].on` sections. The stage recipe copies `flows/mac/f8h-food-shop.json`; `spec_from` remains the updated `flows/fixtures/mac-food-shop.json`. Only unique id/name/checkpoint and `"selection":{"building_size":"ba:buildingsize_c"}` differ. No pinned address or equipment workaround. The documented step set and read back $100,000,000 before native rental; no campaign or rival-finance write.

Exact commands, run from the official checkout under the same owner/environment:

```sh
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#24"}'
```

Both exited 0. The separate `shop_state` diagnostic ran **after rent and before sales advancement**. Excerpt: `readiness.ready=true; reasons=[]; empty_cargo_holders=[]; staffed_registers=[iqHO2wqk60yPwpPNI60Iew==]; hours[1..7].staffed_counters=1`. Croissant display: 100 units, $5, offer ready. Register: `serving.available=true`, `point_of_sale=true`, 300 paper bags. Default shifts are 09:00–18:00 every day, pinned by `station_item: ba:itemname_cashregister` to that register; employee available=true, customer_service=50.

Picked location: `ba:neighborhood_hellskitchen`, `ba:buildingsize_c`, version 2, 225 m². Cabinet `pBAAakyc4kumG0HBRXe4gg==` at `(897,0.000000119,-3)`; register `iqHO2wqk60yPwpPNI60Iew==` at `(897,0.8800001,-3)`. The prescribed `on` attachment, native placement receipt and raised available register support cabinet placement. `shop_state` does not expose raw parentId/stackedItems. Native `rented=true`, real-estate `owned=false`; PC food management is recorded separately below. `recipe.json` SHA-256 `2c876c4b31f4b35b9bc75a584fd0ebf70b35ca25f06657aa3126364a28572304`; independent `shop-readiness.json` SHA-256 `8dbbdf906e7b46aea420ac439572f9c2b0d2618c3eeaef5e13cf9c7151305f5e`.

## S2 five checks

The requested S2 section is absent from the kept historical-link checklist at both built tips. PR #226 describes the five requested arms but does not supply a more detailed command recipe. This run used that scope, `docs/plans/next-tracks.md` S2 and the implemented command syntax. It did not recreate deleted check documents.

All commands below use `python3 scripts/mac-agent call console '{"line":"<command>"}'`; exits 0. Probe verdicts are its own output, independent of the transport exit.

| Check | Exact console command / excerpt | Result |
|---|---|---|
| Native food sale | `pc probe c0 address:ba%3Astreet_fourthavenue:24:shop 10000` at day 1 10:00 | **PASS**: `kind=Food`, `foodOwned=true` in preceding businesses read, FullServiceBusinessSimulator; `claimedNativeEntry=false`, quoted/paid=500 cents, `refusal=None`, `receipt=Paid`, wallet 130597→130097 cents, orders 4→5, `orderCompleted=true`, `ordersAdded=1`; croissant $5 and bag $0 both paid. Probe wrote one native entry; it did not claim an existing food-owned entry. |
| Goods sale | Documented generic gift rental selected `ba:street_fifthavenue#22`; `ready=false`, reasons `no_staffed_counter`. Original source fixture has a floor register and no `station_item`; no cabinet/station workaround was applied. Sale probe not invoked after the failed serving prerequisite. | **NOT RUN** |
| Service sale | No reviewed service-business recipe/checkpoint in current Mac fixture set; Mac shop-recipe accepts only coffee shop / fast-food types. Generic build support alone does not establish a serving service fixture. | **NOT RUN — needs a serving service fixture.** |
| Below-basket ceiling | `pc probe c1 address:ba%3Astreet_fourthavenue:24:shop 0` at day 1 10:00 | **PASS**: Food basket 500, ceiling 0, `refusal=Ceiling`, `receipt=Missing`, paid=0, wallet 88433→88433, orders 5→5, `native={}`. Exact reason: **“Basket 500 is above the ceiling 0”**. Earlier c0 09:00 attempt independently refused 500>0 with wallet/orders unchanged. |
| Reload mid-probe | `pc probe status` before/after loading `FT Mac 11i S2 probe result`; later cold continuity below | **NOT RUN — needs a supported suspend/interleave checkpoint inside the synchronous console call.** Supplemental completed-sale continuity: Paid receipt survived reload (`recovered=0`). This does not exercise interruption inside a probe. No Pending state was fabricated; no fault/kill arm. |

The 09:00 pre-transfer command used the same c0/place/10000 syntax, claimed a native entry and returned `kind=Goods`, Paid 500, wallet 131097→130597, orders 0→1, `claimedEntryRetired=true`. Its venue remained a coffee shop, so it is not counted as the goods-shop check. At hour 33 the boundary read had `desired=true`, `managed=false`; at hour 34 it had `managed=true`, `lastReadiness=Ready`. This is observed authority timing, not a code-root-cause claim. No time-fit/wallet/stock/hours refusal appeared in the successful Food probe.

Probe output archive: `probe-managed-food.json` SHA-256 `2697d79b0d003c75ad9dfdb79ea542e05540d95abb9d6e09064a0c9c1d1da456`; Food ceiling `probe-food-ceiling.json` SHA-256 `98bbaafb2ab43086f899ae1334f343ceeb31fe42f7141582895ed8b45c93ab8b`; cold independent comparison `cold-continuity-summary.json` SHA-256 `9a8c5f5ee08763c1f5e2f6799e3612a0e4df434a30fc5690a15427e51bbbd80d` (receipts, wallet, stock, revenue all match; recovered=0).

## F8h first sale and check-1 shop assertions

Normal load used the supplied stage template on **the pre-probe checkpoint**, `FT Mac 11i F8h Food v1`. Settled report: `city running`, `food.checkout.shop optional active`, **49 of 49 targets, 0 refusing**. PC managed the shop from hour 33 and remained managed through day-two noon. No settled load-wait refusal. `shop_state` remained ready.

Exact advance command pattern: `python3 scripts/mac-agent call time '{"run_until":{"day":"+0","time":"HH:00"},"speed":20}'` at each hourly row; the overnight step used `{"day":"+1","time":"09:00"}`. Every step retained an autosave readback and waited for paused/cleared run_until. Final native clock **day 2, hour 12, minute 0.00999832153**, Tuesday; FT arrived_at day 2 12:00. Both days' native opening slots and Default register shifts were **09:00–18:00**, employee available=true, customer_service=50.

**FAIL:** `Expected today_revenue > 0 after the documented next-day noon window; actual 0.` Ledger completed_orders=0, units=0, revenue=0; `FoodDiagnostics.nativePurchasesThisSession=0`; saved natural checkpoint has **0 food-sale receipt keys and 0 probe receipt keys**. No diagnostic sale is included in this arm. `first-sale-assertion.json` SHA-256 `b9a0074add2bb7b79ccff9c9352afc8e9bee9f0465645661b8116b51277b951f`.

| Day | Clock | Ready | Revenue | Completed orders | Native purchases |
|---|---|---|---|---|---|
| 1 | 09:00 | True | 0.0 | 0 | 0 |
| 1 | 10:00 | True | 0.0 | 0 | 0 |
| 1 | 11:00 | True | 0.0 | 0 | 0 |
| 1 | 12:00 | True | 0.0 | 0 | 0 |
| 1 | 13:00 | True | 0.0 | 0 | 0 |
| 1 | 14:00 | True | 0.0 | 0 | 0 |
| 1 | 15:00 | True | 0.0 | 0 | 0 |
| 1 | 16:00 | True | 0.0 | 0 | 0 |
| 1 | 17:00 | True | 0.0 | 0 | 0 |
| 1 | 18:00 | True | 0.0 | 0 | 0 |
| 2 | 09:00 | True | 0.0 | 0 | 0 |
| 2 | 10:00 | True | 0.0 | 0 | 0 |
| 2 | 11:00 | True | 0.0 | 0 | 0 |
| 2 | 12:00 | True | 0.0 | 0 | 0 |

Full retained daily evaluated-offer cause breakdown for this shop, extracted from the natural-noon save:

| Day | Evaluated offers | Access/time | Closed/late | Service | Other causes | Chosen / purchases |
|---|---|---|---|---|---|---|
| 1 | 635 | 413 | 222 | 0 | 0 | 0 / 0 |
| 2 | 74 | 0 | 25 | 49 | 0 | 0 / 0 |

Exact-text choice breakdown observed in hourly trace snapshots:

| Exact observed reason | Distinct observed decision/offer/reason rows |
|---|---|
| Travel and service cannot fit break | 211 |
| Beyond walking tolerance | 52 |
| Service unavailable | 5 |
| Closed or too late | 1 |
| Service and return do not fit available time | 15 |

The console retains at most 50 decisions per read. The last table deduplicates resident + meal decision id + business + product + reason across those reads; it is **a bounded observation, not the full window's decision census**. At the noon read, seven shop-choice rows say **“Service and return do not fit available time”**. Full daily class counts above are retained counters. These are observations, not an assigned code root cause. `natural-summary.json` SHA-256 `540ded5596463fada3401ba7a91713e1436b938c63a2fe776775df1d210cec45`; raw snapshots remain in the stage.

**F8h check-1 shop assertions: PASS.** The documented no-op `agent-rig.foreign.test` prefix was installed with `op foreign_patch '{"action":"install","kind":"prefix"}'`, followed by normal load of `FT Mac 11i natural noon`. Capability output named the refusing owner and checkout was off. The 12:00–15:00 native hand-back window changed revenue **$0→$45 (delta $45)**, with independent completed-order/stock readbacks and **0 saved PC food-sale receipt keys**. No shop_log postfix was added. `op foreign_patch '{"action":"remove"}'` removed the owner; final owner read matched baseline. Reload of the saved **native-owned** control and boundaries at 16:00/17:00 reacquired PC management (`managed=true`, `lastReadiness=Ready`). This is a separate control from the failed active-checkout first-sale window. Exact exchanges/exits are in `commands.jsonl`; `control-summary.json` retains scalar evidence.

Native control ledger at 15:00: **7 completed orders**, 9 croissants / 7 paper bags, revenue **$45**; 16 ledger units includes packaging.


## E5b2–5, F9, F6b and F7 sale parts

E5b2–5: **NOT RUN**. The prescribed fixture has one croissant, not the original-three/mixed/eight-nine-offer or 8/64-shop matrices. The supported `shop_edit` set cannot add holders/prices, change business type or perform the required in-place definition writes. No catalogue lifecycle, resource/refill, metadata or menu matrix pass is inferred from S2.

F9: **NOT RUN**. This narrow first-sale/probe run did not produce/seal `f9-city-ready` or run RIG-ACCEPT, paired seven-day/config/body controls, research/vendor or acceptance/provenance campaigns. No official manifest sealing. Prior 11h findings remain in that report.

F6b check 2: **PARTIAL**. The hourly table above supplies staffed-shop native throughput observations: zero natural completions in every sampled hour; the separate native-control delta is $45. One croissant shop plus a diagnostic one-entry sale is not representative calibration. No native `MealsPerHour` projection was available in the advertised PC state properties, so no ±25% median/default-ceiling assertion was passed. Rival Core labels were retained, but tight-profile/full-rival, ServiceAt replay and paired pre-F6b comparisons were not run.

F7 check 1 sale parts: **PARTIAL**. 6 extracted checkpoints have schema 1, matching payload length/minute/population/epoch and no accounting-stop marker. Completed S2 probe receipts, wallet and stock were inspected, including a normal reload and cold process load of `FT Mac 11i S2 managed food`; cold `pc probe status` returned both Paid receipts with `recovered=0`. This is probe-receipt continuity, not a natural paid food-sale-v1/Pending matrix pass. The natural arm produced no paid food receipt. Pending, same-minute, midnight/relocation and menu sub-arms were not run. F7 check 3 receipt_pending: **NOT RUN — needs an observed Pending checkpoint and supported interleave capture**. No Pending state or fault was fabricated. `coherence-summary.json` and cold readbacks retain the evidence.

## Performance

**FAIL, provisional shared-Mac sample:** speed 1, open shop, 1074 frames / 46.347 s; union mod-work p95 **8.0913 ms** (required <5 ms), maximum **1049.5957 ms**, **14 spikes ≥30 ms**. Complete coverage=true; union validity=true with no invalid reasons. All returned mod-work spikes are listed below; every one is dominated by `PopulationRuntime.Update`, group `frame_update`, condition `speed=1|hour=12`.

| Frame | Mod-work ms | Whole-frame ms | Dominant self ms |
|---|---|---|---|
| 23689 | 1049.5957 | 1265.4763 | 1048.9033 |
| 23769 | 410.6507 | 458.3764 | 410.6212 |
| 23770 | 72.7293 | 200.7263 | 71.9558 |
| 23772 | 146.001 | 230.7961 | 145.3675 |
| 23774 | 46.1173 | 99.7712 | 45.7308 |
| 23777 | 46.5861 | 199.887 | 46.195 |
| 23779 | 50.4253 | 131.5789 | 49.8753 |
| 23782 | 36.4623 | 100.3381 | 36.0785 |
| 23785 | 41.9476 | 66.9117 | 41.6098 |
| 23792 | 34.6294 | 64.4794 | 34.221 |
| 23799 | 36.2501 | 97.9613 | 35.8685 |
| 23805 | 39.9243 | 65.8508 | 39.5361 |
| 24378 | 30.6602 | 64.6947 | 30.1703 |
| 24595 | 30.9217 | 63.3896 | 30.531 |

Whole-frame timing is separate: union-recorder p95 **65.9276 ms**, maximum **1265.4763 ms**; outer window p95 **68.3027 ms**, maximum **1268.837 ms**. Native save attempts/allowed/refused=0/0/0, native saving frames=0, save_activity.clean=true. Save epoch sequence stayed 2. Autosave suppression was verified immediately before and after the window. No save was inside it.

The brief identified a concurrent Mac harness; this run did not inspect or alter that protected session. These numbers are provisional on the shared host, not a paired F4/F6/F9 comparison. No production change/optimization or save profiling was performed. Earlier advancement/route timings in Player.log are not treated as union-frame measurements. Raw union/whole evidence and complete coverage qualification remain in `perf-union-end.json` / `perf-whole-end.json`; scalar/spike record `performance-summary.json` SHA-256 `61960b25dd9272f6177e3a86862b71e4b3abbbbabe82cb3b529e62c25dd37fb9`.

## Findings

- #74 produced a ready Mac shop with an available cabinet-mounted register and staffed counter, retained across normal reload.
- S2 produced a native Food sale at this PC-managed shop and a strict below-basket ceiling refusal with unchanged wallet/order count.
- A completed probe receipt survived reload. Synchronous probe interruption and a serving goods/service venue require separate evidence.
- The ready, active-checkout natural-sale window failed with $0 / 0 completed orders / 0 native purchases. Native hand-back control revenue and reacquisition are recorded separately; the observed time/service reasons do not establish a code root cause.
- Supplied `stage-templates/reload.py` was copied and adapted for checkpoint/shop identity. **`from pathlib import Path` is present.** Its player-log copy tail completed. Every stage script using Path was checked for that import; no repeat of 11f's NameError or failed sed insertion.

## NOT RUN grouped

| Group | Reason |
|---|---|
| Goods / service and interruption | Documented generic gift rental selected `ba:street_fifthavenue#22`; `ready=false`, reasons `no_staffed_counter`. Original source fixture has a floor register and no `station_item`; no cabinet/station workaround was applied. No serving service fixture; no supported mid-probe suspend/interleave checkpoint. |
| E5b2–5 / F9 / F6b full calibration / F7 Pending | Required original/mixed/scale fixtures, formal acceptance, rush/profile controls or actual receipt_pending capture absent; this run retains only the named sale/hourly subset. |
| Prior evidence / wider controls | Previous-run passes are not recounted. Only the F8h check-1 shop control was repeated here; wider ownership/absence/scale matrices were not. |
| Reserved capabilities | RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY: not run and no waiting. |
| Explicit exclusions | stress10k, city-50k, RIG-FAULT, RIG-PCFIX, manifest sealing, F4f/F7e implementation; pointer/hover/focus/click playtest; PC-only Windows/overlay rows. No Windows client contacted. |

## Tooling gaps

No mac-agent command/op returned a nonzero exit in this run. Semantic first-sale/performance failures are reported above, separately from transport success.

Documented generic gift rental selected `ba:street_fifthavenue#22`; `ready=false`, reasons `no_staffed_counter`. Original source fixture has a floor register and no `station_item`; no cabinet/station workaround was applied. Exact generic build command: `python3 scripts/mac-agent op shop_fixture "$(cat <stage>/goods-args.json)" --timeout 60`; exit 0. Readiness command: `python3 scripts/mac-agent op shop_state '{"id":"ba:street_fifthavenue#22"}'`; exit 0, `ready=false`, `reasons=["no_staffed_counter"]`, register `serving.available=false`, all seven `staffed_counters=0`. No stdout/stderr transport error. Stage args SHA-256 `bec2275d364f4756ba09560f7f2d1b4dd5779db4d8aae450e78c330e1b8e0ee1`; readiness SHA-256 `8c54601581394d628312b78d79b9085865ba2a088074162ad7de8754e8594981`. The generic source is `flows/shop-fixture/rent-and-purchases.json`; only identity, size-C selection and documented exit_after differ. No additional money set. The unsaved goods fixture was not a retained checkpoint when the game stopped.

Service fixture: no reviewed serving service recipe in this Mac fixture set; the food shop-recipe type guard is not bypassed. Service S2 NOT RUN.

Mid-probe/Pending capture: console probe is synchronous and advertises no suspend/interleave arm. Completed-receipt reload was run; true mid-probe and F7 receipt_pending remain NOT RUN.

The early S2 reload template command `python3 scripts/mac-agent call console '{"line":"pc food contract boundary"}'` ran before PC activation and returned exit 0 with `Warning: [Persistent City] food contract diagnostics unavailable: no active city.` Settled reads succeeded before the managed Food probe; the transient read is not counted as active evidence. `s2-boundary-after-reload.json`.

Requested S2 checklist section absent at the built tips; PR #226 scope, plan row and implemented syntax used.

Stage reload.py: supplied template used, `from pathlib import Path` present; log-copy tail completed. All stage Path imports verified in `stage-script-path-check.json`; no NameError.

Offline document validation: `python3 tools/docs-links/check.py` exited **1**, stdout `docs/glossary.md:37: broken link ../Persistence/CompactWire34.cs` followed by `49 Markdown files checked, 1 broken relative links`; stderr empty. The untouched `d265d1fb` build worktree independently returns the same error (48 Markdown files). Both changed Markdown files pass the checker’s relative-link function. The existing glossary file was not changed. `docs-validation.json` / `docs-links.log` retain this limitation.

One offline validation snippet initially exited **1**, stdout empty, with `SyntaxError: closing parenthesis ']' does not match opening parenthesis '{'` at stdin line 6 (an extra closing bracket in a dict comprehension). The corrected snippet exited 0 and verified the owner baseline and changed-document links. No live check was blocked. This is separate from the required reload.py Path import, which was present and ran successfully.

## Fixtures

Only this run's fresh disposable FT character/checkpoints in the isolated agentmac SaveGames tree were used. Saves stay there. Hashes below are native `.hsg` bytes; raw payloads/saves are excluded from Git.

| Checkpoint | SHA-256 |
|---|---|
| FT Mac 11i F8h Food v1 | `7fa8c12ce1b6e81ac9d63f4eadf2969bbdbbf404f5a10ab79a4798b4c24ebcc7` |
| FT Mac 11i S2 managed food | `af01cb62767fbd5007fbd4928e03f8a1d1fbe72676cf20ce245d1f26b825def0` |
| FT Mac 11i S2 probe result | `8765f12e2c57413593656ec7e5eaa68f0083f3bd6d158f464821dd23e897903a` |
| FT Mac 11i native control | `3e51d253ccc79bd6a1fa5f98804084b8a955a0f224117292a539e9d00ab42fbf` |
| FT Mac 11i natural noon | `21222f8b48731ddffe4832143d5df94cafaf8c612ead3ded866434606dd638ab` |
| FT Mac 11i reacquired | `b870a650183b6a5082eaef425fc5df0f1503e40fab9ec6387553bee2eb38772a` |
| FT-PC-Mac-20261111i-956aac1 | `10eec6792a3cc3921a32a315d051835f59888d0716d35a863a821df8842efd3b` |

## Per-check results

Counts apply to the rows below; grouped exclusions are not added again, and previous passes are not recounted. PARTIAL denotes only the observed subset.

| Check | Result | Evidence / reason |
|---|---|---|
| mac-autosave | **PASS** | Suppressed before windows and after every reload; original plist restored and hashed. |
| shop-fixture-selection | **PASS** | Documented $100M size-C Mac recipe rented 24 Fourth Avenue, Hell’s Kitchen, C2, 225 m². |
| shop-serving-readiness | **PASS** | ready=true, reasons=[]; cabinet-mounted available register and all seven staffed days, retained after reload. |
| F8h-1 shop assertions | **PASS** | Native hand-back delta $45, zero PC food receipts; test prefix removed and native-owned control reacquired. |
| F8h-2 natural first sale | **FAIL** | Expected today_revenue > 0 through next-day noon; actual 0; completed orders/native purchases 0. |
| S2 food sale | **PASS** | Food, Paid 500 cents; wallet debit 500; one native order; stock -1 croissant/-1 bag. |
| S2 goods sale | **NOT RUN** | Documented generic gift rental selected `ba:street_fifthavenue#22`; `ready=false`, reasons `no_staffed_counter`. Original source fixture has a floor register and no `station_item`; no cabinet/station workaround was applied. |
| S2 service sale | **NOT RUN** | Needs a serving service-business fixture; none supplied by the documented Mac recipe set. |
| S2 below-basket ceiling | **PASS** | Food basket 500 > ceiling 0: Ceiling/Missing, paid 0, wallet/orders unchanged, native evidence empty. |
| S2 reload mid-probe | **NOT RUN** | Needs suspend/interleave support inside the synchronous call. Completed Paid receipt reload/cold continuity is supplemental evidence only. |
| F6b-2 hourly sale/calibration subset | **PARTIAL** | 14 hourly readiness/revenue/order rows and native control retained; no representative cohort/MealsPerHour median proof. |
| F7-1 sale/save subset | **PARTIAL** | Schema-1 epoch coherence and completed-probe cold continuity; no natural paid/Pending/same-minute/midnight/relocation/menu matrix. |
| F7-3 receipt_pending | **NOT RUN** | Needs actual Pending checkpoint and supported in-call capture; no fabricated state or fault arm. |
| E5b2 shop matrix | **NOT RUN** | Needs catalogue lifecycle/original-three and 0/8-shop comparison, not the single-croissant fixture. |
| E5b3 shop matrix | **NOT RUN** | Needs original-three mutation/reconciliation/storm and 8/64-shop acquiring/recovery fixtures. |
| E5b4 shop matrix | **NOT RUN** | Needs metadata override/control and held/replayed decisions with original-three shops. |
| E5b5 shop matrix | **NOT RUN** | Needs mixed original/dynamic and genuine eight/nine-offer fixtures plus independent refill/resource controls. |
| F9 acceptance | **NOT RUN** | Narrow first-sale/probe scope; no formal FT-F9/RIG-ACCEPT, paired seven-day/config/body/research/vendor acceptance or sealing. |
| Mac mod-work sample | **FAIL** | Provisional p95 8.0913 ms; 14 spikes ≥30 ms, every spike listed; whole frame separate, no save activity. |
| restore/hash/lease | **PASS** | 260 original hashes and 7 fixture hashes match; autosave restored; stopped/free; owned processes gone. |
