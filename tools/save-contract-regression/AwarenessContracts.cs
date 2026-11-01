using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    // Reflection keeps the same capacity driver usable against B0b. Only the new held image
    // and one activation header consume the existing 1 MiB decision/64 KiB header reserves.
    private static void PopulateAwareness(FoodDecisionState decision)
    {
        var property=typeof(FoodDecisionState).GetProperty("Awareness");if(property==null) return;
        var union=new JArray();
        for(int i=0;i<24;i++)
        {
            string id=i<8 ? decision.Shops[i].BusinessId : "zz:awareness:"+i.ToString("D2")+new string('X',497);
            union.Add(new JObject { ["BusinessId"]=id,["Eligible"]=true,["Sources"]=i<4 ? 1 : i<8 ? 2 : i<12 ? 4 : i<16 ? 8 : 16 });
        }
        var image=new JObject { ["CatalogueRevision"]=Convert.ToBase64String(new byte[32]),["Union"]=union };
        property.SetValue(decision,image.ToObject(property.PropertyType));
    }
    private static int AwarenessChars(CityState state)
    {
        var start=typeof(FoodCommerceState).GetProperty("AwarenessStartsDay");if(start==null) return 0;
        int bytes=("\"AwarenessStartsDay\":"+start.GetValue(state.Food)+",").Length;
        var held=state.Food!.Residents.FirstOrDefault(m=>m.Decision!=null)?.Decision;
        if(held!=null) bytes+=("\"Awareness\":"+JsonConvert.SerializeObject(typeof(FoodDecisionState).GetProperty("Awareness")!.GetValue(held))+",").Length;
        return bytes;
    }
    private static int AwarenessRecords(CityState state)
    {
        var image=state.Food!.Residents.FirstOrDefault(m=>m.Decision!=null)?.Decision;
        if(image==null || typeof(FoodDecisionState).GetProperty("Awareness")==null) return 0;
        var token=JObject.FromObject(image)["Awareness"];
        return token==null || token.Type==JTokenType.Null ? 0 : token["Union"]!.Count();
    }
    private static void AwarenessContractChecks()
    {
        if(typeof(FoodDecisionState).GetProperty("Awareness")==null) return;
        var state=FoodCity(3,true);PopulateDecisions(state);
        string wire=SaveCodec.Write(state);var root=JObject.Parse(WithoutWeather20(wire));root["Schema"]=18;
        ((JObject)root["Food"]!).Remove("AwarenessStartsDay");((JObject)root["Food"]!["Residents"]![0]![9]!).Remove("Awareness");
        string baseline=root.ToString(Formatting.None);
        Check(WithoutWeather20(wire).Length-baseline.Length==AwarenessChars(state),"Awareness byte allocation mismatch.");
        var old=SaveCodec.Read(baseline);Check(old.Status==SaveReadStatus.Loaded && old.SourceSchema==18,"Schema-18 migration refused.");
        var current=SaveCodec.Read(wire);Check(current.Status==SaveReadStatus.Loaded && SaveCodec.Write(current.State!)==wire,"Awareness held image replay changed.");
        var before=SaveCodec.CountRecords(old.State!,SaveCodec.Write(old.State!));var after=SaveCodec.CountRecords(state,wire);
        Check(after.Streaming-before.Streaming==24 && after.Semantic-before.Semantic==24,"Awareness source records not charged exactly once.");
    }
}
