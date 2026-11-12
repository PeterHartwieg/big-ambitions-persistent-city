using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(RushSpikeReplayTests), DisableParallelization = true)]
public sealed class RushSpikeReplaySerial { }

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
        const double from = 2 * 1440 + 1110, to = 2 * 1440 + 1170;
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
