# City audit F6b — rival service limits (mod-owned estimates), 4 November 2026, offline

Refs #93. Base `dc59049` (writer 25; F1 #99, F3 #98 and the F5/F6/F7 plans merged). Scope is the F6b row of
[`docs/plans/f6-economy.md`](../plans/f6-economy.md) section 4, with sections 3.2, 5, 6, 7 and 10. The F6
plan's citations were rechecked at this base: `RivalFoodTerms.Capture` (`Core/RivalFood.cs`), `ArriveRival` and
`CompleteRival`, `FoodBusiness.ServiceMinutes` (`Core/FoodDemand.cs`), `MealOutcomeRecord` (`Core/FoodToday.cs`)
and the schema-21 held-shop validator (`Persistence/RivalWire21.cs`, rival `NextServiceAt == OriginAt`,
`Units == -1`). Status: **implemented; offline checks pass; not verified by rig.** Offline success is not
native proof. Native rival-service-limits checks (before red / after green) are rig-automated and deferred to the next batched combined rig per merge rule v2, recorded in [rig-2026-11-06-checklist.md](rig-2026-11-06-checklist.md#f6b-rival-service-limits), and do not gate merge. Native findings become follow-up fix slices.

## Implemented contract

- **Estimated lanes.** Each rival has `Lanes(businessType)` lanes. Each lane serves one customer per
  `RivalAssumedServiceMinutes`. On arrival a customer joins the next free lane. A lane is busy while a Serving
  rival meal at that place finishes later than now, so the next free lane is the lanes-th latest Serving finish.
  The existing refusals apply: "Queue exceeds patience on arrival" (represented consumers),
  "Arrival or queue no longer fits the break" and the hours check. Each records D4 `Queue` memory.
- **Estimated sellable units per meal period.** `MealPeriodUnits(businessType)` servings per clock meal period:
  breakfast [00:00, 11:00), lunch [11:00, 16:00), dinner [16:00, 24:00). A service counts in the period it
  **finishes** in. At N, arrival refuses with "Service unavailable on arrival" and D4 `Stockout` memory, labeled
  "modeled sell-out (estimated)". The count resets at each period start. There is no restock, rival cost or profit.
- **Choice-time knowledge.** A held decision evaluates each rival at its derived earliest free lane, so a known
  queue gives the existing "Expected queue exceeds patience" / "Service and return do not fit available time"
  reasons and enters the preference queue term. Each otherwise eligible rival offer is also checked against the
  period its **queued finish** falls in. If that period is sold out, the offer is refused before the draw with the
  existing "Out of stock" reason, so a known-sold-out rival can never beat an available shop, including when the
  deadline crosses into the next period. A rival whose every feasible finish falls in one sold-out period is not
  captured at all. In-flight travellers are not yet in the load, the same as for player shops.
- **Existing-stop purchases.** A resident already inside the rival decides and enters Serving without
  `ArriveRival`. The commit-time evaluation includes the lane wait and the finishing-period sell-out. When a
  present customer's only eligible rival offer is refused for the modeled sell-out, D4 records a `Stockout` entry
  for that rival, named explicitly with the same Committed/Reached stages the player existing-stop path uses.
  Other rival stop failures stay neutral, as before. A commit-time sell-out check remains as a fail-safe and
  records the same memory.
- **Derived, never saved.** `RivalServiceLoad` (`Core/RivalServiceLimits.cs`) is rebuilt inside FoodCommerce's
  existing attach pass from active Serving rival meals and current-period paid rival `MealOutcomeRecord`s
  (`TargetKind = Rival`, `Bought`, `PlaceId`, `CheckoutAt`). A lazy outcome-day rollover forgets the dropped
  records, so the live image always equals a rebuild from saved state. Tests check this after every call.
- **Labels.** `RivalFoodDiagnostics.Core` adds `ServiceLimits = "estimated (modeled service limits)"`,
  `SellOut = "modeled sell-out (estimated)"`, the effective terms, `RivalLaneRefusals`, `RivalSellOutRefusals`,
  `RivalSellOutExclusions`, `LoadPlaces` and the canonical `Load` image. Captured rival `ServiceEvidence` reads
  "...with modeled service limits (estimated lanes and meal-period sell-out); native stock and capacity unknown".
  F6c owns the Ctrl+F8 panel label text; `Runtime/FoodEconomyPanel.cs` is unchanged.
- **Authority.** No native rival read is added beyond the existing `ReadRival` detail read. There is no native
  rival write, receipt or checkout. Every refusal precedes `ExternalFoodSettlement.TryPay`, so refusals never
  debit. Player checkout (hold → uncertain → native `Buy` → confirm/refuse) and `FoodSaleReceipts` are unchanged.
- **Schema.** None. Writer 25 is unchanged. Held images keep their writer-25 contract
  (`NextServiceAt == OriginAt`, rival `Units == -1`).

## Calibration and Peter's Q2 default

One setting, `RivalCapacityPercent` (config and `RivalFoodSettings`), holds the Q2 answer:
`RivalServiceCalibration.ModerateCapacityPercent = 100` is the default; `GenerousCapacityPercent = 200` is the
alternative. It scales `RivalServiceLanes` (default 2, 1–8) and `RivalMealPeriodUnits` (default 60, 1–10,000).
Bounded per-business-type overrides go in `RivalServiceProfiles` (at most 64 types; invalid values are clamped
with a log, never discarding the file).

The reference is the verified native K1 player shop: two staffed counters serving **30 meals/hour**
([history](history-2026-10-development.md)). Two lanes × 60/5 min = **24/hour, −20 %**, inside the ±25 % band.
Units default to two rush hours at the reference ceiling. This is one native evidence point. Rig check 2 below
records representative staffed throughput as the calibration source; F6a/F6h curves are not the source.

Offline saturation (served meals/day, one shop, rising demand, `tools/rival-limits-regression`):

| Consumers | Moderate rival | Near-unbounded rival (8 lanes, 10,000 units) | Player shop 30/hour |
| ---: | ---: | ---: | ---: |
| 16 | 16 | 16 | 16 |
| 64 | 46 | 64 | 51 |
| 256 | 65 | 202 | 79 |
| 1,024 | 75 | 271 | 91 |

The moderate rival saturates at 75 against the player shop's 91 (**−18 %**, inside ±25 %). The near-unbounded
profile keeps rising, which is the pre-F6b behaviour this slice removes.

## Config change across a reload: F6d dependency (not resolved in F6b)

Plan section 3.2 says a config change takes effect only at a new decision context. F6b keeps that for the
**held decision image**: nothing about lanes or units is captured into it, and a reload never recaptures or
rerolls it (`HeldDecisionImageIsNeverRecapturedWhenAReloadChangesTheProfile`).

A commitment that is already **travelling** or **queued** carries no saved lane/unit terms in writer 25. It
therefore meets the profile loaded at arrival. With the same profile, reload is byte-identical to no reload.
With a tighter profile under contention, the commitment is refused at arrival with no debit and no native
write; it is never served beyond the new limits
(`ReloadWithATighterProfileUnderContentionRefusesAtArrivalWithoutDebit`). Keeping old terms for such
commitments needs the two bounded integers per commitment named in plan section 5. As instructed, F6b stops
here instead of adding a schema: the integers belong to **F6d's** single additive version. Until then, a
profile change between saves can refuse an already committed rival meal at arrival (fail-safe, no money moves).

## Offline evidence

`tests/RivalServiceLimitsTests.cs` (23 cases) covers:

- lane ordering and freeing;
- sell-out at exactly N, period boundaries and reset;
- 0 B steady-state load operations;
- calibration and the Q2 setting;
- bounded flat/profile settings and the shipped config;
- lane refusals before debit with D4 Queue memory;
- sell-out at exactly N per period with Stockout memory;
- reload after every call rebuilding identical lanes/counts and an identical future, for lanes 1 and 2/units 3;
- 30/60/120 cadence (2/1/0.5-minute steps with 2/4/64 budgets) giving byte-identical saves;
- the held image not recaptured under a changed profile, and the tighter-profile reload above;
- capture exclusion when sold out;
- ownership flip and withdrawal during service (refused, no debit, lane freed);
- player native `Buy` count equal to player sales while rivals saturate;
- existing-stop customers at 1 and 2 lanes, never above the lane count, with reload equivalence;
- existing-stop sell-out by finishing period;
- a sold-out rival whose stop deadline crosses into the next period, with capabilities active: no commitment is
  refused after selection, D4 Stockout entries are recorded, spending is unchanged, and reload at every boundary
  replays the same city;
- a sold-out rival against an available player shop: the rival is never chosen while sold out, and the player
  shop wins those decisions;
- a busy rival against a free player shop, producing queue reasons and player choices, with reload equivalence;
- label strings, plus a source-reachability guard: the limit code names no sale, receipt, Runtime, Unity or
  registration member.

With the lane evaluation disabled, the two existing-stop cases and the busy-rival case fail (3 red). With the
finishing-period sell-out evaluation and the stop memory disabled, the two sell-out cases above fail (2 red).
With the fixes, all pass.

`tools/rival-limits-regression` (1,091 checks) repeats reload-every-call equivalence on a 64-consumer crowd. It
also asserts writer 25, and that the steady state allocates 0 B and makes no rival native write.

Full suite: **2,879/2,879** tests pass (`dotnet test tests/PersistentCity.Tests.csproj`, 5 November 2026); the build passes; the remaining command matrix is listed in the PR. Offline only; no game, rig, lease, profile,
ModsLocal or bridge access.

## Sync onto codex/persistent-city, 6 November 2026 (offline)

PR #116 brings this branch up to date with `codex/persistent-city` at `0a926ef` in two real two-parent merges.
`2e3e032` merges F5a #107 (`c64e3e2`) and F5b #105 (`448247c`). `f90b1df` then merges F6c #110 (`0a926ef`).
The evidence and qualifications above are unchanged and still describe base `dc59049`.

- Conflicts. In `CHECKS.md` (both merges), the F6b, F6c and F5a index lines are all kept, one line each. In
  `docs/plans/f6-economy.md` (second merge), the F6b row keeps its 4 November status and the F6c row keeps its
  status and checks link. `Core/FoodCommerce.cs` (F5b doc comment; F6c transient panel counters),
  `Runtime/PersistentCityMod.cs` (F5a census command), README and roadmap merged automatically. No conflict was
  resolved in code, and no F5a, F5b, F6b or F6c source changed: each slice's own files are byte-identical to its
  side of the merge. Writer 25; no schema or save-contract change.
- Combined offline results on `f90b1df`: build passes; **3,000/3,000** tests pass (2,990 listed). The 68-command
  `tools/rival-memory-regression/validate.py` matrix exits 0 for every command, including
  `rival-limits-regression` (1,091 checks), `feedback-panel-regression`, `population-census-regression` (PASS)
  and `core-identity-regression`. `tools/identity-core-measure` passes, with validator p50 0.6571/3.6856/49.2684 ms
  at 1,914/10,000/100,000 persons and 0 B warm, matching F5b's record.
- Rival-limit perf. Arrival/completion and advancement are unchanged (p95 ≤ 0.0006 ms; interval p95
  0.30–0.69 ms, max ≤ 11.53 ms, no sample ≥ 30 ms). Reconstruction allocation is byte-identical (12,976 /
  233,448 / 4,555,304 B). The detached 100,000-consumer reconstruction (6 timed samples, so p95 = max) gave
  38.5022 ms in the matrix run, while other worktrees' dotnet loads shared the machine. Two quieter reruns gave
  17.5352 and 22.5708 ms, against 17.7817 ms in the committed record. The timed path is
  `RivalServiceLoad.Reconstruct`, which the merge does not touch, so this is machine noise on a stress size. It is
  still flagged: it is attach-time work, not per-frame mod-work, and the supported 1,914-consumer row stays at
  0.069–0.11 ms. These are detached component timings, not native or whole-frame evidence. The in-game checks
  below stay deferred to the batched combined rig.

## In-game checks for the next batched rig

Every check runs in the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig))
in one named disposable **FT-F6b-RivalLimits** city from reusable checkpoints. Diagnostic setup is limited to
that city. Each timed window follows the autosave protocol: record the original setting, verify it is off
before each window (including after reload), keep saves outside windows, and restore it during cleanup. None
of these checks is deferred to Peter, a playtest or a by-hand visual check.

1. **LOCKED-PC — Lane and sell-out refusals at the estimated counts (plan check 8).** The rig loads a lunch-rush checkpoint
   with many residents near one fixture rival. It applies a tight profile (`RivalServiceLanes = 1`,
   `RivalMealPeriodUnits = 3`) through the per-flow config, advances through the rush, and bridge-dumps
   `RivalFoodDiagnostics.Core` before and after. It asserts:
   - `RivalLaneRefusals > 0` and `RivalSellOutRefusals + RivalSellOutExclusions > 0`;
   - paid rival outcome records per meal period ≤ 3;
   - at most one concurrent Serving rival meal at that place, read from the `Load` image;
   - every refused consumer's external-spend account is unchanged;
   - each refusal has a matching D4 `Queue`/`Stockout` memory entry.

   Rig-automated (existing capability: FT `state`/`time`/`console` bridge dump of `RivalFoodDiagnostics`, `rig flow run` with disposable fixture copies; pending rig capability: RIG-MODSET per-flow PC config selection with restore).
2. **LOCKED-PC — Calibration evidence: native staffed throughput (plan check 8, section 3.2).** On representative staffed
   player shops in the same city, the rig reads `shop_state` staffed counters and the PC `MealsPerHour`
   diagnostic each game hour through a lunch window. It records the measured meals/hour beside the default
   rival ceiling (2 lanes × 12/hour = 24/hour). It asserts the default lies within ±25 % of the median staffed
   value, or reports the measured band as calibration evidence for F6h.

   Rig-automated (existing capability: `shop_fixture`, `shop_state`, FT `console`/`state`).
3. **LOCKED-PC — Estimate labels.** The rig bridge-dumps `RivalFoodDiagnostics.Core` and a captured rival decision. It
   asserts `ServiceLimits = "estimated (modeled service limits)"`, `SellOut = "modeled sell-out (estimated)"`,
   `Availability = "EstimatedFromPriceAndHours"`, `NativeRivalCheckout = "Unsupported"`, and `ServiceEvidence`
   containing "modeled service limits". The Ctrl+F8 label text is F6c's check.

   Rig-automated (existing capability: FT `console`/`state` bridge dump).
4. **LOCKED-PC — More own-shop custom when the rival is full (plan check 8).** Two matched checkpoint copies with a player
   shop and a rival at similar distance; arm A uses the default profile, arm B uses `RivalServiceLanes = 1`
   with the rival kept busy by existing-stop customers. The rig advances one lunch window and asserts that arm
   B has more player purchases, as `shop_log` native sales and PC confirmed receipts; that a sampled decision
   trace shows rival "Expected queue exceeds patience"; and that player native sales equal PC receipts in both
   arms, with no duplicate receipt.

   Rig-automated (existing capability: `shop_fixture`, `shop_state`, `shop_log`, FT `console`; pending rig capability: RIG-MODSET per-flow PC config; RIG-LEDGER independent native receipt/order comparator).
5. **LOCKED-PC — No native rival write (plan check 8).** Before and after checks 1 and 4, the rig reads the fixture rival's
   native registration, prices, hours, stock and finance fields. It asserts byte-identical fingerprints and an
   unchanged `rival` owner/prices/hours readback.

   Rig-automated (existing capability: `rival` prices/hours/owner readback; pending rig capability: RIG-RIVALFP read-only native fingerprint of registration, prices, hours, stock and finances).
6. **LOCKED-PC — Reload mid-queue and mid-service.** At a boundary where at least one rival meal is queued (Serving, start
   in the future) and one is being served, the rig saves outside any timing window, reloads, and dumps the
   `Load` image and the meals' `ServiceAt` values. It asserts the image is identical before and after, and that
   advancing to the end of the window gives the same paid count, refusal counters and wallets as an unreloaded
   copy of the checkpoint.

   Rig-automated (existing capability: FT `save`/`load`/`state`/`time`/`console`, `rig flow run` disposable copies).
7. **LOCKED-PC — Profile change across a reload (documented F6d dependency).** Same checkpoint as check 6, reloaded with a
   tighter `RivalMealPeriodUnits`. The rig asserts that every refused traveller's account is unchanged, that no
   period exceeds the new N, and that no native rival write occurs. This checks the declared fail-safe, not
   old-term replay; old-term replay waits for F6d.

   Rig-automated (existing capability: FT `save`/`load`/`console`; pending rig capability: RIG-MODSET per-flow PC config selection with restore).
8. **LOCKED-PC — Performance window (plan check 14, F6b part).** A lunch-rush window at the default profile, against the
   matched pre-F6b build, on a 957-resident/1,914-consumer city. The rig asserts mod-work p95 < 5 ms and flags
   every mod-work sample ≥ 30 ms with its component. It records whole-frame separately and never uses it as
   mod-work. It also records the one-time attach cost on load. Autosave is verified off before every window.

   Rig-automated (existing capability: `rig flow run` autosave guard, FT `state`/`time`; pending rig capability: unified PC mod-work/whole-frame evidence assertions; RIG-MODSET pre-F6b build selection).
