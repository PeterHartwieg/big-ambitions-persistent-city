# Sync E5b with the E5e metadata API base

Target: `codex/pc-e5b-complete-menu`. This branch merges `00506ed` from `codex/persistent-city` into E5b `241e648` with a real two-parent merge; land this sync with a merge commit to retain ancestry.

Conflicts and resolution:

- `CHECKS.md`: retain both complete E5b and E5e index lines and their evidence links.
- `docs/roadmap.md`, Step E: retain E5b's partial/native-incomplete status and E5e's API/precedence status.
- `docs/roadmap.md`, perf wins: retain both slice entries and original measurement qualifications.

README automatically retains both descriptions. Both slice checks, historical receipts, regression tools and rig-proof tooling are retained. E5b's six In-game checks and LOCKED-PC/input-only automation qualifications are preserved; the shared 6 November checklist also includes E5e's three numbered handoffs from its owning checks file.

Minimal code integration fix: **both E5b and E5e Core code changed**. Eight new combined-source cases first failed because E5b's `CompleteFoodMenu.Check` queried only config overrides, bypassing API class metadata. It now calls `FoodProductMetadataResolver.ResolveMealClass`, which shares the full resolver's config > API > defaults expression without computing resident tastes. Coverage includes both Street Food products, all four excluded classes, taste-only config fallback, explicit config override, immutable API publication/removal and independent stock/native refusal. All eight pass after the fix. No additional Runtime behavior was implemented; dynamic native authority and held execution remain refused.

Schema remains **25**. No persistence source, historical migration reader or save-contract regression was changed. Unknown products remain Food by default with existing seeded taste; bundles stay deferred and cart/truck sales remain later roadmap scope.

Validation passed on merge `759045f`:

- `python tools/food-metadata-api-regression/validate.py`: native-reference production builds; **69/69 D3a** and **63/63 D4** matrix commands; three full **2,804/2,804** unit passes, zero failures/skips, and discovery. Every required standalone regression, Python adapter, three demos, E3/E5a guards and the independent two-process taste reference passed.
- E5b `E5bCompleteMenuRegression.csproj`: **1,071 assertions**; actual Runtime checkout regression: **10,648 assertions**. The new combined-admission test records eight failures before the integration fix and eight passes after it.
- E5e `FoodMetadataApiRegression.csproj` Program, `source_checks.py` and `validate.py`: all passed, including the final API probe and unchanged E5a eleven negative isolation mutations.
- `python -m unittest discover -s tools/e5b-native-rig-proof -p "test_*.py" -v`: **16/16** offline mocked Python tests passed.
- Save-contract digest remains `22E3C1955D296E91322F2AD2D950C8B9E181234F3E67825D0DE9A3A2537F3294` at schema 25, 120,279 characters; 849 orders, 797 purchases and two uncertain receipts. `git diff --check` and 54 parent-file preservation comparisons passed.

[Separate sync command/log-hash receipt](../checks/e5b-sync-base-results.json); [owning slice notes](../checks/e5b-complete-menu.md). Historical receipts remain unchanged. Offline timing/spike fields are retained with aggregate/startup qualifications; no new perf win is claimed. Combined native mod-work p95 <5 ms, >=30 ms spike flags, separate whole-frame tracking and the four parked hotspots remain in force.

The final fetch still reports base `00506ed`, already merged. All tests ran in the foreground with the shared dotnet guard and completed. No push, PR creation, game/rig session, profile/lease/Steam change or deployment occurred. In-game checks remain in the batched combined locked-PC session under merge rule v2; this integration does not claim rig proof or release approval.
