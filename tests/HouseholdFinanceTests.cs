using System;
using PersistentCity.Core;
using Xunit;
namespace PersistentCity.Tests;
public sealed class HouseholdFinanceTests
{
    [Fact]
    public void ScarceIncomeFundsFoodBeforeHousingAndCreatesArrearsRatherThanCash()
    {
        var b = new ResidentBudget { DailyIncomeCents = 1200, DailyHomeFoodCents = 500,
            DailyHousingCents = 1000, DailyEssentialsCents = 200, NextPostingMinute = 0 };
        HouseholdFinance.PostDay(b, 0);
        Assert.Equal(0, b.BalanceCents); Assert.Equal(500, b.HomeFoodPaidCents);
        Assert.Equal(700, b.EssentialsPaidCents); Assert.Equal(500, b.ArrearsCents);
        Assert.Equal(0, HouseholdFinance.SpendingLimit(b));
        Assert.Throws<InvalidOperationException>(() => HouseholdFinance.PostDay(b, 0));
        HouseholdFinance.PostDay(b, 1440);
        Assert.Equal(1000, b.ArrearsCents); Assert.Equal(2400, b.IncomeReceivedCents);
        Assert.Equal(b.OpeningBalanceCents + b.IncomeReceivedCents,
            b.BalanceCents + b.HomeFoodPaidCents + b.EssentialsPaidCents);
    }
    [Fact]
    public void AnUnfundedBasketDoesNotBecomeFreeFood()
    {
        var b = new ResidentBudget { DailyIncomeCents = 100, DailyHomeFoodCents = 500,
            DailyHousingCents = 100, NextPostingMinute = 0 };
        HouseholdFinance.PostDay(b, 0);
        Assert.Equal(-1, b.HomeFoodFundedDay); Assert.Equal(0, b.HomeFoodPaidCents);
        Assert.Equal(1, b.DaysWithoutHomeFood); Assert.Equal(0, b.BalanceCents);
    }
    [Theory]
    [InlineData(Persona.Student, ResidentIncomeSource.ExternalSupport)]
    [InlineData(Persona.Retiree, ResidentIncomeSource.ExternalPension)]
    [InlineData(Persona.Commuter, ResidentIncomeSource.ExternalEmployment)]
    public void NonEarnersHaveExplicitIncomeSources(Persona persona, ResidentIncomeSource source)
    {
        var c = new CitizenState { Identity = new CitizenIdentity { Id = "r", Age = 30, Persona = persona }, DayPlaceId = "work" };
        var b = HouseholdFinance.Create(c, 1, 40, null);
        Assert.Equal(source, b.IncomeSource); Assert.True(b.DailyIncomeCents > 0);
        Assert.Equal(7L * b.DailyIncomeCents, b.OpeningBalanceCents);
        Assert.Equal(0, b.IncomeReceivedCents);
    }
    [Fact]
    public void JobLossAndRecoveryAffectOnlyNextUnpostedIncomeAndPreserveOriginalBasis()
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "worker", Age = 30, Persona = Persona.Commuter }, DayPlaceId = "work" };
        var budget = HouseholdFinance.Create(person, 7, 0, null);
        int basis = budget.BaseIncomeCents; long opening = budget.OpeningBalanceCents;
        HouseholdFinance.PostDay(budget, 0, person);
        long posted = budget.IncomeReceivedCents, balance = budget.BalanceCents;
        person.DayPlaceId = "";
        Assert.Equal(posted, budget.IncomeReceivedCents); Assert.Equal(balance, budget.BalanceCents);
        Assert.Throws<InvalidOperationException>(() => HouseholdFinance.PostDay(budget, 0, person));
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, budget.IncomeSource);
        budget = Newtonsoft.Json.JsonConvert.DeserializeObject<ResidentBudget>(Newtonsoft.Json.JsonConvert.SerializeObject(budget))!;
        HouseholdFinance.PostDay(budget, 1440, person);
        Assert.Equal(ResidentIncomeSource.ExternalSupport, budget.IncomeSource);
        Assert.Equal(basis * CityBalance.SupportIncomePercent / 100, budget.DailyIncomeCents);
        Assert.Equal(posted + budget.DailyIncomeCents, budget.IncomeReceivedCents);
        person.DayPlaceId = "new-work";
        HouseholdFinance.PostDay(budget, 2880, person);
        Assert.Equal(ResidentIncomeSource.ExternalEmployment, budget.IncomeSource);
        Assert.Equal(basis, budget.BaseIncomeCents); Assert.Equal(basis, budget.DailyIncomeCents);
        Assert.Equal(opening, budget.OpeningBalanceCents);
        Assert.Equal(budget.OpeningBalanceCents + budget.IncomeReceivedCents,
            budget.BalanceCents + budget.HomeFoodPaidCents + budget.EssentialsPaidCents + budget.PreparedFoodSpentCents + budget.HeldPurchaseCents);
    }

    [Fact]
    public void InitiallySeekingWorkerRetainsFullIncomeBasisForLaterEmployment()
    {
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "worker", Age = 30, Persona = Persona.Commuter } };
        var budget = HouseholdFinance.Create(person, 7, 0, null);
        Assert.Equal(budget.BaseIncomeCents * CityBalance.SupportIncomePercent / 100, budget.DailyIncomeCents);
        long opening = budget.OpeningBalanceCents;
        person.DayPlaceId = "work"; HouseholdFinance.PostDay(budget, 0, person);
        Assert.Equal(budget.BaseIncomeCents, budget.DailyIncomeCents); Assert.Equal(opening, budget.OpeningBalanceCents);
    }
}
