using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;

static partial class Program
{
    private static int CampaignChars22(CityState state) => state.Food==null ? 0 : ("\"Campaigns\":"+JsonConvert.SerializeObject(state.Food.Campaigns.Select(i=>i.Encode()))+",").Length;
    private static int CampaignRecords22(CityState state) => state.Food?.Campaigns.Count ?? 0;
}
