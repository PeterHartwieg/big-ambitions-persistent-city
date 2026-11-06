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
    public void DriverDoubleUsesTheLeaseApiForWalkersAndCars()
    {
        var m = BodiesRun.Value.Metrics;
        Assert.True(m["bodies.walkerFrames"] > 0 && m["bodies.carFrames"] > 0);
        Assert.True(m["bodies.leaseObservations"] > 0);
        Assert.True(m["bodies.maxWalkers"] <= 48 && m["bodies.maxCars"] <= 24);
        var failures = MovementRun.Run(Small(o => o.Bodies.AcquisitionFailurePercent = 50));
        Assert.True(failures.Metrics["bodies.failedAcquisitions"] > 0);
        var hidden = MovementRun.Run(Small(o => o.HiddenTravel = true));
        Assert.True(hidden.Metrics["bodies.hiddenTravelFrames"] > 0);
    }

    /// <summary>Today's recorded divergence (expected red). F2c turns walker arms and F2d all arms equal;
    /// those slices flip this assertion to equality.</summary>
    [Fact]
    public void TodayBodyArmsDivergeFromBodiesOff()
    {
        var comparison = Equivalence.Compare(BodiesRun.Value, PlainRun.Value);
        Assert.False(comparison.Identical);
        Assert.False(comparison.Equal["save"]);
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
