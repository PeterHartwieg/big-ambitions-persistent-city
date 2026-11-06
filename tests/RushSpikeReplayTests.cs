using Xunit;

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(RushSpikeReplayTests), DisableParallelization = true)]
public sealed class RushSpikeReplaySerial { }

/// <summary>Issue #128: the matched 957-resident evening rush, frame-sliced at 30 FPS. Timed alone:
/// parallel tests share the process GC and CPU, which would land in this replay's intervals.</summary>
[Collection(nameof(RushSpikeReplayTests))]
public sealed class RushSpikeReplayTests
{
    [Fact]
    public void EveningRushHasNoAdvancementIntervalAtOrAbove30MsAndKeepsTheUninterruptedState()
    {
        // Day 3 18:30-19:30 around the rig's 19:19 spike, warmed through two whole days first.
        const double from = 2 * 1440 + 1110, to = 2 * 1440 + 1170;
        var result = RushSpikeReplay.Run(from, to);
        Assert.True(result.PeakTravelling > 250 && result.PeakVehicles > 50, "fixture lost its rush: " + result.PeakTravelling + " travelling, " + result.PeakVehicles + " cars");
        Assert.True(result.UnitMax < 30, "single unit " + result.UnitMax + " ms (" + result.MaxUnitKind + ")");
        Assert.True(result.FrameMax < 30, "advancement interval " + result.FrameMax + " ms at minute " + result.MaxFrameMinute
            + " with " + result.CollectionsInMaxFrame + " garbage collections during it");
        Assert.True(result.FrameP95 < 5, "advancement p95 " + result.FrameP95 + " ms");
        // Frame cadence never changes the city: the sliced replay saves the same bytes as one uninterrupted advance.
        Assert.Equal(RushSpikeReplay.RunUninterrupted(from, to), result.SaveSha256);
    }
}
