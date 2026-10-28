# PC rig proof (agent1), rerun after review round 8

**Verdict: PASS for the requested bounded PR #2 gates.** PC / `rig_test`, isolated `C:\BigAmbitions-AgentTests`. The financial gate uses the [technical lead's exact-order reference](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/2#issuecomment-6473698507) and [approved 2^-28-cent unit](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/2#issuecomment-6476561353). No tolerance or comparison rounding is used. Native float accumulation drift is recorded separately. No release, ordinary-save deployment or Workshop approval follows.

Campaign directory label: **29 October 2026**. Actual execution is **28 October UTC**, as recorded in the lease and cleanup receipts. Before is merged PR #1, `36140a741c2bac9c54611561119a9b2ff783c79f`; after is fetched origin head `d7892fde71a1d912d0d0ca52e09cb4191db63314`. Its docs-only commit rebuilds to the expected production hash from the implementation head. No production source/tests were changed. Steam Managed assemblies were referenced in place.

| Check / expected | Before | After | Result / evidence |
| --- | --- | --- | --- |
| Build and complete unit suite pass | 1,267 passed; 0 failed/skipped | 1,403 passed; 0 failed/skipped | PASS offline; [builds](builds.json), [outputs](offline-output.txt) |
| Seven README harness/demonstration commands pass | All seven pass | All seven pass; additional checkout 330, catalogue 29, supply 19 pass | PASS offline; [exit records](offline-results.json) |
| Fractional-charge and invalid money-token behavior is red before / green after | Exact 109-sale charge check fails; all four malformed-token probes load instead of refusing | Exact charge check and all four malformed-token probes pass; complete suite includes the PR money-token tests | PASS red/green; [before](before-money-regression.log), [after](after-money-regression.log), [source](money-regression/Program.cs) |
| Financial (a): every debit/receipt equals captured native order price exactly | RED: 0/110 exact; $10.25 each | GREEN: 110/110 exact; $10.25279998779296875 each | PASS required red/green; [before](before-financial-verification.json), [after](after-financial-verification.json) |
| Financial (b): wallet debit and receipt totals equal exact native order sum | RED: $1127.5 versus $1127.8079986572265625 | GREEN: payments, wallet net debits, prepared-food ledgers and receipts each $1127.8079986572265625 | PASS required red/green; zero tolerance |
| Financial (c): record native running-total drift; do not gate against it | Native history/accounts/posted revenue $1127.8079833984375 | Same; drift from exact orders -$0.0000152587890625 | Recorded as native accumulation, separate from mod gate |
| Each resident ledger reconciles with audited charges | 1,914 consumers; 53 buyers; zero exact failures | Same | PASS; [before matrix](before-resident-ledgers.json), [after matrix](after-resident-ledgers.json) |
| Schema 13 -> 14, historical money/receipts preserved, byte-identical saved reload | Creates schema-13 FT seed with 110 v1 receipts | All 34,456 monetary fields, 957 identities/assignments and 1,914 consumers preserved; schema-14 saved payload identical across reload | PASS native; [migration](migration-summary.json) |
| Interrupted checkout saves/reloads once, paid replay does not duplicate, no stranded held funds | Supplies migration/accounting controls; no separate Serving replay campaign | Same Serving intention completes once; exact debit 1025.279998779296875 cents; paid reload/replay unchanged; final held zero | PASS bounded case; [checkout](interruption-summary.json) |
| Identical native unload/reload/shutdown steps; no new PC exceptions or inactive patches | Three fresh complete cycles | Three fresh complete cycles; same patch set | PASS controlled native smoke; [summary](native-qa-summary.json), [excerpts](native-log-excerpts.txt) |
| Three nominal 60-second windows per build, autosave off, no saves | Three clean windows | Three clean windows; off checked after every reload | PASS; [autosave](autosave-evidence.json) |
| Frame time not worse beyond observed noise | Three alternating samples | Compared under method recorded before timing | PASS descriptive spread comparison; [timings](performance-summary.json), [noise](performance-noise.json), [method](methodology.json) |
| Restore files/preferences, verify hashes, stop and release | Fresh verified backup before install | 4828 profile files and 107 references restored; root files/preferences verified; stopped; unheld/pending zero | PASS; [backup](backup-receipt.json), [cleanup](cleanup.json), [release](lock-released.json) |

## Exact financial observations

Same disposable FT morning checkpoint as the prior run: SHA-256 `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273`. Both builds run Day 5 07:45 -> Day 7 00:45 through native skips of 17 and 24 hours, outside performance windows, producing 110 croissants and bags. Test-player needs suppression is restored. No fixture guard, price, RLO or native financial value is changed.

Native price bits **1092881272** represent exactly **10.25279998779296875 dollars** = **1025.279998779296875 cents** = **275221504000 units**. Before charges **275146342400 units** ($10.25); after charges exactly **275221504000 units** for every order. The exact 110-order sum is **30274365440000 units** = **$1127.8079986572265625**. Before pays **$0.3079986572265625 less** than that sum; after difference is **exactly zero**. All 110 after receipt, held amount and confirmed ledger deltas agree. Observer faults, aggregate ledger failures and final held funds are zero in both financial arms.

| Day | Purchases | Before resident dollars | After resident dollars / exact order sum | Native accumulated history/account/posted revenue dollars |
| --- | ---: | ---: | ---: | ---: |
| 5 | 57 | 584.25 | 584.40959930419921875 | 584.40960693359375 |
| 6 | 53 | 543.25 | 543.39839935302734375 | 543.39837646484375 |

The game's float32 revenue accumulator totals **$1127.8079833984375**, differing from exact orders by **-$0.0000152587890625** in both builds. Native cash starts **$9792.3408203125** and ends **$10487.669921875**; delta **$695.3291015625**. Posted transactions total **$695.329010009765625**, including revenue **$1127.8079833984375** and other transactions **-$432.478973388671875**; cash accumulation residual is **$0.000091552734375**. Cash-implied gross is **$1127.808074951171875**, **$0.0000762939453125** above exact orders. These are native accumulation effects, retained without changing the reference or adding tolerance.

[Before raw observer rows](before-financial.jsonl) and [after rows](after-financial.jsonl) retain IEEE bits and selected measurement data, not raw saves. [Financial verifier](verify_financial.py) and [per-resident verifier](verify_resident_ledgers.py) use integer rationals. The [observer](FinanceObserver.cs) reads both monetary generations. New codec fields hold integer units divided by **268435456** for cents; plain diagnostic `{"Units":n}` values use the same divisor. Legacy `...Cents` fields remain cents. Policy rates with unchanged `...Cents` names remain whole cents. Diagnostic totals expressed as cents strings and receipt v1/v2 cents text are parsed exactly. Three [negative controls](verifier-self-checks.json) detect one payment unit, one wallet unit and one native-price bit; output uses exact integer long division rather than rounded decimal formatting.

Tooling corrections were completed before accepting evidence: the standalone harness's initial expected-unit literal was corrected to the exact value above; codec integer units were distinguished from diagnostic `{"Units":n}` objects in the ledger extractor. Preliminary failures remain private. Final exact verifiers reproduce the accepted results without modifying any native observation or production code.

## Migration and interrupted checkout

The source schema-13 payload is not rewritten merely by loading. The after build saves schema 14 with integer monetary fields, and its stored payload hash remains byte-identical on reload. Every historical monetary amount and all 110 original v1 receipt keys/values survive. All identity and assignment checks pass. Migration hashes are in [migration-summary.json](migration-summary.json); raw payloads remain private.

Serving checkpoint intention `food:consumer%3Apc%253A399%3A1:breakfast:5` resumes once at normal speed after save/reload. Its exact wallet debit equals the quote, **1025.279998779296875 cents**. Paid reload preserves purchase/spend counters and saved payload bytes. Explicit receipt replay returns **"Native saved receipt already confirms this intention"** and changes none of the captured native orders, cash, history, wallets or receipts. After native draining, **34** Day-5 purchases equal **34** native customers and **34** receipts, with zero held funds and zero ledger failures. The checkpoint was reached at speed 10, then continued at speed 1. This tests interrupted Serving/Paid states; it does not inject a process crash inside native checkout.

## Controlled native smoke and timing

Order **before1, after1, before2, after2, before3, after3**. Each fresh process loads the identical copied morning checkpoint, warms up for ten seconds, plays one normal-speed 60-second timing window, pauses, manually saves under a unique disposable FT name, invokes native unload/load, verifies autosave off, plays ten seconds, pauses and quits gracefully. Cycle 3 in each arm additionally repeats the same separate two-skip financial replay before shutdown to pair the pending-midnight-autosave path. This addition was recorded before either cycle 3 began. Complete logs include shutdown. Full native logs and private-capture hashes are retained in [private-evidence-hashes.json](private-evidence-hashes.json).

| Run | Active / inactive patch lines | PC exceptions | StreetLamp NRE | Food Trucks detached-save ERROR | SaveCurrentPosition NRE | Old timing-helper IO exceptions |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| before-qa-1 | 132 / 4 | 0 | 0 | 0 | 0 | 2 |
| after-qa-1 | 132 / 4 | 0 | 0 | 0 | 0 | 2 |
| before-qa-2 | 132 / 4 | 0 | 0 | 0 | 0 | 2 |
| after-qa-2 | 132 / 4 | 0 | 0 | 0 | 0 | 2 |
| before-qa-3 | 132 / 4 | 0 | 0 | 0 | 1 | 3 |
| after-qa-3 | 132 / 4 | 0 | 0 | 0 | 1 | 3 |

The same four inactive City Performance capabilities are retained: pedestrian refill, midnight cleanup deferral, tree detail and cinematic coverage. Old timing-helper `File.Move` IO exceptions are recorded separately. The financial/migration/checkout campaigns retain native detached-save and pending-autosave/SaveCurrentPosition errors ([extra-campaign inventory](native-extra-summary.json)). The extended after campaign initially had an unpaired shutdown NRE. To attribute it, cycle 3 in both arms additionally loads the original morning copy, repeats the same 17-hour and 24-hour skips and quits without another save/reload; the complete-log counts above include this matched path. All of these errors are outside timing. No new PersistentCity exception or inactive patch is concealed by those observations. This is a bounded comparison, not an error-free full-modpack claim.

All windows use RTX 4070 / i5-13600KF, Direct3D11, Low, 1920x1080, vSync off, target 90, one-third calendar factor and speed 1. The same hidden windowed launch, player and camera are used, with tiny camera float differences retained in raw environment data. Financial recording is off during timing. Mod scope is the retained helper's Update/authority union, not every hook or every frame cost.

| Build/window | Seconds | Mod p50 ms | Mod p95 ms | Mod max ms | Frame p50 ms | Frame p95 ms | Frame max ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| before 1 | 60.2828914 | 0.1568 | 0.2202 | 12.0667 | 11.0652 | 13.6422 | 99.605 |
| after 1 | 60.2506746 | 0.1525 | 0.2156 | 8.5781 | 11.1258 | 13.1881 | 101.8558 |
| before 2 | 60.2926503 | 0.1567 | 0.2205 | 8.6442 | 11.0937 | 14.0263 | 99.3309 |
| after 2 | 60.251539 | 0.1518 | 0.2173 | 8.4313 | 11.0696 | 13.3964 | 106.2087 |
| before 3 | 60.3927389 | 0.1542 | 0.2184 | 7.834 | 11.0802 | 13.3145 | 117.2704 |
| after 3 | 60.3441804 | 0.1534 | 0.2148 | 7.8056 | 11.0971 | 13.5797 | 105.9853 |

Before timing, the descriptive rule was recorded: compare **after median minus before median** against the **larger full within-build range**, separately for p50, p95 and maxima of mod and whole-frame observations. This performance-only spread rule never applies to money.

| Metric ms | Before median | After median | Change | Larger within-build spread | Worse beyond spread? |
| --- | ---: | ---: | ---: | ---: | --- |
| mod p50 | 0.1567000 | 0.1525000 | -0.0042000 | 0.0026000 | No |
| mod p95 | 0.2202000 | 0.2156000 | -0.0046000 | 0.0025000 | No |
| mod maximum | 8.6442000 | 8.4313000 | -0.2129000 | 4.2327000 | No |
| wholeFrame p50 | 11.0802000 | 11.0971000 | 0.0169000 | 0.0562000 | No |
| wholeFrame p95 | 13.6422000 | 13.3964000 | -0.2458000 | 0.7118000 | No |
| wholeFrame maximum | 99.6050000 | 105.9853000 | 6.3803000 | 17.9395000 | No |

Three windows do not establish causal speedup or statistical equivalence; tails and the full sample remain visible. All accepted windows have zero final population lag and zero timed/component exceptions or cross-frame calls. No interactive FPS or allocation claim is made.

Rendered/interactive QA remains **INCOMPLETE**: the hidden rig has no targetable UI window; the initial before game-view capture returns a null-texture error and after captures are black. Usable [before](screenshots/before-loaded-scene.png) and [after](screenshots/after-loaded-scene.png) images are native diagnostic scene-camera renders, not screenshots of the game UI. This limits visual acceptance; it does not substitute for or expand the requested controlled native/log and timing gate.

## Autosave, cleanup and boundaries

Original autosave: `preventAutoSave=false`, five minutes, three retained saves. Every timing window verifies `preventAutoSave=true` and no save in progress before/after, unchanged save file sizes/mtimes, and zero actual save-start/completion markers. Autosave is explicitly turned off and verified after each reload, including ten-second resumed play. Manual saves, native skips and shutdown are outside measured windows. Broad "saving" mentions include RLO's "not saving" diagnostics.

The fresh lease was acquired only when free. Original profile, ModsLocal, root installation receipt/run/log and isolated preferences were backed up before installing. Restore verified the complete inventory and SHA-256 of **4828** profile files and **107** installation references, original root files and preferences. Restored preference hash: **`66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d`**. Test helpers and disposable fixtures are absent from the restored active profile. The rig is stopped, lease unheld, pending operations zero. [Lease acquisition](lease-acquired.json), [cleanup](cleanup.json), [release](lock-released.json).

| DLL | SHA-256 |
| --- | --- |
| Before production | `222008b91283e2765747e1dbdf393efa6754b62a93fa1a50b613b87ceb2191ff` |
| After production | `54aa9dc8be97f0a3c89b629b7bc06813de0dad61e515ecd6d02d1f5cee470f54` |
| Identical retained timing helper | `dc939f272b1b451a7eb15dd7c6665e3a74d4a389e3ebfe6bf9d35d15d18696aa` |

No normal profile/game/save/ModsLocal was controlled, no RLO source/settings or Steam setting was changed, and no game DLL, raw save, decompiled code, preference export or credential is committed. No WSL distro, production/test change, PR merge, force-push or Workshop upload was used. Open limits: rendered interaction, larger-city/long-soak performance and crash injection inside native checkout are unverified; they are not represented as passing checks.

## Reproduction

`python verify_financial.py before-financial.jsonl` intentionally exits 1 (red); `python verify_financial.py after-financial.jsonl` exits 0 (green). Both `python verify_resident_ledgers.py verify <arm>-resident-ledgers.json` calls pass. `python verify_verifiers.py` passes all exact negative controls. The portable money harness runs with `dotnet run --project money-regression/Regression.csproj --configuration Release -p:SourceRoot=<source-worktree> -- <private-schema13-population-payload>`: exit 1 before and 0 after. Raw population payloads are intentionally not published.

`compile_helper.py <source-worktree>` compiles the hand-written rig guard/observer against the isolated Managed assemblies and selected built DLL/Harmony references. The SDK location is read from the user environment. Acquire the agent1 lease and make a fresh verified backup before installing or running any rig tooling; requests accept only the named disposable FT fixture family. [Manifest](file-manifest.json) verifies the selected public evidence.
