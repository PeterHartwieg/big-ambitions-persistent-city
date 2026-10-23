# RLO identity contract

Persistent City requires the independently installed Real Life Overhaul mod. Compatibility explicitly permits [RLO v0.41.1](https://github.com/AdultCudi/real-life-overhaul/releases/tag/v0.41.1), source commit `dbd15c0958770219c0f4d969a8b2ac28593014d2`, and the previously reviewed [v0.40.4](https://github.com/AdultCudi/real-life-overhaul/releases/tag/v0.40.4), source commit `46b3b8835e113ec3807344a9b7899439684004ad`. Other releases fail closed until reviewed, including intervening versions. The original 0.40.4 release has been exercised on the isolated Mac rig; 0.41.1 released-binary validation and the bounded PC installation/live smoke passed; the [PC report](reviews/2026-10-20-rlo-0411-pc.md) records exact evidence and unexercised lifecycles. Current physical actors bind to persistent identities and seeded appearances; exact RLO clothing/rendered-look equivalence remains unverified.

**RLO's CityApi comes first.** When the loaded RLO has `RealLifeOverhaul.ModApi.CityApi` with contract version 1, all three adapters read through it on any RLO release and the version pins below do not apply. A CityApi with another contract version, or with a missing or changed member, is refused with no fallback. Everything below describes the reflection fallback for releases without the API. See [the bridge design](rlo-bridge.md).

## API and readiness

`new RloIdentityAdapter().Read(Assembly? rloAssembly)` locates the actual public `RealLifeOverhaul.Flare.FlareDirector.Instance`. `ReadSnapshot(Assembly?, object? director)` accepts an already captured director with the same validation. Both return `RloReadResult` containing `Status`, `Message`, `SourceVersion`, `PopulationKey`, `CitySeed` and `Identities`. They have no Unity or game assembly reference and target `netstandard2.1`.

Call on the game's main thread after RLO's own initialization, and retry `NotReady` on a later bounded update. Never initialize RLO through this adapter. `MissingDependency` means no assembly was supplied. `Incompatible` means the version, public contract or population failed validation; there are no identities in a failed result. `NotReady` covers a missing instance/account, inactive Flare, an uninitialized population, an inconsistent city refresh or an active multiplayer session. Persistent City refuses Going Public sessions; it has no authoritative multiplayer simulation protocol.

All three adapters share one release check. The assembly simple name must be `RealLifeOverhaul`. Assembly version `0.41.1.0` must have informational version `0.41.1`, `0.41.1.0`, or `0.41.1+dbd15c0958770219c0f4d969a8b2ac28593014d2`. Assembly version `0.40.4.0` must instead have `0.40.4`, `0.40.4.0`, or `0.40.4+46b3b8835e113ec3807344a9b7899439684004ad`. Crossed assembly/informational pairs, missing informational metadata, other suffixes and unreviewed releases are refused. Version metadata and signatures bound the contract; they do not authenticate a modified binary.

## Narrow public reads

The adapter validates exact public member types before reading a population:

- `FlareDirector.Instance`, `Running`, `File`, `Citizens`.
- `FlareLiveRules.Citizens == 400` and `CitySeed(FlareFile)`.
- `FlareCitizen.Index`, `First`, `Last`, `District`, `Age`, `Female`, `Persona`.
- The eight `FlarePersona` names and integer values against Core's explicit persona enum.
- `FlareAvatarRules.Citizen(int citySeed, int index)` and `FlareAvatarPlan.Seed`, `Age`, `Female`.
- `RealLifeOverhaul.GoingPublic.InSession`, whose reviewed getter only compares the current role field.

`CitySeed` and `Citizen` are reviewed pure computations. The adapter reads the live **public `File` property**; it never reads private `_file` or `_citizensSeed`. This handles a newly opened account before its first save, with no writes and no delay until persistence. There is no call to `FlareStore.Load`, `Save`, `CitizensOf`, account creation, director startup, settings or any mutating RLO method. `FlareStore.Peek` is read-only but unnecessary here: a persisted account could be stale relative to the live citizen list, so it is not a fallback.

The reviewed RLO donor contract requires exactly 400 actual list members must have unique indices 0–399, complete names, valid ages/personas and nonzero appearance seeds. Portrait age/sex must agree with the current identity. The adapter checks that the director is still running and retains the same current file/seed at the end. The main-thread requirement is part of this contract; reflection does not make RLO's mutable collections thread-safe.

## Mapping and persistence boundary

| Persistent City identity | Actual RLO source |
| --- | --- |
| `Id` | `c` followed by the invariant `FlareCitizen.Index`, RLO's existing account key convention |
| `Name` | `First + " " + Last` |
| `District` | Game district ID from `District`; RLO null means unknown and maps to Core's empty string |
| `Age`, `Female`, `Persona` | Copied and validated public citizen values |
| `AppearanceSeed` | `FlareAvatarRules.Citizen(CitySeed(File), Index).Seed` |

All returned identities are detached copies; modifying Persistent City's state cannot modify RLO. An appearance seed preserves RLO's deterministic look input. It does not by itself prove identical physical game rendering, clothing or an actor binding.

`PopulationKey` is `rlo:0.40.4:` plus a SHA-256 digest of the versioned canonical encoding, actual city seed and every identity field. This historical prefix names the original donor contract, not the installed RLO version. The reviewed 0.41.1 donor generation, avatar, city-seed and district algorithms are byte-identical to 0.40.4, so the prefix and hash encoding remain unchanged. Compatible upgrades preserve existing identities, assignments, journeys and saved payloads; changed donor data still refuses. Actual `SourceVersion` reports the loaded release, while an existing city retains its original `RloVersion` provenance. Encoding uses `BinaryWriter` length-prefixed UTF-8 strings and primitive values, sorted by ordinal citizen ID; source list order therefore does not affect it. It deliberately changes if identity content or seed changes. Store it alongside the game save's Persistent City state; a population hash is not a substitute for the game's save ownership boundary. Initial startup never substitutes a synthetic fallback for missing RLO. This donor count does not set the city population: eligible residential interior capacity does. The housing policy introduced in 0.3.1/schema 4 uses bounded occupant bands and a 90% aggregate fresh-city target, with no whole-property-area fallback. The reference 335 eligible interiors support 1,064 occupants and 957 initial residents; this allocation does not model family relationships or staff roles. Schema 4 preserves this original source in `SourceIdentities`; dependency checks compare that catalogue to live RLO independently of the active population. Imported survivors must still equal their original source identity. New residents use unique `pc:<sequence>` IDs and remix source name parts, persona and appearance donor data. The donor age, sex and appearance seed stay together. RLO itself retains all original profiles; a departure affects only this background simulation. Strict schema 1/2/3 migration preserves the active roster, assignments, cars and interrupted journeys. Correcting housing never evicts legacy occupants, so a migrated roster can exceed the current per-home policy or fresh-city target. The earlier 25,495-resident experiment remains historical evidence, not the current population policy.

## Evidence and remaining validation

Offline checks on 2026-10-19:

- Compatibility builds without game/Unity references, with nullable warnings treated as errors.
- Synthetic contract tests cover missing/unreviewed dependency handling, exact identity copying, stable/change-sensitive population keys, loaded snapshot identity comparison, detached copies, readiness without fallback, partial/duplicate population rejection, avatar consistency and multiplayer refusal. This describes the historical identity-contract checks; current test totals and completed native/reviewer evidence are in CHECKS.md and the linked review reports.
- The actual release ZIP was downloaded to temporary storage. Its DLL reports assembly/file version `0.40.4.0` and informational version `0.40.4+46b3b8835e113ec3807344a9b7899439684004ad`. SHA-256: `e0e7950e684138937d81a7a46c33ca8c9c488753d57ef1ef8d7e81c0eecbb7be`.
- An offline .NET harness loaded that released assembly and resolved its existing managed dependencies locally. Public contract validation completed and returned the expected `NotReady: FlareDirector.Instance is not available yet` without starting Unity or RLO. No DLL is vendored or packaged.

Isolated Mac evidence covers reading the actual 400-member population, startup readiness, reload/save continuity and disabled/removed mod behavior; see the validation notes. District changes, RLO account/seed transitions, actor appearance matching and session-entry suspension still need broader game testing.

## Temporary road-speed contract (0.6.1)

`RloRoadSpeedOverrides` is a separate read-only compatibility adapter for those same two explicitly reviewed releases. The fault director/world/rules sources and reflected contracts are byte-identical between them. On the main thread it reads the reviewed fault director's world and shown entries, validates the exact native waypoint type and integer limit, and returns a detached reference-keyed snapshot of original/effective speeds. Null director/world or released lane means no active override. It does not initialize faults, alter limits or call RLO test/repair methods. Those methods appear only in disposable native test helpers and are excluded from the package.

The speed profile preserves original limits for topology and section capacity across cache clears, and uses effective limits only for new trip timing. Each override must match the reviewed `x <= 20 ? x : Max(20, x - 15)` rule and the current native limit; duplicate lanes or disagreements are refused. No nearest-fault guess or unexplained-speed fallback is allowed. See the [native repair report](reviews/2026-10-20-citywide-traffic-native.md) for provenance, tests and remaining gates.

## RLO 0.41.1 release audit (Persistent City 0.6.2)

The upstream latest release was checked through GitHub's live release API on 20 October 2026. Release ZIP SHA-256: `d2071b9fd84dff06a4e44b622ee346a53477300a9970113c41d53fd9b0b8a384`, matching the published checksum. Released DLL SHA-256: `f1b93c176b33f79dc0fd7bfc8148e7953d438356271da19a2b463e437c13ac89`.

A source audit compared 38 relevant Git blobs: 29 were identical, eight changed and one was new. The identity-generating code, reflected scene/casualty/ragdoll ownership and fault speed lifecycle are unchanged. Social scheduling changes do not alter the fields read by this mod. RLO buses now have additional detour and red-light timeout behavior; they remain custom RLO bodies outside the native Gley pool used by Persistent City. Native coexistence evidence is separate from this source audit. No RLO source or economy data is modified.
