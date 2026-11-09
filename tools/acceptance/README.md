# Acceptance runner (F9a)

One offline entry point that says whether the integrated city still works, and from which code, settings and fixtures the answer came. Plan: [F9 city acceptance](../../docs/plans/f9-city-acceptance.md), Decisions 1, 2, 5 and 6 and section 3.9. Python 3 standard library only; the same command on Windows (`python`), macOS and Linux (`python3`).

```
python tools/acceptance/run.py quick
python tools/acceptance/run.py full --game-managed "<game>/Big Ambitions_Data/Managed"
python tools/acceptance/run.py summarize --result build/acceptance/<runId>/acceptance.json [--rig <file>]... [--performance <file>]
```

## Inputs

| Input | Default | Used by |
| --- | --- | --- |
| `--dotnet PATH` | `dotnet` on `PATH`; on Windows then `%LOCALAPPDATA%/Microsoft/dotnet/dotnet.exe` (as `tools/build.py`) | every .NET stage, `host.dotnetSdk` |
| `--game-managed DIR` | none | `build` only. Without it `build` is `not-run`, so the summary is `incomplete`, never `fail`. The path is recorded as `<supplied>`. |
| `--out DIR` | `build/acceptance/<runId>/` (ignored by Git) | all raw evidence and `acceptance.json` |
| `--price-weight W` | none | passed to the economy harness as `--price-weight W`; recorded as a declared deviation; the `economy` stage can then never pass |
| `--only a,b` | whole profile | runs a subset of the profile's stages, in profile order; the result lists them in `onlyStages` |
| `--no-wait` | wait | refuse instead of waiting when another `testhost` is running |
| `--strict` | off | exit 3 when the summary is `incomplete` |
| `--lock FILE`, env `PC_ACCEPTANCE_LOCK` | `<OS temp>/persistentcity-acceptance.lock` | machine-wide runner lock |
| env `PC_DOTNET_WAIT` | unset | optional shell command run before each .NET step (for example Peter's PC wait script). The runner works without it. |
| `--catalogue FILE` | `tools/acceptance/stages.json` | stage catalogue (tests use fakes) |

No other machine-specific script, path or user name is needed or recorded.

## Profiles and stages

`stages.json` holds one row per stage: `id`, `class`, the rule, and either `steps` (argv lists with the placeholders `{dotnet}`, `{python}`, `{out}`, `{managed}`, `{priceWeightArgs}`) or a test `filter`. A filter stage runs `dotnet test tests/PersistentCity.Tests.csproj --no-build --filter <filter>` with a TRX logger into `{out}/trx/<id>/`.

| Profile | Stages |
| --- | --- |
| `quick` | `provenance`, `test-build` (one `dotnet build`, so the filtered stages share one build), `population`, `economy-fast`, `saves`, `city-s` |
| `full` | `provenance`, `build`, `unit`, `population`, `economy`, `saves`, `movement`, `city-s`, `city-m` |
| `summarize` | merges rig results (class `native`) and F4's performance record into an existing result |

| Stage | Wraps | Verdict |
| --- | --- | --- |
| `build` | `tools/build.py --game-managed {managed}` | exit 0 and a printed DLL SHA-256 (recorded as `provenance.artifact`); `not-run` without `--game-managed`. F9b adds `--provenance` and the game fingerprint. |
| `unit` | the whole test project | zero failed in the TRX |
| `population` | `FullyQualifiedName~F5hIntegratedAcceptanceTests` | zero failed |
| `economy-fast` | `F6hIntegratedTests`, `F6kPriceWeightTests` | zero failed |
| `economy` | `tools/economy-interventions --integrated`, then `acceptance.py`, then `economy_stage.py` | see below |
| `saves` | `F7dCompactWriterTests`, `ReaderCorpusTests`, `SaveEpochTests` and every `*CurrentWriterGolden*` fact | zero failed |
| `movement` | `tools/movement-equivalence --out {out}/movement.json` | see below |
| `city-s`, `city-m` | `FullyQualifiedName~F9IntegratedCityTests&Category=city-s` / `city-m` | zero failed. F9c adds these tests with `[Trait("Category", "city-s")]` / `"city-m"`. Until then the filter matches nothing and the stage is `not-run`. |

A TRX stage with no executed test is `not-run`. A stage with no TRX and a non-zero exit is `fail` (build or host failure). In `full`, the filter stages whose filter is a plain `FullyQualifiedName~` alternation are evaluated on the `unit` TRX instead of running again (`reusedFrom: "unit"`); trait filters run on their own.

**Economy adapter** (`economy_stage.py`). `acceptance.py` (F6) is unchanged and its exit code is ignored, because it exits 1 on every complete sweep.

- `fail`: the harness exits non-zero; `acceptance.py` writes no JSON (its accounting `assert`s abort it); `completeInterventionMatrix` is false; any `failedCriteria` entry other than the household-threshold one; or the recorded `defaults.priceWeight` differs from the compiled default (read from `Core/FoodChoiceSettings.cs`, 1.2) without a declared override.
- `incomplete`: a household-threshold miss alone (deviation "household thresholds measured at weight 1"); an `unmeasuredCriteria` entry the adapter does not know; a declared `--price-weight`.
- Known `unmeasuredCriteria` entries are routed, never failed: native wholesale costs and staffed throughput to the `native` class (`classes.native.pending`), the perf/stress item to `performance` (F4), F1 cadence and F2 body-cap items to the `population`/`city-*` and `movement` stages, and the Pareto item to `superseded` ([price-weight decision](../../docs/decisions/2026-11-08-price-weight.md)).

**Movement expectations** (`expectations/movement.json`). Seeded from `tools/movement-equivalence/baseline.json`: every arm whose comparison was not identical. The harness's self-checks must pass. A listed arm may differ (`expected-red`); any other arm must equal its reference or the stage fails. A listed arm that now equals its reference is reported in `summary.notes`, not failed. F9f empties the list.

## Result (`{out}/acceptance.json`, schema `persistentcity-acceptance-1`)

```text
schema, runId, profile, onlyStages, startedUtc, finishedUtc
provenance: source{commit, branch, dirty, diffSha256, untrackedFiles}, host{os, architecture, dotnetSdk, python},
            artifact{persistentCityDllSha256}, fixtures[], settings{configSha256, compiledDefaults, declaredOverrides},
            capabilities{offline, native}
stages[]: id, class, required, argv (placeholders only), exit, seconds, verdict, reason,
          metrics{... childSeconds, waitSeconds, runnerOverheadSeconds}, digests{}, evidence[]{path, sha256, bytes, retained}
classes: semantic, native{verdict, pending[]}, performance{verdict, reference}, gameplay{"not-assessed"}
nativeEvidence: F9d
summary: verdict, required[], failed[], incomplete[], deviations[], notes[]
```

The summary covers the `semantic` and `native` classes only: `fail` if a required stage failed, else `incomplete` if one is `not-run` or `incomplete`, else `pass`. `expected-red` counts as passing. Until rig results are merged with `summarize`, a placeholder `rig` entry is `not-run`, so an offline run is at best `incomplete`. Performance and gameplay are reported beside the verdict and never change it. A `pass` is not a release or readiness claim.

The game block of `provenance` (fingerprint set and per-assembly hashes) is F9b. Fixture save hashes are `null` until F9c supplies them. Every string in the result is cleaned of the checkout, output, home and game paths and the user name; evidence paths are relative to `{out}`.

To cite a run, record its `runId`, `summary.verdict` and the SHA-256 the runner prints for `acceptance.json`. Raw evidence stays under `{out}` and is not committed; a run kept as reference evidence goes under `tools/acceptance/evidence/` (F9h).

## Lock and other test processes

The runner creates `<temp>/persistentcity-acceptance.lock` exclusively and writes its PID and checkout into it. A second runner exits 4 while that PID is alive; a lock whose PID is gone is cleared. Before each .NET step the runner waits while any `testhost` process runs (`tasklist` on Windows, `ps` elsewhere); time spent waiting is `waitSeconds`, not runner cost. Child processes get `MSBUILDDISABLENODEREUSE=1` and `UseSharedCompilation=false`, so no build server stays behind.

Exit codes: 0 pass or incomplete, 1 fail, 2 usage, 3 incomplete with `--strict`, 4 lock held or (`--no-wait`) other tests running.

## Tests

```
python -m unittest discover -s tools/acceptance -p test_*.py
```

Fake stage commands only; no .NET, game or rig. The runner does not call any `tools/*-regression` suite.
