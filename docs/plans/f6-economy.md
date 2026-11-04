# City-audit F6: connected economic feedback

4 November 2026. Refs #93. Design plan only. Base `origin/codex/persistent-city` at
`d6b534a` (writer 25; E1–E5, audit #97, F3 #98 and F1 #99 merged). The audit cites
`00506ed`. Every source location below was rechecked at `d6b534a` and is cited at that
base. Approval: Peter approved F5 #92, F6 #93 and F7 #94 on 4 November 2026 at 21:23 JST
("I like all changes, let's make it happen."). This document plans the work. Implementation
slices follow in later runs after this plan merges. Nothing here is implemented, and
nothing here is verified by rig.

Player-facing summary of F6: employment, household money, services and the player's shops
feed each other. Rivals stop looking endlessly stocked for free. Price, hours and staffing
changes get clearer feedback.

F5 ([`docs/plans/f5-population.md`](f5-population.md), in a parallel run) owns the identity
model. F7 ([`docs/plans/f7-save-snapshots.md`](f7-save-snapshots.md), also parallel) owns
save representation and snapshots. This plan builds on the shared F5 baseline B1–B6 quoted
in section 3.8 and states what changes if F5 differs.

## 1. Acceptance mapping

The issue's acceptance list is the contract. Each item is quoted verbatim.

| # | Acceptance item (verbatim from #93) | How this plan meets it |
| --- | --- | --- |
| A1 | Specify the chosen links among employment, resources, needs, schedules, services, purchasing and travel. | Section 3.1 lists the chosen causal links. Each has a named source event, a mod-owned state change and an observable consequence. Slices **F6e** (attendance → wages → employment), **F6f** (household resources → purchasing/paid outings), **F6b** (rival service limits) and the existing B1 memory deliver them. F6a's offline harness tests each link by intervention (section 3.5). Rig checks 6, 7 and 9 exercise them in game. |
| A2 | Declare remaining outside-economy boundaries and prevent double accounting with native systems. | Section 3.3 has the boundary table: every money flow, its counterpart and its authority. Section 3.4 lists the double-accounting guards: native sale authority, receipt idempotency, F1 epochs, one money owner, and no modeled exchange at a player-owned place. The F6d contract extends the conservation identity. Offline proof: the F6a ledger invariants on every simulated day. Rig checks 1 and 13 run the independent native/PC ledger comparison. |
| A3 | Calibrate competition assumptions so unknown rival stock/service does not grant an unexamined unlimited advantage. | Section 3.2 replaces "unknown stock, fixed 5-minute service, no queue" with mod-owned, labeled estimates: service lanes, a per-meal-period sellable estimate and a queue. Native rival finances are not written, and the plan does not claim native rival stock. Slice **F6b**. The F6a harness measures the rival advantage before and after the change. Rig check 8 covers it in game. |
| A4 | Run controlled interventions in prices, staffing, hours, shortages, employment and access over multiple weeks. | Section 3.5 defines a six-week offline protocol per intervention family: two baseline weeks, two intervention weeks and two recovery weeks, with five seeds. It also defines a three-week rig protocol per arm. **F6a** builds the harness and records the current baseline. **F6h** reruns it with every loop implemented. Rig checks 2–7 apply each intervention in game. |
| A5 | Measure demand response, market share, recovery, household sustainability and stable accounting. | Section 3.5 defines each metric exactly: purchase counts and elasticity, meal shares by own/rival/home, days to recover, arrears/unfed/balance trends, and daily exact conservation with reconciliation. F6a emits them. F6h sets the acceptance thresholds. Rig checks 2–10 read them in game through PC console diagnostics and independent native reads. |
| A6 | Provide player feedback that makes the effects understandable and actionable. | Section 3.6 specifies what the player sees, where and how often: Ctrl+F8 week-over-week causes by hour and meal, a change journal, labeled rival estimates and resident inspector words. Everything is read-only UI. Slices **F6c** and **F6g**. Rig check 11 covers panel text, geometry and screenshots. |
| A7 | Demonstrate meaningful tradeoffs and avoid hidden hardware/detail-level differences, in coordination with F1/F2/F4. | Section 3.5 defines the tradeoff tests: no dominant extreme, and a measurable cost to every gain. Section 3.7 covers detail independence: all economic transitions are logical Core events inside F1 epochs, nothing is body-dependent, and no fidelity tier changes coefficients. F6h produces the offline cadence and body-cap equivalence. Rig check 12 runs matched FPS/body-cap arms, sharing F1's cadence arms, F2's body-cap divergence contract and F4's integrated gate. |

Out of scope for F6: identity, household formation and workplace assignment (F5); save
representation and snapshot timing (F7); movement authority (F2); the headline frame gate
(F4); rival finances (never written); and RLO (unchanged).

## 2. Current state at `d6b534a`

### 2.1 What the code does

**Money owner today is the represented consumer.** `FoodRepresentation.ConsumerId`
(`Core/FoodRepresentation.cs:28–32`) creates `RepresentationFactor` consumers per resident.
`CityBalance.ConsumerRepresentationFactor = 2` (`Core/CityBalance.cs:7`). Each `ResidentMeal`
owns one `ResidentBudget` (`Core/HouseholdFinance.cs:11–32`). The class comment says
"Individual budget unit, not an inferred family." `FoodCommerce.InitializeRepresentation`
(`Core/FoodRepresentation.cs:89–116`) refuses a roster whose size does not match the
fixed factor. Extra consumers share the anchor's routine (audit F5).

**Income comes from status, not attendance.** `HouseholdFinance.PostDay`
(`Core/HouseholdFinance.cs:62–97`) runs once per day per budget. It credits `DailyIncomeCents`,
funds the home-food basket, then pays housing, essentials and arrears. `IncomeSource`
(`:106–109`) is `ExternalEmployment` when the person is an adult worker with a non-empty
`DayPlaceId`. It is pension for retirees and support otherwise. `IncomeAmount` (`:111–113`)
scales support/pension by `SupportIncomePercent = 60` and `PensionIncomePercent = 70`
(`Core/CityBalance.cs:11–12`). Whether the person actually reached work does not matter.
`CitySimulator.MissRoutine` (`Core/CitySimulator.cs:111–115`) increments
`CitizenState.MissedActivities` (`Core/Model.cs:237`) and emits a diagnostic. It feeds
neither income nor employment.

**Employment changes are daily probabilities.** `ResidentLifecycle.BeginEmploymentCheck`
(`Core/ResidentLifecycle.cs:43–65`) uses `DailyJobLossChance = 0.001` and
`DailyJobFindingChance = 0.10` (`Core/CityBalance.cs:8–9`). It probes
`WorkplaceRouteProbes = 2` candidates (`:10`), checked once per day at home. Each business
gets 16 assignments (`Runtime/GamePlaces.cs:16`). No native employment, payroll or staffing
is created. The README states "resident budget income is an explicit outside flow".

**Spending limit and conservation.** `HouseholdFinance.SpendingLimit` (`:115–122`) is zero
while a hold or arrears exists. Otherwise it is the balance minus the next day's
housing+essentials+home-food reserve, capped at $10,000. `CashConserved` (`:103–104`) is the
save invariant:
`Opening + IncomeReceived == Balance + EssentialsPaid + HomeFoodPaid + PreparedFoodSpent + ExternalFoodSpent + HeldPurchase`.

**Native player checkout is the sale authority.** In `FoodCommerce`
(`Core/FoodCommerce.cs:421–451`), checkout first holds funds with
`HouseholdFinance.HoldPurchase`, then persists `MealStatus.Uncertain` *before* entering
native code ("Persist uncertainty before entering native code. No exception can trigger a
replay"). It then calls `market.Buy` and confirms or refuses. An exception keeps the
uncertain ownership and never issues a second order. `FoodSaleReceipts`
(`Persistence/FoodSaleReceipts.cs:13–30`) keeps native save-side idempotency markers
(`persistentcity:food-sale-v2:<IntentId>`). "Missing is absence of evidence, never proof that
a native purchase failed." An uncertain receipt is confirmed later from the original receipt
with no replay (`Core/FoodCommerce.cs:310–323`). Native daily statements are read, never
recalculated (`Runtime/NativeFoodAccounts.cs:37–56`).

**Rivals have unknown stock, a fixed service time and no queue.** `RivalFoodTerms.Capture`
(`Core/RivalFood.cs:77–87`) sets `Units = null` on every rival offer. It sets
`RivalServiceMinutes` to the configured `RivalAssumedServiceMinutes` (default 5, range 1–30;
`:13,30`). The evidence string reads "Estimated availability from advertised price/hours;
native stock and capacity unknown". `FoodBusiness.ServiceMinutes` (`Core/FoodDemand.cs:22–23`)
returns that constant for rivals. Player shops instead use `60 / MealsPerHour` from live
staffed throughput (`Core/FoodDemand.cs:40–42`). `ArriveRival` (`Core/RivalFood.cs:193–213`)
computes `finish = now + ServiceMinutes`. There is no reservation, queue position or
remaining-units check, so any number of residents can be served in parallel at one rival
with unlimited stock. Player shops reserve, queue and can stock out (`FoodCommerce.Reserve`,
`Core/FoodCommerce.cs:223`; arrival service/queue/hours refusals at `:360–399`; live
stock/staff refusal in `Runtime/NativeFoodMarket.cs:391–400`). `CompleteRival`
(`:214–224`) pays through `ExternalFoodSettlement.TryPay` (`:105–123`), an atomic own-ledger
debit to `ExternalFoodSpent` with a paid watermark, recorded in D3a's `RivalOutflowHistory`.
There is no native rival transaction (`NativeRivalCheckout = "Unsupported"`, `:227`).
`FoodAlternativeObservation` (`Core/FoodAlternativeObservation.cs`) exposes owner, business
type, creation day, hours, offers, access, door, temporary closure and
`CustomerServicePercent`. It reports stock as `FoodStockEvidenceKind.Unknown`. No floor area
or staffing is read for rivals.

**Ordinary shopping and leisure create activity without exchange.** Optional visits
(`Core/OptionalDestinations.cs:11,35`) choose `PlaceKind.Shop`/`Leisure` destinations. Only a
managed food scope (native checkout) or a modeled rival meal moves money. Subway fares are
"not an economic cost" (README). Car costs, taxes and non-food retail are not modeled.

**Player feedback today.** Ctrl+F8 (`Runtime/FoodEconomyPanel.cs:61–157`) refreshes text
every 3 s while open. Per managed shop it shows day purchases, meals, products, and observed
offer outcomes by cause (`price`, `travel/time`, `closed/late`, `stock`, `no staffed counter`,
`service/queue`, `preferred another`, `unknown`). These come from `FoodShopDay`
(`Core/FoodMarketHistory.cs:24–46`, classified at `:57–72`), kept for 14 days (`:51–56`). The
panel also shows purchase hours, the last three native address statements, the D3a rival
outflow line and city-wide resident account totals. It has no week-over-week comparison, no
record of what the player changed and when, and no household-strain view. While open, each
refresh scans every consumer (`food.Residents.Count(...)` at `:104,106` and the budget
projection at `:146–153`). That is O(consumers) every 3 s. Paid market research
(`Core/MarketResearchEngine.cs`, C4) is implemented and inactive because C3 found no safe
native fee path. The free panel shows only city-wide rival outflow totals (D3a), and own-shop
regulars only (B2, Peter's 31 October Q7).

### 2.2 Boundaries and invariants F6 must preserve

1. **Native sale authority.** A player-shop sale exists only through native checkout plus
   its receipt. PC never books revenue, stock, wages or rent into native accounts. Native
   statements are read-only.
2. **Duplicate-sale protection.** `Uncertain` is persisted before native entry. Receipts are
   keyed by `IntentId`. Confirmation reads the original receipt. No retry issues a second
   order, and no refund is fabricated.
3. **Exact money.** `Money` has integer units under the native-money rules. Every addition
   is checked before publication (`Money.TryAdd`). City totals use exact wide sums
   (`Money.TotalUnits`). `CashConserved` holds after every mutation.
4. **F1 accounting boundary** ([plan](f1-accounting-boundary.md)). One native minute is the
   input epoch (`Core/AccountingClock.cs:6–11`). Every event finishes before native time
   leaves its epoch. Lag is at most one game minute. Historical sales are forbidden
   (`HistoricalClockMisses`, `Core/FoodCommerce.cs:334`). `IsCaughtUpTo`
   (`Core/CitySimulator.cs:172–173`) drains all pending work. `BeginAccountingObservations`
   (`:185`) publishes boundary inputs before boundary events. Backlog is never paid down by
   forgiving or replaying.
5. **F3 bounded units** ([checks](../checks/pc-f3-bounded-bulk-work.md)). `StepBudget` posts at
   most `MaximumBudgetPostingsPerUnit = 32` budgets per unit, in minute/ordinal order, before
   meals at that minute (`Core/FoodFinances.cs:76–107`). The posting heap is derived from saved
   timestamps and has no cursor. Route retries are finite. Campaign capture is coherent and
   publication is resumable. Any new F6 bulk work uses the same unit discipline.
6. **Rival read-only rule.** No native rival write, receipt or checkout. Estimated
   availability is always labeled (Peter's 31 October Q4).
7. **Paid-research boundary.** Named rival figures, market shares and city-wide trait
   distributions belong to paid research (31 October Q1/Q2/Q7). Research stays off until a
   proven native fee path exists.
8. **Unsupported businesses stay native-owned.** Whole-business ownership transfers only at
   native accounting boundaries (AGENTS.md, 24 October).
9. **Saves.** Strict readers. Unknown or future payloads are refused and preserved. Exact
   ids and money across migration. Writer 25 is current (`Core/Model.cs:256`). Caps are
   256 Mi input characters, 50,000 citizens, 4,000,000 embedded records and 20,000 places
   (`Persistence/SaveCodec.cs:38–41`).

## 3. Design

The principle, from the audit: add causal loops and observable decisions, not more preference
coefficients. Every link below is a logical Core event with a persisted, exact effect, plus a
read-only observation. No link depends on a body, the camera or frame rate.

### 3.1 Chosen causal links

| Link | Source event (Core) | Mod-owned effect | What the player can observe | Slice |
| --- | --- | --- | --- | --- |
| L1 Travel/access → attendance | Work arrival/admission against the employment schedule. Late or missed arrival is already detected by routing (`MissRoutine`). | Attended minutes accrue per shift. A missed shift records a miss on the employment. | Inspector: "missed work twice this week (route blocked)". | F6e |
| L2 Attendance → wage | Shift end, or the midnight posting for a shift that crosses midnight. | Wage = `round(hourly rate × attended minutes / 60)` in exact units, credited to the F5 money owner as outside employment income. No wage for unattended minutes. | Household-strain words; city "income earned vs scheduled" in the panel. | F6e |
| L3 Attendance → employment | The existing daily employment check. | Job-loss chance rises with misses in the last 14 days: `base × (1 + k × misses)`, capped. Finding chance is restricted to vacancies that have a reachable route (existing probes). | Inspector: "lost job after repeated absences". | F6e |
| L4 Workplace closure → attendance | Workplace venue closed or temporarily closed at shift start (existing hours/closure evidence). | Shift not scheduled, so no wage. Counted as a closure day, not a personal miss. | Inspector: "workplace closed today". | F6e |
| L5 Resources → purchasing | Every decision reads `SpendingLimit` from the F5 money owner. | The existing gate, now fed by attended wages instead of status income. Lower income means fewer prepared meals and more home food. | Own-shop "unaffordable" share rises; household-strain words. | F6e/F6f |
| L6 Resources → travel/schedule | Optional-destination admission for a *paid* destination (section 3.3 C9). | Admitted only when the owner's spending limit covers the modeled visit price. Free destinations (parks, beach) are unaffected. | Fewer paid outings when money is short (inspector words). | F6f |
| L7 Ordinary exchange | Admission at a paid non-food shop or leisure venue that is not player-owned. | Exact own-ledger debit to a new `DiscretionarySpent` account with a watermark, like D2's rival settlement. City dated outflow totals. | Free panel line "spending outside your businesses", city totals only. | F6f |
| L8 Service capacity → rival outcomes | Rival arrival/service. | Estimated lanes and a queue, plus an estimated sellable count per meal period (section 3.2). A full or sold-out rival refuses, and B1/D4 memory records a bad visit. | Rival line labeled "estimated". More own-shop custom when a rival is saturated. | F6b |
| L9 Shortage/staffing/hours → demand → memory | Existing: player stock/staff/hours refusals and B1 memory. | Unchanged mechanics. F6 adds *attribution over time* (section 3.6). | Week-over-week causes by hour/meal; change journal. | F6c/F6g |
| L10 Employment ↔ businesses | Workplace assignment (F5) and closures (L4). | Workers' income depends on their workplaces being open and reachable. No native payroll is touched. | City income line; household sustainability metric. | F6e |

Deliberately not linked in F6: native payroll to resident wages, player staff identity to
residents, rent from residents to player-owned property, taxes, car/fuel/fare costs, and
supply chains. These are outside boundaries (section 3.3).

**Wage rate.** `HouseholdFinance.Create` draws a daily income band per budget
(`Core/HouseholdFinance.cs:44–48`). F6e converts it to an hourly rate over the employment's
scheduled weekly minutes. Full attendance at the scheduled shifts then reproduces today's
weekly income within rounding. Rounding remainders carry on the employment
(exact-units accumulator), so no cents are lost or created. Support and pension stay daily
postings, unchanged. Whether a non-attending worker falls back to support is decided as
follows: support begins only after job loss. The plan does not let a worker collect support
while employed and absent.

### 3.2 Calibrated rival competition (mod-owned estimates)

The current asymmetry: a rival accepts unlimited parallel customers with unlimited stock at a
fixed 5-minute service. A player shop is limited by live staffed throughput, a queue and real
stock. F6b replaces the unlimited terms with **bounded mod-owned estimates**. Every place they
appear carries the label "estimated". They never claim native stock, and they never read or
write rival finances.

- **Estimated service lanes.** `RivalServiceLanes(businessType)` comes from a bounded config
  profile with defaults calibrated against observed player-shop throughput at comparable menu
  sizes. The calibration source is F6a's measurement of player `MealsPerHour` distributions
  on supported fixtures. Each lane serves one customer per `RivalAssumedServiceMinutes`. A
  rival gets the queue rules player shops already use: arrival joins the next free lane, and
  the existing patience/deadline refusals apply. Implementation: a transient per-rival
  `NextFreeAt[lanes]` array.
- **Estimated sellable units per meal period.** `RivalMealPeriodUnits(businessType)` is the
  total servings the model assumes a rival sells in one breakfast, lunch or dinner window
  across all its offers. When the window's count is reached, the rival refuses with
  `ShopExperience.Stockout`, which D4 memory already records for modeled rival visits. The
  label reads "modeled sell-out (estimated)". It is not a claim about native inventory. The
  count resets at each meal-period start. There is no modeled restock purchase and no rival
  cost or profit.
- **Derived, not new, saved state.** The current period's served count per rival is
  reconstructed at load from saved current-day `MealOutcomeRecord`s (`TargetKind = Rival`,
  paid, `PlaceId`, `CheckoutAt`; `Core/FoodToday.cs:37–54`) plus active `Serving`
  commitments. The lane schedule is reconstructed from active `Serving` meals' `ServiceAt`.
  This keeps F6b schema-free. Reload, cadence and order equivalence are tested exactly. A
  config change between saves changes terms only at a new decision context, following the
  E5c rule. If review shows that a *held* decision captured under old terms must replay with
  them, F6b adds the two integers to the held image under the next free schema (section 5).
- **Choice-time knowledge.** Residents choose with the same evidence class as for player
  shops. A rival known to be full at decision time is refused with the existing
  "Service unavailable"/queue reasons. Unknown remains unknown and is not treated as
  available.
- **Calibration target.** For a rival and a player shop with the same menu, price, hours,
  distance and service rating, F6a measures served meals per day under rising demand. Today
  the rival's curve has no ceiling. After F6b the two curves must saturate within a declared
  band of each other. The F6h default is ±25 % of the player shop's staffed ceiling at the
  default profile. Rival `RivalChoiceWeight` (`Core/RivalFood.cs:12`) stays a separate
  preference knob, not a capacity substitute.

### 3.3 Outside-economy boundaries

| # | Flow | Counterpart | Authority | F6 treatment |
| --- | --- | --- | --- | --- |
| C1 | Employment wage | Outside employer (abstract) | PC exact ledger | Attendance-based (L2). Never sourced from native payroll. A workplace that is a business does not pay from its native accounts. |
| C2 | Pension, support | Outside (abstract) | PC exact ledger | Unchanged daily posting. Support follows employment status after job loss. |
| C3 | Housing, essentials | Outside (abstract) | PC exact ledger | Unchanged per budget. Moves to the F5 money owner (household) if B4 chooses household wallets. No payment to a player-owned residential building. |
| C4 | Home-food basket | Outside grocery (abstract) | PC exact ledger | Unchanged. |
| C5 | Prepared food at a managed player shop | Native checkout | **Native** sale + PC receipt | Unchanged hold → uncertain → confirm/refuse. |
| C6 | Prepared food at a rival | Modeled outside sale | PC own ledger + watermark | Unchanged settlement. Availability now bounded (section 3.2). |
| C7 | Food at an unsupported player business | Native customers | Native | No PC visit or exchange. The business stays native-owned. |
| C8 | Native player costs (payroll, rent, wholesale, marketing) | Native | Native | Read-only display only. |
| C9 | Paid non-food shop/leisure visit, not player-owned | Modeled outside sale | PC own ledger + watermark | **New** (L7): a modeled price per visit class from a bounded config table, debited at admission. |
| C10 | Visit to a player-owned non-food business | Native customers | Native | Activity only, no modeled spend. A PC spend there would duplicate native customer revenue. |
| C11 | Transit fares, car costs, taxes, savings interest | None | — | Not modeled. Declared abstraction; the panel footnote says so. |
| C12 | Rival finances, stock, staff | Native, inaccessible | Native, read-only | Never written. Estimates are labeled mod-owned. |
| C13 | Visitors' (B2) origin income | Outside | PC exact ledger | A visitor arrives with an endowment and spends from it. No wage, housing or essentials (section 3.8, B2). |

### 3.4 Preventing double accounting

1. **One counterpart per exchange.** Every debit has exactly one counterpart kind:
   NativeSale (receipt namespace), ModeledOutside (own watermark), or OutsideFlow (posting).
   A Core type guard (the D3a pattern) rejects a ledger entry without exactly one kind.
2. **No modeled exchange at a player-owned place.** C9 destinations exclude every place
   whose current owner is the player, whether managed or unmanaged. Ownership is re-read at
   admission. An ownership transfer invalidates a pending modeled visit with no debit. This
   is tested with ownership flips during the visit, around save/reload and at the accounting
   hour.
3. **Wages never touch native payroll.** No PC code reads native payroll into a wallet or
   writes it. A source-reachability guard test (the E5a pattern) fails on any call path from
   F6 wage code into native finance members.
4. **Extended conservation identity** (F6d contract). The left side is
   `Opening + EmploymentIncome + OtherIncome`. The right side is
   `Balance + Essentials + HomeFood + PreparedFood + ExternalFood + Discretionary + Held`.
   It is checked per money owner after every mutation and at save. It overflow-checks both
   sides, as `CashConserved` does today.
5. **Exactly-once watermarks.** Wage accrual and discretionary debits carry the same style of
   day/shift/intent watermark as D2. A repeated event, replayed continuation or reload adds
   nothing.
6. **F1 epoch.** Every new debit or credit is a logical event inside the current native
   minute epoch. Nothing is posted for an earlier epoch. Overload holds the calendar (F1); it
   never forgives a wage or a visit.
7. **F3 units.** Midnight wage settlement for shifts that cross midnight rides the existing
   32-unit posting heap ordering (minute, ordinal), so postings stay before meals.

### 3.5 Controlled interventions and measurement

**Offline harness (F6a, extended in F6h).** A deterministic Core scenario runner reuses the
F1 supported fixture: 957 residents / 1,914 consumers, minute-stamped inputs and
one-minute walking routes. It runs owned shops, rivals and the current population model, and
after F5 the F5 population. Interventions are timestamped inputs published at F1 epoch
boundaries, never mid-epoch mutations. Each family runs **6 simulated weeks**: weeks 1–2
baseline, weeks 3–4 intervention, weeks 5–6 reverted. Each uses **5 seeds** and a matched
control arm with the same seed and no intervention.

| Family | Intervention arm(s) |
| --- | --- |
| Price | One product −25 % and +25 %. Whole menu +50 %. |
| Staffing | Lunch staffed counters halved; one day with zero staff. |
| Hours | Breakfast window removed; evening close 2 h earlier. |
| Shortages | One product stocked out on 3 lunch periods; one whole-shop stockout day. |
| Employment | 10 % of workplaces closed for the 2 weeks (L4). Separately, 5 % of workers' routes blocked (L1/L3). |
| Access | Walking route time to the shop +50 % (access-cost input). Shop door temporarily closed on 2 days. A second shop site at twice the distance from the residential cluster. |
| Rival | One nearby rival's estimated lanes halved; a rival price −25 %. |

**Metrics, emitted per day and per arm:**

- *Demand response:* own purchases by meal and hour, revenue, chosen/considered offers and
  causes. Arc elasticity for the price arms.
- *Market share:* the share of resolved meals in the shop's catchment (residents whose
  decision considered the shop) going to the own shop, rivals and home food, from
  `MealOutcomeRecord` target kinds. This is a **diagnostic/experiment metric**, exposed in the
  harness and the rig console. It is not free player UI (section 3.6, Q3).
- *Recovery:* days after reversion until the 3-day mean own purchases is within the control
  arm's seed band (mean ± 2σ). Also the residual effect via B1 memory after 14 days.
- *Household sustainability:* the share of money owners in arrears, mean unfed days, median
  and p10 balance trend per week, job-loss/finding counts, and income earned vs scheduled. A
  run fails on unbounded drift: p10 balance falling every week in the control arm, or arrears
  share above the F6h threshold in control.
- *Stable accounting:* the extended conservation identity holds for every owner every day.
  PC confirmed receipts equal native sales units for managed shops. Modeled outside totals
  equal the sum of watermarked debits. Every arm has zero duplicate receipts and zero
  `HistoricalClockMisses`.

**Tradeoff acceptance (F6h).** Each family must produce an effect outside the control seed
band. No arm may dominate. In particular, the +50 % menu price must not maximize weekly
operating revenue minus measured wholesale cost in every seed, and no staffing cut may raise
served meals. Every gain must have a measured cost. Recovery must be finite (≤ 14 days) for
every reverted arm. F6a first records **today's** values, before F6b–F6g, so every later
slice reports before/after numbers. Thresholds are set in F6h from those measurements and
recorded with seed bands. They are not invented in advance.

**Rig protocol.** Matched checkpoints per arm in the disposable FT city: 1 game week
baseline, 1 week intervention, 1 week recovery. Section 7 specifies the arms and reads. The
rig span is shorter than the offline protocol because game time is expensive. It checks
that native behavior matches the offline direction and accounting is exact. It does not
re-derive thresholds.

### 3.6 Player feedback (read-only UI)

Where and how often:

- **Ctrl+F8 food panel** (`Runtime/FoodEconomyPanel.cs`). Text is still built only while open,
  every 3 s, from incrementally maintained aggregates.
  - Per own shop, **this week vs last week**: purchases, revenue and the delta, from the
    existing 14-day `FoodShopDay` rows. No new history.
  - **Top lost-sale causes by meal and hour**, using the existing cause columns plus the
    purchase-hour array. Example: "Lunch 12:00–13:00: 41 residents found no staffed counter
    (last week 3)." Facts only, not prescriptive advice (Q4).
  - **Change journal (F6g):** "Day 23: burger price $6 → $7. Day 25: closed at 20:00 instead
    of 22:00." The panel shows the 7-day purchase change before and after each entry and
    marks overlapping changes as "mixed". Entries come from detected native read
    differences at accounting boundaries (price, hours, staffed counters, stock-out days). PC
    never changes the shop.
  - **Rival line:** the existing D3a outflow total, labeled "estimated availability (modeled
    service limits)", plus how many own-shop customers chose the player because a rival was
    full or sold out, city-wide count only. No rival names or shares (paid-research boundary).
  - **Residents' money (city-wide):** income earned vs scheduled this week, arrears share,
    the share who skipped prepared food for lack of money, and the outside discretionary
    spending total (F6f).
- **Resident inspector words** (A1/B2 word style, not numbers): "short on money this week",
  "missed work (route blocked)", "lost job", "workplace closed today", "skipped the cinema to
  save money", "found <rival> full (estimated)".
- **Pacing notice:** unchanged F1 behavior.

Strings are built only for visible UI or on rig request. The existing O(consumers) panel
scans (`:104,106,146–153`) are replaced by counters updated at the transitions F6 already
touches. This is the slice's expected cheap performance win, measured in F6c.

### 3.7 Detail-level and hardware independence (with F1/F2/F4)

- Every F6 state change happens in a Core logical event, ordered by the simulator, inside an
  F1 epoch. No F6 code runs from presentation, a physical lease, OnGUI or a camera query.
- F6 adds no fidelity tier. Population sampling, coefficients and caps do not vary with
  frame rate, body cap, graphics settings or hardware.
- **F1:** the same logical weeks at 30/60/120 rendered FPS and different event/time budgets
  must give byte-identical economic ledgers. F6h runs this offline on F1's cadence fixture
  with F6 loops implemented. Rig check 12 shares F1's pending cadence arms.
- **F2:** attendance (L1) and service (L8) use logical arrival. F6 accepts whatever
  body-induced divergence F2 declares and tests the economic ledgers against that declared
  bound across body caps 0 and default. Before F2 lands, F6 reports measured body-cap
  differences as flagged evidence, not as a pass.
- **F4:** F6 slices report mod-work under the current gate (section 6) and feed F4's
  integrated contract with the per-event costs measured in this plan.

### 3.8 Assumptions about F5

F6 relies on the shared F5 baseline. If the merged F5 plan has a "Differences from the shared
baseline" subsection, the column "If F5 differs" applies.

| Baseline | F6 relies on | If F5 differs | Affected F6 slices |
| --- | --- | --- | --- |
| **B1** One logical Person per independent consumer, stable `PersonId`. Existing consumer ids preserved or mapped one-to-one. | Every demand unit, wage and debit is keyed by `PersonId`. F6 watermarks are per person. Rival load derivation counts outcome records per person. | If F5 keeps represented extra consumers, F6e attaches attendance only to the anchor and wages to the anchor's money owner. Extra consumers would then remain on outside allowance, and F6 must report that L1–L3 apply to anchors only. The F6d shape gains a consumer→person mapping key. | F6d, F6e, F6f |
| **B2** Kind Resident or Visitor. Residents in exactly one Household with a home. Visitors have origin/stay and no home. | Residents pay housing/essentials (C3). Visitors spend from an endowment with no wage (C13). | If visitors have no money model in F5, F6f treats visitors as non-spending for C9 and excludes them from sustainability metrics. If households are optional, sustainability is per person. | F6f, F6h metrics |
| **B3** Employment is Person → Workplace with its own schedule. At most one wage source per employment. No native employment. | L1–L4 attach to the employment record. The wage rate uses scheduled weekly minutes. | If F5 keeps `DayPlaceId` plus persona templates, F6e derives the schedule from `DailyRoutine` work windows and stores attended minutes on the person, with a larger per-person tuple. Multiple jobs per person would need one accrual per employment, which increases the byte budget (section 5). **Request to F5:** exclude player-owned businesses from logical workplace assignment, or flag them, because PC residents are not the player's native staff (C1). | F6d, F6e |
| **B4** Money has exactly one owner per unit (person or household wallet, F5 decides). Every demand unit has exactly one owning Person. | The extended conservation identity is per owner. Spending limit = owner balance − owner reserve. A person's purchase draws from their owner. | With *household* wallets, F6f owns the pooling policy: reserve per household, plus a per-person daily discretionary share so one member cannot drain the household. With *person* wallets, pooling is absent and housing is split by deterministic share. Both are planned; the F6d shape has one owner key either way. | F6d, F6f |
| **B5** Bodies are capped presentation. Identity, money and economics never depend on a body. | L1/L8 use logical arrival. Section 3.7. | If F5 lets body selection change logical outcomes, F6 rig check 12 fails by design and F6 waits for F2. | F6e, F6h |
| **B6** Deterministic, mod-owned, stable ordering by id, seeded draws, no native references in saved state. | Rival load reconstruction, wage rounding carry and job-loss draws are keyed by stable ids. | Any nondeterminism blocks F6d. Any persisted native reference is refused by F6's strict reader. | All |

**Can start before F5 lands:** F6a (harness and baseline on the current model; it gains an F5
fixture later), F6b (rival calibration, independent of identity), F6c (feedback v1 on
existing history and per-consumer budgets). **Must wait for F5:** F6d (needs F5's identity
schema and `PersonId`/`HouseholdId`/`WorkplaceId`), F6e (B3), F6f (B2/B4). F6g needs F6d for
its journal shape. F6h runs last.
