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

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(ResearchMenuTests), DisableParallelization = true)]
public sealed class ResearchMenuSerialCollection { }

/// <summary>C5e: the agency research menu, the campaign-screen gate and the localization table (plan C5 section 3.1).
/// Pure: Runtime/ResearchAgencyMenu.cs is linked into this project; the native dialog is pinned by source.</summary>
[Collection(nameof(ResearchMenuTests))]
public sealed class ResearchMenuTests
{
    private readonly ITestOutputHelper output;
    public ResearchMenuTests(ITestOutputHelper output) { this.output = output; }

    private static IReadOnlyList<ResearchProduct> Products(int count)
    {
        var tiers = new[] { MarketResearchTier.Basic, MarketResearchTier.Detailed, MarketResearchTier.InDepth };
        return Enumerable.Range(0, count).Select(i => new ResearchProduct("test-" + i, tiers[i % 3],
            i % 2 == 0 ? ResearchCoverage.Catchment : ResearchCoverage.City, 500 * (i + 1) + 12)).ToArray();
    }

    // ---- menu tree ----

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void MenuTreeOffersEveryProductWithTwoChoicesPerEntry(int count)
    {
        var products = Products(count);
        var menu = ResearchMenu.Build(products);
        Assert.Equal(2 * count, menu.Nodes.Count);
        for (int i = 0; i < count; i++)
        {
            var offer = menu.Nodes[i];
            Assert.Equal(ResearchMenuNodeKind.Product, offer.Kind);
            Assert.Same(products[i], offer.Product);
            Assert.Equal(ResearchText.ProductOffer, offer.MessageKey);
            Assert.Equal(new ResearchMenuChoice(ResearchMenuTarget.Node, count + i, ResearchText.Choose), offer.Confirm);
            Assert.Equal(i + 1 < count ? new ResearchMenuChoice(ResearchMenuTarget.Node, i + 1, ResearchText.OtherProduct)
                : new ResearchMenuChoice(ResearchMenuTarget.CampaignScreen, -1, ResearchText.BackToCampaigns), offer.Second);

            var confirm = menu.Nodes[count + i];
            Assert.Equal(ResearchMenuNodeKind.Confirmation, confirm.Kind);
            Assert.Same(products[i], confirm.Product);
            Assert.Equal(ResearchText.Confirmation, confirm.MessageKey);
            Assert.Equal(new ResearchMenuChoice(ResearchMenuTarget.PlaceOrder, -1, ResearchText.PlaceOrder), confirm.Confirm);
            Assert.Equal(new ResearchMenuChoice(ResearchMenuTarget.Node, i, ResearchText.NativeBack), confirm.Second);
        }

        // Walking the tree from the root reaches every product's confirmation once; only confirmations place orders.
        var ordered = new List<ResearchProduct>();
        var seen = new HashSet<int>();
        var pending = new Stack<int>(new[] { ResearchMenu.Root });
        while (pending.Count > 0)
        {
            int at = pending.Pop();
            if (!seen.Add(at)) continue;
            var node = menu.Nodes[at];
            foreach (var choice in new[] { node.Confirm, node.Second })
            {
                Assert.False(string.IsNullOrEmpty(choice.TextKey));
                if (choice.Target == ResearchMenuTarget.Node) { Assert.InRange(choice.Node, 0, menu.Nodes.Count - 1); pending.Push(choice.Node); }
                else Assert.Equal(-1, choice.Node);
                if (choice.Target == ResearchMenuTarget.PlaceOrder) { Assert.Equal(ResearchMenuNodeKind.Confirmation, node.Kind); ordered.Add(node.Product); }
            }
        }
        Assert.Equal(2 * count, seen.Count);
        Assert.Equal(products.OrderBy(p => p.Code, StringComparer.Ordinal), ordered.OrderBy(p => p.Code, StringComparer.Ordinal));
        // Exactly one way out to the native campaign screen: the last product's second option.
        Assert.Single(menu.Nodes, n => n.Second.Target == ResearchMenuTarget.CampaignScreen || n.Confirm.Target == ResearchMenuTarget.CampaignScreen);
    }

    [Fact]
    public void ConfirmationStatesProductBusinessExactPriceTonightsBillAndTomorrowsReport()
    {
        var table = Table();
        var menu = ResearchMenu.Build(ResearchProducts.All);
        Assert.Equal(new[] { "basic-local", "detailed-local", "detailed-city" }, menu.Nodes.Take(3).Select(n => n.Product.Code));
        Assert.Equal(new[] { "$500", "$2,000", "$6,000" }, ResearchProducts.All.Select(ResearchText.Price));
        foreach (var product in Products(6).Concat(ResearchProducts.All))
            Assert.Equal(product.PriceDollars, int.Parse(ResearchText.Price(product).TrimStart('$').Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture));

        string confirmation = table[ResearchText.Confirmation];
        foreach (string placeholder in new[] { "{product}", "{business}", "{price}" }) Assert.Contains(placeholder, confirmation);
        Assert.Contains("billed with tonight's marketing bill", confirmation);
        Assert.Contains("report arrives tomorrow morning", confirmation);

        var node = menu.Nodes[ResearchProducts.All.Count + 1];
        var arguments = node.Arguments("Ada's Diner");
        Assert.Equal(ResearchText.ProductName("detailed-local"), arguments[ResearchText.ProductArgument]);
        Assert.Equal("Ada's Diner", arguments[ResearchText.BusinessArgument]);
        Assert.Equal("$2,000", arguments[ResearchText.PriceArgument]);
        Assert.Equal("Order Customer profile for Ada's Diner for $2,000? It is billed with tonight's marketing bill, and the report arrives tomorrow morning.",
            Render(table, node.MessageKey, arguments));
        Assert.Equal("Neighbourhood snapshot for Ada's Diner: residents, meal opportunities, household food and budget bands around the business. Price $500.",
            Render(table, menu.Nodes[0].MessageKey, menu.Nodes[0].Arguments("Ada's Diner")));
    }

    [Fact]
    public void AnEmptyProductTableBuildsNoMenu()
    {
        Assert.Throws<ArgumentException>(() => ResearchMenu.Build(Array.Empty<ResearchProduct>()));
    }

    // ---- research button click ----

    [Fact]
    public void NullOrIneligibleBusinessStaysOnTheCampaignScreen()
    {
        foreach (var (selected, eligible, error) in new[] { (false, false, ResearchText.NativeSelectBusiness), (false, true, ResearchText.NativeSelectBusiness),
            (true, false, ResearchText.Ineligible) })
        {
            var errors = new List<string>();
            int built = 0;
            var next = ResearchClick.Run<object>(selected, eligible, errors.Add, () => { built++; return new object(); });
            // Null is native DialogController.SecondOptionCurrentEntry's "show nothing new": the input entry stays.
            Assert.Null(next);
            Assert.Equal(new[] { error }, errors);
            Assert.Equal(0, built);
        }
        Assert.Equal("common_notification_select_business", ResearchText.NativeSelectBusiness);

        var shown = new List<string>();
        var menu = new object();
        Assert.Same(menu, ResearchClick.Run(true, true, shown.Add, () => menu));
        Assert.Empty(shown);
    }

    // ---- campaign-screen gate ----

    private sealed class FakeEntry
    {
        public Func<FakeEntry?>? OnConfirm, OnSecondOption;
        public Action? OnCancel;
        public int Template = 1, InputTemplate = 5;
        public string? SecondOptionTextOverride;
    }

    private static ResearchDialogPreconditions Preconditions(int bits) => new ResearchDialogPreconditions(
        (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0);

    [Fact]
    public void NativeEntryIsUnchangedWheneverAnyPreconditionFails()
    {
        for (int bits = 0; bits < 128; bits++)
        {
            Func<FakeEntry?> confirm = () => null;
            Action cancel = () => { };
            var entry = new FakeEntry { OnConfirm = confirm, OnCancel = cancel };
            int extended = 0;
            var refusal = ResearchDialogGate.TryExtend(entry, _ => Preconditions(bits), e =>
            {
                extended++;
                e.OnSecondOption = () => null;
                e.SecondOptionTextOverride = ResearchText.Button;
            });
            if (bits == 127)
            {
                Assert.Equal(ResearchDialogRefusal.None, refusal);
                Assert.Equal(1, extended);
                Assert.NotNull(entry.OnSecondOption);
                Assert.Equal(ResearchText.Button, entry.SecondOptionTextOverride);
            }
            else
            {
                Assert.NotEqual(ResearchDialogRefusal.None, refusal);
                Assert.Equal(0, extended);
                Assert.Null(entry.OnSecondOption);
                Assert.Null(entry.SecondOptionTextOverride);
            }
            // OnConfirm, OnCancel and the template are the native ones in every case.
            Assert.Same(confirm, entry.OnConfirm);
            Assert.Same(cancel, entry.OnCancel);
            Assert.Equal(1, entry.Template);
            Assert.Equal(5, entry.InputTemplate);
        }
    }

    [Fact]
    public void GateReportsTheFirstFailedPreconditionAndStaysHiddenWhileUnavailable()
    {
        var order = new[] { ResearchDialogRefusal.NotAttached, ResearchDialogRefusal.Contract, ResearchDialogRefusal.Unavailable,
            ResearchDialogRefusal.NativeEntryChanged, ResearchDialogRefusal.SecondOptionInUse, ResearchDialogRefusal.Localization,
            ResearchDialogRefusal.NoEligibleBusiness };
        for (int failed = 0; failed < order.Length; failed++)
            Assert.Equal(order[failed], ResearchDialogGate.Check(Preconditions(127 & ~(1 << failed))));
        Assert.Equal(ResearchDialogRefusal.None, ResearchDialogGate.Check(Preconditions(127)));

        // Settings with no attached checks (C5f derives Available from them) are unavailable, so the button is never added.
        Assert.False(new MarketResearchSettings().Available);
        Assert.False(typeof(MarketResearchSettings).GetProperty("Available")!.CanWrite);
        var settings = new MarketResearchSettings();
        var available = new ResearchDialogPreconditions(true, true, settings.Available, true, true, true, true);
        var entry = new FakeEntry();
        Assert.Equal(ResearchDialogRefusal.Unavailable, ResearchDialogGate.TryExtend(entry, _ => available, _ => throw new InvalidOperationException()));
        Assert.Null(entry.OnSecondOption);

        // A missing native entry is never read or extended.
        Assert.Equal(ResearchDialogRefusal.NativeEntryChanged,
            ResearchDialogGate.TryExtend<FakeEntry>(null, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
    }

    // ---- native wiring, pinned by source ----

    // ---- eligibility (ordering and billing share it) ----

    [Fact]
    public void EligibilityFollowsCurrentFoodAuthorityThroughWithdrawalAndReacquisition()
    {
        var authority = new FoodAuthority { PlaceId = "address:test:1:shop" };
        Assert.True(authority.Boundary(11, oldWorkDrained: true, liveNativeCustomers: false, marketReady: true));
        string first = authority.BusinessId;
        Assert.True(ResearchEligibility.Decide(true, first, true, authority));
        Assert.False(ResearchEligibility.Decide(false, first, true, authority));
        Assert.False(ResearchEligibility.Decide(true, null, true, authority));
        Assert.False(ResearchEligibility.Decide(true, first, false, authority));
        Assert.False(ResearchEligibility.Decide(true, first, true, null));

        // An unsupported menu returns the business to native ownership. The scope, registration and BusinessId stay
        // (the retained generation still matches), but the authority can no longer serve: refused at once.
        Assert.Equal(first, authority.BusinessId);
        Assert.True(authority.Managed);
        Assert.False(authority.CanServe);
        Assert.False(ResearchEligibility.Decide(true, first, true, authority));
        Assert.True(authority.Boundary(21, true, false, true));
        Assert.False(authority.Managed);
        Assert.False(ResearchEligibility.Decide(true, first, true, authority));

        Assert.False(ResearchEligibility.Decide(true, first, true, authority));
        Assert.True(authority.Boundary(31, true, false, true));
        Assert.True(ResearchEligibility.Decide(true, first, true, authority));

        // A replaced business is a new generation: the old scope's ID no longer matches until the scope follows.
        Assert.NotEqual(first, authority.BusinessId);
        Assert.False(ResearchEligibility.Decide(true, first, true, authority));

        // Ordering (the dialog desk) and billing (the charge port) get the same predicate.
    }

    // ---- contract refusal keeps saved research idle ----

    [Fact]
    public void ARefusedDialogOrBillingContractLeavesSavedOrdersUnsurveyedAndUncharged()
    {
        for (int mask = 0; mask < 8; mask++)
            Assert.Equal(mask == 7, ResearchEligibility.SessionEnabled((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0));
    }

    // ---- localization ----

    [Fact]
    public void LocalizationTableIsTheFlatEnglishTableTheNativeLoaderReads()
    {
        var table = Table();
        // LoadLocalizationTables lower-cases and trims every key; ours already are, and all carry the mod prefix.
        foreach (string key in table.Keys)
        {
            Assert.Equal(key.ToLowerInvariant().Trim(), key);
            Assert.StartsWith(ResearchText.Prefix, key);
        }
        // C5f adds the agency delivery and decline messages to the same table.
        var used = ResearchText.Keys(ResearchProducts.All).Concat(ResearchNoticeText.Keys()).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(used.OrderBy(k => k, StringComparer.Ordinal), table.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var value in table.Values) Assert.False(string.IsNullOrWhiteSpace(value));
        // Every placeholder is an argument the dialog passes.
        var arguments = new[] { ResearchText.ProductArgument, ResearchText.DescriptionArgument, ResearchText.BusinessArgument, ResearchText.PriceArgument,
            ResearchNoticeText.BusinessNameArgument };
        foreach (var value in table.Values)
            foreach (Match match in Regex.Matches(value, @"\{(\w+)\}")) Assert.Contains(match.Groups[1].Value, arguments);
        // English only.
        // Shipped as <mod>/Locales/<locale>.json, the folder native ModDiscoveryRegistry registers per mod.
    }

    // ---- perf ----

    private void Measure(string label, int runs, Action call)
    {
        for (int i = 0; i < 200; i++) call(); // tier-up JIT work is not per-call work
        string line = "", check = label + " p95 (5 ms gate)";
        TimingBudget.Run(timing =>
        {
            var samples = new double[runs];
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < runs; i++)
            {
                var start = timing.Start();
                call();
                samples[i] = timing.Stop(start, check);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Array.Sort(samples);
            line = $"{label}: p50={samples[runs / 2]:F4}ms p95={samples[runs * 95 / 100]:F4}ms max={samples[runs - 1]:F4}ms bytes/call={bytes / runs}";
            timing.Under(check, samples[runs * 95 / 100], 5);
        });
        output.WriteLine(line);
    }

    // ---- helpers ----

    /// <summary>Native LocalizorManager.GetLocalization: key lookup, then "{name}" replacement, with a key-valued
    /// argument rendered through the table.</summary>
    private static string Render(IReadOnlyDictionary<string, string> table, string key, IReadOnlyDictionary<string, string> arguments)
    {
        string text = table[key];
        foreach (var (name, value) in arguments) text = text.Replace("{" + name + "}", table.TryGetValue(value, out var localized) ? localized : value);
        return text;
    }

    private static Dictionary<string, string> Table()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Runtime", "Localization", "en.json")));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
            Assert.True(table.TryAdd(property.Name, property.Value.GetString()!), "duplicate key " + property.Name);
        }
        return table;
    }

    private static string Between(string text, string start, string end)
    {
        int at = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(at >= 0, start);
        int stop = text.IndexOf(end, at, StringComparison.Ordinal);
        Assert.True(stop > at, end);
        return text.Substring(at, stop - at);
    }

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Root(), relative)).Replace("\r\n", "\n");

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }
}
