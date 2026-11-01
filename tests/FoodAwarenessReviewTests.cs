using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodAwarenessReviewTests
{
    internal static (CitySimulator city, WeightedFoodChoiceTests.Market market, WeightedFoodChoiceTests.Planner planner) AtStop(int draws)
    {
        var (prepared,market,planner)=WeightedFoodChoiceTests.Ready(79,1024);
        var state=prepared.State;state.Food!.AwarenessStartsDay=1;
        foreach(var place in state.Places.Where(p=>p.Kind==PlaceKind.Shop)) place.Position=new Position(10,0,0);
        var person=state.Citizens[0];var meal=state.Food.Residents[0];
        person.PlaceId="s1023";person.Activity=Activity.Shop;person.Phase=CitizenPhase.AtPlace;
        person.NextEventMinute=person.ActivityUntilMinute=meal.DesiredAt+90;
        person.RecentVisits.Add(new VisitRecord {AtMinute=state.Minute,PlaceId=person.PlaceId,Activity=Activity.Shop});
        var city=new CitySimulator(state,planner);city.AttachFoodMarket(market,awarenessSettings:new FoodAwarenessSettings {DrawsPerAnchor=draws});
        city.InspectFoodAwareness("r"); // explicit diagnostic preparation, outside the measured decision unit
        return (city,market,planner);
    }
    [Theory] [InlineData(0)] [InlineData(4)]
    public void PresentShopAlwaysOccupiesAnExistingOriginSlotInDenseAtStopContext(int draws)
    {
        var (city,_,planner)=AtStop(draws);var meal=city.State.Food!.Residents[0];
        city.AdvanceTo(meal.DesiredAt,1);
        var d=meal.Decision!;Assert.StartsWith("stop:",d.ContextKey);
        Assert.Equal("b1023",Assert.Single(d.Shops).BusinessId);
        var origin=Assert.Single(d.Awareness!.Union,c=>c.BusinessId=="b1023");
        Assert.True(origin.Eligible);Assert.True((origin.Sources & FoodAwarenessSource.Origin)!=0);
        Assert.InRange(d.Awareness.Union.Count,1,24);Assert.Equal(0,planner.Out+planner.Back);
    }
    [Fact]
    public void AtStopDiagnosticMatchesTheHeldSourceImageAndShortlist()
    {
        var (city,_,_)=AtStop(4);var meal=city.State.Food!.Residents[0];city.AdvanceTo(meal.DesiredAt,1);
        var dump=city.InspectFoodAwareness("r")!;
        Assert.Equal(JsonConvert.SerializeObject(meal.Decision!.Awareness),JsonConvert.SerializeObject(dump.Image));
        Assert.Equal(meal.Decision.Shops.Select(s=>s.BusinessId),dump.Shortlist);
    }
    [Fact]
    public void PostActivationNullImageRefusesButPreActivationHeldImageStillLoads()
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,24);city.State.Food!.AwarenessStartsDay=1;
        city.InspectFoodAwareness("r");var meal=city.State.Food.Residents[0];city.AdvanceTo(meal.DesiredAt,1);
        var json=JObject.Parse(SaveCodec.Write(city.State));json["Food"]!["Residents"]![0]![9]!["Awareness"]=null;
        string source=json.ToString(Formatting.None);
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(source).Status);
        meal.Decision!.Awareness=null;Assert.Throws<SaveValidationException>(()=>SaveCodec.Write(city.State));
        city.State.Food.AwarenessStartsDay=meal.Day+1;
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(city.State)).Status);
    }
    [Fact]
    public void InvalidationCannotRebuildAndCaptureWithinOneNormalWorkUnit()
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,1024);city.State.Food!.AwarenessStartsDay=1;
        city.InspectFoodAwareness("r");city.InvalidateFoodAwareness();var meal=city.State.Food.Residents[0];
        city.AdvanceFrameSlice(meal.DesiredAt,1);
        Assert.Null(meal.Decision);Assert.Equal(0,meal.ContextAttempts);
    }
    [Theory] [InlineData(1024)] [InlineData(20000)]
    public void BuilderBoundsEveryUnitAndPublishesOnlyACompletePermutationIndependentImage(int count)
    {
        var entries=Enumerable.Range(0,count).Select(i=>new FoodAwarenessCatalogue.Entry("g"+i.ToString("D5"),"p"+i,new Position(i%200*125,0,i/200*125))).ToArray();
        var expected=new FoodAwarenessCatalogue(entries);
        using var builder=FoodAwarenessCatalogue.BeginBuild(entries.Reverse());
        Assert.Throws<InvalidOperationException>(()=>builder.Result);
        int units=0;
        while(!builder.Continue())
        {
            units++;Assert.InRange(builder.LastWorkItems,0,FoodAwarenessCatalogue.Builder.MaximumWorkPerUnit);
            Assert.Throws<InvalidOperationException>(()=>builder.Result);
        }
        Assert.True(units>count/FoodAwarenessCatalogue.Builder.MaximumWorkPerUnit);
        Assert.Equal(expected.Revision,builder.Result.Revision);
        var settings=new FoodAwarenessSettings();
        for(int day=0;day<16;day++)
        {
            FoodAwarenessImage Sample(FoodAwarenessCatalogue c)=>c.Sample(77,"consumer",day,new(0,0,0),new(0,0,0),new(0,0,0),settings,_=>true);
            Assert.Equal(JsonConvert.SerializeObject(Sample(expected)),JsonConvert.SerializeObject(Sample(builder.Result)));
        }
    }
    [Fact]
    public void UncapturedEventReloadAndIndexRestartPreserveDecisionsAndConsumeNoAttempt()
    {
        var (a,ma,pa)=WeightedFoodChoiceTests.Ready(79,1024);a.State.Food!.AwarenessStartsDay=1;
        var (b,mb,pb)=WeightedFoodChoiceTests.Ready(79,1024);b.State.Food!.AwarenessStartsDay=1;
        double at=b.State.Food.Residents[0].DesiredAt;
        for(int i=0;i<12;i++)
        {
            var work=b.AdvanceFrameSlice(at,1);Assert.Equal(1,work.FoodContinuationEvents);
            Assert.Equal(at,b.State.Minute);Assert.Null(b.State.Food.Residents[0].Decision);Assert.Equal(0,b.State.Food.Residents[0].ContextAttempts);
        }
        b.InvalidateFoodAwareness();b.AdvanceFrameSlice(at,1); // discard partial private image
        var source=SaveCodec.Write(b.State);var loaded=SaveCodec.Read(source);Assert.True(loaded.Status==SaveReadStatus.Loaded,loaded.Error);
        b=new CitySimulator(loaded.State!,pb);b.AttachFoodMarket(mb);
        while(!a.AdvanceTo(at,128).CaughtUp) {}
        while(!b.AdvanceTo(at,1).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(a.State),SaveCodec.Write(b.State));Assert.Equal(ma.Orders,mb.Orders);Assert.Equal(pa.Out,pb.Out);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void LatePreActivationScopeHasIdenticalStopDecisionAcrossReloadAndInspection(int inspection)
    {
        (CitySimulator city,WeightedFoodChoiceTests.Market market,WeightedFoodChoiceTests.Planner planner) LateScope(bool inspect)
        {
            var (prepared,market,planner)=WeightedFoodChoiceTests.Ready(79,2);
            var state=prepared.State;var food=state.Food!;food.AwarenessStartsDay=2;
            var acquired=food.Shops[1];food.Shops.RemoveAt(1);
            var person=state.Citizens[0];var meal=food.Residents[0];
            person.PlaceId="s1";person.Activity=Activity.Shop;person.Phase=CitizenPhase.AtPlace;
            person.NextEventMinute=person.ActivityUntilMinute=meal.DesiredAt+90;
            person.RecentVisits.Add(new VisitRecord {AtMinute=state.Minute,PlaceId=person.PlaceId,Activity=Activity.Shop});
            var city=new CitySimulator(state,planner);city.AttachFoodMarket(market);
            if(inspect && inspection==1) city.InspectFoodAwareness("r");
            food.Shops.Add(acquired); // authority acquired after attachment, on the migration/load day
            if(inspect && inspection==2) city.InspectFoodAwareness("r");
            return (city,market,planner);
        }
        var (continuous,mc,pc)=LateScope(false);
        var (reloaded,mr,pr)=LateScope(false);
        var (inspected,mi,pi)=LateScope(true);
        var read=SaveCodec.Read(SaveCodec.Write(reloaded.State));Assert.Equal(SaveReadStatus.Loaded,read.Status);
        reloaded=new CitySimulator(read.State!,pr);reloaded.AttachFoodMarket(mr);
        double at=continuous.State.Food!.Residents[0].DesiredAt;
        foreach(var city in new[]{reloaded,inspected,continuous})
        {
            city.AdvanceTo(at,1);
            var meal=city.State.Food!.Residents[0];Assert.NotNull(meal.Decision);
            Assert.StartsWith("stop:",meal.Decision!.ContextKey);
            Assert.Equal("b1",Assert.Single(meal.Decision.Shops).BusinessId);
            Assert.Null(meal.Decision.Awareness); // still the pre-activation policy
        }
        Assert.Equal(SaveCodec.Write(continuous.State),SaveCodec.Write(reloaded.State));
        Assert.Equal(SaveCodec.Write(continuous.State),SaveCodec.Write(inspected.State));
        foreach(var city in new[]{continuous,reloaded,inspected})
            while(!city.AdvanceTo(at+20,1).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(continuous.State),SaveCodec.Write(reloaded.State));
        Assert.Equal(SaveCodec.Write(continuous.State),SaveCodec.Write(inspected.State));
        Assert.Equal(mc.Orders,mr.Orders);Assert.Equal(mc.Orders,mi.Orders);
        Assert.Equal(0,pc.Out+pc.Back+pr.Out+pr.Back+pi.Out+pi.Back);
    }

    [Fact]
    public void MissingPlacesStillConsumeBoundedCollectorOperations()
    {
        var scopes=Enumerable.Range(0,1024).Select(i=>new FoodScope {BusinessId="g"+i,PlaceId="p"+i});
        using var builder=FoodAwarenessCatalogue.BeginBuild(scopes,_=>null);int units=0;
        while(!builder.Continue()) {units++;Assert.InRange(builder.LastWorkItems,0,64);}
        Assert.True(units>1024/64);Assert.Equal(0,builder.Result.Count);
    }

}

