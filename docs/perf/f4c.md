# F4c: presentation trace and stall attribution, performance

9 November 2026, offline. Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Measurement named in the [F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown) for F4c: join cost on a 7-hour synthetic trace; no mod work. [Checks](../checks/f4c.md).

## Mod work

F4c changes no Core, Runtime or Persistence code, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged and not measured here. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately) stays as F4a's rows M1 and M2. F4c reports presented and whole-frame stalls in their own table, and never counts whole minus union as PC. #128 stays a separate investigation; F4c only supplies its attribution fields. The four parked hotspots stay parked. A tool-only slice has no mod-work win to take.

## Join cost on a 7-hour synthetic trace

Session: `fixtures/present/synthetic_trace.py` `long_session(7.0)`: 1,512,000 probe frames at about 60 FPS, a stall of 60–400 ms every 480 frames (3,150 stalls), 0.1 % of presents missing from the trace and 0.2 % dropped. The trace is in PresentMon 2.x format with a +37 ms clock offset, 197.2 MB of CSV. One window, Python 3.12, one process, on the development machine (shared with other sessions). Two runs:

| Step | Run 1 | Run 2 |
| --- | ---: | ---: |
| CSV parse (`present.read_trace`) | 5.16 s | 5.49 s |
| Frame normalisation (`attribute.frame_rows`) | 2.08 s | 2.74 s |
| Offset estimate and refinement | 0.77 s | 1.14 s |
| Join | 0.69 s | 0.95 s |
| Whole `attribute()` (normalise, estimate, join, series, 6,304 stalls classified, M2) | 10.08 s | 15.41 s |

Result in both runs: offset −36.749 ms, so the trace's +37 ms shift was found, with the present 0.25 ms before the frame end as generated. 99.90 % of clean frames joined, which is exactly the frames whose present was not removed. 6,304 stalls (3,150 whole-frame, 3,154 presented), all `unattributed`, because the synthetic stalls carry no evidence.

## Cheap win taken

The first version found each stall's game minute by scanning every frame of its window, and deep-copied the whole export before enriching it. On the 7-hour session that scan is 6,304 × 1.5 M comparisons; the first timing run was stopped after more than 10 minutes. On a 0.5-hour session (108,000 frames, 451 stalls) cProfile showed 3.3 s of 6.2 s profiled in `deepcopy` and 1.5 s in the frame scan. Three changes:
- the game minute now comes from the stall's own last frame;
- the export is copied shallowly (frame rows are read-only; only window keys and receipts change);
- reference-rate counts use bisection on sorted stall lists.

| 0.5-hour session | Before | After |
| --- | ---: | ---: |
| Whole `attribute()` | 2.77 s | 0.82 s |

All tests passed before and after. A test checks that the input export is left unchanged.

The unit suite runs a 10-minute session (`EndToEndTests.test_ten_minute_join_runs_in_seconds`); all 75 perf-gate tests take about 6 s.
