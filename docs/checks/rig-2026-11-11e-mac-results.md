# Rig results — 11 November 2026 (e), Mac (#70 smoke: --rig-test + shop_fixture, autosave-off re-runs)

Role `rig_test`, where `mac`, isolated instance `agentmac`. Candidate `86c2e92f33e176fd29cea0072c0f5341f299e97f`; `git fetch origin` and `git rev-parse origin/codex/persistent-city` matched the requested tip before build. Schema held at 1. Testing only; no production change, release, version/changelog, merge or issue creation.

**Summary: 4 passed / 1 partial / 0 failed / 1 not run.** Counts cover the two #70 tooling smoke rows, fix-reload-accounting-epoch checks 1–2, X3 check 1 and the deliberately unrun Mac F8h first-sale continuation. Three command refusals are recorded separately below; they are not failed numbered checks. Remaining catalogue rows are NOT RUN in this run, grouped below, and are not added to these subset counts. Existing results from other runs are retained.

`<stage>` is `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-11e-mac`. Raw logs, JSON replies, save payloads, stage-only tooling and backups stay there. Procedure/reference: [Mac run b](rig-2026-11-11b-mac-results.md), whose stage was read only.

## Build and rig

| Item | Value |
|---|---|
| Candidate / worktrees | Detached build `rig-2026-11-11e-mac`; separate evidence branch/worktree `codex/mac-rig-2026-11-11e` / `rig-2026-11-11e-mac-evidence`, both from the verified candidate. Main tree unchanged. |
| Host / player | Native Apple Silicon Mac; private `Big Ambitions Agent Tests Mac.app`; isolated `com.hovgaardgames.bigambitions.agenttests.mac` profile; loopback 28330, no tunnel. Runtime 7. |
| Official rig checkout | `508185c59979cd77e2129e998675503a3eba4563`, clean/read only. Official `scripts/mac-agent` used directly; no prior LocalControl wrapper needed. No install/update/rollback script used. |
| Build | `python3 tools/build.py --game-managed "$HOME/Library/Application Support/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; succeeded. Same private-app reference location as run b, with the already installed #70 overlay. |
| Environment | Every Python/.NET/build invocation used `PATH="/opt/homebrew/bin:$PATH"` and `DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec"`. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`; installed hash matched. |
| Dependencies / provenance | Reused local Harmony DLL/license; build verified pinned DLL SHA-256 `7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`. Mac assembly fingerprint `unknown`, 27 differences from declared sets; no Windows compatibility claim. |
| Modpack | 29 DLL paths match the [Mac owner record](f8h-check1-modpack-owners-mac.json). Hash differences are the candidate PC DLL and the preinstalled #70 FoodTrucks `Dependencies/BridgeControl.dll` (`98770f834dd9719d925e509e6dc2c45406462e24b66097ddf9cd1180e08d8d62`). No other mod was replaced. `modpack-comparison.json`. |
| Lease | Free/stopped before acquisition; owner `pc-mac-rig-2026-11-11e`, purpose `Persistent City 11 Nov Mac rig e`; same owner throughout. No token in arguments, evidence or report. |
| Mode / resources | Two headless launches. Immediately before launches: 78.9 GiB / 71% reported free memory, then 78.7 GiB / 54%. Both clear the documented gates. |
| Backup / installation | 260 files copied and SHA-256 recorded before mutation: isolated ModsLocal, profile top-level preferences, native preference plist and installation receipt. Candidate DLL, English locale and PC config installed only there; receipt hashes updated. |
| PC config | Enabled/PhysicalEnabled true; 48 pedestrians, 24 vehicles, radius 120 m; remaining compiled defaults. |
| Original autosave | No autosave keys in original plist. No preference edited to suppress saving. Both launches used `--rig-test`; suppression was read back after each completed creation/reload and before/after the one-hour window. |
| Rig ops | `ready`, instance `agentmac`; addon SHA-256 `0db54d83df58b6d7ed9bc7f35398aa2913c0ad4b1125ba59b2d007e0b57061fd`. |
| Offline checks | Candidate build and stage-only auxiliary reader build succeeded. No production/test source change, full suite or regression suite. Native and auxiliary results are distinguished below. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| #70 launch / creation | Fresh `FT-PC-Mac-20261111e-86c2e92f-Smoke` | `--rig-test` accepted; runtime 7, suppression true; native creation completed. |
| #70 shop recipe | `mac-agent shop-recipe flows/mac/f8h-food-shop.json --timeout 60` | PASS: 36 3rd Street, rented/staffed/stocked coffee shop; checkpoint `FT Mac F8h Food v1`. |
| Fresh process / epoch creation | Normal stop/start under same lease, then fresh `FT-PC-Mac-20261111e-86c2e92f-Epoch` | Candidate enabled; suppression verified again. |
| S1 | Ordinary save at epoch minute 1920; immediate bridge native menu/load | PASS check 1: running at saved 08:00, settled 49/49 targets; suppression true after completed reload. |
| H1 | Normal speed-6 run 08:00 → 09:00; ordinary H1 save and native menu/load | PASS check 2; same resident identities. X3 PARTIAL for the native Loaded/schema log assertion. |
| Cleanup | Normal stop, restore, hash check, release | 260/260 files match; no owned game/caffeinate remains; stopped, lease free, pending 0. |

## #70 smoke: --rig-test and Mac shop_fixture

**Both smoke rows PASS.** These exact launcher commands ran from the official rig checkout (owner/environment as above):

```sh
python3 scripts/mac-agent status
python3 scripts/mac-agent resources
python3 scripts/mac-agent lock acquire --purpose "Persistent City 11 Nov Mac rig e"
python3 scripts/mac-agent resources
python3 scripts/mac-agent start --mode headless --rig-test
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call new_game '{"name":"FT-PC-Mac-20261111e-86c2e92f-Smoke"}'
# Poll health until this named creation is done.
python3 scripts/mac-agent autosave
python3 scripts/mac-agent call state '{}'
python3 scripts/mac-agent call console '{"line":"pc capability status"}'
python3 scripts/mac-agent shop-recipe flows/mac/f8h-food-shop.json --timeout 60
python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-f8h-food"}'
python3 scripts/mac-agent call state '{}'
python3 scripts/mac-agent op shop_state '{"id":"ba:street_thirdstreet#36"}'
```

`start`, `autosave`, creation and recipe exit 0. Startup and later readbacks show `enabled=true`, `autosave_disabled=true`, `suppressed=true`, with `save_specific=false`, empty allow list and no save-mode error. Rig ops becomes ready during creation.

The recipe's documented `money set` and `state` readback gates both accepted **$100,000,000 before acquisition**; it could not reach `shop_fixture` otherwise. The successful result reports `money_set=100000000`. Its internal individual money replies are not separately printed by the stock launcher. State after construction reports $99,986,352. No other money setup was used.

**Address picked: 36 3rd Street, `ba:street_thirdstreet#36`**, Hell's Kitchen, `ba:buildingsize_a`, 75 sqm. Native readback: `rented=true`, `owned=false`, coffee shop `FT Mac F8h Food`; daily rent $225, deposit $13,500. Cash register, two bakery showcases, storage shelf and cleaning station were placed. One customer-service employee has seven 09:00–18:00 shifts of native type `Default`, wage $25/hour. Stock: 100 croissants, 300 bags; croissant price $5; initial revenue $0 on day 1. Recipe saves `FT Mac F8h Food v1` and verifies suppression again. Separate `shop_state` succeeds after the checkpoint.

The recipe's normal builder completed; no shop build error or timed-out mutation occurred. This establishes the Mac tooling path, not a paid-sale assertion. No `shop_log` recording patch, foreign patch, campaign or rival-finance write was requested.

### Exact command refusals

All three commands below exited **1**, stdout empty. Full replies are retained in `failures.json`; these are setup/inspection refusals, not product check failures.

1. `python3 scripts/mac-agent op shop_fixture '{"action":"status","id":"mac-f8h-food"}'`, after the successful checkpoint. Stderr verbatim:

```json
{
  "error": "ops_fixture_unknown",
  "reply": {
    "ok": false,
    "id": "aa36016247df4f12b7c0f0660dab637b",
    "session": "79a862fbb193423abc8765c29101d142",
    "error": "ops_fixture_unknown"
  }
}
```

The documented in-memory job resets on save-name change; the recipe's checkpoint changed that name. The completed recipe reply and separate native `shop_state` readback remain available. No mutation was retried.

2. `python3 scripts/mac-agent call new_game '{"name":"FT-PC-Mac-20261111e-86c2e92f-Epoch"}'`, issued while still in the smoke city. Stderr verbatim:

```text
new_game works from the main menu only; the game is at the city now.
```

No city was created by this refused command. A normal stop/start returned to the menu; the same fresh-creation request then succeeded.

3. `python3 scripts/mac-agent call time '{"pause": true}'`, immediately after requesting S1 load. Stderr verbatim:

```text
time works in the city only; the game is at the loading now.
```

The stage runner had accepted the previous creation's stale `load.status=done` snapshot. It was corrected to require the requested save/load name as well as completed status. It waited for S1, re-read suppression, and paused successfully. No save/load mutation was replayed.

## Autosave-off re-runs

### fix-reload-accounting-epoch checks 1–2

**Check 1 PASS.** A second fresh native FT city completed creation with PC enabled. Ordinary S1 is schema 1, success epoch `v1`, sequence 1, saved minute **1920 (08:00)**. Direct bridge `load S1` executes the native return-to-menu/load path. S1 returns at 08:00 with PC city running; the settled hook report is **49/49**, zero checkout refusals. There are zero lost-epoch, `start refused (clock.city)`, startup-wait or already-active-clock messages in the final log. Suppression is true after the actual completed named reload. The earlier autosave-only qualification is resolved.

The first hook snapshot, taken before physical preflight completed, was 38/49. Settled `s1-warm-hooks.json` is 49/49, with physical and inspector capabilities active. This is preflight progression, not a permanently missing patch.

**Check 2 PASS, reverified at this head.** Reloaded S1 advanced normally at speed 6 to 09:00, H1 saved at exact logical minute **1980.0099983215332**, then native menu/loaded. PC returns running, settled 49/49, at that saved native/logical minute, lag 0; no accounting refusal/stop or historical clock misses. H1 and H1-Reload each have **957 residents / 957 visitors**, with equal resident identity SHA-256:

`b51add9b565d560204fba71f309040b4ff97403cf427a79e7b8ae88c2d0c90ef`.

The early hour window included an accounting catch-up hold (observed maximum recovery 87.6327982 seconds); later diagnostics showed ordinary progress and no refusal. No clock bypass or time jump was used.

Commands after the normal restart were `start --mode headless --rig-test`, `autosave`, native `new_game`, `save S1`, `load S1`, `autosave`, `time {"run_until":{"time":"09:00"},"speed":6}`, `autosave`, `save H1`, `load H1`, `autosave`, `save H1-Reload`, with state/status/hooks and read-only `pcstate` projections. Exact argument arrays, exits and UTC timestamps are in `commands.jsonl`. No autosave file was found for either created character at final extraction.

### X3 check 1

**PARTIAL.** Autosave-off is now established before/after the hour and after H1 reload. Native H1 load succeeds with the same residents, schema-1 persisted payload and no `start a new city` message. The native PC log still does not emit the specified literal population-read `Loaded` / schema-1 assertion.

The stage-only .NET reader reports `Loaded`, schema 1 for S1 and the shop checkpoint. For **both H1 and H1-Reload** it reports exactly **`Invalid state: Invalid visitor next event.`**, while native Mono loads H1 and reaches city running. This repeats the prior auxiliary/native disagreement. Identity extraction uses the expanded saved payload independently of that validation result; no full-payload validity or native-corruption verdict is claimed. No production logging or codec change was made. X3 lives only in the history tag; its result is here and in the PR body, without recreating that deleted check doc.

## Performance

**Provisional:** concurrent harness activity was not excluded; the other-project session was left alone. No mod-work union p95 window, before/after comparison or save profiling was requested or run. The <5 ms mod-work p95 gate is not assessed here. Whole-frame CityPerformance/RLO figures are separate.

Activation conflict scans: smoke creation **14.312 ms**, epoch creation **19.600 ms**, S1 reload **11.471 ms**, H1 reload **7.792 ms**. These are startup observations.

Every emitted PC slow-routing component observation ≥30 ms in the two process logs is flagged below:

| Log line / frame | Component | ms |
|---|---|---:|
| `player-smoke.log:1480` / 2602 | preparation | 126.548 |
| `player-final.log:1504` / 6094 | preparation | 150.982 |
| `player-final.log:2140` / 6787 | preparation | 137.088 |
| `player-final.log:2182` / 6976 | preparation | 61.073 |
| `player-final.log:3009` / 10159 | preparation | 121.706 |
| `player-final.log:3027` / 10227 | closure refresh | 98.771 |

Aggregate diagnostic maxima ≥30 ms, not additional enumerated samples: scene search **133.210 ms**, advance **43.141 ms**, advance unit **41.485 ms**, route slice **41.484 ms**, preparation **137.088 ms**, closure refresh **98.771 ms**. The emitted preparation sample reaches **150.982 ms**; the aggregate observation does not replace that higher event. `observed-spikes.json` retains both. Unlogged samples cannot be enumerated.

## Findings

1. #70's Mac launch guard and funded food-shop recipe are verified by rig. The earlier Mac rental blocker is removed for this fixture.
2. Immediate saved-minute reload passes with autosave suppression established; check 2 also passes at this head.
3. X3's autosave gap is resolved, but its native Loaded/schema log assertion remains absent; the auxiliary H1 validation disagreement remains.
4. The three exact command refusals above were inspection/setup errors. The recipe itself completed, and no timed-out mutation needed recovery.
5. Existing headless shader, portrait-readback and pinned-mod asset/API limitations remain in the raw logs. No OS settings or unrelated process were changed.

## NOT RUN grouped

**PC rig 11d coverage — no duplicated shop rows.** Mac F8h first paid sale (`f8h-2` continuation, counted as this run's single NOT RUN row), F8h check-1 shop assertions, E5b2–5, F9, F6b, F7 checks 1 and 3, and other rows blocked only on PC shop rental remain with 11d. This run verifies the distinct Mac recipe path only; it does not report 11d outcomes.

**No additional row blocked solely on autosave remains in the earlier Mac PARTIAL set.** E5b3-1 still needs observation of transient Warming; F9 provenance still needs supported-reference/comparison evidence. Neither gap is solved by disabling autosave or renting a shop. They are NOT RUN here and retain their earlier results.

**Existing PC evidence / no Mac-specific gap:** reload-epoch 3–4, F8a-9/F8bg-8 RLO-toggle checks and F8h foreign-patch assertions were covered by [PC run c](rig-2026-11-11c-pc-results.md). NOT RUN here; no duplicate option/patch mutation. No new RLO setting change was made.

**Catalogue rows beyond this subset:** the [earlier Mac catalogue](rig-2026-11-11-mac-results.md#not-run-grouped-by-missing-capability-fixture-or-needs-windows-client--pc) remains the row inventory, with the reasons below replacing obsolete blanket claims that Mac rig ops or autosave suppression do not exist:

- **Shop/catalogue/metadata scenarios:** `e5b2-2..4`, `e5b3-2..6`, `e5b4-4..6`, `e5b5-2..6` are NOT RUN here: PC 11d overlap or require their named mixed-business/catalogue/mod-set/comparator setup beyond this coffee-shop smoke.
- **Save/schema matrices and independent transport:** `e5b4-1..3`, `f8a-3`, `f8bg-2` are NOT RUN: named invalid/interrupted/schema fixtures or independent transport are not created by this autosave/recipe run. Schema stays 1; no historical fixture is loaded.
- **Windows/input comparisons:** `f2g-1..11`, `f5i-1..5`, `f8a-4..5`, `f8a-10`, `f8bg-3..4`, `f8bg-9` are NOT RUN: named Windows/input or cross-player comparison arms; no Windows contact is authorized in this run.
- **Street Food / FT-E6-Vendors:** `e6f-1..7` are NOT RUN: pinned Street Food vendor API is missing; the coffee shop is not the named cart/truck fixture.
- **View/checkpoint/comparison arms:** `f2c-1..5`, `f2d-1..7`, `f2e-1..2` are NOT RUN: named view/cadence/checkpoint and matched comparison fixtures are outside the two requested autosave checks; headless shop setup does not supply them.
- **Performance arms:** `f4a-1..8`, `f4b-1..8`, `f4c-1..5`, `f4d-1..15`, `f4e-1..5`, `f8a-12`, `f8bg-10` are NOT RUN: named matched performance fixtures/producers and excluded capability runs. No generic p95 claim is substituted.
- **Fault arms:** `f8a-6..8`, `f8bg-5..7` are NOT RUN: RIG-FAULT explicitly excluded.
- **Acceptance arms:** `f9a-2..10`, `f9bdf-2..4`, `f9c-1..6`, `f9e-1..3`, `f9e-5..6`, `f9g-1..4`, `f9h-2..3` are NOT RUN: 11d overlap or named FT-F9 acceptance/matched fixture requirements beyond this Mac smoke.
- **Pointer/hover/focus/click:** `f5i-6`, `f9e-4` and related interaction assertions are NOT RUN: Peter's playtest.
- **Explicit exclusions:** RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY, stress10k, city-50k, RIG-FAULT, official manifest sealing, F4f/F7e implementation, release and Workshop are NOT RUN by instruction.

These are run-local omissions, not reversals of passes in other reports. No remaining Mac-only row whose sole prerequisite was this recipe or autosave suppression was identified after the targeted re-runs.

## Fixtures

Two fresh disposable FT characters created by native `new_game` at the candidate, with PC enabled, only in the isolated agentmac profile. Six checkpoints remain there; no preexisting city was loaded. The automatic creation files have no PC payload; ordinary shop/S1/H1/H1-Reload checkpoints contain schema 1. Success epochs are `v1`: shop sequence 1 at minute 1920; epoch city sequences 1–3 at minutes 1920 and 1980.0099983215332. No Recover file was found for either character.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT Mac F8h Food v1` | `20bce5d56c9adbbeb476a348eb8ca34b11b63b14eb507bdb6ed723517dd1b929` | 1039457 |
| `FT-PC-Mac-20261111e-86c2e92f-Smoke` | `6ecf318b1990e53313d44c97ea41e0f1bd6bb131556fe2508e423046a0a4760a` | 513659 |
| `FT-PC-Mac-20261111e-86c2e92f-Epoch-H1-Reload` | `c9b2ce204cfa0358d2f7a7101db32efa8fe55e8e96f805e283ebfda1a4e8ebad` | 2769179 |
| `FT-PC-Mac-20261111e-86c2e92f-Epoch-H1` | `9dfe96bb1b5b85730708bffcb73b1a21081b3debd768579b64b46a374d1568a8` | 2726141 |
| `FT-PC-Mac-20261111e-86c2e92f-Epoch-S1` | `db723ad356d2fddedfa7cc62faae29b3b4014116dfedf47dcce79beb67becad2` | 674777 |
| `FT-PC-Mac-20261111e-86c2e92f-Epoch` | `fd9efa2affe92dc04d18a0cc6e93fa8e7b190b8b50ae5889497c119006d53282` | 506779 |

Shop address: **36 3rd Street (`ba:street_thirdstreet#36`)**. No campaign purchased, rival finances written, real save accessed or fixture save removed. Raw saves stay out of Git; `fixtures.json` retains payload lengths/hashes and epoch markers.

## Evidence archive

All paths below are under `<stage>`; these hashes allow the small report to identify the raw evidence:

| Artifact | SHA-256 |
|---|---|
| `player-smoke.log` | `993fc674f0bcca2694b1752ffe19a796941f2956c40cd7ff8847abc3c9179efd` |
| `player-final.log` | `ce20bdf69df27eac67677339183c636540da6f45431c4a80a74c3de4ba5b6d0f` |
| `shop-recipe.json` | `ed27e95f2d267ea4f7ef4ba8dfea36efeb9acd186f9bf372fb2ff705e66cad95` |
| `identity-comparison.jsonl` | `50ec47b32fc4f42bbcc45e3e2fdca39d1afca5f140f5d457a451d3f39f6ddcef` |
| `failures.json` | `e2b8fd02bac1d4e138888b531c3b1110f93cd2a6974f9ed601c287bc0f88778d` |
| `backup-manifest.json` | `5c64d540fff300d69248db3e43bbdd92279d243af3780c408588f5eaf23d2632` |
| `restore.json` | `222a39f5c934e8d1caa07ed922d736fd1f240a365de5ba0e7be98d20953f74e1` |

`commands.jsonl` holds every staged launcher invocation/exit/time; `*.json` / `*.err` hold replies, with the original three refusals preserved in `failures.json`. Stage-only `manage.py`, `run.py`, `checks.py`, `extract.py` and `tooling/SaveRead` are not committed.

## Cleanup

Both stops were normal `python3 scripts/mac-agent stop`; no force stop. Restored and SHA-256 verified all **260** backed-up files with **zero mismatches**, including isolated ModsLocal, persistent-city.json, preferences, native plist and install receipt. Newly added mod files were removed. Original autosave-key absence and exact plist bytes restored. A second hash check after lease release still matched 260/260.

Final status: `running=false`, lease `held=false`, pending **0**. Game PIDs **49991** and **53151** are absent; no caffeinate assertion targeting either remains. An unrelated caffeinate was left untouched. All foreground build/helper/launcher commands completed. Main mod checkout and official rig checkout are clean. No Windows/agent1, normal/Steam install/profile, OS setting or unrelated process was controlled. No WRAP_UP_NOW marker appeared at checked boundaries.

Report copied to `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-11e-mac.md`. Evidence worktree is detached after PR creation so its branch is available for review. Results PR is docs only and is not merged.

## Per-check results

| Check | Result | Evidence / reason |
|---|---|---|
| `rig70-mac-rig-test` | PASS | Runtime 7; launch accepts guard; suppression true through creation, completed reloads and hour window. |
| `rig70-mac-shop-fixture` | PASS | Stock recipe funds/reads back $100M before native rental; 36 3rd Street rented, equipped, staffed and stocked; checkpoint saved. |
| `fix-reload-accounting-epoch-1` | PASS | Immediate S1 native menu/load at 08:00; city running, settled 49/49, no epoch/clock/startup warnings, suppression verified. |
| `fix-reload-accounting-epoch-2` | PASS | H1 native menu/load at saved minute; settled 49/49; 957/957 and equal resident identities. |
| `x3-1` | PARTIAL | Autosave-off and native schema-1 H1 reload/identity assertions established. Native Loaded/schema log assertion absent; auxiliary H1 reader reports Invalid visitor next event. |
| `f8h-2` Mac first-sale continuation | NOT RUN | PC 11d covers first sale; no duplicate sale window. Earlier F8h result retained. |
