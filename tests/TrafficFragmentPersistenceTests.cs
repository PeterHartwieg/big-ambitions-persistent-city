using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficFragmentPersistenceTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static Position Point(int i) => new(220 + i * 10, i * .5, 0);
    private static string Tag(int from, int to) => $"native:road:v1:{Native}:{from}:{to}";
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public Planner(double rate = 1, bool boundary = false)
        {
            TrafficNetwork = new TrafficNetwork(Native, Enumerable.Range(0, 3).Select(i => new TrafficNetworkNode
            {
                Id = i, Position = Point(i), AllowsCar = true, KilometresPerHour = 36, Boundary = boundary && i == 1,
                Successors = i == 2 ? Array.Empty<int>() : new[] { i + 1 }
            }).ToArray(), rate);
        }
        public TrafficNetwork TrafficNetwork { get; }
        public Route? Plan(Place a, Place b, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private static (CityState State, double Progress) Retimed(bool boundary = false)
    {
        double duration = Position.Distance(Point(0), Point(1)) / 10;
        var route = new Route
        {
            Legs = Enumerable.Range(0, 2).Select(i => new RouteLeg
            { From = Point(i), To = Point(i + 1), Minutes = duration, Mode = TravelMode.Car, ServiceId = Tag(i, i + 1) }).ToList()
        };
        var state = Population.Create(new[] { new CitizenIdentity { Id = "car", Name = "Car", Age = 30, Persona = Persona.Commuter } }, new[]
        {
            new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = Point(0) },
            new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = Point(2) }
        }, "fragment-persistence", "0.40.4", 7, 0);
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a";
        citizen.DestinationId = citizen.DayPlaceId = "b"; citizen.Phase = CitizenPhase.Travelling;
        citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.Journey = route; citizen.DepartureMinute = 0; citizen.NextEventMinute = route.Minutes;
        citizen.Preferences.OwnsCar = true; citizen.Car = new CarState { Id = "car:car", ParkedPosition = Point(0) };
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        var simulation = new CitySimulator(state, new Planner(boundary: boundary)); simulation.AdvanceTo(0);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        double progress = duration - 1e-8;
        simulation.AdvanceTo(progress); Assert.True(simulation.ObservePhysicalJourney(lease!, progress));
        Assert.True(simulation.ReleasePhysicalJourney(lease!)); state.PrepareForSave();
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(3, citizen.Journey!.Legs.Count);
        double metres = Position.Distance(Tail(state).From, Tail(state).To);
        Assert.InRange(metres, double.Epsilon, 1e-6);
        Assert.StartsWith("native:road:fragment1:", Tail(state).ServiceId);
        return (state, progress);
    }

    private static RouteLeg Tail(CityState state) => state.Citizens[0].Journey!.Legs[1];

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualRetimedHundredNanometreFragmentSavesReloadsAndRebinds(bool boundary)
    {
        var (state, progress) = Retimed(boundary);
        var direct = new CitySimulator(state, new Planner(2, boundary));
        Assert.Equal(progress, direct.Traffic!.GetPermission("car").NominalMinutes);
        Position pose = state.Citizens[0].Journey!.PositionAt(progress);
        string wire = SaveCodec.Write(state);
        var decoded = SaveCodec.Read(wire); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        Assert.Equal(wire, SaveCodec.Write(decoded.State!));
        var rebound = new CitySimulator(decoded.State!, new Planner(2, boundary));
        Assert.Equal(progress, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(pose, decoded.State!.Citizens[0].Journey!.PositionAt(progress));
        Assert.Equal(1, rebound.Traffic.VehicleCount); Assert.Equal(0, decoded.State.Citizens[0].CompletedJourneys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("native:road:fragment1:")]
    [InlineData("native:road:fragment1:short:0:1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag:0:1")]
    [InlineData("native:road:fragment1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:0:1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:-1:1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0:+1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0:2147483648")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:1:1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0:1:extra")]
    [InlineData("native:road:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0:1")]
    public void TinyTrafficMovementNeedsTheExactFragmentTagShape(string service)
    {
        var (state, _) = Retimed();
        string valid = SaveCodec.Write(state);
        Tail(state).ServiceId = service;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        var wire = JObject.Parse(valid); wire["Citizens"]![0]!["Journey"]!["Legs"]![1]!["ServiceId"] = service;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("walk")]
    [InlineData("nonfinite")]
    public void FragmentTagCannotAuthorizeStationaryWalkingOrNonfiniteTraffic(string defect)
    {
        var (state, _) = Retimed(); var tail = Tail(state);
        var wire = JObject.Parse(SaveCodec.Write(state));
        if (defect == "zero") tail.From = tail.To;
        if (defect == "walk") tail.Mode = TravelMode.Walk;
        if (defect == "nonfinite") tail.From = new Position(double.NaN, tail.From.Y, tail.From.Z);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        wire["Citizens"]![0]!["Journey"]!["Legs"]![1] = JObject.FromObject(tail);
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData("native:road:fragment1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:0:1")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:1:0")]
    [InlineData("native:road:fragment1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:0:2")]
    public void SyntaxAcceptanceStillRequiresActualGraphProvenanceAtBinding(string service)
    {
        var (state, _) = Retimed(); Tail(state).ServiceId = service;
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        Assert.Throws<ArgumentException>(() => new CitySimulator(decoded.State!, new Planner(2)));
    }
}
