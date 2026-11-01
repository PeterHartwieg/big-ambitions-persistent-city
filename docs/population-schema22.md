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

## Owner decision 1 - 1 November 2026

Campaign preference applies to all residents with no demographic or native-target
mapping. A positive mask bit at the meal's captured logical hour supplies the
active modifier, even if native observation has since advanced or stopped.
Retained awareness grants no active bonus at stopped or unobserved hours.
Missing reads add no bits and age retained scope generations.
Only absent/replaced generations retire. The actual runtime observes after safe
ownership transfers and scope activation, including hours with no transfers.

The existing held `Sources` integer additionally accepts **ActiveCampaign=64**
as a modifier on an admitted source identity, not another draw/source slot.
At least one ordinary source bit (1-32) is still required; unknown bits refuse.
The six admission caps remain 4/4/4/4/8/8 and the union/shortlist/routes remain
32/8/16. Schema 19's frozen validator still rejects both campaign bits. This is
an amendment of the unpublished schema-22 candidate; no schema 17-19 contract,
resident tuple, extra field or schema number is changed.

Active bit 64 captures a fixed 1.25 preference and half soft-distance input in
both shortlist and final offer weighting. Actual route times, money and
feasibility are unchanged; stopped/disabled live campaigns cannot rerank a held
image. The obsolete FoodMarketingTargetMatchMultiplier setting and target-policy
classes are removed. Reach, retention and daily decay govern awareness draws;
active known-shop weighting is not gated by a successful extra draw.

See [slice evidence and rig checks](checks/c2b-campaign-awareness.md).
