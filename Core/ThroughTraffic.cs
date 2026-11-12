using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>GR-8 (G5) setting: the share of all cars on the road that only pass through. J0 default 0.10 (Q3), range 0
    /// to 0.5. At 0 nothing is generated and the kernel is exactly as without through traffic.</summary>
    public sealed class ThroughTrafficSettings
    {
        public const double DefaultShare = 0.10, MaximumShare = 0.5;
        public ThroughTrafficSettings(double share = DefaultShare)
        {
            if (!Position.Finite(share) || share < 0 || share > MaximumShare) throw new ArgumentOutOfRangeException(nameof(share), "Through-traffic share must be 0 to 0.5.");
            Share = share;
        }
        public double Share { get; }
        /// <summary>Through entries per resident car entry: share ÷ (1 − share), so the share holds as resident traffic rises
        /// and falls.</summary>
        public double EntriesPerResidentCar => Share / (1 - Share);
    }

    /// <summary>
    /// GR-8: the map edges of one road network and the edge-to-edge routes between them. An entry is a section whose first
    /// native node no section leads into, an exit a section whose last node leads nowhere; both must lie on the network's
    /// outer band (<see cref="EdgeBand"/> of the bounding box, at least <see cref="MinimumEdgeMetres"/>), so a cul-de-sac in
    /// the middle of the city is neither. A route is the shortest free-flow path, whole sections only: it never ends at a
    /// curb, so a through car never parks.
    /// </summary>
    public sealed class ThroughTrafficRoutes
    {
        public const double EdgeBand = 0.1, MinimumEdgeMetres = 30;
        private readonly TrafficNetwork network;
        private readonly List<int>[] successors;
        // Per entry: the shortest-path tree (each section's predecessor) and the exits it reaches, in exit order.
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

        /// <summary>The route from entry <paramref name="entry"/> to the exit chosen by <paramref name="draw"/> among the exits it
        /// reaches, or null when it reaches none. The path tree is computed once per entry; each route is a new list.</summary>
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
            // A binary heap with stale entries skipped on pop (ties by link ID, as the ordered set it replaced).
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

    /// <summary>
    /// GR-8 (G5): anonymous cars that cross the city edge to edge in the traffic kernel. They queue and fill road sections
    /// like resident cars, but they carry nobody, have no person ID, never park and are never saved: the save captures the
    /// kernel without them (<see cref="TrafficKernel.Capture(Func{string, bool})"/>), and a loaded city generates them again
    /// from the setting. Each hour's entries are the previous hour's resident car entries × share ÷ (1 − share), at seeded
    /// minutes and edges; the hour after an attach or reload uses the resident cars on the road instead.
    /// </summary>
    public static class ThroughTraffic
    {
        public const string Prefix = "through:";
        /// <summary>The inspector's line for a through car.</summary>
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
        /// <summary>Resident car entries counted since this minute (the first schedule), in CountHour and the hour before it.</summary>
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
        /// <summary>The same setting with nothing generated yet (a replacement simulator restores the saved kernel).</summary>
        internal ThroughTrafficState Fresh() => new ThroughTrafficState(Settings);
    }

    public sealed partial class CitySimulator
    {
        private ThroughTrafficState? throughTraffic;

        /// <summary>GR-8: generates through cars at the setting's share from the next event on. Share 0 generates nothing.</summary>
        public void AttachThroughTraffic(ThroughTrafficSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            DetachThroughTraffic();
            throughTraffic = settings.Share > 0 ? new ThroughTrafficState(settings) : null;
        }

        /// <summary>Removes every through car from the kernel and stops generating them.</summary>
        public void DetachThroughTraffic()
        {
            var state = throughTraffic;
            throughTraffic = null;
            if (state != null) DropThroughCars(state);
        }

        public double ThroughTrafficShare => throughTraffic?.Settings.Share ?? 0;
        /// <summary>Through cars in the kernel now; generated and completed since attach (diagnostics).</summary>
        public int ThroughCarCount => throughTraffic?.InFlight.Count ?? 0;
        public long ThroughCarsGenerated => throughTraffic?.Generated ?? 0;
        public long ThroughCarsCompleted => throughTraffic?.Completed ?? 0;
        /// <summary>Through cars not started because their route was closed, and cars taken off a closed road.</summary>
        public long ThroughCarsSkippedClosed => throughTraffic?.SkippedClosed ?? 0;
        public long ThroughCarsCancelledClosed => throughTraffic?.CancelledClosed ?? 0;

        /// <summary>The inspector's words for a vehicle that belongs to no person, or null for anything else.</summary>
        public static string? DescribeVehicle(string vehicleId) => ThroughTraffic.IsThroughVehicle(vehicleId) ? ThroughTraffic.InspectorLine : null;

        /// <summary>The next through-traffic work: a pending entry or the next hour boundary, which schedules that hour.</summary>
        private double ThroughMinute()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || trafficNetwork == null) return double.PositiveInfinity;
            if (!ReferenceEquals(state.Network, trafficNetwork)) BindThroughNetwork(state);
            return ThroughPeek();
        }

        /// <summary><see cref="ThroughMinute"/> without binding, for the caught-up checks: an unbound network is work now.</summary>
        private double ThroughPeek()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || trafficNetwork == null) return double.PositiveInfinity;
            if (!ReferenceEquals(state.Network, trafficNetwork)) return State.Minute;
            if (state.Routes!.Entries.Count == 0 || state.Routes.Exits.Count == 0) return double.PositiveInfinity;
            double boundary = state.ScheduledHour == long.MinValue ? State.Minute : (state.ScheduledHour + 1) * 60.0;
            return state.Cursor < state.Pending.Count ? Math.Min(boundary, state.Pending[state.Cursor].At) : boundary;
        }

        /// <summary>Removes the through cars of the bound network and forgets its routes; the next query binds again.</summary>
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

        /// <summary>Runs the through-traffic work due at the clock: a new hour's schedule, then every entry due.</summary>
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
                // A route through a closed section is not driven (review Opus-r1-2): the car would wait at its head and hold
                // every resident behind it.
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
            // The previous hour's resident entries when they were all counted, else the resident cars on the road now.
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
                if (at < State.Minute) continue; // the part of the hour before an attach or reload is not replayed
                uint exit = StableRandom.Finish(StableRandom.Append(StableRandom.AppendDecimal(key, i), ":exit"));
                string id = ThroughTraffic.Prefix + hour.ToString(CultureInfo.InvariantCulture) + ":" + i.ToString(CultureInfo.InvariantCulture);
                state.Pending.Add((at, id, entries[(int)((draw >> 16) % (uint)entries.Count)], exit));
            }
            state.Pending.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : string.CompareOrdinal(a.Id, b.Id));
            state.ScheduledHour = hour;
        }

        /// <summary>Counts a resident car entering the kernel for the next hour's through entries.</summary>
        private void CountResidentCar()
        {
            var state = throughTraffic;
            if (state == null || State.Minute < state.CountingSince) return;
            state.Roll((long)Math.Floor(State.Minute / 60));
            state.CountEntries++;
        }

        /// <summary>After a closure refresh: a through car held by a closed section leaves the road instead of waiting there
        /// (review Opus-r1-2). Residents plan around closures; through cars have no plan to fall back on.</summary>
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
