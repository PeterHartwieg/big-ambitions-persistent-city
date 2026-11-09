using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit.Sdk;

namespace PersistentCity.Tests;

/// <summary>The shared rule for wall-clock budgets in tests (X3). A busy machine stretches wall-clock time by
/// keeping the test thread waiting for a core; a code regression makes the thread itself do more work. So:
/// <list type="number">
/// <item>Each attempt measures its load factor: wall-clock time over the test thread's own CPU time, never below 1.
/// A quiet machine gives about 1. A value counts as within budget when <c>value &lt; budget × load factor</c>.</item>
/// <item>A missed budget re-runs the whole measurement, at most <see cref="Attempts"/> times, and each check is judged
/// on its best attempt (lowest value ÷ load factor), because load comes and goes.</item>
/// </list>
/// A quiet run passes on the first attempt with factor about 1, so it catches exactly what the bare assertion caught.
/// A code regression costs CPU time as well as wall time, so the factor does not hide it. Timed code must run on the
/// calling thread and must not sleep or wait on I/O, or the factor overstates the load.
///
/// Use it for every Stopwatch value compared against a fixed budget. Inside the measurement, compare with
/// <see cref="TimingChecks.Under"/> instead of <c>Assert.True</c>. Deterministic values (allocation bytes, counts,
/// sizes, outcomes) keep their exact <c>Assert</c>: an assertion that throws inside the measurement fails at once
/// and is never retried. The measurement must build its own state, since it may run more than once.</summary>
internal static class TimingBudget
{
    /// <summary>At most this many runs of one measurement. A regression has to miss its budget on all of them.</summary>
    public const int Attempts = 5;

    /// <summary>Extra attempts taken by timing checks in this process, for the perf notes.</summary>
    public static int Retries => retries;
    private static int retries;

    /// <summary>Load factor of the most recent attempt on any thread, for the perf notes.</summary>
    public static double LastLoadFactor { get; private set; } = 1;

    /// <summary>Runs <paramref name="measure"/> until its timing checks all pass, at most <see cref="Attempts"/>
    /// times, and returns the number of runs. Fails if some budget is missed by its best value.</summary>
    public static int Run(Action<TimingChecks> measure, int attempts = Attempts) => Run(measure, attempts, LoadFactor);

    /// <summary><see cref="Run(Action{TimingChecks}, int)"/> with the load factor taken from
    /// <paramref name="loadFactor"/>(wall ms, thread CPU ms); TimingBudgetTests fixes it.</summary>
    internal static int Run(Action<TimingChecks> measure, int attempts, Func<double, double, double> loadFactor)
    {
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        // Per check: the attempt with the lowest load-adjusted value.
        var best = new Dictionary<string, (double Adjusted, double Value, double Factor, double Budget)>();
        for (int attempt = 1; ; attempt++)
        {
            var checks = new TimingChecks();
            long wall = Stopwatch.GetTimestamp();
            double cpu = ThreadCpu.Milliseconds();
            measure(checks);
            double factor = loadFactor((Stopwatch.GetTimestamp() - wall) * 1000.0 / Stopwatch.Frequency, ThreadCpu.Milliseconds() - cpu);
            LastLoadFactor = factor;
            foreach (var (label, value, budget) in checks.Entries)
            {
                double adjusted = value / factor;
                if (best.TryGetValue(label, out var known))
                {
                    if (known.Budget != budget) throw new InvalidOperationException($"Timing check '{label}' changed its budget between attempts.");
                    if (adjusted < known.Adjusted) best[label] = (adjusted, value, factor, budget);
                }
                else best[label] = (adjusted, value, factor, budget);
            }
            var missed = best.Where(b => !(b.Value.Adjusted < b.Value.Budget)).ToList();
            if (missed.Count == 0) return attempt;
            if (attempt >= attempts)
                throw new XunitException("Wall-clock budget missed on all " + attempt + " attempts (best attempt per check): " + string.Join("; ", missed.Select(m =>
                    string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.####} ms at load factor {2:0.00}, budget {3:0.####} ms", m.Key, m.Value.Value, m.Value.Factor, m.Value.Budget))));
            Interlocked.Increment(ref retries);
            Log(attempt, factor, missed.Select(m => m.Key));
            // Give a briefly busy machine a moment before measuring again.
            Thread.Sleep(20 * attempt);
        }
    }

    private static readonly object LogLock = new();

    // With PC_TIMING_LOG set to a file path, every retry appends one line, for the perf notes.
    private static void Log(int attempt, double factor, IEnumerable<string> missed)
    {
        string? path = Environment.GetEnvironmentVariable("PC_TIMING_LOG");
        if (string.IsNullOrEmpty(path)) return;
        lock (LogLock)
            System.IO.File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture, "retry after attempt {0}, load factor {1:0.00}: {2}{3}",
                attempt, factor, string.Join(", ", missed), Environment.NewLine));
    }

    /// <summary>Wall-clock time over thread CPU time, at least 1. With no thread clock, or no CPU time to judge by
    /// (under 10 µs), the factor is 1: the budget is not loosened.</summary>
    internal static double LoadFactor(double wallMs, double cpuMs)
        => double.IsNaN(cpuMs) || cpuMs < 0.01 ? 1 : Math.Max(1, wallMs / cpuMs);

    /// <summary>One timed value against one budget: shorthand for a measurement with a single check.</summary>
    public static int Under(string label, double budgetMs, Func<double> measureMs, int attempts = Attempts)
        => Run(t => t.Under(label, measureMs(), budgetMs), attempts);
}

/// <summary>The wall-clock checks of one measurement attempt. Values must stay below their budget (strictly), in
/// the same unit as the budget; milliseconds unless the label says otherwise.</summary>
internal sealed class TimingChecks
{
    private readonly List<(string Label, double Value, double Budget)> entries = new();
    internal IReadOnlyList<(string Label, double Value, double Budget)> Entries => entries;

    /// <summary>Records that <paramref name="value"/> should be below <paramref name="budget"/>. Labels must be
    /// unique within one attempt and stable across attempts.</summary>
    public void Under(string label, double value, double budget)
    {
        if (double.IsNaN(value)) throw new XunitException($"Timing check '{label}' measured NaN.");
        if (entries.Any(e => e.Label == label)) throw new InvalidOperationException($"Timing check '{label}' recorded twice in one attempt.");
        entries.Add((label, value, budget));
    }
}

/// <summary>CPU time the calling thread has run, in milliseconds; NaN where the platform clock is unavailable.</summary>
internal static class ThreadCpu
{
    public static double Milliseconds()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return Windows.Milliseconds();
            if (OperatingSystem.IsMacOS()) return Posix(16); // CLOCK_THREAD_CPUTIME_ID on Darwin
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

    // GetThreadTimes only moves once per scheduler tick (about 15.6 ms), so Windows counts the thread's cycles and
    // converts them with the cycle rate, measured once as the fastest cycles-per-millisecond of a few short spins.
    private static class Windows
    {
        private static readonly double CyclesPerMs = Rate();

        public static double Milliseconds()
        {
            QueryThreadCycleTime(GetCurrentThread(), out ulong cycles);
            return cycles / CyclesPerMs;
        }

        private static double Rate()
        {
            double best = 0;
            for (int i = 0; i < 5; i++)
            {
                IntPtr thread = GetCurrentThread();
                QueryThreadCycleTime(thread, out ulong c0);
                long t0 = Stopwatch.GetTimestamp();
                while ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency < 2) { }
                QueryThreadCycleTime(thread, out ulong c1);
                // Waiting for a core adds wall time but no cycles, so the fastest rate is the true one.
                best = Math.Max(best, (c1 - c0) / ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency));
            }
            return best;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);
    }
}
