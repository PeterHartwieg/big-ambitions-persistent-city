# PC rig proof (agent1), after base merge

**Verdict: BLOCKED.** PC / `rig_test`, isolated `C:\BigAmbitions-AgentTests`. Campaign label: 29 October 2026; exact UTC execution times are retained in the lease, backup and cleanup receipts. No Workshop publish, PR merge, release or ordinary-save deployment is authorised by this report.

Before: `7357f78ca13edd5200e7e708950ffbc7a6ade1fe`. After: fetched origin head `06bc0faa17004b45f4fc35e2820d19d564d2cb0f`. The after documentation commits reproduce the production DLL from `27a856b`. Steam Managed assemblies were referenced in place. Production source and repository tests were not edited.

| Check / expected | Before | After | Result / evidence |
| --- | --- | --- | --- |
| Full suite, build, README harnesses and all standalone regressions pass | 1,403 tests; 21 regression projects; 30 recorded commands, all pass | 1,503 tests; 21 projects; 31 commands, all pass | PASS offline; [outputs](offline-output.txt), [exit records](offline-results.json) |
| PR #3 scheduling/settings and base-merge regressions absent/red before, green after | New scheduling/settings and BaseMerge cases absent | 43 scheduling, 18 settings and 34 BaseMerge cases pass in the full suite | PASS absence/green, not a claim the merged money implementation is red; [presence](regression-presence.json) |
| Default is 25; native opportunities show persistent stops | Feature absent; zero choices/stops | 317/1,309 accepted choices = 24.21696%; 95% Wilson 21.97385–26.61096%; 98 directly observed scheduled records | INCOMPLETE literal eligible-trip density; configured-choice rate passes. Realised recorded share 98/1,309 = 7.48663%, with geometry/duration/occupancy/group filters; [trace](after-optional.jsonl), [summary](optional-summary.json) |
| Visible native dwell, saved stop reload and original continuation | Feature absent | PASS | [Selected body/state proof](optional-reload-summary.json), [native diagnostic image](screenshots/default-optional-dwell.png) |
| 0/100, clamps and fallback are actually attached on reload | Feature absent | 8/8 setting cases; 0/100 behaviour separately sampled | PASS; [settings](config-summary.json), [behaviour](config-behaviour-summary.json) |
| Native schema 13 and before-written 14 migrate exactly to 16; saved reload byte-identical | Supplies schema-13 checkpoint and native schema-14 save with 110 receipts | 34,453 / 34,456 monetary fields; 957 identities/assignments and 1,914 consumers preserved | PASS; [migration proof](migration-summary.json) |
| Schema 15/16 exact units and exact legacy cents migrate; rounded/mixed/noncanonical/future payloads refused and preserved | BaseMerge tests absent | 13 in-process native-codec cases; 9 full native startup/save/reload refusal cases | PASS; [codec](native-codec-summary.json), [native byte comparisons](native-refusal-summary.json) |
| Financial (a): every payment and receipt exactly equals its native binary32 order | 110/110 exact | 110/110 exact | PASS both; no tolerance or rounding; [before](before-financial-verification.json), [after](after-financial-verification.json) |
| Financial (b): wallet debits and receipts equal exact rational order sum | $1127.8079986572265625 each | Same, exact difference zero | PASS both; 1,914 consumer ledgers, 53 buyers and zero exact failures each; [before ledgers](before-resident-ledgers.json), [after ledgers](after-resident-ledgers.json) |
| Financial (c): native running-total drift recorded separately | Native revenue $1127.8079833984375 | Same | Native float accumulation drift −$0.0000152587890625; not the mod reference |
| Identical native unload/reload/shutdown; no new PC exceptions/inactive patches | Three complete cycles | Three complete cycles | PASS bounded smoke: 132 active / same four inactive capabilities; zero PC exceptions; [full inventory](native-qa-summary.json) |
| Three 60-second frame windows per build, autosave off, no saves | Three clean windows | Three clean windows | PASS; [autosave checks](autosave-evidence.json), [timings](performance-summary.json) |
| Whole-frame times not worse beyond recorded within-build spread | Medians in table below | No whole-frame metric exceeds spread | PASS descriptive rule; mod work p50/p95 does regress; [comparison](performance-noise.json) |
| Restore/hash-verify original rig, stop and release | Fresh verified full backup | Restored=True; preferences match=True; lease held=False | PASS; [backup](backup-receipt.json), [restore](restore-receipt.json), [release](lock-released.json) |

## Scope and optional-stop denominator

The approved feature overlays an existing admitted visit. It does not add a destination or food sale. The source excludes food-owned shops and cancels a stop before an accepted food outing or food decision. The native trace records choices before geometry, time, occupancy and partner checks. Thus 25% applies to saved participation choices; it does not promise 25% of all journeys visibly stopping. A scheduled record is direct evidence of that citizen's stop; a paired conversation also grants its reciprocal member. The 98 count is the directly observed scheduler records, not an invented count of additional trips. The Wilson interval is a descriptive binomial reference for deterministic per-visit hash draws, not a claim of independent random residents.

Payments made *by* optional stops are not applicable to this design: they create none. All 110 after food payments were checked with the feature active. The additional 0 and 100 replays also produce 110 exact sales apiece; [their financial results](config-financial-summary.json) and raw observer rows are retained. The [native place sets](optional-food-separation.json) are disjoint. No price, fixture guard, native sales rule or RLO value was changed to manufacture a stop sale.

The literal requested realised-trip density gate remains **INCOMPLETE**. Against all pre-geometry choice opportunities, the directly scheduled-record share is **98/1,309 = 7.48663%**, not 25% (descriptive Wilson interval recorded in [rate limits](rate-acceptance-limits.json)). At 100, all **1,310/1,310** choices accept but only **420** direct scheduler records are created. This is consistent with the documented later eligibility filters; it does not prove every independently eligible trip stopped. No independent post-filter denominator was observed for declined choices, and paired peers do not each emit a scheduler callback. The source-configured choice gate, real native dwell and persistence gates pass; this unresolved metric prevents an overall pass under the literal requested plan.

## Visible optional-stop reload

The default-rate native image shows resident `c101` window shopping at **15 Fourth Avenue**. Its stop starts at minute **9304.556035041809**, ends at **9306.632435041809**, and retains original activity event **9340**. Before reload, native body generation 29 approaches and dwells. After reload, generation 13 / instance `-10331486` approaches, dwells, returns at 9306.633154392242 and reaches the entrance at 9306.806192874908. That same native body owns all four post-reload stages; the persistent identity/appearance, saved choice and stop geometry/deadlines remain equal. The original shopping continuation then completes: journey count **5?6**, at home **9 Fourth Avenue**. Saved payload bytes, global wallet/purchase counters and receipt keys/values are unchanged across reload. [Selected state/physical rows](optional-reload-summary.json) and [native diagnostic image](screenshots/default-optional-dwell.png).

## Native setting cases

| Input | Expected attached value | Observed parsed / attached | Result |
| --- | ---: | --- | --- |
| `0` | 0 | 0 / 0 | PASS |
| `100` | 100 | 100 / 100 | PASS |
| `-5` | 0 | 0 / 0 | PASS |
| `150` | 100 | 100 / 100 | PASS |
| `12.5` | 25 | 25 / 25 | PASS |
| `"invalid"` | 25 | 25 / 25 | PASS |
| `"75"` | 75 | 75 / 75 | PASS |
| `missing` | 25 | 25 / 25 | PASS |

Nonnumeric text and in-range fractional values fall back to 25. Numeric text is accepted by the documented existing converter, so `"75"` uses 75. Config edits were made while the city was unloaded. The campaign config is restored after the cases; final cleanup restores the pre-session file bytes.

- `config-zero`: 0/1310 accepted choices; 0 scheduled records; observer faults [].
- `config-hundred`: 1310/1310 accepted choices; 420 scheduled records; observer faults [].

## Exact finance

Reference: [financial gate (a)/(b)/(c)](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/2#issuecomment-6473698507), [approved checked integer unit](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/2#issuecomment-6476561353).

The same disposable Day-5 morning checkpoint as the PR #2 rerun was available: SHA-256 `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273`. Both arms used native skips of 17 and 24 hours, Day 5 07:45 → Day 7 00:45, outside performance windows. Native price bits `1092881272` are exactly **$10.25279998779296875** = **1025.279998779296875 cents** = **275221504000 units**. One unit is **2^-28 cents**, checked Int64 in production. Each arm's 110 orders sum to **30274365440000 units** = **$1127.8079986572265625**. Payments, receipts, net wallet debits and prepared-food ledgers equal this exact sum. Final held funds, ledger failures and observer faults are zero.

| Day | Orders | Exact resident/order dollars, both builds | Native accumulated revenue dollars |
| --- | ---: | ---: | ---: |
| 5 | 57 | 584.40959930419921875 | 584.40960693359375 |
| 6 | 53 | 543.39839935302734375 | 543.39837646484375 |

The game's native revenue total is **$1127.8079833984375**, drift **−$0.0000152587890625**. Native cash delta is **$695.3291015625**; posted transactions **$695.329010009765625**, including other transactions **−$432.478973388671875**. Native cash accumulation residual is **$0.000091552734375**; cash-implied gross differs from exact orders by **+$0.0000762939453125**. These native accumulation effects never change the resident reference.

[Raw before](before-financial.jsonl), [raw after](after-financial.jsonl), [observer](FinanceObserver.cs), [exact rational verifier](verify_financial.py), [per-resident verifier](verify_resident_ledgers.py). Three [negative controls](verifier-self-checks.json) reject a one-unit payment change, one-unit wallet change and one-bit native-price change. The initial driver mistakenly saved the verifier's abbreviated stdout instead of its full returned result for the ledger extractor; the final driver uses the full result. Raw observations and verifiers were unchanged.

## Native lifecycle and timing

Accepted order: **before2, after1, before3, after2, before4, after3**. Each fresh process loads the same checkpoint, warms up for ten seconds, plays one normal-speed 60-second window, pauses, manually saves under a unique FT name, unloads/reloads natively, verifies autosave off, plays ten seconds, pauses and quits gracefully. The first accepted pair additionally repeats the same fresh-checkpoint two-skip financial run and saved reload before shutdown. Later migration/configuration/refusal/visual probes use separate processes, outside the six controlled logs. A preliminary before1 window overlapped offline checks and is excluded.

RTX 4070 / i5-13600KF, Direct3D11, Low, 1920×1080, vSync off, target 90, speed 1 and one-third calendar factor. Financial and optional tracing are off during timing. The retained timing helper measures the Update/authority union; other hooks and whole-game costs remain outside that mod attribution. The camera/player and launch sequence are matched; small native float camera differences remain in raw environment records.

| Build/window | Seconds | Mod p50 / p95 / max ms | Whole-frame p50 / p95 / max ms |
| --- | ---: | --- | --- |
| before 2 | 60.2998244 | 0.1506 / 0.2161 / 10.9343 | 11.1162 / 13.3319 / 203.2643 |
| after 1 | 60.384485 | 0.2266 / 0.3015 / 11.5077 | 11.0737 / 13.3404 / 93.9059 |
| before 3 | 60.2738 | 0.1506 / 0.2133 / 11.3006 | 11.1196 / 13.3635 / 95.7946 |
| after 2 | 60.2925658 | 0.2267 / 0.3084 / 12.3245 | 11.126 / 13.3233 / 133.6168 |
| before 4 | 60.1885133 | 0.1537 / 0.2189 / 11.1348 | 11.1001 / 13.262 / 108.3813 |
| after 3 | 60.2516485 | 0.2225 / 0.2939 / 11.888 | 11.1177 / 13.3055 / 100.8914 |

The method recorded before timing compares after median minus before median with the larger full within-build range, for each metric separately.

| Scope / metric | Before median | After median | Change | Larger spread | Worse beyond spread? |
| --- | ---: | ---: | ---: | ---: | --- |
| mod p50 | 0.1506000 | 0.2266000 | 0.0760000 | 0.0042000 | Yes |
| mod p95 | 0.2161000 | 0.3015000 | 0.0854000 | 0.0145000 | Yes |
| mod maximum | 11.1348000 | 11.8880000 | 0.7532000 | 0.8168000 | No |
| wholeFrame p50 | 11.1162000 | 11.1177000 | 0.0015000 | 0.0523000 | No |
| wholeFrame p95 | 13.3319000 | 13.3233000 | -0.0086000 | 0.1015000 | No |
| wholeFrame maximum | 108.3813000 | 100.8914000 | -7.4899000 | 107.4697000 | No |

Whole-frame results pass this descriptive comparison; they do not establish equivalence or speedup. The 90-FPS cap can absorb extra mod work. **Measured mod work regresses:** median p50 +0.0760 ms and p95 +0.0854 ms exceed within-build spread. This is an open performance concern, not concealed by the capped whole-frame pass. All accepted windows have zero population lag, timed exceptions and cross-frame calls.

The first pair retains one Food Trucks detached-save error and four old timing-helper I/O exceptions per build; the other two pairs retain zero detached-save errors and two helper I/O exceptions per build. StreetLamp and SaveCurrentPosition NRE counts are zero in all six. These existing other-mod errors are outside timing and do not become an error-free-modpack claim. Exact exceptions and inactive capability lines remain in the linked inventory; [complete private-log hashes](native-log-provenance.json) identify every retained process log, including preliminary tooling attempts.

## Tooling corrections and evidence limits

Two initial settings attempts are excluded. The test unload coroutine was first moved from a city-scoped host to a persistent runner with a completion marker. A subsequent run identified the operative repeated-unload cause: the helper replayed an already answered unload request after each load. The final helper ignores completed request IDs. Only the stalled Python test controllers were stopped for these diagnosed tooling faults; owned game processes quit normally, and their complete logs are retained privately. A separate visual-controller race initially requested pause just as an asynchronous native skip began; it was corrected to wait for both the target minute and skip completion, then continued in the same fixture. The first full-refusal input also arrived too late for the startup snapshot; the input hook was moved to process lifetime and installed on a valid bootstrap city before the matrix. The accepted refusal cases require actual startup failure plus unchanged payload hashes after native save/reload. Finally, long negative-case caller IDs exceeded the retained probe 60-character guard; caller IDs were shortened and the already-refused city was continued without changing that guard. [Fixture-input timing](fixture-injection-correction.json), [caller ID correction](diagnostic-id-correction.json). No elapsed-time command limit or production fix was used. [First correction](unload-tooling-correction.json), [confirmed request replay](unload-replay-correction.json).

The hidden player has no targetable Windows game window. Images are native bridge diagnostic scene-camera renders, not UI screenshots. No interactive UI, every optional-stop kind/weather combination, larger-city/long-soak performance, or process crash inside native checkout is claimed. Required visible-stop/reload status is stated explicitly above; incomplete evidence is not a pass.

## Autosave and cleanup

Original setting: `preventAutoSave=false`, five minutes, three retained saves. Before/after every accepted measured window and after reload, autosave is verified off and no save is in progress. Save file sizes/mtimes are unchanged, and actual save-start/completion markers are absent. Broad RLO “not saving” log mentions are not save activity. Manual saves and skips stay outside timing.

Fresh backup verified **4828 profile files** and **107 installation references**. Restore verifies **4828 files**, **107 references**, original root files and preference export hash `66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d`. Original ModsLocal/config/preferences are restored; disposable fixtures/helpers are removed from the active profile by complete-profile restoration. Final stopped/released evidence is linked above.

| DLL | SHA-256 |
| --- | --- |
| before production | `54aa9dc8be97f0a3c89b629b7bc06813de0dad61e515ecd6d02d1f5cee470f54` |
| after production | `f860f88cc4cd1cf4cfbe69e164b596492d27b419fe63a57e6a6dbd145b890343` |

No normal game/profile/ModsLocal/save was controlled, no RLO/Steam setting or source was changed, no game DLL/raw save/credential is committed, and no WSL distro, Workshop upload, force-push or PR merge was used. Open concerns include the measured mod-work increase and the explicitly unverified broader native/UI coverage.

Incomplete required checks: realised-stop share among independently eligible trips (choice-rate evidence is complete).

## Reproduction

`python verify_financial.py before-financial.jsonl` and `python verify_financial.py after-financial.jsonl` must both exit 0. `python verify_resident_ledgers.py verify <arm>-resident-ledgers.json` and `python verify_verifiers.py` must pass. The same verifier accepts the published 0/100 raw observer files. Native replay controllers require an owned agent1 lease, a fresh verified backup, the local game/rig assemblies, and the private FT checkpoint/population captures; these inputs are deliberately not published. `file-manifest.json` hashes every selected public artifact without Git newline conversion.
