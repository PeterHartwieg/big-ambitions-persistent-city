Status: implemented (offline); not verified by rig

# F6e performance

Refs #93. Offline macOS arm64 / .NET 10 Release. Final runs check for an existing testhost before launch and run in the foreground. A launch racing E6e was cancelled and discarded. No game, rig, ModsLocal or bridge was used.

## Method

`tests/F6eMeasure.cs` measures 20,000 shift settlements after 1,000 warmups, with reset outside the interval. Posting measurements use the same bounded `StepBudget` path, support wallets and exact money at 1,914 / 10,000 / 100,000 owners. Fifty initial days warm the queue and tiered JIT; four days are sampled, with a 32-posting cap and allocation assertions. Largest-first ordering is identical in both runs. The baseline is the actual launch base `fb64fa576d`, extracted into a temporary source tree without creating or switching branches, compiled in Release with the same posting fixture and timer. Fresh-city read/write measurements use 957 / 5,000 / 50,000 residents plus factor-2 F5 visitors, one warmup and five samples. Saves are measured separately from mod-work.

## Shift settlement and postings

Shift settlement: p50 **0.000125 ms**, p95 **0.000250 ms**, maximum **0.001958 ms**, **0 B/operation**. There are zero ≥30 ms settlement samples.

| Owners | Samples per arm | Actual base p95 ms | Current heap, F6e disabled p95 ms | Current attendance policy p95 ms | Attendance maximum ms | F3 reference p95 ms | Allocation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1,914 | 240 | 0.005709 | 0.005834 | 0.005916 | 0.006334 | 0.0208 | 0 B/unit |
| 10,000 | 1,252 | 0.006250 | 0.007667 | 0.006792 | 0.013333 | 0.0229 | 0 B/unit |
| 100,000 | 12,500 | 0.009291 | 0.011833 | 0.011792 | 0.093250 | 0.0111 | 0 B/unit |

The attendance toggle comparison uses the same new heap implementation, with and without F6e policy. Posting p95 remains at or below the F3 reference levels within timing noise (the 100,000-owner row is 0.000692 ms above its 0.0111 ms reference). The separately compiled base is included to expose heap overhead: its 100,000-owner p95 is lower than the current run. No optimization win is claimed from that between-process comparison. All posting samples are below 30 ms, and all measured mod-work p95 values are below 5 ms. The heap reuses entries and caches the resolved F5 wallet owner; no new saved cursor, allocation per posting or wage sweep is introduced.

## Fresh-city save latency (separate criterion)

| Residents × factor | Persons/wallets | Characters | Write p50 / p95 ms | Read p50 / p95 ms | ≥30 ms write/read samples |
| --- | --- | --- | --- | --- | --- |
| 957 × 2 | 1,914 | 1,729,129 | 71.711 / 110.936 | 236.680 / 279.747 | 5 / 5 |
| 5,000 × 2 | 10,000 | 9,043,347 | 173.794 / 181.754 | 378.778 / 389.238 | 5 / 5 |
| 50,000 × 2 | 100,000 | 83,838,318 | 1652.843 / 1687.388 | 3633.421 / 3780.691 | 5 / 5 |

Flagged save/read samples ≥30 ms (milliseconds; these are save-path observations, not ordinary-play mod-work):

- fresh 957x2 write (1729129 characters): 52.218, 60.194, 71.711, 106.028, 110.936.
- fresh 957x2 read: 73.609, 86.880, 236.680, 276.476, 279.747.
- fresh 5000x2 write (9043347 characters): 161.237, 166.885, 173.794, 178.263, 181.754.
- fresh 5000x2 read: 375.610, 377.197, 378.778, 382.814, 389.238.
- fresh 50000x2 write (83838318 characters): 1581.019, 1634.906, 1652.843, 1679.249, 1687.388.
- fresh 50000x2 read: 3543.632, 3550.744, 3633.421, 3649.262, 3780.691.

Every measured write was byte-identical to its fixture payload and every read returned Loaded. These are newly created cities, not migrated inputs. F6d's wire shape and record contract are unchanged; only the writer label changes. Save allocation was not measured in this slice.

## Boundary

Whole-frame cost was not measured by these offline checks. The unified in-game mod-work gate and separate whole-frame evidence remain in the batched rig list in [the checks file](../checks/f6e.md). Autosave settings are not applicable to this headless run; the rig must apply its autosave guard for native timing.

Physical population, closure refresh, optional-stop refresh and save serialization remain parked. Changed workplace-hour observations can allocate replacement hours; steady-state settlement and posting measurements exclude input changes and assert zero allocation.
