using System;
using System.Text.Json;
using PersistentCity.Core;

namespace UnityEngine { public static class Time { public static float unscaledTime; } }
namespace PersistentCity.Runtime
{
    // Compile the real player-status formatter against the same native-clock doubles.
    public sealed partial class PopulationRuntime
    {
        private CitySimulator? simulation = null;
        private static double Now() => 0;
        public string StatusForTest() => AccountingStatusText();
        public string BoundaryForTest() => BoundaryAccountingText;
        private string failureForTest = "";
        private void Fail(string message) { failureForTest = message; NativeCityClock.Stop(); }
        public string StopForTest() { StopAccounting(); return failureForTest; }
    }
}
internal static class AccountingStatusBenchmark
{
    public static void Run()
    {
        (long Bytes, int Builds) Measure(bool everyCall)
        {
            var runtime = new PersistentCity.Runtime.PopulationRuntime();
            long allocatedStart = GC.GetAllocatedBytesForCurrentThread(); int builds = 0; string previous = "";
            for (int i = 0; i < 1200; i++)
            {
                UnityEngine.Time.unscaledTime = everyCall ? i : i / 120f;
                string current = runtime.StatusForTest();
                if (!ReferenceEquals(previous, current)) builds++;
                previous = current;
            }
            return (GC.GetAllocatedBytesForCurrentThread() - allocatedStart, builds);
        }
        Measure(true); Measure(false);
        var before = Measure(true); var after = Measure(false);
        if (after.Builds > 41 || after.Bytes >= before.Bytes) throw new Exception("Visible accounting text was not cached at 4 Hz");
        var baseline = new PersistentCity.Runtime.PopulationRuntime();
        string boundary = baseline.BoundaryForTest(); // Warm the accessor before measuring.
        long boundaryStart = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1200; i++)
            if (!ReferenceEquals(boundary, baseline.BoundaryForTest())) throw new Exception("Baseline text rebuilt");
        long boundaryBytes = GC.GetAllocatedBytesForCurrentThread() - boundaryStart;
        if (boundaryBytes != 0) throw new Exception("Quiet baseline text allocated");
        Console.WriteLine(JsonSerializer.Serialize(new { statusRequests = 1200, simulatedSeconds = 10,
            beforeBytes = before.Bytes, afterBytes = after.Bytes, beforeBuilds = before.Builds, afterBuilds = after.Builds,
            boundaryBytes, boundaryBuilds = 0 }));
    }
}
