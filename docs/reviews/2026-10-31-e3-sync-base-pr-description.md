# Sync approved E3 donut feasibility with the development base

PR #23's E3 branch predates the merged E2a, A1 and A2 work. Merge `origin/codex/persistent-city` at `8b05227d80e150404669d6d8243537a8acfb688b` into E3 `462a99d8313968a2a1760ac88806b3aed47a0003` on `codex/pc-e3-sync-base`, retaining both parents. Common ancestor: `00d1321b257e70e57829cfef2ca823b4d0e25060`. No rebase, squash or cherry-pick.

This PR targets **`codex/pc-e3-first-product` and must land with a merge commit** so PR #23 retains the imported ancestry. The separate locked-PC proof follows on PR #23 before its squash merge into `codex/persistent-city`. PR #33 / the cupcake branch is outside this sync and untouched.

## Conflict resolutions

Both parents' diffs against the common ancestor were read before resolving all three conflicted files:

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep E3's entire feasibility, offline evidence, refusal/noninterference, performance and numbered 6 Nov in-game checklist, including every LOCKED-OK and NEEDS-INPUT marker. Follow it with all incoming A2/A1 sync, migration, inspector and E2a sections, matrices, tests, checklists and historical evidence. Retain the common C1/D1/F0 and earlier sections once. Add this sync's own dated evidence separately. |
| `README.md` | Keep the E3 status and all incoming A2/A1/E2a status entries before C1. Retain the incoming current schema-17 / migration-from-13-16 statements. Historical milestones remain historical. |
| `docs/roadmap.md` | Keep both E3 and E2a status paragraphs. Keep the E3 performance row and all six incoming A2 performance rows, followed by the unchanged C1/D1/F0 rows and parked-hotspot policy. |

`docs/plans/steps-b-e.md` merged automatically: both the E3 feasibility status and E2a findings remain. No config, production source, tests-project or regression-link conflict occurred. The config template and every incoming tracked file outside the E3 additions/shared documentation remain identical to the incoming parent. E3's candidate inventory, feasibility record, original offline results and original PR handoff remain intact.

## Integration scope

**No E3 runtime code changed. No additional production behavior changes or persistence edits were needed.** Every incoming Core/Persistence/Runtime/Compatibility/test and existing food/save regression blob matches `8b05227`; E3's reader and compiled harness match `462a99d`. Population schema **17**, A2 traits/Today contracts, all migrations and refusal cases are inherited unchanged. E3 claims no schema number or saved fields, enables no additional product and remains an explicitly invoked, read-only diagnostic.

One minimal **tool-only** integration fix was necessary: the original E3 `source_checks.py` correctly failed on incoming `Core/CitySimulator.cs`, because its preservation baseline was the pre-A2 ancestor `00d1321`. Pin its existing-source checks to the actual imported parent `8b05227`, and independently pin E3's reader/project/harness to `462a99d`. Keep all isolation/native-write/caller guards. This admits only the already approved parent changes without weakening the requirement that this sync edits no runtime or persistence code. The original failure is retained in ignored `build/e3-sync-base-gate/source-checks-red.txt`.

## Validation

All **36/36** commands pass: the **32 required checks** (Steam-reference build with warnings as errors, **1,791/1,791 unit tests**, zero failed/skipped, separate test listing, all 22 Release regression projects, four Python harnesses and three demos), E3 reader (**2,082 assertions**), E3 isolation/preservation guard, same-schema save/economy comparison against `8b05227`, and optional-stop `--performance --findings`. The guard verifies **218** incoming blobs and the unchanged approved E3 reader/project/harness. Parent documentation additions were checked for retained content, including checklists, matrices and performance rows.

The save comparator retains identical schema-17 bytes (digest `7B95E73969CAFBE2FB1BD964258D74638061BA5C3C57DD04827A5CD3D4F333D9`, **119,027 characters**), refusals, **849 orders / 797 purchases / two unconfirmed**, money and reload economy. F0's twelve off/on/save/reload transitions pass. Production DLL SHA-256: `46ca8593751d2c69f4c5cd110baec8fa6460ba149a138e9ab8eb412106c8da7b`.

Final merged-tree results are recorded in [CHECKS.md](../checks/e3-donut.md) and the [command/exit/log-hash record](offline/2026-10-31-e3-sync-base.json). Commands run sequentially in the foreground, with the prescribed other-dotnet guard and build/compiler-server reuse disabled. No CLI command timeout is set. Logs and parent-preservation assertions remain in ignored `build/e3-sync-base-gate/`.

Offline timing flags: D1 aggregate cold 20,000-place build **37.2917 ms (>=30 ms)** across bounded units; C1 synchronous cold 1,024-business p95 **6.5558 ms**; optional-stop 957-resident simultaneous admission p95 **9.2365 / 11.8645 ms** at 25/100%, zero >=30 ms samples in all reported optional-stop arms. E3's unchanged reader: one cold read **1.5027 ms / 968 B**, warm 2,000-furniture p50/p95/max **0.0959 / 0.1450 / 1.5351 ms**, **49,104 B/call**, zero >=30 ms samples. These are synthetic reader/batch costs, separate from Unity frames and combined mod-work.

No new performance optimization is taken. Native requirements remain combined mod-work p95 **<5 ms**, every spike **>=30 ms** flagged, whole-frame timing separate and all four broad hotspots parked. Offline timings cannot certify that native gate.

No game launch, rig directory/profile/bridge lease access, Steam/Workshop write, WSL or third-party interior content access, normal-save deployment or release action. Native E3 checks remain pending exactly as described in [the retained in-game checklist](../checks/e3-donut.md#in-game-checks-for-6-nov-rig). No push or PR creation is performed by this session.
