using System;
using System.Linq;
using PersistentCity.MovementEquivalence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F2a (city audit F2 #89): the detached movement-equivalence harness and its self-checks.
/// Small fixture (96 residents, one day); the 957-resident baseline is recorded in docs/checks/f2a.md.</summary>
public sealed class MovementEquivalenceTests
{
    private const int Residents = 96;
    private static MovementOptions Small(Action<MovementOptions>? change = null)
    {
        var options = SelfChecks.Small("test", Residents);
        change?.Invoke(options);
        return options;
    }
    private static MovementOptions Plain(Action<MovementOptions>? change = null)
        => Small(o => { o.Driver = false; change?.Invoke(o); });

    private static readonly Lazy<MovementResult> PlainRun = new(() => MovementRun.Run(Plain()));
    private static readonly Lazy<MovementResult> BodiesRun = new(() => MovementRun.Run(Small()));

    [Fact]
    public void SameSeedIsByteIdenticalWithAndWithoutBodies()
    {
        Assert.True(SelfChecks.Same(PlainRun.Value, MovementRun.Run(Plain())));
        Assert.True(SelfChecks.Same(BodiesRun.Value, MovementRun.Run(Small())));
        Assert.NotEqual(PlainRun.Value.FinalSaveSha256, MovementRun.Run(Plain(o => o.Seed = 1)).FinalSaveSha256);
    }

    [Fact]
    public void BodiesOffArmsEqualThePlainSimulation()
    {
        var none = MovementRun.Run(Small(o => o.Focus = FocusPath.None));
        var zero = MovementRun.Run(Small(o => { o.Bodies.Pedestrians = 0; o.Bodies.Vehicles = 0; }));
        Assert.Equal(0, none.Metrics["bodies.acquired"]);
        Assert.Equal(0, zero.Metrics["bodies.acquired"]);
        Assert.True(SelfChecks.Same(none, PlainRun.Value));
        Assert.True(SelfChecks.Same(zero, PlainRun.Value));
        // Without bodies the frame cadence and a hidden-travel fast-forward change nothing (F1 discipline).
        Assert.True(SelfChecks.Same(MovementRun.Run(Plain(o => o.FramesPerSecond = 30)), PlainRun.Value));
        Assert.True(SelfChecks.Same(MovementRun.Run(Plain(o => o.HiddenTravel = true)), PlainRun.Value));
    }

    [Theory]
    [InlineData(Fault.DuplicateArrival, "journey ledger")]
    [InlineData(Fault.SkippedEvent, "observation epoch not published")]
    public void InjectedFaultsFailLoudly(Fault fault, string message)
    {
        var error = Assert.Throws<HarnessViolation>(() => MovementRun.Run(Small(o => o.Fault = fault)));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void MidJourneyReloadResumes()
    {
        var plain = MovementRun.Run(Plain(o => o.ReloadMidJourney = true));
        Assert.Equal(1, plain.Reloads);
        Assert.True(plain.InFlightAtReload > 0);
        Assert.True(SelfChecks.Same(plain, PlainRun.Value));
        var bodies = MovementRun.Run(Small(o => o.ReloadMidJourney = true));
        Assert.Equal(1, bodies.Reloads);
        Assert.True(bodies.BodiesAtReload > 0);
        Assert.True(bodies.InFlightAtReload > 0);
        Assert.Equal(bodies.Metrics["journeys.arrivals"], bodies.Boundaries[^1].Arrivals);
    }

    [Fact]
    public void BoundariesAreTheDrainedThreeOClockMinutes()
    {
        var run = PlainRun.Value;
        Assert.Equal(new[] { 1440.0 + 180, 2 * 1440.0 + 180 }, run.Boundaries.Select(b => b.Minute));
        Assert.True(run.FifoSamples >= (1440 + 180) / MovementRun.FifoSampleMinutes - 1);
        Assert.True(run.Metrics["journeys.walk.count"] > 0 && run.Metrics["journeys.car.count"] > 0);
        Assert.True(run.Metrics["meals.ownSales"] > 0);
        Assert.True(run.QueuedFifoSamples > 0, "the capacity-2 grid should queue cars");
    }

    [Fact]
    public void DriverDoubleFollowsCarsAndWalkers()
    {
        var m = BodiesRun.Value.Metrics;
        Assert.True(m["bodies.walkerFrames"] > 0 && m["bodies.carFrames"] > 0);
        Assert.True(m["walkers.lagSamples"] > 0 && m["cars.lagSamples"] > 0);
        Assert.True(m["cars.handoffs"] > 0, "a car reaching its parking point hands over to a walker");
        Assert.True(m["bodies.maxWalkers"] <= 48 && m["bodies.maxCars"] <= 24);
        var failures = MovementRun.Run(Small(o => o.Bodies.AcquisitionFailurePercent = 50));
        Assert.True(failures.Metrics["bodies.failedAcquisitions"] > 0);
        var hidden = MovementRun.Run(Small(o => o.HiddenTravel = true));
        Assert.True(hidden.Metrics["bodies.hiddenTravelFrames"] > 0);
    }

    [Fact]
    public void HiddenTravelReleasesBodiesBeforeTheHiddenSliceAdvances()
    {
        // MovementRun requires no binding and no physically controlled vehicle when a hidden slice advances.
        // 200 residents: the smallest fixture here with bodies bound when the 12:00 window opens.
        var hidden = MovementRun.Run(Small(o => { o.Residents = 200; o.HiddenTravel = true; }));
        Assert.True(hidden.Metrics["bodies.hiddenTravelFrames"] > 0);
        Assert.True(hidden.Metrics["bodies.hiddenTravelReleases"] > 0);
    }

    [Fact]
    public void SkippedMealsAreCountedForEveryDay()
    {
        var run = MovementRun.Run(Plain(o => { o.Days = 2; o.ReloadMidJourney = true; }));
        Assert.Equal(3, run.Boundaries.Count);
        Assert.Equal(0, run.Boundaries[0].SkippedMeals);
        Assert.True(run.Boundaries[1].SkippedMeals > 0 && run.Boundaries[2].SkippedMeals > 0);
        Assert.Equal(run.Boundaries.Sum(b => b.SkippedMeals), run.Metrics["meals.skipped"]);
    }

    /// <summary>F2c: in a walking-only city every body arm equals bodies-off at the same cadence (E1): bodies follow
    /// the logical pose and write nothing. Covers focus paths, caps, native lag, acquisition failures, a slower
    /// cadence, a mid-journey reload with walkers bound and hidden travel.</summary>
    [Fact]
    public void WalkerArmsEqualBodiesOffInWalkingOnlyCities()
    {
        MovementOptions Walking(Action<MovementOptions>? change = null) => Small(o => { o.WalkingOnly = true; change?.Invoke(o); });
        var plain = MovementRun.Run(Walking(o => o.Driver = false));
        Assert.False(plain.Metrics.TryGetValue("journeys.car.count", out double cars) && cars > 0);
        void Equal(MovementResult arm, MovementResult reference, string name)
        {
            var comparison = Equivalence.Compare(arm, reference);
            Assert.True(comparison.Identical, name + ": " + string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        }
        var stationary = MovementRun.Run(Walking());
        Assert.True(stationary.Metrics["bodies.walkerFrames"] > 0 && stationary.Metrics["walkers.lagSamples"] > 0);
        Assert.Equal(0, stationary.Metrics["cars.lagSamples"]);
        Equal(stationary, plain, "stationary");
        Equal(MovementRun.Run(Walking(o => o.Focus = FocusPath.Sweep)), plain, "sweep");
        Equal(MovementRun.Run(Walking(o => { o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64; })), plain, "caps 256");
        Equal(MovementRun.Run(Walking(o => o.Bodies.Lag = LagModel.Ideal)), plain, "ideal lag");
        var failures = MovementRun.Run(Walking(o => o.Bodies.AcquisitionFailurePercent = 50));
        Assert.True(failures.Metrics["bodies.failedAcquisitions"] > 0);
        Equal(failures, plain, "50 % acquisition failures");
        var reload = MovementRun.Run(Walking(o => o.ReloadMidJourney = true));
        Assert.True(reload.BodiesAtReload > 0);
        Equal(reload, plain, "reload with walkers bound");
        Equal(MovementRun.Run(Walking(o => o.HiddenTravel = true)), MovementRun.Run(Walking(o => { o.Driver = false; o.HiddenTravel = true; })), "hidden travel");
        Equal(MovementRun.Run(Walking(o => o.FramesPerSecond = 30)), MovementRun.Run(Walking(o => { o.Driver = false; o.FramesPerSecond = 30; })), "30 fps");
        // Native lag makes walkers fall behind; beyond 8 m they are moved (the double has no camera, so off screen).
        Assert.True(stationary.Metrics["walkers.moves"] > 0);
    }

    /// <summary>F2d: cars follow the traffic kernel, so in the mixed city (walkers and cars on the capacity-2 signalled
    /// grid) every body arm equals bodies-off at the same cadence (E1): focus paths, caps, native lag including slow
    /// visible head cars, acquisition failures, cadence, a mid-journey reload with bodies bound and hidden travel.
    /// Equality covers save bytes, sales, journeys, parked cars, wallets and per-link FIFO order. Before F2d this was
    /// the recorded divergence (red).</summary>
    [Fact]
    public void MixedCityBodyArmsEqualBodiesOff()
    {
        void Equal(MovementResult arm, MovementResult reference, string name)
        {
            var comparison = Equivalence.Compare(arm, reference);
            Assert.True(comparison.Identical, name + ": " + string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        }
        var stationary = BodiesRun.Value;
        Assert.True(stationary.Metrics["bodies.carFrames"] > 0 && stationary.Metrics["cars.lagSamples"] > 0);
        Assert.True(PlainRun.Value.QueuedFifoSamples > 0);
        Equal(stationary, PlainRun.Value, "stationary");
        Equal(MovementRun.Run(Small(o => o.Focus = FocusPath.Sweep)), PlainRun.Value, "sweep");
        Equal(MovementRun.Run(Small(o => o.Focus = FocusPath.AwayReturn)), PlainRun.Value, "away/return");
        Equal(MovementRun.Run(Small(o => { o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1; })), PlainRun.Value, "caps 1/1");
        Equal(MovementRun.Run(Small(o => { o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64; })), PlainRun.Value, "caps 256/64");
        Equal(MovementRun.Run(Small(o => o.Bodies.Lag = LagModel.Ideal)), PlainRun.Value, "ideal lag");
        var slow = MovementRun.Run(Small(o => o.Bodies.Lag = LagModel.SlowHeadCar));
        Assert.True(slow.Metrics["cars.corrections"] > 0 || slow.Metrics["cars.catchUpFrames"] > 0, "slow head cars fall behind the kernel");
        Equal(slow, PlainRun.Value, "slow visible head cars");
        var failures = MovementRun.Run(Small(o => o.Bodies.AcquisitionFailurePercent = 50));
        Assert.True(failures.Metrics["bodies.failedAcquisitions"] > 0);
        Equal(failures, PlainRun.Value, "50 % acquisition failures");
        Equal(MovementRun.Run(Small(o => o.Bodies.AcquisitionFailurePercent = 10)), PlainRun.Value, "10 % acquisition failures");
        var reload = MovementRun.Run(Small(o => o.ReloadMidJourney = true));
        Assert.True(reload.BodiesAtReload > 0);
        Equal(reload, PlainRun.Value, "reload with bodies bound");
        Equal(MovementRun.Run(Small(o => o.HiddenTravel = true)), MovementRun.Run(Plain(o => o.HiddenTravel = true)), "hidden travel");
        foreach (int fps in new[] { 30, 120 })
            Equal(MovementRun.Run(Small(o => o.FramesPerSecond = fps)), MovementRun.Run(Plain(o => o.FramesPerSecond = fps)), fps + " fps");
    }

    [Fact]
    public void ArmCatalogueCoversThePlanRow()
    {
        var arms = MovementArms.All(Residents, 1);
        Assert.Equal(arms.Count, arms.Select(a => a.Name).Distinct().Count());
        foreach (var arm in arms.Where(a => a.Reference.Length > 0))
            Assert.Contains(arms, a => a.Name == arm.Reference);
        var options = arms.Select(a => a.Options()).ToList();
        Assert.Equal(new[] { FocusPath.None, FocusPath.Stationary, FocusPath.Sweep, FocusPath.AwayReturn },
            options.Where(o => o.Driver).Select(o => o.Focus).Distinct().OrderBy(f => f));
        Assert.Equal(MovementArms.Caps, options.Where(o => o.Driver && o.Focus == FocusPath.Stationary && o.Bodies.Lag == LagModel.Native
            && o.Bodies.AcquisitionFailurePercent == 0 && o.FramesPerSecond == 60 && !o.ReloadMidJourney && !o.HiddenTravel)
            .Select(o => (o.Bodies.Pedestrians, o.Bodies.Vehicles)).Distinct().OrderBy(c => c.Pedestrians));
        Assert.Equal(new[] { 0, 10, 50 }, options.Where(o => o.Driver).Select(o => o.Bodies.AcquisitionFailurePercent).Distinct().OrderBy(p => p));
        Assert.Equal(new[] { 30, 60, 120 }, options.Where(o => o.Driver).Select(o => o.FramesPerSecond).Distinct().OrderBy(f => f));
        Assert.Equal(3, options.Where(o => o.Driver).Select(o => o.Bodies.Lag).Distinct().Count());
        Assert.Contains(options, o => o.Driver && o.ReloadMidJourney);
        Assert.Contains(options, o => o.Driver && o.HiddenTravel);
        Assert.All(options, o => Assert.Equal(Residents, o.Residents));
    }
}
