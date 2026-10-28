# Static-map object lifetime — isolated PC check, 26 October 2026

Peter requested a quick native check of whether movement/render distance recreates the gates and subway stations used by route discovery. In this rendered Windows check, **all 4 gates and 20 stations retained their original instances** across distant player/camera moves and a return. No production optimization was installed.

## Method and result

Used the existing isolated Windows agent player in windowed mode and the disposable `FT Food Routines Venues Settled 20261023` city. Its existing production mod packages were retained. A temporary standalone observer tracked original Unity component references every frame, including destruction and active-state changes. At six checkpoints it independently enumerated all gates/stations, including inactive objects, and recorded instance IDs, scene handles, positions and the actual main-camera position. All 18 compile-reference assembly hashes matched the installed rig.

The completed sequence visited Midtown northwest, Garment District harbor, Industry City/Sunset Park and the Hamptons, then returned to its starting position. It used the game's player-position operation through the owned FT bridge, with eight seconds at each target and ten seconds after returning. It did not create synthetic gates or stations.

| Observation | Result |
| --- | --- |
| Completed observation interval | 44.38 seconds / 3,883 frames |
| Maximum separation between observed camera positions | 3,400 metres |
| Native inventory at every checkpoint | 4 gates / 20 stations |
| New, replaced, missing or destroyed tracked objects | 0 |
| Active-state changes of tracked objects | 0 |
| Scene load/unload events during observation | 0 |
| Read-only evidence assertions | 42 passed / 0 failed |

The first attempt completed two moves before the next teleport was refused because the diagnostic specified ground height zero where the walkable surface was at approximately two metres. That attempt is preserved separately. The completed retry let the native bridge select the walkable surface. Its baseline object IDs also match the first attempt's baseline; no recreation occurred between attempts.

## Interpretation and limits

The observed gates and stations remained loaded across distant camera/player movement, including across the Streets, IndustryCity and TheHamptons areas. This supports retaining their inventory instead of repeatedly rediscovering it merely because the camera moves. The earlier discovery fixtures tested deliberately inserted objects; they did not establish spontaneous additions during ordinary play.

This is a short native lifetime check, not a proof about every game lifecycle, future mod, interior transition or save reload. It did not test removing the periodic scan or measure the resulting performance. An event-driven implementation should preserve explicit load/unload, city replacement and destroyed-object handling. Live gate eligibility, road closures, traffic forecasts and new-journey route planning are separate concerns and are not disabled by this finding.

## Cleanup and evidence

The isolated game was stopped, the observer removed, all **4,667 pre-existing rig files** hash-verified after restoration, and preferences and installation manifest restored. Ownership was released with zero pending operations. The ordinary profile was untouched. No ordinary-save deployment, production-code change, RLO modification or release occurred.

Private evidence is in ignored `build/discovery-lifecycle-20261026/`: observer source/build manifest, matching-reference receipt, `verification.json` with input hashes, captured observations under `evidence/`, the retained first attempt, and cleanup/final-status receipts. No raw save or game assembly is included in this report.

## Subsequent authorized implementation

Peter then requested removing the recurring scan. The development source now captures the inventory during initialization and retains it without timed rediscovery. It preserves sliced loading and explicit lifecycle recovery: scene load/unload, city replacement, lost road scene, explicit invalidation and destroyed/ineligible retained components. Ordinary updates only check the retained references; they perform no world-hierarchy discovery. Newly injected components without any lifecycle/invalidation signal wait for the next explicit discovery. Current access, station eligibility, road closure and traffic checks retain their existing behavior.

The changed discovery harness passes **675 assertions**; its new idle invariant fails against the old implementation. The integration harness passes **265 routing-invalidation checks**, and the frozen production source compiles against native game references with warnings as errors. Logs/source hashes and the private candidate are under `build/discovery-load-once-20261026/`. This implementation has not been installed or measured in the game; the PC evidence above validates the lifetime assumption on the prior installed build, not the new candidate's performance.
