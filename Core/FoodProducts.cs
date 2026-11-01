using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Provisional meal suitability, in generalized-cost cents. These are explicit
    /// gameplay priors, not nutritional values or a target sales mix. Native eligibility is
    /// separately limited to direct stocked products with a verified checkout contract.</summary>
    public static partial class FoodProducts
    {
        public const string Croissant = "ba:itemname_croissant";
        public const string Burger = "ba:itemname_burger";
        public const string HotDog = "ba:itemname_hotdog";
        private static readonly Dictionary<string, int[]> Fits = new Dictionary<string, int[]>(StringComparer.Ordinal) {
            [Croissant] = new[] { 0, 350, 1200 },
            [Burger] = new[] { 1200, 100, 0 },
            [HotDog] = new[] { 900, 0, 450 }
        };
        // Implemented only by the offline envelope harness; absent from production compilation.
        static partial void AddTestFits(Dictionary<string, int[]> fits);
        static FoodProducts() { AddTestFits(Fits); }
        public static IEnumerable<string> Ids => Fits.Keys;
        public static bool Known(string product) => Fits.ContainsKey(product);
        public static bool KnownLegacy(string product) => product==Croissant || product==Burger || product==HotDog;
        public static int Penalty(MealKind kind, string product) => kind == MealKind.Legacy ? 0
            : Fits.TryGetValue(product, out var fit) ? fit[(int)kind - 1] : 0;
    }
}
