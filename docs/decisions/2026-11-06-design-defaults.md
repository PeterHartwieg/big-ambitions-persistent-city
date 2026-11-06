# Game-design decisions, 6 November 2026

Peter decided these on 6 November 2026. He makes game-design decisions; technical decisions are made by the planner or implementer and recorded in the plan.

## Release scope

- Paid market research ships in the first release, in game. Players order it from the existing marketing agencies by phone and pay through the agency's own billing. Plan: [C5](../plans/c5-agency-research.md).
- Cart and food-truck sales (E6) ship in the first release, so Persistent City can be part of Peter's modpack with the Street Food products that have no building holder. Plan: [E6](../plans/e6-carts-food-trucks.md).

## Accepted defaults

Peter accepted every recommended default in section 9 of these three plans:

- [F2 movement authority](../plans/f2-movement.md#9-game-design-questions-for-peter): a held-up visible person quietly catches up, and the city never waits for what is on screen. Blocking someone delays them for real, by at most 1 game minute per journey. A visible person who has fallen far behind hurries for up to 10 seconds, then fades out at the next corner or doorway and reappears in the right place; nobody jumps on screen. People still stop at a street performer's spot when no performer is out.
- [E6 carts and food trucks](../plans/e6-carts-food-trucks.md#9-game-design-questions-for-peter): at a vehicle Persistent City owns, residents replace Street Food's own customers. Openings the player works personally keep Street Food's queue; crew-run openings get residents. Drinks count as food unless config marks them as drinks. City events and catering stay with Street Food. Street Food reputation and health grades do not affect residents.
- [C5 research through agencies](../plans/c5-agency-research.md#9-game-design-questions-for-peter): three products, billed once with that night's marketing bill: neighbourhood snapshot (500 m) for $500, customer profile (500 m) for $2,000 and city profile for $6,000. No in-depth tier until its data exists. The report arrives the next morning, after the bill is paid. Only Persistent City food businesses can order. An unpaid bill declines the order: nothing is charged, there is no report, and the agency sends a message. The last 16 reports are kept per city; at most one pending order per business and 8 per city.
