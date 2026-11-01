// Test-only definitions, compiled ONLY by E4MenuRegression. Never native IDs or sale registrations.
using System.Collections.Generic;
namespace PersistentCity.Core
{
    public static partial class FoodChoicePolicyBefore
    {
        public const int MaximumShortlist=8;
        public const int MaximumOffersPerShop=4;
    }
    public static partial class FoodProducts
    {
        static partial void AddTestFits(Dictionary<string,int[]> fits)
        { for(int code=7;code<=14;code++) fits.Add(FoodMenuPolicy.Product(code)!,new[]{0,0,0}); }
    }
    public static partial class FoodMenuPolicy
    {
        private static readonly string[] TestIds={"test:e4:7","test:e4:8","test:e4:9","test:e4:10","test:e4:11","test:e4:12","test:e4:13","test:e4:14"};
        static partial void TestCode(string product,ref int code)
        { for(int i=0;i<TestIds.Length;i++) if(product==TestIds[i]) {code=i+7;return;} }
        static partial void TestProduct(int code,ref string? product)
        { if(code>=7 && code<=14) product=TestIds[code-7]; }
    }
}
