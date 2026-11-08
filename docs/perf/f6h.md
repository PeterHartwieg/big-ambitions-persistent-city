Status: implemented (offline); not verified by rig

Refs #93. Base `3c5ce3b4`, writer 33. **Price calibration remains stopped on the tradeoff gate.** The independent F6h cadence, body-cap, posting/advancement stress and current section 6 offline measurements are now implemented and recorded below. These measurements do not approve price defaults or native performance.

## New experiment

Foreground Mac arm64, .NET 10 Release, one dotnet process at a time. `tools/economy-interventions --integrated` runs 17 arms × five seeds × 42 days at 957 residents plus 957 independent F5 visitors. All seven section 3.5 families run with wages, ordinary admission/spending, rival constraints, meal-period causes and observation journal enabled. The data and acceptance failure are in [calibration](f6h-calibration.md). `F6hIntegratedTests` contains the initial six fast representative cases plus the new fast cadence/budget assertion in the default suite; the long experiment remains an opt-in tool. The acceptance-reader tests take about a millisecond and launch no dotnet process.

The initial complete sweep took about **4.5–5.1 seconds per six-week run** after the first warm run (**7.4 seconds**), roughly **0.75–0.85 seconds/simulated week**. These rounded console observations include fixture construction, native doubles, decision tracing, daily all-owner accounting checks and the final detached save hash. They are **tool wall time**, not per-frame mod-work. They have no component spike attribution or allocation measurement. A final complete sweep repeats the experiment after descriptive metric-label corrections; the retained machine summary carries its per-run metrics hashes. The optional bounded price-weight diagnostic is a separate run, not a deployed default.

No Core or Runtime performance optimization is taken. There is no claimed before/after product win. Physical population, closure refresh, optional-stop refresh and save serialization stay parked; #128 whole-frame stalls are not investigated.

## Full section 6 table: before-values and current summary

The historical column retains before-values from the merged slices. The current column and detailed tables below use fresh measurements of writer 33 and the current F6 code. Native getters, controller memory, Mono and the unified mod-work union remain separate batched-rig evidence.

| Section 6 work and sizes | Historical before-values | F6h current measurement |
| --- | --- | --- |
| Rival arrival/completion; 1,914/10,000/100,000 consumers | [F6b](f6b.md): two lanes p95 0.0001/0.0001 ms, 0 B/op; eight lanes/16 serving p95 0.0007/0.0001 ms, 0 B/op. These are load microbenchmarks, not three full integrated size arms. | All three sizes × two/eight lanes: arrival p95 0.0005–0.0009 ms; completion p95 ≤0.0001 ms; 0 B/op; no ≥30 ms. Detailed per-size maxima below. |
| Rival reconstruction; 1,914/10,000/100,000 consumers and 20,000 rivals | [F6b](f6b.md): p95 0.0805/0.3226/17.7817 ms; 12,976/233,448/4,555,304 B/rebuild; 3,570,344 B retained at 20,000 rivals. | Current p95/max 3.3755 / 3.9993 / 19.2995 ms at the three sizes (cold detached rebuild); zero ≥30 ms. 20,000-rival retained bytes below. |
| Requested/hidden F6c/F6g panel; 1/64/1,024 shops × 1,914/10,000/100,000 consumers | [F6c](f6c.md): old consumer scans p95 1.1088/3.4898/32.1975 → counters 0.0026/0.0050/0.0030 ms; old all-shop text at 1,024 shops p95 14.5748 ms. [F6g](f6g.md): final paged panel p95 0.033667/0.706292/2.357084 ms, hidden 0 builds/0 B. | Full nine-case consumer/shop matrix recorded below; all hidden cases 0 calls/0 B. Native layout/read timing remains rig work. |
| Wage accrual and ≤32-posting units; 1,914/10,000/100,000 consumers | [F6e](f6e.md): settlement p95 0.000250 ms, 0 B; posting p95 0.005916/0.006792/0.011792 ms, 0 B/unit. F3 references 0.0208/0.0229/0.0111 ms. | Current settlement p95 0.000209 ms; posting p95 0.008500 / 0.010625 / 0.012708 ms, 0 B/unit. Seven-day integrated stress at all three sizes below. |
| Ordinary admission/debit/owner lookup; 1,914/10,000/100,000 consumers | [F6f](f6f.md): gate p95 0.0001 ms, wallet lookup+gate 0.0001/0.0001/0.0002 ms; debit+dated counters 0.0003 ms; 0 B steady state. | Current gate/debit and per-size wallet lookup recorded below; 0 B steady state. No native payment is modeled. |
| Hourly ordinary ownership refresh; 1/1,024/20,000 places, ≤16/unit | [F6f](f6f.md): managed callback unit p95 <0.0001/0.0008/0.0010 ms, 0 B; retained 2,072/185,728/1,843,464 B. Bound ≤2 native owner-member reads/place, ≤32/unit; actual native getter time unmeasured there. | Managed callback unit p95 0.0001 / 0.0010 / 0.0011 ms, 0 B; exact read counts and retained metadata below. Native getter latency remains rig work. |
| Journal change detection; 1/64/1,024 shops, ≤8 offers | [F6g](f6g.md): unchanged one-offer supply p95 0.000125/0.000125/0.000125 ms, 0 B; changed supply 0.001166/0.001333/0.001542 ms; changed hours 0.005875/0.006083/0.008916 ms. Matched headless market read counts equal. | Fresh one/eight-offer supply, changed-supply and hours rows at all three shop sizes below; unchanged observations 0 B; native read equality remains guarded and batched. |
| Save/read, separate; 957/5,000/50,000 residents × factor 2/3, CF32/CF64 | [F6d](f6d.md): all size-matrix bytes/records and cold read/write latency; CF32 composed 214,047,318 chars/2,279,079 records. [F6e](f6e.md): fresh factor-2 write p95 110.936/181.754/1687.388 ms, read 279.747/389.238/3780.691 ms. [F6g](f6g.md): writer-33 observed 225-person fixture 198,523 chars/858 records; write/read p95 4.747792/14.840833 ms. | Complete current eight-fixture save/read matrix below, with current bytes, records and flags; separate from play timing. No codec, cap or golden-pin changes. |

## Retained ≥30 ms flags and whole-frame boundary

Historical flags remain flagged: [F6b's merged-checks rerun](../checks/f6b.md) for the 100,000-consumer attach reached **38.5022 ms** while other worktrees shared the host; quieter reruns did not erase it. F6c's then-current 1,024-shop all-text panel had **1/200 at 34.2305 ms**, before F6g pagination; removed scans/lookups had 45/200 and 200/200 ≥30 ms. F6g's final paginated samples had zero ≥30 ms. These are different builds and fixture shapes, not a matched F6h before/after experiment.

F6d's 96/96 measured bulk save/cold-load operations and F6e's 30/30 fresh-city writes/reads reached 30 ms and are retained as save-path flags, outside ordinary mod-work. Current bounded advancement samples and component rows are recorded below; no native unified mod-work p95 is inferred from them. **Whole-frame cost is unmeasured separately**; no inference is made from headless tool runtime. The standing gate stays mod-work p95 <5 ms with every ≥30 ms sample flagged/attributed. There is no new autosave setting in a headless run. The final rig list in [checks](../checks/f6h.md) requires original-setting capture, verification off before each native window and after reload, invalidation on any save inside a window, and cleanup restoration.

## Initial implementation validation (b808c7c)

- Six new C# integrated seam/reload/accounting-fault checks: **6 passed** (test execution about 1.1 s).
- Four Python acceptance-reader checks: **4 passed**.
- Initial 85-run six-week sweep: **6,832,980** daily owner conservation checks, zero duplicate receipts, zero HistoricalClockMisses, exact native-double receipt and outside-watermark reconciliation. Acceptance reader exits **1** on the deliberately retained dominant-menu failure.
- Final 85-run sweep: all economic values exactly reproduce the initial sweep; descriptive rival labels were corrected and the final 85 metric hashes are retained. Repeated tool runtime is about **4.8–5.6 s/run** after the **7.2 s** first warm run (about **0.80–0.93 s/week**); still tool cost only.
- Bounded-price diagnostic: **20 runs**, **1,607,760** daily owner checks; the same +50% menu criterion remains red at the existing maximum price weight **3**. Final diagnostic tool runtime about **4.9–5.3 s/run** after warmup.
- Required build: **passed** using the supplied external Managed directory. DLL SHA256 `467e9f0f97c772e56c26415ec331e30e43eec170d1ff023f812aa63bf84660c1`.
- Required foreground `dotnet test tests/PersistentCity.Tests.csproj`: **3,866 passed, zero failed, zero skipped**, test duration **3 min 43 s**. This includes the six new representative checks, F6a–F6g, E6c/E6d/E6e and both unchanged writer-33 golden pins. No second testhost was running at launch.
- `git diff --check`: passed. `python3 tools/docs-links/check.py`: **378 Markdown files, zero broken relative links**. Only the three allowed F6h documentation files are added; no shared doc changes.
- No game, rig, ModsLocal, bridge, regression matrix or native save-profiling window was run. All foreground runs finished; SDK compiler/MSBuild servers were shut down before handoff. No test, watcher or task server remains. No push, PR or merge is performed.

## Current section 6 offline measurements (review Opus-r1-1)

Foreground macOS arm64 / .NET 10 Release, writer 33, one filtered testhost. `F6hMeasurements.CurrentSectionSixComponents` runs component fixtures sequentially, including the existing F6e/F6f/F6g/E5d-size measurement helpers against the current code. These are offline component timings; native getters, Unity layout and whole-frame time are unmeasured. No performance optimization or changed product coefficient is claimed. Historical values above remain before-values, with different hosts/configurations noted rather than interpreted as a matched speedup.

The data are in [review measurement evidence](../../tools/economy-interventions/evidence/f6h-review-r1.json). Samples include timer overhead after warmup. All times below are milliseconds; each timing cell is **p95 / max**. A zero at the timer's resolution means below that resolution, not a zero-duration operation. Every ≥30 ms sample is counted; slice flags retain sample index/time in the evidence, and bulk save flags retain count/condition/component. Allocation rows refer only to the measured operation.

### Rival arrival/completion and reconstruction

Arrival/completion measure the added F6b lane/count/admit/paid load operations with 17 competing Serving records, two/eight lanes, and the indicated retained F3-size consumer roster. They exclude the native rival read and full external-wallet settlement. Reconstruction has three outcomes per consumer, one-third paid-rival lunch records and 2% active Serving meals; ten timed rebuilds follow a warmup (at ten samples, p95 is the observed maximum). The retained load has two Serving records per rival and excludes the meal objects themselves.

| Consumers | Rivals | Lanes | Arrival p95 / max | Completion p95 / max | B/arrival / completion | ≥30 arrival / completion |
| --- | --- | --- | --- | --- | --- | --- |
| 1914 | 64 | 2 | 0.000700 / 0.043700 | 0.000100 / 0.041200 | 0.0 / 0.0 | 0 / 0 |
| 1914 | 64 | 8 | 0.000900 / 0.020000 | 0.000000 / 0.019600 | 0.0 / 0.0 | 0 / 0 |
| 10000 | 1024 | 2 | 0.000500 / 0.029100 | 0.000000 / 0.011800 | 0.0 / 0.0 | 0 / 0 |
| 10000 | 1024 | 8 | 0.000800 / 0.047200 | 0.000000 / 0.025400 | 0.0 / 0.0 | 0 / 0 |
| 100000 | 20000 | 2 | 0.000600 / 0.030800 | 0.000100 / 0.009200 | 0.0 / 0.0 | 0 / 0 |
| 100000 | 20000 | 8 | 0.000900 / 0.044800 | 0.000100 / 0.027800 | 0.0 / 0.0 | 0 / 0 |

| Consumers | Rivals | Rebuild p95 / max | B/rebuild | ≥30 |
| --- | --- | --- | --- | --- |
| 1914 | 64 | 3.375500 / 3.375500 | 12968 | 0 |
| 10000 | 1024 | 3.999300 / 3.999300 | 233440 | 0 |
| 100000 | 20000 | 19.299500 / 19.299500 | 4555296 | 0 |

20,000-rival transient retained load: **-165,569,728 B**, managed collected-heap delta.

### Full panel size matrix and change detection

The panel includes the current money-counter projection, 14 history rows and eight journal entries/shop, indexing the complete history and rendering the actual 16-shop page bound. The complete 1,914/10,000/100,000 consumer × 1/64/1,024 shop matrix is measured. Native statements, IMGUI and inspector draw are excluded. Hidden windows run 10,000 actual refresh-gate updates: **zero builds, zero requests, zero allocation** in all nine cases.

| Consumers | Shops | Panel p95 / max | B/build | ≥30 |
| --- | --- | --- | --- | --- |
| 1914 | 1 | 0.025200 / 0.124600 | 30152 | 0 |
| 1914 | 64 | 0.568200 / 1.402900 | 323336 | 0 |
| 1914 | 1024 | 2.699800 / 5.547100 | 610032 | 0 |
| 10000 | 1 | 0.024700 / 0.047600 | 30232 | 0 |
| 10000 | 64 | 0.391300 / 3.900700 | 323488 | 0 |
| 10000 | 1024 | 1.343700 / 7.735400 | 610184 | 0 |
| 100000 | 1 | 0.022900 / 7.672700 | 30304 | 0 |
| 100000 | 64 | 0.342600 / 14.746200 | 322976 | 0 |
| 100000 | 1024 | 1.162500 / 23.571700 | 610144 | 0 |

| Journal observation / shops | Samples | p95 / max | B/observation | ≥30 |
| --- | --- | --- | --- | --- |

These are Core consumers of existing supply/hours images, including one/eight-offer unchanged supply, changed supply and changed hours. The current matched-read and source guards in the full unit suite remain the offline no-added-native-read checks. Actual native counter/fingerprint proof remains in the numbered rig list.

### Accrual, posting and C9 ownership

Shift settlement: **0.000209 / 0.023042 ms**, **0 B/op**, **0 ≥30 ms** (20,000 samples). Posting rows are ≤32-owner units after fifty warm days, with the attendance policy enabled. The 100,000-consumer posting p95 is 0.001608 ms above the F3 reference of 0.0111 ms; that measured difference is retained without claiming a performance win. The separate integrated advancement table exercises actual wages and ordinary admission in a seven-day run at all three sizes.

| Consumers | Posting p95 / max | B/unit | ≥30 |
| --- | --- | --- | --- |
| 1914 | 0.008500 / 0.029833 | 0.0 | 0 |
| 10000 | 0.010625 / 0.033542 | 0.0 | 0 |
| 100000 | 0.012708 / 0.074792 | 0.0 | 0 |

| C9 work | p95 / max | B/op | ≥30 |
| --- | --- | --- | --- |
| admission gate | 0.000000 / 0.000100 | 0.0 | 0 |
| wallet lookup and admission 1914 | 0.000000 / 0.000100 | 0.0 | 0 |
| wallet lookup and admission 10000 | 0.000000 / 0.000200 | 0.0 | 0 |
| wallet lookup and admission 100000 | 0.000100 / 0.013600 | 0.0 | 0 |
| admission and debit (reset outside conceptual operation) | 0.000100 / 0.019100 | 0.0 | 0 |

| Places | Ownership unit p95 / max | B/unit | Observed callbacks / rounds | Retained B | ≥30 |
| --- | --- | --- | --- | --- | --- |
| 1 | 0.000100 / 0.005800 | 0.0 | 1,000 / 1000 | 2,072 | 0 |
| 1024 | 0.001000 / 0.050500 | 0.0 | 1,024,000 / 1000 | 174,376 | 0 |
| 20000 | 0.001100 / 0.038800 | 0.0 | 600,000 / 30 | 1,842,832 | 0 |

The ownership table uses the production bounded index with managed source callbacks. Each unit observes ≤16 places, publishes atomically, and reports exact callback counts. At most two owner-member getters/place means ≤32 reads/unit in Runtime; native getter latency/controller memory is not supplied by this double. Zero steady allocation assertions pass. Requested outside text is measured separately in the evidence. The native hourly-read assertion remains rig work.

### Current-writer save matrix (separate from mod-work)

All eight E5d/F6d-size fixtures use the current writer, not a copied historical latency. Every timed current write is byte-identical to the fixture, and every read loads and rewrites identically. Three samples per operation means p95 is the observed maximum. Construction, ledger population, predecessor comparisons and post-read rewrite checks stay outside the timing cells. These reuse the populated F6d ledger/journal fixtures; their F6g exact comparison images remain unobserved, so the composed counts do not assert saturation of every new observed-image maximum. This is offline codec work, not game save profiling. The four parked hotspots remain unchanged.

| Fixture | Characters / bytes (ASCII) | Streaming records | Current write p95 / max | Current read p95 / max | ≥30 write / read |
| --- | --- | --- | --- | --- | --- |
| 957x2 | 16,255,152 | 187,093 | 483.562700 / 483.562700 | 667.829000 / 667.829000 | 3 / 3 |
| 957x3 | 16,812,462 | 189,964 | 476.675900 / 476.675900 | 705.014700 / 705.014700 | 3 / 3 |
| 5000x2 | 27,514,966 | 344,770 | 747.221000 / 747.221000 | 1061.870700 / 1061.870700 | 3 / 3 |
| 5000x3 | 30,425,890 | 359,770 | 773.920500 / 773.920500 | 1211.131100 / 1211.131100 | 3 / 3 |
| 50000x2 | 146,225,439 | 2,099,770 | 3235.749600 / 3235.749600 | 5679.311400 / 5679.311400 | 3 / 3 |
| 50000x3 | 175,374,130 | 2,249,770 | 4273.468100 / 4273.468100 | 6821.072200 / 6821.072200 | 3 / 3 |
| CF32 | 179,975,056 | 2,266,049 | 4833.980600 / 4833.980600 | 7583.631200 / 7583.631200 | 3 / 3 |
| CF64 | 218,375,056 | 3,866,049 | 6816.703800 / 6816.703800 | 8352.746900 / 8352.746900 | 3 / 3 |

CF32 conservative composed envelope: **214,435,414 characters / 2,279,079 records**. CF64 conservative composed envelope: **252,835,414 characters / 3,879,079 records**.

Save/cold-read flags are retained under their separate component; they do not establish a mod-work or whole-frame pass. Current fixture hashes, bytes, both record counters and predecessor timings are retained in the evidence.

## Current cadence, body-cap and integrated stress evidence

`F6hMeasurements.CadenceAndBodyEvidence` uses the existing F6a/F1-derived 957-resident / 1,914-independent-person fixture, seed 0, for the same 42 logical days. It applies F1's float-minute accounting fence, `AccountingClock.Limit` / `BeginAccountingObservations`, clock holds, and budgeted `AdvanceFrameSlice` order from `AccountingScenario`. The F1 fixture's accelerated clock is 30 game minutes/real second; these are simulated rendered frames, not native graphics measurements. Each FPS is tested with **2 units / 0.1 ms** and **256 units / 4 ms** budgets. The complete economic day-metric JSON (including daily ledgers, receipts and outside totals) and the **entire final save** are asserted byte-identical to an independent epoch-drained reference. Each arm performs 80,388 daily wallet checks.

| FPS | Units/frame | Time budget ms | Frames | Days | Metric bytes / save bytes |
| --- | --- | --- | --- | --- | --- |
| 30 | 2 | 0.1 | 822768 | 42 | identical / identical |
| 30 | 256 | 4.0 | 97448 | 42 | identical / identical |
| 60 | 2 | 0.1 | 883417 | 42 | identical / identical |
| 60 | 256 | 4.0 | 158100 | 42 | identical / identical |
| 120 | 2 | 0.1 | 1004762 | 42 | identical / identical |
| 120 | 256 | 4.0 | 279419 | 42 | identical / identical |

Common economic metrics SHA256: `CE77BE246FAAE61690993CDF085FF5CF7A95D5F732160DF1991907519F15705A`. Common final-save SHA256: `DAE7566D49F1DF145733025057357D87A0CD9917681F6AFC1C534F9143652A9A`. The metric hash also matches the original full-size control seed 0.

### Body cap: flagged evidence, not a pass

At 60 simulated FPS on that same 42-day walking fixture, F2a's `PresentationDriver` calls the real Core physical-lease/arrival APIs with its Native lag model. It uses real-time movement even when F1 holds native time. The body-cap 0 arm is also asserted byte-identical to the cadence reference. Default settings are 48 pedestrians / 24 vehicles; this F1 fixture contains walking routes only, so **vehicle bodies, door queues, interiors, ambient reservations and optional-stop departure holds are outside this evidence**. No native movement is simulated by a stubbed zero-cap switch.

The default arm acquired **7,786 bodies**, made **126,450 lease observations**, and reached **13 concurrent walkers**; cap 0 acquired/observed none. Default minus cap 0 differences:

| Metric | Cap 0 | Default | Exact difference |
| --- | --- | --- | --- |
| Employment units | 202595375754213239 | 202488234478578341 | -107141275634898 |
| Outside discretionary units | 18570660125081600 | 18566069878784000 | -4590246297600 |
| Total own+rival purchases | 26515 | 26481 | -34 |
| Daily metric rows differing | — | — | 42 |

The monetary differences are about **$-3,991.32** in wages and **$-171.00** in C9 spend (display rounding only; exact units above). Both arms retain daily cash/receipt/watermark invariants and zero duplicate receipts/HistoricalClockMisses. Their ledger/save hashes differ and are retained. **Flagged: F2 has no declared divergence bound. This is no economic-equivalence pass or F2 contract change.**

### Integrated advancement and 100,000-consumer stress

Each size runs seven days on a fresh F5 person-wallet fixture with wages, rival limits, C9 admission/spending, memory, meal-period causes and the journal active. A price change on day 3 and reversion on day 5 produce actual journal entries. Every measured unit is an existing `AdvanceFrameSlice(..., 1)` call; simulation can continue over many units within the same minute, while midnight postings retain the production ≤32-owner bound. The timing includes the inclusive scheduler and market/decision-double work in that unit, excludes fixture construction, daily all-owner checks, output, final save hashing and sample-array management, and is not a Unity whole-frame sample. Allocation in the evidence is full **tool-run** allocation, not a steady-state allocation claim for F6.

| Consumers | Days | Slice samples | p95 / max ms | ≥30 ms | Daily wallet checks | Price journal entries |
| --- | --- | --- | --- | --- | --- | --- |
| 1914 | 7 | 236,725 | 0.007000 / 5.408300 | 0 | 13,398 | 2 |
| 10000 | 7 | 1,107,885 | 0.007500 / 8.383000 | 0 | 70,000 | 2 |
| 100000 | 7 | 10,607,016 | 0.008000 / 20.657300 | 0 | 700,000 | 2 |

Every run has nonzero actual employment credits, ordinary debits and meals; raw exact totals and metric hashes are in the evidence. At 100,000 consumers this is 50,000 residents plus 50,000 visitors. The first attempt incorrectly supplied 50,000 immutable donors, and its final codec validation refused the fixture (`Invalid immutable identity source catalogue size`); no successful stress/save result was claimed from it. The corrected fixture uses the normal **5,000-donor catalogue plus F5 housing expansion** to 50,000 residents. Its seven-day run completes the strict current-writer final save hash without relaxing a cap or modifying production population code.

The integrated slice maxima above are all below 30 ms. Current save/cold-read rows are flagged separately (all 3/3 current writes and reads in each of eight bulk fixtures reach 30 ms). Detached 100,000-consumer rival reconstruction exceeds 5 ms at p95 and remains a cold-attach observation, not frame work. The mod-work p95 <5 ms gate remains a native integrated-union obligation; whole-frame timing is unmeasured separately. No parked hotspot is changed.

## Full-size integrated reload (Opus-r1-3)

The ordinary `--integrated` sweep now repeats seed 0's full-size control for six weeks with a mid-epoch reload, outside every timing claim, passing both `Integrated` and the selected `PriceWeight` to the replay. This is additional to the fast 8-resident seven-day reload test. The explicit one-seed control invocation completed at 957 residents / 1,914 persons with **six meals in flight**: its 42-day metrics and final save are byte-identical. Both hashes and the reload count are retained in the review evidence. Failure of either comparison makes the sweep fail; recording the price stop cannot skip this check.

## Reproduce review measurements

Use the task's Homebrew PATH/DOTNET_ROOT, check `pgrep -f testhost` and wait before each dotnet launch, and run sequentially in the foreground with a fresh output directory (the row writers append). These are slice-specific opt-in tests, not a tools/*-regression matrix:

```sh
PC_F6H_MEASURE="$PWD/build/f6h-review" dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6hMeasurements --logger 'console;verbosity=normal'
dotnet run --project tools/economy-interventions -c Release -- build/f6h-review-reload --integrated --seeds 1 --arms control/control
```

The component case invokes F6e/F6f/F6g/F6d helpers **sequentially** and restores their temporary environment variables. `measurements.jsonl`, `wages.jsonl`, `ordinary.jsonl`, `journal.txt` and `saves.jsonl` retain the individual rows; the committed review evidence combines them with the final cadence/body/stress run and `reload-equivalence.json`. Three opt-in facts return without running long sweeps in the default suite; one new fast cadence/budget identity assertion runs normally. No game, rig, ModsLocal or bridge is used. The original acceptance report's unmeasured labels describe the initial price experiment; this separate current review record closes the independent offline items, while price calibration and native evidence remain stopped/pending under their existing contracts.

Review validation: the required build passed with unchanged DLL SHA256 `467e9f0f97c772e56c26415ec331e30e43eec170d1ff023f812aa63bf84660c1`; the full-size reload passed; the current component case and final cadence/body/stress cases passed. The required full foreground unit suite passed **3,870 tests**, zero failed/skipped, in **3 min 45 s** on the final report-only wide-sum code (the preceding full run also passed in 3 min 59 s), including the new fast cadence assertion, F6a–F6g, E6c/E6d/E6e and both unchanged writer-33 golden pins. The opt-in current-component case passed (8 min 45 s); the final cadence/body and integrated-stress cases passed (51 s and 1 min 11 s). The full-size reload interrupted six meals and passed both hash comparisons. `git diff --check` and the documentation-link check pass. All test/tool runs finish in the foreground, and SDK build servers are shut down before handoff. No push, PR or merge is performed.
