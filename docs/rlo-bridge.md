# RLO bridge: a versioned contract instead of version pins

Status: proposed on 21 October 2026. The RLO half is proposed upstream in [AdultCudi/real-life-overhaul#24](https://github.com/AdultCudi/real-life-overhaul/issues/24), with the code on [PeterHartwieg/real-life-overhaul `feature/mod-api`](https://github.com/PeterHartwieg/real-life-overhaul/compare/main...feature/mod-api) (`6b7c73ce`). The Persistent City half is built and tested offline. Where this note and the branch's `docs/reference/mod-api.md` differ, the branch is current.

## The problem

Persistent City reads Real Life Overhaul through three reflection adapters (`Compatibility/RloIdentityAdapter.cs`, `RloPhysicalReservations.cs`, `RloRoadSpeedOverrides.cs`). They bind 40-odd members, a dozen of them private fields such as `SocialCast._wears`, `PedestrianIncidents._casualties` and `FaultWorld._shown`. Because a private field can change meaning without changing its name or type, all three adapters refuse any RLO release that is not on an allowlist of exact version and commit pairs.

The cost showed on 21 October. RLO went from 0.41.1 to 0.45.0, an audit found every member Persistent City uses unchanged, and Persistent City still broke until someone added a third pair to the allowlist and rebuilt. Every RLO release repeats that, and RLO published at least 20 releases between 18 and 21 October, four of them on 21 October.

The fix is to give the two mods a contract with its own version number. RLO keeps the contract stable across releases, and Persistent City checks the contract version rather than the release.

## Decision

1. RLO gets a small public read API for other mods, `RealLifeOverhaul.ModApi.CityApi`, with a `ContractVersion` that is separate from RLO's release number.
2. Persistent City uses that API whenever the loaded RLO has it, on any release, and checks only the contract version and the members it promises.
3. When RLO has no API (0.40.4, 0.41.1, 0.45.0 and anything else released before it), Persistent City falls back to the existing reflection adapters, still pinned to reviewed releases.

### Why not structural checks without an upstream change

The alternative is to drop the version pins and accept any release whose reflected members still have the expected names and types. I don't recommend it, for three reasons.

- **Shape is not meaning.** The adapters read private fields whose meaning RLO never promised. `SocialCast._wears[slot]` being an index into `SocialState.People` is an implementation detail; a refactor could keep the field and change what it indexes. A shape check passes and Persistent City then reserves the wrong citizen, or none, and a person stands in two places at once. The pins exist because nothing else catches that.
- **Some reads duplicate RLO's rules.** The road-speed adapter recomputes RLO's pothole rule (`x <= 20 ? x : Max(20, x - 15)`) to check it read the right field. If RLO retunes potholes, a structural check would refuse the release anyway, and Persistent City would need a change. With the API, RLO reports the original and current limits and the rule stays RLO's business.
- **It keeps all the cost on one side.** Every RLO refactor near Flare, the social cast, ragdolls or faults could break Persistent City with nobody on the RLO side knowing. An API in RLO makes the dependency visible where the code changes, and RLO's own offline check holds it.

Structural checks would cut today's maintenance a little: the identity read uses public members and has strong runtime checks of its own (exactly 400 unique indices, avatar age and sex agreement, a save-time hash of every identity). If upstream declines the API, relaxing the pin on the identity adapter alone is a reasonable fallback plan. The reservation and road-speed adapters should stay pinned whatever happens.

## The contract, version 1

Everything lives in RLO's feature folder `Scripts/ModApi`. The names below are the contract; consumers bind them by reflection, so nothing in them may be renamed while `ContractVersion` is 1.

```csharp
namespace RealLifeOverhaul.ModApi
{
    public enum CityApiStatus
    {
        Ready = 0,       // the answer is complete
        Off = 1,         // the feature behind it is switched off or not running
        NotYet = 2,      // no city, account, seed or population yet; ask again later
        Refreshing = 3,  // RLO is between two consistent states; ask again next frame
        Shared = 4,      // a Going Public session; the shared city is not readable this way
        Failed = 5       // RLO could not answer; the feature behind it has faulted
    }

    public sealed class CityApiCitizen
    {
        public readonly int Index;           // 0 to count - 1, unique; RLO's account key is "c" + Index
        public readonly string First, Last;  // never empty
        public readonly string District;     // a game district id, or null when unknown
        public readonly int Age;             // 1 to 130
        public readonly bool Female;
        public readonly string Persona;      // one of: Commuter, Foodie, Parent, Student, Retiree, Sporty, Grumpy, NightOwl
        public readonly int AppearanceSeed;  // never 0; the seed RLO draws this citizen's face and body from
    }

    public static class CityApi
    {
        public static int ContractVersion => 1;

        // Flare's city people for the current account. Clears and fills `into`.
        public static CityApiStatus ReadCitizens(List<CityApiCitizen> into, out int citySeed);

        // Flare citizens whose person RLO has on the street right now (the social cast). Clears and fills `indexes`.
        public static CityApiStatus ReadReservedCitizens(List<int> indexes);

        // Bodies RLO holds right now. `pedestrians`: the game's own Streets.Pedestrians.Pedestrian objects RLO has taken
        // over (casualties). `characters`: every character body RLO owns or animates (the social cast's bodies, out or
        // not, and active ragdolls), as the game's character component. Clears and fills both.
        public static CityApiStatus ReadReservedBodies(List<object> pedestrians, List<object> characters);

        // Road lanes RLO has slowed (potholes). For each i: lanes[i] is the game's GleyTrafficSystem.Waypoint,
        // originalLimits[i] its limit before RLO touched it, currentLimits[i] the limit RLO set. Clears and fills all three.
        public static CityApiStatus ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits);
    }
}
```

### Promises RLO makes while `ContractVersion` is 1

- Every type, member, parameter list and status value above stays, with the meaning written beside it.
- Calls are for the game's main thread. They never throw: a fault returns `Failed` and leaves the lists empty. They never change RLO or the game, never start a feature, and allocate nothing beyond growing the caller's lists.
- **Citizens are stable.** The same account and city seed give the same citizens, field for field, on every release with contract 1. Changing how Flare generates names, districts, ages, personas or appearance seeds is a contract change.
- `ReadCitizens` answers `Off` when Flare is switched off or not running. `ReadReservedCitizens`, `ReadReservedBodies` and `ReadSlowedLanes` report what RLO holds right now, whatever its options say and whether or not a city is loaded: `Ready` (empty when nothing is held or slowed), `Shared` or `Failed`, and `ReadReservedCitizens` also `Refreshing` when a cast slot names a person the social life no longer has. Persistent City treats any other answer from them as unknown: it reserves everyone, or refuses the road snapshot, until a complete answer arrives.
- `ReadCitizens` answers `Refreshing` rather than a mix of old and new citizens, for example when a citizen's age has moved on but their face seed has not yet.
- RLO may add members, status values never; additions do not change the version. A consumer must not depend on a member that is not listed for its version.
- Removing or changing anything listed raises `ContractVersion` to 2. RLO may keep version 1 members beside version 2 ones for a while, but does not have to.

### What each side checks

RLO adds an offline check, in EconomyCheck, that pins the contract from RLO's side: the version number, the status values, the persona names, and that a made-up city's citizens through `CityApi`'s rules equal what `FlareLiveRules` and `FlareAvatarRules` produce, including the age and sex agreement that yields `Refreshing`. A future RLO change that breaks the contract fails RLO's own build gate, where the person changing the code sees it.

Persistent City binds every listed member when it starts and refuses to run (`Incompatible`) if any is missing or has another type, or if `ContractVersion` is not one it supports. It does not then fall back to reflection: a release with the API is never on the reviewed list, so the fallback would refuse anyway, and the message names the real cause.

## Persistent City's side

`Compatibility/RloBridge.cs` binds `CityApi` once per RLO assembly and turns each method into a typed delegate, so a read costs a delegate call rather than reflection.

| Adapter | With the API | Without the API |
| --- | --- | --- |
| `RloIdentityAdapter` | `ReadCitizens`; maps statuses to `NotReady`/`Incompatible`; Persistent City's own checks (exactly 400, unique indices 0 to 399, names, ages, known personas, nonzero seeds) stay | Existing reflection read, pinned to 0.40.4, 0.41.1, 0.45.0 |
| `RloPhysicalReservations` | `ReadReservedCitizens` and `ReadReservedBodies`; an answer other than `Ready` or `Off` reserves everyone for that read until the next complete one; `Shared` or `Failed` stops the guard | Existing reflection read, pinned |
| `RloRoadSpeedOverrides` | `ReadSlowedLanes`; trusts RLO's original and current limits; refuses any answer but `Ready` or `Off`, a lane listed twice, or a limit of 0 or less | Existing reflection read and pothole-rule check, pinned |

The identities from the API are byte-for-byte what the reflection adapter reads, so `PopulationKey` (still prefixed `rlo:0.40.4:`, the original donor contract) does not change. A city saved on RLO 0.45.0 through reflection loads on a later RLO through the API. Persistent City's offline tests check that equivalence.

`RloReadResult.SourceVersion` still reports RLO's release. Messages say which path was used ("through RLO's CityApi contract 1" or "through the reviewed reflection contract").

## Going upstream: an issue first

RLO's CONTRIBUTING asks contributors to talk to the maintainer first about anything larger than a small fix, and this is more than code: it asks the maintainer to promise other mods a stable interface and to hold every future change near Flare, the social cast, ragdolls and faults to it. That is the maintainer's call, so the proposal goes up as an issue that links a ready branch on Peter's fork. If the maintainer agrees, the pull request follows from that branch; if they want another shape, the branch changes before anyone reviews a PR.

## Hard Mode Patch's factory diplomas

Hard Mode Patch pins RLO's DLL hash because it writes into RLO: it replaces `StudyCatalogue.All` with a longer array, constructs `CourseDef` through a 19-argument constructor, and reads `CareersDirector._broken`, `StudyFile.Holds` and `CampusRules._courses`. A course-registration call in RLO (for example `CityApi.RegisterCourse(...)` before careers start, plus `HoldsCourse(int id)`) would remove that hash pin for the RLO half. The game hooks (`PlayerItemPurchaser.GrabItem`, `PurchaseUI.PlaceOrder`) are unaffected; they pin the game, not RLO.

I left it out of contract 1. It is a write into RLO's save id space, its campus and its texts, which is a bigger promise than reading, and it serves one series-balance mod rather than every RLO player. It should be its own proposal once contract 1 has landed and the maintainer has seen how the read API works out.

## What remains to check in the game

Offline tests cover both paths in Persistent City. These need the game, with an RLO build that has the API:

- Persistent City starts on an RLO release with the API and a version not on the allowlist, and logs that it used `CityApi`.
- A city saved on RLO 0.45.0 (reflection path) loads on the API build with the same `PopulationKey` and no refusal.
- Reserved citizens: meet a Flare citizen online, have them appear on the street, and check Persistent City keeps its copy of that citizen off the street.
- Casualties and ragdolls: knock a pedestrian over and check Persistent City does not take the body.
- Potholes: a slowed lane's speed in Persistent City's road snapshot matches RLO's current limit, and returns to the original when the pothole is mended.
- Going Public: joining a session still suspends Persistent City.
