# Sync A1/A2 into approved E2b

PR #28 branched before A1/A2 were squash-merged into `codex/persistent-city`. Merge fetched development base `8b05227` into approved E2b `8aec702`, with common ancestor `9b1b89c`, preserving both parents. This sync targets `codex/pc-e2b-street-life` and must land with a **merge commit** so PR #28 inherits that ancestry. The harness handles pushing and PR publication; E2b's locked-PC rig proof follows on the synced head before PR #28 is squash-merged into the development base.

This is conflict resolution and integration only. Keep A1/A2 and their sync fixes, F0, D1, C1 and E2a, together with all approved E2b frontage ownership refusals, bound-line planning/move-up through rain, per-kind dwell maxima, tests and evidence. Review for dropped content, broken migrations and incorrect integration. The separate rig run follows this sync.

Both parents' diffs against the merge base were inspected for every conflicted file. All four conflicts are resolved as follows:

| Conflict | Resolution |
| --- | --- |
| `CHECKS.md` | Concatenate the complete E2b implementation and both review records with the complete A1/A2 implementation, review, sync, migration and performance records. Preserve all shared history, native checklists and LOCKED-OK/NEEDS-INPUT markers, including E2b's seven numbered **In-game checks for 6 Nov rig**. Numbering stays local to each slice. |
| `README.md` | Keep E2b's notice alongside both A1/A2 notices and every existing slice notice/link. Retain A2's schema-17 and schema-13/14/15/16 migration statements. |
| `docs/roadmap.md` | Retain every E2b and A2 performance row, E2b status/link, shared F0/D1/C1/E2a status and all standing performance rules. |
| `tools/optional-stop-regression/run.py` | Keep E2b's conditional `PERFORMANCE` compilation constant, source extraction, argument forwarding, findings and performance arms. Retain A1/A2's PATH/per-user SDK fallback and use the resolved SDK for both subprocess invocations. |

The shared clean code merges retain A1's inspector lifecycle/projection hooks, A2's immutable trait event copies, and E2b's timing propagation and committed queue planning. The optional-stop tests retain A2's legacy-schema fixture conversion and current-schema expectation alongside E2b's default-save/tuned-reload assertions. **No E2b runtime code was manually changed; no additional production behavior change, integration fix or performance optimization was needed.** The launcher resolution is the only manual executable-code edit.

Population **schema 17 remains A2's existing contract**. `Persistence/SaveCodec.cs`, `Core/Model.cs`, `Core/Population.cs` and `tests/Schema17Tests.cs` match the merged A2 parent exactly. E2b adds no schema, persisted field or migration. Its queue reload reconstruction limits remain documented. Existing schema-13/14/15/16 migration, interrupted meals/journeys, exact money and refuse-and-preserve coverage remain intact.

Preservation checks confirm all 15 E2b-only changed files and all 33 development-base-only changed files match their respective parents exactly. The four shared code/test files are exact clean three-way merges. Both complete CHECKS/roadmap documents remain subsequences of the result; every added README line from either parent remains. All test method declarations in either parent's changed test files survive. This also retains both plans, config template, performance evidence and regression/project inputs.

All **32/32 required offline commands pass**, sequentially in the foreground:

- Production build against the supplied read-only Steam Managed references, warnings as errors. DLL SHA-256 `e16d0b1f99ae16180cdbf4bb6effe6ec39fa8766cb6151fdccb4e2e87957d833`.
- **1,833/1,833 unit tests**, zero failed/skipped, plus separate `--no-build --list-tests` discovery.
- All **22 Release regression projects**: core identity, diagnostic response, event observation, food catalogue, food checkout, food hours, food household catalogue, food ownership, food supply, frame observation, gate access, road ceiling, road handoff, road invalidation, road speed, route discovery, save contract, startup clock, traffic closure, traffic queue, walking quote and campaign reader.
- All four Python harnesses: native command, physical selection, vehicle ownership and optional stop.
- All three demos with the prescribed arguments: 28-day simulate, 957-resident food routines and food demand.

Two additional integration checks pass (**34/34 including extras**): optional-stop `--performance --findings` exercises both merged launcher invocations, and `tools/save-contract-regression/compare.py` compares tracked A2 sources from `8b05227` with the merged candidate. The comparator confirms **identical save bytes**, refusal outcomes, orders, money and reload economy: **849 orders, 797 purchases, two unconfirmed**. Schema-17 save-contract digest remains `7B95E73969CAFBE2FB1BD964258D74638061BA5C3C57DD04827A5CD3D4F333D9` (119,027 characters). F0's twelve off/on/hidden/save/reload byte-equality transitions also pass.

The prescribed `wait-other-dotnet.ps1` guard ran successfully before every command, including build and Python harnesses. No execution deadline was set. Child environments disabled MSBuild/compiler-server reuse. All owned test/run processes completed; no background test, watcher or server remains. [Command exits, source audit and full optional-stop findings/performance output](offline/2026-10-31-e2b-sync-base.txt); full ignored logs and `results-final.json`: `build/e2b-sync-base-gate/`. `git diff --check` passes.

**Offline timing flags:** D1's 20,000-place aggregate cold build is **38.8589 ms across bounded units**. C1's 1,024-business synchronous cold p95 is **7.0536 ms** (warm **2.2100 ms**, one-business continuation **0.0019 ms**; zero sampled spikes >=30 ms). Optional-stop 957-resident simultaneous-admission default p95 is **8.2633/11.7493 ms** at 25/100%, tuned p95 **5.8726/17.1885 ms**, with **zero >=30 ms samples** across all optional-stop cache/facade/admission/selector arms. These are synthetic batch/read costs, separate from native mod-work or whole-frame evidence. No performance-affecting code change is introduced by this sync.

Native performance practice remains combined mod-work **p95 <5 ms**, every spike **>=30 ms** flagged, and whole-frame cost tracked separately. Physical population, closure refresh, optional-stop refresh and serialization rewrites stay parked. No new performance win is claimed. Dated parent timings remain historical offline evidence.

No game launch, rig/test profile/bridge lease access, ordinary-save deployment, release, Steam/Workshop mutation, WSL use, push or PR publication occurs in this task. The separate candidate-head `rig_test` gate remains required for E2b's later merge/release; the existing full checklists and input-only markers remain the handoff.
