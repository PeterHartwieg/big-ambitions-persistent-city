# Population schema 22: observed campaign images

Owner: `pc-c2b-campaign-awareness`, 1 November 2026. Exact predecessor on this
branch: **19**. Reserved schemas 20/21 are foreign here; 23 and newer refuse.
Integration can repoint the isolated `MigrateCampaigns19To22` step after reviewing
the intervening predecessor. This branch never interprets those foreign shapes.
Schemas 17–19's documents and schema-19 awareness validator remain unchanged.

The only new saved collection is `FoodCommerceState.Campaigns`, sorted by stable
business-reference slot, maximum **1,024** images. There are no resident arrays,
daily consumer IDs, native campaign controls, research orders or payments.
Campaign identities reuse the existing bounded `BusinessReferences` table;
registration never reclaims a slot or changes a complete generation. The production memory
and campaign providers share the same current reference index: inserts finish at
the hourly publication, never as an O(images) catch-up on the next consumer.
Independent diagnostic stores synchronize append-only additions before lookup or
insertion, preventing either writer from allocating the other's slot.

Each image is one canonical **265-character ASCII string**, charged as exactly
one record by both counters. Including JSON quotes/separator it consumes at most
268 bytes, below the plan's 512-character/image and 524,288-byte allocation:

| Offset | Width | Meaning |
| --- | ---: | --- |
| 0 | 1 | Wire version `1` |
| 1 | 3 | Uppercase base-36 business-reference slot, 0–19,999 |
| 4 | 5 | Uppercase base-36 actual observation hour, 0–floor(1e9/60) |
| 9 | 256 | Canonical Base64 of 192 bytes, 64 little-endian 24-bit masks |

Masks are ordered by age 0–7, then type code 0–7. Age zero means the observation
hour's logical day. Each bit 0–23 is one **actually observed** enabled/verified
C1 campaign at that logical accounting hour. The first set bit is that day's
first-observed boundary. Unobserved earlier hours remain zero. Masks for negative
days, unknown type codes (currently 6/7), hours beyond the last observation or
all-zero images refuse. Missing/disabled/unknown observations add no bits, but
retain prior observed days until the eight-day window expires. Retired images
drop on the next complete publication. Runtime also refuses evidence ahead of
the native saved clock at attachment; a lagging Core clock cannot backdate a
mask's eligibility.

An empty campaign collection adds **15 bytes**, zero records. One image adds
**282 bytes** including that field; 1,024 add **274,446 bytes / 1,024 records**
versus schema 19 when generation references are already present. New shared
generation references additionally consume their existing 2 MiB/20,000-reference
allocation; they are not hidden in the campaign byte figure. No global cap grows.
Capacity tooling deducts actual campaign field/records once from the existing
future envelope.

The existing one held awareness image can now include `Campaign=32`; its source
limits are 4/4/4/4/8/**8**, union **32**, shortlist **8**, routes **16**. No new
decision field is needed: eligibility and source remain captured in that image,
with the existing complete native terms, quotes and selection. Held decisions
never consult fresh campaigns, groups or tuning on reload/continuation.

Schema 19 first passes its exact filtered shape and complete original semantic
contract, including the 24-member union and forbidden campaign bit. Only then
does the isolated migration set schema 22 and retain an empty campaign list.
Schema 18 and 13–17 keep their existing frozen readers/activation behavior and
acquire an empty list. Unknown fields, mixed old/new shapes, duplicate/foreign
references, malformed wire, unsupported schemas, noncanonical scalars and future
input refuse without replacing the source. Namespace, money, menu limits,
receipts, business authority and resident tuples are unchanged.

Native type identity and demographic targeting are different evidence. C1 has
six verified identifiers but **no verified resident-target mapping**. Production
therefore retains Unknown groups and zero campaign admission. The explicit PC
policy interface supports reviewed commuter routines (an assigned work/study
place, never native employment), health orientation (saved trait band ≥3), and
their overlap. Synthetic reviewed policies exercise this interface offline;
they are not native target evidence or an authorization to guess targets.

See [slice evidence and rig checks](checks/c2b-campaign-awareness.md).
