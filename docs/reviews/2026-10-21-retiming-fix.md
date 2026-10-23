# Traffic retiming fix — 21 October 2026

The readiness and queue-wakeup failures found by the Windows modpack smoke are fixed in DLL `6e0e5b58776a5a4430b4b585e03f7233d1dc5dedfd94200cb7f603a44ea55de1`. The frozen source passes **915 tests**, the Astra / Sol / Opus 5.5 / Grok 4.7 panel, and **65 bounded Windows regression checks**. This resolves the demonstrated retiming blocker. Full recording, genuine driving and performance clearance remain unverified.

## Cause and fix

Three related floating-point failures were isolated. First, `RescaleTime` scaled already-rounded absolute readiness independently of the reconstructed residual route. A 60× change amplified their difference beyond the existing four-ulp validation tolerance. Readiness now retains any later constraint and cannot precede reconstructed motion completion.

Second, independently scaling queue events could leave one later than a recomputed signal/curb wakeup. Retiming now reconstructs derived wakeups on its detached kernel through the existing `Reconsider`/`Schedule` logic. Safe early events remain; late events move forward. Movement still checks readiness, signal, headway, capacity and physical endpoint observation. Queue order and merge cursors stay intact; repaired events can receive different tie-order values.

Third, the original signal calculation reconstructed a boundary from each queried clock. Cancellation made the same upcoming green move as the clock advanced, so an ordinary live snapshot could become invalid before another retiming even started. Signals now use canonical absolute cycle boundaries and half-open green intervals, with bounded correction at rounded cycle boundaries. A positive green window includes its exact canonical start even if no later timestamp fits within it. Periods below the clock's representable resolution fail closed. No per-frame link scan or polling loop was added.

The first two candidates failed native repetition and stayed confined to the rig. The third candidate's stable signal arithmetic passed offline tests, but Astra found it rejected 291 of 1,000 valid old synthetic snapshots. It was never installed. The final revision adds an explicit migration for that compatibility issue.

## Save compatibility

The outer city schema remains **7**; the nested traffic-kernel schema is now **2**. A schema-1 snapshot must first pass all structural, ownership, readiness and event checks, including the exact legacy signal arithmetic. Only then can its detached derived schedule migrate to canonical boundaries. Current schema-2 public restore is strict and never reconstructs a malformed schedule. Unsupported kernel schema 3 and later, missing/late legacy events and inconsistent readiness remain rejected.

`SaveCodec` validates the whole legacy city before migration, preserves physical leases and source payload bytes, validates the migrated result, and reports `Migrated=true` on that first read. A subsequent save/read uses schema 2 without another migration. Reconstructed event-counter overflow rejects atomically. Older builds cannot read the new nested traffic schema; retain backups when rolling back and do not overwrite a newer save with an older build.

Production changes are limited to `Core/TrafficKernel.cs`, `Core/TrafficContracts.cs` and `Persistence/SaveCodec.cs`. The frozen tree has 49 production C# files / 164 source-manifest entries. Concurrent CityApi work in the moving checkout is excluded. Resident identities, routes, RLO data and traffic admission policy are retained.

## Tests and review

- **915/915 tests pass**, including 29 new retiming, signal and migration/persistence cases. The existing future-kernel rejection control now uses schema 3. Tests cover delayed readiness, mapped/unmapped residual movement, repeated rates, physical endpoints, signal stability, signed offsets/large clocks, deterministic scheduling, FIFO/merge rotation, malformed legacy/current state, migration marking and atomic overflow.
- Six of seven initial regressions fail against the original installed baseline; the delayed-readiness control passes. Two expanded cases fail the readiness-only candidate. Eight of ten new signal cases fail the old input-relative signal implementation, including independent ordinary-advancement and post-retiming reproductions.
- **15/15** retained-state/rate combinations pass final retiming plus serialization/reload. **20/20** synthetic three-car queue/signal/curb/rate scenarios pass. These are additional offline checks.
- Astra independently verified all 164 source hashes, ran 49 focused tests, 200,000 signal property scenarios, all 1,000 old-code-generated snapshots through migration/advancement/validation, and collapsed-window boundary cases. Sol verified the manifest, 100,000 signal probes and migration/rejection cases.
- Astra, GPT-6.1 Sol, Claude Opus 5.5 and Grok 4.7 approve the final source; no blocking findings remain. Opus and Grok inspected code and supplied execution evidence, rather than running native tests. Model metadata records `claude-opus-5-5` and Grok's actual backend. Intermediate approvals do not substitute for the final reviewed revision.
- Mac-reference and retained Windows-reference builds are byte-identical at the hash above. Game, RLO and diagnostic helper DLLs are excluded from the package.

## Windows regression

The isolated Direct3D 11 background rig uses the full installed modpack, 30 FPS cap, four workers, exact RLO 0.45.0 DLL `2f360a36…`, and unchanged enabled Persistent City limits: 48 pedestrians, 24 cars, radius 120 m. The normal game was not used for testing.

While the rig was stopped between candidates, an external installation changed Food Trucks and restored the previously reviewed Persistent City baseline. The guard caught the unexpected hash before any overwrite. The updated 97-entry manifest was verified and a fresh profile backup established. Final testing preserves that newer Food Trucks package (`1814c255…`, Core `ccb1758f…`, BridgeControl `6f64e4f4…`); it is not the earlier smoke's `e033780a…` bridge. This is a changed modpack baseline, so the runs are not a controlled performance comparison.

- Loaded the original failing `FT PCity Windows Modpack 20261021 Speed` checkpoint, retaining its stored namespace hash `d4560759…` until the explicitly named new save. Its legacy traffic schedule migrated successfully.
- Two 60× bridge-clock cycles, each followed by paused catchup and a return to 1×, remained healthy. Native 6× speed withdrew physical bodies; returning to native 1× reacquired them.
- The bridge truck trip completed without disabling Persistent City. Its log identifies simulated accelerated travel and parking, so it is not a real Driven Rides test.
- All 957 resident identities and 400 immutable RLO donors stayed identical through speed changes and restart. Sampled bodies remained unique, valid and within limits, matching saved names/appearance seeds. All 6 samples with physical diagnostics reported zero traffic overshoots. The remaining 5 samples had zero bindings and unavailable diagnostics; they do not establish an overshoot count.
- `FT PCity Retiming Fixed 20261021` saved exactly the live serialized namespace. A fresh process loaded the same saved payload, validated the save identity, retained all citizens/donors and advanced again. All paused checkpoints caught up to zero lag. Maximum sampled lag across these accelerated/checkpoint samples was 165.55 game minutes; this is not a normal-speed performance measurement.
- Both process logs contain no Persistent City stop or either retiming exception. Evaluator: **65 checks passed, zero failed**, across 11 selected running/checkpoint samples. Read-only snapshots add diagnostic overhead.

## Cleanup and remaining limits

The rig was stopped and the temporary helper removed. All 3,260 files in the refreshed baseline (including 977 existing save files) were restored/verified, and the 97-entry rig manifest verified. The fresh backup is `C:\Users\Peter\HART-Backups\persistent-city-retiming-r4-20261021T111733Z`. The original pre-task backup remains at `C:\Users\Peter\HART-Backups\persistent-city-retiming-fix-20261021T104317Z`. New disposable FT checkpoints remain. Final installation and preference restoration are recorded in the deployment receipt.

The existing dense-population backlog has no controlled FPS comparison. Genuine Driven Rides behavior and the earlier Mac watchdog result still need separate testing. HMP's Bakery Help and Hyperreal guarded hooks withdraw on the private game. This short disposable-save campaign does not cover every business workflow, established production saves, weather, a whole day or recording cameras. The [original Windows smoke](2026-10-21-windows-modpack-smoke.md) remains historical evidence; this report supersedes its unresolved-retiming status only.

Evidence root: ignored `build/retiming-fix-20261021/` (frozen snapshots/manifests, builds, tests, review rounds, native responses/logs, backups and receipts). Raw game state remains private and is excluded from distribution. Private package configuration defaults disabled; authorized installation preserves each user's current configuration byte-for-byte.
