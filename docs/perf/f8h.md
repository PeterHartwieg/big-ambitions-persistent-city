Status: implemented (offline); not verified by rig

# F8h perf

Refs [#95](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/95). Row F8h of the [F8 plan](../plans/f8-capability-boundaries.md). Mac (Apple M1 Pro, macOS 26.5.2), .NET 10, Release harness in the foreground, three runs. No game, rig, ModsLocal or bridge was used. Checks: [docs/checks/f8h.md](../checks/f8h.md).

Standing gate: mod-work p95 < 5 ms; every mod-work sample >= 30 ms flagged; whole-frame timing reported separately (#128 stays its own investigation). F8h adds no per-frame work. The refusal is decided once per city load, in the `NativeFoodMarket` constructor, from one `HookConflicts.Scan`. The activation line now reuses that scan instead of running its own, so a load still scans once. The four parked hotspots stay parked.

| Measured | Number | Source |
| --- | --- | --- |
| F8e activation scan, warm (3 patched stand-in targets plus 1 foreign-only method) | p50 0.079-0.080 ms, p95 0.096-0.099 ms | `tools/capability-contracts`, arm HookConflictArms |
| F8e scan, same state as the refusal check (6 patched targets, check-1 modpack owners on the tick, one refusing prefix) | p50 0.18-0.23 ms, p95 0.23-0.29 ms, max 0.89-1.03 ms | arm ConflictRefusalArms |
| F8h refusal check, `NativeFoodContract.CheckoutForLoad` (scan, policy join, refusal string) | p50 0.18-0.23 ms, p95 0.23-0.29 ms, max 0.91-1.13 ms | same |
| Scans per city load | before 1 (activation line); after 1 (market construction, reused by the activation line) | `HookConflictReportTests.TheRefusalIsDecidedOncePerLoadFromTheLoadScan` |
| Rig activation scan at `b3e5a910`, full modpack (F8e, input to this row) | PC 16.278 ms, Mac 19.693 ms | [PC record](../checks/f8h-check1-modpack-owners-pc.json), [Mac record](../checks/f8h-check1-modpack-owners-mac.json) |

The policy adds nothing measurable over the scan: the two columns overlap within run noise. The rig scan numbers include the first call's JIT and a 49-target modpack. Both stay under the 30 ms flag and run once per load, outside gameplay windows. Reusing the load scan for the activation line is the cheap win: without it a load would scan twice. The second scan would be warm, about 0.2 ms here. Rig check 2 in the F8h checks records the real number at head.
