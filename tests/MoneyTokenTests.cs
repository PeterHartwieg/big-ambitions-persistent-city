using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// Round-8 regressions. Each failed at 57cb8c1, except the exact-boundary posting control, and
// passes after the fix; see the FIX report.
public sealed partial class FoodCommerceTests
{
    private static (CitySimulator city, string current) R8_Paid(float price)
    {
        var (city, market, routes) = Ready();
        market.Price = Money.FromNative(price);
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        Assert.Equal(MealStatus.Paid, city.State.Food.Residents.Single().Status);
        return (city, SaveCodec.Write(city.State));
    }

    private static string R8_Generation(string current, string generation)
    {
        if (generation == "current") return current;
        var token = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(current), wholeCents: generation == "schema13"),
            generation == "schema13" ? 13 : 14);
        return token.ToString(Formatting.None);
    }

    // Both reviewers: Json.NET reads 0x.. as hexadecimal, and only the trailing digits were validated,
    // so a current field loaded the hex value and a migrated field changed amount. The whole token
    // must be canonical in every field generation, and the refused payload must stay in place.
    // The fixture holds no purchase, so each damaged value would otherwise pass every validation.
    [Theory]
    [InlineData("current", "Allowance", "0x20")] [InlineData("current", "Allowance", "0X20")]
    [InlineData("current", "Allowance", "0x0")] [InlineData("current", "Arrears", "0x10")]
    [InlineData("schema13", "AllowanceCents", "0x1")] [InlineData("schema13", "ArrearsCents", "0x10")]
    [InlineData("schema13", "AllowanceCents", "0x0")]
    [InlineData("earlier14", "AllowanceCents", "0x1")] [InlineData("earlier14", "ArrearsCents", "0x10")]
    [InlineData("earlier14", "AllowanceCents", "0x0")]
    public void R8_NonCanonicalMoneyTokenIsInvalidInEveryFieldGeneration(string generation, string field, string token)
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        city.State.Food!.Residents.Single().Allowance = Money.FromCents(2);
        string current = SaveCodec.Write(city.State);
        string encoded = R8_Generation(current, generation);
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(current), LegacyMoneyWire.WithoutUpgradeFacts(SaveCodec.Write(SaveCodec.Read(encoded).State!)));
        string damaged = Regex.Replace(encoded, "\"" + field + "\":[0-9.]+", "\"" + field + "\":" + token);
        Assert.NotEqual(encoded, damaged);
        var read = SaveCodec.Read(damaged);
        Assert.True(read.Status == SaveReadStatus.Invalid, "Expected Invalid; got " + read.Status + "; allowance " + read.State?.Food?.Residents.Single().Allowance.ToCentsText()
            + "; arrears " + read.State?.Food?.Residents.Single().Budget?.Arrears.ToCentsText());
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = damaged };
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(damaged, data[SaveStore.StateKey]);
    }

    // Reviewer B N4: earlier schema-14 money with whitespace before a colon migrates like schema 13.
    [Fact]
    public void R8_EarlierSchema14MoneyMigratesWithWhitespaceBeforeColons()
    {
        var (_, current) = R8_Paid(.3f);
        string spaced = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(current), wholeCents: false), 14).ToString(Formatting.Indented)
            .Replace("\": ", "\"   :  ");
        var read = SaveCodec.Read(spaced);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(current), LegacyMoneyWire.WithoutUpgradeFacts(SaveCodec.Write(read.State!)));
    }

    private static ResidentBudget R8_BoundaryBudget(CitySimulator city, long fundedCents)
    {
        // Every amount fits, and their conservation totals sit just below the unit range.
        var b = city.State.Food!.Residents.Single().Budget!;
        b.BaseIncomeCents = b.DailyIncomeCents = 50000;
        b.DailyHousingCents = 3000; b.DailyEssentialsCents = 1500; b.DailyHomeFoodCents = 900;
        b.OpeningBalance = Money.FromCents(1000000);
        b.IncomeReceived = Money.FromCents(fundedCents) - b.OpeningBalance;
        b.Balance = Money.FromCents(17179869183);
        b.EssentialsPaid = Money.FromCents(fundedCents) - b.Balance;
        b.HomeFoodPaid = b.PreparedFoodSpent = b.HeldPurchase = b.Arrears = Money.Zero;
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
        return b;
    }

    // Reviewer A R7-03: each posted amount fit, but OpeningBalance + IncomeReceived did not, so an
    // accepted posting produced a ledger the save contract rejects. Refuse it atomically instead.
    [Fact]
    public void R8_PostingRefusesAConservationTotalTheSaveCannotHold()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var b = R8_BoundaryBudget(city, 34359738367);
        string before = JsonConvert.SerializeObject(b);
        Assert.Throws<OverflowException>(() => HouseholdFinance.PostDay(b, city.State.Minute, city.State.Citizens.Single()));
        Assert.Equal(before, JsonConvert.SerializeObject(b));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
    }

    // Control: a posting that reaches exactly the largest whole-cent total still posts and saves.
    [Fact]
    public void R8_PostingUpToTheLargestSavableTotalSucceeds()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var b = R8_BoundaryBudget(city, 34359738367 - 50000);
        HouseholdFinance.PostDay(b, city.State.Minute);
        Assert.Equal(Money.FromCents(34359738367), b.OpeningBalance + b.IncomeReceived);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city.State)).Status);
    }

    // Reviewer A N1: the wage refresh from the resident is part of the posting and is not kept
    // when the posting overflows.
    [Fact]
    public void R8_PostingWithAResidentIsAtomic()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var b = city.State.Food!.Residents.Single().Budget!;
        b.BaseIncomeCents = b.DailyIncomeCents = 50000;
        b.IncomeSource = ResidentIncomeSource.ExternalEmployment;
        b.Balance = b.OpeningBalance = Money.FromUnits(long.MaxValue);
        var person = city.State.Citizens.Single(); person.Identity.Persona = Persona.Retiree;
        string before = JsonConvert.SerializeObject(b);
        Assert.Throws<OverflowException>(() => HouseholdFinance.PostDay(b, city.State.Minute, person));
        Assert.Equal(before, JsonConvert.SerializeObject(b));
    }
}

public sealed class ShadowMoneyTokenTests
{
    // Both reviewers: the detached shadow document shares the token check, in every schema.
    [Theory]
    [InlineData(4, "DailyAllowanceCents", "0xA1")] [InlineData(5, "DailyAllowanceCents", "0xA1")]
    [InlineData(5, "DailyAllowance", "0x10")] [InlineData(4, "DailyAllowanceCents", "0x0")]
    public void R8_ShadowNonCanonicalMoneyTokenIsRefused(int schema, string field, string token)
    {
        var state = FoodDemandTests.Scenario(1, 1);
        string json = FoodShadowCodec.Write(state);
        if (field.EndsWith("Cents", StringComparison.Ordinal))
        {
            var legacy = LegacyMoneyWire.ToLegacy(JObject.Parse(json), wholeCents: schema == 4);
            legacy["Schema"] = schema;
            json = legacy.ToString(Formatting.None);
        }
        Assert.Equal(state.CityKey, FoodShadowCodec.Read(json, state.CityKey).CityKey);
        string damaged = Regex.Replace(json, "\"" + field + "\": ?[0-9.]+", "\"" + field + "\":" + token);
        Assert.NotEqual(json, damaged);
        FoodShadowState? loaded = null;
        var error = Record.Exception(() => { loaded = FoodShadowCodec.Read(damaged, state.CityKey); });
        Assert.True(error is InvalidDataException, "Expected refusal; got " + (error?.GetType().Name ?? "allowance " + loaded!.Consumers.Single().DailyAllowance.ToCentsText()));
    }
}
