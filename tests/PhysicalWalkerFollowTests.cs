using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;

namespace PersistentCity.Tests;

// F2c (docs/plans/f2-movement.md Decision 2): walkers follow the logical pose and never write it. The production
// PhysicalPopulation runs on the native scene doubles; a body moves only when a test moves it, so every lag here is
// one the test made. Each case runs a plain twin city without bodies and compares save bytes (E1).
[Collection(StreetFoodHooks.Collection)]
public sealed class PhysicalWalkerFollowTests
{
    private const double Now = 1440 + 600;
    private static readonly Position Home = new(40, 0, 0), Work = new(100, 0, 0);

    // Two walk legs through the midpoint, 2 m per game minute.
    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => Legs(a.Position, b.Position);
    }
    private static RouteLeg Leg(Position a, Position b, TravelMode mode = TravelMode.Walk)
        => new RouteLeg { From = a, To = b, Mode = mode, Minutes = Math.Max(1, Position.Distance(a, b) / 2) };
    internal static Route Legs(Position a, Position b)
    {
        var mid = Position.Lerp(a, b, 0.5);
        return new Route { Legs = new() { Leg(a, mid), Leg(mid, b) } };
    }

    private static CityState Build(Route? journey, bool optional)
    {
        var ids = new List<CitizenIdentity> { new() { Id = "w", Name = "W", Age = 30, AppearanceSeed = 3 } };
        var state = Population.Create(ids, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2, Position = Home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 2, Position = Work },
            new Place { Id = "venue", Name = "Venue", Kind = PlaceKind.Leisure, Capacity = 2, Position = Work } }, "f2c", "0.40.4", 3, Now);
        var c = state.Citizens[0];
        c.HomeId = "home"; c.DayPlaceId = "work"; c.MealOuting = null;
        if (optional)
        {
            c.PlaceId = "venue"; c.Activity = Activity.Leisure; c.Phase = CitizenPhase.AtPlace; c.Journey = null;
            c.NextEventMinute = c.ActivityUntilMinute = Now + 10;
            var spot = new Position(104, 0, 0);
            c.OptionalStop = new OptionalStopState { Kind = DwellKind.WindowShopping, PlaceId = "venue", SpotId = "solo:w", TargetId = "front:w",
                Position = spot, Facing = new Position(104, 0, 1), ReturnPosition = Work, StartMinute = Now - 1, EndMinute = Now + 5,
                VisitMinute = Now - 1, ResumeEventMinute = Now + 10 };
            return state;
        }
        c.PlaceId = "home"; c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
        c.Journey = journey ?? Legs(Home, Work);
        c.DepartureMinute = Now; c.NextEventMinute = Now + c.Journey.Minutes; c.ActivityUntilMinute = Now + 300;
        return state;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly CitySimulator City, Plain;
        internal readonly PhysicalPopulation Bodies;
        internal readonly long FirstSequence = PhysicalPopulation.Telemetry.LastSequence;
        internal readonly long Handoffs = PhysicalPopulation.Telemetry.Handoffs;
        internal PhysicalPedestrians Pool => PhysicalPedestrians.Last!;
        internal CitizenState W => City.State.Citizens[0];
        internal Fixture(Func<Route>? journey = null, bool optional = false)
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            NativeTravelPace.Focus = Work; Time.Next(3);
            City = new CitySimulator(Build(journey?.Invoke(), optional), new Walk());
            Plain = new CitySimulator(Build(journey?.Invoke(), optional), new Walk());
            Bodies = new(new Transform { Component = new PopulationRuntime() }, City, new MultimodalRoutes(), typeof(Fixture).Assembly,
                new VendorBodyTests.Log(), 8, 3, 100, new DwellSpots());
        }
        internal void Tick(float seconds = 0.3f) { Time.Next(seconds); Bodies.Update(); }
        internal void Advance(double minute) { City.AdvanceTo(minute); Plain.AdvanceTo(minute); }
        internal PhysicalPedestrianBody Body => Pool.Active["w"];
        internal bool Bound => Pool.Active.ContainsKey("w");
        internal void Place(Position at) => Body.Character.transform.position = PhysicalPedestrianBody.Vector(at);
        internal List<WalkerCorrection> Corrections()
        {
            var list = new List<WalkerCorrection>();
            PhysicalPopulation.Telemetry.Since(FirstSequence, list, out bool gap);
            Assert.False(gap);
            return list;
        }
        internal void AssertSameAsBodiesOff()
        {
            // ObserveForSave runs Core's own optional-stop refresh, which PersistentCityMod also runs every frame.
            Bodies.BeforeSave(); Plain.RefreshOptionalStops();
            Assert.Equal(SaveCodec.Write(Plain.State), SaveCodec.Write(City.State));
        }
        public void Dispose() => Bodies.Dispose();
    }

    [Fact]
    public void WalkLegsAreNeverLeasedAndArriveAtTheLogicalMinuteWithTheBodyTenMetresBehind()
    {
        using var f = new Fixture();
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(Home, f.Body.Position);
        Assert.False(f.City.TryAcquirePhysicalJourney("w", out _));
        // 25 minutes in: the logical pose is at 90 m; the body is held 10 m behind it, on screen.
        f.Advance(Now + 25);
        f.Body.OnScreen = true; f.Place(new Position(80, 0, 0));
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(NativeTravelPace.WalkingMetresPerSecond * WalkerFollow.MaximumPaceFactor, f.Body.Speed, 9);
        Assert.Equal(new Position(100, 0, 0), f.Body.Target);
        f.AssertSameAsBodiesOff();
        // The arrival happens at the logical minute, exactly as without bodies, while the body is still 10 m out.
        f.Advance(Now + 30);
        Assert.Equal(CitizenPhase.AtPlace, f.W.Phase); Assert.Equal("work", f.W.PlaceId); Assert.Equal(1, f.W.CompletedJourneys);
        f.AssertSameAsBodiesOff();
        // Arrival tail: the body keeps walking to the door, then goes in. Short of the door after 10 s, it is a lag release.
        f.Tick();
        Assert.True(f.Bound); Assert.True(f.Body.Speed > 0); Assert.Empty(f.Corrections());
        for (int i = 0; i < 3; i++) f.Tick(3);
        Assert.True(f.Bound);
        f.Tick(3);
        Assert.False(f.Bound);
        var release = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.True(release.OnScreen);
        Assert.Equal(new Position(80, 0, 0), release.Before); Assert.Equal(Work, release.After);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void ArrivalTailAtTheDoorGoesInWithoutACorrection()
    {
        using var f = new Fixture();
        f.Tick();
        f.Advance(Now + 30);
        f.Tick();
        Assert.True(f.Bound);
        f.Place(Work);
        f.Tick();
        Assert.False(f.Bound);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void EveryCorrectionIsRecordedWithItsOnScreenFlagAndNobodyMovesOnScreen()
    {
        using var f = new Fixture();
        long onScreenMoves = PhysicalPopulation.Telemetry.OnScreenMoves;
        f.Tick();
        // Off screen and 20 m behind: moved to the logical pose.
        f.Advance(Now + 10);
        f.Tick();
        var move = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.Move, move.Kind); Assert.False(move.OnScreen); Assert.Equal("w", move.PersonId);
        Assert.Equal(Home, move.Before); Assert.Equal(new Position(60, 0, 0), move.After);
        Assert.Equal(new Position(60, 0, 0), f.Body.Position); Assert.Equal(1, f.Body.Warps);
        Assert.Equal(Time.frameCount, move.Frame);
        // On screen and 20 m behind again: it hurries; nothing is recorded and it is not moved.
        f.Body.OnScreen = true;
        f.Advance(Now + 20);
        for (int i = 0; i < 5; i++) f.Tick(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Warps); Assert.Single(f.Corrections());
        // After 10 s of hurrying it fades at the next corner (the midpoint at 70 m) and reappears later.
        f.Place(new Position(70, 0, 0));
        f.Tick();
        Assert.False(f.Bound);
        var corrections = f.Corrections();
        Assert.Equal(2, corrections.Count);
        Assert.Equal(WalkerCorrectionKind.Fade, corrections[1].Kind); Assert.True(corrections[1].OnScreen);
        Assert.Equal(new Position(70, 0, 0), corrections[1].Before); Assert.Equal(new Position(80, 0, 0), corrections[1].After);
        Assert.Equal(corrections[0].Sequence + 1, corrections[1].Sequence);
        Assert.Equal(onScreenMoves, PhysicalPopulation.Telemetry.OnScreenMoves);
        f.AssertSameAsBodiesOff();
        // It reappears at the logical pose after the retry interval.
        f.Tick(2.1f);
        Assert.True(f.Bound); Assert.Equal(new Position(80, 0, 0), f.Body.Position);
        Assert.Contains("followers=1", PhysicalPopulation.Telemetry.Status(f.Bodies.Followers));
    }

    [Fact]
    public void FollowEndsWhenTheJourneyIsReplaced()
    {
        using var f = new Fixture();
        f.Tick();
        Assert.True(f.Bound);
        f.W.Journey = Legs(Home, new Position(40, 0, 60));
        f.Tick();
        Assert.False(f.Bound);
        Assert.Empty(f.Corrections());
    }

    [Fact]
    public void ModeBoundaryReacquiresAWalkerAtTheLogicalPoseWithoutTheRetryCooldown()
    {
        Route Transit() => new Route { Legs = new() { Leg(Home, new Position(70, 0, 0)),
            Leg(new Position(70, 0, 0), new Position(90, 0, 0), TravelMode.Subway), Leg(new Position(90, 0, 0), Work) } };
        using var f = new Fixture(Transit);
        f.Tick();
        // The body reached the station while the person is already on the subway: a handoff, not a correction.
        f.Advance(Now + 16);
        f.Body.OnScreen = true; f.Place(new Position(70, 0, 0));
        f.Tick();
        Assert.False(f.Bound);
        Assert.Empty(f.Corrections());
        Assert.Equal(f.Handoffs + 1, PhysicalPopulation.Telemetry.Handoffs);
        f.Tick();
        Assert.False(f.Bound); // no walker body for a subway leg
        // Back on foot: a walker is acquired at the logical pose at the next selection, well inside the 2 s retry.
        f.Advance(Now + 26);
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(new Position(92, 0, 0), f.Body.Position);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void OffScreenWalkerLeftBehindAModeBoundaryIsReleasedAndRecorded()
    {
        Route Transit() => new Route { Legs = new() { Leg(Home, new Position(70, 0, 0)),
            Leg(new Position(70, 0, 0), new Position(90, 0, 0), TravelMode.Subway), Leg(new Position(90, 0, 0), Work) } };
        using var f = new Fixture(Transit);
        f.Tick();
        f.Advance(Now + 16);
        f.Tick();
        Assert.False(f.Bound);
        var release = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.False(release.OnScreen);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void ReloadWithWalkersVisibleEqualsBodiesOff()
    {
        using var f = new Fixture();
        f.Tick();
        f.Advance(Now + 7);
        f.Place(new Position(50, 0, 0.1));
        f.Tick();
        Assert.True(f.Bound);
        f.AssertSameAsBodiesOff();
        var read = SaveCodec.Read(SaveCodec.Write(f.City.State));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var reloaded = new CitySimulator(read.State!, new Walk());
        foreach (double minute in new[] { Now + 12, Now + 30, Now + 400 })
        {
            f.Advance(minute); reloaded.AdvanceTo(minute);
            f.Tick();
            f.Bodies.BeforeSave(); f.Plain.RefreshOptionalStops(); reloaded.RefreshOptionalStops();
            string plain = SaveCodec.Write(f.Plain.State);
            Assert.Equal(plain, SaveCodec.Write(f.City.State));
            Assert.Equal(plain, SaveCodec.Write(reloaded.State));
        }
    }

    [Fact]
    public void DepartureTailKeepsWalkingBackWhileTheLogicalJourneyHasStarted()
    {
        using var f = new Fixture(optional: true);
        f.Tick();
        Assert.True(f.Bound);
        f.Place(new Position(104, 0, 0)); // at the stop's spot
        f.Tick();
        // The stop ends: the body turns back to the entrance.
        f.Advance(Now + 6);
        f.Tick();
        Assert.Equal(Work, f.Body.Target);
        // The next journey commits while the body is still on its way back. Nothing holds it.
        f.Advance(Now + 10);
        Assert.Equal(CitizenPhase.Travelling, f.W.Phase);
        Assert.Equal(f.Plain.State.Citizens[0].DepartureMinute, f.W.DepartureMinute);
        f.Tick();
        Assert.True(f.Bound); Assert.Equal(Work, f.Body.Target);
        f.AssertSameAsBodiesOff();
        // Back at the entrance it follows the journey with catch-up.
        f.Advance(Now + 12);
        f.Place(Work);
        f.Tick(); f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(f.W.Journey!.Legs[0].To, f.Body.Target);
        Assert.True(f.Body.Speed > NativeTravelPace.WalkingMetresPerSecond);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void FaultedAcquisitionsAreCountedAndChangeNothing()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear();
        NativeTravelPace.Focus = Work; Time.Next(3);
        var city = new CitySimulator(Build(null, false), new Walk());
        var plain = new CitySimulator(Build(null, false), new Walk());
        long faulted = PhysicalPopulation.Telemetry.FaultedAcquisitions;
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(Fixture).Assembly, new VendorBodyTests.Log(), 8, 3, 100, new DwellSpots(), faultAcquirePercent: 100);
        for (int i = 0; i < 5; i++) { Time.Next(2.1f); bodies.Update(); }
        Assert.False(PhysicalPedestrians.Last!.Active.ContainsKey("w"));
        Assert.Equal(faulted + 5, PhysicalPopulation.Telemetry.FaultedAcquisitions);
        Assert.Contains("unavailable=5", bodies.Diagnostics);
        city.AdvanceTo(Now + 40); plain.AdvanceTo(Now + 40);
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(city.State));
    }

    // Opt-in offline timing for docs/perf/f2c.md (PC_F2C_MEASURE): the follower arithmetic one walker costs per frame.
    [Fact]
    public void MeasureFollowerArithmeticPerBody()
    {
        string? path = System.Environment.GetEnvironmentVariable("PC_F2C_MEASURE");
        if (path == null) return;
        var city = new CitySimulator(Build(null, false), new Walk());
        var c = city.State.Citizens[0]; var route = c.Journey!;
        city.AdvanceTo(Now + 12);
        const int count = 200000;
        double sink = 0;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            var logical = city.SampleJourney(c);
            double body = WalkerFollow.Advance(route, 10, logical.ElapsedMinutes, new Position(60 + (i & 7) * 0.1, 0, 0.05));
            double lag = WalkerFollow.RouteMetres(route, body, logical.ElapsedMinutes);
            sink += lag + WalkerFollow.Sample(route, body).LegIndex + WalkerFollow.PaceFactor(lag);
        }
        timer.Stop();
        System.IO.File.AppendAllText(path, string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Follower arithmetic per body per frame (SampleJourney, Advance, RouteMetres, Sample, PaceFactor): {0:F3} us over {1} calls (checksum {2:F1}){3}",
            timer.Elapsed.TotalMilliseconds * 1000 / count, count, sink, System.Environment.NewLine));
    }
}
