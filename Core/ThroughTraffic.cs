using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public sealed class ThroughTrafficSettings
    {
        public const double DefaultShare = 0.10, MaximumShare = 0.5;
        public ThroughTrafficSettings(double share = DefaultShare)
        {
            if (!Position.Finite(share) || share < 0 || share > MaximumShare) throw new ArgumentOutOfRangeException(nameof(share), "Through-traffic share must be 0 to 0.5.");
            Share = share;
        }
        public double Share { get; }
        public double EntriesPerResidentCar => Share / (1 - Share);
    }

    public sealed class ThroughTrafficRoutes
    {
        public const double EdgeBand = 0.1, MinimumEdgeMetres = 30;
        private readonly TrafficNetwork network;
        private readonly List<int>[] successors;
        private readonly Dictionary<int, (int[] Previous, int[] Reachable)> routes = new Dictionary<int, (int[] Previous, int[] Reachable)>();
        public IReadOnlyList<int> Entries { get; }
        public IReadOnlyList<int> Exits { get; }

        public ThroughTrafficRoutes(TrafficNetwork network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            int count = network.Links.Count;
            successors = new List<int>[count];
            var startsAt = new Dictionary<int, List<int>>();
            var endsAt = new HashSet<int>();
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
            for (int id = 0; id < count; id++)
            {
                var nodes = network.NativeNodes(id);
                if (!startsAt.TryGetValue(nodes[0], out var list)) startsAt.Add(nodes[0], list = new List<int>());
                list.Add(id);
                endsAt.Add(nodes[nodes.Count - 1]);
                foreach (int node in nodes)
                {
                    var at = network.NodePosition(node);
                    minX = Math.Min(minX, at.X); maxX = Math.Max(maxX, at.X); minZ = Math.Min(minZ, at.Z); maxZ = Math.Max(maxZ, at.Z);
                }
            }
            double band = Math.Max(MinimumEdgeMetres, EdgeBand * Math.Max(maxX - minX, maxZ - minZ));
            bool OnEdge(int node)
            {
                var at = network.NodePosition(node);
                return at.X - minX <= band || maxX - at.X <= band || at.Z - minZ <= band || maxZ - at.Z <= band;
            }
            var entries = new List<int>(); var exits = new List<int>();
            for (int id = 0; id < count; id++)
            {
                var nodes = network.NativeNodes(id);
                int first = nodes[0], last = nodes[nodes.Count - 1];
                successors[id] = startsAt.TryGetValue(last, out var next) ? next : new List<int>();
                if (!endsAt.Contains(first) && OnEdge(first)) entries.Add(id);
                if (!startsAt.ContainsKey(last) && OnEdge(last)) exits.Add(id);
            }
            Entries = entries; Exits = exits;
        }

        public bool IsEntry(int linkId) => ((List<int>)Entries).BinarySearch(linkId) >= 0;
        public bool IsExit(int linkId) => ((List<int>)Exits).BinarySearch(linkId) >= 0;

        public List<TrafficRouteLeg>? Route(int entry, uint draw)
        {
            if (!routes.TryGetValue(entry, out var tree)) routes.Add(entry, tree = Paths(entry));
            if (tree.Reachable.Length == 0) return null;
            var chain = new List<int>();
            for (int link = tree.Reachable[(int)(draw % (uint)tree.Reachable.Length)]; link != -1; link = tree.Previous[link]) chain.Add(link);
            chain.Reverse();
            var legs = new List<TrafficRouteLeg>(chain.Count);
            double nominal = 0;
            foreach (int link in chain)
            {
                double minutes = network.Links[link].FreeFlowMinutes;
                legs.Add(new TrafficRouteLeg { LinkId = link, StartNominalMinutes = nominal, EndNominalMinutes = nominal + minutes,
                    FreeFlowMinutes = minutes, StartLinkFraction = 0, EndLinkFraction = 1 });
                nominal += minutes;
            }
            return legs;
        }

        private (int[] Previous, int[] Reachable) Paths(int entry)
        {
            int count = network.Links.Count;
            var cost = new double[count]; var previous = new int[count];
            for (int i = 0; i < count; i++) { cost[i] = double.PositiveInfinity; previous[i] = -1; }
            cost[entry] = network.Links[entry].FreeFlowMinutes;
            var open = new List<(double Cost, int Link)>();
            Push(open, (cost[entry], entry));
            while (open.Count > 0)
            {
                var (at, link) = Pop(open);
                if (at > cost[link]) continue;
                foreach (int next in successors[link])
                {
                    double through = at + network.Links[next].FreeFlowMinutes;
                    if (through >= cost[next]) continue;
                    cost[next] = through; previous[next] = link; Push(open, (through, next));
                }
            }
            var reachable = new List<int>();
            foreach (int exit in Exits)
                if (exit != entry && !double.IsPositiveInfinity(cost[exit])) reachable.Add(exit);
            return (previous, reachable.ToArray());
        }

        private static bool Before((double Cost, int Link) a, (double Cost, int Link) b) => a.Cost < b.Cost || a.Cost == b.Cost && a.Link < b.Link;

        private static void Push(List<(double Cost, int Link)> heap, (double Cost, int Link) item)
        {
            heap.Add(item);
            for (int i = heap.Count - 1; i > 0;)
            {
                int parent = (i - 1) / 2;
                if (!Before(heap[i], heap[parent])) break;
                (heap[i], heap[parent]) = (heap[parent], heap[i]); i = parent;
            }
        }

        private static (double Cost, int Link) Pop(List<(double Cost, int Link)> heap)
        {
            var top = heap[0];
            heap[0] = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            for (int i = 0; ;)
            {
                int left = 2 * i + 1, right = left + 1, least = i;
                if (left < heap.Count && Before(heap[left], heap[least])) least = left;
                if (right < heap.Count && Before(heap[right], heap[least])) least = right;
                if (least == i) break;
                (heap[i], heap[least]) = (heap[least], heap[i]); i = least;
            }
            return top;
        }
    }

    public static class ThroughTraffic
    {
        public const string Prefix = "through:";
        public const string InspectorLine = "Passing through the city";
        public const string InspectorKey = "persistentcity_through_inspector";
        public static bool IsThroughVehicle(string? vehicleId) => vehicleId != null && vehicleId.StartsWith(Prefix, StringComparison.Ordinal);
    }

    internal sealed class ThroughTrafficState
    {
        internal readonly ThroughTrafficSettings Settings;
        internal ThroughTrafficRoutes? Routes;
        internal TrafficNetwork? Network;
        internal readonly List<(double At, string Id, int Entry, uint Exit)> Pending = new List<(double, string, int, uint)>();
        internal int Cursor;
        internal readonly HashSet<string> InFlight = new HashSet<string>(StringComparer.Ordinal);
        internal long ScheduledHour = long.MinValue;
        internal double CountingSince = double.PositiveInfinity;
        internal long CountHour = long.MinValue;
        internal int CountEntries, LastHourEntries;
        internal void Roll(long hour)
        {
            if (hour == CountHour) return;
            LastHourEntries = hour == CountHour + 1 ? CountEntries : 0;
            CountHour = hour; CountEntries = 0;
        }
        internal long Generated, Completed, SkippedClosed, CancelledClosed;
        internal ThroughTrafficState(ThroughTrafficSettings settings) { Settings = settings; }
        internal ThroughTrafficState Fresh() => new ThroughTrafficState(Settings);
    }

    public sealed partial class CitySimulator
    {
        private ThroughTrafficState? throughTraffic;

        public void AttachThroughTraffic(ThroughTrafficSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            DetachThroughTraffic();
            throughTraffic = settings.Share > 0 ? new ThroughTrafficState(settings) : null;
        }

        public void DetachThroughTraffic()
        {
            var state = throughTraffic;
            throughTraffic = null;
            if (state != null) DropThroughCars(state);
        }

        public double ThroughTrafficShare => throughTraffic?.Settings.Share ?? 0;
        public int ThroughCarCount => throughTraffic?.InFlight.Count ?? 0;
        public long ThroughCarsGenerated => throughTraffic?.Generated ?? 0;
        public long ThroughCarsCompleted => throughTraffic?.Completed ?? 0;
        public long ThroughCarsSkippedClosed => throughTraffic?.SkippedClosed ?? 0;
        public long ThroughCarsCancelledClosed => throughTraffic?.CancelledClosed ?? 0;

        public static string? DescribeVehicle(string vehicleId) => ThroughTraffic.IsThroughVehicle(vehicleId) ? ThroughTraffic.InspectorLine : null;

        private double ThroughMinute()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || trafficNetwork == null) return double.PositiveInfinity;
            if (!ReferenceEquals(state.Network, trafficNetwork)) BindThroughNetwork(state);
            return ThroughPeek();
        }

        private double ThroughPeek()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || trafficNetwork == null) return double.PositiveInfinity;
            if (!ReferenceEquals(state.Network, trafficNetwork)) return State.Minute;
            if (state.Routes!.Entries.Count == 0 || state.Routes.Exits.Count == 0) return double.PositiveInfinity;
            double boundary = state.ScheduledHour == long.MinValue ? State.Minute : (state.ScheduledHour + 1) * 60.0;
            return state.Cursor < state.Pending.Count ? Math.Min(boundary, state.Pending[state.Cursor].At) : boundary;
        }

        private void DropThroughCars(ThroughTrafficState state)
        {
            if (traffic != null) foreach (var id in state.InFlight) traffic.Cancel(id);
            state.InFlight.Clear(); state.Pending.Clear(); state.Cursor = 0; state.Network = null; state.Routes = null;
        }

        private void BindThroughNetwork(ThroughTrafficState state)
        {
            DropThroughCars(state);
            state.Network = trafficNetwork; state.Routes = new ThroughTrafficRoutes(trafficNetwork!);
            state.ScheduledHour = long.MinValue; state.CountingSince = double.PositiveInfinity; state.CountHour = long.MinValue; state.CountEntries = state.LastHourEntries = 0;
        }

        private void RunThroughTraffic()
        {
            var state = throughTraffic!;
            long hour = (long)Math.Floor(State.Minute / 60);
            if (state.ScheduledHour == long.MinValue || hour > state.ScheduledHour) ScheduleThroughHour(state, hour);
            while (state.Cursor < state.Pending.Count && state.Pending[state.Cursor].At <= State.Minute)
            {
                var entry = state.Pending[state.Cursor++];
                var route = state.Routes!.Route(entry.Entry, entry.Exit);
                if (route == null) continue;
                bool closed = false;
                if (routes is ITrafficRoutePlanner planner)
                    foreach (var leg in route)
                    {
                        bool shut = planner.IsTrafficLinkClosed(leg.LinkId);
                        traffic!.SetLinkClosed(leg.LinkId, shut);
                        closed |= shut;
                    }
                if (closed) { state.SkippedClosed++; continue; }
                traffic!.Enqueue(entry.Id, route);
                state.InFlight.Add(entry.Id); state.Generated++;
            }
            if (state.Cursor == state.Pending.Count) { state.Pending.Clear(); state.Cursor = 0; }
        }

        private void ScheduleThroughHour(ThroughTrafficState state, long hour)
        {
            if (double.IsPositiveInfinity(state.CountingSince)) state.CountingSince = State.Minute;
            state.Roll(hour);
            int residents = state.CountingSince <= (hour - 1) * 60.0 ? state.LastHourEntries : traffic!.VehicleCount - state.InFlight.Count;
            double expected = residents * state.Settings.EntriesPerResidentCar;
            uint key = StableRandom.Append(StableRandom.AppendDecimal(StableRandom.Append(StableRandom.Begin(State.Seed), ThroughTraffic.Prefix), (int)(hour % 1000000000)), ':');
            int count = (int)Math.Floor(expected);
            if ((StableRandom.Finish(StableRandom.Append(key, "count")) + 0.5) / (uint.MaxValue + 1.0) < expected - count) count++;
            var entries = state.Routes!.Entries;
            for (int i = 0; i < count; i++)
            {
                uint draw = StableRandom.Finish(StableRandom.AppendDecimal(key, i));
                double at = hour * 60.0 + (draw % 60000) / 1000.0;
                if (at < State.Minute) continue;
                uint exit = StableRandom.Finish(StableRandom.Append(StableRandom.AppendDecimal(key, i), ":exit"));
                string id = ThroughTraffic.Prefix + hour.ToString(CultureInfo.InvariantCulture) + ":" + i.ToString(CultureInfo.InvariantCulture);
                state.Pending.Add((at, id, entries[(int)((draw >> 16) % (uint)entries.Count)], exit));
            }
            state.Pending.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : string.CompareOrdinal(a.Id, b.Id));
            state.ScheduledHour = hour;
        }

        private void CountResidentCar()
        {
            var state = throughTraffic;
            if (state == null || State.Minute < state.CountingSince) return;
            state.Roll((long)Math.Floor(State.Minute / 60));
            state.CountEntries++;
        }

        private void CancelBlockedThroughCars()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || state.InFlight.Count == 0) return;
            foreach (var id in state.InFlight) if (traffic.IsBlockedByClosure(id)) throughBlocked.Add(id);
            if (throughBlocked.Count == 0) return;
            foreach (var id in throughBlocked) { traffic.Cancel(id); state.InFlight.Remove(id); state.CancelledClosed++; }
            throughBlocked.Clear();
        }

        private readonly List<string> throughBlocked = new List<string>();

        private bool ThroughCompleted(string id)
        {
            var state = throughTraffic;
            if (!ThroughTraffic.IsThroughVehicle(id)) return false;
            traffic!.RemoveCompleted(id);
            if (state != null && state.InFlight.Remove(id)) state.Completed++;
            return true;
        }
    }
}
