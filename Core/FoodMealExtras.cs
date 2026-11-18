using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct FoodTicketLine
    {
        public FoodTicketLine(string productId, Money price) { ProductId = productId; Price = price; }
        public string ProductId { get; }
        public Money Price { get; }
    }

    public readonly struct FoodExtraOffer
    {
        public FoodExtraOffer(string productId, Money price, int? units, int tasteBand)
        { ProductId = productId; Price = price; Units = units; TasteBand = tasteBand; }
        public string ProductId { get; }
        public Money Price { get; }
        public int? Units { get; }
        public int TasteBand { get; }
    }

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

    public static class FoodMealExtras
    {
        public const int MaximumExtras = 3;
        public const int CheaperPercent = 75, SimilarPercent = 125;
        public const double CheaperChance = .35, SimilarChance = .05, MaximumChance = .9;
        private static readonly double[] TasteRatios = { .5, .75, 1, 1.5, 2 };

        public static FoodExtraBand Band(Money main, Money extra)
        {
            if (main <= Money.Zero || extra <= Money.Zero) return FoodExtraBand.Excluded;
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

        public static FoodMealTicket Plan(int seed, string consumerId, int day, MealKind kind, string businessId,
            FoodTicketLine main, IEnumerable<FoodExtraOffer> menu, Money allowance)
        {
            var extras = new List<FoodTicketLine>(MaximumExtras);
            Choose(seed, consumerId, day, kind, businessId, main, Listed(menu), allowance, extras);
            return new FoodMealTicket(businessId, main, extras);
        }

        internal static Money PlanTotal(int seed, string consumerId, int day, MealKind kind, string businessId,
            FoodTicketLine main, List<FoodExtraOffer> menu, Money allowance)
            => Choose(seed, consumerId, day, kind, businessId, main, menu, allowance, null);

        [ThreadStatic] private static List<FoodExtraOffer>? menuScratch;
        [ThreadStatic] private static List<(FoodExtraOffer Offer, FoodExtraBand Band, double Rank)>? likedScratch;
        [ThreadStatic] private static List<string>? seenScratch;

        private static readonly Comparison<(FoodExtraOffer Offer, FoodExtraBand Band, double Rank)> LikedOrder = (a, b) =>
        {
            int c = a.Band.CompareTo(b.Band);
            if (c == 0) c = a.Rank.CompareTo(b.Rank);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.Offer.ProductId, b.Offer.ProductId);
        };

        private static List<FoodExtraOffer> Listed(IEnumerable<FoodExtraOffer> menu)
        {
            if (menu is List<FoodExtraOffer> list) return list;
            var copy = menuScratch ??= new List<FoodExtraOffer>();
            copy.Clear();
            foreach (var offer in menu) copy.Add(offer);
            return copy;
        }

        private static bool Seen(List<string> seen, string product)
        {
            for (int i = 0; i < seen.Count; i++) if (string.Equals(seen[i], product, StringComparison.Ordinal)) return true;
            seen.Add(product);
            return false;
        }

        private static Money Choose(int seed, string consumerId, int day, MealKind kind, string businessId,
            FoodTicketLine main, List<FoodExtraOffer> menu, Money allowance, List<FoodTicketLine>? extras)
        {
            var liked = likedScratch ??= new List<(FoodExtraOffer Offer, FoodExtraBand Band, double Rank)>();
            var seen = seenScratch ??= new List<string>();
            liked.Clear(); seen.Clear(); seen.Add(main.ProductId);
            try
            {
                uint prefix = Prefix(seed, consumerId, day, kind, businessId, main.ProductId);
                for (int i = 0; i < menu.Count; i++)
                {
                    var offer = menu[i];
                    if (Seen(seen, offer.ProductId) || offer.Price <= Money.Zero || offer.Units.HasValue && offer.Units.Value <= 0) continue;
                    var band = Band(main.Price, offer.Price);
                    double chance = Chance(band, offer.TasteBand);
                    if (chance <= 0 || Uniform(prefix, "like", offer.ProductId) >= chance) continue;
                    double weight = TasteRatios[Math.Max(0, Math.Min(ResidentTraits.MaximumBand, offer.TasteBand))];
                    liked.Add((offer, band, -Math.Log(1 - Uniform(prefix, "order", offer.ProductId)) / weight));
                }
                liked.Sort(LikedOrder);
                var total = main.Price;
                int taken = 0;
                for (int i = 0; i < liked.Count; i++)
                {
                    if (taken == MaximumExtras) break;
                    var offer = liked[i].Offer;
                    if (!Money.TryAdd(total, offer.Price, out var next) || next > allowance) continue;
                    total = next; taken++;
                    extras?.Add(new FoodTicketLine(offer.ProductId, offer.Price));
                }
                return total;
            }
            finally { liked.Clear(); seen.Clear(); }
        }

        private static uint Prefix(int seed, string consumer, int day, MealKind kind, string business, string main)
        {
            uint h = StableRandom.Append(StableRandom.Begin(seed), "pc:food-extra:v1");
            h = Field(h, consumer); h = Field(h, day);
            h = Field(h, (int)kind); h = Field(h, business);
            return Field(h, main);
        }
        private static double Uniform(uint prefix, string purpose, string product)
            => StableRandom.Finish(Field(Field(prefix, purpose), product)) / 4294967296.0;
        private static uint Field(uint h, string s)
            => StableRandom.Append(StableRandom.Append(StableRandom.AppendDecimal(StableRandom.Append(h, '|'), s.Length), ':'), s);
        private static uint Field(uint h, int value)
            => StableRandom.AppendDecimal(StableRandom.Append(StableRandom.AppendDecimal(StableRandom.Append(h, '|'), DecimalLength(value)), ':'), value);
        private static int DecimalLength(int value)
        {
            uint magnitude = value < 0 ? unchecked((uint)(-(long)value)) : (uint)value;
            int length = value < 0 ? 2 : 1;
            while (magnitude >= 10) { magnitude /= 10; length++; }
            return length;
        }
    }
}
