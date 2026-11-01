# C2b: sync the E3 ice cream integration base

Merge `75c956c` (`codex/persistent-city`) into C2b head `ffe85a0` with a real
two-parent merge, `033ba81`, from merge base `0ffb819`. Target this PR at
`codex/pc-c2b-campaign-awareness` and retain its merge ancestry when landing.
Both sides' diffs against the merge base were inspected before resolution.

The sole textual conflict was `README.md`: keep both the C2b campaign status
paragraph and E3 ice cream's independent feasibility STOP/conditional-reader
paragraph, followed by the existing A5 and F1 notices. The automatic compositions
in `CHECKS.md`, `docs/roadmap.md` and `docs/plans/steps-b-e.md` retain all incoming
ice cream entries alongside C2b and every existing slice. Both C2b and A5 perf
index entries and their evidence files remain unchanged. Historical measurements
and schema-20 ice cream evidence retain their original scope; they do not become
schema-22 or current-head measurements through this merge.

No integration fix or design change was needed. No C2b Core, Runtime,
persistence, test or harness code changed. The only Runtime differences from
the C2b parent are the incoming, unchanged E3 ice cream facade and its single
shared-reader descriptor; every E3 source/tool file matches `75c956c`.
No other slice code changed. Schema remains 22 with the approved E1 19 -> 20
then C2b 20 -> 22 chain; reserved 21 and future 23 stay refused. Campaign
preference remains 1.25 with half soft distance for all residents in shortlist
and final weighting under Peter's decision, with the existing observation,
failure-isolation, bounds and held-input contracts preserved.

The merge-tree preservation audit checks all 1,351 tracked paths: 1,347 exact
parent blobs outside the four composed documents, all checks-index lines from
both parents, and every added line from both sides of those documents. No
source hash pins were changed.

All **60 foreground gates pass** on `033ba81`: production build, **2,416 tests**
passing and discoverable (zero failed/skipped), all **38 top-level regression
projects**, four Python regressions, three simulations, continuity pickup,
optional-stop performance/findings, reader source guards/frozen baselines,
C4's exact saved-byte comparison and five combined A5 acceptance/benchmark
modes. Every dotnet test/run, including Python children, used the prescribed
other-dotnet guard. No command time limit, detached tests or remaining owned
test process. The final fetch still reports `75c956c`, already included.

The schema-22 save fixture remains 120,184 characters with the same prior C2b
digest, economy/order/reload fingerprints and twelve F0 transitions. C4's
41,787 / 66,275-byte fixtures are identical with/without its engine. Campaign
wire charges and reader assertion counts/allocations reverify unchanged.
The current offline C1 cold p95 **6.713 ms** is flagged above 5 ms; C2a setup
>=30 ms samples, aggregate rush CPU and separate save/read/migration timings
remain explicitly qualified in the
[owning C2b checks](../checks/c2b-campaign-awareness.md) and
[machine record](../checks/c2b-sync2-base-results.json). Older measurements and
byte records remain historical, not silently relabeled as current.

All local validation is offline. Under merge rule v2, native checks remain batched into the combined
locked `rig_test` (Codex Sol, high) on the development branch head; pointer/input
checks wait for Peter's 6 Nov playtest. No per-PR rig proof, release/upload or
ordinary-save deployment approval is claimed. No game, test rig, bridge lease,
Steam/Workshop, normal-profile or WSL operations were performed. No new
performance win or combined native/whole-frame performance result is claimed;
the four parked hotspots stay parked.
