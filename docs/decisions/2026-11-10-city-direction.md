# City direction, 10 November 2026

Peter's answers to eight design questions on 10 November 2026, and a ninth decision later that day, asked after the [9 November review](../reviews/2026-11-09-city-review.md). The review found that the model and the density policy limit how real the city feels, and that the technology does not. These decisions set the direction for the remaining work. The [roadmap](../roadmap.md) carries the slices.

## Peter's decisions

1. **Everyone on the street is a named person.** The population grows by housing renters in homes the player cannot rent. Peter: "We can expand the population by having renters in non-player rentable houses. But we should still make sure that every person you meet can be inspected you can see they live/work somewhere." No anonymous pedestrians, as on 31 October.
2. **Through-traffic cars are the one exception.** Peter: "I think cars we can make the exception and have some cars just spawn to cross the city (someone travelling through the city)."
3. **Daily life in the first release** has three changes. Work follows the workplace's opening hours. Evenings and weekends differ from weekdays, and days off are not the same for everyone. People who share a home sometimes go out together.
4. **Residents buy at every kind of player business in the first release.** Peter: "All businesses need to be supported somehow, lets think about the easiest implementation for all that doesnt require per store individualization (also a fallback for modded stores)." This replaces "food-first products" from 31 October.
5. **Non-food buying has two causes.** Recurring needs that come due on their own rhythm, and impulse buys while visiting or passing.
6. **Wallets stay individual.** On a shared outing each person pays for themselves.
7. **City work goes first.** Slices already in flight finish. Then cleanup, saves and the city work. Food slices not yet started wait for the new day model so they are built once.
8. **The hands-on session stays last**, before the single release. Feature work does not pause for it.
9. **Visitors are the balance lever.** Added later the same day. Peter: "We should have the visitor mechanic allow us to finetune balance a bit more (e.g. we can add just more, or some with specific preferences to increase a specific's shop types customers)". Commuters and day visitors stay named people who arrive by subway.

## What this changes in older decisions

- [30 October to 2 November](2026-10-30-roadmap-decisions.md): "food-first products" becomes all player businesses. "No anonymous crowds" stays for people and gains the car exception. "Later: residents as employees, households" moves into the first release as shared outings.
- [24 October scope](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/archive/implementation-plan.md): "domestic simulation" stays out. Households share outings and nothing else.
- AGENTS.md keeps its current ownership rules ("unsupported businesses stay native-owned", "no economic effect unless the saved city owns a food scope") until the generic sale path below is proven in game. The plan for that track proposes the new wording.

## Technical direction

The coordinator's proposals, revised on 10 November after an approach review by Claude Opus 5.5, gpt-6.1-sol and gpt-6-astra. All three judged the direction sound with changes; the last section lists what they changed. The joint plan refines these proposals, and any of them can change without a new owner decision.

### Order

- Prove a sale first. Repair the rig's serving fixture and read the game's customer code before the city is built around it. This runs beside the cleanup and save work.
- Build one whole loop at today's population before growing it: a resident earns, plans a visit, travels, buys, pays and survives a reload.
- Grow the population after that loop is measured, so the save and frame work is sized against the real workload.
- Days, needs and sales get one joint plan. A need coming due is a plan item, a shared outing is a plan item, and the wallet decides whether either happens.

### Population

- House people in residential buildings the player cannot rent. Those buildings expose no interior area, so the plan needs a second capacity rule, for example floor area from the building footprint and floor count. The rule that whole-property area never stands in for a rentable interior stays for rentable homes.
- Size the population from consistency between homes, jobs, shops and trips, and from what the save and the frame can carry. The plan sets the number after the save rewrite and a Mono GC measurement on the rig.
- Commuters and day visitors stay as built: named people with traits and a wallet who live outside the city and arrive through the subway stations. Day plans, needs and the purchase contract apply to them as to residents. Households do not, because a visitor has no home in the city.

### Visitors as the balance lever

- Today the visitor count follows fixed constants in `Core/PopulationCensus.cs`: at most two visitors per resident, a day-visitor pool of about 15 percent of residents, a 25 percent chance of a visit per day. Those become a tuning table in the mod's balance settings.
- The table has two controls. How many visitors come, by kind and by day of the week. And visitor groups: a named group has a share of the visitors and its own leaning toward business types, for example a group that favours clothes shops or nightlife.
- A group changes who comes, never what a shop is paid. Every visitor in a group is still one named person with one wallet who makes their own choices, so the inspector stays truthful and a shop still has to win the visit on price, distance and memory.
- Groups lean toward business types from the loaded catalogue, so a modded shop type can be given customers the same way.
- This is the first tool for the revenue-scale question below, ahead of any change to what one visit is worth.
- The table is for balancing the mod. Whether the player ever sees or changes it is not decided.

### Days

- Replace the fixed template in `Core/DailyRoutine.cs` with a plan per person per day: an ordered list of intended activities with a venue, a time window, a duration and a priority.
- A plan depends on the person's wallet, needs and the current catalogue, so a seed alone cannot replay it. Each day's plan is generated once, at the start of the day, with seeded draws, and saved. A reload continues the saved plan.
- One executor runs plans. Today commute preparation, meal outings, waits for opening and optional stops each keep their own scheduling state in `CitySimulator`. The executor takes those over and handles late arrivals, closed venues, missed items and a household member who does not turn up.
- Work windows come from the workplace's real weekly hours and a shift type. Meals become plan items instead of interrupts.
- Days off vary by workplace and person. The shared three-day weekend goes.
- A household is the people in one home. It gets one kind of shared plan item, an optional outing with a clear rule for cancelling. No shared budget, no relationships beyond living together.

### Through-traffic cars

- A through car enters at one map edge and leaves at another. It occupies road capacity in the traffic kernel like a resident's car, so queues stay honest.
- It has no driver identity, wallet or destination in the city. The inspector says it is passing through.
- Its share of traffic is a setting with a low default. It never parks and nobody gets out.

### One sale path for every business

What the code shows today. `CustomerEntriesHelper` creates hourly customer entries for a player business, and each entry already carries its order. The business simulator then processes the entry: price acceptance, stock, a completed order. The food code stops the native entries for a shop it owns, builds its own entry with a one-product order (`Runtime/NativeFoodMarket.cs:588`), and runs it through a `FullServiceBusinessSimulator`. It refuses businesses with an entrance fee. So food proves one processing family with an order the mod wrote. It does not prove that the game will choose a basket for a resident, or that services and entrance-fee venues use the same processing.

The proposal is one purchase contract for every business, with as little per-type code as the game allows.

- **The resident decides whether and where.** A need comes due or an impulse fires. Budget, distance, memory of past visits, price level and opening hours pick the business.
- **One purchase contract.** A purchase carries the resident, a spending ceiling and the intention of the visit, and returns a confirmed result: what was bought, what was paid, or why not. The ceiling is checked before anything irreversible happens. Debiting whatever the game booked afterwards is too late when the basket costs more than the wallet holds.
- **Where the order comes from.** First candidate: keep the game's own entry generation and let an arriving resident take one of that hour's native entries, so the game keeps order choice, stock and booking for every type, modded ones included. The plan checks this against the game assembly. Where it does not hold, a small adapter per processing family fills the gap, chosen by what the business can do and never by shop name.
- **One checkout.** Food rides the same contract and adds only its product choice from tastes and menus. A second checkout beside the food one would double the ownership, receipt and refusal code.
- **A small need model.** A purchase has an effect: it satisfies a need for a while, the resident can defer, and the visit is remembered. Categories come from the loaded catalogue, modded ones included. A category nobody has described gets a neutral default. That is the fallback for modded shops, with the honest limit that catalogue presence cannot guarantee a modded shop's own code works with the contract.
- **Native customer counts calibrate, they do not drive.** The plan first establishes what those counts already include, such as price, marketing or location, so the resident's choice does not apply the same effect twice. Household essentials already deducted from wallets are re-cut so a need is not paid for twice.
- **Businesses without walk-in customers**, such as offices, factories and warehouses, are supported through their workforce. Residents work there on the venue's hours. Their contracts and production stay native.
- **Rivals** stay read-only. Spending at rival non-food shops stays a modeled outflow, as today.

Prerequisites shared with the day model, built before day plans: a business type on every place, a place catalogue that refreshes when businesses open or close, and the wallet moved from the food roster to the person.

### Saves

- Start with a direct single-pass writer inside the existing save callback, and measure capture, validation and encoding separately.
- Resident wallets and native sales must describe the same moment. Encoding on a worker or into a sidecar file comes only with a mechanism that keeps one save consistent and recovers from a crash between the two writes.

### Open question for Peter: revenue scale

The game tunes rent, wages and prices to its own customer volume. The 9 November review estimates about three resident visits per shop per day in today's city. Nobody has measured the native volume, and a population of 8,000 to 15,000 may still fall short of it. The first answer is decision 9: more visitors, and visitor groups that lean toward the shop types that fall short. If that is not enough, either the player's revenue drops, or each visit has to count for more than one customer in the books, or prices and spending per visit rise. The joint plan measures the gap in game, shows how far visitors close it, and brings what is left to Peter with numbers.

### Not yet proven

- Where native orders are generated, and whether one native processing path serves every customer-facing business type, including services, gyms, nightclubs, cinemas and theatres.
- That a native entry can be bound to the resident who takes it, and that a spending ceiling can be enforced before the sale commits.
- What the game's own customer counts already include.
- The rig cannot make a sale today, with or without the mod ([9 November rig results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170)). The serving fixture has to work before any sale path counts as verified. The proof is one food sale, one goods sale and one service sale, including a refusal and a reload.
- The hands-on session stays last. Its cost: density, day plans and bodies are tuned without the owner seeing them. A recorded rig walk-through at morning, midday and night, once day plans land, is the stand-in.

### Coordinator defaults Peter can overrule

- Clicking a through car shows "Passing through the city" and nothing else.
- Indoor customers and staff inside a shop stay native bodies for now. Whether they can be shown as the residents they stand for is a question for the plan, because the interior view experiments found no safe renderer.
- Children do not exist in the population. Residents are 18 to 85, so a household is adults who share a home.

## Approach review, 10 November 2026

Three reviewers read the first version of this record, the review and the roadmap, with the brief to judge the approach and ignore detail: Claude Opus 5.5, gpt-6.1-sol and gpt-6-astra. All three said "sound with changes", agreed with no full rewrite and no change of stack, and none disputed an owner decision.

What changed because of them:

- The first version said the game's customer processing picks what is sold. All three checked `Runtime/NativeFoodMarket.cs` and found the mod writes the order itself. The sale path is now a purchase contract with a spending ceiling checked first, and the native-entry route is a candidate to verify.
- Proving a sale moved to the front, beside cleanup and saves (all three).
- Business types and the wallet on the person come before day plans, and days, needs and sales share one plan (all three).
- Day plans are saved when generated and run by one executor (Opus 5.5, gpt-6-astra).
- Needs have effects and native counts only calibrate (gpt-6.1-sol, gpt-6-astra). The revenue-scale question is written down (Opus 5.5).
- The save starts as a direct writer inside the save callback; anything asynchronous needs a consistency mechanism (gpt-6.1-sol, gpt-6-astra).
- Food and everything else share one checkout (Opus 5.5).
- The population grows after one whole loop works at today's size (gpt-6.1-sol, gpt-6-astra).
- The repository-wide rename is cut; a subsystem is renamed when its track replaces it (all three).
- Fingerprints, the native-evidence ledger and the cross-machine acceptance record are deferred as guarding nobody sees (Opus 5.5).
