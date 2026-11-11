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

Saves: own state is in `persistentcity:state_v1`, written at `CityState.CurrentSchema` (`Core/Model.cs`), which is held at 1 until release. Cities from an older development build are refused with a "start a new city" status, not migrated; invalid data is refused without overwriting it ([decision](docs/decisions/2026-11-09-schema-one.md)).

## Build and test

Requires the .NET 10 SDK and Python 3.

```sh
python tools/build.py --game-managed "/path/to/Big Ambitions_Data/Managed"
dotnet test tests/PersistentCity.Tests.csproj   # full suite; while working use python tools/test-focused.py
python tools/docs-links/check.py
```

The build references game assemblies in place; none enters the package. Output is `build/PersistentCity/`, including `Dependencies/0Harmony.dll`. Install it in `ModsLocal/PersistentCity` beside a reviewed RLO (0.41.1 or 0.40.4) with RLO Social Media enabled. Use disposable test cities only; this is not cleared for ordinary saves. Settings are in [behaviour.md](docs/behaviour.md#settings).

## Status

Not released. Nothing is verified by rig yet. What is done, what remains and in which order is in the [roadmap](docs/roadmap.md).

## Links

- [AGENTS.md](AGENTS.md): rules for implementers and reviewers.
- [Roadmap](docs/roadmap.md): direction, done and remaining work, and the performance gate.
- [City direction, 10 November 2026](docs/decisions/2026-11-10-city-direction.md) and the [9 November review](docs/reviews/2026-11-09-city-review.md) behind it.
- [Behaviour](docs/behaviour.md), [glossary](docs/glossary.md), [rig checklist](docs/rig-checklist.md).
- [Decisions](docs/decisions/) and open [plans](docs/plans/).
- Contracts: [RLO contract](docs/rlo-contract.md), [RLO bridge](docs/rlo-bridge.md), [save format](docs/save-format.md), [food product metadata API](docs/food-product-metadata-api.md).
- History before 10 November 2026 is in the tag [`docs-history-2026-11-10`](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10) ([why](docs/decisions/2026-11-10-lean-docs.md)).
