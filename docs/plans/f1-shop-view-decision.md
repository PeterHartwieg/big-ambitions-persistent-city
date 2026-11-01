# F1 shop view: drawing decision

1 November 2026, offline only. Base: `51ce709e2696648a189a17f09890561446b2d75d` on `codex/pc-f1-shop-view`.

**NO-GO at task step 1.** None of the evaluated approaches has sufficient evidence to satisfy the complete F0 drawing contract. Stop before steps 2-4: no renderer, door hand-off, new diagnostics bridge, patch, setting or saved state is added. The existing F0 physical-retention experiment and E2c guard remain unchanged. This is an evidence limit for this implementation attempt, not a claim that a safe renderer is impossible.

## Inputs and evidence boundary

Read AGENTS, CHECKS, README, roadmap, design, B-E plan, the complete Track F plan, F0/E2c checks and the current interior adapter/state. Independently inspected only vanilla types in the supplied Steam Managed directory using local ILSpy. Selected output stays ignored under `build/f1/vanilla/`; no decompiled source or assemblies belong in Git. No game, scene/Addressables inventory, raw save, rig/profile/bridge/lease or foreign mod implementation was accessed. No E1 branch or code was used.

| Read-only vanilla input | SHA-256 |
|---|---|
| BigAmbitions.dll | `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911` |
| ExternalPlugins.dll | `7d43e9d42d2f464674cff045bcb048c48e0f87aec0a987baa34122ac6e3415d1` |
| BigAmbitions.InteriorDesigner.dll | `04bab0df97d0bf7eb4c04b299e1b59bd81c254b5f57e8edca82f661b3ef50c5c` |

The following are independently written observations of that build, not copied native code. Method names make the findings reproducible against those inputs. They establish code behavior; they do not establish an individual shop's scene geometry or rendered result.

## Candidate evaluation

The required view has a strict effective radius of 30-120 m (default 60 m, further limited by the street setting), coherent indoor/outdoor projection and depth, preserved indoor lighting/volume, and complete restoration on every lifecycle exit. F0's height test establishes eligibility only. It supplies neither a street-to-layout transform nor proof of a visible aperture.

| Independently considered approach | Useful property | Evidence preventing acceptance |
|---|---|---|
| Enable nearby native street renderers; leave the indoor main camera alone | No layout relocation | `CullingGroupController.CreateCullingGroup` binds the main camera but measures distance from the player, which is inside the detached layout. Its 60/200 m bands are not the requested entrance radius. Private pending bands and distributed dispatches can overwrite an enable sweep. Even a correct entrance reference would not transform street vertices into the indoor frustum. **Reject.** |
| Translate/override the main camera to the exterior entrance | Native street geometry need not move | One view matrix applies to both spaces. Translating the camera makes the detached interior appear displaced too; changing the camera alone cannot remove the relative displacement. `CameraHelper` also owns Cinemachine priorities/blends, while `IndoorLightVisibilityManager.LateUpdate` uses the main camera frustum every other frame. A street camera override can change indoor light visibility even without writing the volume. **Reject as a complete shop view.** |
| Render a bounded street pass into the indoor main-camera view, or use a second camera/render texture | Could keep native simulation and layout unmoved | A separate outdoor transform per pass can solve projection in principle, but requires a verified door transform, aperture/depth composition, distance clipping for boundary-crossing meshes, compatible materials/shadow passes, and render-pipeline ordering. A texture alone supplies no shared depth at the door. No inspected vanilla API establishes this composite or its restoration. Scene/material coverage is unmeasured offline. **Unproven; do not install a guessed pass.** |
| Main-camera street-geometry proxy transformed to the detached entrance | Best smaller candidate: native layout, NavMesh and city routes can stay fixed | Needs a complete bounded renderer inventory and unique exterior/interior door correspondence. `CityBuildingController` has multiple renderer/fade/hide/LOD collections; `TreeController` has separate LODs, particle activation and material-property blocks. These are not a complete street mesh/material contract. Copying a hierarchy can copy scripts/colliders/agents; copying meshes alone omits skinned bodies, particles and live LOD/material changes. Selecting renderer centers within radius does not clip geometry that crosses it. Indoor light influence/shadows on the proxy and aperture occlusion still need proof. **Preferred next investigation, not an accepted renderer.** |
| Relocate the native layout to the street, then retain the ordinary camera | Could align the two spaces | Cache audit below has concrete invalidation hazards and no complete rebuild transaction. **Reject relocation.** |

For a fixed native interior point I and street point S, any common rigid camera transform preserves their separation. Aligning a detached street with the interior therefore needs geometry relocation or a separate geometry/view transform for one pass; changing only culling or the camera origin is insufficient. A proxy could use an independently verified rigid transform derived from paired door frames. That is a mathematical possibility, not observed door/window alignment. The vanilla entry path's fallback to a primary/available exit is unsuitable evidence for such a mapping: fail closed unless the exact `ExitZoneDespawner.exitToZoneId` correspondence is unique and its transforms, orientation and scale are verified.

**Patch decision:** no new Harmony target. Patching `CullingGroupController.DispatchLod` alone would address neither projection nor all renderer owners. Patching the building entry coroutine would not rebuild the caches below. No indispensable minimal patch has been established, so adding one (or a foreign-owner logging hook for it) is unjustified. Existing E2c detection and existing patches are byte-unchanged.

## World-space cache audit

No candidate is permitted to move native layout or city geometry in this attempt. The proposed proxy would touch only newly owned render resources, but their complete membership/material contract is still unproven. This audit records actual owners rather than assuming parent translation refreshes everything.

| Owner / inspected seam | State and consequence | Decision |
|---|---|---|
| `BuildingStructureEntry`; `BuildingManager.ToggleLayout`, `LoadBuilding` | Addressable structure has serialized local position/rotation/scale. Load caches `interiorBounds`, floor elements, exit list and placement grid. Actual layout-to-address geometry resides outside the DLL. | No native layout move; retain F0 eligibility limits. |
| `ExitZone.Awake`, door state updates | Drive-in/elevator open/closed positions are cached in world space; ordinary hinged rotation is cached locally. Trigger occupant lists and running tweens have separate lifetimes. | No generic safe door rebase established; exclude elevators/multi-height as F0 already does. |
| `BuildingManager` placement completion | Item NavMesh refresh and target refresh happen separately, with delayed work. Loaded NavMesh/active agents and held paths are not rebuilt merely by translating a parent. | No relocation or synthetic arrival. |
| `IndoorCustomerSpawner.GetRandomPositionInside`, `SpawnCustomer`; `CustomerPool` | Uses cached bounds, exit transforms, NavMesh sampling/warps and a distinct customer pool/behavior tree. Pool release warps to a safe position, disables behavior and removes callbacks. | Cannot treat a mesh copy or street pedestrian as a native customer adoption. |
| `BuildingManager` entry/exit, vehicle spawn and safe-exit paths | Interior spawn/exit selection, exterior drive-in entrances, selected vehicle state and clearance calculations have distinct coordinate owners. | No vehicle/cache rewrite in a shop renderer. |
| `CameraHelper`; `IndoorLightVisibilityManager`; `BuildingManager.ToggleLayout` / `ResetIndoors` | Camera priority/blend state, frustum-driven light visibility, indoor volume and directional lights have different writers. | Leaving volume enabled alone does not prove preserved lighting. No overrides installed. |
| Our `DwellSpots`, walking routes, `PhysicalPopulation.TryAwaitArrival` / `ObserveArrival` | Door/queue geometry and committed corners remain in city space. Current arrival hand-off retains a body for queue/optional-stop decisions; it does not adopt an indoor customer. | Never transform route progress or infer door arrival from projected proxy coordinates. |
| Native culling, city-building fade/LOD collections, trees | Pending LOD dispatches and independent property-block/particle owners mean a renderer enabled snapshot is not a complete ownership snapshot. | Do not sweep native enabled flags or clone scene components. |

## Customer ownership blocker discovered during the audit

This is a further constraint, not authorization to skip the drawing stop. `IndoorCustomerSpawner.SpawnCustomer(CustomerEntry)` obtains a separate pooled customer, creates neighborhood citizen data, assigns the order/entry, warps to an interior exit and initializes its behavior. `Customer.CompleteOrder` adds completed orders to the player's unprocessed orders; `FullServiceCustomer.Leave` and the exit callback can finish orders too. These paths are economic writers, not visual entry effects.

Our `NativeFoodOwnership.CustomerDraw` suppresses native entries for managed businesses. `NativeFoodMarket.BuyLive` already owns the receipt-guarded native checkout and checks product, bag, stock and completed-order deltas. Re-enabling native spawning to obtain a visible indoor body would cross that authority boundary; an additional customer order could duplicate checkout/completion, while a fabricated already-completed order could be a phantom sale. There is no proven API here that adopts our street body and consumer/intent identity into that lifecycle without another order owner.

F0 retail eligibility is also broader than managed food authority. A generic shopping visit to a native-owned business is not evidence of a purchase. A future implementation must distinguish committed food consumers from ordinary activity visitors and represented consumers; it must not generate sales merely to animate entry. Save/reload and re-entry must derive presentation from existing authoritative intent/receipt state, without a new saved hand-off record or an animation-owned sale. None of those new hand-off invariants is claimed as tested in this NO-GO.

## Smaller next step

Use the normal combined 6 Nov session for a **read-only geometry and ownership feasibility probe** on prepared disposable fixtures, before any renderer implementation:

- Capture three F0-eligible own shops with differing size/frontage/layout and one elevator refusal. Record actual addresses, building size/version, first exterior door and every interior exit's id, position, rotation, scale, elevator flag, floor height and camera matrices. Require a unique matching door id and check the proposed rigid mapping against more than one aperture/edge; no address guesses.
- Inventory only metadata for the nearby static renderers (bounds, mesh/submesh/material/shader names, renderer kind, LOD/fade owner, enabled state), moving resident/vehicle bodies and relevant lighting/volume state. Establish a bounded refresh/invalidation plan and strict radius treatment for meshes crossing 30/60/120 m. No asset or raw-save export to Git.
- Visual confirmation must establish that doors/windows actually expose the projected street with sensible depth. A successful matrix or active lease is insufficient. If the proxy cannot preserve indoor lighting/materials or cannot cover the street with bounded work, keep the renderer rejected.
- Separately trace one managed food intent/receipt and the native customer completion callbacks through save/reload/exit. Establish a presentation-only customer path with a single economic owner before any adoption. Keep ordinary native-owned shop visitors out of managed checkout.

If those observations support it, the next implementation can be a scoped, main-camera **render-only proxy** with its own resource lifetime, no native transform/renderer mutation and no new Harmony patch unless independently justified. It must still prove one visible body per consumer and exactly one existing checkout. This proposal is smaller evidence collection, not an implementation promise or an expansion into F2/F3.

## Performance and acceptance limits

This branch adds **zero production frame calls and zero production allocation sites**, with either flag value: production/config/test sources remain identical to the base. Thus incremental F1 mod-work and allocations are exactly zero by construction, not a benchmark of a working view. Existing F0 on/off accessor/retention benchmarks are reported separately in the [F1 checks](../checks/f1-shop-view.md); F0-on already does nonzero physical-retention work and must not be called zero-cost.

No cheap runtime perf win was taken: no runtime code is touched, and changing the four parked hotspots to optimize an unimplemented view would defeat the stop. No perf-win index entry is appropriate. A future 60-80 m static proxy has a **planning estimate of +2-4 ms whole-frame cost**, carried from F0, with unknown dynamic/shadow/material costs and no established upper bound. It is not a measurement or accepted budget; measure actual whole-frame cost separately from combined mod-work p50/p95/max on the combined rig. Combined mod-work p95 must remain <5 ms and every mod-work spike >=30 ms must be flagged.

The [checks and numbered 6 Nov handoff](../checks/f1-shop-view.md) and [prepared PR description](../reviews/2026-11-01-f1-pr-description.md) explicitly distinguish runnable baseline observations from blocked F1 visual/customer acceptance. No new F1 bridge or hand-off counters exist in this stopped slice; absence must be reported as unavailable, never zero successful hand-offs.
