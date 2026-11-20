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
