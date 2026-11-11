using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalProgressTests
{
    private static PhysicalJourneySample Sample(double elapsed = 0, TravelMode mode = TravelMode.Walk, bool wait = false)
    {
        var route = new Route { Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(wait ? 0 : 10,0,0), Minutes = 10, Mode = mode },
            new RouteLeg { From = new Position(wait ? 0 : 10,0,0), To = new Position(wait ? 0 : 10,0,10), Minutes = 10, Mode = mode }
        } };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "p" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = 20 };
        var state = new CityState { Minute = elapsed, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = route.Legs[0].From }, new Place { Id = "b", Position = route.Legs[1].To } } };
        return new CitySimulator(state, new NoRoutes()).SampleJourney(citizen, elapsed);
    }

    private static PhysicalJourneySample Vertical(double elapsed = 0, TravelMode mode = TravelMode.Walk, double height = 4)
    {
        var route = new Route { Legs = new()
        { new RouteLeg { From = new Position(2,0,3), To = new Position(2,height,3), Minutes = 8, Mode = mode } } };
        var citizen = new CitizenState { Identity = new CitizenIdentity { Id = "vertical" }, Phase = CitizenPhase.Travelling,
            Journey = route, PlaceId = "a", DestinationId = "b", DepartureMinute = 0, NextEventMinute = 8 };
        var state = new CityState { Minute = elapsed, Citizens = new() { citizen }, Places = new()
        { new Place { Id = "a", Position = route.Legs[0].From }, new Place { Id = "b", Position = route.Legs[0].To } } };
        return new CitySimulator(state, new NoRoutes()).SampleJourney(citizen, elapsed);
    }

    [Fact]
    public void VerticalWalkerAtSourceCanInitializeTargetWithoutInventingProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(), new Position(2,0,3), false, out double progress));
        Assert.Equal(0, progress);
    }

    [Fact]
    public void ShortVerticalLegAtSourceDoesNotReceiveEndpointToleranceCredit()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(height: .02), new Position(2,0,3), false, out double progress));
        Assert.Equal(0, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(height: .02), new Position(2,.01,3), false, out progress));
        Assert.Equal(4, progress);
    }

    [Fact]
    public void VerticalWalkerCreditsMeasuredHeightAndNeverRegresses()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(), new Position(2,1,3), false, out double progress));
        Assert.Equal(2, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(4), new Position(2,1,3), false, out progress));
        Assert.Equal(4, progress);
        Assert.True(PhysicalProgress.TryProject(Vertical(4), new Position(2,3,3), false, out progress));
        Assert.Equal(6, progress);
    }

    [Fact]
    public void VerticalWalkerEndpointCompletesOnlyMeasuredLeg()
    {
        Assert.True(PhysicalProgress.TryProject(Vertical(2), new Position(2,4,3), false, out double progress));
        Assert.Equal(8, progress);
    }

    [Theory]
    [InlineData(2.081, 2, 3)] [InlineData(2, 4.081, 3)] [InlineData(2, -0.081, 3)]
    public void VerticalWalkerRejectsOffSegmentBodiesEvenWithTrustedCorner(double x, double y, double z)
    {
        Assert.False(PhysicalProgress.TryProject(Vertical(2), new Position(x,y,z), true, out double progress));
        Assert.Equal(2, progress);
    }

    [Fact]
    public void VerticalCarsCannotUseWalkingProjectionFallback()
    {
        Assert.False(PhysicalProgress.TryProject(Vertical(2, TravelMode.Car), new Position(2,4,3), true, out double progress));
        Assert.Equal(2, progress);
    }

    [Fact]
    public void StationaryMovingActorNeverGetsElapsedBasedProgress()
    {
        var sample = Sample(4);
        for (int frame = 0; frame < 100; frame++)
        { Assert.True(PhysicalProgress.TryProject(sample, new Position(4,0,0), false, out double progress)); Assert.Equal(4, progress); }
    }

    [Fact]
    public void WalkingBackwardsCannotRegressCommittedProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(6), new Position(2,0,0), false, out double progress));
        Assert.Equal(6, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(6), new Position(-3,0,0), false, out progress)); Assert.Equal(6, progress);
    }

    [Theory]
    [InlineData(TravelMode.Walk, .36)] [InlineData(TravelMode.Car, 2.01)]
    public void AdjacentParallelRoadIsRejectedEvenWhenPastEndpoint(TravelMode mode, double offset)
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4, mode), new Position(12,0,offset), false, out double progress));
        Assert.Equal(4, progress);
    }

    [Fact]
    public void CurvedNextSegmentCannotShortcutTheCurrentCorner()
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(8,0,3), false, out double progress)); Assert.Equal(4, progress);
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(10,0,3), false, out progress)); Assert.Equal(4, progress);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VerticalSeparationRejectsProjectionAndTrustedCorner(bool trusted)
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(10,2.01,0), trusted, out double progress)); Assert.Equal(4, progress);
    }

    [Fact]
    public void ActualEndpointAndAlignedForwardOvershootCompleteOnlyCurrentLeg()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(10,0,0), false, out double progress)); Assert.Equal(10, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(14,0,.2), false, out progress)); Assert.Equal(10, progress);
        Assert.True(PhysicalProgress.TryProject(Sample(4, TravelMode.Car), new Position(18,0,1.9), false, out progress)); Assert.Equal(10, progress);
    }

    [Theory]
    [InlineData(TravelMode.Walk, 9.93)] [InlineData(TravelMode.Car, 8.6)]
    public void NearCornerUsesNativeActorDistanceTolerance(TravelMode mode, double x)
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4, mode), new Position(x,0,0), false, out double progress)); Assert.Equal(10, progress);
    }

    [Fact]
    public void TrustedNativeCornerCanCompleteAfterVehicleTurnsAway()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4, TravelMode.Car), new Position(10,0,3), true, out double progress));
        Assert.Equal(10, progress);
    }

    [Fact]
    public void CrossTrackToleranceAllowsSmallOffsetsWithoutInflatingProgress()
    {
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(7,0,.3), false, out double progress)); Assert.Equal(7, progress, 10);
        Assert.True(PhysicalProgress.TryProject(Sample(4), new Position(7,2,0), false, out progress)); Assert.Equal(7, progress, 10);
    }

    [Fact]
    public void WaitConsumesOnlyNonnegativeSimulationElapsedAndStopsAtBoundary()
    {
        var sample = Sample(4, wait: true);
        Assert.False(PhysicalProgress.TryProject(sample, new Position(0,0,0), false, out double progress)); Assert.Equal(4, progress);
        Assert.Equal(4, PhysicalProgress.AdvanceWait(sample, 0)); Assert.Equal(6, PhysicalProgress.AdvanceWait(sample, 2));
        Assert.Equal(10, PhysicalProgress.AdvanceWait(sample, 100));
        Assert.Throws<ArgumentException>(() => PhysicalProgress.AdvanceWait(Sample(4), 1));
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidWaitElapsedCannotAdvance(double elapsed)
    { Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalProgress.AdvanceWait(Sample(4, wait: true), elapsed)); }

    [Fact]
    public void InvalidCoordinatesAndEmptySampleCannotBecomeProgress()
    {
        Assert.False(PhysicalProgress.TryProject(Sample(4), new Position(double.NaN,0,0), false, out double progress)); Assert.Equal(4, progress);
        Assert.False(PhysicalProgress.TryProject(default, new Position(0,0,0), true, out progress));
    }

    // F2c walker follow (plan Decision 2): pure follower arithmetic and the bounded correction ring.
    private static Route FollowRoute() => new Route { Legs = new()
    {
        new RouteLeg { From = new Position(0,0,0), To = new Position(10,0,0), Minutes = 10, Mode = TravelMode.Walk },
        new RouteLeg { From = new Position(10,0,0), To = new Position(10,0,0), Minutes = 2, Mode = TravelMode.Walk, ServiceId = "preparation" },
        new RouteLeg { From = new Position(10,0,0), To = new Position(10,0,20), Minutes = 10, Mode = TravelMode.Walk },
        new RouteLeg { From = new Position(10,0,20), To = new Position(50,0,20), Minutes = 4, Mode = TravelMode.Subway },
        new RouteLeg { From = new Position(50,0,20), To = new Position(52,0,20), Minutes = 2, Mode = TravelMode.Walk }
    } };

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 0)] [InlineData(WalkerFollow.StopMetres, 0)] [InlineData(0.2, 1)] [InlineData(1.5, 1)]
    [InlineData(2.25, 1.25)] [InlineData(3, 1.5)] [InlineData(40, 1.5)] [InlineData(double.NaN, 0)]
    public void FollowPaceIsNativeUpToCatchUpAndNeverAboveOneAndAHalf(double lag, double factor)
        => Assert.Equal(factor, WalkerFollow.PaceFactor(lag), 12);

    private const string UrpLit = "Universal Render Pipeline/Lit";
    // The facts the production probe reads from a material, for the verified shaders and their look-alikes.
    [Theory]
    // URP transparent surfaces fade through _BaseColor.
    [InlineData(UrpLit, 3000, true, false, 5f, 10f, 1f, true, false, false, true)]
    [InlineData("Universal Render Pipeline/Simple Lit", 3000, true, false, 5f, 10f, 1f, true, false, false, true)]
    [InlineData("Universal Render Pipeline/Unlit", 2501, true, false, 5f, 10f, 1f, true, false, false, true)]
    // URP Lit with an opaque surface (_Surface 0), queue 3000 and blend 5/10: its fragment alpha is forced to 1.
    [InlineData(UrpLit, 3000, true, false, 5f, 10f, 0f, false, false, false, false)]
    [InlineData(UrpLit, 3000, true, false, 5f, 10f, 1f, false, false, false, false)] // keyword missing
    // Alpha clipping cuts the body out halfway instead of fading it.
    [InlineData(UrpLit, 3000, true, false, 5f, 10f, 1f, true, false, true, false)]
    // Queue, blend and colour must all agree.
    [InlineData(UrpLit, 2500, true, false, 5f, 10f, 1f, true, false, false, false)]
    [InlineData(UrpLit, 3000, true, false, 1f, 0f, 1f, true, false, false, false)]
    [InlineData(UrpLit, 3000, true, false, 5f, 1f, 1f, true, false, false, false)]
    [InlineData(UrpLit, 3000, true, false, null, null, 1f, true, false, false, false)]
    [InlineData(UrpLit, 3000, false, true, 5f, 10f, 1f, true, false, false, false)] // no _BaseColor
    // Built-in Standard in its alpha-blend (Fade) mode fades through _Color.
    [InlineData("Standard", 3000, false, true, 5f, 10f, null, false, true, false, true)]
    [InlineData("Standard (Specular setup)", 3000, false, true, 5f, 10f, null, false, true, false, true)]
    [InlineData("Standard", 3000, false, true, 5f, 10f, null, false, false, false, false)] // opaque or cutout mode
    [InlineData("Standard", 3000, false, true, 5f, 10f, null, false, true, true, false)]   // alpha test on
    // An unsupported shader exposing every matching property and keyword is still opaque for the fade.
    [InlineData("Custom/Character", 3000, true, true, 5f, 10f, 1f, true, true, false, false)]
    [InlineData("", 3000, true, true, 5f, 10f, 1f, true, true, false, false)]
    public void OnlyVerifiedAlphaBlendedShaderConfigurationsCanFade(string shader, int queue, bool baseColour, bool colour,
        float? source, float? destination, float? surface, bool transparentKeyword, bool alphaBlendKeyword, bool alphaTest, bool fades)
        => Assert.Equal(fades, WalkerFollow.MaterialCanFade(new MaterialFadeFacts(shader, queue, baseColour, colour, source, destination,
            surface, transparentKeyword, alphaBlendKeyword, alphaTest)));

    [Fact]
    public void AProjectedPoseAheadOnTheLegIsNotUsable()
    {
        var from = new Position(40, 0, 0); var to = new Position(70, 0, 0); var pose = new Position(44, 0, 0);
        Assert.True(WalkerFollow.NotAheadOnLeg(from, to, pose, new Position(44, 0.12, 0)));   // straight up
        Assert.True(WalkerFollow.NotAheadOnLeg(from, to, pose, new Position(43.8, 0.1, 0.2))); // behind and aside
        Assert.False(WalkerFollow.NotAheadOnLeg(from, to, pose, new Position(44.3, 0.1, 0)));  // ahead along the leg
        Assert.False(WalkerFollow.NotAheadOnLeg(from, to, pose, new Position(double.NaN, 0, 0)));
        // A pose that is not an exact float, projected straight up through float coordinates, is not ahead.
        // 44.1234 and 44.7 round up in float (ahead by about a micrometre), the others round down.
        foreach (double x in new[] { 44.1234, 44.7, 44.123456789, 344.987654321, 1234.5678912 })
        {
            var exact = new Position(x, 0, 0.3333333333);
            var rounded = new Position((float)exact.X, 0.12f, (float)exact.Z);
            Assert.True(WalkerFollow.NotAheadOnLeg(new Position(x - 4, 0, 0), new Position(x + 26, 0, 0), exact, rounded), "x " + x);
        }
        Assert.False(WalkerFollow.NotAheadOnLeg(from, to, pose, new Position(44.02, 0, 0)));
    }

    [Fact]
    public void CorrectionStartsBeyondEightMetresOrOnAnotherMode()
    {
        Assert.False(WalkerFollow.NeedsCorrection(8, TravelMode.Walk));
        Assert.True(WalkerFollow.NeedsCorrection(8.01, TravelMode.Walk));
        Assert.True(WalkerFollow.NeedsCorrection(double.NaN, TravelMode.Walk));
        Assert.True(WalkerFollow.NeedsCorrection(0, TravelMode.Subway));
        Assert.True(WalkerFollow.NeedsCorrection(0, TravelMode.Car));
    }

    [Fact]
    public void RouteMetresMeasureAlongCommittedLegs()
    {
        var route = FollowRoute();
        Assert.Equal(5, WalkerFollow.RouteMetres(route, 0, 5), 12);
        Assert.Equal(10, WalkerFollow.RouteMetres(route, 0, 12), 12);   // the wait leg adds no distance
        Assert.Equal(15, WalkerFollow.RouteMetres(route, 5, 17), 12);
        Assert.Equal(70, WalkerFollow.RouteMetres(route, 0, 26), 12);
        Assert.Equal(0, WalkerFollow.RouteMetres(route, 7, 7));
        Assert.Equal(0, WalkerFollow.RouteMetres(route, 9, 3));
    }

    [Fact]
    public void BodyProgressIsNeverAheadOfTheLogicalPose()
    {
        var route = FollowRoute();
        // Physically ahead of a logical pose at 4 m: credited only up to the logical progress.
        Assert.Equal(4, WalkerFollow.Advance(route, 0, 4, new Position(9,0,0)), 12);
        // Behind on the same leg: the projection.
        Assert.Equal(3, WalkerFollow.Advance(route, 0, 8, new Position(3,0,0.1)), 12);
        // Pushed aside: no credit, the previous progress stays.
        Assert.Equal(2, WalkerFollow.Advance(route, 2, 8, new Position(3,0,5)), 12);
        // At the corner it passes onto the wait leg and waits with the logical clock, never past it.
        Assert.Equal(11, WalkerFollow.Advance(route, 9, 11, new Position(10,0,0)), 12);
        Assert.Equal(12, WalkerFollow.Advance(route, 9, 13, new Position(10,0,0)), 12);
        // At the station it stops at the start of the subway leg even when the logical pose is past it.
        Assert.Equal(22, WalkerFollow.Advance(route, 21, 25, new Position(10,0,20)), 12);
        Assert.Equal(TravelMode.Subway, WalkerFollow.Sample(route, 22).Mode);
        // Never below zero or above the route, whatever the inputs.
        Assert.Equal(0, WalkerFollow.Advance(route, -5, double.NaN, new Position(0,0,0)));
        Assert.Equal(route.Minutes, WalkerFollow.Advance(route, 27, 1e9, new Position(52,0,20)), 12);
    }

    [Fact]
    public void CorrectionRingKeepsTheLast512AndReportsAGapAfterEviction()
    {
        var log = new WalkerMovementTelemetry();
        var entries = new System.Collections.Generic.List<WalkerCorrection>();
        log.Since(0, entries, out bool gap);
        Assert.Empty(entries); Assert.False(gap); Assert.Equal(0, log.LastSequence);
        for (int i = 1; i <= 600; i++)
            Assert.Equal(i, log.Record(i * 10, "p" + i, (WalkerCorrectionKind)(i % 3), new Position(i,0,0), new Position(i,0,1), i % 7 == 0));
        Assert.Equal(600, log.LastSequence); Assert.Equal(89, log.OldestSequence);
        log.Since(0, entries, out gap);
        Assert.True(gap); Assert.Equal(512, entries.Count); Assert.Equal(89, entries[0].Sequence); Assert.Equal(600, entries[^1].Sequence);
        entries.Clear(); log.Since(87, entries, out gap);
        Assert.True(gap); Assert.Equal(512, entries.Count);
        entries.Clear(); log.Since(88, entries, out gap);
        Assert.False(gap); Assert.Equal(512, entries.Count);
        entries.Clear(); log.Since(590, entries, out gap);
        Assert.False(gap); Assert.Equal(Enumerable.Range(591, 10).Select(i => (long)i), entries.Select(e => e.Sequence));
        var last = entries[^1];
        Assert.Equal("p600", last.PersonId); Assert.Equal(6000, last.Frame); Assert.Equal(600, last.Before.X); Assert.Equal(1, last.After.Z);
        Assert.Equal(WalkerCorrectionKind.Move, last.Kind);
        entries.Clear(); log.Since(600, entries, out gap);
        Assert.Empty(entries); Assert.False(gap);
        Assert.Equal(600, log.Corrections); Assert.Equal(200, log.Moves); Assert.Equal(200, log.Fades); Assert.Equal(200, log.LagReleases);
        // Moves on screen are what the Q3 default prohibits; they are counted separately.
        Assert.Equal(Enumerable.Range(1, 600).Count(i => i % 3 == 0 && i % 7 == 0), log.OnScreenMoves);
        Assert.Contains("on_screen_moves=" + log.OnScreenMoves, log.Status(3));
        Assert.Contains("kind=Move on_screen=false", WalkerMovementTelemetry.Format(last));
    }

    [Fact]
    public void LagQuantilesComeFromEverySample()
    {
        var log = new WalkerMovementTelemetry();
        Assert.Equal(0, log.LagQuantile(0.95));
        for (int i = 0; i < 100; i++) log.RecordLag(i < 95 ? 0.52 : 20);
        log.RecordLag(double.NaN); log.RecordLag(-1);
        Assert.Equal(100, log.LagSamples);
        Assert.Equal(0.55, log.LagQuantile(0.5), 9);
        Assert.Equal(0.55, log.LagQuantile(0.95), 9);
        Assert.Equal(20, log.LagQuantile(0.96), 9);
        Assert.Equal(20, log.MaximumLagMetres);
        log.RecordLag(500);
        Assert.Equal(500, log.LagQuantile(1));
    }
}
