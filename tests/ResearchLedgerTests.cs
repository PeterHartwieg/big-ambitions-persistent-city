using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Tests;
using Xunit;
using Xunit.Abstractions;

public sealed class ResearchLedgerTests
{
    private const double Minute = 8 * 60 + 15.25;
    private readonly ITestOutputHelper output;
    public ResearchLedgerTests(ITestOutputHelper output) { this.output = output; }

    private static int reportSerial;
    private static MarketResearchReport ReportFor(ResearchOrder order, string? id = null)
        => new MarketResearchReport(id ?? "q-" + order.Id + "-" + (++reportSerial), order.Product.Tier, 3,
            order.Product.Coverage == ResearchCoverage.City ? null : order.BusinessAddress, order.RadiusMetres,
            new[] { new MarketResearchCell(MarketResearchMetric.Consumers, MarketResearchBand.All, 7) });

    private static ResearchOrder Order(ResearchLedger ledger, string business, string code = ResearchProducts.BasicLocalCode)
    {
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(code, business, "agency-1", false, 500, Minute, out var order));
        return order!;
    }

    private static ResearchOrder Surveyed(ResearchLedger ledger, string business, string code = ResearchProducts.BasicLocalCode)
    {
        var order = Order(ledger, business, code);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(order.Id, ReportFor(order)));
        return order;
    }

    private static ResearchOrder InState(ResearchLedger ledger, ResearchOrderState state, string business)
    {
        if (state == ResearchOrderState.Ordered) return Order(ledger, business);
        var order = Surveyed(ledger, business);
        switch (state)
        {
            case ResearchOrderState.Paid: Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(order.Id)); break;
            case ResearchOrderState.Declined: Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(order.Id, ResearchDeclineReason.InsufficientFunds)); break;
            case ResearchOrderState.Cancelled: Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(order.Id)); break;
            case ResearchOrderState.PaymentUnknown: Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(order.Id)); break;
            case ResearchOrderState.Replaced:
                Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(order.Id));
                for (int i = 0; i < ResearchLedger.MaximumKeptReports; i++)
                    Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Surveyed(ledger, business + "-fill-" + i).Id));
                break;
        }
        Assert.Equal(state, order.State);
        return order;
    }

    // ---- Products ----

    [Fact]
    public void ProductTableHasTheAcceptedDefaultsWithExactMoneyAndBinary32Prices()
    {
        Assert.Equal(new[] { "basic-local", "detailed-local", "detailed-city" }, ResearchProducts.All.Select(p => p.Code));
        Assert.Equal(new[] { 500, 2000, 6000 }, ResearchProducts.All.Select(p => p.PriceDollars));
        Assert.Equal(new[] { MarketResearchTier.Basic, MarketResearchTier.Detailed, MarketResearchTier.Detailed }, ResearchProducts.All.Select(p => p.Tier));
        Assert.Equal(new[] { ResearchCoverage.Catchment, ResearchCoverage.Catchment, ResearchCoverage.City }, ResearchProducts.All.Select(p => p.Coverage));
        Assert.DoesNotContain(ResearchProducts.All, p => p.Tier == MarketResearchTier.InDepth);
        foreach (var product in ResearchProducts.All)
        {
            Assert.Equal(product.PriceDollars * Money.UnitsPerDollar, product.Price.Units);
            Assert.Equal(Money.FromCents(product.PriceDollars * 100L), product.Price);
            Assert.True(product.Price.IsWholeCents());
            // The binary32 value is the exact whole dollar: same bits as the integer converted, same value back.
            Assert.Equal(BitConverter.SingleToInt32Bits((float)product.PriceDollars), BitConverter.SingleToInt32Bits(product.NativePrice));
            Assert.Equal((double)product.PriceDollars, (double)product.NativePrice);
            Assert.Equal(product.PriceDollars, (int)product.NativePrice);
            Assert.Equal(product.Price, Money.FromNative(product.NativePrice));
            Assert.Equal(product.Price, Money.FromNative(-(-product.NativePrice)));
            Assert.True(ResearchProducts.TryGet(product.Code, out var found) && ReferenceEquals(product, found));
        }
        Assert.Equal(500f, BitConverter.Int32BitsToSingle(0x43FA0000));
        Assert.Equal(0x43FA0000, BitConverter.SingleToInt32Bits(ResearchProducts.BasicLocal.NativePrice));
        Assert.Equal(0x44FA0000, BitConverter.SingleToInt32Bits(ResearchProducts.DetailedLocal.NativePrice));
        Assert.Equal(0x45BB8000, BitConverter.SingleToInt32Bits(ResearchProducts.DetailedCity.NativePrice));
        Assert.Equal(ResearchProducts.BasicLocalPriceDollars, ResearchProducts.BasicLocal.PriceDollars);
        Assert.Equal(ResearchProducts.DetailedLocalPriceDollars, ResearchProducts.DetailedLocal.PriceDollars);
        Assert.Equal(ResearchProducts.DetailedCityPriceDollars, ResearchProducts.DetailedCity.PriceDollars);
        foreach (var code in new[] { null, "", "Basic-Local", "basic-local ", "in-depth-local" })
            Assert.False(ResearchProducts.TryGet(code, out _));
    }

    // ---- Ordering ----

    [Fact]
    public void OrderStoresFixedPriceSequenceIdAndSurveyMinute()
    {
        var ledger = new ResearchLedger();
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder("detailed-local", "biz-a", "agency-1", true, 750, Minute, out var a));
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder("detailed-city", "biz-b", "agency-2", false, 750, Minute, out var b));
        Assert.Equal("pcr-1", a!.Id); Assert.Equal(1, a.Sequence);
        Assert.Equal("pcr-2", b!.Id); Assert.Equal(3, ledger.NextSequence);
        Assert.Equal(ResearchProducts.DetailedLocal.Price, a.Price);
        Assert.Equal(ResearchOrderState.Ordered, a.State); Assert.Equal(ResearchDeclineReason.None, a.DeclineReason);
        Assert.Equal("biz-a", a.BusinessAddress); Assert.Equal("agency-1", a.AgencyAddress); Assert.True(a.TaxDeductible);
        Assert.Equal(750, a.RadiusMetres); Assert.Equal(0, b.RadiusMetres);
        Assert.Equal(Minute, a.OrderMinute); Assert.Equal(AccountingClock.Target(Minute), a.SurveyAtMinute);
        Assert.Equal("", a.ReportId); Assert.Null(a.Report);
        Assert.Equal(new[] { a, b }, ledger.Open);

        // At the minute fence the target is the epoch's last double, not the native minute.
        double fence = 8 * 60 + 15 + (1 - 1.0 / 262144);
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder("basic-local", "biz-c", "agency-1", false, 500, fence, out var c));
        Assert.Equal(AccountingClock.Target(fence), c!.SurveyAtMinute);
        Assert.True(c.SurveyAtMinute < 8 * 60 + 16);
    }

    [Fact]
    public void InvalidOrdersAreRefusedWithoutUsingASequence()
    {
        var ledger = new ResearchLedger();
        string longAddress = new string('x', ResearchLedger.MaximumAddressLength + 1);
        Assert.Equal(ResearchOrderRefusal.UnknownProduct, ledger.TryOrder("in-depth-local", "biz", "ag", false, 500, Minute, out _));
        Assert.Equal(ResearchOrderRefusal.UnknownProduct, ledger.TryOrder(null, "biz", "ag", false, 500, Minute, out _));
        foreach (var bad in new[] { null, "", "  ", longAddress })
        {
            Assert.Equal(ResearchOrderRefusal.InvalidBusiness, ledger.TryOrder("basic-local", bad, "ag", false, 500, Minute, out _));
            Assert.Equal(ResearchOrderRefusal.InvalidAgency, ledger.TryOrder("basic-local", "biz", bad, false, 500, Minute, out _));
        }
        foreach (int radius in new[] { 0, 49, 2001, -500 })
            Assert.Equal(ResearchOrderRefusal.InvalidRadius, ledger.TryOrder("basic-local", "biz", "ag", false, radius, Minute, out _));
        foreach (double minute in new[] { double.NaN, double.PositiveInfinity, -1, 2e9 })
            Assert.Equal(ResearchOrderRefusal.InvalidMinute, ledger.TryOrder("basic-local", "biz", "ag", false, 500, minute, out _));
        Assert.Empty(ledger.Open); Assert.Equal(1, ledger.NextSequence);
        // City coverage ignores the catchment radius.
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder("detailed-city", "biz", "ag", false, 0, Minute, out var city));
        Assert.Equal(0, city!.RadiusMetres); Assert.Equal("pcr-1", city.Id);
    }

    [Fact]
    public void OneOpenOrderPerBusinessAndEightPerCity()
    {
        var ledger = new ResearchLedger();
        var first = Order(ledger, "biz-0");
        foreach (var code in ResearchProducts.All.Select(p => p.Code))
            Assert.Equal(ResearchOrderRefusal.BusinessHasOpenOrder, ledger.TryOrder(code, "biz-0", "agency-2", false, 500, Minute, out _));
        for (int i = 1; i < ResearchLedger.MaximumOpenOrders; i++) Order(ledger, "biz-" + i);
        Assert.Equal(8, ledger.Open.Count);
        Assert.Equal(ResearchOrderRefusal.CityOrderLimit, ledger.TryOrder("basic-local", "biz-8", "agency-1", false, 500, Minute, out var refused));
        Assert.Null(refused); Assert.Equal(9, ledger.NextSequence);

        // A surveyed order is still open; only leaving Surveyed frees the business and the city slot.
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(first.Id, ReportFor(first)));
        Assert.Equal(ResearchOrderRefusal.CityOrderLimit, ledger.TryOrder("basic-local", "biz-8", "agency-1", false, 500, Minute, out _));
        Assert.Equal(ResearchOrderRefusal.BusinessHasOpenOrder, ledger.TryOrder("basic-local", "biz-0", "agency-1", false, 500, Minute, out _));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(first.Id));
        Assert.Equal(7, ledger.Open.Count);
        var again = Order(ledger, "biz-0");
        Assert.Equal("pcr-9", again.Id);
        Assert.Equal(ResearchOrderRefusal.CityOrderLimit, ledger.TryOrder("basic-local", "biz-8", "agency-1", false, 500, Minute, out _));
    }

    // ---- Moves ----

    private enum Move { Seal, Paid, DeclineFunds, DeclinePrecision, Cancel, Unknown }

    private static ResearchMoveRefusal Apply(ResearchLedger ledger, ResearchOrder order, Move move) => move switch
    {
        Move.Seal => ledger.TrySeal(order.Id, ReportFor(order)),
        Move.Paid => ledger.TryMarkPaid(order.Id),
        Move.DeclineFunds => ledger.TryDecline(order.Id, ResearchDeclineReason.InsufficientFunds),
        Move.DeclinePrecision => ledger.TryDecline(order.Id, ResearchDeclineReason.BalancePrecision),
        Move.Cancel => ledger.TryCancel(order.Id),
        _ => ledger.TryMarkPaymentUnknown(order.Id),
    };

    private static readonly Dictionary<Move, ResearchOrderState> Target = new Dictionary<Move, ResearchOrderState>
    {
        [Move.Seal] = ResearchOrderState.Surveyed, [Move.Paid] = ResearchOrderState.Paid,
        [Move.DeclineFunds] = ResearchOrderState.Declined, [Move.DeclinePrecision] = ResearchOrderState.Declined,
        [Move.Cancel] = ResearchOrderState.Cancelled, [Move.Unknown] = ResearchOrderState.PaymentUnknown,
    };

    [Fact]
    public void EveryAllowedMoveSucceedsAndEveryForbiddenMoveChangesNothing()
    {
        var allowed = new HashSet<(ResearchOrderState, Move)>
        {
            (ResearchOrderState.Ordered, Move.Seal), (ResearchOrderState.Surveyed, Move.Paid),
            (ResearchOrderState.Surveyed, Move.DeclineFunds), (ResearchOrderState.Surveyed, Move.DeclinePrecision),
            (ResearchOrderState.Surveyed, Move.Cancel), (ResearchOrderState.Surveyed, Move.Unknown),
        };
        int checkedPairs = 0;
        foreach (ResearchOrderState state in Enum.GetValues(typeof(ResearchOrderState)))
            foreach (Move move in Enum.GetValues(typeof(Move)))
            {
                var ledger = new ResearchLedger();
                var order = InState(ledger, state, "biz");
                var before = (order.State, order.DeclineReason, order.ReportId, order.Report, ledger.NextSequence,
                    ledger.Open.Count, ledger.Kept.Count, ledger.Closed.Count);
                var result = Apply(ledger, order, move);
                if (allowed.Contains((state, move)))
                {
                    Assert.Equal(ResearchMoveRefusal.None, result);
                    Assert.Equal(Target[move], order.State);
                    Assert.Equal(move == Move.DeclineFunds ? ResearchDeclineReason.InsufficientFunds
                        : move == Move.DeclinePrecision ? ResearchDeclineReason.BalancePrecision : ResearchDeclineReason.None, order.DeclineReason);
                    Assert.Equal(order.IsOpen, ledger.Open.Contains(order));
                    Assert.Equal(order.State == ResearchOrderState.Paid, ledger.Kept.Contains(order));
                    Assert.Equal(!order.IsOpen && order.State != ResearchOrderState.Paid, ledger.Closed.Contains(order));
                    Assert.Equal(order.State == ResearchOrderState.Surveyed || order.State == ResearchOrderState.Paid, order.Report != null);
                }
                else
                {
                    Assert.Equal(ResearchMoveRefusal.WrongState, result);
                    Assert.Equal(before, (order.State, order.DeclineReason, order.ReportId, order.Report, ledger.NextSequence,
                        ledger.Open.Count, ledger.Kept.Count, ledger.Closed.Count));
                }
                checkedPairs++;
            }
        Assert.Equal(7 * 6, checkedPairs);
    }

    [Fact]
    public void MovesRefuseUnknownOrdersInvalidReasonsAndMismatchedReports()
    {
        var ledger = new ResearchLedger();
        foreach (var id in new[] { null, "", "pcr-1", "pcr-01" })
        {
            Assert.Equal(ResearchMoveRefusal.UnknownOrder, ledger.TryMarkPaid(id));
            Assert.Equal(ResearchMoveRefusal.UnknownOrder, ledger.TryCancel(id));
            Assert.Equal(ResearchMoveRefusal.UnknownOrder, ledger.TryMarkPaymentUnknown(id));
            Assert.Equal(ResearchMoveRefusal.UnknownOrder, ledger.TryDecline(id, ResearchDeclineReason.InsufficientFunds));
            Assert.Equal(ResearchMoveRefusal.UnknownOrder, ledger.TrySeal(id, null));
        }
        var local = Order(ledger, "biz-a");
        var city = Order(ledger, "biz-b", ResearchProducts.DetailedCityCode);
        var cell = new[] { new MarketResearchCell(MarketResearchMetric.Consumers, MarketResearchBand.All, 1) };
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, null));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("", MarketResearchTier.Basic, 3, "biz-a", 500, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("q", MarketResearchTier.Detailed, 3, "biz-a", 500, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("q", MarketResearchTier.Basic, 3, "biz-b", 500, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("q", MarketResearchTier.Basic, 3, "biz-a", 600, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("q", MarketResearchTier.Basic, 3, null, 500, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(city.Id, new MarketResearchReport("q", MarketResearchTier.Detailed, 3, "biz-b", 0, cell)));
        Assert.Equal(ResearchOrderState.Ordered, local.State);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(city.Id, new MarketResearchReport("q", MarketResearchTier.Detailed, 3, null, 0, cell)));
        // Two orders never share a report identity.
        Assert.Equal(ResearchMoveRefusal.InvalidReport, ledger.TrySeal(local.Id, new MarketResearchReport("q", MarketResearchTier.Basic, 3, "biz-a", 500, cell)));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(local.Id, new MarketResearchReport("q2", MarketResearchTier.Basic, 3, "biz-a", 500, cell)));
        Assert.Equal(ResearchMoveRefusal.InvalidReason, ledger.TryDecline(local.Id, ResearchDeclineReason.None));
        Assert.Equal(ResearchMoveRefusal.InvalidReason, ledger.TryDecline(local.Id, (ResearchDeclineReason)9));
        Assert.Equal(ResearchOrderState.Surveyed, local.State);
    }

    // ---- Billing boundaries ----

    private enum Outcome { Paid, InsufficientFunds, BalancePrecision, Ineligible, ThrowsAfterEntry, EvidenceMismatch }

    /// <summary>The shape C5d follows: every Surveyed order gets one attempt per boundary, and the result
    /// leaves Surveyed whatever happens. Counts native entries per order.</summary>
    private static void Boundary(ResearchLedger ledger, Func<ResearchOrder, Outcome> outcome, Dictionary<string, int> entries)
    {
        foreach (var order in ledger.Open.Where(o => o.State == ResearchOrderState.Surveyed).ToList())
        {
            var result = outcome(order);
            if (result == Outcome.Ineligible) { Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(order.Id)); continue; }
            if (result == Outcome.BalancePrecision) { Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(order.Id, ResearchDeclineReason.BalancePrecision)); continue; }
            entries[order.Id] = entries.TryGetValue(order.Id, out int n) ? n + 1 : 1;
            try
            {
                if (result == Outcome.ThrowsAfterEntry) throw new InvalidOperationException("native threw");
                var move = result switch
                {
                    Outcome.Paid => ledger.TryMarkPaid(order.Id),
                    Outcome.InsufficientFunds => ledger.TryDecline(order.Id, ResearchDeclineReason.InsufficientFunds),
                    _ => ledger.TryMarkPaymentUnknown(order.Id),
                };
                Assert.Equal(ResearchMoveRefusal.None, move);
            }
            catch (InvalidOperationException) { Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(order.Id)); }
        }
    }

    [Fact]
    public void RepeatedBoundariesChargeEachOrderOnceAndUnknownBlocksOnlyItself()
    {
        var ledger = new ResearchLedger();
        var plan = new Dictionary<string, Outcome>();
        foreach (Outcome outcome in Enum.GetValues(typeof(Outcome)))
            plan[Surveyed(ledger, "biz-" + outcome).Id] = outcome;
        var waiting = Order(ledger, "biz-waiting");
        var entries = new Dictionary<string, int>();
        for (int day = 0; day < 4; day++) Boundary(ledger, o => plan[o.Id], entries);

        Assert.All(entries.Values, n => Assert.Equal(1, n));
        Assert.Equal(new[] { "pcr-1", "pcr-2", "pcr-5", "pcr-6" }, entries.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(ResearchOrderState.Paid, ledger.Find("pcr-1")!.State);
        Assert.Equal(ResearchDeclineReason.InsufficientFunds, ledger.Find("pcr-2")!.DeclineReason);
        Assert.Equal(ResearchDeclineReason.BalancePrecision, ledger.Find("pcr-3")!.DeclineReason);
        Assert.Equal(ResearchOrderState.Cancelled, ledger.Find("pcr-4")!.State);
        Assert.Equal(ResearchOrderState.PaymentUnknown, ledger.Find("pcr-5")!.State);
        Assert.Equal(ResearchOrderState.PaymentUnknown, ledger.Find("pcr-6")!.State);
        // Only the order still waiting for its survey is open; nothing was retried or reset.
        Assert.Equal(new[] { waiting }, ledger.Open);

        // Unknown blocks only itself: the same business orders again under a new ID, and that order bills normally.
        var retry = Surveyed(ledger, "biz-" + Outcome.ThrowsAfterEntry);
        Assert.Equal("pcr-8", retry.Id);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(waiting.Id, ReportFor(waiting)));
        for (int day = 0; day < 3; day++) Boundary(ledger, _ => Outcome.Paid, entries);
        Assert.Equal(ResearchOrderState.Paid, retry.State); Assert.Equal(ResearchOrderState.Paid, waiting.State);
        Assert.Equal(1, entries[retry.Id]); Assert.Equal(1, entries[waiting.Id]);
        Assert.Equal(ResearchOrderState.PaymentUnknown, ledger.Find("pcr-5")!.State);
        Assert.Equal(ResearchOrderState.PaymentUnknown, ledger.Find("pcr-6")!.State);
        Assert.All(entries.Values, n => Assert.Equal(1, n));
    }

    [Fact]
    public void DeclinedAndCancelledOrdersReleaseTheReportAndGrantNothing()
    {
        var ledger = new ResearchLedger();
        foreach (var (move, state) in new[] { (Move.DeclineFunds, ResearchOrderState.Declined), (Move.DeclinePrecision, ResearchOrderState.Declined),
            (Move.Cancel, ResearchOrderState.Cancelled), (Move.Unknown, ResearchOrderState.PaymentUnknown) })
        {
            var order = Surveyed(ledger, "biz-" + move);
            var report = order.Report!;
            Assert.Equal(ResearchMoveRefusal.None, Apply(ledger, order, move));
            Assert.Equal(state, order.State);
            Assert.Null(order.Report); Assert.Equal(report.Id, order.ReportId);
            Assert.False(ledger.TryGet(order.Id, out var grant)); Assert.Null(grant);
            Assert.False(report.TryRead(ledger, order.Id, out var numbers)); Assert.Null(numbers);
            Assert.Contains(order, ledger.Closed); Assert.DoesNotContain(order, ledger.Open);
            // A new order for the same business is accepted.
            Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder("basic-local", order.BusinessAddress, "agency-1", false, 500, Minute, out var next));
            Assert.Equal(ResearchMoveRefusal.WrongState, ledger.TryCancel(next!.Id));
            Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(next.Id, ReportFor(next)));
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(next.Id));
        }
    }

    // ---- Entitlement ----

    [Fact]
    public void TryReadSucceedsOnlyForPaidAndFailsAfterReplaced()
    {
        var ledger = new ResearchLedger();
        foreach (ResearchOrderState state in Enum.GetValues(typeof(ResearchOrderState)))
        {
            if (state == ResearchOrderState.Ordered || state == ResearchOrderState.Replaced) continue;
            var order = Surveyed(ledger, "probe-" + state);
            var report = order.Report!;
            if (state != ResearchOrderState.Surveyed)
            {
                var move = state switch
                {
                    ResearchOrderState.Paid => Move.Paid, ResearchOrderState.Declined => Move.DeclineFunds,
                    ResearchOrderState.Cancelled => Move.Cancel, _ => Move.Unknown,
                };
                Assert.Equal(ResearchMoveRefusal.None, Apply(ledger, order, move));
            }
            bool read = report.TryRead(ledger, order.Id, out var numbers);
            Assert.Equal(state == ResearchOrderState.Paid, read);
            Assert.Equal(state == ResearchOrderState.Paid, numbers != null);
        }
        var ordered = Order(ledger, "probe-ordered");
        Assert.False(ledger.TryGet(ordered.Id, out _));

        var paid = ledger.Kept.Single();
        Assert.True(ledger.TryGet(paid.Id, out var grant));
        Assert.Equal(paid.Id, grant!.EntitlementId); Assert.Equal(paid.ReportId, grant.ReportId);
        Assert.True(grant.Paid && grant.Delivered && !grant.Replaced);
        // The grant pairs one order with one report: another paid order's ID cannot open this report.
        var other = Surveyed(ledger, "probe-other");
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(other.Id));
        Assert.False(paid.Report!.TryRead(ledger, other.Id, out _));
        Assert.False(paid.Report.TryRead(ledger, "", out _));
        Assert.False(paid.Report.TryRead(NoPaymentPath.Instance, paid.Id, out _));
        // Mutating a returned grant cannot change the next one.
        grant.Replaced = true; grant.ReportId = "forged";
        Assert.True(paid.Report.TryRead(ledger, paid.Id, out var paidNumbers));
        Assert.Equal(7, paidNumbers!.Single().Value);

        // Retention: the 17th delivered report replaces the oldest.
        var held = paid.Report;
        for (int i = ledger.Kept.Count; i < ResearchLedger.MaximumKeptReports; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Surveyed(ledger, "fill-" + i).Id));
        Assert.Equal(ResearchOrderState.Paid, paid.State);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Surveyed(ledger, "seventeenth").Id));
        Assert.Equal(ResearchOrderState.Replaced, paid.State);
        Assert.Null(paid.Report); Assert.Equal(held.Id, paid.ReportId);
        Assert.Equal(ResearchLedger.MaximumKeptReports, ledger.Kept.Count);
        Assert.DoesNotContain(paid, ledger.Kept); Assert.Contains(paid, ledger.Closed);
        Assert.True(ledger.TryGet(paid.Id, out var replaced));
        Assert.True(replaced!.Replaced); Assert.Equal(held.Id, replaced.ReportId);
        Assert.False(held.TryRead(ledger, paid.Id, out var none)); Assert.Null(none);
        Assert.Equal(ResearchMoveRefusal.WrongState, ledger.TryMarkPaid(paid.Id));
        Assert.Equal(ResearchOrderState.Replaced, paid.State);
    }

    [Fact]
    public void ClosedRecordsAreBoundedOldestFirstAndSequencesAreNeverReused()
    {
        var ledger = new ResearchLedger();
        var all = new List<ResearchOrder>();
        for (int i = 0; i < ResearchLedger.MaximumClosedOrders + 5; i++)
        {
            var order = Surveyed(ledger, "biz");
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(order.Id));
            all.Add(order);
        }
        Assert.Equal(ResearchLedger.MaximumClosedOrders, ledger.Closed.Count);
        Assert.Equal(all.Skip(5), ledger.Closed);
        Assert.Null(ledger.Find(all[0].Id));
        Assert.Equal(all.Select(o => o.Sequence), Enumerable.Range(1, all.Count).Select(i => (long)i));
        var next = Order(ledger, "biz");
        Assert.Equal("pcr-" + (all.Count + 1), next.Id);
        Assert.True(ledger.Open.Count + ledger.Kept.Count + ledger.Closed.Count
            <= ResearchLedger.MaximumOpenOrders + ResearchLedger.MaximumKeptReports + ResearchLedger.MaximumClosedOrders);
    }

    // ---- Restore seam (C5c) ----

    private static (long, List<ResearchOrderImage>, List<ResearchOrderImage>, List<ResearchOrderImage>) Images(ResearchLedger ledger)
        => (ledger.NextSequence, ledger.Open.Select(ResearchOrderImage.Of).ToList(),
            ledger.Kept.Select(ResearchOrderImage.Of).ToList(), ledger.Closed.Select(ResearchOrderImage.Of).ToList());

    private static ResearchLedger MixedLedger()
    {
        var ledger = new ResearchLedger();
        InState(ledger, ResearchOrderState.Replaced, "r");
        InState(ledger, ResearchOrderState.Declined, "d");
        InState(ledger, ResearchOrderState.Cancelled, "c");
        InState(ledger, ResearchOrderState.PaymentUnknown, "u");
        Surveyed(ledger, "s");
        Order(ledger, "o", ResearchProducts.DetailedCityCode);
        return ledger;
    }

    [Fact]
    public void RestoreRebuildsTheSameLedgerAndEntitlements()
    {
        var source = MixedLedger();
        var (next, open, kept, closed) = Images(source);
        Assert.True(ResearchLedger.TryRestore(next, open, kept, closed, out var copy, out var error), error);
        Assert.Equal(source.NextSequence, copy!.NextSequence);
        foreach (var (a, b) in new[] { (source.Open, copy.Open), (source.Kept, copy.Kept), (source.Closed, copy.Closed) })
        {
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal((a[i].Id, a[i].ProductCode, a[i].Price, a[i].BusinessAddress, a[i].AgencyAddress, a[i].TaxDeductible,
                    a[i].RadiusMetres, a[i].OrderMinute, a[i].SurveyAtMinute, a[i].State, a[i].DeclineReason, a[i].ReportId, a[i].Report),
                    (b[i].Id, b[i].ProductCode, b[i].Price, b[i].BusinessAddress, b[i].AgencyAddress, b[i].TaxDeductible,
                    b[i].RadiusMetres, b[i].OrderMinute, b[i].SurveyAtMinute, b[i].State, b[i].DeclineReason, b[i].ReportId, b[i].Report));
            }
        }
        foreach (var paid in copy.Kept) Assert.True(paid.Report!.TryRead(copy, paid.Id, out _));
        Assert.Equal(ResearchOrderRefusal.BusinessHasOpenOrder, copy.TryOrder("basic-local", "s", "agency-1", false, 500, Minute, out _));
        Assert.Equal(ResearchOrderRefusal.None, copy.TryOrder("basic-local", "new", "agency-1", false, 500, Minute, out var fresh));
        Assert.Equal(ResearchLedger.OrderId(next), fresh!.Id);
    }

    [Fact]
    public void RestoreRefusesEveryInconsistentLedger()
    {
        var source = MixedLedger();
        var corruptions = new List<(string, Action<(long next, List<ResearchOrderImage> open, List<ResearchOrderImage> kept, List<ResearchOrderImage> closed)>)>
        {
            ("unknown product", s => s.open[0].ProductCode = "in-depth-local"),
            ("price", s => s.open[0].Price = s.open[0].Price + Money.FromUnits(1)),
            ("state", s => s.open[0].State = (ResearchOrderState)42),
            ("state in wrong list", s => s.open[0].State = ResearchOrderState.Paid),
            ("decline reason", s => s.closed.First(o => o.State == ResearchOrderState.Cancelled).DeclineReason = ResearchDeclineReason.InsufficientFunds),
            ("missing decline reason", s => s.closed.First(o => o.State == ResearchOrderState.Declined).DeclineReason = ResearchDeclineReason.None),
            ("report on closed", s => s.closed[0].Report = s.kept[0].Report),
            ("missing report", s => s.kept[0].Report = null),
            ("report id", s => s.kept[0].ReportId = "other"),
            ("reused report", s => { s.closed[0].ReportId = s.kept[0].ReportId; }),
            ("reused sequence", s => s.closed[0].Sequence = s.kept[0].Sequence),
            ("sequence ahead", s => s.open[0].Sequence = s.next),
            ("minute", s => s.open[0].SurveyAtMinute += 1),
            ("radius", s => s.open[1].RadiusMetres = 500),
            ("address", s => s.open[0].BusinessAddress = " "),
            ("two open per business", s => s.open[1].BusinessAddress = s.open[0].BusinessAddress),
            ("too many open", s => { for (int i = 0; i < 7; i++) s.open.Add(s.open[0]); }),
        };
        foreach (var (name, corrupt) in corruptions)
        {
            var images = Images(source);
            corrupt((images.Item1, images.Item2, images.Item3, images.Item4));
            Assert.False(ResearchLedger.TryRestore(images.Item1, images.Item2, images.Item3, images.Item4, out var ledger, out var error), name);
            Assert.Null(ledger); Assert.NotEqual("", error);
        }
        var ok = Images(source);
        Assert.False(ResearchLedger.TryRestore(0, ok.Item2, ok.Item3, ok.Item4, out _, out _));
        Assert.False(ResearchLedger.TryRestore(ok.Item1 - 1, ok.Item2, ok.Item3, ok.Item4, out _, out _));
    }

    // ---- Perf (recorded in docs/perf/c5a.md) ----

    [Fact]
    public void OrderAndGrantCostsAreSmallAndFindDoesNotAllocate()
    {
        var ledger = new ResearchLedger();
        for (int i = 0; i < ResearchLedger.MaximumKeptReports; i++) InState(ledger, ResearchOrderState.Paid, "paid-" + i);
        for (int i = 0; i < ResearchLedger.MaximumClosedOrders; i++) InState(ledger, ResearchOrderState.Cancelled, "closed-" + i);
        for (int i = 0; i < ResearchLedger.MaximumOpenOrders - 1; i++) Surveyed(ledger, "open-" + i);
        string lastPaid = ledger.Kept[^1].Id;
        ledger.Find(lastPaid); ledger.TryGet(lastPaid, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) ledger.Find(lastPaid);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        const int samples = 20000;
        string Line(string name, double[] values, long bytes)
        {
            Array.Sort(values);
            return $"{name}: p50 {values[samples / 2] * 1000:0.00} us, p95 {values[samples * 95 / 100] * 1000:0.00} us, "
                + $"max {values[^1] * 1000:0.00} us, {bytes / (double)samples:0} B/call";
        }
        string grantLine = "", orderLine = "";
        // The ledger is shared across attempts: each sample orders, seals and cancels, so it stays at the same bounds.
        TimingBudget.Run(timing =>
        {
            var grant = new double[samples];
            var order = new double[samples];
            long grantBytes = 0, orderBytes = 0;
            for (int i = 0; i < samples; i++)
            {
                before = GC.GetAllocatedBytesForCurrentThread();
                var window = timing.Start();
                ledger.TryGet(lastPaid, out _);
                grant[i] = timing.Stop(window);
                grantBytes += GC.GetAllocatedBytesForCurrentThread() - before;

                before = GC.GetAllocatedBytesForCurrentThread();
                window = timing.Start();
                var refusal = ledger.TryOrder("basic-local", "perf-biz", "agency-1", false, 500, Minute, out var placed);
                order[i] = timing.Stop(window);
                orderBytes += GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(ResearchOrderRefusal.None, refusal);
                Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(placed!.Id, ReportFor(placed)));
                Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(placed.Id));
            }
            grantLine = Line("TryGet (paid, worst-case position)", grant, grantBytes);
            orderLine = Line("TryOrder (7 open, 16 kept, 16 closed)", order, orderBytes);
            timing.Under("TryGet p95", grant[samples * 95 / 100], 0.5);
            timing.Under("TryOrder p95", order[samples * 95 / 100], 0.5);
        });
        output.WriteLine(grantLine);
        output.WriteLine(orderLine);
    }
}
