using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new InvalidOperationException(message); }

    // Read the captured boxes once, outside measurements. The oracle is the original full-list scan,
    // independent of the index and its bucket assignment, including retained moving-leaf envelopes.
    private sealed class LinearAccess
    {
        internal readonly struct Gate
        {
            public Gate(Position min, Position max, string door) { Min = min; Max = max; Door = door; }
            public Position Min { get; }
            public Position Max { get; }
            public string Door { get; }
        }
        internal readonly List<Gate> Gates = new List<Gate>();
        public LinearAccess(PublicRoadAccess access)
        {
            var field = typeof(PublicRoadAccess).GetField("gates", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (object box in (IEnumerable)field.GetValue(access)!)
            {
                var type = box.GetType();
                Gates.Add(new Gate((Position)type.GetProperty("Min")!.GetValue(box)!,
                    (Position)type.GetProperty("Max")!.GetValue(box)!, (string)type.GetProperty("Door")!.GetValue(box)!));
            }
        }
        public bool ClearSegment(Position a, Position b)
        {
            if (!a.IsFinite || !b.IsFinite) return false;
            foreach (var gate in Gates)
                if (TransportAccessGeometry.IntersectsBounds(a, b, gate.Min, gate.Max)) return false;
            return true;
        }
    }

    private static FenceDoor Door(float x, float y, float z, float width = 4, float height = 4, float depth = 2)
        => new FenceDoor
        {
            acceptsVehicles = true, name = "gate",
            Colliders = new[] { new Collider { bounds = new Bounds(new Vector3(x, y, z), new Vector3(width, height, depth)) } }
        };

    private static FenceDoor[] DistantDoors(int count) => Enumerable.Range(0, count)
        .Select(i => Door(500 + i % 16 * 150, 0, 500 + i / 16 * 150)).ToArray();

    private static void Compare(PublicRoadAccess indexed, LinearAccess linear, Position a, Position b)
    {
        Check(indexed.ClearSegment(a, b) == linear.ClearSegment(a, b), "Indexed segment differs from the original scan");
        Check(indexed.ClearSegment(b, a) == linear.ClearSegment(b, a), "Reversed segment differs from the original scan");
    }

    private static void Differential()
    {
        var random = new Random(74193);
        double Coordinate() => random.NextDouble() * 5000 - 2500;
        foreach (int count in new[] { 0, 1, 8, 9, 32, 128, 512 })
        {
            Object.Doors = Enumerable.Range(0, count).Select(i => Door((float)Coordinate(), (float)(Coordinate() / 100),
                (float)Coordinate(), i % 17 == 0 ? 700 : 4, 4, i % 17 == 0 ? 700 : 2)).ToArray();
            var access = new PublicRoadAccess(); var linear = new LinearAccess(access);
            for (int i = 0; i < 12000; i++)
            {
                var a = new Position(Coordinate(), Coordinate() / 100, Coordinate());
                var b = i % 4 == 0 ? new Position(Coordinate(), Coordinate() / 100, Coordinate())
                    : new Position(a.X + random.NextDouble() * 80 - 40, a.Y + random.NextDouble() * 10 - 5, a.Z + random.NextDouble() * 80 - 40);
                Compare(access, linear, a, b);
            }
            foreach (var gate in linear.Gates)
            {
                // Exact faces, edges, corners, zero-length segments, and a point one representable value outside.
                foreach (double x in new[] { gate.Min.X, gate.Max.X, Math.BitDecrement(gate.Min.X), Math.BitIncrement(gate.Max.X) })
                    foreach (double y in new[] { gate.Min.Y, gate.Max.Y })
                        foreach (double z in new[] { gate.Min.Z, gate.Max.Z })
                        {
                            var point = new Position(x, y, z);
                            Compare(access, linear, point, point);
                            Compare(access, linear, point, new Position(x, y, z + 80));
                        }
            }
        }
    }

    private static void BoundariesAndFallbacks()
    {
        // Expanded boxes have faces exactly on positive, negative and zero cell boundaries.
        var special = new[] { Door(48, 0, 48, 3, 2, 3), Door(-48, 0, -48, 3, 2, 3),
            Door(0, 0, 0, 99, 2, 99), Door(10000, 0, 10000, 50000, 4, 50000),
            new FenceDoor { acceptsVehicles = true, transform = new Transform { position = new Vector3(-100, 0, -100) } },
            Door(float.MaxValue, 0, 0), Door(-float.MaxValue, 0, 0) };
        foreach (var gate in special)
        {
            // Test separately so a huge fallback box cannot mask a missed small indexed gate.
            Object.Doors = DistantDoors(12).Concat(new[] { gate }).ToArray();
            var indexed = new PublicRoadAccess(); var oracle = new LinearAccess(indexed);
            foreach (double x in new[] { -100d, -50, -0.000001, 0, 50, 100, (double)int.MaxValue * 50,
                (double)int.MinValue * 50, double.MaxValue, -double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                foreach (double z in new[] { -50d, 0, 50, 100000 })
                    foreach (double y in new[] { 0d, 100 })
                        Compare(indexed, oracle, new Position(x, y, z), new Position(x, y, z + 100));
            Compare(indexed, oracle, new Position(-double.MaxValue, 0, 0), new Position(double.MaxValue, 0, 0));
            Compare(indexed, oracle, new Position(0, double.NaN, 0), new Position(0, 0, 0));
            Compare(indexed, oracle, new Position(0, 0, double.PositiveInfinity), new Position(0, 0, 0));
        }
        foreach (float centre in new[] { 48f, -48f })
        {
            Object.Doors = DistantDoors(12).Concat(new[] { Door(centre, 0, 0, 3, 2, 3) }).ToArray();
            var indexed = new PublicRoadAccess();
            double face = centre > 0 ? 50 : -50;
            Check(!indexed.ClearSegment(new Position(face, 0, -10), new Position(face, 0, 10)), "Exact cell-boundary contact was missed");
            double outside = centre > 0 ? Math.BitIncrement(face) : Math.BitDecrement(face);
            Check(indexed.ClearSegment(new Position(outside, 0, -10), new Position(outside, 0, 10)), "Outside cell-boundary point was falsely blocked");
            Check(indexed.ClearSegment(new Position(face, 50, -10), new Position(face, 50, 10)), "Grid discarded vertical clearance");
        }
        // Invalid fallback geometry must still reach the original predicate, not silently disappear from the index.
        Object.Doors = DistantDoors(12).Concat(new[] { new FenceDoor { acceptsVehicles = true,
            transform = new Transform { position = new Vector3(float.NaN, 0, 0) } } }).ToArray();
        var access = new PublicRoadAccess(); var linear = new LinearAccess(access);
        Compare(access, linear, new Position(-5, 0, 0), new Position(5, 0, 0));
    }

    private static void Refreshes()
    {
        var door = Door(0, 0, 0); var remote = DistantDoors(16);
        Object.Doors = remote.Concat(new[] { door }).ToArray();
        var original = new PublicRoadAccess();
        var a = new Position(-10, 0, 0); var b = new Position(10, 0, 0);
        Check(!original.ClearSegment(a, b), "Initial gate did not block");
        var leaf = new Transform(); door.doors = new[] { leaf };
        door.Colliders = new[] { door.Colliders[0], new Collider { transform = new Transform { parent = leaf },
            bounds = new Bounds(new Vector3(100, 0, 0), new Vector3(4, 4, 2)) } };
        var grown = original.Refreshed(out _)!;
        Check(grown != null && !grown.ClearSegment(new Position(100, 0, -10), new Position(100, 0, 10)), "Leaf growth into another cell was missed");
        door.Colliders[1].bounds = new Bounds(new Vector3(-100, 0, 0), new Vector3(4, 4, 2));
        var both = grown!.Refreshed(out _)!;
        Check(both != null && !both.ClearSegment(new Position(100, 0, -10), new Position(100, 0, 10))
            && !both.ClearSegment(new Position(-100, 0, -10), new Position(-100, 0, 10)), "Retained leaf envelope lost an indexed cell");
        door.Colliders[1].bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(4, 4, 2));
        Check(both!.Refreshed(out _) == null, "Leaf shrinking within retained envelope changed gates");
        Object.Doors = Object.Doors.Reverse().ToArray();
        Check(both.Refreshed(out _) == null, "Door order changed the snapshot");
        door.Colliders[0].bounds = new Bounds(new Vector3(0, 0, 300), new Vector3(4, 4, 2));
        door.Colliders = new[] { door.Colliders[0] };
        var moved = both.Refreshed(out _)!;
        Check(moved != null && moved.ClearSegment(a, b), "Moving a gate retained its old blocked cell");
        Check(!moved!.ClearSegment(new Position(-10, 0, 300), new Position(10, 0, 300)), "Moved gate's new cell stayed clear");
        Check(!original.ClearSegment(a, b), "Refreshing mutated the prior snapshot");
        Object.Doors = remote;
        var removed = moved.Refreshed(out _)!;
        Check(removed != null && removed.ClearSegment(new Position(-10, 0, 300), new Position(10, 0, 300)), "Removed gate remained indexed");
        Object.Doors = remote.Concat(new[] { Door(0, 0, 0) }).ToArray();
        var added = removed!.Refreshed(out _)!;
        Check(added != null && !added.ClearSegment(a, b), "New gate was not indexed");
    }

    private static void RepeatedCandidates()
    {
        // Twelve gates: four high boxes each occupy X cells 0, 1 and 2, and a low blocker sits in cell 2.
        // The first two buckets pass (4 + 4 tests). The third has five entries against four remaining,
        // forcing the mid-scan fallback, which must still find the blocker. The high query is clear.
        Object.Doors = Enumerable.Range(0, 4).Select(_ => Door(75, 20, 25, 148, 4, 2))
            .Concat(new[] { Door(135, 0, 25) }).Concat(DistantDoors(7)).ToArray();
        var access = new PublicRoadAccess(); var linear = new LinearAccess(access);
        foreach (double y in new[] { 0d, 20, 50 })
            foreach (double end in new[] { 49d, 99, 149 })
            {
                var a = new Position(1, y, 25); var b = new Position(end, y, 25);
                Compare(access, linear, a, b);
                Check(access.ClearSegment(a, b) == (y == 50 || y == 0 && end < 135), "Repeated-candidate fixture lost its blocked/clear outcome");
            }
        // Equality at the budget boundary, with no blocker. This also reaches the third-bucket fallback.
        Object.Doors = Enumerable.Range(0, 4).Select(_ => Door(75, 20, 25, 148, 4, 2)).Concat(DistantDoors(8)).ToArray();
        access = new PublicRoadAccess(); linear = new LinearAccess(access);
        Compare(access, linear, new Position(1, 0, 25), new Position(149, 0, 25));
        Check(access.ClearSegment(new Position(1, 0, 25), new Position(149, 0, 25)), "Budget-equality fallback falsely blocked a clear query");
    }

    private static void GatedDetour()
    {
        var points = new[] { new Position(0, 0, 0), new Position(30, 0, 0), new Position(60, 0, 0),
            new Position(0, 0, 30), new Position(30, 0, 30), new Position(60, 0, 30) };
        var successors = new[] { new[] { 1, 3 }, new[] { 2 }, Array.Empty<int>(), new[] { 4 }, new[] { 5 }, new[] { 2 } };
        var graph = new TransportRoadGraph(points.Select((p, i) => new TransportRoadNode
        { Position = p, Successors = successors[i], AllowsCar = true, KilometresPerHour = 30 }).ToArray());
        int[]? ungated = null;
        var direct = graph.Plan(0, 2, _ => false, (_, _) => true, pathFound: path => ungated = path);
        Check(direct != null && ungated!.SequenceEqual(new[] { 0, 1, 2 }), "Ungated fixture did not take the short route");
        Object.Doors = DistantDoors(16).Concat(new[] { Door(15, 0, 0) }).ToArray();
        var access = new PublicRoadAccess();
        for (int phase = 0; phase < 3; phase++)
        {
            var linear = new LinearAccess(access);
            int[]? expected = null, actual = null; int denied = 0;
            bool Clear(Position a, Position b) { bool result = access.ClearSegment(a, b); if (!result) denied++; return result; }
            var before = graph.Plan(0, 2, _ => false, linear.ClearSegment, pathFound: path => expected = path);
            var after = graph.Plan(0, 2, _ => false, Clear, pathFound: path => actual = path);
            Check(denied > 0, "Route comparison never exercised a gate-blocked edge");
            if (phase == 1) Check(before == null && after == null, "Gates on both paths did not make the goal unreachable");
            else
            {
                Check(before != null && after != null && before.Minutes == after.Minutes, "Gate changed indexed route feasibility/cost");
                Check(actual != null && expected != null && actual.SequenceEqual(expected)
                    && actual.SequenceEqual(new[] { 0, 3, 4, 5, 2 }), "Gate did not force the expected detour");
                Check(after!.Minutes > direct!.Minutes, "Gated route did not differ from the ungated route");
            }
            if (phase == 0) Object.Doors = Object.Doors.Concat(new[] { Door(60, 0, 15) }).ToArray();
            else if (phase == 1) Object.Doors = Object.Doors.Take(Object.Doors.Length - 1).ToArray();
            if (phase < 2)
            {
                var next = access.Refreshed(out _);
                Check(next != null, "Gate addition/removal did not refresh routing access"); access = next!;
            }
        }
    }

    private static void RouteParity()
    {
        // Gates lie on horizontal road edges, rather than in the gaps between the road grid lines.
        Object.Doors = Enumerable.Range(0, 64).Select(i => Door(315 + i % 8 * 90, 0, 300 + i / 8 * 90)).ToArray();
        var access = new PublicRoadAccess(); var linear = new LinearAccess(access);
        const int side = 40;
        var nodes = Enumerable.Range(0, side * side).Select(i => new TransportRoadNode
        {
            Position = new Position(i % side * 30, 0, i / side * 30), AllowsCar = true, KilometresPerHour = 30,
            Successors = new[] { i % side > 0 ? i - 1 : -1, i % side < side - 1 ? i + 1 : -1,
                i >= side ? i - side : -1, i < side * (side - 1) ? i + side : -1 }.Where(n => n >= 0).ToArray()
        }).ToArray();
        var graph = new TransportRoadGraph(nodes);
        var random = new Random(17239);
        int denied = 0;
        bool Clear(Position a, Position b) { bool result = access.ClearSegment(a, b); if (!result) denied++; return result; }
        for (int i = 0; i < 80; i++)
        {
            int from = random.Next(nodes.Length), to = random.Next(nodes.Length);
            // Some cases exhaust the graph at an unreachable goal; some stop at the expansion cap.
            bool Disabled(int node) => node != from && (i % 3 == 0 ? nodes[node].Successors.Contains(to) : node % 71 == 0);
            int limit = i % 5 == 0 ? 20 : 30000;
            int[]? expectedPath = null, actualPath = null;
            var before = graph.Plan(from, to, Disabled, linear.ClearSegment, limit, path => expectedPath = path);
            var after = graph.Plan(from, to, Disabled, Clear, limit, path => actualPath = path);
            Check((before == null) == (after == null), "Index changed route feasibility");
            if (before != null)
            {
                Check(before.Minutes == after!.Minutes, "Index changed route cost");
                Check(expectedPath != null && actualPath != null && expectedPath.SequenceEqual(actualPath), "Index changed chosen road nodes");
            }
        }
        Check(denied > 0, "Seeded road comparisons never exercised a gate-blocked edge");
    }

    private static (double Milliseconds, long Bytes, int Clear) Measure(Func<Position, Position, bool> clear,
        (Position A, Position B)[] queries, int repetitions)
    {
        int count = 0;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int n = 0; n < repetitions; n++)
            foreach (var query in queries) if (clear(query.A, query.B)) count++;
        return ((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency,
            GC.GetAllocatedBytesForCurrentThread() - bytes, count);
    }

    private static void Benchmark()
    {
        Console.WriteLine("Synthetic identical-query benchmark; 5 alternating pairs, median ms, 200000 queries per run. Not game/FPS evidence.");
        Console.WriteLine("gates,layout,linear_ms,indexed_ms,speedup,indexed_allocated_bytes");
        foreach (int count in new[] { 1, 8, 32, 128, 512 })
            foreach (string layout in new[] { "spread", "clustered", "overlap" })
            {
                bool clustered = layout == "clustered", overlap = layout == "overlap";
                Object.Doors = overlap ? Enumerable.Range(0, count).Select(i => Door(0, 0, 0, 300, 4, 300)).ToArray()
                    : clustered ? Enumerable.Range(0, count).Select(i => Door(i % 8 * 4, 0, i / 8 * 4)).ToArray() : DistantDoors(count);
                var access = new PublicRoadAccess(); var linear = new LinearAccess(access);
                var random = new Random(9723);
                var queries = Enumerable.Range(0, 2000).Select(i =>
                {
                    if (overlap) return (new Position(-140, 20, -140), new Position(140, 20, 140));
                    double x = clustered ? random.NextDouble() * 40 : random.NextDouble() * 3000;
                    double z = clustered ? random.NextDouble() * 280 : random.NextDouble() * 5500;
                    double y = i % 3 == 0 ? 20 : 0;
                    return (new Position(x, y, z), new Position(x + 20, y, z + 30));
                }).ToArray();
                // Aggregate clear counts could hide cancelling mistakes. Verify every timed request first.
                foreach (var query in queries)
                    Check(linear.ClearSegment(query.Item1, query.Item2) == access.ClearSegment(query.Item1, query.Item2), "Benchmark query changed its answer");
                // Warm tiered JIT for both paths before timing or measuring allocation.
                Measure(linear.ClearSegment, queries, 100); Measure(access.ClearSegment, queries, 100);
                var oldTimes = new List<double>(); var newTimes = new List<double>(); long allocated = 0;
                for (int n = 0; n < 5; n++)
                {
                    var first = Measure(n % 2 == 0 ? linear.ClearSegment : access.ClearSegment, queries, 100);
                    var second = Measure(n % 2 == 0 ? access.ClearSegment : linear.ClearSegment, queries, 100);
                    var before = n % 2 == 0 ? first : second; var after = n % 2 == 0 ? second : first;
                    Check(before.Clear == after.Clear, "Benchmark changed answers");
                    Check(after.Bytes == 0, "Indexed hot path allocated");
                    oldTimes.Add(before.Milliseconds); newTimes.Add(after.Milliseconds); allocated += after.Bytes;
                }
                oldTimes.Sort(); newTimes.Sort();
                Console.WriteLine(FormattableString.Invariant($"{count},{layout},{oldTimes[2]:F3},{newTimes[2]:F3},{oldTimes[2] / newTimes[2]:F2},{allocated}"));
            }
    }

    public static void Main(string[] args)
    {
        StagingHarness.Guard.MainThread = Environment.CurrentManagedThreadId;
        try
        {
            Differential(); BoundariesAndFallbacks(); Refreshes(); RepeatedCandidates(); GatedDetour(); RouteParity();
            if (args.Contains("--benchmark")) Benchmark();
            Console.WriteLine($"PASS {assertions} gate-access checks against actual Runtime PublicRoadAccess; offline only.");
        }
        finally { Object.Doors = Array.Empty<FenceDoor>(); }
    }
}
