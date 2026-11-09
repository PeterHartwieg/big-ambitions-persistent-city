using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(RushSpikeReplayTests), DisableParallelization = true)]
public sealed class RushSpikeReplaySerial { }

/// <summary>Issue #128: the matched 957-resident evening rush, frame-sliced at 30 FPS. Timed alone:
/// parallel tests share the process GC and CPU, which would land in this replay's intervals.</summary>
[Collection(nameof(RushSpikeReplayTests))]
public sealed class RushSpikeReplayTests
{
    private readonly ITestOutputHelper output;
    public RushSpikeReplayTests(ITestOutputHelper output) { this.output = output; }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveningRushHasNoAdvancementIntervalAtOrAbove30MsAndKeepsTheUninterruptedState(bool nativePrices)
    {
        // Day 3 18:30-19:30 around the rig's 19:19 spike, warmed through two whole days first.
        const double from = 2 * 1440 + 1110, to = 2 * 1440 + 1170;
        var result = RushSpikeReplay.Run(from, to, nativePrices: nativePrices);
        output.WriteLine($"nativePrices={nativePrices}; p95={result.FrameP95:F6}ms; frameMax={result.FrameMax:F6}ms; unitMax={result.UnitMax:F6}ms; frame/unit spikes30={result.FrameSpikes30}/{result.UnitSpikes30}; hash={result.SaveSha256}");
        Assert.True(result.PeakTravelling > 250 && result.PeakVehicles > 50, "fixture lost its rush: " + result.PeakTravelling + " travelling, " + result.PeakVehicles + " cars");
        PerfGate.Under(result.UnitMax, 30, "single unit (" + result.MaxUnitKind + ")");
        PerfGate.Under(result.FrameMax, 30, "advancement interval at minute " + result.MaxFrameMinute
            + " with " + result.CollectionsInMaxFrame + " garbage collections during it");
        PerfGate.Under(result.FrameP95, 5, "advancement p95");
        // Frame cadence never changes the city: the sliced replay saves the same bytes as one uninterrupted advance.
        Assert.Equal(RushSpikeReplay.RunUninterrupted(from, to, nativePrices), result.SaveSha256);
    }
}
