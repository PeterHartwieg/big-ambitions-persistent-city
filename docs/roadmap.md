# Persistent City roadmap (Peter's answers, 2026-10-30 15:17–15:24 JST, guided Q&A with Spock)

Replaces the phase order in docs/reviews/2026-10-27-realism-gameplay-roadmap.md where they differ. Same principles still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals.

## Vision
"Both equally: a living city that also drives my business." It should feel like a deep optimization game, not formula numbers you work out in the first 5 hours.

## Product decisions
1. **Choice is probabilistic, not a fixed score.** Preferences raise the chance for an item. Distance raises the chance for nearby shops but never rules out distant ones. Keep it deterministic per save through seeded draws, so a reload replays the same day.
2. **Resident inspector first.** Click a resident to see where they live and work, what their preferences are, and what they ate or skipped and why.
3. **Individuals are described in words, not numbers** (e.g. "loves burgers", "price-sensitive", "hates waiting"). City-wide statistics come from **paid market research through marketing agencies**, which gives players a new thing to spend money on.
4. **Traits, core plus lifestyle:** food preferences, price sensitivity, patience (queues), loyalty vs curiosity, willingness to travel, health-conscious, brand or quality focus, time pressure (commuters).
5. **Memory is driven by the loyalty trait.** Loyal residents keep going back to the same shops, curious ones try new places. Bad visits (stockout, closed, queue) lower the chance of returning, weighted by traits. Memory stays bounded, about 4 shops per consumer. The performance review checks the cost.
6. **Marketing means awareness plus targeting.** Campaign types reach different resident groups (commuters, health-conscious, ...). Awareness only makes a visit possible; traits still decide.
7. **Rivals are real alternatives.** Residents can choose them over Peter's shops. Rivals are read-only with an outflow line, and nothing native is written.
8. **Weather, street life and bigger menus come after the core loop works.**

## Order
**Peter 15:25 JST: "Release will only happen at the end."** There's no interim release. Steps A–E build on the development line (still merged through the normal gates), and the single Workshop release comes after the full roadmap. Performance stays a standing requirement that is re-checked as features land, and it must pass before that final release.

0. Performance: the comprehensive review is running now, then fixes. This isn't a release step.
A. Inspector, traits and probabilistic choice. Start now (Peter 15:24 JST). With no interim release, it doesn't need to wait for the performance gate.
B. Memory, loyalty and regulars.
C. Marketing awareness and targeting, plus the market research product.
D. Rivals as alternatives.
E. Weather, street life, bigger menus and more products.
Then: the final performance gate, the hands-on session, and one release.
Later: residents as employees, households.
