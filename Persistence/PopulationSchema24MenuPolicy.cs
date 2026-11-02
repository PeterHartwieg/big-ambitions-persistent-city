using System;
using PersistentCity.Core;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Persistence
{
    /// <summary>Schema 23 envelope, separate from native sale permission. Append codes; never renumber.</summary>
    internal static class PopulationSchema24MenuPolicy
    {
        public const int MaximumOffers = 8;
        public const int LiveMaximumOffers = 4;
        public const int MaximumDefinitions = 16;
        public const int Version = 1;
        public const string Wire = "1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad";
        public static int Code(string product)
        {
            int code = product == FoodProducts.Croissant ? 1 : product == FoodProducts.Burger ? 2
            : product == FoodProducts.HotDog ? 3 : product == "ba:itemname_donut" ? 4
            : product == "ba:itemname_cupcake" ? 5 : product == "ba:itemname_salad" ? 6 : 0;
            return code;
        }
        public static string? Product(int code)
        {
            string? product = code == 1 ? FoodProducts.Croissant : code == 2 ? FoodProducts.Burger
            : code == 3 ? FoodProducts.HotDog : code == 4 ? "ba:itemname_donut"
            : code == 5 ? "ba:itemname_cupcake" : code == 6 ? "ba:itemname_salad" : null;
            return product;
        }

    }
}
