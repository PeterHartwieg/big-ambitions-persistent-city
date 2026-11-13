# Decisions in force

This is the one register of owner decisions that still bind Persistent City. Each entry carries its date. Binding agent rules are in [AGENTS.md](../../AGENTS.md); the food price weight is in [its own record](2026-11-08-price-weight.md); optional stops are in [stationary citizens](../stationary-citizens.md); the RLO contract is in [rlo-contract](../rlo-contract.md). The source records were removed on 13 November 2026 and are readable at [a3a6c8d](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/decisions). Deleting a record does not revive an approach it rejected.

## City direction

Peter answered these on 10 November 2026 after the [9 November review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/reviews/2026-11-09-city-review.md). They replace "food-first products" and "later: households" from 30 October to 2 November.

1. **Everyone on the street is a named person** who can be inspected and lives and works somewhere. The population grows by housing renters in homes the player cannot rent.
2. **Through-traffic cars are the one exception.** They cross the city with no driver identity, wallet or destination.
3. **Daily life in the first release:** work follows the workplace's hours, evenings and weekends differ, days off differ between people, and people who share a home sometimes go out together. Households share outings and nothing else; domestic simulation stays out.
4. **Residents buy at every kind of player business**, with no per-store individualization and a neutral fallback for modded stores.
5. **Non-food buying has two causes:** recurring needs and impulse buys.
6. **Wallets stay individual.** On a shared outing each person pays for themselves.
7. **City work goes first.**
8. **The hands-on session stays last**, before the single release. Feature work does not pause for it.
9. **Visitors are the first balance lever:** more visitors, or visitor groups that lean toward a shop type. A group changes who comes, never what a shop is paid.

The technical plan that followed (one purchase contract with a spending ceiling checked first, saved day plans, one checkout) was the coordinator's proposal, not an owner decision. It can change without Peter. No revenue multiplier, bigger baskets or population target is approved.

### Open: revenue scale

The game tunes rent, wages and prices to its own customer volume, and the resident volume may fall short. Decision 9 is the first answer. If visitors do not close the gap, the choices are lower player revenue, one visit counting as more than one customer, or higher spending per visit. Peter decides that with measured numbers.

## Scope and product, 30 October to 6 November 2026

- **Vision** (30 October): a living city that also drives the business, played as a deep optimization game.
- **Choice is probabilistic** and seeded per save, so a reload replays the same day. Distance raises the chance for nearby shops and never rules out distant ones.
- **Traits are shown in words.** City-wide statistics are sold as paid research through the marketing agencies.
- **Memory** follows the loyalty or curiosity trait and stays bounded, about four shops per person.
- **Marketing** is awareness plus targeting; awareness makes a visit possible and traits decide.
- **Rivals** are real alternatives and read-only, with a modeled outflow. Nothing native is written to them.
- **One release, at the end** (30 October). The hands-on session comes last.
- **Research products** (6 November): neighbourhood snapshot (500 m) $500, customer profile (500 m) $2,000, city profile $6,000. Billed with that night's marketing bill; the report arrives the next morning only if the bill is paid; an unpaid bill declines the order with nothing charged. At most one pending order per owned food business and eight per city; the last 16 reports are kept.
- **Street Food vehicles** (6 November): at a vehicle Persistent City owns, crew-run openings get residents instead of Street Food's customers. Openings the player works personally, city events and catering stay with Street Food. Street Food reputation and health grades do not affect residents. Drinks count as food unless config marks them as drinks.
- **Movement** (6 November): blocking a person delays them by at most one game minute per journey. A visible person far behind hurries for up to ten seconds, then fades out at a safe corner or doorway. Nobody jumps on screen.

## Ownership and authority, October 2026

- On 24 October automatic ownership of eligible player food businesses replaced opt-in enrolment. Ownership transfers only at native accounting boundaries. Unsupported businesses stay native-owned. Rival finances are never written.
- None of this authorizes RLO source changes, deployment to an ordinary save, or publication.

## RLO overlap, 12 November 2026

Peter decided that Persistent City stays independent of RLO, stays compatible with RLO 1.0, and overrules RLO where the two overlap ([#239](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/239)).

- RLO's Living population switch on or off, and RLO's customer-calculator replacements, no longer refuse the owned checkout.
- A Living population switch that reads Unknown at load refuses the checkout. Unknown during play defers new acquisitions.
- Foreign Harmony patches on the economy hooks still refuse.
- While Persistent City cars hold the ordinary car groups, RLO's accident switches are held off and the player's stored value is restored at release.
- This is runtime coordination. It authorizes no RLO source edit and no new RLO API. Details are in the [RLO contract](../rlo-contract.md).

## Money, 28 October 2026

- Resident payments match native price precision exactly. Native prices are never changed or rounded. Amounts use a fixed unit that holds every supported native price and every cent; the unit is defined in `Core/FoodMoney.cs`.
- Peter accepted a cap of about $343.6 million per amount; checked arithmetic fails loudly above it.
- The game's own float running totals drift by native accumulation. That drift is the game's and is reported apart from the exact order and resident sums.
- Accepted limit: a receipt confirmed late adds to its checkout day's history row, which other sales may have filled meanwhile. Overflow there needs about $343.6 million in one shop-day. It is not guarded, and it is not a fix task.

## Performance and RLO checks, 28 October 2026

- Performance work must keep outputs, save validation and save bytes unchanged.
- The food decision cache stays; checkout always reads live offers and prices.
- RLO checks stay synchronous and single-pass, with no sliced or delayed refusal.

## Food choice, 8 November 2026

- **Meals:** one main dish plus up to three affordable extras from the same shop in the same visit. Extras are mostly cheaper items; a second similarly priced dish is rare. Extras add no appetite, journey or service slot, and nothing is inferred from product names. The band edges are policy: an extra costs at most 75% of the main to count as cheaper, 75% to 125% is the rare band, and above 125% is never an extra. Smoothing that step would be a new owner decision. Rates are in `Core/FoodMealExtras.cs`.
- **Price anchor:** a fixed $12.50 typical-meal reference gives a monopoly a demand penalty for raising its whole menu. The earlier $5 anchor came from a detached fixture and had no grounding in native meal prices; it was rejected. Hard wallet affordability stays. The weight and the acceptance criterion are in the [price weight record](2026-11-08-price-weight.md).

## Save schema, 9 November 2026

Peter: "We should hold it at 1 until release. Nobody has a save since its not released."

- The save schema and every own-state version written into a save stay 1 until release. No per-feature schema constant, writer number, migration, build stamp or format fingerprint.
- The format changes in place. A deliberate change re-pins the golden hashes in the same change.
- Other schema numbers, malformed data and over-bound data are refused and kept, never overwritten.
- Accepted risk: a development save written at schema 1 by an older build may fail or misread after an in-place change. Rig and test cities are rebuilt. `tests/SchemaHeldAtOneTests.cs` enforces the number.
- Normal versioning starts with the released format.

## Delivery and docs, 6 to 13 November 2026

- In-game checks are batched for the rig and do not gate merge (6 November). Evidence goes in the PR description.
- Tests run focused while working and in full once before merge (9 November). Peter removed the machine-wide dotnet lock on 10 November.
- Docs stay as lean as possible (10 November): slices write no docs unless the task names the file. Later rules on code comments and new Markdown files are in AGENTS.md.

## Lessons from failed approaches

- **Compact save format (F7d, 6 November):** fewer bytes did not show a shorter native pause, and the extra encoding, compression and identity work cost time. The format was dropped in P-A. [Record](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/decisions/2026-11-06-f7d-compact-format.md).
- **Interior view (Track F, October):** no safe renderer for life seen from inside or through shop windows was found.
- **Physical bodies at 6× speed:** the experiment drifted off roads. Native 6×, interiors and time-machine skips release bodies and the city continues in the background.

## Continuity rules

Carried from the October design page.

- A route is contiguous and ends at a known destination. Unreachable routes wait and retry; arrival is never fabricated.
- Player and camera position change representation only, never who exists or what they did.
- Travel completes before another activity starts. Late or closed arrivals may lose an activity; nobody teleports to recover a timetable.
- An attainable later opening uses a saved waiting state with the original destination and deadline.
- A simulation step has a bounded event budget and records how far it has caught up.
- The RLO source roster stays separate from active residents. Actual assignments and journeys are saved; incompatible snapshots are refused, never re-rolled.
- Housing uses only known rentable or buyable interior area. Gated private estates stay excluded. Decision 1's homes the player cannot rent need their own capacity rule.
- A road body needs the saved topology fingerprint and node sequence. No match means no body; the adapter never fabricates a road route or borrows a foreign vehicle.
