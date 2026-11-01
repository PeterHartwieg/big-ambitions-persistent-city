using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static class Program
{
    private static WeatherCommitment? sink;
    private static uint hash;
    private static int admitted;
    private static object Measure(Action action,int boundaries,int repeats=100)
    {
        for(int i=0;i<20;i++) action();
        var samples=new double[repeats];long bytes=0;
        for(int i=0;i<repeats;i++)
        {
            long allocated=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();action();
            samples[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;bytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
        }
        Array.Sort(samples);
        return new {boundaries,repeats,p50Ms=samples[repeats/2],p95Ms=samples[(int)(repeats*.95)],maxMs=samples[^1],
            nsPerBoundary=samples[repeats/2]*1e6/boundaries,bytesPerBoundary=bytes/(double)(repeats*boundaries),sweepsAtLeast30Ms=samples.Count(x=>x>=30)};
    }
    static void Main()
    {
        var sizes=new[] {957,5000,50000}.Select(count=>
        {
            var keys=Enumerable.Range(0,count).Select(i=>"resident:"+i+":optional:Shop:2540").ToArray();
            long frame=0;int reads=0;
            var source=new FrameWeatherSource(()=>frame,()=> {reads++;return true;});
            var capture=Measure(()=> {frame++;for(int i=0;i<count;i++) sink=new WeatherCommitment(2500,WeatherPolicy.Sample(source),true);},count);
            int cachedReads=reads;reads=0;
            var held=new FoodDecisionState();
            var foodCapture=Measure(()=> {frame++;for(int i=0;i<count;i++) {held.Weather=WeatherPolicy.Sample(source);held.WeatherTravelMultiplier=held.Weather==WeatherSample.Rain ? 1.3 : 1;}},count);
            int cachedFoodReads=reads;
            foreach(string key in keys) if(StableRandom.Hash(key+":weather-admission",77)!=WeatherPolicy.AdmissionDraw(key,77)) throw new Exception("Changed draw key");
            object Admission(WeatherSample weather,bool prototype)=>Measure(()=>
            {
                for(int i=0;i<count;i++)
                {
                    uint draw=prototype ? StableRandom.Hash(keys[i]+":weather-admission",77)
                        : weather==WeatherSample.Rain ? WeatherPolicy.AdmissionDraw(keys[i],77) : 0;
                    if(WeatherPolicy.Admit(draw,weather,.7)) admitted++;
                }
            },count);
            return new {count,capture,foodCapture,cachedSourceReads=cachedReads,cachedFoodSourceReads=cachedFoodReads,uncachedSourceReads=count*120,
                dry=Admission(WeatherSample.Dry,false),rain=Admission(WeatherSample.Rain,false),rainConcatenationPrototype=Admission(WeatherSample.Rain,true),
                residentWeatherMaxInclusiveBytes=22,optionalBytesHeadroom=2,consumerWeatherDeltaBytes=2,consumerBytesHeadroom=6};
        }).ToArray();
        string memory="1-------00000001G100100001G100200001G100300001G1|2";
        // Four sorted complete B0 memories, checked by both old and new shape paths.
        MemoryWire.CheckWire(memory[..^2]);SaveCodec.CheckMemory20(memory);
        var oldPrecheck=Measure(()=> {for(int i=0;i<5000;i++) MemoryWire.CheckWire(memory[..^2]);},5000);
        var newPrecheck=Measure(()=> {for(int i=0;i<5000;i++) SaveCodec.CheckMemory20(memory);},5000);
        var sample=new WeatherCommitment(2500,WeatherSample.Rain,true);hash=StableRandom.Hash(sample.ToWire(),77);
        Console.WriteLine(JsonConvert.SerializeObject(new {environment=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            protocol="20 warmups, 100 measured sweeps; policy/capture only, no routes/native work; frame increment per capture sweep; timers/arrays outside allocation counters",
            sizes,oldPrecheck,newPrecheck,sink=sink?.ToWire(),hash,admitted}));
    }
}
