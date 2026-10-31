using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class WeatherTests
{
    private sealed class Source : IWeatherSource
    {
        public WeatherSample Value; public bool Throw;public int Calls;
        public WeatherSample ReadWeather() { Calls++;if(Throw) throw new Exception();return Value; }
    }
    private sealed class Paths : IRoutePlanner
    {
        public int Calls; public bool Fail;
        public Route Plan(Place a,Place b,double at,TravelPreferences p)
        {
            Calls++;if(Fail) throw new InvalidOperationException("interrupted boundary");
            return new Route {Legs=new() {new RouteLeg {From=a.Position,To=b.Position,Mode=TravelMode.Walk,Minutes=1}}};
        }
    }
    private sealed class Market : IFoodMarket
    {
        public double EarliestMinute=>0;public int Orders;
        public IReadOnlyList<FoodBusiness> Read(double at)=>new[] {new FoodBusiness {Id="g",PlaceId="shop",Name="Shop",MealsPerHour=60,
            Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=new() {new FoodOffer {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100}}}};
        public FoodSale Buy(ResidentMeal meal,double at) { Orders++;return new FoodSale(true,""); }
    }
    private static CityState State(int seed=7)
    {
        double start=1440+1050+(int)(StableRandom.Hash("person:hours",seed)%31)-15;
        var state=Population.Create(new[] {new CitizenIdentity {Id="person",Name="Person",Age=30,Persona=Persona.Foodie}},
            new[] {new Place {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=1},
                new Place {Id="shop",Name="Shop",Kind=PlaceKind.Shop,Position=new(10,0,0)}},"weather-test","0.40.4",seed,start);
        state.Citizens[0].ShopId="shop";return state;
    }
    private static CityState Read(string wire)
    { var r=SaveCodec.Read(wire);Assert.True(r.Status==SaveReadStatus.Loaded,r.Error);return r.State!; }
    internal static string As19(string wire)
    {
        var root=JObject.Parse(wire);root["Schema"]=19;root.Remove("WeatherStartsDay");
        foreach(JObject p in root["Citizens"]!) p.Remove("W");
        if(root["Food"] is JObject food) foreach(JArray m in food["Residents"]!)
        {
            string memory=(string)m[35]!;m[35]=memory[..^2];
            if(m[9] is JObject d) { d.Remove("Weather");d.Remove("WeatherTravelMultiplier"); }
        }
        return root.ToString(Formatting.None);
    }
    [Fact]
    public void Migration19IsNeutralActivatesNextDayAndPreservesSource()
    {
        var state=State();string old=As19(SaveCodec.Write(state));var read=Read(old);
        Assert.Equal(20,read.Schema);Assert.Equal(2,read.WeatherStartsDay);Assert.Null(read.Citizens[0].W);
        var rain=new Source {Value=WeatherSample.Rain};var city=new CitySimulator(read,new Paths());
        city.AttachWeather(rain,new WeatherSettings {RainOptionalOutingMultiplier=0});city.AdvanceTo(state.Minute);
        Assert.Equal(WeatherSample.Unknown,read.Citizens[0].W!.Weather);Assert.Equal(0,rain.Calls);
        Assert.Equal(SaveCodec.Write(read),SaveCodec.Write(Read(SaveCodec.Write(read))));
        city.AdvanceTo(state.Minute+1440);Assert.Equal(WeatherSample.Rain,read.Citizens[0].W!.Weather);Assert.True(rain.Calls>0);
        var forbidden=JObject.Parse(old);forbidden["WeatherStartsDay"]=0;
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(forbidden.ToString()).Status);
        forbidden=JObject.Parse(old);forbidden["Schema"]=21;Assert.Equal(SaveReadStatus.Future,SaveCodec.Read(forbidden.ToString()).Status);
        forbidden["Schema"]=12;Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(forbidden.ToString()).Status);
    }
    [Theory] [InlineData(WeatherSample.Dry)] [InlineData(WeatherSample.Rain)] [InlineData(WeatherSample.Unknown)]
    public void OutingCaptureSurvivesInterruptedPlanningChangedRainAndChangedTuning(WeatherSample weather)
    {
        var state=State();var source=new Source {Value=weather};var paths=new Paths {Fail=true};
        var settings=new WeatherSettings {RainOptionalOutingMultiplier=1};var city=new CitySimulator(state,paths);city.AttachWeather(source,settings);
        Assert.Throws<InvalidOperationException>(()=>city.AdvanceTo(state.Minute));
        Assert.Equal(weather,state.Citizens[0].W!.Weather);Assert.Equal(1,source.Calls);
        string captured=SaveCodec.Write(state);var loaded=Read(captured);var changed=new Source {Throw=true};
        var next=new CitySimulator(loaded,new Paths());next.AttachWeather(changed,new WeatherSettings {RainOptionalOutingMultiplier=0,Enabled=false});
        next.AdvanceTo(loaded.Minute);Assert.Equal(CitizenPhase.Travelling,loaded.Citizens[0].Phase);Assert.Equal(0,changed.Calls);
        var committed=SaveCodec.Write(loaded);var after=Read(committed);var resume=new CitySimulator(after,new Paths());resume.AttachWeather(changed,settings);
        resume.AdvanceTo(after.Minute+1);Assert.Equal("shop",after.Citizens[0].PlaceId);Assert.Equal(0,changed.Calls);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void UnknownAndFailedSourcesAreNeutralAndDisabledMakesNoRead(bool throwing)
    {
        foreach(bool enabled in new[] {false,true})
        {
            var state=State();var source=new Source {Value=WeatherSample.Unknown,Throw=throwing};var city=new CitySimulator(state,new Paths());
            city.AttachWeather(source,new WeatherSettings {Enabled=enabled,RainOptionalOutingMultiplier=0});city.AdvanceTo(state.Minute);
            Assert.Equal(CitizenPhase.Travelling,state.Citizens[0].Phase);Assert.Equal(WeatherSample.Unknown,state.Citizens[0].W!.Weather);
            Assert.Equal(enabled ? 1 : 0,source.Calls);
        }
        Assert.Equal(WeatherSample.Unknown,WeatherPolicy.Sample(null));Assert.Equal(WeatherSample.Unknown,WeatherPolicy.Sample(new Source {Value=(WeatherSample)123}));
    }
    [Fact]
    public void FrameCacheSamplesOnceAndCachesFailureAndMissingSource()
    {
        long frame=1;int calls=0;bool fail=false;bool? raining=false;
        var source=new FrameWeatherSource(()=>frame,()=> {calls++;if(fail) throw new Exception();return raining;});
        for(int i=0;i<50000;i++) Assert.Equal(WeatherSample.Dry,source.ReadWeather());Assert.Equal(1,calls);
        frame++;raining=true;Assert.Equal(WeatherSample.Rain,source.ReadWeather());
        frame++;fail=true;for(int i=0;i<100;i++) Assert.Equal(WeatherSample.Unknown,source.ReadWeather());Assert.Equal(3,calls);
        frame++;fail=false;raining=null;Assert.Equal(WeatherSample.Unknown,source.ReadWeather());
    }
    [Fact]
    public void AdmissionKeyOptimizationPreservesTheOriginalIndependentDraw()
    {
        for(int seed=0;seed<200;seed++)
            foreach(string key in new[] {"resident:optional:Shop:1234.5","resident:optional:Leisure:2880","resident:\u263A"})
                Assert.Equal(StableRandom.Hash(key+":weather-admission",seed),WeatherPolicy.AdmissionDraw(key,seed));
    }
    [Fact]
    public void AdmissionIsNestedAcrossSeedsAndEndpointsAndDoesNotPollAfterRejection()
    {
        int dry=0,rain=0;foreach(int seed in Enumerable.Range(1,200))
        {
            foreach(double multiplier in new[] {0.0,.7,1.0})
            {
                var state=State(seed);var source=new Source {Value=WeatherSample.Rain};var paths=new Paths();var city=new CitySimulator(state,paths);
                city.AttachWeather(source,new WeatherSettings {RainOptionalOutingMultiplier=multiplier});city.AdvanceTo(state.Minute);
                bool admitted=state.Citizens[0].W!.Value==1;
                if(multiplier==0) Assert.False(admitted);if(multiplier==1) {Assert.True(admitted);dry++;}
                if(multiplier==.7 && admitted) rain++;
                if(!admitted) {Assert.Equal(0,paths.Calls);source.Value=WeatherSample.Dry;city.AdvanceTo(state.Minute+1);Assert.Equal(1,source.Calls);}
            }
        }
        Assert.InRange(rain,1,dry-1);
    }
    [Fact]
    public void RainStrengthensSoftTravelAndDistantOffersStillWinAcrossSeeds()
    {
        var traits=new ResidentTraits {Bands="2222222222"};var settings=new FoodChoiceSettings();int dryFar=0,rainFar=0;
        var offers=new[] {new EvaluatedFoodOffer("near",FoodProducts.Croissant,Money.FromCents(100),1,0,1,200,null,null,MealKind.Lunch),
            new EvaluatedFoodOffer("far",FoodProducts.Croissant,Money.FromCents(100),90,0,1,200,null,null,MealKind.Lunch)};
        for(int seed=0;seed<20000;seed++)
        {
            var draw=new FoodDrawContext(seed,"consumer",1,MealKind.Lunch,"context",1);
            int dry=FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),draw,settings,false).Index;
            int rain=FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),draw,settings,false,1.3).Index;
            if(dry==1) dryFar++;if(rain==1) {rainFar++;Assert.Equal(1,dry);}
        }
        Assert.InRange(rainFar,1,dryFar-1);Assert.True(FoodChoicePolicy.DistanceWeight(100000,traits,settings,true,2)>0);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FoodHeldCaptureQuotesSelectionAndPaidTripIgnoreLiveRain(int units)
    {
        var state=State();var p=state.Citizens[0];p.NextEventMinute=p.ActivityUntilMinute=state.Minute+120;
        state.Food=new FoodCommerceState {Shops=new() {new FoodScope {BusinessId="g",PlaceId="shop",ProductId=FoodProducts.Croissant}}};
        var source=new Source {Value=WeatherSample.Rain};var market=new Market();var paths=new Paths();var city=new CitySimulator(state,paths);
        city.AttachWeather(source);city.AttachFoodMarket(market);state.Food.ChoiceStartsDay=1;state.Food.AwarenessStartsDay=2;
        var meal=Assert.Single(state.Food.Residents);
        // New intent at the current stay, without advancing into a different routine.
        meal.DesiredAt=meal.DueAt=state.Minute;meal.ExpiresAt=state.Minute+60;
        city=new CitySimulator(state,paths);city.AttachWeather(source);city.AttachFoodMarket(market);
        for(int i=0;i<units;i++) city.AdvanceFrameSlice(state.Minute,1);
        Assert.NotNull(meal.Decision);Assert.Equal(WeatherSample.Rain,meal.Decision!.Weather);Assert.Equal(1.3,meal.Decision.WeatherTravelMultiplier);
        string wire=SaveCodec.Write(state);int routes=paths.Calls;
        string Finish(WeatherSample weather,int budget)
        {
            var loaded=Read(wire);var q=new Paths();var s=new Source {Value=weather};var m=new Market();var sim=new CitySimulator(loaded,q);
            sim.AttachWeather(s,new WeatherSettings {Enabled=false,RainTravelWeightMultiplier=2});sim.AttachFoodMarket(m);
            while(!sim.AdvanceFrameSlice(loaded.Minute,budget).CaughtUp) { }
            Assert.Equal(0,s.Calls);Assert.Equal(2-routes,q.Calls);Assert.Equal(1,loaded.Food!.Residents[0].ContextAttempts);
            Assert.Equal(MealStatus.Travelling,loaded.Food.Residents[0].Status);
            sim.AdvanceTo(loaded.Minute+3);Assert.Equal(1,m.Orders);Assert.Equal(0,s.Calls);
            return SaveCodec.Write(loaded);
        }
        Assert.Equal(Finish(WeatherSample.Dry,1),Finish(WeatherSample.Rain,128));
    }
    [Theory]
    [InlineData(WeatherSample.Dry,false,true,false)]
    [InlineData(WeatherSample.Rain,false,true,true)]
    [InlineData(WeatherSample.Unknown,false,true,true)]
    [InlineData(WeatherSample.Rain,true,true,false)]
    [InlineData(WeatherSample.Rain,false,false,true)]
    public void FoodCapturesNeutralAndRainInputsBeforeEitherShortlistWithoutExtraAttempts(WeatherSample weather,bool failure,bool enabled,bool awareness)
    {
        var (city,_,planner)=WeightedFoodChoiceTests.Ready(79,12);
        city.State.Food!.AwarenessStartsDay=awareness ? 1 : 2;
        var source=new Source {Value=weather,Throw=failure};city.AttachWeather(source,new WeatherSettings {Enabled=enabled});
        var meal=city.State.Food.Residents.Single();double at=meal.DesiredAt;
        while(meal.Decision==null) city.AdvanceFrameSlice(at,1);
        var expected=enabled && !failure ? weather : WeatherSample.Unknown;
        Assert.Equal(expected,meal.Decision!.Weather);Assert.Equal(expected,meal.Memory.Weather);
        Assert.Equal(expected==WeatherSample.Rain ? 1.3 : 1,meal.Decision.WeatherTravelMultiplier);
        Assert.Equal(enabled ? 1 : 0,source.Calls);Assert.Equal(1,meal.ContextAttempts);Assert.Equal(0,planner.Out);
        source.Value=WeatherSample.Rain;source.Throw=true;
        while(!city.AdvanceFrameSlice(at,1).CaughtUp) { }
        Assert.Equal(enabled ? 1 : 0,source.Calls);Assert.Equal(1,meal.ContextAttempts);
        Assert.InRange(planner.Out+planner.Back,1,16);
    }
    [Theory]
    [InlineData("code")] [InlineData("low")] [InlineData("high")] [InlineData("nonneutral")]
    [InlineData("missing")] [InlineData("consumer")]
    public void BadSchema20FoodWeatherRefusesAndPreservesSource(string damage)
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,2);city.AttachWeather(new Source {Value=WeatherSample.Rain});
        city.AdvanceFrameSlice(city.State.Food!.Residents.Single().DesiredAt,1);
        var root=JObject.Parse(SaveCodec.Write(city.State));var meal=(JArray)root["Food"]!["Residents"]![0]!;var d=(JObject)meal[9]!;
        if(damage=="code") d["Weather"]=3;
        if(damage=="low") d["WeatherTravelMultiplier"]=0;
        if(damage=="high") d["WeatherTravelMultiplier"]=3;
        if(damage=="nonneutral") d["Weather"]=0;
        if(damage=="missing") d.Remove("Weather");
        if(damage=="consumer") meal[35]=((string)meal[35]!)[..^1]+"3";
        string payload=root.ToString(Formatting.None);var store=new Dictionary<string,string> {[SaveStore.StateKey]=payload};
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(payload).Status);Assert.False(SaveStore.WriteTo(store,city.State).Success);Assert.Same(payload,store[SaveStore.StateKey]);
    }
    [Fact]
    public void QueueCaptureRetainsLeadAcrossRainConfigAndReload()
    {
        var state=State();var p=state.Citizens[0];double now=state.Minute;var shop=state.Places[1];shop.OpenMinute=(int)(now%1440)+8;
        p.PlaceId=p.DestinationId="shop";p.DestinationActivity=Activity.Shop;p.Activity=Activity.Shop;p.Phase=CitizenPhase.WaitingForOpening;
        p.NextEventMinute=now+8;p.ActivityUntilMinute=now+45;
        var source=new Source {Value=WeatherSample.Dry};var city=new CitySimulator(state,new Paths());city.AttachWeather(source);
        var dry=city.QueueAtOpening(p,shop,now);Assert.NotNull(dry);source.Value=WeatherSample.Rain;
        Assert.Equal(dry,city.QueueAtOpening(p,shop,now+1));Assert.Equal(1,source.Calls);
        var loaded=Read(SaveCodec.Write(state));var resumed=new CitySimulator(loaded,new Paths());resumed.AttachWeather(source,new WeatherSettings {QueueRainLeadMinutes=2});
        Assert.Equal(dry,resumed.QueueAtOpening(loaded.Citizens[0],loaded.Places[1],now+1));Assert.Equal(1,source.Calls);
    }
    [Fact]
    public void RainQueueEndpointIsFrozenAndOnlyNewOpeningUsesChangedLead()
    {
        CityState? state=null;
        for(int seed=1;seed<100;seed++)
        {
            var trial=State(seed);var p=trial.Citizens[0];var shop=trial.Places[1];double now=trial.Minute;
            shop.OpenMinute=(int)(now%1440)+8;p.PlaceId=p.DestinationId="shop";p.DestinationActivity=Activity.Shop;
            p.Activity=Activity.Shop;p.Phase=CitizenPhase.WaitingForOpening;p.NextEventMinute=now+8;p.ActivityUntilMinute=now+45;
            if(DwellPlanner.QueueAtOpening(p,shop,seed,now,true)!=null) { state=trial;break; }
        }
        Assert.NotNull(state);var person=state!.Citizens[0];var source=new Source {Value=WeatherSample.Rain};
        var settings=new WeatherSettings {QueueRainLeadMinutes=2};var sim=new CitySimulator(state,new Paths());sim.AttachWeather(source,settings);
        var first=sim.QueueAtOpening(person,state.Places[1],state.Minute);Assert.NotNull(first);Assert.Equal(2,first!.Value.EndMinute-first.Value.StartMinute);
        settings.QueueRainLeadMinutes=10;source.Value=WeatherSample.Dry;
        Assert.Equal(first,sim.QueueAtOpening(person,state.Places[1],state.Minute+1));Assert.Equal(1,source.Calls);
        person.NextEventMinute+=1440;person.ActivityUntilMinute+=1440;state.Minute+=1440;
        sim.QueueAtOpening(person,state.Places[1],state.Minute);Assert.Equal(2,source.Calls);Assert.Equal(10,person.W!.Value);
    }
    [Fact]
    public void SettingsClampOnceAndKeepFlatDefaults()
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>("{\"WeatherEnabled\":{},\"WeatherRainOptionalOutingMultiplier\":\"Infinity\",\"WeatherRainTravelWeightMultiplier\":false,\"WeatherQueueRainLeadMinutes\":4.5}")!;
        var logs=new List<string>();settings.Weather.Clamp(logs.Add);Assert.Equal(4,logs.Count);settings.Weather.Clamp(logs.Add);Assert.Equal(4,logs.Count);
        Assert.True(settings.WeatherEnabled);
        Assert.False(JsonConvert.DeserializeObject<PrototypeSettings>("{\"WeatherEnabled\":false}")!.WeatherEnabled);
        Assert.Equal(1,settings.WeatherRainOptionalOutingMultiplier);Assert.Equal(1.3,settings.WeatherRainTravelWeightMultiplier);Assert.Equal(5,settings.WeatherQueueRainLeadMinutes);
        settings.Weather.QueueRainLeadMinutes=100;settings.Weather.RainOptionalOutingMultiplier=-1;settings.Weather.RainTravelWeightMultiplier=99;settings.Weather.Clamp();
        Assert.Equal(10,settings.WeatherQueueRainLeadMinutes);Assert.Equal(0,settings.WeatherRainOptionalOutingMultiplier);Assert.Equal(2,settings.WeatherRainTravelWeightMultiplier);
    }
    [Fact]
    public void WireBudgetCountsPhysicalCaptureAndConsumerSuffixWithoutExtraConsumerRecord()
    {
        var state=State();string before=SaveCodec.Write(state);var count=SaveCodec.CountRecords(state,before);
        state.Citizens[0].W=new WeatherCommitment(state.Minute,WeatherSample.Rain,true);string after=SaveCodec.Write(state);
        Assert.Equal(13,after.Length-before.Length); // 15-char value replaces null (2 quotes + 15 - 4)
        Assert.Equal(22,"\"W\":\"".Length+state.Citizens[0].W!.ToWire().Length+2);
        var charged=SaveCodec.CountRecords(state,after);Assert.Equal(count.Streaming+1,charged.Streaming);Assert.Equal(count.Semantic+1,charged.Semantic);
        foreach(string damage in new[] {"1O00AAAAAAAAAAA","2O21AAAAAAAAAAA","1O91AAAAAAAAAAA","1O22AAAAAAAAAAA","1Q21AAAAAAAAAAA","1O21AAAAAAAAAA/"})
        {var token=JObject.Parse(after);token["Citizens"]![0]!["W"]=damage;Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(token.ToString()).Status);}
        Assert.Equal(after,SaveCodec.Write(Read(after)));
    }
}
