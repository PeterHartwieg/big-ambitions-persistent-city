using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal static class GateStatusReport
    {
        public const int MaximumRows = 40;
        public const string Usage = "pc gate status [collider|renderer|footprint|fallback|all] | pc gate status near <x>,<z>,<radius>";

        public static bool TryParse(string detail, string value, out PublicRoadAccess.GateShape? source, out bool list,
            out double x, out double z, out double radius, out string error)
        {
            source = null; list = false; x = z = radius = double.NaN; error = "";
            if (detail.Length == 0 && value.Length == 0) return true;
            if (string.Equals(detail, "near", StringComparison.OrdinalIgnoreCase))
            {
                var parts = value.Split(',');
                if (parts.Length != 3 || !Number(parts[0], out x) || !Number(parts[1], out z) || !Number(parts[2], out radius) || radius < 0)
                { error = "near needs <x>,<z>,<radius> with a non-negative radius"; return false; }
                list = true;
                return true;
            }
            if (value.Length > 0) { error = "unexpected argument '" + value + "'"; return false; }
            list = true;
            if (string.Equals(detail, "all", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (PublicRoadAccess.GateShape shape in Enum.GetValues(typeof(PublicRoadAccess.GateShape)))
                if (string.Equals(detail, Name(shape), StringComparison.OrdinalIgnoreCase)) { source = shape; return true; }
            error = "unknown gate source '" + detail + "'";
            return false;
        }

        public static string Format(IReadOnlyList<PublicRoadAccess.GateReading> gates, Func<FenceDoor, string> building,
            IReadOnlyList<TransportRoadNode>? roads, string roadState, Func<int, int> roadId,
            PublicRoadAccess.GateShape? source, bool list, double x, double z, double radius)
        {
            var counts = new int[4];
            foreach (var gate in gates) counts[(int)gate.Source]++;
            var text = new StringBuilder("[Persistent City] gate status (read-only)\n");
            text.Append("gates=").Append(gates.Count);
            for (int i = 0; i < counts.Length; i++) text.Append(' ').Append(Name((PublicRoadAccess.GateShape)i)).Append('=').Append(counts[i]);
            text.Append("\nroad_snapshot=").Append(roadState).Append(" nodes=").Append(roads?.Count ?? 0);
            if (!list) return text.ToString();
            bool near = !double.IsNaN(radius);
            var segments = string.Equals(roadState, "ready", StringComparison.Ordinal) && roads != null ? new RoadSegments(roads) : null;
            var matched = new List<(PublicRoadAccess.GateReading Gate, int Hits, int From, int To)>();
            foreach (var gate in gates)
            {
                if (source.HasValue && gate.Source != source.Value) continue;
                if (near && !Within(gate, x, z, radius)) continue;
                int from = -1, to = -1;
                int hits = segments?.Hits(gate.Min, gate.Max, out from, out to) ?? 0;
                matched.Add((gate, hits, from, to));
            }
            matched.Sort((a, b) => a.Hits > 0 != b.Hits > 0 ? (a.Hits > 0 ? -1 : 1) : string.CompareOrdinal(a.Gate.Name, b.Gate.Name));
            int shown = Math.Min(matched.Count, MaximumRows);
            text.Append("\nfilter=").Append(near ? "near " + Num(x) + "," + Num(z) + "," + Num(radius) : source.HasValue ? Name(source.Value) : "all")
                .Append(" matched=").Append(matched.Count).Append(" shown=").Append(shown).Append(" omitted=").Append(matched.Count - shown);
            for (int i = 0; i < shown; i++)
            {
                var (gate, hits, from, to) = matched[i];
                text.Append("\ndoor=").Append(gate.Name).Append(" | building=").Append(building(gate.Door))
                    .Append(" | source=").Append(Name(gate.Source))
                    .Append(" | min=").Append(Point(gate.Min)).Append(" | max=").Append(Point(gate.Max))
                    .Append(" | public_road=");
                if (segments == null) text.Append("unknown");
                else if (hits == 0) text.Append("no");
                else text.Append("yes segments=").Append(hits).Append(" first=").Append(from).Append("->").Append(to)
                    .Append(" road_id=").Append(roadId(from));
            }
            return text.ToString();
        }

        public static string Name(PublicRoadAccess.GateShape shape) => shape switch
        {
            PublicRoadAccess.GateShape.Collider => "collider",
            PublicRoadAccess.GateShape.Renderer => "renderer",
            PublicRoadAccess.GateShape.Footprint => "footprint",
            _ => "fallback"
        };

        private sealed class RoadSegments
        {
            private const double CellMetres = 64;
            private const int MaximumCells = 4096;
            private readonly List<(int From, int To, Position A, Position B)> segments = new List<(int, int, Position, Position)>();
            private readonly Dictionary<(int X, int Z), List<int>> cells = new Dictionary<(int, int), List<int>>();
            private int[] seen = Array.Empty<int>();
            private int stamp;

            public RoadSegments(IReadOnlyList<TransportRoadNode> roads)
            {
                for (int i = 0; i < roads.Count; i++)
                {
                    var node = roads[i];
                    if (!Usable(node)) continue;
                    foreach (int next in node.Successors)
                    {
                        if (next == i || next < 0 || next >= roads.Count || !Usable(roads[next])
                            || !TrafficNetwork.EdgeLength(Position.Distance(node.Position, roads[next].Position))) continue;
                        int index = segments.Count;
                        var a = node.Position; var b = roads[next].Position;
                        segments.Add((i, next, a, b));
                        for (int cx = Cell(Math.Min(a.X, b.X)); cx <= Cell(Math.Max(a.X, b.X)); cx++)
                            for (int cz = Cell(Math.Min(a.Z, b.Z)); cz <= Cell(Math.Max(a.Z, b.Z)); cz++)
                            {
                                if (!cells.TryGetValue((cx, cz), out var bucket)) cells.Add((cx, cz), bucket = new List<int>());
                                bucket.Add(index);
                            }
                    }
                }
                seen = new int[segments.Count];
            }

            public int Hits(Position min, Position max, out int from, out int to)
            {
                from = to = -1;
                int hits = 0, first = int.MaxValue;
                double x0 = Math.Floor(min.X / CellMetres), x1 = Math.Floor(max.X / CellMetres);
                double z0 = Math.Floor(min.Z / CellMetres), z1 = Math.Floor(max.Z / CellMetres);
                if (!Position.Finite(x0) || !Position.Finite(x1) || !Position.Finite(z0) || !Position.Finite(z1)
                    || x1 < x0 || z1 < z0 || (x1 - x0 + 1) * (z1 - z0 + 1) > MaximumCells)
                {
                    for (int i = 0; i < segments.Count; i++) Test(i, min, max, ref hits, ref first);
                }
                else
                {
                    stamp++;
                    for (int cx = (int)x0; cx <= (int)x1; cx++)
                        for (int cz = (int)z0; cz <= (int)z1; cz++)
                        {
                            if (!cells.TryGetValue((cx, cz), out var bucket)) continue;
                            foreach (int i in bucket)
                            {
                                if (seen[i] == stamp) continue;
                                seen[i] = stamp;
                                Test(i, min, max, ref hits, ref first);
                            }
                        }
                }
                if (hits > 0) { from = segments[first].From; to = segments[first].To; }
                return hits;
            }

            private void Test(int index, Position min, Position max, ref int hits, ref int first)
            {
                var segment = segments[index];
                if (!TransportAccessGeometry.IntersectsBounds(segment.A, segment.B, min, max)) return;
                hits++;
                if (index < first) first = index;
            }

            private static bool Usable(TransportRoadNode? node) => node != null && node.AllowsCar && node.Position.IsFinite
                && Position.Finite(node.KilometresPerHour) && node.KilometresPerHour > 0;

            private static int Cell(double value) => (int)Math.Floor(value / CellMetres);
        }

        private static bool Within(PublicRoadAccess.GateReading gate, double x, double z, double radius)
        {
            double dx = Math.Max(gate.Min.X - x, Math.Max(0, x - gate.Max.X));
            double dz = Math.Max(gate.Min.Z - z, Math.Max(0, z - gate.Max.Z));
            return dx * dx + dz * dz <= radius * radius;
        }

        private static bool Number(string text, out double value)
            => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Position.Finite(value);

        private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Point(Position p) => "(" + Num(p.X) + ", " + Num(p.Y) + ", " + Num(p.Z) + ")";
    }
}
