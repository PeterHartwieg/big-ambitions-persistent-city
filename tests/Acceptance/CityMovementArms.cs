using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Tests.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests.Acceptance
{
    internal sealed record CityBodies(FocusPath Focus = FocusPath.Stationary, int Walkers = 48, int Cars = 24, int FailurePercent = 0,
        LagModel Lag = LagModel.Native, bool Leak = false)
    {
        public const double RadiusMetres = 120, AwayMetres = 1000, SweepDwellMinutes = 10;
        public const int SweepPoints = 6;
        public const double AwayFrom = 9 * 60, AwayTo = 15 * 60;
    }

    internal sealed partial record CityArm
    {
        public CityBodies? Bodies { get; init; }
        public string BodiesOff { get; init; } = "";

        public static IEnumerable<CityArm> BodyArms()
        {
            CityArm On(string name, string off, CityBodies bodies) => Arms().Single(a => a.Name == off) with { Name = "bodies-" + name, Bodies = bodies, BodiesOff = off };
            return new[]
            {
                On("stationary", "fps60", new CityBodies()),
                On("sweep", "fps60", new CityBodies(FocusPath.Sweep)),
                On("away-return", "fps60", new CityBodies(FocusPath.AwayReturn)),
                On("caps-1", "fps60", new CityBodies(Walkers: 1, Cars: 1)),
                On("caps-256", "fps60", new CityBodies(Walkers: 256, Cars: 64)),
                On("failures-50pct", "fps60", new CityBodies(FailurePercent: 50)),
                On("hidden-skip", "hidden-skip", new CityBodies()),
                On("reload", "reload", new CityBodies()),
            };
        }
    }

    internal sealed class CityBodyStats
    {
        public long Acquired, Released, FailedAcquisitions, BodyFrames, WalkerFrames, CarFrames, HiddenFrames, HiddenReleases;
        public long VisitorAcquired;
        public int MaxWalkers, MaxCars, MaxVisitorBodies, Drivers;
        public List<string> BoundAtReload = new();
        public int ReboundAfterReload;
        public long AcquiredAfterReload;
        public long AwayFramesWithBodies, ReturnFramesWithBodies;
        public double ReloadMinute = -1;
    }

    internal sealed partial class CityRun
    {
        public CityBodyStats? Bodies;
    }

    internal sealed partial class CityScenario
    {
        private PresentationDriver? driver;
        private readonly List<PresentationDriver> drivers = new();
        private readonly List<(CitizenState Citizen, bool Car)> followed = new();
        private HashSet<string>? followedAfterReload;
        private int firstDriverAfterReload = -1;
        private Position[]? sweep;
        private bool leaked;

        private PresentationDriver NewDriver()
        {
            var b = arm.Bodies!;
            var created = new PresentationDriver(city, new DriverSettings { Pedestrians = b.Walkers, Vehicles = b.Cars, RadiusMetres = CityBodies.RadiusMetres,
                AcquisitionFailurePercent = b.FailurePercent, Lag = b.Lag, Seed = CitySize.Seed });
            drivers.Add(created);
            return created;
        }

        private Position Focus(double minute)
        {
            var shop = city.FindPlace(CityFixture.ShopPlace(0)).Position;
            switch (arm.Bodies!.Focus)
            {
                case FocusPath.None: return new Position(double.NaN, 0, 0);
                case FocusPath.Sweep:
                    sweep ??= SweepPoints(city.State.Places);
                    return sweep[(int)(Math.Floor(minute / CityBodies.SweepDwellMinutes) % sweep.Length)];
                case FocusPath.AwayReturn:
                    double day = minute % 1440;
                    return day >= CityBodies.AwayFrom && day < CityBodies.AwayTo ? new Position(shop.X + CityBodies.AwayMetres, shop.Y, shop.Z) : shop;
                default: return shop;
            }
        }

        private static Position[] SweepPoints(IEnumerable<Place> places)
        {
            var homes = places.Where(p => p.Kind == PlaceKind.Home).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
            return Enumerable.Range(0, CityBodies.SweepPoints).Select(i => homes[i * homes.Count / CityBodies.SweepPoints].Position).ToArray();
        }

        partial void BodiesBeforeAdvance(bool hidden, float gameMinutes)
        {
            if (arm.Bodies == null) return;
            driver ??= NewDriver();
            if (hidden)
            {
                driver.SuspendBeforeHiddenAdvance();
                if (driver.Count != 0) throw new CityViolation(arm.Name + ": hidden skip advanced with bodies bound");
            }
            driver.MoveBodies(1.0 / arm.Fps, gameMinutes);
        }

        partial void BodiesAfterFrame(double nativeMinute, bool hidden)
        {
            if (arm.Bodies == null || driver == null) return;
            driver.Update(Focus(nativeMinute), hidden);
            if (hidden && driver.Count != 0) throw new CityViolation(arm.Name + ": a body is bound during the hidden skip");
            if (driver.Walkers > arm.Bodies.Walkers || driver.Cars > arm.Bodies.Cars) throw new CityViolation(arm.Name + ": bodies over the caps");
            if (arm.Bodies.Focus == FocusPath.AwayReturn && driver.Count > 0)
            {
                var stats = run.Bodies ??= new CityBodyStats();
                double day = nativeMinute % 1440;
                if (day >= CityBodies.AwayFrom + 1 && day < CityBodies.AwayTo) stats.AwayFramesWithBodies++;
                else if (day >= CityBodies.AwayTo) stats.ReturnFramesWithBodies++;
            }
            if (followedAfterReload != null || arm.Bodies.Leak)
            {
                followed.Clear();
                driver.Followed(followed);
                if (followedAfterReload != null) foreach (var f in followed) followedAfterReload.Add(f.Citizen.Identity.Id);
                if (arm.Bodies.Leak && !leaked && followed.Count > 0)
                {
                    followed[0].Citizen.CompletedJourneys++;
                    leaked = true;
                }
            }
        }

        partial void BodiesBeforeSave()
        {
            driver?.BeforeSave();
        }

        partial void BodiesAfterLoad()
        {
            if (arm.Bodies != null) driver = NewDriver();
        }

        partial void BodiesReadyToReload(CityReload state, ref bool ready)
        {
            if (arm.Bodies == null || state != CityReload.MidJourney) return;
            followed.Clear();
            driver?.Followed(followed);
            ready = followed.Any(f => !f.Car && f.Citizen.Phase == CitizenPhase.Travelling);
            if (!ready) return;
            run.Bodies ??= new CityBodyStats();
            run.Bodies.BoundAtReload = followed.Select(f => f.Citizen.Identity.Id).ToList();
            run.Bodies.ReloadMinute = city.View.Minute;
            followedAfterReload = new HashSet<string>(StringComparer.Ordinal);
            firstDriverAfterReload = drivers.Count;
        }

        partial void BodiesFinish()
        {
            if (arm.Bodies == null) return;
            var stats = run.Bodies ??= new CityBodyStats();
            stats.Drivers = drivers.Count;
            foreach (var d in drivers)
            {
                stats.Acquired += d.Acquired; stats.Released += d.Released; stats.FailedAcquisitions += d.FailedAcquisitions;
                stats.BodyFrames += d.BodyFrames; stats.WalkerFrames += d.WalkerFrames; stats.CarFrames += d.CarFrames;
                stats.HiddenFrames += d.HiddenFrames; stats.HiddenReleases += d.HiddenReleases; stats.VisitorAcquired += d.VisitorAcquired;
                stats.MaxWalkers = Math.Max(stats.MaxWalkers, d.MaxWalkers); stats.MaxCars = Math.Max(stats.MaxCars, d.MaxCars);
                stats.MaxVisitorBodies = Math.Max(stats.MaxVisitorBodies, d.MaxVisitorBodies);
            }
            if (followedAfterReload != null) stats.ReboundAfterReload = stats.BoundAtReload.Count(followedAfterReload.Contains);
            if (firstDriverAfterReload >= 0) stats.AcquiredAfterReload = drivers[firstDriverAfterReload].Acquired;
        }
    }
}

namespace PersistentCity.Tests
{
    [Collection(StreetFoodHooks.Collection)]
    public sealed class F9IntegratedCityMovementTests : IClassFixture<F9IntegratedCityTests.Runs>
    {
        private readonly F9IntegratedCityTests.Runs runs;
        private readonly ITestOutputHelper output;
        public F9IntegratedCityMovementTests(F9IntegratedCityTests.Runs runs, ITestOutputHelper output) { this.runs = runs; this.output = output; }

        public static IEnumerable<object[]> BodyArmNames() => CityArm.BodyArms().Select(a => new object[] { a.Name });

        [Theory, Trait("Size", "city-s"), MemberData(nameof(BodyArmNames))]
        public void SmallBodyArmEqualsBodiesOff(string arm) => BodyArmEqualsBodiesOff("city-s", arm);

        [Theory, Trait("Size", "city-m"), MemberData(nameof(BodyArmNames))]
        public void MediumBodyArmEqualsBodiesOff(string arm) => BodyArmEqualsBodiesOff("city-m", arm);

        private CityRun Run(string size, CityArm arm) => CityScenario.Run(runs.Checkpoint(size), arm);

        private void BodyArmEqualsBodiesOff(string size, string name)
        {
            var spec = CityArm.BodyArms().Single(a => a.Name == name);
            var reference = runs.Run(size, "reference");
            var off = runs.Run(size, spec.BodiesOff);
            var arm = Run(size, spec);
            CityOracles.Equal(reference, arm);
            CityOracles.Equal(off, arm);
            Assert.Equal(0, arm.HistoricalClockMisses);
            Assert.Equal(0, arm.HistoricalCheckouts);
            Assert.InRange(arm.MaxLag, 0, AccountingClock.MaximumLagMinutes);
            Assert.Equal(reference.Inputs.Count, arm.InputsPublished);
            Assert.Equal(reference.Postings, arm.Postings);

            var b = arm.Bodies!;
            var bodies = spec.Bodies!;
            Assert.True(b.Acquired > 0 && b.WalkerFrames > 0, $"{name}: no body was ever bound");
            Assert.InRange(b.MaxWalkers, 1, bodies.Walkers);
            Assert.Equal(0, b.CarFrames);
            switch (name)
            {
                case "bodies-caps-1": Assert.Equal(1, b.MaxWalkers); break;
                case "bodies-failures-50pct": Assert.True(b.FailedAcquisitions > 0, "no acquisition failed"); break;
                case "bodies-away-return":
                    Assert.Equal(0, b.AwayFramesWithBodies);
                    Assert.True(b.ReturnFramesWithBodies > 0, "no body after the return");
                    break;
                case "bodies-hidden-skip":
                    Assert.True(b.HiddenFrames > 0, "the skip never ran with bodies on");
                    break;
                case "bodies-reload":
                    foreach (CityReload state in Enum.GetValues(typeof(CityReload))) Assert.Contains(arm.Reloads, r => r.State == state);
                    Assert.NotEmpty(b.BoundAtReload);
                    Assert.Equal(b.ReloadMinute, arm.Reloads.Single(r => r.State == CityReload.MidJourney).Minute);
                    Assert.Equal(arm.Reloads.Count + 1, b.Drivers);
                    Assert.True(b.AcquiredAfterReload > 0, "no body was bound after the mid-journey reload");
                    break;
            }
            if (name != "bodies-failures-50pct") Assert.Equal(0, b.FailedAcquisitions);
            Assert.True(ActiveJourneyIndex.VisitorsReceiveBodies);
            Assert.True(b.VisitorAcquired > 0, "no visitor received a body");
            Assert.InRange(b.MaxVisitorBodies, 1, bodies.Walkers);
            output.WriteLine($"{size} {name}: {arm.Seconds:F2} s tool time ({arm.ToolMsPerDay:F0} ms per simulated day); bodies-off {spec.BodiesOff} "
                + $"{off.Seconds:F2} s; acquired {b.Acquired}, max walkers {b.MaxWalkers}, walker frames {b.WalkerFrames}, failed {b.FailedAcquisitions}, "
                + $"hidden frames {b.HiddenFrames}, visitor bodies {b.VisitorAcquired} (max {b.MaxVisitorBodies}), drivers {b.Drivers}, "
                + $"bound at reload {b.BoundAtReload.Count}, rebound {b.ReboundAfterReload}, acquired after it {b.AcquiredAfterReload}");
        }

        [Fact, Trait("Size", "city-s")]
        public void VisitorsReceiveBodiesAtThisBase()
        {
            Assert.True(ActiveJourneyIndex.VisitorsReceiveBodies);
            var sweep = Run("city-s", CityArm.BodyArms().Single(a => a.Name == "bodies-sweep"));
            Assert.NotEmpty(sweep.State.Visitors);
            Assert.True(sweep.Bodies!.VisitorAcquired > 0, "no visitor received a body");
            output.WriteLine($"visitors {sweep.State.Visitors.Count} at the end, visitor bodies acquired {sweep.Bodies!.VisitorAcquired}");
        }

        [Fact, Trait("Size", "city-s")]
        public void ABodyThatWritesToItsPersonFailsTheOracles()
        {
            var spec = CityArm.BodyArms().Single(a => a.Name == "bodies-stationary") with { Name = "bodies-leak", Bodies = new CityBodies(Leak: true) };
            var reference = runs.Run("city-s", "reference");
            var error = Assert.Throws<CityViolation>(() => CityOracles.Equal(reference, Run("city-s", spec)));
            output.WriteLine(error.Message.Length > 300 ? error.Message[..300] : error.Message);
        }
    }
}
