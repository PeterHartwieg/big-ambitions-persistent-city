using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

// F2d: the pure car-follow arithmetic and the car part of the F2c movement telemetry.
public sealed class CarFollowTests
{
    // Walk 2 min to the car, three car legs of 1 min (the middle one a zero-distance wait), walk 3 min from the parked car.
    private static Route Route() => new Route { Legs = new() {
        new RouteLeg { From = new Position(0, 0, 0), To = new Position(10, 0, 0), Minutes = 2, Mode = TravelMode.Walk },
        new RouteLeg { From = new Position(10, 0, 0), To = new Position(20, 0, 0), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:t:0:1" },
        new RouteLeg { From = new Position(20, 0, 0), To = new Position(20, 0, 0), Minutes = 1, Mode = TravelMode.Car },
        new RouteLeg { From = new Position(20, 0, 0), To = new Position(30, 0, 0), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:t:1:2" },
        new RouteLeg { From = new Position(30, 0, 0), To = new Position(40, 0, 0), Minutes = 3, Mode = TravelMode.Walk } } };

    [Fact]
    public void CarLegsEndIsTheParkingPointOfTheRunOfCarLegs()
    {
        var route = Route();
        Assert.Equal(1, CarFollow.CarLegsEnd(route, 1));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 2));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 3.5));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 5));
        Assert.Equal(6, CarFollow.CarLegsEnd(route, 6));
        // The cap is the logical coordinate, but never past the parking point.
        Assert.Equal(3.25, CarFollow.Cap(route, 2.5, 3.25));
        Assert.Equal(5, CarFollow.Cap(route, 2.5, 7));
        Assert.Equal(0, CarFollow.Cap(route, 2.5, double.NaN));
    }

    [Fact]
    public void AdvanceWaitsOutAZeroDistanceCarLegWithTheCapAndStopsAtTheParkingPoint()
    {
        var route = Route();
        // Gley reported the first road corner: the car crosses it, waits out the zero-distance leg up to the cap.
        Assert.Equal(3.5, CarFollow.Advance(route, 2.5, 3.5, new Position(20, 0, 0), 1), 12);
        // With the cap past the wait, it projects on the last car leg, and never onto the walk after parking.
        Assert.Equal(4.5, CarFollow.Advance(route, 3.5, CarFollow.Cap(route, 3.5, 6), new Position(25, 0, 0), -1), 12);
        Assert.Equal(5, CarFollow.Advance(route, 4.5, CarFollow.Cap(route, 4.5, 8), new Position(30, 0, 0), 3), 12);
        // A body pushed off its leg keeps its progress.
        Assert.Equal(2.5, CarFollow.Advance(route, 2.5, 3, new Position(15, 0, 9), -1));
    }

    [Fact]
    public void CorrectionThresholdsAreFiveAndThirtyMetres()
    {
        Assert.Equal(5, CarFollow.CatchUpMetres); Assert.Equal(30, CarFollow.CorrectionMetres);
        Assert.False(CarFollow.NeedsCorrection(30, true));
        Assert.True(CarFollow.NeedsCorrection(30.01, true));
        Assert.True(CarFollow.NeedsCorrection(double.NaN, true));
        Assert.True(CarFollow.NeedsCorrection(0, false));
    }

    [Fact]
    public void PhaseMismatchIsMeasuredOnlyForAStandingCarWithAKnownNativeLight()
    {
        Assert.Equal(CarPhaseMismatch.KernelHeldAtNativeGreen, CarFollow.Classify(true, false, 0.2, true, false));
        Assert.Equal(CarPhaseMismatch.NativeStopAtKernelGreen, CarFollow.Classify(true, true, 12, false, true));
        // Moving, unknown native light, still catching up, or both agreeing: nothing.
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(false, false, 0.2, true, false));
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(true, null, 0.2, true, true));
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(true, false, 6, true, false));
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(true, true, 0.2, true, false));
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(true, false, 0.2, false, true));
    }

    [Fact]
    public void CarCorrectionsShareTheRingAndCountSeparately()
    {
        var t = new WalkerMovementTelemetry();
        t.Record(1, "w", WalkerCorrectionKind.Move, new Position(0, 0, 0), new Position(1, 0, 0), false);
        t.Record(2, "c", WalkerCorrectionKind.LagRelease, new Position(0, 0, 0), new Position(40, 0, 0), false, car: true);
        t.Record(3, "c", WalkerCorrectionKind.Fade, new Position(5, 0, 0), new Position(40, 0, 0), true, car: true);
        Assert.Equal(3, t.Corrections); Assert.Equal(2, t.CarCorrections);
        Assert.Equal(1, t.CarFades); Assert.Equal(1, t.CarLagReleases); Assert.Equal(0, t.OnScreenMoves);
        var list = new List<WalkerCorrection>();
        t.Since(1, list, out bool gap);
        Assert.False(gap); Assert.Equal(new long[] { 2, 3 }, list.ConvertAll(c => c.Sequence));
        Assert.EndsWith("body=car", WalkerMovementTelemetry.Format(list[1]));
        Assert.Contains("kind=Fade on_screen=true", WalkerMovementTelemetry.Format(list[1]));
        Assert.EndsWith("body=walker", WalkerMovementTelemetry.Format(new WalkerCorrection(1, 1, "w", WalkerCorrectionKind.Move, default, default, false)));
        // Eviction is shared: 600 car entries push the walker's out and the ring reports the gap.
        for (int i = 0; i < 600; i++) t.Record(10 + i, "c", WalkerCorrectionKind.LagRelease, default, default, false, car: true);
        list.Clear(); t.Since(0, list, out gap);
        Assert.True(gap); Assert.Equal(WalkerMovementTelemetry.Capacity, list.Count); Assert.All(list, c => Assert.True(c.Car));
    }

    [Fact]
    public void CarLagAndPhaseCountsAreReportedByStatus()
    {
        var t = new WalkerMovementTelemetry();
        foreach (double lag in new[] { 1.0, 2.0, 3.0, 40.0 }) t.RecordCarLag(lag);
        t.RecordLag(0.5);
        Assert.Equal(4, t.CarLagSamples); Assert.Equal(1, t.LagSamples); Assert.Equal(40, t.CarMaximumLagMetres);
        Assert.InRange(t.CarLagQuantile(0.5), 2, 2.051); Assert.Equal(40, t.CarLagQuantile(0.95));
        t.RecordPhase(CarPhaseMismatch.KernelHeldAtNativeGreen, CarPhaseMismatch.None, 0.5);
        t.RecordPhase(CarPhaseMismatch.KernelHeldAtNativeGreen, CarPhaseMismatch.KernelHeldAtNativeGreen, 0.25);
        t.RecordPhase(CarPhaseMismatch.NativeStopAtKernelGreen, CarPhaseMismatch.KernelHeldAtNativeGreen, 1);
        t.RecordPhase(CarPhaseMismatch.None, CarPhaseMismatch.NativeStopAtKernelGreen, 1);
        Assert.Equal(1, t.KernelHeldAtNativeGreen); Assert.Equal(0.75, t.KernelHeldAtNativeGreenSeconds);
        Assert.Equal(1, t.NativeStopAtKernelGreen); Assert.Equal(1, t.NativeStopAtKernelGreenSeconds);
        string status = t.Status(2, 3);
        Assert.Contains("followers=2\n", status);
        Assert.Contains("car_followers=3\n", status);
        Assert.Contains("car_lag_samples=4\n", status);
        Assert.Contains("car_lag_max_m=40.00\n", status);
        Assert.Contains("phase_kernel_held_native_green=1\n", status);
        Assert.Contains("phase_native_stop_kernel_green=1\n", status);
        Assert.Contains("car_thresholds=catch_up_m:5;correct_m:30;hurry_s:10;fade_out_s:1;stopped_mps:0.5", status);
        // The walker lines keep their F2c order and names.
        Assert.StartsWith("followers=2\nlag_samples=1\n", status);
    }
}
