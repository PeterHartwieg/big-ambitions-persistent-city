using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficJourneyTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; }
        public Func<int, double, TrafficLinkForecast>? Forecast;
        public HashSet<int> Closed = new();
        public Planner(double rate = 1, string native = Native)
        {
            TrafficNetwork = new TrafficNetwork(native, new[]
            {
                new TrafficNetworkNode { Id = 0, Position = new Position(0,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
                new TrafficNetworkNode { Id = 1, Position = new Position(12,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 2 } },
                new TrafficNetworkNode { Id = 2, Position = new Position(24,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
            }, rate);
        }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) => Forecast = forecast;
        public bool IsTrafficLinkClosed(int id) => Closed.Contains(id);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    private static CitySimulator Engine(int count = 8, double access = 0, double egress = 0, Planner? planner = null)
    {
        var state = Population.Create(Enumerable.Range(0,count).Select(i => new CitizenIdentity
        { Id = "traffic" + i.ToString("D3"), Name = "Traffic " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(access == 0 ? 0 : -access*2,0,0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(24+egress*2,0,0) }
        }, "traffic-journey-test", "0.40.4", 7, 1440);
        foreach (var citizen in state.Citizens)
        {
            citizen.PlaceId = citizen.HomeId = "home"; citizen.DayPlaceId = "work";
            citizen.Phase = CitizenPhase.Travelling; citizen.DestinationId = "work"; citizen.DestinationActivity = Activity.Work;
            citizen.ActivityUntilMinute = 2880; citizen.DepartureMinute = 1440;
            citizen.Preferences.OwnsCar = true;
            citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = new Position(0,0,0) };
            var route = new Route();
            if (access > 0) route.Legs.Add(new RouteLeg { From = state.Places[0].Position, To = new Position(0,0,0), Minutes = access, Mode = TravelMode.Walk });
            route.Legs.Add(new RouteLeg { From = new Position(0,0,0), To = new Position(12,0,0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":0:1" });
            route.Legs.Add(new RouteLeg { From = new Position(12,0,0), To = new Position(24,0,0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":1:2" });
            if (egress > 0) route.Legs.Add(new RouteLeg { From = new Position(24,0,0), To = state.Places[1].Position, Minutes = egress, Mode = TravelMode.Walk });
            citizen.Journey = route; citizen.NextEventMinute = 1440 + route.Minutes;
        }
        return new CitySimulator(state, planner ?? new Planner());
    }

    [Fact]
    public void ExactCurbDepartureRoundoffDoesNotRequireAnEmptySection()
    {
        var engine=Engine(3,0.2); engine.AdvanceTo(1440.2);
        Assert.Equal(2, engine.State.Citizens.Count(c=>engine.Traffic!.GetPermission(c.Identity.Id).LinkId.HasValue));
        Assert.Equal(1, engine.Traffic!.Capture().Vehicles.Count(v=>v.RouteIndex<0));
    }

    [Fact]
    public void RetimedOwnedCoalescedSectionSurvivesSaveAndRebindInsideSecondNativeEdge()
    {
        Planner Coalesced(double rate)
        {
            var planner=new Planner(rate);
            planner.TrafficNetwork=new TrafficNetwork(Native, new[]
            {
                new TrafficNetworkNode { Id=0, Position=new Position(0,0,0), KilometresPerHour=36, AllowsCar=true, Successors=new[]{1} },
                new TrafficNetworkNode { Id=1, Position=new Position(12,0,0), KilometresPerHour=36, AllowsCar=true, Successors=new[]{2} },
                new TrafficNetworkNode { Id=2, Position=new Position(24,0,0), KilometresPerHour=36, AllowsCar=true, Successors=new[]{3} },
                new TrafficNetworkNode { Id=3, Position=new Position(36,0,0), KilometresPerHour=36, AllowsCar=true }
            },rate);
            return planner;
        }
        var initial=Engine(1); var state=initial.State; state.PrepareForSave();
        state.Traffic=null; state.TrafficTopologyId="";
        state.Places[1].Position=new Position(36,0,0);
        var citizen=state.Citizens[0];
        citizen.Journey!.Legs.Add(new RouteLeg { From=new Position(24,0,0), To=new Position(36,0,0), Minutes=1.2, Mode=TravelMode.Car, ServiceId="native:road:v1:"+Native+":2:3" });
        citizen.NextEventMinute=1440+citizen.Journey.Minutes;
        var engine=new CitySimulator(state,Coalesced(1)); engine.AdvanceTo(1441.8);
        Assert.Equal(18,engine.PositionOf(citizen).X,8); state.PrepareForSave();
        Assert.True(TravelTiming.Apply(state,2,2));
        var parsed=SaveCodec.Read(SaveCodec.Write(state)); Assert.True(parsed.Status==SaveReadStatus.Loaded,parsed.Error);
        var restored=new CitySimulator(parsed.State!,Coalesced(2));
        Assert.Equal(18,restored.PositionOf(restored.State.Citizens[0]).X,8);
        Assert.Single(restored.Traffic!.Capture().Vehicles);
        restored.AdvanceTo(1500);
        Assert.Equal(1,restored.State.Citizens[0].CompletedJourneys);
        Assert.Equal(36,restored.State.Citizens[0].Car!.ParkedPosition.X);
    }

    [Fact]
    public void PendingAccessCannotBypassAnIncompatibleReplacementRoadNetwork()
    {
        var planner = new Planner(); var engine = Engine(1,2,0,planner);
        engine.AdvanceTo(1441);
        string original = SaveCodec.Write(engine.State);
        planner.TrafficNetwork = new Planner(native:new string('b',64)).TrafficNetwork;
        Assert.Throws<InvalidOperationException>(()=>engine.RefreshTrafficClosures());
        Assert.Equal(original,SaveCodec.Write(engine.State));
        Assert.Equal(0,engine.State.Citizens[0].CompletedJourneys);
    }

    [Fact]
    public void RoadNetworkChangeDoesNotBlockAlreadyCompletedDrivingAndRemainingEgress()
    {
        var planner = new Planner(); var engine = Engine(1,0,20,planner);
        engine.AdvanceTo(1445);
        Assert.Equal(0,engine.Traffic!.VehicleCount);
        planner.TrafficNetwork = new Planner(native:new string('b',64)).TrafficNetwork;
        engine.RefreshTrafficClosures(); engine.AdvanceTo(1470);
        Assert.Equal(1,engine.State.Citizens[0].CompletedJourneys);
    }

    [Fact]
    public void NativeFrameSlicesPublishOnceWithoutChangingTrafficOrSaveOrdering()
    {
        var ordinary=Engine(30,2,3);var sliced=Engine(30,2,3);
        ordinary.AdvanceTo(1530);
        int slices=0;while(!sliced.AdvanceFrameSlice(1530).CaughtUp) Assert.True(++slices<2000);
        sliced.SynchronizeSnapshot();
        Assert.Equal(SaveCodec.Write(ordinary.State),SaveCodec.Write(sliced.State));
    }

    [Fact]
    public void LiveQueueClockConversionRebindsTheSameSectionsAndPreservesPose()
    {
        var engine=Engine(4);engine.AdvanceTo(1440.5);engine.State.PrepareForSave();
        double[] positions=engine.State.Citizens.Select(c=>engine.PositionOf(c).X).ToArray();
        Assert.True(TravelTiming.Apply(engine.State,2,2));
        var converted=new CitySimulator(engine.State,new Planner(2));
        Assert.Equal(positions,converted.State.Citizens.Select(c=>converted.PositionOf(c).X));
        converted.AdvanceTo(1500);
        Assert.All(converted.State.Citizens,c=>Assert.Equal(1,c.CompletedJourneys));
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(converted.State)).Status);
    }

    [Fact]
    public void QueuesDelayRealArrivalsAndPreserveCarOwnership()
    {
        var engine = Engine();
        engine.AdvanceTo(1442.5);
        Assert.InRange(engine.State.Citizens.Count(c => c.CompletedJourneys == 1), 0, 2);
        Assert.Contains(engine.State.Citizens, c => c.Phase == CitizenPhase.Travelling && engine.PositionOf(c).X < 24);
        engine.AdvanceTo(1500);
        Assert.All(engine.State.Citizens, c =>
        { Assert.Equal(1,c.CompletedJourneys); Assert.Equal("work",c.PlaceId); Assert.Equal(24,c.Car!.ParkedPosition.X); });
        Assert.Equal(0,engine.Traffic!.VehicleCount);
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(engine.State)).Status);
    }

    [Fact]
    public void CoarseFineAndOneEventBudgetsProduceIdenticalTraffic()
    {
        var fine = Engine(12,2,3); var coarse = Engine(12,2,3); var budgeted = Engine(12,2,3);
        for (int i=0;i<=200;i++) fine.AdvanceTo(1440+i*.25);
        coarse.AdvanceTo(1490);
        int iterations=0;
        while (!budgeted.AdvanceTo(1490,1).CaughtUp) Assert.True(++iterations<1000);
        Assert.Equal(SaveCodec.Write(coarse.State),SaveCodec.Write(fine.State));
        Assert.Equal(SaveCodec.Write(coarse.State),SaveCodec.Write(budgeted.State));
    }

    [Fact]
    public void InterruptedQueuesReloadWithoutDuplicatingOrSkippingResidents()
    {
        var engine=Engine(15,2,3); engine.AdvanceTo(1445.1);
        string saved=SaveCodec.Write(engine.State); var read=SaveCodec.Read(saved);
        Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        var restored=new CitySimulator(read.State!,new Planner());
        Assert.Equal(engine.State.Citizens.Select(c=>engine.PositionOf(c).X),restored.State.Citizens.Select(c=>restored.PositionOf(c).X));
        engine.AdvanceTo(1510); while(!restored.AdvanceTo(1510,3).CaughtUp) { }
        Assert.Equal(SaveCodec.Write(engine.State),SaveCodec.Write(restored.State));
    }

    [Fact]
    public void VisibleHeadHoldsItsPlaceAndBackgroundFollowers()
    {
        var engine=Engine(4); engine.AdvanceTo(1440);
        var head=engine.State.Citizens[0];
        Assert.True(engine.TryAcquirePhysicalJourney(head.Identity.Id,out var lease));
        engine.AdvanceTo(1480);
        Assert.Equal(0,head.CompletedJourneys); Assert.Equal(0,engine.PositionOf(head).X);
        Assert.All(engine.State.Citizens,c=>Assert.Equal(0,c.CompletedJourneys));
        Assert.True(engine.ReleasePhysicalJourney(lease!));
        engine.AdvanceTo(1500);
        Assert.All(engine.State.Citizens,c=>Assert.Equal(1,c.CompletedJourneys));
    }

    [Fact]
    public void PhysicalOvershootCannotBypassFiniteStorage()
    {
        var engine=Engine(4); engine.AdvanceTo(1440);
        var citizen=engine.State.Citizens[0];
        Assert.True(engine.TryAcquirePhysicalJourney(citizen.Identity.Id,out var lease));
        Assert.False(engine.ObservePhysicalJourney(lease!,2.4));
        Assert.Equal(0,citizen.CompletedJourneys);
        Assert.InRange(engine.PositionOf(citizen).X,0,12);
        engine.ReleasePhysicalJourney(lease!);
        engine.AdvanceTo(1500); Assert.Equal(1,citizen.CompletedJourneys);
    }

    [Fact]
    public void SavedPhysicalQueueResumesAsSameBackgroundQueue()
    {
        var engine=Engine(4); engine.AdvanceTo(1440);
        Assert.True(engine.TryAcquirePhysicalJourney(engine.State.Citizens[0].Identity.Id,out var lease));
        engine.AdvanceTo(1440.5); Assert.True(engine.ObservePhysicalJourney(lease!,0.3));
        var read=SaveCodec.Read(SaveCodec.Write(engine.State)); Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        var restored=new CitySimulator(read.State!,new Planner()); engine.ReleasePhysicalJourney(lease!);
        engine.AdvanceTo(1500); restored.AdvanceTo(1500);
        Assert.Equal(SaveCodec.Write(engine.State),SaveCodec.Write(restored.State));
    }
}
