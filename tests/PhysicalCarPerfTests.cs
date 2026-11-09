using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests;

// Opt-in offline measurements for docs/perf/f2d.md: set PC_F2D_MEASURE to an output file. Uses only API that also
// exists before F2d (kernel counters, the movement harness and the production PhysicalPopulation on the native
// doubles), so the same file measures the base build (traffic leases) and this one (cars follow the kernel).
[Collection(StreetFoodHooks.Collection)]
public sealed class PhysicalCarPerfTests
{
    private const string Native = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const int Cars = 24, Sections = 6;
    private const double Now = 1440 + 600;

    // One straight road per car, 12 m sections at 36 km/h (1.2 game minutes each), then a short walk.
    private sealed class Roads : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = new TrafficNetwork(Native, Nodes().ToList(), 1);
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }
    private static Position Node(int car, int index) => new(40 + 12 * index, 0, -36 + 3 * car);
    private static IEnumerable<TrafficNetworkNode> Nodes()
    {
        for (int car = 0; car < Cars; car++)
            for (int i = 0; i <= Sections; i++)
                yield return new TrafficNetworkNode { Id = car * 100 + i, Position = Node(car, i), KilometresPerHour = 36, AllowsCar = true,
                    Boundary = true, Successors = i < Sections ? new[] { car * 100 + i + 1 } : Array.Empty<int>() };
    }
    private static Route Journey(int car)
    {
        var route = new Route { Legs = new() };
        for (int i = 0; i < Sections; i++)
            route.Legs.Add(new RouteLeg { From = Node(car, i), To = Node(car, i + 1), Minutes = 1.2, Mode = TravelMode.Car,
                ServiceId = "native:road:v1:" + Native + ":" + (car * 100 + i) + ":" + (car * 100 + i + 1) });
        var end = Node(car, Sections);
        route.Legs.Add(new RouteLeg { From = end, To = new Position(end.X + 10, 0, end.Z), Minutes = 5, Mode = TravelMode.Walk });
        return route;
    }

    private static CitySimulator City()
    {
        var ids = Enumerable.Range(0, Cars).Select(i => new CitizenIdentity { Id = "car" + i.ToString("D2"), Name = "C" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place>();
        for (int i = 0; i < Cars; i++)
        {
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = Node(i, 0) });
            var end = Node(i, Sections);
            places.Add(new Place { Id = "work" + i, Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = new Position(end.X + 10, 0, end.Z) });
        }
        var state = Population.Create(ids, places, "f2d-perf", "0.40.4", 3, Now);
        for (int i = 0; i < Cars; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = c.DestinationId = "work" + i; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationActivity = Activity.Work; c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = Node(i, 0) };
            c.Journey = Journey(i);
            c.DepartureMinute = Now; c.NextEventMinute = Now + c.Journey.Minutes; c.ActivityUntilMinute = Now + 600;
        }
        var city = new CitySimulator(state, new Roads());
        city.AdvanceTo(Now + 0.01);
        return city;
    }

    // The next committed road corner ahead of a car body: Gley raises its reached marker there.
    private static Position? Corner(CitySimulator city, PhysicalVehicleBody body)
    {
        var c = city.State.Citizens.First(p => p.Identity.Id == body.CitizenId);
        Position? best = null; double nearest = double.PositiveInfinity;
        foreach (var leg in c.Journey?.Legs ?? new List<RouteLeg>())
        {
            if (leg.Mode != TravelMode.Car) continue;
            double d = Position.Distance(body.Position, leg.To);
            if (d < nearest) { nearest = d; best = leg.To; }
        }
        return best;
    }

    [Fact]
    public void MeasureUpdateKernelWorkAndHarnessWithTwentyFourCars()
    {
        string? path = System.Environment.GetEnvironmentVariable("PC_F2D_MEASURE");
        if (path == null) return;
        var lines = new List<string>();
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        MultimodalRoutes.CarSamples = true;
        try
        {
            var city = City();
            NativeTravelPace.Focus = new Position(76, 0, 0); Time.Next(3);
            using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
                typeof(PhysicalCarPerfTests).Assembly, new VendorBodyTests.Log(), 8, Cars, 120, new DwellSpots());
            var pool = PhysicalVehicles.Last!;
            // A third of the cars drive at 0.6x native pace (a slow visible car); the rest at 1.2x the kernel's pace.
            double minute = city.State.Minute;
            const float frame = 1 / 60f;
            const double gamePerSecond = 1 / 3.0;
            void Frame()
            {
                foreach (var body in pool.Active.Values) body.Pace = body.CitizenId.GetHashCode() % 3 == 0 ? 0.6 : 1.2;
                pool.Drive(frame, 10 * gamePerSecond, b => Corner(city, b));
                minute += frame * gamePerSecond; city.AdvanceTo(minute); Time.Next(frame);
            }
            int warm = 0;
            while (pool.Active.Count < Cars && warm++ < 600) { Frame(); bodies.Update(); }
            int count = 1800, seen = 0;
            var times = new List<double>(count);
            long reconsiders = city.Traffic!.Reconsiders, events = city.Traffic.ProcessedEvents;
            double startMinute = city.State.Minute;
            for (int i = 0; i < count; i++)
            {
                Frame();
                long start = Stopwatch.GetTimestamp();
                bodies.Update();
                times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                seen = Math.Max(seen, pool.Active.Count);
            }
            double hours = (city.State.Minute - startMinute) / 60;
            times.Sort();
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "PhysicalPopulation.Update: max cars={0}, frames={1}, p50={2:F4} ms, p95={3:F4} ms, max={4:F4} ms, spikes>=30ms={5}; "
                + "kernel reconsiders/frame={6:F2}, kernel events/game hour={7:F1}, acquisitions={8}",
                seen, count, times[count / 2], times[(int)Math.Ceiling(count * .95) - 1], times[^1], times.Count(t => t >= 30),
                (city.Traffic.Reconsiders - reconsiders) / (double)count, (city.Traffic.ProcessedEvents - events) / hours, pool.Acquisitions));
        }
        finally { MultimodalRoutes.CarSamples = false; }

        // The movement harness (957 residents, one day): kernel work and lag with up to 24 visible cars.
        foreach (var (name, change) in new (string, Action<MovementOptions>)[] {
            ("bodies-off", o => o.Driver = false), ("focus/stationary 48/24", o => { }),
            ("lag/slow-head-car", o => o.Bodies.Lag = LagModel.SlowHeadCar) })
        {
            var options = SelfChecks.Small(name, MovementFixture.Residents);
            change(options);
            var run = MovementRun.Run(options);
            var m = run.Metrics;
            double hours = options.Days * 24 + 3;
            string Metric(string key) => m.TryGetValue(key, out double v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "n/a";
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "harness {0}: frames={1}, kernel reconsiders/frame={2:F3}, kernel events/game hour={3:F1}, max cars={4}, car frames={5}, "
                + "car lag p50/p95/max m={6}/{7}/{8}, car corrections={9}, run {10:F1} s",
                name, m["run.frames"], m["traffic.reconsiders"] / m["run.frames"], m["traffic.events"] / hours, m["bodies.maxCars"],
                m["bodies.carFrames"], Metric("cars.lagP50Metres"), Metric("cars.lagP95Metres"), Metric("cars.lagMaxMetres"),
                Metric("cars.corrections"), run.Seconds));
        }
        File.AppendAllText(path, string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
    }
}
