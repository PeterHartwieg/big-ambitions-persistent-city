using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

static partial class Program
{
    // Reflection keeps the same driver usable with archived Core/Persistence sources.
    private static object? Outflow24(CityState state) => state.Food == null ? null
        : typeof(FoodCommerceState).GetProperty("Outflow")?.GetValue(state.Food);
    private static int OutflowChars24(CityState state) => typeof(FoodCommerceState).GetProperty("Outflow") == null || state.Food == null ? 0
        : ("\"Outflow\":" + JsonConvert.SerializeObject(Outflow24(state)?.GetType().GetMethod("ToWire")!.Invoke(Outflow24(state), null)) + ",").Length;
    private static int OutflowRecords24(CityState state) => Outflow24(state) is object history
        ? (int)history.GetType().GetProperty("RecordCount")!.GetValue(history)! : 0;
    private static string WithoutOutflow24(string wire)
    {
        var root = JObject.Parse(wire);
        if ((int)root["Schema"]! < 24) return wire;
        root["Schema"] = 23;
        if (root["Food"] is JObject food) food.Remove("Outflow");
        return root.ToString(Formatting.None);
    }
}
