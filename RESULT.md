# persistent-city-pc-advance result

Status: partial
Date: 2026-10-28
Branch: codex/pc-advance-perf
Baseline: 63248e4
PR: https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/1 (draft; base codex/persistent-city)

## Delivered

The requested design-question file was written before any source change:
C:/Users/Peter/.codex/handoffs/leftover-2026-10-28/persistent-city-DESIGN-QUESTIONS.md
Seven numbered questions retain context, options/trade-offs, recommendations and blockers. No open gameplay/accounting/schema decision was implemented.

- Opt-in audit helper: per-frame slowest event slices, citizen and entry branch, Plan/Begin count, native NavMesh timer delta, A-star Advance and broader walking/road timings; separate fixed-step/intersection mod-hook bodies; allocation delta or explicitly named signed heap delta. Analyzer retains the new context. Actual diagnostic patches have an offline capture on/off comparison.
- Traffic queues hold owned vehicle references, avoiding repeated string lookups and the second curb-exit queue search. Forecast queues reference detached copies; captures retain identical ordered IDs. This is part of finding 4, not an O(1) snapshot implementation.
- SaveCodec shares type-only contract metadata while retaining fresh settings, all validation, strict reads and identical bytes. This is part of finding 5, not a solution to O(city) main-thread saves.
- Three dated review reports, harness READMEs, README and CHECKS updates.

## Commands and results

The initial shell lacked dotnet on PATH and BA_GAME_MANAGED was unset. Used the existing SDK at C:/Users/Peter/AppData/Local/Microsoft/dotnet, with DOTNET_CLI_HOME and NUGET_PACKAGES under ignored build/pc-advance. Set BA_GAME_MANAGED for each command to the supplied Steam Managed path. Assemblies were referenced read-only in place. No SDK installation, game DLL copy, RLO modification or credential read was needed. An intermediate traffic refactor compilation error was fixed before final validation.

- dotnet test tests/PersistentCity.Tests.csproj: final 1,267 passed, 0 failed, 0 skipped (29 seconds reported by runner).
- python tools/event-observation-regression/compare.py --baseline build/pc-advance/baseline --output build/pc-advance/event-comparison: passed against 63248e4; actual Harmony patches preserve 680 activity events and exact save bytes with capture off/on and injected failure. Runtime/Unity doubles; no native installation proof.
- python tools/traffic-queue-regression/compare.py --baseline build/pc-advance/baseline --output build/pc-advance/traffic-comparison: 32,414 observations match; SHA256 D328799A130E4BF1834A448B46765F4D5BC1750A21DF86B879B42B97307ABAFE.
- python tools/save-contract-regression/compare.py --baseline build/pc-advance/baseline --output build/pc-advance/save-comparison: save bytes and all refusal outcomes match. Twenty warmed writes allocate 36,369,304 to 21,794,064 bytes; twenty reads 78,218,592 to 61,925,608 bytes (.NET 10 fixture only).
- python tools/core-identity-regression/compare.py --baseline build/pc-advance/baseline --output build/pc-advance/core-comparison: 38,924 activity events, traffic/request/probe digests and seven save hashes match, including mid-commute reload. No aggregate allocation improvement claimed.
- dotnet run --project tools/road-ceiling-regression -c Release: 69 + 142 sliced checks pass.
- dotnet run --project tools/road-handoff-regression -c Release: all sections pass.
- dotnet run --project tools/traffic-closure-regression -c Release: 35 checks pass.
- dotnet run --project tools/frame-observation-regression -c Release: 38 checks pass.
- dotnet run --project tools/diagnostic-response-regression -c Release: 14 invariants pass.
- python -m unittest discover -s tools/food-demand-audit -p test_analyze_stalls.py: 8 tests pass (rerun after final analyzer edit).
- python tools/build.py --game-managed "$env:BA_GAME_MANAGED": passes with warnings as errors. DLL SHA256 222008b91283e2765747e1dbdf393efa6754b62a93fa1a50b613b87ceb2191ff.
- PC_FOOD_CAMPAIGN=pc-advance/helper and PC_FOOD_SOURCE=<this worktree>; python tools/food-demand-audit/build_probe.py --game-managed "$env:BA_GAME_MANAGED": passes. Helper SHA256 c0eeea6e5f2843348979ca71f578155094f6722eb803446d8b8e60f1b0a88644.
- git diff --check: passed.
- git push -u origin codex/pc-advance-perf: succeeded; no other branch pushed.
- gh pr create --draft --base codex/persistent-city --head codex/pc-advance-perf: draft PR #1 created.

Commits: 5598e33 diagnostics; 0f33f6f traffic references; 43ce63b save metadata; e30ef96 evidence/boundaries.
Local ignored logs, baseline sources and comparison outputs: build/pc-advance/.

## Open questions and remaining work

Peter's design questions cover hour/rain visibility versus routines, new dwells/audiences/night density, fractional native price accounting, walking invalidation guarantees, sliced RLO consistency/detection latency, priority/save-validation policy and inherited one-minute food-cache freshness. Recommendations in the design file are not decisions.

Findings 8 and 9 remain unchanged: gate equivalence alone does not establish NavMesh equivalence, and slicing a live RLO catalogue needs a coherent snapshot/revision and explicit detection contract. No changed save validation or schema is proposed. Traffic snapshots remain O(queue), projection kernels still allocate, and the synchronous save/RLO workload remains. Event labels describe entry branches, not every internal Step branch. Food/traffic diagnostic attribution and Unity/Mono hook installation/overhead still require native verification. No per-game-frame speedup is established. The fractional-cent financial-conservation failure remains a release blocker.

Suggested next steps: Peter answers the design file; review these three bounded changes; complete internal Step branch tracing if needed by the next measurement; then separately authorize native validation under the autosave exclusion protocol. Use capture on/off overhead checks and matched helper versions. Consider full traffic snapshot caching only with comprehensive invalidation and differential evidence.

No game launched; no game/profile/ModsLocal/rig writes, RLO edits, deployment, release, version bump or Workshop action. Local build outputs are not release packaging. RESULT.md is deliberately uncommitted and copied to the requested handoff location.
