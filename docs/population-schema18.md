# Population schema 18 — B0

Allocated from A3 `754c2b6` (population 17). Only `persistentcity:state_v1`
changes. Detached shadow, native authority and receipt contracts retain their formats.
B is inactive; this binary refuses an active B capability. There is no capture,
decay, loyalty weight, paid-research state or UI change.

`LegacyPopulationCodec` freezes the source 13/14/15/16/17 contracts, allow-lists,
exact money conversion, semantic validation and counting. Each version has a
dedicated resolver. It does not resolve through the current codec. Successful
source validation precedes in-memory conversion; schema 18 then receives full
validation and serialized character/UTF-8 preflight before activation or replacement.
Constructors initialize empty memory/references and inactive B, never inferred visits.
Unsupported, malformed and oversized payloads remain owned by their original save.

## Wire contracts

All tuples start with integer version `1`. Arity is exact. Scalars are checked by
the streaming shape pass before deserialization, including canonical integer money
tokens. These scalar slots do not enter the generic array record counter.
No arbitrary extension slots or nested lists are allowed.

* Budget: 19 slots (version plus 18 fields), zero collection charges. Field order:
  `Arrears Balance BaseIncomeCents DailyEssentialsCents DailyHomeFoodCents
  DailyHousingCents DailyIncomeCents DaysWithoutHomeFood EssentialsPaid HeldPurchase
  HomeFoodFundedDay HomeFoodPaid IncomeReceived IncomeSource LastPostedDay
  NextPostingMinute OpeningBalance PreparedFoodSpent`.
* Meal: 36 slots (version plus 35 fields). Order:
  `Allowance AnchorId Budget BusinessId ContextAttempts Day DayAllowanceUsed Deadline
  Decision DesiredAt DueAt Excursion ExistingStop ExistingStopPurchases ExpiresAt Kind
  LastContextKey NeedsHomeResync PlaceId PreferenceBonusCents PreferredProductId Price
  ProductId Purchases Reason RepresentedTraits ResidentId ReturnMinutes ServiceAt
  ServiceMinutes Slot Spent Status Today Memory`. Nested existing contracts remain
  unchanged, including A3's four-offer limit and saved selected-before-commit state.
* Today: `[1,day,unrecorded,outcome?... ]`, 3–6 slots, at most three named meals or
  one legacy meal. Existing outcome strings and all receipt/status/reason/identity
  checks remain. One charged unit per consumer rollup slot, including null or an empty rollup.
* Visit: exactly 21 ASCII characters: `1`, 16 uppercase hexadecimal binary64 time
  bits, three base-36 place-reference digits, one activity digit `0`–`4`. Place
  references index the **ordinal-ID-sorted complete saved Places table**, independent
  of root/property/array order. All 64 visits remain accepted and each charges one
  record. Finite/time/order/place-kind checks remain; fractional time and negative
  zero bits are preserved. Ordinary simulation still retains 32 visits.
* Consumer extension: one ASCII string, `1` + seven-character watermark + zero to
  four ten-character memories; exact lengths 8/18/28/38/48. Its JSON value and
  separator cost at most **51 characters**, below the 64 inclusive allocation.
  It charges **one unit**, including empty memory. Later fixed D2/E1 scalar fields
  require a new schema and retain this one-unit rule with a 96-character ceiling;
  none are frozen here.

The canonical base-36 alphabet is `0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ`; lowercase,
signs, whitespace, other alphabets, surplus fields and out-of-range codes refuse.
A memory is business slot (3), last-experience logical day (4), saturated confirmed
served count (1, 0–35), affinity plus 17 (1, affinity −17…17; code `Z` forbidden),
last outcome (1: served=1, closed=2, stockout=3, queue=4). A served outcome needs a
nonzero served count. Entries have strictly increasing business slots, no duplicates.
Days are 0…694444 (the existing billion-minute bound). Their actual saved day must
not exceed the city day. Watermark is day (4), meal `0`–`3`, context `1`–`4`, stage
committed=1/reached=2/failed=3/served=4; absent is exactly `-------`. B1 must atomically
update memory and watermark at its simulator commit, including late receipts.

The reference table is `[ [1,slot,completeBusinessIdentity,"PPP"], ... ]`, at most
20,000 entries, ordered by stable slot (0…19999), with unique complete identities.
`PPP` indexes the same saved place table and must resolve to a shop. Historical
generations may share a place; one generation cannot reference different places.
Combined table text (brackets, commas, quotes and escaping included) must fit both
2,097,152 characters and 2,097,152 UTF-8 bytes. IDs retain the existing 512-character
limit; they are never truncated or hashed. Slot numbers are not list indices and
must never be renumbered. B0 has no live allocator/reclaimer. B1 may reclaim only
unreferenced slots in a coherent snapshot, accounting for all memories, scopes,
held meals/decisions, receipts and later report/payment contracts. Table saturation
must leave existing references and transactions intact; an unremembered experience
needs a counted capacity diagnostic in B1, never a new checkout or dropped receipt.

Root `Capabilities` has exactly `Code:"B", Revision:1, Active:false`. Missing,
duplicate, unknown code/revision and active B refuse. The early native authority
bootstrap probes this header before releasing any saved managed business. Population
decode and simulator construction also guard capability execution. Future binaries
must explicitly implement a revision before accepting its active state.

Both streaming and semantic counters retain all previous collection charges and
the 4,000,000 global cap. The reference table charges one per entry, capability
header one, Today one and extension one. Preexisting streaming/semantic differences
are reported separately; capacity budgets use the larger. Population 50,000,
representation ≤3, places 20,000, route/depth/string caps remain unchanged.
Schema 18 also bounds the complete payload's UTF-8 bytes at 268,435,456 alongside
its unchanged character limit. Old sources retain their original limits and must
pass the complete new preflight before conversion can be published.

## Regulars projection

`RegularsProjection` builds a transient index lazily once per coherent snapshot/day.
Queries then use an indexed shop lookup, without another consumer scan. It counts
stable consumers (including represented slots), never offer counts or a saved
per-shop resident list. Only saved player-owned food scopes can return free counts.
The caller supplies B1's effective affinity; B0 invents no decay policy. A new index
is required after memory, ownership or logical-day changes.

Definition: at least **3 confirmed served visits**, positive effective affinity,
and last retained visit no more than **14 logical days** ago (inclusive). Memory
eviction limits what can be observed. Defaults `RegularMinServedVisits=3` and
`RegularMaxDaysSinceVisit=14` are constants for this pure B0 projection. No config
keys are needed by active B0 behavior. B1 should expose flat keys in the existing
`config/persistent-city.json` if tuning is needed: served range 1–35, recency 0–694444,
default 3/14, with normal clamp/log-once handling. City-wide loyalty distributions
remain a later paid marketing-agency research tier; B0 exposes no such report.

## Reproduction and evidence

`tools/save-contract-regression --capacity <reference|5000|ceiling2|ceiling|CF32|CF64>`
emits exact synthetic manifests and offline save timings/memory. Run each in a
fresh process. CF32 and CF64 have 50,000 residents ×3, all three outcomes, four
memories/consumer, a full 20,000-entry/2 Mi-character reference table and 20,000
places. They differ only in 32 versus 64 visits. Future contract allocations appear
only as explicit manifest reservations; no speculative C/D/E DTOs are persisted.
The byte/record ledger and native acceptance limitations are recorded in CHECKS.
