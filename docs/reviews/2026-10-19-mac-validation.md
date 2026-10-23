# Isolated Mac validation and follow-up review — 19 October 2026

The background prototype ran in Big Ambitions, imported all 400 live RLO citizens, followed real walking routes, and preserved a midjourney save. Testing found a catch-up throughput problem; a bounded batch fixed the measured scenario. Astra, Sol, Grok 4.7 and Opus 5.5 all explicitly approved that production change with no actionable findings. This is evidence for the background prototype, not approval of visible population replacement or a public release.

## Environment and isolation

- Native Apple Silicon Mac test rig `agentmac`, Big Ambitions build 3682, rig runtime 6, headless. The bridge identity, exact executable/profile and ownership reservation were checked before each lifecycle change.
- Released RLO 0.40.4, Food Trucks bridge 0.10.0 and the rig's existing Driven Rides snapshot. Persistent City and a separate read-only diagnostic helper were installed only into this isolated profile. The helper required the exact rig profile and an FT save name; it could only emit snapshots.
- Fresh FT character. No Windows connection, normal Steam game/profile access, or real-save modification. Eleven preexisting rig save files were hashed before testing.
- Original rig installation manifest backed up. Every installed package change was made while stopped and reserved, with manifest hashes checked. No normal-game deploy scripts were used.
- Headless rendering emits expected shader/portrait errors. RLO's three released bundles identify Windows as their target. Managed Flare behavior worked, but Mac asset/portrait rendering was not validated.

## Functional evidence

Startup initially reported Flare not ready, then activated without intervention when RLO finished loading. The catalogue contained 998 places: 335 homes, 383 work roles, 246 shops, 33 leisure places and one study venue. All 400 citizens had homes across six districts; allocation caps remained four per home and sixteen per day role.

With the player at the starting location, citizens elsewhere performed work, study, shopping, leisure and returns home using complete native NPC NavMesh paths. The final one-day snapshot contained 1,409 cumulative completed journeys, including 668 home admissions, 344 work, 225 shop, 160 leisure and 12 study admissions. There were 214 missed activities. These counters describe the provisional schedule/assignment policies; a completed test does not imply every assigned activity was feasible.

The single study venue has a provisional capacity of 16 against 54 students, leaving 38 without a study assignment. Some assigned venues are unreachable or poorly matched to activity/opening times. Reachability-aware assignments, meaningful capacity policy and live venue refresh remain playable-milestone work; tests did not replace these failures with invented arrivals.

A manual save at day 1 08:39 held five active journeys, 129 opening waits and eight route waits. Its exact Persistent City payload survived native reload. The native clock advanced 0.3333320618 game minutes while loading. An independent offline replay from the saved payload processed three arrivals and zero new path requests and reproduced the entire observed reloaded state exactly after canonical serialization. The other two journeys continued along unchanged routes; all five derived positions, all identities/appearance seeds, assignments and history matched the replay.

## Catch-up defect and fix

The original runtime advanced one event per frame. During a 24-hour run at bridge speed 60, the largest sampled lag was 951.35 game minutes. It was still behind 84.4 real seconds after the first snapshot at the paused target time, and reached zero by the observation 126.6 seconds after that snapshot. Sampling does not establish the exact recovery instant.

`Runtime/PersistentCityMod.cs` now advances at most sixteen events per frame, checking elapsed monotonic wall time after each complete event and stopping at a soft two-millisecond budget or when caught up. The first event is always allowed. A single native path/event cannot be preempted, so this is not a hard frame-time guarantee. The core's event order and saved schema did not change.

The revised run began from the exact same full state and repeated the same 24-hour/60x scenario. It reached zero backlog at the first paused observation, within the 2.39-second interval since the last running observation. Total observed run time was 26.85 seconds. The largest sampled lag during the run was 57.19 game minutes; this is a sampled value, not a measured peak.

One diagnostic interval recorded 4,019 events over 1,641 advancement frames, 205.926 ms total measured advancement time, 3.622 ms maximum advancement time in one frame and sixteen maximum events in one frame. The observed overrun is permitted by the soft budget. These timings include the advancement loop only, excluding population creation, dependency checks, logging, saving, rendering and diagnostic snapshot work; no total-frame/CPU or allocation claim follows.

Both full-day runs ended with the same 1,409 journeys and 214 misses. All 400 citizens' identities, assignments, preferences, final phases, counters, active routes and visit destination/activity sequences matched, as did the full catalogue. There were 178 historical visit-time differences across 99 citizens, plus a tiny final native-clock difference. The largest visit-time difference was 0.0234925306 game minutes (1.40955 game seconds); 162 of the time differences were at most 1e-7 game minutes. Retained route geometry was identical and derived positions matched at a common clock. Native path/cache variation is plausible, but its cause was not established without historical path traces. Full-day timestamp determinism is therefore not claimed. A separate replay using shared recorded routes was identical under event budgets one and sixteen.

## Save isolation and lifecycle

During a paused catch-up interval, all 103 other modData entries, including 102 RLO entries, remained unchanged. Bridge-visible money, transactions, employees, vehicles, Food Trucks stock/orders and ledger fields also remained unchanged while the background state advanced. The new character owned no businesses or inventory, so this is limited fixture evidence, not complete economy non-interference coverage.

The one-day own payload was 921,200 UTF-8 bytes. With Persistent City configured off, the native game loaded and saved successfully and retained the own payload exactly. Full removal and final rig restoration are recorded in the completion note below.

## Follow-up panel

| Reviewer | Model | Verdict on bounded production change |
| --- | --- | --- |
| Astra | `gpt-6-astra` | APPROVE; no actionable findings |
| Sol | `gpt-6.1-sol` | APPROVE; no actionable findings |
| Grok | requested `grok-4.7`, provider `grok-4.7-build` | APPROVE; code correctness for this milestone |
| Opus | `claude-opus-5-5` | APPROVE; no findings |

Reviewers examined the new runtime loop, unchanged `AdvanceTo` contract, failure/save behavior and timing claims. They agreed that bounded repeated one-event calls preserve chronological order and partial-save validity, that the event cap is sixteen, and that the two-millisecond budget is soft. Findings were evaluated against code and live/offline evidence; agreement was not substituted for testing. The reviews did not certify the later performance measurements or a public release. No further code fix was requested.

Only `Runtime/PersistentCityMod.cs` changed in production after the original approved snapshot. README budget wording changed with it; validation documentation was then updated with observed evidence. All 114 existing tests passed, and compilation against the exact Mac managed assemblies succeeded with warnings treated as errors. The additional recorded-route/reload comparators are local test tooling, not production dependencies.

Tested final DLL SHA256: `1d68651cecefdc7f08edee5ccd349a81851ad426285012b3626273f6fbc4c902`.

The adjacent `2026-10-19-mac-manifest.json` records the final source/evidence file hashes and reviewed runtime hash. The original panel manifest remains a historical snapshot rather than being overwritten.

## Retained evidence and remaining gates

Ignored local evidence is under `build/mac-validation/`: original/current package receipts, native bridge states, full probe snapshots, baseline/improved logs and timelines, exact reload replay, and classified full-day differences. Reviewer transcripts are under `/tmp/persistent-city-panel-20261019/round4-*`. Raw saves and game/RLO binaries remain local and are not committed.

Remaining gates are listed in CHECKS.md: broad navigation/accessibility, meaningful capacities and reachable assignments, populated economy scenarios, corrupt/future saves and dependency-loss behavior inside the game, initialization/save/allocation costs, longer soaks and Windows compatibility. Visible citizens, identity-preserving actor pooling, transport integration and the fun of the final experience remain unimplemented/unverified.

## Completion note

Persistent City and its diagnostic helper were removed while RLO remained installed. The native game loaded the saved character, advanced from day 2 08:39 to 08:41 and saved successfully. The rig was then stopped. RLO and all added test packages were parked outside ModsLocal, the original installation manifest was restored byte-for-byte, all original manifest hashes passed, and all eleven preexisting save files were unchanged. Only the original Food Trucks and Driven Rides packages remain installed. Ownership was released with zero pending work; the final status confirms the rig is stopped and unreserved. New disposable FT saves and ignored evidence remain available for follow-up testing. No deployment, publication, commit or Windows test was performed.
