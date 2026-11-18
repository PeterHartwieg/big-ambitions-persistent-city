using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5KernelCopyTests
{
    private readonly ITestOutputHelper output;
    public Gr5KernelCopyTests(ITestOutputHelper output) { this.output = output; }

    private static long Bytes(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void ARescaleCopiesTheLiveKernelOnceInsteadOfCapturingAndRestoringIt()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var kernel = TrafficKernel.Restore(state.Traffic!);
        kernel.RescaleTime(1.5); kernel.Capture(); TrafficKernel.Restore(state.Traffic!);
        long restore = Bytes(() => TrafficKernel.Restore(state.Traffic!));
        long capture = Bytes(() => kernel.Capture());
        long rescale = Bytes(() => kernel.RescaleTime(1.5));
        output.WriteLine($"13,965 links, 250 vehicles (.NET): restore {restore / 1e6:0.00} MB, capture {capture / 1e6:0.00} MB, rescale {rescale / 1e6:0.00} MB"
            + " (before: restore 5.16, capture 3.25, rescale 7.74)");
        Assert.True(rescale + capture / 2 < capture + restore, rescale + " vs " + (capture + restore));
        Assert.True(restore < 4_000_000, "restore " + restore);
    }

    [Fact]
    public void ARescaledLiveKernelEqualsARescaledRestoredCopy()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var live = TrafficKernel.Restore(state.Traffic!);
        live.AdvanceTo(state.Traffic!.Clock + 0.4, 100000);
        var copy = TrafficKernel.Restore(live.Capture());
        Func<string, double, double> map = (id, minute) => minute * 1.25;
        foreach (double factor in new[] { 1.0 / 3, 3, 20 })
        {
            string a = System.Text.Json.JsonSerializer.Serialize(live.RescaleTime(factor, map).Capture());
            string b = System.Text.Json.JsonSerializer.Serialize(copy.RescaleTime(factor, map).Capture());
            Assert.Equal(b, a);
        }
    }
}
