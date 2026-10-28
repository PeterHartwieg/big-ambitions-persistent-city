# Physical view caching, 27 October 2026

This change implements finding 6 of [the architecture performance review](2026-10-27-architecture-performance-review.md). The physical layer repeated native hierarchy searches and reflection reads on per-frame and per-physics-step paths. It now does each of those searches at most once per epoch or frame. Which bodies are owned, the order of selection, the attempt caps and the cooldown writes are unchanged. Ownership loss is seen at the same call as before except for one bounded relaxation. Three per-frame reads add smaller ones. All four are listed under semantic relaxations.

No game was launched and no rig was touched. Everything here is offline harness evidence plus one compile against the local Mac game assemblies.

## What changed

### Ownership verdict memo (`Runtime/PhysicalVehicles.cs`)

`PhysicalVehicleBody.OwnershipLossReason` still runs every reference, state and geometry check live, in the same order: released, active in hierarchy, rigidbody, native index, prefab, rigidbody identity, front marker identity and parenting, finite marker position, and preset path reference. Only the component search (`RandomAmbient`, three `GetComponentInChildren` calls and `VehicleScriptOwnership.NativeOnly`) is memoized. The memo is keyed by a static ownership epoch and by `Time.frameCount`, and a verdict is reused only when both match.

The epoch advances at these points:

- the first statement of `PhysicalVehicles.Update`;
- the first statement of `BeginNativeFixedStep`, also when the adapter is inactive;
- the start of `Release`, so a despawn decision always comes from a fresh search;
- after every native mutation the adapter performs, in a `finally` so a throwing call still ends the epoch. All five go through wrappers: `LoadNative` (activation in `TryAcquire`), `TargetNative` (`SetVehicleTargetWaypoint`), `StopNative` (`SetVehicleAction` stops in `Update` and `MayRequest`), `RemoveNative` (`RemoveVehicle` in `Release`, the retire sweep and the `TryAcquire` rollback) and `ExpireNativeStop` (the temporary-stop expiry in `ObserveCrashExpiry`).

The memo reads its stamp before searching, so a search that itself advances the epoch leaves a stale stamp and the next call searches again. A search that throws stores nothing. `TryAcquire` still calls `RandomAmbient` directly right after `LoadVehicle`, and its final `body.Valid` runs after `TargetNative` has advanced the epoch, so activation always sees a fresh verdict.

Before, each owned body paid five component searches per physics step (force, rotation, two in `EndNativeFixedStep` and the final command check), up to two more inside the permission callback (`TryPeekReached`, `Position`), and one per hook call on native per-vehicle paths. Now it pays one per physics step, one per frame, and one after each native mutation the adapter makes.

### Reservations, renderer visibility and pool membership (`Runtime/AmbientPedestrians.cs`)

`RefreshReservations` reads RLO at most once per `Time.frameCount`. `Update` and the early checks in `EndCyclePrefix` share that read; since 28 October the prefix forces a live read again right before it holds or retires a body, including the door-arrival retirement, so a takeover never acts on a lease RLO took earlier in the same frame. `Dispose` passes `force: true` before its restoration pass. The stamp is written after a read returns, so a read that throws can be retried in the same frame.

`IsOffscreen` answers once per pedestrian per frame from a dictionary cleared at each new frame. The renderer search moved into `RenderersOffscreen` unchanged.

`Update` checks membership of `spawner.activePedestrians` through `InActivePool`. Pools of fewer than 16 entries still use `List.Contains`. Larger pools are hashed once per frame and rehashed if the list reference or its count changes. Unity objects hash and compare by instance ID, and `List.Contains` compares the same way for non-null references; every call site already rejects null and destroyed pedestrians before the membership test. `EndCyclePrefix` and `Dispose` keep their live `List.Contains`. The one-retire-per-frame and one-resume-per-frame rules are untouched.

### Taxi classification (`Runtime/PhysicalVehicles.cs`, `Runtime/PhysicalTrafficHooks.cs`)

`IsTaxiThisFrame` classifies each pool vehicle once per frame, and `TaxiNeedsReplenishment` counts active taxis once per frame. `SelectIdle` uses `IsTaxiThisFrame`. Its ordinary-ambient checks (`RandomAmbient` on the candidate and on group members) stay live. `Activate` keeps its own uncached count.

### Selection without LINQ (`Runtime/PhysicalPopulation.cs`)

The six-stage pipeline is now a loop over `simulation.State.Citizens` that applies the same filters in the same order and short-circuit sequence. The filters are next event after now, not bound, not reserved, cooldown, phase or queue or outdoor, then `PositionOf` and the radius check. Passing citizens go into a reusable `List<SelectionCandidate>` with their keys captured once: last attempt time (negative infinity when never attempted), distance to focus, ordinal ID and roster index. `SelectionCandidate.ByPriority` sorts on those four keys. Roster index reproduces the stable tie-break of `OrderBy`/`ThenBy`, and with unique citizen IDs it is never reached. The keys are the values the old pipeline computed at sort time, because nothing writes `attempted` between filtering and sorting. The loop body, the four-attempt cap, attempt recording and the separate walker and car caps are unchanged.

`Diagnostics` and the selection's starting walker count read running counters (`personBindings`, `carBindings`, `queueBindings`). `Bind`, `Unbind` and `TryJoinQueue` are the only places that register a binding, remove one or give a registered binding a queue. `Unbind` removes whatever binding holds the ID, as before, and decrements by that binding's flags. The per-frame `bindings.Values.ToArray()` in `Update` became a reused snapshot list, taken out of its field while in use.

## Semantic relaxations

1. **Foreign components on owned cars.** A foreign or service component added to an owned car mid-epoch is noticed at the next epoch boundary instead of at the next call. That is at most one frame or one physics step later, and sooner if the adapter mutates native state in between. Until then, the memoized verdict can let `DeferNativeForce`, `ApplyNativeRotation`, the final command in `EndNativeFixedStep`, `Owned` in the native hooks and `Position` treat the body as still owned. `Release` searches fresh first, so the adapter never despawns a body claimed in the same epoch. The same holds for a claim made inside the `CommandLimit` permission callback. That step's final force and rotation can still be applied, and the claim is seen at the next step.
2. **Taxi count.** The active-taxi count is taken at the frame's first `AddVehicle` draw. A taxi native spawns earlier in a frame is counted from the next frame's first draw, so several draws in one frame can each see the same shortfall and spawn more than one taxi. Classification is also fixed for the frame, so a taxi controller added or removed mid-frame takes effect next frame.
3. **RLO reservations.** Withdrawn on 28 October after two review rounds flagged it: `EndCyclePrefix` reuses the frame's read only for its early exits and reads live before every hold or retire. The per-frame memo remains for `Update`.
4. **Renderer visibility.** `IsOffscreen` keeps its first answer for the frame. Unity updates `isVisible` only at render time, so the only possible difference is a pedestrian whose renderer set changes within the frame.

The active-pool set is not a relaxation. It rehashes on any change of list or count, and the only native call between two lookups in one `Update` is `OnRelease` inside `Hold`, which the code documents as not returning the pool lease.

## Verification

All commands ran from the repository root on 27 October. Before the change, the three harnesses passed with 235 ownership assertions, 65 native command checks, and 27,561 selection checks (positions 93,665 to 56,231, hierarchy reads 88,308 to 33,300).

- `python3 tools/vehicle-ownership-regression/run.py` passed: `PASS 361 actual Runtime vehicle ownership assertions` and `PASS epoch call sites: Update, BeginNativeFixedStep and Release advance first; 5 native mutation wrappers advance in finally; no raw native mutation elsewhere`. The runner now also extracts the epoch members, the five wrappers and the taxi block. New cases cover verdict reuse within an epoch (one search, then none across 50 calls of `Valid`, `OwnershipLossReason` and `Position`), every reference and state loss detected inside an epoch with no component search, invalidation by an epoch advance and by a new frame, a foreign component unseen mid-epoch and seen at the next epoch, a claim made inside each wrapper's native call seen as soon as the wrapper returns (also when the native call throws), a search that advances the epoch or throws, per-body memos, and the per-frame taxi count with its one-frame lag. The legacy predicate stays the oracle, and a new `OldLoss` mirrors the legacy body check live. Its own searches are dropped from the query log, so the harness counts only production searches. A one-off mutation check against scratch copies failed the harness as expected in four ways: ignoring the epoch, ignoring the frame, removing the `finally` from `RemoveNative`, and removing the advance at the start of `Release`.
- `python3 tools/native-command-regression/run.py` passed: `PASS 72 native command/step checks`. The new checks are that each fixed step advances the epoch exactly once, also while inactive, and that one epoch spans the step's force, rotation and final command. The ownership-loss-inside-permission-callback case still passes. With the Managed directory and the scratch DLL as arguments it also printed `PASS read-only native IL: grounded skip, exact production pair preflight, four malformed-shape refusals, no later direct Rigidbody reads`.
- `python3 tools/physical-selection-regression/run.py` passed. With the original fixtures it printed `PASS 27561 checks; positions 93665->56231; hierarchy 88308->33300`, identical to the baseline run. I then widened the fixtures with an off-axis focus, which gives non-integer distances, and many distinct past attempt times. It printed `PASS 27561 checks; positions 93665->55737; hierarchy 88308->33300`. The after-count changed only because the extra random draw changes the resident mix, and the frozen baseline's count does not depend on that mix. The selection marker is now a comment line naming the harness, and a second marker extracts the `SelectionCandidate` block. `baseline.json` is unchanged.
- `python3 tools/build.py --game-managed "$HOME/Library/Application Support/Steam/steamapps/common/Big Ambitions/Big Ambitions.app/Contents/Resources/Data/Managed"` passed in a scratch copy of Core, Compatibility, Persistence and Runtime, with C# 9, nullable checks and warnings as errors. DLL SHA256 `49b7f6fb1d0e7a3201a51f2985993166782e5c625153880f9b5683a6d1aa41cf`. The copy includes another session's in-flight Core and Persistence edits, and `build/PersistentCity` in the repository was not written.
- `dotnet test` was not run. `tests/` and Core belong to the concurrent session.

## Not verified

No game run. Nothing here measures frame time, allocation or native call counts on the rig. The taxi count and ownership memo run in offline doubles. The `AmbientPedestrians` changes are only compiled, because that class has no Unity-free harness. The equality argument for the active-pool set rests on Unity's documented `Object.Equals` and `GetHashCode` behaviour, not on a test.

## Items left

- **One native acquisition per frame (item 5).** Not done. Spreading one tick's attempts over frames means each later attempt sees changed bindings, reservations, focus and journey samples, and records its cooldown at a later time. The differential compares one synchronous pass and asserts identical attempted IDs and cooldown values. It would need a frame model it cannot check against the game, so it would stop being a like-for-like comparison.
- **Appearance JSON check (item 6).** Not done. The local `CharacterData` has three nested lists (`blendshapes`, `elements`, `skills`), two `Color32` values and an `ItemInstance` reference besides its scalars. A field-wise check equal to the Newtonsoft comparison would need a deep snapshot before `UpdateVisuals` and Newtonsoft's member rules for `ItemInstance`. An incomplete version would weaken the guard against silent randomization.
- `PhysicalVehicles.Update` and `EndNativeFixedStep` still copy `bodies.Values.ToArray()`, and `EndNativeFixedStep` still uses a LINQ `Any`. They are small (24 bodies at most) and out of this item's scope.
- The review's `RouteObjectDiscovery.Prune` and `TrafficRoadNetwork.NodeCurrent` bullets are untouched. `TrafficRoadNetwork.cs` belongs to the concurrent session.
