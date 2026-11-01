# Sync approved B2 with the merged development base

PR #19 conflicts after its B0/B0b/B1 prerequisites landed as squashes. Merge
`codex/persistent-city` at `7053a28` into approved B2 `2c74051`, retaining both
parents and all incoming slices. This sync targets `codex/pc-b2-regulars` and is
intended to land with a **merge commit**; PR #19 subsequently squash-merges into
`codex/persistent-city`. No published ancestry is rewritten.

The common ancestor is `7a6153c`. Both parents' diffs against that merge base
are retained under ignored `build/b2-sync-base-gate/`. Comparing both parents
with B2's B1 prerequisite `f214cd4` isolates the approved B2 delta from the
synced prerequisites and other slices. The preservation audit covers the whole
executable tree, including automatically merged files.

| Conflicted file | Resolution |
|---|---|
| `CHECKS.md` | Keep development's short index and add exactly one B2 line. Move both B2 checks sections, measurements, review evidence and the complete eight-item 6 Nov checklist into `docs/checks/b2-regulars-inspector.md`, preserving every LOCKED-OK / NEEDS-INPUT qualification and repairing relative links. |
| `Core/CitySimulator.cs` | Keep the synced complete-address formatter and B2's memory-reference/settings/shop-name/current-generation arguments to the read-only inspector capture. |
| `Core/FoodMemoryCommerce.cs` | Development equals the B1 prerequisite. Retain B2's regulars projection/dump and extended memory dump, including all historical entries and OwnCurrent flags. |
| `Core/FoodMemorySettings.cs` | Development equals the prerequisite. Retain B2's two projection-only defaults and their existing clamp/log behavior. |
| `Core/FoodMemoryStore.cs` | Development equals the prerequisite. Retain B2's read-only reference lookup; no commit maintenance is added. |
| `Core/ResidentInsight.cs` | Combine development's complete native address formatting, safe fallback and duplicate-address correction with B2's own-consumer memory words and popup text. |
| `Core/ShopMemory.cs` | Retain every synced B0/B1 wire/policy contract. Keep B2's reviewed removal of the obsolete B0 projection; the full B2 implementation remains in `Core/RegularsProjection.cs`. |
| `README.md` | Keep development's concise README. Move B2's dated notice into its owning checks file; preserve all incoming notices in their existing slice files. |
| `Runtime/FoodMemoryDiagnostics.cs` | Development equals the prerequisite. Keep B2's cached panel/popup reads, read-only observed-generation filter and both bridge dumps. |
| `Runtime/PersistentCityMod.cs` | Combine development's interior lifecycle/failure paths with B2's inspector callbacks and effective projection-settings log. |
| `Runtime/PrototypeSettings.cs` | Keep development's partial class and all interior settings/converters; add B2's two existing flat projection settings, with one copy of each converter. |
| `Runtime/ResidentInspector.cs` | Keep the synced native complete-address formatter, passing B2's shop-name/generation callbacks. Retain B2's visible-only content refresh and cached popup read. |
| `config/persistent-city.json` | Union of every development setting and B2's two projection keys, with all values unchanged. |
| `docs/plans/step-a-inspector-traits-choice.md` | Take development's corrected address plan and relocated checks links; B2 has no prerequisite delta here. |
| `docs/reviews/2026-10-31-a3-pr-description.md` | Take development's relocated evidence link; retain all historical A3 text and checklist items. |
| `docs/reviews/2026-10-31-b1-v2-pr-description.md` | Take development's relocated evidence link; retain all historical B1 text and checklist items. |
| `docs/roadmap.md` | Keep all development planning, shared perf gates and slice indexes; retain B2's implemented status. Move all five B2 perf rows, numbers and limits into `docs/perf/b2-regulars-inspector.md` with one index line. |
| `tests/ResidentInsightTests.cs` | Take development's complete address-format/fallback coverage; B2 adds its memory/popup cases separately in `FoodRegularsTests.cs`. |
| `tests/Schema18Tests.cs` | Keep all synced migration/round-trip/size cases and B2's stable consumer IDs for the regulars deduplication fixture. |
| `tools/build.py` | Take development's synced SDK discovery, missing-SDK handling and interior assembly reference. |
| `tools/native-command-regression/run.py` | Take development's synced SDK discovery and native-command harness setup. |
| `tools/optional-stop-regression/run.py` | Take development's synced compiler/setup, optional-stop performance and findings modes. |
| `tools/physical-selection-regression/run.py` | Take development's synced SDK discovery, interior wiring and selection-performance coverage. |
| `tools/save-contract-regression/Program.cs` | Take development's runtime import and twelve F0 off/on save/reload byte-preservation transitions; retain all B0/B0b/B1 contracts. |

**No new feature, policy change or additional integration fix.** B2 Core/Runtime
files do change relative to `2c74051`: `CitySimulator.cs`, `ResidentInsight.cs`
and `ResidentInspector.cs` integrate the already merged A1 address formatter;
`PersistentCityMod.cs` and `PrototypeSettings.cs` integrate the existing interior
lifecycle/settings. B2's projection, defaults, one lazy consumer pass per open
panel refresh, positive-affinity test, current-generation filtering, represented
labels, historical memory diagnostics and removal of the home-district line
remain as reviewed. No new Runtime/Core logic is authored beyond combining
those existing changes. All incoming C1/C3/D1/E2a/E2c/F0 code, tests and tools
remain; C3 remains inactive.

**Schema remains 18 with no collision.** Every Persistence file and
`docs/population-schema18.md` is identical to both parents. B2 adds no saved
fields, changes no reader/migration/record charge and does not change memory,
watermarks, captured weights, checkout, money, routes or RLO. The gate includes
13–17 migrations, frozen readers, round-trips, refused-source preservation,
nonzero held replay and all six full-memory/table capacity fixtures, including
CF32/CF64 reservations and exact input/record boundaries.

**Validation: 2,142/2,142 unit tests**, zero failures/skips, and **2,142 discovered
cases** (including all eighteen global-namespace agency cases). All **53 sequential
foreground checks pass**: Steam-reference build; tests/discovery; all thirty
regression projects including B2; four Python harnesses; three demos; six
capacity fixtures; base recovery, exact input/record boundaries, history churn,
choice rush and optional-stop performance/findings. The prescribed other-dotnet
guard precedes every command. No execution deadlines or background server reuse.

All six full-memory/table capacity hashes, character/UTF-8 sizes, record counts
and future reservations match synced B1 exactly: reference **5,013,141**,
5,000 consumers **9,701,305**, 5,000 residents **17,298,040**, ceiling
**141,087,195**, CF32 **192,020,107**, CF64 **230,420,107 bytes**. Complete
CF32/CF64 reserved budgets remain **220,664,366 (82.204%) /259,064,366 (96.509%)
bytes**, **2,111,867 /3,711,867 records**. CF32 passes 85%; CF64 passes hard caps
with its existing >95% warning and no 85% claim. Exact **268,435,456-character/
UTF-8** and **4,000,000-record** boundaries pass; one-over and oversized 13–17
sources retain their original text/reference. History stress preserves all
**280,000** generation purchases and exact product totals. Base recovery remains
**115,049,515 characters**. Basic save length **119,083**, refusals and exact
economy/reload digests match synced B1: **849 orders, 797 purchases, two uncertain
and 107,967,424,757,760 spent units**. F0's twelve off/on runtime transitions
remain byte-identical. B2 read/reload/replay/settings tests retain zero saved
growth; the benchmark payloads remain **1,503,076 /3,925,066 bytes**.

The executable preservation audit covers **499 files**: 417 identical between
parents, 61 development-only versions, 15 B2-only versions and six exact
combinations (five Core/Runtime files plus config). Independent reconstruction
applies the reviewed B2 delta to development, translating only the already
merged complete-address callback. All **428 historical B2-head checks blocks**,
B2's dated README notice, five performance rows and complete eight-item checklist
survive. Development's **24 checks/performance files remain unchanged**. The
link audit checks **300** local file/heading targets with no missing target.

The B2 offline remeasurement uses .NET 10, saturated four-entry memory, sixteen
shops and forty normalized batch means after five warmups. Projection refresh
p95 is **0.667 /1.009 ms** at 1,914 /5,000 consumers; panel B2 text/projection
p95 is **0.993 /1.107 ms**, excluding native accounts and Unity. Full Core popup
capture/text p95 is **0.010 /0.013 ms**, allocating **9,136 bytes/call** in both
fixtures (historical B2 R1: 9,104). Cached lookup and warmed B1 commit remain
zero-allocation. These are uncontrolled offline batch measurements, with zero
>=30 ms batches in B2's measured operations; no speedup or native timing claim.
Offline save/load and other aggregate/batch metrics >=30 ms are individually
flagged in the results, separately from native mod-work and whole-frame cost.

[Command receipts, tested source hashes, preservation audit and measurements](2026-11-01-b2-sync-base-results.json).
Raw command/guard logs, audit scripts and both merge-base diffs remain ignored
in `build/b2-sync-base-gate/`. Production DLL SHA-256:
`c91b1321a27d52514e58bf0f57f6dd0329d6b536b960ae7a73ce36f4272f7fc9`.

Offline integration only. No game, disposable rig/profile, bridge lease,
Steam/Workshop mutation, ordinary save, WSL, deployment or release action.
Under merge rule v2, native checks join the batched combined locked
`codex/persistent-city` head; pointer/input checks wait for Peter's 6 Nov playtest.
No per-PR rig is requested. Mod-work p95 below 5 ms, flagging every spike of at
least 30 ms, separate whole-frame tracking and all four parked hotspots remain
unchanged. No new performance win or native clearance is claimed.
