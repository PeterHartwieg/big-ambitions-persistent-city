using BigAmbitions.Items;
using Seasons;

namespace PersistentCity.Runtime
{
    // Seasonal availability helpers can load assets or read preferences. This probe accepts
    // only explicitly ungated loaded definitions; it never evaluates or substitutes a season.
    internal static class RemainingSeasonalStockProbe
    {
        internal static DirectStockFields Read(BuildingRegistration registration, DirectStockCandidate candidate)
        {
            var product = ItemsGetter.AllItems == null ? null : ItemsGetter.GetByName(candidate.ProductId, true);
            if (product != null && (product.season != SeasonName.None
                || product.isSpecialGift || product.isSeasonalForSale || product.isSeasonalItemFromAIBusiness
                || product.itemsBySeason != null && product.itemsBySeason.Length != 0))
                return new DirectStockFields("Seasonal availability or substitution not evaluated: unsupported");
            return DirectStockShowcaseReader.Read(registration, candidate);
        }
    }

    /// <summary>Conditional field diagnostic only; shipped cheeseplatter holder unestablished.</summary>
    public static class CheesePlatterFeasibilityReader
    {
        public const string Product = "ba:itemname_cheeseplatter";
        public static CheesePlatterFeasibilitySnapshot Read(BuildingRegistration registration)
            => new CheesePlatterFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.CheesePlatter));
    }
    public sealed class CheesePlatterFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal CheesePlatterFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => CheesePlatterFeasibilityReader.Product;
    }

    /// <summary>Conditional field diagnostic only; shipped cottoncandy holder unestablished.</summary>
    public static class CottonCandyFeasibilityReader
    {
        public const string Product = "ba:itemname_cottoncandy";
        public static CottonCandyFeasibilitySnapshot Read(BuildingRegistration registration)
            => new CottonCandyFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.CottonCandy));
    }
    public sealed class CottonCandyFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal CottonCandyFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => CottonCandyFeasibilityReader.Product;
    }

    /// <summary>Conditional field diagnostic only; shipped popcorn holder unestablished.</summary>
    public static class PopcornFeasibilityReader
    {
        public const string Product = "ba:itemname_popcorn";
        public static PopcornFeasibilitySnapshot Read(BuildingRegistration registration)
            => new PopcornFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Popcorn));
    }
    public sealed class PopcornFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal PopcornFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => PopcornFeasibilityReader.Product;
    }

    /// <summary>Conditional field diagnostic only; shipped kabob holder unestablished.</summary>
    public static class KabobFeasibilityReader
    {
        public const string Product = "ba:itemname_kabob";
        public static KabobFeasibilitySnapshot Read(BuildingRegistration registration)
            => new KabobFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Kabob));
    }
    public sealed class KabobFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal KabobFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => KabobFeasibilityReader.Product;
    }

    /// <summary>Conditional ungated field diagnostic only; retail status/availability unestablished.</summary>
    public static class CandyCaneFeasibilityReader
    {
        public const string Product = "ba:itemname_candycane";
        public static CandyCaneFeasibilitySnapshot Read(BuildingRegistration registration)
            => new CandyCaneFeasibilitySnapshot(RemainingSeasonalStockProbe.Read(registration, DirectStockCandidate.CandyCane));
    }
    public sealed class CandyCaneFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal CandyCaneFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => CandyCaneFeasibilityReader.Product;
    }

    /// <summary>Conditional ungated field diagnostic only; retail status/availability unestablished.</summary>
    public static class ChristmasCookiesFeasibilityReader
    {
        public const string Product = "ba:itemname_christmascookies";
        public static ChristmasCookiesFeasibilitySnapshot Read(BuildingRegistration registration)
            => new ChristmasCookiesFeasibilitySnapshot(RemainingSeasonalStockProbe.Read(registration, DirectStockCandidate.ChristmasCookies));
    }
    public sealed class ChristmasCookiesFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal ChristmasCookiesFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => ChristmasCookiesFeasibilityReader.Product;
    }
}
