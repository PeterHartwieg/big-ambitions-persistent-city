# Realism and gameplay roadmap, 27 October 2026

A proposal from a read-only pass over the current source (schema 13, 0.10.0 candidate), the reviews up to 27 October and the sibling projects that already read native marketing and rival state. It does not change the required release scope in [the bounded plan](../implementation-plan.md), approve a release, or authorize deployment to an ordinary save. No game was launched and no rig was touched. Facts about native fields taken from `big-copilot` were read against build 3682 and need rechecking against the referenced assemblies before any adapter is written.

## Where the simulation stands

Residents have stable identities, homes, workplaces, one shop and one leisure venue, quiet hours, weekends, shift offsets and staggered commute preparation. They walk, ride the subway or drive their own persistent car, and cars queue on real road sections citywide. Each physical resident anchors two represented consumers. Every consumer has a wallet with declared income, housing, essentials and a paid home-food basket, and three meal opportunities a day chosen by price, round-trip time, a single preferred product, service quality, district appeal and meal fit, against the home-food alternative. Eligible player food businesses are owned whole: native customer generation stops, residents buy through the native checkout, and the game keeps stock, staff, suppliers and accounts.

What the player can decide today: where to rent, what to charge, when to open, whom to staff, and which of three products to sell. What the player sees: the Ctrl+F8 text panel with purchases by meal and product, rejection counts by reason, buyer districts, purchase hours and the native operating result.

Four gates stay open: the performance target (the Mac sample fails at 31.63 ms p95; the PC sample passes 5 ms but with whole-frame stalls up to 329 ms and a locked console), money conservation (a 0.28-cent-per-sale residual from the native fractional price $10.2528 against integer-cent payment), the unfinished price comparison, and Peter's hands-on session. Startup and reload waits also miss their frozen ceilings.

## What is missing, in the player's terms

| Lever the game offers | What happens at an owned shop today | What should happen |
| --- | --- | --- |
| Marketing campaigns | Nothing. The choice function reads price, hours, stock, staff, service score and district demand; no campaign or promotion field. Whole-business ownership silenced the native generator that used them. | Campaigns widen which residents know the shop exists. |
| Consistency over days | Nothing. Every meal is a fresh evaluation of the current state. A stockout costs that day's sales only. | Regulars form; a stockout or a closed door costs future visits; a new shop ramps up over days. |
| Competing with rivals | Rivals are absent from the choice set. A resident chooses between the player's shops and eating at home. | Rival shops on the same street are alternatives, read-only, with their price, hours and service. |
| Choosing a location | No information before renting. The catchment appears only after sales. | A survey of who lives, works and passes within reach, by meal, before signing. |
| Hours and staffing | Aggregate rejection counts per day. | Lost willing customers by hour and reason. |
| Menu | Three products, one preferred product per consumer. Within-shop choice is nearly all-or-nothing (23 October menu review). | More verified products and individual taste vectors. |
| Weather and season | None. Rain changes nothing about outings or meals. | Rain thins outings and shortens walks. |
| Knowing the people | Residents are visible bodies without a story. | Click a resident: who they are, where they go, what they ate, why not here. |

The first row is a regression against vanilla, not a missing feature. A player who buys a campaign for a managed café gets nothing for the money.

## Principles for every increment

- A lever, a visible consequence and a readable reason. Never a guaranteed outcome, protected chain or minimum sale.
- Deterministic from saved state and stable hashing. The same day replays identically across reloads, camera moves and event budgets.
- No new route searches per decision. New per-consumer state has a fixed maximum size, stated in the schema note.
- Native accounting stays authoritative. Nothing writes rival finances, marketing state or stock. Unknown native evidence stays unknown and never defaults to the favourable value.
- One Core change with invariant tests, one matched native day on a disposable city, a dated review, then stop. No tuning campaign to produce a prescribed winner.

## Phase 0: close the bounded release

Not new scope. Listed because each later phase adds per-decision work and none of it should land on a candidate that already fails the frame gate.

1. **Commit the development baseline.** The working tree carries 80 modified and about 30 untracked files since the 23 October snapshot, including the whole food economy. Commit before any new work so a phase can be reverted on its own.
2. **Decide the cent residual.** The native price is a float; residents pay integer cents. Three options: declare the invariant as revenue within half a cent per sale and report the residual per day; charge residents the float price kept in tenths of a cent; or have the checkout approve a cent-rounded price. The first changes a verifier definition only. The second changes every wallet. The third writes a price into a native call and needs a new native proof. Recommendation: the first, recorded as a product decision, because the game itself prices in fractions.
3. **Take the first three items of the [architecture review](2026-10-27-architecture-performance-review.md)**: resumable budgeted route planning parked in `WaitingForRoute`, frame-stamped physical ownership checks, and the supply snapshot cache with batched midnight budget postings. Repeat the matched rendered window after each. The advancement maximum should fall to the budget plus one NavMesh query.
4. **Run the price branch** on the preserved Day 5 checkpoint, then the hands-on session. The session also decides whether one-third pace reads as a day or as waiting.

## Phase 1: make the people legible

Read-only over existing state. It touches no decision, so it can be built on a branch while Phase 0 runs, as long as it is not merged into the release candidate before the gate passes.

### 1a. Resident inspector

Pick a visible resident (a key toggles inspect mode; click the nearest leased body) or search by name. Core builds a `ResidentInsight` on demand: name, age, persona, home address and district, workplace or study place, car model and paint, current phase and destination, the next routine slot from `DailyRoutine.At`, the last visits already kept in `RecentVisits`, and for each consumer slot the current meal and a new ring of the last three meal outcomes: where, what, price, or the reason it did not happen in the same words the shop panel uses. Wallet shown as a band (tight, steady, comfortable), not cents, so the display does not claim precision the budget policy does not have.

New state: three outcome records per consumer, 1,914 consumers, schema 14. Tests: the capture is pure and allocation-bounded, reasons match the `FoodShopDay` classification, the ring never exceeds three, reload preserves it. Native check: follow one walker, leave, meet them later and read the same story.

This is the cheapest way to deliver the product promise. "Looking away and back does not replace the city" is only felt when the player can recognise someone.

### 1b. Lost demand by hour and reason

`FoodShopDay` gains hour arrays for the closed, unstaffed, queue and stock rejections. Closed and late use the consumer's desired meal minute; queue and stock use the arrival hour. The panel prints "Willing customers not served: 07:00 closed 14, 12:00 queue 9, 19:00 closed 22." Tests: the hour arrays sum to the existing counters; the 14-day retention is unchanged. Lever: hours and staffing. Consequence: visible the next day.

### 1c. Catchment survey before renting

For a selected rentable or owned commercial building, Core counts consumers whose home, workplace, study place or leisure venue lies within walking reach, grouped by meal window (breakfast near home, lunch near work and study, dinner near home and leisure) and by income band, and lists managed shops already inside that reach. Reach uses `IWalkingTimeBound` or straight-line distance with a stated factor; no route search. Represented consumers are counted with their label. Output reads "At lunch, 212 consumers are usually within a ten-minute walk; 61 of them are already within reach of Fourth Avenue 20."

After opening, the same panel shows the survey beside the observed result: "survey 212 at lunch; served 18 per hour at peak; 94 preferred home food; 40 ran out of time." Predict, observe, understand is the fun gate in one screen.

Selection needs a read-only hook on the property UI; the fallback is a list of the player's rented empty properties in the panel. Tests: each consumer is counted once per window, results are deterministic, one pass over citizens per request cached per building and game hour.

## Phase 2: memory, regulars and disappointment

Each consumer keeps at most four `ShopMemory` entries: business, last visit day, served count, last outcome (served, queue, stock, closed, unstaffed, price rise) and a signed expectation in cents that decays toward zero by a fixed amount per day. The expectation enters the choice score beside the service-quality adjustment. A served visit nudges it up; after a few served visits a habit bonus applies, stronger for Commuters and weaker for Foodies. A stockout, a closed door or an unstaffed counter sets a penalty that halves daily. A consumer who walked to a closed shop also remembers the hour and skips that hour until the memory fades. Variety: the same product on three consecutive days adds a small penalty, strongest for Foodies.

A second accumulator per managed business, per district, replaces the legacy district demand signal once the business has seven days of its own history. The 22 October quality review already asked for this: the mod's own served and unmet outcomes should inform local appeal instead of the native provider formula.

Consequences the player can see: a new shop ramps up instead of capturing demand on day one, which also keeps the Phase 1 survey honest; a stockout costs more than the day's sales; the panel shows "regulars 34, lost this week 5, most after Tuesday's stockout."

Tests: decay is monotone and bounded; no money is created or moved by memory; results are independent of event budget; entries never exceed four; a city with memory policy version 0 reproduces the pre-memory choices exactly. Native comparison: the existing fixture with a two-day induced stockout, observing recovery on days three to six. Cost: four comparisons per candidate, no route search.

## Phase 3: awareness and marketing

Today a consumer considers every managed shop within walking tolerance of its origin, up to eight by distance. Replace that with a known-shop set per consumer, maximum eight: shops within reach of home or workplace, shops passed within about 60 metres on a committed route leg (computed once at commitment, not per frame), shops in memory, and shops reached by a campaign. The native registration carries `marketingCampaigns[]` with `marketingTypeName` and `enabled`, and a `promotion` object with `trafficIndex`, `marketing` and `total` (field names from `big-copilot`, build 3682; verify). A campaign adds awareness among consumers by home district for its duration, decaying afterwards. Unknown marketing state means no awareness, never full awareness.

This restores the lever the current build silently removed. The panel attributes purchases to how the consumer knew the shop: neighbourhood, commute, regular, campaign. Tests: awareness alone never creates a visit or moves money; the set is bounded; unknown state yields the empty set; a consumer with no campaign exposure reproduces Phase 2 behaviour. The reads live in Runtime behind an interface with the same binding discipline as the RLO adapters.

## Phase 4: rivals as alternatives

Rival shops of supported food types are readable: `businessOwnerRivalId` on the registration, `retailPrices` per product, hours and customer service. They enter the candidate list as read-only offers labelled "stock unknown, assumed available." A consumer who chooses a rival spends from its wallet as a declared outflow to the outside economy; nothing native is written, consumed or booked. Special-rival price defences in `specialRivalStates[].defenseStates` are read so the panel can warn "a price war on hot dogs meets a cut," never triggered by the mod.

Consequence: the player's café competes with the street it stands on. The panel reports "41 nearby prospects chose Burger Barn: cheaper by $1.50 (26), closer (11), better service (4)." Lever: price, service, hours, location. Tests: zero native writes under every path; wallet conservation with the outflow line; a rival without a readable price is excluded with a reason; determinism. This needs a scope decision: AGENTS.md forbids writing rival finances and says nothing about reading rivals as alternatives.

## Phase 5: weather and the street

Rain, read in Runtime the way the dwell planner already reads it, lowers the chance that an optional outing starts, scales walking tolerance to about 0.7, makes dinner outings prefer the nearest feasible shop, and shortens outdoor queue leads. The sample taken at decision time is persisted with the decision, so a reload does not re-roll it. This also answers open issue 4, the crowd that no longer thins in rain, from the simulation side. Tests: rain only reduces, never adds; no money or visits are created.

In the same phase, verify stationary citizens step one in the game (queues at opening, built on 21 October and unchecked), then add the next two dwell kinds from [the proposal](../stationary-citizens.md): staff breaks beside the entrance and small groups outside bars in the evening. These are the cheapest visible realism per line of code once the planner is confirmed on the rig.

## Phase 6: wider menus and individual tastes

Verify the next stocked food products against the checkout contract one at a time, as 0.9.0 did for burgers and hot dogs. The native catalogue has coffee, salad, ice cream, soda and water among the retail items; which of them pass the native equipment rules is an empirical question per product. Replace the single preferred product with a taste vector in cents per product, persona-shaped and stable per consumer, and give meal-fit penalties a personal spread. The 23 October menu review named uniform tastes as the main realism gap in product choice. Consequence: menu composition becomes a decision, and a café and a fast-food shop differ by resident, not only by meal.

## Later, and why not now

- **Residents as the player's employees and payroll.** Needs the one-population identity that design.md defers (Flare, social cast, employees, passengers under one id). Worth it, but it is the largest identity change in the project and belongs after the loop above works.
- **Households and shared accounts.** The budget policy deliberately treats co-residence as no family. Pooling changes affordability citywide and would reorder every comparison done so far.
- **K=3, a general detour planner, rival finances, a Rust rewrite.** The scope agreement excluded them for reasons that still hold.

## Sequence and size

| Phase | Core change | Schema | New native reads | Native comparison | Size |
| --- | --- | --- | --- | --- | --- |
| 0 | Planner resumption, physical stamps, supply cache | none | none | matched frame window, price branch | M |
| 1 | Insight captures, hour arrays, catchment query | 14 | property selection hook (optional) | one walker, one survey versus observed day | S to M |
| 2 | Shop memory, business reputation | 15 | none | stockout recovery, days 3 to 6 | M |
| 3 | Known-shop sets, campaign awareness | 16 | marketing campaigns, promotion | one campaign on, one off | M |
| 4 | Rival offers, outflow line | 17 | rival registrations, prices, defences | one rival next door, two prices | M |
| 5 | Rain terms, dwell kinds two and three | 18 | weather | one rainy day, one dry | S |
| 6 | Taste vectors, product penalties per person | 19 | checkout contract per product | one product at a time | M per product |

Phases 2 and 3 are independent; 2 first because it needs no new native reads. Phase 4 depends on 3, since a rival the consumer has never heard of should not be an option. Phase 5 is independent of all of them. Phase 6 follows 2, because taste variation is only interpretable once regulars exist.

## Decisions for Peter

1. The cent residual: redefine the invariant with a reported residual, or change how residents pay.
2. Whether Phase 1 may be built on a branch before the performance gate passes, kept out of the release candidate.
3. Whether rivals may enter the choice set as read-only alternatives with a declared outflow.
4. Phase order after the release: 2 then 3 as proposed, or awareness first because it repairs the marketing regression.

## Evidence boundaries

Nothing here was run in the game or measured. Native field names for marketing and rivals come from another project's reading of build 3682 and must be confirmed against the assemblies this mod references. Sizes are judgements from the source, not estimates with a track record. Planning agreement, if given, approves direction and order, not calibration, performance, fun or publication.
