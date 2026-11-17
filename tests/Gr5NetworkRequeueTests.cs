using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5NetworkRequeueTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private sealed class SwappablePlanner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = Network();
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
    }

    private static TrafficNetwork Network() => new TrafficNetwork(Native, new[]
    {
        new TrafficNetworkNode { Id = 0, Position = new Position(0, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
        new TrafficNetworkNode { Id = 1, Position = new Position(12, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 2 } },
        new TrafficNetworkNode { Id = 2, Position = new Position(24, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
    }, 1);

    private static CitySimulator City(int commuters, int resting, SwappablePlanner planner)
    {
        int count = commuters + resting;
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "requeue" + i.ToString("D4"), Name = "Resident " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(30, 0, 0) }
        }, "network-requeue", "0.40.4", 7, 1440);
        for (int i = 0; i < count; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
            if (i >= commuters)
            {
                c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
                c.ActivityUntilMinute = 9000 + i; c.NextEventMinute = 9000 + i;
                continue;
            }
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work";
            c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 5000; c.DepartureMinute = 1440; c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 0) };
            c.Journey = new Route();
            c.Journey.Legs.Add(new RouteLeg { From = state.Places[0].Position, To = new Position(0, 0, 0), Minutes = 2, Mode = TravelMode.Walk });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(0, 0, 0), To = new Position(12, 0, 0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":0:1" });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(12, 0, 0), To = new Position(24, 0, 0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":1:2" });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(24, 0, 0), To = state.Places[1].Position, Minutes = 3, Mode = TravelMode.Walk });
            c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        return new CitySimulator(state, planner);
    }

    [Fact]
    public void SwappedEqualNetworkKeepsTheSameQueueJourneysAndSaveBytes()
    {
        var steadyPlanner = new SwappablePlanner();
        var swappedPlanner = new SwappablePlanner();
        var steady = City(40, 60, steadyPlanner);
        var swapped = City(40, 60, swappedPlanner);
        foreach (double minute in new[] { 1440.5, 1441.7, 1443.1, 1444.9, 1447.3, 1452.0 })
        {
            steady.AdvanceTo(minute);
            swapped.AdvanceTo(minute);
            swappedPlanner.TrafficNetwork = Network();
            steady.RefreshTrafficClosures();
            swapped.RefreshTrafficClosures();
            Assert.Equal(steady.QueuedPersonEvents, swapped.QueuedPersonEvents);
            Assert.Equal(SaveCodec.Write(steady.State), SaveCodec.Write(swapped.State));
        }
        steady.AdvanceTo(3600);
        swapped.AdvanceTo(3600);
        Assert.Equal(steady.State.Citizens.Select(c => c.CompletedJourneys), swapped.State.Citizens.Select(c => c.CompletedJourneys));
        Assert.Equal(steady.State.Citizens.Select(c => c.NextEventMinute), swapped.State.Citizens.Select(c => c.NextEventMinute));
        Assert.Equal(SaveCodec.Write(steady.State), SaveCodec.Write(swapped.State));
    }

    [Fact]
    public void NetworkSwapDoesNotReallocateTheRestingQueue()
    {
        const int resting = 3000;
        var planner = new SwappablePlanner();
        var city = City(0, resting, planner);
        city.RefreshTrafficClosures();
        planner.TrafficNetwork = Network();
        city.RefreshTrafficClosures();
        planner.TrafficNetwork = Network();
        long before = GC.GetAllocatedBytesForCurrentThread();
        city.RefreshTrafficClosures();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(resting, city.QueuedPersonEvents);
        Assert.True(allocated < 4096, "network swap allocated " + allocated + " bytes for " + resting + " resting residents");
    }
}
