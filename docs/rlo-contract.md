# RLO contract

Persistent City requires the independently installed Real Life Overhaul (RLO) mod. All RLO access is in `Compatibility/`. Persistent City never writes RLO data, never initializes RLO and never makes replacement identities when RLO is missing or refused.

## Two read paths

1. **CityApi first.** When the loaded RLO has `RealLifeOverhaul.ModApi.CityApi` with `ContractVersion` 1, all three adapters (`RloIdentityAdapter`, `RloPhysicalReservations`, `RloRoadSpeedOverrides`) read through it on any RLO release. `Compatibility/RloBridge.cs` binds every listed member once per assembly. A CityApi with another contract version, or with a missing or changed member, is refused as `Incompatible` with no reflection fallback.
2. **Reflection fallback.** Without CityApi, the adapters read RLO by reflection and accept only reviewed releases: 0.40.4, 0.41.1 and 0.45.0 (`RloIdentityAdapter.ReviewedVersions`). The assembly name must be `RealLifeOverhaul` and the assembly and informational versions must form a reviewed pair. Any other release is refused.

Why a versioned API instead of looser checks: the fallback reads private fields whose meaning RLO never promised, such as which person a social-cast slot indexes. A name-and-type check would pass a refactor that changed that meaning, and Persistent City would then reserve the wrong citizen. The road-speed fallback also recomputes RLO's pothole rule (`x <= 20 ? x : Max(20, x - 15)`), so any retune would break it. A contract owned by RLO puts the dependency where the code changes. The design and promises are in the [bridge note](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/rlo-bridge.md); RLO's half was proposed upstream in [AdultCudi/real-life-overhaul#24](https://github.com/AdultCudi/real-life-overhaul/issues/24).

## CityApi contract 1, as Persistent City relies on it

- Calls are main-thread only, never throw, never change RLO or the game, and return a status: `Ready`, `Off`, `NotYet`, `Refreshing`, `Shared` or `Failed`.
- `ReadCitizens` gives the same citizens field for field for the same account and city seed on every release with contract 1. It answers `Refreshing` instead of a mix of old and new citizens.
- `ReadReservedCitizens` and `ReadReservedBodies` report the people and bodies RLO holds now. Any answer other than `Ready` or `Off` reserves everyone until a complete answer arrives; `Shared` or `Failed` stops the guard.
- `ReadSlowedLanes` reports original and current limits. The road snapshot refuses any answer but `Ready` or `Off`, a lane listed twice, or a limit of 0 or less.
- RLO may add members; removing or changing a listed one raises the contract version.

## Identities

The identity read checks exactly 400 citizens with unique indices 0 to 399, full names, ages 1 to 130, the eight known personas, nonzero appearance seeds, and avatar age and sex that agree with the citizen. It confirms the director is still running on the same file and seed at the end of the read.

| Persistent City identity | RLO source |
| --- | --- |
| `Id` | `c` plus `FlareCitizen.Index` |
| `Name` | `First + " " + Last` |
| `District` | `District`; null maps to an empty string |
| `Age`, `Female`, `Persona` | Copied citizen values |
| `AppearanceSeed` | `FlareAvatarRules.Citizen(CitySeed(File), Index).Seed` |

Identities are detached copies. `PopulationKey` is `rlo:0.40.4:` plus a SHA-256 digest over the city seed and every identity field, sorted by citizen ID. The prefix names the original donor contract, not the installed release; both read paths give the same key. A changed key is refused.

The donor list seeds the city; it does not size it. Housing capacity sets the population. The saved `SourceIdentities` catalogue is authoritative after activation: new residents get `pc:<sequence>` IDs remixed from it, and bodies are dressed by the game's own appearance setter from the saved seed.

## Readiness

`RloReadStatus` is `Ready`, `MissingDependency`, `Incompatible` or `NotReady`, with a `DonorReadReason` for the cause.

- At city creation and activation RLO must be `Ready` and, on load, match the saved key. Missing RLO waits with a status line; incompatible RLO stops the city.
- During play the periodic check keeps a city running and saving on its saved catalogue while RLO is `Off`, `NotYet` or `Refreshing`. A different catalogue, `Shared`, `Failed` or `Incompatible` stops it. Persistent City refuses Going Public sessions.
- Call on the main thread after RLO's own startup and retry `NotReady` on a later update.

## Overlap with RLO 1.0: Persistent City overrules

Owner decision of 12 November 2026 (#239): Persistent City stays independent, stays compatible with RLO 1.0 and overrules RLO where the two overlap. Rules are in `Core/RloOverrule.cs`; the runtime side is `Runtime/RloOverrules.cs`.

- **Living population switch.** `Compatibility/RloLivingPopulation` reads `RealLifeOverhaul.Settings.LivingPopulation` as Absent, NotInRelease (0.4x), Off, On or Unknown. A missing or changed surface on a release that has the switch reads Unknown, never Off. A mid-session toggle is followed at most once a real second.
- **Owned shops.** An owned food shop takes no native draw, batch or customer during owned hours; Persistent City tickets are its only sales. Replaced customer calculators (RLO's five subclasses) are overruled there, not refused. Foreign Harmony prefixes on the checkout hooks still refuse.
- **Unknown switch.** At load, Unknown refuses `food.checkout.shop` and every shop stays native. During play, Unknown defers new takeovers; owned hours keep their gate, and hand-backs never wait. RLO presence, Living on or off, and calculator replacement never refuse checkout.
- **Baskets and charges.** RLO loyalty and interiors lines stay on native entries; owned checkout sells private orders only. Persistent City books no native rent, wage or tax; RLO household books stay RLO's.
- **Crowds.** Ordinary standers are off in every native zone, including RLO's landmark, campus and Uptown copies. RLO's own scene people and its CityPeople pool are untouched.
- **Traffic.** While Persistent City cars hold the ordinary native groups, RLO's TrafficAccidents and NaturalAccidents switches are held off, an accident under way is ended through `TrafficAccidents.EndActive()`, and the player's stored options are given back on release. Native cars loaded straight into a held group are retired before they render. Cars RLO holds are never retired.
- `pc capability status` and the activation log carry one RLO overrule line.

## Evidence

Offline tests cover both read paths, the overrule rules and the stub-compiled `tools/capability-contracts` arms. Released-binary and earlier rig evidence for 0.40.4 and 0.41.1 is in the [history tag](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/rlo-contract.md). The RLO 1.0 overrule and the CityApi path have no rig evidence yet; their in-game checks are in the [rig checklist](rig-checklist.md).
