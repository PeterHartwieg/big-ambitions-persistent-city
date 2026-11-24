using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RepeatedRetimeLeakTests
{
    private const int Cycles = 4;
    private const long Allowance = 1_000_000, PlateauAllowance = 250_000;
    private static readonly string[] Watched = { "CitySimulator", "TrafficKernel", "FoodCommerce", "FoodMoneyCounters", "RouteLeg", "RouteLeg[]", "Int64[]", "Position[]", "Double[]", "TrafficLinkDefinition", "FrozenLink", "Entry" };
    private readonly ITestOutputHelper output;
    public Gr5RepeatedRetimeLeakTests(ITestOutputHelper output) { this.output = output; }

    private static RetainedHeap Census(params (string Name, object Root)[] roots)
    {
        var heap = new RetainedHeap();
        foreach (var (name, root) in roots) heap.Add(name, root);
        return heap;
    }

    private static long Objects(RetainedHeap heap, string type) => heap.Types.FirstOrDefault(r => r.Name == type)?.Objects ?? 0;

    private static long Bytes(RetainedHeap heap, string type) => heap.Types.FirstOrDefault(r => r.Name == type)?.Bytes ?? 0;
    private static readonly string[] JourneyLegs = { "RouteLeg", "RouteLeg[]" };
    private static long Settled(RetainedHeap heap) => heap.Bytes - JourneyLegs.Sum(t => Bytes(heap, t));

    private void Judge(string fixture, IReadOnlyList<RetainedHeap> retimed, IReadOnlyList<RetainedHeap> control, IReadOnlyList<int> travelling, RetainedHeap drained, RetainedHeap drainedControl)
    {
        var excess = retimed.Zip(control, (r, c) => r.Bytes - c.Bytes).ToList();
        var settled = retimed.Zip(control, (r, c) => Settled(r) - Settled(c)).ToList();
        var legs = retimed.Zip(control, (r, c) => Objects(r, "RouteLeg") - Objects(c, "RouteLeg")).ToList();
        output.WriteLine(fixture + ": retained bytes retimed / control / excess / excess without journey legs at R0..R" + Cycles + " (Mono 64-bit layout model):");
        for (int i = 0; i < retimed.Count; i++)
            output.WriteLine("  R" + i + " " + retimed[i].Bytes + " / " + control[i].Bytes + " / " + excess[i].ToString("+0;-0") + " / " + settled[i].ToString("+0;-0") + " B, objects " + retimed[i].Objects + " / " + control[i].Objects
                + ", travelling " + travelling[i] + "; " + string.Join(", ", Watched.Select(t => t + " " + Objects(retimed[i], t) + "/" + Objects(control[i], t))));
        long drainedExcess = drained.Bytes - drainedControl.Bytes;
        output.WriteLine("  drained (every journey in flight at R" + Cycles + " has arrived): " + drained.Bytes + " / " + drainedControl.Bytes + " / " + drainedExcess.ToString("+0;-0") + " B; RouteLeg "
            + Objects(drained, "RouteLeg") + "/" + Objects(drainedControl, "RouteLeg") + ", objects " + drained.Objects + " / " + drainedControl.Objects);
        Assert.Equal(0, excess[0]);
        Assert.All(retimed, heap => Assert.Equal(1, Objects(heap, "CitySimulator")));
        Assert.All(excess, e => Assert.True(e < Allowance, fixture + " retimed minus control " + e + " B"));
        long first = settled[1], plateau = settled.Skip(1).Max();
        Assert.True(plateau - first < PlateauAllowance, fixture + " excess without journey legs rose by " + (plateau - first) + " B after R1: " + string.Join(", ", settled));
        bool risingEveryCycle = true;
        for (int i = 2; i < settled.Count; i++) risingEveryCycle &= settled[i] > settled[i - 1];
        Assert.False(risingEveryCycle, fixture + " excess without journey legs rose at every checkpoint: " + string.Join(", ", settled));
        for (int i = 1; i < legs.Count; i++)
            Assert.True(legs[i] <= 2L * i * travelling[0], fixture + " extra RouteLeg at R" + i + " " + legs[i] + " exceeds one per travelling journey per retime");
        Assert.True(drainedExcess < Allowance, fixture + " drained retimed minus control " + drainedExcess + " B");
        Assert.True(Math.Abs(Objects(drained, "RouteLeg") - Objects(drainedControl, "RouteLeg")) <= travelling[0] / 10 + 2,
            fixture + " drained RouteLeg " + Objects(drained, "RouteLeg") + " vs control " + Objects(drainedControl, "RouteLeg"));
        Assert.Equal(1, Objects(drained, "CitySimulator"));
        foreach (string type in new[] { "TrafficKernel", "FoodCommerce", "FoodMoneyCounters" })
        {
            var counts = retimed.Skip(1).Select(h => Objects(h, type)).Distinct().ToList();
            Assert.True(counts.Count == 1 && counts[0] == Objects(control[0], type), fixture + " " + type + " counts: " + string.Join(", ", retimed.Select(h => Objects(h, type))));
        }
    }

    private static long OwnedLegs(CityState state)
    {
        var legs = new HashSet<RouteLeg>(ReferenceEqualityComparer.Instance);
        foreach (var person in state.Citizens.Concat(state.VisitorRoutines))
            if (person.Journey != null) foreach (var leg in person.Journey.Legs) legs.Add(leg);
        return legs.Count;
    }

    private static int Travelling(CitySimulator city) => city.State.Citizens.Count(c => c.Phase == CitizenPhase.Travelling)
        + city.State.VisitorRoutines.Count(c => c.Phase == CitizenPhase.Travelling);

    [Fact]
    public void FourOneTwoOneRetimesOfTheRigTrafficCityRetainNoMoreThanAnUnretimedControlAndPlateau()
    {
        const string native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        (CitySimulator City, TrafficNetwork One, TrafficRateCache Cache) Build()
        {
            var one = new TrafficNetwork(native, Gr5NetworkFootprintTests.SignalledRoad(1), 1);
            var city = Gr5NetworkRequeueTests.RoadCity(750, 2250, new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = one });
            city.RefreshTrafficClosures();
            city.AdvanceTo(city.State.Minute + 2, int.MaxValue);
            var cache = new TrafficRateCache();
            cache.Remember(1, one);
            return (city, one, cache);
        }
        var retimed = Build(); var control = Build();
        var driver = new ClockRetimeDriver();
        var doubled = Gr5NetworkFootprintTests.SignalledRoad(2).ToDictionary(n => n.Id, n => n.Signal);
        var two = retimed.One.Rerated(2, id => doubled[id]);
        var city = retimed.City; var state = city.State;
        var retimedReads = new List<RetainedHeap> { Census(("simulation", city), ("cache", retimed.Cache), ("driver", driver)) };
        var controlReads = new List<RetainedHeap> { Census(("simulation", control.City), ("cache", control.Cache), ("driver", new ClockRetimeDriver())) };
        var travelling = new List<int> { Travelling(city) };
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            if (!retimed.Cache.TryGet(2, out var rated)) { rated = two; retimed.Cache.Remember(2, two); }
            city = Gr5NetworkFootprintTests.FullRetime(state, city, driver, 2, rated);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            Assert.True(retimed.Cache.TryGet(1, out var back));
            city = Gr5NetworkFootprintTests.FullRetime(state, city, driver, 1, back);
            double end = state.Minute + 0.01;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            Assert.Equal(end, control.City.State.Minute);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            retimedReads.Add(Census(("simulation", city), ("cache", retimed.Cache), ("driver", driver)));
            controlReads.Add(Census(("simulation", control.City), ("cache", control.Cache), ("driver", new ClockRetimeDriver())));
            travelling.Add(Travelling(city));
        }
        Assert.True(city.Traffic!.VehicleCount > 0);
        double drainedAt = state.Minute + 30;
        city.AdvanceTo(drainedAt, int.MaxValue); control.City.AdvanceTo(drainedAt, int.MaxValue);
        Assert.Equal(0, Travelling(city));
        Judge("750 drivers, " + retimed.One.Links.Count + " links", retimedReads, controlReads, travelling,
            Census(("simulation", city), ("cache", retimed.Cache), ("driver", driver)), Census(("simulation", control.City), ("cache", control.Cache), ("driver", new ClockRetimeDriver())));
    }

    [Fact]
    public void FourOneTwoOneRetimesOfTheRigFoodCityRetainNoMoreThanAnUnretimedControlAndPlateau()
    {
        (CityState State, CitySimulator City) Build()
        {
            var state = Gr5AllocationMeasure.RigCity();
            var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
            return (state, city);
        }
        var retimed = Build(); var control = Build();
        var driver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        var retimedReads = new List<RetainedHeap> { Census(("simulation", city), ("driver", driver)) };
        var controlReads = new List<RetainedHeap> { Census(("simulation", control.City), ("driver", new ClockRetimeDriver())) };
        var travelling = new List<int> { Travelling(city) };
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = state.Minute + 0.01;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            Assert.Equal(end, control.State.Minute);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            Assert.Equal(FoodMoneyCounters.Rescan(state.Food!), city.FoodMoney!.Snapshot());
            retimedReads.Add(Census(("simulation", city), ("driver", driver)));
            controlReads.Add(Census(("simulation", control.City), ("driver", new ClockRetimeDriver())));
            travelling.Add(Travelling(city));
        }
        var inFlight = state.Citizens.Concat(state.VisitorRoutines).Where(c => c.Phase == CitizenPhase.Travelling).ToList();
        double drainedAt = inFlight.Count == 0 ? state.Minute : inFlight.Max(c => c.NextEventMinute) + 0.01;
        city.AdvanceTo(drainedAt, int.MaxValue); control.City.AdvanceTo(drainedAt, int.MaxValue);
        Judge("rig food city, " + state.Citizens.Count + " residents, " + state.VisitorRoutines.Count + " visitors, " + state.Food!.Residents.Count + " meals", retimedReads, controlReads, travelling,
            Census(("simulation", city), ("driver", driver)), Census(("simulation", control.City), ("driver", new ClockRetimeDriver())));
    }

    [Fact]
    public void TheRetimeHandoverPeakLiveGraphStaysFlatAcrossRepeatedOneTwoOneCycles()
    {
        const int HandoverCycles = 4;
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        var driver = new ClockRetimeDriver();
        var peaks = new Dictionary<double, List<RetainedHeap>> { [2] = new List<RetainedHeap>(), [1] = new List<RetainedHeap>() };
        var settled = new Dictionary<double, List<RetainedHeap>> { [2] = new List<RetainedHeap>(), [1] = new List<RetainedHeap>() };
        output.WriteLine("rig food city from 15:30, " + HandoverCycles + " 1->2->1 cycles; live graph read after every driver slice and at every rebuild yield while the rebuild iterator is reachable (simulation + driver + rebuild roots, Mono 64-bit layout model):");
        string Line(RetainedHeap heap) => heap.Bytes + " B / " + heap.Objects + " objects, CitySimulator " + Objects(heap, "CitySimulator") + ", RouteLeg " + Objects(heap, "RouteLeg") + ", RouteLeg[] " + Bytes(heap, "RouteLeg[]") + " B";
        void Retime(double rate, int cycle)
        {
            var before = Census(("simulation", city), ("driver", driver));
            var peak = before;
            string where = "before";
            void Observe(RetainedHeap heap, string stage) { if (heap.Bytes > peak.Bytes) { peak = heap; where = stage; } }
            int slices = 0;
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed)
                Observe(Census(("simulation", city), ("driver", driver)), "driver slice " + ++slices);
            var committed = Census(("simulation", city), ("driver", driver));
            Observe(committed, "commit");
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            Observe(Census(("simulation", city), ("rebuild", rebuild), ("driver", driver)), "rebuild begun");
            int yields = 0;
            while (!rebuild.Step(0))
                Observe(Census(("simulation", city), ("rebuild", rebuild), ("driver", driver)), "rebuild yield " + ++yields);
            var result = rebuild.TakeResult();
            var handedOver = Census(("simulation", city), ("result", result), ("rebuild", rebuild), ("driver", driver));
            Observe(handedOver, "old and new simulation held");
            city = result;
            var after = Census(("simulation", city), ("driver", driver));
            output.WriteLine("  cycle " + cycle + " to " + rate + "x: " + slices + " driver slices, " + yields + " rebuild yields; before " + Line(before) + "; peak at " + where + " " + Line(peak)
                + "; settled " + Line(after) + "; peak minus settled " + (peak.Bytes - after.Bytes).ToString("+0;-0;0") + " B / " + (peak.Objects - after.Objects).ToString("+0;-0;0") + " objects");
            Assert.True(Objects(handedOver, "CitySimulator") <= 2);
            Assert.Equal(1, Objects(after, "CitySimulator"));
            Assert.True(yields > 0, "the rebuild finished without a yield to read");
            peaks[rate].Add(peak); settled[rate].Add(after);
        }
        for (int cycle = 1; cycle <= HandoverCycles; cycle++)
        {
            Retime(2, cycle);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            Retime(1, cycle);
            city.AdvanceTo(state.Minute + 0.01, int.MaxValue);
        }
        foreach (double rate in new[] { 2.0, 1.0 })
        {
            var p = peaks[rate]; var a = settled[rate];
            output.WriteLine("  to " + rate + "x by cycle: peak " + string.Join(" / ", p.Select(h => h.Bytes + " B (" + h.Objects + " objects)")) + "; settled " + string.Join(" / ", a.Select(h => h.Bytes + " B (" + h.Objects + " objects)")));
            for (int k = 1; k < HandoverCycles; k++)
            {
                Assert.True(p[k].Bytes - p[k - 1].Bytes < PlateauAllowance, "to " + rate + "x peak rose from cycle " + k + " to " + (k + 1) + ": " + string.Join(", ", p.Select(h => h.Bytes)));
                Assert.True(a[k].Bytes - a[k - 1].Bytes < PlateauAllowance, "to " + rate + "x settled graph rose from cycle " + k + " to " + (k + 1) + ": " + string.Join(", ", a.Select(h => h.Bytes)));
            }
            Assert.True(p[HandoverCycles - 1].Bytes - p[1].Bytes < PlateauAllowance, "to " + rate + "x peak rose over cycles 2.." + HandoverCycles + ": " + string.Join(", ", p.Select(h => h.Bytes)));
            Assert.True(a[HandoverCycles - 1].Bytes - a[1].Bytes < PlateauAllowance, "to " + rate + "x settled graph rose over cycles 2.." + HandoverCycles + ": " + string.Join(", ", a.Select(h => h.Bytes)));
        }
    }

    private static long InstrumentBytes()
    {
        var census = new RetainedCensus();
        census.AddStatics(new[] { typeof(RetainedCensus).Assembly }, "static.");
        return census.Roots.Where(r => r.Name.StartsWith("static.RetainedCensus.", StringComparison.Ordinal)).Sum(r => r.Bytes);
    }

    private static object NovelGraph(int walk)
    {
        var element = new[] { typeof(byte), typeof(short), typeof(long), typeof(float), typeof(decimal), typeof(char), typeof(ushort), typeof(uint) }[walk];
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(ValueTuple<,>).MakeGenericType(element, typeof(string))))!;
        var map = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(element, typeof(List<string>)))!;
        return new object[] { list, map, Array.CreateInstance(element, 4) };
    }

    [Fact]
    public void RepeatedCensusWalksOverNewTypesRetainNothingInTheCensusInstrument()
    {
        long before = InstrumentBytes();
        var reads = new List<long>();
        for (int walk = 0; walk < 8; walk++)
        {
            var census = new RetainedCensus();
            census.Add("graph", NovelGraph(walk));
            census.Add("city", Gr5AllocationMeasure.RigCity());
            Assert.True(census.Bytes > 0);
            reads.Add(InstrumentBytes());
        }
        output.WriteLine("census instrument retained bytes before / after each of 8 walks over new types (Mono 64-bit layout model): " + before + " / " + string.Join(", ", reads));
        Assert.All(reads, read => Assert.Equal(before, read));
        Assert.Equal(0, before);
    }

    private sealed class StandInMesh { public bool Alive = true; public int Vertices; }
    private sealed class StandInBody { public bool Alive = true; public int Person; public StandInMesh? Mesh; }

    private sealed class BodyReplay
    {
        private const int Ceiling = 96;
        private readonly int[] catalogue;
        public readonly MergedBodyLedger<StandInBody, StandInMesh> Ledger = new(b => b.Alive, m => m.Alive, m => m.Vertices);
        public readonly LengthScratch<(float X, float Y, float Z)> Scratch = BlendshapeBakePlan.NewScratch<(float X, float Y, float Z)>();
        private readonly Dictionary<int, StandInBody> active = new();
        private readonly List<StandInBody> idle = new(), all = new(), reserve = new();
        public int Reserved => reserve.Count;
        public BodyReplay(int[] catalogue) { this.catalogue = catalogue; }

        private int Part(int person, int part) => catalogue[(person + part * 13) % catalogue.Length];

        private void Destroy(StandInBody body)
        {
            Ledger.Forget(body);
            if (body.Mesh != null) { Ledger.CountReleased(body.Mesh); body.Mesh.Alive = false; }
            body.Alive = false;
        }

        public void Show(int visible)
        {
            foreach (var person in active.Keys.Where(p => p >= visible).ToList()) { idle.Add(active[person]); active.Remove(person); }
            for (int person = 0; person < visible; person++)
            {
                if (active.ContainsKey(person)) continue;
                var body = idle.FirstOrDefault(b => b.Person == person);
                if (body != null) { idle.Remove(body); active.Add(person, body); continue; }
                body = new StandInBody { Person = person };
                all.Add(body);
                Ledger.Own(body);
                int vertices = 0;
                for (int part = 0; part < 5; part++) { int length = Part(person, part); Scratch.Take(length); vertices += length; }
                body.Mesh = new StandInMesh { Vertices = vertices };
                Ledger.Merged(body, body.Mesh);
                active.Add(person, body);
            }
        }

        public void Retime(bool leak)
        {
            foreach (var body in active.Values) idle.Add(body);
            active.Clear();
            if (reserve.Count > 0) { foreach (var kept in reserve) Destroy(kept); reserve.Clear(); Scratch.Clear(); }
            foreach (var body in idle) reserve.Add(body);
            foreach (var body in all) if (!idle.Contains(body)) Destroy(body);
            if (reserve.Count == 0) Scratch.Clear();
            idle.Clear(); all.Clear();
            Ledger.PruneOwned(); Ledger.Prune();
            if (leak) { reserve.Clear(); return; }
            foreach (var kept in reserve)
            {
                if (all.Count >= Ceiling) { Destroy(kept); continue; }
                all.Add(kept); idle.Add(kept);
            }
            reserve.Clear();
        }

        public (int Owned, int Live, long Vertices, int Unmerged, long Scratch) Read()
        {
            int live = Ledger.LiveMeshes(out long vertices);
            return (Ledger.Owned, live, vertices, Ledger.Unmerged, Scratch.Bytes);
        }
    }

    private static List<(int Owned, int Live, long Vertices, int Unmerged, long Scratch)> ReplayBodies(int[] catalogue, int[] visibleAt, bool retime, bool leak)
    {
        var replay = new BodyReplay(catalogue);
        var reads = new List<(int, int, long, int, long)>();
        for (int checkpoint = 0; checkpoint < visibleAt.Length; checkpoint++)
        {
            if (retime && checkpoint > 0 && checkpoint < visibleAt.Length - 1)
                for (int leg = 0; leg < 2; leg++) { replay.Retime(leak); replay.Show(Math.Max(1, visibleAt[checkpoint] / 2)); }
            replay.Show(visibleAt[checkpoint]);
            replay.Show(Math.Max(1, visibleAt[checkpoint] - 3));
            replay.Show(visibleAt[checkpoint]);
            reads.Add(replay.Read());
        }
        return reads;
    }

    [Fact]
    public void RepeatedRetimeBodyReleaseAndReserveAdoptionKeepOwnershipMeshesAndScratchMatchedToAControl()
    {
        var raw = Enumerable.Range(0, 100).Select(i => 300 + i * 37 + (i * 7919) % 900).Distinct().ToArray();
        double scale = (double)(1_718_964 / (BlendshapeBakePlan.ScratchWidth * BlendshapeBakePlan.ScratchElementBytes)) / raw.Sum();
        var catalogue = raw.Select(v => Math.Max(64, (int)(v * scale))).Distinct().ToArray();
        int[] visibleAt = { 8, 9, 12, 16, 24, 31, 63 };
        var control = ReplayBodies(catalogue, visibleAt, retime: false, leak: false);
        var retimed = ReplayBodies(catalogue, visibleAt, retime: true, leak: false);
        var leaky = ReplayBodies(catalogue, visibleAt, retime: true, leak: true);
        string Row(List<(int Owned, int Live, long Vertices, int Unmerged, long Scratch)> r) => string.Join(", ", r.Select(x => x.Owned + "/" + x.Live + "/" + x.Vertices + "/" + x.Scratch));
        output.WriteLine("owned / live meshes / live vertices / bake scratch at R0..+30 min (production MergedBodyLedger and bake scratch policy, PhysicalPedestrians reserve rules):");
        output.WriteLine("  control " + Row(control));
        output.WriteLine("  retimed " + Row(retimed));
        output.WriteLine("  leaky retimed (reserve neither adopted nor destroyed) " + Row(leaky));
        for (int i = 0; i < visibleAt.Length; i++)
        {
            Assert.Equal(retimed[i].Owned, retimed[i].Live + retimed[i].Unmerged);
            Assert.Equal(control[i].Owned, retimed[i].Owned);
            Assert.Equal(control[i].Live, retimed[i].Live);
            Assert.Equal(control[i].Vertices, retimed[i].Vertices);
            Assert.Equal(control[i].Scratch, retimed[i].Scratch);
            Assert.True(retimed[i].Scratch <= BlendshapeBakePlan.ScratchBudgetBytes / 2);
        }
        Assert.Contains(Enumerable.Range(0, visibleAt.Length), i => leaky[i].Owned > control[i].Owned && leaky[i].Vertices > control[i].Vertices);
    }

    [Fact]
    public void ProductionBakeScratchGrowsOnlyWithFirstLookEncountersAndRetimeRebakesAddNothing()
    {
        const long plateau16t = 1_718_964;
        var raw = Enumerable.Range(0, 100).Select(i => 300 + i * 37 + (i * 7919) % 900).Distinct().ToArray();
        long vertices = plateau16t / (BlendshapeBakePlan.ScratchWidth * BlendshapeBakePlan.ScratchElementBytes);
        double scale = (double)vertices / raw.Sum();
        var catalogue = raw.Select(v => Math.Max(64, (int)(v * scale))).Distinct().ToArray();
        int Part(int body, int part) => catalogue[(body + part * 13) % catalogue.Length];
        int[] bodiesAt = { 8, 9, 12, 16, 24, 31, 63 };
        var control = BlendshapeBakePlan.NewScratch<(float X, float Y, float Z)>();
        var retimed = BlendshapeBakePlan.NewScratch<(float X, float Y, float Z)>();
        void Bake(LengthScratch<(float X, float Y, float Z)> scratch, int body) { for (int part = 0; part < 5; part++) scratch.Take(Part(body, part)); }
        var controlReads = new List<long>(); var retimedReads = new List<long>();
        int spawned = 0;
        for (int checkpoint = 0; checkpoint < bodiesAt.Length; checkpoint++)
        {
            for (; spawned < bodiesAt[checkpoint]; spawned++) { Bake(control, spawned); Bake(retimed, spawned); }
            if (checkpoint > 0 && checkpoint < bodiesAt.Length - 1)
                for (int retime = 0; retime < 2; retime++) for (int body = 0; body < spawned; body++) Bake(retimed, body);
            controlReads.Add(control.Bytes); retimedReads.Add(retimed.Bytes);
        }
        long seen = retimed.Bytes, allocations = retimed.Allocations;
        var matched = new List<long>(8);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int cycle = 0; cycle < 5; cycle++) { for (int body = 0; body < spawned; body++) Bake(retimed, body); matched.Add(retimed.Bytes); }
        long churn = GC.GetAllocatedBytesForCurrentThread() - allocated;
        long catalogueBytes = catalogue.Sum(length => retimed.CostOf(length));
        output.WriteLine("production bake scratch (budget " + BlendshapeBakePlan.ScratchBudgetBytes + " B), catalogue " + catalogue.Length + " lengths / " + catalogueBytes
            + " B, bodies at R0..+30 min " + string.Join(", ", bodiesAt) + ": control (first encounters only) " + string.Join(", ", controlReads)
            + "; retimed (every live body re-baked twice per checkpoint) " + string.Join(", ", retimedReads) + "; 5 matched cycles after the ramp " + string.Join(", ", matched)
            + "; allocations " + allocations + ", evictions " + retimed.Evictions + ", bytes allocated in the matched cycles " + churn + " (.NET)");
        Assert.Equal(controlReads, retimedReads);
        bool risesLikeL = true;
        for (int i = 1; i < controlReads.Count - 1; i++) risesLikeL &= controlReads[i] > controlReads[i - 1];
        Assert.True(risesLikeL, "the replay should rise at every L checkpoint from first encounters alone: " + string.Join(", ", controlReads));
        Assert.True(seen <= catalogueBytes);
        Assert.All(matched, b => Assert.Equal(seen, b));
        Assert.Equal(allocations, retimed.Allocations);
        Assert.Equal(control.Allocations, retimed.Allocations);
        Assert.Equal(0, retimed.Evictions);
        Assert.Equal(0, churn);
        Assert.True(BlendshapeBakePlan.ScratchBudgetBytes >= 2 * catalogueBytes);
    }

    [Fact]
    public void BakeScratchStaysWithinItsBudgetAndStopsAllocatingOnceTheBodyLooksRepeat()
    {
        const long budget = 2L * 1024 * 1024;
        var scratch = new LengthScratch<(float X, float Y, float Z)>(3, 12, budget);
        var catalogue = Enumerable.Range(0, 48).Select(i => 400 + (i * 7919) % 3600).Distinct().ToArray();
        var reads = new List<(long Bytes, long Allocations)>();
        for (int cycle = 0; cycle < 6; cycle++)
        {
            int seen = Math.Min(catalogue.Length, 8 * (cycle + 1));
            for (int body = 0; body < 40; body++)
                for (int part = 0; part < 4; part++)
                {
                    int length = catalogue[(body * 5 + part * 11 + cycle) % seen];
                    var arrays = scratch.Take(length);
                    Assert.Equal(3, arrays.Length);
                    Assert.All(arrays, a => Assert.Equal(length, a.Length));
                    Assert.True(scratch.Bytes <= budget || scratch.Count == 1, "bake scratch " + scratch.Bytes + " B over its budget");
                }
            reads.Add((scratch.Bytes, scratch.Allocations));
        }
        output.WriteLine("bake scratch bytes / allocations at the end of 6 cycles, " + catalogue.Length + " body part lengths, budget " + budget + " B: "
            + string.Join(", ", reads.Select(r => r.Bytes + " / " + r.Allocations)) + "; evictions " + scratch.Evictions);
        Assert.All(reads, r => Assert.True(r.Bytes <= budget));
        long unbounded = catalogue.Sum(length => scratch.CostOf(length));
        Assert.True(unbounded > budget, "the catalogue " + unbounded + " B must exceed the budget for this test to bound anything");

        var repeat = new LengthScratch<(float X, float Y, float Z)>(3, 12, budget);
        var looks = catalogue.Take(12).ToArray();
        foreach (int length in looks) repeat.Take(length);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int round = 0; round < 50; round++) foreach (int length in looks) repeat.Take(length);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.Equal(looks.Length, repeat.Allocations);
        Assert.Equal(0, repeat.Evictions);
    }

    [Fact]
    public void FiveOneTwoOneRetimesAtTheRigSpacingRetainNoMoreThanAnUnretimedControl()
    {
        const int cycles = 5;
        const double spacing = 10;
        (CityState State, CitySimulator City) Build()
        {
            var state = Gr5AllocationMeasure.RigCity();
            var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
            return (state, city);
        }
        var retimed = Build(); var control = Build();
        var driver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        var split = new Dictionary<CitizenState, (Route Journey, int Planned, int Added)>();
        int retimes = 0, retimedJourneys = 0, maxAddedPerRetime = 0, maxHeldPerJourney = 0;
        CitySimulator Retime(double rate)
        {
            var before = state.Citizens.Concat(state.VisitorRoutines).Where(p => p.Journey != null)
                .Select(p => (Person: p, Journey: p.Journey!, Legs: p.Journey!.Legs.Count)).ToList();
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            var result = rebuild.TakeResult();
            retimes++;
            foreach (var old in before)
            {
                var person = old.Person;
                if (person.Journey == null || ReferenceEquals(person.Journey, old.Journey)) continue;
                int added = person.Journey.Legs.Count - old.Legs;
                Assert.True(added <= 1, "retime " + retimes + " gave " + person.Identity.Id + " " + added + " extra legs; only the active leg may split");
                retimedJourneys++; maxAddedPerRetime = Math.Max(maxAddedPerRetime, added);
                var tracked = split.TryGetValue(person, out var known) && ReferenceEquals(known.Journey, old.Journey) ? known : (old.Journey, old.Legs, 0);
                split[person] = (person.Journey, tracked.Item2, tracked.Item3 + added);
                maxHeldPerJourney = Math.Max(maxHeldPerJourney, tracked.Item3 + added);
            }
            return result;
        }
        long HeldSplits()
        {
            long held = 0;
            foreach (var person in split.Keys.ToList())
            {
                var entry = split[person];
                if (!ReferenceEquals(person.Journey, entry.Journey)) { split.Remove(person); continue; }
                Assert.Equal(entry.Planned + entry.Added, person.Journey!.Legs.Count);
                held += entry.Added;
            }
            return held;
        }
        var retimedReads = new List<RetainedHeap> { Census(("simulation", city), ("driver", driver)) };
        var controlReads = new List<RetainedHeap> { Census(("simulation", control.City), ("driver", new ClockRetimeDriver())) };
        var instrument = new List<long> { InstrumentBytes() };
        var travelling = new List<int> { Travelling(city) };
        var retimedOwned = new List<long> { OwnedLegs(state) };
        var controlOwned = new List<long> { OwnedLegs(control.State) };
        var held = new List<long> { 0 };
        var retimedArrays = new List<RetainedCensus.JourneyLegLedger> { RetainedCensus.JourneyLegs(state) };
        var controlArrays = new List<RetainedCensus.JourneyLegLedger> { RetainedCensus.JourneyLegs(control.State) };
        double start = state.Minute;
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = start + cycle * spacing;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            Assert.Equal(end, control.State.Minute);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            var walk = new RetainedCensus();
            walk.Add("simulation", city);
            Assert.Equal(1, walk.ObjectsOf(typeof(CitySimulator)));
            instrument.Add(InstrumentBytes());
            travelling.Add(Travelling(city));
            retimedOwned.Add(OwnedLegs(state));
            controlOwned.Add(OwnedLegs(control.State));
            held.Add(HeldSplits());
            retimedArrays.Add(RetainedCensus.JourneyLegs(state));
            controlArrays.Add(RetainedCensus.JourneyLegs(control.State));
            retimedReads.Add(Census(("simulation", city), ("driver", driver)));
            controlReads.Add(Census(("simulation", control.City), ("driver", new ClockRetimeDriver())));
        }
        var settled = retimedReads.Zip(controlReads, (r, c) => Settled(r) - Settled(c)).ToList();
        output.WriteLine("rig food city, five 1->2->1 retimes " + spacing + " game minutes apart from 15:30 (the 16t L spacing), Mono 64-bit layout model:");
        for (int i = 0; i <= cycles; i++)
            output.WriteLine("  R" + i + " retimed " + retimedReads[i].Bytes + " B / control " + controlReads[i].Bytes + " B / excess without journey legs " + settled[i].ToString("+0;-0")
                + " B; RouteLeg " + Objects(retimedReads[i], "RouteLeg") + "/" + Objects(controlReads[i], "RouteLeg") + ", String " + Objects(retimedReads[i], "String") + "/" + Objects(controlReads[i], "String")
                + "; census instrument " + instrument[i] + " B");
        Assert.Equal(0, settled[0]);
        Assert.All(instrument, b => Assert.Equal(0, b));
        Assert.All(retimedReads, heap => Assert.Equal(1, Objects(heap, "CitySimulator")));
        long first = settled[1], plateau = settled.Skip(1).Max();
        Assert.True(plateau - first < PlateauAllowance, "excess without journey legs rose by " + (plateau - first) + " B after R1: " + string.Join(", ", settled));
        bool rising = true;
        for (int i = 2; i < settled.Count; i++) rising &= settled[i] > settled[i - 1];
        Assert.False(rising, "excess without journey legs rose at every checkpoint: " + string.Join(", ", settled));
        Assert.All(settled, e => Assert.True(e < Allowance, "retimed minus control " + e + " B"));
        for (int i = 0; i <= cycles; i++)
        {
            Assert.Equal(Objects(retimedReads[i], "RouteLeg"), retimedOwned[i]);
            Assert.Equal(Objects(controlReads[i], "RouteLeg"), controlOwned[i]);
            output.WriteLine("  R" + i + " RouteLeg retimed / control " + retimedOwned[i] + " / " + controlOwned[i] + ", every one owned by a current journey; legs added by retime splits and still held " + held[i]);
            long excess = retimedOwned[i] - controlOwned[i];
            Assert.True(Math.Abs(excess - held[i]) <= 2, "R" + i + " RouteLeg excess " + excess + " is not explained by the " + held[i] + " split legs held by live journeys");
            Assert.True(held[i] <= travelling[i], "R" + i + " split legs held " + held[i] + " exceed one per travelling journey (" + travelling[i] + ")");
        }
        output.WriteLine("  most split legs held by one journey across all retimes: " + maxHeldPerJourney);
        Assert.True(maxHeldPerJourney <= 1, "a journey held " + maxHeldPerJourney + " split legs; repeated retimes must not add a leg per retime");
        var inFlight = state.Citizens.Concat(state.VisitorRoutines).Where(c => c.Phase == CitizenPhase.Travelling).ToList();
        double drainedAt = (inFlight.Count == 0 ? state.Minute : inFlight.Max(c => c.NextEventMinute)) + 0.01;
        city.AdvanceTo(drainedAt, int.MaxValue); control.City.AdvanceTo(drainedAt, int.MaxValue);
        var drained = Census(("simulation", city), ("driver", driver));
        var drainedControl = Census(("simulation", control.City), ("driver", new ClockRetimeDriver()));
        retimedReads.Add(drained); controlReads.Add(drainedControl);
        retimedArrays.Add(RetainedCensus.JourneyLegs(state)); controlArrays.Add(RetainedCensus.JourneyLegs(control.State));
        output.WriteLine("  RouteLeg[] bytes by owner, retimed / control / gap (Mono 64-bit layout model; journey arrays from the census journey ledger):");
        var otherGaps = new List<long>();
        for (int i = 0; i < retimedReads.Count; i++)
        {
            string label = i <= cycles ? "R" + i : "drained";
            long retimedTotal = Bytes(retimedReads[i], "RouteLeg[]"), controlTotal = Bytes(controlReads[i], "RouteLeg[]");
            long retimedJourney = retimedArrays[i].LegArrayBytes, controlJourney = controlArrays[i].LegArrayBytes;
            long retimedOther = retimedTotal - retimedJourney, controlOther = controlTotal - controlJourney;
            otherGaps.Add(retimedOther - controlOther);
            output.WriteLine("    " + label + " all " + Objects(retimedReads[i], "RouteLeg[]") + " / " + Objects(controlReads[i], "RouteLeg[]") + " arrays, " + retimedTotal + " / " + controlTotal + " / " + (retimedTotal - controlTotal).ToString("+0;-0;0")
                + " B; current journeys " + retimedArrays[i].LegArrays + " / " + controlArrays[i].LegArrays + " arrays, " + retimedJourney + " / " + controlJourney + " / " + (retimedJourney - controlJourney).ToString("+0;-0;0")
                + " B; other owners " + retimedOther + " / " + controlOther + " / " + (retimedOther - controlOther).ToString("+0;-0;0") + " B; live legs / backing slots in current journeys "
                + retimedArrays[i].Legs + " / " + retimedArrays[i].LegArraySlots + " vs " + controlArrays[i].Legs + " / " + controlArrays[i].LegArraySlots);
            Assert.True(retimedOther >= 0 && controlOther >= 0, label + " journey arrays exceed the census RouteLeg[] bytes");
        }
        Assert.All(otherGaps, gap => Assert.Equal(0, gap));
        var rawGaps = retimedReads.Zip(controlReads, (r, c) => Bytes(r, "RouteLeg[]") - Bytes(c, "RouteLeg[]")).ToList();
        var finalThree = rawGaps.Skip(cycles - 2).Take(3).ToList();
        output.WriteLine("  raw RouteLeg[] (the straight planner keeps no route cache, so every array is outside caches), retimed minus control at R0..R" + cycles + " and drained: " + string.Join(" / ", rawGaps.Select(g => g.ToString("+0;-0;0"))) + " B");
        Assert.False(finalThree[1] > finalThree[0] && finalThree[2] > finalThree[1], "raw RouteLeg[] gap rose at each of the final three retime cycles: " + string.Join(", ", finalThree));
        Assert.True(rawGaps[rawGaps.Count - 1] <= Math.Max(0, rawGaps.Take(cycles + 1).Max()), "drained raw RouteLeg[] gap " + rawGaps[rawGaps.Count - 1] + " B is above every earlier read: " + string.Join(", ", rawGaps));
        Assert.All(rawGaps, gap => Assert.True(Math.Abs(gap) < PlateauAllowance, "raw RouteLeg[] gap " + gap + " B"));
        retimedReads.RemoveAt(retimedReads.Count - 1); controlReads.RemoveAt(controlReads.Count - 1);
        output.WriteLine("  drained at minute " + drainedAt.ToString("0.00") + ": RouteLeg " + Objects(drained, "RouteLeg") + "/" + Objects(drainedControl, "RouteLeg")
            + ", retimed minus control " + (drained.Bytes - drainedControl.Bytes).ToString("+0;-0") + " B");
        Assert.True(drained.Bytes - drainedControl.Bytes < Allowance, "drained retimed minus control " + (drained.Bytes - drainedControl.Bytes) + " B");
        Assert.Equal(Objects(drained, "RouteLeg"), OwnedLegs(state));
        output.WriteLine("  " + retimes + " retimes replaced " + retimedJourneys + " journeys, at most " + maxAddedPerRetime + " added leg per journey per retime; split legs still held after drain " + HeldSplits());
        Assert.Equal(0, HeldSplits());
        Assert.Equal(Objects(drainedControl, "RouteLeg"), OwnedLegs(control.State));
        Assert.True(Objects(drained, "RouteLeg") - Objects(drainedControl, "RouteLeg") <= inFlight.Count + 2,
            "drained RouteLeg " + Objects(drained, "RouteLeg") + " vs control " + Objects(drainedControl, "RouteLeg") + " with " + inFlight.Count + " journeys in flight at R" + cycles);
    }

    [Fact]
    public void PrePositioningTheRigFoodCityToNightAtSixtyTimesRetainsNoMoreThanAtTwentyTimes()
    {
        RetainedHeap Run(double rate)
        {
            var state = Gr5AllocationMeasure.RigCity();
            var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            city.AdvanceTo(day + 15.5 * 60, int.MaxValue);
            var driver = new ClockRetimeDriver();
            CitySimulator Retime(double to)
            {
                while (driver.Update(state, to, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
                var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
                while (!rebuild.Step(0)) { }
                return rebuild.TakeResult();
            }
            if (rate != 1) city = Retime(rate);
            city.AdvanceTo(day + 1440 + 50, int.MaxValue);
            if (rate != 1) city = Retime(1);
            city.AdvanceTo(day + 1440 + 60, int.MaxValue);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            return Census(("simulation", city), ("driver", driver));
        }
        var one = Run(1); var twenty = Run(20); var sixty = Run(60);
        output.WriteLine("rig food city at 01:00 after 15:30 -> 00:50 pre-positioning (Mono 64-bit layout model): 1x " + one.Bytes + " B / " + one.Objects
            + " objects, 20x " + twenty.Bytes + " B / " + twenty.Objects + " objects, 60x " + sixty.Bytes + " B / " + sixty.Objects + " objects; 60x minus 20x " + (sixty.Bytes - twenty.Bytes).ToString("+0;-0") + " B");
        foreach (var row in sixty.Types.Take(12))
        {
            var other = twenty.Types.FirstOrDefault(r => r.Name == row.Name);
            output.WriteLine("  " + row.Name + ": 20x " + (other?.Objects ?? 0) + " / 60x " + row.Objects + " objects, " + ((row.Bytes - (other?.Bytes ?? 0)) / 1e6).ToString("+0.000;-0.000") + " MB");
        }
        Assert.True(Math.Abs(sixty.Bytes - twenty.Bytes) < Allowance, "60x minus 20x " + (sixty.Bytes - twenty.Bytes) + " B");
        Assert.True(sixty.Bytes - one.Bytes < Allowance, "60x minus 1x " + (sixty.Bytes - one.Bytes) + " B");
        Assert.Equal(1, Objects(sixty, "CitySimulator"));
    }

    internal sealed class NightPlanner : IResumableRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner
    {
        private const int CacheLimit = 1024, Side = 49;
        private const double Spacing = 50, WalkMetresPerSecond = 1.4;
        private const string Native = "3700000000000000000000000000000000000000000000000000000000000037";
        private static readonly TravelPreferences Checked = new TravelPreferences { OwnsCar = true, AllowTransit = true, WalkingToleranceMetres = 10_000_000 };
        private static readonly Position[] Stations = { new Position(400, 0, 400), new Position(2000, 0, 400), new Position(400, 0, 2000), new Position(2000, 0, 2000) };
        private readonly bool head;
        private readonly RouteCandidates candidates = new RouteCandidates();
        private readonly Dictionary<(Position, Position), Route> cache = new Dictionary<(Position, Position), Route>();
        private readonly Queue<(Position, Position)> order = new Queue<(Position, Position)>();
        private readonly Dictionary<(Position, Position), Route> trains = new Dictionary<(Position, Position), Route>();
        private readonly TrafficNetwork grid;
        private readonly Dictionary<double, TrafficNetwork> rated = new Dictionary<double, TrafficNetwork>();
        public long Requests, SubwayValid, CarValid, CarChosen;
        public TrafficNetwork? TrafficNetwork { get; private set; }

        public NightPlanner(bool head)
        {
            this.head = head;
            var nodes = new List<TrafficNetworkNode>();
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++)
                {
                    var next = new List<int>();
                    if (x > 0) next.Add(Node(x - 1, y)); if (x + 1 < Side) next.Add(Node(x + 1, y));
                    if (y > 0) next.Add(Node(x, y - 1)); if (y + 1 < Side) next.Add(Node(x, y + 1));
                    nodes.Add(new TrafficNetworkNode { Id = Node(x, y), Position = At(Node(x, y)), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = next.ToArray() });
                }
            grid = new TrafficNetwork(Native, nodes, 1);
            Rate(1);
        }

        private double rate = 1;

        public void Rate(double rate)
        {
            if (!rated.TryGetValue(rate, out var network)) rated.Add(rate, network = grid.Rerated(rate, _ => null));
            TrafficNetwork = network;
            if (rate == this.rate) return;
            this.rate = rate; cache.Clear(); order.Clear(); trains.Clear();
        }

        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;

        private static int Node(int x, int y) => y * Side + x;
        private static Position At(int node) => new Position(node % Side * Spacing, 0, node / Side * Spacing);
        internal static Position Curb(Position p) => At(NodeOf(p));
        private static int NodeOf(Position p) => Node(Math.Clamp((int)Math.Round(p.X / Spacing), 0, Side - 1), Math.Clamp((int)Math.Round(p.Z / Spacing), 0, Side - 1));

        private static Route Line(Position from, Position to, int legs, TravelMode mode, string service, double minutes)
        {
            var route = new Route();
            if (Position.Distance(from, to) < 0.001) legs = 1;
            for (int i = 0; i < legs; i++)
                route.Legs.Add(new RouteLeg { From = Position.Lerp(from, to, (double)i / legs), To = i == legs - 1 ? to : Position.Lerp(from, to, (double)(i + 1) / legs),
                    Mode = mode, ServiceId = service, Minutes = minutes / legs });
            return route;
        }

        private Route Cached(Position from, Position to)
        {
            if (!cache.TryGetValue((from, to), out var route))
            {
                if (cache.Count >= CacheLimit) cache.Remove(order.Dequeue());
                route = TransportComposition.Shared(Line(from, to, 6, TravelMode.Walk, "", Math.Max(0.5, Position.Distance(from, to) / TravelTiming.WalkingMetresPerMinute(WalkMetresPerSecond, rate))));
                cache.Add((from, to), route); order.Enqueue((from, to));
            }
            return route;
        }

        private Route Walk(Position from, Position to) => head ? Cached(from, to) : new Route { Legs = new RouteLegs(Cached(from, to).Legs) };

        private Route Train(Position from, Position to)
        {
            if (!trains.TryGetValue((from, to), out var route))
                trains.Add((from, to), route = TransportComposition.Shared(Line(from, to, 4, TravelMode.Subway, "native:subway", rate * (90 + Position.Distance(from, to) / 600))));
            return route;
        }

        private static Route Pause(Position at)
        {
            var route = new Route { Legs = new RouteLegs(1) };
            route.Legs.Add(new RouteLeg { From = at, To = at, Mode = TravelMode.Car, Minutes = TransportComposition.CarPreparationMinutes });
            return TransportComposition.Shared(route);
        }

        private Route Road(int a, int b)
        {
            var route = new Route();
            int x = a % Side, y = a / Side, tx = b % Side, ty = b / Side, at = a;
            while (at != b)
            {
                if (x != tx) x += Math.Sign(tx - x); else y += Math.Sign(ty - y);
                int next = Node(x, y);
                TrafficNetwork!.TryGetEdge(at, next, out var edge);
                route.Legs.Add(new RouteLeg { From = At(at), To = At(next), Mode = TravelMode.Car, Minutes = edge.FreeFlowMinutes, ServiceId = "native:road:v1:" + Native + ":" + at + ":" + next });
                at = next;
            }
            return route;
        }

        private static Position Nearest(Position at)
        {
            var best = Stations[0];
            foreach (var station in Stations) if (Position.Distance(at, station) < Position.Distance(at, best)) best = station;
            return best;
        }

        private Route? best;
        private double bestScore;
        private Position from, to;
        private Position? parked;

        private void Consider(Route? route)
        {
            if (head) { candidates.Consider(route, from, to, Checked, parked); return; }
            if (route == null || !TransportComposition.Valid(route, from, to, Checked, parked)) return;
            if (!TransportComposition.IsFaster(route.EstimatedMinutes, bestScore)) return;
            best = route; bestScore = route.EstimatedMinutes;
        }

        public Route? Plan(Place origin, Place destination, double minute, TravelPreferences preferences) => Plan(origin, destination, null);

        private Route? Plan(Place origin, Place destination, CitizenState? citizen)
        {
            Requests++;
            from = origin.Position; to = destination.Position; best = null; bestScore = double.PositiveInfinity;
            var car = citizen != null && citizen.Preferences.OwnsCar ? citizen.Car : null;
            parked = car?.ParkedPosition;
            if (head) candidates.Reset();
            Consider(Walk(from, to));
            Position start = Nearest(from), end = Nearest(to);
            if (!start.Equals(end))
            {
                var origin1 = Walk(from, start); var train = Train(start, end); var back = Walk(end, to);
                if (head)
                {
                    if (TransportComposition.TryJoinedScore(origin1, train, back, from, to, Checked, parked, out _)) SubwayValid++;
                    candidates.ConsiderJoined(origin1, train, back, from, to, Checked, parked);
                }
                else
                {
                    var joined = TransportComposition.Join(new[] { origin1, train, back });
                    if (joined != null && TransportComposition.Valid(joined, from, to, Checked, parked)) SubwayValid++;
                    Consider(joined);
                }
            }
            if (car != null)
            {
                int a = NodeOf(car.ParkedPosition), b = NodeOf(to);
                if (a != b)
                {
                    var access = Walk(from, car.ParkedPosition); var road = Road(a, b); var egress = Walk(At(b), to);
                    var drive = head ? TransportComposition.Join(access, Pause(car.ParkedPosition), road, Pause(At(b)), egress)
                        : TransportComposition.Join(new[] { access, Pause(car.ParkedPosition), road, Pause(At(b)), egress });
                    if (drive != null && TransportComposition.Valid(drive, from, to, Checked, parked)) CarValid++;
                    Consider(drive);
                }
            }
            var chosen = head ? candidates.Publish() : best;
            if (head) candidates.Reset();
            best = null; parked = null;
            if (chosen != null) foreach (var leg in chosen.Legs) if (leg.Mode == TravelMode.Car) { CarChosen++; break; }
            return chosen;
        }

        public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);
        private sealed class Job : IRoutePlanningJob
        {
            private readonly NightPlanner owner; private readonly RouteRequest request; private int slices;
            public Job(NightPlanner owner, RouteRequest request) { this.owner = owner; this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                if (++slices < 2 && !double.IsPositiveInfinity(budgetMilliseconds)) return false;
                Result = owner.Plan(request.From, request.To, request.Citizen);
                return true;
            }
            public void Dispose() { }
        }
    }

    private sealed class NightArm
    {
        public RetainedHeap ArmBefore = null!, ArmAfter = null!, SamplerStart = null!, SamplerStop = null!;
        public long RunToArmAfter, ArmAfterToSamplerStart, SamplerWindow;
        public int CommitsToArmAfter, CommitsBeforeSampler, CommitsInWindow, Vehicles, MostVehicles;
        public long Requests, SubwayValid, CarValid, CarChosen;
        public int OffsetsLength = -1;
        public string Hash = "";
    }

    private enum NightScenario { Recorded20, Recorded60, Hypothetical60 }

    private static readonly string[] RouteTypes = { "Route", "RouteLegs", "List<RouteLeg>", "RouteLeg[]", "RouteLeg", "RouteCandidates", "TrafficVehicleState", "TrafficRouteLeg", "Double[]" };

    private static NightArm NightProtocol(NightScenario scenario, bool head, double samplerMinutes)
    {
        NightArm? arm = null;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() => { try { arm = RunNight(scenario, head, samplerMinutes); } catch (Exception error) { failure = error; } }, 64 * 1024 * 1024);
        thread.Start(); thread.Join();
        if (failure != null) throw new InvalidOperationException(scenario + (head ? " HEAD" : " base"), failure);
        return arm!;
    }

    private static NightArm RunNight(NightScenario scenario, bool head, double samplerMinutes)
    {
        var offsets = typeof(TrafficNetwork).GetField("validationOffsets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var arm = new NightArm();
        var planner = new NightPlanner(head);
        var state = Gr5AllocationMeasure.RigCity();
        var homes = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var person in state.Citizens)
            if (person.Preferences.OwnsCar) person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = NightPlanner.Curb(homes[person.HomeId].Position) };
        var city = new CitySimulator(state, planner);
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
        city.AdvanceTo(day + 15.5 * 60, int.MaxValue);
        var driver = new ClockRetimeDriver();
        CitySimulator Retime(double to)
        {
            while (driver.Update(state, to, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            planner.Rate(to);
            var rebuild = city.BeginRebuildRoutes(planner, driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            var result = rebuild.TakeResult();
            arm.Vehicles = result.Traffic?.VehicleCount ?? 0;
            arm.MostVehicles = Math.Max(arm.MostVehicles, arm.Vehicles);
            return result;
        }
        RetainedHeap Read()
        {
            var roots = new List<(string, object)> { ("simulation", city), ("driver", driver), ("planner", planner) };
            if (offsets.GetValue(null) is double[] buffer) roots.Add(("validationOffsets", buffer));
            return Census(roots.ToArray());
        }
        if (scenario != NightScenario.Recorded60) city = Retime(scenario == NightScenario.Recorded20 ? 20 : 60);
        city.AdvanceTo(day + 1440 + 50, int.MaxValue);
        arm.ArmBefore = Read();
        long started = GC.GetAllocatedBytesForCurrentThread();
        if (scenario == NightScenario.Recorded20) { city = Retime(1); city = Retime(20); arm.CommitsToArmAfter += 2; }
        city.AdvanceTo(day + 1440 + 60, int.MaxValue);
        arm.RunToArmAfter = GC.GetAllocatedBytesForCurrentThread() - started;
        arm.ArmAfter = Read();
        started = GC.GetAllocatedBytesForCurrentThread();
        if (scenario == NightScenario.Hypothetical60) { city = Retime(1); arm.CommitsBeforeSampler++; }
        arm.ArmAfterToSamplerStart = GC.GetAllocatedBytesForCurrentThread() - started;
        Assert.True(city.ActiveJourneyIndexMatchesState());
        arm.SamplerStart = Read();
        started = GC.GetAllocatedBytesForCurrentThread();
        if (scenario == NightScenario.Recorded20) { city = Retime(1); arm.CommitsInWindow++; }
        city.AdvanceTo(day + 1440 + 60 + samplerMinutes, int.MaxValue);
        arm.SamplerWindow = GC.GetAllocatedBytesForCurrentThread() - started;
        Assert.True(city.ActiveJourneyIndexMatchesState());
        arm.SamplerStop = Read();
        return Finish();
        NightArm Finish()
        {
            arm.Requests = planner.Requests; arm.SubwayValid = planner.SubwayValid; arm.CarValid = planner.CarValid; arm.CarChosen = planner.CarChosen;
            arm.OffsetsLength = offsets.GetValue(null) is double[] used ? used.Length : -1;
            state.PrepareForSave();
            arm.Hash = RushSpikeReplay.Sha(SaveCodec.Write(state));
            return arm;
        }
    }

    [Fact]
    public void TheRigNightProtocolReportsAllocationAndRetainedGraphAtEachSamplerBoundary()
    {
        const double SamplerMinutes = 200;
        string Line(RetainedHeap heap) => heap.Bytes + " B / " + heap.Objects + " objects / " + heap.References + " refs";
        string Routes(RetainedHeap heap) => string.Join(", ", RouteTypes.Select(t => t + " " + Objects(heap, t)));
        output.WriteLine("rig food city with cars (35% owners, parked at the road node nearest home); commit order from the archived rig 17c and 17h night Player.logs; " + SamplerMinutes + " game minutes at 1x in the sampler window. "
            + "Recorded20: native clock speed 1 to 15:30, speed 20 to 00:50, speed 20 to 01:00, each arrival pausing at speed 1; committed mod travel rate 20x from 15:30, 00:50 arm-before, commits to 1x and back to 20x, 01:00 arm-after at 20x, no commit before sampler start, one commit to 1x inside the window. "
            + "Recorded60: native clock speed 1 to 15:30, speed 60 to 00:50, speed 60 to 01:00, each arrival pausing at speed 1; the mod reads 1x throughout (17h food reach 0.333 game minutes per second, only the load-time commit), so the committed travel rate stays 1x at 15:30, 00:50, 01:00 and sampler start and no interval has a commit. "
            + "Hypothetical60 (not in the log): 15:30 commit to 60x and a 1x commit between arm-after and sampler start. "
            + "Deterministic planner: cached shared 6-leg walks (1,024-entry FIFO), 4 stations, a fresh mapped car route with two parking pauses on a 49x49 road grid (50 m links); "
            + "every commit switches the planner to the network rated for the new travel clock rate and drops the planner caches, as the runtime does with a new planner, so the rebuild validates every live vehicle against that network. "
            + "Rated networks are kept per exact rate: the 1x network is built with the planner and the 20x or 60x network at the 15:30 commit, so every commit inside the measured intervals reuses a network built before arm-before. "
            + "The measured intervals therefore include the rebuild's traffic mapping validation and leave out the allocation of reconstructing a rated network. "
            + "base forms = copy per walk, array Join + Valid; HEAD forms = RouteCandidates, TryJoinedScore, fixed-arity Join. Each arm runs on a fresh thread. "
            + "Retained: Mono 64-bit layout model, incl. the thread's validation offsets buffer; allocation: .NET.");
        var arms = new List<(NightScenario Scenario, NightArm Base, NightArm Head)>();
        foreach (var scenario in new[] { NightScenario.Recorded20, NightScenario.Recorded60, NightScenario.Hypothetical60 })
            arms.Add((scenario, NightProtocol(scenario, false, SamplerMinutes), NightProtocol(scenario, true, SamplerMinutes)));
        foreach (var (scenario, before, after) in arms)
        {
            foreach (var (label, arm) in new[] { ("base forms", before), ("HEAD forms", after) })
            {
                output.WriteLine("  " + scenario + " " + label + ": arm-before " + Line(arm.ArmBefore) + "; arm-after " + Line(arm.ArmAfter) + "; sampler start " + Line(arm.SamplerStart) + "; sampler stop " + Line(arm.SamplerStop));
                output.WriteLine("    allocated 00:50 -> arm-after " + arm.RunToArmAfter + " B (" + arm.CommitsToArmAfter + " commits), arm-after -> sampler start " + arm.ArmAfterToSamplerStart + " B (" + arm.CommitsBeforeSampler
                    + " commits), sampler window " + arm.SamplerWindow + " B (" + arm.CommitsInWindow + " commits); " + arm.Requests + " planner requests, " + arm.SubwayValid + " valid subway candidates, "
                    + arm.CarValid + " valid car candidates, " + arm.CarChosen + " car routes chosen; live vehicles validated at the last commit " + arm.Vehicles + " (most " + arm.MostVehicles + "); validation offsets length " + arm.OffsetsLength + "; save SHA-256 " + arm.Hash.Substring(0, 12));
                foreach (var (boundary, heap) in new[] { ("arm-before", arm.ArmBefore), ("arm-after", arm.ArmAfter), ("sampler start", arm.SamplerStart), ("sampler stop", arm.SamplerStop) })
                    output.WriteLine("    " + boundary + " objects: " + Routes(heap) + "; Double[] " + Bytes(heap, "Double[]") + " B");
                Assert.Equal(1, Objects(arm.ArmAfter, "CitySimulator"));
                Assert.Equal(1, Objects(arm.SamplerStart, "CitySimulator"));
                Assert.Equal(1, Objects(arm.SamplerStop, "CitySimulator"));
                Assert.True(arm.CarChosen > 0 && arm.SubwayValid > 0, scenario + " " + label + " cars " + arm.CarChosen + " / subway " + arm.SubwayValid);
            }
            output.WriteLine("    HEAD minus base forms: allocated 00:50 -> arm-after " + (after.RunToArmAfter - before.RunToArmAfter).ToString("+0;-0;0") + " B, arm-after -> sampler start "
                + (after.ArmAfterToSamplerStart - before.ArmAfterToSamplerStart).ToString("+0;-0;0") + " B, sampler window " + (after.SamplerWindow - before.SamplerWindow).ToString("+0;-0;0")
                + " B; retained at arm-after " + (after.ArmAfter.Bytes - before.ArmAfter.Bytes).ToString("+0;-0;0") + " B, at sampler start " + (after.SamplerStart.Bytes - before.SamplerStart.Bytes).ToString("+0;-0;0")
                + " B, at sampler stop " + (after.SamplerStop.Bytes - before.SamplerStop.Bytes).ToString("+0;-0;0") + " B");
            Assert.Equal(before.Hash, after.Hash);
            Assert.Equal(before.Requests, after.Requests);
            Assert.Equal(before.CarChosen, after.CarChosen);
            Assert.True(after.SamplerWindow < before.SamplerWindow, scenario + " window " + before.SamplerWindow + " -> " + after.SamplerWindow);
            Assert.True(Math.Abs(after.SamplerStop.Bytes - before.SamplerStop.Bytes) < Allowance, scenario + " retained at stop " + before.SamplerStop.Bytes + " -> " + after.SamplerStop.Bytes);
            if (scenario != NightScenario.Recorded60)
                Assert.True(after.MostVehicles > 0 && after.OffsetsLength > 0 && before.MostVehicles == after.MostVehicles, scenario + " validated vehicles " + before.MostVehicles + " / " + after.MostVehicles + ", offsets " + after.OffsetsLength);
        }
    }
}
