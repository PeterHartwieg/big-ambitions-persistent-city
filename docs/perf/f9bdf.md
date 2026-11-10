# F9b, F9d and F9f performance and numbers

Refs #96. These rows add offline tooling and test arms. They change no mod code (no Core, Persistence, Runtime or Compatibility file), so mod work is unchanged and no frame was timed. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately, #128 a separate investigation) is not affected. The four parked hotspots stay parked. Every number below is tool or test wall time on the PC (Windows 11 x64, .NET SDK 10.0.402, Python 3.12.10), 10 Nov 2026. None of it is mod work.

## F9b: build time with and without `--provenance`

Five warm builds each, back to back (`python tools/build.py --game-managed <Steam Managed>`):

| Build | Median | Runs (s) | DLL SHA-256 |
| --- | --- | --- | --- |
| Base, before the change | 1.44 s | 1.53, 1.44, 1.42, 1.42, 1.97 | `a7509204…67b1cd` |
| This branch, without the flag | 1.45 s | 1.47, 1.46, 1.45, 1.44, 1.40 | `a7509204…67b1cd` |
| This branch, with `--provenance` | 1.57 s | 1.48, 1.57, 1.49, 1.73, 2.19 | `a7509204…67b1cd` |

The provenance step itself, hashing the 28 assemblies (18.3 MB), takes 17.0 ms (median of seven, 15.9–17.6 ms); the rest of the 0.12 s median difference is run-to-run noise of the compiler step. The DLL is byte-identical with and without the flag, which `InstalledGame.test_dll_hash_is_unchanged_by_the_provenance_flag` checks on every run with the installed game.

## F9d: ledger and manifest wall time

| Measurement | Median |
| --- | --- |
| `python tools/acceptance/rig_manifest.py` end to end (17 rows, 136 first-parent commits; Python start, one `git log`, render) | 0.14 s (five runs, 0.13–0.16 s, one outlier 0.63 s) |
| `assess()` alone, first version: one shared `git log`, one `git rev-parse` per row | 0.339 s |
| `assess()` alone, after the cheap win | 0.061 s before the base merge, 0.076 s after |
| Alternative not taken: one `git log since..HEAD` per row | 0.45–1.05 s |

**Cheap win taken.** A row's `lastRigHead`/`recordedAt` that is a (possibly abbreviated) commit on the first-parent chain is resolved by prefix in the history already read, so no `git rev-parse` per row: 0.339 s → 0.061 s (5.6×). Revisions off the chain still get their own `git` query (`gitQueries` reports how many; 1 at this head). The runner calls `assess()` once per `quick`/`full` run and once per `summarize`.

## F9f: test time per arm

Tool time per arm (`CityRun.Seconds`, Debug test build), from one uncontended run of `FullyQualifiedName~F9IntegratedCity`. Each body arm runs the three-day F9c scenario with the presentation driver; the bodies-off column is the F9c arm of the same cadence in the same run.

| Arm | city-s body arm | city-s bodies-off | city-m body arm | city-m bodies-off |
| --- | --- | --- | --- | --- |
| `bodies-stationary` (on `fps60`) | 0.53 s | 0.40 s | 2.80 s | 2.99 s |
| `bodies-sweep` | 0.49 s | 0.40 s | 3.14 s | 2.99 s |
| `bodies-away-return` | 0.52 s | 0.40 s | 3.27 s | 2.99 s |
| `bodies-caps-1` | 0.39 s | 0.40 s | 3.58 s | 2.99 s |
| `bodies-caps-256` | 0.43 s | 0.40 s | 3.94 s | 2.99 s |
| `bodies-failures-50pct` | 0.42 s | 0.40 s | 3.20 s | 2.99 s |
| `bodies-hidden-skip` (on `hidden-skip`) | 0.53 s | 0.47 s | 3.27 s | 3.17 s |
| `bodies-reload` (on `reload`) | 0.86 s | 1.21 s | 6.47 s | 5.65 s |
| Sum of the eight | 4.17 s | | 29.67 s | |

The bodies-off reference (drained) took 0.58 s (city-s) and 3.22 s (city-m) in the same run. Body arm minus bodies-off ranges from −0.35 s to +0.95 s per arm (city-m caps 256: up to 26 walkers bound, 112,303 walker frames); single runs of the same arm vary by about ±0.3 s, so most of that is noise.

**Cheap win taken: one run cache for both city classes.** `F9IntegratedCityTests.Runs` now keeps its checkpoints and runs in static dictionaries, so `F9IntegratedCityMovementTests` reuses F9c's reference and bodies-off runs instead of computing both sizes' checkpoints, references and three bodies-off arms again. Both classes together (45 facts, `dotnet test --no-build --filter FullyQualifiedName~F9IntegratedCity`, no other test run on the machine):

| | Runs | Wall |
| --- | --- | --- |
| Before (one cache per class) | 2 | 71 s, 72 s |
| After (shared cache) | 3 | 47 s, 45 s, 45 s |

About 25 s (35%) less, and the F9f class compares with exactly the runs F9c asserts on.

## Not taken

- Running body arms only in `city-s`: `city-m` is F5h's declared load and holds most of the visitor and walker traffic (up to 27 walkers and 22 visitor bodies at once, against 9 and 5 in `city-s`), so it stays.
- Caching the body arms across facts: each arm is used by one fact.
