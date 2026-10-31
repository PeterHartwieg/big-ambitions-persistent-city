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
