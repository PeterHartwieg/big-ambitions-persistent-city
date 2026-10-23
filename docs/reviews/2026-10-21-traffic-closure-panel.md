# Traffic-closure refresh review panel — 21 October 2026

Peter's established panel is GPT-6 Astra, GPT-6.1 Sol, Claude Opus 5.5 and Grok 4.7. Each reviewer received a separate frozen candidate, the exact previous source, full diff, manifest and offline evidence. None modified production source or accessed the game, saves or PC. Opus and Grok use read-only file tools. Astra and Sol waited until the primary timing run finished before executing offline checks.

Frozen source set: `6758035e24d5b87f1eebacc5d138464bb95e3a3102371987e46e1af569591a72` (160 files).

All four approve the production change. Astra and Sol also approve the validation supplement. No blocking findings remain.

| Reviewer | Verdict |
| --- | --- |
| Astra | Approve; no actionable findings |
| GPT-6.1 Sol | Approve; no findings |
| Opus 5.5 | Approve; optional harness/diagnostic suggestions |
| Grok 4.7 | Approve; optional defensive/test/provenance notes |

Astra and Sol independently verified all 160 manifest hashes and reproduced **24 exact closure-state comparisons with 34 assertions per revision**. Astra additionally executed both untimed benchmark-fixture modes and compared all five fingerprints. Both inspected the primary measurement's source hashes, five exact fixture preflight results and all timed fingerprints. Neither reran timing; the 883-test suite, legacy harnesses and native-reference compilations remain supplied evidence in their reviews.

Opus inspected the diff, baseline, callers, native contracts, tests and supplied logs. It did not execute checks or recompute hashes and did not receive the later timing table. Its actual usage record identifies `claude-opus-5-5`.

Grok likewise inspected source, baseline, callers, tests and supplied logs without executing checks or recomputing file hashes. It compared supplied per-file parity hashes with the frozen manifest. Its session metadata selects `grok-4.7`; the actual usage record identifies backend `grok-4.7-build`. It did not receive the later timing table or validation supplement, so its approval covers production correctness and methodology, not the measured speedup.

The reviewed change preserves the detached ascending relevant-section set, applies the same closure observations in the same order, and scopes shared native validation to one synchronous read-only pass. Each pass gets fresh node/junction results, `finally` releases the scope on failure, and ordinary single-section boundary checks stay fresh. Only successful gate checks survive across passes, tied to immutable access geometry and invalidated by a different access snapshot. Structural mismatch still makes the snapshot stale. The reviewed diagnostics measure closure-refresh cost separately from advancement and complete frame time.

The [implementation report](2026-10-21-traffic-closure-refresh.md) records full synthetic timing/allocation results and their limits. Final measurements were gathered from the frozen production and test source. Timing tables and review status are documentation amendments after that freeze. No native performance or gameplay acceptance is claimed.

## Validation supplement and optional findings

Final validation source set: `504c5aca795373b17531b2442239effadb29309d37fc903bc3fafb201dc77a0d` (161 files). All **49 production C# files** are identical to the initial four-model source and the native-reference build inputs.

- Following Opus's suggestion, parity failures now use explicit exceptions, so `python -O` cannot disable the checks before timing.
- An extra assertion verifies that the two closure/reopening passes really keep the same stubbed native frame counter. The default harness now has 35 assertions; its 24 fingerprints are unchanged.
- The report includes the ranges of the three process medians, identifies steady-state timing separately from changed-closure correctness coverage, and explains that the runtime closure timer includes occasional network binding.
- Three scratch mutants fail: omission of gate-cache invalidation, omission of the stamp clear on generation reset, and omission of native batch cleanup. The tests deliberately cover those faults, not every conceivable wrap implementation. These runs never changed production source.

Astra and Sol **approve the supplement**. Both independently checked all 161 final hashes and all 49 unchanged production files, reproduced 24 exact states/35 assertions under `python -O`, and confirmed deliberate fingerprint mismatches still fail in that mode. Sol also checked count mismatches. Mutation executions remain supplied evidence; the reviewers inspected them but did not rerun them. The timed loops and fixtures are unchanged by the supplement.

Other Opus suggestions remain documented rather than changing production: sorted-set enumeration still has small consistent allocations; malformed native-node indices cannot arise under the current importer contract; null-road nesting is guarded by the simulator; and production gate refresh normally replaces the road adapter while the new test directly exercises its `SetAccess` safeguard. The injected native-read failure is reached in node validation; the separate Core tests cover scope cleanup at begin/read/end/nested failure stages. None was a demonstrated production defect.

Grok's defensive suggestions concern a hypothetical throw from the current dictionary-clear-only `End`, recovery after out-of-memory between the two first-batch array allocations, and a future rename silently bypassing the reflection-based wrap setup. Current names and setup were verified, and no production change was needed for those hypothetical cases. Initial scratch allocation and gate-cache fill remain explicitly outside the timing claim.

The build-input manifest hashes differ from the review-bundle hash because they describe a different source/evidence bundle. The delivery check compares all 49 actual production-file hashes across build inputs, frozen review source and final validation source; all match. Reviewers' supplied evidence distinctions remain intact.

After review, the primary agent reran the complete suite directly from the frozen final source: **883 passed, zero failed**. `frozen-test-inputs.json` records its command and source hashes; `frozen-tests.log` and `frozen-test-results/frozen-suite.trx` retain the results and named cases. This resolves the initial main-checkout test log's provenance limitation noted by Grok. It is a later primary verification, not a test run by Grok.

During final packaging, concurrent RLO 0.45 compatibility edits appeared in the working tree's `Compatibility/RloIdentityAdapter.cs` and `tests/RloCompatibilityTests.cs`. They were preserved and are outside this review. The package comes from the frozen reviewed build, retaining its RLO 0.40.4/0.41.1 contract; it does not include those newer compatibility edits. The six production files changed by this traffic-refresh task still match the frozen review. `concurrent-working-tree-changes.json` records the observed difference without treating it as part of the delivery.

Prompts, source/baseline copies, manifests, diffs, reviews, build inputs and measured evidence remain under ignored `build/traffic-closure-refresh-20261021/`. The reviewed package is `PersistentCity-traffic-closure-reviewed-development.zip` in that directory. Its DLL is `6be406eba6e2178d4322d5635b6c0092fe11ac089b6b38413ffcb0bc0c16c63f`, identical from the Mac and retained Windows references. Distribution remains disabled by default. This candidate is not installed; prior reviewed packages and PC installations are unchanged.
