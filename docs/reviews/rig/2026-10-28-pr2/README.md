# PC rig proof (agent1) — PR #2, 28 October 2026

**Verdict: FAIL.** The requested exact native financial equality fails after PR #2. All unit/README checks pass, the intended fractional-charge regression is red before and green after, migration and the bounded interrupted-checkout campaign pass. Native full-run QA and the stronger frame-time/noise gate remain **INCOMPLETE**. No merge, release, ordinary-save deployment or Workshop clearance follows.

PC / `rig_test`, isolated agent1 at `C:\BigAmbitions-AgentTests`. Origin and PR head were checked before compilation: before `3df8232384d293f6c7fb20522577a9ef3dc5901c`, after `9f89590cbea9897ffb46e510073e2104a73614b3`. Before is PR #1 code; its later evidence-only head does not change that code. No production code or tests were edited. Both packages compiled against installed Steam Managed DLLs referenced in place.

| Check / expected | Before (expected red for changed money behavior) | After (expected green) | Result and evidence |
| --- | --- | --- | --- |
| Build and unit suite pass | 1,267 passed, 0 failed, 0 skipped | 1,304 passed, 0 failed, 0 skipped | **PASS**; [outputs](offline-output.txt), [exits](offline-results.json) |
| Seven README regression/simulation commands pass | All seven pass | All seven pass; 329 checkout, 29 catalogue and 19 supply checks also pass | **PASS offline**; [outputs](offline-output.txt) |
| Same 109-croissant fractional-charge regression fails before, passes after | 1,025 cents each; 111,725 cents debited; exit 1 | 1025.279998779297 cents each; 111755.519866943373 cents debited; exit 0 | **PASS red/green**; [source](fractional-regression/Program.cs), [outputs](offline-output.txt), [exits](fractional-regression-results.json) |
| Every payment, wallet and receipt agrees with native orders, history, accounts and cash **exactly** | Known mismatch reproduced, 110 sales | 110 matching float identities and internally conserved wallets/receipts; exact native equality still fails | **FAIL**; [before](before-financial-verification.json), [after](after-financial-verification.json), [exact verifier](verify_financial.py) |
| Every resident ledger reconciles with audited payments | All 1,914 consumers pass; 53 buyers | All 1,914 consumers pass; 53 buyers | **PASS internal conservation**, separate from native gate; [before](before-resident-ledgers.json), [after](after-resident-ledgers.json), [verifier](verify_resident_ledgers.py) |
| Before schema-13 save loads, migrates, saves and reloads as 14 | Creates nonempty schema-13 seed | All 34,456 monetary fields, 110 historical receipts, 957 identities/assignments and 1,914 consumer IDs preserved | **PASS**; [migration](migration-summary.json) |
| Save/reload around checkout; paid receipt replay; no stranded holds | Before arm supplies migration/accounting controls; no separate native Serving replay in this arm | Same Serving intention completes once at normal speed; Paid save/reload and receipt replay leave native orders/cash/history/wallets/receipts unchanged; final held funds zero | **PASS bounded case**; [interruption](interruption-summary.json). No forced crash inside native checkout was tested |
| Same unload/reload steps: no new error/patch signature | 132 active patch lines, same 4 inactive; one Food Trucks detached-save ERROR and one helper IO exception in controlled sequence | Same controlled sequence and signatures; no StreetLamp exception in either; no PersistentCity exception stack | **PASS controlled subset**, [checker](native-log-checks.json), [excerpts](native-log-excerpts.txt) |
| Full native QA, including shutdown, has no unexplained new error | Main run: 2 helper IO exceptions; one detached-save ERROR | Main: 3 helper IO exceptions; same detached-save ERROR. Extended run: 9 helper IO exceptions; final shutdown adds native SaveCurrentPosition/pending-midnight-autosave NullReferenceException | **INCOMPLETE** broad error gate; final native exception is not paired with an equivalent current-before shutdown reproduction. [Inventory](exception-summary.json) |
| Three nominal 60-second windows per build, autosave off, no saves | 3 clean windows | 3 clean windows, including off verification after reload | **PASS collection**; [autosave](autosave-evidence.json), [timings](performance-summary.json) |
| Frame times not worse beyond noise; rendered smoke | p95 scopes and whole-frame intervals retained | No observed p95 regression; maxima/tails mixed; hidden launch has no game-view texture | **INCOMPLETE stronger performance/rendered gate**; see limits below |
| Restore every changed file/preferences, stop, release | Original backup verified before installation | 4,828 profile files, exact inventory/SHA-256, 107 installation references, original root files and preferences verified; helpers removed; stopped; lease unheld/pending 0 | **PASS**; [cleanup](cleanup.json), [release](lock-released.json) |

## Raw money observations

The same copied FT morning checkpoint (SHA-256 `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273`) runs from Day 5 07:45 through Day 7 00:45: native skips of 17 and 24 hours, outside timing windows. Both arms produce 57 Day-5 and 53 Day-6 purchases, 110 croissants and 110 bags in native history, with identical native prices and native financial results. Test-player needs suppression is restored after skips. The earlier PR #1 displayed $1127.808 total is not used as an exact raw binary value here.

| Dollars, no rounding of comparison values | Before resident payments | After resident payments | Native history / accounts / posted cash revenue |
| --- | ---: | ---: | ---: |
| Day 5, 57 purchases | 584.25 | 584.40959930419929 | 584.40960693359375 |
| Day 6, 53 purchases | 543.25 | 543.39839935302741 | 543.39837646484375 |
| Total | 1127.5 | 1127.8079986572267 | 1127.8079833984375 |

Native revenue minus resident payments is **0.3079833984375 dollars before** and **-0.0000152587892 dollars after**. The exact sum of the 110 native order prices is **1127.8079986572265625 dollars**; after wallets exceed that sum by **0.0000000000001375 dollars**. Each native price has binary32 bits `1092881272`, exact value **10.25279998779296875 dollars**; each after debit is **10.25279998779297 dollars**. All 110 after charges round-trip to the same float, but 0/110 are mathematically equal to its exact binary rational. Round-trip identity is supplemental; it does not replace the requested equality check.

Native aggregate float arithmetic creates a second difference. Both builds start cash at **9792.3408203125** and end at **10487.669921875**, a delta of **695.3291015625**. New posted transactions are two revenues totaling **1127.8079833984375** and two wages totaling **-432.478973388671875**; their exact sum is **695.329010009765625**, with a **0.000091552734375** cash residual. Cash delta minus the other transactions implies gross receipts of **1127.808074951171875**. Inventory resource expenses in the business statements are retained separately; pre-bought stock is not invented as a new cash debit in this window. No native value was rewritten or rounded to obtain equality.

The [read-only observer](FinanceObserver.cs) records every checkout/new native order, paid receipt and confirmation, selected native history/statements/transactions/cash, plus ledger totals. [Before raw commerce measurements](before-financial.jsonl) and [after measurements](after-financial.jsonl) retain the captured float bits and decimal numbers. They contain selected measurement data, not raw city saves. The standard-library [verifier](verify_financial.py) uses integer rationals; there is no tolerance or comparison rounding. The separate per-resident matrices verify net wallet debits after income, household costs and holds for all consumers, including non-buyers. Observer faults: zero in both arms. Held funds and ledger failures at both financial ends: zero.

## Migration and checkout scope

The before build manually saved the schema-13 fixture with 110 v1 receipts. After loading it with PR #2, the source payload remained byte-identical until a successful manual schema-14 save. The schema-14 payload is byte-identical across reload. All historical monetary fields and original receipt strings survive; no retroactive repricing or native sale is introduced by migration. [Hashes and assertions](migration-summary.json).

The interrupted case saves a live Serving intention, reloads it, then completes that same intention once at normal speed. Exact debit: **1025.279998779297 cents**. Its Paid save reloads with unchanged purchase/spend counters and identical stored payload bytes. Direct receipt replay returns “Native saved receipt already confirms this intention” and leaves the captured financial/native state unchanged. After a separate native drain, Day-5 history, native customers and paid receipts all count **34**, with zero held funds across all 1,914 consumers. This is a separate natural-play campaign, not the matched 110-sale financial replay.

Initial speed-10 continuation was retained: core minute 7867 versus native minute 8277.012397766113, zero purchases. The same Serving save was then repeated at speed 1 after paused catch-up. The initial lag is not a passing acceleration result. One migration helper poll saw the old city before loading started; the time command refused it. The corrected wait required the requested loaded save and the successful completed reload is separately captured. Neither issue was repaired in production code or hidden by verifier changes.

## Native timings, logs and images

Both arms use the same checkpoint/player/camera, Low quality, 1920×1080, RTX 4070/i5-13600KF, Direct3D11, vSync off, target 90, speed 1 and one-third calendar rate, with the same retained timing helper (`dc939f272b1b451a7eb15dd7c6665e3a74d4a389e3ebfe6bf9d35d15d18696aa`). Financial recording is off during timing. These scopes measure Update/authority work, not every possible mod hook.

| Arm/window | Seconds | Mod p95 ms | Mod max ms | Whole-frame p95 ms | Whole-frame max ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Before 1 | 60.3096859 | 0.2848 | 16.1611 | 14.9666 | 142.9396 |
| Before 2 | 60.1736567 | 0.4043 | 9.2082 | 15.6326 | 168.4078 |
| Before 3 | 60.2834187 | 0.3398 | 8.7122 | 14.7384 | 86.9537 |
| After 1 | 60.2096236 | 0.2725 | 9.0284 | 14.9359 | 186.8571 |
| After 2 | 60.1767447 | 0.3166 | 12.3768 | 15.5794 | 254.391 |
| After 3 | 60.2102573 | 0.3154 | 10.6522 | 14.2579 | 89.6966 |

All six have zero timed/component exceptions, cross-frame calls and final population lag. All before windows precede after windows. The larger after whole-frame maxima and second-window p99 (89.0881 versus 22.8958 ms), run order and system/native variation prevent a stronger no-regression-beyond-noise conclusion. No interactive FPS, rendered-gameplay or native allocation claim is made. Hidden launch returns `ArgumentException: Passed in texture is invalid (null)` for the game-view capture. The small [before](before-loaded-city.png) and [after](after-loaded-city.png) PNGs are fixed diagnostic scene-camera renders, not screenshots of the game UI; identical scenery is not proof of UI/state equality.

Original autosave was `preventAutoSave=false`, 5 minutes, 3 retained files. The isolated guard verifies `preventAutoSave=true` and no save in progress before and after each accepted window, including after reload. Save inventories/mtimes remain unchanged; actual save markers are absent. Broad “saving” mentions include RLO’s “not saving” and are retained separately. Manual saves, native skips and the final shutdown error occur outside all measured windows. Original preferences were restored byte-for-byte.

Native ModAPI uses Player-agent.log; BepInEx/MelonLoader do not apply. Both main logs contain 132 active patch lines and the same four inactive CityPerformance capabilities (pedestrian refill, midnight cleanup deferral, tree detail and cinematic coverage). Each matched save/unload/reload control produces one old-helper File.Move IOException and one Food Trucks detached-save ERROR; neither produces StreetLamp or PersistentCity exceptions. The previous StreetLamp failure was not reproduced here. The extended after shutdown adds a native SaveCurrentPosition/pending-midnight-autosave NullReferenceException, with no PersistentCity frame. This is retained and keeps broad error acceptance incomplete. [Raw-log hashes/excerpts](native-log-excerpts.txt), [checker](native-log-checks.json), [exception stacks](exception-summary.json).

## Restoration and boundaries

Lease acquired `2026-10-28T14:57:03.9839391Z` after waiting for the previous owner to release it. Agent1 alone was controlled through identity-checked port 18330. Full profile, root installation receipt/run/log and isolated registry preferences were backed up before installation. All **4,828** original profile files, exact file inventory, **107** receipt references and original root files were restored and SHA-256 verified; preference export SHA-256 **66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d** matches the original. Full profile restoration removes test helpers and disposable fixtures. Rig stopped, lease released `2026-10-28T15:23:47.8978074Z`, unheld and pending zero. [Backup](backup-receipt.json), [cleanup](cleanup.json), [release](lock-released.json).

No ordinary profile/save/ModsLocal, RLO source/settings, Steam setting, WSL distro, production source/test, merge, force-push or Workshop upload was changed as part of this test. Runtime-generated isolated files were restored with the profile. Full raw logs, saves, payloads and inventories remain private; game DLLs, decompiled code and credentials are not committed.

DLL SHA-256:

- Before: `222008b91283e2765747e1dbdf393efa6754b62a93fa1a50b613b87ceb2191ff`.
- After: `2c2a9c14f34f4a7ba7522000ef6002e12d932ddca2fdde225dd5d951ca8403a0`.

Open gates: exact native finance equality; full native shutdown/error acceptance; rendered QA and stronger frame-tail/noise acceptance. [Evidence hashes](file-manifest.json).

## Reproduction

Run `python verify_financial.py before-financial.jsonl` and the corresponding after file (both intentionally exit 1 under exact equality). Run `python verify_resident_ledgers.py verify before-resident-ledgers.json` and the after matrix (both pass). Raw bits and all payment rows are retained. Run the fractional harness with `dotnet run --project fractional-regression/FractionalRegression.csproj --configuration Release -p:SourceRoot=<before-or-after-worktree>` (exit 1 before, exit 0 after). Output uses the machine’s decimal display culture; verification data uses canonical JSON decimals.

`compile_helper.py <source-worktree>` compiles the hand-written rig guard/observer against the isolated Managed assemblies and the selected built PersistentCity/Harmony DLLs; the SDK path reflects this rig. Read the agent1 lease/backup procedure before installing any helper. It accepts only disposable FT/PR2 fixtures. `check_native_logs.py <private-evidence-root>` and `check_native_exceptions.py <private-evidence-root>` rerun the retained log checks. Migration/interruption analyzers require the privately retained selected population captures; their input hashes are recorded, not their raw save payloads.
