# Sync B1 with the merged B0b and development slices

Target: `codex/pc-b1-memory-loyalty-v2`. Land this sync with a **merge commit**;
PR #17 subsequently squash-merges into `codex/persistent-city`. Keep the ancestry
for B2 #19 to sync afterwards.

Merge development `a7ab5bc` into approved B1 `f214cd4`, with common ancestor
`7a6153c`. The original B0/B0b history overlaps their squashed development slices.
Both merge-base diffs are retained under ignored `build/b1-sync-base-gate/`.
Comparing each side with B1's B0b prerequisite `c0af745` separates B1's reviewed
changes from the later base-sync changes. The entire executable tree is audited
against that combination, including automatically merged files.

| Conflicted file | Resolution |
|---|---|
| `CHECKS.md` | Keep the development index; add one B1 line. Move all four B1 checks sections and the complete eight-item 6 Nov checklist into `docs/checks/b1-memory-loyalty.md`, preserving LOCKED-OK / NEEDS-INPUT qualifications and repairing relative links. |
| `README.md` | Keep development's concise README; move B1's dated notice into its checks file. |
| `docs/roadmap.md` | Keep all development planning and performance-gate prose. Retain B1's implemented status and protected-insertion exception. Move all five B1 performance rows, with their measurements and limits, into `docs/perf/b1-memory-loyalty.md`; add one index line. |
| `docs/population-schema18.md` | Development matches the B0b prerequisite. Keep B1's execution amendment and the complete B0/B0b contract. |
| `docs/plans/step-a-inspector-traits-choice.md` | Take development's synced plan and corrected checklist links; B1 has no changes to this file relative to its prerequisite. |
| `docs/reviews/2026-10-31-a3-pr-description.md` | Take development's relocated checks link; retain all historical A3 evidence/checklist text. |
| `Core/CitySimulator.cs` | Combine development's complete native address formatter with B1's memory settings attachment/rebinding and ordered midnight activation. |
| `Core/FoodChoicePolicy.cs` | Keep B1's captured contribution in final offer weights; development equals the prerequisite. |
| `Core/FoodCommerce.cs` | Keep B1's causal experience capture, typed checkout evidence and unchanged physical queue-sale behavior; development equals the prerequisite. |
| `Core/FoodWeightedCommerce.cs` | Keep B1's shortlist contribution capture/reuse, selected-offer contribution and commit/reached-stop capture; retain B0b's held A3 image. |
| `Core/ShopMemory.cs` | Keep B1's active revision-1 support and unknown-revision refusal; all bounded contracts remain. |
| `Core/ResidentInsight.cs` | Take development's native address formatting/fallback and duplicate-address fix; B1 has no prerequisite delta. |
| `Persistence/SaveCodec.cs` | Keep B1's bounded nonzero scalar support; retain synced B0b validation, migrations, counting and frozen readers. Development equals the prerequisite. |
| `Runtime/NativeFoodMarket.cs` | Keep B1's live, generation-matched failure evidence and exact stock/bag/closure attribution; development equals the prerequisite. |
| `Runtime/PersistentCityMod.cs` | Combine B1 memory loading, clamp/log and market attachment with all incoming interior start/restore/stop and failure paths. |
| `Runtime/PrototypeSettings.cs` | Combine all thirteen B1 flat settings/converters with development's partial settings class and interior settings/converters. |
| `Runtime/ResidentInspector.cs` | Take development's native complete-address callback; B1 has no prerequisite delta. |
| `config/persistent-city.json` | Keep the union of every development and B1 setting with unchanged values, including all thirteen memory keys and default-off interior mode/auto guard. |
| `tests/HeldFoodTuningTests.cs` | Keep B1's deliberate replacement of neutral-only expectations with bounded nonzero support; malformed/nonfinite/out-of-range checks remain. |
| `tests/ResidentInsightTests.cs` | Take development's complete synced address-format/fallback tests; B1 has no prerequisite delta. |
| `tests/Schema18ReviewTests.cs` | Keep B1's supported active-B semantics; retain strict code/revision/header refusals. |
| `tests/Schema18Tests.cs` | Keep B1's active revision-1 acceptance and unknown-revision refusal; retain migration, round-trip, budget and source-preservation cases. |
| `tools/build.py` | Take development's interior assembly reference, dotnet discovery and explicit missing-SDK handling. |
| `tools/food-ownership-regression/Program.cs` | Keep B1's actual earliest-hook active-B acceptance and unknown-code/revision/future refusal cases; no authority rewrite. |
| `tools/native-command-regression/run.py` | Take development's synced SDK discovery and native harness setup. |
| `tools/optional-stop-regression/run.py` | Take development's synced compiler/setup, optional-stop findings and performance modes. |
| `tools/physical-selection-regression/run.py` | Take development's synced SDK discovery, interior wiring and selection-performance coverage. |
| `tools/save-contract-regression/HeldTuningContracts.cs` | Keep B1's nonzero eight-shop read/write/counting allowance checks and out-of-bound refusal alongside B0b's held-image coverage. |
| `tools/save-contract-regression/Program.cs` | Take development's F0 off/on twelve-transition byte-preservation checks and runtime imports; B1's changed contracts live in the separate partial above. |

**No additional behavior change or integration fix.** B1's memory algorithms,
capture rules, protected insertions, lazy decay/replacement, read-only dump,
bounded scalars and their tests remain as approved. Three shared B1 Core/Runtime
files do change relative to `f214cd4`: `CitySimulator.cs`, `PersistentCityMod.cs`
and `PrototypeSettings.cs` combine the existing development changes described
above. Other shared address/inspector files take development's existing fixes.
No new B1 policy, Runtime/Core logic or Persistence behavior is authored here.
All incoming C1/C3/D1/E2a/E2c/F0 production/test/tool additions remain intact;
C3 remains inactive. B2 remains separate.

Schema remains **18, amended in place while unshipped**. Development's entire
Persistence tree equals `c0af745`; C3/E2c claim no competing field or schema 19+.
B1 uses the existing B0 memory tuple and B0b per-shop scalar reservation, with
unchanged frozen 13–17 readers, record charges and size limits. The nonzero
eight-shop vector remains **525 bytes within 589**; neutral tuning is 405 bytes.

**Validation: 2,115/2,115 unit tests**, zero failures/skips, and **2,115 discovered
cases**. All **52 sequential foreground checks pass**: production Steam-reference
build; tests/discovery; all 29 regression projects, including memory, held tuning,
native candidates, pickup, agency payment and interior guard; all four required
Python harnesses; three demos; six capacity fixtures; base recovery, exact
input/record boundaries and history churn; choice rush and optional-stop
performance/findings. The prescribed other-dotnet guard precedes every command.
No execution deadlines or background build/compiler-server reuse.

All migrations from 13–17, schema-18 round-trips, frozen-source validation,
refused-source preservation, first-continuation capture and held nonzero replay
pass. All six full-memory/table capacity hashes, character/UTF-8 sizes, record
counts and future reservations match synced B0b exactly: reference **5,013,141**,
5,000 consumers **9,701,305**, 5,000 residents **17,298,040**, ceiling **141,087,195**,
CF32 **192,020,107**, CF64 **230,420,107 bytes**. Complete CF32/CF64 reserved budgets
remain **220,664,366 (82.204%) /259,064,366 (96.509%) bytes**, **2,111,867 /3,711,867
records**. CF32 passes 85%; CF64 passes hard caps while retaining its >95% warning
and no 85% claim. Exact **268,435,456-character/UTF-8** and **4,000,000-record**
boundaries pass; one-over and oversized 13–17 sources retain their original
text/reference. History stress preserves all **280,000** generation purchases and
exact product totals. Base recovery remains **115,049,515 characters**. Basic save
length **119,083**, refusals and economy/reload digests match synced B0b: **849
orders, 797 purchases, two uncertain and 107,967,424,757,760 spent units**.
F0's twelve off/on hidden/save/reload transitions remain byte-identical.

The executable preservation audit covers **638 files**: 548 identical between
parents, 63 development-only versions, 24 B1-only versions and three exact
combinations. All **406 historical B1-head checks blocks**, B1's dated README
notice, five performance rows and complete eight-item checklist survive.
Development's **22 checks/performance files remain unchanged**. The link audit
checks local files and heading targets; no missing target. B1's historical PR
description gets only its relocated checks link repaired.

Offline synthetic save/load and aggregate/batch samples of at least 30 ms are
individually flagged in the results, with their measurement boundary. These are
separate from native frame cost. Lookup and warmed repeat commits still allocate
zero bytes; saturated memory save growth remains **77,387 /200,827 bytes** at
1,914 /5,000 consumers, with the **51-byte** inclusive extension within B0's 64.
No native timing inference or new performance win is claimed.

[Command receipts, source hashes, preservation audit and measurements](2026-11-01-b1-sync-base-results.json).
Raw command/guard logs, audit scripts and both merge-base diffs remain ignored in
`build/b1-sync-base-gate/`. Production DLL SHA-256:
`0d63860784e2da52df73b9eba0bfef71a4eb0583d9c4c9d29588bb5317dd295e`.

Offline integration only. No game, disposable rig/profile, bridge lease,
Steam/Workshop mutation, ordinary save, WSL, deployment or release action.
Under merge rule v2, native checks join the batched combined locked
`codex/persistent-city` head; pointer/input checks wait for Peter's 6 Nov playtest.
No per-PR rig is requested. Mod-work p95 below 5 ms, flagging every spike of at
least 30 ms, separate whole-frame tracking and all four parked hotspots remain
unchanged. No new performance win or native clearance is claimed.
