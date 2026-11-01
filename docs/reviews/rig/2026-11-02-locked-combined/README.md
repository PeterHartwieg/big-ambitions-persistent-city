VERDICT: FAIL — C4: one clean candidate frame at 30.9925 ms combined mod work; all p95 values pass.

Role: `rig_test`. Locked-session combined rig proof #2, requested evidence date 2 November 2026; actual owned rig run 1 November, approximately 16:03–18:12 UTC. Findings require follow-up fix PRs under merge rule v2. No production change, revert, merge, PR action, release or Workshop upload. Pointer-only proof remains DEFERRED-INPUT for 6 November.

Pinned candidate **`dbddada2e35d0ac2ac826985214741392a406364`**, recorded immediately after `git fetch origin`; control **`a7ab5bc51ffaabae28697efad7b35411553f03a7`**. Exactly these SHAs were tested. Final read-only remote check still named the same head; the candidate was never retargeted. Detached candidate/control builds succeeded with unchanged production source and the required dotnet PATH prefix. No offline dotnet test suite was run, as requested; build checks are separate from native evidence.

All 17 expected merges are present; **not in candidate: none**. Actual writer **23**; schema chain **18 →19(C2a) →20(E1) →21(D2) →22(C2b) →23(E4)**. [Exact first-parent log](scope.json), [provenance](provenance.json), [installed/loaded builds](loaded-builds.json). All named slice rig sections and PR descriptions were read before testing. Prior combined#1 tooling and genuine schema 18 checkpoints were reused by copying; raw saves remain private.

| First-parent commit | Slice / PR |
|---|---|
| `7053a28` | Step B1: shop memory capture and loyalty policy (stacked on #16) (#17) |
| `81472e6` | Step B2: regulars projection and inspector words (stacked on #17) (#19) |
| `3f7b885` | Step E3: first direct-stock food product feasibility record (read-only) (#23) |
| `193c81d` | Step E2b: bounded street-life timing and fail-closed venue ownership (#28) |
| `3178f3e` | Step E3: cupcake feasibility record + shared direct-stock reader (read-only, stacked on #23) (#33) |
| `692b8b5` | Step C2a: core bounded awareness sources and candidate interface (stacked on #16) (#24) |
| `1f8f4b4` | Step C4: bounded market-research engine and access contract, inactive (stacked on #16) (#30) |
| `51ce709` | Step E3: salad feasibility record on the shared direct-stock reader (read-only, stacked on #33) (#36) |
| `2f12ae9` | Track F1: NO-GO stop report for shop city view, feasibility proposal (docs only) (#54) |
| `5877453` | Step E1: rain in new outings and food decisions, schema 20 weather capture (stacked on #24) (#37) |
| `0ffb819` | Step A5: integrated acceptance, offline closure and perf report (#59) |
| `75c956c` | Step E3: icecream feasibility record on the shared direct-stock reader (read-only) (#62) |
| `2bd9006` | Step E4: bounded wider-menu framework, live limit four (schema 23) (#56) |
| `8c6b3f3` | Step E3: pizza feasibility record on the shared direct-stock reader (read-only) (#60) |
| `6909beb` | Step C2b: campaign awareness and targeting (schema 22) (#55) |
| `04b502c` | Step E3: frenchfries feasibility record on the shared direct-stock reader (read-only) (#66) |
| `dbddada` | Step D2: rival choice and own-ledger outflow (schema 21) (#58) |

| Loaded artifact | SHA-256 |
|---|---|
| Candidate PersistentCity.dll | `c521129592619b3e80ee101952c700e5b957c35e20a4aad6a15eb21992333603` |
| Control PersistentCity.dll | `36e79f5c6aa6819ebe6f247c37252dae4b7cf8af47b71a21049eef200b5a852d` |
| RealLifeOverhaul.dll 0.45.0.0 | `2f360a365cf7915f012cd62f4fd06214ef5cdaa0498331e05a7c831a3c20ab32` |

Harmony 2.4.2. Game assemblies were referenced in place under `C:\BigAmbitions-AgentTests`. No game DLL, decompiled code or credential is committed. Runtime provenance identifies all seven owned game launches and the loaded hashes.

| Check | Verdict | Result / evidence |
|---|---|---|
| C1 load/defaults | PASS | 957 residents/1059 places/1914 consumers, seed−1903163055. Viewoff, guardauto/not detected, research inactive, menu cap4, rain/campaign/rival defaults. All diagnostics available; no unexpected PC error. [diagnostics](diagnostics-summary.json), [logs](log-review.json). |
| C2 migration/refusal | PASS | Exact control18→23 +two stored reloads; old fields/IDs/traits/assignments/money/authority and57 receipts retained. [migration](migration-summary.json), [native contract](native-migration-contract.json), [refusal](refusal-summary.json), [open-shop refusal](refusal-open-summary.json). |
| C3 accounting/reload | PASS | Full native day279 purchases=9own receipts+270external outflows; exact spend/stock/bags/accounts. Ordinary journey/held native saves do not replay debit/checkout or lose return. [accounting](accounting-summary.json), [journeys](journey-summary.json), [held terms](held-summary.json). |
| C4 combined perf | FAIL | Six p95 values <5 ms; one >=30 ms candidate frame,30.9925 ms. [raw-backed timing/load/spike](performance-summary.json), [verifier](verify_evidence.py). |
| C5 save bytes/time | PASS | No ordinary save/load/migration error; numbers informational. First23nativeHSG+1444B versus1197683B reference. [save profile](save-profile.json), [operations](operation-latencies.json). |
| C6 per-slice items | PARTIAL | Native/read-only checks where reachable; all121 numbered items mapped below and in [coverage](slice-coverage.json). |
| C7 carry-over | NOT-RUN | No naturally late/uncertain midnight receipt, payment failure or95%size warning; never forced. Payload far below256 MiB cap. |
| C8 F geometry | PARTIAL | One small own shop/six matched captures; medium/large own/elevator fixtures absent. F1renderer/handoff BLOCKED. [geometry](geometry-summary.json), [guard](guard-summary.json). |

The one follow-up defect is **C4, suspected integration slice D2 / PR #58**, provisionally for triage because the new rival path is active in the window. **Causality is not isolated to that PR.** The measured slow region is shared `PopulationRuntime.Update`; most of its duration has no named component attribution. Minimal repro: pinned candidate/RLO above, disposable copied control schema 18 fixture, migrate and advance to day 7/noon with defaults,1920×1080 Low/90 FPS target/1x and autosaveoff; capture a60s lunch window after activation. Observed frame **13539**, `traffic-advance`, native minute **10817.0502739**, window `cand2`. This is an intermittent observed spike, not guaranteed reproduction or a p95 miss. Preserve this trace for a follow-up fix; no extra perf rerun taken.

That frame: Update/authority union30.8240 ms, Update30.8234 ms, authority0.0006 ms, OnGUI0.0039 ms, six disjoint hook bodies 0.1646 ms; final30.9925 ms. Named nested work is small: mod PhysicalPopulation.Update0.3549 ms, AdvancePopulation0.1521 ms, RefreshTrafficClosures0.1657 ms. Managed-used-heap delta−123,830,272B is consistent with collection, not proof GC/D2 caused it. Native traffic/physics bodies are excluded from mod hook timers; no evidence identifies a native game body as the cause. No dotnet/testhost at load samples; start CPU32.1%, free RAM29.861 GiB, codex6. The flag is retained as FAIL under the requested>=30 ms rule, without inventing a component cause.

C2 source: prior control native schema 18 checkpoint1,197,683B, SHA-256 `519e73ccbb83d9e46f65d190635f1638cdb8b47d94c28d68ac53a2b1e9e85db1`, source/copy identical. Source-key projection excluding schema has zero decoded differences; both stored reloads are exact. Third native write progresses a roughly third-minute tick: traffic/city clock and one event deadline differences are listed. Memory starts empty and B activates next day7; awareness/rival/weather starts7, campaign images empty, external-spend zero, menu policy4. Traits never rerolled. Corrupt and future99 copies refuse with original bytes preserved, also at an open-shop boundary with unchanged native cash/stock/orders.

C3 spans day 7 00:05→day 8 00:05: native time-machine/hour boundaries cover23 hours, ordinary play the final hour, all outside perf. Exact resident spend98,807,486,566,400 units=2,476,993,536,000 own receipt units+96,330,493,030,400 external units. Nine croissants/nine bags/nine receipts/history reconcile; native revenue92.2752 has expected binary32 bits `e78cb842`, zero pending completed orders, budgets conserve and native accounts reload exactly.270 rival choices settle once each. A passive observer of naturally called external payment records selected rival native registration/prices/hours/stock/orders/cash/finances/receipts unchanged. No rival native checkout or forced settlement/credit was invoked.

Slot-zero `pc:12` and represented `consumer:c339:1` use ordinary native outward/service/paid/return saves. Weightzero loaded while unloaded retains the same held external payment/return. Slot-zero normal1x tail restores original home and clears outing/journey; represented excursion clears without a second body. An earlier6x tail retimed the return and is separately retained, not claimed as arrival. Genuine prior schema 18 partial-quote consumer `pc:350` keeps old taste0/household0.1 despite live1/1 versus3/5 edits; both arms buy one croissant for275,221,504,000 units with one receipt. Fresh edited decision wire/all quote cursors were not caught and remain unproved.

A copied test observer initially requested native save inside an unfinished Core unit. PC protectively refused the incoherent traffic/city clocks and retained the last valid population payload unchanged. That invalid test checkpoint is excluded from ordinary-save proof. The observer was disabled; subsequent accepted checkpoints use ordinary caught-up paused saves plus an ordinary running1x save/reload. [Tooling refusal/correction](checkpoint-tooling-refusal.json). No production change. Deliberate corrupt/future refusal logs are expected. Native Music/Hangout culling exceptions also occur on control; candidate long soak additionally has StreetLamp coroutine exceptions without a PC stack, with no matched control soak for that signature. [Log classification](log-review.json).

Performance uses six original windows only: control idle→candidate idle→control lunch→candidate lunch, then candidate optional-stops100 idle/lunch. Same fixture/seed/body limits/graphics/native pace/camera; each about20 native minutes. The idle pair is day 6 before next-day feature activation; day 7 lunch/100 windows cover active integration. This does not prove all-features-active idle cost or isolated slice deltas. No per-slice/panel-open/reader-active perf matrix added. Autosaveoff verified before/after each and after reload; no save activity/inventory change during any window. Actual owned menu is croissant only, configured live cap4.

Final mod work sums per-frame Update/native-authority **union**, whole OnGUI, and six disjoint mod traffic/intersection hook bodies, following prior F0 tooling. All added calls report outside the primary scope; nested Update components are not summed. Interim30.8279 ms covered union+GUI; final30.9925 ms includes existing raw hook spans without rerunning. [Verifier](verify_evidence.py) recomputes distributions/spikes and file hashes. Native whole-frame intervals/stalls are informational. Wall-clock spans include scheduling/GC and do not establish CPU attribution or exhaustive coverage of uninstrumented callbacks.

| Window / state | Seconds | Mod p50 / p95 / max ms | Whole-frame p50 / p95 / max ms | Whole >50 / >100 / >250 ms |
|---|---:|---:|---:|---:|
| base1: control day6 idle | 60.162 | 0.3563 / 0.4302 / 3.4131 | 11.1199 / 12.4550 / 69.8367 | 1 / 0 / 0 |
| cand1: candidate day6 idle | 60.168 | 0.4290 / 0.6062 / 12.0948 | 11.2017 / 12.8145 / 70.4735 | 2 / 0 / 0 |
| base2: control day 7 lunch | 60.104 | 0.9622 / 1.3406 / 19.5753 | 11.2690 / 13.0917 / 31.9944 | 0 / 0 / 0 |
| cand2: candidate day 7 lunch | 60.269 | 0.9656 / 1.3314 / 30.9925 | 11.1972 / 13.2149 / 43.7850 | 0 / 0 / 0 |
| cand100idle: candidate100 day7 idle | 60.165 | 0.3090 / 0.3680 / 3.3994 | 11.1637 / 12.5150 / 32.3429 | 0 / 0 / 0 |
| cand100rush: candidate100 day 7 lunch | 60.221 | 1.0054 / 1.3988 / 25.9830 | 11.2744 / 12.9484 / 38.9759 | 0 / 0 / 0 |

Hardware: i5-13600KF/RTX4070/63.837 GiB RAM, D3D11,1920×1080 Low, vSync0,target90 FPS,1x/calendarfactor1/3, physical48 pedestrians/24 vehicles. Unity reports focused=True despite the locked desktop; no activation attempted. Camera transforms/lag/GC retained per window. Allocation-byte counter/peak-working-set support is limited; unavailable precision is not invented.

| Window | Start CPU% | Sample CPU range% | Start free GiB | dotnet / testhost / codex at start | GC delta0/1/2 |
|---|---:|---:|---:|---:|---:|
| base1 | 26.7 | 22.6–26.7 | 28.497 | 0 / 0 / 8 | 2 / 2 / 2 |
| cand1 | 24.1 | 21.2–24.1 | 25.639 | 0 / 0 / 8 | 2 / 2 / 2 |
| base2 | 30.7 | 25.3–30.7 | 29.396 | 0 / 0 / 6 | 2 / 2 / 2 |
| cand2 | 32.1 | 21.6–32.1 | 29.861 | 0 / 0 / 6 | 3 / 3 / 3 |
| cand100idle | 15.1 | 7.6–19.0 | 30.035 | 0 / 0 / 6 | 2 / 2 / 2 |
| cand100rush | 28.0 | 25.9–28.0 | 30.098 | 0 / 0 / 6 | 2 / 2 / 2 |

All snapshots have dotnet/testhost0. First coordination waited60s for test processes to finish, later windows no wait. Background work remained recorded; no p95 miss needs an “over gate under load” exception. Total CPU/top-process CPU deltas/RAM/counts before/start/after are in [performance evidence](performance-summary.json). Snapshots do not exclude short-lived external processes; top rankings are CPU deltas, not synchronized per-process percentages. Whole-frame69–70 ms idle stalls are separate from mod-work spikes.

| Frozen codec operation (3 samples, median) | Source schema | UTF-8 bytes | Write ms | Read/migration ms |
|---|---:|---:|---:|---:|
| base-reference-profile | 18 | 5,322,121 | 288.264 | 592.630 |
| candidate-migration-profile | 18 | 5,344,209 | 321.341 | 1541.849 |
| candidate23-profile | 23 | 5,344,209 | 326.089 | 655.542 |

| Native compressed save | Bytes |
|---|---:|
| FT Food Routines Locked Combined 20261102 | 1,197,683 |
| FT Food Routines Locked Combined Schema23 20261102 | 1,199,127 |
| FT Food Routines Locked Combined Schema23 Roundtrip 20261102 | 1,199,133 |
| FT Food Routines Locked Combined Schema23 Twice 20261102 | 1,199,129 |
| FT Food Routines Locked Combined CandidateDayStart23 20261102 | 1,473,317 |

| End-to-end bridge operation, outside windows | Seconds |
|---|---:|
| base1-matched-wall | 28.735 |
| migration-wall | 31.875 |
| reload1-wall | 29.781 |
| reload2-wall | 30.203 |
| candidate23-wall | 0.703 |
| candidate23-roundtrip-wall | 0.563 |
| candidate23-twice-wall | 0.547 |

Schema18→23 frozen payload grows22,088 UTF-8B; first nativeHSG+1,444B versus combined#1 reference 1,197,683B. Native compression/ticks vary; CandidateDayStart contains later trajectory growth, not schema-only overhead. Codec timing excludes scene loading; bridge load includes scene/preflight/wait. Full native control save duration was not remeasured (copied hash-identical checkpoint); control codec write/read and matched load are reported. Every save/load/export/diagnostic operation>=30 ms is listed separately in [operations](operation-latencies.json), outside normal-frame windows. Peak working-set API returned0/unavailable; no peak-memory claim.

All six E3 readers returned missing/ambiguous advertised-price refusal and Unknown eligibility on an existing registration, matching advertised fixtures0.100repeated/interleaved paused reads per reader preserve exact native/payload hashes across rounds; no conditional equipment/direct-stock/sale approval.100absent-campaign reads also preserve native/payload. Same-candidate native save/reload has equal5,782,625B lengths but7geometry/derived-time differences already present before reader calls; literal replay equality remains PARTIAL. Schema18/23 literal equality would not isolate read-only slices. C4 has no production research command/job/cache/input reader/scheduler; fixed bridge only, no eval command added.

B2’s genuine archived B1 native recovery save, excluding synthetic capacity fixtures, gives4 slot-zero+4 represented positive regulars on this head. All8project once, memory hashes/counts survive save/reload and repeated diagnostics preserve native/payload. `c154` popup says a regular at 30 3rd Street; represented `consumer:pc%3A9:1` remains separately labelled/no body, while `pc:9` has no inherited regular story. Four-day current soak cannot establish a fresh3-day cohort: shop serves8on day7 then reports no staffed counter on days8–11. No staff/bags/money injected. Prior B1 checks1/5/6/7PASS reused rather than repeated standalone; integration checks above do not close its multiple-seed recovery/generation edges.

C2a96 consumer×8day read batches find two eligible player discoveries beyond every250m anchor in the ordinary shortlist (`c111`day9, represented`consumer:c120:1`day7); admission is not sale. Native trace separately records rival winner/player competition. Active campaign fixture absent: no purchase/fee edit/mask/probability proof. C2b design uniformly gives every resident1.25preference/half soft-distance input; no demographic mapping/targeting boost.

Native day7–11 soak: dry/rain/dry/weather capture,723 cached Work/Shop/Leisure frontages, no actual rebind. Chance conservation940 draws=1099 realized−159 initialpending, finalpending 0/drops0. Logical stop records include future/cancelled items, separate from physical caps; pending limit957is not48/256fixture variants.84genuine future-break ends/continuations survive reduced maxima unchanged, all exceeding new5 min bound.0/minimum100 short arms had no new eligible opportunity, verifying settings/empty ledger only. Bound-line rain move-up, same-break transient rain, nightclub overlap, dense-origin, largest menu, active campaign, unsupported edit and large-city proof remain unobserved.

All 121 numbered slice checks follow. PASS applies to the stated current combined scope; PARTIAL keeps missing subcases. Perf rows reuse the one C4 failure, not separate defects. Isolated historical writers superseded by integrated23 are qualified. [Coverage/source-section hashes](slice-coverage.json).

| Slice / item | Verdict | Evidence / limit |
|---|---|---|
| b1-memory-loyalty 1 | PASS | Frozen fixture/build/settings/autosave/restore. Prior same-code standalone PASS reused. [evidence](provenance.json) |
| b1-memory-loyalty 2 | PARTIAL | Current9 memories and archived real served-day cohort project8 regulars; no multi-seed loyal/curious frequency proof. Later shop lacks staffed counter. [evidence](b2-retained-summary.json) |
| b1-memory-loyalty 3 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| b1-memory-loyalty 4 | PARTIAL | Genuine archived native recovery read on candidate; no current prepared failure/recovery/generation-turnover sequence. [evidence](b2-retained-summary.json) |
| b1-memory-loyalty 5 | PARTIAL | Integration partial quote preserves old tuning despite unloaded edit; one native receipt. Journey replay passes. No late receipt/every cursor/fresh edited capture; prior standalone PASS retained. [evidence](held-summary.json) |
| b1-memory-loyalty 6 | PASS | Integration full native day exact money/receipts/stock/bags/accounts/reload; prior standalone PASS retained. [evidence](accounting-summary.json) |
| b1-memory-loyalty 7 | PASS | Combined18->23 retains old data, empty memory then next-day activation; corrupt/future refusal preserves bytes. Prior capacity proof reused, not repeated. [evidence](migration-summary.json) |
| b1-memory-loyalty 8 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| b2-regulars-inspector 1 | PASS | Frozen native archived cohort/default3visits14days/autosave/restore. [evidence](provenance.json) |
| b2-regulars-inspector 2 | PARTIAL | 4 slot-zero+4 represented current regulars from genuine3/5 served counts/positive affinity; no new1/2/3-day or14/15/expiry progression. [evidence](b2-retained-summary.json) |
| b2-regulars-inspector 3 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| b2-regulars-inspector 4 | PARTIAL | Repeated diagnostics/popup preserve exact native/payload; positive words and own/represented separation. No freshly prepared stockout transition. [evidence](b2-retained-summary.json) |
| b2-regulars-inspector 5 | PARTIAL | 8 regulars and memory hashes stable native save/reload; no actual generation replacement/continuation-budget variants or isolated B1/B2 byte comparison. [evidence](b2-retained-summary.json) |
| b2-regulars-inspector 6 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| b2-regulars-inspector 7 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| b2-regulars-inspector 8 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| c2a-core-awareness 1 | PARTIAL | 96 consumers x8days, exact payload, union<=32/shortlist<=8 and actual traces<=16routes; dense-origin/draw0 fixture absent. [evidence](awareness-batch-summary.json) |
| c2a-core-awareness 2 | PASS | 2eligible PLAYER discoveries beyond every250m anchor enter ordinary shortlist; consumer/day/revision/positions/distances retained, no sale inferred. [evidence](awareness-batch-summary.json) |
| c2a-core-awareness 3 | NOT-RUN | Matched price/stock/deadline fixture conditions absent, no forced edits. [evidence](rival-competition.json) |
| c2a-core-awareness 4 | PARTIAL | Absent campaigns/initial empty memory observed; old isolated unknown-reserved-slot condition superseded by integrated C2b. [evidence](diagnostics-summary.json) |
| c2a-core-awareness 5 | PARTIAL | C2 covers18->23/two reloads/held old terms; isolated writer19 superseded. Every cursor/new edited context not caught. [evidence](migration-summary.json) |
| c2a-core-awareness 6 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| c2a-core-awareness 7 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| c4-market-research 1 | PASS | Public API/state enumerate no research command/job/cache/reader/scheduler. Fixed-command bridge only; no eval command added or payment/report execution. [evidence](diagnostics-summary.json) |
| c4-market-research 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| c4-market-research 3 | PARTIAL | Integrated23 cannot literally equal18. Direct paused reads exact; candidate native replay has7geometry/time differences present before reads, no research fields. [evidence](reader-save-control.json) |
| c4-market-research 4 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e2b-street-life 1 | PARTIAL | 723cachedWork/Shop/Leisure frontages, Home/Study absent. No destroyed-controller rebind or ambiguous native fixture. [evidence](soak-summary.json) |
| e2b-street-life 2 | NOT-RUN | No naturally captured bound queuer with vacant forward slot during rain. [evidence](soak-summary.json) |
| e2b-street-life 3 | PARTIAL | 0/25/100 and default/minimum settings read; chance ledger conserves.84genuine future-break ends/continuations retained under lower tuning. No new opportunity in0/minimum short arms. [evidence](optional-held-replay.json) |
| e2b-street-life 4 | NOT-RUN | No same-break natural rain-onset/clearing-before-start timeline. [evidence](soak-summary.json) |
| e2b-street-life 5 | NOT-RUN | No independently identified native-nightclub/managed-stop simultaneous ownership fixture. [evidence](soak-summary.json) |
| e2b-street-life 6 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e2b-street-life 7 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e1-rain 1 | PARTIAL | Native dry/rain/dry and captured weather/settings observed; no matched causal probability/distance mix or failed-source arm. [evidence](soak-summary.json) |
| e1-rain 2 | PARTIAL | Native soak/reload preserves active population; individual work/queue/paired transition matrix not captured. [evidence](soak-summary.json) |
| e1-rain 3 | NOT-RUN | No genuine held-E1-weather checkpoint with changed rain/tuning; legacy held replay is separate. [evidence](held-summary.json) |
| e1-rain 4 | PASS | C2 covers18 through19/20/21/22/23, next-day weather start7 and preserved refused sources; standalone19 step omitted as instructed. [evidence](migration-summary.json) |
| e1-rain 5 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e1-rain 6 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| a5-integrated-acceptance 1 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| a5-integrated-acceptance 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| a5-integrated-acceptance 3 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| a5-integrated-acceptance 4 | PARTIAL | Semantic popup payload/routes/market unchanged, formatted addresses/identity; B2positive/represented controls. Pointer camera/pool subset deferred. [evidence](diagnostics-summary.json) |
| a5-integrated-acceptance 5 | PARTIAL | Traits/Today/own and represented words/unknown meals read; active B/regular evidence. Not every meal/generation case available. [evidence](b2-retained-summary.json) |
| a5-integrated-acceptance 6 | PARTIAL | 408native businesses, traced rival/player bounded competition <=8shortlist/16routes. Continuation-budget matrix not repeated. [evidence](rival-competition.json) |
| a5-integrated-acceptance 7 | PARTIAL | Genuine legacy partial quote/one purchase and rival outward/service/paid/return plus weightzero replay. Not every cursor/red baseline/fresh tuning image. [evidence](held-summary.json) |
| a5-integrated-acceptance 8 | PASS | C2 integration18->23/two reloads stable IDs/traits/money/old graph/authority and source refusal. Older16/17 steps omitted from this combined scope. [evidence](migration-summary.json) |
| a5-integrated-acceptance 9 | PARTIAL | Chance ledgers/reloads/0/25/100/held stops observed; no unsafe UI edits or uncertain/late receipt. [evidence](optional-functional-summary.json) |
| a5-integrated-acceptance 10 | PASS | Full native day9own+270external reconciles, ordinary interrupted native saves do not replay purchase/debit or lose return; rival fields exact. [evidence](accounting-summary.json) |
| a5-integrated-acceptance 11 | PARTIAL | Reference957/1914 consumers,4 day soak/reload; no existing authorized5k/ceiling fixture used. [evidence](soak-summary.json) |
| a5-integrated-acceptance 12 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e4-wider-menu 1 | PARTIAL | Live cap4 confirmed; actual own menu only croissant. Authority/shared pool/accounting read; largest multi-offer fixture absent. [evidence](diagnostics-summary.json) |
| e4-wider-menu 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e4-wider-menu 3 | NOT-RUN | Multi-offer/product/shared-shortage fixture missing. [evidence](diagnostics-summary.json) |
| e4-wider-menu 4 | PARTIAL | Supported croissant exact debit/bag/receipt/day accounts and legacy-held upgrade; no largest-menu/shortage matrix. [evidence](held-summary.json) |
| e4-wider-menu 5 | NOT-RUN | Unsupported UI menu edit/restoration fixture not prepared. [evidence](diagnostics-summary.json) |
| e4-wider-menu 6 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| c2b-campaign-awareness 1 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| c2b-campaign-awareness 2 | NOT-RUN | No already-active campaign fixture; acquisition-hour masks/observer error not exercised. [evidence](diagnostics-summary.json) |
| c2b-campaign-awareness 3 | NOT-RUN | No native active/stopped pair. Design uniform1.25 preference/half soft distance, no targeting/demographics; no campaign purchase. [evidence](diagnostics-summary.json) |
| c2b-campaign-awareness 4 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| c2b-campaign-awareness 5 | NOT-RUN | Active/stopped/ineligible/retired campaign fixture absent. [evidence](diagnostics-summary.json) |
| c2b-campaign-awareness 6 | PARTIAL | 100absent-campaign snapshots preserve native/payload/empty outputs; no running masks or daily fee proof. [evidence](reader-summary.json) |
| c2b-campaign-awareness 7 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| d2-rival-outflow 1 | PASS | Actual rival winner with player competitor in same bounded shortlist; estimated supply/service/exact price; not a share estimate. [evidence](rival-competition.json) |
| d2-rival-outflow 2 | PASS | Slot0/represented outward/service/paid/return native boundaries; slot0 normal1x home/clear and represented clear, no fabricated body. [evidence](journey-summary.json) |
| d2-rival-outflow 3 | PARTIAL | One exact external debit/count per consumer and conservation/no replay; arrears/hold/future-essentials refusals absent. [evidence](journey-summary.json) |
| d2-rival-outflow 4 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| d2-rival-outflow 5 | PARTIAL | Outward/service/paid/return native reload and weightzero pass; old player partialquote control passes. New D2 every-cursor/changed-terms fixture not captured. [evidence](journey-summary.json) |
| d2-rival-outflow 6 | PASS | Passive observer of natural debit keeps selected rival registration/cash/finance/receipts exact; player stock/bag/order/day control exact. No forced settlement/credit/checkout. [evidence](journey-summary.json) |
| d2-rival-outflow 7 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| d2-rival-outflow 8 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| f1-shop-view 1 | PARTIAL | One small own shop30 3rd Street geometry/authority/caps. Medium/large own/elevator prepared fixtures missing. [evidence](geometry-summary.json) |
| f1-shop-view 2 | PARTIAL | Six matched native doorway off/on captures30/60/120; F1drawing/depth/lighting BLOCKED absent renderer. [evidence](geometry-summary.json) |
| f1-shop-view 3 | BLOCKED | F1renderer/indoor customer hand-off absent by design; F0active/body retention not proof. [evidence](geometry-summary.json) |
| f1-shop-view 4 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| f1-shop-view 5 | PARTIAL | Prepared transitions/inside save-reload captured; reload uncertain/outside-first preflight. Not full scene/disable/fast-time/elevator matrix. [evidence](geometry-summary.json) |
| f1-shop-view 6 | PASS | Defaultnot detected; own harmless stand-in forces viewoff after cityload;100 cached reads exact, no real third-party implementation. [evidence](guard-summary.json) |
| f1-shop-view 7 | NOT-RUN | Optional F0interior performance omitted; only six required C4 windows. [evidence](performance-summary.json) |
| e3-donut 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-donut 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-donut 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-donut 4 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-donut 5 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e3-cupcake 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-cupcake 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-cupcake 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-cupcake 4 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-cupcake 5 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-cupcake 6 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e3-salad 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-salad 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-salad 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-salad 4 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-salad 5 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-salad 6 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-salad 7 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e3-icecream 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-icecream 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-icecream 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-icecream 4 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-icecream 5 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-icecream 6 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-icecream 7 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-icecream 8 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e3-pizza 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-pizza 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-pizza 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-pizza 4 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-pizza 5 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-pizza 6 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-pizza 7 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |
| e3-frenchfries 1 | PARTIAL | Existing registration refuses missing/ambiguous advertised price with Unknown eligibility; matching fixture0, conditional equipment/direct-stock approval withheld. [evidence](reader-summary.json) |
| e3-frenchfries 2 | DEFERRED-INPUT | Real pointer/focus/fixture UI preparation or visual interaction requires unlocked6Nov playtest. [evidence](diagnostics-summary.json) |
| e3-frenchfries 3 | NOT-RUN | Advertised product/equipment/stock fixture absent; no product-specific natural sale/settlement or synthetic cargo/customer. [evidence](reader-summary.json) |
| e3-frenchfries 4 | PARTIAL | 100paused repeated/interleaved reads exact native/payload; matched native reload has7geometry/time differences already present before reads. Literal replay equality not claimed. [evidence](reader-save-control.json) |
| e3-frenchfries 5 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-frenchfries 6 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-frenchfries 7 | PARTIAL | Shared readers100x refusal/Unknown/native-payload preservation; advertised regression fixtures absent. [evidence](reader-summary.json) |
| e3-frenchfries 8 | FAIL | C4 six combined windows only: p95 passes, one >=30 ms flag. Slice-specific variants omitted as instructed. [evidence](performance-summary.json) |

C8: **30 3rd Street**, ground-floor size`ba:buildingsize_a`,version1, business generation1/from122. Exterior door position`(230.262573,0.09000015,21.27832)`, quaternion`(0,-0.707106769,0,0.7071068)`,scale1. One interior exit`-42733408`,position`(999,0,-4.43)`,no elevator; cached floor y0/layout origin`(1000,0,0)`. Unique door candidate by one-to-one cardinality; no proven trigger/transform correspondence. Effective radius30/60/120m, admission20/50/110m, caps 24/12 within 48/24global, observed bodies 0in all six captures. Full transforms/authority/cached eligibility/lighting: [geometry](geometry-summary.json).

| Prepared doorway | Viewoff | Viewon |
|---|---|---|
| Radius30m | [off30](geometry-off-30.jpg) | [on30](geometry-on-30.jpg) |
| Radius60m | [off60](geometry-off-60.jpg) | [on60](geometry-on-60.jpg) |
| Radius120m | [off120](geometry-off-120.jpg) | [on120](geometry-on-120.jpg) |

Visually inspected Unity captures, normal visible owned window, day 6 10:03/matched graphics/lighting;1280×720JPEG derivatives. Interior doorway nonblack, exterior dark consistent with absent F1drawing. F0viewactive/HiddenFalse is not F1proof; no radius/occlusion/shadow correctness inferred. Screenshot FPS overlays are diagnostic, not C4. Inside-save/reload records uncertain/outside-first preflight fallback. Default guardnot detected; harmless own stand-in after cityreload forces viewoff/HiddenTrue/no bodies;100 cached reads exact. Medium/large OWNshop/elevator prepared checkpoints missing, other businesses not substituted. **F1drawing/customer hand-off BLOCKED: absent on this head.**

DEFERRED-INPUT for Peter’s6November playtest: real hover/highlight/click/pool/camera follow and stable selection; map address-link clicks; popup drag/release/right-click/wheel versus camera/HUD/dialog/focus routing; panel hotkey/freshness/visual regular/stockout words; normal UI setup of missing E3/menu/campaign fixtures, equipment/stock/bag/closure/unsupported-menu edits; rival price/hour/ownership edits; visible dry/rain/paired/queue/outward/service/return continuity; unprepared window/camera/elevator viewpoints. No pointer motion/activation/arbitrary click/wheel/WGC attempted. Missing fixture-dependent downstream checks remain NOT-RUN; campaign purchase or money changes were not performed.

Cleanup **PASS**: seven owned games quit normally exit0, gamekills0. Only two explicitly superseded own checkpoint Python observers were stopped, not because of elapsed time. No unrelated process touched.4,828 original profile file hashes and107install references match; root agent-install/run/Playerlog and native game preference export hashes match. ModsLocal/config/autosave restored by full ledger: autosave enabled,`preventAutoSave=false`,5 min/3slots. No owned game/driver remains. Lease released18:12:29UTC,heldfalse/pending 0; final read-only session11flags0 confirms still locked. [Restore/hash/lease receipt](cleanup.json), [owned-process check](owned-process-check.json), [initial](session-initial.json), [final lock](session-final.json). Normal profile/Steam/RLOsource/Windows settings/session untouched. Registry backup/restore covered only the isolated game preference key required by rig cleanup.

Only this report folder is committed on `codex/pc-rig-locked-combined-20261102`; no PR opened. [Manifest](manifest.json) hashes published files except itself. Evidence excludes raw saves/DLLs/full logs/secrets/personal paths; raw backup/checkpoints/tooling remain private in the owned stage. This is not full release proof or ordinary-save approval.
