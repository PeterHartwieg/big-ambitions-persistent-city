using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

static object Measure(Action action,int runs=301)
{
    for(int i=0;i<30;i++) action();
    var samples=new double[runs];long allocation=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<runs;i++) {long start=Stopwatch.GetTimestamp();action();samples[i]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
    allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;Array.Sort(samples);
    return new {p50Ms=samples[runs/2],p95Ms=samples[(int)(runs*.95)],maxMs=samples[^1],spikesAtLeast30Ms=samples.Count(t=>t>=30),allocatedBytesPerOperation=allocation/runs};
}
static object ReferenceLookup(int count,CampaignSnapshot observation,Dictionary<string,string> places)
{
    var duplicateMs=new double[101];var sharedMs=new double[101];long duplicateBytes=0,sharedBytes=0;
    // Reflection selects the production-owned store once per fixture, outside both measured lookups.
    var field=typeof(FoodCampaignSource).GetField("references",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
    for(int iteration=-10;iteration<101;iteration++)
    {
        var food=new FoodCommerceState();var source=new FoodCampaignSource(food,79,new(),FoodCampaignTargetPolicy.Unknown);
        var duplicate=new FoodMemoryStore(food);var shared=(FoodMemoryStore)field.GetValue(source)!;
        duplicate.Reference(-1);shared.Reference(-1);
        source.Publish(observation,places);
        long allocated=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
        GC.KeepAlive(duplicate.Reference(count-1));
        double before=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long beforeBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        allocated=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();
        GC.KeepAlive(shared.Reference(count-1));
        double after=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long afterBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        if(iteration<0) continue;
        duplicateMs[iteration]=before;sharedMs[iteration]=after;duplicateBytes+=beforeBytes;sharedBytes+=afterBytes;
    }
    object Stats(double[] values,long bytes)
    {
        Array.Sort(values);return new {p50Ms=values[50],p95Ms=values[95],maxMs=values[^1],allocatedBytesPerLookup=bytes/101,spikesAtLeast30Ms=values.Count(v=>v>=30)};
    }
    if(sharedBytes!=0) throw new Exception("Shared index deferred allocation to the next consumer lookup.");
    return new {duplicateIndexControl=Stats(duplicateMs,duplicateBytes),sharedProductionIndex=Stats(sharedMs,sharedBytes)};
}
var targets=new FoodCampaignTargetPolicy(Enumerable.Repeat(FoodCampaignGroup.Commuters|FoodCampaignGroup.HealthOriented,6).ToArray());
var probe=new NativeCampaignReader();
if(probe.HasCampaignEntries(null) || probe.HasCampaignEntries(new object())) throw new Exception("Unknown campaign presence admitted.");
var probeRegistration=new BuildingRegistration();
if(!probe.HasCampaignEntries(probeRegistration)) throw new Exception("Verified list presence missed.");
probeRegistration.marketingCampaigns.Clear();
if(probe.HasCampaignEntries(probeRegistration)) throw new Exception("Empty list presence admitted.");
var traits=new ResidentTraits {Bands="2222222322"};
var settings=new FoodCampaignSettings {ReachProbability=1};
foreach(int count in new[]{1,1024})
{
    var native=Enumerable.Range(0,count).Select(i=>new BuildingRegistration {Id="b"+i.ToString("D4")}).ToArray();
    var sources=native.Select(n=>new CampaignBusinessSource(n.Id,n)).ToArray();var reader=new NativeCampaignReader();
    var places=native.ToDictionary(n=>n.Id,n=>"place:"+n.Id);
    var food=new FoodCommerceState();var source=new FoodCampaignSource(food,79,settings,targets);
    int hour=0;
    CampaignSnapshot Read()
    {
        var refresh=reader.Begin(sources,hour*60,hour);
        while(refresh.Snapshot==null) refresh.Step(16,hour*60,hour);
        return refresh.Snapshot;
    }
    // Eight-day history loaded before the measured same-day hourly replacement series.
    for(hour=0;hour<=7*24;hour++) source.Publish(Read(),places);
    var sample=Measure(()=>{hour++;source.Publish(Read(),places);});
    var referenceLookup=ReferenceLookup(count,Read(),places);
    int day=hour/24;
    var rebuild=Measure(source.Rebuild);
    int consumer=0;
    var decision=Measure(()=>{
        source.Prepare("resident:stable",day,hour*60,traits,true);
        for(int slot=0;slot<8;slot++) GC.KeepAlive(source.Reference("resident:stable",day,slot));
        consumer++;
    },10001);
    // Actual production scheduler, including scope-vector construction and C1 publication; targets remain Unknown.
    var runtimeFood=new FoodCommerceState();var adapter=new NativeFoodMarket(runtimeFood,native);
    NativeFoodOwnership.Hour=0;adapter.AttachCampaignAwareness(79,new());long runtimeHour=0;
    string before=JsonSerializer.Serialize(native,new JsonSerializerOptions {IncludeFields=true});
    var production=Measure(()=>adapter.ObserveCampaignBoundary(++runtimeHour));
    string after=JsonSerializer.Serialize(native,new JsonSerializerOptions {IncludeFields=true});
    if(before!=after) throw new Exception("Native campaign/account state changed.");
    foreach(var registration in native) registration.marketingCampaigns.Clear();
    // Allow all eight retained days to expire before absent-path measurements.
    adapter.ObserveCampaignBoundary(runtimeHour+=8*24);
    if(runtimeFood.Campaigns.Count!=0) throw new Exception("Stopped evidence did not expire.");
    var absent=Measure(()=>adapter.ObserveCampaignBoundary(++runtimeHour),10001);
    var disabledFood=new FoodCommerceState();var disabled=new NativeFoodMarket(disabledFood,native);
    disabled.AttachCampaignAwareness(79,new() {Enabled=false});
    var off=Measure(()=>disabled.ObserveCampaignBoundary(++runtimeHour),10001);
    var city=EconomyScenario.Create(1,79);city.Food=EconomyScenario.Food();new CitySimulator(city,new FixturePlanner()).AttachFoodMarket(new FixtureMarket());var baseline=JObject.Parse(SaveCodec.Write(city));baseline["Schema"]=19;((JObject)baseline["Food"]!).Remove("Campaigns");
    // Shared reference table is separately charged; masks occupy their reserved campaign allocation.
    var previous=SaveCodec.Read(baseline.ToString(Newtonsoft.Json.Formatting.None));
    if(previous.Status!=SaveReadStatus.Loaded) throw new Exception(previous.Error);
    var imageFood=previous.State!.Food!;imageFood.BusinessReferences.Clear();
    imageFood.BusinessReferences.AddRange(food.BusinessReferences.Select(r=>new BusinessReference {Slot=r.Slot,BusinessId=r.BusinessId,PlaceId="shop-1"}));
    imageFood.Campaigns.AddRange(food.Campaigns);
    string emptyWire=SaveCodec.Write(previous.State); // contains campaign references and records
    var imageCounts=SaveCodec.CountRecords(previous.State,emptyWire);
    imageFood.Campaigns.Clear();string noImages=SaveCodec.Write(previous.State);var baseCounts=SaveCodec.CountRecords(previous.State,noImages);
    int campaignBytes=emptyWire.Length-noImages.Length;
    if(imageCounts.Semantic-baseCounts.Semantic!=count || imageCounts.Streaming-baseCounts.Streaming!=count) throw new Exception("Image record charge changed.");
    Console.WriteLine(JsonSerializer.Serialize(new {businesses=count,syntheticReviewedHourlyC1AndPublication=sample,groupIndexRebuild=rebuild,eightSlotDecision=decision,
        productionUnknownTargetsHourly=production,noCampaignHourlyCountProbes=absent,disabledHourly=off,referenceLookupAfterPublication=referenceLookup,
        campaignBytesAbove19WithSharedReferences=campaignBytes+15,campaignRecordDelta=count,imageWireCharacters=265,
        nativeStateUnchanged=before==after,notes="Offline .NET only. No native timing/targeting evidence. Whole-frame unavailable; count probes are included, not claimed as zero CPU work."}));
}

sealed class FixturePlanner : IRoutePlanner
{
    public Route? Plan(Place from,Place to,double minute,TravelPreferences preferences)=>null;
}
sealed class FixtureMarket : IFoodMarket
{
    public double EarliestMinute=>0;
    public IReadOnlyList<FoodBusiness> Read(double minute)=>Array.Empty<FoodBusiness>();
    public FoodSale Buy(ResidentMeal meal,double minute)=>throw new Exception("No fixture checkout");
}
