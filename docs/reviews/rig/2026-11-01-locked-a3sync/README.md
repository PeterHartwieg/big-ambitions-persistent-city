# VERDICT: PASS

Role: `rig_test`. Merge-gating locked-session integration proof for [PR #7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/7) after [PR #29](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/29). Fetched A3 head remained **`322e4ef`**. Control is **`8b05227`**. This verdict covers the requested integration subset; pointer checks remain for Peter. No merge, PR action, issue, release or ordinary-save deployment was performed.

| Check | Result | Evidence and scope |
| --- | --- | --- |
| 1. Load / F0 / D1 / C1 / E2a | PASS | Both builds load schema 17 with 957 residents / 1,914 consumers. No unexpected Persistent City error/exception in played logs. C1/D1 snapshots and initial legacy payload are identical; diagnostic reads leave state unchanged. E2a state/venue readers load in both. Default-off F0 releases 6 outside bodies to 0 inside, then reacquires 7 candidate / 6 control. [States and reads](functional-summary.json), [logs](log-checks.json). |
| 2. Schema-16 migration / two reloads / refusal | PASS | Every pre-17 field is exactly preserved by codec migration, including IDs, assignments, money, budgets, shops, active meal/return data and optional chances. Saved source is identical across two native reloads; live trait hashes remain identical. Policy starts on day 6; all written/reloaded schemas are 17. Noon fixture retains authority and all receipts; earlier meals have upgrade wording. Corrupt and schema-99 containers refuse and preserve original HSG hashes. [Migration](migration-summary.json), [fixtures](fixture-provenance.json). |
| 3. Weighted decisions / replay / checkout | PASS | 878/878 decision contexts match across two checkpoint replays, including shortlist/order, versioned draws, result, reason and exact quote; 1914 consumer money records match. Household replay spans create no order, receipt or stock delta. Mid-outward reload preserves intent/terms, then one checkout gives one receipt and one product/bag decrement. [Replay/accounting](a3-summary.json), [all projections](replay-projections.json.gz), [reload terms](midjourney-summary.json). |
| 4. Inspector bridge reads | PASS | Native-formatted home/work addresses; no displayed raw street keys. Traits are words only; actual upgrade wording, recorded household-choice explanation and travel purpose retained. Every batch preserves the entire payload and frozen route/market counters. [Strings and counters](inspector-summary.json), [household capture](household-popup.jpg). Seam evidence only; the supplementary paused capture is not an FPS sample. |
| 5. Matched performance | PASS | Eight clean >=60 s windows: control/candidate, idle/breakfast, two rounds. Every mod-work p95 <5 ms; 1 measured spike(s) >=30 ms. Whole-frame timing is separate below. [Raw mapping, load, validity and timings](performance-summary.json). |
| 6. Save size / save and load duration | PASS (informational) | Matched reference-population samples outside every timing bracket. [Sizes and timings](save-times.json); candidate schema-16 migration/read and both reload durations also in [migration evidence](migration-summary.json). |
| Real hover / click / wheel / focus | DEFERRED-INPUT | Locked-session pointer limitations; Peter's playtest. No gesture pass inferred from a bridge seam. |

## Performance and system load

Times are **p50 / p95 / max milliseconds**. The last columns show start CPU, dotnet/testhost/codex counts and available RAM; full before/start/after snapshots include top-process CPU-delta rankings. Other work shares this PC, so no causal speedup claim is made.

| Execution order | Seconds | Mod work ms | Whole frame ms | >=30 ms mod | Start load; RAM | Wait s |
| --- | --- | --- | --- | --- | --- | --- |
| base1-idle (load) | 60.280 | 0.311 / 0.396 / 11.985 | 11.206 / 13.530 / 56.359 | 0 | 25.6%; 0/0/9; 30.55 GiB | 690.1 |
| base1-meal (load) | 60.122 | 0.595 / 0.939 / 28.654 | 11.154 / 12.836 / 49.134 | 0 | 31.6%; 1/0/10; 29.69 GiB | 150.0 |
| cand1-idle (load) | 60.249 | 0.342 / 0.437 / 9.776 | 11.180 / 12.624 / 31.513 | 0 | 22.6%; 1/0/8; 30.08 GiB | 900.2 |
| cand1-meal (load) | 60.142 | 0.587 / 0.772 / 6.268 | 11.133 / 12.670 / 39.050 | 0 | 43.0%; 5/1/10; 25.94 GiB | 900.2 |
| base2-idle (load) | 60.102 | 0.293 / 0.380 / 9.313 | 11.239 / 13.350 / 50.109 | 0 | 16.7%; 1/0/8; 30.13 GiB | 900.2 |
| base2-meal (load) | 60.197 | 0.599 / 1.038 / 30.111 | 11.537 / 15.570 / 107.263 | 1 | 31.0%; 2/0/8; 29.55 GiB | 900.2 |
| cand2-idle (load) | 60.234 | 0.315 / 0.404 / 8.942 | 11.127 / 12.649 / 53.077 | 0 | 38.3%; 5/1/8; 28.04 GiB | 900.2 |
| cand2-meal (load) | 60.147 | 0.532 / 0.672 / 3.601 | 11.181 / 12.790 / 46.478 | 0 | 33.6%; 5/1/10; 29.24 GiB | 900.2 |

| Pooled matched condition | Mod work ms | Whole frame ms |
| --- | --- | --- |
| control-idle | 0.303 / 0.389 / 11.985 | 11.223 / 13.414 / 56.359 |
| control-meal | 0.596 / 0.987 / 30.111 | 11.297 / 13.941 / 107.263 |
| candidate-idle | 0.330 / 0.422 / 9.776 | 11.149 / 12.641 / 53.077 |
| candidate-meal | 0.561 / 0.726 / 6.268 | 11.155 / 12.722 / 46.478 |

**Flagged measured mod spikes >=30 ms:**

- `base2-meal`, Unity frame **188821**, phase **traffic-advance**: **30.1113 ms** (primary union 30.1064 ms; GUI/other disjoint components detailed in JSON).

Mod work is the Update/authority union plus same-frame whole `PopulationRuntime.OnGUI` and six physics/intersection hook bodies asserted outside the primary union. Nested components are not added. Other small callbacks outside these scopes remain unmeasured. Percentiles use floor(n*q); partial mod frames are excluded. Timing, cross-frame, event, dropped-frame and active-report errors are zero. Every measured spike >=30 ms is listed with window/frame/phase in JSON. Startup/load/skip component flags are [separate](outside-window-spikes.json).

Whole-frame stalls >50 / >100 / >250 ms: base1-idle: 2/0/0; base1-meal: 0/0/0; cand1-idle: 0/0/0; cand1-meal: 0/0/0; base2-idle: 1/0/0; base2-meal: 7/1/0; cand2-idle: 1/0/0; cand2-meal: 0/0/0.

Each arm reloads identical retained fixture bytes and uses the same native clock target, normal speed, one-third calendar factor, 15 s warmup, Low/1920x1080, 90 FPS target and vSync off. Capture-start clocks differ by transport edges; exact values are in JSON. Idle is day 5 morning, meal is day 6 breakfast. Autosave is verified off after reload and before/after every bracket; HSG inventories are unchanged and no save activity occurs inside a bracket. Manual saves stay outside captures. Dotnet/testhost is polled before each window for up to 15 minutes, then any remaining/new load is retained and labelled. No offline dotnet suite or extra perf-only rerun ran.

## Migration, replay and save samples

The real schema-16 morning fixture contains 6 active meals and 3 resident meal outings. Native reload advances a fraction of a minute before bridge pause; exact migration compares the saved source to the codec-upgraded image, separately from subsequent gameplay. Traits, IDs and exact saved source remain stable across both reloads. Noon migration has 301 consumers / 141 own residents with earlier-meal upgrade markers. The noon fixture also contains one actual returning meal journey; its pre-17 fields are preserved exactly. Native authority and receipt dictionaries compare exactly.

Fixed seed **-1903163055**, day 6. Draw hashes are independently recomputed from observed saved key inputs using the version-1 contract, rather than claimed as a separate RNG log. The two replays have 878 weighted decisions each. After mid-outward reload, resident debit, receipt and history each increase by **275221504000 exact units**; native purchases, receipt count and resident purchases each increase by one, and product/bags each fall by one. The traveller completes its return. Physical stock is checked from native item cargo: the staffing-dependent available bag count changes 0 to 181 at opening while physical cargo remains 181 throughout both household replay spans. The weighted day-6 order remains queued for native daily settlement; no full day-6 statement claim is made. All sampled household ledgers conserve exact cash with no negative balances or holds. [Detailed accounting and scope](a3-summary.json).

| Build; reference population | PC payload UTF-8 bytes | Native HSG bytes | Native save s | Native load/migration wall s |
| --- | --- | --- | --- | --- |
| control; 957 residents / 1914 consumers | 6,004,487 | 1,129,416 | 0.484 | 33.938 |
| candidate; 957 residents / 1914 consumers | 6,004,487 | 1,129,414 | 0.453 | 31.750 |

These are single native samples under changing load; wall durations include transport/readiness. The matched morning source is the retained schema-13 fixture, normally migrated to 17 in both arms. The real schema-16 gate uses the separate morning/noon fixtures. No isolated save-performance regression claim or large-city claim is made.

## Build, tooling and evidence

| Build | Tested source SHA | Built and actually loaded DLL SHA-256 |
| --- | --- | --- |
| control | `8b05227d80e150404669d6d8243537a8acfb688b` | `9b820a695480e842b09a4876a8466077163a6dca27850744b9cefe48ad723e30` |
| candidate | `322e4ef13de6572d034d0e0b122bcf7f0ca1edb2` | `b10eefbcec69518c6ae17a6085521774ae0ba7107d95fffac1e9ad19b3461fb5` |

Both untouched detached worktrees build against read-only agent1 Managed references with `%LOCALAPPDATA%\Microsoft\dotnet` prefixed on PATH. Loaded DLL hashes are verified in every arm. Unchanged RLO, Persistent City, bridge-only host and passive stage observers; Workshop disabled. Normal visible game window, D3D11; console remains locked by read-only WTS evidence [before](session-initial.json) and [after](session-final.json). [Build/load hashes and tooling corrections](tested-builds.json).

Two stage-only setup corrections are retained: an empty rival-shortlist request was safely refused before timing, and copied F0 static reflection flags were corrected before the busy-clock proof. The passive frame observer is byte-identical in every window. E2a static diagnostic reads were added outside captures without attaching a watcher. Production source/behavior was not patched. Earlier full A3 proofs are reused only as context; this bundle does not repeat the 16-shop/distant-winner/quote-cursor/unconfirmed-checkout matrix.

## Cleanup and publication

**Restore/hash/lease: PASS.** All owned games quit normally; no kill. Original profile **4828/4828** files and **107/107** install references match. Original ModsLocal, config, saves, preferences/autosave, receipt, run metadata and Player log are restored; preference-export and root-file SHA-256 values match. Original autosave was enabled, five minutes, three slots. Lease released, **held=false / pending=0**; no owned game/helper remains. [Backup, hashes, normal quits and release](cleanup-summary.json).

Windows settings/session connections, normal profile, Steam/Workshop, RLO source, unrelated worktrees and other processes were not changed. Private backups, raw saves/logs and lease token remain outside Git. Only this report folder is committed on the dedicated docs branch based on `8b05227`.

`python verify_evidence.py` recomputes all eight raw distributions/spikes and checks the manifest, deterministic draw projections, migration/refusal/reload, accounting, read-only inspector, logs, restore and lease invariants. No DLL, raw game save, credential or personal path is published.
