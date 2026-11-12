using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal enum ResearchDialogRefusal { None, NotAttached, Contract, Unavailable, NativeEntryChanged, SecondOptionInUse, Localization, NoEligibleBusiness }

    internal readonly struct ResearchDialogPreconditions
    {
        internal ResearchDialogPreconditions(bool attached, bool contractPassed, bool available, bool nativeEntryShape, bool secondOptionUnused,
            bool localizationLoaded, bool hasEligibleBusiness)
        {
            Attached = attached; ContractPassed = contractPassed; Available = available; NativeEntryShape = nativeEntryShape;
            SecondOptionUnused = secondOptionUnused; LocalizationLoaded = localizationLoaded; HasEligibleBusiness = hasEligibleBusiness;
        }
        internal bool Attached { get; }
        internal bool ContractPassed { get; }
        internal bool Available { get; }
        internal bool NativeEntryShape { get; }
        internal bool SecondOptionUnused { get; }
        internal bool LocalizationLoaded { get; }
        internal bool HasEligibleBusiness { get; }
    }

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

        internal static ResearchDialogRefusal TryExtend<TEntry>(TEntry? entry, Func<TEntry, ResearchDialogPreconditions> read, Action<TEntry> extend)
            where TEntry : class
        {
            if (entry == null) return ResearchDialogRefusal.NativeEntryChanged;
            var refusal = Check(read(entry));
            if (refusal == ResearchDialogRefusal.None) extend(entry);
            return refusal;
        }
    }

    internal static class ResearchEligibility
    {
        internal static bool Decide(bool rentedAndNamed, string? scopeBusinessId, bool observedOwnGeneration, FoodAuthority? authority)
            => rentedAndNamed && !string.IsNullOrEmpty(scopeBusinessId) && observedOwnGeneration
                && authority != null && string.Equals(authority.BusinessId, scopeBusinessId, StringComparison.Ordinal) && authority.CanServe;

        internal static bool SessionEnabled(bool dialogContract, bool billingContract, bool deliveryContract)
            => dialogContract && billingContract && deliveryContract;
    }

    internal enum ResearchClickOutcome { SelectBusiness, Ineligible, Menu }

    internal static class ResearchClick
    {
        internal static ResearchClickOutcome Decide(bool businessSelected, bool eligible)
            => !businessSelected ? ResearchClickOutcome.SelectBusiness : !eligible ? ResearchClickOutcome.Ineligible : ResearchClickOutcome.Menu;

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
    internal enum ResearchMenuTarget { Node, CampaignScreen, PlaceOrder }

    internal readonly struct ResearchMenuChoice
    {
        internal ResearchMenuChoice(ResearchMenuTarget target, int node, string textKey) { Target = target; Node = node; TextKey = textKey; }
        internal ResearchMenuTarget Target { get; }
        internal int Node { get; }
        internal string TextKey { get; }
    }

    internal sealed class ResearchMenuNode
    {
        internal ResearchMenuNode(ResearchMenuNodeKind kind, ResearchProduct product, string messageKey, ResearchMenuChoice confirm, ResearchMenuChoice second)
        { Kind = kind; Product = product; MessageKey = messageKey; Confirm = confirm; Second = second; }
        internal ResearchMenuNodeKind Kind { get; }
        internal ResearchProduct Product { get; }
        internal string MessageKey { get; }
        internal ResearchMenuChoice Confirm { get; }
        internal ResearchMenuChoice Second { get; }

        internal Dictionary<string, string> Arguments(string businessName) => new Dictionary<string, string>(4, StringComparer.Ordinal)
        {
            { ResearchText.ProductArgument, ResearchText.ProductName(Product.Code) },
            { ResearchText.DescriptionArgument, ResearchText.ProductDescription(Product.Code) },
            { ResearchText.BusinessArgument, businessName },
            { ResearchText.PriceArgument, ResearchText.Price(Product) }
        };
    }

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
        internal const string NativeSelectBusiness = "common_notification_select_business";
        internal const string NativeBack = "common_back";

        internal const string ProductArgument = "product";
        internal const string DescriptionArgument = "description";
        internal const string BusinessArgument = "business";
        internal const string PriceArgument = "price";

        internal static string ProductName(string code) => Prefix + "product_" + code;
        internal static string ProductDescription(string code) => Prefix + "product_" + code + "_description";
        internal static string Price(ResearchProduct product) => "$" + product.PriceDollars.ToString("N0", CultureInfo.InvariantCulture);

        internal static IEnumerable<string> Keys(IReadOnlyList<ResearchProduct> products)
        {
            foreach (var key in new[] { Button, Ineligible, OpenOrder, OrderLimit, Unavailable, ProductOffer, Confirmation, Choose,
                OtherProduct, BackToCampaigns, PlaceOrder, Ordered }) yield return key;
            foreach (var product in products) { yield return ProductName(product.Code); yield return ProductDescription(product.Code); }
        }
    }
}
