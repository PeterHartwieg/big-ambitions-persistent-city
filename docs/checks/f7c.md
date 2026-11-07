Status: implemented (offline); not verified by rig

# City audit F7c: save snapshot and wire split, current writer byte-identical

Refs #94. Plan: [F7 section 4, row F7c](../plans/f7-save-snapshots.md#4-slice-breakdown); sections 3.2, 3.4, 3.8, 5, 6, 7 and 10. Base `57607b2` (`origin/codex/persistent-city`), current writer 29. No schema change and no new key. Measurements: [docs/perf/f7c.md](../perf/f7c.md). Persistent City is not live or released.

## What F7c changes

- **Save snapshot** (`Persistence/SaveSnapshot.cs`). `SaveCodec.Capture` runs today's synchronize steps on the live city (`PrepareForSave`, outflow start, name table) and then copies the wire-carried state into a detached `SaveSnapshot`. The copy runs on the calling thread, which in the game is the main thread inside `onSaveGame`, at the same point in the F7b callback as before. It runs in seven independent sections (header, places, residents, visitors, traffic, food, research). Any order gives the same snapshot. The snapshot holds no runtime list or record. It shares only objects with no setters (weather commitments, offer metadata, held tuning, campaign images, research reports) and committed journey routes.
- **Encode and validate from the snapshot.** `SaveCodec.Encode` validates the snapshot with the existing `Validate` and encodes it with the existing writer-29 serializer settings and converters. The snapshot uses the same wire DTO types as the live model, so the bytes are the ones the writer produced from the live graph before. `SaveCodec.Write` is now capture followed by encode. The frozen writers 25 to 28 that tests still call go through the same path, and their golden pins hold.
- **Publication.** `SaveSession.WriteTo(modData, state)` captures and then publishes through the new `WriteTo(modData, snapshot)`. A snapshot is published at most once. A second publication is refused and the payload stays. A snapshot for another population is refused before encoding. A capture or validation refusal keeps the previous payload, as before.
- **Committed routes are immutable.** `Route.Commit` runs where the simulator assigns a journey (`CitySimulator`, `TravelTiming` retiming, optional visits and meal returns). Afterwards every `RouteLeg` setter and the `Legs` setter throw, and `Minutes` is computed once with the same summation order. The type is still `List<RouteLeg>`, so a structural list edit is not blocked by the compiler. `Minutes` throws if the leg count changed, and a capture refuses a committed route whose legs changed ("A committed journey route changed after commit."), keeping the previous payload. Routes that are not committed (food quotes, journeys of a just-loaded city, test fixtures) are copied leg by leg.
- **Diagnostics.** `SaveCostPhase.Capture` is appended to the F7a phases, so the earlier phase numbers stay. The on-request save-cost record shows `captureMs`.
- **Unchanged:** the reader, including the refusal of older development schemas with the "start a new city" status, the future and invalid refusals, the F7b epoch contract and the Runtime save order.

Small Core hooks for the copy: `ResidentMeal.CopyHostTo` copies the transient roster host, and `ResearchLedger.CopyForSave` copies the ledger lists.

Test-side edits: four tests that corrupted a running city's journey now corrupt an uncommitted copy (`tests/RouteEdits.cs`), which is the shape a journey has after load. `PersonIdentityTests` lists the snapshot file among the files allowed to name the stored person types it copies. A few private helpers in `ResumablePlanningTests` and `FoodBudgetBatchTests` are now internal for reuse.

## Offline evidence (7 November 2026)

| Plan requirement | Test or record | Result |
| --- | --- | --- |
| Current writer's pinned golden hash unchanged | `ResearchSaveTests.GoldenResearchWriterBytesArePinned`, `Writer26/27/28` golden tests | pass |
| Golden SHA-256 equality on every F7a and E5d-sized fixture, fresh cities in the current schema | `SaveSnapshotTests.CurrentWriterBytesEqualTheBaseWriterOnFreshFixtures` (five 957-resident fixtures pinned to base-commit hashes); [perf table](../perf/f7c.md#byte-identity) for all 11 fixtures up to 50,000 x 3 | equal on all 11 |
| Permuted capture order | `PermutedCaptureOrderGivesIdenticalBytes`: reverse, 6 rotations, 16 seeded shuffles on a rush city with every research state | identical bytes |
| Snapshot is complete and detached | `SnapshotEqualsTheLiveGraphAndSharesOnlyImmutableObjects` (field-for-field reflection audit, private fields included); `EveryWritableMemberOfTheSavedGraphIsCaptured` (every public writable member reachable from `CityState` filled with a distinct value) | no difference; only immutable objects and committed routes shared |
| Mutation after capture | `MutationAfterCaptureNeverReachesThePayload`: 90 more minutes of play plus edits in every section | payload equals the bytes at capture |
| Validation runs on the snapshot | `ValidationRunsOnTheSnapshotAndARefusedSaveKeepsThePayload` | refusal from the snapshot, payload kept |
| Committed routes immutable at commit | `CommittedJourneysAreFrozenAndSharedWhileLoadedRoutesAreCopied` | setters refuse, shared by reference, structural edit refused with payload kept |
| Section 3.8: pending route answer | `SaveSnapshotInterruptedStateTests.PendingRouteAnswerIsNotSavedAsFinishedAndReplaysFromItsTimestamp` | equals the unbroken city each day |
| Section 3.8: midnight posting units after 32/64/96 | `MidnightPostingUnitsCapturedPartWayRebuildTheirQueueFromTimestamps` | ledgers and orders equal the unbroken city |
| Section 3.8: committed movement mid-journey | `MidJourneyCheckpointContinuesLikeTheUnbrokenCityUnderAnotherCadence` (slice 1 against 64) | bytes equal |
| Section 3.8: physical lease active | `PhysicalLeaseProgressIsCapturedAndTheLoadedJourneyResumesInBackground` | progress at capture saved, journey resumes |
| Section 3.8: campaign publication queued | `SaveEpochTests.RuntimeWiringKeepsCaptureOrderAndDetachMarkersFirst` (source order, capture inside `.WriteTo` after `CompleteCampaignsForSave`) | unchanged order |
| Section 3.8: encode or validation failure | the two refusal tests above; F7b's marker tests | previous payload kept |

Mutation check: removing two copied members from the capture fails 13 of the new tests.

Rows of section 3.8 owned by other slices stay with them: re-entrancy, failure markers and detach (F7b), held contexts and receipts (F7d), worker encode (F7e), process kills (rig check 3).

### Local commands, 7 November 2026, this Mac

- `python3 tools/build.py --game-managed "<dll-import-shim>/Big Ambitions_Data/Managed"`: package built.
- `dotnet test tests/PersistentCity.Tests.csproj`: 3,540 passed, 0 failed. The base had 3,521.
- Opt-in measurements in [docs/perf/f7c.md](../perf/f7c.md): `F7cMeasure` on base and branch, `F7cSnapshotMeasure`, `tools/bounded-bulk-regression` default and `--integrated` on base and branch.

### Measured cost, short form

Save pause p50 grows 5% to 34% from the new capture phase: representative 50,000 x 2 goes from 1,355.78 to 1,551.69 ms. Capture is 4.5% to 22.3% of validate plus encode. Allocation per save grows 16% to 23%. Retained memory shows no win: about 24 to 33 B more per committed one-leg journey. The StepBudget fixture is unchanged at 0 B. One 35.43 ms integrated-replay frame at 100,000 consumers did not repeat in the rerun and is flagged. Details and every flagged sample are in the perf file.

## In-game checks for the next batched rig

Checks 1 to 5 belong to the save-specific campaign of plan section 7 and run only when that campaign is requested. Check 6 belongs to the ordinary batched rig. Each runs on the headless agent rig in a disposable FT city. None is deferred to Peter.

1. **Byte identity on a real checkpoint (plan check 2 and 5 inputs).** Setup: one disposable FT city with an owned food shop, saved by the base build `57607b2` and copied. Steps: load the copy with the base build, advance one game hour with autosave off, save; load the same copy with the F7c build, advance the same hour under the same inputs, save. Extract `persistentcity:state_v1` from both checkpoints (RIG-PCSAVE). Expected: the two payloads have the same SHA-256, and the F7c checkpoint loads with PC active. If the two runs' inputs cannot be made identical, the expected result is instead that the F7c payload re-encodes to itself after a strict read in the comparator.
2. **Save phase cost with the capture phase (plan check 2).** Setup: the 957-resident representative checkpoint and a 10,000-consumer checkpoint, save-cost diagnostics requested. Steps: trigger `Default`, `MidnightSave` and `RecoverSave` saves (RIG-SAVETYPES), five each, and read the save-cost record through the rig console after each. Expected: `captureMs` is present and nonzero, synchronize, capture, validate, encode and publish add up to the total, the payload sizes match the extracted payload, and every phase at or above 30 ms is flagged as a save sample, not mod-work.
3. **Interrupted save and reload at named states (plan check 3).** Setup: RIG-CHECKPOINTS states: a pending route answer, midnight posting stopped after 32, 64 and 96 postings, the morning rush with journeys in flight, a visible physical lease, and a queued campaign publication. Steps: save at each state, keep playing 30 game minutes, then reload the save in a fresh process and continue 4 game hours at a different frame cadence. Run an unbroken control from the same state. Expected: ledgers, journeys, queues, receipts and identities equal the control (RIG-PCSAVE comparator), no event counts as finished twice, and the lease resumes as background travel.
4. **Committed journeys stay unchanged in play (new for F7c).** Setup: a disposable FT city at the morning rush with bodies on screen, F7c build. Steps: play 2 game hours with autosave off while the camera moves through busy streets and the bus and subway are used, then make one manual save. Expected: no "committed route" exception in the PC log, PC stays active, and the manual save publishes with a success epoch marker.
5. **Large-city capture (plan check 6).** Setup: `city-50k@1`, F7c build. Steps: three manual saves with diagnostics requested, then reload the last one. Expected: each save publishes; capture, validate and encode are recorded and flagged when at or above 30 ms; peak process memory during the save is recorded; reload restores the same counts and population key.
6. **Clean gameplay windows carry no save work (plan check 9).** Setup: the ordinary batched perf windows, autosave preference recorded, set off and checked off before each window, including after reload. Steps: run the standard windows. Expected: zero save callbacks and zero snapshot captures inside any window, mod-work p95 under 5 ms, every mod-work spike at or above 30 ms flagged with count and component, whole-frame timing reported separately, and the original autosave preference restored at cleanup.
