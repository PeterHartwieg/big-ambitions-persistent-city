using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using PersistentCity.Persistence;

static partial class Program
{
    private sealed class BaselineContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;
        internal BaselineContext(string path) : base(isCollectible:false) {resolver=new AssemblyDependencyResolver(path);}
        protected override Assembly? Load(AssemblyName name)
        {string? path=resolver.ResolveAssemblyToPath(name);return path==null ? null : LoadFromAssemblyPath(path);}
    }
    private static object InterleavedSaveBench(string baselinePath)
    {
        // One OS process, alternating order, equally warmed independent codec/type caches. The A2
        // assembly and its private Core/Persistence dependencies are loaded from an isolated build.
        baselinePath=Path.GetFullPath(baselinePath);
        var baseline=new BaselineContext(baselinePath);
        var assembly=baseline.LoadFromAssemblyPath(baselinePath);
        var create=assembly.GetType("Program",throwOnError:true)!.GetMethod("FoodCity",BindingFlags.NonPublic|BindingFlags.Static)!;
        // This harness compiles its selected Core/Persistence sources into the executable itself.
        var codec=assembly.GetType("PersistentCity.Persistence.SaveCodec",throwOnError:true)!;
        var write=codec.GetMethod("Write",BindingFlags.Public|BindingFlags.Static)!;
        var currentWrite=typeof(SaveCodec).GetMethod("Write",BindingFlags.Public|BindingFlags.Static)!;
        var results=new List<object>();
        foreach(int residents in new[]{957,5000})
        {
            object old=create.Invoke(null,new object[]{residents,true,2})!;
            var control=FoodCity(residents,true,2);var current=FoodCity(residents,true,2);PopulateDecisions(current);
            var writers=new Func<string>[] {()=> (string)write.Invoke(null,new[]{old})!,
                ()=> (string)currentWrite.Invoke(null,new object[]{control})!,()=> (string)currentWrite.Invoke(null,new object[]{current})!};
            var names=new[]{"A2","A3_same_payload","A3_saved_decisions"};
            var times=writers.Select(_=>new List<double>()).ToArray();var bytes=writers.Select(_=>new List<long>()).ToArray();
            // Run all modes in the warmup, then alternate A2/control/A3 and its reverse on every round.
            for(int round=-8;round<16;round++)
            {
                for(int item=0;item<3;item++)
                {
                    int i=(round&1)==0 ? item : 2-item;
                    long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
                    int length=writers[i]().Length;
                    double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                    if(length==0) throw new Exception("Empty save");
                    if(round>=0) {times[i].Add(elapsed);bytes[i].Add(allocated);}
                }
            }
            for(int i=0;i<3;i++)
            {
                times[i].Sort();bytes[i].Sort();
                results.Add(new {residents,mode=names[i],samples=times[i].Count,chars=writers[i]().Length,
                    writeMedianMs=times[i][times[i].Count/2],writeP95Ms=times[i][(int)(times[i].Count*.95)],
                    allocatedBytes=bytes[i][bytes[i].Count/2]});
            }
        }
        return new {environment="same .NET 10 process; independent noncollectible A2/current load contexts; identical reflection entry; eight warmups + sixteen alternating rounds",results};
    }
}
