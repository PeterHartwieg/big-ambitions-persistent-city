# E3 ice cream: merge latest development base

PR #62 conflicted after A5 landed. This integration merges `origin/codex/persistent-city` at `0ffb819` into the approved ice cream head `89111ef`, retaining real two-parent ancestry. Target: `codex/pc-e3-icecream`; land this sync with a merge commit.

## Conflict resolution and preservation

- `README.md` (the only textual conflict): retain both the E3 ice cream feasibility STOP paragraph and A5's offline acceptance / pending native-proof paragraph. Retain A5's schema-20 documentation correction.
- `CHECKS.md` (automatic merge): retain both A5 and ice cream index entries, one per slice. Ice cream's evidence and complete eight-item **In-game checks for 6 Nov rig** section, including **LOCKED-OK** / **NEEDS-INPUT** qualifications, stay in `docs/checks/e3-icecream.md`.
- `docs/roadmap.md` (automatic merge): retain A5's status and perf-log entry alongside ice cream's feasibility status. Preserve the shared performance gates and all existing perf entries.
- All other A5 files, including acceptance tests, source optimization, regression tools, checks, plan status and performance/measurement reports, are retained unchanged from the incoming base. All approved ice cream files are retained, with sync evidence added only to its owning checks file and separate review documents. No other files conflicted.

## Integration scope

No E3 ice cream or shared-reader code changed during this sync. No build/test integration fix was required. Exact source pins remain valid without re-pinning; the explicit whole-tree audit uses the incoming `0ffb819` head rather than freezing the earlier base. Donut, cupcake and salad facades, cases, read branches and refusal contracts remain identical under the existing exact-hash guards. No cross-candidate substitution was introduced.

Persistence and schema remain exactly the incoming schema-20 implementation; this PR claims no new schema or saved fields. The only production source change relative to the old ice cream head is A5's already-landed `Core/ResidentInsight.cs` address-format reuse.

## Validation

**58/58 foreground gates passed:** production build against local game assemblies; 2,392 unit tests (zero failed/skipped) and 2,392 discovered tests; every top-level regression project; native-command, physical-selection, vehicle-ownership and optional-stop Python regressions; continuity pickup; optional-stop performance/findings; simulate, food-routines and food-demand demonstrations; donut/salad/ice cream source guards; frozen reader baselines; and five inherited A5 rush/integrated/save benchmark modes. The explicit whole-tree audit preserves 543 incoming source/test/tool/config blobs. Reader assertion counts remain 2,082 / 2,098 / 2,119 / 2,127, and the schema-20 save digest remains unchanged.

Full command/exit records, log hashes, preservation checks and scoped offline diagnostics are linked from `docs/checks/e3-icecream.md`. The prescribed other-dotnet guard precedes each dotnet test/run, including commands invoked by Python harnesses. No game, rig, bridge lease, test profile, Steam/Workshop or WSL operations were performed.

Under merge rule v2, native checks wait for the batched combined locked rig on the development head; pointer/input checks wait for Peter's 6 Nov playtest. No new performance win or native performance pass is claimed. Existing mod-work p95 <5 ms, >=30 ms spike reporting, separate whole-frame tracking and the four parked hotspots remain in force.
