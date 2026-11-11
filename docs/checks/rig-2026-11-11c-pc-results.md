# Rig results — 11 November 2026 c PC (FOREIGNPATCH + RLOTOGGLE + shop-rental retry)

**Summary: 4 passed / 3 partial / 0 failed / 65 not run (72 scoped numbered rows).** Cross-listed slice rows count separately; the explicit exclusions below are outside this count. Counts describe this run; earlier results are not silently carried forward.

Role `rig_test`, HomePCHartwieg, Windows background agent1. Candidate `10b2d7bfab033db9c97cf811a81b940d7a22f9d1`, fetched and verified before build. Schema held at 1. Testing only; no production changes, release, Workshop, version/changelog, issues or merge.

[Combined checklist](rig-2026-11-06-checklist.md). [Combined batch #211](rig-2026-11-11-batch-results.md). [Previous PC #210](rig-2026-11-11b-pc-results.md).

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11c-pc`. Raw replies, logs, binaries, saves and backup bytes remain outside Git. Earlier stages and the rig checkout are read only.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `10b2d7bfab033db9c97cf811a81b940d7a22f9d1`; requested tip unchanged. Detached build worktree `rig-2026-11-11c-pc`. |
| Build | Requested `tools/build.py` succeeded; PATH prefixed with `%LOCALAPPDATA%/Microsoft/dotnet`; wait-other-dotnet script ran first (no-op). No offline dotnet suite. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`. |
| Rig checkout | `19595b849f83c4afe9a328014a8566ca9af482ae`, clean, read only. |
| Installed overlay | Ops ProductVersion `1.0.0+19595b849f83c4afe9a328014a8566ca9af482ae`; Ops SHA-256 `576789cf42a17cb65f9258a8ad5e56367c2f367aadc9fc90a890500d1e440702`; runtime `f35e5848fd8fcec41af32ab7ef455be1c24b7ebe5fa41f5e17a21795b978043e`; Input `6739396f4a782f955826a629ffbe3672c7e6f5348d5229e1077e5357bcc0276b`. No overlay installation/build/update. |
| Backup | Current post-install 301-entry SHA-256 verified backup plus exact isolated HKCU key; `<stage>/backup-manifest.json`, SHA-256 `d94da116039a5dfbda1eab970fd5595a8a7a316d2fab0e7dd4f63739b4f5fe31`. Rollback revision `20261111T084212647` untouched. |
| Isolation | Fixed agent1 background player, port 18330; stable owner `pc-rig-2026-11-11c`. No foreground, real input, normal profile, normal ModsLocal or normal bridge access. Full receipt-hashed 30-DLL modpack. |
| Installed capabilities | Live `ops` advertises `foreign_patch`, `rlo_option`, `shop_fixture`, `save`, `pcsave`, `pcstate`, `modwork`, `ledger`, `rival_fingerprint` and the baseline ops. See `<stage>/installed-capabilities.json` and `<stage>/runs/foreign2/results/001-installed-ops.json`. |
| Autosave | Original native preference 5 minutes / 3 slots, `prevent_autosave=false`; native rig-test guard reports `enabled=true`, `autosave_disabled=true` after every load. Preferences were retained; suppression was provided by the installed guard. Saves were explicit and outside functional wait intervals. Exact preferences/key restored and verified at cleanup. |

## Runs

| Flow | Outcome | Cleanup |
|---|---|---|
| `creation` | PASS; raw evidence `<stage>/runs/creation` | All recorded actions succeeded. |
| `foreign` | INFRA; raw evidence `<stage>/runs/foreign` | Retained cleanup refusals; see explanation below. Eventual restoration verified separately. |
| `foreign2` | COMPLETE; raw evidence `<stage>/runs/foreign2` | All recorded actions succeeded. |
| `creation2` | PASS; raw evidence `<stage>/runs/creation2` | All recorded actions succeeded. |
| `epoch` | COMPLETE; raw evidence `<stage>/runs/epoch` | All recorded actions succeeded. |
| `epoch4` | COMPLETE; raw evidence `<stage>/runs/epoch4` | All recorded actions succeeded. |
| `shop` | COMPLETE; raw evidence `<stage>/runs/shop` | All recorded actions succeeded. |
| `shopalt` | COMPLETE; raw evidence `<stage>/runs/shopalt` | All recorded actions succeeded. |

## F8h check 1

**PARTIAL; install/refusal/remove/reload assertions verified by rig.** Also covers the same F8a-11 foreign-prefix row. Owned-shop hand-back, lunch service, sale suppression and reacquisition require the shop fixture and are not inferred from capability reports.

On this run's fresh Ready city, `foreign_patch install` added owner `agent-rig.foreign.pc1111c`, kind `prefix`, to `RetailBusinessSimulator.ProcessCustomer(CustomerEntry)`. Its native readback included `AgentRigOps.ForeignPatchWorld.ForeignPrefix`. Reloaded status:

```text
food.checkout.shop optional off: foreign prefix by agent-rig.foreign.pc1111c on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)
```

The hooks entry ended `-> refuses food.checkout.shop`, with one refusing entry; the activation capability line and deciding hook scan named the same refusal (scan 13.002 ms, a provisional startup observation on the shared PC; no measured window or p95 verdict). `foreign_patch remove` succeeded. After reload, status reported checkout active, hooks reported 49/49 targets and 0 refusing, and `ProcessCustomer` had no foreign owner. The final owner list matched the initial owner list. Live PC keys contained no food-sale receipt key. Raw evidence: `<stage>/runs/foreign2/results`, `capability-summary.json`, `foreign-refusal-Player.log`, `foreign-restored-Player.log`.

The first attempt (`<stage>/runs/foreign`) is retained: after a successful install, the stage loader accepted stale `load=done` state and attempted removal during the menu transition. Exact removal error: `ops_city_required`. Cleanup pause was refused at the menu; normal quit succeeded, removing the process-local owner. Mods and lifecycle were restored. The stage loader was corrected to require a new native `Test bridge: load '…' done` log line. The second attempt ran the full cycle; no rig checkout or overlay change.
`foreign2` then advanced naturally from 08:00 to 09:00, paused at the boundary, with checkout active and no accounting refusal. Cleanup stopped only the owned game, restored mods/preferences, and released the lease (all cleanup actions succeeded).


## fix-reload-accounting-epoch checks 3-4

**Check 3 PASS.** `rlo_option` read Social Media live-on, stored absent, `original_saved=false`; set-off and readback confirmed live-off. Reloaded S1 with autosave suppression verified. During 20.203 seconds unpaused, one new warning said `Persistent City startup is still waiting after 15 seconds; native time is released.` Native time advanced 08:00→08:15; frames advanced 7242→7763, and the city continued answering commands. Capability status was `start waiting (rlo.donor transient)` with `transient (Off): Flare is not running`, as expected for this startup case. Evidence: `<stage>/runs/epoch/epoch3-wait20-clock.json`, `epoch3-loaded-Player.log`, `epoch3-complete-Player.log`, results 016–025.

### Check 4: verified late-minute switch

**PASS, verified by rig.** S2 belongs to this run's second fresh native FT character. Its saved success marker is schema 1, minute `1920.7541997432709` (final quarter of 08:00), sequence 2. With Social Media off, S1 was loaded; Social Media was set on and S2 requested **0.406 seconds** after S1's native load completion. The native load path traversed the menu without foreground or real input. S2 reached `city running`, with logical minute exactly `1920.7541997432709`, native minute `1920.7562360167503`, no accounting refusal and zero historical clock misses. Neither `City clock already active` nor `Accounting input epoch was lost` appeared in the captured process log. Autosave suppression was checked after loads. Social Media restore/readback returned stored absent, live true, `original_saved=false`; all cleanup actions succeeded, game stopped normally and lease free. Evidence: `<stage>/runs/epoch4/saved-minute-1.json`, `epoch4-switch.json`, results 026–042 and `epoch4-verified-Player.log`.

The earlier S2 minute `1922.0277554988861` and `start ready` observation remain PARTIAL evidence in `<stage>/runs/epoch`; the late-minute verdict uses only the completed `epoch4` observation above.

### F8a-9 / F8bg-8: Social Media switched off mid-session

**PASS, verified by rig.** The 09:00 baseline, 10:00 off-state, 11:00 on-state and fresh-process post-reload live checkpoint all contain 957 residents and 957 visitors. Canonical SHA-256 of all resident identity fields (`Age`, `AppearanceSeed`, `District`, `Female`, `Id`, `Name`, `Persona`) is identical: `fdd762cbaf0cb0f162f79cef4db4b79da356a0d16d1ef458ec0f7978c247e9ec`. Saved minutes are 1980 / 2040 / 2100 / 2100; success-epoch sequences 2 / 3 / 4 / 5 have valid payload lengths and schema 1, with no accounting-stop key. The final checkpoint was newly saved after `city running` in a fresh process, not re-extracted from the pre-reload file. `<stage>/identity-summary.json` and its four source extracts/expansions retain the projection and hashes.

Off-state status reported `city running` and `rlo.donor required off: transient (Off)` with `The city continues and saves on its saved donor catalogue.` Reservations, road speeds and physical capabilities were active. During the off hour, PC's own population line at logical minute 2017.5 reported physical=true, 48 pedestrians, 6 cars, 132 acquired and 78 released. Native walker snapshots had 53 / 55 walkers, 39 shared observation handles and 37 with changed world position. Logical identity equality uses the saved person fields; native handles remain observation handles. `<stage>/runs/epoch/toggle-off-Player.log:7710`, results 045–057 and `walker-summary.json`.

Native reload reached city running, and Social Media's installed restore returned live-on, stored-absent, `original_saved=false`. The strict host semantic reader's visitor timing refusals remain recorded; identity comparison uses the candidate's lossless expansion, not that host reader's acceptance verdict.


## Shop-rental results

**No owned staffed food shop established; shop-dependent checks NOT RUN.** One attempt used the requested `ba:street_fourthavenue#18`; one permitted alternative used the checked-in historical fixture address `ba:street_thirdstreet#32` (`docs/reviews/live/20261102-live4/native/shop-build.json`, repeated in live-6 records). Both used `acquire: rent`, with native vacancy/suitability/funds gates and the documented coffee-shop equipment/staff/stock spec. No further address or rental workaround was attempted.

| Flow / address | Exact build error | Native readback | Follow-up status | Money before / after |
|---|---|---|---|---|
| `shop`, 18 Fourth Avenue | `ops_shop_vacant_suitable_required` | Existing Pump Up gym, 75 m²; not player-rented/owned | `ops_fixture_unknown` | 10000.0 / 10000.0 |
| `shopalt`, 32 Third Street | `ops_shop_vacant_suitable_required` | Existing The Grocery Cart supermarket, 75 m²; not player-rented/owned | `ops_fixture_unknown` | 10000.0 / 10000.0 |

Both refusals occurred before a fixture job or acquisition step existed. These are current-fixture vacancy gaps. **Native rental was not reached**, so the previous `ops_shop_native_rental_refused` defect and absent agent-rig #71 fix were not retested. The existing businesses were left untouched; no campaign, money injection, purchase offer, rival-finance write or native-field workaround was used. `<stage>/runs/shop` and `shopalt`, results 007–010, retain exact requests/errors/state and the money comparison.

F8h-2's missing first-sale half, F8h-1/F8a-11 shop service/receipt/ownership halves, native-shop halves F8a-5/F8bg-4, the named E5b shop rows, F9 owned-shop/f9-city-ready rows, F6b staffed/rush/config variants and F7-1/3 remain NOT RUN. Existing `save`, `pcsave`, `pcstate`, `modwork`, `ledger` and `rival_fingerprint` were not mislabeled as missing. Both shop lifecycles stopped normally, restored their named state and released the lease.


## Findings

1. The foreign-prefix refusal and its removal/reload recovery are verified by rig. Shop ownership and sales remain separate unverified assertions. The first stage timing failure remains in the runs and tooling gaps.
2. Startup with Flare off released native time after the documented warning. The saved-minute switch and related mid-session verdicts are described above; no early-minute checkpoint is substituted for the late-minute setup.
3. Shop-dependent checks remain NOT RUN after vacancy preflight refused both named addresses; native rental was never reached, so this run supplies no new verdict on the earlier `ops_shop_native_rental_refused` defect or agent-rig #71. No money injection, campaign purchase, rival-finance write, or rental bypass was used.
4. Payload identity assertions use the lossless `SaveCodec.ExpandCompact` projection and canonical JSON, with native activation authoritative for loading. The copied stage decoder also tries the strict host reader; its `Invalid state: Invalid visitor next event.` and `Invalid state: Invalid visitor departure.` outputs are retained. This host-reader mismatch, also seen in #210, is not counted as a native refusal or semantic equality proof. No offline dotnet test suite ran.
5. No performance window was measured. 17 emitted PC component observations were >=30 ms. These startup/reload observations are provisional (PC shared), with no p95 or complete-frame coverage claim.

### Observed component timings

Every emitted observation >=30 ms is listed below. Raw logs are `<stage>/runs/<flow>/Player.log`; the compact inventory is `<stage>/component-observations.json`, SHA-256 `98e3bf8aa532fd81e47b03eaf6baf3333e517e45c6d12e9ebbfed6a0881ddb72`. Whole-frame timing was not measured.

- `creation` line 1624, frame 5941, preparation_ms: **182.304 ms**.
- `foreign` line 1560, frame 3879, preparation_ms: **183.521 ms**.
- `foreign2` line 2712, frame 4091, preparation_ms: **198.840 ms**.
- `foreign2` line 3888, frame 4944, preparation_ms: **186.615 ms**.
- `creation2` line 1623, frame 3986, preparation_ms: **183.687 ms**.
- `epoch` line 1600, frame 3258, preparation_ms: **177.892 ms**.
- `epoch` line 1743, frame 5361, preparation_ms: **43.037 ms**.
- `epoch` line 1756, frame 5444, closure_ms: **38.942 ms**.
- `epoch` line 1807, advancement_max_in_diagnostic_interval: **31.350 ms**.
- `epoch` line 2927, frame 6443, preparation_ms: **198.317 ms**.
- `epoch` line 7402, frame 9627, preparation_ms: **152.400 ms**.
- `epoch` line 8986, frame 14358, preparation_ms: **175.277 ms**.
- `epoch4` line 1753, frame 3922, preparation_ms: **182.963 ms**.
- `epoch4` line 2866, frame 4582, preparation_ms: **184.571 ms**.
- `epoch4` line 5373, frame 7200, preparation_ms: **127.361 ms**.
- `shop` line 1629, frame 4387, preparation_ms: **197.512 ms**.
- `shopalt` line 1553, frame 1940, preparation_ms: **109.991 ms**.

## NOT RUN grouped by missing capability or fixture

| Missing prerequisite | Checks / assertions |
|---|---|
| Owned staffed food shop / native rental | F8h-1 and F8a-11 ownership/native-service/receipt halves; F8h-2 first sale; native-shop halves F8a-5/F8bg-4; E5b-complete-menu and E5b2–5 shop rows; F7-1/3. |
| FT-F6b-RivalLimits staffed/rush fixture and config variants | F6b-1..8. ledger, pcstate and rival_fingerprint are installed; their presence supplies neither this fixture nor its control variants. |
| f9-city-ready / FT-F9, RIG-ACCEPT and paired controls | F9a/bdf/c/e/g/h numbered rows below. No official manifest was sealed. Missing PhysicalEnabled/config readback, vendor/dialog or paired base conditions remain prerequisites where their check text requires them. |
| RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY | NOT RUN: any reserved present/foreground/priority flow; official F4 G1/G3/G4/G5/G6/G7/G8/G11/G13 and G2 presentation-trace half. These fail validation before lease acquisition; no foreground/input fallback. |
| pc-stress-10k@1 / ft-f4-stress10k / city-50k@1 | NOT RUN: F4 G15, F7-6, 10k/50k half of F7-7; fixtures BLOCKED. |
| RIG-FAULT | NOT RUN: F8a-6..8, F8bg-5..7 and F7-4; no fault build/injection. |
| RIG-PCFIX / visitor-body / historical-schema fixtures | NOT RUN: F5i visitor fixtures, F7-5 and older/future-schema rows. Only fresh schema-one FT cities were loaded; older development cities require start a new city. |
| Independent holder/notification/authority traces / Street Food absent transport / vendor API | Additional prerequisites of the named E5b and F9 rows remain missing; catalogue presence or generic reads are not substitutes. |
| Sealed FT-F4-Declared/FT-F9 / reserved caps / unbuilt F7e | NOT RUN: official F4f verdict and F7-8; no F4f/F7e implementation, no manifest sealing. |

F7-2 / F4 G12, F7-7 and F7-9 from #211 were not repeated. Reload-epoch checks 1–2 were not re-run as separate checks. These are scope exclusions, not new verdicts.

## Tooling gaps

| Capability / action | Exact error | Flow | Blocked checks / disposition |
|---|---|---|---|
| `foreign_patch remove` | `ops_city_required` | `foreign`; `runs/foreign/results/009-foreign-remove.json` | First foreign cycle interrupted by stale load state; corrected full cycle succeeded. No remaining core refusal/recovery check blocked. |
| `shop_fixture build` | `ops_shop_vacant_suitable_required` | `shop`; `runs/shop/results/007-shop-rental-once.json` | Owned-shop checks and shop-specific assertion halves blocked at vacancy preflight; native rental not entered. |
| `shop_fixture status` | `ops_fixture_unknown` | `shop`; `runs/shop/results/008-shop-final-status.json` | No job after refused start; secondary status readback, no additional check blocked. |
| `shop_fixture build` | `ops_shop_vacant_suitable_required` | `shopalt`; `runs/shopalt/results/007-shop-rental-once.json` | Owned-shop checks and shop-specific assertion halves blocked at vacancy preflight; native rental not entered. |
| `shop_fixture status` | `ops_fixture_unknown` | `shopalt`; `runs/shopalt/results/008-shop-final-status.json` | No job after refused start; secondary status readback, no additional check blocked. |

The first S2 pause produced an early-minute checkpoint (`1922.0277554988861`); that setup shortfall and the verified replacement are retained above. Full scrubbed op replies, including native fixture diagnostics, remain in `<stage>/tooling-gaps.json` and each flow result.

## Fixtures

Only the two fresh native characters produced at this candidate were loaded. All created fixture saves remain in place. Files below are in the isolated agent1 profile's save folder, one subfolder per character. Absolute paths and the full file/companion inventory are in `<stage>/fixtures.json`; SHA-256 was re-read after stop. All ten save hashes still match their creation/manual-save records (`<stage>/fixture-hash-verification.json`).

| Name | File | SHA-256 |
|---|---|---|
| `FT E5b PC 11 Nov c 10b2d7bf Ready` | character 1 / `FT E5b PC 11 Nov c 10b2d7bf Ready.hsg` | `ec6157fe1173b1264c30e4d99f3a957aebae3529a8f8ac051f312fba9d0bba29` |
| `FT E5b PC 11 Nov c 10b2d7bf` | character 1 / `FT E5b PC 11 Nov c 10b2d7bf.hsg` | `afede46380a1b0ed15f57ca33aecd3557c3d63cbdbacd18314f4b4260d5d8714` |
| `FT PC 11 Nov c epoch Toggle Baseline` | character 1 / `FT PC 11 Nov c epoch Toggle Baseline.hsg` | `94294ccd30ee3d10535bc6c24fc56c7ec9a67588dd5bceb0425ffd75c737f9e5` |
| `FT PC 11 Nov c epoch Toggle Off` | character 1 / `FT PC 11 Nov c epoch Toggle Off.hsg` | `4f4c96407205b0ceca70b8a194c1f9fde21f520122bddad65a4e4fd4136662bd` |
| `FT PC 11 Nov c epoch Toggle On` | character 1 / `FT PC 11 Nov c epoch Toggle On.hsg` | `436bf5017275bf8fcd4e9a69406aa38fb666f89fe7950fbec9ba30e3f80d790d` |
| `FT PC 11 Nov c epoch4 Toggle Reload Verified` | character 1 / `FT PC 11 Nov c epoch4 Toggle Reload Verified.hsg` | `7b492348206d4ccf3fbf6c4a253afb275b71f26ddc4cf6c54b3f86a84f5a7519` |
| `FT E5b PC 11 Nov c S2 10b2d7bf Ready` | character 2 (S2) / `FT E5b PC 11 Nov c S2 10b2d7bf Ready.hsg` | `ca1eb123fbbf59907f4a529d633431a155e77e6734fbbca4cb4e40a0d3d0710e` |
| `FT E5b PC 11 Nov c S2 10b2d7bf` | character 2 (S2) / `FT E5b PC 11 Nov c S2 10b2d7bf.hsg` | `5ea11d3ece175561d750f64f6e470e6a04c120eed3a88212de6de6662ea9e559` |
| `FT PC 11 Nov c epoch S2 Late` | character 2 (S2) / `FT PC 11 Nov c epoch S2 Late.hsg` | `25f7eabdd162ff4b7327624ad37f7eb92053b0dd17e4090c8a3219dc7f2e5f3f` |
| `FT PC 11 Nov c epoch4 S2 Late Verified 1` | character 2 (S2) / `FT PC 11 Nov c epoch4 S2 Late Verified 1.hsg` | `16ef92d6e1a151e1ee4097babaa8fedbeaa1fac8f01eccf049e8952b39d4ad82` |

## Per-check results

Historical slice text was read from `docs-history-2026-11-10` where retired; current F8h, reload-epoch, F8bg and F9bdf/g/h text was read at the candidate. No historical writer number overrides schema 1.

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b-complete-menu-2` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b-complete-menu-3` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b-complete-menu-4` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b-complete-menu-5` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b-complete-menu-6` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b2-3` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b2-4` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b3-2` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b3-3` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b3-4` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b3-5` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b3-6` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b4-4` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b4-5` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b4-6` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b5-2` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b5-3` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b5-4` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b5-5` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `e5b5-6` | NOT RUN | Needs an owned staffed food shop; both named addresses refused vacancy preflight. Cases also requiring independent holder/notification/authority traces or Street Food absent transport retain those prerequisites. |
| `f6b-1` | NOT RUN | NOT RUN: Needs FT-F6b-RivalLimits lunch-rush checkpoint and lane/stock limit variants; no owned staffed food shop established. |
| `f6b-2` | NOT RUN | NOT RUN: Needs representative owned staffed/rush food shops and native throughput observations; no staffed shop established. |
| `f6b-3` | NOT RUN | NOT RUN: Needs captured rival-decision/estimate-label fixture in the shop phase; not established. pcstate is installed. |
| `f6b-4` | NOT RUN | NOT RUN: Needs matched rival-full/control checkpoints and an owned player food shop; no matched fixture established. |
| `f6b-5` | NOT RUN | NOT RUN: Needs before/after rival fingerprints around F6b-1/4 experiments; those experiments did not run. rival_fingerprint is installed. |
| `f6b-6` | NOT RUN | NOT RUN: Needs actual queued/serving rival meals and a mid-queue/service reload checkpoint; not established. |
| `f6b-7` | NOT RUN | NOT RUN: Needs F6b-6 checkpoint and documented capacity-profile config variants; neither was established. |
| `f6b-8` | NOT RUN | NOT RUN: Needs a staffed lunch-rush fixture/control window; no shop established and no performance window measured. |
| `f9a-1` | NOT RUN | NOT RUN: needs RIG-ACCEPT and the same-head offline full acceptance/provenance comparison. RIG-MODSET/DLL hashes were recorded; they do not complete the formal F9 provenance row. |
| `f9a-2` | NOT RUN | NOT RUN: both documented addresses failed vacancy preflight; no owned staffed food shop, day-3 FT-F9 or f9-city-ready checkpoint was established. No manifest sealing attempted. |
| `f9a-3` | NOT RUN | NOT RUN: needs f9-city-ready and RIG-ACCEPT. F8 capability readbacks are retained, but the required F9 fixture was not established. |
| `f9a-4` | NOT RUN | NOT RUN: needs f9-city-ready with an open owned shop and paired seven-day daily-reload/uninterrupted copies. Installed pcsave/ledger do not supply that missing fixture/control. |
| `f9a-5` | NOT RUN | NOT RUN: needs f9-city-ready and matched cadence/skip arms. Visible frame-cap/presentation conditions were not run under the background-only constraint. |
| `f9a-6` | NOT RUN | NOT RUN: needs f9-city-ready with followers mid-leg and a supported PhysicalEnabled=false config arm. No owned-shop/in-flight fixture or bodies-off arm established. |
| `f9a-7` | NOT RUN | NOT RUN: needs f9-city-ready, agency-dialog/crew-truck fixture and working Street Food vendor API. Live capability status says Street Food vendor API missing; no campaign was bought. |
| `f9a-8` | NOT RUN | NOT RUN: needs historical-schema FT loader/fixture. Only fresh schema-one cities are allowed; older development cities require start a new city. |
| `f9a-9` | NOT RUN | NOT RUN: needs paired f9-city-ready owned shops and two days of raised/control prices with loaded price-weight readback. No shop established; no price experiment performed. |
| `f9a-10` | NOT RUN | NOT RUN: needs RIG-ACCEPT, same-head full acceptance output, required native check records and the official F4 performance record. Those prerequisites are incomplete; no summary verdict synthesized. |
| `f9bdf-1` | NOT RUN | NOT RUN: needs RIG-ACCEPT and the same-head offline full acceptance/provenance comparison. RIG-MODSET/DLL hashes were recorded; they do not complete the formal F9 provenance row. |
| `f9bdf-2` | NOT RUN | NOT RUN: needs f9-city-ready with followers mid-leg and a supported PhysicalEnabled=false config arm. No owned-shop/in-flight fixture or bodies-off arm established. |
| `f9bdf-3` | NOT RUN | NOT RUN: needs RIG-ACCEPT, same-head full acceptance output, required native check records and the official F4 performance record. Those prerequisites are incomplete; no summary verdict synthesized. |
| `f9bdf-4` | NOT RUN | NOT RUN: needs f9-city-ready and RIG-ACCEPT. F8 capability readbacks are retained, but the required F9 fixture was not established. |
| `f9c-1` | NOT RUN | NOT RUN: both documented addresses failed vacancy preflight; no owned staffed food shop, day-3 FT-F9 or f9-city-ready checkpoint was established. No manifest sealing attempted. |
| `f9c-2` | NOT RUN | NOT RUN: needs f9-city-ready with an open owned shop and paired seven-day daily-reload/uninterrupted copies. Installed pcsave/ledger do not supply that missing fixture/control. |
| `f9c-3` | NOT RUN | NOT RUN: needs f9-city-ready and matched cadence/skip arms. Visible frame-cap/presentation conditions were not run under the background-only constraint. |
| `f9c-4` | NOT RUN | NOT RUN: needs f9-city-ready with followers mid-leg and a supported PhysicalEnabled=false config arm. No owned-shop/in-flight fixture or bodies-off arm established. |
| `f9c-5` | NOT RUN | NOT RUN: needs f9-city-ready, agency-dialog/crew-truck fixture and working Street Food vendor API. Live capability status says Street Food vendor API missing; no campaign was bought. |
| `f9c-6` | NOT RUN | NOT RUN: needs historical-schema FT loader/fixture. Only fresh schema-one cities are allowed; older development cities require start a new city. |
| `f9e-1` | NOT RUN | NOT RUN: needs f9-city-ready and RIG-ACCEPT. F8 capability readbacks are retained, but the required F9 fixture was not established. |
| `f9e-2` | NOT RUN | NOT RUN: needs paired head/base f9-city-ready copies for diagnostic equivalence. No base/control fixture or owned shop established. |
| `f9e-3` | NOT RUN | NOT RUN: needs f9-city-ready and paired base/head panel/inspector observations. No corresponding owned-shop/control fixture; no foreground/real-input fallback. |
| `f9e-4` | NOT RUN | NOT RUN: needs f9-city-ready and paired base/head panel/inspector observations. No corresponding owned-shop/control fixture; no foreground/real-input fallback. |
| `f9e-5` | NOT RUN | NOT RUN: needs f9-city-ready census/save-cost status after its save. The required shop checkpoint was not established; no separate save-profiling run. |
| `f9e-6` | NOT RUN | NOT RUN: needs the F9 fixture and paired observer/presentation performance windows. Reserved presentation/foreground/priority steps were not used; no performance window measured. |
| `f9g-1` | NOT RUN | NOT RUN: needs f9-city-ready with followers mid-leg and a supported PhysicalEnabled=false config arm. No owned-shop/in-flight fixture or bodies-off arm established. |
| `f9g-2` | NOT RUN | NOT RUN: needs paired f9-city-ready head/base body-selection fixtures and resident/body readback. Generic native walker handles are not that binding proof. |
| `f9g-3` | NOT RUN | NOT RUN: needs owned-shop opening queues plus a published vendor stand/queue fixture. Shop setup failed and Street Food vendor API is missing. |
| `f9g-4` | NOT RUN | NOT RUN: needs the F9 fixture and paired observer/presentation performance windows. Reserved presentation/foreground/priority steps were not used; no performance window measured. |
| `f9h-1` | NOT RUN | NOT RUN: needs RIG-ACCEPT and the same-head offline full acceptance/provenance comparison. RIG-MODSET/DLL hashes were recorded; they do not complete the formal F9 provenance row. |
| `f9h-2` | NOT RUN | NOT RUN: needs paired f9-city-ready owned shops and two days of raised/control prices with loaded price-weight readback. No shop established; no price experiment performed. |
| `f9h-3` | NOT RUN | NOT RUN: needs RIG-ACCEPT, same-head full acceptance output, required native check records and the official F4 performance record. Those prerequisites are incomplete; no summary verdict synthesized. |
| `f7-1` | NOT RUN | Needs owned staffed food shop and natural purchases; check 3 additionally needs FT F7 food shop with actual receipt_pending activity. Both named addresses refused vacancy preflight. |
| `f7-3` | NOT RUN | Needs owned staffed food shop and natural purchases; check 3 additionally needs FT F7 food shop with actual receipt_pending activity. Both named addresses refused vacancy preflight. |
| `f8h-1` | PARTIAL | Install/refusal/hooks/activation/remove/reload and next accounting boundary verified by rig. Shop hand-back/service/receipt/reacquisition assertions not run: both named addresses refused vacancy preflight. |
| `f8h-2` | PARTIAL | Normal reload: checkout active, 49/49 targets, zero refusing entries. Missing first-sale proof not run: both named addresses refused vacancy preflight. |
| `f8a-11` | PARTIAL | Same foreign-prefix cycle as F8h-1; native ownership/service half not run: both named addresses refused vacancy preflight. |
| `f8a-5 native-shop half` | NOT RUN | Needs an owned staffed shop to observe native service; both named addresses refused vacancy preflight. Earlier non-shop evidence is unchanged. |
| `f8bg-4 native-shop half` | NOT RUN | Needs an owned staffed shop to observe native service; both named addresses refused vacancy preflight. Earlier non-shop evidence is unchanged. |
| `fix-reload-accounting-epoch-3` | PASS | One 15-second release warning during 20.203 s unpaused; native clock 08:00 to 08:15 and responsive commands. RLO option restore verified. |
| `fix-reload-accounting-epoch-4` | PASS | Verified saved minute 1920.7541997432709; S2 requested after 0.406 s; city running at exact saved logical minute, no clock/epoch refusal. Option restore verified. |
| `f8a-9` | PASS | Off/on hours and successful saves; 957 complete resident identities equal across baseline/off/on and newly saved fresh-process reload; city running. |
| `f8bg-8` | PASS | Same identity/save/reload sequence; transient Off reported, reservations/roadspeeds active; off-hour PC physical counts 48 pedestrians/6 cars and native walker movement captured. |

## Cleanup

Restored this run's current post-install backup, with 301 protected entries matching SHA-256/absence and the exact isolated HKCU key verified. ModsLocal, install receipt/managed overlay, launcher/run state, preferences, persistent-city.json and autosave state match the backup. The rollback revision was not used. All owned game processes exited normally; final agent1 stopped, lease free, pending=0. No process this run started remains running. Evidence: `<stage>/restore-verification.json`, `final-verification.json`, `owned-process-verification.json`. Fresh fixture saves and their test evidence remain.
