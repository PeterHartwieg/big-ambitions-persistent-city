using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ResearchCoverage { Catchment = 0, City = 1 }

    public sealed class ResearchProduct
    {
        internal ResearchProduct(string code, MarketResearchTier tier, ResearchCoverage coverage, int priceDollars)
        {
            if (priceDollars <= 0 || priceDollars > MaximumPriceDollars) throw new ArgumentOutOfRangeException(nameof(priceDollars));
            Code = code; Tier = tier; Coverage = coverage; PriceDollars = priceDollars;
            Price = Money.FromCents(priceDollars * 100L);
            NativePrice = priceDollars;
            if (Money.FromNative(NativePrice) != Price) throw new InvalidOperationException("Research price is not exact in binary32.");
        }
        public const int MaximumPriceDollars = 10000;
        public string Code { get; }
        public MarketResearchTier Tier { get; }
        public ResearchCoverage Coverage { get; }
        public int PriceDollars { get; }
        public Money Price { get; }
        public float NativePrice { get; }
    }

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
