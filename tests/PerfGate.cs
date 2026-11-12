using System;
using Xunit;

internal static class PerfGate
{
    public static bool Enabled => Environment.GetEnvironmentVariable("PC_PERF_ASSERTS") == "1";

    public static string? Variable(string name) => Enabled ? Environment.GetEnvironmentVariable(name) : null;

    public static void Under(double milliseconds, double limitMilliseconds, string label = "")
    {
        if (Enabled) Assert.True(milliseconds < limitMilliseconds, $"{label} {milliseconds:0.0000} ms, limit {limitMilliseconds} ms".Trim());
    }
}
