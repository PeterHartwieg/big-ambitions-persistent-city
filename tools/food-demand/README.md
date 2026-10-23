# Food-demand experiment

Run from the repository root:

```sh
dotnet run --project tools/food-demand/FoodDemand.csproj -- build/food-demand-demo
```

Open the generated `index.html`. Choose a scenario and a resident/day to inspect which shops were considered and why a meal was fulfilled or unmet. `initial.json` and each scenario's final JSON retain the complete experiment state. No game process or save is read by this command.

To compare an explicitly prepared experiment document:

```sh
dotnet run --project tools/food-demand/FoodDemand.csproj -- build/my-food-comparison /absolute/path/initial.json expected-city-key
```

The input must be an unstarted schema-1 `FoodShadowState` document with one intention per resident/day, explicit provenance and route quotes. Missing stock and routes remain unknown. The comparison changes the business with the last ordinal ID, identifies it in the report, and writes four separate scenario outputs. It refuses an input path that collides with one of its output files.

Saved in-progress state can be resumed through `FoodShadowCodec.Read` and `new FoodDemand(state).AdvanceTo(...)`. The comparison CLI deliberately accepts only unstarted states so its baseline and interventions begin from the same point.

Scope, native integration findings, validation and remaining work: [docs/food-demand.md](../../docs/food-demand.md).
