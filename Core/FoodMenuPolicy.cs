using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Schema 23 envelope, separate from native sale permission. Append codes; never renumber.</summary>
    public static partial class FoodMenuPolicy
    {
        public const int MaximumOffers = 8;
        public const int LiveMaximumOffers = 4;
        public const int MaximumDefinitions = 16;
        public const int Version = 1;
        public const string Wire = "1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad";
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
