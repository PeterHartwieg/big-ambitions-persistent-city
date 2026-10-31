using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

internal static class Program
{
    private static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
    private sealed class NeverNative : IRoutePlanner, IFoodMarket
    {
        internal int Calls;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double at) { Calls++; throw new Exception("Native read"); }
        public FoodSale Buy(ResidentMeal meal,double at) { Calls++; throw new Exception("Native write"); }
        public Route? Plan(Place a,Place b,double at,TravelPreferences p) { Calls++; throw new Exception("Route"); }
    }
    private static object Proof()
    {
        var state=EconomyScenario.Create(40,11); var trap=new NeverNative(); var sim=new CitySimulator(state,trap);
        // No AttachFoodMarket: that would initialize food ownership, outside research's pure input boundary.
        string before=SaveCodec.Write(state);
        var fixture=new ResearchFixture(40);
        var report=ResearchFixture.Finish(fixture,16);
        Require(report.FixtureCells[0].Value==40 && report.FixtureCells[1].Value==26,"Manual population counts");
        Require(report.FixtureCells[2].Value==20 && report.FixtureCells[3].Value==20 && report.FixtureCells[4].Value==20,"Manual meals");
        Require(report.FixtureCells[62].Value==null && report.FixtureCells[63].Value==null,"Unavailable siblings");
        Require(!report.TryRead(NoPaymentPath.Instance,"fixture",out var denied) && denied==null,"No free stats");
        var expected=report.FixtureCells.Select(c=>c.Value).ToArray();
        foreach(int size in new[]{16,256,1024})
        {
            var src=new ResearchFixture(2501); var job=new MarketResearchEngine(src,new MarketResearchScope(),MarketResearchTier.InDepth);
            job.Continue(src,size); Require(job.Completed==null,"Partial publication");
            var saved=JsonConvert.DeserializeObject<MarketResearchCursor>(JsonConvert.SerializeObject(job.Checkpoint()))!;
            job=new MarketResearchEngine(src,new MarketResearchScope(),MarketResearchTier.InDepth,saved);
            while(job.Completed==null) job.Continue(src,size);
            Require(src.Reads==2501,"Duplicate consumer");
            Require(job.Completed.FixtureCells.Select(c=>c.Value).SequenceEqual(ResearchFixture.Finish(new ResearchFixture(2501)).FixtureCells.Select(c=>c.Value)),"Paging/restart mismatch");
        }
        Require(SaveCodec.Write(state)==before,"Research changed saved bytes"); Require(trap.Calls==0,"Native call");
        GC.KeepAlive(sim); GC.KeepAlive(expected);
        return new { schema=CityState.CurrentSchema, savedBytes=Encoding.UTF8.GetByteCount(before),
            savedSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(before))), savedByteDelta=0,nativeCalls=trap.Calls,routeRequests=trap.Calls };
    }
    private static double Milliseconds(long ticks) => ticks*1000.0/Stopwatch.Frequency;
    private static object TraitValidationWin()
    {
        const int iterations=100000;
        var cases=new[]{"0123401234","2222222222","4444444444","012340123", "0123401235"};
        foreach(string s in cases) Require(new ResidentTraits { Bands=s }.IsValid==MarketResearchConsumer.ValidTraits(s),"Validation parity");
        for(int i=0;i<10000;i++) { _=new ResidentTraits { Bands=cases[i%5] }.IsValid; _=MarketResearchConsumer.ValidTraits(cases[i%5]); }
        var before=new List<double>(); var after=new List<double>(); long beforeBytes=0,afterBytes=0; int accepted=0;
        void Sample(bool old)
        {
            long alloc=GC.GetAllocatedBytesForCurrentThread(), start=Stopwatch.GetTimestamp();
            for(int i=0;i<iterations;i++) if(old ? new ResidentTraits { Bands=cases[i%5] }.IsValid : MarketResearchConsumer.ValidTraits(cases[i%5])) accepted++;
            long ticks=Stopwatch.GetTimestamp()-start, bytes=GC.GetAllocatedBytesForCurrentThread()-alloc;
            if(old) { before.Add(Milliseconds(ticks)*1000000/iterations); beforeBytes=bytes/iterations; }
            else { after.Add(Milliseconds(ticks)*1000000/iterations); afterBytes=bytes/iterations; }
        }
        for(int round=0;round<10;round++) { Sample(round%2==0); Sample(round%2!=0); }
        before.Sort(); after.Sort(); Require(accepted==1200000,"Validation result");
        return new { iterationsPerSample=iterations,samplesPerImplementation=10,
            prototypeObjectValidationBytesPerRow=beforeBytes,finalDirectValidationBytesPerRow=afterBytes,
            prototypeMedianNs=before[5],finalMedianNs=after[5] };
    }
    private static object Bench(int count)
    {
        long heapBefore=GC.GetTotalMemory(true); var source=new ResearchFixture(count);
        long inputRetained=Math.Max(0,GC.GetTotalMemory(true)-heapBefore);
        // Warm JIT/paths separately. Frozen input construction is not continuation work.
        for(int i=0;i<10;i++) ResearchFixture.Finish(source);
        var units=new List<double>(30*((count+255)/256)); var totals=new List<double>(30); var allocations=new List<long>(30);
        var requests=new List<double>(30);
        long unitAlloc=0, completionAlloc=0; int spikes=0;
        for(int run=0;run<30;run++)
        {
            long requestStart=Stopwatch.GetTimestamp();
            var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
            requests.Add(Milliseconds(Stopwatch.GetTimestamp()-requestStart));
            long totalStart=Stopwatch.GetTimestamp(), allocationStart=GC.GetAllocatedBytesForCurrentThread();
            while(job.Completed==null)
            {
                long alloc=GC.GetAllocatedBytesForCurrentThread(), start=Stopwatch.GetTimestamp();
                job.Continue(source,256);
                long elapsed=Stopwatch.GetTimestamp()-start, bytes=GC.GetAllocatedBytesForCurrentThread()-alloc;
                if(job.Completed==null) unitAlloc=Math.Max(unitAlloc,bytes); else completionAlloc=Math.Max(completionAlloc,bytes);
                double ms=Milliseconds(elapsed); units.Add(ms); if(ms>=30) spikes++;
            }
            // Includes instrumentation above; separate stage samples retained for audit.
            allocations.Add(GC.GetAllocatedBytesForCurrentThread()-allocationStart); totals.Add(Milliseconds(Stopwatch.GetTimestamp()-totalStart));
        }
        units.Sort(); totals.Sort(); requests.Sort();
        long allocStart=GC.GetAllocatedBytesForCurrentThread(); var retainedJob=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
        long jobAllocation=GC.GetAllocatedBytesForCurrentThread()-allocStart;
        var held = new MarketResearchEngine[1000]; var scope=new MarketResearchScope();
        long retainedBefore=GC.GetTotalMemory(true);
        for(int i=0;i<held.Length;i++) held[i]=new MarketResearchEngine(source,scope,MarketResearchTier.InDepth);
        double engineRetained=(GC.GetTotalMemory(true)-retainedBefore)/(double)held.Length;
        int reads=source.Reads; long idleStart=GC.GetAllocatedBytesForCurrentThread();
        // Closed/no request means no caller/hook. Holding a dormant source performs no work.
        for(int i=0;i<100000;i++) GC.KeepAlive(source);
        long idleAlloc=GC.GetAllocatedBytesForCurrentThread()-idleStart;
        Require(source.Reads==reads && idleAlloc==0,"Idle work");
        GC.KeepAlive(retainedJob); GC.KeepAlive(held);
        return new { consumers=count,unitSize=256,runs=30,unitSamples=units.Count,
            requestConstructionMs=new { p50=requests[15],p95=requests[28],max=requests[^1] },
            unitMs=new { p50=units[units.Count/2],p95=units[(int)(units.Count*.95)],max=units[^1],spikesAtLeast30ms=spikes },
            completionMs=new { p50=totals[15],p95=totals[28],max=totals[^1] },
            nonfinalUnitAllocatedBytes=unitAlloc,finalUnitAllocatedBytes=completionAlloc,
            completionAllocatedBytesMedian=allocations.OrderBy(v=>v).ElementAt(15),
            engineConstructionAllocatedBytes=jobAllocation,engineCounterPayloadBytes=64*sizeof(int),
            engineRetainedBytesMean=engineRetained,
            borrowedFrozenFixtureRetainedBytes=inputRetained,idleReads=source.Reads-reads,idleAllocatedBytes=idleAlloc,routeRequests=0 };
    }
    private static void SourceGuard(string root)
    {
        // Only configuration wiring may name research in Runtime. No UI/bridge/Update reader or caller.
        foreach(string file in Directory.GetFiles(Path.Combine(root,"Runtime"),"*.cs"))
        {
            var hits=File.ReadAllLines(file).Where(l=>l.Contains("MarketResearch",StringComparison.Ordinal)).ToArray();
            if(Path.GetFileName(file)=="PrototypeSettings.cs") Require(hits.Length==3,"Unexpected settings consumer");
            else if(Path.GetFileName(file)=="PersistentCityMod.cs") Require(hits.Length==1 && hits[0].Contains(".Clamp("),"Unexpected runtime caller");
            else Require(hits.Length==0,"Research exposed by runtime: "+Path.GetFileName(file));
        }
        foreach(string file in Directory.GetFiles(Path.Combine(root,"Persistence"),"*.cs"))
            Require(!File.ReadAllText(file).Contains("MarketResearch",StringComparison.Ordinal),"Research saved state");
        foreach(string file in Directory.GetFiles(Path.Combine(root,"Core"),"MarketResearch*.cs"))
        {
            string text=File.ReadAllText(file);
            Require(!text.Contains("IRoutePlanner") && !text.Contains("IFoodMarket") && !text.Contains("UnityEngine"),"Native dependency");
        }
    }
    public static void Main(string[] args)
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../")); SourceGuard(root);
        var result=new { runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,configuration="Release",fixture="seed-independent frozen ordinal rows, logical day 3",
            proof=Proof(),traitValidationWin=TraitValidationWin(),benchmarks=new[]{Bench(957),Bench(5000),Bench(50000)} };
        string json=JsonConvert.SerializeObject(result,Formatting.Indented);
        if(args.Length>0) File.WriteAllText(args[0],json+Environment.NewLine);
        Console.WriteLine(json);
    }
}
