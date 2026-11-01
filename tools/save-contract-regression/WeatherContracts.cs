using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    // Keep this driver usable against the archived stack; each projection removes only its owned fields.
    private static bool Weather20 => typeof(CityState).GetProperty("WeatherStartsDay")!=null;
    private static string WithoutMenu23(string wire)
    {
        if(typeof(CityState).GetProperty("MenuPolicy")==null) return wire;
        var root=JObject.Parse(wire);root["Schema"]=root["Food"] is JObject f && f["Campaigns"]!=null ? 22 : 20;root.Remove("MenuPolicy");
        return root.ToString(Formatting.None);
    }
    private static string WithoutWeather20(string wire)
    {
        if(!Weather20) return wire;
        var root=JObject.Parse(WithoutCampaigns22(WithoutMenu23(wire)));root["Schema"]=19;root.Remove("WeatherStartsDay");
        foreach(JObject p in root["Citizens"]!) p.Remove("W");
        if(root["Food"] is JObject food) foreach(JArray m in food["Residents"]!)
        {
            string memory=(string)m[35]!;m[35]=memory.Substring(0,memory.Length-2);
            if(m[9] is JObject decision) {decision.Remove("Weather");decision.Remove("WeatherTravelMultiplier");}
        }
        return root.ToString(Formatting.None);
    }
    private static void PopulateWeather20(CityState state)
    {
        if(!Weather20) return;
        var property=typeof(CitizenState).GetProperty("W")!;
        var constructor=property.PropertyType.GetConstructors().Single(c=>c.GetParameters()[2].ParameterType==typeof(bool));
        var weather=Enum.ToObject(constructor.GetParameters()[1].ParameterType,2);
        foreach(var citizen in state.Citizens) property.SetValue(citizen,constructor.Invoke(new object[] {state.Minute,weather,true}));
        foreach(var meal in state.Food!.Residents)
        {
            typeof(ConsumerMemory).GetProperty("Weather")!.SetValue(meal.Memory,weather);
            if(meal.Decision!=null)
            {
                typeof(FoodDecisionState).GetProperty("Weather")!.SetValue(meal.Decision,weather);
                typeof(FoodDecisionState).GetProperty("WeatherTravelMultiplier")!.SetValue(meal.Decision,2.0);
            }
        }
    }
    private static int WeatherRecords20(CityState state) => !Weather20 ? 0 : state.Citizens.Count(c=>typeof(CitizenState).GetProperty("W")!.GetValue(c)!=null);
    private static int WeatherChars20(CityState state)
    {
        if(!Weather20) return 0;
        int count=("\"WeatherStartsDay\":"+typeof(CityState).GetProperty("WeatherStartsDay")!.GetValue(state)+",").Length;
        foreach(var citizen in state.Citizens)
        {
            var capture=typeof(CitizenState).GetProperty("W")!.GetValue(citizen);
            count+=capture==null ? 9 : 22;
        }
        if(state.Food!=null) foreach(var meal in state.Food.Residents)
        {
            count+=2;
            if(meal.Decision==null) continue;
            count+=("\"Weather\":"+Convert.ToInt32(typeof(FoodDecisionState).GetProperty("Weather")!.GetValue(meal.Decision))+",").Length;
            count+=("\"WeatherTravelMultiplier\":"+JsonConvert.SerializeObject(typeof(FoodDecisionState).GetProperty("WeatherTravelMultiplier")!.GetValue(meal.Decision))+",").Length;
        }
        return count;
    }
    private static int MenuHeaderChars23(CityState state) => typeof(CityState).GetProperty("MenuPolicy") is System.Reflection.PropertyInfo menu
        ? ("\"MenuPolicy\":"+JsonConvert.SerializeObject(menu.GetValue(state))+",").Length : 0;
    private static void WeatherContractChecks()
    {
        if(!Weather20) return;
        var state=FoodCity(3,true);PopulateDecisions(state);PopulateWeather20(state);
        string wire=SaveCodec.Write(state),old=WithoutWeather20(wire);
        string schema22=WithoutMenu23(wire),schema20=WithoutCampaigns22(schema22);
        Check(schema20.Length-old.Length==WeatherChars20(state),"Schema-20 additive weather byte ledger mismatch.");
        Check(schema22.Length-schema20.Length==CampaignChars22(state),"Schema-22 campaign byte ledger mismatch.");
        Check(wire.Length-schema22.Length==MenuHeaderChars23(state),"Schema-23 menu header byte ledger mismatch.");
        var from20=SaveCodec.Read(schema20);
        Check(from20.Status==SaveReadStatus.Loaded && from20.SourceSchema==20,"Schema-20 migration refused.");
        Check(WithoutCampaigns22(WithoutMenu23(SaveCodec.Write(from20.State!)))==schema20,"Schema-20 fields changed during menu migration.");
        var migrated=SaveCodec.Read(old);Check(migrated.Status==SaveReadStatus.Loaded && migrated.SourceSchema==19,"Schema-19 migration refused.");
        Check((int)typeof(CityState).GetProperty("WeatherStartsDay")!.GetValue(migrated.State!)! == (int)(state.Minute/1440)+1,"Weather migration did not defer activation.");
        Check(SaveCodec.Write(SaveCodec.Read(wire).State!)==wire,"Weather roundtrip changed terms.");
        var before=SaveCodec.CountRecords(migrated.State!,SaveCodec.Write(migrated.State!));var after=SaveCodec.CountRecords(state,wire);
        Check(after.Streaming-before.Streaming==3 && after.Semantic-before.Semantic==3,"Weather consumer scalars charged a separate record.");
    }
}
