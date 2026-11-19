using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
namespace PersistentCity.Tests;
public sealed class AudienceGeometryCacheTests
{
    private static OptionalStopSpot Spot(Place place, AudienceTarget member, int angle)
        => new OptionalStopSpot { Id = member.Id + ":" + angle, TargetId = member.Id, Position = new Position(member.Position.X + angle, 0, place.Position.X) };

    [Fact]
    public void LivePerformerImageReusesPathsButInvalidatesEveryLifetimeChange()
    {
        var cache=new AudienceGeometryCache(); var place=new Place {Id="venue"};
        object zone=new(), actor=new();
        OptionalStopSpot? Sample(Place p,AudienceTarget member,int angle) => Spot(p,member,angle);
        AudienceTarget[] Image(object z,object a,double x=1,string id="show") => new[] {new AudienceTarget(z,a,id,new Position(x,0,0))};
        var first=cache.Get(place,Image(zone,actor),Sample);
        for(int i=0;i<100;i++) Assert.Same(first,cache.Get(place,Image(zone,actor),Sample));
        Assert.Equal(1,cache.Builds);
        actor=new(); Assert.NotSame(first,cache.Get(place,Image(zone,actor),Sample)); Assert.Equal(2,cache.Builds);
        cache.Get(place,Image(zone,actor,2),Sample); Assert.Equal(3,cache.Builds);
        zone=new(); cache.Get(place,Image(zone,actor,2),Sample); Assert.Equal(4,cache.Builds);
        cache.Get(place,Image(zone,actor,2,"other"),Sample); Assert.Equal(5,cache.Builds);
        place.Position=new(3,0,0); cache.Get(place,Image(zone,actor,2,"other"),Sample); Assert.Equal(6,cache.Builds);
        var absent=cache.Get(place,Array.Empty<AudienceTarget>(),Sample); Assert.Equal(7,cache.Builds);
        Assert.Same(absent,cache.Get(place,Array.Empty<AudienceTarget>(),Sample));
        cache.Get(place,Image(zone,actor),Sample); Assert.Equal(8,cache.Builds);
    }
    [Fact]
    public void CallerOwnedImageChangesCannotMutateRetainedCacheKeyAndPlacesDoNotSharePaths()
    {
        var cache=new AudienceGeometryCache(); var place=new Place {Id="a"}; object zone=new(),actor=new();
        OptionalStopSpot? Sample(Place p,AudienceTarget member,int angle) => null;
        var targets=new List<AudienceTarget> {new(zone,actor,"show",new(1,0,0))};
        cache.Get(place,targets,Sample); targets.Clear(); cache.Get(place,targets,Sample); Assert.Equal(2,cache.Builds);
        cache.Get(new Place {Id="b"},targets,Sample); Assert.Equal(3,cache.Builds);
    }

    [Fact]
    public void ASlicedAudienceBuildMatchesTheUnslicedOneAndStaysPendingUntilItFinishes()
    {
        var place = new Place { Id = "venue", Position = new Position(4, 0, 0) };
        var members = Enumerable.Range(0, 5).Select(i => new AudienceTarget(new object(), new object(), "show" + i, new Position(i * 3, 0, 0))).ToArray();
        int calls = 0;
        OptionalStopSpot? Sample(Place p, AudienceTarget member, int angle) { calls++; return angle % 3 == 1 ? null : Spot(p, member, angle); }
        var whole = new AudienceGeometryCache().Get(place, members, Sample);
        int unsliced = calls; calls = 0;
        var cache = new AudienceGeometryCache();
        int slices = 0;
        IReadOnlyList<OptionalStopSpot> spots;
        while (!cache.TryGet(place, members, Sample, 0, out spots))
        {
            slices++;
            Assert.Empty(spots);
            Assert.True(cache.Pending(place, members));
            Assert.False(cache.Cached(place, members, out _));
        }
        Assert.Equal(members.Length * AudienceGeometryCache.Angles, unsliced);
        Assert.Equal(unsliced, calls);
        Assert.Equal(unsliced - 1, slices);
        Assert.Equal(whole.Select(s => s.Id + "@" + s.Position), spots.Select(s => s.Id + "@" + s.Position));
        Assert.True(cache.Cached(place, members, out var cached));
        Assert.Same(spots, cached);
        Assert.False(cache.Pending(place, members));
        Assert.Equal(1, cache.Builds);
    }

    [Fact]
    public void AFailedSliceDropsThePartialBuildSoTheNextReadStartsAgain()
    {
        var place = new Place { Id = "venue" };
        var members = new[] { new AudienceTarget(new object(), new object(), "show", new Position(1, 0, 0)) };
        int calls = 0;
        OptionalStopSpot? Failing(Place p, AudienceTarget member, int angle) { calls++; if (angle == 3) throw new InvalidOperationException("scene"); return Spot(p, member, angle); }
        var cache = new AudienceGeometryCache();
        Assert.Throws<InvalidOperationException>(() => cache.Get(place, members, Failing));
        Assert.False(cache.Pending(place, members));
        Assert.Equal(4, calls);
        var spots = cache.Get(place, members, (p, member, angle) => Spot(p, member, angle));
        Assert.Equal(AudienceGeometryCache.Angles, spots.Count);
        Assert.Equal(2, cache.Builds);
    }

    [Fact]
    public void ResumableSamplesNumberSpotsByResultCountAcrossSlices()
    {
        var inputs = Enumerable.Range(0, 40).ToArray();
        OptionalStopSpot? Sample(string prefix, int input, int count) => input % 4 == 0 ? null : new OptionalStopSpot { Id = prefix + count, TargetId = "p" + input };
        var whole = new ResumableSamples<int>(inputs);
        Assert.True(whole.Continue(Sample, "park:", long.MaxValue));
        var sliced = new ResumableSamples<int>(inputs);
        int continuations = 1;
        while (!sliced.Continue(Sample, "park:", 0)) continuations++;
        Assert.Equal(inputs.Length, continuations);
        Assert.Equal(whole.Take().Select(s => s.Id + s.TargetId), sliced.Take().Select(s => s.Id + s.TargetId));
        Assert.Equal(30, sliced.Spots.Count);
        Assert.True(new ResumableSamples<int>(Array.Empty<int>()).Continue(Sample, "park:", 0));
    }

    private sealed class Parks : IGeometryWarmTarget
    {
        public readonly int[] Samples; public readonly bool[] SetUp, Done; public int Starts;
        public double StartMilliseconds, SampleMilliseconds;
        public Parks(int count, int samples) { Samples = Enumerable.Repeat(samples, count).ToArray(); SetUp = new bool[count]; Done = new bool[count]; }
        public int Count => Samples.Length;
        public bool Warm(int index) => Done[index];
        public bool Started(int index) => SetUp[index];
        public void Start(int index) { Starts++; SetUp[index] = true; Spin(StartMilliseconds); }
        public bool Continue(int index, long deadline)
        {
            while (Samples[index] > 0)
            {
                Samples[index]--; Spin(SampleMilliseconds);
                if (Samples[index] > 0 && Stopwatch.GetTimestamp() >= deadline) return false;
            }
            Done[index] = true; return true;
        }
        private static void Spin(double milliseconds)
        {
            long until = Stopwatch.GetTimestamp() + (long)(milliseconds * Stopwatch.Frequency / 1000);
            while (Stopwatch.GetTimestamp() < until) { }
        }
    }

    [Fact]
    public void AnExhaustedFrameBudgetPreventsTheSetupOfAnotherColdPark()
    {
        var budget = new GeometryFrameBudget();
        var warmup = new GeometryWarmup();
        var parks = new Parks(3, 1) { StartMilliseconds = 1.5 };
        warmup.Run(budget, 1, parks);
        Assert.Equal(1, parks.Starts);
        Assert.True(parks.Done[0]);
        Assert.False(parks.SetUp[1]);
        warmup.Run(budget, 1, parks);
        Assert.Equal(1, parks.Starts);
        Assert.True(budget.Deferrals >= 1);
        warmup.Run(budget, 2, parks);
        Assert.Equal(2, parks.Starts);
        Assert.Equal(2, warmup.Setups);
        Assert.True(warmup.MaximumMilliseconds >= 1.5, "setup charged " + warmup.MaximumMilliseconds);
    }

    [Fact]
    public void WarmUpResumesAcrossFramesAndGoesIdleOnceEveryParkIsWarm()
    {
        var budget = new GeometryFrameBudget();
        var warmup = new GeometryWarmup();
        var parks = new Parks(2, 12) { SampleMilliseconds = 0.2 };
        int frames = 0;
        while (!warmup.Idle && frames < 100) warmup.Run(budget, ++frames, parks);
        Assert.True(parks.Done.All(d => d));
        Assert.Equal(2, parks.Starts);
        Assert.InRange(frames, 4, 20);
        long slices = warmup.Slices;
        warmup.Run(budget, ++frames, parks);
        Assert.Equal(slices, warmup.Slices);
        warmup.Reset();
        warmup.Run(budget, ++frames, parks);
        Assert.True(warmup.Idle);
    }

    [Fact]
    public void TheGeometryBudgetGrantsOneMillisecondPerFrameAndCountsDeferrals()
    {
        var budget = new GeometryFrameBudget();
        long now = Stopwatch.GetTimestamp();
        Assert.True(budget.TryDeadline(7, now, out long deadline));
        Assert.Equal(now + (long)(GeometryFrameBudget.DefaultFrameMilliseconds * Stopwatch.Frequency / 1000), deadline);
        budget.Spend(0.4);
        Assert.True(budget.TryDeadline(7, now, out deadline));
        Assert.InRange(deadline - now, (long)(0.6 * Stopwatch.Frequency / 1000) - 2, (long)(0.6 * Stopwatch.Frequency / 1000) + 2);
        budget.Spend(0.7);
        Assert.False(budget.TryDeadline(7, now, out _));
        Assert.False(budget.TryDeadline(7, now, out _));
        Assert.Equal(2, budget.Deferrals);
        Assert.True(budget.TryDeadline(8, now, out deadline));
        Assert.Equal(now + (long)(GeometryFrameBudget.DefaultFrameMilliseconds * Stopwatch.Frequency / 1000), deadline);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeometryFrameBudget(0));
    }
}
