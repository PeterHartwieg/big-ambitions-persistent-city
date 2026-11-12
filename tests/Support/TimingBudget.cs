using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit.Sdk;

namespace PersistentCity.Tests;

internal static class TimingBudget
{
    public const int Attempts = 5;

    public static int Retries => retries;
    private static int retries;

    public static double LastLoadFactor { get; private set; } = 1;

    public static bool Enforced => PerfGate.Enabled || enforcedDepth > 0;
    [ThreadStatic] private static int enforcedDepth;

    internal static IDisposable Enforce() { enforcedDepth++; return new EnforceScope(); }
    private sealed class EnforceScope : IDisposable { public void Dispose() => enforcedDepth--; }

    public static int Run(Action<TimingChecks> measure, int attempts = Attempts)
    {
        if (Enforced) return Run(measure, attempts, LoadFactor);
        ThreadCpu.Milliseconds();
        measure(new TimingChecks());
        return 1;
    }

    internal static int Run(Action<TimingChecks> measure, int attempts, Func<double, double, double> loadFactor)
    {
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        ThreadCpu.Milliseconds();
        var best = new Dictionary<string, (double Adjusted, double Value, double Factor, double Budget)>();
        for (int attempt = 1; ; attempt++)
        {
            var checks = new TimingChecks();
            measure(checks);
            double highest = 1;
            foreach (var (label, value, budget, isMax) in checks.Entries)
            {
                double factor = checks.TryTimed(label, out double wallMs, out double cpuMs, out double largestSampleCpuMs) ? loadFactor(wallMs, cpuMs) : 1;
                highest = Math.Max(highest, factor);
                double adjusted = value / factor;
                if (isMax) adjusted = Math.Max(adjusted, largestSampleCpuMs);
                if (best.TryGetValue(label, out var known))
                {
                    if (known.Budget != budget) throw new InvalidOperationException($"Timing check '{label}' changed its budget between attempts.");
                    if (adjusted < known.Adjusted) best[label] = (adjusted, value, factor, budget);
                }
                else best[label] = (adjusted, value, factor, budget);
            }
            LastLoadFactor = highest;
            var missed = best.Where(b => !(b.Value.Adjusted < b.Value.Budget)).ToList();
            if (missed.Count == 0) return attempt;
            if (attempt >= attempts)
                throw new XunitException("Wall-clock budget missed on all " + attempt + " attempts (best attempt per check): " + string.Join("; ", missed.Select(m =>
                    string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.####} ms at load factor {2:0.00}, budget {3:0.####} ms", m.Key, m.Value.Value, m.Value.Factor, m.Value.Budget))));
            Interlocked.Increment(ref retries);
            Log(attempt, missed.Select(m => (m.Key, m.Value.Factor)));
            Thread.Sleep(20 * attempt);
        }
    }

    private static readonly object LogLock = new();

    private static void Log(int attempt, IEnumerable<(string Label, double Factor)> missed)
    {
        string? path = Environment.GetEnvironmentVariable("PC_TIMING_LOG");
        if (string.IsNullOrEmpty(path)) return;
        lock (LogLock)
            System.IO.File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture, "retry after attempt {0}: {1}{2}", attempt,
                string.Join(", ", missed.Select(m => string.Format(CultureInfo.InvariantCulture, "{0} (best at load factor {1:0.00})", m.Label, m.Factor))), Environment.NewLine));
    }

    internal static double LoadFactor(double wallMs, double cpuMs)
        => double.IsNaN(cpuMs) || cpuMs < ThreadCpu.MinimumMs ? 1 : Math.Max(1, wallMs / cpuMs);

    public static int Under(string label, double budgetMs, Action work, int attempts = Attempts)
        => Run(t => { var w = t.Start(); work(); t.Under(label, t.Stop(w, label), budgetMs); }, attempts);
}

internal sealed class TimingChecks
{
    private readonly List<(string Label, double Value, double Budget, bool IsMax)> entries = new();
    internal IReadOnlyList<(string Label, double Value, double Budget, bool IsMax)> Entries => entries;
    private const int MaximumSeries = 16;
    private readonly string[] seriesLabels = new string[MaximumSeries];
    private readonly double[] seriesWallMs = new double[MaximumSeries], seriesCpuMs = new double[MaximumSeries], seriesLargestCpuMs = new double[MaximumSeries];
    private int series;

    public TimingWindow Start()
    {
        double cpu = ThreadCpu.Milliseconds();
        return new TimingWindow(Stopwatch.GetTimestamp(), cpu);
    }

    public double Stop(TimingWindow window, string? check = null)
    {
        var sample = End(window);
        return check == null ? sample.Ms : Keep(check, sample);
    }

    public TimedSample End(TimingWindow window)
    {
        double wall = (Stopwatch.GetTimestamp() - window.Timestamp) * 1000.0 / Stopwatch.Frequency;
        return new TimedSample(wall, ThreadCpu.Milliseconds() - window.CpuMs);
    }

    public double Keep(string check, TimedSample sample)
    {
        int i = 0;
        while (i < series && !string.Equals(seriesLabels[i], check, StringComparison.Ordinal)) i++;
        if (i == series)
        {
            if (series == MaximumSeries) throw new InvalidOperationException($"More than {MaximumSeries} timed checks in one attempt.");
            seriesLabels[series++] = check;
        }
        seriesWallMs[i] += sample.Ms;
        seriesCpuMs[i] += sample.CpuMs;
        seriesLargestCpuMs[i] = Math.Max(seriesLargestCpuMs[i], Math.Min(sample.CpuMs, sample.Ms));
        return sample.Ms;
    }

    internal bool TryTimed(string check, out double wallMs, out double cpuMs, out double largestSampleCpuMs)
    {
        for (int i = 0; i < series; i++)
            if (string.Equals(seriesLabels[i], check, StringComparison.Ordinal))
            {
                wallMs = seriesWallMs[i]; cpuMs = seriesCpuMs[i]; largestSampleCpuMs = double.IsNaN(seriesLargestCpuMs[i]) ? 0 : seriesLargestCpuMs[i];
                return true;
            }
        wallMs = cpuMs = largestSampleCpuMs = 0;
        return false;
    }

    public void Under(string label, double value, double budget) => Add(label, value, budget, false);

    public void MaxUnder(string label, double value, double budget) => Add(label, value, budget, true);

    private void Add(string label, double value, double budget, bool isMax)
    {
        if (double.IsNaN(value)) throw new XunitException($"Timing check '{label}' measured NaN.");
        if (entries.Any(e => e.Label == label)) throw new InvalidOperationException($"Timing check '{label}' recorded twice in one attempt.");
        entries.Add((label, value, budget, isMax));
    }
}

internal readonly record struct TimingWindow(long Timestamp, double CpuMs);

internal readonly record struct TimedSample(double Ms, double CpuMs);

internal static class ThreadCpu
{
    public static readonly double MinimumMs = OperatingSystem.IsWindows() ? 250 : 0.01;

    public static double Milliseconds()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return Windows();
            if (OperatingSystem.IsMacOS()) return Posix(16);
            if (OperatingSystem.IsLinux()) return Posix(3);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        return double.NaN;
    }

    private static double Posix(int clock)
    {
        if (clock_gettime(clock, out var ts) != 0) return double.NaN;
        return ts.Seconds * 1000.0 + ts.Nanoseconds / 1e6;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec { public long Seconds; public long Nanoseconds; }

    [DllImport("libc", SetLastError = true)]
    private static extern int clock_gettime(int clock, out Timespec time);

    private static double Windows()
    {
        if (!GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user)) return double.NaN;
        return (kernel + user) / 10_000.0;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);
}
