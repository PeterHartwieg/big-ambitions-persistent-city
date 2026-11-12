using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PersistentCity.MovementEquivalence;
using Xunit;

namespace PersistentCity.Tests;

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

    /// <summary>P-C: one path per leg follows as the F2c pose aim did. Both equal the bodies-off run on every projection,
    /// the walker lag is no worse, and the path calculations drop. The ahead count is structural here: the driver steps a
    /// body at most its lag (MoveBodies), so it checks the AheadMetres accounting, not native movement. Native movement
    /// against the pose is covered by PhysicalWalkerFollowTests (BeforeNavigation, steps before Update, skipped Updates)
    /// and in game by pc movement status ahead_frames.</summary>
    [Fact]
    public void OnePathPerLegFollowsLikeThePoseAimWithFewerPathCalculations()
    {
        var legs = BodiesRun.Value;
        var pose = MovementRun.Run(Small(o => o.Bodies.PoseAim = true));
        foreach (var run in new[] { legs, pose })
        {
            var comparison = Equivalence.Compare(run, PlainRun.Value);
            Assert.True(comparison.Identical, string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        }
        Assert.True(legs.Metrics["walkers.aheadFrames"] == 0 && pose.Metrics["walkers.aheadFrames"] == 0,
            string.Join(" ", new[] { legs, pose }.Select(r => r.Metrics["walkers.aheadFrames"] + "/" + r.Metrics["walkers.aheadMaxMetres"]
                + "/" + r.Metrics["walkers.lagP95Metres"] + "/" + r.Metrics["walkers.pathRequests"])));
        Assert.True(legs.Metrics["walkers.lagP95Metres"] <= pose.Metrics["walkers.lagP95Metres"]);
        Assert.True(legs.Metrics["walkers.lagMaxMetres"] < PersistentCity.Core.WalkerFollow.CorrectionMetres + 0.1, "max lag " + legs.Metrics["walkers.lagMaxMetres"]);
        Assert.True(legs.Metrics["walkers.pathRequests"] > 0);
        Assert.True(legs.Metrics["walkers.pathRequests"] * 4 < pose.Metrics["walkers.pathRequests"],
            legs.Metrics["walkers.pathRequests"] + " vs " + pose.Metrics["walkers.pathRequests"]);
    }

    [Fact]
    public void HiddenTravelReleasesBodiesBeforeTheHiddenSliceAdvances()
    {
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
        Assert.True(stationary.Metrics["walkers.moves"] > 0);
    }

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
        var visitors = options.Where(o => o.Driver && o.Visitors).ToList();
        Assert.Equal(new[] { FocusPath.None, FocusPath.Stationary, FocusPath.Sweep, FocusPath.AwayReturn }, visitors.Select(o => o.Focus).Distinct().OrderBy(f => f));
        Assert.Equal(MovementArms.Caps, visitors.Select(o => (o.Bodies.Pedestrians, o.Bodies.Vehicles)).Distinct().OrderBy(c => c.Pedestrians));
        Assert.Equal(new[] { 0, 10, 50 }, visitors.Select(o => o.Bodies.AcquisitionFailurePercent).Distinct().OrderBy(p => p));
        Assert.Equal(new[] { 30, 60, 120 }, visitors.Select(o => o.FramesPerSecond).Distinct().OrderBy(f => f));
        Assert.Equal(3, visitors.Select(o => o.Bodies.Lag).Distinct().Count());
        Assert.Contains(visitors, o => o.ReloadMidJourney);
        Assert.Contains(visitors, o => o.HiddenTravel);
        var combined = arms.Where(a => a.Family == "combined").Select(a => a.Options()).ToList();
        Assert.Contains(combined, o => o.ReloadMidJourney && o.FramesPerSecond != 60 && o.Bodies.AcquisitionFailurePercent > 0);
        Assert.Contains(combined, o => o.HiddenTravel && o.Bodies.Lag == LagModel.SlowHeadCar);
        Assert.Contains(combined, o => o.Visitors && o.ReloadMidJourney);
        Assert.Contains(combined, o => o.Visitors && o.HiddenTravel);
    }

    [Fact]
    public void CombinedArmsEqualBodiesOff()
    {
        void Equal(MovementResult arm, MovementResult reference, string name)
        {
            var comparison = Equivalence.Compare(arm, reference);
            Assert.True(comparison.Identical, name + ": " + string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        }
        foreach (int fps in new[] { 30, 120 })
            Equal(MovementRun.Run(Plain(o => o.FramesPerSecond = fps)), PlainRun.Value, "bodies-off " + fps + " fps");
        var sweep = MovementRun.Run(Small(o => { o.Focus = FocusPath.Sweep; o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64;
            o.Bodies.AcquisitionFailurePercent = 10; o.FramesPerSecond = 120; }));
        Assert.True(sweep.Metrics["bodies.failedAcquisitions"] > 0);
        Equal(sweep, PlainRun.Value, "sweep 256/64 10 % 120 fps");
        var reload = MovementRun.Run(Small(o => { o.Focus = FocusPath.AwayReturn; o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1;
            o.Bodies.AcquisitionFailurePercent = 50; o.FramesPerSecond = 30; o.ReloadMidJourney = true; }));
        Assert.Equal(1, reload.Reloads);
        Assert.True(reload.BodiesAtReload > 0);
        Equal(reload, PlainRun.Value, "away/return 1/1 50 % 30 fps reload");
        var hidden = MovementRun.Run(Small(o => { o.Bodies.Pedestrians = 24; o.Bodies.Vehicles = 12; o.Bodies.AcquisitionFailurePercent = 50;
            o.Bodies.Lag = LagModel.SlowHeadCar; o.HiddenTravel = true; }));
        Assert.True(hidden.Metrics["bodies.hiddenTravelFrames"] > 0);
        Equal(hidden, PlainRun.Value, "stationary 24/12 50 % slow head cars hidden travel");
    }

    [Fact]
    public void PairwiseArmsCoverEveryPairOfTheFullProduct()
    {
        var arms = MovementArms.All(Residents, 1);
        var pairwise = arms.Where(a => a.Family == "pairwise").ToList();
        var covered = pairwise.Select(a => a.Options()).Select(o => new MovementArms.Factors(o.Visitors, o.Focus,
                (o.Bodies.Pedestrians, o.Bodies.Vehicles), o.Bodies.AcquisitionFailurePercent, o.FramesPerSecond, o.Bodies.Lag,
                o.ReloadMidJourney, o.HiddenTravel))
            .SelectMany(t => { var l = t.Levels; return Enumerable.Range(0, l.Length).SelectMany(a => Enumerable.Range(a + 1, l.Length - a - 1)
                .Select(b => (a, l[a], b, l[b]))); })
            .ToHashSet();
        Assert.All(covered, p => Assert.True(p.Item2 >= 0 && p.Item4 >= 0, "level outside the plan factors"));
        var valid = MovementArms.ValidPairs();
        Assert.Empty(valid.Except(covered));
        var all = new System.Collections.Generic.HashSet<(int, int, int, int)>();
        for (int a = 0; a < MovementArms.FactorSizes.Length; a++)
            for (int b = a + 1; b < MovementArms.FactorSizes.Length; b++)
                for (int la = 0; la < MovementArms.FactorSizes[a]; la++)
                    for (int lb = 0; lb < MovementArms.FactorSizes[b]; lb++)
                        all.Add((a, la, b, lb));
        Assert.Equal(new[] { (1, 0, 6, 1), (1, 0, 6, 3), (2, 0, 6, 1), (2, 0, 6, 3) }, all.Except(valid).OrderBy(p => p));
        Assert.Contains(pairwise, a => a.Options() is { ReloadMidJourney: true, HiddenTravel: true, Visitors: true });
        Assert.Contains(pairwise, a => a.Options() is { ReloadMidJourney: true, HiddenTravel: true, Visitors: false });
        foreach (var arm in pairwise)
        {
            var o = arm.Options();
            Assert.Equal(MovementArms.PlainReference(o.Visitors, o.FramesPerSecond, o.HiddenTravel), arm.Reference);
        }
    }

    [Fact]
    public void PairwiseArmsEqualBodiesOff()
    {
        var arms = MovementArms.All(Residents, 1).ToDictionary(a => a.Name, StringComparer.Ordinal);
        var references = new System.Collections.Generic.Dictionary<string, MovementResult>(StringComparer.Ordinal);
        foreach (var arm in arms.Values.Where(a => a.Family == "pairwise"))
        {
            if (!references.TryGetValue(arm.Reference, out var reference))
                references[arm.Reference] = reference = MovementRun.Run(arms[arm.Reference].Options());
            var options = arm.Options();
            var result = MovementRun.Run(options);
            if (options.ReloadMidJourney) Assert.True(result.Reloads == 1 && result.BodiesAtReload > 0, arm.Name + " reloads with bodies bound");
            if (options.HiddenTravel) Assert.True(result.Metrics["bodies.hiddenTravelFrames"] > 0, arm.Name + " travels hidden");
            var comparison = Equivalence.Compare(result, reference);
            Assert.True(comparison.Identical, arm.Name + ": " + string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        }
    }

    [Fact]
    public void ReloadArmThatNeverReloadsFailsLoudly()
    {
        var error = Assert.Throws<HarnessViolation>(() => MovementRun.Run(Small(o => { o.Focus = FocusPath.None; o.ReloadMidJourney = true; })));
        Assert.Contains("reload never happened", error.Message);
    }

    [Fact]
    public void E2ReplayIsGreenWithVisitorBodies()
    {
        var rows = HoldReplay.Check(Residents, 1, visitors: true);
        Assert.All(rows, r => Assert.True(r.Pass, r.Check + ": " + r.Detail));
        Assert.Contains(rows, r => r.Check == "e2 visitors visitor bodies bound and only residents held");
        Assert.Equal(3, rows.Count(r => r.Check.StartsWith("e2 visitors replay from", StringComparison.Ordinal)));
        Assert.Equal(5, rows.Count(r => r.Check.StartsWith("e2 visitors replay refuses", StringComparison.Ordinal)));
    }

    private static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;

    [Fact]
    public void RecordedBaselineIsTheGreenMatrix()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tests", "movement-equivalence", "baseline.json")));
        var root = json.RootElement;
        Assert.Equal(MovementRun.Schema, root.GetProperty("schema").GetString());
        Assert.Equal(MovementFixture.Residents, root.GetProperty("residents").GetInt32());
        Assert.Equal(MovementRun.Days, root.GetProperty("days").GetInt32());
        var selfChecks = root.GetProperty("selfChecks").EnumerateArray().ToList();
        var e2 = root.GetProperty("e2").EnumerateArray().ToList();
        Assert.Equal(9, selfChecks.Count);
        Assert.All(selfChecks.Concat(e2), c => Assert.True(c.GetProperty("pass").GetBoolean(), c.GetProperty("check").GetString()));
        foreach (string prefix in new[] { "e2 replay", "e2 visitors replay" })
        {
            Assert.Equal(3, e2.Count(c => c.GetProperty("check").GetString()!.StartsWith(prefix + " from", StringComparison.Ordinal)));
            foreach (string refusal in new[] { "gap", "duplicate", "reordering", "wrong start", "wrong end" })
                Assert.Contains(e2, c => c.GetProperty("check").GetString() == prefix + " refuses a " + refusal);
        }
        var recorded = root.GetProperty("arms").EnumerateArray().ToDictionary(a => a.GetProperty("arm").GetString()!, StringComparer.Ordinal);
        var catalogue = MovementArms.All(MovementFixture.Residents, MovementRun.Days);
        Assert.Equal(catalogue.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal), recorded.Keys.OrderBy(n => n, StringComparer.Ordinal));
        foreach (var arm in catalogue.Where(a => a.Reference.Length > 0))
        {
            var comparison = recorded[arm.Name].GetProperty("comparison");
            Assert.Equal(arm.Reference, comparison.GetProperty("Reference").GetString());
            Assert.True(comparison.GetProperty("Identical").GetBoolean(), arm.Name + " differs from " + arm.Reference);
            Assert.Equal(Equivalence.Projections.OrderBy(p => p, StringComparer.Ordinal),
                comparison.GetProperty("Equal").EnumerateObject().Select(p => p.Name).OrderBy(p => p, StringComparer.Ordinal));
        }
    }
}
