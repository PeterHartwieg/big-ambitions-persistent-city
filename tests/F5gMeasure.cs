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

/// <summary>F5g measurement harness (docs/perf/f5g.md), not a gate: it runs only when PC_F5G_MEASURE names an output
/// file, e.g. <c>PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5gMeasure</c>.
/// Each city is the F5g test fixture scaled up: a factor-1 fresh city whose census catalogue gives the stated commuter
/// target, filled at its first day boundary, then run one more natural day. Persons are persisted persons (residents,
/// commuters and the whole day-visitor pool), never residents alone. The planner double is the endpoint-keyed bounded
/// geometry cache of <see cref="F5gPopulationFillTests.CachedPortalPlanner"/>, a model of Runtime's WalkingRoutes cache.</summary>
public sealed class F5gMeasure
{
    private static object Stats(List<double> values)
    {
        if (values.Count == 0) return new { count = 0 };
        var s = values.OrderBy(v => v).ToList();
        return new { count = s.Count, p50 = Math.Round(s[s.Count / 2], 4), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 4),
            max = Math.Round(s[^1], 4), atLeast30Ms = s.Count(v => v >= 30) };
    }

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];

    private sealed class Arm
    {
        public string Name = "";
        public int Homes, Budget;
        public long CommuterTarget;
        public bool Skip;
    }

    private static Dictionary<string, object> Run(Arm arm)
    {
        var (state, market) = F5gPopulationFillTests.City(arm.Homes, 1, seed: 2026);
        var (fill, _) = F5gPopulationFillTests.CatalogueFor(state, arm.Homes, arm.CommuterTarget);
        var planner = new F5gPopulationFillTests.CachedPortalPlanner();
        var city = F5gPopulationFillTests.Start(state, market, fill, planner);
        double boundary = 2 * 1440, end = 3 * 1440 + 6 * 60;
        var unitMs = new List<double>(); var unitBytes = new List<double>(); var unitPersons = new List<double>();
        var sliceMs = new List<double>(); var fillSliceMs = new List<double>();
        var requestsPerMinute = new Dictionary<long, long>();
        long events = 0;
        int peakPersonQueue = 0, peakMealQueue = 0;
        if (arm.Skip)
        {
            long started = Stopwatch.GetTimestamp();
            while (!city.AdvanceTo(end, 1_000_000).CaughtUp) { }
            sliceMs.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        else
        {
            while (!city.IsCaughtUpTo(end))
            {
                long units = city.PopulationFillUnits, requests = planner.Requests;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                var result = city.AdvanceFrameSlice(end, arm.Budget);
                double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                events += result.Events; sliceMs.Add(ms);
                if (city.PopulationFillUnits != units)
                {
                    fillSliceMs.Add(ms);
                    // Budget 1 isolates one unit per slice: its time and allocation are the unit's own.
                    if (arm.Budget == 1) { unitMs.Add(ms); unitBytes.Add(allocated); unitPersons.Add(city.LastFillUnitPersons); }
                }
                long minute = (long)state.Minute;
                requestsPerMinute[minute] = (requestsPerMinute.TryGetValue(minute, out var r) ? r : 0) + planner.Requests - requests;
                peakPersonQueue = Math.Max(peakPersonQueue, city.QueuedPersonEvents);
                peakMealQueue = Math.Max(peakMealQueue, city.QueuedMealEvents);
            }
        }
        city.SynchronizeSnapshot();
        F5gPopulationFillTests.Invariants(state, arm.Name); F5gPopulationFillTests.Conserved(state, arm.Name);
        var targets = city.PopulationFillTargets!.Value;
        var (commuters, pool) = F5gPopulationFillTests.Counts(state);
        long peakMinuteRequests = requestsPerMinute.Count == 0 ? 0 : requestsPerMinute.Values.Max();
        return new Dictionary<string, object>
        {
            ["arm"] = arm.Name, ["budget"] = arm.Skip ? "skip" : arm.Budget.ToString(),
            ["residents"] = state.Citizens.Count, ["commuters"] = commuters, ["pool"] = pool,
            ["persistedPersons"] = state.Citizens.Count + state.Visitors.Count,
            ["commuterTarget"] = targets.CommuterTarget, ["poolTarget"] = targets.PoolTarget, ["visitorCapacity"] = targets.VisitorCapacity,
            ["fillUnits"] = city.PopulationFillUnits, ["fillCreated"] = city.PopulationFillCreated,
            ["unitMs"] = Stats(unitMs), ["unitAllocatedBytes"] = Stats(unitBytes), ["unitPersons"] = Stats(unitPersons),
            ["fillSliceMs"] = Stats(fillSliceMs), ["sliceMs"] = Stats(sliceMs), ["events"] = events,
            ["historicalClockMisses"] = city.HistoricalClockMisses, ["peakPersonQueue"] = peakPersonQueue, ["peakMealQueue"] = peakMealQueue,
            ["routeRequests"] = planner.Requests, ["portalRequests"] = planner.PortalRequests,
            ["portalHits"] = planner.PortalHits, ["portalMisses"] = planner.PortalMisses,
            ["peakRequestsPerGameMinute"] = peakMinuteRequests,
            // Admission model: Runtime admits at most 16 native path queries per frame (MultimodalRoutes.MaximumJobPathsPerFrame).
            ["peakMinuteFramesAt16PathsPerFrame"] = (peakMinuteRequests + 15) / 16,
            ["stateHash"] = Sha(SaveCodec.Write(state) + string.Join(",", market.Orders)),
            ["fillBoundary"] = boundary
        };
    }

    [Fact]
    public void Measure()
    {
        string? path = Environment.GetEnvironmentVariable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        var lines = new List<string>();
        void Emit(object row) { string line = JsonConvert.SerializeObject(row); lines.Add(line); File.AppendAllText(path, line + Environment.NewLine); }
        // Reference: 957 residents (152 homes) with a 1,000-commuter target; the pool takes its census target.
        // Stress: 3,339 residents (530 homes) filled to their whole visitor capacity, about 10,000 persisted persons.
        var shapes = new[] { ("reference", 152, 1000L), ("stress-10k", 530, -1L) };
        foreach (var (name, homes, commuterTarget) in shapes)
        {
            long target = commuterTarget;
            var (probe, _) = F5gPopulationFillTests.City(homes, 1, seed: 2026);
            if (target < 0)
            {
                var t = VisitorTargets.Compute(0, 0, probe.Citizens.Count, probe.Portals.Count);
                target = t.VisitorCapacity - t.PoolDemand;
            }
            // The nearest target at or below that the even band capacities reach.
            while (F5gPopulationFillTests.TryCatalogueFor(probe, homes, target) == null) target--;
            string? hash = null;
            foreach (var (budget, skip) in new[] { (1, false), (16, false), (128, false), (0, true) })
            {
                var row = Run(new Arm { Name = name, Homes = homes, Budget = budget, CommuterTarget = target, Skip = skip });
                Emit(row);
                hash ??= (string)row["stateHash"];
                Assert.Equal(hash, (string)row["stateHash"]);
                Assert.Equal(0L, (long)row["historicalClockMisses"]);
            }
        }
        Assert.NotEmpty(lines);
    }
}
