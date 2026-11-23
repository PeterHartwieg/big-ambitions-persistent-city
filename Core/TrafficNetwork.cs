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

    public interface ITrafficClosureBatchPlanner
    {
        void BeginTrafficClosureBatch();
        void EndTrafficClosureBatch();
    }

    public interface ITrafficEntryRoutePlanner
    {
        void SetTrafficEntryForecast(Func<int, double, double, double, double>? forecast);
    }

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

    public sealed class TrafficNetwork
    {
        public const string PolicyId = "directed-sections-v1;max80m;storage6m;headway1.8s;native-periodic-groups";
        private readonly EdgeTable edges;
        private readonly double clockRate;
        private readonly double[] linkSpeeds;
        private readonly List<TrafficLinkDefinition> links;
        private readonly int[][] linkNodes;
        private readonly Dictionary<int, Position> positions;
        private readonly Dictionary<int, double> kilometres;
        private readonly Position[] sectionPoints;
        private readonly double[] sectionDistances;
        private readonly int[] sectionStarts;
        private const double MappingMetresTolerance = 0.002;

        private readonly struct SectionGeometry
        {
            private readonly Position[] points;
            private readonly double[] distances;
            private readonly int start;
            internal readonly int Count;
            internal readonly double Length;
            internal SectionGeometry(Position[] points, double[] distances, int start, int count)
            { this.points = points; this.distances = distances; this.start = start; Count = count; Length = distances[start + count - 1]; }
            internal Position Point(int i) => points[start + i];
            internal double Distance(int i) => distances[start + i];
        }

        private int SectionCount => sectionStarts.Length - 1;

        private SectionGeometry Section(int linkId)
        {
            int start = sectionStarts[linkId];
            return new SectionGeometry(sectionPoints, sectionDistances, start, sectionStarts[linkId + 1] - start);
        }

        private sealed class EdgeTable
        {
            internal readonly Dictionary<(int, int), int> Index;
            internal int[] From, To, Link;
            internal double[] Start, End, Metres;
            internal int Count;
            internal EdgeTable(int capacity)
            {
                Index = new Dictionary<(int, int), int>(capacity);
                From = new int[capacity]; To = new int[capacity]; Link = new int[capacity];
                Start = new double[capacity]; End = new double[capacity]; Metres = new double[capacity];
            }
            internal int Add(int from, int to, int link, double start, double end, double metres)
            {
                if (Count == From.Length)
                {
                    int grown = Math.Max(4, Count * 2);
                    Array.Resize(ref From, grown); Array.Resize(ref To, grown); Array.Resize(ref Link, grown);
                    Array.Resize(ref Start, grown); Array.Resize(ref End, grown); Array.Resize(ref Metres, grown);
                }
                Index.Add((from, to), Count);
                From[Count] = from; To[Count] = to; Link[Count] = link; Start[Count] = start; End[Count] = end; Metres[Count] = metres;
                return Count++;
            }
        }
        public string TopologyId { get; }
        public string NativeTopologyId { get; }
        public IReadOnlyList<TrafficLinkDefinition> Links { get; }
        internal bool RatedCopy { get; }

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
            links = new List<TrafficLinkDefinition>();
            var nodeLists = new List<int[]>(); positions = new Dictionary<int, Position>();
            kilometres = new Dictionary<int, double>();

            var nodes = new Dictionary<int, TrafficNetworkNode>();
            foreach (var node in snapshot)
            {
                if (node == null || node.Id < 0 || node.Successors == null || node.StructuralContract == null
                    || !nodes.TryAdd(node.Id, node)) throw new ArgumentException("Duplicate/invalid traffic node.");
                positions.Add(node.Id, node.Position);
                kilometres.Add(node.Id, node.KilometresPerHour);
            }
            var outgoing = nodes.Keys.ToDictionary(i => i, i => new List<Edge>());
            var incoming = nodes.Keys.ToDictionary(i => i, i => 0);
            var rawIncoming = nodes.Keys.ToDictionary(i => i, i => 0);
            foreach (var node in nodes.Values)
                foreach (int next in node.Successors.Distinct()) if (rawIncoming.ContainsKey(next)) rawIncoming[next]++;
            var all = new List<Edge>();
            var speeds = new List<double>();
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
                return !node.Boundary && node.Signal == null && incoming[id] == 1 && rawIncoming[id] == 1 && outgoing[id].Count == 1
                    && node.Successors.Length == 1;
            }
            edges = new EdgeTable(all.Count);
            var visited = new HashSet<(int, int)>();
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
                links.Add(TrafficLinkDefinition.Frozen(id, Math.Max(1, checked((int)Math.Floor(Math.Max(metres, 6) / 6))),
                    minutes, 1.8 * clockRate, nodes[chain[chain.Count - 1].To].Signal));
                speeds.Add(Math.Min(40, nodes[chain[0].From].KilometresPerHour) / 3.6);
                var indices = new List<int> { chain[0].From };
                double offset = 0;
                foreach (var part in chain)
                {
                    int index = edges.Add(part.From, part.To, id, offset / metres, (offset + part.Metres) / metres, part.Metres);
                    if (EdgeMinutes(edges, speeds[id], clockRate, index) != part.Minutes) throw new ArgumentException("Native section speed differs along a section.");
                    indices.Add(part.To); offset += part.Metres;
                }
                nodeLists.Add(indices.ToArray());
            }
            linkNodes = nodeLists.ToArray();
            int total = 0;
            foreach (var list in linkNodes) total += list.Length;
            sectionPoints = new Position[total]; sectionDistances = new double[total]; sectionStarts = new int[linkNodes.Length + 1];
            int at = 0;
            for (int link = 0; link < linkNodes.Length; link++)
            {
                sectionStarts[link] = at;
                var list = linkNodes[link];
                double cumulative = 0;
                for (int i = 0; i < list.Length; i++, at++)
                {
                    Position point = positions[list[i]];
                    if (i > 0) cumulative += Position.Distance(sectionPoints[at - 1], point);
                    sectionPoints[at] = point; sectionDistances[at] = cumulative;
                }
            }
            sectionStarts[linkNodes.Length] = at;
            this.clockRate = clockRate;
            linkSpeeds = speeds.ToArray();
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

        public TrafficNetwork Rerated(double clockRate, Func<int, TrafficSignalSchedule?> signalAt)
        {
            if (!Position.Finite(clockRate) || clockRate <= 0) throw new ArgumentException("Invalid traffic network clock rate.");
            if (signalAt == null) throw new ArgumentNullException(nameof(signalAt));
            return new TrafficNetwork(this, clockRate, signalAt);
        }

        private TrafficNetwork(TrafficNetwork source, double clockRate, Func<int, TrafficSignalSchedule?> signalAt)
        {
            NativeTopologyId = source.NativeTopologyId; TopologyId = source.TopologyId;
            positions = source.positions; kilometres = source.kilometres; linkNodes = source.linkNodes;
            edges = source.edges; sectionPoints = source.sectionPoints; sectionDistances = source.sectionDistances; sectionStarts = source.sectionStarts;
            linkSpeeds = source.linkSpeeds; this.clockRate = clockRate; RatedCopy = true;
            links = new List<TrafficLinkDefinition>(source.links.Count);
            for (int id = 0; id < source.links.Count; id++)
            {
                var indices = linkNodes[id];
                double minutes = 0;
                for (int i = 1; i < indices.Length; i++)
                {
                    int from = indices[i - 1], to = indices[i];
                    double length = Position.Distance(positions[from], positions[to]);
                    double part = length / (Math.Min(40, kilometres[from]) / 3.6 / clockRate);
                    if (!Position.Finite(part) || part <= 0) throw new ArgumentException("Invalid native edge duration.");
                    if (EdgeMinutes(edges, linkSpeeds[id], clockRate, edges.Index[(from, to)]) != part) throw new ArgumentException("Native section speed differs along a section.");
                    minutes += part;
                }
                var link = source.links[id];
                links.Add(TrafficLinkDefinition.Frozen(id, link.Capacity, minutes, 1.8 * clockRate, signalAt(indices[indices.Length - 1])));
            }
            Links = links.AsReadOnly();
        }

        public IReadOnlyList<int> NativeNodes(int linkId) => linkId >= 0 && linkId < linkNodes.Length
            ? linkNodes[linkId] : throw new ArgumentOutOfRangeException(nameof(linkId));
        public bool TryGetEdge(int from, int to, out TrafficNetworkEdge edge)
        {
            if (!edges.Index.TryGetValue((from, to), out int index)) { edge = default; return false; }
            edge = new TrafficNetworkEdge(edges.From[index], edges.To[index], edges.Link[index], EdgeMinutes(edges, linkSpeeds[edges.Link[index]], clockRate, index), edges.Start[index], edges.End[index]);
            return true;
        }
        public Position NodePosition(int nodeId) => positions.TryGetValue(nodeId, out var at) ? at : throw new ArgumentOutOfRangeException(nameof(nodeId));

        public bool TryMap(Route route, out List<TrafficRouteLeg> mapped) => TryMapCore(route, 0, false, out mapped);

        public bool TryMapRemaining(Route route, double progress, out List<TrafficRouteLeg> mapped) =>
            TryMapCore(route, progress, true, out mapped);

        internal struct MappedSpan
        {
            public int LinkId;
            public double StartNominalMinutes, EndNominalMinutes, FreeFlowMinutes, StartLinkFraction, EndLinkFraction;
        }

        [ThreadStatic] private static List<MappedSpan>? mappingScratch;

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
                        ? Math.Abs(previousEndFraction - startFraction) * Section(edge.LinkId).Length > MappingMetresTolerance
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
                !TryTag(leg, out int from, out int to, out bool fragment) || !TryGetEdge(from, to, out edge)) return false;
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

        [ThreadStatic] private static double[]? validationOffsets;

        public void ValidateMapping(Route journey, IReadOnlyList<TrafficRouteLeg> traffic) => ValidateMapping(journey, traffic, out _);

        private static Position PositionAt(Route journey, double[] offsets, double minute, ref int hint)
        {
            var legs = journey.Legs;
            for (; hint < legs.Count; hint++)
            {
                RouteLeg leg = legs[hint];
                double elapsed = minute - offsets[hint];
                if (elapsed <= leg.Minutes) return Position.Lerp(leg.From, leg.To, Math.Max(0, elapsed / leg.Minutes));
            }
            return legs[legs.Count - 1].To;
        }

        private void ValidateMapping(Route journey, IReadOnlyList<TrafficRouteLeg> traffic, out double[] offsets)
        {
            if (journey == null || journey.Legs == null || journey.Legs.Count == 0 || traffic == null || traffic.Count == 0)
                throw new ArgumentException("A committed journey and traffic mapping are required.");
            offsets = validationOffsets ??= new double[TransportComposition.MaximumLegs + 1];
            if (offsets.Length < journey.Legs.Count + 1) offsets = validationOffsets = new double[Math.Max(journey.Legs.Count + 1, offsets.Length * 2)];
            offsets[0] = 0;
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
            int firstLeg = 0, positionLeg = 0;
            for (int spanIndex = 0; spanIndex < traffic.Count; spanIndex++)
            {
                TrafficRouteLeg span = traffic[spanIndex];
                if (span == null || span.LinkId < 0 || span.LinkId >= SectionCount ||
                    !Position.Finite(span.StartNominalMinutes) || !Position.Finite(span.EndNominalMinutes) ||
                    span.StartNominalMinutes < previousEnd || span.EndNominalMinutes <= span.StartNominalMinutes ||
                    span.EndNominalMinutes > offsets[journey.Legs.Count] + 0.0000001 ||
                    !Position.Finite(span.StartLinkFraction) || !Position.Finite(span.EndLinkFraction) ||
                    span.StartLinkFraction < 0 || span.EndLinkFraction > 1 || span.StartLinkFraction >= span.EndLinkFraction)
                    throw new ArgumentException("Invalid traffic mapping span.", nameof(traffic));
                SectionGeometry section = Section(span.LinkId);
                double cursor = span.StartLinkFraction * section.Length;
                double endDistance = span.EndLinkFraction * section.Length;
                if (!Near(PositionAt(journey, offsets, span.StartNominalMinutes, ref positionLeg), SectionPoint(section, cursor)) ||
                    !Near(PositionAt(journey, offsets, span.EndNominalMinutes, ref positionLeg), SectionPoint(section, endDistance)))
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

        public void ValidateRemainingMapping(Route journey, IReadOnlyList<TrafficRouteLeg> traffic, double progress)
        {
            ValidateMapping(journey, traffic, out var offsets);
            if (!Position.Finite(progress) || progress < 0 || progress > journey.Minutes)
                throw new ArgumentException("Invalid traffic journey progress.");
            int firstLeg = 0;
            for (int spanIndex = 0; spanIndex < traffic.Count; spanIndex++)
            {
                TrafficRouteLeg span = traffic[spanIndex];
                if (span.EndNominalMinutes <= progress) continue;
                while (firstLeg < journey.Legs.Count && offsets[firstLeg + 1] <= span.StartNominalMinutes) firstLeg++;
                ValidateRemainingTiming(journey, offsets, firstLeg, span, progress);
            }
            double offset = 0;
            int claim = 0;
            foreach (RouteLeg leg in journey.Legs)
            {
                double end = offset + leg.Minutes;
                if (end > progress && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal))
                {
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

        internal static void ValidateCommittedSpanTiming(TrafficRouteLeg span)
        {
            double committed = span.EndNominalMinutes - span.StartNominalMinutes;
            double clockNoise = 8 * 2.2204460492503131e-16
                * Math.Max(Math.Abs(span.StartNominalMinutes), Math.Abs(span.EndNominalMinutes));
            if (!Position.Finite(span.FreeFlowMinutes) || span.FreeFlowMinutes <= 0
                || Math.Abs(span.FreeFlowMinutes - committed) > clockNoise + 1e-8 * committed)
                throw new ArgumentException("Traffic travel duration disagrees with its committed journey timing.");
        }

        private void ValidateRemainingTiming(Route journey, double[] offsets, int firstLeg, TrafficRouteLeg span, double progress)
        {
            const double maximumSlowdown = 1.75;
            const double relativeTolerance = 1e-8;
            const double doubleEpsilon = 2.2204460492503131e-16;
            SectionGeometry section = Section(span.LinkId);
            double metres = (span.EndLinkFraction - span.StartLinkFraction) * section.Length;
            double clockNoise = 8 * doubleEpsilon * Math.Max(Math.Abs(span.StartNominalMinutes), Math.Abs(span.EndNominalMinutes));
            ValidateCommittedSpanTiming(span);

            var legs = journey.Legs;
            RouteLeg? reference = null;
            double referenceMetres = 0;
            int lastLeg = firstLeg;
            for (int i = firstLeg; i < legs.Count && offsets[i] < span.EndNominalMinutes; i++)
            {
                lastLeg = i + 1;
                double end = offsets[i + 1];
                if (end <= progress || end <= span.StartNominalMinutes) continue;
                RouteLeg leg = legs[i];
                double distance = Position.Distance(leg.From, leg.To);
                if (!Position.Finite(distance) || distance <= 0)
                    throw new ArgumentException("Traffic timing requires moving committed geometry.");
                if (distance > referenceMetres) { reference = leg; referenceMetres = distance; }
            }
            if (reference == null) throw new ArgumentException("Traffic timing has no unfinished committed geometry.");
            double canonicalDensity = links[span.LinkId].FreeFlowMinutes / section.Length;
            double density = reference.Minutes / referenceMetres;
            for (int i = firstLeg; i < lastLeg; i++)
            {
                double end = offsets[i + 1];
                if (end <= progress || end <= span.StartNominalMinutes) continue;
                RouteLeg leg = legs[i];
                double legMetres = Position.Distance(leg.From, leg.To);
                double coordinate = Math.Max(1, Math.Max(Math.Max(Math.Abs(leg.From.X), Math.Abs(leg.To.X)),
                    Math.Max(Math.Max(Math.Abs(leg.From.Y), Math.Abs(leg.To.Y)),
                        Math.Max(Math.Abs(leg.From.Z), Math.Abs(leg.To.Z)))));
                double spatialNoise = 8 * doubleEpsilon * coordinate;
                if (spatialNoise > MappingMetresTolerance)
                    throw new ArgumentException("Traffic timing cannot be verified at this coordinate precision.");
                double canonical = canonicalDensity * legMetres;
                double tolerance = relativeTolerance * Math.Max(canonical, leg.Minutes)
                    + Math.Max(canonicalDensity, density) * spatialNoise;
                if (!Position.Finite(density) || density <= 0 || !Position.Finite(canonical)
                    || leg.Minutes < canonical - tolerance || leg.Minutes > maximumSlowdown * canonical + tolerance
                    || Math.Abs(leg.Minutes - density * legMetres) > tolerance)
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
            for (int i = 1; i < section.Count; i++)
                if (distance <= section.Distance(i))
                    return Position.Lerp(section.Point(i - 1), section.Point(i),
                        Math.Max(0, (distance - section.Distance(i - 1)) / (section.Distance(i) - section.Distance(i - 1))));
            return section.Point(section.Count - 1);
        }

        private static bool AdvanceSection(SectionGeometry section, Position from, Position to, double limit, ref double cursor)
        {
            if (!Near(from, SectionPoint(section, cursor))) return false;
            double previousAlong = 0;
            for (int i = 1; i < section.Count && cursor < limit; i++)
            {
                if (section.Distance(i) <= cursor) continue;
                double segmentEnd = Math.Min(limit, section.Distance(i));
                Position begin = SectionPoint(section, cursor);
                Position end = SectionPoint(section, segmentEnd);
                if (TryProjectSegment(to, begin, end, out double endpointAlong))
                {
                    double next = cursor + endpointAlong * (segmentEnd - cursor);
                    if (next < cursor || (next == cursor && !Near(from, to))) return false;
                    cursor = next;
                    return true;
                }
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
        private static double EdgeMinutes(EdgeTable edges, double speed, double clockRate, int index) => edges.Metres[index] / (speed / clockRate);
        private static void WriteSignal(BinaryWriter writer, TrafficSignalSchedule? s, double clockRate)
        {
            writer.Write(s != null);
            if (s != null) { writer.Write(Math.Round(s.CycleMinutes / clockRate, 12)); writer.Write(Math.Round(s.GreenStartMinutes / clockRate, 12)); writer.Write(Math.Round(s.GreenDurationMinutes / clockRate, 12)); writer.Write(Math.Round(s.PhaseOffsetMinutes / clockRate, 12)); }
        }
    }
}
