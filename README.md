# Persistent City

An independent Big Ambitions mod that requires Real Life Overhaul (RLO). Working name; not released. There will be one release, after the remaining work in the [roadmap](docs/roadmap.md).

## What it does

- Builds a persistent population from the city's rentable and buyable homes. Each resident has a stable ID, a home, a daily routine of work or study, shopping and leisure, and saved journeys that continue when the player is elsewhere.
- Walks and drives residents on the game's own navigation mesh and road network. Nearby residents get native bodies; distant ones travel in the background. Citywide cars queue on finite road sections.
- Runs food demand for the player's eligible food businesses. It takes them over automatically at a native accounting boundary, and sales go through native checkout and settlement. Residents choose by taste, price, distance, memory of past visits, active campaigns, weather and rival shops. Unsupported businesses stay native-owned.
- Shows a resident inspector and a business feedback panel (**Ctrl+F8**).

Details and limitations: [docs/behaviour.md](docs/behaviour.md). Terms: [docs/glossary.md](docs/glossary.md).

## Architecture

| Folder | Role |
| --- | --- |
| `Core/` | Pure C# simulation (netstandard2.1): population, routines, traffic, food demand, money |
| `Persistence/` | Save codecs and stores; the schema `CityState.CurrentSchema` is held at 1 |
| `Runtime/` | Unity and game integration, Harmony hooks, physical bodies, native food market |
| `Compatibility/` | The only access to RLO |
| `tests/` | xUnit tests (`PersistentCity.Tests.csproj`) |
| `tools/` | Build script, harnesses and measurement tools |

Saves: own state is in `persistentcity:state_v1`, written at `CityState.CurrentSchema` (`Core/Model.cs`), which is held at 1 until release. Cities from an older development schema are refused with a "start a new city" status, not migrated; any other schema number or invalid data is refused without overwriting it ([decision](docs/decisions/2026-11-06-delivery-rules.md)).
Superseded for schema numbers on 9 November 2026: the schema is held at 1 and never bumped before release ([decision](docs/decisions/2026-11-09-schema-one.md)).

## Build and test

Requires the .NET 10 SDK and Python 3.

```sh
python tools/build.py --game-managed "/path/to/Big Ambitions_Data/Managed"
dotnet test tests/PersistentCity.Tests.csproj   # full suite; while working use python tools/test-focused.py
python tools/docs-links/check.py
```

The build references game assemblies in place; none enters the package. Output is `build/PersistentCity/`, including `Dependencies/0Harmony.dll`. Install it in `ModsLocal/PersistentCity` beside a reviewed RLO (0.41.1 or 0.40.4) with RLO Social Media enabled. Use disposable test cities only; this is not cleared for ordinary saves. Settings are in [behaviour.md](docs/behaviour.md#settings).

## Status

Status words: planned, implemented, partial, stopped, verified by rig. Rig is "pending" until the next batched rig run verifies a slice; nothing is verified by rig yet. Per-slice evidence is in [CHECKS.md](CHECKS.md).

| Slice | Status | Rig |
| --- | --- | --- |
| A1–A3, A5 inspector, traits, weighted choice, integrated acceptance | implemented | pending |
| B0, B0b, B1, B2 memory, loyalty, regulars | implemented | pending |
| C1, C2a, C2b campaign reader and awareness | implemented | pending |
| C3 agency payment discovery (no safe fee path found) | implemented | pending |
| C4 market research engine, C5a–C5f paid research through the native agency | implemented | pending |
| D1, D2, D3a, D4 rival catalogue, choice, outflow, memory | implemented | pending |
| E1 weather, E2a–E2c street life and interior guard | implemented | pending |
| E3 per-product feasibility (donut, cupcake, salad, ice cream, French fries, pizza, six more) | stopped | pending |
| E4 wider-menu envelope | implemented | pending |
| E5a, E5c, E5d, E5e catalogue reader, metadata, names, metadata API | implemented | pending |
| E5b, E5b2–E5b5 complete-menu support | implemented | pending |
| E6b–E6f carts and food trucks | implemented | pending |
| E6a Street Food vendor API (Street Food repository) | planned | pending |
| Track F: F0 interior view experiment (default off) | implemented | pending |
| Track F: F1 shop view, F1b street proxy | stopped | pending |
| Audit F1 accounting backpressure, F3 bounded bulk work | implemented | pending |
| Audit F2a–F2h movement: bodies follow logical journeys | implemented | pending |
| Audit F4a–F4e perf gate contract and tooling | implemented | pending |
| Audit F4f gate run record | planned | pending |
| Audit F5a–F5i population: census, identity, schedules, employment, visitors | implemented | pending |
| Audit F6a–F6k economy: ledger, attended wages, household resources, feedback, calibration | implemented | pending |
| Audit F7a–F7d, F7f, F7p saves: cost, epoch, snapshot split, compact writer | implemented | pending |
| Audit F8a capability model | implemented | pending |
| Audit F8b–F8g capability report and contracts | partial | pending |
| Audit F9a, F9c, F9e acceptance runner, city fixture, read-only views | implemented | pending |
| Audit F9f movement in the integrated city | planned | pending |
| X1–X3 codec cleanup, file split, schema held at 1 | implemented | pending |
| X4–X7, X9 a tree agents can read | planned | pending |
| Track P saves and spikes | planned | pending |
| R0 prove a sale in game; J0 joint plan for days, needs and sales | planned | pending |
| Track G the living city | planned | pending |
| Track R sales at every business | planned | pending |

## Links

- [AGENTS.md](AGENTS.md): rules for implementers and reviewers.
- [Roadmap](docs/roadmap.md): direction, remaining, deferred and superseded work, and the performance gate.
- [City direction, 10 November 2026](docs/decisions/2026-11-10-city-direction.md) and the [9 November review](docs/reviews/2026-11-09-city-review.md) behind it.
- [CHECKS.md](CHECKS.md): index of per-slice evidence.
- [Plans](docs/plans/): F5 population, F6 economy, F7 saves, E5 products, Track F, audit F1.
- [Decisions](docs/decisions/) and [archive](docs/archive/).
- [Design](docs/design.md), [RLO contract](docs/rlo-contract.md), [RLO bridge](docs/rlo-bridge.md), [food product metadata API](docs/food-product-metadata-api.md).
