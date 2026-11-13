# Rig results — 13 November 2026, Mac (GR-4, GR-6/7/8, GR-9, RLO #239 at a3a6c8db)

Role `rig_test`, where `mac`, isolated `agentmac`. **Summary: 2 passed / 6 partial / 1 failed / 11 not run. Result: BLOCKED.** Counts cover the 20 requested rows below; setup and cleanup are reported separately. This is an evidence PR and does not gate a merge.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13-mac`; `<rig>` = `/Users/peterhartwieg/Coding_Projects/big-ambitions/big-ambitions-agent-rig`. Raw replies, logs, parsed snapshots, images, tools and backups stay there. The report is also copied to `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13-mac.md`.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `a3a6c8db8686f4382ac3cf941010e0b614e48805`; `git fetch origin` and `git rev-parse origin/codex/persistent-city` before building returned the briefing tip. Detached build worktree `rig-2026-11-13-mac`. No production changes. |
| Evidence branch | `codex/mac-rig-2026-11-13`; separate `rig-2026-11-13-mac-evidence` worktree. No remote name collision at creation. |
| Official rig checkout | `eea054728f546f07dc74305112fb429da8997cea` (#78), clean and read only. **HEAD is ahead of the live #77 install.** |
| Live AgentRigOps | SHA-256 `6478e76cb4c04b44f8d8ef315d4ef75fb5d912034f957696fdc4ad7a6b22691c`, checked before start and during the run; final verification is retained. No reinstall. |
| Rollback retained, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T070545Z`; documented command `python3 scripts/install-mac.py --rollback "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T070545Z"` was not run. |
| Build reference | Same isolated Mac Managed directory as [12b](rig-2026-11-12b-mac-results.md): `~/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed`. `python3 tools/build.py --game-managed <that-directory> --provenance <stage>/build-provenance.json` passed in the detached worktree. Fingerprint unknown, 27 differing assemblies. |
| Product DLL | SHA-256 `bdc95999f6b55f54da398ee8eca81079cbd7f91c5ee54e34e56cf73209556641`. |
| Environment | Every Python/.NET/build command inherited `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No full test or regression suite; production source and tests are unchanged. |
| Modpack / RLO | Full existing isolated modpack. RLO **`0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`**, not 1.0. Player.log says “Real Life Overhaul 0.45.0 is made for Build 3682.” No RLO update or vendoring. |
| Configuration | Enabled, PhysicalEnabled, NeedPurchasesEnabled true; 96 pedestrians, 24 vehicles, radius 120 m, with a 48-pedestrian comparison. ThroughTrafficShare 0.10 and HouseholdOutingsEnabled true defaults. No FoodChoicePriceWeight or FoodChoiceHouseholdWeight override; compiled defaults 1.2 and 1.0. |
| Lifecycle / visible client | Runtime 7; only endpoint 28330. Headless text/advance arms, documented background graphics and one stage-only visible launcher. That copy adds the official scripts import path and an explicit Path import, and omits `-batchmode`; it starts the same private app, validates the same install and uses the same lease/guard. Official files stayed unchanged. |
| Lease / resources | Initially stopped/free; owner `pc-mac-rig-2026-11-13`, purpose `Persistent City 13 Nov Mac rig`. First start: 50.4 GiB free / 49% reported free memory. Resources checked before each start; all start replies retained. Other sessions were left alone. Performance is provisional on the shared Mac. |
| Backup / autosave | 260 original files backed up and SHA-256 manifested before staging DLL, English locale, configuration and receipt. Original plist has no autosave keys; live native preference is 5 minutes / 3 saves. #70 `--rig-test` process guard reports enabled=true, autosave_disabled=true, suppressed=true; read after every settled reload and before measured windows. No autosave preference edit. |
| Cleanup | Original files verified: 260; mismatches: 0; autosave plist restored: True. Fixture verification and owned-process evidence are retained; remaining owned processes: []. Final lease held=False, pending=0. |

## Runs

| Segment | Observation |
|---|---|
| Fresh FT setup | Food and goods rented; independent readiness true before sales. |
| Original loop | Day-one work and day-two need travel; mid-trip reload, natural c69 Paid 500, return home. |
| Visible natural replay | Five natural receipts/debits, native orders 3→8 and revenue $20→$45. |
| Actual ceiling arm | Retail gift price $500, ready=true; 15 Ceiling refusals, native zero sales, c69/c132 zero debit. |
| Households | Workday 19:30/20:15; Saturday 15:00/15:45; normal-speed Saturday20:30–23:45, with 46 matched late-night residents. |
| Performance | Cap96 and cap48, two ~120-second rendered background windows; both p95 target misses, provisional. |
| GR-9 visuals / reload | 77 frames / 121 s; 20 native walker targets; parked hot load pending at 89%, normal recovery stop, successful cold reload and settled-body captures. |

## Rental and readiness

Both shops were created in fresh `FT-PC-Mac-20261113-a3a6c8d`, at this candidate, through stage copies of the documented Mac recipes. Each recipe used its documented $100M funding/readback before rent. Food selection was `{"building_size":"ba:buildingsize_c"}`, without an address pin. Only fixture id/name/checkpoint and that selection differ from the food recipe. Goods uses the documented goods recipe with run-specific names.

| Fixture | Picked address | Readiness before sales |
|---|---|
| `mac-13-food` / `FT Mac 13 Food v1` | **24 Fourth Avenue, Hell’s Kitchen, C2 / 225 m²**, `ba:street_fourthavenue#24` | Recipe, `shop_fixture status`, independent `shop_state`: ready=true, reasons=[], unmet_requirements=[]; staffed cabinet register, 100 croissants at $5, 300 bags. |
| `mac-13-goods` / `FT Mac 13 Goods v1` | **21 Fourth Avenue, Garment District, A1 / 75 m²**, `ba:street_fourthavenue#21` | Same independent reads: ready=true, reasons=[], unmet_requirements=[]; stocked gifts, bags and staffed cabinet register. |

No sales wait ran against a known not-ready shop. The high-price refusal arm also read ready=true before advancing.

## GR-4 — whole loop

Checks read from [PR #236](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/236).

The merged PR has no numbered In-game checks list. Its exact requested loop is: “one resident earns, plans a non-food visit, travels, buys through the purchase contract, pays and reloads”. It also requires “money conserved end to end” and “ceiling refusal leaves no debit”.

Steps used: fresh city and recipes; day-one work; save the day-two 10:10 mid-trip checkpoint; reload; let c69 finish the gift visit; compare saved wallet/plan/need state, natural purchase receipts and native ledger. c69 is Esme Thorne, source identity unchanged. Employment income rises from 0 to 370430862950 units; at minute 3490 the resident is travelling to the gift shop, journeys=2. The original continuation has natural Paid 500 at minute 3497 and is home with journeys=4 at 18:30.

An independent visible replay from that mid-trip checkpoint completes c69’s sale during load settling at minute 3490, plus four other natural sales. Five distinct purchase-v1 Paid 500 receipts and five exact 134217728000-unit discretionary debits match native books from the saved baseline 3 orders/$20 to 8 orders/$45. These are independent arms, not ten cumulative debits. The attempted 10:16–10:18 narrow window was already after those sales and its native ledger stayed at 8/$45; it is not presented as a one-order isolation.

For actual-contract refusal, reload the 08:00 checkpoint, use documented player-shop price edit to set cheap gifts to $500, read readiness true, then run at speed 1 to 10:30. `pc probe status` reads actual need-purchase status: attached=true, bought=0, refused=15, last c132 refusal=Ceiling, reason **“Mandatory lines 50000 are above the ceiling 8000”**, receipts=[]. Native books remain 0/$0; c69 and c132 wallet balances and discretionary accounts are exactly unchanged. c69’s need is deferred and its plan marks Refused. The earlier c302 zero-ceiling diagnostic probe is supplemental; it is not substituted for this contract arm.

The whole population payload retains schema 1. Saved-wallet arithmetic is checked offline. Commands/replies are retained under `isolate-*`, `contract-ceiling-*`, `natural-visible-aggregate.json`, `loop-verification.json` and the named payloads.

A later cold load of `FT Mac 13 GR9 parked` also preserves c69’s exact purchase-v1 Paid receipt at minute 3497, full wallet and identity from the pre-reload snapshot. This is paid-loop continuity, independent of the supplemental sale-probe record. `parked-reload-verification.json` checks the exact saved strings and records.

| Check | Result | Observation / evidence |
|---|---|---|
| whole loop | **PASS** | c69 earned, planned, travelled across reload and bought naturally; five new 500-cent receipts match five wallet debits and native +5 orders / +$25 in the visible replay. Actual purchase contract ceiling arm: 15 refusals, no paid receipts, native 0 orders/$0; c69/c132 balances and discretionary accounts unchanged. |

## GR-6/7/8

Checks read from [PR #237](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/237).

All three numbered checks were kept distinct. NeedPurchasesEnabled=true, HouseholdOutingsEnabled=true and ThroughTrafficShare=0.10. The inspector text and business-panel text require UI observation; console contract output alone is not substituted for those strings.

Check 3 follows the PR expected observations: the weekday pair are an active shared outing, whereas the weekend pair are co-located ordinary park visits after Late cancellation. This is not represented as a successful weekend shared plan. The nightlife count requires Active, AtPlace equal to the planned nightclub, start >=21:00 and Sunday in the days-off mask; it excludes alternative destinations. c123 is one exact matched row.

| Check | Result | Observation / evidence |
|---|---|---|
| 1. Non-food feedback text | **PARTIAL** | Setup, natural purchases and refusals observed. Ctrl+F8 opens the business panel, but lower feedback text and selected inspector text were not verified; pointer/scroll attempts did not change controls. |
| 2. Through-traffic queues | **PARTIAL** | Default share 0.10 generated through traffic: 2 active / 6 generated in a captured line. No controlled rush-hour drive/queue proof; through-car inspector has no body. |
| 3. Evenings and households | **PASS** | Workday 20:15: c147/c99 (13 Second Avenue) at Global Features, 5 Sixth Street, both Active. Saturday 15:45: c1/pc:39 (15 Fourth Street) at the same Native park; outing marks Late, so these are ordinary visits. Saturday 23:45 after a speed-1 20:30–23:45 window: 46 residents Active and AtPlace at their intended nightclub, with Sunday off; c123 at Luminous Lounge, 19 First Avenue, planned 21:33–00:03. See household/nightlife parsed snapshots and resident readbacks. |

## GR-9

Checks read from [PR #238](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/238).

All ten requested rows are accounted for below. Body images and diagnostics do not establish player-car collision or pedestrian-layer behavior by themselves.

The focused visible client uses `screen_capture`; the failed background attempt used the RenderTexture path. The successful two-minute images are retained as `fade-0000.png` through `fade-0076.png`, with native frames and elapsed seconds in `fade-frames.json`. They do not resolve a subsecond fade. The 20 wardrobe images retain opaque native walker handles, not a stable Persistent City resident/donor mapping.

The first parked reload did execute the documented menu transition, then remained at 89% with load.status=pending. It was stopped normally; the resulting polling No bridge error is recovery-related, not an independent crash. A separate cold comparison is reported in row 3. The three saved car ModelId values are empty and PaintSeed values are zero; these fields alone cannot verify native model/paint.

The cold load completed, and the first image set still showed the Preparing Persistent City overlay. Those images are not treated as settled physical evidence. A subsequent speed-1 wait required a live Population line with physical=True and parkedCars>0, then paused for three additional views. The selected logical Car records remain equal across cold reload, but native model/paint and body ownership are not established by the images. `parked-settled-line.txt` records readiness. Median capture spacing in the 121-second fade sequence is 1.489 seconds.

| Check | Result | Observation / evidence |
|---|---|---|
| 1. Walker fade-in | **PARTIAL** | Busy-street focused-client observation: 77 PNGs over 121.12 seconds at default physical settings. Modal obscures the centre; ~1.6-second frame spacing cannot resolve the expected 0.75-second fade or exclude pops. |
| 2. Mode-change fades | **NOT RUN** | No resident-to-own-car and own-car-to-parking pair was followed through both transitions. Movement counters and unrelated walker follows are not transition proof. |
| 3. Parked cars across reload | **PARTIAL** | c172/c368/c87 full saved Car records (IDs and curb positions) are identical after a cold reload. Three before, early-cold and physically-settled-cold views retained, but the modal and absent native-body identity binding prevent a three-model/paint match. The quit-to-menu load remained at 89% / pending for about five minutes, then was stopped normally; cold load succeeded. Owner departure/destination fade not observed. |
| 4. Own cars / no native borrowing hooks | **PARTIAL** | After clean load, hook reports contain no physicalvehicles or physicalintersections owner. Live ownCars=1, heldGroups=Personal, retired 7→18 (loadsRetired 0→10). Literal log criterion is not wholly met: DrivenRides logs Patch DrivingAI/GameSpeedController/TrafficManager lines. Taxi/service driving was not visually established. |
| 5. Wardrobe and looks | **PARTIAL** | 20 distinct nearby native walker handles followed/hovered/captured while paused. Camera Tools dialog obscures faces and clothes; no persistent resident/donor binding was supplied, so clothing-family, same-donor differences and same-resident reload appearance remain unverified. |
| 6. 96/48 performance | **FAIL** | Provisional shared-host target miss: cap96 mod-work p95 5.2064 ms, cap48 5.0399 ms (target <5). Complete/valid union timing, no save work; 1/2 mod-work spikes >=30 ms. Physical-body log samples were 72/61 and 48/48; a sustained 96-body scene is not proved. |
| 7. Native/player traffic against own cars | **NOT RUN** | No physical player-car fixture/control sequence was established. The advertised drive operation is simulated; it was not used as physical junction/collision proof. Initial state other_vehicles=[] readback retained. |
| 8. Parked occupancy | **NOT RUN** | No controlled two-resident same-home curb test against the player car and bollard/lamp was established. Logical parked points or isolated car photos do not establish this occupancy criterion. |
| 9. Own cars stop for traffic/player car | **NOT RUN** | No stopped player car / own-car red-light approach / adjacent turn sequence was established. No stop-distance or rear-sweep claim. |
| 10. Own cars stop for walkers | **NOT RUN** | No controlled player-on-foot / native-or-mod walker crossing in front of an identified own car was established. Scene pedestrian layer was not exposed/read; no stopping result or layer name is invented. |

**Performance — provisional on the shared Mac.** Both windows used the same workday 18:30 checkpoint, radius 120 m and speed 1. No screenshots or explicit saves ran inside them. Native preference reads were 5 minutes / 3 saves; #70 suppression was true before and after each window and after reload. Save counters stayed at zero, no saving condition was recorded and the recorder reports valid timing with complete coverage (61 bound entry points). Its qualification excludes coroutines, worker threads and callbacks registered through other native events. These windows test configured caps; sampled bodies do not establish sustained 96-body saturation.

| Cap | Seconds / frames | Mod-work p50 / p95 / p99 / max (ms) | >=30 ms | Whole-frame p50 / p95 / p99 / max (ms) |
|---|---|---|---|---|
| 96 | 120.747 / 3196 | 1.5508 / 5.2064 / 9.2925 / 39.527 | 1 | 33.394 / 49.591 / 141.9401 / 281.2407 |
| 48 | 121.329 / 3243 | 1.1844 / 5.0399 / 7.1752 / 33.483 | 2 | 33.4168 / 36.962 / 140.8275 / 280.5786 |

Every mod-work spike >=30 ms (the record lists all three):

| Cap | Unity frame / window index | Mod-work ms | Whole-frame ms | Dominant component | Condition |
|---|---|---|---|---|---|
| 96 | 5648 / 2263 | 39.527 | 62.8289 | PopulationRuntime.Update | `walkers=96-speed=1 / hour=18` |
| 48 | 4661 / 1345 | 33.483 | 65.5919 | PopulationRuntime.Update | `walkers=48-speed=1 / hour=18` |
| 48 | 5610 / 2294 | 31.7521 | 56.2572 | PopulationRuntime.Update | `walkers=48-speed=1 / hour=18` |

48 versus 96 configured cap: mod-work p95 decreases by 0.1665 ms (3.2%). Whole-frame intervals are reported separately; their >=30 ms counts are 2982 / 3108 at caps 96 / 48. No performance code was changed. Raw files: `perf96-end.json`, `perf48-end.json`, their save/autosave reads and `perf-summary.json`. The recorder condition names walkers=96/48 denote configured caps, not measured body counts.

## RLO #239

Checks read from [PR #239](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/239).

All six numbered checks are NOT RUN because the required installed version is missing. The live log and saved source version identify 0.45.0. `pc capability status` says **“Persistent City RLO overrule: no Living population in this RLO release”**. Its optional shop checkout is active, but that does not establish the 1.0 overrule. The advertised rlo_option schema supports only social_media/social_media_notices, not Living population. No legacy toggle or native-sales result is promoted to a #239 pass.

| Check | Result | Observation / evidence |
|---|---|---|
| 1. Living on / owned food sales | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |
| 2. Living off | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |
| 3. Mid-session toggle | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |
| 4. Crowds | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |
| 5. Traffic and RLO scenes | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |
| 6. Native-owned non-food / basket extras | **NOT RUN** | Required RLO 1.0 is absent. Installed RLO is 0.45.0+b12d3581860ce380397223a2790fcce8b72b3482; no Living population in this release. No replacement installed. |

## Findings

1. Both documented shop fixtures passed readiness before sales/refusals.
2. The natural non-food loop, actual contract Ceiling refusal and exact resident debits are present in live game evidence.
3. RLO 1.0 checks are blocked by installed version 0.45.0.
4. Native keyboard opens the PC panel; pointer/scroll observation remains incomplete. No code root cause is assigned.
5. Provisional mod-work p95 exceeds 5 ms at both configured caps; all three >=30 ms mod-work spikes are listed separately from whole-frame timing.
6. Focused screenshots succeed, but the Camera Tools update modal and frame spacing prevent full fade, wardrobe and parked-car visual proof.
7. Cold paid-loop continuity: c69 identity, full wallet and exact natural Paid receipt survive reload; all 24 payloads keep schema 1 and all 22,968 wallet arithmetic checks pass offline.

## NOT RUN grouped

- GR-9: 2. Mode-change fades, 7. Native/player traffic against own cars, 8. Parked occupancy, 9. Own cars stop for traffic/player car, 10. Own cars stop for walkers. Reasons are in the per-check rows.
- RLO #239: 1. Living on / owned food sales, 2. Living off, 3. Mid-session toggle, 4. Crowds, 5. Traffic and RLO scenes, 6. Native-owned non-food / basket extras. Reasons are in the per-check rows.
- Through-car inspector “Passing through the city”: no physical body/binding, as the PR states; not counted as an extra failed row.
- The optional 12b reconfirmation was not run. Historical check trees were not recreated. Windows and normal-profile checks are outside this Mac slot.

## Tooling gaps

Game-operation failures below retain their exact command, exit and error. Full stdout/stderr or JSON replies are in `<stage>/failures-later.jsonl`, with argv/exits in `commands.jsonl`. Successful input delivery is not treated as successful UI interaction.

| Command | Exit | Exact error |
|---|---|---|
| `python3 scripts/mac-agent call walk '{"place": "exit"}'` | 1 | `You're not inside a building.` |
| `python3 scripts/mac-agent call ui '{"op":"escape"}'` | 1 | `Nothing is open to close (Esc would open the pause menu).` |
| `python3 scripts/mac-agent call ui '{"op":"escape"}'` | 1 | `Nothing is open to close (Esc would open the pause menu).` |
| `python3 scripts/mac-agent op camera '{"action":"look_at","target":{"kind":"position","x":176.14,"y":0.09,"z":-36.35},"offset":{"x":0,"y":14,"z":-14},"seconds":120}'` | 1 | `Invalid view number` |
| `python3 scripts/mac-agent call time '{"pause": true}'` | 1 | `time works in the city only; the game is at the loading now.` |
| `python3 scripts/mac-agent op camera '{"action": "follow", "target": {"kind": "walker", "id": "walker-031287e1cef144e0ac432d017ab2f124"}, "offset": {"x": 0, "y": 4, "z": -5}, "seconds": 15}'` | 1 | `ops_view_camera_busy` |
| `python3 scripts/mac-agent op camera '{"action": "follow", "target": {"kind": "walker", "id": "walker-b50e41c25a2048abb57ed2b5760d176c"}, "offset": {"x": 0, "y": 4, "z": -5}, "seconds": 15}'` | 1 | `ops_view_camera_busy` |
| `python3 scripts/mac-agent op capture '{"action":"take"}'` | 1 | `ops_timeout_do_not_retry` |
| `python3 scripts/mac-agent call time '{"pause": true}'` | 1 | `No bridge at http://127.0.0.1:28330: start the local Mac fallback with tools/agent-tests/mac-agent start in the Driven Rides checkout; no tunnel is needed` |
| `python3 scripts/mac-agent health` | 1 | `No bridge at http://127.0.0.1:28330: start the local Mac fallback with tools/agent-tests/mac-agent start in the Driven Rides checkout; no tunnel is needed` |
| `python3 scripts/mac-agent health` | 1 | `No bridge at http://127.0.0.1:28330: start the local Mac fallback with tools/agent-tests/mac-agent start in the Driven Rides checkout; no tunnel is needed` |

CUA batch attachment failed with NSCocoaErrorDomain Code=256 / NSOSStatusErrorDomain Code=-600, procNotFound: no eligible process with specified descriptor (outer server error -10005). The stage-only visible launcher resolved attachment and keyboard Ctrl+F8. Clicks/scrolls did not change the Camera Tools update button or PC panel controls in captured views. A temporary zoom of the owned game window was reversed.

The first background capture exited 1 with ops_timeout_do_not_retry. The next pause and health commands found no bridge. Player.log records System.InvalidOperationException: The TransformStreamHandle cannot be resolved, followed by the Burst standalone application-abort message. Status then says running=false. The stack includes AgentRigOps.ViewCaptureRenderer.Render / ViewWorld.CaptureFrame. This records the sequence, without attributing a code cause. Focused screen_capture succeeded after restart.

The parked quit-to-menu load remained at 89% with load.status=pending for about five minutes. It was stopped normally for recovery. Its last No bridge polling error follows that deliberate stop. The cold load completed; early views still showed Preparing Persistent City, so a further wait required physical=True and parkedCars>0 before the final images.

Physical own-car identity/pose, physical player-car controls and scene collider-layer readbacks were not established by the advertised ops. The advertised drive path is simulated. These gaps block controlled traffic and pedestrian-crossing criteria; diagnostic counters cannot replace them.

Recoverable setup/input errors: the shop recipe had already left the player outside when walk exit was attempted; ui escape twice found no open panel; camera seconds=120 was refused (corrected to 110 with a longer request deadline); replacing an active follow refused until explicit camera release. The stale paid-reload completion was corrected as described below.

A local one-line performance parser treated a mapping as a list and raised TypeError: string indices must be integers, not 'str'. The corrected mapping-based parser passed. A metadata tool request had SyntaxError: Invalid or unexpected token before shell execution; quoting was corrected and verification rerun.

Docs validation: python3 tools/docs-links/check.py exited 1: docs/glossary.md:37: broken link ../Persistence/CompactWire34.cs; 53 Markdown files checked, 1 broken relative links. The same link is in candidate HEAD and git cat-file -e HEAD:Persistence/CompactWire34.cs exits 128 with fatal: path 'Persistence/CompactWire34.cs' does not exist in 'HEAD'. Both changed documents have zero broken relative links. The pre-existing glossary was left outside this evidence change.

Stage corrections: the initial paid-reload wait accepted a stale completion, then `time` refused while loading. The wait now requires the requested load name, completed city state and settled PC activation. A continuation script raised **`NameError: name 'x' is not defined`** before any native command; it was corrected. A local sed insertion refused **`command a expects \ followed by text`**; the stage script was fixed with a patch. A local Pillow availability check raised **`ModuleNotFoundError: No module named 'PIL'`**; no package was installed. These are not game assertion failures. Every executed stage script using Path includes **`from pathlib import Path`**; the supplied 12b template was read before staging. AST/import validation is retained.

## Fixtures

All checkpoint files belong to this run’s new isolated FT city; they remain in the isolated profile. No normal save was opened, copied or changed. SHA-256 was taken before restore and rechecked afterwards.

| Checkpoint | SHA-256 |
|---|---|
| `FT Mac 13 Food v1` | `bad2b39e2b1210bfaa739521a0c6e37bc63608ea77dba4e38a6473c99053c6e7` |
| `FT Mac 13 GR9 parked reloaded` | `5b547281a0cdb09e0ff623248c6b93e1ec9dcd2cf7092cc6cb2b5ab022d04fed` |
| `FT Mac 13 GR9 parked settled` | `37a2fe1ad2789e0d7d0b11b3dd7355de5db8aac1eb6ad0aa245988284fadc780` |
| `FT Mac 13 GR9 parked` | `66abb682cabcfd0dcfff2b2c58b10d46491c60d6d81060f0edfec63835d4dff4` |
| `FT Mac 13 Goods v1` | `81edeafdacac3794b612beb198bc1934f064c166719cb03fe4781ef46c3d7063` |
| `FT Mac 13 Loop baseline` | `14175489da760b7fef33cb9b87470c572b32a8ec9a7945737b9a966c39c0bc58` |
| `FT Mac 13 Loop nine` | `23332c0e911f8739afc5422809ac5f507adaf3ec3e23997c2fe6027c31468e5c` |
| `FT Mac 13 Loop paid probe` | `2cb86af0ab2b8bac813b5814132320d21b8cc25cf5d775a1cefb28dce20948a6` |
| `FT Mac 13 Loop paid reloaded` | `7549aeda02e3f7f691b1d61c2bc78c166f034945e37cc73158606fb43e5bf61a` |
| `FT Mac 13 Natural c69 paid` | `706702eb7623ab663bba0560effeff0a310f934a6e02ae8bd5d0488c4745808d` |
| `FT Mac 13 Saturday 1400` | `b21a17482fd6a4d88310d9985489d5aec02e4523c9c8cbe8e9239049c7aa6e0a` |
| `FT Mac 13 Saturday 1500` | `f80e5ded9f0df41c369ee1404cd15271e67965759d5d715d4fabc73f70858473` |
| `FT Mac 13 Saturday 1545` | `54a21b47e5eabe203daac53d30a94b6ca178edf43447b32d4362a23fafbeba0d` |
| `FT Mac 13 Saturday 2030` | `ee59031c74ae5f462886a40dca48901f5bbbb1c0dede22018e00bf7d578d41ae` |
| `FT Mac 13 Saturday 2345` | `56d6de389e3080264d8d2401598a43ecf2c751bfcb1ff293fe1afe7bd5aec6df` |
| `FT Mac 13 c69 after arrival` | `57ee68f915beb446cc1616bcfb26dcccdf8e17fb8b970905117fc793711d2924` |
| `FT Mac 13 c69 before arrival` | `32b66267cbaf3dbbadf992cf5d03dcdcabe72991f2fb7c64d0407ed1ab5bb3f4` |
| `FT Mac 13 ceiling morning` | `50f3039d7c52cf9ed3b992cf3cee87c3f747e9408944b2db576170c3e6d6e71a` |
| `FT Mac 13 day2morning` | `30b95ffee46465dd560cc5cd1992f2b26c744b499e419476d0edab32f3676609` |
| `FT Mac 13 evening` | `811598f69f9a307afd91b33a4b941191b574424525788164b84cc118485ed178` |
| `FT Mac 13 native1010` | `3fc2435006b71d11f5cf7bef6d5a9b38cb8508193b8e029f62f70f869ab98df2` |
| `FT Mac 13 need ceiling result` | `5ba7f4040dcf64f07d0b99b53d0066c789c1fa653cfb71f3114c53b216f0edef` |
| `FT Mac 13 nightlife2300` | `3ba669424cd193f522e70b9ce16604d506dda4ea36e07b4421630ccceec38231` |
| `FT Mac 13 workday1830` | `3f7294f4cf0a341568e088e4c37eb163c39a0953d459bfd047e64641817a7488` |
| `FT Mac 13 workday1930` | `44826d5ce1bd2b59e4b7b64aa05455465408580df04d33ebfcc79eac20f32ae9` |
| `FT Mac 13 workday2015` | `46c41d4af2c914565fd623b40ba6f669c2a14fff4bf0922df414829f2e29846f` |
| `FT-PC-Mac-20261113-a3a6c8d` | `621d52fe28568a710eeb88e67da2a56af62027ad420b3b01b1b5494d5d7f057e` |

## Evidence

Raw JSON, native logs, screenshots, binaries, saves and the backup manifest remain outside Git. `evidence-index.json` maps retained artifacts to bytes/SHA-256. Snapshot arithmetic is offline analysis of game-produced data; it is separate from native readbacks and visual observations.

| Evidence | SHA-256 |
|---|---|
| `build-provenance.json` | `35ce34ecf088f3bd90bb3ec15f8fa4db4a6f8afb4ceacc3631512800f5ee1083` |
| `loop-verification.json` | `c0e2a3369191696c977c5dec4c403f252cc68792438572d7b364d24bf250ad65` |
| `natural-visible-aggregate.json` | `98f9ea12796c6c809079c2b1a0ee27093af967221d1eac0158820f9688a33c19` |
| `contract-ceiling-receipts.json` | `8e53e8bbbe6e914ff04c4c5486ef927cab2b3a8b7f35329aa62cf13417876992` |
| `contract-ceiling-before-payload.json` | `1b8bbee5a8592b17e21f22a34f93db75942043113a51f423ad9dcb7ecfe73d7b` |
| `contract-ceiling1030-payload.json` | `350ddabae4f7c86cb2f495b3001491f47a473d5c2632cc529914f644d7b30bd1` |
| `workday2015-households.json` | `ad741cfce765a358acd0eaf2003359ba0d5c3b6e63ce7a19f6670e59d52df3f6` |
| `saturday1545-households.json` | `3b273c3c0a507da56ced0f0cd9a373bc2dd62838b207e73b014fda0fc95fd6f5` |
| `visible-launcher-provenance.json` | `4c9a1c985b3cdbaa76a528607252b2121bd80e95be86472912f4f9171fea9366` |
| `failures-later.jsonl` | `e9dfbdc2964851277dd44bb185385bedb0922a73a3480c53d3f8d3547aee6495` |
| `restore.json` | `222a39f5c934e8d1caa07ed922d736fd1f240a365de5ba0e7be98d20953f74e1` |
| `fixture-hash-verification.json` | `40cb4d22405abcee7ea3f4c2eb18183761abc09b78ca197eb6d5d60ceb91c2bb` |
| `owned-process-verification.json` | `f86ebe129a35042e96e1f95ce855e8534148462e854cea471543a15b87c47fe0` |
| `evidence-index.json` | `695b9a969aaa9f1caa4fb6e0b5aa03cca2f5e7448f7250262ba0dbf6a43ce469` |
| `saturday2345-households.json` | `c2c0260d3aaf04ac94439aa8648db64d9fdb928f6abcf687b84a58db240f69a1` |
| `nightlife-matched.json` | `81a37908ccd156fafbc4eb0bc10ab4ed8c8d43e847b1b8557eba1abf07df3f6b` |
| `all-snapshots-verification.json` | `189abe459eac5e3e33fa5edca0accb38e37c2a8ed7d84635ab6a128a75c9b8a1` |
| `perf96-end.json` | `73f0c12a756e6964f4f2eb23502e36c5b1bac2a69f0b7934f16ab2729ae6eb81` |
| `perf48-end.json` | `58f89ce164ef1f3342470eebb394e7a82bf32290c99673d5a6dfe7b86659288e` |
| `perf-summary.json` | `f4b3cc206ad2856b6259bef12488fac70176c4d23d1a5d8684364c95d4099bb4` |
| `player-background-capture-failure.log` | `6192d85b440798a4b77421976dffca67e7fa7c59fb25aa61e206d7d009496976` |
| `fade-frames.json` | `26a75e0fc1a6264c1ddbb7849bb396ed373b04afe5ba9411fe034e208d2e04ba` |
| `wardrobe-targets.json` | `d03c257464673701144dc8a0c1429d1e7c6a31d920d31b348df2a0215654b1ad` |
| `parked-targets.json` | `c68aa2ef38a94d0614baeb8d61699435f27361fb5405454d568cf862a0611df5` |
| `gr9-hooks.json` | `20c35532b9fb8009c444b788ed8b594ac55166eeb53f6c987e4918549b33e1fb` |
| `perf96-hooks.json` | `ba5e057b4e448bbe338c6e8c060c9f32bc5c38f53fbb7d453450ef62c2808ac5` |
| `player-parked-hot-load-pending.log` | `7b72bbb5f578657058e9d186016c0b65699878417fbbf86ab23915331dc0140a` |
| `parked-reload-verification.json` | `376f47151c0de88d49e358aeefddf72b7ce01ad1d3457c4e90273258fb481f90` |
| `gr9-parked-before-payload.json` | `9b9351acb91bc5ad1507862997c1545f68ac568541e75a565d784da6e9fabf1d` |
| `gr9-parked-after-payload.json` | `ad644ab0fbb7eea4e7029b147fa86172d097f5ea32340db76e6c1c7149b9b8d3` |
| `parked-settled-line.txt` | `6f6dc206b574edd426df2bc907115fd9521023c7fdbc4cbf7670129d2463933c` |
| `path-import-verification.json` | `88296caebb34d7be7130b55c8feac62c6b294cc4c6d5b4596901d68e78f3a9df` |
| `final-verification.json` | `7b70f62ce40b4ffca09f569ba8e82eb1ca3b1585e00787bed2569dc9ba22df30` |
| `backup-manifest.json` | `c4c4eb591700e577814831fe98e581f98fa3d1b36fd52c9e795d536284a5dfcb` |
| `fixtures.json` | `3e7b9f4e045def17a457bf7ad45954ee6b5144a993f1d828090176ed36680af0` |
| `cleanup-status-final.json` | `d9599ae5368c9ca0284ed5567ca23b7832064d357928a68e7da87691c1f229e4` |
| `commands.jsonl` | `f0a3182e42d44bfbaf39c933bd926c34e44a171e42a9f56f7d45f65780ac1d81` |
| `ops.json` | `4c679e9ee722fc5ffc795159cb6d0f834f938a35a1286ae0180f7690839b9f7c` |
| `docs-validation.json` | `0ae8de9ce24c86f736267944c14ec0d0d5fedf650fedd17caeed0d9013c26926` |

## Per-check results

Counts: **2 passed / 6 partial / 1 failed / 11 not run**. The group tables contain all 20 requested rows. Setup/readiness, schema-1 observations and restore/hash/lease verification are outside these counts.
