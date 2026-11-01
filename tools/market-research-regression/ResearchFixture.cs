using System;
using PersistentCity.Core;

// Offline only. Compact frozen fixture, not a city clone or a runtime input adapter.
internal sealed class ResearchFixture : IMarketResearchSnapshot
{
    public MarketResearchIdentity Identity { get; set; } = new MarketResearchIdentity("fixture", 3, 7, 11, 13);
    public int Count { get; }
    private readonly MarketResearchConsumer[] rows;
    internal int Reads;
    internal Action<int>? OnRead { get; set; }
    internal ResearchFixture(int count)
    {
        Count = count; rows = new MarketResearchConsumer[count];
        for (int i=0;i<count;i++) rows[i]=Row(i);
    }
    public MarketResearchConsumer Read(int i)
    {
        Reads++; OnRead?.Invoke(i);
        return rows[i];
    }
    internal static MarketResearchConsumer Row(int i) => new MarketResearchConsumer(
        "consumer:" + i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture), i % 3 != 0,
        new Position(i % 1000, 0, 0), new Position(0, 0, i % 1000),
        i % 3 == 0 ? Activity.Work : i % 3 == 1 ? Activity.Study : Activity.Home,
        i % 5 == 0 ? "0000000000" : i % 5 == 1 ? "1111111111" : i % 5 == 2 ? "2222222222" : i % 5 == 3 ? "3333333333" : "4444444444",
        (byte)(i % 8), i % 2 == 0, i % 3 == 0 ? 999 : i % 3 == 1 ? 1000 : 2500);

    internal static MarketResearchReport Finish(IMarketResearchSnapshot source, int unit = 256,
        MarketResearchTier tier = MarketResearchTier.InDepth, MarketResearchScope? scope = null)
    {
        var job = new MarketResearchEngine(source, scope ?? new MarketResearchScope(), tier);
        while (job.Completed == null) job.Continue(source, unit);
        return job.Completed;
    }
}
