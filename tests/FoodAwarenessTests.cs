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

public sealed class FoodAwarenessTests
{
    private static FoodAwarenessCatalogue Catalogue(int count, bool dense=false, bool reverse=false)
    {
        var shops=Enumerable.Range(0,count).Select(i=>new FoodScope {BusinessId="generation:"+i.ToString("D5"),PlaceId="p"+i,ProductId=FoodProducts.Croissant});
        if(reverse) shops=shops.Reverse();
        return new FoodAwarenessCatalogue(shops,id=>new Position(dense ? 10 : int.Parse(id.Substring(1))*100,0,0));
    }
    private static FoodAwarenessImage Sample(FoodAwarenessCatalogue c,int day=1,FoodAwarenessSettings? settings=null,IFoodAwarenessProvider? memory=null,FoodAwarenessDump? dump=null)
        => c.Sample(77,"consumer",day,new Position(0,0,0),new Position(0,0,0),new Position(0,0,0),settings ?? new(),_=>true,memory,null,dump);
    private static string Image(FoodAwarenessImage image) => JsonConvert.SerializeObject(image);
    private sealed class Four : IFoodAwarenessProvider
    {
        public int Calls;
        public string? Reference(string consumer,int day,int slot) { Calls++;return "generation:"+slot.ToString("D5"); }
    }
    private sealed class Unknown : IFoodAwarenessProvider
    {
        public string? Reference(string consumer,int day,int slot) => throw new Exception("Campaign provider must never be consulted by C2a");
    }
    [Theory] [InlineData(0)] [InlineData(1024)] [InlineData(20000)]
    public void AllBoundsAndPermutationIndependenceAtFutureRivalScale(int count)
    {
        var a=Catalogue(count,true);var b=Catalogue(count,true,true);var memory=new Four();
        for(int day=0;day<20;day++)
        {
            var dump=new FoodAwarenessDump();var image=Sample(a,day,memory:memory,dump:dump);
            Assert.Equal(a.Revision,b.Revision);Assert.Equal(Image(image),Image(Sample(b,day,memory:memory)));
            Assert.InRange(image.Union.Count,0,24);Assert.InRange(dump.Samples.Count,0,32);
            Assert.Equal(8,dump.Samples.Count(s=>s.Source==FoodAwarenessSource.Campaign && s.Result=="unknown"));
            Assert.Equal(24,dump.Samples.Count(s=>s.Source!=FoodAwarenessSource.Campaign));
            var shortlist=a.Rank(image,new(0,0,0),new ResidentTraits {Bands="2222222222"},new(),new(1,"consumer",day,MealKind.Lunch,"stay",1),120);
            Assert.InRange(shortlist.Length,0,8);Assert.InRange(shortlist.Length*2,0,16);
            Assert.Equal(shortlist.Length,shortlist.Select(s=>s.BusinessId).Distinct().Count());
        }
    }
    private sealed class IndexedMarket : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision
    {
        sealed class Batch : IDisposable { public void Dispose() {} }
        readonly Dictionary<string,FoodBusiness> shops;
        public IndexedMarket(IEnumerable<FoodBusiness> businesses) { shops=businesses.ToDictionary(b=>b.Id); }
        public int Predicates,Reads;
        public long AwarenessRevision=>0;
        public double EarliestMinute=>0;
        public IDisposable BeginCapture(double at)=>new Batch();
        public bool IsCandidate(FoodScope scope) { Predicates++;return shops.ContainsKey(scope.BusinessId); }
        public FoodBusiness? CaptureBusiness(string id,double at) { Reads++;return shops.TryGetValue(id,out var b) ? b : null; }
        public IReadOnlyList<FoodBusiness> Read(double at)=>throw new Exception("All-shop read forbidden");
        public FoodSale Buy(ResidentMeal meal,double at)=>throw new Exception("No checkout before decision capture");
    }
    [Theory] [InlineData(1024)] [InlineData(20000)]
    public void ProductionDecisionRequestsAtMostSixteenRoutesAndChecksOnlySampledMetadata(int count)
    {
        var (prepared,source,planner)=WeightedFoodChoiceTests.Ready(79,count);prepared.State.Food!.AwarenessStartsDay=1;
        var market=new IndexedMarket(source.Shops);var city=new CitySimulator(prepared.State,planner);city.AttachFoodMarket(market);
        city.InspectFoodAwareness("r");market.Predicates=0; // explicit shared build is outside this consumer's work
        var meal=city.State.Food!.Residents[0];city.AdvanceTo(meal.DesiredAt,1);
        Assert.InRange(market.Predicates,0,24);Assert.InRange(market.Reads,0,8);
        var decision=meal.Decision!;
        while(decision.Result==FoodDecisionResult.Evaluating) city.AdvanceTo(meal.DesiredAt,1);
        Assert.InRange(planner.Out+planner.Back,0,16);Assert.InRange(decision.Shops.Count,0,8);Assert.InRange(decision.Awareness!.Union.Count,0,24);
    }
    [Fact]
    public void ManagedAndFutureExternalMetadataShareTheSameEightSlotsWithoutAuthorityEnrollment()
    {
        var entries=Enumerable.Range(0,16).Select(i=>new FoodAwarenessCatalogue.Entry("generation:"+i.ToString("D5"),"p"+i,new Position(0,0,0),i<8 ? FoodAlternativeKind.Managed : FoodAlternativeKind.External));
        var c=new FoodAwarenessCatalogue(entries);bool external=false,managed=false;
        for(int day=0;day<20;day++)
        {
            var selected=c.Rank(Sample(c,day),new(0,0,0),new ResidentTraits {Bands="2222222222"},new(),new(77,"consumer",day,MealKind.Lunch,"stay",1),120);
            Assert.InRange(selected.Length,1,8);external|=selected.Any(e=>e.Kind==FoodAlternativeKind.External);managed|=selected.Any(e=>e.Kind==FoodAlternativeKind.Managed);
        }
        Assert.True(external && managed);
    }
    [Fact]
    public void CachedPrefixOptimizationPreservesOriginalSourceDrawKeys()
    {
        var c=Catalogue(1024,true);var dump=new FoodAwarenessDump();Sample(c,7,dump:dump);
        uint Original(FoodAwarenessSource source,int slot,string domain)
        {
            uint h=StableRandom.Append(StableRandom.Begin(77),"pc:awareness:v1:"+domain);
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),8);h=StableRandom.Append(StableRandom.Append(h,':'),"consumer");
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),7);
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),(int)source);
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),slot);
            return StableRandom.Finish(StableRandom.Append(StableRandom.Append(h,'|'),c.Revision));
        }
        foreach(var sample in dump.Samples.Where(s=>s.Source!=FoodAwarenessSource.Campaign && s.Source!=FoodAwarenessSource.Memory))
            Assert.Equal("generation:"+(Original(sample.Source,sample.Slot,"slot")%1024).ToString("D5"),sample.BusinessId);
    }
    [Fact]
    public void DenseBucketsReachLastEntriesWithoutFirstEntryBias()
    {
        var c=Catalogue(1024,true);var seen=new HashSet<string>();
        for(int day=0;day<2000;day++)
            foreach(var x in Sample(c,day).Union.Where(x=>(x.Sources & FoodAwarenessSource.Home)!=0)) seen.Add(x.BusinessId);
        Assert.True(seen.Count>1000);Assert.Contains("generation:01023",seen);
    }
    [Fact]
    public void EveryDistantShopHasPositiveDaySeededProbabilityWithoutSavedDailyLists()
    {
        var c=Catalogue(64);var seen=new HashSet<string>();
        var settings=new FoodAwarenessSettings {DrawsPerAnchor=0,DiscoveryProbability=.20};
        for(int day=0;day<2000;day++)
        {
            var image=Sample(c,day,settings);Assert.Equal(Image(image),Image(Sample(c,day,settings)));
            foreach(var x in image.Union) {Assert.Equal(FoodAwarenessSource.Discovery,x.Sources);seen.Add(x.BusinessId);}
        }
        Assert.Equal(64,seen.Count);Assert.Contains("generation:00063",seen);
    }
    [Fact]
    public void OutsideRadiusAndDuplicatesConsumeDrawsWithoutRefill()
    {
        var c=new FoodAwarenessCatalogue(new[]{new FoodScope {BusinessId="far",PlaceId="far"}},_=>new Position(400,0,0));
        var dump=new FoodAwarenessDump();var image=Sample(c,dump:dump);
        Assert.Equal(12,dump.Samples.Count(x=>x.Source==FoodAwarenessSource.Home || x.Source==FoodAwarenessSource.Routine || x.Source==FoodAwarenessSource.Origin));
        Assert.All(dump.Samples.Where(x=>x.Source==FoodAwarenessSource.Home),x=>Assert.Equal("far",x.BusinessId));
        var entry=Assert.Single(image.Union);
        // Discovery is allowed to expose this far shop; the rejected local draws cannot.
        Assert.Equal((entry.Sources & FoodAwarenessSource.Discovery)!=0,entry.Eligible);
        Assert.Equal(entry.Eligible ? 1 : 0,c.Rank(image,new(0,0,0),new ResidentTraits {Bands="2222222222"},new(),new(1,"c",1,MealKind.Lunch,"x",1),120).Length);
        var dense=Catalogue(1,true);var duplicates=Sample(dense);Assert.Single(duplicates.Union);Assert.True(duplicates.Union[0].Eligible);
    }
    [Fact]
    public void EvenNonemptyCampaignProviderCannotFabricateC2aAdmissions()
    {
        var c=Catalogue(8,true);var campaign=new Four();var dump=new FoodAwarenessDump();
        var image=c.Sample(77,"consumer",1,null,null,null,new FoodAwarenessSettings {DrawsPerAnchor=0},_=>true,
            campaign:campaign,diagnostic:dump);
        Assert.Equal(0,campaign.Calls);Assert.DoesNotContain(image.Union,x=>(x.Sources & FoodAwarenessSource.Campaign)!=0);
        Assert.Equal(8,dump.Samples.Count(x=>x.Source==FoodAwarenessSource.Campaign && x.Result=="unknown"));
        Assert.Equal(Image(image),Image(c.Sample(77,"consumer",1,null,null,null,new FoodAwarenessSettings {DrawsPerAnchor=0},_=>true,campaign:EmptyFoodAwarenessProvider.Instance)));
    }
    [Fact]
    public void MemorySeamUsesExactlyFourDirectGenerationLookupsAndCampaignIsAlwaysUnknown()
    {
        var c=Catalogue(8);var settings=new FoodAwarenessSettings {DrawsPerAnchor=0,DiscoveryProbability=.001};
        var none=c.Sample(77,"consumer",1,null,null,null,settings,_=>true,EmptyFoodAwarenessProvider.Instance,new Unknown());
        var memory=new Four();var four=c.Sample(77,"consumer",1,null,null,null,settings,_=>true,memory,new Unknown());
        Assert.Equal(4,memory.Calls);Assert.InRange(four.Union.Count-none.Union.Count,0,4);
        Assert.Equal(4,four.Union.Count(x=>(x.Sources & FoodAwarenessSource.Memory)!=0));
        var replaced=new FoodAwarenessCatalogue(new[]{new FoodScope {BusinessId="new generation",PlaceId="p0"}},_=>new Position(0,0,0));
        var after=replaced.Sample(77,"consumer",1,null,null,null,settings,_=>true,memory);
        Assert.DoesNotContain(after.Union,x=>(x.Sources & FoodAwarenessSource.Memory)!=0);Assert.NotEqual(c.Revision,replaced.Revision);
    }
    [Fact]
    public void RefusalAfterAdmissionStillEnforcesPriceStockAndDeadline()
    {
        foreach(int refusal in new[]{0,1,2,3})
        {
            var (city,market,planner)=WeightedFoodChoiceTests.Ready(79,24);city.State.Food!.AwarenessStartsDay=1;city.InspectFoodAwareness("r");
            foreach(var b in market.Shops)
            {
                if(refusal==0) b.Offers[0].Price=Money.FromCents(20000);
                if(refusal==1) b.Offers[0].Units=0;
                if(refusal==2) b.MealsPerHour=1;
                if(refusal==3) b.Hours.Clear();
            }
            var meal=city.State.Food.Residents.Single();FoodDecisionState? captured=null;city.FoodDecisionObserved+=x=>captured=x.Decision;
            while(!city.AdvanceTo(meal.DesiredAt,1).CaughtUp) {}
            Assert.NotNull(captured!.Awareness);Assert.Contains(captured.Awareness!.Union,x=>x.Eligible);
            Assert.Equal(FoodDecisionResult.NoFeasible,captured.Result);Assert.Empty(market.Orders);Assert.InRange(planner.Out+planner.Back,0,16);
        }
    }
    [Fact]
    public void AdmittedOffersStillUseTasteAndCanLoseToTheFundedHouseholdAlternative()
    {
        var catalogue=Catalogue(2,true);var image=Sample(catalogue,memory:new Four());
        var neutral=new ResidentTraits {Bands="2222222222"};var taste=new ResidentTraits {Bands="2422222222"};
        int neutralBurgers=0,tasteBurgers=0,outside=0;
        for(int seed=0;seed<4096;seed++)
        {
            var draw=new FoodDrawContext(seed,"consumer",1,MealKind.Lunch,"stay",1);
            var admitted=catalogue.Rank(image,new(0,0,0),neutral,new(),draw,120);
            Assert.Equal(2,admitted.Length);
            var offers=admitted.Select(e=>new EvaluatedFoodOffer(e.BusinessId,e.BusinessId=="generation:00000" ? FoodProducts.Burger : FoodProducts.Croissant,
                Money.FromCents(100),1,0,12,90,null,null,MealKind.Lunch)).ToArray();
            var a=FoodChoicePolicy.Select(offers,neutral,Money.FromCents(1000),draw,new(),true);
            var b=FoodChoicePolicy.Select(offers,taste,Money.FromCents(1000),draw,new(),true);
            if(a.Outside) outside++;else if(offers[a.Index].ProductId==FoodProducts.Burger) neutralBurgers++;
            if(!b.Outside && offers[b.Index].ProductId==FoodProducts.Burger) tasteBurgers++;
        }
        Assert.InRange(outside,1,4095);Assert.True(tasteBurgers>neutralBurgers);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(6)] [InlineData(10)]
    public void HeldAwarenessReloadAndCadenceRetainIdentityWithChangedLiveSettingsAndCatalogue(int units)
    {
        var (control,mc,pc)=WeightedFoodChoiceTests.Ready(79,24);control.State.Food!.AwarenessStartsDay=1;control.InspectFoodAwareness("r");
        var (changed,mm,pm)=WeightedFoodChoiceTests.Ready(79,24);changed.State.Food!.AwarenessStartsDay=1;changed.InspectFoodAwareness("r");
        double at=changed.State.Food.Residents.Single().DesiredAt;
        for(int i=0;i<units;i++) changed.AdvanceTo(at,1);
        var held=changed.State.Food.Residents.Single().Decision;Assert.NotNull(held!.Awareness);
        string checkpoint=SaveCodec.Write(changed.State);var read=SaveCodec.Read(checkpoint);Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        changed=new CitySimulator(read.State!,pm);changed.AttachFoodMarket(mm,awarenessSettings:new FoodAwarenessSettings {RadiusMetres=25,DrawsPerAnchor=0,DiscoveryProbability=.001});
        Assert.Equal(checkpoint,SaveCodec.Write(changed.State));
        while(!control.AdvanceTo(at+20,128).CaughtUp) {}
        while(!changed.AdvanceTo(at+20,1).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(control.State),SaveCodec.Write(changed.State));Assert.Equal(mc.Orders,mm.Orders);Assert.Equal(pc.Out,pm.Out);Assert.InRange(pm.Out+pm.Back,0,17);
    }
    [Theory]
    [InlineData("null",250,.02,4)] [InlineData("true",250,.02,4)]
    [InlineData("{}",250,.02,4)] [InlineData("[]",250,.02,4)]
    [InlineData("\"NaN\"",250,.02,4)] [InlineData("1e100",1000,.20,4)]
    [InlineData("Infinity",1000,.20,4)] [InlineData("-Infinity",25,.001,0)]
    [InlineData("\"Infinity\"",1000,.20,4)] [InlineData("\"-Infinity\"",25,.001,0)] [InlineData("NaN",250,.02,4)]
    [InlineData("-1e100",25,.001,0)] [InlineData("2.5",25,.20,4)]
    public void FlatSettingsClampOrDefaultOnceWithoutDiscardingOtherKeys(string value,double radius,double probability,int draws)
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>("{\"Enabled\":false,\"FoodMarketingNeighbourhoodMetres\":"+value+",\"FoodMarketingDiscoveryDailyProbability\":"+value+",\"FoodMarketingNeighbourhoodDrawsPerAnchor\":"+value+"}")!;
        var log=new List<string>();settings.Awareness.Clamp(log.Add);int messages=log.Count;
        settings.Awareness.Clamp(log.Add);Assert.Equal(messages,log.Count);Assert.False(settings.Enabled);
        Assert.Equal(radius,settings.Awareness.RadiusMetres);Assert.Equal(probability,settings.Awareness.DiscoveryProbability);Assert.Equal(draws,settings.Awareness.DrawsPerAnchor);
    }
    [Fact]
    public void DiagnosticIsReadOnlyAndUnchangedPublicationReusesIndex()
    {
        var (city,_,planner)=WeightedFoodChoiceTests.Ready(79,24);city.State.Food!.AwarenessStartsDay=1;city.InspectFoodAwareness("r");
        string checkpoint=SaveCodec.Write(city.State);
        var a=city.InspectFoodAwareness("r");var b=city.InspectFoodAwareness("r");
        Assert.Equal(JsonConvert.SerializeObject(a),JsonConvert.SerializeObject(b));Assert.Equal(checkpoint,SaveCodec.Write(city.State));Assert.Equal(0,planner.Out+planner.Back);
        city.State.Food.Shops[0].BusinessId="replacement";city.InvalidateFoodAwareness();
        Assert.NotEqual(a!.Image.CatalogueRevision,city.InspectFoodAwareness("r")!.Image.CatalogueRevision);
    }
    [Fact]
    public void SelectedAwarenessContextNeverConsultsProvidersAgainAfterReload()
    {
        var (control,mc,pc)=WeightedFoodChoiceTests.Ready(79,24);control.State.Food!.AwarenessStartsDay=1;control.InspectFoodAwareness("r");
        var (changed,mm,pm)=WeightedFoodChoiceTests.Ready(79,24);changed.State.Food!.AwarenessStartsDay=1;changed.InspectFoodAwareness("r");
        double at=changed.State.Food.Residents.Single().DesiredAt;changed.AdvanceTo(at,1);
        var selected=changed.State.Food.Residents.Single().Decision!;
        while(selected.Result==FoodDecisionResult.Evaluating) changed.AdvanceTo(at,1);
        Assert.Equal(FoodDecisionResult.Selected,selected.Result);Assert.NotNull(selected.Awareness);
        var read=SaveCodec.Read(SaveCodec.Write(changed.State));Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        changed=new CitySimulator(read.State!,pm);changed.AttachFoodMarket(mm,awarenessSettings:new FoodAwarenessSettings {DrawsPerAnchor=0},memory:new Unknown(),campaign:new Unknown());
        while(!control.AdvanceTo(at+20,128).CaughtUp) {}
        while(!changed.AdvanceTo(at+20,1).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(control.State),SaveCodec.Write(changed.State));Assert.Equal(mc.Orders,mm.Orders);Assert.Equal(pc.Out,pm.Out);
    }
    private static JObject Held()
    {
        var (city,_,_)=WeightedFoodChoiceTests.Ready(79,24);city.State.Food!.AwarenessStartsDay=1;city.InspectFoodAwareness("r");
        city.AdvanceTo(city.State.Food.Residents.Single().DesiredAt,1);return JObject.Parse(SaveCodec.Write(city.State));
    }
    [Fact]
    public void Schema18MigratesOnceAndNeverRecapturesHeldShortlist()
    {
        var json=Held();json["Schema"]=18;json["Food"]!["AwarenessStartsDay"]!.Parent!.Remove();
        var decision=json["Food"]!["Residents"]![0]![9]!;decision["Awareness"]!.Parent!.Remove();
        string source=json.ToString(Formatting.None);var data=new Dictionary<string,string> {[SaveStore.StateKey]=source};
        decision["Awareness"]=new JObject();Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(json.ToString(Formatting.None)).Status);((JObject)decision).Remove("Awareness");
        var read=SaveCodec.Read(source);Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);Assert.Equal(18,read.SourceSchema);Assert.Equal(19,read.State!.Schema);
        Assert.Equal((int)(read.State.Minute/1440)+1,read.State.Food!.AwarenessStartsDay);Assert.Null(read.State.Food.Residents[0].Decision!.Awareness);
        Assert.Equal(SaveCodec.Write(read.State),SaveCodec.Write(SaveCodec.Read(SaveCodec.Write(read.State)).State!));Assert.Equal(source,data[SaveStore.StateKey]);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void MalformedAwarenessRefusesWithoutReplacingSource(int corruption)
    {
        var json=Held();var image=json["Food"]!["Residents"]![0]![9]!["Awareness"]!;
        if(corruption==0) image["CatalogueRevision"]="bad";
        if(corruption==1) image["Union"]![0]!["Sources"]=32;
        if(corruption==2) ((JArray)image["Union"]!).Add(image["Union"]![0]!.DeepClone());
        if(corruption==3) image["Union"]![0]!["BusinessId"]="";
        if(corruption==4) foreach(var x in image["Union"]!) x["Eligible"]=false;
        if(corruption==5) for(int i=0;i<33;i++) ((JArray)image["Union"]!).Add(image["Union"]![0]!.DeepClone());
        string source=json.ToString(Formatting.None);var data=new Dictionary<string,string> {[SaveStore.StateKey]=source};
        var read=SaveSession.Open(data,"weighted").InitialReadResult;Assert.Equal(SaveReadStatus.Invalid,read.Status);Assert.Equal(source,data[SaveStore.StateKey]);
    }
}
