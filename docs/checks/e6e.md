Status: implemented (offline); not verified by rig

# E6e — bodies at the hatch

Binding scope: [E6 plan](../plans/e6-carts-food-trucks.md), section 3.9, the E6e implementation/ownership rows and section 7 check 11. Base `fb64fa576d2a2d138b9242271e2bf5e12a1864b2` is E6d, after F6d writer 31. The local `origin/codex/persistent-city` and feature branch matched that tip at launch.

## Implemented

- `DwellSpots.TryVendorQueue` uses the published stand and the copied `QueueX/QueueY/QueueZ/QueueHeading`. Heading runs toward the back of the line in Unity yaw degrees; cart spacing is 0.8 m and truck spacing 1 m. Both keep the existing eight-slot bound. Walkable samples must stay within 0.15 m horizontally and 0.5 m vertically of their requested point; a blocked segment ends the usable line. There is no building-door fallback for a vendor.
- Queue sampling shares the existing one-uncached-line-per-frame budget and retry delay. Layout changes invalidate the cache; unchanged layouts reuse it.
- The `PhysicalPopulation` vendor hook reads the existing market's published image. Residents walk to the stand on their committed route, retain their body through the budgeted arrival, then walk to a free queue slot. Queue movement follows the existing walking speed and movement bound. Service completion, stand closure or loss of authority ends the queue walk. A completed meal's body waits at the captured stand until its scheduled departure takes the existing lease, bounded by the captured departure time plus one game minute of catch-up grace. The wait follows the city clock; the existing walking-time bound remains unchanged. A departing visible resident resumes its measured return with the same body.
- Stationary vendor residents are tracked from committed activity events. Admission uses the existing pool, pedestrian/vehicle caps, four-attempt budget, focus radius, reservation filter and visitor rule. The resident's existing identity and appearance seed are passed to the unchanged body pool.

Production edits are limited to `Runtime/DwellSpots.cs` and the vendor hook in `Runtime/PhysicalPopulation.cs`. Street Food's queue view, RLO, wages, employment, save codecs, wire files and schema are unchanged.

## Offline checks

The xUnit project compiles the complete production `DwellSpots.cs` and `PhysicalPopulation.cs`; only native scene, movement and pool contracts are doubled. Tests explicitly move actors to targets: issuing a path command alone cannot prove arrival. Native prefab/appearance acquisition remains a batched-rig measurement.

`VendorBodyTests` covers queue coordinates/height/heading for carts and trucks; sampling refusal and partial lines; cache invalidation/shared sampling budget; stand-to-slot movement and facing; departure on close; completed-meal body continuity through the departure gap; body/attempt/line caps and reserved identities; camera release and actor reuse without changing the resident's identity/appearance seed; arrival and return handoffs; and the current-writer golden.

Writer 31's populated seed-5150 SHA-256 stays `06b494d2627e0d6675d8c437e1c0dff72e176ab3ae7aaa54a0f152052aace308`. The existing second populated golden remains covered by `EconomicLedgerTests`. No schema bump or migration.

Initial implementation validation (`4e1e005`):

- Required build: passed against the supplied local Managed directory; DLL SHA-256 `62d2c9ebdd5614bf2305fef5ce321a0f205783e3b9f94cd1c5a6a2292f004cc9`.
- First full `dotnet test tests/PersistentCity.Tests.csproj`, with the opt-in E6e timing actions enabled: 3,767 passed / 1 failed. All 22 E6e cases passed. The single failure was the existing Core rush replay's 57.176417 ms GC interval; it is flagged in [performance](../perf/e6e.md#final-component-measurements).
- Isolated Core rush replay: passed (1/1). Final required `dotnet test tests/PersistentCity.Tests.csproj` without opt-in measurements: **3,768 passed, zero failed**, 3 m 37 s. The final queue-coordinate assertions also pin cardinal endpoints independently of the implementation; their focused E6e rerun passed (22/22).
- `git diff --check`: passed. `python3 tools/docs-links/check.py`: 369 Markdown files, zero broken relative links.

The `tools/*-regression` matrix was not run.

## Review fix Opus-r1-1

The finding is implemented offline. `ObserveVendorQueue` no longer releases a completed meal's body at the open-pavement stand while its citizen is still `AtPlace`. It keeps that binding until `OnOptionalDeparture` takes the journey lease. The deadline is captured when the body leaves its queue slot, so rescheduling cannot extend a stale hold indefinitely; paused wall-clock frames do not consume it. Body caps, pool identity, walking-time bounds and writer 31 are unchanged.

Before the fix, all four cases of `FinishedMealKeepsSameBodyThroughGapAndDeparture` failed because the body pool was empty at the stand. After the fix, the Paid/Missed/Declined/Uncertain cases retain the same body through a paused gap and a real simulator departure, with no additional acquisition. `FinishedMealHoldEndsAfterCapturedDepartureGrace` checks the bound. The pool-reuse check now uses explicit suspension instead of relying on the reported despawn.

Focused E6e checks and measurements: 26 passed. Required build: passed against the supplied Managed directory; DLL SHA-256 `e5b6dcbab54c3f67a107f5f2fc8abdded2fab11e69162d65d052d3dd374b2a4f`. Required full `dotnet test tests/PersistentCity.Tests.csproj`: **3,772 passed, zero failed**, 3 m 33 s. `git diff --check` and documentation links passed. All checks ran in the foreground; retries that overlapped another worktree's test host were stopped before the final single-host run. [Review-fix performance](../perf/e6e.md#review-fix-opus-r1-1) reports the completed-meal hold separately from native whole-frame cost. Batched rig check 1 now explicitly captures the paid-to-departure gap; not verified by rig.

## In-game checks for the next batched rig

All checks are rig-automated in disposable `FT-E6-Vendors`, **locked-PC**. They extend E6 section 7 check 11. They are batched before release; this slice has no native proof yet.

1. **Bodies at the hatch, including service departure.** Rig-automated — existing capabilities: `camera look_at`, `screen_pos list`, `capture`, FT `shoot` pose `hatch-pavement`, FT `fixture`/`route`/`time`/`console`. Setup: an open crew-operated PC-owned truck with an original-three-only executable menu, and a hot-dog cart, stocked at pavement stands near residents. Steps: follow one resident's visible body from approach through queueing and service; capture approach, standing, payment, the paid-to-departure gap and the first return step at each vehicle. Include a meal paid before its scheduled departure. Expected: the same resident walks to the pavement-side stand, then a queue slot; faces the hatch/front of the line; keeps the same body at the stand throughout the paid-to-departure gap, then walks away on its return route without disappearing and respawning; no body stands in the road and Street Food draws no PC residents in its own queue view.
2. **Closed stand and measured return.** Rig-automated — existing capabilities: `camera look_at`, `screen_pos list`, `capture`, FT `shoot` pose `hatch-pavement`, FT `route`/`time`/`console`. Setup: the same eligible stand with a visible queued resident and a scheduled close before service. Steps: capture the resident before close; advance through close and the return journey; capture each transition. Expected: the resident stops queueing, walks back to the captured stand point and leaves on the ordinary return route; the closed stand acquires no new queued bodies. A route move keeps the old stand while committed residents drain; no body jumps to the new stand.
3. **Caps, reuse and cost at the hatch.** Rig-automated — existing capabilities: `camera look_at`, `screen_pos list`, `capture`, FT `shoot` pose `hatch-pavement`, `rig flow run` autosave guard; pending capability: correlate PC body identity/generation, queue slot and global body counts across camera release/reacquisition, and report PC spawn/dwell mod-work separately from whole-frame samples. Setup: matched 0/2/8 eligible-vehicle windows with enough residents to fill the configured pedestrian cap. Record the original autosave setting, disable it and verify it is off before each measured window and after reload. Steps: capture a full queue, pan away/back to reuse pool actors, and collect matched timing windows; keep saves outside them, discard any sample containing a save, restore autosave at cleanup. Expected: configured global caps, four admission attempts and eight slots per line remain bounded; a reused actor binds to its resident's stable identity/appearance; PC mod-work p95 is below 5 ms, every spike >=30 ms is listed with component/condition, and whole-frame cost is reported separately.

## PR handoff

Review E6e only: the two owned Runtime files, offline checks and perf notes. No schema change; current-writer golden unchanged; body caps and pool identity contracts unchanged; Street Food's own queue untouched. Copy the numbered batched-rig list above and the measured numbers from [E6e performance](../perf/e6e.md) into the harness-created PR description. No push, PR creation, merge or rig access is performed by this implementer.
