using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

static partial class Program
{
    private static string WithoutCampaigns22(string wire)
    {
        var root=JObject.Parse(wire);
        if(root["Food"] is JObject food) food.Remove("Campaigns");
        root["Schema"]=21;
        return root.ToString(Formatting.None);
    }
    private static int CampaignChars22(CityState state) => state.Food==null ? 0 : ("\"Campaigns\":"+JsonConvert.SerializeObject(state.Food.Campaigns.Select(i=>i.Encode()))+",").Length;
    private static int CampaignRecords22(CityState state) => state.Food?.Campaigns.Count ?? 0;
}
