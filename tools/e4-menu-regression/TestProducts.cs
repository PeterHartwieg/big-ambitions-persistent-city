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
        { for(int code=7;code<=14;code++) fits.Add("test:e4:"+code,new[]{0,0,0}); }
    }
    public static partial class FoodMenuPolicy
    {
        static partial void TestCode(string product,ref int code)
        { for(int i=7;i<=14;i++) if(product=="test:e4:"+i) {code=i;return;} }
        static partial void TestProduct(int code,ref string? product)
        { if(code>=7 && code<=14) product="test:e4:"+code; }
    }
}
