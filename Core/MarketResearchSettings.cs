using System;

namespace PersistentCity.Core
{
    public readonly struct MarketResearchActivation
    {
        public MarketResearchActivation(bool attached, bool dialogContract, bool billingContract, bool deliveryContract, bool ledgerAttached)
        {
            Attached = attached; DialogContract = dialogContract; BillingContract = billingContract;
            DeliveryContract = deliveryContract; LedgerAttached = ledgerAttached;
        }
        public bool Attached { get; }
        public bool DialogContract { get; }
        public bool BillingContract { get; }
        public bool DeliveryContract { get; }
        public bool LedgerAttached { get; }
        public bool Available => Attached && DialogContract && BillingContract && DeliveryContract && LedgerAttached;
        public string Refusal => !Attached ? "Persistent City is not attached" : !DialogContract ? "agency dialog contract failed"
            : !BillingContract ? "billing contract failed" : !DeliveryContract ? "delivery contract failed"
            : !LedgerAttached ? "research ledger not attached" : "";
    }

    public sealed class MarketResearchSettings
    {
        private bool reported;
        private Func<MarketResearchActivation>? checks;
        public int MarketResearchCatchmentMetres { get; set; } = 500;
        public int MarketResearchConsumersPerUnit { get; set; } = 256;
        public bool Available => Activation.Available;
        public MarketResearchActivation Activation => checks?.Invoke() ?? default;
        public void DeriveAvailability(Func<MarketResearchActivation> attachedChecks) => checks = attachedChecks ?? throw new ArgumentNullException(nameof(attachedChecks));
        public void ClearAvailability() => checks = null;
        public void Clamp(Action<string>? log = null)
        {
            int radius = Math.Max(50, Math.Min(2000, MarketResearchCatchmentMetres));
            int unit = Math.Max(16, Math.Min(1024, MarketResearchConsumersPerUnit));
            MarketResearchCatchmentMetres = radius; MarketResearchConsumersPerUnit = unit;
            if (!reported && log != null)
            {
                log("MarketResearchCatchmentMetres=" + radius.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research availability follows the attach and contract checks).");
                log("MarketResearchConsumersPerUnit=" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research availability follows the attach and contract checks).");
                reported = true;
            }
        }
    }
}
