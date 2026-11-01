using System;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Deliberately refusing C3 port. Discovery proves a native recurring debit, but not
    /// a research purchase with durable request identity. See docs/plans/c3-agency-payment.md.
    /// No native calls, no food checkout, no money writes; no gameplay consumer.</summary>
    public sealed class AgencyPaymentPort : IAgencyPaymentPort
    {
        public AgencyPaymentRefusal Validate(AgencyQuote quote) => AgencyPaymentRefusal.Unavailable;
        public AgencyNativeOutcome Charge(AgencyQuote quote, Action enteringNative, out AgencyPaymentEvidence? evidence)
        { evidence = null; return AgencyNativeOutcome.Refused; }
    }
}
