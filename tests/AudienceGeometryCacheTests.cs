using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
namespace PersistentCity.Tests;
public sealed class AudienceGeometryCacheTests
{
    [Fact]
    public void LivePerformerImageReusesPathsButInvalidatesEveryLifetimeChange()
    {
        var cache=new AudienceGeometryCache(); var place=new Place {Id="venue"};
        object zone=new(), actor=new(); int samples=0;
        IReadOnlyList<OptionalStopSpot> Sample(Place p,IReadOnlyList<AudienceTarget> image)
        { samples++; return new[] {new OptionalStopSpot {Id="spot"+samples}}; }
        AudienceTarget[] Image(object z,object a,double x=1,string id="show") => new[] {new AudienceTarget(z,a,id,new Position(x,0,0))};
        var first=cache.Get(place,Image(zone,actor),Sample);
        for(int i=0;i<100;i++) Assert.Same(first,cache.Get(place,Image(zone,actor),Sample));
        Assert.Equal(1,samples);
        actor=new(); Assert.NotSame(first,cache.Get(place,Image(zone,actor),Sample)); Assert.Equal(2,samples);
        cache.Get(place,Image(zone,actor,2),Sample); Assert.Equal(3,samples);
        zone=new(); cache.Get(place,Image(zone,actor,2),Sample); Assert.Equal(4,samples);
        cache.Get(place,Image(zone,actor,2,"other"),Sample); Assert.Equal(5,samples);
        place.Position=new(3,0,0); cache.Get(place,Image(zone,actor,2,"other"),Sample); Assert.Equal(6,samples);
        var absent=cache.Get(place,Array.Empty<AudienceTarget>(),Sample); Assert.Equal(7,samples);
        Assert.Same(absent,cache.Get(place,Array.Empty<AudienceTarget>(),Sample));
        cache.Get(place,Image(zone,actor),Sample); Assert.Equal(8,samples);
    }
    [Fact]
    public void CallerOwnedImageChangesCannotMutateRetainedCacheKeyAndPlacesDoNotSharePaths()
    {
        var cache=new AudienceGeometryCache(); var place=new Place {Id="a"}; object zone=new(),actor=new(); int calls=0;
        IReadOnlyList<OptionalStopSpot> Sample(Place p,IReadOnlyList<AudienceTarget> t)
        { calls++; return Array.Empty<OptionalStopSpot>(); }
        var targets=new List<AudienceTarget> {new(zone,actor,"show",new(1,0,0))};
        cache.Get(place,targets,Sample); targets.Clear(); cache.Get(place,targets,Sample); Assert.Equal(2,calls);
        cache.Get(new Place {Id="b"},targets,Sample); Assert.Equal(3,calls);
    }
}
