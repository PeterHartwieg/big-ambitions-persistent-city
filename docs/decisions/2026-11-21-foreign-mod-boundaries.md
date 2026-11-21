# Foreign mod boundaries, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- The third-party interior guard ([ThirdPartyInteriorGuard](../../Core/ThirdPartyInteriorGuard.cs)) matches exact names or one trailing `*` prefix. It uses no regex, type loading, foreign API calls or knowledge of another mod's implementation.
- RLO test fixtures ([RloCompatibilityTests](../../tests/RloCompatibilityTests.cs)) are synthetic public shapes. They are not copied RLO rules and never stand in for a production population.
- The F4b perf probe ([Probe](../../tools/perf-union-probe/Probe.cs)) lives outside the `persistentcity` owner namespaces, so the scope inventory never sees it.
