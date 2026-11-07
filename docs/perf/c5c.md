# C5c: research save section cost (7 November 2026, offline)

Plan: [C5 section 4, row C5c](../plans/c5-agency-research.md#c5c--save-schema) and section 5. Checks: [docs/checks/c5c.md](../checks/c5c.md). C5c adds no per-frame code. Nothing places research orders in the game build yet (C5e wires the dialog), so a live city saves an empty ledger, `"Research":[1,1,[],[],[]]`, which is 26 characters. The cost below is what the section adds to a save and a load once orders exist.

## Method

Test `ResearchSaveTests.MeasureSectionCostAtBounds`, opt-in. Run it with `PC_C5C_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~ResearchSaveTests.MeasureSectionCostAtBounds --logger "console;verbosity=detailed"`. Release build, .NET 10, Apple M1 Pro (macOS, arm64), one foreground dotnet process. 200 warm-up round trips, then 2,000 timed `SaveCodec.Write` and `SaveCodec.Read` calls per arm, timed with `Stopwatch`. Allocation comes from `GC.GetAllocatedBytesForCurrentThread`.

Both arms use the minimal one-resident city (`CurrentSchemaOnlyTests.City`), so the difference between them is the section. The bounded arm holds `ResearchSaveTests.AtBounds()`: 8 Surveyed open orders, 16 kept Paid orders and 16 closed orders, all DetailedLocal. That is the widest tier sold (62 cells), and its report also carries the business address. Every business, agency and report address is the 256-character maximum (review Sol-r1-1; the first version used a short agency address and city reports without an address). Cell values are the fixture's 40-consumer counts, not the 6-digit cap; the size section below bounds that difference.

These are synthetic offline numbers, not game, Mono or rig evidence.

## Results

| Arm | Payload | Write p50 / p95 / max | Read p50 / p95 / max | Allocation per write + read |
| --- | ---: | --- | --- | ---: |
| Empty ledger | 1,780 B | 0.028 / 0.046 / 0.137 ms | 0.072 / 0.113 / 0.912 ms | 73,323 B |
| Section at its bounds | 48,089 B | 0.150 / 0.195 / 1.573 ms | 1.058 / 1.850 / 4.648 ms | 949,654 B |

At its bounds the section adds about 0.14 ms p95 to a save and about 1.7 ms p95 to a load. Both run only at save and load, never on a per-frame path. Neither comes near the 30 ms flag.

Most of the read cost comes from Json.NET, not from the research code. The reader makes four token passes over the payload (schema probe, place context, streaming shape check, deserialization) and then validates. One bare `JsonTextReader` pass over the first version's 36 KB bounded payload took 0.20 ms. The streaming check and the deserializer each decode the section and run `ResearchLedger.TryRestore`, and the final validation runs it once more. I left that alone: at most 40 orders, once per load.

## Size against the plan's estimate

Plan section 5 estimated the section at "well under 20 KB". At its bounds it is 46,309 bytes over the empty ledger. The estimate left out the address bound: three 256-character addresses on each of 24 report-holding orders and two on each of 16 closed ones come to about 26 KB on their own. Cell values at the 150,000-consumer cap would add at most 4 digits to each of the 1,488 cells, about 6 KB, so the true maximum is about 52 KB. With 32-character addresses, closer to native address strings, the same bounded ledger writes a 24,793-byte payload, a section of about 22.5 KB. The payload limit is 256 MB. `TheSectionAtItsBoundsRoundTripsAndStaysUnderSixtyFourKilobytes` asserts that the section plus that 6 KB cell allowance stays under 64 KB.

## Perf decisions

- **Failure messages built only on failure.** The order and cell loops first passed concatenated messages to `Require`, so every cell of every load built a string. They now call `Fail` behind a condition, or use constant text. Measured on the first version's 36 KB bounded fixture, read went from p50 1.017 ms / p95 1.836 ms and 1,144,456 B to p50 0.960 ms / p95 1.625 ms and 809,171 B.
- **Cell codes stay on the wire.** Each cell keeps its metric and band codes next to its value, so a saved report describes itself and the reader can refuse a layout that C4 does not produce. Writing values only would save about 9 KB at the bounds. It is not worth a payload that depends on the engine's table order.
- The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are not touched.

## Gate

C5c adds no mod-work. Mod-work p95 stays under 5 ms because the section runs only inside save and load. No spike reaches 30 ms; the largest single load at the bounds is 4.6 ms. Whole-frame timing is not measured offline. The batched rig measures save and load with orders in a real city (check 6 in the checks file).
