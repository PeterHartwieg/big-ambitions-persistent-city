using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    // Keep archived-source capacity comparisons usable; never relabel a schema-21
    // tuple as an older source. The actual frozen reader/writer owns its shape.
    private static string PreRivalWire(CityState state)
    {
        var predecessor=typeof(SaveCodec).Assembly.GetType("PersistentCity.Persistence.PopulationSchema19");
        if(predecessor==null) return SaveCodec.Write(state);
        int schema=state.Schema;
        try {state.Schema=19;return (string)predecessor.GetMethod("Write")!.Invoke(null,new object[]{state})!;}
        finally {state.Schema=schema;}
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
