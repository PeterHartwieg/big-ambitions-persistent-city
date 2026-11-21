using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RetainedRouteLegTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetainedRouteLegTests(ITestOutputHelper output) { this.output = output; }

    private static readonly Type[] Tracked = { typeof(RouteLeg), typeof(RouteLeg[]), typeof(List<RouteLeg>), typeof(RouteLegs), typeof(Route), typeof(Position[]),
        typeof(double[]), typeof(long[]), typeof(CitizenState), typeof(TrafficVehicleState), typeof(CitySimulator) };

    private readonly struct Read
    {
        public Read(JsonElement root)
        {
            var types = root.GetProperty("tracked_types").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("objects").GetInt64());
            Types = types;
            var legs = root.GetProperty("route_legs");
            Retained = legs.GetProperty("retained").GetInt64(); InJourneys = legs.GetProperty("in_current_journeys").GetInt64();
            Outside = legs.GetProperty("outside_current_journeys").GetInt64(); Travelling = legs.GetProperty("travelling").GetInt64();
            Splits = legs.GetProperty("split_prefix_legs").GetInt64();
            TopTypes = root.GetProperty("top_types").GetArrayLength();
        }
        public Dictionary<string, long> Types { get; }
        public long Retained { get; }
        public long InJourneys { get; }
        public long Outside { get; }
        public long Travelling { get; }
        public long Splits { get; }
        public int TopTypes { get; }
    }

    private static Read Census(CitySimulator city, ClockRetimeDriver driver)
    {
        var census = new RetainedCensus();
        census.Add("runtime.simulation", city);
        census.Add("runtime.retimeDriver", driver);
        using var report = JsonDocument.Parse(census.ReportJson(1, 0, new[] { typeof(CitySimulator) }, RetainedCensus.InstrumentRoot, Tracked, RetainedCensus.JourneyLegs(city.State)));
        return new Read(report.RootElement.Clone());
    }

    [Fact]
    public void SevenOneTwoOneRetimesExportRouteLegByNameAndHoldNoLegOutsideACurrentJourney()
    {
        const int cycles = 7;
        const double spacing = 9;
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
        var controlDriver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        var retimedReads = new List<Read> { Census(city, driver) };
        var controlReads = new List<Read> { Census(control.City, controlDriver) };
        double start = state.Minute;
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = start + cycle * spacing;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            retimedReads.Add(Census(city, driver));
            controlReads.Add(Census(control.City, controlDriver));
        }
        var gap = new List<long>();
        for (int i = 0; i <= cycles; i++)
        {
            var r = retimedReads[i]; var c = controlReads[i];
            output.WriteLine("R" + i + " RouteLeg retimed " + r.Retained + " (" + r.InJourneys + " in current journeys, " + r.Outside + " outside, " + r.Travelling + " travelling) / control "
                + c.Retained + " (" + c.InJourneys + ", " + c.Outside + ", " + c.Travelling + "); split prefixes " + r.Splits + " / " + c.Splits + "; top_types rows " + r.TopTypes);
            Assert.Equal(1, r.TopTypes);
            foreach (var type in Tracked) Assert.True(r.Types.ContainsKey(RetainedCensus.Name(type)) && c.Types.ContainsKey(RetainedCensus.Name(type)), RetainedCensus.Name(type) + " is exported by name");
            Assert.Equal(r.Retained, r.Types["RouteLeg"]);
            Assert.Equal(1, r.Types["CitySimulator"]);
            Assert.Equal(0, r.Outside);
            Assert.Equal(0, c.Outside);
            long extra = r.InJourneys - c.InJourneys;
            Assert.Equal(0, c.Splits);
            Assert.True(r.Splits <= r.Travelling, "R" + i + " split prefixes " + r.Splits + " exceed one per travelling journey");
            Assert.True(Math.Abs(extra - r.Splits) <= 2, "R" + i + " the retimed excess " + extra + " is the " + r.Splits + " split prefixes held by journeys");
            Assert.True(extra <= Math.Max(r.Travelling, c.Travelling) + 2, "R" + i + " retimed journeys hold " + extra + " more legs than the control, above one per travelling journey");
            gap.Add(extra);
        }
        bool rising = true;
        for (int i = 2; i < gap.Count; i++) rising &= gap[i] > gap[i - 1];
        Assert.False(rising, "retimed minus control RouteLeg rose at every checkpoint: " + string.Join(", ", gap));
        output.WriteLine("retimed minus control RouteLeg R0..R" + cycles + ": " + string.Join(", ", gap));
    }

    [Fact]
    public void TheJourneyLegLedgerCountsSharedLegsOnceAndIgnoresPeopleWithoutAJourney()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var shared = new RouteLeg { From = new Position(0, 0, 0), To = new Position(10, 0, 0), Minutes = 1, Mode = TravelMode.Walk };
        var own = new RouteLeg { From = new Position(10, 0, 0), To = new Position(20, 0, 0), Minutes = 1, Mode = TravelMode.Walk };
        foreach (var person in PersonTable.Schedules(state)) person.Journey = null;
        var people = PersonTable.Schedules(state).Take(3).ToArray();
        people[0].Journey = new Route { Legs = new List<RouteLeg> { shared, own } };
        people[1].Journey = new Route { Legs = new List<RouteLeg> { shared } };
        var ledger = RetainedCensus.JourneyLegs(state);
        Assert.Equal(2, ledger.Journeys);
        Assert.Equal(2, ledger.Legs);
        Assert.Equal(0, RetainedCensus.JourneyLegs(null).Legs);
    }
}
