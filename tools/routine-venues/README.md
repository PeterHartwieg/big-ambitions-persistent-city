# Optional venue feasibility

The 23 October campaign compares frozen `baseline-source` (previous behaviour plus optional miss diagnostics) against `candidate-source` from the same isolated PC checkpoint, both at one-third calendar speed. It reuses the owned backup, build, deployment, audit and restoration tools in `tools/food-demand-audit` with `PC_FOOD_CAMPAIGN=routine-venues-20261023`. Read the sibling Driven Rides `tools/agent-tests/README.md` before contacting the rig. Do not deploy into normal profiles.

The helper's routine trace explains each missed-activity increment and records candidate rejections separately. The baseline is observed, the owned rig stopped, the candidate deployed with `update_helper.py`, and the same save reloaded. After settlement/reload/replay checks, `session.py restore` verifies all preexisting files, preferences and manifest, removes the helper and releases ownership. Ignored campaign output retains both source snapshots, helper fingerprints and failure/cleanup receipts.

Offline verification:

```sh
python3 tools/routine-venues/verify.py build/routine-venues-20261023
DOTNET_TieredCompilation=0 dotnet run --project tools/routine-venues/replay/RoutineVenues.csproj -c Release -- build/routine-venues-scale.json
```

The scale check uses 400 synthetic mixed-hours venues at 1,000/2,000/5,000 residents and requires identical saved outcomes at event budgets 1 and 128. Native navigation and rendered bodies are excluded.
