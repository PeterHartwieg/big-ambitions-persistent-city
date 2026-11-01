VERDICT: FAIL (candidate mod-work spike >=30 ms (maximum 36.048 ms))

Role: `rig_test`. Candidate `f214cd4` (PR #17) versus B0b control `c0af745` (PR #16). This is locked-session rig evidence; no merge, deployment, release, PR action or ordinary-save operation was performed.

Disposable fixture: `FT Food Routines Locked B1 20261101`, seed `-1903163055`, 957 residents / 1,914 consumers, agent1 only. [Final locked-session read](session-final.json).

All measured p95 values are below 5 ms. Every >=30 ms mod-work spike is retained below. The candidate has a flagged single-frame spike; this report does not clear that performance condition or attribute a regression to B1. Whole-frame timings remain informational.

| Check | Result | Evidence |
| --- | --- | --- |
| 1. Setup/provenance | PASS | [Frozen source/DLLs](provenance.json), [control checkpoints](control-checkpoints.json), [consumer IDs and traits](consumer-provenance.csv.gz), per-window config/checkpoint hashes in performance evidence. Original autosave enabled (5 min/3 slots); disabled and verified after loads and around every clean window. |
| 2. Loyal/curious cohorts | PARTIAL | [Genuine memory/choice observations](memory-summary.json): 59 matched consumers/group, represented slots included, four recovery days. One actual shop/generation and one seed; choice traces cap at 5,000 and report drops. Multi-shop novelty-frequency attribution is NOT-RUN. |
| 3. Shop UI failure gestures | DEFERRED-INPUT | Locked pointer/focus routing remains unavailable. Peter's playtest must cover separate shop-UI product/bag stockout, closure and queue gestures. Bridge seams do not prove gestures. |
| 4. Failure/recovery | PARTIAL | [Memory dumps and recovery](memory-summary.json). One confirmed stockout seam, no positive affinity or served gain; genuine queue failures and four recovery days. Stockout subject later suffered a queue failure and was not served in that follow-up. Default-retention expiry and actual business replacement/turnover are NOT-RUN. Capacity fixture history is synthetic and excluded from cohorts. |
| 5. Save/reload/config/cadence | PASS | [Checkpoint cases](checkpoint-summary.json), [first-unit continuity](load-continuity-summary.json). Selected/travelling/returning and interrupted journey preserve money/terms/memory; nonzero held-config result below. Reached/paid checkpoints on the initial journey were not observed; uncertain receipt and late-after-midnight confirmation NOT-RUN (none encountered). |
| 6. Full native day | PASS | [Exact native reconcile](native-day-summary.json): 18 purchases/receipts, 4,953,987,072,000 units, 18 croissants and 18 bags, zero pending completed orders and conservation failures. Reload and independent day replay exact. |
| 7. Pre-B1/capacity/refusal | PASS | [Real B0b in-flight migration](preb1-inflight-summary.json), [activation](migration-summary.json), [constructed capacity case](capacity-summary.json), [refusals](refusal-summary.json). Four synthetic historical entries retained for 18 consumers at capacity one; 18 actual native insertions replaced normally. Future schema/unknown revision preserve live and file bytes. |
| 8. Performance | FAIL | [Distributions, load, routes/queries, GC/allocations and every spike](performance-summary.json). Eight planned 60+ s samples, no extra clean-window reruns. |
| Player.log gate | PASS | [Reviewed log summary](log-summary.json). Intentional refusal messages and native game culling exceptions are separated. |

[Different-budget replay](budget-replay-summary.json) also preserves the target journey/outing, money, watermark and terms exactly; its resolved-save boundary was skipped, so the completed outcome is used rather than claiming that checkpoint was captured.

The first neutral evaluating context skipped route quotes, so its partial checkpoint was NOT-RUN. The additional positive context is reported separately. Actual first-unit snapshots compare loaded state before continuation rather than inferring it from a later result.

For `pc:390`, held contribution **0.07232066157962608** survives reload, event-budget replay and the unloaded weight-zero edit. The selected native shop/product stays identical; the next uncaptured context uses **0.0** on the same day. Exact target money, memory, watermark, journey and outing are preserved. [Structured evidence](checkpoint-summary.json).

| Window | Seconds | Mod p50 / p95 / max ms | Whole p50 / p95 / max ms | >=30 ms mod flags | Load |
| --- | ---: | --- | --- | ---: | --- |
| base1-idle | 60.220 | 0.435 / 0.642 / 22.758 | 11.239 / 12.821 / 40.425 | 0 | dotnet/testhost observed |
| base1-meal | 60.342 | 1.656 / 4.367 / 297.876 | 19.627 / 27.368 / 501.215 | 2 | dotnet/testhost observed |
| cand1-idle | 60.278 | 0.950 / 1.743 / 36.048 | 12.361 / 17.177 / 174.197 | 1 | dotnet/testhost observed |
| cand1-meal | 60.218 | 0.637 / 1.082 / 14.135 | 11.184 / 12.867 / 44.480 | 0 | dotnet/testhost observed |
| base2-idle | 60.092 | 0.413 / 0.710 / 12.980 | 11.166 / 12.808 / 42.008 | 0 | no dotnet/testhost at snapshots |
| base2-meal | 60.221 | 1.072 / 2.866 / 28.919 | 12.214 / 19.051 / 50.779 | 0 | dotnet/testhost observed |
| cand2-idle | 60.106 | 1.000 / 1.734 / 28.438 | 12.212 / 15.168 / 73.380 | 0 | dotnet/testhost observed |
| cand2-meal | 60.323 | 0.678 / 1.129 / 28.325 | 11.200 / 12.907 / 55.278 | 0 | dotnet/testhost observed |

Order is B0b -> B1 active -> B0b -> B1 weight zero, with idle/meal in each arm. Active and zero arms reload the same B1 native checkpoint; [all four input hashes match and only FoodMemoryWeight differs](matched-input-summary.json). Controls reload the same genuine B0b checkpoint; both derive from the same disposable FT source/seed, settings and clock. Model trajectories differ across builds; system load is uncontrolled, so these data do not establish a speedup or feature-cost percentage. Timing is Update/native-authority union plus same-frame whole PopulationRuntime.OnGUI; nested component times are not summed. Other hooks outside those scopes remain outside the gate measurement.

| Spike window/frame | Mod ms | Phase | Largest inclusive component |
| --- | ---: | --- | --- |
| base1-meal / 207090 | 297.876 | traffic-advance | MultimodalRoutes.PrepareInclusiveMs 78.421 ms |
| base1-meal / 207091 | 125.657 | traffic-advance | PhysicalTrafficHooks.FixedStepPostfixInclusiveMs 42.439 ms |
| cand1-idle / 129573 | 36.048 | traffic-advance | PopulationRuntime.AdvancePopulationInclusiveMs 34.820 ms |


| Window | CPU total start / after % | Dotnet / testhost / codex at start | Free RAM start GiB |
| --- | --- | --- | ---: |
| base1-idle | 40.4 / 31.4 | 2 / 0 / 8 | 29.36 |
| base1-meal | 71.2 / 26.8 | 2 / 0 / 8 | 21.01 |
| cand1-idle | 29.0 / 58.2 | 1 / 0 / 10 | 28.84 |
| cand1-meal | 22.2 / 29.6 | 5 / 1 / 10 | 25.81 |
| base2-idle | 33.7 / 29.2 | 0 / 0 / 8 | 25.29 |
| base2-meal | 91.2 / 52.3 | 1 / 0 / 12 | 21.0 |
| cand2-idle | 46.8 / 56.8 | 1 / 0 / 10 | 23.63 |
| cand2-meal | 28.6 / 31.3 | 5 / 1 / 12 | 30.27 |

Raw wall intervals, whole-frame stall counts, per-window CPU/top processes, dotnet/testhost/codex counts, RAM, route/query counts, GC and catch-up lag are in the performance JSON. Allocation counters that are unavailable or unobserved are not treated as zero allocations. [Recompute published evidence](verify_evidence.py).

| Saved state | Own UTF-8 bytes | Native gzip bytes | Core write ms | Native save observed wall ms |
| --- | ---: | ---: | ---: | ---: |
| Control18 | 5322121 | 1197676 | 288.601 | 594.0 |
| ControlDay9 | 5633911 | 1517293 | 312.096 | 578.0 |
| ActiveDay7 | 5692975 | 1421030 | 325.551 | 547.0 |
| ServedDay7 | 5683223 | 1508115 | 352.467 | 703.0 |
| TrainedDay9 | 9091714 | 2172235 | 465.835 | 812.0 |
| RecoveryFinal | 5619701 | 1618431 | 327.558 | 578.0 |
| LowerCapacity | 5687423 | 1419837 | 310.997 | 625.0 |
| CapacityInserted | 5682470 | 1505542 | 317.961 | 531.0 |

[Save/load observations](save-size-time.json) are single samples from required functional operations, outside timing windows. All >=30 ms save/load latencies are informational flags. Payload differences include trajectories, visits and financial history; this is not isolated memory growth. No offline dotnet test suite or additional save/perf-only run was executed.

Tested commits: candidate `f214cd4a88bb872f9e96233e9616f1bd941abe50`; control `c0af745b044d9c53d6e0b59f9fd12da0b61fc4fd`. DLL SHA-256: candidate `ce13dd20a574e46986438b8684350feee6335150aa468e9c311a9054585a4ecd`; control `34bbcf02fded476c79b58e84e3a005af38b979694435994e4f9220d2ce73a246`. Control built from an unchanged source archive; the reused control worktree was read-only. Loaded hashes/dependencies are recorded in [provenance](provenance.json).

Tooling corrections were confined to the private stage: a repeated Unity lookup in the batch diagnostic and an unload-coroutine/snapshot race required clean candidate restarts. Completed clean timing windows were retained, not rerun. Fixture staffing/stock changes and synthetic capacity history are labelled seams, not gesture or genuine historical-service proof.

Cleanup: PASS: all six owned game processes quit normally (exit0), no kill; 4,828/4,828 profile and 107/107 install hashes verified, original preferences/autosave/config/root hashes match, no owned game remains; lease released held=false/pending=0. See [restore/release evidence](cleanup-summary.json). Windows settings/session connection, other processes/worktrees, normal game/profile, Steam/Workshop, RLO source and production code were not changed. Only this evidence folder is committed.
