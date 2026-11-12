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

[Collection(StreetFoodHooks.Collection)]
public sealed class PhysicalWalkerFollowTests
{
    private const double Now = 1440 + 600;
    private static readonly Position Home = new(40, 0, 0), Work = new(100, 0, 0), Venue = new(100, 0, 20);

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
            Bodies.BeforeSave(); Plain.RefreshOptionalStops();
            Assert.Equal(SaveCodec.Write(Plain.State), SaveCodec.Write(City.State));
        }
        public void Dispose() { PhysicalPedestrians.VisiblePoint = null; PhysicalPedestrians.AppearOnScreen = false; Bodies.Dispose(); }
    }

    [Fact]
    public void WalkLegsAreNeverLeasedAndArriveAtTheLogicalMinuteWithTheBodyTenMetresBehind()
    {
        using var f = new Fixture();
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(Home, f.Body.Position);
        f.Advance(Now + 25);
        f.Body.OnScreen = true; f.Place(new Position(80, 0, 0));
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(NativeTravelPace.WalkingMetresPerSecond * WalkerFollow.MaximumPaceFactor, f.Body.Speed, 9);
        Assert.Equal(Work, f.Body.Target);
        f.AssertSameAsBodiesOff();
        f.Advance(Now + 30);
        Assert.Equal(CitizenPhase.AtPlace, f.W.Phase); Assert.Equal("work", f.W.PlaceId); Assert.Equal(1, f.W.CompletedJourneys);
        f.AssertSameAsBodiesOff();
        f.Tick();
        Assert.True(f.Bound); Assert.True(f.Body.Speed > 0); Assert.Empty(f.Corrections());
        for (int i = 0; i < 5; i++) f.Tick(3);
        Assert.True(f.Bound);
        for (int i = 0; i < 6; i++) f.Run(3);
        Assert.True(f.Bound); Assert.True(f.Body.Speed > 0); Assert.Equal(1, f.Body.Alpha); Assert.Empty(f.Corrections());
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
        f.Advance(Now + 30);
        foreach (var city in new[] { f.City, f.Plain })
            city.State.Citizens[0].OptionalStop = new OptionalStopState { Kind = DwellKind.Break, PlaceId = "work", SpotId = "solo:w",
                TargetId = "front:w", Position = new Position(104, 0, 0), Facing = new Position(104, 0, 1), ReturnPosition = Work,
                StartMinute = Now + 30, EndMinute = Now + 38, VisitMinute = Now + 30, ResumeEventMinute = Now + 300 };
        f.City.ActiveJourneys.File(f.W);
        f.Body.OnScreen = true;
        f.Tick();
        Assert.True(f.Bound);
        for (int i = 0; i < 4; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.True(f.Body.Speed > 0); Assert.Empty(f.Corrections());
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
        PhysicalPedestrians.VisiblePoint = _ => true;
        f.Run(2.1f); f.Run(2.1f);
        Assert.False(f.Bound);
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
        f.Advance(Now + 10);
        f.Tick();
        var move = Assert.Single(f.Corrections());
        Assert.Equal(WalkerCorrectionKind.Move, move.Kind); Assert.False(move.OnScreen); Assert.Equal("w", move.PersonId);
        Assert.Equal(Home, move.Before); Assert.Equal(new Position(60, 0, 0), move.After);
        Assert.Equal(new Position(60, 0, 0), f.Body.Position); Assert.Equal(1, f.Body.Warps);
        Assert.Equal(Time.frameCount, move.Frame);
        f.Body.OnScreen = true;
        f.Advance(Now + 20);
        for (int i = 0; i < 5; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Warps); Assert.Single(f.Corrections()); Assert.Equal(1, f.Body.Alpha);
        for (int i = 0; i < 5; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.Single(f.Corrections()); Assert.True(f.Body.Speed > 0);
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
        var restrictions = typeof(PhysicalPopulation).GetField("fadedUntilHidden", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f.Bodies)!;
        var add = restrictions.GetType().GetMethod("Add")!;
        for (int i = 0; i < 600; i++) add.Invoke(restrictions, new object[] { "other" + i, (f.W, f.W.Journey, (string?)null) });
        PhysicalPedestrians.VisiblePoint = _ => true;
        f.Run(2.1f); f.Run(2.1f);
        Assert.False(f.Bound);
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
        if (projectionAhead) NavMesh.Sample = p => new Vector3(p.x + 0.3f, p.y + 0.1f, p.z);
        f.Tick();
        long requests = PhysicalPopulation.Telemetry.PathRequests, ahead = PhysicalPopulation.Telemetry.AheadFrames;
        f.Advance(Now + 0.1);
        static void Native(Fixture x, float seconds)
        {
            var body = x.Body;
            if (body.Agent.isStopped) return;
            double d = Position.Distance(body.Position, body.Target), step = body.Speed * seconds;
            x.Place(d <= step ? body.Target : Position.Lerp(body.Position, body.Target, step / d));
        }
        static void Frame(Fixture x, float seconds, bool update = true)
        {
            Time.Next(seconds);
            x.Pool.BeforeNavigation?.Invoke();
            Native(x, seconds);
            if (update) x.Bodies.Update();
        }
        f.Tick(1 / 60f);
        foreach (var (frame, update) in new[] { (0.5f, true), (1 / 120f, true), (0.2f, false), (1 / 60f, true), (1f, false),
            (1f, false), (0.05f, true), (2f, true) })
        {
            Frame(f, frame, update);
            Assert.Equal(new Position(70, 0, 0), f.Body.Target);
            Assert.True(f.Body.Position.X <= 40.2 + WalkerFollow.AheadToleranceMetres, "body at " + f.Body.Position.X);
        }
        f.Tick(0.5f);
        Assert.Equal(0, f.Body.Speed); Assert.True(f.Body.Agent.isStopped);
        Assert.True(Position.Distance(new Position(40.2, 0, 0), f.Body.Position) < 1e-4);
        Assert.Equal(requests, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Equal(ahead, PhysicalPopulation.Telemetry.AheadFrames);
        f.Advance(Now + 1);
        Frame(f, 1 / 60f); Frame(f, 1 / 60f);
        Assert.True(f.Body.Speed > 0); Assert.Equal(new Position(70, 0, 0), f.Body.Target);
        Assert.True(f.Body.Position.X > 40.2 && f.Body.Position.X <= 42 + WalkerFollow.AheadToleranceMetres);
        Assert.Equal(requests, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Equal(ahead, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.Empty(f.Corrections());
        using var g = new Fixture();
        g.Tick();
        g.Advance(Now + 0.1);
        g.Tick(1 / 60f);
        Time.Next(0.5f); Native(g, 0.5f);
        Assert.True(g.Body.Position.X > 40.2 + WalkerFollow.AheadToleranceMetres, "body at " + g.Body.Position.X);
        g.Bodies.Update();
        Assert.Equal(0, g.Body.Speed);
        Assert.Equal(ahead + 1, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.Contains("ahead_frames=", PhysicalPopulation.Telemetry.Status(1));
    }

    [Fact]
    public void TheLastLegIsAimedAtTheDoorOnceAndTheTailReachesIt()
    {
        using var f = new Fixture();
        f.Tick();
        f.Body.OnScreen = true;
        f.Advance(Now + 29.8);
        f.Place(new Position(99.5, 0, 0));
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Tick();
        Assert.Equal(Work, f.Body.Target);
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests);
        f.Place(new Position(99.6, 0, 0));
        f.Advance(Now + 30);
        f.Run();
        Assert.True(f.Bound); Assert.Equal(Work, f.Body.Target); Assert.True(f.Body.Speed > 0);
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests);
        f.Place(f.Body.Target);
        f.Run();
        Assert.False(f.Bound);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AWholeWalkCalculatesOnePathPerLegAndNeverRunsAheadOfThePose()
    {
        using var f = new Fixture();
        long requests = PhysicalPopulation.Telemetry.PathRequests, ahead = PhysicalPopulation.Telemetry.AheadFrames;
        double minute = Now;
        f.Tick(1 / 60f);
        f.Body.OnScreen = true;
        int frames = 0;
        for (; frames < 60 * 40 && f.Bound; frames++)
        {
            float seconds = frames % 50 == 49 ? 0.25f : 1 / 60f;
            Time.Next(seconds);
            f.Pool.BeforeNavigation?.Invoke();
            var body = f.Body;
            if (!body.Agent.isStopped)
            {
                double d = Position.Distance(body.Position, body.Target), step = body.Speed * seconds;
                f.Place(d <= step ? body.Target : Position.Lerp(body.Position, body.Target, step / d));
            }
            if (f.W.Phase == CitizenPhase.Travelling)
                Assert.True(body.Position.X <= f.City.PositionOf(f.W).X + 1e-6, "ahead at frame " + frames);
            minute += seconds; f.Advance(minute); f.Bodies.Update();
        }
        Assert.False(f.Bound);
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests);
        Assert.Equal(ahead, PhysicalPopulation.Telemetry.AheadFrames);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AheadIsMeasuredOnlyOnThePosesOwnLeg()
    {
        var route = new Route { Legs = new() { Leg(new Position(0, 0, 0), new Position(10, 0, 0)), Leg(new Position(10, 0, 0), new Position(0, 0, 0.2)) } };
        var logical = WalkerFollow.Sample(route, 5.5);
        Assert.Equal(0, WalkerFollow.AheadMetres(WalkerFollow.Sample(route, 4), logical, new Position(8, 0, 0)));
        Assert.InRange(WalkerFollow.AheadMetres(logical, logical, new Position(8.5, 0, 0.1)), 0.45, 0.55);
        Assert.Equal(0, WalkerFollow.AheadMetres(logical, logical, new Position(9.5, 0, 0)));
    }

    [Fact]
    public void AnInterruptedLegResumesWithTheSameIdentityAndOneNewPath()
    {
        using var f = new Fixture();
        f.Tick();
        var first = f.Pool.Acquisitions[^1];
        f.Advance(Now + 5);
        f.Place(new Position(49.5, 0, 0));
        f.Tick();
        Assert.Equal(new Position(70, 0, 0), f.Body.Target);
        NativeTravelPace.Focus = new Position(1000, 0, 1000);
        f.Tick();
        Assert.False(f.Bound);
        f.AssertSameAsBodiesOff();
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
        f.Body.Agent.hasPath = false;
        f.Advance(Now + 7); f.Tick(); f.Tick();
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests);
        Assert.True(f.Body.Speed > 0);
        f.Body.Agent.isPathStale = true;
        f.Advance(Now + 8); f.Tick(); f.Tick();
        Assert.Equal(requests + 3, PhysicalPopulation.Telemetry.PathRequests);
        Assert.False(f.Body.Agent.isPathStale); Assert.True(f.Body.Speed > 0);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void APoseBetweenCornersIsProjectedOntoTheWalkableSurfaceAndAFailedAimKeepsWalking()
    {
        using var f = new Fixture();
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Tick();
        Assert.Equal(requests + 1, PhysicalPopulation.Telemetry.PathRequests);
        NavMesh.Sample = p => new Vector3(p.x, 0.12f, p.z);
        PhysicalPedestrianBody.Unreachable = p => Math.Abs(p.Y - 0.12) > 0.01;
        f.Body.Agent.hasPath = false;
        f.Advance(Now + 2);
        f.Tick();
        Assert.True(Position.Distance(new Position(44, 0.12, 0), f.Body.Target) < 1e-4);
        Assert.False(f.Body.Agent.isStopped); Assert.True(f.Body.Speed > 0);
        Assert.Equal(requests + 3, PhysicalPopulation.Telemetry.PathRequests);
        PhysicalPedestrianBody.Unreachable = p => p.Y > 0.05;
        f.Advance(Now + 3);
        f.Tick();
        Assert.True(Position.Distance(new Position(46, 0, 0), f.Body.Target) < 1e-4);
        Assert.Equal(requests + 5, PhysicalPopulation.Telemetry.PathRequests);
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
        Assert.Equal(calls + 1, f.Body.SetTargets);
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
        f.Body.Agent.hasPath = false;
        long requests = PhysicalPopulation.Telemetry.PathRequests;
        f.Advance(Now + 4.1234 / 2);
        f.Tick();
        Assert.True(Math.Abs(f.Body.Target.Y - 0.12) < 1e-4, "target " + f.Body.Target);
        Assert.True(Math.Abs(f.Body.Target.X - 44.1234) < 1e-4);
        Assert.Equal(requests + 2, PhysicalPopulation.Telemetry.PathRequests);
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
        f.Place(new Position(70, 0, 0));
        for (int i = 0; i < 4; i++) f.Run(3);
        Assert.True(f.Bound); Assert.Equal(1, f.Body.Alpha); Assert.Empty(f.Body.Fades); Assert.Empty(f.Corrections());
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
            var next = Legs(Work, new Position(100, 0, 60));
            f.W.Phase = CitizenPhase.Travelling; f.W.Journey = next; f.W.DepartureMinute = f.City.State.Minute - 1;
            f.Tick();
            Assert.True(f.Bound); Assert.Equal(new Position(70, 0, 0), f.Body.Target);
            f.Place(Work);
            f.Tick(); f.Tick();
            Assert.True(f.Bound);
            Assert.Equal(new Position(100, 0, 30), f.Body.Target);
            Assert.True(f.Body.Speed > 0);
            Assert.Empty(f.Corrections());
        }
        using (var f = new Fixture())
        {
            f.Tick();
            f.Advance(Now + 30);
            f.Tick();
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
        f.Advance(Now + 16);
        f.Body.OnScreen = true; f.Body.CanFade = false; f.Place(new Position(70, 0, 0));
        f.Tick();
        Assert.False(f.Bound);
        Assert.Empty(f.Corrections());
        Assert.Equal(f.Handoffs + 1, PhysicalPopulation.Telemetry.Handoffs);
        f.Tick();
        Assert.False(f.Bound);
        f.Advance(Now + 26);
        f.Tick();
        Assert.True(f.Bound);
        Assert.Equal(new Position(92, 0, 0), f.Body.Position);
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AWalkerAppearingInViewFadesIn()
    {
        using var f = new Fixture();
        PhysicalPedestrians.AppearOnScreen = true;
        f.Tick();
        Assert.True(f.Bound); Assert.Equal(0f, f.Body.Fades[0]);
        f.Run();
        Assert.InRange(f.Body.Alpha, 0.2f, 0.6f);
        for (int i = 0; i < 4; i++) f.Run();
        Assert.Equal(1f, f.Body.Alpha);
        int fades = f.Body.Fades.Count;
        f.Run();
        Assert.Equal(fades, f.Body.Fades.Count);
        Assert.Empty(f.Corrections());
        f.AssertSameAsBodiesOff();
    }

    [Fact]
    public void AVisibleWalkerBoardingFadesOutInsteadOfPopping()
    {
        Route Transit() => new Route { Legs = new() { Leg(Home, new Position(70, 0, 0)),
            Leg(new Position(70, 0, 0), new Position(90, 0, 0), TravelMode.Subway), Leg(new Position(90, 0, 0), Work) } };
        using var f = new Fixture(Transit);
        f.Tick();
        f.Advance(Now + 16);
        var body = f.Body;
        body.OnScreen = true; f.Place(new Position(70, 0, 0));
        f.Tick();
        Assert.True(f.Bound); Assert.Same(body, f.Body);
        Assert.Equal(0, body.Speed);
        for (int i = 0; i < 3; i++) f.Run();
        Assert.True(f.Bound); Assert.InRange(body.Alpha, 0.05f, 0.95f);
        for (int i = 0; i < 4; i++) f.Run();
        Assert.False(f.Bound); Assert.Contains(body.Fades, a => a < 1 && a > 0);
        Assert.Empty(f.Corrections());
        Assert.Equal(f.Handoffs + 1, PhysicalPopulation.Telemetry.Handoffs);
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
        f.Place(new Position(104, 0, 20));
        f.Tick();
        f.Advance(Now + 6);
        f.Tick();
        Assert.Equal(Venue, f.Body.Target);
        f.Advance(Now + 10);
        Assert.Equal(CitizenPhase.Travelling, f.W.Phase);
        Assert.Equal(f.Plain.State.Citizens[0].DepartureMinute, f.W.DepartureMinute);
        f.Tick();
        Assert.True(f.Bound); Assert.Equal(Venue, f.Body.Target);
        f.AssertSameAsBodiesOff();
        f.Advance(Now + 12);
        Assert.Equal(CitizenPhase.Travelling, f.W.Phase);
        f.Place(Venue);
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

    [Fact]
    public void MeasureFollowerArithmeticPerBody()
    {
        string? path = PerfGate.Variable("PC_F2C_MEASURE");
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
