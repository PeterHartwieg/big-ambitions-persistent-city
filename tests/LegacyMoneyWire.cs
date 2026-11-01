using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>Rewrites current unit-count money fields as the earlier "...Cents" wire fields:
/// whole cents (city schema 13, shadow schemas 1-4) or the decimal cents that earlier schema-14 and
/// shadow-5 writers produced (System.Decimal tokens, whole amounts written with ".0").</summary>
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
            // Raw text: a long exact amount can exceed System.Decimal's 28-29 significant digits.
            else { string text = money.ToCentsText(); cents = new JRaw(text.Contains('.') ? text : text + ".0"); }
            property.Replace(new JProperty(Names[property.Name], cents));
        }
        return token;
    }

    /// <summary>Labels a current payload as an earlier city schema, dropping the fields that schema did
    /// not have (optional stops arrived in 15, saved visit choices in 16, traits, current-day outcomes,
    /// the decision contract and the food choice policy header in 17).</summary>
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

    /// <summary>A schema-17 payload without the facts a 13-16 upgrade synthesizes rather than reads: the policy
    /// activation day and the current-day outcomes. Everything else must survive an upgrade byte for byte.</summary>
    public static string WithoutUpgradeFacts(string payload)
    {
        var token = JObject.Parse(payload);
        if (token["Food"] is JObject food)
        {
            food.Remove("ChoiceStartsDay");
            foreach (var meal in food["Residents"]!.OfType<JObject>()) meal.Remove("Today");
        }
        return token.ToString(Newtonsoft.Json.Formatting.None);
    }
}
