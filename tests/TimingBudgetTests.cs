using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(TimingBudgetTests), DisableParallelization = true)]
public sealed class TimingBudgetCollection { }

[Collection(nameof(TimingBudgetTests))]
public sealed class TimingBudgetTests
{
    private readonly ITestOutputHelper output;
    public TimingBudgetTests(ITestOutputHelper output) => this.output = output;

    private static int Run(Action<TimingChecks> measure, int attempts = TimingBudget.Attempts, double factor = 1)
        => TimingBudget.Run(t =>
        {
            foreach (var label in new[] { "value", "a", "b" }) t.Keep(label, default);
            measure(t);
        }, attempts, (_, _) => factor);

    [Fact]
    public void BudgetsAreJudgedOnlyWhenRequested()
    {
        if (PerfGate.Enabled) return;
        int runs = 0;
        Assert.Equal(1, TimingBudget.Run(t => { runs++; t.Under("value", 9, 2); }));
        Assert.Equal(1, runs);
        Assert.Throws<EqualException>(() => TimingBudget.Run(t => Assert.Equal(0, 1)));
        using (TimingBudget.Enforce())
            Assert.Throws<XunitException>(() => TimingBudget.Run(t => t.Under("value", 9, 2), attempts: 2));
        Assert.False(TimingBudget.Enforced);
    }

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
        Assert.Equal(1, Run(t => t.Under("value", 9, 4), factor: 3));
        Assert.Throws<XunitException>(() => Run(t => t.Under("value", 9, 2), attempts: 2, factor: 3));
        Assert.Equal(3, TimingBudget.LoadFactor(3000, 1000));
        Assert.Equal(1, TimingBudget.LoadFactor(500, 1000));
        Assert.Equal(1, TimingBudget.LoadFactor(5, double.NaN));
        Assert.Equal(1, TimingBudget.LoadFactor(ThreadCpu.MinimumMs * 1.5, ThreadCpu.MinimumMs / 2));
    }

    [Fact]
    public void MeasurementWithoutATimedWindowGetsNoDiscount()
    {
        Assert.Throws<XunitException>(() => TimingBudget.Run(t => t.Under("value", 9, 4), 2, (_, _) => 3));
        Assert.Equal(1, TimingBudget.Run(t => { t.Stop(t.Start(), "value"); t.Under("value", 9, 4); }, 2, (_, _) => 3));
        Assert.Throws<XunitException>(() => TimingBudget.Run(t => { t.Stop(t.Start(), "other"); t.Stop(t.Start()); t.Under("value", 9, 4); }, 2, (_, _) => 3));
    }

    [Fact]
    public void DelayOutsideTheTimedWindowDoesNotLoosenTheBudget()
    {
        using var enforced = TimingBudget.Enforce();
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
        using var enforced = TimingBudget.Enforce();
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
    public void StallInOneSampleDoesNotHideACpuSpikeInAnother()
    {
        using var enforced = TimingBudget.Enforce();
        long spike = SpinFor(48);
        var error = Assert.Throws<XunitException>(() => TimingBudget.Run(t =>
        {
            var a = t.Start(); Spin(spike); double first = t.Stop(a, "unit max");
            var b = t.Start(); Thread.Sleep(65); double second = t.Stop(b, "unit max");
            t.MaxUnder("unit max", Math.Max(first, second), 30);
        }, attempts: 2));
        Assert.Contains("unit max:", error.Message);
    }

    [Fact]
    public void StalledSampleAloneStillPassesAMaxCheck()
    {
        if (ThreadCpu.MinimumMs > 1) return;
        using var enforced = TimingBudget.Enforce();
        long work = SpinFor(1);
        Assert.Equal(1, TimingBudget.Run(t =>
        {
            var a = t.Start(); Spin(work); double first = t.Stop(a, "unit max");
            var b = t.Start(); Thread.Sleep(65); Spin(work); double second = t.Stop(b, "unit max");
            t.MaxUnder("unit max", Math.Max(first, second), 30);
        }, attempts: 1));
    }

    [Fact]
    public void TimedWindowsDoNotAllocate()
    {
        using var enforced = TimingBudget.Enforce();
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
        using var enforced = TimingBudget.Enforce();
        if (ThreadCpu.MinimumMs > 1) return;
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
        Spin(SpinFor(OperatingSystem.IsWindows() ? 100 : 5));
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
        if (!PerfGate.Enabled) return;
        using var enforced = TimingBudget.Enforce();
        int before = TimingBudget.Retries;
        var watch = Stopwatch.StartNew();
        using (new CpuLoad())
        {
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
        if (!PerfGate.Enabled) return;
        using var enforced = TimingBudget.Enforce();
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

    private static long SpinFor(double ms)
    {
        const long probe = 1_000_000;
        double best = Enumerable.Range(0, 5).Min(_ => Time(() => Spin(probe)));
        return Math.Max(1, (long)(probe * ms / best));
    }

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
