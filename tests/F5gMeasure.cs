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
        var unitMs = new List<double>(); var unitBytes = new List<double>(); var unitPersons = new List<double>();
        var sliceMs = new List<double>(); var fillSliceMs = new List<double>();
        var requestsPerMinute = new Dictionary<long, long>();
        long events = 0;
        int peakPersonQueue = 0, peakMealQueue = 0, fillDays = 0;
        // Fill phase: a boundary publishes at most 1,024 persons, so cross boundaries until one publishes nobody. Then the
        // measured day: from that boundary to 06:00 on the following day, at the full persisted population.
        double end = 2 * 1440;
        while (true)
        {
            long created = city.PopulationFillCreated;
            if (arm.Skip) { while (!city.AdvanceTo(end + 1, 1_000_000).CaughtUp) { } }
            else
            {
                // The day before the boundary is not measured; the boundary itself is driven slice by slice.
                while (!city.AdvanceTo(end - 1, 1_000_000).CaughtUp) { }
                Drive(end + 1, false);
            }
            // A boundary that publishes less than its cap has reached its targets (daily job churn can still add a few later).
            if (city.PopulationFillCreated - created < PopulationFillPolicy.MaximumPersonsPerBoundary) break;
            fillDays++; end += 1440;
            Assert.True(fillDays < 40, "fill did not converge");
        }
        double measuredFrom = state.Minute; end += 1440 + 1;
        sliceMs.Clear(); events = 0;
        if (arm.Skip)
        {
            long started = Stopwatch.GetTimestamp();
            while (!city.AdvanceTo(end, 1_000_000).CaughtUp) { }
            sliceMs.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        else Drive(end, true);
        void Drive(double target, bool measured)
        {
            while (!city.IsCaughtUpTo(target))
            {
                long units = city.PopulationFillUnits, requests = planner.Requests, published = city.PopulationFillCreated;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                var result = city.AdvanceFrameSlice(target, arm.Budget);
                double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                events += result.Events; sliceMs.Add(ms);
                if (city.PopulationFillUnits != units)
                {
                    fillSliceMs.Add(ms);
                    // Budget 1 isolates one unit per slice: its time and allocation are the unit's own.
                    if (arm.Budget == 1) { unitMs.Add(ms); unitBytes.Add(allocated); unitPersons.Add(city.LastFillUnitPersons); }
                }
                if (!measured) { sliceMs.Clear(); events = 0; }
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
            ["fillUnits"] = city.PopulationFillUnits, ["fillCreated"] = city.PopulationFillCreated, ["fillBoundaries"] = fillDays + 1,
            ["skipped"] = city.PopulationFillSkipped,
            ["measuredFrom"] = measuredFrom,
            ["unitMs"] = Stats(unitMs), ["unitAllocatedBytes"] = Stats(unitBytes), ["unitPersons"] = Stats(unitPersons),
            ["fillSliceMs"] = Stats(fillSliceMs), ["sliceMs"] = Stats(sliceMs), ["events"] = events,
            ["historicalClockMisses"] = city.HistoricalClockMisses, ["peakPersonQueue"] = peakPersonQueue, ["peakMealQueue"] = peakMealQueue,
            ["routeRequests"] = planner.Requests, ["portalRequests"] = planner.PortalRequests,
            ["portalHits"] = planner.PortalHits, ["portalMisses"] = planner.PortalMisses,
            ["peakRequestsPerGameMinute"] = peakMinuteRequests,
            // Admission model: Runtime admits at most 16 native path queries per frame (MultimodalRoutes.MaximumJobPathsPerFrame).
            ["peakMinuteFramesAt16PathsPerFrame"] = (peakMinuteRequests + 15) / 16,
            ["stateHash"] = Sha(SaveCodec.Write(state) + string.Join(",", market.Orders))
        };
    }

    /// <summary>Review Opus-r1-3 and Sol-r2-2: a fill boundary at the resident bound. 7,936 homes give 49,996 residents and a
    /// capacity of 99,992 visitors; factor 2 seeds 49,996 commuters, and catalogue-only businesses keep commuter demand above
    /// the capacity. The first boundary creates 1,024 more commuters in 16 units of 64, each merging its persons
    /// into about 50,000 visitors. The remaining deficit fills over later boundaries.</summary>
    private static Dictionary<string, object> Capacity()
    {
        const int homes = 7936;
        long build = Stopwatch.GetTimestamp();
        var (state, market) = F5gPopulationFillTests.City(homes, 2, seed: 2026);
        var fill = F5gPopulationFillTests.Catalogue(homes, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(5400));
        var city = F5gPopulationFillTests.Start(state, market, fill);
        double buildMs = Stopwatch.GetElapsedTime(build).TotalMilliseconds;
        double boundary = 2 * 1440;
        long run = Stopwatch.GetTimestamp();
        while (!city.AdvanceTo(boundary - 1, 100000).CaughtUp) { }
        double dayMs = Stopwatch.GetElapsedTime(run).TotalMilliseconds;
        int seeded = state.Visitors.Count;
        var unitMs = new List<double>(); var unitBytes = new List<double>();
        while (state.Minute < boundary || city.PopulationFillPending)
        {
            long units = city.PopulationFillUnits, bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            city.AdvanceFrameSlice(boundary + 1, 1);
            double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (city.PopulationFillUnits != units) { unitMs.Add(ms); unitBytes.Add(allocated); }
        }
        var targets = city.PopulationFillTargets!.Value;
        var (commuters, pool) = F5gPopulationFillTests.Counts(state);
        long remaining = targets.VisitorCapacity - state.Visitors.Count;
        return new Dictionary<string, object>
        {
            ["arm"] = "capacity-boundary", ["residents"] = state.Citizens.Count, ["seededVisitors"] = seeded, ["commuters"] = commuters, ["pool"] = pool,
            ["visitorCapacity"] = targets.VisitorCapacity, ["commuterTarget"] = targets.CommuterTarget,
            ["persistedPersons"] = state.Citizens.Count + state.Visitors.Count, ["created"] = city.PopulationFillCreated,
            ["fillUnits"] = unitMs.Count, ["skipped"] = city.PopulationFillSkipped, ["unitMs"] = Stats(unitMs), ["unitAllocatedBytes"] = Stats(unitBytes),
            ["unitSumMs"] = Math.Round(unitMs.Sum(), 1),
            ["remainingVisitors"] = remaining, ["remainingBoundaries"] = (remaining + PopulationFillPolicy.MaximumPersonsPerBoundary - 1) / PopulationFillPolicy.MaximumPersonsPerBoundary,
            ["buildMs"] = Math.Round(buildMs, 1), ["dayToBoundaryMs"] = Math.Round(dayMs, 1)
        };
    }

    [Fact]
    public void MeasureReference() => MeasureShape("reference", 152, 1000L);

    [Fact]
    public void MeasureStress() => MeasureShape("stress-10k", 530, -1L);

    [Fact]
    public void MeasureCapacity()
    {
        string? path = Environment.GetEnvironmentVariable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        File.AppendAllText(path, JsonConvert.SerializeObject(Capacity()) + Environment.NewLine);
    }

    // Reference: 957 residents (152 homes) with a 1,000-commuter target; the pool takes its census target.
    // Stress: 3,339 residents (530 homes) filled to their whole visitor capacity, about 10,000 persisted persons.
    private static void MeasureShape(string name, int homes, long commuterTarget)
    {
        string? path = Environment.GetEnvironmentVariable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
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
            File.AppendAllText(path, JsonConvert.SerializeObject(row) + Environment.NewLine);
            hash ??= (string)row["stateHash"];
            Assert.Equal(hash, (string)row["stateHash"]);
            Assert.Equal(0L, (long)row["historicalClockMisses"]);
        }
    }
}
