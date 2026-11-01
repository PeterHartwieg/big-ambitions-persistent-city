# Population schema 18 — B0

Allocated from A3 `754c2b6` (population 17). Only `persistentcity:state_v1`
changes. Detached shadow, native authority and receipt contracts retain their formats.
B0 allocated the inactive contract. B1 now executes active revision 1 as described
below; the original tuple/reference allocations and frozen 13-17 readers remain unchanged.

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
Schema-18 integer slots also reject octal/hex coercions. Memory, visit and compact
place-reference strings use literal canonical ASCII; escaped spellings refuse.

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
served count (1, 0–35), affinity plus 8 (1, affinity −8…8; codes above `G` forbidden),
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

Root `Capabilities` has exactly `Code:"B", Revision:1, Active:boolean`. B0/B0b
refuse active B; B1 executes it. Missing, duplicate and unknown code/revision refuse. The early native authority
bootstrap probes this header before releasing any saved managed business. New writes
place `Schema` then `Capabilities` first so this single-pass probe can stop before
the graph. Older root ordering remains supported; full startup decoding still checks
all remaining fields, duplicates and trailing content. Tuple initialization also
requires exact coverage of every public writable DTO property, failing loudly if a
future field is omitted from a positional layout. Population
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

## B0b amendment — 31 October 2026

Schema 18 is amended in place before any player save has used it. This is the
B0b prerequisite between B0 and B1, fixing inherited A3 held-context tuning.
It adds no B activation, memory update, loyalty policy or settings keys.

`FoodDecisionState.Tuning` is a nullable **scalar string**, revision `1` followed
by twelve uppercase hexadecimal binary64 words (16 digits each), exactly **193
ASCII characters**. The words preserve the exact effective coefficients, in order:
`FoodChoiceTemperature`, `FoodChoiceWeightFloor`, `FoodChoiceTasteWeight`,
`FoodChoicePriceWeight`, `FoodChoiceDistanceWeight`, `FoodChoiceDistanceScaleMinutes`,
`FoodChoiceQueueWeight`, `FoodChoiceQualityWeight`, `FoodChoiceTimePressureWeight`,
`FoodChoiceHealthWeight`, `FoodChoiceHouseholdWeight`, `FoodShortlistTemperature`.
Each value must be finite and inside its existing A3 clamp range. Unknown image
versions, wrong arity/length/alphabet, non-finite and out-of-range values refuse;
read never clamps a malformed saved image. The immutable in-memory image owns a
copy of the effective settings. Capture clamps live values once, before any
shortlist weight is drawn. Shortlisting and final selection use this same copy.
Continuation/commit never consult live tuning after capture. Context and draw
keys are unchanged and tuning-independent; the image is discarded with the decision.

`FoodDecisionShop.MemoryContribution` reserves **one binary64 effective log-weight
contribution per candidate**, not per memory entry or offer. Its structural bound
is finite **-8..8**; zero is neutral. This can hold B1's aggregate lookup result from
its four-entry memory, including configured strength and loyal/curious effects,
without retaining a second memory diary. For the plan's bounded 0.1..3 multiplier
and strength 0..2, the log contribution lies within -4.606..2.198. B1 must capture
its final bounded contribution before its first affected draw and retain it for
all offers of the selected candidate. **B0b always writes zero, never applies it,
and refuses nonzero saved contributions until B1 support exists.** It reserves
no StartsDay or watermark behavior.

Missing `Tuning` or explicit null means **capture live values at first continuation**;
missing `MemoryContribution` means zero. These two omissions alone are allowed
for pre-image schema-18 fixtures. All other required fields, duplicate/unknown
fields and bounds retain strict validation. Schema 17 and older pass their frozen
readers first and migrate with the same null/zero defaults; earlier captures cannot
be reconstructed. An already selected old context keeps its saved winner and quote
progress. Once resumed, even a legacy context saves the new image before further
work. The frozen 13-17 resolvers explicitly exclude both new fields, retaining
all previous required fields, counting and unknown-field refusals. Older binaries
continue to reject a new image as an unknown field; no fallback strips state.

The current validator now bounds **all held decisions to one**, including empty
shortlists, matching the simulator's one-decision scheduler. This prevents empty
contexts from becoming a population-wide tuning history. No existing valid
simulator save needs more. Source validation/refusal remains atomic.

**Byte/record ledger.** Canonical writes add **205 characters/UTF-8 bytes** for the
image including its named field, quotes and separator, plus **25 per shortlisted
shop** for neutral `MemoryContribution:0.0` including field/separator: **405 bytes
at eight shops**, independent of population. Null migration images cost 14 bytes
instead of 205 until continuation. In-memory additions are one reference per
held decision, one immutable wrapper/settings copy, and **8 bytes per candidate**;
measured managed sizes are recorded in CHECKS. For B1 allow **48 bytes per
candidate** for full-precision signed scalar text, so this reservation is at most
**589 bytes** including A3 tuning. Both are deducted from the existing **65,536-byte
aggregate policy/header/held-tuning allowance**, not the 64-byte per-consumer
memory allocation. Neither scalar charges a collection record: streaming and
semantic counts remain unchanged, including the 104 existing fixture image
records and the future ledger's conservative context/tuning header reservation.
The capacity tool populates a real image and updates the resulting size/digests;
ordinary population-only and economy digests remain unchanged. Reference/5k and
CF32/CF64 measured totals appear in CHECKS and the B0b measurement manifest.

## B1 execution — 31 October 2026

B1 fills the allocated consumer extension; **no schema bump, new DTO field, tuple
slot, record charge or older-source resolver change**. The B0b neutral-only
`MemoryContribution` semantic gate is deliberately lifted; finite -8..8 validation
is retained. The final effective multiplier, including `FoodMemoryWeight`, clamps
to the loaded minimum/maximum (0.1..1 / 1..3); zero strength is exactly neutral.
Every shortlisted shop keeps the contribution computed before shortlisting and
uses it for all its offers. The A3 tuning image plus eight scalar fields remains
within **589 bytes**. No B tuning image is needed: its only held-choice effect is
already reduced to those captured scalars, and the existing B revision identifies
the algorithm. Committed visits use actual subsequent service evidence.

Migration keeps B inactive and memories empty. The simulator flips `Active` at
the first crossed logical midnight, in event order, without a population sweep.
Saving/reloading before that boundary cannot postpone it. This does not change
A3 activation or tuning-edit semantics. Old held contexts have zero contribution;
old committed/uncertain meals without a B commitment watermark finish neutrally.

Stable consumer records own the memory. Generation IDs come directly from the
existing player `FoodAuthority.BusinessId` (including its persisted generation),
never reconstructed from an address or presentation. The table is indexed once
on attach; a candidate makes one generation lookup and at most four entry checks.
New generations append only at actual experiences. Slots never renumber or reclaim
in B1, avoiding a new city-wide liveness scan. Both 20k slots and the 2 MiB character/
UTF-8 table budget bound registration. Saturation increments the on-demand
`CapacityMisses` diagnostic and advances the experience watermark without erasing
old memory, changing accounts, dropping receipts or retrying checkout. The diagnostic
counter is session-local; the terminal saved watermark prevents repeat counting
of the same missed capture after reload.

Commit/reached stages carry no affinity change. A confirmed served event or a named
actual closed/stockout/queue failure atomically publishes its compact memory and
terminal watermark. Duplicate/older contexts and all terminal repeats are ignored.
A failed terminal context cannot later book a served memory. Unknown checkout stays
at its commitment/reached stage until exact paid evidence resolves; the consumer's
existing uncertain meal already retains the original generation, intent and service
minute across midnight. The memory day is the original experience day, not receipt
reconciliation day. Generic mixed-cause refusal strings remain neutral; native
checkout now provides a typed cause only for a confirmed zero-unit stockout or closure.
Rejected candidates, unaffordability, unknown supply, lost routes and interrupted
personal schedules have no disappointment effect. An already reached shopping stop
can retain named failure evidence without committing a second journey.

Affinity decays toward zero by whole points lazily from logical elapsed days;
loyalty scales days/point from 0.5x to 2x. Entries expire only after the configured
retention age (the endpoint is retained). Update materializes decay only for the
experienced entry, except a failure whose effective result remains positive: it
subtracts the penalty from stored affinity and preserves the previous day as the
decay/expiry anchor. Re-dating that positive habit could otherwise outlive the
pre-failure entry and raise future return chance. The cause still records the
actual failure; this case's day is not the date of that failure. Served visits and
nonpositive results use the experience day. Served counts saturate at 35; affinity
saturates at -8..8.
Positive affinity raises habit with loyalty; unseen/expired alternatives receive
curiosity novelty. A bad experience subtracts at least one point, even with a zero
configured penalty; zero then removes only the extra configured magnitude. Queue
impatience/other-cause time pressure strengthen penalties, loyalty forgives within
a negative penalty. Influence is monotone: bad service cannot raise return weight;
floors, saturation, neutral strength and neutral multiplier endpoints can make the
change equal rather than strictly smaller. No served-count bonus masks disappointment.

Insertion evicts expired first, then least effective affinity, oldest experience,
then ordinal complete generation ID, independent of slot/list order. Lower capacity
never trims on load or projection; an ordinary insertion applies the new capacity
through those normal evictions. For a **new bad-visit entry**, first preflight every
required victim in that same eviction order. Forgetting a competing shop must not
lower its contribution; otherwise a smaller failed-shop multiplier can still raise
its relative return probability. If a victim would lose a boost, retain the entries,
increment session-local `ProtectedInsertions` and commit the terminal failed
watermark once without registering a new reference. Bounded multipliers and probability
floors make a universal stronger penalty impossible; this conservative capacity case
keeps return odds equal, rather than increasing them. Free-slot and safe-eviction
bad experiences still retain their negative entries. This includes lower-capacity
insertions which would otherwise remove several boosted shops. No midnight diary/cleanup pass is introduced.

**Protected insertion is an explicit exception to the brief's strict "bad visits
always lower return chance" rule:** return chance stays equal, never higher, and
the failed shop keeps its pre-visit novelty contribution. No negative per-shop
entry or failure cause is retained in this case; only the terminal failed watermark
is saved. B2 must not infer that every confirmed failure has a negative entry, or
interpret a missing entry as proof that no failure occurred. `ProtectedInsertions`
counts these cases only for the current session and is not a persisted failure history.

`PopulationRuntime.FoodMemoryDump(consumerId)` / `CitySimulator.DumpFoodMemory`
returns detached entries, effective affinity, cause and watermark only when called.
No regular projection/inspector wording or B2 settings are added.

Round 1 capture correction: explicitly verified bag exhaustion (`Out of stock`,
zero units, zero throughput at a staffed counter) is checked before a generic
arrival-service refusal. At a reached stop, product stockout requires every
affordable offer to have confirmed zero units, with known service, queue and time
otherwise fitting the visit; stocked unaffordable alternatives do not mask it.
Wholly unaffordable menus, affordable unknown stock and visits already impossible
from queue/time stay neutral. The saved generic service flag is
revalidated using one on-demand, generation-matched `IFoodFailureMarket` read;
the native implementation bypasses the decision-supply cache. Missing/throwing or
unknown-staffing proof stays neutral and does not interrupt the original refusal.
Revalidated bag exhaustion also requires the captured queue, deadline and opening
bounds to fit. With its zero throughput hiding service duration, queue plus eating
must leave strictly positive time for service; a short personal stop, excessive
queue or no remaining service time stays neutral. No service rate is invented.
Failed-stop capture passes the target directly to the store, leaving unchosen
meal business/place/product fields untouched. Queue capture requires the decision's
queue reason and a break/opening window which would fit without the wait.
Physical consumers retain the pre-B1 arrival rule: a growing queue still serves
when the meal fits the committed break. A queue memory is recorded only when the
existing break-fit refusal occurs and removing the wait would make it fit. No new
physical queue-patience refusal is introduced, including before B activation or at
zero memory weight. These changes do not amend any saved tuple or scalar contract.


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
`config/persistent-city.json` if tuning is needed: served range 2–10, recency 1–28,
default 3/14, with normal clamp/log-once handling. City-wide loyalty distributions
remain a later paid marketing-agency research tier; B0 exposes no such report.

## Reproduction and evidence

`tools/save-contract-regression --capacity <reference|consumers5000|5000|ceiling2|ceiling|CF32|CF64>`
emits exact synthetic manifests and offline save timings/memory. Run each in a
fresh process. CF32 and CF64 have 50,000 residents ×3, all three outcomes, four
memories/consumer, a full 20,000-entry/2 Mi-character reference table and 20,000
places. They differ only in 32 versus 64 visits. Future contract allocations appear
only as explicit manifest reservations; no speculative C/D/E DTOs are persisted.
The byte/record ledger and native acceptance limitations are recorded in CHECKS.
The full 12 Mi-character existing-state allowance uses 6,343,907 characters for
19,991 added places and 6,239,005 ASCII characters in existing pc: resident names,
each within its unchanged string limit. `--record-boundary`, `--input-boundary`,
`--history-churn` and `--base-recovery` reproduce the independent cap/refusal proofs.

## E1 budget consumption - 1 November 2026

[Schema 20](population-schema20.md) consumes E1's existing allocation without
amending the 18/19 source formats. Resident W costs at most **22/24** ASCII bytes
including field/quotes/separator; the consumer extension adds **2/8** bytes inside
its existing charge. At 50,000 x3, **1,400,000/2,400,000** weather bytes are used,
leaving **1,000,000**. A nonnull W charges one of the reserved 50,000 physical
weather records; consumers add zero records. The root activation and single held
food image use the existing header/decision reserves. CapacityBench populates real
weather captures and deducts these bytes/records from future reservations once;
the full-memory headroom stays charged. Measured CF32/CF64 totals are in the dated
[E1 checks](checks/e1-rain.md). All existing caps remain fixed.
