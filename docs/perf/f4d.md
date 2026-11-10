# F4d: batch flow and fixture specs, performance

10 November 2026, offline. Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). The [F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown) lists no perf measurement for F4d ("tool only"); the task asks for the flow's generation time. [Checks](../checks/f4d.md).

## Mod work

F4d changes no Core, Runtime or Persistence code, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged and not measured here. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately) is rows M1 and M2 of F4a's `contract-v1.json`, and the flow measures it in every candidate window. #128 stays a separate investigation. The four parked hotspots stay parked.

## Generation cost

Apple M1 Pro, Python 3.15, one process, development machine shared with other sessions. The full flow is 3,937 steps, 46 game processes, 88 gate windows, 41 discarded prime windows and 14 measurement segments.

| Step | Time |
| --- | ---: |
| `build_flow` in process, 20 runs | p50 15.1 ms, max 18.4 ms |
| JSON serialisation (`indent=1`, sorted keys) | 8.2 ms |
| CLI end to end (`make_gate_flow.py --out`), 5 runs | 0.07 s each |

The output is 1.74 MB of JSON plus a 2.4 KB Markdown summary. The test module (28 tests, which build the flow about six times) runs in about 0.35 s.

## Cheap wins

None taken. Generation takes 70 ms, so there is nothing worth winning. I tried compact JSON separators: 1.22 MB instead of 1.74 MB and 7.7 ms instead of 8.2 ms to serialise. I kept the indented form because the rig logs and reviewers read it step by step, and 0.5 ms is not worth losing that.

## Rig time the flow implies

These are estimates from fixed per-step allowances (launch 90 s, load 60 s, readiness 30 s, window real time at the declared pace), not measurements:

- Full flow: about 18.5 real hours.
- G4–G8 matrix plus the High-preset and 256/64 arms: 36 processes, 76 primary and 8 report-only windows, about 15.5 real hours.

The plan's run budget says "about 50 windows" and "roughly 7 hours". The matrix it specifies has more windows than that: 5 windows × 2 scripts × 2 builds × 2 or 4 repetitions is 56, plus 8 S-sweep and 8 S-drive evening windows and 4 S-manage lunch windows, so 76 primary windows in total. Each window is also longer than the 300 s floor, because it runs 100 game minutes (330 real s at the declared pace). And every process needs a discarded prime window and a reload, because a build switch needs a new process. The batch coordinator can split the matrix across rig sessions between processes. Every process starts from a saved checkpoint, so a split keeps the results as long as each condition's processes keep their ABAB order.
