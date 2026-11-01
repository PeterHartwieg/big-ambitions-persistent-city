using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    private static object CapabilityProbeBench(string beforePath, string fixture)
    {
        beforePath = System.IO.Path.GetFullPath(beforePath);
        var assembly = new BaselineContext(beforePath).LoadFromAssemblyPath(beforePath);
        var previous = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), assembly.GetType("PersistentCity.Persistence.SaveCodec")!.GetMethod("RequireExecutableCapabilities")!);
        var state = FoodCity(fixture == "reference" ? 957 : 50000, true, fixture == "reference" ? 2 : 3);
        PopulateDecisions(state);
        if (fixture == "reference") RetainVisits(state, 32);
        FullMemoryAndTable(state);
        string headersFirst = SaveCodec.Write(state);
        // Build the old root order without a full-payload DOM. Only move the two root headers;
        // every byte within the population graph remains unchanged.
        const string prefix = "{\"Schema\":18,";
        if (!headersFirst.StartsWith(prefix, StringComparison.Ordinal)) throw new Exception("Missing header-first writer.");
        string oldOrder = "{" + headersFirst.Substring(prefix.Length).Replace(",\"Seed\":", ",\"Schema\":18,\"Seed\":");
        if (oldOrder.Length != headersFirst.Length || oldOrder == headersFirst) throw new Exception("Old order fixture failed.");
        var results = new List<object>();
        int samples = fixture == "reference" ? 7 : 3;
        foreach (var entry in new[] { (Order: "previous_ordinal", Source: oldOrder), (Order: "headers_first", Source: headersFirst) })
        {
            var actions = new Action[] { () => previous(entry.Source), () => SaveCodec.RequireExecutableCapabilities(entry.Source) };
            var times = actions.Select(_ => new List<double>()).ToArray();
            var allocations = actions.Select(_ => new List<long>()).ToArray();
            var cold = new double[2];
            for (int round = -1; round < samples; round++)
                for (int j = 0; j < 2; j++)
                {
                    int i = (round & 1) == 0 ? j : 1 - j;
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); actions[i]();
                    double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    if (round < 0) cold[i] = elapsed;
                    else { times[i].Add(elapsed); allocations[i].Add(GC.GetAllocatedBytesForCurrentThread() - allocated); }
                }
            for (int i = 0; i < 2; i++) results.Add(new { fixture, residents = state.Citizens.Count, consumers = state.Food!.Residents.Count,
                chars = entry.Source.Length, order = entry.Order, probe = i == 0 ? "before" : "after", samples,
                firstCallMs = cold[i], medianMs = times[i].OrderBy(t => t).ElementAt(samples / 2),
                p95Ms = times[i].OrderBy(t => t).ElementAt((int)(samples * .95)), maxMs = times[i].Max(),
                samplesAtLeast30Ms = times[i].Count(t => t >= 30), firstCallAtLeast30Ms = cold[i] >= 30,
                allocatedBytesMedian = allocations[i].OrderBy(n => n).ElementAt(samples / 2) });
        }
        // Whole-graph validation is intentionally outside the early probe and must remain mandatory.
        var loaded = SaveCodec.Read(oldOrder);
        if (loaded.Status != SaveReadStatus.Loaded || SaveCodec.Write(loaded.State!) != headersFirst) throw new Exception("Root order changed saved facts.");
        object FirstInvocation(string path, string probe)
        {
            // Separate class/JSON caches so the fast-path first invocation does not inherit the
            // earlier ordinal-fallback warmup. Assembly loading/delegate binding are outside timing.
            var fresh = new BaselineContext(path).LoadFromAssemblyPath(path);
            var action = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), fresh.GetType("PersistentCity.Persistence.SaveCodec")!.GetMethod("RequireExecutableCapabilities")!);
            long start = Stopwatch.GetTimestamp(); action(headersFirst);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return new { probe, order = "headers_first", ms, atLeast30Ms = ms >= 30 };
        }
        return new { fixture, beforeAssembly = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(beforePath))),
            sourceFactsAndSizeUnchanged = true, byteSizeUnchanged = System.Text.Encoding.UTF8.GetByteCount(oldOrder) == System.Text.Encoding.UTF8.GetByteCount(headersFirst),
            boundary = "offline bootstrap probe only; alternating before/after, first call per order plus measured samples; fresh-context first invocation measured separately; no Unity/game timing",
            freshContextFirstInvocations = new[] { FirstInvocation(beforePath, "before"), FirstInvocation(typeof(SaveCodec).Assembly.Location, "after") }, results };
    }
}
