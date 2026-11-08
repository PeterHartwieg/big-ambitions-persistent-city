using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>One priced line of a meal ticket.</summary>
    public readonly struct FoodTicketLine
    {
        public FoodTicketLine(string productId, Money price) { ProductId = productId; Price = price; }
        public string ProductId { get; }
        public Money Price { get; }
    }

    /// <summary>One item on the same shop's menu that could join the main dish. Units null means the seller
    /// publishes no stock (modeled rivals); a known zero is out of stock. TasteBand is 0-4.</summary>
    public readonly struct FoodExtraOffer
    {
        public FoodExtraOffer(string productId, Money price, int? units, int tasteBand)
        { ProductId = productId; Price = price; Units = units; TasteBand = tasteBand; }
        public string ProductId { get; }
        public Money Price { get; }
        public int? Units { get; }
        public int TasteBand { get; }
    }

    /// <summary>Main dish plus up to three same-visit extras from the same shop. Total is the whole ticket.</summary>
    public sealed class FoodMealTicket
    {
        public FoodMealTicket(string businessId, FoodTicketLine main, IReadOnlyList<FoodTicketLine> extras)
        {
            if (extras.Count > FoodMealExtras.MaximumExtras) throw new ArgumentException("Too many meal extras.");
            BusinessId = businessId; Main = main; Extras = extras;
            var total = main.Price;
            foreach (var extra in extras) total += extra.Price;
            Total = total;
        }
        public string BusinessId { get; }
        public FoodTicketLine Main { get; }
        public IReadOnlyList<FoodTicketLine> Extras { get; }
        public Money Total { get; }
    }

    public enum FoodExtraBand { Cheaper, Similar, Excluded }

    /// <summary>F6j same-visit extras (Peter, 8 Nov 2026). The main dish is chosen first by the unchanged F6i rule.
    /// Extras come only from that shop's own priced, stocked menu and are recognised only by price relative to the
    /// main: no product names, classes or the $12.50 meal reference. The wallet limit applies to the running total.</summary>
    public static class FoodMealExtras
    {
        public const int MaximumExtras = 3;
        // Price bands, as a fraction of the main dish price: at most 75% is a cheaper, snack-like extra;
        // above 75% and at most 125% is a similarly priced second dish; above 125% is never an extra.
        public const int CheaperPercent = 75, SimilarPercent = 125;
        // Neutral-taste chance that a resident wants one eligible item. A similar second dish is 7x rarer.
        public const double CheaperChance = .35, SimilarChance = .05, MaximumChance = .9;
        private static readonly double[] TasteRatios = { .5, .75, 1, 1.5, 2 };

        public static FoodExtraBand Band(Money main, Money extra)
        {
            if (main <= Money.Zero || extra <= Money.Zero) return FoodExtraBand.Excluded;
            // Exact integer comparison; decimal keeps the products inside range for any Money value.
            decimal m = main.Units, e = extra.Units;
            if (e * 100 <= m * CheaperPercent) return FoodExtraBand.Cheaper;
            if (e * 100 <= m * SimilarPercent) return FoodExtraBand.Similar;
            return FoodExtraBand.Excluded;
        }

        public static double Chance(FoodExtraBand band, int tasteBand)
        {
            if (band == FoodExtraBand.Excluded) return 0;
            double ratio = TasteRatios[Math.Max(0, Math.Min(ResidentTraits.MaximumBand, tasteBand))];
            return Math.Min(MaximumChance, (band == FoodExtraBand.Cheaper ? CheaperChance : SimilarChance) * ratio);
        }

        /// <summary>Deterministic for the same consumer, day, meal, shop, main and menu terms. The draw holds no
        /// context or attempt, so the choice-time ticket and the checkout ticket agree while terms are unchanged.
        /// Liked extras are added cheaper band first, then by taste-weighted rank, while the total stays within
        /// <paramref name="allowance"/>; an unaffordable one is skipped and the next considered.</summary>
        public static FoodMealTicket Plan(int seed, string consumerId, int day, MealKind kind, string businessId,
            FoodTicketLine main, IEnumerable<FoodExtraOffer> menu, Money allowance)
        {
            var liked = new List<(FoodExtraOffer Offer, FoodExtraBand Band, double Rank)>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { main.ProductId };
            uint prefix = Prefix(seed, consumerId, day, kind, businessId, main.ProductId);
            foreach (var offer in menu)
            {
                if (!seen.Add(offer.ProductId) || offer.Price <= Money.Zero || offer.Units.HasValue && offer.Units.Value <= 0) continue;
                var band = Band(main.Price, offer.Price);
                double chance = Chance(band, offer.TasteBand);
                if (chance <= 0 || Uniform(prefix, "like", offer.ProductId) >= chance) continue;
                double weight = TasteRatios[Math.Max(0, Math.Min(ResidentTraits.MaximumBand, offer.TasteBand))];
                liked.Add((offer, band, -Math.Log(1 - Uniform(prefix, "order", offer.ProductId)) / weight));
            }
            liked.Sort((a, b) =>
            {
                int c = a.Band.CompareTo(b.Band);
                if (c == 0) c = a.Rank.CompareTo(b.Rank);
                return c != 0 ? c : StringComparer.Ordinal.Compare(a.Offer.ProductId, b.Offer.ProductId);
            });
            var extras = new List<FoodTicketLine>(MaximumExtras);
            var total = main.Price;
            foreach (var (offer, _, _) in liked)
            {
                if (extras.Count == MaximumExtras) break;
                if (!Money.TryAdd(total, offer.Price, out var next) || next > allowance) continue;
                total = next; extras.Add(new FoodTicketLine(offer.ProductId, offer.Price));
            }
            return new FoodMealTicket(businessId, main, extras);
        }

        private static uint Prefix(int seed, string consumer, int day, MealKind kind, string business, string main)
        {
            uint h = StableRandom.Append(StableRandom.Begin(seed), "pc:food-extra:v1");
            h = Field(h, consumer); h = Field(h, day.ToString(CultureInfo.InvariantCulture));
            h = Field(h, ((int)kind).ToString(CultureInfo.InvariantCulture)); h = Field(h, business);
            return Field(h, main);
        }
        private static double Uniform(uint prefix, string purpose, string product)
            => StableRandom.Finish(Field(Field(prefix, purpose), product)) / 4294967296.0;
        private static uint Field(uint h, string s)
            => StableRandom.Append(StableRandom.Append(StableRandom.AppendDecimal(StableRandom.Append(h, '|'), s.Length), ':'), s);
    }
}
