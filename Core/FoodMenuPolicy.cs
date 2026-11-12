using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public enum FoodLiveMenuRefusal
    {
        None, Empty, Width, Duplicate, Name,
        OriginalThreeGuard,
        NotLoaded,
        MealClass
    }

    public sealed class FoodLivePolicy
    {
        private FoodProductPolicy? published;
        public FoodProductPolicy Resolve(FoodProductPolicy current, bool catalogueRebuilding)
        {
            if (current.LoadedNames != null) { published = current; return current; }
            return catalogueRebuilding && published != null ? published : current;
        }
        public void Clear() => published = null;
    }

    public static partial class FoodMenuPolicy
    {
        public const int MaximumOffers = 8;
        public const int LiveMaximumOffers = MaximumOffers;
        public const int MaximumDefinitions = 16;
        public const int Version = 1;
        public const string Wire = "1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad";
        public static FoodLiveMenuRefusal LiveMenu(IReadOnlyList<string>? menu, FoodProductPolicy? dynamic)
            => LiveMenu(menu, dynamic, false);
        public static FoodLiveMenuRefusal LiveMenu(IReadOnlyList<string>? menu, FoodProductPolicy? dynamic, bool loadedNamesPending)
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
                    if (dynamic.LoadedNames == null ? !loadedNamesPending : !dynamic.LoadedNames.Contains(name)) return FoodLiveMenuRefusal.NotLoaded;
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
