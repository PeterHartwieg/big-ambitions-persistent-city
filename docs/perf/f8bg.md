Status: implemented (offline); not verified by rig

# F8b-F8g perf

Refs [#95](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/95). Plan section 6 of the [F8 plan](../plans/f8-capability-boundaries.md). Windows 11 PC, .NET 10, Debug test build for the xUnit arms and Release for the two tools, all in the foreground; the machine was shared with other sessions, so wall-clock numbers are upper bounds. No game, rig, ModsLocal or bridge was used. Checks: [docs/checks/f8bg.md](../checks/f8bg.md).

Standing gate: mod-work p95 < 5 ms; every mod-work sample >= 30 ms flagged; whole-frame timing reported separately (#128 stays its own investigation). **F8b-F8g add no per-frame work.** The commands run on request; the two activation lines run once per load inside `CompleteInitialization`, outside gameplay timing windows (source test `CapabilityReportTests.DiagnosticsAreNotReachedFromUpdate`). No mod-work or whole-frame number is claimed offline; rig check 10 measures them. The four parked hotspots stay parked.

| Row | Measured | Number | Source |
| --- | --- | --- | --- |
| F8b | Component only: Core snapshot build plus report text from prebuilt inputs, 957x2 city alive | p50 0.003 ms, p95 0.006 ms, 3,992 B per call (one 16 ms outlier in 5,000, Debug, shared machine) | `CapabilityReportTests.CommandCostOnThe957x2City` |
| F8b | `pc capability status` as production runs it, minus plain field reads: the shared `HookConflicts.ReadMetadata` (one assembly pass, one Harmony pass over 44 patched methods from 5 owners, both `HasAnyPatches` checks), snapshot and report, 40 assemblies, Release | p50 0.64-0.74 ms, p95 0.92-1.21 ms, max 1.99-4.30 ms; **222,205 B per command** (246,263 B before the `FullName` win below) | `tools/capability-contracts` |
| F8c | Contract check, both arms, 13 members, once per process (stand-in types) | p50 0.004 ms, p95 0.005 ms, 3,176 B per check; first-call outlier 10.8 ms in one Debug run | `NativeFoodContractTests.ContractCheckCost` |
| F8c | Same check on the stubs (Release) | p50 0.0045 ms, p95 0.0078 ms, max 0.048 ms | `tools/capability-contracts` |
| F8c | Core part of one F6j checkout (ticket plan on an eight-item menu, receipt begin, confirm, read), base `17dccf8` vs head, 20,000 meals, three runs each | base p50 9.7-9.9 µs, p95 13.8-15.7 µs; head p50 9.9-10.5 µs, p95 15.2-18.4 µs (same Core code; the spread is run-to-run noise on a shared machine) | `.scratch` bench, method below |
| F8d | Classification per 30 s read, Ready (the 400-identity comparison the base already made) | p50 0.036 ms, p95 0.055 ms | `RloContinuityTests.ClassificationCostPerRead` |
| F8d | Classification per read, transient (no comparison) | p50 < 0.0001 ms | same |
| F8d | Extra RLO reads | none: four `ReadRlo` call sites as before, one `rlo.Read`, 30 s cadence unchanged | `RloContinuityTests.CallSitesUseTheClassAndAddNoRead` |
| F8e | Activation scan, warm (Release, stand-ins: 3 patched targets plus 1 foreign-only method, 49-method inventory join) | p50 0.094 ms, p95 0.116 ms, max 1.06 ms | `tools/capability-contracts` |
| F8e | Activation scan, first call in the process (includes JIT) | 3.4-3.6 ms | same |
| F8f | `pc capability business <place>`, component only: scope lookup through the city view, mechanism and text on the 957x2 city | p50 0.0004 ms, p95 0.0008 ms | `TransactionMechanismTests.BusinessCommandCostOnThe957x2City` |
| F8f | `ReadOnlyScopeStatus` (the market's verdict) | not measured offline: it reads native registrations, which no harness stubs; approximation: seven dictionary or field reads and one menu loop of at most eight names; rig check 10 measures the command | — |
| F8g | Matrix wall time, 8 Core arms, 2 simulated days per economy arm, 957 residents x2 | 7.6 s (Release tool); 1-day arms inside the unit suite | `tools/capability-matrix`, `CapabilityMatrixTests.EveryCoreArmPasses` |

**F8e estimate check.** The plan estimated 27 x 0.011 ms = 0.3 ms from the C5d foreign-patch check. The warm scan on 4 patched methods is 0.094 ms, most of it the fixed join over the 52-row inventory; at the 30-odd patched methods of a full modpack it stays under the estimate's order. The first call in a process pays JIT (3.6 ms), once per load and below the 30 ms flag. Rig check 10 records the real activation line.

**F8c checkout p95 before/after.** The bench builds one console program against the base's and the head's `Core` and `Persistence` and times the Core part of an F6j checkout: `FoodMealExtras.Plan` on an eight-item menu, `FoodSaleReceipts.Begin`, `Confirm` and `AlreadyPaid`, 20,000 meals after 2,000 warm-up meals, three alternating runs. The numbers are in the table; no Core file on the path changed, and base and head overlap. The native checkout cannot run offline. The Runtime change on it is one reference check per sold line (`process == null`, before `beginReceipt`) and one `FoodCheckoutMode` property read in `CanServe`, `SupportedMenu` and `ScopeStatus`. Rig check 10 measures native checkout with the rest of mod-work.

**F8b input path, cheap wins.** Round 1 replaced three `Harmony.GetAllPatchedMethods` passes (one per inspector guard) and two assembly enumerations with one of each. Round 2 moved that metadata read into `HookConflicts.ReadMetadata`, which production calls and the harness times, and compares `Assembly.FullName` instead of building an `AssemblyName` per assembly: 246,263 B to 222,205 B per command. The rest of the allocation is `Assembly.GetType` per assembly and Harmony's patch-info objects. The command runs on request only; the game loads more assemblies than the harness, so rig check 10 records the real figure.

**Cheap win taken (plan section 6).** `FoodSupplyReader`'s three members stay `static readonly`; they now come from the checkout contract, which is checked once per process, so the per-read cost does not change (before: three reflection lookups at type initialisation; after: the same three lookups inside the one contract check, plus their exact type checks).

**F8b allocation.** Per complete command 222,205 B (table); the Core component alone is 3,992 B per call, on request only. A reused report object returns its cached text while the registry revision and header are unchanged (F8a arm, 0 B), but F8b builds a fresh registry per request by design, so the text is rebuilt; at one request per game minute (rig check 10) that is about 4 KB per minute.

**Test-only change.** F8a's `RegistryUpdateAndReportCost` zero-allocation arms re-measure once, untimed, when the first window shows bytes: one focused run saw 7,216 B in one 20,000-call window (0 B per call) and 12 isolated runs saw none. A per-call allocation repeats in the second window and still fails.
