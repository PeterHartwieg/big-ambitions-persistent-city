using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>E5b5: why a live player shop menu is not admitted. Any refusal keeps the whole shop with the game's customers.</summary>
    public enum FoodLiveMenuRefusal
    {
        None, Empty, Width, Duplicate, Name,
        /// <summary>A non-original product while dynamic acquisition is off (Decision 12: a capability failed or the
        /// coordinator is not active).</summary>
        OriginalThreeGuard,
        /// <summary>Not in the published loaded-name image (mod removed, catalogue rebuilding or policy fallback).</summary>
        NotLoaded,
        /// <summary>The resolver classes the product Drink, Bundle, NonFood or Unknown.</summary>
        MealClass
    }

    /// <summary>Schema 23 envelope, separate from native sale permission. Append codes; never renumber.</summary>
    public static partial class FoodMenuPolicy
    {
        public const int MaximumOffers = 8;
        /// <summary>E5b5 (Q1 default): live width is the hard limit; a shop with more products keeps the game's customers.</summary>
        public const int LiveMaximumOffers = MaximumOffers;
        public const int MaximumDefinitions = 16;
        public const int Version = 1;
        public const string Wire = "1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad";
        /// <summary>E5b5 (Decision 12, R12): the name rule of a live player shop menu, in place of the historical
        /// original-three guard. <paramref name="dynamic"/> is null while dynamic acquisition is off, so only the original
        /// three pass. Otherwise every product must be canonical, in the policy's published loaded-name image and classed
        /// Food by its resolver, the same rule as <see cref="FoodProductPolicy.Executable"/>. The stock contract and
        /// <see cref="CompleteFoodMenu.Check"/> still decide through the published contract image. Allocation-free.</summary>
        public static FoodLiveMenuRefusal LiveMenu(IReadOnlyList<string>? menu, FoodProductPolicy? dynamic)
        {
            if (menu == null || menu.Count < 1) return FoodLiveMenuRefusal.Empty;
            if (menu.Count > LiveMaximumOffers) return FoodLiveMenuRefusal.Width;
            for (int i = 0; i < menu.Count; i++)
            {
                string name = menu[i];
                if (!FoodProducts.Known(name))
                {
                    if (dynamic == null) return FoodLiveMenuRefusal.OriginalThreeGuard;
                    if (!FoodNameIdentity.IsCanonical(name)) return FoodLiveMenuRefusal.Name;
                    if (dynamic.LoadedNames == null || !dynamic.LoadedNames.Contains(name)) return FoodLiveMenuRefusal.NotLoaded;
                    if (dynamic.Resolver.ResolveMealClass(name) != MealClass.Food) return FoodLiveMenuRefusal.MealClass;
                }
                for (int j = 0; j < i; j++) if (string.Equals(name, menu[j], StringComparison.Ordinal)) return FoodLiveMenuRefusal.Duplicate;
            }
            return FoodLiveMenuRefusal.None;
        }
        static partial void TestCode(string product, ref int code);
        static partial void TestProduct(int code, ref string? product);
        public static int Code(string product)
        {
            int code = product == FoodProducts.Croissant ? 1 : product == FoodProducts.Burger ? 2
            : product == FoodProducts.HotDog ? 3 : product == "ba:itemname_donut" ? 4
            : product == "ba:itemname_cupcake" ? 5 : product == "ba:itemname_salad" ? 6 : 0;
            if(code==0) TestCode(product,ref code);
            return code;
        }
        public static string? Product(int code)
        {
            string? product = code == 1 ? FoodProducts.Croissant : code == 2 ? FoodProducts.Burger
            : code == 3 ? FoodProducts.HotDog : code == 4 ? "ba:itemname_donut"
            : code == 5 ? "ba:itemname_cupcake" : code == 6 ? "ba:itemname_salad" : null;
            if(product==null) TestProduct(code,ref product);
            return product;
        }

        // Reserved identifiers establish no equipment, taste/fit/health/quality prior or sale permission.
        // Health and quality remain unknown for all entries. Existing meal fits live in FoodProducts.
        public static int? HealthTag(int code) => null;
        public static int? QualityTag(int code) => null;
        public static int Taste(int seed, string consumerId, int code)
        {
            if (string.IsNullOrEmpty(consumerId) || code < 1 || code > MaximumDefinitions) throw new ArgumentException("Invalid product taste identity.");
            uint hash = StableRandom.Append(StableRandom.Begin(seed), "pc:product-taste:v1|");
            hash = StableRandom.Append(StableRandom.AppendDecimal(hash, consumerId.Length), ':');
            hash = StableRandom.Append(StableRandom.Append(hash, consumerId), '|');
            uint percentile = StableRandom.Finish(StableRandom.AppendDecimal(hash, code)) % 100;
            return percentile < 10 ? 0 : percentile < 30 ? 1 : percentile < 70 ? 2 : percentile < 90 ? 3 : 4;
        }
        public static int Taste(ResidentTraits traits, int seed, string consumer, string product)
        {
            if (product == FoodProducts.Croissant) return traits.Band(ResidentTrait.CroissantTaste);
            if (product == FoodProducts.Burger) return traits.Band(ResidentTrait.BurgerTaste);
            if (product == FoodProducts.HotDog) return traits.Band(ResidentTrait.HotDogTaste);
            int code = Code(product);
            return code == 0 ? ResidentTraits.Neutral : Taste(seed, consumer, code);
        }
    }
}
