using System;

namespace PersistentCity.Core
{
    /// <summary>The attach and contract checks research availability is derived from (plan C5 sections 3.1, 3.6, 3.7).
    /// Research is available only when every check holds; a default value holds none.</summary>
    public readonly struct MarketResearchActivation
    {
        public MarketResearchActivation(bool attached, bool dialogContract, bool billingContract, bool deliveryContract, bool ledgerAttached)
        {
            Attached = attached; DialogContract = dialogContract; BillingContract = billingContract;
            DeliveryContract = deliveryContract; LedgerAttached = ledgerAttached;
        }
        /// <summary>Persistent City is attached to the current save and not stopped or failed.</summary>
        public bool Attached { get; }
        /// <summary>The agency dialog contract passed and its postfix is installed.</summary>
        public bool DialogContract { get; }
        /// <summary>The billing contract passed, its postfix is installed and no foreign patch turned it off since.</summary>
        public bool BillingContract { get; }
        /// <summary>The native contact message members research delivery uses were found with their signatures.</summary>
        public bool DeliveryContract { get; }
        /// <summary>The saved research ledger is attached to the survey and to the native bill.</summary>
        public bool LedgerAttached { get; }
        public bool Available => Attached && DialogContract && BillingContract && DeliveryContract && LedgerAttached;
        /// <summary>The first failed check, in the order above; empty when research is available.</summary>
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
        /// <summary>Computed from the attach and contract checks, with no setter or config switch. Read at call time,
        /// so a stopped runtime or a contract turned off later is seen on the next read.</summary>
        public bool Available => Activation.Available;
        public MarketResearchActivation Activation => checks?.Invoke() ?? default;
        /// <summary>The attached runtime supplies its checks once research is attached; Detach clears them.</summary>
        public void DeriveAvailability(Func<MarketResearchActivation> attachedChecks) => checks = attachedChecks ?? throw new ArgumentNullException(nameof(attachedChecks));
        public void ClearAvailability() => checks = null;
        public void Clamp(Action<string>? log = null)
        {
            int radius = Math.Max(50, Math.Min(2000, MarketResearchCatchmentMetres));
            int unit = Math.Max(16, Math.Min(1024, MarketResearchConsumersPerUnit));
            MarketResearchCatchmentMetres = radius; MarketResearchConsumersPerUnit = unit;
            // Parsing may already have clamped or defaulted a value. Report only the two
            // bounded effective values once, never the raw configuration tokens.
            if (!reported && log != null)
            {
                log("MarketResearchCatchmentMetres=" + radius.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research availability follows the attach and contract checks).");
                log("MarketResearchConsumersPerUnit=" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (research availability follows the attach and contract checks).");
                reported = true;
            }
        }
    }
}
