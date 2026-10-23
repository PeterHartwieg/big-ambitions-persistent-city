# Food budgets and outside-choice comparison

Replays the retained 22 October PC observation with the previous common-$18 policy, varied individual allowances, and varied allowances plus home/packed-food preferences. This tool performs no game calls, native writes or rig connection.

From the repository root:

```sh
dotnet run --project tools/food-household-comparison/FoodHouseholdComparison.csproj --configuration Release -- build/food-demand-curve-20261022 build/food-household-budgets-20261022/comparison
python3 tools/food-household-comparison/plot.py build/food-household-budgets-20261022/comparison
dotnet run --project tools/food-household-catalogue-regression/FoodHouseholdCatalogueRegression.csproj --configuration Release
dotnet test tests/PersistentCity.Tests.csproj
```

The input directory must contain `native-result.json`, `probe-output/observed-routines.json` and `probe-output/controlled-initial.json` from the retained PC curve run. Its 31 original price points must reproduce after schema migration. Two new policy variants replay the same price sweep, checking opportunity counts, stock and unchanged buying power within each variant. `report.json`, the new policy's initial/final experiment documents and PNG/SVG/PDF charts are written to the output directory. Chart generation requires matplotlib.

The retained capture has no verified home-district composition. Its curves therefore use the explicit neutral individual-budget prior. The second chart uses separate controlled 80/15/5 and 15/45/40 working/middle/upper distributions with the same resident IDs and seed. These are sensitivity examples, not observed home demographics. Do not relabel identity donor districts as home districts.

All results remain hypothetical purchases within an eight-shop market with controlled availability. A declined purchase is not a fulfilled meal, and no store receives a customer floor. See [policy and results](../../docs/reviews/2026-10-22-food-household-budgets.md).
