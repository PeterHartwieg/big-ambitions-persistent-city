# pc-stop-rate result

Status: done

Branch: codex/pc-optional-stops
PR: https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/3
PR #3 was updated in place and verified draft, targeting codex/pc-decisions-1. No new PR.
Commits: d1bbb9a (implementation/tests), 0fe51c8 (decision/configuration/evidence documentation). Both pushed normally.

## Configuration

- Exact key: `OptionalStopChoicePercentage`.
- Section: root/top-level JSON object (no named nested section).
- Runtime file: `Path.Combine(context.ModRootPath, "persistent-city.json")`, i.e. `<ModContext.ModRootPath>\persistent-city.json`; standard documented installation-relative path is `ModsLocal\PersistentCity\persistent-city.json`.
- Exact source file: `C:\Users\Peter\Coding_Projects\big-ambitions\big-ambitions-persistent-city-worktrees\pc-stops\config\persistent-city.json`.
- Exact generated package file: `C:\Users\Peter\Coding_Projects\big-ambitions\big-ambitions-persistent-city-worktrees\pc-stops\build\PersistentCity\persistent-city.json`.
- Default 25; integer range 0-100 inclusive. Numeric out-of-range values clamp; missing/non-numeric/null/boolean/object/array or in-range fractional values use 25. Numeric strings accepted. Malformed JSON retains the existing startup failure behavior.
- Edit with city unloaded and reload the mod/city. No live watcher or QA decision-reset command. Only new chances use the changed setting. Existing accepted/declined decisions, including waiting conversation partners, retain their saved outcome. Active stops retain original deadlines even at rate 0.

## Changes

Set OptionalStopPolicy.ApprovedChoicePercentage to 25 and use the existing JSON settings mechanism, extracted into its own Runtime source file for direct unit coverage. No new configuration framework.

Fixed conversation recruitment re-evaluating an older visit with the current rate. Schema 16 now stores nullable OptionalStopChoice with event copy/commit/reset support. Strict migration accepts schemas 13-15; unknown legacy visit choices stay null and cannot be inferred/recruited, while schema-15 stops remain intact. No financial changes.

Recorded DECISIONS-3 beside DECISIONS-2 and documented QA config and migration in README, stationary-citizens and CHECKS. One fixed chance per existing visit, at most one audience chance per journey, mid-shift worker stops; no minimum crowd or extra outings.

## Commands and results

- `git pull --ff-only origin codex/pc-optional-stops`: already up to date; branch/worktree initially clean.
- `dotnet test tests/PersistentCity.Tests.csproj --no-restore`: 1,360 passed, 0 failed/skipped. Initial migration field-count/legacy-fixture and event-copy fixture failures were corrected; final full suite passes.
- `python tools/build.py --game-managed "C:/Program Files (x86)/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed"`: passed with warnings as errors, read-only assembly references, output only in this worktree. DLL SHA-256: 8c14477ebc239957dca743a052724c68417c54b924fdc6afc68ca8df1eb7dc63.
- `python tools/optional-stop-regression/run.py`: 179 checks passed.
- `python build/pc-stops/run-harnesses.py`: all 21 standalone regression projects passed (each runs `dotnet run --project tools/<name>-regression/<project>.csproj -c Release`), including startup-clock, save-contract, traffic and routing checks.
- `dotnet run --project tools/simulate/Simulate.csproj -- 28 build/pc-stop-rate-demo`: passed; exact schema-16 save round trip.
- `git diff --check`: passed.
- `git push -u origin codex/pc-optional-stops`: successful, no force push.
- `gh pr edit 3 --body-file build/pc-stop-rate-pr-body.md`: successful; `gh pr view 3 --json url,isDraft,headRefName,baseRefName` confirms draft and expected branches.

Environment: rg and dotnet were absent from the initial PATH. Used PowerShell/git search instead of rg and prepended the existing `C:/Users/Peter/AppData/Local/Microsoft/dotnet` for test/build commands. No installation or persistent environment change. Logs are ignored under build/pc-stop-rate-*; harness detail logs are in build/pc-stops/.

## Open questions and next steps

No open product question for this task. Independent reviewers can review the two pushed commits in PR #3. Native geometry, rendered stop behavior, weather and performance remain unverified; a separately authorized disposable-city QA run is the next validation step. General mid-route audience interception and kind-specific animation clips remain outside the implemented claim.

No game launch, deployment, game/profile/ModsLocal writes, release, RLO changes, other-worktree changes, new PR, force push, reviewers/subagents or Grok invocation. RESULT.md is intentionally uncommitted; identical copy written to the requested handoff path.
