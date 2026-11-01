using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class D2CompositionTests
{
    [Theory] [InlineData(21)] [InlineData(22)] [InlineData(23)]
    public void ExactContractsRejectForeignFieldsAndPreD2TuplesWithoutReplacingSource(int schema)
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79);
        city.State.Schema=schema;
        string source=SaveCodec.WriteForSchema(city.State,schema);
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(source).Status);
        foreach(string damage in new[]{"tuple","account","foreign","campaign","menu"})
        {
            if(damage=="campaign" && schema>=22 || damage=="menu" && schema==23) continue;
            var root=JObject.Parse(source);
            var food=(JObject)root["Food"]!;
            if(damage=="tuple") {var row=(JArray)food["Residents"]![0]!;row[0]=1;row.RemoveAt(36);row[3]=JValue.CreateNull();food.Remove("RivalStartsDay");}
            if(damage=="account") food.Remove("RivalStartsDay");
            if(damage=="foreign") root["ForeignPopulation"]=true;
            if(damage=="campaign") food["Campaigns"]=new JArray();
            if(damage=="menu") root["MenuPolicy"]=FoodMenuPolicy.Wire;
            string bad=root.ToString(Formatting.None);
            var data=new Dictionary<string,string>{{SaveStore.StateKey,bad}};
            Assert.Equal(SaveReadStatus.Invalid,SaveSession.Open(data,"weighted").InitialReadResult.Status);
            Assert.Same(bad,data[SaveStore.StateKey]);
        }
    }

    [Fact]
    public void RainCampaignAndRivalsShareBoundedHeldChoiceAndReplay()
    {
        var (prepared,players,paths)=WeightedFoodChoiceTests.Ready(79,4);
        var state=prepared.State;var food=state.Food!;
        var market=new RivalOutflowTests.Market {Players=players.Shops.Take(2).ToArray()};
        food.Shops.RemoveRange(2,2);
        for(int i=2;i<4;i++)
        {
            string id="rival"+i;
            market.Rivals.Add(id,new FoodScope {AlternativeKind=FoodAlternativeKind.Rival,BusinessId=id,PlaceId="s"+i,ProductId=FoodProducts.Croissant});
            market.Observations.Add(id,RivalOutflowTests.Observation(id,"s"+i));
        }
        int hour=(int)(food.Residents[0].DesiredAt/60);
        var masks=new uint[64];masks[0]=1u<<(hour%24);
        food.BusinessReferences.Add(new BusinessReference {Slot=0,BusinessId="b0",PlaceId="s0"});
        food.Campaigns.Add(new FoodCampaignImage(0,hour,masks));
        var campaign=new FoodCampaignSource(food,state.Seed,new FoodCampaignSettings {ReachProbability=1});
        var city=new CitySimulator(state,paths);
        city.AttachWeather(new FrameWeatherSource(()=>1,()=>true));
        city.AttachFoodMarket(market,campaign:campaign,rivalSettings:new RivalFoodSettings {RivalChoiceWeight=.7});
        food.ChoiceStartsDay=food.AwarenessStartsDay=food.RivalStartsDay=1;
        city.InspectFoodAwareness("r");var meal=food.Residents[0];
        city.AdvanceTo(meal.DesiredAt,1);
        var held=meal.Decision!;
        Assert.Equal(WeatherSample.Rain,held.Weather);
        Assert.Equal(1.3,held.WeatherTravelMultiplier);
        Assert.True(held.Awareness!.CampaignActive("b0"));
        Assert.Contains(held.Shops,s=>s.AlternativeKind==FoodAlternativeKind.Rival && s.AlternativeWeight==.7);
        Assert.Contains(held.Shops,s=>s.BusinessId=="b0" && s.AlternativeWeight==1);
        Assert.InRange(held.Shops.Count,1,8);
        state.Schema=22;string source=SaveCodec.WriteForSchema(state,22);state.Schema=23;
        var read=SaveCodec.Read(source);Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        var resumed=new CitySimulator(read.State!,paths);
        resumed.AttachWeather(new FrameWeatherSource(()=>2,()=>false),new WeatherSettings {Enabled=false});
        resumed.AttachFoodMarket(market,campaign:new FoodCampaignSource(read.State!.Food!,state.Seed,new FoodCampaignSettings {Enabled=false}),rivalSettings:new RivalFoodSettings {RivalChoiceWeight=0});
        while(held.Result==FoodDecisionResult.Evaluating) city.AdvanceTo(meal.DesiredAt,1);
        var restored=read.State.Food!.Residents[0].Decision!;
        while(restored.Result==FoodDecisionResult.Evaluating) resumed.AdvanceTo(meal.DesiredAt,128);
        Assert.Equal(JsonConvert.SerializeObject(held),JsonConvert.SerializeObject(restored));
        Assert.InRange(paths.Out+paths.Back,0,32);
    }
}
