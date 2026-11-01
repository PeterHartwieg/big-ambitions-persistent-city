# F1b diagnostic allocation win — 1 November 2026 (offline)

Reuse one command-local `List<Material>` for `Renderer.GetSharedMaterials` across the inventory. Every renderer still gets its own immutable names/shader arrays; no native material is instantiated or changed. The buffer dies when the command returns. No cache, frame caller or parked-hotspot rewrite is introduced.

Reproduce with `python tools/street-proxy-regression/compare_buffers.py`. It generates an ignored baseline project using the actual adapter and identical doubles/fixtures; the only source change creates a fresh shared-material list for each renderer. The generated baseline removes the unused caller buffer, so its allocation is not charged twice. It runs the required other-dotnet wait before each synchronous foreground invocation, without timeouts. Both variants pass 2,028 actual-source assertions. Raw output: ignored `build/f1b/buffer-comparison/{before,after}.txt`.

Windows .NET 10 Release, 20 warm-ups; single-call timings, metadata creation included, JSON/native-engine/graphics work excluded:

| Fixture | Before p50 / p95 / max ms | After p50 / p95 / max ms | Mean allocation before → after |
|---|---|---|---|
| 129 renderers, three bodies, light/volume; 300 calls | 0.1786 /0.3153 /10.4334 | 0.1694 /0.1935 /3.4292 | 80,838.96 →69,547.73 B/call |
| Row-cap refusal, 1,025 renderers; 50 calls | 1.1371 /1.5706 /2.5051 | 1.0965 /1.6218 /2.4965 | 549,980.32 →460,348.80 B/call |

The representative fixture avoids 128 repeated list/buffer allocations (about 11.3 KB less measured allocation, ~14%). Timing varies with JIT/framework metadata caching/GC; the cap fixture's p95 is slightly higher after, so no universal throughput improvement is claimed. All sampled single calls in both comparator runs are below 30 ms; spike count is zero. Initial command JIT/reflection cost is reported separately in [checks](../checks/f1b-street-proxy.md).

This is a one-shot diagnostic win, not a frame-time improvement or native performance clearance. Incremental F1b work/allocations with commands uncalled are zero by source construction. Explicit disabled calls also measure 0 B. No renderer exists; +2-4 ms whole-frame remains a planning estimate. Mod-work p95 <5 ms and every spike >=30 ms remain the normal combined-session gates, separate from whole-frame observations.
