using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5CarSearchAllocationTests
{
    private readonly ITestOutputHelper output;
    public Gr5CarSearchAllocationTests(ITestOutputHelper output) { this.output = output; }

    private const int Side = 60, ExpansionStep = 16;
    private static readonly string Topology = new string('b', 64);

    private sealed class BusyStreets
    {
        public TransportRoadGraph Graph;
        public readonly TransportRoadNode[] Nodes = new TransportRoadNode[Side * Side];
        private readonly Dictionary<(int, int), int> edges = new Dictionary<(int, int), int>();
        private readonly Dictionary<long, string> tags = new Dictionary<long, string>();
        private readonly TrafficKernel kernel;
        private readonly Dictionary<int, TrafficLinkForecast> captured = new Dictionary<int, TrafficLinkForecast>();
        private readonly Dictionary<int, double> entries = new Dictionary<int, double>();
        private readonly Func<int, int, double, double> traversal;
        private readonly Func<int, int, double> extra;
        private readonly Func<int, bool> disabled = _ => false;
        private readonly Func<Position, Position, bool> clear = (_, _) => true;
        private readonly Action<int[]> found;
        public readonly Func<int, int, string> Tag;
        private int from;
        public double Pace = 1.1;
        public string TopologyId = Topology;
        public int[]? Path;

        public BusyStreets(int vehicles)
        {
            var definitions = new List<TrafficLinkDefinition>();
            for (int x = 0; x < Side; x++)
                for (int z = 0; z < Side; z++)
                {
                    var next = new List<int>(4);
                    if (x > 0) next.Add((x - 1) * Side + z); if (x + 1 < Side) next.Add((x + 1) * Side + z);
                    if (z > 0) next.Add(x * Side + z - 1); if (z + 1 < Side) next.Add(x * Side + z + 1);
                    Nodes[x * Side + z] = new TransportRoadNode { Position = new Position(x * 30, 0, z * 30), Successors = next.ToArray(), KilometresPerHour = 30, AllowsCar = true };
                    foreach (int n in next)
                    {
                        edges[(x * Side + z, n)] = definitions.Count;
                        definitions.Add(new TrafficLinkDefinition { Id = definitions.Count, Capacity = 4, FreeFlowMinutes = 0.05, HeadwayMinutes = 0.02 });
                    }
                }
            kernel = new TrafficKernel(definitions, 0);
            var random = new Random(3);
            for (int v = 0; v < vehicles; v++)
                kernel.Enqueue("v" + v, new List<TrafficRouteLeg> { new TrafficRouteLeg { LinkId = random.Next(definitions.Count), EndNominalMinutes = 0.05, FreeFlowMinutes = 0.05 } });
            kernel.AdvanceTo(0.01, 1000);
            Graph = new TransportRoadGraph(Nodes);
            Graph.ReserveRoadLegCache();
            traversal = (a, b, baseline) => baseline * Pace;
            extra = Extra;
            found = p => Path = p;
            Tag = EdgeTag;
        }

        private double Extra(int a, int b)
        {
            int id = edges[(a, b)];
            double entry = 0;
            if (a == from)
            {
                if (!entries.TryGetValue(id, out entry)) { entry = kernel.ForecastEntry(id, 0.02, 0.5, 0.5); entries.Add(id, entry); }
                if (!Position.Finite(entry)) return 1e15;
            }
            if (!captured.TryGetValue(id, out var forecast)) { forecast = kernel.Forecast(id, 0.02); captured.Add(id, forecast); }
            if (forecast.Closed || !Position.Finite(forecast.DelayMinutes)) return 1e15;
            return entry + forecast.DelayMinutes;
        }

        private string EdgeTag(int a, int b)
        {
            long key = (long)a << 32 | (uint)b;
            if (!ReferenceEquals(tagTopology, TopologyId)) { tags.Clear(); tagTopology = TopologyId; }
            if (tags.TryGetValue(key, out var tag)) return tag;
            tag = string.Concat("native:road:v1:", TopologyId, ":", a.ToString(CultureInfo.InvariantCulture) + ":" + b.ToString(CultureInfo.InvariantCulture));
            tags.Add(key, tag);
            return tag;
        }
        private string tagTopology = Topology;

        public void Retime(double rate)
        {
            Graph = new TransportRoadGraph(Nodes, rate, Graph);
            Graph.ReserveRoadLegCache();
        }

        public TransportRoadGraph.RoadSearch Begin(int start, int goal, bool shared)
        {
            from = start; captured.Clear(); entries.Clear(); Path = null;
            return Graph.BeginSearch(start, goal, disabled, clear, traversal, pathFound: found, extraMinutes: extra, edgeTag: shared ? Tag : null);
        }

        public Route? Run(int start, int goal, bool shared)
        {
            using var search = Begin(start, goal, shared);
            while (!search.Advance(ExpansionStep)) { }
            var road = search.Result;
            if (road == null || shared) return road;
            for (int i = 0; i < road.Legs.Count; i++) road.Legs[i].ServiceId = Tag(Path![i], Path[i + 1]);
            return road;
        }
    }

    private readonly struct Pair { public readonly int Start, Goal; public Pair(int start, int goal) { Start = start; Goal = goal; } }

    private static List<Pair> Trips(int count, int seed, int hubs)
    {
        var random = new Random(seed);
        var hubNodes = new int[hubs];
        for (int i = 0; i < hubs; i++) hubNodes[i] = random.Next(Side * Side);
        var trips = new List<Pair>(count);
        while (trips.Count < count)
        {
            int a = hubNodes[random.Next(hubs)], b = hubNodes[random.Next(hubs)];
            if (a != b) trips.Add(new Pair(a, b));
        }
        return trips;
    }

    private sealed class Window
    {
        public long SliceBytes, FinalBytes, Slices, FinalSlices, Searches, Legs, Collections, PeakSliceBytes, SliceCollections;
        public double PerSearch => (double)(SliceBytes + FinalBytes) / Searches;
        public double PerLeg => (double)FinalBytes / Legs;
    }

    private static Window Replay(BusyStreets streets, List<Pair> trips, bool shared)
    {
        var window = new Window();
        int gen0 = GC.CollectionCount(0);
        foreach (var trip in trips)
        {
            int beganGen0 = GC.CollectionCount(0);
            long began = GC.GetAllocatedBytesForCurrentThread();
            var search = streets.Begin(trip.Start, trip.Goal, shared);
            long begun = GC.GetAllocatedBytesForCurrentThread() - began;
            window.SliceBytes += begun;
            window.PeakSliceBytes = Math.Max(window.PeakSliceBytes, begun);
            window.SliceCollections += GC.CollectionCount(0) - beganGen0;
            window.Searches++;
            while (true)
            {
                bool finalizing = search.Finalizing;
                int sliceGen0 = GC.CollectionCount(0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool done = search.Advance(ExpansionStep);
                var tagged = done && !shared ? search.Result : null;
                if (tagged != null)
                    for (int i = 0; i < tagged.Legs.Count; i++) tagged.Legs[i].ServiceId = streets.Tag(streets.Path![i], streets.Path[i + 1]);
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                window.SliceCollections += GC.CollectionCount(0) - sliceGen0;
                window.PeakSliceBytes = Math.Max(window.PeakSliceBytes, bytes);
                if (finalizing || search.Finalizing || done && search.Result != null) { window.FinalBytes += bytes; window.FinalSlices++; }
                else { window.SliceBytes += bytes; window.Slices++; }
                if (done) break;
            }
            var road = search.Result;
            window.Legs += road?.Legs.Count ?? 0;
            search.Dispose();
        }
        window.Collections = GC.CollectionCount(0) - gen0;
        return window;
    }

    [Fact]
    public void ABusyStreetCarSearchReusesItsFrozenRoadLegsAndCutsSliceAllocation()
    {
        var copied = new BusyStreets(600);
        var shared = new BusyStreets(600);
        var warm = Trips(400, 11, 24);
        Replay(copied, warm, false); Replay(shared, warm, true);
        var busy = Trips(2000, 12, 24);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = Replay(copied, busy, false);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var after = Replay(shared, busy, true);
        var cold = new BusyStreets(600);
        var spread = Trips(300, 13, Side * Side);
        var coldCopied = Replay(cold, spread, false);
        var coldShared = Replay(new BusyStreets(600), spread, true);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "busy streets, {0} searches, {1} road legs: before {2:N0} B/search ({3:0.0} B/leg, expansion slices {4:N0} B over {5} slices, gen0 {6}); after {7:N0} B/search ({8:0.0} B/leg, expansion slices {9:N0} B, gen0 {10}); {11:0.0}x less; reuses {12}, builds {13}, cached {14}",
            before.Searches, before.Legs, before.PerSearch, before.PerLeg, before.SliceBytes, before.Slices, before.Collections,
            after.PerSearch, after.PerLeg, after.SliceBytes, after.Collections, before.PerSearch / after.PerSearch,
            shared.Graph.RoadLegReuses, shared.Graph.RoadLegBuilds, shared.Graph.CachedRoadLegs));
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "cold spread trips, {0} searches: before {1:N0} B/search, after {2:N0} B/search",
            coldCopied.Searches, coldCopied.PerSearch, coldShared.PerSearch));
        Assert.Equal(before.Legs, after.Legs);
        Assert.True(before.Legs > 20000, "legs " + before.Legs);
        Assert.True(after.PerSearch * 3 <= before.PerSearch, before.PerSearch + " -> " + after.PerSearch);
        Assert.True(after.SliceBytes <= before.SliceBytes, before.SliceBytes + " -> " + after.SliceBytes);
        Assert.True(coldShared.PerSearch <= coldCopied.PerSearch * 1.05, coldCopied.PerSearch + " -> " + coldShared.PerSearch);
        Assert.True(shared.Graph.CachedRoadLegs <= RoadSearchWorkspace.MaximumRoadLegs);
    }

    [Fact]
    public void SharedRoadLegsMatchCopiedOnesAndAreFrozen()
    {
        var copied = new BusyStreets(400);
        var shared = new BusyStreets(400);
        foreach (var trip in Trips(200, 21, 30))
        {
            var a = copied.Run(trip.Start, trip.Goal, false);
            var b = shared.Run(trip.Start, trip.Goal, true);
            Assert.Equal(a == null, b == null);
            if (a == null) continue;
            Assert.Equal(a.Legs.Count, b!.Legs.Count);
            Assert.Equal(a.EstimatedMinutes, b.EstimatedMinutes);
            for (int i = 0; i < a.Legs.Count; i++)
            {
                Assert.True(RouteLeg.SameContent.Equals(a.Legs[i], b.Legs[i]), "leg " + i);
                Assert.True(b.Legs[i].Frozen);
                Assert.Throws<InvalidOperationException>(() => b.Legs[i].Minutes = 1);
            }
        }
        Assert.True(shared.Graph.RoadLegReuses > 0);
    }

    [Fact]
    public void ACachedLegIsNotReusedWhenItsMinutesOrTagChange()
    {
        var streets = new BusyStreets(0);
        var first = streets.Run(0, Side * Side - 1, true)!;
        var again = streets.Run(0, Side * Side - 1, true)!;
        for (int i = 0; i < first.Legs.Count; i++) Assert.Same(first.Legs[i], again.Legs[i]);
        streets.Pace = 1.3;
        var slower = streets.Run(0, Side * Side - 1, true)!;
        Assert.Equal(first.Legs.Count, slower.Legs.Count);
        for (int i = 0; i < first.Legs.Count; i++)
        {
            Assert.NotSame(first.Legs[i], slower.Legs[i]);
            Assert.Equal(first.Legs[i].Minutes / 1.1 * 1.3, slower.Legs[i].Minutes, 9);
        }
        streets.TopologyId = new string('c', 64);
        var moved = streets.Run(0, Side * Side - 1, true)!;
        for (int i = 0; i < moved.Legs.Count; i++)
        {
            Assert.NotSame(slower.Legs[i], moved.Legs[i]);
            Assert.Contains(streets.TopologyId, moved.Legs[i].ServiceId, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ANestedSearchWhileTheWorkspaceIsBorrowedBuildsFreshLegsAndLeavesTheCacheAlone()
    {
        var streets = new BusyStreets(0);
        streets.Run(0, 59, true);
        long builds = streets.Graph.RoadLegBuilds, reuses = streets.Graph.RoadLegReuses;
        int cached = streets.Graph.CachedRoadLegs;
        using var held = streets.Begin(100, 3000, true);
        var nested = streets.Graph.BeginSearch(0, 59, _ => false, (_, _) => true, (a, b, n) => n * 1.1, edgeTag: streets.Tag);
        while (!nested.Advance(ExpansionStep)) { }
        Assert.NotNull(nested.Result);
        Assert.All(nested.Result!.Legs, leg => Assert.True(leg.Frozen && leg.ServiceId.Length > 0));
        nested.Dispose();
        Assert.Equal((builds, reuses, cached), (streets.Graph.RoadLegBuilds, streets.Graph.RoadLegReuses, streets.Graph.CachedRoadLegs));
    }

    [Fact]
    public void NoSearchSliceGrowsTheRoadLegCacheInColdWarmOrRetimedReplays()
    {
        var spread = Trips(300, 13, Side * Side);
        var busy = Trips(1000, 12, 24);
        var copied = new BusyStreets(600);
        var shared = new BusyStreets(600);
        var reads = new List<(string Name, Window Copied, Window Shared)>();
        int Capacity() => ((Dictionary<long, RouteLeg>)shared.Graph.RoadLegCache!).EnsureCapacity(0);
        int reserved = Capacity();
        void Read(string name, List<Pair> trips)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var c = Replay(copied, trips, false);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            reads.Add((name, c, Replay(shared, trips, true)));
            Assert.True(Capacity() == reserved, name + " road-leg cache capacity " + reserved + " -> " + Capacity());
        }
        Assert.True(reserved >= RoadSearchWorkspace.MaximumRoadLegs, "reserved capacity " + reserved);
        Read("cold spread", spread);
        Read("warm busy", busy);
        copied.Retime(2); shared.Retime(2);
        Assert.Equal(0, shared.Graph.CachedRoadLegs);
        Read("immediate retime busy", busy);
        foreach (var (name, c, sh) in reads)
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}, {1} searches: peak slice before {2:N0} B, after {3:N0} B; collections inside slices before {4}, after {5}; {6:N0} -> {7:N0} B/search",
                name, c.Searches, c.PeakSliceBytes, sh.PeakSliceBytes, c.SliceCollections, sh.SliceCollections, c.PerSearch, sh.PerSearch));
        output.WriteLine("cached road legs " + shared.Graph.CachedRoadLegs + ", reuses " + shared.Graph.RoadLegReuses + ", builds " + shared.Graph.RoadLegBuilds);
        foreach (var (name, c, sh) in reads)
        {
            Assert.True(sh.PeakSliceBytes <= c.PeakSliceBytes, name + " peak slice " + c.PeakSliceBytes + " -> " + sh.PeakSliceBytes);
            Assert.True(sh.SliceCollections <= c.SliceCollections, name + " collections inside slices " + c.SliceCollections + " -> " + sh.SliceCollections);
        }
    }

    [Fact]
    public void FiveRetimesOfTheRealRoadLegCacheKeepItsRetainedGraphBoundedAndCurrent()
    {
        const int Retimes = 5;
        var streets = new BusyStreets(600);
        var busy = Trips(1000, 12, 24);
        string types = "";
        long Census(out long legs, out long objects)
        {
            var heap = new RetainedHeap();
            heap.Exclude(streets.Nodes);
            foreach (var node in streets.Nodes) { heap.Exclude(node); heap.Exclude(node.Successors); }
            heap.Add("graph", streets.Graph);
            legs = heap.Types.FirstOrDefault(r => r.Name == "RouteLeg")?.Objects ?? 0;
            objects = heap.Objects;
            types = string.Join(", ", heap.Types.Take(4).Select(r => r.Name + " " + r.Bytes + " B"));
            return heap.Bytes;
        }
        var bytes = new List<long>();
        var retainedLegs = new List<long>();
        var lines = new List<string>();
        void Read(string stage)
        {
            long b = Census(out long legs, out long objects);
            bytes.Add(b); retainedLegs.Add(legs);
            lines.Add(stage + " " + b + " B, " + objects + " objects, RouteLeg " + legs + ", cached " + streets.Graph.CachedRoadLegs + " (" + types + ")");
        }
        Read("reserved");
        Replay(streets, busy, true);
        Read("R0 busy");
        for (int cycle = 1; cycle <= Retimes; cycle++)
        {
            streets.Retime(cycle % 2 == 1 ? 2 : 1);
            Assert.Equal(0, streets.Graph.CachedRoadLegs);
            Read("R" + cycle + " retimed");
            Replay(streets, busy, true);
            Assert.True(streets.Graph.CachedRoadLegs <= RoadSearchWorkspace.MaximumRoadLegs);
            Read("R" + cycle + " busy");
        }
        output.WriteLine("road-leg cache retained graph (Mono 64-bit layout model), routes discarded after every search: " + string.Join("; ", lines));
        long busyFirst = bytes[1], busyPeak = bytes.Where((_, i) => i % 2 == 1).Max();
        Assert.True(busyPeak - busyFirst < 50_000, "busy retained rose " + (busyPeak - busyFirst) + " B: " + string.Join(", ", bytes));
        for (int i = 2; i < bytes.Count; i += 2) Assert.Equal(0, retainedLegs[i]);
        Assert.True(bytes.Where((_, i) => i >= 2 && i % 2 == 0).Max() <= bytes[2] + 4096, "retimed retained: " + string.Join(", ", bytes));
    }
}
