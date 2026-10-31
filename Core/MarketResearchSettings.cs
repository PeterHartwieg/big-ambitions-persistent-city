using System;

namespace PersistentCity.Core
{
    public sealed class MarketResearchSettings
    {
        private bool reported;
        public int MarketResearchCatchmentMetres { get; set; } = 500;
        public int MarketResearchConsumersPerUnit { get; set; } = 256;
        // Computed, with no setter/config switch. A native payment implementation must precede activation.
        public bool Available => false;
        public void Clamp(Action<string>? log = null)
        {
            int radius = Math.Max(50, Math.Min(2000, MarketResearchCatchmentMetres));
            int unit = Math.Max(16, Math.Min(1024, MarketResearchConsumersPerUnit));
            MarketResearchCatchmentMetres = radius; MarketResearchConsumersPerUnit = unit;
            // Parsing may already have clamped or defaulted a value. Report only the two
            // bounded effective values once, never the raw configuration tokens.
            if (!reported && log != null)
            {
                log("MarketResearchCatchmentMetres=" + radius.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research unavailable).");
                log("MarketResearchConsumersPerUnit=" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research unavailable).");
                reported = true;
            }
        }
    }
}
