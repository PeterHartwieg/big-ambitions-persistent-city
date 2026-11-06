# City-audit F5b — identity and ownership core: performance

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F5b — [Ordered-input owner lookup hint, boxing-free enum checks, allocation-free intent ownership and concatenation-free identity hashing](f5b-identity-core.md): validator p50 1,914/10,000/100,000 persons 1.3185/8.3299/108.7320 -> 0.6485/3.6938/49.2912 ms; warm validator 68,741/358,737/3,587,106 -> 0 B/call; intent check 127.8 -> 0 B/intent (3.06 -> 1.05 ms per 30,000); identity draw 284 -> 0 B/lookup. New code, offline components only; no frame work.

4 November 2026, offline. Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Measurement named in the [F5 plan](../plans/f5-population.md) section 4 F5b row: validator cost at 1,914 / 10,000 / 100,000 persons (one pass, no per-person allocation) and derived identity cost per lookup.

## Scope and limits

- F5b adds **no frame work**. The validator, `IdentityPolicy` and the income-source function are not called by the simulator, the codec or Runtime, so in-game mod-work p95, single-spike counts and whole-frame timing are unchanged by construction. No native number is claimed here; the next batched rig run measures that (check 4 in [the checks file](../checks/f5b-identity-core.md#in-game-checks-for-the-next-batched-rig)).
- Numbers come from `tools/identity-core-measure` (Release, .NET 10.0.13, tiered compilation off so short loops measure optimized code, this PC, shared with other worktrees). These are component timings, not Unity/Mono timings.
- Fixture: a factor-2-style city, half residents (`c<n>`/`pc:<n>`) and half commuter visitors (`consumer:<anchor>:1`), one wallet and one slot-0 roster record per person, three meal intents per person, one employment per resident (none at player-owned workplaces), one wage per Work employment, 1,000 workplaces (10% Study, about 1% player-owned) and 12 portals.
- Three consecutive runs; the table shows the third, with the other runs' ranges given where they differ materially.

## Validator (one whole-graph pass, warm reused validator)

| Persons | Intents / employments / wages | p50 | p95 | max | Warm allocation |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1,914 | 5,742 / 946 / 851 | 0.6485 ms | 0.7140 ms | 0.8512 ms | 0 B/call |
| 10,000 | 30,000 / 4,945 / 4,450 | 3.6938 ms | 5.1875 ms | 5.9275 ms | 0 B/call |
| 100,000 | 300,000 / 49,450 / 44,500 | 49.2912 ms | 56.3153 ms | 56.6138 ms | 0 B/call |

The three runs ranged 0.6482–0.6508 ms p50 at 1,914, 3.6786–3.7159 ms at 10,000 and 48.68–49.40 ms at 100,000 (max 56.6–97.0 ms).

**Flag (≥ 30 ms):** a full validation of a 100,000-person graph takes about 49 ms p50 and up to 97 ms max on this PC. It is not frame work in F5b. F5c, which is the slice that calls the validator on codec read/write and migration, must run whole-graph validation off the frame (detached read/migration, as the codec already does) or split it into bounded units. It must not validate the whole graph inside a frame. Narrow checks before an economic commit are per person, not whole-graph.

## Derived identity (`IdentityPolicy` v1)

| Donors | Policy build (once per catalogue) | `Draw` (indices only) | `Derive` (full identity with name) |
| ---: | ---: | ---: | ---: |
| 400 | 1.2058–1.2735 ms | 53.3–58.3 ns, 0 B/lookup | 204.8–232.8 ns, 196.5 B/lookup |
| 5,000 | 0.5225–0.5625 ms | 57.3–63.5 ns, 0 B/lookup | 132.2–144.0 ns, 206.3 B/lookup |

100,000 lookups per row. The 400-donor build is the first build in the process and includes first-use costs. `Derive` allocates only the returned identity and its name string; callers that need only age, sex, persona or appearance use `Draw`.

## Perf wins taken in new code (before → after)

Both alternatives were measured on matched inputs in the same tool. They are design choices in new code, not changes to existing hot paths.

1. **Ordered-input owner lookup hint.** Wallets, roster records, intents and employments usually arrive in person order, so the validator tries the previous match and its successor before the binary search. The result is the same either way; the unit tests cover reversed orders. Validator p50/p95 before → after: 1,914 persons 1.3185/1.8242 → 0.6485/0.7140 ms; 10,000 persons 8.3299/11.1043 → 3.6938/5.1875 ms; 100,000 persons 108.7320/147.7408 → 49.2912/56.3153 ms. Allocation stays 0 B/call.
2. **Range checks instead of `Enum.IsDefined`.** `Enum.IsDefined` boxes its value. Replacing it cut warm validator allocation at 1,914 / 10,000 / 100,000 persons from 68,741 / 358,737 / 3,587,106 B/call to 40 B/call. Those 40 B were the harness's own `Stopwatch` inside the measured region; with that removed, the figure is 0 B/call. Both measurements ran with tiered compilation on; the allocation difference does not depend on the JIT tier.
3. **Allocation-free intent-ownership comparison.** Checking `food:<escaped owner>:` without building `Uri.EscapeDataString(owner)` takes 30,000 intents from 3.0357–3.0923 ms and 127.8 B/intent to 1.0289–1.0643 ms and 0 B/intent. The two give identical results on all 30,000 intents; the unit tests compare them over every printable ASCII owner and over non-ASCII owners.
4. **Hashing without concatenated keys.** Version-1 draws continue the FNV state over `PersonId` and the purpose suffix (`StableRandom.Begin/Append/Finish`). Three draws per lookup cost 53.3–63.5 ns and 0 B. The matched concatenated-key form costs 166.5–295.6 ns and 283.9 B/lookup. The values are identical; a unit test checks them against an independent reference.

## Parked hotspots

Physical population, closure refresh, optional-stop refresh and save serialization are untouched and stay parked. The only change F5b makes to existing code is a doc comment in `Core/FoodCommerce.cs`.
