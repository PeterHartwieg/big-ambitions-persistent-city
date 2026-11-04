# City audit F6c (#93): feedback v1 — week-over-week causes, budget states, incremental panel counters

Refs #93.

Implements the F6c row of [the F6 plan](../plans/f6-economy.md#4-slice-breakdown) (section 3.6 and the F6c rows of sections 5 and 6), offline. Persistent City is not live or released; nothing here is verified by rig yet. Base `c64e3e2`, writer 25.

## What changes

- **Ctrl+F8, per managed shop:** this week (days today − 6 … today, today in progress) against last week (today − 13 … today − 7) from the existing 14-day `FoodShopDay` rows: purchases, sales and the change, and week totals of evaluated offers by cause (chosen, unaffordable, travel/time, closed or too late, stock unavailable, no staffed counter, service/queue, preferred another option, unknown evidence). Every block says these are evaluated offers, not people, and are not located by hour or meal. Partial weeks are labeled `partial: recorded from day X` or `last week not recorded`, and a change appears only when last week is fully recorded. No new history.
- **Rival label:** the D3a line is prefixed with `FoodFeedback.RivalEstimateLabel` (`"Rival availability: estimated"`), one constant for F6b's wording. `Core/RivalFood.cs` and rival settings are untouched.
- **City money summary** on the current per-consumer budgets: the four existing lifetime lines plus counts behind on bills, with no money to spare for prepared food, and without the home-food basket at the last posting.
- **Incremental counters** (`FoodMoneyCounters`) replace the panel's O(consumers) scans: attached budget, meal-status and excursion setters report exact changes (O(1), 0 B), rebuilt by one scan when a market attaches. The panel also groups history, places and native statements once per build.
- **Inspector words** for current budget states (`ResidentBudgetWords`), never numbers.
- **Rig diagnostic** `PopulationRuntime.FoodFeedbackOverview` (counters vs rescan, request counts, week and money text), built only when read.
- Plan section 9 defaults: Q3 no free share line (`FoodFeedback.FreeMealShareLine = false`), Q4 facts only (`FoodFeedback.SuggestionLines = false`).
- **Unchanged:** behaviour, economy, saves (writer 25; counters are never serialized), native reads/writes, RLO, the 3 s open-panel cadence. Nothing is built while the panel is hidden.

## Offline evidence

GATES_PLACEHOLDER

12 new tests (`tests/FoodFeedbackTests.cs`): golden week text with row-sum invariants; partial first weeks; the week boundary for days 0–40 with and without pruning; a 16-day simulated history and its reload; counters equal rescans after 4,000 random setter mutations, at 128-bit extremes, after every simulated minute for four days, after save/reload, schema-24 → 25 migration and route rebuild; short flag equals `SpendingLimit == 0`; inspector words; hidden panel 0 requests / 0 B plus Runtime source guards; no paid-research fields (D3a type guard extended); saves byte-identical. New tool `tools/feedback-panel-regression` repeats the invariants and measures.

## Performance ([docs/perf/f6c.md](../perf/f6c.md))

Desktop .NET 10 component timings; no Unity/whole-frame evidence (whole-frame is tracked separately by the rig).

| Work | Before p95 (max) | After p95 (max) |
| --- | --- | --- |
| Panel consumer work, 1,914 consumers | 1.1088 ms (1.4989), 743,240 B | 0.0026 ms (0.0111), 6,280 B |
| 10,000 consumers | 3.4898 ms (4.4826), 3,863,008 B | 0.0050 ms (0.0086), 6,456 B |
| 100,000 consumers | 32.1975 ms (**46.5256, flagged**), 39,248,344 B | 0.0030 ms (0.0048), 7,768 B |
| Shop blocks, 1 shop (20,000 places) | 0.0453 ms (0.1472) | 0.2211 ms (0.2695), with new week text |
| 64 shops | 2.7575 ms (8.3129) | 0.5459 ms (7.3592) |
| 1,024 shops | 116.1171 ms (**123.5872, flagged**) | 14.5748 ms (26.0409), 4,525,584 B |
| Hidden panel | — | 0 B, 0 requests |
| Posting call (32 budgets), 1,914 / 10,000 / 100,000 | 0.0232 / 0.0169 / 0.0089 ms | 0.0281–0.0395 / 0.0268–0.0273 / 0.0095–0.0099 ms (cost, 0 B) |

Samples ≥ 30 ms occurred only in the removed code. The four parked hotspots are untouched. Perf wins are logged in the roadmap "Perf wins log".

## In-game checks for the next batched rig

RIG_PLACEHOLDER

🤖 Generated with [Claude Code](https://claude.com/claude-code)
