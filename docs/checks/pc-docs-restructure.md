# pc-docs-restructure: documentation restructure

Status: implemented, 6 November 2026. Docs only, plus the new link checker `tools/docs-links/check.py`. No change to Core, Persistence, Runtime, Compatibility, tests or other tools.

## What changed

- **AGENTS.md** holds the current rules only (under 5k characters): the code map, how to start a slice, the standing rules and the binding safety rules. The October authorizations and the old "implementation-plan.md is the current scope" statement moved verbatim to [2026-10-22-24-authorizations.md](../decisions/2026-10-22-24-authorizations.md). The whole previous AGENTS.md is kept verbatim in [archive/agents-2026-11-05.md](../archive/agents-2026-11-05.md).
- **README.md** is one page with a status table. The old body (behaviour, history, commands, limitations) moved verbatim into [history-2026-10-development.md](history-2026-10-development.md#readme-body-before-the-6-nov-2026-restructure-moved-6-nov-2026); current behaviour is restated in [behaviour.md](../behaviour.md). The schema contradiction (25 in one place, 24 in another) is gone: the current writer is 25.
- **docs/roadmap.md** lists only remaining, deferred and superseded items, the performance gate and the wording rule. Its product decisions moved verbatim to [2026-10-30-roadmap-decisions.md](../decisions/2026-10-30-roadmap-decisions.md).
- **CHECKS.md** is a table sorted by slice ID.
- Every per-slice paragraph from README, roadmap and CHECKS, and the status text of plan rows, moved verbatim into the slice's checks file under "README entry", "Roadmap entry", "CHECKS index entry" or "Plan row status (moved 6 Nov 2026)". Perf-log entries moved into the slice's perf file under "Roadmap perf-log entry (moved 6 Nov 2026)". Link targets were rebased; text is unchanged.
- **Plans** (`docs/plans/`) carry a frozen line at the top. Status text was removed from the F5, F6, F7 and Track F slice rows; plan content and scope are unchanged.
- **docs/decisions/2026-11-06-delivery-rules.md** records Peter's 6 November decisions.
- **docs/glossary.md** and **docs/behaviour.md** are new.
- **docs/archive/** received the plan PR descriptions, finished step plans (step A, B–E, E2, E3, C1, C3, Track F F1 and F1b decisions), `implementation-plan.md`, `population-schema18.md` to `population-schema25.md`, the plans' offline result JSON files, and three historical sections of design.md. Each Markdown file has a one-line banner; only relative link targets changed.
- **docs/design.md** marks its historical sections.

## Checks

- `python tools/docs-links/check.py`: 0 broken relative links. It also passed on the base (`e9995e5`) before any move.
- Preservation script (scratch, not committed): every sentence of 25 or more characters in the base README, roadmap, CHECKS, AGENTS, design.md and the edited plans was searched for in the new Markdown tree, ignoring link targets. The only sentences not found are process instructions replaced by the 6 November rules (the old CHECKS, README, roadmap and AGENTS layout rules), the reworded perf-gate heading, and the plan-row status fragments, which are in the checks files with a row label in front.
- Build: `python tools/build.py --game-managed <Steam Managed>` succeeded (no code change; DLL SHA-256 `678cdb58f58c3c3b79f57d4954b0612a7318a685d36c959d7d742ef22173e80d`).
- `dotnet test tests/PersistentCity.Tests.csproj`: 3,168 passed, 0 failed. No test reads a moved file; the tests that locate the repository root use AGENTS.md, which stays in place.
- `git diff --stat e9995e5..HEAD -- Core Persistence Runtime Compatibility tests tools config ':!tools/docs-links'` is empty.

## Known effects

- `tools/e5a-eligibility-regression/source_checks.py` compares `docs/plans/e5-dynamic-products.md` to an old base; the frozen line changes that file. The regression suites are not run before release.
- A code comment in `Runtime/AgencyPaymentPort.cs` names `docs/plans/c3-agency-payment.md`, now `docs/archive/c3-agency-payment.md`. Code is out of scope for this slice.

- Sizes: README, roadmap, CHECKS and design.md are shorter than before. AGENTS.md grew from 3,930 to 4,527 bytes because it now carries the map and standing rules (limit 5k). Each plan gained the one frozen line. Checks, perf and history files grew by the moved text.

## In-game checks for the next batched rig

None. This slice changes no game behaviour.
