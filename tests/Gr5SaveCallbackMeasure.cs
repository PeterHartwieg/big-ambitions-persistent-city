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

public sealed class Gr5SaveCallbackMeasure
{
    internal const int GameTrafficLinks = 13965;

    [Fact]
    public void MainThreadCapture()
    {
        string? output = PerfGate.Variable("PC_GR5_SAVE_CALLBACK_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        int samples = int.TryParse(Environment.GetEnvironmentVariable("PC_GR5_SAVE_CALLBACK_SAMPLES"), out int n) ? n : 20;
        foreach (string fixture in F7cMeasure.Fixtures(Environment.GetEnvironmentVariable("PC_F7C_FIXTURES") ?? "fresh-1000x3,representative-957x2"))
            File.AppendAllText(output!, JsonConvert.SerializeObject(Measure(fixture, samples)) + Environment.NewLine);
    }

    internal static CityState WithGameTraffic(string fixture)
    {
        var state = F7cFixtures.Build(fixture).State;
        var kernel = new TrafficKernel(Enumerable.Range(1, GameTrafficLinks).Select(id => new TrafficLinkDefinition
        {
            Id = id, Capacity = 1 + id % 12, FreeFlowMinutes = 0.05 + id % 7 * 0.01, HeadwayMinutes = 0.02,
            Signal = id % 5 == 0 ? new TrafficSignalSchedule { CycleMinutes = 1.5, GreenStartMinutes = 0.1, GreenDurationMinutes = 0.6 } : null,
        }), state.Minute);
        var original = state.SnapshotSynchronizer;
        state.SnapshotSynchronizer = () => { original?.Invoke(); state.Traffic = kernel.Capture(); };
        return state;
    }

    private static object Measure(string fixture, int samples)
    {
        var state = WithGameTraffic(fixture);
        var cost = new SaveCostRecorder();
        var total = new List<double>(); var synchronize = new List<double>(); var capture = new List<double>();
        var bytes = new List<long>();
        var steps = Enum.GetValues(typeof(SaveCostStep)).Cast<SaveCostStep>().Take(SaveCostRecorder.StepCount).ToArray();
        var sections = Enum.GetValues(typeof(SnapshotSection)).Cast<SnapshotSection>().ToArray();
        var stepMs = steps.ToDictionary(s => s, _ => new List<double>());
        var sectionMs = sections.ToDictionary(s => s, _ => new List<double>());
        int collections = 0; bool shared = false;
        for (int i = 0; i <= samples; i++)
        {
            int gen0 = GC.CollectionCount(0);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            cost.Begin();
            var snapshot = SaveCodec.Capture(state, cost);
            cost.End(true, "");
            long used = GC.GetAllocatedBytesForCurrentThread() - allocated;
            GC.KeepAlive(snapshot);
            if (i == 0) continue;
            collections += GC.CollectionCount(0) - gen0;
            shared = cost.SharedTraffic;
            total.Add(cost.TotalMilliseconds); synchronize.Add(cost.Milliseconds(SaveCostPhase.Synchronize));
            capture.Add(cost.Milliseconds(SaveCostPhase.Capture)); bytes.Add(used);
            foreach (var step in steps) stepMs[step].Add(cost.StepMilliseconds(step));
            foreach (var section in sections) sectionMs[section].Add(cost.CaptureSectionMilliseconds(section));
        }
        return new
        {
            fixture, trafficLinks = GameTrafficLinks, residents = state.Citizens.Count, visitors = state.Visitors.Count, visitorRoutines = state.VisitorRoutines.Count, samples,
            totalMs = F7cMeasure.Stats(total), synchronizeMs = F7cMeasure.Stats(synchronize), captureMs = F7cMeasure.Stats(capture),
            allocatedBytes = F7cMeasure.Median(bytes), gen0Collections = collections, sharedTraffic = shared,
            stepsMsP50 = stepMs.Where(p => p.Value.Any(v => v > 0)).ToDictionary(p => p.Key.ToString(), p => Math.Round(p.Value.OrderBy(v => v).ElementAt(p.Value.Count / 2), 3)),
            captureSectionsMsP50 = sectionMs.ToDictionary(p => p.Key.ToString(), p => Math.Round(p.Value.OrderBy(v => v).ElementAt(p.Value.Count / 2), 3)),
            evidence = "macOS arm64 .NET 10 Release offline, one dotnet process; Mono in game is slower; main-thread capture only (no observe/publish)",
        };
    }
}
