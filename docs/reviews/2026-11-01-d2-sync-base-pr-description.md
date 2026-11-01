# Sync D2 with the E1/F1/A5/E3 integration head

Target: `codex/pc-d2-rival-outflow`. Merge this PR with a merge commit so its
integration ancestry survives; PR #58 then squash-merges into
`codex/persistent-city`. This is conflict resolution and integration only.

Merge parents: D2 `ef2d72e` and integration `5877453`; merge base `51ce709`.
Both sides' diffs against that base were inspected for the conflicted files.

During validation the integration head advanced to A5 `0ffb819`. A second real
merge takes it into the E1/F1 merge `e263af6`, with merge base `5877453`.
README conflicts retain both D2/A5 notices and current schema 21; the roadmap
perf-log conflict retains both D2 and A5 entries. Every A5 check/document/tool,
its incoming `ResidentInsight` changes and formatter optimization are retained.
The sole A5 test integration fix updates its current migration result assertion
from 20 to 21, preserving all source/money/identity/read-only checks. The full
batch is rerun on this latest tree, including A5's integrated/rush/save-bench
modes and the nested pickup regression.


The last fetch caught E3 ice cream `75c956c` after A5 acceptance. A third real
merge brings it into local head `9ffc990`, using base `0ffb819`. Its sole README
conflict keeps both D2 and ice cream STOP notices. All E3 source pins, reader,
tools, checks and indices are retained unchanged. No Core/persistence/schema or
D2/E1 integration code changes for this continuation. The complete gate is rerun,
adding the new regression, three isolation guards and frozen reader baseline.

D2 now builds on E1 schema 20. Its isolated `RivalWire21.Migrate` uses the frozen
E1 reader (`PopulationSchema20*`), validates the source, preserves all captured
weather, and adds the schema-21 rival watermark/account defaults. Older supported
saves traverse E1's neutral next-day weather upgrade before 20 -> 21. Schema 22/23
remain future/refused; no reserved number is reassigned.

Runtime/Core scope: E1's incoming runtime capture/settings/physical queue code is
retained unchanged. The integration changes D2/E1 Core food scoring only to pass
both independent weights: captured rain travel weighting and D2's rival weight
apply in the bounded shortlist and final offer selection. `CurrentSchema` stays
21 alongside E1's weather fields. A5's incoming `ResidentInsight` Core changes are retained without modification. No journey, ledger, settlement, rival catalogue,
native checkout or other slice policy is redesigned. Persistence changes are the
predecessor hook, frozen schema-20 reader (its strict tuple-property guard excludes only D2 additions), schema-19 fixture field exclusions,
current weather wire support and early schema-20 authority acceptance.

## Conflicts resolved

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep D2 and F1 entries and every integration index line, including E1; current D2 index points to its final results, with the archived timing/index record preserved in D2 checks. All sync evidence lives in D2's own checks file. |
| `README.md` | Retain D2 overview and F1 NO-GO pointer; current schema list includes predecessor 20. |
| `docs/roadmap.md` | Retain D2 and E1 status, F1 stop entry, both perf-log links and all other entries. |
| `config/persistent-city.json` | Keep all four rival and all four weather settings. |
| `Core/Model.cs` | Schema 21 plus E1 citizen weather capture and activation day. |
| `Core/FoodAwareness.cs` | Keep separate rival and weather arguments; apply both during ranking. |
| `Core/FoodChoicePolicy.cs` | Apply rain-weighted travel and the captured alternative weight; preserve zero-weight exclusion. |
| `Core/FoodWeightedCommerce.cs` | Capture weather once and forward both weights in normal/diagnostic shortlists; retain alternative terms in final selection. |
| `Persistence/SaveCodec.cs` | Delegate every predecessor through frozen E1 then isolated D2; retain both strict wire/semantic contracts. |
| `tests/FoodAwarenessMemoryIntegrationTests.cs` | Preserve E1's 18/19/20 held-memory matrix using genuine predecessor writers; expect schema 21. |
| `tests/FoodAwarenessTests.cs` | Retain D2's frozen schema-19 fixture, old-awareness refusal, no-recapture and current replay assertions. |
| `tests/MarketResearchTests.cs` | Keep byte-invariance assertions with schema 21. |
| `tests/Schema17Tests.cs` | Keep unknown/newer refusal checks for 22, 23 and 1000. |
| `tests/Schema18ReviewTests.cs` | Keep D2 tuple coverage, schema-21 header ordering and genuine future-header refusal (22). |
| `tests/Schema18Tests.cs` | Keep D2 tuple-v2 corruption tests plus E1's weather-suffixed escaped-memory refusal. |
| `tools/awareness-regression/Program.cs` | Genuine frozen 19 fixture; separately report current wire, awareness-only projection, weather and rival byte deltas. |
| `tools/save-contract-regression/AwarenessContracts.cs` | Retain D2's exact awareness allocation assertion; project E1 weather out of the genuine predecessor before schema-18 migration. |
| `tools/save-contract-regression/CapacityBench.cs` | Populate E1 captures; consume both weather and rival allocations once and report both; retain all caps. |

Other minimal integration repairs: D2's native-double size harness now compares
20 -> 21; E1 migration fixtures use genuine pre-rival/pre-weather writers; the C4
save comparator explicitly expects schema 21; sequential gate runner includes all
root regression projects, including weather. Existing D2 held-reload tests also
exercise changed rain settings, predecessor migration covers 19 and 20, and a
combined weighting invariant checks rain, rival weight and zero-weight exclusion.

## A5-head validation and limits (archived)

Acceptance source `29147ee` includes latest integration `0ffb819`. The installed-game
assembly build, all **2,438 unit tests** (0 skipped), discovery of all 2,438 tests,
and **58/58 final foreground gates pass**. This includes every required regression,
all root regression projects, three simulation tools, four capacity cases, nested
pickup, A5 rush/integrated/save-bench modes and C4 exact-byte comparison. Every
test/run used the required other-dotnet guard; no CLI deadline was set. Commands,
log hashes and raw metrics: `docs/reviews/2026-11-01-d2-sync-base-results.json`.

Current population-only fixture is 120,184 bytes; its weather-stripped projection
is 119,083. D2's genuine 20 -> 21 idle delta remains +5 bytes/consumer +19 header,
zero records. C4 control is identical at 41,787 population / 66,679 food bytes.
CF64 full future budget remains 259,064,366 bytes / 3,711,867 records; no cap grows.
One D2 offline batch spike is flagged at 40.9275 ms, and one full dense awareness
build sample at 33.2802 ms. Whole-save latency >=30 ms is separately recorded;
these are shared-host offline operations, not native mod-work or frame proof.

Old D2 byte/perf tables were measured on the line based on `51ce709`, before E1;
they are explicitly labelled historical and their measured numbers are preserved.
No native performance proof is claimed. No game/rig/bridge/Steam/profile action,
ordinary-save deployment, push or PR creation was performed. Under merge rule v2,
combined locked-rig evidence follows on the integration head; pointer/input-only
checks wait for Peter's 6 Nov playtest. The four parked hotspots stay parked.
