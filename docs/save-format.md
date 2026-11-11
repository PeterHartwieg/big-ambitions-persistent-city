# Persistent City save format

This page describes the population payload as of 11 November 2026. The save schema is held at 1 until release ([decision](decisions/2026-11-09-schema-one.md)): the format changes in place and the number stays 1. Payloads with any other schema number are refused (see *Refusals*). The code is `Persistence/SaveCodec.cs` and its partials; the [code map](map.md#save-path) shows the call chain.

## Keys

Own data lives in the native save's `modData`:

| Key | Content | Written |
| --- | --- | --- |
| `persistentcity:state_v1` (`SaveCodec.StateKey`) | The city: one compact `pcc-c` string, below | Inside the native save callback |
| `persistentcity:save-epoch_v1` | Save-epoch marker: sequence, `schema=1`, population key, minute and the state payload's length, or `failed:N` | After the state key, same callback |
| `persistentcity:food-authority-v1` | Food ownership manifest per shop | At ownership boundaries |
| `persistentcity:food-sale-v1:<intent>` | One receipt per purchase (Pending, Paid, Conflict) | At sale time |
| `persistentcity:accounting-stop_v1` | Accounting stop or detach record; the first value wins | On a failure or detach |

Research orders, Street Food vendor rows and the economic ledger are inside the state payload; they have no keys of their own.

## Compact wire

The state payload is one JSON object with a fixed envelope (`Persistence/CompactWire34.cs`):

```
{"Schema":1,"f":"pcc-c","v":1,"k":[keys],"h":[shapes],"s":[strings],"n":routes,"r":routeColumn,"t":rootColumn}
```

`k` is the key table, `h` the object shapes, `s` the shared strings, `n` and `r` the shared route table, and `t` the root column. The column grammar (numbers, strings, booleans, nulls, objects, arrays, route references, road-tag ranges, packed varints) is documented on `SaveCodec` in that file. The reader checks the envelope prefix and the top-level `Schema` first, decodes the columns strictly, writes the body tree back out and runs the full strict body reader on it. A compact city is accepted exactly when its body would be. A plain JSON body with schema 1 is not a valid payload and is refused as Invalid.

## Body shape

The decoded body is one JSON object. `Schema`, `Capabilities` and `MenuPolicy` come first so the native authority bootstrap can stop before the population graph; the remaining fields are in ordinal name order. Every field is required, unknown and duplicate fields are refused, numbers and money are canonical, and strings are exact. Adding or removing a field is an in-place change: older payloads are refused as Invalid ("Unknown field" or a missing field), not migrated.

Person-related root fields:

| Field | Meaning |
| --- | --- |
| `Citizens[]` | Residents only. Each row is the resident object plus `P`, the resident person tuple. |
| `Visitors[]` | Visitor persons, one string tuple each, strictly ordinal by person ID. |
| `VisitorRoutines[]` | One per visitor, aligned with `Visitors`: `{"J": journey or null, "T": "<routine tuple>", ...}`. Identity, traits and travel preferences are derived from the city seed and person ID, never stored. |
| `NextVisitorSequence` | Next `pv:<n>` sequence. Numbers below it without a visitor are allowed only for sequences the fill skipped because their derived identity is a student or retiree, at most 4 × 100,000. |
| `IdentityPolicyVersion` | Version of the derived visitor identity policy (currently 1). |
| `Portals[]` | Station table for visitor stays: at most 256 IDs of at most 128 characters, strictly ordinal. |

Other root sections: `Food` (the meal roster, scopes, history), `Economy` (the economic ledger and change journal, exact base-36 money), `Research` (the research ledger), `Vendors` (the Street Food vendor table), `Traffic` (the traffic kernel), `PlayerHoldSequence`, and the place, assignment and clock fields of `CityState` in `Core/Model.cs`.

### Resident person tuple `P`

`1R-` for a resident without employment, or `1RE<start day>.<schedule>.<native>`:

- `1` is the tuple version and `R` the kind.
- The workplace is the row's `DayPlaceId` (stored once). Its kind follows the persona: Study for students, Work otherwise. Retirees have none.
- `<start day>` is the canonical decimal day on which the current `DayPlaceId` assignment began. It lies between the day of `JoinedMinute` and `LastEmploymentCheckDay`.
- `<schedule>` is `0` (today's persona routine) and `<native>` is `0` (`NativeEmployment.None`). No other value is accepted.

`HouseholdId` is derived as `h:<HomeId>` and never stored.

### Visitor person tuple

`1V|<id>|<stay>|<employment>|<carried>|<arrival>`, at most 512 characters:

- `<id>`: a `pv:<n>` person ID. Any other form (including the old `consumer:` IDs) is refused.
- `<stay>`: `<portal><pattern><presence>.<first day>.<next stay start>`. The portal is a three-digit base-36 index into `Portals`; every visitor has one. The pattern is `C` (commuter) or `D` (day visit), and the presence is `A` (active) or `D` (dormant). The first day is a canonical decimal. The next stay start is 16 uppercase hexadecimal digits of the binary64 minute: `0000000000000000` while active, a finite minute after the city clock while dormant.
- `<employment>`: `-`, or `W`/`S` + a three-digit base-36 index into the ordinal `Places` table + `.<start day>.0.0`. Only commuters hold one; a day visitor never has a city workplace. The place must have the matching kind, it counts toward the place capacity, and the start day lies between the first day and today.
- `<carried>`: `-` or the `ResidentIncomeSource` digit (`0` employment, `1` pension, `2` support).
- `<arrival>` (`LastArrival`): `-`, or the visit token `1` + 16 hexadecimal binary64 digits + a three-character place cell + an activity digit `0`–`4`. The place cell is a three-digit base-36 index into `Places`, or `v` and two base-36 digits indexing the vendor table. The reader checks structure: the alphabet, a finite minute that is not after the city minute, a valid index, a known activity, and no tuple on a dormant visitor. Place kind and activity are checked against each other when service is admitted.

A visitor has no home, car, recent-visit list or optional stop; the tuples have no slot for them.

### Meal roster (factor-1 rule)

`Food.Residents[]` holds exactly one roster record per person (residents and visitors). Each record is a positional tuple at version 1 (held at 1, like the schema) with no `AnchorId` or `Slot` field. `Food` has no `RepresentationFactor`. Every record decodes, and is written, as its own person's: `AnchorId == ResidentId`, `Slot == 0`, stored factor 1, host slot 0. The saved person table must also pass `OwnershipValidator`; resident IDs stay opaque.

### Traffic

`Traffic` is the traffic kernel's state (`TrafficKernelState` in `Core/TrafficContracts.cs`): link definitions and states, vehicles with their route legs, progress floor (`NominalMinutes`) and motion fields, and the pending link events. Since X6 a vehicle has no `PhysicalControl` field: the kernel owns every car's progress, and a payload that still carries the field is refused.

## Bounds

Named in `SaveCodec`, `PersonTable` and the Core policy classes, and checked while streaming, before allocation and before every write. Nothing is truncated: a payload over a bound is refused.

| Bound | Value |
| --- | --- |
| Persons (`Citizens` + `Visitors`) | 150,000 (`MaxPersons`) |
| Residents (`Citizens`) | 50,000 (`MaxResidents`) |
| Visitors and visitor routines | 100,000 (`MaxVisitors`) |
| Meal roster records, routine stops | 150,000 |
| Traffic vehicles, link vehicle and waiting lists | 50,000 (`MaxTrafficVehicles`; only residents own cars) |
| Traffic links, link states and events | 250,000 (`MaxTrafficLinks`) |
| Places | 20,000 (`MaxPlaces`) |
| Route legs per journey | 512 (`MaxRouteLegs`) |
| Stations | 256, IDs of at most 128 characters |
| Tuples | resident 32 characters, visitor 512, visitor routine 512 |
| Street Food vendors | 64 live, 128 live plus retired, 1,024 tombstones (`VendorTable`) |
| Research | 8 open orders (1 per business), 16 kept reports, 16 closed orders (`ResearchLedger`) |
| Compact wire | nesting 48, 64 Mi decoded values and at most 4 per payload character, 65,536 shape fields |
| Input | 256 Mi characters and UTF-8 bytes; 4,000,000 embedded records (one per resident tuple, visitor tuple, station entry and compact table entry) |

The other limits are named in `SaveCodec.CollectionLimit`.

## Refusals

`SaveCodec.Read` returns:

- `Missing`: no payload.
- `Loaded`: a valid schema-1 city.
- `DevelopmentSchema`: any other schema number (older development builds wrote 2–36). Message: "This Persistent City save was created by an older development build; start a new city. The saved state is kept unchanged." There is no migration.
- `Invalid`: anything else that is malformed, over a bound or inconsistent, including a schema-1 payload that is not a valid `pcc-c` string or carries a field the current format no longer has.

Every status except `Missing` and `Loaded` keeps the stored payload and every other `modData` key. The store and session refuse to replace it. For a `DevelopmentSchema` city, Runtime takes no food authority and writes no key. A shop whose saved manifest owned the loaded hour stays protected until that hour ends, then gets native customers again (`FoodAuthorityStandDown`).
