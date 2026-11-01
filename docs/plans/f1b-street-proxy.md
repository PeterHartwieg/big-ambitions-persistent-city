# F1b street proxy investigation

1 November 2026, offline; base `dbddada2e35d0ac2ac826985214741392a406364`.

**Renderer NO-GO; read-only tooling GO.** No unique semantic door pair or independent aperture residual is established by the available evidence. No proxy, render setting, owned graphics resource, Harmony patch or save change is introduced. F0 and the E2c forced-off guard keep their existing behavior. Customer presentation/adoption remains blocked by F1's single-economic-owner finding and is outside this investigation.

## Rig #2: what the small shop proves

Read with `git show`, without merging `origin/codex/pc-rig-locked-combined-20261102`. The report is dated 2 November; it explicitly records the actual run on 1 November. Its C8 `geometry-summary.json`, six doorway JPEGs and README belong to the pinned candidate `dbddada`. They establish one own coffee shop, **30 3rd Street**, size `ba:buildingsize_a`, version 1. All six geometry samples show one exterior transform and one non-elevator interior exit, detached layout origin (1000,0,0), unit scales. On samples establish floor y=0 and F0 eligibility; off/inside-reload samples' null cached floor is not evidence of a missing native floor.

The exterior frame is approximately (230.262573, 0.09000015, 21.27832), yaw -90 degrees; the exit-zone transform is (999,0,-4.43), yaw approximately 0 degrees. A hypothetical same-axis street-to-interior mapping is therefore yaw +90 degrees and translation approximately (977.72168,-0.09000015,225.832573). This fits the one chosen origin by construction. Opposite-facing frame conventions would give another candidate; neither convention is validated by the report. F0's <=3 m floor test cannot choose between them.

The report's exit `id` changes (-38555532 through -44794402, with another value on reload). It is not recorded as `ExitZoneDespawner.exitToZoneId`, and exterior `BuildingEntranceDoor.doorId` is absent. **One pair by cardinality does not prove unique semantic correspondence.** There are no independently paired window corners, jambs, aperture dimensions or second door frames. The six repeated captures of the same doorway do not supply six independent constraints. A consistent rigid transform across more than one aperture/edge is **unknown**, not passed or disproved. The doorway images show the retained interior and dark exterior, consistent with the documented drawing no-op; they cannot establish proxy depth or lighting.

Medium/large own shops and an elevator fixture were absent. There is no bounded renderer/material/LOD inventory. F1 renderer and customer hand-off are explicitly BLOCKED. The report also flags a separate combined-mod-work 30.9925 ms spike; passing p95 values do not clear that flag or attribute it to F1b.

## Independent vanilla audit

Only the supplied Steam Managed DLLs were inspected with local ILSpy; output stays ignored in `build/f1b/vanilla/`. No external implementation or Workshop material was accessed. `BigAmbitions.dll` SHA-256: `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911` (same input as F1).

`BuildingEntranceDoor.doorId` and `ExitZone.despawner.exitToZoneId` are integer semantic fields. `BuildingManager.GetFirstExitZone` tries that equality, then falls back to a primary/available exit. Exit routing also selects exterior doors by `doorId`. Our diagnostic must require exact, nonnegative, one-to-one ids and reject every duplicate/missing association; it must never reproduce native fallback. `ExitZone.door` is an animated leaf: its open/closed rotations (and elevator/drive-in world positions) have separate cached state. A current leaf transform is unsuitable as a fixed jamb. The diagnostic labels exit-zone roots as **candidate frames**, never proven apertures.

`CityBuildingController` has separate base/fade/hide/LOD renderer collections; property blocks and native LOD updates have independent writers. An ancestry owner label is useful metadata, not complete ownership proof. Mesh/material names and bounds establish neither shader compatibility nor clipping. Diagnostics must read shared meshes/materials only; accessing instantiated `material`, `materials`, volume `profile`, native setters or scene cloning is unnecessary and prohibited.

## Remaining decision gates

The tooling must collect exact ids and frames, independent explicitly identified paired aperture/edge points with residuals, conservative radius classification, capped renderer metadata and owner uncertainty, body counts and lighting/volume snapshots. Missing or capped observations must remain visible refusals. A single fitted origin or synthetic frame axis is never independent validation.

Even green geometry and inventory would leave aperture occlusion/shared depth, strict clipping of boundary-crossing geometry, property-block/LOD semantics, shadows/material compatibility and indoor light influence unproven. Only subsequent prepared-view evidence can settle those. The +2-4 ms whole-frame figure remains a planning estimate, separate from mod-work p95 <5 ms and the >=30 ms spike rule.
