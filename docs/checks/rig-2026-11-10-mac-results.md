# Batched rig results — 10 November 2026, Mac (F8h check 1 + post-9 Nov pending checks)

Peter's intermediate Mac rig scope, role `rig_test`, machine `mac / agentmac`. Tested exactly `b3e5a9101fe065662c0baf3aedab01caa123773e`; `git fetch origin` and `git rev-parse origin/codex/persistent-city` matched before building. This is a Mac rig record and establishes no Windows-specific compatibility. Native proof gates no merge. No release, Workshop, version, changelog, campaign purchase, product change or merge.

**Summary: 4 passed / 6 partial / 1 failed / 147 not run.** The 158 catalogue rows comprise 157 post-`c2fc1b09` numbered check rows plus one native creation-flow finding. Repeated checks in F8a/F8b-g and F9 provenance retain their own rows; counts are not unique experiments. The initial PC-enabled creation failed; the running-city check-1 retry passed and is committed as F8h input.

Sources: changed check files between `c2fc1b09` and `docs-history-2026-11-10`, plus added/modified current check files through the tested head. Deleted check files were read from that tag. X3 schema-one's first check is from `bf343e3`; the older test-only X3 section has no native check. X1/X2 add no in-game rows. The historical 6 November checklist has no post-baseline change; its already pending rows are not recounted here. The requested [9 November report format](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/05d505b678ba71c564186193f1e66ec0793e7a3e/docs/checks/rig-2026-11-09-results.md) supplied the structure.

`<stage>` is `~/HART-Builds/persistent-city-rig-2026-11-10-mac`. Raw logs, bridge replies, payload extracts, backup manifest and stage-only tooling stay there. No raw saves, assemblies, large evidence dump or tokens are committed.

## Build and rig

| Item | Value |
|---|---|
| Host / role | Mac, native Apple Silicon M1 Pro, 16 GB; `agentmac` / `rig_test`; loopback port 28330, no tunnel. |
| Head / branch | `b3e5a9101fe065662c0baf3aedab01caa123773e`; detached build worktree; separate `codex/mac-rig-2026-11-10` evidence worktree. Main tree left clean. |
| Build command | `python3 tools/build.py --game-managed "<private-app>/Contents/Resources/Data/Managed" --provenance <stage>/build-provenance.json`; Homebrew PATH and DOTNET_ROOT set as instructed. Private Mac Managed references, no import shim. |
| DLL SHA-256 | `0f34e6e22e5157f7aed25b41fe8cb2af14727275cdea99fbf64a5bd5be476c2d`; installed DLL independently hash-matched before cleanup. |
| Build provenance | Private-app referenced assemblies hashed read-only: `unknown` declared set, 27 differing files; `Newtonsoft.Json.dll` matches. No same-head offline `full` artifact was produced. F9 native provenance remains incomplete. Full per-file hashes: `<stage>/build-provenance.json`. |
| Agent-rig source | `cfcc5fde3f652356a5aa3585250f4a92604557fb`, official checkout read-only. Runtime 6; game log identifies build 3682; capability header itself says `game 0.1`. |
| Launcher | Official Mac launcher through a stage wrapper that changes only its local controller path. Missing `build/mac/control/LocalControl.dll` was rebuilt from copied official sources in `<stage>/control`; no official source edit or system-wide install. |
| Start mode / resources | Headless, 30 FPS cap, two workers, muted; start gate passed. Before first start: 98.2 GiB disk, 47% reported free memory. Concurrent harness work untouched; no performance isolation claim. |
| Install / mod set | Private Agent Tests Mac app; isolated `com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal`. Existing full Mac DLL set, including RLO 0.45.0 and Food Trucks 0.10.2; all DLL hashes in the check-1 JSON. Only PC DLL, its English locale and isolated receipt were staged. Missing Bakery/PolyModsHyperreal asset bundles are retained findings. |
| PC config | Enabled=true, PhysicalEnabled=true, MaximumPedestrians=48, MaximumVehicles=24, PhysicalRadiusMetres=120; other keys compiled defaults. R2's automatic creation save used Enabled=false, then stopped/restarted with Enabled=true before its first PC load. |
| Autosave | Original keys absent: native defaults 5 minutes / maximum 3. Set isolated native `MinutesBetweenAutoSaves=0` before positive PC load; read back 0 after restarts/reloads. No measured windows or save profiling. Original key absence restored. |
| Ownership / isolation | Lease owner `pc-mac-rig-2026-11-10`; initially free, acquired once, released after stop and restore. No Windows/agent1 contact, normal profile/app access, OS setting change, foreground/input change or unrelated process control. |
| Offline checks | Candidate build succeeded. No full suite or regression suite run. Stage-only readback helper compiled Core/Persistence and expanded actual fixture payloads; its strict .NET reader limitations are described below. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| `mac-creation-1` | Fresh native new_game, PC enabled, automatic creation save | FAIL: save reached PC initialization before completion; city stopped. |
| `mac-creation-r2` | Fresh native new_game at the same candidate, PC disabled for creation save | Completed; new character/FT city saved; no old fixture reused. |
| `mac-check1` | Restart PC enabled; load R2; capability status, hooks and activation log | PASS; city running. |
| `mac-contracts` | Capability/status reads before/after warm-up and two menu/load cycles | All 34 entries pass, 23/23 hooks, coordinator Active; generations 1 → 3 → 5. |
| `mac-hour` | R2 08:03 → 09:04, speed 6; manual H1 checkpoint | Saved schema 1; 957 residents and 957 visitors. |
| `mac-reload` | FT load H1 via native menu transition, save H1-Reload; repeat load H1 | Native city running both times; same resident identities; 685/682-frame warm readbacks. |
| `mac-cleanup` | Pause, normal stop, restore and release | Hash verification passed; no owned process remains. |

## F8h check 1

**Verified by rig (Mac): [f8h-check1-modpack-owners.json](f8h-check1-modpack-owners.json).** Header `city running`; four required capabilities and the present-dependent Driven Rides clock are active. Twelve optional capabilities are active; two are off with reasons: `vendor.streetfood` — Street Food vendor API missing; `interior.view` — default setting off.

All 49/49 inventory targets are patched. Six targets have four foreign owners: `cityperformance.mod`, `drivenrides.mod`, `foodtrucks.mod`, `hardmodepatch`. The JSON includes each target's patch kind and class (`may-skip`, `observer`, `swallows`), the status report, activation excerpts, head/DLL/modpack hashes and fixture. The activation line precedes some optional initialization and says `not checked` for those capabilities; the settled report records them active or off with a reason. Activation conflict scan: 19.693 ms.

R2 is a fresh native FT city created at this head, then loaded with the full enabled Mac modpack. No owned shop/cart was required by the authorized check-1 exception. This records owners only; F8h conflict refusal behaviour was not run. No PC producer evidence was copied.

## Findings

1. **FAIL — native creation save versus initialization (Refs #94, Refs #95).** The first automatic FT new_game save logged `Persistent City save epoch refused: Persistent City initialization has not completed.` PC stopped with `start refused (clock.city)`; only 13/49 targets were patched in that attempt. R2 avoided that creation-save race by creating with PC disabled and loading after an enabled restart. The initial failed fixture is retained; the workaround is not a product fix.
2. **PARTIAL — X3 exact log/readback coverage (Refs #192).** H1 and H1-Reload both contain `{"Schema":1,"f":"pcc-c","v":1,`; native reloads reactivated 957 residents/957 visitors with no older-build refusal. Resident identity digest is identical: `c24a8f0d9d4f7304a7f68d438b51c0873761b77556a1aa240922a140e866d5c3`. The production compact expansion was used read-only for that comparison. The game does not emit a literal `Loaded, schema 1` line. The auxiliary .NET 10 strict reader reports `Invalid commute duration` / `Invalid visitor next event`; those are offline helper results, while the native Mono loads succeed. `ParseR27`'s exact round-trip numeric spelling is a possible explanation, not a confirmed cause. Full payload equality is not claimed.
3. **PARTIAL — Mac provenance (Refs #96).** The installed Mac assembly fingerprint is unknown in `supported-game.json`; the three provenance aliases retain partial status. No Windows compatibility inference.
4. **Mac modpack limits.** Street Food's vendor API is absent despite FoodTrucks loading, so vehicle scope checks have no positive fixture. Bakery and PolyModsHyperreal log missing asset bundles. No replacement assets or product mods were installed.
5. **Diagnostic spikes, outside measured windows.** No union performance verdict is available. PC's existing routing logs flag preparation 127.658 ms (frame 2569), preparation 127.527 ms (6624), closure 97.455 ms (6681), preparation 126.420 ms (9338), preparation 31.773 ms (9376), closure 95.035 ms (9411). Diagnostic scene-search maxima are 120.246, 124.898 and 123.770 ms; repeated maxima are not additional samples. These are startup/reload diagnostics, not whole-frame or complete union measurements. All emitted ≥30 ms PC diagnostic maxima are retained in `<stage>/observed-spikes.json`; unreported samples cannot be enumerated from these bounded logs.

## NOT RUN grouped by missing capability, fixture, or needs Windows client / PC

The official rig-ops documentation gates its addon to the Windows isolated product/port/lease; Mac loading/install support is not implemented. Its catalogue, shop fixture, metadata, comparison and performance operations were not assumed available through `mac-agent call`. Only the documented Mac FT bridge was used. Checks explicitly marked Locked-PC remain not run except Peter's explicit check-1 and read-only provenance exceptions.

**RIG-FOODSHOP / Mac shop operations — 14 rows.** `e5b2-2`, `e5b2-3`, `e5b2-4`, `e5b3-2`, `e5b3-3`, `e5b3-4`, `e5b3-5`, `e5b3-6`, `e5b4-4`, `e5b5-2`, `e5b5-3`, `e5b5-4`, `e5b5-5`, `e5b5-6`.

**RIG-PCSAVE / schema fixtures — 3 rows.** `e5b4-1`, `e5b4-2`, `e5b4-3`.

**needs Windows client / PC — 25 rows.** `e5b4-5`, `e5b4-6`, `f2g-1`, `f2g-2`, `f2g-3`, `f2g-4`, `f2g-5`, `f2g-6`, `f2g-7`, `f2g-8`, `f2g-9`, `f2g-10`, `f2g-11`, `f5i-1`, `f5i-2`, `f5i-3`, `f5i-4`, `f5i-5`, `f5i-6`, `f8a-4`, `f8a-5`, `f8a-10`, `f8bg-3`, `f8bg-4`, `f8bg-9`.

**Street Food API / FT-E6-Vendors — 7 rows.** `e6f-1`, `e6f-2`, `e6f-3`, `e6f-4`, `e6f-5`, `e6f-6`, `e6f-7`.

**Mac view / checkpoint / comparison capabilities — 14 rows.** `f2c-1`, `f2c-2`, `f2c-3`, `f2c-4`, `f2c-5`, `f2d-1`, `f2d-2`, `f2d-3`, `f2d-4`, `f2d-5`, `f2d-6`, `f2d-7`, `f2e-1`, `f2e-2`.

**FT-F4 / pending performance capabilities — 43 rows.** `f4a-1`, `f4a-2`, `f4a-3`, `f4a-4`, `f4a-5`, `f4a-6`, `f4a-7`, `f4a-8`, `f4b-1`, `f4b-2`, `f4b-3`, `f4b-4`, `f4b-5`, `f4b-6`, `f4b-7`, `f4b-8`, `f4c-1`, `f4c-2`, `f4c-3`, `f4c-4`, `f4c-5`, `f4d-1`, `f4d-2`, `f4d-3`, `f4d-4`, `f4d-5`, `f4d-6`, `f4d-7`, `f4d-8`, `f4d-9`, `f4d-10`, `f4d-11`, `f4d-12`, `f4d-13`, `f4d-14`, `f4d-15`, `f4e-1`, `f4e-2`, `f4e-3`, `f4e-4`, `f4e-5`, `f8a-12`, `f8bg-10`.

**RIG-PCSAVE / independent transport — 2 rows.** `f8a-3`, `f8bg-2`.

**RIG-FAULT / RIG-PCSAVE — 6 rows.** `f8a-6`, `f8a-7`, `f8a-8`, `f8bg-5`, `f8bg-6`, `f8bg-7`.

**RIG-RLOTOGGLE / RIG-PCSAVE — 2 rows.** `f8a-9`, `f8bg-8`.

**F8h / RIG-FOREIGNPATCH — 1 rows.** `f8a-11`.

**FT-F9 / RIG-ACCEPT — 30 rows.** `f9a-2`, `f9a-3`, `f9a-4`, `f9a-5`, `f9a-6`, `f9a-7`, `f9a-8`, `f9a-9`, `f9a-10`, `f9bdf-2`, `f9bdf-3`, `f9bdf-4`, `f9c-1`, `f9c-2`, `f9c-3`, `f9c-4`, `f9c-5`, `f9c-6`, `f9e-1`, `f9e-2`, `f9e-3`, `f9e-4`, `f9e-5`, `f9e-6`, `f9g-1`, `f9g-2`, `f9g-3`, `f9g-4`, `f9h-2`, `f9h-3`.

F7e and F4f were also excluded as requested; they add no numbered post-baseline check-file row at this head and are not added to the counts. No FT-F4/FT-F9 fixture was created or used. No fault build, RLO mid-session toggle, PCSAVE write, foreign patch or Windows-only install was attempted.

## Fixtures

Only new disposable FT characters/cities in the isolated Mac profile. All checkpoints remain there. `R2` was created with PC disabled solely for its automatic creation save; H1 and H1-Reload were saved with PC enabled at the tested head. Failed first creation remains separate.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT-PC-Mac-20261110-b3e5a910` | `72582922547f2a36805f99fbecdba8e1acd8d8666808ddbb45962cb849416136` | 509035 |
| `FT-PC-Mac-20261110-b3e5a910-H1` | `518663d55bda5d81fa41f3fdeb3dd00af76df00c5f49c4ecee9735135f9c9122` | 2722743 |
| `FT-PC-Mac-20261110-b3e5a910-H1-Reload` | `3cc66e75076039669821811b5fdbd8c54e7f66f0213fe519480998c2558dbdbe` | 2796475 |
| `FT-PC-Mac-20261110-b3e5a910-R2` | `0a121cd78a5bdfa423595c967567747ff726860ab52b6d47d59adeb91541ee2e` | 504981 |

## Cleanup

Normal `mac-agent stop` succeeded; no force stop. Restored and SHA-256 verified all **1,841** backed-up isolated profile files, including every original ModsLocal file and `persistent-city.json`; removed the newly added English locale; restored and hash-verified the isolated install receipt and native preference plist. Autosave key absence/defaults restored. The native plist backup was captured during the first creation-load phase, before its creation save; a pre-launch byte snapshot was not taken, so complete pre-launch plist equality is not established. The backed-up plist itself restored byte-for-byte. Fixture saves were retained.

Final status: `running=false`, lease `held=false`, pending 0. Owned game/controller/helper processes exited; no per-process assertion from this run remains. The unrelated older caffeinate process was untouched. Official rig Git source remains clean. No Windows PC, agent1, normal profile or Steam installation was used.

## Per-check results grouped by slice

PASS means the numbered assertion passed on this Mac; PARTIAL names the missing assertion. NOT RUN reasons below belong to their group above. Historical check text naming writers 35–37 is superseded by schema 1 and was not treated as an executable positive assertion.

### [e5b2](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b2.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b2-1` | PASS | All 34 capability entries passed; 23/23 hooks; generation 1 → 3 → 5. |
| `e5b2-2` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-3` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |
| `e5b2-4` | NOT RUN | Requires serving/mixed shop or catalogue/mod-set/shop-edit/comparator extensions; rig-ops is Windows-gated and Mac installation is not implemented. |

### [e5b3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `e5b3-1` | PARTIAL | Cargo reader/self-test and Active coordinator verified across reloads; transient Warming was already over when native load completed. |
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
| `e5b5-1` | PASS | Dynamic acquisition true, product policy published, all capability entries passed; reload readbacks 685 and 682 frames apart. |
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
| `f5i-6` | NOT RUN | Locked-PC or input-only visitor-body check; RIG-PCFIX/body-ID readback and Mac view/input installation unavailable. |

### [f8a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f8a.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f8a-1` | PARTIAL | All 49 inventory targets reported; foreign owners/classes kept. Original complete PC-owner inventory assertion is not exposed by the foreign-owner report. |
| `f8a-2` | PASS | Same check as F8b-g 1; Mac exception explicitly authorized. |
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

### [f8bg](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/b3e5a9101fe065662c0baf3aedab01caa123773e/docs/checks/f8bg.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f8bg-1` | PASS | Mac running-city capability and hook-owner record committed. |
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
| `f9a-1` | PARTIAL | Private Mac assemblies hashed and installed DLL matches candidate; declared fingerprint unknown; no same-head offline full artifact comparison. |
| `f9a-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-7` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-8` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-9` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9a-10` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9bdf](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/b3e5a9101fe065662c0baf3aedab01caa123773e/docs/checks/f9bdf.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9bdf-1` | PARTIAL | Same provenance read as F9a 1; unknown Mac fingerprint leaves native incomplete. |
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
| `f9e-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-5` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9e-6` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9g](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/b3e5a9101fe065662c0baf3aedab01caa123773e/docs/checks/f9g.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9g-1` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9g-4` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [f9h](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/b3e5a9101fe065662c0baf3aedab01caa123773e/docs/checks/f9h.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `f9h-1` | PARTIAL | Same provenance read as F9a 1; unknown Mac fingerprint leaves native incomplete. |
| `f9h-2` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |
| `f9h-3` | NOT RUN | FT-F9 / f9-city-ready fixture and RIG-ACCEPT excluded; Windows client steps remain not run. |

### [x3](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/bf343e3/docs/checks/x3.md)

| Check | Result | Evidence / reason |
|---|---|---|
| `x3-1` | PARTIAL | Native hour/save/reload and schema 1 verified; identical resident identity digest. Literal Loaded/schema log line not emitted; auxiliary .NET reader rejects canonical numeric tuples. |

### native FT new_game

| Check | Result | Evidence / reason |
|---|---|---|
| `mac-creation-1` | FAIL | Bridge automatic creation save reached PC before detached initialization completed; Save epoch refused: Persistent City initialization has not completed. |
