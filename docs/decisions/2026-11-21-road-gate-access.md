# Road gate access, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- Public road access excludes every vehicle gate, whoever owns the property ([RoadRoutes](../../Runtime/RoadRoutes.cs), `PublicRoadAccess`). The fallback box for gates without a shape is tracked in [#331](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/331).
- `TransportAccessGeometry.IntersectsBounds` ([TransportComposition](../../Core/TransportComposition.cs)) tests a segment against one bounded gate region. An unknown gate never denies unrelated city coordinates, and touching the boundary counts as a crossing.
