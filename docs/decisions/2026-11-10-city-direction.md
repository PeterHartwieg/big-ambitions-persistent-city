# City direction, 10 November 2026

Peter's answers to eight design questions on 10 November 2026, asked after the [9 November review](../reviews/2026-11-09-city-review.md). The review found that the model and the density policy limit how real the city feels, and that the technology does not. These decisions set the direction for the remaining work. The [roadmap](../roadmap.md) carries the slices.

## Peter's decisions

1. **Everyone on the street is a named person.** The population grows by housing renters in homes the player cannot rent. Peter: "We can expand the population by having renters in non-player rentable houses. But we should still make sure that every person you meet can be inspected you can see they live/work somewhere." No anonymous pedestrians, as on 31 October.
2. **Through-traffic cars are the one exception.** Peter: "I think cars we can make the exception and have some cars just spawn to cross the city (someone travelling through the city)."
3. **Daily life in the first release** has three changes. Work follows the workplace's opening hours. Evenings and weekends differ from weekdays, and days off are not the same for everyone. People who share a home sometimes go out together.
4. **Residents buy at every kind of player business in the first release.** Peter: "All businesses need to be supported somehow, lets think about the easiest implementation for all that doesnt require per store individualization (also a fallback for modded stores)." This replaces "food-first products" from 31 October.
5. **Non-food buying has two causes.** Recurring needs that come due on their own rhythm, and impulse buys while visiting or passing.
6. **Wallets stay individual.** On a shared outing each person pays for themselves.
7. **City work goes first.** Slices already in flight finish. Then cleanup, saves and the city work. Food slices not yet started wait for the new day model so they are built once.
8. **The hands-on session stays last**, before the single release. Feature work does not pause for it.

## What this changes in older decisions

- [30 October to 2 November](2026-10-30-roadmap-decisions.md): "food-first products" becomes all player businesses. "No anonymous crowds" stays for people and gains the car exception. "Later: residents as employees, households" moves into the first release as shared outings.
- [24 October scope](../archive/implementation-plan.md): "domestic simulation" stays out. Households share outings and nothing else.
- AGENTS.md keeps its current ownership rules ("unsupported businesses stay native-owned", "no economic effect unless the saved city owns a food scope") until the generic sale path below is proven in game. The plan for that track proposes the new wording.

## Technical direction

The coordinator's proposals. Each track's plan refines them, and any of them can change without a new owner decision.

### Population

- House people in residential buildings the player cannot rent. Those buildings expose no interior area, so the plan needs a second capacity rule, for example floor area from the building footprint and floor count. The rule that whole-property area never stands in for a rentable interior stays for rentable homes.
- Size the population from consistency between homes, jobs, shops and trips, and from what the save and the frame can carry. The plan sets the number after the save rewrite and a Mono GC measurement on the rig.
- Visitors and commuters stay as built.

### Days

- Replace the fixed template in `Core/DailyRoutine.cs` with a plan per person per day: an ordered list of activities with a venue, a time window, a duration and a priority. The plan is seeded from city seed, person and day, so a reload replays the same day.
- Work windows come from the workplace's real weekly hours and a shift type. Meals become plan items instead of interrupts.
- Days off vary by workplace and person. The shared three-day weekend goes.
- A household is the people in one home. Shared plan items give them joint meals, shopping and leisure. No shared budget, no relationships beyond living together.

### Through-traffic cars

- A through car enters at one map edge and leaves at another. It occupies road capacity in the traffic kernel like a resident's car, so queues stay honest.
- It has no driver identity, wallet or destination in the city. The inspector says it is passing through.
- Its share of traffic is a setting with a low default. It never parks and nobody gets out.

### One sale path for every business

The game already has one customer pipeline for player businesses. `CustomerEntriesHelper` creates hourly customer entries, the business simulator processes each one, picks what is sold, checks price acceptance and stock, and books a completed order. The food code already stops the native entries for a shop it owns and calls the native `ProcessCustomer` when a resident buys.

The proposal is to use that pipeline for everything else, with a smaller contract than food has.

- **The resident decides whether and where.** A need comes due or an impulse fires. Budget, distance, memory of past visits, price level and opening hours pick the business.
- **The game decides what is sold.** At arrival the mod runs the native customer processing for that resident. The native code picks the product or service, checks stock and capacity, and books the order. The resident's price tolerance answers the native price check, as it does for food.
- **The resident pays what the game booked.** The wallet is debited by the order amount. Stockouts, refusals and queues go into memory and the feedback panel.
- **No table per shop type.** Need categories come from the business types and products in the loaded catalogue, which includes modded ones. The base rate for a category comes from the number of customers the game itself would have generated for that type, scaled to the population. A type nobody has described still gets customers at that base rate. That is the fallback for modded shops.
- **Food keeps its detailed path** with tastes per product and menus. A food business the detailed path cannot support falls back to the generic path instead of staying native.
- **Businesses without walk-in customers**, such as offices, factories and warehouses, are supported through their workforce. Residents work there on the venue's hours. Their contracts and production stay native.
- **Rivals** stay read-only. Spending at rival non-food shops stays a modeled outflow, as today.

Prerequisites this track shares with the day model: a business type on every place, a place catalogue that refreshes when businesses open or close, and the wallet moved from the food roster to the person.

### Not yet proven

- That one native customer path serves every customer-facing business type, including services, gyms, nightclubs, cinemas and theatres. The plan confirms this by reading the game assembly, then proves one non-food sale in game.
- That a completed native order can be tied back to the resident who caused it.
- That the game's own entry counts are a usable base rate per business type.
- The rig cannot make a sale today, with or without the mod ([9 November rig results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170)). The serving fixture has to work before any sale path, food or generic, counts as verified.

### Coordinator defaults Peter can overrule

- Clicking a through car shows "Passing through the city" and nothing else.
- Indoor customers and staff inside a shop stay native bodies for now. Whether they can be shown as the residents they stand for is a question for the plan, because the interior view experiments found no safe renderer.
- Children do not exist in the population. Residents are 18 to 85, so a household is adults who share a home.
