using System.Diagnostics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;

static class Program
{
    private static double sink;
    private static readonly ResidentTraits Traits=new() {Bands="2222222222"};
    private static readonly FoodChoiceSettings Settings=new();
    private static readonly Money Allowance=Money.FromCents(1000);
    private static object Measure(Action action,int samples=1001,int batch=100)
    {
        for(int i=0;i<1000;i++) action();
        var timings=new double[samples];
        int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<samples;i++)
        {
            long start=Stopwatch.GetTimestamp();
            for(int j=0;j<batch;j++) action();
            timings[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds/batch;
        }
        long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Array.Sort(timings);
        return new {samples,batch,gc0=GC.CollectionCount(0)-gc0,gc1=GC.CollectionCount(1)-gc1,gc2=GC.CollectionCount(2)-gc2,p50=timings[samples/2],p95=timings[(int)(samples*.95)],max=timings[^1],
            bytesPerCall=allocated/(double)(samples*batch),spikesAtLeast30ms=timings.Count(t=>t>=30),
            maxBatchMs=timings[^1]*batch,
            batchSpikesAtLeast30ms=timings.Count(t=>t*batch>=30)};
    }
    private static string Table(int count) => "{"+string.Join(",",Enumerable.Range(0,count).Select(i=>JsonConvert.ToString("foodtrucks:itemname_"+i)+":{\"MealClass\":\"Food\",\"TasteBand\":2,\"BreakfastFitCents\":900,\"LunchFitCents\":100,\"DinnerFitCents\":450}"))+"}";
    private static FoodProductOverrideTable ParseTable(string json,bool before)
    {
        using var input=new StringReader(json); using var reader=new JsonTextReader(input); reader.Read();
        var serializer=new JsonSerializer();
        return before ? new FoodProductOverrideConverterBefore().ReadJson(reader,typeof(FoodProductOverrideTable),null,false,serializer)
            : new FoodProductOverrideConverter().ReadJson(reader,typeof(FoodProductOverrideTable),null,false,serializer);
    }
    private static long Retained(string table)
    {
        // Whole managed heap estimate, isolated from timed samples and excluding retained input JSON.
        _=ParseTable(table,false);
        long before=GC.GetTotalMemory(true);
        var loaded=ParseTable(table,false);
        long delta=GC.GetTotalMemory(true)-before;
        GC.KeepAlive(loaded); GC.KeepAlive(table);
        return delta;
    }
    private static object Resolver(int count)
    {
        var resolver=ParseTable(Table(count),false).Resolver;
        string name=count==0 ? "foodtrucks:itemname_dumplings" : "foodtrucks:itemname_"+(count-1);
        string maximumName=new string('x',256);
        return new {count,retainedTableBytesEstimate=Retained(Table(count)),lookup=Measure(()=>sink+=resolver.Resolve(name,Traits,957,"consumer").BreakfastFitCents),
            miss=Measure(()=>sink+=resolver.Resolve("missing:item",Traits,957,"consumer").LunchFitCents),
            maximumNameMiss=Measure(()=>sink+=resolver.Resolve(maximumName,Traits,957,"consumer").DinnerFitCents)};
    }
    private static object Weights(int width)
    {
        string[] names={FoodProducts.Croissant,FoodProducts.Burger,FoodProducts.HotDog,"foodtrucks:itemname_dumplings",
            "foodtrucks:itemname_choppedcheese","another:food","ba:itemname_donut","ba:itemname_salad"};
        var offers=Enumerable.Range(0,8).SelectMany(shop=>names.Take(width).Select(name=>new EvaluatedFoodOffer("shop"+shop,name,
            Money.FromCents(100),2,1,10,90,null,null,MealKind.Breakfast))).ToArray();
        var draw=new FoodDrawContext(957,"consumer",1,MealKind.Breakfast,"home",1);
        foreach(var offer in offers)
            if(FoodChoicePolicyBefore.Weight(offer,Traits,Allowance,Settings)!=FoodChoicePolicy.Weight(offer,Traits,Allowance,Settings))
                throw new Exception("Historical weight changed.");
        return new {width,offers=offers.Length,
            historicalBefore=Measure(()=>sink+=FoodChoicePolicyBefore.Select(offers,Traits,Allowance,draw,Settings,true).Index),
            historicalAfter=Measure(()=>sink+=FoodChoicePolicy.Select(offers,Traits,Allowance,draw,Settings,true).Index),
            resolvedWeights=Measure(()=>{foreach(var o in offers) sink+=FoodChoicePolicy.Weight(o,Traits,Allowance,Settings,FoodProductMetadataResolver.Empty.Resolve(o.ProductId,Traits,957,"consumer"));})};
    }
    private static object Parse(int count, bool maximumNames=false)
    {
        string table=maximumNames ? "{"+string.Join(",",Enumerable.Range(0,count).Select(i=>JsonConvert.ToString(i.ToString("D3")+new string('\u2028',253))+":{\"MealClass\":\"Food\",\"TasteBand\":2}"))+"}" : Table(count);
        string config="{\"FoodProductOverrides\":"+table+"}";
        if(ParseTable(table,false).Resolver.Count!=count) throw new Exception("Config count mismatch.");
        return new {count,maximumNames,retainedTableBytesEstimate=Retained(table),characters=config.Length,utf8=System.Text.Encoding.UTF8.GetByteCount(config),
            tableBefore=Measure(()=>sink+=ParseTable(table,true).Resolver.Count,101,1),
            tableAfter=Measure(()=>sink+=ParseTable(table,false).Resolver.Count,101,1),
            coldConfig=Measure(()=>sink+=JsonConvert.DeserializeObject<PrototypeSettings>(config)!.FoodProductOverrides.Resolver.Count,101,1)};
    }
    public static void Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--taste-vector")
        {
            Console.WriteLine(string.Join(",",Enumerable.Range(0,100).SelectMany(i=>new[]{"foodtrucks:itemname_dumplings","foodtrucks:itemname_choppedcheese"}.Select(name=>FoodProductMetadataResolver.DefaultTaste(957,"pc:"+i,name)))));
            return;
        }
        string firstConfig="{\"FoodProductOverrides\":"+Table(256)+"}";
        long coldBytes=GC.GetAllocatedBytesForCurrentThread(), coldStart=Stopwatch.GetTimestamp();
        sink+=JsonConvert.DeserializeObject<PrototypeSettings>(firstConfig)!.FoodProductOverrides.Resolver.Count;
        double coldMs=Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;
        long coldAllocation=GC.GetAllocatedBytesForCurrentThread()-coldBytes;
        // Cold here means a new reader/object/table each call; CLR/JIT and Json.NET contracts are warm.
        var results=new {baseCommit="034bb76",parserBefore="policy-matched per-entry HashSet",units="ms/call",nativeEvidence=false,
            firstUseMaxConfig=new {milliseconds=coldMs,bytes=coldAllocation,spikesAtLeast30ms=coldMs>=30 ? 1 : 0},
            resolver=new[]{Resolver(0),Resolver(1),Resolver(256)},weights=new[]{Weights(4),Weights(8)},config=new[]{Parse(0),Parse(1),Parse(256),Parse(256,true)},sink};
        Console.WriteLine(JsonConvert.SerializeObject(results,Formatting.Indented));
    }
}
