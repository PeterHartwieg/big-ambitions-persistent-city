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
/// F7d matched before/after harness (docs/perf/f7d.md), not a gate: it runs only when PC_F7D_MEASURE names an output file,
/// e.g. <c>PC_F7D_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F7dMeasure</c>.
/// PC_F7D_FIXTURES and PC_F7D_SAMPLES narrow a run. Each fixture is one fresh seeded city written by the predecessor JSON
/// writer (the frozen writer 33, "before") and by the compact writer 34 ("after") in the same process: sizes, records and
/// tables, the save pause and its phases, allocation per save, the off-thread read, activation and retained bytes.
/// </summary>
public sealed class F7dMeasure
{
    [Fact]
    public void CompactWriterAgainstThePredecessorJsonWriter()
    {
        string? output = Environment.GetEnvironmentVariable("PC_F7D_MEASURE");
        if (string.IsNullOrEmpty(output)) return; // measurement only on request
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F7D_SAMPLES"), out int n) ? n : 7;
        foreach (string fixture in F7cMeasure.Fixtures(Environment.GetEnvironmentVariable("PC_F7D_FIXTURES")))
            File.AppendAllText(output!, JsonConvert.SerializeObject(Measure(fixture, samples)) + Environment.NewLine);
    }

    private sealed class Holder { public F7cFixtures.Built? Built; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Measure(string fixture, int samples)
    {
        var holder = new Holder { Built = F7cFixtures.Build(fixture) };
        var state = holder.Built!.State;
        string before = DevelopmentChain.Write33(state);
        string after = SaveCodec.Write(state);
        if (SaveCodec.ExpandCompact(after) != ReaderCorpus.Relabel(before, 33, 34)) throw new InvalidOperationException("Bodies differ on " + fixture);
        var stats = new SaveCodec.CompactStats();
        SaveCodec.CompactFromBody(SaveCodec.ExpandCompact(after), stats);
        var counts = SaveCodec.CountRecords(state, after);

        var writeBefore = Writes(state, samples, before, frozen: true);
        var writeAfter = Writes(state, samples, after, frozen: false);
        // The compact transform alone (body text -> compact text), the part F7d adds to the encode phase.
        string body = SaveCodec.ExpandCompact(after);
        var transform = Time(samples, () => SaveCodec.CompactFromBody(body));
        var split = new SaveCodec.CompactStats();
        for (int i = 0; i < samples; i++) SaveCodec.CompactFromBody(body, split);
        double tableMs = split.CollectTicks * 1000.0 / Stopwatch.Frequency / samples, columnMs = split.WriteTicks * 1000.0 / Stopwatch.Frequency / samples;
        // Decode -> body text alone, the part F7d adds before the strict body reader.
        var expand = Time(samples, () => SaveCodec.ExpandCompact(after));

        var readBefore = Reads(samples, () => SaveCodec.ReadWriter33(before));
        var readAfter = Reads(samples, () => SaveCodec.Read(after));
        var activateBefore = Time(samples, () => { var s = SaveCodec.ReadWriter33(before).State!; var t = Stopwatch.GetTimestamp(); SaveCodec.ValidateForActivation(s, 33); _ = new CitySimulator(s, holder.Built.Routes); return Stopwatch.GetElapsedTime(t).TotalMilliseconds; }, inner: true);
        var activateAfter = Time(samples, () => { var s = SaveCodec.Read(after).State!; var t = Stopwatch.GetTimestamp(); SaveCodec.ValidateForActivation(s); _ = new CitySimulator(s, holder.Built.Routes); return Stopwatch.GetElapsedTime(t).TotalMilliseconds; }, inner: true);
        long retainedBefore = Retained(() => SaveCodec.ReadWriter33(before));
        long retainedAfter = Retained(() => SaveCodec.Read(after));

        int journeys = PersonTable.Schedules(state).Count(c => c.Journey != null);
        return new
        {
            fixture, residents = state.Citizens.Count, visitors = state.Visitors.Count, journeys,
            before = new
            {
                schema = 33, characters = before.Length, utf8Bytes = Encoding.UTF8.GetByteCount(before), utf16Bytes = 2L * before.Length,
                sha256 = RepresentativeFixture.Sha256(before), write = writeBefore, read = readBefore, activationMs = activateBefore, retainedBytesAfterRead = retainedBefore,
            },
            after = new
            {
                schema = 34, characters = after.Length, utf8Bytes = Encoding.UTF8.GetByteCount(after), utf16Bytes = 2L * after.Length,
                sha256 = RepresentativeFixture.Sha256(after), write = writeAfter, compactTransformMs = transform, compactTablePassMeanMs = Math.Round(tableMs, 3), compactColumnWriterMeanMs = Math.Round(columnMs, 3), read = readAfter, expandMs = expand,
                activationMs = activateAfter, retainedBytesAfterRead = retainedAfter,
                tables = new { strings = stats.Strings, keys = stats.Keys, shapes = stats.Shapes, routes = stats.Routes, entries = stats.TableEntries },
                routeReferences = stats.RouteReferences, roadTags = stats.RoadTags, nesting = SaveCodec.CompactTree.Nesting(after), decodedValues = stats.Values,
                records = new { streaming = counts.Streaming, semantic = counts.Semantic },
            },
            characterRatio = Math.Round((double)after.Length / before.Length, 4),
            samples,
            evidence = "Windows 11 .NET 10 Release offline, one dotnet process; save and load timing is its own criterion, never ordinary-play mod-work",
        };
    }

    private static object Writes(CityState state, int samples, string expected, bool frozen)
    {
        var phases = Enum.GetNames(typeof(SaveCostPhase)).ToDictionary(p => p, _ => new List<double>());
        var totals = new List<double>(); var allocated = new List<long>();
        var cost = new SaveCostRecorder();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            cost.Begin();
            string again;
            if (frozen)
            {
                int schema = state.Schema; state.Schema = 33;
                try { again = SaveCodec.WriteForSchema(state, 33, cost); } finally { state.Schema = schema; }
            }
            else again = SaveCodec.Write(state, cost);
            cost.End(true, "");
            long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (again != expected) throw new InvalidOperationException("Writer is not deterministic.");
            if (i == 0) continue; // warm-up
            foreach (SaveCostPhase phase in Enum.GetValues(typeof(SaveCostPhase))) phases[phase.ToString()].Add(cost.Milliseconds(phase));
            totals.Add(cost.TotalMilliseconds); allocated.Add(used);
        }
        return new
        {
            pauseMs = F7cMeasure.Stats(totals),
            phasesMs = phases.Where(p => p.Value.Count > 0 && p.Value.Any(v => v > 0)).ToDictionary(p => p.Key.ToLowerInvariant(), p => F7cMeasure.Stats(p.Value)),
            allocatedBytes = new { median = F7cMeasure.Median(allocated), max = allocated.Max() },
            allocationPerUtf16Byte = Math.Round(F7cMeasure.Median(allocated) / (2.0 * expected.Length), 3),
        };
    }

    private static object Reads(int samples, Func<SaveReadResult> read)
    {
        var times = new List<double>(); var allocated = new List<long>();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            var result = read();
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (result.Status != SaveReadStatus.Loaded) throw new InvalidOperationException("Read failed: " + result.Error);
            if (i == 0) continue;
            times.Add(ms); allocated.Add(used);
        }
        return new { ms = F7cMeasure.Stats(times), allocatedBytes = new { median = F7cMeasure.Median(allocated), max = allocated.Max() } };
    }

    private static object Time(int samples, Func<object?> work)
    {
        var times = new List<double>();
        for (int i = 0; i <= samples; i++)
        {
            long start = Stopwatch.GetTimestamp();
            GC.KeepAlive(work());
            if (i > 0) times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        return F7cMeasure.Stats(times);
    }

    /// <summary>Times only the part <paramref name="work"/> reports (its own stopwatch), excluding its setup.</summary>
    private static object Time(int samples, Func<double> work, bool inner)
    {
        var times = new List<double>();
        for (int i = 0; i <= samples; i++) { double ms = work(); if (i > 0) times.Add(ms); }
        return F7cMeasure.Stats(times);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Retained(Func<SaveReadResult> read)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseline = GC.GetTotalMemory(true);
        var kept = read();
        long bytes = GC.GetTotalMemory(true) - baseline;
        GC.KeepAlive(kept);
        return bytes;
    }
}
