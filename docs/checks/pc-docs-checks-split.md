# pc-docs-checks-split: documentation layout

## Structural change — 1 November 2026 (offline only)

CHECKS.md indexes the owning slice/topic files. The README's dated opening notices and the roadmap's standalone B0/C3/E2a notices now live with their slice checks. The roadmap indexes six per-slice performance files; its shared performance gate and normal planning prose remain in place. AGENTS.md requires future validation, notices, sync/review-fix notes and performance entries to stay with their slice.

The source snapshot is `c6d7535411996f757783f5122f1aed46205ccc63`. The split moves 157 original blocks: 101 CHECKS sections, 31 README notice blocks and 25 roadmap entries (three notices and 22 performance rows). Their non-link text is unchanged, each block occurs exactly once in its destination, and each source's order within a topic is preserved. Only relative Markdown link targets change to account for the move or to reach the new section anchors. Normal prose documents receive only those link repairs.

All seven **In-game checks for 6 Nov rig** sections remain, including all 21 `LOCKED-OK`, 12 `NEEDS-INPUT` and four `input-only` occurrences from the original CHECKS.md. Historical counts and claims in the moved text retain their original context.

## Validation

The ignored `build/docs-split-manifest.json` records every original block, source range, destination and link edit. The ignored `build/verify_docs_split.py` checks full source coverage, one occurrence per moved block, unchanged non-link text, ordering, checklist markers, performance rows, complete indexes, local files and Markdown heading anchors. The source snapshots also allow the README's remaining prose and roadmap performance gate to be compared exactly.

Preservation audit: PASS, including 188 repaired link targets and all 279 local links in affected documents (zero missing files/headings). The indexes cover 12 checks files and six performance files. Each checks file links back to the index. `git diff --cached --check` passes, and all 35 staged files are Markdown.

Repository-wide consumer search found that `tools/build.py` and `tools/food-ownership-regression/prepare_candidate.py` copy the root README as a file; no tool/test parses its sections or CHECKS.md. Historical portable-bundle validators read their own bundle READMEs, which are untouched. No executable consumer change was needed. `rg` is unavailable on this machine; `git grep` supplied the search.

Required Steam-reference build: PASS. DLL SHA-256 `a28fb061a2b2fa2857b4521ffdae8076d54466997ff3547f360a37d4e116261b` matches the existing C3 final-head evidence. `dotnet test tests/PersistentCity.Tests.csproj`: **1,972 passed, zero failed/skipped**. `dotnet test tests/PersistentCity.Tests.csproj --no-build --list-tests`: PASS, **1,972 discovered cases**. Both use `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`. Output is retained in ignored `build/docs-split-*.log`; the prescribed other-dotnet guard completed before each build/test invocation, in the foreground with server reuse disabled. All invoked processes completed; no owned test, watcher or server remains.

Docs only: no game behavior, test, configuration or schema changes; no rig needed under merge rule v2. No game, test profile, bridge lease, Steam or Workshop access. No performance impact is expected. Mod-work p95 below 5 ms, reporting spikes of 30 ms or more, measured cheap wins, the four parked hotspots and separate whole-frame tracking remain unchanged.

[Checks index](../../CHECKS.md).
