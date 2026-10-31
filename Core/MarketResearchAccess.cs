using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum MarketResearchTier { Basic = 0, Detailed = 1, InDepth = 2 }
    public enum MarketResearchMetric
    {
        Consumers = 0, RepresentedConsumers = 1, BreakfastOpportunities = 2, LunchOpportunities = 3, DinnerOpportunities = 4,
        HouseholdFood = 5, DiscretionaryBudget = 6, UsualLocationUnknown = 7,
        CroissantTaste = 8, BurgerTaste = 9, HotDogTaste = 10, PriceSensitivity = 11, QueuePatience = 12,
        TravelWillingness = 13, HealthOrientation = 14, QualityOrientation = 15, TimePressure = 16,
        UsualDayGroup = 17, TraitsUnknown = 18, LoyaltyDistribution = 19, RivalMarketShare = 20
    }
    public enum MarketResearchBand
    {
        All = 0, Low = 1, SomewhatLow = 2, Neutral = 3, SomewhatHigh = 4, High = 5, Unknown = 6,
        Funded = 7, Unfunded = 8, Work = 9, Study = 10, Other = 11
    }

    /// <summary>Fixed codes, no native names. Null is unavailable; zero is an observed empty count.</summary>
    public sealed class MarketResearchCell
    {
        internal MarketResearchCell(MarketResearchMetric metric, MarketResearchBand band, int? value)
        { Metric = metric; Band = band; Value = value; }
        public MarketResearchMetric Metric { get; }
        public MarketResearchBand Band { get; }
        public int? Value { get; }
        public string Label => Metric.ToString() + ":" + Band.ToString();
    }

    /// <summary>C5 must supply durable proof. C4 provides no accepting production implementation.</summary>
    public interface IMarketResearchEntitlement
    {
        bool TryGet(string entitlementId, out MarketResearchGrant? grant);
    }

    public sealed class MarketResearchGrant
    {
        public string EntitlementId { get; set; } = "";
        public string ReportId { get; set; } = "";
        public bool Paid { get; set; }
        public bool Delivered { get; set; }
        public bool Replaced { get; set; }
    }

    public sealed class NoPaymentPath : IMarketResearchEntitlement
    {
        public static NoPaymentPath Instance { get; } = new NoPaymentPath();
        private NoPaymentPath() { }
        public bool TryGet(string entitlementId, out MarketResearchGrant? grant)
        { grant = null; return false; }
    }

    /// <summary>Opaque completed report. Numbers have one public exit, guarded on every read.
    /// No public constructor, engine, diagnostic bypass or serialization getter for its cells.</summary>
    public sealed class MarketResearchReport
    {
        private readonly IReadOnlyList<MarketResearchCell> cells;
        internal MarketResearchReport(string id, MarketResearchTier tier, int logicalDay, string? address,
            int radiusMetres, MarketResearchCell[] cells)
        { Id = id; Tier = tier; LogicalDay = logicalDay; Address = address; RadiusMetres = radiusMetres; this.cells = Array.AsReadOnly(cells); }
        public string Id { get; }
        public MarketResearchTier Tier { get; }
        public int LogicalDay { get; }
        public string? Address { get; }
        public int RadiusMetres { get; }
        public string Evidence => "Modelled demand estimate, including represented consumers; not sales or footfall. "
            + "Catchment uses usual home/work/study straight-line distance, a walking-time lower bound; no routes. "
            + "Household food means individual basket funding, not inferred families. Budget bands: below $10, $10 to below $25, $25 or more. "
            + "Band counts cover known inputs; unknown inputs have separate buckets. Unknown usual locations can undercount catchment membership.";

        public bool TryRead(IMarketResearchEntitlement? access, string entitlementId,
            out IReadOnlyList<MarketResearchCell>? numbers)
        {
            numbers = null;
            if (access == null || string.IsNullOrEmpty(entitlementId)
                || !access.TryGet(entitlementId, out var grant) || grant == null
                || !grant.Paid || !grant.Delivered || grant.Replaced
                || grant.EntitlementId != entitlementId || grant.ReportId != Id) return false;
            numbers = cells;
            return true;
        }

        // Only friend test/offline assemblies can inspect fixtures. Runtime has no access to this seam.
        internal IReadOnlyList<MarketResearchCell> FixtureCells => cells;
    }
}
