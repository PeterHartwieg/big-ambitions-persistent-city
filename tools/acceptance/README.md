# Acceptance runner (F9a)

One offline entry point that says whether the integrated city still works, and from which code, settings and fixtures the answer came. Plan: [F9 city acceptance](../../docs/plans/f9-city-acceptance.md), Decisions 1, 2, 5 and 6 and section 3.9. Python 3 standard library only; the same command on Windows (`python`), macOS and Linux (`python3`).

```
python tools/acceptance/run.py quick
python tools/acceptance/run.py full --game-managed "<game>/Big Ambitions_Data/Managed"
python tools/acceptance/run.py summarize --result build/acceptance/<runId>/acceptance.json [--rig <file>]... [--performance <file>] [--update-ledger]
python tools/acceptance/run.py rig-manifest [--out DIR] [--result <acceptance.json>]
```

## Inputs

| Input | Default | Used by |
| --- | --- | --- |
| `--dotnet PATH` | `dotnet` on `PATH`; on Windows then `%LOCALAPPDATA%/Microsoft/dotnet/dotnet.exe` (as `tools/build.py`) | every .NET stage and `host.dotnetSdk`. An explicit host's directory is put first on `PATH` for every stage, so `tools/build.py`'s own discovery uses the same SDK. |
| `--game-managed DIR` | none | `build` only. Without it `build` is `not-run`, so the summary is `incomplete`, never `fail`. The path is recorded as `<supplied>`. |
| `--out DIR` | `build/acceptance/<runId>/` (ignored by Git) | all raw evidence and `acceptance.json` |
| `--price-weight W` | none | passed to the economy harness as `--price-weight W`; recorded as a declared deviation; the `economy` stage can then never pass |
| `--only a,b` | whole profile | runs a subset of the profile's stages, in profile order; the result lists them in `onlyStages`, and every other stage of the profile is recorded `not-run` ("omitted by --only"), so a partial run is never a `pass` |
| `--no-wait` | wait | refuse instead of waiting when another test run is active |
| `--strict` | off | exit 3 when the summary is `incomplete` |
| `--lock FILE`, env `PC_ACCEPTANCE_LOCK` | `<OS temp>/persistentcity-acceptance.lock` | machine-wide runner lock |
| env `PC_DOTNET_WAIT` | unset | optional shell command run before each .NET step (for example Peter's PC wait script). The runner works without it. |
| `--catalogue FILE` | `tools/acceptance/stages.json` | stage catalogue (tests use fakes) |

No other machine-specific script, path or user name is needed or recorded.

## Profiles and stages

`stages.json` holds one row per stage: `id`, `class`, the rule, and either `steps` (argv lists with the placeholders `{dotnet}`, `{python}`, `{out}`, `{managed}`, `{priceWeightArgs}`) or a test `filter`. A filter stage runs `dotnet test tests/PersistentCity.Tests.csproj --no-build --filter <filter>` with a TRX logger into `{out}/trx/<id>/`.

| Profile | Stages |
| --- | --- |
| `quick` | `provenance`, `test-build` (one required `dotnet build`, so the filtered stages share one build), `population`, `economy-fast`, `saves`, `city-s` |
| `full` | `provenance`, `build`, `unit`, `population`, `economy`, `saves`, `movement`, `city-s`, `city-m` |
| `summarize` | merges rig results (class `native`) and F4's performance record into an existing result (see *Summarize*) |
| `rig-manifest` | writes the native-evidence ledger state and the ordered rig batch manifest (see *Native-evidence ledger*); seconds, no .NET |

| Stage | Wraps | Verdict |
| --- | --- | --- |
| `build` | `tools/build.py --game-managed {managed} --provenance {out}/build.json` | exit 0 and a printed DLL SHA-256 (recorded as `provenance.artifact`); `not-run` without `--game-managed`. The fingerprint record (F9b, below) fills `provenance.game`. |
| `unit` | the whole test project | zero failed in the TRX |
| `population` | `FullyQualifiedName~F5hIntegratedAcceptanceTests` | zero failed |
| `economy-fast` | `F6hIntegratedTests`, `F6kPriceWeightTests` | zero failed |
| `economy` | `tests/economy-interventions --integrated`, then `acceptance.py`, then `economy_stage.py` | see below |
| `saves` | `SaveRoundTripTests`, `ReaderCorpusTests`, `SaveEpochTests` and every `*CurrentWriterGolden*` fact | zero failed |
| `movement` | `tests/movement-equivalence --out {out}/movement.json` | see below |
| `city-s`, `city-m` | `FullyQualifiedName~F9IntegratedCity&Size=city-s` / `city-m` | zero failed. Selects F9c's `F9IntegratedCityTests` and F9f's body arms `F9IntegratedCityMovementTests`, both with `[Trait("Size", "city-s")]` / `"city-m"`. |

Before a stage runs, the runner deletes the outputs it owns under `{out}` (its log, TRX directory, harness JSON, `f6h/`), so a rerun into the same directory never reads a stale report. A filter stage runs with `--no-build` only after this run's test build (`test-build` or `unit`) succeeded; if that build failed, the filter stages fail without running older binaries, and without a test build in the run they build themselves. A TRX stage with no executed test is `not-run` when the exit is 0 or the log says no test matched the filter; a non-zero exit with no result (a crashed test host) is `fail`. A stage with no TRX and a non-zero exit is `fail` (build or host failure). In `full`, the filter stages whose filter is a plain `FullyQualifiedName~` alternation are evaluated on the `unit` TRX instead of running again (`reusedFrom: "unit"`); trait filters run on their own.

**Economy adapter** (`economy_stage.py`). `acceptance.py` (F6) is unchanged and its exit code is ignored, because it exits 1 on every complete sweep.

- `fail`: the harness exits non-zero; `acceptance.py` writes no JSON (its accounting `assert`s abort it); `completeInterventionMatrix` is false; any `failedCriteria` entry other than the household-threshold one; or the recorded `defaults.priceWeight` differs from the compiled default (read from `Core/FoodChoiceSettings.cs`, 1.2) without a declared override.
- `incomplete`: a household-threshold miss alone (deviation "household thresholds measured at weight 1"); an `unmeasuredCriteria` entry the adapter does not know; a declared `--price-weight`.
- Known `unmeasuredCriteria` entries are routed, never failed: native wholesale costs and staffed throughput to the `native` class as rows `native-wholesale-costs` and `native-staffed-throughput` (`classes.native.pending`). Each row is a required native result that stays `incomplete` until a native stage record with `"measures": "<row>"` reports; an unrelated rig pass does not clear it. Then the perf/stress item to `performance` (F4), F1 cadence and F2 body-cap items to the `population`/`city-*` and `movement` stages, and the Pareto item to `superseded` ([price-weight decision](../../docs/decisions/2026-11-08-price-weight.md)).

**Movement expectations** (`expectations/movement.json`). Seeded from `tests/movement-equivalence/baseline.json`: every arm whose comparison was not identical. The harness's self-checks must pass. A listed arm may differ (`expected-red`); any other arm must equal its reference or the stage fails. A listed arm that now equals its reference is reported in `summary.notes`, not failed. F9f empties the list.

## Result (`{out}/acceptance.json`, schema `persistentcity-acceptance-1`)

```text
schema, runId, profile, onlyStages, startedUtc, finishedUtc
provenance: source{commit, branch, dirty, diffSha256, untrackedFiles}, host{os, architecture, dotnetSdk, python},
            artifact{persistentCityDllSha256}, fixtures[], settings{configSha256, compiledDefaults, declaredOverrides},
            capabilities{offline, native}
stages[]: id, class, required, argv (placeholders only), exit, seconds, verdict, reason,
          metrics{... childSeconds, waitSeconds, runnerOverheadSeconds}, digests{}, evidence[]{path, sha256, bytes, retained}
classes: semantic, native{verdict, pending[]}, performance{verdict, reference}, gameplay{"not-assessed"}
nativeEvidence: status, head, fingerprint, batchDue, batchDueReasons[], stale[], unproven[], requiredChecks[], rows[], ledger, ledgerSha256
summary: verdict, required[], failed[], incomplete[], deviations[], notes[], nativeBatchDue, staleNativeRows[], unprovenNativeRows[]
```

## Game fingerprints (F9b)

`supported-game.json` declares named fingerprint sets: the SHA-256 of every assembly `tools/build.py` references plus `Newtonsoft.Json.dll`. It holds hashes only, never assemblies. `current` names the set the native ledger is measured against. The first set, `steam-1331550-build-25482473`, was hashed read-only from the PC's installed Steam game on 10 Nov 2026. A game update is a new set added by a reviewed change.

`python tools/build.py --game-managed <Managed> --provenance <file>` hashes the supplied assemblies before compiling and writes `{schema: persistentcity-build-provenance-1, managed: "<supplied>", set, differing[], assemblies{file: sha256}}`: the matching set's name, or `unknown` with the file names that differ from the closest declared set. No path is written. `unknown` never stops the build, so the Mac shim and the Linux box keep building; a missing assembly is still refused before anything is hashed. The flag does not change the compiled DLL.

The `build` stage copies the record into `provenance.game` (`supplied`, `managed: "<supplied>"`, `set`, `differing`, `assemblies`). With `set` `unknown` (or no record) the summary adds a required native item `game-fingerprint` as `incomplete`; the build itself still passes.

## Summarize

`summarize --result <acceptance.json> --rig <file>... [--performance <file>] [--out DIR]` reads rig results in the same schema. Their stages of class `native` count only when the rig file's `provenance.source` has the same commit, dirty flag and diff hash as the offline run and, when both record one, the same `provenance.artifact.persistentCityDllSha256`. Otherwise they are kept as `incomplete` with the reason, whatever their own verdict (a foreign `fail` does not fail this run); the original verdicts stay in `importedEvidence[].stages`. Each rig file's name, SHA-256, provenance and `sameSource` flag are kept in `importedEvidence`. Every evidence file, the run's own and each rig file's (paths relative to the rig file), is checked against its hash and copied under the merged directory (`rig/<n>-<name>/` for rig files, with `<n>` numbered after every earlier import, so repeated `summarize` runs on a merged result never overwrite earlier evidence); a missing or changed file makes its stage `incomplete`.

## Native-evidence ledger (F9d)

`native-assumptions.json` lists each native assumption the city relies on (Decision 7): an ID, one sentence, the records that established it, `recordedAt` (the commit that added the record), the source globs that depend on it, the rig check that proves it (section 7 numbering, `f9-1` … `f9-10`, plus `f6b-2`), the F8 capability ID where one exists, and the head, fingerprint set and run of its last rig evidence (`lastRigHead`, `lastRigFingerprint`, `lastRigEvidence`; all `null` until a batch passes). It also holds the section 7 checks with their mode (locked-PC or input-only) and their existing and pending rig capabilities.

`rig_manifest.py` reads the first-parent history of the head once (`git log --first-parent --diff-merges=first-parent --name-only`) and, per row, counts the commits since `lastRigHead` (or `recordedAt` without rig evidence) that touched the row's globs. That count is the row's **debt**. A row is `stale` when it has rig evidence and debt, when its evidence head is not in this history, or when the game fingerprint differs from the set its evidence was taken on; `unproven` without rig evidence; `fresh` otherwise. A fingerprint other than the evidence's, or `unknown`, marks every row stale. The fingerprint is the run's build set, else `supported-game.json`'s `current`. **Native batch due** when any row's debt is at least `debtThreshold` (3) or the fingerprint changed. The manifest lists every check: `f9-1` first, then the checks of stale rows, then unproven, then fresh (by largest debt, then catalogue order), `f9-10` last, plus rows that no check can prove yet (`unscheduled`, with the pending capability).

In `quick` and `full` the runner writes the ledger state into `nativeEvidence`. `requiredChecks` (the check of every row that is not fresh) replaces the single `rig` placeholder: each missing check is a required native item `not-run`, so the summary is `incomplete` until a rig record for it is merged, and a merged check with `fail` fails `native`. A rig stage satisfies a check when its `id` is the check ID or its `checks[]` lists it. `summary.nativeBatchDue`, `staleNativeRows` and `unprovenNativeRows` are printed next to the verdict and never change it; nothing here blocks a merge (merge rule v2). `summarize` writes `{out}/native-assumptions.json` with the run's commit, fingerprint set and run ID recorded for every row whose check passed from the same source; `--update-ledger` also writes it into `tools/acceptance/native-assumptions.json` for a reviewed commit. `rig-manifest --result <merged>/acceptance.json` reads that `native-assumptions.json` beside the result when it exists, so the manifest shows the batch's evidence without `--update-ledger`. Rows are not updated without a declared fingerprint set, or when a rig file names another set than the build.

## Verdict

The summary covers the `semantic` and `native` classes only: `fail` if a required stage failed, else `incomplete` if one is `not-run` or `incomplete`, else `pass`. `expected-red` counts as passing. Until rig results are merged with `summarize`, the required rig checks of the ledger (or, for a catalogue without a ledger, one placeholder `rig` entry) are `not-run`, so an offline run is at best `incomplete`. Performance and gameplay are reported beside the verdict and never change it. A `pass` is not a release or readiness claim.

Fixture save hashes are `null` until F9c supplies them. Every string in the result is cleaned of the checkout, output, home and game paths and the user name; evidence paths are relative to `{out}`.

To cite a run, record its `runId`, `summary.verdict` and the SHA-256 the runner prints for `acceptance.json`. Raw evidence stays under `{out}` and is not committed; a run kept as reference evidence goes under `tools/acceptance/evidence/` (F9h).

## Lock and other test processes

The runner takes an OS lock (`msvcrt.locking` on Windows, `flock` elsewhere) on one byte of `<temp>/persistentcity-acceptance.lock` and then writes its PID and checkout into the file for messages. A second runner exits 4 while the lock is held, even before the PID is written. The kernel drops the lock when its holder exits, so a crashed runner's file is taken over (and reported as cleared); the PID in the file never decides ownership. The file is not deleted on release. Before each .NET step, including `tools/build.py`, the runner waits while another test run is active: a `testhost`, or a `dotnet` host whose arguments include `test` or vstest, in restore, build or run (CIM on Windows, `ps` elsewhere). MSBuild nodes and other `dotnet` commands do not count; time spent waiting is `waitSeconds`, not runner cost. Child processes get `MSBUILDDISABLENODEREUSE=1` and `UseSharedCompilation=false`, so no build server stays behind.

Exit codes: 0 pass or incomplete, 1 fail, 2 usage, 3 incomplete with `--strict`, 4 lock held or (`--no-wait`) other tests running.

## Tests

```
python -m unittest discover -s tools/acceptance -p test_*.py
```

Fake stage commands only; no .NET, game or rig. The runner does not call any `tools/*-regression` suite.
