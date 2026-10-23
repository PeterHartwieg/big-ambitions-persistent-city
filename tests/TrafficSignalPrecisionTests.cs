using System;
using System.Reflection;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficSignalPrecisionTests
{
    private static readonly Func<TrafficSignalSchedule, double, double> NextGreen =
        typeof(TrafficSignalSchedule).GetMethod("NextGreen", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TrafficSignalSchedule, double, double>>();

    [Fact]
    public void OrdinaryClockAdvanceKeepsTheSamePendingSignalWakeupValid()
    {
        const double clock = 3678.3051732231675;
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition
        {
            Id = 0, Capacity = 2, FreeFlowMinutes = 1,
            Signal = new TrafficSignalSchedule { CycleMinutes = 954.2101412698878,
                GreenStartMinutes = 850.1726553262563, GreenDurationMinutes = 13.637459620133104,
                PhaseOffsetMinutes = 89464.96137302305 }
        } }, clock - 1);
        kernel.Enqueue("car", new[] { new TrafficRouteLeg { LinkId = 0, EndNominalMinutes = 1 } });
        kernel.AdvanceTo(clock, 100);
        TrafficKernel.ValidateState(kernel.Capture());
        double green = kernel.NextEventMinute;
        kernel.AdvanceTo(4081.1806146462413, 100);
        TrafficKernel.ValidateState(kernel.Capture());
        Assert.Equal(green, kernel.NextEventMinute);
        Assert.False(kernel.GetPermission("car").IsCompleted);
        kernel.AdvanceTo(green, 100);
        Assert.Equal(green, kernel.GetPermission("car").CompletedAt);
    }

    [Fact]
    public void RetimedSignalWakeupRemainsValidBetweenEvents()
    {
        const double start = 2298.455691523131, duration = 4.639076615841629;
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition
        {
            Id = 0, Capacity = 2, FreeFlowMinutes = duration,
            Signal = new TrafficSignalSchedule { CycleMinutes = 8.43545518137303,
                GreenStartMinutes = 1.687091036274606, GreenDurationMinutes = 2.5306365544119087,
                PhaseOffsetMinutes = 8.116416534463138 }
        } }, start);
        kernel.Enqueue("car", new[] { new TrafficRouteLeg { LinkId = 0, EndNominalMinutes = duration } });
        kernel.AdvanceTo(start + .01, 100);
        kernel = kernel.RescaleTime(60);
        double green = kernel.NextEventMinute;
        for (int i = 0; i < 4; i++)
        {
            kernel.AdvanceTo(kernel.Clock + .8 * (green - kernel.Clock), 100);
            TrafficKernel.ValidateState(kernel.Capture());
            Assert.Equal(green, kernel.NextEventMinute);
            Assert.False(kernel.GetPermission("car").IsCompleted);
        }
        kernel.AdvanceTo(green, 100);
        // An independently scaled event may be a few ulps early; its normal guard
        // reschedules the canonical boundary without granting an early discharge.
        kernel.AdvanceTo(green + 1e-8, 100);
        Assert.True(kernel.GetPermission("car").IsCompleted);
    }

    [Theory]
    [InlineData(0)] [InlineData(-100000)] [InlineData(100000)]
    public void GreenWindowsAreHalfOpenAcrossSignedOffsets(double offset)
    {
        var signal = new TrafficSignalSchedule { CycleMinutes = 10, GreenStartMinutes = 3,
            GreenDurationMinutes = 2, PhaseOffsetMinutes = offset };
        Assert.Equal(13, NextGreen(signal, Math.BitDecrement(13)));
        Assert.Equal(13, NextGreen(signal, 13));
        Assert.Equal(Math.BitDecrement(15), NextGreen(signal, Math.BitDecrement(15)));
        Assert.Equal(23, NextGreen(signal, 15));
        Assert.Equal(23, NextGreen(signal, Math.BitIncrement(15)));
    }

    [Theory]
    [InlineData(0.572, 1.13, .33, .07, 10000000)]
    [InlineData(18.304, 1.13, .33, .07, 1000000000)]
    [InlineData(-135121.70332599027, 506.1273108823818, 101.22546217647636, 151.83819326471453, 2500)]
    [InlineData(89464.96137302305, 954.2101412698878, 850.1726553262563, 13.637459620133104, 3500)]
    public void NextGreenIsMonotoneIdempotentAndConstantThroughoutRed(double offset, double cycle, double greenStart, double duration, double start)
    {
        var signal = new TrafficSignalSchedule { CycleMinutes = cycle, GreenStartMinutes = greenStart,
            GreenDurationMinutes = duration, PhaseOffsetMinutes = offset };
        double previous = double.NegativeInfinity;
        for (int i = 0; i < 1000; i++)
        {
            double time = start + i * cycle / 100;
            double next = NextGreen(signal, time);
            Assert.True(double.IsFinite(next) && next >= time && next >= previous);
            Assert.Equal(next, NextGreen(signal, next));
            if (next > time)
            {
                double middle = time + (next - time) * .8;
                Assert.Equal(next, NextGreen(signal, middle));
                Assert.Equal(next, NextGreen(signal, Math.BitDecrement(next)));
            }
            previous = next;
        }
    }

    [Fact]
    public void UnresolvableCyclesFailClosedAndFullGreenRetainsItsMeaning()
    {
        var signal = new TrafficSignalSchedule { CycleMinutes = double.Epsilon,
            GreenDurationMinutes = 0, PhaseOffsetMinutes = double.MaxValue };
        Assert.Equal(double.PositiveInfinity, NextGreen(signal, 1e9));
        signal.CycleMinutes = 1e-300; signal.GreenDurationMinutes = 1e-301;
        Assert.Equal(double.PositiveInfinity, NextGreen(signal, 1e9));
        signal.GreenDurationMinutes = signal.CycleMinutes;
        Assert.Equal(1e9, NextGreen(signal, 1e9));
        signal.CycleMinutes = 10; signal.GreenDurationMinutes = .1; signal.PhaseOffsetMinutes = 0;
        Assert.Equal(1e16, NextGreen(signal, 1e16));
        double boundary = NextGreen(signal, Math.BitIncrement(1e16));
        Assert.Equal(boundary, NextGreen(signal, boundary));
        signal.CycleMinutes = double.MaxValue; signal.GreenStartMinutes = double.MaxValue / 2;
        signal.GreenDurationMinutes = 1;
        double next = NextGreen(signal, 1e9);
        Assert.False(double.IsNaN(next)); Assert.True(next >= 1e9);
    }
}
