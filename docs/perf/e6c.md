# E6c: vendor table save cost (7 November 2026, offline)

Plan: [E6 section 6](../plans/e6-carts-food-trucks.md#6-performance), row E6c. Checks: [docs/checks/e6c.md](../checks/e6c.md). E6c adds no per-frame code. Nothing in the game build creates vendor rows yet (E6d wires the market), so a live city saves `"Vendors":[1,[]]`, 16 characters, and `"Channel":0,` (12 characters) per scope. The numbers below are what the table adds once vehicles exist.

## Method

The opt-in test `E6cVendorSaveMeasure.SaveBytesAndReadWriteTime`:
`PC_E6C_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~E6cVendorSaveMeasure --logger "console;verbosity=detailed"`.
Release build, .NET 10, Apple M1 Pro (macOS, arm64), one foreground dotnet process. Three warm-up round trips, then 200 timed `SaveCodec.Write` and `SaveCodec.Read` pairs on the small city and 20 on the representative one, timed with `Stopwatch`.

Two cities. The small one is the one-resident, two-shop food city (`WeightedFoodChoiceTests.Ready`), so the difference between rows is the vendor cost. The representative one is F7c's `representative-957x2` fixture. `VendorSaveTests.Fill` adds the rows. Every live and retired row has a stand, a vendor place and a Vehicle scope with two menu items. Every row, tombstones included, gets one business reference, as a remembered vehicle has. These are synthetic offline numbers, not game, Mono or rig evidence.

## Results

"Added" is the whole payload's growth over the same city with no rows. It includes the vehicle scopes and business references, not only the table.

| City | Rows | Payload | Added | Table alone | Write p50 / p95 / max | Read p50 / p95 / max |
| --- | --- | ---: | ---: | ---: | --- | --- |
| small | 0 | 3,010 | 0 | 16 | 0.316 / 0.546 / 4.636 ms | 0.737 / 1.184 / 2.150 ms |
| small | 8 live | 5,854 | 2,844 | 875 | 0.524 / 0.665 / 2.035 ms | 1.468 / 1.802 / 2.789 ms |
| small | 64 live | 25,646 | 22,636 | 7,005 | 0.746 / 1.005 / 1.876 ms | 2.066 / 3.112 / 4.101 ms |
| small | 64 live + 64 retired | 48,417 | 45,407 | 14,132 | 1.063 / 1.999 / 3.540 ms | 2.418 / 3.759 / 5.653 ms |
| small | 1,024 tombstones | 128,874 | 125,864 | 70,671 | 1.524 / 3.357 / 21.479 ms | 4.227 / 6.336 / 10.672 ms |
| small | full table (64 + 64 + 1,024) | 174,521 | 171,511 | 84,788 | 1.738 / 3.772 / 30.759 ms | 5.313 / 6.466 / 75.020 ms |
| 957x2 | 0 | 2,311,343 | 0 | 16 | 29.1 / 45.7 / 46.9 ms | 79.5 / 91.2 / 92.3 ms |
| 957x2 | 8 live | 2,314,145 | 2,802 | 875 | 29.8 / 38.6 / 41.0 ms | 81.6 / 91.7 / 96.7 ms |
| 957x2 | 64 live | 2,333,937 | 22,594 | 7,005 | 31.8 / 35.0 / 43.6 ms | 81.2 / 91.3 / 94.9 ms |
| 957x2 | 64 live + 64 retired | 2,356,708 | 45,365 | 14,132 | 32.2 / 38.9 / 41.6 ms | 81.5 / 92.6 / 94.1 ms |
| 957x2 | 1,024 tombstones | 2,437,207 | 125,864 | 70,671 | 32.1 / 41.8 / 48.6 ms | 82.4 / 98.2 / 101.8 ms |
| 957x2 | full table | 2,482,812 | 171,469 | 84,788 | 35.2 / 44.7 / 56.4 ms | 84.2 / 96.4 / 104.6 ms |

Payload sizes are in characters. Every fixture is ASCII, so UTF-8 bytes equal characters. A live or retired row costs about 110 characters in the table and about 245 more in its scope. A tombstone costs about 69 in the table, and its reference about 54.

At the full table the vendor state adds about 3.2 ms p95 to a save and 5.3 ms p95 to a load in the small city. In the 957x2 city, the extra write time (+6 ms p50) is the same size as the run-to-run spread of the base city, which was 33.1 ms p50 in the first run and 29.1 ms in this one. A realistic city with up to 8 vehicles adds under 1 ms to either.

**Flagged outliers (save and load path, not mod-work).** The small city's full-table arm had one write at 30.8 ms and one read at 75.0 ms, and the 1,024-tombstone arm one write at 21.5 ms. The first measured run of the same arms, before the decode change below, had maxima of 6.96 ms and 10.73 ms. These look like garbage collections landing in a timed call; the measurement does not separate GC time. They happen only inside save and load.

## Size against the plan's estimate

Plan section 5 estimated at most 128 × 600 + 1,024 × 400 = 486,400 characters. The full table with its scopes and references measures 171,511 characters, about 35 % of the estimate, and it stays under the 2 MiB business-reference budget and the 256 MiB payload limit.

## Perf decisions

- **One decode fewer on read (taken).** The reader decoded the vendor table three times: once for `WireContext` (so `v` indices resolve before business references), once in the streaming shape check, and once in deserialization. The converter now takes the table `WireContext.FromSource` already decoded, and skips the tokens. Small city, full table, read: p50 5.755 → 5.313 ms, p95 6.937 → 6.466 ms. The p50 dropped about 8 %, and run-to-run noise is about the same size, so I count it as a small win, not a measured one. Before: the first measured run on this branch; after: the table above.
- **Place and business ids stay on the wire** even though they are derived. The reader checks them against the derivation, so a row whose escaping changed would be refused, not silently re-keyed. Dropping them would save about 50 characters per row, under 60 KB at the full table. That is not worth losing the check.
- The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are not touched.

## Gate

E6c adds no mod-work. Mod-work p95 stays under 5 ms because the table runs only inside save and load. The outliers above are flagged even though they are on the save path. Whole-frame timing is not measured offline. The batched rig measures save and load with vehicles in a real city (check 6 in the checks file).
