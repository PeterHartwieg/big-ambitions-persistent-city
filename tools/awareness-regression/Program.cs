using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static class Program
{
    sealed class Paths : IRoutePlanner
    {
        public Route? Plan(Place a,Place b,double at,TravelPreferences p)=>null;
    }
    sealed class Market : IFoodMarket
    {
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double at)=>Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal,double at)=>throw new Exception("No checkout in awareness benchmark");
    }
    sealed class Memory : IFoodAwarenessProvider
    {
        readonly FoodScope[] scopes;
        public Memory(FoodScope[] scopes) { this.scopes=scopes; }
        public string? Reference(string consumer,int day,int slot)=>scopes[slot].BusinessId;
    }
    static readonly ResidentTraits Traits=new() {Bands="2222222222"};
    static readonly FoodChoiceSettings Choice=new();
    static readonly FoodAwarenessSettings Settings=new();
    static object Stats(double[] values)
    {
        Array.Sort(values);return new {p50=values[values.Length/2],p95=values[(int)(values.Length*.95)],max=values[^1],spikesAtLeast30ms=values.Count(x=>x>=30)};
    }
    // Exact B0b admission algorithm (FoodWeightedCommerce.Shortlist at c0af745), with setup,
    // native metadata publication and scope copying excluded on both sides. It visits all shops.
    static string[] Baseline(FoodScope[] scopes,Dictionary<string,Position> positions,FoodDrawContext draw)
    {
        var selected=new FoodScope[8];var ranks=new double[8];int count=0;
        foreach(var scope in scopes)
        {
            double minutes=Position.Distance(new(0,0,0),positions[scope.PlaceId])/120;
            double rank=FoodDrawContext.Rank(draw.ShopHash(scope.BusinessId),FoodChoicePolicy.DistanceWeight(minutes,Traits,Choice,true));
            int insert=0;while(insert<count && (ranks[insert]<rank || ranks[insert]==rank && StringComparer.Ordinal.Compare(selected[insert].BusinessId,scope.BusinessId)<0)) insert++;
            if(insert==8) continue;
            for(int j=Math.Min(count,7);j>insert;j--) {selected[j]=selected[j-1];ranks[j]=ranks[j-1];}
            selected[insert]=scope;ranks[insert]=rank;count=Math.Min(count+1,8);
        }
        return selected.Take(count).Select(s=>s.BusinessId).ToArray();
    }
    static object Bench(int count,bool dense=true)
    {
        var scopes=Enumerable.Range(0,count).Select(i=>new FoodScope {BusinessId="business:ba%3Astreet_thirdstreet:"+i+":1:ba%3Abusiness_fastfoodrestaurant:player",PlaceId="p"+i,ProductId=FoodProducts.Croissant}).ToArray();
        // Dense is deliberately worst case for bucket occupancy; no dense scan is permitted.
        var positions=scopes.Select((s,i)=>(s.PlaceId,Point:dense ? new Position(10,0,0) : new Position(i%200*125,0,i/200*125))).ToDictionary(p=>p.PlaceId,p=>p.Point);
        var memory=new Memory(scopes);var build=new double[12];var buildAlloc=new long[12];FoodAwarenessCatalogue catalogue=null!;
        for(int i=0;i<build.Length;i++)
        {
            long bytes=GC.GetAllocatedBytesForCurrentThread(),at=Stopwatch.GetTimestamp();
            catalogue=new FoodAwarenessCatalogue(scopes,id=>positions[id]);
            build[i]=Stopwatch.GetElapsedTime(at).TotalMilliseconds;buildAlloc[i]=GC.GetAllocatedBytesForCurrentThread()-bytes;
        }
        const int warmup=300,samples=3000;
        var before=new double[samples];var after=new double[samples];var baselineBytes=new long[samples];var candidateBytes=new long[samples];
        int maxUnion=0,maxShortlist=0;FoodAwarenessImage held=null!;
        for(int i=-warmup;i<samples;i++)
        {
            int day=i+warmup;var draw=new FoodDrawContext(77,"consumer",day,MealKind.Lunch,"stay",1);
            void Old()
            {
                long bytes=GC.GetAllocatedBytesForCurrentThread(),at=Stopwatch.GetTimestamp();
                var selected=Baseline(scopes,positions,draw);
                if(i>=0) { before[i]=Stopwatch.GetElapsedTime(at).TotalMilliseconds;baselineBytes[i]=GC.GetAllocatedBytesForCurrentThread()-bytes; }
                GC.KeepAlive(selected);
            }
            void New()
            {
                long bytes=GC.GetAllocatedBytesForCurrentThread(),at=Stopwatch.GetTimestamp();
                held=catalogue.Sample(77,"consumer",day,new(0,0,0),new(0,0,0),new(0,0,0),Settings,_=>true,memory);
                var selected=catalogue.Rank(held,new(0,0,0),Traits,Choice,draw,120);
                if(i>=0) { after[i]=Stopwatch.GetElapsedTime(at).TotalMilliseconds;candidateBytes[i]=GC.GetAllocatedBytesForCurrentThread()-bytes; }
                maxUnion=Math.Max(maxUnion,held.Union.Count);maxShortlist=Math.Max(maxShortlist,selected.Length);
                GC.KeepAlive(selected);
            }
            if((day&1)==0) {Old();New();} else {New();Old();}
        }
        return new {alternatives=count,seed=77,dense,buildMs=Stats(build.Skip(2).ToArray()),buildAllocatedBytes=buildAlloc.Skip(2).Average(),samples,
            baselineAdmissionMs=Stats(before),candidateAdmissionMs=Stats(after),baselineAllocatedBytes=baselineBytes.Average(),candidateAllocatedBytes=candidateBytes.Average(),maxUnion,maxShortlist,maxRouteSlots=2*maxShortlist,
            heldImageJsonBytes=System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(held))};
    }
    // Isolate C2a's added bytes against its frozen schema-18 source shape. E1's
    // additive weather bytes are reported separately, never passed to the old reader.
    static JObject WithoutWeather20(JObject root)
    {
        root["Schema"]=19;root.Remove("WeatherStartsDay");
        foreach(JObject citizen in root["Citizens"]!) citizen.Remove("W");
        foreach(JArray row in root["Food"]!["Residents"]!)
        {
            string memory=(string)row[35]!;row[35]=memory[..^2];
            if(row[9] is JObject decision) {decision.Remove("Weather");decision.Remove("WeatherTravelMultiplier");}
        }
        return root;
    }
    static object Saved(int residents)
    {
        var state=EconomyScenario.Create(residents,11);state.Food=EconomyScenario.Food();
        var city=new CitySimulator(state,new Paths());city.AttachFoodMarket(new Market());
        var meal=state.Food.Residents[0];meal.ContextAttempts=1;meal.LastContextKey="held:size";
        var d=meal.Decision=new FoodDecisionState {Tuning=FoodDecisionTuning.Capture(Choice),IntentId=meal.IntentId,ContextKey=meal.LastContextKey,Attempt=1,OriginAt=state.Minute,Deadline=state.Minute+90};
        state.Food.ChoiceStartsDay=meal.Day;
        var image=new FoodAwarenessImage {CatalogueRevision=Convert.ToBase64String(new byte[32])};
        // Maximum 24 unique C2a slots, each counted once. IDs model native complete generation identities.
        for(int i=0;i<24;i++) image.Union.Add(new FoodAwarenessCandidate {BusinessId="business:ba%3Astreet_thirdstreet:"+i.ToString("D2")+":1:ba%3Abusiness_fastfoodrestaurant:player",Eligible=true,
            Sources=i<4 ? FoodAwarenessSource.Memory : i<8 ? FoodAwarenessSource.Home : i<12 ? FoodAwarenessSource.Routine : i<16 ? FoodAwarenessSource.Origin : FoodAwarenessSource.Discovery});
        for(int i=0;i<8;i++) d.Shops.Add(new FoodDecisionShop {BusinessId=image.Union[i].BusinessId,PlaceId="shop-1",NextServiceAt=state.Minute});
        var before=WithoutWeather20(JObject.Parse(SaveCodec.Write(state)));before["Schema"]=18;((JObject)before["Food"]!).Remove("AwarenessStartsDay");
        ((JObject)before["Food"]!["Residents"]![0]![9]!).Remove("Awareness");
        string baseline=before.ToString(Formatting.None);if(SaveCodec.Read(baseline).Status!=SaveReadStatus.Loaded) throw new Exception("Invalid B0b fixture");
        d.Awareness=image;string candidate=SaveCodec.Write(state);var read=SaveCodec.Read(candidate);
        if(read.Status!=SaveReadStatus.Loaded || SaveCodec.Write(read.State!)!=candidate) throw new Exception("Awareness save replay: "+read.Error);
        var wireBytes=System.Text.Encoding.UTF8.GetByteCount(candidate);
        var bytes=System.Text.Encoding.UTF8.GetByteCount(WithoutWeather20(JObject.Parse(candidate)).ToString(Formatting.None));var baselineBytes=System.Text.Encoding.UTF8.GetByteCount(baseline);
        var counts=SaveCodec.CountRecords(state,candidate);
        long allocation=GC.GetAllocatedBytesForCurrentThread();
        var clone=new FoodAwarenessImage {CatalogueRevision=image.CatalogueRevision};
        foreach(var entry in image.Union) clone.Union.Add(new FoodAwarenessCandidate {BusinessId=entry.BusinessId,Eligible=entry.Eligible,Sources=entry.Sources});
        long managedGraphBytes=GC.GetAllocatedBytesForCurrentThread()-allocation;GC.KeepAlive(clone);
        // Equal held image exists once even at the 150k-consumer ceiling. No per-consumer delta.
        return new {residents,consumers=state.Food.Residents.Count,baselineBytes,candidateBytes=bytes,addedBytes=bytes-baselineBytes,awarenessProjectionSchema=19,currentWireBytes=wireBytes,weatherBytes=wireBytes-bytes,managedGraphBytesSharedStrings=managedGraphBytes,heldImageBytes=System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(image)),counts.Streaming,counts.Semantic};
    }
    static object ReviewRebuildUnits(int count,bool dense)
    {
        var entries=Enumerable.Range(0,count).Select(i=>new FoodAwarenessCatalogue.Entry(
            "business:ba%3Astreet_thirdstreet:"+i+":1:ba%3Abusiness_fastfoodrestaurant:player","p"+i,
            dense ? new Position(10,0,0) : new Position(i%200*125,0,i/200*125))).ToArray();
        var scopes=entries.Select(e=>new FoodScope {BusinessId=e.BusinessId,PlaceId=e.PlaceId}).ToArray();
        var positions=entries.ToDictionary(e=>e.PlaceId,e=>e.Position);
        var units=new List<double>();var totals=new List<double>();var spikes=new List<object>();int unitCount=0,maxItems=0;
        for(int iteration=0;iteration<6;iteration++)
        {
            long first=Stopwatch.GetTimestamp();using var builder=FoodAwarenessCatalogue.BeginBuild(scopes,id=>positions[id]);
            double total=0;bool complete;
            do
            {
                var stage=builder.Stage;long started=unitCount==0 || total==0 ? first : Stopwatch.GetTimestamp();
                complete=builder.Continue();double ms=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                units.Add(ms);total+=ms;unitCount++;maxItems=Math.Max(maxItems,builder.LastWorkItems);
                if(ms>=30) spikes.Add(new {iteration,stage,ms});
                if(builder.LastWorkItems>FoodAwarenessCatalogue.Builder.MaximumWorkPerUnit) throw new Exception("Rebuild unit exceeded work bound");
            }while(!complete);
            totals.Add(total);
        }
        return new {alternatives=count,dense,iterations=6,includesColdJit=true,unitCount,maxItems,unitMs=Stats(units.ToArray()),totalCpuMs=Stats(totals.ToArray()),spikes};
    }
    static void Main(string[] args)
    {
        if(args.Contains("--review-rebuild"))
        {
            Console.WriteLine(JsonConvert.SerializeObject(new {workPerUnit=FoodAwarenessCatalogue.Builder.MaximumWorkPerUnit,
                cases=new[]{ReviewRebuildUnits(1024,true),ReviewRebuildUnits(20000,true),ReviewRebuildUnits(1024,false),ReviewRebuildUnits(20000,false)}},Formatting.Indented));return;
        }
        Console.WriteLine(JsonConvert.SerializeObject(new {schema=CityState.CurrentSchema,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            baseline="c0af745 A3/B0b metadata rank loop; native reads/routes excluded",policy=new {radiusMetres=250,drawsPerAnchor=4,discoveryDailyProbability=.02},
            stages=new[]{Bench(1024),Bench(20000),Bench(1024,false),Bench(20000,false)},saved=new[]{Saved(957),Saved(2500)}},Formatting.Indented));
    }
}
