# Sync approved C2a with E3 and E2b

PR #24 conflicted after E3 donut feasibility (#23, `3f7b885`) and E2b street-life timing (#28, `193c81d`) landed. This branch merges development head `193c81d` into approved C2a `0aa41ff` with two-parent merge `09f6457` (merge base `81472e6`). Target this sync at `codex/pc-c2a-core-awareness` and land with a merge commit to preserve ancestry. The harness handles pushing and PR creation.

## Conflict resolutions

Both sides' diffs for every conflicted file were read against `81472e6` before resolution.

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep C2a's single index line and E2b's single index line. Retain the automatically merged E3 index line. All checks stay in the existing slice files; both complete C2a in-game checklists and LOCKED-OK / NEEDS-INPUT markers remain intact. |
| `Core/CitySimulator.cs` | Combine the two replacement attachments: retain E2b's `stopTiming` argument and all C2a food awareness settings/providers. |
| `docs/roadmap.md` | Keep both C2a and E2b perf index lines, the automatically merged E3 perf line, and C2a's status/contract link. Existing per-slice perf evidence remains intact. |

The other shared files merged automatically: `Runtime/PersistentCityMod.cs`, `Runtime/PrototypeSettings.cs`, and `config/persistent-city.json` retain both awareness settings/attachment/diagnostics and street-life timing configuration/reporting/attachment. README and existing slice check/perf files needed no conflict resolution.

## Behavior and preservation

Relative to approved C2a, the only hand-resolved Core change passes E2b's timing settings when replacing the simulator. Other Core/Runtime changes are the incoming approved E2b implementation and E3's isolated reader. **No C2a awareness or B1/B2 choice, memory, loyalty, inspector or regulars implementation was changed.** The sync #48 Sol-r1-1 failure-only memory admission/eviction fix and its regression cases are byte-identical to `0aa41ff`. No combination-only build/test fix was needed.

Development head still declares schema **18**, so C2a retains **19** without a version collision. All persistence sources are unchanged from approved C2a, including the frozen schema-18 reader, 18-to-19 next-day migration, populated B1 payload validation, held replay, record accounting and save-size limits. Awareness remains local/routine plus positive daily discovery and bounded positive B1 memory through the same eight-shop shortlist; campaigns remain unknown/unused and geometry replacement yields in 64-item phases.

The merge preservation audit verified all **45 C2a-only** changed files and **41 base-only** changed files byte-for-byte against their respective parents. The six shared paths are the three conflict rows and three automatic merges above. E3's reader, regression executable and source guard pins are unchanged from the base; its history-independent source guards remain applicable to this combined tree. E1 is untouched.

## Validation

**47/47 gates pass**, with **2,293/2,293 unit tests** (zero skipped) and **2,293 discovered cases**. Results are recorded in [the sync manifest](2026-11-01-c2a-sync2-base-results.json) and [C2a checks](../checks/c2a-core-awareness.md). All commands run sequentially in the foreground, without execution timeouts, with the required other-dotnet wait before each gate and shared compilation/build-server reuse disabled.

The gate includes the required game-assembly build, all 32 regression executables (including pickup continuity), four Python harnesses, three requested simulations, E3 source/isolation guards and four full B1-memory/table capacity runs. Sizes and record counts exactly match approved C2a: reference **5,022,655 bytes / 54,584 streaming records**, 5,000 consumers **9,710,819 / 110,132**, CF32 **192,029,621 / 1,920,132**, CF64 **230,429,621 / 3,520,132**. CF32/CF64 future envelopes remain **220,664,366 / 259,064,366 bytes** and **2,111,867 / 3,711,867 records**. All unchanged hard gates pass; CF64 retains its existing >95% warning. No save/record cap was enlarged.

The unchanged synchronous awareness benchmark reports **two >=30 ms complete-build samples**, max **33.1144 ms**. All capacity write/read and successful legacy migration samples are also >=30 ms and are flagged separately in the slice checks/perf file. These are offline build/save/startup costs, separate from yielded production work and native mod-work/whole-frame timing. Full data, source hashes and command/log hashes are embedded in the manifest; [the preservation audit](2026-11-01-c2a-sync2-base-preservation.json) pins both parents' one-sided file blobs. Game-target DLL SHA-256: `e0e5131f85676495144ac8d988989c27b7d0c989ea41b6e103e0fcea9795cfae`.

No game, rig, `C:\BigAmbitions-AgentTests`, bridge lease, Steam/Workshop, ordinary profile or WSL distro was used. This is offline integration evidence. Under merge rule v2, in-game checks belong to the later batched combined-head locked rig; pointer/input-only checks remain for Peter's 6 November playtest. This work grants no release/upload/deployment clearance. Mod-work p95 <5 ms, >=30 ms spike reporting, autosave discipline, separate whole-frame tracking and the four parked hotspots remain unchanged; no performance win or native timing claim is made.
