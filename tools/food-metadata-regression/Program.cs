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
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<samples;i++)
        {
            long start=Stopwatch.GetTimestamp();
            for(int j=0;j<batch;j++) action();
            timings[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds/batch;
        }
        long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Array.Sort(timings);
        return new {samples,batch,p50=timings[samples/2],p95=timings[(int)(samples*.95)],max=timings[^1],
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
    private static object Resolver(int count)
    {
        var resolver=ParseTable(Table(count),false).Resolver;
        string name=count==0 ? "foodtrucks:itemname_dumplings" : "foodtrucks:itemname_"+(count-1);
        return new {count,lookup=Measure(()=>sink+=resolver.Resolve(name,Traits).BreakfastFitCents),
            miss=Measure(()=>sink+=resolver.Resolve("missing:item",Traits).LunchFitCents)};
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
            resolvedWeights=Measure(()=>{foreach(var o in offers) sink+=FoodChoicePolicy.Weight(o,Traits,Allowance,Settings,FoodProductMetadataResolver.Empty.Resolve(o.ProductId,Traits));})};
    }
    private static object Parse(int count)
    {
        string table=Table(count), config="{\"FoodProductOverrides\":"+table+"}";
        if(ParseTable(table,false).Resolver.Count!=count) throw new Exception("Config count mismatch.");
        return new {count,characters=config.Length,utf8=System.Text.Encoding.UTF8.GetByteCount(config),
            tableBefore=Measure(()=>sink+=ParseTable(table,true).Resolver.Count,101,1),
            tableAfter=Measure(()=>sink+=ParseTable(table,false).Resolver.Count,101,1),
            coldConfig=Measure(()=>sink+=JsonConvert.DeserializeObject<PrototypeSettings>(config)!.FoodProductOverrides.Resolver.Count,101,1)};
    }
    public static void Main()
    {
        long coldBytes=GC.GetAllocatedBytesForCurrentThread(), coldStart=Stopwatch.GetTimestamp();
        sink+=JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodProductOverrides\":"+Table(256)+"}")!.FoodProductOverrides.Resolver.Count;
        double coldMs=Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;
        long coldAllocation=GC.GetAllocatedBytesForCurrentThread()-coldBytes;
        // Cold here means a new reader/object/table each call; CLR/JIT and Json.NET contracts are warm.
        var results=new {baseCommit="034bb76",parserBefore="c365731",units="ms/call",nativeEvidence=false,
            firstUseMaxConfig=new {milliseconds=coldMs,bytes=coldAllocation,spikesAtLeast30ms=coldMs>=30 ? 1 : 0},
            resolver=new[]{Resolver(0),Resolver(1),Resolver(256)},weights=new[]{Weights(4),Weights(8)},config=new[]{Parse(0),Parse(1),Parse(256)},sink};
        Console.WriteLine(JsonConvert.SerializeObject(results,Formatting.Indented));
    }
}
