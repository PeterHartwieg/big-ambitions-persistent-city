using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class UpdateCityFrameModelTests
{
    private readonly ITestOutputHelper output;
    public UpdateCityFrameModelTests(ITestOutputHelper output) { this.output = output; }

    private sealed class Outcome
    {
        public double P50, P95, P99, Max, AdvancePerSecond, DemandPerSecond, BuildsPerSecond, RequestsPerSecond, EndBacklog, EndWaiting;
        public int Spikes;
    }

    private static double Rank(List<double> sorted, double fraction) => sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(fraction * sorted.Count) - 1))];

    private static Outcome Run(bool bounded, double fps, double seconds = 600, int seed = 0x14A)
    {
        var dice = new Random(seed);
        var frame = new UpdateCityBudget();
        double elapsed = 0;
        var gate = bounded ? new DressFrameGate(() => elapsed) : null;
        var dress = new DressBuildBudget(gate: gate);
        double step = 1 / fps, backlog = 0, demand = 0, done = 0, waiting = 0, requests = 0, nextBurst = 0;
        int builds = 0, frames = (int)(seconds * fps), spikes = 0;
        var samples = new List<double>(frames);
        for (int f = 0; f < frames; f++)
        {
            double now = f * step;
            double pre = 0.25 + dice.NextDouble() * 0.35 + (dice.NextDouble() < 0.01 ? 1.5 : 0);
            elapsed = pre;
            double arriving = BaseAdvanceMillisecondsPerSecond * step;
            while (nextBurst <= now) { arriving += 5 + dice.NextDouble() * 20; nextBurst += -Math.Log(1 - dice.NextDouble()) / BurstsPerSecond; }
            backlog += arriving; demand += arriving;
            var native = new PopulationFrameBudget(step, false);
            double budget = bounded ? frame.Advance(native.Milliseconds, elapsed) : native.Milliseconds;
            double advance = 0;
            while (backlog > 0)
            {
                double unit = Math.Min(backlog, 0.05 + dice.NextDouble() * 0.3);
                advance += unit; backlog -= unit;
                if (advance >= budget) break;
            }
            done += advance;
            elapsed += advance;
            double physical = 0.35 + dice.NextDouble() * 0.5;
            waiting += BuildsWantedPerSecond * step; requests += BuildsWantedPerSecond * step;
            double build = 0;
            if (waiting >= 1 && dress.TryTake(f, now))
            {
                build = 4 + dice.NextDouble() * 4 + (dice.NextDouble() < 0.04 ? 10 + dice.NextDouble() * 15 : 0);
                waiting -= 1; builds++;
                gate?.Observe(build);
            }
            frame.ObservePhysical(physical);
            elapsed += physical + build;
            samples.Add(elapsed);
            if (elapsed >= 30) spikes++;
        }
        samples.Sort();
        return new Outcome
        {
            P50 = Rank(samples, .5), P95 = Rank(samples, .95), P99 = Rank(samples, .99), Max = samples[samples.Count - 1], Spikes = spikes,
            AdvancePerSecond = done / seconds, DemandPerSecond = demand / seconds, BuildsPerSecond = builds / seconds,
            RequestsPerSecond = requests / seconds, EndBacklog = backlog, EndWaiting = waiting,
        };
    }

    private const double BaseAdvanceMillisecondsPerSecond = 1, BurstsPerSecond = 0.2, BuildsWantedPerSecond = 0.55;

    private string Line(string label, double fps, Outcome o) => string.Format(CultureInfo.InvariantCulture,
        "{0} {1:0.0} fps: UpdateCity p50/p95/p99/max {2:0.00}/{3:0.00}/{4:0.00}/{5:0.00} ms, spikes>=30 {6}; advance {7:0.0}/{8:0.0} ms per s (backlog {9:0.0} ms); builds {10:0.00}/{11:0.00} per s (waiting {12:0})",
        label, fps, o.P50, o.P95, o.P99, o.Max, o.Spikes, o.AdvancePerSecond, o.DemandPerSecond, o.EndBacklog, o.BuildsPerSecond, o.RequestsPerSecond, o.EndWaiting);

    [Theory]
    [InlineData(5.8)]
    [InlineData(28)]
    [InlineData(60)]
    public void TheFrameBudgetBoundsUpdateCityWithoutStarvingTheCityOrTheBodies(double fps)
    {
        var before = Run(bounded: false, fps);
        var after = Run(bounded: true, fps);
        output.WriteLine(Line("before", fps, before));
        output.WriteLine(Line("after ", fps, after));
        Assert.True(after.P95 <= 4.5, Line("after", fps, after));
        Assert.True(after.P95 < before.P95 || before.P95 <= 4.5);
        Assert.Equal(0, after.Spikes);
        Assert.True(after.AdvancePerSecond >= after.DemandPerSecond * 0.99, "advance falls behind");
        Assert.True(after.EndBacklog < 50, "advance backlog grows");
        if (fps >= 28) Assert.True(after.BuildsPerSecond >= after.RequestsPerSecond * 0.98, "bodies fall behind at " + fps + " fps");
        else Assert.True(after.BuildsPerSecond >= fps / DressFrameGate.DefaultMaximumFrames * 0.95);
    }

    [Fact]
    public void TheAdvanceGetsWhatIsLeftOfTheFrameAboveItsFloor()
    {
        var budget = new UpdateCityBudget();
        Assert.Equal(UpdateCityBudget.InitialReserveMilliseconds, budget.ReserveMilliseconds);
        Assert.Equal(4 - 0.5 - 1, budget.Advance(4, 0.5), 9);
        Assert.Equal(UpdateCityBudget.AdvanceFloorMilliseconds, budget.Advance(4, 3.5));
        Assert.Equal(2, budget.Advance(2, 3.5));
        Assert.Equal(16, new UpdateCityBudget(32).Advance(16, 1));
        Assert.Equal(3, budget.Advance(4, double.NaN));
        budget.ObservePhysical(50);
        Assert.Equal(UpdateCityBudget.MaximumReserveMilliseconds, budget.ReserveMilliseconds);
        for (int i = 0; i < 200; i++) budget.ObservePhysical(0.4);
        Assert.InRange(budget.ReserveMilliseconds, 0.4, 0.45);
        budget.ObservePhysical(double.NaN); budget.ObservePhysical(-1);
        Assert.InRange(budget.ReserveMilliseconds, 0.4, 0.45);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Advance(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UpdateCityBudget(0));
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 10000; i++) { budget.ObservePhysical(i % 7 * 0.2); sum += budget.Advance(4, i % 5 * 0.3); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.True(sum > 0);
    }

    [Fact]
    public void TheDressGateBuildsOnLightFramesAndNeverWaitsLongerThanItsMaximum()
    {
        double elapsed = 0;
        var gate = new DressFrameGate(() => elapsed);
        Assert.True(gate.Allows(0));
        gate.Built(0);
        elapsed = 0;
        for (int f = 1; f < DressFrameGate.DefaultQuietFrames; f++) Assert.False(gate.Allows(f));
        Assert.Equal(DressFrameGate.DefaultEstimateMilliseconds, gate.EstimateMilliseconds);
        Assert.True(gate.Allows(DressFrameGate.DefaultQuietFrames));
        elapsed = 0.5;
        Assert.False(gate.Allows(DressFrameGate.DefaultQuietFrames));
        gate.Observe(2);
        Assert.True(gate.EstimateMilliseconds < 4 && gate.EstimateMilliseconds > 2);
        for (int i = 0; i < 40; i++) gate.Observe(2);
        Assert.True(gate.Allows(DressFrameGate.DefaultQuietFrames));
        gate.Observe(9);
        Assert.Equal(9, gate.EstimateMilliseconds);
        Assert.False(gate.Allows(DressFrameGate.DefaultMaximumFrames - 1));
        Assert.True(gate.Allows(DressFrameGate.DefaultMaximumFrames));
        Assert.True(gate.Allows(-5));
        elapsed = double.NaN;
        Assert.False(gate.Allows(DressFrameGate.DefaultQuietFrames));
        gate.Observe(double.NaN); gate.Observe(-3);
        Assert.Equal(9, gate.EstimateMilliseconds);
        var budget = new DressBuildBudget(gate: gate);
        elapsed = 0;
        Assert.False(budget.CanTake(5, 100));
        Assert.False(budget.TryTake(5, 100));
        Assert.True(budget.CanTake(DressFrameGate.DefaultMaximumFrames, 100));
        Assert.True(budget.TryTake(DressFrameGate.DefaultMaximumFrames, 100));
        Assert.Equal(1, gate.Waited);
        Assert.False(budget.TryTake(DressFrameGate.DefaultMaximumFrames + 1, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DressFrameGate(() => 0, quietFrames: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DressFrameGate(() => 0, quietFrames: 9, maximumFrames: 8));
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 100; f < 5100; f++) budget.TryTake(f, f / 30.0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
}
