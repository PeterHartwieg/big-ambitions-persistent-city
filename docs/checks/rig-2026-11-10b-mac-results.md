# Batched rig results — 10 November 2026 (b), Mac (creation-save fix + re-run rows)

Role `rig_test`, where `mac`, instance `agentmac`. Tested `18008ef38d7d6c555df216edc8ae0affdee3479a`: `git fetch origin` and `git rev-parse origin/codex/persistent-city` matched the requested candidate before building; the tip had not moved. Native proof gates no merge. This is testing evidence only; no product changes or merge.

**Summary: 1 passed / 5 partial / 1 failed / 148 not run.** Counts cover the prior report's 153 PARTIAL/NOT RUN rows plus the two fix-creation-save-init checks. Prior PASS rows and the old failed creation-flow finding are not recounted. Alias rows retain their own counts.

Sources and format follow [the previous Mac batch](rig-2026-11-10-mac-results.md). Historical checks remain linked to `docs-history-2026-11-10`; schema-number assertions there are superseded by the held schema 1. `<stage>` means `~/HART-Builds/persistent-city-rig-2026-11-10b-mac`. Raw logs, bridge replies, payload extracts, backup manifest and stage-only tooling stay there. No raw saves, binaries or large evidence dumps are in this PR.

## Build and rig

| Item | Value |
|---|---|
| Host / role | Native Apple Silicon Mac M1 Pro, 16 GB; `rig_test`; agentmac at loopback port 28330, no tunnel. |
| Head / worktrees | `18008ef38d7d6c555df216edc8ae0affdee3479a`; detached `rig-2026-11-10b-mac` build and separate `rig-2026-11-10b-mac-evidence` / `codex/mac-rig-2026-11-10b`. Main working tree left clean. |
| Build command | `python3 tools/build.py --game-managed "<private-app>/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; Homebrew PATH and DOTNET_ROOT set for Python/dotnet/build commands. Private Mac Managed references; no import shim. Build succeeded. |
| DLL SHA-256 | `2310d07d1cf1a89d37ed758821523175fc1cca151d4c08b330ea1503d3262751`; installed DLL independently hash-matched. |
| Provenance | Mac fingerprint `unknown`, 27 differing assemblies. Per-file hashes in `<stage>/build-provenance.json`; installed mod DLL hashes in `<stage>/installed-dlls.json`. Same-head offline `full` artifact comparison unavailable. |
| Agent-rig | Official read-only checkout `cfcc5fde3f652356a5aa3585250f4a92604557fb`; runtime 6, native game log build 3682. |
| Launcher | Official `scripts/mac-agent` through a stage wrapper replacing only the local controller path. Default LocalControl DLL absent; built from copied official sources into `<stage>/control`. No official source edit or system-wide installation. |
| Start mode / resources | Headless, native launcher 30 FPS/two workers/muted settings. Before enabled fixture launch: 95.8 GiB disk, 49% reported free memory; before R2: 95.8 GiB / 46%. Resource gates passed. Concurrent harness work untouched. |
| Installation / modpack | Only isolated Agent Tests Mac app/profile. Existing full DLL set (29 DLLs), including RLO and Food Trucks. Changed PC DLL, `Locales/en.json`, PC config and isolated receipt only. No Steam app, normal profile or Windows/agent1 access. |
| PC config | Enabled=true, PhysicalEnabled=true, MaximumPedestrians=48, MaximumVehicles=24, PhysicalRadiusMetres=120; other compiled defaults. Both fresh native new_game flows created with PC enabled. No player food shop required for the fix checks; no shop fixture attempted. |
| Autosave | Original native keys absent (default interval 5 minutes / maximum 3). Pre-launch plist backed up; isolated `MinutesBetweenAutoSaves=0` set and read back after reloads. A native Recover #0 write occurred despite that plist value; runtime autosave suppression is not established. No measured windows or save profiling. Original plist restored byte-for-byte. |
| Ownership | Owner `pc-mac-rig-2026-11-10b`, initially free, acquired once. Normal stop, restore and release at cleanup. No OS setting changes or unrelated process control. |
| Offline checks | Candidate build and stage-only actual-payload reader builds succeeded. No full suite or regression suite run; no production/test-source changes. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| `mac-b-creation` | Fresh native new_game, enabled PC; automatic creation save | PASS; save deferred PC payload, initialization completed, city running, 49/49 targets. |
| `mac-b-save-reload` | Ordinary S1 save, native menu/load transition | FAIL; schema-1 success epoch written, native load done, PC stopped on lost accounting input epoch. |
| `mac-b-r2` | Second fresh enabled native new_game | City running; initial and 600-frame capability/coordinator reads collected. |
| `mac-b-hour` | R2 08:01 → 09:03, speed 6; H1 checkpoint | City running, 957 residents/957 visitors; manual save retry completed after native recovery write. |
| `mac-b-h1-reload` | H1 native menu/load, 600-frame warm read, H1-Reload save | City running, 34/34 entries, 23/23 hooks; generation 1 → 3. |
| `mac-b-cleanup` | Pause, normal stop, restore and release | Restored hashes verified; game stopped, lease free. |

## fix-creation-save-init

**Check 1 PASS / check 2 FAIL.** [Committed fix result](fix-creation-save-init-mac-b.json), first evidence commit `72ea5bb`.

Check 1's log contains `Persistent City skipped the save during initialization; no Persistent City state was written: Persistent City initialization has not completed.` The automatic creation save completes with no `save epoch refused` or `start refused (clock.city)` in that creation log. The settled capability header is `city running` with 49/49 target methods patched. The creation checkpoint contains no PC payload, as expected for the deferred callback.

S1's ordinary save contains the PC payload at schema 1 and a success epoch marker `v1`, `sequence=1`, `schema=1`, describing all 4,634,419 payload characters. The first ordinary sequence remains 1 after the skipped creation callback. Stage-only production codec read is `Loaded`, schema 1, 957 residents / 957 visitors. Native menu/load completes; PC then logs `Accounting input epoch was lost` and reports `start refused (clock.city)` with 13/49 targets. The save itself is retained. This is a reload failure after the creation-save fix has completed successfully.

## Findings

1. **FAIL — immediate manual-save reload.** PC stops on `Accounting input epoch was lost` after loading S1. Native game remains playable. This is the check-2 failure; no root-cause claim is made beyond the emitted refusal.
2. **PARTIAL — E5b3 transient warm-up.** R2/H1 initial and 600-frame readbacks show all 34 capability entries passed, 23/23 hooks, Active with warming=0; router generation 1 → 3. Native bridge completion comes after the transient Warming mode; that assertion remains unobserved.
3. **PARTIAL — X3 exact native readback.** H1 reload is running with 957 residents / 957 visitors. H1 and H1-Reload resident identity digests match: `9cbcbf784406a6f4e58fdb3044ca7be172a02681326a9c780ce8dc9e417f1108`. The literal `Loaded, schema 1` native log assertion is not emitted. The stage-only strict .NET reader reports `Invalid commute duration` for H1 and `Invalid visitor next event` for H1-Reload, while native Mono load succeeds. These auxiliary results do not establish native corruption; full payload equality is not claimed.
4. **PARTIAL — F9 provenance aliases.** Private Mac assembly set remains unknown; installed candidate DLL matches the built artifact. No same-head offline full artifact comparison, Windows-native proof or RIG-ACCEPT run.
5. **Native recovery-save overlap.** Native recovery checkpoints were written despite the isolated plist interval reading 0. The first H1 manual save was refused as already saving; the following load correctly refused the absent H1 name. The later manual save and actual menu/load retry completed. No performance window was measured, and no busy-save refusal was counted as a successful checkpoint. All recovery files in the two newly created character folders are hashed below and retained.

Mac modpack limits remain: Street Food vendor API absent, interior view disabled by default, and Bakery/PolyModsHyperreal asset-bundle errors retained in raw logs. Headless unsupported-shader messages are expected. No alternate assets/mods were installed.

No performance window was measured, so there is no performance table or p95 verdict. Existing routing diagnostics at or above 30 ms are flagged below; concurrent harness work makes these observations provisional. Whole-frame traces are kept separate in the isolated profile; they are not PC mod-work measurements.

Emitted routing spikes (provisional, outside measured windows): `player-fix-reload.log` frame 4137: preparation_ms 131.681 ms; `player-final.log` frame 2562: preparation_ms 124.152 ms; `player-final.log` frame 8415: preparation_ms 131.273 ms; `player-final.log` frame 8475: closure_ms 91.560 ms.

Diagnostic maxima at or above 30 ms (aggregate maxima, not additional enumerated samples): `player-fix-reload.log` scene search maximum 124.559 ms; `player-final.log` scene search maximum 128.054 ms; `player-final.log` closure refresh maximum 91.560 ms. Full bounded diagnostics: `<stage>/observed-spikes.json`; samples not emitted by the game cannot be enumerated.

## NOT RUN grouped by missing capability, fixture, or needs Windows client / PC

The current Mac installation supplies the documented FT bridge, not the Windows-gated rig-ops/flow extensions. No capability was assumed available from an offline implementation. The following are current-run NOT RUN rows, not inherited passes.

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

Explicit scope exclusions: F7e, F8h itself and F8h check 1 (already recorded in PRs #199/#200); FT-F4 / FT-F9 fixtures; pending RIG-PRESENT, RIG-PERF-UNION, RIG-FOREGROUND, RIG-PRIORITY, RIG-F4-PRODUCER, RIG-SHOPFIX, RIG-ACCEPT, RIG-FAULT, RIG-RLOTOGGLE and RIG-PCSAVE. F7e/F8h add no separate row to this prior-report catalogue. Pointer/hover/focus/click checks are NOT RUN (playtest), including `f5i-6` and `f9e-4`. No Windows client/player/input or Windows test directory was used. Fix-required target counts do not re-run F8h's owner inventory assertion.

## Fixtures

Only fresh disposable FT cities created by native new_game in the isolated profile at this candidate, PC enabled throughout. Checkpoints remain in place; none of Peter's real saves were accessed.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT-PC-Mac-b-20261110-18008ef3-H1-Reload` | `fba33c337122efffc81c2b109ccbc508af6cb6582cbc87a6f0f6d59b4a13cd65` | 2668647 |
| `FT-PC-Mac-b-20261110-18008ef3-H1` | `0e0f0ed1c7d3732d312c679d3457852120d106e0ccbd2933420072a90dbffd8d` | 2611504 |
| `FT-PC-Mac-b-20261110-18008ef3-R2` | `432b90f1c19f97e1723338e6531061220037d37392604d89d2fdae8d73164735` | 678674 |
| `FT-PC-Mac-b-20261110-18008ef3-R2 / Recover #0` | `c881dccf95c9bae72c98b2dab4479e9c9cf047cc2405d4c374118b7cc9177825` | 2611479 |
| `FT-PC-Mac-b-20261110-18008ef3-R2 / Recover #1` | `c9c2b6b173ae3b5065443545c33b25589b1e114ef79700e118c8f75104814034` | 495878 |
| `FT-PC-Mac-b-20261110-18008ef3-R2 / Recover #2` | `222ca8e7303c7fb49df13162f7dcc4d824abac88b62a1d801a6b44e7192dd7fc` | 500364 |
| `FT-PC-Mac-b-20261110-18008ef3-S1` | `ab307a7a39413e09a1a5d203bb664b055f34e080996c566fac18a4c0d459503c` | 1584690 |
| `FT-PC-Mac-b-20261110-18008ef3` | `43ba4f28e96cd2d39764e5a27604354d3f0f72b8f5db9bb7a83248c5ed27a01c` | 512198 |
| `FT-PC-Mac-b-20261110-18008ef3 / Recover #0` | `6443d79d434fffc3dd65db4c05e1abbcea23eacfa84fdbb53b90b04bcfcf9a80` | 7553 |
| `FT-PC-Mac-b-20261110-18008ef3 / Recover #1` | `6320b330b73b59289ceefb7842336a19e787a91a6c0c09253e87b02756e9c5f4` | 504576 |
| `FT-PC-Mac-b-20261110-18008ef3 / Recover #2` | `a5ab615f6d353a9bc154eb573819632f4d430ac76105b51743f657a919c5dc84` | 509048 |

## Cleanup

Normal `mac-agent stop` succeeded; no force stop. Restored and SHA-256 verified all **260** backed-up files, including original isolated ModsLocal, PC config, install receipt, profile top-level preferences and native preference plist. Removed newly added mod files, including `Locales/en.json`; autosave key absence restored. Disposable saves retained. `<stage>/restore.json` records zero mismatches.

Final status: `running=false`, lease `held=false`, pending 0. Owned game/controller/helper commands finished; no per-process assertion from this run remains. Official rig source and main repo remain clean. No Windows PC or normal game instance was contacted.

## Per-check results grouped by slice

PASS means the complete numbered assertion passed. PARTIAL lists the missing assertion; FAIL names an observed failed expectation. Previously passed checks were not re-run as catalogue rows.

### [fix-creation-save-init](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/18008ef38d7d6c555df216edc8ae0affdee3479a/docs/checks/fix-creation-save-init.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `fix-creation-save-init-1` | PASS | Fresh enabled native creation save completed; skipped-initialization line, no refusal in creation log, city running, 49/49 targets. |
| `fix-creation-save-init-2` | FAIL | S1 ordinary save has schema-1 PC payload and success epoch sequence 1. Native menu/load done, PC stopped: Accounting input epoch was lost; 13/49 targets. |

### [e5b2](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b2.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b2-2` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-3` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-4` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |

### [e5b3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b3-1` | PARTIAL | R2 and H1 native reload: 34/34 capability entries passed, cargo reader/self-test passed, 23/23 hooks; generation 1 → 3; Active/warming=0 before/after 600 frames on both sides. Transient Warming not observable after bridge load done; S1 immediate reload failure is recorded separately. |
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
| `f5i-6` | NOT RUN | Pointer / hover / click checks stay for Peter’s playtest. |

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

### [f8bg](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/18008ef38d7d6c555df216edc8ae0affdee3479a/docs/checks/f8bg.md)

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
| `f9a-1` | PARTIAL | Candidate built against private Mac Managed; installed DLL hash matched. Fingerprint set unknown (27 differing assemblies), same-head offline full artifact comparison unavailable; native incomplete. |
| `f9a-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-7` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-8` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-9` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-10` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9bdf](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/18008ef38d7d6c555df216edc8ae0affdee3479a/docs/checks/f9bdf.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9bdf-1` | PARTIAL | Candidate built against private Mac Managed; installed DLL hash matched. Fingerprint set unknown (27 differing assemblies), same-head offline full artifact comparison unavailable; native incomplete. |
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
| `f9e-4` | NOT RUN | Pointer / hover / click checks stay for Peter’s playtest. |
| `f9e-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9g](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/18008ef38d7d6c555df216edc8ae0affdee3479a/docs/checks/f9g.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9g-1` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9h](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/18008ef38d7d6c555df216edc8ae0affdee3479a/docs/checks/f9h.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9h-1` | PARTIAL | Candidate built against private Mac Managed; installed DLL hash matched. Fingerprint set unknown (27 differing assemblies), same-head offline full artifact comparison unavailable; native incomplete. |
| `f9h-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9h-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [x3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/bf343e3/docs/checks/x3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `x3-1` | PARTIAL | R2 ran 08:01 → 09:03, schema-1 H1 native reload reached city running with 957 residents/957 visitors. Identity comparison recorded in Findings. Literal Loaded/schema-1 native log assertion unavailable; strict auxiliary .NET reader reports Invalid commute duration on H1. |
