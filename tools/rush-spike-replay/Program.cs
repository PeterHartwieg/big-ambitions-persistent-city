using System;
using System.Text.Json;
using PersistentCity.Tests;

// Issue #128 replay. SourceRoot=<dir> compiles the same fixture against another Core/Persistence tree
// (for example an extracted baseline), so before/after numbers and hashes come from one fixture.
static class Program
{
    static void Main(string[] args)
    {
        var windows = new (string Name, double From, double To)[]
        {
            ("evening day 3 17:30-20:30", 2 * 1440 + 1050, 2 * 1440 + 1230),
            ("morning day 3 07:00-09:30", 2 * 1440 + 420, 2 * 1440 + 570),
        };
        foreach (var window in windows)
        {
            if (args.Length > 0 && !window.Name.StartsWith(args[0], StringComparison.Ordinal)) continue;
            var result = RushSpikeReplay.Run(window.From, window.To);
            Console.WriteLine(JsonSerializer.Serialize(new { window = window.Name, result.Frames, result.Units, result.CitizenEvents,
                result.TrafficEvents, frameMs = new { p50 = result.FrameP50, p95 = result.FrameP95, p99 = result.FrameP99, max = result.FrameMax,
                    atMinute = result.MaxFrameMinute, collectionsInMaxFrame = result.CollectionsInMaxFrame, spikesAtLeast30 = result.FrameSpikes30 },
                unitMs = new { max = result.UnitMax, kind = result.MaxUnitKind, atMinute = result.MaxUnitMinute, spikesAtLeast30 = result.UnitSpikes30 },
                result.SyncMax, result.MaxUnitsInFrame, result.MaxStopSeamCallsInUnit, result.MaxRouteCallsInUnit, result.PeakVehicles,
                result.PeakTravelling, result.StopSeamCalls, result.RouteCalls, result.Purchases, result.CompletedJourneys, result.Stops, result.SaveSha256,
                uninterruptedSha256 = RushSpikeReplay.RunUninterrupted(window.From, window.To) }));
        }
    }
}
