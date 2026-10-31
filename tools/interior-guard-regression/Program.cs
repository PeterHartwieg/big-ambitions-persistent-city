using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using PersistentCity.Runtime;

static class Program
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        var settings = new PrototypeSettings { InteriorCityView = "on" };
        NativeInteriorGuard.Start(settings);
        Require(!NativeInteriorGuard.Verdict.Detected && Harmony.Reads == 0, "absent + no owner metadata scan");
        settings.ThirdPartyInteriorHarmonyOwnerIds = new[] { "persistentcity.harmless.fixture" };
        NativeInteriorGuard.Start(settings);
        Require(!NativeInteriorGuard.Verdict.Detected && Harmony.Reads == 2, "both vanilla targets inspected");
        Harmony.Owners = settings.ThirdPartyInteriorHarmonyOwnerIds;
        NativeInteriorGuard.Start(settings);
        Require(NativeInteriorGuard.Verdict.Detected, "owner match");
        Harmony.Throw = true;
        NativeInteriorGuard.Start(settings);
        Require(NativeInteriorGuard.Verdict.Detected && NativeInteriorGuard.Verdict.Reason.Contains("exception"), "metadata failure closes");
        Harmony.Throw = false;
        settings = new PrototypeSettings { InteriorCityView = "on" };
        for (int i = 0; i < 50; i++) NativeInteriorGuard.Start(settings);
        const int samples = 501;
        var times = new double[samples];
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; i++)
        {
            long start = Stopwatch.GetTimestamp();
            NativeInteriorGuard.Start(settings);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        bytes = (GC.GetAllocatedBytesForCurrentThread() - bytes) / samples;
        Array.Sort(times);
        int assemblies = AppDomain.CurrentDomain.GetAssemblies().Length;
        int spikes = times.Count(t => t >= 30);
        NativeInteriorCityView.Configured = NativeInteriorGuard.Verdict.AllowsView(true);
        var bridge = new PopulationRuntime();
        Require(bridge.InteriorGuardDiagnostics.Contains("not detected"), "read-only bridge verdict");

        // A new assembly after evaluation cannot alter the session verdict, even through dumps.
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TrueCity.PersistentCityHarmlessStandIn"), AssemblyBuilderAccess.Run);
        int metadataReads = Harmony.Reads;
        for (int i = 0; i < 100; i++) Require(bridge.InteriorGuardDiagnostics.Contains("not detected"), "dump must not rescan");
        for (int i = 0; i < 10000; i++) _ = NativeInteriorGuard.Verdict.AllowsView(true);
        bytes = Math.Max(0, bytes);
        long frameBytes = GC.GetAllocatedBytesForCurrentThread();
        long frameStart = Stopwatch.GetTimestamp();
        bool allAllowed = true;
        for (int i = 0; i < 1000000; i++) allAllowed &= NativeInteriorGuard.Verdict.AllowsView(true);
        double frameMs = Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds / 1000000;
        frameBytes = GC.GetAllocatedBytesForCurrentThread() - frameBytes;
        Require(allAllowed && frameBytes == 0 && Harmony.Reads == metadataReads, "cached reads allocate/scan zero");
        NativeInteriorGuard.Stop(); NativeInteriorGuard.Start(settings);
        Require(NativeInteriorGuard.Verdict.Detected && !NativeInteriorGuard.Verdict.AllowsView(true), "new load detects own stand-in");
        NativeInteriorCityView.Configured = NativeInteriorGuard.Verdict.AllowsView(true);
        Require(bridge.InteriorGuardDiagnostics.Contains("effective view=off"), "forced off dump");
        settings.ThirdPartyInteriorGuard = "off"; NativeInteriorGuard.Start(settings);
        Require(NativeInteriorGuard.Verdict.AllowsView(true), "off bypasses detection only");
        Console.WriteLine("PASS actual Runtime adapter: assembly/owner/error/off, cached session and on-demand bridge; own emitted stand-in only.");
        Console.WriteLine(JsonSerializer.Serialize(new { samples, assemblies, scanP50Ms = times[samples / 2],
            scanP95Ms = times[(int)(samples * .95)], scanMaxMs = times[^1], scanAllocatedBytes = bytes,
            spikesAtLeast30Ms = spikes, cachedReads = 1000000, cachedMeanMs = frameMs, cachedAllocatedBytes = frameBytes,
            perFrameDetectionCalls = 0 }));
    }
}
