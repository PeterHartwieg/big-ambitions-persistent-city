# Rig results — 12 November 2026 (a), Mac (#76 mid-probe checkpoint, S2 mid-probe reload and F7-3, tip 0eb780b5)

Role `rig_test`, where `mac`, isolated `agentmac`. Candidate `0eb780b53185cb0c8ce9fcddece199a6223313dc` was fetched and verified before build and again after cleanup; the tip did not move. Read [11j](rig-2026-11-11j-mac-results.md) first and followed the verified official Mac, readiness, cabinet-register, food-fixture and #76 checkpoint procedures. No production code change, rig reinstall, rollback, Windows contact, normal-profile access, release, version/changelog, merge or issue creation. Other harness sessions and their worktrees were left alone.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-12a-mac`. `<rig>` = the official agent-rig checkout. Raw replies, command exits, scripts, logs, parsed saves and backups remain in the stage. Only this report and an appended checklist link enter Git.

**Summary: 7 passed / 0 partial / 1 failed / 3 not run. Result: BLOCKED.** Counts cover the 11 named rows below. #76 captures and saves work in game. S2 mid-probe reload **FAILS** its “no half sale” assertion: the exit cut loads one native order / $5 with an unchanged resident-wallet payload and a Pending receipt; Persistent City refuses the cut, so reconciliation does not run. F7-3's requested capture-only `receipt_pending` arm **PASSES**. Broader historical matrices are excluded from these targeted counts. In-game proof gates no merge.

## Build and rig

| Item | Value |
|---|---|
| Candidate / isolation | `0eb780b53185cb0c8ce9fcddece199a6223313dc`; detached build worktree `rig-2026-11-12a-mac`, independent evidence worktree `rig-2026-11-12a-mac-evidence`. Main working tree stayed clean. Product DLL equals 11j's bytes. |
| Official rig checkout | `f47367565d5e0cb103930aec47090110864945fb` (#76), clean before and after; read only. |
| Live AgentRigOps | SHA-256 `728d094450dff0aca54c35ddcbf467a18197789ac3a36364ba8c3529a82e65a9`, verified before start and after cleanup; live health says ready with this hash. `op ops` in the FT city lists `probe_checkpoint`; initial read is disarmed, unhooked, sequence 0. |
| Rollback retained, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T005804Z`. No update/install-mac/rollback command ran. |
| Build / compile reference | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; passes, same isolated Mac reference as 11j. Fingerprint set `unknown`, 27 differing assemblies; no Windows compatibility assertion. |
| DLL | Built/installed SHA-256 `1c840d1c4b63a651c186b144ed64d3ba5d81951250b7547c9d0a8d05d3913bad`. `NativeSaleProbe`, `SaleProbeCommand`, `pc probe status`, `travelClockRate`, `walkingMetresPerGameMinute` confirmed before checks. Schema remains 1 in all four parsed snapshots. |
| Environment / checks | Every Python/.NET/build command used `PATH="/opt/homebrew/bin:$PATH"`, `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No source/test changes, full suite or regression suite. Stage scripts only; no performance window taken. |
| Profile / mode | Private Apple Silicon app, isolated `com.hovgaardgames.bigambitions.agenttests.mac`, port 28330, runtime 7, headless `--rig-test`, full existing modpack. Candidate DLL/English locale and enabled/physical config (48 pedestrians, 24 vehicles, radius 120 m); remaining compiled defaults. |
| Lease / resources | Initially stopped/free; owner `pc-mac-rig-2026-11-12a`, purpose `Persistent City 12 Nov Mac rig a`. Immediately before start: 59.2 GiB disk free, 68% reported free memory. Same owner throughout. |
| Backup / original autosave | 260 files copied and SHA-256 manifested before changes: isolated ModsLocal, profile top-level files, preferences plist and install receipt. Original plist has no autosave keys; native op reads 5 minutes / 3 saves. Process-only #70 suppression used, no preference edit. |
| Cleanup | Normal stop; 260/260 original hashes and 6/6 disposable fixture hashes match. Original preferences/autosave restored, lease free/pending 0, owned game PID 24734 and its helper gone. |

## Runs

| Segment | Fixture / result |
|---|---|
| Fresh city / rental | `FT-PC-Mac-20261112a-0eb780b`; size-C selection, $100M documented recipe, food checkpoint `FT Mac 12a F8h Food v1`; independent readiness before advancement. |
| Reload / probe setup | Supplied `reload.py` template, actual shop id, `from pathlib import Path` retained; log-copy tail completes. Short day-one 08:00→09:00 setup at speed 20, then paused. Pre-probe saved at minute 1980.0099983215332; no next-day-noon rerun. |
| Enter cut | Sequence 1 reaches Sell entry, Pending=1, native Default cut save returns and becomes idle. Probe completes Paid; native cut reload has 0 orders / $0, unchanged wallet payload, PC refused. |
| Exit cut | Reload synchronized pre-probe first; sequence 2 reaches Sell exit, Pending=1, target_threw=null, cut save returns and becomes idle. Native cut reload has 1 order / $5, unchanged wallet payload, PC refused. |
| F7-3 capture only | Reload synchronized pre-probe again; sequence 3 reaches Sell entry without saving, Pending=1. Probe Paid, accounting stays active; normal completed save has Paid receipt, debit and coherent epoch. |
| Cleanup | Normal stop and restore/hash/lease/process checks complete. Disposable saves retained as instructed, including refused cuts. |

## Shop rental and readiness diagnostic

Copied `flows/mac/f8h-food-shop.json` into `<stage>/recipe.json`. `spec_from` remains `flows/fixtures/mac-food-shop.json`; only fixture identity/checkpoint and `"selection":{"building_size":"ba:buildingsize_c"}` differ. No pinned address. The recipe sets and reads back $100,000,000 before native rental; no other money setup, campaign or rival-finance write.

Exact commands from `<rig>`, with the environment/owner above:

```sh
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#10"}'
```

Both exit 0. Selected **10 Fourth Avenue, Hell's Kitchen, C2, 225 m²**, business `ba:street_fourthavenue#10`; neighborhood `ba:neighborhood_hellskitchen`, size `ba:buildingsize_c`, version 2. `rented=true`, real-estate `owned=false`. Independent `readiness-rented.json` reports `ready=true`, `reasons=[]`; croissant `units=100`, `reserve_units=0`, `price=5`, offer `ready=true`, `reasons=[]`. Seven open days each have one staffed counter. This read preceded sales advancement or any probe, and readiness was checked again before both cuts and F7-3.

The prescribed equipment attachment is unchanged: cash register `on: ba:itemname_moderncounter`; customer-service shift `station_item: ba:itemname_cashregister`. Actual readback: `ba:itemname_moderncounter` id `F5aAKl4GPUSRN3eKfdKNcQ==`, position `{'x': 897.0, 'y': 1.1920929e-07, 'z': -3.0}`; `ba:itemname_cashregister` id `lsZm3HV5P0e7pMGXou+EeQ==`, position `{'x': 897.0, 'y': 0.8800001, 'z': -3.0}`. Register `lsZm3HV5P0e7pMGXou+EeQ==` is in `staffed_registers`, `serving.available=true`, `point_of_sale=true`, with 300 paper bags. Hours/Default register shifts are 09:00–18:00 all seven days. No shop-log hook, floor-register workaround or fixture retry.

#230 side-effect read `op pcstate '{"action":"read","property":"FoodDiagnostics"}'` after reload reports `travelClockRate=0.3333333333333333`, `walkingMetresPerGameMinute=6.0`, `active=true`. `pc probe businesses` identifies this run's place `address:ba%3Astreet_fourthavenue:10:shop`, FullService / Food, `foodOwned=true`, `open=true`. Probe resident `c0` has wallet headroom; requested ceiling 10000 cents, basket 500 cents. No pre-existing probe receipt in the pre-probe save.

## Mid-probe checkpoint: enter and exit

Exact command sequence (all completed in the foreground, all exit 0):

```sh
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent call console '{"line":"pc probe businesses"}'
python3 scripts/mac-agent call save '{"name":"FT Mac 12a S2 pre-probe"}'
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:10:shop 10000' '{"at":"enter","save":{"name":"FT Mac 12a S2 cut enter"}}'
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent op pcsave '{"action":"take","name":"FT Mac 12a S2 cut enter","timeout_s":180}'
python3 scripts/mac-agent call load '{"name":"FT Mac 12a S2 cut enter"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call load '{"name":"FT Mac 12a S2 pre-probe"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:10:shop 10000' '{"at":"exit","save":{"name":"FT Mac 12a S2 cut exit"}}'
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent op pcsave '{"action":"take","name":"FT Mac 12a S2 cut exit","timeout_s":180}'
python3 scripts/mac-agent call load '{"name":"FT Mac 12a S2 cut exit"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call load '{"name":"FT Mac 12a S2 pre-probe"}'
python3 scripts/mac-agent autosave
```

Each load was polled until the requested load name/status `done`, scene `city` and save name matched, then autosave verified and time paused. Per-load native ledger, shop state, capability, epoch, food and probe-status reads are retained. The same c0/minute is valid in these independent arms because each starts from the pristine pre-probe save, which has no probe receipt. No `AlreadyRecorded` or `ops_probe_checkpoint_not_reached` occurred. An early pre-exit status read briefly said no active city; the subsequent probe reached Sell and completed normally. No failed mutation was replayed.

Both save captures have `matching=1`, `pending=1`, `truncated=false`, `later_calls=0`. Their identical captured entry is:

```text
key=persistentcity:sale-probe-v1:c0:address%3Aba%253Astreet_fourthavenue%3A10%3Ashop:1980
status=pending
value=pending|Food|c0|address%3Aba%253Astreet_fourthavenue%3A10%3Ashop|500:ba%3Aitemname_croissant
```

| Observation | Enter cut, sequence 1 | Exit cut, sequence 2 |
|---|---|---|
| Hook point / UTC | Sell prefix / 2026-11-12T01:11:00.3082640Z | Sell finalizer / 2026-11-12T01:13:07.7470730Z; `target_threw=null` |
| Capture clock | Day 1, 09:00.00999832153 | Same |
| Native effect at cut (loaded ledger) | 0 completed orders, $0, 100 croissants / 300 bags | 1 completed order, $5, 99 croissants / 299 bags |
| Resident debit at cut | None; complete saved population payload equals pre-probe | None; complete saved population payload equals pre-probe |
| Save reply | `returned=true`; client waits for `in_progress=false`, `compress_thread_alive=false` | Same |
| Saved receipt / markers | Pending; `save-epoch_v1=failed:3`, `accounting-stop_v1=save-epoch-failed` | Same |
| Native load | Requested cut `done`; autosave suppressed | Requested cut `done`; autosave suppressed |
| PC after load | `sale probe unavailable: no active city.`; FoodDiagnostics active=false | Same |
| Probe's own final verdict in original live call | `refusal=None`, `receipt=Paid`, quoted/paid=500, wallet 47698→47198, orders 0→1 | Same |
| Hook cleanup | `armed=false`, `hooked=false` | Same |

Both parsed cuts retain schema 1, 957 residents and the same logical minute as the pre-probe. All three population payloads have SHA-256 **`21faec77d9e358b8aec7ea333fce609f6de96c11ddf9f51d8dd498ff379dfe19`**. This establishes unchanged saved resident wallets, rather than inferring a debit from the eventual live Paid verdict. The save keeps the old payload and failed markers; it publishes no updated population payload inside the unit.

Verbatim save-epoch warning: **“Persistent City save epoch refused: A Persistent City unit was executing when the native save started.”** Live accounting then stops. On both cut loads capability reports `start refused (clock.city)`, with **“Accounting input epoch was lost.”** in the refusal; probe status says **“sale probe unavailable: no active city.”** These are the observed texts, not an inferred code root cause. Reloaded the synchronized pre-probe after each cut; the final capture-only arm proves active accounting resumed there.

**S2 check 5: FAIL.** Enter has no native sale at the cut. Exit preserves a native $5 order with a Pending receipt and no saved resident debit. PC refuses that cut rather than reconciling it. No recovery verdict or “no half sale” pass is claimed. Completed-Paid continuity from 11j was not substituted for this check.

## F7-3 `receipt_pending`

After the second pre-probe reload:

```sh
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:10:shop 10000' '{"at":"enter"}'
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent call console '{"line":"pc probe status"}'
python3 scripts/mac-agent call save '{"name":"FT Mac 12a F7 completed food"}'
python3 scripts/mac-agent op pcsave '{"action":"take","name":"FT Mac 12a F7 completed food","timeout_s":180}'
```

All exit 0. Sequence 3 at `2026-11-12T01:15:04.7484980Z`, enter, reaches Sell and captures exactly the Pending entry printed above (`matching=1`, `pending=1`, `truncated=false`). `save_name=null`, no capture save; hook disarmed/unhooked after the call. **PASS for the requested real `receipt_pending` capture.** No Pending receipt was fabricated, and the literal `receipt_pending` label is the check name, not a claimed console line.

Probe's own final verdict: Food / FullService, `None` / `Paid`, 500 cents, wallet 47698→47198, orders 0→1, recovered=0. FoodDiagnostics stays `active=true`. The normal completed save contains `paid|500|Food|...`, schema 1 / 957 residents / same minute, coherent success epoch sequence 3 and no accounting-stop marker. The saved wallet differs by the probe debit. This is capture evidence and a normal completion, not the full F7 interrupted-state/replay/control matrix.

## S2 five checks

| Check | This run | Result |
|---|---|---|
| 1 Food | Three probes reach native Sell; capture-only arm completes with active PC, one order, Paid 500 and wallet debit 500. | **PASS**, quick setup/side-effect confirmation. |
| 2 Goods | No goods city needed because food is ready and both hooks work. 11j passed at identical product bytes. | **NOT RUN**, unchanged pass deliberately not re-proved. |
| 3 Service | No recipe/probe invoked. #76 has no service-fixture fix; in-flight work was not awaited or installed. | **NOT RUN**, needs service native-finish fixture. |
| 4 Ceiling | No refusal probe needed; it would not reach Sell. 11j passed at identical product bytes. | **NOT RUN**, unchanged pass deliberately not re-proved. |
| 5 Mid-probe reload | Both real cut saves extracted and loaded. Exit native order remains without saved debit; PC refused. | **FAIL**, no-half-sale assertion unmet. |

## Findings

1. Installed #76 checkpoint arm/read/save/disarm and `checkpoint-call` work for enter, exit and capture-only on the full Mac modpack. All three reached; all disarmed; no rollback needed.
2. S2 exit cut preserves the half sale described above. The save-epoch guard refuses the city; recovery was not observed. This is a product assertion failure, not a tooling failure.
3. F7-3 now has a real Pending capture while PC keeps running. The larger reader/fault/relocation/menu and interrupted-state comparison matrices remain unproved.
4. #230 fields and serving readiness were observed as setup evidence. The 11j natural-sale, food/goods/ceiling, reload-epoch and GR-1 passes were not broadly rerun at identical product bytes.

## NOT RUN grouped

These grouped exclusions are not counted again in the 11-row summary:

- Service sale: needs the hairdresser recipe to complete native finish. Known 11j failure `ops_fixture_incomplete` / `ops_shop_requirements_unmet`, finish step 23/25. No workaround, retry or wait for the other session.
- Unchanged 11j passes beyond the food probe setup: F8h natural first sale, goods/ceiling, reload-epoch 1–2, GR-1 1/4/6, native hand-back, speed control and performance. No full next-day-noon window.
- Prior PARTIAL rows F6b-2 (representative cohort/MealsPerHour median), F7-1 (larger reader/fault/relocation/menu matrix), X3-1 (literal population-read Loaded/schema assertion), F2 (controlled journey/body/contact/replay), F8f (broader shop/vendor mechanisms). No promotion or repeat of the old subset. Pending/failed epoch side effects are recorded here for F7-1 only.
- `present`, `foreground`, `priority`: reserved step kinds, unavailable. stress10k, city-50k, RIG-FAULT, RIG-PCFIX, official manifest sealing, F4f/F7e: required capability/fixture or implementation absent. Broader F7 named-state/replay/control campaign not exercised by a receipt capture.
- Pointer/hover/focus/click: Peter's playtest. Windows/PC-overlay rows: PC offline, old overlay, outside Mac-only slot. No PC contacted.
- E5b2–5 and F9: single-croissant fixture lacks original-three/mixed/eight-nine offers and the shop/vendor/config/body/research/seven-day matrices; supported shop_edit cannot add holders/prices.
- Other GR-1 rows need close/sell/type replacement, established modded type or old-city fixtures; only fresh current-head cities authorized. No additional newly unblocked cheap row was identified. Item 8 had no extended gameplay window.

## Tooling gaps

| Capability / flow | Exact command and exit | Observed output / blocked checks |
|---|---|---|
| Op catalogue at menu | `python3 scripts/mac-agent op ops`, **exit 1** | stdout empty; stderr JSON `error:"ops_city_required"`, `reply.ok:false`, `reply.error:"ops_city_required"`. First menu read only; later same read in the FT city exits 0 and lists checkpoint support. No check remained blocked. |
| #76 mid-probe ops | All three `checkpoint-call` commands above, reads, extractions and loads, **exit 0** | No `ops_probe_checkpoint_not_reached`, `ops_probe_checkpoint_call_failed`, save-idle timeout or disarm error. Actual Pending captured; no tooling gap claimed for the failed S2 assertion. |
| Stage runner `reload.py` | Supplied 12a template copied, checkpoint and business id adapted; **exit 0** | `from pathlib import Path` present from the start; final log copy completes. Every stage script using Path has its import (`stage-script-path-check.json`). No 11f NameError or sed insertion. |
| Goods status, known #75 gap | **NOT RETESTED this run**; 11j exact command `python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-goods"}'`, exit 1 | 11j JSON `ops_fixture_unknown`. No fresh goods recipe/status was needed. This report makes no new claim that status still fails in a freshly built goods fixture; the known fix is not installed in verified #76. Goods readiness fallback was unnecessary. |
| Service recipe, known #75 gap | **NOT RETESTED this run**; 11j exact command `python3 scripts/mac-agent shop-recipe flows/mac/s2-service-shop.json --timeout 60`, exit 1 | 11j outer `ops_fixture_incomplete`, inner `ops_shop_requirements_unmet`, next_step=24, steps=25, failed finish step=23. Same verified #76 carries no fixture fix; service sale remains NOT RUN by instruction. No new service-failure observation is asserted. |

The sole command failure in this run's `failures-later.jsonl` is the menu `op ops` read. All warnings associated with PC refusal are preserved in the checkpoint/cut-load evidence. No command failed with a hidden retry.

## Fixtures

All created at this build in the isolated profile, character folder `SaveGames/Big Ambitions/ExcKSV9KlkiIxHqCIzHc+Q==`. No normal saves read or touched. Disposable saves remain, including refused cuts, under the user instruction to retain them. Hashes verified after restore:

| Checkpoint | Bytes | SHA-256 |
|---|---|---|
| `FT Mac 12a F7 completed food` | 4,385,710 | `66cd85b89b1cdfbe4d46be1d39e0fbea0c03c09605ecf43a617dec459d150573` |
| `FT Mac 12a F8h Food v1` | 1,073,750 | `a7b84e3afe3ef4902cd51a5945d97065a9acf4a76ba15f526cdb4518715ea8fc` |
| `FT Mac 12a S2 cut enter` | 4,349,347 | `74e67d575db6aba56372010ad6a162d066fc1f938072ac815c09d9f49b31c475` |
| `FT Mac 12a S2 cut exit` | 4,349,456 | `807e35cd3964981630a4a7bf9f9649ef6a523fd174947d1bfbedf73dce1669ca` |
| `FT Mac 12a S2 pre-probe` | 4,349,304 | `6ae4cb7e4ea0727a2970d8914143719303b8c6590efb410b1d959859fe444ceb` |
| `FT-PC-Mac-20261112a-0eb780b` | 513,756 | `4ac8b58e9660283b5bd1836ad3b69a8824d56f005351aa5d8355735db70820b9` |

## Evidence archive

Raw evidence stays at `<stage>`; `commands.jsonl` includes exact argv/exit/UTC without credentials. `evidence-index.json` maps the retained replies, logs, scripts, extractions and restore records to size/SHA-256. `analysis-summary.json` includes capture/probe verdicts, extracted schema/epoch/payload hashes, wallet comparisons and native ledgers. No raw saves, payload JSON, binaries or large logs enter Git.

| Evidence | SHA-256 |
|---|---|
| `cut-enter.json` | `038add67b465a7e3f938e151c06dcb93149a3ae79a3ea2401f35157eb59db032` |
| `cut-exit.json` | `e09a7f06eea609868e86ca063d7db51ae8a836d2e54fca055b0389d5429c5715` |
| `f7-capture.json` | `47aee0f1c6db1cf0fc82c723d67ea0fc61b87e3d002aebad6e3429444d79e631` |
| `analysis-summary.json` | `266e5654062b1cc18e87bbcace02cd67081916968e3c25dfbb3dc17f9fb177af` |
| `evidence-index.json` | `f28ba697d8b4a091e94745647c20ddaaf304bb3728789f24a1428069ee860ea7` |

Report copied to the local agent-log directory as `persistent-city-rig-2026-11-12a-mac.md`. Historical check documents remain in history; no deleted tree recreated.

## Per-check results

Counts apply only to these 11 rows. F7-3 below is specifically the requested `receipt_pending` capture arm.

| Check | Result | Evidence / reason |
|---|---|---|
| mac-autosave | **PASS** | 10 standalone #70 suppression readbacks, recipe before/after suppression; checked after every load; original settings restored. |
| food shop selection | **PASS** | 10 Fourth Avenue, Hell's Kitchen, C2 / 225 m²; documented money and unpinned size-C selection. |
| food serving readiness | **PASS** | Independent ready=true, no reasons, available cabinet register, 7 staffed open days before probes. |
| #76 checkpoint operations | **PASS** | Enter/exit real captures and idle cut saves; capture-only third arm; all disarmed/unhooked. |
| S2 food sale | **PASS** | Capture-only final probe Paid 500, wallet -500, orders +1, PC remains active. |
| S2 goods sale | **NOT RUN** | Identical-byte 11j pass; no fallback needed. |
| S2 service sale | **NOT RUN** | Needs service fixture native finish; known ops_shop_requirements_unmet, no service probe. |
| S2 ceiling refusal | **NOT RUN** | Identical-byte 11j pass; refusal cannot exercise Sell hook. |
| S2 mid-probe reload | **FAIL** | Exit cut retains one $5 native order without saved debit; Pending/failed epoch, PC refused. Enter cut has no native effect. |
| F7-3 receipt_pending capture | **PASS** | Capture-only enter sees 1 real Pending, no save; Paid afterward with active accounting. |
| restore/hash/lease | **PASS** | Normal stop; 260/260 original and 6/6 fixture hashes; autosave restored; free lease/pending 0; owned processes gone. |
