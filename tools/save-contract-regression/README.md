# Save contract cache regression

Run `python tools/save-contract-regression/compare.py --baseline <pre-change Core/Persistence sources> --output <ignored directory>`.
Run `dotnet run --project tools/save-contract-regression -c Release` for the current tree.
Add `--bench` to measure save size and time at 957 and 5,000 residents, and `--ceiling` for the 50,000-resident codec ceiling as well.

The same 120-resident fixture builds against both source trees. Compare exact save SHA-256, length and missing/future/malformed/unknown-field refusal outcomes. When the two trees write different schemas (for example 16 and 17), the save digest and length differ by design and are reported, not compared. After warming metadata, measure thread allocations for 20 writes and 20 reads separately. The comparator excludes allocation totals from equality; output changes fail. The existing core-identity comparison additionally covers seven complete city saves including a mid-commute reload. `PersistenceTests.ConcurrentCodecsShareOnlyMetadataAndStillRejectInvalidState` covers concurrent independent cities and refusals after warm-up.

`EconomyScenario.cs` is a fixed three-day food city: 60 residents, two consumers each, three meals, household finances, three shops, checkout refusals, lost acknowledgements with later receipt proof, and a save/reload mid-run. It uses only API that exists before and after schema 17. Its order log, purchases, money and its save without the schema-17 fields must be identical in both trees. `FoodCommerceTests.S17_EconomyScenarioMatchesThePreSchema17Baseline` pins the schema-16 baseline values.

`--bench` builds a resident food city at the runtime representation factor (two consumers per resident) with household finances. On a schema-17 tree, each consumer carries an end-of-day image with three recorded outcomes, the largest steady state. It reports payload characters, the schema-16 image of the same city, median write/read time, time to read and upgrade the schema-16 image, and memory retained by a loaded state. On a schema-17 tree, the hot section reports trait generation and outcome-update cost. Cities above 5,000 residents grow through housing expansion from 1,000 imported donors, as real cities do.

These are .NET 10 synthetic checks, not Unity/Mono measurements or evidence that the O(city) save path is solved. No save validation was removed.
