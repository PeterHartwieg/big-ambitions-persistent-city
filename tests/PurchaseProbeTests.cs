using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>S2 sale proof, Core side: the ceiling refuses before any native call, a refused or interrupted probe changes
/// nothing, and a reload taken mid-probe leaves no half sale.</summary>
public sealed class PurchaseProbeTests
{
    private const string Resident = "resident-0001", Place = "address:Main%20Street:12:shop";
    private static readonly ProbeLine Fee = new ProbeLine("ba:itemname_entrancefee", Money.FromCents(500), true);
    private static readonly ProbeLine Shirt = new ProbeLine("ba:itemname_shirt", Money.FromCents(300), false);
    private static readonly ProbeLine Socks = new ProbeLine("ba:itemname_socks", Money.FromCents(200), false);

    private static ResidentBudget Wallet(long cents) => new ResidentBudget { OpeningBalance = Money.FromCents(cents), Balance = Money.FromCents(cents) };
    private static PurchaseProbeRequest Request(long ceilingCents, ProbeKind kind = ProbeKind.Goods, long minute = 600, params ProbeLine[] basket)
        => new PurchaseProbeRequest(Resident, Place, kind, minute, Money.FromCents(ceilingCents), basket.Length == 0 ? new[] { Fee, Shirt, Socks } : basket);

    /// <summary>Native double: stock and booked orders, changed only when a sale goes through.</summary>
    private sealed class Native : IProbeNativeSale
    {
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>(StringComparer.Ordinal) { ["ba:itemname_shirt"] = 5, ["ba:itemname_socks"] = 5 };
        public readonly List<string> Orders = new List<string>();
        public int Calls;
        public Func<IReadOnlyList<ProbeLine>, ProbeNativeOutcome?>? Before;
        public Func<ProbeLine, bool> Accept = _ => true;
        public Func<IReadOnlyList<ProbeLine>, IReadOnlyList<ProbeLine>>? Report;
        public string Books => string.Join(",", Stock.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) + "|" + string.Join(",", Orders);
        public ProbeNativeOutcome Sell(IReadOnlyList<ProbeLine> lines)
        {
            Calls++;
            var early = Before?.Invoke(lines);
            if (early.HasValue) return early.Value;
            var paid = lines.Where(Accept).ToList();
            foreach (var line in paid) if (Stock.ContainsKey(line.ItemId)) Stock[line.ItemId]--;
            Orders.Add(string.Join("+", lines.Select(l => l.ItemId)));
            return ProbeNativeOutcome.Processed(Report?.Invoke(paid) ?? paid);
        }
    }

    private static string WalletText(ResidentBudget w)
        => string.Join("|", w.Balance, w.PreparedFoodSpent, w.Ledger.Discretionary, w.HeldPurchase);

    [Fact]
    public void ACeilingBelowTheMandatoryBasketRefusesBeforeAnyNativeCall()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native();
        string walletBefore = WalletText(wallet), books = native.Books;

        var result = PurchaseProbe.Run(Request(499), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.Ceiling, result.Refusal);
        Assert.Equal(Money.FromCents(500), result.Quote!.Total); // the fee alone, never trimmed
        Assert.Equal(0, native.Calls);
        Assert.Empty(receipts);
        Assert.Equal(walletBefore, WalletText(wallet));
        Assert.Equal(books, native.Books);
    }

    [Fact]
    public void TheWalletLimitIsPartOfTheCeiling()
    {
        var wallet = Wallet(400); var receipts = new Dictionary<string, string>(); var native = new Native();

        var result = PurchaseProbe.Run(Request(100000), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.Ceiling, result.Refusal);
        Assert.Equal(Money.FromCents(400), result.Quote!.Ceiling);
        Assert.Equal(0, native.Calls);
        Assert.Empty(receipts);
    }

    [Fact]
    public void OptionalGoodsAreTrimmedFromTheEndAndTheNativeSideSeesOnlyTheQuote()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native();
        IReadOnlyList<ProbeLine>? seen = null;
        native.Before = lines => { seen = lines; return null; };

        var result = PurchaseProbe.Run(Request(850), wallet, receipts, native);

        Assert.True(result.Bought);
        Assert.Equal(new[] { Fee.ItemId, Shirt.ItemId }, seen!.Select(l => l.ItemId));
        Assert.Equal(1, result.Quote!.Dropped);
        Assert.True(result.Paid <= Money.FromCents(850));
    }

    [Fact]
    public void AnInterruptedProbeLeavesWalletReceiptsAndNativeBooksUnchanged()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string> { ["other"] = "kept" }; var native = new Native();
        string walletBefore = WalletText(wallet), books = native.Books;
        // The pending receipt exists while the native side runs, and is gone again after an interruption.
        native.Before = _ => { Assert.Equal(ProbeReceiptStatus.Pending, PurchaseProbe.Status(receipts[Request(100000).Key])); return ProbeNativeOutcome.Interrupted("hour changed during setup"); };

        var result = PurchaseProbe.Run(Request(100000), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.Interrupted, result.Refusal);
        Assert.Equal(new[] { "other" }, receipts.Keys);
        Assert.Equal(walletBefore, WalletText(wallet));
        Assert.Equal(books, native.Books);
        Assert.True(HouseholdFinance.CashConserved(wallet));
    }

    [Fact]
    public void AnUncertainNativeOutcomeNeverDebitsAndIsNeverRetried()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native();
        string walletBefore = WalletText(wallet);
        native.Before = _ => throw new InvalidOperationException("Native order booking is uncertain");

        var first = PurchaseProbe.Run(Request(100000), wallet, receipts, native);
        native.Before = null;
        var repeat = PurchaseProbe.Run(Request(100000), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.Uncertain, first.Refusal);
        Assert.Equal(ProbeReceiptStatus.Uncertain, PurchaseProbe.Status(receipts[first.Key]));
        Assert.Equal(ProbeRefusal.AlreadyRecorded, repeat.Refusal);
        Assert.Equal(1, native.Calls);
        Assert.Equal(walletBefore, WalletText(wallet));
    }

    [Fact]
    public void PaidLinesOutsideTheQuoteAreUncertainAndNotDebited()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native();
        string walletBefore = WalletText(wallet);
        native.Report = paid => paid.Select(l => new ProbeLine(l.ItemId, l.Price + Money.FromCents(1), false)).ToList();

        var result = PurchaseProbe.Run(Request(100000), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.Uncertain, result.Refusal);
        Assert.Equal(walletBefore, WalletText(wallet));
    }

    [Fact]
    public void ANativePriceRefusalOfEveryLineBooksNoMoney()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native { Accept = _ => false };
        string walletBefore = WalletText(wallet);

        var result = PurchaseProbe.Run(Request(100000), wallet, receipts, native);

        Assert.Equal(ProbeRefusal.NativeDeclined, result.Refusal);
        Assert.Equal(ProbeReceiptStatus.Declined, result.Receipt);
        Assert.Equal(walletBefore, WalletText(wallet));
    }

    [Theory]
    [InlineData(ProbeKind.Goods)]
    [InlineData(ProbeKind.Service)]
    [InlineData(ProbeKind.Food)]
    public void ASaleDebitsThePaidTotalOnceAndConservesCash(ProbeKind kind)
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native { Accept = l => l.ItemId != Socks.ItemId };

        var result = PurchaseProbe.Run(Request(100000, kind), wallet, receipts, native);
        var repeat = PurchaseProbe.Run(Request(100000, kind), wallet, receipts, native);

        // The game's per-item check refused the socks: only the paid lines are debited.
        Assert.Equal(Money.FromCents(800), result.Paid);
        Assert.Equal(Money.FromCents(100000 - 800), wallet.Balance);
        Assert.Equal(kind == ProbeKind.Food ? Money.FromCents(800) : Money.Zero, wallet.PreparedFoodSpent);
        Assert.Equal(kind == ProbeKind.Food ? Money.Zero : Money.FromCents(800), wallet.Ledger.Discretionary);
        Assert.Equal(Money.Zero, wallet.HeldPurchase);
        Assert.True(HouseholdFinance.CashConserved(wallet));
        Assert.Equal(ProbeReceiptStatus.Paid, PurchaseProbe.Status(receipts[result.Key]));
        Assert.Equal(ProbeRefusal.AlreadyRecorded, repeat.Refusal);
        Assert.Equal(1, native.Calls);
    }

    /// <summary>A save is the receipts in native mod data, the wallet in the population and the native books, taken
    /// together. The only point inside a probe where anything is written is the pending receipt before the native call;
    /// a "save" there reloads with no native effect and no debit, and recovery abandons the receipt for good.</summary>
    [Fact]
    public void AReloadMidProbeLeavesNoHalfSale()
    {
        var wallet = Wallet(100000); var receipts = new Dictionary<string, string>(); var native = new Native();
        string walletBefore = WalletText(wallet), booksBefore = native.Books;
        Dictionary<string, string>? savedReceipts = null; string? savedWallet = null, savedBooks = null;
        native.Before = _ => { savedReceipts = new Dictionary<string, string>(receipts); savedWallet = WalletText(wallet); savedBooks = native.Books; return null; };

        var live = PurchaseProbe.Run(Request(100000), wallet, receipts, native);
        Assert.True(live.Bought); // the session that kept running finished the sale

        // Reload the mid-probe image.
        Assert.Equal(walletBefore, savedWallet);
        Assert.Equal(booksBefore, savedBooks);
        Assert.Equal(ProbeReceiptStatus.Pending, PurchaseProbe.Status(savedReceipts![live.Key]));
        Assert.Equal(1, PurchaseProbe.Recover(savedReceipts));
        Assert.Equal(ProbeReceiptStatus.Abandoned, PurchaseProbe.Status(savedReceipts[live.Key]));
        Assert.Equal(0, PurchaseProbe.Recover(savedReceipts));

        // The reloaded city never completes the abandoned probe; a new probe at a later minute is a separate sale.
        var reloadedWallet = Wallet(100000); var reloadedNative = new Native();
        var again = PurchaseProbe.Run(Request(100000), reloadedWallet, savedReceipts, reloadedNative);
        Assert.Equal(ProbeRefusal.AlreadyRecorded, again.Refusal);
        Assert.Equal(0, reloadedNative.Calls);
        Assert.Equal(walletBefore, WalletText(reloadedWallet));
        var later = PurchaseProbe.Run(Request(100000, minute: 601), reloadedWallet, savedReceipts, reloadedNative);
        Assert.True(later.Bought);
        Assert.Equal(ProbeReceiptStatus.Abandoned, PurchaseProbe.Status(savedReceipts[live.Key]));
        Assert.Single(reloadedNative.Orders);
    }

    [Fact]
    public void RecoveryTouchesOnlyPendingProbeReceipts()
    {
        var receipts = new Dictionary<string, string>
        {
            [PurchaseProbe.Prefix + "a"] = "paid|800|Goods|x",
            [PurchaseProbe.Prefix + "b"] = "pending|Goods|y",
            ["persistentcity:food-sale-v1:m"] = "pending|100|z",
        };

        Assert.Equal(1, PurchaseProbe.Recover(receipts));
        Assert.Equal("paid|800|Goods|x", receipts[PurchaseProbe.Prefix + "a"]);
        Assert.Equal("abandoned|Goods|y", receipts[PurchaseProbe.Prefix + "b"]);
        Assert.Equal("pending|100|z", receipts["persistentcity:food-sale-v1:m"]);
    }
}
