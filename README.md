# Persistent City

An independent Big Ambitions mod that keeps a persistent city of residents, journeys and traffic, and connects them to the player's food businesses. It requires Real Life Overhaul (RLO), installed separately. Working name; not released. There will be one release, after the remaining work in the [roadmap](docs/roadmap.md).

## What it does

- Builds a population from the city's homes. Each resident has a stable ID, a home, a daily routine and saved journeys that continue while the player is elsewhere.
- Walks and drives residents on the game's navigation mesh and roads. Nearby residents get native bodies and cars; distant ones travel in the background on finite road sections.
- Runs food demand for the player's eligible food businesses. It takes them over at a native accounting boundary, and sales go through native checkout. Unsupported businesses stay native-owned.
- Overrules RLO where the two overlap (owned shops, crowds, traffic). See the [RLO contract](docs/rlo-contract.md).
- Shows a resident inspector and a business feedback panel (**Ctrl+F8**).

## RLO

Persistent City reads RLO through RLO's `CityApi` contract 1 when the loaded release has it, on any release. Without it, a reflection fallback accepts only the reviewed releases 0.40.4, 0.41.1 and 0.45.0. Anything else is refused, and no replacement identities are made. Details: [RLO contract](docs/rlo-contract.md).

## Build and test

Requires the .NET 10 SDK and Python 3.

```sh
python tools/build.py --game-managed "/path/to/Big Ambitions_Data/Managed"
python tools/test-focused.py
python tools/docs-links/check.py
```

The full suite is `dotnet test tests/PersistentCity.Tests.csproj`. The build references game assemblies in place; none enters the package. Output is `build/PersistentCity/`, including `Dependencies/0Harmony.dll`. Install it in `ModsLocal/PersistentCity` beside a supported RLO. Use disposable test cities only; it is not cleared for ordinary saves.

Own state is saved as direct JSON in `persistentcity:state_v1` at schema 1 (`Persistence/SaveCodec.cs`). Unreadable data and any other schema are refused without being overwritten.

## Status

Not released. Implemented work and its proof state, including what is verified by rig, are in the [roadmap](docs/roadmap.md). Pending in-game checks are in the [rig checklist](docs/rig-checklist.md).

## Links

- [AGENTS.md](AGENTS.md): rules for implementers and reviewers.
- [Decisions in force](docs/decisions/2026-11-10-city-direction.md) and the [price weight](docs/decisions/2026-11-08-price-weight.md) decision.
- Contracts: [RLO](docs/rlo-contract.md), [food product metadata API](docs/food-product-metadata-api.md). Reader corpus: [tests/ReaderCorpus](tests/ReaderCorpus/README.md).
- Older records are in the tag [`docs-history-2026-11-10`](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10).
