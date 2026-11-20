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

    [Fact]
    public void TheProductionBakeScratchPlateausAtTheLookCatalogueAcrossAnLRampAndThenStopsAllocating()
    {
        const long plateau16t = 1_718_964;
        var scratch = BlendshapeBakePlan.NewScratch<(float X, float Y, float Z)>();
        var raw = Enumerable.Range(0, 24).Select(i => 500 + (i * 7919) % 3500).Distinct().ToArray();
        long vertices = plateau16t / (BlendshapeBakePlan.ScratchWidth * BlendshapeBakePlan.ScratchElementBytes);
        double scale = (double)vertices / raw.Sum();
        var catalogue = raw.Select(v => Math.Max(64, (int)(v * scale))).Distinct().ToArray();
        long catalogueBytes = catalogue.Sum(length => scratch.CostOf(length));
        Assert.InRange(catalogueBytes, plateau16t * 9 / 10, plateau16t * 11 / 10);
        int[] bodiesAt = { 8, 9, 12, 16, 24, 31, 63 };
        int Part(int body, int part) => catalogue[(int)(((uint)(body * 2654435761u) >> 7) + (uint)(part * 40503)) % catalogue.Length];
        void Spawn(int bodies) { for (int body = 0; body < bodies; body++) for (int part = 0; part < 5; part++) scratch.Take(Part(body, part)); }
        var ramp = new List<long>();
        foreach (int bodies in bodiesAt) { Spawn(bodies); ramp.Add(scratch.Bytes); }
        for (int length = 0; length < catalogue.Length; length++) scratch.Take(catalogue[length]);
        long allocations = scratch.Allocations, plateau = scratch.Bytes;
        var matched = new List<long>(8);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int cycle = 0; cycle < 5; cycle++) { Spawn(63); for (int length = 0; length < catalogue.Length; length++) scratch.Take(catalogue[length]); matched.Add(scratch.Bytes); }
        long churn = GC.GetAllocatedBytesForCurrentThread() - allocated;
        output.WriteLine("production bake scratch (budget " + BlendshapeBakePlan.ScratchBudgetBytes + " B), " + catalogue.Length + " body part lengths, catalogue " + catalogueBytes
            + " B: L ramp R0..+30 min " + string.Join(", ", ramp) + "; after every look " + plateau + " B; 5 matched cycles " + string.Join(", ", matched)
            + "; allocations " + allocations + ", evictions " + scratch.Evictions + ", bytes allocated in the matched cycles " + churn);
        for (int i = 1; i < ramp.Count; i++) Assert.True(ramp[i] >= ramp[i - 1]);
        Assert.All(ramp, b => Assert.True(b <= catalogueBytes));
        Assert.Equal(catalogueBytes, plateau);
        Assert.All(matched, b => Assert.Equal(plateau, b));
        Assert.Equal(catalogue.Length, allocations);
        Assert.Equal(allocations, scratch.Allocations);
        Assert.Equal(0, scratch.Evictions);
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
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        var retimedReads = new List<RetainedHeap> { Census(("simulation", city), ("driver", driver)) };
        var controlReads = new List<RetainedHeap> { Census(("simulation", control.City), ("driver", new ClockRetimeDriver())) };
        var instrument = new List<long> { InstrumentBytes() };
        var travelling = new List<int> { Travelling(city) };
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
        for (int i = 1; i <= cycles; i++)
        {
            long extra = Objects(retimedReads[i], "RouteLeg") - Objects(controlReads[i], "RouteLeg");
            Assert.True(extra <= 2L * i * Math.Max(1, travelling.Take(i + 1).Max()), "extra RouteLeg at R" + i + " " + extra + " exceeds one per travelling journey per retime");
        }
        var inFlight = state.Citizens.Concat(state.VisitorRoutines).Where(c => c.Phase == CitizenPhase.Travelling).ToList();
        double drainedAt = (inFlight.Count == 0 ? state.Minute : inFlight.Max(c => c.NextEventMinute)) + 0.01;
        city.AdvanceTo(drainedAt, int.MaxValue); control.City.AdvanceTo(drainedAt, int.MaxValue);
        var drained = Census(("simulation", city), ("driver", driver));
        var drainedControl = Census(("simulation", control.City), ("driver", new ClockRetimeDriver()));
        output.WriteLine("  drained at minute " + drainedAt.ToString("0.00") + ": RouteLeg " + Objects(drained, "RouteLeg") + "/" + Objects(drainedControl, "RouteLeg")
            + ", retimed minus control " + (drained.Bytes - drainedControl.Bytes).ToString("+0;-0") + " B");
        Assert.True(drained.Bytes - drainedControl.Bytes < Allowance, "drained retimed minus control " + (drained.Bytes - drainedControl.Bytes) + " B");
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
}
