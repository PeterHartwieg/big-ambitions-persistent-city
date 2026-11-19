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
    public void ResumableSamplesNumberSpotsByResultCountAcrossSlices()
    {
        var inputs = Enumerable.Range(0, 40).ToArray();
        OptionalStopSpot? Sample(int input, int count) => input % 4 == 0 ? null : new OptionalStopSpot { Id = "park:" + count, TargetId = "p" + input };
        var whole = new ResumableSamples<int>(inputs);
        Assert.True(whole.Continue(Sample, long.MaxValue));
        var sliced = new ResumableSamples<int>(inputs);
        int continuations = 1;
        while (!sliced.Continue(Sample, 0)) continuations++;
        Assert.Equal(inputs.Length, continuations);
        Assert.Equal(whole.Take().Select(s => s.Id + s.TargetId), sliced.Take().Select(s => s.Id + s.TargetId));
        Assert.Equal(30, sliced.Spots.Count);
        Assert.True(new ResumableSamples<int>(Array.Empty<int>()).Continue(Sample, 0));
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
