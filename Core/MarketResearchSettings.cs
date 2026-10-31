using System;

namespace PersistentCity.Core
{
    public sealed class MarketResearchSettings
    {
        public int MarketResearchCatchmentMetres { get; set; } = 500;
        public int MarketResearchConsumersPerUnit { get; set; } = 256;
        // Computed, with no setter/config switch. A native payment implementation must precede activation.
        public bool Available => false;
        public void Clamp(Action<string>? log = null)
        {
            int radius = Math.Max(50, Math.Min(2000, MarketResearchCatchmentMetres));
            int unit = Math.Max(16, Math.Min(1024, MarketResearchConsumersPerUnit));
            if (radius != MarketResearchCatchmentMetres) log?.Invoke("MarketResearchCatchmentMetres clamped to " + radius);
            if (unit != MarketResearchConsumersPerUnit) log?.Invoke("MarketResearchConsumersPerUnit clamped to " + unit);
            MarketResearchCatchmentMetres = radius; MarketResearchConsumersPerUnit = unit;
        }
    }
}
