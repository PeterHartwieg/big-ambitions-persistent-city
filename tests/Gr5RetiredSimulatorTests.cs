using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetiredSimulatorTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetiredSimulatorTests(ITestOutputHelper output) { this.output = output; }

    private const string Native = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static Position P(double x) => new(x, 0, 0);

    private sealed class RoadPlanner : IRoutePlanner, ITrafficRoutePlanner, ITrafficEntryRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; }
        public Func<int, double, TrafficLinkForecast>? Forecast;
        public Func<int, double, double, double, double>? EntryForecast;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { Forecast = forecast; }
        public void SetTrafficEntryForecast(Func<int, double, double, double, double>? forecast) { EntryForecast = forecast; }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private sealed class Donor { public RoadPlanner? Planner; }

    private static TrafficNetwork Line() => new TrafficNetwork(Native, Enumerable.Range(0, 12).Select(i => new TrafficNetworkNode
    { Id = i, Position = P(i * 10), AllowsCar = true, KilometresPerHour = 36, Boundary = i % 4 == 0,
        Successors = i == 11 ? Array.Empty<int>() : new[] { i + 1 } }).ToArray(), 1);

    private static CityState City(TrafficNetwork network)
    {
        var route = new Route();
        for (int i = 0; i < 11; i++)
        {
            network.TryGetEdge(i, i + 1, out var edge);
            route.Legs.Add(new RouteLeg { From = P(i * 10), To = P((i + 1) * 10), Mode = TravelMode.Car, Minutes = edge.FreeFlowMinutes,
                ServiceId = $"native:road:v1:{Native}:{i}:{i + 1}" });
        }
        var state = Population.Create(new[] { new CitizenIdentity { Id = "driver", Name = "Driver", Age = 30, Persona = Persona.Commuter } }, new[]
        { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = route.Legs[0].From },
          new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = route.Legs[^1].To } }, "gr5-retired-simulator", "0.40.4", 7, 100);
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a";
        citizen.DestinationId = citizen.DayPlaceId = "b"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.DepartureMinute = 100; citizen.NextEventMinute = 100 + route.Minutes; citizen.Journey = route;
        citizen.Preferences.OwnsCar = true; citizen.Car = new CarState { Id = "driver:car", ParkedPosition = route.Legs[0].From };
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        return state;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (CitySimulator Live, RoadPlanner Planner, List<WeakReference> Retired) Retime(Donor donor, int retimes)
    {
        var network = Line();
        var planner = new RoadPlanner { TrafficNetwork = network };
        var city = new CitySimulator(City(network), planner);
        city.AdvanceTo(100.5, 64);
        Assert.NotNull(planner.Forecast);
        var retired = new List<WeakReference>();
        for (int i = 0; i < retimes; i++)
        {
            var next = new RoadPlanner { TrafficNetwork = network };
            donor.Planner = planner;
            var rebuild = city.BeginRebuildRoutes(next, null);
            while (!rebuild.Step(long.MaxValue)) { }
            var replacement = rebuild.TakeResult();
            Assert.NotNull(next.Forecast);
            retired.Add(new WeakReference(city));
            city = replacement; planner = next;
            city.AdvanceTo(100.5 + 0.5 * (i + 1), 64);
        }
        return (city, planner, retired);
    }

    [Fact]
    public void ARetiredSimulatorBecomesUnreachableAfterEveryRetimeWhileTheRoadDonorKeepsItsPlanner()
    {
        var donor = new Donor();
        var (live, planner, retired) = Retime(donor, 3);
        Assert.NotNull(donor.Planner);
        Assert.Null(donor.Planner!.Forecast);
        Assert.Null(donor.Planner.EntryForecast);
        Assert.NotNull(planner.Forecast);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(retired, simulator => Assert.False(simulator.IsAlive));
        GC.KeepAlive(live); GC.KeepAlive(donor);
    }

    [Fact]
    public void TheCensusCountsOneSimulatorAfterRepeatedRetimesWithARoadDonor()
    {
        var donor = new Donor();
        var (live, planner, _) = Retime(donor, 4);
        var census = new RetainedCensus();
        census.Add("runtime.simulation", live);
        census.Add("runtime.routes", planner);
        census.Add("static.RoadRoutes.donor", donor);
        using var json = JsonDocument.Parse(census.ReportJson(8, 0, new[] { typeof(CitySimulator), typeof(TrafficKernel) }));
        output.WriteLine(json.RootElement.GetProperty("type_counts").ToString());
        Assert.Equal(1, json.RootElement.GetProperty("type_counts").GetProperty(nameof(CitySimulator)).GetInt64());
        Assert.Equal(0, census.Failures);
    }

    [Fact]
    public void AForecastUsedByTheReplacementIsKeptWhenARebuildReusesThePlanner()
    {
        var network = Line();
        var planner = new RoadPlanner { TrafficNetwork = network };
        var city = new CitySimulator(City(network), planner);
        city.AdvanceTo(100.5, 64);
        var rebuild = city.BeginRebuildRoutes(planner, null);
        while (!rebuild.Step(long.MaxValue)) { }
        var replacement = rebuild.TakeResult();
        Assert.NotNull(planner.Forecast);
        Assert.Same(replacement, planner.Forecast!.Target is CitySimulator target ? target : Closure(planner.Forecast));
        replacement.AdvanceTo(101.5, 64);
    }

    private static object? Closure(Delegate value)
    {
        var target = value.Target;
        if (target == null) return null;
        foreach (var field in target.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            if (field.GetValue(target) is CitySimulator city) return city;
        return target;
    }

    private sealed class SettingsShape { public Func<int>? Callback; public int[] Values = new int[100]; }
    private sealed class RuntimeShape
    {
        public SettingsShape Settings = new SettingsShape();
        public int[] Large = new int[100_000];
        public RuntimeShape() { Settings.Callback = () => Large.Length; }
    }

    [Fact]
    public void ACallbackBackToTheRuntimeIsNotChargedToTheFieldThatHoldsIt()
    {
        var runtime = new RuntimeShape();
        var census = new RetainedCensus();
        census.AddFields("runtime.", runtime, null);
        var roots = census.Roots.ToDictionary(r => r.Name, r => r);
        output.WriteLine("settings " + roots["runtime.Settings"].Bytes + " B, large " + roots["runtime.Large"].Bytes + " B");
        Assert.True(roots["runtime.Settings"].Bytes < 2048, "settings " + roots["runtime.Settings"].Bytes);
        Assert.True(roots["runtime.Large"].Bytes >= 400_000, "large " + roots["runtime.Large"].Bytes);
    }
}
