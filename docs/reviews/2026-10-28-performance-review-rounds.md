# Performance changes: review rounds, 28 October 2026

Peter asked for the remaining architecture-review findings (2, 3 and 7) to be implemented by Claude Opus 5.5 agents and for review rounds by GPT-6.1 Sol, GPT-6 Astra, Grok 4.7 and one Claude Opus 5.5 agent, fixing meaningful findings only. This record lists each round, what it covered, every finding and what was done. Sol and Astra ran through `codex exec` in a read-only sandbox (they could read everything but their `dotnet test` attempts failed on a denied temporary directory); Grok ran headless with read access plus `dotnet` and `python3` (rounds 1 to 3 carried a 200-turn ceiling that no run reached; Peter then asked for no turn budgets, and the round 4 run is uncapped); the Opus reviewers ran the suite and harnesses in copies of the snapshot. Every round reviewed a frozen copy of the development tree, never the live checkout.

## Round 1: resumable planning and physical caching (27 October changes)

Snapshot: the tree as integrated on 27 October, before today's agents started.

| # | Finding | Raised by | Decision |
| --- | --- | --- | --- |
| 1 | `TrafficJourneys.EnsureTrafficNetwork` rebuilt the due queue without skipping the event whose planning request was pending; `CompleteEvent` then changed that item's minute inside the `SortedSet` and its `Add` failed silently. The runtime reaches this whenever a road snapshot is replaced while a job is suspended. One reviewer reproduced it offline: queue out of order, clock moving backwards, traffic kernel throwing, population stopped. | Sol, Astra, Grok, Opus (blocker) | Fixed: the rebuild skips `InProgress(item)`; `CompleteEvent` throws on a failed `Add`; `tests/PendingEventQueueTests.cs` adapted from the reviewer's reproduction. |
| 2 | A planning job that restarted alone on a replaced snapshot kept a car `HomeCurbWalk` had allocated against the old one, so the retried plan differed from a reload's replay. | Sol, Astra, Grok (meaningful) | Fixed through finding 3's event-level restart (an interim `carAtEntry` restore was replaced). |
| 3 | The same restart kept answers already applied earlier in the event (workplace probes, optional candidates), so one event mixed two snapshots while a reload replays all of it on the new one. | Grok (meaningful) | Fixed: the job throws `RoutePlanningRestartRequired`; `CitySimulator` discards the working copy, requeues the unstarted event, holds the clock and replays it; after three restarts the event runs inline. Harness case for the first home-car allocation across a replacement; tests for replay from the first yield and for the inline fallback. |
| 4 | `EndCyclePrefix` reused the frame's RLO reservation read before holding or retiring a body (a relaxation the physical caching report had stated). | Sol, Opus (minor) | Fixed: a live read right before every hold or retire; the per-frame memo stays for `Update` and the early checks. |

## Round 2: core identities, food supply cache, round 1 fixes

Snapshot: after both implementation agents finished.

| # | Finding | Raised by | Decision |
| --- | --- | --- | --- |
| 5 | A decision supply image can be up to one native minute old, so a resident deciding in the same minute as a restock may exclude a shop the live read would offer. Opus added that the game's own distributed hourly processing restocks mod-owned shops mid-minute, so this applies to native writers, not only the player. | Astra, Sol (blocker under the brief's exact-output wording), Opus (minor) | Not fixed. This is the relaxation the cache was designed around; checkout stays live and no sale is booked on a stale image. The food report and CHECKS state it for any native writer and name two possible tightenings. |
| 6 | The once-a-second discovery slice moved when `FoodAuthority.RequestedHour` is first recorded; eligibility in the last two seconds of a native hour would acquire at H+2 instead of H+1. | Astra, Sol (meaningful) | Fixed: per-frame `DiscoverShops(64)` restored with the place-id memo kept; harness scenario replaced. |
| 7 | The door-arrival branch of `EndCyclePrefix` retired a departing walker before the forced reservation read. The order predates today's work. | Astra, Sol (minor), Grok (meaningful) | Fixed: the arrival path forces the live read and rechecks before `Retire`. |
| 8 | A cache hit could hide the supply reader's contract throw if a registration's item list became null or exceeded 2,000 entries within the minute. | Grok (minor) | Fixed: hits require the item list inside the contract. |

Verified correct by the reviewers: bit-identical hash prefixes including negative and extreme days, immutable shared `RoutineKeys`, exact opening-hours invalidation, decoded tags with live topology comparison, transient fields outside the serialized set, the queue fix on every requeue path, budget batching order, live checkout. Opus ran the core-identity comparison against the round 1 snapshot (identical digests, 364 MB to 120 MB).

## Round 3: event-level restart

Snapshot: after the restart fix landed.

| # | Finding | Raised by | Decision |
| --- | --- | --- | --- |
| 9 | A successor job begun in the same `ContinuePlanning` call as its predecessor's answer captured native state only at its first slice, so a snapshot replaced at that frame boundary escaped the restart check and the event still mixed snapshots. Opus reproduced it in a harness copy (job 2 of a carless resident planned on the new snapshot from the old snapshot's car). | Sol, Astra, Grok, Opus (blocker) | Fixed: the job captures roads, access, stations and the snapshot's currency in its constructor and checks them on every `Continue`, including the first; a job begun and finished within one call cannot throw. Harness case `snapshot-replacement-before-first-slice`; test theory index 0; the test double may throw at its first sliced `Continue`. |
| 10 | `planningPendingFrames` missed a frame whose last permitted iteration began a job. | Astra (minor) | Fixed. |
| 11 | The summary printed lifetime restart totals next to per-window counts. | Opus (minor) | Fixed: per-window deltas. |

## Round 4: confirmation of the round 3 fixes, the arrival read and the cache guard

Sol and Astra: no blocker; both verified the four fixes by source. Opus: no blocker; ran the suite (1,260) and the road-ceiling harness (69 plus 142 sliced) in a copy, traced the synchronous, inline, stale-at-Begin, lazy-access, station-rescan and workspace-release paths, and noted one spurious restart reachable only by a tool that drives `Begin` without `Prepare` (left as is). Grok (uncapped rerun; its first run ended without a report after its context compacted): no blocker; ran the suite (1,260) and the road-ceiling harness (69 plus 142 sliced) in its copy and traced the same paths. All four reviewers are green on the final tree.

## Preexisting, outside scope

`tools/food-household-comparison` fails identically on the pre-change snapshot: its retained 22 October input document no longer passes `FoodShadowCodec.Read`.

## Final verification

Recorded in CHECKS.md under "Identities, route tags, food supply cache and review rounds — 28 October 2026".
