# Persistent City review, 9 November 2026

Reviewed head: `649467b` on `codex/persistent-city`. X3 (schema held at 1) and ten other slices merged after the review; where a later merge changes a statement, a note says so. Peter's decisions on the findings are in [City direction, 10 November 2026](../decisions/2026-11-10-city-direction.md).

How the review worked. Four independent reviewers each took one area: simulation model, runtime and presentation, core performance and saves, agent readability. The lead read the vision decisions, the [3 November audit](2026-11-03-city-vision-audit.md) and both rig result sets, then checked the reviewers' main claims against the code. Nothing was built and the game was not started. The 9 November rig numbers come from the unmerged [rig results PR](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170), and the rig labels them provisional because the PC was shared.

## Short answer

Keep C#, the pure Core, the event queue, the traffic kernel and the identity work. A full rewrite would not fix what is wrong, and neither would ECS, Burst or another language. Core spends microseconds per event. The time goes into native Unity calls, garbage and the save.

What limits how real the city feels is four things, in this order.

1. Nobody has played the current head. The rig ran 510 checks on 9 November and 435 did not run. Neither November rig session saw a single paid sale.
2. The street is thin by policy. The mod switches off the game's own crowds and ambient cars, then shows only its 2,871 named people.
3. Everyone lives the same day. One hard-coded template per persona produces three citywide pulses and quiet streets between them.
4. The bodies give the trick away. Twins, one wardrobe, pop-in, cars that vanish at the kerb.

Three subsystems deserve a rewrite: the save path, car presentation and the daily routine. Agents need a deletion and renaming pass more than any new structure.

## 1. The evidence gap comes first

The repository grew from 15,000 to 52,700 production lines in 17 days, through 187 PRs. The in-game record did not keep up.

| Rig session | Checks | Passed | Partial | Failed | Not run |
| --- | ---: | ---: | ---: | ---: | ---: |
| 6 Nov | 310 | 6 | | 1 | 303 |
| 9 Nov | 510 | 6 | 64 | 5 | 435 |

The rig cannot build a shop that sells. On 9 November the test shop refused croissant service even with Persistent City removed, so every sales, receipt and accounting check is blocked by a fixture problem in the rig. About 10,400 of Core's 24,800 lines are food code, and a large part of Runtime serves it. That code has unit tests and no in-game proof at this head. The last in-game sales evidence is from October.

No hands-on session is recorded for the current head either. The roadmap lists Peter's playtest as the last step before release. That order is backwards for a mod whose whole point is how the city feels. Your question, how close can this get to a real city, is one a 30-minute session in a disposable city would answer better than this document.

## 2. Realism

### What the player sees today

Numbers from the 9 November perf run. Fixed camera at the test shop in the Garment District, default caps of 48 walkers and 24 cars inside 120 m, 957 residents plus 1,914 commuters, 119 samples over two game days.

| | Median | Mean | At the cap | None in view |
| --- | ---: | ---: | --- | --- |
| Walkers | 12 | 20 | 48 reached in 14 samples | never |
| Cars | 2 | 3.2 | max 13, cap never reached | 34 samples |

People on the move citywide ranged from 9 to 1,800. So the street swings between nearly empty and capped, and the road has no car in view almost a third of the time.

The code explains the pattern.

- The mod swallows the game's pedestrian density request (`Runtime/AmbientPedestrians.cs:123`) and retires ambient cars one per frame. Everything on the street is a named resident, an RLO scene, or a native customer or worker.
- `Core/DailyRoutine.cs:77-160` is the whole day. Work is eight hours from 08:30, 07:45 for parents, 12:00 for night owls, with a fixed offset of up to 15 minutes per person. Everyone works days 1 to 4 and shares a three-day weekend. Shopping is 45 minutes at 17:30 on about two days in three. Leisure is 75 minutes at 19:00 on about half of weekdays.
- A workplace's opening hours do not shape its staff's shift. A bar worker turns up at 08:30, finds the bar closed and goes home.
- `Place` has five kinds and no business type. "Shop" means any retail building, and each person has one shop for life.
- Derived residents copy a donor's appearance seed (`Core/IdentityPolicy.cs:90`) from about 400 RLO profiles, so about seven people share each look. With 48 bodies on screen the chance of an identical pair is about 94 percent. Every body is dressed from the `Casual` tag only and can stand or walk. It cannot sit, talk or carry anything.
- Bodies appear in view with no fade. A walker reaching the subway or a car reaching its parking point disappears on the spot. Parked cars do not exist as objects.
- Fast-forward, entering a building and the time machine release every body, and the native crowd stays suppressed. The reviewer expects empty streets at 6x. Nobody has checked that in game.

### The ceiling of the current approach

As built, the mod delivers one thing well. You can click a person, follow them all day, reload, and they are still the same person on the same journey. That part holds up under code review.

As a street scene it will read emptier and more regimented than vanilla, because vanilla tunes density for looks and this mod tunes it for bookkeeping.

The model caps realism before the technology does. The reviewers estimate about three shop visits per shop per day in the reference city. No amount of rendering work makes that look like a city.

### What raises the ceiling, by effect per cost

**Density is a design decision and it is yours.** Three options.

- Let native crowds and ambient cars back in at reduced density as anonymous filler. This is one policy change in two hooks and the street looks alive the same day. It breaks the rule that everyone you see is someone, which you set on 31 October.
- Grow the named population. Housing counts only interiors the player can rent or buy, so apartment blocks hold nobody. Counting them could give 8,000 to 15,000 people. Core can carry that. The save, the garbage collector and route queries cannot yet, see section 3.
- Both. Named people near the player's businesses and on the inspector, filler for the rest, with a visible difference when you click one.

I would pick the second and accept the first as a stopgap for the first playtest, so the playtest judges the city and not the emptiness.

**Day plans instead of the template.** Generate an ordered plan per person per day from role, the workplace's real hours, household, needs and wallet. Keep it seeded so reloads still replay. Meals become plan items. This spreads trips across the day, gives bars night staff and evening crowds, and makes weekends differ. It replaces one file's logic but touches the food routines, so call it 8 to 12 slices together with the next two items.

**Business type on places, wallet on the person.** Today the wallet lives inside the food roster and non-food visits charge a flat 5 or 12 dollars. The player's own non-food shops get visits and no money. Adding one new activity such as a gym habit costs 12 to 15 files today. With a type table and a plan generator it costs a table row and a rule.

**Households.** Budgets are individual and no relation field exists, so nobody ever does anything together. Shared plan items for couples and families are the cheapest way to make two bodies on the street look like they know each other.

**Presentation fixes.** Fade in on acquisition and fade out at mode changes. Vary appearance beyond the donor seed and use more than the casual wardrobe. Spawn a parked car object. Replace the borrowed Gley cars with the mod's own kinematic cars that follow the kernel position, which also removes the most fragile code in the repository.

### What stays out of reach

- Life seen through shop windows or from inside. Track F found no safe renderer.
- Physical bodies at 6x. The experiment drifted off roads.
- Rich body language. The mod has the game's animation set and nothing else, unless it ships its own asset bundle.
- Independence from RLO and from game updates. Runtime has 34 Harmony patch sites and about 99 private reflection lookups. The car hook demands exactly 24 and 17 IL instructions around `AddForce` in Gley's `FixedUpdate` (`Runtime/PhysicalTrafficHooks.cs:164-185`). A recompile of that plugin fails closed, and physical cars silently stop appearing.

## 3. Performance

### Measured in game, 9 November, 2,871 persons

| Measure | Result | Gate |
| --- | --- | --- |
| Mod-work p50 per window | 0.5 to 1.4 ms | |
| Mod-work p95 per window | 0.9 to 5.1 ms, over 5 in 2 of 9 windows | under 5 ms |
| Mod-work p95 per rush hour | 5.0 to 5.7 ms in 9 hours | under 5 ms |
| Intervals of 30 ms or more | 10 | 0 |
| Save encode, default save, 1,914 persons | 374 ms median | plan budget 40 ms |
| Save encode, default save, 2,871 persons | 671 ms median | |
| Save encode, midnight save, 1,914 persons | 986 ms median | |

The mod is at its gate with under 3,000 people and 20 walkers on screen. Any plan to grow the population or the body count has to start here.

The ten long intervals have four causes.

- Five are the first frames after switching from speed 6 to speed 1, at 110 to 430 ms inside `PopulationRuntime.Update`.
- Two are optional-stop reads at 97 and 129 ms. `Runtime/OptionalStopEnvironment.cs` calls `FindObjectsOfType` and NavMesh queries on the event path. Issue 128 was meant to close this and it still shows.
- Two are route planning with a garbage collection inside, at 33 and 42 ms.
- One is the physical population at 47 ms.

### The save is the worst number

The rig machine's native autosave setting is every five minutes. Each autosave would freeze the game for 0.4 to 1.0 seconds at today's size. The perf gate never sees this because the rules switch autosave off in measured windows.

The compact writer made it worse. In game the previous writer encoded a comparable 1,914-person city in 263 ms against 374 ms now. The two cities had different seeds, so treat the gap as indicative. The writer builds a Newtonsoft token tree and then runs a column pass over it. The reader takes four passes. The game gzips its save anyway, and the decision record itself notes deflate would have saved 1.3 percent. The hand-written columnar format bought a shorter string at the price of a longer pause.

### Where the time goes

Core is not the bottleneck. The offline F5h measurement puts a citizen event at 2 to 3 microseconds and frame p95 under 0.4 ms up to 100,000 persons. I did not rerun it.

The costs are elsewhere.

- Each walker recalculates a NavMesh path about every half metre, roughly four synchronous `CalculatePath` calls per second per body (`Runtime/PhysicalPedestrians.cs:204`). This is the main obstacle to showing three times as many people.
- `RefreshOptionalStops` walks every citizen twice on every frame (`Core/OptionalStopScheduling.cs:148`). Car snapshots sync every car in traffic every frame.
- Every simulation event clones the citizen and its visit list. On Unity's non-generational collector that garbage becomes pauses. The reviewer suspects this behind the unexplained 100 to 175 ms whole-frame stalls. No Mono GC measurement exists to confirm it.
- Every 30 seconds the mod does a full reflection read of RLO and drops the whole walking path cache. The 27 October review flagged both.
- The physical layer has no time budget at all, only counts.

### Scaling

| Persons | Core frame cost | Save pause | First thing to break |
| --- | --- | --- | --- |
| 3,000, today | fine | 0.4 to 1.0 s in game | save, spikes |
| 5,000 | fine | about 0.7 s offline, more in game | save |
| 20,000 | fine | 1.5 to 2 s offline estimate | save, GC |
| 50,000 | per-frame scans start to count | 2 to 4 s offline | save, GC, route queries at fast speeds |

### Stack options

| Option | Verdict |
| --- | --- |
| Another language | No. The game loads C# mod DLLs and the cost is in Unity calls. |
| Burst and Jobs | No. Core CPU is not the problem, and a runtime-loaded mod would need a precompiled Burst library matched to the game's exact Unity version. |
| ECS-style arrays with integer IDs | Only for the per-event working copy and the traffic kernel, and only after a Mono GC measurement says so. |
| Background simulation thread | Later. Events call native stock and checkout synchronously, so it needs a command queue first. |
| Single-pass save, encoded off the main thread or to a sidecar file | Yes, now. |

## 4. Agent readability

The layering is sound. What costs agents time is history left in the working tree.

**Entry docs are wrong.** `AGENTS.md` is accurate and short. Everything it points to is stale. `README.md` lists F5c to F5i, F6d to F6h and F7c to F7e as planned, and all have merged. `docs/behaviour.md` says schema 25 with migration from 13 to 24 and paid research inactive. At the reviewed head the code wrote schema 36 through a chain of eight "+1" constants (`Core/Model.cs:373-389`), refused everything older, and had research billing merged. `docs/save-format.md` describes a JSON wire that F7d replaced. `CHECKS.md` links 56 of 102 checks files. X3 has since set the schema to 1 and removed the chain.

**The docs outweigh the code five to one.** 586 files, 255,000 lines, 17 MB, against 52,700 production lines. Thirteen files and 1,100 lines are live reference. A search for "queue" returns 1,658 hits in 277 files and under 2 percent are current.

**File names do not say what is inside.**

- `CitySimulator` is one partial class across 17 files, `PopulationRuntime` across 22, `SaveCodec` across 15.
- The 13 `Persistence/*WireNN.cs` files are `SaveCodec` partials named after dead schema numbers.
- `PrototypeSettings` is the live settings class. `FoodFeedbackV2` and `FoodFeedback` are both in use.
- Production code carries 667 slice-ID tokens. Comments say "E5b4:" or "F2e:" where they should state the rule.
- Core has 122 files in one flat folder and one namespace, 41 of them named `Food*`.

**Saved fields are expensive.** The slices that added one touched 43 to 50 files each, about half of them test pins. Changing a queue time touches 3 to 5.

**Tests and tools carry the same residue.** The test project compiles 42 Runtime and tools files directly. Fifty-four test files define 77 private route-planner doubles. Eighteen `*Measure.cs` files run as ordinary tests in the full suite. Some tests assert on production source text and will break on any rename. `tools/` has 60 regression directories, about 18,000 lines, that the rules say nobody runs, and X1 had to edit six of them to keep them compiling.

**Dead code.** About 3,100 Runtime lines are diagnostics or stopped work, including the E3 feasibility readers with no caller and the street proxy. The traffic kernel still carries the physical-control branches that F2 made unreachable, and still saves that field.

### Fixes, cheapest first

1. Hours. Correct the five entry docs. Generate the README and CHECKS tables from the checks files so they cannot drift.
2. A day. Move `docs/checks`, `docs/perf`, `docs/reviews`, `docs/archive`, merged plans, the regression tools and their evidence JSON to a tag. About 300,000 lines leave the tree.
3. A day. Delete the unreachable readers, E3 readers, street proxy and kernel physical-control code.
4. A day. Write one `docs/map.md` with the tick path, the save path, where settings live, the partial classes and their files, and three how-to recipes.
5. Two to three days, mechanical. Subfolders with matching namespaces. Rename by meaning. Ban slice IDs, schema numbers, V2, Legacy and Prototype from identifiers and file names. Remove the source-text test pins first.
6. Two to three days. One test city builder, one fake planner, one golden hash, measurements behind an environment flag.

Stop requiring a checks file and a perf file per slice. A PR description and one rolling rig checklist do the same job.

## 5. Recommended order

1. **Play it.** Build the head, install it in a disposable city on your machine, play 30 to 60 minutes at morning, midday and night. Fix the rig's serving fixture in parallel. Hold new feature slices until both are done.
2. **Clean the tree.** The six readability fixes. One to two weeks of agent time, and every later slice gets cheaper.
3. **Rewrite the save path.** Single pass from the snapshot, one strict reader, encode off the main thread or into a sidecar file. Target under 40 ms at 3,000 persons. This removes most of Persistence's 8,100 lines.
4. **Remove the spikes.** Index active optional stops, cache the scene reads, make the RLO read change-driven, fix the speed-change retiming, give walkers one path per leg. Add a Mono GC measurement to the rig.
5. **Decide density, then build day plans.** Population policy, plans, place types, wallet on the person, households.
6. **Presentation.** Kinematic cars, fades, appearance variety, parked cars. Then raise the walker cap.

More food detail, such as the rest of E5b, should wait until the rig has seen one sale.

## 6. Decisions asked of Peter

1. Density. Anonymous filler, a larger named population, or both.
2. Whether the hands-on session moves to the front.

Peter answered both on 10 November: a larger named population with through-traffic cars as the one anonymous exception, and the hands-on session stays last. See the [decision record](../decisions/2026-11-10-city-direction.md).

## What this review did not establish

- Nothing was run. Performance numbers are the rig's and the repository's own.
- The rig timings are provisional. The PC was shared and the background player ran at 30 FPS.
- Mono GC behaviour, native pool sizes and whether vanilla fast-forward also hides pedestrians are unknown.
- Trip-volume figures in section 2 are the reviewer's estimates from code, not measurements.
