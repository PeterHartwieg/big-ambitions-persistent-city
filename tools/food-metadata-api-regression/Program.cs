using System.Diagnostics;
using System.Text.Json;
using PersistentCity.Core;
using Before = PersistentCity.Core.Before;

static class Program
{
    private static long sink;
    private static readonly ResidentTraits Traits = new() { Bands="2222222222" };
    private static readonly string[] Names = Enumerable.Range(0,256).Select(i=>"metadata:product:"+i.ToString("D3")).ToArray();
    private static readonly FoodProductMetadataInput Input = new() { MealClass=MealClass.Drink, TasteBand=4, BreakfastFitCents=12, LunchFitCents=34, DinnerFitCents=56 };
    private static readonly Before.FoodProductMetadataInput BeforeInput = new() { MealClass=MealClass.Drink, TasteBand=4, BreakfastFitCents=12, LunchFitCents=34, DinnerFitCents=56 };
    private static FoodProductMetadataLoadedNames Catalogue() => new(Names);
    private static FoodProductMetadataRegistry Registry()
    { var r=new FoodProductMetadataRegistry(); r.BindPublicationThread(); return r; }
    private static FoodProductMetadataProvider Provider(FoodProductMetadataRegistry r,string id)
    { if(!r.TryCreateProvider(id,out var p,out var reason)) throw new Exception(reason); return p; }
    private static void Fill(FoodProductMetadataProvider p)
    { foreach(string n in Names) if(!p.TryRegisterProductMetadata(n,Input,1,out string reason) && reason!=FoodProductMetadataRegistry.ConflictReason) throw new Exception(reason); }
    private static object Measure(Action action, int samples=501, int batch=1)
    {
        for(int i=0;i<100;i++) action();
        var times=new double[samples]; int[] collections={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<samples;i++)
        {
            long start=Stopwatch.GetTimestamp(); for(int j=0;j<batch;j++) action();
            times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes; Array.Sort(times);
        return new {samples,batch,p50=times[samples/2]/batch,p95=times[(int)(samples*.95)]/batch,max=times[^1]/batch,
            bytesPerCall=allocated/(double)(samples*batch),maxWindowMs=times[^1],windowsAtLeast30ms=times.Count(t=>t>=30),
            gc=collections.Select((count,i)=>GC.CollectionCount(i)-count).ToArray()};
    }
    private static object ResolveAndRegister(int count)
    {
        var r=Registry(); using var p=Provider(r,"provider"); if(count>0) Fill(p);
        var catalogue=Catalogue(); var image=r.Publish(catalogue); var resolver=FoodProductMetadataResolver.Empty.WithApi(image);
        string name=Names[^1]; var replace=new FoodProductMetadataInput {TasteBand=0}; int changes=0;
        var result=new {count,resolve=Measure(()=>sink+=resolver.Resolve(name,Traits,957,"pc:1").TasteBand,1001,100),
            cleanPublication=Measure(()=>sink+=r.Publish(catalogue).Count,1001,100),
            idempotent=Measure(()=>{p.TryRegisterProductMetadata(count==0 ? Names[0] : name,Input,1,out _);sink+=r.Publish(catalogue).Count;},501,1),
            replaceAndPublish=Measure(()=>{replace.TasteBand=(changes++%2)*4;p.TryRegisterProductMetadata(name,replace,1,out _);sink+=r.Publish(catalogue).Count;})};
        return result;
    }
    private static object Burst(int width)
    {
        var r=Registry(); var catalogue=Catalogue();
        return new {width,registerUnregisterPublish=Measure(()=> {
            var p=Provider(r,"burst"); foreach(string n in Names.Take(width)) p.TryRegisterProductMetadata(n,Input,1,out _);
            sink+=r.Publish(catalogue).Count;p.Dispose();sink+=r.Publish(catalogue).Count;
        })};
    }
    private static object Conflict()
    {
        var r=Registry(); using var first=Provider(r,"first"); Fill(first); var catalogue=Catalogue();r.Publish(catalogue);
        return Measure(()=>{var p=Provider(r,"conflict");Fill(p);sink+=r.Publish(catalogue).Count;p.Dispose();sink+=r.Publish(catalogue).Count;});
    }
    private static object MeasurePrepared(Action prepare,Action action,Action cleanup)
    {
        for(int i=0;i<100;i++) {prepare();action();cleanup();}
        const int samples=501;
        var times=new double[samples];long allocated=0;
        int[] gc={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        for(int i=0;i<samples;i++)
        {
            prepare();long bytes=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            action();times[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated+=GC.GetAllocatedBytesForCurrentThread()-bytes;cleanup();
        }
        Array.Sort(times);
        return new {samples,batch=1,p50=times[samples/2],p95=times[(int)(samples*.95)],max=times[^1],
            bytesPerCall=allocated/(double)samples,windowsAtLeast30ms=times.Count(t=>t>=30),
            gcIncludingSetup=gc.Select((n,i)=>GC.CollectionCount(i)-n).ToArray()};
    }
    private static object IndividualMutations(int width)
    {
        FoodProductMetadataRegistry r=null!;FoodProductMetadataProvider p=null!;
        var catalogue=Catalogue();
        void Prepare(bool fill) {r=Registry();p=Provider(r,"individual");if(fill) Fill(p);}
        return new {width,
            register=MeasurePrepared(()=>Prepare(false),()=>{foreach(string n in Names.Take(width)) p.TryRegisterProductMetadata(n,Input,1,out _);},()=>r.ClearOnUnload()),
            unregister=MeasurePrepared(()=>Prepare(width>0),()=>p.Dispose(),()=>r.ClearOnUnload()),
            conflictRegister=MeasurePrepared(()=>{Prepare(width>0);p=Provider(r,"second");},()=>{
                foreach(string n in Names.Take(width)) p.TryRegisterProductMetadata(n,Input,1,out _);sink+=r.Publish(catalogue).Count;
            },()=>r.ClearOnUnload())};
    }
    private static object MaximumBudget()
    {
        var r=Registry(); var catalogue=Catalogue(); var held=new List<FoodProductMetadataProvider>();
        bool full=false;
        for(int i=0;i<256 && !full;i++)
        {
            if(!r.TryCreateProvider("provider:"+i,out var p,out _)) break;
            held.Add(p);
            foreach(string n in Names)
                if(!p.TryRegisterProductMetadata(n,Input,1,out var reason) && reason=="registry-budget") {full=true;break;}
        }
        if(!full) throw new Exception("Budget fixture did not reach its bound.");
        var image=r.Publish(catalogue); var resolver=FoodProductMetadataResolver.Empty.WithApi(image);
        var marker=held[0]; var edited=new FoodProductMetadataInput {TasteBand=0}; int change=0;
        return new {providers=r.ProviderCount,products=r.ProductCount,characters=r.BudgetCharacters,utf8=r.BudgetUtf8Bytes,
            resolve=Measure(()=>sink+=resolver.Resolve(Names[0],Traits,957,"pc:1").TasteBand,1001,100),
            boundedPublication=Measure(()=>{edited.TasteBand=(change++%2)*4;marker.TryRegisterProductMetadata(Names[0],edited,1,out _);sink+=r.Publish(catalogue).Count;}),
            cleanPublication=Measure(()=>sink+=r.Publish(catalogue).Count,1001,100)};
    }
    private static object MaximumProviderCount()
    {
        var r=Registry();var catalogue=Catalogue();var providers=new FoodProductMetadataProvider[256];
        void RegisterAll()
        {
            for(int i=0;i<256;i++)
            { providers[i]=Provider(r,Names[i]);if(!providers[i].TryRegisterProductMetadata(Names[i],Input,1,out var reason)) throw new Exception(reason); }
        }
        RegisterAll();var resolver=FoodProductMetadataResolver.Empty.WithApi(r.Publish(catalogue));
        var edited=new FoodProductMetadataInput {TasteBand=0};int changes=0;
        return new {providers=r.ProviderCount,products=r.ProductCount,characters=r.BudgetCharacters,
            resolve=Measure(()=>sink+=resolver.Resolve(Names[0],Traits,957,"pc:1").TasteBand,1001,100),
            publication=Measure(()=>{edited.TasteBand=(changes++%2)*4;providers[0].TryRegisterProductMetadata(Names[0],edited,1,out _);sink+=r.Publish(catalogue).Count;}),
            allProviderUnregisterRegisterBurst=Measure(()=>{foreach(var p in providers) p.Dispose();sink+=r.Publish(catalogue).Count;RegisterAll();sink+=r.Publish(catalogue).Count;})};
    }
    private static object BeforeAfterConflictWalk()
    {
        var before=new Before.FoodProductMetadataRegistry();before.BindPublicationThread(); var after=Registry();
        var catalogue=Catalogue(); FoodProductMetadataProvider? a=null; Before.FoodProductMetadataProvider? b=null;
        for(int i=0;i<12;i++)
        {
            var ap=Provider(after,"p:"+i); before.TryCreateProvider("p:"+i,out var bp,out _);
            Fill(ap);foreach(string n in Names) bp.TryRegisterProductMetadata(n,BeforeInput,1,out _);
            a??=ap;b??=bp;
        }
        var edited=new FoodProductMetadataInput {TasteBand=0}; var oldEdited=new Before.FoodProductMetadataInput {TasteBand=0};int i1=0,i2=0;
        return new {claims=12*256,
            before=Measure(()=>{oldEdited.TasteBand=(i1++%2)*4;b!.TryRegisterProductMetadata(Names[0],oldEdited,1,out _);sink+=before.Publish(catalogue).Count;}),
            after=Measure(()=>{edited.TasteBand=(i2++%2)*4;a!.TryRegisterProductMetadata(Names[0],edited,1,out _);sink+=after.Publish(catalogue).Count;})};
    }
    private static object BeforeAfterEmptyProvider()
    {
        var before=new Before.FoodProductMetadataRegistry();before.BindPublicationThread();var after=Registry();var catalogue=Catalogue();
        before.Publish(catalogue);after.Publish(catalogue);
        return new {
            before=Measure(()=>{before.TryCreateProvider("empty",out var p,out _);sink+=before.Publish(catalogue).Count;p.Dispose();sink+=before.Publish(catalogue).Count;}),
            after=Measure(()=>{var p=Provider(after,"empty");sink+=after.Publish(catalogue).Count;p.Dispose();sink+=after.Publish(catalogue).Count;})};
    }
    private static object Retained(int count,bool maximum)
    {
        // Estimate managed retained registry + immutable image; excludes shared input names/catalogue.
        var catalogue=Catalogue();long start=GC.GetTotalMemory(true); var r=Registry();
        if(count>0)
        {
            bool full=false;
            for(int i=0;i<(maximum?256:1) && !full;i++)
            {
                if(!r.TryCreateProvider("retained:"+i,out var p,out _)) break;
                foreach(string n in Names)
                    if(!p.TryRegisterProductMetadata(n,Input,1,out var reason) && reason=="registry-budget") {full=true;break;}
            }
        }
        var image=r.Publish(catalogue); long bytes=GC.GetTotalMemory(true)-start;
        GC.KeepAlive(r);GC.KeepAlive(image);GC.KeepAlive(catalogue);
        return new {count,maximum,bytes,providers=r.ProviderCount,budgetCharacters=r.BudgetCharacters};
    }
    private static object LoadedNames(int count)
    {
        string[] names=Enumerable.Range(0,count).Select(i=>"loaded:product:"+i).ToArray();
        var timing=Measure(()=>sink+=new FoodProductMetadataLoadedNames(names).Count,101,1);
        long start=GC.GetTotalMemory(true);var image=new FoodProductMetadataLoadedNames(names);
        long retained=GC.GetTotalMemory(true)-start;GC.KeepAlive(image);GC.KeepAlive(names);
        return new {count,copyAtCataloguePublication=timing,retainedBytesEstimate=retained};
    }
    public static void Main()
    {
        long start=Stopwatch.GetTimestamp(),bytes=GC.GetAllocatedBytesForCurrentThread();
        var cold=Registry();using(var p=Provider(cold,"cold")) {Fill(p);sink+=cold.Publish(Catalogue()).Count;}
        var firstUse=new {ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds,bytes=GC.GetAllocatedBytesForCurrentThread()-bytes};
        Console.WriteLine(JsonSerializer.Serialize(new {nativeEvidence=false,units="ms/call; windows measured separately for >=30ms flags",firstUse,
            emptyAndFull=new[]{ResolveAndRegister(0),ResolveAndRegister(256)},bursts=new[]{Burst(0),Burst(1),Burst(256)},
            individualMutations=new[]{IndividualMutations(0),IndividualMutations(256)},
            conflict=Conflict(),maximumBudget=MaximumBudget(),maximumProviderCount=MaximumProviderCount(),
            allRefusedWin=BeforeAfterConflictWalk(),emptyProviderWin=BeforeAfterEmptyProvider(),
            retained=new[]{Retained(0,false),Retained(256,false),Retained(256,true)},
            loadedNameImages=new[]{LoadedNames(0),LoadedNames(256),LoadedNames(16384)},
            nativeSetupCalls=0,routeCalls=0,providerCallbacks=0,frameParticipant=false,sink},new JsonSerializerOptions {WriteIndented=true}));
    }
}
