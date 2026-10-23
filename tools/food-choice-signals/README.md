# Food choice: service quality and district appeal

Run the controlled comparison and its small interactive explanation:

```sh
dotnet run --project tools/food-choice-signals/FoodChoiceSignals.csproj --configuration Release -- build/food-choice-signals-demo
dotnet run --project tools/food-demand/FoodDemand.csproj --configuration Release -- build/food-choice-signals-insight build/food-choice-signals-demo/insight-input.json choice-signals-fixture
```

The full comparison uses 957 synthetic residents over seven days with equal route durations, temporary allowances and deliberately ample service/stock. It isolates service-quality and district-product-appeal trade-offs. The HTML input contains 24 residents and reruns that subset independently. No native game is launched or modified.

Higher customer service can justify a modest premium for residents who value it. Higher district product demand improves an offer's relative appeal, with a smaller provisional weight. Neither is a population or service-capacity multiplier. Unknown observations contribute no adjustment. The native demand percentage already includes supplier saturation; do not interpret it as a count of buyers or apply it again to every shop's customer allocation.

See [implementation and evidence](../../docs/reviews/2026-10-22-food-quality-demand.md) for the exact policy, current limitations and native validation boundary.
