using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

internal static class LegacyMoneyWire
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OpeningBalance"] = "OpeningBalanceCents", ["Balance"] = "BalanceCents", ["IncomeReceived"] = "IncomeReceivedCents",
        ["EssentialsPaid"] = "EssentialsPaidCents", ["HomeFoodPaid"] = "HomeFoodPaidCents", ["PreparedFoodSpent"] = "PreparedFoodSpentCents",
        ["HeldPurchase"] = "HeldPurchaseCents", ["Arrears"] = "ArrearsCents", ["DayAllowanceUsed"] = "DayAllowanceUsedCents",
        ["Allowance"] = "AllowanceCents", ["DailyAllowance"] = "DailyAllowanceCents", ["Price"] = "PriceCents",
        ["Spent"] = "SpentCents", ["Revenue"] = "RevenueCents",
    };

    public static JObject ToLegacy(JObject token, bool wholeCents)
    {
        foreach (var property in token.Descendants().OfType<JProperty>().Where(p => Names.ContainsKey(p.Name)).ToArray())
        {
            var money = Money.FromUnits((long)property.Value);
            JToken cents;
            if (wholeCents) { Assert.True(money.IsWholeCents()); cents = new JValue(money.Units / Money.UnitsPerCent); }
            else { string text = money.ToCentsText(); cents = new JRaw(text.Contains('.') ? text : text + ".0"); }
            property.Replace(new JProperty(Names[property.Name], cents));
        }
        return token;
    }

    public static JObject AsSchema(JObject token, int schema)
    {
        token["Schema"] = schema;
        foreach (var citizen in token["Citizens"]!.OfType<JObject>())
        {
            if (schema < 15) citizen.Remove("OptionalStop");
            if (schema < 16) citizen.Remove("OptionalStopChoice");
            if (schema < 17) citizen.Remove("Traits");
        }
        if (schema < 17 && token["Food"] is JObject food)
        {
            food.Remove("ChoicePolicyVersion"); food.Remove("ChoiceStartsDay");
            foreach (var meal in food["Residents"]!.OfType<JObject>())
                foreach (string name in Schema17MealFields) meal.Remove(name);
        }
        return token;
    }

    public static readonly string[] Schema17MealFields = { "RepresentedTraits", "Today", "Decision", "LastContextKey", "ContextAttempts" };

    public static string WithoutUpgradeFacts(string payload)
    {
        var token = SaveWire.Parse(payload);
        if (token["Food"] is JObject food)
        {
            food.Remove("ChoiceStartsDay");
            foreach (var meal in food["Residents"]!.OfType<JObject>()) meal.Remove("Today");
        }
        return token.ToString(Newtonsoft.Json.Formatting.None);
    }
}
