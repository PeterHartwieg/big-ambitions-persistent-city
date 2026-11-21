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
            MostSplits = legs.GetProperty("most_split_prefixes_in_a_journey").GetInt64();
            TopTypes = root.GetProperty("top_types").GetArrayLength();
        }
        public Dictionary<string, long> Types { get; }
        public long Retained { get; }
        public long InJourneys { get; }
        public long Outside { get; }
        public long Travelling { get; }
        public long Splits { get; }
        public long MostSplits { get; }
        public int TopTypes { get; }
    }

    private static readonly Type[] TrafficTracked = Tracked.Concat(new[] { typeof(TrafficRouteLeg), typeof(TrafficRouteLeg[]), typeof(List<TrafficRouteLeg>), typeof(TrafficKernelState) }).ToArray();

    private static Read Census(CitySimulator city, ClockRetimeDriver driver) => Census(city, driver, Tracked);

    private static Read Census(CitySimulator city, ClockRetimeDriver driver, Type[] tracked)
    {
        var census = new RetainedCensus();
        census.Add("runtime.simulation", city);
        census.Add("runtime.retimeDriver", driver);
        using var report = JsonDocument.Parse(census.ReportJson(1, 0, new[] { typeof(CitySimulator) }, RetainedCensus.InstrumentRoot, tracked, RetainedCensus.JourneyLegs(city.State)));
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
        var unexplained = new List<long>();
        var mostSplits = new List<long>();
        var outside = new List<long>();
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
            Assert.True(r.MostSplits <= 1, "R" + i + " a journey holds " + r.MostSplits + " split prefixes after repeated retimes");
            gap.Add(extra); unexplained.Add(extra - r.Splits); mostSplits.Add(r.MostSplits); outside.Add(r.Outside);
        }
        output.WriteLine("retimed minus control RouteLeg R0..R" + cycles + ": " + string.Join(", ", gap) + "; travelling " + string.Join(", ", retimedReads.Select(r => r.Travelling))
            + "; excess not explained by split prefixes " + string.Join(", ", unexplained) + "; most split prefixes in one journey " + string.Join(", ", mostSplits)
            + "; raw gap " + (StillClimbing(gap) ? "rises past its earlier maximum" : "plateaus"));
        Assert.False(StillClimbing(unexplained), "RouteLeg excess beyond the split prefixes climbs: " + string.Join(", ", unexplained));
        Assert.False(StillClimbing(mostSplits), "split prefixes per journey climb: " + string.Join(", ", mostSplits));
        Assert.False(StillClimbing(outside), "RouteLeg outside current journeys climbs: " + string.Join(", ", outside));
    }

    [Fact]
    public void TrafficOwnedJourneysHoldARouteLegPlateauAcrossRepeatedOneTwoOneRetimes()
    {
        const int residents = 6, edges = 381, cycles = 12;
        const double departure = 1440, spacing = 2.5;
        var retimed = RoadCity(residents, edges, departure); var control = RoadCity(residents, edges, departure);
        var driver = new ClockRetimeDriver();
        var controlDriver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 2, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            Assert.Equal(0, driver.Degraded);
            var rebuild = city.BeginRebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(rate, edges), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        var network = new TravelTimingMaximumRouteTests.LongRoad(1, edges).TrafficNetwork!;
        (Read Census, int Owned, long Completed) Checkpoint(CitySimulator simulator, ClockRetimeDriver retimeDriver)
        {
            var city = simulator.State;
            city.PrepareForSave();
            long completed = 0;
            foreach (var c in city.Citizens)
            {
                var vehicle = city.Traffic!.Vehicles.Single(v => v.VehicleId == c.Identity.Id);
                network.ValidateRemainingMapping(c.Journey!, vehicle.Route, vehicle.NominalMinutes);
                completed += Math.Max(0, vehicle.RouteIndex);
            }
            return (Census(simulator, retimeDriver), city.Traffic!.Vehicles.Count, completed);
        }
        var reads = new List<((Read Census, int Owned, long Completed) Retimed, (Read Census, int Owned, long Completed) Control)>
        { (Checkpoint(city, driver), Checkpoint(control.City, controlDriver)) };
        double start = state.Minute;
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = start + cycle * spacing;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            reads.Add((Checkpoint(city, driver), Checkpoint(control.City, controlDriver)));
        }
        var gap = new List<long>();
        var mostSplits = new List<long>();
        for (int i = 0; i <= cycles; i++)
        {
            var (r, c) = reads[i];
            output.WriteLine("R" + i + " RouteLeg retimed " + r.Census.Retained + " / control " + c.Census.Retained + "; split prefixes " + r.Census.Splits + " / " + c.Census.Splits
                + "; most split prefixes in a journey " + r.Census.MostSplits + "; completed traffic spans retimed " + r.Completed + " / control " + c.Completed + "; travelling " + r.Census.Travelling + " / " + c.Census.Travelling
                + "; traffic-owned " + r.Owned + " / " + c.Owned);
            Assert.Equal(residents, r.Census.Travelling); Assert.Equal(residents, c.Census.Travelling);
            Assert.Equal(residents, r.Owned); Assert.Equal(residents, c.Owned);
            Assert.Equal(0, r.Census.Outside); Assert.Equal(0, c.Census.Outside);
            Assert.Equal(0, c.Census.Splits);
            long extra = r.Census.InJourneys - c.Census.InJourneys;
            Assert.Equal(r.Census.Splits, extra);
            Assert.True(r.Census.MostSplits <= 2, "R" + i + " a traffic-owned journey holds " + r.Census.MostSplits + " split prefixes");
            Assert.True(extra <= 2 * residents, "R" + i + " retimed journeys hold " + extra + " more RouteLegs than the control");
            gap.Add(extra); mostSplits.Add(r.Census.MostSplits);
        }
        output.WriteLine("retimed minus control RouteLeg R0..R" + cycles + ": " + string.Join(", ", gap) + "; most split prefixes in one journey " + string.Join(", ", mostSplits));
        Assert.True(reads[cycles].Control.Completed > 0, "the replay ends with traffic spans completed");
        Assert.False(StillClimbing(gap), "raw retimed minus control RouteLeg climbs while the journeys are active: " + string.Join(", ", gap));
        Assert.False(StillClimbing(mostSplits), "split prefixes per traffic-owned journey climb: " + string.Join(", ", mostSplits));
    }

    private static (CityState State, CitySimulator City) RoadCity(int residents, int edges, double departure)
    {
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity
        { Id = "road" + i, Name = "Road " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(edges + 4, 0, 0) }
        }, "long-road", "0.40.4", 7, departure);
        for (int i = 0; i < residents; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 4000;
            c.DepartureMinute = departure - i * 0.7;
            c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 0) };
            var legs = new List<RouteLeg> { Leg(-4, -2, 1, TravelMode.Walk, ""), Leg(-2, 0, 1, TravelMode.Walk, "") };
            for (int e = 0; e < edges; e++) legs.Add(Leg(e, e + 1, 0.1, TravelMode.Car, "native:road:v1:" + TravelTimingMaximumRouteTests.LongRoad.Topology + ":" + e + ":" + (e + 1)));
            legs.Add(Leg(edges, edges + 2, 1, TravelMode.Walk, "")); legs.Add(Leg(edges + 2, edges + 4, 1, TravelMode.Walk, ""));
            c.Journey = new Route { Legs = legs }; c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        var city = new CitySimulator(state, new TravelTimingMaximumRouteTests.LongRoad(1, edges));
        city.AdvanceTo(departure + 2 + 3.037, int.MaxValue);
        return (state, city);
    }

    [Fact]
    public void ActiveTrafficHoldsNoRouteOrVehicleStateBeyondItsLiveCarsAcrossRetimesAndDrain()
    {
        const int residents = 6, edges = 381, cycles = 7;
        const double departure = 1440, spacing = 2.5;
        var retimed = RoadCity(residents, edges, departure); var control = RoadCity(residents, edges, departure);
        var driver = new ClockRetimeDriver();
        var controlDriver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 2, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            Assert.Equal(0, driver.Degraded);
            var rebuild = city.BeginRebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(rate, edges), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        (Read Census, int Cars, long Legs) Checkpoint(CitySimulator simulator, ClockRetimeDriver retimeDriver)
        {
            var live = new List<TrafficVehicleState>();
            simulator.Traffic?.CopyVehiclesInOrder(live);
            return (Census(simulator, retimeDriver, TrafficTracked), live.Count, live.Sum(v => (long)v.Route.Count));
        }
        var reads = new List<(string Name, (Read Census, int Cars, long Legs) Retimed, (Read Census, int Cars, long Legs) Control)>
        { ("R0", Checkpoint(city, driver), Checkpoint(control.City, controlDriver)) };
        double start = state.Minute;
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = start + cycle * spacing;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            reads.Add(("R" + cycle, Checkpoint(city, driver), Checkpoint(control.City, controlDriver)));
        }
        double drained = departure + 120;
        city.AdvanceTo(drained, int.MaxValue);
        control.City.AdvanceTo(drained, int.MaxValue);
        reads.Add(("drain", Checkpoint(city, driver), Checkpoint(control.City, controlDriver)));
        Assert.All(new[] { state, control.State }, s => Assert.All(s.Citizens, c => Assert.NotEqual(CitizenPhase.Travelling, c.Phase)));
        var types = new[] { typeof(TrafficVehicleState), typeof(List<TrafficRouteLeg>), typeof(TrafficRouteLeg[]), typeof(TrafficRouteLeg), typeof(TrafficKernelState) };
        var gaps = types.ToDictionary(t => t, _ => new List<long>());
        foreach (var (name, r, c) in reads)
        {
            output.WriteLine(name + " live cars retimed " + r.Cars + " / control " + c.Cars + "; live traffic legs " + r.Legs + " / " + c.Legs + "; "
                + string.Join("; ", types.Select(t => RetainedCensus.Name(t) + " " + r.Census.Types[RetainedCensus.Name(t)] + " / " + c.Census.Types[RetainedCensus.Name(t)])));
            Assert.Equal(r.Cars, c.Cars);
            foreach (var (census, cars, legs) in new[] { (r.Census, r.Cars, r.Legs), (c.Census, c.Cars, c.Legs) })
            {
                Assert.Equal(cars, census.Types["TrafficVehicleState"]);
                Assert.Equal(cars, census.Types["List<TrafficRouteLeg>"]);
                Assert.Equal(cars, census.Types["TrafficRouteLeg[]"]);
                Assert.Equal(legs, census.Types["TrafficRouteLeg"]);
            }
            foreach (var t in types) gaps[t].Add(r.Census.Types[RetainedCensus.Name(t)] - c.Census.Types[RetainedCensus.Name(t)]);
        }
        foreach (var t in types) output.WriteLine("retimed minus control " + RetainedCensus.Name(t) + " R0..R" + cycles + ", drain: " + string.Join(", ", gaps[t]));
        var (_, drainRetimed, drainControl) = reads[reads.Count - 1];
        Assert.Equal(0, drainRetimed.Cars);
        foreach (var t in types)
        {
            Assert.False(StillClimbing(gaps[t].Take(cycles + 1).ToList()), "retimed minus control " + RetainedCensus.Name(t) + " climbs while traffic is active: " + string.Join(", ", gaps[t]));
            Assert.True(drainRetimed.Census.Types[RetainedCensus.Name(t)] <= drainControl.Census.Types[RetainedCensus.Name(t)],
                RetainedCensus.Name(t) + " outlives its cars after the drain: retimed " + drainRetimed.Census.Types[RetainedCensus.Name(t)] + " / control " + drainControl.Census.Types[RetainedCensus.Name(t)]);
        }
    }

    [Fact]
    public void ARebuildReleasesTheConsumedTrafficSnapshotAndAnUnpreparedSecondRebuildKeepsEveryCar()
    {
        const int residents = 6, edges = 381;
        var (state, city) = RoadCity(residents, edges, 1440);
        Assert.Equal(residents, city.Traffic!.VehicleCount);
        state.PrepareForSave();
        Assert.Equal(residents, state.Traffic!.Vehicles.Count);
        city = city.RebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(1, edges));
        Assert.Null(state.Traffic);
        city.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        city = city.RebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(1, edges));
        Assert.Null(state.Traffic);
        Assert.Equal(residents, city.Traffic!.VehicleCount);
        state.PrepareForSave();
        Assert.Equal(residents, state.Traffic!.Vehicles.Count);
        city.AdvanceTo(1440 + 120, int.MaxValue);
        Assert.All(state.Citizens, c => Assert.Equal(1, c.CompletedJourneys));
    }

    [Fact]
    public void ADirectRetimeBetweenRebuildsKeepsEveryActiveCarMappedAndSavesLikeAPreparedRetime()
    {
        const int residents = 6, edges = 381;
        var direct = RoadCity(residents, edges, 1440);
        var prepared = RoadCity(residents, edges, 1440);
        CitySimulator Run(CityState state, CitySimulator city, bool prepare)
        {
            city = city.RebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(1, edges));
            Assert.Null(state.Traffic);
            if (prepare) state.PrepareForSave();
            Assert.True(TravelTiming.Apply(state, 1.4, 2, out int degraded));
            Assert.Equal(0, degraded);
            city = city.RebuildRoutes(new TravelTimingMaximumRouteTests.LongRoad(2, edges));
            Assert.Null(state.Traffic);
            Assert.Equal(residents, city.Traffic!.VehicleCount);
            return city;
        }
        var directCity = Run(direct.State, direct.City, false);
        var preparedCity = Run(prepared.State, prepared.City, true);
        var live = new List<TrafficVehicleState>();
        directCity.Traffic!.CopyVehiclesInOrder(live);
        foreach (var vehicle in live)
        {
            var owner = direct.State.Citizens.Single(c => c.Identity.Id == vehicle.VehicleId);
            Assert.Equal(CitizenPhase.Travelling, owner.Phase);
            Assert.True(vehicle.Route.Count > 0 && vehicle.Route[vehicle.Route.Count - 1].EndNominalMinutes <= owner.Journey!.Minutes + 1e-9);
        }
        direct.State.PrepareForSave(); prepared.State.PrepareForSave();
        Assert.Equal(PersistentCity.Persistence.SaveCodec.Write(prepared.State), PersistentCity.Persistence.SaveCodec.Write(direct.State));
        Assert.False(TravelTiming.Apply(direct.State, 1.4, 2));
        directCity.AdvanceTo(1440 + 240, int.MaxValue);
        preparedCity.AdvanceTo(1440 + 240, int.MaxValue);
        Assert.All(direct.State.Citizens, c => Assert.Equal(1, c.CompletedJourneys));
        direct.State.PrepareForSave(); prepared.State.PrepareForSave();
        Assert.Equal(PersistentCity.Persistence.SaveCodec.Write(prepared.State), PersistentCity.Persistence.SaveCodec.Write(direct.State));
    }

    [Fact]
    public void SubwayAndBusWaitsAndRidesHoldOneSplitPrefixAcrossRepeatedOneTwoOneRetimes()
    {
        const int residents = 6, cycles = 10;
        const double departure = 1440, spacing = 2.5;
        (CityState State, CitySimulator City) Build()
        {
            var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity
            { Id = "rider" + i, Name = "Rider " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
            {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(-4, 0, 0) },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(404, 0, 0) }
            }, "subway-line", "0.40.4", 7, departure);
            for (int i = 0; i < residents; i++)
            {
                var c = state.Citizens[i];
                c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
                c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 4000;
                c.DepartureMinute = departure - i * 0.7;
                var legs = new List<RouteLeg> { Leg(-4, 0, 1, TravelMode.Walk, ""), Leg(0, 0, 9, TravelMode.Subway, "native:subway"),
                    Leg(0, 200, 12, TravelMode.Subway, "native:subway"), Leg(200, 200, 8, TravelMode.Bus, "bus:7"), Leg(200, 300, 11, TravelMode.Bus, "bus:7"),
                    Leg(300, 404, 9, TravelMode.Walk, "") };
                c.Journey = new Route { Legs = legs }; c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
            }
            var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
            city.AdvanceTo(departure + 1.6, int.MaxValue);
            return (state, city);
        }
        var retimed = Build(); var control = Build();
        var driver = new ClockRetimeDriver();
        var controlDriver = new ClockRetimeDriver();
        var city = retimed.City; var state = retimed.State;
        CitySimulator Retime(double rate)
        {
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = city.BeginRebuildRoutes(new FreshCityTraceTests.Walk(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            return rebuild.TakeResult();
        }
        var reads = new List<(Read Retimed, Read Control)> { (Census(city, driver), Census(control.City, controlDriver)) };
        double start = state.Minute;
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            city = Retime(2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = Retime(1);
            double end = start + cycle * spacing;
            city.AdvanceTo(end, int.MaxValue);
            control.City.AdvanceTo(end, int.MaxValue);
            reads.Add((Census(city, driver), Census(control.City, controlDriver)));
        }
        var gap = new List<long>();
        var mostSplits = new List<long>();
        for (int i = 0; i <= cycles; i++)
        {
            var (r, c) = reads[i];
            output.WriteLine("R" + i + " RouteLeg retimed " + r.Retained + " / control " + c.Retained + "; split prefixes " + r.Splits + " / " + c.Splits
                + "; most split prefixes in a journey " + r.MostSplits + "; travelling " + r.Travelling + " / " + c.Travelling);
            Assert.Equal(residents, r.Travelling); Assert.Equal(residents, c.Travelling);
            Assert.Equal(0, r.Outside);
            gap.Add(r.InJourneys - c.InJourneys); mostSplits.Add(r.MostSplits);
        }
        output.WriteLine("retimed minus control RouteLeg R0..R" + cycles + ": " + string.Join(", ", gap) + "; most split prefixes in one journey " + string.Join(", ", mostSplits));
        Assert.All(mostSplits, most => Assert.True(most <= 1, "a transit journey holds " + most + " split prefixes"));
        Assert.All(gap, extra => Assert.True(extra <= residents, "retimed journeys hold " + extra + " more RouteLegs than the control"));
        Assert.False(StillClimbing(gap), "raw retimed minus control RouteLeg climbs: " + string.Join(", ", gap));
        Assert.False(StillClimbing(mostSplits), "split prefixes per transit journey climb: " + string.Join(", ", mostSplits));
    }

    private static RouteLeg Leg(double from, double to, double minutes, TravelMode mode, string service) => new()
    { From = new Position(from, 0, 0), To = new Position(to, 0, 0), Minutes = minutes, Mode = mode, ServiceId = service };

    private static bool StillClimbing(IReadOnlyList<long> series)
    {
        if (series.Count < 4) return false;
        long earlier = long.MinValue;
        for (int i = 1; i < series.Count - 1; i++) earlier = Math.Max(earlier, series[i]);
        return series[series.Count - 1] > earlier;
    }

    [Fact]
    public void TheLeakRuleFailsALateRiseAfterAnEarlyDipAndPassesAPlateau()
    {
        var rig16v = new long[] { 0, 1538, 1440, 1916, -369, 1776, 2235, 2831 };
        Assert.True(StillClimbing(rig16v), "the 16v RouteLeg gap R4..R7 rises after the R4 dip");
        Assert.True(StillClimbing(new long[] { 0, 5, -3, 1, 2, 3, 4, 6 }), "a slow late rise after a dip fails");
        Assert.False(StillClimbing(new long[] { 0, 40, -10, 38, 41, 39, 40, 41 }), "a plateau that touches its earlier maximum passes");
        Assert.False(StillClimbing(new long[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
        bool everyStepRose(IReadOnlyList<long> s) { for (int i = 2; i < s.Count; i++) if (s[i] <= s[i - 1]) return false; return true; }
        Assert.False(everyStepRose(rig16v), "the earlier every-step rule passed the 16v gap");
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
        Assert.Equal(0, ledger.MostSplitPrefixes);
    }
}
