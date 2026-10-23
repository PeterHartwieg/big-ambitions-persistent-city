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
    public void PhysicalPredecessorMustPassCurbWithSpacingBeforeInteriorTerminalCanLeave()
    {
        var kernel=new TrafficKernel(new[]{Link(red:true)});kernel.Enqueue("through",Route());kernel.Enqueue("curb",Route(.5,1));kernel.Enqueue("rear",Route());kernel.AdvanceTo(0,100);
        kernel.SetPhysicalControl("through",true);kernel.AdvanceTo(100,100);
        kernel.ReportPhysicalProgress("through",7.4);kernel.AdvanceTo(100,100);
        Assert.False(kernel.GetPermission("curb").IsCompleted);Assert.Equal(0,kernel.PendingEventCount);
        kernel.ReportPhysicalProgress("through",7.5);kernel.AdvanceTo(100,100);
        Assert.Equal(100,kernel.GetPermission("curb").CompletedAt);
        Assert.Equal(new[]{"through","rear"},kernel.Capture().Links[0].Vehicles);
        Assert.False(kernel.GetPermission("through").IsCompleted);Assert.False(kernel.GetPermission("rear").IsCompleted);
    }

    [Fact]
    public void PhysicalCurbRequiresEndpointObservationAndCompletesCallbackExactlyOnce()
    {
        var kernel=new TrafficKernel(new[]{Link(red:true)});kernel.Enqueue("curb",Route(.5,1));kernel.AdvanceTo(0,100);kernel.SetPhysicalControl("curb",true);
        int callbacks=0;kernel.VehicleCompleted+=(id,at)=>{Assert.Equal("curb",id);Assert.Equal(100,at);callbacks++;Assert.True(kernel.RemoveCompleted(id));};
        kernel.AdvanceTo(100,100);Assert.Equal(0,callbacks);Assert.Equal(0,kernel.PendingEventCount);
        kernel.ReportPhysicalProgress("curb",1);kernel.AdvanceTo(100,1);kernel.AdvanceTo(200,100);
        Assert.Equal(1,callbacks);Assert.Equal(0,kernel.VehicleCount);
    }

    [Fact]
    public void InteriorCurbWakeupReloadAndOneEventBudgetsPreserveExactState()
    {
        var continuous=new TrafficKernel(new[]{Link(red:true)});continuous.Enqueue("through",Route());continuous.Enqueue("curb",Route(.5,1));continuous.AdvanceTo(0,100);
        var missingWake=continuous.Capture();missingWake.Events.Clear();
        Assert.Throws<ArgumentException>(()=>TrafficKernel.ValidateState(missingWake));
        var restored=TrafficKernel.Restore(continuous.Capture(),false);
        continuous.AdvanceTo(100,100);
        while(!restored.AdvanceTo(100,1).ReachedTarget)restored=TrafficKernel.Restore(restored.Capture(),false);
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

    [Fact]
    public void InteriorEntryExtrapolatesPhysicalCopiesButRefusesProvenFiniteStorageCycle()
    {
        var physical=new TrafficKernel(new[]{Link()});physical.Enqueue("body",Route());physical.AdvanceTo(0,100);physical.SetPhysicalControl("body",true);
        physical.AdvanceTo(100,100);string physicalBefore=Capture(physical);
        Assert.Equal(10,physical.ForecastEntry(0,100,.5));Assert.Equal(physicalBefore,Capture(physical));
        Assert.True(physical.GetPermission("body").PhysicalControl);Assert.Equal(0,physical.GetPermission("body").NominalMinutes);
        Assert.True(double.IsFinite(physical.Forecast(0,100).DelayMinutes));
        var cycle=new TrafficKernel(new[]{new TrafficLinkDefinition{Id=0,Capacity=1,FreeFlowMinutes=1},new TrafficLinkDefinition{Id=1,Capacity=1,FreeFlowMinutes=1}});
        cycle.Enqueue("a",new[]{new TrafficRouteLeg{LinkId=0,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=1,StartNominalMinutes=1,EndNominalMinutes=2}});
        cycle.Enqueue("b",new[]{new TrafficRouteLeg{LinkId=1,StartNominalMinutes=0,EndNominalMinutes=1},new TrafficRouteLeg{LinkId=0,StartNominalMinutes=1,EndNominalMinutes=2}});
        cycle.AdvanceTo(100,100);string before=Capture(cycle);
        Assert.True(double.IsPositiveInfinity(cycle.Forecast(0,100).DelayMinutes));Assert.True(double.IsPositiveInfinity(cycle.Forecast(1,100).DelayMinutes));
        Assert.True(double.IsPositiveInfinity(cycle.ForecastEntry(0,100,.5)));Assert.Equal(before,Capture(cycle));
        Assert.Equal(0,cycle.PendingEventCount);Assert.Equal(2,cycle.VehicleCount);
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
    public void PhysicalStopLineAdmissionAlreadyInOrdinaryForecastIsNotChargedAgain()
    {
        var link=Link(1,.1);link.Signal=new TrafficSignalSchedule{CycleMinutes=20,GreenStartMinutes=0,GreenDurationMinutes=1};
        var kernel=new TrafficKernel(new[]{link});kernel.Enqueue("body",Route());kernel.AdvanceTo(0,100);kernel.SetPhysicalControl("body",true);
        kernel.AdvanceTo(10,100);kernel.ReportPhysicalProgress("body",10);
        Assert.Equal(10,kernel.Forecast(0,10).DelayMinutes,9);Assert.Equal(0,kernel.ForecastEntry(0,10,.5));
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

    [Theory]
    [InlineData(0)]
    [InlineData(1000000000)]
    public void RestoreRejectsForgedEarlyReadinessWithoutRejectingLegitimatePhysicalHolds(double clock)
    {
        var kernel=new TrafficKernel(new[]{Link()},clock);kernel.Enqueue("car",Route());kernel.AdvanceTo(clock+.5,100);
        var forged=kernel.Capture();forged.Vehicles[0].ReadyAt=clock+.5;
        Assert.Throws<ArgumentException>(()=>TrafficKernel.ValidateState(forged));
        kernel.SetPhysicalControl("car",true);kernel.AdvanceTo(clock+100,100);
        TrafficKernel.ValidateState(kernel.Capture());
        var retimed=kernel.RescaleTime(.5);TrafficKernel.ValidateState(retimed.Capture());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetimeNearEndpointRetainsUnreportedPhysicalResidualInsteadOfZeroSpatialSpan(bool mapCoordinates)
    {
        var kernel=new TrafficKernel(new[]{Link(2)});kernel.Enqueue("car",Route(1,1,.9));kernel.AdvanceTo(0,100);kernel.SetPhysicalControl("car",true);kernel.AdvanceTo(10,100);
        double almostEnd=Math.BitDecrement(1);Assert.Equal(almostEnd,kernel.ReportPhysicalProgress("car",almostEnd));
        var retimed=kernel.RescaleTime(2,mapCoordinates?(id,p)=>100+p*.5:null);
        var state=retimed.Capture();TrafficKernel.ValidateState(state);
        var leg=state.Vehicles.Single().Route.Single();
        Assert.True(leg.StartLinkFraction<leg.EndLinkFraction);Assert.True(leg.StartNominalMinutes<leg.EndNominalMinutes);
        Assert.True(retimed.GetPermission("car").NominalMinutes<leg.EndNominalMinutes);
        retimed.AdvanceTo(100,100);Assert.False(retimed.GetPermission("car").IsCompleted);Assert.Equal(0,retimed.PendingEventCount);
        retimed.ReportPhysicalProgress("car",leg.EndNominalMinutes);retimed.AdvanceTo(100,100);
        Assert.True(retimed.GetPermission("car").IsCompleted);
    }
    [Theory]
    [InlineData(1)] [InlineData(11)] [InlineData(12)]
    public void PhysicalHeadCanReportItsExactAllowedEndpointAndEventuallyComplete(int capacity)
    {
        var kernel = new TrafficKernel(new[]
        {
            new TrafficLinkDefinition { Id = 0, Capacity = capacity, FreeFlowMinutes = 10 },
            new TrafficLinkDefinition { Id = 1, Capacity = capacity, FreeFlowMinutes = 10 }
        });
        kernel.Enqueue("body", new[]
        {
            new TrafficRouteLeg { LinkId = 0, StartNominalMinutes = 0, EndNominalMinutes = 10 },
            new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = 10, EndNominalMinutes = 20 }
        });
        kernel.AdvanceTo(0, 100); kernel.SetPhysicalControl("body", true);
        kernel.AdvanceTo(100, 100);
        Assert.Equal(0, kernel.GetPermission("body").LinkId);
        Assert.Equal(0, kernel.GetPermission("body").NominalMinutes);
        Assert.Equal(0, kernel.PendingEventCount); // Time alone never supplies physical arrival.
        var first = kernel.GetPermission("body");
        Assert.True(first.CanCrossBoundary); Assert.Equal(10, first.MaximumNominalMinutes);
        Assert.Equal(10, kernel.ReportPhysicalProgress("body", first.MaximumNominalMinutes));
        kernel.AdvanceTo(100, 100);
        Assert.Equal(1, kernel.GetPermission("body").LinkId);
        Assert.Empty(kernel.Capture().Links.Single(link => link.LinkId == 0).Vehicles);
        Assert.Equal(new[] { "body" }, kernel.Capture().Links.Single(link => link.LinkId == 1).Vehicles);
        int completed = 0; kernel.VehicleCompleted += (_, _) => completed++;
        kernel.AdvanceTo(200, 100);
        Assert.False(kernel.GetPermission("body").IsCompleted);
        Assert.Equal(20, kernel.ReportPhysicalProgress("body", kernel.GetPermission("body").MaximumNominalMinutes));
        kernel.AdvanceTo(200, 100); kernel.AdvanceTo(300, 100);
        Assert.True(kernel.GetPermission("body").IsCompleted); Assert.Equal(1, completed);
        Assert.All(kernel.Capture().Links, link => Assert.Empty(link.Vehicles));
        Assert.Equal(0, kernel.PendingEventCount); Assert.Equal(1, kernel.VehicleCount);
    }

    [Fact]
    public void FullyAllowedPhysicalHeadUsesCommittedEndpointInsteadOfRoundedSpanReconstruction()
    {
        const double start = 98.60761910894645, end = 250.12917554692152;
        Assert.True(start + (end - start) < end); // Independent arithmetic regression fixture.
        var kernel = new TrafficKernel(new[] { Link(1) });
        kernel.Enqueue("body", new[] { new TrafficRouteLeg { LinkId = 0,
            StartNominalMinutes = start, EndNominalMinutes = end, FreeFlowMinutes = 10 } });
        kernel.AdvanceTo(0, 100); kernel.SetPhysicalControl("body", true); kernel.AdvanceTo(100, 100);
        Assert.Equal(end, kernel.GetPermission("body").MaximumNominalMinutes);
        Assert.Equal(end, kernel.ReportPhysicalProgress("body", kernel.GetPermission("body").MaximumNominalMinutes));
        kernel.AdvanceTo(100, 100);
        Assert.True(kernel.GetPermission("body").IsCompleted); Assert.Equal(end, kernel.GetPermission("body").NominalMinutes);
        Assert.Equal(0, kernel.PendingEventCount);
    }

    [Theory]
    [InlineData(11)] [InlineData(12)]
    public void ExactHeadEndpointPreservesFollowerSpacingAndFiniteSourceStorage(int capacity)
    {
        var kernel = new TrafficKernel(new[] { Link(capacity) });
        for (int i = 0; i <= capacity; i++) kernel.Enqueue("car" + i, Route());
        kernel.AdvanceTo(0, 1000);
        for (int i = 0; i < capacity; i++) kernel.SetPhysicalControl("car" + i, true);
        kernel.AdvanceTo(100, 1000);
        Assert.Equal(capacity, kernel.Capture().Links.Single().Vehicles.Count);
        Assert.Single(kernel.Capture().Links.Single().WaitingSources);
        Assert.Null(kernel.GetPermission("car" + capacity).LinkId);
        Assert.Equal(10, kernel.GetPermission("car0").MaximumNominalMinutes);
        Assert.Equal(0, kernel.GetPermission("car1").MaximumNominalMinutes); // Unobserved head still occupies its source point.
        kernel.ReportPhysicalProgress("car0", 10);
        double follower = kernel.GetPermission("car1").MaximumNominalMinutes;
        Assert.True(follower < 10);
        Assert.Equal((1 - 1.0 / capacity) * 10, follower);
        Assert.Equal(0, kernel.GetPermission("car2").MaximumNominalMinutes);
        kernel.AdvanceTo(100, 1000);
        Assert.True(kernel.GetPermission("car0").IsCompleted);
        Assert.Equal(capacity, kernel.Capture().Links.Single().Vehicles.Count);
        Assert.Empty(kernel.Capture().Links.Single().WaitingSources);
        Assert.Equal(10, kernel.GetPermission("car1").MaximumNominalMinutes);
        TrafficKernel.ValidateState(kernel.Capture());
    }

    [Fact]
    public void StalledInteriorPhysicalProgressCannotRegressOnReleaseRestoreOrReacquire()
    {
        const double start = .9518824138568586, end = 28.182987254561827, clock = .5864380512395461;
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition { Id = 0, Capacity = 1, FreeFlowMinutes = 1 } });
        kernel.Enqueue("body", new[] { new TrafficRouteLeg { LinkId = 0,
            StartNominalMinutes = start, EndNominalMinutes = end, FreeFlowMinutes = 1 } });
        kernel.AdvanceTo(0, 100); kernel.SetPhysicalControl("body", true); kernel.AdvanceTo(clock, 100);
        double observed = kernel.ReportPhysicalProgress("body", kernel.GetPermission("body").MaximumNominalMinutes);
        Assert.Equal(16.92123846973965, observed);
        Assert.True(start + ((observed - start) / (end - start)) * (end - start) < observed);
        var restored = TrafficKernel.Restore(kernel.Capture()); // Saved bodies resume background at the same actual point.
        Assert.Equal(observed, restored.GetPermission("body").NominalMinutes);
        kernel.SetPhysicalControl("body", false);
        Assert.Equal(observed, kernel.GetPermission("body").MaximumNominalMinutes);
        Assert.Equal(observed, kernel.GetPermission("body").NominalMinutes);
        kernel.SetPhysicalControl("body", true);
        Assert.Equal(observed, kernel.GetPermission("body").NominalMinutes);
        Assert.Equal(observed, kernel.GetPermission("body").MaximumNominalMinutes);
        Assert.False(kernel.GetPermission("body").IsCompleted);
        Assert.Equal(new[] { "body" }, kernel.Capture().Links.Single().Vehicles);
        restored.AdvanceTo(100, 100); Assert.True(restored.GetPermission("body").IsCompleted);
    }

}
