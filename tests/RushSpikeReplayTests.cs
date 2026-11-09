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
        // X3: the replay builds its own city, so a missed wall-clock budget re-runs it (TimingBudget). The row and the
        // spike details are written once, from the last attempt, also when the budget fails.
        // Frame cadence never changes the city: every sliced replay attempt, retried or not, saves the same bytes as one
        // uninterrupted advance. A mismatch fails that attempt at once and is never retried away.
        string uninterrupted = RushSpikeReplay.RunUninterrupted(from, to, nativePrices);
        RushSpikeReplay.Result? result = null;
        try
        {
            TimingBudget.Run(timing =>
            {
                result = RushSpikeReplay.Run(from, to, nativePrices: nativePrices);
                Assert.Equal(uninterrupted, result.SaveSha256);
                Assert.True(result.PeakTravelling > 250 && result.PeakVehicles > 50, "fixture lost its rush: " + result.PeakTravelling + " travelling, " + result.PeakVehicles + " cars");
                timing.Under("single unit max", result.UnitMax, 30);
                timing.Under("advancement interval max", result.FrameMax, 30);
                timing.Under("advancement p95", result.FrameP95, 5);
            });
        }
        finally
        {
            if (result != null)
                output.WriteLine($"nativePrices={nativePrices}; p95={result.FrameP95:F6}ms; frameMax={result.FrameMax:F6}ms at minute {result.MaxFrameMinute} with {result.CollectionsInMaxFrame} garbage collections; unitMax={result.UnitMax:F6}ms ({result.MaxUnitKind}); frame/unit spikes30={result.FrameSpikes30}/{result.UnitSpikes30}; hash={result.SaveSha256}");
        }
    }
}
