# Sync the F0 base into the D1 rival reader

PR #10 conflicts with `codex/persistent-city` after F0 (PR #8, squash `7a344a7`). This sync merges that base into `codex/pc-d1-sync-base` with a real two-parent merge: D1 `487f2e9` is the first parent and F0 `7a344a7` is the second. The common ancestor is `6d43276`. Land this sync into `codex/pc-d1-rival-reader` with a **merge commit**, preserving the base ancestry; PR #10 can then be squash-merged into `codex/persistent-city` as planned.

Both parents' diffs against the common ancestor were read before resolving the three documentation conflicts:

- `CHECKS.md`: keep the complete D1 catalogue evidence, identity/performance limits and five numbered **In-game checks for 6 Nov rig**, including all **LOCKED-OK** and **NEEDS-INPUT** markers. Follow these with every F0 review/fix/validation/performance section and its separate seven numbered in-game checks. Each checklist remains under its original slice's section; no checks are removed or combined.
- `README.md`: keep both the D1 catalogue notice and the F0 review/settings notice as separate paragraphs. Retain F0's automatically merged explanation of default indoor travel and the optional interior experiment, plus all prior notices and links.
- `docs/roadmap.md`: keep D1's performance row and both F0 performance rows in one table. Retain the automatically merged Track F status/plan paragraph and all existing roadmap and performance-gate requirements.

Every added or changed nonblank line from both parents' conflicted-file diffs is retained. The six non-conflicting D1 files match `487f2e9` Git blobs exactly; the thirty non-conflicting F0 files match `7a344a7` exactly. Only these three docs overlap between the parents. No source, test, configuration or tool changes beyond the automatic merge; no integration fix was needed. This description is the only additional file.

Existing native proof is reused as directed by this task: D1 has the locked-PC PASS on `487f2e9`, posted on [PR #10](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/10) from `codex/pc-rig-locked-d1`; F0 is unchanged from its rig-proven `7a344a7` squash and remains default off. No new native rig proof is required for this documentation conflict resolution. The parents' dated offline-only/pending-gate statements are preserved as historical records. No game launch, test-rig/profile/lease access, Steam/Workshop mutation, push, PR creation or release/deployment action was performed.

All **31/31 required commands passed** on the merged tree, sequentially in the foreground:

- Production build against the supplied read-only Steam Managed assemblies; DLL SHA-256 `690c46f3f7c1bf06163acbfe5586b7b7b20e7c9f90ed8983d374eea3c85d0b1b`.
- Unit tests: **1,596 passed, zero failed/skipped**; the separate `--no-build --list-tests` command also passed.
- All 21 Release regression projects: core identity, diagnostic response, event observation, food catalogue, food checkout, food hours, food household catalogue, food ownership, food supply, frame observation, gate access, road ceiling, road handoff, road invalidation, road speed, route discovery, save contract, startup clock, traffic closure, traffic queue and walking quote.
- All four Python harnesses: native command, physical selection, vehicle ownership and optional stop.
- All three demos with the requested arguments: 28-day simulate, 957-resident food routines and food demand.

The required `wait-other-dotnet.ps1` guard ran before every command, including Python launchers that invoke dotnet. It waited for another checkout's job before continuing at traffic closure; test loads did not overlap. Build/native-command/physical-selection retained the original PATH. Only the vehicle-ownership and optional-stop child environments added the local SDK directory to PATH, as in the parent gates. Logs, exact command arguments, exit codes and the runner are ignored under `build/d1-sync-base/gates/` (`results.json`).

Focused results: **29 player catalogue +272 D1 Release checks**, **1,057 F0 adapter/lifecycle +17 startup-clock checks**, **1,938 F0 wiring +27,568 physical differential checks**. Save-contract payloads remain identical off/on over twelve transitions, SHA-256 `6b9097a2a036a7791f118ddff0a96530ac4a2ebaffdfa6bf37c51adbcc52062c`. Both documentation preservation checks and `git diff --check` pass.

Offline performance flag retained: the 20,000-place D1 empty-cache build averages **37.1434 ms across all continuation units**, exceeding 30 ms in aggregate; never drain it in one frame. Warm refresh is **0.7380 ms /32 B** and one 16-read unit **0.0020 ms /0 B**. F0's sampled street/interior retention loops report zero samples at least 30 ms; food-routine maximum 16-event batches range **9.2861–20.9776 ms**. These synthetic measurements do not establish combined native mod-work p95 or whole-frame performance. The standing native practice remains mod-work **p95 <5 ms**, every spike **at least 30 ms** flagged, with whole-frame timing reported separately; this sync introduces no performance-affecting edits.
