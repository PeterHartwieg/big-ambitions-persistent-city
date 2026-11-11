using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5d measurement harness (docs/perf/f5d.md), not a gate: it runs only when PC_F5D_MEASURE names an output
/// file, e.g. <c>PC_F5D_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5dMeasure</c>.
/// Each fresh city is the F7a representative replay (EconomyScenario, one natural day, household finances) with two
/// portals at factor K, so K-1 visitor persons per resident run their own routines. The same residents at K=1 give the
/// per-visitor cost. Events are counted per game hour; queue lengths are sampled after every slice.</summary>
public sealed class F5dMeasure
{
    private sealed class Run
    {
        public CityState State = null!;
        public long Events;
        public double BuildMs;
        public readonly long[] EventsPerHour = new long[48];
        public int PeakPersonQueue, PeakMealQueue;
    }

    private static Run Build(int residents, int factor)
    {
        long started = Stopwatch.GetTimestamp();
        var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed); state.Food = EconomyScenario.Food();
        state.Food.RepresentationFactor = factor;
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        var routes = new RepresentativeFixture.Routes(); var market = new RepresentativeFixture.Market();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        var run = new Run { State = state };
        double start = state.Minute;
        while (!city.IsCaughtUpTo(RepresentativeFixture.TargetMinute))
        {
            var result = city.AdvanceFrameSlice(RepresentativeFixture.TargetMinute, 64);
            run.Events += result.Events;
            int hour = (int)Math.Min(run.EventsPerHour.Length - 1, Math.Max(0, (state.Minute - start) / 60));
            run.EventsPerHour[hour] += result.Events;
            run.PeakPersonQueue = Math.Max(run.PeakPersonQueue, city.QueuedPersonEvents);
            run.PeakMealQueue = Math.Max(run.PeakMealQueue, city.QueuedMealEvents);
        }
        city.SynchronizeSnapshot();
        if (state.Food!.Residents.Any(m => !HouseholdFinance.CashConserved(m.Budget!))) throw new InvalidOperationException("Unconserved cash.");
        run.BuildMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return run;
    }

    private static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = Math.Round(s[s.Count / 2], 3), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 3),
            max = Math.Round(s[^1], 3), atLeast30Ms = s.Count(v => v >= 30) };
    }

    private static long Retained(string payload)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseline = GC.GetTotalMemory(true);
        var kept = SaveWire.Read(payload);
        long bytes = GC.GetTotalMemory(true) - baseline;
        GC.KeepAlive(kept);
        return bytes;
    }

    /// <summary>Review Opus-r1-1: one-time visitor creation at the fresh roster (VisitorSchedules.Create, ScheduleVisitors and
    /// Enrol), which runs inside AttachFoodMarket on the main thread during initialization. The catalogue is padded with
    /// 2,000 zero-capacity homes to a game-sized place count. Attach at factor K minus attach at K=1 is the visitor cost.</summary>
    [Fact]
    public void VisitorCreationCostAtInitialization()
    {
        string? output = PerfGate.Variable("PC_F5D_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        string[] only = (Environment.GetEnvironmentVariable("PC_F5D_FIXTURES") ?? "957x2,957x3,5000x2,5000x3,50000x2,50000x3").Split(',');
        double Attach(int residents, int factor, out int visitors)
        {
            var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed); state.Food = EconomyScenario.Food();
            state.Food.RepresentationFactor = factor;
            for (int i = 0; i < 2000; i++)
                state.Places.Add(new Place { Id = "pad:" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Name = "Pad", District = "a",
                    Kind = PlaceKind.Home, Capacity = 0, Position = new Position(i % 50 * 10, 0, i / 50 * 10) });
            state.Places.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            PortalPlaces.Add(state, RepresentativeFixture.Portals);
            var city = new CitySimulator(state, new RepresentativeFixture.Routes());
            long t = Stopwatch.GetTimestamp();
            city.AttachFoodMarket(new RepresentativeFixture.Market());
            double ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
            visitors = state.Visitors.Count;
            return ms;
        }
        Attach(957, 2, out _); // warm
        foreach (string fixture in only)
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            var withVisitors = new List<double>(); var residentsOnly = new List<double>(); int visitors = 0;
            for (int i = 0; i < 3; i++) { withVisitors.Add(Attach(residents, factor, out visitors)); residentsOnly.Add(Attach(residents, 1, out _)); }
            double median(List<double> v) { v.Sort(); return Math.Round(v[v.Count / 2], 1); }
            File.AppendAllText(output, JsonConvert.SerializeObject(new { creation = true, fixture, visitors, places = 2000 + 11 + 2,
                attachMs = median(withVisitors), residentsOnlyAttachMs = median(residentsOnly),
                visitorCreationMs = Math.Round(median(withVisitors) - median(residentsOnly), 1) }) + Environment.NewLine);
        }
    }

    /// <summary>Visitor wire while visitors are in the city (day 2, 12:00): tuple plus routine, with and without a journey.</summary>
    [Fact]
    public void ActiveVisitorWireAtMidday()
    {
        string? output = PerfGate.Variable("PC_F5D_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        var state = EconomyScenario.Create(957, RepresentativeFixture.Seed); state.Food = EconomyScenario.Food();
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        var city = new CitySimulator(state, new RepresentativeFixture.Routes()); city.AttachFoodMarket(new RepresentativeFixture.Market()); city.EnableHouseholdFinances();
        var sizes = new List<(VisitorPresence Presence, bool Journey, int Characters)>();
        foreach (double at in new[] { 2 * 1440 + 8 * 60 + 20.0, 2 * 1440 + 12 * 60.0, 2 * 1440 + 17 * 60 + 40.0 })
        {
            while (!city.IsCaughtUpTo(at)) city.AdvanceFrameSlice(at, 64);
            city.SynchronizeSnapshot();
            var root = SaveWire.Parse(SaveCodec.Write(state));
            var tuples = (JArray)root["Visitors"]!; var routines = (JArray)root["VisitorRoutines"]!;
            for (int i = 0; i < tuples.Count; i++)
                sizes.Add((state.Visitors[i].Stay.Presence, state.VisitorRoutines[i].Journey != null,
                    JsonConvert.ToString((string)tuples[i]!).Length + routines[i]!.ToString(Formatting.None).Length + 2));
        }
        object Group(IEnumerable<int> values) { var v = values.DefaultIfEmpty().ToList(); return new { count = v.Count(x => x > 0), mean = Math.Round(v.Average(), 1), max = v.Max() }; }
        File.AppendAllText(output, JsonConvert.SerializeObject(new { activeVisitorWire = true, samples = new[] { "08:20", "12:00", "17:40" },
            dormant = Group(sizes.Where(s => s.Presence == VisitorPresence.Dormant).Select(s => s.Characters)),
            activeIdle = Group(sizes.Where(s => s.Presence == VisitorPresence.Active && !s.Journey).Select(s => s.Characters)),
            activeTravelling = Group(sizes.Where(s => s.Presence == VisitorPresence.Active && s.Journey).Select(s => s.Characters)) }) + Environment.NewLine);
    }

    [Fact]
    public void FreshCityPersonsCostAtWriter27()
    {
        string? output = PerfGate.Variable("PC_F5D_MEASURE");
        if (string.IsNullOrEmpty(output)) return; // measurement only on request
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F5D_SAMPLES"), out int n) ? n : 5;
        string[] only = (Environment.GetEnvironmentVariable("PC_F5D_FIXTURES") ?? "957x2,957x3,5000x2,5000x3,50000x2,50000x3").Split(',');
        foreach (string fixture in only)
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            var baseRun = Build(residents, 1); string basePayload = SaveCodec.Write(baseRun.State);
            var run = Build(residents, factor); var state = run.State;
            string payload = SaveCodec.Write(state);
            var records = SaveCodec.CountRecords(state, payload); var baseRecords = SaveCodec.CountRecords(baseRun.State, basePayload);
            // Per-visitor wire: its tuple plus its routine object, by presence.
            var root = SaveWire.Parse(payload);
            var tuples = (JArray)root["Visitors"]!; var routines = (JArray)root["VisitorRoutines"]!;
            var perVisitor = new List<(bool Dormant, int Characters)>();
            for (int i = 0; i < tuples.Count; i++)
                perVisitor.Add((state.Visitors[i].Stay.Presence == VisitorPresence.Dormant,
                    JsonConvert.ToString((string)tuples[i]!).Length + routines[i]!.ToString(Formatting.None).Length + 2));
            root = null; tuples = routines = null;
            var writes = new List<double>(); var reads = new List<double>();
            SaveWire.Read(SaveCodec.Write(state)); // warm
            for (int i = 0; i < samples; i++)
            {
                long t = Stopwatch.GetTimestamp(); string again = SaveCodec.Write(state); writes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                if (again != payload) throw new InvalidOperationException("Writer is not deterministic.");
                t = Stopwatch.GetTimestamp(); var read = SaveWire.Read(payload); reads.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                if (read.Status != SaveReadStatus.Loaded) throw new InvalidOperationException(read.Error);
            }
            long kept = Retained(payload), keptBase = Retained(basePayload);
            int visitors = state.Visitors.Count;
            var line = new
            {
                fixture, residents, factor, visitors, persons = PersonTable.PersonCount(state),
                characters = payload.Length, capFraction = Math.Round(payload.Length / (double)SaveCodec.MaxInputCharacters, 4),
                residentsOnlyCharacters = basePayload.Length,
                addedCharactersPerVisitor = visitors == 0 ? 0 : Math.Round((payload.Length - basePayload.Length) / (double)visitors, 1),
                visitorWire = new { dormantMax = perVisitor.Where(v => v.Dormant).Select(v => v.Characters).DefaultIfEmpty().Max(),
                    activeMax = perVisitor.Where(v => !v.Dormant).Select(v => v.Characters).DefaultIfEmpty().Max(),
                    mean = perVisitor.Count == 0 ? 0 : Math.Round(perVisitor.Average(v => v.Characters), 1),
                    dormant = perVisitor.Count(v => v.Dormant), active = perVisitor.Count(v => !v.Dormant) },
                records = new { records.Streaming, records.Semantic, residentsOnly = baseRecords.Semantic, cap = SaveCodec.MaxEmbeddedRecords },
                retainedBytes = kept, retainedPerVisitor = visitors == 0 ? 0 : Math.Round((kept - keptBase) / (double)visitors, 1),
                writeMs = Stats(writes), readMs = Stats(reads),
                dayEvents = run.Events, residentsOnlyDayEvents = baseRun.Events,
                eventsPerGameHour = new { max = run.EventsPerHour.Max(), mean = Math.Round(run.EventsPerHour.Where(e => e > 0).Average(), 1) },
                residentsOnlyEventsPerGameHourMax = baseRun.EventsPerHour.Max(),
                peakQueue = new { persons = run.PeakPersonQueue, meals = run.PeakMealQueue },
                residentsOnlyPeakQueue = new { persons = baseRun.PeakPersonQueue, meals = baseRun.PeakMealQueue },
                purchases = state.Food!.Residents.Sum(m => m.Purchases),
                visitorPurchases = state.Food.Residents.Where(m => m.ResidentId.StartsWith(PersonIds.VisitorPrefix, StringComparison.Ordinal)).Sum(m => m.Purchases),
                buildMs = Math.Round(run.BuildMs), samples,
                evidence = "Windows .NET 10 Release offline; one natural day of the representative fixture; save timing separate from ordinary play"
            };
            File.AppendAllText(output, JsonConvert.SerializeObject(line) + Environment.NewLine);
        }
    }
}
