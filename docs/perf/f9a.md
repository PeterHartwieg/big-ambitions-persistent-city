# F9a performance and numbers

Refs #96. F9a adds an offline Python runner under `tools/acceptance/`. It changes no mod code, so mod work is unchanged and no frame was timed. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately, #128 a separate investigation) is not affected by this slice. The four parked hotspots stay parked. Every number below is **tool wall time** on the PC (Windows 11, x64, .NET SDK 10.0.402, Python 3.12.10) at base `b8feb06c` plus this branch. None of it is mod work.

## Runner overhead per stage

`runnerOverheadSeconds` = stage wall time − child process time − time spent waiting for another session's `testhost`. It covers expanding the argv, the `tasklist` check, TRX parsing, hashing evidence and the adapters.

| Stage (quick, run `quick-3`) | Wall s | Child s | Wait s | Overhead s |
| --- | --- | --- | --- | --- |
| provenance | 0.0 | – | – | 0.0 |
| test-build | 2.12 | 1.79 | 0 | 0.33 |
| population | 25.34 | 24.98 | 0 | 0.37 |
| economy-fast | 3.06 | 2.68 | 0 | 0.38 |
| saves | 25.27 | 24.96 | 0 | 0.31 |
| city-s | 2.02 | 1.68 | 0 | 0.35 |

Runner overhead is 0.31–0.38 s per .NET stage, almost all of it the `tasklist` check. Provenance (git status/diff, `dotnet --version`, source reads, config hash) is taken once before the first stage; quick's total wall time (58.2 s) minus its stage times (57.8 s) leaves about 0.4 s for it and the result write. Reusing the `unit` TRX for a filter stage in `full` took 0.1 s per stage (`population`, `saves`) instead of a second `dotnet test`.

## Profile wall time

| Profile | Stages run | Wall time |
| --- | --- | --- |
| `quick` (final, `quick-3`) | all six; 220 tests (`saves` 188); `city-s` not-run | 58.2 s |
| `full`, chunk A (`--only provenance,build,unit,population,saves,city-s,city-m`) | build 2.0 s, unit 515.3 s (of which about 232 s waiting for another worktree's `testhost`; test child 283.6 s), population 0.1 s and saves 0.1 s (reused TRX), city-s 1.5 s, city-m 2.0 s | 8 min 42 s |
| `full`, chunk B (`--only provenance,movement`) | movement 272.8 s (child 180.8 s; the rest was waiting for another worktree's `testhost`) | 4 min 33 s |
| `full`, `economy` | not run in this session: the integrated matrix (86 six-week runs, 472 s at F6h, plus the harness's curve and tool-cost passes) does not fit the session's 10-minute foreground command limit. F9h runs it. | – |

`full` was run in chunks with `--only` because a foreground command here is limited to 10 minutes. Without waiting, chunk A + B is about 7.8 min of child time; with the F6h matrix's 472 s, an uncontended `full` is estimated at roughly 17–20 min on this PC, inside the plan's 25–35 min estimate.

## Cheap win: one test build for `quick`

Before, each filtered stage ran `dotnet test` with its own incremental build. After, `quick` runs one `dotnet build` (`test-build`) and every filtered stage runs `dotnet test --no-build`. Both measured warm, back to back, on the same machine:

| | population | economy-fast | saves | city-s | test-build | quick wall |
| --- | --- | --- | --- | --- | --- | --- |
| Before (per-stage build) | 23.6 s | 5.7 s | 27.7 s | 2.9 s | – | 60.7 s |
| After (`--no-build`) | 24.4 s | 3.0 s | 26.1 s | 2.0 s | 2.6 s | 58.4 s |

About 2 s (4%) faster. More usefully, every filtered stage now tests the same build. The `saves` filter was widened afterwards ([checks](../checks/f9a.md#offline-validation)), so the final `quick` runs 6 more tests than these two runs (58.2 s).

## Not taken

- The `tasklist` check before each .NET step costs about 0.3 s. It is the section 3.9 safety check and stays.
- Running filter stages in parallel would break the one-`dotnet`-at-a-time rule.
