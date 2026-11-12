using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
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
        static partial void AddTestFits(Dictionary<string, int[]> fits);
        static FoodProducts() { AddTestFits(Fits); }
        public static IEnumerable<string> Ids => Fits.Keys;
        public static bool Known(string product) => Fits.ContainsKey(product);
        public static bool KnownLegacy(string product) => product==Croissant || product==Burger || product==HotDog;
        public static int Penalty(MealKind kind, string product) => kind == MealKind.Legacy ? 0
            : Fits.TryGetValue(product, out var fit) ? fit[(int)kind - 1] : 0;
    }
}
