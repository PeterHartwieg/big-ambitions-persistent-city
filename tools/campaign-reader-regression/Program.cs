using System.Diagnostics;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;

var runtime = new PopulationRuntime();
var nativeBefore = JsonSerializer.Serialize(SaveGameManager.Current, new JsonSerializerOptions { IncludeFields = true });
for (int i = 0; i < 20; i++)
    if (runtime.ReadCampaignSnapshot(new[] { 0 }).Businesses[0].Campaigns.Count != 6) throw new Exception("Bridge dump incomplete");
var nativeAfter = JsonSerializer.Serialize(SaveGameManager.Current, new JsonSerializerOptions { IncludeFields = true });
if (nativeBefore != nativeAfter) throw new Exception("Native inputs changed");
bool duplicateRefused = false;
try { runtime.ReadCampaignSnapshot(new[] { 0, 0 }); } catch (ArgumentException) { duplicateRefused = true; }
if (!duplicateRefused) throw new Exception("Duplicate identity accepted");
Console.WriteLine("Campaign bridge: 20 repeated detached reads unchanged; duplicate scope refused; native helper/money spies untouched.");

// F3: the runtime boundary admits exactly one atomic unit. Large on-demand diagnostics below
// still measure explicit refreshes; they do not justify a per-frame synchronous drain.
foreach (int count in new[] { 1, 64, 65, 1024 })
{
    var sources = Enumerable.Range(0, count).Select(i => new CampaignBusinessSource("boundary:" + i, new BuildingRegistration())).ToArray();
    var reader = new NativeCampaignReader();
    var times = new List<double>(); var bytes = new List<long>(); int refusals = 0;
    for (int i = 0; i < 302; i++)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        bool admitted = NativeCampaignReader.BoundaryScopeAdmitted(count);
        if (admitted)
        {
            var snapshot = reader.ReadBoundary(sources, 60, 1);
            if (snapshot.Businesses.Count != count) throw new Exception("Partial boundary image escaped");
        }
        else refusals++;
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (i != 0) { times.Add(elapsed); bytes.Add(allocated); }
    }
    times.Sort(); bytes.Sort();
    Console.WriteLine(JsonSerializer.Serialize(new { boundaryBusinesses = count, admittedWork = count <= 64 ? count : 0, refusals,
        p50Ms = times[150], p95Ms = times[285], maxMs = times[300], spikesAtLeast30Ms = times.Count(t => t >= 30),
        medianAllocatedBytes = bytes[150], maxAllocatedBytes = bytes[300],
        evidence = "Fields-only production reader with native doubles; oversized boundary refused before reads/allocation" }));
}

// Largest previously saved campaign history feeding one newly admitted 64-business boundary.
// Construction/load-time index rebuild stays outside the atomic boundary measurement.
{
    var times = new List<double>(); var bytes = new List<long>();
    for (int iteration = 0; iteration < 32; iteration++)
    {
        var food = new FoodCommerceState();
        for (int slot = 0; slot < 1024; slot++)
        {
            food.BusinessReferences.Add(new BusinessReference { Slot = slot, BusinessId = "b:" + slot, PlaceId = "p:" + slot });
            var masks = new uint[64];
            for (int age = 0; age < 8; age++) for (int type = 0; type < 6; type++) masks[age * 8 + type] = 0xffffff;
            food.Campaigns.Add(new FoodCampaignImage(slot, 191, masks));
        }
        var publisher = new FoodCampaignSource(food, 7, new FoodCampaignSettings());
        var places = Enumerable.Range(0, 64).ToDictionary(i => "b:" + i, i => "p:" + i);
        var sources = Enumerable.Range(0, 64).Select(i => new CampaignBusinessSource("b:" + i, new BuildingRegistration())).ToArray();
        var reader = new NativeCampaignReader(); reader.ReadBoundary(sources, 60, 1); // bind reflection before timing
        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        publisher.Publish(reader.ReadBoundary(sources, 192 * 60, 192), places);
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (food.Campaigns.Count != 64 || publisher.Publications != 1) throw new Exception("Atomic campaign publication incomplete");
        if (iteration != 0) { times.Add(elapsed); bytes.Add(allocated); }
    }
    times.Sort(); bytes.Sort();
    Console.WriteLine(JsonSerializer.Serialize(new { boundaryWithRetainedHistory = 1024, admittedBusinesses = 64,
        p50Ms = times[15], p95Ms = times[29], maxMs = times[30], spikesAtLeast30Ms = times.Count(t => t >= 30),
        medianAllocatedBytes = bytes[15], maxAllocatedBytes = bytes[30],
        evidence = "Production atomic reader + publication, 1024 full eight-day histories -> 64 admitted scope, native doubles" }));
}

foreach (int count in new[] { 1, 8, NativeCampaignReader.MaximumBusinesses })
{
    var sources = Enumerable.Range(0, count).Select(i => new CampaignBusinessSource("business:" + i, new BuildingRegistration())).ToArray();
    var reader = new NativeCampaignReader();
    CampaignSnapshot Run(NativeCampaignReader target)
    {
        var refresh = target.Begin(sources, 123, 1);
        while (refresh.Cursor < count) refresh.Step(64, 123, 1);
        return refresh.Snapshot ?? throw new Exception("No coherent image");
    }
    for (int i = 0; i < 30; i++) Run(reader);
    object Measure(bool cold)
    {
        var samples = new double[301];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            var snapshot = Run(cold ? new NativeCampaignReader() : reader);
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            GC.KeepAlive(snapshot);
        }
        allocated = (GC.GetAllocatedBytesForCurrentThread() - allocated) / samples.Length;
        Array.Sort(samples);
        return new { p50Ms = samples[150], p95Ms = samples[285], maxMs = samples[300], spikesAtLeast30Ms = samples.Count(x => x >= 30), allocatedBytes = allocated };
    }
    var coldResult = Measure(true);
    var warmResult = Measure(false);
    var continuation = new List<double>();
    long continuationBytes = 0;
    for (int iteration = 0; iteration < 101; iteration++)
    {
        var refresh = reader.Begin(sources, 123, 1);
        while (refresh.Cursor < count)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            refresh.Step(1, 123, 1);
            continuation.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            continuationBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }
    }
    continuation.Sort();
    // Average 32 simultaneously retained images to reduce GC accounting noise; sources/cache already live.
    var held = new CampaignSnapshot[32];
    long baseline = GC.GetTotalMemory(true);
    for (int i = 0; i < held.Length; i++) held[i] = Run(reader);
    long retained = Math.Max(0, GC.GetTotalMemory(true) - baseline) / held.Length;
    GC.KeepAlive(held); GC.KeepAlive(reader); GC.KeepAlive(sources);
    Console.WriteLine(JsonSerializer.Serialize(new { businesses = count, campaignsPerBusiness = 6, cold = coldResult, warm = warmResult,
        continuation = new { p50Ms = continuation[continuation.Count / 2], p95Ms = continuation[(int)(continuation.Count * .95)], maxMs = continuation[^1],
            spikesAtLeast30Ms = continuation.Count(x => x >= 30), allocatedBytesPerUnit = continuationBytes / continuation.Count }, retainedBytesPerSnapshot = retained }));
}
