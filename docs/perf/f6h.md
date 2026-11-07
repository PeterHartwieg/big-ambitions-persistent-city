Status: implemented (offline); not verified by rig

Refs #93. Base `3c5ce3b4`, writer 33. **Integrated acceptance/calibration stopped on the price-tradeoff gate.** This record distinguishes the new experiment cost from historical component measurements. The full section 6 current-build timing/stress matrix is unmeasured after that stop; it is not passed by copying earlier slices' numbers.

## New experiment

Foreground Mac arm64, .NET 10 Release, one dotnet process at a time. `tools/economy-interventions --integrated` runs 17 arms × five seeds × 42 days at 957 residents plus 957 independent F5 visitors. All seven section 3.5 families run with wages, ordinary admission/spending, rival constraints, meal-period causes and observation journal enabled. The data and acceptance failure are in [calibration](f6h-calibration.md). `F6hIntegratedTests` adds six fast representative cases to the default suite; the long experiment remains an opt-in tool. The acceptance-reader tests take about a millisecond and launch no dotnet process.

The initial complete sweep took about **4.5–5.1 seconds per six-week run** after the first warm run (**7.4 seconds**), roughly **0.75–0.85 seconds/simulated week**. These rounded console observations include fixture construction, native doubles, decision tracing, daily all-owner accounting checks and the final detached save hash. They are **tool wall time**, not per-frame mod-work. They have no component spike attribution or allocation measurement. A final complete sweep repeats the experiment after descriptive metric-label corrections; the retained machine summary carries its per-run metrics hashes. The optional bounded price-weight diagnostic is a separate run, not a deployed default.

No Core or Runtime performance optimization is taken. There is no claimed before/after product win. Physical population, closure refresh, optional-stop refresh and save serialization stay parked; #128 whole-frame stalls are not investigated.

## Full section 6 table: inherited evidence and remaining current measurements

Every number in the historical column is from the linked merged slice, **not remeasured F6h evidence**. Current-build F6h measurements in this table remain stopped. Native reads, controller retention, Mono and the unified mod-work union require the batched rig separately from offline component work.

| Section 6 work and sizes | Historical before-values | F6h current measurement |
| --- | --- | --- |
| Rival arrival/completion; 1,914/10,000/100,000 consumers | [F6b](f6b.md): two lanes p95 0.0001/0.0001 ms, 0 B/op; eight lanes/16 serving p95 0.0007/0.0001 ms, 0 B/op. These are load microbenchmarks, not three full integrated size arms. | Stopped; all three integrated sizes remain. |
| Rival reconstruction; 1,914/10,000/100,000 consumers and 20,000 rivals | [F6b](f6b.md): p95 0.0805/0.3226/17.7817 ms; 12,976/233,448/4,555,304 B/rebuild; 3,570,344 B retained at 20,000 rivals. | Stopped; includes detached/cold attach, separately from frame work. |
| Requested/hidden F6c/F6g panel; 1/64/1,024 shops × 1,914/10,000/100,000 consumers | [F6c](f6c.md): old consumer scans p95 1.1088/3.4898/32.1975 → counters 0.0026/0.0050/0.0030 ms; old all-shop text at 1,024 shops p95 14.5748 ms. [F6g](f6g.md): final paged panel p95 0.033667/0.706292/2.357084 ms, hidden 0 builds/0 B. | Stopped; the current full Cartesian matrix remains, including native statement read and Unity layout costs in the rig. |
| Wage accrual and ≤32-posting units; 1,914/10,000/100,000 consumers | [F6e](f6e.md): settlement p95 0.000250 ms, 0 B; posting p95 0.005916/0.006792/0.011792 ms, 0 B/unit. F3 references 0.0208/0.0229/0.0111 ms. | Stopped; current F6-loop posting/advancement stress at 100,000 is unrun. |
| Ordinary admission/debit/owner lookup; 1,914/10,000/100,000 consumers | [F6f](f6f.md): gate p95 0.0001 ms, wallet lookup+gate 0.0001/0.0001/0.0002 ms; debit+dated counters 0.0003 ms; 0 B steady state. | Stopped. |
| Hourly ordinary ownership refresh; 1/1,024/20,000 places, ≤16/unit | [F6f](f6f.md): managed callback unit p95 <0.0001/0.0008/0.0010 ms, 0 B; retained 2,072/185,728/1,843,464 B. Bound ≤2 native owner-member reads/place, ≤32/unit; actual native getter time unmeasured there. | Stopped; current native read count/per-unit p95/max/allocation/controller retention remain rig work. |
| Journal change detection; 1/64/1,024 shops, ≤8 offers | [F6g](f6g.md): unchanged one-offer supply p95 0.000125/0.000125/0.000125 ms, 0 B; changed supply 0.001166/0.001333/0.001542 ms; changed hours 0.005875/0.006083/0.008916 ms. Matched headless market read counts equal. | Stopped; no new native read from the harness journal; native equality remains the rig's independent counters/fingerprint. |
| Save/read, separate; 957/5,000/50,000 residents × factor 2/3, CF32/CF64 | [F6d](f6d.md): all size-matrix bytes/records and cold read/write latency; CF32 composed 214,047,318 chars/2,279,079 records. [F6e](f6e.md): fresh factor-2 write p95 110.936/181.754/1687.388 ms, read 279.747/389.238/3780.691 ms. [F6g](f6g.md): writer-33 observed 225-person fixture 198,523 chars/858 records; write/read p95 4.747792/14.840833 ms. | Stopped; earlier schemas' saturated matrix is not a writer-33 composition claim. No codec, cap, field or golden pin changes here. |

## Retained ≥30 ms flags and whole-frame boundary

Historical flags remain flagged: [F6b's merged-checks rerun](../checks/f6b.md) for the 100,000-consumer attach reached **38.5022 ms** while other worktrees shared the host; quieter reruns did not erase it. F6c's then-current 1,024-shop all-text panel had **1/200 at 34.2305 ms**, before F6g pagination; removed scans/lookups had 45/200 and 200/200 ≥30 ms. F6g's final paginated samples had zero ≥30 ms. These are different builds and fixture shapes, not a matched F6h before/after experiment.

F6d's 96/96 measured bulk save/cold-load operations and F6e's 30/30 fresh-city writes/reads reached 30 ms and are retained as save-path flags, outside ordinary mod-work. No current per-frame sample or unified mod-work p95 was measured by this slice. **Whole-frame cost is unmeasured separately**; no inference is made from headless tool runtime. The standing gate stays mod-work p95 <5 ms with every ≥30 ms sample flagged/attributed. There is no new autosave setting in a headless run. The final rig list in [checks](../checks/f6h.md) requires original-setting capture, verification off before each native window and after reload, invalidation on any save inside a window, and cleanup restoration.

## Foreground validation

- Six new C# integrated seam/reload/accounting-fault checks: **6 passed** (test execution about 1.1 s).
- Four Python acceptance-reader checks: **4 passed**.
- Initial 85-run six-week sweep: **6,832,980** daily owner conservation checks, zero duplicate receipts, zero HistoricalClockMisses, exact native-double receipt and outside-watermark reconciliation. Acceptance reader exits **1** on the deliberately retained dominant-menu failure.
- Final 85-run sweep: all economic values exactly reproduce the initial sweep; descriptive rival labels were corrected and the final 85 metric hashes are retained. Repeated tool runtime is about **4.8–5.6 s/run** after the **7.2 s** first warm run (about **0.80–0.93 s/week**); still tool cost only.
- Bounded-price diagnostic: **20 runs**, **1,607,760** daily owner checks; the same +50% menu criterion remains red at the existing maximum price weight **3**. Final diagnostic tool runtime about **4.9–5.3 s/run** after warmup.
- Required build: **passed** using the supplied external Managed directory. DLL SHA256 `467e9f0f97c772e56c26415ec331e30e43eec170d1ff023f812aa63bf84660c1`.
- Required foreground `dotnet test tests/PersistentCity.Tests.csproj`: **3,866 passed, zero failed, zero skipped**, test duration **3 min 43 s**. This includes the six new representative checks, F6a–F6g, E6c/E6d/E6e and both unchanged writer-33 golden pins. No second testhost was running at launch.
- `git diff --check`: passed. `python3 tools/docs-links/check.py`: **378 Markdown files, zero broken relative links**. Only the three allowed F6h documentation files are added; no shared doc changes.
- No game, rig, ModsLocal, bridge, regression matrix or native save-profiling window was run. All foreground runs finished; SDK compiler/MSBuild servers were shut down before handoff. No test, watcher or task server remains. No push, PR or merge is performed.
