using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PersistentCity.Core
{
    public interface ITrafficRoutePlanner
    {
        TrafficNetwork? TrafficNetwork { get; }
        void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast);
        bool IsTrafficLinkClosed(int linkId);
    }

    /// <summary>Optional validation scope for one synchronous closure refresh. Native state must not
    /// change inside the scope; results must be discarded at End, including after an exception.</summary>
    public interface ITrafficClosureBatchPlanner
    {
        void BeginTrafficClosureBatch();
        void EndTrafficClosureBatch();
    }

    /// <summary>Optional first-edge entry estimate for conservative interior curb admission.</summary>
    public interface ITrafficEntryRoutePlanner
    {
        void SetTrafficEntryForecast(Func<int, double, double, double, double>? forecast);
    }

    /// <summary>Native-free structural snapshot. Boundary includes junctions, lane changes and access changes.</summary>
    public sealed class TrafficNetworkNode
    {
        public int Id { get; set; }
        public Position Position { get; set; }
        public double KilometresPerHour { get; set; }
        public bool AllowsCar { get; set; }
        public bool Boundary { get; set; }
        public string StructuralContract { get; set; } = "";
        public int[] Successors { get; set; } = Array.Empty<int>();
        public TrafficSignalSchedule? Signal { get; set; }
    }

    public readonly struct TrafficNetworkEdge
    {
        internal TrafficNetworkEdge(int from, int to, int link, double minutes, double startFraction, double endFraction)
        { From = from; To = to; LinkId = link; FreeFlowMinutes = minutes; StartLinkFraction = startFraction; EndLinkFraction = endFraction; }
        public int From { get; }
        public int To { get; }
        public int LinkId { get; }
        public double FreeFlowMinutes { get; }
        public double StartLinkFraction { get; }
        public double EndLinkFraction { get; }
    }

    /// <summary>Finite directed sections. Storage/headway and periodic phases are declared policy, not measured native capacities.</summary>
    public sealed class TrafficNetwork
    {
        public const string PolicyId = "directed-sections-v1;max80m;storage6m;headway1.8s;native-periodic-groups";
        private readonly Dictionary<(int, int), TrafficNetworkEdge> edges = new Dictionary<(int, int), TrafficNetworkEdge>();
        private readonly List<TrafficLinkDefinition> links = new List<TrafficLinkDefinition>();
        private readonly List<IReadOnlyList<int>> linkNodes = new List<IReadOnlyList<int>>();
        private readonly Dictionary<int, Position> positions = new Dictionary<int, Position>();
        private readonly List<SectionGeometry> sectionGeometry = new List<SectionGeometry>();
        private const double MappingMetresTolerance = 0.002;

        private sealed class SectionGeometry
        {
            internal readonly Position[] Points;
            internal readonly double[] Distances;
            internal readonly double Length;
            internal readonly double FreeFlowMinutes;
            internal SectionGeometry(Position[] points, double[] distances, double freeFlowMinutes)
            { Points = points; Distances = distances; Length = distances[distances.Length - 1]; FreeFlowMinutes = freeFlowMinutes; }
        }
        public string TopologyId { get; }
        public string NativeTopologyId { get; }
        public IReadOnlyList<TrafficLinkDefinition> Links { get; }

        private sealed class Edge
        {
            public int From, To;
            public double Metres, Minutes;
        }

        public TrafficNetwork(string nativeTopologyId, IReadOnlyList<TrafficNetworkNode> snapshot, double clockRate)
        {
            if (string.IsNullOrEmpty(nativeTopologyId) || snapshot == null || !Position.Finite(clockRate) || clockRate <= 0)
                throw new ArgumentException("Invalid traffic network snapshot.");
            NativeTopologyId = nativeTopologyId;
            var nodes = new Dictionary<int, TrafficNetworkNode>();
            foreach (var node in snapshot)
            {
                if (node == null || node.Id < 0 || node.Successors == null || node.StructuralContract == null
                    || !nodes.TryAdd(node.Id, node)) throw new ArgumentException("Duplicate/invalid traffic node.");
                positions.Add(node.Id, node.Position);
            }
            var outgoing = nodes.Keys.ToDictionary(i => i, i => new List<Edge>());
            var incoming = nodes.Keys.ToDictionary(i => i, i => 0);
            var rawIncoming = nodes.Keys.ToDictionary(i => i, i => 0);
            foreach (var node in nodes.Values)
                foreach (int next in node.Successors.Distinct()) if (rawIncoming.ContainsKey(next)) rawIncoming[next]++;
            var all = new List<Edge>();
            foreach (var node in nodes.Values.OrderBy(n => n.Id))
            {
                if (!Usable(node)) continue;
                foreach (int next in node.Successors.Distinct().OrderBy(i => i))
                {
                    if (next == node.Id || !nodes.TryGetValue(next, out var target) || !Usable(target)) continue;
                    double length = Position.Distance(node.Position, target.Position);
                    if (!Position.Finite(length) || length <= 0.000001 || length > 60) continue;
                    double minutes = length / (Math.Min(40, node.KilometresPerHour) / 3.6 / clockRate);
                    if (!Position.Finite(minutes) || minutes <= 0) throw new ArgumentException("Invalid native edge duration.");
                    var edge = new Edge { From = node.Id, To = next, Metres = length, Minutes = minutes };
                    outgoing[node.Id].Add(edge); incoming[next]++; all.Add(edge);
                }
            }
            bool Interior(int id)
            {
                var node = nodes[id];
                // A filtered/malformed branch is still a boundary; never infer an unbranched road from permission filtering.
                return !node.Boundary && node.Signal == null && incoming[id] == 1 && rawIncoming[id] == 1 && outgoing[id].Count == 1
                    && node.Successors.Length == 1;
            }
            var visited = new HashSet<(int, int)>();
            // Start at proven boundaries first. Remaining degree-one cycles break at their smallest native edge.
            foreach (var first in all.Where(e => !Interior(e.From)).Concat(all))
            {
                if (visited.Contains((first.From, first.To))) continue;
                var chain = new List<Edge>(); var edge = first; double metres = 0, minutes = 0;
                while (true)
                {
                    if (!visited.Add((edge.From, edge.To))) break;
                    chain.Add(edge); metres += edge.Metres; minutes += edge.Minutes;
                    if (!Interior(edge.To)) break;
                    var next = outgoing[edge.To][0];
                    if (visited.Contains((next.From, next.To)) || metres + next.Metres > 80
                        || nodes[edge.From].KilometresPerHour != nodes[edge.To].KilometresPerHour
                        || !string.Equals(nodes[edge.From].StructuralContract, nodes[edge.To].StructuralContract, StringComparison.Ordinal)) break;
                    edge = next;
                }
                int id = links.Count;
                links.Add(new TrafficLinkDefinition
                {
                    Id = id, Capacity = Math.Max(1, checked((int)Math.Floor(Math.Max(metres, 6) / 6))),
                    FreeFlowMinutes = minutes, HeadwayMinutes = 1.8 * clockRate,
                    Signal = CopySignal(nodes[chain[chain.Count - 1].To].Signal)
                });
                var indices = new List<int> { chain[0].From };
                double offset = 0;
                foreach (var part in chain)
                { edges.Add((part.From, part.To), new TrafficNetworkEdge(part.From, part.To, id, part.Minutes, offset / metres, (offset + part.Metres) / metres)); indices.Add(part.To); offset += part.Metres; }
                linkNodes.Add(indices.AsReadOnly());
                var sectionPositions = new Position[indices.Count];
                var sectionDistances = new double[indices.Count];
                double cumulative = 0;
                for (int i = 0; i < indices.Count; i++)
                {
                    sectionPositions[i] = positions[indices[i]];
                    if (i > 0) cumulative += chain[i - 1].Metres;
                    sectionDistances[i] = cumulative;
                }
                sectionGeometry.Add(new SectionGeometry(sectionPositions, sectionDistances, minutes));
            }
            Links = links.AsReadOnly();
            using var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
            {
                writer.Write(PolicyId); writer.Write(nativeTopologyId);
                foreach (var node in nodes.Values.OrderBy(n => n.Id))
                {
                    writer.Write(node.Id); writer.Write(node.Position.X); writer.Write(node.Position.Y); writer.Write(node.Position.Z);
                    writer.Write(node.KilometresPerHour); writer.Write(node.AllowsCar); writer.Write(node.Boundary); writer.Write(node.StructuralContract);
                    writer.Write(node.Successors.Length); foreach (int successor in node.Successors) writer.Write(successor);
                    WriteSignal(writer, node.Signal, clockRate);
                }
                foreach (var link in links) { writer.Write(link.Id); writer.Write(link.Capacity); foreach (int native in linkNodes[link.Id]) writer.Write(native); writer.Write(-1); }
            }
            using var hash = SHA256.Create();
            TopologyId = BitConverter.ToString(hash.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        public IReadOnlyList<int> NativeNodes(int linkId) => linkId >= 0 && linkId < linkNodes.Count
            ? linkNodes[linkId] : throw new ArgumentOutOfRangeException(nameof(linkId));
        public bool TryGetEdge(int from, int to, out TrafficNetworkEdge edge) => edges.TryGetValue((from, to), out edge);

        /// <summary>Offsets address the full committed route. Native fragments preserve directed edge provenance after retiming.</summary>
        public bool TryMap(Route route, out List<TrafficRouteLeg> mapped) => TryMapCore(route, 0, false, out mapped);

        /// <summary>
        /// Maps only uncompleted native road geometry, clipping the current whole edge or explicit
        /// native fragment. Legacy untagged fragments and ordinary connectors remain outside traffic;
        /// callers advance to the first returned StartNominalMinutes before admission.
        /// </summary>
        public bool TryMapRemaining(Route route, double progress, out List<TrafficRouteLeg> mapped) =>
            TryMapCore(route, progress, true, out mapped);

        /// <summary>One mapped traffic span, as TryMapRemaining would return it, without allocating a leg object.</summary>
        internal struct MappedSpan
        {
            public int LinkId;
            public double StartNominalMinutes, EndNominalMinutes, FreeFlowMinutes, StartLinkFraction, EndLinkFraction;
        }

        [ThreadStatic] private static List<MappedSpan>? mappingScratch;

        /// <summary>TryMapRemaining into a caller-owned buffer, cleared first and left empty on failure.</summary>
        internal bool TryMapRemainingSpans(Route route, double progress, List<MappedSpan> spans) =>
            TryMapSpans(route, progress, true, spans);

        private bool TryMapCore(Route route, double progress, bool remaining, out List<TrafficRouteLeg> mapped)
        {
            var spans = mappingScratch ??= new List<MappedSpan>();
            if (!TryMapSpans(route, progress, remaining, spans)) { mapped = new List<TrafficRouteLeg>(); return false; }
            mapped = new List<TrafficRouteLeg>(spans.Count);
            foreach (var span in spans)
                mapped.Add(new TrafficRouteLeg
                {
                    LinkId = span.LinkId, StartNominalMinutes = span.StartNominalMinutes, EndNominalMinutes = span.EndNominalMinutes,
                    FreeFlowMinutes = span.FreeFlowMinutes, StartLinkFraction = span.StartLinkFraction, EndLinkFraction = span.EndLinkFraction
                });
            spans.Clear();
            return true;
        }

        private bool TryMapSpans(Route route, double progress, bool remaining, List<MappedSpan> mapped)
        {
            mapped.Clear();
            if (route == null || route.Legs == null || !Position.Finite(progress) || progress < 0) return false;
            double nominal = 0, previousEndFraction = 0;
            int previousNativeTo = -1, previousLink = -1;
            Position previousPoint = default;
            bool previousWasRoad = false;
            var legs = route.Legs;
            for (int index = 0; index < legs.Count; index++)
            {
                RouteLeg leg = legs[index];
                if (leg == null || !Position.Finite(leg.Minutes) || leg.Minutes <= 0 || leg.ServiceId == null)
                { mapped.Clear(); return false; }
                double end = nominal + leg.Minutes;
                if (!Position.Finite(end) || end <= nominal) { mapped.Clear(); return false; }
                if (remaining && end <= progress)
                { nominal = end; previousWasRoad = false; continue; }
                if (leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal))
                {
                    if (!TryRoadLeg(leg, out TrafficNetworkEdge edge, out double legStart, out double legEnd))
                    { mapped.Clear(); return false; }
                    double start = remaining ? Math.Max(nominal, progress) : nominal;
                    double consumed = (start - nominal) / leg.Minutes;
                    double startFraction = legStart + consumed * (legEnd - legStart);
                    Position point = Position.Lerp(leg.From, leg.To, consumed);
                    bool sameSection = previousWasRoad && previousLink == edge.LinkId;
                    if (previousWasRoad && (!Near(previousPoint, point) || (sameSection
                        ? Math.Abs(previousEndFraction - startFraction) * sectionGeometry[edge.LinkId].Length > MappingMetresTolerance
                        : previousNativeTo != edge.From || !Near(previousPoint, positions[previousNativeTo]) || !Near(point, positions[edge.From]))))
                    { mapped.Clear(); return false; }
                    if (!previousWasRoad && mapped.Count > 0 && mapped[mapped.Count - 1].LinkId == edge.LinkId)
                    { mapped.Clear(); return false; }
                    if (sameSection)
                    {
                        MappedSpan existing = mapped[mapped.Count - 1];
                        existing.EndNominalMinutes = end;
                        existing.FreeFlowMinutes += end - start;
                        existing.EndLinkFraction = legEnd;
                        mapped[mapped.Count - 1] = existing;
                    }
                    else mapped.Add(new MappedSpan
                    {
                        LinkId = edge.LinkId, StartNominalMinutes = start, EndNominalMinutes = end,
                        FreeFlowMinutes = end - start, StartLinkFraction = startFraction, EndLinkFraction = legEnd
                    });
                    previousNativeTo = edge.To;
                    previousLink = edge.LinkId;
                    previousEndFraction = legEnd;
                    previousPoint = leg.To;
                    previousWasRoad = true;
                }
                else previousWasRoad = false;
                nominal = end;
            }
            if (progress > nominal) { mapped.Clear(); return false; }
            return mapped.Count > 0;
        }

        private bool TryRoadLeg(RouteLeg leg, out TrafficNetworkEdge edge, out double startFraction, out double endFraction)
        {
            edge = default;
            startFraction = endFraction = 0;
            if (leg.Mode != TravelMode.Car || !leg.From.IsFinite || !leg.To.IsFinite ||
                !TryTag(leg, out int from, out int to, out bool fragment) || !edges.TryGetValue((from, to), out edge)) return false;
            double localStart = 0, localEnd = 1;
            if (fragment)
            {
                if (!TryProjectSegment(leg.From, positions[from], positions[to], out localStart) ||
                    !TryProjectSegment(leg.To, positions[from], positions[to], out localEnd) || localEnd <= localStart) return false;
            }
            else if (!Near(leg.From, positions[from]) || !Near(leg.To, positions[to])) return false;
            startFraction = edge.StartLinkFraction + localStart * (edge.EndLinkFraction - edge.StartLinkFraction);
            endFraction = edge.StartLinkFraction + localEnd * (edge.EndLinkFraction - edge.StartLinkFraction);
            return startFraction < endFraction;
        }

        /// <summary>
        /// Read-only save binding check. Every claimed nominal span must follow the directed section
        /// geometry, including its intermediate corners. Timing may change and a current native edge
        /// may have been split into explicit native fragments; geometry and retained edge provenance must agree.
        /// Legacy untagged fragments can still substantiate an individual geometric claim.
        /// </summary>
        public void ValidateMapping(Route journey, IReadOnlyList<TrafficRouteLeg> traffic)
        {
            if (journey == null || journey.Legs == null || journey.Legs.Count == 0 || traffic == null || traffic.Count == 0)
                throw new ArgumentException("A committed journey and traffic mapping are required.");
            var offsets = new double[journey.Legs.Count + 1];
            for (int i = 0; i < journey.Legs.Count; i++)
            {
                RouteLeg leg = journey.Legs[i];
                if (leg == null || !leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) ||
                    leg.Minutes <= 0 || leg.ServiceId == null || !Position.Finite(offsets[i] + leg.Minutes) ||
                    offsets[i] + leg.Minutes <= offsets[i])
                    throw new ArgumentException("Invalid committed journey geometry.", nameof(journey));
                offsets[i + 1] = offsets[i] + leg.Minutes;
            }
            double previousEnd = 0;
            int firstLeg = 0;
            foreach (TrafficRouteLeg span in traffic)
            {
                if (span == null || span.LinkId < 0 || span.LinkId >= sectionGeometry.Count ||
                    !Position.Finite(span.StartNominalMinutes) || !Position.Finite(span.EndNominalMinutes) ||
                    span.StartNominalMinutes < previousEnd || span.EndNominalMinutes <= span.StartNominalMinutes ||
                    span.EndNominalMinutes > offsets[offsets.Length - 1] + 0.0000001 ||
                    !Position.Finite(span.StartLinkFraction) || !Position.Finite(span.EndLinkFraction) ||
                    span.StartLinkFraction < 0 || span.EndLinkFraction > 1 || span.StartLinkFraction >= span.EndLinkFraction)
                    throw new ArgumentException("Invalid traffic mapping span.", nameof(traffic));
                SectionGeometry section = sectionGeometry[span.LinkId];
                double cursor = span.StartLinkFraction * section.Length;
                double endDistance = span.EndLinkFraction * section.Length;
                if (!Near(journey.PositionAt(span.StartNominalMinutes), SectionPoint(section, cursor)) ||
                    !Near(journey.PositionAt(span.EndNominalMinutes), SectionPoint(section, endDistance)))
                    throw new ArgumentException("Traffic section endpoints disagree with the committed journey.", nameof(traffic));
                while (firstLeg < journey.Legs.Count && offsets[firstLeg + 1] <= span.StartNominalMinutes) firstLeg++;
                bool moved = false;
                for (int i = firstLeg; i < journey.Legs.Count && offsets[i] < span.EndNominalMinutes; i++)
                {
                    RouteLeg leg = journey.Legs[i];
                    double fromMinute = Math.Max(offsets[i], span.StartNominalMinutes);
                    double toMinute = Math.Min(offsets[i + 1], span.EndNominalMinutes);
                    if (toMinute <= fromMinute) continue;
                    if (leg.Mode != TravelMode.Car || (Position.Distance(leg.From, leg.To) <= 0.000001 &&
                        !leg.ServiceId.StartsWith("native:road:fragment1:", StringComparison.Ordinal)))
                        throw new ArgumentException("Traffic spans must contain moving car geometry.", nameof(traffic));
                    Position from = Position.Lerp(leg.From, leg.To, (fromMinute - offsets[i]) / leg.Minutes);
                    Position to = Position.Lerp(leg.From, leg.To, (toMinute - offsets[i]) / leg.Minutes);
                    double before = cursor;
                    if (!AdvanceSection(section, from, to, endDistance, ref cursor))
                        throw new ArgumentException("Committed car geometry skips or reverses traffic section geometry.", nameof(traffic));
                    if (leg.ServiceId.Length != 0)
                    {
                        if (!TryRoadLeg(leg, out TrafficNetworkEdge edge, out double legStart, out double legEnd) || edge.LinkId != span.LinkId ||
                            before < legStart * section.Length - MappingMetresTolerance ||
                            cursor > legEnd * section.Length + MappingMetresTolerance)
                            throw new ArgumentException("Native road tag disagrees with the traffic section geometry.", nameof(traffic));
                    }
                    moved = true;
                }
                if (!moved || Math.Abs(cursor - endDistance) > MappingMetresTolerance)
                    throw new ArgumentException("Traffic mapping does not cover its claimed section geometry.", nameof(traffic));
                previousEnd = span.EndNominalMinutes;
            }
        }

        /// <summary>
        /// Binding requires complete future native road ownership and committed section-uniform
        /// travel duration, bounded by the reviewed native slowdown, in addition to valid individual geometry claims. Completed timing may belong to an older
        /// clock rate. An ULP-scale nominal gap is tolerated only when its omitted road distance is also tiny.
        /// </summary>
        public void ValidateRemainingMapping(Route journey, IReadOnlyList<TrafficRouteLeg> traffic, double progress)
        {
            ValidateMapping(journey, traffic);
            if (!Position.Finite(progress) || progress < 0 || progress > journey.Minutes)
                throw new ArgumentException("Invalid traffic journey progress.");
            foreach (TrafficRouteLeg span in traffic)
            {
                if (span.EndNominalMinutes <= progress) continue;
                ValidateRemainingTiming(journey, span, progress);
            }
            double offset = 0;
            int claim = 0;
            foreach (RouteLeg leg in journey.Legs)
            {
                double end = offset + leg.Minutes;
                if (end > progress && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal))
                {
                    // Missing claims cannot hide malformed native tags or replace real edge length
                    // with a forged tiny nominal duration when checking harmless clock roundoff.
                    if (!TryRoadLeg(leg, out _, out _, out _))
                        throw new ArgumentException("Invalid future native road geometry.");
                    double edgeMetres = Position.Distance(leg.From, leg.To);
                    double cursor = Math.Max(offset, progress);
                    while (cursor < end)
                    {
                        while (claim < traffic.Count && traffic[claim].EndNominalMinutes <= cursor) claim++;
                        if (claim >= traffic.Count || traffic[claim].StartNominalMinutes > cursor)
                        {
                            double gapEnd = claim >= traffic.Count ? end : Math.Min(end, traffic[claim].StartNominalMinutes);
                            double missingMinutes = gapEnd - cursor;
                            double roundoff = 1e-12 + 8 * 2.2204460492503131e-16 * Math.Max(1, Math.Max(Math.Abs(cursor), Math.Abs(gapEnd)));
                            double missingMetres = missingMinutes / leg.Minutes * edgeMetres;
                            if (missingMinutes > roundoff || missingMetres > MappingMetresTolerance)
                                throw new ArgumentException("Traffic ownership omits committed future road geometry.");
                            cursor = gapEnd;
                            continue;
                        }
                        cursor = Math.Min(end, traffic[claim].EndNominalMinutes);
                    }
                }
                offset = end;
            }
        }

        /// <summary>Shared exact binding gate for committed nominal duration, including only its
        /// established relative and nominal-coordinate arithmetic allowance.</summary>
        internal static void ValidateCommittedSpanTiming(TrafficRouteLeg span)
        {
            double committed = span.EndNominalMinutes - span.StartNominalMinutes;
            double clockNoise = 8 * 2.2204460492503131e-16
                * Math.Max(Math.Abs(span.StartNominalMinutes), Math.Abs(span.EndNominalMinutes));
            if (!Position.Finite(span.FreeFlowMinutes) || span.FreeFlowMinutes <= 0
                || Math.Abs(span.FreeFlowMinutes - committed) > clockNoise + 1e-8 * committed)
                throw new ArgumentException("Traffic travel duration disagrees with its committed journey timing.");
        }

        private void ValidateRemainingTiming(Route journey, TrafficRouteLeg span, double progress)
        {
            const double maximumSlowdown = 1.75;
            const double relativeTolerance = 1e-8;
            const double doubleEpsilon = 2.2204460492503131e-16;
            SectionGeometry section = sectionGeometry[span.LinkId];
            double metres = (span.EndLinkFraction - span.StartLinkFraction) * section.Length;
            double clockNoise = 8 * doubleEpsilon * Math.Max(Math.Abs(span.StartNominalMinutes), Math.Abs(span.EndNominalMinutes));
            ValidateCommittedSpanTiming(span);

            // Use the largest unfinished committed fragment as the density reference. A tiny
            // cropped kernel residual must not amplify subtraction noise into a new road speed.
            var pieces = new List<(RouteLeg Leg, double Metres)>();
            RouteLeg? reference = null;
            double referenceMetres = 0, offset = 0;
            foreach (RouteLeg leg in journey.Legs)
            {
                double end = offset + leg.Minutes;
                if (end > progress && end > span.StartNominalMinutes && offset < span.EndNominalMinutes)
                {
                    double distance = Position.Distance(leg.From, leg.To);
                    if (!Position.Finite(distance) || distance <= 0)
                        throw new ArgumentException("Traffic timing requires moving committed geometry.");
                    pieces.Add((leg, distance));
                    if (distance > referenceMetres) { reference = leg; referenceMetres = distance; }
                }
                offset = end;
            }
            if (reference == null) throw new ArgumentException("Traffic timing has no unfinished committed geometry.");
            double canonicalDensity = section.FreeFlowMinutes / section.Length;
            double density = reference.Minutes / referenceMetres;
            foreach (var piece in pieces)
            {
                // Retiming interpolates a fragment endpoint in double precision. Limit the
                // numerical exception to that coordinate roundoff, never the geometry tolerance.
                double coordinate = Math.Max(1, Math.Max(Math.Max(Math.Abs(piece.Leg.From.X), Math.Abs(piece.Leg.To.X)),
                    Math.Max(Math.Max(Math.Abs(piece.Leg.From.Y), Math.Abs(piece.Leg.To.Y)),
                        Math.Max(Math.Abs(piece.Leg.From.Z), Math.Abs(piece.Leg.To.Z)))));
                double spatialNoise = 8 * doubleEpsilon * coordinate;
                if (spatialNoise > MappingMetresTolerance)
                    throw new ArgumentException("Traffic timing cannot be verified at this coordinate precision.");
                double canonical = canonicalDensity * piece.Metres;
                double tolerance = relativeTolerance * Math.Max(canonical, piece.Leg.Minutes)
                    + Math.Max(canonicalDensity, density) * spatialNoise;
                if (!Position.Finite(density) || density <= 0 || !Position.Finite(canonical)
                    || piece.Leg.Minutes < canonical - tolerance || piece.Leg.Minutes > maximumSlowdown * canonical + tolerance
                    || Math.Abs(piece.Leg.Minutes - density * piece.Metres) > tolerance)
                    throw new ArgumentException("Unfinished traffic timing must use one bounded slowdown per section metre.");
            }
            double expected = density * metres;
            double geometryNoise = density * 8 * doubleEpsilon * section.Length;
            if (!Position.Finite(expected) || Math.Abs(span.FreeFlowMinutes - expected)
                > clockNoise + geometryNoise + relativeTolerance * Math.Max(expected, span.FreeFlowMinutes))
                throw new ArgumentException("Traffic travel duration disagrees with its committed spatial timing.");
        }

        private static bool Near(Position first, Position second) => first.IsFinite && second.IsFinite &&
            Position.Distance(first, second) <= MappingMetresTolerance;

        private static Position SectionPoint(SectionGeometry section, double distance)
        {
            for (int i = 1; i < section.Points.Length; i++)
                if (distance <= section.Distances[i])
                    return Position.Lerp(section.Points[i - 1], section.Points[i],
                        Math.Max(0, (distance - section.Distances[i - 1]) / (section.Distances[i] - section.Distances[i - 1])));
            return section.Points[section.Points.Length - 1];
        }

        private static bool AdvanceSection(SectionGeometry section, Position from, Position to, double limit, ref double cursor)
        {
            if (!Near(from, SectionPoint(section, cursor))) return false;
            double previousAlong = 0;
            for (int i = 1; i < section.Points.Length && cursor < limit; i++)
            {
                if (section.Distances[i] <= cursor) continue;
                double segmentEnd = Math.Min(limit, section.Distances[i]);
                Position begin = SectionPoint(section, cursor);
                Position end = SectionPoint(section, segmentEnd);
                if (TryProjectSegment(to, begin, end, out double endpointAlong))
                {
                    double next = cursor + endpointAlong * (segmentEnd - cursor);
                    if (next < cursor || (next == cursor && !Near(from, to))) return false;
                    cursor = next;
                    return true;
                }
                // Every intervening native corner must lie on this directed committed segment.
                // A chord through a turn or a collinear reversal cannot prove native road traversal.
                if (!TryProjectSegment(end, from, to, out double along) || along < previousAlong) return false;
                previousAlong = along;
                cursor = segmentEnd;
            }
            return false;
        }

        private static bool TryProjectSegment(Position point, Position from, Position to, out double fraction)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y, dz = to.Z - from.Z;
            double square = dx * dx + dy * dy + dz * dz;
            fraction = 0;
            if (!Position.Finite(square) || square <= 0.000000000001) return Near(point, from);
            double projected = ((point.X - from.X) * dx + (point.Y - from.Y) * dy + (point.Z - from.Z) * dz) / square;
            fraction = Math.Max(0, Math.Min(1, projected));
            return Position.Finite(projected) && Near(point, Position.Lerp(from, to, fraction));
        }

        // The leg decoded its tag when ServiceId was set; only the topology is compared here, in place,
        // so a tag from another network still fails and the caller falls back as before.
        private bool TryTag(RouteLeg leg, out int from, out int to, out bool fragment)
        {
            from = to = -1;
            fragment = false;
            var tag = leg.RoadTag;
            if (leg.ServiceId == null || !tag.IsTopology(leg.ServiceId, NativeTopologyId)) return false;
            from = tag.From; to = tag.To;
            fragment = tag.Kind == RoadTagKind.Fragment1;
            return true;
        }
        private static bool Usable(TrafficNetworkNode node) => node.AllowsCar && node.Position.IsFinite
            && Position.Finite(node.KilometresPerHour) && node.KilometresPerHour > 0;
        private static TrafficSignalSchedule? CopySignal(TrafficSignalSchedule? s) => s == null ? null : new TrafficSignalSchedule
        { CycleMinutes = s.CycleMinutes, GreenStartMinutes = s.GreenStartMinutes, GreenDurationMinutes = s.GreenDurationMinutes, PhaseOffsetMinutes = s.PhaseOffsetMinutes };
        private static void WriteSignal(BinaryWriter writer, TrafficSignalSchedule? s, double clockRate)
        {
            writer.Write(s != null);
            // Structural identity uses native seconds. Round sub-picosecond conversion noise away so a
            // pure clock-rate retime does not look like a different road network.
            if (s != null) { writer.Write(Math.Round(s.CycleMinutes / clockRate, 12)); writer.Write(Math.Round(s.GreenStartMinutes / clockRate, 12)); writer.Write(Math.Round(s.GreenDurationMinutes / clockRate, 12)); writer.Write(Math.Round(s.PhaseOffsetMinutes / clockRate, 12)); }
        }
    }
}
