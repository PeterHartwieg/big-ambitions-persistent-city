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
        private readonly Dictionary<int, List<TrafficRouteLeg>?[]> routes = new Dictionary<int, List<TrafficRouteLeg>?[]>();
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
        /// reaches, or null when it reaches none. Paths are computed once per entry.</summary>
        public List<TrafficRouteLeg>? Route(int entry, uint draw)
        {
            if (!routes.TryGetValue(entry, out var toExit)) routes.Add(entry, toExit = Paths(entry));
            int reachable = 0;
            foreach (var path in toExit) if (path != null) reachable++;
            if (reachable == 0) return null;
            int pick = (int)(draw % (uint)reachable);
            foreach (var path in toExit)
                if (path != null && pick-- == 0) return Copy(path);
            return null;
        }

        private List<TrafficRouteLeg>?[] Paths(int entry)
        {
            int count = network.Links.Count;
            var cost = new double[count]; var previous = new int[count];
            for (int i = 0; i < count; i++) { cost[i] = double.PositiveInfinity; previous[i] = -1; }
            cost[entry] = network.Links[entry].FreeFlowMinutes;
            var open = new SortedSet<(double Cost, int Link)> { (cost[entry], entry) };
            while (open.Count > 0)
            {
                var (at, link) = open.Min; open.Remove(open.Min);
                if (at > cost[link]) continue;
                foreach (int next in successors[link])
                {
                    double through = at + network.Links[next].FreeFlowMinutes;
                    if (through >= cost[next]) continue;
                    if (!double.IsPositiveInfinity(cost[next])) open.Remove((cost[next], next));
                    cost[next] = through; previous[next] = link; open.Add((through, next));
                }
            }
            var result = new List<TrafficRouteLeg>?[Exits.Count];
            for (int e = 0; e < Exits.Count; e++)
            {
                int exit = Exits[e];
                if (exit == entry || double.IsPositiveInfinity(cost[exit])) continue;
                var chain = new List<int>();
                for (int link = exit; link != -1; link = previous[link]) chain.Add(link);
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
                result[e] = legs;
            }
            return result;
        }

        private static List<TrafficRouteLeg> Copy(List<TrafficRouteLeg> path)
        {
            var copy = new List<TrafficRouteLeg>(path.Count);
            foreach (var leg in path)
                copy.Add(new TrafficRouteLeg { LinkId = leg.LinkId, StartNominalMinutes = leg.StartNominalMinutes, EndNominalMinutes = leg.EndNominalMinutes,
                    FreeFlowMinutes = leg.FreeFlowMinutes, StartLinkFraction = leg.StartLinkFraction, EndLinkFraction = leg.EndLinkFraction });
            return copy;
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
        internal long Generated, Completed;
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

        /// <summary>The inspector's words for a vehicle that belongs to no person, or null for anything else.</summary>
        public static string? DescribeVehicle(string vehicleId) => ThroughTraffic.IsThroughVehicle(vehicleId) ? ThroughTraffic.InspectorLine : null;

        /// <summary>The next through-traffic work: a pending entry or the next hour boundary, which schedules that hour.</summary>
        private double ThroughMinute()
        {
            var state = throughTraffic;
            if (state == null || traffic == null || trafficNetwork == null) return double.PositiveInfinity;
            if (!ReferenceEquals(state.Network, trafficNetwork)) BindThroughNetwork(state);
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
                if (routes is ITrafficRoutePlanner planner)
                    foreach (var leg in route) traffic!.SetLinkClosed(leg.LinkId, planner.IsTrafficLinkClosed(leg.LinkId));
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
