using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

/// <summary>C5f delivery, report window, activation and cleanup (plan C5 sections 3.6, 3.7 and 3.9). Pure doubles of the
/// native bill; offline only, no native evidence.</summary>
public sealed class ResearchDeliveryTests
{
    private const double Minute = 10 * 60 + 5;
    private const string Agency = "address:Agency%20Street:7:agency";
    private readonly ITestOutputHelper output;
    public ResearchDeliveryTests(ITestOutputHelper output) { this.output = output; }

    // ---- doubles ----

    private static int serial;

    private static MarketResearchReport ReportFor(ResearchOrder order)
    {
        var cells = order.Product.Tier == MarketResearchTier.Basic ? BasicCells() : DetailedCells();
        return new MarketResearchReport("q-" + order.Id + "-" + (++serial), order.Product.Tier, 3,
            order.Product.Coverage == ResearchCoverage.City ? null : order.BusinessAddress, order.RadiusMetres, cells);
    }

    private static MarketResearchCell[] BasicCells()
        => new ResearchFixtureCells(MarketResearchTier.Basic).Cells;

    private static MarketResearchCell[] DetailedCells()
        => new ResearchFixtureCells(MarketResearchTier.Detailed).Cells;

    /// <summary>Real C4 cells from the offline fixture engine, with sentinel values 7001.. so a number is recognisable.</summary>
    private sealed class ResearchFixtureCells
    {
        internal ResearchFixtureCells(MarketResearchTier tier)
        {
            var fixture = new ResearchFixture(64) { Identity = new MarketResearchIdentity("cells-" + (++serial), 3, 7, 11, 13) };
            var source = ResearchFixture.Finish(fixture, tier: tier).FixtureCells;
            Cells = source.Select((c, i) => new MarketResearchCell(c.Metric, c.Band, c.Value.HasValue ? 7001 + i : (int?)null)).ToArray();
        }
        internal MarketResearchCell[] Cells { get; }
    }

    private static ResearchOrder Surveyed(ResearchLedger ledger, string business, string code = ResearchProducts.BasicLocalCode)
    {
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(code, business, Agency, false, 500, Minute, out var order));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(order!.Id, ReportFor(order)));
        return order;
    }

    private static string Shop(int i) => "address:Main%20Street:" + i + ":shop";

    /// <summary>The native ChangeMoneySafe contract: false before any change when money is short, otherwise
    /// Money += amount and one queued record with balance floor(Money).</summary>
    private sealed class Port : IResearchChargePort
    {
        public float Money = 50000;
        public readonly List<(string Tag, float Amount, float Balance, int Day, string Address)> Queue = new List<(string, float, float, int, string)>();
        public int Charges;
        public bool TagStored => true;
        public float Balance => Money;
        public int BilledDay => 40;
        public bool TryResolve(ResearchOrder order, out ResearchChargeTarget? target)
        { target = new ResearchChargeTarget("Name of " + order.BusinessAddress, order.BusinessAddress); return true; }
        public void CollectTagged(List<ResearchChargeRecord> into) { foreach (var r in Queue) into.Add(Row(r)); }
        public object? Tail() => Queue.Count;
        public void CollectTaggedAfter(object? tail, List<ResearchChargeRecord> into) { for (int i = (int)tail!; i < Queue.Count; i++) into.Add(Row(Queue[i])); }
        public bool Charge(in ResearchChargeRequest request, Action enteringNative)
        {
            enteringNative(); Charges++;
            if (Money + request.Amount < 0) return false;
            Money += request.Amount;
            Queue.Add((request.OrderId, request.Amount, (float)Math.Floor(Money), request.Day, (string)request.Target.Address));
            return true;
        }
        private static ResearchChargeRecord Row((string Tag, float Amount, float Balance, int Day, string Address) r)
            => new ResearchChargeRecord(r.Tag, true, false, r.Amount, r.Balance, r.Day, r.Address);
    }

    /// <summary>ResearchBilling.RunDailyPostfix's order: remember the Surveyed orders, bill, then send the notices.</summary>
    private static List<ResearchNotice> Boundary(ResearchChargeSession session, ResearchNoticeTracker tracker)
    {
        var sent = new List<ResearchNotice>();
        tracker.Before(session.Ledger);
        try { session.Boundary(); }
        finally { tracker.After(sent.Add); }
        return sent;
    }

    private static ResearchChargeSession Session(ResearchLedger ledger, Port port) => new ResearchChargeSession(ledger, port, () => true, unit => unit());

    // ---- delivery and decline messages ----

    [Fact]
    public void DeliveryMessageOnlyForPaidAndOncePerOrder()
    {
        var ledger = new ResearchLedger();
        var port = new Port();
        var session = Session(ledger, port);
        var tracker = new ResearchNoticeTracker();
        var first = Surveyed(ledger, Shop(1));
        var second = Surveyed(ledger, Shop(2), ResearchProducts.DetailedLocalCode);

        var sent = Boundary(session, tracker);
        Assert.Equal(new[] { first.Id, second.Id }, sent.Select(n => n.OrderId));
        Assert.All(sent, n => Assert.Equal(ResearchNoticeKind.Delivered, n.Kind));
        Assert.All(sent, n => Assert.Equal(ResearchNoticeText.Delivered, n.MessageKey));
        Assert.All(sent, n => Assert.Equal(Agency, n.AgencyAddress));
        Assert.Equal(first.BusinessAddress, sent[0].BusinessAddress);

        // Repeated boundaries, with and without new work, never repeat a message.
        for (int i = 0; i < 5; i++) Assert.Empty(Boundary(session, tracker));
        var third = Surveyed(ledger, Shop(3));
        Assert.Equal(new[] { third.Id }, Boundary(session, tracker).Select(n => n.OrderId));
        Assert.Empty(Boundary(session, tracker));
        Assert.Equal(3, port.Charges);

        // A reloaded ledger (the save copy) with these Paid orders sends nothing at the next bill.
        var reloaded = ledger.CopyForSave();
        Assert.Empty(Boundary(Session(reloaded, port), new ResearchNoticeTracker()));
        Assert.Equal(3, port.Charges);
    }

    [Fact]
    public void DeclineMessageOnlyForDeclinedWithEitherReasonAndOncePerOrder()
    {
        var ledger = new ResearchLedger();
        var port = new Port { Money = 100 };
        var session = Session(ledger, port);
        var tracker = new ResearchNoticeTracker();
        var poor = Surveyed(ledger, Shop(1));

        var sent = Boundary(session, tracker);
        Assert.Equal(ResearchOrderState.Declined, poor.State);
        Assert.Equal(ResearchDeclineReason.InsufficientFunds, poor.DeclineReason);
        var notice = Assert.Single(sent);
        Assert.Equal(ResearchNoticeKind.Declined, notice.Kind);
        Assert.Equal(ResearchNoticeText.Declined, notice.MessageKey);
        Assert.Empty(Boundary(session, tracker));

        // BalancePrecision: $100,000,000 has a binary32 spacing of $8, so a $500 debit is refused before any native call.
        port.Money = 100_000_000f;
        int charges = port.Charges;
        var precise = Surveyed(ledger, Shop(2));
        notice = Assert.Single(Boundary(session, tracker));
        Assert.Equal(ResearchDeclineReason.BalancePrecision, precise.DeclineReason);
        Assert.Equal(ResearchNoticeKind.Declined, notice.Kind);
        Assert.Equal(precise.Id, notice.OrderId);
        Assert.Equal(charges, port.Charges);
        Assert.Empty(Boundary(session, tracker));
    }

    [Fact]
    public void CancelledUnknownOpenAndReplacedOrdersSendNothing()
    {
        var ledger = new ResearchLedger();
        var tracker = new ResearchNoticeTracker();
        var sent = new List<ResearchNotice>();
        var cancelled = Surveyed(ledger, Shop(1));
        var unknown = Surveyed(ledger, Shop(2));
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(ResearchProducts.BasicLocalCode, Shop(3), Agency, false, 500, Minute, out var ordered));
        var waiting = Surveyed(ledger, Shop(4));
        tracker.Before(ledger);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(cancelled.Id));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(unknown.Id));
        tracker.After(sent.Add);
        Assert.Empty(sent);
        Assert.Equal(ResearchOrderState.Ordered, ordered!.State);
        Assert.Equal(ResearchOrderState.Surveyed, waiting.State);

        // An order that became Paid outside a remembered boundary (for example restored) sends nothing either.
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(waiting.Id));
        tracker.After(sent.Add);
        Assert.Empty(sent);

        // A boundary that ended with the runtime stopped forgets its orders.
        var late = Surveyed(ledger, Shop(5));
        tracker.Before(ledger);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(late.Id));
        tracker.Forget();
        tracker.After(sent.Add);
        Assert.Empty(sent);
    }

    [Fact]
    public void BillingSendsNoticesAfterTheBoundaryOnlyWhileActive()
    {
        string billing = Source("Runtime/ResearchBilling.cs");
        string postfix = Between(billing, "private static void RunDailyPostfix()", "/// <summary>The native members research billing uses");
        int before = postfix.IndexOf("notices.Before(current.Ledger);", StringComparison.Ordinal);
        int boundary = postfix.IndexOf("current.Boundary();", StringComparison.Ordinal);
        int after = postfix.IndexOf("if (sessionActive?.Invoke() == true) notices.After(ResearchMessages.Send); else notices.Forget();", StringComparison.Ordinal);
        Assert.True(before > 0 && boundary > before && after > boundary);
        Assert.True(postfix.LastIndexOf("finally", after, StringComparison.Ordinal) > boundary);
        // Idle cost unchanged: nothing is remembered before the HasWork check.
        Assert.True(postfix.IndexOf("if (current == null || !current.HasWork) return;", StringComparison.Ordinal) < before);
        // Messages never change the ledger, money or campaigns, and never create a contact.
        string messages = Between(Source("Runtime/ResearchDiagnostics.cs"), "internal static class ResearchMessages", "public sealed partial class PopulationRuntime");
        foreach (string forbidden in new[] { "TryMarkPaid", "TryDecline", "ChangeMoney", "marketingCampaigns", "AddContact", "GetOrAddBusinessContact", "Contacts.Add", "GetContact(" })
            Assert.DoesNotContain(forbidden, messages);
        Assert.Contains("contact.SendMessage(new TextMessage(notice.MessageKey,", messages);
    }

    [Fact]
    public void MessagesAreTheApprovedEnglishTextWithTheNativeBusinessNameArgument()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Runtime", "Localization", "en.json")));
        var table = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        Assert.Equal("Your market research for {businessName} is ready. Open Persistent City (Ctrl+F8) to read it.", table[ResearchNoticeText.Delivered]);
        Assert.Equal("We could not bill the market research for {businessName}, so it was not delivered.", table[ResearchNoticeText.Declined]);
        Assert.Equal(new[] { ResearchNoticeText.Delivered, ResearchNoticeText.Declined }, ResearchNoticeText.Keys());
        Assert.Equal("businessName", ResearchNoticeText.BusinessNameArgument);
    }

    [Theory]
    [InlineData("Agency Street", 7)]
    [InlineData("ba:street_fourthavenue", 120)]
    [InlineData("Straße 9/10 % #", 1)]
    public void AgencyAddressReadsBackTheDialogsAgencyId(string street, int number)
    {
        // C5e ResearchAgencyDialog.AgencyId.
        string id = "address:" + Uri.EscapeDataString(street) + ":" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":agency";
        Assert.True(ResearchNoticeText.TryParseAgency(id, out var parsed, out int parsedNumber));
        Assert.Equal(street, parsed); Assert.Equal(number, parsedNumber);
        foreach (string bad in new[] { "", "address::7:agency", "address:Street:x:agency", "address:Street:7:shop", "Street:7:agency", "address:Street:-7:agency" })
            Assert.False(ResearchNoticeText.TryParseAgency(bad, out _, out _), bad);
        Assert.False(ResearchNoticeText.TryParseAgency(null, out _, out _));
    }

    // ---- window and console: numbers only for Paid ----

    [Fact]
    public void WindowAndConsoleShowNumbersOnlyForPaid()
    {
        var ledger = new ResearchLedger();
        var paid = Surveyed(ledger, Shop(1), ResearchProducts.DetailedLocalCode);
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(paid.Id));
        var surveyed = Surveyed(ledger, Shop(2));
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(ResearchProducts.BasicLocalCode, Shop(3), Agency, false, 500, Minute, out var ordered));
        var declined = Surveyed(ledger, Shop(4));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(declined.Id, ResearchDeclineReason.InsufficientFunds));
        var precision = Surveyed(ledger, Shop(5));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(precision.Id, ResearchDeclineReason.BalancePrecision));
        var cancelled = Surveyed(ledger, Shop(6));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(cancelled.Id));
        var unknown = Surveyed(ledger, Shop(7));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(unknown.Id));

        string Name(string place) => "Shop " + place.Split(':')[2];
        string paidText = ResearchReportText.Report(ledger, paid, Name(paid.BusinessAddress));
        Assert.Contains("Paid and delivered", paidText);
        Assert.Contains("7,001", paidText);
        Assert.Contains(paid.Report!.Evidence, paidText);
        Assert.Contains("500 m around Shop 1", paidText);
        foreach (var order in new[] { surveyed, ordered!, declined, precision, cancelled, unknown })
        {
            string text = ResearchReportText.Report(ledger, order, Name(order.BusinessAddress));
            Assert.Contains("No report numbers: only a paid order has a report.", text);
            Assert.DoesNotMatch(@"\b70\d\d\b|7,0\d\d", text);
            Assert.DoesNotContain("Modelled demand estimate", text);
        }
        Assert.Contains("not enough money", ResearchReportText.State(declined));
        Assert.Contains("could not be charged exactly", ResearchReportText.State(precision));

        // The surveyed order holds a sealed report, but TryRead refuses it, so its numbers never show.
        Assert.NotNull(surveyed.Report);
        Assert.False(surveyed.Report!.TryRead(ledger, surveyed.Id, out _));

        // Replaced: the 17th delivery releases the oldest report; its text then has no numbers.
        for (int i = 0; i < ResearchLedger.MaximumKeptReports; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Surveyed(ledger, Shop(100 + i)).Id));
        Assert.Equal(ResearchOrderState.Replaced, paid.State);
        Assert.Contains("No report numbers", ResearchReportText.Report(ledger, paid, "Shop 1"));

        // Order lines (pcresearch orders and the window list) never carry numbers.
        foreach (var order in ResearchReportText.Orders(ledger))
            Assert.DoesNotMatch(@"\b70\d\d\b|7,0\d\d", ResearchReportText.Line(order, Name(order.BusinessAddress)));

        // A Paid order whose report does not belong to another ledger cannot be read through it.
        var stranger = new ResearchLedger();
        var strangerPaid = Surveyed(stranger, Shop(1));
        Assert.Equal(ResearchMoveRefusal.None, stranger.TryMarkPaid(strangerPaid.Id));
        Assert.Contains("the report cannot be read", ResearchReportText.Report(new ResearchLedger(), strangerPaid, "Shop 1"));
    }

    [Fact]
    public void ConsoleReportUsesTheSameGuardedText()
    {
        string diagnostics = Source("Runtime/ResearchDiagnostics.cs");
        Assert.Contains("CommandHelper.AddCommand<string>(\"pcresearch\"", diagnostics);
        Assert.Contains("CommandHelper.AddCommand<string, string>(\"pcresearch\"", diagnostics);
        Assert.Contains("ResearchReportText.Report(ledger, order, ResearchBusinessName(order.BusinessAddress))", diagnostics);
        foreach (string action in new[] { "\"status\"", "\"orders\"", "\"report\"", "\"window\"", "\"open\"", "\"close\"" })
            Assert.Contains(action, diagnostics);
        // TryRead is the only number exit, and only the pure text builder calls it.
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "Runtime"), "*.cs"))
            if (!file.EndsWith("ResearchReportText.cs", StringComparison.Ordinal))
                Assert.DoesNotContain(".TryRead(", File.ReadAllText(file));
        // The census file keeps its pc command only.
        Assert.DoesNotContain("pcresearch", Source("Runtime/PopulationCensusDiagnostics.cs"));
    }

    [Fact]
    public void CellLabelsAreDistinctEnglishTextFromTheFixedCodes()
    {
        foreach (var tier in new[] { MarketResearchTier.Basic, MarketResearchTier.Detailed, MarketResearchTier.InDepth })
        {
            var cells = new ResearchFixtureCells(tier).Cells;
            var labels = cells.Select(c => ResearchCellLabels.Label(c.Metric, c.Band)).ToArray();
            Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
            foreach (var (cell, label) in cells.Zip(labels))
            {
                Assert.NotEqual(cell.Label, label);
                Assert.DoesNotMatch(@"[a-z][A-Z]", label); // no enum identifier such as CroissantTaste
                Assert.DoesNotContain("Metric ", label);
                Assert.DoesNotContain("band ", label);
            }
        }
        Assert.Equal("Prepared-meal budget: below $10", ResearchCellLabels.Label(MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Low));
        Assert.Equal("Croissant taste: somewhat high", ResearchCellLabels.Label(MarketResearchMetric.CroissantTaste, MarketResearchBand.SomewhatHigh));
        Assert.Equal("Consumers", ResearchCellLabels.Label(MarketResearchMetric.Consumers, MarketResearchBand.All));
        Assert.Equal("unknown", ResearchCellLabels.Value(null));
        Assert.Equal("0", ResearchCellLabels.Value(0));
        foreach (MarketResearchMetric metric in Enum.GetValues(typeof(MarketResearchMetric)))
            Assert.DoesNotContain("Metric ", ResearchCellLabels.Metric(metric));
        foreach (MarketResearchBand band in Enum.GetValues(typeof(MarketResearchBand)))
            Assert.DoesNotContain("band ", ResearchCellLabels.Band(MarketResearchMetric.Consumers, band));
    }

    // ---- window: nothing built while closed ----

    [Fact]
    public void ClosedWindowBuildsNothing()
    {
        var model = new ResearchWindowModel();
        var ledger = new ResearchLedger();
        var paid = Surveyed(ledger, Shop(1));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(paid.Id));
        int names = 0;
        string Name(string place) { names++; return "Shop"; }
        for (int i = 0; i < 1000; i++) Assert.Null(model.Update(i, ledger, Name));
        Assert.Equal(0, model.Builds); Assert.Equal(0, names); Assert.Null(model.View);

        model.Show();
        var view = model.Update(10, ledger, Name);
        Assert.NotNull(view);
        Assert.Equal(1, model.Builds);
        Assert.Equal(new[] { paid.Id }, view!.Ids);
        Assert.Contains("7,0", view.Detail); // the newest Paid report is selected by default
        // Cached within the refresh interval; rebuilt after it and on a new selection.
        Assert.Same(view, model.Update(10.5, ledger, Name));
        Assert.Equal(1, model.Builds);
        Assert.NotSame(view, model.Update(11.01, ledger, Name));
        Assert.Equal(2, model.Builds);
        model.Select(paid.Id);
        model.Update(11.02, ledger, Name);
        Assert.Equal(3, model.Builds);

        // Closing drops the view; closed again, nothing is read.
        model.Hide();
        Assert.Null(model.View);
        names = 0;
        Assert.Null(model.Update(20, null, _ => throw new InvalidOperationException("closed window read a name")));
        Assert.Equal(3, model.Builds); Assert.Equal(0, names);
        model.Toggle(); Assert.True(model.Open); model.Toggle(); Assert.False(model.Open);

        // Open without an attached city: a message, no numbers.
        model.Show();
        Assert.Equal("Market research is not attached to this city.", model.Update(30, null, Name)!.Detail);

        // The live draw is skipped before any read while closed, and the panel adds exactly one button line.
        string panel = Source("Runtime/FoodEconomyPanel.cs");
        Assert.Contains("if (researchWindow.Open) researchWindow.Draw(ResearchLedgerNow, researchNames ??= ResearchBusinessName);", panel);
        Assert.Single(Regex.Matches(panel, "\"Research reports\""));
        Assert.Contains("if (GUILayout.Button(\"Research reports\", GUILayout.Width(140))) researchWindow.Toggle();", panel);
        string window = Source("Runtime/ResearchReportWindow.cs");
        string draw = Between(window, "internal void Draw(", "private void DrawContents(int id)");
        Assert.StartsWith("ResearchLedger? ledger, Func<string, string> businessName)\n        {\n            if (!model.Open) return;", draw.Replace("\r\n", "\n"));
    }

    // ---- activation ----

    [Fact]
    public void AvailableIsFalseWhenAnyAttachOrContractCheckFails()
    {
        var names = new[] { "Persistent City is not attached", "agency dialog contract failed", "billing contract failed",
            "delivery contract failed", "research ledger not attached" };
        for (int mask = 0; mask < 32; mask++)
        {
            bool Bit(int i) => (mask & (1 << i)) != 0;
            var activation = new MarketResearchActivation(Bit(0), Bit(1), Bit(2), Bit(3), Bit(4));
            Assert.Equal(mask == 31, activation.Available);
            int firstFailed = Enumerable.Range(0, 5).FirstOrDefault(i => !Bit(i), -1);
            Assert.Equal(firstFailed < 0 ? "" : names[firstFailed], activation.Refusal);
            var settings = new MarketResearchSettings();
            settings.DeriveAvailability(() => activation);
            Assert.Equal(mask == 31, settings.Available);
        }
        Assert.False(default(MarketResearchActivation).Available);

        // Unbound, cleared and config-supplied settings are never available; the checks are read on every call.
        var live = new MarketResearchSettings();
        Assert.False(live.Available);
        bool attached = true;
        live.DeriveAvailability(() => new MarketResearchActivation(attached, true, true, true, true));
        Assert.True(live.Available);
        attached = false;
        Assert.False(live.Available);
        Assert.Equal("Persistent City is not attached", live.Activation.Refusal);
        attached = true; live.ClearAvailability();
        Assert.False(live.Available);
        Assert.False(typeof(MarketResearchSettings).GetProperty("Available")!.CanWrite);
        Assert.False(typeof(MarketResearchSettings).GetProperty("Activation")!.CanWrite);
        Assert.Throws<ArgumentNullException>(() => live.DeriveAvailability(null!));

        // The runtime derives Available from every check and clears it on detach.
        string mod = Source("Runtime/PersistentCityMod.cs");
        Assert.Contains("settings.MarketResearch.DeriveAvailability(ResearchActivation);", mod);
        Assert.Contains("settings.MarketResearch.ClearAvailability();", mod);
        string derive = Between(mod, "private MarketResearchActivation ResearchActivation()", "private bool ResearchAttached()");
        foreach (string check in new[] { "ResearchAttached()", "ResearchAgencyDialog.Installed && ResearchAgencyDialog.Status.Length == 0",
            "ResearchBilling.Installed && ResearchBilling.Status.Length == 0 && ResearchBilling.Attached", "ResearchMessages.ContractPassed", "simulation?.Research != null" })
            Assert.Contains(check, derive);
        Assert.Contains("() => settings.MarketResearch.Available", mod);
    }

    // ---- cleanup ----

    [Fact]
    public void C3PlaceholdersAreRemovedAndTheBindingDiagnosticStays()
    {
        string root = Root();
        foreach (string removed in new[] { "Core/AgencyOrders.cs", "Runtime/AgencyPaymentPort.cs", "tests/AgencyOrdersTests.cs" })
            Assert.False(File.Exists(Path.Combine(root, removed)), removed);
        Assert.False(Directory.Exists(Path.Combine(root, "tools", "agency-payment-regression")));
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "AgencyBindingDiagnostic.cs")));
        foreach (var folder in new[] { "Core", "Runtime", "Persistence", "Compatibility", "tests", "tools" })
            foreach (var file in Directory.GetFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".csproj", StringComparison.Ordinal) || f.EndsWith(".py", StringComparison.Ordinal))
                .Where(f => !f.EndsWith("ResearchDeliveryTests.cs", StringComparison.Ordinal) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
            {
                string text = File.ReadAllText(file);
                foreach (string name in new[] { "AgencyOrderBook", "IAgencyPaymentPort", "AgencyPaymentPort", "agency-payment-regression" })
                    Assert.False(text.Contains(name, StringComparison.Ordinal), file + " still names " + name);
            }
    }

    // ---- perf ----

    [Fact]
    public void WindowViewBuildCostAtTheLedgerBoundsAndZeroWorkClosed()
    {
        // The largest ledger: 8 open, 16 delivered Detailed reports (62 cells) and 16 closed orders.
        var ledger = new ResearchLedger();
        for (int i = 0; i < ResearchLedger.MaximumKeptReports; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Surveyed(ledger, Shop(i), ResearchProducts.DetailedCityCode).Id));
        for (int i = 0; i < ResearchLedger.MaximumClosedOrders; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(Surveyed(ledger, Shop(100 + i)).Id, ResearchDeclineReason.InsufficientFunds));
        for (int i = 0; i < ResearchLedger.MaximumOpenOrders; i++) Surveyed(ledger, Shop(200 + i));
        Assert.Equal(40, ResearchReportText.Orders(ledger).Count());
        Func<string, string> Name = place => "Business " + place;

        var model = new ResearchWindowModel();
        double now = 0;
        Measure("window closed, Update", 20000, () => model.Update(now += 0.016, ledger, Name));
        Assert.Equal(0, model.Builds);
        model.Show();
        Measure("window open, view rebuild (40 orders, 62-cell report)", 2000, () => { model.Select(""); model.Update(now += 0.016, ledger, Name); });
        Measure("window open, cached frame", 20000, () => model.Update(now += 0.0001, ledger, Name));
        var paid = ledger.Kept[ledger.Kept.Count - 1];
        Measure("pcresearch report, 62 cells", 2000, () => ResearchReportText.Report(ledger, paid, "Business"));
        var tracker = new ResearchNoticeTracker();
        Measure("notice tracker, 8 Surveyed orders", 20000, () => { tracker.Before(ledger); tracker.After(_ => { }); });
    }

    private void Measure(string label, int runs, Action call)
    {
        for (int i = 0; i < 200; i++) call();
        var samples = new double[runs];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < runs; i++)
        {
            long start = Stopwatch.GetTimestamp();
            call();
            samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Array.Sort(samples);
        output.WriteLine($"{label}: p50={samples[runs / 2]:F4}ms p95={samples[runs * 95 / 100]:F4}ms max={samples[runs - 1]:F4}ms bytes/call={bytes / runs}");
        Assert.True(samples[runs * 95 / 100] < 5, label + " p95 under the 5 ms gate");
    }

    // ---- helpers ----

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        int to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text.Substring(from + start.Length, to - from - start.Length);
    }
}
