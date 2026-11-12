# Rig results — 11 November 2026 (j), Mac (#230 first-sale diagnostics, #75 goods/service fixtures, S2 probe checks)

Role `rig_test`, where `mac`, isolated `agentmac`. Candidate `f8013e79abee429be27d81fda586386f61e13f93` was fetched and verified before build. Official rig checkout `fae4803117431c7a3b0ed18eea88640e19c12672`, clean, read only. No reinstall, rollback, production change, release, version/changelog, merge, issue creation or Windows contact.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-11j-mac`. Raw responses, command exits, logs, scripts, extractions and backups stay there. `<rig>` = `/Users/peterhartwieg/Coding_Projects/big-ambitions/big-ambitions-agent-rig`. Previous stages and worktrees were read only; protected harness sessions were not inspected or changed.

**Summary: 16 passed / 5 partial / 2 failed / 11 not run. Result: BLOCKED.** Counts cover the 34 named rows below; grouped historical exclusions are not counted again. Natural first sale, S2 Food/Goods/ceiling, completed-receipt continuity and reload clock/identity checks passed. The required goods fixture-status read failed; the service recipe refused native finish, so its sale is NOT RUN. Mid-probe interruption remains unavailable. In-game proof gates no merge.

Run started 11 November and finished 12 November (Europe/Berlin); the requested run-j title and fixture names are retained. A final fetch still found `f8013e79`. The official checkout remained clean at `fae4803`.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `f8013e79abee429be27d81fda586386f61e13f93`; detached build worktree `rig-2026-11-11j-mac`, independent evidence worktree `rig-2026-11-11j-mac-evidence`. Main working tree unchanged. |
| Official rig checkout | `fae4803117431c7a3b0ed18eea88640e19c12672` (#75); installed AgentRigOps SHA-256 `7414e27bf76c6115ef270df9ae5334168e8f572794fefbe454229efc658c811b`. |
| Rollback retained, unused | `~/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261111T214106Z`; no reinstall/update/rollback. |
| Build / reference | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; same private Mac compile reference as 11i. Build passes. |
| Environment | Every Python/.NET/build command used `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. No full or regression suite. |
| DLL SHA-256 | `1c840d1c4b63a651c186b144ed64d3ba5d81951250b7547c9d0a8d05d3913bad`; built and installed bytes match. |
| Built diagnostics | `NativeSaleProbe`, `SaleProbeCommand`, UTF-16 `pc probe status`, `travelClockRate` and `walkingMetresPerGameMinute` confirmed in the DLL before checks. `dll-check.json`. |
| Profile / mode | Private native Apple Silicon app, isolated `com.hovgaardgames.bigambitions.agenttests.mac`, port 28330, runtime 7, headless `--rig-test`. All 29 full-modpack DLL paths retained in `modpack-live.json`. |
| Lease / resources | Initially stopped/free; owner `pc-mac-rig-2026-11-11j`, purpose `Persistent City 11 Nov Mac rig j`, same throughout. Immediately before first start 62.2 GiB disk free / 68% reported free memory. |
| Backup / configuration | 260 files copied and SHA-256 manifested before changes: isolated ModsLocal, profile top-level files, plist and receipt. Candidate DLL/English locale and enabled/physical config (48 pedestrians, 24 vehicles, radius 120 m); remaining compiled defaults. |
| Compile provenance | Mac reference set `unknown`, 27 differing assemblies; no Windows compatibility claim. |
| Cleanup | Normal stop; 260/260 original hashes, 20/20 fixture hashes, original autosave plist restored; no force stop or rollback. |
| Original autosave | No autosave keys in the isolated plist; process-only #70 guard used without editing preferences. `autosave` suppression checked before windows and after each completed reload. |

## Runs

| Segment | Fixture / result |
|---|---|
| Fresh food city / recipe | `FT-PC-Mac-20261111j-f8013e7`; size-C rental; `FT Mac 11j F8h Food v1`; independent readiness before advancement. |
| Normal-load natural window | FT speed 1, hourly reads through day-two noon; $450 / 90 orders on day one, $50 / 10 on day two. No probe receipt in this arm. |
| S2 food / ceiling | Reload stocked original, independently ready; day 1 10:00; Food Paid 500 cents and Ceiling refusal. |
| Fresh goods city | `FT-PC-Mac-20261111j-S2-goods`; official recipe succeeds; required status fails; independent readiness succeeds; Goods probe Paid at 09:00. |
| Fresh service city | `FT-PC-Mac-20261111j-S2-service`; native finish refuses; status and shop_state inspected; no service probe or recipe retry. |
| Completed-receipt / natural cold continuity | Reload in a later process after the two new cities; Paid probe, natural noon and post-midnight checkpoints compared with paused reload copies. |
| Union timing | Ready open food shop, speed 1, complete/valid union; provisional shared-Mac sample. |
| Fresh accounting city | `FT-PC-Mac-20261111j-Epoch`; S1 immediate reload, one game hour to H1, H1 reload and settled revisit. |
| Speed-20 / foreign-prefix controls | Same pristine food checkpoint; speed-20 noon arm $0; test-prefix native hand-back +$45; remove, reload, reacquire at boundary 64. |
| Cleanup | Normal stop; 260 original and 20 fixture hashes match; autosave restored, lease free/pending 0, owned game/helpers gone. |

## Shop rental and readiness diagnostic

Read 11i, the official README/Mac procedures, serving-readiness and cabinet-attachment sections, and pc-food-shop/Mac F8h fixture paragraphs. The run-specific recipe copies `flows/mac/f8h-food-shop.json`; `spec_from` stays `flows/fixtures/mac-food-shop.json`. Only identity/checkpoint and `"selection":{"building_size":"ba:buildingsize_c"}` differ. No pinned address or equipment workaround. The documented recipe sets and reads back $100,000,000 before native rental; no campaign or rival-finance write.

Exact commands, under the same owner/environment from `<rig>`:

```sh
python3 scripts/mac-agent shop-recipe <stage>/recipe.json --timeout 60
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#15"}'
```

Both exit 0. Independent readiness ran after rental and before any sales advancement. Selected **15 Fourth Avenue, Garment District, C2, 225 m²**, `ba:street_fourthavenue#15`. `rented=true`, real-estate `owned=false`; PC management is a separate authority fact. Readiness: `ready=true`, `reasons=[]`, croissant `units=100`, `reserve_units=0`, `price=5`, `ready=true`; one staffed counter on every open day. Register serving is available at a point of sale, with 300 paper bags. The prescribed Modern Cabinet `on` attachment and `station_item: ba:itemname_cashregister` are unchanged. Hours and Default employee shifts are 09:00–18:00 on all seven days.

Cabinet (`ba:itemname_moderncounter`) `IKRzjtGpeE6ajMQRZ1vsiw==` at `{'x': 897.0, 'y': 1.1920929e-07, 'z': -3.0}`; register `Bpsu2wqp40qvpZIbL3wEMg==` at `{'x': 897.0, 'y': 0.8800001, 'z': -3.0}`. The prescribed `on` attachment and raised, available point of sale are retained. `shop_state` does not expose raw parentId/stackedItems. Native shifts point to that register, type `Default`, customer_service=50; employee available=true.

## F8h first sale and #230 diagnostics

Read the merged `f8013e79` diff (`Runtime/PersistentCityMod.cs`) and PR #230. Its new diagnostics are public `FoodDiagnostics` fields, read with the installed Mac pcstate op. Exact read commands:

```sh
python3 scripts/mac-agent op pcstate '{"action":"read","property":"FoodDiagnostics"}'
python3 scripts/mac-agent call console '{"line":"pc food decisions"}'
```

Verbatim new-field output throughout the sampled native-pace window:

```json
"travelClockRate": 0.3333333333333333,
"walkingMetresPerGameMinute": 6.0
```

Normal load used the supplied `reload.py` template with the actual business id and checkpoint, keeping `from pathlib import Path`. Its final player-log copy completed (`player-template-reload.log`). The settled full-modpack report reads **city running**, **food.checkout.shop optional active**, **49 of 49 target methods patched**, **0 refusing food.checkout.shop**. Activation scans and hook owners remain in the raw logs. No `shop_log` postfix was installed.

Advance pattern, each followed by state polling until paused/run_until cleared and autosave readback:

```sh
python3 scripts/mac-agent call time '{"run_until":{"day":"+0","time":"HH:00"},"speed":1}'
# Overnight to day-two opening:
python3 scripts/mac-agent call time '{"run_until":{"day":"+1","time":"09:00"},"speed":1}'
```

**PASS:** final native day 2, Tuesday, 12:00 (`minute=0.00999832153`): `today_revenue=50`, completed orders=10, `nativePurchasesThisSession=100`. First positive boundary was day 1 10:00: $55 / 11 orders / 11 purchases. Day-one native history independently retains 90 orders, 90 croissants, 90 bags and $450. The noon save contains **100 Paid natural food receipts, zero probe receipts**, schema 1, coherent epoch, no accounting-stop marker. Opening hours and register shifts remain 09:00–18:00 on both days.

| Day | Clock | Ready | Revenue | Completed orders today | Session purchases | Croissants left |
|---|---|---|---|---|---|---|
| 1 | 09:00 | True | $0 | 0 | 0 | 100 |
| 1 | 10:00 | True | $55 | 11 | 11 | 89 |
| 1 | 11:00 | True | $80 | 16 | 16 | 84 |
| 1 | 12:00 | True | $175 | 35 | 35 | 65 |
| 1 | 13:00 | True | $275 | 55 | 55 | 45 |
| 1 | 14:00 | True | $375 | 75 | 75 | 25 |
| 1 | 15:00 | True | $420 | 84 | 84 | 16 |
| 1 | 16:00 | True | $450 | 90 | 90 | 10 |
| 1 | 17:00 | True | $450 | 90 | 90 | 10 |
| 1 | 18:00 | True | $450 | 90 | 90 | 10 |
| 2 | 09:00 | True | $0 | 0 | 90 | 10 |
| 2 | 10:00 | True | $10 | 2 | 92 | 8 |
| 2 | 11:00 | True | $25 | 5 | 95 | 5 |
| 2 | 12:00 | False | $50 | 10 | 100 | 0 |

At the final noon read only, readiness becomes `ready=false`, shop reason **`no_ready_offer`**, offer reason **`out_of_stock`**; stock=0, reserve=0 after all 100 units sold. The required window had ended. No further natural-sale wait or refill was attempted. S2 used a normal reload of the original stocked checkpoint, independently `ready=true`, units=100.

Exact observed shop-choice reasons below are deduplicated resident + decision + business + product + reason across bounded reads. Each console read retains at most 50 decisions; these are observations, not a full decision census or an assigned code root cause.

| Verbatim reason | Distinct observed rows |
|---|---|
| Travel and service cannot fit break | 314 |
| Beyond walking tolerance | 66 |
| Chosen: price + round-trip/service time and urgency + quality/district adjustments - food preference | 35 |
| Closed or too late | 5 |
| Expected queue exceeds patience | 4 |
| Service and return do not fit available time | 24 |
| Chosen: weighted preference draw | 1 |
| Eligible | 2 |
| Out of stock | 7 |

The old text **“Service and return do not fit available time”** remains observable at native pace alongside successful purchases. The new fields expose the pace; they do not supply a new per-decision textual cause. Full retained daily model counters for this shop:

| Arm / day | Offers considered | Access rejections | Closed | Service | Stock | Chosen offers | Purchases |
|---|---|---|---|---|---|---|---|
| Native pace / 1 | 3436 | 1856 | 1332 | 68 | 0 | 153 | 90 |
| Native pace / 2 | 384 | 0 | 105 | 218 | 38 | 10 | 10 |
| Speed 20 / 1 | 617 | 415 | 202 | 0 | 0 | 0 | 0 |
| Speed 20 / 2 | 145 | 0 | 41 | 104 | 0 | 0 | 0 |

**#230 speed-20 control: PASS against its stated expected outcome.** Normal reload of the same pristine checkpoint; `time run_until` to day-one noon and then day-two noon with `speed:20`. Both noon revenues and session purchases are 0, shop ready, no Paid natural receipt. Verbatim diagnostics:

```json
"travelClockRate": 6.666666666666666,
"walkingMetresPerGameMinute": 0.30000000000000004
```

These are paired observations at this head; no code root cause is assigned.

## S2 five checks and new fixtures

All probe commands use `python3 scripts/mac-agent call console '{"line":"<command>"}'`; transport exits 0. Verdict columns are the probe's own output. Food read at the probe hour is open and `foodOwned=true`.

| Check | Exact pc command / own verdict | Result |
|---|---|---|
| 1 Food | `pc probe c0 address:ba%3Astreet_fourthavenue:15:shop 10000`; Food / FullServiceBusinessSimulator; `refusal=None`, `receipt=Paid`, quoted/paid=500; wallet 107448→106948, orders 11→12, orderCompleted=true, ordersAdded=1, claimedNativeEntry=false. Croissant $5, bag $0 paid. | **PASS** |
| 2 Goods | `pc probe c0 address:ba%3Astreet_fourthavenue:14:shop 10000`; Goods / SelfServiceBusinessSimulator; `refusal=None`, `receipt=Paid`, quoted/paid=500; wallet 86689→86189, orders 0→1, orderCompleted=true, ordersAdded=1. claimedNativeEntry=true, claimedEntryRetired=true; `sameHourRegenerationMayAddOneEntry=true` is retained in the raw verdict. | **PASS** |
| 3 Service | No probe invoked. Official recipe refuses native finish, detailed below. | **NOT RUN — needs a service fixture that completes native requirements.** |
| 4 Ceiling | `pc probe c1 address:ba%3Astreet_fourthavenue:15:shop 0`; Food, basket=500, ceiling=0, `refusal=Ceiling`, `receipt=Missing`, paid=0; wallet 114861→114861, orders 12→12, native={}. Exact reason: **“Basket 500 is above the ceiling 0”**. | **PASS** |
| 5 Mid-probe reload | Official Mac docs/log at `fae4803` and installed Ops hash have no suspend/interleave support; unmerged branch not installed. | **NOT RUN — needs suspend/interleave inside the synchronous call.** |

**Supplemental completed-receipt cold reload:** after the two further fresh cities/process restarts, `pc probe status` returns the Food receipt Paid, recovered=0. Native ledger remains 12 orders / $60, stock and wallet unchanged. Parsed paused reload copies preserve identities, wallets, complete receipt maps and minute. This does not exercise interruption inside a probe.

Exact new recipe/read commands:

```sh
python3 scripts/mac-agent shop-recipe flows/mac/s2-goods-shop.json --timeout 60
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-goods"}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_fourthavenue#14"}'
python3 scripts/mac-agent shop-recipe flows/mac/s2-service-shop.json --timeout 60
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-s2-service"}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_thirdavenue#11"}'
```

**Goods: 14 Fourth Avenue**, `ba:street_fourthavenue#14`, `ba:neighborhood_hellskitchen`, `ba:buildingsize_c`, version 1, 225 m². Independent readiness `True`, reasons `[]`. Recipe exit 0, checkpoint `FT Mac S2 Goods v1` saved; 200 cheap gifts at $5, 400 bags, available cabinet register and seven Default register shifts. Required status read exits 1 `ops_fixture_unknown`. Its failure is retained; the separately required `shop_state` read is ready, so the sale runs without a recipe retry, fixture change or alternate status id.

**Service: 11 Third Avenue**, `ba:street_thirdavenue#11`, `ba:neighborhood_hellskitchen`, `ba:buildingsize_c`, version 1, 225 m². Independent readiness `True`, reasons `[]`. Recipe exit 1: outer `ops_fixture_incomplete`, inner `ops_shop_requirements_unmet`; next_step=24, steps=25; failing report `step=23`, `kind=finish`, `ok=false`, `reason=ops_shop_requirements_unmet`. Required status exits 0 and returns that incomplete job. The three $20/$25/$30 fee offers all name chair `eRQWXvmXBkasThvIdppsLA==` as holder; two Default staff shifts target register/chair. Native placement report attaches register `2rm049Eu1UmyKPE0I7HsPg==` to cabinet `bIBT80Pby0iVAerx+TZjQ==`. Readiness=true does not resolve the native finish refusal. `FT Mac S2 Service v1` was **not saved**. The stage had begun advancing, was stopped at 08:07 before any probe, and no fixture workaround or retry was attempted.

## F8h check 1 shop assertions

`op foreign_patch '{"action":"install","kind":"prefix"}'` exits 0. Normal load of `FT Mac 11j speed20 noon` reports `food.checkout.shop optional off: foreign prefix by agent-rig.foreign.test on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)` and the refusing hook owner. No shop-log postfix is installed.

**PASS:** the 12:00–15:00 native hand-back window changes revenue $0→$45. Independent native ledger: **8 completed orders, 9 croissants / 8 bags**, $45. The saved control has **zero PC food-sale receipt keys**. `op foreign_patch '{"action":"remove"}'` exits 0; final target owners contain only `persistentcity.food-authority`. Normal load of `FT Mac 11j native control` restores checkout active. At hour 65 (17:00) the boundary reports Published, desired=true, managed=true, lastBoundary=64 (16:00), lastReadiness=Ready. Revenue is $70 at 16:00 and remains $70 at 17:00. Test prefix removed; no native rival fields written.

## E5b2–5, F9, F6b and F7 sale parts

**E5b2–5 / F9: NOT RUN.** The single-croissant fixture does not cover original-three/mixed/eight-nine-offer, catalogue lifecycle, metadata, resource/refill or 8/64-shop cases. Supported shop_edit cannot add the required holders/prices. No formal FT-F9/RIG-ACCEPT, paired seven-day/config/body/research/vendor campaign or official sealing was run.

**F6b check 2: PARTIAL.** The 14 hourly rows now contain natural staffed-shop throughput. Lunch deltas include 19, 20 and 20 completions/hour; the separate native-control delta is $45. This is one shop, without a representative cohort or advertised native MealsPerHour projection, so no median/default-ceiling calibration assertion is passed. Other rival limits/profile/replay comparisons remain NOT RUN with their fixtures/capabilities.

**F7 check 1: PARTIAL.** Natural noon has 100 Paid food receipts; the post-midnight checkpoint has 90. Cold paused reload copies preserve all 957 identity and wallet tuples, complete receipt maps and saved minute in both arms. The probe checkpoint also preserves its Paid probe and 11 natural receipts, native 12 orders / $60 and stock. All extracted snapshots have schema 1, matching epoch schema/minute/population/payload length and no accounting-stop marker. These are parsed epoch and native reload observations, not a full reader-corpus validation. Pending, same-minute fault, relocation and menu matrices remain untested. **F7 check 3: NOT RUN**, needs actual Pending and supported interleave; none fabricated.

## Reload accounting epoch, X3, GR-1 and diagnostic subsets

Fresh accounting city, full modpack, Enabled/PhysicalEnabled, autosave guard verified. S1 is saved after creation/city running, then normally loaded once. An early console read refuses during loading (Tooling gaps); no load mutation is replayed. The corrected wait requires the requested `load.name`, status done, city and save name.

| Checkpoint | Saved / loaded logical minute | Residents | Identity digest | Result |
|---|---|---|---|---|
| S1 | 1920.0 | 957 | `251416a318f7ded3798e49368b6b10727aae0db8ea5a2cd91ec829336a6ce162` | Identity digest and minute equal after reload |
| H1 | 1980.0099983215332 | 957 | `251416a318f7ded3798e49368b6b10727aae0db8ea5a2cd91ec829336a6ce162` | Identity digest and minute equal after reload |

**Reload checks 1–2: PASS.** S1 settled 49/49, 0 refusing; the one-hour H1 copy preserves identity/wallet maps and minute. The first H1 hook snapshot is 38/49; a further normal load of the same H1 settles **49/49**, 0 refusing, while still paused at saved minute `1980.0099983215332`; its saved copy again preserves identity, wallets, receipts and minute. No “Accounting input epoch was lost”, `start refused (clock.city)`, “startup is still waiting” or “City clock already active” appears in the two retained epoch logs. Autosave suppression is true after these completed loads and around the hour.

**Additional fresh-S1 observation:** all 957 wallet tuples differ between fresh S1 and its paused reload copy at the same minute 1920. For c0, `balance_units` is 81475529605120→78527839862784; c1 is 27599459844096→25302189211648. Identities, minute and receipt maps match. H1, the post-midnight checkpoint and the paid checkpoints preserve wallet tuples. No code root cause is assigned; `s1-wallet-differences.json` retains examples.

**X3-1: PARTIAL.** Native current-format loads succeed with 957 identities and schema-1 extracted bodies, without “start a new city”; the specified literal population-read `Loaded`/schema assertion is not emitted. No auxiliary reader verdict is substituted. Its deleted check doc is not recreated.

**GR-1:** check 1 PASS: fresh `Business types (load): opened=0 closed=0 changed=0; types=37`, save and reload accepted. The food shop's later load observes opened=1; the full close/replace lifecycle (check 2) is NOT RUN. Check 3 needs an established modded-type fixture; check 5 needs a pre-GR-1 save outside this fresh-head-only run. Check 4 PASS: more than three normal-speed game hours; all retained business-type scan ms/max_ms values are at most **1.37 ms**, no value ≥30. Check 6 PASS on the post-daily checkpoint: c0 balance_units 29695403884544 before/after; c1 33391484967322 before/after. All 957 post-midnight wallet tuples match after reload.

**F2 diagnostic subset: PARTIAL.** Exact console reads: `pc movement status`, `pc movement corrections since 0`, `pc movement holds since 0`. Sample: followers=10, walker lag p50/p95/max=0.50/4.65/8.25 m, corrections=320, on_screen_moves=0; car followers=3, car lag p95=27.60 m, car corrections=47. Correction/hold outputs are bounded rings. No controlled camera/body/contact/fault/cadence replay assertion is inferred from these reads; visual/pointer rows stay for Peter.

**F8f diagnostic subset: PARTIAL.** `pc capability business address:ba%3Astreet_fourthavenue:15:shop` reports `shop.fullservice-bag.v1 (implemented; rig pending (tests/A5IntegratedAcceptanceTests.cs))` and native FullService/paper-bag evidence. `pc capability show food.checkout.shop` reports active. The broader mechanism/vendor matrix is not run.

## Performance

**PASS for the p95 gate, provisional shared-Mac sample:** speed 1, ready open shop, 1,291 frames; union mod-work p95 **4.2778 ms**, maximum **33.609 ms**. Coverage complete=true, validity=true, no invalid reasons, no dropped frames. Every returned mod-work spike ≥30 ms (one) is below:

| Frame | Mod-work ms | Whole-frame ms | Dominant component / self ms | Condition |
|---|---|---|---|---|
| 13479 | 33.609 | 65.8872 | PopulationRuntime.Update / 33.2861 | speed=1, hour=10 |

Whole-frame timing is separate: union-recorder p95 **35.3626 ms**, max **165.6998 ms**; outer recorder p95 **40.2805 ms**, max **432.6592 ms**. Native save attempts/allowed/refused=0/0/0, native saving frames=0, save_activity.clean=true. Epoch sequence stays **3**. Autosave suppression verified before/after. No save is inside the window.

The brief identified other live harness work; protected sessions were not inspected or changed. This is a provisional shared-host sample, not a paired optimization or Windows claim. Additional logged PC component/aggregate timing lines containing values ≥30 ms are flagged in `observed-timing-flags.json` (deduplicated by exact text, with source lines); they are not extra enumerated union frames. No production optimization or save profiling was performed.

## Findings

- Verified by rig: normal-pace natural sale through next-day noon, with 100 Paid natural receipts and independent day-one/day-two order totals. At speed 20 the same pristine checkpoint stays at zero, with the verbatim pace fields above.
- Verified by rig: S2 native Food and actual gift-shop Goods sales, zero-ceiling refusal, and completed receipt continuity. Mid-probe interruption remains untested.
- Goods recipe is ready and sells; its required status id returns unknown. Service readiness/fee holders look ready, but native finish refuses. Service sale and its recipe checkpoint are not claimed.
- Fresh S1 wallet tuples change at the same minute; post-hour/post-day and paid reload tuples match. This is an observation requiring owner interpretation, not an assigned root cause.
- Restore, hash verification, process stop and lease release completed before documentation/push.

## NOT RUN grouped

- Reserved `present`, `foreground`, `priority` flows: need RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY.
- Stress10k, city-50k, RIG-FAULT, RIG-PCFIX, official manifest sealing, F4f / F7e implementation: excluded by the brief.
- Pointer, hover, focus and click assertions: retained for Peter's playtest. Windows client/PC-overlay-specific rows: Windows PC offline; no contact made.
- E5b2–5: needs original-three, mixed/eight-nine-offer, catalogue lifecycle/metadata/resource/refill and 8/64-shop fixtures. The current single-croissant fixture and supported `shop_edit` cannot add the required holders/prices. No workaround.
- F9: needs formal FT-F9/RIG-ACCEPT, paired seven-day/config/body/research/vendor acceptance fixtures and comparisons; no sealing.
- S2 mid-probe reload / F7 Pending capture: needs installed suspend/interleave support; official Mac docs/log remain at #75 with no such support. Completed-receipt reload is supplemental only.
- Remaining F2/F8 matrix arms: needs controlled journey/body/contact or option/fixture input and matched replay; read-only diagnostics alone do not pass them.
- GR-1 modded type / older-city refusal: needs a modded-business-type fixture / pre-GR-1 save, outside this fresh-city-only run. GR-1 close/replace: needs supported business lifecycle fixture; no native rival write or manual workaround.

## Tooling gaps

All native commands below are from the official checkout, with the same owner/environment. Full stdout/stderr and command exits remain in `commands.jsonl`, `failures-later.jsonl` and the named response files.

| Capability / flow | Exact failed command | Exit and exact error | Blocked checks / disposition |
|---|---|---|---|
| shop_fixture status / `flows/mac/s2-goods-shop.json` | `python3 scripts/mac-agent op shop_fixture '{"action": "status", "id": "mac-s2-goods"}'` | **1**; stdout/stderr JSON `error: "ops_fixture_unknown"`, `reply.ok:false`, `reply.error:"ops_fixture_unknown"`. | Required fixture-status readback FAIL. Recipe itself exited 0; independent shop_state is ready, so the Goods sale ran. No retry/alternate id/fixture change. |
| shop-recipe / `flows/mac/s2-service-shop.json` | `python3 scripts/mac-agent shop-recipe flows/mac/s2-service-shop.json --timeout 60` | **1**; stderr JSON outer `ops_fixture_incomplete`; result `complete:false`, `error:"ops_shop_requirements_unmet"`, `next_step:24`, `steps:25`; last report `ok:false`, `reason:"ops_shop_requirements_unmet"`, `step:23`, `kind:"finish"`. | Service recipe FAIL; S2 service NOT RUN. No `FT Mac S2 Service v1` checkpoint. Status and independent readiness/holders recorded; no named missing requirement was returned. |
| console / fresh S1 reload | `python3 scripts/mac-agent call console '{"line": "pc capability status"}'` | **1**; exact stdout/stderr: **`console works in the city only; the game is at the loading now.`** | Separate stage helper accepted the old load receipt. Corrected to require requested load.name; waited for the already-requested load and continued. No load replay. Final S1/H1 assertions observed separately. |

**Stage runner:** supplied `reload.py` template used; **`from pathlib import Path` is present**. Initial template reload finishes its log-copy tail; archive `player-template-reload.log`. Final AST check confirms every new/copied stage script that uses Path imports it. The 11f NameError did not recur. The service runner had begun a normal advance after the failed recipe; it was deliberately terminated (stage exit 143) before any service probe, paused at 08:07, and its recipe-success guard corrected. `service-stage-stop.json` records probe_file_exists=false; no service-probe command appears in the command archive.

Optional stage-only console IL inspection initially reports exactly `Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly: 'NaughtyAttributes.Core, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'`; re-run with the private Managed directory as resolver search path succeeds. Individual failed dotnet exit was not retained (compound shell ended 0 after cat). No native command or pace change resulted. Two report-helper corrections were also needed: filename selection (`KeyError: 'epoch-extract-S1'`) and numeric parsing (`ValueError: could not convert string to float: '1.29.'`). They do not replace native verdicts. Details stay in the stage.

## Fixtures

All were newly created in this run with candidate bytes installed. SHA-256 was captured and verified unchanged after stop/restore; all **20/20** match. Full absolute paths and sizes are in `fixtures.json`. The failed service recipe leaves only its initial fresh-city save; the requested service recipe checkpoint was not created. Disposable saves remain in the isolated profile.

| Checkpoint / fresh city | SHA-256 |
|---|---|
| FT Mac 11j epoch H1 copy | `82eef7c4a9ccde33e9e9d617018f32e5562d668c7de678f6e209234bf0c3c2ad` |
| FT Mac 11j epoch H1 settled | `959e69e7929e985a1d41df8fc8b49fa0f53448496751b2cde761173966fbc3e8` |
| FT Mac 11j epoch H1 | `45242e482a6cfcfba8e87bbea943d7d981b142fe17bcec48148503f7aad1c975` |
| FT Mac 11j epoch S1 copy | `25f2b5be4c435d87b04e55202fde800f89e625c553787d976ff8185eaf3c7324` |
| FT Mac 11j epoch S1 | `d4a003b96779d1e93ac4142289a329c38f36f15fe34c80e8cb2c12ae8f0ed312` |
| FT-PC-Mac-20261111j-Epoch | `2c737df8bf41f8e1f52cccd90dda1414ff4ab6feaf3037c9a5ebee3cb1c16216` |
| FT-PC-Mac-20261111j-S2-service | `f5885a99aa9c4690ee72fcdb2233a6a955ea261df4c774bdb851ade224e54bfc` |
| FT Mac 11j S2 goods result | `3418215b82672802cfc76d1b87529070ee392810e6614eea0ccf9c63ca49ccdd` |
| FT Mac S2 Goods v1 | `7737688d1e36595136a19e4cda55795694cc073004f1cb8e44e9e9d6fd3ecd69` |
| FT-PC-Mac-20261111j-S2-goods | `8b4454ffde6535dea0c60b19ea16ecb6267439f133ce6370018a5bc585624a37` |
| FT Mac 11j F8h Food v1 | `6a9b601360334cc234901e68df47c1373a23d707b186a182bf0e4b2ca2c91f1b` |
| FT Mac 11j S2 completed food | `b7bfecb37cb33cd2ffabdb94dcdbfce662ced4b69958bd101cc34bcbee9a0fee` |
| FT Mac 11j food reload copy | `5f4507e31230181e4637340d4b6b5b7f4a6d1bac7ba7f525564efc0fb6662d86` |
| FT Mac 11j native control | `3c0d25a0677eeb41d2e038453d0e14077cef8c7815e4a926104c6839cd0910ed` |
| FT Mac 11j natural midnight reload | `8439f63505969bafbaad6a5ee422e062081d69c3f6e06ad1c0082934ae7b7b26` |
| FT Mac 11j natural midnight | `e91b13079653953b070796f0485d39ee62e19e7fa4ee3a94a62e6d6ecd11162f` |
| FT Mac 11j natural noon | `69ed5e69c9324e1035234e7a103d4faf76d96ea5e9b2375a6db237506012b938` |
| FT Mac 11j natural paid reload | `41b9452f374faa3332857d517efcf78b954566420e01f177eb8c861fad85b0dd` |
| FT Mac 11j speed20 noon | `aa9257169f37472589dfcbfd72e4242a21d7410738ce9a873c7ed71e4c76243a` |
| FT-PC-Mac-20261111j-f8013e7 | `c1ac5658d15e06c94ee19e5d029ba52940fb98d59673f87efffdb9987d46cc23` |

## Evidence archive

Raw JSON, logs, saves and binaries are not committed. Selected archive hashes:

| File in <stage> | SHA-256 |
|---|---|
| dll-check.json | `9394a9e898e5c1156cb72e650acd09d7d63d6184e4110a2495f6ce64b9a73da5` |
| shop-readiness.json | `2533e3d3a367dd7fa9c897555bd107e90e55422fbea09f0116c07f78ccbb8c81` |
| first-sale-assertion.json | `df0aa2f1f9e989a0e5b10b7541d89833285a2394364fbecc2f9ebeb2aeeb3c49` |
| analysis-summary.json | `c03fa66c5f9a9c7f4d77fd4f53179c6d1c3d27f51b45c373e4d3bf92b427b380` |
| food-probe.json | `ebb83a7b9d0136e476408a8642e24da027e4185b0354ecdf5777635575146723` |
| food-ceiling.json | `0c0d9dfff4026be052b712eb074e280d526fb6d2a0f67bb6e609ef12128cbcd7` |
| goods-probe.json | `161dc2412c463fc0097e30952d05147a9440fd2c38e343c89d072f3f923a22a3` |
| goods-fixture-status.json | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| service-recipe.err | `608ec5cb7a9d8c280ebf41af673936685a66d92ba0dbee1cfb3eb585bf360e83` |
| service-chair-holder-check.json | `b703988f1f90743544ab16d9825aabb35193afca59f43e10c47e18808978c367` |
| performance-summary.json | `e8b033615b10cb86a7aa6d647a485884e53baf261ae5e173f8425e50c7f76383` |
| s1-wallet-differences.json | `5049e8b82a9d2afbf210f0d722b84da6cac5b307bae6901571d7bb99b780dbba` |
| stage-script-path-check.json | `d0f7e8ad3104d48c6ae47b5fdeb03619f4a43a9ae401f203d28ac1abd5026450` |
| restore.json | `222a39f5c934e8d1caa07ed922d736fd1f240a365de5ba0e7be98d20953f74e1` |
| fixture-hash-verification.json | `99ad90eb134331d6835fa2d1afa37f11638e1b12ee0bba3c332beffa97d8aa53` |
| owned-process-verification.json | `e1a0730c6bb135ba5adec7b75345446173ef991126b10cc8b86ad7cc22f306b1` |

The conservative item-7 window (including supplemental reload work) was **31.5 wall minutes**, below the about-90-minute cap. The normal-speed natural-sale window is separate.

## Per-check results

Counts apply only to these 34 rows; PARTIAL denotes the observed subset. Prior passes are not recounted.

| Check | Result | Evidence / reason |
|---|---|---|
| mac-autosave | **PASS** | Suppressed before windows and after reloads; original plist restored/hash verified. |
| food shop selection | **PASS** | 15 Fourth Avenue, Garment District, C2, 225 m²; documented $100M recipe. |
| food serving readiness | **PASS** | Independent ready=true before sales; available cabinet-mounted register; seven staffed days. |
| F8h-1 shop assertions | **PASS** | Foreign refusal named; native delta $45; zero PC food receipts; owner removed; managed again at boundary 64. |
| F8h-2 / #230 natural first sale | **PASS** | Normal load through day-two noon: $50 / 10 orders today; 100 session purchases; 100 Paid natural receipts; native pace. |
| #230 speed-20 control | **PASS** | Same pristine checkpoint; day-two noon $0 / 0 purchases, clock rate 6.666666666666666, walking 0.30000000000000004. |
| S2 food sale | **PASS** | Food, FullService, Paid 500 cents; wallet -500; orders +1. |
| S2 goods sale | **PASS** | Gift shop, SelfService, Goods, Paid 500 cents; wallet -500; orders +1. |
| S2 service sale | **NOT RUN** | Needs a service fixture that completes native finish: ops_shop_requirements_unmet. No probe invoked. |
| S2 ceiling refusal | **PASS** | Basket 500 is above the ceiling 0; Ceiling/Missing; wallet/orders unchanged. |
| S2 mid-probe reload | **NOT RUN** | Needs installed suspend/interleave support; completed-Paid cold reload is supplemental only. |
| goods required fixture-status read | **FAIL** | Required id mac-s2-goods returned ops_fixture_unknown, exit 1; independent shop_state ready and sale succeeded. |
| service recipe completion | **FAIL** | ops_fixture_incomplete / ops_shop_requirements_unmet; next_step 24/25, failing finish report step 23. |
| F6b-2 hourly calibration | **PARTIAL** | 14 hourly rows now contain natural sales; one shop is not a representative cohort or native MealsPerHour median proof. |
| F7-1 sale/save subset | **PARTIAL** | 100 Paid natural receipts and 90 post-midnight receipts preserve identities/wallets/maps/minute on cold reload; larger matrix remains. |
| F7-3 receipt_pending | **NOT RUN** | Needs actual Pending checkpoint and supported interleave capture. |
| reload-accounting-epoch check 1 | **PASS** | Fresh S1 reload at minute 1920, settled 49/49, 957 identities equal; early console-read race retained as tooling gap. |
| reload-accounting-epoch check 2 | **PASS** | H1 at 1980.0099983215332; identity digest equal; final normal reload settled 49/49 at saved minute. |
| X3-1 | **PARTIAL** | Native schema-1 reload accepted with 957 identities; specified literal population-read Loaded/schema assertion is not emitted. |
| F2 diagnostic subset | **PARTIAL** | Movement status/corrections/holds read; controlled journey/body/contact/replay matrices not run. |
| F8f mechanism diagnostic subset | **PARTIAL** | Ready food scope reports shop.fullservice-bag.v1; broader shop/vendor mechanisms not run. |
| GR-1 check 1 types at load | **PASS** | Fresh load opened=0 closed=0 changed=0; types=37; save/reload accepted. |
| GR-1 check 2 open/close/replace | **NOT RUN** | Needs supported close/sell/type-replacement fixture; opening observations retained only. |
| GR-1 check 3 modded type | **NOT RUN** | Needs an established modded-business-type fixture. |
| GR-1 check 4 scan cost | **PASS** | More than three normal-speed game hours; recorded scan maxima below 30 ms; raw scan lines archived. |
| GR-1 check 5 older city refusal | **NOT RUN** | Needs a pre-GR-1 save; outside fresh-current-head-only scope. |
| GR-1 check 6 post-daily wallets | **PASS** | Post-midnight normal reload preserves all 957 wallet tuples; c0/c1 budget words retained. |
| E5b2 shop matrix | **NOT RUN** | Needs original-three/catalogue lifecycle and shop-count fixtures. |
| E5b3 shop matrix | **NOT RUN** | Needs mutation/reconciliation/storm and 8/64-shop fixtures. |
| E5b4 shop matrix | **NOT RUN** | Needs metadata override/control and held/replayed original-three decisions. |
| E5b5 shop matrix | **NOT RUN** | Needs mixed original/dynamic, eight/nine-offer, refill/resource fixtures. |
| F9 acceptance | **NOT RUN** | Needs formal FT-F9/RIG-ACCEPT and paired seven-day/config/body/research/vendor fixtures; no sealing. |
| Mac mod-work sample | **PASS** | Provisional p95 4.2778 ms; single 33.609 ms spike flagged; complete/valid union, clean save activity. |
| restore/hash/lease | **PASS** | Normal stop; 260/260 original hashes and 20/20 fixture hashes match; autosave restored; lease free; owned game/helpers gone. |
