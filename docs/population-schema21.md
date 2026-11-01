# Population schema 21: D2 rival choice and own-ledger outflow

Owner: `pc-d2-rival-outflow`, 1 November 2026. Predecessor on this branch:
schema 19 at `51ce709`. Schema 20 belongs to the independent rain slice and is
not read, rewritten, implemented or renumbered here. The isolated migration hook
is `RivalWire21.Migrate`; integration can point it to a later frozen predecessor.
Schemas 17–19 documentation and published wire contracts are unchanged.

## Migration and authority

`PopulationSchema19*` preserves the predecessor's complete streaming reader,
tuple arities, limits, converters and semantic validation. Its resolver excludes
only the newly introduced D2 DTO members. Source schema 18 still uses its own
filtered resolver; schemas 13–17 still pass the original `LegacyPopulationCodec`.
All source validation completes before migration. A successful upgrade sets 21,
initializes external accounts to zero and target variants to empty, and sets
`Food.RivalStartsDay` to the next logical day. Existing held player choices,
quotes, money, receipts, B1 memory and excursions retain their original terms.
Ordinary config changes apply to the next uncaptured context that same day.

Unknown/future schemas, schema 20, foreign fields, bad enum codes, malformed
tuples and noncanonical money refuse without overwriting the source. Early
native-authority bootstrap accepts only the implemented schemas 13–19 and 21;
older binaries reject 21 before resuming authority. Native authority keys,
`persistentcity:state_v1`, `FoodSaleReceipts` keys/content and the detached shadow
schema are unchanged. Transient D2 `FoodBusiness` evidence is excluded from the
unchanged shadow format; it never authorizes shadow sales.

## Fixed tuples and exact accounts

Current meal and household-budget tuples use prefix **2**; predecessor tuples
retain prefix **1** in the frozen reader. Existing positions do not move.

* `ResidentBudget`: append `ExternalFoodSpent` at position 19 (positions include
  prefix 0). This is one nonnegative canonical integer `Money` unit count, at
  most 19 decimal digits plus its separator: **20 ASCII bytes**. Native prepared
  food remains in `PreparedFoodSpent`; holds still belong to native checkout.
* `ResidentMeal`: append `External` at position 36, after `Memory` at 35.
  Empty means player. A rival target is `1s` followed by **19** unpadded Base64url
  characters: `s=0` before settlement, `s=1` after settlement. Its quoted string
  and separator occupy exactly **24 ASCII bytes** at the maximum. Prefix 1 is
  the target format revision, not another population schema. The complete saved
  meal intent (resident/day/kind), business ID, product, exact price, destination,
  deadline, service duration and existing outing/return remain its authority;
  there is no duplicate receipt or route tree.
* The suffix is a canonical **112-bit SHA-256 prefix** of the advertised owner
  and sorted complete weekly hours. It detects changed terms; it is not a
  replacement business identity. Exact price is retained and compared separately.
  The final Base64url character must have canonical zero padding bits.
* For allowance-only legacy consumers, the existing `Budget` position 3 is a
  nonnegative integer external-food account rather than a budget array. Zero is
  explicit; null and noncanonical/fractional/string units refuse. The converter
  stores this variant as `LegacyExternalFoodSpent` with `Budget == null`.
  Real household scopes retain the budget-array variant and a zero legacy field.
  This reuses the account allocation and adds no extra tuple slot or record.
  A diagnostic attempt to enable household finances after legacy outside spending
  refuses before mutation; it cannot erase that account or fabricate an opening
  endowment. Ordinary automatic household-finance startup is unchanged.

External accounts cannot exceed the consumer's recorded spending. A settled
target requires a paid status, positive purchase count and sufficient retained
account/spending amounts; a rival can never have an uncertain native checkout or
held native purchase. No account becomes negative. The conservation identity is:

`opening + income = balance + essentials + household food + player prepared food + external food + held purchase`.

Every arithmetic change and counter bound is checked before commitment and again
before payment. Settlement publishes the debit, external account, consumer spend,
count, target watermark and paid status in one callback-free Core transaction.
`SpendingLimit` retains arrears, holds and future-essentials protection. Legacy
scopes debit the existing daily allowance and exact external account without
creating household money. Repeating settlement, including after reload, is refused.

## Captured candidates and outcomes

`FoodAwarenessCandidate.AlternativeKind` and
`FoodDecisionShop.AlternativeKind` use `FoodAlternativeKind`: Player=0, Rival=1.
Each shortlist member must retain its admitted source's kind. The held rival shop
also captures `RivalTerms`, `RivalServiceMinutes` (integer 1–30) and
`AlternativeWeight` (finite >0 through 2). Player defaults are empty/0/1. Rival
`MealsPerHour` is **-1 (unknown)**, every offer's `Units` and district-demand
evidence are **-1 (unknown)**, memory contribution is zero, and its service clock
equals the captured origin minute: no native capacity, stock or queue is invented.
The materialized evidence always says `EstimatedFromPriceAndHours` and includes
that wording in `ServiceEvidence`. The runtime reader copies advertised facts only.

The existing ten-field current-day outcome string keeps its width. Status codes
0–3 retain the original player meaning. Schema 21 uses codes 4–7 as the rival
target variant of the same status; the converter exposes the separate
`MealOutcomeRecord.TargetKind` and ordinary status. Only bought/skipped rival
outcomes are executable; alternative and unconfirmed variants refuse. A rival
outcome cannot claim `ReceiptConfirmed`. `CheckoutAt` is the modeled settlement
minute for a rival and retains the original native-attempt meaning for a player.
Player outcome bytes are unchanged. A modeled payment is never native receipt
proof, even if someone presents a matching native receipt key to the API.

`Food.RivalStartsDay` is a single 0..current-day+1 header. Food scopes remain
player-owned; their transient `AlternativeKind` is not serialized. Rivals enter
the shared transient index through detached metadata and never enter `Food.Shops`.

## Bounds, continuity and evidence limits

The existing 32 source / 8 shortlist / 16 logical route envelope is unchanged;
campaign's eight reserved slots remain unknown and unused. The D2 production
source uses D1's indexed metadata and direct live detail reads. Unchanged refreshes
preserve the index revision; changes publish after bounded 1–64-read units.
Index work remains in C2a's existing 64-item continuation budget. Missing entries
consume their draw without refill, including at 20,000 alternatives. Only a held
choice captures terms; there is no consumer-by-shop table or per-meal catalogue pass.

Slot zero uses the original physical outing/return and represented slots use the
original excursion, deadlines and return safety. Arrival and completion recheck
readable identity, owner/kind, exact price, hours, closure, door and access.
Withdrawal/changed terms refuses without a debit or substitute. Ownership changes
invalidate old authority. D1's conservative quarantine of observed replacements,
collection replacement, disappearance and ownership change remains in force.
The native tuple is still readable identity, **not proof of durable native object
generation across reload**; no new native identifier or memory continuity is claimed.

With B1 inactive the memory source is empty. Rival outcomes never call its updater,
including while B1 is active; D4 owns that later composition. Campaign providers
are not called. Weight zero admits no new rivals and requests no new rival refresh,
predicate or detail work. Saved held/committed targets still prepare a cold
read-only reader when needed; saved payments and returns remain owned.

The new target/account are fixed scalars in existing tuples: **zero additional
per-consumer records** in both streaming and semantic counting. Outcome target
encoding adds zero bytes. The 24/20-byte allocations and existing 96-byte combined
consumer-extension envelope remain intact; only the one held source/shop image
uses the existing decision reserve. No global cap, menu, route, memory or history
bound is raised. No D3 history, panel or paid-report buckets are introduced.

Offline measurements and the pending numbered native checks are in
[D2 checks](checks/d2-rival-outflow.md). Native behavior and the combined mod-work
gate remain separate from offline proof.
