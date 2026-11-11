# Rig results — 11 November 2026, Mac (reload accounting-epoch fix + re-run rows)

Role `rig_test`, where `mac`, instance `agentmac`. Tested `eaac521cdbe3cf46e9fe6451d358d993966cc72c`. `git fetch origin` and `git rev-parse origin/codex/persistent-city` matched the requested candidate before building; the tip had not moved. In-game proof gates no merge. Testing evidence only; no production change, release or merge.

**Summary: 2 passed / 6 partial / 0 failed / 150 not run.** Counts cover the previous Mac-b report’s 154 non-PASS rows plus the four reload accounting-epoch checks. Previously passed creation-save-init check 1 and other prior PASS rows are not recounted. Alias rows retain separate counts.

Format follows [the 10 November Mac report](rig-2026-11-10-mac-results.md). Re-run catalogue and prior failures were read from [PR #203’s branch, read-only](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/mac-rig-2026-11-10b/docs/checks/rig-2026-11-10b-mac-results.md). That branch and its stage were not modified. Historical check links use `docs-history-2026-11-10`; historical writer-number assertions are superseded by held schema 1.

`<stage>` means `~/HART-Builds/persistent-city-rig-2026-11-11-mac`. Raw bridge replies, player logs, payload extracts, SHA-256 backup manifest, per-file assembly/mod hashes and test tooling stay there. No raw logs, binaries or saves are committed.

## Build and rig

| Item | Value |
|---|---|
| Host / role | Native Apple Silicon M1 Pro Mac, 16 GB; `rig_test`; loopback port 28330, no tunnel. |
| Candidate / worktrees | `eaac521cdbe3cf46e9fe6451d358d993966cc72c`; detached `rig-2026-11-11-mac` build and separate `rig-2026-11-11-mac-evidence` on `codex/mac-rig-2026-11-11`. Main tree left clean. |
| Build | `python3 tools/build.py --game-managed "<private-app>/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; Homebrew PATH and DOTNET_ROOT set for every Python/dotnet/build command. Private Mac Managed references, no import shim. Succeeded. |
| DLL SHA-256 | `6100afdff98873e0c633646617f9de615da747aee16f968e98a7270fcbe73dd1`; installed hash independently matched. |
| Provenance | `unknown` Mac fingerprint, 27 differing assemblies. `<stage>/build-provenance.json`, `installed-dlls.json`. No same-head offline `full` artifact comparison. |
| Official rig | Read-only checkout `cfcc5fde3f652356a5aa3585250f4a92604557fb`; installed runtime 6; native game build 3682. |
| Launcher | Official `scripts/mac-agent` through a stage wrapper changing only LocalControl DLL path. Default DLL missing; copied official LocalControl and BridgeControl sources built into the stage. No official checkout change or system-wide install. |
| Launch / resources | Headless throughout. Initial pre-start 94.0 GiB disk / 77% reported free memory; menu-only second start 93.8 GiB / 67%. Native launcher 30 FPS/two workers/muted configuration. Other harness work untouched. |
| Isolated install | Existing full modpack, 29 DLLs including RLO and Food Trucks. Changed only isolated PC DLL, `Locales/en.json`, `persistent-city.json`, receipt and native preference plist; originals backed up first. No Steam/normal profile or Windows/agent1 access. |
| PC settings | Enabled and PhysicalEnabled true, caps 48 pedestrians / 24 cars, radius 120 m; other compiled defaults. No food shop needed or created. No campaign or money mutation. |
| Autosave | Original native autosave keys absent. Pre-launch isolated plist value 0 did not establish runtime suppression: native UI clamps to at least 1 minute / shows 3 saves, and Recover #0/#1 occurred. Read back after the 08:01 and H1 reloads. No measured performance windows or save profiling. Original preference bytes restored. |
| Ownership | Owner `pc-mac-rig-2026-11-11`; initially free; acquired once, normal stops and release. No force stop, OS settings change or unrelated process control. |
| Offline checks | Candidate and stage-only payload-reader/controller builds succeeded. No production/test-source changes; no full test or regression suite. Auxiliary reader output is separate from native results. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| `mac-creation` | Fresh enabled native new_game | Automatic creation save done; PC running at 08:00. |
| `mac-s1` | Ordinary S1, immediate native menu/load | Expected reload assertions PASS at saved minute 08:00; check 1 PARTIAL for autosave setup. |
| `mac-s1-0801` | Advance to 08:01, ordinary save, menu/load | PC running at saved 08:01 minute; 49/49; no clock refusal or startup warning. |
| `mac-hour-h1` | Reloaded city 08:01 → 09:01, speed 6; H1 save/menu/load | PASS check 2; PC running, 49/49 targets, identity digest equal after H1 reload. |
| `mac-warm` | Initial and 600-frame post-load food capability/coordinator reads at S1-0801 and H1 | PARTIAL E5b3: Active and 34/34 entries, 23/23 hooks; transient Warming unobserved. |
| `mac-rlo-options` | Native city Options tabs/phone, then main-menu Mods route | NOT RUN checks 3/4: no exposed Social Media switch; menu mutation refused by FT-only bridge. |
| `mac-cleanup` | Normal stop, restore/hash check, lease release | PASS cleanup; 260 files verified, zero mismatches; stopped and free. |

## fix-reload-accounting-epoch

**Check 1 PARTIAL / check 2 PASS / checks 3–4 NOT RUN.** [Compact result](fix-reload-accounting-epoch-mac.json). First evidence commit `5c21c3f` recorded the initial reload behavior before further checks; `9815667` completes the priority result and corrects check 1 to PARTIAL after native autosave verification.

1. **PARTIAL — immediate reload.** Both ordinary S1 at 08:00 and the additional S1-0801 at 08:01 menu/load successfully. PC reports `city running`, 49/49 targets patched, and activates at the saved minute. Across the positive-run log there are zero `Accounting input epoch was lost`, `start refused (clock.city)`, `startup is still waiting` or `City clock already active` messages. Expected reload assertions passed; the documented autosave-off setup was not established. Evidence: `s1-status.json`, `s1-hooks.json`, `s1-0801-status.json`, `s1-0801-hooks.json`, `player-s1-reload.log`, `player-s1-0801-reload.log` and health captures in the stage.
2. **PASS — one hour later.** From reloaded S1-0801, time advances normally at speed 6 from 08:01 to 09:01. H1 is saved with epoch minute `1981.0111902952194`, menu/loaded, and PC is running at 09:01 with 49/49 targets. H1 and H1-Reload have 957 residents / 957 visitors and the same resident identity digest: `a2d963d3f32bb7ae75432f2ec475fcbfd7c5e97529a3201b173c2aaae984f575`. Evidence: `save-h1.json`, `h1-reload-health-start.json`, `h1-reload-status.json`, `h1-reload-hooks.json`, `identity-comparison.jsonl`.
3. **NOT RUN — RIG-RLOTOGGLE.** Tried the documented native mod-option route before skipping: Settings → Options → Mods exposes camera controls; Alcware exposes Retail Expansion/Seasons; listed phone buttons expose no Social Media control. After a normal stop/restart to the menu, Mods is listed but `ui click` refuses with `ui works only in a loaded FT city; use load from the main menu.` No Flare-off startup was established. Evidence: `ui-mods.json`, `ui-alcware.json`, `ui-city2.json`, `ui-main-menu.json`, `rlo-main-menu-attempt.err`. No RLO file or setting changed.
4. **NOT RUN — RIG-RLOTOGGLE.** The same limitation prevents preparing the off/on startup arm and the within-10-seconds switch to S2. No S2 fixture was made and no switch-save assertion is claimed.

### fix-creation-save-init check 2

**PASS.** The ordinary S1 contains schema-1 PC payload, 2,956,413 characters, with success epoch `v1`, sequence 2, schema 1 and matching payload length. Native menu/load attaches and reports city running with 49/49 targets. The additional 08:01 ordinary save also reloads. The native creation callback at this head already has a PC payload, so sequence 2 is expected here; creation-save-init check 1 is not recounted. Strict auxiliary reading of S1 is `Loaded`, schema 1, 957 residents / 957 visitors. No lost accounting-epoch failure was observed.

## Findings

1. **Yesterday’s immediate reload failure was not reproduced.** Saved and activated minutes match at 08:00, 08:01 and 09:01. Check 1 retains its autosave setup qualification.
2. **Autosave suppression is not verified.** Native Options shows a minimum of one minute despite the plist value 0; native recovery writes occurred. No performance verdict is made. The Mac launcher has no documented autosave-guard launch option in this installation.
3. **E5b3 remains PARTIAL.** Both reloads and 600-frame readbacks pass all 34 capability entries, 23 hooks and Active/warming=0; the bridge cannot expose the earlier Warming interval through its loaded-FT console gate.
4. **X3 remains PARTIAL.** Native H1 reload and resident identity equality pass. PC does not emit the literal `Loaded, schema 1` assertion. The strict auxiliary .NET reader reports `Invalid visitor next event` for both H1 payloads while native Mono loads H1 and runs; this is not a native-corruption verdict or full-payload equality claim.
5. **F9 provenance aliases remain PARTIAL.** Private Mac assembly set is unknown; installed candidate hash is correct. No Windows or RIG-ACCEPT proof or same-head offline full artifact comparison.

Mac modpack limitations remain in the raw logs: Street Food vendor API absent, interior view disabled by default, and Bakery/PolyModsHyperreal asset-bundle errors. Headless unsupported-shader messages are expected. No alternate mod or asset was installed.

No measured performance window was run, so there is no Performance table or p95 verdict. Whole-frame CityPerformance/RLO traces are separate from PC mod-work. The following emitted PC routing observations at or above 30 ms are flagged provisionally because concurrent harness work was not excluded; they were not re-measured:

- `player-final.log:1389`, frame 2675: `preparation_ms` 120.904 ms.
- `player-final.log:1395`, frame 2732: `closure_ms` 38.732 ms.
- `player-final.log:1942`, frame 3902: `preparation_ms` 119.355 ms.
- `player-final.log:2980`, frame 8731: `preparation_ms` 119.616 ms.
- `player-final.log:3948`, frame 11838: `preparation_ms` 117.691 ms.
- `player-final.log:3957`, frame 11930: `closure_ms` 79.215 ms.

Aggregate diagnostic maxima at or above 30 ms (not extra enumerated samples): scene search 117.065 ms, advance 63.437 ms, advance unit 63.436 ms, closure refresh 79.215 ms, route slice 63.427 ms. Raw summaries and `<stage>/observed-spikes.json` / `aggregate-maxima.json` retain the bounds; samples not emitted by the game cannot be enumerated.

## NOT RUN grouped by missing capability, fixture, or needs Windows client / PC

The installed Mac runtime remains version 6 with the FT bridge. Official rig source and installation were not upgraded; Windows-gated rig-ops/flow extensions were not assumed present because their source exists. All 148 prior NOT RUN prerequisites were checked against the documented current Mac route; none became runnable with this installation. No row was skipped merely for elapsed time or a stopped PC city.

**RIG-RLOTOGGLE — 2 rows.** `fix-reload-accounting-epoch-3`, `fix-reload-accounting-epoch-4`.

**RIG-FOODSHOP / Mac shop operations — 14 rows.** `e5b2-2`, `e5b2-3`, `e5b2-4`, `e5b3-2`, `e5b3-3`, `e5b3-4`, `e5b3-5`, `e5b3-6`, `e5b4-4`, `e5b5-2`, `e5b5-3`, `e5b5-4`, `e5b5-5`, `e5b5-6`.

**RIG-PCSAVE / schema fixtures — 3 rows.** `e5b4-1`, `e5b4-2`, `e5b4-3`.

**needs Windows client / PC — 24 rows.** `e5b4-5`, `e5b4-6`, `f2g-1`, `f2g-2`, `f2g-3`, `f2g-4`, `f2g-5`, `f2g-6`, `f2g-7`, `f2g-8`, `f2g-9`, `f2g-10`, `f2g-11`, `f5i-1`, `f5i-2`, `f5i-3`, `f5i-4`, `f5i-5`, `f8a-4`, `f8a-5`, `f8a-10`, `f8bg-3`, `f8bg-4`, `f8bg-9`.

**Street Food API / FT-E6-Vendors — 7 rows.** `e6f-1`, `e6f-2`, `e6f-3`, `e6f-4`, `e6f-5`, `e6f-6`, `e6f-7`.

**Mac view / checkpoint / comparison capabilities — 14 rows.** `f2c-1`, `f2c-2`, `f2c-3`, `f2c-4`, `f2c-5`, `f2d-1`, `f2d-2`, `f2d-3`, `f2d-4`, `f2d-5`, `f2d-6`, `f2d-7`, `f2e-1`, `f2e-2`.

**FT-F4 / pending performance capabilities — 43 rows.** `f4a-1`, `f4a-2`, `f4a-3`, `f4a-4`, `f4a-5`, `f4a-6`, `f4a-7`, `f4a-8`, `f4b-1`, `f4b-2`, `f4b-3`, `f4b-4`, `f4b-5`, `f4b-6`, `f4b-7`, `f4b-8`, `f4c-1`, `f4c-2`, `f4c-3`, `f4c-4`, `f4c-5`, `f4d-1`, `f4d-2`, `f4d-3`, `f4d-4`, `f4d-5`, `f4d-6`, `f4d-7`, `f4d-8`, `f4d-9`, `f4d-10`, `f4d-11`, `f4d-12`, `f4d-13`, `f4d-14`, `f4d-15`, `f4e-1`, `f4e-2`, `f4e-3`, `f4e-4`, `f4e-5`, `f8a-12`, `f8bg-10`.

**playtest — 2 rows.** `f5i-6`, `f9e-4`.

**F8h / RIG-HARMONYOWNERS — 1 rows.** `f8a-1`.

**RIG-PCSAVE / independent transport — 2 rows.** `f8a-3`, `f8bg-2`.

**RIG-FAULT / RIG-PCSAVE — 6 rows.** `f8a-6`, `f8a-7`, `f8a-8`, `f8bg-5`, `f8bg-6`, `f8bg-7`.

**RIG-RLOTOGGLE / RIG-PCSAVE — 2 rows.** `f8a-9`, `f8bg-8`.

**F8h / RIG-FOREIGNPATCH — 1 rows.** `f8a-11`.

**FT-F9 / RIG-ACCEPT — 29 rows.** `f9a-2`, `f9a-3`, `f9a-4`, `f9a-5`, `f9a-6`, `f9a-7`, `f9a-8`, `f9a-9`, `f9a-10`, `f9bdf-2`, `f9bdf-3`, `f9bdf-4`, `f9c-1`, `f9c-2`, `f9c-3`, `f9c-4`, `f9c-5`, `f9c-6`, `f9e-1`, `f9e-2`, `f9e-3`, `f9e-5`, `f9e-6`, `f9g-1`, `f9g-2`, `f9g-3`, `f9g-4`, `f9h-2`, `f9h-3`.

Explicit scope exclusions remain F7e, F8h itself and F8h check 1, FT-F4 / FT-F9 fixtures, pending RIG-PRESENT, RIG-PERF-UNION, RIG-FOREGROUND, RIG-PRIORITY, RIG-F4-PRODUCER, RIG-SHOPFIX, RIG-ACCEPT, RIG-FAULT, RIG-RLOTOGGLE and RIG-PCSAVE. F7e/F8h add no separate catalogue row here. Pointer/hover/focus/click assertions are NOT RUN (playtest). Read-only target-count diagnostics used by the fix are not a re-run of F8h’s owner inventory check. Windows clients, Windows input/player, `C:\BigAmbitions-AgentTests`, scripts/agent and Agent-Windows.ps1 were not used.

## Fixtures

One fresh disposable FT character created by native new_game at this candidate in the isolated profile, PC enabled throughout. No older fixture used for a positive check. No shop fixture was needed. All checkpoints below are retained in place; no raw saves enter Git.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT-PC-Mac-20261111-eaac521c-H1-Reload` | `dc81cef472eecbbc5f797498bf50fdaa270058107b3fb3e69f394fe05a8d9277` | 2612308 |
| `FT-PC-Mac-20261111-eaac521c-H1` | `6b0bf4df430504f34cf5751cf991b2dbf24a0ad38c57981fa763eaa149a5a489` | 2540832 |
| `FT-PC-Mac-20261111-eaac521c-S1-0801` | `85ae7f7c56af46806c778e726c1919166c68a2ba7786253584a3ffa1d3659b56` | 1670619 |
| `FT-PC-Mac-20261111-eaac521c-S1` | `91bf641677d620fe57791dca3b8babd0aa8ac2736fd45a15b83b9248cbeef065` | 1207276 |
| `FT-PC-Mac-20261111-eaac521c` | `3c09e8e16085f073686772610cb6633dddc6add9eb6ea52903dadc98b37356df` | 673668 |
| `FT-PC-Mac-20261111-eaac521c / Recover #0` | `34305271ea496171b7b1ccc58b1357b92f9d993e3dc0e5463c2b01574f5b13f3` | 7371 |
| `FT-PC-Mac-20261111-eaac521c / Recover #1` | `e9eb1dc3d27237772d5a8045858e225e25122796eb48734576c2e5afb1c23657` | 502125 |

## Cleanup

Both stops were normal `mac-agent stop`; no force stop. Restored and SHA-256 verified all **260** backed-up files, including isolated ModsLocal, PC config, receipt, profile top-level preferences and native preference plist. Newly added mod files were removed. Original autosave key absence and preference bytes restored. `<stage>/restore.json` has zero mismatches. The seven disposable fixture/recovery saves remain.

Final `status-final.json`: `running=false`, lease `held=false`, pending 0. All foreground runner/controller/helper commands finished; no game or caffeinate process from this run remains. The shared compiler used for stage builds had exited. Official rig checkout and main mod tree remain clean. No normal profile, Windows PC/agent1 or unrelated process was controlled. No WRAP_UP_NOW marker was present at the checked restore boundary.

## Per-check results grouped by slice

PASS means the numbered expected assertions passed with the stated setup. PARTIAL identifies a missing assertion or setup condition; FAIL means an observed failed expectation. Previously passed rows are not re-counted.

### [fix-reload-accounting-epoch](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/fix-reload-accounting-epoch.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `fix-reload-accounting-epoch-1` | PARTIAL | Expected reload assertions PASS at 08:00 and 08:01: city running, 49/49 targets, activated at saved minute, no lost epoch, clock.city refusal or startup-wait warning. Native autosave-off setup not established. |
| `fix-reload-accounting-epoch-2` | PASS | Reloaded 08:01 city advanced one hour to 09:01, H1 saved and menu/loaded at 09:01; city running with 49/49 targets. H1/H1-Reload resident identity digests equal. |
| `fix-reload-accounting-epoch-3` | NOT RUN | Tried the native mod-option route: city Options > Mods/Alcware exposes no RLO Social Media control; main-menu Mods click refused by the FT-only bridge. RIG-RLOTOGGLE unavailable on this Mac; Flare-off arm not established. |
| `fix-reload-accounting-epoch-4` | NOT RUN | Same mod-option/FT-only UI restriction as check 3; Flare-off startup and timed off/on switch unavailable. No S2 switch-save fixture was produced. |

### [fix-creation-save-init](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/fix-creation-save-init.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `fix-creation-save-init-2` | PASS | Ordinary S1 contains schema-1 PC payload and a v1 success epoch (sequence 2); native menu/load completes with city running and 49/49 targets. Also repeated at 08:01. |

### [e5b2](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b2.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b2-2` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-3` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-4` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |

### [e5b3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b3-1` | PARTIAL | S1-0801 and H1 reload reads, then 600 further frames each: 34/34 capability entries passed (including cargo reader/self-test), 23/23 hooks, Active and warming=0. Router generations 5 → 7. Transient Warming is already over at bridge load completion. |
| `e5b3-2` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b3-3` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b3-4` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b3-5` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b3-6` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |

### [e5b4](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b4.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b4-1` | NOT RUN | RIG-PCSAVE read/write unavailable on Mac; historical 35/36/37 assertions superseded by schema 1. Older/future fixture construction excluded. |
| `e5b4-2` | NOT RUN | RIG-PCSAVE read/write unavailable on Mac; historical 35/36/37 assertions superseded by schema 1. Older/future fixture construction excluded. |
| `e5b4-3` | NOT RUN | RIG-PCSAVE read/write unavailable on Mac; historical 35/36/37 assertions superseded by schema 1. Older/future fixture construction excluded. |
| `e5b4-4` | NOT RUN | Requires an owned serving shop and metadata_config (rig-ops; Mac installation not implemented). |
| `e5b4-5` | NOT RUN | Documented Locked-PC dynamic sale/performance arm; serving fixture and Mac rig-ops absent. |
| `e5b4-6` | NOT RUN | Documented Locked-PC dynamic sale/performance arm; serving fixture and Mac rig-ops absent. |

### [e5b5](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b5.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b5-2` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b5-3` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b5-4` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b5-5` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b5-6` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |

### [e6f](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e6f.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e6f-1` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-2` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-3` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-4` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-5` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-6` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |
| `e6f-7` | NOT RUN | Installed Street Food vendor API missing; no PC-owned cart/truck or FT-E6-Vendors paid-sales fixture. Catalog/metadata and ledger/trace extensions are unavailable on Mac. |

### [f2c](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2c.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f2c-1` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2c-2` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2c-3` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2c-4` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2c-5` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |

### [f2d](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2d.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f2d-1` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-2` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-3` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-4` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-5` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-6` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2d-7` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |

### [f2e](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2e.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f2e-1` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |
| `f2e-2` | NOT RUN | Requires RIG-VIEW, RIG-CHECKPOINTS, RIG-CADENCE, RIG-PCSAVE or comparison/perf arms not installed on Mac. |

### [f2g](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2g.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f2g-1` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-2` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-3` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-4` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-5` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-6` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-7` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-8` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-9` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-10` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |
| `f2g-11` | NOT RUN | Documented Locked-PC/input-only arm; native comparison/view/cadence and PCSAVE tooling has no Mac installation. |

### [f4a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f4a.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f4a-1` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-2` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-3` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-4` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-5` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-6` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-7` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4a-8` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |

### [f4b](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f4b.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f4b-1` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-2` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-3` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-4` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-5` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-6` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-7` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4b-8` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |

### [f4c](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f4c.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f4c-1` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4c-2` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4c-3` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4c-4` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4c-5` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |

### [f4d](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f4d.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f4d-1` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-2` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-3` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-4` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-5` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-6` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-7` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-8` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-9` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-10` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-11` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-12` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-13` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-14` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4d-15` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |

### [f4e](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f4e.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f4e-1` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4e-2` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4e-3` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4e-4` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |
| `f4e-5` | NOT RUN | FT-F4 fixtures excluded; RIG-PERF-UNION/PRESENT/FOREGROUND/PRIORITY/producer/SHOPFIX capabilities not available on Mac. |

### [f5i](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5i.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f5i-1` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |
| `f5i-2` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |
| `f5i-3` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |
| `f5i-4` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |
| `f5i-5` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |
| `f5i-6` | NOT RUN | Pointer/hover/focus/click assertion reserved for Peter’s playtest. |

### [f8a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f8a.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f8a-1` | NOT RUN | F8h check 1 already recorded in PRs #199/#200 and explicitly excluded here; complete PC-owner inventory capability remains unavailable. Fix checks read only the required 49/49 count. |
| `f8a-3` | NOT RUN | Street Food removal would also remove FT control transport; PCSAVE and an independent Mac transport are unavailable. |
| `f8a-4` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8a-5` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8a-6` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8a-7` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8a-8` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8a-9` | NOT RUN | Explicitly excluded mid-session RLO toggle and identity projections. |
| `f8a-10` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8a-11` | NOT RUN | F8h conflict refusals explicitly excluded; this batch only supplies check-1 input. |
| `f8a-12` | NOT RUN | RIG-PERF-UNION not available on Mac; no clean union window measured. |

### [f8bg](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/f8bg.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f8bg-2` | NOT RUN | Street Food removal would also remove FT control transport; PCSAVE and an independent Mac transport are unavailable. |
| `f8bg-3` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8bg-4` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8bg-5` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8bg-6` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8bg-7` | NOT RUN | Explicitly excluded fault-build selection and PCSAVE projections; no Mac installation. |
| `f8bg-8` | NOT RUN | Explicitly excluded mid-session RLO toggle and identity projections. |
| `f8bg-9` | NOT RUN | Documented Locked-PC mod-set/mechanism arm; Mac mod-set/SHOPEDIT/payload comparator installation not implemented. |
| `f8bg-10` | NOT RUN | RIG-PERF-UNION not available on Mac; no clean union window measured. |

### [f9a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f9a.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9a-1` | PARTIAL | Private Mac Managed assemblies hashed; built/installed candidate DLL independently matches. Fingerprint unknown (27 differing assemblies); same-head offline full artifact comparison unavailable; native provenance incomplete. |
| `f9a-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-7` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-8` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-9` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-10` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9bdf](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/f9bdf.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9bdf-1` | PARTIAL | Private Mac Managed assemblies hashed; built/installed candidate DLL independently matches. Fingerprint unknown (27 differing assemblies); same-head offline full artifact comparison unavailable; native provenance incomplete. |
| `f9bdf-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9bdf-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9bdf-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9c](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f9c.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9c-1` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9c-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9c-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9c-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9c-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9c-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9e](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f9e.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9e-1` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-4` | NOT RUN | Pointer/hover/focus/click assertion reserved for Peter’s playtest. |
| `f9e-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9g](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/f9g.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9g-1` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9h](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/eaac521cdbe3cf46e9fe6451d358d993966cc72c/docs/checks/f9h.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9h-1` | PARTIAL | Private Mac Managed assemblies hashed; built/installed candidate DLL independently matches. Fingerprint unknown (27 differing assemblies); same-head offline full artifact comparison unavailable; native provenance incomplete. |
| `f9h-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9h-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [x3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/bf343e3/docs/checks/x3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `x3-1` | PARTIAL | Fresh city ran 08:01 → 09:01; schema-1 H1 menu/reload running with 957 residents/957 visitors and equal resident identity digests. Literal Loaded/schema-1 native log assertion absent; autosave-off setup not established. Auxiliary .NET reader reports Invalid visitor next event for both payloads. |

