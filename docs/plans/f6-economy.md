# City-audit F6: connected economic feedback

6 November 2026: Owner decisions after approval are in [docs/decisions/](../decisions/); this plan is frozen. Slice status is in the checks files indexed by [CHECKS.md](../../CHECKS.md).

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
| A6 | Provide player feedback that makes the effects understandable and actionable. | Section 3.6 specifies what the player sees, where and how often: Ctrl+F8 week-over-week daily evaluated-offer totals by cause (F6c) and by meal period (F6g), with no hour or resident attribution, a first-observed change journal, labeled rival estimates and resident inspector words. Everything is read-only UI. Slices **F6c** and **F6g**. Rig check 11 covers panel text, geometry and screenshots. |
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
| L9 Shortage/staffing/hours → demand → memory | Existing: player stock/staff/hours refusals and B1 memory. | Unchanged mechanics. F6 adds *attribution over time* (section 3.6). | Week-over-week evaluated-offer causes (daily totals, then by meal period); first-observed change journal. | F6c/F6g |
| L10 Employment ↔ businesses | Workplace assignment (F5) and closures (L4). | Workers' income depends on their workplaces being open and reachable. No native payroll is touched. | City income line; household sustainability metric. | F6e |

Deliberately not linked in F6: native payroll to resident wages, player staff identity to
residents, rent from residents to player-owned property, taxes, car/fuel/fare costs, and
supply chains. These are outside boundaries (section 3.3).

**Wage rate.** `HouseholdFinance.Create` draws a daily income band per budget
(`Core/HouseholdFinance.cs:44–48`). F6e converts it to an hourly rate over the employment's
scheduled weekly minutes. Full attendance at the scheduled shifts then reproduces today's
weekly income within rounding. Rounding remainders carry on the employment
(exact-units accumulator), so no cents are lost or created. Support and pension stay daily
postings, unchanged. Support begins only after job loss, so an employed worker who misses a
shift earns nothing for it and does not collect support instead.

**Switch-over from the daily employment credit.** F6e removes the `ExternalEmployment` daily
credit from `PostDay`. Today that credit is paid at midnight whenever `IncomeSource` resolves
to employment (`Core/HouseholdFinance.cs:69–75,106–109`). F6e then credits employment only
through shift accrual. The switch follows the existing saved start-day pattern
(`ChoiceStartsDay`/`AwarenessStartsDay`/`RivalStartsDay`, `Core/FoodCommerce.cs:121–123,205`).
A saved city-level `WageStartsDay` is set to the first day boundary after F6e first runs on a
city. Days before `WageStartsDay` keep the legacy daily employment posting. Shifts on those
days accrue nothing, including a shift that is already in progress when F6e first runs.
From `WageStartsDay` on, `PostDay` posts no employment income, while pension and support
continue. This gives each day exactly one employment income source, including after a
reload across the switch-over day. The day is persisted, never recomputed from the load
time.

### 3.2 Calibrated rival competition (mod-owned estimates)

The current asymmetry: a rival accepts unlimited parallel customers with unlimited stock at a
fixed 5-minute service. A player shop is limited by live staffed throughput, a queue and real
stock. F6b replaces the unlimited terms with **bounded mod-owned estimates**. Every place they
appear carries the label "estimated". They never claim native stock, and they never read or
write rival finances.

- **Estimated service lanes.** `RivalServiceLanes(businessType)` comes from a bounded config
  profile with defaults calibrated against observed player-shop throughput at comparable menu
  sizes. The calibration source is **native** staffed throughput. The rig reads it on
  representative staffed player shops through `shop_state` staffed-counter readback and PC's
  live `MealsPerHour` diagnostic (rig check 8), and records it as evidence. Offline fixtures
  take `MealsPerHour` as an input, so F6a fixtures are used only to show the before/after
  saturation curves, never as the calibration source. Each lane serves one customer per `RivalAssumedServiceMinutes`. A
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
   whose current owner is the player, whether managed or unmanaged. `Place` (`Core/Model.cs`)
   and `Runtime/GamePlaces.cs` carry no owner, and Core cannot read native state. F6f
   therefore adds a **bounded Runtime ownership index** for paid non-food destinations. It
   reads the same native registration members the existing adapters already read:
   `BuildingRegistration.RentedByPlayer` and `businessOwnerRivalId`
   (`Runtime/RivalFoodCatalogue.cs:106,136`; `RentedByPlayer` also in
   `Runtime/NativeFoodAccounts.cs:38`). It is refreshed at each hourly accounting boundary in
   F3-style work units of at most 16 places. Each unit is published atomically with that
   boundary's revision, and Core reads only the published index at admission. A modeled
   spend is **refused with no debit**, while the visit remains an activity, when the place's
   entry is missing, the place is player-owned, or its revision is older than the current
   boundary. A player purchase between refreshes therefore takes effect at the next boundary.
   The residual window is at most one game hour, and it can produce only labeled outside
   spend, never native revenue. The window is declared and tested. An ownership transfer
   also invalidates a pending modeled visit with no debit. Tests cover ownership flips during
   the visit, around save/reload, at the accounting hour and with a stale index. No new
   native member is introduced.
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
    existing 14-day `FoodShopDay` rows (F6c, no new history).
  - **Observed offer outcomes by cause, as daily totals (F6c).** `FoodShopDay`
    (`Core/FoodMarketHistory.cs:24–46`) keeps rejection causes only as daily totals.
    `PurchaseHours`/`PurchaseMeals` count successful purchases only. `RecordChoices`
    (`:57–72`) counts evaluated offers, including repeated decisions by the same resident,
    not unique residents. F6c therefore shows week-over-week **daily totals of evaluated
    offers** per cause, for example "No staffed counter: 41 evaluated offers this week (last
    week 3)". It never presents these as resident counts or as hour or meal locations.
  - **Lost-sale causes by meal period (F6g, new bounded history).** Locating rejections by
    meal needs new saved counters. F6d adds fixed-width counters per meal period (legacy,
    breakfast, lunch, dinner) for four actionable causes (staffed counter, stock,
    service/queue, closed/late) on each `FoodShopDay` row. They share that row's existing
    14-day retention (`Core/FoodMarketHistory.cs:51–56`), so both the current and the
    previous 7-day window are kept. Byte and record budget are in section 5. Days recorded
    before the counters existed, including every migrated day, are labeled "not recorded",
    never estimated. A comparison window with any "not recorded" day says so instead of
    showing a delta. Tests cover the 14-day rollover (the oldest day drops with its row, so
    the previous window always has exactly the days the row history has) and require the
    same projection after reload. They count evaluated offers. Unique-resident counting is not
    supported and not planned here. Example: "Lunch: 41 evaluated offers found no staffed
    counter (last week 3)." Hourly resolution is not planned. Facts only, not prescriptive
    advice (Q4).
  - **Change journal (F6g):** "First observed day 23, 12:04: burger price $6 → $7. First
    observed day 25: closes at 20:00 instead of 22:00." The base has no complete hourly image
    of every managed shop. `NativeCampaignAwareness.SampleCampaignBoundary`
    (`Runtime/NativeCampaignAwareness.cs:48`) captures campaign fields only.
    `RefreshRoutineHours` (`Runtime/NativeFoodHours.cs:17`) refreshes schedules. Supply
    (prices, stock, staffed counters) is read on demand by `NativeFoodMarket.ReadSelected`
    (`Runtime/NativeFoodMarket.cs:249`), and `DecisionMenu` (`:337`) caches it for the current
    observation minute, so a quiet shop may have no fresh supply image. F6g therefore
    **consumes the observations that already happen**. It compares each demand-time
    `ReadSelected`/`DecisionMenu` image and each hours refresh against that shop's last
    observed fingerprint. A difference records a **first-observed** change, labeled as such
    and stamped with the observation minute, not the moment the player made it. A shop that
    nobody evaluates records the change later, or not at all, and the panel says "changes
    are recorded when residents next check the shop". A stock-out day is recorded from
    observed stock refusals. The panel shows the 7-day purchase change before and after
    each entry and marks overlapping changes as "mixed". PC adds no native read for the
    journal and never changes the shop.
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

## 4. Slice breakdown

Effort is in focused engineer-days, including review and offline proof but excluding rig
infrastructure. Each slice branches from the then-current `codex/persistent-city` once its
prerequisites have merged. Slices are siblings, not a branch chain. Each implementation PR
adds its own `docs/checks/<slice>.md`, `docs/perf/<slice>.md` and a numbered batched-rig list
drawn from section 7. Offline success is not native proof.

| Slice | Scope and effort | Offline/headless tests | Perf measurement | Schema | Dependencies / branching |
| --- | --- | --- | --- | --- | --- |
| **F6a — intervention harness and baseline** (slice 1), 2–3 days | `tools/economy-interventions` deterministic Core scenario runner. Uses the F1 supported fixture, timestamped inputs at epoch boundaries, six-week arms × 5 seeds plus controls (section 3.5), and JSON metrics. Records **today's** baseline, including the unlimited rival curve. No Runtime or Core behavior change. | Determinism: the same seed gives byte-identical metrics, including across a mid-run save/reload. Inputs mutate only at epoch boundaries. Conservation identity checked every day. A fault injection (a deliberately duplicated debit) must turn the ledger check red. | Harness runtime and allocation per simulated week at 1,914/4,000 consumers (tool cost only, no gate). | None | Independent. Can merge before F5. Touches only `tools/` and `tests/`. |
| **F6b — rival service limits (mod-owned estimates)**, 2–4 days | `RivalFoodSettings` gains bounded `RivalServiceLanes`/`RivalMealPeriodUnits` profiles by business type. Transient lane schedule and period count. Refusals use the existing reasons and D4 memory. Labels on diagnostics and the D3a line. Reconstruction from saved outcomes and active meals. Owns `Core/RivalFood.cs` and rival settings; does not edit `FoodEconomyPanel.cs` (F6c adds the label text). | Queue/lane ordering; sell-out at exactly N; period reset; refusal before debit; no debit on refusal; reload mid-queue and mid-service rebuilds the same lanes and counts byte-for-byte; 30/60/120 cadence equivalence; config change only at a new decision context; held replay under old terms; rival ownership flip; no native rival write (reachability guard); estimate labels present. F6a before/after curves. | Arrival/complete cost p50/p95/max and allocation (target 0 B steady state). Attach-time reconstruction at 1,914/10,000/100,000 consumers. Retained bytes for 20,000 rivals. | None expected. Conditional: two integers in the held image if review requires replay of old terms (section 5). | Independent of F5. Sibling of F6a and F6c. |
| **F6c — feedback v1**, 2–3 days | Panel: week-over-week per shop from the existing 14-day rows, daily evaluated-offer totals by cause (no hour/meal/resident attribution), rival estimate label, city money summary on the current per-consumer budgets. Incremental aggregates replace the O(consumers) panel scans. Inspector words for current budget states. Owns `Runtime/FoodEconomyPanel.cs` and inspector word tables. | Projections from fixture histories (golden text with invariant checks: numbers equal row sums and are labeled evaluated offers, the week boundary is correct across the 14-day rollover, the partial first week is labeled); counters equal full rescans after every mutation, reload and migration; no paid-research fields (D3a type guard extended); strings built only on request. | Panel build cost at 1 / 64 / 1,024 shops. Before/after scan cost at 1,914/10,000/100,000 consumers. Hidden-panel cost 0 B and no calls. | None | Independent of F5. After F6b only for the rival label wording; can merge first with a neutral label. |
| **F6d — economic ledger contract (inactive)**, 3–4 days | Persistence owner. Employment accrual tuple, extended owner accounts (`EmploymentIncome`, `Discretionary` plus watermark), dated outside-spending totals, change-journal shape, per-shop last-observed fingerprint, 14-day meal-period cause counters on `FoodShopDay` rows, city-level `WageStartsDay`. Strict reader; exact migration from F5's schema; inactive until F6e/F6f/F6g (E5d pattern). | Exact predecessor round-trip; migration preserves every id and money unit; migrated days' meal-period counters labeled "not recorded", empty fingerprints, unset `WageStartsDay`; future/unknown/duplicate/missing fields refused with originals kept; bounds and one-over refusal; conservation-identity overflow; record and character counts; projection back to the predecessor byte-identical. | Bytes/records added at E5d fixtures (957/5,000/50,000; CF32/CF64); read/write/migration latency, separate from the play gate. | **Yes**: one next-free version after F5 (and after F7 if F7 lands first). | Waits for F5's identity schema. Serializes with F7's schema slice (section 5). |
| **F6e — attendance-based wages and employment feedback**, 3–4 days | L1–L4, L10: accrual at shift end, rounding carry, miss record, job-loss sensitivity, closure days. Midnight-crossing shifts settle on the posting heap. Diagnostic console command for a logical workplace closure, gated to the named disposable FT city. | Full attendance reproduces old weekly income within rounding; no double income on the switch-over day (`WageStartsDay`), including a shift already in progress and reload across that day; late/missed/closed shifts; shift across midnight; interruption and reload at every shift stage; exactly-once watermark; job-loss draw stability; 30/60/120 cadence equivalence; no native payroll path (reachability guard); conservation each day. F6a employment arms. | Per-shift accrual cost; posting-unit p95 unchanged at 1,914/10,000/100,000; zero steady-state allocation. | Uses F6d | After F5 (B3) and F6d. Sibling of F6f. |
| **F6f — household resources and ordinary exchanges**, 3–5 days | B4 pooling policy (household reserve, per-person daily share) or person wallets per F5. C9 modeled paid visits with a bounded price table; L6 admission gate for paid destinations; bounded Runtime ownership index refreshed at hourly boundaries (section 3.4); dated outside-spending totals and a free total line; visitor endowment spending (C13). | No debit at player-owned places (ownership flips, stale/missing index entries, reload, accounting hour); refusal before debit; exactly-once watermark; pooling never overdraws the reserve; visitors never pay housing; conservation; sustainability metrics in F6a arms; free output limited to totals (type guard). | Admission gate cost; owner lookup; zero steady-state allocation; panel line built only on request. | Uses F6d | After F5 (B2/B4) and F6d. Sibling of F6e. |
| **F6g — change journal, meal-period causes and feedback v2**, 3–4 days | Records first-observed price/stock/staffed-counter changes inside the existing demand-time `ReadSelected`/`DecisionMenu` path and hours changes in `RefreshRoutineHours`, against a saved last-observed fingerprint per shop. Stock-out days come from observed refusals. Bounded journal plus 14-day meal-period cause counters for this-week/last-week comparison (section 5). Panel before/after deltas with "mixed" marking and "first observed" labels. Household-strain and outside-spending lines from F6e/F6f. Touches the `NativeFoodMarket` read path, so it coordinates with E5b's supply integration. | Detection exactness vs fixture edits at observed and unobserved shops (unobserved: no entry until the next read); no entry on unchanged reads; observation minute stamped, never back-dated; fingerprint and counters identical after reload; 14-day rollover keeps exactly two 7-day windows; windows with migrated days labeled "not recorded"; bound and eviction order (oldest first, counted); ownership handoff clears the business's entries; overlapping changes marked; zero added native reads (read-counter assertion); no PC write to the shop. | Added cost and allocation per `ReadSelected`/`DecisionMenu` observation and per hours refresh (fingerprint compare, O(offers ≤ 8)) at 1/64/1,024 shops; native read counts equal before/after; flagged tails; panel build. | Uses F6d (journal, fingerprint and counter shapes) | After F6c and F6d. F6e/F6f only for their lines. Composes with E5b's read path. |
| **F6h — integrated multi-week acceptance and calibration**, 2–3 days | Runs every F6a family with all loops implemented, on the F5 population fixture. Sets thresholds and default profiles (rival lanes/units, job-loss k, C9 prices) from measurements. Cadence and body-cap equivalence. Final rig handoff list. | All section 3.5 tradeoff and recovery criteria; detail-independence (section 3.7); 100,000-consumer stress posting/advancement with F6 loops. | Full section 6 table; flagged ≥ 30 ms intervals attributed or retained as flagged. | None | Last. After F6b–F6g; composes with F1/F2/F3 fixtures. |

**Parallelism.** F6a, F6b and F6c can start now, in parallel, with disjoint file ownership
(tools/tests; rival Core/settings; panel/inspector). F6d starts when F5's identity schema has
merged. F6e and F6f are siblings after F6d. F6g follows F6c and F6d. F6h is last. No F6 slice
edits F5's identity/assignment code or F7's codec beyond F6d's own additive contract.

## 5. Save schema and migration impact

Against writer 25 (`Core/Model.cs:256`):

| Slice | Schema need |
| --- | --- |
| F6a, F6c, F6h | None. |
| F6b | None expected: rival load is derived from saved outcomes and active commitments. Conditional only: if held decisions must replay old estimate terms, two bounded integers join the held image in the next free version. Prefer folding them into F6d if F6b has not merged by F6d's shape freeze. |
| F6d | **One additive version** carrying everything F6e/F6f/F6g persist. Inactive at merge. |
| F6g | Uses F6d's journal, last-observed fingerprint and meal-period counter shapes. If any of these shapes is not frozen with F6d, F6g takes a separate next-free version for them; a second F6 version is allowed, but not two formats under one number. |

**Shape and migration (A2/E5d style).** The strict reader accepts exactly the predecessor
schema's shape and the new version. It refuses unknown, future or duplicate fields, bad
enums, out-of-range or noncanonical money tokens and over-bound collections, keeping the
original payload untouched. Migration is exact: every `PersonId`/`HouseholdId`/`WorkplaceId`
and every money unit is preserved. New accounts start at zero. `EmploymentIncome` starts at
zero and existing `IncomeReceived` stays as "other income" history, so the conservation
identity holds without reclassifying past money. `WageStartsDay` is unset at migration and is
set by F6e at its first day boundary (section 3.1), so the migration day keeps the legacy
daily employment posting and accrues no shift wage. No historical wage is recomputed from past
attendance. A migration failure keeps the original payload and refuses the city. Projection
back to the predecessor is byte-identical in tests. New modules start empty, with zero
new spending until the slice that uses them has merged. A binary that cannot execute an F6
capability recorded in the save refuses and preserves the payload (steps B–E capability rule).

**Byte and record budget (ASCII, including field overhead).**

| Location | Bound | Chars/bytes |
| --- | --- | ---: |
| Employment accrual tuple, per person with employment | Attended minutes of the current shift, unposted wage units, 14-day miss mask, day/shift watermark | ≤ 40 / person |
| Owner account extension, per money owner | `EmploymentIncome`, `Discretionary`, visit watermark | ≤ 48 / owner |
| Dated outside-spending totals | 14 rows × fixed categories, exact wide integers (D3a encoding) | ≤ 16,384 / city |
| Change journal | ≤ 1,024 managed shops × ≤ 8 entries × ≤ 40 chars | ≤ 327,680 / city |
| Journal last-observed fingerprint | ≤ 1,024 managed shops × ≤ 32 chars | ≤ 32,768 / city |
| Meal-period cause counters on `FoodShopDay` | 4 periods × 4 causes, fixed width, on the existing 14-day rows: ≤ 14 × 1,024 rows × ≤ 64 chars | ≤ 917,504 / city |
| `WageStartsDay` | One city-level day | ≤ 16 / city |

Records: the per-person/per-owner tuples are fixed-width fields on existing records, so they
add 0 records (the B0 fixed-tuple counting rule). The meal-period counters are fixed-width
fields on existing `FoodShopDay` rows and also add 0 records. The journal is charged itemwise
(≤ 8,192 records), the fingerprints add ≤ 1,024 records, and totals add ≤ 14 records.

**Versus the codec caps.** The E5d CF32 composition budget is 223,716,156 B
([E5d perf](../perf/e5d-name-identity-save.md)), and CF32 must stay within 85 % of the
268,435,456-character cap, which is 228,170,137. That leaves **4,453,981 B** of headroom at
today's saturated 50,000-resident composition. F6's worst case at 50,000 persons with
per-person wallets is 50,000 × 88 + 1,294,352 = **5,694,352 B**, which **does not fit** that
headroom by about 1.24 MB. F6d therefore must not freeze its shape on today's envelope.
Before F6d freezes, its shape must be re-budgeted against the envelope after F5's
consolidation (which removes or remaps the factor-two consumer budgets) and, if merged, F7's
compact representation. If the composed CF32 fixture still exceeds 85 %, F6d reduces its
optional parts first. It first shortens meal-period counters to 7 days and drops the
meal-period "last week" comparison from the panel, then cuts journal entries per shop, then
the miss mask width. It never drops an
exact money field. Above any bound, a save write refuses and keeps the previous save, never
truncating. F6 does not raise the 50,000-citizen, 4,000,000-record or 256 Mi caps; raising
them is F5/F7 work.

**Ordering with F5 and F7.** Numbers are allocated at implementation time, in merge order.

1. **F5's identity schema comes first.** F6d references `PersonId`/`HouseholdId`/`WorkplaceId`
   and cannot precede it.
2. **F7's representation change and F6d are sequential, in either order.** Preferred: F7
   first, because the byte finding above favors F6d landing on F7's compact representation.
   If F6d lands first, F7 migrates F6d's fields exactly. If F7 lands first, F6d forward-merges
   and adds its fields in F7's representation.
3. **No combination.** F5, F6 and F7 have different owners and different freeze
   prerequisites. One version per owner, one format per version. A conditional F6b held
   addition joins F6d, not F5's version.

## 6. Performance

The standing gate applies to every slice: **mod-work p95 < 5 ms**, and every mod-work sample
**≥ 30 ms** is flagged with count, condition and component. Whole-frame and presented-frame
behavior is tracked separately and never certified by the mod-work union. F6 adds no frame
participants. All new work runs in existing logical events, the F3 posting heap or
already-existing accounting-boundary units.

| Work | Expected cost and bound | Measured at |
| --- | --- | --- |
| F6b rival arrival/completion | O(lanes ≤ 8) array scan plus one dictionary lookup. Target ≪ 0.01 ms, 0 B steady state. | 1,914 / 10,000 / 100,000 consumers on the F3 matched replay fixture |
| F6b attach-time reconstruction | O(current-day outcome records ≤ 3 × consumers). Off the frame path inside detached construction, or 1,024-record units if on the main thread. | Same sizes, plus 20,000 rivals |
| F6c/F6g panel | Built only while open, every 3 s, from counters. Hidden: 0 calls and 0 B. Replaces today's O(consumers) scans (expected taken win). | 1 / 64 / 1,024 shops; 1,914 / 10,000 / 100,000 consumers |
| F6e accrual | O(1) per shift end in an existing event. Midnight-crossing settlement in ≤ 32-posting units. | Posting-call p95 must stay at the F3 level (0.0208 / 0.0229 / 0.0111 ms at 1,914 / 10,000 / 100,000) within noise |
| F6f admission gate and debit | O(1) published-index lookup in an existing admission event. | Same sizes |
| F6f ownership index refresh | New bounded native registration reads (`RentedByPlayer`, `businessOwnerRivalId`) for paid non-food destinations at each hourly boundary, ≤ 16 places per unit, ordered under F1 boundary observations, published atomically per unit. Measure native read count, per-unit p95/max, allocation and retained index bytes. | 1 / 1,024 / 20,000 places (place cap) |
| F6g change detection | Fingerprint compare inside existing demand-time supply reads and hours refreshes. No added native reads (asserted by read counters) and no boundary sweep. Measure added cost and allocation per observation. | 1 / 64 / 1,024 shops |
| F6d save | Separate save latency (F7 owns save timing). | E5d fixtures 957 / 5,000 / 50,000 × factor 2/3, CF32/CF64 |

The sizes reuse the audit `StepBudget` fixture
([assets](../reviews/assets/2026-11-03-city-vision-audit/)), F3's matched 1,914/10,000/100,000
fixture and E5d's save fixtures. The 50,000-person rows apply to saves; 100,000 consumers
apply to posting and advancement stress. F6 code allows no per-frame reflection, catalogue
or resident scans, LINQ in hot paths, or steady-state allocations. Debug strings are built on
request only.

**Parked hotspots.** Physical population, closure refresh, optional-stop refresh and save
serialization stay parked. F6 takes none of them. F7 owns save serialization; F5 may own
physical population selection. F6's only presentation-side change is the panel's scan
removal, which is not one of the four.

## 7. In-game checks for the batched rig

Every check is executed by the headless agent rig
([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig),
owned by the Coding Projects bot, read-only here) in one named disposable FT city, using
reusable checkpoints. Existing interfaces, as recorded in the [E5 plan §8](e5-dynamic-products.md#8-fully-rig-automated-batched-in-game-proof),
are: FT `state`/`save`/`load`/`time`/`money`/`console`/`shoot`/`ui`/`walk`; rig-ops
`shop_fixture`, `shop_state`, `shop_log`, `rival` (prices/hours/owner readback), `weather`
and `panel`; virtual mouse and `ui geometry`; and `rig flow run` with disposable fixture copies
and the autosave guard. Those capabilities are built and await RIG-LIVE. Old-schema and large
fixture saves are built in game by the rig with the earlier PC build (RIG-MODSET); the rig
does not support older game builds. PC diagnostics read by the rig (ledger projection, rival
load, wage accrual, journal) are implementation work in the owning slice. Diagnostic
interventions are limited to the named disposable FT city (AGENTS.md fixture rule) and refuse
elsewhere. Every performance window follows the autosave protocol: record the original
setting, verify it is off before each window including after reload, keep saves outside
windows, and restore it during cleanup.

1. **Baseline ledger reconciliation** — rig-automated (existing capability: `rig flow run`,
   FT `state`/`save`/`load`/`time`/`console`, `shop_fixture`, `shop_state`, `shop_log`;
   pending rig capability: RIG-LEDGER independent daily native receipt/order/bank vs PC
   ledger comparator). The rig runs 7 game days on a two-shop fixture. Each day it asserts
   that the extended conservation identity holds for every owner, PC confirmed receipts
   equal native sales units for managed shops, modeled outside totals equal watermarked
   debits, there are zero duplicate receipts and zero `HistoricalClockMisses`, and native
   statements are unchanged by PC reads.
2. **Price intervention, 3 weeks** — rig-automated (existing capability: `shop_state`,
   `shop_log`, FT `time`/`console`; pending rig capability: RIG-SHOPEDIT price edit on an
   existing shop; RIG-CAMPAIGN multi-week unattended arms with per-day metric export;
   RIG-LEDGER). Week
   1 baseline, week 2 at +25 % on one product, week 3 reverted, against a control checkpoint
   copy. It asserts the direction of the purchase and revenue change matches F6a, the
   journal entry and panel delta appear, recovery is reported, and accounting is exact.
3. **Staffing intervention** — rig-automated (existing capability: `shop_state`, `shop_log`;
   pending rig capability: RIG-SHOPEDIT staff removal/shift change; RIG-CAMPAIGN; RIG-LEDGER). Lunch staff
   halved for one week. It asserts that daily "no staffed counter" evaluated-offer totals
   rise, that the F6g lunch meal-period counter rises versus the previous week, that B1 bad
   visits are recorded and served meals do not rise, and that the panel shows these as
   evaluated offers, with no hour or resident attribution.
4. **Hours intervention** — rig-automated (existing capability: `shop_state`, `shop_log`;
   pending rig capability: RIG-SHOPEDIT hours; RIG-CAMPAIGN; RIG-LEDGER). Closing 2 h earlier for one
   week. It asserts closed/late causes, demand moving to rivals or home, journal and
   reacquisition at the native boundary, and no duplicate ending-hour orders.
5. **Shortage intervention** — rig-automated (existing capability: `shop_state`, `shop_log`;
   pending rig capability: RIG-SHOPEDIT set/zero stock; RIG-CAMPAIGN; RIG-LEDGER). Product zeroed for 3
   lunches. It asserts stock causes, memory, journal "stock-out day", and no checkout against
   zero stock.
6. **Employment shock** — rig-automated (existing capability: FT `console`/`state`/`time`;
   pending rig capability: RIG-CAMPAIGN; RIG-LEDGER). The F6e PC console command closes 10 % of logical
   workplaces for one week in the FT city. It asserts no wages for closed shifts, closure days
   rather than misses, a lower income-earned line, higher "unaffordable" causes at own shops,
   recovery after reopening, and conservation.
7. **Access intervention** — rig-automated (existing capability: `shop_fixture` for the
   second-site arm, `shop_state`, `shop_log`; pending rig capability: RIG-ACCESS apply/revert a
   native temporary street closure or shop door closure in the FT city with readback;
   RIG-CAMPAIGN; RIG-LEDGER). It
   asserts longer travel causes, fewer purchases at the far or closed site, workers on blocked
   routes missing shifts (L1) with exact wage loss, and recovery.
8. **Rival service limits** — rig-automated (existing capability: `rival` readback, FT
   `console`/`state`; pending rig capability: RIG-RIVALFP read-only native fingerprint of
   rival registration, prices, hours and finances before/after). A lunch rush next to a
   rival. For calibration, it also records native staffed throughput on representative
   staffed player shops (`shop_state` staffed-counter readback plus PC's live `MealsPerHour`
   diagnostic) as the F6b calibration evidence. It asserts lane and sell-out refusals at the estimated counts, labels reading
   "estimated", memory entries, more own-shop custom when the rival is full, and a
   byte-identical rival native fingerprint (no native rival write).
9. **Attendance and wage across save/reload** — rig-automated (existing capability: FT
   `save`/`load`/`state`/`time`/`console`; pending rig capability: named-resident shift-stage
   checkpoints: before arrival, mid-shift, after a midnight-crossing shift). It asserts
   identical accrual, watermark, money and job state after reload, and no double wage.
10. **Household sustainability soak** — rig-automated (existing capability: `rig flow run`,
    FT `time`/`state`/`console`; pending rig capability: RIG-CAMPAIGN). Three game weeks
    without intervention. It asserts that the arrears share, unfed days and p10 balance stay
    within the F6h control band, that there is no runaway growth or collapse, and that
    conservation holds every day.
11. **Feedback UI** — rig-automated (existing capability: `panel`, `ui geometry`, virtual
    mouse, FT `shoot`, FT `console`). After checks 2–7 it opens Ctrl+F8 and the inspector. It
    asserts that the text equals the PC projection (week deltas, daily evaluated-offer cause
    totals, meal-period counters with "not recorded" windows labeled, first-observed journal,
    estimate labels, money lines), that no rival names, shares or paid-research fields appear,
    and that layout clearance holds. Screenshots are retained. Closed panel: zero build calls.
12. **Cadence and detail independence** — rig-automated (pending rig capability: RIG-CADENCE
    fixed 30/60/120 rendered-FPS arms with scheduler-budget arms, shared with F1's pending
    cadence arms, plus a body-cap 0 vs default arm). Same checkpoint and inputs for one game
    day. It asserts byte-identical economic ledgers across cadence arms and the body-cap
    difference within F2's declared bound (flagged, not passed, before F2).
13. **Migration and refusal** — rig-automated (existing capability: FT `save`/`load`;
    pending rig capability: RIG-MODSET build selection; bounded PC payload fault matrix in
    copied FT checkpoints; RIG-LEDGER). The rig builds the predecessor-schema city in game with the
    earlier PC build, saves it with in-flight purchases, shifts and visits, then loads it with
    the F6d/F6e build. It asserts exact ids and money, no reclassified history, no double
    employment income on the switch-over day, including after a reload across it, and an original
    payload that is refused and preserved for future, unknown or over-bound variants.
14. **Performance batch** — rig-automated (existing capability: `rig flow run` autosave guard,
    FT `state`/`time`; pending rig capability: unified PC mod-work/whole-frame evidence
    assertions). Breakfast, lunch and evening rush windows with all F6 loops, against the
    matched pre-F6 checkpoint. It asserts mod-work p95 < 5 ms and flags every ≥ 30 ms sample
    with its component. Whole-frame is recorded separately. Autosave is verified off before
    every window.

**Rig capability requests for Coding Projects** (short request texts):

- **RIG-LIVE** (existing request) — first live verification of rig-ops, fixtures, flows and
  the autosave guard. Prerequisite for checks 1–14.
- **RIG-SHOPEDIT** (existing request) — edit an existing player shop's price, hours, staff and
  stock with readback. Checks 2–5.
- **RIG-MODSET** (existing request) — per-flow PC build selection with restore. Checks 13–14.
- **RIG-CAMPAIGN** — "Run a multi-week unattended flow: copy a baseline checkpoint per arm,
  apply timestamped interventions via existing ops, advance game time with time-machine or
  6×, export a PC console metric JSON per game day, and fail on lost coverage." Checks 2–7
  and 10.
- **RIG-ACCESS** — "Apply and revert a native temporary street closure or shop door closure
  in the FT city, with readback." Check 7.
- **RIG-RIVALFP** — "Read-only fingerprint of a rival's native registration, prices, hours,
  stock and finance fields, before and after." Check 8.
- **RIG-LEDGER** — "Independent per-day native receipts, completed orders, bank and statement
  reader compared with a PC ledger export." Shared with F1's pending comparator. Checks 1–7
  and 13.
- **RIG-CADENCE** — "Fixed rendered-FPS caps and scheduler-budget arms, plus a body-cap
  override arm, for one matched checkpoint." Shared with F1/F2. Check 12.
- **Shift-stage checkpoints** — "Pause and save at a named resident's shift stage." Check 9.

## 8. Dependencies on the other plans

| Plan | F6 needs (assumed contract) | F6 provides |
| --- | --- | --- |
| **F5 #92** | B1–B6 (section 3.8): `PersonId`, kind, `HouseholdId`, Workplace and employment schedule, one money owner, body independence, determinism. Its identity schema merged before F6d. Workplace assignment that excludes or flags player-owned businesses. | Wage, pooling and visitor-spending policies on F5's entities. Sustainability metrics validating F5's population consistency. The F6a harness accepts an F5 population fixture. |
| **F7 #94** | Save representation and timing. A compact representation preferred before F6d (section 5). Exact migration of F6d fields if F7 lands after. | F6d's bounded per-person/per-owner/city shapes and byte numbers for F7's budget. No save-path work. |
| **F1 #88** | Minute epochs, `IsCaughtUpTo`, boundary observations, no historical sales, cadence fixture and RIG-CADENCE/RIG-LEDGER. | New events inside epochs. Shared ledger comparator use. |
| **F2 #89** | A declared bound on body-induced divergence for arrival. | Economic ledgers as one of F2's divergence measures (check 12). |
| **F3 #90** | 32-unit posting heap ordering, bounded continuations. | No new unbounded unit. Midnight settlement through the existing heap. |
| **F4 #91** | The integrated gate definition. | Per-event F6 costs and rush-window measurements (check 14). |
| C4/C5 research | Stays off. Paid-research boundary. | Market-share computation exists as a diagnostic only, usable later as an entitled report input. |

## 9. Questions for Peter

Player-facing only. None blocks the plan. The recommended default is implemented if unanswered.

| # | Question | Recommended default | If Peter picks the other option |
| --- | --- | --- | --- |
| Q1 | When a resident arrives late for work, are they paid for the minutes worked, or nothing for that shift? | Paid for minutes worked. | Whole-shift-or-nothing: stronger income swings from traffic. F6e changes one rule; the tuple gets smaller. |
| Q2 | How tight should rivals feel? | Moderate: rivals queue and sell out at about the level of a typical comparable player shop (±25 %). | Generous (about 2×): rivals stay a strong fallback; player gains from rival saturation are smaller. Only F6b defaults change. |
| Q3 | Should the free panel show your share of nearby meals, or keep shares in paid research as decided on 31 October? | Keep shares in paid research. The free panel shows own-shop causes, week deltas and city totals. | A free coarse "share of nearby meals" line in F6c. The D3a/31 October rule would need an explicit exception. |
| Q4 | Facts only, or also suggestions such as "add lunch staff"? | Facts only (evaluated-offer causes by day and meal period, change deltas). The player draws the conclusion. | One suggestion line per shop in F6g; easier but shallower. |
| Q5 | Should residents spend money on non-food shops and leisure outside your businesses, competing with their food budget? | Yes, as a labeled outside total. | Leisure stays free, food budgets stay larger, and F6f drops C9. |
| Q6 | Should repeated missed work make job loss more likely? | Yes, mildly. | No link: employment stays random, and traffic affects income only through missed wages. |

## 10. Risks and refusals

**Risks, and how each fails safe:**

- *Rival estimates mis-calibrated* (too tight or too loose). The defaults are config
  profiles measured in F6a/F6h. Labels always say "estimated", and no native claim depends
  on them. A bad profile changes balance, never accounting.
- *Household collapse or runaway wealth after attendance wages.* F6h control-band thresholds
  catch it offline before merge. Support after job loss is unchanged. Reserve rules are kept.
- *Derived rival load disagrees after reload.* Exact reconstruction tests. On any
  inconsistency, the rival is treated as unavailable for that period (refusal, no debit), not
  as unlimited.
- *Byte budget overrun* (section 5). F6d does not freeze until the composed fixture fits.
  Writes above a bound refuse and keep the previous save.
- *F5 differs from B1–B6.* Section 3.8 defines the fallback per baseline. F6d/F6e/F6f wait;
  F6a–F6c are unaffected.
- *Change-journal noise from native refreshes.* Entries come only from first-observed
  differences between demand-time supply reads or hours refreshes and the shop's saved
  last-observed fingerprint. Unchanged reads add nothing, which is tested.
- *Rig capability gaps.* Checks stay pending under named capabilities. Offline results are
  reported separately and never as native proof.
- *Performance regressions.* Per-slice measurements; every ≥ 30 ms sample is flagged. A slice
  that breaks the gate does not merge.

**Refusals. F6 will not:**

- replay historical purchases, back-date wages, or recompute past income from past attendance;
- write native rival finances, stock, hours or staff, or claim inaccessible native rival stock;
- credit or debit native payroll, rent, wholesale or business accounts from PC wallets;
- book a modeled exchange at any player-owned place (C7/C10);
- forgive, skip or late-post a wage or a visit to pay down backlog (F1);
- use a stale background snapshot for economic state or saving (F7);
- show free named-rival figures, market shares or city-wide trait distributions;
- add a hardware- or detail-dependent fidelity tier;
- modify RLO or True City Interiors, or report F6 status in any terms other than
  "implemented" and, later, "verified by rig".

## Verification boundary of this planning change

Read AGENTS.md, CHECKS.md, README.md, `docs/roadmap.md`, `docs/design.md`, the steps B–E
plan, the E5 plan, the F1 plan, the F3 checks, the E5d perf record, issues #93/#88/#90 and the
merged audit. Inspected at `d6b534a` the source members cited in sections 2–5. No native API
is introduced beyond members already read by existing adapters; F6f's new ownership index
reads the existing `RentedByPlayer`/`businessOwnerRivalId` registration members at new call sites,
and F6g adds no native read. No code, test, tool, config,
schema, CHECKS, README or roadmap change. No build, `dotnet test`, `dotnet run`, game, rig,
lease, profile, ModsLocal or bridge access. Validation for this file is the Git diff checks
requested by the task.
