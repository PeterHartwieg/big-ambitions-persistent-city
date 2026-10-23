# Car-search ceiling review panel — 21 October 2026

This work uses Peter's established panel: GPT-6 Astra, GPT-6.1 Sol, Claude Opus 5.5 and Grok 4.7. Each reviewer received its own frozen candidate, the pre-change source, full diff, manifest and offline evidence. No reviewer changed production source, ran the game or accessed the PC. Astra and Sol could run offline checks in their copies; Opus and Grok had read-only file tools.

All four approve the production change. Astra and Sol also approve the subsequent validation supplement. No blocking findings remain.

## Production review

Frozen source set: `668b6a0baa1d42039a6c56466bdb2740a0148950f6c874e2f62be2985e45654d` (153 files).

| Reviewer | Verdict |
| --- | --- |
| Astra | Approve; optional benchmark-parity improvement |
| GPT-6.1 Sol | Approve; optional benchmark-parity improvement |
| Opus 5.5 | Approve; optional connector test and documentation improvements |
| Grok 4.7 | Approve; optional test/evidence observations |

Astra and Sol independently verified all 153 manifest hashes, ran the 877-test suite and reproduced the original 34 full-route comparisons with 60 assertions per revision. Opus and Grok inspected the baseline, source, tests and supplied logs; neither ran checks or recomputed hashes. Opus's actual usage record identifies `claude-opus-5-5`. Grok's session metadata selects `grok-4.7`; its usage record identifies the backend as `grok-4.7-build`.

The reviews agree that the cutoff preserves competitive searches under the existing immutable-geometry, slowdown-only and nonnegative-delay contract. The subtraction includes all required trip pieces, the allowance conservatively handles floating-point regrouping, infinity retains full searches, and the final strict comparison still decides mode choice. Workspace cleanup, method-group overloads and final selected-path validation remain intact.

## Validation supplement

Final validation source set: `b358370a631f05d8b082674e582537f6ec9dca99631ad5f9eb82ba3aed3d3c42` (153 files). All **49 production C# files** remain identical to the initial reviewed source and the native-reference build inputs. Only validation, stubs and documentation changed.

- The original benchmark checked each process's local result but did not compare the exact three timed fixtures across versions. An untimed preflight now compares every full-route fingerprint before timing, and every timed row is checked against that preflight.
- Original near-tie fixtures had zero curb connectors. New exact-tie and adjacent-representable ETA cases use unequal nonzero source/destination connectors. A scratch mutation that deducts the source connector twice fails specifically in the one-ULP car-win case; production source was not mutated.
- A subway-winning fixture now asserts that road-search forecasting actually runs, proving the existing geometric precheck did not bypass A*.
- The report documents that off-path native changes may be discovered later by a subsequent query or the existing staged sweep. It also distinguishes strict graph input validation from the Runtime helper's conservative fallback to unbounded search.

Astra and Sol independently **approve the supplement**. Each verified all 153 final hashes and byte equality of the 49 production files, reproduced **38 exact full-route matches with 69 assertions per revision**, and compared all **three untimed benchmark-fixture fingerprints**. Neither reran timing or the mutation; those remain supplied evidence. Opus and Grok's production reviews refer to the initial freeze, not this later validation supplement.

Grok additionally noted that the random-graph test permits pruning immediately below the unbounded ETA and that the helper unit test does not chain both Runtime deductions. These tests serve different invariants; exact-tie and one-ULP preservation are exercised by the dedicated tests and the full Runtime connector/composition fixtures, including the new unequal-connector cases. No further production or test change was needed. Its caution about winning-car timing variance is reflected in the performance report, including the slower final measurement.

## Evidence and delivery boundary

- **877 tests**, **6,300 graph comparisons**, **213,884 gate checks**, the road-handoff/sweep/gate harness and **602 speed/persistence checks** pass. The full-planner comparisons are described above.
- C# 9 compilation with warnings as errors against both local Mac and retained Windows game references produces DLL SHA-256 `164af7813e3b3ed0c8ca18c3605d7f8a1d38535407f815f401df1a7e6db19c1e`.
- The [performance report](2026-10-21-road-search-ceiling.md) records synthetic timings, allocation/read counts, repeat variance and the slightly slower winning-car result. No native frame-time, FPS or gameplay gain is established.
- Prompts, frozen snapshots, manifests, raw results, diffs and build inputs remain under ignored `build/road-search-ceiling-20261021/`. The reviewed package is `build/road-search-ceiling-20261021/PersistentCity-road-search-ceiling-reviewed-development.zip`, disabled by default. Its manifest records review identities, source and build hashes, and later documentation amendments. Both PC profiles retain the previously installed reviewed gate-index DLL.

Review status and delivery metadata are later documentation amendments to the frozen validation source set; production and validation code remain unchanged.
