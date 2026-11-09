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
/// <item>Timed code runs between <see cref="TimingChecks.Start"/> and <see cref="TimingChecks.Stop"/>, and each
/// kept window is recorded under the check its sample feeds. Over that check's windows only, each attempt measures
/// the check's load factor: wall-clock time over the test thread's own CPU time, never below 1. Setup, warm-up,
/// discarded samples, other checks' windows, sorting and reporting do not count. A quiet machine gives about 1.
/// A value counts as within budget when <c>value &lt; budget × load factor</c>.</item>
/// <item>A missed budget re-runs the whole measurement, at most <see cref="Attempts"/> times, and each check is judged
/// on its best attempt (lowest value ÷ load factor), because load comes and goes.</item>
/// </list>
/// A quiet run passes on the first attempt with factor about 1, so it catches exactly what the bare assertion caught.
/// A code regression costs CPU time as well as wall time, so the factor does not hide it. Timed code must run on the
/// calling thread and must not sleep or wait on I/O, or the factor overstates the load. A measurement that marks no
/// window for a check (its times come from code that cannot see <see cref="TimingChecks"/>) gets factor 1: retries
/// only.
///
/// A check's factor is one average over its own samples. It discounts load that is spread over the samples, which is
/// what moves a p95, and a single stalled sample among thousands barely moves it, so a regression in the others still
/// shows. For the same reason a max check against a spike line is helped mainly by the retries.
///
/// Use it for every Stopwatch value compared against a fixed budget. Inside the measurement, compare with
/// <see cref="TimingChecks.Under"/> instead of <c>Assert.True</c>. Deterministic values (allocation bytes, counts,
/// sizes, outcomes) keep their exact <c>Assert</c>: an assertion that throws inside the measurement fails at once
/// and is never retried. The measurement must build its own state, since it may run more than once.
///
/// Budgets are judged only when <see cref="PerfGate.Enabled"/> (<c>PC_PERF_ASSERTS=1</c>, #186). Otherwise the
/// measurement runs once with all its deterministic asserts and no budget is checked.</summary>
internal static class TimingBudget
{
    /// <summary>At most this many runs of one measurement. A regression has to miss its budget on all of them.</summary>
    public const int Attempts = 5;

    /// <summary>Extra attempts taken by timing checks in this process, for the perf notes.</summary>
    public static int Retries => retries;
    private static int retries;

    /// <summary>Load factor of the most recent attempt on any thread, for the perf notes.</summary>
    public static double LastLoadFactor { get; private set; } = 1;

    /// <summary>True when budgets are judged: <c>PC_PERF_ASSERTS=1</c>, or inside <see cref="Enforce"/>.</summary>
    public static bool Enforced => PerfGate.Enabled || enforcedDepth > 0;
    [ThreadStatic] private static int enforcedDepth;

    /// <summary>Judges budgets on this thread until disposed, whatever <c>PC_PERF_ASSERTS</c> says; for
    /// TimingBudgetTests, which test the judging itself.</summary>
    internal static IDisposable Enforce() { enforcedDepth++; return new EnforceScope(); }
    private sealed class EnforceScope : IDisposable { public void Dispose() => enforcedDepth--; }

    /// <summary>Runs <paramref name="measure"/> until its timing checks all pass, at most <see cref="Attempts"/>
    /// times, and returns the number of runs. Fails if some budget is missed by its best value. When budgets are not
    /// <see cref="Enforced"/>, runs it once and judges nothing.</summary>
    public static int Run(Action<TimingChecks> measure, int attempts = Attempts)
    {
        if (Enforced) return Run(measure, attempts, LoadFactor);
        ThreadCpu.Milliseconds(); // the windows still read the clock; see the warm-up below
        measure(new TimingChecks());
        return 1;
    }

    /// <summary><see cref="Run(Action{TimingChecks}, int)"/> with the load factor taken from
    /// <paramref name="loadFactor"/>(timed wall ms, timed thread CPU ms), always judged; TimingBudgetTests fixes it.</summary>
    internal static int Run(Action<TimingChecks> measure, int attempts, Func<double, double, double> loadFactor)
    {
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        // The first thread-clock call in a process allocates; make it here, not inside a zero-allocation loop.
        ThreadCpu.Milliseconds();
        // Per check: the attempt with the lowest load-adjusted value.
        var best = new Dictionary<string, (double Adjusted, double Value, double Factor, double Budget)>();
        for (int attempt = 1; ; attempt++)
        {
            var checks = new TimingChecks();
            measure(checks);
            double highest = 1;
            foreach (var (label, value, budget) in checks.Entries)
            {
                double factor = checks.TryTimed(label, out double wallMs, out double cpuMs) ? loadFactor(wallMs, cpuMs) : 1;
                highest = Math.Max(highest, factor);
                double adjusted = value / factor;
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
            // Give a briefly busy machine a moment before measuring again.
            Thread.Sleep(20 * attempt);
        }
    }

    private static readonly object LogLock = new();

    // With PC_TIMING_LOG set to a file path, every retry appends one line, for the perf notes.
    private static void Log(int attempt, IEnumerable<(string Label, double Factor)> missed)
    {
        string? path = Environment.GetEnvironmentVariable("PC_TIMING_LOG");
        if (string.IsNullOrEmpty(path)) return;
        lock (LogLock)
            System.IO.File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture, "retry after attempt {0}: {1}{2}", attempt,
                string.Join(", ", missed.Select(m => string.Format(CultureInfo.InvariantCulture, "{0} (best at load factor {1:0.00})", m.Label, m.Factor))), Environment.NewLine));
    }

    /// <summary>Timed wall-clock time over timed thread CPU time, at least 1. With no thread clock, or less timed CPU
    /// time than the clock can judge (<see cref="ThreadCpu.MinimumMs"/>), the factor is 1: the budget is not
    /// loosened.</summary>
    internal static double LoadFactor(double wallMs, double cpuMs)
        => double.IsNaN(cpuMs) || cpuMs < ThreadCpu.MinimumMs ? 1 : Math.Max(1, wallMs / cpuMs);

    /// <summary>One timed piece of work against one budget: shorthand for a measurement with a single window and
    /// a single check.</summary>
    public static int Under(string label, double budgetMs, Action work, int attempts = Attempts)
        => Run(t => { var w = t.Start(); work(); t.Under(label, t.Stop(w, label), budgetMs); }, attempts);
}

/// <summary>The timed windows and wall-clock checks of one measurement attempt. Values must stay below their budget
/// (strictly), in the same unit as the budget; milliseconds unless the label says otherwise.</summary>
internal sealed class TimingChecks
{
    private readonly List<(string Label, double Value, double Budget)> entries = new();
    internal IReadOnlyList<(string Label, double Value, double Budget)> Entries => entries;
    // Kept windows per check, in fixed arrays so recording a sample never allocates inside a zero-allocation loop.
    private const int MaximumSeries = 16;
    private readonly string[] seriesLabels = new string[MaximumSeries];
    private readonly double[] seriesWallMs = new double[MaximumSeries], seriesCpuMs = new double[MaximumSeries];
    private int series;

    /// <summary>Opens a timed window. The thread clock is read before the timestamp, so its cost stays outside.</summary>
    public TimingWindow Start()
    {
        double cpu = ThreadCpu.Milliseconds();
        return new TimingWindow(Stopwatch.GetTimestamp(), cpu);
    }

    /// <summary>Closes a window and returns its wall-clock milliseconds. With <paramref name="check"/> the window is
    /// a kept sample of that check and goes into its load factor; without, it counts for nothing (warm-up, discarded
    /// samples). Pass a label built once outside the loop: the same string as the check's <see cref="Under"/>.</summary>
    public double Stop(TimingWindow window, string? check = null)
    {
        var sample = End(window);
        return check == null ? sample.Ms : Keep(check, sample);
    }

    /// <summary>Closes a window without deciding yet whether it is kept; see <see cref="Keep"/>.</summary>
    public TimedSample End(TimingWindow window)
    {
        double wall = (Stopwatch.GetTimestamp() - window.Timestamp) * 1000.0 / Stopwatch.Frequency;
        return new TimedSample(wall, ThreadCpu.Milliseconds() - window.CpuMs);
    }

    /// <summary>Records <paramref name="sample"/> as a kept sample of <paramref name="check"/> and returns its
    /// wall-clock milliseconds.</summary>
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
        return sample.Ms;
    }

    /// <summary>The kept wall and CPU time of <paramref name="check"/>, if any window was kept for it.</summary>
    internal bool TryTimed(string check, out double wallMs, out double cpuMs)
    {
        for (int i = 0; i < series; i++)
            if (string.Equals(seriesLabels[i], check, StringComparison.Ordinal)) { wallMs = seriesWallMs[i]; cpuMs = seriesCpuMs[i]; return true; }
        wallMs = cpuMs = 0;
        return false;
    }

    /// <summary>Records that <paramref name="value"/> should be below <paramref name="budget"/>. Labels must be
    /// unique within one attempt and stable across attempts.</summary>
    public void Under(string label, double value, double budget)
    {
        if (double.IsNaN(value)) throw new XunitException($"Timing check '{label}' measured NaN.");
        if (entries.Any(e => e.Label == label)) throw new InvalidOperationException($"Timing check '{label}' recorded twice in one attempt.");
        entries.Add((label, value, budget));
    }
}

/// <summary>An open timed window: its start timestamp and the thread's CPU milliseconds at that moment.</summary>
internal readonly record struct TimingWindow(long Timestamp, double CpuMs);

/// <summary>A closed window: its wall-clock and thread CPU milliseconds.</summary>
internal readonly record struct TimedSample(double Ms, double CpuMs);

/// <summary>CPU time the calling thread has run, in milliseconds; NaN where the platform clock is unavailable.</summary>
internal static class ThreadCpu
{
    /// <summary>The least timed CPU time a load factor is computed from. macOS and Linux count thread CPU time in
    /// nanoseconds, so 10 µs is enough. Windows charges thread time once per scheduler tick (about 15.6 ms), so
    /// short windows read 0 or a whole tick; summed over many windows the count is right on average, and 250 ms
    /// (16 ticks) keeps the error near a quarter. Below it Windows gets factor 1, retries only.</summary>
    public static readonly double MinimumMs = OperatingSystem.IsWindows() ? 250 : 0.01;

    public static double Milliseconds()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return Windows();
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

    // Kernel plus user time in 100 ns units. QueryThreadCycleTime is finer, but Microsoft documents that its cycles
    // must not be converted to elapsed time.
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
