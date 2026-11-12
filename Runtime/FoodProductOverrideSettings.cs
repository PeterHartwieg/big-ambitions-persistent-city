using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed partial class PrototypeSettings
    {
        private FoodProductOverrideTable productOverrides = new FoodProductOverrideTable();
        private bool productOverridesSeen;
        [JsonConverter(typeof(FoodProductOverrideConverter))]
        public FoodProductOverrideTable FoodProductOverrides
        {
            get => productOverrides;
            set
            {
                if (productOverridesSeen) productOverrides = new FoodProductOverrideTable { InvalidTable=true };
                else productOverrides = value ?? new FoodProductOverrideTable { InvalidTable=true };
                productOverridesSeen=true;
            }
        }
    }

    internal sealed class FoodProductOverrideTable
    {
        internal FoodProductMetadataResolver Resolver = FoodProductMetadataResolver.Empty;
        internal bool InvalidTable, InvalidEntry, AdjustedField, IgnoredOriginal;
        private bool reported, unloadedReported;
        internal void ReportOnce(Action<string> report)
        {
            if (reported) return;
            reported=true;
            if (InvalidTable) report("FoodProductOverrides rejected: duplicate/invalid key, type or table bound; using empty metadata table.");
            else
            {
                if (InvalidEntry) report("FoodProductOverrides: invalid entry/field absent; using API metadata before defaults.");
                if (AdjustedField) report("FoodProductOverrides: numeric fields clamped/absent (taste 0-4; fit 0-5000); absent fields use API before defaults.");
                if (IgnoredOriginal) report("FoodProductOverrides: original-three overrides ignored; saved tastes and established food fits preserved.");
            }
        }
        // E5a/E5b must supply an already detached loaded-name image at load/publication, not per frame.
        // Pure E5c cannot infer loaded names, so this diagnostic seam is deliberately not polled.
        internal void ReportUnloadedOnce(ISet<string> loadedNames, Action<string> report)
        {
            if (unloadedReported) return;
            unloadedReported=true;
            int absent=0;
            foreach (string name in Resolver.Names) if (!loadedNames.Contains(name)) absent++;
            if (absent>0) report("FoodProductOverrides: " + absent.ToString(CultureInfo.InvariantCulture) + " unloaded keys are inert metadata; no product registration or eligibility.");
        }
    }

    internal sealed class FoodProductOverrideConverter : JsonConverter<FoodProductOverrideTable>
    {
        private sealed class Budget
        {
            private long escaped, utf8;
            internal bool Read(JsonReader reader)
            {
                if (!reader.Read()) throw new JsonSerializationException("Incomplete food metadata table.");
                // Charge canonical escaped JSON and UTF-8 independently, including punctuation.
                // One spare separator per token makes the bound conservative without serializing
                // or retaining a second table. Raw source whitespace is not retained metadata.
                if (reader.Value is string text)
                {
                    escaped+=text.Length+3; utf8+=Encoding.UTF8.GetByteCount(text)+3;
                    foreach(char c in text)
                    {
                        int extra=c=='"' || c=='\\' || c=='\b' || c=='\t' || c=='\n' || c=='\f' || c=='\r' ? 1
                            : c<32 || c=='\u0085' || c=='\u2028' || c=='\u2029' ? 5 : 0;
                        escaped+=extra;
                        utf8+=extra-(c=='\u0085' ? 1 : c=='\u2028' || c=='\u2029' ? 2 : 0);
                    }
                }
                else
                {
                    int charge=reader.Value is System.Numerics.BigInteger integer
                        ? Math.Max(32,integer.ToString(CultureInfo.InvariantCulture).Length+1) : 32;
                    escaped+=charge; utf8+=charge;
                }
                return escaped <= FoodProductMetadataResolver.MaximumTableCharactersAndBytes
                    && utf8 <= FoodProductMetadataResolver.MaximumTableCharactersAndBytes;
            }
        }
        public override FoodProductOverrideTable ReadJson(JsonReader reader, Type type, FoodProductOverrideTable? old, bool exists, JsonSerializer serializer)
        {
            var result=new FoodProductOverrideTable();
            if (reader.TokenType!=JsonToken.StartObject) { reader.Skip(); result.InvalidTable=true; return result; }
            int rootDepth=reader.Depth;
            var entries=new Dictionary<string,FoodProductMetadataOverride>(StringComparer.Ordinal);
            var budget=new Budget();
            while (true)
            {
                if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                if (reader.TokenType==JsonToken.EndObject) break;
                string? name=reader.Value as string;
                if (reader.TokenType!=JsonToken.PropertyName || !FoodProductMetadataResolver.ValidName(name)
                    || entries.Count==FoodProductMetadataResolver.MaximumOverrides || entries.ContainsKey(name!))
                    return Reject(reader,rootDepth,result);
                if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                bool invalid=false;
                MealClass? mealClass=null;
                int? taste=null, breakfast=null, lunch=null, dinner=null;
                if (reader.TokenType!=JsonToken.StartObject)
                {
                    invalid=true;
                    if (!ConsumeValue(reader,budget)) return Reject(reader,rootDepth,result);
                }
                else
                {
                    int fields=0;
                    while (true)
                    {
                        if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                        if (reader.TokenType==JsonToken.EndObject) break;
                        if (reader.TokenType!=JsonToken.PropertyName) throw new JsonSerializationException("Invalid food metadata entry.");
                        string field=(string)reader.Value!;
                        int bit=field=="MealClass" ? 1 : field=="TasteBand" ? 2 : field=="BreakfastFitCents" ? 4
                            : field=="LunchFitCents" ? 8 : field=="DinnerFitCents" ? 16 : 0;
                        if (bit==0 || (fields&bit)!=0) invalid=true;
                        fields|=bit;
                        if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                        switch (field)
                        {
                            case "MealClass":
                                string? word=reader.TokenType==JsonToken.String ? (string?)reader.Value : null;
                                mealClass=word=="Food" ? MealClass.Food : word=="Drink" ? MealClass.Drink : word=="Bundle" ? MealClass.Bundle
                                    : word=="NonFood" ? MealClass.NonFood : word=="Unknown" ? MealClass.Unknown : (MealClass?)null;
                                if (word!="Unknown" && word!="Food" && word!="Drink" && word!="Bundle" && word!="NonFood") result.InvalidEntry=true;
                                break;
                            case "TasteBand": taste=Number(reader,4,result); break;
                            case "BreakfastFitCents": breakfast=Number(reader,5000,result); break;
                            case "LunchFitCents": lunch=Number(reader,5000,result); break;
                            case "DinnerFitCents": dinner=Number(reader,5000,result); break;
                            default: invalid=true; break;
                        }
                        if (!ConsumeValue(reader,budget)) return Reject(reader,rootDepth,result);
                    }
                }
                if (invalid) { result.InvalidEntry=true; mealClass=null; taste=null; breakfast=null; lunch=null; dinner=null; }
                if (FoodProducts.KnownLegacy(name!)) result.IgnoredOriginal=true;
                entries.Add(name!,new FoodProductMetadataOverride(mealClass,taste,breakfast,lunch,dinner));
            }
            result.Resolver=new FoodProductMetadataResolver(entries);
            return result;
        }
        private static bool ConsumeValue(JsonReader reader, Budget budget)
        {
            if (reader.TokenType!=JsonToken.StartObject && reader.TokenType!=JsonToken.StartArray) return true;
            int depth=reader.Depth;
            do { if (!budget.Read(reader)) return false; }
            while (reader.Depth>depth);
            return true;
        }
        private static FoodProductOverrideTable Reject(JsonReader reader, int rootDepth, FoodProductOverrideTable result)
        {
            result.InvalidTable=true;
            while (!(reader.TokenType==JsonToken.EndObject && reader.Depth==rootDepth))
            {
                if (!reader.Read()) throw new JsonSerializationException("Incomplete food metadata table.");
                reader.Skip();
            }
            return result;
        }
        private static int? Number(JsonReader reader, int maximum, FoodProductOverrideTable result)
        {
            if ((reader.TokenType==JsonToken.Integer || reader.TokenType==JsonToken.Float || reader.TokenType==JsonToken.String)
                && double.TryParse(Convert.ToString(reader.Value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out double number)
                && !double.IsNaN(number))
            {
                if (number<0) { result.AdjustedField=true; return 0; }
                if (number>maximum) { result.AdjustedField=true; return maximum; }
                if (number==Math.Truncate(number)) return (int)number;
            }
            result.AdjustedField=true;
            return null;
        }
        public override void WriteJson(JsonWriter writer, FoodProductOverrideTable? value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            if (value!=null)
            {
                foreach (string name in value.Resolver.Names.OrderBy(n=>n,StringComparer.Ordinal))
                {
                    value.Resolver.TryGetOverride(name,out var m);
                    writer.WritePropertyName(name); writer.WriteStartObject();
                    if (m.OptionalMealClass.HasValue) { writer.WritePropertyName("MealClass"); writer.WriteValue(m.OptionalMealClass.Value.ToString()); }
                    if (m.TasteBand.HasValue) { writer.WritePropertyName("TasteBand"); writer.WriteValue(m.TasteBand.Value); }
                    if (m.OptionalBreakfastFitCents.HasValue) { writer.WritePropertyName("BreakfastFitCents"); writer.WriteValue(m.OptionalBreakfastFitCents.Value); }
                    if (m.OptionalLunchFitCents.HasValue) { writer.WritePropertyName("LunchFitCents"); writer.WriteValue(m.OptionalLunchFitCents.Value); }
                    if (m.OptionalDinnerFitCents.HasValue) { writer.WritePropertyName("DinnerFitCents"); writer.WriteValue(m.OptionalDinnerFitCents.Value); }
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
        }
    }
}
