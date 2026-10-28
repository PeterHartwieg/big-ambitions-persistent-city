# Save contract cache regression

Run `python tools/save-contract-regression/compare.py --baseline <pre-change Core/Persistence sources> --output <ignored directory>`.
Run `dotnet run --project tools/save-contract-regression -c Release` for the current tree.

The same 120-resident fixture builds against both source trees. Compare exact save SHA-256, length and missing/future/malformed/unknown-field refusal outcomes. After warming metadata, measure thread allocations for 20 writes and 20 reads separately. The comparator excludes allocation totals from equality; output changes fail. The existing core-identity comparison additionally covers seven complete city saves including a mid-commute reload. `PersistenceTests.ConcurrentCodecsShareOnlyMetadataAndStillRejectInvalidState` covers concurrent independent cities and refusals after warm-up.

These are .NET 10 synthetic checks, not Unity/Mono measurements or evidence that the O(city) save path is solved. No save validation was removed.
