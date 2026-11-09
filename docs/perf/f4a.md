# F4a: perf gate contract and analyzer, performance

9 November 2026, offline. Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Measurement named in the [F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown) for F4a: analyzer run time on a synthetic 50-window export; no mod work. [Checks](../checks/f4a.md).

## Mod work

F4a changes no Core, Runtime or Persistence code, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged and not measured here. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately) is now frozen as rows M1 and M2 of `tools/perf-gate/contract-v1.json`. Whole-frame timing has its own column in every report. #128 stays a separate investigation. The four parked hotspots stay parked; no mod-work win applies to a tool-only slice.

## Analyzer cost

Export: `python tools/perf-gate/fixtures/synthetic.py --windows 50 --out <file>`. That gives 50 windows (25 candidate, 25 reference) × 18,000 frames, i.e. 300 s at 60 FPS each, with 900,000 probe frames and 900,000 presented intervals. The file is 121.8 MB of JSON. Python 3, one process, on the development machine (shared with other sessions). Three runs each:

| Step | Before | After |
| --- | ---: | ---: |
| JSON load | 1.05–1.47 s | 1.05–1.08 s |
| Analyze (validity, rows, pairing, pooling, spikes, stalls) | 3.94–4.30 s | 1.47–1.49 s |
| Write JSON + Markdown | 0.01 s | 0.01 s |
| **Total** | **5.02–5.44 s** | **2.53–2.57 s** |

The report is 157 KB of JSON and 20 KB of Markdown. Cost is linear in frames: one normalisation pass, three sorts per window (mod-work, whole-frame, presented) and one pass per pool.

## Cheap win taken

The first version normalised each frame row by trying every camelCase and snake_case key name through a lambda and a generator. Under cProfile that took 6.2 of 9.9 profiled seconds. The analyzer now picks the naming once per window from the first row and reads keys directly; a row missing a timing key refuses the report. The finite/negative check moved to the sorted list (`values[0] < 0`, then `all(map(math.isfinite, ...))`). Analyze time fell from about 4.0 s to 1.5 s, with identical reports and all 35 tests passing before and after.

The unit tests run a 50-window × 2,000-frame export in `ReportTests.test_fifty_window_export_runs_in_seconds`; the whole suite takes about 0.4 s.

## After review round 1

The round 1 fixes added a stall count per window for S4, the dense-street row, per-call advancement evidence and GC-unknown tracking. Same 50-window export, three runs: load 1.39–1.50 s, analyze 2.01–2.12 s, write 0.01 s, total 3.41–3.63 s. JSON load is unchanged code and also moved from about 1.05 s to about 1.4 s, so part of the change is load on the shared machine; the two extra passes over each window's frames for the stall count are the rest. That is still a few seconds, so no further change. The suite now has 43 tests.
