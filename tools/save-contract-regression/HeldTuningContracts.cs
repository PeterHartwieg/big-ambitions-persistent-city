using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    private static void HeldTuningContractChecks()
    {
        // Keep the measurement tool compilable against archived B0 sources. This is an
        // intentional in-place schema-18 amendment; it changes held-image bytes, not economy facts.
        if (typeof(FoodDecisionState).GetProperty("Tuning") == null) return;
        var state = FoodCity(3, true); PopulateDecisions(state);
        string wire = SaveCodec.Write(state); var root = JObject.Parse(wire);
        var decision = (JObject)root["Food"]!["Residents"]![0]![9]!;
        string image = (string)decision["Tuning"]!;
        Check(image.Length == 193, "Held image binary64 arity changed.");
        decision.Remove("Tuning"); foreach (JObject shop in decision["Shops"]!) shop.Remove("MemoryContribution");
        string previous = root.ToString(Formatting.None);
        Check(wire.Length - previous.Length == 405, "Held tuning/neutral memory byte allocation changed.");
        var migrated = SaveCodec.Read(previous); Check(migrated.Status == SaveReadStatus.Loaded, "Pre-image 18 refused.");
        Check(SaveCodec.CountRecords(state, wire) == SaveCodec.CountRecords(migrated.State!, previous), "Scalar held inputs changed record counting.");
        foreach (string bad in new[] { image + "0", "2" + image.Substring(1), "1" + "7FF8000000000000" + image.Substring(17) }) {
            decision["Tuning"] = bad; PreserveB0(root.ToString(Formatting.None), state);
        }
        decision["Tuning"] = image;
        // B1 deliberately lifts B0b's neutral-only semantic gate. No wire fields,
        // tuple arities, record charges or frozen source resolvers change.
        foreach (JObject shop in decision["Shops"]!) shop["MemoryContribution"] = -4.605170185988091;
        string nonzero = root.ToString(Formatting.None);
        var accepted = SaveCodec.Read(nonzero);
        Check(accepted.Status == SaveReadStatus.Loaded, "B1 bounded nonzero contribution refused.");
        Check(nonzero.Length - previous.Length <= 589, "B1 exceeded B0b scalar allowance.");
        Check(SaveCodec.CountRecords(accepted.State!, nonzero) == SaveCodec.CountRecords(state, wire), "B1 scalar changed record counting.");
        foreach (JObject shop in decision["Shops"]!) shop["MemoryContribution"] = 0.0;
        decision["Shops"]![0]!["MemoryContribution"] = 8.01;
        PreserveB0(root.ToString(Formatting.None), state);
    }
}
