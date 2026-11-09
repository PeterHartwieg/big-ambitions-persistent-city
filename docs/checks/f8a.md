Status: implemented (offline); not verified by rig

# F8a: capability model and hook inventory

Slice row: [F8 plan, section 4](../plans/f8-capability-boundaries.md#4-slice-breakdown), decisions D1 and D2. Refs #95. Base `origin/codex/persistent-city` at `3655662` (F8 plan #167 on top of F2e #172), current writer 35. No schema change, no save change, no Runtime, Persistence or Compatibility edit. The writer-35 golden hashes are unchanged (`NativeCapabilityTests.CurrentWriterGoldenHashIsUnchanged`, and F2e's `PlayerHoldTests.TheCurrentWriterGoldenIsPinned` still passes).

## What changed

- `Core/NativeCapabilities.cs` (new, pure netstandard2.1). Not wired to Runtime; F8b builds the first registry snapshot.
  - `CapabilityId`: the 19 D2 capabilities, in report order. `NativeCapabilities.All` holds each one's stable name (`food.authority`, ...), requirement (`Required`, `Optional`, `RequiredWhenPresent`), failure scope (a fixed `CapabilityScope` flag set), declared re-check point (`CityLoad` or `CompanionRevision`), companion mod if any, native contract, fallback and whether it is off by default (`food.dynamic`, `interior.view`).
  - `CapabilityRegistry`: the D1 four-state model (`NotChecked`, `Active`, `Off` with a reason, `Absent`). Rules: `Detect` moves `NotChecked` to `Active`, `Off` or `Absent`, once. `Fail` moves `Active` to `Off`; a second `Fail` keeps the first reason. `Recheck` is the only way out of `Off` or `Absent`, and only at the capability's declared point. `Absent` is only for capabilities backed by a companion mod. An `Off` without a reason throws. `Revision` advances on every applied change.
  - `CapabilityRegistry.Start()`: `Refused` when a `Required` capability is `Off` or `Absent` or a `RequiredWhenPresent` one is `Off`, naming the first in report order; `Waiting` while a required check has not run; otherwise `Ready`. Optional capabilities never block. The refusal message says the game or a companion mod changed, that this is not a damaged save and not an older city, and that the saved city is kept unchanged. It differs from the older-schema "start a new city" status and from a corrupt-save read.
  - `DisabledScopes` / `IsEnabled(scope)`: the scopes of every `Off` capability and of every `Absent` one except `RequiredWhenPresent` (Driven Rides absent disables nothing).
  - `CapabilityReport`: the D7 text. `Status(registry, gameVersion)` gives one header line and one line per capability (ID, requirement, state, reason, scope). `Show(registry, id)` adds the native contract, companion, every inventory hook target, the fallback and the re-check point. States print as `active`, `off`, `absent`, `not checked`. Reasons are folded to one line. The text never says "compatible" or "verified". A `Status` result is reused while the registry revision and the game version string are unchanged.
  - `NativeHookInventory`: the static owner and target list, below.
- `tests/NativeCapabilityTests.cs` (new): definitions, transitions, sticky `Off`, start refusal, scopes, report determinism, golden hash, and the perf arm.
- `tests/HookInventorySourceTests.cs` (new): the source inventory test.

## Hook inventory at this base

The plan's section 2 counted 11 owners and 27 methods at `c2fc1b0`. Re-read at `3655662` (after F2c-F2e, E5b2/E5b3 and F5h): **12 owners, 52 owner-target rows**. E5b2 added `persistentcity.food-contract` (`Runtime/LoadedCatalogueLifecycleMembers.cs`, targets from the 23-row `FoodContractHookTable` in `Runtime/FoodContractHooks.cs`), mapped to `food.dynamic`. F2c-F2e and F5h added no owner or target; H1-H11 are as the plan lists them. Without food-contract the 11 owners still patch the plan's 27 distinct methods (29 owner-target rows, since `RunMainGameTick` carries H1, H3 and H4).

| Owner | File | `.Patch(` sites | Targets | Capability |
| --- | --- | ---: | ---: | --- |
| `persistentcity.food-authority` | `Runtime/NativeFoodOwnership.cs` | 2 | 6 (by name) | `food.authority` |
| `persistentcity.food-routines` | `Runtime/NativeFoodMarket.cs` | 1 | 1 | `food.checkout.shop` |
| `persistentcity.food-contract` | `Runtime/LoadedCatalogueLifecycleMembers.cs` | 1 | 23 (table) | `food.dynamic` |
| `hart.persistentcity.cityclock.v1` | `Runtime/NativeCityClock.cs` | 3 | 2 | `clock.city`, `clock.drivenrides` |
| `hart.persistentcity.clockobserver.v1` | `Runtime/NativeTravelPace.cs` | 1 | 1 | `clock.observe` |
| `persistentcity.research-billing` | `Runtime/ResearchBilling.cs` | 1 | 1 | `research.billing` |
| `persistentcity.research-dialog` | `Runtime/ResearchAgencyDialog.cs` | 1 | 1 | `research.dialog` |
| `hart.persistentcity.physicalvehicles.v1` | `Runtime/PhysicalTrafficHooks.cs` | 5 | 8 | `physical.vehicles` |
| `hart.persistentcity.physicalintersections.v1` | `Runtime/PhysicalIntersections.cs` | 2 | 2 | `physical.vehicles` |
| `hart.persistentcity.ambientpedestrians.v1` | `Runtime/AmbientPedestrians.cs` | 3 | 3 | `physical.pedestrians` |
| `hart.persistentcity.stationarycrowds.v1` | `Runtime/StationaryCrowds.cs` | 1 | 1 | `physical.pedestrians` |
| `persistentcity.resident-inspector-input` | `Runtime/ResidentInspector.cs` | 3 | 3 | `inspector.click`, `.scroll`, `.uiblock` |

Each target records its declaring type, method, parameter list (CLR short names; `null` for H1's name-only lookups, which F8c pins), patch kinds and capability. `BuildingRegistration.Reset` appears under food-authority (by name) and food-contract (`Boolean`).

### How the source test works

`HookInventorySourceTests.Check` reads `Core`, `Persistence`, `Compatibility` and `Runtime` sources (full-line comments stripped) and reports a finding when:

- a `new Harmony(...)` owner is not declared, is declared for another file, or is not a string constant;
- a declared owner is not found in its file;
- a file's Harmony `.Patch(` call-site count differs from the declared count, or a file with no declared owner calls `.Patch(` (the router's single `patcher.Patch(hook)` in `FoodContractHooks.cs` is the one allowed exception);
- a direct `.Patch(` site's first argument, resolved through its local or field assignment (`AccessTools.Method(typeof(T), "M")`, `typeof(T).GetMethod("M")`, `nameof`), is not a declared target of that owner; sites whose target is a helper or table parameter are counted separately (one each in `NativeFoodOwnership.cs`, `PhysicalTrafficHooks.cs`, `LoadedCatalogueLifecycleMembers.cs`);
- any file uses attribute or bulk patching (`[HarmonyPatch]`, `PatchAll`, `CreateAndPatchAll`, `PatchCategory`, `CreateClassProcessor`);
- a `Patch(typeof(T), "M", ...)` helper call names an undeclared target (the food-authority and physical-vehicles helpers add targets without a new `.Patch(` site);
- the food-contract table rows differ from the declared rows;
- a declared target's method or type no longer appears in its file.

## Offline checks

Local, at `59d170e` plus this docs change (no code change since): build (`tools/build.py`) succeeded, DLL SHA-256 `7ee70a36971dcbbdbf998af42fe14bb9b1e30dd09c35cb0f8a131c9125fe6b8e`; `dotnet test tests/PersistentCity.Tests.csproj`: **4,304 passed, 0 failed, 0 skipped** (4 m 34 s). New cases:

1. **Transitions** (`Transitions`): each D1 transition applied once; repeated detection, `Fail` from `NotChecked` or `Absent`, and `Absent` on a native contract rejected with no state or revision change; reasonless `Off` and `NotChecked` outcomes throw.
2. **Off is sticky until the declared re-check** (`OffIsStickyUntilTheDeclaredRecheck`): research billing stays `Off` through `Detect`, a second `Fail` and a companion-revision re-check, and returns to `Active` only at city load; Street Food leaves `Absent` only at its companion revision.
3. **Required Off is a distinct start refusal** (`ARequiredOffIsAStartRefusalDistinctFromCorruptionAndFromAnOlderCity`): `Waiting` before checks, `Ready` with all active, `Refused` naming `food.authority` once it is off; the same save reads `Loaded`; the message differs from `SaveCodec.DevelopmentSchemaMessage` and from a truncated payload's `Invalid` error and contains none of "start a new city", "corrupt", "invalid". RLO absent refuses with its own wording; Driven Rides absent is `Ready`, Driven Rides changed is `Refused`.
4. **Optional Off disables only its scope** (`AnOptionalOffDisablesOnlyItsListedScope`): for each of the 15 optional capabilities, `DisabledScopes` equals its scope, every other scope stays enabled and the start stays `Ready`. Street Food absent disables vehicle scopes only.
5. **Deterministic report** (`DeterministicReport`): the same end state reached in two orders gives identical text; reused while unchanged and rebuilt after a transition or a different game version; line order and wording pinned; `Show` pinned for `clock.city`.
6. **Source inventory** (`HookInventorySourceTests`): production sources agree with the inventory; **red with one injected test-only owner** (`persistentcity.injected-test-owner` in a new Runtime file gives exactly two findings); red with an undeclared helper target, an extra direct `.Patch(` site, an existing direct site redirected to an undeclared target with the call count unchanged (directly and through a field assignment), attribute or bulk patching, and an extra food-contract table row; red when a declared target is renamed or an owner ID changes; the table parser agrees with the compiled `FoodContractHookTable`; counts (12, 52) and the owner-to-capability mapping pinned.
7. **Golden hash** (`CurrentWriterGoldenHashIsUnchanged`): writer 35, both seeds, hashes and lengths as F2e pinned them.
8. **Perf** (`RegistryUpdateAndReportCost`): see [F8a perf](../perf/f8a.md). Steady-state updates and a reused report are asserted at 0 B.

## In-game checks for the next batched rig

F8a is not wired to Runtime, so it adds no in-game behaviour of its own. The plan's section 7 checks that read the capability report need F8b's `pc capability status` and later slices; they are listed here so the batched rig carries them, each tagged with the slice that makes it runnable. Fixture for all: a disposable FT city on the owned rig with Persistent City, RLO and Street Food, one owned food shop and one Street Food cart owned by PC, built and aged at head. Autosave is off in the isolated test profile, checked before every measured window (also after reload) and restored at cleanup. All checks are rig-automated; none is for Peter, a playtest or a by-hand check.

1. **Hook owners under the full modpack match the F8a inventory** (no slice dependency; runnable once RIG-HARMONYOWNERS exists; locked-PC). Setup: full modpack DLL set (RIG-MODSET). Steps: load the fixture through FT `load`; read Harmony's patched-method owners for every inventory target through FT `console` (pending capability RIG-HARMONYOWNERS: dump `Harmony.GetPatchInfo` owners for a named method list). Expected: every PC owner and target in the table above is present once the city is active (bodies and inspector on), no other `persistentcity` owner exists, and foreign owners are recorded per target as F8h's input. **Existing:** RIG-MODSET, FT `console`/`load`; **pending:** RIG-HARMONYOWNERS.
2. **Capability report and modpack hook owners** (plan check 1; after F8b and F8e; locked-PC). Steps: load the fixture, run `pc capability status` and `pc capability hooks`. Expected: every required capability `active`; optional ones `active` or `off` with a reason; the hook list names every foreign owner on the inventory targets with its class. **Existing:** RIG-MODSET full set, FT `console`/`load`.
3. **Street Food absent, then returned** (plan check 2; after F8b; locked-PC). Steps: save; load without `FoodTrucks`; run 2 game hours; save; restore Street Food and load; run to the next crew opening. Expected: `vendor.streetfood absent` with Street Food's missing-API reason; shop money and receipts equal a run with no cart; vehicle rows, receipts and identities unchanged; after return, `active` and the cart requalifies. **Existing:** RIG-MODSET, FT `save`/`load`/`time`; **pending:** RIG-PCSAVE `money`/`receipts`.
4. **Driven Rides absent** (plan check 3; after F8b; locked-PC). Steps: load without Driven Rides. Expected: `clock.drivenrides absent`, start `ready`, city runs, calendar factor as configured. **Existing:** RIG-MODSET, FT `console`.
5. **RLO absent at load** (plan check 4; after F8b; locked-PC). Steps: load without RLO. Expected: no city starts; the status names `rlo.donor` as missing and does not say "start a new city"; raw PC payload hash unchanged; native customers serve the shop; no PC receipt key is written. **Existing:** RIG-MODSET build selection and raw PC payload hash/refusal-status readback.
6. **Changed required member** (plan check 5; after F8c; locked-PC). Steps: select a PC fault variant whose `NativeFoodContract` expects one H1 target under another name; load. Expected green: `food.authority off: <member> changed`, start refused with the capability wording; payload hash unchanged; no PC Harmony owner present; native customers serve the shop. **Pending:** RIG-FAULT.
7. **Changed checkout member** (plan check 6; after F8c; locked-PC). Steps: fault variant with `ProcessCustomer` expected under another signature; load and run to lunch. Expected green: the owned shop keeps the loaded hour protected, gets a scope reason, begins no new PC receipt and is handed back at the next drained accounting boundary; the city runs. **Existing:** RIG-MODSET, FT `time`; **pending:** RIG-FAULT, RIG-PCSAVE `receipts`.
8. **Changed optional research contract** (plan check 7; after F8b and F8c; locked-PC). Steps: research-billing fault point; load; run past one nightly marketing bill. Expected: `research.billing off` with the member named, no research charge, native campaigns unchanged, city runs. **Existing:** RIG-MODSET, FT `time`; **pending:** RIG-FAULT, RIG-PCSAVE.
9. **RLO Flare switched off mid-session** (plan check 8; after F8d; input-only, because it toggles an RLO option in the running session). Steps: run 1 game hour; switch RLO's Social Media option off; run 1 hour; save; switch it on; run 1 hour; save; reload. Expected green: `rlo.donor off (transient)`; city runs; the in-window save succeeds; resident IDs, names and count unchanged; reload continues. **Pending:** RIG-RLOTOGGLE, RIG-PCSAVE; **existing:** FT `save`/`load`.
10. **Mechanism report** (plan check 9; after F8f; locked-PC). Steps: `pc capability business <shop>` and `<cart place>`. Expected: `shop.fullservice-bag.v1` and `vehicle.streetfood-api.v1`; a shop with a non-historical product reports `shop.dynamic-catalogue off`. **Existing:** FT `console`, RIG-SHOPEDIT `shop_edit`.
11. **Foreign prefix on checkout** (plan check 10; after F8h, conditional; locked-PC). Steps: a rig-owned test Harmony owner adds a prefix on `RetailBusinessSimulator.ProcessCustomer`; load. Expected: `food.checkout.shop off: foreign prefix by <owner>`; shops native; zero PC receipts; removing it and reloading restores ownership at the next accounting boundary. **Pending:** RIG-FOREIGNPATCH.
12. **Performance** (plan check 11; after F8b; locked-PC). Steps: in a clean RIG-PERF-UNION window at default caps, run `pc capability status` once per game minute for 30 game minutes. Expected: mod-work p95 < 5 ms; every >= 30 ms sample listed with its component; whole-frame metrics reported separately. **Existing:** RIG-PERF-UNION `perf_window`, FT `console`.

Rig capability request added by this slice: **RIG-HARMONYOWNERS** (check 1), dump Harmony patch owners and kinds for a named method list in the running game. The others (RIG-RLOTOGGLE, RIG-FOREIGNPATCH, RIG-FAULT, RIG-PCSAVE) are the plan's.
