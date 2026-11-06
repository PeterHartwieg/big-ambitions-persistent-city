# Persistent City save format: writer 26

This page describes the population payload written by the current build (schema 26, F5c-lite, #92). Older schemas are not described here: a build that writes 26 refuses them (see *Refusals*).

## Keys

The payload is one JSON string in the native save's `modData` under `persistentcity:state_v1` (`SaveCodec.StateKey`). The same `modData` also carries the save-epoch marker `persistentcity:save-epoch_v1` (F7b; its `schema=` line reads `26`), the food authority manifest `persistentcity:food-authority-v1`, food sale receipts and, after a failure, `persistentcity:accounting-stop_v1`. The format of those keys does not change in writer 26.

## Shape

The root is one JSON object. `Schema`, `Capabilities` and `MenuPolicy` come first so the native authority bootstrap can stop before the population graph; the remaining fields are in ordinal name order. Every field is required, unknown and duplicate fields are refused, numbers and money are canonical, and strings are exact.

Writer 26 adds the person shape to the writer-25 fields:

| Field | Meaning |
| --- | --- |
| `Citizens[]` | Residents only (kind Resident). Each row is the existing resident object plus `P`, the resident person tuple. |
| `Visitors[]` | Visitor persons, one string tuple each, strictly ordinal by person ID. |
| `NextVisitorSequence` | Next `pv:<n>` sequence. It equals 1 + the number of `pv:` visitors (writer 26 creates none). |
| `IdentityPolicyVersion` | Version of the derived, never stored, visitor identity policy (currently 1). |
| `Portals[]` | Saved station table for visitor stays: at most 256 IDs of at most 128 characters, strictly ordinal. |

### Resident person tuple `P`

`1R-` for a resident without Employment, or `1RE<start day>.<schedule>.<native>`:

- `1` is the tuple version and `R` the kind.
- The Employment workplace is the row's `DayPlaceId` (stored once). Its kind follows the persona: Study for students, Work otherwise. Retirees have none.
- `<start day>` is the canonical decimal day on which the current `DayPlaceId` assignment began. It lies between the day of `JoinedMinute` and `LastEmploymentCheckDay`.
- `<schedule>` is `0` (today's persona routine) and `<native>` is `0` (`NativeEmployment.None`). No other value is accepted.

`HouseholdId` is derived as `h:<HomeId>` and never stored.

### Visitor person tuple

`1V|<id>|<stay>|<employment>|<carried>|<arrival>`, at most 512 characters:

- `<id>`: a `consumer:<escaped anchor>:<slot>` or `pv:<n>` person ID.
- `<stay>`: `<portal><pattern><presence>.<first day>.<next stay start>`. The portal is `---` or a three-digit base-36 index into `Portals`. The pattern is `C` (commuter) or `D` (day visit), and the presence is `A` (active) or `D` (dormant). The first day is a canonical decimal. The next stay start is 16 uppercase hexadecimal digits of the binary64 minute: `0000000000000000` while active, a finite minute after the city clock while dormant.
- `<employment>`: `-`, or `W`/`S` + a three-digit base-36 index into the ordinal `Places` table + `.<start day>.0.0`. Only commuters may hold one. The place must have the matching kind, it counts toward the place capacity, and the start day lies between the first day and today.
- `<carried>`: `-` or the `ResidentIncomeSource` digit (`0` employment, `1` pension, `2` support).
- `<arrival>` (`LastArrival`): `-`, or the visit token `1` + 16 hexadecimal binary64 digits + a three-digit base-36 place index + an activity digit `0`–`4`. The reader checks structure only: the alphabet, a finite minute that is not after the city minute, an index inside `Places`, a known activity, and no tuple on a dormant visitor. Place, Shop activity and `DesiredAt` are checked only when service is admitted.

A visitor has no identity, home, car or recent visits; the tuple has no slot for them. In writer 26, each visitor is the represented consumer of one resident. Its ID is `FoodRepresentation.ConsumerId(host, slot)`, and it eats on today's excursion schedule through that host. The fresh-city default is pattern Commuter (`PersonPolicy.MigratedConsumerPattern`, section 9 Q3), active, first day = the roster's enrolment day, with no employment, carried source or arrival.

### Meal roster (factor-1 roster rule)

`Food.Residents[]` holds exactly one roster record per person (residents and visitors). Each record is tuple version 3, which is the writer-25 tuple without `AnchorId` and `Slot`. `Food` has no `RepresentationFactor`. On read, each record's host and slot and the city's factor (1 + visitors / residents, at most 3) are derived from the person table. Every visitor must be one resident's represented consumer, and every resident has the same number of them.

## Bounds

Named in `SaveCodec`/`PersonTable` and checked while streaming, before allocation, and before every write:

| Bound | Value |
| --- | --- |
| Persons (`Citizens` + `Visitors`) | 150,000 (`MaxPersons`) |
| Residents (`Citizens`) | 50,000 (`MaxResidents`) |
| Visitors | 100,000 (`MaxVisitors`) |
| Meal roster records, routine stops | 150,000 |
| Traffic vehicles, link vehicle and waiting lists | 50,000 (`MaxTrafficVehicles`; only residents own cars) |
| Stations | 256 |
| Input | 256 Mi characters and UTF-8 bytes; 4,000,000 embedded records (one per visitor tuple) |

The other limits are unchanged from writer 25. Nothing is truncated: a payload over a bound is refused.

## Refusals

`SaveCodec.Read` returns:

- `Missing`: no payload.
- `Loaded`: a valid schema-26 city.
- `Future`: a newer schema.
- `DevelopmentSchema`: schemas 1–25, written by older development builds. Message: "This Persistent City save was created by an older development build; start a new city. The saved state is kept unchanged." There is no migration.
- `Invalid`: anything else that is malformed, over a bound or inconsistent.

Every status except `Missing` and `Loaded` keeps the stored payload and every other `modData` key. The store and session refuse to replace it. For a `DevelopmentSchema` city, Runtime takes no food authority and writes no key. A shop whose saved manifest owned the loaded hour stays protected until that hour ends, then gets native customers again (`FoodAuthorityStandDown`).
