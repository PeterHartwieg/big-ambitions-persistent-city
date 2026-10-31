# VERDICT: PASS

Role: `rig_test`. PR #10 locked-session merge gate. Candidate **`487f2e9f02906e060494e76d7ca311f1ff6b9af4`**, unchanged remote head at fetch; baseline/actual merge base **`6d43276198eba5d0dd016b51bc7fc670bc651762`**, not `73129e0`. Only test tooling and this evidence were written. No merge, PR action, release, ordinary-save deployment, Windows/session change or offline test suite was performed.

| Required gate | Result | Evidence |
| --- | --- | --- |
| 1. Native facts, owner split, exact prices, reload refusal | **PASS** | [Field comparisons, binary32 bits/exact units, dumps, reload](fact-match.json) |
| 2. Zero native changes after >=100 refreshes/dumps | **PASS** | [Independent native hashes, stock/finance coverage, paused clock](zero-native-changes.json) |
| 3. Exercised/idle and interleaved build performance | **PASS** | [Per-window distributions, load, GC, components and raw mapping](performance-summary.json) |
| 4. Cold refresh spread over bounded units | **PASS** | [Cold frames, unit costs, allocations and retained memory](cold-spread.json) |
| 5. No Persistent City errors/exceptions | **PASS** | [Full private Player-log scan and log hashes](log-summary.json) |
| Diagnostic: native allocation bytes | **NOT-RUN (counter unavailable)** | Independent known-allocation check and Unity counter availability are recorded; raw zero deltas are not zero-allocation proof. |
| Diagnostic: native 20,000-place coverage | **NOT-RUN (fixture has 883 controllers)** | Gate 4 uses the largest available in-game image; the 20k figure remains offline evidence. |
| PR input item 3: real price/hour UI change | **DEFERRED-INPUT** | Requires Peter's own playtest. No gesture was injected. |
| PR input item 4: real ownership/replacement UI change | **DEFERRED-INPUT** | Requires Peter's own playtest. No replacement was fabricated. |

The disposable FT morning checkpoint has **957 residents / 1,059 simulation places** and **883 native controllers**. Independent native discovery found **38 reviewed-food advertisers / 36 eligible rival food shops**. Chosen rival: `address:ba%3Astreet_fifthavenue:6:shop`; player: `address:ba%3Astreet_thirdstreet:30:shop`. The eight-place shortlist includes both. Native identity fields, owner, exact price units, hours, temporary-closure flag, access/door facts and saved rating match. The closure flag was read in its fixture state; no closure gesture is claimed. Rival stock is `Unknown`, evidence `EstimatedFromPriceAndHours`; no stock quantity, service time or capacity is fabricated. Unsupported advertised counts and a native refusal example are retained. Save/reload occurred outside timing: the old instance refuses the changed save, while a fresh reader reproduces the independently recaptured readable facts. Matching tuples across reload are **not generation continuity**.

One retained reader completed **100** refresh/`ReadSelected(8)` cycles with the game paused. Each read constructs the detached eight-shop DTO image; JSON serialization is outside that loop. All captured registration fields, prices, stock containers and saved finance/rival histories match exactly. The rival has 0 native item-instance records and 6 saved daily-income entries; empty native containers are preserved as empty evidence, without an invented stock estimate. Field-only reflection reads existing state and does not call sale, stock setup, registration creation or finance mutation methods. [Selected before/after native canaries](native-canaries.json) allow recomputing their hashes; full fixture inventories and saves remain private. Truncated field paths: **0**.

Timing values below are **p50 / p95 / max, milliseconds**. Mod work is the same-frame Update/authority union plus whole PopulationRuntime.OnGUI plus the separately observed D1 operation. Nested scopes are not added twice. Other hook bodies outside those scopes remain unmeasured. D1 reflection invocation overhead is included; report serialization and observer bookkeeping are outside the operation timer. Whole-frame pacing/stalls are informational under the locked desktop.

| Window (execution order) | Seconds | Combined mod work ms | Whole-frame ms | Mod spikes >=30 ms | CPU before/after |
| --- | --- | --- | --- | --- | --- |
| base1-breakfast-idle | 60.30 | 0.3242 / 0.4422 / 8.2581 | 11.1527 / 12.8453 / 31.3253 | 0 | 29/22% |
| cand1-breakfast-idle | 60.20 | 0.3208 / 0.4506 / 25.7373 | 11.1462 / 13.1104 / 37.8873 | 0 | 25/30% |
| cand1-breakfast-reader | 60.44 | 0.4226 / 0.8903 / 13.5921 | 12.2561 / 18.2643 / 71.4907 | 0 | 26/69% |
| base2-quiet-safe-idle | 60.11 | 0.4494 / 0.6272 / 19.5391 | 11.1495 / 12.6479 / 40.6260 | 0 | 23/30% |
| base2-breakfast-idle | 60.28 | 0.2947 / 0.4291 / 9.0298 | 11.2239 / 13.3878 / 46.2046 | 0 | 19/42% |
| cand2-quiet-safe-idle | 60.20 | 0.5708 / 1.0500 / 8.0912 | 12.3440 / 17.7499 / 50.4415 | 0 | 29/65% |
| cand2-quiet-safe-reader | 60.49 | 0.5452 / 0.9206 / 9.4642 | 11.5032 / 14.4658 / 34.1241 | 0 | 74/24% |
| cand2-breakfast-reader | 60.43 | 0.3535 / 0.5007 / 9.8475 | 11.1460 / 13.2493 / 38.5866 | 0 | 24/25% |

All measured mod-work p95 values are below **5 ms**; accepted-window maximum is **25.7373 ms**. **0 mod spikes >=30 ms** occur across accepted and supplemental captures. Whole-frame stalls are separate: **3 >50 ms, 0 >100 ms, 0 >250 ms** in the eight accepted windows. All eight have normal native clock advance and autosave off before/after, unchanged save-file metadata, no native save activity, >=60 seconds and zero attribution/drop/active-report errors. Clock starts are within one game minute of the common 15-second warm-up target; exact offsets and native advances are recorded. Original autosave was enabled (`preventAutoSave=false`, five minutes, three slots) and restored.

Build order is **baseline 1 → candidate 1 → baseline 2 → candidate 2**. The accepted breakfast comparison uses baseline 1, candidate 1 idle/exercised, baseline 2 and candidate 2 exercised windows. The accepted quiet comparison uses baseline 2 and candidate 2 idle/exercised on the fresh safe quiet checkpoint. Three original quiet captures are retained as supplemental: two valid timing captures from the exhausted-player fixture and one hospital-paused capture with zero native advance. They are not matched quiet clearance. The fresh fixture uses the existing skip-needs helper only during preparation; the native energy setting is restored before its save. These replace the invalid quiet comparison within the requested eight accepted windows. Each window reloads identical checkpoint bytes and uses normal native speed plus 15 seconds of warm-up. Safe quiet: day 6, 00:45 checkpoint; breakfast: day 5, 07:45 checkpoint. Same Low graphics, 1920×1080, target 90 FPS, vSync 0, D3D11/RTX 4070, unchanged RLO plus bridge-only host and observer. This is the isolated test arrangement, not full-modpack or unlocked presentation certification. No clean, matched performance sample was rerun. The failed quiet comparison is retained separately and replaced with the controlled fixture.

| D1 phase in the paused 100-cycle proof | Operations | Cost ms p50 / p95 / max | Allocated bytes (sum) |
| --- | --- | --- | --- |
| cold-begin | 1 | 0.0362 / 0.0362 / 0.0362 | unavailable |
| cold-unit16 | 56 | 0.1237 / 0.1406 / 0.1540 | unavailable |
| dump8 | 100 | 0.1344 / 0.1720 / 0.2143 | unavailable |
| warm-begin | 99 | 0.0322 / 0.0446 / 0.0537 | unavailable |
| warm-unit16 | 5544 | 0.0515 / 0.0649 / 0.1470 | unavailable |

| Normal-play exercised window | Complete cycles | D1 operation max ms | Managed bytes released at disposal | GC count deltas (0/1/2) |
| --- | --- | --- | --- | --- |
| cand1-breakfast-reader | 82 | 0.4026 | 139,264 | [2, 2, 2] |
| cand2-quiet-safe-reader | 90 | 6.8431 | 352,256 | [2, 2, 2] |
| cand2-breakfast-reader | 93 | 0.2755 | 163,840 | [2, 2, 2] |

The first-process cold trace spans 58 distinct frames, max **6.8161 ms**; the maximum D1 operation in accepted normal play is **6.8431 ms**. Neither is a single-frame full refresh. Mono exposes equal collection-count deltas for its generation queries; these are not summed as independent collections.

Measured exercised-window phase distributions, unit/backlog/completed counts, allocations, GC collections and disposal heap deltas are in the linked JSON/raw CSVs. **Allocation bytes: NOT-RUN (counter unavailable).** Both Unity frame allocation and independently checked Mono thread counters are unavailable; raw zero thread-counter deltas are not zero-allocation evidence. GC collections and managed retention are recorded. This diagnostic gap does not alter the stated p95/max timing thresholds. Heap deltas are noisy Unity managed-heap observations, excluding native fixtures/unmanaged weak handles. The largest available native image is **883 retained places**, far below 20,000. Every continuation requests **16 controllers**, with at most one catalogue operation per Unity frame. The offline 20k aggregate **40.9646 ms** is many bounded units; this native run does not claim 20k coverage or a single-frame drain.

System CPU samples range **19–74%**, available RAM **18.40–29.13 GiB**. Each window checks external dotnet/testhost activity and waits up to 15 minutes; observed waits range **0.0–900.2 s**. Per-window process counts, top CPU-delta processes and before/after load are included. Windows that remain under external load are labelled by `underDotnetLoad`; no unrelated process was controlled. Per-process CPU deltas are not synchronized machine-percent measurements.

| Built / installed artifact | SHA-256 |
| --- | --- |
| Baseline PersistentCity.dll | `f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c` |
| Candidate PersistentCity.dll | `843b078085cbcdb5603ee4d082cbabdfd5c0cba3996203c2b3f2c74b73478336` |

[Build provenance and actual loaded-location checks](tested-builds.json). Both production builds used read-only agent1 Managed references and the per-user dotnet PATH prefix. No production source was patched. The initial quiet-checkpoint attempt encountered a bridge skip/pause timing race before measurement and quit normally; the driver wait was corrected. Initial observer compilation removed an unused sibling-only address seam, then native selection was corrected from an unowned default AI advertiser to an actual established rival. These were test-tooling corrections, not mod defects. The expected reload refusal initially stopped the wrapper, and native enumeration order required pinning the original eight shop IDs. Games quit normally; one owned stage controller was stopped after clean game quit before timing to correct its shortlist flow. The final 100-cycle/reload proof ran cleanly in one process during the paused load wait, then restored the identical quiet checkpoint. No candidate perf sample was repeated. A same-name bridge reload briefly returned stale readiness; the controller now observes loading before accepting city readiness. The exhausted-player quiet fixture caused a native hospital jump and pause, so that capture is explicitly excluded from normal-play clearance. Frame observer bytes remain identical across builds.

**Cleanup: restored and released.** All **4828/4828** original profile hashes and **107/107** install-reference hashes match; original ModsLocal, preferences/autosave, persistent-city.json, saves, install receipt, run metadata and Player log are restored. Root/preference hashes match the private originals. All owned games quit normally; no game kill was required. Lease is **held=false / pending=0**, agent1 stopped, and no owned game/build/measurement command remains. [Backup](backup-receipt.json), [root manifest](backup-root-manifest.json), [restore](restore-receipt.json), [lease release](lock-released.json), [process check](owned-process-cleanup.json), [locked session initial](session-initial.json) / [final](session-final.json). [Tooling corrections and owned-game exits](tooling-corrections.json) retain the preflight failures, controller correction and invalid quiet sample.

Deferred to Peter:

- Real rival price/hour UI change and corresponding observation/refusal.
- Real ownership/same-day replacement through normal gameplay, when feasible.

Recompute raw distributions and evidence hashes with `python verify_evidence.py`. Private saves, game DLLs, lease token, full native logs and profile backups are excluded from Git. Stage observer source is provided solely as test tooling; it is not production startup wiring.
