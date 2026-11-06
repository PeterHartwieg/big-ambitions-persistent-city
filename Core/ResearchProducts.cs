using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ResearchCoverage { Catchment = 0, City = 1 }

    /// <summary>One fixed research product. Codes are saved: changing a row's meaning needs a new code.
    /// Prices are whole dollars, exact as Money and as a native binary32 amount. They are not settings.</summary>
    public sealed class ResearchProduct
    {
        internal ResearchProduct(string code, MarketResearchTier tier, ResearchCoverage coverage, int priceDollars)
        {
            // A whole-dollar price below 2^24 is exact in binary32; refuse any row that is not.
            if (priceDollars <= 0 || priceDollars > MaximumPriceDollars) throw new ArgumentOutOfRangeException(nameof(priceDollars));
            Code = code; Tier = tier; Coverage = coverage; PriceDollars = priceDollars;
            Price = Money.FromCents(priceDollars * 100L);
            NativePrice = priceDollars;
            if (Money.FromNative(NativePrice) != Price) throw new InvalidOperationException("Research price is not exact in binary32.");
        }
        /// <summary>The native money checks refuse prices above $10,000 (Money.MaximumNativePriceCents).</summary>
        public const int MaximumPriceDollars = 10000;
        public string Code { get; }
        public MarketResearchTier Tier { get; }
        public ResearchCoverage Coverage { get; }
        public int PriceDollars { get; }
        public Money Price { get; }
        /// <summary>The binary32 amount the native bill subtracts. Exactly PriceDollars.</summary>
        public float NativePrice { get; }
    }

    /// <summary>Peter's accepted defaults of 6 November 2026 (docs/decisions/2026-11-06-design-defaults.md).
    /// No In-depth product until its loyalty and rival-share cells have real inputs.</summary>
    public static class ResearchProducts
    {
        public const string BasicLocalCode = "basic-local";
        public const string DetailedLocalCode = "detailed-local";
        public const string DetailedCityCode = "detailed-city";
        public const int BasicLocalPriceDollars = 500;
        public const int DetailedLocalPriceDollars = 2000;
        public const int DetailedCityPriceDollars = 6000;

        public static ResearchProduct BasicLocal { get; } = new ResearchProduct(BasicLocalCode, MarketResearchTier.Basic, ResearchCoverage.Catchment, BasicLocalPriceDollars);
        public static ResearchProduct DetailedLocal { get; } = new ResearchProduct(DetailedLocalCode, MarketResearchTier.Detailed, ResearchCoverage.Catchment, DetailedLocalPriceDollars);
        public static ResearchProduct DetailedCity { get; } = new ResearchProduct(DetailedCityCode, MarketResearchTier.Detailed, ResearchCoverage.City, DetailedCityPriceDollars);

        /// <summary>Menu order.</summary>
        public static IReadOnlyList<ResearchProduct> All { get; } = Array.AsReadOnly(new[] { BasicLocal, DetailedLocal, DetailedCity });

        public static bool TryGet(string? code, out ResearchProduct? product)
        {
            foreach (var row in All)
                if (string.Equals(row.Code, code, StringComparison.Ordinal)) { product = row; return true; }
            product = null;
            return false;
        }
    }
}
