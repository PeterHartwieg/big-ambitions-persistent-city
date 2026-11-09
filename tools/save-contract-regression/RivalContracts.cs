using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    // Never relabel a schema-21 tuple as an older source. X1 removed the frozen schema-20 writer from the
    // current tree; before schema 21 the current writer is that source, after it run against an archived tree.
    private static string PreRivalWire(CityState state)
    {
        if(typeof(FoodCommerceState).GetProperty("RivalStartsDay")==null) return SaveCodec.Write(state);
        throw new NotSupportedException("X1 removed the frozen schema-20 writer; run this comparison against an archived source tree.");
    }
    private static int RivalChars(CityState state)
    {
        var start=typeof(FoodCommerceState).GetProperty("RivalStartsDay");if(start==null) return 0;
        int chars=("\"RivalStartsDay\":"+start.GetValue(state.Food)+",").Length;
        foreach(var meal in state.Food!.Residents)
        {
            chars+=JsonConvert.SerializeObject(typeof(ResidentMeal).GetProperty("External")!.GetValue(meal)).Length+1;
            if(meal.Budget!=null)
            {
                var money=(Money)typeof(ResidentBudget).GetProperty("ExternalFoodSpent")!.GetValue(meal.Budget)!;
                chars+=money.Units.ToString(CultureInfo.InvariantCulture).Length+1;
            }
            else
            {
                var money=(Money)typeof(ResidentMeal).GetProperty("LegacyExternalFoodSpent")!.GetValue(meal)!;
                chars+=money.Units.ToString(CultureInfo.InvariantCulture).Length-4; // old null -> scalar variant
            }
            if(meal.Decision==null) continue;
            foreach(var shop in meal.Decision.Shops)
            foreach(string name in new[]{"AlternativeKind","RivalTerms","RivalServiceMinutes","AlternativeWeight"})
                chars+=(JsonConvert.ToString(name)+":"+JsonConvert.SerializeObject(typeof(FoodDecisionShop).GetProperty(name)!.GetValue(shop))+",").Length;
        }
        // The candidate-kind fields are already in AwarenessChars; no second charge.
        return chars;
    }
}
