using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RebuildFootprintTests
{
    private readonly ITestOutputHelper output;
    public Gr5RebuildFootprintTests(ITestOutputHelper output) { this.output = output; }

    private static Position P(double x, double z = 0) => new Position(x, 0, z);

    private static Route MakeRoute(Random random, Position from, int legs, TravelMode mode, string service, bool shared, double jitter = 0)
    {
        var route = new Route(new RouteLegs(legs));
        var at = from;
        for (int i = 0; i < legs; i++)
        {
            var next = new Position(at.X + 1 + random.NextDouble() * 4, 0, at.Z + random.NextDouble() - 0.5);
            route.Legs.Add(new RouteLeg { From = i == 0 ? new Position(at.X + jitter, 0, at.Z) : at, To = next, Minutes = 0.05 + random.NextDouble(), Mode = mode, ServiceId = service });
            at = next;
        }
        if (random.Next(4) == 0 && route.Legs.Count > 0) route.WithEstimate(route.Minutes + random.NextDouble() * 3);
        return shared ? TransportComposition.Shared(route) : route;
    }

    private static Position End(Route route, Position fallback) => route.Legs.Count == 0 ? fallback : route.Legs[route.Legs.Count - 1].To;

    private static long MinRoundBytes(int rounds, Action action)
    {
        action();
        long best = long.MaxValue;
        for (int r = 0; r < rounds; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            if (used < best) best = used;
        }
        return best;
    }

    private static (CityState State, CitySimulator City, Gr5RepeatedRetimeLeakTests.NightPlanner Planner) NightCity()
    {
        var planner = new Gr5RepeatedRetimeLeakTests.NightPlanner(true);
        var state = Gr5AllocationMeasure.RigCity();
        var homes = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var person in state.Citizens)
            if (person.Preferences.OwnsCar)
                person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = Gr5RepeatedRetimeLeakTests.NightPlanner.Curb(homes[person.HomeId].Position) };
        var city = new CitySimulator(state, planner);
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
        city.AdvanceTo(day + 15.5 * 60, int.MaxValue);
        return (state, city, planner);
    }

    private static (long Driver, long Commit, long Rebuild, CitySimulator City) Retime(CityState state, CitySimulator city, Gr5RepeatedRetimeLeakTests.NightPlanner planner, ClockRetimeDriver driver, double rate)
    {
        long driverBytes = 0, commitBytes = 0;
        while (true)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var outcome = driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds);
            long spent = GC.GetAllocatedBytesForCurrentThread() - before;
            driverBytes += spent;
            if (outcome == ClockRetimeOutcome.Committed) { commitBytes = spent; break; }
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
        }
        planner.Rate(rate);
        long started = GC.GetAllocatedBytesForCurrentThread();
        var rebuild = city.BeginRebuildRoutes(planner, driver.TakeRetimedTraffic());
        while (!rebuild.Step(0)) { }
        var result = rebuild.TakeResult();
        return (driverBytes, commitBytes, GC.GetAllocatedBytesForCurrentThread() - started, result);
    }

    private static List<CitizenState> Members(CityState state)
    {
        var all = new List<CitizenState>(state.Citizens);
        all.AddRange(state.VisitorRoutines);
        return all;
    }

    private static List<Position> Foci()
    {
        var foci = new List<Position>();
        for (int x = 0; x <= 2400; x += 300)
            for (int z = 0; z <= 2400; z += 300) foci.Add(new Position(x, 0, z));
        return foci;
    }

    [Fact]
    public void ARebuiltJourneyGridQueriesLikeAFreshGridHoldsNoReferencePerRegistrationAndRefilesWithoutAllocating()
    {
        var (state, city, planner) = NightCity();
        var driver = new ClockRetimeDriver();
        var rebuilt = new List<long>();
        foreach (double rate in new[] { 20.0, 1, 20, 1 })
        {
            var step = Retime(state, city, planner, driver, rate);
            city = step.City;
            rebuilt.Add(step.Rebuild);
        }
        Assert.True(city.ActiveJourneyIndexMatchesState());
        Assert.True(city.ActiveJourneys.Grid.Matches(city.ActiveJourneys.BodyCandidates, true));

        var fresh = new ActiveJourneyIndex(id => city.PlaceIndex.TryGetValue(id, out var place) ? place.Position : (Position?)null);
        foreach (var person in Members(state)) fresh.File(person);
        Assert.True(fresh.MatchesScan(state.Citizens, state.VisitorRoutines));
        Assert.Equal(fresh.BodyCandidates, city.ActiveJourneys.BodyCandidates);
        var mine = new List<IPersonView>();
        var theirs = new List<IPersonView>();
        int compared = 0;
        foreach (var focus in Foci())
            foreach (double radius in new[] { 120.0, 400, double.PositiveInfinity })
            {
                city.ActiveJourneys.Grid.Query(focus, radius, mine);
                fresh.Grid.Query(focus, radius, theirs);
                Assert.Equal(theirs, mine);
                compared += mine.Count;
            }
        Assert.True(compared > 0);

        RetainedHeap GridOnly(PresentationGrid grid)
        {
            var heap = new RetainedHeap();
            heap.Exclude(city); heap.Exclude(state);
            foreach (var person in Members(state)) { heap.Exclude(person); heap.Exclude(person.Journey); }
            heap.Add("grid", grid);
            return heap;
        }
        var rebuiltHeap = GridOnly(city.ActiveJourneys.Grid);
        var freshHeap = GridOnly(fresh.Grid);
        output.WriteLine("night city, 4 retimes: rebuild B " + string.Join(" / ", rebuilt) + "; grid " + city.ActiveJourneys.Grid.Count + " entries / "
            + city.ActiveJourneys.Grid.Registrations + " registrations; retained grid structure without people or journeys (Mono 64-bit layout model): rebuilt grid " + rebuiltHeap.Bytes + " B / "
            + rebuiltHeap.Objects + " objects / " + rebuiltHeap.References + " refs, incl. the spare entries and cell lists the hand-over keeps; the same members filed into a fresh grid without spares " + freshHeap.Bytes + " B / "
            + freshHeap.Objects + " objects / " + freshHeap.References + " refs; " + compared + " query results compared");
        int registrations = city.ActiveJourneys.Grid.Registrations;
        Assert.True(freshHeap.References * 2 < registrations, freshHeap.References + " refs for " + registrations + " registrations");
        Assert.True(rebuiltHeap.References < registrations, rebuiltHeap.References + " refs for " + registrations + " registrations");
        Assert.All(rebuilt.Skip(1), b => Assert.True(b < 5_500_000, "rebuild " + b + " B"));

        var members = Members(state);
        var foci = Foci();
        long before = 0;
        for (int round = 0; round < 4; round++)
        {
            if (round == 1) before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var person in members) city.ActiveJourneys.File(person);
            foreach (var focus in foci) city.ActiveJourneys.Grid.Query(focus, 400, mine);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ARetimeCommitResetsEveryCommutePreparationInPlaceAndAllocatesUnderAKilobyte()
    {
        var (state, city, planner) = NightCity();
        var driver = new ClockRetimeDriver();
        var schedules = Members(state);
        foreach (var person in schedules.Take(64))
        {
            var preparation = person.CommutePreparation;
            preparation.PlaceId = "work"; preparation.WorkStartsAt = 1; preparation.WorkEndsAt = 2; preparation.PreparedAt = 3; preparation.TravelMinutes = 4;
        }
        var held = schedules.Select(p => p.CommutePreparation).ToArray();
        var step = Retime(state, city, planner, driver, 20);
        output.WriteLine("retime of " + schedules.Count + " schedules: driver " + step.Driver + " B, commit " + step.Commit + " B, rebuild " + step.Rebuild + " B");
        Assert.True(step.Commit < 1024, "commit " + step.Commit + " B");
        for (int i = 0; i < schedules.Count; i++)
        {
            var preparation = schedules[i].CommutePreparation;
            Assert.Same(held[i], preparation);
            Assert.Equal("", preparation.PlaceId);
            Assert.Equal(0, preparation.WorkStartsAt); Assert.Equal(0, preparation.WorkEndsAt);
            Assert.Equal(0, preparation.PreparedAt); Assert.Equal(0, preparation.TravelMinutes);
        }
        Assert.True(step.City.ActiveJourneyIndexMatchesState());
    }

    [Fact]
    public void ARouteLegIsNinetySixBytesAndItsRoadTagMatchesTheDecodedService()
    {
        var legs = new RouteLeg[4096];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < legs.Length; i++) legs[i] = new RouteLeg();
        double bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)legs.Length;
        var heap = new RetainedHeap(); heap.Add("leg", new RouteLeg());
        long modelled = heap.Types.Single(r => r.Name == "RouteLeg").Bytes;
        output.WriteLine("RouteLeg: " + bytes + " B allocated (.NET), " + modelled + " B in the Mono 64-bit layout model");
        Assert.Equal(96, bytes);
        Assert.Equal(96, modelled);

        const string Topology = "3700000000000000000000000000000000000000000000000000000000000037";
        var services = new[]
        {
            "", "native:subway", "native:road:v1:" + Topology + ":12:13", "native:road:fragment1:" + Topology + ":7:7", "native:road:v1::1:2",
            "native:road:fragment1:ab:5:9", "native:road:v1:ab:cd:1:2", "native:road:v2:" + Topology + ":1:2", "native:road:v1:" + Topology + ":x:2",
            "native:road:v1:" + Topology + ":1", "native:road:fragment1:" + Topology + ":4294967296:1", "native:road:", "road:v1:ab:1:2",
        };
        var topologies = new[] { Topology, "ab", "", "ab:cd", null };
        foreach (string service in services)
        {
            var leg = new RouteLeg { From = new Position(1, 0, 2), To = new Position(3, 0, 4), Minutes = 2, Mode = TravelMode.Car, ServiceId = service };
            foreach (var tag in new[] { leg.RoadTag, leg.CopyAs(leg.From, new Position(5, 0, 6), 3, service).RoadTag })
            {
                var decoded = RoadTag.Decode(service);
                Assert.Equal(decoded.Kind, tag.Kind);
                Assert.Equal(decoded.TopologyLength, tag.TopologyLength);
                Assert.Equal(decoded.From, tag.From);
                Assert.Equal(decoded.To, tag.To);
                if (decoded.Kind != RoadTagKind.None) Assert.Equal(decoded.TopologyStart, tag.TopologyStart);
                foreach (var topology in topologies) Assert.Equal(decoded.IsTopology(service, topology), tag.IsTopology(service, topology));
            }
        }
        var frozen = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = new Position(0, 0, 0), To = new Position(1, 0, 0), Minutes = 1, ServiceId = services[2] })).Legs[0];
        Assert.True(frozen.Frozen && frozen.Shared && !frozen.SplitPrefix);
        var split = frozen.CopyAs(frozen.From, new Position(0.5, 0, 0), 0.5, frozen.ServiceId, true);
        Assert.True(split.SplitPrefix && !split.Frozen && !split.Shared);
        Assert.Throws<InvalidOperationException>(() => frozen.Minutes = 2);
        Assert.Same(frozen, frozen.CopyAs(frozen.From, frozen.To, frozen.Minutes, frozen.ServiceId));
    }

    private static List<TransportRoadNode> RoadGrid(int side)
    {
        var nodes = new List<TransportRoadNode>(side * side);
        for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                var next = new List<int>();
                if (x > 0) next.Add(z * side + x - 1); if (x + 1 < side) next.Add(z * side + x + 1);
                if (z > 0) next.Add((z - 1) * side + x); if (z + 1 < side) next.Add((z + 1) * side + x);
                nodes.Add(new TransportRoadNode { Position = new Position(x * 50, 0, z * 50), KilometresPerHour = 36, AllowsCar = true, Successors = next.ToArray() });
            }
        return nodes;
    }

    [Fact]
    public void ASuccessorRoadGraphOverTheSameNodesReusesTheSearchWorkspaceAndPlansTheSameRoute()
    {
        const int Side = 120;
        var nodes = RoadGrid(Side);
        Func<int, bool> open = _ => false;
        Func<Position, Position, bool> clear = (_, _) => true;
        Func<int, int, double, double> traversal = (_, _, baseline) => baseline;
        int start = 3, goal = Side * Side - 7;
        var first = new TransportRoadGraph(nodes, 1);
        Assert.NotNull(first.PlanWithTraversal(start, goal, open, clear, traversal, 1_000_000));
        var successor = new TransportRoadGraph(nodes, 2, first);
        var unrelated = new TransportRoadGraph(RoadGrid(Side), 2, first);
        var fresh = new TransportRoadGraph(nodes, 2);
        Assert.True(successor.SharesWorkspaceWith(first));
        Assert.False(unrelated.SharesWorkspaceWith(first));
        Assert.False(fresh.SharesWorkspaceWith(first));
        long before = GC.GetAllocatedBytesForCurrentThread();
        var reused = successor.PlanWithTraversal(start, goal, open, clear, traversal, 1_000_000);
        long reusedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var rebuilt = fresh.PlanWithTraversal(start, goal, open, clear, traversal, 1_000_000);
        long freshBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine(nodes.Count + " road nodes, first search after a rerate: shared workspace " + reusedBytes + " B, new workspace " + freshBytes + " B");
        Assert.NotNull(reused); Assert.NotNull(rebuilt);
        Assert.Equal(rebuilt!.Legs.Count, reused!.Legs.Count);
        for (int i = 0; i < rebuilt.Legs.Count; i++) Assert.True(RouteLeg.SameContent.Equals(rebuilt.Legs[i], reused.Legs[i]), "leg " + i);
        Assert.True(freshBytes - reusedBytes >= 24L * nodes.Count, reusedBytes + " vs " + freshBytes);

        var held = first.BeginSearch(start, goal, open, clear, traversal, 1_000_000);
        var concurrent = successor.PlanWithTraversal(start, goal, open, clear, traversal, 1_000_000);
        while (!held.Advance(1 << 20)) { }
        Assert.NotNull(held.Result);
        held.Dispose();
        Assert.NotNull(concurrent);
        Assert.Equal(rebuilt.Legs.Count, concurrent!.Legs.Count);
        Assert.Equal(reused.Legs.Count, successor.PlanWithTraversal(start, goal, open, clear, traversal, 1_000_000)!.Legs.Count);
    }

    [Fact]
    public void FivePartCarCandidateScoringMatchesJoinAndValidateAndAllocatesZeroBytes()
    {
        var random = new Random(3801);
        int valid = 0, invalid = 0;
        var samples = new List<(Route? A, Route? B, Route? C, Route? D, Route? E, Position From, Position To, TravelPreferences Prefs, Position? Parked)>(512);
        for (int i = 0; i < 4000; i++)
        {
            var from = P(random.NextDouble() * 10, random.NextDouble() * 10);
            var access = MakeRoute(random, from, random.Next(0, 12), TravelMode.Walk, "", random.Next(2) == 0);
            var parked = End(access, from);
            var prep = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = parked, To = parked, Mode = TravelMode.Car, Minutes = 2 }));
            var road = MakeRoute(random, parked, random.Next(1, 30), random.Next(15) == 0 ? TravelMode.Walk : TravelMode.Car, "", random.Next(2) == 0, random.Next(15) == 0 ? 0.01 : 0);
            var roadEnd = End(road, parked);
            var park = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = roadEnd, To = roadEnd, Mode = TravelMode.Car, Minutes = 2 }));
            var egress = MakeRoute(random, roadEnd, random.Next(0, 12), TravelMode.Walk, "", random.Next(2) == 0, random.Next(15) == 0 ? 0.002 : 0);
            var to = End(egress, roadEnd);
            if (random.Next(12) == 0) to = new Position(to.X + 0.01, 0, to.Z);
            var prefs = new TravelPreferences
            {
                OwnsCar = random.Next(6) != 0,
                AllowTransit = true,
                WalkingToleranceMetres = random.Next(6) == 0 ? 5 : 4000,
            };
            Position? parkedArg = random.Next(10) == 0 ? (Position?)null : parked;
            Route? a = random.Next(25) == 0 ? null : access;
            Route? c = random.Next(25) == 0 ? null : road;

            var joined = TransportComposition.Join(a, prep, c, park, egress);
            bool expected = joined != null && TransportComposition.Valid(joined, from, to, prefs, parkedArg);
            bool actual = TransportComposition.TryJoinedScore(a, prep, c, park, egress, from, to, prefs, parkedArg, out double score);
            Assert.Equal(expected, actual);
            if (expected)
            {
                valid++;
                Assert.Equal(BitConverter.DoubleToInt64Bits(joined!.EstimatedMinutes), BitConverter.DoubleToInt64Bits(score));
                if (samples.Count < 256) samples.Add((a, prep, c, park, egress, from, to, prefs, parkedArg));
            }
            else invalid++;
        }
        Assert.True(valid > 500 && invalid > 500);

        long scoreAlloc = MinRoundBytes(5, () =>
        {
            for (int r = 0; r < 10; r++)
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    TransportComposition.TryJoinedScore(s.A, s.B, s.C, s.D, s.E, s.From, s.To, s.Prefs, s.Parked, out _);
                }
        });
        Assert.Equal(0, scoreAlloc);

        var candidates = new RouteCandidates();
        var fastWalk = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = samples[0].From, To = samples[0].To, Mode = TravelMode.Walk, Minutes = 0.01 }));
        long losingBefore = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 200; i++)
            {
                var s = samples[0];
                candidates.Reset();
                candidates.Consider(fastWalk, s.From, s.To, s.Prefs, s.Parked);
                candidates.Consider(TransportComposition.Join(s.A, s.B, s.C, s.D, s.E), s.From, s.To, s.Prefs, s.Parked);
            }
        }) / 200;
        long losingAfter = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 200; i++)
            {
                var s = samples[0];
                candidates.Reset();
                candidates.Consider(fastWalk, s.From, s.To, s.Prefs, s.Parked);
                candidates.ConsiderJoined(s.A, s.B, s.C, s.D, s.E, s.From, s.To, s.Prefs, s.Parked);
            }
        }) / 200;

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "5-part car candidate: {0} valid / {1} refused bit-identical; TryJoinedScore {2} B over {3} calls; losing car candidate Join+Consider {4:N0} B -> ConsiderJoined {5:N0} B (.NET)",
            valid, invalid, scoreAlloc, samples.Count * 10, losingBefore, losingAfter));
        Assert.Equal(0, losingAfter);
        Assert.True(losingBefore > 0);
    }

    [Fact]
    public void CarAppearanceSplitHashMatchesConcatenationWithZeroAllocation()
    {
        static uint Fnv(string text)
        {
            unchecked
            {
                uint value = 2166136261;
                foreach (char c in text) value = (value ^ c) * 16777619;
                return value;
            }
        }
        var ids = Enumerable.Range(0, 500).Select(i => "car:resident-" + i + ":vehicle").ToArray();
        foreach (string id in ids)
        {
            Assert.Equal(Fnv(id + ":paint"), CarAppearance.Hash(id, ":paint"));
            Assert.Equal(Fnv(id + ":parked"), CarAppearance.Hash(id, ":parked"));
            Assert.Equal(Fnv(id), CarAppearance.Hash(id));
            Assert.Equal(1 + (int)(Fnv(id + ":paint") % 2147483646u), CarAppearance.Paint(new CarState { Id = id }));
        }
        Assert.Equal(Fnv(""), CarAppearance.Hash(null!));
        long alloc = MinRoundBytes(5, () =>
        {
            uint acc = 0;
            for (int i = 0; i < ids.Length; i++) acc ^= CarAppearance.Hash(ids[i], ":parked");
            if (acc == 123456789u) throw new InvalidOperationException();
        });
        output.WriteLine("CarAppearance.Hash(id, suffix) over 500 IDs: " + alloc + " B (.NET)");
        Assert.Equal(0, alloc);
    }
}
