using System;

namespace PersistentCity.Core
{
    // Provisional, in-memory C3 types only. C5 owns the future wire/entitlement contract.
    public readonly struct AgencyRequestId
    {
        public AgencyRequestId(Guid value) { Value = value; }
        public Guid Value { get; }
    }
    public readonly struct AgencyIdentity
    {
        public AgencyIdentity(Guid citySession, string binding) { CitySession = citySession; Binding = binding; }
        public Guid CitySession { get; }
        public string Binding { get; }
    }
    public readonly struct ResearchScopePlaceholder
    {
        public ResearchScopePlaceholder(string token) { Token = token; }
        public string Token { get; }
    }
    public sealed class AgencyQuote
    {
        public AgencyQuote(AgencyRequestId request, AgencyIdentity agency, ResearchScopePlaceholder scope, Money price)
        { Request = request; Agency = agency; Scope = scope; Price = price; }
        public AgencyRequestId Request { get; }
        public AgencyIdentity Agency { get; }
        public ResearchScopePlaceholder Scope { get; }
        public Money Price { get; }
    }
    public enum AgencyOrderState { Quoted, ChargePending, Paid, PaymentUnknown }
    public enum AgencyPaymentRefusal { None, InvalidQuote, InvalidAgency, InsufficientFunds, Unavailable, BeforeNativeEntry, AlreadyBlocked }
    public enum AgencyNativeOutcome { Paid, InsufficientFunds, Refused, Unknown }

    public sealed class AgencyPaymentEvidence
    {
        public AgencyPaymentEvidence(AgencyRequestId request, AgencyIdentity agency, Money amount, string nativeRecord)
        { Request = request; Agency = agency; Amount = amount; NativeRecord = nativeRecord; }
        public AgencyRequestId Request { get; }
        public AgencyIdentity Agency { get; }
        public Money Amount { get; }
        public string NativeRecord { get; }
        internal bool Matches(AgencyQuote quote) => Request.Value == quote.Request.Value && Amount == quote.Price
            && Agency.CitySession == quote.Agency.CitySession && Agency.Binding == quote.Agency.Binding
            && !string.IsNullOrWhiteSpace(NativeRecord);
    }

    /// <summary>A port must call enteringNative immediately BEFORE its first possible native mutation.
    /// Paid requires exact matching native evidence, not merely a successful return/debit estimate.
    /// InsufficientFunds/Refused promise no debit occurred. All ambiguous returns are Unknown.
    /// No implementations may refund or retry internally. C3 Runtime always refuses.</summary>
    public interface IAgencyPaymentPort
    {
        AgencyPaymentRefusal Validate(AgencyQuote quote);
        AgencyNativeOutcome Charge(AgencyQuote quote, Action enteringNative, out AgencyPaymentEvidence? evidence);
    }

    /// <summary>Inactive single-city coordinator, shared by every potential agency front end.
    /// One slot, no reset/cancel/reconciliation API: even Paid IDs cannot be charged again here.
    /// C5 must establish persistent ownership and lifecycle before adding multiple orders/reload.
    /// No gameplay constructs this type. No report or entitlement is granted by this contract.</summary>
    public sealed class AgencyOrderBook
    {
        private readonly Guid citySession;
        private readonly object sync = new object();
        private AgencyQuote? quote;
        public AgencyOrderBook(Guid citySession)
        {
            if (citySession == Guid.Empty) throw new ArgumentException("A loaded-city session is required.", nameof(citySession));
            this.citySession = citySession;
        }
        public AgencyQuote? Quote { get { lock (sync) return quote; } }
        public AgencyOrderState State { get; private set; } = AgencyOrderState.Quoted;
        public AgencyPaymentEvidence? Evidence { get; private set; }

        public AgencyPaymentRefusal TryQuote(AgencyQuote candidate, out AgencyQuote? current)
        {
            lock (sync)
            {
                current = quote;
                // Repeated clicks, even with a fresh ID, reuse the existing immutable slot.
                if (quote != null) return AgencyPaymentRefusal.None;
                if (candidate == null || candidate.Request.Value == Guid.Empty || candidate.Price <= Money.Zero
                    || string.IsNullOrWhiteSpace(candidate.Scope.Token)) return AgencyPaymentRefusal.InvalidQuote;
                if (candidate.Agency.CitySession != citySession || string.IsNullOrWhiteSpace(candidate.Agency.Binding))
                    return AgencyPaymentRefusal.InvalidAgency;
                quote = current = candidate;
                return AgencyPaymentRefusal.None;
            }
        }

        public AgencyPaymentRefusal TryCharge(IAgencyPaymentPort port)
        {
            lock (sync)
            {
                if (quote == null) return AgencyPaymentRefusal.InvalidQuote;
                if (State != AgencyOrderState.Quoted) return AgencyPaymentRefusal.AlreadyBlocked;
                // Set before external validation too: reentrant/double-click callers must see pending.
                State = AgencyOrderState.ChargePending;
                bool entered = false;
                try
                {
                    var refusal = port.Validate(quote);
                    if (refusal != AgencyPaymentRefusal.None) { State = AgencyOrderState.Quoted; return refusal; }
                    var outcome = port.Charge(quote, () =>
                    {
                        if (entered) throw new InvalidOperationException("Native entry may occur only once.");
                        entered = true;
                    }, out var evidence);
                    if (outcome == AgencyNativeOutcome.Paid && entered && evidence != null && evidence.Matches(quote))
                    { Evidence = evidence; State = AgencyOrderState.Paid; return AgencyPaymentRefusal.None; }
                    if (outcome == AgencyNativeOutcome.InsufficientFunds || outcome == AgencyNativeOutcome.Refused)
                    {
                        State = AgencyOrderState.Quoted;
                        return outcome == AgencyNativeOutcome.InsufficientFunds ? AgencyPaymentRefusal.InsufficientFunds : AgencyPaymentRefusal.Unavailable;
                    }
                    State = AgencyOrderState.PaymentUnknown;
                    return AgencyPaymentRefusal.AlreadyBlocked;
                }
                catch (Exception)
                {
                    State = entered ? AgencyOrderState.PaymentUnknown : AgencyOrderState.Quoted;
                    return entered ? AgencyPaymentRefusal.AlreadyBlocked : AgencyPaymentRefusal.BeforeNativeEntry;
                }
            }
        }
    }
}
