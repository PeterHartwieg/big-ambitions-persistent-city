# Acceptance runner

One offline entry point that says whether the integrated city still works, and from which code, settings and fixtures the answer came. Python 3 standard library only. Stages are in `stages.json`; the result format, ledger rules and options are in the source (`run.py`, `rig_manifest.py`, `evidence.py`, `economy_stage.py`).

```
python tools/acceptance/run.py quick
python tools/acceptance/run.py full --game-managed "<game>/Big Ambitions_Data/Managed"
python tools/acceptance/run.py summarize --result build/acceptance/<runId>/acceptance.json [--rig <file>]... [--performance <file>] [--update-ledger]
python tools/acceptance/run.py rig-manifest [--out DIR] [--result <acceptance.json>]
python -m unittest discover -s tools/acceptance -p test_*.py
```

## Evidence classes

- **semantic**: offline stages (build, unit tests, population, economy, saves, movement, city sizes, whole loop).
- **native**: rig results merged with `summarize`. They count only when the rig file's source commit, dirty flag, diff hash and DLL hash match the offline run. Every evidence file is checked against its hash; a missing or changed file makes its stage `incomplete`.
- **performance** and **gameplay**: reported beside the verdict; they never change it.

The summary covers semantic and native: `fail` if a required stage failed, else `incomplete` if one is `not-run` or `incomplete`, else `pass`. Until rig results are merged, the ledger's required rig checks are `not-run`, so an offline run is at best `incomplete`. A `pass` is not a release or readiness claim.

## Economy stage limits

- A household-threshold miss alone is a diagnostic `incomplete`, not a `fail`.
- Native wholesale costs and staffed throughput are required native rows (`native-wholesale-costs`, `native-staffed-throughput`). Each stays `incomplete` until a native record that measures that row reports; an unrelated rig pass does not clear it.
- The price response does not clear those rows. A declared `--price-weight` makes the stage `incomplete`. The default and the superseded Pareto item follow the [price-weight decision](../../docs/decisions/2026-11-08-price-weight.md).

## Provenance

`supported-game.json` holds fingerprint sets (assembly hashes only). An `unknown` set adds a required native `game-fingerprint` item. `native-assumptions.json` lists the native assumptions, the rig check that proves each and its last rig evidence; `rig-manifest` marks rows stale, unproven or fresh and orders the next rig batch. Nothing here blocks a merge.

## Lock

The runner keeps its own OS lock on `<temp>/persistentcity-acceptance.lock` (exit 4 while held) and waits before each .NET step while another test run is active (`--no-wait` refuses instead). This is the runner's behaviour only; implementers have no shared dotnet lock and do not wait for other test runs.

Exit codes: 0 pass or incomplete, 1 fail, 2 usage, 3 incomplete with `--strict`, 4 lock held or other tests running with `--no-wait`.
