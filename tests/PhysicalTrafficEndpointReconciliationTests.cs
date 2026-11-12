using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalTrafficEndpointReconciliationTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly double[] X = { 220, 226.99713134765625, 233.34124755859375, 240.2172393798828 };
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = new TrafficNetwork(Native, X.Select((x, i) => new TrafficNetworkNode
        { Id = i, Position = new Position(x,0,0), KilometresPerHour = 30, AllowsCar = true, Boundary = i == 2,
            Successors = i + 1 < X.Length ? new[] { i + 1 } : Array.Empty<int>() }).ToArray(), 1);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    private sealed class Fixture
    {
        public readonly Planner Planner = new Planner();
        public readonly CitySimulator Simulation;
        public readonly CitizenState Citizen;
        public readonly Route Road;
        public readonly TrafficRouteLeg[] Mapped;
        public Position Corner => new Position(X[2],0,0);
        public double MiddleOfSecondLeg => Road.Legs[0].Minutes + Road.Legs[1].Minutes / 2;

        public Fixture()
        {
            var graph = new TransportRoadGraph(X.Select((x, i) => new TransportRoadNode
            { Position = new Position(x,0,0), KilometresPerHour = 30, AllowsCar = true,
                Successors = i + 1 < X.Length ? new[] { i + 1 } : Array.Empty<int>() }).ToArray());
            Road = graph.Plan(0, 3, _ => false, (_, _) => true)!;
            for (int i = 0; i < Road.Legs.Count; i++) Road.Legs[i].ServiceId = $"native:road:v1:{Native}:{i}:{i + 1}";
            Assert.True(Planner.TrafficNetwork!.TryMap(Road, out var mapped));
            Mapped = mapped.ToArray(); Assert.Equal(2, Mapped.Length);
            var state = Population.Create(new[] { new CitizenIdentity { Id = "proof", Name = "Proof", Age = 30, Persona = Persona.Commuter } }, new[]
            {
                new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = Road.Legs[0].From },
                new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = Road.Legs[2].To }
            }, "negative-endpoint-proof", "0.40.4", 1, 100);
            Citizen = state.Citizens[0];
            Citizen.PlaceId = Citizen.HomeId = "a"; Citizen.DayPlaceId = Citizen.DestinationId = "b";
            Citizen.Phase = CitizenPhase.Travelling; Citizen.DestinationActivity = Activity.Work; Citizen.ActivityUntilMinute = 3000;
            Citizen.DepartureMinute = 100; Citizen.Preferences.OwnsCar = true;
            Citizen.Car = new CarState { Id = "proof:car", ParkedPosition = Road.Legs[0].From };
            Citizen.Journey = Road; Citizen.NextEventMinute = 100 + Road.Minutes;
            Simulation = new CitySimulator(state, Planner); Simulation.AdvanceTo(100);
        }
    }

    [Fact]
    public void KernelCrossesTheActualMappedEndpointAndCompletesExactlyOnceWithoutABody()
    {
        var f = new Fixture(); string id = f.Citizen.Identity.Id;
        int completions = 0; var links = new List<int?>();
        f.Simulation.Traffic!.VehicleCompleted += (vehicle, _) => { Assert.Equal(id, vehicle); completions++; };
        for (double minute = 100; minute <= 104; minute += 0.01)
        {
            f.Simulation.AdvanceTo(minute);
            if (f.Simulation.TryGetTrafficPermission(id, out var permission) && (links.Count == 0 || links[^1] != permission.LinkId)) links.Add(permission.LinkId);
        }
        Assert.Equal(new int?[] { f.Mapped[0].LinkId, f.Mapped[1].LinkId }, links);
        Assert.Equal(1, completions); Assert.Equal(1, f.Citizen.CompletedJourneys);
        Assert.Equal(f.Road.Legs[2].To, f.Citizen.Car!.ParkedPosition);
        Assert.Equal(0, f.Simulation.Traffic.VehicleCount);
        Assert.All(f.Simulation.Traffic.Capture().Links, link => Assert.Empty(link.Vehicles));
        f.Simulation.AdvanceTo(112); f.Simulation.AdvanceTo(113);
        Assert.Equal(1, completions); Assert.Equal(1, f.Citizen.CompletedJourneys);
    }

    [Fact]
    public void FollowerAtTheActualMappedCornerReadsTheKernelAndNeverPassesIt()
    {
        var f = new Fixture(); string id = f.Citizen.Identity.Id;
        f.Simulation.Traffic!.SetLinkClosed(f.Mapped[1].LinkId, true);
        f.Simulation.AdvanceTo(110);
        var permission = f.Simulation.Traffic.GetPermission(id);
        Assert.Equal(f.Mapped[0].LinkId, permission.LinkId); Assert.False(permission.CanCrossBoundary);
        Assert.Equal(1.60094970703125, permission.MaximumNominalMinutes);
        Assert.Equal(permission.MaximumNominalMinutes, permission.LinkEndNominalMinutes);
        var middle = WalkerFollow.Sample(f.Road, f.MiddleOfSecondLeg);
        Assert.Equal(1, middle.LegIndex);
        Assert.True(PhysicalProgress.TryProject(middle, f.Corner, true, out double corner));
        Assert.True(corner < permission.MaximumNominalMinutes);
        double cap = CarFollow.Cap(f.Road, f.MiddleOfSecondLeg, permission.MaximumNominalMinutes);
        Assert.Equal(permission.MaximumNominalMinutes, cap);
        Assert.True(PhysicalTrafficCornerPermission.CanReachEndpoint(middle, cap));
        double approach = CarFollow.Advance(f.Road, f.MiddleOfSecondLeg, cap, f.Corner, -1);
        Assert.Equal(1, WalkerFollow.Sample(f.Road, approach).LegIndex); Assert.True(approach < corner);
        double crossed = CarFollow.Advance(f.Road, f.MiddleOfSecondLeg, cap, f.Corner, 1);
        var last = WalkerFollow.Sample(f.Road, crossed);
        Assert.Equal(2, last.LegIndex); Assert.True(crossed <= cap);
        Assert.Equal(0, PhysicalTrafficMotion.MaximumAdvance(last, f.Corner, cap));
        f.Simulation.Traffic.SetLinkClosed(f.Mapped[1].LinkId, false);
        f.Simulation.AdvanceTo(112);
        Assert.Equal(1, f.Citizen.CompletedJourneys);
    }

}
