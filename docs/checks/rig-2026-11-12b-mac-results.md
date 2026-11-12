# Rig results — 12 November 2026 (b), Mac (#233 mid-probe refuse-and-recover, #77 goods/service fixtures, tip 060a7947)

Role `rig_test`, where `mac`, isolated `agentmac`. Candidate `060a7947ef9680486f17f7cf535d64cd94ebe2da` was fetched and verified before the build and after cleanup; the tip did not move. Followed [12a](rig-2026-11-12a-mac-results.md) and the verified official Mac procedures. Testing only; no production code changes, rig reinstall, rollback, Windows/agent1 contact, normal-profile access, system-setting changes, release, version/changelog, merge or issue creation. Other harness sessions and their files/branches were left alone. In-game proof gates no merge.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-12b-mac`; `<rig>` = the official agent-rig checkout. Raw command exits/replies, saves parsed by the rig, logs, scripts and backups remain in the stage. Only this report and an appended checklist link enter Git.

**Summary: 15 passed / 0 partial / 0 failed / 0 not run. Result: DONE.** Counts cover the 15 named rows below; grouped exclusions are not counted again. #233 enter/exit refusal and clean pre-probe recovery pass. S2 check 5 passes under the restated refuse-and-recover contract. #77 service recipe/readiness and goods post-save status work in game; both sales pass. The zero-ceiling refusal is re-proved at the changed product bytes.

## Build and rig

| Item | Value |
|---|---|
| Candidate / isolation | `060a7947ef9680486f17f7cf535d64cd94ebe2da` (#233); detached build worktree `rig-2026-11-12b-mac`, separate evidence worktree `rig-2026-11-12b-mac-evidence`. New branch `codex/mac-rig-2026-11-12b`; no remote name collision. |
| Official rig checkout | `5dae2f6f0d51ae56b7dfde77ecabb1ecc6cbc8d9` (#77), clean and read only. |
| Live AgentRigOps | SHA-256 `6478e76cb4c04b44f8d8ef315d4ef75fb5d912034f957696fdc4ad7a6b22691c`, verified before start and after cleanup. In-city `op ops` lists `probe_checkpoint`; initial read disarmed/unhooked, sequence 0. |
| Rollback retained, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T070545Z`. No install-mac/update/rollback command ran. |
| Build / compile reference | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; passes in the detached worktree, same isolated Mac reference as 12a. Fingerprint `unknown`, 27 differing assemblies. |
| Product DLL | SHA-256 `f89c9e04d14552484fb0a29481a66c320623f56eef3e21c16d9027f9208eacbd`. Before any check, raw UTF-8/UTF-16LE byte searches confirm `NativeSaleProbe`, `SaleProbeCommand`, `pc probe status`, `travelClockRate`, `walkingMetresPerGameMinute` and both #233 refusal phrases. Parsed snapshots retain schema 1. |
| Environment / validation | Every Python/.NET/build command used `PATH="/opt/homebrew/bin:$PATH"`, `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No source/test changes, full suite or regression suite. Stage evidence assertions pass. No performance window was taken. |
| Profile / modpack | Private Apple Silicon player, runtime 7, endpoint 28330, headless `--rig-test`; full existing isolated modpack. Candidate DLL/English locale, enabled/physical config: 48 pedestrians, 24 vehicles, radius 120 m; remaining compiled defaults. |
| Lease / resources | Initially stopped/free; owner `pc-mac-rig-2026-11-12b`, purpose `Persistent City 12 Nov Mac rig b`, same owner throughout. Immediately before initial start: 52.3 GiB free, 71% reported free memory; rechecked before service/goods starts. |
| Backup / autosave | 260 files copied and SHA-256 manifested before installing: isolated ModsLocal, top-level profile files, plist and install receipt. Original plist has no autosave keys; native readback 5 minutes / 3 saves. #70 process suppression used, no preference edit. 20 retained standalone suppression readbacks plus recipe readbacks; checked after every completed load. |
| Cleanup | Normal stops; 260/260 original hashes and 12/12 disposable fixture hashes match. Preferences/autosave, config, ModsLocal and receipt restored; lease free/pending 0; owned game/helper processes gone. |

## Runs

| Segment | Fixture / result |
|---|---|
| Food city / rental | `FT-PC-Mac-20261112b-060a794`; selected 25 Fourth Avenue, Garment District, C2 / 225 m². Documented $100M recipe, independent ready read before advancing. |
| Setup | Supplied reload template, actual shop id, Path import present; log-copy tail completes. Short day-one 08:00→09:00 speed-20 setup, paused; coherent pre-probe checkpoint at minute 1980.0099983215332. |
| Exit cut, first | Sequence 1, Sell finalizer, real Pending capture and idle cut save. Loaded cut: 1 order / $5, Pending, unchanged population payload; named startup refusal. |
| Enter cut | Reload pre-probe first. Sequence 2, Sell prefix. Loaded cut: 0 orders / $0, Pending, unchanged payload; same named refusal. |
| Recovery | Reload pre-probe after inspecting each refused cut. Settled baseline: active, no receipts/order, original wallet. Plain probe Paid 500 with one order/debit. Coherent completed checkpoint loads active with Paid retained and recovered=0. |
| Capture / ceiling | F7-3 capture-only enter sequence 3; Pending=1, no save, active afterward. Zero-ceiling probe refuses; final confirmation after goods uses the coherent completed food checkpoint. |
| Service city | Fresh `FT-PC-Mac-20261112b-S2-service`; official recipe, status and readiness succeed; service probe Paid 2000. |
| Goods city | Fresh `FT-PC-Mac-20261112b-S2-goods`; official recipe, required status id and independent readiness succeed; goods probe Paid 500. |
| Cleanup | Normal stop, restored file hashes, retained fixture hashes, free lease and owned-process checks. |

## Food-shop rental and readiness

Copied `flows/mac/f8h-food-shop.json` to `<stage>/recipe.json`; kept `spec_from: flows/fixtures/mac-food-shop.json`. Only id/name/checkpoint and `"selection":{"building_size":"ba:buildingsize_c"}` differ. No address pin. Recipe sets and reads back $100,000,000 before native rental; no other money setup or rival-finance write.

```sh
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#25"}'
```

Both exit 0. **25 Fourth Avenue, Garment District, C2, 225 m²**, business `ba:street_fourthavenue#25`; neighborhood `ba:neighborhood_garmentdistrict`, size `ba:buildingsize_c`, version 2. `rented=true`, real-estate `owned=false`. Independent readiness: **ready=true, reasons=[], unmet_requirements=[]**; croissant units=100, reserve=0, price=5, offer ready=true. Seven open days have one staffed counter. Register is attached to the Modern Cabinet and staffed via `station_item`; `serving.available=true`, `point_of_sale=true`, 300 paper bags. No shop_log hook or fixture workaround.

The incidental early `shop_state '{"id":"ba:street_fourthavenue#24"}'` read an example address rather than the returned fixture id. It exited 0 and was read only; it is not readiness evidence. The correct returned id above was read before any advance or probe. No address hunt followed.

`pc probe businesses` selects this run's `address:ba%3Astreet_fourthavenue:25:shop`, FullService/Food, foodOwned=true, open=true. Resident c0, ceiling 10000 cents, basket 500 cents. #230 read `op pcstate '{"action":"read","property":"FoodDiagnostics"}'` after settled recovery reports active=true, travelClockRate=0.3333333333333333, walkingMetresPerGameMinute=6.0. The immediate speed-20 setup read instead reports rate=6.666666666666666 / walking=0.30000000000000004; both raw replies are retained.

## #233: cut refusal, native ledger, Pending and payload

Exact commands from `<rig>` with the environment/owner above (all exit 0):

```sh
python3 scripts/mac-agent call console '{"line":"pc probe businesses"}'
python3 scripts/mac-agent call save '{"name":"FT Mac 12b S2 pre-probe"}'
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:25:shop 10000' '{"at":"exit","save":{"name":"FT Mac 12b S2 cut exit"}}'
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent op pcsave '{"action":"take","name":"FT Mac 12b S2 cut exit","timeout_s":180}'
python3 scripts/mac-agent call load '{"name":"FT Mac 12b S2 cut exit"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call console '{"line":"pc capability status"}'
python3 scripts/mac-agent call console '{"line":"pc probe status"}'
python3 scripts/mac-agent op pcstate '{"action":"read","property":"FoodDiagnostics"}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#25"}'
python3 scripts/mac-agent op ledger '{"day":1,"shops":["ba:street_fourthavenue#25"]}'
python3 scripts/mac-agent call load '{"name":"FT Mac 12b S2 pre-probe"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:25:shop 10000' '{"at":"enter","save":{"name":"FT Mac 12b S2 cut enter"}}'
python3 scripts/mac-agent op probe_checkpoint '{"action":"read"}'
python3 scripts/mac-agent op pcsave '{"action":"take","name":"FT Mac 12b S2 cut enter","timeout_s":180}'
python3 scripts/mac-agent call load '{"name":"FT Mac 12b S2 cut enter"}'
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call load '{"name":"FT Mac 12b S2 pre-probe"}'
python3 scripts/mac-agent autosave
```

Each load is polled for requested load.name/status done, scene city and matching save name, then autosave is checked and time paused. The same capability/status/epoch/food/shop/ledger/person reads were retained for both cuts. No AlreadyRecorded or ops_probe_checkpoint_not_reached occurred. Both captures match one receipt, Pending=1, truncated=false, later_calls=0. Exit target_threw=null. Both Default saves returned, then the client confirmed in_progress=false and compress_thread_alive=false. Hooks disarmed/unhooked after every call.

```text
persistentcity:sale-probe-v1:c0:address%3Aba%253Astreet_fourthavenue%3A25%3Ashop:1980
pending|Food|c0|address%3Aba%253Astreet_fourthavenue%3A25%3Ashop|500:ba%3Aitemname_croissant
```

| Observation | Exit cut | Enter cut |
|---|---|---|
| Capture UTC / hook | 2026-11-12T07:30:09.5973310Z / Sell finalizer | 2026-11-12T07:31:59.7401650Z / Sell prefix |
| Loaded native ledger | 1 completed order / $5, croissants 99 / bags 299 | 0 orders / $0, croissants 100 / bags 300 |
| Saved receipt | Pending | Pending |
| Epoch / stop | failed:3 / save-epoch-failed | failed:3 / save-epoch-failed |
| Saved population/wallet | Equals pre-probe | Equals pre-probe |
| PC startup | start refused (clock.city), FoodDiagnostics active=false | Same |

Pre-probe and both cuts: schema 1, 957 residents, minute 1980.0099983215332; complete population-payload SHA-256 **`0f0fa8366c52fe96c8139e360a129e335213771129f8b5e4c10fcb00a86bc5d1`** in all three. Saved c0 wallet tuples are equal. This compares the persisted payload, not the eventual live call's Paid verdict.

Both loaded cuts' capability and player log carry this **verbatim cause**:

> This save did not publish a coherent Persistent City snapshot (for example it was written while a Persistent City sale or event was running), so its native books may hold a sale its residents never paid for. Persistent City never settles or replays such a sale; the earlier coherent save holds it undone.

Neither cut's loaded capability refusal says “Accounting input epoch was lost.” `pc probe status` says **“sale probe unavailable: no active city.”** The save-time warning is **“Persistent City save epoch refused: A Persistent City unit was executing when the native save started.”** Native loading succeeds; Persistent City does not start or settle/replay the Pending sale. The original live probes eventually print Paid 500, but those eventual verdicts are not substituted for saved-cut state. **Reloaded the pre-probe checkpoint after inspecting each refused cut.**

## #233: clean pre-probe recovery

After loading `FT Mac 12b S2 pre-probe`, checked autosave and paused. Immediate post-load reads sometimes briefly returned “sale probe unavailable: no active city.” All such replies exited 0 and are retained. A separate settled recovery arm explicitly waited on FoodDiagnostics active=true before the baseline commands:

```sh
python3 scripts/mac-agent op pcstate '{"action":"read","property":"FoodDiagnostics"}'
python3 scripts/mac-agent call console '{"line":"pc capability status"}'
python3 scripts/mac-agent call console '{"line":"pc probe status"}'
python3 scripts/mac-agent op ledger '{"day":1,"shops":["ba:street_fourthavenue#25"]}'
python3 scripts/mac-agent call console '{"line":"pc population person c0"}'
python3 scripts/mac-agent call console '{"line":"pc probe c0 address:ba%3Astreet_fourthavenue:25:shop 10000"}'
python3 scripts/mac-agent call console '{"line":"pc probe status"}'
```

All exit 0. Before: city running, FoodDiagnostics active=true; **`{"recovered":0,"receipts":[]}`**, ledger 0 orders / $0, c0 balance_units=27878632718336 (wallet 103856 cents). After: **Food / FullServiceBusinessSimulator; refusal=None, receipt=Paid, quoted/paid=500/500 cents; wallet 103856→103356, orders 0→1**. Native orderCompleted=true, ordersAdded=1. Active remains true, balance_units=27744414990336; exactly one 500-cent debit. Reloads are independent arms, not additional debits in one city state.

Normal save `FT Mac 12b S2 completed food`, taken outside the probe, contains `paid|500|Food|...`, coherent epoch sequence 3, no accounting-stop marker, payload SHA-256 `eeb3c5a80e1a5cfbe9aa3c84096bca620d74f8d5ecad71e23a26e8446e4bc3a0`. Its settled reload is active; status retains Paid 500 with recovered=0, ledger 1 order / $5 and c0 balance_units=27744414990336. No second debit. The pre-probe normal save has no probe receipt/order and original wallet; both outside-probe states are coherent.

**#233 checks 1–2 PASS. S2 check 5 PASS under the restated contract:** either cut refuses PC startup; neither half of that cut sale is applied in a started PC city. The last coherent save loads with the sale fully undone; normal outside-probe saves hold it not started or Paid. The exit cut still has its native half sale, exactly as the refusal warns. No reconciliation of that refused cut was attempted.

## S2 check 3: service sale and #77 fixture

Fresh city `FT-PC-Mac-20261112b-S2-service`, full modpack, autosave suppressed. Official recipe without address/spec changes:

```sh
python3 scripts/mac-agent shop-recipe flows/mac/s2-service-shop.json --timeout 60
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-service"}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_thirdstreet#25"}'
```

All exit 0; checkpoint `FT Mac S2 Service v1` saved. **25 Third Street, Garment District, A1 / 75 m²**, business `ba:street_thirdstreet#25`; neighborhood `ba:neighborhood_garmentdistrict`, size `ba:buildingsize_a`, version 1. Exact status excerpt (selected unchanged fields):

```json
{"after":{"id":"mac-s2-service","business_id":"ba:street_thirdstreet#25","next_step":28,"steps":28,"complete":true,"error":null,"shop":{"readiness":{"ready":true,"reasons":[],"unmet_requirements":[]}}}}
```

Independent readiness ready=true, reasons=[], **unmet_requirements=[]**. Three staff: one customer-service employee and **two hairstylists** with 09:00–18:00 shifts at the chair/head wash. Head wash `3WkRBqTtTkKLJR5J7PYsMg==` is in the shampooing-fee holders; price **15**. Chair holders cover $20 cutting, $25 styling and $30 chemical fees. Hair-care shelf has 50 units. Native finish succeeds; no 11j ops_shop_requirements_unmet.

After ready read, advance to 09:00, pause, then:

```sh
python3 scripts/mac-agent call console '{"line":"pc probe c0 address:ba%3Astreet_thirdstreet:25:shop 10000"}'
```

Exit 0: **Service / HairdresserBusinessSimulator; refusal=None, receipt=Paid, quoted/paid=2000/2000 cents; wallet 327954→325954, orders 0→1**. Basket haircutting fee $20; native entries paid=true for fee and $0 hair-care consumption. orderCompleted=true, ordersAdded=1, claimedNativeEntry=true, claimedEntryRetired=true. `sameHourRegenerationMayAddOneEntry=true` retained. **S2 service PASS**; the probe exercises cutting, while shampooing is readiness/fixture evidence. No claim of a paid shampooing service.

## S2 check 2: goods sale and post-save fixture status

Fresh city `FT-PC-Mac-20261112b-S2-goods`; official recipe unchanged, autosave suppressed:

```sh
python3 scripts/mac-agent shop-recipe flows/mac/s2-goods-shop.json --timeout 60
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-goods"}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#23"}'
```

All exit 0; checkpoint `FT Mac S2 Goods v1` saved. **23 Fourth Avenue, Garment District, C1 / 225 m²**, business `ba:street_fourthavenue#23`; neighborhood `ba:neighborhood_garmentdistrict`, size `ba:buildingsize_c`, version 1. Required status **succeeds after the recipe's checkpoint save-as**, unlike 11j. Exact output excerpt (selected unchanged fields; full output retained with hash below):

```json
{"after":{"id":"mac-s2-goods","business_id":"ba:street_fourthavenue#23","next_step":21,"steps":21,"complete":true,"error":null,"shop":{"readiness":{"ready":true,"reasons":[],"unmet_requirements":[]}}}}
```

Independent shop_state readiness ready=true, reasons=[], unmet_requirements=[]; 200 cheap gifts at $5, 400 bags, available cabinet register and staffed counter on all seven open days. No real goods reload occurred; all readiness reads use shop_state, and status persistence across a real reload is not claimed.

```sh
python3 scripts/mac-agent call console '{"line":"pc probe c0 address:ba%3Astreet_fourthavenue:23:shop 10000"}'
```

Exit 0: **Goods / SelfServiceBusinessSimulator; refusal=None, receipt=Paid, quoted/paid=500/500 cents; wallet 192711→192211, orders 0→1**. Native orderCompleted=true, ordersAdded=1, claimedNativeEntry=true, claimedEntryRetired=true; sameHourRegenerationMayAddOneEntry=true retained. **S2 goods PASS; #77 goods status PASS.**

## S2 check 4: ceiling refusal

After goods, load `FT Mac 12b S2 completed food`, check autosave, pause and wait for FoodDiagnostics active=true:

```sh
python3 scripts/mac-agent call console '{"line":"pc probe c1 address:ba%3Astreet_fourthavenue:25:shop 0"}'
```

Exit 0, **refusal=Ceiling, receipt=Missing, quoted=500, paid=0**, wallet 49777→49777, orders 1→1, native={}. Verbatim reason: **“Basket 500 is above the ceiling 0”**. Fresh c1 receipt identity; no effect. **PASS.** This never reaches Sell and is not a mid-probe run. An identical earlier food-arm ceiling read is retained; the final command is the post-goods confirmation.

## S2 check 1 food confirmation; F7-3 capture

The no-checkpoint recovery probe above is the requested short **S2 food PASS**. No separate next-day-noon natural-sale window.

Capture-only command after another coherent pre-probe reload:

```sh
python3 scripts/mac-agent checkpoint-call 'pc probe c0 address:ba%3Astreet_fourthavenue:25:shop 10000' '{"at":"enter"}'
```

Exit 0; sequence 3 at 2026-11-12T07:35:47.6469080Z, real Pending=1, matching=1, truncated=false, no save; hook disarmed/unhooked. Probe completes Paid 500, wallet -500 and orders +1; FoodDiagnostics stays active=true. **F7-3 receipt_pending capture PASS**, only this capture arm, not its larger interrupted-state matrix. No fabricated Pending receipt or extended gameplay window.

## Findings

1. #233's diagnostic cause is present in the built DLL and both real cut loads. Pre-probe recovery and outside-probe Paid continuity satisfy the restated S2 check 5 contract.
2. #77 service native finish/readiness and post-save goods status now succeed in game. Service, Goods and Food probe sales each produce one native order and one matching resident debit.
3. Immediate post-load PC reads can precede active startup; settled recovery explicitly waits for active=true and proves the empty baseline. No code root cause is assigned.
4. No performance result is claimed. Other harness activity may share the Mac; any incidental frame logs are separate from mod-work timing.

## NOT RUN grouped

These exclusions are outside the 15-row targeted counts:

- RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY: reserved present/foreground/priority flow step kinds unavailable. Commands themselves ran in the foreground and were awaited.
- stress10k, city-50k, RIG-FAULT, RIG-PCFIX, manifest sealing, F4f/F7e: required capabilities, fixtures or implementation absent; not awaited.
- Pointer/hover/focus/click: Peter's playtest. Windows/PC-overlay-specific rows: Mac-only slot; GR-3 owns the Windows run, no contact made.
- E5b2–5 / F9: current single-croissant food fixture and supported ops do not cover their original-three/mixed/eight-nine offers and broader vendor/config/body/research/seven-day matrices. No workaround.
- 11j PARTIALs F6b-2, F7-1 larger matrix, X3-1 literal assertion, F2/F8f matrices: not rerun or promoted. This run's specific Pending/refusal/recovery evidence is confined to the rows above.
- 11j F8h natural first sale and broader unchanged passes: no next-day-noon repeat. Paid shampooing sale, full service-fee matrix, performance, other GR-1 lifecycle and old-city fixtures: outside this targeted run.

## Tooling gaps

**No mac-agent command/op failures in this run.** `commands.jsonl` records argv, UTC and exit for every command without credentials; no failures-later entries. Required capabilities are individually accounted for:

| Capability / flow | Exact command / exit | Observation / blocked checks |
|---|---|---|
| probe_checkpoint / checkpoint-call | Commands in #233 section, reads/extractions/loads; exit 0 | Both real cuts reached; idle saves, disarmed hooks. No ops_probe_checkpoint_not_reached, call_failed or save/disarm timeout. No blocked check. |
| goods status, #77 | `python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-goods"}'`, exit 0 | Complete post-save job, no error, ready=true. 11j ops_fixture_unknown does not recur. |
| service recipe/status, #77 | `python3 scripts/mac-agent shop-recipe flows/mac/s2-service-shop.json --timeout 60`; status command above; exit 0 | Native finish complete, head wash holder, two hairstylists, $15 shampooing and no unmet requirements. 11j ops_fixture_incomplete / ops_shop_requirements_unmet do not recur. |
| Stage reload.py | Supplied 12b template copied, checkpoint/shop adapted; exit 0 | **from pathlib import Path present from the start**; log-copy tail completes. AST validation confirms every stage script using Path imports it. No NameError or sed insertion. |
| Transitional startup read | `python3 scripts/mac-agent call console '{"line":"pc probe status"}'`, exit 0 | Exact early warning: “sale probe unavailable: no active city.” Settled recovery waits for active=true, then status returns empty receipts before the successful plain probe. No op failure or blocked check. |

Local preparation corrections: the first build invocation ran in the main checkout; tracked files stayed clean and the detached build was then completed and used. The first local DLL string checker searched one UTF-16 alignment and raised AssertionError on two present strings; raw encoded-byte searches confirmed all required strings before any check. Neither is an in-game assertion failure. The incidental wrong-address read is disclosed above; it changed no business.

## Fixtures

All fresh at this candidate in the isolated profile; saves remain disposable and retained, including refused cuts. Three character folders correspond to the named food/service/goods cities. No normal saves read or changed. SHA-256 verified after restore:

| Checkpoint | SHA-256 |
|---|---|
| `FT Mac 12b F8h Food v1` | `1fafd407410932e3ed9d1da41dad8a5fa722b128185c5c80401170eb648d8bc9` |
| `FT Mac 12b S2 completed food` | `cfe924120a9fb08968873734c5e0c1dbcf6658e304e2fd87f899b793dda2f42c` |
| `FT Mac 12b S2 cut enter` | `6117d76fe8f971dc897b8af32339744ad7ab30a438fc7562aa14095341653b5d` |
| `FT Mac 12b S2 cut exit` | `3c19b60d8eb333a7f82c5b429a758d683fbaf6c10a388a1948690e5c9c655a93` |
| `FT Mac 12b S2 pre-probe` | `0113375c95055e9918722ca142ffe91a5f5995b9d4ac55740a899ddc329f3bd3` |
| `FT-PC-Mac-20261112b-060a794` | `92b5cecc502031348306e2e72ac51c8f4648d3df14a2fd496a9833ba53d9129e` |
| `FT Mac 12b S2 service result` | `834a04e0016cf218ecb5078e251a0b15517dfa512262b9d3d3e0bccd847b71f3` |
| `FT Mac S2 Service v1` | `8f9a0d5f96757416b6617412e79bbbee5b5c4a1de9e3d7d6bffa6510a55a8743` |
| `FT-PC-Mac-20261112b-S2-service` | `f4f9a6ac78a905f491e786001bfec936a5ae5219525d6d757aba69ae043fb8dc` |
| `FT Mac 12b S2 goods result` | `8a2a252fa06105053ee9f6298fd2ad0f31bef780028727979548435b38e3933a` |
| `FT Mac S2 Goods v1` | `24f927b133a76900721929cd1ae531d0cad85c4557ab4a9d1e86f990229f22e2` |
| `FT-PC-Mac-20261112b-S2-goods` | `ad4a027cbba30ba5b24c6109f4c29545e60509f50a357d9edda44471ed70c4fa` |

## Evidence

Raw evidence lives in `<stage>`; `evidence-index.json` maps files to sizes/SHA-256. `analysis-summary.json` records captured receipts, saved payload hashes, probe verdicts and ledgers; `food-verification.json` records all cut/recovery/ceiling/capture assertions true. No raw JSON dumps, logs, binaries or saves enter Git.

A copy of this report is kept in the local agent log. Historical check docs stay in the history tag; no deleted tree recreated. Only an appended link/result line updates the kept rig checklist.

| Local evidence file | SHA-256 |
|---|---|
| `dll-check.json` | `4081aac3b7f11150fadafefd7438042543c00df40e535934f0a98c943f81853e` |
| `cut-exit.json` | `9bfdd03fe7da8f1467e44e8e36b3e1af128f67672c7b0ddc430010e51fff552f` |
| `cut-enter.json` | `9a14d62cb036216475bd4fb607d1d6d02dd2183c09179e91f9a7d092dde51097` |
| `settled-recovery-probe.json` | `615e8a28de2ca1fe981fe4c9e3d034b5c296b26180ccbb15f437679299a3f7a5` |
| `goods-fixture-status.json` | `434330c3377f47ef43815f86cfb2b6f502ba56a29889f3cdee856ec3eb6693a0` |
| `service-fixture-status.json` | `575905e94a9df8de9ddaa1152d35bba3b1479e0112577eab8c1c1d5313553e4a` |
| `service-headwash-check.json` | `d968479bea2fb1ee4b7e6b561b4abd47bc1f1530cf20d74d3fc77b0432f0c290` |
| `goods-probe.json` | `1c11754246945a14ee96f3d8f3aec8d2edf570aa2259e2e8b81b0134ebb3bac0` |
| `service-probe.json` | `2f14aac8b14736d49215f69aba3ec438231d7beed5dc9f46b835e575bc987767` |
| `ceiling-final.json` | `2d9e1caa6bd0cf36222eb42b7e23215695581b58bf9f3ed402c9782756777b42` |
| `analysis-summary.json` | `70624a80759d54f4d9a3ae08e7ee288a7110e4ad1e20bc2b8578c59e5f605a0c` |
| `food-verification.json` | `921c58f280578e0e50fe6a121f15bbcf818370e229deb23c51df64a23101d96d` |
| `restore.json` | `222a39f5c934e8d1caa07ed922d736fd1f240a365de5ba0e7be98d20953f74e1` |
| `evidence-index.json` | `6fda2b130ffa25ffc6222b2215cd13a1fa8659e972a8fe09d078adb8ff8bc593` |

## Per-check results

Counts apply to these 15 rows; some intentionally share evidence because #233 restates S2 check 5.

| Check | Result | Evidence |
|---|---|---|
| mac-autosave | **PASS** | Suppression after every load/window; original autosave restored. |
| food shop selection | **PASS** | Size-C rental, 25 Fourth Avenue / Garment District / C2 / 225 m². |
| food serving readiness | **PASS** | Independent ready=true before advances/probes. |
| mid-probe checkpoint operations | **PASS** | Real enter/exit cut saves and capture-only; all hooks removed. |
| #233 cut refusal | **PASS** | Both refused with new verbatim cause, no old input-epoch text; ledger/Pending/payload checked. |
| #233 pre-probe recovery | **PASS** | Settled active/empty baseline; one order, Paid 500, one debit; coherent Paid reload. |
| S2 food | **PASS** | Plain recovery probe FullService/Food Paid 500. |
| S2 goods | **PASS** | SelfService/Goods Paid 500; one order/debit. |
| S2 service | **PASS** | Hairdresser/Service Paid 2000; one order/debit. |
| S2 ceiling | **PASS** | Basket 500 above ceiling 0; Missing, no effect. |
| S2 check 5 mid-probe reload | **PASS** | Restated refuse-and-recover contract, not old loaded-cut no-half-sale assertion. |
| #77 goods status | **PASS** | mac-s2-goods complete after checkpoint save-as; independent shop_state ready. |
| #77 service fixture/readiness | **PASS** | Native finish, head wash holder, two hairstylists, $15 fee, empty unmet_requirements. |
| F7-3 receipt_pending capture | **PASS** | Real capture-only Pending=1, active after Paid completion. |
| restore/hash/lease | **PASS** | 260 original and 12 fixture hashes; normal stops, free lease, owned processes gone. |
