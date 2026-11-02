using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

static partial class Program
{
    // Archived-source comparisons compile this driver against earlier Core/Persistence too.
    // Projection removes only E5d's additions; every older source still uses its frozen reader.
    private static string WithoutNames25(string wire)
    {
        var root = JObject.Parse(wire);
        if ((int)root["Schema"]! < 25) return wire;
        root["Schema"] = 24;
        root["MenuPolicy"] = "1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad";
        root.Remove("ProductNames"); root.Remove("NamePolicyStartsDay");
        if (root["Food"] is JObject food) foreach (JArray meal in food["Residents"]!)
            if (meal[9] is JObject decision) foreach (JObject shop in decision["Shops"]!)
                foreach (JObject offer in shop["Offers"]!) offer.Remove("Metadata");
        return root.ToString(Formatting.None);
    }
}
