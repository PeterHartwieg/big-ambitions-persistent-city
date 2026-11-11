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

/// <summary>F5h measurement harness (docs/perf/f5h.md), not a gate: it runs only when PC_F5H_MEASURE names an output file,
/// e.g. <c>PC_F5H_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5hMeasure</c>.
/// <c>PC_F5H_SIZES</c> picks sizes (default <c>reference,1914,10000,50000,100000</c>). Every city is a fresh F5g fixture
/// city in writer 34 with the fill attached, measured over one game day of the F1 frame loop
/// (<see cref="F5hIntegratedAcceptanceTests.Accounting"/>). Persons are persisted persons: residents, commuters and the
/// whole day-visitor pool, active or dormant.</summary>
public sealed class F5hMeasure
{
    private static string? Output => PerfGate.Variable("PC_F5H_MEASURE") is { Length: > 0 } path ? path : null;

    private static object Stats(List<double> values, int digits = 4)
    {
        if (values.Count == 0) return new { count = 0 };
        var s = values.OrderBy(v => v).ToList();
        return new { count = s.Count, p50 = Math.Round(s[s.Count / 2], digits), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], digits),
            max = Math.Round(s[^1], digits), atLeast5Ms = s.Count(v => v >= 5), atLeast30Ms = s.Count(v => v >= 30) };
    }

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];

    private static Dictionary<string, object> Row(string size, string arm, F5hIntegratedAcceptanceTests.Window w, double buildMs, int fps, bool skip)
    {
        // Frames the day takes against the frames at an unheld native clock (half a game minute per frame at 60 FPS).
        double nominal = skip ? 1440 / 120.0 : 1440 / (30.0 / fps);
        return new Dictionary<string, object>
        {
            ["size"] = size, ["arm"] = arm, ["personsAtStart"] = w.PersonsAtStart, ["personsAtEnd"] = w.PersonsAtEnd,
            ["frames"] = w.Frames, ["wallFactor"] = Math.Round(w.Frames / nominal, 3), ["held"] = w.Held, ["heldAtBoundary"] = w.HeldAtBoundary, ["maxLag"] = w.MaxLag,
            ["historicalClockMisses"] = w.HistoricalClockMisses, ["reloads"] = w.Reloads, ["fillReloads"] = w.FillReloads,
            ["fillUnits"] = w.FillUnits, ["fillCreated"] = w.FillCreated, ["postings"] = w.Postings,
            ["citizenUnits"] = w.CitizenUnits, ["foodContinuationUnits"] = w.FoodUnits,
            ["eventsPerGameHour"] = new { max = w.EventsPerHour.Max(), mean = Math.Round(w.EventsPerHour.Average(), 1), total = w.EventsPerHour.Sum() }, ["plannedEvents"] = w.PlannedEvents,
            ["queueOccupancy"] = new { persons = w.PeakPersonQueue, meals = w.PeakMealQueue },
            ["dueBacklog"] = new { peak = w.PeakDueBacklog, framesWithBacklog = w.FramesWithBacklog, longestDrainFrames = w.LongestBacklogFrames }, ["peakPlanningFrames"] = w.PeakPlanningFrames,
            ["planning"] = new { requests = w.PlanningRequests, jobs = w.Jobs, paths = w.Paths, deferred = w.Deferred, maxPathsInFrame = w.MaxPathsInFrame, maxFramesWaited = w.MaxFramesWaited },
            ["portal"] = new { hits = w.PortalHits, misses = w.PortalMisses },
            ["frameMs"] = Stats(w.Frame), ["fillMs"] = Stats(w.FillMs), ["postingMs"] = Stats(w.PostingMs), ["planningMs"] = Stats(w.PlanningMs), ["eventMs"] = Stats(w.EventMs),
            ["fillUnitMs"] = Stats(w.FillUnitMs), ["fillUnitBytes"] = Stats(w.FillUnitBytes, 0), ["postingUnitMs"] = Stats(w.PostingUnitMs), ["citizenUnitMs"] = Stats(w.CitizenUnitMs), ["foodUnitMs"] = Stats(w.FoodUnitMs),
            ["spikes"] = new { atLeast5Ms = w.Spikes.Count, withGc = w.Spikes.Count(x => x.Gc >= 0), withGen2 = w.Spikes.Count(x => x.Gc == 2),
                byComponent = w.Spikes.GroupBy(x => x.Component).ToDictionary(g => g.Key, g => g.Count()),
                top = w.Spikes.OrderByDescending(x => x.Ms).Take(8).Select(x => $"{x.Ms:F1} ms {x.Component} @{x.Minute:F0} gc{x.Gc}").ToArray(),
                withoutGc = w.Spikes.Where(x => x.Gc < 0).Select(x => $"{x.Ms:F1} ms {x.Component} @{x.Minute:F0}").ToArray() },
            ["orders"] = w.Orders.Length, ["stateHash"] = Sha(w.Snapshot + string.Join(",", w.Orders)), ["buildMs"] = Math.Round(buildMs)
        };
    }

    private static long Retained(string payload)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseline = GC.GetTotalMemory(true);
        var kept = SaveCodec.Read(payload);
        long bytes = GC.GetTotalMemory(true) - baseline;
        GC.KeepAlive(kept);
        return bytes;
    }

    private static Dictionary<string, object> Saved(string size, string payload, int samples)
    {
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var state = read.State!;
        var records = SaveCodec.CountRecords(state, payload);
        var writes = new List<double>(); var reads = new List<double>();
        for (int i = 0; i < samples; i++)
        {
            long t = Stopwatch.GetTimestamp(); string again = SaveCodec.Write(state); writes.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            Assert.Equal(payload, again);
            t = Stopwatch.GetTimestamp(); var back = SaveCodec.Read(payload); reads.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            Assert.Equal(SaveReadStatus.Loaded, back.Status);
        }
        var (commuters, pool) = F5gPopulationFillTests.Counts(state);
        return new Dictionary<string, object>
        {
            ["size"] = size, ["saved"] = true, ["residents"] = state.Citizens.Count, ["commuters"] = commuters, ["pool"] = pool,
            ["persons"] = state.Citizens.Count + state.Visitors.Count, ["characters"] = payload.Length,
            ["utf8Bytes"] = System.Text.Encoding.UTF8.GetByteCount(payload), ["capFraction"] = Math.Round(payload.Length / (double)SaveCodec.MaxInputCharacters, 4),
            ["records"] = new { records.Streaming, records.Semantic, cap = SaveCodec.MaxEmbeddedRecords }, ["retainedBytes"] = Retained(payload),
            ["writeMs"] = Stats(writes, 1), ["readMs"] = Stats(reads, 1), ["samples"] = samples
        };
    }

    /// <summary>One measured shape: homes, seeding factor, fill catalogue and the day whose 06:00 starts the window.</summary>
    private sealed record Size(string Name, Func<F5hIntegratedAcceptanceTests.Shape> Shape, int Factor, int Day);

    // Residents are floor(6.3 × homes) (seven beds, 90% fill). Factor-1 sizes are filled toward their census targets before
    // the window; factor-2 sizes start fresh with R seeded commuters, and the window's first boundary creates 1,024 pool
    // members. Every shape's saved places are its census businesses (review Astra-r1-1).
    private static readonly Size[] Sizes =
    {
        new("reference", F5hIntegratedAcceptanceTests.ReferenceShape, 1, 3),
        new("1914", F5hIntegratedAcceptanceTests.ReferenceShape, 2, 1),
        new("10000", F5hIntegratedAcceptanceTests.StressShape, 1, 8),
        new("50000", () => F5hIntegratedAcceptanceTests.ScaledShape(3887), 2, 1),
        new("100000", () => F5hIntegratedAcceptanceTests.ScaledShape(7855), 2, 1),
    };

    /// <summary>Section 6 matrix: per size, one day at 60 FPS with Runtime's 60 FPS frame shape (128 units, 16 citizen events, 2 ms; <see cref="PopulationFrameBudget"/>), then the saved city's bytes, records,
    /// retained memory and offline write/read time.</summary>
    [Fact]
    public void MeasureSizes()
    {
        string? path = Output; if (path == null) return;
        string[] only = (Environment.GetEnvironmentVariable("PC_F5H_SIZES") ?? "reference,1914,10000,50000,100000").Split(',');
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_F5H_SAMPLES"), out int n) ? n : 3;
        foreach (var size in Sizes.Where(s => only.Contains(s.Name)))
        {
            long started = Stopwatch.GetTimestamp();
            var shape = size.Shape(); var fill = shape.Catalogue;
            string payload = F5hIntegratedAcceptanceTests.Prefilled(shape, size.Day, size.Factor);
            double buildMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var caps = new PopulationFrameBudget(1 / 60.0, false);
            var w = F5hIntegratedAcceptanceTests.Accounting(payload, fill, 60, caps.Units, caps.Milliseconds, reload: false, skip: false, citizenLimit: caps.CitizenEvents);
            Assert.Equal(0, w.HistoricalClockMisses);
            File.AppendAllText(path, JsonConvert.SerializeObject(Row(size.Name, $"60 FPS Runtime caps {caps.Units} units / {caps.CitizenEvents} citizen / {caps.Milliseconds} ms", w, buildMs, 60, false)) + Environment.NewLine);
            File.AppendAllText(path, JsonConvert.SerializeObject(Saved(size.Name, w.Snapshot, samples)) + Environment.NewLine);
        }
    }

    /// <summary>The full F1 matrix at about 10,000 persons: from 06:00 on day 7 across the seventh boundary, which completes the
    /// fill. Every arm, with and without saves, must equal the 60/128/2 reference in save and orders.</summary>
    [Fact]
    public void MeasureStressMatrix() => Matrix("10000", F5hIntegratedAcceptanceTests.StressShape, 7);

    /// <summary>The same matrix at the declared load, with timings (the ordinary test asserts equality only).</summary>
    [Fact]
    public void MeasureReferenceMatrix() => Matrix("reference", F5hIntegratedAcceptanceTests.ReferenceShape, 2);

    private static void Matrix(string name, Func<F5hIntegratedAcceptanceTests.Shape> make, int day)
    {
        string? path = Output; if (path == null) return;
        var shape = make(); var fill = shape.Catalogue;
        long started = Stopwatch.GetTimestamp();
        string payload = F5hIntegratedAcceptanceTests.Prefilled(shape, day);
        double buildMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var reference = F5hIntegratedAcceptanceTests.Accounting(payload, fill, 60, 128, 2, reload: false, skip: false);
        File.AppendAllText(path, JsonConvert.SerializeObject(Row(name, "reference 60/128/2", reference, buildMs, 60, false)) + Environment.NewLine);
        foreach (var arm in F5hIntegratedAcceptanceTests.Arms())
        {
            int fps = (int)arm[0], budget = (int)arm[1]; double ms = (double)arm[2]; bool skip = (bool)arm[3];
            foreach (bool reload in new[] { false, true })
            {
                var w = F5hIntegratedAcceptanceTests.Accounting(payload, fill, fps, budget, ms, reload, skip);
                File.AppendAllText(path, JsonConvert.SerializeObject(Row(name, $"{fps}/{budget}/{ms}{(skip ? " skip" : "")}{(reload ? " reload" : "")}", w, 0, fps, skip)) + Environment.NewLine);
                Assert.Equal(reference.Snapshot, w.Snapshot);
                Assert.Equal(reference.Orders, w.Orders);
                Assert.Equal(0, w.HistoricalClockMisses);
                Assert.InRange(w.MaxLag, 0, AccountingClock.MaximumLagMinutes);
                Assert.InRange(w.MaxPathsInFrame, 0, F5hIntegratedAcceptanceTests.AdmittedPortalPlanner.PathsPerFrame);
            }
        }
    }
}
