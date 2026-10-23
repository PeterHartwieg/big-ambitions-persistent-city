# Native food comparison

A bounded diagnostic of directional responses, not an exact-sales calibration or a replacement economy. Peter's criterion is directional alignment with more variety through semi-realistic simulation.

1. Build `python3 tools/food-native-comparison/build_probe.py` against the installed isolated Mac game's assemblies.
2. Once the Mac rig is free, run `python3 tools/food-native-comparison/run_mac.py`. It refuses an occupied rig, acquires its own reservation, clones and hashes the current profile, installs only the diagnostic helper, starts a fresh disposable FT city, captures twice while paused, then stops, restores the previous files/preferences/manifest and releases ownership. New files belonging to the disposable test city are retained and listed. It never contacts Windows.
3. Run `dotnet run --project tools/food-native-comparison/Compare.csproj --configuration Release -- build/food-native-comparison-20261021/mac-run/native-0.json build/food-native-comparison-20261021/results`.

Append a third argument (`0`–`3`) to select a different observed target shop; use a separate output directory for each comparison.

The native probe reads existing registrations and chooses up to four businesses advertising a common food. It invokes the actual native retail calculator and purchase-price logic with copied retail-price lists, plus closed, missing-producer and unavailable-service checks. It restores Unity's random state and verifies native market/order/inventory fields are unchanged. Active Harmony owners are recorded. Native code, game binaries, raw saves and generated captures remain outside Git.

The comparison runs the current `FoodDemand` model for twelve seeded cohorts of 240 synthetic residents, seven days each. Prices and schedules come from the capture. Allowances, preferences, meal windows and travel quotes are controlled experiment inputs; they are not observations of the game's residents or routes. Finite plentiful stock isolates choice effects, followed by separate shortages. A constant-context run measures whether residents vary between days without a changing market.

Interpret each layer separately:

- Native hourly calculator results are potential customers before AI random thinning, basket composition, stock and checkout. They are not measured sales, resident counts or a demand scale target.
- Native price acceptance is evaluated by social class. Successful detached quotes are not completed economic transactions.
- Missing producers and missing service exercise native refusal paths. They do not prove that a live shop had empty shelves or lacked staff.
- Shadow fulfillment needs stock, affordable food and supplied feasible travel, but currently has no service-capacity model. That difference is a finding, not something to conceal by tuning.
- Compare signs, switching and distribution. Revenue need not move monotonically with price. Matching native totals or inventing external residents is not a success criterion.

No changes to the production mod or economic authority are made by this tool.

Completed Mac campaign and interpretation: [21 October native comparison](../../docs/reviews/2026-10-21-food-native-comparison.md).
