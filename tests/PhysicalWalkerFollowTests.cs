using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly Position Home = new(40, 0, 0), Work = new(100, 0, 0), Venue = new(100, 0, 20);

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
            new Place { Id = "venue", Name = "Venue", Kind = PlaceKind.Leisure, Capacity = 2, Position = Venue } }, "f2c", "0.40.4", 3, Now);
        var c = state.Citizens[0];
        c.HomeId = "home"; c.DayPlaceId = "work"; c.MealOuting = null;
        if (optional)
        {
            c.PlaceId = "venue"; c.Activity = Activity.Leisure; c.Phase = CitizenPhase.AtPlace; c.Journey = null;
            c.NextEventMinute = c.ActivityUntilMinute = Now + 10;
            var spot = new Position(104, 0, 20);
            c.OptionalStop = new OptionalStopState { Kind = DwellKind.WindowShopping, PlaceId = "venue", SpotId = "solo:w", TargetId = "front:w",
                Position = spot, Facing = new Position(104, 0, 21), ReturnPosition = Venue, StartMinute = Now - 1, EndMinute = Now + 5,
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
            PhysicalPedestrians.VisiblePoint = null;
            NativeTravelPace.Focus = Work; Time.Next(3);
            City = new CitySimulator(Build(journey?.Invoke(), optional), new Walk());
            Plain = new CitySimulator(Build(journey?.Invoke(), optional), new Walk());
            Bodies = new(new Transform { Component = new PopulationRuntime() }, City, new MultimodalRoutes(), typeof(Fixture).Assembly,
                new VendorBodyTests.Log(), 8, 3, 100, new DwellSpots());
        }
        internal void Tick(float seconds = 0.3f) { Time.Next(seconds); Bodies.Update(); }
        // A frame in which the city clock runs (by a negligible 0.0001 game minutes), so the follow clock advances too.
        internal void Run(float seconds = 0.3f) { Advance(City.State.Minute + 0.0001); Tick(seconds); }
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
        public void Dispose() { PhysicalPedestrians.VisiblePoint = null; Bodies.Dispose(); }
    }

    [Fact]
    public void WalkLegsAreNeverLeasedAndArriveAtTheLogicalMinuteWithTheBodyTenMetresBehind()
    {
        using var f = new Fixture();
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(Home, f.Body.Position);
        // 25 minutes in: the logical pose is at 90 m; the body is held 10 m behind it, on screen.
        f.Advance(Now + 25);
        f.Body.OnScreen = true; f.Place(new Position(80, 0, 0));
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(NativeTravelPace.WalkingMetresPerSecond * WalkerFollow.MaximumPaceFactor, f.Body.Speed, 9);
        Assert.Equal(Work, f.Body.Target); // P-C: its destination is its leg's corner; the commanded speed keeps it behind the pose
        f.AssertSameAsBodiesOff();
        // The arrival happens at the logical minute, exactly as without bodies, while the body is still 10 m out.
        f.Advance(Now + 30);
        Assert.Equal(CitizenPhase.AtPlace, f.W.Phase); Assert.Equal("work", f.W.PlaceId); Assert.Equal(1, f.W.CompletedJourneys);
        f.AssertSameAsBodiesOff();
        // Arrival tail: the body keeps walking to the door.
        f.Tick();
        Assert.True(f.Bound); Assert.True(f.Body.Speed > 0); Assert.Empty(f.Corrections());
        // While the game is paused (the city clock stands still) the tail bound does not run.
        for (int i = 0; i < 5; i++) f.Tick(3);
        Assert.True(f.Bound);
        // On screen on the last leg the tail is not cut short: past the 10 s bound it still walks to the door, unfaded.
        for (int i = 0; i < 6; i++) f.Run(3);
        Assert.True(f.Bound); Assert.True(f.Body.Speed > 0); Assert.Equal(1, f.Body.Alpha); Assert.Empty(f.Corrections());
        // Out of view after the bound it is released, recorded as an off-screen lag release.
        f.Body.OnScreen = false;
        f.Run();
        Assert.False(f.Bound);
        var release = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.False(release.OnScreen);
        Assert.Equal(new Position(80, 0, 0), release.Before); Assert.Equal(Work, release.After);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AVisibleArrivalTailPastTheBoundFadesAtTheNextIntermediateCorner()
    {
        using var f = new Fixture();
        long onScreenMoves = PhysicalPopulation.Telemetry.OnScreenMoves;
        f.Tick();
        // The arrival is processed while the body is still at home, 30 m before the midpoint corner. The person then
        // stands outside the door on an optional stop, so selection would give it a body there.
        f.Advance(Now + 30);
        foreach (var city in new[] { f.City, f.Plain })
            city.State.Citizens[0].OptionalStop = new OptionalStopState { Kind = DwellKind.Break, PlaceId = "work", SpotId = "solo:w",
                TargetId = "front:w", Position = new Position(104, 0, 0), Facing = new Position(104, 0, 1), ReturnPosition = Work,
                StartMinute = Now + 30, EndMinute = Now + 38, VisitMinute = Now + 30, ResumeEventMinute = Now + 300 };
        // Set directly, not by an event, so it is filed into the selection index by hand.
        f.City.ActiveJourneys.File(f.W);
        f.Body.OnScreen = true;
        f.Tick();
        Assert.True(f.Bound);
        // Past the 10 s bound, mid-leg, it keeps walking to the corner: no fade between corners.
        for (int i = 0; i < 4; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.True(f.Body.Speed > 0); Assert.Empty(f.Corrections());
        // At the corner (70 m) it stops and fades out there, instead of keeping its route and body slot.
        f.Place(new Position(70, 0, 0));
        var body = f.Body;
        f.Run();
        Assert.True(f.Bound); Assert.Equal(0, body.Speed);
        f.Run(0.6f);
        Assert.True(f.Bound); Assert.InRange(body.Alpha, 0.3f, 0.5f); Assert.Equal(new Position(70, 0, 0), body.Position);
        f.Run(0.6f);
        Assert.False(f.Bound); Assert.Equal(0, body.Alpha); Assert.Equal(0, body.Warps);
        var fade = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.Fade, fade.Kind); Assert.True(fade.OnScreen);
        Assert.Equal(new Position(70, 0, 0), fade.Before); Assert.Equal(Work, fade.After);
        Assert.Equal(onScreenMoves, PhysicalPopulation.Telemetry.OnScreenMoves);
        f.AssertSameAsBodiesOff();
        // Arrived, it does not reappear at its optional stop while that is in view...
        PhysicalPedestrians.VisiblePoint = _ => true;
        f.Run(2.1f); f.Run(2.1f);
        Assert.False(f.Bound);
        // ...and reappears there once it is out of view.
        PhysicalPedestrians.VisiblePoint = null;
        f.Run(0.3f);
        Assert.True(f.Bound);
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
        for (int i = 0; i < 5; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Warps); Assert.Single(f.Corrections()); Assert.Equal(1, f.Body.Alpha);
        // Held mid-leg well past the hurry bound, it does not fade there: it waits for a corner.
        for (int i = 0; i < 5; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.Single(f.Corrections()); Assert.True(f.Body.Speed > 0);
        // At the next corner (the midpoint at 70 m) it stops and fades out there.
        f.Place(new Position(70, 0, 0));
        var body = f.Body;
        f.Run();
        Assert.True(f.Bound); Assert.Equal(0, body.Speed); Assert.Equal(1, body.Warps);
        f.Run(0.6f);
        Assert.True(f.Bound); Assert.InRange(body.Alpha, 0.3f, 0.5f); Assert.Equal(new Position(70, 0, 0), body.Position);
        f.Run(0.6f);
        Assert.False(f.Bound); Assert.Equal(0, body.Alpha);
        var corrections = f.Corrections();
        Assert.Equal(2, corrections.Count);
        Assert.Equal(WalkerCorrectionKind.Fade, corrections[1].Kind); Assert.True(corrections[1].OnScreen);
        Assert.Equal(new Position(70, 0, 0), corrections[1].Before); Assert.True(Position.Distance(new Position(80, 0, 0), corrections[1].After) < 0.01);
        Assert.Equal(corrections[0].Sequence + 1, corrections[1].Sequence);
        Assert.Equal(onScreenMoves, PhysicalPopulation.Telemetry.OnScreenMoves);
        f.AssertSameAsBodiesOff();
        // It does not reappear while its logical pose is in view, however many other restrictions are held...
        var restrictions = typeof(PhysicalPopulation).GetField("fadedUntilHidden", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f.Bodies)!;
        var add = restrictions.GetType().GetMethod("Add")!;
        for (int i = 0; i < 600; i++) add.Invoke(restrictions, new object[] { "other" + i, (f.W, f.W.Journey, (string?)null) });
        PhysicalPedestrians.VisiblePoint = _ => true;
        f.Run(2.1f); f.Run(2.1f);
        Assert.False(f.Bound);
        // ...and reappears at the logical pose once that is out of view. A deferral costs no retry time.
        PhysicalPedestrians.VisiblePoint = null;
        f.Run(0.3f);
        Assert.True(f.Bound); Assert.True(Position.Distance(new Position(80, 0, 0), f.Body.Position) < 0.01);
        Assert.Equal(1, f.Body.Alpha);
        Assert.Contains("followers=1", PhysicalPopulation.Telemetry.Status(f.Bodies.Followers));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheBodyNeverPassesTheLogicalPoseWhileTheClockStandsStillWhateverTheFrameDurations(bool projectionAhead)
    {
        using var f = new Fixture();
        // A kerb whose nearest walkable point lies 0.3 m further along the leg: the corner path never projects a pose.
        if (projectionAhead) NavMesh.Sample = p => new Vector3(p.x + 0.3f, p.y + 0.1f, p.z);
        f.Tick();
        long requests = PhysicalPopulation.Telemetry.PathRequests, ahead = PhysicalPopulation.Telemetry.AheadFrames;
        f.Advance(Now + 0.1); // logical pose at 40.2 m; the body stands at 40 m
        // Native movement double: the agent walks toward its destination at the commanded speed and stops there. Unity
        // moves it after Update in the same frame, for that frame's duration (an overrun moves it for longer).
        void Native(float seconds)
        {
            var body = f.Body;
            if (body.Agent.isStopped) return;
            double d = Position.Distance(body.Position, body.Target), step = body.Speed * seconds;
            f.Place(d <= step ? body.Target : Position.Lerp(body.Position, body.Target, step / d));
        }
        // Long and varying frames while the clock stands still: the destination stays the leg's corner (one path for
        // the leg) and the speed cap stops the body at the pose.
        foreach (float frame in new[] { 1 / 60f, 0.5f, 1 / 120f, 0.2f, 1 / 60f, 1f, 0.05f, 2f })
        {
            f.Tick(frame);
            Assert.Equal(new Position(70, 0, 0), f.Body.Target);
            Native(frame);
            Assert.True(f.Body.Position.X <= 40.2 + 1e-4, "body at " + f.Body.Position.X);
        }
        f.Tick(0.5f);
        Assert.Equal(0, f.Body.Speed); Assert.True(f.Body.Agent.isStopped);
        Assert.True(Position.Distance(new Position(40.2, 0, 0), f.Body.Position) < 1e-4);
        Assert.Equal(requests, PhysicalPopulation.Telemetry.PathRequests); // the leg's path from the first frame
        Assert.Equal(ahead, PhysicalPopulation.Telemetry.AheadFrames);
        // A native overrun (the agent moved for longer than the frame its speed was set for) puts the body ahead of the
        // pose: that frame is counted, the body stands until the pose passes it, and nobody is corrected or re-aimed.
        f.Advance(Now + 0.2); // pose at 40.4 m
        f.Tick(1 / 60f);
        Native(0.5f);
        Assert.True(f.Body.Position.X > 40.4 + WalkerFollow.AheadToleranceMetres, "body at " + f.Body.Position.X);
        f.Tick(1 / 60f);
        Assert.Equal(0, f.Body.Speed);
        Assert.Equal(ahead + 1, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.True(PhysicalPopulation.Telemetry.MaximumAheadMetres > WalkerFollow.AheadToleranceMetres);
        f.Advance(Now + 1); // pose at 42 m, ahead of the body again
        f.Tick(1 / 60f);
        Assert.True(f.Body.Speed > 0); Assert.Equal(new Position(70, 0, 0), f.Body.Target);
        Assert.Equal(requests, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Equal(ahead + 1, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.Empty(f.Corrections());
        Assert.Contains("ahead_frames=", PhysicalPopulation.Telemetry.Status(1));
    }

    [Fact]
    public void TheLastLegIsAimedAtTheDoorOnceAndTheTailReachesIt()
    {
        using var f = new Fixture();
        f.Tick();
        f.Body.OnScreen = true;
        f.Advance(Now + 29.8); // the logical pose is at 99.6 m on the last leg
        f.Place(new Position(99.5, 0, 0));
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Tick();
        Assert.Equal(Work, f.Body.Target);
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests);
        f.Place(new Position(99.6, 0, 0)); // the agent stopped at the pose
        f.Advance(Now + 30); // arrival: the door is 0.4 m on
        f.Run();
        Assert.True(f.Bound); Assert.Equal(Work, f.Body.Target); Assert.True(f.Body.Speed > 0);
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests); // the leg's path is kept through the tail
        f.Place(f.Body.Target);
        f.Run();
        Assert.False(f.Bound); // it reached the door and went in
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AWholeWalkCalculatesOnePathPerLegAndNeverRunsAheadOfThePose()
    {
        using var f = new Fixture();
        long requests = PhysicalPopulation.Telemetry.PathRequests, ahead = PhysicalPopulation.Telemetry.AheadFrames;
        double minute = Now;
        // 60 m in two legs at 2 m per game minute, one game minute per real second at 60 frames a second; the native
        // agent moves after Update at the commanded speed for the frame.
        f.Tick(1 / 60f);
        f.Body.OnScreen = true;
        int frames = 0;
        for (; frames < 60 * 40 && f.Bound; frames++)
        {
            minute += 1 / 60.0; f.Advance(minute); f.Tick(1 / 60f);
            if (!f.Bound) break;
            var body = f.Body;
            if (!body.Agent.isStopped)
            {
                double d = Position.Distance(body.Position, body.Target), step = body.Speed / 60;
                f.Place(d <= step ? body.Target : Position.Lerp(body.Position, body.Target, step / d));
            }
            if (f.W.Phase == CitizenPhase.Travelling)
                Assert.True(body.Position.X <= f.City.PositionOf(f.W).X + 1e-6, "ahead at frame " + frames);
        }
        Assert.False(f.Bound); // it walked the route and went in at the door
        // Two legs, two paths (the F2c pose aim re-aimed every 0.5 m of pose progress, about 120 for this walk).
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Equal(ahead, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AheadIsMeasuredOnlyOnThePosesOwnLeg()
    {
        // A U-turn: 10 m east, then back west. The pose is 1 m into the return leg (x = 9).
        var route = new Route { Legs = new() { Leg(new Position(0, 0, 0), new Position(10, 0, 0)), Leg(new Position(10, 0, 0), new Position(0, 0, 0.2)) } };
        var logical = WalkerFollow.Sample(route, 5.5);
        // A body at 8 m on the first leg lies on the return leg's line, 1 m "past" the pose there: it is behind.
        Assert.Equal(0, WalkerFollow.AheadMetres(WalkerFollow.Sample(route, 4), logical, new Position(8, 0, 0)));
        // On the pose's own leg, 0.5 m further west than the pose is ahead.
        Assert.InRange(WalkerFollow.AheadMetres(logical, logical, new Position(8.5, 0, 0.1)), 0.45, 0.55);
        Assert.Equal(0, WalkerFollow.AheadMetres(logical, logical, new Position(9.5, 0, 0)));
    }

    [Fact]
    public void AnInterruptedLegResumesWithTheSameIdentityAndOneNewPath()
    {
        using var f = new Fixture();
        f.Tick();
        var first = f.Pool.Acquisitions[^1];
        f.Advance(Now + 5); // logical pose at 50 m on the first leg
        f.Place(new Position(49.5, 0, 0));
        f.Tick();
        Assert.Equal(new Position(70, 0, 0), f.Body.Target);
        // The walk is interrupted: the focus moves away and the body goes back to the pool.
        NativeTravelPace.Focus = new Position(1000, 0, 1000);
        f.Tick();
        Assert.False(f.Bound);
        f.AssertSameAsBodiesOff();
        // Back in view: the pooled actor is reused for the same person, who resumes the same leg at the logical pose.
        NativeTravelPace.Focus = Work;
        f.Advance(Now + 6);
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        for (int i = 0; i < 20 && !f.Bound; i++) f.Tick();
        Assert.True(f.Bound);
        var again = f.Pool.Acquisitions[^1];
        Assert.Same(first.Actor, again.Actor);
        Assert.Same(first.Identity, again.Identity); Assert.Equal("w", f.Body.IdentityId);
        Assert.True(again.Generation > first.Generation);
        Assert.True(Position.Distance(new Position(52, 0, 0), f.Body.Position) < 1e-6);
        Assert.Equal(new Position(70, 0, 0), f.Body.Target);
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests);
        // The agent drops its path mid-leg (a native reset): the corner is calculated once more, then held.
        f.Body.Agent.hasPath = false;
        f.Advance(Now + 7); f.Tick(); f.Tick();
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests);
        Assert.True(f.Body.Speed > 0);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void APoseBetweenCornersIsProjectedOntoTheWalkableSurfaceAndAFailedAimKeepsWalking()
    {
        using var f = new Fixture();
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Tick();
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests); // the first leg's corner path
        // A crowned road: the walkable surface is 0.12 m above the straight leg, and the native endpoint check
        // refuses any destination off that surface, the leg's corner included. The agent drops its path.
        NavMesh.Sample = p => new Vector3(p.x, 0.12f, p.z);
        PhysicalPedestrianBody.Unreachable = p => Math.Abs(p.Y - 0.12) > 0.01;
        f.Body.Agent.hasPath = false;
        f.Advance(Now + 2); // logical pose at 44 m, the body 4 m behind at 40 m
        f.Tick();
        // The corner is refused, so this leg falls back to the F2c pose aim, at the projected pose.
        Assert.True(Position.Distance(new Position(44, 0.12, 0), f.Body.Target) < 1e-4);
        Assert.False(f.Body.Agent.isStopped); Assert.True(f.Body.Speed > 0);
        Assert.Equal(requests + 3, PhysicalPopulation.Telemetry.PathRequests);
        // The projection is found but its path is refused: the raw pose is tried too, and both calculations count.
        PhysicalPedestrianBody.Unreachable = p => p.Y > 0.05;
        f.Advance(Now + 3);
        f.Tick();
        Assert.True(Position.Distance(new Position(46, 0, 0), f.Body.Target) < 1e-4);
        Assert.Equal(requests + 5, PhysicalPopulation.Telemetry.PathRequests);
        // Projection and the raw pose both fail: it keeps walking to its previous destination, retrying once a second.
        NavMesh.Sample = _ => null;
        PhysicalPedestrianBody.Unreachable = _ => true;
        f.Advance(Now + 3.5);
        int calls = f.Body.SetTargets;
        for (int i = 0; i < 10; i++)
        {
            f.Tick(0.05f);
            Assert.True(Position.Distance(new Position(46, 0, 0), f.Body.Target) < 1e-4);
            Assert.False(f.Body.Agent.isStopped); Assert.True(f.Body.Speed > 0);
        }
        Assert.Equal(calls + 1, f.Body.SetTargets); // one failed attempt in half a second, not one per frame
        f.Tick(0.6f);
        Assert.Equal(calls + 2, f.Body.SetTargets);
        Assert.False(f.Body.Agent.isStopped);
        Assert.Equal(requests + 5 + 2, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Contains("path_requests=", PhysicalPopulation.Telemetry.Status(1));
    }

    [Fact]
    public void APoseThatIsNotAnExactFloatStillUsesItsStraightUpProjection()
    {
        using var f = new Fixture();
        f.Tick();
        NavMesh.Sample = p => new Vector3(p.x, 0.12f, p.z);
        PhysicalPedestrianBody.Unreachable = p => Math.Abs(p.Y - 0.12) > 0.01;
        f.Body.Agent.hasPath = false; // the corner path is calculated again and refused: the pose fallback
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Advance(Now + 4.1234 / 2); // logical pose at x = 44.1234, which rounds up (ahead) as a float
        f.Tick();
        Assert.True(Math.Abs(f.Body.Target.Y - 0.12) < 1e-4, "target " + f.Body.Target);
        Assert.True(Math.Abs(f.Body.Target.X - 44.1234) < 1e-4);
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests); // the refused corner, then the projection; no raw retry
        Assert.False(f.Body.Agent.isStopped);
    }

    [Fact]
    public void AnOpaqueBodyIsNotFadedButKeptUntilItIsOutOfView()
    {
        using var f = new Fixture();
        f.Tick();
        f.Body.OnScreen = true; f.Body.CanFade = false;
        f.Advance(Now + 20);
        for (int i = 0; i < 5; i++) f.Run(3);
        // At the corner, past the hurry bound, it cannot fade: it keeps following, visible and unfaded.
        f.Place(new Position(70, 0, 0));
        for (int i = 0; i < 4; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.Empty(f.Body.Fades); Assert.Empty(f.Corrections());
        // Once out of view it is moved to the logical pose.
        f.Body.OnScreen = false;
        f.Run();
        Assert.True(f.Bound);
        var move = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.Move, move.Kind); Assert.False(move.OnScreen);
    }

    [Fact]
    public void ArrivalTailHandsOverToAJourneyCommittedFromTheDoorAndEndsForOneFromElsewhere()
    {
        using (var f = new Fixture())
        {
            f.Tick();
            f.Advance(Now + 30);
            f.Tick();
            Assert.True(f.Bound);
            // A new journey commits from the door while the body is still on its way there.
            var next = Legs(Work, new Position(100, 0, 60));
            f.W.Phase = CitizenPhase.Travelling; f.W.Journey = next; f.W.DepartureMinute = f.City.State.Minute - 1;
            f.Tick();
            Assert.True(f.Bound); Assert.Equal(new Position(70, 0, 0), f.Body.Target);
            f.Place(Work);
            f.Tick(); f.Tick();
            Assert.True(f.Bound);
            Assert.Equal(new Position(100, 0, 30), f.Body.Target); // following the new journey, aimed at its first leg's corner
            Assert.True(f.Body.Speed > 0);
            Assert.Empty(f.Corrections());
        }
        using (var f = new Fixture())
        {
            f.Tick();
            f.Advance(Now + 30);
            f.Tick();
            // A journey starting somewhere else ends the tail at once (off screen: released and recorded).
            f.W.Phase = CitizenPhase.Travelling; f.W.Journey = Legs(new Position(0, 0, 90), Home);
            f.Tick();
            Assert.False(f.Bound);
            var release = Assert.Single(f.Corrections());
            Assert.Equal(WalkerCorrectionKind.LagRelease, release.Kind); Assert.False(release.OnScreen);
        }
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
        // Close behind a person who has just boarded, off screen: an ordinary handoff, not a correction.
        using var near = new Fixture(Transit);
        near.Tick();
        near.Advance(Now + 15.5);
        near.Place(new Position(69.9, 0, 0));
        near.Tick();
        Assert.False(near.Bound);
        Assert.Empty(near.Corrections());
        Assert.Equal(near.Handoffs + 1, PhysicalPopulation.Telemetry.Handoffs);
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
        f.Place(new Position(104, 0, 20)); // at the stop's spot
        f.Tick();
        // The stop ends: the body turns back to the entrance.
        f.Advance(Now + 6);
        f.Tick();
        Assert.Equal(Venue, f.Body.Target);
        // The next journey commits while the body is still on its way back. Nothing holds it.
        f.Advance(Now + 10);
        Assert.Equal(CitizenPhase.Travelling, f.W.Phase);
        Assert.Equal(f.Plain.State.Citizens[0].DepartureMinute, f.W.DepartureMinute);
        f.Tick();
        Assert.True(f.Bound); Assert.Equal(Venue, f.Body.Target);
        f.AssertSameAsBodiesOff();
        // Back at the entrance it follows the journey with catch-up.
        f.Advance(Now + 12);
        Assert.Equal(CitizenPhase.Travelling, f.W.Phase);
        f.Place(Venue);
        f.Tick(); f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(f.W.Journey!.Legs[0].To, f.Body.Target); // its leg's corner
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
