using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodCampaignAwarenessTests
{
    private static readonly ResidentTraits Health = new() { Bands = "2222222322" };
    private static readonly ResidentTraits Neutral = new() { Bands = "2222222222" };
    private static BusinessCampaignSnapshot Business(string id, params CampaignObservation[] campaigns)
        => new(id, CampaignReadStatus.Observed, campaigns, 100, 100, 100);
    private static CampaignObservation Campaign(int type = 0, bool enabled = true, string? name = null, CampaignReadStatus status = CampaignReadStatus.Observed)
        => new(type, name ?? CampaignTypeGroups.VerifiedName(type), enabled, "agency", status);
    private static CampaignSnapshot Snapshot(int hour, params BusinessCampaignSnapshot[] businesses) => new(hour * 60, hour, businesses);
    private static Dictionary<string,string> Places(params string[] ids) => ids.ToDictionary(id => id, id => "place:" + id);
    private static FoodCampaignSource Source(FoodCommerceState food, FoodCampaignSettings? settings = null)
        => new(food, 79, settings ?? new() { ReachProbability = 1, DailyReachDecay = 1 });
    private static string?[] Draw(FoodCampaignSource source, int day, double minute, string consumer = "r")
    {
        source.Prepare(consumer, day, minute);
        return Enumerable.Range(0,8).Select(i => source.Reference(consumer,day,i)).ToArray();
    }

    [Fact]
    public void OnlyActuallyObservedHoursSurviveToggleMidnightReloadAndStop()
    {
        var food = new FoodCommerceState(); var source = Source(food); var places = Places("b");
        source.Publish(Snapshot(22,Business("b",Campaign())),places);
        source.Publish(Snapshot(23,Business("b",Campaign(enabled:false))),places);
        source.Publish(Snapshot(24,Business("b",Campaign())),places);
        var image = Assert.Single(food.Campaigns);
        Assert.Equal(1u << 22,image.Mask(0,0)); Assert.Equal(1u,image.Mask(1,0));
        Assert.Null(Draw(source,0,21*60).FirstOrDefault(x=>x!=null));
        Assert.Contains("b",Draw(source,0,22*60));
        food.Campaigns = food.Campaigns.Select(i=>FoodCampaignImage.Decode(i.Encode())).ToList();
        source = Source(food); Assert.Contains("b",Draw(source,1,24*60));
        source.Publish(Snapshot(25,Business("b")),places); Assert.Contains("b",Draw(source,1,25*60));
        // Missed boundaries add no bits, including when a later sample observes enabled again.
        source.Publish(Snapshot(50,Business("b",Campaign())),places);
        image=Assert.Single(food.Campaigns);Assert.Equal(1u << 2,image.Mask(2,0)); Assert.Equal(1u,image.Mask(1,0));
        Assert.Equal(image.Encode(),FoodCampaignImage.Decode(image.Encode()).Encode());
    }
    [Fact]
    public void RetentionDecayAndExpiryUseStableSharedReachDraws()
    {
        var food = new FoodCommerceState(); var settings = new FoodCampaignSettings { ReachProbability=1,DailyReachDecay=.5 };
        var source = Source(food,settings:settings); source.Publish(Snapshot(0,Business("b",Campaign())),Places("b"));
        int yesterday=0,today=0;
        for(int consumer=0;consumer<1000;consumer++)
        {
            bool one=Draw(source,1,1440,consumer:"r"+consumer).Contains("b");
            bool two=Draw(source,2,2880,consumer:"r"+consumer).Contains("b");
            if(one) yesterday++; if(two) today++; Assert.False(two && !one);
        }
        Assert.InRange(yesterday,400,600);Assert.InRange(today,180,320);
    }
    [Fact]
    public void EightDayHardHistoryAndConfigurableRetentionAreIndependentOfNativeStop()
    {
        var food=new FoodCommerceState();var source=Source(food);source.Publish(Snapshot(0,Business("b",Campaign())),Places("b"));
        Assert.Contains("b",Draw(source,7,7*1440));Assert.All(Draw(source,8,8*1440),Assert.Null);
        var shortSource=Source(food,settings:new() {ReachProbability=1,DailyReachDecay=1,RetentionDays=1});
        Assert.Contains("b",Draw(shortSource,1,1440));Assert.All(Draw(shortSource,2,2880),Assert.Null);
        source.Publish(Snapshot(8*24,Business("b")),Places("b"));Assert.Empty(food.Campaigns);Assert.False(source.HasEvidence);
    }
    [Fact]
    public void OverlappingTypesCountOneBusinessOnceAndAllResidentsShareTheSameRule()
    {
        var food=new FoodCommerceState();var source=Source(food);
        source.Publish(Snapshot(1,Business("b",Campaign(0),Campaign(1))),Places("b"));
        Assert.Single(Draw(source,0,60),id=>id!=null);
        Assert.Equal(1,source.Admitted);Assert.Equal(7,source.Refused);
        Assert.Contains("b",Draw(source,0,60,consumer:"retired"));
        Assert.Contains("b",Draw(source,0,60,consumer:"commuter"));
    }
    [Fact]
    public void MissingCurrentReadKeepsHistoryWithoutInventingActiveEvidenceAndRetirementDropsIt()
    {
        var food=new FoodCommerceState();var source=Source(food);var places=Places("b");
        source.Publish(Snapshot(24,Business("b",Campaign())),places);
        Draw(source,1,1440);Assert.True(source.Active("b"));
        source.Publish(Snapshot(25),places); // temporarily not a current candidate
        Assert.Equal(1u,Assert.Single(food.Campaigns).Mask(1,0));
        Assert.Contains("b",Draw(source,1,1500));Assert.False(source.Active("b"));
        source.Publish(Snapshot(26,Business("b",Campaign())),places);
        Draw(source,1,1560);Assert.True(source.Active("b"));
        source.Publish(Snapshot(27),new Dictionary<string,string>());
        Assert.Empty(food.Campaigns);Assert.False(source.HasEvidence);
    }
    [Fact]
    public void DelayedMealUsesItsObservedHourAcrossContinuousStoppedAndReloadedCampaigns()
    {
        var food=new FoodCommerceState();var source=Source(food,settings:new() {ReachProbability=0});
        var places=Places("b");
        var catalogue=new FoodAwarenessCatalogue(new[]{new FoodAwarenessCatalogue.Entry("b","p",new Position(10,0,0))});
        FoodAwarenessImage Capture(int day,double minute)
        {
            source.Prepare("r",day,minute);
            return catalogue.Sample(79,"r",day,new Position(),null,null,new(),_=>true,campaign:source);
        }
        // Capture at 23:10 and 24:10, straddling midnight; the meal clock can lag the observer.
        source.Publish(Snapshot(23,Business("b",Campaign())),places);
        var timely23=Capture(0,1390);Assert.True(timely23.CampaignActive("b"));
        source.Publish(Snapshot(24,Business("b",Campaign())),places);
        var timely24=Capture(1,1450);Assert.True(timely24.CampaignActive("b"));
        source.Publish(Snapshot(25,Business("b",Campaign())),places);
        Assert.Equal(JsonConvert.SerializeObject(timely23),JsonConvert.SerializeObject(Capture(0,1390)));
        Assert.Equal(JsonConvert.SerializeObject(timely24),JsonConvert.SerializeObject(Capture(1,1450)));
        source.Publish(Snapshot(26,Business("b",Campaign(enabled:false))),places);
        Assert.False(Capture(1,1570).CampaignActive("b"));
        // The disabled hour stays disabled even when a later observation is enabled again.
        source.Publish(Snapshot(28,Business("b",Campaign())),places);
        for(int replay=0;replay<2;replay++)
        {
            Assert.Equal(JsonConvert.SerializeObject(timely23),JsonConvert.SerializeObject(Capture(0,1390)));
            Assert.Equal(JsonConvert.SerializeObject(timely24),JsonConvert.SerializeObject(Capture(1,1450)));
            Assert.False(Capture(1,1570).CampaignActive("b")); // observed stopped
            Assert.False(Capture(1,1630).CampaignActive("b")); // unobserved gap
            Assert.False(Capture(0,1330).CampaignActive("b")); // before first observation
            Assert.True(Capture(1,1690).CampaignActive("b"));
            Assert.False(Capture(1,1750).CampaignActive("b")); // not yet observed
            food.Campaigns=food.Campaigns.Select(i=>FoodCampaignImage.Decode(i.Encode())).ToList();
            source=Source(food,settings:new() {ReachProbability=0});
            source.Rebuild();
        }
        source.Prepare("r",1,1450);source.Active("b"); // warm bounded generation lookup
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++) Assert.True(source.Active("b"));
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
        source.Publish(Snapshot(9*24,Business("b")),places);
        Assert.False(Capture(1,1450).CampaignActive("b")); // outside retained eight-day image
    }
    [Fact]
    public void ActiveCampaignRaisesPickProbabilityAndSoftensDistanceForEveryTraitProfile()
    {
        var settings=new FoodChoiceSettings();var allowance=Money.FromCents(1000);
        foreach(var traits in new[]{Health,Neutral,new ResidentTraits {Bands="0000000000"},new ResidentTraits {Bands="4444444444"}})
        {
            EvaluatedFoodOffer Offer(double travel,bool active)=>new("b",FoodProducts.Croissant,Money.FromCents(300),travel,0,5,90,50,50,MealKind.Lunch,campaignActive:active);
            double near=FoodChoicePolicy.Weight(Offer(5,false),traits,allowance,settings);
            double far=FoodChoicePolicy.Weight(Offer(60,false),traits,allowance,settings);
            double nearActive=FoodChoicePolicy.Weight(Offer(5,true),traits,allowance,settings);
            double farActive=FoodChoicePolicy.Weight(Offer(60,true),traits,allowance,settings);
            Assert.True(nearActive>near && farActive>far);
            Assert.True(FoodChoicePolicy.DistanceWeight(60,traits,settings,false,true)/FoodChoicePolicy.DistanceWeight(5,traits,settings,false,true)
                > FoodChoicePolicy.DistanceWeight(60,traits,settings,false)/FoodChoicePolicy.DistanceWeight(5,traits,settings,false));
            Assert.True(FoodChoicePolicy.DistanceWeight(0,traits,settings,false,true)>FoodChoicePolicy.DistanceWeight(0,traits,settings,false));
            int baseline=0,advertised=0;
            for(int seed=0;seed<2000;seed++)
            {
                var draw=new FoodDrawContext(seed,"r",1,MealKind.Lunch,"stay",1);
                bool old=FoodChoicePolicy.Select(new[]{Offer(60,false)},traits,allowance,draw,settings,true).Index==0;
                bool active=FoodChoicePolicy.Select(new[]{Offer(60,true)},traits,allowance,draw,settings,true).Index==0;
                if(old)baseline++;if(active)advertised++;Assert.False(old && !active);
            }
            Assert.True(advertised>baseline);
        }
    }
    [Fact]
    public void ActiveModifierAlsoAppliesToCoreKnownShopsWhenCampaignDrawsAdmitNothing()
    {
        var food=new FoodCommerceState();var source=Source(food,settings:new() {ReachProbability=0});
        source.Publish(Snapshot(24,Business("b",Campaign())),Places("b"));source.Prepare("r",1,1440);
        var catalogue=new FoodAwarenessCatalogue(new[]{new FoodAwarenessCatalogue.Entry("b","p",new Position(10,0,0))});
        var image=catalogue.Sample(79,"r",1,new Position(),null,null,new(),_=>true,campaign:source);
        Assert.True(image.CampaignActive("b"));
        Assert.DoesNotContain(image.Union,c=>(c.Sources&FoodAwarenessSource.Campaign)!=0);
        source.Publish(Snapshot(25),Places("b"));source.Prepare("r",1,1500);
        image=catalogue.Sample(79,"r",1,new Position(),null,null,new(),_=>true,campaign:source);
        Assert.False(image.CampaignActive("b"));
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void UnknownTypeDisabledMalformedAndDuplicateEvidenceAddZero(int fault)
    {
        var food=new FoodCommerceState();var source=Source(food);
        var campaign=fault==1 ? Campaign(6) : fault==2 ? Campaign(name:"unverified") : fault==3 ? Campaign(enabled:false)
            : fault==4 ? Campaign(status:CampaignReadStatus.Unknown) : Campaign();
        source.Publish(Snapshot(1,Business("b",fault==5 ? new[]{campaign,campaign} : new[]{campaign})),Places("b"));
        Assert.All(Draw(source,0,60),Assert.Null);Assert.False(source.HasEvidence);Assert.Equal(0,source.Admitted);
    }
    [Fact]
    public void PublicationRejectsIncompleteForeignDuplicateOrBackdatedImagesAtomically()
    {
        var food=new FoodCommerceState();var source=Source(food);source.Publish(Snapshot(1,Business("b",Campaign())),Places("b"));
        string before=food.Campaigns[0].Encode();
        foreach(var bad in new[]{Snapshot(2,Business("foreign",Campaign())),Snapshot(2,Business("b",Campaign()),Business("b",Campaign())),Snapshot(1,Business("b",Campaign()))})
        {
            Assert.Throws<ArgumentException>(()=>source.Publish(bad,Places("b")));
            Assert.Equal(before,food.Campaigns[0].Encode());Assert.Contains("b",Draw(source,0,60));
        }
        Assert.Throws<ArgumentException>(()=>source.Publish(new CampaignSnapshot(61,0,new[]{Business("b",Campaign())}),Places("b")));
    }
    [Fact]
    public void PermutationReloadAndCadenceDoNotChangeEvidenceOrEightDrawsAt1024Images()
    {
        var ids=Enumerable.Range(0,1024).Select(i=>"b"+i.ToString("D4")).ToArray();var places=Places(ids);
        var a=new FoodCommerceState();var b=new FoodCommerceState();var sa=Source(a);var sb=Source(b);
        foreach(int hour in new[]{22,23,24,25,48})
        {
            var observations=ids.Select(id=>Business(id,Campaign(hour%6))).ToArray();
            sa.Publish(Snapshot(hour,observations),places);sb.Publish(Snapshot(hour,observations.Reverse().ToArray()),places);
            b.Campaigns=b.Campaigns.Select(i=>FoodCampaignImage.Decode(i.Encode())).ToList();sb=Source(b);
            Assert.Equal(a.Campaigns.Select(i=>i.Encode()),b.Campaigns.Select(i=>i.Encode()));
            Assert.Equal(Draw(sa,hour/24,hour*60),Draw(sb,hour/24,hour*60));
            Assert.InRange(Draw(sa,hour/24,hour*60).Count(id=>id!=null),1,8);
        }
        Assert.Equal(1024,a.Campaigns.Count);Assert.All(a.Campaigns,i=>Assert.Equal(265,i.Encode().Length));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void NoCampaignsOrDisabledAreByteIdenticalToC2aAndAllocateNothingOnTheSourcePath(bool disabled)
    {
        var food=new FoodCommerceState();var source=Source(food,settings:new() {Enabled=!disabled});
        if(disabled) source.Publish(Snapshot(1,Business("b",Campaign())),Places("b"));
        Assert.False(source.HasEvidence);
        source.Prepare("r",0,60); // warm up before allocation counter
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++) {source.Prepare("r",0,60);source.Reference("r",0,0);}
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
        var catalogue=new FoodAwarenessCatalogue(Enumerable.Range(0,32).Select(i=>new FoodAwarenessCatalogue.Entry("b"+i,"p"+i,new(0,0,0))));
        FoodAwarenessImage Sample(IFoodAwarenessProvider? campaign)=>catalogue.Sample(79,"r",0,new Position(),null,null,new(),_=>true,campaign:campaign);
        Assert.Equal(JsonConvert.SerializeObject(Sample(null)),JsonConvert.SerializeObject(Sample(source)));
    }
    [Fact]
    public void CampaignsCompeteInUnchangedEnvelopeAndHeldCaptureSurvivesDisableReload()
    {
        var (prepared,market,planner)=WeightedFoodChoiceTests.Ready(79,64);var state=prepared.State;state.Food!.AwarenessStartsDay=1;
        var source=Source(state.Food);var ids=state.Food.Shops.Select(s=>s.BusinessId).ToArray();
        int observationHour=(int)(state.Food.Residents[0].DesiredAt/60);
        source.Publish(Snapshot(observationHour,ids.Select(id=>Business(id,Campaign())).ToArray()),state.Food.Shops.ToDictionary(s=>s.BusinessId,s=>s.PlaceId));
        // Production uses the same universal rule; replay retains the captured active modifier.
        source=Source(state.Food);
        var city=new CitySimulator(state,planner);city.AttachWeather(new FrameWeatherSource(()=>1,()=>true));city.AttachFoodMarket(market,campaign:source);city.InspectFoodAwareness("r");
        var meal=state.Food.Residents[0];city.AdvanceTo(meal.DesiredAt,1);var decision=meal.Decision!;
        Assert.Equal(WeatherSample.Rain,decision.Weather);Assert.Equal(1.3,decision.WeatherTravelMultiplier);
        Assert.InRange(decision.Awareness!.Union.Count,1,32);Assert.InRange(decision.Shops.Count,0,8);
        Assert.Contains(decision.Awareness.Union,c=>(c.Sources&FoodAwarenessSource.Campaign)!=0);
        Assert.Contains(decision.Awareness.Union,c=>(c.Sources&FoodAwarenessSource.ActiveCampaign)!=0);
        string held=JsonConvert.SerializeObject(decision);var loaded=SaveCodec.Read(SaveCodec.Write(state));Assert.True(loaded.Status==SaveReadStatus.Loaded,loaded.Error);
        Assert.Equal(held,JsonConvert.SerializeObject(loaded.State!.Food!.Residents[0].Decision));
        var other=new CitySimulator(loaded.State,planner);other.AttachFoodMarket(market,campaign:Source(loaded.State.Food,settings:new() {Enabled=false}));
        while(decision.Result==FoodDecisionResult.Evaluating) city.AdvanceTo(meal.DesiredAt,1);
        var otherDecision=loaded.State.Food.Residents[0].Decision!;
        while(otherDecision.Result==FoodDecisionResult.Evaluating) other.AdvanceTo(meal.DesiredAt,128);
        Assert.Equal(JsonConvert.SerializeObject(decision),JsonConvert.SerializeObject(otherDecision));
        Assert.InRange(planner.Out+planner.Back,0,32); // two matched continuations, <=16 each
    }
    [Fact]
    public void Exact20MigrationAddsOnlyCampaignAndMenuFieldsAndPreservesSource()
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,2);var root=JObject.Parse(SaveCodec.Write(city.State));
        city.State.WeatherStartsDay=1;
        city.State.Citizens[0].W=new WeatherCommitment(city.State.Minute,WeatherSample.Rain,true);
        root=JObject.Parse(SaveCodec.Write(city.State));
        root["Schema"]=20;root.Remove("MenuPolicy");((JObject)root["Food"]!).Remove("Campaigns");
        string source=root.ToString(Formatting.None);var data=new Dictionary<string,string> {[SaveStore.StateKey]=source};
        SaveCodec.RequireExecutableCapabilities(source);
        var read=SaveSession.Open(data,"weighted").InitialReadResult;Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        Assert.Equal(20,read.SourceSchema);Assert.Equal(CityState.CurrentSchema,read.State!.Schema);Assert.Empty(read.State.Food!.Campaigns);
        var expected=(JObject)root.DeepClone();expected["Schema"]=CityState.CurrentSchema;expected["MenuPolicy"]=FoodMenuPolicy.Wire;expected["Food"]!["Campaigns"]=new JArray();
        Assert.True(JToken.DeepEquals(expected,JObject.Parse(SaveCodec.Write(read.State))));Assert.Equal(source,data[SaveStore.StateKey]);
        foreach(int schema in new[]{21,24,99})
        {
            root["Schema"]=schema;var wire=root.ToString(Formatting.None);data[SaveStore.StateKey]=wire;
            Assert.NotEqual(SaveReadStatus.Loaded,SaveSession.Open(data,"weighted").InitialReadResult.Status);Assert.Equal(wire,data[SaveStore.StateKey]);
            Assert.Throws<SaveValidationException>(()=>SaveCodec.RequireExecutableCapabilities(wire));
        }
        root["Schema"]=20;root["Food"]!["Campaigns"]=new JArray();Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(root.ToString(Formatting.None)).Status);
        ((JObject)root["Food"]!).Remove("Campaigns");root["Food"]!["AwarenessStartsDay"]=-1;
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(root.ToString(Formatting.None)).Status);
        var (heldCity,_,_)=WeightedFoodChoiceTests.Ready(79,24);heldCity.State.Food!.AwarenessStartsDay=1;
        heldCity.AttachWeather(new FrameWeatherSource(()=>1,()=>true));
        heldCity.InspectFoodAwareness("r");heldCity.AdvanceTo(heldCity.State.Food.Residents[0].DesiredAt,1);
        var heldRoot=JObject.Parse(SaveCodec.Write(heldCity.State));heldRoot["Schema"]=20;heldRoot.Remove("MenuPolicy");((JObject)heldRoot["Food"]!).Remove("Campaigns");
        var heldRead=SaveCodec.Read(heldRoot.ToString(Formatting.None));Assert.True(heldRead.Status==SaveReadStatus.Loaded,heldRead.Error);
        Assert.Equal(JsonConvert.SerializeObject(heldCity.State.Food.Residents[0].Decision),JsonConvert.SerializeObject(heldRead.State!.Food!.Residents[0].Decision));
        var legacy19=JObject.Parse(WeatherTests.As19(heldRoot.ToString(Formatting.None)));
        string legacySource=legacy19.ToString(Formatting.None);data[SaveStore.StateKey]=legacySource;
        var legacyRead=SaveSession.Open(data,"weighted").InitialReadResult;
        Assert.True(legacyRead.Status==SaveReadStatus.Loaded,legacyRead.Error);
        Assert.Equal(19,legacyRead.SourceSchema);Assert.Equal(CityState.CurrentSchema,legacyRead.State!.Schema);
        Assert.Equal((int)(heldCity.State.Minute/1440)+1,legacyRead.State.WeatherStartsDay);
        Assert.Equal(WeatherSample.Unknown,legacyRead.State.Food!.Residents[0].Decision!.Weather);
        Assert.Equal(legacySource,data[SaveStore.StateKey]);
        legacy19["Food"]!["Residents"]![0]![9]!["Awareness"]!["Union"]![0]!["Sources"]=32;
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(legacy19.ToString(Formatting.None)).Status);
        heldRoot["Food"]!["Residents"]![0]![9]!["Awareness"]!["Union"]![0]!["Sources"]=32;
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(heldRoot.ToString(Formatting.None)).Status); // predecessor 20 still forbids campaigns
    }
    [Fact]
    public void Exact22CampaignAndRainCommitmentsSurviveMenuMigrationAndChangedLiveInputs()
    {
        var (prepared,market,planner)=WeightedFoodChoiceTests.Ready(79,64);
        var state=prepared.State;state.Food!.AwarenessStartsDay=1;state.WeatherStartsDay=1;
        var campaign=Source(state.Food);int hour=(int)(state.Food.Residents[0].DesiredAt/60);
        campaign.Publish(Snapshot(hour,state.Food.Shops.Select(s=>Business(s.BusinessId,Campaign())).ToArray()),
            state.Food.Shops.ToDictionary(s=>s.BusinessId,s=>s.PlaceId));
        var city=new CitySimulator(state,planner);
        city.AttachWeather(new FrameWeatherSource(()=>1,()=>true));city.AttachFoodMarket(market,campaign:campaign);city.InspectFoodAwareness("r");
        var meal=state.Food.Residents[0];city.AdvanceTo(meal.DesiredAt,1);
        state.Citizens[0].W=new WeatherCommitment(state.Minute,WeatherSample.Rain,true);
        Assert.Equal(FoodDecisionResult.Evaluating,meal.Decision!.Result);
        Assert.Contains(meal.Decision.Awareness!.Union,c=>(c.Sources&FoodAwarenessSource.ActiveCampaign)!=0);

        string As22(CityState value)
        {
            var context=typeof(SaveCodec).GetNestedType("WireContext",BindingFlags.NonPublic)!
                .GetMethod("FromState")!.Invoke(null,new object[]{value});
            var settings=(JsonSerializerSettings)typeof(SaveCodec).GetMethod("SettingsForSchema",BindingFlags.NonPublic|BindingFlags.Static)!
                .Invoke(null,new[]{context,(object)22})!;
            var root=JObject.Parse(JsonConvert.SerializeObject(value,settings));root["Schema"]=22;
            return root.ToString(Formatting.None);
        }
        string source=As22(state);var data=new Dictionary<string,string>{{SaveStore.StateKey,source}};
        SaveCodec.RequireExecutableCapabilities(source);
        var read=SaveSession.Open(data,"weighted").InitialReadResult;
        Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);Assert.Equal(22,read.SourceSchema);
        Assert.Equal(23,read.State!.Schema);Assert.Equal(FoodMenuPolicy.Wire,read.State.MenuPolicy);
        Assert.Equal(source,As22(read.State));Assert.Same(source,data[SaveStore.StateKey]);
        Assert.Equal(state.Citizens[0].W!.ToWire(),read.State.Citizens[0].W!.ToWire());
        Assert.Equal(WeatherSample.Rain,read.State.Food!.Residents[0].Decision!.Weather);
        Assert.Equal(1.3,read.State.Food.Residents[0].Decision!.WeatherTravelMultiplier);
        string migrated=SaveCodec.Write(read.State);
        Assert.Equal(migrated,SaveCodec.Write(SaveCodec.Read(migrated).State!));

        var resumed=new CitySimulator(read.State,planner);
        resumed.AttachWeather(new FrameWeatherSource(()=>2,()=>false),new WeatherSettings{Enabled=false});
        resumed.AttachFoodMarket(market,new FoodChoiceSettings{FoodMenuMaximumOffersPerShop=8,FoodMenuNewTasteWeight=0},
            campaign:Source(read.State.Food,settings:new(){Enabled=false}));
        Assert.Equal(migrated,SaveCodec.Write(resumed.State));
        while(meal.Decision.Result==FoodDecisionResult.Evaluating) city.AdvanceTo(meal.DesiredAt,1);
        var restored=read.State.Food.Residents[0].Decision!;
        while(restored.Result==FoodDecisionResult.Evaluating) resumed.AdvanceTo(meal.DesiredAt,128);
        Assert.Equal(JsonConvert.SerializeObject(meal.Decision),JsonConvert.SerializeObject(restored));
    }
    [Fact]
    public void RainAndActiveCampaignComposeInShortlistAndFinalOfferWithoutChangingTravel()
    {
        var traits=new ResidentTraits {Bands="2222222222"};var settings=new FoodChoiceSettings();
        var plain=new EvaluatedFoodOffer("b",FoodProducts.Croissant,Money.FromCents(100),40,0,1,200,null,null,MealKind.Lunch);
        var active=new EvaluatedFoodOffer("b",FoodProducts.Croissant,Money.FromCents(100),40,0,1,200,null,null,MealKind.Lunch,0,true);
        var half=new EvaluatedFoodOffer("b",FoodProducts.Croissant,Money.FromCents(100),20,0,1,200,null,null,MealKind.Lunch);
        double weight=FoodChoicePolicy.Weight(active,traits,Money.FromCents(1000),settings,1.3);
        Assert.Equal(1.25*FoodChoicePolicy.Weight(half,traits,Money.FromCents(1000),settings,1.3),weight,12);
        Assert.True(weight>FoodChoicePolicy.Weight(plain,traits,Money.FromCents(1000),settings,1.3));
        Assert.True(weight<FoodChoicePolicy.Weight(active,traits,Money.FromCents(1000),settings));
        Assert.Equal(40,active.Travel);
        Assert.Equal(1.25*FoodChoicePolicy.DistanceWeight(20,traits,settings,true,1.3),
            FoodChoicePolicy.DistanceWeight(40,traits,settings,true,1.3,true),12);
    }
    [Fact]
    public void CampaignImageCountsExactlyOnceAndRejectsCorruptOrForeignReferences()
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,2);var state=city.State;string before=SaveCodec.Write(state);
        var source=Source(state.Food!);var shop=state.Food!.Shops[0];source.Publish(Snapshot(24,Business(shop.BusinessId,Campaign())),new Dictionary<string,string>{{shop.BusinessId,shop.PlaceId}});
        string wire=SaveCodec.Write(state);var read=SaveCodec.Read(wire);Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);Assert.Equal(wire,SaveCodec.Write(read.State!));
        var counts=SaveCodec.CountRecords(state,wire);var previous=SaveCodec.Read(before).State!;var oldCounts=SaveCodec.CountRecords(previous,before);
        Assert.Equal(2,counts.Streaming-oldCounts.Streaming);Assert.Equal(2,counts.Semantic-oldCounts.Semantic); // image plus shared generation
        var root=JObject.Parse(wire);var image=root["Food"]!["Campaigns"]![0]!;
        root["Food"]!["Campaigns"]![0]="bad";Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(root.ToString(Formatting.None)).Status);
        root["Food"]!["Campaigns"]![0]="1ZZZ"+image.ToString().Substring(4);Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(root.ToString(Formatting.None)).Status);
        Assert.Throws<ArgumentException>(()=>new FoodCampaignImage(0,0,new uint[64]));
        var masks=new uint[64];masks[6]=1;Assert.Throws<ArgumentException>(()=>new FoodCampaignImage(0,0,masks));
        masks=new uint[64];masks[0]=2;Assert.Throws<ArgumentException>(()=>new FoodCampaignImage(0,0,masks));
        masks[0]=1;var detached=new FoodCampaignImage(0,0,masks);masks[0]=0;
        Assert.Equal(1u,detached.Mask(0,0)); // public buffers cannot alter a published image
    }
    [Fact]
    public void HourlyReferenceInsertionDoesNotMoveCatalogueWorkOntoTheNextMemoryLookup()
    {
        var (city,market,planner)=WeightedFoodChoiceTests.Ready(79,1024);
        var food=city.State.Food!;var source=Source(food);
        var commerce=new FoodCommerce(city,market,planner,campaign:source);
        commerce.MemoryReference(-1); // warm the bounded lookup before publication
        source.Publish(Snapshot(24,food.Shops.Select(s=>Business(s.BusinessId,Campaign())).ToArray()),
            food.Shops.ToDictionary(s=>s.BusinessId,s=>s.PlaceId));
        Assert.Equal(1024,food.BusinessReferences.Count);
        long before=GC.GetAllocatedBytesForCurrentThread();
        var reference=commerce.MemoryReference(1023);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(0,allocated);Assert.Same(food.BusinessReferences[1023],reference);
    }
    [Fact]
    public void AForeignCityCampaignSourceCannotChangeThisCitysReferenceOwnership()
    {
        var (prepared,market,planner)=WeightedFoodChoiceTests.Ready(79,2);
        string before=SaveCodec.Write(prepared.State);
        var city=new CitySimulator(prepared.State,planner);
        Assert.Throws<ArgumentException>(()=>city.AttachFoodMarket(market,campaign:Source(new FoodCommerceState())));
        Assert.Equal(before,SaveCodec.Write(prepared.State));
    }
    [Fact]
    public void FlatTunablesClampAndLogOnceWithoutChangingOtherSettings()
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodMarketingCampaignReachProbability\":2,\"FoodMarketingTargetMatchMultiplier\":0,\"FoodMarketingRetentionDays\":99,\"FoodMarketingDailyReachDecay\":false}")!;
        var logs=new List<string>();settings.Campaigns.Clamp(logs.Add);settings.Campaigns.Clamp(logs.Add);
        Assert.Equal(3,logs.Count);Assert.Equal(1,settings.Campaigns.ReachProbability);
        Assert.Equal(7,settings.Campaigns.RetentionDays);Assert.Equal(.5,settings.Campaigns.DailyReachDecay);
    }
}
