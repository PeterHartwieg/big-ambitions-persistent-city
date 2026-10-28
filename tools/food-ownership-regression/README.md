# Automatic food authority validation

This directory contains an offline harness and a separate, guarded native diagnostic helper. No script here deploys, launches, connects to a rig, or changes a save. Native validation belongs to the parent task after the persistent traffic gate and the usual owned-rig backup checks.

## Build and offline checks

```sh
dotnet run --project tools/food-ownership-regression/FoodOwnershipRegression.csproj
dotnet test tests/PersistentCity.Tests.csproj --filter 'FullyQualifiedName~FoodAuthorityTests|FullyQualifiedName~FoodReceiptEvidenceTests'
python3 tools/food-ownership-regression/prepare_candidate.py --output build/ownership-k1/source --factor 1 --game-managed "$MANAGED"
python3 tools/food-ownership-regression/build_probe.py --campaign build/ownership-k1 --persistent-city build/ownership-k1/source/build/PersistentCity/PersistentCity.dll --game-managed "$MANAGED"
```

Set `MANAGED` to the already verified local native reference directory. Both builders refuse an existing output directory and write hash manifests. The candidate retains disabled configuration. K1 is compiled into that snapshot only; K2 uses a separate snapshot and fresh comparison city after K1 operations pass. Never hot-change a loaded city's factor or wallets.

## One bounded native sequence

1. Under the parent's existing rig lock, preserve/verify the original profile, preferences, install manifest and source checkpoint. Install only through the rig's established guarded workflow. Load an existing disposable `FT Food Routines ...` fixture source and pause. Use the bridge `save` operation with a new name such as `FT Food Routines Ownership K1-20261024-a`. Verify that the source name still exists and the new name is distinct. No ordinary save is involved.
2. Send the helper this explicit one-shot request while the new copy is paused:

   ```json
   {"id":"fresh-k1-a","op":"ownership-fresh-copy","expectedName":"FT Food Routines Ownership K1-20261024-a","sourceName":"THE EXACT EXISTING FT Food Routines SOURCE NAME","confirmation":"new-copy-original-backed-up"}
   ```

   The helper preserves original modData in a create-new local evidence file, shuts down the old runtime, removes only `persistentcity:` keys from this new copy, and records a one-shot diagnostic marker. Keep paused. Bridge `save` this same new copy and then `load` it. Do not advance between clearing and reload. Never call this operation on an established supported city. Its marker prevents repeating cleanup on an initialized copy.
3. Send `ownership-status`. Require current household finance policy, factor1, no uncertainty, stable physical population, and a saved authority manifest even if no scopes exist yet. Existing player fixtures will be acquired automatically after a later hour. If using a truly empty business baseline, `neighborhood-candidates` remains read-only, and `ownership-fixtures` accepts the existing neighborhood setup fields (`shops`, `skill`, `opens`, `closes`, `menus`, `prices`) before any scope/sale. It uses native fixture setup, not manual enrollment. Opening inventory/equipment are a declared fixture endowment; no replenishment is granted.
4. Capture `ownership-status` immediately before and after one natural native hour. Require old queued native work to finish, then exactly one owner for the new hour. Compare receipt/order count, native stock/bags, consumer held/spent cash and native account settlement. `audit-start`, `audit-end`, `frames-start` and `frames-stop` remain available from the frozen audit helper. Do not run the old `menu-contract-check`: it targets superseded private opt-in hooks and intentionally creates direct test sales.
5. On a paused diagnostic branch, send `ownership-menu-native` with the explicit fixture `shop` place ID. New mod commitments must stop immediately; native current-hour demand must remain suppressed until the next drained boundary. Advance one hour and capture status: future native entries resume, no past hour is rebuilt. Send `ownership-menu-supported` in the same helper session, advance to a later boundary and verify automatic reacquisition. This toggles only advertised eligibility for the ownership fault test; discard the branch before ordinary operating measurements.
6. Use ordinary native UI on the operating copy to remove a shift temporarily and let stock deplete, then restore practical shifts and buy supplier replenishment through the game. Ownership/generation must remain unchanged throughout zero staff/stock and recovery. Status supplies separate service/stock evidence. Record purchases, native paid supplier cost and deliveries; do not refill cargo, restore an inventory snapshot, synthesize payroll or use diagnostic opening endowments as replenishment. The old helper contains no verified automatic supplier-purchase API, so the normal game UI is the existing native path here.
7. Use a separate branch while a supported shop is still native and the player is inside with active native customer bodies. Leave it eligible across a native hour. Acquisition must defer while bodies hold their baskets, and finish after natural completion/normal exit at a later boundary. No forced customer cleanup is part of this helper. On another discardable branch, `ownership-reset` with explicit `shop` and `confirmation: "discard-this-fixture-copy"` calls the actual native registration Reset while paused. Record the generation/suspension and absence of stale mod checkout. Do not restore that business by copying raw native fields; discard the branch or establish a fresh fixture through normal setup.
8. Save/reload an owned operating copy and compare the first loaded hour's authority, receipt keys/terms, wallets, stock and orders. A naturally proven paid receipt remains evidence-only and cannot consume stock again. Offline tests cover pending-marker retention and paid-marker reconciliation. If a controlled uncertain native fault is needed, design its exact native point before injecting it; this helper does not fabricate a pending/paid result or treat missing evidence as refusal. Any unexplained or naturally accumulating uncertainty blocks release.

For helper operations, use the existing diagnostic request transport: atomically replace its `request.json` with a unique `id` and operation, then read `response-<id>.json`. The existing PC `protocol.probe_call` does this under the parent's lock and baseline checks; the Mac workflow uses the corresponding local request path. Preserve responses plus native logs, authority manifests and build hashes with the campaign. Stop, retain evidence, and restore/verify preexisting rig files/preferences before releasing the lock.

Native results must be reported separately from the offline field/callback harness. Do not widen this into another full economic matrix: one ordinary K1 operating sequence, short handoff branches, and one in-flight reload establish this boundary before the K comparison.
