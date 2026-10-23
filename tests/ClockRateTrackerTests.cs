using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;
public sealed class ClockRateTrackerTests
{
    [Fact]
    public void CappedBoundaryThenPauseDoesNotReplaceTravelPace()
    {
        var t=new ClockRateTracker();
        for(int f=0;f<4;f++)t.Observe(f,1.0/3,1);
        Assert.Equal(1.0/3,t.Rate);
        t.Observe(4,.009885812472060796,1);t.Observe(5,0,1);t.Observe(6,0,1);
        Assert.True(t.Held);Assert.Equal(1.0/3,t.Rate);Assert.Equal("",t.Error);
        for(int f=7;f<11;f++)t.Observe(f,1.0/3,1);
        Assert.False(t.Held);Assert.Equal(1.0/3,t.Rate);Assert.Equal("",t.Error);
    }
    [Fact]
    public void MultipleTicksInAFrameUseTotalMinutesAndRealChangesAreConfirmed()
    {
        var t=new ClockRateTracker();
        for(int f=0;f<4;f++){t.Observe(f,.01,.06);t.Observe(f,.01,.06);}
        Assert.Equal(1.0/3,t.Rate!.Value,10);
        t.Observe(4,.06,.06);Assert.Equal(1.0/3,t.Rate.Value,10);
        t.Observe(5,.06,.06);Assert.Equal(1.0/3,t.Rate.Value,10);
        t.Observe(6,.06,.06);Assert.Equal(1,t.Rate);
    }
    [Theory]
    [InlineData(.001)] [InlineData(1001)]
    public void SustainedUnsupportedPaceStillRefuses(double rate)
    {
        var t=new ClockRateTracker();for(int f=0;f<3;f++)t.Observe(f,rate,1);
        Assert.NotEmpty(t.Error);Assert.Null(t.Rate);
    }
    [Fact]
    public void MalformedClockSampleIsImmediatelyRejected()
    {var t=new ClockRateTracker();t.Observe(0,double.NaN,1);Assert.NotEmpty(t.Error);}
}
