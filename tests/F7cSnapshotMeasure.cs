using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// F7c snapshot split (docs/perf/f7c.md), after F7c only, not a gate: runs when PC_F7C_SNAPSHOT_MEASURE names an output
/// file. Times the three parts on their own, each after a warm-up: Capture (synchronize + copy), validation plus encode
/// from that snapshot (Encode), and the copy alone (the section copy without synchronize). Also the allocation of each
/// (the copy's allocation is the snapshot's own size) and how many routes it shared or copied.
/// </summary>
public sealed class F7cSnapshotMeasure
{
    [Fact]
    public void SnapshotSplit()
    {
        string? output = PerfGate.Variable("PC_F7C_SNAPSHOT_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F7C_SAMPLES"), out int n) ? n : 7;
        foreach (string fixture in F7cMeasure.Fixtures(Environment.GetEnvironmentVariable("PC_F7C_FIXTURES")))
            File.AppendAllText(output!, JsonConvert.SerializeObject(Measure(fixture, samples)) + Environment.NewLine);
    }

    private static object Measure(string fixture, int samples)
    {
        var state = F7cFixtures.Build(fixture).State;
        string payload = SaveCodec.Write(state);
        var capture = new List<double>(); var encode = new List<double>(); var copy = new List<double>();
        var captureBytes = new List<long>(); var encodeBytes = new List<long>(); var copyBytes = new List<long>();
        SaveSnapshot? last = null;
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), t = Stopwatch.GetTimestamp();
            var snapshot = SaveCodec.Capture(state);
            double captureMs = Stopwatch.GetElapsedTime(t).TotalMilliseconds; long captured = GC.GetAllocatedBytesForCurrentThread() - bytes;
            bytes = GC.GetAllocatedBytesForCurrentThread(); t = Stopwatch.GetTimestamp();
            string encoded = SaveCodec.Encode(snapshot);
            double encodeMs = Stopwatch.GetElapsedTime(t).TotalMilliseconds; long encodedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (encoded != payload) throw new InvalidOperationException("Snapshot bytes differ on " + fixture);
            bytes = GC.GetAllocatedBytesForCurrentThread(); t = Stopwatch.GetTimestamp();
            var copied = SaveSnapshot.Capture(state);
            double copyMs = Stopwatch.GetElapsedTime(t).TotalMilliseconds; long copiedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            GC.KeepAlive(copied);
            last = snapshot;
            if (i == 0) continue;
            capture.Add(captureMs); encode.Add(encodeMs); copy.Add(copyMs);
            captureBytes.Add(captured); encodeBytes.Add(encodedBytes); copyBytes.Add(copiedBytes);
        }
        return new
        {
            fixture, characters = payload.Length,
            captureMs = F7cMeasure.Stats(capture), encodeFromSnapshotMs = F7cMeasure.Stats(encode), copyOnlyMs = F7cMeasure.Stats(copy),
            captureAllocatedBytes = F7cMeasure.Median(captureBytes), encodeAllocatedBytes = F7cMeasure.Median(encodeBytes),
            copyOnlyAllocatedBytes = F7cMeasure.Median(copyBytes),
            sharedRoutes = last!.SharedRoutes, copiedRoutes = last.CopiedRoutes,
            captureShareOfEncode = Math.Round(Median(capture) / Median(encode), 3), samples,
            evidence = "macOS arm64 .NET 10 Release offline, one dotnet process; save timing is its own criterion, never ordinary-play mod-work",
        };
    }

    private static double Median(List<double> values) => values.OrderBy(v => v).ElementAt(values.Count / 2);
}
