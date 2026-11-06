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
        // Native cleanup surfaces are doubles; failure/reset decisions compile from Runtime.
        private bool failed, physicalFailed, inspectorFailed, clockRetimingRoutes, initializingHadPayload;
        private string status = "", economyText = "";
        private float nextStart, nextEconomyText;
        private object? saveIdentity, initializingSave, saveSession, routes, stopEnvironment;
        private string? initializingPayload;
        private System.Threading.Tasks.Task<int>? initialization;
        private TestDisposable? physical;
        private readonly TestInspector inspector = new();
        private readonly TestLogger log = new();
        private void Detach() { physical?.Dispose(); physical = null; NativeCityClock.Stop(); }
        public string StopForTest() { StopAccounting(); return status; }
        public bool PrepareForTest() => PrepareAccountingSaveChange();
        public bool CanStartForTest() => CanStartAccountingSave();
        public bool FailedForTest => failed;
        public void FailForTest() => Fail("non-accounting failure");
        public void SeedOldSaveForTest(bool beforeAttachment = false)
        {
            saveIdentity = beforeAttachment ? null : SaveGameManager.Current;
            initializingSave = SaveGameManager.Current; initializingPayload = "old payload";
            initializingHadPayload = true;
            initialization = System.Threading.Tasks.Task.FromResult(1);
            saveSession = routes = stopEnvironment = new object(); physical = new TestDisposable();
            physicalFailed = inspectorFailed = clockRetimingRoutes = true;
            economyText = "old economy"; nextStart = nextEconomyText = nextAccountingText = 100;
        }
        public bool OldSaveClearedForTest => saveIdentity == null && initializingSave == null
            && initializingPayload == null && !initializingHadPayload && initialization == null && simulation == null
            && saveSession == null && routes == null && stopEnvironment == null && physical == null && !physicalFailed
            && !inspectorFailed && !clockRetimingRoutes && economyText.Length == 0 && accountingText.Length == 0
            && nextStart == 0 && nextEconomyText == 0 && nextAccountingText == 0 && inspector.Resets == 1;
    }
    internal sealed class TestDisposable { public void Dispose() { } }
    internal sealed class TestInspector { internal int Resets; public void Reset() => Resets++; }
    internal sealed class TestLogger { public void Warn(string message) { } }
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
