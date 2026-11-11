using System;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficBoundaryTests
{
    private static TrafficLinkDefinition Link(int capacity=4,double headway=0,bool red=false)=>new()
    {
        Id=0,Capacity=capacity,FreeFlowMinutes=10,HeadwayMinutes=headway,
        Signal=red?new TrafficSignalSchedule{CycleMinutes=10,GreenDurationMinutes=0}:null
    };
    private static TrafficRouteLeg[] Route(double end=1,double minutes=10,double start=0)=>new[]
    {new TrafficRouteLeg{LinkId=0,StartNominalMinutes=0,EndNominalMinutes=minutes,FreeFlowMinutes=minutes,StartLinkFraction=start,EndLinkFraction=end}};
    private static string Capture(TrafficKernel kernel)=>JsonSerializer.Serialize(kernel.Capture());

    [Fact]
    public void CurbExitDoesNotWaitForFarEndRedSignal()
    {
        var kernel=new TrafficKernel(new[]{Link(red:true)});kernel.Enqueue("curb",Route(.5,1));
        kernel.AdvanceTo(.999,100);Assert.False(kernel.GetPermission("curb").IsCompleted);
        kernel.AdvanceTo(1,100);Assert.Equal(1,kernel.GetPermission("curb").CompletedAt);
        Assert.Equal(0,kernel.PendingEventCount);
    }

    [Fact]
    public void CurbExitDoesNotConsumeOrWaitForFarEndDischargeHeadway()
    {
        var kernel=new TrafficKernel(new[]{Link(headway:100)});kernel.Enqueue("through",Route(minutes:1));kernel.AdvanceTo(1,100);
        kernel.Enqueue("curb",Route(.5,1));kernel.AdvanceTo(2,100);
        Assert.Equal(2,kernel.GetPermission("curb").CompletedAt);
        Assert.Equal(101,kernel.Capture().Links[0].NextDischargeAt);
    }

    [Fact]
    public void BackgroundCurbExitSchedulesActualPredecessorClearanceWithoutPollingOrRedBypass()
    {
        var kernel=new TrafficKernel(new[]{Link(red:true)});kernel.Enqueue("through",Route());kernel.Enqueue("curb",Route(.5,1));kernel.AdvanceTo(0,100);
        Assert.Equal(7.5,kernel.NextEventMinute);
        kernel.AdvanceTo(7.49,100);Assert.False(kernel.GetPermission("curb").IsCompleted);
        var result=kernel.AdvanceTo(100,100);
        Assert.Equal(1,result.ProcessedEvents);Assert.Equal(7.5,kernel.GetPermission("curb").CompletedAt);
        Assert.False(kernel.GetPermission("through").IsCompleted);Assert.Equal(0,kernel.PendingEventCount);
        Assert.Equal(new[]{"through"},kernel.Capture().Links[0].Vehicles);
    }

    [Fact]
    public void InteriorCurbWakeupReloadAndOneEventBudgetsPreserveExactState()
    {
        var continuous=new TrafficKernel(new[]{Link(red:true)});continuous.Enqueue("through",Route());continuous.Enqueue("curb",Route(.5,1));continuous.AdvanceTo(0,100);
        var missingWake=continuous.Capture();missingWake.Events.Clear();
        Assert.Throws<ArgumentException>(()=>TrafficKernel.ValidateState(missingWake));
        var restored=TrafficKernel.Restore(continuous.Capture());
        continuous.AdvanceTo(100,100);
        while(!restored.AdvanceTo(100,1).ReachedTarget)restored=TrafficKernel.Restore(restored.Capture());
        Assert.Equal(Capture(continuous),Capture(restored));Assert.Equal(7.5,restored.GetPermission("curb").CompletedAt);
    }

    [Theory]
    [InlineData(10000000,.5,.572)]
    [InlineData(10000000,2,.572)]
    [InlineData(1000000000,.5,18.304)]
    [InlineData(1000000000,2,18.304)]
    public void SignalRetimingAtLargeClockAcceptsOnlyFloatingPointWakeupNoise(double clock,double factor,double phase)
    {
        var link=Link(1,.17);link.Signal=new TrafficSignalSchedule{CycleMinutes=1.13,GreenStartMinutes=.33,GreenDurationMinutes=.07,PhaseOffsetMinutes=phase};
        var kernel=new TrafficKernel(new[]{link},clock);kernel.Enqueue("car",Route(minutes:3.7));kernel.AdvanceTo(clock+.37,100);
        var retimed=kernel.RescaleTime(factor);TrafficKernel.ValidateState(retimed.Capture());
        retimed.AdvanceTo(clock+100,1000);Assert.True(retimed.GetPermission("car").IsCompleted);
        Assert.Equal(0,retimed.PendingEventCount);
    }

    [Fact]
    public void LargeClockValidationStillRejectsMissingAndMateriallyLateWakeups()
    {
        var kernel=new TrafficKernel(new[]{Link(1)},1e9);kernel.Enqueue("car",Route());kernel.AdvanceTo(1e9,100);
        var missing=kernel.Capture();missing.Events.Clear();Assert.Throws<ArgumentException>(()=>TrafficKernel.ValidateState(missing));
        var late=kernel.Capture();late.Events[0].At+=.0001;Assert.Throws<ArgumentException>(()=>TrafficKernel.ValidateState(late));
    }

    [Fact]
    public void InteriorEntryAddsOnlyMissingEmptySectionWaitAndIsReadOnly()
    {
        var kernel=new TrafficKernel(new[]{Link(4,.1)});kernel.Enqueue("head",Route());kernel.AdvanceTo(0,100);
        string before=Capture(kernel);double ordinary=kernel.Forecast(0,0).DelayMinutes;
        Assert.Equal(.1,ordinary,9);Assert.Equal(9.9,kernel.ForecastEntry(0,0,.5),9);
        Assert.Equal(10,ordinary+kernel.ForecastEntry(0,0,.5),9);Assert.Equal(0,kernel.ForecastEntry(0,0,0));
        Assert.Equal(9.95,kernel.ForecastEntry(0,0,.5,.5),9);
        Assert.Equal(10,ordinary*.5+kernel.ForecastEntry(0,0,.5,.5),9);
        Assert.Throws<ArgumentOutOfRangeException>(()=>kernel.ForecastEntry(0,0,.5,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>kernel.ForecastEntry(0,0,.5,1.01));
        Assert.Equal(before,Capture(kernel));
    }

    [Fact]
    public void InteriorEntryProjectsExistingIncomingAndFullReceivingQueues()
    {
        var links=new[]{new TrafficLinkDefinition{Id=0,Capacity=1,FreeFlowMinutes=1},new TrafficLinkDefinition{Id=1,Capacity=1,FreeFlowMinutes=1}};
        var incoming=new TrafficKernel(links);incoming.Enqueue("head",Route(minutes:10));
        incoming.Enqueue("incoming",new[]{new TrafficRouteLeg{LinkId=1,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=0,StartNominalMinutes=1,EndNominalMinutes=2}});
        incoming.AdvanceTo(0,100);string before=Capture(incoming);
        Assert.Equal(11,incoming.ForecastEntry(0,0,.5));Assert.Equal(before,Capture(incoming));
        var downstream=new TrafficKernel(links);downstream.Enqueue("receiver",new[]{new TrafficRouteLeg{LinkId=1,StartNominalMinutes=0,EndNominalMinutes=10}});
        downstream.Enqueue("waiting",new[]{new TrafficRouteLeg{LinkId=0,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=1,StartNominalMinutes=1,EndNominalMinutes=2}});
        downstream.AdvanceTo(0,100);Assert.Equal(10,downstream.ForecastEntry(0,0,.5));
    }

    [Theory]
    [InlineData(.9,true)]
    [InlineData(.25,false)]
    public void FullCycleClassificationAccountsForWhetherRearCurbsCanActuallyFreeStorage(double curbEnd,bool blocked)
    {
        var kernel=new TrafficKernel(new[]{new TrafficLinkDefinition{Id=0,Capacity=2,FreeFlowMinutes=1},new TrafficLinkDefinition{Id=1,Capacity=2,FreeFlowMinutes=1}});
        kernel.Enqueue("a",new[]{new TrafficRouteLeg{LinkId=0,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=1,StartNominalMinutes=1,EndNominalMinutes=2}});
        kernel.Enqueue("curbA",new[]{new TrafficRouteLeg{LinkId=0,StartNominalMinutes=0,EndNominalMinutes=1,EndLinkFraction=curbEnd}});
        kernel.Enqueue("b",new[]{new TrafficRouteLeg{LinkId=1,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=0,StartNominalMinutes=1,EndNominalMinutes=2}});
        kernel.Enqueue("curbB",new[]{new TrafficRouteLeg{LinkId=1,StartNominalMinutes=0,EndNominalMinutes=1,EndLinkFraction=curbEnd}});
        kernel.AdvanceTo(0,100);string before=Capture(kernel);
        Assert.Equal(blocked,double.IsPositiveInfinity(kernel.Forecast(0,0).DelayMinutes));Assert.Equal(before,Capture(kernel));
        kernel.AdvanceTo(100,100);
        Assert.All(kernel.Capture().Vehicles,v=>Assert.Equal(!blocked,v.Completed));Assert.Equal(0,kernel.PendingEventCount);
    }

    [Fact]
    public void InteriorProjectionRefusesDependenciesBeyondDeclaredBoundsWithoutMutatingDemand()
    {
        var links=Enumerable.Range(0,33).Select(id=>new TrafficLinkDefinition{Id=id,Capacity=1,FreeFlowMinutes=1});
        var kernel=new TrafficKernel(links);kernel.Enqueue("long",Enumerable.Range(0,33).Select(id=>new TrafficRouteLeg{LinkId=id,StartNominalMinutes=id,EndNominalMinutes=id+1}).ToArray());
        kernel.AdvanceTo(0,100);string before=Capture(kernel);Assert.True(double.IsPositiveInfinity(kernel.ForecastEntry(0,0,.5)));Assert.Equal(before,Capture(kernel));
        var queued=new TrafficKernel(new[]{Link()});for(int i=0;i<64;i++)queued.Enqueue("v"+i,Route());queued.AdvanceTo(0,100);
        Assert.True(double.IsFinite(queued.ForecastEntry(0,0,.5)));
        queued.Enqueue("over-bound",Route());
        Assert.True(double.IsPositiveInfinity(queued.ForecastEntry(0,0,.5)));Assert.Equal(65,queued.VehicleCount);
        var empty=new TrafficKernel(new[]{Link()});Assert.Equal(0,empty.ForecastEntry(0,0,.5));
    }

}
