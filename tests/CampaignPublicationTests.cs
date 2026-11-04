using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class CampaignPublicationTests
{
    private static CampaignSnapshot Snapshot(int count, int hour) => new(hour*60,hour,
        Enumerable.Range(0,count).Select(i => new BusinessCampaignSnapshot("b:"+i,CampaignReadStatus.Observed,
            new[] {new CampaignObservation(0,"SmallInternet",true,"agency",CampaignReadStatus.Observed)},0,0,0)).ToArray());
    [Theory]
    [InlineData(65,1)] [InlineData(65,16)] [InlineData(1024,16)] [InlineData(1024,64)]
    public void QueuedBoundariesMatchUninterruptedPublicationAndSaveCompletion(int count,int budget)
    {
        var places=Enumerable.Range(0,count).ToDictionary(i=>"b:"+i,i=>"p:"+i);
        var expected=new FoodCommerceState(); var actual=new FoodCommerceState();
        var reference=new FoodCampaignSource(expected,7,new()); var source=new FoodCampaignSource(actual,7,new());
        for(int hour=1;hour<=3;hour++)
        {
            var snapshot=Snapshot(count,hour); reference.Publish(snapshot,places); source.EnqueuePublication(snapshot,places);
        }
        int calls=0;
        while(source.HasPendingPublication && calls++<10)
        {
            source.ContinuePublication(budget); Assert.InRange(source.LastPublicationUnits,1,budget);
            Assert.True(source.Publications<3);
        }
        source.CompletePublicationsForSave(); // production save hook does this before any state capture
        Assert.False(source.HasPendingPublication);
        Assert.Equal(expected.BusinessReferences.Select(x=>(x.Slot,x.BusinessId,x.PlaceId)),actual.BusinessReferences.Select(x=>(x.Slot,x.BusinessId,x.PlaceId)));
        Assert.Equal(expected.Campaigns.Select(x=>x.Encode()),actual.Campaigns.Select(x=>x.Encode()));
        var loaded=new FoodCommerceState {BusinessReferences=actual.BusinessReferences,
            Campaigns=actual.Campaigns.Select(x=>FoodCampaignImage.Decode(x.Encode())).ToList()};
        var restored=new FoodCampaignSource(loaded,7,new());
        reference.Prepare("resident",0,180); restored.Prepare("resident",0,180);
        Assert.Equal(Enumerable.Range(0,8).Select(i=>reference.Reference("resident",0,i)),Enumerable.Range(0,8).Select(i=>restored.Reference("resident",0,i)));
        Assert.Equal(reference.IndexedEntries,restored.IndexedEntries);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationFailureClearsQueueAndDoesNotBlockCityOrSave(bool failDuringSave)
    {
        var state=EconomyScenario.Create(1,23);state.Food=EconomyScenario.Food();
        var source=new FoodCampaignSource(state.Food,23,new());
        int failures=0;source.PublicationFailed=error=> { Assert.IsType<ArgumentException>(error);failures++; };
        var city=new CitySimulator(state,new Routes());city.AttachFoodMarket(new EmptyMarket(),campaign:source);
        source.EnqueuePublication(Snapshot(1,30),new Dictionary<string,string>()); // invalid business mapping
        source.EnqueuePublication(Snapshot(1,31),new Dictionary<string,string>{{"b:0","p:0"}});
        if(failDuringSave) source.CompletePublicationsForSave();
        else while(source.HasPendingPublication) city.AdvanceFrameSlice(state.Minute,1);
        Assert.Equal(1,failures); Assert.False(source.HasPendingPublication); Assert.Equal(0,source.Publications);
        Assert.Empty(state.Food.Campaigns);
        Assert.True(city.AdvanceFrameSlice(state.Minute,1).CaughtUp);
        source.CompletePublicationsForSave();
        Assert.NotEmpty(PersistentCity.Persistence.SaveCodec.Write(state));
        Assert.True(source.ContinuePublication());Assert.Equal(1,failures);
    }

    [Fact]
    public void OfflineFailurePropagatesButCannotLeaveAFaultedIteratorQueued()
    {
        var food=new FoodCommerceState();var source=new FoodCampaignSource(food,23,new());
        source.EnqueuePublication(Snapshot(1,30),new Dictionary<string,string>());
        Assert.Throws<ArgumentException>(()=>source.CompletePublicationsForSave());
        Assert.False(source.HasPendingPublication);
        source.Publish(Snapshot(1,31),new Dictionary<string,string>{{"b:0","p:0"}});
        Assert.Equal(1,source.Publications);Assert.Single(food.Campaigns);
    }

    private sealed class EmptyMarket : IFoodMarket
    {
        public int Reads;
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double at) {Reads++;return Array.Empty<FoodBusiness>();}
        public FoodSale Buy(ResidentMeal meal,double at)=>throw new Exception("Unexpected sale");
    }
    private sealed class Routes : IRoutePlanner
    {
        public Route? Plan(Place a,Place b,double at,TravelPreferences preferences)=>throw new Exception("Unexpected route");
    }
    [Fact]
    public void PendingPublicationIsAClockAndMealBarrierEvenWhenNoEventIsDue()
    {
        var state=EconomyScenario.Create(1,23);state.Food=EconomyScenario.Food();
        var market=new EmptyMarket();var source=new FoodCampaignSource(state.Food,23,new());
        var city=new CitySimulator(state,new Routes());city.AttachFoodMarket(market,campaign:source);
        source.EnqueuePublication(Snapshot(65,30),Enumerable.Range(0,65).ToDictionary(i=>"b:"+i,i=>"p:"+i));
        double minute=state.Minute;
        Assert.False(city.IsCaughtUpTo(minute));
        var result=city.AdvanceFrameSlice(minute,1);
        Assert.Equal(1,result.FoodContinuationEvents);Assert.Equal(0,market.Reads);Assert.Equal(minute,state.Minute);
        Assert.True(source.HasPendingPublication);
        while(source.HasPendingPublication) {city.AdvanceFrameSlice(minute,1);Assert.Equal(0,market.Reads);}
    }
}
