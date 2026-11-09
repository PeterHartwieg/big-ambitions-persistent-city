using System;
using Xunit;

/// <summary>Wall-clock limits from the slices' perf targets. They fail whenever the machine is busy (parallel slices,
/// builds, reviewers), so the unit suite checks them only with PC_PERF_ASSERTS=1 on a quiet machine. The timed code,
/// its correctness assertions and allocation checks always run. Global namespace so every test file can use it.</summary>
internal static class PerfGate
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PC_PERF_ASSERTS") == "1";

    public static void Under(double milliseconds, double limitMilliseconds, string label = "")
    {
        if (Enabled) Assert.True(milliseconds < limitMilliseconds, $"{label} {milliseconds:0.0000} ms, limit {limitMilliseconds} ms".Trim());
    }
}
