using System;
using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

// F2d: the pure car-follow arithmetic and the car part of the F2c movement telemetry.
public sealed class CarFollowTests
{
    // The Runtime car shape: walk 2 min to the car, a 1 min preparation pause, two road legs of 1 min, a 0.5 min kerb
    // connector off the road, a 2 min parking pause, then a 3 min walk from the parked car.
    private static Route Route() => new Route { Legs = new() {
        new RouteLeg { From = new Position(0, 0, 0), To = new Position(10, 0, 0), Minutes = 2, Mode = TravelMode.Walk },
        new RouteLeg { From = new Position(10, 0, 0), To = new Position(10, 0, 0), Minutes = 1, Mode = TravelMode.Car },
        new RouteLeg { From = new Position(10, 0, 0), To = new Position(20, 0, 0), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:t:0:1" },
        new RouteLeg { From = new Position(20, 0, 0), To = new Position(30, 0, 0), Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:v1:t:1:2" },
        new RouteLeg { From = new Position(30, 0, 0), To = new Position(34, 0, 0), Minutes = 0.5, Mode = TravelMode.Car },
        new RouteLeg { From = new Position(34, 0, 0), To = new Position(34, 0, 0), Minutes = 2, Mode = TravelMode.Car },
        new RouteLeg { From = new Position(34, 0, 0), To = new Position(40, 0, 0), Minutes = 3, Mode = TravelMode.Walk } } };

    [Fact]
    public void CarLegsEndIsTheEndOfTheNativeRoadLegs()
    {
        var route = Route();
        Assert.Equal(1, CarFollow.CarLegsEnd(route, 1));
        Assert.Equal(2.5, CarFollow.CarLegsEnd(route, 2.5));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 3));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 4.5));
        Assert.Equal(5, CarFollow.CarLegsEnd(route, 5));
        Assert.Equal(6, CarFollow.CarLegsEnd(route, 6));
        // The cap is the logical coordinate, but never past the road's end.
        Assert.Equal(4.25, CarFollow.Cap(route, 3.5, 4.25));
        Assert.Equal(5, CarFollow.Cap(route, 3.5, 7));
        Assert.Equal(0, CarFollow.Cap(route, 3.5, double.NaN));
        Assert.True(CarFollow.IsRoad(route.Legs[2])); Assert.False(CarFollow.IsRoad(route.Legs[4])); Assert.False(CarFollow.IsRoad(route.Legs[5]));
    }

    [Fact]
    public void TheTrailingConnectorAndParkingPauseAreStillTheCarsRun()
    {
        var route = Route();
        Assert.True(CarFollow.SameCarRun(route, 4, 4.5));
        Assert.True(CarFollow.SameCarRun(route, 4, 5.2));
        Assert.True(CarFollow.SameCarRun(route, 4, 7));
        Assert.False(CarFollow.SameCarRun(route, 4, 8));
        Assert.False(CarFollow.SameCarRun(route, 4, double.NaN));
    }

    [Fact]
    public void AdvanceStopsAtTheRoadEndAndNeverOntoTheConnector()
    {
        var route = Route();
        // Gley reported the first road corner: the car crosses it and projects on the second road leg.
        Assert.Equal(4.5, CarFollow.Advance(route, 3.5, 6, new Position(25, 0, 0), 2), 12);
        // At the road's end with Gley's evidence: the parking point, never onto the connector, whatever the cap.
        Assert.Equal(5, CarFollow.Advance(route, 4.5, 7, new Position(30, 0, 0), 3), 12);
        Assert.Equal(5, CarFollow.Advance(route, 5, 7, new Position(34, 0, 0), 4), 12);
        Assert.False(CarFollow.IsRoad(WalkerFollow.Sample(route, 5)));
        // A body pushed off its leg keeps its progress.
        Assert.Equal(3.5, CarFollow.Advance(route, 3.5, 4, new Position(15, 0, 9), -1));
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
        // Both red now at the car's junction: no mismatch, whatever the kernel did there earlier.
        Assert.Equal(CarPhaseMismatch.None, CarFollow.Classify(true, true, 25, false, false));
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

    /// <summary>F2d (E1): a car body's model and paint are derived from the stable car ID, the same values the native
    /// adapter used to write on first acquisition, and nothing is written, so a seen car saves like an unseen one.</summary>
    [Fact]
    public void CarAppearanceIsDerivedWithoutWritingTheSavedCar()
    {
        var models = new[] { "Coupe", "Hatch", "Sedan", "Van" };
        int traversals = 0;
        IReadOnlyList<string> Catalogue() { traversals++; return models; }
        var fresh = new CarState { Id = "resident-17:car" };
        Assert.True(CarAppearance.TryModel(fresh, Catalogue, out string model));
        uint hash = 2166136261; foreach (char c in fresh.Id) hash = unchecked((hash ^ c) * 16777619);
        Assert.Equal(models[hash % (uint)models.Length], model);
        uint paintHash = 2166136261; foreach (char c in fresh.Id + ":paint") paintHash = unchecked((paintHash ^ c) * 16777619);
        Assert.Equal(1 + (int)(paintHash % 2147483646u), CarAppearance.Paint(fresh));
        // Nothing written, and the same answer every time.
        Assert.Equal("", fresh.ModelId); Assert.Equal(0, fresh.PaintSeed);
        Assert.True(CarAppearance.TryModel(fresh, Catalogue, out string again)); Assert.Equal(model, again);
        // An empty catalogue refuses the body (a failed acquisition), still writing nothing.
        Assert.False(CarAppearance.TryModel(fresh, () => Array.Empty<string>(), out _));
        Assert.Equal("", fresh.ModelId); Assert.Equal(0, fresh.PaintSeed);
        // A saved assignment (an older city) is kept and needs no catalogue traversal.
        traversals = 0;
        var saved = new CarState { Id = "resident-17:car", ModelId = "Truck", PaintSeed = 42 };
        Assert.True(CarAppearance.TryModel(saved, Catalogue, out string kept));
        Assert.Equal("Truck", kept); Assert.Equal(42, CarAppearance.Paint(saved)); Assert.Equal(0, traversals);
    }
}
