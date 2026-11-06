# City audit F6b — rival service limits: performance, 4 November 2026 (offline)

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F6b — [Rival lane/sell-out cost, reconstruction and retained bytes; no win taken](f6b.md).

Refs #93. Source: `dotnet run --project tools/rival-limits-regression --configuration Release`, foreground, on
this PC (Windows, .NET 10, Release). Wall times are detached Core cost. They are **not** in-game mod-work,
whole-frame or native evidence, and other worktrees' work shared the machine. The gate is mod-work p95
< 5 ms, with every mod-work sample ≥ 30 ms flagged; whole-frame is tracked separately. In-game numbers come
from the next batched rig run ([check 8](../checks/f6b.md#in-game-checks-for-the-next-batched-rig)).

## Added work per rival arrival and completion

Arrival is the added F6b work: a lane scan, the sell-out count and admit. Completion is the paid move.
200,000 operations each; the first 1,000 are warm-up and excluded.

| Lanes / Serving at rival | Arrival p50 / p95 / max ms | Completion p50 / p95 / max ms | Allocation |
| --- | ---: | ---: | ---: |
| 2 / 2 | 0.0001 / 0.0001 / 0.1145 | 0.0000 / 0.0001 / 0.0124 | 0 B/op |
| 8 / 16 | 0.0004 / 0.0007 / 0.0879 | 0.0001 / 0.0001 / 0.0879 | 0 B/op |

Plan target: ≪ 0.01 ms and 0 B steady state. Met. The maxima are single scheduler/timer outliers; there is no
sample ≥ 30 ms. The one-time first allocation per rival place (dictionary entry and a 4-slot list) happens on
that rival's first load.

## Attach-time reconstruction

The load is rebuilt inside FoodCommerce's existing O(consumers) attach pass. The fixture gives every consumer
three outcome records, a third of them paid at a rival this period, and 2 % in Serving. Six timed runs follow
one warm-up.

| Consumers / rivals | p50 / p95 / max ms | Allocation per rebuild |
| ---: | ---: | ---: |
| 1,914 / 64 | 0.0701 / 0.0805 / 0.0805 | 12,976 B |
| 10,000 / 1,024 | 0.2472 / 0.3226 / 0.3226 | 233,448 B |
| 100,000 / 20,000 | 4.9798 / 17.7817 / 17.7817 | 4,555,304 B |

This is load/attach work, not per-frame work. The 100,000-consumer maximum (17.78 ms, likely a GC during
first growth) is under 30 ms but is noted. It is the stress size, not the supported 1,914-consumer city.

## Retained bytes

20,000 rivals, each with one paid count and two Serving meals: **3,570,344 B (178.5 B/rival)** for the
transient load (dictionary, entries and Serving lists; the meal objects already exist). Rivals with no load
retain nothing.

## Whole-day Core advancement (one rival, 1,914 consumers)

1,440 one-minute `AdvanceTo` calls through a full day: p50 **0.0003 ms**, p95 **0.3313 ms**, max
**7.9163 ms**, **0 samples ≥ 30 ms**, 20,188 B/call average, 78 served. All scheduler work is included, so this
is not an F6b-only cost and not whole-frame.

## Wins and parked hotspots

No performance win was taken in this slice. A candidate was found: replacing the LINQ closures in
`RivalMatches` (one per rival arrival/completion) with loops. It was not taken because no matched before
baseline was recorded, and the native `ReadRival` read it sits next to dominates. Nothing is logged in the
roadmap Perf wins log. The four parked hotspots (physical population, closure refresh, optional-stop refresh,
save serialization) are untouched. F6b adds no frame participant, reflection, polling, scan or per-frame
string; diagnostics strings are built only on request.
