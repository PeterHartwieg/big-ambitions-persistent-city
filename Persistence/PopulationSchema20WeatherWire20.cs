using System;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    internal static partial class PopulationSchema20
    {
        private static bool WeatherProperty(Type type,string? name) => type==typeof(CityState) && name=="WeatherStartsDay"
            || type==typeof(CitizenState) && name=="W"
            || type==typeof(FoodDecisionState) && (name=="Weather" || name=="WeatherTravelMultiplier");
        internal static void CheckMemory20(string wire)
        {
            Require(wire.Length>=10 && wire.Length<=50 && wire[wire.Length-2]=='|' && wire[wire.Length-1]>='0' && wire[wire.Length-1]<='2',"Invalid schema-20 consumer weather.");
            int length=wire.Length-2;
            Require((length-8)%10==0 && wire[0]=='1',"Invalid schema-20 memory length/version.");
            if(string.CompareOrdinal(wire,1,"-------",0,7)!=0)
            {
                int day=MemoryWire.Number(wire,1,4),meal=MemoryWire.Number(wire,5,1),context=MemoryWire.Number(wire,6,1),stage=MemoryWire.Number(wire,7,1);
                Require(day<=MemoryWire.MaximumDay && meal<=3 && context>=1 && context<=4 && stage>=1 && stage<=4,"Invalid schema-20 watermark.");
            }
            int previous=-1;
            for(int i=8;i<length;i+=10)
            {
                int business=MemoryWire.Number(wire,i,3),day=MemoryWire.Number(wire,i+3,4),served=MemoryWire.Number(wire,i+7,1),affinity=MemoryWire.Number(wire,i+8,1),outcome=MemoryWire.Number(wire,i+9,1);
                Require(business>previous && business<20000 && day<=MemoryWire.MaximumDay && affinity<=16 && outcome>=1 && outcome<=4 && (outcome!=1 || served>0),"Invalid schema-20 memory/reference.");previous=business;
            }
        }
        private sealed class WeatherConverter : JsonConverter<WeatherCommitment>
        {
            public override void WriteJson(JsonWriter writer,WeatherCommitment? value,JsonSerializer serializer)
            { if(value==null) writer.WriteNull();else writer.WriteValue(value.ToWire()); }
            public override WeatherCommitment? ReadJson(JsonReader reader,Type type,WeatherCommitment? old,bool exists,JsonSerializer serializer)
                => reader.TokenType==JsonToken.Null ? null : WeatherCommitment.Parse((string)reader.Value!);
        }
    }
}
