using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite measurement harness (docs/perf/f5c-lite.md), not a gate: it runs only when
/// PC_F5C_LITE_MEASURE names an output file, e.g.
/// <c>PC_F5C_LITE_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5cLiteMeasure</c>.
/// Each fresh city is the F7a representative replay (one natural day, household finances) at factor K. The identical
/// state is written by writer 25 (the frozen exact-25 path) and writer 26, so the byte, record, memory and latency
/// differences are the person shape alone.</summary>
public sealed class F5cLiteMeasure
{
    private static RepresentativeFixture.Replay Build(int residents, int factor)
    {
        var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed); state.Food = EconomyScenario.Food();
        state.Food.RepresentationFactor = factor;
        var routes = new RepresentativeFixture.Routes(); var market = new RepresentativeFixture.Market();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        long events = 0;
        while (!city.IsCaughtUpTo(RepresentativeFixture.TargetMinute)) events += city.AdvanceFrameSlice(RepresentativeFixture.TargetMinute, 64).Events;
        city.SynchronizeSnapshot();
        return new RepresentativeFixture.Replay(city, routes, market, events);
    }

    private static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = Math.Round(s[s.Count / 2], 3), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 3),
            max = Math.Round(s[^1], 3), atLeast30Ms = s.Count(v => v >= 30) };
    }

    private static (List<double> Write, List<double> Read, long Retained, long ReadAllocated) Time(Func<string> write, Func<string, SaveReadResult> read, int samples)
    {
        var writes = new List<double>(); var reads = new List<double>(); long readAllocated = 0;
        string payload = write();
        for (int i = 0; i <= samples; i++)
        {
            long start = Stopwatch.GetTimestamp(); string again = write(); double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (!ReferenceEquals(again, payload) && again != payload) throw new InvalidOperationException("Writer is not deterministic.");
            long bytes = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            var result = read(payload); double readMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (result.Status != SaveReadStatus.Loaded) throw new InvalidOperationException(result.Error);
            if (i == 0) continue;
            writes.Add(ms); reads.Add(readMs); readAllocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long baseline = GC.GetTotalMemory(true);
        var retained = read(payload); long kept = GC.GetTotalMemory(true) - baseline; GC.KeepAlive(retained);
        return (writes, reads, kept, readAllocated);
    }

    [Fact]
    public void FreshCitySaveCostAtWriter25And26()
    {
        string? output = Environment.GetEnvironmentVariable("PC_F5C_LITE_MEASURE");
        if (string.IsNullOrEmpty(output)) return; // measurement only on request
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F5C_LITE_SAMPLES"), out int n) ? n : 5;
        string[] only = (Environment.GetEnvironmentVariable("PC_F5C_LITE_FIXTURES") ?? "957x2,957x3,5000x2,5000x3,50000x2,50000x3").Split(',');
        foreach (string fixture in only)
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            long buildStart = Stopwatch.GetTimestamp();
            var replay = Build(residents, factor); var state = replay.State;
            double buildMs = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
            string Write25() => DevelopmentChain.Write25(state);
            string Write26() => SaveCodec.Write(state);
            string p25 = Write25(), p26 = Write26();
            var c25 = SaveCodec.CountRecordsForSchema(DevelopmentChainState(state), p25, 25);
            var c26 = SaveCodec.CountRecords(state, p26);
            // Warm both paths (JIT, caches) before either is measured, then alternate the measured arms.
            Time(Write25, SaveCodec.ReadDevelopmentChain, 1); Time(Write26, SaveCodec.Read, 1);
            var t25 = Time(Write25, SaveCodec.ReadDevelopmentChain, samples);
            var t26 = Time(Write26, SaveCodec.Read, samples);
            int visitors = state.Visitors.Count, persons = PersonTable.PersonCount(state);
            long added = p26.Length - (long)p25.Length;
            var row = new
            {
                fixture, residents = state.Citizens.Count, visitors, persons, rosterRecords = state.Food!.Residents.Count, buildMs = Math.Round(buildMs, 1),
                replayEvents = replay.Events, purchases = state.Food.Residents.Sum(m => m.Purchases),
                writer25 = new { characters = p25.Length, streamingRecords = c25.Streaming, semanticRecords = c25.Semantic,
                    writeMs = Stats(t25.Write), readMs = Stats(t25.Read), readAllocatedBytes = t25.ReadAllocated, retainedBytesAfterRead = t25.Retained },
                writer26 = new { characters = p26.Length, streamingRecords = c26.Streaming, semanticRecords = c26.Semantic,
                    writeMs = Stats(t26.Write), readMs = Stats(t26.Read), readAllocatedBytes = t26.ReadAllocated, retainedBytesAfterRead = t26.Retained },
                addedCharacters = added, addedRecords = c26.Streaming - c25.Streaming,
                residentTupleCharacters = state.Citizens.Max(c => c.DayPlaceId.Length == 0 ? 3 : 3 + c.EmploymentStartDay.ToString().Length + 4) + 7,
                visitorTupleMaxCharacters = visitors == 0 ? 0 : SaveCodecVisitorWire(p26).Max(),
                capPercent26 = Math.Round(100.0 * p26.Length / SaveCodec.MaxInputCharacters, 2),
                retainedDeltaBytesPerVisitor = visitors == 0 ? 0 : Math.Round((t26.Retained - t25.Retained) / (double)visitors, 1),
                evidence = "Windows .NET 10 synthetic, foreground, one test process; save/load timing is its own criterion, never ordinary-play mod-work"
            };
            File.AppendAllText(output!, JsonConvert.SerializeObject(row) + Environment.NewLine);
        }
    }

    /// <summary>Perf win in code this slice adds: represented visitors were first kept ordinal by binary-search insert
    /// (O(n) shift per visitor), now appended and sorted once. Measured on the in-game RLO ID order c0, c1, ...</summary>
    [Fact]
    public void VisitorEnrolmentOrderingInsertVersusSortOnce()
    {
        string? output = Environment.GetEnvironmentVariable("PC_F5C_LITE_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        foreach (int residents in new[] { 957, 5000, 50000 })
            foreach (int factor in new[] { 2, 3 })
            {
                var ids = new List<string>();
                for (int r = 0; r < residents; r++)
                    for (int slot = 1; slot < factor; slot++) ids.Add(FoodRepresentation.ConsumerId("c" + r, slot));
                double Insert()
                {
                    var list = new List<VisitorPerson>(); long start = Stopwatch.GetTimestamp();
                    foreach (var id in ids)
                    {
                        int low = 0, high = list.Count - 1;
                        while (low <= high) { int mid = low + ((high - low) >> 1); if (string.CompareOrdinal(list[mid].Id, id) < 0) low = mid + 1; else high = mid - 1; }
                        list.Insert(low, new VisitorPerson { Id = id });
                    }
                    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                double SortOnce()
                {
                    var city = new CityState(); long start = Stopwatch.GetTimestamp();
                    foreach (var id in ids) PersonTable.AddRepresentedVisitor(city, id);
                    PersonTable.OrderVisitors(city);
                    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                Insert(); SortOnce();
                var before = Enumerable.Range(0, 5).Select(_ => Insert()).ToList(); var after = Enumerable.Range(0, 5).Select(_ => SortOnce()).ToList();
                File.AppendAllText(output!, JsonConvert.SerializeObject(new { measure = "visitor-enrolment-ordering", residents, factor, visitors = ids.Count,
                    insertMs = Stats(before), appendSortMs = Stats(after) }) + Environment.NewLine);
            }
    }

    // The writer-25 record counter validates the state at 25; give it the same graph labelled 25.
    private static CityState DevelopmentChainState(CityState state) => SaveCodec.ReadDevelopmentChain(DevelopmentChain.Write25(state)).State!;

    private static IEnumerable<int> SaveCodecVisitorWire(string payload)
    {
        var root = SaveWire.Parse(payload);
        foreach (var visitor in root["Visitors"]!) yield return JsonConvert.SerializeObject((string)visitor!).Length + 1;
    }
}
