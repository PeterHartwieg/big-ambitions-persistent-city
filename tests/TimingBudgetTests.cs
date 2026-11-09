using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace PersistentCity.Tests;

// The load tests saturate every core, so they run alone, after the parallel collections.
[CollectionDefinition(nameof(TimingBudgetTests), DisableParallelization = true)]
public sealed class TimingBudgetCollection { }

[Collection(nameof(TimingBudgetTests))]
public sealed class TimingBudgetTests
{
    private readonly ITestOutputHelper output;
    public TimingBudgetTests(ITestOutputHelper output) => this.output = output;

    // The semantic tests fix the load factor so they do not depend on how busy the machine is.
    private static int Run(Action<TimingChecks> measure, int attempts = TimingBudget.Attempts, double factor = 1)
        => TimingBudget.Run(t =>
        {
            // One empty kept window per label these tests use, so the fixed factor applies to each check.
            foreach (var label in new[] { "value", "a", "b" }) t.Keep(label, default);
            measure(t);
        }, attempts, (_, _) => factor);

    [Fact]
    public void QuietPassTakesOneRun()
    {
        int runs = 0;
        Assert.Equal(1, Run(t => { runs++; t.Under("value", 1, 2); }));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void MissedBudgetIsMeasuredAgainAndPassesOnABetterRun()
    {
        var values = new Queue<double>(new double[] { 9, 7, 1, 50 });
        Assert.Equal(3, Run(t => t.Under("value", values.Dequeue(), 2)));
        Assert.Single(values);
    }

    [Fact]
    public void EachCheckIsJudgedOnItsBestValue()
    {
        // No single run is clean, but each budget is met by its best value.
        var runs = new[] { (a: 9.0, b: 1.0), (a: 1.0, b: 9.0) };
        int run = 0;
        Assert.Equal(2, Run(t => { var (a, b) = runs[run++]; t.Under("a", a, 2); t.Under("b", b, 2); }));
    }

    [Fact]
    public void ConsistentMissFailsAfterTheLastAttempt()
    {
        int runs = 0;
        var error = Assert.Throws<XunitException>(() => Run(t => { runs++; t.Under("value", 3 + runs, 2); }, attempts: 3));
        Assert.Equal(3, runs);
        Assert.Contains("value: 4 ms at load factor 1.00, budget 2 ms", error.Message);
    }

    [Fact]
    public void LoadFactorScalesTheBudget()
    {
        // 9 ms measured while the thread had a third of the wall-clock time is 3 ms of its own time.
        Assert.Equal(1, Run(t => t.Under("value", 9, 4), factor: 3));
        Assert.Throws<XunitException>(() => Run(t => t.Under("value", 9, 2), attempts: 2, factor: 3));
        Assert.Equal(3, TimingBudget.LoadFactor(3000, 1000));
        Assert.Equal(1, TimingBudget.LoadFactor(500, 1000));
        Assert.Equal(1, TimingBudget.LoadFactor(5, double.NaN));
        // Too little timed CPU time to judge by: no discount.
        Assert.Equal(1, TimingBudget.LoadFactor(ThreadCpu.MinimumMs * 1.5, ThreadCpu.MinimumMs / 2));
    }

    [Fact]
    public void MeasurementWithoutATimedWindowGetsNoDiscount()
    {
        // The factor comes from the check's own kept windows; with none, a busy attempt is judged on retries alone.
        Assert.Throws<XunitException>(() => TimingBudget.Run(t => t.Under("value", 9, 4), 2, (_, _) => 3));
        Assert.Equal(1, TimingBudget.Run(t => { t.Stop(t.Start(), "value"); t.Under("value", 9, 4); }, 2, (_, _) => 3));
        // A window kept for another check, or not kept at all (warm-up), does not count for this one.
        Assert.Throws<XunitException>(() => TimingBudget.Run(t => { t.Stop(t.Start(), "other"); t.Stop(t.Start()); t.Under("value", 9, 4); }, 2, (_, _) => 3));
    }

    [Fact]
    public void DelayOutsideTheTimedWindowDoesNotLoosenTheBudget()
    {
        // Review round 1: a 100 ms pause in setup, then about 20 ms of timed CPU work against 5 ms. Setup is not
        // timed, so the factor stays near 1 and the work misses on every attempt.
        long work = SpinFor(20);
        var error = Assert.Throws<XunitException>(() => TimingBudget.Run(t =>
        {
            Thread.Sleep(100);
            var w = t.Start();
            Spin(work);
            t.Under("work", t.Stop(w, "work"), 5);
        }, attempts: 2));
        Assert.Contains("work:", error.Message);
    }

    [Fact]
    public void LoadInOneCheckOrAWarmUpDoesNotDiscountAnother()
    {
        // Review round 2: a 100 ms pause inside another check's window and inside a warm-up window, then about 20 ms
        // of timed CPU work against 5 ms. Only the work's own window sets its factor, so it misses on every attempt.
        long work = SpinFor(20);
        var error = Assert.Throws<XunitException>(() => TimingBudget.Run(t =>
        {
            var warm = t.Start(); Thread.Sleep(100); t.Stop(warm);
            var other = t.Start(); Thread.Sleep(100); Spin(work / 20); t.Under("other", t.Stop(other, "other"), 1000);
            var w = t.Start();
            Spin(work);
            t.Under("work", t.Stop(w, "work"), 5);
        }, attempts: 2));
        Assert.Contains("work:", error.Message);
        Assert.DoesNotContain("other:", error.Message);
    }

    [Fact]
    public void TimedWindowsDoNotAllocate()
    {
        // Converted tests keep exact zero-allocation asserts around their timed loops.
        TimingBudget.Run(t =>
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                var w = t.Start();
                if (i % 2 == 0) t.Stop(w, "kept"); else t.Keep("other", t.End(w));
                t.Stop(t.Start());
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            t.Under("kept", 0, 1);
        });
    }

    [Fact]
    public void WaitInsideTheTimedWindowIsDiscounted()
    {
        // The other side: wall time the thread spent off the CPU inside the window is load, not work.
        if (ThreadCpu.MinimumMs > 1) return; // Windows: 2 ms of CPU is below what its tick clock can judge
        long work = SpinFor(2);
        Assert.Equal(1, TimingBudget.Run(t =>
        {
            var w = t.Start();
            Thread.Sleep(100);
            Spin(work);
            t.Under("work", t.Stop(w, "work"), 50);
        }, attempts: 1));
        Assert.True(TimingBudget.LastLoadFactor > 10, "load factor " + TimingBudget.LastLoadFactor);
    }

    [Fact]
    public void ThreadCpuClockRuns()
    {
        double before = ThreadCpu.Milliseconds();
        Spin(SpinFor(OperatingSystem.IsWindows() ? 100 : 5)); // Windows counts whole 15.6 ms ticks
        double spent = ThreadCpu.Milliseconds() - before;
        Assert.False(double.IsNaN(spent), "no thread CPU clock on this platform");
        Assert.InRange(spent, 1, 1000);
    }

    [Fact]
    public void BudgetIsExclusive()
    {
        Assert.Throws<XunitException>(() => Run(t => t.Under("value", 2, 2), attempts: 2));
    }

    [Fact]
    public void DeterministicAssertionFailsAtOnce()
    {
        int runs = 0;
        Assert.Throws<EqualException>(() => Run(t => { runs++; t.Under("value", 9, 2); Assert.Equal(0, 1); }));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void GuardedWorkloadsPassBesideAFullCpuLoad()
    {
        int before = TimingBudget.Retries;
        var watch = Stopwatch.StartNew();
        using (new CpuLoad())
        {
            // A synthetic per-call p95 against the 5 ms mod-work gate, and E5b3's real budgets, which went red under load on 9 Nov.
            long call = SpinFor(0.2);
            double p95 = 0;
            int runs = TimingBudget.Run(t => t.Under("spin p95", p95 = P95(t, "spin p95", 400, () => Spin(call)), 5));
            output.WriteLine($"spin p95 {p95:0.000} ms at load factor {TimingBudget.LastLoadFactor:0.00} after {runs} run(s)");
            FoodContractCoordinatorTests.RecheckBoundaryRebuildAndCommitGateTimings(new List<string>());
        }
        output.WriteLine($"{Environment.ProcessorCount} spinning threads; guarded runs took {watch.Elapsed.TotalSeconds:0.0} s and {TimingBudget.Retries - before} extra attempts");
    }

    [Fact]
    public void TenfoldSlowdownStillFails()
    {
        // The budget is three times the fastest quiet run; the slowed workload does ten times the work.
        long iterations = SpinFor(1.0);
        double quiet = Enumerable.Range(0, 5).Min(_ => Time(() => Spin(iterations)));
        double budget = 3 * quiet;
        TimingBudget.Under("quiet", budget, () => Spin(iterations));
        Assert.Throws<XunitException>(() => TimingBudget.Under("slowed", budget, () => Spin(10 * iterations)));
        using (new CpuLoad())
            Assert.Throws<XunitException>(() => TimingBudget.Under("slowed under load", budget, () => Spin(10 * iterations)));
    }

    private static double P95(TimingChecks timing, string check, int samples, Action call)
    {
        var times = new double[samples];
        for (int i = 0; i < samples; i++) { var w = timing.Start(); call(); times[i] = timing.Stop(w, check); }
        Array.Sort(times);
        return times[samples * 95 / 100];
    }

    private static double Time(Action call)
    {
        long t = Stopwatch.GetTimestamp();
        call();
        return (Stopwatch.GetTimestamp() - t) * 1000.0 / Stopwatch.Frequency;
    }

    private static long sink;

    private static void Spin(long iterations)
    {
        long x = 1;
        for (long i = 0; i < iterations; i++) x = x * 6364136223846793005L + 1442695040888963407L;
        Volatile.Write(ref sink, x);
    }

    // Iterations of Spin that take about this long on a quiet core (fastest of several tries).
    private static long SpinFor(double ms)
    {
        const long probe = 1_000_000;
        double best = Enumerable.Range(0, 5).Min(_ => Time(() => Spin(probe)));
        return Math.Max(1, (long)(probe * ms / best));
    }

    /// <summary>One spinning thread per logical core, at normal priority, until disposed.</summary>
    private sealed class CpuLoad : IDisposable
    {
        private volatile bool stop;
        private readonly Thread[] threads;

        public CpuLoad()
        {
            threads = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => new Thread(() =>
            {
                long x = 1;
                while (!stop) x = x * 6364136223846793005L + 1442695040888963407L;
                Volatile.Write(ref sink, x);
            }) { IsBackground = true }).ToArray();
            foreach (var thread in threads) thread.Start();
        }

        public void Dispose()
        {
            stop = true;
            foreach (var thread in threads) thread.Join();
        }
    }
}
