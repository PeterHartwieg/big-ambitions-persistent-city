# Bounded implementation — 24 October 2026

The schema-13 / 0.10.0 development candidate implements the agreed simplified scope. It is **not release-ready: measured frame pacing, startup and time-skip waits fail the acceptance gates**. No ordinary-save deployment, Windows work, RLO source change or publication was performed.

## Implemented scope

- Stable residents, home/work/leisure assignments and persistent car ownership remain the foundation. Daily, deterministic job-loss/job-finding chances replace resident turnover and career schedules. Changes occur at safe routine boundaries and affect future income postings.
- Each physical resident represents two independently budgeted consumers, including the original. Additional consumers use bounded saved walking excursions, without extra bodies, vehicles or full daily itineraries. Native checkout consumes one product, bag and service slot per purchase.
- Eligible player food businesses are managed automatically as whole businesses. Unsupported menus retain native ownership. Empty shelves and absent staff remain operating consequences; uncertain receipts retain their ownership and cannot be blindly retried.
- The package enables the mod at one-third calendar pace. Balance values remain developer controls. Current-format saves preserve identities, journeys, wallets and receipts; unsupported payloads are refused unchanged.
- Route reuse retains current access checks. Native testing identified and corrected startup clock initialization, overly restrictive walking validation and return-route hash changes after reload. A validated replacement return must still fit the original opportunity; existing unresolved journeys are not silently repaired.

## Verification and economic result

The integrated Core/Persistence suite passes **1,200 tests**. Actual-source checks cover startup (19), walking validation (28), ownership (78), catalogue (29), supply (19) and route reuse/invalidation (265). The production assembly compiles with warnings as errors. These are separate from game evidence.

Native traffic evidence includes the same identifiable rendered car after leaving and returning to view, in-flight save/reload, and logical car pickup after subway travel. The latter does not prove a rendered parking manoeuvre. Ownership evidence covers native reset-driven identity change and supported/unsupported menu transitions; an actual business sale through the ordinary UI remains unproven.

The corrected K2 return replay passes **1,925 checks**, and its completed Day 4 accounting passes **1,945**, with 30 purchases and no stranded returns. Day 5 passes **1,944 checks** across 957 physical residents and 1,914 consumers: **122 purchases / $1,250.50**, with no held funds, uncertain checkouts or unfinished excursions.

| Completed Day 5 | Seventh Street 8 | Fourth Avenue 20 |
|---|---:|---:|
| Building / equipped capacity | 75 / 40 | 30 / 30 |
| Purchases | 30 | 92 |
| Peak purchases/hour | 11 | **18** |
| Remaining croissants | 0 | 28 |
| Native operating result | −$1,737.59 | −$438.78 |

This establishes useful demand above a 15/hour small shop at the favorable location, without increasing physical population. It does not establish demand for 75/hour or profitability. Seventh Street sold out. K1 recorded 76 combined purchases and a Fourth Avenue peak of 12/hour, but its prior spending, stock and supplier prices differed: the comparison is descriptive, not a fully matched causal estimate.

The first Day 5 capture stopped when a diagnostic observer threw at its 5,000-record limit. It remains marked invalid. The completed replay uses two shorter recording windows and reconciles full-day ledgers independently of those traces. A separate helper-only fix now truncates safely, reports dropped records and detaches observers at audit end; it compiles and passes 15 focused checks, but was not substituted into the native replay.

The planned price-only branch could not run: the native game reported its reload of the same Day 5 checkpoint as failed after about five minutes. No price change executed. The exact checkpoint and completed baseline were preserved; this comparison remains open rather than receiving an inferred result.

## Remaining gate

The final candidate's car sample, recorded before any food-audit tracing, measured combined mod p95 **31.6292 ms**, maximum **517.9501 ms**, versus the unchanged 5 ms target. Whole-frame p95 was **359.1553 ms**. Large measured calls include population advancement and physical population updates; the evidence does not isolate a single route/event or establish machine contention as the cause. Earlier K2 measurements also miss the frozen wait ceilings: fresh initialization 222.426 s versus 112.158 s, supported in-flight reload 162.970 s versus 125.651 s, and an ordinary one-day skip 133.822 s versus 77.466 s. These are different earlier K2 captures, not timings of the completed split-window replay. Private inputs are `ownership-k2-native-20261024-r1/k2-fresh-load-timing.json`, `k2-one-day-timing.json`, and `ownership-k2-native-20261024-r2/k2-inflight-reload-timing.json`; the frozen criteria are retained in `build/execution-20261024/`. Functional success does not clear these failures.

The next implementation work should isolate the expensive population/route work and make a small measured correction, then repeat the affected acceptance window. Keep K=2 and the present category scope. Do not add K=3, a Rust rewrite, more economic systems or more player settings to compensate. Peter's short hands-on pacing and management check remains necessary before release.

Private detailed evidence and the exact-byte development package are under `build/execution-20261024/`; raw saves and game assemblies remain excluded from Git. [Current validation](../checks/history-2026-10-development.md#current-implementation--24-october-2026) and the [agreed plan](../implementation-plan.md) distinguish completed implementation from the remaining release gates.

Final cleanup verified all **4,113 pre-existing Mac rig files**, preferences and the installation manifest. The game is stopped, diagnostic helper removed, and ownership released with zero pending commands. The completed new named baseline save remains available only in the isolated profile; the common checkpoint is preserved privately for the unfinished price branch.
