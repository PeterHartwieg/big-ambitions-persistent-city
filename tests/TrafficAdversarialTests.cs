using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficAdversarialTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; }
        public HashSet<int> Closed { get; } = new();
        public Planner()
        {
            TrafficNetwork = new TrafficNetwork(Native, new[]
            {
                new TrafficNetworkNode { Id=0,Position=new Position(0,0,0),KilometresPerHour=36,AllowsCar=true,Boundary=true,Successors=new[]{1}},
                new TrafficNetworkNode { Id=1,Position=new Position(12,0,0),KilometresPerHour=36,AllowsCar=true,Boundary=true,Successors=new[]{2}},
                new TrafficNetworkNode { Id=2,Position=new Position(24,0,0),KilometresPerHour=36,AllowsCar=true,Boundary=true}
            },1);
        }
        public void SetTrafficForecast(Func<int,double,TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => Closed.Contains(linkId);
        public Route? Plan(Place from,Place to,double departureMinute,TravelPreferences preferences) => null;
    }
    private static CitySimulator City(int count, Planner planner, double access=2,double egress=3)
    {
        var state=Population.Create(Enumerable.Range(0,count).Select(i=>new CitizenIdentity
        {Id="adversary"+i.ToString("D4"),Name="Commuter "+i,Age=30,Persona=Persona.Commuter}),new[]
        {
            new Place{Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=count,Position=new Position(-access*2,0,0)},
            new Place{Id="work",Name="Work",Kind=PlaceKind.Work,Capacity=count,Position=new Position(24+egress*2,0,0)}
        },"adversarial-traffic","0.40.4",7,1440);
        foreach(var c in state.Citizens)
        {
            c.PlaceId=c.HomeId="home";c.DayPlaceId="work";c.Phase=CitizenPhase.Travelling;c.DestinationId="work";
            c.DestinationActivity=Activity.Work;c.ActivityUntilMinute=5000;c.DepartureMinute=1440;c.Preferences.OwnsCar=true;
            c.Car=new CarState{Id=c.Identity.Id+":car",ParkedPosition=new Position(0,0,0)};
            c.Journey=new Route();
            if(access>0)c.Journey.Legs.Add(new RouteLeg{From=state.Places[0].Position,To=new Position(0,0,0),Minutes=access,Mode=TravelMode.Walk});
            c.Journey.Legs.Add(new RouteLeg{From=new Position(0,0,0),To=new Position(12,0,0),Minutes=1.2,Mode=TravelMode.Car,ServiceId="native:road:v1:"+Native+":0:1"});
            c.Journey.Legs.Add(new RouteLeg{From=new Position(12,0,0),To=new Position(24,0,0),Minutes=1.2,Mode=TravelMode.Car,ServiceId="native:road:v1:"+Native+":1:2"});
            if(egress>0)c.Journey.Legs.Add(new RouteLeg{From=new Position(24,0,0),To=state.Places[1].Position,Minutes=egress,Mode=TravelMode.Walk});
            c.NextEventMinute=1440+c.Journey.Minutes;
        }
        return new CitySimulator(state,planner);
    }
    private static TrafficLinkDefinition Link(int id,int capacity=2)=>new(){Id=id,Capacity=capacity,FreeFlowMinutes=1,HeadwayMinutes=.1};
    private static List<TrafficRouteLeg> Route(params int[] links)=>links.Select((id,i)=>new TrafficRouteLeg{LinkId=id,StartNominalMinutes=i,EndNominalMinutes=i+1}).ToList();
    private static void Conserved(TrafficKernel kernel,int expected)
    {
        var state=kernel.Capture();
        var owned=state.Links.SelectMany(l=>l.Vehicles.Concat(l.WaitingSources)).ToArray();
        Assert.Equal(expected,state.Vehicles.Count);
        Assert.Equal(owned.Length,owned.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(state.Vehicles.Where(v=>!v.Completed).Select(v=>v.VehicleId).OrderBy(id=>id),owned.OrderBy(id=>id));
        Assert.All(state.Links,l=>Assert.InRange(l.Vehicles.Count,0,state.Definitions.Single(d=>d.Id==l.LinkId).Capacity));
        Assert.InRange(kernel.PendingEventCount,0,kernel.LinkCount);
    }

    [Fact]
    public void PhysicalAccessWalkCannotBypassClosedCarSection()
    {
        var planner=new Planner();planner.Closed.Add(0);
        var city=City(1,planner);city.AdvanceTo(1440);
        var citizen=city.State.Citizens[0];
        Assert.True(city.TryAcquirePhysicalJourney(citizen.Identity.Id,out var lease));
        city.AdvanceTo(1450);
        Assert.True(city.ObservePhysicalJourney(lease!,2));
        city.AdvanceTo(1450);
        Assert.True(city.TryGetTrafficPermission(citizen.Identity.Id,out var permission));
        Assert.Null(permission.LinkId);
        Assert.False(city.ObservePhysicalJourney(lease!,4.4));
        Assert.Equal(0,citizen.CompletedJourneys);
        Assert.Equal(0,city.CarPositionOf(citizen)!.Value.X);
    }

    [Fact]
    public void FiniteCycleGridlockSleepsAndCancellingOneOwnerWakesTheOther()
    {
        var kernel=new TrafficKernel(new[]{Link(1,1),Link(2,1)});
        kernel.Enqueue("clockwise",Route(1,2));kernel.Enqueue("counterclockwise",Route(2,1));
        kernel.AdvanceTo(100,100);
        Conserved(kernel,2);Assert.Equal(0,kernel.PendingEventCount);
        Assert.All(kernel.Capture().Vehicles,v=>Assert.False(v.Completed));
        Assert.True(kernel.Cancel("clockwise"));
        kernel.AdvanceTo(200,100);
        Conserved(kernel,1);Assert.True(kernel.GetPermission("counterclockwise").IsCompleted);
    }

    [Fact]
    public void RepeatedClosureAndPhysicalReleaseAreDeterministicAcrossEveryEventReload()
    {
        TrafficKernel Make()
        {
            var k=new TrafficKernel(new[]{Link(1,4),Link(2,3),Link(3,2),Link(4,2)});
            for(int i=0;i<91;i++)k.Enqueue("v"+i.ToString("D3"),i%3==0?Route(2,3,4):Route(1,3,4));
            return k;
        }
        var whole=Make();var interrupted=Make();
        void Advance(double at)
        {
            whole.AdvanceTo(at,10000);int turns=0;
            while(!interrupted.AdvanceTo(at,1).ReachedTarget)
            {
                Conserved(interrupted,91);interrupted=TrafficKernel.Restore(interrupted.Capture(),false);
                Assert.True(++turns<10000);
            }
            Conserved(whole,91);Conserved(interrupted,91);
            Assert.Equal(JsonSerializer.Serialize(whole.Capture()),JsonSerializer.Serialize(interrupted.Capture()));
        }
        Advance(0);
        whole.SetPhysicalControl("v000",true);interrupted.SetPhysicalControl("v000",true);
        whole.SetLinkClosed(4,true);interrupted.SetLinkClosed(4,true);Advance(20);
        whole.ReportPhysicalProgress("v000",.5);interrupted.ReportPhysicalProgress("v000",.5);Advance(25);
        whole.SetPhysicalControl("v000",false);interrupted.SetPhysicalControl("v000",false);Advance(30);
        whole.SetLinkClosed(4,false);interrupted.SetLinkClosed(4,false);Advance(32);
        whole.SetLinkClosed(3,true);interrupted.SetLinkClosed(3,true);Advance(50);
        whole.SetLinkClosed(3,false);interrupted.SetLinkClosed(3,false);Advance(200);
        Assert.All(whole.Capture().Vehicles,v=>Assert.True(v.Completed));
    }

    [Fact]
    public void FullCommuterWaveSurvivesClosureDormancyBudgetReloadAndArrivesExactlyOnce()
    {
        const int commuters=957;
        var planner=new Planner();planner.Closed.Add(1);
        var city=City(commuters,planner);city.AdvanceTo(1460);
        Assert.Equal(commuters,city.Traffic!.VehicleCount);
        Assert.Equal(0,city.Traffic.PendingEventCount);
        Conserved(city.Traffic,commuters);
        var result=SaveCodec.Read(SaveCodec.Write(city.State));Assert.True(result.Status==SaveReadStatus.Loaded,result.Error);
        var newPlanner=new Planner();newPlanner.Closed.Add(1);var restored=new CitySimulator(result.State!,newPlanner);
        planner.Closed.Clear();newPlanner.Closed.Clear();city.RefreshTrafficClosures();restored.RefreshTrafficClosures();
        int turns=0;
        city.AdvanceTo(3600);
        while(!restored.AdvanceTo(3600,7).CaughtUp)Assert.True(++turns<10000);
        Assert.All(restored.State.Citizens,c=>
        {Assert.Equal(1,c.CompletedJourneys);Assert.Equal("work",c.PlaceId);Assert.Equal(24,c.Car!.ParkedPosition.X);});
        Assert.Equal(0,restored.Traffic!.VehicleCount);
        Assert.Equal(SaveCodec.Write(city.State),SaveCodec.Write(restored.State));
    }

    [Theory]
    [InlineData(1,0)]
    [InlineData(6,0)]
    [InlineData(1,2)]
    [InlineData(6,2)]
    public void NativeFrameSlicesPublishSameBudgetedCityAsPerEventSnapshots(int speed,int physical)
    {
        var oldCity=City(31,new Planner(),0,3);var batchedCity=City(31,new Planner(),0,3);
        oldCity.AdvanceTo(1440);batchedCity.AdvanceTo(1440);
        for(int i=0;i<physical;i++)
        {
            var oldCitizen=oldCity.State.Citizens[i];
            Assert.True(oldCity.TryAcquirePhysicalJourney(oldCitizen.Identity.Id,out _));
            Assert.True(batchedCity.TryAcquirePhysicalJourney(oldCitizen.Identity.Id,out _));
        }
        for(int frame=1;frame<=60;frame++)
        {
            double target=1440+frame*.05*speed;int oldEvents=0,newEvents=0;bool oldCaught=false,newCaught=false;
            for(int step=0;step<16;step++)
            {
                var result=oldCity.AdvanceTo(target,1);oldEvents+=result.Events;
                if(result.CaughtUp){oldCaught=true;break;}
            }
            for(int step=0;step<16;step++)
            {
                var result=batchedCity.AdvanceFrameSlice(target,1);newEvents+=result.Events;
                if(result.CaughtUp){newCaught=true;break;}
            }
            batchedCity.SynchronizeSnapshot();
            Assert.Equal(oldEvents,newEvents);Assert.Equal(oldCaught,newCaught);Assert.Equal(oldCity.State.Minute,batchedCity.State.Minute);
            Assert.Equal(oldCity.State.Citizens.Select(c=>c.DepartureMinute),batchedCity.State.Citizens.Select(c=>c.DepartureMinute));
            Assert.Equal(oldCity.State.Citizens.Select(c=>c.NextEventMinute),batchedCity.State.Citizens.Select(c=>c.NextEventMinute));
        }
        Assert.Equal(SaveCodec.Write(oldCity.State),SaveCodec.Write(batchedCity.State));
    }

    [Fact]
    public void LeaseSurvivesLastRoadTransferThenEgressWithoutSecondAdmission()
    {
        var city=City(1,new Planner(),0,3);city.AdvanceTo(1440);
        var citizen=city.State.Citizens[0];
        Assert.True(city.TryAcquirePhysicalJourney(citizen.Identity.Id,out var lease));
        city.AdvanceTo(1442);Assert.True(city.ObservePhysicalJourney(lease!,1.2));city.AdvanceTo(1442);
        city.AdvanceTo(1444);Assert.True(city.ObservePhysicalJourney(lease!,2.4));city.AdvanceTo(1444);
        Assert.False(city.TryGetTrafficPermission(citizen.Identity.Id,out _));
        Assert.True(city.IsPhysicalJourneyValid(lease!));Assert.Equal(0,citizen.CompletedJourneys);
        city.AdvanceTo(1450);Assert.True(city.ObservePhysicalJourney(lease!,5.4));
        Assert.Equal(0,citizen.CompletedJourneys);city.AdvanceTo(1450);
        Assert.Equal(1,citizen.CompletedJourneys);Assert.False(city.IsPhysicalJourneyValid(lease!));
        Assert.False(city.ReleasePhysicalJourney(lease!));city.AdvanceTo(1500);Assert.Equal(1,citizen.CompletedJourneys);
    }
}
