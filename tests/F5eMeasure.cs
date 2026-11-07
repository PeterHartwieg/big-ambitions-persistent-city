using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5e measurement harness (docs/perf/f5e.md), not a gate: it runs only when PC_F5E_MEASURE names an output
/// file, e.g. <c>PC_F5E_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5eMeasure</c>.
/// The fixture is the F7a representative replay's people, homes, shops and portals (EconomyScenario) with its two
/// 16-person-per-resident workplaces replaced by a business catalogue shaped like the reference city (383 work roles
/// per 957 residents, every 30th a school, areas across all bands, 1 in 20 unknown, 1 in 50 player-owned). The banded
/// arm seats them by <see cref="WorkplaceCapacityPolicy"/>; the base arm gives every business the old 16.</summary>
public sealed class F5eMeasure
{
    private static readonly double[] Areas = { 30, 60, 120, 250, 500, 900 };

    private static List<Place> Businesses(int residents, bool banded, double share)
    {
        // Capped so the stress catalogue stays inside SaveCodec.MaxPlaces with homes, shops, schools and portals.
        int count = Math.Min(19000, Math.Max(8, (int)Math.Round(residents * 383.0 / 957 * share)));
        var places = new List<Place>(count + count / 30 + 1);
        for (int i = 0; i < count; i++)
        {
            string id = "biz:" + i.ToString("D5", CultureInfo.InvariantCulture);
            uint draw = StableRandom.Hash(id, 5);
            double area = draw % 20 == 0 ? 0 : Areas[draw / 20 % (uint)Areas.Length];
            bool player = draw % 50 == 1, school = i % 30 == 29;
            var at = new Position(i % 40 * 25 - 300, 0, i / 40 * 25 - 300);
            places.Add(new Place { Id = id + ":work", Name = "Business " + i, District = i % 2 == 0 ? "a" : "b", Kind = PlaceKind.Work, Position = at,
                Capacity = banded ? WorkplaceCapacityPolicy.Work(area, player) : 16 });
            if (school)
                places.Add(new Place { Id = id + ":study", Name = "School " + i, District = "a", Kind = PlaceKind.Study, Position = at,
                    Capacity = banded ? WorkplaceCapacityPolicy.Study(area) : 16 });
        }
        return places;
    }

    /// <param name="share">Fraction of the reference-shaped business count; 0.2 leaves most visitors without a vacancy.</param>
    internal static (CityState State, CitySimulator City) Build(int residents, int factor, bool banded, double share = 1)
    {
        int imported = residents <= 5000 ? residents : 1000;
        var identities = Enumerable.Range(0, imported).Select(i => new CitizenIdentity
        {
            Id = "rlo:econ:c" + i.ToString("D5", CultureInfo.InvariantCulture), Name = "Citizen " + i, Age = 20 + i % 50,
            District = i % 2 == 0 ? "a" : "b", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new List<Place>
        {
            new() { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(0, 0, 0) },
            new() { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(500, 0, 0) },
            new() { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new Position(150, 0, 120) },
            new() { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new Position(600, 0, 100) },
            new() { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new Position(300, 0, -200) },
            new() { Id = "leisure-1", Name = "Leisure", District = "a", Kind = PlaceKind.Leisure, Position = new Position(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
        };
        places.AddRange(Businesses(residents, banded, share));
        var state = Population.Create(identities, places, "economy-scenario", "0.41.1", RepresentativeFixture.Seed, 1440 + 300);
        if (imported != residents)
        {
            long beds = (long)residents * 10 / 9;
            while (HousingPolicy.Target(beds) < residents) beds++;
            state.Places.Single(p => p.Id == "home-a").Capacity = (int)(beds - beds / 2);
            state.Places.Single(p => p.Id == "home-b").Capacity = (int)(beds / 2);
            Population.ExpandToHousing(state);
        }
        state.Food = EconomyScenario.Food(); state.Food.RepresentationFactor = factor;
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        var city = new CitySimulator(state, new RepresentativeFixture.Routes());
        city.AttachFoodMarket(new RepresentativeFixture.Market()); city.EnableHouseholdFinances();
        return (state, city);
    }

    private static object Stats(List<double> values)
    {
        if (values.Count == 0) return new { count = 0 };
        var s = values.OrderBy(v => v).ToList();
        return new { count = s.Count, p50 = Math.Round(s[s.Count / 2], 4), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 4),
            max = Math.Round(s[^1], 4), atLeast30Ms = s.Count(v => v >= 30) };
    }

    private static string? Output() => Environment.GetEnvironmentVariable("PC_F5E_MEASURE");
    private static string[] Fixtures(string defaults) => (Environment.GetEnvironmentVariable("PC_F5E_FIXTURES") ?? defaults).Split(',');

    /// <summary>The job-finding path of one successful daily roll: capacity bookkeeping (occupancy of every assigned
    /// place) plus the two-probe shortlist. Seated workers are unseated so their next rolls run the finding branch;
    /// each timed call is one that returned a shortlist (the roll succeeded). The two probes themselves are route requests
    /// through F3 admission and are counted in the day gate, not here.</summary>
    [Fact]
    public void JobFindingShortlistCost()
    {
        string? output = Output(); if (string.IsNullOrEmpty(output)) return;
        foreach (string fixture in Fixtures("957x2,5000x2,50000x2"))
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            var (state, _) = Build(residents, factor, banded: true);
            var sample = PersonTable.Schedules(state).Where(p => ResidentLifecycle.IsWorker(p) && p.DayPlaceId.Length != 0)
                .Where((_, i) => i % 7 == 0).Take(400).ToList();
            foreach (var person in sample) { person.DayPlaceId = ""; person.EmploymentStartDay = 0; person.VisitorCarried = null; }
            var times = new List<double>(); var bytes = new List<double>(); var bookkeeping = new List<double>(); var bookkeepingBytes = new List<double>();
            var indexedTimes = new List<double>(); var indexedBytes = new List<double>();
            int empty = 0, differ = 0;
            for (int warm = 0; warm < 3; warm++) Population.Assignments(state);
            var index = new WorkplaceOccupancy();
            long rebuildAllocated = GC.GetAllocatedBytesForCurrentThread(), rebuildStarted = Stopwatch.GetTimestamp();
            index.Rebuild(state);
            double rebuildMs = Stopwatch.GetElapsedTime(rebuildStarted).TotalMilliseconds;
            long rebuildBytes = GC.GetAllocatedBytesForCurrentThread() - rebuildAllocated;
            foreach (var person in sample)
            {
                int start = person.LastEmploymentCheckDay;
                for (int day = start + 1; day < start + 400; day++)
                {
                    double now = day * 1440.0 + 60;
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), t = Stopwatch.GetTimestamp();
                    var candidates = ResidentLifecycle.BeginEmploymentCheck(state, person, now);
                    double ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
                    long used = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (candidates == null) continue;
                    times.Add(ms); bytes.Add(used);
                    if (candidates.Count == 0) empty++;
                    // The same roll through the simulator's occupancy index (the check day is rewound for the replay).
                    person.LastEmploymentCheckDay = day - 1;
                    allocated = GC.GetAllocatedBytesForCurrentThread(); t = Stopwatch.GetTimestamp();
                    var indexed = ResidentLifecycle.BeginEmploymentCheck(state, person, now, null, index);
                    indexedTimes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds); indexedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
                    if (indexed == null || !indexed.Select(p => p.Id).SequenceEqual(candidates.Select(p => p.Id))) differ++;
                    break;
                }
                long a = GC.GetAllocatedBytesForCurrentThread(), s = Stopwatch.GetTimestamp();
                Population.Assignments(state, person);
                bookkeeping.Add(Stopwatch.GetElapsedTime(s).TotalMilliseconds); bookkeepingBytes.Add(GC.GetAllocatedBytesForCurrentThread() - a);
            }
            File.AppendAllText(output!, JsonConvert.SerializeObject(new
            {
                shortlist = true, fixture, persons = PersonTable.PersonCount(state), places = state.Places.Count,
                workplaces = state.Places.Count(p => p.Kind == PlaceKind.Work), probes = CityBalance.WorkplaceRouteProbes,
                findRollMs = Stats(times), findRollBytesMean = Math.Round(bytes.DefaultIfEmpty().Average()), emptyShortlists = empty,
                indexedFindRollMs = Stats(indexedTimes), indexedFindRollBytesMean = Math.Round(indexedBytes.DefaultIfEmpty().Average()), shortlistsDiffer = differ,
                indexRebuildMs = Math.Round(rebuildMs, 3), indexRebuildBytes = rebuildBytes,
                bookkeepingMs = Stats(bookkeeping), bookkeepingBytesMean = Math.Round(bookkeepingBytes.DefaultIfEmpty().Average())
            }) + Environment.NewLine);
        }
    }

    /// <summary>One natural day (the F7a representative window) of each arm: per-slice mod-work proxy, seated workers,
    /// carried visitors and job changes. Frame slices of 64 units, as F5d.</summary>
    [Fact]
    public void DayGateBandedAgainstBase()
    {
        string? output = Output(); if (string.IsNullOrEmpty(output)) return;
        foreach (string fixture in Fixtures("957x2,5000x2"))
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            foreach (var (banded, share, rule) in new[] { (false, 1.0, "base-16"), (true, 1.0, "bands"), (false, 0.2, "base-16-tight"), (true, 0.2, "bands-tight") })
            {
                var (state, city) = Build(residents, factor, banded, share);
                var slices = new List<double>(); long events = 0;
                while (!city.IsCaughtUpTo(RepresentativeFixture.TargetMinute))
                {
                    long t = Stopwatch.GetTimestamp();
                    var result = city.AdvanceFrameSlice(RepresentativeFixture.TargetMinute, 64);
                    slices.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds); events += result.Events;
                }
                city.SynchronizeSnapshot();
                var people = PersonTable.Schedules(state).ToList();
                File.AppendAllText(output!, JsonConvert.SerializeObject(new
                {
                    day = true, fixture, rule, persons = people.Count, events, sliceMs = Stats(slices),
                    workCapacity = state.Places.Where(p => p.Kind == PlaceKind.Work).Sum(p => (long)p.Capacity),
                    studyCapacity = state.Places.Where(p => p.Kind == PlaceKind.Study).Sum(p => (long)p.Capacity),
                    seatedResidents = state.Citizens.Count(c => c.DayPlaceId.Length != 0), seatedVisitors = state.VisitorRoutines.Count(v => v.DayPlaceId.Length != 0),
                    carriedVisitors = state.VisitorRoutines.Count(v => v.VisitorCarried.HasValue), jobChanges = people.Sum(p => p.JobChanges),
                    purchases = state.Food!.Residents.Sum(m => m.Purchases)
                }) + Environment.NewLine);
            }
        }
    }

    /// <summary>Fresh-city save cost after one natural day, banded against base: characters, cap fraction, both record
    /// counters, write and read latency (save timing, separate from ordinary play).</summary>
    [Fact]
    public void FreshCitySaveCost()
    {
        string? output = Output(); if (string.IsNullOrEmpty(output)) return;
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F5E_SAMPLES"), out int n) ? n : 5;
        foreach (string fixture in Fixtures("957x2,5000x2,50000x3"))
        {
            int residents = int.Parse(fixture.Split('x')[0]), factor = int.Parse(fixture.Split('x')[1]);
            foreach (bool banded in new[] { false, true })
            {
                var (state, city) = Build(residents, factor, banded);
                while (!city.IsCaughtUpTo(RepresentativeFixture.TargetMinute)) city.AdvanceFrameSlice(RepresentativeFixture.TargetMinute, 64);
                city.SynchronizeSnapshot();
                string payload = SaveCodec.Write(state);
                var records = SaveCodec.CountRecords(state, payload);
                SaveCodec.Read(payload);
                var writes = new List<double>(); var reads = new List<double>();
                for (int i = 0; i < samples; i++)
                {
                    long t = Stopwatch.GetTimestamp(); string again = SaveCodec.Write(state); writes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                    if (again != payload) throw new InvalidOperationException("Writer is not deterministic.");
                    t = Stopwatch.GetTimestamp(); var read = SaveCodec.Read(payload); reads.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                    if (read.Status != SaveReadStatus.Loaded) throw new InvalidOperationException(read.Error);
                }
                File.AppendAllText(output!, JsonConvert.SerializeObject(new
                {
                    save = true, fixture, rule = banded ? "bands" : "base-16", persons = PersonTable.PersonCount(state), places = state.Places.Count,
                    characters = payload.Length, capFraction = Math.Round(payload.Length / (double)SaveCodec.MaxInputCharacters, 4),
                    records = new { records.Streaming, records.Semantic, cap = SaveCodec.MaxEmbeddedRecords },
                    writeMs = Stats(writes), readMs = Stats(reads), samples
                }) + Environment.NewLine);
            }
        }
    }
}
