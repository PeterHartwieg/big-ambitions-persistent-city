using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Why the agency campaign screen stays native. None means the research button may be added.</summary>
    internal enum ResearchDialogRefusal { None, NotAttached, Contract, Unavailable, NativeEntryChanged, SecondOptionInUse, Localization, NoEligibleBusiness }

    /// <summary>Plan C5 section 3.1 preconditions, in the order the postfix checks them (cheapest first).</summary>
    internal readonly struct ResearchDialogPreconditions
    {
        internal ResearchDialogPreconditions(bool attached, bool contractPassed, bool available, bool nativeEntryShape, bool secondOptionUnused,
            bool localizationLoaded, bool hasEligibleBusiness)
        {
            Attached = attached; ContractPassed = contractPassed; Available = available; NativeEntryShape = nativeEntryShape;
            SecondOptionUnused = secondOptionUnused; LocalizationLoaded = localizationLoaded; HasEligibleBusiness = hasEligibleBusiness;
        }
        /// <summary>Persistent City is attached to the current save and not stopped.</summary>
        internal bool Attached { get; }
        /// <summary>The dialog and billing contract checks passed and no foreign patch is on CampaignSettings.</summary>
        internal bool ContractPassed { get; }
        /// <summary>MarketResearchSettings.Available, derived from the attach and contract checks (C5f).</summary>
        internal bool Available { get; }
        /// <summary>The returned entry is the native input entry with the campaign settings template and both handlers.</summary>
        internal bool NativeEntryShape { get; }
        /// <summary>The native second-option slot is still empty.</summary>
        internal bool SecondOptionUnused { get; }
        /// <summary>The mod's own table is loaded (the button key resolves).</summary>
        internal bool LocalizationLoaded { get; }
        /// <summary>At least one player business is an eligible research target.</summary>
        internal bool HasEligibleBusiness { get; }
    }

    /// <summary>The only place that decides whether the native campaign entry is extended. A refused check returns
    /// the entry untouched; an accepted one lets the caller set the second-option slot and nothing else.</summary>
    internal static class ResearchDialogGate
    {
        internal static ResearchDialogRefusal Check(in ResearchDialogPreconditions p)
        {
            if (!p.Attached) return ResearchDialogRefusal.NotAttached;
            if (!p.ContractPassed) return ResearchDialogRefusal.Contract;
            if (!p.Available) return ResearchDialogRefusal.Unavailable;
            if (!p.NativeEntryShape) return ResearchDialogRefusal.NativeEntryChanged;
            if (!p.SecondOptionUnused) return ResearchDialogRefusal.SecondOptionInUse;
            if (!p.LocalizationLoaded) return ResearchDialogRefusal.Localization;
            if (!p.HasEligibleBusiness) return ResearchDialogRefusal.NoEligibleBusiness;
            return ResearchDialogRefusal.None;
        }

        /// <summary>Calls extend only when every precondition holds. The preconditions are read lazily, so a refusal
        /// early in the list never reaches the eligibility scan.</summary>
        internal static ResearchDialogRefusal TryExtend<TEntry>(TEntry? entry, Func<TEntry, ResearchDialogPreconditions> read, Action<TEntry> extend)
            where TEntry : class
        {
            if (entry == null) return ResearchDialogRefusal.NativeEntryChanged;
            var refusal = Check(read(entry));
            if (refusal == ResearchDialogRefusal.None) extend(entry);
            return refusal;
        }
    }

    /// <summary>Plan section 9, Q4, shared by ordering and billing: a player-rented, named business whose PC food scope
    /// is the generation Persistent City observed and whose food authority can serve now. A business that went back to
    /// native ownership (an unsupported menu, a closed interval) keeps its scope and generation but cannot serve.</summary>
    internal static class ResearchEligibility
    {
        internal static bool Decide(bool rentedAndNamed, string? scopeBusinessId, bool observedOwnGeneration, FoodAuthority? authority)
            => rentedAndNamed && !string.IsNullOrEmpty(scopeBusinessId) && observedOwnGeneration
                && authority != null && string.Equals(authority.BusinessId, scopeBusinessId, StringComparison.Ordinal) && authority.CanServe;

        /// <summary>Saved research runs (survey and bill) only when both the dialog and the billing contract passed.
        /// Otherwise the ledger is kept in the save unchanged and nothing surveys or charges.</summary>
        internal static bool SessionEnabled(bool dialogContract, bool billingContract) => dialogContract && billingContract;
    }

    internal enum ResearchClickOutcome { SelectBusiness, Ineligible, Menu }

    internal static class ResearchClick
    {
        /// <summary>The native confirm's rule: no selected business shows the native error and stays.</summary>
        internal static ResearchClickOutcome Decide(bool businessSelected, bool eligible)
            => !businessSelected ? ResearchClickOutcome.SelectBusiness : !eligible ? ResearchClickOutcome.Ineligible : ResearchClickOutcome.Menu;

        /// <summary>Runs the click: an error shows a notification and returns null, which native
        /// DialogController.SecondOptionCurrentEntry treats as "stay on this entry".</summary>
        internal static TEntry? Run<TEntry>(bool businessSelected, bool eligible, Action<string> showError, Func<TEntry> menu) where TEntry : class
        {
            switch (Decide(businessSelected, eligible))
            {
                case ResearchClickOutcome.SelectBusiness: showError(ResearchText.NativeSelectBusiness); return null;
                case ResearchClickOutcome.Ineligible: showError(ResearchText.Ineligible); return null;
                default: return menu();
            }
        }
    }

    internal enum ResearchMenuNodeKind { Product, Confirmation }
    /// <summary>Where a button leads: another menu node, back to the native campaign screen, or the order.</summary>
    internal enum ResearchMenuTarget { Node, CampaignScreen, PlaceOrder }

    internal readonly struct ResearchMenuChoice
    {
        internal ResearchMenuChoice(ResearchMenuTarget target, int node, string textKey) { Target = target; Node = node; TextKey = textKey; }
        internal ResearchMenuTarget Target { get; }
        /// <summary>The node index for Target Node; -1 otherwise.</summary>
        internal int Node { get; }
        internal string TextKey { get; }
    }

    /// <summary>One text entry of the research menu: a message, at most two choices (confirm and second option)
    /// and the native hang-up as cancel.</summary>
    internal sealed class ResearchMenuNode
    {
        internal ResearchMenuNode(ResearchMenuNodeKind kind, ResearchProduct product, string messageKey, ResearchMenuChoice confirm, ResearchMenuChoice second)
        { Kind = kind; Product = product; MessageKey = messageKey; Confirm = confirm; Second = second; }
        internal ResearchMenuNodeKind Kind { get; }
        internal ResearchProduct Product { get; }
        internal string MessageKey { get; }
        internal ResearchMenuChoice Confirm { get; }
        internal ResearchMenuChoice Second { get; }

        /// <summary>Message arguments. The product and description values are localization keys; native
        /// LocalizorManager.GetLocalization renders a key-valued argument.</summary>
        internal Dictionary<string, string> Arguments(string businessName) => new Dictionary<string, string>(4, StringComparer.Ordinal)
        {
            { ResearchText.ProductArgument, ResearchText.ProductName(Product.Code) },
            { ResearchText.DescriptionArgument, ResearchText.ProductDescription(Product.Code) },
            { ResearchText.BusinessArgument, businessName },
            { ResearchText.PriceArgument, ResearchText.Price(Product) }
        };
    }

    /// <summary>The research menu as a binary tree of text entries built from the product table. Nodes 0..n-1 offer
    /// one product each: confirm chooses it, second option offers the next product, and on the last product goes back
    /// to the native campaign screen. Nodes n..2n-1 confirm one product: confirm places the order, second option goes
    /// back to that product. Pure; the native dialog walks it.</summary>
    internal sealed class ResearchMenu
    {
        private ResearchMenu(ResearchMenuNode[] nodes) { Nodes = nodes; }
        internal IReadOnlyList<ResearchMenuNode> Nodes { get; }
        internal const int Root = 0;

        internal static ResearchMenu Build(IReadOnlyList<ResearchProduct> products)
        {
            if (products == null || products.Count == 0) throw new ArgumentException("The research menu needs at least one product.", nameof(products));
            int n = products.Count;
            var nodes = new ResearchMenuNode[2 * n];
            for (int i = 0; i < n; i++)
            {
                var next = i + 1 < n
                    ? new ResearchMenuChoice(ResearchMenuTarget.Node, i + 1, ResearchText.OtherProduct)
                    : new ResearchMenuChoice(ResearchMenuTarget.CampaignScreen, -1, ResearchText.BackToCampaigns);
                nodes[i] = new ResearchMenuNode(ResearchMenuNodeKind.Product, products[i], ResearchText.ProductOffer,
                    new ResearchMenuChoice(ResearchMenuTarget.Node, n + i, ResearchText.Choose), next);
                nodes[n + i] = new ResearchMenuNode(ResearchMenuNodeKind.Confirmation, products[i], ResearchText.Confirmation,
                    new ResearchMenuChoice(ResearchMenuTarget.PlaceOrder, -1, ResearchText.PlaceOrder),
                    new ResearchMenuChoice(ResearchMenuTarget.Node, i, ResearchText.NativeBack));
            }
            return new ResearchMenu(nodes);
        }
    }

    /// <summary>The mod's localization keys. The table is Runtime/Localization/en.json, shipped as
    /// &lt;mod&gt;/Locales/en.json: the native mod loader (ModDiscoveryRegistry.SyncDiscoveredLocalizationPaths)
    /// registers every discovered mod's Locales folder, and LocalizorManager.LoadLocalizationTables reads
    /// &lt;folder&gt;/&lt;locale&gt;.json as one flat JSON object of lower-case keys. English only; other languages fall back to it.</summary>
    internal static class ResearchText
    {
        internal const string Prefix = "persistentcity_research_";
        internal const string Button = Prefix + "button";
        internal const string Ineligible = Prefix + "notification_ineligible";
        internal const string OpenOrder = Prefix + "notification_open_order";
        internal const string OrderLimit = Prefix + "notification_order_limit";
        internal const string Unavailable = Prefix + "notification_unavailable";
        internal const string ProductOffer = Prefix + "menu_product";
        internal const string Confirmation = Prefix + "menu_confirmation";
        internal const string Choose = Prefix + "menu_choose";
        internal const string OtherProduct = Prefix + "menu_other_product";
        internal const string BackToCampaigns = Prefix + "menu_back_to_campaigns";
        internal const string PlaceOrder = Prefix + "menu_place_order";
        internal const string Ordered = Prefix + "menu_ordered";
        /// <summary>Native keys reused as-is.</summary>
        internal const string NativeSelectBusiness = "common_notification_select_business";
        internal const string NativeBack = "common_back";

        internal const string ProductArgument = "product";
        internal const string DescriptionArgument = "description";
        internal const string BusinessArgument = "business";
        internal const string PriceArgument = "price";

        internal static string ProductName(string code) => Prefix + "product_" + code;
        internal static string ProductDescription(string code) => Prefix + "product_" + code + "_description";
        /// <summary>The exact whole-dollar price, for example "$2,000".</summary>
        internal static string Price(ResearchProduct product) => "$" + product.PriceDollars.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Every mod key the dialog uses for the given products.</summary>
        internal static IEnumerable<string> Keys(IReadOnlyList<ResearchProduct> products)
        {
            foreach (var key in new[] { Button, Ineligible, OpenOrder, OrderLimit, Unavailable, ProductOffer, Confirmation, Choose,
                OtherProduct, BackToCampaigns, PlaceOrder, Ordered }) yield return key;
            foreach (var product in products) { yield return ProductName(product.Code); yield return ProductDescription(product.Code); }
        }
    }
}
