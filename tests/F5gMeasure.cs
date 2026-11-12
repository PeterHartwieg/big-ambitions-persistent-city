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
        double end = 2 * 1440;
        while (true)
        {
            long created = city.PopulationFillCreated;
            if (arm.Skip) { while (!city.AdvanceTo(end + 1, 1_000_000).CaughtUp) { } }
            else
            {
                while (!city.AdvanceTo(end - 1, 1_000_000).CaughtUp) { }
                Drive(end + 1, false);
            }
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
            ["peakMinuteFramesAt16PathsPerFrame"] = (peakMinuteRequests + 15) / 16,
            ["stateHash"] = Sha(SaveCodec.Write(state) + string.Join(",", market.Orders))
        };
    }

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
        string? path = PerfGate.Variable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        File.AppendAllText(path, JsonConvert.SerializeObject(Capacity()) + Environment.NewLine);
    }

    private static void MeasureShape(string name, int homes, long commuterTarget)
    {
        string? path = PerfGate.Variable("PC_F5G_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        long target = commuterTarget;
        var (probe, _) = F5gPopulationFillTests.City(homes, 1, seed: 2026);
        if (target < 0)
        {
            var t = VisitorTargets.Compute(0, 0, probe.Citizens.Count, probe.Portals.Count);
            target = t.VisitorCapacity - t.PoolDemand;
        }
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
