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
    private static ResidentBudget R8_BoundaryBudget(CitySimulator city, long fundedCents)
    {
        // Every amount fits, and their conservation totals sit just below the unit range.
        var b = city.State.Food!.Residents.Single().Budget!;
        b.IncomeSource = ResidentIncomeSource.ExternalSupport;
        b.BaseIncomeCents = b.DailyIncomeCents = 50000;
        b.DailyHousingCents = 3000; b.DailyEssentialsCents = 1500; b.DailyHomeFoodCents = 900;
        b.OpeningBalance = Money.FromCents(1000000);
        b.IncomeReceived = Money.FromCents(fundedCents) - b.OpeningBalance;
        b.Balance = Money.FromCents(17179869183);
        b.EssentialsPaid = Money.FromCents(fundedCents) - b.Balance;
        b.HomeFoodPaid = b.PreparedFoodSpent = b.HeldPurchase = b.Arrears = Money.Zero;
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Status);
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
        Assert.Throws<OverflowException>(() => HouseholdFinance.PostDay(b, city.State.Minute));
        Assert.Equal(before, JsonConvert.SerializeObject(b));
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Status);
    }

    // Control: a posting that reaches exactly the largest whole-cent total still posts and saves.
    [Fact]
    public void R8_PostingUpToTheLargestSavableTotalSucceeds()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var b = R8_BoundaryBudget(city, 34359738367 - 50000);
        HouseholdFinance.PostDay(b, city.State.Minute);
        Assert.Equal(Money.FromCents(34359738367), b.OpeningBalance + b.IncomeReceived);
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Status);
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
            var legacy = LegacyMoneyWire.ToLegacy(SaveWire.Parse(json), wholeCents: schema == 4);
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
