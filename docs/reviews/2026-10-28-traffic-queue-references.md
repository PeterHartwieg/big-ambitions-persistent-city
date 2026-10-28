# Traffic queue references — 28 October 2026

Partial finding 4 of the architecture performance review. `TrafficKernel.Link.Queue` now holds its owned `TrafficVehicleState` references. Reading queue heads, occupants and predecessors no longer hashes a string and looks up the same object repeatedly. `Snapshot` retains the node it already found for curb-exit checks instead of calling `Queue.Find` for a second walk. Ordering, floating-point operations, event scheduling and physical permissions are unchanged.

Sources and external lookup keys remain strings. Capture emits the same ordered IDs into a preallocated list. Restore resolves each validated ID to that restored kernel's own vehicle. Projection builds its vehicle copies first and then links queue nodes to those copies, never to live vehicles. Cancellation removes the owned object; a subsequent enqueue of the same external ID owns a fresh object. Save schema and policy versions are unchanged.

This is **not** an O(1) snapshot cache or a replacement of the projection kernel. Snapshot remains O(queue), link storage remains sorted and sparse-ID compatible, and forecasts still construct detached projections. No native speedup is claimed.

## Offline evidence

- `tools/traffic-queue-regression/compare.py` compares the same program against baseline `63248e4` and this tree: 32,414 observations match exactly, digest `D328799A130E4BF1834A448B46765F4D5BC1750A21DF86B879B42B97307ABAFE`. Includes sparse IDs, budgets 1/17/10,000, callbacks, closures, physical control/progress, curb exits, cancellation/reused IDs, interrupted restore and a 400-vehicle queue. Forecasts cannot mutate live captures; restore preserves exact captures.
- Two new unit tests independently check projection isolation and restored cancellation/reuse.
- Existing core-identity differential, 600 residents / six days: 38,924 activity events, traffic/request/probe digests and all seven city-save hashes match, including a mid-commute reload. Final run's total advance allocations are 120,431,064 versus 120,440,232 bytes; this does not establish an allocation reduction or a timing improvement.
- Integrated suite: 1,267 passed. Road-ceiling, road-handoff and traffic-closure harnesses pass.

No game launched; all evidence is offline/source/build only. Full candidate native-target build is recorded in CHECKS.md. No installation or release approval.
