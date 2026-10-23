using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalJourneyTests
{
    private sealed class Planner : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 12, Mode = TravelMode.Walk } } };
    }

    private static CitySimulator Engine(int count = 1)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "c" + i, Name = "Person " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(120, 0, 0) }
        }, "physical-test", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + 12; c.ActivityUntilMinute = 2880;
            c.Journey = new Planner().Plan(state.Places[0], state.Places[1], state.Minute, c.Preferences);
        }
        return new CitySimulator(state, new Planner());
    }

    private static PhysicalJourneyLease Acquire(CitySimulator engine, int index = 0)
    {
        Assert.True(engine.TryAcquirePhysicalJourney(engine.State.Citizens[index].Identity.Id, out var lease));
        return Assert.IsType<PhysicalJourneyLease>(lease);
    }

    [Fact]
    public void BlockedVisibleJourneyCannotArriveOrBeAdmittedAtNominalTime()
    {
        var engine = Engine(); var c = engine.State.Citizens[0]; var lease = Acquire(engine);
        engine.AdvanceTo(2000);
        Assert.Equal(CitizenPhase.Travelling, c.Phase); Assert.Equal(0, c.CompletedJourneys); Assert.Empty(c.RecentVisits);
        Assert.Equal(0, engine.PositionOf(c).X); Assert.Equal(2012, c.NextEventMinute);
        Assert.True(engine.ObservePhysicalJourney(lease, 6));
        Assert.Equal(60, engine.PositionOf(c).X);
        engine.AdvanceTo(2500);
        Assert.Equal(60, engine.PositionOf(c).X); Assert.Empty(c.RecentVisits);
        Assert.True(engine.ObservePhysicalJourney(lease, 12));
        Assert.False(engine.IsPhysicalJourneyValid(lease)); Assert.Empty(c.RecentVisits);
        Assert.Equal(1, engine.AdvanceTo(2500, 1).Events);
        Assert.Equal(1, c.CompletedJourneys); Assert.Single(c.RecentVisits);
        Assert.Equal(2500, c.RecentVisits[0].AtMinute);
    }

    [Fact]
    public void LatePhysicalArrivalUsesActualWindowAndDoesNotInventAdmission()
    {
        var engine = Engine(); var c = engine.State.Citizens[0]; var lease = Acquire(engine);
        engine.AdvanceTo(3000); engine.ObservePhysicalJourney(lease, 12); engine.AdvanceTo(3000, 1);
        Assert.Equal(1, c.CompletedJourneys); Assert.True(c.MissedActivities > 0);
        Assert.DoesNotContain(c.RecentVisits, v => v.PlaceId == "work");
    }

    [Fact]
    public void ReleaseAndReloadContinueFromMeasuredPointAndRetainDelay()
    {
        var engine = Engine(); var lease = Acquire(engine);
        engine.AdvanceTo(1600); engine.ObservePhysicalJourney(lease, 3);
        var read = SaveCodec.Read(SaveCodec.Write(engine.State)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new Planner());
        Assert.Equal(30, resumed.PositionOf(resumed.State.Citizens[0]).X);
        Assert.True(engine.ReleasePhysicalJourney(lease));
        engine.AdvanceTo(1608); resumed.AdvanceTo(1608);
        Assert.Equal(CitizenPhase.Travelling, resumed.State.Citizens[0].Phase);
        Assert.Equal(110, resumed.PositionOf(resumed.State.Citizens[0]).X);
        engine.AdvanceTo(1700); resumed.AdvanceTo(1700);
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
    }

    [Fact]
    public void MultipleLeasesAndBoundedBacklogSaveRemainCoherent()
    {
        var engine = Engine(8); var a = Acquire(engine); var b = Acquire(engine, 1);
        engine.ObservePhysicalJourney(a, 2); engine.ObservePhysicalJourney(b, 4);
        var result = engine.AdvanceTo(5000, 1);
        Assert.False(result.CaughtUp); Assert.Equal(1, result.Events); Assert.True(engine.State.Minute < 5000);
        Assert.Equal(20, engine.PositionOf(engine.State.Citizens[0]).X);
        Assert.Equal(40, engine.PositionOf(engine.State.Citizens[1]).X);
        var read = SaveCodec.Read(SaveCodec.Write(engine.State)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new Planner());
        engine.ReleasePhysicalJourney(a); engine.ReleasePhysicalJourney(b);
        while (!engine.AdvanceTo(5000, 3).CaughtUp) { }
        while (!resumed.AdvanceTo(5000, 2).CaughtUp) { }
        Assert.Equal(SaveCodec.Write(engine.State), SaveCodec.Write(resumed.State));
    }

    [Fact]
    public void CompletedLeaseArrivalStillHonoursBudgetAndOtherLeases()
    {
        var engine = Engine(3); var held = Acquire(engine); var completed = Acquire(engine, 2);
        engine.AdvanceTo(1452, 1);
        engine.ObservePhysicalJourney(completed, 12);
        Assert.Equal(0, engine.State.Citizens[2].CompletedJourneys);
        var saved = SaveCodec.Read(SaveCodec.Write(engine.State)); Assert.Equal(SaveReadStatus.Loaded, saved.Status);
        Assert.Equal(1, engine.AdvanceTo(1452, 1).Events);
        Assert.Equal(1, engine.State.Citizens[2].CompletedJourneys);
        Assert.Equal(CitizenPhase.Travelling, engine.State.Citizens[0].Phase);
        Assert.True(engine.IsPhysicalJourneyValid(held));
    }

    [Fact]
    public void ReleasedAndForeignHandlesCannotControlReacquiredJourney()
    {
        var engine = Engine(); var old = Acquire(engine);
        Assert.False(engine.TryAcquirePhysicalJourney(old.CitizenId, out _));
        engine.ReleasePhysicalJourney(old); var current = Acquire(engine);
        Assert.True(current.Generation > old.Generation);
        Assert.False(engine.ObservePhysicalJourney(old, 1)); Assert.False(engine.ReleasePhysicalJourney(old));
        Assert.False(Engine().ObservePhysicalJourney(current, 1));
        var c = engine.State.Citizens[0]; c.Identity = new CitizenIdentity { Id = current.CitizenId };
        Assert.False(engine.IsPhysicalJourneyValid(current)); Assert.False(engine.ObservePhysicalJourney(current, 1));
    }

    [Fact]
    public void RemovedCitizenInvalidatesLease()
    {
        var engine = Engine(); var lease = Acquire(engine); engine.State.Citizens.Clear();
        Assert.False(engine.TrySamplePhysicalJourney(lease, out _)); Assert.False(engine.ReleasePhysicalJourney(lease));
        Assert.Throws<InvalidOperationException>(() => engine.AdvanceTo(1500));
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(13)]
    public void InvalidObservationCannotChangeProgress(double progress)
    {
        var engine = Engine(); var lease = Acquire(engine);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.ObservePhysicalJourney(lease, progress));
        Assert.True(engine.TrySamplePhysicalJourney(lease, out var sample)); Assert.Equal(0, sample.ElapsedMinutes);
    }

    [Fact]
    public void EqualAndZeroProgressAreValidButRegressionAndClockOverflowAreRejected()
    {
        var engine = Engine(); var lease = Acquire(engine);
        engine.ObservePhysicalJourney(lease, 0); engine.ObservePhysicalJourney(lease, 2); engine.ObservePhysicalJourney(lease, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.ObservePhysicalJourney(lease, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AdvanceTo(1e9));
        Assert.Equal(1440, engine.State.Minute);
    }

    [Theory]
    [InlineData(TravelMode.Car)] [InlineData(TravelMode.Subway)]
    public void StationaryAndTravelBoundarySamplesAndReleasePreserveGeometry(TravelMode mode)
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        c.Preferences.OwnsCar = mode == TravelMode.Car;
        if (c.Preferences.OwnsCar) c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(20, 0, 0) };
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk },
            new RouteLeg { From = new Position(20,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk, ServiceId = "preparation" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(100,0,0), Minutes = 6, Mode = mode },
            new RouteLeg { From = new Position(100,0,0), To = new Position(120,0,0), Minutes = 2, Mode = TravelMode.Walk }
        };
        var sample = engine.SampleJourney(c, 2); Assert.True(sample.Stationary); Assert.Equal(mode, sample.NextMode);
        Assert.Equal(2, sample.RemainingLegMinutes); Assert.Equal(0, sample.Direction.X);
        var lease = Acquire(engine); engine.ObservePhysicalJourney(lease, 4);
        Assert.True(engine.TrySamplePhysicalJourney(lease, out sample)); Assert.Equal(2, sample.LegIndex); Assert.Equal(mode, sample.Mode);
        Assert.Equal(0, sample.LegFraction); Assert.Equal(1, sample.Direction.X);
        engine.ReleasePhysicalJourney(lease); engine.AdvanceTo(1443);
        Assert.Equal(60, engine.PositionOf(c).X);
        if (mode == TravelMode.Car) Assert.Equal(60, engine.CarPositionOf(c)!.Value.X);
        Assert.Equal(100, engine.SampleJourney(c, 10).CarPosition?.X ?? 100);
        Assert.True(engine.SampleJourney(c, 12).AtEnd);
    }
    [Fact]
    public void FractionalPrefixesCannotTrapACompletedPhysicalCornerOrFinalArrival()
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        double[] prefix = { 2.2355080526319506, 14.833594490199058, 4.9053881575314895,
            2.7993631912176262, 2.0588785716684344, 14.815948215906838 };
        c.Journey!.Legs.Clear();
        for (int i = 0; i < prefix.Length; i++)
            c.Journey.Legs.Add(new RouteLeg { From = new Position(i,0,0), To = new Position(i+1,0,0), Minutes = prefix[i], Mode = TravelMode.Walk });
        c.Journey.Legs.Add(new RouteLeg { From = new Position(6,0,0), To = new Position(7,0,0), Minutes = 10.91187700962951, Mode = TravelMode.Walk });
        c.Journey.Legs.Add(new RouteLeg { From = new Position(7,0,0), To = new Position(120,0,0), Minutes = .7, Mode = TravelMode.Walk });
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        // Reconstruct to rebuild the due queue after preparing the committed route fixture.
        engine = new CitySimulator(engine.State, new Planner());
        var sample = engine.SampleJourney(c, 48.09206235010644);
        double corner = sample.ElapsedMinutes + (1 - sample.LegFraction) * sample.LegMinutes;
        var next = engine.SampleJourney(c, corner);
        Assert.Equal(7, next.LegIndex); Assert.Equal(0, next.LegFraction); Assert.Equal(7, next.Position.X);
        double final = next.ElapsedMinutes + (1 - next.LegFraction) * next.LegMinutes;
        var end = engine.SampleJourney(c, final);
        Assert.True(end.AtEnd); Assert.Equal(c.Journey.Minutes, end.ElapsedMinutes); Assert.Equal(1, end.LegFraction);
        // A legitimately unfinished point remains on the old leg.
        Assert.Equal(6, engine.SampleJourney(c, corner - .000001).LegIndex);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(6)]
    public void SamplingJustAboveFractionalCornerNeverRegressesAnActiveLeaseAcrossReload(int prefixCount)
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        double[] durations = { 2.2355080526319506, 14.833594490199058, 4.9053881575314895,
            2.7993631912176262, 2.0588785716684344, 14.815948215906838 };
        c.Journey!.Legs.Clear(); double boundary = 0;
        for (int i = 0; i < prefixCount; i++)
        {
            boundary += durations[i];
            c.Journey.Legs.Add(new RouteLeg { From = new Position(i,0,0), To = new Position(i+1,0,0), Minutes = durations[i], Mode = TravelMode.Walk });
        }
        c.Journey.Legs.Add(new RouteLeg { From = new Position(prefixCount,0,0), To = new Position(120,0,0), Minutes = 10.91187700962951, Mode = TravelMode.Walk });
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        engine = new CitySimulator(engine.State, new Planner());
        var lease = Acquire(engine); engine.AdvanceTo(3000);
        double observed = Math.BitIncrement(boundary);
        Assert.True(engine.ObservePhysicalJourney(lease, observed));
        for (int pass = 0; pass < 2; pass++)
        {
            Assert.True(engine.TrySamplePhysicalJourney(lease, out var sample));
            Assert.True(sample.ElapsedMinutes >= observed, "Sampling must never lower the lease's observed progress.");
            Assert.True(PhysicalProgress.TryProject(sample, sample.Position, false, out double projected));
            Assert.True(projected >= observed);
            Assert.True(engine.ObservePhysicalJourney(lease, projected));
            var loaded = SaveCodec.Read(SaveCodec.Write(engine.State)); Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            engine = new CitySimulator(loaded.State!, new Planner()); c = engine.State.Citizens[0];
            observed = engine.State.Minute - c.DepartureMinute;
            lease = Acquire(engine);
        }
        // Final-leg normalization and the projector's total cap cannot regress either.
        observed = Math.BitDecrement(c.Journey!.Minutes);
        Assert.True(engine.ObservePhysicalJourney(lease, observed));
        Assert.True(engine.TrySamplePhysicalJourney(lease, out var end));
        Assert.True(PhysicalProgress.TryProject(end, end.To, false, out double completed));
        Assert.Equal(c.Journey.Minutes, completed); Assert.True(completed >= observed);
        Assert.True(engine.ObservePhysicalJourney(lease, completed));
    }

    [Fact]
    public void CompletedLeaseAtLargeFractionalClockCanSampleAndRetimeBeforeQueuedArrival()
    {
        var random = new Random(3417);
        for (int example = 0; example < 128; example++)
        {
            var engine = Engine(); var state = engine.State; var c = state.Citizens[0];
            state.Minute = 144000.37 + example * .1;
            c.Journey!.Legs.Clear(); int count = 3 + example % 8;
            for (int leg = 0; leg < count; leg++)
                c.Journey.Legs.Add(new RouteLeg { From = new Position(120.0 * leg / count,0,0),
                    To = new Position(120.0 * (leg+1) / count,0,0), Minutes = .01 + random.NextDouble() * 70, Mode = TravelMode.Walk });
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + c.Journey.Minutes;
            engine = new CitySimulator(state, new Planner()); var lease = Acquire(engine);
            double total = c.Journey.Minutes;
            Assert.True(engine.ObservePhysicalJourney(lease, total));
            var arrived = engine.SampleJourney(c); Assert.True(arrived.AtEnd); Assert.Equal(120, arrived.Position.X);
            state.TravelTimingVersion = 1; state.TravelClockRate = 1;
            Assert.True(TravelTiming.Apply(state, 2, 2));
            Assert.Equal(state.Minute, c.NextEventMinute);
            Assert.True(engine.SampleJourney(c).AtEnd);
            // A meaningful overshoot remains invalid rather than being hidden by the roundoff fix.
            c.DepartureMinute -= .000001;
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SampleJourney(c));
            Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 3));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ConstantTimeSlotGuardRejectsSameCountReplacementOrReordering(bool replace)
    {
        var engine = Engine(2); var lease = Acquire(engine); string id = lease.CitizenId;
        if (replace) engine.State.Citizens[0] = new CitizenState { Identity = new CitizenIdentity { Id = id } };
        else (engine.State.Citizens[0], engine.State.Citizens[1]) = (engine.State.Citizens[1], engine.State.Citizens[0]);
        Assert.Equal(2, engine.State.Citizens.Count);
        Assert.False(engine.IsPhysicalJourneyValid(lease)); Assert.False(engine.TrySamplePhysicalJourney(lease, out _));
        Assert.False(engine.ObservePhysicalJourney(lease, 1)); Assert.False(engine.ReleasePhysicalJourney(lease));
        Assert.False(engine.TryAcquirePhysicalJourney(id, out _));
        Assert.Throws<InvalidOperationException>(() => engine.AdvanceTo(1500));
    }

    [Fact]
    public void ConstantTimeSlotGuardAlsoRejectsAcquisitionAfterUnleasedReorder()
    {
        var engine = Engine(2); string id = engine.State.Citizens[0].Identity.Id;
        (engine.State.Citizens[0], engine.State.Citizens[1]) = (engine.State.Citizens[1], engine.State.Citizens[0]);
        Assert.False(engine.TryAcquirePhysicalJourney(id, out _));
    }

}
