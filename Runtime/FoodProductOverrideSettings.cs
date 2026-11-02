using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    // Diagnostics belong to the load, never to Resolve/Weight. No raw names/values in logs.
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
                if (InvalidEntry) report("FoodProductOverrides: invalid entry/field defaulted (new meal class Unknown).");
                if (AdjustedField) report("FoodProductOverrides: numeric fields clamped/defaulted (taste 0-4; fit 0-5000).");
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

    /// <summary>Streaming, bounded optional table; duplicate properties are observed before Json.NET can replace them.</summary>
    internal sealed class FoodProductOverrideConverter : JsonConverter<FoodProductOverrideTable>
    {
        private sealed class Budget
        {
            private long escaped;
            internal bool Read(JsonReader reader)
            {
                if (!reader.Read()) throw new JsonSerializationException("Incomplete food metadata table.");
                // Conservative escaped-JSON and UTF-8 charge, including quotes/separators. Each
                // UTF-16 code unit needs at most six escaped ASCII bytes. No reserialized table.
                escaped += reader.Value is string text ? 6L*text.Length+4 : 32;
                return escaped <= FoodProductMetadataResolver.MaximumTableCharactersAndBytes;
            }
        }
        public override FoodProductOverrideTable ReadJson(JsonReader reader, Type type, FoodProductOverrideTable? old, bool exists, JsonSerializer serializer)
        {
            var result=new FoodProductOverrideTable();
            if (reader.TokenType!=JsonToken.StartObject) { reader.Skip(); result.InvalidTable=true; return result; }
            int rootDepth=reader.Depth;
            var entries=new Dictionary<string,FoodProductMetadata>(StringComparer.Ordinal);
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
                var neutral=FoodProductMetadataResolver.Neutral;
                MealClass mealClass=neutral.MealClass;
                int taste=neutral.TasteBand, breakfast=neutral.BreakfastFitCents, lunch=neutral.LunchFitCents, dinner=neutral.DinnerFitCents;
                if (reader.TokenType!=JsonToken.StartObject)
                {
                    invalid=true;
                    if (!ConsumeValue(reader,budget)) return Reject(reader,rootDepth,result);
                }
                else
                {
                    var fields=new HashSet<string>(StringComparer.Ordinal);
                    while (true)
                    {
                        if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                        if (reader.TokenType==JsonToken.EndObject) break;
                        if (reader.TokenType!=JsonToken.PropertyName) throw new JsonSerializationException("Invalid food metadata entry.");
                        string field=(string)reader.Value!;
                        if (!fields.Add(field)) invalid=true;
                        if (!budget.Read(reader)) return Reject(reader,rootDepth,result);
                        switch (field)
                        {
                            case "MealClass":
                                string? word=reader.TokenType==JsonToken.String ? (string?)reader.Value : null;
                                mealClass=word=="Food" ? MealClass.Food : word=="Drink" ? MealClass.Drink : word=="Bundle" ? MealClass.Bundle
                                    : word=="NonFood" ? MealClass.NonFood : MealClass.Unknown;
                                if (word!="Unknown" && word!="Food" && word!="Drink" && word!="Bundle" && word!="NonFood") result.InvalidEntry=true;
                                break;
                            case "TasteBand": taste=Number(reader,2,4,result); break;
                            case "BreakfastFitCents": breakfast=Number(reader,900,5000,result); break;
                            case "LunchFitCents": lunch=Number(reader,100,5000,result); break;
                            case "DinnerFitCents": dinner=Number(reader,450,5000,result); break;
                            default: invalid=true; break;
                        }
                        if (!ConsumeValue(reader,budget)) return Reject(reader,rootDepth,result);
                    }
                }
                if (invalid) { result.InvalidEntry=true; mealClass=neutral.MealClass; taste=2; breakfast=900; lunch=100; dinner=450; }
                if (FoodProducts.KnownLegacy(name!)) result.IgnoredOriginal=true;
                entries.Add(name!,new FoodProductMetadata(mealClass,taste,breakfast,lunch,dinner));
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
            // Drain only this optional value; unrelated top-level settings still deserialize.
            while (!(reader.TokenType==JsonToken.EndObject && reader.Depth==rootDepth))
            {
                if (!reader.Read()) throw new JsonSerializationException("Incomplete food metadata table.");
                reader.Skip();
            }
            return result;
        }
        private static int Number(JsonReader reader, int fallback, int maximum, FoodProductOverrideTable result)
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
            return fallback;
        }
        public override void WriteJson(JsonWriter writer, FoodProductOverrideTable? value, JsonSerializer serializer)
        {
            // Supports settings diagnostics/roundtrip tools; the loader never writes the file.
            writer.WriteStartObject();
            if (value!=null)
            {
                var traits=new ResidentTraits {Bands="2222222222"};
                foreach (string name in value.Resolver.Names.OrderBy(n=>n,StringComparer.Ordinal))
                {
                    var m=value.Resolver.Resolve(name,traits);
                    writer.WritePropertyName(name); writer.WriteStartObject();
                    writer.WritePropertyName("MealClass"); writer.WriteValue(m.MealClass.ToString());
                    writer.WritePropertyName("TasteBand"); writer.WriteValue(m.TasteBand);
                    writer.WritePropertyName("BreakfastFitCents"); writer.WriteValue(m.BreakfastFitCents);
                    writer.WritePropertyName("LunchFitCents"); writer.WriteValue(m.LunchFitCents);
                    writer.WritePropertyName("DinnerFitCents"); writer.WriteValue(m.DinnerFitCents);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
        }
    }
}
