using System;
using PersistentCity.Core;
using Xunit;

public sealed class AgencyOrdersTests
{
    private static readonly Guid City = Guid.NewGuid();
    private static AgencyQuote Quote(Money? price = null, Guid? city = null, string binding = "agency-session-binding", string scope = "unresolved-scope", Guid? request = null)
        => new AgencyQuote(new AgencyRequestId(request ?? Guid.NewGuid()), new AgencyIdentity(city ?? City, binding),
            new ResearchScopePlaceholder(scope), price ?? Money.FromUnits(123456789));
    private static AgencyOrderBook Book(AgencyQuote? quote = null)
    {
        var book = new AgencyOrderBook(City);
        Assert.Equal(AgencyPaymentRefusal.None, book.TryQuote(quote ?? Quote(), out _));
        return book;
    }
    private sealed class Spy : IAgencyPaymentPort
    {
        public int Validations, Charges;
        public AgencyPaymentRefusal Refusal;
        public bool ThrowBefore, ThrowAfter, Enter = true, EmptyEvidence, WrongEvidence, ThrowValidate;
        public int WrongBinding;
        public AgencyNativeOutcome Outcome = AgencyNativeOutcome.Paid;
        public Action? During;
        public AgencyQuote? Seen;
        public AgencyPaymentRefusal Validate(AgencyQuote q)
        { Validations++; if (ThrowValidate) throw new InvalidOperationException(); return Refusal; }
        public AgencyNativeOutcome Charge(AgencyQuote q, Action entry, out AgencyPaymentEvidence? evidence)
        {
            Charges++; Seen = q;
            if (ThrowBefore) throw new InvalidOperationException();
            if (Enter) entry();
            During?.Invoke();
            if (ThrowAfter) throw new InvalidOperationException();
            evidence = EmptyEvidence ? null : new AgencyPaymentEvidence(WrongBinding == 1 ? new AgencyRequestId(Guid.NewGuid()) : q.Request,
                WrongBinding == 2 ? new AgencyIdentity(q.Agency.CitySession, "different") : q.Agency,
                WrongEvidence ? Money.Zero : q.Price, "double-only evidence; never native proof");
            return Outcome;
        }
    }
    [Fact]
    public void QuotePendingPaidAndExactMoney()
    {
        var q = Quote(Money.FromNative(1.234567f)); var book = Book(q); var port = new Spy();
        Assert.Equal(AgencyOrderState.Quoted, book.State);
        port.During = () => Assert.Equal(AgencyOrderState.ChargePending, book.State);
        Assert.Equal(AgencyPaymentRefusal.None, book.TryCharge(port));
        Assert.Same(q, port.Seen); Assert.Equal(q.Price.Units, port.Seen!.Price.Units);
        Assert.Equal(AgencyOrderState.Paid, book.State); Assert.NotNull(book.Evidence);
        Assert.Equal(AgencyPaymentRefusal.AlreadyBlocked, book.TryCharge(port)); Assert.Equal(1, port.Charges);
    }
    [Fact]
    public void RepeatedClicksAcrossAgenciesReuseImmutableIdEvenWhenPendingOrPaid()
    {
        var q = Quote(); var book = Book(q); var port = new Spy();
        void Duplicate()
        {
            Assert.Equal(AgencyPaymentRefusal.None, book.TryQuote(Quote(binding: "another agency"), out var reused));
            Assert.Same(q, reused); Assert.Equal(q.Request.Value, reused!.Request.Value);
            Assert.Equal(AgencyPaymentRefusal.AlreadyBlocked, book.TryCharge(port));
        }
        Assert.Equal(AgencyPaymentRefusal.None, book.TryQuote(q, out var same)); Assert.Same(q, same);
        port.During = Duplicate; book.TryCharge(port); Duplicate(); Assert.Equal(1, port.Charges);
    }
    [Theory]
    [InlineData(AgencyPaymentRefusal.InsufficientFunds)]
    [InlineData(AgencyPaymentRefusal.InvalidAgency)]
    [InlineData(AgencyPaymentRefusal.InvalidQuote)]
    [InlineData(AgencyPaymentRefusal.Unavailable)]
    public void PreflightRefusesWithoutEntry(AgencyPaymentRefusal refusal)
    {
        var book = Book(); var port = new Spy { Refusal = refusal };
        Assert.Equal(refusal, book.TryCharge(port)); Assert.Equal(0, port.Charges);
        Assert.Equal(AgencyOrderState.Quoted, book.State); Assert.Null(book.Evidence);
    }
    [Fact]
    public void NativeInsufficientFundsIsDefinitiveNoDebitAndKeepsQuoteId()
    {
        var book = Book(); var q = book.Quote; var port = new Spy { Outcome = AgencyNativeOutcome.InsufficientFunds };
        Assert.Equal(AgencyPaymentRefusal.InsufficientFunds, book.TryCharge(port));
        Assert.Equal(AgencyOrderState.Quoted, book.State); Assert.Same(q, book.Quote); Assert.Null(book.Evidence);
    }
    [Fact]
    public void InvalidTermsDoNotOccupyCitySlot()
    {
        var book = new AgencyOrderBook(City);
        foreach (var q in new[] { Quote(Money.Zero), Quote(Money.FromUnits(-1)), Quote(scope: " "), Quote(request: Guid.Empty) })
            Assert.Equal(AgencyPaymentRefusal.InvalidQuote, book.TryQuote(q, out _));
        foreach (var q in new[] { Quote(city: Guid.NewGuid()), Quote(binding: " ") })
            Assert.Equal(AgencyPaymentRefusal.InvalidAgency, book.TryQuote(q, out _));
        Assert.Null(book.Quote); Assert.Equal(AgencyPaymentRefusal.InvalidQuote, book.TryCharge(new Spy()));
        Assert.Equal(AgencyPaymentRefusal.None, book.TryQuote(Quote(Money.FromUnits(1)), out var exact));
        Assert.Equal(1, exact!.Price.Units);
    }
    [Fact]
    public void BeforeEntryExceptionAllowsSameQuoteRetry()
    {
        var book = Book(); var port = new Spy { ThrowBefore = true };
        Assert.Equal(AgencyPaymentRefusal.BeforeNativeEntry, book.TryCharge(port));
        Assert.Equal(AgencyOrderState.Quoted, book.State); port.ThrowBefore = false;
        Assert.Equal(AgencyPaymentRefusal.None, book.TryCharge(port)); Assert.Equal(2, port.Charges);
    }
    [Fact]
    public void AfterEntryExceptionBlocksEveryFurtherChargeAndReplacement()
    {
        var book = Book(); var q = book.Quote; var port = new Spy { ThrowAfter = true };
        Assert.Equal(AgencyPaymentRefusal.AlreadyBlocked, book.TryCharge(port));
        Assert.Equal(AgencyOrderState.PaymentUnknown, book.State); Assert.Null(book.Evidence);
        book.TryQuote(Quote(), out var reused); Assert.Same(q, reused);
        for (int i = 0; i < 100; i++) Assert.Equal(AgencyPaymentRefusal.AlreadyBlocked, book.TryCharge(port));
        Assert.Equal(1, port.Charges); Assert.Equal(1, port.Validations);
    }
    [Theory]
    [InlineData(false, false, AgencyNativeOutcome.Paid)]
    [InlineData(true, true, AgencyNativeOutcome.Paid)]
    [InlineData(true, false, AgencyNativeOutcome.Unknown)]
    public void MissingEvidenceOrUnknownNeverBecomesPaid(bool enter, bool empty, AgencyNativeOutcome outcome)
    {
        var book = Book(); var port = new Spy { Enter = enter, EmptyEvidence = empty, Outcome = outcome };
        book.TryCharge(port); Assert.Equal(AgencyOrderState.PaymentUnknown, book.State);
        Assert.Equal(AgencyPaymentRefusal.AlreadyBlocked, book.TryCharge(port)); Assert.Equal(1, port.Charges);
    }
    [Fact]
    public void ExactMoneyDoesNotOverflowOrRoundInContract()
    {
        var q = Quote(Money.FromUnits(long.MaxValue)); var book = Book(q); var spy = new Spy();
        book.TryCharge(spy); Assert.Equal(long.MaxValue, spy.Seen!.Price.Units);
        Assert.Throws<ArgumentException>(() => new AgencyOrderBook(Guid.Empty));
    }
    [Fact]
    public void EvidenceForAnotherAmountIsUnknown()
    {
        var book = Book(); var spy = new Spy { WrongEvidence = true };
        book.TryCharge(spy); Assert.Equal(AgencyOrderState.PaymentUnknown, book.State);
        Assert.Null(book.Evidence); book.TryCharge(spy); Assert.Equal(1, spy.Charges);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EvidenceMustMatchRequestAndAgency(int wrong)
    {
        var book = Book(); var spy = new Spy { WrongBinding = wrong };
        book.TryCharge(spy); Assert.Equal(AgencyOrderState.PaymentUnknown, book.State);
        Assert.Null(book.Evidence); book.TryCharge(spy); Assert.Equal(1, spy.Charges);
    }
    [Fact]
    public void ValidationExceptionHasNotEnteredNative()
    {
        var book = Book(); var spy = new Spy { ThrowValidate = true };
        Assert.Equal(AgencyPaymentRefusal.BeforeNativeEntry, book.TryCharge(spy));
        Assert.Equal(AgencyOrderState.Quoted, book.State); Assert.Equal(0, spy.Charges);
    }
}
