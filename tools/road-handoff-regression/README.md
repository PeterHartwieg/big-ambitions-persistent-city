# Offline road handoff regression

Run `dotnet run --project tools/road-handoff-regression/RoadHandoffRegression.csproj --configuration Release`.

This C# 9 harness compiles the actual Runtime `RoadRoutes` and staged traffic importer with synthetic Unity/native surfaces. It does not launch or inspect the game. Native metadata getters reject background-thread reads. The synthetic nine-node road includes verified entry/exit associations, partial retimed fragments, and optional consecutive junctions.

Assertions cover current-fragment rejection; later full-edge sampling without fragment driving targets; preserved historical junction distances and identities; normal approach/interior/rear-clearance admission; recovery after verified rear clearance; a future balanced junction that must not hide insufficient prior-exit clearance; a fragment beginning inside an edge that cannot invent a missing entry; malformed/unknown native history; off-edge/reversed fragments; a future fragment bounding the full-edge target span; and sub-millimetre splits just after an exit or just before an entry, with conservative boundary distances and later recovery. The exact two-junction case places exit A at 0, the next native node at 2, the sample at 2.1, and junction B at 20/30 with a four-metre vehicle plus one-metre margin. It asserts that B alone allows admission while A still blocks it, then verifies recovery beyond A's clearance distance.

Later full edges can receive a body only when ordinary native reference checks and junction clearance allow it. Passing this synthetic harness is not evidence of immediate in-game appearance or live native behavior.

The sample-scope checks count native validation directly: each node touched by one `TryPhysicalRoadSample` is validated once and each junction contract once, while two public `ResolveWaypoint` calls still validate twice. With no frame boundary between samples, closures and reopenings of historical, entry, interior, exit and final nodes, and junction, flag, restriction and identity changes, must be seen by the next sample. A reentrant sample inside a native read is refused while a public resolve there stays live; an injected read exception, an unverified speed change and a generation wrap leave no scope or validation behind.

The leading-exit assertion follows the current `PhysicalRoadAdmission` rule: a lone historical exit proves outside only after the whole body and margin have cleared it. It previously expected refusal at any distance, which predated that rule.
