using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

/// <summary>C5d research billing through pure port doubles (plan C5 section 3.5). The double follows the native
/// ChangeMoneySafe and ChangeMoney contract read at BigAmbitions.dll 9427561b...: false before any change when
/// money is short, otherwise binary32 Money += amount, one queued record with balance floor(Money), then the
/// money-change event. Offline only; no native evidence.</summary>
public sealed class ResearchChargeTests
{
    private const double Minute = 9 * 60 + 30;
    private readonly ITestOutputHelper output;
    public ResearchChargeTests(ITestOutputHelper output) { this.output = output; }

    // ---- doubles ----

    private sealed class Record
    {
        public string Type = ResearchCharge.MarketingType;
        public List<string>? Categories = new List<string> { ResearchCharge.MarketingCategory };
        public float Amount, Balance;
        public int Day;
        public string? Address;
        public bool TaxDeductible;
        public Dictionary<string, string>? Data;
    }

    private sealed class Port : IResearchChargePort
    {
        public bool TagStored { get; set; } = true;
        public float Money = 50000;
        public int Day = 41;
        public readonly List<Record> Queue = new List<Record>();
        public readonly Dictionary<string, string> Businesses = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly HashSet<string> Ineligible = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<ResearchChargeRequest> Requests = new List<ResearchChargeRequest>();
        public readonly List<bool> Campaigns = new List<bool> { true, true };
        public int Calls, BalanceReads, Charges;
        public bool ThrowBeforeEntry, ThrowAfterEntry, SkipEntry, EnterTwice, ReturnTrueWithoutChange, AppendOnFalse, Duplicate;
        public int DropFront;
        public Action<Record>? Corrupt;
        public Action? OnMoneyChange;
        public Func<Record>? Listener;

        public float Balance { get { Calls++; BalanceReads++; return Money; } }
        public int BilledDay { get { Calls++; return Day - 1; } }

        public bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target)
        {
            Calls++;
            target = null;
            if (!Businesses.TryGetValue(order.BusinessAddress, out var address) || Ineligible.Contains(order.BusinessAddress)) return false;
            target = new ResearchChargeTarget("Name of " + order.BusinessAddress, address);
            return true;
        }

        public void CollectTagged(List<ResearchChargeRecord> into)
        {
            Calls++;
            foreach (var record in Queue) if (Tagged(record, out var row)) into.Add(row);
        }

        public object? Tail() { Calls++; return Queue.Count == 0 ? null : Queue[Queue.Count - 1]; }

        public void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into)
        {
            Calls++;
            int start = tail == null ? 0 : Queue.FindIndex(r => ReferenceEquals(r, tail)) + 1; // -1 + 1: tail dropped
            for (int i = start; i < Queue.Count; i++) if (Tagged(Queue[i], out var row)) into.Add(row);
        }

        public bool Charge(in ResearchChargeRequest request, Action enteringNative)
        {
            Calls++; Charges++;
            Requests.Add(request);
            if (ThrowBeforeEntry) throw new InvalidOperationException("before entry");
            if (!SkipEntry) enteringNative();
            if (EnterTwice) enteringNative();
            if (ReturnTrueWithoutChange) return true;
            if (request.Amount < 0 && Money < -request.Amount)
            {
                if (AppendOnFalse) Queue.Add(Make(request, Money));
                return false;
            }
            Money += request.Amount; // binary32, as native ChangeMoney
            var record = Make(request, (float)Math.Floor(Money));
            Corrupt?.Invoke(record);
            Queue.Add(record);
            if (Duplicate) Queue.Add(Make(request, (float)Math.Floor(Money)));
            if (DropFront > 0) Queue.RemoveRange(0, Math.Min(DropFront, Queue.Count));
            if (ThrowAfterEntry) throw new InvalidOperationException("after entry");
            if (Listener != null) Queue.Add(Listener());
            OnMoneyChange?.Invoke();
            return true;
        }

        private static Record Make(in ResearchChargeRequest request, float balance)
        {
            var data = new Dictionary<string, string> { { ResearchCharge.BusinessNameKey, request.Target.BusinessName }, { ResearchCharge.TagKey, request.OrderId } };
            if (request.TaxDeductible) data["taxDeductibleName"] = ResearchCharge.TaxDeductibleName;
            return new Record { Amount = request.Amount, Balance = balance, Day = request.Day, Address = (string)request.Target.Address,
                TaxDeductible = request.TaxDeductible, Data = data };
        }

        private static bool Tagged(Record record, out ResearchChargeRecord row)
        {
            row = default;
            if (record.Data == null || !record.Data.TryGetValue(ResearchCharge.TagKey, out var tag)) return false;
            row = new ResearchChargeRecord(tag, record.Type == ResearchCharge.MarketingType && record.Categories != null
                && record.Categories.Contains(ResearchCharge.MarketingCategory), record.TaxDeductible, record.Amount, record.Balance, record.Day, record.Address);
            return true;
        }

        public Record Existing(ResearchOrder order, float? amount = null) => new Record
        {
            Amount = amount ?? -order.Product.NativePrice, Balance = 1000, Day = Day - 3, Address = Businesses[order.BusinessAddress],
            Data = new Dictionary<string, string> { { ResearchCharge.TagKey, order.Id } }
        };
    }

    private sealed class Guard
    {
        public int Depth, Units, MaxDepth;
        public void Run(Action unit)
        {
            Depth++; Units++; MaxDepth = Math.Max(MaxDepth, Depth);
            try { unit(); } finally { Depth--; }
        }
    }

    private static int reportSerial;

    private static ResearchOrder Surveyed(ResearchLedger ledger, Port port, string business, string code = ResearchProducts.BasicLocalCode, bool tax = false)
    {
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(code, business, "agency-1", tax, 500, Minute, out var order));
        var report = new MarketResearchReport("q-" + order!.Id + "-" + (++reportSerial), order.Product.Tier, 3,
            order.Product.Coverage == ResearchCoverage.City ? null : order.BusinessAddress, order.RadiusMetres,
            new[] { new MarketResearchCell(MarketResearchMetric.Consumers, MarketResearchBand.All, 7) });
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(order.Id, report));
        port.Businesses[business] = "street-of-" + business + "#1";
        return order;
    }

    private static (ResearchLedger ledger, Port port, Guard guard, ResearchChargeSession session) Setup(Func<bool>? active = null)
    {
        var ledger = new ResearchLedger();
        var port = new Port();
        var guard = new Guard();
        return (ledger, port, guard, new ResearchChargeSession(ledger, port, active ?? (() => true), guard.Run));
    }

    // ---- outcomes ----

    [Fact]
    public void SuccessChargesOnceWithNativeMarketingDataAndMarksPaid()
    {
        var (ledger, port, guard, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        var tally = session.Boundary();
        Assert.Equal(1, tally.Paid);
        Assert.Equal(ResearchOrderState.Paid, order.State);
        Assert.Equal(49500f, port.Money);
        var request = Assert.Single(port.Requests);
        Assert.Equal(order.Id, request.OrderId);
        Assert.Equal(-500f, request.Amount);
        Assert.Equal(port.Day - 1, request.Day);
        Assert.Equal("street-of-shop-a#1", request.Target.Address);
        var record = Assert.Single(port.Queue);
        Assert.Equal(order.Id, record.Data![ResearchCharge.TagKey]);
        Assert.Equal("Name of shop-a", record.Data[ResearchCharge.BusinessNameKey]);
        Assert.Equal(49500f, record.Balance);
        Assert.True(ledger.TryGet(order.Id, out var grant) && grant!.Paid && grant.Delivered && !grant.Replaced);
        Assert.Equal(1, guard.Units);
        Assert.Equal(1, guard.MaxDepth);
    }

    [Fact]
    public void InsufficientFundsDeclinesWithNoMoneyChangeAndNoRecord()
    {
        var (ledger, port, _, session) = Setup();
        port.Money = 499.5f;
        var order = Surveyed(ledger, port, "shop-a");
        Assert.Equal(1, session.Boundary().Declined);
        Assert.Equal(ResearchOrderState.Declined, order.State);
        Assert.Equal(ResearchDeclineReason.InsufficientFunds, order.DeclineReason);
        Assert.Equal(499.5f, port.Money);
        Assert.Empty(port.Queue);
        Assert.Equal(1, port.Charges);
        Assert.False(ledger.TryGet(order.Id, out _));
    }

    [Fact]
    public void FalseWithATaggedRecordIsPaymentUnknown()
    {
        var (ledger, port, _, session) = Setup();
        port.Money = 10; port.AppendOnFalse = true;
        var order = Surveyed(ledger, port, "shop-a");
        Assert.Equal(1, session.Boundary().PaymentUnknown);
        Assert.Equal(ResearchOrderState.PaymentUnknown, order.State);
    }

    [Fact]
    public void ExceptionBeforeEntryLeavesTheOrderSurveyedForTheNextBill()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        var other = Surveyed(ledger, port, "shop-b");
        port.ThrowBeforeEntry = true;
        var tally = session.Boundary();
        Assert.Equal(2, tally.Deferred);
        Assert.Equal(ResearchOrderState.Surveyed, order.State);
        Assert.Equal(ResearchOrderState.Surveyed, other.State);
        Assert.Equal(50000f, port.Money);
        port.ThrowBeforeEntry = false;
        Assert.Equal(2, session.Boundary().Paid);
        Assert.Equal(49000f, port.Money);
    }

    [Fact]
    public void ExceptionAfterEntryBlocksOnlyThatOrderAndNeverRetries()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        port.ThrowAfterEntry = true;
        Assert.Equal(1, session.Boundary().PaymentUnknown);
        Assert.Equal(ResearchOrderState.PaymentUnknown, order.State);
        port.ThrowAfterEntry = false;
        var next = Surveyed(ledger, port, "shop-a");
        int charges = port.Charges;
        Assert.Equal(1, session.Boundary().Paid);
        Assert.Equal(charges + 1, port.Charges);
        Assert.Single(port.Requests, r => r.OrderId == order.Id);
        Assert.Equal(ResearchOrderState.Paid, next.State);
        Assert.Equal(ResearchOrderState.PaymentUnknown, order.State);
    }

    [Fact]
    public void ContractViolationsOfThePortArePaymentUnknown()
    {
        foreach (var violation in new Action<Port>[] { p => p.SkipEntry = p.ReturnTrueWithoutChange = true, p => p.EnterTwice = true, p => p.ReturnTrueWithoutChange = true })
        {
            var (ledger, port, _, session) = Setup();
            var order = Surveyed(ledger, port, "shop-a");
            violation(port);
            Assert.Equal(1, session.Boundary().PaymentUnknown);
            Assert.Equal(ResearchOrderState.PaymentUnknown, order.State);
        }
        var (ledger2, port2, _, session2) = Setup();
        var deferred = Surveyed(ledger2, port2, "shop-a");
        port2.SkipEntry = true; port2.Money = 0; // false without entry: no native call was made
        Assert.Equal(1, session2.Boundary().Deferred);
        Assert.Equal(ResearchOrderState.Surveyed, deferred.State);
    }

    public static IEnumerable<object[]> Mismatches() => new[]
    {
        new object[] { "amount" }, new object[] { "address" }, new object[] { "day" }, new object[] { "balance" },
        new object[] { "id" }, new object[] { "type" }, new object[] { "category" }, new object[] { "tax" }
    };

    [Theory]
    [MemberData(nameof(Mismatches))]
    public void AppendedRecordMismatchIsPaymentUnknown(string field)
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        port.Corrupt = record =>
        {
            switch (field)
            {
                case "amount": record.Amount = -496f; break;
                case "address": record.Address = "elsewhere#2"; break;
                case "day": record.Day += 1; break;
                case "balance": record.Balance -= 1; break;
                case "id": record.Data![ResearchCharge.TagKey] = "pcr-999"; break;
                case "type": record.Type = "ba:transaction_rent"; break;
                case "category": record.Categories = null; break;
                case "tax": record.TaxDeductible = !record.TaxDeductible; break;
            }
        };
        Assert.Equal(1, session.Boundary().PaymentUnknown);
        Assert.Equal(ResearchOrderState.PaymentUnknown, order.State);
        Assert.False(ledger.TryGet(order.Id, out _));
    }

    [Fact]
    public void ZeroOrTwoMatchingRecordsArePaymentUnknown()
    {
        var (ledger, port, _, session) = Setup();
        var none = Surveyed(ledger, port, "shop-a");
        port.Corrupt = record => record.Data!.Remove(ResearchCharge.TagKey);
        session.Boundary();
        Assert.Equal(ResearchOrderState.PaymentUnknown, none.State);

        var (ledger2, port2, _, session2) = Setup();
        var two = Surveyed(ledger2, port2, "shop-a");
        port2.Duplicate = true;
        session2.Boundary();
        Assert.Equal(ResearchOrderState.PaymentUnknown, two.State);
    }

    [Fact]
    public void UnrelatedRecordsAppendedByAMoneyChangeListenerStillGivePaid()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        var other = Surveyed(ledger, port, "shop-b");
        int fired = 0;
        port.Listener = () => ++fired % 2 == 1
            ? new Record { Type = "ba:transaction_casino", Amount = 3, Data = new Dictionary<string, string> { { "x", "y" } } }
            : new Record { Amount = -1, Address = "x", Data = new Dictionary<string, string> { { ResearchCharge.TagKey, "pcr-9999" } } };
        Assert.Equal(2, session.Boundary().Paid);
        Assert.Equal(ResearchOrderState.Paid, order.State);
        Assert.Equal(ResearchOrderState.Paid, other.State);
    }

    [Fact]
    public void QueueFrontDroppedDuringTheCallStillFindsTheAppendedRecord()
    {
        foreach (int drop in new[] { 1, 5, 6 }) // 6 drops the remembered tail itself
        {
            var (ledger, port, _, session) = Setup();
            for (int i = 0; i < 5; i++) port.Queue.Add(new Record { Type = "ba:transaction_rent", Amount = -10 });
            var order = Surveyed(ledger, port, "shop-a");
            port.DropFront = drop;
            Assert.Equal(1, session.Boundary().Paid);
            Assert.Equal(ResearchOrderState.Paid, order.State);
        }
    }

    [Fact]
    public void AnExistingRecordFoundByTheScanIsPaidWithoutACharge()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        port.Queue.Add(new Record { Type = "ba:transaction_rent", Amount = -10 });
        port.Queue.Add(port.Existing(order));
        Assert.Equal(1, session.Boundary().PaidFromScan);
        Assert.Equal(ResearchOrderState.Paid, order.State);
        Assert.Equal(0, port.Charges);
        Assert.Equal(50000f, port.Money);
    }

    [Fact]
    public void AnExistingRecordWithAnotherAmountOrTwoRecordsArePaymentUnknownWithoutACharge()
    {
        var (ledger, port, _, session) = Setup();
        var wrongAmount = Surveyed(ledger, port, "shop-a");
        var twice = Surveyed(ledger, port, "shop-b");
        port.Queue.Add(port.Existing(wrongAmount, -496f));
        port.Queue.Add(port.Existing(twice));
        port.Queue.Add(port.Existing(twice));
        Assert.Equal(2, session.Boundary().PaymentUnknown);
        Assert.Equal(ResearchOrderState.PaymentUnknown, wrongAmount.State);
        Assert.Equal(ResearchOrderState.PaymentUnknown, twice.State);
        Assert.Equal(0, port.Charges);
    }

    [Fact]
    public void RepeatedBoundaryChargesEachOrderAtMostOnce()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        for (int day = 0; day < 4; day++) { session.Boundary(); port.Day++; }
        Assert.Equal(1, port.Charges);
        Assert.Equal(49500f, port.Money);
        Assert.Equal(ResearchOrderState.Paid, order.State);

        // Ledger move lost (for example a reload from the save that already holds the debit): the scan pays it.
        var (ledger2, port2, _, session2) = Setup();
        var again = Surveyed(ledger2, port2, "shop-a");
        port2.Queue.AddRange(port.Queue.Select(r => { r.Data![ResearchCharge.TagKey] = again.Id; return r; }));
        port2.Businesses["shop-a"] = port.Businesses["shop-a"];
        Assert.Equal(1, session2.Boundary().PaidFromScan);
        Assert.Equal(0, port2.Charges);
    }

    [Fact]
    public void StoppedOrDetachedRuntimeDoesNothing()
    {
        bool active = false;
        var (ledger, port, guard, session) = Setup(() => active);
        var order = Surveyed(ledger, port, "shop-a");
        var tally = session.Boundary();
        Assert.Equal(0, tally.Attempts);
        Assert.Equal(0, port.Calls);
        Assert.Equal(0, guard.Units);
        Assert.Equal(ResearchOrderState.Surveyed, order.State);

        // Stopped after the first order (for example by a refused save): later orders wait, uncharged.
        active = true;
        var later = Surveyed(ledger, port, "shop-b");
        port.OnMoneyChange = () => active = false;
        Assert.Equal(1, session.Boundary().Paid);
        Assert.Equal(ResearchOrderState.Paid, order.State);
        Assert.Equal(ResearchOrderState.Surveyed, later.State);
        Assert.Equal(1, port.Charges);
    }

    [Fact]
    public void BusinessNoLongerEligibleIsCancelledWithoutACharge()
    {
        var (ledger, port, _, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        var gone = Surveyed(ledger, port, "shop-b");
        port.Ineligible.Add("shop-a");
        port.Businesses.Remove("shop-b");
        Assert.Equal(2, session.Boundary().Cancelled);
        Assert.Equal(ResearchOrderState.Cancelled, order.State);
        Assert.Equal(ResearchOrderState.Cancelled, gone.State);
        Assert.Equal(0, port.Charges);
    }

    [Fact]
    public void CampaignsAreNeverTouched()
    {
        var (ledger, port, _, session) = Setup();
        port.Money = 100;
        Surveyed(ledger, port, "shop-a");
        session.Boundary();
        Assert.Equal(new[] { true, true }, port.Campaigns);
        // The native port has no campaign access at all; native RunDaily alone disables campaigns on its own bill.
        string billing = File.ReadAllText(Path.Combine(Root(), "Runtime", "ResearchBilling.cs"));
        Assert.DoesNotContain("marketingCampaigns", billing);
        Assert.DoesNotContain("MarketingCampaign", billing);
        Assert.DoesNotContain(".enabled", billing);
        Assert.DoesNotContain("UpdatePromotion", billing);
        Assert.DoesNotContain("force: true", billing);
        Assert.Contains("force: false", billing);
    }

    [Fact]
    public void TaxFlagSavedAtOrderTimeIsCarriedToTheCharge()
    {
        var (ledger, port, _, session) = Setup();
        var taxed = Surveyed(ledger, port, "shop-a", tax: true);
        var plain = Surveyed(ledger, port, "shop-b", tax: false);
        Assert.Equal(2, session.Boundary().Paid);
        Assert.True(port.Requests.Single(r => r.OrderId == taxed.Id).TaxDeductible);
        Assert.False(port.Requests.Single(r => r.OrderId == plain.Id).TaxDeductible);
        Assert.Equal(ResearchCharge.TaxDeductibleName, port.Queue.Single(r => r.Data![ResearchCharge.TagKey] == taxed.Id).Data!["taxDeductibleName"]);
    }

    [Fact]
    public void UnsupportedTagKeepsChargingOff()
    {
        var (ledger, port, guard, session) = Setup();
        var order = Surveyed(ledger, port, "shop-a");
        port.TagStored = false;
        var tally = session.Boundary();
        Assert.True(tally.ChargingOff);
        Assert.Equal(0, tally.Attempts);
        Assert.Equal(0, port.Calls);
        Assert.Equal(0, guard.Units);
        Assert.Equal(ResearchOrderState.Surveyed, order.State);
        Assert.Empty(port.Queue);
    }

    [Fact]
    public void OnlySurveyedOrdersAreBilledEachInItsOwnGuardedUnit()
    {
        var (ledger, port, guard, session) = Setup();
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(ResearchProducts.BasicLocalCode, "shop-z", "agency-1", false, 500, Minute, out var ordered));
        var a = Surveyed(ledger, port, "shop-a", ResearchProducts.DetailedLocalCode);
        var b = Surveyed(ledger, port, "shop-b", ResearchProducts.DetailedCityCode);
        int depthInCharge = -1;
        port.OnMoneyChange = () => depthInCharge = guard.Depth;
        Assert.Equal(2, session.Boundary().Paid);
        Assert.Equal(2, guard.Units);
        Assert.Equal(1, depthInCharge);
        Assert.Equal(ResearchOrderState.Ordered, ordered!.State);
        Assert.Equal(50000f - 2000 - 6000, port.Money);
        Assert.Equal(new[] { a.Id, b.Id }, port.Requests.Select(r => r.OrderId));
    }

    // ---- exact-debit preflight ----

    private static ResearchProduct Price(int dollars) => new ResearchProduct("test-" + dollars, MarketResearchTier.Basic, ResearchCoverage.Catchment, dollars);

    [Fact]
    public void PreflightNamedBalances()
    {
        var basic = ResearchProducts.BasicLocal;
        Assert.True(ResearchCharge.TryExactDebit(0f, basic, out float after, out float floored));
        Assert.Equal(-500f, after); Assert.Equal(-500f, floored);
        Assert.True(ResearchCharge.TryExactDebit(16777216f, basic, out after, out floored));
        Assert.Equal(16776716f, after); Assert.Equal(16776716f, floored);
        // Spacing $8 at $100,000,000: native would remove $496.
        Assert.Equal(100000000f - 500f, 100000000f - 496f);
        Assert.False(ResearchCharge.TryExactDebit(100000000f, basic, out _, out _));
        var p512 = Price(512);
        Assert.True(ResearchCharge.TryExactDebit(100000000f, p512, out after, out floored));
        float native = 100000000f; native += -512f;
        Assert.Equal(native, after);
        Assert.Equal(99999488f, after);
        Assert.True(ResearchCharge.TryExactDebit(100000000f, Price(504), out _, out _));
        Assert.True(ResearchCharge.TryExactDebit(1234.75f, basic, out after, out floored));
        Assert.Equal(734.75f, after); Assert.Equal(734f, floored);
        Assert.True(ResearchCharge.TryExactDebit(-0.25f, basic, out after, out floored));
        Assert.Equal(-500.25f, after); Assert.Equal(-501f, floored);
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, float.Epsilon, 1e-30f })
            Assert.False(ResearchCharge.TryExactDebit(bad, basic, out _, out _));
    }

    [Fact]
    public void PreflightAtOneHundredMillionRefusesBeforeAnyNativeCall()
    {
        var (ledger, port, guard, session) = Setup();
        port.Money = 100000000f;
        var order = Surveyed(ledger, port, "shop-a");
        Assert.Equal(1, session.Boundary().BalancePrecision);
        Assert.Equal(ResearchOrderState.Declined, order.State);
        Assert.Equal(ResearchDeclineReason.BalancePrecision, order.DeclineReason);
        Assert.Equal(0, port.Charges);
        Assert.Equal(100000000f, port.Money);
        Assert.Empty(port.Queue);
        Assert.Equal(1, guard.Units);

        var (ledger2, port2, _, session2) = Setup();
        port2.Money = 16777216f;
        var exact = Surveyed(ledger2, port2, "shop-a");
        Assert.Equal(1, session2.Boundary().Paid);
        Assert.Equal(16776716f, port2.Money);
        Assert.Equal(ResearchOrderState.Paid, exact.State);
    }

    /// <summary>Independent oracle: the balance scaled by 2^160 is an integer double, so BigInteger holds it exactly.</summary>
    private static bool Oracle(float balance, int price)
    {
        if (float.IsNaN(balance) || float.IsInfinity(balance)) return false;
        const int scale = 160;
        var exact = new BigInteger(Math.ScaleB(balance, scale)) - (new BigInteger(price) << scale);
        if (exact.IsZero) return true;
        int shift = 0;
        while (exact.IsEven) { exact >>= 1; shift++; }
        var magnitude = BigInteger.Abs(exact);
        int power = shift - scale;
        if (magnitude >= (BigInteger.One << 24) || power < -149) return false;
        return magnitude.GetBitLength() + power <= 128;
    }

    [Fact]
    public void PreflightSweepEveryProductPriceAgainstEveryBinary32Exponent()
    {
        int checkedCount = 0, exact = 0;
        var prices = ResearchProducts.All.Select(p => p).Concat(new[] { Price(1), Price(504), Price(512), Price(10000) }).ToArray();
        foreach (var product in prices)
            for (int biased = 0; biased < 255; biased++)
                foreach (int mantissa in new[] { 0, 1, 2, 0x400000, 0x7ffffe, 0x7fffff, 0x2a5a5a })
                    foreach (int sign in new[] { 0, unchecked((int)0x80000000) })
                    {
                        float balance = BitConverter.Int32BitsToSingle(sign | biased << 23 | mantissa);
                        bool expected = Oracle(balance, product.PriceDollars);
                        bool actual = ResearchCharge.TryExactDebit(balance, product, out float after, out float floored);
                        Assert.True(expected == actual, $"balance {balance:R} price {product.PriceDollars}");
                        checkedCount++;
                        if (!actual) continue;
                        exact++;
                        float native = balance; native += -product.NativePrice;
                        Assert.Equal(native, after);
                        Assert.Equal((double)native, (double)balance - product.PriceDollars);
                        Assert.Equal((float)Math.Floor(native), floored);
                    }
        output.WriteLine($"preflight sweep: {checkedCount} balances, {exact} exact");
        Assert.True(exact > 0 && exact < checkedCount);
    }

    // ---- save guard ----

    /// <summary>Mirrors PopulationRuntime: Save (SaveEpochRuntime.cs), the RunSaveEpochUnit helper and the deferred stop.</summary>
    private sealed class EpochRuntime
    {
        public readonly object Instance = new object();
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.Ordinal) { { SaveStore.StateKey, "payload-before" } };
        public readonly SaveEpochBinding Epoch;
        public int Units, Stops, Captures;
        public bool StopPending, Failed;
        public SaveEpochResult LastSave;
        public Func<string> Payload = () => "payload-after";

        public EpochRuntime() { Epoch = new SaveEpochBinding(Instance, Data, 0); Epoch.CompleteInitialization(); }

        public void Save()
        {
            LastSave = SaveEpochCallback.Run(Epoch, Instance, Data, Units > 0, ticket =>
            {
                Captures++;
                Data[SaveStore.StateKey] = Payload();
                return SaveEpochCapture.Written(Minute, "population");
            }, _ => Stop(), _ => { });
            if (LastSave == SaveEpochResult.Reentrant) StopPending = true;
            if (StopPending && Units == 0 && !Epoch.Saving) { StopPending = false; Stop(); }
        }

        public void RunSaveEpochUnit(Action unit)
        {
            Units++;
            try { unit(); }
            finally { Units--; if (StopPending && Units == 0) { StopPending = false; Stop(); } }
        }

        private void Stop() { if (!Failed) { Stops++; Failed = true; Epoch.Detach(Instance); } }
    }

    [Fact]
    public void SaveInsideTheChargeWritesFailureMarkersAndPublishesNoPayload()
    {
        var runtime = new EpochRuntime();
        var ledger = new ResearchLedger();
        var port = new Port();
        var session = new ResearchChargeSession(ledger, port, () => !runtime.Failed, runtime.RunSaveEpochUnit);
        var first = Surveyed(ledger, port, "shop-a");
        var second = Surveyed(ledger, port, "shop-b");
        runtime.Payload = () => "state " + first.State;
        bool stoppedDuringUnit = true;
        port.OnMoneyChange = () => { runtime.Save(); stoppedDuringUnit = runtime.Failed; };
        var tally = session.Boundary();

        Assert.Equal(SaveEpochResult.Reentrant, runtime.LastSave);
        Assert.Equal(0, runtime.Captures);
        Assert.Equal("payload-before", runtime.Data[SaveStore.StateKey]);
        Assert.StartsWith(SaveEpochStore.FailedPrefix, runtime.Data[SaveEpochStore.Key]);
        Assert.True(AccountingStopStore.IsStopped(runtime.Data));
        Assert.False(stoppedDuringUnit); // the ledger move completes inside the unit first
        Assert.Equal(ResearchOrderState.Paid, first.State);
        Assert.Equal(1, runtime.Stops);
        // Stopped after the first unit: the second order is not charged in a city that can no longer be saved.
        Assert.Equal(1, tally.Attempts);
        Assert.Equal(ResearchOrderState.Surveyed, second.State);
        Assert.Equal(1, port.Charges);
        // A later native save of this object is refused too: the debit and the ledger are never saved apart.
        runtime.Save();
        Assert.Equal(SaveEpochResult.NotBound, runtime.LastSave);
        Assert.Equal("payload-before", runtime.Data[SaveStore.StateKey]);
        Assert.Equal(0, runtime.Captures);
    }

    [Fact]
    public void SaveAfterTheBoundaryPublishesTheLedgerMove()
    {
        var runtime = new EpochRuntime();
        var ledger = new ResearchLedger();
        var port = new Port();
        var session = new ResearchChargeSession(ledger, port, () => !runtime.Failed, runtime.RunSaveEpochUnit);
        var order = Surveyed(ledger, port, "shop-a");
        runtime.Payload = () => "state " + order.State;
        session.Boundary();
        runtime.Save();
        Assert.Equal(SaveEpochResult.Published, runtime.LastSave);
        Assert.Equal("state Paid", runtime.Data[SaveStore.StateKey]);
        Assert.False(AccountingStopStore.IsStopped(runtime.Data));
    }

    [Fact]
    public void RuntimeWiringUsesTheGuardedUnitAndTheContractCheck()
    {
        string root = Root();
        string epoch = File.ReadAllText(Path.Combine(root, "Runtime", "SaveEpochRuntime.cs")).Replace("\r\n", "\n");
        int at = epoch.IndexOf("internal void RunSaveEpochUnit(Action unit)", StringComparison.Ordinal);
        Assert.True(at > 0);
        string helper = epoch.Substring(at, epoch.IndexOf("private void Save()", at, StringComparison.Ordinal) - at);
        string[] order = { "saveEpochUnits++;", "try { unit(); }", "finally", "saveEpochUnits--;",
            "if (saveEpochStopPending && saveEpochUnits == 0) FinishDeferredSaveEpochStop();" };
        int next = 0;
        foreach (string step in order) { int found = helper.IndexOf(step, next, StringComparison.Ordinal); Assert.True(found >= next, step); next = found; }
        // Update keeps its own unchanged unit (pinned by SaveEpochTests); Save still reads the same counter.
        Assert.Contains("SaveEpochCallback.Run(saveEpoch, save, save?.modData, saveEpochUnits > 0", epoch);

        string billing = File.ReadAllText(Path.Combine(root, "Runtime", "ResearchBilling.cs")).Replace("\r\n", "\n");
        Assert.Contains("new ResearchChargeSession(ledger, new NativeResearchChargePort(eligible), active, guardedUnit)", billing);
        Assert.Contains("ResearchBillingContract.Check(out runDaily)", billing);
        Assert.Contains("postfix: new HarmonyMethod(typeof(ResearchBilling), nameof(RunDailyPostfix))", billing);
        Assert.Contains("GameManager.ChangeMoneySafe(request.Amount, info, request.Day, address, force: false, showNotification: false)", billing);
        // Entry is marked immediately before the native call.
        int entering = billing.IndexOf("enteringNative();", StringComparison.Ordinal);
        Assert.True(entering > 0 && billing.IndexOf("GameManager.ChangeMoneySafe(", entering, StringComparison.Ordinal) - entering < 40);
        // C5d is not wired: no caller installs or attaches it yet, and research stays unavailable.
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Runtime"), "*.cs"))
            if (!file.EndsWith("ResearchBilling.cs", StringComparison.Ordinal))
                Assert.DoesNotContain("ResearchBilling.", File.ReadAllText(file));
        Assert.False(new MarketResearchSettings().Available);
    }

    // ---- perf ----

    [Fact]
    public void PostfixCostWithZeroOneAndEightOrdersOnAFullQueue()
    {
        var (ledgerIdle, portIdle, _, idle) = Setup();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) if (idle.HasWork) idle.Boundary();
        long idleBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, idleBytes);
        Assert.Equal(0, portIdle.Calls);
        var stopped = new ResearchChargeSession(ledgerIdle, portIdle, () => false, _ => throw new InvalidOperationException());
        Surveyed(ledgerIdle, portIdle, "idle");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) if (stopped.HasWork) stopped.Boundary();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, portIdle.Calls);

        foreach (int orders in new[] { 0, 1, 8 })
        {
            const int runs = 300;
            var samples = new double[runs];
            long bytes = 0;
            for (int run = 0; run < runs + 20; run++)
            {
                var (ledger, port, _, session) = Setup();
                port.Money = 1e6f;
                for (int i = 0; i < 1000; i++)
                    port.Queue.Add(new Record { Type = i % 7 == 0 ? ResearchCharge.MarketingType : "ba:transaction_rent", Amount = -10, Day = 40,
                        Data = new Dictionary<string, string> { { "businessName", "b" + (i % 13) } } });
                for (int i = 0; i < orders; i++) Surveyed(ledger, port, "shop-" + i, ResearchProducts.All[i % 3].Code);
                long start = Stopwatch.GetTimestamp();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                if (session.HasWork) session.Boundary();
                long end = Stopwatch.GetTimestamp();
                if (run < 20) continue;
                bytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
                samples[run - 20] = (end - start) * 1000.0 / Stopwatch.Frequency;
                Assert.Equal(orders, port.Charges);
                Assert.Equal(orders, ledger.Kept.Count);
            }
            Array.Sort(samples);
            output.WriteLine($"orders={orders} queue=1000 p50={samples[runs / 2]:F4}ms p95={samples[runs * 95 / 100]:F4}ms max={samples[runs - 1]:F4}ms bytes/boundary={bytes / runs}");
            Assert.True(samples[runs * 95 / 100] < 5, "p95 under the 5 ms gate");
        }
    }

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }
}
