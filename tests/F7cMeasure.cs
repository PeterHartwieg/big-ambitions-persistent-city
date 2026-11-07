using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// F7c before/after harness (docs/perf/f7c.md), not a gate: it runs only when PC_F7C_MEASURE names an output file, e.g.
/// <c>PC_F7C_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F7cMeasure</c>.
/// PC_F7C_FIXTURES and PC_F7C_SAMPLES narrow a run. It calls only API that exists before and after F7c, so the same file
/// measures the base commit and this branch: payload SHA-256 and sizes, SaveCodec.Write phase times (every phase the
/// recorder knows), allocation per write, the live city's retained bytes and the retained bytes after a strict read.
/// </summary>
public sealed class F7cMeasure
{
    internal static string[] Fixtures(string? only) => string.IsNullOrEmpty(only) ? F7cFixtures.All : only!.Split(',');

    [Fact]
    public void SaveCostBeforeAndAfter()
    {
        string? output = Environment.GetEnvironmentVariable("PC_F7C_MEASURE");
        if (string.IsNullOrEmpty(output)) return; // measurement only on request
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F7C_SAMPLES"), out int n) ? n : 7;
        foreach (string fixture in Fixtures(Environment.GetEnvironmentVariable("PC_F7C_FIXTURES")))
            File.AppendAllText(output!, JsonConvert.SerializeObject(Measure(fixture, samples)) + Environment.NewLine);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Measure(string fixture, int samples)
    {
        // The live city's retained bytes come from a separate, identical build that is measured and dropped first.
        var probe = new Holder();
        long retainedLive = BuildAndKeep(fixture, probe) - Released(probe);
        long buildStart = Stopwatch.GetTimestamp();
        var holder = new Holder { Built = F7cFixtures.Build(fixture) };
        double buildMs = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
        var state = holder.Built!.State;
        string payload = SaveCodec.Write(state);
        var phases = Enum.GetNames(typeof(SaveCostPhase)).ToDictionary(p => p, _ => new List<double>());
        var totals = new List<double>(); var allocated = new List<long>();
        var cost = new SaveCostRecorder();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            cost.Begin();
            string again = SaveCodec.Write(state, cost);
            cost.End(true, "");
            long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (again != payload) throw new InvalidOperationException("Writer is not deterministic on " + fixture);
            if (i == 0) continue; // warm-up
            foreach (SaveCostPhase phase in Enum.GetValues(typeof(SaveCostPhase))) phases[phase.ToString()].Add(cost.Milliseconds(phase));
            totals.Add(cost.TotalMilliseconds); allocated.Add(used);
        }
        var read = SaveCodec.Read(payload);
        if (read.Status != SaveReadStatus.Loaded || SaveCodec.Write(read.State!) != payload) throw new InvalidOperationException("Read-back differs on " + fixture);
        read = null!;
        int routes = PersonTable.Schedules(state).Count(c => c.Journey != null);
        int legs = PersonTable.Schedules(state).Sum(c => c.Journey?.Legs.Count ?? 0);
        var row = new
        {
            fixture, schema = state.Schema, residents = state.Citizens.Count, visitors = state.Visitors.Count,
            journeys = routes, journeyLegs = legs, buildMs = Math.Round(buildMs),
            payload = new { characters = payload.Length, utf8Bytes = Encoding.UTF8.GetByteCount(payload), sha256 = RepresentativeFixture.Sha256(payload) },
            writeMs = Stats(totals), phasesMs = phases.Where(p => p.Value.Count > 0).ToDictionary(p => p.Key.ToLowerInvariant(), p => Stats(p.Value)),
            allocatedBytesPerWrite = new { median = Median(allocated), max = allocated.Max() },
            allocationToUtf16Ratio = Math.Round(Median(allocated) / (2.0 * payload.Length), 3),
            retainedLiveCityBytes = retainedLive,
            retainedBytesAfterRead = RetainedRead(payload), samples,
            evidence = "macOS arm64 .NET 10 Release offline, one dotnet process; save timing is its own criterion, never ordinary-play mod-work",
        };
        return row;
    }

    private sealed class Holder { public F7cFixtures.Built? Built; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long BuildAndKeep(string fixture, Holder holder)
    {
        holder.Built = F7cFixtures.Build(fixture);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        return GC.GetTotalMemory(true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Released(Holder holder)
    {
        holder.Built = null;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        return GC.GetTotalMemory(true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long RetainedRead(string payload)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseline = GC.GetTotalMemory(true);
        var kept = SaveCodec.Read(payload);
        long bytes = GC.GetTotalMemory(true) - baseline;
        GC.KeepAlive(kept);
        return bytes;
    }

    internal static long Median(List<long> values) { var s = values.OrderBy(v => v).ToList(); return s[s.Count / 2]; }

    internal static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = Math.Round(s[s.Count / 2], 3), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 3),
            max = Math.Round(s[^1], 3), atLeast30Ms = s.Count(v => v >= 30) };
    }
}
